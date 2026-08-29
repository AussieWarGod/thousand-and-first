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
		internal static bool UpgradeLegacyGrowthArrivalCandidate(
			KingdomGrowthArrivalCandidate Candidate)
		{
			if (Candidate == null) return true;
			string legacyBaseHash;
			string baseHash;
			if (!TryLegacyGrowthArrivalCandidateBasePlanHash(Candidate, out legacyBaseHash)
				|| !string.Equals(Candidate.PlanHash, legacyBaseHash, StringComparison.Ordinal)
				) return false;
			KingdomGrowthObjectCallbackStep create = Candidate.CreateStep;
			if (create != null && create.State == KingdomLifecyclePhysicalState.Proved
				&& !string.Equals(create.ReceiptProofId,
					GrowthArrivalCandidateCallbackProof(Candidate, create, 0, true),
					StringComparison.Ordinal)) return false;
			KingdomGrowthArrivalCandidatePhase phase = Candidate.Phase ==
				KingdomGrowthArrivalCandidatePhase.Quarantined
					? Candidate.EvidencePhase : Candidate.Phase;
			if (Candidate.LegacyGrowthV1UnboundZone)
			{
				return Candidate.LodgingZoneId == null
					&& (phase == KingdomGrowthArrivalCandidatePhase.Prepared
						|| phase == KingdomGrowthArrivalCandidatePhase.CreateIntent
						|| phase == KingdomGrowthArrivalCandidatePhase.Escrowed)
					&& Candidate.LodgingState == KingdomLifecyclePhysicalState.None;
			}
			if (!ValidName(Candidate.LodgingZoneId)
				|| !TryGrowthArrivalCandidateBasePlanHash(Candidate, out baseHash)) return false;
			string currentHash;
			if (Candidate.LodgingState != KingdomLifecyclePhysicalState.Proved)
			{
				if (!TryGrowthArrivalCandidatePlanHash(Candidate, out currentHash)) return false;
				Candidate.PlanHash = currentHash;
				if (create != null && create.State == KingdomLifecyclePhysicalState.Proved)
					create.ReceiptProofId = GrowthArrivalCandidateCallbackProof(
						Candidate, create, 0);
				return true;
			}
			string proof = GrowthArrivalLodgingProof(Candidate);
			if (proof == null) return false;
			if (!string.Equals(Candidate.LodgingDeclaredGraphHash,
					Candidate.LodgingReceiptGraphHash, StringComparison.Ordinal)) return false;
			KingdomGrowthObjectCallbackStep disposition = Candidate.DispositionStep;
			if (disposition != null
				&& disposition.State == KingdomLifecyclePhysicalState.Proved
				&& !string.Equals(disposition.ReceiptProofId,
					GrowthArrivalCandidateCallbackProof(Candidate, disposition, 1, true),
					StringComparison.Ordinal)) return false;
			Candidate.LodgingDeclaredGraphHash = proof;
			if (!TryGrowthArrivalCandidatePlanHash(Candidate, out currentHash)) return false;
			Candidate.PlanHash = currentHash;
			if (create != null && create.State == KingdomLifecyclePhysicalState.Proved)
				create.ReceiptProofId = GrowthArrivalCandidateCallbackProof(Candidate, create, 0);
			if (disposition != null
				&& disposition.State == KingdomLifecyclePhysicalState.Proved)
				disposition.ReceiptProofId = GrowthArrivalCandidateCallbackProof(
					Candidate, disposition, 1);
			return true;
		}

		internal static bool BindLegacyGrowthArrivalCandidateZone(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, string ZoneId, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase == KingdomGrowthArrivalCandidatePhase.Quarantined
				|| !Candidate.LegacyGrowthV1UnboundZone || Candidate.LodgingZoneId != null
				|| !ValidName(ZoneId) || Tick < Candidate.UpdatedTick
				|| Tick < Book.OptionTick || Tick < Book.HealthTick) return false;
			KingdomGrowthArrivalCandidatePhase phase = Candidate.Phase ==
				KingdomGrowthArrivalCandidatePhase.Quarantined
					? Candidate.EvidencePhase : Candidate.Phase;
			if (phase != KingdomGrowthArrivalCandidatePhase.Prepared
				&& phase != KingdomGrowthArrivalCandidatePhase.AwaitingChoice
				&& phase != KingdomGrowthArrivalCandidatePhase.CreateIntent
				&& phase != KingdomGrowthArrivalCandidatePhase.Escrowed) return false;
			string oldHash = Candidate.PlanHash;
			long oldTick = Candidate.UpdatedTick;
			string oldProof = Candidate.CreateStep == null
				? null : Candidate.CreateStep.ReceiptProofId;
			Candidate.LodgingZoneId = ZoneId;
			Candidate.LegacyGrowthV1UnboundZone = false;
			string hash;
			if (!TryGrowthArrivalCandidatePlanHash(Candidate, out hash))
			{
				Candidate.LodgingZoneId = null;
				Candidate.LegacyGrowthV1UnboundZone = true;
				return false;
			}
			Candidate.PlanHash = hash;
			if (Candidate.CreateStep != null
				&& Candidate.CreateStep.State == KingdomLifecyclePhysicalState.Proved)
				Candidate.CreateStep.ReceiptProofId = GrowthArrivalCandidateCallbackProof(
					Candidate, Candidate.CreateStep, 0);
			Candidate.UpdatedTick = Tick;
			if (ExactGrowthArrivalCandidateAuthority(Book, Candidate)) return true;
			Candidate.LodgingZoneId = null;
			Candidate.LegacyGrowthV1UnboundZone = true;
			Candidate.PlanHash = oldHash;
			Candidate.UpdatedTick = oldTick;
			if (Candidate.CreateStep != null)
				Candidate.CreateStep.ReceiptProofId = oldProof;
			return false;
		}

		internal static bool UpgradeLegacyGrowthArrivalSemanticPlan(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, int PlanVersion, string StreamId,
			uint EventKind, string Origin, string Creed, string Name, string Arrived,
			int ArrivalX, int ArrivalY, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase == KingdomGrowthArrivalCandidatePhase.Quarantined
				|| !Candidate.LegacySemanticPlan || Candidate.LegacyGrowthV1UnboundZone
				|| Tick < Candidate.UpdatedTick) return false;
			KingdomGrowthArrivalCandidatePhase phase = Candidate.Phase;
			if (phase != KingdomGrowthArrivalCandidatePhase.Prepared
				&& phase != KingdomGrowthArrivalCandidatePhase.CreateIntent
				&& phase != KingdomGrowthArrivalCandidatePhase.Escrowed) return false;

			string oldHash = Candidate.PlanHash;
			long oldTick = Candidate.UpdatedTick;
			string oldProof = Candidate.CreateStep == null
				? null : Candidate.CreateStep.ReceiptProofId;
			Candidate.LegacySemanticPlan = false;
			Candidate.SemanticPlanVersion = PlanVersion;
			Candidate.SemanticStreamId = StreamId;
			Candidate.SemanticEventKind = EventKind;
			Candidate.PlannedOrigin = Origin;
			Candidate.PlannedCreed = Creed;
			Candidate.PlannedName = Name;
			Candidate.PlannedArrived = Arrived;
			Candidate.ArrivalX = ArrivalX;
			Candidate.ArrivalY = ArrivalY;
			string hash;
			if (GrowthArrivalSemanticPlanShape(Candidate)
				&& TryGrowthArrivalCandidatePlanHash(Candidate, out hash))
			{
				Candidate.PlanHash = hash;
				if (Candidate.CreateStep != null && Candidate.CreateStep.State ==
					KingdomLifecyclePhysicalState.Proved)
					Candidate.CreateStep.ReceiptProofId = GrowthArrivalCandidateCallbackProof(
						Candidate, Candidate.CreateStep, 0);
				Candidate.UpdatedTick = Tick;
				if (ExactGrowthArrivalCandidateAuthority(Book, Candidate)) return true;
			}
			Candidate.LegacySemanticPlan = true;
			Candidate.SemanticPlanVersion = 0;
			Candidate.SemanticStreamId = null;
			Candidate.SemanticEventKind = 0U;
			Candidate.PlannedOrigin = null;
			Candidate.PlannedCreed = null;
			Candidate.PlannedName = null;
			Candidate.PlannedArrived = null;
			Candidate.ArrivalX = -1;
			Candidate.ArrivalY = -1;
			Candidate.PlanHash = oldHash;
			Candidate.UpdatedTick = oldTick;
			if (Candidate.CreateStep != null) Candidate.CreateStep.ReceiptProofId = oldProof;
			return false;
		}

		internal static bool DowngradeGrowthArrivalCandidateForV1Fixture(
			KingdomGrowthArrivalCandidate Candidate)
		{
			if (Candidate == null) return true;
			string currentHash;
			string legacyBaseHash;
			if (!TryGrowthArrivalCandidatePlanHash(Candidate, out currentHash)
				|| !string.Equals(Candidate.PlanHash, currentHash, StringComparison.Ordinal)
				|| !TryLegacyGrowthArrivalCandidateBasePlanHash(Candidate,
					out legacyBaseHash)) return false;
			if (Candidate.LodgingState == KingdomLifecyclePhysicalState.Proved)
			{
				string proof = GrowthArrivalLodgingProof(Candidate);
				if (proof == null || !string.Equals(Candidate.LodgingDeclaredGraphHash, proof,
					StringComparison.Ordinal)) return false;
				Candidate.LodgingDeclaredGraphHash = Candidate.LodgingReceiptGraphHash;
			}
			KingdomGrowthArrivalCandidatePhase phase = Candidate.Phase ==
				KingdomGrowthArrivalCandidatePhase.Quarantined
					? Candidate.EvidencePhase : Candidate.Phase;
			if (phase == KingdomGrowthArrivalCandidatePhase.Prepared
				|| phase == KingdomGrowthArrivalCandidatePhase.CreateIntent
				|| phase == KingdomGrowthArrivalCandidatePhase.Escrowed)
			{
				Candidate.LodgingZoneId = null;
				Candidate.LegacyGrowthV1UnboundZone = true;
			}
			Candidate.LegacySemanticPlan = true;
			Candidate.SemanticPlanVersion = 0;
			Candidate.SemanticStreamId = null;
			Candidate.SemanticEventKind = 0U;
			Candidate.PlannedOrigin = null;
			Candidate.PlannedCreed = null;
			Candidate.PlannedName = null;
			Candidate.PlannedArrived = null;
			Candidate.ArrivalX = -1;
			Candidate.ArrivalY = -1;
			Candidate.PlanHash = legacyBaseHash;
			KingdomGrowthObjectCallbackStep create = Candidate.CreateStep;
			if (create != null && create.State == KingdomLifecyclePhysicalState.Proved)
				create.ReceiptProofId = GrowthArrivalCandidateCallbackProof(
					Candidate, create, 0, true);
			KingdomGrowthObjectCallbackStep disposition = Candidate.DispositionStep;
			if (disposition != null
				&& disposition.State == KingdomLifecyclePhysicalState.Proved)
				disposition.ReceiptProofId = GrowthArrivalCandidateCallbackProof(
					Candidate, disposition, 1, true);
			return true;
		}

		public static KingdomGrowthArrivalCandidate PrepareGrowthArrivalCandidate(
			KingdomGrowthBook Book, string Marker, string Blueprint, string EscrowKey, string ZoneId,
			long Tick, string BeforeOwnerGraphHash, string BeforeObjectGraphHash,
			string BeforeTopologyHash)
		{
			return PrepareGrowthArrivalCandidate(Book, Marker, Blueprint, EscrowKey, ZoneId,
				Tick, BeforeOwnerGraphHash, BeforeObjectGraphHash, BeforeTopologyHash,
				0, null, 0U, null, null, null, null, -1, -1, true);
		}

	}
}
