#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomStandingRulesTests
	{
		[Test]
		public void FractionalSpilloverIsSignedAndPartitionIndependent()
		{
			int standing = 0;
			int remainder = 0;
			for (int i = 0; i < 10; i++)
				Assert.IsTrue(KingdomStandingRules.TrySpillover(standing, remainder,
					0, 1, GrowthStage.City, out standing, out remainder));
			Assert.AreEqual(1, standing);
			Assert.AreEqual(0, remainder);

			Assert.IsTrue(KingdomStandingRules.TrySpillover(0, 0, 0, 10,
				GrowthStage.City, out int wholeStanding, out int wholeRemainder));
			Assert.AreEqual(standing, wholeStanding);
			Assert.AreEqual(remainder, wholeRemainder);

			for (int i = 0; i < 10; i++)
				Assert.IsTrue(KingdomStandingRules.TrySpillover(standing, remainder,
					0, -1, GrowthStage.City, out standing, out remainder));
			Assert.AreEqual(0, standing);
			Assert.AreEqual(0, remainder);
		}

		[TestCase(GrowthStage.Camp, 9, 4, 50)]
		[TestCase(GrowthStage.Steading, 9, 3, 60)]
		[TestCase(GrowthStage.Village, -9, -2, -70)]
		[TestCase(GrowthStage.Town, -9, -1, -80)]
		[TestCase(GrowthStage.City, 9, 0, 90)]
		public void CarriesExactHundredthsAcrossStages(GrowthStage stage, int delta,
			int expectedStanding, int expectedRemainder)
		{
			Assert.IsTrue(KingdomStandingRules.TrySpillover(0, 0, 0, delta, stage,
				out int standing, out int remainder));
			Assert.AreEqual(expectedStanding, standing);
			Assert.AreEqual(expectedRemainder, remainder);
		}

		[Test]
		public void ArithmeticSaturatesWithoutWrappingOrRetainingUnpayableCarry()
		{
			Assert.IsTrue(KingdomStandingRules.TrySpillover(int.MaxValue - 1, 99,
				int.MinValue, int.MaxValue, GrowthStage.Camp,
				out int high, out int highRemainder));
			Assert.AreEqual(int.MaxValue, high);
			Assert.AreEqual(0, highRemainder);
			Assert.IsTrue(KingdomStandingRules.TrySpillover(int.MinValue + 1, -99,
				int.MaxValue, int.MinValue, GrowthStage.Camp,
				out int low, out int lowRemainder));
			Assert.AreEqual(int.MinValue, low);
			Assert.AreEqual(0, lowRemainder);
			Assert.AreEqual(int.MaxValue,
				KingdomStandingRules.SaturatingAdd(int.MaxValue, 1));
			Assert.AreEqual(int.MinValue,
				KingdomStandingRules.SaturatingAdd(int.MinValue, -1));
		}

		[Test]
		public void RejectsInvalidCarryStageAndReservedDirections()
		{
			Assert.IsFalse(KingdomStandingRules.TrySpillover(0, 100, 0, 1,
				GrowthStage.Camp, out _, out _));
			Assert.IsFalse(KingdomStandingRules.TrySpillover(0, 0, 0, 1,
				(GrowthStage)99, out _, out _));
			Assert.IsFalse(KingdomStandingRules.EligibleForeignFaction("Player", "realm"));
			Assert.IsFalse(KingdomStandingRules.EligibleForeignFaction("realm", "realm"));
			Assert.IsFalse(KingdomStandingRules.EligibleForeignFaction("*", "realm"));
			Assert.IsTrue(KingdomStandingRules.EligibleForeignFaction("Joppa", "realm"));
		}

		[Test]
		public void SignedFragmentationMatchesOneBatchForEveryStage()
		{
			int[] fragments = { 17, -9, 1, 1, 40, -3, 22, -11, 7, 7, -2 };
			int total = 0;
			for (int i = 0; i < fragments.Length; i++) total += fragments[i];
			foreach (GrowthStage stage in new[] { GrowthStage.Camp, GrowthStage.Steading,
				GrowthStage.Village, GrowthStage.Town, GrowthStage.City })
			{
				Assert.IsTrue(KingdomStandingRules.TrySpillover(123, 37, 0, total,
					stage, out int batchedStanding, out int batchedRemainder));
				int standing = 123;
				int remainder = 37;
				for (int i = 0; i < fragments.Length; i++)
					Assert.IsTrue(KingdomStandingRules.TrySpillover(standing, remainder,
						0, fragments[i], stage, out standing, out remainder));
				Assert.AreEqual(batchedStanding, standing, stage + " standing");
				Assert.AreEqual(batchedRemainder, remainder, stage + " remainder");

				Assert.IsTrue(KingdomStandingRules.TrySpillover(-123, -37, 0, -total,
					stage, out batchedStanding, out batchedRemainder));
				standing = -123;
				remainder = -37;
				for (int i = fragments.Length - 1; i >= 0; i--)
					Assert.IsTrue(KingdomStandingRules.TrySpillover(standing, remainder,
						0, -fragments[i], stage, out standing, out remainder));
				Assert.AreEqual(batchedStanding, standing, stage + " negative standing");
				Assert.AreEqual(batchedRemainder, remainder, stage + " negative remainder");
			}
		}

		[Test]
		public void StageChangesCarryTheExactSignedWeightedSum()
		{
			int[] deltas = { 3, -8, 11, -2, 17, -9, 1 };
			GrowthStage[] stages = { GrowthStage.Camp, GrowthStage.Steading,
				GrowthStage.Village, GrowthStage.Town, GrowthStage.City,
				GrowthStage.Steading, GrowthStage.City };
			int standing = 40;
			int remainder = 63;
			long scaled = (long)standing * KingdomStandingRules.FractionScale + remainder;
			for (int i = 0; i < deltas.Length; i++)
			{
				scaled += (long)deltas[i] * KingdomRules.SpilloverPercent(stages[i]);
				Assert.IsTrue(KingdomStandingRules.TrySpillover(standing, remainder,
					0, deltas[i], stages[i], out standing, out remainder));
			}
			Assert.AreEqual(scaled / KingdomStandingRules.FractionScale, standing);
			Assert.AreEqual(scaled % KingdomStandingRules.FractionScale, remainder);
		}

		[Test]
		public void PersistedPairHasOneCanonicalQuotientAndNoOutwardBoundaryDebt()
		{
			Assert.IsTrue(KingdomStandingRules.CanonicalPair(0, 99));
			Assert.IsTrue(KingdomStandingRules.CanonicalPair(0, -99));
			Assert.IsTrue(KingdomStandingRules.CanonicalPair(10, 50));
			Assert.IsTrue(KingdomStandingRules.CanonicalPair(-10, -50));
			Assert.IsFalse(KingdomStandingRules.CanonicalPair(1, -50));
			Assert.IsFalse(KingdomStandingRules.CanonicalPair(-1, 50));
			Assert.IsFalse(KingdomStandingRules.CanonicalPair(int.MaxValue, 1));
			Assert.IsFalse(KingdomStandingRules.CanonicalPair(int.MinValue, -1));

			Dictionary<string, int> regard = new Dictionary<string, int> { ["Joppa"] = 1 };
			Dictionary<string, int> carry = new Dictionary<string, int> { ["Joppa"] = -50 };
			Assert.IsFalse(KingdomStandingRules.CanonicalPairs(regard, carry));
			carry["Joppa"] = 0;
			Assert.IsFalse(KingdomStandingRules.CanonicalPairs(regard, carry),
				"zero carry rows must be omitted");
			carry.Clear();
			Assert.IsTrue(KingdomStandingRules.CanonicalPairs(regard, carry));
		}

		[Test]
		public void WholePointAdjustmentPreservesScaledCarryAndClearsItWhenClipping()
		{
			Assert.IsTrue(KingdomStandingRules.TryAdjustPair(0, 50, -1,
				out int crossed, out int crossedCarry));
			Assert.AreEqual(0, crossed);
			Assert.AreEqual(-50, crossedCarry);
			Assert.IsTrue(KingdomStandingRules.TryAdjustPair(int.MaxValue - 1, 99, 1,
				out int clipped, out int clippedCarry));
			Assert.AreEqual(int.MaxValue, clipped);
			Assert.AreEqual(0, clippedCarry);
		}

		[Test]
		public void OrderAndPartitionMatchOnlyWhileNoIntermediateStepClips()
		{
			Assert.IsTrue(KingdomStandingRules.TrySpillover(400, 25, 0, 37,
				GrowthStage.City, out int whole, out int wholeCarry));
			Assert.IsTrue(KingdomStandingRules.TrySpillover(400, 25, 0, 17,
				GrowthStage.City, out int split, out int splitCarry));
			Assert.IsTrue(KingdomStandingRules.TrySpillover(split, splitCarry, 0, 20,
				GrowthStage.City, out split, out splitCarry));
			Assert.AreEqual(whole, split);
			Assert.AreEqual(wholeCarry, splitCarry);

			Assert.IsTrue(KingdomStandingRules.TrySpillover(int.MaxValue, 0, 0, 1,
				GrowthStage.City, out int clipped, out int clippedCarry));
			Assert.IsTrue(KingdomStandingRules.TrySpillover(clipped, clippedCarry, 0, -1,
				GrowthStage.City, out int reversed, out int reversedCarry));
			Assert.AreEqual(int.MaxValue - 1, reversed);
			Assert.AreEqual(90, reversedCarry);
			Assert.AreNotEqual(int.MaxValue, reversed,
				"clipping intentionally discards overflow debt and is not reversible");
		}

		[Test]
		public void EligibilityRejectsBlankAndOversizedFactionKeys()
		{
			Assert.IsFalse(KingdomStandingRules.EligibleForeignFaction(null, "realm"));
			Assert.IsFalse(KingdomStandingRules.EligibleForeignFaction("   ", "realm"));
			Assert.IsFalse(KingdomStandingRules.EligibleForeignFaction(
				new string('x', KingdomStandingRules.MaxFactionNameChars + 1), "realm"));
			Assert.IsTrue(KingdomStandingRules.EligibleForeignFaction(
				new string('x', KingdomStandingRules.MaxFactionNameChars), "realm"));
			Assert.IsFalse(KingdomStandingRules.EligibleForeignFaction("bad\ud800key",
				"realm"));
		}

		[TestCase(-100, -600)]
		[TestCase(-50, -250)]
		[TestCase(0, 0)]
		[TestCase(50, 250)]
		[TestCase(100, 600)]
		public void LegacyFeelingMigrationPreservesOnlyCanonicalEdges(int feeling,
			int expectedPolicy)
		{
			Assert.IsTrue(KingdomStandingRules.TryLegacyFeelingPolicy(feeling,
				out int policy));
			Assert.AreEqual(expectedPolicy, policy);
		}

		[TestCase(-101)]
		[TestCase(-49)]
		[TestCase(1)]
		[TestCase(49)]
		[TestCase(101)]
		public void LegacyFeelingMigrationRejectsAmbiguousResidue(int feeling)
		{
			Assert.IsFalse(KingdomStandingRules.TryLegacyFeelingPolicy(feeling, out _));
		}
	}
}
#endif
