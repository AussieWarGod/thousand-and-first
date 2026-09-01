#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomBenefitAdmissionRulesTests
	{
		[Test]
		public void ExactZoneCapRemainsFullyUsable()
		{
			int cap = KingdomBenefitEmbodimentRules.MaxProvidersPerZone;
			Assert.That(KingdomBenefitAdmissionRules.WholeObjectFits(cap - 1, 1), Is.True);
			Assert.That(KingdomBenefitAdmissionRules.Remaining(cap), Is.Zero);
			Assert.That(KingdomBenefitAdmissionRules.WholeObjectFits(cap, 1), Is.False);
		}

		[Test]
		public void OversizeObjectConsumesOneDiagnosticRowNotHostilePartCount()
		{
			Assert.That(KingdomBenefitAdmissionRules.ExplicitRows(
				KingdomBenefitEmbodimentRules.MaxProviderPartsPerObject + 1, true), Is.EqualTo(1));
			Assert.That(KingdomBenefitAdmissionRules.ExplicitRows(16, false), Is.EqualTo(16));
		}

		[Test]
		public void DeclarativeObjectAdmissionIsAtomic()
		{
			int cap = KingdomBenefitEmbodimentRules.MaxProvidersPerZone;
			Assert.That(KingdomBenefitAdmissionRules.WholeObjectFits(cap - 2, 3), Is.False);
			Assert.That(KingdomBenefitAdmissionRules.Remaining(cap - 2), Is.EqualTo(2));
		}

		[Test]
		public void OversizeTieIsSkippedAndLaterExactGroupStillFits()
		{
			int admitted = 0;
			Assert.That(KingdomBenefitAdmissionRules.TryAdmitWholeGroup(
				ref admitted, KingdomBenefitEmbodimentRules.MaxProvidersPerZone + 16), Is.False);
			Assert.That(admitted, Is.Zero);
			Assert.That(KingdomBenefitAdmissionRules.TryAdmitWholeGroup(
				ref admitted, 2), Is.True);
			Assert.That(admitted, Is.EqualTo(2));
		}

		[Test]
		public void CanonicalNativePrefixFillsOnlyRemainingRows()
		{
			int cap = KingdomBenefitEmbodimentRules.MaxProvidersPerZone;
			Assert.That(KingdomBenefitAdmissionRules.NativePrefix(cap - 2, 5), Is.EqualTo(2));
			Assert.That(KingdomBenefitAdmissionRules.NativePrefix(cap, 5), Is.Zero);
		}

		[Test]
		public void RuntimeRecordsOneVisibleLimitFaultInsteadOfFailingTheIndex()
		{
			string collect = TestMain.ReadRepositoryText("Growth/KingdomBenefitIndex.Collect.cs");
			StringAssert.Contains("KingdomBenefitFault.ObservationLimit", collect);
			StringAssert.Contains("stable bounded admission", collect);
			StringAssert.Contains("a.ExactIdentity ? -1 : 1", collect);
			StringAssert.Contains("start = end; continue", collect);
			StringAssert.Contains("ReproveAdmittedBatches", collect);
			string reproof = TestMain.ReadRepositoryText(
				"Growth/KingdomBenefitIndex.CollectionReproof.cs");
			StringAssert.Contains("SameExplicitReferences", reproof);
			StringAssert.Contains("bool[] used", reproof,
				"provider references are an exact multiset, not count plus membership");
			StringAssert.Contains("SameNativePrefix", reproof);
			StringAssert.Contains("SamePlacement", reproof);
			StringAssert.Contains("designation root moved", reproof);
			StringAssert.Contains("KingdomConstruction.FindExactId(Z", reproof);
			StringAssert.Contains("!= KingdomPhysicalLookupState.Exact", reproof);
			StringAssert.DoesNotContain("aggregate.Root == null) continue", reproof);
			StringAssert.Contains("ReproveCollectedProviderSnapshot", TestMain.ReadRepositoryText(
				"Growth/KingdomBenefitIndex.Build.cs"));
			string declarations = TestMain.ReadRepositoryText(
				"Growth/KingdomBenefitIndex.DeclarationReproof.cs");
			StringAssert.Contains("ReproveProviderDescriptions", declarations);
			StringAssert.Contains("CanonicalDescription", declarations);
			StringAssert.Contains("TryDescribeKingdomBenefits", declarations);
			StringAssert.DoesNotContain(
				"physical provider inspection exceeded its bounded zone rows", collect);
		}
	}
}
#endif
