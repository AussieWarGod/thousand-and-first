using System;
using System.Collections.Generic;
using HarmonyLib;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Native trade and container screens are ordinary custody movers. Their cached rows
	/// never authorize purpose cargo; every selected row is reproved when offer execution begins.</summary>
	internal static class KingdomNativeTradeCargoFence
	{
		[ThreadStatic]
		private static bool OfferActive;

		internal static bool Safe(GameObject item)
		{
			return KingdomOrdinaryCustody.TryProveNoProtectedCargo(item, out _);
		}

		internal static bool SelectedRowsSafe(List<TradeEntry>[] objects, int[][] selected)
		{
			if (objects == null || selected == null || objects.Length != 2
				|| selected.Length != 2) return false;
			for (int side = 0; side < 2; side++)
			{
				if (objects[side] == null || selected[side] == null
					|| selected[side].Length != objects[side].Count) return false;
				for (int row = 0; row < objects[side].Count; row++)
				{
					if (selected[side][row] <= 0) continue;
					GameObject item = objects[side][row]?.GO;
					if (!GameObject.Validate(item) || selected[side][row] > item.Count
						|| !Safe(item)) return false;
				}
			}
			return true;
		}

		[HarmonyPatch(typeof(TradeUI), nameof(TradeUI.ValidForTrade),
			new Type[] { typeof(GameObject), typeof(GameObject), typeof(GameObject),
				typeof(float), typeof(bool) })]
		private static class ValidForTradePatch
		{
			private static bool Prefix(GameObject Object, ref bool __result)
			{
				if (Safe(Object)) return true;
				__result = false;
				return false;
			}
		}

		[HarmonyPatch(typeof(TradeUI), nameof(TradeUI.PerformOffer),
			new Type[] { typeof(int), typeof(bool), typeof(GameObject),
				typeof(TradeUI.TradeScreenMode), typeof(List<TradeEntry>[]), typeof(int[][]) })]
		private static class PerformOfferPatch
		{
			private static bool Prefix(List<TradeEntry>[] Objects, int[][] NumberSelected,
				ref TradeUI.OfferStatus __result)
			{
				OfferActive = false;
				if (!SelectedRowsSafe(Objects, NumberSelected))
				{
					__result = TradeUI.OfferStatus.REFRESH;
					return false;
				}
				OfferActive = true;
				return true;
			}

			private static Exception Finalizer(Exception __exception)
			{
				OfferActive = false;
				return __exception;
			}
		}

		/// <summary>Container callbacks run after PerformOffer's prefix. Refuse any trade-time split
		/// if such a callback stamps the selected stack before native removal.</summary>
		[HarmonyPatch(typeof(GameObject), nameof(GameObject.SplitStack),
			new Type[] { typeof(int), typeof(GameObject), typeof(bool) })]
		private static class TradeSplitPatch
		{
			private static bool Prefix(GameObject __instance, ref GameObject __result)
			{
				if (!OfferActive || Safe(__instance)) return true;
				__result = null;
				return false;
			}
		}

		[HarmonyPatch(typeof(TradeUI), "TryRemove",
			new Type[] { typeof(GameObject), typeof(GameObject), typeof(List<GameObject>),
				typeof(List<GameObject>), typeof(bool) })]
		private static class TradeRemovePatch
		{
			private static bool Prefix(GameObject Object, ref bool __result)
			{
				if (!OfferActive || Safe(Object)) return true;
				__result = false;
				return false;
			}
		}
	}
}
