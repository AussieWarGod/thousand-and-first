#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomContainerCatchUpRuntimeSourceTests
	{
		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static string Slice(string source, string start, string end)
		{
			int at = source.IndexOf(start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(at, 0, start);
			int until = source.IndexOf(end, at + start.Length, StringComparison.Ordinal);
			Assert.Greater(until, at, end);
			return source.Substring(at, until - at);
		}

		[Test]
		public void RuntimePlansAndSettlesEverySurveyedContainerThenChargesMeasuredSpend()
		{
			string source = Source(Path.Combine("Simulation", "City", "KingdomCity.cs"));
			string reify = Slice(source, "private static KingdomCityState Reify(",
				"private static List<GameObject> Posted(");
			StringAssert.Contains("ContainerGround.Take(Survey)", reify);
			StringAssert.Contains("KingdomContainerCatchUpRules.TryMeasure", reify);
			StringAssert.Contains("KingdomContainerCatchUpRules.TrySettle", reify);
			StringAssert.Contains("new KingdomReifySpend(heavySpent, mediumSpent", reify);
			int settlement = reify.LastIndexOf("KingdomContainerCatchUpRules.TrySettle", StringComparison.Ordinal);
			int charge = reify.IndexOf("Charge(System, TimeTicks, spend)", StringComparison.Ordinal);
			Assert.Greater(charge, settlement, "planned work was charged before physical callbacks");
			Assert.IsFalse(reify.Contains("waterSeen ? 1"));
			Assert.IsFalse(reify.Contains("foodSeen ? 1"));
			Assert.IsFalse(reify.Contains("StoreHarvest"), "deferred reify is not a second harvest-loss event");
		}

		[Test]
		public void ReceiptUsesPostMutationGroundDemandRatherThanCityKindProxy()
		{
			string source = Source(Path.Combine("Simulation", "City", "KingdomCity.cs"));
			StringAssert.Contains("Receipt(Z.ZoneID, spend, watch, GroundDemandThirds(Z, survey, written, index))", source);
			string receipt = Slice(source, "private static int GroundDemandThirds(",
				"private static KingdomCityState Carry(");
			StringAssert.Contains("ContainerGround.Take(Survey)", receipt);
			StringAssert.Contains("measured.OwedThirds", receipt);
			StringAssert.Contains("Posted(Z, Survey, stations).Count", receipt);
		}

		[Test]
		public void ExactGroundCallbacksPublishOnlyMeasuredWaterAndFoodDeltas()
		{
			string survey = Source(Path.Combine("Growth", "KingdomSurvey.cs"));
			string food = Slice(survey, "public int StoreFoodIn(", "private sealed class SpoilFrame");
			string water = Slice(survey, "public int StoreIn(", "public int DrawFromPools(");
			StringAssert.Contains("heldAfter != heldBefore + 1", food);
			StringAssert.Contains("FoodStored += accepted", food);
			StringAssert.Contains("int added = Store.Volume - before", water);
			StringAssert.Contains("StoredWater += added", water);
			StringAssert.Contains("StorageSpace -= added", water);
		}

		[Test]
		public void WorstEnvelopeIsDerivedFromLiveRails()
		{
			string source = Source(Path.Combine("Simulation", "City", "KingdomCatchUpRules.cs"));
			StringAssert.Contains("KingdomRules.MaxCivicContainersPerZone + KingdomRules.MaxPopulation", source);
			Assert.IsFalse(source.Contains("WorstBacklogUnits = 232"));
		}

		[Test]
		public void PlotFurnitureCannotMultiplyOneCommissionIntoCivicStoreRows()
		{
			string plot = Source(Path.Combine("Growth", "KingdomPlot2.cs"));
			string durable = Slice(plot, "private static bool FurnishDurable(",
				"private static bool TryFreezeFurnishPlan(");
			string legacy = Slice(plot, "private static bool FurnishLegacyDurable(",
				"private static bool WriteLegacyFurnishPlan(");
			Assert.IsFalse(durable.Contains("SetIntProperty(\"KingdomStores\", 1)"));
			Assert.IsFalse(legacy.Contains("SetIntProperty(\"KingdomStores\", 1)"));
			string survey = Source(Path.Combine("Growth", "KingdomSurvey.cs"));
			StringAssert.Contains("item.GetIntProperty(KingdomPlots.PlotPartProperty) == 1", survey);
			StringAssert.Contains("item.SetIntProperty(\"KingdomStores\", 0)", survey);
		}
	}
}
#endif
