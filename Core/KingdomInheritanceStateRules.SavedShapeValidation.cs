using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritanceStateRules
	{
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

	}
}
