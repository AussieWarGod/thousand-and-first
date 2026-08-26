using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.UI;
using XRL.Wish;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Read-only visual-state gallery receipts. These wishes never manufacture a state:
	/// the audit reports only facts the current ground actually carries.</summary>
	[HasWishCommand]
	public class KingdomVisualStateWishes
	{
		private const string ModVersion = KingdomReleaseInfo.Version;
		private const int MaxRows = 80;

		[WishCommand("kingdom:visuallegend", null)]
		public static void Legend()
		{
			Popup.Show("{{C|Settlement visual-state legend}}\nMod " + ModVersion + ", Qud "
				+ XRLGame.CoreVersion + "\nHash " + KingdomVisualStateRules.GalleryHash()
				+ "\n\n" + KingdomVisualStateRules.GalleryReceipt());
		}

		[WishCommand("kingdom:visualaudit", null)]
		public static void Audit()
		{
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			Zone zone = The.Player?.CurrentZone;
			if (system == null || !system.Founded || zone == null)
			{
				Popup.Show("Stand in a founded settlement to audit its real visual states.");
				return;
			}
			KingdomVisualState.Refresh(system, zone);
			List<GameObject> works = zone.GetObjectsWithPart("r_KingdomVisualState")
				?? new List<GameObject>();
			works.Sort(CompareGroundOrder);
			StringBuilder text = new StringBuilder();
			text.Append("{{C|Settlement visual-state audit}}\nMod ").Append(ModVersion)
				.Append(", Qud ").Append(XRLGame.CoreVersion).Append("\nZone ")
				.Append(zone.ZoneID).Append("\nLegend hash ")
				.Append(KingdomVisualStateRules.GalleryHash()).Append("\n");
			int shown = 0;
			for (int i = 0; i < works.Count && shown < MaxRows; i++)
			{
				GameObject work = works[i];
				if (!GameObject.Validate(work) || work.CurrentCell == null) continue;
				KingdomVisualFacts facts = KingdomVisualState.FactsOf(work);
				KingdomVisualStateKind state = KingdomVisualStateRules.Resolve(facts);
				KingdomVisualCue cue = KingdomVisualStateRules.Cue(state);
				text.Append('\n').Append(work.CurrentCell.X).Append(',')
					.Append(work.CurrentCell.Y).Append("  ").Append(work.ShortDisplayName)
					.Append(" — ").Append(state).Append(" [")
					.Append(cue.Glyph ?? "sound").Append(';')
					.Append(cue.Tile ?? "text").Append(']');
				shown++;
			}
			if (works.Count > shown)
				text.Append("\n… ").Append(works.Count - shown).Append(" more readers; walk closer or audit a smaller gallery.");
			text.Append("\n\nCapture this receipt with the map screenshot and record a human pass/fail verdict.");
			Popup.Show(text.ToString());
		}

		private static int CompareGroundOrder(GameObject A, GameObject B)
		{
			Cell a = A?.CurrentCell;
			Cell b = B?.CurrentCell;
			if (a == null) return b == null ? string.Compare(A?.ID, B?.ID,
				StringComparison.Ordinal) : 1;
			if (b == null) return -1;
			int compare = a.Y.CompareTo(b.Y);
			if (compare != 0) return compare;
			compare = a.X.CompareTo(b.X);
			if (compare != 0) return compare;
			return string.Compare(A.ID, B.ID, StringComparison.Ordinal);
		}
	}
}
