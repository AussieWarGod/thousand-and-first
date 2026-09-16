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
			ClassicAssert.IsTrue(KingdomQuickstartLifecycleDebitRules.Judge(Two, new[] { 3, 2 }, new[] { 2, 2 }, null, null, 1, out string failure), failure);
			ClassicAssert.IsTrue(KingdomQuickstartLifecycleDebitRules.Judge(Two, new[] { 3, 2 }, new[] { 3, 1 }, null, null, 1, out failure), failure);
			ClassicAssert.IsNull(failure);
		}

		[Test]
		public void AnUnchangedTotalOrAnOverDebitRefusesNamingEveryStore()
		{
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleDebitRules.Judge(Two, new[] { 3, 2 }, new[] { 3, 2 }, null, null, 1, out string failure));
			StringAssert.Contains("moved by 0, not the design's exact 1", failure);
			StringAssert.Contains("store=503 timber=3->3 store=1204 timber=2->2", failure);
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleDebitRules.Judge(Two, new[] { 3, 2 }, new[] { 2, 1 }, null, null, 1, out failure));
			StringAssert.Contains("moved by 2", failure);
		}

		[Test]
		public void AStoreThatGainsRefusesEvenWhenTheTotalDropIsExact()
		{
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleDebitRules.Judge(Two, new[] { 3, 2 }, new[] { 1, 3 }, null, null, 1, out string failure));
			StringAssert.Contains("store 1204 GAINED timber", failure);
		}

		[Test]
		public void AChangedStoreSetOrNoStoreRefuses()
		{
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleDebitRules.Judge(Two, new[] { 3, 2 }, new[] { 2 }, null, null, 1, out string failure));
			StringAssert.Contains("store set changed", failure);
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleDebitRules.Judge(new string[0], new int[0], new int[0], null, null, 1, out failure));
			StringAssert.Contains("no dedicated store", failure);
		}

		/// <summary>PR #183 review: an unread store is never a zero. It refuses first, whether the
		/// arithmetic over the fabricated zero would have balanced or would have shown a gain.</summary>
		[Test]
		public void AnUnreadStoreRefusesFirstWhenTheTotalsWouldBalance()
		{
			// Store 1204 unreadable after the commission (count 0 recorded); 503 dropped by one,
			// so the fabricated arithmetic (3+2 -> 2+0) would read a drop of 3, and with the
			// unread store's count matching before it would read exactly 1: both must refuse
			// on the read failure, never on the numbers.
			string[] unread = { null, "materials stockpile is not a real placed inventory holder" };
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleDebitRules.Judge(Two, new[] { 3, 2 },
				new[] { 2, 2 }, null, unread, 1, out string failure));
			StringAssert.StartsWith("dedicated store 1204 could not be stocked: materials stockpile", failure);
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleDebitRules.Judge(Two, new[] { 3, 2 },
				new[] { 2, 2 }, unread, null, 1, out failure), "unread before the commission");
			StringAssert.StartsWith("dedicated store 1204 could not be stocked", failure);
		}

		[Test]
		public void AnUnreadStoreRefusesOnTheReadNotOnTheGainTheZeroWouldFabricate()
		{
			// Unread BEFORE (recorded 0) and 2 after: the arithmetic would say "GAINED"; the
			// refusal must name the read failure instead.
			string[] unread = { "materials stockpile is not a real placed inventory holder", null };
			ClassicAssert.IsFalse(KingdomQuickstartLifecycleDebitRules.Judge(Two, new[] { 0, 2 },
				new[] { 2, 1 }, unread, null, 1, out string failure));
			StringAssert.StartsWith("dedicated store 503 could not be stocked", failure);
			StringAssert.DoesNotContain("GAINED", failure);
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
