#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomObservedBenefitProjectionRulesTests
	{
		[Test]
		public void HostedWardFoldsIntoLiveCarriesExactlyOnce()
		{
			List<KindAmount> live = new List<KindAmount> {
				new KindAmount("luxury", 4), new KindAmount("order", 4) };
			ClassicAssert.IsTrue(KingdomObservedBenefitProjectionRules.TryProject(
				live, 8, 2, 100, out List<KindAmount> projected, out string failure), failure);
			ClassicAssert.AreEqual(8, KingdomObservedBenefitProjectionRules.Amount(projected, "roof"));
			ClassicAssert.AreEqual(6, KingdomObservedBenefitProjectionRules.Amount(projected, "luxury"));
			ClassicAssert.AreEqual(4, KingdomObservedBenefitProjectionRules.Amount(projected, "order"));
			ClassicAssert.AreEqual(10, KingdomObservedBenefitProjectionRules.PhysicalLift(projected));
		}

		[Test]
		public void OnlyHostedRowsReceiveShellEffectivenessAtProjectionBoundary()
		{
			List<KindAmount> live = new List<KindAmount> { new KindAmount("luxury", 4) };
			ClassicAssert.IsTrue(KingdomObservedBenefitProjectionRules.TryProject(
				live, 8, 2, 50, out List<KindAmount> projected, out string failure), failure);
			ClassicAssert.AreEqual(4, KingdomObservedBenefitProjectionRules.Amount(projected, "roof"));
			ClassicAssert.AreEqual(5, KingdomObservedBenefitProjectionRules.Amount(projected, "luxury"));
		}

		[Test]
		public void MalformedProjectionFailsClosed()
		{
			ClassicAssert.IsFalse(KingdomObservedBenefitProjectionRules.TryProject(
				new List<KindAmount> { new KindAmount("luxury", -1) }, 0, 0, 100,
				out List<KindAmount> projected, out string failure));
			ClassicAssert.IsNull(projected);
			StringAssert.Contains("malformed", failure);
		}
	}
}
#endif
