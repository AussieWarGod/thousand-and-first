#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomUpgradeContentRulesTests
	{
		[TestCase(-1, false)]
		[TestCase(0, true)]
		[TestCase(4096, true)]
		[TestCase(4097, false)]
		public void ManifestCardinalityIsBoundedBeforeMutation(int count, bool expected)
		{
			Assert.AreEqual(expected, KingdomUpgradeContentRules.ManifestCardinalityValid(count));
		}

		[Test]
		public void ManifestSlotsReconcileMovedPendingAndFutureItems()
		{
			Assert.AreEqual(KingdomHandoverManifestSlot.Destination,
				KingdomUpgradeContentRules.ExpectedSlot(0, 3, 1, -1, 0));
			Assert.AreEqual(KingdomHandoverManifestSlot.Source,
				KingdomUpgradeContentRules.ExpectedSlot(1, 3, 1, -1, 0));
			Assert.AreEqual(KingdomHandoverManifestSlot.Pending,
				KingdomUpgradeContentRules.ExpectedSlot(1, 3, 1, 1, 2));
			Assert.AreEqual(KingdomHandoverManifestSlot.Source,
				KingdomUpgradeContentRules.ExpectedSlot(2, 3, 1, 1, 2));
			Assert.AreEqual(KingdomHandoverManifestSlot.Invalid,
				KingdomUpgradeContentRules.ExpectedSlot(1, 3, 0, 1, 2));
		}

		[Test]
		public void LiquidAdmissionRejectsOpenAndCallbackSensitiveEndpoints()
		{
			Assert.IsTrue(KingdomUpgradeContentRules.LiquidEndpointSafe(16, false, false));
			Assert.IsFalse(KingdomUpgradeContentRules.LiquidEndpointSafe(-1, false, false));
			Assert.IsFalse(KingdomUpgradeContentRules.LiquidEndpointSafe(16, true, false));
			Assert.IsFalse(KingdomUpgradeContentRules.LiquidEndpointSafe(16, false, true));
		}
	}
}
#endif
