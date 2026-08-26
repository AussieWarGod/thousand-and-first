using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomLocusRules
	{
		/// <summary>
		/// Chooses who tends the bench from the settlement's own born citizens, keeping the same
		/// keeper across passes when they are still a candidate rather than reshuffling for its
		/// own sake &mdash; a keeper who steps away and back should still be the keeper.
		/// </summary>
		/// <param name="Candidates">Eligible settlers this pass, in scan order.</param>
		/// <param name="CurrentKeeperID">The presently marked keeper's id, or null/empty if
		/// nobody is marked.</param>
		/// <returns>The chosen candidate's id, or null when there are no candidates at all.</returns>
		public static string SelectKeeper(IReadOnlyList<string> Candidates, string CurrentKeeperID)
		{
			if (Candidates == null || Candidates.Count == 0)
			{
				return null;
			}
			if (!string.IsNullOrEmpty(CurrentKeeperID))
			{
				for (int i = 0; i < Candidates.Count; i++)
				{
					if (Candidates[i] == CurrentKeeperID)
					{
						return CurrentKeeperID;
					}
				}
			}
			return Candidates[0];
		}

		/// <summary>
		/// Drams drawn from the settlement's own stores to offer a guest. Small and symbolic on
		/// purpose &mdash; this is a greeting, not an arrival; <c>KingdomRules.DramsPerArrival</c>
		/// is what a settler who is staying costs.
		/// </summary>
		public const int GuestWaterCostDrams = 1;

		/// <summary>How rarely a traveller who is not settling passes through. Three days: often
		/// enough that a settlement feels visited, rare enough that it stays an event.</summary>
		public const long GuestIntervalTicks = KingdomRules.TicksPerDay * 3;

		/// <summary>
		/// How long a guest waits, once arrived, before giving up and moving on unmet. Real
		/// elapsed game time, and it runs whether or not anybody is there to watch it run
		/// (Addendum 8 clause 1): travellers walk the road on their own business, and a gate
		/// nobody answers is answered by nobody for exactly as long as that lasts. What the
		/// founder gets at awareness is the dated news of who came and went
		/// (<see cref="PassagesLedgerNote"/>), never a stranger who has been standing in the
		/// square since spring.
		/// <para>
		/// Shorter than <see cref="GuestIntervalTicks"/>, and that is load-bearing: it is what
		/// makes "at most one is still standing" true of
		/// <c>KingdomRules.PassagesThrough</c>'s answer, and it is what made an existing guest
		/// blocking the next one a bound rather than a coincidence.
		/// </para>
		/// </summary>
		public const long GuestPatienceTicks = KingdomRules.TicksPerDay / 3;

		/// <summary>Whether it is time for the next traveller to arrive. The caller is
		/// responsible for confirming no guest is already present; this only judges the
		/// clock.</summary>
		public static bool GuestShouldArrive(long TimeTicks, long NextGuestTick)
		{
			return TimeTicks >= NextGuestTick;
		}

		/// <summary>The tick after which the settlement may draw its next guest.</summary>
		public static long NextGuestDueTick(long TimeTicks)
		{
			return TimeTicks + GuestIntervalTicks;
		}

		/// <summary>The tick a newly arrived guest's patience runs out.</summary>
		public static long GuestDepartTickFor(long ArrivalTick)
		{
			return ArrivalTick + GuestPatienceTicks;
		}

		/// <summary>Whether a guest who has not been offered water should give up and leave.
		/// <paramref name="DepartTick"/> of zero or less means no guest is tracked, which is
		/// never a reason to depart one.</summary>
		public static bool GuestShouldDepartUnattended(long TimeTicks, long DepartTick)
		{
			return DepartTick > 0 && TimeTicks >= DepartTick;
		}

		/// <summary>The guest's spoken thanks, shown as a popup at the moment they are offered
		/// water &mdash; the mod's central rite in miniature, extended to someone who is not
		/// staying.</summary>
		public static string GuestThanks(string GuestName, string SettlementName)
		{
			return GuestName + " drinks, and takes a moment over it.\n\n\"Live and drink. I'll say a good word for "
				+ SettlementName + " on the road, and mean it.\"";
		}

		/// <summary>The chronicle line for a guest's passage, in the founder's-perspective voice
		/// <c>KingdomChronicle.Record</c> expects. Differs only in whether the settlement is the
		/// one who is remembered kindly for it &mdash; never in blame; an ignored guest is a
		/// missed pleasantry, not a fault logged against the founder.</summary>
		public static string GuestChronicleLine(bool Greeted, string SettlementName)
		{
			if (Greeted)
			{
				return "a traveller stopped at " + SettlementName + ", was given water, and went on speaking well of it";
			}
			return "a traveller passed through " + SettlementName + " and went on again, having spoken to no one";
		}

		/// <summary>The homecoming ledger's note for a guest who came and went while the founder
		/// was elsewhere &mdash; news to discover, not a debt to answer for.</summary>
		public static string GuestLedgerNote(string GuestName)
		{
			return GuestLedgerNote(GuestName, 0);
		}

		/// <summary>
		/// The same note, dated against the day their patience actually ran out. They gave up
		/// when they gave up, which may be well before the pass that noticed &mdash; the same
		/// honest elapsed a brink quotes, for a piece of news that costs nothing.
		/// </summary>
		/// <param name="GuestName">Who it was.</param>
		/// <param name="DaysAgo">Whole days since they left. Zero and below drop the clause.</param>
		public static string GuestLedgerNote(string GuestName, int DaysAgo)
		{
			string when = (DaysAgo <= 0)
				? ""
				: ((DaysAgo == 1) ? " a day before you saw it" : (" " + DaysAgo + " days before you saw it"));
			return "{{K|" + GuestName + " passed through while you were away, waited a while, and moved on"
				+ when + ". Nothing was lost.}}";
		}

		/// <summary>
		/// How a run of unwitnessed passages is dated: against the day the founder is being told
		/// about it, exactly as a subsidence rung is. Nobody's name is in it, because nobody
		/// wrote their name down &mdash; there was no one at the gate to ask.
		/// </summary>
		/// <param name="DaysAgo">Whole days since the last of them stood at the gate. Zero and
		/// below read as today.</param>
		public static string PassageWhen(int DaysAgo)
		{
			if (DaysAgo <= 0)
			{
				return "the last of them today";
			}
			return (DaysAgo == 1)
				? "the last of them a day before you saw it"
				: ("the last of them " + DaysAgo + " days before you saw it");
		}

		/// <summary>
		/// The homecoming ledger's note for the travellers who came, waited out their patience at
		/// an unanswered gate, and went on again while the founder was elsewhere. One line for
		/// the whole run however long it was: the chronicle keeps two hundred entries and a
		/// season of ambient traffic is not what they are for.
		/// </summary>
		/// <param name="Passed">How many came and went. Zero or less has no news in it and
		/// answers null, which is the caller's signal to say nothing.</param>
		/// <param name="DaysAgo">Days since the last of them, for <see cref="PassageWhen"/>.</param>
		public static string PassagesLedgerNote(int Passed, int DaysAgo)
		{
			if (Passed <= 0)
			{
				return null;
			}
			if (Passed == 1)
			{
				return "{{K|A traveller came through while you were away, waited at the gate, and moved on — "
					+ PassageWhen(DaysAgo) + ". Nothing was lost.}}";
			}
			return "{{K|" + Passed + " travellers came through while you were away, waited at the gate, and moved on — "
				+ PassageWhen(DaysAgo) + ". Nothing was lost.}}";
		}

		/// <summary>The chronicle's own telling of the same run, in the founder's-perspective
		/// voice <c>KingdomChronicle.Record</c> expects. Never blame: an unanswered gate is a
		/// missed pleasantry and not a fault logged against anybody.</summary>
		public static string PassagesChronicleLine(int Passed, string SettlementName, int DaysAgo)
		{
			if (Passed <= 0)
			{
				return null;
			}
			string who = (Passed == 1) ? "a traveller" : (Passed + " travellers");
			return who + " passed through " + SettlementName + " while you were away and went on again, "
				+ PassageWhen(DaysAgo);
		}
	}
}
