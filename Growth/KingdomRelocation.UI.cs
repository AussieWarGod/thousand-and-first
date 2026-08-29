using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		public static bool CanOffer(KingdomSystem System, Zone Zone, GameObject Heart,
			KingdomUpgrade.Assessment Assessment)
		{
			return System != null && Zone != null && GameObject.Validate(Heart)
				&& Assessment.Valid && Assessment.Verdict
					== KingdomUpgradeRules.UpgradeVerdict.NoGroundToGrow
				&& Assessment.Reason != null && Assessment.Reason.IndexOf(
					"marked to yield", global::System.StringComparison.Ordinal) >= 0
				&& KingdomPlots.IsHeartPlot(Heart) && !HasActive(Zone);
		}

		public static bool OpenHeartRingCall(KingdomSystem System, Zone Zone, GameObject Heart,
			KingdomUpgrade.Assessment Assessment)
		{
			if (!CanOffer(System, Zone, Heart, Assessment)) return false;
			Dictionary<string, KingdomPlotRules.PlotRect> overrides =
				new Dictionary<string, KingdomPlotRules.PlotRect>();
			if (!TryPreparePlan(System, Zone, Heart, Assessment.SuccessorKey, overrides,
				out PreparedPlan prepared, out string failure))
			{ Popup.Show(failure ?? "The ring call cannot form an exact plan."); return false; }
			while (true)
			{
				int choice = Popup.PickOption(Title: "Call the heart's ring",
					Intro: prepared.Preview, Options: new List<string>
					{
						"{{W|Consent to this complete plan}}",
						"Choose different lawful ground",
						"Leave the ring uncalled"
					}, AllowEscape: true);
				if (choice < 0 || choice == 2) return false;
				if (choice == 1)
				{
					if (!ChooseOverride(System, Zone, Heart, Assessment.SuccessorKey,
						prepared, overrides, out prepared, out failure) && failure != null)
						Popup.Show(failure);
					continue;
				}
				if (!ReproveApproved(System, Zone, Heart, Assessment.SuccessorKey,
					prepared, out PreparedPlan exact, out failure))
				{ Popup.Show("Nothing was spent. " + failure); return false; }
				if (!TryOpen(Zone, exact.Receipt, out string encoded, out failure))
				{ Popup.Show("Nothing was spent. " + failure); return false; }
				if (!EnsureFrames(Zone, exact.Receipt, out _, out failure))
				{
					Quarantine(Zone, encoded, exact.Receipt, failure);
					Popup.Show("The exact slate was kept, but its frame could not be raised: " + failure);
					return false;
				}
				System.Ledger.Note("{{W|The heart's ring was called. "
					+ exact.Receipt.Moves.Count + " exact yielding "
					+ (exact.Receipt.Moves.Count == 1 ? "lot will move" : "lots will move")
					+ ", one at a time, for labour and no stores.}}");
				KingdomChronicle.Record(System, "the founder called the heart's ring and consented to its whole-lot plan");
				KingdomGovernanceScope.Commit("call the heart ring");
				return true;
			}
		}

		private static bool ChooseOverride(KingdomSystem System, Zone Zone, GameObject Heart,
			string SuccessorKey, PreparedPlan Current,
			Dictionary<string, KingdomPlotRules.PlotRect> Overrides,
			out PreparedPlan Updated, out string Failure)
		{
			Updated = Current; Failure = null; int picked = 0;
			if (Current.Receipt.Moves.Count > 1)
			{
				List<string> names = new List<string>();
				for (int i = 0; i < Current.Receipt.Moves.Count; i++)
					names.Add(Current.Receipt.Moves[i].DisplayName ?? Current.Receipt.Moves[i].BuildKey);
				picked = Popup.PickOption(Title: "Which whole lot?", Intro:
					"Choose the lot whose receiving ground you want to set.", Options: names,
					AllowEscape: true);
				if (picked < 0) return false;
			}
			KingdomRelocationMove move = Current.Receipt.Moves[picked];
			Cell center = The.Player?.Physics.PickDestinationCell(9999, AllowVis.OnlyExplored,
				Locked: false, IgnoreSolid: true, IgnoreLOS: true, RequireCombat: false,
				PickTarget.PickStyle.EmptyCell, "Centre the receiving lot where?");
			if (center == null || center.ParentZone != Zone) return false;
			int x1 = center.X - (move.Source.Width - 1) / 2;
			int y1 = center.Y - (move.Source.Height - 1) / 2;
			Overrides[move.PlotId] = new KingdomPlotRules.PlotRect(x1, y1,
				x1 + move.Source.Width - 1, y1 + move.Source.Height - 1);
			if (!TryPreparePlan(System, Zone, Heart, SuccessorKey, Overrides,
				out Updated, out Failure))
			{
				Overrides.Remove(move.PlotId); Updated = Current; return false;
			}
			return true;
		}
	}
}
