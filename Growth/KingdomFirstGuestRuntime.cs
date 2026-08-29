using System;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomFirstGuestRuntime
	{
		public static string CharterLabel(KingdomSystem system)
		{
			KingdomGrowthArrivalCandidate candidate =
				system?.LifecycleBook?.Growth?.ArrivalCandidate;
			KingdomGrowthFirstGuestOpportunity x = candidate?.FirstGuest;
			if (x == null || x.ChoiceState != KingdomGrowthFirstGuestChoiceState.AwaitingChoice
				&& x.ChoiceState != KingdomGrowthFirstGuestChoiceState.Deferred)
				return "{{K|No first guest is awaiting an answer}}";
			return KingdomMaster.NewWorkAllowed(system)
				? "{{W|Read the first guest's correspondence}}"
				: "{{W|Read the first guest's correspondence}} "
					+ "{{K|(simulation paused; read-only)}}";
		}

		public static void Open(KingdomSystem system, GameObject founder)
		{
			long now = The.Game?.TimeTicks ?? -1L;
			KingdomGrowthBook growth = system?.LifecycleBook?.Growth;
			KingdomGrowthArrivalCandidate candidate = growth?.ArrivalCandidate;
			KingdomGrowthFirstGuestOpportunity x = candidate?.FirstGuest;
			if (now < 0L || growth == null || candidate == null || x == null
				|| !KingdomLifecycleRules.CanOwnGrowthAuthority(growth, growth.SettlementId)
				|| x.ChoiceState != KingdomGrowthFirstGuestChoiceState.AwaitingChoice
					&& x.ChoiceState != KingdomGrowthFirstGuestChoiceState.Deferred)
			{
				Popup.Show("No Growth-owned first-guest correspondence is awaiting a choice.");
				return;
			}
			if (!KingdomMaster.NewWorkAllowed(system))
			{
				Popup.Show("Settlement simulation is paused; this Growth correspondence is "
					+ "read-only until settlement work resumes.\n\n"
					+ ComposeFacts(candidate, false,
						"simulation paused; read-only record"));
				return;
			}
			bool rendered = TryOpenPresentationLease(system, candidate, now,
				out KingdomExperienceAudienceReceipt audience, out string presentationFailure);
			try
			{
				string intro = ComposeFacts(candidate, rendered, presentationFailure);
				KingdomExperienceRuntime.TryRecord(system,
					KingdomExperienceExperiment.FirstGuestCorrespondence,
					rendered ? KingdomExperienceTrialArm.SemanticOnly
						: KingdomExperienceTrialArm.FactsOnly,
					KingdomExperienceObservationKind.Exposed, 1);
				int pick = Popup.PickOption(Title: "A first guest writes to "
					+ KingdomPresentation.Rich(system.KingdomDisplayName), Intro: intro,
					Options: new string[] { "Admit this person through Growth",
						"Defer without limit", "Decline without penalty" },
					Hotkeys: new char[] { 'a', 'd', 'x' }, AllowEscape: true);
				if (pick == 0) Admit(system, founder, growth, candidate, now);
				else if (pick == 1) Defer(system, growth, candidate, now);
				else if (pick == 2) Decline(system, founder, growth, candidate, now);
			}
			finally
			{
				if (audience != null && !KingdomExperienceRuntime.TryReleaseAudience(system,
					audience.ReservationId, x.OpportunityId,
					out KingdomExperienceCapacityFault _, out string releaseFailure))
					KingdomLog.Log("first-guest audience cleanup retained: " + releaseFailure);
			}
		}

		private static void Defer(KingdomSystem system, KingdomGrowthBook growth,
			KingdomGrowthArrivalCandidate candidate, long now)
		{
			if (!KingdomLifecycleRules.TryDeferGrowthFirstGuest(growth, candidate, now))
				Popup.Show("The exact Growth opportunity changed; nothing was overwritten.");
			else
			{
				KingdomExperienceRuntime.TryRecord(system,
					KingdomExperienceExperiment.FirstGuestCorrespondence,
					KingdomExperienceTrialArm.FactsOnly,
					KingdomExperienceObservationKind.Viewed, 1);
				Popup.Show("The answer is deferred without expiry, charge, or hidden departure.");
			}
		}

		private static void Decline(KingdomSystem system, GameObject founder,
			KingdomGrowthBook growth, KingdomGrowthArrivalCandidate candidate, long now)
		{
			if (!KingdomLifecycleRules.TryDeclineGrowthFirstGuest(growth, candidate, now))
			{
				Popup.Show("The exact Growth opportunity changed; nothing was overwritten."); return;
			}
			if (KingdomMaster.NewWorkAllowed(system))
				KingdomGrowth.TryContinueFirstGuestDecision(system, founder, now, out string _);
			KingdomExperienceRuntime.TryRecord(system,
				KingdomExperienceExperiment.FirstGuestCorrespondence,
				KingdomExperienceTrialArm.FactsOnly,
				KingdomExperienceObservationKind.Closed, 1);
			Popup.Show("The correspondence is declined. No body, value, standing, or penalty was made.");
		}
	}
}
