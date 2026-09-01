using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
namespace ThousandAndFirst
{
	/// <summary>Receipts and moves only Qud-native <c>_stock</c> objects. Creation, pricing,
	/// water payment, and removal remain wholly inside Qud's loaded-body trade transaction.</summary>
	internal static partial class KingdomMarketStockCustody
	{
		private const string NativeStockProperty = "_stock";
		private sealed class Move
		{
			internal GameObject Source;
			internal GameObject Item;
		}
		internal static bool TryActiveOffice(GameObject Body,
			r_KingdomOfficeProjection Marker, out KingdomSystem System, out string SettlementId)
		{
			System = The.Game?.GetSystem<KingdomSystem>();
			SettlementId = Marker?.SettlementId;
			if (!GameObject.Validate(Body) || Marker == null || Marker.MarketServicePhase != 2
				|| System == null || System.Experience == null || string.IsNullOrEmpty(SettlementId)
				|| !System.HasShopkeeper
				|| System.ShopTier < KingdomShopStockRules.FirstPhysicalMarketTier
				|| Body.GetIntProperty("InventoryTier") != System.ShopTier
				|| Body.IDIfAssigned != Marker.BodyObjectId
				|| System.SettlementIdForOwnedZone(Body.CurrentZone?.ZoneID) != SettlementId
				|| !KingdomCitizenship.BelongsTo(System, Body)
				|| !KingdomExperienceRules.TryGetOffice(System.Experience, SettlementId,
					out KingdomCivicOfficeReceipt receipt, out _)
				|| receipt == null || receipt.Phase != KingdomCivicOfficePhase.Held
				|| !KingdomMarketProviderAuthority.LiveResident(System, SettlementId, Body,
					receipt.HolderResidentId)
				|| !Marker.Matches(System, receipt, Body)) return false;
			return true;
		}
		internal static bool TryAdmitNativeTrade(GameObject Body,
			r_KingdomOfficeProjection Marker, GameObject Item, out string Failure)
		{
			Failure = null;
			return TryActiveOffice(Body, Marker, out KingdomSystem system,
				out string settlementId)
				&& TryBind(system, settlementId, Body, Item, true, out Failure);
		}
		internal static bool TryAdmitHeld(KingdomSystem System, string SettlementId,
			GameObject Body, out string Failure)
		{
			if (!CanAdmitHeld(System, SettlementId, Body, out Failure)) return false;
			int rows = 0;
			for (int i = 0; i < Body.Inventory.Objects.Count; i++)
			{
				GameObject item = Body.Inventory.Objects[i];
				if (!NativeStock(Body, item)) continue;
				if (++rows > KingdomShopStockRules.MaximumCustodyRows)
					{ Failure = "market stock exceeds the bounded custody roster"; return false; }
				if (Exact(System, SettlementId, Body, item))
				{
					NormalizeExactRealmMarker(System, item);
					if (!KingdomMarketStockProtection.TryProtect(item, out Failure)) return false;
					continue;
				}
				if (PendingTo(System, SettlementId, Body, item)) continue;
				if (KingdomMarketStockProtection.HasProjection(item))
				{
					Failure = "foreign, torn, or stale market custody blocks automatic admission";
					return false;
				}
				if (!TryBind(System, SettlementId, Body, item, false, out Failure)) return false;
			}
			return true;
		}
		internal static bool TrySealDeparting(KingdomSystem System, GameObject Body,
			r_KingdomOfficeProjection Marker, out string Failure)
		{
			Failure = null;
			bool liveCitizen = KingdomCitizenship.BelongsTo(System, Body);
			bool frozenReceipt = Marker != null && System?.Experience != null
				&& KingdomExperienceRules.TryGetOffice(System.Experience, Marker?.SettlementId,
					out KingdomCivicOfficeReceipt receipt, out _)
				&& receipt != null && (receipt.Phase == KingdomCivicOfficePhase.Held
					|| receipt.Phase == KingdomCivicOfficePhase.VacancyPrepared)
				&& Marker.Matches(System, receipt, Body);
			if (System == null || !GameObject.Validate(Body) || Marker == null
				|| Marker.MarketServicePhase != 2 || Marker.RealmId != System.RealmId
				|| Marker.BodyObjectId != Body.IDIfAssigned
				|| System.SettlementIdForOwnedZone(Body.CurrentZone?.ZoneID) != Marker.SettlementId
				|| (!liveCitizen && !frozenReceipt))
				{ Failure = "departing market projection cannot prove its local body"; return false; }
			return TryAdmitHeld(System, Marker.SettlementId, Body, out Failure);
		}
		internal static bool TryGather(KingdomSystem System, Zone Zone, KingdomSurvey Survey,
			GameObject Target, r_KingdomOfficeProjection Marker, out string Failure)
		{
			Failure = null;
			if (System == null || Zone == null || Survey == null || !GameObject.Validate(Target)
				|| Marker == null || Marker.MarketServicePhase != 2
				|| Marker.RealmId != System.RealmId || Marker.SettlementId
					!= System.SettlementIdForOwnedZone(Zone.ZoneID)
				|| Target.IDIfAssigned != Marker.BodyObjectId
				|| !KingdomMarketStockDetachment.TryRetire(System, Zone, Survey,
					Marker.SettlementId, out Failure)
				|| !TryAdmitHeld(System, Marker.SettlementId, Target, out Failure)) return false;
			List<Move> candidates = new List<Move>();
			List<GameObject> pending = new List<GameObject>();
			HashSet<string> receipts = new HashSet<string>(StringComparer.Ordinal);
			int rows = 0;
			for (int b = 0; b < Survey.Objects.Count; b++)
			{
				GameObject body = Survey.Objects[b];
				if (!GameObject.Validate(body) || body.Inventory == null
					|| !ReferenceEquals(body.CurrentZone, Zone)) continue;
				for (int i = 0; i < body.Inventory.Objects.Count; i++)
				{
					GameObject item = body.Inventory.Objects[i];
					if (!NativeStock(body, item) || !OurMarker(System, Marker.SettlementId, item))
						continue;
					if (++rows > KingdomShopStockRules.MaximumCustodyRows)
						{ Failure = "market stock exceeds the bounded custody roster"; return false; }
					string receipt = item.GetStringProperty(KingdomShopStockRules.StockReceiptProperty);
					string expected = KingdomShopStockRules.StockReceiptId(System.RealmId,
						Marker.SettlementId, item.IDIfAssigned);
					if (receipt != expected || expected == null || !receipts.Add(receipt))
						{ Failure = "market stock has a torn or duplicate physical receipt"; return false; }
					string custodian = item.GetStringProperty(KingdomShopStockRules.StockCustodianProperty);
					string target = item.GetStringProperty(
						KingdomShopStockRules.StockTransferTargetProperty);
					if (ReferenceEquals(body, Target))
					{
						if (custodian == Target.IDIfAssigned && string.IsNullOrEmpty(target)) continue;
						if (target == Target.IDIfAssigned && !string.IsNullOrEmpty(custodian))
							{ pending.Add(item); continue; }
						Failure = "market stock arrived without an exact transfer intent"; return false;
					}
					if (!KingdomCitizenship.BelongsTo(System, body)) continue;
					if (body.GetIntProperty(KingdomGuestbook.LegendaryTraderResidentProperty) == 1)
						{ Failure = "an explicit legendary market owns this stock"; return false; }
					if (custodian != body.IDIfAssigned
						|| (!string.IsNullOrEmpty(target) && target != Target.IDIfAssigned))
						{ Failure = "market stock is outside its receipted custodian"; return false; }
					candidates.Add(new Move { Source = body, Item = item });
				}
			}
			List<Move> attempted = new List<Move>();
			for (int i = 0; i < candidates.Count; i++)
			{
				Move move = candidates[i];
				if (!Transferable(move.Source, move.Item, out Failure))
				{
					Failure = null;
					if (KingdomMarketStockProtection.TryRetire(move.Item)) continue;
					Failure = "non-transferable market marks did not retire exactly"; return false;
				}
				attempted.Add(move);
				if (!MoveTo(move, Target, out Failure))
				{
					if (!Rollback(System, Marker.SettlementId, Target, attempted))
						KingdomLog.Log("market stock rollback remains open for exact retry");
					return false;
				}
				pending.Add(move.Item);
			}
			for (int i = 0; i < pending.Count; i++)
				if (!TryBind(System, Marker.SettlementId, Target, pending[i], true, out Failure))
					return false;
			return true;
		}
		internal static bool ExactTransferable(KingdomSystem System, string SettlementId,
			GameObject Prior, GameObject Item)
		{
			return System != null && GameObject.Validate(Prior) && GameObject.Validate(Item)
				&& Item.GetIntProperty(NativeStockProperty) == 1
				&& Exact(System, SettlementId, Prior, Item)
				&& !Item.IsImportant() && KingdomConstructionInputLeaseAuthority
					.TryObjectGraphAvailableForOrdinaryTransfer(Item, out _);
		}
		internal static bool ExactHeld(KingdomSystem System, string SettlementId,
			GameObject Body, GameObject Item)
			{
				return NativeStock(Body, Item) && Exact(System, SettlementId, Body, Item);
			}
		internal static bool TryCommitExternal(KingdomSystem System, string SettlementId,
			GameObject Prior, GameObject Target, GameObject Item, out string Failure)
		{
			Failure = null;
			return ExactTransferable(System, SettlementId, Prior, Item)
				&& NativeStock(Target, Item)
				&& TryBind(System, SettlementId, Target, Item, true, out Failure);
		}
		internal static bool TryRebindPhysical(KingdomSystem System, string SettlementId,
			GameObject Body, GameObject Item, out string Failure)
		{
			Failure = null;
			return OurMarker(System, SettlementId, Item)
				&& TryBind(System, SettlementId, Body, Item, true, out Failure);
		}
		internal static bool TryBind(KingdomSystem System, string SettlementId,
			GameObject Body, GameObject Item, bool Rebind, out string Failure)
		{
			Failure = null;
			if (!NativeStock(Body, Item) || System == null || string.IsNullOrEmpty(SettlementId))
				{ Failure = "market receipt has no exact physical stock"; return false; }
			string bodyId = Body.ID;
			string itemId = Item.ID;
			string receipt = KingdomShopStockRules.StockReceiptId(System.RealmId,
				SettlementId, itemId);
			if (receipt == null)
				{ Failure = "market receipt identity is invalid or already claimed"; return false; }
			if (KingdomMarketStockProtection.HasProjection(Item))
			{
				bool local = Rebind && OurMarker(System, SettlementId, Item)
					&& Item.GetStringProperty(KingdomShopStockRules.StockReceiptProperty) == receipt;
				string custodian = Item.GetStringProperty(
					KingdomShopStockRules.StockCustodianProperty);
				string target = Item.GetStringProperty(
					KingdomShopStockRules.StockTransferTargetProperty);
				if (!local || (custodian != bodyId && target != bodyId))
				{
					Failure = "market receipt is foreign, divergent, or lacks exact transfer intent";
					return false;
				}
			}
			else if (!Rebind && Item.HasStringProperty(
				KingdomShopStockRules.StockReceiptProperty))
				{ Failure = "market receipt is already claimed"; return false; }
			if (!KingdomMarketStockProtection.CanProtect(Item, out Failure)) return false;
			Item.SetStringProperty(KingdomShopStockRules.StockReceiptProperty, receipt);
			Item.SetStringProperty(KingdomShopStockRules.StockRealmProperty, System.RealmId);
			Item.SetStringProperty(KingdomShopStockRules.LegacyStockRealmProperty, null,
				RemoveIfNull: true);
			Item.SetStringProperty(KingdomShopStockRules.StockSettlementProperty, SettlementId);
			Item.SetStringProperty(KingdomShopStockRules.StockCustodianProperty, bodyId);
			Item.SetStringProperty(KingdomShopStockRules.StockTransferTargetProperty,
				null, RemoveIfNull: true);
			if (!KingdomMarketStockProtection.TryProtect(Item, out Failure)) return false;
			return Exact(System, SettlementId, Body, Item);
		}
		private static bool Transferable(GameObject Source, GameObject Item, out string Failure)
		{
			Failure = null;
			if (!NativeStock(Source, Item) || Item.IsImportant() || Item.Physics == null
				|| !Item.IsTakeable())
				{ Failure = "protected market stock stays with its current custodian"; return false; }
			if (!KingdomConstructionInputLeaseAuthority
				.TryObjectGraphAvailableForOrdinaryTransfer(Item, out Failure)) return false;
			return true;
		}
		private static bool NativeStock(GameObject Body, GameObject Item)
		{
			return GameObject.Validate(Body) && GameObject.Validate(Item) && Body.Inventory != null
				&& ReferenceEquals(Item.InInventory, Body)
				&& Body.Inventory.Objects.Contains(Item) && Item.GetIntProperty(NativeStockProperty) == 1;
		}
		private static bool Exact(KingdomSystem System, string SettlementId,
			GameObject Body, GameObject Item)
		{
			return KingdomShopStockRules.TryResolveStockRealm(Item.GetStringProperty(
				KingdomShopStockRules.StockRealmProperty), Item.GetStringProperty(
				KingdomShopStockRules.LegacyStockRealmProperty), out string realm)
				&& realm == System?.RealmId
				&& Item.GetStringProperty(KingdomShopStockRules.StockSettlementProperty)
					== SettlementId
				&& KingdomShopStockRules.ExactStockCustody(
					Item.GetStringProperty(KingdomShopStockRules.StockReceiptProperty),
					realm,
				Item.GetStringProperty(KingdomShopStockRules.StockSettlementProperty),
				Item.GetStringProperty(KingdomShopStockRules.StockCustodianProperty),
					System?.RealmId, SettlementId, Body?.IDIfAssigned, Item?.IDIfAssigned);
		}
		private static void NormalizeExactRealmMarker(KingdomSystem System, GameObject Item)
		{
			Item.SetStringProperty(KingdomShopStockRules.StockRealmProperty, System.RealmId);
			Item.SetStringProperty(KingdomShopStockRules.LegacyStockRealmProperty, null,
				RemoveIfNull: true);
		}
		internal static bool OurMarker(KingdomSystem System, string SettlementId, GameObject Item)
		{
			return KingdomShopStockRules.TryResolveStockRealm(Item.GetStringProperty(
				KingdomShopStockRules.StockRealmProperty), Item.GetStringProperty(
				KingdomShopStockRules.LegacyStockRealmProperty), out string realm)
				&& realm == System.RealmId
				&& Item.GetStringProperty(KingdomShopStockRules.StockSettlementProperty) == SettlementId;
		}
		private static bool PendingTo(KingdomSystem System, string SettlementId,
			GameObject Target, GameObject Item)
		{
			string expected = KingdomShopStockRules.StockReceiptId(System.RealmId,
				SettlementId, Item.IDIfAssigned);
			return expected != null && OurMarker(System, SettlementId, Item)
				&& Item.GetStringProperty(KingdomShopStockRules.StockReceiptProperty) == expected
				&& Item.GetStringProperty(KingdomShopStockRules.StockTransferTargetProperty)
					== Target.IDIfAssigned;
		}
	}
}
