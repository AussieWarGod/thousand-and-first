using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityNpcRules
	{
		/// <summary>Current resolver: the profile's immutable loadout policy owns every item.</summary>
		public static bool TryResolve(KingdomPolityProfileRevision Profile, string RoleKey,
			int Ordinal, int MinimumLevel, int MaximumLevel,
			out KingdomPolityNpcSpec Spec, out string Failure)
		{
			Spec = null; Failure = null;
			if (!ValidProfile(Profile, true) || !KingdomPolityRules.Text(RoleKey, true) ||
				!Contains(Profile.RoleKeys, RoleKey) || Ordinal < 0 || Ordinal > 1023 ||
				MinimumLevel < 1 || MaximumLevel < MinimumLevel ||
				MaximumLevel > KingdomPolityRules.MaxLevel)
			{
				Failure =
					"current polity NPC resolver lacks a legal pinned profile, role, or level band";
				return false;
			}
			string expression = KingdomPolityRules.ProfileExpressionDigest(Profile);
			string digest = KingdomPolityRules.ActivationDigest("polity-npc-spec-v3",
				KingdomPolityLoadoutCatalogue.CatalogueVersion.ToString(
					CultureInfo.InvariantCulture), Profile.ProfileId,
				Profile.Revision.ToString(CultureInfo.InvariantCulture), expression, RoleKey,
				Ordinal.ToString(CultureInfo.InvariantCulture),
				MinimumLevel.ToString(CultureInfo.InvariantCulture),
				MaximumLevel.ToString(CultureInfo.InvariantCulture));
			int draw = int.Parse(digest.Substring(0, 2), NumberStyles.HexNumber,
				CultureInfo.InvariantCulture);
			string bodyKey = Profile.BodyKeys[draw % Profile.BodyKeys.Count];
			string bodyBlueprint = BodyBlueprint(bodyKey);
			if (bodyBlueprint == null)
			{
				Failure = "frozen polity profile has no admissible manifested body"; return false;
			}
			// Body admissibility answers first; only a manifestable body ever consults
			// the committed loadout catalogue.
			if (!KingdomPolityLoadoutCatalogue.ExactCurrentPolicy(Profile, out Failure))
				return false;
			if (!TryPolicyGear(Profile, RoleKey, bodyKey,
				out List<string> gear, out Failure)) return false;
			KingdomPolityNpcSpec result = new KingdomPolityNpcSpec
			{
				ResolverDigest = digest, ProfileId = Profile.ProfileId,
				ProfileRevision = Profile.Revision, ProfileRulesVersion = Profile.RulesVersion,
				RoleKey = RoleKey, Ordinal = Ordinal, TechnologyBand = Profile.TechnologyBand,
				BodyBlueprint = bodyBlueprint,
				Level = MinimumLevel + draw % (MaximumLevel - MinimumLevel + 1),
				Skills = Skills(RoleKey, 0, false),
				Mutations = new List<KingdomPolityMutationSpec>(), GearBlueprints = gear
			};
			ApplyExpression(Profile, result, digest, CommittedLoadout: true);
			if (!ResolvedPolicyGear(Profile, result, out Failure)) return false;
			Spec = result; return true;
		}

		private static bool TryPolicyGear(KingdomPolityProfileRevision Profile, string Role,
			string BodyKey, out List<string> Gear, out string Failure)
		{
			Gear = new List<string>(); Failure = null;
			if (BodyKey == "mechanical") return true;
			for (int i = 0; i < Profile.Loadout.SelectedKeys.Count; i++)
			{
				if (!KingdomPolityLoadoutCatalogue.TryEntry(Profile.Loadout.SelectedKeys[i],
					out string blueprint, out _, out KingdomPolityLoadoutSlot slot))
				{
					Failure = "current polity loadout contains unknown selected gear"; return false;
				}
				if (KingdomPolityLoadoutCatalogue.RoleUses(slot, Role)) Gear.Add(blueprint);
			}
			Gear.Sort(StringComparer.Ordinal); return true;
		}

		private static bool ResolvedPolicyGear(KingdomPolityProfileRevision Profile,
			KingdomPolityNpcSpec Spec, out string Failure)
		{
			Failure = null;
			if (Spec.BodyBlueprint == BodyBlueprint("mechanical"))
				return Spec.GearBlueprints.Count == 0 || FailPolicy(out Failure);
			if (!TryPolicyGear(Profile, Spec.RoleKey, "human",
				out List<string> expected, out Failure)) return false;
			List<string> actual = new List<string>();
			for (int i = 0; i < Spec.GearBlueprints.Count; i++)
			{
				string item = Spec.GearBlueprints[i]; bool cargo = false;
				for (int j = 0; j < Spec.CargoBlueprints.Count; j++)
					if (Spec.CargoBlueprints[j] == item) { cargo = true; break; }
				if (!cargo) actual.Add(item);
			}
			if (actual.Count != expected.Count) return FailPolicy(out Failure);
			for (int i = 0; i < actual.Count; i++)
				if (actual[i] != expected[i]) return FailPolicy(out Failure);
			return true;
		}

		private static bool FailPolicy(out string Failure)
		{
			Failure = "resolved polity gear diverges from its committed loadout policy";
			return false;
		}
	}
}
