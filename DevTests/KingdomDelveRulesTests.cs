#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The delve: the work that opens the rock a claim already reaches.
	/// <para>
	/// The section that matters most is deliberately first: <b>a shaft goes straight down, one
	/// stratum, into rock.</b> Everything else in this file is downstream of that sentence, and
	/// the claim machinery is deliberately NOT downstream of any of it &mdash; ground stays cheap
	/// to own. What a shaft buys is the ability to WORK the rock, and the tests that prove it are
	/// the reach section.
	/// </para>
	/// </summary>
	public class KingdomDelveRulesTests
	{
		// Zone ids in the engine's own six-part shape: World.parasangX.parasangY.zoneX.zoneY.Z,
		// with ten the surface (KingdomRules.SurfaceZLevel) and larger deeper.
		private static string Zone(int ZoneX, int ZoneY, int Z)
		{
			return "JoppaWorld.11.22." + ZoneX + "." + ZoneY + "." + Z;
		}

		private static List<string> List(params string[] Items)
		{
			return new List<string>(Items);
		}

		// ==================================================================================
		// A shaft goes straight down, one stratum, into rock.
		// ==================================================================================

		[Test]
		public void AShaftIsOneStratumStraightDown()
		{
			Assert.IsTrue(KingdomDelveRules.IsShaftPair(Zone(1, 1, 10), Zone(1, 1, 11)));
		}

		[Test]
		public void AShaftNeverSkipsAStratum()
		{
			Assert.IsFalse(KingdomDelveRules.IsShaftPair(Zone(1, 1, 10), Zone(1, 1, 12)),
				"two strata down is two shafts, and the middle one has to be cut first");
		}

		[Test]
		public void AShaftNeverGoesUpAndAcross()
		{
			Assert.IsFalse(KingdomDelveRules.IsShaftPair(Zone(1, 1, 10), Zone(2, 1, 11)));
			Assert.IsFalse(KingdomDelveRules.IsShaftPair(Zone(1, 1, 10), Zone(2, 2, 11)));
		}

		[Test]
		public void AShaftIsNotSunkUpwards()
		{
			Assert.IsFalse(KingdomDelveRules.IsShaftPair(Zone(1, 1, 11), Zone(1, 1, 10)),
				"the head is the shallower end, always");
		}

		[Test]
		public void AShaftNeverEndsAboveTheRock()
		{
			// A stair up the inside of a tower is a different building in a set nobody has
			// written. If this ever passes, the sky arrived through the delve's back door.
			Assert.IsFalse(KingdomDelveRules.IsShaftPair(Zone(1, 1, 9), Zone(1, 1, 10)));
		}

		[Test]
		public void ADifferentWorldIsNotUnderThisOne()
		{
			Assert.IsFalse(KingdomDelveRules.IsShaftPair(Zone(1, 1, 10), "OtherWorld.11.22.1.1.11"));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("JoppaWorld.11.22.1.1")]
		[TestCase("JoppaWorld.11.22.1.1.10.4")]
		[TestCase("not a zone at all")]
		public void AMalformedIdRefusesTheShaftRatherThanGuessingAtIt(string bad)
		{
			Assert.IsFalse(KingdomDelveRules.IsShaftPair(bad, Zone(1, 1, 11)));
			Assert.IsFalse(KingdomDelveRules.IsShaftPair(Zone(1, 1, 10), bad));
		}

		[TestCase(10, 11, true, true)]
		[TestCase(10, 11, false, false)]
		[TestCase(11, 12, true, true)]
		[TestCase(10, 12, true, false)]
		[TestCase(11, 10, true, false)]
		[TestCase(9, 10, true, false)]
		[TestCase(10, 10, true, false)]
		public void TheStrataRuleIsTheSameOneTheRoutingGraphAsks(int head, int foot, bool cut, bool expected)
		{
			// The graph has already named the direction off coordinates it carries, so it asks this
			// rather than re-deriving the geometry from a zone id. One sentence, two callers: if
			// these ever disagree, a carrier walks down a shaft the catalogue says is not there.
			Assert.AreEqual(expected, KingdomDelveRules.ShaftJoinsStrata(head, foot, cut));
		}

		// ==================================================================================
		// The building is the fact.
		// ==================================================================================

		[Test]
		public void RockUnderNoShaftIsJoinedToNothing()
		{
			Assert.IsFalse(KingdomDelveRules.ShaftJoins(Zone(1, 1, 10), Zone(1, 1, 11), null));
			Assert.IsFalse(KingdomDelveRules.ShaftJoins(Zone(1, 1, 10), Zone(1, 1, 11), List(Zone(2, 1, 10))));
		}

		[Test]
		public void AFinishedShaftJoinsItsTwoEnds()
		{
			Assert.IsTrue(KingdomDelveRules.ShaftJoins(Zone(1, 1, 10), Zone(1, 1, 11), List(Zone(1, 1, 10))));
		}

		[Test]
		public void AShaftInTheWrongZoneJoinsNothing()
		{
			// The delve is recorded against the zone it STANDS in, never the one it opens. If
			// this ever passes, the two lists were read the wrong way round.
			Assert.IsFalse(KingdomDelveRules.ShaftJoins(Zone(1, 1, 10), Zone(1, 1, 11), List(Zone(1, 1, 11))));
		}

		[TestCase("delve", true)]
		[TestCase("Delve", true)]
		[TestCase("  DELVE  ", true)]
		[TestCase("delvehead", false)]
		[TestCase("gatehouse", false)]
		[TestCase(null, false)]
		[TestCase("", false)]
		public void TheDesignThatOpensGroundIsKnownByItsKey(string key, bool expected)
		{
			Assert.AreEqual(expected, KingdomDelveRules.IsDelve(key));
		}

		// ==================================================================================
		// Reach: the ground a city can work, as against the ground it merely owns.
		// ==================================================================================

		[Test]
		public void NothingClaimedIsNothingReached()
		{
			Assert.AreEqual(0, KingdomDelveRules.ReachedZones(null, null).Count);
			Assert.AreEqual(0, KingdomDelveRules.ReachedZones(List(), List()).Count);
		}

		[Test]
		public void EveryPieceOfSurfaceIsReachedWhetherOrNotItTouchesTheRest()
		{
			// The world between two claims is walkable and always was: a realm is never asked to
			// pave the wilderness to reach its own second parasang.
			List<string> claimed = List(Zone(1, 1, 10), Zone(2, 2, 10));
			List<string> reached = KingdomDelveRules.ReachedZones(claimed, null);
			Assert.AreEqual(2, reached.Count);
			Assert.IsTrue(reached.Contains(Zone(1, 1, 10)));
			Assert.IsTrue(reached.Contains(Zone(2, 2, 10)));
		}

		[Test]
		public void RockIsHeldAndNotReachedUntilSomebodyCutsToIt()
		{
			List<string> claimed = List(Zone(1, 1, 10), Zone(1, 1, 11));
			List<string> reached = KingdomDelveRules.ReachedZones(claimed, null);
			Assert.AreEqual(1, reached.Count);
			Assert.AreEqual(Zone(1, 1, 10), reached[0]);
			List<string> waiting = KingdomDelveRules.UnreachedZones(claimed, null);
			Assert.AreEqual(1, waiting.Count);
			Assert.AreEqual(Zone(1, 1, 11), waiting[0]);
		}

		[Test]
		public void AShaftMakesTheRockBelowItReached()
		{
			List<string> claimed = List(Zone(1, 1, 10), Zone(1, 1, 11));
			Assert.IsTrue(KingdomDelveRules.Reaches(Zone(1, 1, 11), claimed, List(Zone(1, 1, 10))));
			Assert.AreEqual(0, KingdomDelveRules.UnreachedZones(claimed, List(Zone(1, 1, 10))).Count);
		}

		[Test]
		public void TheDeepSpreadsSidewaysFromTheFootOfTheShaft()
		{
			// One shaft opens a stratum, not a single parasang of it: underground, sideways is
			// walking, and walking is free.
			List<string> claimed = List(Zone(1, 1, 10), Zone(1, 1, 11), Zone(2, 1, 11));
			Assert.IsTrue(KingdomDelveRules.Reaches(Zone(2, 1, 11), claimed, List(Zone(1, 1, 10))));
		}

		[Test]
		public void TheDeepDoesNotSpreadThroughACorner()
		{
			// The claim may be taken across a corner and it is legal ground. Nobody can carry a
			// load through the corner, which is the call the routing graph already makes.
			List<string> claimed = List(Zone(1, 1, 10), Zone(1, 1, 11), Zone(2, 2, 11));
			Assert.IsFalse(KingdomDelveRules.Reaches(Zone(2, 2, 11), claimed, List(Zone(1, 1, 10))));
		}

		[Test]
		public void ASecondStratumWantsASecondShaft()
		{
			List<string> claimed = List(Zone(1, 1, 10), Zone(1, 1, 11), Zone(1, 1, 12));
			Assert.IsFalse(KingdomDelveRules.Reaches(Zone(1, 1, 12), claimed, List(Zone(1, 1, 10))));
			Assert.IsTrue(KingdomDelveRules.Reaches(Zone(1, 1, 12), claimed, List(Zone(1, 1, 10), Zone(1, 1, 11))));
		}

		[Test]
		public void AShaftSunkFromGroundNothingReachesOpensNothing()
		{
			// The second shaft cannot precede the first. If this ever fails, a founder can open
			// the bottom of the world by declaring it.
			List<string> claimed = List(Zone(1, 1, 10), Zone(1, 1, 11), Zone(1, 1, 12));
			List<string> reached = KingdomDelveRules.ReachedZones(claimed, List(Zone(1, 1, 11)));
			Assert.AreEqual(1, reached.Count);
			Assert.AreEqual(Zone(1, 1, 10), reached[0]);
		}

		[Test]
		public void ARealmWhoseGroundIsAllUnderTheRockStillReachesItsOwnWorks()
		{
			// The seed is the SHALLOWEST ground held, not the surface, so no state of the world
			// leaves a realm unable to reach anything at all.
			List<string> claimed = List(Zone(1, 1, 11), Zone(2, 1, 11));
			Assert.AreEqual(2, KingdomDelveRules.ReachedZones(claimed, null).Count);
		}

		[Test]
		public void GroundTheRealmDoesNotHoldIsNeverReachedHoweverNearItLies()
		{
			List<string> claimed = List(Zone(1, 1, 10));
			Assert.IsFalse(KingdomDelveRules.Reaches(Zone(2, 1, 10), claimed, null));
			Assert.IsFalse(KingdomDelveRules.Reaches(null, claimed, null));
		}

		[Test]
		public void AZoneNamedTwiceIsOneZone()
		{
			List<string> claimed = List(Zone(1, 1, 10), Zone(1, 1, 10));
			Assert.AreEqual(1, KingdomDelveRules.ReachedZones(claimed, null).Count);
		}

		[Test]
		public void AMalformedClaimDisablesItsOwnRowAndNothingElse()
		{
			List<string> claimed = List(Zone(1, 1, 10), "not a zone", Zone(1, 1, 11));
			List<string> reached = KingdomDelveRules.ReachedZones(claimed, List(Zone(1, 1, 10)));
			Assert.AreEqual(2, reached.Count);
			Assert.IsTrue(reached.Contains(Zone(1, 1, 11)));
		}

		[Test]
		public void TheSameCityAnswersTheSameWayEveryTimeItIsAsked()
		{
			List<string> claimed = List(Zone(1, 1, 10), Zone(1, 1, 11), Zone(2, 1, 11));
			List<string> delved = List(Zone(1, 1, 10));
			List<string> first = KingdomDelveRules.ReachedZones(claimed, delved);
			List<string> second = KingdomDelveRules.ReachedZones(claimed, delved);
			Assert.AreEqual(first.Count, second.Count);
			for (int i = 0; i < first.Count; i++)
			{
				Assert.AreEqual(first[i], second[i]);
				Assert.AreEqual(claimed[i], first[i], "the answer is written in the order the claims were");
			}
		}

		// ==================================================================================
		// Judging a shaft.
		// ==================================================================================

		[Test]
		public void AnUnfoundedRealmSinksNothing()
		{
			Assert.AreEqual(KingdomDelveRules.DelveVerdict.NothingFoundedYet,
				KingdomDelveRules.JudgeDelve(false, Zone(1, 1, 10), List(Zone(1, 1, 10), Zone(1, 1, 11)), null));
		}

		[Test]
		public void AShaftIsSunkThroughTheCitysOwnFloor()
		{
			Assert.AreEqual(KingdomDelveRules.DelveVerdict.GroundIsNotOurs,
				KingdomDelveRules.JudgeDelve(true, Zone(2, 1, 10), List(Zone(1, 1, 10), Zone(1, 1, 11)), null));
		}

		[Test]
		public void AShaftWantsSomewhereToGo()
		{
			Assert.AreEqual(KingdomDelveRules.DelveVerdict.NoGroundBelow,
				KingdomDelveRules.JudgeDelve(true, Zone(1, 1, 10), List(Zone(1, 1, 10)), null));
		}

		[Test]
		public void RockClaimedSidewaysIsNotUnderTheGroundYouAreStandingOn()
		{
			Assert.AreEqual(KingdomDelveRules.DelveVerdict.NoGroundBelow,
				KingdomDelveRules.JudgeDelve(true, Zone(1, 1, 10), List(Zone(1, 1, 10), Zone(2, 1, 11)), null));
		}

		[Test]
		public void AShaftIsSunkFromGroundTheCrewCanAlreadyStandOn()
		{
			Assert.AreEqual(KingdomDelveRules.DelveVerdict.GroundIsUnreached,
				KingdomDelveRules.JudgeDelve(true, Zone(1, 1, 11), List(Zone(1, 1, 10), Zone(1, 1, 11), Zone(1, 1, 12)), null));
		}

		[Test]
		public void OneHoleInOneFloorIsOneHoleInOneFloor()
		{
			Assert.AreEqual(KingdomDelveRules.DelveVerdict.AlreadyDelved,
				KingdomDelveRules.JudgeDelve(true, Zone(1, 1, 10), List(Zone(1, 1, 10), Zone(1, 1, 11)), List(Zone(1, 1, 10))));
		}

		[Test]
		public void TheSecondShaftIsAllowedOnceTheFirstIsCut()
		{
			Assert.AreEqual(KingdomDelveRules.DelveVerdict.Allowed,
				KingdomDelveRules.JudgeDelve(true, Zone(1, 1, 11),
					List(Zone(1, 1, 10), Zone(1, 1, 11), Zone(1, 1, 12)), List(Zone(1, 1, 10))));
		}

		[Test]
		public void TheFirstShaftIsAllowedFromTheSurfaceOverClaimedRock()
		{
			Assert.AreEqual(KingdomDelveRules.DelveVerdict.Allowed,
				KingdomDelveRules.JudgeDelve(true, Zone(1, 1, 10), List(Zone(1, 1, 10), Zone(1, 1, 11)), null));
		}

		// ==================================================================================
		// STANDARDS 7b: every refusal names the lack AND what lifts it.
		// ==================================================================================

		[TestCase(KingdomDelveRules.DelveVerdict.NothingFoundedYet)]
		[TestCase(KingdomDelveRules.DelveVerdict.GroundIsNotOurs)]
		[TestCase(KingdomDelveRules.DelveVerdict.GroundIsUnreached)]
		[TestCase(KingdomDelveRules.DelveVerdict.NoGroundBelow)]
		[TestCase(KingdomDelveRules.DelveVerdict.AlreadyDelved)]
		public void EveryRefusalSaysSomethingAndTellsTheFounderWhatToDo(KingdomDelveRules.DelveVerdict verdict)
		{
			string said = KingdomDelveRules.DelveRefusal(verdict, "Kavvat");
			Assert.IsNotNull(said);
			Assert.Greater(said.Length, 30, "a refusal that only says no teaches nothing");
			Assert.IsTrue(said.EndsWith("."), "the founder is spoken to in sentences");
		}

		[Test]
		public void APermittedShaftIsToldNothing()
		{
			Assert.AreEqual("", KingdomDelveRules.DelveRefusal(KingdomDelveRules.DelveVerdict.Allowed, "Kavvat"));
		}

		[Test]
		public void ARefusalNamesTheCityAndAnUnnamedOneIsStillASentence()
		{
			Assert.IsTrue(KingdomDelveRules.DelveRefusal(KingdomDelveRules.DelveVerdict.NoGroundBelow, "Kavvat")
				.Contains("{{C|Kavvat}}"));
			Assert.IsTrue(KingdomDelveRules.DelveRefusal(KingdomDelveRules.DelveVerdict.NoGroundBelow, null)
				.Contains("the settlement"));
		}

		[Test]
		public void TheRefusalForUnworkableRockNamesTheBuildingThatLiftsIt()
		{
			string said = KingdomDelveRules.RefuseUnreached("Kavvat", "fungal vault");
			Assert.IsTrue(said.Contains("delve"), "a refusal that does not name the fix is a stall in silence");
			Assert.IsTrue(said.Contains("fungal vault"), "the founder is told what was refused, not only that something was");
		}

		[Test]
		public void TheRefusalForUnworkableRockStillReadsWithNothingNamed()
		{
			string said = KingdomDelveRules.RefuseUnreached(null, null);
			Assert.IsTrue(said.Contains("the settlement"));
			Assert.IsTrue(said.Contains("delve"));
		}

		[Test]
		public void TheShaftSaysWhatItOpened()
		{
			string said = KingdomDelveRules.ShaftOpens("Kavvat");
			Assert.IsTrue(said.Contains("{{C|Kavvat}}"));
			Assert.IsTrue(said.Contains("delve"));
		}

		[Test]
		public void GroundWaitingOnAShaftSaysSoOnceHoweverMuchOfItThereIs()
		{
			Assert.IsNull(KingdomDelveRules.UnreachedNote("Kavvat", 0));
			Assert.IsNull(KingdomDelveRules.UnreachedNote("Kavvat", -1));
			string one = KingdomDelveRules.UnreachedNote("Kavvat", 1);
			Assert.IsTrue(one.Contains("one parasang"));
			Assert.IsTrue(one.Contains("delve"));
			Assert.IsTrue(KingdomDelveRules.UnreachedNote("Kavvat", 3).Contains("3 parasangs"));
		}

		// ==================================================================================
		// The connection: what the descent costs anyone carrying a load.
		// ==================================================================================

		[Test]
		public void AShaftCostsThreeOrdinaryHops()
		{
			// The level hop the routing graph is written in is forty cells, half a zone's width.
			Assert.AreEqual(120, KingdomDelveRules.ShaftHopCells(40));
			Assert.AreEqual(3, KingdomDelveRules.ShaftHopMultiplier);
		}

		[Test]
		public void TheDescentIsAlwaysDearerThanWalkingOnTheLevel()
		{
			// The catalogue promises the asymmetry out loud: a deep city is hand-cheap and
			// ceilinged, and this is the half of it the haul pays.
			for (int hop = 1; hop <= 200; hop++)
			{
				Assert.Greater(KingdomDelveRules.ShaftHopCells(hop), hop);
			}
		}

		[TestCase(0)]
		[TestCase(-1)]
		[TestCase(-40)]
		public void ANonPositiveHopIsNeverANegativeDistance(int hop)
		{
			Assert.AreEqual(0, KingdomDelveRules.ShaftHopCells(hop));
		}
	}
}
#endif
