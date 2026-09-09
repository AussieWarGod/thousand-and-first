#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The rite ground is a camp: a horseshoe of canvas round the basin, a cooking fire on its
	/// own hearthstone, and one dry store that rides every heart rung at the same rite-relative
	/// cell. Everything asserted here is authored fact - the maps, the palettes, the tiers and
	/// the catalogue bills - so a later edit that moves the store, drops the hearth slot, or
	/// trims the timber out of a transition bill fails here rather than in a founded settlement.
	/// </summary>
	public class KingdomCampHeartTests
	{
		private const string StoreRole = "fixture:storage";
		private const string HearthRole = "fixture:hearth";
		private const string StoreBlueprint = "r_KingdomHeartStockpile";
		private const string HearthBlueprint = "r_KingdomCivicCampfire";
		private const string CanvasBlueprint = "r_KingdomStructureCanvasWall";

		/// <summary>Build key, map key, palette key, the claim the store cell declares at that
		/// rung, and the cover it declares. Yard and open under canvas; building and walled from
		/// the moot up, because an open-cover cell dropped into a roofed hall is a bare leak in
		/// four directions and the map would not compile.</summary>
		private static readonly string[][] Rungs = new string[][]
		{
			new string[] { "heartbasin", "civic-heartbasin-s0", "civic-heart-natural", "Yard", "Open" },
			new string[] { "heartwaterstone", "civic-heartwaterstone-m1", "civic-heart-stone", "Yard", "Open" },
			new string[] { "heartmoot", "civic-heartmoot-l2", "civic-heart-moot", "Building", "Walled" },
			new string[] { "heartcourt", "civic-heartcourt-xl3", "civic-heart-court", "Building", "Walled" },
			new string[] { "arcology", "civic-arcology-xl4", "civic-heart-court", "Building", "Walled" }
		};

		[Test]
		public void EveryHeartRungKeepsTheStoreAtTheSameRiteRelativeCell()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			for (int i = 0; i < Rungs.Length; i++)
			{
				ArchitectureMapDraft map = Map(corpus, Rungs[i][1]);
				int[] store = Single(map, StoreRole);
				int[] main = Single(map, "main");
				ClassicAssert.AreEqual(2, store[0] - main[0], Rungs[i][0] + " store x offset");
				ClassicAssert.AreEqual(1, store[1] - main[1], Rungs[i][0] + " store y offset");
			}
		}

		[Test]
		public void TheStoreSlotIsByteIdenticalInEveryHeartPaletteThatCarriesIt()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Rungs.Length; i++)
			{
				ArchitecturePaletteSlot slot = Slot(corpus, Rungs[i][2], "store");
				identities.Add(string.Join("|", new string[] { slot.Blueprint, slot.Role,
					slot.Material, slot.MinTech, slot.Knowledge ?? "", slot.Power ?? "",
					slot.Natural ? "yes" : "no" }));
			}
			// One identity across all four heart palettes, or the retained-placement law refuses
			// the very first upgrade a settlement takes.
			ClassicAssert.AreEqual(1, identities.Count, string.Join(" / ", identities));
			ArchitecturePaletteSlot first = Slot(corpus, "civic-heart-natural", "store");
			ClassicAssert.AreEqual(StoreBlueprint, first.Blueprint);
			ClassicAssert.AreEqual("timber", first.Material);
			ClassicAssert.IsFalse(first.Natural);
		}

		[Test]
		public void TheStoreCellDeclaresTheClaimAndCoverItsRungCanCarry()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			for (int i = 0; i < Rungs.Length; i++)
			{
				ArchitectureMapDraft map = Map(corpus, Rungs[i][1]);
				int[] store = Single(map, StoreRole);
				ArchitectureGlyphDraft glyph = At(map, store[0], store[1]);
				ClassicAssert.AreEqual(Rungs[i][3], glyph.Claim.ToString(), Rungs[i][0] + " claim");
				ClassicAssert.AreEqual(Rungs[i][4], Cover(map, glyph).ToString(), Rungs[i][0] + " cover");
				ClassicAssert.AreEqual(ArchitecturePassability.Adjacent, glyph.Passability,
					Rungs[i][0] + " store is used from beside it, never stood on");
				ClassicAssert.IsTrue(glyph.StatefulObject, Rungs[i][0] + " store must be stateful");
			}
		}

		[Test]
		public void FromTheMootUpEveryNeighbourOfTheStoreCellIsInsideTheWalls()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			int[] dx = new int[] { 0, 1, 0, -1 };
			int[] dy = new int[] { -1, 0, 1, 0 };
			for (int i = 2; i < Rungs.Length; i++)
			{
				ArchitectureMapDraft map = Map(corpus, Rungs[i][1]);
				int[] store = Single(map, StoreRole);
				for (int d = 0; d < 4; d++)
				{
					ArchitectureGlyphDraft glyph = At(map, store[0] + dx[d], store[1] + dy[d]);
					ClassicAssert.IsNotNull(glyph, Rungs[i][0] + " neighbour " + d + " is off-map");
					ClassicAssert.AreEqual(ArchitectureClaim.Building, glyph.Claim,
						Rungs[i][0] + " neighbour " + d);
					ClassicAssert.AreEqual(ArchitectureCover.Walled, Cover(map, glyph),
						Rungs[i][0] + " neighbour " + d);
				}
			}
		}

		[Test]
		public void TheRiteGroundIsSevenCanvasCellsOneFireOneStoreAndTwoEntrances()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			ArchitectureMapDraft map = Map(corpus, "civic-heartbasin-s0");
			ClassicAssert.AreEqual(7, Count(map, g => g.Structure == "$canvas"), "canvas cells");
			ClassicAssert.AreEqual(1, Count(map, g => g.Object == "$hearth"), "hearths");
			ClassicAssert.AreEqual(1, Count(map, g => g.Object == "$store"), "stores");
			ClassicAssert.AreEqual(2, Count(map, g => g.Anchors.Contains("entrance:public")),
				"entrances");
			// The horseshoe opens south, onto the two-cell approach the rite already declared.
			ClassicAssert.AreEqual(".####.", map.Rows[0]);
			ClassicAssert.AreEqual("#RB@R#", map.Rows[1]);
			ClassicAssert.AreEqual("#h0PPS", map.Rows[2]);
			ClassicAssert.AreEqual("..EE..", map.Rows[3]);
		}

		[Test]
		public void TheCampStoreStandsOnYardGroundOutsideTheFrozenFootprint()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			ArchitectureMapDraft map = Map(corpus, "civic-heartbasin-s0");
			ClassicAssert.IsTrue(map.HasFootprint);
			ClassicAssert.AreEqual(1, map.FootprintX);
			ClassicAssert.AreEqual(0, map.FootprintY);
			ClassicAssert.AreEqual(4, map.FootprintWidth);
			ClassicAssert.AreEqual(4, map.FootprintHeight);
			int[] store = Single(map, StoreRole);
			// Outside the frozen 4x4, which is lawful ONLY while it stays yard-claimed: a
			// building claim outside the footprint is refused outright.
			ClassicAssert.IsTrue(store[0] < map.FootprintX
				|| store[0] >= map.FootprintX + map.FootprintWidth, "store is inside the footprint");
			ClassicAssert.AreEqual(ArchitectureClaim.Yard, At(map, store[0], store[1]).Claim);
		}

		[Test]
		public void TheCampFireBurnsOnItsOwnHearthstoneAndCarriesNothingElse()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			for (int i = 0; i < 2; i++)
			{
				ArchitectureMapDraft map = Map(corpus, Rungs[i][1]);
				int[] hearth = Single(map, HearthRole);
				ArchitectureGlyphDraft glyph = At(map, hearth[0], hearth[1]);
				ClassicAssert.AreEqual("$hearth", glyph.Object, Rungs[i][0]);
				// The vanilla Campfire part heats the objects standing on ITS OWN cell every
				// turn and nothing beyond it. So the fire's cell must carry no other fabric,
				// and nobody may be stood on it: the canvas one cell away is safe by geometry,
				// not by any tag on the blueprint (no such tag exists in the engine).
				ClassicAssert.IsNull(glyph.Structure, Rungs[i][0] + " hearth carries a structure");
				ClassicAssert.AreEqual(ArchitecturePassability.Adjacent, glyph.Passability,
					Rungs[i][0] + " hearth cell must be used from beside it");
				ClassicAssert.IsFalse(glyph.StatefulObject,
					Rungs[i][0] + " the fire is ordinary fabric and is struck at the moot");
				ArchitecturePaletteSlot slot = Slot(corpus, Rungs[i][2], "hearth");
				ClassicAssert.AreEqual(HearthBlueprint, slot.Blueprint, Rungs[i][0]);
				ClassicAssert.AreEqual("timber", slot.Material, Rungs[i][0]);
			}
			// A4: the waterstone palette had no hearth slot at all, so a fire re-laid there had
			// nothing to resolve against and the tier refused.
			ClassicAssert.IsNotNull(Slot(corpus, "civic-heart-stone", "hearth"));
			ClassicAssert.IsNotNull(Slot(corpus, "civic-heart-natural", "canvas"));
			ClassicAssert.AreEqual(CanvasBlueprint,
				Slot(corpus, "civic-heart-natural", "canvas").Blueprint);
			ClassicAssert.AreEqual("canvas", Slot(corpus, "civic-heart-natural", "canvas").Material);
		}

		[Test]
		public void EveryHeartRungCompilesInAllFourFacingsWithExactlyOneStore()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			int compiled = 0;
			for (int i = 0; i < Rungs.Length; i++)
			{
				ArchitectureCorpusCase item = Case(corpus, Rungs[i][0]);
				foreach (ArchitectureFacing facing in Enum.GetValues(typeof(ArchitectureFacing)))
				{
					ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(
						KingdomArchitectureCorpusFixture.Request(corpus, item, facing),
						out ArchitectureLayoutSnapshot snapshot, out string failure),
						Rungs[i][0] + " " + facing + ": " + failure);
					List<ArchitecturePlacement> stores = snapshot.Placements.Where(value =>
						KingdomArchitectureRules.AnchorRole(value.StatefulAnchor) == StoreRole)
						.ToList();
					ClassicAssert.AreEqual(1, stores.Count, Rungs[i][0] + " " + facing);
					ClassicAssert.AreEqual(StoreBlueprint, stores[0].Blueprint);
					ClassicAssert.AreEqual("timber", stores[0].Material);
					ClassicAssert.IsFalse(stores[0].Natural);
					ClassicAssert.IsFalse(stores[0].ExistingAuthority);
					compiled++;
				}
			}
			ClassicAssert.AreEqual(Rungs.Length * 4, compiled);
		}

		[Test]
		public void AStandingStoreIsRetainedAcrossEveryHeartTransition()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			int checkedTransitions = 0;
			for (int i = 0; i + 1 < Rungs.Length; i++)
			{
				foreach (ArchitectureFacing facing in Enum.GetValues(typeof(ArchitectureFacing)))
				{
					ArchitectureLayoutDelta delta = Delta(corpus, Rungs[i][0], Rungs[i + 1][0],
						facing, false);
					ClassicAssert.AreEqual(1, delta.Retained.Count(value =>
						KingdomArchitectureRules.AnchorRole(value.StatefulAnchor) == StoreRole),
						Rungs[i][0] + "->" + Rungs[i + 1][0] + " " + facing);
					ClassicAssert.AreEqual(0, delta.Removed.Count(value =>
						value.Blueprint == StoreBlueprint), "a store is never struck");
					ClassicAssert.AreEqual(0, delta.Added.Count(value =>
						value.Blueprint == StoreBlueprint), "a standing store is never re-added");
					checkedTransitions++;
				}
			}
			ClassicAssert.AreEqual(16, checkedTransitions);
		}

		[Test]
		public void AHeartFoundedBeforeThisChangeRaisesItsStoreAtItsNextImprovement()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			Dictionary<string, KingdomMaterialTally> bills = TransitionBills();
			for (int i = 0; i + 1 < Rungs.Length; i++)
			{
				// The pre-change fixture: the standing rung exactly as it is authored today,
				// minus the store it did not have. This is what a save founded before this
				// update decodes to at rungs 1-4.
				ArchitectureLayoutDelta delta = Delta(corpus, Rungs[i][0], Rungs[i + 1][0],
					ArchitectureFacing.North, true);
				List<ArchitecturePlacement> added = delta.Added.Where(value =>
					value.Blueprint == StoreBlueprint).ToList();
				ClassicAssert.AreEqual(1, added.Count,
					Rungs[i][0] + "->" + Rungs[i + 1][0] + " must add the missing store");
				// And the bill that transition charges must carry the store's own material, or
				// TryPlacementClaim refuses the added placement and the upgrade cannot finish.
				KingdomMaterialTally bill = bills[Rungs[i][0]];
				KingdomMaterial material;
				ClassicAssert.IsTrue(KingdomMaterialRules.TryParseMaterial(added[0].Material,
					out material), added[0].Material);
				ClassicAssert.Greater(bill.Get(material), 0, Rungs[i][0] + "->" + Rungs[i + 1][0]
					+ " has no " + added[0].Material + " for the store it must add");
			}
		}

		[Test]
		public void EveryHeartTransitionCarriesTimber()
		{
			Dictionary<string, KingdomMaterialTally> bills = TransitionBills();
			for (int i = 0; i + 1 < Rungs.Length; i++)
			{
				ClassicAssert.IsTrue(bills.ContainsKey(Rungs[i][0]), Rungs[i][0]);
				ClassicAssert.Greater(bills[Rungs[i][0]].Get(KingdomMaterial.Timber), 0,
					Rungs[i][0] + " must fund the store a pre-change heart still owes");
			}
		}

		[Test]
		public void TheCampStockpileIsAStockpileFromThePlacementAtTheDeclaredDefault()
		{
			XDocument blueprints = XDocument.Parse(
				TestMain.ReadRepositoryText("RuntimeData/ObjectBlueprints.xml"));
			XElement store = blueprints.Root.Elements("object").Single(value =>
				(string)value.Attribute("Name") == StoreBlueprint);
			ClassicAssert.AreEqual("Chest", (string)store.Attribute("Inherits"));
			XElement mark = store.Elements("intproperty").Single(value =>
				(string)value.Attribute("Name") == StockpilePropertyName());
			ClassicAssert.AreEqual("1", (string)mark.Attribute("Value"));
			XElement capacity = store.Elements("tag").Single(value =>
				(string)value.Attribute("Name") == KingdomRules.StockpileCapacityTag);
			ClassicAssert.AreEqual(KingdomRules.DefaultStockpileCapacity.ToString(),
				(string)capacity.Attribute("Value"));
			ClassicAssert.AreEqual("false",
				(string)store.Elements("part").Single(value =>
					(string)value.Attribute("Name") == "Physics").Attribute("Takeable"));
			// Construction never stocks anything: the store is raised empty.
			ClassicAssert.IsTrue(store.Elements("tag").Any(value =>
				(string)value.Attribute("Name") == "InventoryPopulationTable"
				&& (string)value.Attribute("Value") == "*delete"));
		}

		[Test]
		public void AnImprovementAlreadyUnderWayFinishesOnItsFrozenPlan()
		{
			// The store arrives one rung later for a settlement caught mid-upgrade, and that is
			// deliberate: Begin freezes the successor payload AND the paid claim together, and
			// the plan-change gate re-proves the standing work against that frozen payload
			// rather than against the live catalogue. Nothing is re-priced, refunded or
			// quarantined. This PR touches no Growth/KingdomUpgrade.* file; these pins say so.
			string begin = TestMain.ReadRepositoryText("Growth/KingdomUpgrade.14.Begin.cs");
			StringAssert.Contains("KingdomMaterials.UpgradeCostFor(", begin);
			StringAssert.Contains("KingdomConstructionRoute.Improvement, cell, Work, "
				+ "A.SuccessorKey, payload,", begin);
			StringAssert.Contains("A.CostDrams, claim, now, now + A.BuildTicks)", begin);
			string planChange = TestMain.ReadRepositoryText("Growth/KingdomUpgrade.16.PlanChange.cs");
			StringAssert.Contains("TryValidateFrozenUpgrade", planChange);
		}

		// --- helpers ---------------------------------------------------------------------

		/// <summary>The dedication mark, read off the production declaration rather than typed
		/// again here, so a rename moves the pin with the code instead of quietly passing.
		/// </summary>
		private static string StockpilePropertyName()
		{
			string source = TestMain.ReadRepositoryText(
				"Growth/KingdomMaterials.01.Declarations.cs");
			System.Text.RegularExpressions.Match match =
				System.Text.RegularExpressions.Regex.Match(source,
					"StockpileProperty\\s*=\\s*\"([^\"]+)\"");
			ClassicAssert.IsTrue(match.Success, "StockpileProperty is no longer declared");
			return match.Groups[1].Value;
		}

		private static Dictionary<string, KingdomMaterialTally> TransitionBills()
		{
			XDocument catalogue = XDocument.Parse(
				TestMain.ReadRepositoryText("RuntimeData/KingdomBuildings.xml"));
			Dictionary<string, KingdomMaterialTally> result =
				new Dictionary<string, KingdomMaterialTally>(StringComparer.Ordinal);
			foreach (XElement building in catalogue.Root.Elements("building"))
			{
				string key = (string)building.Attribute("Key");
				string bill = (string)building.Attribute("UpgradeMaterials");
				if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(bill)) continue;
				ClassicAssert.IsTrue(KingdomMaterialRules.TryParseMaterialCost(bill,
					out KingdomMaterialTally tally, out string error), key + ": " + error);
				result[key] = tally;
			}
			return result;
		}

		private static ArchitectureLayoutDelta Delta(ArchitectureCorpus corpus, string beforeKey,
			string afterKey, ArchitectureFacing facing, bool StripStore)
		{
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(
				KingdomArchitectureCorpusFixture.Request(corpus, Case(corpus, beforeKey), facing),
				out ArchitectureLayoutSnapshot before, out string failure), beforeKey + ": " + failure);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(
				KingdomArchitectureCorpusFixture.Request(corpus, Case(corpus, afterKey), facing),
				out ArchitectureLayoutSnapshot after, out failure), afterKey + ": " + failure);
			if (StripStore)
				ClassicAssert.AreEqual(1, before.Placements.RemoveAll(value =>
					value.Blueprint == StoreBlueprint), beforeKey + " had no store to strip");
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(before, after,
				out ArchitectureLayoutDelta delta, out failure),
				beforeKey + "->" + afterKey + " " + facing + ": " + failure);
			return delta;
		}

		private static ArchitectureCorpusCase Case(ArchitectureCorpus corpus, string buildKey)
		{
			return corpus.Cases.Single(item => item.Tier.BuildKey == buildKey
				&& item.Variant.Key == "fallback");
		}

		private static ArchitectureMapDraft Map(ArchitectureCorpus corpus, string key)
		{
			ClassicAssert.IsTrue(corpus.Maps.ContainsKey(key), key);
			return corpus.Maps[key];
		}

		private static ArchitecturePaletteSlot Slot(ArchitectureCorpus corpus, string palette,
			string slot)
		{
			ClassicAssert.IsTrue(corpus.Palettes.ContainsKey(palette), palette);
			return corpus.Palettes[palette].Slots.Single(value => value.Key == slot);
		}

		private static ArchitectureGlyphDraft At(ArchitectureMapDraft map, int x, int y)
		{
			if (x < 0 || y < 0 || y >= map.Rows.Count || x >= map.Rows[y].Length) return null;
			char character = map.Rows[y][x];
			return map.Glyphs.FirstOrDefault(value => value.Character == character);
		}

		private static ArchitectureCover Cover(ArchitectureMapDraft map, ArchitectureGlyphDraft glyph)
		{
			return glyph.HasCover ? glyph.Cover : map.DefaultCover;
		}

		private static int Count(ArchitectureMapDraft map, Func<ArchitectureGlyphDraft, bool> match)
		{
			int total = 0;
			for (int y = 0; y < map.Rows.Count; y++)
				for (int x = 0; x < map.Rows[y].Length; x++)
				{
					ArchitectureGlyphDraft glyph = At(map, x, y);
					if (glyph != null && match(glyph)) total++;
				}
			return total;
		}

		private static int[] Single(ArchitectureMapDraft map, string anchor)
		{
			List<int[]> found = new List<int[]>();
			for (int y = 0; y < map.Rows.Count; y++)
				for (int x = 0; x < map.Rows[y].Length; x++)
				{
					ArchitectureGlyphDraft glyph = At(map, x, y);
					if (glyph != null && glyph.Anchors.Contains(anchor))
						found.Add(new int[] { x, y });
				}
			ClassicAssert.AreEqual(1, found.Count, map.Key + " " + anchor);
			return found[0];
		}
	}
}
#endif
