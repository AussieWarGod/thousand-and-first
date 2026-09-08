#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Engine-free proof of material receipt planning, accounting and phase laws.</summary>
	public class KingdomMaterialDebitRulesTests
	{
		[Test]
		public void DebitDeclarationsKeepTheirPersistedAndPublicAbi()
		{
			AssertEnum(typeof(KingdomMaterialDebitSourceKind), 0, 1, 2, 3);
			AssertEnum(typeof(KingdomMaterialDebitOutcome), 0, 1, 2, 3, 4, 5, 6, 7);
			AssertEnum(typeof(KingdomMaterialDebitFault), 0, 1, 2, 3, 4, 5, 6, 7,
				8, 9, 10, 11, 12, 13, 14);

			ClassicAssert.IsTrue(typeof(KingdomMaterialDebitCost).IsSealed);
			ClassicAssert.IsTrue(typeof(KingdomMaterialDebitSource).IsSealed);
			ClassicAssert.IsTrue(typeof(KingdomMaterialDebitStep).IsSealed);
			ClassicAssert.IsTrue(typeof(KingdomMaterialDebitPlan).IsSealed);
			ClassicAssert.IsTrue(typeof(KingdomMaterialDebitResult).IsSealed);
			ClassicAssert.IsTrue(typeof(KingdomMaterialDebitCost).GetField("Materials").IsInitOnly);
			ClassicAssert.IsTrue(typeof(KingdomMaterialDebitSource).GetField("Source").IsInitOnly);
			ClassicAssert.IsTrue(typeof(KingdomMaterialDebitStep).GetField("Taken").IsInitOnly);
			ClassicAssert.IsTrue(typeof(KingdomMaterialDebitPlan).GetField("Steps").IsInitOnly);
			ClassicAssert.IsTrue(typeof(KingdomMaterialDebitResult).GetField("Outcome").IsInitOnly);
		}

		private static void AssertEnum(Type type, params int[] expected)
		{
			ClassicAssert.AreEqual(typeof(byte), Enum.GetUnderlyingType(type), type.FullName);
			Array values = Enum.GetValues(type);
			ClassicAssert.AreEqual(expected.Length, values.Length, type.FullName);
			for (int i = 0; i < expected.Length; i++)
			{
				ClassicAssert.AreEqual(expected[i], Convert.ToInt32(values.GetValue(i)),
					type.FullName + "[" + i + "]");
			}
		}

		private static string ReadRepoSource(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static KingdomMaterialTally Materials(KingdomMaterial Kind, int Count)
		{
			KingdomMaterialTally tally = new KingdomMaterialTally();
			tally.Set(Kind, Count);
			return tally;
		}

		private static KingdomBitTally Bits(int Tier, int Count)
		{
			KingdomBitTally tally = new KingdomBitTally();
			tally.Set(Tier, Count);
			return tally;
		}

		private static KingdomExoticTally Exotics(KingdomExotic Kind, int Count)
		{
			KingdomExoticTally tally = new KingdomExoticTally();
			tally.Set(Kind, Count);
			return tally;
		}

		private static KingdomMaterialDebitSource MaterialSource(int Source,
			KingdomMaterial Kind, int Count, KingdomBitTally AlsoWorth = null)
		{
			return new KingdomMaterialDebitSource(Source, KingdomMaterialDebitSourceKind.Material,
				(int)Kind, Count, AlsoWorth);
		}

		private static KingdomMaterialDebitSource ExoticSource(int Source,
			KingdomExotic Kind, int Count)
		{
			return new KingdomMaterialDebitSource(Source, KingdomMaterialDebitSourceKind.Exotic,
				(int)Kind, Count);
		}

		private static KingdomMaterialDebitSource BitSource(int Source, int Count,
			KingdomBitTally UnitWorth)
		{
			return new KingdomMaterialDebitSource(Source, KingdomMaterialDebitSourceKind.BitStock,
				0, Count, UnitWorth);
		}

		private static KingdomMaterialDebitPlan Plan(KingdomMaterialDebitCost Cost,
			params KingdomMaterialDebitSource[] Sources)
		{
			KingdomMaterialDebitPlan plan;
			KingdomMaterialDebitFault fault;
			ClassicAssert.IsTrue(KingdomMaterialDebitRules.TryPlan(Cost, Sources, out plan, out fault), fault.ToString());
			ClassicAssert.AreEqual(KingdomMaterialDebitFault.None, fault);
			return plan;
		}

		[Test]
		public void ClaimString_RoundTripsEveryLaneAndIsIndependent()
		{
			KingdomMaterialTally materials = Materials(KingdomMaterial.Timber, 7);
			materials.Set(KingdomMaterial.Stone, 3);
			KingdomBitTally bits = Bits(0, 2);
			bits.Set(8, 1);
			KingdomExoticTally exotics = Exotics(KingdomExotic.Gem, 4);
			KingdomMaterialDebitCost original = new KingdomMaterialDebitCost(materials, bits, exotics);
			string encoded = original.ToClaimString();
			KingdomMaterialDebitCost parsed;
			ClassicAssert.IsTrue(KingdomMaterialDebitCost.TryParseClaim(encoded, out parsed));
			ClassicAssert.AreEqual(7, parsed.Materials.Get(KingdomMaterial.Timber));
			ClassicAssert.AreEqual(3, parsed.Materials.Get(KingdomMaterial.Stone));
			ClassicAssert.AreEqual(2, parsed.Bits.Get(0));
			ClassicAssert.AreEqual(1, parsed.Bits.Get(8));
			ClassicAssert.AreEqual(4, parsed.Exotics.Get(KingdomExotic.Gem));

			materials.Set(KingdomMaterial.Timber, 99);
			bits.Set(0, 99);
			exotics.Set(KingdomExotic.Gem, 99);
			ClassicAssert.AreEqual(7, original.Materials.Get(KingdomMaterial.Timber));
			ClassicAssert.AreEqual(2, original.Bits.Get(0));
			ClassicAssert.AreEqual(4, original.Exotics.Get(KingdomExotic.Gem));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("v2|m:0|b:0|e:0")]
		[TestCase("v1|m:0|b:0|e:0")]
		[TestCase("v1|m:-1,0,0,0,0,0,0,0,0|b:0,0,0,0,0,0,0,0,0|e:0,0,0,0")]
		[TestCase("v1|m:0,0,0,0,0,0,0,0,0|b:0,0,0,0,0,0,0,0,0|e:0,0,0,no")]
		public void ClaimString_RejectsMalformedClaimsWhole(string Text)
		{
			KingdomMaterialDebitCost parsed;
			ClassicAssert.IsFalse(KingdomMaterialDebitCost.TryParseClaim(Text, out parsed));
			ClassicAssert.IsNull(parsed);
		}

		[Test]
		public void OneStackOfTwoOwingTwo_IsOneWholeFinalizer()
		{
			KingdomMaterialDebitPlan plan = Plan(
				new KingdomMaterialDebitCost(Materials(KingdomMaterial.Timber, 2)),
				MaterialSource(0, KingdomMaterial.Timber, 2));
			ClassicAssert.AreEqual(1, plan.Steps.Count);
			ClassicAssert.AreEqual(2, plan.Steps[0].Original);
			ClassicAssert.AreEqual(2, plan.Steps[0].Taken);
			ClassicAssert.AreEqual(0, plan.Steps[0].Remaining);
			ClassicAssert.IsTrue(plan.Steps[0].NeedsFinalization);
		}

		[Test]
		public void OneThenTwoOwingThree_FinalizesBothWithoutStackerDecrementAmbiguity()
		{
			KingdomMaterialDebitPlan plan = Plan(
				new KingdomMaterialDebitCost(Materials(KingdomMaterial.Stone, 3)),
				MaterialSource(0, KingdomMaterial.Stone, 1),
				MaterialSource(1, KingdomMaterial.Stone, 2));
			ClassicAssert.AreEqual(2, plan.Steps.Count);
			ClassicAssert.IsTrue(plan.Steps[0].NeedsFinalization);
			ClassicAssert.IsTrue(plan.Steps[1].NeedsFinalization);
			ClassicAssert.AreEqual(1, plan.Steps[0].Taken);
			ClassicAssert.AreEqual(2, plan.Steps[1].Taken);
		}

		[Test]
		public void PartialStackPlan_LeavesTheExactSurvivingCount()
		{
			KingdomMaterialDebitPlan plan = Plan(
				new KingdomMaterialDebitCost(Materials(KingdomMaterial.Timber, 1)),
				MaterialSource(0, KingdomMaterial.Timber, 2));
			ClassicAssert.AreEqual(1, plan.Steps[0].Taken);
			ClassicAssert.AreEqual(1, plan.Steps[0].Remaining);
			ClassicAssert.IsFalse(plan.Steps[0].NeedsFinalization);
		}

		[Test]
		public void DuplicatePhysicalReferenceId_IsCountedOnceOnly()
		{
			KingdomMaterialDebitSource[] duplicate = new KingdomMaterialDebitSource[]
			{
				MaterialSource(7, KingdomMaterial.Timber, 2),
				MaterialSource(7, KingdomMaterial.Timber, 2)
			};
			KingdomMaterialDebitPlan plan;
			KingdomMaterialDebitFault fault;
			ClassicAssert.IsFalse(KingdomMaterialDebitRules.TryPlan(
				new KingdomMaterialDebitCost(Materials(KingdomMaterial.Timber, 3)),
				duplicate, out plan, out fault));
			ClassicAssert.AreEqual(KingdomMaterialDebitFault.InsufficientMaterials, fault);
			ClassicAssert.IsNull(plan);
		}

		[Test]
		public void MaterialClaimOwnsAnObjectExclusively_ItCannotAlsoPayBits()
		{
			KingdomMaterialDebitSource tempting = MaterialSource(0, KingdomMaterial.Scrap, 1,
				Bits(0, 4));
			KingdomMaterialDebitPlan plan;
			KingdomMaterialDebitFault fault;
			ClassicAssert.IsFalse(KingdomMaterialDebitRules.TryPlan(new KingdomMaterialDebitCost(
				Materials(KingdomMaterial.Scrap, 1), Bits(0, 1)),
				new KingdomMaterialDebitSource[] { tempting }, out plan, out fault));
			ClassicAssert.AreEqual(KingdomMaterialDebitFault.InsufficientBits, fault);
			ClassicAssert.IsNull(plan);
		}

		[Test]
		public void MixedCompositePlan_UsesThreeDifferentPhysicalSources()
		{
			KingdomMaterialDebitPlan plan = Plan(new KingdomMaterialDebitCost(
				Materials(KingdomMaterial.ShapedStone, 2), Bits(3, 1),
				Exotics(KingdomExotic.Gem, 1)),
				MaterialSource(0, KingdomMaterial.ShapedStone, 3),
				ExoticSource(1, KingdomExotic.Gem, 1),
				BitSource(2, 2, Bits(3, 1)));
			ClassicAssert.AreEqual(3, plan.Steps.Count);
			ClassicAssert.AreEqual(KingdomMaterialDebitSourceKind.Material, plan.Steps[0].Kind);
			ClassicAssert.AreEqual(KingdomMaterialDebitSourceKind.Exotic, plan.Steps[1].Kind);
			ClassicAssert.AreEqual(KingdomMaterialDebitSourceKind.BitStock, plan.Steps[2].Kind);
			ClassicAssert.AreEqual(2, plan.Steps[0].Taken);
			ClassicAssert.AreEqual(1, plan.Steps[1].Taken);
			ClassicAssert.AreEqual(1, plan.Steps[2].Taken);
		}

		[Test]
		public void BitsUseCheapestUnitFirst_ThenStableSourceOrder()
		{
			KingdomBitTally costly = Bits(0, 1);
			costly.Set(8, 1);
			KingdomBitTally cheap = Bits(0, 1);
			KingdomMaterialDebitPlan plan = Plan(
				new KingdomMaterialDebitCost(null, Bits(0, 2)),
				BitSource(0, 1, costly),
				BitSource(2, 1, cheap),
				BitSource(1, 1, cheap));
			ClassicAssert.AreEqual(2, plan.Steps.Count);
			ClassicAssert.AreEqual(1, plan.Steps[0].Source, "equal cheap sources use stable source order");
			ClassicAssert.AreEqual(2, plan.Steps[1].Source);
		}

		[Test]
		public void BitSurplus_IsLostButNeverCreditedTwice()
		{
			KingdomMaterialDebitPlan plan = Plan(
				new KingdomMaterialDebitCost(null, Bits(0, 1)),
				BitSource(0, 1, Bits(0, 2)));
			KingdomMaterialDebitResult result = KingdomMaterialDebitRules.Classify(plan,
				new int[] { 1 }, new bool[] { false }, KingdomMaterialDebitFault.None, null);
			ClassicAssert.AreEqual(KingdomMaterialDebitOutcome.ExactCommit, result.Outcome);
			ClassicAssert.AreEqual(1, result.Spent.Bits.Get(0));
			ClassicAssert.AreEqual(2, result.Lost.Bits.Get(0));
			ClassicAssert.AreEqual(0, result.Outstanding.Bits.Get(0));
		}

		[Test]
		public void NthTerminalVeto_IsCleanBeforeAnyFinalizerAndIrreversibleAfterOne()
		{
			KingdomMaterialDebitPlan plan = Plan(
				new KingdomMaterialDebitCost(Materials(KingdomMaterial.Stone, 3)),
				MaterialSource(0, KingdomMaterial.Stone, 1),
				MaterialSource(1, KingdomMaterial.Stone, 1),
				MaterialSource(2, KingdomMaterial.Stone, 1));
			KingdomMaterialDebitResult firstVeto = KingdomMaterialDebitRules.Classify(plan,
				new int[] { 0, 0, 0 }, new bool[] { true, true, true },
				KingdomMaterialDebitFault.OperationRefused, "first");
			ClassicAssert.AreEqual(KingdomMaterialDebitOutcome.CleanRefusal, firstVeto.Outcome);
			ClassicAssert.IsTrue(firstVeto.Spent.IsEmpty);

			KingdomMaterialDebitResult secondVeto = KingdomMaterialDebitRules.Classify(plan,
				new int[] { 1, 0, 0 }, new bool[] { false, true, true },
				KingdomMaterialDebitFault.OperationRefused, "second");
			ClassicAssert.AreEqual(KingdomMaterialDebitOutcome.IrreversiblePartial, secondVeto.Outcome);
			ClassicAssert.AreEqual(1, secondVeto.FinalizedSources);
			ClassicAssert.AreEqual(1, secondVeto.Spent.Materials.Get(KingdomMaterial.Stone));
			ClassicAssert.AreEqual(2, secondVeto.Outstanding.Materials.Get(KingdomMaterial.Stone));
			ClassicAssert.IsTrue(secondVeto.MeasurementExact);

			KingdomMaterialDebitResult exact = KingdomMaterialDebitRules.Classify(plan,
				new int[] { 1, 1, 1 }, new bool[] { false, false, false },
				KingdomMaterialDebitFault.None, null);
			ClassicAssert.AreEqual(KingdomMaterialDebitOutcome.ExactCommit, exact.Outcome);
		}

		[Test]
		public void EarlierPartialStackLoss_IsRecoverableWhenLaterCategoryRefuses()
		{
			KingdomMaterialDebitPlan plan = Plan(new KingdomMaterialDebitCost(
				Materials(KingdomMaterial.Timber, 1), null,
				Exotics(KingdomExotic.Gold, 1)),
				MaterialSource(0, KingdomMaterial.Timber, 2),
				ExoticSource(1, KingdomExotic.Gold, 1));
			KingdomMaterialDebitResult result = KingdomMaterialDebitRules.Classify(plan,
				new int[] { 1, 0 }, new bool[] { true, true },
				KingdomMaterialDebitFault.OperationRefused, "later veto");
			ClassicAssert.AreEqual(KingdomMaterialDebitOutcome.RecoverablePartial, result.Outcome);
			ClassicAssert.IsTrue(KingdomMaterialDebitRules.CanCompensate(plan,
				new int[] { 1, 0 }, new int[] { 1, 1 }, new bool[] { true, true }));
		}

		[Test]
		public void EverySmallPartialFaultShape_HasOneHonestTerminalClassification()
		{
			KingdomMaterialTally price = Materials(KingdomMaterial.Mud, 1);
			price.Set(KingdomMaterial.Brush, 1);
			price.Set(KingdomMaterial.Timber, 1);
			KingdomMaterialDebitPlan plan = Plan(new KingdomMaterialDebitCost(price),
				MaterialSource(0, KingdomMaterial.Mud, 2),
				MaterialSource(1, KingdomMaterial.Brush, 2),
				MaterialSource(2, KingdomMaterial.Timber, 2));
			for (int a = 0; a <= 2; a++)
			for (int b = 0; b <= 2; b++)
			for (int c = 0; c <= 2; c++)
			for (int mask = 0; mask < 8; mask++)
			{
				int[] removed = new int[] { a, b, c };
				bool[] same = new bool[]
				{
					(mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0
				};
				KingdomMaterialDebitResult result = KingdomMaterialDebitRules.Classify(plan,
					removed, same, KingdomMaterialDebitFault.OperationRefused, "fault");
				bool any = a + b + c > 0;
				bool allExact = a == 1 && b == 1 && c == 1 && same[0] && same[1] && same[2];
				bool observationExact = (a == 2 || same[0]) && (b == 2 || same[1]) &&
					(c == 2 || same[2]);
				bool recoverable = any && observationExact && a < 2 && b < 2 && c < 2
					&& same[0] && same[1] && same[2] && !allExact;
				KingdomMaterialDebitOutcome expected = allExact
					? KingdomMaterialDebitOutcome.ExactCommit
					: (!any && observationExact ? KingdomMaterialDebitOutcome.CleanRefusal
						: (recoverable ? KingdomMaterialDebitOutcome.RecoverablePartial
							: KingdomMaterialDebitOutcome.IrreversiblePartial));
				ClassicAssert.AreEqual(expected, result.Outcome,
					"removed=[" + a + "," + b + "," + c + "] same=" + mask);
			}
		}

		[Test]
		public void CompensationProofRejectsEveryMismatchAndEveryFinalizedSource()
		{
			KingdomMaterialDebitPlan partial = Plan(
				new KingdomMaterialDebitCost(Materials(KingdomMaterial.Timber, 1)),
				MaterialSource(0, KingdomMaterial.Timber, 2));
			ClassicAssert.IsTrue(KingdomMaterialDebitRules.CanCompensate(partial,
				new int[] { 1 }, new int[] { 1 }, new bool[] { true }));
			ClassicAssert.IsFalse(KingdomMaterialDebitRules.CanCompensate(partial,
				new int[] { 1 }, new int[] { 0 }, new bool[] { true }), "count changed after debit");
			ClassicAssert.IsFalse(KingdomMaterialDebitRules.CanCompensate(partial,
				new int[] { 1 }, new int[] { 1 }, new bool[] { false }), "identity or owner changed");

			KingdomMaterialDebitPlan final = Plan(
				new KingdomMaterialDebitCost(Materials(KingdomMaterial.Timber, 2)),
				MaterialSource(0, KingdomMaterial.Timber, 2));
			ClassicAssert.IsFalse(KingdomMaterialDebitRules.CanCompensate(final,
				new int[] { 2 }, new int[] { -1 }, new bool[] { false }), "graveyard identity is never recreated");
		}

		[Test]
		public void MutationBetweenReserveAndCommit_CannotReadAsExact()
		{
			KingdomMaterialDebitPlan plan = Plan(
				new KingdomMaterialDebitCost(Materials(KingdomMaterial.Marble, 1)),
				MaterialSource(0, KingdomMaterial.Marble, 2));
			KingdomMaterialDebitResult result = KingdomMaterialDebitRules.Classify(plan,
				new int[] { 0 }, new bool[] { false },
				KingdomMaterialDebitFault.SourceChanged, "mutated");
			ClassicAssert.AreEqual(KingdomMaterialDebitOutcome.IrreversiblePartial, result.Outcome);
			ClassicAssert.IsFalse(result.Exact);
			ClassicAssert.IsFalse(result.MeasurementExact);
			ClassicAssert.AreEqual(1, result.Outstanding.Materials.Get(KingdomMaterial.Marble));
		}

		[Test]
		public void LateUnknownCallbackPreservesEarlierExactPartialButQuarantinesRemainder()
		{
			KingdomMaterialDebitPlan plan = Plan(
				new KingdomMaterialDebitCost(Materials(KingdomMaterial.Stone, 2)),
				MaterialSource(0, KingdomMaterial.Stone, 1),
				MaterialSource(1, KingdomMaterial.Stone, 1));
			KingdomMaterialDebitResult result = KingdomMaterialDebitRules.Classify(plan,
				new int[] { 1, 0 }, new bool[] { false, false },
				new bool[] { true, false }, KingdomMaterialDebitFault.SourceChanged, "late callback");
			ClassicAssert.AreEqual(KingdomMaterialDebitOutcome.IrreversiblePartial, result.Outcome);
			ClassicAssert.AreEqual(1, result.Spent.Materials.Get(KingdomMaterial.Stone));
			ClassicAssert.AreEqual(1, result.Outstanding.Materials.Get(KingdomMaterial.Stone));
			ClassicAssert.AreEqual(1, result.FinalizedSources);
			ClassicAssert.IsFalse(result.MeasurementExact);
		}

		[Test]
		public void LiveTerminalPathHasOneDestructiveCallbackAndRunsAfterStackWork()
		{
			string source = KingdomMaterialDebitLogicalSource.Read();
			ClassicAssert.IsFalse(source.Contains("BeforeDestroyObjectEvent.Check"),
				"Calling Check before Obliterate dispatches destructive callbacks twice.");
			int obliterate = source.IndexOf("entry.Item.Obliterate", StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(obliterate, 0);
			ClassicAssert.AreEqual(-1, source.IndexOf("entry.Item.Obliterate", obliterate + 1,
				StringComparison.Ordinal));
			int stackWork = source.IndexOf("Nonterminal stack work first", StringComparison.Ordinal);
			int terminalWork = source.IndexOf("Whole sources are necessarily irreversible",
				StringComparison.Ordinal);
			ClassicAssert.Greater(terminalWork, stackWork);
			ClassicAssert.Greater(obliterate, terminalWork);
		}

		[Test]
		public void LiveDebitLogicalSourceKeepsOneNestedDeclarationSetAndTransactionOrder()
		{
			string source = KingdomMaterialDebitLogicalSource.Read();
			ClassicAssert.AreEqual(1, Count(source, "private sealed class HeldWitness"));
			ClassicAssert.AreEqual(1, Count(source, "private sealed class ContainerWitness"));
			ClassicAssert.AreEqual(1, Count(source, "private sealed class Entry"));
			int reserve = source.IndexOf("internal static KingdomMaterialDebit Reserve(",
				StringComparison.Ordinal);
			int commit = source.IndexOf("public KingdomMaterialDebitResult Commit()",
				StringComparison.Ordinal);
			int compensate = source.IndexOf("public KingdomMaterialDebitResult Compensate()",
				StringComparison.Ordinal);
			int cancel = source.IndexOf("public KingdomMaterialDebitResult Cancel()",
				StringComparison.Ordinal);
			int snapshot = source.IndexOf("private List<KingdomMaterialDebitSource> SnapshotSources()",
				StringComparison.Ordinal);
			ClassicAssert.Greater(commit, reserve);
			ClassicAssert.Greater(compensate, commit);
			ClassicAssert.Greater(cancel, compensate);
			ClassicAssert.Greater(snapshot, cancel);
		}

		private static int Count(string source, string term)
		{
			int count = 0;
			int cursor = -1;
			while ((cursor = source.IndexOf(term, cursor + 1, StringComparison.Ordinal)) >= 0)
			{
				count++;
			}
			return count;
		}

		[Test]
		public void EverySmallMaterialShapePlansExactlyOrLeavesNoPlan()
		{
			for (int first = 0; first <= 4; first++)
			for (int second = 0; second <= 4; second++)
			for (int owed = 0; owed <= 10; owed++)
			{
				List<KingdomMaterialDebitSource> sources = new List<KingdomMaterialDebitSource>();
				if (first > 0) sources.Add(MaterialSource(0, KingdomMaterial.Brush, first));
				if (second > 0) sources.Add(MaterialSource(1, KingdomMaterial.Brush, second));
				KingdomMaterialDebitPlan plan;
				KingdomMaterialDebitFault fault;
				bool actual = KingdomMaterialDebitRules.TryPlan(
					new KingdomMaterialDebitCost(Materials(KingdomMaterial.Brush, owed)),
					sources, out plan, out fault);
				ClassicAssert.AreEqual(owed <= first + second, actual,
					"[" + first + "," + second + "] owed " + owed);
				if (!actual)
				{
					ClassicAssert.IsNull(plan);
					ClassicAssert.AreEqual(KingdomMaterialDebitFault.InsufficientMaterials, fault);
					continue;
				}
				int taken = 0;
				for (int i = 0; i < plan.Steps.Count; i++) taken += plan.Steps[i].Taken;
				ClassicAssert.AreEqual(owed, taken);
			}
		}

		[Test]
		public void TallyArithmeticSaturatesInsteadOfWrappingAReceiptEmpty()
		{
			KingdomMaterialTally materials = Materials(KingdomMaterial.Mud, int.MaxValue);
			materials.Add(KingdomMaterial.Mud, 1);
			ClassicAssert.AreEqual(int.MaxValue, materials.Get(KingdomMaterial.Mud));
			ClassicAssert.IsFalse(materials.IsEmpty());

			KingdomBitTally bits = Bits(0, int.MaxValue);
			bits.Add(0, 1);
			ClassicAssert.AreEqual(int.MaxValue, bits.Get(0));
			ClassicAssert.IsFalse(bits.IsEmpty());

			KingdomExoticTally exotics = Exotics(KingdomExotic.Gem, int.MaxValue);
			exotics.Add(KingdomExotic.Gem, 1);
			ClassicAssert.AreEqual(int.MaxValue, exotics.Get(KingdomExotic.Gem));
			ClassicAssert.IsFalse(exotics.IsEmpty());
		}
	}
}
#endif
