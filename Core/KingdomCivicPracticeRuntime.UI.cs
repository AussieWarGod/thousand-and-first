#if !TAF_TESTS
using System.Collections.Generic;
using System.Text;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Bounded Charter presentation for exact D1 and explicit D12 service.</summary>
	public static partial class KingdomCivicPracticeRuntime
	{
		public static void OpenPracticeAndVocation(KingdomSystem system, GameObject actor)
		{
			if (system == null || actor == null || !GameObject.Validate(actor) ||
				actor.CurrentZone == null)
			{
				Popup.Show("Site practice and vocation can only be read in the loaded city.");
				return;
			}
			while (!KingdomGovernanceScope.HasCommitted)
			{
				bool paused = !KingdomMaster.NewWorkAllowed(system);
				int choice = Popup.PickOption(
					Title: "Practice & vocation",
					Intro: "Both readings use exact current-city evidence. Vocation reports show their source, sink, cadence, closure, and durable history before any service choice.",
					Options: new string[4]
					{
						"Read the site signature and choose its practice" +
							(paused ? " {{K|[read only while paused]}}" : ""),
						"Read the vocation service report" +
							(paused ? " {{K|[read only while paused]}}" : ""),
						"Read realm vocation-service results",
						"{{K|Back to people & belief}}"
					},
					Hotkeys: new char[4] { 'p', 'v', 'r', 'x' }, AllowEscape: true);
				if (choice == 0) OpenPractice(system, actor.CurrentZone);
				else if (choice == 1) ShowVocation(system, actor.CurrentZone);
				else if (choice == 2) ShowRealmVocationResults(system, actor.CurrentZone);
				else return;
			}
		}

		private static void OpenPractice(KingdomSystem system, Zone zone)
		{
			if (!TryOpenCurrent(system, zone, out KingdomSitePracticeChoiceView view,
				out string failure))
			{
				Popup.Show("The current site signature cannot be proved. Nothing changed.\n\n" +
					KingdomPresentation.Rich(failure));
				return;
			}
			int choice = Popup.PickOption(
				Title: "Exact site signature",
				Intro: PracticeEvidence(view),
				Options: new string[3]
				{
					KingdomPresentation.Rich(view.FirstTitle),
					KingdomPresentation.Rich(view.SecondTitle),
					"{{K|Leave the signature unchanged}}"
				},
				Hotkeys: new char[3] { 'a', 'b', 'x' }, AllowEscape: true);
			if (choice < 0 || choice > 1) return;
			if (!KingdomMaster.NewWorkAllowed(system))
			{
				Popup.Show("Settlement simulation is paused. The exact signature remains readable, but choosing a practice is new civic work.");
				return;
			}
			if (!TryChooseCurrent(system, zone, view, choice + 1,
				out KingdomCivicPracticeCommitResult result, out failure))
			{
				Popup.Show("The practice was not chosen. Nothing changed.\n\n" +
					KingdomPresentation.Rich(failure));
				return;
			}
			if (result == null)
			{
				Popup.Show("Civic memory accepted no practice result. Nothing was reported as changed.");
				return;
			}
			if (!result.Changed)
			{
				Popup.Show("{{G|" + KingdomPresentation.Rich(result.Title) +
					"}} is already the durable reading of this exact signature. No new governance was spent.");
				return;
			}
			KingdomGovernanceScope.Commit("choose civic practice");
			Popup.Show("{{G|" + KingdomPresentation.Rich(result.Title) + "}}\n\n" +
				KingdomPresentation.Rich(result.Description));
		}

		private static string PracticeEvidence(KingdomSitePracticeChoiceView view)
		{
			StringBuilder text = new StringBuilder();
			text.Append("{{C|Current city: }}").Append(
				KingdomPresentation.Rich(view.SettlementId)).Append('\n');
			text.Append("{{C|Vocation: }}").Append(
				KingdomPresentation.Rich(view.Vocation)).Append('\n');
			text.Append(KingdomPresentation.Rich(view.SourceSummary)).Append('\n');
			text.Append(KingdomPresentation.Rich(view.VocationNotice)).Append("\n\n");
			text.Append("{{W|A. ").Append(KingdomPresentation.Rich(view.FirstTitle))
				.Append("}}\n").Append(KingdomPresentation.Rich(view.FirstReading));
			text.Append("\n\n{{W|B. ").Append(KingdomPresentation.Rich(view.SecondTitle))
				.Append("}}\n").Append(KingdomPresentation.Rich(view.SecondReading));
			text.Append("\n\n{{K|Evidence seal: ").Append(
				KingdomPresentation.Rich(view.EvidenceDigest)).Append("}}");
			return text.ToString();
		}

		private static void ShowVocation(KingdomSystem system, Zone zone)
		{
			if (!KingdomVocationServiceRuntime.TryOpenCurrent(system, zone,
				out KingdomVocationServiceOffer offer, out string failure) || offer == null ||
				!KingdomVocationServiceRules.TryValidateOffer(offer, out failure))
			{
				Popup.Show("The current vocation report cannot be proved. Nothing opened.\n\n" +
					KingdomPresentation.Rich(failure));
				return;
			}
			bool historyReadable = KingdomVocationServiceRuntime.TryReadCurrentView(
				system, zone, offer, out string history,
				out KingdomVocationServiceStatus status, out string historyFailure);
			if (!historyReadable)
				history = "Durable history unavailable: " + historyFailure +
					" Remedy: restore the exact current-realm C18 civic-practice authority.";
			string report = VocationReport(offer, status, history);
			if (offer.State != KingdomVocationServiceOfferState.Available || !historyReadable ||
				status == null || status.State != KingdomVocationServiceActionState.Available)
			{
				Popup.Show(report);
				return;
			}
			int choice = Popup.PickOption(
				Title: "Vocation service",
				Intro: report,
				Options: new string[2]
				{
					KingdomPresentation.Rich(offer.Verb),
					"{{K|Leave without recording service}}"
				},
				Hotkeys: new char[2] { 's', 'x' }, AllowEscape: true);
			if (choice != 0) return;
			if (!KingdomMaster.NewWorkAllowed(system))
			{
				Popup.Show("Settlement simulation is paused. The report and history remain readable, but no service receipt was recorded.");
				return;
			}
			if (!KingdomVocationServiceRuntime.TryExecuteCurrent(system, zone, offer,
				out KingdomVocationServiceCommitResult result, out failure))
			{
				Popup.Show("The vocation service was not recorded. Nothing changed.\n\n" +
					KingdomPresentation.Rich(failure));
				return;
			}
			if (result == null)
			{
				Popup.Show("Civic memory accepted no vocation result. Nothing was reported as changed.");
				return;
			}
			if (!result.Changed)
			{
				Popup.Show("This exact source is already recorded. No new governance was spent.\n\n" +
					KingdomPresentation.Rich(result.ReceiptText));
				return;
			}
			Popup.Show("{{G|Vocation service recorded}}\n\n" +
				KingdomPresentation.Rich(result.ReceiptText));
		}

		private static string VocationReport(KingdomVocationServiceOffer offer,
			KingdomVocationServiceStatus status, string history)
		{
			StringBuilder text = new StringBuilder();
			text.Append("{{C|Vocation: }}").Append(
				KingdomPresentation.Rich(offer.Vocation)).Append('\n');
			text.Append(KingdomPresentation.Rich(offer.Report)).Append('\n');
			text.Append("{{K|Authority: }}").Append(
				KingdomPresentation.Rich(offer.SourceAuthority)).Append('\n');
			if (offer.State == KingdomVocationServiceOfferState.Available)
			{
				text.Append("{{C|Action state: }}").Append(status == null ? "Unknown" :
					status.State.ToString()).Append('\n');
				if (status != null)
				{
					text.Append("{{K|Series retention: }}").Append(status.SeriesCount)
						.Append('/').Append(KingdomVocationServiceRules.MaxRowsPerSeries)
						.Append(" for this city and vocation\n");
					text.Append("{{K|Realm retention: }}").Append(status.RealmCount)
						.Append('/').Append(KingdomVocationServiceRules.MaxRows).Append('\n');
				}
				text.Append("{{W|Offer: }}").Append(
					KingdomPresentation.Rich(offer.Verb)).Append('\n');
				text.Append("{{K|Exact receipt: }}").Append(
					KingdomPresentation.Rich(offer.SourceReceiptId)).Append('\n');
				text.Append("{{K|Source: }}").Append(
					KingdomPresentation.Rich(offer.SourceDescription)).Append('\n');
			}
			else if (offer.State == KingdomVocationServiceOfferState.Unavailable)
			{
				text.Append("{{r|Unavailable: }}").Append(
					KingdomPresentation.Rich(offer.UnavailableCause)).Append('\n');
				text.Append("{{K|To inspect it: }}").Append(
					KingdomPresentation.Rich(offer.Remedy)).Append('\n');
			}
			else if (offer.State == KingdomVocationServiceOfferState.Neutral)
			{
				text.Append("{{K|Holding report: neutral; no operation opens.}}\n");
			}
			text.Append("{{K|Sink: }}").Append(
				KingdomPresentation.Rich(offer.Sink)).Append('\n');
			text.Append("{{K|Cadence: }}").Append(
				KingdomPresentation.Rich(offer.Cadence)).Append('\n');
			text.Append("{{K|Closure: }}").Append(
				KingdomPresentation.Rich(offer.Closure));
			if (offer.State == KingdomVocationServiceOfferState.Available && status != null &&
				status.State == KingdomVocationServiceActionState.Available)
				text.Append("\n{{K|Cost: }}Recording one new result uses one normal 1000-energy Charter action.");
			else if (status != null && status.State ==
				KingdomVocationServiceActionState.AlreadyRecorded)
				text.Append("\n{{K|Cost: }}This exact retry is read-only and uses no governance charge.");
			else text.Append("\n{{K|Cost: }}No service action opens; no governance charge applies.");
			text.Append("\n{{K|Transfer: }}0 material/value input; 0 material/value output; source unchanged.");
			text.Append("\n{{K|History: }}").Append(KingdomPresentation.Rich(history));
			if (status != null && status.State ==
				KingdomVocationServiceActionState.AlreadyRecorded)
				text.Append("\n{{G|Recorded result: }}").Append(
					KingdomPresentation.Rich(status.ExistingReceiptText));
			text.Append("\n\nCancel, leave, and exact retry are free. Recording creates no item, yield, value, or continuing effect.");
			return text.ToString();
		}

		private static void ShowRealmVocationResults(KingdomSystem system, Zone zone)
		{
			if (!KingdomVocationServiceRuntime.TryReadRealmResults(system, zone,
				out List<string> pages, out string failure) || pages == null || pages.Count == 0)
			{
				Popup.Show("Realm vocation-service results cannot be proved. Nothing changed.\n\n" +
					KingdomPresentation.Rich(failure));
				return;
			}
			int page = 0;
			while (true)
			{
				List<string> options = new List<string>();
				List<char> hotkeys = new List<char>();
				if (page + 1 < pages.Count) { options.Add("Read next page"); hotkeys.Add('n'); }
				if (page > 0) { options.Add("Read previous page"); hotkeys.Add('p'); }
				options.Add("{{K|Leave realm results}}"); hotkeys.Add('x');
				int choice = Popup.PickOption(Title: "Realm vocation-service results",
					Intro: KingdomPresentation.Rich(pages[page]) +
						"\n\nThis view is read-only; it creates no Journal entry, item, value, or governance charge.",
					Options: options.ToArray(), Hotkeys: hotkeys.ToArray(), AllowEscape: true);
				if (page + 1 < pages.Count && choice == 0) { page++; continue; }
				if (page > 0 && choice == (page + 1 < pages.Count ? 1 : 0)) { page--; continue; }
				return;
			}
		}
	}
}
#endif
