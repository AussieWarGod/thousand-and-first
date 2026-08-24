using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// Persisted petition lifecycle. Values are append-only: the integer is carried by kingdom
	/// saves and by the seat-swap record.
	/// </summary>
	public enum PetitionLifecycle : byte
	{
		None = 0,
		Offered = 1,
		Accepted = 2,
		Declined = 3,
		Resolved = 4,
		Expired = 5
	}

	/// <summary>
	/// Engine-free petition calendar, transition, snapshot, and completion rules. The shell in
	/// <c>KingdomPetitions</c> owns messages and kingdom fields; this file owns every verdict.
	/// </summary>
	public static class KingdomPetitionRules
	{
		// XRL.World.Calendar in 2.0.211.51: 438000 ticks/year, ordinary month boundaries at
		// 36001-tick offsets, and the five-day Ut yara Ux from offsets 216001 through 222000.
		// It is a named month in Calendar.GetMonth, so one Qud year has thirteen offer buckets.
		public const long TicksPerYear = 438000L;

		public const int MonthsPerYear = 13;

		private static readonly long[] MonthStarts = new long[MonthsPerYear - 1]
		{
			36001L, 72001L, 108001L, 144001L, 180001L, 216001L,
			222001L, 258001L, 294001L, 330001L, 366001L, 402001L
		};

		/// <summary>
		/// Monotone identity of Calendar.GetMonth/GetYear at a game tick. Negative/corrupt ticks
		/// fail closed to the first month rather than producing a negative offer lane.
		/// </summary>
		public static long CanonicalMonthOrdinal(long Tick)
		{
			long tick = (Tick > 0L) ? Tick : 0L;
			long year = tick / TicksPerYear;
			long withinYear = tick % TicksPerYear;
			int month = 0;
			while (month < MonthStarts.Length && withinYear >= MonthStarts[month])
			{
				month++;
			}
			return year * MonthsPerYear + month;
		}

		public static bool IsActive(PetitionLifecycle State)
		{
			return State == PetitionLifecycle.Offered || State == PetitionLifecycle.Accepted;
		}

		public static bool IsTerminal(PetitionLifecycle State)
		{
			return State == PetitionLifecycle.Declined
				|| State == PetitionLifecycle.Resolved
				|| State == PetitionLifecycle.Expired;
		}

		/// <summary>Frozen lifecycle graph. No terminal state reopens.</summary>
		public static bool CanTransition(PetitionLifecycle From, PetitionLifecycle To)
		{
			switch (From)
			{
			case PetitionLifecycle.None:
			case PetitionLifecycle.Declined:
			case PetitionLifecycle.Resolved:
			case PetitionLifecycle.Expired:
				return To == PetitionLifecycle.Offered;
			case PetitionLifecycle.Offered:
				return To == PetitionLifecycle.Accepted
					|| To == PetitionLifecycle.Declined
					|| To == PetitionLifecycle.Expired;
			case PetitionLifecycle.Accepted:
				return To == PetitionLifecycle.Resolved || To == PetitionLifecycle.Expired;
			default:
				return false;
			}
		}

		/// <summary>
		/// One offer per canonical Qud month. <paramref name="LastOfferMonth"/> is the new
		/// persisted authority; <paramref name="LegacyLastTick"/> safely closes the current month
		/// for an old save that has not acquired it yet.
		/// </summary>
		public static bool CanOffer(long NowTick, long LastOfferMonth, long LegacyLastTick,
			PetitionLifecycle State, KingdomRules.PetitionKind Kind)
		{
			if (IsActive(State) || Kind != KingdomRules.PetitionKind.None)
			{
				return false;
			}
			long last = LastOfferMonth;
			if (last < 0L && LegacyLastTick > 0L)
			{
				last = CanonicalMonthOrdinal(LegacyLastTick);
			}
			return CanonicalMonthOrdinal(NowTick) > last;
		}

		/// <summary>
		/// Expiry uses subtraction only after ordering, avoiding overflow near long.MaxValue.
		/// Existing semantics are preserved: the petition remains live through exactly Lifetime
		/// ticks and expires on the first later tick.
		/// </summary>
		public static bool IsExpired(long NowTick, long IssuedTick, long Lifetime)
		{
			if (Lifetime < 0L || IssuedTick < 0L || NowTick <= IssuedTick)
			{
				return false;
			}
			return NowTick - IssuedTick > Lifetime;
		}

		/// <summary>Only the settlement that minted the snapshot may settle it.</summary>
		public static bool OriginMatches(string Snapshot, string Current)
		{
			return !string.IsNullOrEmpty(Snapshot)
				&& string.Equals(Snapshot, Current, StringComparison.Ordinal);
		}

		/// <summary>
		/// Snapshot the exact threshold spoken at offer time. Later population changes cannot move
		/// the goalposts.
		/// </summary>
		public static int SnapshotTarget(KingdomRules.PetitionKind Kind, int Population)
		{
			switch (Kind)
			{
			case KingdomRules.PetitionKind.Thirst:
				return KingdomRules.ThirstPetitionTarget(Population);
			case KingdomRules.PetitionKind.Shelter:
				return (Population >= int.MaxValue) ? int.MaxValue : Math.Max(1, Population + 1);
			case KingdomRules.PetitionKind.Peace:
				return -100;
			case KingdomRules.PetitionKind.Memorial:
			case KingdomRules.PetitionKind.Flesh:
			case KingdomRules.PetitionKind.Chrome:
				return 1;
			case KingdomRules.PetitionKind.Craft:
			default:
				return 0;
			}
		}

		/// <summary>Tests current evidence against the immutable target snapshot.</summary>
		public static bool IsMet(KingdomRules.PetitionKind Kind, int Target, int StoredWater,
			int Beds, int IdleWorks, int Standing, bool HasShrine)
		{
			switch (Kind)
			{
			case KingdomRules.PetitionKind.Thirst:
				return StoredWater >= Target;
			case KingdomRules.PetitionKind.Shelter:
				return Target > 0 && Beds >= Target;
			case KingdomRules.PetitionKind.Craft:
				return IdleWorks <= Target;
			case KingdomRules.PetitionKind.Peace:
				return Standing >= Target;
			case KingdomRules.PetitionKind.Memorial:
				return HasShrine;
			case KingdomRules.PetitionKind.Flesh:
			case KingdomRules.PetitionKind.Chrome:
				// Accepting follows hearing the pure speech at the Charter. No later world mutation is
				// needed for these authored objections; acceptance itself is the witnessed answer.
				return Target > 0;
			default:
				return false;
			}
		}

		/// <summary>Acceptance is a hard gate in the rule, not merely a shell convention.</summary>
		public static bool CanResolve(PetitionLifecycle State, KingdomRules.PetitionKind Kind,
			int Target, int StoredWater, int Beds, int IdleWorks, int Standing, bool HasShrine)
		{
			return State == PetitionLifecycle.Accepted
				&& IsMet(Kind, Target, StoredWater, Beds, IdleWorks, Standing, HasShrine);
		}

		/// <summary>Fallback lifecycle for fields loaded from a pre-lifecycle save.</summary>
		public static PetitionLifecycle NormalizeLegacy(PetitionLifecycle State,
			KingdomRules.PetitionKind Kind)
		{
			if ((int)State > (int)PetitionLifecycle.Expired)
			{
				return (Kind == KingdomRules.PetitionKind.None)
					? PetitionLifecycle.None : PetitionLifecycle.Offered;
			}
			if (IsActive(State) && Kind == KingdomRules.PetitionKind.None)
			{
				return PetitionLifecycle.Expired;
			}
			if ((State == PetitionLifecycle.None || IsTerminal(State))
				&& Kind != KingdomRules.PetitionKind.None)
			{
				// Old petitions were never explicitly accepted. Migrating them as Offered is the only
				// state that cannot invent a reward.
				return PetitionLifecycle.Offered;
			}
			return State;
		}

		/// <summary>Whether a loaded target could invent completion or erase the spoken goal.</summary>
		public static bool TargetNeedsRepair(KingdomRules.PetitionKind Kind, int Target)
		{
			switch (Kind)
			{
			case KingdomRules.PetitionKind.Thirst:
			case KingdomRules.PetitionKind.Shelter:
			case KingdomRules.PetitionKind.Memorial:
			case KingdomRules.PetitionKind.Flesh:
			case KingdomRules.PetitionKind.Chrome:
				return Target <= 0;
			case KingdomRules.PetitionKind.Peace:
				return Target == 0;
			default:
				return false;
			}
		}
	}
}
