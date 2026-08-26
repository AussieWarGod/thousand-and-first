using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Engine-free merge, validation, selection, and presentation rules for the open
	/// city-style registry.</summary>
	public static partial class KingdomStyleRules
	{
		public const int MaxStyles = 64;
		public const int MaxNameChars = 64;
		public const int MaxSelectorChars = 512;
		public const int MaxSelectorTokens = 32;
		public const int MaxGroundClauseChars = 256;
		public const int MaxBlueprintChars = 128;
		public const int MinPriority = -10000;
		public const int MaxPriority = 10000;

		public static bool ValidName(string Name)
		{
			if (string.IsNullOrWhiteSpace(Name)) return false;
			string value = Name.Trim();
			if (value.Length > MaxNameChars) return false;
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if (char.IsControl(c) || char.IsWhiteSpace(c) || c == ',' || c == '!') return false;
			}
			return true;
		}

		/// <summary>Merges a later declaration over the prior one. Name matching belongs to the
		/// registry; this method preserves the first declaration's canonical spelling.</summary>
		public static KingdomStyleDraft Merge(KingdomStyleDraft Earlier, KingdomStyleDraft Later)
		{
			if (Earlier == null) return Later == null ? null : Later.Copy();
			if (Later == null) return Earlier.Copy();
			KingdomStyleDraft merged = Earlier.Copy();
			if (Later.Terrain != null) merged.Terrain = Later.Terrain;
			if (Later.Region != null) merged.Region = Later.Region;
			if (Later.Strata != null) merged.Strata = Later.Strata;
			if (Later.Priority != null) merged.Priority = Later.Priority;
			if (Later.GroundClause != null) merged.GroundClause = Later.GroundClause;
			if (Later.Crop != null) merged.Crop = Later.Crop;
			if (Later.Seed != null) merged.Seed = Later.Seed;
			if (Later.CropRow != null) merged.CropRow = Later.CropRow;
			if (Later.WallMaterial != null) merged.WallMaterial = Later.WallMaterial;
			if (Later.TimberWall != null) merged.TimberWall = Later.TimberWall;
			return merged;
		}

		public static bool TryParse(KingdomStyleDraft Draft, out KingdomStyleDefinition Definition,
			out string Error)
		{
			Definition = null;
			Error = null;
			if (Draft == null || !ValidName(Draft.Name))
			{
				Error = "style needs a bounded Name without spaces, commas, !, or control characters";
				return false;
			}
			string name = Draft.Name.Trim();
			if (!TryTokens(Draft.Terrain, out string[] terrain, out string tokenError))
			{
				Error = "style " + name + " has bad Terrain selectors: " + tokenError;
				return false;
			}
			if (!TryTokens(Draft.Region, out string[] region, out tokenError))
			{
				Error = "style " + name + " has bad Region selectors: " + tokenError;
				return false;
			}
			KingdomStyleStratum stratum = KingdomStyleStratum.Any;
			string strata = string.IsNullOrWhiteSpace(Draft.Strata) ? null : Draft.Strata.Trim();
			if (strata != null)
			{
				if (string.Equals(strata, "all", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(strata, "any", StringComparison.OrdinalIgnoreCase))
					stratum = KingdomStyleStratum.Any;
				else if (string.Equals(strata, "surface", StringComparison.OrdinalIgnoreCase))
					stratum = KingdomStyleStratum.Surface;
				else if (string.Equals(strata, "deep", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(strata, "underground", StringComparison.OrdinalIgnoreCase))
					stratum = KingdomStyleStratum.Deep;
				else
				{
					Error = "style " + name + " has bad Strata (expected all, surface, or deep)";
					return false;
				}
			}
			int priority = 0;
			if (!string.IsNullOrWhiteSpace(Draft.Priority)
				&& (!int.TryParse(Draft.Priority.Trim(), out priority)
					|| priority < MinPriority || priority > MaxPriority))
			{
				Error = "style " + name + " has bad Priority";
				return false;
			}
			string clause = string.IsNullOrWhiteSpace(Draft.GroundClause)
				? null : Draft.GroundClause.Trim();
			if (clause != null && (clause.Length > MaxGroundClauseChars || HasControl(clause)))
			{
				Error = "style " + name + " has a bad GroundClause";
				return false;
			}
			if (!TryBlueprint(Draft.Crop, out string crop)
				|| !TryBlueprint(Draft.Seed, out string seed)
				|| !TryBlueprint(Draft.CropRow, out string cropRow)
				|| !TryBlueprint(Draft.TimberWall, out string timberWall))
			{
				Error = "style " + name + " has an overlong or control-bearing behavior blueprint";
				return false;
			}
			bool hasWallMaterial = !string.IsNullOrWhiteSpace(Draft.WallMaterial);
			KingdomMaterial wallMaterial = KingdomMaterial.Mud;
			if (hasWallMaterial && (Draft.WallMaterial.Length > MaxBlueprintChars
				|| HasControl(Draft.WallMaterial)))
			{
				Error = "style " + name + " has an overlong or control-bearing WallMaterial";
				return false;
			}
			if (hasWallMaterial
				&& !KingdomMaterialRules.TryParseMaterial(Draft.WallMaterial, out wallMaterial))
			{
				Error = "style " + name + " has bad WallMaterial";
				return false;
			}
			Definition = new KingdomStyleDefinition
			{
				Name = name, TerrainTokens = terrain, RegionTokens = region,
				Stratum = stratum, Priority = priority, GroundClause = clause,
				CropBlueprint = crop, SeedBlueprint = seed, CropRowBlueprint = cropRow,
				HasWallMaterial = hasWallMaterial, WallMaterial = wallMaterial,
				TimberWallBlueprint = timberWall
			};
			return true;
		}

		/// <summary>Proves a style's crop behavior is an atomic, reversible declaration. A style
		/// may omit the whole trio and inherit common, but never publish a crop without its seed and
		/// standing row. Shared triples are legal; conflicting reverse keys are refused.</summary>
		public static bool TryValidateBehavior(IList<KingdomStyleDefinition> Definitions,
			KingdomStyleDefinition Candidate, int ReplacingIndex, out string Error)
		{
			Error = null;
			if (Candidate == null)
			{
				Error = "style behavior has no definition";
				return false;
			}
			bool crop = !string.IsNullOrEmpty(Candidate.CropBlueprint);
			bool seed = !string.IsNullOrEmpty(Candidate.SeedBlueprint);
			bool row = !string.IsNullOrEmpty(Candidate.CropRowBlueprint);
			if ((crop || seed || row) && !(crop && seed && row))
			{
				Error = "style " + Candidate.Name
					+ " must declare Crop, Seed, and CropRow together";
				return false;
			}
			if (!crop || Definitions == null) return true;
			for (int i = 0; i < Definitions.Count; i++)
			{
				if (i == ReplacingIndex) continue;
				KingdomStyleDefinition other = Definitions[i];
				if (other == null || string.IsNullOrEmpty(other.CropBlueprint)) continue;
				if (string.Equals(other.SeedBlueprint, Candidate.SeedBlueprint,
					StringComparison.Ordinal)
					&& !string.Equals(other.CropBlueprint, Candidate.CropBlueprint,
						StringComparison.Ordinal))
				{
					Error = "style " + Candidate.Name + " maps seed " + Candidate.SeedBlueprint
						+ " to a different crop than style " + other.Name;
					return false;
				}
				if (string.Equals(other.CropBlueprint, Candidate.CropBlueprint,
					StringComparison.Ordinal)
					&& (!string.Equals(other.SeedBlueprint, Candidate.SeedBlueprint,
						StringComparison.Ordinal)
						|| !string.Equals(other.CropRowBlueprint, Candidate.CropRowBlueprint,
							StringComparison.Ordinal)))
				{
					Error = "style " + Candidate.Name + " gives crop " + Candidate.CropBlueprint
						+ " a different seed or row than style " + other.Name;
					return false;
				}
			}
			return true;
		}
	}
}
