#if TAF_TESTS
using System;
using System.Collections.Generic;

using NUnit.Framework;
using NUnit.Framework.Legacy;
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
				ClassicAssert.IsTrue(KingdomLiquidVisualRules.TryCue(mask, false, out waterCue));
				ClassicAssert.IsTrue(KingdomLiquidVisualRules.TryCue(mask, true, out brineCue));
				ClassicAssert.AreEqual(fresh[mask], waterCue.Glyph, "fresh mask " + mask);
				ClassicAssert.AreEqual(brine[mask], brineCue.Glyph, "brine mask " + mask);
				ClassicAssert.AreNotEqual(waterCue.Glyph, brineCue.Glyph,
					"liquid distinction must survive absent color at mask " + mask);
				ClassicAssert.AreEqual(KingdomLiquidVisualRules.FormOf(mask), waterCue.Form);
				string joins;
				int roundTrip;
				ClassicAssert.IsTrue(KingdomLiquidVisualRules.TryCanonicalJoins(mask, out joins));
				ClassicAssert.IsTrue(KingdomNetworkRules.TryParseJoins(joins, out roundTrip));
				ClassicAssert.AreEqual(mask, roundTrip);
			}
		}

		[Test]
		public void MalformedMasksAndDeclarationsFailClosedWithoutInventingJoins()
		{
			KingdomLiquidVisualCue cue;
			ClassicAssert.IsFalse(KingdomLiquidVisualRules.TryCue(-1, false, out cue));
			ClassicAssert.AreEqual(0, cue.Mask);
			ClassicAssert.AreEqual(250, cue.Glyph);
			ClassicAssert.IsFalse(cue.Valid);
			ClassicAssert.IsFalse(KingdomLiquidVisualRules.TryCue(16, true, out cue));
			ClassicAssert.AreEqual(254, cue.Glyph);
			ClassicAssert.IsFalse(KingdomLiquidVisualRules.TryCue("NX", false, out cue));
			ClassicAssert.AreEqual(KingdomLiquidForm.Cap, cue.Form);
			ClassicAssert.IsFalse(KingdomLiquidVisualRules.TryCue((string)null, true, out cue));
			ClassicAssert.AreEqual(0, cue.Mask);
			string joins;
			ClassicAssert.IsFalse(KingdomLiquidVisualRules.TryCanonicalJoins(16, out joins));
			ClassicAssert.IsNull(joins);
		}

		[Test]
		public void LegalChoiceSetCoversEveryMaskExactlyOnce()
		{
			HashSet<int> masks = new HashSet<int>();
			for (int choice = 0; choice < 16; choice++)
			{
				int mask;
				ClassicAssert.IsTrue(KingdomLiquidConfigurationRules.TryMaskForChoice(choice, out mask));
				ClassicAssert.IsTrue(masks.Add(mask), "duplicate mask " + mask);
			}
			ClassicAssert.AreEqual(16, masks.Count);
			ClassicAssert.IsFalse(KingdomLiquidConfigurationRules.TryMaskForChoice(-1, out _));
			ClassicAssert.IsFalse(KingdomLiquidConfigurationRules.TryMaskForChoice(16, out _));
			ClassicAssert.AreEqual(16, KingdomLiquidConfigurationRules.Options(false).Length);
			ClassicAssert.AreEqual(16, KingdomLiquidConfigurationRules.Options(true).Length);
		}

		[Test]
		public void DeclarationPlanningEnforcesAuthorizationReadbackAndIdempotence()
		{
			string next;
			int mask;
			bool changed;
			string failure;
			ClassicAssert.IsFalse(KingdomLiquidConfigurationRules.TryPlanDeclaration("EW", 7, false,
				out next, out mask, out changed, out failure));
			ClassicAssert.AreEqual("EW", next);
			ClassicAssert.IsFalse(changed);
			StringAssert.Contains("Only the player", failure);

			// Choice 6 is east-west. Equivalent old ordering stays byte-for-byte unchanged and
			// therefore cannot dirty topology.
			ClassicAssert.IsTrue(KingdomLiquidConfigurationRules.TryPlanDeclaration("WE", 6, true,
				out next, out mask, out changed, out failure));
			ClassicAssert.AreEqual("WE", next);
			ClassicAssert.AreEqual(12, mask);
			ClassicAssert.IsFalse(changed);

			// Choice 7 is north-east.
			ClassicAssert.IsTrue(KingdomLiquidConfigurationRules.TryPlanDeclaration("EW", 7, true,
				out next, out mask, out changed, out failure));
			ClassicAssert.AreEqual("NE", next);
			ClassicAssert.AreEqual(5, mask);
			ClassicAssert.IsTrue(changed);
			ClassicAssert.IsTrue(KingdomLiquidConfigurationRules.DeclarationReadsBack(next, mask));
			ClassicAssert.IsFalse(KingdomLiquidConfigurationRules.DeclarationReadsBack("NX", mask));

			ClassicAssert.IsTrue(KingdomLiquidConfigurationRules.TryPlanDeclaration("NX", 0, true,
				out next, out mask, out changed, out failure));
			ClassicAssert.AreEqual("", next);
			ClassicAssert.IsTrue(changed, "repairing unreadable durable text is an actual change");
		}

		[Test]
		public void OldNsewRowsRemainCrossesAndReadPathsDoNotNormalizeThem()
		{
			string old = "NSEW";
			KingdomLiquidVisualCue water;
			KingdomLiquidVisualCue brine;
			ClassicAssert.IsTrue(KingdomLiquidVisualRules.TryCue(old, false, out water));
			ClassicAssert.IsTrue(KingdomLiquidVisualRules.TryCue(old, true, out brine));
			ClassicAssert.AreEqual(KingdomLiquidForm.Cross, water.Form);
			ClassicAssert.AreEqual(197, water.Glyph);
			ClassicAssert.AreEqual(206, brine.Glyph);
			KingdomLiquidConfigurationRules.Status("water", old, false);
			ClassicAssert.AreEqual("NSEW", old);
		}

		[Test]
		public void CrossingOrientationIsVisibleWhileFunctionalPairsStayIsolated()
		{
			int glyph;
			bool vertical;
			ClassicAssert.IsTrue(KingdomLiquidVisualRules.TryCrossingCue("NSEW", out glyph, out vertical));
			ClassicAssert.AreEqual(216, glyph);
			ClassicAssert.IsTrue(vertical);
			ClassicAssert.IsTrue(KingdomLiquidVisualRules.TryCrossingCue("EWNS", out glyph, out vertical));
			ClassicAssert.AreEqual(215, glyph);
			ClassicAssert.IsFalse(vertical);
			int oldMask;
			int rotatedMask;
			ClassicAssert.IsTrue(KingdomNetworkRules.TryParseJoins("NSEW", out oldMask));
			ClassicAssert.IsTrue(KingdomNetworkRules.TryParseJoins("EWNS", out rotatedMask));
			ClassicAssert.AreEqual(KingdomNetworkRules.JoinAll, oldMask);
			ClassicAssert.AreEqual(oldMask, rotatedMask);
			ClassicAssert.AreEqual(KingdomNetworkRules.JoinSouth,
				KingdomNetworkRules.CrossoverExit(rotatedMask, KingdomNetworkRules.JoinNorth));
			ClassicAssert.AreEqual(KingdomNetworkRules.JoinWest,
				KingdomNetworkRules.CrossoverExit(rotatedMask, KingdomNetworkRules.JoinEast));
			ClassicAssert.AreNotEqual(KingdomNetworkRules.JoinEast,
				KingdomNetworkRules.CrossoverExit(rotatedMask, KingdomNetworkRules.JoinNorth));
			ClassicAssert.IsFalse(KingdomLiquidVisualRules.TryCrossingCue("NSX", out glyph, out vertical));
			ClassicAssert.AreEqual(254, glyph);
		}

		[Test]
		public void CrossingPlanningEnforcesAuthorizationAndActualChangeOnly()
		{
			string next;
			bool changed;
			string failure;
			ClassicAssert.IsFalse(KingdomLiquidConfigurationRules.TryPlanCrossing("NSEW", 1, false,
				out next, out changed, out failure));
			ClassicAssert.AreEqual("NSEW", next);
			ClassicAssert.IsFalse(changed);
			ClassicAssert.IsTrue(KingdomLiquidConfigurationRules.TryPlanCrossing("NSEW", 0, true,
				out next, out changed, out failure));
			ClassicAssert.IsFalse(changed);
			ClassicAssert.IsTrue(KingdomLiquidConfigurationRules.TryPlanCrossing("NSEW", 1, true,
				out next, out changed, out failure));
			ClassicAssert.AreEqual("EWNS", next);
			ClassicAssert.IsTrue(changed);
			ClassicAssert.IsTrue(KingdomLiquidConfigurationRules.CrossingReadsBack(next, false));
		}

		[Test]
		public void MutualDeclarationLawStillRejectsOneSidedAndCrossLiquidJoins()
		{
			ClassicAssert.IsTrue(KingdomNetworkRules.DeclaredToward(
				KingdomNetworkRules.JoinEast, KingdomNetworkRules.JoinWest,
				KingdomNetworkRules.JoinEast));
			ClassicAssert.IsFalse(KingdomNetworkRules.DeclaredToward(
				KingdomNetworkRules.JoinEast, KingdomNetworkRules.JoinNorth,
				KingdomNetworkRules.JoinEast));
			ClassicAssert.AreEqual(KingdomJoinVerdict.RefusedLiquid,
				KingdomNetworkRules.JudgeJoin(true, KingdomNetworkKind.Liquid, "water",
					KingdomNetworkKind.Liquid, "salt"));
			ClassicAssert.AreEqual(KingdomJoinVerdict.Crossed,
				KingdomNetworkRules.JudgeJoin(false, KingdomNetworkKind.Liquid, "water",
					KingdomNetworkKind.Liquid, "salt"));
		}
	}
}
#endif
