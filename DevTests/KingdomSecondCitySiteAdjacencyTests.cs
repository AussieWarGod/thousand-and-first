#if TAF_TESTS
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Behavioural coverage row 12: which candidate sites production calls too close, and which
	/// candidates the live search must build. KingdomFounding.JudgeSite reads GroundIsTooClose for
	/// a zone bordering any realm claim, by KingdomFounding.ZonesAdjacent: KingdomRules.CoordsAdjacent
	/// at the global zone coordinates wx * 3 + zx, wy * 3 + zy, vertical neighbours included. That
	/// call parses ids with the engine's ZoneID.Parse, so these cases run production's engine-free
	/// twin, KingdomRules.ZonesAdjacent with IncludeVertical, whose KingdomRules.TryParseZoneID
	/// computes the same coordinates, and pin the production calls that make them equal. The
	/// search's probes are held to KingdomSecondCitySiteRules.ProbeOrderFault, run here by value,
	/// and every text naming where city two sits is held to the zone it is.
	/// Not native acceptance: the live search still asks JudgeSite itself for every candidate.
	/// </summary>
	public class KingdomSecondCitySiteAdjacencyTests
	{
		private const string Home = "JoppaWorld.8.22.1.1.10";
		private const string EastOfHome = "JoppaWorld.8.22.2.1.10";
		private const string SiteSource = "Harness/KingdomSecondCityNativeSite.cs";
		private const string ChecksSource = "Harness/KingdomSecondCityNativeChecks.cs";
		private const string ProviderSource = "Harness/KingdomSecondCityNativeProvider.cs";
		private const string CasesSource = "Harness/KingdomSecondCityNativeCases.cs";
		private const string PersonaSource = "Tools/personas/second-city-native-check.persona";

		/// <summary>The site's place relative to city one, as every summary must state it.</summary>
		private const string SitePlace =
			"on a non-adjacent surface zone, one world parasang out and three zones from city one,";

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

		/// <summary>KingdomFounding.ZonesAdjacent's law, through production's engine-free twin.</summary>
		private static bool Adjacent(string A, string B)
		{
			return KingdomRules.ZonesAdjacent(A, B, IncludeVertical: true);
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
			string spatial = TestMain.ReadRepositoryText("Core/KingdomRules.Spatial.cs");
			Assert.That(spatial, Does.Contain("GX = wx * 3 + zx;"));
			Assert.That(spatial, Does.Contain(
				"return CoordsAdjacent(worldA, gxA, gyA, zA, worldB, gxB, gyB, zB, IncludeVertical);"));
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
		public void EveryLawfulSiteIsANonAdjacentZoneOneParasangOutAndNoTextCallsItAParasangApart()
		{
			// Every probe the search may build lies in a world parasang bordering city one's
			// (production's gx = wx * 3 + zx, so gx / 3 is the parasang) and three zones from
			// city one. Only the ZONE is non-adjacent: a non-adjacent or distant PARASANG would
			// be a separation no run has.
			IList<string> candidates = KingdomSecondCitySiteRules.Candidates(Home);
			Assert.That(candidates.Count, Is.GreaterThanOrEqualTo(KingdomSecondCitySiteRules.MaxProbes));
			int[] home = Global(Home);
			for (int i = 0; i < KingdomSecondCitySiteRules.MaxProbes; i++)
			{
				int[] at = Global(candidates[i]);
				Assert.That(Chebyshev(at[0] / 3 - home[0] / 3, at[1] / 3 - home[1] / 3), Is.EqualTo(1),
					candidates[i]);
				Assert.That(Chebyshev(at[0] - home[0], at[1] - home[1]), Is.EqualTo(3), candidates[i]);
			}
			Regex apart = new Regex(@"\b(non-?adjacent|distant)\s+((surface|world)\s+)?parasangs?\b",
				RegexOptions.IgnoreCase);
			foreach (string path in new[] { PersonaSource, ProviderSource, ChecksSource, CasesSource,
				SiteSource })
				Assert.That(apart.Match(Prose(path)).Value, Is.Empty, path);
			// The run's headline, the persona header and both harness summaries say where it is.
			Assert.That(Prose(PersonaSource), Does.Contain("DESCRIPTION=a realm founds a second city "
				+ "on a non-adjacent surface zone three zones from the first city, refuses"));
			foreach (string path in new[] { PersonaSource, ProviderSource, ChecksSource })
				Assert.That(Prose(path).ToLowerInvariant(), Does.Contain(SitePlace), path);
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
			// Spelling tripwires only. Whether a candidate was skipped is behaviour, held at run
			// time by ProbeOrderFault (the probe-order cases below).
			Assert.That(site, Does.Not.Contain("ZonesAdjacent("));
			Assert.That(site, Does.Not.Contain("ClaimedZones.Contains("));
		}

		[Test]
		public void OnlyTheNearestFirstPrefixIsALawfulProbeList()
		{
			List<string> all = new List<string>(KingdomSecondCitySiteRules.Candidates(Home));
			int limit = KingdomSecondCitySiteRules.MaxProbes;
			for (int n = 1; n <= limit; n++)
				Assert.That(Fault(all.GetRange(0, n), n - 1, true), Is.Null, "qualified on probe " + n);
			Assert.That(Fault(all.GetRange(0, limit), limit, false), Is.Null, "refused at the limit");
			// A home with no surface candidates permits no probe at all.
			Assert.That(KingdomSecondCitySiteRules.ProbeOrderFault("JoppaWorld.8.22.1.1.11",
				new List<string>(), 0, false), Is.Null);
		}

		[Test]
		public void ASkippedReorderedFilteredOrPaddedProbeListRefuses()
		{
			List<string> all = new List<string>(KingdomSecondCitySiteRules.Candidates(Home));
			// Ring 1 dropped before the loop: probing would start at ring 2's first candidate.
			Assert.That(Fault(all.GetRange(8, 1), 0, true), Is.EqualTo("the site search probed "
				+ "JoppaWorld.6.20.1.1.10 at position 1 where the nearest-first order offers "
				+ "JoppaWorld.7.21.1.1.10"));
			Assert.That(Fault(new List<string> { all[1], all[0] }, 1, true),
				Does.Contain("probed " + all[1] + " at position 1 "));
			Assert.That(Fault(new List<string> { all[0], all[2] }, 1, true), Does.EndWith(
				"probed " + all[2] + " at position 2 where the nearest-first order offers " + all[1]));
			Assert.That(Fault(new List<string> { all[0], all[0] }, 1, true),
				Does.Contain("probed " + all[0] + " at position 2 "));
			Assert.That(Fault(all.GetRange(0, 9), 8, true), Is.EqualTo(
				"the site search qualified a site after 9 probes where 1..8 are permitted"));
			Assert.That(Fault(all.GetRange(0, 7), 7, false), Is.EqualTo(
				"the site search refused after 7 probes where exactly 8 are permitted"));
			Assert.That(Fault(new List<string>(), 0, true), Is.EqualTo(
				"the site search qualified a site after 0 probes where 1..8 are permitted"));
			Assert.That(Fault(null, 0, false), Is.EqualTo("the site search reported no probe list"));
		}

		[Test]
		public void AProbeNeitherRejectedNorChosenRefuses()
		{
			List<string> all = new List<string>(KingdomSecondCitySiteRules.Candidates(Home));
			Assert.That(Fault(all.GetRange(0, 3), 1, true), Is.EqualTo(
				"the site search rejected 1 of 3 probes and chose one"));
			Assert.That(Fault(all.GetRange(0, 3), 3, true), Is.EqualTo(
				"the site search rejected 3 of 3 probes and chose one"));
			Assert.That(Fault(all.GetRange(0, 8), 7, false), Is.EqualTo(
				"the site search rejected 7 of 8 probes"));
		}

		[Test]
		public void EveryProbeFaultSurvivesTheVerbRowFailureBound()
		{
			// Two-digit parasang coordinates, so every id is at its widest.
			string home = "JoppaWorld.40.14.1.1.10";
			List<string> all = new List<string>(KingdomSecondCitySiteRules.Candidates(home));
			string[] faults = {
				KingdomSecondCitySiteRules.ProbeOrderFault(home, all.GetRange(8, 1), 0, true),
				KingdomSecondCitySiteRules.ProbeOrderFault(home, all.GetRange(0, 9), 8, true),
				KingdomSecondCitySiteRules.ProbeOrderFault(home, all.GetRange(0, 7), 7, false),
				KingdomSecondCitySiteRules.ProbeOrderFault(home, all.GetRange(0, 8), 7, false),
				KingdomSecondCitySiteRules.ProbeOrderFault(home, null, 0, false) };
			foreach (string fault in faults)
			{
				Assert.That(fault, Is.Not.Null);
				string failure = "InvalidOperationException: " + fault;
				Assert.That(KingdomScenarioRules.Bounded(failure), Is.EqualTo(failure));
			}
		}

		[Test]
		public void TheLiveSearchRecordsEveryProbeAndTheFrameRefusesAnyOtherOrder()
		{
			string site = TestMain.ReadRepositoryText(SiteSource);
			// The candidates come straight from the pure rules and are never reassigned or edited.
			Assert.That(site, Does.Contain(
				"IList<string> candidates = KingdomSecondCitySiteRules.Candidates(home);"));
			Assert.That(Regex.Matches(site, @"\bcandidates\s*=").Count, Is.EqualTo(1));
			Assert.That(Regex.IsMatch(site, @"\bcandidates\s*\.\s*(Add|AddRange|Insert|Remove|"
				+ @"RemoveAt|RemoveAll|RemoveRange|Clear|Sort|Reverse)\b"), Is.False);
			Assert.That(Regex.IsMatch(site, @"\bcandidates\s*\[[^\]]*\]\s*=[^=]"), Is.False);
			// Every candidate taken is recorded as a probe, in order, before anything can reject it.
			Assert.That(Squash(site), Does.Contain(Squash(
				"string id = candidates[i]; probes++; probed.Add(id); Zone zone;")));
			Assert.That(Regex.Matches(site, @"\bprobed\.Add\(").Count, Is.EqualTo(1));
			Assert.That(site, Does.Contain("Probed = probed;"));
			string checks = TestMain.ReadRepositoryText(ChecksSource);
			Assert.That(Squash(checks), Does.Contain(Squash("string order = KingdomSecondCitySiteRules"
				+ ".ProbeOrderFault(HomeZoneId, probed, rejections.Count, found);")));
		}

		private static string Fault(IList<string> Probed, int Rejected, bool Found)
		{
			return KingdomSecondCitySiteRules.ProbeOrderFault(Home, Probed, Rejected, Found);
		}

		private static string Squash(string Source)
		{
			System.Text.StringBuilder kept = new System.Text.StringBuilder(Source.Length);
			foreach (char c in Source)
				if (c != ' ' && c != '\t' && c != '\r' && c != '\n') kept.Append(c);
			return kept.ToString();
		}

		private static int Chebyshev(int Dx, int Dy)
		{
			return System.Math.Max(System.Math.Abs(Dx), System.Math.Abs(Dy));
		}

		/// <summary>
		/// A file's text with each line's leading comment marker dropped and every run of
		/// whitespace made one space, so a phrase a comment wraps still reads as one phrase.
		/// </summary>
		private static string Prose(string Path)
		{
			string text = Regex.Replace(TestMain.ReadRepositoryText(Path), @"(?m)^[ \t]*(///|//|#)", "");
			return Regex.Replace(text, @"\s+", " ");
		}
	}
}
#endif
