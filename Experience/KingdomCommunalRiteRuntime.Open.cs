#if !TAF_TESTS
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomCommunalRiteRuntime
	{
		internal static void Open(KingdomSystem system, GameObject founder,
			KingdomFirstFeastRuntime.CityContext context, KingdomFirstFeastReceipt practice,
			string practiceText)
		{
			long now = XRL.The.Game == null || XRL.The.Game.TimeTicks < 1L
				? 1L : XRL.The.Game.TimeTicks;
			if (system == null || context == null
				|| !KingdomFirstFeastRules.IsAffirmative(practice)
				|| founder == null || !founder.IsPlayer() || founder.CurrentZone == null
				|| !system.OwnedZone(founder.CurrentZone.ZoneID))
			{
				Popup.Show("This practice has no exact situated communal expression."); return;
			}
			if (!TryRead(system, out KingdomCommunalRiteBook book, out string failure)
				|| !KingdomCommunalRiteRules.TryFind(book, context.SettlementId,
					out KingdomCommunalRiteReceipt row))
			{
				Popup.Show(failure ?? "Communal-expression memory is unavailable."); return;
			}
			string intro = practiceText + "\n\n" + StateText(row);
			if (!KingdomMaster.NewWorkAllowed(system))
			{
				Popup.Show(intro + "\n\nSettlement work is paused; this record is read-only.");
				return;
			}
			string[] options; char[] keys;
			if (row == null)
			{
				options = new[] { "Start a communal expression", "Close" };
				keys = new[] { 's', 'x' };
			}
			else if (row.Phase == KingdomCommunalRitePhase.Prepared
				|| row.Phase == KingdomCommunalRitePhase.Committed)
			{
				options = new[] { "Resume the communal expression",
					"Cancel the unfinished expression", "Close" };
				keys = new[] { 'r', 'c', 'x' };
			}
			else
			{
				// Terminal C18 can precede its physical acknowledgement by one crash. Reviewing
				// the record retries only that exact acknowledgement/cleanup.
				if (!TryResume(system, context, practice, now, out failure))
					Popup.Show(intro + "\n\nPhysical recovery remains pending: " + failure);
				else Popup.Show(intro);
				return;
			}
			int choice = Popup.PickOption(Title: "Communal expression of the First Feast",
				Intro: intro, Options: options, Hotkeys: keys, AllowEscape: true);
			if (choice < 0 || choice == options.Length - 1) return;
			bool ok;
			if (row == null)
			{
				if (Popup.ShowYesNo("Begin this once-only, zero-benefit gathering? It has no "
					+ "calendar, reward, or penalty for absence.") != DialogResult.Yes) return;
				ok = TryStart(system, context, practice, now, out failure);
			}
			else if (choice == 0)
				ok = TryResume(system, context, practice, now, out failure);
			else
			{
				if (Popup.ShowYesNo("Cancel this unfinished physical gathering? The adopted "
					+ "practice remains.") != DialogResult.Yes) return;
				ok = TryCancel(system, context, practice, now, out failure);
			}
			if (!ok) Popup.Show(failure ?? "Communal expression retained exact recovery state.");
			else if (TryRead(system, out book, out failure)
				&& KingdomCommunalRiteRules.TryFind(book, context.SettlementId, out row))
				Popup.Show(StateText(row));
			else Popup.Show(failure ?? "Communal expression changed, but its record is unreadable.");
		}

		private static string StateText(KingdomCommunalRiteReceipt row)
		{
			if (row == null)
				return "No communal expression has been started. It is optional and once-only.";
			switch (row.Phase)
			{
			case KingdomCommunalRitePhase.Prepared:
				return "The expression is prepared in civic memory; no physical gathering was queued.";
			case KingdomCommunalRitePhase.Committed:
				return "The expression is committed. Resume it here, or cancel its physical gathering.";
			case KingdomCommunalRitePhase.Attended:
				return "The gathered residents completed this expression. It grants no benefit and is quiet now.";
			case KingdomCommunalRitePhase.Suppressed:
				return "The physical expression was cancelled or suppressed; the adopted practice remains.";
			default:
				return "The communal-expression record cannot be interpreted.";
			}
		}
	}
}
#endif
