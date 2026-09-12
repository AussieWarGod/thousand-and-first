#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>VALUE tests for the zone-wide debit expectation lifecycle-next consults (run 46b:
	/// production paid from the other dedicated store and a single-store census refused).</summary>
	[TestFixture]
	public sealed class KingdomQuickstartLifecycleDebitRulesTests
	{
		private static readonly string[] Two = { "503", "1204" };

		[Test]
		public void ADropFromEitherStoreSatisfiesTheZoneWideExpectation()
		{
			ClassicAssert.IsTrue(KingdomQuickstartLifecycleDebitRules.Judge(Two, new[] { 3, 2 }, new[] { 2, 2 }, 1, out string failure), failure);
			ClassicAssert.IsTrue(KingdomQuickstartLifecycleDebitRules.Judge(Two, new[] { 3, 2 }, new[] { 3, 1 }, 1, out failure), failure);
			ClassicAssert.IsNull(failure);
		}

		[Test]
		public void AnUnchangedTotalOrAnOverDebitRefusesNamingEveryStore()
		{
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleDebitRules.Judge(Two, new[] { 3, 2 }, new[] { 3, 2 }, 1, out string failure));
			StringAssert.Contains("moved by 0, not the design's exact 1", failure);
			StringAssert.Contains("store=503 timber=3->3 store=1204 timber=2->2", failure);
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleDebitRules.Judge(Two, new[] { 3, 2 }, new[] { 2, 1 }, 1, out failure));
			StringAssert.Contains("moved by 2", failure);
		}

		[Test]
		public void AStoreThatGainsRefusesEvenWhenTheTotalDropIsExact()
		{
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleDebitRules.Judge(Two, new[] { 3, 2 }, new[] { 1, 3 }, 1, out string failure));
			StringAssert.Contains("store 1204 GAINED timber", failure);
		}

		[Test]
		public void AChangedStoreSetOrNoStoreRefuses()
		{
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleDebitRules.Judge(Two, new[] { 3, 2 }, new[] { 2 }, 1, out string failure));
			StringAssert.Contains("store set changed", failure);
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleDebitRules.Judge(new string[0], new int[0], new int[0], 1, out failure));
			StringAssert.Contains("no dedicated store", failure);
		}

		[Test]
		public void TheClauseNamesEveryStoreAndBothTotals()
		{
			ClassicAssert.AreEqual("stores=2 timberBefore=5 timberAfter=4 store=503 timber=3->2 store=1204 timber=2->2",
				KingdomQuickstartLifecycleDebitRules.Describe(Two, new[] { 3, 2 }, new[] { 2, 2 }));
		}
	}
}
#endif
