#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;
using Candidate = ThousandAndFirst.KingdomLodgingRules.LodgingCandidate;
using Reason = ThousandAndFirst.KingdomLodgingRules.UnhousedReason;
using Quarters = ThousandAndFirst.KingdomLodgingRules.Closeness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Cohabitation (Addendum 4): who is allowed to sleep under which roof, and who is never put
	/// beside whom. Every gate, every tiebreak, and every named line is asserted directly, so
	/// dropping a check, flipping a comparison, or losing a reason's own wording fails a test here
	/// rather than only showing up as a silent stall in play (STANDARDS 7b).
	/// </summary>
	public class KingdomLodgingRulesTests
	{
		private static List<string> Tags(params string[] Values)
		{
			return new List<string>(Values);
		}

		// --- ParseTags: comma list -> trimmed, non-empty tokens ------------------------------

		[Test]
		public void ParseTags_NullAndEmptyBothYieldAnEmptyNonNullList()
		{
			Assert.AreEqual(0, KingdomLodgingRules.ParseTags(null).Count);
			Assert.AreEqual(0, KingdomLodgingRules.ParseTags("").Count);
		}

		[Test]
		public void ParseTags_TrimsWhitespaceAndDropsEmptyEntries()
		{
			List<string> parsed = KingdomLodgingRules.ParseTags(" charge ,, water,damp ");
			CollectionAssert.AreEqual(new[] { "charge", "water", "damp" }, parsed);
		}

		// --- Intersects ------------------------------------------------------------------------

		[Test]
		public void Intersects_IsCaseInsensitiveAndOrderIndependent()
		{
			Assert.IsTrue(KingdomLodgingRules.Intersects(Tags("Fungal"), Tags("water", "fungal")));
		}

		[TestCase(0, 2)]
		[TestCase(2, 0)]
		public void Intersects_EmptyEitherSideIsFalse(int aCount, int bCount)
		{
			List<string> a = new List<string>();
			for (int i = 0; i < aCount; i++) a.Add("x" + i);
			List<string> b = new List<string>();
			for (int i = 0; i < bCount; i++) b.Add("y" + i);
			Assert.IsFalse(KingdomLodgingRules.Intersects(a, b));
		}

		[Test]
		public void Intersects_NoSharedTagIsFalse()
		{
			Assert.IsFalse(KingdomLodgingRules.Intersects(Tags("charge"), Tags("water", "sky")));
		}

		// --- MeetsNeeds: the hard Needs-vs-Provides gate ----------------------------------------

		[Test]
		public void MeetsNeeds_NoNeedsIsAlwaysMet()
		{
			Assert.IsTrue(KingdomLodgingRules.MeetsNeeds(new List<string>(), new List<string>()));
			Assert.IsTrue(KingdomLodgingRules.MeetsNeeds(new List<string>(), Tags("charge")));
			Assert.IsTrue(KingdomLodgingRules.MeetsNeeds(null, null));
		}

		[Test]
		public void MeetsNeeds_ANeedWithNoProvidesAtAllIsUnmet()
		{
			Assert.IsFalse(KingdomLodgingRules.MeetsNeeds(Tags("charge"), new List<string>()));
			Assert.IsFalse(KingdomLodgingRules.MeetsNeeds(Tags("charge"), null));
		}

		[Test]
		public void MeetsNeeds_EveryNeedMustAppearInProvides()
		{
			Assert.IsTrue(KingdomLodgingRules.MeetsNeeds(Tags("charge", "roof"), Tags("roof", "charge", "water")));
			Assert.IsFalse(KingdomLodgingRules.MeetsNeeds(Tags("charge", "roof"), Tags("roof", "water")), "the second need, charge, is not provided");
		}

		[Test]
		public void MeetsNeeds_IsCaseInsensitive()
		{
			Assert.IsTrue(KingdomLodgingRules.MeetsNeeds(Tags("Charge"), Tags("charge")));
		}

		// --- HasFreeBed --------------------------------------------------------------------------

		[TestCase(0, 0, false)]
		[TestCase(2, 2, false)]
		[TestCase(2, 1, true)]
		[TestCase(2, 3, false)]
		public void HasFreeBed_StrictlyLessThanCapacity(int capacity, int occupants, bool expected)
		{
			Assert.AreEqual(expected, KingdomLodgingRules.HasFreeBed(capacity, occupants));
		}

		// --- Conflicts: creed hostility and Refuses, both directions ---------------------------

		[Test]
		public void Conflicts_ZeroHostilityAndNoRefusesIsNoConflict()
		{
			Assert.IsFalse(KingdomLodgingRules.Conflicts(Tags(), Tags(), Tags(), Tags(), 0));
		}

		[Test]
		public void Conflicts_AnyHostilityAboveTheFloorConflicts()
		{
			Assert.IsTrue(KingdomLodgingRules.Conflicts(Tags(), Tags(), Tags(), Tags(), 1));
		}

		[Test]
		public void Conflicts_HostilityAtTheFloorDoesNotConflict()
		{
			Assert.IsFalse(KingdomLodgingRules.Conflicts(Tags(), Tags(), Tags(), Tags(), KingdomLodgingRules.CreedRefusalHostilityFloor));
		}

		[Test]
		public void Conflicts_ARefusesATagBCarriesInSelfTagsConflicts()
		{
			Assert.IsTrue(KingdomLodgingRules.Conflicts(Tags("fungal"), Tags(), Tags(), Tags("fungal"), 0));
		}

		[Test]
		public void Conflicts_BRefusingAAlsoConflictsEvenWhenAIsSilent()
		{
			Assert.IsTrue(KingdomLodgingRules.Conflicts(Tags(), Tags("loud"), Tags("loud"), Tags(), 0), "a refusal only one side states is still a refusal");
		}

		[Test]
		public void Conflicts_ARefusesATagNeitherSideCarriesDoesNotConflict()
		{
			Assert.IsFalse(KingdomLodgingRules.Conflicts(Tags("fungal"), Tags(), Tags(), Tags("dry"), 0));
		}

		// --- ChooseIndex: fewest free beds first, plot id as a stable tiebreak -----------------

		[Test]
		public void ChooseIndex_EmptyListReturnsMinusOne()
		{
			Assert.AreEqual(-1, KingdomLodgingRules.ChooseIndex(new List<Candidate>()));
			Assert.AreEqual(-1, KingdomLodgingRules.ChooseIndex(null));
		}

		[Test]
		public void ChooseIndex_SingleCandidateWins()
		{
			List<Candidate> candidates = new List<Candidate> { new Candidate("hut@1.1", 2, 0) };
			Assert.AreEqual(0, KingdomLodgingRules.ChooseIndex(candidates));
		}

		[Test]
		public void ChooseIndex_PicksTheFewestFreeBedsSoAHouseholdFillsBeforeAnEmptyOneOpens()
		{
			List<Candidate> candidates = new List<Candidate>
			{
				new Candidate("empty@0.0", 4, 0), // 4 free
				new Candidate("nearlyFull@1.0", 4, 3) // 1 free
			};
			Assert.AreEqual(1, KingdomLodgingRules.ChooseIndex(candidates));
		}

		[Test]
		public void ChooseIndex_TiesOnFreeBedsBreakByPlotIdOrdinalAscending()
		{
			List<Candidate> candidates = new List<Candidate>
			{
				new Candidate("zzz@9.9", 2, 0),
				new Candidate("aaa@0.0", 2, 0)
			};
			Assert.AreEqual(1, KingdomLodgingRules.ChooseIndex(candidates), "aaa sorts before zzz");
		}

		[Test]
		public void ChooseIndex_IsStableWhenCalledTwiceOnTheSameInput()
		{
			List<Candidate> candidates = new List<Candidate>
			{
				new Candidate("b@1.0", 3, 1),
				new Candidate("a@0.0", 3, 1)
			};
			int first = KingdomLodgingRules.ChooseIndex(candidates);
			int second = KingdomLodgingRules.ChooseIndex(candidates);
			Assert.AreEqual(first, second);
		}

		// --- Diagnose: the priority order a founder should hear reasons in ---------------------

		[Test]
		public void Diagnose_NoRoofAtAllOutranksEverything()
		{
			Assert.AreEqual(Reason.NoRoofAtAll, KingdomLodgingRules.Diagnose(false, false, false, false));
			Assert.AreEqual(Reason.NoRoofAtAll, KingdomLodgingRules.Diagnose(false, true, true, true));
		}

		[Test]
		public void Diagnose_NeedsUnmetOutranksFullAndRefused()
		{
			Assert.AreEqual(Reason.NeedsUnmet, KingdomLodgingRules.Diagnose(true, false, false, false));
			Assert.AreEqual(Reason.NeedsUnmet, KingdomLodgingRules.Diagnose(true, false, true, true));
		}

		[Test]
		public void Diagnose_FullOutranksRefused()
		{
			Assert.AreEqual(Reason.Full, KingdomLodgingRules.Diagnose(true, true, false, false));
			Assert.AreEqual(Reason.Full, KingdomLodgingRules.Diagnose(true, true, false, true));
		}

		[Test]
		public void Diagnose_RefusedIsLastWhenEverythingElseIsTrue()
		{
			Assert.AreEqual(Reason.Refused, KingdomLodgingRules.Diagnose(true, true, true, false));
		}

		[Test]
		public void Diagnose_AllTrueReadsHoused()
		{
			Assert.AreEqual(Reason.Housed, KingdomLodgingRules.Diagnose(true, true, true, true));
		}

		// --- UnhousedLine: named once, per 7b, never a pronoun guess ---------------------------

		[TestCase(Reason.NoRoofAtAll, "no roof standing yet")]
		[TestCase(Reason.NeedsUnmet, "nothing built here answers")]
		[TestCase(Reason.Full, "is full")]
		[TestCase(Reason.Refused, "will not live beside")]
		public void UnhousedLine_NamesTheResidentAndTheReason(Reason reason, string expectedSubstring)
		{
			string line = KingdomLodgingRules.UnhousedLine("Vashti", reason);
			StringAssert.StartsWith("Vashti sleeps in the open", line);
			StringAssert.Contains(expectedSubstring, line);
		}

		[Test]
		public void UnhousedLine_EmptyNameFallsBackToASettler()
		{
			StringAssert.StartsWith("a settler sleeps in the open", KingdomLodgingRules.UnhousedLine("", Reason.NoRoofAtAll));
			StringAssert.StartsWith("a settler sleeps in the open", KingdomLodgingRules.UnhousedLine(null, Reason.Full));
		}

		// --- MatchedTag --------------------------------------------------------------------------

		[Test]
		public void MatchedTag_ReturnsTheFirstNeedTheHomeAlsoProvides()
		{
			Assert.AreEqual("charge", KingdomLodgingRules.MatchedTag(Tags("charge", "roof"), Tags("roof", "charge")));
		}

		[Test]
		public void MatchedTag_NoOverlapReturnsNull()
		{
			Assert.IsNull(KingdomLodgingRules.MatchedTag(Tags("charge"), Tags("water")));
		}

		[Test]
		public void MatchedTag_NullEitherSideReturnsNull()
		{
			Assert.IsNull(KingdomLodgingRules.MatchedTag(null, Tags("water")));
			Assert.IsNull(KingdomLodgingRules.MatchedTag(Tags("water"), null));
		}

		// --- HomeSuffix: genotype/tag colour when the derivation gives it, plain otherwise ------

		[Test]
		public void HomeSuffix_KnownTagAddsItsOwnClause()
		{
			Assert.AreEqual("sleeps in the charging shed, by the charging post", KingdomLodgingRules.HomeSuffix("charging shed", "charge"));
		}

		[Test]
		public void HomeSuffix_UnknownOrAbsentTagIsPlain()
		{
			Assert.AreEqual("sleeps in the timber hut", KingdomLodgingRules.HomeSuffix("timber hut", null));
			Assert.AreEqual("sleeps in the timber hut", KingdomLodgingRules.HomeSuffix("timber hut", "some-unrecognised-tag"));
		}

		[Test]
		public void HomeSuffix_EmptyBuildingNameFallsBackToARoof()
		{
			Assert.AreEqual("sleeps under a roof", KingdomLodgingRules.HomeSuffix("", null));
		}

		[Test]
		public void HomeSuffix_IsCaseInsensitiveOnTheTag()
		{
			Assert.AreEqual("sleeps in the hut, under open sky", KingdomLodgingRules.HomeSuffix("hut", "SKY"));
		}

		// ==================================================================================
		// Addendum 4b -- housing binds. The arrival gate is assignment-level, the grace is
		// counted in ATTENDED passes, and a spent grace ends in departure.
		// ==================================================================================

		private static KingdomLodgingRules.ArrivalHome Home(int Capacity, int Occupants, bool OccupantsRefuse, params string[] Provides)
		{
			return new KingdomLodgingRules.ArrivalHome(Tags(Provides), Capacity, Occupants, OccupantsRefuse);
		}

		private static List<KingdomLodgingRules.ArrivalHome> Homes(params KingdomLodgingRules.ArrivalHome[] Values)
		{
			return new List<KingdomLodgingRules.ArrivalHome>(Values);
		}

		// --- The arrival gate ------------------------------------------------------------

		[Test]
		public void AnyWouldTake_AHomeThatMeetsEveryNeedTakesTheArrival()
		{
			Reason reason;
			Assert.IsTrue(KingdomLodgingRules.AnyWouldTake(Homes(Home(2, 0, false, "taf:charge")), Tags("taf:charge"), out reason));
			Assert.AreEqual(Reason.Housed, reason);
		}

		[Test]
		public void AnyWouldTake_EmptyBedsAreNotRoomWhenNoneOfThemMeetsTheStandardTheArrivalSets()
		{
			// The whole of Addendum 4b's arrival gate: ten beds and no charging post is no room at
			// all for the settler who needs one, and a bed tally could never say so. A mutation
			// that puts the bed count back in charge fails here.
			Reason reason;
			Assert.IsFalse(KingdomLodgingRules.AnyWouldTake(
				Homes(Home(10, 0, false), Home(10, 0, false, "taf:damp")), Tags("taf:charge"), out reason));
			Assert.AreEqual(Reason.NeedsUnmet, reason);
		}

		[Test]
		public void AnyWouldTake_AHomeThatMeetsTheNeedButIsFullIsNotRoom()
		{
			Reason reason;
			Assert.IsFalse(KingdomLodgingRules.AnyWouldTake(Homes(Home(2, 2, false, "taf:charge")), Tags("taf:charge"), out reason));
			Assert.AreEqual(Reason.Full, reason);
		}

		[Test]
		public void AnyWouldTake_AHomeWithRoomAndAnOccupantWhoRefusesThemIsNotRoom()
		{
			Reason reason;
			Assert.IsFalse(KingdomLodgingRules.AnyWouldTake(Homes(Home(2, 1, true, "taf:charge")), Tags("taf:charge"), out reason));
			Assert.AreEqual(Reason.Refused, reason);
		}

		[Test]
		public void AnyWouldTake_NoHousingAtAllIsNamedAsNoRoofRatherThanAsARefusal()
		{
			Reason reason;
			Assert.IsFalse(KingdomLodgingRules.AnyWouldTake(Homes(), Tags(), out reason));
			Assert.AreEqual(Reason.NoRoofAtAll, reason);
			Assert.IsFalse(KingdomLodgingRules.AnyWouldTake(null, Tags(), out reason));
			Assert.AreEqual(Reason.NoRoofAtAll, reason);
		}

		[Test]
		public void AnyWouldTake_OneAcceptableHomeAmongManyRefusalsStillTakesThem()
		{
			Reason reason;
			Assert.IsTrue(KingdomLodgingRules.AnyWouldTake(
				Homes(Home(1, 1, false, "taf:charge"), Home(2, 0, true, "taf:charge"), Home(1, 0, false, "taf:charge")),
				Tags("taf:charge"), out reason));
			Assert.AreEqual(Reason.Housed, reason);
		}

		[Test]
		public void AnyWouldTake_AnArrivalWhoNeedsNothingIsTakenByAHomeThatProvidesNothing()
		{
			// The unauthored catalogue, which is every design that shipped before this vocabulary:
			// no Provides anywhere, and arrivals go on arriving exactly as they always did.
			Reason reason;
			Assert.IsTrue(KingdomLodgingRules.AnyWouldTake(Homes(Home(1, 0, false)), Tags(), out reason));
			Assert.IsTrue(KingdomLodgingRules.AnyWouldTake(Homes(Home(1, 0, false)), null, out reason));
		}

		[Test]
		public void ArrivalRefusedChronicle_NamesTheRealReasonAndNotABedCount()
		{
			string line = KingdomLodgingRules.ArrivalRefusedChronicle("Kavvat", Reason.NeedsUnmet);
			Assert.IsTrue(line.Contains("Kavvat"), "the settlement is named");
			Assert.IsTrue(line.Contains("no home they would take"), "the ruling's own words: " + line);
			Assert.IsFalse(line.Contains("bed"), "a bed count is exactly what this replaces: " + line);
		}

		[Test]
		public void ArrivalRefusedChronicle_EachReasonReadsDifferently()
		{
			string noRoof = KingdomLodgingRules.ArrivalRefusedChronicle("Kavvat", Reason.NoRoofAtAll);
			string needs = KingdomLodgingRules.ArrivalRefusedChronicle("Kavvat", Reason.NeedsUnmet);
			string full = KingdomLodgingRules.ArrivalRefusedChronicle("Kavvat", Reason.Full);
			string refused = KingdomLodgingRules.ArrivalRefusedChronicle("Kavvat", Reason.Refused);
			Assert.AreNotEqual(noRoof, needs);
			Assert.AreNotEqual(needs, full);
			Assert.AreNotEqual(full, refused);
			Assert.AreNotEqual(needs, refused);
		}

		[Test]
		public void ArrivalRefusedNote_TellsTheFounderWhatToGoAndDo()
		{
			Assert.IsTrue(KingdomLodgingRules.ArrivalRefusedNote(Reason.NeedsUnmet).Contains("Commission housing"));
			Assert.IsTrue(KingdomLodgingRules.ArrivalRefusedNote(Reason.NoRoofAtAll).Contains("Commission housing"));
		}

		[Test]
		public void ArrivalRefusedChronicle_ABlankSettlementNameStillReadsAsASentence()
		{
			Assert.IsTrue(KingdomLodgingRules.ArrivalRefusedChronicle(null, Reason.NeedsUnmet).Contains("the settlement"));
			Assert.IsTrue(KingdomLodgingRules.ArrivalRefusedChronicle("   ", Reason.NeedsUnmet).Contains("the settlement"));
		}

		// --- The grace: attended passes, and nothing else -------------------------------

		[Test]
		public void GracePasses_IsTwo()
		{
			Assert.AreEqual(2, KingdomLodgingRules.GracePasses, "the ruling says a grace of two attended passes");
		}

		[Test]
		public void GraceAfterPass_TheFirstPassAnnouncesAndSpendsNothing()
		{
			Assert.AreEqual(0, KingdomLodgingRules.GraceAfterPass(KingdomLodgingRules.NoGrace));
			Assert.IsFalse(KingdomLodgingRules.GraceRunOut(0), "the pass a loss is announced on is not the pass they leave on");
		}

		[TestCase(0, false)]
		[TestCase(1, false)]
		[TestCase(2, true)]
		[TestCase(3, true)]
		public void GraceRunOut_LeavesAtExactlyTwoAttendedPassesAndNotBefore(int grace, bool expected)
		{
			Assert.AreEqual(expected, KingdomLodgingRules.GraceRunOut(grace));
		}

		[Test]
		public void TheGraceIsExactlyTwoAttendedPassesAfterTheOneThatAnnouncedIt()
		{
			// Driven as the pass drives it, so a mutation to either half -- the advance or the
			// threshold -- moves the departure and fails here.
			int grace = KingdomLodgingRules.NoGrace;
			grace = KingdomLodgingRules.GraceAfterPass(grace);
			Assert.IsFalse(KingdomLodgingRules.GraceRunOut(grace), "pass 1: announced, and nobody leaves");
			grace = KingdomLodgingRules.GraceAfterPass(grace);
			Assert.IsFalse(KingdomLodgingRules.GraceRunOut(grace), "pass 2: the first of the two");
			grace = KingdomLodgingRules.GraceAfterPass(grace);
			Assert.IsTrue(KingdomLodgingRules.GraceRunOut(grace), "pass 3: the second of the two is spent, and they go");
		}

		[Test]
		public void AbsenceNeverRunsTheGraceBecauseNothingButAPassAdvancesIt()
		{
			// The founder is away: no attended pass runs, so GraceAfterPass is never called, and
			// the settler's grace is exactly where they left it however long the founder is gone.
			// Nothing in this file reads a clock, an age, or a tick -- there is no other input a
			// passing day could reach.
			int grace = KingdomLodgingRules.GraceAfterPass(KingdomLodgingRules.NoGrace);
			Assert.AreEqual(0, grace);
			Assert.IsFalse(KingdomLodgingRules.GraceRunOut(grace), "still held after any amount of absence");
			Assert.AreEqual(1, KingdomLodgingRules.GraceAfterPass(grace), "and one attended pass advances it by exactly one");
		}

		[Test]
		public void GraceAfterPass_AnyNegativeSentinelEntersAtZeroRatherThanCountingUpFromIt()
		{
			Assert.AreEqual(0, KingdomLodgingRules.GraceAfterPass(-1));
			Assert.AreEqual(0, KingdomLodgingRules.GraceAfterPass(-7));
		}

		// --- The leaving ----------------------------------------------------------------

		[Test]
		public void DepartureCause_NamesTheHousingAndNotTheDrought()
		{
			// The cause both registers carry. The drought's own clause is KingdomGrowth's default
			// and must not be what a housing departure is written down as.
			Assert.IsTrue(KingdomLodgingRules.DepartureCause.Contains("roof"));
			Assert.IsFalse(KingdomLodgingRules.DepartureCause.Contains("cistern"));
			Assert.IsFalse(KingdomLodgingRules.DepartureCause.Contains("wetter"));
		}

		[Test]
		public void LeavingLine_NamesThePersonAndSaysTheyAreGoing()
		{
			string line = KingdomLodgingRules.LeavingLine("Vashti");
			Assert.IsTrue(line.StartsWith("Vashti"), line);
			Assert.IsTrue(line.Contains("leaving"), line);
		}

		[Test]
		public void LeavingLine_ANamelessSettlerStillReadsAsASentence()
		{
			Assert.IsTrue(KingdomLodgingRules.LeavingLine(null).StartsWith("a settler"));
			Assert.IsTrue(KingdomLodgingRules.LeavingLine("").StartsWith("a settler"));
		}

		// --- The shipped vocabulary reads in prose ---------------------------------------

		[Test]
		public void HomeSuffix_TheShippedNamespacedTagsColourTheLineToo()
		{
			// The catalogue ships taf:charge, not charge. A flavour table that only knew the bare
			// words would silently stop colouring every line in the game.
			Assert.AreEqual("sleeps in the charging shed, by the charging post",
				KingdomLodgingRules.HomeSuffix("charging shed", KingdomQolRules.TagCharge));
			Assert.AreEqual("sleeps in the reservoir yard, by the water",
				KingdomLodgingRules.HomeSuffix("reservoir yard", KingdomQolRules.TagOpenWater));
			Assert.AreEqual("sleeps in the cellar, in the damp dark",
				KingdomLodgingRules.HomeSuffix("cellar", KingdomQolRules.TagDamp));
		}

		// ==================================================================================
		// Addendum 4c -- feelings scale with closeness. The single CohabitHostility floor is a
		// four-rung ladder: what a tent refuses, a stone house carries. Every rung's boundary is
		// pinned here, both sides, so moving a threshold or flipping a comparison fails a test
		// rather than quietly changing who may live where.
		// ==================================================================================

		// --- The derivation: beds against the ground the tier stands on -----------------------

		[TestCase(1, 3, Quarters.Packed)]
		[TestCase(1, 4, Quarters.Close)]
		[TestCase(1, 5, Quarters.Close)]
		[TestCase(1, 6, Quarters.Roomed)]
		[TestCase(1, 9, Quarters.Roomed)]
		[TestCase(1, 10, Quarters.Private)]
		[TestCase(1, 1000, Quarters.Private)]
		public void ClosenessFromDensity_EachRungBoundaryIsExact(int beds, int cells, Quarters expected)
		{
			Assert.AreEqual(expected, KingdomLodgingRules.ClosenessFromDensity(cells, beds));
		}

		[TestCase(3, 11, Quarters.Packed)]
		[TestCase(3, 12, Quarters.Close)]
		[TestCase(3, 17, Quarters.Close)]
		[TestCase(3, 18, Quarters.Roomed)]
		[TestCase(3, 29, Quarters.Roomed)]
		[TestCase(3, 30, Quarters.Private)]
		public void ClosenessFromDensity_TheBoundariesScaleWithTheBedCountAndNeverRound(int beds, int cells, Quarters expected)
		{
			// The thresholds are multiplied out rather than the density divided down, so a rung
			// boundary never lands on a rounding direction. Three beds move every boundary to
			// exactly three times where one bed put it.
			Assert.AreEqual(expected, KingdomLodgingRules.ClosenessFromDensity(cells, beds));
		}

		[TestCase(0, 0)]
		[TestCase(0, 4)]
		[TestCase(12, 0)]
		[TestCase(-8, 2)]
		[TestCase(12, -2)]
		public void ClosenessFromDensity_ADegenerateReadingIsTheTightestRungAndNotTheRoomiest(int cells, int beds)
		{
			// A roof the registry cannot measure is one cell with a bunk in it, not a manor. The
			// safe answer to a gate with no arithmetic behind it is the strict one.
			Assert.AreEqual(Quarters.Packed, KingdomLodgingRules.ClosenessFromDensity(cells, beds));
		}

		// --- The shipped catalogue, design by design ------------------------------------------

		// Footprint cells and beds exactly as KingdomBuildings.xml declares them: a tier's own
		// Footprint where it has one, and the whole plot (S 5x4, M 8x6, L 12x9, XL 20x14) where it
		// fills its plot. If a design's Carries or Footprint is rebalanced, this table is where the
		// rung it lands on has to be re-agreed.
		[TestCase("tent", 6, 2, Quarters.Packed)]
		[TestCase("tentrow", 10, 3, Quarters.Packed)]
		[TestCase("hut", 12, 3, Quarters.Close)]
		[TestCase("hutyard", 20, 5, Quarters.Close)]
		[TestCase("house", 48, 8, Quarters.Roomed)]
		[TestCase("court", 280, 40, Quarters.Roomed)]
		[TestCase("finehouse", 48, 4, Quarters.Private)]
		[TestCase("manor", 108, 6, Quarters.Private)]
		public void ClosenessFromDensity_TheShippedDesignsLandOnTheRungsTheRulingNames(string design, int cells, int beds, Quarters expected)
		{
			// Addendum 4c's own examples: tent and bunk row Packed, hut Close, stone house Roomed,
			// fine house and manor Private -- all four derived from the arithmetic and none of them
			// declared.
			Assert.AreEqual(expected, KingdomLodgingRules.ClosenessFromDensity(cells, beds), design);
		}

		[TestCase("housecourt", 48, 18, Quarters.Packed)]
		[TestCase("terrace", 108, 26, Quarters.Close)]
		public void ClosenessFromDensity_TheTwoMultiDwellingDesignsAreExactlyWhereTheDerivationReadsWrong(string design, int cells, int beds, Quarters derived)
		{
			// Three households around a square and a whole terraced street put many beds on little
			// ground and measure tighter than the single stone house whose walls they repeat. This
			// is why they are the only two entries in the catalogue carrying a Closeness override,
			// and this test is the evidence that the override is needed rather than decorative.
			Assert.AreEqual(derived, KingdomLodgingRules.ClosenessFromDensity(cells, beds), design);
			Assert.AreNotEqual(Quarters.Roomed, derived, design + " would not need an override if the arithmetic already agreed");
		}

		[Test]
		public void TheDeclaredClosenessIsWhatTheCatalogueOverridesWith()
		{
			// What KingdomLodging does with the attribute, in the order it does it: parse the
			// declaration, and only measure when there is none. The override wins over an
			// arithmetic that says Packed.
			Quarters declared;
			Assert.IsTrue(KingdomLodgingRules.TryParseCloseness("Roomed", out declared));
			Assert.AreEqual(Quarters.Roomed, declared);
			Assert.AreEqual(Quarters.Packed, KingdomLodgingRules.ClosenessFromDensity(48, 18), "the housecourt's own arithmetic");
			Assert.AreNotEqual(KingdomLodgingRules.ClosenessFromDensity(48, 18), declared, "and the declaration is what the design gets");
		}

		// --- Parsing the attribute -------------------------------------------------------------

		[TestCase("Packed", Quarters.Packed)]
		[TestCase("close", Quarters.Close)]
		[TestCase("ROOMED", Quarters.Roomed)]
		[TestCase("  Private  ", Quarters.Private)]
		public void TryParseCloseness_FoldsCaseAndSurroundingWhitespace(string raw, Quarters expected)
		{
			Quarters parsed;
			Assert.IsTrue(KingdomLodgingRules.TryParseCloseness(raw, out parsed), raw);
			Assert.AreEqual(expected, parsed);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		[TestCase("cosy")]
		[TestCase("Packed,Close")]
		public void TryParseCloseness_AnythingElseIsRefusedSoTheCallerFallsBackToMeasuring(string raw)
		{
			Quarters parsed;
			Assert.IsFalse(KingdomLodgingRules.TryParseCloseness(raw, out parsed), raw ?? "null");
		}

		[Test]
		public void ClosenessNames_AreTheEnumInRungOrderSoTheParseAndTheEnumCannotDrift()
		{
			Assert.AreEqual(4, KingdomLodgingRules.ClosenessNames.Length);
			for (int i = 0; i < KingdomLodgingRules.ClosenessNames.Length; i++)
			{
				Quarters parsed;
				Assert.IsTrue(KingdomLodgingRules.TryParseCloseness(KingdomLodgingRules.ClosenessNames[i], out parsed));
				Assert.AreEqual((Quarters)i, parsed, KingdomLodgingRules.ClosenessNames[i]);
			}
		}

		// --- The ladder itself -------------------------------------------------------------

		[TestCase(Quarters.Packed, 1)]
		[TestCase(Quarters.Close, 50)]
		[TestCase(Quarters.Roomed, 75)]
		[TestCase(Quarters.Private, 100)]
		public void RefusalHostility_IsTheRulingsOwnFourThresholds(Quarters quarters, int expected)
		{
			Assert.AreEqual(expected, KingdomLodgingRules.RefusalHostility(quarters));
		}

		[Test]
		public void RefusalHostility_RisesStrictlyWithTheRoomSoBetterQuartersAlwaysHoldWorseFeelings()
		{
			// The whole of Addendum 4c in one assertion: no rung ever tolerates less than a
			// tighter one. A mutation that swaps two rungs fails here.
			Assert.Less(KingdomLodgingRules.RefusalHostility(Quarters.Packed), KingdomLodgingRules.RefusalHostility(Quarters.Close));
			Assert.Less(KingdomLodgingRules.RefusalHostility(Quarters.Close), KingdomLodgingRules.RefusalHostility(Quarters.Roomed));
			Assert.Less(KingdomLodgingRules.RefusalHostility(Quarters.Roomed), KingdomLodgingRules.RefusalHostility(Quarters.Private));
		}

		[Test]
		public void ThePackedRungIsExactlyTheOldFloorAndThePrivateRungIsExactlyTheOldCohabitCeiling()
		{
			// Nothing was thrown away. The tightest rung restates KingdomLodgingRules' own creed
			// floor -- any enmity at all refuses -- and the roomiest restates the single
			// CohabitHostility the vocabulary shipped with, which used to be applied to every roof
			// in the settlement and now applies only where everybody has a door of their own.
			Assert.AreEqual(KingdomLodgingRules.CreedRefusalHostilityFloor + 1, KingdomLodgingRules.PackedRefusalHostility);
			Assert.AreEqual(KingdomQolRules.CohabitHostility, KingdomLodgingRules.PrivateRefusalHostility);
		}

		[TestCase(Quarters.Packed, 0, false)]
		[TestCase(Quarters.Packed, 1, true)]
		[TestCase(Quarters.Close, 49, false)]
		[TestCase(Quarters.Close, 50, true)]
		[TestCase(Quarters.Roomed, 74, false)]
		[TestCase(Quarters.Roomed, 75, true)]
		[TestCase(Quarters.Private, 99, false)]
		[TestCase(Quarters.Private, 100, true)]
		public void Conflicts_EachRungRefusesAtItsOwnThresholdAndCarriesOneShortOfIt(Quarters quarters, int hostility, bool expected)
		{
			Assert.AreEqual(expected, KingdomLodgingRules.Conflicts(Tags(), Tags(), Tags(), Tags(), hostility, quarters));
		}

		[TestCase(Quarters.Packed, true)]
		[TestCase(Quarters.Close, true)]
		[TestCase(Quarters.Roomed, false)]
		[TestCase(Quarters.Private, false)]
		public void Conflicts_TheAmbientFiftyGrudgeBreaksATentAndAHutAndIsCarriedByAHouse(Quarters quarters, bool expected)
		{
			// The standing -50 fifty-three faction pairs hold toward everyone they have not
			// troubled to name. This is the case the ruling is about: a mixed city cannot bunk
			// together and can live in stone.
			Assert.AreEqual(expected, KingdomLodgingRules.Conflicts(Tags(), Tags(), Tags(), Tags(), 50, quarters));
		}

		[TestCase(Quarters.Packed)]
		[TestCase(Quarters.Close)]
		[TestCase(Quarters.Roomed)]
		[TestCase(Quarters.Private)]
		public void Conflicts_TheFlatHundredFaultLineRefusesAtEveryRungIncludingTheRoomiest(Quarters quarters)
		{
			// The Templar and the Girsh do not share a manor either.
			Assert.IsTrue(KingdomLodgingRules.Conflicts(Tags(), Tags(), Tags(), Tags(), 100, quarters));
		}

		[TestCase(Quarters.Packed)]
		[TestCase(Quarters.Close)]
		[TestCase(Quarters.Roomed)]
		[TestCase(Quarters.Private)]
		public void Conflicts_OneCreedSharesAnythingAtEveryRung(Quarters quarters)
		{
			// Same creed reads as zero hostility (KingdomCreedRules.Hostility short-circuits it),
			// and zero clears every rung of the ladder. Believers of one creed are never kept apart
			// by these quarters or any other.
			Assert.IsFalse(KingdomLodgingRules.Conflicts(Tags(), Tags(), Tags(), Tags(), 0, quarters));
		}

		[TestCase(Quarters.Packed)]
		[TestCase(Quarters.Close)]
		[TestCase(Quarters.Roomed)]
		[TestCase(Quarters.Private)]
		public void Conflicts_ARefusesTagIsAbsoluteAtEveryClosenessAndNoAmountOfRoomSoftensIt(Quarters quarters)
		{
			// The ladder scales the creed half and nothing else. A Refuses names a thing about the
			// other person that a wall does not fix -- so it fires with zero hostility, in a manor,
			// and in both directions.
			Assert.IsTrue(KingdomLodgingRules.Conflicts(Tags("taf:damp"), Tags(), Tags(), Tags("taf:damp"), 0, quarters), "A refuses B");
			Assert.IsTrue(KingdomLodgingRules.Conflicts(Tags(), Tags("taf:damp"), Tags("taf:damp"), Tags(), 0, quarters), "B refuses A");
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(50)]
		[TestCase(100)]
		public void Conflicts_TheClosenessFreeOverloadJudgesTheTightestQuartersThereAre(int hostility)
		{
			// A caller that has not said what the quarters were gets Packed, which is the only safe
			// reading and is also exactly the rule the five-argument form has always applied.
			Assert.AreEqual(
				KingdomLodgingRules.Conflicts(Tags(), Tags(), Tags(), Tags(), hostility, Quarters.Packed),
				KingdomLodgingRules.Conflicts(Tags(), Tags(), Tags(), Tags(), hostility));
		}

		// --- Five mixed believers, and one bunkhouse -----------------------------------------

		// One home filling up, exactly as the pass fills it: each arrival is judged against
		// everybody already seated, and somebody refused simply never moves in (Addendum 4b -- the
		// refused never join). Returns how many of them ended up under the one roof.
		private static int SeatedInOneHome(Quarters quarters, int[][] Hostility)
		{
			List<int> seated = new List<int>();
			for (int i = 0; i < Hostility.Length; i++)
			{
				bool refused = false;
				for (int j = 0; j < seated.Count; j++)
				{
					if (KingdomLodgingRules.Conflicts(Tags(), Tags(), Tags(), Tags(), Hostility[i][seated[j]], quarters))
					{
						refused = true;
						break;
					}
				}
				if (!refused)
				{
					seated.Add(i);
				}
			}
			return seated.Count;
		}

		// Five people of five different creeds, every pair holding the ambient grudge toward every
		// other, nobody holding anything against themselves.
		private static int[][] FiveMixedBelievers(int Between)
		{
			int[][] matrix = new int[5][];
			for (int i = 0; i < 5; i++)
			{
				matrix[i] = new int[5];
				for (int j = 0; j < 5; j++)
				{
					matrix[i][j] = (i == j) ? 0 : Between;
				}
			}
			return matrix;
		}

		[Test]
		public void FiveMixedBelieversWillNotShareOneBunkhouseAndWillShareOneRoomedHouse()
		{
			// The ruling's own sentence: "You cannot jam five different believers into one
			// bunkhouse and have it be fine." One of the five gets the bunk row and the other four
			// never join; the same five fill a stone house. This is the consequence the addendum
			// calls intended -- a diverse city must build better housing to exist.
			int[][] mixed = FiveMixedBelievers(50);
			Assert.AreEqual(1, SeatedInOneHome(Quarters.Packed, mixed), "the bunk row seats one of the five");
			Assert.AreEqual(1, SeatedInOneHome(Quarters.Close, mixed), "and so does the hut");
			Assert.AreEqual(5, SeatedInOneHome(Quarters.Roomed, mixed), "the stone house takes all five");
			Assert.AreEqual(5, SeatedInOneHome(Quarters.Private, mixed), "and so does the fine house");
		}

		[TestCase(Quarters.Packed)]
		[TestCase(Quarters.Close)]
		[TestCase(Quarters.Roomed)]
		[TestCase(Quarters.Private)]
		public void FiveBelieversOfOneCreedShareAnythingIncludingTheBunkhouse(Quarters quarters)
		{
			Assert.AreEqual(5, SeatedInOneHome(quarters, FiveMixedBelievers(0)));
		}

		[Test]
		public void FiveMixedBelieversStillWillNotShareAManorAcrossAFaultLine()
		{
			// Roomier housing answers the ambient grudge and does not answer hatred. Nothing the
			// founder builds puts the Templar and the Girsh in one household.
			Assert.AreEqual(1, SeatedInOneHome(Quarters.Private, FiveMixedBelievers(100)));
		}

		// --- Composition with Addendum 4b: the refused never join ---------------------------

		[Test]
		public void AnyWouldTake_TheBunkRowRefusesTheMixedArrivalAndTheStoneHouseTakesThem()
		{
			// The arrival gate reads the same pair through the same ladder: OccupantsRefuse is
			// exactly Conflicts at the home's own rung, so raising better housing is what turns a
			// refused arrival into a settler.
			bool refusedInABunkRow = KingdomLodgingRules.Conflicts(Tags(), Tags(), Tags(), Tags(), 50, Quarters.Packed);
			bool refusedInAHouse = KingdomLodgingRules.Conflicts(Tags(), Tags(), Tags(), Tags(), 50, Quarters.Roomed);
			Reason reason;
			Assert.IsFalse(KingdomLodgingRules.AnyWouldTake(Homes(Home(3, 1, refusedInABunkRow)), Tags(), out reason));
			Assert.AreEqual(Reason.Refused, reason, "and it is named as a refusal, never as a bed count");
			Assert.IsTrue(KingdomLodgingRules.AnyWouldTake(Homes(Home(3, 1, refusedInAHouse)), Tags(), out reason));
			Assert.AreEqual(Reason.Housed, reason);
		}

		// --- Naming the quarters (STANDARDS 7b) ---------------------------------------------

		[TestCase(Quarters.Packed, "one open room")]
		[TestCase(Quarters.Close, "a hut's close quarters")]
		[TestCase(Quarters.Roomed, "a house with walls between the beds")]
		[TestCase(Quarters.Private, "a house of their own")]
		public void QuartersPhrase_NamesTheArchitectureRatherThanTheRung(Quarters quarters, string expected)
		{
			// A founder acts on walls, not on a word this mod invented. The rung names never reach
			// the player.
			Assert.AreEqual(expected, KingdomLodgingRules.QuartersPhrase(quarters));
		}

		[Test]
		public void UnhousedLine_ARefusalNamesTheRoomiestQuartersThatStillWouldNotTakeThem()
		{
			string line = KingdomLodgingRules.UnhousedLine("Vashti", Reason.Refused, Quarters.Close);
			StringAssert.StartsWith("Vashti sleeps in the open", line);
			StringAssert.Contains("will not live beside", line);
			StringAssert.Contains("The roomiest of them is a hut's close quarters.", line);
		}

		[TestCase(Reason.NoRoofAtAll)]
		[TestCase(Reason.NeedsUnmet)]
		[TestCase(Reason.Full)]
		public void UnhousedLine_EveryOtherReasonReadsWordForWordAsItDidBeforeTheQuartersExisted(Reason reason)
		{
			// Naming the quarters says nothing at all about a settlement with no roof standing, or
			// one whose every bed is taken. Only a refusal is about the room.
			Assert.AreEqual(
				KingdomLodgingRules.UnhousedLine("Vashti", reason),
				KingdomLodgingRules.UnhousedLine("Vashti", reason, Quarters.Roomed));
		}

		[Test]
		public void Roomier_KeepsTheBestQuartersThatStillRefusedSoTheFounderKnowsWhatToBeat()
		{
			Assert.AreEqual(Quarters.Roomed, KingdomLodgingRules.Roomier(Quarters.Packed, Quarters.Roomed));
			Assert.AreEqual(Quarters.Roomed, KingdomLodgingRules.Roomier(Quarters.Roomed, Quarters.Close));
			Assert.AreEqual(Quarters.Private, KingdomLodgingRules.Roomier(Quarters.Private, Quarters.Private));
		}
	}
}
#endif
