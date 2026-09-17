#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Real execution against the engine-free second-city site arithmetic, not a source pin:
	/// every assertion below drives KingdomSecondCitySiteRules and reads the values it actually
	/// produces. These are the facts the native run cannot re-derive once it is under way -- a
	/// candidate inside the bordering band would be refused as GroundIsTooClose, and an off-map
	/// candidate crashes zone build instead of refusing.
	/// </summary>
	public class KingdomSecondCitySiteRulesTests
	{
		private const string Home = "JoppaWorld.8.22.1.1.10";

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

		[Test]
		public void NoCandidateLiesInsideTheBorderingBand()
		{
			IList<string> candidates = KingdomSecondCitySiteRules.Candidates(Home);
			Assert.That(candidates.Count, Is.GreaterThan(0));
			foreach (string id in candidates)
			{
				string world;
				string subX;
				string subY;
				int wx;
				int wy;
				int depth;
				Assert.That(KingdomSecondCitySiteRules.TrySplit(id, out world, out wx, out wy,
					out subX, out subY, out depth), Is.True);
				int dx = wx > 8 ? wx - 8 : 8 - wx;
				int dy = wy > 22 ? wy - 22 : 22 - wy;
				int ring = dx > dy ? dx : dy;
				Assert.That(ring, Is.GreaterThanOrEqualTo(KingdomSecondCitySiteRules.MinRing));
				Assert.That(ring, Is.LessThanOrEqualTo(KingdomSecondCitySiteRules.MaxRing));
				Assert.That(depth, Is.EqualTo(KingdomSecondCitySiteRules.SurfaceDepth));
				Assert.That(subX, Is.EqualTo("1"));
				Assert.That(subY, Is.EqualTo("1"));
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
				string world;
				string subX;
				string subY;
				int wx;
				int wy;
				int depth;
				KingdomSecondCitySiteRules.TrySplit(id, out world, out wx, out wy, out subX,
					out subY, out depth);
				int dx = wx > 8 ? wx - 8 : 8 - wx;
				int dy = wy > 22 ? wy - 22 : 22 - wy;
				int ring = dx > dy ? dx : dy;
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
		}
	}
}
#endif
