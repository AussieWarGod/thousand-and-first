using System;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		private void ClearDisabledSavedState()
		{
			if (!KingdomSuccessionRules.TryReadDeathToken(CompletedDeathToken,
				out int completedOrdinal, out long _))
			{
				CompletedDeathToken = ""; SuccessionOrdinal = 0;
				PendingPhase = InterregnumPhase.None;
			}
			else
			{
				SuccessionOrdinal = completedOrdinal; PendingPhase = InterregnumPhase.Reigning;
			}
			PendingDeathToken = ""; PendingDueTick = 0L; PendingRoad = NewsRoad.Seat;
			PendingDays = 0; PendingSelectionReceipt = "";
			CompletedSeatConsequenceToken = ""; ActiveSeatClimbRealmId = "";
			ActiveSeatClimbToken = ""; ActiveSeatKeeperResidentId = 0;
			ActiveSeatKeeperName = ""; LegacySelectionReceiptUnavailable = false;
			PendingSealAccessionToken = ""; PendingSealRiteChronicle = "";
			PendingSealAccessionReady = false; PendingAccessionRepairResidentId = 0;
			PendingAccessionRepairFounderName = ""; PendingAccessionRepairHeirName = "";
			PendingAccessionRepairSettlementId = ""; ClearLegacyAccessionRepairSeated();
			PendingAccessionRepairArrivedTick = 0L; PendingAccessionRepairKeptCreeds = "";
			ClearPendingRiteIdentity();
			if (!KingdomSuccessionRules.TryReadDeathToken(CompletedShrineToken,
				out int _, out long _) || string.IsNullOrEmpty(CompletedShrineObjectId)
				|| string.IsNullOrEmpty(CompletedShrineZoneId))
			{
				CompletedShrineToken = ""; CompletedShrineObjectId = "";
				CompletedShrineZoneId = "";
			}
		}
	}
}
