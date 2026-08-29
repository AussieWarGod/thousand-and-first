using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>One Charter command. Offer publication always precedes its first display.</summary>
	public static partial class KingdomFirstFeastRuntime
	{
		public static void Open(KingdomSystem System, GameObject Founder)
		{
			if (!TryCurrentCity(System, Founder, out CityContext context, out string failure))
			{
				Popup.Show(failure); return;
			}
			long now = Now();
			if (!KingdomExperienceRuntime.TryObserveConfiguredOptions(System, now, out failure)
				|| !KingdomExperienceRules.TryGetFirstFeast(System.Experience,
					context.SettlementId, out KingdomFirstFeastReceipt receipt, out failure))
			{
				Popup.Show(failure ?? "The bounded experience ledger is unavailable."); return;
			}
			bool storyEnabled = KingdomExperienceRules.CanEmit(System.Experience,
				KingdomExperienceOptionKind.CivicStory, now);
			if (!storyEnabled && receipt?.Phase == KingdomFirstFeastPhase.Offered)
			{
				if (!KingdomExperienceRules.TryArchiveFirstFeastOffer(System.Experience,
					System.Experience.Revision, context.SettlementId, now, out _,
					out receipt, out failure))
				{
					Popup.Show(failure); return;
				}
			}
			if (!storyEnabled && receipt == null)
			{
				Popup.Show("Civic stories are disabled. No First Feast offer or re-enable "
					+ "backlog was created."); return;
			}
			if (receipt == null && !TryPublishOffer(System, context, now, out receipt, out failure))
			{
				Popup.Show(failure); return;
			}
			if (receipt.Phase != KingdomFirstFeastPhase.Offered)
			{
				ShowExisting(System, Founder, context, receipt); return;
			}

			bool proposer = ResidentAvailable(System, context.SettlementId,
				receipt.ProposerResidentId);
			bool witness = ResidentAvailable(System, context.SettlementId,
				receipt.WitnessResidentId);
			string rendering = KingdomFirstFeastRules.RenderOffer(receipt, proposer, witness);
			KingdomExperienceRuntime.TryRecord(System,
				KingdomExperienceExperiment.FirstFeastPractice,
				proposer && witness ? KingdomExperienceTrialArm.SemanticOnly
					: KingdomExperienceTrialArm.FactsOnly,
				KingdomExperienceObservationKind.Exposed, 1);
			int choice = Popup.PickOption(
				Title: "First Feast of " + KingdomPresentation.Rich(context.SettlementName),
				Intro: rendering + "\n\nThis is a civic practice proposal, not a meal or recipe.",
				Options: new string[]
				{
					"Adopt the proposed dedication",
					"Adapt the dedication",
					"Refuse the practice",
					"Defer without a deadline"
				}, Hotkeys: new char[] { 'a', 'd', 'r', 'w' }, AllowEscape: true);
			if (choice < 0 || choice > 3) return;
			KingdomFirstFeastChoice decision = choice == 0 ? KingdomFirstFeastChoice.Adopt
				: choice == 1 ? KingdomFirstFeastChoice.Adapt
				: choice == 2 ? KingdomFirstFeastChoice.Refuse
				: KingdomFirstFeastChoice.Defer;
			string dedication = null;
			if (decision == KingdomFirstFeastChoice.Adapt
				&& !TryChooseDedication(out dedication)) return;
			string disclosure = KingdomFirstFeastRules.DecisionDisclosure(decision, dedication);
			if (Popup.ShowYesNo(disclosure + "\n\nProceed?") != DialogResult.Yes) return;
			if (!KingdomExperienceRules.TryDecideFirstFeast(System.Experience,
				System.Experience.Revision, context.SettlementId, decision, dedication, now,
				out bool committed, out KingdomFirstFeastReceipt decided, out failure))
			{
				Popup.Show(failure); return;
			}
			if (committed && (decision == KingdomFirstFeastChoice.Adopt
				|| decision == KingdomFirstFeastChoice.Adapt))
			{
				string governance = decision == KingdomFirstFeastChoice.Adopt
					? "adopt First Feast practice"
					: "adapt First Feast practice";
				KingdomGovernanceScope.Commit(governance);
			}
			if (decision != KingdomFirstFeastChoice.Defer
				&& !KingdomGuestFeastRuntime.TryObservePractice(System, Founder.CurrentZone,
					decided, out string guestFailure))
				KingdomLog.Log("guest feast: First Feast observation retained ("
					+ guestFailure + ")");
			if (decision == KingdomFirstFeastChoice.Defer)
			{
				KingdomExperienceRuntime.TryRecord(System,
					KingdomExperienceExperiment.FirstFeastPractice,
					KingdomExperienceTrialArm.SemanticOnly,
					KingdomExperienceObservationKind.Viewed, 1);
				Popup.Show("The proposal remains open without a deadline. Nothing changed."); return;
			}
			if (decision == KingdomFirstFeastChoice.Refuse)
			{
				KingdomExperienceRuntime.TryRecord(System,
					KingdomExperienceExperiment.FirstFeastPractice,
					KingdomExperienceTrialArm.SemanticOnly,
					KingdomExperienceObservationKind.Closed, 1);
				Popup.Show(KingdomFirstFeastRules.RenderOutcome(decided)); return;
			}
			bool history = TellPractice(System, decided);
			KingdomExperienceRuntime.TryRecord(System,
				KingdomExperienceExperiment.FirstFeastPractice,
				KingdomExperienceTrialArm.SemanticOnly,
				KingdomExperienceObservationKind.Committed, 1);
			Popup.Show(KingdomFirstFeastRules.RenderOutcome(decided) + "\n\n"
				+ RecipeStatus(context.Book)
				+ (history ? "" : "\n\nIts attributed Chronicle telling remains pending recovery."));
		}

		private static bool TryPublishOffer(KingdomSystem System, CityContext Context,
			long NowTick, out KingdomFirstFeastReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (!TryDeed(System, Context, out KingdomFirstFeastDeed deed, out Failure)) return false;
			if (!KingdomExperienceRules.TryGetEnableEpoch(System.Experience,
				KingdomExperienceOptionKind.CivicStory, deed.DeedTick, out long epoch,
				out Failure))
			{
				Failure = "No First Feast proposal was created: this founding deed predates the "
					+ "current civic-story option epoch."; return false;
			}
			if (!TryCandidates(Context, out KingdomFirstFeastCandidate[] candidates, out Failure)
				|| !KingdomFirstFeastRules.TryPrepare(deed, candidates, NowTick, epoch,
					out KingdomFirstFeastReceipt offer, out Failure)
				|| !KingdomExperienceRules.TryPublishFirstFeastOffer(System.Experience,
					System.Experience.Revision, offer, out Failure)) return false;
			return KingdomExperienceRules.TryGetFirstFeast(System.Experience,
				Context.SettlementId, out Receipt, out Failure) && Receipt != null;
		}

		private static bool TryChooseDedication(out string Dedication)
		{
			Dedication = null;
			string[] values = new string[] { KingdomFirstFeastRules.ResidentDedication,
				KingdomFirstFeastRules.TravelerDedication,
				KingdomFirstFeastRules.RemembranceDedication };
			int choice = Popup.PickOption(Title: "Adapt the dedication", Options: values,
				Hotkeys: new char[] { 'r', 't', 'd' }, AllowEscape: true);
			if (choice < 0 || choice >= values.Length) return false;
			Dedication = values[choice]; return true;
		}
	}
}
