#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;
using Frontier = ThousandAndFirst.KingdomRules.Frontier;
using Mark = ThousandAndFirst.KingdomLayoutRules.LayoutMark;
using Outcome = ThousandAndFirst.KingdomLayoutRules.LayoutOutcome;
using Point = ThousandAndFirst.KingdomLayoutRules.LayoutPoint;
using Purpose = ThousandAndFirst.KingdomLayoutRules.LayoutPurpose;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The settlement's layout grammar. Scores are asserted by exact value rather than by
	/// ordering wherever a single term is under test, so deleting a penalty or flipping a
	/// weight fails here instead of quietly changing what a city looks like.
	/// </summary>
	public class KingdomLayoutRulesTests
	{
		// A Qud surface zone: wide and short, which is why the frontier band matters more
		// north-south than east-west.
		private const int W = 80;

		private const int H = 25;

		private static Mark M(int X, int Y, Purpose Purpose)
		{
			return new Mark(X, Y, Purpose);
		}

		private static Point P(int X, int Y)
		{
			return new Point(X, Y);
		}

		private static List<Mark> Marks(params Mark[] Items)
		{
			return new List<Mark>(Items);
		}

		private static List<Point> Points(params Point[] Items)
		{
			return new List<Point>(Items);
		}

		private static int Score(Purpose Purpose, int X, int Y, Frontier Edges, List<Mark> Marks)
		{
			return KingdomLayoutRules.ScoreCell(Purpose, X, Y, W, H, Edges, Marks);
		}

		private static Point Chosen(Purpose Purpose, Frontier Edges, List<Mark> Marks, List<Point> Candidates, out Outcome Outcome)
		{
			Outcome = KingdomLayoutRules.Choose(Purpose, W, H, Edges, Marks, Candidates, HasFounder: false, 0, 0, out var index);
			Assert.GreaterOrEqual(index, 0, "expected a sited cell");
			return Candidates[index];
		}

		private static void AssertAt(int X, int Y, Point Actual)
		{
			Assert.AreEqual(X, Actual.X, "x");
			Assert.AreEqual(Y, Actual.Y, "y");
		}

		// --- PurposeOf: a building's Category is the whole of what the plan knows about it ---

		[TestCase("storage", Purpose.Storage)]
		[TestCase("Storage", Purpose.Storage)]
		[TestCase("  STORAGE ", Purpose.Storage)]
		[TestCase("housing", Purpose.Housing)]
		[TestCase("civic", Purpose.Civic)]
		[TestCase("craft", Purpose.Civic)]
		[TestCase("faith", Purpose.Civic)]
		[TestCase("knowledge", Purpose.Civic)]
		[TestCase("food", Purpose.Field)]
		[TestCase("memorial", Purpose.Memorial)]
		[TestCase("power", Purpose.Sited)]
		[TestCase("defense", Purpose.Defence)]
		[TestCase("defence", Purpose.Defence)]
		[TestCase("menagerie", Purpose.Unknown)]
		[TestCase("", Purpose.Unknown)]
		[TestCase(null, Purpose.Unknown)]
		public void PurposeOf_ReadsTheCategory(string category, Purpose expected)
		{
			Assert.AreEqual(expected, KingdomLayoutRules.PurposeOf(category));
		}

		[Test]
		public void PurposeOf_RecognisesEveryCategoryTheModDocuments()
		{
			// MODDING.md names these to third-party authors. One of them falling through to
			// Unknown means a shipped building silently loses its place in the plan.
			string[] documented = new string[] { "storage", "housing", "civic", "faith", "craft", "power", "defense", "knowledge", "food", "memorial" };
			for (int i = 0; i < documented.Length; i++)
			{
				Assert.AreNotEqual(Purpose.Unknown, KingdomLayoutRules.PurposeOf(documented[i]), documented[i]);
			}
		}

		// --- Empty ground: the plan has no opinion, and says so ------------------------------

		[TestCase(Purpose.Storage)]
		[TestCase(Purpose.Housing)]
		[TestCase(Purpose.Civic)]
		[TestCase(Purpose.Field)]
		[TestCase(Purpose.Memorial)]
		[TestCase(Purpose.Defence)]
		[TestCase(Purpose.Sited)]
		[TestCase(Purpose.Unknown)]
		public void EmptyGround_DefersToTheFounder(Purpose purpose)
		{
			List<Mark> marks = Marks();
			Assert.IsFalse(KingdomLayoutRules.HasOpinion(purpose, marks, Frontier.North | Frontier.West));
			Outcome outcome = KingdomLayoutRules.Choose(purpose, W, H, Frontier.North | Frontier.West, marks,
				Points(P(10, 10), P(20, 10)), HasFounder: true, 10, 10, out var index);
			Assert.AreEqual(Outcome.Defer, outcome);
			Assert.AreEqual(-1, index);
		}

		[TestCase(Purpose.Sited)]
		[TestCase(Purpose.Unknown)]
		public void GroundDecidedWorks_DeferEvenInABuiltCity(Purpose purpose)
		{
			// A sailvane wants wind and a wheel wants moving water; the plan can see neither,
			// so it never overrules a founder who walked to the spot. Same for a category
			// another mod invented: the plan will not file someone else's building by guess.
			List<Mark> marks = Marks(M(10, 10, Purpose.Storage), M(12, 10, Purpose.Housing), M(14, 10, Purpose.Civic), M(16, 10, Purpose.Sited));
			Outcome outcome = KingdomLayoutRules.Choose(purpose, W, H, Frontier.North, marks,
				Points(P(30, 10), P(11, 11)), HasFounder: true, 30, 10, out var index);
			Assert.AreEqual(Outcome.Defer, outcome);
			Assert.AreEqual(-1, index);
		}

		[Test]
		public void NoClearGround_SitesNothing()
		{
			List<Mark> marks = Marks(M(10, 10, Purpose.Storage));
			Assert.IsTrue(KingdomLayoutRules.HasOpinion(Purpose.Storage, marks, Frontier.None));
			Outcome outcome = KingdomLayoutRules.Choose(Purpose.Storage, W, H, Frontier.None, marks,
				Points(), HasFounder: true, 10, 11, out var index);
			Assert.AreEqual(Outcome.None, outcome);
			Assert.AreEqual(-1, index);
		}

		// --- Storage: the casks go where the water already is --------------------------------

		[Test]
		public void Storage_GathersByTheWater_WithALaneAroundIt()
		{
			List<Mark> marks = Marks(M(10, 10, Purpose.Storage));
			Assert.AreEqual(-14, Score(Purpose.Storage, 11, 10, Frontier.None, marks), "hard against the cask");
			Assert.AreEqual(-8, Score(Purpose.Storage, 12, 10, Frontier.None, marks), "one lane away");
			Assert.AreEqual(-40, Score(Purpose.Storage, 20, 10, Frontier.None, marks), "across the camp");
			Point chosen = Chosen(Purpose.Storage, Frontier.None, marks,
				Points(P(11, 10), P(12, 10), P(20, 10), P(40, 20)), out var outcome);
			Assert.AreEqual(Outcome.Grammar, outcome);
			AssertAt(12, 10, chosen);
		}

		[Test]
		public void Storage_FollowsTheVessels_NotTheTownCentre()
		{
			// The whole point of the storage rule: a settlement puts its casks together, so a
			// new one goes to the water even when the water is nowhere near the gathering places.
			List<Mark> marks = Marks(M(70, 20, Purpose.Storage), M(10, 5, Purpose.Civic), M(11, 5, Purpose.Civic), M(12, 5, Purpose.Housing));
			Assert.IsTrue(KingdomLayoutRules.TryHeart(marks, out var heartX, out var heartY));
			Assert.AreEqual(26, heartX);
			Assert.AreEqual(9, heartY);
			Point chosen = Chosen(Purpose.Storage, Frontier.None, marks,
				Points(P(68, 20), P(26, 9)), out var outcome);
			Assert.AreEqual(Outcome.Grammar, outcome);
			AssertAt(68, 20, chosen);
		}

		[Test]
		public void Storage_WithNoVesselsYet_FallsBackToTheHeart()
		{
			List<Mark> marks = Marks(M(40, 12, Purpose.Civic));
			Assert.AreEqual(-4, Score(Purpose.Storage, 42, 12, Frontier.None, marks), "two cells off the heart, no lane cost");
			Assert.AreEqual(-20, Score(Purpose.Storage, 50, 12, Frontier.None, marks));
		}

		// --- Housing: people do not sleep on the wall ----------------------------------------

		[Test]
		public void Housing_StandsBackFromTheFrontier_EvenToReachItsOwnKind()
		{
			List<Mark> marks = Marks(M(10, 0, Purpose.Housing));
			Assert.AreEqual(-68, Score(Purpose.Housing, 12, 0, Frontier.North, marks), "close kin, but on the wall line");
			Assert.AreEqual(-16, Score(Purpose.Housing, 14, 4, Frontier.North, marks), "further from kin, off the line");
			Point chosen = Chosen(Purpose.Housing, Frontier.North, marks, Points(P(12, 0), P(14, 4)), out var outcome);
			Assert.AreEqual(Outcome.Grammar, outcome);
			AssertAt(14, 4, chosen);
			Assert.GreaterOrEqual(chosen.Y, KingdomRules.FrontierBandCells, "housing sited inside the wall line");
		}

		[Test]
		public void Housing_ClustersWithHousing_NotWithTheStores()
		{
			List<Mark> marks = Marks(M(10, 10, Purpose.Storage), M(50, 10, Purpose.Housing));
			Point chosen = Chosen(Purpose.Housing, Frontier.None, marks, Points(P(12, 10), P(52, 10)), out var outcome);
			Assert.AreEqual(Outcome.Grammar, outcome);
			AssertAt(52, 10, chosen);
		}

		[Test]
		public void InteriorEdge_IsNotFrontier_SoAnEnclosedZoneHousesAnywhere()
		{
			// Claim the neighbour and the edge stops being frontier: the same cell that was
			// forbidden yesterday is ordinary ground today, with nothing moved or torn down.
			List<Mark> marks = Marks(M(10, 0, Purpose.Housing));
			Assert.AreEqual(-68, Score(Purpose.Housing, 12, 0, Frontier.North, marks));
			Assert.AreEqual(-8, Score(Purpose.Housing, 12, 0, Frontier.None, marks));
		}

		// --- Defence: a wall extends the wall ------------------------------------------------

		[Test]
		public void Defence_ClosesAGapBeforeItExtendsALine_AndThickensLast()
		{
			List<Mark> line = Marks(M(10, 0, Purpose.Defence), M(11, 0, Purpose.Defence), M(12, 0, Purpose.Defence));
			int corner = Score(Purpose.Defence, 10, 1, Frontier.North, line);
			int thicken = Score(Purpose.Defence, 11, 1, Frontier.North, line);
			int extend = Score(Purpose.Defence, 13, 0, Frontier.North, line);
			int stray = Score(Purpose.Defence, 40, 0, Frontier.North, line);
			Assert.AreEqual(40, corner, "two segments in reach");
			Assert.AreEqual(34, thicken, "three segments in reach: the line getting fatter, not longer");
			Assert.AreEqual(20, extend, "the end of the line");
			Assert.AreEqual(0, stray, "a fresh stub somewhere else along the same edge");
			Assert.Greater(corner, thicken);
			Assert.Greater(thicken, extend);
			Assert.Greater(extend, stray);
		}

		[Test]
		public void Defence_FillsTheHoleInTheLine()
		{
			List<Mark> line = Marks(M(6, 0, Purpose.Defence), M(8, 0, Purpose.Defence), M(10, 0, Purpose.Defence));
			Point chosen = Chosen(Purpose.Defence, Frontier.North, line,
				Points(P(11, 0), P(9, 0), P(5, 0)), out var outcome);
			Assert.AreEqual(Outcome.Grammar, outcome);
			AssertAt(9, 0, chosen);
		}

		[Test]
		public void Defence_IgnoresTheFrontierPenaltyThatBindsEverythingElse()
		{
			// The band is where a wall belongs; scoring it as a bad place to build would make
			// the plan refuse to wall anything.
			List<Mark> line = Marks(M(10, 0, Purpose.Defence));
			Assert.AreEqual(20, Score(Purpose.Defence, 11, 0, Frontier.North, line));
		}

		[TestCase(Frontier.None, 1, false, TestName = "Defence_NoFrontier_NothingToWall")]
		[TestCase(Frontier.North, 0, false, TestName = "Defence_NoLineYet_LeavesTheFirstSegmentToTheOldPlacement")]
		[TestCase(Frontier.North, 1, true, TestName = "Defence_FrontierAndALine_HasAnOpinion")]
		public void Defence_HasOpinionOnlyWithBothAFrontierAndALine(Frontier edges, int walls, bool expected)
		{
			List<Mark> marks = Marks(M(40, 12, Purpose.Civic));
			for (int i = 0; i < walls; i++)
			{
				marks.Add(M(10 + i, 0, Purpose.Defence));
			}
			Assert.AreEqual(expected, KingdomLayoutRules.HasOpinion(Purpose.Defence, marks, edges));
		}

		// --- Civic: the settled heart --------------------------------------------------------

		[Test]
		public void Civic_ThickensTheHeart_AndKeepsItsLane()
		{
			List<Mark> marks = Marks(M(10, 10, Purpose.Storage), M(20, 10, Purpose.Housing), M(15, 5, Purpose.Civic));
			Assert.IsTrue(KingdomLayoutRules.TryHeart(marks, out var heartX, out var heartY));
			Assert.AreEqual(15, heartX);
			Assert.AreEqual(8, heartY);
			Assert.AreEqual(-2, Score(Purpose.Civic, 16, 7, Frontier.None, marks), "one cell off the heart");
			Assert.AreEqual(-4, Score(Purpose.Civic, 15, 10, Frontier.None, marks), "two cells off the heart");
			Assert.AreEqual(-14, Score(Purpose.Civic, 15, 6, Frontier.None, marks), "hard against the shrine");
			Assert.AreEqual(-50, Score(Purpose.Civic, 40, 10, Frontier.None, marks), "out in the corner");
			Point chosen = Chosen(Purpose.Civic, Frontier.None, marks,
				Points(P(15, 10), P(16, 7), P(15, 6), P(40, 10)), out var outcome);
			Assert.AreEqual(Outcome.Grammar, outcome);
			AssertAt(16, 7, chosen);
		}

		[Test]
		public void Heart_IgnoresWalls_BecauseAWallIsAtTheEdgeByDefinition()
		{
			List<Mark> marks = Marks(M(0, 0, Purpose.Defence), M(79, 24, Purpose.Defence), M(10, 10, Purpose.Civic));
			Assert.IsTrue(KingdomLayoutRules.TryHeart(marks, out var x, out var y));
			AssertAt(10, 10, P(x, y));
		}

		[TestCase(2, false, TestName = "Heart_TwoWallsAloneAreAPostInAField")]
		[TestCase(3, true, TestName = "Heart_ThreeWallsDescribeAnEnclosure")]
		public void Heart_FallsBackToTheLineWhenNothingElseStands(int walls, bool expected)
		{
			List<Mark> marks = Marks();
			for (int i = 0; i < walls; i++)
			{
				marks.Add(M(10 + i * 2, 4, Purpose.Defence));
			}
			Assert.AreEqual(expected, KingdomLayoutRules.TryHeart(marks, out var x, out var y));
			if (expected)
			{
				AssertAt(12, 4, P(x, y));
			}
		}

		[Test]
		public void Heart_RoundsToTheNearestCell()
		{
			List<Mark> marks = Marks(M(0, 0, Purpose.Civic), M(0, 3, Purpose.Civic));
			Assert.IsTrue(KingdomLayoutRules.TryHeart(marks, out var x, out var y));
			AssertAt(0, 2, P(x, y));
		}

		[Test]
		public void Heart_OfNothingIsNowhere()
		{
			Assert.IsFalse(KingdomLayoutRules.TryHeart(Marks(), out var x, out var y));
			Assert.AreEqual(0, x);
			Assert.AreEqual(0, y);
			Assert.IsFalse(KingdomLayoutRules.TryHeart(null, out _, out _));
		}

		// --- Fields and graves: rings out past the built-up ground ---------------------------

		[Test]
		public void Field_LiesOutPastTheLastRoof_AndRowsAbut()
		{
			List<Mark> marks = Marks(M(40, 12, Purpose.Civic));
			Assert.AreEqual(0, Score(Purpose.Field, 46, 12, Frontier.None, marks), "on the ring");
			Assert.AreEqual(-15, Score(Purpose.Field, 41, 12, Frontier.None, marks), "in among the buildings, and no lane cost");
			Assert.AreEqual(-18, Score(Purpose.Field, 52, 12, Frontier.None, marks), "off in the waste");
			Point chosen = Chosen(Purpose.Field, Frontier.None, marks,
				Points(P(41, 12), P(46, 12), P(52, 12)), out var outcome);
			Assert.AreEqual(Outcome.Grammar, outcome);
			AssertAt(46, 12, chosen);
		}

		[Test]
		public void Field_ExtendsTheFieldOnceThereIsOne()
		{
			List<Mark> marks = Marks(M(40, 12, Purpose.Civic), M(46, 12, Purpose.Field));
			Assert.AreEqual(-10, Score(Purpose.Field, 47, 12, Frontier.None, marks), "the next furrow over");
			Assert.AreEqual(-12, Score(Purpose.Field, 49, 12, Frontier.None, marks), "dead on the ring but away from the plot");
			Point chosen = Chosen(Purpose.Field, Frontier.None, marks, Points(P(47, 12), P(49, 12)), out var outcome);
			Assert.AreEqual(Outcome.Grammar, outcome);
			AssertAt(47, 12, chosen);
		}

		[Test]
		public void Memorial_LiesFurtherOutThanTheFields()
		{
			List<Mark> marks = Marks(M(40, 12, Purpose.Civic));
			Assert.AreEqual(0, Score(Purpose.Memorial, 49, 12, Frontier.None, marks));
			Assert.AreEqual(-9, Score(Purpose.Memorial, 46, 12, Frontier.None, marks));
			Assert.Greater(KingdomLayoutRules.MemorialRingCells, KingdomLayoutRules.FieldRingCells);
		}

		[TestCase(Purpose.Storage, true)]
		[TestCase(Purpose.Housing, true)]
		[TestCase(Purpose.Civic, true)]
		[TestCase(Purpose.Field, false)]
		[TestCase(Purpose.Memorial, false)]
		[TestCase(Purpose.Defence, false)]
		public void KeepsLanes_OnlyForWhatIsWalkedIntoAndUsed(Purpose purpose, bool expected)
		{
			Assert.AreEqual(expected, KingdomLayoutRules.KeepsLanes(purpose));
		}

		// --- The founder: intent beats grammar, up to a point --------------------------------

		[Test]
		public void Founder_WinsGroundThePlanOnlyMildlyPrefersOtherwise()
		{
			List<Mark> marks = Marks(M(10, 10, Purpose.Storage));
			List<Point> candidates = Points(P(12, 10), P(14, 11));
			Assert.AreEqual(-8, Score(Purpose.Storage, 12, 10, Frontier.None, marks), "the plan's own pick");
			Assert.AreEqual(-16, Score(Purpose.Storage, 14, 11, Frontier.None, marks), "beside the founder, within tolerance");
			Outcome outcome = KingdomLayoutRules.Choose(Purpose.Storage, W, H, Frontier.None, marks, candidates,
				HasFounder: true, 14, 10, out var index);
			Assert.AreEqual(Outcome.Founder, outcome);
			AssertAt(14, 11, candidates[index]);
		}

		[Test]
		public void Founder_IsOverruledWhereThePlanFeelsStrongly()
		{
			// Standing on the wall line asking for a bunk. The plan does not build it there.
			List<Mark> marks = Marks(M(40, 10, Purpose.Housing));
			List<Point> candidates = Points(P(11, 0), P(38, 10), P(41, 11));
			Assert.AreEqual(-176, Score(Purpose.Housing, 11, 0, Frontier.North, marks));
			Assert.AreEqual(-8, Score(Purpose.Housing, 38, 10, Frontier.North, marks));
			Outcome outcome = KingdomLayoutRules.Choose(Purpose.Housing, W, H, Frontier.North, marks, candidates,
				HasFounder: true, 10, 0, out var index);
			Assert.AreEqual(Outcome.Grammar, outcome);
			AssertAt(38, 10, candidates[index]);
		}

		[Test]
		public void Founder_OutOfReachIsNotTheFoundersGround()
		{
			// Two cells away is not "where you stand": the founder's claim on a spot is that
			// they are standing next to it.
			List<Mark> marks = Marks(M(10, 10, Purpose.Storage));
			List<Point> candidates = Points(P(12, 10), P(16, 10));
			Outcome outcome = KingdomLayoutRules.Choose(Purpose.Storage, W, H, Frontier.None, marks, candidates,
				HasFounder: true, 18, 10, out var index);
			Assert.AreEqual(Outcome.Grammar, outcome);
			AssertAt(12, 10, candidates[index]);
		}

		// --- Ranking is deterministic --------------------------------------------------------

		[TestCase(0, 5, 1, 1, -1, 0, 2, 2, true, TestName = "Beats_ThePlansOpinionComesFirst")]
		[TestCase(0, 5, 1, 1, 0, 3, 2, 2, false, TestName = "Beats_ThenTheFoundersFeet")]
		[TestCase(0, 3, 5, 1, 0, 3, 2, 2, true, TestName = "Beats_ThenTheLowerRow")]
		[TestCase(0, 3, 5, 2, 0, 3, 2, 2, false, TestName = "Beats_ThenTheLowerColumn")]
		[TestCase(0, 3, 1, 2, 0, 3, 2, 2, true, TestName = "Beats_LowerColumnOnTheSameRow")]
		[TestCase(0, 3, 2, 2, 0, 3, 2, 2, false, TestName = "Beats_NothingBeatsItself")]
		public void Beats_RanksTwoCandidates(int scoreA, int reachA, int ax, int ay, int scoreB, int reachB, int bx, int by, bool expected)
		{
			Assert.AreEqual(expected, KingdomLayoutRules.Beats(scoreA, reachA, P(ax, ay), scoreB, reachB, P(bx, by)));
		}

		[Test]
		public void Choose_DoesNotDependOnTheOrderTheZoneWasWalked()
		{
			List<Mark> marks = Marks(M(10, 10, Purpose.Storage), M(30, 6, Purpose.Housing), M(20, 8, Purpose.Civic));
			List<Point> forward = Points(P(12, 10), P(13, 11), P(21, 9), P(40, 20), P(12, 11));
			List<Point> backward = Points(P(12, 11), P(40, 20), P(21, 9), P(13, 11), P(12, 10));
			KingdomLayoutRules.Choose(Purpose.Storage, W, H, Frontier.South, marks, forward, HasFounder: false, 0, 0, out var first);
			KingdomLayoutRules.Choose(Purpose.Storage, W, H, Frontier.South, marks, backward, HasFounder: false, 0, 0, out var second);
			AssertAt(forward[first].X, forward[first].Y, backward[second]);
		}

		// --- The contract, over many shapes of settlement -------------------------------------

		[Test]
		public void Choose_NeverSitesWorseThanTheFounderToleranceAllows()
		{
			// The one promise the whole grammar rests on: the plan either picks the best ground
			// it can see, or hands the founder ground it is nearly as happy with. Nothing else
			// is ever returned.
			Purpose[] purposes = new Purpose[] { Purpose.Storage, Purpose.Housing, Purpose.Civic, Purpose.Field, Purpose.Memorial, Purpose.Defence, Purpose.Sited, Purpose.Unknown };
			uint seed = 20260820u;
			for (int scenario = 0; scenario < 400; scenario++)
			{
				Purpose purpose = purposes[Next(ref seed, purposes.Length)];
				Frontier edges = (Frontier)Next(ref seed, 16);
				List<Mark> marks = Marks();
				int markCount = Next(ref seed, 12);
				for (int i = 0; i < markCount; i++)
				{
					marks.Add(M(Next(ref seed, W), Next(ref seed, H), purposes[Next(ref seed, purposes.Length)]));
				}
				List<Point> candidates = Points();
				int candidateCount = Next(ref seed, 20);
				for (int i = 0; i < candidateCount; i++)
				{
					candidates.Add(P(Next(ref seed, W), Next(ref seed, H)));
				}
				int founderX = Next(ref seed, W);
				int founderY = Next(ref seed, H);
				bool hasFounder = Next(ref seed, 2) == 1;
				Outcome outcome = KingdomLayoutRules.Choose(purpose, W, H, edges, marks, candidates, hasFounder, founderX, founderY, out var index);
				if (outcome == Outcome.Defer || outcome == Outcome.None)
				{
					Assert.AreEqual(-1, index, "scenario " + scenario);
					continue;
				}
				Assert.GreaterOrEqual(index, 0, "scenario " + scenario);
				Assert.Less(index, candidates.Count, "scenario " + scenario);
				int best = int.MinValue;
				for (int i = 0; i < candidates.Count; i++)
				{
					int score = Score(purpose, candidates[i].X, candidates[i].Y, edges, marks);
					if (score > best)
					{
						best = score;
					}
				}
				int chosen = Score(purpose, candidates[index].X, candidates[index].Y, edges, marks);
				if (outcome == Outcome.Grammar)
				{
					Assert.AreEqual(best, chosen, "scenario " + scenario + ": the plan's own pick is the best ground it saw");
				}
				else
				{
					Assert.IsTrue(hasFounder, "scenario " + scenario + ": no founder, no founder's ground");
					Assert.LessOrEqual(KingdomLayoutRules.Chebyshev(candidates[index].X, candidates[index].Y, founderX, founderY),
						KingdomLayoutRules.FounderReachCells, "scenario " + scenario);
					Assert.GreaterOrEqual(chosen, best - KingdomLayoutRules.FounderTolerance, "scenario " + scenario);
				}
			}
		}

		private static int Next(ref uint Seed, int Bound)
		{
			Seed = Seed * 1664525u + 1013904223u;
			return (int)((Seed >> 8) % (uint)Bound);
		}

		// --- What the founder is told --------------------------------------------------------

		[TestCase(Purpose.Defence, Outcome.Grammar, "on the line")]
		[TestCase(Purpose.Storage, Outcome.Grammar, "beside the stores")]
		[TestCase(Purpose.Housing, Outcome.Grammar, "among the homes")]
		[TestCase(Purpose.Civic, Outcome.Grammar, "on the settled ground")]
		[TestCase(Purpose.Field, Outcome.Grammar, "out past the last roof")]
		[TestCase(Purpose.Memorial, Outcome.Grammar, "on the quiet ground")]
		[TestCase(Purpose.Sited, Outcome.Grammar, null)]
		[TestCase(Purpose.Unknown, Outcome.Grammar, null)]
		[TestCase(Purpose.Storage, Outcome.Founder, "where you stand")]
		[TestCase(Purpose.Sited, Outcome.Founder, "where you stand")]
		[TestCase(Purpose.Storage, Outcome.Defer, null)]
		[TestCase(Purpose.Storage, Outcome.None, null)]
		public void PlacementClause_NamesTheGroundInTheSettlementsTerms(Purpose purpose, Outcome outcome, string expected)
		{
			Assert.AreEqual(expected, KingdomLayoutRules.PlacementClause(purpose, outcome));
		}
	}
}
#endif
