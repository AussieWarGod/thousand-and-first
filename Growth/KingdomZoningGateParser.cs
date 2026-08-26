using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomZoningRules
	{
		/// <summary>
		/// Parses the four optional gate attributes off one <c>&lt;building&gt;</c> entry.
		/// <para>
		/// A malformed attribute is dropped and named in <paramref name="Error"/>; it never fails
		/// the entry. That asymmetry with <c>KingdomRules.TryParseBuildAttributes</c> is
		/// deliberate: <c>Cost</c> and <c>Ticks</c> are the design, so a bad one means there is
		/// no design, but a gate is a restriction ON a design, and a typo in one should never
		/// delete a building from the catalog. Failing open is also the safer direction &mdash;
		/// the worst case is a design that could have been harder to reach, not one that becomes
		/// permanently unreachable with no way for the founder to find out why.
		/// </para>
		/// </summary>
		/// <param name="Key">Building key, for the error text.</param>
		/// <param name="Districts">The <c>Districts</c> attribute, or null.</param>
		/// <param name="MinZones">The <c>MinZones</c> attribute, or null.</param>
		/// <param name="Knowledge">The <c>Knowledge</c> attribute, or null.</param>
		/// <param name="MinTech">The <c>MinTech</c> attribute (a level name or its number), or null.</param>
		/// <param name="Error">Null when every attribute parsed, else one sentence naming each
		/// attribute that was dropped. Callers log this; nothing else depends on its wording.</param>
		/// <returns>The gate. Never invalid; every dropped attribute reads as absent.</returns>
		public static ZoneGate ParseGateAttributes(string Key, string Districts, string MinZones, string Knowledge, string MinTech, out string Error)
		{
			return ParseGateAttributes(Key, Districts, MinZones, Knowledge, MinTech, null, null, null, out Error);
		}

		/// <summary>
		/// The same parse with Addendum 16's three creed attributes folded in. Kept as a second
		/// overload rather than a widened signature because the first one is published and a third
		/// party may already be calling it (STANDARDS &sect;9); the four-gate overload is exactly
		/// this one with three nulls.
		/// </summary>
		/// <param name="Builders">The <c>Builders</c> attribute, or null.</param>
		/// <param name="Creed">The <c>Creed</c> attribute, or null.</param>
		/// <param name="CreedShare">The <c>CreedShare</c> attribute (a whole percent), or null.</param>
		public static ZoneGate ParseGateAttributes(string Key, string Districts, string MinZones, string Knowledge, string MinTech,
			string Builders, string Creed, string CreedShare, out string Error)
		{
			return ParseGateAttributes(Key, Districts, MinZones, Knowledge, MinTech, Builders, Creed, CreedShare, null, out Error);
		}

		/// <summary>
		/// The same parse with Addendum 15's <c>Strata</c> folded in. A third overload for the
		/// reason there was a second: the shape below it is published and a third party may already
		/// be calling it (STANDARDS &sect;9), so it is this one with a null.
		/// </summary>
		/// <param name="Strata">The <c>Strata</c> attribute, or null. A list of nothing but
		/// <see cref="AnyToken"/> is dropped to null, because a design that stands in every stratum
		/// is a design that declares none &mdash; but <c>all,!deep</c> is KEPT, since a list that
		/// refuses something is restricting something.</param>
		public static ZoneGate ParseGateAttributes(string Key, string Districts, string MinZones, string Knowledge, string MinTech,
			string Builders, string Creed, string CreedShare, string Strata, out string Error)
		{
			return ParseGateAttributes(Key, Districts, MinZones, Knowledge, MinTech, Builders, Creed, CreedShare,
				Strata, null, out Error);
		}

		/// <summary>
		/// The same parse with Addendum 22 A1's <c>Megastructure</c>. Optional like the eight before
		/// it: a design that does not claim to be one of the great works is ordinary, which is what
		/// every entry in the catalogue was the day before this landed.
		/// </summary>
		/// <param name="Megastructure">Raw <c>Megastructure</c> attribute.</param>
		public static ZoneGate ParseGateAttributes(string Key, string Districts, string MinZones, string Knowledge, string MinTech,
			string Builders, string Creed, string CreedShare, string Strata, string Megastructure, out string Error)
		{
			return ParseGateAttributes(Key, Districts, MinZones, Knowledge, MinTech, Builders, Creed, CreedShare,
				Strata, Megastructure, null, out Error);
		}

		/// <summary>
		/// The same parse with the capital ruling's <c>Capital</c>. A fifth overload for the reason
		/// there was a fourth: the shape above is published and a third party may already be
		/// calling it (STANDARDS &sect;9), so it is this one with a null.
		/// </summary>
		/// <param name="Capital">Raw <c>Capital</c> attribute. Optional like the ten before it: a
		/// design that does not claim the capital stands in any city, which is what every entry in
		/// the catalogue did the day before this landed.</param>
		public static ZoneGate ParseGateAttributes(string Key, string Districts, string MinZones, string Knowledge, string MinTech,
			string Builders, string Creed, string CreedShare, string Strata, string Megastructure, string Capital, out string Error)
		{
			List<string> faults = new List<string>();
			string districts = null;
			if (!string.IsNullOrEmpty(Districts) && Districts.Trim().Length > 0)
			{
				districts = NormalizeList(Districts);
				if (districts == null)
				{
					faults.Add("Districts");
				}
				else if (ListContains(districts, AnyToken))
				{
					// "all" is how Styles spells "no restriction", so an author who writes it
					// here means the same thing rather than a district literally named all.
					districts = null;
				}
			}
			int minZones = 0;
			if (!string.IsNullOrEmpty(MinZones) && (!int.TryParse(MinZones, out minZones) || minZones < 0))
			{
				minZones = 0;
				faults.Add("MinZones");
			}
			string knowledge = null;
			if (!string.IsNullOrEmpty(Knowledge) && Knowledge.Trim().Length > 0)
			{
				knowledge = NormalizeList(Knowledge);
				if (knowledge == null || knowledge.IndexOf(RosterSeparator) >= 0)
				{
					knowledge = null;
					faults.Add("Knowledge");
				}
			}
			TechLevel minTech = TechLevel.Hands;
			if (!string.IsNullOrEmpty(MinTech) && (!System.Enum.TryParse<TechLevel>(MinTech.Trim(), ignoreCase: true, out minTech) || !IsKnownTechLevel(minTech)))
			{
				// Enum.TryParse takes any number the underlying type can hold, so "99" parses
				// happily into a level that does not exist and would gate the design forever.
				minTech = TechLevel.Hands;
				faults.Add("MinTech");
			}
			string builders = null;
			if (!string.IsNullOrEmpty(Builders) && Builders.Trim().Length > 0)
			{
				builders = NormalizeList(Builders);
				if (builders == null || ListContains(builders, AnyToken))
				{
					// "all" is how every list in this file spells "no restriction", and a design
					// that wants anybody at all wants nobody in particular.
					builders = null;
				}
			}
			// Trimmed and NOT folded: this is handed to the engine's faction table and read back to
			// the founder as prose, and a faction name is the game's, not ours, to re-case.
			string creed = (string.IsNullOrEmpty(Creed) || Creed.Trim().Length == 0) ? null : Creed.Trim();
			int creedShare = ZoneGate.ShareUnsaid;
			if (!string.IsNullOrEmpty(CreedShare) && CreedShare.Trim().Length > 0)
			{
				// A share outside 0..100 is not a stricter gate, it is a design nobody can ever
				// raise; dropped to the default like every other malformed attribute here.
				if (!int.TryParse(CreedShare.Trim(), out creedShare) || creedShare < 0 || creedShare > 100)
				{
					creedShare = ZoneGate.ShareUnsaid;
					faults.Add("CreedShare");
				}
			}
			if (creed == null && (creedShare != ZoneGate.ShareUnsaid || builders != null))
			{
				// Not a fault: Builders stands perfectly well on its own. A share without a creed
				// does not, and saying so is cheaper than a design that silently ignores half of
				// what its author wrote.
				if (creedShare != ZoneGate.ShareUnsaid)
				{
					creedShare = ZoneGate.ShareUnsaid;
					faults.Add("CreedShare (no Creed to take a share of)");
				}
			}
			string strata = null;
			if (!string.IsNullOrEmpty(Strata) && Strata.Trim().Length > 0)
			{
				strata = NormalizeList(Strata);
				if (strata == null)
				{
					faults.Add("Strata");
				}
				else if (Tokens(strata).Count == 1 && ListContains(strata, AnyToken))
				{
					strata = null;
				}
			}
			// No fault branch, and that is deliberate: KingdomLabRules.IsMegastructure reads "yes"
			// and every other string — including a typo — as ordinary. A malformed value here cannot
			// make a design unbuildable, only un-special, which is the safe direction for the one
			// attribute in this file that takes a whole city's purpose away.
			bool megastructure = KingdomLabRules.IsMegastructure(Megastructure);
			// No fault branch here either, and for the same reason one line up: a malformed value
			// makes a design un-special rather than unbuildable, which is the safe direction for an
			// attribute that can put a whole city's capital between the founder and a building.
			bool capital = KingdomLabRules.IsCapitalOnly(Capital);
			Error = (faults.Count == 0) ? null : ("building " + Key + " has a bad " + JoinOr(faults) + "; the attribute was ignored");
			return new ZoneGate(districts, minZones, knowledge, minTech, builders, creed, creedShare, strata, megastructure, capital);
		}

	}
}
