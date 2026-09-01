using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		/// <summary>Keeps personal legendary trade separate from civic market authority.
		/// The sealed native trader remains a merchant when civic proof is lost, but only a live
		/// physical provider plus held office may project VillageMerchant and current standing.</summary>
		private static bool TryMaintainLegendaryCivicProjection(KingdomSystem System,
			KingdomSurvey Survey, string SettlementId, int Tier, bool CivicEligible,
			out string Failure)
		{
			Failure = null;
			GameObject legend = null;
			for (int i = 0; i < Survey.Objects.Count; i++)
			{
				GameObject body = Survey.Objects[i];
				r_KingdomLegendaryMarketProjection marker =
					body?.GetPart<r_KingdomLegendaryMarketProjection>();
				if (marker == null) continue;
				if (GameObject.Validate(body) && !body.IsAlive)
				{
					if (marker.HandoffPrepared == 1)
					{
						if (!TryAbortPreparedLegendaryHandoff(System, Survey, body, marker,
							out Failure)) return false;
						continue;
					}
					if (marker.RealmId != System.RealmId
						|| marker.SettlementId != SettlementId
						|| marker.BodyObjectId != body.IDIfAssigned
						|| body.GetIntProperty(
							KingdomGuestbook.LegendaryTraderResidentProperty) != 1
						|| (body.HasIntProperty("VillageMerchant")
							&& body.GetIntProperty("VillageMerchant") != 0
							&& body.GetIntProperty("VillageMerchant") != 1))
					{
						Failure = "a dead legendary market projection is divergent"; return false;
					}
					body.RemoveIntProperty("VillageMerchant"); body.RemovePart(marker);
					if (body.HasIntProperty("VillageMerchant")
						|| body.GetPart<r_KingdomLegendaryMarketProjection>() != null)
					{
						Failure = "dead legendary civic authority resisted retirement"; return false;
					}
					continue;
				}
				if (marker.HandoffPrepared == 1)
				{
					GameObject prior = GameObject.FindByID(marker.PriorBodyObjectId);
					if (!GameObject.Validate(prior) || !prior.IsAlive)
					{
						if (!TryAbortPreparedLegendaryHandoff(System, Survey, body, marker,
							out Failure)) return false;
						continue;
					}
					Failure = "an exact legendary market handoff is still in progress"; return false;
				}
				if (marker.RealmId != System.RealmId || marker.SettlementId != SettlementId)
					{ Failure = "a foreign legendary market projection is quarantined"; return false; }
				GenericInventoryRestocker restocker = body.GetPart<GenericInventoryRestocker>();
				int heldTier = body.GetIntProperty("InventoryTier");
				if (!GameObject.Validate(body) || !body.IsAlive || body.IsPlayer()
					|| marker.BodyObjectId != body.IDIfAssigned
					|| !KingdomCitizenship.BelongsTo(System, body)
					|| body.GetIntProperty(
						KingdomGuestbook.LegendaryTraderResidentProperty) != 1
					|| !body.HasIntProperty("Merchant")
					|| body.GetIntProperty("Merchant") != 1
					|| !body.HasIntProperty("InventoryTier") || heldTier < 1
					|| heldTier > KingdomShopStockRules.MaximumTier
					|| !SealedFiniteRestocker(restocker)
					|| (body.HasIntProperty("VillageMerchant")
						&& body.GetIntProperty("VillageMerchant") != 1))
				{
					Failure = "the legendary market projection diverged from its exact body";
					return false;
				}
				if (legend != null)
					{ Failure = "more than one legendary market projection is active"; return false; }
				legend = body;
			}
			if (legend == null) return true;
			if (CivicEligible)
			{
				legend.SetIntProperty("InventoryTier", Tier);
				legend.SetIntProperty("VillageMerchant", 1);
				return KnownLegendaryMarketState(System, legend, SettlementId, Tier);
			}
			legend.RemoveIntProperty("VillageMerchant");
			return !legend.HasIntProperty("VillageMerchant") && legend.IsMerchant();
		}

	}
}
