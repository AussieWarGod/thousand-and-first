using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Pure regenerated phenotype resolver; no world time, runtime hash, or random draw.</summary>
	public static partial class KingdomPolityNpcRules
	{
		public const int RulesVersion = 3;

		/// <summary>Frozen resolver for schema-1 cohort plans only.</summary>
		public static bool TryResolve(KingdomPolityProfileRevision Profile, string RoleKey,
			int Ordinal, out KingdomPolityNpcSpec Spec, out string Failure)
		{
			Spec = null; Failure = null;
			if (!ValidProfile(Profile, false) || !KingdomPolityRules.Text(RoleKey, true) ||
				!Contains(Profile.RoleKeys, RoleKey) || Ordinal < 0 || Ordinal > 1023)
			{
				Failure = "polity NPC resolver input is invalid or outside the immutable profile";
				return false;
			}
			string expression = KingdomPolityRules.ProfileExpressionDigest(Profile);
			string digest = KingdomPolityRules.ActivationDigest("polity-npc-spec-v1",
				Profile.ProfileId, Profile.Revision.ToString(CultureInfo.InvariantCulture),
				expression, RoleKey, Ordinal.ToString(CultureInfo.InvariantCulture));
			int draw = int.Parse(digest.Substring(0, 2), NumberStyles.HexNumber,
				CultureInfo.InvariantCulture);
			string bodyKey = Profile.BodyKeys[draw % Profile.BodyKeys.Count];
			string bodyBlueprint = BodyBlueprint(bodyKey);
			if (bodyBlueprint == null) { Failure =
				"frozen polity profile has no admissible manifested body"; return false; }
			int roleBonus = RoleBonus(RoleKey);
			int level = Math.Min(40, 1 + Profile.TechnologyBand * 3 + roleBonus + draw % 4);
			int baseline = Math.Min(28, 14 + Profile.TechnologyBand + level / 5);
			KingdomPolityNpcSpec result = new KingdomPolityNpcSpec
			{
				ResolverDigest = digest, ProfileId = Profile.ProfileId,
					ProfileRevision = Profile.Revision, RoleKey = RoleKey, Ordinal = Ordinal,
					ProfileRulesVersion = Profile.RulesVersion,
				TechnologyBand = Profile.TechnologyBand,
				BodyBlueprint = bodyBlueprint, Level = level,
				Strength = baseline, Agility = baseline, Toughness = baseline,
				Intelligence = baseline, Willpower = baseline, Ego = baseline,
				Hitpoints = 10 + level * 3
			};
			ApplyBodyStats(result, bodyKey); ApplyRoleStats(result);
			bool legacy = Profile.RulesVersion == KingdomPolityProfileRules.LegacyRulesVersion;
			result.Skills = Skills(RoleKey, Profile.TechnologyBand, legacy);
			result.Mutations = Mutations(bodyKey, Profile.TechnologyBand, digest, legacy);
			result.GearBlueprints = Gear(Profile.TechnologyBand, RoleKey, bodyKey);
			if (Profile.RulesVersion == KingdomPolityProfileRules.PriorExpressionRulesVersion)
				ApplyExpression(Profile, result, digest, true);
			Spec = result; return true;
		}
		/// <summary>
		/// Frozen resolver for version-2 cohort plans. The encounter band remains plan-owned.
		/// </summary>
		private static bool TryResolveV2(KingdomPolityProfileRevision Profile, string RoleKey,
			int Ordinal, int MinimumLevel, int MaximumLevel,
			out KingdomPolityNpcSpec Spec, out string Failure)
		{
			Spec = null; Failure = null;
			if (!ValidProfile(Profile, true) || !KingdomPolityRules.Text(RoleKey, true) ||
				!Contains(Profile.RoleKeys, RoleKey) || Ordinal < 0 || Ordinal > 1023 ||
				MinimumLevel < 1 || MaximumLevel < MinimumLevel ||
				MaximumLevel > KingdomPolityRules.MaxLevel)
			{
				Failure = "current polity NPC resolver lacks a legal pinned profile, role, or level band";
				return false;
			}
			string expression = KingdomPolityRules.ProfileExpressionDigest(Profile);
			string digest = KingdomPolityRules.ActivationDigest("polity-npc-spec-v2",
				Profile.ProfileId, Profile.Revision.ToString(CultureInfo.InvariantCulture),
				expression, RoleKey, Ordinal.ToString(CultureInfo.InvariantCulture),
				MinimumLevel.ToString(CultureInfo.InvariantCulture),
				MaximumLevel.ToString(CultureInfo.InvariantCulture));
			int draw = int.Parse(digest.Substring(0, 2), NumberStyles.HexNumber,
				CultureInfo.InvariantCulture);
			string bodyKey = Profile.BodyKeys[draw % Profile.BodyKeys.Count];
			string bodyBlueprint = BodyBlueprint(bodyKey);
			if (bodyBlueprint == null) { Failure =
				"frozen polity profile has no admissible manifested body"; return false; }
			KingdomPolityNpcSpec result = new KingdomPolityNpcSpec
			{
				ResolverDigest = digest, ProfileId = Profile.ProfileId,
				ProfileRevision = Profile.Revision, ProfileRulesVersion = Profile.RulesVersion,
				RoleKey = RoleKey, Ordinal = Ordinal, TechnologyBand = Profile.TechnologyBand,
				BodyBlueprint = bodyBlueprint,
				Level = MinimumLevel + draw % (MaximumLevel - MinimumLevel + 1),
				Skills = Skills(RoleKey, 0, false),
				Mutations = new List<KingdomPolityMutationSpec>(),
				GearBlueprints = Gear(Profile.TechnologyBand, RoleKey, bodyKey)
			};
			ApplyExpression(Profile, result, digest); Spec = result; return true;
		}

		private static bool ValidProfile(KingdomPolityProfileRevision P, bool Current)
		{
			return P != null && KingdomPolityRules.TypedId(P.ProfileId, "taf:polity-profile:") &&
				P.Revision > 0 && KingdomPolityRules.SemanticId(P.PolityId) &&
				P.EffectiveTick >= 0L && (Current ? P.RulesVersion ==
					KingdomPolityProfileRules.RulesVersion : P.RulesVersion ==
					KingdomPolityProfileRules.LegacyRulesVersion || P.RulesVersion ==
					KingdomPolityProfileRules.PriorExpressionRulesVersion) &&
				KingdomPolityRules.Digest(P.FactsDigest) && P.TechnologyBand >= 0 &&
				P.TechnologyBand <= 10 && ValidList(P.DerivedFromFactIds, true, true) &&
				ValidList(P.PracticeTags, false, false) && ValidBodies(P.BodyKeys) &&
				ValidRoles(P.RoleKeys) && ValidList(P.GearKeys, false, false) &&
				ValidLoadout(P.Loadout) && ValidCues(P, Current);
		}

		private static bool ValidCues(KingdomPolityProfileRevision P, bool Current)
		{
			if (P.ExpressionCues == null ||
				P.ExpressionCues.Count > KingdomPolityProfileExpressionCatalogue.MaxCues) return false;
			if (P.RulesVersion == KingdomPolityProfileRules.LegacyRulesVersion)
				return P.ExpressionCues.Count == 0;
			KingdomPolityExpressionKind first = KingdomPolityExpressionKind.None;
			bool independent = false;
			for (int i = 0; i < P.ExpressionCues.Count; i++)
			{
				KingdomPolityExpressionCue cue = P.ExpressionCues[i];
				if (!KingdomPolityProfileExpressionCatalogue.ValidCue(cue) ||
					(Current && !KingdomPolityProfileExpressionCatalogue.CausallyAdmitted(cue)) ||
					(i > 0 && KingdomPolityProfileExpressionCatalogue.Compare(
						P.ExpressionCues[i - 1], cue) >= 0)) return false;
				if (first == KingdomPolityExpressionKind.None) first = cue.Kind;
				else if (cue.Kind != first) independent = true;
			}
			return independent;
		}

		private static string BodyBlueprint(string BodyKey)
		{
			if (BodyKey == "snapjaw") return "Snapjaw Warrior";
			if (BodyKey == "goatfolk") return "Goatfolk";
			if (BodyKey == "dromad") return "Dromad";
			if (BodyKey == "hindren") return "HindrenVillager";
			if (BodyKey == "mechanical") return "Scrapbot";
			return BodyKey == "human" ? "WatervineFarmer" : null;
		}

		private static void ApplyBodyStats(KingdomPolityNpcSpec S, string BodyKey)
		{
			switch (BodyKey)
			{
			case "snapjaw": S.Agility += 2; S.Toughness += 1; break;
			case "goatfolk": S.Strength += 4; S.Toughness += 3; break;
			case "dromad": S.Toughness += 3; S.Ego += 2; break;
			case "hindren": S.Agility += 3; S.Ego += 1; break;
			case "mechanical": S.Strength += 3; S.Toughness += 4; break;
			}
		}

		private static int RoleBonus(string Role)
		{
			switch (Role)
			{
			case "claimant": case "warband": return 4;
			case "guard": case "patrol": return 3;
			case "successor": case "namesake": return 2;
			case "envoy": case "trader": case "courier": return 1;
			default: return 0;
			}
		}

		private static void ApplyRoleStats(KingdomPolityNpcSpec S)
		{
			switch (S.RoleKey)
			{
			case "guard": case "patrol": case "warband":
				S.Strength += 3; S.Toughness += 3; S.Hitpoints += 12; break;
			case "claimant": S.Strength += 2; S.Ego += 3; S.Willpower += 2; break;
			case "envoy": case "namesake": case "successor":
				S.Ego += 4; S.Intelligence += 2; S.Willpower += 2; break;
			case "trader": S.Ego += 3; S.Intelligence += 3; break;
			case "courier": S.Agility += 4; S.Toughness += 1; break;
			case "migrant": S.Toughness += 2; S.Willpower += 1; break;
			case "cook": S.Intelligence += 3; S.Willpower += 2; break;
			}
		}

		private static List<string> Skills(string Role, int Technology, bool Legacy)
		{
			List<string> result = new List<string>();
			switch (Role)
			{
			case "guard": case "patrol": case "warband": case "claimant":
				result.Add("LongBlades"); result.Add("Tactics"); break;
			case "envoy": case "namesake": case "successor":
				result.Add("Customs"); result.Add("Persuasion"); break;
			case "trader": result.Add("Customs"); result.Add("Persuasion");
				if (Technology >= 4) result.Add("Tinkering"); break;
			case "courier": case "migrant": result.Add("Survival");
				result.Add(Legacy ? "Tactics_Run" : "Tactics_Hurdle"); break;
			case "cook": result.Add("CookingAndGathering");
				result.Add("CookingAndGathering_MealPreparation"); break;
			}
			result.Sort(StringComparer.Ordinal); return result;
		}

		private static List<KingdomPolityMutationSpec> Mutations(string BodyKey,
			int Technology, string Digest, bool Legacy)
		{
			List<KingdomPolityMutationSpec> result = new List<KingdomPolityMutationSpec>();
			if (BodyKey != "human") return result;
			int draw = int.Parse(Digest.Substring(2, 2), NumberStyles.HexNumber,
				CultureInfo.InvariantCulture);
			result.Add(new KingdomPolityMutationSpec
			{
				ClassName = draw % 2 == 0 ? "HeightenedHearing" :
					(Legacy ? "NightVision" : "DarkVision"),
				Level = Math.Min(5, 1 + Technology / 3)
			});
			return result;
		}

		private static List<string> Gear(int Technology, string Role, string BodyKey)
		{
			List<string> result = new List<string>();
			if (BodyKey == "mechanical") return result;
			bool military = Role == "guard" || Role == "patrol" || Role == "warband" ||
				Role == "claimant";
			string weapon = Role == "cook" || Technology <= 0 ? "Club" :
				(Technology <= 2 ? "Long Sword" : Technology <= 4 ? "Long Sword2" :
				 Technology <= 6 ? "Steel Long Sword" : "Long Sword3");
			string armor = military && Technology >= 7 ? "Carbide Plate Armor" :
				military && Technology >= 3 ? "Chain Mail" : "Leather Armor";
			result.Add(weapon); result.Add(armor);
			if (military) result.Add("Wooden Buckler");
			result.Sort(StringComparer.Ordinal); return result;
		}

		private static bool ValidBodies(IList<string> Values)
		{
			if (!ValidList(Values, true, false)) return false;
			for (int i = 0; i < Values.Count; i++)
				if (Values[i] != "human" && Values[i] != "snapjaw" && Values[i] != "goatfolk" &&
					Values[i] != "dromad" && Values[i] != "hindren" &&
					Values[i] != "mechanical" && Values[i] != "unresolved") return false;
			return true;
		}

		private static bool ValidRoles(IList<string> Values)
		{
			if (!ValidList(Values, true, false)) return false;
			for (int i = 0; i < Values.Count; i++) if (!KnownRole(Values[i])) return false;
			return true;
		}

		private static bool KnownRole(string Value)
		{
			switch (Value)
			{
			case "claimant": case "cook": case "courier": case "envoy": case "guard":
			case "migrant": case "namesake": case "patrol": case "successor":
			case "trader": case "warband": return true;
			default: return false;
			}
		}

		private static bool ValidLoadout(KingdomPolityLoadoutPolicy P)
		{
			return P != null && P.Kind >= KingdomPolityLoadoutPolicyKind.StockPreserve &&
				P.Kind <= KingdomPolityLoadoutPolicyKind.BoundedAdd &&
				P.ExpectedValueBudget >= 0 &&
				P.ExpectedValueBudget <= KingdomPolityRules.MaxValueBudget &&
				ValidList(P.ExcludedKeys, false, false) &&
				ValidList(P.SelectedKeys, false, false) &&
				(P.Kind != KingdomPolityLoadoutPolicyKind.StockPreserve ||
				 P.SelectedKeys.Count == 0);
		}

		private static bool ValidList(IList<string> Values, bool Required, bool Semantic)
		{
			if (Values == null || Values.Count > KingdomPolityRules.MaxRefs ||
				(Required && Values.Count == 0)) return false;
			string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				string value = Values[i];
				if ((Semantic ? !KingdomPolityRules.SemanticId(value) :
					!KingdomPolityRules.Text(value, true)) ||
					(previous != null && string.CompareOrdinal(previous, value) >= 0)) return false;
				previous = value;
			}
			return true;
		}

		private static bool Contains(IList<string> Values, string Value)
		{
			for (int i = 0; Values != null && i < Values.Count; i++)
				if (string.Equals(Values[i], Value, StringComparison.Ordinal)) return true;
			return false;
		}
	}
}
