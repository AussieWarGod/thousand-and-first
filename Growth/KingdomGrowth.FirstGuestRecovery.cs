using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		private static bool TryInterposeLegacyFirstGuest(KingdomSystem system, Zone zone,
			KingdomGrowthBook growth, KingdomGrowthArrivalCandidate candidate, long tick,
			out string failure)
		{
			failure = null;
			if (candidate == null || !candidate.LegacyAutomaticRecovery
				|| candidate.FirstGuest != null
				|| candidate.Phase != KingdomGrowthArrivalCandidatePhase.Prepared) return true;
			// Story-off recovery remains the exact old ordinary arrival; it never creates a
			// correspondence row that will surface after a later re-enable.
			if (!TryCivicStoryAllowsFirstGuest(system, tick, out bool storyEnabled))
			{
				failure = "first-guest option authority cannot be read"; return false;
			}
			if (!storyEnabled) return true;
			bool noMaterial = DecodedLegacyCandidateHasNoMaterialDebit(growth, candidate);
			bool noBody = DecodedLegacyCandidateHasNoBodyCallback(candidate);
			bool noEscrow = The.Game != null
				&& !The.Game.ObjectGameState.ContainsKey(candidate.EscrowKey);
			int markerCount = CountArrivalMarker(zone, candidate.Marker);
			bool noLodging = markerCount == 0 && candidate.ObjectId == null
				&& candidate.LodgingState == KingdomLifecyclePhysicalState.None;
			bool noCitizenship = markerCount == 0
				&& !LegacyCandidateDomainReceiptExists(zone, candidate.Id);
			if (!noMaterial || !noBody || !noEscrow || !noLodging || !noCitizenship)
				return true;
			if (KingdomLifecycleRules.TryInterposeLegacyPreparedFirstGuest(growth, candidate,
				noMaterial, noBody, noEscrow, noLodging, noCitizenship, tick)) return true;
			// Exact but ineligible historical rows retain their tagged committed-recovery path.
			// Interposition never guesses missing proof and never destroys old transaction evidence.
			return true;
		}

		private static bool DecodedLegacyCandidateHasNoMaterialDebit(KingdomGrowthBook growth,
			KingdomGrowthArrivalCandidate candidate)
		{
			return growth != null && growth.ArrivalOp == null
				&& candidate.CandidateLease?.State == KingdomLifecycleLeaseState.Prepared
				&& candidate.LodgingLease?.State == KingdomLifecycleLeaseState.Prepared
				&& candidate.EscrowLease?.State == KingdomLifecycleLeaseState.Prepared
				&& candidate.Disposition == KingdomGrowthArrivalDisposition.None;
		}

		private static bool DecodedLegacyCandidateHasNoBodyCallback(
			KingdomGrowthArrivalCandidate candidate)
		{
			KingdomGrowthObjectCallbackStep step = candidate?.CreateStep;
			return candidate != null && candidate.ObjectId == null && step != null
				&& step.State == KingdomLifecyclePhysicalState.Prepared
				&& step.ReceiptState == KingdomLifecyclePhysicalState.Prepared
				&& step.ReceiptCallbackObjectId == null && step.ReceiptProofId == null;
		}

		private static bool LegacyCandidateDomainReceiptExists(Zone zone, string candidateId)
		{
			if (zone == null || string.IsNullOrEmpty(candidateId)) return true;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(zone))
				if (item.GetStringProperty(ArrivalEnrollmentReceiptProperty) == candidateId
					|| item.GetStringProperty(ArrivalRosterReceiptProperty) == candidateId
					|| item.GetStringProperty(ArrivalCreedReceiptProperty) == candidateId)
					return true;
			return false;
		}

		private static bool EnsureFirstGuestBodyLeaseForRecovery(KingdomSystem system,
			KingdomGrowthArrivalCandidate candidate, long tick, out string failure)
		{
			failure = null;
			KingdomGrowthFirstGuestOpportunity x = candidate?.FirstGuest;
			if (x == null) return candidate != null && candidate.LegacyAutomaticRecovery;
			if (x.ChoiceState != KingdomGrowthFirstGuestChoiceState.Admitted)
			{
				failure = "first-guest choice does not authorize a body"; return false;
			}
			if (x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.None) return true;
			if (x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.Released) return true;
			KingdomExperienceBodyReservation expected = PersistedFirstGuestBodyRequest(
				system, candidate);
			if (expected == null || system?.Experience == null)
			{
				failure = "first-guest body lease proof is absent"; return false;
			}
			if (!KingdomExperienceRules.TryReadBodyLease(system.Experience,
				expected.ReservationId, out KingdomExperienceBodyReservation actual,
				out KingdomExperienceLeaseState state, out failure)) return false;
			if (state != KingdomExperienceLeaseState.Missing)
				return SameFirstGuestBodyRequest(expected, actual)
					|| FailFirstGuest("first-guest body lease differs from Growth proof", out failure);
			if (!KingdomLifecycleRules.GrowthFirstGuestBodyLeaseRecoveryRequired(
				system.LifecycleBook.Growth, candidate)) return true;
			if (!KingdomExperienceRuntime.TryRecoverDurableBodies(system, expected, tick,
				out KingdomExperienceCapacityFault _, out failure)) return false;
			return true;
		}
	}
}
