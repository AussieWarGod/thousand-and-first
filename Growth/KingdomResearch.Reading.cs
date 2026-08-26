using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomResearch
	{
		// ==================================================================================
		// What the map reads
		// ==================================================================================

		/// <summary>
		/// The nodes the founder has heard of and this city does not hold, as rows the map can
		/// draw: the name, how far off in things rather than in numbers, and what is in the way.
		/// Hidden and forbidden nodes are absent, and absent identically.
		/// </summary>
		public static List<ResearchRow> HeardOf(KingdomSystem System)
		{
			List<ResearchRow> rows = new List<ResearchRow>();
			if (!Enabled || System == null || !System.Founded)
			{
				return rows;
			}
			EnsureLoaded();
			List<string> roster = KingdomZoning.Roster(System);
			TechLevel tech = KingdomZoning.Tech(System);
			int mind = BestMind(System);
			for (int i = 0; i < _nodes.Count; i++)
			{
				ResearchNode node = _nodes[i];
				if (node.Key == System.ResearchSubject || Holds(roster, node) || !Discovered(node.Key) || !Admissible(System, node))
				{
					continue;
				}
				List<string> missing = KingdomZoningRules.DescribeKeys(KingdomZoningRules.MissingKnowledge(roster, node.Requires));
				bool tierShort = !KingdomResearchRules.TierReached(mind, node.Tier);
				bool techShort = node.MinTech > tech;
				rows.Add(new ResearchRow(node.Key, node.Named,
					KingdomResearchRules.Distance(tierShort, techShort, missing.Count),
					Peek(System, node.Key) > 0,
					KingdomTechMapRules.MissingForNode(missing,
						tierShort ? KingdomResearchRules.IntelligenceForTier(node.Tier) : 0, mind,
						techShort ? KingdomZoningRules.TechName(node.MinTech) : null,
						KingdomZoningRules.TechName(tech))));
			}
			KingdomTechMapRules.SortResearch(rows);
			return rows;
		}

		/// <summary>The subject the seated city is working out, as the map's second chapter reads
		/// it, or null when the bench has taken nothing up.</summary>
		public static string Working(KingdomSystem System)
		{
			ResearchNode node;
			if (!Enabled || System == null || !System.Founded || !TryGetNode(System.ResearchSubject, out node))
			{
				return null;
			}
			List<string> shelved = new List<string>();
			if (System.ResearchShelf != null)
			{
				foreach (KeyValuePair<string, int> row in System.ResearchShelf)
				{
					ResearchNode other;
					shelved.Add(TryGetNode(row.Key, out other) ? other.Named : row.Key);
				}
				shelved.Sort(StringComparer.Ordinal);
			}
			return KingdomTechMapRules.WorkingChapter(node.Named,
				KingdomResearchRules.Reach(0, System.ResearchAccrued > 0), shelved);
		}

		/// <summary>Whether this city's keepers write anything down at all &mdash; the one node the
		/// keepers' map itself waits on (&sect;4.1 T1). With research switched off, they always do,
		/// so the map is exactly what it was before nodes existed.</summary>
		public static bool KeepsNotes(KingdomSystem System)
		{
			if (!Enabled)
			{
				return true;
			}
			EnsureLoaded();
			return _nodes.Count == 0 || KingdomZoningRules.Knows(KingdomZoning.Roster(System), KingdomResearchRules.NotesKey);
		}
	}
}
