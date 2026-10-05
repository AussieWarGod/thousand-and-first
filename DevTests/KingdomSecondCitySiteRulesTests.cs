#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Real execution against the engine-free second-city site arithmetic, not a source pin:
	/// every assertion below drives KingdomSecondCitySiteRules (and, through it, production's
	/// own KingdomPlotRules heart geometry) and reads the values it actually produces. These are
	/// the facts the native run cannot re-derive once it is under way -- an off-map candidate
	/// crashes zone build instead of refusing, and a rite poured where the heart rect slides can
	/// never bind the founding basin. Closeness is production's per-zone adjacency law, run in
	/// KingdomSecondCitySiteAdjacencyTests rather than assumed here as a parasang band.
	/// </summary>
	public class KingdomSecondCitySiteRulesTests
	{
		private const string Home = "JoppaWorld.8.22.1.1.10";
		private const int MapWidth = 80;
		private const int MapHeight = 25;

		[Test]
		public void ASurfaceZoneIdSplitsIntoItsWorldParasangAndSubCell()
		{
			string world;
			string subX;
			string subY;
			int wx;
			int wy;
			int depth;
			Assert.That(KingdomSecondCitySiteRules.TrySplit(Home, out world, out wx, out wy,
				out subX, out subY, out depth), Is.True);
			Assert.That(world, Is.EqualTo("JoppaWorld"));
			Assert.That(wx, Is.EqualTo(8));
			Assert.That(wy, Is.EqualTo(22));
			Assert.That(subX, Is.EqualTo("1"));
			Assert.That(subY, Is.EqualTo("1"));
			Assert.That(depth, Is.EqualTo(10));
		}

		[TestCase("JoppaWorld.8.22.1.1")]
		[TestCase("JoppaWorld.8.22.1.1.10.0")]
		[TestCase("JoppaWorld.x.22.1.1.10")]
		[TestCase("JoppaWorld.-8.22.1.1.10")]
		[TestCase(".8.22.1.1.10")]
		[TestCase("")]
		[TestCase(null)]
		public void AMalformedZoneIdRefusesInsteadOfGuessing(string ZoneId)
		{
			string world;
			string subX;
			string subY;
			int wx;
			int wy;
			int depth;
			Assert.That(KingdomSecondCitySiteRules.TrySplit(ZoneId, out world, out wx, out wy,
				out subX, out subY, out depth), Is.False);
		}

		[Test]
		public void ComposeRebuildsAnIdOnTheHomeSubCellAtTheSurfaceDepth()
		{
			Assert.That(KingdomSecondCitySiteRules.Compose("JoppaWorld", 6, 20, "1", "1"),
				Is.EqualTo("JoppaWorld.6.20.1.1.10"));
		}

		/// <summary>A candidate's world-parasang ring around Home, its sub-cell and its depth.</summary>
		private static int Ring(string Id, out string Sub, out int Depth)
		{
			string world;
			string subX;
			string subY;
			int wx;
			int wy;
			Assert.That(KingdomSecondCitySiteRules.TrySplit(Id, out world, out wx, out wy,
				out subX, out subY, out Depth), Is.True, Id);
			Sub = subX + "," + subY;
			int dx = wx > 8 ? wx - 8 : 8 - wx;
			int dy = wy > 22 ? wy - 22 : 22 - wy;
			return dx > dy ? dx : dy;
		}

		[Test]
		public void EveryCandidateSitsOnTheHomeSubCellInRingsOneToFour()
		{
			Assert.That(KingdomSecondCitySiteRules.MinRing, Is.EqualTo(1));
			IList<string> candidates = KingdomSecondCitySiteRules.Candidates(Home);
			Assert.That(candidates.Count, Is.EqualTo(8 + 16 + 17 + 21));
			foreach (string id in candidates)
			{
				string sub;
				int depth;
				Assert.That(Ring(id, out sub, out depth), Is.InRange(
					KingdomSecondCitySiteRules.MinRing, KingdomSecondCitySiteRules.MaxRing));
				Assert.That(depth, Is.EqualTo(KingdomSecondCitySiteRules.SurfaceDepth));
				Assert.That(sub, Is.EqualTo("1,1"));
			}
		}

		[Test]
		public void CandidatesAreDistinctAndOrderedNearestRingFirst()
		{
			IList<string> candidates = KingdomSecondCitySiteRules.Candidates(Home);
			var seen = new HashSet<string>();
			int previous = 0;
			foreach (string id in candidates)
			{
				Assert.That(seen.Add(id), Is.True, id + " was offered twice");
				string sub;
				int depth;
				int ring = Ring(id, out sub, out depth);
				Assert.That(ring, Is.GreaterThanOrEqualTo(previous));
				previous = ring;
			}
		}

		[Test]
		public void EveryCandidateStaysInsideTheWorldMap()
		{
			foreach (string home in new[] { "JoppaWorld.0.0.1.1.10", "JoppaWorld.79.24.1.1.10",
				"JoppaWorld.1.1.2.2.10" })
			{
				foreach (string id in KingdomSecondCitySiteRules.Candidates(home))
				{
					string world;
					string subX;
					string subY;
					int wx;
					int wy;
					int depth;
					KingdomSecondCitySiteRules.TrySplit(id, out world, out wx, out wy, out subX,
						out subY, out depth);
					Assert.That(wx, Is.InRange(0, KingdomSecondCitySiteRules.WorldMaxX));
					Assert.That(wy, Is.InRange(0, KingdomSecondCitySiteRules.WorldMaxY));
				}
			}
		}

		[Test]
		public void TheHomeZoneIsNeverOfferedAsItsOwnSecondSite()
		{
			Assert.That(KingdomSecondCitySiteRules.Candidates(Home), Has.No.Member(Home));
		}

		[Test]
		public void ANonSurfaceHomeOffersNothingRatherThanADeepCandidate()
		{
			Assert.That(KingdomSecondCitySiteRules.Candidates("JoppaWorld.8.22.1.1.11").Count,
				Is.EqualTo(0));
			Assert.That(KingdomSecondCitySiteRules.Candidates("not-a-zone").Count,
				Is.EqualTo(0));
		}

		[Test]
		public void TheFirstCitysRiteGetsTheCentredHeartRectProductionDrafts()
		{
			KingdomPlotRules.PlotRect rect;
			Assert.That(KingdomSecondCitySiteRules.TryRiteHeartRect(40, 12, MapWidth, MapHeight,
				out rect), Is.True);
			Assert.That(new[] { rect.X1, rect.Y1, rect.X2, rect.Y2 },
				Is.EqualTo(new[] { 38, 11, 43, 14 }));
			Assert.That(KingdomSecondCitySiteRules.IsCentred(rect, 40, 12), Is.True);
			// The same rect production's founding draft computes for that rite.
			KingdomPlotRules.PlotRect survey;
			KingdomPlotRules.PlotRect drafted;
			Assert.That(KingdomPlotRules.TrySurveyedHeart(40, 12, MapWidth, MapHeight, out survey),
				Is.True);
			Assert.That(KingdomPlotRules.TryHeartRect(survey, 40, 12,
				KingdomPlotRules.HeartSizeForRung(1), out drafted), Is.True);
			Assert.That(new[] { drafted.X1, drafted.Y1, drafted.X2, drafted.Y2 },
				Is.EqualTo(new[] { rect.X1, rect.Y1, rect.X2, rect.Y2 }));
		}

		[TestCase(4, 12, true, true)]
		[TestCase(3, 12, true, false)]
		[TestCase(2, 12, true, false)]
		[TestCase(1, 12, false, false)]
		[TestCase(74, 12, true, true)]
		[TestCase(77, 12, true, false)]
		[TestCase(78, 12, false, false)]
		[TestCase(40, 3, true, true)]
		[TestCase(40, 2, true, false)]
		[TestCase(40, 1, false, false)]
		[TestCase(40, 20, true, true)]
		[TestCase(40, 22, true, false)]
		[TestCase(40, 23, false, false)]
		[TestCase(1, 1, false, false)]
		public void ARiteTheSurveySlidTheHeartAwayFromIsNeverOffered(int X, int Y, bool Admitted,
			bool Centred)
		{
			KingdomPlotRules.PlotRect rect;
			Assert.That(KingdomSecondCitySiteRules.TryRiteHeartRect(X, Y, MapWidth, MapHeight,
				out rect), Is.EqualTo(Admitted));
			if (!Admitted) return;
			Assert.That(rect.Contains(X, Y), Is.True);
			Assert.That(KingdomSecondCitySiteRules.IsCentred(rect, X, Y), Is.EqualTo(Centred));
		}

		[Test]
		public void TheRiteOrderOpensOnTheFirstCitysRite()
		{
			IList<int> order = KingdomSecondCitySiteRules.RiteOrder(40, 12, MapWidth, MapHeight);
			Assert.That(order.Count, Is.GreaterThan(0));
			Assert.That(order[0], Is.EqualTo(12 * MapWidth + 40));
		}

		[Test]
		public void TheRiteOrderIsDistinctNearestFirstAndSkipsTheOuterTwoRowsAndColumns()
		{
			IList<int> order = KingdomSecondCitySiteRules.RiteOrder(40, 12, MapWidth, MapHeight);
			// Exactly the cells x 2..77, y 2..22 on an 80x25 map: 76 columns by 21 rows. The
			// old landing scan's first pick, row 1, is outside it.
			Assert.That(order.Count, Is.EqualTo(76 * 21));
			var seen = new HashSet<int>();
			int previous = 0;
			foreach (int packed in order)
			{
				Assert.That(seen.Add(packed), Is.True, packed + " was offered twice");
				int x = packed % MapWidth;
				int y = packed / MapWidth;
				Assert.That(x, Is.InRange(2, 77));
				Assert.That(y, Is.InRange(2, 22));
				int dx = x > 40 ? x - 40 : 40 - x;
				int dy = y > 12 ? y - 12 : 12 - y;
				int ring = dx > dy ? dx : dy;
				Assert.That(ring, Is.GreaterThanOrEqualTo(previous));
				previous = ring;
				KingdomPlotRules.PlotRect rect;
				Assert.That(KingdomSecondCitySiteRules.TryRiteHeartRect(x, y, MapWidth, MapHeight,
					out rect), Is.True);
			}
		}

		[Test]
		public void ARitePreferredOnTheMapEdgeStartsAtTheNearestAdmittedCell()
		{
			IList<int> order = KingdomSecondCitySiteRules.RiteOrder(1, 1, MapWidth, MapHeight);
			Assert.That(order.Count, Is.EqualTo(76 * 21));
			Assert.That(order[0], Is.EqualTo(2 * MapWidth + 2));
		}

		[Test]
		public void AMapTooSmallForTheHeartSurveyOffersNoRite()
		{
			Assert.That(KingdomSecondCitySiteRules.RiteOrder(5, 5, 10, 10).Count, Is.EqualTo(0));
			Assert.That(KingdomSecondCitySiteRules.RiteOrder(0, 0, 0, 0).Count, Is.EqualTo(0));
		}

		[Test]
		public void TheScriptDeclaresOneCheckCallPerCase()
		{
			Assert.That(KingdomSecondCityScript.CheckCalls, Is.EqualTo(4));
			Assert.That(KingdomSecondCityScript.Matches(
				new List<string>(KingdomSecondCityScript.Steps)), Is.True);
			Assert.That(KingdomSecondCityScript.Matches(new List<string> { "stagedigest" }),
				Is.False);
			Assert.That(KingdomSecondCityScript.Matches(null), Is.False);
			var wrong = new List<string>(KingdomSecondCityScript.Steps);
			wrong[1] = "status";
			Assert.That(KingdomSecondCityScript.Matches(wrong), Is.False);
			var unsettled = new List<string>(KingdomSecondCityScript.Steps);
			unsettled.Remove(KingdomSecondCityScript.SettleStep);
			Assert.That(KingdomSecondCityScript.Matches(unsettled), Is.False);
		}
	}
}
#endif
