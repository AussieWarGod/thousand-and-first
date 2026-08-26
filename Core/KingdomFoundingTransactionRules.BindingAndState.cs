using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransactionRules
	{

		/// <summary>
		/// Exact durable identity binding. First founding binds its intended realm before the system
		/// can name that realm; every later kind requires the live system realm immediately.
		/// </summary>
		public static bool ReceiptBindingMatches(string BoundOwnerNonce, string CurrentOwnerNonce,
			KingdomFoundingOwnerKind OwnerKind, string TransactionID,
			string BoundRealmFaction, string PendingName, string CurrentRealmFaction,
			KingdomFoundingKind Kind)
		{
			if (OwnerKind != KingdomFoundingOwnerKind.Basin ||
				!IsNonce(BoundOwnerNonce) ||
				!string.Equals(BoundOwnerNonce, CurrentOwnerNonce, StringComparison.Ordinal) ||
				string.IsNullOrEmpty(TransactionID) || string.IsNullOrEmpty(BoundRealmFaction))
			{
				return false;
			}
			if (Kind == KingdomFoundingKind.FirstCity)
			{
				return string.Equals(BoundRealmFaction, PendingName, StringComparison.Ordinal) &&
					(string.IsNullOrEmpty(CurrentRealmFaction) ||
					 string.Equals(BoundRealmFaction, CurrentRealmFaction,
						 StringComparison.Ordinal));
			}
			return (Kind == KingdomFoundingKind.SecondCity ||
				Kind == KingdomFoundingKind.VillageCharter) &&
				string.Equals(BoundRealmFaction, CurrentRealmFaction,
					StringComparison.Ordinal);
		}

		/// <summary>Legacy overload retained for old callers/tests; object IDs are not copy-safe.</summary>
		public static bool ReceiptBindingMatches(string BoundBasinID, string CurrentBasinID,
			string TransactionID, string BoundRealmFaction, string PendingName,
			string CurrentRealmFaction, KingdomFoundingKind Kind)
		{
			return !string.IsNullOrEmpty(BoundBasinID) &&
				string.Equals(BoundBasinID, CurrentBasinID, StringComparison.Ordinal) &&
				!string.IsNullOrEmpty(TransactionID) &&
				!string.IsNullOrEmpty(BoundRealmFaction) &&
				(Kind == KingdomFoundingKind.FirstCity
					? string.Equals(BoundRealmFaction, PendingName, StringComparison.Ordinal) &&
					  (string.IsNullOrEmpty(CurrentRealmFaction) ||
					   string.Equals(BoundRealmFaction, CurrentRealmFaction,
						   StringComparison.Ordinal))
					: (Kind == KingdomFoundingKind.SecondCity ||
					   Kind == KingdomFoundingKind.VillageCharter) &&
					  string.Equals(BoundRealmFaction, CurrentRealmFaction,
						  StringComparison.Ordinal));
		}

		/// <summary>
		/// A second projection may start only from one exact city and no Away seat. Once published,
		/// only its own exact seated city may resume. This prevents a stale receipt from replacing an
		/// unrelated second city.
		/// </summary>
		public static bool SecondRecoveryCanProject(int SettlementCount, int MaximumSettlements,
			bool AwayIsNull, bool TargetIsExactSeat, bool TargetIsExactAway,
			bool AlreadyPublished)
		{
			if (MaximumSettlements < 2 || SettlementCount < 0 ||
				SettlementCount > MaximumSettlements)
			{
				return false;
			}
			if (AlreadyPublished)
			{
				return !AwayIsNull && SettlementCount == MaximumSettlements &&
					(TargetIsExactSeat ^ TargetIsExactAway);
			}
			return !TargetIsExactSeat && !TargetIsExactAway && AwayIsNull &&
				SettlementCount == MaximumSettlements - 1;
		}

		public static bool SecondRecoveryCanProject(int SettlementCount, int MaximumSettlements,
			bool AwayIsNull, bool TargetIsExactSeat, bool AlreadyPublished)
		{
			return SecondRecoveryCanProject(SettlementCount, MaximumSettlements, AwayIsNull,
				TargetIsExactSeat, TargetIsExactAway: false, AlreadyPublished);
		}

		/// <summary>Exact liquid algebra for a basin receipt, independent of current vessel.</summary>
		public static bool WaterAlgebraValid(int OriginalVolume, int OriginalMaxVolume,
			int CommittedVolume, int CommittedMaxVolume, int Cost,
			bool OriginalIsPureWater, bool CommittedComponentsValid)
		{
			return OriginalVolume >= Cost && Cost > 0 && OriginalMaxVolume >= OriginalVolume &&
				OriginalMaxVolume >= 0 && CommittedMaxVolume == OriginalMaxVolume &&
				CommittedVolume == OriginalVolume - Cost && CommittedVolume >= 0 &&
				OriginalIsPureWater && CommittedComponentsValid;
		}

		/// <summary>Phase-specific vessel proof. None is clearable only at original; paid phases
		/// require committed. RecoveryRequired is always quarantine. Complete remains paid until
		/// live completion is observed.</summary>
		public static KingdomFoundingReceiptNormalization ValidatePhaseState(
			KingdomFoundingPhase Phase, bool PayloadValid, bool CurrentMatchesOriginal,
			bool CurrentMatchesCommitted, bool CompletionObserved)
		{
			if (!IsKnownPhase(Phase) || !PayloadValid)
			{
				return KingdomFoundingReceiptNormalization.Quarantine;
			}
			switch (Phase)
			{
			case KingdomFoundingPhase.None:
				return CurrentMatchesOriginal
					? KingdomFoundingReceiptNormalization.ClearStaged
					: KingdomFoundingReceiptNormalization.Quarantine;
			case KingdomFoundingPhase.WaterCommitted:
			case KingdomFoundingPhase.PublicationCommitted:
				return CurrentMatchesCommitted
					? KingdomFoundingReceiptNormalization.Pending
					: KingdomFoundingReceiptNormalization.Quarantine;
			case KingdomFoundingPhase.RecoveryRequired:
				return KingdomFoundingReceiptNormalization.Quarantine;
			case KingdomFoundingPhase.Complete:
				return CurrentMatchesCommitted && CompletionObserved
					? KingdomFoundingReceiptNormalization.ClearStaged
					: KingdomFoundingReceiptNormalization.Quarantine;
			default:
				return KingdomFoundingReceiptNormalization.Quarantine;
			}
		}

	}
}
