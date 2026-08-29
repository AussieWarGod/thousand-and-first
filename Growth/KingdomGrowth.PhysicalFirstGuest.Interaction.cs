using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		public static bool CanUsePhysicalFirstGuest(GameObject body, GameObject actor,
			string candidateId, string opportunityId)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			KingdomGrowthArrivalCandidate candidate =
				system?.LifecycleBook?.Growth?.ArrivalCandidate;
			return actor != null && actor.IsPlayer() && candidate?.Id == candidateId
				&& candidate.FirstGuest?.OpportunityId == opportunityId
				&& candidate.Phase == KingdomGrowthArrivalCandidatePhase.GuestHosted
				&& candidate.FirstGuest.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Hosted
				&& TryExactLoadedPhysicalFirstGuest(candidate, body?.CurrentZone,
					out GameObject exact) && ReferenceEquals(exact, body)
				&& ExactPhysicalFirstGuestHardening(body, candidate,
					body.GetPart<r_KingdomFirstGuestBody>());
		}

		public static void OpenPhysicalFirstGuest(GameObject body, GameObject actor,
			string candidateId, string opportunityId)
		{
			if (!CanUsePhysicalFirstGuest(body, actor, candidateId, opportunityId))
			{
				Popup.Show("This person is not the exact first guest named by Growth."); return;
			}
			KingdomSystem system = The.Game.GetSystem<KingdomSystem>();
			KingdomGrowthBook growth = system.LifecycleBook.Growth;
			KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
			int pick = Popup.PickOption(Title: "Your first guest", Intro:
				candidate.PlannedName + " remains a guest: present and named, but not yet a citizen.",
				Options: new string[] { "Welcome as citizen", "Ask to depart", "Remain our guest" },
				Hotkeys: new char[] { 'w', 'd', 'r' }, AllowEscape: true);
			long now = The.Game.TimeTicks;
			if (pick == 0) WelcomePhysicalFirstGuest(system, growth, candidate, body, actor, now);
			else if (pick == 1) DepartPhysicalFirstGuest(system, growth, candidate, body, now);
			else if (pick == 2) Popup.Show(candidate.PlannedName
				+ " remains your guest without deadline, cost, work, or hidden consequence.");
		}

		private static void WelcomePhysicalFirstGuest(KingdomSystem system,
			KingdomGrowthBook growth, KingdomGrowthArrivalCandidate candidate, GameObject body,
			GameObject actor, long now)
		{
			string failure = null;
			if (!KingdomMaster.NewWorkAllowed(system)
				|| !KingdomExperienceRuntime.TryObserveConfiguredOptions(system, now,
					out failure)
				|| !KingdomExperienceRules.TryGetEnableEpoch(system.Experience,
					KingdomExperienceOptionKind.CivicStory, now, out long _, out failure))
			{
				Popup.Show((failure ?? "Settlement work or civic stories are paused")
					+ ". The guest remains unchanged."); return;
			}
			Zone zone = body.CurrentZone;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(zone) ?? KingdomSurvey.Take(zone, system);
			int supportCap = system.SupportedLevel > 0
				? KingdomSubsidenceRules.SlideBeginsAbove(system.SupportedLevel) : int.MaxValue;
			if (survey == null || !KingdomLifecycleRules.TryCheckGrowthFirstGuestCurrentApplicability(
				growth, candidate, system.Population, KingdomRules.MaxPopulation,
				system.SupportedLevel, supportCap, survey == null ? -1 : survey.StoredWater,
				KingdomRules.DramsPerArrival, out failure))
			{
				Popup.Show((failure ?? "Current conditions refuse citizenship")
					+ ". The guest remains unchanged."); return;
			}
			if (!KingdomLifecycleRules.TryBeginGrowthFirstGuestCitizenship(growth, candidate, now))
			{
				Popup.Show("The exact guest state changed; nothing was overwritten."); return;
			}
			KingdomGovernanceScope.Commit("welcome first guest as citizen");
			ContinueCommittedPhysicalFirstGuestAction(system, zone, candidate, body);
			if (candidate.FirstGuest.GuestPhase ==
				KingdomGrowthFirstGuestGuestPhase.CitizenshipPrepared)
				TryContinueFirstGuestDecision(system, actor, now, out string _);
		}

		private static void DepartPhysicalFirstGuest(KingdomSystem system,
			KingdomGrowthBook growth, KingdomGrowthArrivalCandidate candidate, GameObject body,
			long now)
		{
			if (!KingdomLifecycleRules.TryBeginGrowthFirstGuestDeparture(growth, candidate, now))
			{
				Popup.Show("The exact guest state changed; nothing was overwritten."); return;
			}
			KingdomGovernanceScope.Commit("ask first guest to depart");
			ContinueCommittedPhysicalFirstGuestAction(system, body.CurrentZone, candidate, body);
			bool departed = candidate.Phase == KingdomGrowthArrivalCandidatePhase.GuestTerminal
				&& candidate.FirstGuest?.GuestPhase ==
					KingdomGrowthFirstGuestGuestPhase.Terminal
				&& candidate.FirstGuest.GuestTerminalState ==
					KingdomGrowthFirstGuestTerminalState.Departed;
			Popup.Show(departed
				? candidate.PlannedName + " departs without penalty or reward."
				: "Departure remains committed, pending exact loaded-body recovery.");
		}
	}
}
