#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.AreEqual(expected, KingdomUpgradeContentRules.ManifestCardinalityValid(count));
		}

		[Test]
		public void ManifestSlotsReconcileMovedPendingAndFutureItems()
		{
			ClassicAssert.AreEqual(KingdomHandoverManifestSlot.Destination,
				KingdomUpgradeContentRules.ExpectedSlot(0, 3, 1, -1, 0));
			ClassicAssert.AreEqual(KingdomHandoverManifestSlot.Source,
				KingdomUpgradeContentRules.ExpectedSlot(1, 3, 1, -1, 0));
			ClassicAssert.AreEqual(KingdomHandoverManifestSlot.Pending,
				KingdomUpgradeContentRules.ExpectedSlot(1, 3, 1, 1, 2));
			ClassicAssert.AreEqual(KingdomHandoverManifestSlot.Source,
				KingdomUpgradeContentRules.ExpectedSlot(2, 3, 1, 1, 2));
			ClassicAssert.AreEqual(KingdomHandoverManifestSlot.Invalid,
				KingdomUpgradeContentRules.ExpectedSlot(1, 3, 0, 1, 2));
		}

		[Test]
		public void LiquidAdmissionRejectsOpenAndCallbackSensitiveEndpoints()
		{
			ClassicAssert.IsTrue(KingdomUpgradeContentRules.LiquidEndpointSafe(16, false, false));
			ClassicAssert.IsFalse(KingdomUpgradeContentRules.LiquidEndpointSafe(-1, false, false));
			ClassicAssert.IsFalse(KingdomUpgradeContentRules.LiquidEndpointSafe(16, true, false));
			ClassicAssert.IsFalse(KingdomUpgradeContentRules.LiquidEndpointSafe(16, false, true));
		}
	}
}
#endif
