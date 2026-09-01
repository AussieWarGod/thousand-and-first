#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomArchitectureReceiptPrefixRulesTests
	{
		[TestCase(false, 0, false, false, null, false, null,
			ArchitectureOutputPrefix.Empty)]
		[TestCase(false, 0, false, true, "id", false, null,
			ArchitectureOutputPrefix.IdOnly)]
		[TestCase(true, 1, false, false, null, false, "id",
			ArchitectureOutputPrefix.StateOnly)]
		[TestCase(true, 1, false, true, "id", false, "id",
			ArchitectureOutputPrefix.Published)]
		[TestCase(true, 2, false, true, "id", false, "id",
			ArchitectureOutputPrefix.Settled)]
		[TestCase(true, 2, false, false, null, false, "id",
			ArchitectureOutputPrefix.Malformed)]
		[TestCase(true, 1, true, true, "id", false, "id",
			ArchitectureOutputPrefix.Malformed)]
		[TestCase(true, 1, false, true, "id", true, "id",
			ArchitectureOutputPrefix.Malformed)]
		[TestCase(true, 1, false, true, "third", false, "id",
			ArchitectureOutputPrefix.Malformed)]
		public void OutputCutTable(bool hasState, int state, bool stringState,
			bool hasId, string id, bool intId, string expected,
			ArchitectureOutputPrefix result)
		{
			Assert.AreEqual(result, KingdomArchitectureReceiptPrefixRules.ClassifyOutput(
				hasState, state, stringState, hasId, id, intId, expected));
		}

		[TestCase(0, ArchitectureOutputPrefix.Empty, true)]
		[TestCase(0, ArchitectureOutputPrefix.StateOnly, true)]
		[TestCase(0, ArchitectureOutputPrefix.Published, true)]
		[TestCase(0, ArchitectureOutputPrefix.IdOnly, false)]
		[TestCase(0, ArchitectureOutputPrefix.Settled, false)]
		[TestCase(1, ArchitectureOutputPrefix.Published, true)]
		[TestCase(1, ArchitectureOutputPrefix.Settled, true)]
		[TestCase(1, ArchitectureOutputPrefix.StateOnly, false)]
		[TestCase(2, ArchitectureOutputPrefix.Settled, true)]
		[TestCase(2, ArchitectureOutputPrefix.Published, false)]
		public void RetainedPublicationCutTable(int ownerState,
			ArchitectureOutputPrefix target, bool legal)
		{
			Assert.AreEqual(legal,
				KingdomArchitectureReceiptPrefixRules.LegalRetainedTarget(ownerState, target));
		}

		[Test]
		public void HeaderScalarsAreExactOrAbsentAndTypeSafe()
		{
			Assert.IsTrue(KingdomArchitectureReceiptPrefixRules.ExactOrAbsentInt(
				false, 0, false, 7));
			Assert.IsTrue(KingdomArchitectureReceiptPrefixRules.ExactOrAbsentInt(
				true, 7, false, 7));
			Assert.IsFalse(KingdomArchitectureReceiptPrefixRules.ExactOrAbsentInt(
				true, 8, false, 7));
			Assert.IsFalse(KingdomArchitectureReceiptPrefixRules.ExactOrAbsentInt(
				false, 0, true, 7));
			Assert.IsTrue(KingdomArchitectureReceiptPrefixRules.ExactOrAbsentString(
				true, "expected", false, "expected"));
			Assert.IsFalse(KingdomArchitectureReceiptPrefixRules.ExactOrAbsentString(
				true, "third", false, "expected"));
			Assert.IsFalse(KingdomArchitectureReceiptPrefixRules.ExactOrAbsentString(
				false, null, true, "expected"));
		}

		[TestCase(true, 7, false, 7, true)]
		[TestCase(false, 0, false, 0, false)]
		[TestCase(true, 7, true, 7, false)]
		[TestCase(true, 8, false, 7, false)]
		public void ExactIntegerRejectsAbsenceWrongValueAndDualTypeCollision(
			bool hasInt, int observed, bool hasString, int expected, bool legal)
		{
			Assert.AreEqual(legal, KingdomArchitectureReceiptPrefixRules.ExactInt(
				hasInt, observed, hasString, expected));
		}

		[TestCase(true, "expected", false, "expected", true)]
		[TestCase(false, null, false, "expected", false)]
		[TestCase(true, "expected", true, "expected", false)]
		[TestCase(true, "third", false, "expected", false)]
		public void ExactStringRejectsAbsenceWrongValueAndDualTypeCollision(
			bool hasString, string observed, bool hasInt, string expected, bool legal)
		{
			Assert.AreEqual(legal, KingdomArchitectureReceiptPrefixRules.ExactString(
				hasString, observed, hasInt, expected));
		}

		[TestCase(false, 0, false, 1, true)]
		[TestCase(true, 1, false, 1, true)]
		[TestCase(true, 0, false, 1, false)]
		[TestCase(true, 1, true, 1, false)]
		public void OptionalIntegerAllowsOnlyAbsenceOrExactTypedValue(
			bool hasInt, int observed, bool hasString, int expected, bool legal)
		{
			Assert.AreEqual(legal, KingdomArchitectureReceiptPrefixRules.ExactOptionalInt(
				hasInt, observed, hasString, expected));
		}

		[TestCase(false, null, false, null, true)]
		[TestCase(true, "anchor", false, "anchor", true)]
		[TestCase(false, null, false, "anchor", false)]
		[TestCase(true, "anchor", false, null, false)]
		[TestCase(true, "anchor", true, "anchor", false)]
		public void OptionalStringHasExactIntentionalAbsenceTypeAndValue(
			bool hasString, string observed, bool hasInt, string expected, bool legal)
		{
			Assert.AreEqual(legal, KingdomArchitectureReceiptPrefixRules.ExactOptionalString(
				hasString, observed, hasInt, expected));
		}

		[TestCase(false, null, false, ArchitectureUpgradeFaultEvidence.None)]
		[TestCase(true, "fault", false, ArchitectureUpgradeFaultEvidence.Message)]
		[TestCase(true, "", false, ArchitectureUpgradeFaultEvidence.EmptyString)]
		[TestCase(true, "   ", false, ArchitectureUpgradeFaultEvidence.EmptyString)]
		[TestCase(false, null, true, ArchitectureUpgradeFaultEvidence.Integer)]
		[TestCase(true, "fault", true, ArchitectureUpgradeFaultEvidence.Collision)]
		[TestCase(true, "", true, ArchitectureUpgradeFaultEvidence.Collision)]
		public void UpgradeFaultPropertyPresenceIsAlwaysTerminalEvidence(
			bool hasString, string observed, bool hasInt,
			ArchitectureUpgradeFaultEvidence evidence)
		{
			Assert.AreEqual(evidence,
				KingdomArchitectureReceiptPrefixRules.ClassifyUpgradeFault(
					hasString, observed, hasInt));
		}

		[TestCase(true, 3, false, 3, 4, true)]
		[TestCase(true, 4, false, 3, 4, true)]
		[TestCase(true, 5, false, 3, 4, false)]
		[TestCase(false, 0, false, 3, 4, false)]
		[TestCase(true, 3, true, 3, 4, false)]
		public void RetagIntegerScalarCutTable(bool hasInt, int observed, bool hasString,
			int oldValue, int nextValue, bool legal)
		{
			Assert.AreEqual(legal, KingdomArchitectureReceiptPrefixRules.OldOrNewInt(
				hasInt, observed, hasString, oldValue, nextValue));
		}

		[TestCase(true, "old", false, "old", "next", true)]
		[TestCase(true, "next", false, "old", "next", true)]
		[TestCase(true, "third", false, "old", "next", false)]
		[TestCase(false, null, false, null, "next", true)]
		[TestCase(false, null, false, "old", "next", false)]
		[TestCase(true, "old", true, "old", "next", false)]
		public void RetagStringScalarCutTable(bool hasString, string observed, bool hasInt,
			string oldValue, string nextValue, bool legal)
		{
			Assert.AreEqual(legal, KingdomArchitectureReceiptPrefixRules.OldOrNewString(
				hasString, observed, hasInt, oldValue, nextValue));
		}
	}
}
#endif
