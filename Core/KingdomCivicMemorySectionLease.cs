using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// One copy-isolated reading of one known civic-memory section.
	/// <para>
	/// A lease belongs to the authority that issued it and names that authority's revision. It may
	/// therefore be committed only to the same authority and only while no other section commit has
	/// moved the envelope. The payload is copied both into and out of the lease.
	/// </para>
	/// </summary>
	public sealed class KingdomCivicMemorySectionLease
	{
		private readonly KingdomCivicMemoryAuthority Owner;
		private readonly byte[] Bytes;

		public int SectionId { get; private set; }
		public long ExpectedRevision { get; private set; }
		public bool Present { get; private set; }

		internal KingdomCivicMemorySectionLease(KingdomCivicMemoryAuthority Owner,
			int SectionId, long ExpectedRevision, byte[] Bytes)
		{
			this.Owner = Owner;
			this.SectionId = SectionId;
			this.ExpectedRevision = ExpectedRevision;
			Present = Bytes != null;
			this.Bytes = Bytes == null ? null : (byte[])Bytes.Clone();
		}

		/// <summary>The caller's own payload copy, or an empty array when the section is absent.</summary>
		public byte[] Payload()
		{
			return Bytes == null ? new byte[0] : (byte[])Bytes.Clone();
		}

		internal bool IssuedBy(KingdomCivicMemoryAuthority Authority)
		{
			return ReferenceEquals(Owner, Authority);
		}
	}

	public sealed partial class KingdomCivicMemoryAuthority
	{
		/// <summary>
		/// Opens one known, current section for a typed family adapter.
		/// <para>
		/// Missing is a successful, explicit result so a family may construct its canonical empty
		/// book. A nested-future section is not missing and is not handed to an older mutator. A
		/// family reader that changes its mind after load latches the authority instead of letting
		/// new bytes be based on a payload it now refuses.
		/// </para>
		/// </summary>
		public bool TryReadSection(int SectionId, out KingdomCivicMemorySectionLease Lease,
			out string Failure)
		{
			Lease = null;
			Failure = "";
			lock (MutationGate)
			{
				// A family reader is injected code. Monitor locks are re-entrant, so the gate alone
				// cannot stop that reader from opening another lease or committing while this read is
				// still deciding what the held bytes mean. Share the mutation barrier for the whole
				// operation and release only the scope this call successfully acquired.
				if (!EnterMutation("read section " + SectionId))
				{
					Failure = "civic memory refused a re-entrant section read";
					return false;
				}
				try
				{
					if (!KingdomCivicMemoryLimits.Known(SectionId))
					{
						Failure = "civic memory section " + SectionId
							+ " is not a known section this build may interpret";
						return false;
					}
					if (ReadOnly)
					{
						string reason = ReadOnlyReason;
						Failure = "civic memory is read-only"
							+ (string.IsNullOrEmpty(reason) ? "" : " (" + reason + ")");
						return false;
					}
					// State and sections are immutable; only the selected payload needs a caller-owned
					// copy. Copying the whole envelope here would make an unrelated wide treaty section
					// tax every small civic-memory read.
					KingdomCivicMemoryState snapshot = Current;
					KingdomCivicMemorySection section = snapshot.Section(SectionId);
					if (section == null)
					{
						KingdomCivicMemoryState afterMissingRead = Current;
						if (!ReferenceEquals(afterMissingRead, snapshot)
							|| afterMissingRead.Revision != snapshot.Revision)
						{
							Failure = "civic memory moved while absent section " + SectionId
								+ " was inspected";
							Latch.Trip(Failure);
							return false;
						}
						Lease = new KingdomCivicMemorySectionLease(this, SectionId,
							snapshot.Revision, null);
						return true;
					}
					byte[] payload = section.Payload();
					KingdomCivicMemoryNested verdict;
					string nested;
					try { verdict = InspectStable(SectionId, payload, out nested); }
					catch (Exception error) when (RecoverableInspectionFailure(error))
					{
						Failure = "civic-memory family for section " + SectionId
							+ " could not inspect its held payload (" + error.Message + ")";
						Latch.Trip(Failure);
						return false;
					}
					if (Latch.Tripped)
					{
						Failure = "civic memory became read-only while section " + SectionId
							+ " was inspected (" + Latch.Reason + ")";
						return false;
					}
					KingdomCivicMemoryState afterInspection = Current;
					if (!ReferenceEquals(afterInspection, snapshot)
						|| afterInspection.Revision != snapshot.Revision)
					{
						Failure = "civic memory moved while section " + SectionId + " was inspected";
						Latch.Trip(Failure);
						return false;
					}
					if (verdict == KingdomCivicMemoryNested.Future)
					{
						Failure = "civic memory section " + SectionId
							+ " was written by a newer family version and is carried, not edited";
						return false;
					}
					if (verdict != KingdomCivicMemoryNested.Current)
					{
						Failure = "civic memory section " + SectionId + " is no longer recognised ("
							+ (verdict == KingdomCivicMemoryNested.Malformed
								? (string.IsNullOrEmpty(nested) ? "no reason given" : nested)
								: "unsupported family verdict " + (int)verdict) + ")";
						Latch.Trip(Failure);
						return false;
					}
					Lease = new KingdomCivicMemorySectionLease(this, SectionId,
						snapshot.Revision, payload);
					return true;
				}
				finally { MutationInProgress = false; }
			}
		}

		/// <summary>Offers one replacement payload under the exact lease that produced it.</summary>
		public bool TryCommitSection(KingdomCivicMemorySectionLease Lease, byte[] Payload,
			out string Failure)
		{
			Failure = "";
			if (Lease == null)
			{
				Failure = "civic memory was offered no section lease";
				return false;
			}
			if (!Lease.IssuedBy(this))
			{
				Failure = "civic memory refused a section lease issued by another authority";
				return false;
			}
			if (Payload == null)
			{
				Failure = "civic memory was offered no replacement payload for section "
					+ Lease.SectionId;
				return false;
			}
			return TryCommit(new List<KingdomCivicMemorySection>
			{
				new KingdomCivicMemorySection(Lease.SectionId, Payload)
			}, Lease.ExpectedRevision, out Failure);
		}
	}
}
