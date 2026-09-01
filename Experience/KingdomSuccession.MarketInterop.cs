using XRL;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		/// <summary>A frozen succession rite prevents a new market handoff from claiming either
		/// resident identity. Existing handoffs may resume; heir selection excludes their endpoints.</summary>
		internal static bool MarketHandoffMayStart()
		{
			KingdomSuccession succession = The.Game?.GetSystem<KingdomSuccession>();
			return KingdomShopStockRules.MayStartMarketHandoff(DeathSelectionInProgress,
				succession != null && !string.IsNullOrEmpty(succession.PendingDeathToken),
				succession?.PendingAccessionRepairResidentId ?? 0);
		}
	}
}
