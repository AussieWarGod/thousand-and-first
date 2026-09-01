#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The resolved-plan digest. It is what lets an attended run prove it is executing the plan
	/// that was stamped rather than a later request, so it must bind the exact parameter selection
	/// and every resolved argument.
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioPlanDigestTests
	{
		private const string Digest =
			"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

		/// <summary>Frozen by the launcher into the request, never by authored data.</summary>
		private const string Seed = "#4242";

		private static KingdomScenarioDefinition Sound(bool referenced)
		{
			KingdomScenarioDefinition definition = new KingdomScenarioDefinition
			{
				Key = "arch-gallery-slice",
				Family = "architecture",
				AuthorityClass = "architecture-stamper",
				SyntheticRaw = "false",
				DisplayName = "architecture gallery slice"
			};
			definition.Parameters.Add(new KingdomScenarioParameter
			{
				Name = "facing",
				Domain = new List<string> { "north", "east", "south", "west" }
			});
			KingdomScenarioStep prove =
				new KingdomScenarioStep { Verb = KingdomScenarioVerb.ProveCatalogue };
			prove.Arguments["Catalogue"] = "architecture";
			KingdomScenarioStep stage =
				new KingdomScenarioStep { Verb = KingdomScenarioVerb.StageGalleryCase };
			stage.Arguments["Suite"] = "architecture";
			// The exact expected case is frozen in authored data, so StageGalleryCase requires
			// Build, type, size, variant, and pose are the complete gallery identity.
			stage.Arguments["Build"] = "tent";
			stage.Arguments["Type"] = "housing";
			stage.Arguments["Size"] = "s";
			stage.Arguments["Variant"] = "fallback";
			stage.Arguments["Facing"] = referenced ? "{facing}" : "north";
			definition.Steps.Add(prove);
			definition.Steps.Add(stage);
			return definition;
		}

		private static KingdomScenarioPlan Plan(KingdomScenarioDefinition definition, string facing)
		{
			Dictionary<string, string> selection =
				new Dictionary<string, string>(StringComparer.Ordinal) { { "facing", facing } };
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsTrue(KingdomScenarioRules.TryPlan(definition, selection, Digest, Seed, out plan,
				out failure), failure);
			return plan;
		}

		[Test]
		public void PlanDigestIsAHexDigestAndIsStableForTheSameResolvedPlan()
		{
			string first = Plan(Sound(false), "north").PlanDigest;
			string second = Plan(Sound(false), "north").PlanDigest;
			Assert.AreEqual(64, first.Length);
			Assert.AreEqual(first, second);
		}

		/// <summary>A different bound selection is a different plan, even with identical verbs.</summary>
		[Test]
		public void PlanDigestChangesWithTheBoundSelection()
		{
			Assert.AreNotEqual(Plan(Sound(false), "north").PlanDigest,
				Plan(Sound(false), "east").PlanDigest);
		}

		/// <summary>A different resolved argument is a different plan.</summary>
		[Test]
		public void PlanDigestChangesWithAResolvedArgument()
		{
			string literal = Plan(Sound(false), "north").PlanDigest;
			string resolvedEast = Plan(Sound(true), "east").PlanDigest;
			Assert.AreNotEqual(literal, resolvedEast);
		}

		[Test]
		public void PlanDigestChangesWithTheExactLotEnvelope()
		{
			KingdomScenarioDefinition small = Sound(false);
			KingdomScenarioDefinition large = Sound(false);
			large.Steps[1].Arguments["Size"] = "l";
			Assert.AreNotEqual(Plan(small, "north").PlanDigest,
				Plan(large, "north").PlanDigest);
		}

		/// <summary>
		/// A reference that resolves to the same value as a literal is the same plan: the digest
		/// binds what will actually be executed, not how it was authored.
		/// </summary>
		[Test]
		public void PlanDigestIgnoresHowAnArgumentWasAuthoredOnceResolved()
		{
			Assert.AreEqual(Plan(Sound(false), "north").PlanDigest,
				Plan(Sound(true), "north").PlanDigest);
		}

		[Test]
		public void PlanDigestRefusesAMalformedPlanRatherThanThrowing()
		{
			Assert.IsNull(KingdomScenarioDigests.Plan(null));
			Assert.IsNull(KingdomScenarioDigests.Plan(new KingdomScenarioPlan { Key = "BAD KEY" }));
			KingdomScenarioPlan torn = Plan(Sound(false), "north");
			torn.Steps[0] = null;
			Assert.IsNull(KingdomScenarioDigests.Plan(torn));
		}
	}
}
#endif
