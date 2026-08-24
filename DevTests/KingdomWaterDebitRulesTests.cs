#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Exhaustive engine-free proof of the exact-water receipt's plan and phase laws.</summary>
	public class KingdomWaterDebitRulesTests
	{
		[Test]
		public void EverySmallPlanIsExactGreedyAndReadOnlyOrReturnsNothing()
		{
			for (int a = 0; a <= 4; a++)
			for (int b = 0; b <= 4; b++)
			for (int c = 0; c <= 4; c++)
			for (int mask = 0; mask < 64; mask++)
			for (int amount = -2; amount <= 16; amount++)
			{
				int[] volumes = new int[3] { a, b, c };
				int[] before = (int[])volumes.Clone();
				bool[] pure = new bool[3] { (mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0 };
				bool[] dedicated = new bool[3] { (mask & 8) != 0, (mask & 16) != 0, (mask & 32) != 0 };
				int available = 0;
				for (int i = 0; i < 3; i++)
				{
					if (pure[i] && dedicated[i]) available += volumes[i];
				}
				bool expected = amount <= 0 || available >= amount;

				int[] allocation;
				int total;
				KingdomWaterDebitFault fault;
				bool actual = KingdomWaterDebitRules.TryPlan(
					amount, volumes, pure, dedicated, out allocation, out total, out fault);

				Assert.AreEqual(expected, actual, "availability classification");
				CollectionAssert.AreEqual(before, volumes, "planning mutated source volumes");
				Assert.AreEqual(actual ? KingdomWaterDebitFault.None : KingdomWaterDebitFault.InsufficientWater, fault);
				Assert.AreEqual(actual && amount > 0 ? amount : 0, total);
				int remaining = (amount > 0) ? amount : 0;
				for (int i = 0; i < 3; i++)
				{
					int expectedTake = 0;
					if (actual && pure[i] && dedicated[i] && remaining > 0)
					{
						expectedTake = Math.Min(volumes[i], remaining);
						remaining -= expectedTake;
					}
					Assert.AreEqual(expectedTake, allocation[i], "allocation row " + i);
				}
			}
		}

		[Test]
		public void InvalidPlanShapesAlwaysFailClosed()
		{
			int[] allocations;
			int total;
			KingdomWaterDebitFault fault;
			Assert.IsFalse(KingdomWaterDebitRules.TryPlan(1, null, new bool[0], new bool[0], out allocations, out total, out fault));
			Assert.AreEqual(KingdomWaterDebitFault.InvalidVessels, fault);
			Assert.IsFalse(KingdomWaterDebitRules.TryPlan(1, new int[1], null, new bool[1], out allocations, out total, out fault));
			Assert.IsFalse(KingdomWaterDebitRules.TryPlan(1, new int[1], new bool[1], null, out allocations, out total, out fault));
			Assert.IsFalse(KingdomWaterDebitRules.TryPlan(1, new int[1], new bool[0], new bool[1], out allocations, out total, out fault));
			Assert.IsFalse(KingdomWaterDebitRules.TryPlan(1, new int[1], new bool[1], new bool[0], out allocations, out total, out fault));
		}

		[Test]
		public void EveryLifecycleStateHasOneExplicitCommitAndRollbackLaw()
		{
			KingdomWaterDebitState[] states = (KingdomWaterDebitState[])Enum.GetValues(typeof(KingdomWaterDebitState));
			for (int i = 0; i < states.Length; i++)
			{
				KingdomWaterDebitState state = states[i];
				KingdomWaterDebitAction commit = KingdomWaterDebitRules.CommitAction(state);
				KingdomWaterDebitAction rollback = KingdomWaterDebitRules.RollbackAction(state);
				switch (state)
				{
				case KingdomWaterDebitState.Reserved:
					Assert.AreEqual(KingdomWaterDebitAction.Drain, commit);
					Assert.AreEqual(KingdomWaterDebitAction.CancelReservation, rollback);
					break;
				case KingdomWaterDebitState.Committed:
					Assert.AreEqual(KingdomWaterDebitAction.SucceedWithoutMutation, commit);
					Assert.AreEqual(KingdomWaterDebitAction.Restore, rollback);
					break;
				case KingdomWaterDebitState.RolledBack:
					Assert.AreEqual(KingdomWaterDebitAction.Reject, commit);
					Assert.AreEqual(KingdomWaterDebitAction.SucceedWithoutMutation, rollback);
					break;
				default:
					Assert.AreEqual(KingdomWaterDebitAction.Reject, commit);
					Assert.AreEqual(KingdomWaterDebitAction.Reject, rollback);
					break;
				}
			}
		}

		[Test]
		public void ReservedRevalidationRequiresEveryOriginalFact()
		{
			for (int original = 0; original <= 3; original++)
			for (int current = 0; current <= 3; current++)
			for (int allocation = 0; allocation <= 4; allocation++)
			for (int mask = 0; mask < 16; mask++)
			{
				bool pure = (mask & 1) != 0;
				bool dedicated = (mask & 2) != 0;
				bool same = (mask & 4) != 0;
				bool capacity = (mask & 8) != 0;
				bool expected = original > 0 && current == original && allocation > 0 &&
					allocation <= original && pure && dedicated && same && capacity;
				Assert.AreEqual(expected, KingdomWaterDebitRules.EntryStillReserved(
					original, current, allocation, pure, dedicated, same, capacity));
			}
		}

		[Test]
		public void CommittedRevalidationAcceptsOnlyTheExactPostDebitShape()
		{
			for (int original = 0; original <= 3; original++)
			for (int current = 0; current <= 3; current++)
			for (int allocation = 0; allocation <= 4; allocation++)
			for (int mask = 0; mask < 16; mask++)
			{
				bool emptyOrPure = (mask & 1) != 0;
				bool dedicated = (mask & 2) != 0;
				bool same = (mask & 4) != 0;
				bool capacity = (mask & 8) != 0;
				bool expected = original > 0 && allocation > 0 && allocation <= original &&
					current == original - allocation && emptyOrPure && dedicated && same && capacity;
				Assert.AreEqual(expected, KingdomWaterDebitRules.EntryStillCommitted(
					original, current, allocation, emptyOrPure, dedicated, same, capacity));
			}
		}

		[Test]
		public void ZoneAndComponentIdentityAreRequiredAtEveryReceiptPhase()
		{
			for (int mask = 0; mask < 4; mask++)
			{
				bool sameZone = (mask & 1) != 0;
				bool sameComponents = (mask & 2) != 0;
				bool expected = sameZone && sameComponents;
				Assert.AreEqual(expected, KingdomWaterDebitRules.EntryStillReserved(
					5, 5, 2, true, true, true, true, sameZone, sameComponents));
				Assert.AreEqual(expected, KingdomWaterDebitRules.EntryStillCommitted(
					5, 3, 2, true, true, true, true, sameZone, sameComponents));
			}
		}

		[Test]
		public void DrainTransitionRequiresExactDeltaReturnStateAndBinding()
		{
			for (int before = 0; before <= 5; before++)
			for (int after = 0; after <= 5; after++)
			for (int allocation = 0; allocation <= 6; allocation++)
			for (int returned = 0; returned <= 6; returned++)
			for (int mask = 0; mask < 4; mask++)
			{
				bool state = (mask & 1) != 0;
				bool binding = (mask & 2) != 0;
				bool expected = before > 0 && allocation > 0 && allocation <= before &&
					returned == allocation && after == before - allocation && state && binding;
				Assert.AreEqual(expected, KingdomWaterDebitRules.DrainTransitionExact(
					before, after, allocation, returned, state, binding));
			}
		}

		[Test]
		public void CounterProofsAreExactInverseAndRejectBoundaries()
		{
			for (int stored = 0; stored <= 20; stored++)
			for (int space = 0; space <= 20; space++)
			for (int amount = 0; amount <= 20; amount++)
			{
				int afterStored;
				int afterSpace;
				bool commits = KingdomWaterDebitRules.TryCountersAfterCommit(
					stored, space, amount, out afterStored, out afterSpace);
				Assert.AreEqual(stored >= amount, commits);
				if (!commits) continue;
				int restoredStored;
				int restoredSpace;
				Assert.IsTrue(KingdomWaterDebitRules.TryCountersAfterRollback(
					afterStored, afterSpace, amount, out restoredStored, out restoredSpace));
				Assert.AreEqual(stored, restoredStored);
				Assert.AreEqual(space, restoredSpace);
			}

			int ignoredA;
			int ignoredB;
			Assert.IsFalse(KingdomWaterDebitRules.TryCountersAfterCommit(0, 0, -1, out ignoredA, out ignoredB));
			Assert.IsFalse(KingdomWaterDebitRules.TryCountersAfterCommit(1, int.MaxValue, 1, out ignoredA, out ignoredB));
			Assert.IsFalse(KingdomWaterDebitRules.TryCountersAfterRollback(int.MaxValue, 1, 1, out ignoredA, out ignoredB));
			Assert.IsFalse(KingdomWaterDebitRules.TryCountersAfterRollback(0, 0, 1, out ignoredA, out ignoredB));
		}

		[Test]
		public void ClaimClassificationCreditsOnlyProvedPhysicalLoss()
		{
			int spent;
			int outstanding;
			int lost;
			bool exact;
			Assert.IsTrue(KingdomWaterDebitRules.TryClassifyClaim(8,
				new int[2] { 5, 7 }, new int[2] { 1, 5 }, new int[2] { 4, 2 },
				new bool[2] { true, true }, new bool[2] { true, true },
				out spent, out outstanding, out lost, out exact));
			Assert.AreEqual(6, spent);
			Assert.AreEqual(2, outstanding);
			Assert.AreEqual(6, lost);
			Assert.IsTrue(exact);

			// A vanished second vessel makes the whole requested credit unsafe. Exact loss in
			// the first row remains diagnostic, but no spend/outstanding split may invite retry.
			Assert.IsTrue(KingdomWaterDebitRules.TryClassifyClaim(8,
				new int[2] { 5, 7 }, new int[2] { 1, -1 }, new int[2] { 4, 1 },
				new bool[2] { true, false }, new bool[2] { true, false },
				out spent, out outstanding, out lost, out exact));
			Assert.AreEqual(0, spent);
			Assert.AreEqual(8, outstanding);
			Assert.AreEqual(4, lost);
			Assert.IsFalse(exact);
		}

		[Test]
		public void ClaimClassificationSaturatesAndRejectsMalformedShapes()
		{
			int spent;
			int outstanding;
			int lost;
			bool exact;
			Assert.IsTrue(KingdomWaterDebitRules.TryClassifyClaim(int.MaxValue,
				new int[2] { int.MaxValue, int.MaxValue }, new int[2] { 0, 0 },
				new int[2] { int.MaxValue, int.MaxValue }, new bool[2] { true, true },
				new bool[2] { true, true }, out spent, out outstanding, out lost, out exact));
			Assert.AreEqual(int.MaxValue, spent);
			Assert.AreEqual(0, outstanding);
			Assert.AreEqual(int.MaxValue, lost);
			Assert.IsTrue(exact);
			Assert.IsFalse(KingdomWaterDebitRules.TryClassifyClaim(1,
				new int[1], new int[0], new int[1], new bool[1], new bool[1],
				out spent, out outstanding, out lost, out exact));
			Assert.IsFalse(exact);
		}
	}
}
#endif
