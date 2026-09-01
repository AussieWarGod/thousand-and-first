#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomRoadPaletteRulesTests
	{
		[Test]
		public void TerrainAndTechChangeSurfaceWithoutPrematureMetalOrConcrete()
		{
			AssertSurface("fungal", KingdomRoadPaletteRules.LocalRole, TechLevel.Hands,
				"WoodFloor", KingdomMaterial.Timber);
			AssertSurface("fungal", KingdomRoadPaletteRules.LocalRole, TechLevel.Workshop,
				"FoamcreteFloor", KingdomMaterial.ShapedStone);
			AssertSurface("eater", KingdomRoadPaletteRules.LocalRole, TechLevel.Hands,
				"SaltPath", KingdomMaterial.Stone);
			AssertSurface("eater", KingdomRoadPaletteRules.LocalRole, TechLevel.Salvage,
				"GreenTile", KingdomMaterial.Scrap);
			AssertSurface("eater", KingdomRoadPaletteRules.LocalRole, TechLevel.Foundry,
				"SmallHexFloor", KingdomMaterial.WorkedMetal);
			AssertSurface("ruins", KingdomRoadPaletteRules.LocalRole, TechLevel.Hands,
				"SaltPath", KingdomMaterial.Stone);
			AssertSurface("ruins", KingdomRoadPaletteRules.LocalRole, TechLevel.Salvage,
				"FoamcreteFloor", KingdomMaterial.Stone);
			AssertSurface("moonstair", KingdomRoadPaletteRules.LocalRole, TechLevel.Hands,
				"BlackMarbleWalkway", KingdomMaterial.Marble);
		}

		[Test]
		public void RouteRoleIsAnIndependentPaletteAxis()
		{
			AssertSurface("verdant", KingdomRoadPaletteRules.LocalRole, TechLevel.Workshop,
				"WoodFloor", KingdomMaterial.ShapedTimber);
			AssertSurface("verdant", KingdomRoadPaletteRules.MarketRole, TechLevel.Workshop,
				"MarbleFloor", KingdomMaterial.Marble);
			AssertSurface("verdant", KingdomRoadPaletteRules.CaravanRole, TechLevel.Hands,
				"SaltPath", KingdomMaterial.Stone);
			AssertSurface("common", KingdomRoadPaletteRules.MonumentalRole, TechLevel.Foundry,
				"MarbleFloor", KingdomMaterial.Marble);
			AssertSurface("eater", KingdomRoadPaletteRules.MonumentalRole, TechLevel.Foundry,
				"SmallHexFloor", KingdomMaterial.WorkedMetal);
			AssertSurface("verdant", "taf:othermod:procession", TechLevel.Hands,
				"WoodFloor", KingdomMaterial.Timber);
		}

		[Test]
		public void ResolverTieLawIsKeyStableAndConflictingKeysFailClosed()
		{
			List<KingdomRoadSurfaceRule> rules = new List<KingdomRoadSurfaceRule>
			{
				new KingdomRoadSurfaceRule("z-last", "common", "local",
					TechLevel.Hands, TechLevel.Arclight, "WoodFloor", KingdomMaterial.Timber, 10),
				new KingdomRoadSurfaceRule("a-first", "common", "local",
					TechLevel.Hands, TechLevel.Arclight, "SaltPath", KingdomMaterial.Stone, 10)
			};
			Assert.IsTrue(KingdomRoadPaletteRules.TryResolve(rules, "COMMON", "LOCAL",
				TechLevel.Hands, out var surface));
			Assert.AreEqual("a-first", surface.RuleKey);

			rules.Add(new KingdomRoadSurfaceRule("a-first", "common", "local",
				TechLevel.Hands, TechLevel.Arclight, "WoodFloor", KingdomMaterial.Timber, 10));
			Assert.IsFalse(KingdomRoadPaletteRules.TryResolve(rules, "common", "local",
				TechLevel.Hands, out _));
		}

		[Test]
		public void BehaviorLaneRegistrationIsBoundedIdempotentAndCollisionSafe()
		{
			KingdomRoadSurfaceRule rule = new KingdomRoadSurfaceRule(
				"taf-test-glass-road", "taf:test-glass", "taf:test:pilgrim",
				TechLevel.Hands, TechLevel.Arclight, "MarbleFloor", KingdomMaterial.Marble, 90);
			Assert.IsTrue(KingdomRoadPaletteRules.RegisterSurfaceRule(rule, out var failure), failure);
			Assert.IsTrue(KingdomRoadPaletteRules.RegisterSurfaceRule(rule, out failure), failure);
			Assert.IsTrue(KingdomRoadPaletteRules.TryResolveCurrent("taf:test-glass",
				"taf:test:pilgrim", TechLevel.Hands, out var surface));
			Assert.AreEqual("taf-test-glass-road", surface.RuleKey);
			Assert.IsFalse(KingdomRoadPaletteRules.RegisterSurfaceRule(
				new KingdomRoadSurfaceRule("taf-test-glass-road", "taf:test-glass",
					"taf:test:pilgrim", TechLevel.Hands, TechLevel.Arclight,
					"SaltPath", KingdomMaterial.Stone, 90), out failure));
			Assert.IsNotEmpty(failure);
		}

		[Test]
		public void FrontagesReadRouteBuildingAndCoLocatedFunctionalEvidence()
		{
			ArchitectureAnchor entrance = new ArchitectureAnchor
			{
				Key = "entrance:public", X = 2, Y = 3
			};
			ArchitectureLayoutSnapshot snapshot = new ArchitectureLayoutSnapshot
			{
				BuildKey = "ordinary-house"
			};
			snapshot.Anchors.Add(entrance);
			AssertFrontage(KingdomRoadClearanceRules.ForArchitecture(
				snapshot.BuildKey, snapshot, entrance), KingdomRoadPaletteRules.LocalRole, 1);

			snapshot.Anchors.Add(new ArchitectureAnchor { Key = "market:stall", X = 2, Y = 3 });
			AssertFrontage(KingdomRoadClearanceRules.ForArchitecture(
				snapshot.BuildKey, snapshot, entrance), KingdomRoadPaletteRules.MarketRole, 2);

			snapshot.BuildKey = "caravanserai";
			AssertFrontage(KingdomRoadClearanceRules.ForArchitecture(
				snapshot.BuildKey, snapshot, entrance), KingdomRoadPaletteRules.CaravanRole, 2);

			ArchitectureAnchor service = new ArchitectureAnchor
			{
				Key = "entrance:service", X = 4, Y = 3
			};
			snapshot = new ArchitectureLayoutSnapshot { BuildKey = "smithy" };
			snapshot.Anchors.Add(service);
			AssertFrontage(KingdomRoadClearanceRules.ForArchitecture(
				snapshot.BuildKey, snapshot, service), KingdomRoadPaletteRules.ServiceRole, 2);
			AssertFrontage(KingdomRoadClearanceRules.ForRoute(
				KingdomRoadRules.RouteKind.HeartToGate), KingdomRoadPaletteRules.GateRole, 2);
			AssertFrontage(KingdomRoadClearanceRules.ForRoute(
				KingdomRoadRules.RouteKind.HomeToWork), KingdomRoadPaletteRules.LocalRole, 1);
		}

		[Test]
		public void OptionalClearanceUsesCanonicalSideThenFallsBackWhole()
		{
			List<int> centre = new List<int> { P(2, 2), P(3, 2), P(4, 2) };
			List<int> cells = new List<int>();
			KingdomRoadFrontage preferred = new KingdomRoadFrontage("market", 2, 1);

			Assert.IsTrue(KingdomRoadClearanceRules.TryExpand((x, y) => true, 8, 7,
				1, 2, 5, 2, centre, preferred, cells, out int width));
			Assert.AreEqual(2, width);
			CollectionAssert.AreEqual(new[] { P(2, 2), P(3, 2), P(4, 2),
				P(2, 1), P(3, 1), P(4, 1) }, cells);

			Assert.IsTrue(KingdomRoadClearanceRules.TryExpand((x, y) => y != 1, 8, 7,
				1, 2, 5, 2, centre, preferred, cells, out width));
			Assert.AreEqual(2, width);
			CollectionAssert.AreEqual(new[] { P(2, 2), P(3, 2), P(4, 2),
				P(2, 3), P(3, 3), P(4, 3) }, cells);

			Assert.IsTrue(KingdomRoadClearanceRules.TryExpand((x, y) => y == 2, 8, 7,
				1, 2, 5, 2, centre, preferred, cells, out width));
			Assert.AreEqual(1, width);
			CollectionAssert.AreEqual(centre, cells);
		}

		[Test]
		public void RequiredClearanceAndMalformedCentrelinesRefuseWhole()
		{
			List<int> cells = new List<int> { 99 };
			List<int> centre = new List<int> { P(2, 2), P(3, 2) };
			Assert.IsFalse(KingdomRoadClearanceRules.TryExpand((x, y) => y == 2, 8, 7,
				1, 2, 4, 2, centre, new KingdomRoadFrontage("taf:wide", 2, 2),
				cells, out int width));
			Assert.AreEqual(0, width);
			Assert.AreEqual(0, cells.Count);

			centre.Add(P(2, 2));
			Assert.IsFalse(KingdomRoadClearanceRules.TryExpand((x, y) => true, 8, 7,
				1, 2, 4, 2, centre, new KingdomRoadFrontage("local", 1, 1),
				cells, out _));
			Assert.AreEqual(0, cells.Count);
		}

		[Test]
		public void TerrainDerivationKeepsDeepAndRuinsIndependentFromCityStyle()
		{
			Assert.AreEqual("deep", KingdomRoadPaletteRules.TerrainKey(
				"verdant", "some ruins", true));
			Assert.AreEqual("ruins", KingdomRoadPaletteRules.TerrainKey(
				"verdant", "salt dunes ruins", false));
			Assert.AreEqual("verdant", KingdomRoadPaletteRules.TerrainKey(
				" VERDANT ", "salt marsh", false));
			Assert.AreEqual("moonstair", KingdomRoadPaletteRules.TerrainKey(
				"gyre", "Moon Stair", false));
			Assert.AreEqual("common", KingdomRoadPaletteRules.TerrainKey(null, null, false));
		}

		private static void AssertSurface(string terrain, string role, TechLevel tech,
			string blueprint, KingdomMaterial material)
		{
			Assert.IsTrue(KingdomRoadPaletteRules.TryResolve(
				KingdomRoadPaletteRules.DefaultRules(), terrain, role, tech, out var surface));
			Assert.AreEqual(blueprint, surface.Blueprint);
			Assert.AreEqual(material, surface.Material);
		}

		private static void AssertFrontage(KingdomRoadFrontage frontage,
			string role, int width)
		{
			Assert.AreEqual(role, frontage.Role);
			Assert.AreEqual(width, frontage.PreferredWidth);
			Assert.AreEqual(1, frontage.MinimumWidth);
		}

		private static int P(int x, int y)
		{
			return KingdomRoadRules.Pack(x, y, 8);
		}
	}
}
#endif
