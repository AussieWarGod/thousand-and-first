using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ThousandAndFirst
{
	/// <summary>Pure bootstrap gates and monotonic target-save phase transitions.</summary>
	internal static partial class KingdomInheritanceStateRules
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

	}
}
