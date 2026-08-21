#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;
using Candidate = ThousandAndFirst.KingdomLodgingRules.LodgingCandidate;
using Reason = ThousandAndFirst.KingdomLodgingRules.UnhousedReason;

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
	}
}
#endif
