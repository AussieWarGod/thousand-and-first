using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		/// <summary>
		/// Seal-safe commitment to only the phenotype actually projected across runs. It excludes
		/// polity/profile ids, ticks, fact ids, source refs, and every other identity correlator.
		/// </summary>
		internal static string LegacySealPhenotypeDigest(KingdomPolityProfileRevision Profile)
		{
			if (Profile == null) return null;
			List<string> values = new List<string>
			{
				"schema=" + KingdomPolityProfileRules.CurrentLegacyProfileSchema.ToString(
					CultureInfo.InvariantCulture),
				"rules=" + Profile.RulesVersion.ToString(CultureInfo.InvariantCulture),
				"technology=" + Profile.TechnologyBand.ToString(CultureInfo.InvariantCulture)
			};
			AppendProfileValues(values, "bodies", Profile.BodyKeys);
			return ActivationDigest("polity-profile-seal-phenotype-v1", values);
		}

		/// <summary>Commits every immutable phenotype field, not only its source-fact digest.</summary>
		internal static string ProfileExpressionDigest(KingdomPolityProfileRevision Profile)
		{
			List<string> values = new List<string> { Profile.ProfileId,
				Profile.Revision.ToString(CultureInfo.InvariantCulture), Profile.PolityId,
				Profile.EffectiveTick.ToString(CultureInfo.InvariantCulture),
				Profile.RulesVersion.ToString(CultureInfo.InvariantCulture), Profile.FactsDigest,
				Profile.TechnologyBand.ToString(CultureInfo.InvariantCulture) };
			AppendProfileValues(values, "facts", Profile.DerivedFromFactIds);
			AppendProfileValues(values, "practices", Profile.PracticeTags);
			AppendProfileValues(values, "bodies", Profile.BodyKeys);
			AppendProfileValues(values, "roles", Profile.RoleKeys);
			AppendProfileValues(values, "gear", Profile.GearKeys);
			values.Add("loadout-kind=" + (int)Profile.Loadout.Kind);
			values.Add("loadout-budget=" + Profile.Loadout.ExpectedValueBudget.ToString(
				CultureInfo.InvariantCulture));
			AppendProfileValues(values, "excluded", Profile.Loadout.ExcludedKeys);
			AppendProfileValues(values, "selected", Profile.Loadout.SelectedKeys);
			if (Profile.RulesVersion == KingdomPolityProfileRules.LegacyRulesVersion)
				return ActivationDigest("polity-profile-expression-v1", values);
			int catalogue = Profile.RulesVersion ==
				KingdomPolityProfileRules.PriorExpressionRulesVersion ? 1 :
				KingdomPolityProfileExpressionCatalogue.CatalogueVersion;
			values.Add("catalogue=" + catalogue.ToString(CultureInfo.InvariantCulture));
			values.Add("cues#" + Profile.ExpressionCues.Count.ToString(CultureInfo.InvariantCulture));
			for (int i = 0; i < Profile.ExpressionCues.Count; i++)
			{
				KingdomPolityExpressionCue cue = Profile.ExpressionCues[i];
				values.Add(((byte)cue.Kind).ToString(CultureInfo.InvariantCulture));
				values.Add(cue.ExpressionKey); values.Add(cue.Weight.ToString(CultureInfo.InvariantCulture));
				values.Add(((byte)cue.SourceKind).ToString(CultureInfo.InvariantCulture));
				values.Add(cue.SourceValueKey); values.Add(cue.SourceRef); values.Add(cue.ReasonFactId);
			}
			return ActivationDigest(Profile.RulesVersion ==
				KingdomPolityProfileRules.PriorExpressionRulesVersion ?
				"polity-profile-expression-v2" : "polity-profile-expression-v3", values);
		}

		private static void AppendProfileValues(List<string> Target, string Label,
			IList<string> Values)
		{
			Target.Add(Label + "#" + Values.Count.ToString(CultureInfo.InvariantCulture));
			for (int i = 0; i < Values.Count; i++) Target.Add(Values[i]);
		}
	}
}
