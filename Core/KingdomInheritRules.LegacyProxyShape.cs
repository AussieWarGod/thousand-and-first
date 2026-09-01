using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritRules
	{
		/// <summary>
		/// Spatial-v0 seals carry semantic anchors but no authored footprint. This version binds
		/// those external records to one immutable proxy-shape manifest. Any catalogue drift without
		/// a new reconstruction version refuses before placement instead of changing old ground.
		/// </summary>
		internal const int LegacyProxyShapeVersion = 1;

		internal static bool TryValidateLegacyProxyShape(IList<string> Keys,
			int ReconstructionVersion, out string Failure)
		{
			Failure = "";
			if (Keys == null || Keys.Count > MaxWorks
				|| ReconstructionVersion != LegacyProxyShapeVersion)
			{
				Failure = "the legacy proxy shape version is unsupported";
				return false;
			}
			for (int i = 0; i < Keys.Count; i++)
			{
				string key = Keys[i];
				if (!IsStableSemanticKey(key))
				{
					Failure = "the legacy proxy carries a malformed semantic key";
					return false;
				}
				int expectedWidth;
				int expectedHeight;
				int width;
				int height;
				if (!TryLegacyProxyFootprint(key, out expectedWidth, out expectedHeight))
				{
					// Tokens absent from the v1 catalogue have always become one-cell memories.
					// A new live definition needs a new explicit reconstruction contract.
					if (!IsInheritableKey(key)) continue;
					Failure = "legacy proxy shape is unversioned for " + key;
					return false;
				}
				if (key == RubbleKey || key == MemoryKey || key == FounderCairnKey) continue;
				// A removed v1 definition is drift too; it may not silently become a memory.
				if (!IsInheritableKey(key) || !TryFootprint(key, out width, out height)
					|| width != expectedWidth || height != expectedHeight)
				{
					Failure = "legacy proxy shape changed for " + key;
					return false;
				}
			}
			return true;
		}

		internal static bool LegacyProxyShapeMatches(string Key, int Width, int Height,
			int ReconstructionVersion)
		{
			int expectedWidth;
			int expectedHeight;
			return ReconstructionVersion == LegacyProxyShapeVersion
				&& TryLegacyProxyFootprint(Key, out expectedWidth, out expectedHeight)
				&& Width == expectedWidth && Height == expectedHeight;
		}

		private static bool TryLegacyProxyFootprint(string Key, out int Width, out int Height)
		{
			Width = 0;
			Height = 0;
			switch (Key)
			{
			case "inherit.rubble": Width = 1; Height = 1; return true;
			case "inherit.memory": Width = 1; Height = 1; return true;
			case "inherit.cairn": Width = 1; Height = 1; return true;
			case "tent": Width = 3; Height = 2; return true;
			case "tentrow": Width = 5; Height = 2; return true;
			case "hut": Width = 4; Height = 3; return true;
			case "hutyard": Width = 5; Height = 4; return true;
			case "house": Width = 8; Height = 6; return true;
			case "housecourt": Width = 8; Height = 6; return true;
			case "terrace": Width = 12; Height = 9; return true;
			case "finehouse": Width = 8; Height = 6; return true;
			case "manor": Width = 12; Height = 9; return true;
			case "court": Width = 20; Height = 14; return true;
			case "saltpan": Width = 5; Height = 4; return true;
			case "saltterrace": Width = 5; Height = 4; return true;
			case "catchment": Width = 5; Height = 4; return true;
			case "catchmentbank": Width = 5; Height = 4; return true;
			case "airwellcourt": Width = 8; Height = 6; return true;
			case "airwellfield": Width = 12; Height = 9; return true;
			case "weeptap": Width = 5; Height = 4; return true;
			case "weepgallery": Width = 8; Height = 6; return true;
			case "cistern": Width = 8; Height = 6; return true;
			case "cisternvault": Width = 8; Height = 6; return true;
			case "reservoir": Width = 12; Height = 9; return true;
			case "waterworks": Width = 20; Height = 14; return true;
			case "condensery": Width = 20; Height = 14; return true;
			case "larder": Width = 5; Height = 4; return true;
			case "plot": Width = 5; Height = 4; return true;
			case "plotrows": Width = 5; Height = 4; return true;
			case "field": Width = 8; Height = 6; return true;
			case "fieldrows": Width = 8; Height = 6; return true;
			case "granary": Width = 8; Height = 6; return true;
			case "grange": Width = 12; Height = 9; return true;
			case "homefarm": Width = 20; Height = 14; return true;
			case "toolshed": Width = 5; Height = 4; return true;
			case "chargingpost": Width = 5; Height = 4; return true;
			case "smithy": Width = 8; Height = 6; return true;
			case "forge": Width = 8; Height = 6; return true;
			case "grindmill": Width = 8; Height = 6; return true;
			case "workshop": Width = 8; Height = 6; return true;
			case "sawyeryard": Width = 8; Height = 6; return true;
			case "masonyard": Width = 8; Height = 6; return true;
			case "smelter": Width = 8; Height = 6; return true;
			case "oven": Width = 5; Height = 4; return true;
			case "bench": Width = 5; Height = 4; return true;
			case "hall": Width = 8; Height = 6; return true;
			case "bazaar": Width = 8; Height = 6; return true;
			case "bathhouse": Width = 12; Height = 9; return true;
			case "heartbasin": Width = 3; Height = 3; return true;
			case "heartwaterstone": Width = 6; Height = 4; return true;
			case "heartmoot": Width = 8; Height = 6; return true;
			case "heartcourt": Width = 16; Height = 11; return true;
			case "shrine": Width = 5; Height = 4; return true;
			case "shrinegarth": Width = 5; Height = 4; return true;
			case "temple": Width = 12; Height = 9; return true;
			case "scriptorium": Width = 8; Height = 6; return true;
			case "palisade": Width = 1; Height = 1; return true;
			case "rampart": Width = 1; Height = 1; return true;
			case "watchtower": Width = 1; Height = 1; return true;
			case "gatehouse": Width = 1; Height = 1; return true;
			case "barracks": Width = 12; Height = 9; return true;
			case "cairn": Width = 5; Height = 4; return true;
			case "mill": Width = 5; Height = 4; return true;
			case "waterwheel": Width = 5; Height = 4; return true;
			case "sailvane": Width = 5; Height = 4; return true;
			case "saltstore": Width = 8; Height = 6; return true;
			case "watermain": Width = 1; Height = 1; return true;
			case "brinemain": Width = 1; Height = 1; return true;
			case "liquidcrossing": Width = 1; Height = 1; return true;
			case "watertap": Width = 1; Height = 1; return true;
			case "brinetap": Width = 1; Height = 1; return true;
			case "ydroofline": Width = 5; Height = 4; return true;
			case "hindrenweavehall": Width = 8; Height = 6; return true;
			case "mudhut": Width = 4; Height = 3; return true;
			case "mudhutcourt": Width = 5; Height = 4; return true;
			case "caravanserai": Width = 12; Height = 9; return true;
			case "stiltrow": Width = 8; Height = 6; return true;
			case "gravegrove": Width = 5; Height = 4; return true;
			case "sporecellar": Width = 8; Height = 6; return true;
			case "caproof": Width = 4; Height = 3; return true;
			case "bonefold": Width = 5; Height = 4; return true;
			case "sacramentcourt": Width = 12; Height = 9; return true;
			case "blockhut": Width = 4; Height = 3; return true;
			case "blockyard": Width = 5; Height = 4; return true;
			case "rubblewall": Width = 1; Height = 1; return true;
			case "carvedcell": Width = 4; Height = 3; return true;
			case "carvedgallery": Width = 8; Height = 6; return true;
			case "fungalvault": Width = 8; Height = 6; return true;
			case "vaultgalleries": Width = 12; Height = 9; return true;
			case "deepcut": Width = 8; Height = 6; return true;
			case "nichetomb": Width = 5; Height = 4; return true;
			case "delve": Width = 8; Height = 6; return true;
			case "underbench": Width = 8; Height = 6; return true;
			case "reliquary": Width = 12; Height = 9; return true;
			case "factorhouse": Width = 8; Height = 6; return true;
			case "butcherslab": Width = 5; Height = 4; return true;
			case "vathouse": Width = 8; Height = 6; return true;
			case "graftinghall": Width = 12; Height = 9; return true;
			case "chimerictheatre": Width = 20; Height = 14; return true;
			case "becomingannexe": Width = 20; Height = 14; return true;
			case "mirrorgate": Width = 11; Height = 8; return true;
			case "crownhall": Width = 14; Height = 10; return true;
			case "arcology": Width = 20; Height = 14; return true;
			case "arcologyward": Width = 12; Height = 9; return true;
			case "arcologyterrace": Width = 8; Height = 6; return true;
			case "hallsurgery": Width = 8; Height = 6; return true;
			case "registryoffice": Width = 8; Height = 6; return true;
			default: return false;
			}
		}
	}
}
