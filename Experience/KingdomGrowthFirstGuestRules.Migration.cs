using System;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		internal static bool GrowthArrivalCandidateBoundToZone(
			KingdomGrowthArrivalCandidate Candidate, string ZoneId)
		{
			return Candidate != null && !Candidate.LegacyGrowthV1UnboundZone
				&& !string.IsNullOrEmpty(ZoneId) && string.Equals(Candidate.LodgingZoneId,
					ZoneId, StringComparison.Ordinal);
		}

		internal static bool UpgradeFirstGuestOpportunity(KingdomGrowthBook Book, int WireVersion)
		{
			if (Book == null || WireVersion < LegacyGrowthFormatVersion
				|| WireVersion >= FirstGuestPhysicalGrowthFormatVersion) return false;
			KingdomGrowthArrivalCandidate candidate = Book.ArrivalCandidate;
			if (candidate == null) return true;
			if (candidate.FirstGuest != null || candidate.LegacyAutomaticRecovery) return false;
			candidate.LegacyAutomaticRecovery = true;
			KingdomGrowthArrivalCandidatePhase phase = candidate.Phase ==
				KingdomGrowthArrivalCandidatePhase.Quarantined
					? candidate.EvidencePhase : candidate.Phase;
			if (phase == KingdomGrowthArrivalCandidatePhase.AwaitingChoice
				|| phase == KingdomGrowthArrivalCandidatePhase.Declined)
			{
				candidate.LegacyAutomaticRecovery = false; return false;
			}
			string hash;
			bool exact = candidate.LegacyGrowthV1UnboundZone
				? TryLegacyGrowthArrivalCandidateBasePlanHash(candidate, out hash)
				: TryGrowthArrivalCandidatePlanHash(candidate, out hash);
			if (exact && string.Equals(candidate.PlanHash, hash, StringComparison.Ordinal)) return true;
			candidate.LegacyAutomaticRecovery = false; return false;
		}

		internal static bool DowngradeFirstGuestForV3Fixture(KingdomGrowthBook Book)
		{
			if (Book == null) return false;
			KingdomGrowthArrivalCandidate candidate = Book.ArrivalCandidate;
			if (candidate == null) return true;
			KingdomGrowthFirstGuestOpportunity x = candidate.FirstGuest;
			if (candidate.LegacyAutomaticRecovery || x == null
				|| candidate.Phase != KingdomGrowthArrivalCandidatePhase.AwaitingChoice
				|| x.ChoiceState != KingdomGrowthFirstGuestChoiceState.AwaitingChoice
				|| x.DeferredTick != -1L || x.DeferredReceiptId != null
				|| !GrowthFirstGuestNoBodyProof(x)) return false;
			candidate.FirstGuest = null;
			candidate.LegacyAutomaticRecovery = true;
			candidate.Phase = KingdomGrowthArrivalCandidatePhase.Prepared;
			if (!TryGrowthV3ArrivalCandidateBasePlanHash(candidate, out string hash)) return false;
			candidate.PlanHash = hash;
			return true;
		}

		internal static bool DowngradePhysicalFirstGuestForLegacyFixture(
			KingdomGrowthBook Book)
		{
			if (Book == null) return false;
			KingdomGrowthArrivalCandidate candidate = Book.ArrivalCandidate;
			if (candidate?.FirstGuest?.RulesVersion == 2)
			{
				KingdomGrowthFirstGuestOpportunity x = candidate.FirstGuest;
				if (!GrowthFirstGuestPhysicalDefaults(x)) return false;
				string oldHash = candidate.PlanHash;
				string oldProof = candidate.CreateStep?.ReceiptProofId;
				x.RulesVersion = 1;
				if (!TryRehashFirstGuest(Book, candidate))
				{
					x.RulesVersion = 2; candidate.PlanHash = oldHash;
					if (candidate.CreateStep != null)
						candidate.CreateStep.ReceiptProofId = oldProof;
					return false;
				}
			}
			else if (candidate?.FirstGuest != null
				&& candidate.FirstGuest.RulesVersion != 1) return false;
			KingdomGrowthFirstGuestTerminalReceipt terminal = Book.FirstGuestTerminal;
			if (terminal?.Opportunity?.RulesVersion == 2)
			{
				if (!GrowthFirstGuestPhysicalDefaults(terminal.Opportunity)) return false;
				terminal.Opportunity.RulesVersion = 1;
			}
			else if (terminal?.Opportunity != null
				&& terminal.Opportunity.RulesVersion != 1) return false;
			if (terminal != null)
				terminal.Version = KingdomGrowthFirstGuestTerminalReceipt.LegacyVersion;
			return CanOwnGrowthAuthority(Book, Book.SettlementId);
		}

		public static bool TryInterposeLegacyPreparedFirstGuest(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, bool NoMaterialDebit, bool NoBodyCallback,
			bool NoEscrowRoot, bool NoLodgingMutation, bool NoCitizenshipMutation, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| !Candidate.LegacyAutomaticRecovery || Candidate.FirstGuest != null
				|| Candidate.Sequence != 1L
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.Prepared
				|| Tick < Candidate.UpdatedTick || Book.ArrivalOp != null
				|| !NoMaterialDebit || !NoBodyCallback || !NoEscrowRoot
				|| !NoLodgingMutation || !NoCitizenshipMutation
				|| !GrowthFirstGuestBlueprintAllowed(Candidate.Blueprint)
				|| !ExactUnstartedLegacyCandidate(Candidate)) return false;
				KingdomGrowthFirstGuestOpportunity x = new KingdomGrowthFirstGuestOpportunity
				{
					RulesVersion = 2,
				OpportunityId = GrowthFirstGuestOpportunityId(Candidate.SettlementId,
					Candidate.Sequence),
				CauseId = GrowthFirstGuestCauseId(Candidate.SettlementId, Candidate.Sequence,
					Book.NextArrivalTick, Book.ArrivalIntervalTicks),
				CauseTick = Book.NextArrivalTick, OfferedTick = Candidate.CreatedTick,
				CadenceTicks = Book.ArrivalIntervalTicks,
				FactsState = KingdomGrowthFirstGuestFactsState.LegacyPartial,
					ChoiceState = KingdomGrowthFirstGuestChoiceState.AwaitingChoice,
					GuestPhase = KingdomGrowthFirstGuestGuestPhase.None
			};
			if (x.CauseId == null || x.OfferedTick < x.CauseTick) return false;
			string oldHash = Candidate.PlanHash; long oldTick = Candidate.UpdatedTick;
			Candidate.FirstGuest = x; Candidate.LegacyAutomaticRecovery = false;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.AwaitingChoice;
			Candidate.UpdatedTick = Tick;
			if (TryRehashFirstGuest(Book, Candidate)) return true;
			Candidate.FirstGuest = null; Candidate.LegacyAutomaticRecovery = true;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.Prepared;
			Candidate.PlanHash = oldHash; Candidate.UpdatedTick = oldTick; return false;
		}

		private static bool ExactUnstartedLegacyCandidate(
			KingdomGrowthArrivalCandidate candidate)
		{
			return candidate.ObjectId == null && candidate.DispositionStep == null
				&& candidate.Disposition == KingdomGrowthArrivalDisposition.None
				&& candidate.RefusalReason == KingdomGrowthArrivalRefusalReason.None
				&& candidate.CandidateLease.State == KingdomLifecycleLeaseState.Prepared
				&& candidate.LodgingLease.State == KingdomLifecycleLeaseState.Prepared
				&& candidate.EscrowLease.State == KingdomLifecycleLeaseState.Prepared
				&& candidate.CreateStep.State == KingdomLifecyclePhysicalState.Prepared
				&& candidate.CreateStep.ReceiptState == KingdomLifecyclePhysicalState.Prepared
				&& GrowthObjectCallbackReceiptEmpty(candidate.CreateStep)
				&& GrowthArrivalLodgingEmpty(candidate, candidate.LegacyGrowthV1UnboundZone)
				&& candidate.ConsumingOperationId == null
				&& candidate.ConsumingOperationSequence == 0L;
		}
	}
}
