using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomResearchRules
	{
		// --- The words (STANDARDS 7b; RR4: no percentage, no bar, no ETA) ----------------------

		/// <summary>The roster key the trunk's first node mints, and the one the keepers' map itself
		/// waits on: before it, the keepers write nothing down and there is no map to draw.</summary>
		public const string NotesKey = "node:notes";

		/// <summary>The founder's journal entry for a node, filed unrevealed and revealed the day
		/// they first hear of it. One line, and it names the road rather than the destination.</summary>
		public static string LeadText(string Named, string Branch)
		{
			string named = string.IsNullOrEmpty(Named) ? "something" : Named;
			return string.IsNullOrEmpty(Branch)
				? ("There is a thing called " + named + ", and keepers somewhere know how it is done.")
				: ("There is a thing called " + named + ", and it belongs to the " + Branch + " road.");
		}

		/// <summary>The one sentence a map with no first node draws, and the whole of it.</summary>
		public static string NothingWrittenDown(string CityName)
		{
			return "Nobody at {{C|" + (string.IsNullOrEmpty(CityName) ? "the city" : CityName)
				+ "}} writes anything down. There is no map of what the keepers know, because nobody has begun keeping one.";
		}

		/// <summary>
		/// Why a bench may not take up a subject its city is not clever enough for. Names the mind
		/// the city has and the mind the work wants, and points at the only two things that ever
		/// answer it: somebody who already has it, or a school that raises what the city can teach.
		/// </summary>
		public static string TierRefusal(string CityName, string Named, int BestMind, int Wanted)
		{
			string city = string.IsNullOrEmpty(CityName) ? "the city" : CityName;
			string named = string.IsNullOrEmpty(Named) ? "that" : Named;
			return (BestMind <= 0)
				? ("There is nobody at " + city + " who could begin " + named + ". It wants a mind of {{C|" + Wanted
					+ "}}, and there is nobody at the bench at all. Take in somebody who already knows this kind of work.")
				: ("Nobody at " + city + " can hold " + named + " in their head. It wants a mind of {{C|" + Wanted
					+ "}}, and the ablest keeper there has {{C|" + BestMind
					+ "}}. Take in somebody who already knows this work, lodge a savant who does, or teach what the city can teach.");
		}

		/// <summary>A bench standing over nothing, said once. Not a fault and not a stall: a choice
		/// the founder has not made yet, and one nothing else in the game will remind them of.</summary>
		public static string NoSubjectLine(string LabName, string CityName)
		{
			return (string.IsNullOrEmpty(LabName) ? "The bench" : ("The " + LabName)) + " at "
				+ (string.IsNullOrEmpty(CityName) ? "the city" : CityName)
				+ " stands over nothing. Nobody has been set anything to work out.";
		}

		/// <summary>A subject that has become inadmissible after it was taken up. Deliberately
		/// does not name the hidden/forbidding fact: the research visibility law still applies
		/// while the saved labour waits on the shelf.</summary>
		public static string ClosedSubjectLine(string LabName, string CityName)
		{
			string lab = string.IsNullOrEmpty(LabName) ? "The bench" : ("The " + LabName);
			string city = string.IsNullOrEmpty(CityName) ? "the city" : CityName;
			return lab + " at " + city
				+ " has set this work aside. Its road is no longer open to this city; choose another subject or change who the city has become.";
		}

		/// <summary>A live prerequisite left after work began. Names the missing source and the
		/// repair, as STANDARDS 7b requires, without erasing already-paid thought.</summary>
		public static string MissingSourceLine(string LabName, string Named, string CityName,
			string Missing)
		{
			string lab = string.IsNullOrEmpty(LabName) ? "The bench" : ("The " + LabName);
			string named = string.IsNullOrEmpty(Named) ? "the work" : Named;
			string city = string.IsNullOrEmpty(CityName) ? "the city" : CityName;
			string missing = string.IsNullOrEmpty(Missing) ? "a source it requires" : Missing;
			return lab + " at " + city + " has set " + named + " aside. Nobody there still holds {{C|"
				+ missing + "}}. Bring that living source back, or take up another subject; the work already done is kept.";
		}

		/// <summary>
		/// Why a lab is producing nothing, said once and unsaid the moment it is not. Names exactly
		/// one thing &mdash; the first one that is actually zero &mdash; because a sentence that
		/// names three lacks tells the founder to fix none of them.
		/// </summary>
		public static string StallLine(string LabName, string Named, int CrewEffectiveness, int WearEffectiveness,
			int BestMind, int Wanted)
		{
			string lab = string.IsNullOrEmpty(LabName) ? "The bench" : ("The " + LabName);
			string named = string.IsNullOrEmpty(Named) ? "the work" : Named;
			if (CrewEffectiveness <= 0)
			{
				return lab + " is empty, and " + named + " waits. Nobody thinks a thing out by being near it.";
			}
			if (WearEffectiveness <= 0)
			{
				return lab + " is in no state to be worked in, and " + named + " waits on the mending.";
			}
			if (BestMind < Wanted)
			{
				return lab + " is crewed, and " + named + " wants a mind of " + Wanted + ". The ablest there has "
					+ ((BestMind > 0) ? BestMind.ToString() : "none to speak of") + ".";
			}
			return lab + " is standing idle, and " + named + " waits.";
		}

		/// <summary>What the ledger says when a ninth shelving pushes the least-advanced subject off
		/// the shelf. Once, by name, so nothing is lost in silence.</summary>
		public static string ForgottenLine(string CityName, string Named)
		{
			return "The keepers of " + (string.IsNullOrEmpty(CityName) ? "the city" : CityName)
				+ " have too many half-finished things on the shelf, and what they had of "
				+ (string.IsNullOrEmpty(Named) ? "the oldest" : Named) + " has gone back to nothing.";
		}

		// --- Small shared helpers --------------------------------------------------------------

		private static int Clamp(int Value, int Low, int High)
		{
			if (Value < Low)
			{
				return Low;
			}
			return (Value > High) ? High : Value;
		}

		private static string Fold(string Value)
		{
			if (Value == null)
			{
				return null;
			}
			string folded = Value.Trim().ToLowerInvariant();
			return (folded.Length == 0) ? null : folded;
		}

		private static string Trimmed(string Value)
		{
			if (Value == null)
			{
				return null;
			}
			string trimmed = Value.Trim();
			return (trimmed.Length == 0) ? null : trimmed;
		}
	}
}
