using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Pure validation and union for the capital's visualization-only research map.</summary>
	public static class KingdomGreatArchiveRules
	{
		public const int MaxNodes = 256;
		public const int MaxKeyChars = 128;
		public const int MaxNameChars = 256;
		public const int MaxDisplayChars = 65536;
		public const int MaxTier = 4;

		public static bool TryBuild(IList<KingdomGreatArchiveCityFacts> Cities,
			IList<KingdomGreatArchiveNodeFacts> Nodes, out KingdomGreatArchiveMap Map,
			out string Failure)
		{
			Map = null; Failure = null;
			if (Cities == null || Cities.Count < 1
				|| Cities.Count > KingdomSettlementTopologyRules.MaxOwnedSettlements)
				return Fail("the archive city set is outside the realm bound", out Failure);
			if (Nodes == null || Nodes.Count > MaxNodes)
				return Fail("the archive research registry exceeds its bound", out Failure);

			Dictionary<string, KingdomGreatArchiveNodeFacts> byKey =
				new Dictionary<string, KingdomGreatArchiveNodeFacts>(StringComparer.Ordinal);
			for (int i = 0; i < Nodes.Count; i++)
			{
				KingdomGreatArchiveNodeFacts node = Nodes[i];
				if (!ValidNode(node) || byKey.ContainsKey(node.Key))
					return Fail("the archive research registry is malformed or duplicated", out Failure);
				byKey.Add(node.Key, node);
			}
			foreach (KingdomGreatArchiveNodeFacts node in byKey.Values)
				for (int i = 0; i < node.Requirements.Count; i++)
					for (int j = 0; j < node.Requirements[i].Alternatives.Count; j++)
					{
						string key = node.Requirements[i].Alternatives[j].NodeKey;
						if (!string.IsNullOrEmpty(key) && !byKey.ContainsKey(key))
							return Fail("an archive dependency names no research node", out Failure);
					}

			HashSet<string> cityIds = new HashSet<string>(StringComparer.Ordinal);
			Dictionary<string, List<string>> holders =
				new Dictionary<string, List<string>>(StringComparer.Ordinal);
			KingdomGreatArchiveMap result = new KingdomGreatArchiveMap();
			for (int i = 0; i < Cities.Count; i++)
			{
				KingdomGreatArchiveCityFacts city = Cities[i];
				if (!Valid(city?.SettlementId, MaxKeyChars) || !Valid(city?.DisplayName, MaxNameChars)
					|| !cityIds.Add(city.SettlementId) || city.HeldNodeKeys == null
					|| city.HeldNodeKeys.Count > MaxNodes)
					return Fail("the archive city set is malformed or duplicated", out Failure);
				result.CityNames.Add(city.DisplayName);
				HashSet<string> local = new HashSet<string>(StringComparer.Ordinal);
				for (int j = 0; j < city.HeldNodeKeys.Count; j++)
				{
					string key = city.HeldNodeKeys[j];
					if (!local.Add(key) || !byKey.ContainsKey(key))
						return Fail("a city archive row names an unknown or duplicate node", out Failure);
					if (!holders.TryGetValue(key, out List<string> names))
					{
						names = new List<string>(); holders.Add(key, names);
					}
					names.Add(city.DisplayName);
				}
			}

			for (int i = 0; i < Nodes.Count; i++)
			{
				KingdomGreatArchiveNodeFacts node = Nodes[i];
				holders.TryGetValue(node.Key, out List<string> names);
				if (!node.Discovered && names == null) continue;
				KingdomGreatArchiveRow row = new KingdomGreatArchiveRow {
					Key = node.Key, DisplayName = node.DisplayName, Branch = node.Branch,
					Tier = node.Tier,
					HoldingCityNames = names == null ? new List<string>() : new List<string>(names)
				};
				for (int j = 0; j < node.Requirements.Count; j++)
				{
					List<string> visible = new List<string>();
					List<KingdomGreatArchiveAlternativeFacts> alternatives =
						node.Requirements[j].Alternatives;
					for (int k = 0; k < alternatives.Count; k++)
					{
						KingdomGreatArchiveAlternativeFacts alternative = alternatives[k];
						if (string.IsNullOrEmpty(alternative.NodeKey))
							visible.Add(alternative.DisplayName);
						else
						{
							KingdomGreatArchiveNodeFacts dependency = byKey[alternative.NodeKey];
							if (dependency.Discovered || holders.ContainsKey(alternative.NodeKey))
								visible.Add(dependency.DisplayName);
						}
					}
					if (visible.Count > 0)
						row.RequirementClauses.Add(string.Join(" or ", visible.ToArray()));
				}
				row.HoldingCityNames.Sort(StringComparer.Ordinal);
				result.Rows.Add(row);
			}
			result.CityNames.Sort(StringComparer.Ordinal);
			result.Rows.Sort(CompareRows);
			if (DisplayWeight(result) > MaxDisplayChars)
				return Fail("the archive map exceeds its complete display bound", out Failure);
			Map = result; return true;
		}

		private static int DisplayWeight(KingdomGreatArchiveMap Map)
		{
			long total = 128;
			for (int i = 0; i < Map.CityNames.Count; i++) total += Map.CityNames[i].Length + 8;
			for (int i = 0; i < Map.Rows.Count; i++)
			{
				KingdomGreatArchiveRow row = Map.Rows[i];
				total += row.DisplayName.Length + row.Branch.Length + 32;
				for (int j = 0; j < row.HoldingCityNames.Count; j++)
					total += row.HoldingCityNames[j].Length + 8;
				for (int j = 0; j < row.RequirementClauses.Count; j++)
					total += row.RequirementClauses[j].Length + 8;
				if (total > MaxDisplayChars) return MaxDisplayChars + 1;
			}
			return (int)total;
		}

		private static bool ValidNode(KingdomGreatArchiveNodeFacts Node)
		{
			if (Node == null || !Valid(Node.Key, MaxKeyChars)
				|| !Valid(Node.DisplayName, MaxNameChars) || !Valid(Node.Branch, MaxNameChars)
				|| Node.Tier < 1 || Node.Tier > MaxTier
				|| Node.Requirements == null || Node.Requirements.Count > MaxNodes) return false;
			int alternatives = 0;
			for (int i = 0; i < Node.Requirements.Count; i++)
			{
				KingdomGreatArchiveRequirementFacts group = Node.Requirements[i];
				if (group?.Alternatives == null || group.Alternatives.Count < 1
					|| group.Alternatives.Count > MaxNodes) return false;
				HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
				for (int j = 0; j < group.Alternatives.Count; j++)
				{
					KingdomGreatArchiveAlternativeFacts alternative = group.Alternatives[j];
					if (alternative == null || !Valid(alternative.DisplayName, MaxNameChars)
						|| (!string.IsNullOrEmpty(alternative.NodeKey)
							&& !Valid(alternative.NodeKey, MaxKeyChars))) return false;
					string identity = (alternative.NodeKey ?? "") + "\0" + alternative.DisplayName;
					if (!seen.Add(identity) || ++alternatives > MaxNodes) return false;
				}
			}
			return true;
		}

		private static int CompareRows(KingdomGreatArchiveRow A, KingdomGreatArchiveRow B)
		{
			int compare = string.Compare(A.Branch, B.Branch, StringComparison.Ordinal);
			if (compare != 0) return compare;
			compare = A.Tier.CompareTo(B.Tier);
			return compare != 0 ? compare : string.Compare(A.Key, B.Key, StringComparison.Ordinal);
		}

		private static bool Valid(string Value, int Max)
		{
			return !string.IsNullOrWhiteSpace(Value) && Value.Length <= Max
				&& Value.IndexOf('\0') < 0 && Value.IndexOf('\n') < 0 && Value.IndexOf('\r') < 0;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
