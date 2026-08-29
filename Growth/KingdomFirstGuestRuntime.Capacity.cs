using System;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomFirstGuestRuntime
	{
		private static bool TryOpenPresentationLease(KingdomSystem system,
			KingdomGrowthArrivalCandidate candidate, long now,
			out KingdomExperienceAudienceReceipt audience, out string failure)
		{
			audience = null; failure = null;
			KingdomGrowthFirstGuestOpportunity x = candidate?.FirstGuest;
			if (x == null || !ReleaseStaleAudience(system, x, out failure)
				|| !ReleaseUnadmittedBody(system, candidate, out failure)
				|| !KingdomMaster.NewWorkAllowed(system)
				|| !KingdomExperienceRuntime.TryObserveConfiguredOptions(system, now, out failure)
				|| !KingdomExperienceRules.TryGetEnableEpoch(system.Experience,
					KingdomExperienceOptionKind.CivicStory, now, out long epoch, out failure))
				return false;
			audience = new KingdomExperienceAudienceReceipt
			{
				ReservationId = KingdomLifecycleRules.GrowthFirstGuestAudienceReservationId(
					x.OpportunityId), RealmId = system.RealmId,
				SettlementId = candidate.SettlementId, SourceId = x.OpportunityId,
				Lane = KingdomExperienceLane.FirstGuest,
				OptionKind = KingdomExperienceOptionKind.CivicStory,
				CauseTick = now, ReservedTick = now, EnableEpoch = epoch
			};
			if (KingdomExperienceRuntime.TryReserveAudience(system, audience,
				out KingdomExperienceCapacityFault _, out failure))
			{
				if (KingdomGuestFeastRuntime.TryBeginPresentedOpportunity(system, candidate,
					out failure)) return true;
				if (!KingdomExperienceRuntime.TryReleaseAudience(system,
					audience.ReservationId, x.OpportunityId,
					out KingdomExperienceCapacityFault _, out string releaseFailure))
					failure = failure + "; audience cleanup retained: " + releaseFailure;
			}
			audience = null; return false;
		}

		private static bool ReleaseStaleAudience(KingdomSystem system,
			KingdomGrowthFirstGuestOpportunity x, out string failure)
		{
			failure = null;
			if (system?.Experience == null) return true;
			string id = KingdomLifecycleRules.GrowthFirstGuestAudienceReservationId(
				x.OpportunityId);
			if (!KingdomExperienceRules.TryReadAudienceLease(system.Experience, id,
				out KingdomExperienceAudienceReceipt row, out KingdomExperienceLeaseState _,
				out failure)) return false;
			if (row == null) return true;
			if (row.SourceId != x.OpportunityId)
				return Fail("first-guest audience identity belongs to another source", out failure);
			return KingdomExperienceRuntime.TryReleaseAudience(system, id, x.OpportunityId,
				out KingdomExperienceCapacityFault _, out failure);
		}

		private static bool ReleaseUnadmittedBody(KingdomSystem system,
			KingdomGrowthArrivalCandidate candidate, out string failure)
		{
			failure = null;
			KingdomGrowthFirstGuestOpportunity x = candidate?.FirstGuest;
			if (system?.Experience == null || x == null
				|| x.BodyLeaseState != KingdomGrowthFirstGuestBodyLeaseState.None) return true;
			string id = KingdomLifecycleRules.GrowthFirstGuestBodyReservationId(x.OpportunityId);
			if (!KingdomExperienceRules.TryReadBodyLease(system.Experience, id,
				out KingdomExperienceBodyReservation row, out KingdomExperienceLeaseState _,
				out failure)) return false;
			if (row == null) return true;
			if (row.SourceId != x.OpportunityId)
				return Fail("first-guest body identity belongs to another source", out failure);
			return KingdomExperienceRuntime.TryReleaseBodies(system, id, x.OpportunityId,
				out KingdomExperienceCapacityFault _, out failure);
		}

		private static bool Fail(string reason, out string failure)
		{
			failure = reason; return false;
		}
	}
}
