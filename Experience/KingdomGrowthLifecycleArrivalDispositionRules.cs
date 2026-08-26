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
		internal static bool BeginGrowthArrivalCandidateDisposition(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, string ConsumingOperationId,
			KingdomGrowthObjectMutationKind Kind, KingdomGrowthLocationKind ToLocation,
			string OwnerId, string ZoneId, int X, int Y, string BeforeOwnerGraphHash,
			string AfterOwnerGraphHash, string BeforeObjectGraphHash,
			string AfterObjectGraphHash, string BeforeTopologyHash, string AfterTopologyHash,
			long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.Observed
				|| Tick < Candidate.UpdatedTick || !GrowthWitnessHash(BeforeOwnerGraphHash)
				|| !GrowthWitnessHash(AfterOwnerGraphHash)
				|| !GrowthWitnessHash(BeforeObjectGraphHash)
				|| !GrowthWitnessHash(AfterObjectGraphHash)
				|| !GrowthWitnessHash(BeforeTopologyHash)
				|| !GrowthWitnessHash(AfterTopologyHash)) return false;
			bool joined = Candidate.Disposition == KingdomGrowthArrivalDisposition.Joined;
			KingdomGrowthOperation operation = Book.ArrivalOp;
			if (operation == null || !ReferenceEquals(Book.ArrivalOp, operation)
				|| !string.Equals(operation.Id, ConsumingOperationId, StringComparison.Ordinal)
				|| !string.Equals(operation.ArrivalCandidateId, Candidate.Id,
					StringComparison.Ordinal)
				|| operation.ArrivalDisposition != Candidate.Disposition
				|| (operation.Phase != KingdomGrowthPhase.Prepared
					&& operation.Phase != KingdomGrowthPhase.WaterIntent
					&& operation.Phase != KingdomGrowthPhase.WaterSettled)
				|| !ExactGrowthOperationAuthority(Book, operation)) return false;
			if (joined ? !ValidGeneratedId(ConsumingOperationId)
				|| Kind != KingdomGrowthObjectMutationKind.CellAdd
				|| ToLocation != KingdomGrowthLocationKind.Cell || OwnerId != null
				|| !string.Equals(ZoneId, Candidate.LodgingZoneId,
					StringComparison.Ordinal) || X != Candidate.LodgingX || Y != Candidate.LodgingY
				|| !string.Equals(ZoneId, operation.ZoneId, StringComparison.Ordinal)
				|| X != operation.TargetX || Y != operation.TargetY
				|| !GrowthLocationShape(ToLocation, OwnerId, ZoneId, X, Y)
				: Candidate.Disposition != KingdomGrowthArrivalDisposition.NoAcceptableHome
					|| !ValidGeneratedId(ConsumingOperationId)
					|| Kind != KingdomGrowthObjectMutationKind.Obliterate
					|| ToLocation != KingdomGrowthLocationKind.Graveyard
					|| !GrowthLocationShape(ToLocation, OwnerId, ZoneId, X, Y)) return false;
			KingdomGrowthObjectCallbackStep step = new KingdomGrowthObjectCallbackStep
			{
				EventId = ChildId(Candidate.Id, "object-callback", 1), Kind = Kind,
				FromLocation = KingdomGrowthLocationKind.Escrow, ToLocation = ToLocation,
				EscrowKey = Candidate.EscrowKey, BeforeX = -1, BeforeY = -1,
				AfterOwnerId = OwnerId, AfterZoneId = ZoneId, AfterX = X, AfterY = Y,
				BeforeCount = 1, AfterCount = joined ? 1 : 0, NoStack = joined,
				BeforeOwnerGraphHash = BeforeOwnerGraphHash,
				AfterOwnerGraphHash = AfterOwnerGraphHash,
				BeforeObjectGraphHash = BeforeObjectGraphHash,
				AfterObjectGraphHash = AfterObjectGraphHash,
				BeforeTopologyHash = BeforeTopologyHash, AfterTopologyHash = AfterTopologyHash,
				State = KingdomLifecyclePhysicalState.Intent,
				ReceiptId = ChildId(Candidate.Id, "object-callback-receipt", 1),
				ReceiptBeforeMatches = 1, ReceiptBeforeCount = 1,
				ReceiptBeforeOwnerGraphHash = BeforeOwnerGraphHash,
				ReceiptBeforeObjectGraphHash = BeforeObjectGraphHash,
				ReceiptBeforeTopologyHash = BeforeTopologyHash,
				ReceiptState = KingdomLifecyclePhysicalState.Intent
			};
			KingdomGrowthObjectCallbackStep oldStep = Candidate.DispositionStep;
			string oldConsuming = Candidate.ConsumingOperationId;
			long oldConsumingSequence = Candidate.ConsumingOperationSequence;
			string oldPlanHash = Candidate.PlanHash;
			KingdomLifecycleLeaseState oldLease = Candidate.EscrowLease.State;
			KingdomGrowthArrivalCandidatePhase oldPhase = Candidate.Phase;
			long oldTick = Candidate.UpdatedTick;
			Candidate.DispositionStep = step;
			Candidate.ConsumingOperationId = ConsumingOperationId;
			Candidate.ConsumingOperationSequence = operation.Sequence;
			Candidate.EscrowLease.State = KingdomLifecycleLeaseState.Intent;
			Candidate.Phase = joined ? KingdomGrowthArrivalCandidatePhase.ConsumeIntent
				: KingdomGrowthArrivalCandidatePhase.RefusalIntent;
			Candidate.UpdatedTick = Tick;
			string dispositionPlanHash;
			if (TryGrowthArrivalCandidatePlanHash(Candidate, out dispositionPlanHash))
				Candidate.PlanHash = dispositionPlanHash;
			if (dispositionPlanHash != null && ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				&& ExactGrowthOperationAuthority(Book, operation)) return true;
			Candidate.DispositionStep = oldStep;
			Candidate.ConsumingOperationId = oldConsuming;
			Candidate.ConsumingOperationSequence = oldConsumingSequence;
			Candidate.PlanHash = oldPlanHash;
			Candidate.EscrowLease.State = oldLease;
			Candidate.Phase = oldPhase; Candidate.UpdatedTick = oldTick;
			return false;
		}

		internal static bool CommitGrowthArrivalCandidateDisposition(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, string CallbackReferenceHash,
			bool SameReference, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| (Candidate.Phase != KingdomGrowthArrivalCandidatePhase.ConsumeIntent
					&& Candidate.Phase != KingdomGrowthArrivalCandidatePhase.RefusalIntent)
				|| Tick < Candidate.UpdatedTick || !GrowthWitnessHash(CallbackReferenceHash)
				|| SameReference != (Candidate.Disposition ==
					KingdomGrowthArrivalDisposition.Joined)) return false;
			KingdomGrowthObjectCallbackStep step = Candidate.DispositionStep;
			KingdomLifecycleResourceLease[] leases = { Candidate.EscrowLease };
			KingdomLifecycleResourceRevision[] rows =
				{ FindGrowthResource(Book, leases[0].Key) };
			for (int i = 0; i < rows.Length; i++)
				if (!GrowthResourceMatches(rows[i], leases[i])
					|| rows[i].Revision != leases[i].BeforeRevision
					|| !string.Equals(rows[i].ActiveOperationId, Candidate.Id,
						StringComparison.Ordinal)) return false;
			KingdomLifecyclePhysicalState oldState = step.State;
			KingdomLifecyclePhysicalState oldReceiptState = step.ReceiptState;
			int oldAfterMatches = step.ReceiptAfterMatches;
			int oldAfterCount = step.ReceiptAfterCount;
			string oldCallbackId = step.ReceiptCallbackObjectId;
			string oldCallbackMarker = step.ReceiptCallbackMarker;
			string oldCallbackReference = step.ReceiptCallbackReferenceHash;
			bool oldSameReference = step.ReceiptSameReference;
			string oldAfterOwner = step.ReceiptAfterOwnerGraphHash;
			string oldAfterObject = step.ReceiptAfterObjectGraphHash;
			string oldAfterTopology = step.ReceiptAfterTopologyHash;
			string oldProof = step.ReceiptProofId;
			long oldRevision = rows[0].Revision;
			string oldLastOperation = rows[0].LastOperationId;
			KingdomLifecycleLeaseState oldLeaseState = leases[0].State;
			KingdomGrowthArrivalCandidatePhase oldPhase = Candidate.Phase;
			long oldTick = Candidate.UpdatedTick;
			step.State = KingdomLifecyclePhysicalState.Proved;
			step.ReceiptState = KingdomLifecyclePhysicalState.Proved;
			step.ReceiptAfterMatches = step.AfterCount == 0 ? 0 : 1;
			step.ReceiptAfterCount = step.AfterCount;
			step.ReceiptCallbackObjectId = Candidate.ObjectId;
			step.ReceiptCallbackMarker = Candidate.Marker;
			step.ReceiptCallbackReferenceHash = CallbackReferenceHash;
			step.ReceiptSameReference = SameReference;
			step.ReceiptAfterOwnerGraphHash = step.AfterOwnerGraphHash;
			step.ReceiptAfterObjectGraphHash = step.AfterObjectGraphHash;
			step.ReceiptAfterTopologyHash = step.AfterTopologyHash;
			step.ReceiptProofId = GrowthArrivalCandidateCallbackProof(Candidate, step, 1);
			for (int i = 0; i < rows.Length; i++)
			{
				rows[i].Revision = leases[i].AfterRevision;
				rows[i].LastOperationId = Candidate.Id;
				leases[i].State = KingdomLifecycleLeaseState.Proved;
			}
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.Settled;
			Candidate.UpdatedTick = Tick;
			KingdomGrowthOperation operation = Book.ArrivalOp;
			if (ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				&& operation != null && string.Equals(operation.Id,
					Candidate.ConsumingOperationId, StringComparison.Ordinal)
				&& ExactGrowthOperationAuthority(Book, operation)) return true;
			step.State = oldState; step.ReceiptState = oldReceiptState;
			step.ReceiptAfterMatches = oldAfterMatches;
			step.ReceiptAfterCount = oldAfterCount;
			step.ReceiptCallbackObjectId = oldCallbackId;
			step.ReceiptCallbackMarker = oldCallbackMarker;
			step.ReceiptCallbackReferenceHash = oldCallbackReference;
			step.ReceiptSameReference = oldSameReference;
			step.ReceiptAfterOwnerGraphHash = oldAfterOwner;
			step.ReceiptAfterObjectGraphHash = oldAfterObject;
			step.ReceiptAfterTopologyHash = oldAfterTopology;
			step.ReceiptProofId = oldProof;
			rows[0].Revision = oldRevision; rows[0].LastOperationId = oldLastOperation;
			leases[0].State = oldLeaseState;
			Candidate.Phase = oldPhase; Candidate.UpdatedTick = oldTick;
			return false;
		}

		public static bool RetireGrowthArrivalCandidate(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.Settled
				|| Book.ArrivalOp != null
				|| !GrowthRetiredArrivalBarrierExists(Book, Candidate)) return false;
			KingdomLifecycleResourceRevision candidateRow = FindGrowthResource(Book,
				Candidate.CandidateLease.Key);
			KingdomLifecycleResourceRevision escrowRow = FindGrowthResource(Book,
				Candidate.EscrowLease.Key);
			KingdomLifecycleResourceRevision lodgingRow = FindGrowthResource(Book,
				Candidate.LodgingLease.Key);
			if (!GrowthLeaseProvedByCandidateRow(candidateRow, Candidate.CandidateLease, Candidate.Id)
				|| !GrowthLeaseProvedByCandidateRow(lodgingRow, Candidate.LodgingLease, Candidate.Id)
				|| !GrowthLeaseProvedByCandidateRow(escrowRow, Candidate.EscrowLease,
					Candidate.Id)) return false;
			string candidateActive = candidateRow.ActiveOperationId;
			string lodgingActive = lodgingRow.ActiveOperationId;
			string escrowActive = escrowRow.ActiveOperationId;
			long retiredBefore = Book.ArrivalCandidateRetiredThrough;
			long arrivalBefore = Book.NextArrivalTick;
			candidateRow.ActiveOperationId = null; lodgingRow.ActiveOperationId = null;
			escrowRow.ActiveOperationId = null;
			Book.ArrivalCandidateRetiredThrough = Candidate.Sequence;
			Book.ArrivalCandidate = null;
			if (Book.WorkPaused) Book.NextArrivalTick = 0L;
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			Book.ArrivalCandidate = Candidate;
			Book.ArrivalCandidateRetiredThrough = retiredBefore;
			Book.NextArrivalTick = arrivalBefore;
			candidateRow.ActiveOperationId = candidateActive;
			lodgingRow.ActiveOperationId = lodgingActive;
			escrowRow.ActiveOperationId = escrowActive;
			return false;
		}

		private static bool GrowthRetiredArrivalBarrierExists(KingdomGrowthBook book,
			KingdomGrowthArrivalCandidate candidate)
		{
			if (book == null || candidate == null || candidate.ConsumingOperationSequence <= 0L
				|| book.ArrivalRetiredThrough < candidate.ConsumingOperationSequence
				|| !string.Equals(candidate.ConsumingOperationId,
					GrowthOperationId(book.SettlementId, KingdomGrowthSlotKind.Arrival, null,
						candidate.ConsumingOperationSequence), StringComparison.Ordinal)) return false;
			string subject = GrowthClockSubject(book.SettlementId,
				KingdomGrowthSlotKind.Arrival, null);
			KingdomLifecycleResourceRevision row = FindGrowthResource(book,
				ResourceKey(KingdomLifecycleResourceKind.GrowthClock, book.SettlementId, subject));
			return row != null && row.Kind == KingdomLifecycleResourceKind.GrowthClock
				&& row.ActiveOperationId == null
				&& string.Equals(row.LastOperationId, candidate.ConsumingOperationId,
					StringComparison.Ordinal);
		}

		public static bool QuarantineGrowthArrivalCandidate(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, string Fault)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase == KingdomGrowthArrivalCandidatePhase.Quarantined
				|| string.IsNullOrEmpty(Fault) || TooLong(Fault, MaxTextChars)) return false;
			KingdomGrowthArrivalCandidatePhase before = Candidate.Phase;
			Candidate.EvidencePhase = before;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.Quarantined;
			Candidate.Fault = SafeFault(Fault);
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			Candidate.Phase = before; Candidate.EvidencePhase = 0; Candidate.Fault = null;
			return false;
		}

		private static bool ExactGrowthArrivalCandidateAuthority(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate)
		{
			return Candidate != null && ReferenceEquals(Book == null ? null : Book.ArrivalCandidate,
				Candidate) && CanOwnGrowthAuthority(Book, Book.SettlementId);
		}

		private static bool GrowthLeaseProvedByCandidateRow(
			KingdomLifecycleResourceRevision row, KingdomLifecycleResourceLease lease, string id)
		{
			return GrowthResourceMatches(row, lease) && lease.State == KingdomLifecycleLeaseState.Proved
				&& row.Revision == lease.AfterRevision
				&& string.Equals(row.ActiveOperationId, id, StringComparison.Ordinal)
				&& string.Equals(row.LastOperationId, id, StringComparison.Ordinal);
		}

		private static string GrowthArrivalCandidateCallbackProof(
			KingdomGrowthArrivalCandidate candidate, KingdomGrowthObjectCallbackStep step,
			int ordinal, bool LegacyV1 = false)
		{
			string binding = candidate == null ? null : candidate.PlanHash;
			if (ordinal == 0
				&& !(LegacyV1
					? TryLegacyGrowthArrivalCandidateBasePlanHash(candidate, out binding)
					: TryGrowthArrivalCandidateBasePlanHash(candidate, out binding))) return null;
			return HashId("growth-arrival-candidate-callback-proof", delegate(BinaryWriter w)
			{
				CanonicalString(w, candidate.Id); CanonicalString(w, binding);
				w.Write(ordinal); CanonicalString(w, candidate.ObjectId);
				CanonicalString(w, candidate.Marker); CanonicalString(w, step.EventId);
				CanonicalString(w, step.ReceiptCallbackReferenceHash);
				if (!LegacyV1) w.Write(step.ReceiptSameReference);
				CanonicalString(w, step.ReceiptAfterOwnerGraphHash);
				CanonicalString(w, step.ReceiptAfterObjectGraphHash);
				CanonicalString(w, step.ReceiptAfterTopologyHash);
			});
		}

	}
}
