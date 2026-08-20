using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for the settlement's memory of its own people: how a settler's death is
	/// told, what a cairn is inscribed with, and who holds the settlement's one office and when
	/// it passes to someone else. The engine-coupled half that detects deaths, links a built
	/// cairn to a name, and moves the office's title between citizens is
	/// <see cref="KingdomOffices"/>, in the same folder.
	/// </summary>
	public static class KingdomOfficeRules
	{
		/// <summary>
		/// What is actually known about a settler's death, from the one witness the mod trusts:
		/// the engine's own death event. Never a specific killer's identity or weapon &mdash; only
		/// the coarse distinction the settlement itself could plausibly tell afterward.
		/// </summary>
		public enum DeathCause
		{
			/// <summary>No killer was reported. Used honestly rather than guessed at.</summary>
			Unknown,
			/// <summary>Killed by something the settlement does not call a raider.</summary>
			Violence,
			/// <summary>Killed by a creature spawned as part of a provoked raid.</summary>
			Raid,
			/// <summary>Killed by the founder.</summary>
			Player
		}

		/// <summary>
		/// Classifies a death from what the engine actually reported, in the order the settlement
		/// would judge it: whether the founder's own hand did it outranks everything else, a
		/// raider's hand is told as the raid it was, any other known hand is violence unnamed, and
		/// no hand at all is told as exactly that &mdash; unwitnessed, never invented.
		/// </summary>
		/// <param name="KillerIsPlayer">Whether the reported killer is the founder.</param>
		/// <param name="KillerIsRaider">Whether the reported killer was spawned as a raid.</param>
		/// <param name="KillerKnown">Whether any killer was reported at all.</param>
		public static DeathCause ClassifyCause(bool KillerIsPlayer, bool KillerIsRaider, bool KillerKnown)
		{
			if (KillerIsPlayer)
			{
				return DeathCause.Player;
			}
			if (KillerIsRaider)
			{
				return DeathCause.Raid;
			}
			if (KillerKnown)
			{
				return DeathCause.Violence;
			}
			return DeathCause.Unknown;
		}

		/// <summary>
		/// The clause naming how a settler was lost, fit to follow their name in a sentence
		/// ("Mirrehet, of Ash Reach, {clause}"). No trailing period, so it composes into both the
		/// chronicle's register and a cairn's inscription.
		/// </summary>
		public static string CauseClause(DeathCause Cause)
		{
			switch (Cause)
			{
			case DeathCause.Player:
				return "fell by the founder's own hand";
			case DeathCause.Raid:
				return "fell defending the stores when raiders came";
			case DeathCause.Violence:
				return "was lost to a blade that went unnamed";
			default:
				return "was found gone, and no one living can say how";
			}
		}

		/// <summary>
		/// What a cairn says once it is cut with a name: who they were, where they came from,
		/// when they arrived, and how they were lost, ending on the covenant the whole settlement
		/// was founded on. Blank origin or arrival clauses are simply omitted, never guessed at.
		/// </summary>
		/// <param name="Name">The settler's given name. Required.</param>
		/// <param name="Origin">Where they came from, or empty.</param>
		/// <param name="Arrived">The day they arrived, or empty.</param>
		/// <param name="SettlementName">The settlement the cairn stands in.</param>
		/// <param name="Cause">From <see cref="CauseClause"/>.</param>
		public static string Epitaph(string Name, string Origin, string Arrived, string SettlementName, string Cause)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("Here is remembered ").Append(Name);
			if (!string.IsNullOrEmpty(Origin))
			{
				builder.Append(", of ").Append(Origin);
			}
			builder.Append(", who came to ").Append(SettlementName);
			if (!string.IsNullOrEmpty(Arrived))
			{
				builder.Append(" the ").Append(Arrived);
			}
			builder.Append(" and ").Append(Cause).Append(". The water was shared, and is shared still.");
			return builder.ToString();
		}

		/// <summary>The chronicle's own telling of a death: no trailing period, lower-case start
		/// except where the settler's own name begins the clause.</summary>
		public static string MourningChronicle(string Name, string Origin, string SettlementName, DeathCause Cause)
		{
			string originClause = string.IsNullOrEmpty(Origin) ? "" : (", of " + Origin + ",");
			return Name + originClause + " " + CauseClause(Cause) + ", and " + SettlementName + " mourned";
		}

		/// <summary>The line spoken live, the moment the settlement learns of the loss.</summary>
		public static string MourningMessage(string Name, DeathCause Cause)
		{
			return Name + " " + CauseClause(Cause) + ".";
		}

		/// <summary>The chronicle's own telling of a cairn being cut with a name.</summary>
		public static string MemorialChronicle(string Name, string SettlementName)
		{
			return "a cairn was raised at " + SettlementName + " and cut with the name of " + Name;
		}

		/// <summary>
		/// Whether the earliest unhonoured death is ready for the next cairn to claim, and which
		/// one it is. Guards the index itself so a corrupted or stale count degrades to "nothing
		/// due" rather than an out-of-range read.
		/// </summary>
		/// <param name="DeadCount">Settlers recorded lost, <see cref="KingdomSystem.DeadNames"/>.Count.</param>
		/// <param name="MemorialsRaised">Cairns already cut, oldest-first, so far.</param>
		/// <param name="Index">The index into the dead roll to honour next, or -1.</param>
		/// <returns>True if a death is waiting for a cairn.</returns>
		public static bool TryNextToHonour(int DeadCount, int MemorialsRaised, out int Index)
		{
			if (MemorialsRaised < 0 || MemorialsRaised >= DeadCount)
			{
				Index = -1;
				return false;
			}
			Index = MemorialsRaised;
			return true;
		}

		/// <summary>
		/// How the settlement's one office changed hands, judged only from who held it before and
		/// who heads the living roster now. The office itself is never stored &mdash; it is always
		/// whoever has served longest, so this exists only to notice when that answer changed and
		/// say so once.
		/// </summary>
		public enum OfficeTransition
		{
			/// <summary>Same holder as last time this was checked (including nobody, both times).</summary>
			None,
			/// <summary>The settlement had no one to hold it, and now does.</summary>
			FirstHolder,
			/// <summary>One settler held it, and now a different one does.</summary>
			Passed,
			/// <summary>Someone held it, and now there is no one left.</summary>
			Vacant
		}

		/// <summary>Classifies an office transition from the previous and current holder's names
		/// (null for nobody). Pure equality: reordering the roster without changing who leads it
		/// is not this function's concern, because the roster is never reordered.</summary>
		public static OfficeTransition ClassifyTransition(string PreviousHolder, string CurrentHolder)
		{
			if (PreviousHolder == CurrentHolder)
			{
				return OfficeTransition.None;
			}
			if (CurrentHolder == null)
			{
				return OfficeTransition.Vacant;
			}
			if (PreviousHolder == null)
			{
				return OfficeTransition.FirstHolder;
			}
			return OfficeTransition.Passed;
		}

		/// <summary>
		/// The titles the office may be known by. A name, never a rank: nothing here implies pay,
		/// authority, or a job the settlement did not already have someone doing. Chosen per
		/// settlement by <see cref="ChooseTitle"/> rather than configured, so two settlements
		/// read differently without asking the founder to pick.
		/// </summary>
		public static readonly string[] OfficeTitles = new string[5]
		{
			"the water-keeper",
			"the eldest",
			"who reads the charter aloud",
			"the first-poured",
			"keeper of the well"
		};

		/// <summary>
		/// Picks this settlement's title for the office, stably: the same settlement name always
		/// yields the same title, without the mod having to remember which one it chose. Never the
		/// runtime's own randomized string hash, which is reseeded per process and would relabel
		/// every settlement's office on the next launch.
		/// </summary>
		/// <param name="SettlementName">The settlement's own name. Empty or null takes the first title.</param>
		public static string ChooseTitle(string SettlementName)
		{
			if (string.IsNullOrEmpty(SettlementName))
			{
				return OfficeTitles[0];
			}
			int hash = StableHash(SettlementName);
			int index = hash % OfficeTitles.Length;
			if (index < 0)
			{
				index += OfficeTitles.Length;
			}
			return OfficeTitles[index];
		}

		/// <summary>
		/// The chronicle's own telling of an office changing hands, or empty for
		/// <see cref="OfficeTransition.None"/>, which is never announced.
		/// </summary>
		public static string TransitionChronicle(OfficeTransition Transition, string Title, string Holder, string SettlementName)
		{
			switch (Transition)
			{
			case OfficeTransition.FirstHolder:
				return Holder + " is named " + Title + " of " + SettlementName;
			case OfficeTransition.Passed:
				return "the office of " + Title + " of " + SettlementName + " passes to " + Holder;
			case OfficeTransition.Vacant:
				return SettlementName + " has no one left to hold the office of " + Title;
			default:
				return "";
			}
		}

		/// <summary>
		/// A stable string hash (FNV-1a, 32-bit): deterministic across processes and builds,
		/// unlike <see cref="object.GetHashCode"/> on <c>string</c>, whose randomization exists
		/// for hash-flooding security and is reseeded every process launch. A choice this mod
		/// means to keep &mdash; a settlement's office title &mdash; cannot be built on a hash that
		/// changes every time the game restarts.
		/// </summary>
		private static int StableHash(string Value)
		{
			unchecked
			{
				int hash = (int)2166136261;
				for (int i = 0; i < Value.Length; i++)
				{
					hash = (hash ^ Value[i]) * 16777619;
				}
				return hash;
			}
		}
	}
}
