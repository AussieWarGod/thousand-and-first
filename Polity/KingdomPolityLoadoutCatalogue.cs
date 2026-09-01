using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal enum KingdomPolityLoadoutSlot : byte
	{
		Weapon = 1,
		Armor = 2,
		Shield = 3
	}

	/// <summary>Immutable semantic-to-Qud loadout catalogue for current resolver contracts.</summary>
	internal static class KingdomPolityLoadoutCatalogue
	{
		internal const int CatalogueVersion = 1;
		internal const int PriorResolverVersion = 2;

		internal static List<string> KeysForTechnology(int Technology)
		{
			if (Technology <= 0)
				return new List<string> { "club", "leather-armor", "wooden-buckler" };
			if (Technology <= 2)
				return new List<string> { "bronze-sword", "leather-armor", "wooden-buckler" };
			if (Technology <= 4)
				return new List<string> { "chain-mail", "iron-sword", "wooden-buckler" };
			if (Technology <= 6)
				return new List<string> { "chain-mail", "steel-sword", "wooden-buckler" };
			return new List<string> { "carbide-plate", "carbide-sword", "wooden-buckler" };
		}

		internal static bool TryEntry(string Key, out string Blueprint,
			out int Cost, out KingdomPolityLoadoutSlot Slot)
		{
			Blueprint = null; Cost = 0; Slot = 0;
			switch (Key)
			{
			case "club": Blueprint = "Club"; Cost = 10;
				Slot = KingdomPolityLoadoutSlot.Weapon; break;
			case "bronze-sword": Blueprint = "Long Sword"; Cost = 100;
				Slot = KingdomPolityLoadoutSlot.Weapon; break;
			case "iron-sword": Blueprint = "Long Sword2"; Cost = 200;
				Slot = KingdomPolityLoadoutSlot.Weapon; break;
			case "steel-sword": Blueprint = "Steel Long Sword"; Cost = 350;
				Slot = KingdomPolityLoadoutSlot.Weapon; break;
			case "carbide-sword": Blueprint = "Long Sword3"; Cost = 500;
				Slot = KingdomPolityLoadoutSlot.Weapon; break;
			case "leather-armor": Blueprint = "Leather Armor"; Cost = 20;
				Slot = KingdomPolityLoadoutSlot.Armor; break;
			case "chain-mail": Blueprint = "Chain Mail"; Cost = 150;
				Slot = KingdomPolityLoadoutSlot.Armor; break;
			case "carbide-plate": Blueprint = "Carbide Plate Armor"; Cost = 350;
				Slot = KingdomPolityLoadoutSlot.Armor; break;
			case "wooden-buckler": Blueprint = "Wooden Buckler"; Cost = 20;
				Slot = KingdomPolityLoadoutSlot.Shield; break;
			default: return false;
			}
			return true;
		}

		internal static bool RoleUses(KingdomPolityLoadoutSlot Slot, string Role)
		{
			return Slot != KingdomPolityLoadoutSlot.Shield || Role == "guard" ||
				Role == "patrol" || Role == "warband" || Role == "claimant";
		}

		internal static bool ExactCurrentPolicy(KingdomPolityProfileRevision Profile,
			out string Failure)
		{
			Failure = null;
			if (Profile?.Loadout == null || Profile.Loadout.Kind !=
				KingdomPolityLoadoutPolicyKind.OwnedReplace)
				return Fail("current polity loadout is not owned-replace", out Failure);
			List<string> expected = KeysForTechnology(Profile.TechnologyBand);
			if (!Same(expected, Profile.GearKeys) || !Same(expected, Profile.Loadout.SelectedKeys))
				return Fail("current polity loadout diverges from its technology catalogue",
					out Failure);
			if (!Has(Profile.Loadout.ExcludedKeys, "natural-gear") ||
				!Has(Profile.Loadout.ExcludedKeys, "quest") ||
				!Has(Profile.Loadout.ExcludedKeys, "relic") ||
				!Has(Profile.Loadout.ExcludedKeys, "trader-stock") ||
				!Has(Profile.Loadout.ExcludedKeys, "unique"))
				return Fail("current polity loadout lost a protected exclusion", out Failure);
			int cost = 0;
			for (int i = 0; i < expected.Count; i++)
			{
				if (!TryEntry(expected[i], out _, out int value, out _))
					return Fail("current polity loadout names unknown gear", out Failure);
				cost += value;
			}
			return cost <= Profile.Loadout.ExpectedValueBudget ||
				Fail("current polity loadout exceeds its committed budget", out Failure);
		}

		private static bool Same(IList<string> A, IList<string> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++) if (A[i] != B[i]) return false;
			return true;
		}

		private static bool Has(IList<string> Values, string Value)
		{
			for (int i = 0; Values != null && i < Values.Count; i++)
				if (Values[i] == Value) return true;
			return false;
		}

		private static bool Fail(string Reason, out string Failure)
		{
			Failure = Reason; return false;
		}
	}
}
