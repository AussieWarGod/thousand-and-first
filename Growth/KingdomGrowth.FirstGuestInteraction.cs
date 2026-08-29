using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		internal static bool TryContinueFirstGuestDecision(KingdomSystem system,
			GameObject founder, long tick, out string failure)
		{
			failure = null;
			Zone zone = founder?.CurrentZone;
			KingdomGrowthArrivalCandidate candidate =
				system?.LifecycleBook?.Growth?.ArrivalCandidate;
			if (founder == null || !founder.IsPlayer() || zone == null || candidate == null
				|| system.ClaimedZones == null || !system.ClaimedZones.Contains(zone.ZoneID)
				|| candidate.LodgingZoneId != zone.ZoneID)
			{
				failure = "stand on the exact held ground named by this correspondence";
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.ActiveFor(zone) ?? KingdomSurvey.Take(zone, system);
			ArrivalResult result = ReconcileArrival(system, zone, survey, tick,
				out ArrivalRefusal _);
			if (result != ArrivalResult.Failed) return true;
			failure = "first-guest transaction retained its exact evidence for recovery";
			return false;
		}
	}
}
