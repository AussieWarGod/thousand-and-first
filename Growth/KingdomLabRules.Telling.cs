using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLabRules
	{
		// --- Creed friction (DIVERSITY §3.6) ---------------------------------------------------
		//
		// Nothing new is written. The tags are the shipped QoL vocabulary, the ceiling is Addendum
		// 4d's own, and the standing cost rides the AdjustStanding path that already exists. What
		// is here is the arithmetic of WHEN the city speaks against the hall, and what it says.

		/// <summary>What a vat-house offers the quarter around it, and it is not a compliment.
		/// A resident who <c>Refuses="taf:offal"</c> will not live here &mdash; authored, never
		/// derived, because revulsion is a belief and <c>KingdomQolRules.Derive</c> deliberately
		/// never produces a refusal.</summary>
		public const string TagDamp = "taf:damp";

		/// <summary>The other half of the same sentence.</summary>
		public const string TagOffal = "taf:offal";

		/// <summary>
		/// The share of a city that must hold a creed the hall offends before anybody speaks
		/// against it. A tenth: below that the objection is one person's, and one person's objection
		/// is a conversation rather than a petition.
		/// </summary>
		public const int SpokenAgainstPercent = 10;

		/// <summary>
		/// Whether the city would speak against the hall for a procedure.
		/// <para>
		/// The trigger DIVERSITY &sect;3.6 names: a first procedure of consequence performed while a
		/// hostile-creed minority lives in the city. A minority, not a majority &mdash; a city where
		/// the offended creed is dominant never gets this petition, because that city could not
		/// staff the hall in the first place (Addendum 4d's fault-line ceiling does that work, and
		/// no rule of ours says so).
		/// </para>
		/// </summary>
		/// <param name="Offended">People here holding a creed the procedure costs standing with.</param>
		/// <param name="People">Everyone here.</param>
		/// <param name="AlreadySpoken">Whether the hall has been spoken against before. Once is the
		/// whole of it: a city that petitioned about the hall and was answered does not petition
		/// again every time the hall is used.</param>
		public static bool SpeaksAgainstHall(int Offended, int People, bool AlreadySpoken)
		{
			if (AlreadySpoken || People <= 0 || Offended <= 0 || Offended * 2 >= People)
			{
				return false;
			}
			return Offended * 100 >= People * SpokenAgainstPercent;
		}

		/// <summary>What the petitioner is waiting to speak about.</summary>
		public static string SpokenAgainstSubject()
		{
			return "what is done at the hall";
		}

		/// <summary>
		/// What they actually say, in their own mouth, and there is no correct answer to it. The
		/// founder's call, exactly as &sect;3.6 asks: friction is named people and placement, never
		/// a meter.
		/// </summary>
		/// <param name="Creed">The creed the speaker holds, as the founder reads it.</param>
		public static string SpokenAgainstSpeech(string Creed)
		{
            return "\"I have no quarrel with the hall's people and I will not pretend I do. But I was raised to believe a body is not a "
				+ "workshop, and I walk past that door every morning. I am not asking you to pull it down. I am asking you to say, out "
				+ "loud, in front of " + Named(Creed) + " and everyone else, that you know what it is you have built here.\"";
		}

		/// <summary>The deed, for the chronicle, when the founder answers.</summary>
		public static string SpokenAgainstDeed(string Name)
		{
			return "the hall at " + Named(Name) + " was spoken for out loud, in front of everyone who had to walk past it";
		}

		/// <summary>
		/// What a procedure costs the founder in standing, disclosed before it is committed.
		/// <para>
		/// The record's <c>Creeds</c> is read in the same <c>-Faction</c> removal idiom the QoL
		/// vocabulary already speaks, and spent through the shipped <c>AdjustStanding</c> path with
		/// its existing chronicle entry and outsider-register drift. Nothing new is written; what is
		/// here is only the reading and the sentence.
		/// </para>
		/// </summary>
		/// <returns>Faction name to standing delta, deltas negative. Never null.</returns>
		public static List<KeyValuePair<string, int>> StandingCost(string Creeds, int PerCreed)
		{
			List<KeyValuePair<string, int>> cost = new List<KeyValuePair<string, int>>();
			if (string.IsNullOrEmpty(Creeds) || PerCreed <= 0)
			{
				return cost;
			}
			string[] tokens = Creeds.Split(',');
			for (int i = 0; i < tokens.Length; i++)
			{
				string token = tokens[i].Trim();
				if (token.Length < 2 || token[0] != '-')
				{
					continue;
				}
				string faction = token.Substring(1).Trim();
				if (faction.Length > 0)
				{
					cost.Add(new KeyValuePair<string, int>(faction, -PerCreed));
				}
			}
			return cost;
		}

		/// <summary>
		/// Standing one procedure costs with each creed it offends. Deliberately modest and
		/// deliberately flat across the classes: a graft is a thing you did once, and a ladder of
		/// escalating standing costs would turn a belief into a meter, which &sect;3.6 forbids by
		/// name.
		/// </summary>
		public const int StandingPerCreed = 50;

	}
}
