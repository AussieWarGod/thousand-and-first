#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPurposeEffectTransactionTests
	{
		[Test]
		public void ProductCallbackTruthTableUsesMeasuredAftermathOnly()
		{
			for (int offered = 0; offered <= 1; offered++)
			for (int threw = 0; threw <= 1; threw++)
			for (int settled = 0; settled <= 1; settled++)
			for (int unowned = 0; unowned <= 1; unowned++)
			{
				KingdomPurposeEffectCallbackAftermath expected = offered == 0
					? KingdomPurposeEffectCallbackAftermath.Unavailable
					: threw == 1 ? KingdomPurposeEffectCallbackAftermath.Ambiguous
					: settled == 1 ? KingdomPurposeEffectCallbackAftermath.Settled
					: unowned == 1 ? KingdomPurposeEffectCallbackAftermath.Unavailable
					: KingdomPurposeEffectCallbackAftermath.Ambiguous;
				Assert.AreEqual(expected,
					KingdomPurposePortfolioRules.ClassifyEffectProductAftermath(
						offered == 1, threw == 1, settled == 1, unowned == 1),
					"product " + offered + threw + settled + unowned);
			}
		}

		[Test]
		public void DebitCallbackTruthTableNeverTrustsCallbackReport()
		{
			for (int offered = 0; offered <= 1; offered++)
			for (int threw = 0; threw <= 1; threw++)
			for (int before = 0; before <= 1; before++)
			for (int after = 0; after <= 1; after++)
			{
				KingdomPurposeEffectCallbackAftermath expected = offered == 0
					? KingdomPurposeEffectCallbackAftermath.Unavailable
					: threw == 1 ? KingdomPurposeEffectCallbackAftermath.Ambiguous
					: after == 1 ? KingdomPurposeEffectCallbackAftermath.Settled
					: before == 1 ? KingdomPurposeEffectCallbackAftermath.Unavailable
					: KingdomPurposeEffectCallbackAftermath.Ambiguous;
				Assert.AreEqual(expected,
					KingdomPurposePortfolioRules.ClassifyEffectDebitAftermath(
						offered == 1, threw == 1, before == 1, after == 1),
					"debit " + offered + threw + before + after);
			}
		}

		[Test]
		public void AttemptTruthTableMakesFaultAndForeignEvidenceDominant()
		{
			for (int present = 0; present <= 1; present++)
			for (int ours = 0; ours <= 1; ours++)
			for (int before = 0; before <= 1; before++)
			for (int after = 0; after <= 1; after++)
			for (int fault = 0; fault <= 1; fault++)
			{
				KingdomPurposeEffectAttemptState expected = fault == 1
					? KingdomPurposeEffectAttemptState.Ambiguous
					: present == 0 ? KingdomPurposeEffectAttemptState.Clear
					: ours == 0 ? KingdomPurposeEffectAttemptState.Ambiguous
					: after == 1 ? KingdomPurposeEffectAttemptState.Settled
					: before == 1 ? KingdomPurposeEffectAttemptState.Before
					: KingdomPurposeEffectAttemptState.Ambiguous;
				Assert.AreEqual(expected, KingdomPurposePortfolioRules.ClassifyEffectAttempt(
					present == 1, ours == 1, before == 1, after == 1, fault == 1),
					"attempt " + present + ours + before + after + fault);
			}
		}

		[Test]
		public void EveryOwedKindComposesItsWholeLadderWithoutReset()
		{
			KingdomPurposeKind[] kinds = new KingdomPurposeKind[]
			{
				KingdomPurposeKind.Deep, KingdomPurposeKind.Forge, KingdomPurposeKind.Harvest
			};
			for (int k = 0; k < kinds.Length; k++)
			{
				Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectTerminalStep(
					kinds[k], out int terminal));
				KingdomPurposeOperationReceipt before = Effect(kinds[k], 0,
					KingdomPurposeOperationPhase.EffectPending);
				for (int step = 1; step <= terminal; step++)
				{
					KingdomPurposeOperationReceipt after = Effect(kinds[k], step,
						step == terminal ? KingdomPurposeOperationPhase.EffectApplied
							: KingdomPurposeOperationPhase.EffectPending);
					Assert.IsTrue(KingdomPurposePortfolioRules.EffectStepMonotone(before, after),
						kinds[k] + " step " + step);
					Assert.IsTrue(KingdomPurposePortfolioRules.EffectPhaseCoherent(after));
					before = after;
				}
				Assert.AreEqual(terminal, before.EffectStep);
				Assert.AreEqual(KingdomPurposeOperationPhase.EffectApplied, before.Phase);
			}
		}

		[Test]
		public void SkipBacktrackAndWrongTerminalEdgesAlwaysRefuse()
		{
			KingdomPurposeKind[] kinds = new KingdomPurposeKind[]
			{
				KingdomPurposeKind.Deep, KingdomPurposeKind.Forge, KingdomPurposeKind.Harvest
			};
			for (int k = 0; k < kinds.Length; k++)
			{
				Assert.IsTrue(KingdomPurposePortfolioRules.TryEffectTerminalStep(
					kinds[k], out int terminal));
				for (int step = 0; step < terminal; step++)
				{
					KingdomPurposeOperationReceipt before = Effect(kinds[k], step,
						KingdomPurposeOperationPhase.EffectPending);
					if (step + 2 <= terminal)
						Assert.IsFalse(KingdomPurposePortfolioRules.EffectStepMonotone(before,
							Effect(kinds[k], step + 2,
								KingdomPurposeOperationPhase.EffectPending)));
					if (step > 0)
						Assert.IsFalse(KingdomPurposePortfolioRules.EffectStepMonotone(before,
							Effect(kinds[k], step - 1,
								KingdomPurposeOperationPhase.EffectPending)));
					if (step < terminal - 1)
						Assert.IsFalse(KingdomPurposePortfolioRules.EffectStepMonotone(before,
							Effect(kinds[k], step + 1,
								KingdomPurposeOperationPhase.EffectApplied)));
				}
				Assert.IsFalse(KingdomPurposePortfolioRules.EffectStepMonotone(
					Effect(kinds[k], terminal - 1, KingdomPurposeOperationPhase.EffectPending),
					Effect(kinds[k], terminal, KingdomPurposeOperationPhase.EffectPending)));
			}
		}

		[Test]
		public void LegacyExemptIsAbsorbingAndNeverReachableFromCurrentWork()
		{
			for (int rawKind = (int)KingdomPurposeKind.Flesh;
				rawKind <= (int)KingdomPurposeKind.Harvest; rawKind++)
			{
				KingdomPurposeKind kind = (KingdomPurposeKind)rawKind;
				KingdomPurposeOperationReceipt exempt = Effect(kind,
					KingdomPurposePortfolioRules.PurposeEffectExempt,
					KingdomPurposeOperationPhase.Prepared);
				Assert.IsTrue(KingdomPurposePortfolioRules.EffectStepMonotone(
					exempt, exempt.Copy()));
				Assert.IsFalse(KingdomPurposePortfolioRules.EffectStepMonotone(
					Effect(kind, KingdomPurposePortfolioRules.PurposeEffectNone,
						KingdomPurposeOperationPhase.Prepared), exempt));
			}
		}

		[Test]
		public void RecipeConservationAcceptsOnlyWholeDeclaredBatches()
		{
			for (int kind = (int)KingdomPurposeKind.Flesh;
				kind <= (int)KingdomPurposeKind.Harvest; kind++)
			for (int input = 0; input <= 4; input++)
			for (int primary = 0; primary <= 2; primary++)
			for (int staple = 0; staple <= 7; staple++)
			{
				KingdomPurposeKind typed = (KingdomPurposeKind)kind;
				bool expected = (typed == KingdomPurposeKind.Deep
					|| typed == KingdomPurposeKind.Forge)
					? input == 2 && primary == 1 && staple == 0
					: typed == KingdomPurposeKind.Harvest
						&& input == 3 && primary == 1 && staple == 6;
				Assert.AreEqual(expected, KingdomPurposePortfolioRules.EffectRecipeConserves(
					typed, input, primary, staple),
					typed + " " + input + "/" + primary + "/" + staple);
			}
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
