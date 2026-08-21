using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What a creed makes of the founder's own body. Lane 2 of BUILDING-CATALOGUE-BRIEF
	/// Addendum 13: <i>"the city reacts to what you ARE &hellip; wonder and fear by belief."</i>
	/// </summary>
	internal enum KingdomRegard : byte
	{
		/// <summary>The creed has nothing to say about it, which is the ordinary answer and is
		/// never a failure.</summary>
		Nothing = 0,

		/// <summary>They admire it, and say so.</summary>
		Wonder = 1,

		/// <summary>They mind it, and mutter.</summary>
		Unease = 2
	}

	/// <summary>
	/// What the founder is, read the way a settler's quality-of-life profile is read: off vanilla
	/// truth first, and nothing invented.
	/// <para>
	/// <b>Every field names the exact vanilla read that fills it</b>, exactly as
	/// <c>ResidentTruth</c> does for a settler (<c>KingdomQolRules</c>), and for the same reason:
	/// the derivation table is then testable without a running game, and a genotype or a mutation
	/// another mod ships answers correctly without that mod knowing this system exists.
	/// </para>
	/// </summary>
	internal readonly struct KingdomFounderNature
	{
		/// <summary>
		/// <c>GameObject.GetGenotype()</c> (<c>D/XRL/World/GameObject.cs:10550</c>), for the
		/// telling.
		/// <para>
		/// <b>Display, and deliberately nothing else.</b> The obvious thing to add here is a pair
		/// of flags off <c>genotypeEntry.IsTrueKin</c> / <c>IsMutant</c>
		/// (<c>D/XRL/GenotypeEntry.cs:53-55</c>) and a rule that fault-line creeds mind mutants.
		/// That rule would be this mod's opinion and not the game's: vanilla put numbers against
		/// PARTS (<see cref="PartFeeling"/>) and never against genotypes, and a creed that hates
		/// mutants hates them in Qud by scoring the mutations. Two fields nothing reads would be
		/// two fields a later wave writes a mechanic out of, so they are not here.
		/// </para>
		/// </summary>
		internal readonly string Genotype;

		/// <summary><c>GameObject.GetInstalledCybernetics().Count</c>. The chrome, counted.</summary>
		internal readonly int Chrome;

		/// <summary>
		/// The part this creed has an opinion about, as a phrase fit to follow "the": the mutation
		/// or part named in the creed faction's own <c>PartReputation</c> table
		/// (<c>D/XRL/World/Faction.cs:150</c>, loaded from <c>&lt;partreputation About= Value=&gt;</c>
		/// at <c>D/XRL/World/Factions.cs:664-670</c>). Empty when the creed's table names nothing
		/// the founder carries.
		/// </summary>
		internal readonly string RegardedPart;

		/// <summary>
		/// That table's own number for it. <b>The sign is the whole judgement</b> &mdash; vanilla
		/// wrote <c>-200</c> against <c>MassMind</c> for the Seekers of the Sightless Way
		/// (<c>B/Factions.xml:1397</c>) and <c>+300</c> against <c>Wings</c> for the birds
		/// (<c>:362</c>), and this mod does not get a second opinion about either.
		/// </summary>
		internal readonly int PartFeeling;

		/// <summary>Whether the creed's own <c>&lt;interests&gt;</c> list buys the tag
		/// <c>cybernetics</c> (<c>D/XRL/World/FactionInterest.cs</c>, e.g.
		/// <c>B/Factions.xml:1272</c>).</summary>
		internal readonly bool RevereChrome;

		/// <summary>Whether that same list carries <c>cybernetics</c> under
		/// <c>Inverse="true"</c> &mdash; vanilla's own way of saying a faction defines itself
		/// against a thing (the Putus Templar's "the modern world", <c>B/Factions.xml:1271</c>).</summary>
		internal readonly bool RefuseChrome;

		internal KingdomFounderNature(string genotype, int chrome, string regardedPart, int partFeeling, bool revereChrome, bool refuseChrome)
		{
			Genotype = genotype;
			Chrome = chrome;
			RegardedPart = regardedPart;
			PartFeeling = partFeeling;
			RevereChrome = revereChrome;
			RefuseChrome = refuseChrome;
		}

		/// <summary>Nobody in particular. What a null player reads as, and what a creed says
		/// nothing about.</summary>
		internal static KingdomFounderNature Unremarkable
		{
			get { return new KingdomFounderNature(null, 0, null, 0, false, false); }
		}
	}

	/// <summary>
	/// Lane 2 as arithmetic and prose. Pure, engine-free, drawless.
	/// <para>
	/// <b>The mesh condition is why this file has no table in it.</b> Addendum 13 requires each
	/// lane to be a rendering through surfaces that already exist, and the surface here is
	/// vanilla's own: <c>Faction.PartReputation</c> is the game's table of which factions admire
	/// or fear which bodies, and <c>Faction.Interests</c> is the game's record of what a faction
	/// defines itself for and against. Both are read; neither is written; and a creed another mod
	/// ships is answered correctly the day it loads, because the mod already filled those fields
	/// in for its own reasons.
	/// </para>
	/// <para>
	/// <b>What this may never become.</b> A reaction is a shade, a greeting line, or a clause on
	/// a happening &mdash; it is never a mechanic. Nothing here changes standing, refuses a
	/// settler, alters production, or moves a body. Addendum 13 lane 2 asks for wonder and fear,
	/// and wonder and fear are things people SAY.
	/// </para>
	/// </summary>
	internal static class KingdomNatureRules
	{
		/// <summary>The tag vanilla's own faction interests use for chrome
		/// (<c>B/Factions.xml:1271-1272</c>). Named here so the engine edge and the prose cannot
		/// disagree about which interest is being read.</summary>
		internal const string ChromeInterestTag = "cybernetics";

		/// <summary>No reaction. The key a founder nobody has an opinion about carries.</summary>
		internal const int NoKey = 0;

		/// <summary>
		/// What this creed makes of this founder.
		/// <para>
		/// <b>The part table decides whenever it says anything</b>, because it is the only one of
		/// the two surfaces vanilla put a NUMBER on: a faction that wrote <c>-200</c> against a
		/// mutation has stated a strength of feeling, and a faction that merely lists an interest
		/// has not. Chrome answers only when the part table is silent, and a creed that both
		/// buys and refuses chrome (which vanilla's Putus Templar entry literally does, once each
		/// way) is read as refusing it &mdash; a faction that sells a thing it will not carry is
		/// not admiring it.
		/// </para>
		/// </summary>
		internal static KingdomRegard Judge(KingdomFounderNature nature)
		{
			if (!string.IsNullOrEmpty(nature.RegardedPart) && nature.PartFeeling != 0)
			{
				return (nature.PartFeeling > 0) ? KingdomRegard.Wonder : KingdomRegard.Unease;
			}
			if (nature.Chrome <= 0)
			{
				return KingdomRegard.Nothing;
			}
			if (nature.RefuseChrome)
			{
				return KingdomRegard.Unease;
			}
			return nature.RevereChrome ? KingdomRegard.Wonder : KingdomRegard.Nothing;
		}

		/// <summary>
		/// A small stable number for "this creed feels this way about this body". The city says a
		/// thing once per state-change, and this is what a change IS: a different creed, a
		/// different part, a different sign, or chrome where there was none.
		/// </summary>
		internal static int RegardKey(string creedName, KingdomFounderNature nature)
		{
			KingdomRegard regard = Judge(nature);
			if (regard == KingdomRegard.Nothing)
			{
				return NoKey;
			}
			int key = KingdomCityRules.StableId(creedName ?? "");
			key = unchecked((key * 31) + KingdomCityRules.StableId(nature.RegardedPart ?? ""));
			key = unchecked((key * 31) + (int)regard);
			key = unchecked((key * 31) + ((nature.Chrome > 0) ? 1 : 0));
			// Never the sentinel: a real reaction that happened to fold to zero would be silenced
			// forever, which is the one failure this key must not have.
			return (key == NoKey) ? 1 : key;
		}

		/// <summary>
		/// What they say, once, where the founder can hear it.
		/// <para>
		/// Named rather than vague (STANDARDS 7b): the line says which creed and which thing about
		/// the founder, because a city that mutters without saying why is a city the player cannot
		/// act on or enjoy.
		/// </para>
		/// </summary>
		/// <param name="nature">The founder as vanilla describes them.</param>
		/// <param name="creedDisplayName">The creed's own display name, from
		/// <c>KingdomCreed.CreedName</c>.</param>
		/// <param name="settlementName">The city the creed is the creed of.</param>
		/// <returns>One sentence, or empty when the creed has nothing to say.</returns>
		internal static string RegardLine(KingdomFounderNature nature, string creedDisplayName, string settlementName)
		{
			string who = string.IsNullOrEmpty(creedDisplayName) ? "The people of " + Place(settlementName) : creedDisplayName + " in " + Place(settlementName);
			switch (Judge(nature))
			{
			case KingdomRegard.Wonder:
				if (!string.IsNullOrEmpty(nature.RegardedPart))
				{
					return who + " make a sign when you pass. It is the " + nature.RegardedPart + " they are making it at.";
				}
				return who + " ask twice, and then a third time, whether they may look at the implant.";
			case KingdomRegard.Unease:
				if (!string.IsNullOrEmpty(nature.RegardedPart))
				{
					return who + " will not stand downwind of you. It is the " + nature.RegardedPart + " they mind.";
				}
				return who + " count your chrome under their breath, and get it wrong on purpose.";
			default:
				return "";
			}
		}

		/// <summary>
		/// The chronicle's clause for the same thing: lower case, no trailing period, third
		/// person, so the outsider register can retell it without the founder's own voice getting
		/// into it (<c>KingdomChronicle.RecordDisputed</c>).
		/// </summary>
		internal static string RegardTelling(KingdomFounderNature nature, string creedDisplayName, string settlementName, string founderName)
		{
			string who = string.IsNullOrEmpty(creedDisplayName) ? ("the people of " + Place(settlementName)) : (creedDisplayName + " of " + Place(settlementName));
			string what = !string.IsNullOrEmpty(nature.RegardedPart)
				? ("the " + nature.RegardedPart)
				: ("the chrome " + Named(founderName) + " carries"
					+ (string.IsNullOrEmpty(nature.Genotype) ? "" : (", " + nature.Genotype.ToLowerInvariant() + " as they are")));
			switch (Judge(nature))
			{
			case KingdomRegard.Wonder:
				return who + " took to speaking well of " + what;
			case KingdomRegard.Unease:
				return who + " took to speaking quietly about " + what;
			default:
				return "";
			}
		}

		private static string Place(string settlementName)
		{
			return string.IsNullOrEmpty(settlementName) ? "the settlement" : settlementName;
		}

		private static string Named(string founderName)
		{
			return string.IsNullOrEmpty(founderName) ? "the founder" : founderName;
		}
	}
}
