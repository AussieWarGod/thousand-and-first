using System.Collections.Generic;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public partial class KingdomArchitectureGalleryWishes
	{
		private enum VisualCaseKind : byte
		{
			Objects = 0,
			Gatehouse = 1,
			RoadWorn = 2,
			RoadTrodden = 3,
			RoadPath = 4,
			RoadPaved = 5
		}

		private sealed class VisualPlacement
		{
			public string Role;
			public string Blueprint;
			public int X;
			public int Y;
			public string Declaration;
		}

		private sealed class VisualCase
		{
			public int Number;
			public string Key;
			public string CatalogueKey;
			public string YardKey;
			public VisualCaseKind Kind;
			public int Width;
			public int Height;
			public List<VisualPlacement> Placements = new List<VisualPlacement>();

			public int ExpectedObjects
			{
				get
				{
					if (Kind == VisualCaseKind.Gatehouse) return 7;
					if (Kind == VisualCaseKind.RoadWorn) return 0;
					if (Kind == VisualCaseKind.RoadTrodden || Kind == VisualCaseKind.RoadPath
						|| Kind == VisualCaseKind.RoadPaved) return Width;
					return Placements.Count;
				}
			}
		}

		private static List<VisualCase> VisualCases()
		{
			List<VisualCase> result = new List<VisualCase>();
			AddWallTopologyCase(result, "palisade", "r_KingdomPalisade",
				"r_KingdomFixtureGateBrinestalk");
			AddWallTopologyCase(result, "rampart", "r_KingdomRampart",
				"r_KingdomFixtureGateBrinestalk");
			AddObjectCase(result, "watchtower", "r_KingdomWatchtower");
			result.Add(new VisualCase { Key = "gatehouse", CatalogueKey = "gatehouse",
				Kind = VisualCaseKind.Gatehouse, Width = 3, Height = 3 });
			AddLineCase(result, "watermain", "r_KingdomWaterMain");
			AddLineCase(result, "brinemain", "r_KingdomBrineMain");
			AddLiquidCrossingCase(result);
			AddTapCase(result, "watertap", "r_KingdomWaterTap", "r_KingdomWaterMain");
			AddTapCase(result, "brinetap", "r_KingdomBrineTap", "r_KingdomBrineMain");
			AddWallTopologyCase(result, "rubblewall", "r_KingdomRubbleWall",
				"r_KingdomFixtureGateBrinestalk");
			AddYardWorkCase(result, "vinelattice", "r_KingdomVineLattice");
			AddYardWorkCase(result, "hiderack", "r_KingdomHideRack");
			AddYardWorkCase(result, "dyevat", "r_KingdomDyeVat");
			AddYardWorkCase(result, "vellumpress", "r_KingdomVellumPress");
			result.Add(Road("road-worn", VisualCaseKind.RoadWorn, 3));
			result.Add(Road("road-trodden", VisualCaseKind.RoadTrodden, 5));
			result.Add(Road("road-path", VisualCaseKind.RoadPath, 5));
			result.Add(Road("road-paved", VisualCaseKind.RoadPaved, 5));
			// Append comparison boards so the original eighteen evidence ordinals stay stable.
			AddCompositeCase(result, "semantic-aliases", 5, new string[]
			{
				"founder-statue", "r_KingdomFounderStatue",
				"watchtower", "r_KingdomWatchtower",
				"hide-rack", "r_KingdomHideRack",
				"spectrum-lamp", "r_KingdomArcologySpectrumLamp",
				"mud", "r_KingdomMud",
				"relic-case", "r_KingdomFixtureRelicCaseScrap",
				"machine-relic", "r_KingdomFixtureMachineRelic",
				"settler-cairn", "r_KingdomCairn",
				"grave-grove", "r_KingdomGraveGrove",
				"niche-tomb", "r_KingdomNicheTomb",
				"cragmensch-stone-garden", "r_KingdomCragmenschStoneGarden"
			});
			AddCompositeCase(result, "creed-affordances", 6, new string[]
			{
				"sealed-hamper", "r_KingdomCreedPracticeBasket",
				"bare-board", "r_KingdomCreedPracticeTable",
				"cold-hearth", "r_KingdomCreedPracticeColdHearth",
				"empty-shelf", "r_KingdomCreedPracticeShelf",
				"marked-slab", "r_KingdomCreedPracticeStone",
				"dry-basin", "r_KingdomCreedPracticeDryBasin",
				"witness-rail", "r_KingdomCreedPracticeBench",
				"rolled-pallet", "r_KingdomCreedPracticePallet",
				"spindle-wheel", "r_KingdomCreedSpindleWheel",
				"dry-contact", "r_KingdomCreedDryContact",
				"practice-arms-rest", "r_KingdomCreedPracticeArmsRack",
				"empty-arms-rack", "r_KingdomCreedWeaponRack",
				"solid-living-trunk", "r_KingdomCreedLivingTrunk",
				"Joppa-seed-bin", "r_KingdomCreedJoppaSeedBin",
				"Kyakukya-spice-jar", "r_KingdomCreedKyakukyaSpiceJar",
				"snapjaw-meat-cache", "r_KingdomCreedSnapjawMeatCache",
				"farmers-labelled-bin", "r_KingdomCreedFarmersLabelledBin",
				"goatfolk-pennon", "r_KingdomCreedGoatfolkChallengePennon",
				"gyre-ossuary-screen", "r_KingdomStructureGyreOssuaryScreen",
				"Chavvah-bough-wall", "r_KingdomStructureChavvahTrunk"
			});
			AddCompositeCase(result, "creed-root-markers", 4, new string[]
			{
				"snapjaw-den", "r_KingdomSnapjawTrailDen",
				"issachari-porch", "r_KingdomIssachariRiflePorch",
				"templar-arsenal", "r_KingdomTemplarPurityArsenal",
				"wardens-lodge", "r_KingdomWardensWatchLodge"
			});
			AddCompositeCase(result, "arcology-props", 5, new string[]
			{
				"ceramic-bed", "r_KingdomArcologyCeramicBed",
				"spectrum-lamp", "r_KingdomArcologySpectrumLamp",
				"seed-case", "r_KingdomArcologySeedCase",
				"condenser-shell", "r_KingdomArcologyCondenserShell",
				"grafting-stand", "r_KingdomArcologyGraftingStand",
				"drying-rack", "r_KingdomArcologyDryingRack",
				"cold-range", "r_KingdomArcologyColdRange",
				"infirmary-couch", "r_KingdomArcologyInfirmaryCouch",
				"dry-basin", "r_KingdomArcologyDryBasin",
				"dormant-bunk", "r_KingdomArcologyDormantBunk",
				"watch-post", "r_KingdomArcologyWatchPost",
				"service-cabinet", "r_KingdomArcologyServiceCabinet",
				"freight-pallet", "r_KingdomArcologyFreightPallet",
				"scrub-bank", "r_KingdomArcologyScrubBank",
				"repair-stand", "r_KingdomArcologyRepairStand"
			});
			for (int i = 0; i < result.Count; i++) result[i].Number = i + 1;
			return result;
		}

		private static void AddCompositeCase(List<VisualCase> Into, string Key, int Width,
			string[] RolesAndBlueprints)
		{
			int count = RolesAndBlueprints.Length / 2;
			VisualCase item = new VisualCase { Key = Key, CatalogueKey = null,
				Kind = VisualCaseKind.Objects, Width = Width,
				Height = (count + Width - 1) / Width };
			for (int i = 0; i < count; i++)
				item.Placements.Add(At(RolesAndBlueprints[i * 2], RolesAndBlueprints[i * 2 + 1],
					i % Width, i / Width));
			Into.Add(item);
		}

		private static void AddObjectCase(List<VisualCase> Into, string Key, string Blueprint)
		{
			VisualCase item = new VisualCase { Key = Key, CatalogueKey = Key,
				Kind = VisualCaseKind.Objects, Width = 1, Height = 1 };
			item.Placements.Add(At("root", Blueprint, 0, 0));
			Into.Add(item);
		}

		private static void AddYardWorkCase(List<VisualCase> Into, string Key, string Blueprint)
		{
			VisualCase item = new VisualCase { Key = Key, YardKey = Key,
				Kind = VisualCaseKind.Objects, Width = 1, Height = 1 };
			item.Placements.Add(At("root", Blueprint, 0, 0));
			Into.Add(item);
		}

		private static void AddWallTopologyCase(List<VisualCase> Into, string Key,
			string Wall, string Gate)
		{
			// Each group has a full eight-neighbour gap. PaintedWall may read diagonals,
			// so one review silhouette must never change another group's bitmask.
			VisualCase item = new VisualCase { Key = Key, CatalogueKey = Key,
				Kind = VisualCaseKind.Objects, Width = 13, Height = 9 };
			item.Placements.Add(At("single", Wall, 0, 0));
			item.Placements.Add(At("horizontal-west", Wall, 3, 0));
			item.Placements.Add(At("horizontal-centre", Wall, 4, 0));
			item.Placements.Add(At("horizontal-east", Wall, 5, 0));
			item.Placements.Add(At("vertical-north", Wall, 8, 0));
			item.Placements.Add(At("vertical-centre", Wall, 8, 1));
			item.Placements.Add(At("vertical-south", Wall, 8, 2));
			item.Placements.Add(At("corner-turn", Wall, 0, 4));
			item.Placements.Add(At("corner-east", Wall, 1, 4));
			item.Placements.Add(At("corner-south", Wall, 0, 5));
			item.Placements.Add(At("tee-north", Wall, 4, 3));
			item.Placements.Add(At("tee-west", Wall, 3, 4));
			item.Placements.Add(At("tee-centre", Wall, 4, 4));
			item.Placements.Add(At("tee-east", Wall, 5, 4));
			item.Placements.Add(At("cross-north", Wall, 10, 3));
			item.Placements.Add(At("cross-west", Wall, 9, 4));
			item.Placements.Add(At("cross-centre", Wall, 10, 4));
			item.Placements.Add(At("cross-east", Wall, 11, 4));
			item.Placements.Add(At("cross-south", Wall, 10, 5));
			item.Placements.Add(At("gate-far-west", Wall, 8, 8));
			item.Placements.Add(At("gate-adjacent-west", Wall, 9, 8));
			item.Placements.Add(At("gate", Gate, 10, 8));
			item.Placements.Add(At("gate-adjacent-east", Wall, 11, 8));
			item.Placements.Add(At("gate-far-east", Wall, 12, 8));
			Into.Add(item);
		}

		private static void AddLineCase(List<VisualCase> Into, string Key, string Blueprint)
		{
			VisualCase item = new VisualCase { Key = Key, CatalogueKey = Key,
				Kind = VisualCaseKind.Objects, Width = 7, Height = 7 };
			for (int mask = 0; mask < 16; mask++)
			{
				string joins;
				KingdomLiquidVisualRules.TryCanonicalJoins(mask, out joins);
				item.Placements.Add(At("mask-" + mask.ToString("D2"), Blueprint,
					(mask % 4) * 2, (mask / 4) * 2, joins));
			}
			Into.Add(item);
		}

		private static void AddTapCase(List<VisualCase> Into, string Key, string Tap, string Main)
		{
			VisualCase item = new VisualCase { Key = Key, CatalogueKey = Key,
				Kind = VisualCaseKind.Objects, Width = 7, Height = 7 };
			for (int mask = 0; mask < 16; mask++)
			{
				string joins;
				KingdomLiquidVisualRules.TryCanonicalJoins(mask, out joins);
				item.Placements.Add(At("mask-" + mask.ToString("D2"), Tap,
					(mask % 4) * 2, (mask / 4) * 2, joins));
			}
			Into.Add(item);
		}

		private static void AddLiquidCrossingCase(List<VisualCase> Into)
		{
			VisualCase item = new VisualCase { Key = "liquidcrossing",
				CatalogueKey = "liquidcrossing", Kind = VisualCaseKind.Objects,
				Width = 9, Height = 3 };
			// Full eight-neighbour gap between the two crosses. Each surrounding end declares
			// back toward its crossing, so the screenshot proves both visual orientation and law.
			item.Placements.Add(At("fresh-vertical-crossing", "r_KingdomLiquidCrossing",
				1, 1, "NSEW"));
			item.Placements.Add(At("fresh-vertical-water-n", "r_KingdomWaterMain", 1, 0, "S"));
			item.Placements.Add(At("fresh-vertical-water-s", "r_KingdomWaterMain", 1, 2, "N"));
			item.Placements.Add(At("fresh-vertical-brine-w", "r_KingdomBrineMain", 0, 1, "E"));
			item.Placements.Add(At("fresh-vertical-brine-e", "r_KingdomBrineMain", 2, 1, "W"));
			item.Placements.Add(At("fresh-horizontal-crossing", "r_KingdomLiquidCrossing",
				7, 1, "EWNS"));
			item.Placements.Add(At("fresh-horizontal-brine-n", "r_KingdomBrineMain", 7, 0, "S"));
			item.Placements.Add(At("fresh-horizontal-brine-s", "r_KingdomBrineMain", 7, 2, "N"));
			item.Placements.Add(At("fresh-horizontal-water-w", "r_KingdomWaterMain", 6, 1, "E"));
			item.Placements.Add(At("fresh-horizontal-water-e", "r_KingdomWaterMain", 8, 1, "W"));
			Into.Add(item);
		}

		private static VisualPlacement At(string Role, string Blueprint, int X, int Y)
		{
			return new VisualPlacement { Role = Role, Blueprint = Blueprint, X = X, Y = Y };
		}

		private static VisualPlacement At(string Role, string Blueprint, int X, int Y,
			string Declaration)
		{
			return new VisualPlacement { Role = Role, Blueprint = Blueprint, X = X, Y = Y,
				Declaration = Declaration };
		}

		private static VisualCase Road(string Key, VisualCaseKind Kind, int Width)
		{
			return new VisualCase { Key = Key, CatalogueKey = null, Kind = Kind,
				Width = Width, Height = 1 };
		}
	}
}
