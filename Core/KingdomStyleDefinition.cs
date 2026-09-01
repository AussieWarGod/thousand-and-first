using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Validated data-driven city style. Terrain and region tokens are substring
	/// selectors because Qud names one biome through many terrain-blueprint variants.</summary>
	public sealed class KingdomStyleDefinition
	{
		public string Name;
		public string[] Aliases;
		public string[] TerrainTokens;
		public string[] RegionTokens;
		public KingdomStyleStratum Stratum;
		public int Priority;
		public string GroundClause;
		public string CropBlueprint;
		public string SeedBlueprint;
		public string CropRowBlueprint;
		public bool HasWallMaterial;
		public KingdomMaterial WallMaterial;
		public string TimberWallBlueprint;
	}
}
