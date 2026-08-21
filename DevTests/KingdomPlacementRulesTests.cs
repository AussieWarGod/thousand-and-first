#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Placement by the hour. LIVING-CITY-ARCHITECTURE §3.2(b): the model decides where a person
	/// belongs at this hour, the anchor is set at activation, and vanilla's own idle hook does the
	/// walking. These are the first clause — pure, total, and holding no times of its own.
	/// </summary>
	internal class KingdomPlacementRulesTests
	{
		/// <summary>
		/// The bands are unions of Calendar.GetTime's own eight stretches, cut where the calendar
		/// already cuts (D/XRL/World/Calendar.cs:296-352). Each boundary is tested on both sides,
		/// because a band whose edge is off by one is a market that shuts an hour early forever.
		/// </summary>
		[TestCase(0L, KingdomDayBand.BeetleMoon)]
		[TestCase(150L, KingdomDayBand.BeetleMoon)]
		[TestCase(151L, KingdomDayBand.Rising)]
		[TestCase(450L, KingdomDayBand.Rising)]
		[TestCase(451L, KingdomDayBand.SaltSun)]
		[TestCase(750L, KingdomDayBand.SaltSun)]
		[TestCase(751L, KingdomDayBand.Hindsun)]
		[TestCase(900L, KingdomDayBand.Hindsun)]
		[TestCase(901L, KingdomDayBand.JeweledDusk)]
		[TestCase(1050L, KingdomDayBand.JeweledDusk)]
		[TestCase(1051L, KingdomDayBand.BeetleMoon)]
		[TestCase(1199L, KingdomDayBand.BeetleMoon)]
		public void BandsCutWhereTheGamesOwnCalendarCuts(long tickOfDay, KingdomDayBand expected)
		{
			Assert.AreEqual(expected, KingdomPlacementRules.BandFor(tickOfDay));
		}

		/// <summary>A day is 1200 ticks and the band repeats with it. A clock that read the
		/// hundredth day differently from the first would be a clock nobody could plan around.</summary>
		[Test]
		public void TheBandRepeatsEveryDay()
		{
			for (int within = 0; within < KingdomPlacementRules.TicksPerDay; within += 37)
			{
				KingdomDayBand first = KingdomPlacementRules.BandFor(within);
				Assert.AreEqual(first, KingdomPlacementRules.BandFor(within + 90L * KingdomPlacementRules.TicksPerDay));
				Assert.AreEqual(first, KingdomPlacementRules.BandFor(within + 438000L));
			}
		}

		/// <summary>Total over every representable input. A tick the clock cannot happen to produce
		/// is still a tick this function must answer for, because a placement rule that throws is a
		/// settlement that stops moving.</summary>
		[TestCase(-1L)]
		[TestCase(-1200L)]
		[TestCase(long.MinValue + 1L)]
		[TestCase(long.MaxValue)]
		public void TheBandIsTotalOverEveryTick(long tick)
		{
			int within = KingdomPlacementRules.TickOfDay(tick);
			Assert.IsTrue(within >= 0 && within < KingdomPlacementRules.TicksPerDay);
			KingdomDayBand band = KingdomPlacementRules.BandFor(tick);
			Assert.IsTrue(band >= KingdomDayBand.BeetleMoon && band <= KingdomDayBand.JeweledDusk);
		}

		/// <summary>
		/// §3.2(b)'s table, row by row: rising and the salt sun put everyone at post; Hindsun keeps
		/// the market and the shrine and sends the rest home; the dusk and the night are hearths.
		/// </summary>
		[TestCase(KingdomDayShape.Field, KingdomDayBand.Rising, KingdomPost.Station)]
		[TestCase(KingdomDayShape.Field, KingdomDayBand.SaltSun, KingdomPost.Station)]
		[TestCase(KingdomDayShape.Field, KingdomDayBand.Hindsun, KingdomPost.Hearth)]
		[TestCase(KingdomDayShape.Field, KingdomDayBand.JeweledDusk, KingdomPost.Hearth)]
		[TestCase(KingdomDayShape.Field, KingdomDayBand.BeetleMoon, KingdomPost.Hearth)]
		[TestCase(KingdomDayShape.Craft, KingdomDayBand.Hindsun, KingdomPost.Hearth)]
		[TestCase(KingdomDayShape.Yard, KingdomDayBand.Hindsun, KingdomPost.Hearth)]
		[TestCase(KingdomDayShape.Market, KingdomDayBand.Hindsun, KingdomPost.Station)]
		[TestCase(KingdomDayShape.Shrine, KingdomDayBand.Hindsun, KingdomPost.Station)]
		[TestCase(KingdomDayShape.Market, KingdomDayBand.JeweledDusk, KingdomPost.Hearth)]
		[TestCase(KingdomDayShape.Shrine, KingdomDayBand.BeetleMoon, KingdomPost.Hearth)]
		public void TheHourDecidesWhereADayShapeStands(KingdomDayShape shape, KingdomDayBand band, KingdomPost expected)
		{
			Assert.AreEqual(expected, KingdomPlacementRules.PostFor(shape, band));
		}

		/// <summary>The watch keeps its post in every band, which is what a watch is.</summary>
		[Test]
		public void TheWatchNeverGoesHome()
		{
			for (int band = (int)KingdomDayBand.BeetleMoon; band <= (int)KingdomDayBand.JeweledDusk; band++)
			{
				Assert.AreEqual(KingdomPost.Station, KingdomPlacementRules.PostFor(KingdomDayShape.Watch, (KingdomDayBand)band));
			}
		}

		/// <summary>
		/// A settler the works have no room for spends their day at home, in every band. That is
		/// KingdomResidentRules.DayShapeFor's own ruling and this must not soften it — an unposted
		/// settler dragged to a workplace at dawn would be a post invented out of nothing.
		/// </summary>
		[Test]
		public void TheHearthShapeIsHomeInEveryBand()
		{
			for (int band = (int)KingdomDayBand.BeetleMoon; band <= (int)KingdomDayBand.JeweledDusk; band++)
			{
				Assert.AreEqual(KingdomPost.Hearth, KingdomPlacementRules.PostFor(KingdomDayShape.Hearth, (KingdomDayBand)band));
			}
		}

		/// <summary>Every shape and every band has an answer, and the answer is one of the two the
		/// vocabulary admits. A placement rule with a hole is a settler standing in a doorway.</summary>
		[Test]
		public void EveryShapeAndBandHasAnAnswer()
		{
			for (int shape = 0; shape <= (int)KingdomDayShape.Shrine; shape++)
			{
				for (int band = (int)KingdomDayBand.BeetleMoon; band <= (int)KingdomDayBand.JeweledDusk; band++)
				{
					KingdomPost post = KingdomPlacementRules.PostFor((KingdomDayShape)shape, (KingdomDayBand)band);
					Assert.IsTrue(post == KingdomPost.Hearth || post == KingdomPost.Station);
				}
			}
		}

		/// <summary>The day is legible in bands, so a band change is what a re-anchoring keys
		/// on — never a tick, which would re-anchor sixty settlers fifty times an hour.</summary>
		[Test]
		public void ABandChangeIsTheOnlyThingWorthReanchoringOn()
		{
			Assert.IsFalse(KingdomPlacementRules.BandChanged(500L, 749L));
			Assert.IsTrue(KingdomPlacementRules.BandChanged(750L, 751L));
			Assert.IsTrue(KingdomPlacementRules.BandChanged(1050L, 1051L));
		}

		/// <summary>
		/// §3.2(b) constraint 2: returning false from IdleQueryEvent costs the actor its turn, so a
		/// station must be selective. Vanilla's Bed keeps the identical discipline with the
		/// identical figure (D/XRL/World/Parts/Bed.cs:209-212), and §3.6 is where the number comes
		/// from — one in-game hour.
		/// </summary>
		[Test]
		public void AStationMayNotSpendTurnsFasterThanOnceAnHour()
		{
			Assert.AreEqual(KingdomBudgetRules.HeartbeatCadenceTicks, KingdomPlacementRules.ClaimCooldownTicks);
			Assert.AreEqual(KingdomPlacementRules.TicksPerHour, KingdomPlacementRules.ClaimCooldownTicks);
			Assert.IsTrue(KingdomPlacementRules.MayClaim(0L, 10L), "a station that has never claimed is not on cooldown");
			Assert.IsFalse(KingdomPlacementRules.MayClaim(100L, 149L));
			Assert.IsTrue(KingdomPlacementRules.MayClaim(100L, 150L));
			Assert.IsTrue(KingdomPlacementRules.MayClaim(100L, 5000L));
		}

		/// <summary>
		/// §3.6 chose the cadence from the game's own unit rather than from taste, and the day
		/// arithmetic has to agree with the calendar it was read off: twenty-four hours to the day.
		/// </summary>
		[Test]
		public void TheCadenceIsTheGamesOwnHour()
		{
			Assert.AreEqual(50, KingdomPlacementRules.TicksPerHour);
			Assert.AreEqual(1200, KingdomPlacementRules.TicksPerDay);
			Assert.AreEqual(24, KingdomPlacementRules.TicksPerDay / KingdomPlacementRules.TicksPerHour);
			Assert.AreEqual((long)ThousandAndFirst.KingdomRules.TicksPerDay, (long)KingdomPlacementRules.TicksPerDay);
		}

		/// <summary>
		/// §0.0's own row: the heartbeat amortises to about five row-visits a turn per city, and it
		/// warns at ten. A cadence that made the amortised figure cross its own warn rung would be
		/// a design shipping inside its own warning, which W0 already refused once.
		/// </summary>
		[Test]
		public void TheAmortisedSliceStaysUnderItsOwnWarnRung()
		{
			// R for the City as the rules cap it today: 4 zones + 40 works + 60 residents + 12 clocks.
			const int Rows = 4 + 40 + 60 + 12;
			long perSlice = 2L * Rows;
			long perTurn = perSlice / KingdomBudgetRules.HeartbeatCadenceTicks;
			// §3.6 quotes "≈ 5 row-visits per turn per city"; the exact integer at today's caps is
			// 232/50 = 4, and the point of the figure is the rung it sits under rather than the
			// rounding it was written with.
			Assert.AreEqual(4L, perTurn);
			Assert.IsTrue(perTurn <= 5L);
			Assert.AreEqual(KingdomBudgetVerdict.Within, KingdomBudgetRules.JudgeCount(KingdomBudgetLane.HeartbeatAmortised, perTurn));
			Assert.AreEqual(KingdomBudgetVerdict.Over, KingdomBudgetRules.JudgeCount(KingdomBudgetLane.HeartbeatAmortised, 21L));
		}
	}
}
#endif
