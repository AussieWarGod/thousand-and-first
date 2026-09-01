using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomMarketStockCustody
	{
		/// <summary>Retires exact direct civic-stock marks when its former keeper becomes the
		/// player. The physical item, holder, cell, count, native <c>_stock</c>, and foreign state
		/// are invariant. A later torn row blocks the whole bounded transaction.</summary>
		internal static bool TryRetireAccedingHolder(KingdomSystem System,
			string SettlementId, GameObject Body, out string Failure)
		{
			Failure = null;
			if (System == null || Body?.Inventory == null || string.IsNullOrEmpty(SettlementId))
				{ Failure = "accession market cleanup lacks exact physical custody"; return false; }
			List<GameObject> retire = new List<GameObject>();
			for (int i = 0; i < Body.Inventory.Objects.Count; i++)
			{
				GameObject item = Body.Inventory.Objects[i];
				if (!KingdomMarketStockProtection.HasProjection(item)) continue;
				if (retire.Count >= KingdomShopStockRules.MaximumCustodyRows)
					{ Failure = "accession market stock exceeds its bounded roster"; return false; }
				if (!Exact(System, SettlementId, Body, item))
					{ Failure = "accession market stock is foreign or torn"; return false; }
				retire.Add(item);
			}
			if (!KingdomMarketRemoval.TryPrepareTransaction(System, retire,
				new List<GameObject>(), out KingdomMarketRemovalTransaction transaction,
				out Failure)) return false;
			return KingdomMarketRemoval.TryCommitTransaction(System, transaction, out Failure);
		}

		internal static bool TryRetireAccedingLegendary(KingdomSystem System,
			GameObject Body, out bool HadCivicAuthority, out string Failure)
		{
			HadCivicAuthority = false; Failure = null;
			r_KingdomLegendaryMarketProjection marker =
				Body?.GetPart<r_KingdomLegendaryMarketProjection>();
			if (marker == null)
				{ Failure = "successor lacks an exact legendary market owner"; return false; }
			if (Body.GetPart<r_KingdomOfficeProjection>() != null)
				{ Failure = "successor has competing office and legendary market authority"; return false; }
			if (marker.HandoffPrepared != 0)
				{ Failure = "accession cannot consume an open market handoff"; return false; }
			if (!KingdomMarketRemoval.CanRetireLegendary(System, Body,
				out bool retiresLegend, out Failure) || !retiresLegend) return false;
			HadCivicAuthority = KingdomShopStockRules.IsCurrentLegendaryCivicAuthority(
				Body.GetIntProperty("VillageMerchant") == 1, System.HasShopkeeper,
				System.CurrentSettlementId, marker.SettlementId, System.ShopTier,
				Body.GetIntProperty("InventoryTier"));
			List<GameObject> retire = new List<GameObject>();
			for (int i = 0; Body.Inventory != null && i < Body.Inventory.Objects.Count; i++)
			{
				GameObject item = Body.Inventory.Objects[i];
				if (!KingdomMarketStockProtection.HasProjection(item)) continue;
				if (retire.Count >= KingdomShopStockRules.MaximumCustodyRows
					|| !Exact(System, marker.SettlementId, Body, item))
					{ Failure = "legendary successor stock is foreign, torn, or over bound"; return false; }
				retire.Add(item);
			}
			if (!KingdomMarketRemoval.TryPrepareTransaction(System, retire,
				new List<GameObject> { Body }, out KingdomMarketRemovalTransaction transaction,
				out Failure)) return false;
			if (!KingdomMarketRemoval.TryCommitTransaction(System, transaction,
				out Failure)) return false;
			// The commit is synchronous and contains no save/yield boundary. Clear compatibility
			// only after durable retirement; a refused/rolled-back transaction leaves it intact.
			if (HadCivicAuthority)
				{ System.HasShopkeeper = false; System.ShopTier = 0; }
			return true;
		}
	}
}
