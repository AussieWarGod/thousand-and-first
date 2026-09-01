#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomVisualStateRulesTests
	{
		private static KingdomVisualFacts F(bool active = false, bool selected = false,
			int hands = 0, bool salvage = false, bool repairing = false, int wear = 0,
			bool heart = false, bool withered = false, bool famished = false,
			bool brownout = false, int needed = 0, int effectiveness = 100)
		{
			return new KingdomVisualFacts(active, selected, hands, salvage, repairing, wear,
				heart, withered, famished, brownout, needed, effectiveness);
		}

		[TestCase(0, KingdomVisualStateKind.Sound)]
		[TestCase(1, KingdomVisualStateKind.Battered)]
		[TestCase(KingdomMaterialRules.BadlyUsedWearPercent,
			KingdomVisualStateKind.HalfRuined)]
		[TestCase(KingdomMaterialRules.HalfWreckedWearPercent,
			KingdomVisualStateKind.Ruined)]
		public void DamageLadderUsesTheSimulationThresholds(int wear,
			KingdomVisualStateKind expected)
		{
			Assert.AreEqual(expected, KingdomVisualStateRules.Resolve(F(wear: wear)));
		}

		[Test]
		public void ConstructionShowsRealAssignmentAndQueueStates()
		{
			Assert.AreEqual(KingdomVisualStateKind.RaisingQueued,
				KingdomVisualStateRules.Resolve(F(active: true)));
			Assert.AreEqual(KingdomVisualStateKind.RaisingWaitingForHands,
				KingdomVisualStateRules.Resolve(F(active: true, selected: true)));
			Assert.AreEqual(KingdomVisualStateKind.Raising,
				KingdomVisualStateRules.Resolve(F(active: true, selected: true, hands: 1)));
		}

		[Test]
		public void DestructiveAndRepairStatesTakePriorityOverPassiveDamage()
		{
			Assert.AreEqual(KingdomVisualStateKind.SalvageOrdered,
				KingdomVisualStateRules.Resolve(F(salvage: true, repairing: true, wear: 60,
					brownout: true, needed: 2, effectiveness: 0)));
			Assert.AreEqual(KingdomVisualStateKind.Repairing,
				KingdomVisualStateRules.Resolve(F(repairing: true, wear: 60, brownout: true)));
		}

		[Test]
		public void WaterDeprivationAppearsOnlyOnTheHeartAndLegacyFoodMarksAreIgnored()
		{
			Assert.AreEqual(KingdomVisualStateKind.Sound,
				KingdomVisualStateRules.Resolve(F(withered: true, famished: true)));
			Assert.AreEqual(KingdomVisualStateKind.Withered,
				KingdomVisualStateRules.Resolve(F(heart: true, withered: true)));
			Assert.AreEqual(KingdomVisualStateKind.Sound,
				KingdomVisualStateRules.Resolve(F(heart: true, famished: true)));
			Assert.AreEqual(KingdomVisualStateKind.Withered,
				KingdomVisualStateRules.Resolve(F(heart: true, withered: true, famished: true)));
		}

		[Test]
		public void PowerAndStaffingUseExactRunState()
		{
			Assert.AreEqual(KingdomVisualStateKind.Dark,
				KingdomVisualStateRules.Resolve(F(brownout: true, needed: 2, effectiveness: 0)));
			Assert.AreEqual(KingdomVisualStateKind.Idle,
				KingdomVisualStateRules.Resolve(F(needed: 2, effectiveness: 0)));
			Assert.AreEqual(KingdomVisualStateKind.Shorthanded,
				KingdomVisualStateRules.Resolve(F(needed: 2, effectiveness: 50)));
			Assert.AreEqual(KingdomVisualStateKind.Sound,
				KingdomVisualStateRules.Resolve(F(needed: 2, effectiveness: 100)));
		}

		[Test]
		public void EveryNonSoundStateHasAUniqueColorIndependentCue()
		{
			HashSet<string> glyphs = new HashSet<string>();
			HashSet<string> tileMode = new HashSet<string>();
			for (int i = 0; i < KingdomVisualStateRules.GalleryStates.Length; i++)
			{
				KingdomVisualStateKind state = KingdomVisualStateRules.GalleryStates[i];
				if (state == KingdomVisualStateKind.Sound) continue;
				KingdomVisualCue cue = KingdomVisualStateRules.Cue(state);
				Assert.IsNotEmpty(cue.Glyph, state.ToString());
				Assert.IsNotEmpty(cue.Label, state.ToString());
				Assert.IsTrue(glyphs.Add(cue.Glyph), "duplicate text glyph " + cue.Glyph);
				string silhouette = cue.Tile ?? ("text:" + cue.Glyph);
				Assert.IsTrue(tileMode.Add(silhouette), "duplicate tile silhouette " + silhouette);
			}
		}

		[Test]
		public void GalleryReceiptIsVersionedAndHasAStableSha256()
		{
			StringAssert.StartsWith(KingdomVisualStateRules.GalleryVersion + "\n",
				KingdomVisualStateRules.GalleryReceipt());
			Assert.AreEqual("b8ad67bef7430f2f4007781991db0c2f23aade99a5b0962e15a72be9c41e3504",
				KingdomVisualStateRules.GalleryHash(),
				"change the gallery version and acceptance receipt with any cue change");
		}
	}
}
#endif
