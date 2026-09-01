using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomBenefitOperationTests
	{
		[TestCase(100, 100, 100)]
		[TestCase(80, 50, 40)]
		[TestCase(33, 33, 10)]
		[TestCase(0, 100, 0)]
		[TestCase(-1, 100, 0)]
		[TestCase(101, 100, 0)]
		public void IndependentGatesMultiplyAndMalformedInputsFailClosed(
			int first, int second, int expected)
		{
			Assert.That(KingdomBenefitOperationRules.Compose(first, second),
				Is.EqualTo(expected));
		}

		[TestCase(null, null, true)]
		[TestCase("", "smithy", false)]
		[TestCase(" SMITHY ", "smithy", true)]
		[TestCase("forge", "smithy", false)]
		[TestCase("forge", null, false)]
		[TestCase(" ", "smithy", false)]
		[TestCase("forge|smithy", "forge|smithy", false)]
		public void ExactDesignAffinityIsIndependentAndGenericNativeProvidersRemainOpen(
			string provider, string designation, bool expected)
		{
			Assert.That(KingdomBenefitOperationRules.ProviderMatchesDesign(
				provider, designation), Is.EqualTo(expected));
		}

		[Test]
		public void OversizeDesignAffinityFailsClosed()
		{
			Assert.That(KingdomBenefitOperationRules.ProviderMatchesDesign(
				new string('a', 129), new string('a', 129)), Is.False);
		}

		[Test]
		public void FilledOperationIsRejectedWithoutTypedContentsContract()
		{
			Assert.That(KingdomBenefitProviderRules.TryDescribe("test:filled", "roof:1", "",
				"building", "filled", "", out _, out string failure), Is.False);
			StringAssert.Contains("typed contents", failure);
			KingdomBenefitProviderDeclaration code = new KingdomBenefitProviderDeclaration {
				Key = "test:filled", Scope = KingdomBenefitScope.Building,
				Operation = KingdomBenefitOperation.Filled,
				Carries = new List<KindAmount> { new KindAmount("roof", 1) }
			};
			Assert.That(KingdomBenefitProviderRules.TryNormalize(code, out _, out _), Is.False);
		}

		[Test]
		public void RuntimeComposesRootConditionWithEveryOperationGate()
		{
			string operation = TestMain.ReadRepositoryText(Path.Combine(
				"Growth", "KingdomBenefitIndex.Operation.cs"));
			StringAssert.Contains("Item.IsBroken()", operation);
			StringAssert.Contains("Root.IsBroken()", operation);
			StringAssert.Contains("PhysicalConditionPercent(Root)", operation);
			StringAssert.Contains("Item.HasTag(\"r_KingdomProviderBuildKey\")", operation);
			StringAssert.Contains("PhysicalConditionPercent(Item)", operation);
			StringAssert.Contains("!ReferenceEquals(Item, Root)", operation,
				"one object serving as both root and provider must not have its wear squared");
			StringAssert.Contains("KingdomBenefitOperationRules.Compose(condition, operation)",
				operation);
			StringAssert.DoesNotContain("GetIntProperty(\"KingdomPowered\")", operation);
			StringAssert.Contains("IKingdomQuantitativeBenefitProvider", operation);
			StringAssert.Contains("ProviderMatchesDesign", operation);
			string evaluate = TestMain.ReadRepositoryText(Path.Combine(
				"Growth", "KingdomBenefitIndex.Evaluate.cs"));
			StringAssert.Contains("match.Designation.BuildingKey", evaluate);
			StringAssert.Contains("ReproveAfterCustomOperation", evaluate);
			Assert.Less(operation.IndexOf("CustomPercent(Item", StringComparison.Ordinal),
				operation.IndexOf("int condition =", StringComparison.Ordinal),
				"provider/root condition must be sampled after a custom callback returns");
			string reproof = TestMain.ReadRepositoryText(Path.Combine(
				"Growth", "KingdomBenefitIndex.OperationReproof.cs"));
			StringAssert.Contains("ProviderStillAttached", reproof);
			StringAssert.Contains("FindExactId", reproof);
			StringAssert.Contains("TryProviderCell", reproof);
			StringAssert.Contains("TryAssign", reproof);
			StringAssert.Contains("Aggregate.AccessRead = false", reproof);
			StringAssert.Contains("Aggregate.ShellRead = false", reproof);
			string final = TestMain.ReadRepositoryText(Path.Combine(
				"Growth", "KingdomBenefitIndex.FinalReproof.cs"));
			StringAssert.Contains("pair.Value.AccessRead = false", final);
			StringAssert.Contains("pair.Value.ShellRead = false", final);
			StringAssert.Contains("ReproveCandidate", final);
			StringAssert.Contains("TryAssign", final);
			StringAssert.Contains("Accessible(aggregate", final);
			StringAssert.Contains("ShellValid(aggregate", final);
			StringAssert.Contains("PhysicalConditionPercent(item)", final);
			StringAssert.Contains("item.IsBroken() != Candidate.ItemBroken", final);
			StringAssert.Contains("IsKingdomBenefitOperational", operation,
				"legacy boolean custom providers remain compatible");
		}

		[Test]
		public void StateProvidersPublishBoundedQuantitativeOperation()
		{
			string state = TestMain.ReadRepositoryText(Path.Combine(
				"Growth", "r_KingdomStateBenefitProvider.cs"));
			StringAssert.Contains("IKingdomQuantitativeBenefitProvider", state);
			StringAssert.Contains("TryKingdomBenefitOperationPercent", state);
			StringAssert.Contains("KingdomBenefitIndex.StaffingPercent", state);
			StringAssert.DoesNotContain("KingdomWear.EffectivenessOf", state,
				"root condition belongs to the evaluator's independent gate");
		}

		[Test]
		public void ExtensionCallbacksHaveAnExplicitReadOnlyDeterministicProofBoundary()
		{
			string contract = TestMain.ReadRepositoryText(Path.Combine(
				"Growth", "KingdomBenefitProvider.cs"));
			StringAssert.Contains("observation-only deterministic", contract);
			StringAssert.Contains("not mutate the provider", contract);
			StringAssert.Contains("repeated description calls", contract);
			string api = TestMain.ReadRepositoryText(Path.Combine("docs", "API.md"));
			StringAssert.Contains("custom callback's arbitrary hidden state cannot be re-proved", api);
			StringAssert.Contains("not supported authority", api);
		}
	}
}
