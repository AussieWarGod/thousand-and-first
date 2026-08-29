using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
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
			return ActivationDigest("polity-profile-expression-v1", values);
		}

		private static void AppendProfileValues(List<string> Target, string Label,
			IList<string> Values)
		{
			Target.Add(Label + "#" + Values.Count.ToString(CultureInfo.InvariantCulture));
			for (int i = 0; i < Values.Count; i++) Target.Add(Values[i]);
		}
	}
}
