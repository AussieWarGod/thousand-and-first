using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomCatalogueRules
	{
		/// <summary>Whether any finding in a list is a <see cref="CatalogueSeverity.Fault"/>.
		/// </summary>
		public static bool AnyFault(IEnumerable<CatalogueFinding> Findings)
		{
			if (Findings == null)
			{
				return false;
			}
			foreach (CatalogueFinding finding in Findings)
			{
				if (finding != null && finding.Severity == CatalogueSeverity.Fault)
				{
					return true;
				}
			}
			return false;
		}

		// The stage word is KingdomUpgradeRules' - one register for one idea, rather than a second
		// table here that could drift from the one the founder already reads in the ledger.
		private static string StageWord(GrowthStage Stage)
		{
			return "a " + KingdomUpgradeRules.StageWord(Stage);
		}

		// KingdomPlotRules.SizeName answers with an empty string for PlotSize.None, correctly: a
		// single-cell work has no tier to name. A finding still has to be a sentence, so it gets
		// one here rather than reading "stands on a  plot".
		private static string PlotWord(KingdomPlotRules.PlotSize Plot)
		{
			return (Plot == KingdomPlotRules.PlotSize.None) ? "no plot at all" : ("a " + KingdomPlotRules.SizeName(Plot) + " plot");
		}

		/// <summary>
		/// Reads one entry's <c>Styles</c> tag list into the union of styles the catalogue builds
		/// for.
		/// <para>
		/// A NEGATED tag (<c>Styles="all,!eater"</c>) names a style just as loudly as a welcome
		/// does &mdash; an author who refuses the eater city has referred to it, and reporting it
		/// as a style nothing builds for would be false. It is collected under its bare name, with
		/// the <c>!</c> stripped, so the undeclared-style check catches
		/// <c>Styles="all,!eatr"</c>, which is precisely the typo that is otherwise invisible: a
		/// mis-spelled refusal refuses nobody and the design silently goes everywhere.
		/// </para>
		/// </summary>
		/// <returns>True when this entry is offered to every style there is &mdash; which a list
		/// of pure refusals also is, because <c>KingdomZoningRules.TagAccepts</c> reads one as
		/// "everywhere except".</returns>
		private static bool CollectStyles(CatalogueEntry Entry, List<string> Into)
		{
			string styles = Entry.Styles;
			if (string.IsNullOrEmpty(styles))
			{
				return false;
			}
			bool takesAll = false;
			bool anyWelcome = false;
			string[] parts = styles.Split(ListSeparator);
			for (int i = 0; i < parts.Length; i++)
			{
				string part = Fold(parts[i]);
				if (part == null)
				{
					continue;
				}
				if (part[0] == KingdomZoningRules.NegationPrefix)
				{
					part = Fold(part.Substring(1));
					if (part != null && part != "all" && !Into.Contains(part))
					{
						Into.Add(part);
					}
					continue;
				}
				anyWelcome = true;
				if (part == "all")
				{
					takesAll = true;
					continue;
				}
				if (!Into.Contains(part))
				{
					Into.Add(part);
				}
			}
			return takesAll || !anyWelcome;
		}

		private static int Least(int A, int B, int C)
		{
			int least = (A < B) ? A : B;
			return (least < C) ? least : C;
		}

		private static bool Contains(string[] Set, string Value)
		{
			if (Value == null)
			{
				return false;
			}
			for (int i = 0; i < Set.Length; i++)
			{
				if (Set[i] == Value)
				{
					return true;
				}
			}
			return false;
		}

		private static List<string> Fold(List<string> Values)
		{
			List<string> folded = new List<string>();
			for (int i = 0; i < Values.Count; i++)
			{
				string value = Fold(Values[i]);
				if (value != null && !folded.Contains(value))
				{
					folded.Add(value);
				}
			}
			return folded;
		}

		/// <summary>Trims and lower-cases one token. Null for anything that was only space, so
		/// every caller has one thing to test rather than two.</summary>
		private static string Fold(string Value)
		{
			if (string.IsNullOrEmpty(Value))
			{
				return null;
			}
			string trimmed = Value.Trim().ToLowerInvariant();
			return (trimmed.Length == 0) ? null : trimmed;
		}
	}
}
