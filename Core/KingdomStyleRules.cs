using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public enum KingdomStyleStratum : byte
	{
		Any = 0,
		Surface = 1,
		Deep = 2
	}

	/// <summary>Raw merge-by-name style declaration. Null means omitted and survives a later
	/// layer; blank means explicitly cleared, matching building catalogue merge semantics.</summary>
	public sealed class KingdomStyleDraft
	{
		public string Name;
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
				Name = Name, Terrain = Terrain, Region = Region, Strata = Strata,
				Priority = Priority, GroundClause = GroundClause, Crop = Crop,
				Seed = Seed, CropRow = CropRow, WallMaterial = WallMaterial,
				TimberWall = TimberWall
			};
		}
	}

	/// <summary>Validated data-driven city style. Terrain and region tokens are substring
	/// selectors because Qud names one biome through many terrain-blueprint variants.</summary>
	public sealed class KingdomStyleDefinition
	{
		public string Name;
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

	/// <summary>Engine-free merge, validation, selection, and presentation rules for the open
	/// city-style registry.</summary>
	public static class KingdomStyleRules
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

		/// <summary>Resolves exact terrain evidence. Terrain-blueprint matches outrank every
		/// region match; within one lane, greater priority wins and declaration order breaks ties.</summary>
		public static string Resolve(IList<KingdomStyleDefinition> Definitions,
			string TerrainBlueprint, string RegionName, int ZLevel, int SurfaceZLevel,
			string Fallback = "common")
		{
			string found = Best(Definitions, TerrainBlueprint, ZLevel, SurfaceZLevel, terrain: true);
			if (found == null)
				found = Best(Definitions, RegionName, ZLevel, SurfaceZLevel, terrain: false);
			if (found != null) return found;
			string canonical;
			return TryCanonical(Definitions, Fallback, out canonical) ? canonical : "common";
		}

		public static bool TryCanonical(IList<KingdomStyleDefinition> Definitions, string Name,
			out string Canonical)
		{
			Canonical = null;
			if (Definitions == null || string.IsNullOrWhiteSpace(Name)) return false;
			string wanted = Name.Trim();
			for (int i = 0; i < Definitions.Count; i++)
			{
				KingdomStyleDefinition definition = Definitions[i];
				if (definition != null && string.Equals(definition.Name, wanted,
					StringComparison.OrdinalIgnoreCase))
				{
					Canonical = definition.Name;
					return true;
				}
			}
			return false;
		}

		public static string DescribeGround(IList<KingdomStyleDefinition> Definitions, string Name)
		{
			if (Definitions != null)
			{
				for (int i = 0; i < Definitions.Count; i++)
				{
					KingdomStyleDefinition definition = Definitions[i];
					if (definition != null && string.Equals(definition.Name, Name,
						StringComparison.OrdinalIgnoreCase))
					{
						return definition.GroundClause ?? ("ground claimed by a " + definition.Name + " city");
					}
				}
			}
			return "ground the settlement has made its own";
		}

		/// <summary>Behavior attributes use the same open registry as founding. Missing attributes
		/// inherit the common style's declaration; hard fallbacks exist only for a malformed or
		/// unavailable registry, never as a closed list of style names.</summary>
		public static string CropForStyle(IList<KingdomStyleDefinition> Definitions, string Name)
		{
			KingdomStyleDefinition exact = Find(Definitions, Name);
			if (exact != null && !string.IsNullOrEmpty(exact.CropBlueprint)) return exact.CropBlueprint;
			KingdomStyleDefinition common = Find(Definitions, "common");
			return (common != null && !string.IsNullOrEmpty(common.CropBlueprint))
				? common.CropBlueprint : "Starapple";
		}

		public static string SeedForStyle(IList<KingdomStyleDefinition> Definitions, string Name)
		{
			KingdomStyleDefinition exact = Find(Definitions, Name);
			if (exact != null && !string.IsNullOrEmpty(exact.SeedBlueprint)) return exact.SeedBlueprint;
			KingdomStyleDefinition common = Find(Definitions, "common");
			return (common != null && !string.IsNullOrEmpty(common.SeedBlueprint))
				? common.SeedBlueprint : "r_KingdomSeedStarapple";
		}

		public static string CropRowForStyle(IList<KingdomStyleDefinition> Definitions, string Name)
		{
			KingdomStyleDefinition exact = Find(Definitions, Name);
			if (exact != null && !string.IsNullOrEmpty(exact.CropRowBlueprint)) return exact.CropRowBlueprint;
			KingdomStyleDefinition common = Find(Definitions, "common");
			return (common != null && !string.IsNullOrEmpty(common.CropRowBlueprint))
				? common.CropRowBlueprint : "r_KingdomRowStarapple";
		}

		public static string CropForSeed(IList<KingdomStyleDefinition> Definitions, string Seed)
		{
			if (Definitions == null || string.IsNullOrEmpty(Seed)) return null;
			for (int i = 0; i < Definitions.Count; i++)
			{
				KingdomStyleDefinition definition = Definitions[i];
				if (definition != null && string.Equals(definition.SeedBlueprint, Seed,
					StringComparison.Ordinal)) return definition.CropBlueprint;
			}
			return null;
		}

		public static string SeedForCrop(IList<KingdomStyleDefinition> Definitions, string Crop)
		{
			KingdomStyleDefinition found = FindCrop(Definitions, Crop);
			return found == null ? null : found.SeedBlueprint;
		}

		public static string RowForCrop(IList<KingdomStyleDefinition> Definitions, string Crop)
		{
			KingdomStyleDefinition found = FindCrop(Definitions, Crop);
			return found == null ? null : found.CropRowBlueprint;
		}

		public static bool TryWallMaterial(IList<KingdomStyleDefinition> Definitions, string Name,
			out KingdomMaterial Material)
		{
			Material = KingdomMaterial.Mud;
			KingdomStyleDefinition exact = Find(Definitions, Name);
			if (exact != null && exact.HasWallMaterial)
			{
				Material = exact.WallMaterial;
				return true;
			}
			return false;
		}

		public static string TimberWallForStyle(IList<KingdomStyleDefinition> Definitions,
			string Name)
		{
			KingdomStyleDefinition exact = Find(Definitions, Name);
			if (exact != null && !string.IsNullOrEmpty(exact.TimberWallBlueprint))
				return exact.TimberWallBlueprint;
			KingdomStyleDefinition common = Find(Definitions, "common");
			return (common != null && !string.IsNullOrEmpty(common.TimberWallBlueprint))
				? common.TimberWallBlueprint : "BrinestalkWall";
		}

		private static KingdomStyleDefinition Find(IList<KingdomStyleDefinition> Definitions,
			string Name)
		{
			if (Definitions == null || string.IsNullOrWhiteSpace(Name)) return null;
			for (int i = 0; i < Definitions.Count; i++)
			{
				KingdomStyleDefinition definition = Definitions[i];
				if (definition != null && string.Equals(definition.Name, Name.Trim(),
					StringComparison.OrdinalIgnoreCase)) return definition;
			}
			return null;
		}

		private static KingdomStyleDefinition FindCrop(
			IList<KingdomStyleDefinition> Definitions, string Crop)
		{
			if (Definitions == null || string.IsNullOrEmpty(Crop)) return null;
			for (int i = 0; i < Definitions.Count; i++)
			{
				KingdomStyleDefinition definition = Definitions[i];
				if (definition != null && string.Equals(definition.CropBlueprint, Crop,
					StringComparison.Ordinal)) return definition;
			}
			return null;
		}

		private static string Best(IList<KingdomStyleDefinition> Definitions, string Ground,
			int ZLevel, int SurfaceZLevel, bool terrain)
		{
			if (Definitions == null || string.IsNullOrEmpty(Ground)) return null;
			KingdomStyleDefinition best = null;
			for (int i = 0; i < Definitions.Count; i++)
			{
				KingdomStyleDefinition candidate = Definitions[i];
				if (candidate == null || !Admits(candidate.Stratum, ZLevel, SurfaceZLevel)) continue;
				string[] tokens = terrain ? candidate.TerrainTokens : candidate.RegionTokens;
				if (!Matches(Ground, tokens)) continue;
				if (best == null || candidate.Priority > best.Priority) best = candidate;
			}
			return best == null ? null : best.Name;
		}

		private static bool Admits(KingdomStyleStratum Stratum, int ZLevel, int SurfaceZLevel)
		{
			bool deep = ZLevel > SurfaceZLevel;
			return Stratum == KingdomStyleStratum.Any
				|| (Stratum == KingdomStyleStratum.Deep && deep)
				|| (Stratum == KingdomStyleStratum.Surface && !deep);
		}

		private static bool Matches(string Ground, string[] Tokens)
		{
			if (Tokens == null) return false;
			for (int i = 0; i < Tokens.Length; i++)
				if (Ground.IndexOf(Tokens[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
			return false;
		}

		private static bool TryTokens(string Raw, out string[] Tokens, out string Error)
		{
			Tokens = null;
			Error = null;
			if (string.IsNullOrWhiteSpace(Raw)) return true;
			if (Raw.Length > MaxSelectorChars || HasControl(Raw))
			{
				Error = "value is overlong or contains a control character";
				return false;
			}
			string[] split = Raw.Split(',');
			if (split.Length > MaxSelectorTokens)
			{
				Error = "too many tokens";
				return false;
			}
			List<string> tokens = new List<string>();
			for (int i = 0; i < split.Length; i++)
			{
				string token = split[i].Trim();
				if (token.Length == 0)
				{
					Error = "an empty token";
					return false;
				}
				if (!Contains(tokens, token)) tokens.Add(token);
			}
			Tokens = tokens.Count == 0 ? null : tokens.ToArray();
			return true;
		}

		private static bool Contains(List<string> Values, string Value)
		{
			for (int i = 0; i < Values.Count; i++)
				if (string.Equals(Values[i], Value, StringComparison.OrdinalIgnoreCase)) return true;
			return false;
		}

		private static bool HasControl(string Value)
		{
			for (int i = 0; i < Value.Length; i++) if (char.IsControl(Value[i])) return true;
			return false;
		}

		private static bool TryBlueprint(string Raw, out string Blueprint)
		{
			Blueprint = string.IsNullOrWhiteSpace(Raw) ? null : Raw.Trim();
			return Blueprint == null
				|| (Blueprint.Length <= MaxBlueprintChars && !HasControl(Blueprint));
		}
	}
}
