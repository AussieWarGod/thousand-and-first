using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomOfficeRuntime
	{
		public static void Open(KingdomSystem System, GameObject Founder)
		{
			Zone zone = Founder?.CurrentZone;
			if (Founder == null || !Founder.IsPlayer() || zone == null)
			{
				Popup.Show("Stand on the held ground of the city whose office you mean to govern.");
				return;
			}
			long now = Now();
			if (!KingdomExperienceRuntime.TryObserveConfiguredOptions(System, now,
				out string failure)) { Popup.Show(failure); return; }
			KingdomSurvey survey = KingdomSurvey.ActiveFor(zone) ?? KingdomSurvey.Take(zone, System);
			if (!TryReconcile(System, zone, survey, out failure))
			{
				Popup.Show("The civic office needs exact recovery. Nothing foreign was changed.\n\n"
					+ (failure ?? "Return with the exact holder loaded and ask again.")); return;
			}
			if (!TryContext(System, zone, survey, out CityContext context, out failure))
			{
				Popup.Show(failure); return;
			}
			if (!KingdomExperienceRules.TryGetOffice(System.Experience, context.SettlementId,
				out KingdomCivicOfficeReceipt receipt, out failure))
			{
				Popup.Show(failure); return;
			}
			if (receipt != null && receipt.Phase == KingdomCivicOfficePhase.Quarantined)
			{
				Popup.Show("The civic-office receipt is quarantined. Nothing was overwritten.\n\n"
					+ receipt.Fault); return;
			}
			if (receipt != null && (receipt.Phase == KingdomCivicOfficePhase.AppointmentPrepared
				|| receipt.Phase == KingdomCivicOfficePhase.VacancyPrepared))
			{
				Popup.Show("The office has an unfinished exact title projection. Return with "
					+ KingdomPresentation.Rich(receipt.HolderName)
					+ " loaded on this ground, then ask again."); return;
			}
			if (receipt != null && receipt.Phase == KingdomCivicOfficePhase.Held)
			{
				int pick = Popup.PickOption(Title: "The office of " + RoleFor(receipt),
					Intro: KingdomPresentation.Rich(receipt.HolderName) + " holds this title at work "
						+ receipt.WorkId + ". The title grants no succession claim. If this city can "
						+ "support stalls, its exact holder also runs the finite local market.",
					Options: new string[] { "Keep the present holder", "Release the title" },
					Hotkeys: new char[] { 'k', 'r' }, AllowEscape: true);
				if (pick != 1) return;
				if (!TryRelease(System, context, receipt, out failure)) Popup.Show(failure);
				return;
			}
			if (!TryOffer(context, out KingdomOfficeCandidate first,
				out KingdomOfficeCandidate second))
			{
				Popup.Show("The office remains vacant. Exactly two eligible named residents must "
					+ "be present before the Charter offers a choice; no timer or penalty runs."); return;
			}
			string title = KingdomOfficeRules.ChooseTitle(context.SettlementName)
				+ " of " + context.SettlementName;
			int choice = Popup.PickOption(Title: "Appoint " + title,
				Intro: "Choose between two exact residents, or leave the office vacant indefinitely. "
					+ "The title grants no succession claim. At Steading or later, its exact holder "
					+ "provides the city's finite local-market service.",
				Options: new string[] { CandidateLine(first), CandidateLine(second),
					"Leave the office vacant" }, Hotkeys: new char[] { '1', '2', 'v' },
				AllowEscape: true);
			if (choice < 0 || choice > 1) return;
			if (!TryAppoint(System, context, choice == 0 ? first : second, out failure))
				Popup.Show(failure ?? "The office did not change.");
		}

		private static string CandidateLine(KingdomOfficeCandidate C)
		{
			return KingdomPresentation.Rich(C.Name) + (string.IsNullOrEmpty(C.Origin)
				? " {{K|[origin unrecorded]}}" : " {{K|[from " + C.Origin + "]}}");
		}
	}
}
