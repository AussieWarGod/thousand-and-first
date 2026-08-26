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
		internal static bool CommitGrowthArrivalCandidateCreate(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, string ObjectId,
			string AfterOwnerGraphHash, string AfterObjectGraphHash, string AfterTopologyHash,
			string CallbackReferenceHash, bool SameReference, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.CreateIntent
				|| Tick < Candidate.UpdatedTick || !ValidRootId(ObjectId)
				|| !GrowthWitnessHash(AfterOwnerGraphHash)
				|| !GrowthWitnessHash(AfterObjectGraphHash)
				|| !GrowthWitnessHash(AfterTopologyHash)
				|| !GrowthWitnessHash(CallbackReferenceHash) || !SameReference) return false;
			KingdomLifecycleResourceRevision candidateRow = FindGrowthResource(Book,
				Candidate.CandidateLease.Key);
			if (!GrowthResourceMatches(candidateRow, Candidate.CandidateLease)
				|| candidateRow.Revision != Candidate.CandidateLease.BeforeRevision
				|| !string.Equals(candidateRow.ActiveOperationId, Candidate.Id,
					StringComparison.Ordinal)) return false;
			KingdomGrowthObjectCallbackStep step = Candidate.CreateStep;
			string oldObjectId = Candidate.ObjectId;
			string oldAfterOwner = step.AfterOwnerGraphHash;
			string oldAfterObject = step.AfterObjectGraphHash;
			string oldAfterTopology = step.AfterTopologyHash;
			KingdomLifecyclePhysicalState oldState = step.State;
			KingdomLifecyclePhysicalState oldReceiptState = step.ReceiptState;
			int oldAfterMatches = step.ReceiptAfterMatches;
			int oldAfterCount = step.ReceiptAfterCount;
			string oldCallbackId = step.ReceiptCallbackObjectId;
			string oldCallbackMarker = step.ReceiptCallbackMarker;
			string oldCallbackReference = step.ReceiptCallbackReferenceHash;
			bool oldSameReference = step.ReceiptSameReference;
			string oldReceiptAfterOwner = step.ReceiptAfterOwnerGraphHash;
			string oldReceiptAfterObject = step.ReceiptAfterObjectGraphHash;
			string oldReceiptAfterTopology = step.ReceiptAfterTopologyHash;
			string oldProof = step.ReceiptProofId;
			long oldRevision = candidateRow.Revision;
			string oldLastOperation = candidateRow.LastOperationId;
			KingdomLifecycleLeaseState oldLeaseState = Candidate.CandidateLease.State;
			KingdomGrowthArrivalCandidatePhase oldPhase = Candidate.Phase;
			long oldTick = Candidate.UpdatedTick;
			Candidate.ObjectId = ObjectId;
			step.AfterOwnerGraphHash = AfterOwnerGraphHash;
			step.AfterObjectGraphHash = AfterObjectGraphHash;
			step.AfterTopologyHash = AfterTopologyHash;
			step.State = KingdomLifecyclePhysicalState.Proved;
			step.ReceiptState = KingdomLifecyclePhysicalState.Proved;
			step.ReceiptAfterMatches = 1; step.ReceiptAfterCount = 1;
			step.ReceiptCallbackObjectId = ObjectId;
			step.ReceiptCallbackMarker = Candidate.Marker;
			step.ReceiptCallbackReferenceHash = CallbackReferenceHash;
			step.ReceiptSameReference = true;
			step.ReceiptAfterOwnerGraphHash = AfterOwnerGraphHash;
			step.ReceiptAfterObjectGraphHash = AfterObjectGraphHash;
			step.ReceiptAfterTopologyHash = AfterTopologyHash;
			step.ReceiptProofId = GrowthArrivalCandidateCallbackProof(Candidate, step, 0);
			candidateRow.Revision = Candidate.CandidateLease.AfterRevision;
			candidateRow.LastOperationId = Candidate.Id;
			Candidate.CandidateLease.State = KingdomLifecycleLeaseState.Proved;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.Escrowed;
			Candidate.UpdatedTick = Tick;
			if (ExactGrowthArrivalCandidateAuthority(Book, Candidate)) return true;
			Candidate.ObjectId = oldObjectId;
			step.AfterOwnerGraphHash = oldAfterOwner;
			step.AfterObjectGraphHash = oldAfterObject;
			step.AfterTopologyHash = oldAfterTopology;
			step.State = oldState; step.ReceiptState = oldReceiptState;
			step.ReceiptAfterMatches = oldAfterMatches;
			step.ReceiptAfterCount = oldAfterCount;
			step.ReceiptCallbackObjectId = oldCallbackId;
			step.ReceiptCallbackMarker = oldCallbackMarker;
			step.ReceiptCallbackReferenceHash = oldCallbackReference;
			step.ReceiptSameReference = oldSameReference;
			step.ReceiptAfterOwnerGraphHash = oldReceiptAfterOwner;
			step.ReceiptAfterObjectGraphHash = oldReceiptAfterObject;
			step.ReceiptAfterTopologyHash = oldReceiptAfterTopology;
			step.ReceiptProofId = oldProof;
			candidateRow.Revision = oldRevision;
			candidateRow.LastOperationId = oldLastOperation;
			Candidate.CandidateLease.State = oldLeaseState;
			Candidate.Phase = oldPhase; Candidate.UpdatedTick = oldTick;
			return false;
		}

		internal static bool BeginGrowthArrivalLodgingObservation(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, string ZoneId, int X, int Y,
			string BeforeGraphHash, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.Escrowed
				|| Tick < Candidate.UpdatedTick || !string.Equals(ZoneId,
					Candidate.LodgingZoneId, StringComparison.Ordinal)
				|| X < 0 || X > MaxCoordinate
				|| Y < 0 || Y > MaxCoordinate || !GrowthWitnessHash(BeforeGraphHash)) return false;
			string oldZone = Candidate.LodgingZoneId;
			int oldX = Candidate.LodgingX; int oldY = Candidate.LodgingY;
			string oldBefore = Candidate.LodgingBeforeGraphHash;
			string oldDeclared = Candidate.LodgingDeclaredGraphHash;
			string oldReceiptId = Candidate.LodgingReceiptId;
			KingdomLifecyclePhysicalState oldState = Candidate.LodgingState;
			KingdomLifecycleLeaseState oldLease = Candidate.LodgingLease.State;
			KingdomGrowthArrivalCandidatePhase oldPhase = Candidate.Phase;
			string oldPlanHash = Candidate.PlanHash;
			long oldTick = Candidate.UpdatedTick;
			Candidate.LodgingZoneId = ZoneId; Candidate.LodgingX = X; Candidate.LodgingY = Y;
			Candidate.LodgingBeforeGraphHash = BeforeGraphHash;
			Candidate.LodgingDeclaredGraphHash = null;
			Candidate.LodgingReceiptId = ChildId(Candidate.Id, "lodging-receipt", 0);
			Candidate.LodgingState = KingdomLifecyclePhysicalState.Intent;
			Candidate.LodgingLease.State = KingdomLifecycleLeaseState.Intent;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.LodgingIntent;
			Candidate.UpdatedTick = Tick;
			string intentPlanHash;
			if (TryGrowthArrivalCandidatePlanHash(Candidate, out intentPlanHash))
				Candidate.PlanHash = intentPlanHash;
			if (intentPlanHash != null && ExactGrowthArrivalCandidateAuthority(Book, Candidate))
				return true;
			Candidate.LodgingZoneId = oldZone; Candidate.LodgingX = oldX;
			Candidate.LodgingY = oldY; Candidate.LodgingBeforeGraphHash = oldBefore;
			Candidate.LodgingDeclaredGraphHash = oldDeclared;
			Candidate.LodgingReceiptId = oldReceiptId; Candidate.LodgingState = oldState;
			Candidate.LodgingLease.State = oldLease; Candidate.Phase = oldPhase;
			Candidate.PlanHash = oldPlanHash;
			Candidate.UpdatedTick = oldTick;
			return false;
		}

		internal static bool CommitGrowthArrivalLodgingObservation(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, KingdomGrowthArrivalDisposition Disposition,
			KingdomGrowthArrivalRefusalReason RefusalReason, string ReceiptGraphHash,
			string CallbackReferenceHash, bool SameReference, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.LodgingIntent
				|| Tick < Candidate.UpdatedTick
				|| (Disposition != KingdomGrowthArrivalDisposition.Joined
					&& Disposition != KingdomGrowthArrivalDisposition.NoAcceptableHome)
				|| !GrowthWitnessHash(ReceiptGraphHash)
				|| string.Equals(ReceiptGraphHash, Candidate.LodgingBeforeGraphHash,
					StringComparison.Ordinal)
				|| !Enum.IsDefined(typeof(KingdomGrowthArrivalRefusalReason), RefusalReason)
				|| (Disposition == KingdomGrowthArrivalDisposition.Joined
					? RefusalReason != KingdomGrowthArrivalRefusalReason.None
					: RefusalReason == KingdomGrowthArrivalRefusalReason.None)
				|| !GrowthWitnessHash(CallbackReferenceHash) || !SameReference) return false;
			KingdomLifecycleResourceRevision lodgingRow = FindGrowthResource(Book,
				Candidate.LodgingLease.Key);
			if (!GrowthResourceMatches(lodgingRow, Candidate.LodgingLease)
				|| lodgingRow.Revision != Candidate.LodgingLease.BeforeRevision
				|| !string.Equals(lodgingRow.ActiveOperationId, Candidate.Id,
					StringComparison.Ordinal)) return false;
			KingdomGrowthArrivalDisposition oldDisposition = Candidate.Disposition;
			KingdomGrowthArrivalRefusalReason oldReason = Candidate.RefusalReason;
			string oldDeclaredGraph = Candidate.LodgingDeclaredGraphHash;
			string oldReceiptGraph = Candidate.LodgingReceiptGraphHash;
			string oldCallbackReference = Candidate.LodgingCallbackReferenceHash;
			bool oldSameReference = Candidate.LodgingSameReference;
			string oldPlanHash = Candidate.PlanHash;
			KingdomLifecyclePhysicalState oldState = Candidate.LodgingState;
			long oldRevision = lodgingRow.Revision;
			string oldLastOperation = lodgingRow.LastOperationId;
			KingdomLifecycleLeaseState oldLease = Candidate.LodgingLease.State;
			KingdomGrowthArrivalCandidatePhase oldPhase = Candidate.Phase;
			long oldTick = Candidate.UpdatedTick;
			Candidate.Disposition = Disposition;
			Candidate.RefusalReason = RefusalReason;
			Candidate.LodgingReceiptGraphHash = ReceiptGraphHash;
			Candidate.LodgingCallbackReferenceHash = CallbackReferenceHash;
			Candidate.LodgingSameReference = true;
			Candidate.LodgingDeclaredGraphHash = GrowthArrivalLodgingProof(Candidate);
			Candidate.LodgingState = KingdomLifecyclePhysicalState.Proved;
			string observedPlanHash;
			string basePlanHash;
			if (Candidate.LodgingDeclaredGraphHash == null
				|| !TryGrowthArrivalCandidateBasePlanHash(Candidate, out basePlanHash)
				|| !TryGrowthArrivalObservedPlanHash(Candidate, basePlanHash,
					out observedPlanHash))
			{
				Candidate.Disposition = oldDisposition; Candidate.RefusalReason = oldReason;
				Candidate.LodgingDeclaredGraphHash = oldDeclaredGraph;
				Candidate.LodgingReceiptGraphHash = oldReceiptGraph;
				Candidate.LodgingCallbackReferenceHash = oldCallbackReference;
				Candidate.LodgingSameReference = oldSameReference;
				Candidate.LodgingState = oldState;
				return false;
			}
			Candidate.PlanHash = observedPlanHash;
			lodgingRow.Revision = Candidate.LodgingLease.AfterRevision;
			lodgingRow.LastOperationId = Candidate.Id;
			Candidate.LodgingLease.State = KingdomLifecycleLeaseState.Proved;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.Observed;
			Candidate.UpdatedTick = Tick;
			if (ExactGrowthArrivalCandidateAuthority(Book, Candidate)) return true;
			Candidate.Disposition = oldDisposition;
			Candidate.RefusalReason = oldReason;
			Candidate.LodgingDeclaredGraphHash = oldDeclaredGraph;
			Candidate.LodgingReceiptGraphHash = oldReceiptGraph;
			Candidate.LodgingCallbackReferenceHash = oldCallbackReference;
			Candidate.LodgingSameReference = oldSameReference;
			Candidate.PlanHash = oldPlanHash;
			Candidate.LodgingState = oldState;
			lodgingRow.Revision = oldRevision; lodgingRow.LastOperationId = oldLastOperation;
			Candidate.LodgingLease.State = oldLease; Candidate.Phase = oldPhase;
			Candidate.UpdatedTick = oldTick;
			return false;
		}

	}
}
