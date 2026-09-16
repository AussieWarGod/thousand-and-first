using System;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomRecruitmentBodyWitness
	{
		internal static void Verify(GameObject Body, string Blueprint, string Origin,
			Action<bool, string> Require)
		{
			Require(GameObject.Validate(Body) && Body.Blueprint == Blueprint
				&& Body.Body != null && Body.Brain != null, "recruited native body differs from its frozen blueprint");
			var authored = GameObjectFactory.Factory.Blueprints[Blueprint];
			Require(authored.GetTag(KingdomRecruitment.OriginTag, null) == Origin,
				"recruited origin differs from its native profile");
			string culture = authored.GetPropertyOrTag("Culture"), species = authored.GetPropertyOrTag("Species");
			Require((culture == null || Body.GetCulture() == culture)
				&& (species == null || Body.GetSpecies() == species), "recruited culture/species changed");
			if (authored.GetTag(KingdomRecruitment.FactionTag, null) == "Hindren")
				Require(Body.HasPart("MultipleLegs"), "recruited hindren lost native leg mutation");
		}
	}
}
