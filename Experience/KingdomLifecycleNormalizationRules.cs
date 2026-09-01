using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		public static void Normalize(KingdomLifecycleBook Book)
		{
			if (Book == null) return;
			if (PristineLifecycleBook(Book)) return;
			if (CanonicalLifecycleQuarantine(Book)) return;
			if (Book.FormatVersion != CurrentFormatVersion)
			{
				Book.WireRejected = true;
				Deny(Book, "unsupported lifecycle book version");
				return;
			}
			bool bad = Book.WireRejected || !ValidRootId(Book.SettlementId)
				|| !Book.IdentityBound || !ExactSettlementIdentityProof(Book)
				|| (Book.LegacyIdentity ? !ValidRootId(Book.LegacyMigrationKey)
					: !string.IsNullOrEmpty(Book.LegacyMigrationKey))
				|| !CounterShape(Book.PlainGuestNextSequence, Book.PlainGuestRetiredThrough)
				|| !CounterShape(Book.NotableGuestNextSequence, Book.NotableGuestRetiredThrough)
				|| !CounterShape(Book.RaidNextSequence, Book.RaidRetiredThrough)
				|| !CounterShape(Book.PetitionNextSequence, Book.PetitionRetiredThrough)
				|| !KnownOption(Book.LocusOption) || !KnownOption(Book.NotableOption)
				|| !KnownOption(Book.RaidOption) || !KnownOption(Book.PetitionOption)
				|| Book.LocusOptionTick < 0L || Book.NotableOptionTick < 0L
				|| Book.RaidOptionTick < 0L || Book.PetitionOptionTick < 0L
				|| TooLong(Book.Fault, MaxTextChars)
				|| Book.Resources == null || Book.Resources.Count > MaxResourceRows
				|| Book.RecentProofs == null || Book.RecentProofs.Count > MaxRecentProofs
				|| !KingdomRaidIncidentRules.ValidLedger(Book.RaidLedger);

			HashSet<string> resourceKeys = new HashSet<string>(StringComparer.Ordinal);
			if (Book.Resources != null && Book.Resources.Count <= MaxResourceRows)
			{
				for (int i = 0; i < Book.Resources.Count; i++)
				{
					KingdomLifecycleResourceRevision row = Book.Resources[i];
					if (!ResourceShape(row) || !resourceKeys.Add(row.Key)) bad = true;
				}
			}

			if (!NormalizeOperation(Book, Book.PlainGuest, KingdomLifecycleLane.PlainGuest)) bad = true;
			if (!NormalizeOperation(Book, Book.NotableGuest, KingdomLifecycleLane.NotableGuest)) bad = true;
			if (!NormalizeOperation(Book, Book.Raid, KingdomLifecycleLane.Raid)) bad = true;
			if (!NormalizeOperation(Book, Book.Petition, KingdomLifecycleLane.Petition)) bad = true;
			if (!LaneSequenceValid(Book, KingdomLifecycleLane.PlainGuest, Book.PlainGuest)
				|| !LaneSequenceValid(Book, KingdomLifecycleLane.NotableGuest, Book.NotableGuest)
				|| !LaneSequenceValid(Book, KingdomLifecycleLane.Raid, Book.Raid)
				|| !LaneSequenceValid(Book, KingdomLifecycleLane.Petition, Book.Petition)) bad = true;
			if (!ProofListValid(Book)) bad = true;
			if (!ActiveResourcesValid(Book)) bad = true;
			if (bad) Deny(Book, "malformed lifecycle authority was quarantined without reinterpretation");
		}

		public static void Normalize(KingdomCarryBook Book)
		{
			if (Book == null) return;
			if (Book.OpaquePayload != null)
			{
				if (Book.FormatVersion == CurrentCarryFormatVersion && !Book.WireRejected
					&& Book.Quarantined && Book.OpaqueWireVersion > CurrentCarryFormatVersion
					&& Book.OpaquePayload.Length <= MaxCarrySectionBytes
					&& !string.IsNullOrEmpty(Book.Fault) && !TooLong(Book.Fault, MaxTextChars)) return;
				Deny(Book, "malformed opaque carry evidence was quarantined");
				return;
			}
			if (PristineCarryBook(Book)) return;
			if (Book.FormatVersion != CurrentCarryFormatVersion)
			{
				Book.WireRejected = true;
				Deny(Book, "unsupported carry book version");
				return;
			}
			bool bad = Book.WireRejected || !ValidRootId(Book.RealmId)
				|| !Book.IdentityBound || !ExactCarryIdentityProof(Book)
				|| (Book.LegacyIdentity ? !ValidRootId(Book.LegacyMigrationKey)
					: !string.IsNullOrEmpty(Book.LegacyMigrationKey))
				|| !CounterShape(Book.NextSequence, Book.RetiredThrough)
				|| TooLong(Book.Fault, MaxTextChars)
				|| !CarrySettlementSetShape(Book)
				|| !CarryResourceRegistryValid(Book)
				|| Book.RecentProofs == null || Book.RecentProofs.Count > MaxRecentProofs
				|| !CarryProofListValid(Book);
			if (Book.Open != null)
			{
				KingdomCarryOperation op = Book.Open;
				string hash;
					bool opBad = !CarrySequenceValid(Book)
						|| !string.Equals(op.Id, CarryId(Book.RealmId, op.Sequence), StringComparison.Ordinal)
						|| !ExactStringList(op.SettlementIds, Book.SettlementIds)
						|| !string.Equals(op.RealmTopologyHash,
							RealmTopologyDigest(Book.RealmId, Book.SettlementIds), StringComparison.Ordinal)
					|| !CarryPhaseAllowed(op.Phase) || op.CreatedTick < 0L
					|| op.UpdatedTick < op.CreatedTick || TooLong(op.Fault, MaxTextChars)
					|| !CarryPlanShape(op, false) || !TryCarryPlanHash(op, out hash)
					|| !string.Equals(op.PlanHash, hash, StringComparison.Ordinal)
					|| !SettlementMember(Book, op.OriginSettlementId)
					|| !SettlementMember(Book, op.DestinationSettlementId)
					|| !CarryConserved(op) || !CarryPhaseProgressValid(op);
				if (opBad)
				{
					if (KnownPhase(op.Phase)) Quarantine(op,
						"malformed carry operation was denied authority");
					bad = true;
				}
			}
			else if (!CarrySequenceValid(Book)) bad = true;
			if (!CarryActiveResourcesValid(Book)) bad = true;
			if (bad) Deny(Book, "malformed carry authority was quarantined without reinterpretation");
		}

		private static bool NormalizeOperation(KingdomLifecycleBook Book,
			KingdomLifecycleOperation Operation, KingdomLifecycleLane ExpectedLane)
		{
			if (Operation == null) return true;
			string hash;
			bool knownPhase = KnownPhase(Operation.Phase);
			bool good = Operation.Lane == ExpectedLane
					&& ActionAllowedInLane(Operation.Action, ExpectedLane)
					&& LaneSequenceValid(Book, ExpectedLane, Operation)
				&& CanonicalOperationId(Operation)
				&& string.Equals(Operation.SettlementId, Book.SettlementId, StringComparison.Ordinal)
				&& knownPhase && PhaseAllowed(Operation.Action, Operation.Phase)
				&& Operation.CreatedTick >= 0L && Operation.UpdatedTick >= Operation.CreatedTick
					&& !TooLong(Operation.Fault, MaxTextChars)
					&& PlanShape(Operation, false)
					&& LifecyclePhaseProgressValid(Operation)
					&& TryPlanHash(Operation, out hash)
				&& string.Equals(Operation.PlanHash, hash, StringComparison.Ordinal);
			if (!good && knownPhase) Quarantine(Operation,
				"malformed lifecycle operation was denied authority");
			return good;
		}

		private static bool PublicationPlanValid(KingdomLifecycleOperation Operation)
		{
			return PlanShape(Operation, true)
				&& Operation.CreatedTick == Operation.UpdatedTick
				&& Operation.Phase == KingdomLifecyclePhase.Prepared;
		}

	}
}
