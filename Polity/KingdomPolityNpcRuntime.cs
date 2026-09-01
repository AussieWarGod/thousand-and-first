using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Creates one fresh unplaced body from a pinned profile; placement belongs to witness lanes.</summary>
	public static partial class KingdomPolityNpcRuntime
	{
		internal const string PolityProperty = "r_TAF_PolityActorSource_v1";
		internal const string ProfileProperty = "r_TAF_PolityActorProfile_v1";
		internal const string ResolverProperty = "r_TAF_PolityActorResolver_v1";
		internal const string RoleProperty = "r_TAF_PolityActorRole_v1";
		internal const string FigureProperty = "r_TAF_PolityActorFigure_v1";
		internal const string GearOwnerProperty = "r_TAF_PolityGearResolver_v1";
		internal const string GearReceiptProperty = "r_TAF_PolityGearReceipt_v1";
		internal const string GearRealmProperty = "r_TAF_PolityGearRealm_v1";
		internal const string GearCohortProperty = "r_TAF_PolityGearCohort_v1";
		internal const string GearProjectionProperty = "r_TAF_PolityGearProjection_v1";
		internal const string GearBodyProperty = "r_TAF_PolityGearBody_v1";
		internal const string GearProfileProperty = "r_TAF_PolityGearProfile_v1";
		internal const string GearMemberOrdinalProperty = "r_TAF_PolityGearMember_v1";
		internal const string GearOrdinalProperty = "r_TAF_PolityGearOrdinal_v1";
		internal const string ContestedProperty = "r_TAF_PolityPhysicalContested_v1";
		internal const string SignatureCueProperty = "r_TAF_PolitySignatureCues_v1";
		internal const string DialogueCueProperty = "r_TAF_PolityDialogueCues_v1";
		internal const string ExpressionReasonProperty = "r_TAF_PolityExpressionReasons_v1";

		public static bool TryCreate(KingdomPolityProfileRevision Profile, string RoleKey,
			int Ordinal, int ResolverRulesVersion, int MinimumLevel, int MaximumLevel,
			string FactionId, string FigureId, string DisplayName, string RealmId,
			string CohortId, string ProjectionId, string BodyId, Action<GameObject> FreezeBody,
			out GameObject Body, out string Failure)
		{
			Body = null; Failure = null;
			if (!KingdomPolityRules.SemanticId(FactionId) || Factions.GetIfExists(FactionId) == null ||
				!KingdomPolityRules.TypedId(RealmId, "taf:realm:") ||
				!KingdomPolityRules.TypedId(CohortId, "taf:cohort:") ||
				!KingdomPolityRules.TypedId(ProjectionId, "taf:projection:") ||
				!KingdomPolityRules.TypedId(BodyId, "taf:object:") || FreezeBody == null ||
				(!string.IsNullOrEmpty(FigureId) &&
				 !KingdomPolityRules.TypedId(FigureId, "taf:figure:")) ||
				(!string.IsNullOrEmpty(FigureId) &&
				 !KingdomPolityRules.Text(DisplayName, true)) ||
				!KingdomPolityNpcRules.TryResolvePinned(Profile, RoleKey, Ordinal,
					ResolverRulesVersion, MinimumLevel, MaximumLevel,
					out KingdomPolityNpcSpec spec, out Failure))
			{
				if (Failure == null) Failure = "regenerated actor input is invalid"; return false;
			}
			GameObject created = null;
			try
			{
					created = GameObject.Create(spec.BodyBlueprint);
				if (!GameObject.Validate(created) || created.Blueprint != spec.BodyBlueprint ||
					created.CurrentCell != null || created.Brain == null)
					return FailAndDestroy(ref created, "fresh polity body blueprint was not exact", out Failure);
					if (!ClearGeneratedLoadout(created, out Failure))
						return FailAndDestroy(ref created, Failure, out Failure);
					if (!TryFindResidentGear(BodyId, out GameObject collision,
						out Failure)) return FailAndDestroy(ref created, Failure, out Failure);
					if (GameObject.Validate(collision) && !ReferenceEquals(collision, created))
						return FailAndDestroy(ref created,
							"prepared polity body id already resolves to another object", out Failure);
					FreezeBody(created);
					if (created.IDIfAssigned != BodyId)
						return FailAndDestroy(ref created,
							"prepared polity body did not accept its frozen identity", out Failure);
				// Optional regenerated bodies are presentation, never a renewable corpse,
				// dismemberment, inventory-drop, or trade-value source.
				created.SetIntProperty("SuppressCorpseDrops", 1);
				created.SetIntProperty("NoXP", 1);
				Commerce bodyCommerce = created.GetPart<Commerce>();
				if (bodyCommerce != null) bodyCommerce.Value = 0.0;
				created.Brain.Factions = FactionId + "-100";
				created.Brain.Allegiance.Hostile = false;
				created.RequirePart<NoXPGain>();
				created.SetStringProperty(PolityProperty, Profile.PolityId);
				created.SetStringProperty(ProfileProperty, Profile.ProfileId + ":" +
					Profile.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture));
				created.SetStringProperty(ResolverProperty, spec.ResolverDigest);
				created.SetStringProperty(RoleProperty, spec.RoleKey);
				if (spec.SignatureCues.Count > 0) created.SetStringProperty(
					SignatureCueProperty, string.Join("|", spec.SignatureCues.ToArray()));
				if (spec.DialogueCues.Count > 0) created.SetStringProperty(
					DialogueCueProperty, string.Join("|", spec.DialogueCues.ToArray()));
				if (spec.ReasonFactIds.Count > 0) created.SetStringProperty(
					ExpressionReasonProperty, string.Join("|", spec.ReasonFactIds.ToArray()));
				if (!string.IsNullOrEmpty(FigureId))
				{
					created.SetStringProperty(FigureProperty, FigureId);
					created.GiveProperName(DisplayName, Force: true);
				}
					if (!ApplyStats(created, spec, out Failure) ||
						!ApplySkillsAndMutations(created, spec, out Failure) ||
						!ApplyGear(created, spec, RealmId, CohortId, ProjectionId,
							BodyId, out Failure))
					return FailAndDestroy(ref created, Failure, out Failure);
				if (created.CurrentCell != null)
					return FailAndDestroy(ref created,
						"regenerated actor was placed outside its witness adapter", out Failure);
				Body = created; return true;
			}
			catch (Exception ex)
			{
				return FailAndDestroy(ref created,
					"regenerated actor creation failed: " + ex.Message, out Failure);
			}
		}

		private static bool ApplyStats(GameObject Body, KingdomPolityNpcSpec S, out string Failure)
		{
			Failure = null;
			if (S.ProfileRulesVersion == KingdomPolityProfileRules.RulesVersion)
				return SetStat(Body, "Level", S.Level) ||
					KingdomPolityRules.Fail("regenerated actor lacks a level statistic", out Failure);
			return SetStat(Body, "Level", S.Level) && SetStat(Body, "Strength", S.Strength) &&
				SetStat(Body, "Agility", S.Agility) && SetStat(Body, "Toughness", S.Toughness) &&
				SetStat(Body, "Intelligence", S.Intelligence) &&
				SetStat(Body, "Willpower", S.Willpower) && SetStat(Body, "Ego", S.Ego) &&
				SetStat(Body, "Hitpoints", S.Hitpoints) ||
				KingdomPolityRules.Fail("regenerated actor lacks required statistics", out Failure);
		}

		private static bool SetStat(GameObject Body, string Name, int Value)
		{
			Statistic stat = Body.GetStat(Name); if (stat == null) return false;
			stat.BaseValue = Value; return stat.BaseValue == Value;
		}

		private static bool ApplySkillsAndMutations(GameObject Body, KingdomPolityNpcSpec S,
			out string Failure)
		{
			Failure = null;
			for (int i = 0; i < S.Skills.Count; i++)
			{
				string skill = S.ProfileRulesVersion == KingdomPolityProfileRules.LegacyRulesVersion &&
					S.Skills[i] == "Tactics_Run" ? "Tactics_Hurdle" : S.Skills[i];
				if (!Body.HasPart(skill) && Body.AddSkill(skill) == null)
					return KingdomPolityRules.Fail("regenerated actor skill was unavailable", out Failure);
			}
			Mutations mutations = S.Mutations.Count == 0 ? Body.GetPart<Mutations>() :
				Body.RequirePart<Mutations>();
			for (int i = 0; i < S.Mutations.Count; i++)
			{
				KingdomPolityMutationSpec mutation = S.Mutations[i];
				string className = S.ProfileRulesVersion ==
					KingdomPolityProfileRules.LegacyRulesVersion && mutation.ClassName ==
					"NightVision" ? "DarkVision" : mutation.ClassName;
				if (mutations.HasMutation(className)) continue;
				if (mutations.AddMutation(className, mutation.Level) < 0)
					return KingdomPolityRules.Fail("regenerated actor mutation was unavailable", out Failure);
			}
			return true;
		}

		private static bool ClearGeneratedLoadout(GameObject Body, out string Failure)
		{
			Failure = null;
			List<GameObject> inherited = Body.GetInventoryDirectAndEquipment();
			for (int i = 0; inherited != null && i < inherited.Count; i++)
			{
				GameObject item = inherited[i];
				if (!GameObject.Validate(item) || item.IsNatural()) continue;
				if (!item.ForceUnequipAndRemove(Silent: true) ||
					!item.Obliterate(null, Silent: true) || GameObject.Validate(item))
					return KingdomPolityRules.Fail(
						"fresh polity body's inherited loadout could not be cleared", out Failure);
			}
			return true;
		}

		private static bool FailAndDestroy(ref GameObject Body, string Reason, out string Failure)
		{
			Failure = string.IsNullOrEmpty(Reason) ? "regenerated actor was refused" : Reason;
			GameObject failed = Body;
			if (GameObject.Validate(failed)) failed.SetIntProperty(ContestedProperty, 1);
			try
			{
				if (CanDestroyFailedBody(failed)) failed.Obliterate(null, Silent: true);
			}
			catch (Exception) { }
			Body = GameObject.Validate(failed) ? failed : null; return false;
		}
	}
}
