using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRules
	{
		/// <summary>
		/// Resolves an in-place tier successor through the exact variant frozen by the standing
		/// architecture receipt. Later demographic or creed changes may shape new commissions,
		/// but cannot silently restyle already-paid stateful fabric.
		/// </summary>
		public static bool TrySelectFrozenSuccessorVariant(
			IList<ArchitectureVariantDraft> Variants, string FrozenVariantKey,
			out ArchitectureVariantDraft Variant, out string Failure)
		{
			Variant = null;
			if (!TryValidateVariants(Variants, out Failure)) return false;
			if (!ValidKey(FrozenVariantKey))
				return Fail("standing architecture has no bounded frozen variant identity",
					out Failure);
			for (int i = 0; i < Variants.Count; i++)
				if (Variants[i].Key == FrozenVariantKey)
				{
					Variant = Variants[i];
					Failure = null;
					return true;
				}
			return Fail("authored successor has no exact frozen variant " + FrozenVariantKey,
				out Failure);
		}

		private static bool ValidSelector(ArchitectureSelector Selector, out string Failure)
		{
			Failure = null;
			if (Selector == null) return true;
			if (!ValidTagExpression(Selector.Styles) || !ValidTagExpression(Selector.Creeds)
				|| !ValidTagExpression(Selector.Cultures) || !ValidTagExpression(Selector.Species)
				|| !ValidTagExpression(Selector.Genotypes) || !ValidTagExpression(Selector.Bodies)
				|| !ValidTagExpression(Selector.Terrains) || !ValidTagExpression(Selector.Strata))
				return Fail("selector tag expression is malformed", out Failure);
			if (Selector.MinimumStage < -1 || Selector.MaximumStage < -1
				|| Selector.MinimumTech < -1 || Selector.MaximumTech < -1
				|| (Selector.MinimumStage >= 0 && Selector.MaximumStage >= 0
					&& Selector.MinimumStage > Selector.MaximumStage)
				|| (Selector.MinimumTech >= 0 && Selector.MaximumTech >= 0
					&& Selector.MinimumTech > Selector.MaximumTech))
				return Fail("selector numeric range is malformed", out Failure);
			return true;
		}

		private static bool SelectorMatches(ArchitectureSelector Selector,
			ArchitectureSelectionContext Context)
		{
			if (Selector == null) return true;
			return TagAccepts(Selector.Styles, Context.Style)
				&& TagAccepts(Selector.Creeds, Context.Creed)
				&& TagSetAccepts(Selector.Cultures, Context.Cultures)
				&& TagSetAccepts(Selector.Species, Context.Species)
				&& TagSetAccepts(Selector.Genotypes, Context.Genotypes)
				&& TagSetAccepts(Selector.Bodies, Context.Bodies)
				&& TagAccepts(Selector.Terrains, Context.Terrain)
				&& TagAccepts(Selector.Strata, Context.Stratum)
				&& (Selector.MinimumStage < 0 || Context.Stage >= Selector.MinimumStage)
				&& (Selector.MaximumStage < 0 || Context.Stage <= Selector.MaximumStage)
				&& (Selector.MinimumTech < 0 || Context.Tech >= Selector.MinimumTech)
				&& (Selector.MaximumTech < 0 || Context.Tech <= Selector.MaximumTech);
		}

		private static int SelectorSpecificity(ArchitectureSelector Selector)
		{
			if (Selector == null) return 0;
			int result = 0;
			if (ConditionalExpression(Selector.Styles)) result++;
			if (ConditionalExpression(Selector.Creeds)) result++;
			if (ConditionalExpression(Selector.Cultures)) result++;
			if (ConditionalExpression(Selector.Species)) result++;
			if (ConditionalExpression(Selector.Genotypes)) result++;
			if (ConditionalExpression(Selector.Bodies)) result++;
			if (ConditionalExpression(Selector.Terrains)) result++;
			if (ConditionalExpression(Selector.Strata)) result++;
			if (Selector.MinimumStage >= 0) result++;
			if (Selector.MaximumStage >= 0) result++;
			if (Selector.MinimumTech >= 0) result++;
			if (Selector.MaximumTech >= 0) result++;
			return result;
		}

		private static bool Unconditional(ArchitectureSelector Selector)
		{
			return Selector == null || (!ConditionalExpression(Selector.Styles)
				&& !ConditionalExpression(Selector.Creeds)
				&& !ConditionalExpression(Selector.Cultures)
				&& !ConditionalExpression(Selector.Species)
				&& !ConditionalExpression(Selector.Genotypes)
				&& !ConditionalExpression(Selector.Bodies)
				&& !ConditionalExpression(Selector.Terrains)
				&& !ConditionalExpression(Selector.Strata)
				&& Selector.MinimumStage < 0 && Selector.MaximumStage < 0
				&& Selector.MinimumTech < 0 && Selector.MaximumTech < 0);
		}

		private static bool ConditionalExpression(string Expression)
		{
			return !string.IsNullOrWhiteSpace(Expression)
				&& !string.Equals(Expression.Trim(), "all", StringComparison.OrdinalIgnoreCase);
		}

		private static bool TagAccepts(string Expression, string Value)
		{
			if (string.IsNullOrWhiteSpace(Expression)) return true;
			string value = (Value ?? "").Trim();
			bool hasPositive = false;
			bool positiveMatch = false;
			string[] tokens = Expression.Split(',');
			for (int i = 0; i < tokens.Length; i++)
			{
				string token = tokens[i].Trim();
				bool negative = token.Length > 1 && token[0] == '!';
				string name = negative ? token.Substring(1) : token;
				if (negative)
				{
					if (string.Equals(name, value, StringComparison.OrdinalIgnoreCase)) return false;
				}
				else
				{
					hasPositive = true;
					if (name == "*" || string.Equals(name, "all", StringComparison.OrdinalIgnoreCase)
						|| string.Equals(name, value, StringComparison.OrdinalIgnoreCase)) positiveMatch = true;
				}
			}
			return !hasPositive || positiveMatch;
		}

		/// <summary>Set-valued identity selector. Any explicitly excluded live fact refuses the
		/// variant; otherwise one positive fact must match when positives are named. A pure exclusion
		/// matches a city carrying none of its refused facts. Empty/all preserve existing wildcard
		/// semantics. Caller supplies canonical bounded facts, but comparison remains case-insensitive
		/// because Qud identity vocabularies preserve display case.</summary>
		private static bool TagSetAccepts(string Expression, IList<string> Values)
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
				if (negative)
				{
					for (int j = 0; Values != null && j < Values.Count; j++)
						if (string.Equals(name, Values[j], StringComparison.OrdinalIgnoreCase))
							return false;
				}
				else
				{
					hasPositive = true;
					if (name == "*" || string.Equals(name, "all", StringComparison.OrdinalIgnoreCase))
					{
						positiveMatch = true;
						continue;
					}
					for (int j = 0; Values != null && j < Values.Count; j++)
						if (string.Equals(name, Values[j], StringComparison.OrdinalIgnoreCase))
						{
							positiveMatch = true;
							break;
						}
				}
			}
			return !hasPositive || positiveMatch;
		}

		private static bool ValidTagExpression(string Expression)
		{
			if (string.IsNullOrEmpty(Expression)) return true;
			if (Expression.Length > MaxSelectorChars) return false;
			string[] tokens = Expression.Split(',');
			if (tokens.Length > MaxSelectorTokens) return false;
			for (int i = 0; i < tokens.Length; i++)
			{
				string token = tokens[i].Trim();
				if (token.Length == 0 || token.Length > 64 || token == "!" || HasControl(token)) return false;
			}
			return true;
		}

	}
}
