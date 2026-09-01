using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityNpcRules
	{
		internal static bool TryResolvePinned(KingdomPolityProfileRevision Profile,
			string RoleKey, int Ordinal, int ResolverRulesVersion,
			int MinimumLevel, int MaximumLevel, out KingdomPolityNpcSpec Spec,
			out string Failure)
		{
			if (ResolverRulesVersion == 1)
				return TryResolve(Profile, RoleKey, Ordinal, out Spec, out Failure);
			if (ResolverRulesVersion == KingdomPolityLoadoutCatalogue.PriorResolverVersion)
				return TryResolveV2(Profile, RoleKey, Ordinal, MinimumLevel, MaximumLevel,
					out Spec, out Failure);
			if (ResolverRulesVersion == RulesVersion)
				return TryResolve(Profile, RoleKey, Ordinal, MinimumLevel, MaximumLevel,
					out Spec, out Failure);
			Spec = null; Failure = "cohort pins an unknown NPC resolver version"; return false;
		}

		private static void ApplyExpression(KingdomPolityProfileRevision Profile,
			KingdomPolityNpcSpec Spec, string Digest, bool PriorBehavior = false,
			bool CommittedLoadout = false)
		{
			for (KingdomPolityExpressionKind kind = KingdomPolityExpressionKind.Body;
				kind <= KingdomPolityExpressionKind.Dialogue; kind++)
			{
				KingdomPolityExpressionCue cue = Choose(Profile.ExpressionCues, kind,
					Draw(Digest, (int)kind));
				if (cue == null) continue;
				// Resolver-v3's immutable SelectedKeys already own physical equipment. A
				// presentation cue may explain that policy, but cannot replace one item after
				// budget/exclusion validation and thereby make a valid profile unreachable.
				if (CommittedLoadout && cue.Kind == KingdomPolityExpressionKind.Gear) continue;
				ApplyCue(Spec, cue, PriorBehavior);
				AddUnique(Spec.ReasonFactIds, cue.ReasonFactId);
			}
			for (int i = 0; i < Spec.CargoBlueprints.Count; i++)
				AddUnique(Spec.GearBlueprints, Spec.CargoBlueprints[i]);
			Spec.Skills.Sort(StringComparer.Ordinal);
			Spec.GearBlueprints.Sort(StringComparer.Ordinal);
			Spec.CargoBlueprints.Sort(StringComparer.Ordinal);
			Spec.SignatureCues.Sort(StringComparer.Ordinal);
			Spec.DialogueCues.Sort(StringComparer.Ordinal);
			Spec.ReasonFactIds.Sort(StringComparer.Ordinal);
		}

		private static void ApplyCue(KingdomPolityNpcSpec S, KingdomPolityExpressionCue C,
			bool PriorBehavior)
		{
			switch (C.Kind)
			{
			case KingdomPolityExpressionKind.Body:
				if (C.ExpressionKey == "mechanical")
				{
					S.BodyBlueprint = BodyBlueprint("mechanical"); S.GearBlueprints.Clear();
					S.CargoBlueprints.Clear(); S.Mutations.Clear();
					if (PriorBehavior) { S.Strength += 3; S.Toughness += 4; }
				}
				break;
			case KingdomPolityExpressionKind.Role:
				if (PriorBehavior && C.ExpressionKey == S.RoleKey)
					{ S.Willpower += 1; S.Hitpoints += 3; }
				AddUnique(S.SignatureCues, "role-affinity:" + C.ExpressionKey); break;
			case KingdomPolityExpressionKind.Skill:
				AddUnique(S.Skills, C.ExpressionKey); break;
			case KingdomPolityExpressionKind.Mutation:
				if (S.BodyBlueprint == BodyBlueprint("human")) AddMutation(S, C.ExpressionKey); break;
			case KingdomPolityExpressionKind.Cybernetic:
				AddUnique(S.SignatureCues, C.ExpressionKey); break;
			case KingdomPolityExpressionKind.Gear:
				if (S.BodyBlueprint != BodyBlueprint("mechanical")) ReplaceWeapon(S, C.ExpressionKey); break;
			case KingdomPolityExpressionKind.Signature:
				AddUnique(S.SignatureCues, C.ExpressionKey); break;
			case KingdomPolityExpressionKind.Cargo:
				if (CarriesCargo(S.RoleKey) && S.BodyBlueprint != BodyBlueprint("mechanical"))
					AddUnique(S.CargoBlueprints, C.ExpressionKey);
				break;
			case KingdomPolityExpressionKind.Dialogue:
				AddUnique(S.DialogueCues, C.ExpressionKey); break;
			}
		}

		private static KingdomPolityExpressionCue Choose(IList<KingdomPolityExpressionCue> Cues,
			KingdomPolityExpressionKind Kind, int Draw)
		{
			int total = 0;
			for (int i = 0; i < Cues.Count; i++) if (Cues[i].Kind == Kind) total += Cues[i].Weight;
			if (total == 0) return null;
			int selected = Draw % total;
			for (int i = 0; i < Cues.Count; i++)
				if (Cues[i].Kind == Kind && (selected -= Cues[i].Weight) < 0) return Cues[i];
			return null;
		}

		private static int Draw(string Digest, int Lane)
		{
			int at = (Lane * 6) % (Digest.Length - 4);
			return int.Parse(Digest.Substring(at, 4), NumberStyles.HexNumber,
				CultureInfo.InvariantCulture);
		}

		private static void AddMutation(KingdomPolityNpcSpec S, string ClassName)
		{
			for (int i = 0; i < S.Mutations.Count; i++)
				if (S.Mutations[i].ClassName == ClassName) return;
			S.Mutations.Add(new KingdomPolityMutationSpec { ClassName = ClassName,
				Level = Math.Min(3, 1 + S.TechnologyBand / 4) });
		}

		private static void ReplaceWeapon(KingdomPolityNpcSpec S, string Blueprint)
		{
			for (int i = S.GearBlueprints.Count - 1; i >= 0; i--)
				if (Weapon(S.GearBlueprints[i])) S.GearBlueprints.RemoveAt(i);
			AddUnique(S.GearBlueprints, Blueprint);
		}

		private static bool Weapon(string Value)
		{
			return Value == "Club" || Value == "Long Sword" || Value == "Long Sword2" ||
				Value == "Steel Long Sword" || Value == "Long Sword3";
		}

		private static bool CarriesCargo(string Role)
		{
			return Role == "trader" || Role == "courier" || Role == "migrant" || Role == "envoy";
		}

		private static void AddUnique(List<string> Values, string Value)
		{
			for (int i = 0; i < Values.Count; i++) if (Values[i] == Value) return;
			Values.Add(Value);
		}
	}
}
