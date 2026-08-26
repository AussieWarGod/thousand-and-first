using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomBounty
	{
		private static bool PickRect(KingdomSystem System, Zone Z, GameObject Founder, out int X1, out int Y1, out int X2, out int Y2, out int Cells)
		{
			X1 = 0;
			Y1 = 0;
			X2 = 0;
			Y2 = 0;
			Cells = 0;
			Cell here = (Founder != null) ? Founder.CurrentCell : null;
			if (here == null)
			{
				return false;
			}
			int[] widths = new int[3] { 3, 5, 9 };
			int[] heights = new int[3] { 3, 5, 7 };
			string[] names = new string[3] { "the ground you stand on", "a working yard", "a wide clearing" };
			string[] options = new string[3];
			int[][] rects = new int[3][];
			KingdomMaterials.ClearanceAssessment[] assessed = new KingdomMaterials.ClearanceAssessment[3];
			for (int i = 0; i < 3; i++)
			{
				int left = here.X - widths[i] / 2;
				int top = here.Y - heights[i] / 2;
				rects[i] = new int[4] { left, top, left + widths[i] - 1, top + heights[i] - 1 };
				assessed[i] = KingdomMaterials.Assess(System, Z, rects[i][0], rects[i][1], rects[i][2], rects[i][3]);
				string size = " (" + widths[i] + " by " + heights[i] + ")";
				if (!assessed[i].Valid)
				{
					options[i] = "{{K|" + names[i] + size + " -- runs off the edge of this ground}}";
				}
				else if (assessed[i].Refusal != null)
				{
					options[i] = "{{K|" + names[i] + size + " -- something stands in it}}";
				}
				else if (assessed[i].Standing <= 0)
				{
					options[i] = "{{K|" + names[i] + size + " -- nothing in it has to come down}}";
				}
				else
				{
					options[i] = names[i] + size + " {{K|(" + assessed[i].Standing + " standing, and "
						+ (assessed[i].Yield.Describe() ?? "turned earth") + " out of it)}}";
				}
			}
			int pick = Popup.PickOption(
				Title: "Which ground?",
				Intro: "A clearance notice pays twice: the price you post, and whatever comes out of the ground.",
				Options: options, AllowEscape: true);
			if (pick < 0)
			{
				return false;
			}
			if (!assessed[pick].Valid)
			{
				Popup.Show("That ground runs off the edge of this one.");
				return false;
			}
			if (assessed[pick].Refusal != null)
			{
				Popup.Show(assessed[pick].Refusal);
				return false;
			}
			if (assessed[pick].Standing <= 0)
			{
				Popup.Show("There is nothing in it that has to come down. A notice over clear ground would never be claimed.");
				return false;
			}
			X1 = rects[pick][0];
			Y1 = rects[pick][1];
			X2 = rects[pick][2];
			Y2 = rects[pick][3];
			Cells = assessed[pick].Cells;
			return true;
		}

		private static bool PickPile(KingdomSystem System, Zone Z, GameObject Founder, out GameObject Pile, out int Units)
		{
			Pile = null;
			Units = 0;
			List<GameObject> candidates = MarkablePiles(Z, Founder);
			if (candidates.Count == 0)
			{
				Popup.Show("There is no pile of materials within reach to mark.");
				return false;
			}
			string[] options = new string[candidates.Count];
			int[] counts = new int[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
			{
				counts[i] = MaterialUnits(candidates[i]);
				options[i] = candidates[i].ShortDisplayName + " {{K|(" + counts[i]
					+ ((counts[i] == 1) ? " load" : " loads") + " of material)}}";
			}
			int pick = Popup.PickOption(
				Title: "Which pile?",
				Intro: "The mark is the whole of the designation. Nothing is ever carried out of a container you have not marked, and the mark comes off when the notice does.",
				Options: options, AllowEscape: true);
			if (pick < 0)
			{
				return false;
			}
			Pile = candidates[pick];
			Units = counts[pick];
			return true;
		}

		private static int PickPrice(KingdomSystem System, KingdomSurvey Survey, BountyTask Task, int Magnitude)
		{
			int suggested = KingdomBountyRules.SuggestedPrice(Task, Magnitude);
			string[] options = new string[PriceLadder.Length];
			for (int i = 0; i < PriceLadder.Length; i++)
			{
				int price = KingdomBountyRules.ClampPrice(PriceLadder[i]);
				options[i] = price + ((price == 1) ? " dram" : " drams")
					+ ((price == suggested) ? " {{G|[what the work is worth]}}" : "")
					+ ((price > Survey.StoredWater) ? " {{r|(more than the stores hold today)}}" : "");
			}
			int pick = Popup.PickOption(
				Title: "Name the price",
				Intro: "The stores hold " + Survey.StoredWater + " drams. None of it is set aside by posting; the price is drawn the day the work is done.",
				Options: options, AllowEscape: true);
			return (pick < 0) ? 0 : KingdomBountyRules.ClampPrice(PriceLadder[pick]);
		}

	}
}
