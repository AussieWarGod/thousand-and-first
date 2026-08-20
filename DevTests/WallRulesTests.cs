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
	}
}
#endif
