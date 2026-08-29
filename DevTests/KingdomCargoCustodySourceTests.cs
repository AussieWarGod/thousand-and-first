#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomCargoCustodySourceTests
	{
		[Test]
		public void CustodyWalkIsBoundedCycleRejectingAndCoversAllEngineContents()
		{
			string custody = Read("Growth/KingdomOrdinaryCustody.cs");
			StringAssert.Contains("MaxNodes = 1024", custody);
			StringAssert.Contains("MaxDepth = 32", custody);
			Ordered(custody, "GetContents(new List<GameObject>())",
				"contents.Count > 0 && depths[cursor] >= MaxDepth",
				"contents.Count > MaxNodes - graph.Count",
				"ContainsReference(graph, child)");
			StringAssert.Contains("KingdomPurpose.HasProtectedCargoEvidence(graph[i])", custody);

			string leases = Read("Growth/KingdomConstructionInputLeaseAuthority.Custody.cs");
			Ordered(leases, "KingdomOrdinaryCustody.TryCollect", "TryCapture(",
				"KingdomPurpose.HasProtectedCargoEvidence(held)", "IsLeased(snapshot, held)");
		}

		[Test]
		public void NativeTradeRejectsListBuildOfferExecutionAndLateContainerMutation()
		{
			string trade = Read("Trade/KingdomNativeTradeCargoFence.cs");
			StringAssert.Contains("HarmonyPatch(typeof(TradeUI), nameof(TradeUI.ValidForTrade)",
				trade);
			StringAssert.Contains("HarmonyPatch(typeof(TradeUI), nameof(TradeUI.PerformOffer)",
				trade);
			Ordered(trade, "SelectedRowsSafe(Objects, NumberSelected)",
				"__result = TradeUI.OfferStatus.REFRESH", "OfferActive = true");
			StringAssert.Contains("HarmonyPatch(typeof(GameObject), nameof(GameObject.SplitStack)",
				trade);
			StringAssert.Contains("HarmonyPatch(typeof(TradeUI), \"TryRemove\"", trade);
			StringAssert.Contains("KingdomOrdinaryCustody.TryProveNoProtectedCargo", trade);
		}

		[Test]
		public void EveryNamedOrdinaryConsumerUsesRecursiveOrEmptyCustodyProof()
		{
			StringAssert.Contains("KingdomOrdinaryCustody.TryProveEmpty(Object, out _)",
				Read("Growth/KingdomMaterials.03.StockClassification.cs"));
			StringAssert.Contains("KingdomOrdinaryCustody.TryProveEmpty(target, out _)",
				Read("Growth/KingdomMaterials.StrikeProtection.cs"));
			Ordered(Read("Growth/KingdomMaterials.14.ClearanceWork.cs"),
				"KingdomOrdinaryCustody.TryProveEmpty(item, out _)", "item.Obliterate");
			StringAssert.Contains("TryObjectGraphAvailableForOrdinaryTransfer(item, out _)",
				Read("Quests/KingdomBounty.Transfer.cs"));
			StringAssert.Contains("TryObjectGraphAvailableForOrdinaryTransfer(Item, out _)",
				Read("Experience/KingdomGuestbook.z01b.MarketHandoff.cs"));
			string salvage = Read("Growth/KingdomSalvage.cs");
			Ordered(salvage, "TryObjectGraphAvailableForOrdinaryTransfer(Machine, out Failure)",
				"Machine.SetIntProperty(CertifiedProperty, 0)");
			Assert.LessOrEqual(Read("Trade/KingdomNativeTradeCargoFence.cs").Split('\n').Length,
				300);
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }

		private static void Ordered(string source, params string[] markers)
		{
			int at = -1;
			for (int i = 0; i < markers.Length; i++)
			{
				int next = source.IndexOf(markers[i], at + 1, StringComparison.Ordinal);
				Assert.Greater(next, at, markers[i]);
				at = next;
			}
		}
	}
}
#endif
