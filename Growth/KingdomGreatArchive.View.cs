using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomGreatArchive
	{
		private static string Draw(KingdomSystem System)
		{
			if (!TryFacts(System, out List<KingdomGreatArchiveCityFacts> cities,
				out List<KingdomGreatArchiveNodeFacts> nodes, out string failure))
				return failure ?? "The realm's keeper rolls cannot be reconciled.";
			if (!KingdomGreatArchiveRules.TryBuild(cities, nodes,
				out KingdomGreatArchiveMap map, out failure))
				return failure ?? "The realm's knowledge map cannot be reconciled.";
			StringBuilder text = new StringBuilder();
			text.Append("{{C|The Great Archive of ")
				.Append(KingdomPresentation.Rich(System.KingdomDisplayName)).Append("}}")
				.Append("\n{{K|A read-only concordance. Nothing here can be commanded.}}")
				.Append("\n\n{{W|Keeper rolls:}} ");
			for (int i = 0; i < map.CityNames.Count; i++)
			{
				if (i > 0) text.Append(i == map.CityNames.Count - 1 ? " and " : ", ");
				text.Append(KingdomPresentation.Rich(map.CityNames[i]));
			}
			string branch = null;
			for (int i = 0; i < map.Rows.Count; i++)
			{
				KingdomGreatArchiveRow row = map.Rows[i];
				if (row.Branch != branch)
				{
					branch = row.Branch;
					text.Append("\n\n{{W|").Append(KingdomPresentation.Rich(branch))
						.Append("}}:");
				}
				text.Append("\n  T").Append(row.Tier).Append(" ")
					.Append(KingdomPresentation.Rich(row.DisplayName));
				if (row.Held)
				{
					text.Append(" {{G|— held by ");
					AppendNames(text, row.HoldingCityNames); text.Append("}}");
				}
				else text.Append(" {{K|— heard of}} ");
				if (row.RequirementClauses.Count > 0)
				{
					text.Append("\n    {{K|from ");
					AppendNames(text, row.RequirementClauses); text.Append("}}");
				}
			}
			if (map.Rows.Count == 0)
				text.Append("\n\n{{K|No keeper roll yet names a discovered road.}} ");
			return text.ToString().TrimEnd();
		}

		private static void AppendNames(StringBuilder Text, List<string> Names)
		{
			for (int i = 0; i < Names.Count; i++)
			{
				if (i > 0) Text.Append(i == Names.Count - 1 ? " and " : ", ");
				Text.Append(KingdomPresentation.Rich(Names[i]));
			}
		}
	}
}
