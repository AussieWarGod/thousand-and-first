#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomWaterRiteCivicOfficeRulesTests
	{
		[TestCase(false)]
		[TestCase(true)]
		public void TitleOnlyCivicOfficeNeverChangesAnyRiteEligibility(bool holdsOffice)
		{
			foreach (WaterRiteBar baseline in Enum.GetValues(typeof(WaterRiteBar)))
				Assert.AreEqual(baseline,
					KingdomWaterRiteRules.PreserveEligibilityAcrossCivicTitle(
						baseline, holdsOffice), baseline.ToString());
		}

		[Test]
		public void RuntimeNeverMapsCivicOfficeCompatibilityProjectionToTheirOffice()
		{
			string source = KingdomWaterRiteLogicalSource.Read();
			int start = source.IndexOf("private static WaterRiteBar BarFor(",
				StringComparison.Ordinal);
			int end = source.IndexOf("private static bool CouldWalkAway(", start,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0);
			Assert.Greater(end, start);
			string body = source.Substring(start, end - start);
			StringAssert.Contains("PreserveEligibilityAcrossCivicTitle(baseline", body);
			StringAssert.Contains("residentId == System.OfficeHolderResidentId", body);
			StringAssert.DoesNotContain("WaterRiteBar.TheirOffice", body);
		}
	}
}
#endif
