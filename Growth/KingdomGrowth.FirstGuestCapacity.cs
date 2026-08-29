using System;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		internal static KingdomExperienceBodyReservation NewFirstGuestBodyRequest(
			KingdomSystem system, KingdomGrowthArrivalCandidate candidate, long tick,
			long enableEpoch)
		{
			KingdomGrowthFirstGuestOpportunity x = candidate?.FirstGuest;
			if (system == null || x == null || tick < x.CauseTick || enableEpoch <= 0L) return null;
			return new KingdomExperienceBodyReservation
			{
				ReservationId = KingdomLifecycleRules.GrowthFirstGuestBodyReservationId(
					x.OpportunityId), RealmId = system.RealmId,
				SettlementId = candidate.SettlementId, SourceId = x.OpportunityId,
				Lane = KingdomExperienceLane.FirstGuest,
				OptionKind = KingdomExperienceOptionKind.CivicStory,
				CauseTick = tick, ReservedTick = tick, EnableEpoch = enableEpoch, BodyCount = 1
			};
		}

		private static KingdomExperienceBodyReservation PersistedFirstGuestBodyRequest(
			KingdomSystem system, KingdomGrowthArrivalCandidate candidate)
		{
			KingdomGrowthFirstGuestOpportunity x = candidate?.FirstGuest;
			if (system == null || x == null || x.BodyLeaseState ==
				KingdomGrowthFirstGuestBodyLeaseState.None) return null;
			return new KingdomExperienceBodyReservation
			{
				ReservationId = x.BodyReservationId, RealmId = x.BodyRealmId,
				SettlementId = candidate.SettlementId, SourceId = x.OpportunityId,
				Lane = KingdomExperienceLane.FirstGuest, OptionKind = x.BodyOptionKind,
				CauseTick = x.BodyReservedTick, ReservedTick = x.BodyReservedTick,
				EnableEpoch = x.BodyEnableEpoch, BodyCount = 1
			};
		}

		private static bool SameFirstGuestBodyRequest(KingdomExperienceBodyReservation a,
			KingdomExperienceBodyReservation b)
		{
			return a != null && b != null && a.ReservationId == b.ReservationId
				&& a.RealmId == b.RealmId && a.SettlementId == b.SettlementId
				&& a.SourceId == b.SourceId && a.Lane == b.Lane
				&& a.OptionKind == b.OptionKind && a.CauseTick == b.CauseTick
				&& a.ReservedTick == b.ReservedTick && a.EnableEpoch == b.EnableEpoch
				&& a.BodyCount == b.BodyCount;
		}

		private static bool FailFirstGuest(string reason, out string failure)
		{
			failure = reason; return false;
		}
	}
}
