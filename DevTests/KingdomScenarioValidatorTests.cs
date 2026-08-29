#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The one shared row validator, and the canonicalization that must agree with it. A row the
	/// validator rejects must have no canonical text and must not plan, or registry load, direct
	/// preflight, and digesting could disagree about what a scenario is.
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioValidatorTests
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

		// ----- structural atomicity ----------------------------------------------------------

		/// <summary>
		/// Atomicity is structural: a mutating verb that is not last would let an observation refuse
		/// after production state had already changed.
		/// </summary>
		[Test]
		public void AMutatingVerbThatIsNotTheLastStepIsRejected()
		{
			KingdomScenarioDefinition definition = Sound();
			definition.Steps.Clear();
			definition.Steps.Add(Stage("north"));
			definition.Steps.Add(Step(KingdomScenarioVerb.ProveCatalogue, "Catalogue", "architecture"));
			StringAssert.Contains("not the last step",
				string.Join("; ", KingdomScenarioRowValidator.Findings(definition)));
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(definition, Facing("north"), Digest, Seed,
				out plan, out failure));
		}

		[Test]
		public void MoreThanOneMutatingVerbIsRejected()
		{
			KingdomScenarioDefinition definition = Sound();
			definition.Steps.Clear();
			definition.Steps.Add(Stage("north"));
			definition.Steps.Add(Stage("east"));
			StringAssert.Contains("at most one production transaction",
				string.Join("; ", KingdomScenarioRowValidator.Findings(definition)));
		}

		/// <summary>
		/// The agreement that keeps the three callers honest: anything the validator rejects has no
		/// canonical text and cannot plan.
		/// </summary>
		[Test]
		public void ARowTheValidatorRejectsHasNoCanonicalTextAndCannotPlan()
		{
			List<KingdomScenarioDefinition> broken = new List<KingdomScenarioDefinition>();
			KingdomScenarioDefinition oversizeDomain = Sound();
			List<string> domain = new List<string>();
			for (int i = 0; i <= KingdomScenarioRowValidator.MaxDomainValues; i++)
				domain.Add("v" + i);
			domain.Add("north");
			oversizeDomain.Parameters[0].Domain = domain;
			broken.Add(oversizeDomain);
			KingdomScenarioDefinition badSynthetic = Sound();
			badSynthetic.SyntheticRaw = "True";
			broken.Add(badSynthetic);
			for (int i = 0; i < broken.Count; i++)
			{
				KingdomScenarioDefinition row = broken[i];
				Assert.IsFalse(KingdomScenarioRowValidator.Valid(row), "row " + i);
				Assert.IsNull(KingdomScenarioDigests.Canonical(row), "canonical row " + i);
				Assert.IsNull(KingdomScenarioDigests.Registry(
					new List<KingdomScenarioDefinition> { row }), "digest row " + i);
				KingdomScenarioPlan plan;
				string failure;
				Assert.IsFalse(KingdomScenarioRules.TryPlan(row, Facing("north"), Digest, Seed, out plan,
					out failure), "plan row " + i);
			}
		}

		/// <summary>The seed arrives from the launcher-frozen request, so its shape is judged there.</summary>
		[TestCase("")]
		[TestCase("#")]
		[TestCase("Kavvat")]
		[TestCase("4242 ")]
		public void PlanRefusesAMalformedRequestedSeed(string seed)
		{
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(Sound(), Facing("north"), Digest, seed,
				out plan, out failure));
			Assert.IsNotEmpty(failure);
		}

		/// <summary>No requested seed is a lawful plan: it simply claims no determinism.</summary>
		[Test]
		public void PlanWithoutARequestedSeedMakesNoDeterminismClaim()
		{
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsTrue(KingdomScenarioRules.TryPlan(Sound(), Facing("north"), Digest, null,
				out plan, out failure), failure);
			Assert.IsNull(plan.Seed);
		}

		[Test]
		public void PlanRefusesMoreStepsThanTheProvenanceVerbCap()
		{
			KingdomScenarioDefinition definition = Sound();
			definition.Steps.Clear();
			for (int i = 0; i <= KingdomScenarioRules.MaxSteps; i++)
				definition.Steps.Add(Step(KingdomScenarioVerb.ProveCatalogue, "Catalogue", "architecture"));
			KingdomScenarioPlan plan;
			string failure;
			Assert.IsFalse(KingdomScenarioRules.TryPlan(definition, Facing("north"), Digest, Seed,
				out plan, out failure));
			Assert.IsNotEmpty(failure);
		}

		// ----- Validate totality -------------------------------------------------------------

		[Test]
		public void ValidateReportsRatherThanThrowsOnAWhollyMalformedRoster()
		{
			KingdomScenarioDefinition broken = new KingdomScenarioDefinition
			{
				Key = "broken",
				Family = null,
				AuthorityClass = null,
				SyntheticRaw = "maybe",
				Parameters = null,
				Steps = null
			};
			IList<string> findings = KingdomScenarioRules.Validate(
				new List<KingdomScenarioDefinition> { broken, null });
			Assert.Greater(findings.Count, 3, string.Join("; ", findings));
		}

		[Test]
		public void ValidateFlagsDuplicateKeysAndDuplicateParameters()
		{
			KingdomScenarioDefinition a = Sound();
			KingdomScenarioDefinition b = Sound();
			b.Parameters.Add(new KingdomScenarioParameter
			{
				Name = "facing",
				Domain = new List<string> { "north" }
			});
			IList<string> findings = KingdomScenarioRules.Validate(
				new List<KingdomScenarioDefinition> { a, b });
			string joined = string.Join("; ", findings);
			StringAssert.Contains("duplicate scenario key", joined);
			StringAssert.Contains("twice", joined);
		}

		[Test]
		public void ValidateFlagsEmptyAndRepeatedDomainValues()
		{
			KingdomScenarioDefinition empty = Sound();
			empty.Parameters[0].Domain = new List<string>();
			KingdomScenarioDefinition repeated = Sound();
			repeated.Key = "repeated";
			repeated.Parameters[0].Domain = new List<string> { "north", "north" };
			string joined = string.Join("; ", KingdomScenarioRules.Validate(
				new List<KingdomScenarioDefinition> { empty }))
				+ string.Join("; ", KingdomScenarioRules.Validate(
					new List<KingdomScenarioDefinition> { repeated }));
			StringAssert.Contains("empty domain", joined);
			StringAssert.Contains("repeats domain value", joined);
		}

		[Test]
		public void ValidateFlagsAnEmptyRoster()
		{
			Assert.IsNotEmpty(KingdomScenarioRules.Validate(null));
			Assert.IsNotEmpty(KingdomScenarioRules.Validate(new List<KingdomScenarioDefinition>()));
		}
		// ----- Canonical / Digest totality ---------------------------------------------------

		[Test]
		public void CanonicalReturnsNullRatherThanThrowingOnMalformedRows()
		{
			Assert.IsNull(KingdomScenarioDigests.Canonical(null));
			Assert.IsNull(KingdomScenarioDigests.Canonical(new KingdomScenarioDefinition
			{
				Key = "BAD KEY"
			}));
			KingdomScenarioDefinition nullStep = Sound();
			nullStep.Steps[0] = null;
			Assert.IsNull(KingdomScenarioDigests.Canonical(nullStep));
			KingdomScenarioDefinition nullArgs = Sound();
			nullArgs.Steps[0].Arguments = null;
			Assert.IsNull(KingdomScenarioDigests.Canonical(nullArgs));
		}

		[Test]
		public void DigestReturnsNullWhenAnyRowCannotBeCanonicalized()
		{
			Assert.IsNull(KingdomScenarioDigests.Registry(null));
			Assert.IsNull(KingdomScenarioDigests.Registry(
				new List<KingdomScenarioDefinition> { Sound(), null }));
		}

		[Test]
		public void DigestIsStableAcrossRowOrderAndChangesWithAuthoredText()
		{
			KingdomScenarioDefinition a = Sound();
			KingdomScenarioDefinition b = Sound();
			b.Key = "second";
			string forward = KingdomScenarioDigests.Registry(
				new List<KingdomScenarioDefinition> { a, b });
			string reversed = KingdomScenarioDigests.Registry(
				new List<KingdomScenarioDefinition> { b, a });
			Assert.AreEqual(forward, reversed);
			KingdomScenarioDefinition changed = Sound();
			changed.AuthorityClass = "architecture-stamper-changed";
			Assert.AreNotEqual(KingdomScenarioDigests.Registry(
				new List<KingdomScenarioDefinition> { a }),
				KingdomScenarioDigests.Registry(new List<KingdomScenarioDefinition> { changed }));
		}

		/// <summary>Synthetic is digested from the raw text, so a typo changes the roster digest.</summary>
		[Test]
		public void DigestDistinguishesSyntheticText()
		{
			KingdomScenarioDefinition truthy = Sound();
			truthy.SyntheticRaw = "true";
			Assert.AreNotEqual(
				KingdomScenarioDigests.Registry(new List<KingdomScenarioDefinition> { Sound() }),
				KingdomScenarioDigests.Registry(new List<KingdomScenarioDefinition> { truthy }));
		}
	}
}
#endif
