using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomRemovalProjectionRuntime
	{
		private const string PolityOwnerRealmProperty = "r_TAF_PolityOwnerRealm_v1";

		internal static bool TryInspectFactions(KingdomSystem System,
			IList<KingdomRemovalLocator> Locators,
			out List<Faction> Targets, out List<string> Rows, out string Failure)
		{
			Targets = new List<Faction>(); Rows = new List<string>(); Failure = null;
			if (System == null || string.IsNullOrEmpty(System.KingdomFactionName))
				return Fail("realm faction identity is absent", out Failure);
			HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> ground = Ground(Locators);
			foreach (Faction faction in Factions.GetList())
			{
				if (faction == null) continue;
				bool current = faction.Name == System.KingdomFactionName;
				bool polity = faction.GetStringProperty(PolityOwnerRealmProperty, null)
					== System.RealmId;
				if (!current && !polity) continue;
				if (string.IsNullOrEmpty(faction.Name) || !names.Add(faction.Name))
					return Fail("retirement faction identities are empty or duplicated", out Failure);
				Targets.Add(faction);
				if (FactionNeedsRetirement(faction, ground))
					Rows.Add(FactionPreviewRow(faction, ground));
			}
			if (!names.Contains(System.KingdomFactionName))
				return Fail("the live realm faction is absent from the native registry", out Failure);
			Rows.Sort(StringComparer.Ordinal);
			return true;
		}

		internal static bool TryRetireFactions(KingdomSystem System,
			IList<Faction> FactionsToRetire, IList<KingdomRemovalLocator> Locators,
			IList<string> FrozenRows, out string Failure)
		{
			Failure = null;
			if (!TryInspectFactions(System, Locators, out List<Faction> current,
				out List<string> rows, out Failure)
				|| !SameReferences(FactionsToRetire, current)
				|| KingdomRealmRemovalRetryRules.CutProgress(FrozenRows, rows, true)
					== KingdomRemovalCutProgress.Quarantine)
				return Fail(Failure ?? "realm faction changed after its exact preview", out Failure);
			HashSet<string> ground = Ground(Locators);
			for (int i = 0; i < (FactionsToRetire?.Count ?? 0); i++)
				RetireFaction(FactionsToRetire[i], ground);
			return TryInspectFactions(System, Locators, out current, out rows, out Failure)
				&& rows.Count == 0
				|| Fail(Failure ?? "realm faction remains active after inert conversion", out Failure);
		}

		private static void RetireFaction(Faction Faction, HashSet<string> Ground)
		{
			if (Faction == null) return;
			Faction.Visible = false;
			Faction.ExtradimensionalVersions = false;
			Faction.HatesPlayer = false;
			Faction.Pettable = false;
			Faction.WaterRitualLiquid = null;
			Faction.WaterRitualSkill = null;
			Faction.WaterRitualSkillCost = -1;
			Faction.WaterRitualBuyMostValuableItem = false;
			Faction.WaterRitualFungusInfect = -1;
			Faction.WaterRitualHermitOath = -1;
			Faction.WaterRitualSkillPointAmount = -1;
			Faction.WaterRitualSkillPointCost = -1;
			Faction.WaterRitualMutation = null;
			Faction.WaterRitualMutationCost = -1;
			Faction.WaterRitualGifts = null;
			Faction.WaterRitualItems = null;
			Faction.WaterRitualItemBlueprint = null;
			Faction.WaterRitualItemCost = -1;
			Faction.WaterRitualBlueprints = null;
			Faction.WaterRitualRecipe = null;
			Faction.WaterRitualRecipeText = null;
			Faction.WaterRitualRecipeGenotype = null;
			Faction.WaterRitualJoin = false;
			Faction.WaterRitualRandomMentalMutation = -1;
			Faction.WaterRitualAltBehaviorPart = null;
			Faction.WaterRitualAltBehaviorTag = null;
			Faction.WaterRitualAltLiquid = null;
			Faction.WaterRitualAltSkill = null;
			Faction.WaterRitualAltSkillCost = -1;
			Faction.WaterRitualAltGifts = null;
			Faction.WaterRitualAltItems = null;
			Faction.WaterRitualAltItemBlueprint = null;
			Faction.WaterRitualAltItemCost = -1;
			Faction.WaterRitualAltBlueprints = null;
			Faction.BuyTargetedSecrets = false;
			Faction.SellTargetedSecrets = false;
			RemoveFactionProperties(Faction);
			if (Faction.HolyPlaces != null)
				for (int i = Faction.HolyPlaces.Count - 1; i >= 0; i--)
					if (Ground.Contains(Faction.HolyPlaces[i])) Faction.HolyPlaces.RemoveAt(i);
		}

		private static void RemoveFactionProperties(Faction Faction)
		{
			List<string> keys = new List<string>();
			if (Faction.Properties != null) foreach (string key in Faction.Properties.Keys)
				if (KingdomRemovalCoverage.IsOwnedObjectProperty(key)) keys.Add(key);
			if (Faction.IntProperties != null) foreach (string key in Faction.IntProperties.Keys)
				if (KingdomRemovalCoverage.IsOwnedObjectProperty(key)) keys.Add(key);
			keys.Add("PlayerKingdom"); keys.Add("Village");
			for (int i = 0; i < keys.Count; i++) Faction.RemoveProperty(keys[i]);
		}

		private static bool FactionNeedsRetirement(Faction Faction, HashSet<string> Ground)
		{
			if (Faction == null) return false;
			if (Faction.Visible || Faction.ExtradimensionalVersions || Faction.HatesPlayer
				|| Faction.Pettable || Faction.WaterRitualLiquid != null
				|| Faction.WaterRitualSkill != null || Faction.WaterRitualSkillCost != -1
				|| Faction.WaterRitualBuyMostValuableItem
				|| Faction.WaterRitualFungusInfect != -1 || Faction.WaterRitualHermitOath != -1
				|| Faction.WaterRitualSkillPointAmount != -1
				|| Faction.WaterRitualSkillPointCost != -1 || Faction.WaterRitualMutation != null
				|| Faction.WaterRitualMutationCost != -1 || Faction.WaterRitualGifts != null
				|| Faction.WaterRitualItems != null || Faction.WaterRitualItemBlueprint != null
				|| Faction.WaterRitualItemCost != -1 || Faction.WaterRitualBlueprints != null
				|| Faction.WaterRitualRecipe != null || Faction.WaterRitualRecipeText != null
				|| Faction.WaterRitualRecipeGenotype != null || Faction.WaterRitualJoin
				|| Faction.WaterRitualRandomMentalMutation != -1
				|| Faction.WaterRitualAltBehaviorPart != null
				|| Faction.WaterRitualAltBehaviorTag != null
				|| Faction.WaterRitualAltLiquid != null || Faction.WaterRitualAltSkill != null
				|| Faction.WaterRitualAltSkillCost != -1 || Faction.WaterRitualAltGifts != null
				|| Faction.WaterRitualAltItems != null
				|| Faction.WaterRitualAltItemBlueprint != null
				|| Faction.WaterRitualAltItemCost != -1
				|| Faction.WaterRitualAltBlueprints != null
				|| Faction.BuyTargetedSecrets || Faction.SellTargetedSecrets
				|| Faction.HasProperty("PlayerKingdom") || Faction.HasProperty("Village")) return true;
			if (Faction.Properties != null) foreach (string key in Faction.Properties.Keys)
				if (KingdomRemovalCoverage.IsOwnedObjectProperty(key)) return true;
			if (Faction.IntProperties != null) foreach (string key in Faction.IntProperties.Keys)
				if (KingdomRemovalCoverage.IsOwnedObjectProperty(key)) return true;
			if (Faction.HolyPlaces != null) for (int i = 0; i < Faction.HolyPlaces.Count; i++)
				if (Ground.Contains(Faction.HolyPlaces[i])) return true;
			return false;
		}

		private static HashSet<string> Ground(IList<KingdomRemovalLocator> Locators)
		{
			HashSet<string> ground = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < (Locators?.Count ?? 0); i++)
				if (!string.IsNullOrEmpty(Locators[i]?.ZoneId)) ground.Add(Locators[i].ZoneId);
			return ground;
		}

		private static string FactionPreviewRow(Faction Faction, HashSet<string> Ground)
		{
			List<string> rows = new List<string>();
			rows.Add(R("Name", Faction.Name)); rows.Add(R("Visible", Faction.Visible));
			rows.Add(R("ExtradimensionalVersions", Faction.ExtradimensionalVersions));
			rows.Add(R("HatesPlayer", Faction.HatesPlayer)); rows.Add(R("Pettable", Faction.Pettable));
			rows.Add(R("WaterRitualLiquid", Faction.WaterRitualLiquid));
			rows.Add(R("WaterRitualSkill", Faction.WaterRitualSkill));
			rows.Add(R("WaterRitualSkillCost", Faction.WaterRitualSkillCost));
			rows.Add(R("WaterRitualBuyMostValuableItem", Faction.WaterRitualBuyMostValuableItem));
			rows.Add(R("WaterRitualFungusInfect", Faction.WaterRitualFungusInfect));
			rows.Add(R("WaterRitualHermitOath", Faction.WaterRitualHermitOath));
			rows.Add(R("WaterRitualSkillPointAmount", Faction.WaterRitualSkillPointAmount));
			rows.Add(R("WaterRitualSkillPointCost", Faction.WaterRitualSkillPointCost));
			rows.Add(R("WaterRitualMutation", Faction.WaterRitualMutation));
			rows.Add(R("WaterRitualMutationCost", Faction.WaterRitualMutationCost));
			rows.Add(R("WaterRitualGifts", Faction.WaterRitualGifts));
			rows.Add(R("WaterRitualItems", Faction.WaterRitualItems));
			rows.Add(R("WaterRitualItemBlueprint", Faction.WaterRitualItemBlueprint));
			rows.Add(R("WaterRitualItemCost", Faction.WaterRitualItemCost));
			rows.Add(R("WaterRitualBlueprints", Faction.WaterRitualBlueprints));
			rows.Add(R("WaterRitualRecipe", Faction.WaterRitualRecipe));
			rows.Add(R("WaterRitualRecipeText", Faction.WaterRitualRecipeText));
			rows.Add(R("WaterRitualRecipeGenotype", Faction.WaterRitualRecipeGenotype));
			rows.Add(R("WaterRitualJoin", Faction.WaterRitualJoin));
			rows.Add(R("WaterRitualRandomMentalMutation", Faction.WaterRitualRandomMentalMutation));
			rows.Add(R("WaterRitualAltBehaviorPart", Faction.WaterRitualAltBehaviorPart));
			rows.Add(R("WaterRitualAltBehaviorTag", Faction.WaterRitualAltBehaviorTag));
			rows.Add(R("WaterRitualAltLiquid", Faction.WaterRitualAltLiquid));
			rows.Add(R("WaterRitualAltSkill", Faction.WaterRitualAltSkill));
			rows.Add(R("WaterRitualAltSkillCost", Faction.WaterRitualAltSkillCost));
			rows.Add(R("WaterRitualAltGifts", Faction.WaterRitualAltGifts));
			rows.Add(R("WaterRitualAltItems", Faction.WaterRitualAltItems));
			rows.Add(R("WaterRitualAltItemBlueprint", Faction.WaterRitualAltItemBlueprint));
			rows.Add(R("WaterRitualAltItemCost", Faction.WaterRitualAltItemCost));
			rows.Add(R("WaterRitualAltBlueprints", Faction.WaterRitualAltBlueprints));
			rows.Add(R("BuyTargetedSecrets", Faction.BuyTargetedSecrets));
			rows.Add(R("SellTargetedSecrets", Faction.SellTargetedSecrets));
			List<string> properties = new List<string>();
			if (Faction.Properties != null) foreach (KeyValuePair<string, object> row in Faction.Properties)
				if (row.Key == "PlayerKingdom" || row.Key == "Village"
					|| KingdomRemovalCoverage.IsOwnedObjectProperty(row.Key))
					properties.Add(R(row.Key, row.Value));
			if (Faction.IntProperties != null) foreach (KeyValuePair<string, int> row in Faction.IntProperties)
				if (row.Key == "PlayerKingdom" || row.Key == "Village"
					|| KingdomRemovalCoverage.IsOwnedObjectProperty(row.Key))
					properties.Add(R(row.Key, row.Value));
			properties.Sort(StringComparer.Ordinal); rows.Add(R("Properties", properties));
			List<string> holy = new List<string>();
			if (Faction?.HolyPlaces != null) for (int i = 0; i < Faction.HolyPlaces.Count; i++)
				if (Ground.Contains(Faction.HolyPlaces[i])) holy.Add(Faction.HolyPlaces[i]);
			holy.Sort(StringComparer.Ordinal);
			rows.Add(R("HolyPlaces", holy));
			return string.Join("\u001e", rows.ToArray());
		}

		private static string R(string Name, object Value)
		{
			string value = C(Value);
			return Name.Length.ToString(CultureInfo.InvariantCulture) + ":" + Name + "="
				+ value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
		}

		private static string C(object Value)
		{
			if (Value == null) return "<null>";
			if (!(Value is string) && Value is IEnumerable values)
			{
				List<string> rows = new List<string>();
				foreach (object value in values) rows.Add(C(value));
				return string.Join("\u001d", rows.ToArray());
			}
			return Convert.ToString(Value, CultureInfo.InvariantCulture) ?? "<null>";
		}

		private static bool SameReferences(IList<Faction> A, IList<Faction> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			HashSet<Faction> seen = new HashSet<Faction>(A);
			return seen.Count == A.Count && seen.SetEquals(B);
		}
	}
}
