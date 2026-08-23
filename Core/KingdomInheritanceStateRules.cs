using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ThousandAndFirst
{
	internal enum KingdomInheritancePhase
	{
		Empty = 0,
		Reserved = 1,
		SiteSelected = 2,
		WorldValidated = 3,
		Installed = 4,
		AppliedPendingDurability = 5,
		Committed = 6,
		Refused = 7,
		RepairRequired = 8
	}

	internal enum KingdomInheritanceStartFault
	{
		None = 0,
		MissingStart = 1,
		AlternateWorld = 2,
		TargetIsStart = 3
	}

	internal enum KingdomCommittedRewindAction
	{
		DeferUntilPrimary = 0,
		AdoptDurable = 1,
		AwaitLazyBuilder = 2,
		ReapplyCleanBuiltTarget = 3,
		RepairRequired = 4
	}

	internal enum KingdomInheritanceLoadKind
	{
		Unknown = 0,
		Primary = 1,
		SameGameRollback = 2
	}

	/// <summary>Serialization-only projection. Runtime cleanup authority is valid only if this
	/// entire shape validates as one canonical state.</summary>
	internal sealed class KingdomInheritanceSavedShape
	{
		internal int PhaseValue;

		internal string LegacyText = "";

		internal string ReceiptText = "";

		internal string CommittedReceiptText = "";

		internal string TargetZoneId = "";

		internal string TargetTerrainBlueprint = "";

		internal int TargetTerrainRank = -1;

		internal string SecretId = "";

		internal string SiteName = "";

		internal int ApplyStatus = -1;

		internal int ApplyFault = -1;

		internal string ApplicationMarker = "";

		internal bool ReleasePending = false;

		internal bool OwnsSkipTerrainBuilders;

		internal bool OwnsNoBiomes;

		internal bool OwnsZoneName;

		internal bool RecoveryDisabled;

		internal bool RetryAuthorized = false;
	}

	/// <summary>Async-flow source capture for Harmony's prefix on XRLGame.LoadGame. A postfix
	/// cannot be used: the original is async and sends AfterGameLoaded after awaits.</summary>
	internal static class KingdomInheritanceLoadSourceFlow
	{
		private sealed class LoadSource
		{
			internal readonly string Path;

			internal bool Consumed;

			internal LoadSource(string Path)
			{
				this.Path = Path ?? "";
			}
		}

		private static readonly AsyncLocal<LoadSource> Current = new AsyncLocal<LoadSource>();

		internal static void Record(string Path)
		{
			Current.Value = new LoadSource(Path);
		}

		internal static bool TryConsume(out string Path)
		{
			Path = "";
			LoadSource source = Current.Value;
			if (source == null || source.Consumed)
			{
				return false;
			}
			source.Consumed = true;
			Path = source.Path;
			Current.Value = null;
			return true;
		}

		internal static void Clear()
		{
			Current.Value = null;
		}
	}

	/// <summary>Pure bootstrap gates and monotonic target-save phase transitions.</summary>
	internal static class KingdomInheritanceStateRules
	{
		internal const int MinimumReachableCells = 400;

		internal static bool IsSupportedSerializationHeader(int Magic, int Version,
			int ExpectedMagic, int CurrentVersion)
		{
			return Magic == ExpectedMagic && Version >= 1 && Version <= CurrentVersion;
		}

		internal static bool TryComposeApplicationMarker(KingdomSealRecord Legacy,
			KingdomSealReceipt Receipt, string TargetZoneId, int ReconstructionVersion,
			out string Marker)
		{
			Marker = "";
			if (Legacy == null || Receipt == null || ReconstructionVersion <= 0
				|| Legacy.Status != KingdomSealStatus.Promoted || !Legacy.IsResolved
				|| Receipt.State != KingdomSealReceiptState.Reserved
				|| Receipt.LineageId != Legacy.LineageId || Receipt.LegacyId != Legacy.LegacyId
				|| Receipt.WrittenTick < 0L || !KingdomSealReceipt.ValidId(Receipt.TargetGameId)
				|| string.IsNullOrEmpty(TargetZoneId)
				|| TargetZoneId.Length > KingdomSealRecord.MaxIdChars
				|| !KingdomSealRules.IsToken(TargetZoneId))
			{
				return false;
			}
			Marker = "taf-inherit-v" + ReconstructionVersion.ToString(CultureInfo.InvariantCulture)
				+ "|" + Legacy.LineageId + "|" + Legacy.LegacyId + "|"
				+ Receipt.TargetGameId + "|reserved|"
				+ Receipt.WrittenTick.ToString(CultureInfo.InvariantCulture) + "|" + TargetZoneId;
			return true;
		}

		internal static string ComposeSiteName(KingdomSealRecord Legacy)
		{
			if (Legacy == null)
			{
				return "";
			}
			string name = KingdomSealRules.SanitizeText(Legacy.SettlementName,
				KingdomSealRecord.MaxNameChars);
			if (string.IsNullOrEmpty(name))
			{
				name = "inherited settlement";
			}
			if ((KingdomRules.InheritedState)Legacy.InheritedState
				== KingdomRules.InheritedState.Abandoned)
			{
				name = "abandoned " + name;
			}
			else if ((KingdomRules.InheritedState)Legacy.InheritedState
				== KingdomRules.InheritedState.Ruins)
			{
				name = "ruins of " + name;
			}
			int maximum = KingdomSealRecord.MaxNameChars + 32;
			return name.Length <= maximum ? name : name.Substring(0, maximum);
		}

		internal static bool IsDurableMarkerProof(KingdomInheritancePhase Phase,
			int ApplyStatus, bool TargetBuilt, string PersistedMarker, string RecomputedMarker,
			string ZoneMarker, bool AllowInstalledRecovery)
		{
			bool afterExactApplication = Phase == KingdomInheritancePhase.AppliedPendingDurability
				|| Phase == KingdomInheritancePhase.Committed
				|| (Phase == KingdomInheritancePhase.RepairRequired
					&& (ApplyStatus == (int)KingdomInheritApplyStatus.Applied
						|| ApplyStatus == (int)KingdomInheritApplyStatus.AlreadyApplied));
			bool installedRecovery = AllowInstalledRecovery
				&& Phase == KingdomInheritancePhase.Installed;
			return TargetBuilt && !string.IsNullOrEmpty(RecomputedMarker)
				&& ZoneMarker == RecomputedMarker
				&& ((afterExactApplication && PersistedMarker == RecomputedMarker)
					|| (installedRecovery && (string.IsNullOrEmpty(PersistedMarker)
						|| PersistedMarker == RecomputedMarker)));
		}

		internal static bool ShouldRetryBuild(KingdomInheritApplyStatus Status, int BuildTry,
			bool ExactCleanupSucceeded)
		{
			return Status == KingdomInheritApplyStatus.Failed && BuildTry == 1
				&& ExactCleanupSucceeded;
		}

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

		internal static KingdomInheritanceLoadKind ClassifyExactLoadSource(
			string SourceStem, string SavesRoot,
			string TargetGameId, FileAttributes SavesRootAttributes,
			FileAttributes GameDirectoryAttributes, bool GzipExists,
			FileAttributes GzipAttributes, long GzipLength, bool LegacyExists,
			FileAttributes LegacyAttributes, long LegacyLength, out string Failure)
		{
			Failure = "";
			if (string.IsNullOrWhiteSpace(SourceStem) || string.IsNullOrWhiteSpace(SavesRoot)
				|| !KingdomSealReceipt.ValidId(TargetGameId))
			{
				Failure = "the load source or target game identity was missing";
				return KingdomInheritanceLoadKind.Unknown;
			}
			try
			{
				if (!string.IsNullOrEmpty(Path.GetExtension(SourceStem)))
				{
					Failure = "the load source was not an extension-free save stem";
					return KingdomInheritanceLoadKind.Unknown;
				}
				string root = Path.GetFullPath(SavesRoot)
					.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				string source = Path.GetFullPath(SourceStem)
					.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				string leaf = Path.GetFileName(source);
				KingdomInheritanceLoadKind kind;
				if (leaf == "Primary")
				{
					kind = KingdomInheritanceLoadKind.Primary;
				}
				else if (leaf == "Quick" || leaf == "Checkpoint" || leaf == "Precognition")
				{
					kind = KingdomInheritanceLoadKind.SameGameRollback;
				}
				else
				{
					Failure = "the load source was not a supported exact save stem";
					return KingdomInheritanceLoadKind.Unknown;
				}
				string expected = Path.GetFullPath(Path.Combine(root, TargetGameId, leaf))
					.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				StringComparison comparison = Path.DirectorySeparatorChar == '\\'
					? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
				if (!string.Equals(source, expected, comparison)
					|| !string.Equals(Path.GetFileName(source), leaf, StringComparison.Ordinal)
					|| !string.Equals(Path.GetFileName(Path.GetDirectoryName(source)), TargetGameId,
						StringComparison.Ordinal)
					|| !string.Equals(Path.GetFileName(Path.GetDirectoryName(
						Path.GetDirectoryName(source))), "Saves", StringComparison.Ordinal))
				{
					Failure = "the load source was not an exact supported stem for the target game";
					return KingdomInheritanceLoadKind.Unknown;
				}
				if (!KingdomSealEngineRules.IsDirectDirectory(SavesRootAttributes)
					|| !KingdomSealEngineRules.IsDirectDirectory(GameDirectoryAttributes))
				{
					Failure = "the Saves root or target game directory was not direct";
					return KingdomInheritanceLoadKind.Unknown;
				}
				bool regularSelected = GzipExists
					? KingdomSealEngineRules.IsRegularPrimary(GzipAttributes, GzipLength)
					: LegacyExists && KingdomSealEngineRules.IsRegularPrimary(LegacyAttributes,
						LegacyLength);
				if (!regularSelected)
				{
					Failure = GzipExists
						? "the preferred .sav.gz source was not a nonempty regular file"
						: "neither the exact .sav.gz nor fallback .sav source was regular";
					return KingdomInheritanceLoadKind.Unknown;
				}
				return kind;
			}
			catch (Exception ex)
			{
				Failure = "the load path could not be normalized: " + ex.Message;
				return KingdomInheritanceLoadKind.Unknown;
			}
		}

		internal static bool TryValidateSavedShape(KingdomInheritanceSavedShape Shape,
			string ExpectedTargetGameId, int ReconstructionVersion, out string Failure)
		{
			Failure = "";
			if (Shape == null || !Enum.IsDefined(typeof(KingdomInheritancePhase), Shape.PhaseValue)
				|| ReconstructionVersion <= 0)
			{
				Failure = "the phase or reconstruction version was invalid";
				return false;
			}
			KingdomInheritancePhase phase = (KingdomInheritancePhase)Shape.PhaseValue;
			if (!ValidApplyPair(Shape.ApplyStatus, Shape.ApplyFault, out Failure)
				|| Shape.OwnsSkipTerrainBuilders != Shape.OwnsNoBiomes)
			{
				if (string.IsNullOrEmpty(Failure))
				{
					Failure = "the two reserved zone-property ownership bits disagreed";
				}
				return false;
			}

			bool noAuthority = Empty(Shape.LegacyText) && Empty(Shape.ReceiptText)
				&& Empty(Shape.CommittedReceiptText) && EmptyTarget(Shape)
				&& Shape.ApplyStatus == -1 && Shape.ApplyFault == -1
				&& Empty(Shape.ApplicationMarker) && !Shape.ReleasePending
				&& !Shape.OwnsSkipTerrainBuilders && !Shape.OwnsNoBiomes
				&& !Shape.OwnsZoneName
				&& !Shape.RetryAuthorized;
			if (Shape.RecoveryDisabled)
			{
				if (phase == KingdomInheritancePhase.RepairRequired && noAuthority)
				{
					return true;
				}
				Failure = "disabled recovery retained target cleanup authority";
				return false;
			}
			if (phase == KingdomInheritancePhase.Empty)
			{
				if (noAuthority)
				{
					return true;
				}
				Failure = "an empty state retained inheritance payload";
				return false;
			}
			if (phase == KingdomInheritancePhase.RepairRequired && noAuthority)
			{
				return true;
			}

			KingdomSealRecord legacy;
			KingdomSealReceipt reserved;
			if (!TryCanonicalReservation(Shape.LegacyText, Shape.ReceiptText,
				ExpectedTargetGameId, out legacy, out reserved, out Failure))
			{
				return false;
			}
			bool hasTarget = !Empty(Shape.TargetZoneId);
			if (hasTarget != (!Empty(Shape.TargetTerrainBlueprint)
				&& Shape.TargetTerrainRank >= 0))
			{
				Failure = "the selected target terrain fields were torn";
				return false;
			}
			if (hasTarget && (!KingdomInheritanceSiteRules.IsCanonicalSurfaceZoneId(
				Shape.TargetZoneId)
				|| !KingdomInheritanceSiteRules.IsStableTerrainToken(Shape.TargetTerrainBlueprint)
				|| Shape.TargetTerrainRank > KingdomInheritanceSiteRules.MaxTerrainRank))
			{
				Failure = "the selected target was not a canonical safe surface identity";
				return false;
			}
			bool hasDiscovery = !Empty(Shape.SecretId) || !Empty(Shape.SiteName);
			if (hasDiscovery && (Shape.SecretId != "taf.inherit." + legacy.LegacyId
				|| Shape.SiteName != ComposeSiteName(legacy) || !hasTarget))
			{
				Failure = "the saved discovery identity was torn or did not name the legacy";
				return false;
			}
			if (Shape.OwnsSkipTerrainBuilders && (!hasTarget || !hasDiscovery))
			{
				Failure = "zone-property cleanup authority lacked exact target discovery metadata";
				return false;
			}
			if (Shape.OwnsZoneName && (!hasTarget || !hasDiscovery))
			{
				Failure = "zone-name ownership lacked an exact target and discovery identity";
				return false;
			}
			if (Shape.RetryAuthorized && (phase != KingdomInheritancePhase.RepairRequired
				|| !Shape.OwnsSkipTerrainBuilders || Shape.ApplyStatus !=
					(int)KingdomInheritApplyStatus.Failed))
			{
				Failure = "direct repair authority lacked a failed exact-owned repair state";
				return false;
			}

			string expectedMarker = "";
			if (hasTarget && !TryComposeApplicationMarker(legacy, reserved, Shape.TargetZoneId,
				ReconstructionVersion, out expectedMarker))
			{
				Failure = "the target marker could not be recomputed";
				return false;
			}
			if (!Empty(Shape.ApplicationMarker) && Shape.ApplicationMarker != expectedMarker)
			{
				Failure = "the saved application marker was not exact";
				return false;
			}

			KingdomSealReceipt committed = null;
			if (!Empty(Shape.CommittedReceiptText)
				&& (!KingdomSealReceipt.TryParse(Shape.CommittedReceiptText, out committed)
					|| committed == null || committed.Compose() != Shape.CommittedReceiptText
					|| committed.State != KingdomSealReceiptState.Committed
					|| committed.LineageId != reserved.LineageId
					|| committed.LegacyId != reserved.LegacyId
					|| committed.TargetGameId != reserved.TargetGameId
					|| committed.WrittenTick < reserved.WrittenTick))
			{
				Failure = "the committed receipt was not a canonical monotone exact receipt";
				return false;
			}

			switch (phase)
			{
			case KingdomInheritancePhase.Reserved:
				return Require(!hasTarget && !hasDiscovery && Shape.ApplyStatus == -1
					&& Empty(Shape.ApplicationMarker) && Empty(Shape.CommittedReceiptText)
					&& !Shape.ReleasePending && !Shape.OwnsSkipTerrainBuilders
					&& !Shape.OwnsZoneName,
					"reserved state retained later-phase fields", out Failure);
			case KingdomInheritancePhase.SiteSelected:
			case KingdomInheritancePhase.WorldValidated:
				return Require(hasTarget && !hasDiscovery && Shape.ApplyStatus == -1
					&& Empty(Shape.ApplicationMarker) && Empty(Shape.CommittedReceiptText)
					&& !Shape.ReleasePending && !Shape.OwnsSkipTerrainBuilders
					&& !Shape.OwnsZoneName,
					"selected state had an impossible artifact or result shape", out Failure);
			case KingdomInheritancePhase.Installed:
				return Require(hasTarget && hasDiscovery && Shape.ApplyStatus == -1
					&& Empty(Shape.ApplicationMarker) && Empty(Shape.CommittedReceiptText)
					&& !Shape.ReleasePending && Shape.OwnsSkipTerrainBuilders
					&& Shape.OwnsZoneName,
					"installed state lacked exact owned artifact fields", out Failure);
			case KingdomInheritancePhase.AppliedPendingDurability:
				return Require(hasTarget && hasDiscovery && IsSuccessfulApply(Shape.ApplyStatus,
					Shape.ApplyFault) && Shape.ApplicationMarker == expectedMarker
					&& Empty(Shape.CommittedReceiptText) && !Shape.ReleasePending
					&& Shape.OwnsSkipTerrainBuilders && Shape.OwnsZoneName,
					"pending state lacked exact initial-application proof", out Failure);
			case KingdomInheritancePhase.Committed:
				return Require(hasTarget && hasDiscovery && IsSuccessfulApply(Shape.ApplyStatus,
					Shape.ApplyFault) && Shape.ApplicationMarker == expectedMarker
					&& committed != null && !Shape.ReleasePending
					&& Shape.OwnsSkipTerrainBuilders && Shape.OwnsZoneName,
					"committed state lacked exact marker, receipt, or ownership proof", out Failure);
			case KingdomInheritancePhase.Refused:
				return Require(Empty(Shape.CommittedReceiptText)
					&& Empty(Shape.ApplicationMarker) && !Shape.ReleasePending
					&& !Shape.OwnsSkipTerrainBuilders && !Shape.OwnsZoneName
					&& (!hasTarget || (Shape.TargetTerrainRank >= 0
						&& (!hasDiscovery || Shape.SecretId == "taf.inherit." + legacy.LegacyId)))
					&& (Shape.ApplyStatus == -1 || (Shape.ApplyStatus !=
						(int)KingdomInheritApplyStatus.Applied
						&& Shape.ApplyStatus != (int)KingdomInheritApplyStatus.AlreadyApplied)),
					"refused state retained commit, release, or cleanup authority", out Failure);
			case KingdomInheritancePhase.RepairRequired:
				return Require((!Shape.ReleasePending || (Empty(Shape.ApplicationMarker)
					&& Empty(Shape.CommittedReceiptText) && !Shape.RetryAuthorized
					&& Shape.ApplyStatus != (int)KingdomInheritApplyStatus.Applied
					&& Shape.ApplyStatus != (int)KingdomInheritApplyStatus.AlreadyApplied))
					&& (!Shape.OwnsSkipTerrainBuilders || hasDiscovery)
					&& (Empty(Shape.CommittedReceiptText) || committed != null),
					"repair state retained unproved release or cleanup authority", out Failure);
			default:
				Failure = "the phase was unsupported";
				return false;
			}
		}

		private static bool TryCanonicalReservation(string LegacyText, string ReceiptText,
			string ExpectedTargetGameId, out KingdomSealRecord Legacy,
			out KingdomSealReceipt Reserved, out string Failure)
		{
			Legacy = null;
			Reserved = null;
			Failure = "";
			KingdomSealFault fault;
			string detail;
			if (Empty(LegacyText) || Empty(ReceiptText)
				|| !KingdomSealRecord.TryParse(LegacyText, out Legacy, out fault, out detail)
				|| Legacy == null || Legacy.Compose() != LegacyText
				|| Legacy.Status != KingdomSealStatus.Promoted || !Legacy.IsResolved
				|| !KingdomSealReceipt.TryParse(ReceiptText, out Reserved)
				|| Reserved == null || Reserved.Compose() != ReceiptText
				|| Reserved.State != KingdomSealReceiptState.Reserved
				|| Reserved.LineageId != Legacy.LineageId || Reserved.LegacyId != Legacy.LegacyId
				|| Reserved.TargetGameId != ExpectedTargetGameId)
			{
				Failure = "the legacy and reserved receipt were not canonical for this target game";
				Legacy = null;
				Reserved = null;
				return false;
			}
			return true;
		}

		private static bool ValidApplyPair(int Status, int Fault, out string Failure)
		{
			Failure = "";
			if (Status == -1 || Fault == -1)
			{
				if (Status == -1 && Fault == -1)
				{
					return true;
				}
				Failure = "the application status and fault were torn";
				return false;
			}
			if (!Enum.IsDefined(typeof(KingdomInheritApplyStatus), Status)
				|| !Enum.IsDefined(typeof(KingdomInheritApplyFault), Fault))
			{
				Failure = "the application status or fault was outside its enum";
				return false;
			}
			bool success = Status == (int)KingdomInheritApplyStatus.Applied
				|| Status == (int)KingdomInheritApplyStatus.AlreadyApplied;
			if (success != (Fault == (int)KingdomInheritApplyFault.None))
			{
				Failure = "the application status and fault semantics disagreed";
				return false;
			}
			return true;
		}

		private static bool IsSuccessfulApply(int Status, int Fault)
		{
			return (Status == (int)KingdomInheritApplyStatus.Applied
				|| Status == (int)KingdomInheritApplyStatus.AlreadyApplied)
				&& Fault == (int)KingdomInheritApplyFault.None;
		}

		private static bool EmptyTarget(KingdomInheritanceSavedShape Shape)
		{
			return Empty(Shape.TargetZoneId) && Empty(Shape.TargetTerrainBlueprint)
				&& Shape.TargetTerrainRank == -1 && Empty(Shape.SecretId) && Empty(Shape.SiteName);
		}

		private static bool Empty(string Value)
		{
			return string.IsNullOrEmpty(Value);
		}

		private static bool Require(bool Condition, string Detail, out string Failure)
		{
			Failure = Condition ? "" : Detail;
			return Condition;
		}

		internal static bool ShouldOffer(string GameMode, bool TutorialActive)
		{
			return !TutorialActive
				&& !string.Equals(GameMode, "Tutorial", StringComparison.Ordinal)
				&& !string.Equals(GameMode, "Daily", StringComparison.Ordinal);
		}

		internal static KingdomInheritanceStartFault ValidateStart(string TargetZoneId,
			string StartWorld, string StartZoneId)
		{
			if (string.IsNullOrEmpty(StartWorld) || string.IsNullOrEmpty(StartZoneId))
			{
				return KingdomInheritanceStartFault.MissingStart;
			}
			if (!string.Equals(StartWorld, KingdomInheritanceSiteRules.WorldId, StringComparison.Ordinal))
			{
				return KingdomInheritanceStartFault.AlternateWorld;
			}
			if (string.Equals(TargetZoneId, StartZoneId, StringComparison.Ordinal))
			{
				return KingdomInheritanceStartFault.TargetIsStart;
			}
			return KingdomInheritanceStartFault.None;
		}

		internal static bool CanTransition(KingdomInheritancePhase From, KingdomInheritancePhase To)
		{
			if (From == To)
			{
				return true;
			}
			switch (From)
			{
			case KingdomInheritancePhase.Empty:
				return To == KingdomInheritancePhase.Reserved
					|| To == KingdomInheritancePhase.RepairRequired;
			case KingdomInheritancePhase.Reserved:
				return To == KingdomInheritancePhase.SiteSelected
					|| To == KingdomInheritancePhase.Refused
					|| To == KingdomInheritancePhase.RepairRequired;
			case KingdomInheritancePhase.SiteSelected:
				return To == KingdomInheritancePhase.WorldValidated
					|| To == KingdomInheritancePhase.Refused
					|| To == KingdomInheritancePhase.RepairRequired;
			case KingdomInheritancePhase.WorldValidated:
				return To == KingdomInheritancePhase.Installed
					|| To == KingdomInheritancePhase.Refused
					|| To == KingdomInheritancePhase.RepairRequired;
			case KingdomInheritancePhase.Installed:
				return To == KingdomInheritancePhase.AppliedPendingDurability
					|| To == KingdomInheritancePhase.Committed
					|| To == KingdomInheritancePhase.Refused
					|| To == KingdomInheritancePhase.RepairRequired;
			case KingdomInheritancePhase.AppliedPendingDurability:
				return To == KingdomInheritancePhase.Committed
					|| To == KingdomInheritancePhase.RepairRequired;
			case KingdomInheritancePhase.RepairRequired:
				return To == KingdomInheritancePhase.AppliedPendingDurability
					|| To == KingdomInheritancePhase.Committed
					|| To == KingdomInheritancePhase.Refused;
			default:
				return false;
			}
		}
	}
}
