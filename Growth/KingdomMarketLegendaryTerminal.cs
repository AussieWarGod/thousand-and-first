using System;
using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public sealed partial class r_KingdomLegendaryMarketProjection
	{
		/// <summary>Terminal handoff proof does not infer death from a missing citizenship part.
		/// It joins the frozen live resident identity to that settlement's exact Dead row.</summary>
		internal bool ExactPreparedTerminal(KingdomSystem System, GameObject Body)
		{
			string expected = KingdomShopStockRules.SourceId(System?.RealmId,
				SettlementId, MarketTier) + ":handoff:" + PriorBodyObjectId + ":" + BodyObjectId;
			return HandoffPrepared == 1 && System != null && GameObject.Validate(Body)
				&& !Body.IsAlive && !Body.IsPlayer() && RealmId == System.RealmId
				&& !string.IsNullOrEmpty(SettlementId) && BodyObjectId == Body.IDIfAssigned
				&& MarketTier >= KingdomShopStockRules.FirstPhysicalMarketTier
				&& MarketTier <= KingdomShopStockRules.MaximumTier
				&& HandoffResidentId > 0 && PriorResidentId > 0
				&& DeadResident(System, SettlementId, HandoffResidentId)
				&& System.SettlementIdForOwnedZone(Body.CurrentZone?.ZoneID) == SettlementId
				&& Body.GetIntProperty(KingdomGuestbook.LegendaryTraderResidentProperty) == 1
				&& Body.HasIntProperty("Merchant") && Body.GetIntProperty("Merchant") == 1
				&& Body.GetIntProperty("InventoryTier") == MarketTier
				&& Body.IsMerchant() && HandoffIntent == expected
				&& Body.GetStringProperty(KingdomGuestbook.MarketHandoffIntentProperty)
					== HandoffIntent
				&& Body.GetStringProperty(KingdomGuestbook.MarketHandoffPriorProperty)
					== PriorBodyObjectId
				&& KingdomGrowth.SealedFiniteRestocker(
					Body.GetPart<XRL.World.Parts.GenericInventoryRestocker>());
		}

		internal bool ExactPriorTerminal(KingdomSystem System, GameObject Prior)
		{
			if (HandoffPrepared != 1 || System == null || RealmId != System.RealmId
				|| string.IsNullOrEmpty(PriorBodyObjectId) || PriorResidentId <= 0
				|| !DeadResident(System, SettlementId, PriorResidentId)) return false;
			return !GameObject.Validate(Prior) || (!Prior.IsAlive
				&& Prior.IDIfAssigned == PriorBodyObjectId);
		}

		internal bool ExactLivePreparedBody(KingdomSystem System, GameObject Body)
		{
			return ExactPreparedBody(System, Body);
		}

		internal static bool DeadResident(KingdomSystem System, string Settlement,
			int ResidentId)
		{
			List<KingdomCityBook> books = System?.OwnedCityBooks();
			KingdomResidentRow found = default(KingdomResidentRow); int matches = 0;
			for (int i = 0; books != null && i < books.Count; i++)
			{
				KingdomCityBook book = books[i];
				if (book?.SettlementId != Settlement
					|| !KingdomResidents.TryResident(book, ResidentId, out KingdomResidentRow row))
					continue;
				found = row; matches++;
			}
			return matches == 1 && found.Standing == KingdomResidentStanding.Dead;
		}
	}
}
