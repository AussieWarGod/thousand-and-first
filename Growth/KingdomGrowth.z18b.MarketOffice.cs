using System;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		/// <summary>Reconciles shop service from one explicit civic-office appointment. A growth
		/// stage can enable the service, but can never select its holder.</summary>
		private static bool ReconcileMarketOffice(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, int Tier, bool LiveMarketCapability,
			bool PriorShopClaim, out string Failure)
		{
			Failure = null;
			string settlementId = System?.SettlementIdForOwnedZone(Z?.ZoneID);
			if (System == null || Z == null || Survey == null
				|| string.IsNullOrEmpty(settlementId) || System.Experience == null)
			{
				Failure = "the market lacks exact settlement or civic-office authority";
				return false;
			}
			if (!KingdomExperienceRules.TryGetOffice(System.Experience, settlementId,
				out KingdomCivicOfficeReceipt receipt, out Failure)) return false;
			if (!TryRecoverAbsentHandoffTargets(System, Survey, settlementId, out Failure))
				return false;
			if (receipt != null && receipt.Phase == KingdomCivicOfficePhase.Quarantined)
			{
				Failure = "the civic-office receipt is quarantined; market state was left untouched";
				return false;
			}

			GameObject holder = FindMarketOfficeHolder(System, Z, Survey, receipt);
			r_KingdomOfficeProjection exact = holder?.GetPart<r_KingdomOfficeProjection>();
			bool exactProjection = exact != null && exact.Matches(System, receipt, holder);
			bool eligible = KingdomShopStockRules.OfficeServiceEligible(System.Stage,
				receipt == null ? KingdomCivicOfficePhase.None : receipt.Phase,
				holder != null, exactProjection, LiveMarketCapability, Tier);
			if (!TryMaintainLegendaryCivicProjection(System, Survey, settlementId,
				Tier, eligible, out Failure)) return false;
			if (!RetireStaleMarketOfficeProjections(System, Survey,
				eligible ? exact : null, out Failure)) return false;

			int visible = CountVisibleCitizenMarkets(System, Survey, out GameObject soleVisible);
			if (eligible && exact.MarketServicePhase != 0 && visible == 1
				&& !ReferenceEquals(soleVisible, holder)
				&& KnownLegendaryMarketState(System, soleVisible, settlementId, Tier)
				&& !TryCleanupMarketService(holder, exact, out Failure)) return false;

			if (eligible && exact.MarketServicePhase == 0)
			{
				if (visible == 0)
				{
					if (!TryProjectMarketService(holder, exact, Tier, out Failure)) return false;
					PublishMarketOpening(System, receipt);
				}
				else if (visible == 1 && ReferenceEquals(soleVisible, holder)
					&& IsLegacyMarketProjection(System, holder, exact, Tier, PriorShopClaim))
				{
					if (!AdoptLegacyMarketProjection(holder, exact, out Failure)) return false;
				}
				else if (!(visible == 1 && KnownLegendaryMarketState(System,
					soleVisible, settlementId, Tier)))
				{
					Failure = "an unowned or ambiguous merchant blocks exact office service";
					return false;
				}
			}
			else if (eligible && exact.MarketServicePhase != 0
				&& !TryMaintainMarketService(holder, exact, Tier, out Failure)) return false;
			if (eligible && exact.MarketServicePhase == 2
				&& !KingdomMarketStockCustody.TryGather(System, Z, Survey,
					holder, exact, out Failure)) return false;

			int canonical = CountCanonicalMarketBodies(System, Survey, settlementId,
				receipt, Tier, out GameObject _);
			visible = CountVisibleCitizenMarkets(System, Survey, out soleVisible);
			System.HasShopkeeper = eligible && canonical == 1;
			if (canonical > 1 || visible != canonical)
			{
				if (canonical == 0 && visible == 1 && PriorShopClaim
					&& !eligible && IsLegacyAutomaticMarket(System, soleVisible, true))
				{
					// Preserve the one old-save migration signal until the player explicitly
					// appoints an office. It is never authorized to issue another stock batch.
					System.HasShopkeeper = true; return true;
				}
				System.HasShopkeeper = false;
				Failure = visible != canonical
					? "an unowned citizen merchant is quarantined from local-market authority"
					: "more than one exact market service is active";
				return false;
			}
			return true;
		}

		private static GameObject FindMarketOfficeHolder(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, KingdomCivicOfficeReceipt Receipt)
		{
			if (Receipt == null || Receipt.Phase != KingdomCivicOfficePhase.Held) return null;
			GameObject found = null;
			for (int i = 0; i < Survey.Objects.Count; i++)
			{
				GameObject body = Survey.Objects[i];
				if (!GameObject.Validate(body) || !body.IsAlive
					|| !ReferenceEquals(body.CurrentZone, Z)
					|| body.IDIfAssigned != Receipt.HolderObjectId
					|| Simulation.City.KingdomResidents.IdOf(body) != Receipt.HolderResidentId
					|| !KingdomCitizenship.BelongsTo(System, body)) continue;
				if (found != null) return null;
				found = body;
			}
			return found;
		}

		private static bool RetireStaleMarketOfficeProjections(KingdomSystem System,
			KingdomSurvey Survey, r_KingdomOfficeProjection Keep, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Survey.Objects.Count; i++)
			{
				GameObject body = Survey.Objects[i];
				r_KingdomOfficeProjection marker = body?.GetPart<r_KingdomOfficeProjection>();
				if (marker == null || marker.MarketServicePhase == 0
					|| ReferenceEquals(marker, Keep)) continue;
				if (marker.DeathResidue) continue;
				if (!GameObject.Validate(body) || !body.IsAlive)
				{
					Failure = "a dead office market awaits exact terminal residue proof"; return false;
				}
				if (marker.RealmId != System.RealmId)
				{
					Failure = "a foreign-realm market projection is quarantined"; return false;
				}
				if (!TryCleanupMarketService(body, marker, out Failure)) return false;
			}
			return true;
		}

		private static int CountCanonicalMarketBodies(KingdomSystem System,
			KingdomSurvey Survey, string SettlementId, KingdomCivicOfficeReceipt Receipt,
			int Tier, out GameObject Sole)
		{
			Sole = null; int count = 0;
			for (int i = 0; i < Survey.Objects.Count; i++)
			{
				GameObject body = Survey.Objects[i];
				if (!GameObject.Validate(body) || !body.IsAlive
					|| body.GetIntProperty("VillageMerchant") != 1
					|| !KingdomCitizenship.BelongsTo(System, body)) continue;
				r_KingdomOfficeProjection marker = body.GetPart<r_KingdomOfficeProjection>();
				if (marker?.DeathResidue == true) continue;
				bool office = marker != null && marker.MarketServicePhase == 2
					&& marker.SettlementId == SettlementId
					&& marker.Matches(System, Receipt, body)
					&& KnownMarketServiceState(body, marker);
				if (!office && !KnownLegendaryMarketState(System, body,
					SettlementId, Tier)) continue;
				count++; Sole = body;
			}
			return count;
		}

		private static int CountVisibleCitizenMarkets(KingdomSystem System,
			KingdomSurvey Survey, out GameObject Sole)
		{
			Sole = null; int count = 0;
			for (int i = 0; i < Survey.Objects.Count; i++)
			{
				GameObject body = Survey.Objects[i];
				if (!GameObject.Validate(body) || !body.IsAlive
					|| body.GetIntProperty("VillageMerchant") != 1
					|| !KingdomCitizenship.BelongsTo(System, body)) continue;
				if (body.GetPart<r_KingdomOfficeProjection>()?.DeathResidue == true) continue;
				count++; Sole = body;
			}
			return count;
		}

		private static bool IsExplicitLegendaryMarket(GameObject Body)
		{
			return GameObject.Validate(Body)
				&& Body.GetIntProperty(KingdomGuestbook.LegendaryTraderResidentProperty) == 1
				&& Body.GetIntProperty("VillageMerchant") == 1;
		}

		internal static bool TryAuthorizedMarketBody(KingdomSystem System, Zone Z,
			GameObject Body, int Tier, out bool OwnsRestocker)
		{
			OwnsRestocker = false;
			if (!GameObject.Validate(Body) || Body.GetIntProperty("VillageMerchant") != 1
				|| !KingdomCitizenship.BelongsTo(System, Body)
				|| !KingdomMarketProviderAuthority.TryProveProjection(System, Body, Tier,
					out string _)) return false;
			string settlementId = System?.SettlementIdForOwnedZone(Z?.ZoneID);
			if (KnownLegendaryMarketState(System, Body, settlementId, Tier))
				{ OwnsRestocker = true; return true; }
			if (string.IsNullOrEmpty(settlementId) || System.Experience == null
				|| !KingdomExperienceRules.TryGetOffice(System.Experience, settlementId,
					out KingdomCivicOfficeReceipt receipt, out string _)) return false;
			r_KingdomOfficeProjection marker = Body.GetPart<r_KingdomOfficeProjection>();
			return marker != null && marker.MarketServicePhase == 2
				&& marker.SettlementId == settlementId
				&& marker.MarketTier == Tier
				&& marker.Matches(System, receipt, Body)
				&& KnownMarketServiceState(Body, marker);
		}

		private static bool KnownLegendaryMarketState(KingdomSystem System, GameObject Body,
			string SettlementId, int Tier)
		{
			r_KingdomLegendaryMarketProjection marker =
				Body?.GetPart<r_KingdomLegendaryMarketProjection>();
			return System != null && IsExplicitLegendaryMarket(Body)
				&& !string.IsNullOrEmpty(SettlementId)
				&& Tier >= KingdomShopStockRules.FirstPhysicalMarketTier
				&& Tier <= KingdomShopStockRules.MaximumTier
				&& Body.GetIntProperty("InventoryTier") == Tier
				&& marker != null && marker.RealmId == System.RealmId
				&& marker.SettlementId == SettlementId
				&& marker.BodyObjectId == Body.IDIfAssigned
				&& SealedFiniteRestocker(Body.GetPart<GenericInventoryRestocker>());
		}

		private static void PublishMarketOpening(KingdomSystem System,
			KingdomCivicOfficeReceipt Receipt)
		{
			if (System == null || Receipt == null) return;
			string key = "taf:growth:market-office:" + Receipt.SettlementId + ":"
				+ Receipt.Generation;
			KingdomChronicle.RecordOnce(System, key, Receipt.HolderName
				+ " opened the finite local market at " + Receipt.SettlementName);
			MessageQueue.AddPlayerMessage("{{G|" + KingdomPresentation.Rich(Receipt.HolderName)
				+ " has opened the local counter. It begins honestly: wares arrive only when "
				+ "someone brings physical goods to trade.}}");
		}

		/// <summary>Reads accepted physical capability, then lets real local craft and designated
		/// market ground improve standing. Growth only caps that answer.</summary>
		internal static bool TryMarketServiceStanding(KingdomSystem System, KingdomSurvey Survey,
			out int Tier, out bool LiveMarketCapability, out string Failure)
		{
			Tier = 0; LiveMarketCapability = false; Failure = null;
			if (System == null || Survey == null)
				{ Failure = "market standing has no exact survey"; return false; }
			if (!Survey.TryBenefits(out KingdomBenefitIndex benefits, out Failure)) return false;
			bool marketDistrict = false;
			System.Collections.Generic.IReadOnlyList<KingdomBenefitReading> readings =
				benefits.Readings;
			for (int i = 0; i < readings.Count; i++)
			{
				KingdomBenefitReading reading = readings[i];
				if (!KingdomBenefitCapabilities.Has(reading,
					KingdomBenefitCapabilities.Market)) continue;
				LiveMarketCapability = true;
				string zoneId = reading.Designation?.ZoneId;
				if (string.Equals(KingdomZoning.DistrictOf(System, zoneId), "market",
					StringComparison.OrdinalIgnoreCase)) marketDistrict = true;
			}
			Tier = KingdomShopStockRules.EffectiveServiceTier(System.Stage,
				(int)KingdomZoning.Tech(System), LiveMarketCapability, marketDistrict);
			return true;
		}
	}
}
