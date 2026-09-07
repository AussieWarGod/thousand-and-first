using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.DevTests
{
	public sealed partial class KingdomPolityNpcRulesTests
	{
		[Test]
		public void FullProfileExpressionIsPinnedIntoResolverDigest()
		{
			KingdomPolityProfileRevision profile = ExpressionProfile("style=common", "band=2");
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "guard", 0, 8, 11,
				out KingdomPolityNpcSpec before, out string failure), failure);
			profile.PracticeTags.Add("zz-new-practice");
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "guard", 0, 8, 11,
				out KingdomPolityNpcSpec after, out failure), failure);
			ClassicAssert.AreNotEqual(before.ResolverDigest, after.ResolverDigest);
			profile.BodyKeys.Add("unknown-body");
			ClassicAssert.IsFalse(KingdomPolityNpcRules.TryResolve(profile, "guard", 0, 8, 11,
				out KingdomPolityNpcSpec _, out failure));
		}

		[TestCase("style=moonstair")]
		[TestCase("style=gyre")]
		public void MoonStairStyleIsEnvironmentalAndDoesNotInventDoctrine(string Style)
		{
			KingdomPolityProfileFact fact = new KingdomPolityProfileFact
			{
				Kind = KingdomPolityProfileFactKind.Style,
				ValueKey = Style,
				SourceRef = "taf:source:test:style",
				FactId = "taf:fact:profile:test:style"
			};
			List<KingdomPolityExpressionCue> cues =
				KingdomPolityProfileExpressionCatalogue.Resolve(
					new List<KingdomPolityProfileFact> { fact }, 0);
			List<string> keys = cues.ConvertAll(cue => cue.ExpressionKey);
			CollectionAssert.Contains(keys, "moon-stair-crystal");
			CollectionAssert.Contains(keys, "warm-static-ground");
			ClassicAssert.IsFalse(cues.Exists(c => c.Kind == KingdomPolityExpressionKind.Skill));
			ClassicAssert.IsFalse(cues.Exists(c => c.Kind == KingdomPolityExpressionKind.Mutation));
			ClassicAssert.IsFalse(cues.Exists(c => c.Kind == KingdomPolityExpressionKind.Body));
			CollectionAssert.DoesNotContain(keys, "recovered-machine");
			CollectionAssert.DoesNotContain(keys, "Tinkering");
		}

		[Test]
		public void EveryStyleAndTechnologyBandStayOutOfBodyMutationAndSkillSurfaces()
		{
			string[] styles = { "style=common", "style=verdant", "style=fungal",
				"style=moonstair", "style=eater" };
			for (int band = 0; band <= 10; band++)
				for (int i = 0; i < styles.Length; i++)
				{
					List<KingdomPolityProfileFact> facts = new List<KingdomPolityProfileFact>
					{
						ExpressionFact(KingdomPolityProfileFactKind.Style, styles[i], "style"),
						ExpressionFact(KingdomPolityProfileFactKind.Technology,
							"band=" + band, "technology")
					};
					List<KingdomPolityExpressionCue> cues =
						KingdomPolityProfileExpressionCatalogue.Resolve(facts, band);
					ClassicAssert.IsFalse(cues.Exists(c => c.Kind == KingdomPolityExpressionKind.Body ||
						c.Kind == KingdomPolityExpressionKind.Mutation ||
						c.Kind == KingdomPolityExpressionKind.Skill ||
						c.Kind == KingdomPolityExpressionKind.Cybernetic ||
						c.Kind == KingdomPolityExpressionKind.Cargo), styles[i] + "/" + band);
				}
		}

		[Test]
		public void DeepExpressionRequiresExactCausalFactKindAndValue()
		{
			List<KingdomPolityProfileFact> facts = new List<KingdomPolityProfileFact>
			{
				ExpressionFact(KingdomPolityProfileFactKind.Practice,
					"mutation=PhotosyntheticSkin", "practice-mutation"),
				ExpressionFact(KingdomPolityProfileFactKind.Practice,
					"skill=Survival", "practice-skill"),
				ExpressionFact(KingdomPolityProfileFactKind.Transformation,
					"body=mechanical", "transformation")
			};
			List<KingdomPolityExpressionCue> cues =
				KingdomPolityProfileExpressionCatalogue.Resolve(facts, 0);
			ClassicAssert.IsTrue(cues.Exists(c => c.Kind == KingdomPolityExpressionKind.Body));
			ClassicAssert.IsTrue(cues.Exists(c => c.Kind == KingdomPolityExpressionKind.Mutation));
			ClassicAssert.IsTrue(cues.Exists(c => c.Kind == KingdomPolityExpressionKind.Skill));
			for (int i = 0; i < cues.Count; i++)
				ClassicAssert.IsTrue(KingdomPolityProfileExpressionCatalogue.CausallyAdmitted(cues[i]));

			KingdomPolityExpressionCue unproved = new KingdomPolityExpressionCue
			{
				Kind = KingdomPolityExpressionKind.Mutation,
				ExpressionKey = "PhotosyntheticSkin", Weight = 1,
				SourceKind = KingdomPolityProfileFactKind.Population,
				SourceValueKey = "style=verdant", SourceRef = "taf:source:test:style",
				ReasonFactId = "taf:fact:profile:test:unproved"
			};
			ClassicAssert.IsTrue(KingdomPolityProfileExpressionCatalogue.ValidCue(unproved));
			ClassicAssert.IsFalse(KingdomPolityProfileExpressionCatalogue.CausallyAdmitted(unproved));
		}
	}
}
