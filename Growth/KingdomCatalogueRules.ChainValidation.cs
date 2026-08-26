using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomCatalogueRules
	{
		private static void ValidateChain(CatalogueEntry Entry, Dictionary<string, CatalogueEntry> ByKey, List<CatalogueFinding> Findings)
		{
			if (string.IsNullOrEmpty(Entry.SuccessorKey))
			{
				return;
			}
			if (!ByKey.TryGetValue(Entry.SuccessorKey, out var successor))
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "UpgradesTo", CatalogueSeverity.Fault,
					"building " + Entry.Key + " improves into " + Entry.SuccessorKey + ", which no building declares" + Layered(Entry)));
				return;
			}
			// Upgrades climb within a plot; sizes compete across plots. A design that improved into
			// a larger one would be an in-place metamorphosis onto ground the settlement never
			// cleared, and would quietly make the whole size-versus-stacking decision free.
			if (successor.Plot != Entry.Plot && !IsHeartGrowth(Entry, successor))
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "UpgradesTo", CatalogueSeverity.Fault,
					"building " + Entry.Key + " stands on " + PlotWord(Entry.Plot) + " and improves into " + successor.Key + ", which wants " + PlotWord(successor.Plot) + "; an improvement climbs within its own plot" + Layered(Entry) + Layered(successor)));
			}
			// Footprints climb within the plot. A successor that stands on LESS ground is not wrong,
			// but it hands back walled ground as yard, which is worth an author seeing.
			if (successor.FootprintWidth > 0 && Entry.FootprintWidth > 0
				&& successor.FootprintWidth * successor.FootprintHeight < Entry.FootprintWidth * Entry.FootprintHeight)
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "Footprint", CatalogueSeverity.Note,
					"building " + Entry.Key + " improves into " + successor.Key + ", which stands on less ground than it does" + Layered(Entry) + Layered(successor)));
			}
			GrowthStage from = EffectiveMinStage(Entry.MinStage, Entry.Plot);
			GrowthStage to = EffectiveMinStage(successor.MinStage, successor.Plot);
			if (to < from)
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "UpgradesTo", CatalogueSeverity.Fault,
					"building " + Entry.Key + " improves into " + successor.Key + ", which the settlement could have raised earlier"));
			}
			if (successor.CostDrams < Entry.CostDrams)
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "UpgradesTo", CatalogueSeverity.Note,
					"building " + Entry.Key + " improves into " + successor.Key + ", which costs less water to raise from nothing"));
			}
			if (Fold(successor.Category) != Fold(Entry.Category))
			{
				Findings.Add(new CatalogueFinding(Entry.Key, "UpgradesTo", CatalogueSeverity.Note,
					"building " + Entry.Key + " improves into " + successor.Key + ", which is filed under a different purpose"));
			}
			// TryParseUpgradeAttributes already refuses a design that improves into itself; only a
			// pass over the whole catalogue can see a longer ring, and a ring spends the
			// settlement's entire surplus on going round it forever.
			List<string> walked = new List<string> { Entry.Key };
			string at = Entry.SuccessorKey;
			while (at != null && !walked.Contains(at) && ByKey.TryGetValue(at, out var next))
			{
				walked.Add(at);
				at = next.SuccessorKey;
			}
			if (at != null && walked.Contains(at))
			{
				// Post-merge, a ring is a thing no single file need contain: one mod may name the
				// first link and another the last, each correct alone. The finding therefore says
				// which of the links are themselves layered, which is the only clue an author has
				// that the file to fix may not be the one they wrote.
				Findings.Add(new CatalogueFinding(Entry.Key, "UpgradesTo", CatalogueSeverity.Fault,
					"the improvement chain from " + Entry.Key + " comes back to " + at + RingLayers(walked, ByKey)));
			}
		}

		/// <summary>The heart is the one authored chain whose surveyed plot grows one ring at a
		/// time. Both links must be adjacent named rungs and their declared sizes must match the
		/// rung table; merely borrowing a heart key cannot waive the ordinary same-plot law.</summary>
		private static bool IsHeartGrowth(CatalogueEntry Entry, CatalogueEntry Successor)
		{
			if (Entry == null || Successor == null)
			{
				return false;
			}
			int from = KingdomPlotRules.HeartRungOf(Entry.Key);
			int to = KingdomPlotRules.HeartRungOf(Successor.Key);
			return from > 0 && to == from + 1
				&& Entry.SuccessorKey == Successor.Key
				&& Entry.Plot == KingdomPlotRules.HeartSizeForRung(from)
				&& Successor.Plot == KingdomPlotRules.HeartSizeForRung(to);
		}

		/// <summary>
		/// The clause that names a design as the merge of several files, or an empty string for a
		/// design only one file declares. Appended to a fault so an author reading the log knows to
		/// look past their own file (STANDARDS 7b: a thing that will not work says why, once, where
		/// somebody can act on it).
		/// </summary>
		private static string Layered(CatalogueEntry Entry)
		{
			if (Entry == null || Entry.Declarations <= 1)
			{
				return "";
			}
			string origin = string.IsNullOrEmpty(Entry.Origin) ? "" : (", last from " + Entry.Origin);
			return " (" + Entry.Key + " is the merge of " + Entry.Declarations + " declarations" + origin + ")";
		}

		/// <summary>Which links of a ring are themselves merged from more than one file.</summary>
		private static string RingLayers(List<string> Walked, Dictionary<string, CatalogueEntry> ByKey)
		{
			string list = "";
			for (int i = 0; i < Walked.Count; i++)
			{
				CatalogueEntry entry;
				if (ByKey.TryGetValue(Walked[i], out entry) && entry != null && entry.Declarations > 1)
				{
					list += ((list.Length == 0) ? "" : ", ") + Walked[i] + " from " + entry.Declarations + " files";
				}
			}
			return (list.Length == 0) ? "" : " (the ring closes across layered designs: " + list + ")";
		}

	}
}
