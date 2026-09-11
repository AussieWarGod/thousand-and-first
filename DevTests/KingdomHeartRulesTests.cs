#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;
using Frontier = ThousandAndFirst.KingdomRules.Frontier;
using Mark = ThousandAndFirst.KingdomLayoutRules.LayoutMark;
using Purpose = ThousandAndFirst.KingdomLayoutRules.LayoutPurpose;
using Rect = ThousandAndFirst.KingdomPlotRules.PlotRect;
using Size = ThousandAndFirst.KingdomPlotRules.PlotSize;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The heart: one plot surveyed whole at the founding rite and staked a rung at a time, each
	/// rung built OVER the last. Every claim the design makes is asserted here by exact value or
	/// by containment, so deleting the survey, flattening the tier weight, inverting the
	/// repulsion, or sliding a rung off the rite ground fails here rather than in a city.
	/// </summary>
	public class KingdomHeartRulesTests
	{
		// A Qud surface zone.
		private const int W = 80;

		private const int H = 25;

		private static Rect R(int X1, int Y1, int X2, int Y2)
		{
			return new Rect(X1, Y1, X2, Y2);
		}

		private static List<Mark> Marks(params Mark[] Items)
		{
			return new List<Mark>(Items);
		}

		private static Rect Survey(int X, int Y)
		{
			ClassicAssert.IsTrue(KingdomPlotRules.TrySurveyedHeart(X, Y, W, H, out var survey), "expected a survey around " + X + "," + Y);
			return survey;
		}

		private static Rect Rung(Rect Survey, int X, int Y, int Rung)
		{
			ClassicAssert.IsTrue(KingdomPlotRules.TryHeartRect(Survey, X, Y, KingdomPlotRules.HeartSizeForRung(Rung), out var rect), "expected ground for rung " + Rung);
			return rect;
		}

		private static bool Contains(Rect Outer, Rect Inner)
		{
			return KingdomPlotRules.Within(Outer, Inner);
		}

		// --- The ladder --------------------------------------------------------------------

		[TestCase("heartbasin", 1)]
		[TestCase("heartwaterstone", 2)]
		[TestCase("heartmoot", 3)]
		[TestCase("heartcourt", 4)]
		[TestCase("arcology", 5)]
		[TestCase("hall", 0)]
		[TestCase("", 0)]
		[TestCase(null, 0)]
		public void OnlyTheFiveRungsAreTheHeart(string Key, int Expected)
		{
			ClassicAssert.AreEqual(Expected, KingdomPlotRules.HeartRungOf(Key));
		}

		[Test]
		public void TheRungKeysAndTheRungNumbersAgreeBothWays()
		{
			for (int rung = 1; rung <= KingdomPlotRules.HeartRungKeys.Length; rung++)
			{
				string key = KingdomPlotRules.HeartKeyForRung(rung);
				ClassicAssert.IsNotNull(key, "rung " + rung + " names a design");
				ClassicAssert.AreEqual(rung, KingdomPlotRules.HeartRungOf(key));
			}
			ClassicAssert.IsNull(KingdomPlotRules.HeartKeyForRung(0));
			ClassicAssert.IsNull(KingdomPlotRules.HeartKeyForRung(KingdomPlotRules.HeartRungKeys.Length + 1));
		}

		[TestCase(1, Size.Small)]
		[TestCase(2, Size.Medium)]
		[TestCase(3, Size.Large)]
		[TestCase(4, Size.Huge)]
		[TestCase(5, Size.Huge)]
		[TestCase(6, Size.None)]
		[TestCase(0, Size.None)]
		public void TheHeartClimbsTheSameSizeLadderTheStagesGate(int Rung, Size Expected)
		{
			ClassicAssert.AreEqual(Expected, KingdomPlotRules.HeartSizeForRung(Rung));
		}

		// --- Issue #144: the shared XL tier at the top of the ladder ---------------------
		//
		// KingdomPlotRules.HeartRungEndpointsAdmit is the pure endpoint half of the authored
		// heart transition guard: adjacent rungs, each end on the tier HeartSizeForRung gives
		// its own rung. Rungs one to four each happen to stand on the tier numbered like
		// themselves, which is why a rung-number comparison passed for so long; rungs four and
		// five both stand on Huge, so the last step is a same-footprint renovation and the
		// number comparison made it unsatisfiable. Tier arguments below are integer values of
		// KingdomPlotRules.PlotSize, which the caller supplies from its own lot size.

		private static int Tier(Size Size) { return (int)Size; }

		[TestCase(1, 2, Size.Small, Size.Medium, TestName = "rung 1->2 grows small to medium")]
		[TestCase(2, 3, Size.Medium, Size.Large, TestName = "rung 2->3 grows medium to large")]
		[TestCase(3, 4, Size.Large, Size.Huge, TestName = "rung 3->4 grows large to huge")]
		[TestCase(4, 5, Size.Huge, Size.Huge, TestName = "rung 4->5 renovates the same huge ground")]
		public void EveryAdjacentRungPairOnItsOwnTiersIsAdmitted(int Before, int After,
			Size BeforeTier, Size AfterTier)
		{
			ClassicAssert.IsTrue(KingdomPlotRules.HeartRungEndpointsAdmit(Before, After,
				Tier(BeforeTier), Tier(AfterTier)));
		}

		[TestCase(1, 2, Size.Medium, Size.Medium, TestName = "wrong before tier refuses")]
		[TestCase(2, 3, Size.Medium, Size.Huge, TestName = "wrong after tier refuses")]
		[TestCase(4, 5, Size.Large, Size.Huge, TestName = "the top step still checks its before tier")]
		[TestCase(4, 5, Size.Huge, Size.Large, TestName = "the top step still checks its after tier")]
		[TestCase(3, 4, Size.None, Size.Huge, TestName = "an untiered before end refuses")]
		public void AnEndpointOnTheWrongTierIsNeverAdmitted(int Before, int After,
			Size BeforeTier, Size AfterTier)
		{
			ClassicAssert.IsFalse(KingdomPlotRules.HeartRungEndpointsAdmit(Before, After,
				Tier(BeforeTier), Tier(AfterTier)));
		}

		[TestCase(1, 3, TestName = "skipping a rung refuses")]
		[TestCase(3, 5, TestName = "skipping the court refuses")]
		[TestCase(2, 5, TestName = "jumping to the arcology refuses")]
		[TestCase(5, 4, TestName = "climbing backward refuses")]
		[TestCase(2, 2, TestName = "standing still is not a step")]
		public void NonAdjacentOrBackwardRungPairsAreNeverAdmitted(int Before, int After)
		{
			// Each end is given its own CORRECT tier, so only adjacency can be doing the work.
			ClassicAssert.IsFalse(KingdomPlotRules.HeartRungEndpointsAdmit(Before, After,
				(int)KingdomPlotRules.HeartSizeForRung(Before),
				(int)KingdomPlotRules.HeartSizeForRung(After)));
		}

		[TestCase(0, 1, TestName = "there is no rung below the first")]
		[TestCase(-1, 0, TestName = "a negative rung is not a rung")]
		[TestCase(5, 6, TestName = "there is no rung above the arcology")]
		[TestCase(6, 7, TestName = "both ends off the ladder refuse")]
		public void ARungOffTheLadderIsNeverAnEndpoint(int Before, int After)
		{
			ClassicAssert.IsFalse(KingdomPlotRules.HeartRungEndpointsAdmit(Before, After,
				(int)KingdomPlotRules.HeartSizeForRung(Before),
				(int)KingdomPlotRules.HeartSizeForRung(After)));
		}

		[TestCase(1, 2, false)]
		[TestCase(2, 3, false)]
		[TestCase(3, 4, false)]
		[TestCase(4, 5, true)]
		public void OnlyTheLastRungRenovatesTheGroundBelowItRatherThanGrowingOntoMore(
			int Before, int After, bool SameFootprint)
		{
			bool shared = KingdomPlotRules.HeartSizeForRung(Before)
				== KingdomPlotRules.HeartSizeForRung(After);
			ClassicAssert.AreEqual(SameFootprint, shared,
				"rung " + Before + "->" + After + " footprint sharing");
		}

		[TestCase(4)]
		[TestCase(5)]
		public void TheTopTwoRungsShareTheOneHugeTierSoTheirRectsAreTheSameGround(int Rung)
		{
			ClassicAssert.AreEqual(Size.Huge, KingdomPlotRules.HeartSizeForRung(Rung));
			int width, height;
			ClassicAssert.IsTrue(KingdomPlotRules.TryDimensions(
				KingdomPlotRules.HeartSizeForRung(Rung), out width, out height));
			ClassicAssert.AreEqual(KingdomPlotRules.HugeWidth, width);
			ClassicAssert.AreEqual(KingdomPlotRules.HugeHeight, height);
		}

		[Test]
		public void OnlyTheTopRungsTierDiffersFromItsOwnNumber()
		{
			for (int rung = 1; rung <= 4; rung++)
				ClassicAssert.AreEqual(rung, (int)KingdomPlotRules.HeartSizeForRung(rung),
					"rungs one to four are unchanged by the canonical mapping");
			ClassicAssert.AreEqual(4, (int)KingdomPlotRules.HeartSizeForRung(5));
			ClassicAssert.AreNotEqual(5, (int)KingdomPlotRules.HeartSizeForRung(5),
				"rung five is the only rung whose tier is not its own number");
		}

		[Test]
		public void EveryRungIsGatedByTheStageThatLaysItsPlotAndNothingElse()
		{
			// The heart needs no gate of its own: a settlement that cannot lay a great plot cannot
			// close the great court, and is refused in the words it already knows.
			ClassicAssert.AreEqual(GrowthStage.Camp, KingdomPlotRules.StageForSize(KingdomPlotRules.HeartSizeForRung(1)));
			ClassicAssert.AreEqual(GrowthStage.Steading, KingdomPlotRules.StageForSize(KingdomPlotRules.HeartSizeForRung(2)));
			ClassicAssert.AreEqual(GrowthStage.Town, KingdomPlotRules.StageForSize(KingdomPlotRules.HeartSizeForRung(3)));
			ClassicAssert.AreEqual(GrowthStage.City, KingdomPlotRules.StageForSize(KingdomPlotRules.HeartSizeForRung(4)));
		}

		// --- The city pulls back onto the heart ---------------------------------------------

		[TestCase(0, 1)]
		[TestCase(1, 1)]
		[TestCase(2, 4)]
		[TestCase(3, 12)]
		[TestCase(4, 40)]
		[TestCase(5, 80)]
		public void TheRiteGroundsWeightRisesWithTheRungStandingOnIt(int Rung, int Expected)
		{
			ClassicAssert.AreEqual(Expected, KingdomPlotRules.HeartWeightForRung(Rung));
		}

		[Test]
		public void TheBasinLetsTheHeartWalkAfterTheCityAndTheCourtDrawsItBack()
		{
			// Twelve works clustered at one end, the rite poured at the other.
			List<Mark> marks = Marks();
			for (int i = 0; i < 12; i++)
			{
				marks.Add(new Mark(60 + (i % 4), 10 + (i / 4), Purpose.Housing));
			}
			ClassicAssert.IsTrue(KingdomPlotRules.TryHeart(marks, HasRite: true, 10, 12, out var atBasin, out _, KingdomPlotRules.HeartWeightForRung(1)));
			ClassicAssert.IsTrue(KingdomPlotRules.TryHeart(marks, HasRite: true, 10, 12, out var atCourt, out _, KingdomPlotRules.HeartWeightForRung(4)));
			// A tin bowl on bare ground is not a monument: the heart is out with the houses.
			ClassicAssert.AreEqual(58, atBasin);
			// The great court is: the settled centre has come more than half the way back.
			ClassicAssert.AreEqual(22, atCourt);
			ClassicAssert.Less(atCourt, atBasin, "the rising work draws the centre back toward itself");
			// And it climbs rung by rung rather than jumping at the end.
			ClassicAssert.IsTrue(KingdomPlotRules.TryHeart(marks, HasRite: true, 10, 12, out var atStone, out _, KingdomPlotRules.HeartWeightForRung(2)));
			ClassicAssert.IsTrue(KingdomPlotRules.TryHeart(marks, HasRite: true, 10, 12, out var atMoot, out _, KingdomPlotRules.HeartWeightForRung(3)));
			ClassicAssert.AreEqual(49, atStone);
			ClassicAssert.AreEqual(36, atMoot);
		}

		[Test]
		public void NoCallerCanVoteTheRiteGroundAway()
		{
			List<Mark> marks = Marks(new Mark(60, 12, Purpose.Housing));
			ClassicAssert.IsTrue(KingdomPlotRules.TryHeart(marks, HasRite: true, 10, 12, out var clamped, out _, 0));
			ClassicAssert.IsTrue(KingdomPlotRules.TryHeart(marks, HasRite: true, 10, 12, out var one, out _, 1));
			ClassicAssert.AreEqual(one, clamped, "a weight under one is read as one, never as no rite at all");
		}

		// --- The survey ---------------------------------------------------------------------

		[Test]
		public void TheSurveyIsTheFinalRungsGroundCentredOnTheRite()
		{
			Rect survey = Survey(40, 12);
			ClassicAssert.AreEqual(KingdomPlotRules.HugeWidth, survey.Width);
			ClassicAssert.AreEqual(KingdomPlotRules.HugeHeight, survey.Height);
			ClassicAssert.AreEqual(40, survey.CenterX);
			ClassicAssert.AreEqual(12, survey.CenterY);
		}

		[Test]
		public void ASurveyAgainstTheZoneEdgeSlidesWholeAndStillHoldsTheRite()
		{
			Rect survey = Survey(2, 2);
			ClassicAssert.IsTrue(KingdomPlotRules.TryInterior(W, H, out var interior));
			ClassicAssert.IsTrue(Contains(interior, survey), "the survey never overhangs the interior");
			ClassicAssert.IsTrue(survey.Contains(2, 2), "the rite ground is always inside its own survey");
			ClassicAssert.AreEqual(KingdomPlotRules.HugeWidth, survey.Width, "it slides rather than shrinking");
			ClassicAssert.AreEqual(KingdomPlotRules.HugeHeight, survey.Height);
		}

		[Test]
		public void AZoneWithNoRoomForTheFinalRungIsNeverSurveyed()
		{
			ClassicAssert.IsFalse(KingdomPlotRules.TrySurveyedHeart(5, 5, 12, 9, out _));
		}

		// --- Build over: every rung encloses the one below ----------------------------------

		[Test]
		public void EachRungsGroundContainsTheRungBelowIt()
		{
			Rect survey = Survey(40, 12);
			Rect previous = Rung(survey, 40, 12, 1);
			for (int rung = 2; rung <= 5; rung++)
			{
				Rect ground = Rung(survey, 40, 12, rung);
				ClassicAssert.IsTrue(Contains(ground, previous), "rung " + rung + " is built over rung " + (rung - 1));
				ClassicAssert.IsTrue(ground.Contains(40, 12), "the rite ground stays inside every rung");
				previous = ground;
			}
			ClassicAssert.AreEqual(survey, previous, "the last rung fills exactly the ground surveyed for it");
		}

		[Test]
		public void EachRungsGroundContainsTheRungBelowItAgainstTheZoneEdge()
		{
			// The clamped case, which is the one that could break the nesting.
			Rect survey = Survey(3, 3);
			Rect previous = Rung(survey, 3, 3, 1);
			for (int rung = 2; rung <= 5; rung++)
			{
				Rect ground = Rung(survey, 3, 3, rung);
				ClassicAssert.IsTrue(Contains(ground, previous), "rung " + rung + " is built over rung " + (rung - 1));
				ClassicAssert.IsTrue(Contains(survey, ground), "no rung ever leaves the surveyed ground");
				previous = ground;
			}
		}

		[Test]
		public void EveryRungIsStakedInsideTheGroundSurveyedForIt()
		{
			Rect survey = Survey(40, 12);
			for (int rung = 1; rung <= 5; rung++)
			{
				ClassicAssert.IsTrue(Contains(survey, Rung(survey, 40, 12, rung)), "rung " + rung + " needs no ground the rite did not survey");
			}
		}

		[Test]
		public void ACentredRectSlidesWholeAndRefusesWhatWillNotFit()
		{
			Rect bounds = R(10, 10, 19, 19);
			ClassicAssert.IsTrue(KingdomPlotRules.TryCentred(bounds, 14, 14, 4, 4, out var middle));
			ClassicAssert.AreEqual(R(13, 13, 16, 16), middle);
			ClassicAssert.IsTrue(KingdomPlotRules.TryCentred(bounds, 10, 10, 4, 4, out var corner));
			ClassicAssert.AreEqual(R(10, 10, 13, 13), corner);
			ClassicAssert.IsTrue(KingdomPlotRules.TryCentred(bounds, 19, 19, 4, 4, out var far));
			ClassicAssert.AreEqual(R(16, 16, 19, 19), far);
			ClassicAssert.IsFalse(KingdomPlotRules.TryCentred(bounds, 14, 14, 11, 4, out _), "wider than the bounds is refused, never trimmed");
			ClassicAssert.IsFalse(KingdomPlotRules.TryCentred(bounds, 14, 14, 0, 4, out _));
		}

		// --- The survey steers and never refuses --------------------------------------------

		[Test]
		public void OverlapIsCountedInCellsAndIsZeroWhenTheyDoNotMeet()
		{
			ClassicAssert.AreEqual(0, KingdomPlotRules.OverlapArea(R(0, 0, 4, 3), R(5, 0, 9, 3)));
			ClassicAssert.AreEqual(20, KingdomPlotRules.OverlapArea(R(0, 0, 4, 3), R(0, 0, 4, 3)));
			ClassicAssert.AreEqual(4, KingdomPlotRules.OverlapArea(R(0, 0, 4, 3), R(3, 2, 9, 9)));
		}

		[Test]
		public void APlotSquarelyInSurveyedGroundPaysTheWholeRepulsionAndOneClippingItPaysAlmostNone()
		{
			Rect survey = Survey(40, 12);
			ClassicAssert.IsTrue(KingdomPlotRules.TryRectAt(38, 11, Size.Small, out var inside));
			ClassicAssert.AreEqual(KingdomPlotRules.SurveyRepulsion, KingdomPlotRules.SurveyPenalty(inside, survey));
			ClassicAssert.IsTrue(KingdomPlotRules.TryRectAt(50, 11, Size.Small, out var clipping));
			ClassicAssert.AreEqual(2, KingdomPlotRules.SurveyPenalty(clipping, survey), "one column of five, so a fifth of the repulsion");
			ClassicAssert.IsTrue(KingdomPlotRules.TryRectAt(70, 11, Size.Small, out var clear));
			ClassicAssert.AreEqual(0, KingdomPlotRules.SurveyPenalty(clear, survey));
		}

		[Test]
		public void TheRepulsionIsAPreferenceTheFoundersOwnGroundStillBeats()
		{
			// The whole of the contract: the term must stay under the tolerance the layout grammar
			// already gives the founder, or a stake in surveyed ground would stop winning.
			ClassicAssert.Less(KingdomPlotRules.SurveyRepulsion, KingdomLayoutRules.FounderTolerance);
			ClassicAssert.Greater(KingdomPlotRules.SurveyRepulsion, 0);
		}

		[Test]
		public void SurveyedGroundCostsAScoreAndNeverARefusal()
		{
			List<Mark> marks = Marks(new Mark(40, 12, Purpose.Housing));
			Rect survey = Survey(40, 12);
			ClassicAssert.IsTrue(KingdomPlotRules.TryRectAt(38, 11, Size.Small, out var inside));
			int without = KingdomPlotRules.ScoreRect(Purpose.Housing, Size.Small, inside, W, H, Frontier.None, marks, true, 40, 12);
			int with = KingdomPlotRules.ScoreRect(Purpose.Housing, Size.Small, inside, W, H, Frontier.None, marks, true, 40, 12, true, survey);
			ClassicAssert.AreEqual(without - KingdomPlotRules.SurveyRepulsion, with, "exactly one repulsion, and the rect is still a candidate");
		}

		[Test]
		public void TheSurveyIsScoredOnEveryTierAlike()
		{
			// A hut in the heart's ground is as much in the way as a hall is, so the term does not
			// read the tier at all.
			List<Mark> marks = Marks(new Mark(40, 12, Purpose.Civic));
			Rect survey = Survey(40, 12);
			ClassicAssert.IsTrue(KingdomPlotRules.TryRectAt(36, 9, Size.Medium, out var medium));
			ClassicAssert.AreEqual(KingdomPlotRules.SurveyRepulsion, KingdomPlotRules.SurveyPenalty(medium, survey));
			int plain = KingdomPlotRules.ScoreRect(Purpose.Civic, Size.Medium, medium, W, H, Frontier.None, marks, true, 40, 12);
			int repelled = KingdomPlotRules.ScoreRect(Purpose.Civic, Size.Medium, medium, W, H, Frontier.None, marks, true, 40, 12, true, survey);
			ClassicAssert.AreEqual(plain - KingdomPlotRules.SurveyRepulsion, repelled);
		}

		[Test]
		public void TheChosenRectIsSteeredOutOfSurveyedGroundAndNotForbiddenIt()
		{
			// Two rects the plan likes exactly as well as each other -- same distance from the
			// housing already standing -- one of them in the ground the heart was surveyed for.
			List<Mark> marks = Marks(new Mark(40, 12, Purpose.Housing));
			Rect survey = Survey(30, 12);
			ClassicAssert.IsTrue(KingdomPlotRules.TryRectAt(32, 11, Size.Small, out var inside));
			ClassicAssert.IsTrue(KingdomPlotRules.TryRectAt(44, 11, Size.Small, out var beside));
			ClassicAssert.AreEqual(KingdomPlotRules.SurveyRepulsion, KingdomPlotRules.SurveyPenalty(inside, survey));
			ClassicAssert.AreEqual(0, KingdomPlotRules.SurveyPenalty(beside, survey));
			List<Rect> candidates = new List<Rect> { inside, beside };
			// Without the survey the tie breaks by position and the near ground wins.
			KingdomPlotRules.ChooseRect(Purpose.Housing, Size.Small, W, H, Frontier.None, marks, candidates,
				false, 0, 0, true, 30, 12, out var plain);
			ClassicAssert.AreEqual(0, plain);
			// With it, both rects are still offered and the settlement volunteers for the other.
			KingdomPlotRules.ChooseRect(Purpose.Housing, Size.Small, W, H, Frontier.None, marks, candidates,
				false, 0, 0, true, 30, 12, out var steered, true, survey);
			ClassicAssert.AreEqual(1, steered);
			ClassicAssert.AreEqual(2, candidates.Count, "nothing was struck off the list; the ground is still legal");
		}

		[Test]
		public void TheSurveyNeverOutweighsWhatThePlanActuallyWants()
		{
			// The other half of "preference, never refusal": ground the grammar genuinely prefers
			// is still chosen even when it stands squarely in the heart's survey. A settlement
			// with nowhere better does build there, and is told what it has done.
			List<Mark> marks = Marks(new Mark(40, 12, Purpose.Housing));
			Rect survey = Survey(40, 12);
			ClassicAssert.IsTrue(KingdomPlotRules.TryRectAt(38, 11, Size.Small, out var inside));
			ClassicAssert.IsTrue(KingdomPlotRules.TryRectAt(52, 11, Size.Small, out var far));
			List<Rect> candidates = new List<Rect> { inside, far };
			KingdomPlotRules.ChooseRect(Purpose.Housing, Size.Small, W, H, Frontier.None, marks, candidates,
				false, 0, 0, true, 40, 12, out var chosen, true, survey);
			ClassicAssert.AreEqual(0, chosen, "the repulsion steers; it does not forbid");
		}

		// --- The yielding mark ---------------------------------------------------------------

		[Test]
		public void TheYieldingMarkPromisesAMoveAndNoCost()
		{
			string line = KingdomPlotRules.YieldingLine("settler's tent");
			StringAssert.Contains("settler's tent", line);
			StringAssert.Contains("marked to yield", line);
			StringAssert.Contains("Nothing is taken from it", line);
			StringAssert.Contains("marked to yield", KingdomPlotRules.YieldingMark.ToLowerInvariant());
		}

		[Test]
		public void EveryHeartRefusalNamesWhatWouldLiftIt()
		{
			StringAssert.Contains("chalk hut", KingdomPlotRules.RefuseHeartGround("great court", "chalk hut"));
			StringAssert.Contains("clear it", KingdomPlotRules.RefuseHeartGround("great court", "chalk hut").ToLowerInvariant());
			StringAssert.Contains("no room", KingdomPlotRules.RefuseHeartRoom("great court").ToLowerInvariant());
			StringAssert.Contains("Chalkhaven", KingdomPlotRules.RefuseSecondHeart("Chalkhaven"));
			StringAssert.Contains("one heart", KingdomPlotRules.RefuseSecondHeart("Chalkhaven").ToLowerInvariant());
		}

		[Test]
		public void AYieldingPlotInTheWayIsToldThePromiseIsBeingKept()
		{
			// The mark said this day would come, so the refusal says so, and says honestly what
			// the settlement can do about it today rather than implying a verb it does not have.
			string line = KingdomPlotRules.RefuseHeartYielding("great court", "chalk hut");
			StringAssert.Contains("chalk hut", line);
			StringAssert.Contains("marked to yield", line);
			StringAssert.Contains("carry the same whole lot to lawful ground", line);
			StringAssert.Contains("nothing moves until the founder reviews and consents", line);
			ClassicAssert.AreNotEqual(KingdomPlotRules.RefuseHeartGround("great court", "chalk hut"), line,
				"a plot that was warned is not told the same thing as one that never was");
		}

		[Test]
		public void TheSurveyIsAnnouncedAtTheRiteWithItsOwnMeasure()
		{
			Rect survey = Survey(40, 12);
			string line = KingdomPlotRules.SurveyLine(survey);
			StringAssert.Contains("20 by 18", line);
			StringAssert.Contains("Nothing is claimed and nothing is spent", line);
			StringAssert.Contains("marked to yield", line);
		}

		// --- The ceremony ----------------------------------------------------------------------

		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		public void EveryRungSaysWhatTheGroundHasBecome(int Rung)
		{
			string chronicle = KingdomCeremonyHeartRules.ChronicleLine(Rung, "Chalkhaven");
			StringAssert.Contains("Chalkhaven", chronicle);
			// There is one grammar for a building rising and this is not it; the ceremony writes
			// that line, and Art/check_xml_refs.py holds the raising paths to it.
			StringAssert.DoesNotContain("was raised at", chronicle);
			StringAssert.Contains("Chalkhaven", KingdomCeremonyHeartRules.MessageLine(Rung, "Chalkhaven"));
		}

		[Test]
		public void TheHigherRungsNameWhatIsStillUnderfoot()
		{
			StringAssert.Contains("basin", KingdomCeremonyHeartRules.ChronicleLine(2, "Chalkhaven"));
			StringAssert.Contains("kerb", KingdomCeremonyHeartRules.ChronicleLine(3, "Chalkhaven"));
			StringAssert.Contains("moot hall", KingdomCeremonyHeartRules.ChronicleLine(4, "Chalkhaven"));
			StringAssert.Contains("kerb", KingdomCeremonyHeartRules.ChronicleLine(4, "Chalkhaven"));
			StringAssert.Contains("basin", KingdomCeremonyHeartRules.ChronicleLine(4, "Chalkhaven"));
		}

		[Test]
		public void AnUnnamedRealmStillGetsAWholeSentence()
		{
			StringAssert.Contains("the settlement", KingdomCeremonyHeartRules.ChronicleLine(2, null));
			StringAssert.Contains("the settlement", KingdomCeremonyHeartRules.MessageLine(2, ""));
			StringAssert.Contains("grew by one course", KingdomCeremonyHeartRules.ChronicleLine(9, "Chalkhaven"));
		}

		[TestCase(1, false)]
		[TestCase(2, false)]
		[TestCase(3, true)]
		[TestCase(4, true)]
		[TestCase(5, true)]
		public void OnlyTheRungsAStrangerWouldCallAPlaceAreAccomplishments(int Rung, bool Expected)
		{
			ClassicAssert.AreEqual(Expected, KingdomCeremonyHeartRules.IsAccomplishment(Rung));
		}

		// Issue #138: the rung a design key names and the drams that rung is worth are the two
		// numbers the shared settlement helper turns a finished job into. Pinned per rung so a
		// ladder edit cannot silently move the waterstone's 48 drams.
		[TestCase("heartbasin", 1, 16)]
		[TestCase("heartwaterstone", 2, 48)]
		[TestCase("heartmoot", 3, 160)]
		[TestCase("heartcourt", 4, 512)]
		[TestCase("arcology", 5, 1024)]
		public void EveryRungKeyMapsToItsRungAndItsBasinCapacity(string key, int rung, int drams)
		{
			ClassicAssert.AreEqual(rung, KingdomPlotRules.HeartRungOf(key));
			ClassicAssert.AreEqual(drams, KingdomPlotRules.HeartBasinCapacityForRung(rung));
			ClassicAssert.AreEqual(key, KingdomPlotRules.HeartKeyForRung(rung));
		}

		// Issue #138: the direction rule the shared settlement helper judges a finished receipt
		// by. Deliberately NOT a plus-one rule: that the ladder climbs one rung at a time is
		// gated upstream in KingdomArchitectureRuntime.TryPrepareSuccessor, where BOTH ends of
		// the transition are known; by settling time the predecessor is gone, so re-deriving it
		// here would guess at a number this helper cannot see. Direction is all it judges.
		[TestCase(2, 1, true, TestName = "one rung forward settles")]
		[TestCase(2, 2, true, TestName = "the same rung settles again, idempotently")]
		[TestCase(3, 1, true, TestName = "two rungs forward is left to the upstream ladder gate")]
		[TestCase(1, 2, false, TestName = "a rung below the standing one never settles")]
		[TestCase(1, 0, true, TestName = "the first rung settles on ground standing at none")]
		[TestCase(0, 0, false, TestName = "a design off the ladder settles no rung")]
		public void ARungSettlesOnlyForwardAndNeverBelowTheStandingRung(int rung, int standing,
			bool expected)
		{
			ClassicAssert.AreEqual(expected, KingdomPlotRules.RungMaySettle(rung, standing));
		}

		[Test]
		public void ADesignOffTheLadderHasNoRungAndNoBasinCapacity()
		{
			ClassicAssert.AreEqual(0, KingdomPlotRules.HeartRungOf("hut"));
			ClassicAssert.AreEqual(0, KingdomPlotRules.HeartRungOf(null));
			ClassicAssert.AreEqual(0, KingdomPlotRules.HeartBasinCapacityForRung(0));
			ClassicAssert.AreEqual(0, KingdomPlotRules.HeartBasinCapacityForRung(6));
			ClassicAssert.IsNull(KingdomPlotRules.HeartKeyForRung(0));
			ClassicAssert.IsNull(KingdomPlotRules.HeartKeyForRung(6));
		}
	}
}
#endif
