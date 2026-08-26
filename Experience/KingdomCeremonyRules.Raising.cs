using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomCeremonyRules
	{
		// ==================================================================================
		// The raising ceremony
		// ==================================================================================

		/// <summary>
		/// A completion is attended when it lands within one day of its own due tick. A scaffold
		/// only ever advances while its zone is active, so every completion happens with the
		/// founder somewhere in the settlement; this distinguishes a founder who was already
		/// there watching the clock run out from one who has just walked back in after real time
		/// carried the due tick past while they were elsewhere &mdash; the same tick therefore
		/// reads as a late arrival, not a live one.
		/// </summary>
		/// <param name="CompleteTick">The scaffold's own due tick.</param>
		/// <param name="NowTicks">The tick completion is actually being resolved at.</param>
		/// <returns>True for a live completion; false for one caught up on return.</returns>
		public static bool IsAttended(long CompleteTick, long NowTicks)
		{
			return NowTicks - CompleteTick < KingdomRules.TicksPerDay;
		}

		/// <summary>The chronicle's line for a completion the founder watched happen: crew
		/// gathered, water shared, named if anyone was there to be named. Lower-case clause, no
		/// trailing period (the chronicle supplies both).</summary>
		/// <param name="DisplayName">The finished building's name.</param>
		/// <param name="SeatName">The settlement's seat name.</param>
		/// <param name="Present">Names of settlers found nearby, or null/empty for none found.</param>
		/// <param name="PlanQuote">The surveyor's plan text staked for this building, or null when
		/// it was never staked as a plan (a direct commission has none).</param>
		public static string RaisingAttendedChronicle(string DisplayName, string SeatName, IList<string> Present, string PlanQuote)
		{
			string who = NamePresent(Present);
			string quote = QuoteClause(PlanQuote);
			if (who == null)
			{
				return "the " + DisplayName + " was raised at " + SeatName + ", the water shared over it" + quote;
			}
			return "the " + DisplayName + " was raised at " + SeatName + " with " + who + " standing by and the water shared" + quote;
		}

		/// <summary>The chronicle's line for a completion nobody was there to see: plain, past
		/// tense, no crew named.</summary>
		public static string RaisingUnattendedChronicle(string DisplayName, string SeatName, string PlanQuote)
		{
			return "the " + DisplayName + " stood finished at " + SeatName + " before anyone came home to see it" + QuoteClause(PlanQuote);
		}

		/// <summary>The homecoming ledger's own line for an unattended completion &mdash; the
		/// "what happened while you were away" register the chronicle line above does not reach
		/// on its own.</summary>
		public static string RaisingLedgerNote(string DisplayName)
		{
			return "{{G|The " + DisplayName + " was finished while you were away.}}";
		}

		/// <summary>The live message for an attended completion.</summary>
		public static string RaisingAttendedMessage(string DisplayName, IList<string> Present)
		{
			string who = NamePresent(Present);
			if (who == null)
			{
				return "{{G|The " + DisplayName + " is complete. The water is shared.}}";
			}
			return "{{G|The " + DisplayName + " is complete. Present: " + who + ". The water is shared.}}";
		}

		private static string QuoteClause(string PlanQuote)
		{
			return string.IsNullOrEmpty(PlanQuote) ? "" : (", true to the plan staked there: \"" + PlanQuote + "\"");
		}

		private static string NamePresent(IList<string> Present)
		{
			if (Present == null || Present.Count == 0)
			{
				return null;
			}
			if (Present.Count == 1)
			{
				return Present[0];
			}
			if (Present.Count == 2)
			{
				return Present[0] + " and " + Present[1];
			}
			return Present[0] + ", " + Present[1] + ", and others";
		}

		/// <summary>Drams shared as part of an attended raising. Small and decorative: a partial
		/// or zero draw from the stores changes nothing about whether the ceremony happens.</summary>
		public const int RaisingShareDrams = 2;

	}
}
