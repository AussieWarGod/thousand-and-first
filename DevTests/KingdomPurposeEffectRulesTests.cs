#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectRefine(
				KingdomPurposeKind.Deep, out KingdomMaterial raw, out KingdomMaterial product));
			ClassicAssert.AreEqual(KingdomMaterial.Stone, raw);
			ClassicAssert.AreEqual(KingdomMaterial.ShapedStone, product);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectRefine(
				KingdomPurposeKind.Forge, out raw, out product));
			ClassicAssert.AreEqual(KingdomMaterial.Scrap, raw);
			ClassicAssert.AreEqual(KingdomMaterial.WorkedMetal, product);
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryEffectRefine(
				KingdomPurposeKind.Harvest, out _, out _));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectHarvest(
				"crop", "seed", "staple", out int crops, out int seeds, out int staples));
			ClassicAssert.AreEqual(3, crops);
			ClassicAssert.AreEqual(1, seeds);
			ClassicAssert.AreEqual(6, staples);
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryEffectHarvest(
				"", "seed", "staple", out _, out _, out _));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.EffectRecipeConserves(
				KingdomPurposeKind.Deep, 2, 1, 0));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.EffectRecipeConserves(
				KingdomPurposeKind.Harvest, 3, 1, 6));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.EffectRecipeConserves(
				KingdomPurposeKind.Harvest, 2, 1, 6));
		}

		[Test]
		public void TypedLaddersRejectCrossKindValuesAndSkipEdges()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.EffectStepIsLegalFor(
				KingdomPurposeKind.Deep, (int)KingdomPurposeEffectRefineStep.Made));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.EffectStepIsLegalFor(
				KingdomPurposeKind.Deep, (int)KingdomPurposeEffectHarvestStep.SeedMade));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.EffectStepIsLegalFor(
				KingdomPurposeKind.Harvest, (int)KingdomPurposeEffectHarvestStep.Milled));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.EffectStepIsLegalFor(
				KingdomPurposeKind.Flesh, 1));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.EffectStepIsLegalFor(
				KingdomPurposeKind.Chrome, KingdomPurposePortfolioRules.PurposeEffectExempt));

			KingdomPurposeOperationReceipt before = Step(KingdomPurposeKind.Deep,
				KingdomPurposeOperationPhase.EffectPending,
				(int)KingdomPurposeEffectRefineStep.None);
			KingdomPurposeOperationReceipt after = before.Copy();
			after.EffectStep = (int)KingdomPurposeEffectRefineStep.FirstRawSpent;
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.EffectStepMonotone(before, after));
			after.EffectStep = (int)KingdomPurposeEffectRefineStep.SecondRawSpent;
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.EffectStepMonotone(before, after));
			after = before.Copy();
			after.EffectStep = KingdomPurposePortfolioRules.PurposeEffectExempt;
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.EffectStepMonotone(before, after));
			before.EffectStep = KingdomPurposePortfolioRules.PurposeEffectExempt;
			after.EffectStep = KingdomPurposePortfolioRules.PurposeEffectExempt;
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.EffectStepMonotone(before, after));

			before = Step(KingdomPurposeKind.Deep,
				KingdomPurposeOperationPhase.EffectPending,
				(int)KingdomPurposeEffectRefineStep.SecondRawSpent);
			after = before.Copy();
			after.EffectStep = (int)KingdomPurposeEffectRefineStep.Made;
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.EffectStepMonotone(before, after));
			after.Phase = KingdomPurposeOperationPhase.EffectApplied;
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.EffectStepMonotone(before, after));
		}

		[Test]
		public void PhaseCoherenceRequiresWholeLadderAtAndAfterApplied()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.EffectPhaseCoherent(Step(
				KingdomPurposeKind.Harvest, KingdomPurposeOperationPhase.EffectPending,
				(int)KingdomPurposeEffectHarvestStep.SecondCropSpent)));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.EffectPhaseCoherent(Step(
				KingdomPurposeKind.Harvest, KingdomPurposeOperationPhase.EffectApplied,
				(int)KingdomPurposeEffectHarvestStep.SeedMade)));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.EffectPhaseCoherent(Step(
				KingdomPurposeKind.Harvest, KingdomPurposeOperationPhase.EffectApplied,
				(int)KingdomPurposeEffectHarvestStep.Milled)));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.EffectPhaseCoherent(Step(
				KingdomPurposeKind.Harvest, KingdomPurposeOperationPhase.EffectPending,
				(int)KingdomPurposeEffectHarvestStep.Milled)));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.EffectPhaseCoherent(Step(
				KingdomPurposeKind.Deep, KingdomPurposeOperationPhase.Prepared,
				(int)KingdomPurposeEffectRefineStep.FirstRawSpent)));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.EffectPhaseCoherent(Step(
				KingdomPurposeKind.Deep, KingdomPurposeOperationPhase.Prepared,
				KingdomPurposePortfolioRules.PurposeEffectExempt)));
		}

		[Test]
		public void CurrentAndLegacyOperationWiresRoundTripWithoutReadMigration()
		{
			KingdomPurposePairReceipt pair = Pair();
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCreateOperation(pair, "operation", 1,
				KingdomPurposeKind.Deep, true, false, null, null, null, null, null,
				out KingdomPurposeOperationReceipt operation, out _));
			string current = KingdomPurposePortfolioRules.EncodeOperation(operation);
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryDecodeOperation(current,
				out KingdomPurposeOperationReceipt currentCopy));
			ClassicAssert.AreEqual(KingdomPurposePortfolioRules.PurposeEffectNone,
				currentCopy.EffectStep);
			ClassicAssert.AreEqual(current, KingdomPurposePortfolioRules.EncodeOperation(currentCopy));

			operation.EffectStep = KingdomPurposePortfolioRules.PurposeEffectExempt;
			KingdomPurposePairReceipt running = pair.Copy();
			running.BootstrapUsed = true;
			running.Phase = KingdomPurposePairPhase.BootstrapOutstanding;
			running.Operation = operation;
			running.NextOperationOrdinal++;
			running.Revision++;
			string legacy = KingdomPurposePortfolioRules.EncodeLegacyPair(running);
			ClassicAssert.IsNotNull(legacy);
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryDecodePair(legacy, out _));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryDecodePairAny(legacy,
				out KingdomPurposePairReceipt legacyCopy, out bool wasLegacy));
			ClassicAssert.IsTrue(wasLegacy);
			ClassicAssert.IsTrue(legacyCopy.LegacyWire);
			ClassicAssert.AreEqual(KingdomPurposePortfolioRules.PurposeEffectExempt,
				legacyCopy.Operation.EffectStep);
			ClassicAssert.AreEqual(legacy, KingdomPurposePortfolioRules.EncodeLegacyPair(legacyCopy));
			ClassicAssert.IsNotNull(KingdomPurposePortfolioRules.EncodePair(legacyCopy));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryDecodePairAny(legacy + "x",
				out _, out _));
		}

		[Test]
		public void EffectEvidenceCodecsAreCanonicalAndPresenceProtects()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectReceipt("pair", 2,
				"operation", KingdomPurposeKind.Harvest, out string receipt));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectAttempt(receipt, 4,
				KingdomPurposeEffectCallbackKind.HarvestStaple, "object", 2, 4, 3,
				D, E,
				out string witness));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryReadEffectAttempt(witness, receipt,
				out KingdomPurposeEffectAttempt attempt));
			ClassicAssert.AreEqual(witness, KingdomPurposePortfolioRules.EncodeEffectAttempt(attempt));
			ClassicAssert.IsFalse(KingdomPurposePortfolioRules.TryReadEffectAttempt(witness + "x",
				receipt, out _));
			KingdomPurposeEffectProductRecord record = new KingdomPurposeEffectProductRecord
				{ Seed = 1, Staple = 6 };
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryEffectProductRecord(receipt, record,
				out string encoded));
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryReadEffectProductRecord(encoded,
				receipt, out KingdomPurposeEffectProductRecord copy));
			ClassicAssert.AreEqual(1, copy.Seed);
			ClassicAssert.AreEqual(6, copy.Staple);
			KingdomPurposeCargoEvidence evidence = new KingdomPurposeCargoEvidence
				{ EffectMark = true };
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.PurposeCargoIsProtected(evidence));
		}

		private static KingdomPurposeOperationReceipt Step(KingdomPurposeKind kind,
			KingdomPurposeOperationPhase phase, int step)
		{
			return new KingdomPurposeOperationReceipt
				{ SourceKind = kind, Phase = phase, EffectStep = step };
		}

		private static KingdomPurposePairReceipt Pair()
		{
			ClassicAssert.IsTrue(KingdomPurposePortfolioRules.TryCreatePair("pair", "realm", 7,
				KingdomPurposeKind.Deep, KingdomPurposeKind.Forge, "city-a", "city-b",
				"work-a", null, "zone-a", "zone-b", "input-a", "output-a", "input-b",
				"output-b", "gate-a", "gate-b", D, out KingdomPurposePairReceipt pair,
				out _));
			return pair;
		}
	}
}
#endif
