#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomShopStockRulesTests
	{
		[TestCase(-1, 1, 1)]
		[TestCase(0, 0, 1)]
		[TestCase(0, 9, 1)]
		public void MalformedTierRefuses(int issued, int requested, int merchants)
		{
			Assert.AreEqual(KingdomShopStockVerdict.RefusedMalformed,
				KingdomShopStockRules.Classify(issued, requested, merchants));
		}

		[Test]
		public void ExactlyOneMerchantAndNewTierAreRequired()
		{
			Assert.AreEqual(KingdomShopStockVerdict.RefusedNoMerchant,
				KingdomShopStockRules.Classify(0, 1, 0));
			Assert.AreEqual(KingdomShopStockVerdict.RefusedAmbiguousMerchant,
				KingdomShopStockRules.Classify(0, 1, 2));
			Assert.AreEqual(KingdomShopStockVerdict.Issue,
				KingdomShopStockRules.Classify(0, 1, 1));
			Assert.AreEqual(KingdomShopStockVerdict.AlreadyIssued,
				KingdomShopStockRules.Classify(1, 1, 1));
			Assert.AreEqual(KingdomShopStockVerdict.Issue,
				KingdomShopStockRules.Classify(1, 2, 1));
		}

		[Test]
		public void SourceIdentityNamesExactRealmSettlementAndTier()
		{
			string exact = KingdomShopStockRules.SourceId("realm-1", "city-2", 3);
			StringAssert.StartsWith("taf:local-market-output:v1:", exact);
			Assert.AreEqual(91, exact.Length);
			Assert.AreEqual(exact, KingdomShopStockRules.SourceId("realm-1", "city-2", 3));
			Assert.AreNotEqual(exact, KingdomShopStockRules.SourceId("realm-1", "city-2", 4));
			Assert.AreNotEqual(KingdomShopStockRules.SourceId("a:b", "c", 3),
				KingdomShopStockRules.SourceId("a", "b:c", 3));
			Assert.IsNull(KingdomShopStockRules.SourceId(null, "city-2", 3));
			Assert.IsNull(KingdomShopStockRules.SourceId(" realm-1", "city-2", 3));
			Assert.IsNull(KingdomShopStockRules.SourceId("realm-1", "city-2", 9));
		}

		[TestCase(3, 2, KingdomShopStockVerdict.AlreadyIssued)]
		[TestCase(3, 3, KingdomShopStockVerdict.AlreadyIssued)]
		[TestCase(3, 4, KingdomShopStockVerdict.Issue)]
		[TestCase(8, 8, KingdomShopStockVerdict.AlreadyIssued)]
		public void RegressionAndReascentNeverReplayIssuedTier(int issued, int requested,
			KingdomShopStockVerdict expected)
		{
			Assert.AreEqual(expected, KingdomShopStockRules.Classify(issued, requested, 1));
		}

		[TestCase(0, 3, 1)]
		[TestCase(1, 3, 2)]
		[TestCase(2, 3, 3)]
		[TestCase(3, 2, 2)]
		[TestCase(8, 9, 9)]
		public void TierLeapsIssueEachNewlyAttainedTierInOrder(int issued, int attained,
			int expected)
		{
			Assert.AreEqual(expected, KingdomShopStockRules.NextIssueTier(issued, attained));
			if (attained > KingdomShopStockRules.MaximumTier)
				Assert.AreEqual(KingdomShopStockVerdict.RefusedMalformed,
					KingdomShopStockRules.Classify(issued, expected, 1));
		}
	}
}
#endif
