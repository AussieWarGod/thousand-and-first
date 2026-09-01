using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomStyleRules
	{
		/// <summary>Frozen pre-v1 save-key migration. This is deliberately narrower than alias
		/// lookup: only a key this mod once wrote is rewritten in durable state.</summary>
		public static string MigrateLegacyKey(string Style)
		{
			return string.Equals(Style, "gyre", StringComparison.OrdinalIgnoreCase)
				? "moonstair" : Style;
		}

		/// <summary>All accepted keys for one style, canonical first. Unknown values remain a
		/// one-key open extension rather than collapsing to common.</summary>
		public static IList<string> KeysFor(IList<KingdomStyleDefinition> Definitions, string Name)
		{
			List<string> result = new List<string>();
			KingdomStyleDefinition definition = Find(Definitions, Name);
			if (definition == null)
			{
				if (!string.IsNullOrWhiteSpace(Name)) result.Add(Name.Trim());
				return result.AsReadOnly();
			}
			result.Add(definition.Name);
			for (int i = 0; definition.Aliases != null && i < definition.Aliases.Length; i++)
				result.Add(definition.Aliases[i]);
			return result.AsReadOnly();
		}

		/// <summary>Alias-aware form of the catalogue's all/list/negation tag grammar.</summary>
		public static bool TagAccepts(IList<KingdomStyleDefinition> Definitions,
			string Expression, string Style)
		{
			return TagAccepts(Expression, KeysFor(Definitions, Style));
		}

		/// <summary>Compatibility form for callers that only know the stored key. It recognizes
		/// the one frozen built-in rename and otherwise preserves the open raw key.</summary>
		public static bool TagAccepts(string Expression, string Style)
		{
			List<string> keys = new List<string>();
			if (!string.IsNullOrWhiteSpace(Style))
			{
				string raw = Style.Trim();
				string canonical = MigrateLegacyKey(raw);
				keys.Add(canonical);
				if (string.Equals(canonical, "moonstair", StringComparison.OrdinalIgnoreCase))
					keys.Add("gyre");
			}
			return TagAccepts(Expression, keys);
		}

		/// <summary>Tag match against canonical-first style keys supplied by the merged registry.</summary>
		public static bool TagAccepts(string Expression, IList<string> StyleKeys)
		{
			if (string.IsNullOrWhiteSpace(Expression)) return true;
			bool hasPositive = false;
			bool positiveMatch = false;
			string[] tokens = Expression.Split(',');
			for (int i = 0; i < tokens.Length; i++)
			{
				string token = tokens[i].Trim();
				bool negative = token.Length > 1 && token[0] == '!';
				string name = negative ? token.Substring(1) : token;
				bool match = NameIn(StyleKeys, name);
				if (negative)
				{
					if (match) return false;
				}
				else
				{
					hasPositive = true;
					if (name == "*" || string.Equals(name, "all",
						StringComparison.OrdinalIgnoreCase) || match) positiveMatch = true;
				}
			}
			return !hasPositive || positiveMatch;
		}

		private static bool NameIn(IList<string> Names, string Wanted)
		{
			for (int i = 0; Names != null && i < Names.Count; i++)
				if (string.Equals(Names[i], Wanted, StringComparison.OrdinalIgnoreCase)) return true;
			return false;
		}
	}
}
