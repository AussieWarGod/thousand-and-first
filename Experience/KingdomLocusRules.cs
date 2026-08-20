using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for the settlement being a place rather than a screen: who tends the
	/// gathering bench, what they say about the state the settlement is in, and when a traveller
	/// who is not one of the settlement's own passes through. No <c>XRL</c> usings &mdash;
	/// everything here is deterministic given the inputs its caller (<see cref="KingdomLocus"/>)
	/// reads off the live kingdom.
	/// </summary>
	public static class KingdomLocusRules
	{
		/// <summary>
		/// What the keeper is minding today. Read worst-first by <see cref="ClassifyMood"/>: a
		/// settlement can be thirsty and growing in the same breath, and the keeper leads with
		/// the thirst.
		/// </summary>
		public enum KeeperMood
		{
			Peaceful,
			Growing,
			Raided,
			Threatened,
			Thirsty
		}

		/// <summary>
		/// How long a raid stays "recent" for the keeper's own account of things, once the
		/// settlement's dry streak and any live threat have both cleared. Two days: long enough
		/// that the keeper still has something to say about it, short enough that a settlement
		/// which has plainly recovered is not stuck describing itself as raided forever.
		/// </summary>
		public const long RecentRaidWindowTicks = KingdomRules.TicksPerDay * 2;

		/// <summary>
		/// Picks the keeper's mood from the settlement's own state. Checked worst-first and each
		/// branch returns on its own condition only, so inverting any one of them changes exactly
		/// one case rather than silently reordering the rest.
		/// </summary>
		/// <param name="DryStreakActive">The settlement's water thirst streak is currently
		/// running (<c>KingdomSystem.DryStreak &gt; 0</c>).</param>
		/// <param name="RaidIncoming">A raid has been warned and is not yet resolved.</param>
		/// <param name="RecentlyRaided">A raid resolved &mdash; by wall, tribute, word, or
		/// looting &mdash; within <see cref="RecentRaidWindowTicks"/>.</param>
		/// <param name="Grew">The settlement's population is higher than it was the last time
		/// this keeper spoke.</param>
		public static KeeperMood ClassifyMood(bool DryStreakActive, bool RaidIncoming, bool RecentlyRaided, bool Grew)
		{
			if (DryStreakActive)
			{
				return KeeperMood.Thirsty;
			}
			if (RaidIncoming)
			{
				return KeeperMood.Threatened;
			}
			if (RecentlyRaided)
			{
				return KeeperMood.Raided;
			}
			if (Grew)
			{
				return KeeperMood.Growing;
			}
			return KeeperMood.Peaceful;
		}

		/// <summary>Whether a raid that resolved at <paramref name="LastRaidTick"/> is still
		/// "recent" at <paramref name="TimeTicks"/>. A raid that has never happened
		/// (<paramref name="LastRaidTick"/> &lt;= 0) is never recent.</summary>
		public static bool WasRecentlyRaided(long LastRaidTick, long TimeTicks)
		{
			return LastRaidTick > 0 && TimeTicks - LastRaidTick < RecentRaidWindowTicks;
		}

		/// <summary>The keeper's half of a conversation, in the three pieces
		/// <c>Qud.API.ConversationsAPI.addSimpleConversationToObject</c> takes: an opening line,
		/// the one question the founder can ask back, and the keeper's answer to it.</summary>
		public readonly struct KeeperSpeech
		{
			public readonly string Greeting;

			public readonly string Question;

			public readonly string Answer;

			public KeeperSpeech(string Greeting, string Question, string Answer)
			{
				this.Greeting = Greeting;
				this.Question = Question;
				this.Answer = Answer;
			}
		}

		/// <summary>The fixed question the founder puts to whoever is minding the bench. Held as
		/// a constant so <see cref="KeeperSpeechFor"/>'s mood table only has to vary the
		/// answer.</summary>
		public const string KeeperQuestion = "How does it stand?";

		/// <summary>
		/// Composes the keeper's greeting and answer for a mood. <paramref name="SettlementName"/>
		/// is folded into the answer, not the greeting, so the greeting reads naturally the first
		/// time a founder walks up before a settlement necessarily has much of a name to it yet.
		/// </summary>
		public static KeeperSpeech KeeperSpeechFor(KeeperMood Mood, string SettlementName)
		{
			switch (Mood)
			{
				case KeeperMood.Thirsty:
					return new KeeperSpeech(
						"Sit if you like, but don't ask me for water. There isn't extra to give today.",
						KeeperQuestion,
						"The cisterns are running low at " + SettlementName + ". Everyone's watching the level and nobody's saying so out loud.");
				case KeeperMood.Threatened:
					return new KeeperSpeech(
						"Sit if you want, but keep an ear out. Word is riders are coming for us.",
						KeeperQuestion,
						"There's a raid warned against " + SettlementName + ". Whatever gets decided about it, it won't be decided at this bench.");
				case KeeperMood.Raided:
					return new KeeperSpeech(
						"We're still finding what's missing. Sit anyway; the bench survived, at least.",
						KeeperQuestion,
						"Raiders came through " + SettlementName + " not long ago. We're patching what they took and counting what's left.");
				case KeeperMood.Growing:
					return new KeeperSpeech(
						"Have you seen how many of us there are now? Sit, if you can find the room.",
						KeeperQuestion,
						SettlementName + " keeps meeting people it doesn't have names for yet. That's the good kind of trouble.");
				default:
					return new KeeperSpeech(
						"Sit a while. There's nothing pressing today, and the bench doesn't mind being used for nothing.",
						KeeperQuestion,
						"Quiet. Water in the casks, roofs over heads, and nobody's counting days since the last trouble at " + SettlementName + ".");
			}
		}

		/// <summary>What the gathering bench's own description says about itself, on examine.
		/// Furniture with nobody minding it says so plainly rather than pretending to be
		/// occupied.</summary>
		public static string BenchDescription(bool Staffed, string KeeperName)
		{
			if (!Staffed)
			{
				return "Split logs worn smooth by sitting, empty for now. No one is minding it; whoever settles here first will make it theirs.";
			}
			return "Split logs worn smooth by sitting. " + KeeperName + " keeps this bench, and most of what is worth knowing about the settlement passes across it sooner or later.";
		}

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
		/// How long a guest waits, once arrived, before giving up and moving on unmet. Checked
		/// only on the settlement's own zone-activation pass (never a per-turn clock), so this
		/// counts down real elapsed game time but is only ever <i>observed</i> while the founder
		/// is actually there to take turns &mdash; a guest never expires "in absence".
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
			return "{{K|" + GuestName + " passed through while you were away, waited a while, and moved on. Nothing was lost.}}";
		}
	}
}
