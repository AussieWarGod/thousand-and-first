namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		/// <summary>Read-only realm-removal fence. Completed history is not a blocker; any torn,
		/// pending, or in-callback succession authority is. The caller must never clear these rows.</summary>
		internal bool TryDescribeRealmRemovalBlocker(out string Detail)
		{
			Detail = null;
			if (LoadFailed)
			{
				Detail = "succession save authority failed to load"; return true;
			}
			if (AccessionOwnershipCommitted || DeathChroniclePublished)
			{
				Detail = "a founder-death callback is crossing accession authority"; return true;
			}
			if (PendingAccessionRepairResidentId != 0
				|| !string.IsNullOrEmpty(PendingAccessionRepairFounderName)
				|| !string.IsNullOrEmpty(PendingAccessionRepairHeirName)
				|| !string.IsNullOrEmpty(PendingAccessionRepairSettlementId)
				|| ReadLegacyAccessionRepairSeated() || PendingAccessionRepairArrivedTick != 0L
				|| !string.IsNullOrEmpty(PendingAccessionRepairKeptCreeds))
			{
				Detail = "an exact resident accession repair is pending or torn"; return true;
			}
			if (!string.IsNullOrEmpty(PendingSealAccessionToken)
				|| !string.IsNullOrEmpty(PendingSealRiteChronicle)
				|| PendingSealAccessionReady)
			{
				Detail = "an exact profile-accession seal is pending or torn"; return true;
			}
			if (!string.IsNullOrEmpty(PendingDeathToken)
				|| PendingRiteStage != MourningRiteStage.None
				|| PendingPhase != InterregnumPhase.None
					&& PendingPhase != InterregnumPhase.Reigning)
			{
				Detail = "an exact founder death or mourning rite is pending"; return true;
			}
			if (!string.IsNullOrEmpty(PendingSelectionReceipt))
			{
				Detail = "a succession selection consequence is pending"; return true;
			}
			if (!string.IsNullOrEmpty(ActiveSeatClimbRealmId)
				|| !string.IsNullOrEmpty(ActiveSeatClimbToken)
				|| ActiveSeatKeeperResidentId != 0 || !string.IsNullOrEmpty(ActiveSeatKeeperName))
			{
				Detail = "a chosen-seat consequence is in flight"; return true;
			}
			if (!string.IsNullOrEmpty(PendingConfigurationChronicle))
			{
				Detail = "a succession-custom Chronicle publication is pending"; return true;
			}
			return false;
		}
	}
}
