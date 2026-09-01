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

		[TestCase(0, false, false)]
		[TestCase(3, true, false)]
		[TestCase(3, false, true)]
		public void FrontierWorkRequiresDefenceAndNoReservedPlot(int defence, bool hasPlot,
			bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.IsFrontierWork(defence, hasPlot));
		}

		[Test]
		public void DefensivePlotKeepsBaseRatingWhileFrontierWorkEarnsWallBonuses()
		{
			Assert.AreEqual(6, KingdomRules.BuiltDefence(6, true,
				"TerrainRuins", "Ruins", true, true),
				"a plotted arsenal is a building, not four abstract wall bonuses");
			Assert.AreEqual(10, KingdomRules.BuiltDefence(6, false,
				"TerrainRuins", "Ruins", true, true),
				"the same unplotted design is a perimeter work and earns wall bonuses");
		}

		[Test]
		public void AdoptedDefensivePlotKeepsDurablePlotIdentityAndReleaseCannotLeaveGhostGround()
		{
			string plot = KingdomPlot2LogicalSource.Read();
			string adopt = string.Join("\n", new string[]
			{
				TestMain.ReadRepositoryText("Growth/KingdomAdopt.cs"),
				TestMain.ReadRepositoryText("Growth/KingdomAdopt.Work.cs"),
				TestMain.ReadRepositoryText("Growth/KingdomAdopt.Release.cs"),
				TestMain.ReadRepositoryText("Growth/KingdomAdopt.Helpers.cs")
			});
			StringAssert.DoesNotContain("public static class KingdomAdopt", adopt);
			StringAssert.Contains(
				"public const string AdoptedPlotProperty = \"KingdomAdoptedPlot\";", plot);
			StringAssert.Contains("string plotId = \"adopted:\" + Adopted.ID;", plot);
			StringAssert.Contains("Adopted.SetStringProperty(PlotIdProperty, plotId);", plot);
			StringAssert.Contains("Adopted.SetIntProperty(AdoptedPlotProperty, 1);", plot);

			int classify = plot.IndexOf("public static bool IsFrontierWork(GameObject Object)");
			int legacyFallback = plot.IndexOf(
				"return Object.GetIntProperty(\"KingdomDefence\") > 0", classify);
			int adoptedReceipt = plot.IndexOf(
				"Object.GetIntProperty(AdoptedPlotProperty) == 1", classify);
			Assert.Greater(adoptedReceipt, classify);
			Assert.Greater(legacyFallback, adoptedReceipt,
				"durable adopted-plot truth must win before legacy Defence fallback");

			int release = plot.IndexOf("public static void ReleaseAdoptedPlot(");
			int removePresence = plot.IndexOf("Adopted.RemoveIntProperty(PlotX2Property);", release);
			int removeIdentity = plot.IndexOf("Adopted.RemoveStringProperty(PlotIdProperty);", release);
			int removeOwner = plot.IndexOf("Adopted.RemoveIntProperty(AdoptedPlotProperty);", release);
			Assert.Greater(removePresence, release);
			Assert.Greater(removeIdentity, removePresence);
			Assert.Greater(removeOwner, removeIdentity,
				"rect presence must disappear before adoption ownership commits its release");

			int releaseAdoption = adopt.IndexOf("public static bool Release(");
			int clearPlot = adopt.IndexOf("KingdomPlots.ReleaseAdoptedPlot(Adopted);",
				releaseAdoption);
			int clearAdoption = adopt.IndexOf("ClearTyped(Adopted, AdoptedProperty);",
				releaseAdoption);
			Assert.Greater(clearPlot, releaseAdoption);
			Assert.Greater(clearAdoption, clearPlot,
				"plot receipt must retire while interrupted release is still retryable");
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

		// --- what a founder's claim does to the wall line ---------------------------------

		private const string Below = "JoppaWorld.5.5.1.1.11";
		private const string Corner = "JoppaWorld.5.5.2.0.10";

		[Test]
		public void AClaimOnTheNeighbourFreesExactlyOneEdgeOfTheGroundAlreadyHeld()
		{
			// The end-to-end shape of the claim action's wall sentence: the same held ground,
			// asked against the old claim and the new one.
			string[] before = new string[1] { Home };
			string[] after = new string[2] { Home, North };
			int wasFacing = KingdomZoningRules.EdgeCount(KingdomRules.FrontierEdges(Home, before));
			int nowFacing = KingdomZoningRules.EdgeCount(KingdomRules.FrontierEdges(Home, after));
			Assert.AreEqual(4, wasFacing);
			Assert.AreEqual(3, nowFacing);
			Assert.IsTrue(KingdomZoningRules.ClaimedWallClause(wasFacing, nowFacing, "Kavvat").Contains("moves outward"));
		}

		[Test]
		public void AVerticalClaimIsLegalGroundThatMovesNoWall()
		{
			// A cellar is a real claim - ClaimZone's adjacency includes the stratum directly
			// below - and FrontierEdges clears an edge only for an orthogonal neighbour in the
			// same stratum, so the wall line honestly does not move. The founder is told that
			// rather than told the wall moved.
			int wasFacing = KingdomZoningRules.EdgeCount(KingdomRules.FrontierEdges(Home, new string[1] { Home }));
			int nowFacing = KingdomZoningRules.EdgeCount(KingdomRules.FrontierEdges(Home, new string[2] { Home, Below }));
			Assert.AreEqual(wasFacing, nowFacing);
			Assert.IsTrue(KingdomZoningRules.ClaimedWallClause(wasFacing, nowFacing, "Kavvat").Contains("does not move"));
		}

		[Test]
		public void ADiagonalClaimIsLegalGroundThatMovesNoWallEither()
		{
			int wasFacing = KingdomZoningRules.EdgeCount(KingdomRules.FrontierEdges(Home, new string[1] { Home }));
			int nowFacing = KingdomZoningRules.EdgeCount(KingdomRules.FrontierEdges(Home, new string[2] { Home, Corner }));
			Assert.AreEqual(wasFacing, nowFacing);
			Assert.IsTrue(KingdomZoningRules.ClaimedWallClause(wasFacing, nowFacing, "Kavvat").Contains("does not move"));
		}

		[Test]
		public void AClaimNeverPutsAnEdgeBackOntoTheWallLine()
		{
			// Growing outward can only ever free wall ground, never create it: the claim widens
			// the set FrontierEdges tests against, so no edge that was interior becomes frontier.
			string[] before = new string[2] { Home, North };
			string[] after = new string[3] { Home, North, West };
			Assert.IsTrue(KingdomZoningRules.EdgeCount(KingdomRules.FrontierEdges(Home, after))
				<= KingdomZoningRules.EdgeCount(KingdomRules.FrontierEdges(Home, before)));
		}

	}
}
#endif
