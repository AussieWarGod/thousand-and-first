using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Read-only kingdom domain digests for the scenario harness: growth stage, water and food
	/// state, and faction standings, each answered as stable key=value tokens a persona can bind
	/// to instead of prose.
	/// <para>
	/// PRESENCE LAW. Observation never mints identity or state. The kingdom system is read through
	/// <c>GetSystem</c>, never <c>RequireSystem</c>: a world that holds no founded kingdom answers
	/// <c>founded=false</c> honestly, and asking leaves the world exactly as it was. An unfounded
	/// world is an ANSWER, not a refusal, so every digest journals OK
	/// (<see cref="KingdomScenarioVerbs"/> owns the one journal row; nothing here shows a popup).
	/// </para>
	/// <para>
	/// Every report carries <see cref="CodeDigest"/> beside its tokens, so an expectation can bind
	/// <c>~taf-scenario-digest</c> or a token the law owns (<c>founded=false</c>,
	/// <c>stage=Camp</c>) and survive every rewording of the sentences around them.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioDigestVerbs
	{
		/// <summary>Stable reason code carried by every digest report.</summary>
		internal const string CodeDigest = "taf-scenario-digest";

		internal const string StageVerb = "stagedigest";

		internal const string ResourceVerb = "resourcedigest";

		internal const string StandingVerb = "standingdigest";

		/// <summary>
		/// Per-faction rows are bounded so one digest stays one journal row of sane size in a
		/// realm that has met everyone; <c>standings=</c> always reports the full count.
		/// </summary>
		private const int MaxStandingRows = 16;

		/// <summary>The kingdom growth stage, or <c>founded=false</c> for an unfounded world.</summary>
		internal static string Stage(out bool Ok)
		{
			Ok = true;
			KingdomSystem system = Observe();
			if (system == null || !system.Founded) return Unfounded("stage");
			return Header("stage") + "\nfounded=true\nstage=" + system.Stage;
		}

		/// <summary>
		/// Water and food state. The settlement-wide streaks and population come from the system;
		/// the stored/open water and daily food figures are read from the ground under the player,
		/// exactly as <c>kingdom:dump</c> reads them, because water sits in real vessels in a real
		/// zone. With no player zone those three figures cover no ground and honestly read zero.
		/// </summary>
		internal static string Resources(out bool Ok)
		{
			Ok = true;
			KingdomSystem system = Observe();
			if (system == null || !system.Founded) return Unfounded("resource");
			GameObject player = The.Player;
			Zone zone = player == null ? null : player.CurrentZone;
			int stored = zone == null ? 0 : KingdomGrowth.CountStoredWater(zone);
			int open = zone == null ? 0 : KingdomGrowth.CountOpenWater(zone);
			int made = zone == null
				? 0 : KingdomGrowth.FoodMadePerDay(KingdomSurvey.Take(zone, system));
			StringBuilder sb = new StringBuilder(Header("resource"));
			sb.Append("\nfounded=true")
				.Append("\nstoredwater=").Append(stored)
				.Append("\nopenwater=").Append(open)
				.Append("\nfoodperday=").Append(made)
				.Append("\nhungerstreak=").Append(system.HungerStreak)
				.Append("\ndrystreak=").Append(system.DryStreak)
				.Append("\npopulation=").Append(system.Population);
			sb.Append(zone == null
				? "\nNo player zone; the water and food figures above cover no ground."
				: "\nWater and food figures read from " + zone.ZoneID + ".");
			return sb.ToString();
		}

		/// <summary>
		/// Faction standings: the full count, then one row per faction in ordinal key order so two
		/// runs over the same realm print the same rows in the same order.
		/// </summary>
		internal static string Standings(out bool Ok)
		{
			Ok = true;
			KingdomSystem system = Observe();
			if (system == null || !system.Founded) return Unfounded("standing");
			Dictionary<string, int> standings = system.Standings;
			List<string> keys = new List<string>(standings.Keys);
			keys.Sort(StringComparer.Ordinal);
			StringBuilder sb = new StringBuilder(Header("standing"));
			sb.Append("\nfounded=true\nstandings=").Append(keys.Count);
			int shown = keys.Count < MaxStandingRows ? keys.Count : MaxStandingRows;
			for (int i = 0; i < shown; i++)
				sb.Append("\nfaction=").Append(keys[i]).Append(':').Append(standings[keys[i]]);
			if (keys.Count > shown)
				sb.Append("\nFirst ").Append(shown).Append(" of ").Append(keys.Count)
					.Append(" factions listed; the count above covers them all.");
			return sb.ToString();
		}

		/// <summary>
		/// The system as it stands, or null. <c>GetSystem</c> and never <c>RequireSystem</c>: a
		/// digest that could mint the system it reports on would be the observation founding the
		/// state, which is exactly what the presence law forbids.
		/// </summary>
		private static KingdomSystem Observe()
		{
			XRLGame game = The.Game;
			return game == null ? null : game.GetSystem<KingdomSystem>();
		}

		private static string Header(string Kind)
		{
			return "{{C|Kingdom " + Kind + " digest}} [" + CodeDigest + "]";
		}

		/// <summary>An unfounded world's one honest answer, shared by all three digests.</summary>
		private static string Unfounded(string Kind)
		{
			return Header(Kind) + "\nfounded=false\nNo founded kingdom exists in this world; the "
				+ "system is read by presence and never created by observation.";
		}
	}
}
