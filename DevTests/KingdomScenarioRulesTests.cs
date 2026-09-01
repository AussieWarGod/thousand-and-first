#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Adversarial execution of the untrusted scenario model. Every case here builds a malformed
	/// model directly, rather than through the registry, and calls the entry point: the rules must
	/// refuse or return null, never throw, and never quietly accept.
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioRulesTests
	{
		private const string Digest =
			"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

		/// <summary>Frozen by the launcher into the request, never by authored data.</summary>
		private const string Seed = "#4242";

		private static KingdomScenarioDefinition Sound()
		{
			KingdomScenarioDefinition definition = new KingdomScenarioDefinition
			{
				Key = "arch-gallery-slice",
				Family = "architecture",
				AuthorityClass = "architecture-stamper",
				SyntheticRaw = "false",
				AnchorId = null,
				DisplayName = "architecture gallery slice"
			};
			definition.Parameters.Add(new KingdomScenarioParameter
			{
				Name = "facing",
				Domain = new List<string> { "north", "east", "south", "west" }
			});
			definition.Steps.Add(Step(KingdomScenarioVerb.ProveCatalogue, "Catalogue", "architecture"));
			definition.Steps.Add(Stage("north"));
			return definition;
		}

		private static KingdomScenarioStep Step(KingdomScenarioVerb verb, string key, string value)
		{
			KingdomScenarioStep step = new KingdomScenarioStep { Verb = verb };
			if (key != null) step.Arguments[key] = value;
			return step;
		}

		/// <summary>The single mutating step, which the validator requires to be last.</summary>
		private static KingdomScenarioStep Stage(string facing)
		{
			KingdomScenarioStep step =
				new KingdomScenarioStep { Verb = KingdomScenarioVerb.StageGalleryCase };
			step.Arguments["Suite"] = "architecture";
			step.Arguments["Build"] = "tent";
			step.Arguments["Type"] = "housing";
			step.Arguments["Size"] = "s";
			step.Arguments["Variant"] = "fallback";
			step.Arguments["Facing"] = facing;
			return step;
		}

		private static Dictionary<string, string> Facing(string value)
		{
			return new Dictionary<string, string>(StringComparer.Ordinal) { { "facing", value } };
		}

		private static KingdomScenarioPlan Plan(KingdomScenarioDefinition definition,
			IDictionary<string, string> selection)
		{
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsTrue(KingdomScenarioRules.TryPlan(definition, selection, Digest, Seed,
				out plan, out failure), failure);
			return plan;
		}

		// ----- happy path -------------------------------------------------------------------

		[Test]
		public void SoundDefinitionPlansWithResolvedArgumentsAndAnExactVerbSequence()
		{
			KingdomScenarioPlan plan = Plan(Sound(), Facing("north"));
			Assert.AreEqual("provecatalogue+stagegallerycase", plan.Verbs);
			Assert.AreEqual(2, plan.Steps.Count);
			Assert.AreEqual("architecture", plan.Steps[0].Arguments["Catalogue"]);
			Assert.AreEqual("architecture", plan.Steps[1].Arguments["Suite"]);
			Assert.AreEqual("north", plan.Steps[1].Arguments["Facing"]);
			Assert.AreEqual("north", plan.Bindings["facing"]);
			Assert.IsFalse(plan.Synthetic);
		}

		[Test]
		public void SoundDefinitionHasNoValidationFindings()
		{
			Assert.IsEmpty(KingdomScenarioRules.Validate(
				new List<KingdomScenarioDefinition> { Sound() }));
		}

		[TestCase("Type")]
		[TestCase("Size")]
		public void GalleryTransactionRequiresTheCompleteLotIdentity(string missing)
		{
			KingdomScenarioDefinition definition = Sound();
			definition.Steps[1].Arguments.Remove(missing);
			string findings = string.Join("; ", KingdomScenarioRules.Validate(
				new List<KingdomScenarioDefinition> { definition }));
			StringAssert.Contains("argument '" + missing + "' is required", findings);
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(definition, Facing("north"), Digest, Seed,
				out plan, out failure));
		}

		// ----- Synthetic must never fail open -----------------------------------------------

		[TestCase("true", true)]
		[TestCase("false", false)]
		public void SyntheticParsesOnlyTheTwoExactLowercaseWords(string raw, bool expected)
		{
			bool synthetic;
			string failure;
			Assert.IsTrue(KingdomScenarioVerbSchema.TryParseSynthetic(raw, out synthetic,
				out failure), failure);
			Assert.AreEqual(expected, synthetic);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("True")]
		[TestCase("FALSE")]
		[TestCase("yes")]
		[TestCase("0")]
		[TestCase("flase")]
		public void MalformedSyntheticIsAFaultRatherThanASilentNotSynthetic(string raw)
		{
			bool synthetic;
			string failure;
			Assert.IsFalse(KingdomScenarioVerbSchema.TryParseSynthetic(raw, out synthetic,
				out failure));
			Assert.IsNotEmpty(failure);
			KingdomScenarioDefinition definition = Sound();
			definition.SyntheticRaw = raw;
			Assert.IsNotEmpty(KingdomScenarioRules.Validate(
				new List<KingdomScenarioDefinition> { definition }));
			KingdomScenarioPlan plan;
			string planFailure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(definition, Facing("north"), Digest, Seed,
				out plan, out planFailure), "a malformed Synthetic row must not realize");
			Assert.IsNull(plan);
		}

		// ----- closed argument schema -------------------------------------------------------

		[Test]
		public void UndeclaredStepArgumentIsRejected()
		{
			KingdomScenarioDefinition definition = Sound();
			definition.Steps[0].Arguments["Rogue"] = "value";
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(definition, Facing("north"), Digest, Seed,
				out plan, out failure));
			StringAssert.Contains("not admitted by this verb", failure);
		}

		[Test]
		public void MissingRequiredStepArgumentIsRejected()
		{
			KingdomScenarioDefinition definition = Sound();
			definition.Steps[0].Arguments.Clear();
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(definition, Facing("north"), Digest, Seed,
				out plan, out failure));
			StringAssert.Contains("is required", failure);
		}

		[Test]
		public void ArgumentValueOutsideItsKindIsRejected()
		{
			KingdomScenarioDefinition definition = Sound();
			definition.Steps[1].Arguments["Suite"] = "Not A Token";
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(definition, Facing("north"), Digest, Seed,
				out plan, out failure));
			StringAssert.Contains("malformed value", failure);
		}

		[Test]
		public void ParameterReferenceResolvesToTheBoundValueInThePlan()
		{
			KingdomScenarioDefinition definition = Sound();
			definition.Steps[1].Arguments["Facing"] = "{facing}";
			KingdomScenarioPlan plan = Plan(definition, Facing("east"));
			Assert.AreEqual("east", plan.Steps[1].Arguments["Facing"],
				"the plan must hold the resolved value, never the reference");
		}

		[Test]
		public void ParameterReferenceToAnUndeclaredParameterIsRejected()
		{
			KingdomScenarioDefinition definition = Sound();
			definition.Steps[1].Arguments["Facing"] = "{nosuch}";
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(definition, Facing("north"), Digest, Seed,
				out plan, out failure));
			StringAssert.Contains("undeclared parameter", failure);
		}

		// ----- parameter binding ------------------------------------------------------------

		[Test]
		public void MissingParameterSelectionIsRejected()
		{
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(Sound(), null, Digest, Seed, out plan,
				out failure));
			StringAssert.Contains("needs a value", failure);
		}

		[Test]
		public void ValueOutsideTheClosedDomainIsRejected()
		{
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(Sound(), Facing("skyward"), Digest, Seed,
				out plan, out failure));
			StringAssert.Contains("is not a declared value", failure);
		}

		[Test]
		public void UndeclaredParameterInTheSelectionIsRejected()
		{
			Dictionary<string, string> selection = Facing("north");
			selection["rogue"] = "value";
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(Sound(), selection, Digest, Seed, out plan,
				out failure));
			StringAssert.Contains("declares no parameter", failure);
		}

		// ----- totality: malformed models must refuse, never throw ---------------------------

		[Test]
		public void PlanRefusesANullDefinitionWithoutThrowing()
		{
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(null, Facing("north"), Digest, Seed, out plan,
				out failure));
			Assert.IsNotEmpty(failure);
		}

		[Test]
		public void PlanRefusesANullStepWithoutThrowing()
		{
			KingdomScenarioDefinition definition = Sound();
			definition.Steps[0] = null;
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(definition, Facing("north"), Digest, Seed,
				out plan, out failure));
			Assert.IsNotEmpty(failure);
		}

		[Test]
		public void PlanRefusesANullArgumentMapWithoutThrowing()
		{
			KingdomScenarioDefinition definition = Sound();
			definition.Steps[0].Arguments = null;
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(definition, Facing("north"), Digest, Seed,
				out plan, out failure));
			Assert.IsNotEmpty(failure);
		}

		[Test]
		public void PlanRefusesAnUnknownVerbWithoutThrowing()
		{
			KingdomScenarioDefinition definition = Sound();
			definition.Steps[0].Verb = KingdomScenarioVerb.None;
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(definition, Facing("north"), Digest, Seed,
				out plan, out failure));
			StringAssert.Contains("admitted verb", failure);
		}

		[Test]
		public void PlanRefusesAMalformedRegistryDigest()
		{
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(Sound(), Facing("north"), "deadbeef", Seed,
				out plan, out failure));
			StringAssert.Contains("digest is malformed", failure);
		}

	}
}
#endif
