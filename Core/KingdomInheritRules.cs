using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritRules
	{
		private sealed class Definition
		{
			internal readonly string Key;
			internal readonly string Blueprint;
			internal readonly int Width;
			internal readonly int Height;

			internal Definition(string Key, string Blueprint, int Width, int Height)
			{
				this.Key = Key;
				this.Blueprint = Blueprint;
				this.Width = Width;
				this.Height = Height;
			}
		}

		private sealed class Candidate
		{
			internal string Key;
			internal int X;
			internal int Y;
			internal int Condition;
			internal KingdomInheritWorkState State;
			internal string ArchitectureSnapshot;
			internal string ArchitectureHash;
		}

		private struct Rect
		{
			internal int X1;
			internal int Y1;
			internal int X2;
			internal int Y2;
		}

		internal const int MaxWorks = 40;

		internal const int TargetWidth = 80;

		internal const int TargetHeight = 25;

		internal const int SafeMargin = 2;

		// Source construction owns the whole margin-two interior (76x21). The entry is
		// outside that interior and the cairn/inside pair is selected from unoccupied
		// cells, so inheritance must not silently shrink the lawful source envelope.
		internal const int WorkMargin = SafeMargin;

		internal const int MaxSourceCoordinateMagnitude = 1000000;

		internal const int MaxRelativeSpan = 255;

		internal const int HeldConditionCeiling = 80;

		internal const int FadedStandingConditionCeiling = 65;

		internal const int FadedDerelictConditionCeiling = 45;

		internal const int FadedDerelictPercent = 25;

		internal const int AbandonedDerelictConditionCeiling = 35;

		internal const int RuinsDerelictConditionCeiling = 20;

		internal const string RubbleKey = "inherit.rubble";

		internal const string MemoryKey = "inherit.memory";

		internal const string FounderCairnKey = "inherit.cairn";

		internal static readonly KingdomInheritEngineCheck RemainingEngineChecks =
			KingdomInheritEngineCheck.ConnectionCell
			| KingdomInheritEngineCheck.Terrain
			| KingdomInheritEngineCheck.ExistingObjects
			| KingdomInheritEngineCheck.Stairs
			| KingdomInheritEngineCheck.EntryToHeartPath;

		private static readonly Definition[] Definitions = new Definition[]
		{
			D("inherit.rubble", "r_KingdomRubbleWall", 1, 1),
			D("inherit.memory", "r_KingdomCairn", 1, 1),
			D("inherit.cairn", "r_KingdomCairn", 1, 1),
			D("tent", "r_KingdomTent", 3, 2),
			D("tentrow", "r_KingdomTentRow", 5, 2),
			D("hut", "r_KingdomHut", 4, 3),
			D("hutyard", "r_KingdomHutYard", 5, 4),
			D("house", "r_KingdomHouse", 8, 6),
			D("housecourt", "r_KingdomHouseCourt", 8, 6),
			D("terrace", "r_KingdomTerrace", 12, 9),
			D("finehouse", "r_KingdomFineHouse", 8, 6),
			D("manor", "r_KingdomManor", 12, 9),
			D("court", "r_KingdomCourt", 20, 14),
			D("saltpan", "r_KingdomSaltPan", 5, 4),
			D("saltterrace", "r_KingdomSaltTerrace", 5, 4),
			D("catchment", "r_KingdomCatchment", 5, 4),
			D("catchmentbank", "r_KingdomCatchmentBank", 5, 4),
			D("airwellcourt", "r_KingdomAirWellCourt", 8, 6),
			D("airwellfield", "r_KingdomAirWellField", 12, 9),
			D("weeptap", "r_KingdomWeepTap", 5, 4),
			D("weepgallery", "r_KingdomWeepGallery", 8, 6),
			D("cistern", "r_KingdomGreatCistern", 8, 6),
			D("cisternvault", "r_KingdomCisternVault", 8, 6),
			D("reservoir", "r_KingdomReservoir", 12, 9),
			D("waterworks", "r_KingdomWaterworks", 20, 14),
			D("condensery", "r_KingdomCondensery", 20, 14),
			D("larder", "r_KingdomLarder", 5, 4),
			D("plot", "r_KingdomPlot", 5, 4),
			D("plotrows", "r_KingdomPlotRows", 5, 4),
			D("field", "r_KingdomField", 8, 6),
			D("fieldrows", "r_KingdomFieldRows", 8, 6),
			D("granary", "r_KingdomGranary", 8, 6),
			D("grange", "r_KingdomGrange", 12, 9),
			D("homefarm", "r_KingdomHomeFarm", 20, 14),
			D("toolshed", "r_KingdomToolShed", 5, 4),
			D("chargingpost", "r_KingdomChargingPost", 5, 4),
			D("smithy", "r_KingdomSmithy", 8, 6),
			D("forge", "r_KingdomForge", 8, 6),
			D("grindmill", "r_KingdomGrindMill", 8, 6),
			D("workshop", "r_KingdomWorkshop", 8, 6),
			D("sawyeryard", "r_KingdomSawyerYard", 8, 6),
			D("masonyard", "r_KingdomMasonYard", 8, 6),
			D("smelter", "r_KingdomSmelter", 8, 6),
			D("oven", "r_KingdomOven", 5, 4),
			D("bench", "r_KingdomBench", 5, 4),
			D("hall", "r_KingdomHall", 8, 6),
			D("bazaar", "r_KingdomBazaar", 8, 6),
			D("bathhouse", "r_KingdomBathhouse", 12, 9),
			D("heartbasin", "r_KingdomRiteGround", 3, 3),
			D("heartwaterstone", "r_KingdomWaterstone", 6, 4),
			D("heartmoot", "r_KingdomMootYard", 8, 6),
			D("heartcourt", "r_KingdomGreatCourt", 16, 11),
			D("shrine", "r_KingdomShrine", 5, 4),
			D("shrinegarth", "r_KingdomShrineGarth", 5, 4),
			D("temple", "r_KingdomTemple", 12, 9),
			D("scriptorium", "r_KingdomScriptorium", 8, 6),
			D("palisade", "r_KingdomPalisade", 1, 1),
			D("rampart", "r_KingdomRampart", 1, 1),
			D("watchtower", "r_KingdomWatchtower", 1, 1),
			D("gatehouse", "r_KingdomGatehouse", 1, 1),
			D("barracks", "r_KingdomBarracks", 12, 9),
			D("cairn", "r_KingdomCairn", 5, 4),
			D("mill", "r_KingdomMill", 5, 4),
			D("waterwheel", "r_KingdomWaterWheel", 5, 4),
			D("sailvane", "r_KingdomSailvane", 5, 4),
			D("saltstore", "r_KingdomSaltStore", 8, 6),
			D("watermain", "r_KingdomWaterMain", 1, 1),
			D("brinemain", "r_KingdomBrineMain", 1, 1),
			D("liquidcrossing", "r_KingdomLiquidCrossing", 1, 1),
			D("watertap", "r_KingdomWaterTap", 1, 1),
			D("brinetap", "r_KingdomBrineTap", 1, 1),
			D("ydroofline", "r_KingdomHutYard", 5, 4),
			D("hindrenweavehall", "r_KingdomSmithy", 8, 6),
			D("mudhut", "r_KingdomMudHut", 4, 3),
			D("mudhutcourt", "r_KingdomMudHutCourt", 5, 4),
			D("caravanserai", "r_KingdomCaravanserai", 12, 9),
			D("stiltrow", "r_KingdomStiltRow", 8, 6),
			D("gravegrove", "r_KingdomGraveGrove", 5, 4),
			D("sporecellar", "r_KingdomSporeCellar", 8, 6),
			D("caproof", "r_KingdomCapRoof", 4, 3),
			D("bonefold", "r_KingdomBoneFold", 5, 4),
			D("sacramentcourt", "r_KingdomSacramentCourt", 12, 9),
			D("blockhut", "r_KingdomBlockHut", 4, 3),
			D("blockyard", "r_KingdomBlockYard", 5, 4),
			D("rubblewall", "r_KingdomRubbleWall", 1, 1),
			D("carvedcell", "r_KingdomCarvedCell", 4, 3),
			D("carvedgallery", "r_KingdomCarvedGallery", 8, 6),
			D("fungalvault", "r_KingdomFungalVault", 8, 6),
			D("vaultgalleries", "r_KingdomVaultGalleries", 12, 9),
			D("deepcut", "r_KingdomDeepCut", 8, 6),
			D("nichetomb", "r_KingdomNicheTomb", 5, 4),
			D("delve", "r_KingdomDelve", 8, 6),
			D("underbench", "r_KingdomUnderBench", 8, 6),
			D("reliquary", "r_KingdomReliquary", 12, 9),
			D("factorhouse", "r_KingdomFactorHouse", 8, 6),
			D("butcherslab", "r_KingdomButcherSlab", 5, 4),
			D("vathouse", "r_KingdomVatHouse", 8, 6),
			D("graftinghall", "r_KingdomGraftingHall", 12, 9),
			D("chimerictheatre", "r_KingdomChimericTheatre", 20, 14),
			D("becomingannexe", "r_KingdomBecomingAnnexe", 20, 14),
			D("mirrorgate", "r_KingdomMirrorGate", 11, 8),
			D("crownhall", "r_KingdomCrownHall", 14, 10),
			D("arcology", "r_KingdomArcology", 20, 14),
			D("arcologyward", "r_KingdomArcologyWard", 12, 9),
			D("arcologyterrace", "r_KingdomArcologyTerrace", 8, 6),
			D("hallsurgery", "r_KingdomHallSurgery", 8, 6),
			D("registryoffice", "r_KingdomRegistryOffice", 8, 6)
		};

		private static Definition D(string Key, string Blueprint, int Width, int Height)
		{
			return new Definition(Key, Blueprint, Width, Height);
		}

		internal static bool IsStableSemanticKey(string Key)
		{
			if (string.IsNullOrEmpty(Key) || Key.Length > 64)
			{
				return false;
			}
			for (int i = 0; i < Key.Length; i++)
			{
				char c = Key[i];
				if ((c < 'a' || c > 'z') && (c < '0' || c > '9') && c != '.' && c != '-' && c != '_')
				{
					return false;
				}
			}
			return true;
		}

		internal static bool IsInheritableKey(string Key)
		{
			return Find(Key) != null;
		}

		internal static bool IsFoundingHeartKey(string Key)
		{
			return Key == "heartbasin" || Key == "heartwaterstone"
				|| Key == "heartmoot" || Key == "heartcourt";
		}

		internal static bool TryResolveBlueprint(string Key, out string Blueprint)
		{
			Blueprint = null;
			Definition definition = Find(Key);
			if (definition == null || !IsTafBlueprint(definition.Blueprint))
			{
				return false;
			}
			Blueprint = definition.Blueprint;
			return true;
		}

		/// <summary>
		/// Converts the blueprint carried by a live city work row into the stable key
		/// written to a Seal. Definitions are ordered canonical-first: inheritance-only
		/// marker rows are skipped, then the base catalogue key wins over later cultural
		/// aliases that intentionally share a blueprint.
		/// </summary>
		internal static bool TrySemanticKeyForBlueprint(string Blueprint, out string Key)
		{
			Key = null;
			if (!IsTafBlueprint(Blueprint) || Blueprint.Length > 96)
			{
				return false;
			}
			for (int i = 0; i < Definitions.Length; i++)
			{
				Definition definition = Definitions[i];
				if (definition.Key.StartsWith("inherit.", StringComparison.Ordinal)
					|| !string.Equals(definition.Blueprint, Blueprint, StringComparison.Ordinal))
				{
					continue;
				}
				Key = definition.Key;
				return true;
			}
			return false;
		}

		internal static bool TryFootprint(string Key, out int Width, out int Height)
		{
			Width = 0;
			Height = 0;
			Definition definition = Find(Key);
			if (definition == null)
			{
				return false;
			}
			Width = definition.Width;
			Height = definition.Height;
			return Width > 0 && Height > 0;
		}

	}
}
