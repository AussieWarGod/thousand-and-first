#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPurposeEffectWireTypeTests
	{
		private const string D = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

		[Test]
		public void CurrentDecoderRejectsStepValuesOutsideEachSourceKindLadder()
		{
			AssertInvalidWireStep(KingdomPurposeKind.Deep, KingdomPurposeKind.Forge, 4);
			AssertInvalidWireStep(KingdomPurposeKind.Forge, KingdomPurposeKind.Deep, 4);
			AssertInvalidWireStep(KingdomPurposeKind.Harvest, KingdomPurposeKind.Forge, 6);
		}

		[Test]
		public void SameNumericValueKeepsKindSpecificMeaning()
		{
			KingdomPurposeOperationReceipt deepMade = Effect(KingdomPurposeKind.Deep, 3,
				KingdomPurposeOperationPhase.EffectApplied);
			KingdomPurposeOperationReceipt harvestThird = Effect(KingdomPurposeKind.Harvest, 3,
				KingdomPurposeOperationPhase.EffectPending);
			Assert.IsTrue(KingdomPurposePortfolioRules.EffectPhaseCoherent(deepMade));
			Assert.IsTrue(KingdomPurposePortfolioRules.EffectPhaseCoherent(harvestThird));
			harvestThird.Phase = KingdomPurposeOperationPhase.EffectApplied;
			Assert.IsFalse(KingdomPurposePortfolioRules.EffectPhaseCoherent(harvestThird));
			Assert.IsFalse(KingdomPurposePortfolioRules.EffectStepMonotone(
				deepMade, Effect(KingdomPurposeKind.Harvest, 3,
					KingdomPurposeOperationPhase.EffectPending)));
		}

		private static void AssertInvalidWireStep(KingdomPurposeKind first,
			KingdomPurposeKind second, int invalidStep)
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreatePair("pair-" + first,
				"realm", 1, first, second, "city-a", "city-b", "work-a", null,
				"zone-a", "zone-b", "input-a", "output-a", "input-b", "output-b",
				"gate-a", "gate-b", D, out KingdomPurposePairReceipt pair, out _));
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(pair, "operation", 1,
				first, true, false, null, null, null, null, null,
				out KingdomPurposeOperationReceipt operation, out _));
			string encoded = KingdomPurposePortfolioRules.EncodeOperation(operation);
			const string tail = ";1:0;17:purpose-operation";
			StringAssert.EndsWith(tail, encoded);
			string invalid = encoded.Substring(0, encoded.Length - tail.Length)
				+ ";1:" + invalidStep + ";17:purpose-operation";
			Assert.IsFalse(KingdomPurposePortfolioRules.TryDecodeOperation(invalid, out _));
		}

		private static KingdomPurposeOperationReceipt Effect(KingdomPurposeKind kind,
			int step, KingdomPurposeOperationPhase phase)
		{
			return new KingdomPurposeOperationReceipt
				{ SourceKind = kind, EffectStep = step, Phase = phase };
		}
	}
}
#endif
