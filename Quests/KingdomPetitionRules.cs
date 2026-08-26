using System;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Legacy projection values. Runtime authority is the retained terminal operation in
	/// <see cref="KingdomLifecycleBook.Petition"/>.</summary>
	public enum PetitionLifecycle : byte
	{
		None = 0,
		Offered = 1,
		Accepted = 2,
		Declined = 3,
		Resolved = 4,
		Expired = 5
	}

	/// <summary>Engine-free petition transition, snapshot, clock, and completion rules.</summary>
	public static class KingdomPetitionRules
	{
		public const string ActiveClock = "petition-active";
		public const string PausedClock = "petition-paused";
		public const string OptionClosedClock = "petition-option-closed";

		public const long TicksPerYear = 438000L;
		public const int MonthsPerYear = 13;

		private static readonly long[] MonthStarts = new long[MonthsPerYear - 1]
		{
			36001L, 72001L, 108001L, 144001L, 180001L, 216001L,
			222001L, 258001L, 294001L, 330001L, 366001L, 402001L
		};

		public static PetitionLifecycle LifecycleOf(KingdomLifecycleAction Action)
		{
			switch (Action)
			{
			case KingdomLifecycleAction.PetitionOffer: return PetitionLifecycle.Offered;
			case KingdomLifecycleAction.PetitionAccept: return PetitionLifecycle.Accepted;
			case KingdomLifecycleAction.PetitionDecline: return PetitionLifecycle.Declined;
			case KingdomLifecycleAction.PetitionResolve: return PetitionLifecycle.Resolved;
			case KingdomLifecycleAction.PetitionExpire: return PetitionLifecycle.Expired;
			default: return PetitionLifecycle.None;
			}
		}

		public static PetitionLifecycle LifecycleOf(KingdomLifecycleOperation Operation)
		{
			return Operation != null && Operation.Lane == KingdomLifecycleLane.Petition
				? LifecycleOf(Operation.Action) : PetitionLifecycle.None;
		}

		public static bool IsActive(PetitionLifecycle State)
		{
			return State == PetitionLifecycle.Offered || State == PetitionLifecycle.Accepted;
		}

		public static bool IsTerminal(PetitionLifecycle State)
		{
			return State == PetitionLifecycle.Declined || State == PetitionLifecycle.Resolved
				|| State == PetitionLifecycle.Expired;
		}

		/// <summary>Exact domain graph. Repeated Accept is reserved for pause/resume restamping.</summary>
		public static bool CanFollow(KingdomLifecycleAction Prior, KingdomLifecycleAction Next)
		{
			if (Prior == KingdomLifecycleAction.None)
				return Next == KingdomLifecycleAction.PetitionOffer;
			switch (Prior)
			{
			case KingdomLifecycleAction.PetitionOffer:
				return Next == KingdomLifecycleAction.PetitionAccept
					|| Next == KingdomLifecycleAction.PetitionDecline
					|| Next == KingdomLifecycleAction.PetitionExpire;
			case KingdomLifecycleAction.PetitionAccept:
				return Next == KingdomLifecycleAction.PetitionAccept
					|| Next == KingdomLifecycleAction.PetitionResolve
					|| Next == KingdomLifecycleAction.PetitionExpire;
			case KingdomLifecycleAction.PetitionDecline:
			case KingdomLifecycleAction.PetitionResolve:
			case KingdomLifecycleAction.PetitionExpire:
				return Next == KingdomLifecycleAction.PetitionOffer;
			default:
				return false;
			}
		}

		/// <summary>Compatibility graph for callers which still reason in projection values.</summary>
		public static bool CanTransition(PetitionLifecycle From, PetitionLifecycle To)
		{
			KingdomLifecycleAction prior = ActionOf(From);
			KingdomLifecycleAction next = ActionOf(To);
			return next != KingdomLifecycleAction.None && CanFollow(prior, next)
				&& !(From == PetitionLifecycle.Accepted && To == PetitionLifecycle.Accepted);
		}

		public static long CanonicalMonthOrdinal(long Tick)
		{
			long tick = Tick > 0L ? Tick : 0L;
			long year = tick / TicksPerYear;
			long withinYear = tick % TicksPerYear;
			int month = 0;
			while (month < MonthStarts.Length && withinYear >= MonthStarts[month]) month++;
			return year * MonthsPerYear + month;
		}

		/// <summary>Documented district interval, rounded up so scaling never fires early.</summary>
		public static long ScaledInterval(long BaseTicks, int Percent)
		{
			if (BaseTicks <= 0L || Percent <= 0 || Percent > 100) return -1L;
			if (BaseTicks > (long.MaxValue - 99L) / Percent) return -1L;
			return (BaseTicks * Percent + 99L) / 100L;
		}

		public static bool CanOfferAt(long NowTick, long LastOfferTick, long EnabledTick,
			long Interval)
		{
			if (NowTick < 0L || LastOfferTick < 0L || EnabledTick < 0L || Interval <= 0L)
				return false;
			long anchor = Math.Max(LastOfferTick, EnabledTick);
			return NowTick >= anchor && NowTick - anchor >= Interval;
		}

		/// <summary>Older public gate retained for source compatibility.</summary>
		public static bool CanOffer(long NowTick, long LastOfferMonth, long LegacyLastTick,
			PetitionLifecycle State, KingdomRules.PetitionKind Kind)
		{
			if (IsActive(State) || Kind != KingdomRules.PetitionKind.None) return false;
			long last = LastOfferMonth;
			if (last < 0L && LegacyLastTick > 0L)
				last = CanonicalMonthOrdinal(LegacyLastTick);
			return CanonicalMonthOrdinal(NowTick) > last;
		}

		public static bool TryDeadline(long IssuedTick, long Lifetime, out long Deadline)
		{
			Deadline = 0L;
			if (IssuedTick < 0L || Lifetime <= 0L || IssuedTick > long.MaxValue - Lifetime)
				return false;
			Deadline = IssuedTick + Lifetime;
			return true;
		}

		public static long PauseRemaining(long NowTick, long Deadline)
		{
			if (NowTick < 0L || Deadline < 0L) return -1L;
			return Deadline > NowTick ? Deadline - NowTick : 1L;
		}

		public static bool TryResumeDeadline(long NowTick, long Remaining, out long Deadline)
		{
			Deadline = 0L;
			if (NowTick < 0L || Remaining <= 0L || NowTick > long.MaxValue - Remaining)
				return false;
			Deadline = NowTick + Remaining;
			return true;
		}

		public static bool IsExpired(long NowTick, long Deadline)
		{
			return NowTick >= 0L && Deadline >= 0L && NowTick > Deadline;
		}

		public static bool IsExpired(long NowTick, long IssuedTick, long Lifetime)
		{
			return TryDeadline(IssuedTick, Lifetime, out long deadline)
				&& IsExpired(NowTick, deadline);
		}

		public static bool OriginMatches(string Snapshot, string Current)
		{
			return !string.IsNullOrEmpty(Snapshot)
				&& string.Equals(Snapshot, Current, StringComparison.Ordinal);
		}

		public static int SnapshotTarget(KingdomRules.PetitionKind Kind, int Population)
		{
			switch (Kind)
			{
			case KingdomRules.PetitionKind.Thirst:
				return KingdomRules.ThirstPetitionTarget(Population);
			case KingdomRules.PetitionKind.Shelter:
				return Population >= int.MaxValue ? int.MaxValue : Math.Max(1, Population + 1);
			case KingdomRules.PetitionKind.Peace: return -100;
			case KingdomRules.PetitionKind.Memorial:
			case KingdomRules.PetitionKind.Flesh:
			case KingdomRules.PetitionKind.Chrome: return 1;
			case KingdomRules.PetitionKind.Craft:
			default: return 0;
			}
		}

		public static bool IsMet(KingdomRules.PetitionKind Kind, int Target, int StoredWater,
			int Beds, int IdleWorks, int Standing, bool HasShrine)
		{
			switch (Kind)
			{
			case KingdomRules.PetitionKind.Thirst: return StoredWater >= Target;
			case KingdomRules.PetitionKind.Shelter: return Target > 0 && Beds >= Target;
			case KingdomRules.PetitionKind.Craft: return IdleWorks <= Target;
			case KingdomRules.PetitionKind.Peace: return Standing >= Target;
			case KingdomRules.PetitionKind.Memorial: return HasShrine;
			case KingdomRules.PetitionKind.Flesh:
			case KingdomRules.PetitionKind.Chrome: return Target > 0;
			default: return false;
			}
		}

		public static bool CanResolve(PetitionLifecycle State, KingdomRules.PetitionKind Kind,
			int Target, int StoredWater, int Beds, int IdleWorks, int Standing, bool HasShrine)
		{
			return State == PetitionLifecycle.Accepted
				&& IsMet(Kind, Target, StoredWater, Beds, IdleWorks, Standing, HasShrine);
		}

		public static bool TryIssuedTick(KingdomLifecycleOperation Operation, out long Tick)
		{
			Tick = 0L;
			return Operation != null && !string.IsNullOrEmpty(Operation.ArrivalText)
				&& long.TryParse(Operation.ArrivalText, NumberStyles.None,
					CultureInfo.InvariantCulture, out Tick) && Tick >= 0L;
		}

		/// <summary>Every fact spoken by an offer remains in every later action plan.</summary>
		public static bool FrozenSnapshotValid(KingdomLifecycleOperation Operation)
		{
			if (Operation == null || Operation.Lane != KingdomLifecycleLane.Petition
				|| LifecycleOf(Operation) == PetitionLifecycle.None
				|| Operation.Phase == KingdomLifecyclePhase.Quarantined
				|| !Enum.IsDefined(typeof(KingdomRules.PetitionKind), Operation.Kind)
				|| (KingdomRules.PetitionKind)Operation.Kind == KingdomRules.PetitionKind.None
				|| !SnapshotTextValid(Operation.ObjectId, KingdomLifecycleRules.MaxIdChars, false)
				|| !SnapshotTextValid(Operation.Blueprint, KingdomLifecycleRules.MaxNameChars, false)
				|| !SnapshotTextValid(Operation.ObjectName, KingdomLifecycleRules.MaxNameChars, false)
				|| !SnapshotTextValid(Operation.Origin, KingdomLifecycleRules.MaxNameChars, false)
				|| !string.Equals(Operation.Origin, Operation.SettlementId, StringComparison.Ordinal)
				|| !SnapshotTextValid(Operation.ZoneId, KingdomLifecycleRules.MaxNameChars, false)
				|| !SnapshotTextValid(Operation.Faction, KingdomLifecycleRules.MaxNameChars, true)
				|| !SnapshotTextValid(Operation.DisplayFaction,
					KingdomLifecycleRules.MaxNameChars, true)
				|| !SnapshotTextValid(Operation.Detail, KingdomLifecycleRules.MaxTextChars, false)
				|| !EventIdValid(Operation.ObjectMarker)
				|| !TargetValid((KingdomRules.PetitionKind)Operation.Kind, Operation.Target)
				|| !TryIssuedTick(Operation, out long issued) || issued > Operation.CreatedTick
				|| Operation.DepartTick <= 0L) return false;
			PetitionLifecycle state = LifecycleOf(Operation);
			if (state == PetitionLifecycle.Offered)
				return string.Equals(Operation.Creed, ActiveClock, StringComparison.Ordinal);
			if (state == PetitionLifecycle.Accepted)
				return string.Equals(Operation.Creed, ActiveClock, StringComparison.Ordinal)
					|| string.Equals(Operation.Creed, PausedClock, StringComparison.Ordinal);
			return Operation.Creed == ActiveClock || Operation.Creed == OptionClosedClock;
		}

		public static bool SameFrozenSnapshot(KingdomLifecycleOperation Left,
			KingdomLifecycleOperation Right)
		{
			return FrozenSnapshotValid(Left) && FrozenSnapshotValid(Right)
				&& Left.SettlementId == Right.SettlementId && Left.ZoneId == Right.ZoneId
				&& Left.ObjectId == Right.ObjectId && Left.Blueprint == Right.Blueprint
				&& Left.ObjectName == Right.ObjectName && Left.Origin == Right.Origin
				&& Left.Faction == Right.Faction && Left.DisplayFaction == Right.DisplayFaction
				&& Left.Detail == Right.Detail && Left.Kind == Right.Kind
				&& Left.Target == Right.Target && Left.ObjectMarker == Right.ObjectMarker
				&& Left.ArrivalText == Right.ArrivalText;
		}

		public static PetitionLifecycle NormalizeLegacy(PetitionLifecycle State,
			KingdomRules.PetitionKind Kind)
		{
			if ((int)State > (int)PetitionLifecycle.Expired)
				return Kind == KingdomRules.PetitionKind.None
					? PetitionLifecycle.None : PetitionLifecycle.Offered;
			if (IsActive(State) && Kind == KingdomRules.PetitionKind.None)
				return PetitionLifecycle.Expired;
			if ((State == PetitionLifecycle.None || IsTerminal(State))
				&& Kind != KingdomRules.PetitionKind.None) return PetitionLifecycle.Offered;
			return State;
		}

		public static bool TargetNeedsRepair(KingdomRules.PetitionKind Kind, int Target)
		{
			switch (Kind)
			{
			case KingdomRules.PetitionKind.Thirst:
			case KingdomRules.PetitionKind.Shelter:
			case KingdomRules.PetitionKind.Memorial:
			case KingdomRules.PetitionKind.Flesh:
			case KingdomRules.PetitionKind.Chrome: return Target <= 0;
			case KingdomRules.PetitionKind.Peace: return Target == 0;
			default: return false;
			}
		}

		/// <summary>Exact fixed targets stay exact; population-derived targets stay positive.</summary>
		public static bool TargetValid(KingdomRules.PetitionKind Kind, int Target)
		{
			switch (Kind)
			{
			case KingdomRules.PetitionKind.Thirst:
			case KingdomRules.PetitionKind.Shelter: return Target > 0;
			case KingdomRules.PetitionKind.Craft: return Target == 0;
			case KingdomRules.PetitionKind.Peace: return Target == -100;
			case KingdomRules.PetitionKind.Memorial:
			case KingdomRules.PetitionKind.Flesh:
			case KingdomRules.PetitionKind.Chrome: return Target == 1;
			default: return false;
			}
		}

		public static bool EventIdValid(string EventId)
		{
			return !string.IsNullOrWhiteSpace(EventId)
				&& SnapshotTextValid(EventId, KingdomLifecycleRules.MaxIdChars, false);
		}

		public static bool SnapshotTextValid(string Text, int Limit, bool AllowEmpty)
		{
			if (Text == null) return AllowEmpty;
			if (Text.Length == 0) return AllowEmpty;
			if (Limit <= 0 || Text.Length > Limit) return false;
			for (int i = 0; i < Text.Length; i++)
			{
				if (char.IsControl(Text[i]) || char.IsLowSurrogate(Text[i])) return false;
				if (char.IsHighSurrogate(Text[i]))
				{
					if (i + 1 >= Text.Length || !char.IsLowSurrogate(Text[i + 1])) return false;
					i++;
				}
			}
			return true;
		}

		private static KingdomLifecycleAction ActionOf(PetitionLifecycle State)
		{
			switch (State)
			{
			case PetitionLifecycle.Offered: return KingdomLifecycleAction.PetitionOffer;
			case PetitionLifecycle.Accepted: return KingdomLifecycleAction.PetitionAccept;
			case PetitionLifecycle.Declined: return KingdomLifecycleAction.PetitionDecline;
			case PetitionLifecycle.Resolved: return KingdomLifecycleAction.PetitionResolve;
			case PetitionLifecycle.Expired: return KingdomLifecycleAction.PetitionExpire;
			default: return KingdomLifecycleAction.None;
			}
		}
	}
}
