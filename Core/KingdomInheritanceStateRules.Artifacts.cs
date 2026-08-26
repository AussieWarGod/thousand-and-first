using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritanceStateRules
	{
		internal static bool IsExactSiteBuilder(string Class, string LegacyId,
			string TargetGameId, string TargetZoneId, int ReconstructionVersion,
			string ExpectedLegacyId, string ExpectedTargetGameId, string ExpectedTargetZoneId,
			int ExpectedReconstructionVersion)
		{
			return Class == "KingdomInheritedSiteBuilder"
				&& LegacyId == ExpectedLegacyId && TargetGameId == ExpectedTargetGameId
				&& TargetZoneId == ExpectedTargetZoneId
				&& ReconstructionVersion == ExpectedReconstructionVersion;
		}

		internal static bool IsExactLocationFinder(string Class, string SecretId, int Value,
			string ExpectedSecretId)
		{
			return Class == "AddLocationFinder" && SecretId == ExpectedSecretId && Value == 1;
		}

		internal static bool IsExactLocationFinderBuilder(string Class, string LegacyId,
			string TargetGameId, string TargetZoneId, int ReconstructionVersion,
			string ExpectedLegacyId, string ExpectedTargetGameId, string ExpectedTargetZoneId,
			int ExpectedReconstructionVersion)
		{
			return Class == "KingdomInheritanceLocationFinderBuilder"
				&& LegacyId == ExpectedLegacyId && TargetGameId == ExpectedTargetGameId
				&& TargetZoneId == ExpectedTargetZoneId
				&& ReconstructionVersion == ExpectedReconstructionVersion;
		}

		internal static bool IsExactZoneNameFootprint(string Name, bool HasContext,
			string Context, bool HasIndefinite, string Indefinite, bool HasDefinite,
			string Definite, bool HasProper, bool Proper, string ExpectedName)
		{
			return !string.IsNullOrEmpty(ExpectedName) && Name == ExpectedName
				&& HasContext && Context == ""
				&& HasIndefinite && Indefinite == ""
				&& HasDefinite && Definite == ""
				&& HasProper && Proper;
		}

		internal static bool IsCompatibleOwnedZoneNameSubset(bool HasName, string Name,
			bool HasContext, string Context, bool HasIndefinite, string Indefinite,
			bool HasDefinite, string Definite, bool HasProper, bool Proper,
			string ExpectedName)
		{
			return !string.IsNullOrEmpty(ExpectedName)
				&& (!HasName || Name == ExpectedName)
				&& (!HasContext || Context == "")
				&& (!HasIndefinite || Indefinite == "")
				&& (!HasDefinite || Definite == "")
				&& (!HasProper || Proper);
		}

		internal static bool IsUsableOwnedMapNote(bool Exists, bool ExactZone,
			bool AttributesPresent,
			string Category, string Text, string ExpectedCategory, string ExpectedText)
		{
			return Exists && ExactZone && AttributesPresent
				&& !string.IsNullOrEmpty(ExpectedCategory)
				&& !string.IsNullOrEmpty(ExpectedText)
				&& Category == ExpectedCategory && Text == ExpectedText;
		}

		internal static bool CanClearZoneNameOwnership(bool HasAnyFootprint)
		{
			return !HasAnyFootprint;
		}

		internal static bool ShouldAttemptFallbackArtifactCleanup(bool ZoneQuarantined,
			bool ProfileCommitted)
		{
			return ZoneQuarantined && !ProfileCommitted;
		}

		internal static bool MustPersistFallbackReleaseIntent(bool ZoneQuarantined,
			bool ProfileCommitted, bool ArtifactsClean)
		{
			return ZoneQuarantined && !ProfileCommitted && !ArtifactsClean;
		}

		internal static bool MeetsReachability(int Reachable)
		{
			return Reachable >= MinimumReachableCells;
		}

		internal static bool RetainsDurableApplicationCandidate(int ApplyStatus, int ApplyFault,
			string ApplicationMarker)
		{
			return IsSuccessfulApply(ApplyStatus, ApplyFault)
				&& !string.IsNullOrEmpty(ApplicationMarker);
		}

		internal static bool CanTerminalizeHiddenFallback(int EntryReachable,
			int OtherReachableComponent)
		{
			// OtherReachableComponent is diagnostic only. An isolated large pocket does not prove
			// the actual (0,0) entry can reach the safe envelope.
			return MeetsReachability(EntryReachable);
		}

		internal static bool CanAuthorizeDirectRepair(bool ExactBuilders, int ZoneParts,
			bool Pristine)
		{
			return ExactBuilders && ZoneParts == 0 && Pristine;
		}

		internal static bool CanClaimEmergencyOwnership(int BuilderCount,
			int ExactSiteBuilderCount, int ExactFinderBuilderCount, bool ExactBuilderPriorities,
			bool ExactSkipTerrainBuilders, bool ExactNoBiomes)
		{
			// Installation requires both properties and builder slots to be absent. One exact
			// payload pair therefore proves our ownership even if another extension later adds
			// unrelated builders. Duplicated payload builders are ambiguous and fail closed.
			return BuilderCount >= 2 && ExactSiteBuilderCount == 1 && ExactFinderBuilderCount == 1
				&& ExactBuilderPriorities
				&& ExactSkipTerrainBuilders && ExactNoBiomes;
		}

		internal static bool PreservesApplicationProofDuringDiscoveryRepair(
			KingdomInheritancePhase Phase, int ApplyStatus, int ApplyFault,
			string ApplicationMarker)
		{
			return (Phase == KingdomInheritancePhase.AppliedPendingDurability
				|| Phase == KingdomInheritancePhase.Committed)
				&& RetainsDurableApplicationCandidate(ApplyStatus, ApplyFault,
					ApplicationMarker);
		}

		internal static bool CanRetryUnvalidatedApplication(int ApplyStatus, int ApplyFault,
			bool RetryAuthorized, string PersistedMarker, string ZoneMarker,
			string ExpectedMarker)
		{
			return ApplyStatus == (int)KingdomInheritApplyStatus.Failed
				&& ApplyFault == (int)KingdomInheritApplyFault.PartialApplication
				&& RetryAuthorized && !string.IsNullOrEmpty(ExpectedMarker)
				&& PersistedMarker == ExpectedMarker && ZoneMarker == ExpectedMarker;
		}

		internal static bool CanRegenerateAfterEmergencyCleanup(bool ExactBuildersAbsent,
			bool SkipTerrainBuildersAbsent, bool NoBiomesAbsent)
		{
			return ExactBuildersAbsent && SkipTerrainBuildersAbsent && NoBiomesAbsent;
		}

		internal static KingdomCommittedRewindAction DecideCommittedRewind(
			KingdomInheritanceLoadKind LoadKind, bool ReceiptAlreadyCommitted,
			bool DurableProof, bool TargetBuilt, bool MarkerEmpty, bool ExactLazyBuilders,
			bool CleanReapplyPrecondition)
		{
			// A Reserved receipt can only be spent after an exact Primary load. Once the
			// profile already says Committed, a proved same-game Quick/Checkpoint/Precognition
			// rollback may be repaired immediately: there is no second profile transition.
			if (LoadKind == KingdomInheritanceLoadKind.Unknown
				|| (!ReceiptAlreadyCommitted && LoadKind != KingdomInheritanceLoadKind.Primary))
			{
				return KingdomCommittedRewindAction.DeferUntilPrimary;
			}
			if (DurableProof)
			{
				return KingdomCommittedRewindAction.AdoptDurable;
			}
			if (!TargetBuilt)
			{
				return ExactLazyBuilders ? KingdomCommittedRewindAction.AwaitLazyBuilder
					: KingdomCommittedRewindAction.RepairRequired;
			}
			return MarkerEmpty && CleanReapplyPrecondition
				? KingdomCommittedRewindAction.ReapplyCleanBuiltTarget
				: KingdomCommittedRewindAction.RepairRequired;
		}

		internal static bool ProfileReceiptBlocksRelease(KingdomSealReceiptState State)
		{
			return State == KingdomSealReceiptState.Committed;
		}

	}
}
