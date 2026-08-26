#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomResearchInteractionSourceTests
	{
		[Test]
		public void SubjectSelectionLivesOnARealBenchAndNotTheCharterReading()
		{
			string inquiry = TestMain.ReadRepositoryText(Path.Combine("Growth",
				"KingdomInquiry.cs"));
			string research = TestMain.ReadRepositoryText(Path.Combine("Growth",
				"KingdomResearch.cs"));
			string zoning = TestMain.ReadRepositoryText(Path.Combine("Growth",
				"KingdomZoning.cs"));
			StringAssert.Contains("GetInventoryActionsEvent.ID", inquiry);
			StringAssert.Contains("set the city's research subject", inquiry);
			StringAssert.Contains("KingdomResearch.OpenBench(ParentObject, E.Actor)", inquiry);
			StringAssert.Contains("public static void OpenBench(GameObject Bench, GameObject Actor)", research);
			StringAssert.Contains("Actor.CurrentZone != zone", research);
			StringAssert.Contains("system.ClaimedZones.Contains(zone.ZoneID)", research);
			StringAssert.Contains("Bench.HasPart<XRL.World.Parts.r_KingdomInquiry>()", research);
			StringAssert.Contains("TakeUp(system, subjects[chosen].Key", research);
			StringAssert.DoesNotContain("Set the keepers a thing to work out", zoning);
			StringAssert.DoesNotContain("private static void SetSubject", zoning);
			Assert.AreEqual(0, Occurrences(zoning, "KingdomResearch.TakeUp("),
				"the Charter/keepers reading must have no hidden research mutation route");
		}

		private static int Occurrences(string text, string value)
		{
			int count = 0;
			for (int at = 0; (at = text.IndexOf(value, at, StringComparison.Ordinal)) >= 0;
				at += value.Length) count++;
			return count;
		}
	}
}
#endif
