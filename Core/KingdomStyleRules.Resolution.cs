using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomStyleRules
	{
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
				if (MatchesName(definition, wanted))
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
					if (MatchesName(definition, Name))
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
				if (MatchesName(definition, Name)) return definition;
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
