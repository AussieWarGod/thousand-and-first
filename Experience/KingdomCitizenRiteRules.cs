using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// Why a settler is, or is not, somebody the founder can share water with on Qud's own terms.
	/// <para>
	/// The order is frozen, and the two refusals at the bottom of it are the two documented ways
	/// the engine's water ritual FAILS HARD rather than declining: an unregistered base faction
	/// throws out of <c>WaterRitualRecord.Initialize</c>
	/// (<c>D/XRL/World/Factions.cs:72</c>, reached from
	/// <c>D/XRL/World/Parts/WaterRitualRecord.cs:44</c>), and a ritual liquid that is not in
	/// <c>LiquidVolume.Liquids</c> nulls out of <c>WaterRitual.LiquidName</c>
	/// (<c>D/XRL/World/Parts/LiquidVolume.cs:417-434</c>,
	/// <c>D/XRL/World/Conversations/Parts/WaterRitual.cs:32</c>). Neither can be recovered from
	/// once the conversation is open, so both are refused before the settler is ever made a host.
	/// </para>
	/// </summary>
	public enum CitizenRiteVerdict : byte
	{
		/// <summary>They can host the rite.</summary>
		Host = 0,

		/// <summary>There is no realm, or this is not its ground.</summary>
		Unfounded = 1,

		/// <summary>Not one of the city's own. Guests, raiders and the founder's own party are
		/// nobody's citizens.</summary>
		NotCitizen = 2,

		/// <summary>Nothing that can hold a conversation.</summary>
		NoBody = 3,

		/// <summary>Their allegiance names a faction the game has never registered. Hosting them
		/// would throw the moment the ritual opened.</summary>
		UnknownFaction = 4,

		/// <summary>The realm's ritual liquid is not a liquid this game knows. Hosting them would
		/// null out naming what is being shared.</summary>
		UnknownLiquid = 5
	}

	/// <summary>
	/// The pure half of lane 1 (BUILDING-CATALOGUE-BRIEF Addendum 13): <i>"water ritual with
	/// citizens &mdash; Qud's founding social act on our runtime faction &hellip; the basin's
	/// fiction completed."</i>
	/// <para>
	/// The mesh condition holds by construction: this file decides nothing about what the ritual
	/// gives. <b>Vanilla decides all of it</b>, off the runtime faction the founding already mints
	/// &mdash; its reputation, and its <c>WaterRitualRecipe</c>, which <c>KingdomDish</c> has been
	/// stamping since the food lane and which, until now, no living creature in Qud could ever hand
	/// over. What this file owns is the judgment and the words.
	/// </para>
	/// </summary>
	internal static class KingdomCitizenRiteRules
	{
		/// <summary>Shared days at which a settler stops greeting the founder as the person who
		/// founded the place and starts greeting them as someone they live near. The same counter
		/// the inward rite already keeps (<c>KingdomWaterRite.SharedDaysProperty</c>), read rather
		/// than duplicated.</summary>
		internal const int SettledDays = 30;

		/// <summary>
		/// Whether this settler may be made a host of the rite.
		/// <para>
		/// Preconditions: none. Side effects: none. Failure mode: none &mdash; total.
		/// </para>
		/// </summary>
		/// <param name="Founded">A realm holds this ground.</param>
		/// <param name="Citizen">Carries the city's own citizen mark.</param>
		/// <param name="HasBody">Has a brain that could hold a conversation.</param>
		/// <param name="FactionKnown">Their base allegiance resolves in the faction table.</param>
		/// <param name="LiquidKnown">The realm's ritual liquid is empty (the engine's own default
		/// stands) or names a liquid this game has.</param>
		internal static CitizenRiteVerdict Judge(bool Founded, bool Citizen, bool HasBody, bool FactionKnown, bool LiquidKnown)
		{
			if (!Founded)
			{
				return CitizenRiteVerdict.Unfounded;
			}
			if (!Citizen)
			{
				return CitizenRiteVerdict.NotCitizen;
			}
			if (!HasBody)
			{
				return CitizenRiteVerdict.NoBody;
			}
			if (!FactionKnown)
			{
				return CitizenRiteVerdict.UnknownFaction;
			}
			if (!LiquidKnown)
			{
				return CitizenRiteVerdict.UnknownLiquid;
			}
			return CitizenRiteVerdict.Host;
		}

		/// <summary>
		/// What the settler says when the founder stops to talk. Written so the water-sharing
		/// choice underneath it reads as the obvious next thing, and so a founder who has been away
		/// for a season is greeted differently from one who lives here.
		/// </summary>
		/// <param name="CityName">The city. Never null in practice; empty degrades to "here".</param>
		/// <param name="SharedDays">Days this settler has lived through in the city, from
		/// <c>KingdomWaterRite.SharedDaysOf</c>.</param>
		internal static string Greeting(string CityName, int SharedDays)
		{
			string place = string.IsNullOrEmpty(CityName) ? "here" : CityName;
			switch (Band(SharedDays))
			{
			case 2:
				return "\"I have been in " + place + " long enough to know where the shade falls. Sit, if you are staying.\"";
			case 1:
				return "\"Still finding my feet in " + place + ", but the roof holds and the water is counted honestly. That is two more than the road offered.\"";
			default:
				return "\"So this is " + place + ". I came a long way on the word of it.\"";
			}
		}

		/// <summary>
		/// Which of the three greetings a settler is owed: newcomer, settling, settled.
		/// <para>
		/// Exists as its own function because a conversation, once built, is a fixed string on the
		/// object &mdash; so the caller has to be able to ask "has this settler crossed into a
		/// different greeting?" without rebuilding one every pass to find out. A settler stamped
		/// once on the day they arrived and never re-read would greet their founder as a stranger
		/// for the rest of their life, which is the bug this band exists to make catchable.
		/// </para>
		/// </summary>
		/// <returns>0, 1 or 2.</returns>
		internal static int Band(int SharedDays)
		{
			if (SharedDays >= SettledDays)
			{
				return 2;
			}
			return (SharedDays > 0) ? 1 : 0;
		}

		// ==================================================================================
		// The chronicle as a tradable secret (W5's named remainder, landed in W6)
		// ==================================================================================

		/// <summary>
		/// Which journal drawer a city's telling goes in.
		/// <para>
		/// <c>"Gossip"</c>, and the choice is load-bearing rather than decorative:
		/// <c>WaterRitualSellSecret.GetWeight</c> reads the observation's category and hands a
		/// <c>Category == "Gossip"</c> entry to the ritual's <i>share some gossip</i> element and a
		/// non-gossip one to <i>share a secret</i>
		/// (<c>D/XRL/World/Conversations/Parts/WaterRitualSellSecret.cs</c>). What a city's roads
		/// are saying about it is gossip; it is not a location, a recipe, or a sultan's deed.
		/// </para>
		/// </summary>
		internal const string SecretCategory = "Gossip";

		/// <summary>
		/// The tags that decide who wants to hear it &mdash; <b>vanilla's own interest vocabulary,
		/// used unchanged</b>.
		/// <para>
		/// A faction buys a secret from the player when one of its <c>&lt;interest Tags="..."/&gt;</c>
		/// entries matches the note's attributes (<c>Faction.GetInterestIn</c>, reached from
		/// <c>Faction.GetBuySecretWeight</c>). In the shipped table <c>settlement</c> is declared by
		/// seventeen factions and <c>gossip</c> by five &mdash; the two that a founded city's own
		/// history honestly IS. Nothing here declares a new interest, adds a conversation part, or
		/// touches anybody's faction: the city writes a line, and the people who already cared about
		/// settlements already want to hear it.
		/// </para>
		/// </summary>
		internal static string[] SecretTags()
		{
			return new string[2] { "gossip", "settlement" };
		}

		/// <summary>
		/// One chronicle telling as a journal secret: a stable id and the text that travels.
		/// <para>
		/// <b>The text is the OUTSIDER register's line</b>, not the official one. The city's own
		/// book is written in the founder's voice and dated in its own calendar; what a stranger
		/// could be told over shared water is the version the roads carry, which this mod has been
		/// composing beside every chronicle entry since it shipped. Trading the founder's own diary
		/// would be a different thing, and a less true one.
		/// </para>
		/// <para>
		/// <b>The id is derived from the text and the immutable realm id</b>, so filing it twice is a no-op:
		/// <c>JournalAPI.AddObservation</c> refuses an id it already holds, which makes the pass
		/// that files this idempotent without a cursor to keep in step with a register that is
		/// trimmed at two hundred entries. Two identical tellings share an id and are filed once,
		/// which is the honest answer &mdash; the roads are already saying that one.
		/// </para>
		/// <para>
		/// Preconditions: none. Side effects: none. Failure mode: <c>false</c> with both outputs
		/// empty, for an unproved realm identity or a telling with no words.
		/// </para>
		/// </summary>
		internal static bool TryTradableSecret(string ExactRealmId, string OutsiderLine,
			out string Id, out string Text)
		{
			Id = "";
			Text = "";
			if (!KingdomIdentityRules.IsRealmId(ExactRealmId) ||
				string.IsNullOrEmpty(OutsiderLine))
			{
				return false;
			}
			Text = OutsiderLine;
			Id = "taf:chronicle:" + ExactRealmId + ":"
				+ Simulation.City.KingdomCityRules.StableId(
					ExactRealmId + "\u001f" + OutsiderLine);
			return true;
		}

		/// <summary>How a settler ends the conversation. Qud's own parting, because that is what
		/// this whole act is borrowed from.</summary>
		internal static string Farewell()
		{
			return "Live and drink.";
		}

		/// <summary>
		/// The one line a blocked realm gets, once. STANDARDS &sect;7b: a rite that will not open
		/// and will not say why is the exact defect the rule was written against &mdash; and the
		/// two blocking verdicts are both somebody else's data being wrong, which the founder can
		/// do nothing about but is entitled to know.
		/// </summary>
		/// <returns>The line, or empty for anything that is merely not applicable.</returns>
		internal static string BlockedLine(CitizenRiteVerdict Verdict, string CityName, string Liquid)
		{
			string place = string.IsNullOrEmpty(CityName) ? "your city" : CityName;
			switch (Verdict)
			{
			case CitizenRiteVerdict.UnknownFaction:
				return "The people of " + place + " answer to a name this world has no record of, so they cannot share water in it. Nothing else about them is affected.";
			case CitizenRiteVerdict.UnknownLiquid:
				return "The rite of " + place + " is poured in " + (string.IsNullOrEmpty(Liquid) ? "something" : Liquid)
					+ ", which this world has no such liquid for. Its people will not share water until that is put right.";
			default:
				return "";
			}
		}
	}
}
