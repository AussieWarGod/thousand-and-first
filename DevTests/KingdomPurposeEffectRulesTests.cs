#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPurposeEffectRulesTests
	{
		private const string D = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		private const string E = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

		[Test]
		public void ManualRecipesAreExactAndKindBound()
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectRefine(
				KingdomPurposeKind.Deep, out KingdomMaterial raw, out KingdomMaterial product));
			Assert.AreEqual(KingdomMaterial.Stone, raw);
			Assert.AreEqual(KingdomMaterial.ShapedStone, product);
			Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectRefine(
				KingdomPurposeKind.Forge, out raw, out product));
			Assert.AreEqual(KingdomMaterial.Scrap, raw);
			Assert.AreEqual(KingdomMaterial.WorkedMetal, product);
			Assert.IsFalse(KingdomPurposePortfolioRules.TryEffectRefine(
				KingdomPurposeKind.Harvest, out _, out _));
			Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectHarvest(
				"crop", "seed", "staple", out int crops, out int seeds, out int staples));
			Assert.AreEqual(3, crops);
			Assert.AreEqual(1, seeds);
			Assert.AreEqual(6, staples);
			Assert.IsFalse(KingdomPurposePortfolioRules.TryEffectHarvest(
				"", "seed", "staple", out _, out _, out _));
			Assert.IsTrue(KingdomPurposePortfolioRules.EffectRecipeConserves(
				KingdomPurposeKind.Deep, 2, 1, 0));
			Assert.IsTrue(KingdomPurposePortfolioRules.EffectRecipeConserves(
				KingdomPurposeKind.Harvest, 3, 1, 6));
			Assert.IsFalse(KingdomPurposePortfolioRules.EffectRecipeConserves(
				KingdomPurposeKind.Harvest, 2, 1, 6));
		}

		[Test]
		public void TypedLaddersRejectCrossKindValuesAndSkipEdges()
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.EffectStepIsLegalFor(
				KingdomPurposeKind.Deep, (int)KingdomPurposeEffectRefineStep.Made));
			Assert.IsFalse(KingdomPurposePortfolioRules.EffectStepIsLegalFor(
				KingdomPurposeKind.Deep, (int)KingdomPurposeEffectHarvestStep.SeedMade));
			Assert.IsTrue(KingdomPurposePortfolioRules.EffectStepIsLegalFor(
				KingdomPurposeKind.Harvest, (int)KingdomPurposeEffectHarvestStep.Milled));
			Assert.IsFalse(KingdomPurposePortfolioRules.EffectStepIsLegalFor(
				KingdomPurposeKind.Flesh, 1));
			Assert.IsTrue(KingdomPurposePortfolioRules.EffectStepIsLegalFor(
				KingdomPurposeKind.Chrome, KingdomPurposePortfolioRules.PurposeEffectExempt));

			KingdomPurposeOperationReceipt before = Step(KingdomPurposeKind.Deep,
				KingdomPurposeOperationPhase.EffectPending,
				(int)KingdomPurposeEffectRefineStep.None);
			KingdomPurposeOperationReceipt after = before.Copy();
			after.EffectStep = (int)KingdomPurposeEffectRefineStep.FirstRawSpent;
			Assert.IsTrue(KingdomPurposePortfolioRules.EffectStepMonotone(before, after));
			after.EffectStep = (int)KingdomPurposeEffectRefineStep.SecondRawSpent;
			Assert.IsFalse(KingdomPurposePortfolioRules.EffectStepMonotone(before, after));
			after = before.Copy();
			after.EffectStep = KingdomPurposePortfolioRules.PurposeEffectExempt;
			Assert.IsFalse(KingdomPurposePortfolioRules.EffectStepMonotone(before, after));
			before.EffectStep = KingdomPurposePortfolioRules.PurposeEffectExempt;
			after.EffectStep = KingdomPurposePortfolioRules.PurposeEffectExempt;
			Assert.IsTrue(KingdomPurposePortfolioRules.EffectStepMonotone(before, after));

			before = Step(KingdomPurposeKind.Deep,
				KingdomPurposeOperationPhase.EffectPending,
				(int)KingdomPurposeEffectRefineStep.SecondRawSpent);
			after = before.Copy();
			after.EffectStep = (int)KingdomPurposeEffectRefineStep.Made;
			Assert.IsFalse(KingdomPurposePortfolioRules.EffectStepMonotone(before, after));
			after.Phase = KingdomPurposeOperationPhase.EffectApplied;
			Assert.IsTrue(KingdomPurposePortfolioRules.EffectStepMonotone(before, after));
		}

		[Test]
		public void PhaseCoherenceRequiresWholeLadderAtAndAfterApplied()
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.EffectPhaseCoherent(Step(
				KingdomPurposeKind.Harvest, KingdomPurposeOperationPhase.EffectPending,
				(int)KingdomPurposeEffectHarvestStep.SecondCropSpent)));
			Assert.IsFalse(KingdomPurposePortfolioRules.EffectPhaseCoherent(Step(
				KingdomPurposeKind.Harvest, KingdomPurposeOperationPhase.EffectApplied,
				(int)KingdomPurposeEffectHarvestStep.SeedMade)));
			Assert.IsTrue(KingdomPurposePortfolioRules.EffectPhaseCoherent(Step(
				KingdomPurposeKind.Harvest, KingdomPurposeOperationPhase.EffectApplied,
				(int)KingdomPurposeEffectHarvestStep.Milled)));
			Assert.IsFalse(KingdomPurposePortfolioRules.EffectPhaseCoherent(Step(
				KingdomPurposeKind.Harvest, KingdomPurposeOperationPhase.EffectPending,
				(int)KingdomPurposeEffectHarvestStep.Milled)));
			Assert.IsFalse(KingdomPurposePortfolioRules.EffectPhaseCoherent(Step(
				KingdomPurposeKind.Deep, KingdomPurposeOperationPhase.Prepared,
				(int)KingdomPurposeEffectRefineStep.FirstRawSpent)));
			Assert.IsTrue(KingdomPurposePortfolioRules.EffectPhaseCoherent(Step(
				KingdomPurposeKind.Deep, KingdomPurposeOperationPhase.Prepared,
				KingdomPurposePortfolioRules.PurposeEffectExempt)));
		}

		[Test]
		public void CurrentAndLegacyOperationWiresRoundTripWithoutReadMigration()
		{
			KingdomPurposePairReceipt pair = Pair();
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(pair, "operation", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out KingdomPurposeOperationReceipt operation, out _));
			string current = KingdomPurposePortfolioRules.EncodeOperation(operation);
			Assert.IsTrue(KingdomPurposePortfolioRules.TryDecodeOperation(current,
				out KingdomPurposeOperationReceipt currentCopy));
			Assert.AreEqual(KingdomPurposePortfolioRules.PurposeEffectNone,
				currentCopy.EffectStep);
			Assert.AreEqual(current, KingdomPurposePortfolioRules.EncodeOperation(currentCopy));

			operation.EffectStep = KingdomPurposePortfolioRules.PurposeEffectExempt;
			KingdomPurposePairReceipt running = pair.Copy();
			running.BootstrapUsed = true;
			running.Phase = KingdomPurposePairPhase.BootstrapOutstanding;
			running.Operation = operation;
			running.NextOperationOrdinal++;
			running.Revision++;
			string legacy = KingdomPurposePortfolioRules.EncodeLegacyPair(running);
			Assert.IsNotNull(legacy);
			Assert.IsFalse(KingdomPurposePortfolioRules.TryDecodePair(legacy, out _));
			Assert.IsTrue(KingdomPurposePortfolioRules.TryDecodePairAny(legacy,
				out KingdomPurposePairReceipt legacyCopy, out bool wasLegacy));
			Assert.IsTrue(wasLegacy);
			Assert.IsTrue(legacyCopy.LegacyWire);
			Assert.AreEqual(KingdomPurposePortfolioRules.PurposeEffectExempt,
				legacyCopy.Operation.EffectStep);
			Assert.AreEqual(legacy, KingdomPurposePortfolioRules.EncodeLegacyPair(legacyCopy));
			Assert.IsNotNull(KingdomPurposePortfolioRules.EncodePair(legacyCopy));
			Assert.IsFalse(KingdomPurposePortfolioRules.TryDecodePairAny(legacy + "x",
				out _, out _));
		}

		[Test]
		public void EffectEvidenceCodecsAreCanonicalAndPresenceProtects()
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectReceipt("pair", 2,
				"operation", KingdomPurposeKind.Harvest, out string receipt));
			Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectAttempt(receipt, 4,
				KingdomPurposeEffectCallbackKind.HarvestStaple, "object", 2, 4, 3,
				D, E,
				out string witness));
			Assert.IsTrue(KingdomPurposePortfolioRules.TryReadEffectAttempt(witness, receipt,
				out KingdomPurposeEffectAttempt attempt));
			Assert.AreEqual(witness, KingdomPurposePortfolioRules.EncodeEffectAttempt(attempt));
			Assert.IsFalse(KingdomPurposePortfolioRules.TryReadEffectAttempt(witness + "x",
				receipt, out _));
			KingdomPurposeEffectProductRecord record = new KingdomPurposeEffectProductRecord
				{ Seed = 1, Staple = 6 };
			Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectProductRecord(receipt, record,
				out string encoded));
			Assert.IsTrue(KingdomPurposePortfolioRules.TryReadEffectProductRecord(encoded,
				receipt, out KingdomPurposeEffectProductRecord copy));
			Assert.AreEqual(1, copy.Seed);
			Assert.AreEqual(6, copy.Staple);
			KingdomPurposeCargoEvidence evidence = new KingdomPurposeCargoEvidence
				{ EffectMark = true };
			Assert.IsTrue(KingdomPurposePortfolioRules.PurposeCargoIsProtected(evidence));
		}

		private static KingdomPurposeOperationReceipt Step(KingdomPurposeKind kind,
			KingdomPurposeOperationPhase phase, int step)
		{
			return new KingdomPurposeOperationReceipt
				{ SourceKind = kind, Phase = phase, EffectStep = step };
		}

		private static KingdomPurposePairReceipt Pair()
		{
			Assert.IsTrue(KingdomPurposePortfolioRules.TryCreatePair("pair", "realm", 7,
				KingdomPurposeKind.Deep, KingdomPurposeKind.Forge, "city-a", "city-b",
				"work-a", null, "zone-a", "zone-b", "input-a", "output-a", "input-b",
				"output-b", "gate-a", "gate-b", D, out KingdomPurposePairReceipt pair,
				out _));
			return pair;
		}
	}
}
#endif
