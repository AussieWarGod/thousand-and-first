using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Pure regenerated phenotype resolver; no world time, runtime hash, or random draw.</summary>
	public static class KingdomPolityNpcRules
	{
		public const int RulesVersion = 1;

		public static bool TryResolve(KingdomPolityProfileRevision Profile, string RoleKey,
			int Ordinal, out KingdomPolityNpcSpec Spec, out string Failure)
		{
			Spec = null; Failure = null;
			if (!ValidProfile(Profile) || !KingdomPolityRules.Text(RoleKey, true) ||
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
			int roleBonus = RoleBonus(RoleKey);
			int level = Math.Min(40, 1 + Profile.TechnologyBand * 3 + roleBonus + draw % 4);
			int baseline = Math.Min(28, 14 + Profile.TechnologyBand + level / 5);
			KingdomPolityNpcSpec result = new KingdomPolityNpcSpec
			{
				ResolverDigest = digest, ProfileId = Profile.ProfileId,
				ProfileRevision = Profile.Revision, RoleKey = RoleKey, Ordinal = Ordinal,
				TechnologyBand = Profile.TechnologyBand,
				BodyBlueprint = BodyBlueprint(bodyKey), Level = level,
				Strength = baseline, Agility = baseline, Toughness = baseline,
				Intelligence = baseline, Willpower = baseline, Ego = baseline,
				Hitpoints = 10 + level * 3
			};
			ApplyBodyStats(result, bodyKey); ApplyRoleStats(result);
			result.Skills = Skills(RoleKey, Profile.TechnologyBand);
			result.Mutations = Mutations(bodyKey, Profile.TechnologyBand, digest);
			result.GearBlueprints = Gear(Profile.TechnologyBand, RoleKey, bodyKey);
			Spec = result; return true;
		}

		private static bool ValidProfile(KingdomPolityProfileRevision P)
		{
			return P != null && KingdomPolityRules.TypedId(P.ProfileId, "taf:polity-profile:") &&
				P.Revision > 0 && KingdomPolityRules.SemanticId(P.PolityId) &&
				P.EffectiveTick >= 0L && P.RulesVersion == RulesVersion &&
				KingdomPolityRules.Digest(P.FactsDigest) && P.TechnologyBand >= 0 &&
				P.TechnologyBand <= 10 && ValidList(P.DerivedFromFactIds, true, true) &&
				ValidList(P.PracticeTags, false, false) && ValidBodies(P.BodyKeys) &&
				ValidRoles(P.RoleKeys) && ValidList(P.GearKeys, false, false) &&
				ValidLoadout(P.Loadout);
		}

		private static string BodyBlueprint(string BodyKey)
		{
			if (BodyKey == "snapjaw") return "Snapjaw Warrior";
			if (BodyKey == "goatfolk") return "Goatfolk";
			if (BodyKey == "dromad") return "Dromad";
			if (BodyKey == "hindren") return "HindrenVillager";
			if (BodyKey == "mechanical") return "Scrapbot";
			return "WatervineFarmer";
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

		private static List<string> Skills(string Role, int Technology)
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
				result.Add("Tactics_Run"); break;
			case "cook": result.Add("CookingAndGathering");
				result.Add("CookingAndGathering_MealPreparation"); break;
			}
			result.Sort(StringComparer.Ordinal); return result;
		}

		private static List<KingdomPolityMutationSpec> Mutations(string BodyKey,
			int Technology, string Digest)
		{
			List<KingdomPolityMutationSpec> result = new List<KingdomPolityMutationSpec>();
			if (BodyKey != "human") return result;
			int draw = int.Parse(Digest.Substring(2, 2), NumberStyles.HexNumber,
				CultureInfo.InvariantCulture);
			result.Add(new KingdomPolityMutationSpec
			{
				ClassName = draw % 2 == 0 ? "HeightenedHearing" : "NightVision",
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
					Values[i] != "mechanical") return false;
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
