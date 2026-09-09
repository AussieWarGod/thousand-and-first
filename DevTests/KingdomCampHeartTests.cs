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
		private const string HearthBlueprint = "r_KingdomCivicCampfireCamp";
		private const string HearthSelfOnlyTag = "CampfireHeatSelfOnly";
		private const string CanvasBlueprint = "r_KingdomStructureCanvasWall";
		/// <summary>The four heart palettes and five heart maps as they stood on dev at
		/// 4f66331, the commit before this change. A save founded before the camp decodes to
		/// these, so every pre-change claim in this suite is proved against them.</summary>
		private const string Baseline = "DevTests/Fixtures/PreCampHeart/heart-4f66331.xml";

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
				// The vanilla Campfire part heats every OTHER object standing on its own cell
				// each turn (D/XRL/World/Parts/Campfire.cs:168, engine 2.0.211.51) unless the
				// blueprint carries CampfireHeatSelfOnly, which the same line reads; :166 still
				// warms the fire itself and :139 stops it claiming heat radiation. The camp fire
				// carries that tag, so a citizen or a dropped bundle sharing the cell is never
				// heated and the canvas ring a cell away can never catch. Pass="adjacent" is an
				// architecture use contract, not an engine exclusion, so it is not the guard.
				ClassicAssert.IsNull(glyph.Structure, Rungs[i][0] + " hearth carries a structure");
				ClassicAssert.AreEqual(ArchitecturePassability.Adjacent, glyph.Passability,
					Rungs[i][0] + " hearth cell must be used from beside it");
				ClassicAssert.IsFalse(glyph.StatefulObject,
					Rungs[i][0] + " the fire is ordinary fabric and is struck at the moot");
				ArchitecturePaletteSlot slot = Slot(corpus, Rungs[i][2], "hearth");
				ClassicAssert.AreEqual(HearthBlueprint, slot.Blueprint, Rungs[i][0]);
				ClassicAssert.AreEqual("timber", slot.Material, Rungs[i][0]);
			}
			// The tag is the whole of the safety answer, so it is pinned on the blueprint the
			// two camp rungs actually name.
			XDocument blueprints = XDocument.Parse(
				TestMain.ReadRepositoryText("RuntimeData/ObjectBlueprints.xml"));
			XElement fire = blueprints.Root.Elements("object").Single(value =>
				(string)value.Attribute("Name") == HearthBlueprint);
			ClassicAssert.AreEqual("r_KingdomCivicCampfire", (string)fire.Attribute("Inherits"),
				"the camp fire must stay the civic wrapper, tag and all");
			ClassicAssert.IsTrue(fire.Elements("tag").Any(value =>
				(string)value.Attribute("Name") == HearthSelfOnlyTag),
				"the camp fire must carry " + HearthSelfOnlyTag);
			// And the ordinary civic campfire must NOT carry it: every other hearth in the
			// catalogue still warms what stands with it.
			ClassicAssert.IsFalse(blueprints.Root.Elements("object").Single(value =>
				(string)value.Attribute("Name") == "r_KingdomCivicCampfire")
				.Elements("tag").Any(value =>
					(string)value.Attribute("Name") == HearthSelfOnlyTag));
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
						facing);
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
			// The BEFORE side is the shipped 4f66331 map compiled whole - old glyphs, old
			// claims, old palette - not the new map with the store deleted out of it. The
			// AFTER side is the successor exactly as this change ships it. That pair is what a
			// save founded before the camp actually presents to the upgrade machinery.
			ArchitectureCorpus old = KingdomArchitectureCorpusFixture.Load();
			KingdomArchitectureCorpusFixture.Overlay(old, Baseline);
			ArchitectureCorpus current = KingdomArchitectureCorpusFixture.Load();
			Dictionary<string, KingdomMaterialTally> bills = TransitionBills();
			for (int i = 0; i + 1 < Rungs.Length; i++)
			{
				foreach (ArchitectureFacing facing in Enum.GetValues(typeof(ArchitectureFacing)))
				{
					ArchitectureLayoutSnapshot before = Compile(old, Rungs[i][0], facing);
					ClassicAssert.AreEqual(0, before.Placements.Count(value =>
						value.Blueprint == StoreBlueprint),
						"the " + Rungs[i][0] + " baseline must have no store at all");
					ArchitectureLayoutSnapshot after = Compile(current, Rungs[i + 1][0], facing);
					ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(before, after,
						out ArchitectureLayoutDelta delta, out string failure),
						Rungs[i][0] + "->" + Rungs[i + 1][0] + " " + facing + ": " + failure);
					ClassicAssert.AreEqual(1, delta.Added.Count(value =>
						value.Blueprint == StoreBlueprint),
						Rungs[i][0] + "->" + Rungs[i + 1][0] + " must add the missing store");
					// And the bill that transition charges must cover EVERY added placement,
					// not only the store: TryPlacementClaim refuses any added, non-natural,
					// non-existing-authority piece whose material is absent from the paid claim,
					// and one refusal stops the whole improvement.
					KingdomMaterialTally bill = bills[Rungs[i][0]];
					for (int p = 0; p < delta.Added.Count; p++)
					{
						ArchitecturePlacement added = delta.Added[p];
						if (added.Natural || added.ExistingAuthority) continue;
						ClassicAssert.IsTrue(KingdomMaterialRules.TryParseMaterial(added.Material,
							out KingdomMaterial material), added.Material);
						ClassicAssert.Greater(bill.Get(material), 0, Rungs[i][0] + "->"
							+ Rungs[i + 1][0] + " cannot pay for added " + added.Slot
							+ " (" + added.Material + ")");
					}
					// Nothing protected is struck to make room for it, so nothing a founder
					// filled is emptied, quarantined or handed back.
					ClassicAssert.AreEqual(0, delta.Removed.Count(value =>
						!string.IsNullOrEmpty(value.StatefulAnchor) || value.ExistingAuthority),
						Rungs[i][0] + "->" + Rungs[i + 1][0] + " strikes protected state");
				}
			}
		}

		[Test]
		public void AnImprovementBegunBeforeThisChangeFinishesStorelessAndTheStoreArrivesNextRung()
		{
			// The frozen payload a mid-flight job carries is the OLD successor. Compile that
			// pair and the store is nowhere in it: the job finishes exactly as it was priced.
			// The rung after it is where the store lands, which is the honest sentence the
			// CHANGELOG and Save-compat notes make.
			ArchitectureCorpus old = KingdomArchitectureCorpusFixture.Load();
			KingdomArchitectureCorpusFixture.Overlay(old, Baseline);
			ArchitectureCorpus current = KingdomArchitectureCorpusFixture.Load();
			for (int i = 0; i + 1 < Rungs.Length; i++)
			{
				ArchitectureLayoutSnapshot frozenBefore = Compile(old, Rungs[i][0],
					ArchitectureFacing.North);
				ArchitectureLayoutSnapshot frozenAfter = Compile(old, Rungs[i + 1][0],
					ArchitectureFacing.North);
				ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(frozenBefore,
					frozenAfter, out ArchitectureLayoutDelta inFlight, out string failure),
					Rungs[i][0] + "->" + Rungs[i + 1][0] + ": " + failure);
				ClassicAssert.AreEqual(0, inFlight.Added.Count(value =>
					value.Blueprint == StoreBlueprint),
					"a job already begun must finish storeless on its frozen plan");
				ClassicAssert.AreEqual(0, inFlight.Removed.Count(value =>
					!string.IsNullOrEmpty(value.StatefulAnchor) || value.ExistingAuthority),
					"a frozen job strikes no protected state either");
				if (i + 2 >= Rungs.Length) continue;
				// One rung later, from the storeless heart that job left standing.
				ArchitectureLayoutSnapshot nextAfter = Compile(current, Rungs[i + 2][0],
					ArchitectureFacing.North);
				ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(frozenAfter, nextAfter,
					out ArchitectureLayoutDelta nextDelta, out failure),
					Rungs[i + 1][0] + "->" + Rungs[i + 2][0] + ": " + failure);
				ClassicAssert.AreEqual(1, nextDelta.Added.Count(value =>
					value.Blueprint == StoreBlueprint),
					"the store must arrive at the rung after the one the job finished");
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
			// 48 by ruling, stated as a number rather than borrowed from the default: the camp
			// store is the first rung of the shipped ladder (48/64/96/192/384) and must never be
			// SMALLER than a chest a founder walked up to and dedicated by hand. The plan asked
			// for 32; that would have made the settlement's only store the meanest container in
			// the game. The equality below is a coincidence worth knowing about, not the source
			// of the number, so both are asserted.
			ClassicAssert.AreEqual("48", (string)capacity.Attribute("Value"));
			ClassicAssert.AreEqual(48, KingdomRules.ShelfCapacity);
			ClassicAssert.AreEqual(48, KingdomRules.DefaultStockpileCapacity,
				"if the default ever moves, the camp store keeps its own declared 48");
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

		private static ArchitectureLayoutSnapshot Compile(ArchitectureCorpus corpus, string key,
			ArchitectureFacing facing)
		{
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(
				KingdomArchitectureCorpusFixture.Request(corpus, Case(corpus, key), facing),
				out ArchitectureLayoutSnapshot snapshot, out string failure),
				key + " " + facing + ": " + failure);
			return snapshot;
		}

		private static ArchitectureLayoutDelta Delta(ArchitectureCorpus corpus, string beforeKey,
			string afterKey, ArchitectureFacing facing)
		{
			ArchitectureLayoutSnapshot before = Compile(corpus, beforeKey, facing);
			ArchitectureLayoutSnapshot after = Compile(corpus, afterKey, facing);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryBuildDelta(before, after,
				out ArchitectureLayoutDelta delta, out string failure),
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
