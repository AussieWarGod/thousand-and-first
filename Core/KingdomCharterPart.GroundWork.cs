using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCharterPart
	{
		/// <summary>
		/// The two things a crew does to bare ground: take down what stands on it, or lay what the
		/// settlement's own feet have already decided. The report of what is worn is the intro,
		/// because the founder should be able to see whether there is anything to pave in the same
		/// breath they are asked.
		/// </summary>
		public void GroundWork(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			int num = Popup.PickOption(
				Title: "The ground at " + KingdomPresentation.Rich(System.SeatName),
				Intro: KingdomRoads.WornLine(zone),
				Options: new string[2] { "Order ground cleared", "Pave a worn path" },
				Hotkeys: new char[2] { 'c', 'p' },
				AllowEscape: true);
			if (num == 0)
			{
				ClearGround(System);
				return;
			}
			string failure = null;
			if (num == 1 && KingdomRoads.Pave(System, zone, ParentObject.CurrentCell, out failure))
			{
				return;
			}
			else if (num == 1 && failure != null)
			{
				Popup.Show(failure);
			}
		}

		/// <summary>
		/// Orders ground cleared around where the founder is standing. The size is the founder's
		/// choice and nothing here gates it: what may then be laid on cleared ground is the plan's
		/// business, not the clearing gang's. The service does its own messaging; this only picks
		/// the rect and surfaces a decline.
		/// </summary>
		public void ClearGround(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			Cell cell = ParentObject.CurrentCell;
			if (zone == null || cell == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Ground is cleared on the kingdom's own claim.");
				return;
			}
			int[] widths = new int[3] { 3, 5, 9 };
			int[] heights = new int[3] { 3, 5, 7 };
			string[] names = new string[3] { "the ground you stand on", "a working yard", "a wide clearing" };
			string[] options = new string[3];
			int[][] rects = new int[3][];
			for (int i = 0; i < 3; i++)
			{
				int x1 = cell.X - widths[i] / 2;
				int y1 = cell.Y - heights[i] / 2;
				rects[i] = new int[4] { x1, y1, x1 + widths[i] - 1, y1 + heights[i] - 1 };
				KingdomMaterials.ClearanceAssessment assessment = KingdomMaterials.Assess(System, zone, rects[i][0], rects[i][1], rects[i][2], rects[i][3]);
				string size = " (" + widths[i] + " by " + heights[i] + ")";
				if (!assessment.Valid)
				{
					options[i] = "{{K|" + names[i] + size + " — runs off the edge of this ground}}";
				}
				else if (assessment.Refusal != null)
				{
					options[i] = "{{K|" + names[i] + size + " — something stands in it}}";
				}
				else
				{
					string yield = assessment.Yield.Describe();
					options[i] = names[i] + size + " — " + KingdomMaterialRules.DaysForOneHand(assessment.Effort) + " hand-days"
						+ ((yield == null) ? "" : (", for " + yield));
				}
			}
			int num = Popup.PickOption(Title: "Clear ground at " + KingdomPresentation.Rich(System.SeatName),
				Intro: "Clearing spends no water. It spends the hands the water detail and the works have left over, and everything that comes down is carried to the stockpiles.",
				Options: options, AllowEscape: true);
			if (num < 0)
			{
				return;
			}
			if (!KingdomMaterials.StakeClearance(System, zone, rects[num][0], rects[num][1], rects[num][2], rects[num][3], out var failure))
			{
				Popup.Show(failure);
				return;
			}
		}

		/// <summary>
		/// Condemns one of the settlement's own buildings, or calls off a condemnation already
		/// standing. The service does its own eligibility check and its own messaging; this only
		/// picks the target and surfaces a decline.
		/// </summary>
		public void StrikeBuilding(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			Cell cell = ParentObject.CurrentCell;
			if (zone == null || cell == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Buildings are struck on the kingdom's own ground.");
				return;
			}
			System.Collections.Generic.List<GameObject> candidates = new System.Collections.Generic.List<GameObject>();
			CollectBuiltNear(cell, candidates);
			foreach (Cell adjacent in cell.GetLocalAdjacentCells())
			{
				CollectBuiltNear(adjacent, candidates);
			}
			if (candidates.Count == 0)
			{
				Popup.Show("Stand beside something " + KingdomPresentation.Rich(System.SeatName) + " built to take it down.");
				return;
			}
			string[] options = new string[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
			{
				int left = candidates[i].GetIntProperty(KingdomMaterials.StrikeEffortProperty);
				options[i] = candidates[i].ShortDisplayName
					+ ((left > 0) ? (" {{r|[condemned, " + KingdomMaterialRules.DaysForOneHand(left) + " hand-days left]}}") : "");
			}
			int num = Popup.PickOption(Title: "Take down a building at " + KingdomPresentation.Rich(System.SeatName),
				Intro: "Striking frees the plot and returns half of what the building was made of. It refunds no water, and picking one already condemned calls the order off.",
				Options: options, AllowEscape: true);
			if (num < 0)
			{
				return;
			}
			if (!KingdomMaterials.OrderStrike(System, zone, candidates[num], out var failure,
				"condemn building"))
			{
				Popup.Show(failure);
				return;
			}
		}

		private static void CollectBuiltNear(Cell C, System.Collections.Generic.List<GameObject> Into)
		{
			if (C == null)
			{
				return;
			}
			foreach (GameObject item in C.GetObjects())
			{
				if (item.GetIntProperty("KingdomBuilt") == 1 && !Into.Contains(item))
				{
					Into.Add(item);
				}
			}
		}

	}
}
