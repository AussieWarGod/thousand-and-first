using System;
using System.Collections.Generic;

using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	public static partial class KingdomCrops
	{
		// ==================================================================================
		// Small shared helpers
		// ==================================================================================

		/// <summary>The finished field under this cell, or null. A field is a rect, so the
		/// founder may be standing anywhere in its footprint rather than on the one cell the
		/// building object occupies.</summary>
		public static GameObject FieldUnder(Zone Z, Cell C)
		{
			if (Z == null || C == null)
			{
				return null;
			}
			GameObject best = null;
			foreach (GameObject item in Z.GetObjects())
			{
				if (FieldOf(item) == null)
				{
					continue;
				}
				Cell at = item.CurrentCell;
				if (at != null && at.X == C.X && at.Y == C.Y)
				{
					return item;
				}
				KingdomPlotRules.PlotRect rect;
				if (best == null && KingdomPlots.TryReadFootprint(item, out rect) && rect.Contains(C.X, C.Y))
				{
					best = item;
				}
			}
			return best;
		}

		/// <summary>What the founder calls a crop, off its own blueprint rather than out of a
		/// second table that could disagree with it.</summary>
		public static string CropName(string CropBlueprint)
		{
			if (string.IsNullOrEmpty(CropBlueprint))
			{
				return "the crop";
			}
			GameObjectBlueprint blueprint = GameObjectFactory.Factory.GetBlueprintIfExists(CropBlueprint);
			string name = (blueprint == null) ? null : blueprint.DisplayName();
			return string.IsNullOrEmpty(name) ? CropBlueprint : name;
		}

		/// <summary>A tick as an int property can hold it. Clamped rather than wrapped, for the
		/// reason <c>KingdomSubsidence.SeenStamp</c> clamps: a game that somehow outruns the slot
		/// stops dating rather than reading as the future.</summary>
		public static int StampOf(long TimeTicks)
		{
			if (TimeTicks <= 0L)
			{
				return 0;
			}
			return (TimeTicks >= int.MaxValue) ? int.MaxValue : (int)TimeTicks;
		}

		/// <summary>
		/// Says a field's want once and unsays it when the block lifts (STANDARDS 7b). The flag is
		/// the want itself rather than a bare bool, so a field that stops wanting hands and starts
		/// wanting a larder says the new thing instead of staying silent.
		/// </summary>
		public static void Announce(KingdomSystem System, GameObject Work, KingdomCropRules.FieldWant Want)
		{
			if (System == null || Work == null)
			{
				return;
			}
			if (Want == KingdomCropRules.FieldWant.None)
			{
				Work.SetIntProperty(SaidProperty, 0);
				return;
			}
			if (Work.GetIntProperty(SaidProperty) == (int)Want)
			{
				return;
			}
			Work.SetIntProperty(SaidProperty, (int)Want);
			string line = KingdomCropRules.WantNote(Want, Work.ShortDisplayName, KingdomPresentation.Rich(System.KingdomDisplayName));
			System.Ledger.Note("{{r|" + line + "}}");
			MessageQueue.AddPlayerMessage("{{r|" + line + "}}");
		}
	}
}
