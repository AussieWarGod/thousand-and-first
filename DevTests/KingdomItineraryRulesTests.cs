#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The itinerary. LIVING-CITY-ARCHITECTURE §3.7, invariant I5: for any tick the model gives ONE
	/// answer to where a carrier is, and every zone renders that same answer — which is why the
	/// body never has to literally traverse anything, and why following one across an edge cannot
	/// pop.
	/// </summary>
	public class KingdomItineraryRulesTests
	{
		[Test]
		public void ItineraryPhaseKeepsExactByteWireValues()
		{
			Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(KingdomItineraryPhase)));
			Assert.AreEqual(0, (byte)KingdomItineraryPhase.Pending);
			Assert.AreEqual(1, (byte)KingdomItineraryPhase.EnRoute);
			Assert.AreEqual(2, (byte)KingdomItineraryPhase.Handoff);
			Assert.AreEqual(3, (byte)KingdomItineraryPhase.Delivered);
		}

		private static KingdomLeg[] Contiguous()
		{
			return new KingdomLeg[3]
			{
				new KingdomLeg("taf:zone:a", 0, 0, 10, 0, 10, 100L, 110L),
				new KingdomLeg("taf:zone:b", 79, 0, 40, 0, 40, 110L, 150L),
				new KingdomLeg("taf:zone:c", 0, 0, 5, 5, 5, 150L, 155L)
			};
		}

		private static KingdomLeg[] WithAWait()
		{
			return new KingdomLeg[3]
			{
				new KingdomLeg("taf:zone:a", 0, 0, 10, 0, 10, 100L, 110L),
				new KingdomLeg("taf:zone:b", 79, 0, 40, 0, 40, 110L, 150L),
				new KingdomLeg("taf:zone:c", 0, 0, 5, 5, 5, 160L, 165L)
			};
		}

		private static KingdomItineraryFix At(KingdomLeg[] legs, long tick)
		{
			KingdomItineraryFix fix;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomItineraryRules.TryAt(legs, legs.Length, tick, out fix, out fault), fault.ToString());
			return fix;
		}

		[Test]
		public void BeforeTheFirstDepartureTheCarrierIsPendingAtTheStart()
		{
			KingdomItineraryFix fix = At(Contiguous(), 50L);
			Assert.AreEqual(KingdomItineraryPhase.Pending, fix.Phase);
			Assert.AreEqual("taf:zone:a", fix.ZoneId);
			Assert.AreEqual(0, fix.X);
			Assert.AreEqual(0, fix.StepsTaken);
			Assert.AreEqual(-1, fix.LegIndex);
		}

		[Test]
		public void MidLegThePositionInterpolatesAlongTheLeg()
		{
			KingdomItineraryFix fix = At(Contiguous(), 105L);
			Assert.AreEqual(KingdomItineraryPhase.EnRoute, fix.Phase);
			Assert.AreEqual(0, fix.LegIndex);
			Assert.AreEqual(5, fix.X);
			Assert.AreEqual(5, fix.StepsTaken, "steps are floor(progress x PathLength)");
		}

		/// <summary>
		/// The edge handoff, and the timing that stops it popping (§3.7 step 3): cross at the
		/// moment the next leg begins and the carrier is at the ENTRY edge, a cell or two along —
		/// not at the far wall and not standing on the boundary.
		/// </summary>
		[Test]
		public void AtTheHandoffTickTheCarrierIsJustInsideTheNextZonesEntryEdge()
		{
			KingdomItineraryFix fix = At(Contiguous(), 110L);
			Assert.AreEqual(KingdomItineraryPhase.EnRoute, fix.Phase);
			Assert.AreEqual(1, fix.LegIndex);
			Assert.AreEqual("taf:zone:b", fix.ZoneId);
			Assert.AreEqual(79, fix.X, "the carrier popped away from the entry edge");
			Assert.AreEqual(0, fix.StepsTaken);
		}

		/// <summary>Dawdle and they are further on. Both are correct renderings of the same one
		/// answer, which is the whole point of I5.</summary>
		[Test]
		public void CrossSlowerAndThePorterIsFurtherAlong()
		{
			KingdomItineraryFix early = At(Contiguous(), 110L);
			KingdomItineraryFix late = At(Contiguous(), 130L);
			Assert.AreEqual(1, late.LegIndex);
			Assert.AreEqual(20, late.StepsTaken);
			Assert.Greater(early.X, late.X, "the leg runs from 79 toward 40, so later is a lower x");
			Assert.AreEqual(60, late.X);
		}

		[Test]
		public void BetweenTwoLegsTheCarrierWaitsAtTheExitCellItReached()
		{
			KingdomItineraryFix fix = At(WithAWait(), 155L);
			Assert.AreEqual(KingdomItineraryPhase.Handoff, fix.Phase);
			Assert.AreEqual(1, fix.LegIndex);
			Assert.AreEqual("taf:zone:b", fix.ZoneId);
			Assert.AreEqual(40, fix.X);
		}

		[Test]
		public void PastTheLastArrivalTheCarrierIsDelivered()
		{
			KingdomItineraryFix fix = At(Contiguous(), 155L);
			Assert.AreEqual(KingdomItineraryPhase.Delivered, fix.Phase);
			Assert.AreEqual("taf:zone:c", fix.ZoneId);
			Assert.AreEqual(5, fix.X);
			Assert.AreEqual(5, fix.Y);
			KingdomItineraryFix later = At(Contiguous(), 5000L);
			Assert.AreEqual(KingdomItineraryPhase.Delivered, later.Phase);
			Assert.AreEqual(fix.X, later.X, "a delivered carrier kept moving");
		}

		/// <summary>One answer, and asking twice gives it twice. Consistent re-rendering IS
		/// following.</summary>
		[Test]
		public void TheSameTickAlwaysGivesTheSameAnswer()
		{
			KingdomItineraryFix first = At(Contiguous(), 137L);
			KingdomItineraryFix second = At(Contiguous(), 137L);
			Assert.AreEqual(first.Phase, second.Phase);
			Assert.AreEqual(first.LegIndex, second.LegIndex);
			Assert.AreEqual(first.X, second.X);
			Assert.AreEqual(first.StepsTaken, second.StepsTaken);
		}

		// ---- Re-projection ---------------------------------------------------------------

		/// <summary>
		/// The re-projection rule: only the unstarted remainder may move. A porter body-blocked for
		/// ten turns arrives ten turns later and everything downstream shifts by ten — no
		/// rubber-banding, no catch-up sprint, no time travel.
		/// </summary>
		[Test]
		public void OnlyTheUnstartedRemainderMoves()
		{
			KingdomLeg[] legs = Contiguous();
			KingdomLeg[] shifted;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomItineraryRules.TryReproject(legs, 3, 1, 10L, out shifted, out fault), fault.ToString());
			Assert.AreEqual(100L, shifted[0].DepartTick, "a completed leg moved");
			Assert.AreEqual(110L, shifted[0].ArriveTick, "a completed leg moved");
			Assert.AreEqual(110L, shifted[1].DepartTick, "the leg already begun lost its departure");
			Assert.AreEqual(160L, shifted[1].ArriveTick);
			Assert.AreEqual(160L, shifted[2].DepartTick);
			Assert.AreEqual(165L, shifted[2].ArriveTick);
		}

		[Test]
		public void ReprojectionIsCopyOnWriteAndNeverTouchesTheInput()
		{
			KingdomLeg[] legs = Contiguous();
			KingdomLeg[] shifted;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomItineraryRules.TryReproject(legs, 3, 0, 25L, out shifted, out fault));
			Assert.AreEqual(150L, legs[2].DepartTick, "the input itinerary was mutated");
			Assert.AreEqual(175L, shifted[2].DepartTick);
			Assert.AreNotSame(legs, shifted);
		}

		[Test]
		public void MasterPauseMovesEveryLegWithoutAdvancingTheCarrier()
		{
			KingdomLeg[] legs = Contiguous();
			Assert.IsTrue(KingdomItineraryRules.TryShiftAll(legs, legs.Length, 40L,
				out KingdomLeg[] shifted, out KingdomCityFault fault), fault.ToString());
			Assert.AreEqual(100L, legs[0].DepartTick, "input mutated");
			Assert.AreEqual(140L, shifted[0].DepartTick);
			Assert.AreEqual(195L, shifted[2].ArriveTick);
			KingdomItineraryFix before = At(legs, 105L);
			KingdomItineraryFix resumed = At(shifted, 145L);
			Assert.AreEqual(before.ZoneId, resumed.ZoneId);
			Assert.AreEqual(before.X, resumed.X);
			Assert.AreEqual(before.StepsTaken, resumed.StepsTaken);
			Assert.IsFalse(KingdomItineraryRules.TryShiftAll(legs, legs.Length, -1L,
				out shifted, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidTick, fault);
		}

		/// <summary>A carrier that made up time still cannot arrive before it left, and an
		/// impossible shift is refused whole rather than half-applied.</summary>
		[Test]
		public void AShiftThatWouldInvertTheJourneyIsRefusedAndPublishesNothing()
		{
			KingdomLeg[] legs = Contiguous();
			KingdomLeg[] shifted;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomItineraryRules.TryReproject(legs, 3, 1, -100L, out shifted, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidLegOrder, fault);
			Assert.IsNull(shifted);
		}

		[TestCase(-1)]
		[TestCase(3)]
		public void ReprojectingALegThatIsNotThereIsRefused(int leg)
		{
			KingdomLeg[] shifted;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomItineraryRules.TryReproject(Contiguous(), 3, leg, 5L, out shifted, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidIndex, fault);
		}

		// ---- Validation ------------------------------------------------------------------

		[Test]
		public void SixLegsIsTheCapAndASeventhIsRefused()
		{
			Assert.AreEqual(6, KingdomItineraryRules.MaxLegs);
			KingdomLeg[] legs = new KingdomLeg[7];
			for (int i = 0; i < 7; i++)
			{
				legs[i] = new KingdomLeg("taf:zone:" + i, 0, 0, 1, 0, 1, 100L * i, 100L * i + 50L);
			}
			KingdomCityFault fault;
			Assert.IsTrue(KingdomItineraryRules.TryValidate(legs, 6, out fault));
			Assert.IsFalse(KingdomItineraryRules.TryValidate(legs, 7, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidIndex, fault);
		}

		[Test]
		public void ALegThatArrivesBeforeItLeavesIsRefused()
		{
			KingdomLeg[] legs = new KingdomLeg[1] { new KingdomLeg("taf:zone:a", 0, 0, 1, 0, 1, 200L, 100L) };
			KingdomCityFault fault;
			Assert.IsFalse(KingdomItineraryRules.TryValidate(legs, 1, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidLegOrder, fault);
		}

		[Test]
		public void LegsThatOverlapInTimeAreRefused()
		{
			KingdomLeg[] legs = new KingdomLeg[2]
			{
				new KingdomLeg("taf:zone:a", 0, 0, 1, 0, 1, 100L, 200L),
				new KingdomLeg("taf:zone:b", 0, 0, 1, 0, 1, 150L, 250L)
			};
			KingdomCityFault fault;
			Assert.IsFalse(KingdomItineraryRules.TryValidate(legs, 2, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidLegOrder, fault);
		}

		[Test]
		public void AnEmptyItineraryHasNoPositionAtAll()
		{
			KingdomItineraryFix fix;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomItineraryRules.TryAt(new KingdomLeg[0], 0, 100L, out fix, out fault));
			Assert.AreEqual(KingdomCityFault.OutsideItinerary, fault);
			Assert.IsFalse(KingdomItineraryRules.TryAt(null, 0, 100L, out fix, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
		}

		// ---- Estimation: the endpoints are truth, the length is a prior --------------------

		[TestCase(0, 0, 3, 7, 7)]
		[TestCase(0, 0, 9, 2, 9)]
		[TestCase(5, 5, 5, 5, 0)]
		[TestCase(10, 10, 0, 0, 10)]
		public void DistanceIsChebyshevBecauseThatIsWhatAWalkerPays(int fromX, int fromY, int toX, int toY, int expected)
		{
			int cells;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomItineraryRules.TryChebyshev(fromX, fromY, toX, toY, out cells, out fault));
			Assert.AreEqual(expected, cells);
		}

		/// <summary>Open ground ≈ 1.25, built-up ≈ 1.6, both named rules constants.
		/// LIVING-CITY-ARCHITECTURE §3.7.</summary>
		[TestCase(10, KingdomItineraryRules.SinuosityOpenPercent, KingdomItineraryRules.NoRoadDiscountPercent, 12)]
		[TestCase(40, KingdomItineraryRules.SinuosityBuiltPercent, KingdomItineraryRules.NoRoadDiscountPercent, 64)]
		[TestCase(40, KingdomItineraryRules.SinuosityBuiltPercent, KingdomItineraryRules.RoadDiscountPercent, 38)]
		[TestCase(0, KingdomItineraryRules.SinuosityOpenPercent, KingdomItineraryRules.NoRoadDiscountPercent, 0)]
		public void LengthAtCreationIsEstimatedFromTheEndpointsAndNeverPathfound(int chebyshev, int sinuosity, int road, int expected)
		{
			int cells;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomItineraryRules.TryEstimatePathLength(chebyshev, sinuosity, road, out cells, out fault));
			Assert.AreEqual(expected, cells);
		}

		/// <summary>The consequence the player actually sees: laying a road visibly shortens every
		/// itinerary that uses it. LIVING-CITY-ARCHITECTURE §3.10(3).</summary>
		[Test]
		public void ARoadShortensEveryLegItTouches()
		{
			KingdomCityFault fault;
			for (int cheb = 5; cheb <= 80; cheb += 5)
			{
				int unpaved;
				int paved;
				Assert.IsTrue(KingdomItineraryRules.TryEstimatePathLength(cheb, KingdomItineraryRules.SinuosityBuiltPercent, KingdomItineraryRules.NoRoadDiscountPercent, out unpaved, out fault));
				Assert.IsTrue(KingdomItineraryRules.TryEstimatePathLength(cheb, KingdomItineraryRules.SinuosityBuiltPercent, KingdomItineraryRules.RoadDiscountPercent, out paved, out fault));
				Assert.Less(paved, unpaved, "a paved leg at " + cheb + " cells did not shorten");
			}
		}

		[TestCase(-1, 125, 100)]
		[TestCase(10, 0, 100)]
		[TestCase(10, 125, 0)]
		[TestCase(10, 125, 101)]
		public void AnImpossibleEstimateIsRefusedRatherThanClamped(int chebyshev, int sinuosity, int road)
		{
			int cells;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomItineraryRules.TryEstimatePathLength(chebyshev, sinuosity, road, out cells, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidRate, fault);
		}

		// ---- Overrun ---------------------------------------------------------------------

		/// <summary>Block them indefinitely and the job fails, is named, and the cargo is where it
		/// fell — so a founder standing in a doorway produces a story, not an unbounded job set.</summary>
		[TestCase(155L, false)]
		[TestCase(210L, false)]
		[TestCase(211L, true)]
		[TestCase(100000L, true)]
		public void AJobPastTwiceItsProjectedDurationFails(long now, bool expected)
		{
			bool overrun;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomItineraryRules.TryHasOverrun(Contiguous(), 3, now, out overrun, out fault));
			Assert.AreEqual(expected, overrun);
			Assert.AreEqual(2, KingdomItineraryRules.FailAtProjectedDurationMultiple);
		}

		[Test]
		public void OverrunRefusesATickBeforeTheJourneyBegan()
		{
			bool overrun;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomItineraryRules.TryHasOverrun(Contiguous(), 3, 99L, out overrun, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidTick, fault);
		}

		/// <summary>At Speed 100 an actor covers exactly one cell per tick, so PathLength cells is
		/// PathLength ticks and a founder walking beside a porter neither outpaces them nor falls
		/// behind. LIVING-CITY-ARCHITECTURE §3.7.</summary>
		[Test]
		public void ALegAtWalkingSpeedTakesOneTickPerCell()
		{
			Assert.AreEqual(1, KingdomItineraryRules.WalkTicksPerCellDefault);
			KingdomLeg[] legs = Contiguous();
			for (int i = 0; i < 2; i++)
			{
				Assert.AreEqual((long)legs[i].PathLength * KingdomItineraryRules.WalkTicksPerCellDefault, legs[i].ArriveTick - legs[i].DepartTick,
					"leg " + i + " is not dated at its own walking speed");
			}
		}
	}
}
#endif
