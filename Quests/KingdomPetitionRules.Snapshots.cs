using System;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPetitionRules
	{
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

	}
}
