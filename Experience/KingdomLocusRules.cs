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
	public static partial class KingdomLocusRules
	{
		/// <summary>Durable state of one history-caused pilgrim opportunity. The opportunity is
		/// carried by the city book; the body is only its attended rendering.</summary>
		public enum PilgrimState
		{
			None = 0,
			Waiting = 1,
			Standing = 2
		}

		/// <summary>Typed result of adding one outsider-worthy city story.</summary>
		public readonly struct PilgrimAccrual
		{
			public readonly int Loudness;
			public readonly PilgrimState State;
			public readonly bool Minted;

			public PilgrimAccrual(int Loudness, PilgrimState State, bool Minted)
			{
				this.Loudness = Loudness;
				this.State = State;
				this.Minted = Minted;
			}
		}

		/// <summary>How many disputed, city-owned stories make the road loud enough to send one
		/// pilgrim. Three makes the visit earned without turning every calendar feast into traffic.</summary>
		public const int PilgrimStoryThreshold = 3;

		/// <summary>Bound on the exact cause frozen into the city book and the pilgrim body.</summary>
		public const int MaxPilgrimCauseChars = 384;

		/// <summary>Bound on a generated display identity retained for receipt recovery.</summary>
		public const int MaxPilgrimNameChars = 128;

		/// <summary>Bound on the city display name frozen with the caused visit.</summary>
		public const int MaxPilgrimPlaceChars = 256;

		/// <summary>World time for a road story to become a body at the heart.</summary>
		public const long PilgrimTravelTicks = KingdomRules.TicksPerDay;

		/// <summary>
		/// Adds one typed qualifying story. A live opportunity is never overwritten: later stories
		/// can make the road loud again only after the exact standing pilgrim has resolved.
		/// </summary>
		public static PilgrimAccrual AccruePilgrim(int Loudness, PilgrimState State)
		{
			int loudness = Loudness < 0 ? 0 : (Loudness >= PilgrimStoryThreshold
				? PilgrimStoryThreshold - 1 : Loudness);
			if (State == PilgrimState.Waiting || State == PilgrimState.Standing)
			{
				return new PilgrimAccrual(loudness, State, false);
			}
			loudness++;
			if (loudness < PilgrimStoryThreshold)
			{
				return new PilgrimAccrual(loudness, PilgrimState.None, false);
			}
			return new PilgrimAccrual(0, PilgrimState.Waiting, true);
		}

		public static bool KnownPilgrimState(int State)
		{
			return State >= (int)PilgrimState.None && State <= (int)PilgrimState.Standing;
		}

		/// <summary>Exact arrival and patience window priced from the frozen cause tick.</summary>
		public static bool TryPilgrimWindow(long CauseTick, out long ArrivalTick,
			out long DepartTick)
		{
			ArrivalTick = 0L;
			DepartTick = 0L;
			if (CauseTick <= 0L || CauseTick > long.MaxValue - PilgrimTravelTicks) return false;
			ArrivalTick = CauseTick + PilgrimTravelTicks;
			if (ArrivalTick > long.MaxValue - GuestPatienceTicks)
			{
				ArrivalTick = 0L;
				return false;
			}
			DepartTick = ArrivalTick + GuestPatienceTicks;
			return true;
		}

		/// <summary>Freezes a bounded plain-language cause. Null means the event cannot authorize a
		/// pilgrim opportunity.</summary>
		public static string PilgrimCause(string FeastName, string SettlementName, string DishName)
		{
			string feast = CleanCausePart(FeastName);
			string settlement = CleanCausePart(SettlementName);
			string dish = CleanCausePart(DishName);
			if (string.IsNullOrEmpty(feast) || string.IsNullOrEmpty(settlement)) return null;
			string cause = "the " + feast + " feast kept at " + settlement;
			if (!string.IsNullOrEmpty(dish)) cause += " over " + dish;
			return cause.Length <= MaxPilgrimCauseChars
				? cause : cause.Substring(0, MaxPilgrimCauseChars).TrimEnd();
		}

		public static string PilgrimGreeting(string Cause)
		{
			return "Live and drink. I came because the roads remember "
				+ (CleanCausePart(Cause) ?? "what was done here") + ".";
		}

		public static string PilgrimChronicleLine(string Name, string SettlementName,
			string Cause, bool Greeted)
		{
			string name = CleanCausePart(Name) ?? "a pilgrim";
			string settlement = CleanCausePart(SettlementName) ?? "the settlement";
			string cause = CleanCausePart(Cause) ?? "a story told on the road";
			return Greeted
				? name + " came to the heart of " + settlement + " because of " + cause
					+ ", was given water, and carried the story onward"
				: name + " came to the heart of " + settlement + " because of " + cause
					+ ", waited there, and went on unmet";
		}

		public static string PilgrimLedgerNote(string Name, string Cause, int DaysAgo)
		{
			string when = DaysAgo <= 0 ? "today" : (DaysAgo == 1
				? "a day before you saw it" : DaysAgo + " days before you saw it");
			return "{{K|" + (CleanCausePart(Name) ?? "A pilgrim") + " came to the heart because of "
				+ (CleanCausePart(Cause) ?? "a story told on the road") + ", waited, and went on "
				+ when + ". Nothing was lost.}}";
		}

		private static string CleanCausePart(string Value)
		{
			if (string.IsNullOrWhiteSpace(Value)) return null;
			string value = Value.Trim().Replace('\r', ' ').Replace('\n', ' ');
			while (value.IndexOf("  ", System.StringComparison.Ordinal) >= 0)
				value = value.Replace("  ", " ");
			return value.Length == 0 ? null : value;
		}

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
			return BenchDescription(Staffed ? KeeperServiceState.Ready
				: KeeperServiceState.Unstaffed, KeeperName);
		}
	}
}
