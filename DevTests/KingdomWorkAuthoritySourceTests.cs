#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source lock for the engine edge: resident rows own crew, and one shared classifier
	/// owns the kind published by work rows and resident posts.</summary>
	public class KingdomWorkAuthoritySourceTests
	{
		private static string Source(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		[Test]
		public void CheckInRefreshesResidentAuthorityBeforeItBuildsWorkRows()
		{
			string city = KingdomCityLogicalSource.Read();
			int checkIn = city.IndexOf("public static void CheckIn", StringComparison.Ordinal);
			int roster = city.IndexOf("state = KingdomResidents.ReadRoster(System, Z, Survey, state, TimeTicks)",
				checkIn, StringComparison.Ordinal);
			int works = city.IndexOf("state = ReadWorks(state, Z, Survey)", checkIn,
				StringComparison.Ordinal);
			Assert.Greater(checkIn, 0);
			Assert.Greater(roster, checkIn);
			Assert.Greater(works, roster);

			int readWorks = city.IndexOf("private static KingdomCityState ReadWorks",
				StringComparison.Ordinal);
			int audit = city.IndexOf("public static string AuditLine", readWorks,
				StringComparison.Ordinal);
			string body = city.Substring(readWorks, audit - readWorks);
			StringAssert.Contains("KingdomResidentRules.CrewAssigned(state, Z.ZoneID, workId)",
				body);
			StringAssert.DoesNotContain("column is honestly empty", body);
		}

		[Test]
		public void WorkRowsAndPostsUseOneClassifierForEveryEngineTrait()
		{
			string stations = Source("Simulation/City/KingdomStations.cs");
			StringAssert.Contains("KingdomWorkRules.Classify(new KingdomWorkTraits(", stations);
			StringAssert.Contains("KingdomCrops.FieldOf(Work) != null", stations);
			StringAssert.Contains("KingdomConstructionPresence.ActiveProperty", stations);
			StringAssert.Contains("KingdomAdopt.StoresProperty", stations);
			StringAssert.Contains("Work.HasPart(\"SolarArray\")", stations);
			StringAssert.Contains("Work.HasPart(\"ItemConvertor\")", stations);
			StringAssert.Contains("Work.HasPart(\"LiquidProducer\")", stations);

			string city = KingdomCityLogicalSource.Read();
			int runState = city.IndexOf("private static KingdomWorkRunState RunStateOf",
				StringComparison.Ordinal);
			int crop = city.IndexOf("private static string CropOf", runState,
				StringComparison.Ordinal);
			string body = city.Substring(runState, crop - runState);
			StringAssert.Contains("KingdomStations.KindOf(work)", body);
			StringAssert.DoesNotContain("GetIntProperty(\"KingdomStores\")", body);
			StringAssert.DoesNotContain("GetIntProperty(\"KingdomLarder\")", body);
		}

		[Test]
		public void AssignmentStampsTheExactResidentPostConsumedByTheNextReading()
		{
			string growth = KingdomGrowthLogicalSource.Read();
			int assign = growth.IndexOf("public static void AssignWork", StringComparison.Ordinal);
			int emigrate = growth.IndexOf("public static bool Emigrate", assign,
				StringComparison.Ordinal);
			string body = growth.Substring(assign, emigrate - assign);
			StringAssert.Contains("KingdomResidents.RollRows(System, true)", body);
			StringAssert.Contains("KingdomCityRules.StableId(work.ID)", body);
			StringAssert.Contains("KingdomStations.KindOf(work)", body);
			StringAssert.Contains("KingdomStations.Post(available[at], postId, postKind)", body);
		}
	}
}
#endif
