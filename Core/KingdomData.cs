using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomData
	{
		private static List<KingdomRules.BuildEntry> _buildings;

		private static List<string> _styles;

		private static List<KingdomStyleDraft> _styleDrafts;

		private static List<KingdomStyleDefinition> _styleDefinitions;

		private static List<KingdomRules.DealEntry> _deals;

		public static List<KingdomRules.DealEntry> Deals
		{
			get
			{
				EnsureLoaded();
				return _deals;
			}
		}

		public static bool TryGetDeal(string Key, out KingdomRules.DealEntry Entry)
		{
			EnsureLoaded();
			for (int i = 0; i < _deals.Count; i++)
			{
				if (_deals[i].Key == Key)
				{
					Entry = _deals[i];
					return true;
				}
			}
			Entry = null;
			return false;
		}

		public static List<KingdomRules.BuildEntry> Buildings
		{
			get
			{
				EnsureLoaded();
				return _buildings;
			}
		}

		public static List<string> Styles
		{
			get
			{
				EnsureLoaded();
				return _styles;
			}
		}

		/// <summary>Canonicalizes a style against the merged registry.</summary>
		public static bool TryGetStyle(string Name, out string Canonical)
		{
			EnsureLoaded();
			return KingdomStyleRules.TryCanonical(_styleDefinitions, Name, out Canonical);
		}

		/// <summary>Resolves founding terrain against all merged style selectors. Exact terrain
		/// blueprint evidence outranks region evidence; priority then declaration order break ties.</summary>
		public static string StyleForSite(string TerrainBlueprint, string RegionName, int ZLevel)
		{
			EnsureLoaded();
			return KingdomStyleRules.Resolve(_styleDefinitions, TerrainBlueprint, RegionName,
				ZLevel, KingdomRules.SurfaceZLevel);
		}

		public static string StyleGroundClause(string Style)
		{
			EnsureLoaded();
			return KingdomStyleRules.DescribeGround(_styleDefinitions, Style);
		}

		/// <summary>Behavior declarations live on the same merged style row as founding. These
		/// lookups keep third-party styles out of closed switches while retaining total common-style
		/// fallbacks for old files that declared only a name.</summary>
		public static string CropForStyle(string Style)
		{
			EnsureLoaded();
			return KingdomStyleRules.CropForStyle(_styleDefinitions, Style);
		}

		public static string SeedForStyle(string Style)
		{
			EnsureLoaded();
			return KingdomStyleRules.SeedForStyle(_styleDefinitions, Style);
		}

		public static string CropRowForStyle(string Style)
		{
			EnsureLoaded();
			return KingdomStyleRules.CropRowForStyle(_styleDefinitions, Style);
		}

		public static string CropForSeed(string SeedBlueprint)
		{
			EnsureLoaded();
			return KingdomStyleRules.CropForSeed(_styleDefinitions, SeedBlueprint);
		}

		public static string SeedForCrop(string CropBlueprint)
		{
			EnsureLoaded();
			return KingdomStyleRules.SeedForCrop(_styleDefinitions, CropBlueprint);
		}

		public static string RowForCrop(string CropBlueprint)
		{
			EnsureLoaded();
			return KingdomStyleRules.RowForCrop(_styleDefinitions, CropBlueprint);
		}

		public static bool TryStyleWallMaterial(string Style, out KingdomMaterial Material)
		{
			EnsureLoaded();
			return KingdomStyleRules.TryWallMaterial(_styleDefinitions, Style, out Material);
		}

		public static string TimberWallForStyle(string Style)
		{
			EnsureLoaded();
			return KingdomStyleRules.TimberWallForStyle(_styleDefinitions, Style);
		}

		public static void Reload()
		{
			_buildings = null;
			_styles = null;
			_styleDrafts = null;
			_styleDefinitions = null;
			_deals = null;
			EnsureLoaded();
		}

		/// <summary>
		/// Reads the registries if they have not been read yet, and does nothing if they have.
		/// The trigger for anything that lives beside the catalog rather than in it &mdash; zoning
		/// gates, upgrade chains &mdash; which are filled during this same pass and would otherwise
		/// answer from an empty table for whoever asked first.
		/// </summary>
		public static void EnsureBuildings()
		{
			EnsureLoaded();
		}

		public static bool TryGetBuilding(string Key, out KingdomRules.BuildEntry Entry)
		{
			EnsureLoaded();
			for (int i = 0; i < _buildings.Count; i++)
			{
				if (_buildings[i].Key == Key)
				{
					Entry = _buildings[i];
					return true;
				}
			}
			Entry = null;
			return false;
		}

	}
}
