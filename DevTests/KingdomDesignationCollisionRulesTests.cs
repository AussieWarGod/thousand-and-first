#if TAF_TESTS
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomDesignationCollisionRulesTests
	{
		[Test]
		public void ExtensionCannotEraseTrustedIdentityOrRoot()
		{
			Assert.IsTrue(KingdomDesignationCollisionRules.TryRefused(
				new[] { "trusted", "trusted", "other" },
				new[] { "root-a", "root-b", "root-a" },
				new[] { true, false, false }, out HashSet<int> refused));
			CollectionAssert.AreEquivalent(new[] { 1, 2 }, refused);
		}

		[Test]
		public void PeerCollisionsQuarantineBothButIndependentRowsSurvive()
		{
			Assert.IsTrue(KingdomDesignationCollisionRules.TryRefused(
				new[] { "a", "a", "safe" }, new[] { "one", "two", "three" },
				new[] { false, false, false }, out HashSet<int> refused));
			CollectionAssert.AreEquivalent(new[] { 0, 1 }, refused);
			Assert.IsFalse(refused.Contains(2));
		}

		[Test]
		public void ExtensionBoundaryDerivesBenefitsAndRestrictsGenericSpatialClaims()
		{
			string source = TestMain.ReadRepositoryText(Path.Combine("Growth",
				"KingdomDesignationIndex.Runtime.cs"));
			int copy = source.IndexOf("KingdomBenefitDesignation row = CopySource(source)");
			int restrict = source.IndexOf("RestrictExternalSpatialClaims(row)", copy);
			int catalogue = source.IndexOf("CompleteCatalogueContract(row, Z", copy);
			Assert.GreaterOrEqual(copy, 0);
			Assert.Greater(restrict, copy);
			Assert.Greater(catalogue, restrict);
			StringAssert.DoesNotContain("Caps.AddRange(Source.Caps)", source);
			StringAssert.DoesNotContain("AcceptedTags.AddRange(Source.AcceptedTags)", source);
			StringAssert.Contains("KingdomBenefitCellUse.Ingress", source);
			StringAssert.Contains("KingdomBenefitCover.Open", source);
		}

		[Test]
		public void ExtensionRowsAreBudgetedBeforeUntrustedCopyAndRegistryIsCopiedBeforeSort()
		{
			string source = TestMain.ReadRepositoryText(Path.Combine("Growth",
				"KingdomDesignationIndex.Runtime.cs"));
			int budget = source.IndexOf("totalCells > KingdomDesignationRules.MaxCellsPerZoneIndex");
			int copy = source.IndexOf("KingdomBenefitDesignation row = CopySource(source)");
			Assert.GreaterOrEqual(budget, 0);
			Assert.Greater(copy, budget);
			StringAssert.Contains("new List<Type>(discovered)", source);
			StringAssert.Contains("MaxDesignationProviders", source);
			StringAssert.Contains("MaxSourceFaults", source);
		}
	}
}
#endif
