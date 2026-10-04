#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Behavioural coverage row 12: which candidate sites production calls too close.
	/// KingdomFounding.JudgeSite reads GroundIsTooClose for a zone bordering any realm claim, by
	/// KingdomFounding.ZonesAdjacent: KingdomRules.CoordsAdjacent at the global zone coordinates
	/// wx * 3 + zx, wy * 3 + zy, vertical neighbours included. That call parses ids with the
	/// engine's ZoneID.Parse, so these cases run the same law through KingdomRules.TryParseZoneID,
	/// which computes the same coordinates, and pin the production call that makes them equal.
	/// Not native acceptance: the live search still asks JudgeSite itself for every candidate.
	/// </summary>
	public class KingdomSecondCitySiteAdjacencyTests
	{
		private const string Home = "JoppaWorld.8.22.1.1.10";
		private const string EastOfHome = "JoppaWorld.8.22.2.1.10";

		/// <summary>Production's global zone coordinates: wx * 3 + zx, wy * 3 + zy, depth.</summary>
		private static int[] Global(string Id)
		{
			string world;
			int gx;
			int gy;
			int z;
			Assert.That(KingdomRules.TryParseZoneID(Id, out world, out gx, out gy, out z), Is.True, Id);
			Assert.That(world, Is.EqualTo("JoppaWorld"));
			return new[] { gx, gy, z };
		}

		/// <summary>KingdomFounding.ZonesAdjacent's law: CoordsAdjacent, vertical included.</summary>
		private static bool Adjacent(string A, string B)
		{
			int[] a = Global(A);
			int[] b = Global(B);
			return KingdomRules.CoordsAdjacent("JoppaWorld", a[0], a[1], a[2], "JoppaWorld", b[0],
				b[1], b[2], IncludeVertical: true);
		}

		[Test]
		public void JudgeSiteReadsTooCloseOnlyForAZoneBorderingARealmClaim()
		{
			string judge = TestMain.ReadRepositoryText("Core/KingdomFounding.03.SiteJudgmentAndStyle.cs");
			Assert.That(judge, Does.Contain("if (!adjacent && ZonesAdjacent(zoneID, Site.ZoneID))"));
			Assert.That(judge, Does.Contain("return KingdomSettlement.JudgeSecondFounding("
				+ "System.Founded, System.SettlementCount, claimed, adjacent);"));
			Assert.That(TestMain.ReadRepositoryText("Core/KingdomFounding.04.Claims.cs"), Does.Contain(
				"return KingdomRules.CoordsAdjacent(worldA, pxA * 3 + zxA, pyA * 3 + zyA, zA, worldB, "
				+ "pxB * 3 + zxB, pyB * 3 + zyB, zB, IncludeVertical: true);"));
			Assert.That(TestMain.ReadRepositoryText("Core/KingdomRules.Spatial.cs"),
				Does.Contain("GX = wx * 3 + zx;"));
			Assert.That(KingdomSettlement.JudgeSecondFounding(true, 1, false, true),
				Is.EqualTo(KingdomSettlement.SecondFoundingVerdict.GroundIsTooClose));
			Assert.That(KingdomSettlement.JudgeSecondFounding(true, 1, false, false),
				Is.EqualTo(KingdomSettlement.SecondFoundingVerdict.Allowed));
		}

		[Test]
		public void NoCandidateBordersCityOneAndRingOneIsThreeZonesAwayAndOfferedFirst()
		{
			IList<string> candidates = KingdomSecondCitySiteRules.Candidates(Home);
			int[] home = Global(Home);
			int nearest = int.MaxValue;
			foreach (string id in candidates)
			{
				Assert.That(Adjacent(Home, id), Is.False, id);
				int[] at = Global(id);
				int dx = at[0] > home[0] ? at[0] - home[0] : home[0] - at[0];
				int dy = at[1] > home[1] ? at[1] - home[1] : home[1] - at[1];
				if ((dx > dy ? dx : dy) < nearest) nearest = dx > dy ? dx : dy;
			}
			Assert.That(nearest, Is.EqualTo(3));
			Assert.That(new List<string>(candidates).GetRange(0, 8), Is.EqualTo(new[] {
				"JoppaWorld.7.21.1.1.10", "JoppaWorld.8.21.1.1.10", "JoppaWorld.9.21.1.1.10",
				"JoppaWorld.7.22.1.1.10", "JoppaWorld.9.22.1.1.10", "JoppaWorld.7.23.1.1.10",
				"JoppaWorld.8.23.1.1.10", "JoppaWorld.9.23.1.1.10" }));
		}

		[Test]
		public void TheZoneEastOfCityOneBordersItAndIsNeverOffered()
		{
			// The setup's tabled negative reads GroundIsTooClose on exactly this zone.
			Assert.That(Adjacent(Home, EastOfHome), Is.True);
			Assert.That(KingdomSecondCitySiteRules.Candidates(Home), Has.No.Member(EastOfHome));
			Assert.That(TestMain.ReadRepositoryText("Harness/KingdomSecondCityNativeChecks.cs"),
				Does.Contain("string border = Zone.GetZoneIDFromDirection(\"E\");"));
		}

		[Test]
		public void TheLiveSearchLeavesHeldAndTooCloseGroundToJudgeSite()
		{
			string site = TestMain.ReadRepositoryText("Harness/KingdomSecondCityNativeSite.cs");
			Assert.That(site, Does.Contain("KingdomFounding.JudgeSite(System, zone);"));
			Assert.That(site, Does.Contain("{ Reject(tried, id, verdict.ToString()); continue; }"));
			// No candidate is skipped unbuilt by a harness copy of the claim or adjacency law.
			Assert.That(site, Does.Not.Contain("ZonesAdjacent("));
			Assert.That(site, Does.Not.Contain("ClaimedZones.Contains("));
		}
	}
}
#endif
