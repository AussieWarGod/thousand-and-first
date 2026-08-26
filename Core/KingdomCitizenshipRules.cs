using System;

namespace ThousandAndFirst
{
	/// <summary>Durable ownership state for the one realm-faction slot TAF may write.</summary>
	public enum KingdomCitizenshipPhase
	{
		None = 0,
		Prepared = 1,
		Applied = 2,
		LegacyPriorUnknown = 3,
		Diverged = 4,
		Removed = 5
	}

	/// <summary>What occupied the owned base-allegiance slot before enrolment.</summary>
	public enum KingdomCitizenshipPriorKind
	{
		Absent = 0,
		Present = 1,
		Unknown = 2
	}

	public enum KingdomCitizenshipMutation
	{
		Quarantine = 0,
		ApplyOwnedValue = 1,
		ConfirmApplied = 2,
		RestorePriorValue = 3,
		RemoveOwnedValue = 4,
		ConfirmRemoved = 5
	}

	public enum KingdomCitizenshipEnrollmentReason
	{
		Arrival = 1,
		GuestAdoption = 2,
		LegacyObservation = 3,
		Repair = 4
	}

	public enum KingdomCitizenshipRemovalReason
	{
		Emigration = 1,
		Death = 2,
		Accession = 3,
		ForeignTransfer = 4
	}

	/// <summary>
	/// Pure compare-and-swap rules for citizenship. They deliberately know nothing about an
	/// AllegianceSet: the engine edge supplies only the exact value (or absence) of TAF's one
	/// base slot. Every other base slot, temporary layer, flag and Brain field is outside this
	/// state machine and therefore cannot be mutated by it.
	/// </summary>
	public static class KingdomCitizenshipRules
	{
		public const int CurrentReceiptVersion = 1;
		public const int RealmMembership = 100;

		public static KingdomCitizenshipMutation JudgeApply(KingdomCitizenshipPhase Phase,
			KingdomCitizenshipPriorKind PriorKind, int PriorValue, bool CurrentPresent,
			int CurrentValue, int AppliedValue)
		{
			if (AppliedValue != RealmMembership)
				return KingdomCitizenshipMutation.Quarantine;
			if (Phase == KingdomCitizenshipPhase.Applied
				|| Phase == KingdomCitizenshipPhase.LegacyPriorUnknown)
			{
				return CurrentPresent && CurrentValue == AppliedValue
					? KingdomCitizenshipMutation.ConfirmApplied
					: KingdomCitizenshipMutation.Quarantine;
			}
			if (Phase != KingdomCitizenshipPhase.Prepared
				|| PriorKind == KingdomCitizenshipPriorKind.Unknown)
				return KingdomCitizenshipMutation.Quarantine;

			if (!MatchesPrior(PriorKind, PriorValue, CurrentPresent, CurrentValue))
				return KingdomCitizenshipMutation.Quarantine;
			// A prior value of 100 makes pre- and post-state identical. Confirming is exact: removal
			// will restore that same value and no foreign slot ever changes.
			return CurrentPresent && CurrentValue == AppliedValue
				? KingdomCitizenshipMutation.ConfirmApplied
				: KingdomCitizenshipMutation.ApplyOwnedValue;
		}

		public static KingdomCitizenshipMutation JudgeRemove(KingdomCitizenshipPhase Phase,
			KingdomCitizenshipPriorKind PriorKind, int PriorValue, bool CurrentPresent,
			int CurrentValue, int AppliedValue)
		{
			if (AppliedValue != RealmMembership)
				return KingdomCitizenshipMutation.Quarantine;
			if (Phase == KingdomCitizenshipPhase.Removed)
				return KingdomCitizenshipMutation.ConfirmRemoved;
			if (Phase == KingdomCitizenshipPhase.Diverged
				|| Phase == KingdomCitizenshipPhase.None)
				return KingdomCitizenshipMutation.Quarantine;

			if (Phase == KingdomCitizenshipPhase.Prepared)
				return PriorKind != KingdomCitizenshipPriorKind.Unknown
					&& MatchesPrior(PriorKind, PriorValue, CurrentPresent, CurrentValue)
						? KingdomCitizenshipMutation.ConfirmRemoved
						: KingdomCitizenshipMutation.Quarantine;

			if (CurrentPresent && CurrentValue == AppliedValue)
			{
				return PriorKind == KingdomCitizenshipPriorKind.Present
					? KingdomCitizenshipMutation.RestorePriorValue
					: KingdomCitizenshipMutation.RemoveOwnedValue;
			}

			return KingdomCitizenshipMutation.Quarantine;
		}

		public static bool MatchesPrior(KingdomCitizenshipPriorKind PriorKind, int PriorValue,
			bool CurrentPresent, int CurrentValue)
		{
			return PriorKind == KingdomCitizenshipPriorKind.Absent
				? !CurrentPresent
				: PriorKind == KingdomCitizenshipPriorKind.Present
					&& CurrentPresent && CurrentValue == PriorValue;
		}

		/// <summary>Exact slot state left by a completed removal. Legacy-unknown can prove
		/// only absence: its destructive predecessor did not retain any prior value to restore.</summary>
		public static bool MatchesRemovalPost(KingdomCitizenshipPriorKind PriorKind,
			int PriorValue, bool CurrentPresent, int CurrentValue)
		{
			return PriorKind == KingdomCitizenshipPriorKind.Present
				? CurrentPresent && CurrentValue == PriorValue
				: (PriorKind == KingdomCitizenshipPriorKind.Absent
					|| PriorKind == KingdomCitizenshipPriorKind.Unknown) && !CurrentPresent;
		}

		public static bool ValidReceiptShape(KingdomCitizenshipPhase Phase,
			KingdomCitizenshipPriorKind PriorKind, int AppliedValue, int EnrollmentReason,
			int RemovalReason, long AppliedTick, long RemovedTick)
		{
			if (AppliedValue != RealmMembership || AppliedTick < 0L || RemovedTick < 0L
				|| !ValidPrior(PriorKind) || !ValidEnrollmentReason(EnrollmentReason)) return false;
			bool legacy = EnrollmentReason == (int)KingdomCitizenshipEnrollmentReason.LegacyObservation;
			if ((PriorKind == KingdomCitizenshipPriorKind.Unknown) != legacy) return false;
			switch (Phase)
			{
			case KingdomCitizenshipPhase.Prepared:
			case KingdomCitizenshipPhase.Applied:
				return !legacy && RemovalReason == 0 && RemovedTick == 0L;
			case KingdomCitizenshipPhase.LegacyPriorUnknown:
				return legacy && RemovalReason == 0 && RemovedTick == 0L;
			case KingdomCitizenshipPhase.Diverged:
				return RemovalReason == 0 && RemovedTick == 0L;
			case KingdomCitizenshipPhase.Removed:
				return ValidRemovalReason(RemovalReason);
			default:
				return false;
			}
		}

		private static bool ValidPrior(KingdomCitizenshipPriorKind value)
		{
			return value == KingdomCitizenshipPriorKind.Absent
				|| value == KingdomCitizenshipPriorKind.Present
				|| value == KingdomCitizenshipPriorKind.Unknown;
		}

		private static bool ValidEnrollmentReason(int value)
		{
			return value >= (int)KingdomCitizenshipEnrollmentReason.Arrival
				&& value <= (int)KingdomCitizenshipEnrollmentReason.Repair;
		}

		private static bool ValidRemovalReason(int value)
		{
			return value >= (int)KingdomCitizenshipRemovalReason.Emigration
				&& value <= (int)KingdomCitizenshipRemovalReason.ForeignTransfer;
		}
	}
}
