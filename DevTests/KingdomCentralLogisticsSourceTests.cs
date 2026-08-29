#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomCentralLogisticsSourceTests
	{
		[Test]
		public void LogicalFamilyKeepsScalarManifestAndRouteOrder()
		{
			string source = KingdomCentralLogisticsLogicalSource.Read();
			Ordered(source,
				"internal readonly struct KingdomManifestTripView",
				"internal readonly struct KingdomManifestReservation",
				"internal const string TargetReceiptProperty",
				"internal static bool TryQueueScalar(",
				"internal static int StartPlanned(",
				"internal static int RecoverPreparedSources(",
				"internal static int SettleScalarArrivals(",
				"internal static bool TryPrepareManifestReservation(",
				"internal static bool TryActivateManifestReservation(",
				"internal static bool TryMaterializeManifestArrival(",
				"private static List<KingdomJobRow> OwnerRows(",
				"private static bool TryBuildManifestRoute(",
				"private static bool TryPassage(",
				"private static bool TryExactScalarAmount(",
				"private static void SweepTarget(",
				"internal static bool TryPrepareConstructionInputReservation(",
				"internal static bool TryActivateConstructionInputReservations(",
				"internal static bool TryConstructionInputTrip(",
				"internal static bool TryAcknowledgeConstructionInputPickup(",
				"internal static bool TryMaterializeConstructionInputArrival(",
				"internal static bool TryAcknowledgeConstructionInputLanded(",
				"internal static bool TryCloseConstructionInputTrip(",
				"internal static bool TryReleaseUndebitedConstructionInputOwner(",
				"internal static bool TryQuarantineConstructionInputOwner(",
				"internal readonly struct KingdomConstructionInputRouteProof",
				"internal static bool TryDescribeConstructionInputReservation(");
		}

		[Test]
		public void DeclarationsAndConstantsHaveOneOwner()
		{
			string source = KingdomCentralLogisticsLogicalSource.Read();
			Assert.AreEqual(1, Count(source, "internal readonly struct KingdomManifestTripView"));
			Assert.AreEqual(1, Count(source, "internal readonly struct KingdomManifestReservation"));
			Assert.AreEqual(17, Count(source, "internal static partial class KingdomCentralLogistics"));
			Assert.AreEqual(1, Count(source, "TargetReceiptProperty = \"KingdomDeliveryReceipt\""));
			Assert.AreEqual(1, Count(source, "FoodReceiptJobProperty = \"KingdomDeliveryReceiptJob\""));
			StringAssert.DoesNotContain("internal static class KingdomCentralLogistics", source);
		}

		[Test]
		public void ExactCargoAndReceiptHelpersRemainInLogicalAuthority()
		{
			string source = KingdomCentralLogisticsLogicalSource.Read();
			StringAssert.Contains("SystemLongDistanceMoveTo", source);
			StringAssert.Contains("PublishMarkedFoodDelta(survey, target, jobId, before);", source);
			StringAssert.Contains("survey.SynchronizeReceiptObject(target);", source);
			StringAssert.Contains("KingdomLogisticsRules.TryPlanSnapshot", source);
			StringAssert.Contains("TryDebitScalar(survey, seed, total", source);
			StringAssert.Contains("AvailableIn(candidate, leases)", source);
			StringAssert.Contains("target.Inventory.AddObject(food, Silent: true, NoStack: true)", source);
			StringAssert.Contains("survey.RefreshFoodTopology();", source);
		}

		private static void Ordered(string source, params string[] markers)
		{
			int position = -1;
			for (int i = 0; i < markers.Length; i++)
			{
				int next = source.IndexOf(markers[i], position + 1, StringComparison.Ordinal);
				Assert.Greater(next, position, markers[i]);
				position = next;
			}
		}

		private static int Count(string source, string token)
		{
			int count = 0;
			for (int at = 0; (at = source.IndexOf(token, at, StringComparison.Ordinal)) >= 0;
				at += token.Length) count++;
			return count;
		}
	}
}
#endif
