#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomRulesTests
	{
		[TestCase(GrowthStage.Camp, 50)]
		[TestCase(GrowthStage.Steading, 40)]
		[TestCase(GrowthStage.Village, 30)]
		[TestCase(GrowthStage.Town, 20)]
		[TestCase(GrowthStage.City, 10)]
		public void SpilloverPercent(GrowthStage stage, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.SpilloverPercent(stage));
		}

		[TestCase(100, GrowthStage.Camp, 50)]
		[TestCase(10, GrowthStage.Camp, 5)]
		[TestCase(-10, GrowthStage.Camp, -5)]
		[TestCase(1, GrowthStage.Camp, 0)]
		[TestCase(-1, GrowthStage.Camp, 0)]
		[TestCase(0, GrowthStage.Camp, 0)]
		[TestCase(100, GrowthStage.Steading, 40)]
		[TestCase(75, GrowthStage.Town, 15)]
		[TestCase(100, GrowthStage.City, 10)]
		[TestCase(-200, GrowthStage.City, -20)]
		public void SpilloverDelta(int repDelta, GrowthStage stage, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.SpilloverDelta(repDelta, stage));
		}

		[TestCase(0, 3600L)]
		[TestCase(1, 4200L)]
		[TestCase(10, 9600L)]
		[TestCase(50, 33600L)]
		public void ArrivalIntervalTicks(int population, long expected)
		{
			Assert.AreEqual(expected, KingdomRules.ArrivalIntervalTicks(population));
		}

		[TestCase(0, 0)]
		[TestCase(3, 0)]
		[TestCase(4, 1)]
		[TestCase(7, 1)]
		[TestCase(8, 2)]
		[TestCase(50, 12)]
		public void UpkeepDrams(int population, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.UpkeepDrams(population));
		}

		[TestCase(0, 0, GrowthStage.Camp)]
		[TestCase(4, 1000, GrowthStage.Camp)]
		[TestCase(5, 15, GrowthStage.Camp)]
		[TestCase(5, 16, GrowthStage.Steading)]
		[TestCase(11, 1000, GrowthStage.Steading)]
		[TestCase(12, 63, GrowthStage.Steading)]
		[TestCase(12, 64, GrowthStage.Village)]
		[TestCase(25, 255, GrowthStage.Village)]
		[TestCase(25, 256, GrowthStage.Town)]
		[TestCase(50, 1023, GrowthStage.Town)]
		[TestCase(50, 1024, GrowthStage.City)]
		[TestCase(100, 500, GrowthStage.Town)]
		[TestCase(100, 0, GrowthStage.Camp)]
		public void StageFor(int population, int drams, GrowthStage expected)
		{
			Assert.AreEqual(expected, KingdomRules.StageFor(population, drams));
		}

		[TestCase(0, 100, 100, 0)]
		[TestCase(5, 100, 100, 10)]
		[TestCase(5, 3, 100, 3)]
		[TestCase(5, 100, 4, 4)]
		[TestCase(5, 0, 100, 0)]
		[TestCase(5, 100, 0, 0)]
		[TestCase(50, 30, 200, 30)]
		public void FetchableDrams(int population, int openWater, int storageSpace, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.FetchableDrams(population, openWater, storageSpace));
		}

		[TestCase("hello happened", 0, "It is said that hello happened.")]
		[TestCase("hello happened", 5, "Some deny that hello happened.")]
		[TestCase("hello happened", 6, "It is said that hello happened.")]
		[TestCase("hello happened", -1, "Some deny that hello happened.")]
		public void ComposeOutsider(string text, int roll, string expected)
		{
			Assert.AreEqual(expected, KingdomRules.ComposeOutsider(text, roll));
		}

		[TestCase("JoppaWorld.11.22.1.1.10", true, "JoppaWorld", 34, 67, 10)]
		[TestCase("JoppaWorld.0.0.0.0.10", true, "JoppaWorld", 0, 0, 10)]
		[TestCase("JoppaWorld.5.3.2.1.15", true, "JoppaWorld", 17, 10, 15)]
		[TestCase("NorthSheva.1.1.1.1", false, null, 0, 0, 0)]
		[TestCase("garbage", false, null, 0, 0, 0)]
		[TestCase("", false, null, 0, 0, 0)]
		[TestCase(null, false, null, 0, 0, 0)]
		[TestCase("JoppaWorld.a.22.1.1.10", false, null, 0, 0, 0)]
		public void TryParseZoneID(string zoneID, bool expectedOk, string world, int gx, int gy, int z)
		{
			bool ok = KingdomRules.TryParseZoneID(zoneID, out var w, out var x, out var y, out var depth);
			Assert.AreEqual(expectedOk, ok);
			if (expectedOk)
			{
				Assert.AreEqual(world, w);
				Assert.AreEqual(gx, x);
				Assert.AreEqual(gy, y);
				Assert.AreEqual(z, depth);
			}
		}

		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.1.2.10", true)]
		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.2.2.10", true)]
		[TestCase("JoppaWorld.11.22.2.1.10", "JoppaWorld.12.22.0.1.10", true)]
		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.1.1.10", false)]
		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.22.1.1.11", false)]
		[TestCase("JoppaWorld.11.22.1.1.10", "JoppaWorld.11.23.1.1.10", false)]
		[TestCase("JoppaWorld.11.22.1.1.10", "OtherWorld.11.22.1.2.10", false)]
		[TestCase("garbage", "JoppaWorld.11.22.1.2.10", false)]
		public void ZonesAdjacent(string a, string b, bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.ZonesAdjacent(a, b));
		}

		[TestCase("Joppa:100", true, "Joppa", 100)]
		[TestCase("Gyre Wights:-50", true, "Gyre Wights", -50)]
		[TestCase("Barathrumites: 250 ", true, "Barathrumites", 250)]
		[TestCase("SultanCult1:0", true, "SultanCult1", 0)]
		[TestCase("a:b:5", true, "a:b", 5)]
		[TestCase("nocolon", false, null, 0)]
		[TestCase(":100", false, null, 0)]
		[TestCase("Joppa:", false, null, 0)]
		[TestCase("Joppa:abc", false, null, 0)]
		[TestCase("", false, null, 0)]
		[TestCase(null, false, null, 0)]
		public void TryParseFactionAmount(string parameter, bool expectedOk, string expectedFaction, int expectedAmount)
		{
			bool ok = KingdomRules.TryParseFactionAmount(parameter, out var faction, out var amount);
			Assert.AreEqual(expectedOk, ok);
			if (expectedOk)
			{
				Assert.AreEqual(expectedFaction, faction);
				Assert.AreEqual(expectedAmount, amount);
			}
		}
	}
}
#endif
