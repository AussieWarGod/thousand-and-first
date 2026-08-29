using System.Collections.Generic;
using System.IO;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCivicMemoryAuthority
	{
		/// <summary>
		/// Offers replacement payloads for named known sections.
		/// <para>
		/// A commit is an upsert, not a replacement of everything. Sections the caller did not
		/// name are carried through byte-for-byte, which is what keeps two kinds of content alive
		/// that no caller in this build is able to reconstruct: sections whose ids this build does
		/// not recognise, and known sections whose payloads their own families report as newer
		/// than themselves. Neither may be written by anyone here, so neither may be lost by
		/// anyone here either.
		/// </para>
		/// <para>
		/// Every check runs against a candidate that has not been installed, and the field is
		/// assigned only once all of them pass. There is no path where a rejected commit leaves
		/// this authority half-changed.
		/// </para>
		/// </summary>
		/// <param name="Candidate">Sections to add or replace. Known ids only.</param>
		/// <param name="ExpectedRevision">The revision the caller last read.</param>
		/// <param name="Failure">Why the commit was refused, or an empty string.</param>
		public bool TryCommit(IList<KingdomCivicMemorySection> Candidate, long ExpectedRevision,
			out string Failure)
		{
			lock (MutationGate)
			{
				if (!EnterMutation("commit sections"))
				{
					Failure = "civic memory refused a re-entrant commit";
					return false;
				}
				try
				{
					List<KingdomCivicMemorySection> snapshot;
					if (!Snapshot(Candidate, out snapshot, out Failure)) return false;
					return TryCommitSnapshot(snapshot, ExpectedRevision, out Failure);
				}
				finally { MutationInProgress = false; }
			}
		}

		private bool TryCommitSnapshot(List<KingdomCivicMemorySection> Candidate,
			long ExpectedRevision, out string Failure)
		{
			if (!Families.Complete)
			{
				Failure = "civic memory is missing a reader for at least one known section; "
					+ "no commit is safe until its family table is complete";
				return false;
			}
			// Quarantine is asked about first, and deliberately. Every path that quarantines also
			// latches, so both would refuse this commit either way -- but only one of them can
			// tell the founder that their records still exist and are being defended, rather than
			// that the session has gone read-only for some reason. The more specific answer is
			// the more useful one, so it goes first.
			if (Current.Quarantined)
			{
				Failure = "civic memory is holding records it could not read (" + Current.Fault
					+ "); overwriting them would destroy the only copy";
				return false;
			}
			if (Latch.Tripped)
			{
				Failure = "civic memory is read-only for this session: " + Latch.Reason;
				return false;
			}
			if (Current.IsFutureOuter)
			{
				Failure = "civic memory in this save was written by a newer build (envelope "
					+ "version " + Current.OuterVersion + "); this build may carry it but must "
					+ "not rewrite it";
				return false;
			}
			if (ExpectedRevision != Current.Revision)
			{
				Failure = "civic memory moved to revision " + Current.Revision
					+ " while this change was being prepared against revision " + ExpectedRevision;
				return false;
			}
			// The counter is the whole basis of the staleness check. A wrapped one would let a
			// much older caller match by accident, so it is refused rather than allowed to turn
			// over.
			if (Current.Revision == long.MaxValue)
			{
				Failure = "civic memory has reached the last revision this counter can express "
					+ "and will not wrap";
				return false;
			}
			if (Candidate.Count == 0)
			{
				Failure = "civic memory was offered no sections to commit";
				return false;
			}
			try
			{
				if (!Admissible(Candidate, out Failure)) return false;
			}
			catch (System.Exception e) when (RecoverableInspectionFailure(e))
			{
				Failure = "a civic-memory family could not inspect a proposed commit ("
					+ e.Message + ")";
				Latch.Trip(Failure);
				return false;
			}
			// Family readers are injected code. A re-entrant mutation attempt trips the latch;
			// a concurrent caller cannot enter the gate. Recheck immediately before installation.
			if (Latch.Tripped)
			{
				Failure = "civic memory became read-only while this commit was inspected: "
					+ Latch.Reason;
				return false;
			}
			if (ExpectedRevision != Current.Revision)
			{
				Failure = "civic memory moved while this commit was inspected";
				return false;
			}
			return Install(Candidate, out Failure);
		}

		/// <summary>Whether every offered section is one a caller in this build may author.</summary>
		private bool Admissible(IList<KingdomCivicMemorySection> Candidate, out string Failure)
		{
			HashSet<int> seen = new HashSet<int>();
			for (int i = 0; i < Candidate.Count; i++)
			{
				KingdomCivicMemorySection section = Candidate[i];
				if (section == null)
				{
					Failure = "civic memory was offered an absent section at position " + i;
					return false;
				}
				if (!KingdomCivicMemoryLimits.Allocatable(section.Id))
				{
					Failure = "civic memory section id " + section.Id + " is below the first id "
						+ "that could ever be allocated";
					return false;
				}
				if (!seen.Add(section.Id))
				{
					Failure = "civic memory was offered section " + section.Id + " twice in one "
						+ "commit; which one was meant cannot be guessed";
					return false;
				}
				if (!section.KnownToThisBuild)
				{
					Failure = "civic memory section " + section.Id + " is not a section this "
						+ "build understands; unknown sections are carried, never authored";
					return false;
				}
				if (!Authorable(section, out Failure)) return false;
				if (!Replaceable(section.Id, out Failure)) return false;
			}
			Failure = "";
			return true;
		}

		/// <summary>Whether the offered payload is something its own family recognises today.</summary>
		private bool Authorable(KingdomCivicMemorySection Section, out string Failure)
		{
			string nested;
			KingdomCivicMemoryNested verdict = InspectStable(Section.Id,
				Section.Payload(), out nested);
			if (verdict == KingdomCivicMemoryNested.Current)
			{
				Failure = "";
				return true;
			}
			if (verdict == KingdomCivicMemoryNested.Future)
			{
				Failure = "civic memory refused a commit to section " + Section.Id + ": the "
					+ "payload reads as newer than this build, which no caller here can have "
					+ "authored";
				return false;
			}
			Failure = "civic memory refused a commit to section " + Section.Id + ": "
				+ (verdict == KingdomCivicMemoryNested.Malformed ? nested
					: "its family returned unsupported verdict " + (int)verdict);
			return false;
		}

		/// <summary>
		/// Whether what is already in that section may lawfully be written over. A payload its own
		/// family reports as newer than this build is the family's forward-compatibility promise;
		/// replacing it here would break that promise on the family's behalf.
		/// </summary>
		private bool Replaceable(int Id, out string Failure)
		{
			KingdomCivicMemorySection existing = Current.Section(Id);
			if (existing == null)
			{
				Failure = "";
				return true;
			}
			string nested;
			KingdomCivicMemoryNested verdict = InspectStable(Id,
				existing.Payload(), out nested);
			if (verdict == KingdomCivicMemoryNested.Current)
			{
				Failure = "";
				return true;
			}
			if (verdict == KingdomCivicMemoryNested.Future)
			{
				Failure = "civic memory section " + Id + " already holds a payload newer than "
					+ "this build; it is carried, never replaced";
				return false;
			}
			Failure = "civic memory section " + Id + " already holds a payload its own codec "
				+ (verdict == KingdomCivicMemoryNested.Malformed ? "refuses (" + nested + ")"
					: "reported with unsupported verdict " + (int)verdict)
				+ "; it is kept as evidence, never replaced";
			return false;
		}

		/// <summary>Merges the candidate over the held sections and installs the result.</summary>
		private bool Install(IList<KingdomCivicMemorySection> Candidate, out string Failure)
		{
			Dictionary<int, KingdomCivicMemorySection> merged =
				new Dictionary<int, KingdomCivicMemorySection>();
			List<KingdomCivicMemorySection> held = Current.Sections();
			for (int i = 0; i < held.Count; i++) merged[held[i].Id] = held[i];
			for (int i = 0; i < Candidate.Count; i++) merged[Candidate[i].Id] = Candidate[i];

			List<KingdomCivicMemorySection> sections =
				new List<KingdomCivicMemorySection>(merged.Values);
			if (sections.Count > KingdomCivicMemoryLimits.MaxSections)
			{
				Failure = "civic memory would carry " + sections.Count + " sections, more than "
					+ "the envelope reserves room for";
				return false;
			}
			KingdomCivicMemoryState candidate = KingdomCivicMemoryState.Of(sections,
				Current.Revision);
			try
			{
				// Proving the candidate by writing it is the only honest check: a state that
				// cannot be encoded is a state that would be lost at the next save, and finding
				// that out here costs nothing while finding it out there costs the save.
				KingdomCivicMemoryCodec.Encode(candidate);
			}
			catch (InvalidDataException e)
			{
				Failure = e.Message;
				return false;
			}
			Current = candidate.AtRevision(Current.Revision + 1);
			Established = true;
			Failure = "";
			return true;
		}
	}
}
