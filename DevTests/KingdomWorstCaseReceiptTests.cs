#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// <b>The measured worst case, printed and pinned.</b>
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;6.5, Pass 32 step 90, corrected at the pre-release boundary:
	/// found a City, hold 4 zones, every plot those zones admit, and 60 settlers; leave for a
	/// season; come home. W6 is the wave that finally gives the
	/// model a producing rate, which is the wave in which that scenario stops being free — a rate is
	/// the first thing that can make a reckoning cost more the longer you were away. So this is the
	/// receipt: the whole scenario, at W6's rates, with a full backlog standing, with every figure
	/// counted and judged against its own &sect;0.0 lane rather than against an author's assurance.
	/// </para>
	/// <para>
	/// <b>Counts, not milliseconds.</b> &sect;6.5: <i>"a timing is hardware and a count is a
	/// contract."</i> A test that asserted a wall clock would fail on a slow machine and pass on a
	/// fast one; every figure here is an integer the model actually produced.
	/// </para>
	/// </summary>
	public class KingdomWorstCaseReceiptTests
	{
		/// <summary>Pass 32 step 90's city, exactly.</summary>
		private const int Zones = 4;

		private static readonly int Works = KingdomCityState.MaxWorks;

		private const int Residents = 60;

		private const int SeasonDays = 90;

		/// <summary>Four claimed grounds in a line, so every zone id parses and the graph is a real
		/// four-node graph rather than four islands.</summary>
		private static string ZoneId(int index)
		{
			return "JoppaWorld.10.10.1." + index + ".10";
		}

		private static KingdomCityState WorstCase()
		{
			KingdomZoneRow[] zones = new KingdomZoneRow[Zones];
			for (int i = 0; i < Zones; i++)
			{
				// A City-stage quarter: cisterns and granaries near their ceilings, real works
				// making real drams and servings every day, and a standing debt against the
				// containers in both directions at once.
				zones[i] = new KingdomZoneRow(
					ZoneId(i),
					0,
					1L,
					new KingdomStocks(
						new KingdomStockPair(120L, 800L),
						new KingdomStockPair(60L, 600L),
						new KingdomStockPair(0L, 0L)),
					12,
					9,
					17 + i,
					11 + i,
					(i == 0) ? 0 : 40,
					(i == 0) ? -40 : 0,
					0);
			}
			KingdomWorkRow[] works = new KingdomWorkRow[Works];
			for (int i = 0; i < Works; i++)
			{
				works[i] = new KingdomWorkRow(
					i + 1,
					ZoneId(i % Zones),
					(short)(i % 70),
					(short)(i % 20),
					"cistern",
					100 - (i % 40),
					1,
					0L,
					new KingdomWorkRunState(KingdomWorkKind.Producer, 0, 0, 0L));
			}
			KingdomResidentRow[] residents = new KingdomResidentRow[Residents];
			for (int i = 0; i < Residents; i++)
			{
				residents[i] = new KingdomResidentRow(
					i + 1,
					"settler " + (i + 1),
					0,
					0,
					0L,
					(i % Works) + 1,
					((i + 3) % Works) + 1,
					0,
					KingdomDayShape.Field,
					KingdomResidentStanding.Resident,
					KingdomStandingCause.None,
					ZoneId(i % Zones),
					KingdomBrinkWindow.None,
					KingdomBrinkWindow.None,
					null,
					0);
			}
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityState.TryCreate(
				KingdomCityRules.SchemaVersion,
				KingdomCityRules.RulesVersion,
				"taf:city:kavvat",
				0L,
				default(KingdomStocks),
				zones,
				works,
				residents,
				null,
				out state,
				out fault), fault.ToString());
			return state;
		}

		private static KingdomAdvanceOutcome<KingdomCityState> Season(KingdomCityState state, long days)
		{
			KingdomAdvanceOutcome<KingdomCityState> outcome;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomAdvanceRules.TryRun(
				new KingdomCityAdvanceable(KingdomRules.TicksPerDay, null, null),
				state,
				state.ProcessedThroughTick,
				state.ProcessedThroughTick + days * KingdomRules.TicksPerDay,
				out outcome,
				out fault), fault.ToString());
			return outcome;
		}

		/// <summary>
		/// The receipt itself. Every figure is measured, printed in &sect;6.5's own greppable shape,
		/// and judged against its lane. A number that crosses a budget fails this test with the
		/// budget named, which is what "pinned" means.
		/// </summary>
		[Test]
		public void TheWorstCaseSeasonAwayStaysInsideEveryBudgetItIsPricedAgainst()
		{
			KingdomCityState state = WorstCase();
			int rows = state.RowCount;
			KingdomAdvanceOutcome<KingdomCityState> season = Season(state, SeasonDays);

			long ceiling;
			Assert.IsTrue(KingdomBudgetRules.TryMaxRowVisits(rows, out ceiling));

			// The catch-up backlog the homecoming inherits, in the reify lane's own thirds.
			KingdomCatchUpCounter counter = KingdomCityRules.CityCounter(season.State);
			int turns;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCatchUpRules.TryTurnsToDrain(counter.OwedThirds, out turns, out fault), fault.ToString());
			// And the architectural worst §0.0(b) prices: the whole backlog a full parasang can
			// present at once, not merely the one this city happens to be carrying.
			int worstTurns;
			Assert.IsTrue(KingdomCatchUpRules.TryTurnsToDrain(
				KingdomCatchUpRules.WorstBacklogUnits * KingdomCatchUpRules.ThirdsPerUnit, out worstTurns, out fault), fault.ToString());

			// One slice's planning, at the hard caps, on the same city.
			int stops = KingdomLogisticsRules.MaxStopsPerTrip;
			int nodes = stops + 1;
			int[] between = new int[nodes * nodes];
			for (int i = 0; i < nodes; i++)
			{
				for (int j = 0; j < nodes; j++)
				{
					between[(i * nodes) + j] = (i == j) ? 0 : (((i * 7) + (j * 13)) % 97) + 1;
				}
			}
			KingdomTripPlan plan;
			Assert.IsTrue(KingdomLogisticsRules.TryPlanTrip(between, stops, out plan, out fault), fault.ToString());

			KingdomZoneGraph graph;
			Assert.IsTrue(KingdomCityRules.TryZoneGraph(season.State, out graph, out fault), fault.ToString());

			long modelBytes;
			Assert.IsTrue(KingdomCityMemoryRules.TryCityModelBytes(Zones, Works, Residents, 0, out modelBytes));
			long realmBytes;
			Assert.IsTrue(KingdomCityMemoryRules.TryRealmBytesAtTodaysCaps(out realmBytes));

			Console.WriteLine(
				"[TAF] perf WORSTCASE city=Kavvat zones=" + Zones + " works=" + Works + " settlers=" + Residents
				+ " days=" + SeasonDays + " R=" + rows
				+ " steps=" + season.Steps + " rows=" + season.RowVisits + "/" + ceiling
				+ " overflow=" + season.Overflowed
				+ " owed=" + counter.OwedThirds + "/3 drain=" + turns + "turns worstdrain=" + worstTurns + "turns"
				+ " planops=" + plan.Operations + " graphops=" + graph.Operations
				+ " model=" + modelBytes + "B realm=" + realmBytes + "B");

			// ---- The pins ------------------------------------------------------------------
			Assert.IsFalse(season.Overflowed,
				"a season away must not exhaust the breakpoint budget: the model, not the elapsed, bounds the passes");
			Assert.LessOrEqual(season.RowVisits, ceiling,
				"row-visits are 64 x 2R at the very worst (§0.0(a)); measured " + season.RowVisits + " against " + ceiling);
			Assert.LessOrEqual(season.Steps, KingdomBudgetRules.MaxBreakpoints);
			Assert.AreEqual(KingdomBudgetVerdict.Within, KingdomBudgetRules.JudgeCount(KingdomBudgetLane.Heartbeat, season.Steps > KingdomBudgetRules.HeartbeatStepsPerSlice ? KingdomBudgetRules.HeartbeatStepsPerSlice : season.Steps),
				"the slice lane's step cap is a constant and the season does not move it");
			Assert.AreEqual(KingdomBudgetVerdict.Within, KingdomBudgetRules.JudgeCount(KingdomBudgetLane.RoutePlan, plan.Operations),
				"the planner cost " + plan.Operations + " int ops at its own hard caps");
			Assert.LessOrEqual(graph.Operations, 729L,
				"§3.10(2) prices the zone graph at 9³ = 729 ops and a four-zone city must be far under it");
			Assert.AreEqual(KingdomBudgetVerdict.Within, KingdomBudgetRules.JudgeCount(KingdomBudgetLane.CatchUpDrain, turns),
				"this city's backlog drains in " + turns + " turns at 8 units a turn");
			Assert.AreEqual(KingdomBudgetVerdict.Within, KingdomBudgetRules.JudgeCount(KingdomBudgetLane.CatchUpDrain, worstTurns),
				"the architectural worst backlog drains in " + worstTurns + " turns, against a warn rung of 40");
			Assert.AreEqual(KingdomBudgetVerdict.Within, KingdomBudgetRules.JudgeCount(KingdomBudgetLane.ModelBytes, realmBytes),
				"the realm's model is " + realmBytes + " bytes at today's caps");
		}

		/// <summary>
		/// <b>Step 90a, as an assertion instead of an instruction.</b> The same city left for one
		/// day and for a season: the passes and row-visits differ only by the breakpoints the model
		/// actually has to cross, and never in proportion to the absence. Ninety days and nine
		/// hundred cost the same, which is the statement that the elapsed appears in no term.
		/// </summary>
		[Test]
		public void ASeasonAndAYearAwayCostTheSameReckoningOnTheWorstCaseCity()
		{
			KingdomCityState state = WorstCase();
			KingdomAdvanceOutcome<KingdomCityState> season = Season(state, SeasonDays);
			KingdomAdvanceOutcome<KingdomCityState> year = Season(state, 4L * SeasonDays);
			KingdomAdvanceOutcome<KingdomCityState> decade = Season(state, 40L * SeasonDays);
			Assert.AreEqual(season.Steps, year.Steps, "the year crossed no breakpoint the season did not");
			Assert.AreEqual(year.Steps, decade.Steps);
			Assert.AreEqual(season.RowVisits, decade.RowVisits,
				"row-visits that scale with the absence mean a lane is drawing per day");
		}

		/// <summary>
		/// A homecoming that lands mid-day, on the worst-case city: the breakpoint proposals are
		/// computed from an unaligned cursor while the days are counted on world boundaries, and the
		/// two must not fight. If they did, the step count would drift upward pass after pass; it
		/// does not move at all.
		/// </summary>
		[Test]
		public void AReckoningThatStartsAndEndsMidDayCostsNoMorePassesThanAnAlignedOne()
		{
			KingdomCityState aligned = WorstCase();
			KingdomAdvanceOutcome<KingdomCityState> flat = Season(aligned, SeasonDays);

			KingdomCityState state = WorstCase();
			KingdomCityFault fault;
			KingdomCityState offset;
			Assert.IsTrue(state.TryWithProcessedThroughTick(737L, out offset, out fault), fault.ToString());
			KingdomAdvanceOutcome<KingdomCityState> ragged;
			Assert.IsTrue(KingdomAdvanceRules.TryRun(
				new KingdomCityAdvanceable(KingdomRules.TicksPerDay, null, null),
				offset,
				737L,
				737L + (SeasonDays * KingdomRules.TicksPerDay) + 419L,
				out ragged,
				out fault), fault.ToString());
			Assert.IsFalse(ragged.Overflowed);
			Assert.AreEqual(flat.Steps, ragged.Steps,
				"an unaligned cursor must not buy the model extra passes");
		}

		/// <summary>
		/// <b>I1 on the worst case</b>: after a season of four quarters producing at four different
		/// rates, with a landing debt in one quarter and a draw in another, the ground total is
		/// exactly what it was. Nothing was invented into the world; a claim was written down.
		/// </summary>
		[Test]
		public void TheWorstCaseSeasonLeavesTheGroundTotalUntouched()
		{
			KingdomCityState state = WorstCase();
			long water = Ground(state, KingdomStockKind.Water);
			long food = Ground(state, KingdomStockKind.Food);
			KingdomCityState after = Season(state, SeasonDays).State;
			Assert.AreEqual(water, Ground(after, KingdomStockKind.Water),
				"model total == ground total + counter-owed, per stock kind, at every instant");
			Assert.AreEqual(food, Ground(after, KingdomStockKind.Food));
		}

		private static long Ground(KingdomCityState state, KingdomStockKind kind)
		{
			long total = 0L;
			for (int i = 0; i < state.ZoneCount; i++)
			{
				KingdomZoneRow row;
				Assert.IsTrue(state.TryZone(i, out row));
				KingdomStockPair pair;
				Assert.IsTrue(row.Stocks.TryGet(kind, out pair));
				total += pair.Level - row.OwedOf(kind);
			}
			return total;
		}
	}
}
#endif
