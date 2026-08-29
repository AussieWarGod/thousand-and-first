#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomShopStockSourceTests
	{
		[Test]
		public void RuntimeDisablesAutomaticAndReplacementRestock()
		{
			string source = TestMain.ReadRepositoryText(
				"Growth/KingdomGrowth.z18.StageAndShops.cs");
			StringAssert.Contains("Restocker.Chance = 0;", source);
			StringAssert.Contains("Restocker.RestockFrequency = long.MaxValue;", source);
			StringAssert.Contains("Restocker.LastRestockTick = Math.Max(1L, The.Game.TimeTicks);",
				source);
			StringAssert.Contains("System.ShopTier = Tier;", source);
			StringAssert.Contains("KingdomShopStockRules.Classify", source);
			StringAssert.Contains("IssueIntentProperty", source);
			StringAssert.Contains("local market-output batch", source);
			StringAssert.Contains("StampMarketOutput", source);
			StringAssert.Contains("ItemSourceProperty", source);
			StringAssert.Contains("item.SetIntProperty(\"norestock\", 1);", source);
			StringAssert.Contains("ProtectPriorMarketStock", source);
			StringAssert.Contains("RemoveIfNull: true", source);
			StringAssert.Contains("IntentReceipt(sourceId)", source);
			StringAssert.Contains("RecoverMarketOutputCut", source);
			StringAssert.DoesNotContain("Chance = 100;", source);
			StringAssert.DoesNotContain("ExactExternalReceipt", source);
			StringAssert.DoesNotContain("outside consignment", source);
			Assert.AreEqual(1, Count(source, "restocker.PerformRestock(Silent: true);"),
				"only one local market-output batch per new tier may stock inventory");
			int prepare = source.IndexOf("ConfigureFiniteStock", System.StringComparison.Ordinal);
			int intent = source.IndexOf("merchant.SetStringProperty(KingdomShopStockRules.IssueIntentProperty",
				System.StringComparison.Ordinal);
			int commit = source.IndexOf("System.ShopTier = Tier;", System.StringComparison.Ordinal);
			int callback = source.IndexOf("restocker.PerformRestock", System.StringComparison.Ordinal);
			Assert.Greater(prepare, 0); Assert.Greater(intent, prepare);
			Assert.Greater(commit, intent); Assert.Greater(callback, commit);
		}

		private static int Count(string value, string token)
		{
			int count = 0;
			for (int at = 0; (at = value.IndexOf(token, at,
				System.StringComparison.Ordinal)) >= 0; at += token.Length) count++;
			return count;
		}
	}
}
#endif
