using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	public sealed partial class KingdomPolityNpcRulesTests
	{
		[Test]
		public void FullProfileExpressionIsPinnedIntoResolverDigest()
		{
			KingdomPolityProfileRevision profile = ExpressionProfile("style=common", "band=2");
			Assert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "guard", 0, 8, 11,
				out KingdomPolityNpcSpec before, out string failure), failure);
			profile.PracticeTags.Add("zz-new-practice");
			Assert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "guard", 0, 8, 11,
				out KingdomPolityNpcSpec after, out failure), failure);
			Assert.AreNotEqual(before.ResolverDigest, after.ResolverDigest);
			profile.BodyKeys.Add("unknown-body");
			Assert.IsFalse(KingdomPolityNpcRules.TryResolve(profile, "guard", 0, 8, 11,
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
			Assert.IsFalse(cues.Exists(c => c.Kind == KingdomPolityExpressionKind.Skill));
			Assert.IsFalse(cues.Exists(c => c.Kind == KingdomPolityExpressionKind.Mutation));
			Assert.IsFalse(cues.Exists(c => c.Kind == KingdomPolityExpressionKind.Body));
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
					Assert.IsFalse(cues.Exists(c => c.Kind == KingdomPolityExpressionKind.Body ||
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
			Assert.IsTrue(cues.Exists(c => c.Kind == KingdomPolityExpressionKind.Body));
			Assert.IsTrue(cues.Exists(c => c.Kind == KingdomPolityExpressionKind.Mutation));
			Assert.IsTrue(cues.Exists(c => c.Kind == KingdomPolityExpressionKind.Skill));
			for (int i = 0; i < cues.Count; i++)
				Assert.IsTrue(KingdomPolityProfileExpressionCatalogue.CausallyAdmitted(cues[i]));

			KingdomPolityExpressionCue unproved = new KingdomPolityExpressionCue
			{
				Kind = KingdomPolityExpressionKind.Mutation,
				ExpressionKey = "PhotosyntheticSkin", Weight = 1,
				SourceKind = KingdomPolityProfileFactKind.Population,
				SourceValueKey = "style=verdant", SourceRef = "taf:source:test:style",
				ReasonFactId = "taf:fact:profile:test:unproved"
			};
			Assert.IsTrue(KingdomPolityProfileExpressionCatalogue.ValidCue(unproved));
			Assert.IsFalse(KingdomPolityProfileExpressionCatalogue.CausallyAdmitted(unproved));
		}
	}
}
