#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class WallRulesTests
	{
		// Blueprint match, worked stone.
		[TestCase("TerrainRuins", "Ruins", 2)]
		[TestCase("TerrainBaroqueRuins", "BaroqueRuins", 2)]
		[TestCase("TerrainGritGate", "Ruins", 2)]
		[TestCase("TerrainRustWell", "Ruins", 2)]
		// Blueprint match, quarriable but not worked.
		[TestCase("TerrainMountains", "Mountains", 1)]
		[TestCase("TerrainBethesdaSusa", "Mountains", 1)]
		[TestCase("TerrainHills", "Hills", 1)]
		[TestCase("TerrainAsphaltMines", "Hills", 1)]
		// Ground the ladder offers nothing for: the floor, not a penalty.
		[TestCase("TerrainSaltdunes", "Saltdunes", 0)]
		[TestCase("TerrainSaltmarsh", "Saltmarsh", 0)]
		[TestCase("TerrainJungle", "Jungle", 0)]
		[TestCase("TerrainFungal", "Fungal", 0)]
		[TestCase("TerrainWatervine", "Saltmarsh", 0)]
		[TestCase(null, null, 0)]
		[TestCase("", "", 0)]
		public void GroundWallBonus(string blueprint, string region, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.GroundWallBonus(blueprint, region));
		}

		[Test]
		public void GroundWallBonusReadsBlueprintBeforeRegion()
		{
			// A ruin blueprint outranks a contradicting region reading, the same order
			// StyleForSite resolves ground in.
			Assert.AreEqual(2, KingdomRules.GroundWallBonus("TerrainRuins", "Saltmarsh"));
		}

		[Test]
		public void GroundWallBonusFallsBackToRegionWhenBlueprintIsUnrecognised()
		{
			// A renamed or third-party blueprint the ladder does not know falls back to the
			// region tag rather than answering zero outright.
			Assert.AreEqual(1, KingdomRules.GroundWallBonus("TerrainOfSomeFutureUpdate", "Hills"));
		}

		[TestCase(false, false, 0)]
		[TestCase(true, false, 1)]
		[TestCase(false, true, 1)]
		[TestCase(true, true, 2)]
		public void KnowledgeWallBonus(bool hasTinkering, bool hasAdvancedTinkering, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.KnowledgeWallBonus(hasTinkering, hasAdvancedTinkering));
		}

		[TestCase(0, "TerrainRuins", "Ruins", true, true, 0)]
		[TestCase(-3, "TerrainRuins", "Ruins", true, true, -3)]
		public void WallDefenceLeavesNonDefensiveDesignsUnchanged(int baseDefence, string blueprint, string region, bool hasTinkering, bool hasAdvancedTinkering, int expected)
		{
			// A design with no defence of its own never becomes a wall just because the ground
			// and the founder both qualify.
			Assert.AreEqual(expected, KingdomRules.WallDefence(baseDefence, blueprint, region, hasTinkering, hasAdvancedTinkering));
		}

		[Test]
		public void WallDefenceOnPoorGroundWithAnUnskilledFounderMatchesTodaysBuild()
		{
			// The ladder's own floor: exactly the design's base Defence, nothing added and
			// nothing taken away.
			Assert.AreEqual(6, KingdomRules.WallDefence(6, "TerrainSaltdunes", "Saltdunes", false, false));
		}

		[Test]
		public void WallDefenceAddsGroundAndKnowledgeTogether()
		{
			// Worked stone (+2) plus a Tinker I founder (+1 base, +1 advanced) on a defence-6
			// design: 6 + 2 + 1 + 1.
			Assert.AreEqual(10, KingdomRules.WallDefence(6, "TerrainRuins", "Ruins", true, true));
		}

		[Test]
		public void WallDefenceNeverFallsBelowBaseDefence()
		{
			int baseDefence = 3;
			Assert.GreaterOrEqual(KingdomRules.WallDefence(baseDefence, null, null, false, false), baseDefence);
			Assert.GreaterOrEqual(KingdomRules.WallDefence(baseDefence, "TerrainSaltdunes", "Saltdunes", true, true), baseDefence);
			Assert.GreaterOrEqual(KingdomRules.WallDefence(baseDefence, "TerrainRuins", "Ruins", true, true), baseDefence);
		}

		// --- The frontier: where a wall belongs when a camp becomes a city ----------------

		private const string Home = "JoppaWorld.5.5.1.1.10";
		private const string North = "JoppaWorld.5.5.1.0.10";
		private const string South = "JoppaWorld.5.5.1.2.10";
		private const string West = "JoppaWorld.5.5.0.1.10";
		private const string East = "JoppaWorld.5.5.2.1.10";

		[Test]
		public void FrontierEdges_ALoneZoneIsFrontierOnEverySide()
		{
			Assert.AreEqual(
				KingdomRules.Frontier.North | KingdomRules.Frontier.South | KingdomRules.Frontier.West | KingdomRules.Frontier.East,
				KingdomRules.FrontierEdges(Home, new string[1] { Home }));
		}

		[Test]
		public void FrontierEdges_ClaimingTheNeighbourStopsThatEdgeBeingFrontier()
		{
			KingdomRules.Frontier edges = KingdomRules.FrontierEdges(Home, new string[2] { Home, North });
			Assert.AreEqual(KingdomRules.Frontier.None, edges & KingdomRules.Frontier.North,
				"the north edge is still frontier after claiming the ground north of it");
			Assert.AreNotEqual(KingdomRules.Frontier.None, edges & KingdomRules.Frontier.South);
		}

		[Test]
		public void FrontierEdges_SurroundedGroundHasNoFrontierAtAll()
		{
			Assert.AreEqual(KingdomRules.Frontier.None,
				KingdomRules.FrontierEdges(Home, new string[5] { Home, North, South, West, East }));
		}

		[Test]
		public void FrontierEdges_AnotherWorldOrAnotherDepthIsNotANeighbour()
		{
			Assert.AreNotEqual(KingdomRules.Frontier.None,
				KingdomRules.FrontierEdges(Home, new string[2] { Home, "OtherWorld.5.5.1.0.10" }) & KingdomRules.Frontier.North,
				"a zone in another world counted as bordering ground");
			Assert.AreNotEqual(KingdomRules.Frontier.None,
				KingdomRules.FrontierEdges(Home, new string[2] { Home, "JoppaWorld.5.5.1.0.11" }) & KingdomRules.Frontier.North,
				"a zone one stratum down counted as bordering ground on the surface");
		}

		[Test]
		public void FrontierEdges_RubbishInputHasNoFrontier()
		{
			Assert.AreEqual(KingdomRules.Frontier.None, KingdomRules.FrontierEdges(null, new string[0]));
			Assert.AreEqual(KingdomRules.Frontier.None, KingdomRules.FrontierEdges("not.a.zone", new string[0]));
			Assert.AreEqual(KingdomRules.Frontier.None, KingdomRules.FrontierEdges(Home, null));
		}

		[Test]
		public void IsOnFrontier_OnlyTheEdgesThatFaceOutwardAreWallGround()
		{
			// An 80x25 zone whose only unclaimed neighbour lies north.
			KingdomRules.Frontier north = KingdomRules.Frontier.North;
			Assert.IsTrue(KingdomRules.IsOnFrontier(40, 0, 80, 25, north), "the north edge is not wall ground");
			Assert.IsTrue(KingdomRules.IsOnFrontier(40, 1, 80, 25, north), "the band is thinner than it claims");
			Assert.IsFalse(KingdomRules.IsOnFrontier(40, 12, 80, 25, north), "the middle of the zone is wall ground");
			Assert.IsFalse(KingdomRules.IsOnFrontier(40, 24, 80, 25, north), "the south edge is wall ground for a north frontier");
		}

		[Test]
		public void IsOnFrontier_NoFrontierMeansNoWallGroundAnywhere()
		{
			for (int x = 0; x < 80; x += 13)
			{
				for (int y = 0; y < 25; y += 6)
				{
					Assert.IsFalse(KingdomRules.IsOnFrontier(x, y, 80, 25, KingdomRules.Frontier.None));
				}
			}
		}

		[Test]
		public void IsOnFrontier_ACornerBelongsToBothItsEdges()
		{
			Assert.IsTrue(KingdomRules.IsOnFrontier(0, 0, 80, 25, KingdomRules.Frontier.North));
			Assert.IsTrue(KingdomRules.IsOnFrontier(0, 0, 80, 25, KingdomRules.Frontier.West));
		}

	}
}
#endif
