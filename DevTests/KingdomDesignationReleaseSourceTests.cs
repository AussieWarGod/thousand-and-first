#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomDesignationReleaseSourceTests
	{
		[Test]
		public void EveryReleaseRoutesThroughOneFailClosedAuthorityBeforeItsWrite()
		{
			string charter = Read("Core/KingdomCharterPart.Vessels.cs");
			Ordered(charter, "KingdomDesignationReleaseAuthority.TryCanRelease(",
				"vessel.SetIntProperty(\"KingdomStores\", 0)");
			int larder = charter.IndexOf("store.SetIntProperty(\"KingdomLarder\", 0)",
				StringComparison.Ordinal);
			int larderGate = charter.LastIndexOf(
				"KingdomDesignationReleaseAuthority.TryCanRelease(", larder,
				StringComparison.Ordinal);
			Assert.Greater(larderGate, -1);
			Assert.Less(larderGate, larder);

			string stock = Read("Growth/KingdomMaterials.05.StockpileAndPaymentGates.cs");
			Ordered(stock, "if (IsStockpile(Container))",
				"KingdomDesignationReleaseAuthority.TryCanRelease(",
				"Container.SetIntProperty(StockpileProperty, 0)");
		}

		[Test]
		public void AuthorityComposesConstructionDeliveryPurposeAndCargoOwnership()
		{
			string authority = Read("Growth/KingdomDesignationReleaseAuthority.cs");
			Ordered(authority, "KingdomConstructionInputLeaseAuthority.TryCapture",
				"TryProveCustodyFree(store, leases", "TryCanReleaseDesignation",
				"TryCanReleasePurposeStore");
			Ordered(authority, "private static bool TryProveCustodyFree",
				"KingdomOrdinaryCustody.TryCollect", "leases.ContainsHolder");
			StringAssert.Contains("KingdomPurpose.HasProtectedCargoEvidence", authority);

			string central = Read(
				"Simulation/City/KingdomCentralLogistics.18.DesignationAuthority.cs");
			StringAssert.Contains("system.Jobs.TryRead", central);
			StringAssert.Contains("out bool canRelease", central);
			StringAssert.Contains("row.DeliverySourceObjectId", central);
			StringAssert.Contains("row.DeliveryTargetObjectId", central);

			string purpose = Read(
				"Growth/KingdomPurposePortfolio.DesignationAuthority.cs");
			StringAssert.Contains("pair.Phase == KingdomPurposePairPhase.Dormant", purpose);
			foreach (string field in new[] { "FirstInputStoreId", "FirstOutputStoreId",
				"SecondInputStoreId", "SecondOutputStoreId", "SourceInputStoreId",
				"SourceOutputStoreId", "DestinationInputStoreId" })
				StringAssert.Contains(field, purpose);
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
