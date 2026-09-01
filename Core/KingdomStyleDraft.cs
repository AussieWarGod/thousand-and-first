using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Raw merge-by-name style declaration. Null means omitted and survives a later
	/// layer; blank means explicitly cleared, matching building catalogue merge semantics.</summary>
	public sealed class KingdomStyleDraft
	{
		public string Name;
		/// <summary>Former or alternate keys accepted for merge, save migration, catalogue tags,
		/// skins, and architecture selectors. The first declaration's <see cref="Name"/> remains
		/// canonical; aliases never become a second style.</summary>
		public string Aliases;
		public string Terrain;
		public string Region;
		public string Strata;
		public string Priority;
		public string GroundClause;
		public string Crop;
		public string Seed;
		public string CropRow;
		public string WallMaterial;
		public string TimberWall;

		public KingdomStyleDraft Copy()
		{
			return new KingdomStyleDraft
			{
				Name = Name, Aliases = Aliases, Terrain = Terrain, Region = Region, Strata = Strata,
				Priority = Priority, GroundClause = GroundClause, Crop = Crop,
				Seed = Seed, CropRow = CropRow, WallMaterial = WallMaterial,
				TimberWall = TimberWall
			};
		}
	}
}
