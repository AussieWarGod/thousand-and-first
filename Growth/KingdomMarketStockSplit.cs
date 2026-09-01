using System;
using HarmonyLib;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Repairs only the exact remainder Qud creates for a split stack already held by
	/// the same receipted keeper. Arbitrary clones never inherit market authority.</summary>
	[HarmonyPatch(typeof(GameObject), nameof(GameObject.SplitStack),
		new Type[] { typeof(int), typeof(GameObject), typeof(bool) })]
	internal static class KingdomMarketStockSplitPatch
	{
		private static void Postfix(GameObject __instance, int Count, GameObject OwningObject,
			GameObject __result)
		{
			if (!KingdomMarketStockCustody.TryRepairNativeSplit(__instance,
				Count, OwningObject, __result, out string failure) && failure != null)
				KingdomLog.Log("market stack split receipt waits (" + failure + ")");
		}
	}

	internal static partial class KingdomMarketStockCustody
	{
		internal static bool TryRepairNativeSplit(GameObject Source, int Count, GameObject Holder,
			GameObject Remainder, out string Failure)
		{
			Failure = null;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			string settlement = system?.SettlementIdForOwnedZone(Holder?.CurrentZone?.ZoneID);
			if (!GameObject.Validate(Source) || !GameObject.Validate(Remainder)
				|| !GameObject.Validate(Holder) || ReferenceEquals(Source, Remainder)
				|| string.IsNullOrEmpty(settlement)
				|| !ExactHeld(system, settlement, Holder, Source)) return true;
			bool active = ActiveSplitCustody(system, Holder, Source);
			if (!CopiedReceipt(Source, Remainder))
			{
				// Normal DeepCopy is stripped by the item projection; AlwaysStack creates a
				// blank blueprint. Only this exact native split may issue the new receipt.
				bool always = Source.HasTag("AlwaysStack");
				bool stockShape = always ? !Remainder.HasIntProperty("_stock")
					: Remainder.GetIntProperty("_stock") == 1;
				if (!KingdomMarketStockProtection.HasProjection(Remainder) && stockShape
					&& Source.Blueprint == Remainder.Blueprint
					&& Count > 0 && Source.Count == Count && Remainder.Count > 0
					&& ReferenceEquals(Source.InInventory, Holder)
					&& ReferenceEquals(Remainder.InInventory, Holder))
				{
					if (always) Remainder.SetIntProperty("_stock", 1);
					if (!active) return true;
					if (TryBind(system, settlement, Holder, Remainder, false, out Failure))
						return true;
					return false;
				}
				if (!KingdomMarketStockProtection.HasProjection(Remainder)) return true;
				Failure = "split remainder did not copy the exact source receipt"; return false;
			}
			if (!KingdomMarketStockProtection.TryRetire(Remainder))
				{ Failure = "split remainder marks did not retire exactly"; return false; }
			if (!active) return true;
			if (!ReferenceEquals(Source.InInventory, Holder)
				|| !ReferenceEquals(Remainder.InInventory, Holder)) return true;
			return TryBind(system, settlement, Holder, Remainder, false, out Failure);
		}

		private static bool ActiveSplitCustody(KingdomSystem System, GameObject Holder,
			GameObject Item)
		{
			r_KingdomOfficeProjection office = Holder.GetPart<r_KingdomOfficeProjection>();
			if (office != null && TryActiveOffice(Holder, office, out KingdomSystem exact,
				out _) && ReferenceEquals(exact, System)
				&& KingdomMarketProviderAuthority.TryProve(System, Holder,
					Holder.GetIntProperty("InventoryTier"), out _)) return true;
			r_KingdomLegendaryMarketProjection legend =
				Holder.GetPart<r_KingdomLegendaryMarketProjection>();
			return legend != null && legend.Active(System, Holder)
				|| r_KingdomLegendaryMarketProjection.PreparedTransferAuthority(
					System, Holder, Item);
		}

		private static bool CopiedReceipt(GameObject Source, GameObject Remainder)
		{
			return Source.GetStringProperty(KingdomShopStockRules.StockReceiptProperty)
				== Remainder.GetStringProperty(KingdomShopStockRules.StockReceiptProperty)
				&& Source.GetStringProperty(KingdomShopStockRules.StockRealmProperty)
					== Remainder.GetStringProperty(KingdomShopStockRules.StockRealmProperty)
				&& Source.GetStringProperty(KingdomShopStockRules.LegacyStockRealmProperty)
					== Remainder.GetStringProperty(KingdomShopStockRules.LegacyStockRealmProperty)
				&& Source.GetStringProperty(KingdomShopStockRules.StockSettlementProperty)
					== Remainder.GetStringProperty(KingdomShopStockRules.StockSettlementProperty)
				&& Source.GetStringProperty(KingdomShopStockRules.StockCustodianProperty)
					== Remainder.GetStringProperty(KingdomShopStockRules.StockCustodianProperty)
				&& Source.GetStringProperty(KingdomShopStockRules.StockTransferTargetProperty)
					== Remainder.GetStringProperty(KingdomShopStockRules.StockTransferTargetProperty);
		}
	}
}
