using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// The single holder of civic memory for one save, and the rules by which it changes hands.
	/// <para>
	/// Two habits do most of the work. Reads hand back a copy, so no caller can edit this state by
	/// keeping hold of something it was shown. Commits carry the revision they were built against,
	/// so a caller that read, thought, and came back late is told its answer is stale instead of
	/// silently overwriting whatever happened while it was thinking.
	/// </para>
	/// <para>
	/// A refusal is never a reset. Records that cannot be read are kept as quarantined evidence
	/// and defended against overwrite, because the failure mode that actually costs somebody their
	/// city is not a save that refuses to load &mdash; it is a save that loads as empty and then
	/// writes that back.
	/// </para>
	/// </summary>
	public sealed partial class KingdomCivicMemoryAuthority : IKingdomCivicMemoryAuthority
	{
		private KingdomCivicMemoryState Current = KingdomCivicMemoryState.Empty();

		private readonly KingdomCivicMemoryFamilyTable Families;
		private readonly object MutationGate = new object();
		private bool MutationInProgress;

		/// <summary>Once a save state or a successful live commit establishes this authority, no
		/// later adoption call may replace it. The first bytes remain the evidence.</summary>
		private bool Established;

		/// <summary>
		/// Builds an authority that puts every known section to the family that owns it.
		/// </summary>
		/// <param name="Families">
		/// Who answers for which section id. Passed in rather than named inside, so that
		/// everything this class does can be exercised without a game running &mdash; see
		/// <see cref="KingdomCivicMemoryFamilyTable"/>.
		/// </param>
		public KingdomCivicMemoryAuthority(KingdomCivicMemoryFamilyTable Families)
		{
			if (Families == null) throw new ArgumentNullException("Families");
			this.Families = Families;
		}

		/// <summary>
		/// Thrown only by the failed-read paths, and never thrown back. See
		/// <see cref="KingdomCivicMemoryLatch"/> for why it is a class and not a field.
		/// </summary>
		public readonly KingdomCivicMemoryLatch Latch = new KingdomCivicMemoryLatch();

		/// <summary>The revision a commit must name to be accepted.</summary>
		public long Revision => Current.Revision;

		/// <summary>Whether the records this authority holds were refused rather than read.</summary>
		public bool Quarantined => Current.Quarantined;

		/// <summary>Whether this save was written by a build newer than ours.</summary>
		public bool IsFutureOuter => Current.IsFutureOuter;

		/// <summary>Nothing held and nothing lost &mdash; a save written before this authority.</summary>
		public bool IsEmpty => Current.IsEmpty;

		/// <summary>Whether this authority may still accept a commit.</summary>
		public bool ReadOnly => Latch.Tripped || Current.ReadOnly || !Families.Complete;

		/// <summary>Whether every known section has its owning family reader.</summary>
		public bool FamiliesComplete => Families.Complete;

		/// <summary>Why this authority is read-only, including lawful future state.</summary>
		public string ReadOnlyReason
		{
			get
			{
				if (Latch.Tripped) return Latch.Reason;
				if (Current.IsFutureOuter) return "civic memory was written by a newer build "
					+ "(envelope version " + Current.OuterVersion + ")";
				if (!Families.Complete) return "civic memory is missing a known family reader";
				return Current.Quarantined ? Current.Fault : "";
			}
		}

		/// <summary>A copy of the current state. The caller owns everything it is handed.</summary>
		public KingdomCivicMemoryState Read()
		{
			return Current.Copy();
		}

		/// <summary>
		/// Takes the bytes found in the save block.
		/// <para>
		/// Three outcomes. An envelope from a later build is adopted whole and held read-only
		/// without complaint. An envelope this build can read is put to every family that owns a
		/// section in it, and is adopted only if all of them recognise their own payload. Anything
		/// else &mdash; unreadable framing, a failed hash, or a known section its own codec
		/// refuses &mdash; is quarantined whole and latches the session.
		/// </para>
		/// <para>
		/// That middle case is the one worth naming. A valid outer hash around a payload the
		/// owning family rejects is still unusable. The hash detects accidental change but does not
		/// prove a producer, so the cause may be a writer defect, deliberate edit, or recomputed
		/// corruption; every case means this session must not save over the original.
		/// </para>
		/// </summary>
		/// <param name="Bytes">The envelope as it was written. Never null on this path.</param>
		public void AdoptSaved(byte[] Bytes)
		{
			lock (MutationGate)
			{
				if (!EnterMutation("adopt a saved envelope")) return;
				try { AdoptSavedCore(Bytes); }
				finally { MutationInProgress = false; }
			}
		}

		private void AdoptSavedCore(byte[] Bytes)
		{
			if (!BeginAdoption("saved envelope")) return;
			long adoptionRevision = Current.Revision + 1L;
			KingdomCivicMemoryState decoded;
			try
			{
				decoded = KingdomCivicMemoryCodec.Decode(Bytes, adoptionRevision);
			}
			catch (Exception e)
			{
				Quarantine(Bytes, e.Message, adoptionRevision);
				return;
			}
			if (decoded.IsFutureOuter)
			{
				Current = decoded;
				return;
			}
			try
			{
				string nested;
				if (!Recognised(decoded, out nested))
				{
					Quarantine(Bytes, nested, adoptionRevision);
					return;
				}
			}
			catch (Exception e)
			{
				Quarantine(Bytes, "a known civic-memory family could not inspect its payload ("
					+ e.Message + ")", adoptionRevision);
				return;
			}
			Current = decoded;
		}

		/// <summary>
		/// Records a block whose own framing could not be read, keeping whatever of it was
		/// actually recovered and the true reason it failed.
		/// <para>
		/// This exists because the latch is one-way and first-cause-wins. A path that quarantined
		/// through the ordinary decode route would trip the latch with whatever complaint that
		/// decode produced about a stand-in payload, and the real reason &mdash; a wrong marker, an
		/// impossible length &mdash; would be lost behind it. So this sets the true cause first and
		/// never decodes anything.
		/// </para>
		/// </summary>
		/// <param name="Evidence">The bytes recovered before the framing failed. Kept as-is.</param>
		/// <param name="Cause">The real reason, already known to the caller.</param>
		public void AdoptUnreadableFraming(byte[] Evidence, string Cause)
		{
			lock (MutationGate)
			{
				if (!EnterMutation("adopt unreadable block framing")) return;
				try
				{
					if (!BeginAdoption("unreadable block framing")) return;
					string cause = string.IsNullOrEmpty(Cause)
						? "the civic memory block's framing could not be read" : Cause;
					Latch.Trip(cause);
					Current = KingdomCivicMemoryState.Quarantine(Evidence, cause,
						Current.Revision + 1L);
				}
				finally { MutationInProgress = false; }
			}
		}

		/// <summary>
		/// The one lawful empty: a save that predates this authority and carried no block at all.
		/// It is not a recovery path and must never be reached from a failed read.
		/// </summary>
		public void AdoptAbsent()
		{
			lock (MutationGate)
			{
				if (!EnterMutation("adopt an absent save block")) return;
				try
				{
					if (!BeginAdoption("absent save block")) return;
					Current = KingdomCivicMemoryState.Empty().AtRevision(Current.Revision + 1L);
				}
				finally { MutationInProgress = false; }
			}
		}

		private bool EnterMutation(string Operation)
		{
			if (!MutationInProgress)
			{
				MutationInProgress = true;
				return true;
			}
			Latch.Trip("civic memory refused a re-entrant attempt to " + Operation);
			return false;
		}

		private bool BeginAdoption(string Source)
		{
			if (!Established)
			{
				Established = true;
				return true;
			}
			Latch.Trip("civic memory was asked to adopt " + Source
				+ " after its state was already established; the first state was retained");
			return false;
		}

		/// <summary>The bytes to write. A future save returns exactly what it arrived as.</summary>
		public byte[] Encode()
		{
			return KingdomCivicMemoryCodec.Encode(Current);
		}

		/// <summary>Puts every known section to the family that owns it.</summary>
		private bool Recognised(KingdomCivicMemoryState State, out string Fault)
		{
			List<KingdomCivicMemorySection> sections = State.Sections();
			for (int i = 0; i < sections.Count; i++)
			{
				KingdomCivicMemorySection section = sections[i];
				if (!section.KnownToThisBuild) continue;
				string nested;
				KingdomCivicMemoryNested verdict = InspectStable(section.Id,
					section.Payload(), out nested);
				if (verdict == KingdomCivicMemoryNested.Current
					|| verdict == KingdomCivicMemoryNested.Future) continue;
				Fault = verdict == KingdomCivicMemoryNested.Malformed ? nested
					: "civic-memory family for section " + section.Id
						+ " returned unsupported verdict " + (int)verdict;
				return false;
			}
			Fault = "";
			return true;
		}

		/// <summary>Keeps the bytes, records the cause, and latches the session read-only.</summary>
		private void Quarantine(byte[] Bytes, string Cause, long Revision)
		{
			string cause = string.IsNullOrEmpty(Cause)
				? "civic memory could not be read from this save" : Cause;
			Latch.Trip(cause);
			Current = KingdomCivicMemoryState.Quarantine(Bytes, cause, Revision);
		}
	}
}
