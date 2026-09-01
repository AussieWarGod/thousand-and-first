using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>One immutable terrain/role/technology paving choice.</summary>
	public sealed class KingdomRoadSurfaceRule
	{
		public string Key { get; private set; }
		public string Terrain { get; private set; }
		public string Role { get; private set; }
		public TechLevel MinimumTech { get; private set; }
		public TechLevel MaximumTech { get; private set; }
		public string Blueprint { get; private set; }
		public KingdomMaterial Material { get; private set; }
		public int Priority { get; private set; }

		public KingdomRoadSurfaceRule(string Key, string Terrain, string Role,
			TechLevel MinimumTech, TechLevel MaximumTech, string Blueprint,
			KingdomMaterial Material, int Priority = 0)
		{
			this.Key = Key == null ? null : Key.Trim().ToLowerInvariant();
			this.Terrain = Terrain == null ? null : Terrain.Trim().ToLowerInvariant();
			this.Role = Role == null ? null : Role.Trim().ToLowerInvariant();
			this.MinimumTech = MinimumTech;
			this.MaximumTech = MaximumTech;
			this.Blueprint = Blueprint == null ? null : Blueprint.Trim();
			this.Material = Material;
			this.Priority = Priority;
		}
	}

	/// <summary>The exact surface and embodied material frozen into one paving order.</summary>
	public readonly struct KingdomRoadSurface
	{
		public readonly string RuleKey;
		public readonly string Blueprint;
		public readonly KingdomMaterial Material;

		public KingdomRoadSurface(string RuleKey, string Blueprint, KingdomMaterial Material)
		{
			this.RuleKey = RuleKey;
			this.Blueprint = Blueprint;
			this.Material = Material;
		}
	}

	/// <summary>
	/// Open-string road palette. Terrain, route purpose, and current craft are independent
	/// inputs; the winning physical surface is frozen by the ordinary construction receipt.
	/// </summary>
	public static class KingdomRoadPaletteRules
	{
		public const string Any = "*";
		public const int MaxRegisteredRules = 64;
		public const int MaxCapabilityChars = 64;

		public const string LocalRole = "local";
		public const string ServiceRole = "service";
		public const string MarketRole = "market";
		public const string CaravanRole = "caravan";
		public const string GateRole = "gate";
		public const string MonumentalRole = "monumental";

		private static readonly KingdomRoadSurfaceRule[] BuiltIns =
		{
			R("base-hands", Any, Any, TechLevel.Hands, TechLevel.Salvage,
				"SaltPath", KingdomMaterial.Stone, 0),
			R("base-workshop", Any, Any, TechLevel.Workshop, TechLevel.Arclight,
				"FoamcreteFloor", KingdomMaterial.ShapedStone, 0),
			R("verdant-hands", "verdant", Any, TechLevel.Hands, TechLevel.Salvage,
				"WoodFloor", KingdomMaterial.Timber, 10),
			R("verdant-workshop", "verdant", Any, TechLevel.Workshop, TechLevel.Arclight,
				"WoodFloor", KingdomMaterial.ShapedTimber, 10),
			R("fungal-hands", "fungal", Any, TechLevel.Hands, TechLevel.Salvage,
				"WoodFloor", KingdomMaterial.Timber, 10),
			R("fungal-workshop", "fungal", Any, TechLevel.Workshop, TechLevel.Arclight,
				"FoamcreteFloor", KingdomMaterial.ShapedStone, 10),
			R("moonstair-marble", "moonstair", Any, TechLevel.Hands, TechLevel.Arclight,
				"BlackMarbleWalkway", KingdomMaterial.Marble, 10),
			R("eater-hands", "eater", Any, TechLevel.Hands, TechLevel.Hands,
				"SaltPath", KingdomMaterial.Stone, 10),
			R("eater-salvage", "eater", Any, TechLevel.Salvage, TechLevel.Workshop,
				"GreenTile", KingdomMaterial.Scrap, 10),
			R("eater-foundry", "eater", Any, TechLevel.Foundry, TechLevel.Arclight,
				"SmallHexFloor", KingdomMaterial.WorkedMetal, 10),
			R("ruins-hands", "ruins", Any, TechLevel.Hands, TechLevel.Hands,
				"SaltPath", KingdomMaterial.Stone, 10),
			R("ruins-salvage", "ruins", Any, TechLevel.Salvage, TechLevel.Workshop,
				"FoamcreteFloor", KingdomMaterial.Stone, 10),
			R("ruins-foundry", "ruins", Any, TechLevel.Foundry, TechLevel.Arclight,
				"FoamcreteFloor", KingdomMaterial.ShapedStone, 10),
			R("deep-hands", "deep", Any, TechLevel.Hands, TechLevel.Salvage,
				"SaltPath", KingdomMaterial.Stone, 10),
			R("deep-workshop", "deep", Any, TechLevel.Workshop, TechLevel.Arclight,
				"FoamcreteFloor", KingdomMaterial.ShapedStone, 10),
			R("service-workshop", Any, ServiceRole, TechLevel.Workshop, TechLevel.Workshop,
				"FoamcreteFloor", KingdomMaterial.ShapedStone, 20),
			R("service-foundry", Any, ServiceRole, TechLevel.Foundry, TechLevel.Arclight,
				"SmallHexFloor", KingdomMaterial.WorkedMetal, 20),
			R("market-workshop", Any, MarketRole, TechLevel.Workshop, TechLevel.Arclight,
				"MarbleFloor", KingdomMaterial.Marble, 20),
			R("caravan-way", Any, CaravanRole, TechLevel.Hands, TechLevel.Arclight,
				"SaltPath", KingdomMaterial.Stone, 20),
			R("gate-workshop", Any, GateRole, TechLevel.Workshop, TechLevel.Arclight,
				"FoamcreteFloor", KingdomMaterial.ShapedStone, 20),
			R("monumental-foundry", Any, MonumentalRole, TechLevel.Foundry, TechLevel.Arclight,
				"MarbleFloor", KingdomMaterial.Marble, 20),
			R("eater-monumental", "eater", MonumentalRole,
				TechLevel.Foundry, TechLevel.Arclight, "SmallHexFloor",
				KingdomMaterial.WorkedMetal, 30)
		};

		private static readonly object Sync = new object();
		private static readonly List<KingdomRoadSurfaceRule> Registered =
			new List<KingdomRoadSurfaceRule>();

		public static IList<KingdomRoadSurfaceRule> DefaultRules()
		{
			return new List<KingdomRoadSurfaceRule>(BuiltIns).AsReadOnly();
		}

		/// <summary>Registers one bounded behavior-lane extension. Keys are idempotent, not layered.</summary>
		public static bool RegisterSurfaceRule(KingdomRoadSurfaceRule Rule, out string Failure)
		{
			Failure = null;
			if (!Valid(Rule))
			{
				Failure = "The road-surface rule is malformed.";
				return false;
			}
			lock (Sync)
			{
				KingdomRoadSurfaceRule prior = FindKey(BuiltIns, Rule.Key)
					?? FindKey(Registered, Rule.Key);
				if (prior != null)
				{
					if (Equivalent(prior, Rule)) return true;
					Failure = "Road-surface key " + Rule.Key + " is already registered differently.";
					return false;
				}
				if (Registered.Count >= MaxRegisteredRules)
				{
					Failure = "The road-surface extension registry is full.";
					return false;
				}
				Registered.Add(Rule);
				return true;
			}
		}

		public static bool TryResolveCurrent(string Terrain, string Role, TechLevel Tech,
			out KingdomRoadSurface Surface)
		{
			List<KingdomRoadSurfaceRule> rules = new List<KingdomRoadSurfaceRule>(BuiltIns);
			lock (Sync) rules.AddRange(Registered);
			return TryResolve(rules, Terrain, Role, Tech, out Surface);
		}

		public static bool TryResolve(IList<KingdomRoadSurfaceRule> Rules, string Terrain,
			string Role, TechLevel Tech, out KingdomRoadSurface Surface)
		{
			Surface = default(KingdomRoadSurface);
			if (Rules == null || !TryCapability(Terrain, false, out string terrain)
				|| !TryCapability(Role, false, out string role) || !KnownTech(Tech)) return false;
			KingdomRoadSurfaceRule best = null;
			Dictionary<string, KingdomRoadSurfaceRule> keys =
				new Dictionary<string, KingdomRoadSurfaceRule>(StringComparer.Ordinal);
			for (int i = 0; i < Rules.Count; i++)
			{
				KingdomRoadSurfaceRule rule = Rules[i];
				if (!Valid(rule)) return false;
				if (keys.TryGetValue(rule.Key, out var prior))
				{
					if (!Equivalent(prior, rule)) return false;
					continue;
				}
				keys.Add(rule.Key, rule);
				if (Tech < rule.MinimumTech || Tech > rule.MaximumTech
					|| !Selector(rule.Terrain, terrain) || !Selector(rule.Role, role)) continue;
				if (best == null || Better(rule, best, terrain, role)) best = rule;
			}
			if (best == null) return false;
			Surface = new KingdomRoadSurface(best.Key, best.Blueprint, best.Material);
			return true;
		}

		public static string TerrainKey(string Style, string Region, bool Underground)
		{
			if (Underground) return "deep";
			if (!string.IsNullOrEmpty(Region)
				&& Region.IndexOf("ruin", StringComparison.OrdinalIgnoreCase) >= 0) return "ruins";
			string migrated = KingdomStyleRules.MigrateLegacyKey(Style);
			return TryCapability(migrated, false, out string style) ? style : "common";
		}

		public static bool TryRole(string Value, out string Role)
		{
			return TryCapability(Value, false, out Role);
		}

		private static bool Better(KingdomRoadSurfaceRule A, KingdomRoadSurfaceRule B,
			string Terrain, string Role)
		{
			if (A.Priority != B.Priority) return A.Priority > B.Priority;
			int a = (A.Terrain == Terrain ? 2 : 0) + (A.Role == Role ? 1 : 0);
			int b = (B.Terrain == Terrain ? 2 : 0) + (B.Role == Role ? 1 : 0);
			if (a != b) return a > b;
			if (A.MinimumTech != B.MinimumTech) return A.MinimumTech > B.MinimumTech;
			return string.CompareOrdinal(A.Key, B.Key) < 0;
		}

		private static bool Valid(KingdomRoadSurfaceRule Rule)
		{
			return Rule != null && TryCapability(Rule.Key, false, out _)
				&& TryCapability(Rule.Terrain, true, out _)
				&& TryCapability(Rule.Role, true, out _)
				&& KnownTech(Rule.MinimumTech) && KnownTech(Rule.MaximumTech)
				&& Rule.MinimumTech <= Rule.MaximumTech && Rule.Priority >= -1000
				&& Rule.Priority <= 1000 && !string.IsNullOrWhiteSpace(Rule.Blueprint)
				&& Rule.Blueprint.Length <= 128 && KingdomRoadRules.CanPaveIn(Rule.Material);
		}

		private static bool TryCapability(string Value, bool Wildcard, out string Canonical)
		{
			Canonical = null;
			if (Wildcard && Value == Any) { Canonical = Any; return true; }
			if (string.IsNullOrWhiteSpace(Value)) return false;
			string text = Value.Trim().ToLowerInvariant();
			if (text.Length > MaxCapabilityChars) return false;
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9')
					&& c != ':' && c != '.' && c != '_' && c != '-') return false;
			}
			Canonical = text;
			return true;
		}

		private static bool KnownTech(TechLevel Tech)
		{
			return Tech >= TechLevel.Hands && Tech <= TechLevel.Arclight;
		}

		private static bool Selector(string Selector, string Value)
		{
			return Selector == Any || Selector == Value;
		}

		private static KingdomRoadSurfaceRule FindKey(IEnumerable<KingdomRoadSurfaceRule> Rules,
			string Key)
		{
			foreach (KingdomRoadSurfaceRule rule in Rules)
				if (rule != null && rule.Key == Key) return rule;
			return null;
		}

		private static bool Equivalent(KingdomRoadSurfaceRule A, KingdomRoadSurfaceRule B)
		{
			return A.Key == B.Key && A.Terrain == B.Terrain && A.Role == B.Role
				&& A.MinimumTech == B.MinimumTech && A.MaximumTech == B.MaximumTech
				&& A.Blueprint == B.Blueprint && A.Material == B.Material
				&& A.Priority == B.Priority;
		}

		private static KingdomRoadSurfaceRule R(string Key, string Terrain, string Role,
			TechLevel Minimum, TechLevel Maximum, string Blueprint,
			KingdomMaterial Material, int Priority)
		{
			return new KingdomRoadSurfaceRule(Key, Terrain, Role, Minimum, Maximum,
				Blueprint, Material, Priority);
		}
	}
}
