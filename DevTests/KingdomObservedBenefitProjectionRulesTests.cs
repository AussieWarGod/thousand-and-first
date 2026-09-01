#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomObservedBenefitProjectionRulesTests
	{
		[Test]
		public void HostedWardFoldsIntoLiveCarriesExactlyOnce()
		{
			List<KindAmount> live = new List<KindAmount> {
				new KindAmount("luxury", 4), new KindAmount("order", 4) };
			Assert.IsTrue(KingdomObservedBenefitProjectionRules.TryProject(
				live, 8, 2, 100, out List<KindAmount> projected, out string failure), failure);
			Assert.AreEqual(8, KingdomObservedBenefitProjectionRules.Amount(projected, "roof"));
			Assert.AreEqual(6, KingdomObservedBenefitProjectionRules.Amount(projected, "luxury"));
			Assert.AreEqual(4, KingdomObservedBenefitProjectionRules.Amount(projected, "order"));
			Assert.AreEqual(10, KingdomObservedBenefitProjectionRules.PhysicalLift(projected));
		}

		[Test]
		public void OnlyHostedRowsReceiveShellEffectivenessAtProjectionBoundary()
		{
			List<KindAmount> live = new List<KindAmount> { new KindAmount("luxury", 4) };
			Assert.IsTrue(KingdomObservedBenefitProjectionRules.TryProject(
				live, 8, 2, 50, out List<KindAmount> projected, out string failure), failure);
			Assert.AreEqual(4, KingdomObservedBenefitProjectionRules.Amount(projected, "roof"));
			Assert.AreEqual(5, KingdomObservedBenefitProjectionRules.Amount(projected, "luxury"));
		}

		[Test]
		public void MalformedProjectionFailsClosed()
		{
			Assert.IsFalse(KingdomObservedBenefitProjectionRules.TryProject(
				new List<KindAmount> { new KindAmount("luxury", -1) }, 0, 0, 100,
				out List<KindAmount> projected, out string failure));
			Assert.IsNull(projected);
			StringAssert.Contains("malformed", failure);
		}
	}
}
#endif
