using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomZoningRules
	{
		/// <summary>Knowledge requirements read back for prose: the kind prefix is dropped,
		/// because "solar condenser" is what the founder calls it either way.</summary>
		public static List<string> DescribeKeys(IEnumerable<string> Keys)
		{
			List<string> names = new List<string>();
			if (Keys == null)
			{
				return names;
			}
			foreach (string entry in Keys)
			{
				string name = NameOf(entry);
				if (name != null && !names.Contains(name))
				{
					names.Add(name);
				}
			}
			return names;
		}

		/// <summary>Every token in a comma list, trimmed and case-folded, blanks dropped.</summary>
		public static List<string> Tokens(string Source)
		{
			List<string> tokens = new List<string>();
			if (string.IsNullOrEmpty(Source))
			{
				return tokens;
			}
			string[] parts = Source.Split(ListSeparator);
			for (int i = 0; i < parts.Length; i++)
			{
				string token = Fold(parts[i]);
				if (token != null && !tokens.Contains(token))
				{
					tokens.Add(token);
				}
			}
			return tokens;
		}

		/// <summary>Joins prose with commas and a final "or". One item joins to itself.</summary>
		public static string JoinOr(IList<string> Items)
		{
			return Join(Items, "or");
		}

		/// <summary>Joins prose with commas and a final "and". One item joins to itself.</summary>
		public static string JoinAnd(IList<string> Items)
		{
			return Join(Items, "and");
		}

		private static string Join(IList<string> Items, string Conjunction)
		{
			if (Items == null || Items.Count == 0)
			{
				return null;
			}
			if (Items.Count == 1)
			{
				return Items[0];
			}
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			for (int i = 0; i < Items.Count; i++)
			{
				if (i > 0)
				{
					text.Append((i == Items.Count - 1) ? (" " + Conjunction + " ") : ", ");
				}
				text.Append(Items[i]);
			}
			return text.ToString();
		}

		/// <summary>A list rewritten as its own trimmed, folded, de-duplicated tokens. Null when
		/// nothing usable survived, which is how a list of nothing but commas is caught.</summary>
		private static string NormalizeList(string Source)
		{
			List<string> tokens = Tokens(Source);
			if (tokens.Count == 0)
			{
				return null;
			}
			return string.Join(ListSeparator.ToString(), tokens.ToArray());
		}

		private static bool ListContains(string Source, string Token)
		{
			return Tokens(Source).Contains(Token);
		}

		/// <summary>True when a list attribute actually restricts anything.</summary>
		private static bool Gated(string Source)
		{
			if (string.IsNullOrEmpty(Source))
			{
				return false;
			}
			List<string> tokens = Tokens(Source);
			return tokens.Count > 0 && !tokens.Contains(AnyToken);
		}

		// Every key, token, and category in this file is compared case-folded and trimmed, in one
		// place, so that "Craft", " craft ", and "craft" cannot ever be three different districts.
		private static string Fold(string Text)
		{
			if (string.IsNullOrEmpty(Text))
			{
				return null;
			}
			string folded = Text.Trim().ToLowerInvariant();
			return (folded.Length == 0) ? null : folded;
		}
	}
}
