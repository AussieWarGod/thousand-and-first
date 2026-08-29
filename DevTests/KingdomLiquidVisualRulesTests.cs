#if TAF_TESTS
using System;
using System.Collections.Generic;

using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomLiquidVisualRulesTests
	{
		[Test]
		public void EveryFrozenMaskMapsToExactSingleAndDoubleLineVocabulary()
		{
			int[] fresh = new int[16]
			{
				250, 24, 25, 179, 26, 192, 218, 195,
				27, 217, 191, 180, 196, 193, 194, 197
			};
			int[] brine = new int[16]
			{
				254, 30, 31, 186, 16, 200, 201, 204,
				17, 188, 187, 185, 205, 202, 203, 206
			};
			for (int mask = 0; mask < 16; mask++)
			{
				KingdomLiquidVisualCue waterCue;
				KingdomLiquidVisualCue brineCue;
				Assert.IsTrue(KingdomLiquidVisualRules.TryCue(mask, false, out waterCue));
				Assert.IsTrue(KingdomLiquidVisualRules.TryCue(mask, true, out brineCue));
				Assert.AreEqual(fresh[mask], waterCue.Glyph, "fresh mask " + mask);
				Assert.AreEqual(brine[mask], brineCue.Glyph, "brine mask " + mask);
				Assert.AreNotEqual(waterCue.Glyph, brineCue.Glyph,
					"liquid distinction must survive absent color at mask " + mask);
				Assert.AreEqual(KingdomLiquidVisualRules.FormOf(mask), waterCue.Form);
				string joins;
				int roundTrip;
				Assert.IsTrue(KingdomLiquidVisualRules.TryCanonicalJoins(mask, out joins));
				Assert.IsTrue(KingdomNetworkRules.TryParseJoins(joins, out roundTrip));
				Assert.AreEqual(mask, roundTrip);
			}
		}

		[Test]
		public void MalformedMasksAndDeclarationsFailClosedWithoutInventingJoins()
		{
			KingdomLiquidVisualCue cue;
			Assert.IsFalse(KingdomLiquidVisualRules.TryCue(-1, false, out cue));
			Assert.AreEqual(0, cue.Mask);
			Assert.AreEqual(250, cue.Glyph);
			Assert.IsFalse(cue.Valid);
			Assert.IsFalse(KingdomLiquidVisualRules.TryCue(16, true, out cue));
			Assert.AreEqual(254, cue.Glyph);
			Assert.IsFalse(KingdomLiquidVisualRules.TryCue("NX", false, out cue));
			Assert.AreEqual(KingdomLiquidForm.Cap, cue.Form);
			Assert.IsFalse(KingdomLiquidVisualRules.TryCue((string)null, true, out cue));
			Assert.AreEqual(0, cue.Mask);
			string joins;
			Assert.IsFalse(KingdomLiquidVisualRules.TryCanonicalJoins(16, out joins));
			Assert.IsNull(joins);
		}

		[Test]
		public void LegalChoiceSetCoversEveryMaskExactlyOnce()
		{
			HashSet<int> masks = new HashSet<int>();
			for (int choice = 0; choice < 16; choice++)
			{
				int mask;
				Assert.IsTrue(KingdomLiquidConfigurationRules.TryMaskForChoice(choice, out mask));
				Assert.IsTrue(masks.Add(mask), "duplicate mask " + mask);
			}
			Assert.AreEqual(16, masks.Count);
			Assert.IsFalse(KingdomLiquidConfigurationRules.TryMaskForChoice(-1, out _));
			Assert.IsFalse(KingdomLiquidConfigurationRules.TryMaskForChoice(16, out _));
			Assert.AreEqual(16, KingdomLiquidConfigurationRules.Options(false).Length);
			Assert.AreEqual(16, KingdomLiquidConfigurationRules.Options(true).Length);
		}

		[Test]
		public void DeclarationPlanningEnforcesAuthorizationReadbackAndIdempotence()
		{
			string next;
			int mask;
			bool changed;
			string failure;
			Assert.IsFalse(KingdomLiquidConfigurationRules.TryPlanDeclaration("EW", 7, false,
				out next, out mask, out changed, out failure));
			Assert.AreEqual("EW", next);
			Assert.IsFalse(changed);
			StringAssert.Contains("Only the player", failure);

			// Choice 6 is east-west. Equivalent old ordering stays byte-for-byte unchanged and
			// therefore cannot dirty topology.
			Assert.IsTrue(KingdomLiquidConfigurationRules.TryPlanDeclaration("WE", 6, true,
				out next, out mask, out changed, out failure));
			Assert.AreEqual("WE", next);
			Assert.AreEqual(12, mask);
			Assert.IsFalse(changed);

			// Choice 7 is north-east.
			Assert.IsTrue(KingdomLiquidConfigurationRules.TryPlanDeclaration("EW", 7, true,
				out next, out mask, out changed, out failure));
			Assert.AreEqual("NE", next);
			Assert.AreEqual(5, mask);
			Assert.IsTrue(changed);
			Assert.IsTrue(KingdomLiquidConfigurationRules.DeclarationReadsBack(next, mask));
			Assert.IsFalse(KingdomLiquidConfigurationRules.DeclarationReadsBack("NX", mask));

			Assert.IsTrue(KingdomLiquidConfigurationRules.TryPlanDeclaration("NX", 0, true,
				out next, out mask, out changed, out failure));
			Assert.AreEqual("", next);
			Assert.IsTrue(changed, "repairing unreadable durable text is an actual change");
		}

		[Test]
		public void OldNsewRowsRemainCrossesAndReadPathsDoNotNormalizeThem()
		{
			string old = "NSEW";
			KingdomLiquidVisualCue water;
			KingdomLiquidVisualCue brine;
			Assert.IsTrue(KingdomLiquidVisualRules.TryCue(old, false, out water));
			Assert.IsTrue(KingdomLiquidVisualRules.TryCue(old, true, out brine));
			Assert.AreEqual(KingdomLiquidForm.Cross, water.Form);
			Assert.AreEqual(197, water.Glyph);
			Assert.AreEqual(206, brine.Glyph);
			KingdomLiquidConfigurationRules.Status("water", old, false);
			Assert.AreEqual("NSEW", old);
		}

		[Test]
		public void CrossingOrientationIsVisibleWhileFunctionalPairsStayIsolated()
		{
			int glyph;
			bool vertical;
			Assert.IsTrue(KingdomLiquidVisualRules.TryCrossingCue("NSEW", out glyph, out vertical));
			Assert.AreEqual(216, glyph);
			Assert.IsTrue(vertical);
			Assert.IsTrue(KingdomLiquidVisualRules.TryCrossingCue("EWNS", out glyph, out vertical));
			Assert.AreEqual(215, glyph);
			Assert.IsFalse(vertical);
			int oldMask;
			int rotatedMask;
			Assert.IsTrue(KingdomNetworkRules.TryParseJoins("NSEW", out oldMask));
			Assert.IsTrue(KingdomNetworkRules.TryParseJoins("EWNS", out rotatedMask));
			Assert.AreEqual(KingdomNetworkRules.JoinAll, oldMask);
			Assert.AreEqual(oldMask, rotatedMask);
			Assert.AreEqual(KingdomNetworkRules.JoinSouth,
				KingdomNetworkRules.CrossoverExit(rotatedMask, KingdomNetworkRules.JoinNorth));
			Assert.AreEqual(KingdomNetworkRules.JoinWest,
				KingdomNetworkRules.CrossoverExit(rotatedMask, KingdomNetworkRules.JoinEast));
			Assert.AreNotEqual(KingdomNetworkRules.JoinEast,
				KingdomNetworkRules.CrossoverExit(rotatedMask, KingdomNetworkRules.JoinNorth));
			Assert.IsFalse(KingdomLiquidVisualRules.TryCrossingCue("NSX", out glyph, out vertical));
			Assert.AreEqual(254, glyph);
		}

		[Test]
		public void CrossingPlanningEnforcesAuthorizationAndActualChangeOnly()
		{
			string next;
			bool changed;
			string failure;
			Assert.IsFalse(KingdomLiquidConfigurationRules.TryPlanCrossing("NSEW", 1, false,
				out next, out changed, out failure));
			Assert.AreEqual("NSEW", next);
			Assert.IsFalse(changed);
			Assert.IsTrue(KingdomLiquidConfigurationRules.TryPlanCrossing("NSEW", 0, true,
				out next, out changed, out failure));
			Assert.IsFalse(changed);
			Assert.IsTrue(KingdomLiquidConfigurationRules.TryPlanCrossing("NSEW", 1, true,
				out next, out changed, out failure));
			Assert.AreEqual("EWNS", next);
			Assert.IsTrue(changed);
			Assert.IsTrue(KingdomLiquidConfigurationRules.CrossingReadsBack(next, false));
		}

		[Test]
		public void MutualDeclarationLawStillRejectsOneSidedAndCrossLiquidJoins()
		{
			Assert.IsTrue(KingdomNetworkRules.DeclaredToward(
				KingdomNetworkRules.JoinEast, KingdomNetworkRules.JoinWest,
				KingdomNetworkRules.JoinEast));
			Assert.IsFalse(KingdomNetworkRules.DeclaredToward(
				KingdomNetworkRules.JoinEast, KingdomNetworkRules.JoinNorth,
				KingdomNetworkRules.JoinEast));
			Assert.AreEqual(KingdomJoinVerdict.RefusedLiquid,
				KingdomNetworkRules.JudgeJoin(true, KingdomNetworkKind.Liquid, "water",
					KingdomNetworkKind.Liquid, "salt"));
			Assert.AreEqual(KingdomJoinVerdict.Crossed,
				KingdomNetworkRules.JudgeJoin(false, KingdomNetworkKind.Liquid, "water",
					KingdomNetworkKind.Liquid, "salt"));
		}
	}
}
#endif
