#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
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
			Assert.IsTrue(KingdomPlotRules.TrySurveyedHeart(X, Y, W, H, out var survey), "expected a survey around " + X + "," + Y);
			return survey;
		}

		private static Rect Rung(Rect Survey, int X, int Y, int Rung)
		{
			Assert.IsTrue(KingdomPlotRules.TryHeartRect(Survey, X, Y, KingdomPlotRules.HeartSizeForRung(Rung), out var rect), "expected ground for rung " + Rung);
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
			Assert.AreEqual(Expected, KingdomPlotRules.HeartRungOf(Key));
		}

		[Test]
		public void TheRungKeysAndTheRungNumbersAgreeBothWays()
		{
			for (int rung = 1; rung <= KingdomPlotRules.HeartRungKeys.Length; rung++)
			{
				string key = KingdomPlotRules.HeartKeyForRung(rung);
				Assert.IsNotNull(key, "rung " + rung + " names a design");
				Assert.AreEqual(rung, KingdomPlotRules.HeartRungOf(key));
			}
			Assert.IsNull(KingdomPlotRules.HeartKeyForRung(0));
			Assert.IsNull(KingdomPlotRules.HeartKeyForRung(KingdomPlotRules.HeartRungKeys.Length + 1));
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
			Assert.AreEqual(Expected, KingdomPlotRules.HeartSizeForRung(Rung));
		}

		[Test]
		public void EveryRungIsGatedByTheStageThatLaysItsPlotAndNothingElse()
		{
			// The heart needs no gate of its own: a settlement that cannot lay a great plot cannot
			// close the great court, and is refused in the words it already knows.
			Assert.AreEqual(GrowthStage.Camp, KingdomPlotRules.StageForSize(KingdomPlotRules.HeartSizeForRung(1)));
			Assert.AreEqual(GrowthStage.Steading, KingdomPlotRules.StageForSize(KingdomPlotRules.HeartSizeForRung(2)));
			Assert.AreEqual(GrowthStage.Town, KingdomPlotRules.StageForSize(KingdomPlotRules.HeartSizeForRung(3)));
			Assert.AreEqual(GrowthStage.City, KingdomPlotRules.StageForSize(KingdomPlotRules.HeartSizeForRung(4)));
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
			Assert.AreEqual(Expected, KingdomPlotRules.HeartWeightForRung(Rung));
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
			Assert.IsTrue(KingdomPlotRules.TryHeart(marks, HasRite: true, 10, 12, out var atBasin, out _, KingdomPlotRules.HeartWeightForRung(1)));
			Assert.IsTrue(KingdomPlotRules.TryHeart(marks, HasRite: true, 10, 12, out var atCourt, out _, KingdomPlotRules.HeartWeightForRung(4)));
			// A tin bowl on bare ground is not a monument: the heart is out with the houses.
			Assert.AreEqual(58, atBasin);
			// The great court is: the settled centre has come more than half the way back.
			Assert.AreEqual(22, atCourt);
			Assert.Less(atCourt, atBasin, "the rising work draws the centre back toward itself");
			// And it climbs rung by rung rather than jumping at the end.
			Assert.IsTrue(KingdomPlotRules.TryHeart(marks, HasRite: true, 10, 12, out var atStone, out _, KingdomPlotRules.HeartWeightForRung(2)));
			Assert.IsTrue(KingdomPlotRules.TryHeart(marks, HasRite: true, 10, 12, out var atMoot, out _, KingdomPlotRules.HeartWeightForRung(3)));
			Assert.AreEqual(49, atStone);
			Assert.AreEqual(36, atMoot);
		}

		[Test]
		public void NoCallerCanVoteTheRiteGroundAway()
		{
			List<Mark> marks = Marks(new Mark(60, 12, Purpose.Housing));
			Assert.IsTrue(KingdomPlotRules.TryHeart(marks, HasRite: true, 10, 12, out var clamped, out _, 0));
			Assert.IsTrue(KingdomPlotRules.TryHeart(marks, HasRite: true, 10, 12, out var one, out _, 1));
			Assert.AreEqual(one, clamped, "a weight under one is read as one, never as no rite at all");
		}

		// --- The survey ---------------------------------------------------------------------

		[Test]
		public void TheSurveyIsTheFinalRungsGroundCentredOnTheRite()
		{
			Rect survey = Survey(40, 12);
			Assert.AreEqual(KingdomPlotRules.HugeWidth, survey.Width);
			Assert.AreEqual(KingdomPlotRules.HugeHeight, survey.Height);
			Assert.AreEqual(40, survey.CenterX);
			Assert.AreEqual(12, survey.CenterY);
		}

		[Test]
		public void ASurveyAgainstTheZoneEdgeSlidesWholeAndStillHoldsTheRite()
		{
			Rect survey = Survey(2, 2);
			Assert.IsTrue(KingdomPlotRules.TryInterior(W, H, out var interior));
			Assert.IsTrue(Contains(interior, survey), "the survey never overhangs the interior");
			Assert.IsTrue(survey.Contains(2, 2), "the rite ground is always inside its own survey");
			Assert.AreEqual(KingdomPlotRules.HugeWidth, survey.Width, "it slides rather than shrinking");
			Assert.AreEqual(KingdomPlotRules.HugeHeight, survey.Height);
		}

		[Test]
		public void AZoneWithNoRoomForTheFinalRungIsNeverSurveyed()
		{
			Assert.IsFalse(KingdomPlotRules.TrySurveyedHeart(5, 5, 12, 9, out _));
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
				Assert.IsTrue(Contains(ground, previous), "rung " + rung + " is built over rung " + (rung - 1));
				Assert.IsTrue(ground.Contains(40, 12), "the rite ground stays inside every rung");
				previous = ground;
			}
			Assert.AreEqual(survey, previous, "the last rung fills exactly the ground surveyed for it");
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
				Assert.IsTrue(Contains(ground, previous), "rung " + rung + " is built over rung " + (rung - 1));
				Assert.IsTrue(Contains(survey, ground), "no rung ever leaves the surveyed ground");
				previous = ground;
			}
		}

		[Test]
		public void EveryRungIsStakedInsideTheGroundSurveyedForIt()
		{
			Rect survey = Survey(40, 12);
			for (int rung = 1; rung <= 5; rung++)
			{
				Assert.IsTrue(Contains(survey, Rung(survey, 40, 12, rung)), "rung " + rung + " needs no ground the rite did not survey");
			}
		}

		[Test]
		public void ACentredRectSlidesWholeAndRefusesWhatWillNotFit()
		{
			Rect bounds = R(10, 10, 19, 19);
			Assert.IsTrue(KingdomPlotRules.TryCentred(bounds, 14, 14, 4, 4, out var middle));
			Assert.AreEqual(R(13, 13, 16, 16), middle);
			Assert.IsTrue(KingdomPlotRules.TryCentred(bounds, 10, 10, 4, 4, out var corner));
			Assert.AreEqual(R(10, 10, 13, 13), corner);
			Assert.IsTrue(KingdomPlotRules.TryCentred(bounds, 19, 19, 4, 4, out var far));
			Assert.AreEqual(R(16, 16, 19, 19), far);
			Assert.IsFalse(KingdomPlotRules.TryCentred(bounds, 14, 14, 11, 4, out _), "wider than the bounds is refused, never trimmed");
			Assert.IsFalse(KingdomPlotRules.TryCentred(bounds, 14, 14, 0, 4, out _));
		}

		// --- The survey steers and never refuses --------------------------------------------

		[Test]
		public void OverlapIsCountedInCellsAndIsZeroWhenTheyDoNotMeet()
		{
			Assert.AreEqual(0, KingdomPlotRules.OverlapArea(R(0, 0, 4, 3), R(5, 0, 9, 3)));
			Assert.AreEqual(20, KingdomPlotRules.OverlapArea(R(0, 0, 4, 3), R(0, 0, 4, 3)));
			Assert.AreEqual(4, KingdomPlotRules.OverlapArea(R(0, 0, 4, 3), R(3, 2, 9, 9)));
		}

		[Test]
		public void APlotSquarelyInSurveyedGroundPaysTheWholeRepulsionAndOneClippingItPaysAlmostNone()
		{
			Rect survey = Survey(40, 12);
			Assert.IsTrue(KingdomPlotRules.TryRectAt(38, 11, Size.Small, out var inside));
			Assert.AreEqual(KingdomPlotRules.SurveyRepulsion, KingdomPlotRules.SurveyPenalty(inside, survey));
			Assert.IsTrue(KingdomPlotRules.TryRectAt(50, 11, Size.Small, out var clipping));
			Assert.AreEqual(2, KingdomPlotRules.SurveyPenalty(clipping, survey), "one column of five, so a fifth of the repulsion");
			Assert.IsTrue(KingdomPlotRules.TryRectAt(70, 11, Size.Small, out var clear));
			Assert.AreEqual(0, KingdomPlotRules.SurveyPenalty(clear, survey));
		}

		[Test]
		public void TheRepulsionIsAPreferenceTheFoundersOwnGroundStillBeats()
		{
			// The whole of the contract: the term must stay under the tolerance the layout grammar
			// already gives the founder, or a stake in surveyed ground would stop winning.
			Assert.Less(KingdomPlotRules.SurveyRepulsion, KingdomLayoutRules.FounderTolerance);
			Assert.Greater(KingdomPlotRules.SurveyRepulsion, 0);
		}

		[Test]
		public void SurveyedGroundCostsAScoreAndNeverARefusal()
		{
			List<Mark> marks = Marks(new Mark(40, 12, Purpose.Housing));
			Rect survey = Survey(40, 12);
			Assert.IsTrue(KingdomPlotRules.TryRectAt(38, 11, Size.Small, out var inside));
			int without = KingdomPlotRules.ScoreRect(Purpose.Housing, Size.Small, inside, W, H, Frontier.None, marks, true, 40, 12);
			int with = KingdomPlotRules.ScoreRect(Purpose.Housing, Size.Small, inside, W, H, Frontier.None, marks, true, 40, 12, true, survey);
			Assert.AreEqual(without - KingdomPlotRules.SurveyRepulsion, with, "exactly one repulsion, and the rect is still a candidate");
		}

		[Test]
		public void TheSurveyIsScoredOnEveryTierAlike()
		{
			// A hut in the heart's ground is as much in the way as a hall is, so the term does not
			// read the tier at all.
			List<Mark> marks = Marks(new Mark(40, 12, Purpose.Civic));
			Rect survey = Survey(40, 12);
			Assert.IsTrue(KingdomPlotRules.TryRectAt(36, 9, Size.Medium, out var medium));
			Assert.AreEqual(KingdomPlotRules.SurveyRepulsion, KingdomPlotRules.SurveyPenalty(medium, survey));
			int plain = KingdomPlotRules.ScoreRect(Purpose.Civic, Size.Medium, medium, W, H, Frontier.None, marks, true, 40, 12);
			int repelled = KingdomPlotRules.ScoreRect(Purpose.Civic, Size.Medium, medium, W, H, Frontier.None, marks, true, 40, 12, true, survey);
			Assert.AreEqual(plain - KingdomPlotRules.SurveyRepulsion, repelled);
		}

		[Test]
		public void TheChosenRectIsSteeredOutOfSurveyedGroundAndNotForbiddenIt()
		{
			// Two rects the plan likes exactly as well as each other -- same distance from the
			// housing already standing -- one of them in the ground the heart was surveyed for.
			List<Mark> marks = Marks(new Mark(40, 12, Purpose.Housing));
			Rect survey = Survey(30, 12);
			Assert.IsTrue(KingdomPlotRules.TryRectAt(32, 11, Size.Small, out var inside));
			Assert.IsTrue(KingdomPlotRules.TryRectAt(44, 11, Size.Small, out var beside));
			Assert.AreEqual(KingdomPlotRules.SurveyRepulsion, KingdomPlotRules.SurveyPenalty(inside, survey));
			Assert.AreEqual(0, KingdomPlotRules.SurveyPenalty(beside, survey));
			List<Rect> candidates = new List<Rect> { inside, beside };
			// Without the survey the tie breaks by position and the near ground wins.
			KingdomPlotRules.ChooseRect(Purpose.Housing, Size.Small, W, H, Frontier.None, marks, candidates,
				false, 0, 0, true, 30, 12, out var plain);
			Assert.AreEqual(0, plain);
			// With it, both rects are still offered and the settlement volunteers for the other.
			KingdomPlotRules.ChooseRect(Purpose.Housing, Size.Small, W, H, Frontier.None, marks, candidates,
				false, 0, 0, true, 30, 12, out var steered, true, survey);
			Assert.AreEqual(1, steered);
			Assert.AreEqual(2, candidates.Count, "nothing was struck off the list; the ground is still legal");
		}

		[Test]
		public void TheSurveyNeverOutweighsWhatThePlanActuallyWants()
		{
			// The other half of "preference, never refusal": ground the grammar genuinely prefers
			// is still chosen even when it stands squarely in the heart's survey. A settlement
			// with nowhere better does build there, and is told what it has done.
			List<Mark> marks = Marks(new Mark(40, 12, Purpose.Housing));
			Rect survey = Survey(40, 12);
			Assert.IsTrue(KingdomPlotRules.TryRectAt(38, 11, Size.Small, out var inside));
			Assert.IsTrue(KingdomPlotRules.TryRectAt(52, 11, Size.Small, out var far));
			List<Rect> candidates = new List<Rect> { inside, far };
			KingdomPlotRules.ChooseRect(Purpose.Housing, Size.Small, W, H, Frontier.None, marks, candidates,
				false, 0, 0, true, 40, 12, out var chosen, true, survey);
			Assert.AreEqual(0, chosen, "the repulsion steers; it does not forbid");
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
			Assert.AreNotEqual(KingdomPlotRules.RefuseHeartGround("great court", "chalk hut"), line,
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
			Assert.AreEqual(Expected, KingdomCeremonyHeartRules.IsAccomplishment(Rung));
		}
	}
}
#endif
