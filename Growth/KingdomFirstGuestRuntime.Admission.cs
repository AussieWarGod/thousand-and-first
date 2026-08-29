using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomFirstGuestRuntime
	{
		private static void Admit(KingdomSystem system, GameObject founder,
			KingdomGrowthBook growth, KingdomGrowthArrivalCandidate candidate, long now)
		{
			Zone zone = founder?.CurrentZone;
			if (!KingdomMaster.NewWorkAllowed(system) || zone == null
				|| candidate.LodgingZoneId != zone.ZoneID || !founder.IsPlayer()
				|| system.ClaimedZones == null || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Admission needs the exact held ground loaded and settlement work resumed. "
					+ "The opportunity is unchanged."); return;
			}
			KingdomSurvey survey = KingdomSurvey.ActiveFor(zone) ?? KingdomSurvey.Take(zone, system);
			int supportCap = system.SupportedLevel > 0
				? KingdomSubsidenceRules.SlideBeginsAbove(system.SupportedLevel) : int.MaxValue;
			string failure = null;
			if (survey == null || !KingdomLifecycleRules.TryCheckGrowthFirstGuestCurrentApplicability(
				growth, candidate, system.Population, KingdomRules.MaxPopulation,
				system.SupportedLevel, supportCap, survey == null ? -1 : survey.StoredWater,
				KingdomRules.DramsPerArrival, out failure))
			{
				Popup.Show((failure ?? "Current Growth conditions cannot admit this traveller")
					+ ". The opportunity is unchanged."); return;
			}
			Cell arrival = zone.GetCell(candidate.ArrivalX, candidate.ArrivalY);
			if (arrival == null || !arrival.IsEmpty() || !arrival.IsPassable()
				|| arrival.HasObjectWithPart("LiquidVolume"))
			{
				Popup.Show("The guest's exact arrival ground is occupied. The opportunity is unchanged.");
				return;
			}
			if (!KingdomExperienceRuntime.TryObserveConfiguredOptions(system, now, out failure)
				|| !KingdomExperienceRules.TryGetEnableEpoch(system.Experience,
					KingdomExperienceOptionKind.CivicStory, now, out long enableEpoch, out failure))
			{
				Popup.Show((failure ?? "Civic-story authority is unavailable")
					+ ". The opportunity is unchanged."); return;
			}
			KingdomExperienceBodyReservation lease = KingdomGrowth.NewFirstGuestBodyRequest(
				system, candidate, now, enableEpoch);
			if (lease == null || !KingdomExperienceRuntime.TryReserveBodies(system, lease,
				out KingdomExperienceCapacityFault _, out failure))
			{
				Popup.Show((failure ?? "No optional body capacity is available")
					+ ". The opportunity is unchanged."); return;
			}
			if (!KingdomLifecycleRules.TryAdmitGrowthFirstGuest(growth, candidate, lease, now))
			{
				if (!KingdomExperienceRuntime.TryReleaseBodies(system, lease.ReservationId,
					lease.SourceId, out KingdomExperienceCapacityFault _, out string releaseFailure))
					KingdomLog.Log("first-guest failed-admit lease retained: " + releaseFailure);
				Popup.Show("The Growth opportunity changed; admission made no body."); return;
			}
			// Growth's choice is durable. Commit the Charter
			// action before attempting physical continuation: a later projection refusal leaves
			// owned recovery work, not an action that falsely reports that nothing changed.
			KingdomGovernanceScope.Commit("admit first guest");
			KingdomExperienceRuntime.TryRecord(system,
				KingdomExperienceExperiment.FirstGuestCorrespondence,
				KingdomExperienceTrialArm.FactsOnly,
				KingdomExperienceObservationKind.Committed, 1);
			if (!KingdomGrowth.TryContinueFirstGuestDecision(system, founder, now, out failure))
				Popup.Show(failure);
			else Popup.Show("The traveller is now your guest. Citizenship remains an explicit choice.");
		}
	}
}
