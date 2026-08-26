using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomLodgingRules
	{
		// --- Condemnation: when a house stops being a roof ---------------------------------

		/// <summary>
		/// Wear at which a home stops counting as a roof for anybody. Derived, not chosen:
		/// <c>KingdomMaterialRules.ConditionPercent</c> says a work at wear W has <c>100 - W</c>
		/// of itself left, and <c>KingdomRules.RuinStandingCeilingPercent</c> is the MOST of an
		/// abandoned settlement that is ever still standing after a generation of nobody. So the
		/// line is the wear at which a house somebody lives in has no more of itself left than
		/// the best-preserved ruin &mdash; 40, which is also, and not by accident, exactly where
		/// <c>KingdomMaterialRules.ConditionWord</c> starts calling a work half-wrecked.
		/// <para>
		/// Strictly below <c>KingdomMaterialRules.MaxWearPercent</c>, so condemnation is a state
		/// a house can be in rather than a synonym for the wear ceiling: a home can be badly used
		/// and still keep the rain off, and every point of the damage is mendable, so a
		/// condemnation is arrested by putting the roof back on and never by waiting.
		/// </para>
		/// <para>
		/// This is a LODGING rule and not a wear rule on purpose. Nothing about a condemned house
		/// stops it working as whatever else it is; what it stops doing is housing people. The
		/// protection law is untouched &mdash; nothing is cleared, nothing is destroyed, and the
		/// building stands exactly where it stood.
		/// </para>
		/// </summary>
		public const int CondemnedWearPercent = 100 - KingdomRules.RuinStandingCeilingPercent;

		/// <summary>Whether a home this worn has stopped being a roof. At the threshold exactly,
		/// it has: the constant names the first wear that is too much, not the last that is
		/// tolerable.</summary>
		public static bool IsCondemned(int Wear)
		{
			return Wear >= CondemnedWearPercent;
		}

		/// <summary>
		/// The named, once-announced line STANDARDS 7b requires for an applicable-but-blocked
		/// state: never a complaint, never a countdown, just what is true and why. Repeats the
		/// resident's own name rather than guessing a pronoun &mdash; the roster carries no
		/// gender, and "Vashti will not live beside Vashti" reads honest where a wrong pronoun
		/// would not.
		/// </summary>
		/// <param name="ResidentName">The roll's own name for them.</param>
		/// <param name="Reason">From <see cref="Diagnose"/>.</param>
		public static string UnhousedLine(string ResidentName, UnhousedReason Reason)
		{
			string name = string.IsNullOrEmpty(ResidentName) ? "a settler" : ResidentName;
			switch (Reason)
			{
			case UnhousedReason.NoRoofAtAll:
				return name + " sleeps in the open: there is no roof standing yet.";
			case UnhousedReason.NeedsUnmet:
				return name + " sleeps in the open: nothing built here answers what " + name + " needs.";
			case UnhousedReason.Full:
				return name + " sleeps in the open: every home that would take " + name + " is full.";
			case UnhousedReason.Refused:
				return name + " sleeps in the open: every home that would take " + name + " already holds someone " + name + " will not live beside.";
			case UnhousedReason.Condemned:
				return name + " sleeps in the open: every roof here has fallen in past living under. Mend one and " + name + " has a home again.";
			default:
				return name + " sleeps in the open.";
			}
		}

		/// <summary>
		/// The same line with the quarters named (Addendum 4c). Only a refusal reads differently:
		/// the founder is told the roomiest quarters that still would not take this person, which
		/// is the fact they can act on &mdash; whatever they build next has to beat it. Every other
		/// reason is word-for-word <see cref="UnhousedLine(string, UnhousedReason)"/>, because
		/// naming the quarters says nothing at all about a settlement with no roof standing.
		/// </summary>
		/// <param name="ResidentName">The roll's own name for them.</param>
		/// <param name="Reason">From <see cref="Diagnose"/>.</param>
		/// <param name="Quarters">The roomiest rung among the homes that had room and refused,
		/// accumulated with <see cref="Roomier"/>.</param>
		public static string UnhousedLine(string ResidentName, UnhousedReason Reason, Closeness Quarters)
		{
			string line = UnhousedLine(ResidentName, Reason);
			if (Reason != UnhousedReason.Refused)
			{
				return line;
			}
			return line + " The roomiest of them is " + QuartersPhrase(Quarters) + ".";
		}

		/// <summary>Of a resident's <c>Needs</c>, the first one their new home's <c>Provides</c>
		/// also names &mdash; the tag <see cref="HomeSuffix"/> colours the line with. Null when
		/// nothing matched, which is the ordinary case for the unauthored base catalogue.
		/// </summary>
		public static string MatchedTag(IReadOnlyList<string> Needs, IReadOnlyList<string> Provides)
		{
			if (Needs == null || Provides == null)
			{
				return null;
			}
			for (int i = 0; i < Needs.Count; i++)
			{
				for (int j = 0; j < Provides.Count; j++)
				{
					if (string.Equals(Needs[i], Provides[j], StringComparison.OrdinalIgnoreCase))
					{
						return Provides[j];
					}
				}
			}
			return null;
		}

		/// <summary>
		/// A small, hand-written table from a namespaced <c>Provides</c> tag to the clause that
		/// names it in prose ("the chrome pilgrim sleeps by the charging post"). Illustrative
		/// rather than exhaustive on purpose: this file owns cohabitation, not the vocabulary's
		/// full derivation, so an unrecognised or absent tag falls back to a plain, honest line
		/// rather than a guess.
		/// </summary>
		private static string FlavorFor(string Tag)
		{
			if (string.IsNullOrEmpty(Tag))
			{
				return null;
			}
			switch (Tag.ToLowerInvariant())
			{
			case "charge":
			case KingdomQolRules.TagCharge:
				return "by the charging post";
			case "water":
			case KingdomQolRules.TagOpenWater:
				return "by the water";
			case "sky":
			case KingdomQolRules.TagSky:
				return "under open sky";
			case "damp":
			case "dark":
			case KingdomQolRules.TagDamp:
			case KingdomQolRules.TagDark:
				return "in the damp dark";
			case "shade":
				return "in the shade";
			case KingdomQolRules.TagQuiet:
				return "in the quiet";
			default:
				return null;
			}
		}

		/// <summary>The line the roll of settlers and the chronicle both read a housed resident's
		/// home as: the building's own name, coloured by the matched tag when the derivation gave
		/// one, plain otherwise.</summary>
		public static string HomeSuffix(string BuildingName, string MatchedProvidesTag)
		{
			string flavor = FlavorFor(MatchedProvidesTag);
			string place = string.IsNullOrEmpty(BuildingName) ? "sleeps under a roof" : ("sleeps in the " + BuildingName);
			return string.IsNullOrEmpty(flavor) ? place : (place + ", " + flavor);
		}

	}
}
