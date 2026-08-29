#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomGatehouseRulesTests
	{
		[TestCase(KingdomRules.Frontier.North, KingdomGatehouseOrientation.North, 10, 1)]
		[TestCase(KingdomRules.Frontier.East, KingdomGatehouseOrientation.East, 18, 10)]
		[TestCase(KingdomRules.Frontier.South, KingdomGatehouseOrientation.South, 10, 18)]
		[TestCase(KingdomRules.Frontier.West, KingdomGatehouseOrientation.West, 1, 10)]
		public void RoadEndpointFreezesDeterministicInwardOrientation(
			KingdomRules.Frontier edge, KingdomGatehouseOrientation expected, int x, int y)
		{
			Assert.IsTrue(KingdomGatehouseRules.TryPlan(20, 20, edge, 10, 10,
				out KingdomGatehousePlan plan));
			Assert.AreEqual(expected, plan.Orientation);
			Assert.AreEqual(x, plan.GateX);
			Assert.AreEqual(y, plan.GateY);
			Assert.AreEqual(3, plan.X2 - plan.X1 + 1);
			Assert.AreEqual(3, plan.Y2 - plan.Y1 + 1);
		}

		[Test]
		public void StoneGuardsTimberWatchesAndOpenCenterlineAreExact()
		{
			Assert.IsTrue(KingdomGatehouseRules.TryPlan(80, 25,
				KingdomRules.Frontier.North, 40, 12, out KingdomGatehousePlan plan));
			HashSet<string> occupied = new HashSet<string>();
			int stone = 0;
			int timber = 0;
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
			{
				Assert.IsTrue(KingdomGatehouseRules.TrySatellite(plan, i,
					out KingdomGatehouseCell cell));
				Assert.IsTrue(occupied.Add(cell.X + "," + cell.Y));
				if (cell.Blueprint == KingdomGatehouseRules.StoneBlueprint) stone++;
				if (cell.Blueprint == KingdomGatehouseRules.WatchBlueprint) timber++;
			}
			Assert.AreEqual(4, stone, "four material-honest stone guard walls");
			Assert.AreEqual(2, timber, "two functional timber watch benches");
			for (int i = 0; i < KingdomGatehouseRules.PassageCount; i++)
			{
				Assert.IsTrue(KingdomGatehouseRules.TryPassage(plan, i,
					out KingdomGatehouseCell passage));
				Assert.IsFalse(occupied.Contains(passage.X + "," + passage.Y),
					"the road centerline must never receive a wall or fixture");
			}
			Assert.AreEqual(KingdomGatehouseRules.FootprintCells,
				occupied.Count + KingdomGatehouseRules.PassageCount);
		}

		[Test]
		public void RoadPassageHasAnApproachOnBothSidesOfTheDoor()
		{
			Assert.IsTrue(KingdomGatehouseRules.TryPlan(20, 20,
				KingdomRules.Frontier.West, 10, 10, out KingdomGatehousePlan plan));
			Assert.IsTrue(KingdomGatehouseRules.TryApproach(plan, 0,
				out KingdomGatehouseCell outside));
			Assert.IsTrue(KingdomGatehouseRules.TryPassage(plan, 0,
				out KingdomGatehouseCell door));
			Assert.IsTrue(KingdomGatehouseRules.TryPassage(plan, 1,
				out KingdomGatehouseCell throat));
			Assert.IsTrue(KingdomGatehouseRules.TryPassage(plan, 2,
				out KingdomGatehouseCell room));
			Assert.IsTrue(KingdomGatehouseRules.TryApproach(plan, 1,
				out KingdomGatehouseCell inside));
			Assert.AreEqual(outside.X + 1, door.X);
			Assert.AreEqual(door.X + 1, throat.X);
			Assert.AreEqual(throat.X + 1, room.X);
			Assert.AreEqual(room.X + 1, inside.X);
			Assert.AreEqual(outside.Y, inside.Y);
		}

		[Test]
		public void FrozenPlanRoundTripsCanonicallyAndRejectsMutation()
		{
			Assert.IsTrue(KingdomGatehouseRules.TryPlan(80, 25,
				KingdomRules.Frontier.South, 40, 12, out KingdomGatehousePlan plan));
			Assert.IsTrue(KingdomGatehouseRules.TryEncode(plan, out string encoded));
			Assert.IsTrue(KingdomGatehouseRules.TryDecode(encoded,
				out KingdomGatehousePlan decoded));
			Assert.AreEqual(plan.GateX, decoded.GateX);
			Assert.AreEqual(plan.GateY, decoded.GateY);
			Assert.IsFalse(KingdomGatehouseRules.TryDecode(encoded + ",0", out _));
			Assert.IsFalse(KingdomGatehouseRules.TryDecode(
				encoded.Replace("v1,3", "v1,03"), out _));
		}

		[TestCase("common", "r_KingdomStructureSandstone", "r_KingdomFixtureChairStone",
			0, 0, 0, 44, 0, 6)]
		[TestCase("verdant", "r_KingdomStructureBrinestalkWall", "r_KingdomFixtureBenchTimber",
			0, 10, 34, 0, 0, 6)]
		[TestCase("fungal", "r_KingdomStructureMushroomWall", "r_KingdomFixtureCushionCanvas",
			24, 4, 16, 0, 0, 6)]
		[TestCase("gyre", "r_KingdomStructureLimestone", "r_KingdomFixtureChairMarble",
			0, 0, 0, 28, 16, 6)]
		[TestCase("eater", "r_KingdomRubbleWall", "r_KingdomFixtureChairStone",
			0, 0, 0, 34, 0, 16)]
		public void V2FormsFreezeExactGeometryFixturesAndMaterialClaims(string style,
			string wall, string watch, int mud, int brush, int timber, int stone,
			int marble, int scrap)
		{
			Assert.IsTrue(KingdomGatehouseRules.TryPlan(80, 25,
				KingdomRules.Frontier.North, 40, 12, style,
				out KingdomGatehousePlan plan));
			Assert.AreEqual(2, plan.ReceiptVersion);
			Assert.AreEqual(style, plan.FormKey);
			Assert.IsTrue(KingdomGatehouseRules.TryEncode(plan, out string receipt));
			Assert.LessOrEqual(receipt.Length, KingdomGatehouseRules.MaxReceiptChars);
			Assert.AreEqual(3, plan.X2 - plan.X1 + 1);
			Assert.AreEqual(3, plan.Y2 - plan.Y1 + 1);
			HashSet<string> occupied = new HashSet<string>();
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
			{
				Assert.IsTrue(KingdomGatehouseRules.TrySatellite(plan, i,
					out KingdomGatehouseCell satellite));
				Assert.AreEqual(i < 4 ? wall : watch, satellite.Blueprint);
				Assert.IsTrue(occupied.Add(satellite.X + "," + satellite.Y));
			}
			for (int i = 0; i < KingdomGatehouseRules.PassageCount; i++)
			{
				Assert.IsTrue(KingdomGatehouseRules.TryPassage(plan, i,
					out KingdomGatehouseCell passage));
				Assert.IsFalse(occupied.Contains(passage.X + "," + passage.Y));
			}
			Assert.IsTrue(KingdomGatehouseRules.TryMaterialCost(plan,
				out KingdomMaterialDebitCost cost));
			Assert.AreEqual(50, cost.Materials.Total());
			Assert.AreEqual(mud, cost.Materials.Get(KingdomMaterial.Mud));
			Assert.AreEqual(brush, cost.Materials.Get(KingdomMaterial.Brush));
			Assert.AreEqual(timber, cost.Materials.Get(KingdomMaterial.Timber));
			Assert.AreEqual(stone, cost.Materials.Get(KingdomMaterial.Stone));
			Assert.AreEqual(marble, cost.Materials.Get(KingdomMaterial.Marble));
			Assert.AreEqual(scrap, cost.Materials.Get(KingdomMaterial.Scrap));
			Assert.AreEqual(0, cost.Materials.Get(KingdomMaterial.ShapedTimber));
			Assert.AreEqual(0, cost.Materials.Get(KingdomMaterial.ShapedStone));
			Assert.AreEqual(0, cost.Materials.Get(KingdomMaterial.WorkedMetal));
			Assert.IsTrue(KingdomGatehouseRules.MaterialClaimMatches(plan,
				cost.ToClaimString()));
		}

		[TestCase("common", 0, 0, 0, 22, 0, 3)]
		[TestCase("verdant", 0, 5, 17, 0, 0, 3)]
		[TestCase("fungal", 12, 2, 8, 0, 0, 3)]
		[TestCase("gyre", 0, 0, 0, 14, 8, 3)]
		[TestCase("eater", 0, 0, 0, 17, 0, 8)]
		public void EveryV2RepairPricesFromItsFrozenPaidForm(string style,
			int mud, int brush, int timber, int stone, int marble, int scrap)
		{
			Assert.IsTrue(KingdomGatehouseRules.TryPlan(80, 25,
				KingdomRules.Frontier.North, 40, 12, style,
				out KingdomGatehousePlan plan));
			Assert.IsTrue(KingdomGatehouseRules.TryMaterialCost(plan,
				out KingdomMaterialDebitCost paid));
			Assert.IsTrue(KingdomGatehouseRules.MaterialClaimMatches(plan,
				paid.ToClaimString()));
			KingdomMaterialTally repair = KingdomMaterialRules.RepairCost(
				paid.Materials, 50);
			Assert.AreEqual(25, repair.Total());
			Assert.AreEqual(mud, repair.Get(KingdomMaterial.Mud));
			Assert.AreEqual(brush, repair.Get(KingdomMaterial.Brush));
			Assert.AreEqual(timber, repair.Get(KingdomMaterial.Timber));
			Assert.AreEqual(stone, repair.Get(KingdomMaterial.Stone));
			Assert.AreEqual(marble, repair.Get(KingdomMaterial.Marble));
			Assert.AreEqual(scrap, repair.Get(KingdomMaterial.Scrap));
			Assert.IsTrue(KingdomMaterialRules.RepairBits(paid.Bits, 50).IsEmpty());
		}

		[Test]
		public void EveryV2FormKeepsTopologyAcrossAllFourFrontiers()
		{
			string[] styles = new string[] { "common", "verdant", "fungal", "gyre", "eater" };
			KingdomRules.Frontier[] edges = new KingdomRules.Frontier[]
			{
				KingdomRules.Frontier.North, KingdomRules.Frontier.East,
				KingdomRules.Frontier.South, KingdomRules.Frontier.West
			};
			for (int s = 0; s < styles.Length; s++)
			for (int e = 0; e < edges.Length; e++)
			{
				Assert.IsTrue(KingdomGatehouseRules.TryPlan(80, 25, edges[e],
					40, 12, styles[s], out KingdomGatehousePlan plan));
				HashSet<string> occupied = new HashSet<string>();
				for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
				{
					Assert.IsTrue(KingdomGatehouseRules.TrySatellite(plan, i,
						out KingdomGatehouseCell cell));
					Assert.IsTrue(occupied.Add(cell.X + "," + cell.Y));
				}
				for (int i = 0; i < KingdomGatehouseRules.PassageCount; i++)
				{
					Assert.IsTrue(KingdomGatehouseRules.TryPassage(plan, i,
						out KingdomGatehouseCell passage));
					Assert.IsFalse(occupied.Contains(passage.X + "," + passage.Y));
				}
				Assert.IsTrue(KingdomGatehouseRules.TryApproach(plan, 0, out _));
				Assert.IsTrue(KingdomGatehouseRules.TryApproach(plan, 1, out _));
			}
		}

		[TestCase("common", "&W", "&y", "y", "177", "&r^W", "&r", "W",
			"190", "&K", "&w", "W", "Items/sw_bench.bmp")]
		[TestCase("verdant", "&g", "&g", "G", "215", "&w^y", "&w", "y",
			"190", "&g", "&g", "G", "Items/sw_bench.bmp")]
		[TestCase("fungal", "&m", "&m", "M", "007", "&y^Y", "&y", "Y",
			"009", "&m", "&m", "M", "Items/sw_cushion1.bmp")]
		[TestCase("gyre", "&c", "&W", "Y", "177", "&W^c", "&W", "Y",
			"190", "&W", "&W", "Y", "Items/sw_bench.bmp")]
		[TestCase("eater", "&r", "&y", "C", "178", "&r", "&w", "w",
			"190", "&r", "&w", "C", "Items/sw_bench.bmp")]
		public void V2FormsFreezeExactRootWallAndWatchPalettes(string style,
			string rootColor, string rootTileColor, string rootDetail,
			string wallRender, string wallColor, string wallTileColor, string wallDetail,
			string watchRender, string watchColor, string watchTileColor,
			string watchDetail, string watchTile)
		{
			Assert.IsTrue(KingdomGatehouseRules.TryPlan(80, 25,
				KingdomRules.Frontier.North, 40, 12, style,
				out KingdomGatehousePlan plan));
			Assert.IsTrue(KingdomGatehouseRules.TryRootRender(plan,
				out string rootRender, out string actualRootColor,
				out string actualRootTileColor, out string actualRootDetail,
				out string closedTile, out string openTile));
			Assert.AreEqual("+", rootRender);
			Assert.AreEqual(rootColor, actualRootColor);
			Assert.AreEqual(rootTileColor, actualRootTileColor);
			Assert.AreEqual(rootDetail, actualRootDetail);
			Assert.AreEqual("Items/sw_fence_gates_2_open.bmp", closedTile);
			Assert.AreEqual("Items/sw_fence_gates_closed.bmp", openTile);
			Assert.IsTrue(KingdomGatehouseRules.TrySatelliteRender(plan, 0,
				out string actualWallRender, out string actualWallColor,
				out string actualWallTileColor, out string actualWallDetail, out string wallTile));
			Assert.AreEqual(wallRender, actualWallRender);
			Assert.AreEqual(wallColor, actualWallColor);
			Assert.AreEqual(wallTileColor, actualWallTileColor);
			Assert.AreEqual(wallDetail, actualWallDetail);
			Assert.IsNull(wallTile, "painted wall adjacency owns its tile family");
			Assert.IsTrue(KingdomGatehouseRules.TrySatelliteRender(plan, 5,
				out string actualWatchRender, out string actualWatchColor,
				out string actualWatchTileColor, out string actualWatchDetail,
				out string actualWatchTile));
			Assert.AreEqual(watchRender, actualWatchRender);
			Assert.AreEqual(watchColor, actualWatchColor);
			Assert.AreEqual(watchTileColor, actualWatchTileColor);
			Assert.AreEqual(watchDetail, actualWatchDetail);
			Assert.AreEqual(watchTile, actualWatchTile);
		}

		[Test]
		public void V2RoundTripIsCanonicalBoundedAndTamperEvident()
		{
			Assert.IsTrue(KingdomGatehouseRules.TryPlan(80, 25,
				KingdomRules.Frontier.South, 40, 12, "fungal",
				out KingdomGatehousePlan plan));
			Assert.IsTrue(KingdomGatehouseRules.TryEncode(plan, out string encoded));
			StringAssert.StartsWith("v2,", encoded);
			Assert.LessOrEqual(encoded.Length, KingdomGatehouseRules.MaxReceiptChars);
			Assert.IsTrue(KingdomGatehouseRules.TryDecode(encoded,
				out KingdomGatehousePlan decoded));
			Assert.IsTrue(KingdomGatehouseRules.TryEncode(decoded, out string roundTrip));
			Assert.AreEqual(encoded, roundTrip);
			char replacement = encoded[encoded.Length / 2] == 'A' ? 'B' : 'A';
			string tampered = encoded.Substring(0, encoded.Length / 2) + replacement
				+ encoded.Substring(encoded.Length / 2 + 1);
			Assert.IsFalse(KingdomGatehouseRules.TryDecode(tampered, out _));
			Assert.IsFalse(KingdomGatehouseRules.TryDecode(
				encoded.Substring(0, encoded.Length - 1), out _));
			Assert.IsFalse(KingdomGatehouseRules.TryDecode(
				new string('x', KingdomGatehouseRules.MaxReceiptChars + 1), out _));
			decoded.FormKey = "unknown";
			Assert.IsFalse(KingdomGatehouseRules.TryEncode(decoded, out _));
			Assert.IsTrue(KingdomGatehouseRules.TryDecode(encoded, out decoded));
			decoded.WallBlueprint = KingdomGatehouseRules.StoneBlueprint;
			Assert.IsFalse(KingdomGatehouseRules.TryEncode(decoded, out _));
			Assert.IsTrue(KingdomGatehouseRules.TryDecode(encoded, out decoded));
			decoded.RootColorString = "&W";
			Assert.IsFalse(KingdomGatehouseRules.TryEncode(decoded, out _));
			Assert.IsFalse(KingdomGatehouseRules.TryDecode(
				encoded.Replace("v2,3,", "v2,03,"), out _));
			Assert.IsFalse(KingdomGatehouseRules.TryDecode(
				"v3" + encoded.Substring(2), out _));
			Assert.IsTrue(KingdomGatehouseRules.TryDecode(encoded, out decoded));
			decoded.ReceiptVersion = 0;
			Assert.IsFalse(KingdomGatehouseRules.TryEncode(decoded, out _));
		}

		[Test]
		public void UnknownLiveStyleFallsBackToCommonBeforeReceiptPublication()
		{
			Assert.IsTrue(KingdomGatehouseRules.TryPlan(80, 25,
				KingdomRules.Frontier.North, 40, 12, "third-party-style",
				out KingdomGatehousePlan fallback));
			Assert.AreEqual("common", fallback.FormKey);
			Assert.AreEqual(KingdomGatehouseRules.StoneBlueprint, fallback.WallBlueprint);
		}

		[Test]
		public void FrozenFormRefusesAChangedMaterialClaim()
		{
			Assert.IsTrue(KingdomGatehouseRules.TryPlan(80, 25,
				KingdomRules.Frontier.North, 40, 12, "gyre",
				out KingdomGatehousePlan plan));
			KingdomMaterialTally wrong = new KingdomMaterialTally();
			wrong.Set(KingdomMaterial.Stone, 50);
			string wrongClaim = new KingdomMaterialDebitCost(wrong).ToClaimString();
			Assert.IsTrue(KingdomGatehouseRules.TryMaterialCost(plan,
				out KingdomMaterialDebitCost exact));
			Assert.IsTrue(KingdomGatehouseRules.MaterialClaimMatches(plan,
				exact.ToClaimString()));
			Assert.IsFalse(KingdomGatehouseRules.MaterialClaimMatches(plan, null));
			Assert.IsFalse(KingdomGatehouseRules.MaterialClaimMatches(plan,
				exact.ToClaimString() + "x"));
			Assert.IsFalse(KingdomGatehouseRules.MaterialClaimMatches(plan, wrongClaim));
			plan.MaterialClaim = wrongClaim;
			Assert.IsFalse(KingdomGatehouseRules.TryEncode(plan, out _));
		}

		[Test]
		public void LiteralV1MigrationReceiptReencodesWithoutChangingIdentity()
		{
			const string legacy = "v1,1,40,1,39,1,41,3";
			Assert.IsTrue(KingdomGatehouseRules.TryDecode(legacy,
				out KingdomGatehousePlan plan));
			Assert.AreEqual(1, plan.ReceiptVersion);
			Assert.IsNull(plan.FormKey);
			Assert.IsNull(plan.MaterialClaim);
			Assert.IsTrue(KingdomGatehouseRules.TryEncode(plan, out string roundTrip));
			Assert.AreEqual(legacy, roundTrip);
			KingdomMaterialTally historicalPaid = new KingdomMaterialTally();
			historicalPaid.Set(KingdomMaterial.Stone, 34);
			historicalPaid.Set(KingdomMaterial.Timber, 10);
			historicalPaid.Set(KingdomMaterial.Scrap, 6);
			string paidClaim = new KingdomMaterialDebitCost(historicalPaid).ToClaimString();
			Assert.IsTrue(KingdomGatehouseRules.MaterialClaimMatches(plan, paidClaim));
			Assert.IsFalse(KingdomGatehouseRules.MaterialClaimMatches(plan, paidClaim + "x"));
			string[] ids = new string[]
			{
				"taf:gatehouse:v1:3c9a464d585e4f3aef5060274a81891c880fa570140f9286bdfc556b64d52da5",
				"taf:gatehouse:v1:a0c92e1d514049ba1c12d6ab7e96d897fe76ad29866310668d131b56c099b21a",
				"taf:gatehouse:v1:bd287694eb9d91a115e1166981c0dc562125a0523fe0a3ed88acbfeaa588248b",
				"taf:gatehouse:v1:88b2340142fc5039e2751ec0ec026e8c0d094f9060ed4f293da273e18e80cf19",
				"taf:gatehouse:v1:09c758ca564cc2f901fe0efd244e85c6d00976fad4b659f8a7e106432c13052c",
				"taf:gatehouse:v1:5602f102313f7baa4eeb5be1387ad21987a8579afccef62edbe61bfc58d3b78a"
			};
			for (int i = 0; i < ids.Length; i++)
				Assert.AreEqual(ids[i], KingdomGatehouseProjectionRules.StableSatelliteId(
					"root-one", legacy, i));
		}

		[Test]
		public void HistoricalPendingV1FixtureExecutesCarrierRemovalSchemaRecovery()
		{
			XElement fixture = XDocument.Parse(TestMain.ReadRepositoryText(Path.Combine(
				"DevTests", "Compatibility", "KingdomGatehousePendingV1.fixture.xml"))).Root;
			string rootId = (string)fixture.Attribute("RootId");
			string receipt = (string)fixture.Attribute("Plan");
			Assert.IsTrue(KingdomGatehouseRules.TryDecode(receipt,
				out KingdomGatehousePlan plan));
			Assert.AreEqual(1, plan.ReceiptVersion);
			Assert.IsTrue(KingdomGatehouseRules.TryEncode(plan, out string canonical));
			Assert.AreEqual(receipt, canonical);
			string paidClaim = (string)fixture.Attribute("PaidClaim");
			Assert.IsTrue(KingdomMaterialDebitCost.TryParseClaim(paidClaim,
				out KingdomMaterialDebitCost paid));
			Assert.AreEqual(paidClaim, paid.ToClaimString());
			Assert.IsTrue(KingdomGatehouseRules.MaterialClaimMatches(plan, paidClaim));

			XElement publicationCuts = fixture.Element("publication-cuts");
			Assert.IsNotNull(publicationCuts);
			foreach (XElement cut in publicationCuts.Elements("legacy"))
			{
				string name = (string)cut.Attribute("Name");
				KingdomGatehouseSlotState state = (KingdomGatehouseSlotState)Enum.Parse(
					typeof(KingdomGatehouseSlotState), (string)cut.Attribute("State"));
				KingdomGatehouseSlotEvidence evidence = (KingdomGatehouseSlotEvidence)Enum.Parse(
					typeof(KingdomGatehouseSlotEvidence), (string)cut.Attribute("Evidence"));
				KingdomGatehouseLegacyPublicationAction expected =
					(KingdomGatehouseLegacyPublicationAction)Enum.Parse(
						typeof(KingdomGatehouseLegacyPublicationAction),
						(string)cut.Attribute("Expected"));
				KingdomGatehouseLegacyPublicationAction action =
					KingdomGatehouseProjectionRules.ResolveLegacyPublicationCut(
						(int)cut.Attribute("Index"), state,
						(bool)cut.Attribute("PublishedIdentity"),
						(bool)cut.Attribute("ExactCarrier"),
						(bool)cut.Attribute("BlueprintExact"),
						(bool)cut.Attribute("Unplaced"),
						(bool)cut.Attribute("BoundedIdentity"),
						(bool)cut.Attribute("UniqueGlobalIdentity"),
						(bool)cut.Attribute("ExistingMarksCompatible"), evidence);
				Assert.AreEqual(expected, action, name);
				Assert.AreEqual((bool)cut.Attribute("CreateNewBody"),
					action == KingdomGatehouseLegacyPublicationAction.Create, name);
				Assert.AreEqual((bool)cut.Attribute("PreserveCarrierIdentity"),
					action != KingdomGatehouseLegacyPublicationAction.Create, name);
			}
			foreach (XElement cut in publicationCuts.Elements("deterministic"))
			{
				string name = (string)cut.Attribute("Name");
				KingdomRules.Frontier frontier = (KingdomRules.Frontier)Enum.Parse(
					typeof(KingdomRules.Frontier), (string)cut.Attribute("Frontier"));
				Assert.IsTrue(KingdomGatehouseRules.TryPlan(
					(int)cut.Attribute("Width"), (int)cut.Attribute("Height"), frontier,
					(int)cut.Attribute("HeartX"), (int)cut.Attribute("HeartY"),
					(string)cut.Attribute("Style"), out KingdomGatehousePlan v2Plan), name);
				Assert.AreEqual(2, v2Plan.ReceiptVersion, name);
				Assert.IsTrue(KingdomGatehouseRules.TryEncode(v2Plan,
					out string v2Receipt), name);
				bool maySerialize = KingdomGatehouseProjectionRules.
					CanSerializeDeterministicCustody(
						(bool)cut.Attribute("PaletteExact"),
						(bool)cut.Attribute("IdentityExact"),
						(bool)cut.Attribute("MarksExact"));
				Assert.AreEqual((bool)cut.Attribute("MaySerialize"), maySerialize, name);
				Assert.AreEqual(0, (int)cut.Attribute("PersistedBodiesAtPreCustodyCut"),
					"nothing before serialized custody may survive reload as an orphan");
				string first = KingdomGatehouseProjectionRules.StableSatelliteId(rootId,
					v2Receipt, (int)cut.Attribute("Index"));
				string retry = KingdomGatehouseProjectionRules.StableSatelliteId(rootId,
					v2Receipt, (int)cut.Attribute("Index"));
				Assert.AreEqual((bool)cut.Attribute("RetryDerivesSameIdentity"),
					first == retry, name);
			}

			XElement[] states = fixture.Element("states").Elements("state").ToArray();
			Assert.AreEqual(KingdomGatehouseRules.SatelliteCount, states.Length);
			Assert.AreEqual(KingdomGatehouseRules.SatelliteCount,
				states.Count(e => (int)e.Attribute("Value")
					== (int)KingdomGatehouseSlotState.Settled));
			XElement[] bodies = fixture.Element("bodies").Elements("body").ToArray();
			Assert.IsTrue(ExactLegacyBodyFixture(plan, receipt, rootId, bodies),
				"the frozen historical body set must be six unique global exact placements");

			foreach (XElement cut in fixture.Elements("cut"))
			{
				string name = (string)cut.Attribute("Name");
				List<XElement> observed = bodies.Select(e => new XElement(e)).ToList();
				if (name == "duplicate-body-after-removal")
					observed.Add(new XElement(bodies[0]));
				else if (name == "missing-body-after-removal")
					observed.RemoveAt(observed.Count - 1);
				bool exactBodies = ExactLegacyBodyFixture(plan, receipt, rootId,
					observed.ToArray());
				Assert.AreEqual((bool)cut.Attribute("ExactSixBodies"), exactBodies, name);
				bool retain = KingdomGatehouseProjectionRules.
					MustRetainLegacyOwnerAcrossSchemaCut(
						(bool)cut.Attribute("SchemaInt"),
						(bool)cut.Attribute("SchemaString"),
						(bool)cut.Attribute("V2Carrier"),
						(bool)cut.Attribute("V1PendingCarrier"),
						states.Length, states.Count(e => (int)e.Attribute("Value")
							== (int)KingdomGatehouseSlotState.Settled),
						(bool)cut.Attribute("CanonicalPlan"),
						(bool)cut.Attribute("UniqueStoredIds"));
				bool resume = KingdomGatehouseProjectionRules.CanResumeLegacySchemaCut(
					(bool)cut.Attribute("SchemaInt"),
					(bool)cut.Attribute("SchemaString"),
					(bool)cut.Attribute("V2Carrier"),
					(bool)cut.Attribute("V1PendingCarrier"),
					states.Length, states.Count(e => (int)e.Attribute("Value")
						== (int)KingdomGatehouseSlotState.Settled),
					(bool)cut.Attribute("CanonicalPlan"),
					(bool)cut.Attribute("UniqueStoredIds"), exactBodies);
				Assert.AreEqual((bool)cut.Attribute("RetainOwner"), retain, name);
				Assert.AreEqual((bool)cut.Attribute("ResumeSchema"), resume, name);
			}
		}

		private static bool ExactLegacyBodyFixture(KingdomGatehousePlan Plan,
			string Receipt, string RootId, IList<XElement> Bodies)
		{
			if (Plan == null || Plan.ReceiptVersion != 1 || Bodies == null
				|| Bodies.Count != KingdomGatehouseRules.SatelliteCount) return false;
			HashSet<int> indices = new HashSet<int>();
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Bodies.Count; i++)
			{
				XElement body = Bodies[i];
				int index = (int)body.Attribute("Index");
				string id = (string)body.Attribute("Id");
				if (!indices.Add(index) || !ids.Add(id)
					|| !KingdomGatehouseProjectionRules.ExactStoredSatelliteId(false,
						RootId, Receipt, index, id)
					|| !KingdomGatehouseRules.TrySatellite(Plan, index,
						out KingdomGatehouseCell spec)
					|| (string)body.Attribute("Owner") != RootId
					|| (string)body.Attribute("Slot") != spec.Slot
					|| (string)body.Attribute("Blueprint") != spec.Blueprint
					|| (int)body.Attribute("X") != spec.X
					|| (int)body.Attribute("Y") != spec.Y
					|| (int)body.Attribute("GlobalCount") != 1
					|| !(bool)body.Attribute("InZone")
					|| (bool)body.Attribute("InCustody")) return false;
			}
			return indices.Count == KingdomGatehouseRules.SatelliteCount
				&& ids.Count == KingdomGatehouseRules.SatelliteCount;
		}

		[Test]
		public void EveryV2FormBindsCallbackRecoveryAndStrikeIdentityToFrozenBytes()
		{
			const string legacy = "v1,1,40,1,39,1,41,3";
			string[] styles = new string[] { "common", "verdant", "fungal", "gyre", "eater" };
			for (int s = 0; s < styles.Length; s++)
			{
				Assert.IsTrue(KingdomGatehouseRules.TryPlan(80, 25,
					KingdomRules.Frontier.North, 40, 12, styles[s],
					out KingdomGatehousePlan plan));
				Assert.IsTrue(KingdomGatehouseRules.TryEncode(plan, out string receipt));
				for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
				{
					string id = KingdomGatehouseProjectionRules.StableSatelliteId(
						"root-one", receipt, i);
					Assert.AreNotEqual(KingdomGatehouseProjectionRules.StableSatelliteId(
						"root-one", legacy, i), id);
					Assert.IsTrue(KingdomGatehouseProjectionRules.ExactSatelliteId(
						"root-one", receipt, i, id));
					Assert.IsFalse(KingdomGatehouseProjectionRules.ExactSatelliteId(
						"root-one", receipt + "x", i, id));
					Assert.AreEqual(KingdomGatehouseSlotAction.Create,
						KingdomGatehouseProjectionRules.Resolve(i,
							KingdomGatehouseSlotState.Pending, true,
							KingdomGatehouseSlotEvidence.Absent));
					Assert.AreEqual(KingdomGatehouseSlotAction.Settle,
						KingdomGatehouseProjectionRules.Resolve(i,
							KingdomGatehouseSlotState.Pending, true,
							KingdomGatehouseSlotEvidence.ExactPlacement));
				}
			}
		}

		[Test]
		public void TypedStrikeIsNonPlotExactAndCannotBePartiallyInvented()
		{
			Assert.IsTrue(KingdomGatehouseRules.IsNetworkStrike("gatehouse", false,
				9, 1, 11, 3, "root-id", 6));
			Assert.IsFalse(KingdomGatehouseRules.IsNetworkStrike("gatehouse", true,
				9, 1, 11, 3, "root-id", 6));
			Assert.IsFalse(KingdomGatehouseRules.IsNetworkStrike("stone-house", false,
				9, 1, 11, 3, "root-id", 6));
			Assert.IsFalse(KingdomGatehouseRules.IsNetworkStrike("gatehouse", false,
				9, 1, 11, 3, "root-id", 5));
		}

		[Test]
		public void ConstructionWireRoundTripsSixNonPlotOwnedTargetsExactly()
		{
			KingdomStrikeIntent intent = new KingdomStrikeIntent
			{
				DisplayName = "gatehouse gate",
				BuildKey = "gatehouse",
				TargetDisplayName = null,
				SalvageClaim = new KingdomMaterialDebitCost().ToClaimString(),
				HasPlot = false,
				X1 = 9,
				Y1 = 1,
				X2 = 11,
				Y2 = 3,
				PlotId = "root-id",
				Effort = 17,
				Targets = new List<KingdomStrikeTarget>()
			};
			for (int i = 0; i < 6; i++)
			{
				intent.Targets.Add(new KingdomStrikeTarget
				{
					Id = "sat-" + i,
					Blueprint = i < 4 ? KingdomGatehouseRules.StoneBlueprint
						: KingdomGatehouseRules.WatchBlueprint,
					X = 9 + i % 3,
					Y = 1 + i / 3
				});
			}
			Assert.IsTrue(KingdomConstructionRules.TryEncodeStrikeIntent(intent,
				out string encoded));
			Assert.IsTrue(KingdomConstructionRules.TryDecodeStrikeIntent(encoded,
				out KingdomStrikeIntent decoded));
			Assert.IsFalse(decoded.HasPlot);
			Assert.AreEqual("root-id", decoded.PlotId);
			Assert.AreEqual(6, decoded.Targets.Count);
			intent.Targets.RemoveAt(5);
			Assert.IsFalse(KingdomConstructionRules.TryEncodeStrikeIntent(intent, out _));
		}

		[Test]
		public void AFrontierEndpointWithoutBothApproachesRefusesInsteadOfMoving()
		{
			Assert.IsFalse(KingdomGatehouseRules.TryPlan(3, 3,
				KingdomRules.Frontier.North, 1, 0, out _));
		}

		[Test]
		public void SatelliteIdentityIsStablePlanBoundAndSlotDistinct()
		{
			Assert.IsTrue(KingdomGatehouseRules.TryPlan(80, 25,
				KingdomRules.Frontier.North, 40, 12, out KingdomGatehousePlan plan));
			Assert.IsTrue(KingdomGatehouseRules.TryEncode(plan, out string receipt));
			string first = KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", receipt, 0);
			Assert.AreEqual(first, KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", receipt, 0));
			StringAssert.StartsWith(KingdomGatehouseProjectionRules.SatelliteIdPrefix, first);
			Assert.AreNotEqual(first, KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", receipt, 1));
			Assert.AreNotEqual(first, KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-two", receipt, 0));
			Assert.IsNull(KingdomGatehouseProjectionRules.StableSatelliteId("", receipt, 0));
			Assert.IsNull(KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", receipt, 6));
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		[TestCase(5)]
		public void EverySatelliteRecoversBothIdentityPublicationCuts(int index)
		{
			Assert.AreEqual(KingdomGatehouseSlotAction.PublishIdentity,
				KingdomGatehouseProjectionRules.Resolve(index,
					KingdomGatehouseSlotState.Empty, false,
					KingdomGatehouseSlotEvidence.Absent));
			Assert.AreEqual(KingdomGatehouseSlotAction.PublishPending,
				KingdomGatehouseProjectionRules.Resolve(index,
					KingdomGatehouseSlotState.Empty, true,
					KingdomGatehouseSlotEvidence.Absent),
				"cold load between identity and pending writes resumes without a new ID");
			Assert.AreEqual(KingdomGatehouseSlotAction.Create,
				KingdomGatehouseProjectionRules.Resolve(index,
					KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.Absent));
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		[TestCase(5)]
		public void EverySatelliteRecoversBothCallbackCutsAndCleanupVeto(int index)
		{
			Assert.AreEqual(KingdomGatehouseSlotAction.Create,
				KingdomGatehouseProjectionRules.Resolve(index,
					KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.Absent),
				"throw-before-effect with proved cleanup reuses frozen identity");
			Assert.AreEqual(KingdomGatehouseSlotAction.Place,
				KingdomGatehouseProjectionRules.Resolve(index,
					KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.Staged),
				"cleanup veto retains exact staged custody for retry");
			Assert.AreEqual(KingdomGatehouseSlotAction.Settle,
				KingdomGatehouseProjectionRules.Resolve(index,
					KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.ExactPlacement),
				"throw-after-effect recovers exact landed identity");
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		[TestCase(5)]
		public void EverySatelliteColdLoadRefusesForeignDuplicateOrLostSettlement(int index)
		{
			Assert.AreEqual(KingdomGatehouseSlotAction.Settle,
				KingdomGatehouseProjectionRules.Resolve(index,
					KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.ExactPlacement));
			Assert.AreEqual(KingdomGatehouseSlotAction.Refuse,
				KingdomGatehouseProjectionRules.Resolve(index,
					KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.Foreign));
			Assert.AreEqual(KingdomGatehouseSlotAction.Refuse,
				KingdomGatehouseProjectionRules.Resolve(index,
					KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.Duplicate));
			Assert.AreEqual(KingdomGatehouseSlotAction.Refuse,
				KingdomGatehouseProjectionRules.Resolve(index,
					KingdomGatehouseSlotState.Settled, true,
					KingdomGatehouseSlotEvidence.Absent));
			Assert.IsFalse(KingdomGatehouseProjectionRules.CanClearCustody(
				KingdomGatehouseSlotState.Pending, true,
				KingdomGatehouseSlotEvidence.Staged), "cleanup veto cannot erase custody");
			Assert.IsTrue(KingdomGatehouseProjectionRules.CanClearCustody(
				KingdomGatehouseSlotState.Pending, true,
				KingdomGatehouseSlotEvidence.Absent));
			Assert.IsTrue(KingdomGatehouseProjectionRules.HasLiveCustody(
				KingdomGatehouseSlotEvidence.Staged));
			Assert.IsTrue(KingdomGatehouseProjectionRules.HasLiveCustody(
				KingdomGatehouseSlotEvidence.ExactPlacement));
			Assert.AreEqual(KingdomGatehouseSlotAction.Refuse,
				KingdomGatehouseProjectionRules.Resolve(index,
					KingdomGatehouseSlotState.Contested, true,
					KingdomGatehouseSlotEvidence.ExactPlacement));
		}
	}
}
#endif
