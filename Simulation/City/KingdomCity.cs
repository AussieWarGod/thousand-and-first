using System;
using System.Collections.Generic;
using System.Diagnostics;

using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Where the city's receipts land. LIVING-CITY-ARCHITECTURE &sect;6.5: one greppable line per
	/// event, in the shape the log-watcher already reads, behind the dev-log option.
	/// <para>
	/// The ring underneath it is <c>KingdomComputeJournalRing</c>'s, so the session worsts are kept
	/// whether or not the option is on: a tester who turns the log on halfway through a session
	/// still has the worst reckon of the session to read.
	/// </para>
	/// </summary>
	public sealed class KingdomCityJournal : IKingdomComputeJournal
	{
		private readonly KingdomComputeJournalRing ring = new KingdomComputeJournalRing();

		void IKingdomComputeJournal.Record(KingdomPerfReceipt receipt)
		{
			((IKingdomComputeJournal)ring).Record(receipt);
			KingdomLog.Log(KingdomBudgetRules.FormatReceiptBody(receipt));
		}

		internal bool TryWorst(KingdomBudgetLane lane, out KingdomPerfReceipt receipt)
		{
			return ring.TryWorst(lane, out receipt);
		}
	}

	/// <summary>
	/// The city book at the engine's edge: check-in, check-out, and the reify that makes a
	/// deficit real.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;1.1: <i>the city is a book, and a zone is a page of it that
	/// happens to be open.</i> While a zone is attended the ground is authoritative and the model
	/// is a mirror; while it is suspended the model is authoritative. The handoff is here and
	/// nowhere else — every consumer that used to read a <c>r_TAF_Supports_*</c> or
	/// <c>r_TAF_Larders_*</c> game-state key now reads a zone row through this class.
	/// </para>
	/// <para>
	/// Engine-coupled by design, and paired with <c>KingdomCityRules</c> exactly as
	/// <c>KingdomSubsidence</c> is paired with <c>KingdomSubsidenceRules</c>: nothing here decides
	/// anything, it only reads the ground, asks the rules, and applies the answer.
	/// </para>
	/// </summary>
	public static class KingdomCity
	{
		/// <summary>
		/// Dedication order, stamped on a vessel or larder the first pass that counts it as the
		/// city's.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.9 needs dedication order to be a STORED FACT rather
		/// than a ranking recomputed from contents, and the several places a container becomes the
		/// city's — the Charter, a commission, a scaffold, an adoption — have no single moment to
		/// stamp from. The earliest moment the city can know about a container is the first pass
		/// that sees it dedicated, so that is when the ordinal is minted, and it never moves
		/// afterwards. The founder's newest dedication is the reserve that outlives everything
		/// else, which is what the ordering is for.
		/// </para>
		/// </summary>
		public const string DedicationOrderProperty = "KingdomDedicationOrder";

		private static readonly KingdomCityJournal Journal = new KingdomCityJournal();

		private static readonly KingdomExecutor Executor = new KingdomExecutor(new KingdomStopwatchClock(), Journal);

		/// <summary>
		/// The one computation seam the city has, shared with the heartbeat.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;2.5: the choke point exists so that no wave after W0 can
		/// grow a second computation path, and &sect;3.6 spends that promise immediately &mdash; the
		/// micro-reckon goes through the same executor as the homecoming reckon, so a slice and a
		/// pass are one advancement split rather than two implementations of a clock.
		/// </para>
		/// </summary>
		internal static KingdomExecutor Seam
		{
			get { return Executor; }
		}

		/// <summary>Records a receipt for work the executor did not run &mdash; the per-turn reify
		/// spend and the prefetch thaw, both of which touch the ground and therefore cannot cross
		/// the seam's engine-free boundary. Same journal, same log line, same session worsts
		/// (&sect;6.5).</summary>
		internal static void Record(KingdomPerfReceipt receipt)
		{
			((IKingdomComputeJournal)Journal).Record(receipt);
		}

		// ==================================================================================
		// Check-in — reconcile before rendering (§3.1)
		// ==================================================================================

		/// <summary>
		/// The pass's first word with the book: advance the model to now, pay this zone's standing
		/// debt onto its real containers, carry the city's stock to where the founder is standing,
		/// and then let the ground overwrite the row.
		/// <para>
		/// Runs after <c>survey</c> and before <c>trade</c>, so everything downstream reads a
		/// ground the book has already made true. A missed check-out costs freshness, never
		/// correctness (&sect;3.4), because this reconciles against the ground either way.
		/// </para>
		/// </summary>
		public static void CheckIn(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks)
		{
			if (System == null || !System.Founded || Z == null || Survey == null || System.City == null)
			{
				return;
			}
			StampDedicationOrder(System, Survey);
			KingdomCityState state;
			KingdomCityFault fault;
			if (!Ensure(System, Z, out state, out fault))
			{
				Refuse("check-in", fault);
				return;
			}
			state = Reckon(System, state, TimeTicks);
			int index;
			if (!IndexOf(state, Z.ZoneID, out index))
			{
				return;
			}
			KingdomReifySpend spend;
			state = Reify(System, Z, Survey, state, index, TimeTicks, true, out spend);
			state = Carry(System, Z, Survey, state, TimeTicks);
			state = Reconcile(System, Z, Survey, state, index, TimeTicks);
			state = ReadWorks(state, Z, Survey);
			// After the works, because a resident row's home is named by the work row that stands
			// over it, and a home read before the works were rebuilt would name last pass's id.
			// This is where the roster becomes rows and where the binding registry learns who is
			// standing in this ground (LIVING-CITY-ARCHITECTURE §8.3, §3.8): every settler here gets
			// a stable id and a row, and every row bound HERE whose body is not here reads back as
			// Abroad or Dead, with the cause.
			state = KingdomResidents.ReadRoster(System, Z, Survey, state, TimeTicks);
			Publish(System, state);
			// The hour's placement, and the carriers who are mid-journey through this ground. Both
			// are renderings of what the book already says (§3.2(b), §3.7): the station gives
			// vanilla's own idle hook something to claim a settler with, and Render puts every open
			// job's carrier at At(job, now) - the same answer every other zone would give.
			KingdomStations.Attend(System, Z, Survey);
			// Before anything is minted: a body carrying a job id the model already closed is the
			// one instant the goods could exist twice (§3.8 t3). ZoneThawedEvent is the hook the
			// architecture names, and it is not enough on its own — a suspended-but-resident zone is
			// entered with no thaw at all (§3.5), so the sweep runs on the entry path too. It is
			// idempotent: absence from the registry is what makes a body stale, and a swept zone
			// has none left.
			KingdomPorters.Sweep(System, Z);
			KingdomPorters.Render(System, Z, TimeTicks);
			Audit(System, Z, Survey, "check-in");
		}

		/// <summary>
		/// The pass's last word: what this zone actually holds once the day has been drawn, the
		/// harvest gathered and the works run.
		/// <para>
		/// &sect;3.4 names <c>SuspendingEvent</c> as the true last read and this as the cheaper one
		/// that usually beats it there. Both write the same row; whichever fires last is the
		/// reading the other zones will be measured against.
		/// </para>
		/// </summary>
		public static void CheckOut(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks)
		{
			// A stamp of zero would date the row as never read, and a row that reads as never read
			// contributes nothing anywhere. So a check-out with no clock to date it is skipped
			// rather than written: a missed check-out costs freshness, and a zeroed one would cost
			// the city a whole parasang.
			if (System == null || !System.Founded || Z == null || Survey == null || System.City == null || TimeTicks <= 0L)
			{
				return;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!Ensure(System, Z, out state, out fault))
			{
				Refuse("check-out", fault);
				return;
			}
			int index;
			if (!IndexOf(state, Z.ZoneID, out index))
			{
				return;
			}
			KingdomZoneRow row;
			state.TryZone(index, out row);
			KingdomCityState written;
			if (!state.TryWithZone(index, row.WithReading(TimeTicks, Ground(Survey, row), row.Roofs, Survey.Defence(), row.WaterCarry, row.FoodCarry), out written, out fault))
			{
				Refuse("check-out", fault);
				return;
			}
			Publish(System, written);
		}

		/// <summary>
		/// The true last read (&sect;3.4). Fires from <c>SuspendZone</c> before <c>Suspended</c> is
		/// set, for ANY zone as it suspends — so the filter is the whole of the handler: only a
		/// zone the seated realm claims is ours to read, and only while its objects are still in
		/// RAM.
		/// </summary>
		public static void OnSuspending(KingdomSystem System, Zone Z)
		{
			if (System == null || !System.Founded || Z == null || System.ClaimedZones == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			CheckOut(System, Z, KingdomSurvey.Take(Z, System), (The.Game != null) ? The.Game.TimeTicks : 0L);
		}

		// ==================================================================================
		// What the retired sightings used to answer
		// ==================================================================================

		/// <summary>
		/// Writes down what this zone's works carry, on the pass that stood in it. Rewritten from
		/// the ground every time, including down to zero: a reservoir that was struck stops
		/// counting toward the city the pass the founder sees the empty plot, and never before.
		/// <para>
		/// This is <c>KingdomSubsidence.RecordZone</c>'s discipline unchanged; what moved is where
		/// it is written. Five game-state ints became one row of the city book, and the arithmetic
		/// downstream reads the same numbers.
		/// </para>
		/// </summary>
		public static void RecordSupports(KingdomSystem System, Zone Z, int Water, int Food, int Roof, int StorageCapacity, long TimeTicks)
		{
			if (System == null || Z == null || System.City == null)
			{
				return;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!Ensure(System, Z, out state, out fault))
			{
				Refuse("record supports", fault);
				return;
			}
			int index;
			KingdomZoneRow row;
			if (!IndexOf(state, Z.ZoneID, out index) || !state.TryZone(index, out row))
			{
				return;
			}
			KingdomStocks stocks = new KingdomStocks(
				new KingdomStockPair(row.Stocks.Water.Level, Floor(StorageCapacity)),
				row.Stocks.Food,
				row.Stocks.Materials);
			KingdomCityState written;
			if (!state.TryWithZone(index, row.WithReading(TimeTicks, stocks, Floor(Roof), row.Defence, Floor(Water), Floor(Food)), out written, out fault))
			{
				Refuse("record supports", fault);
				return;
			}
			Publish(System, written);
		}

		/// <summary>
		/// Writes down what this zone's dedicated pantries hold and can hold, on the pass that
		/// stood in it. <c>KingdomCrops.RecordLarders</c>'s own contract, in the book.
		/// </summary>
		public static void RecordLarder(KingdomSystem System, Zone Z, int FoodStored, int FoodCapacity, long TimeTicks)
		{
			if (System == null || Z == null || System.City == null)
			{
				return;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!Ensure(System, Z, out state, out fault))
			{
				Refuse("record larder", fault);
				return;
			}
			int index;
			KingdomZoneRow row;
			if (!IndexOf(state, Z.ZoneID, out index) || !state.TryZone(index, out row))
			{
				return;
			}
			KingdomStocks stocks = new KingdomStocks(
				row.Stocks.Water,
				new KingdomStockPair(Floor(FoodStored), Floor(FoodCapacity)),
				row.Stocks.Materials);
			KingdomCityState written;
			if (!state.TryWithZone(index, row.WithReading(TimeTicks, stocks, row.Roofs, row.Defence, row.WaterCarry, row.FoodCarry), out written, out fault))
			{
				Refuse("record larder", fault);
				return;
			}
			Publish(System, written);
		}

		/// <summary>
		/// Every claimed zone of the seated city EXCEPT the one the pass is in, as each was last
		/// read. The exclusion is the whole point: this zone has just been counted from the ground,
		/// and counting it twice would double its cisterns.
		/// <para>
		/// The projection &sect;1.2(b) promised: the rows hand a <c>ZoneSighting</c> to the
		/// subsidence arithmetic, so that arithmetic does not change at all — it simply stops
		/// reading a dictionary of ints.
		/// </para>
		/// </summary>
		public static List<KingdomSubsidenceRules.ZoneSighting> OtherZones(KingdomSystem System, Zone Z)
		{
			List<KingdomSubsidenceRules.ZoneSighting> others = new List<KingdomSubsidenceRules.ZoneSighting>();
			KingdomCityBook book = (System == null) ? null : System.City;
			if (book == null)
			{
				return others;
			}
			// Normalized before it is indexed: these read the columns directly rather than
			// materialising the whole model for one projection, so the columns have to be square
			// first. Normalize is idempotent and O(rows).
			book.Normalize();
			string here = (Z == null) ? null : Z.ZoneID;
			for (int i = 0; i < book.ZoneCount; i++)
			{
				if (book.ZoneLastReadTicks[i] <= 0L || string.Equals(book.ZoneIds[i], here, StringComparison.Ordinal))
				{
					continue;
				}
				others.Add(new KingdomSubsidenceRules.ZoneSighting(
					book.ZoneWaterCarries[i],
					book.ZoneFoodCarries[i],
					book.ZoneRoofs[i],
					(int)Clamp(book.ZoneWaterCapacities[i]),
					DayStamp(book.ZoneLastReadTicks[i])));
			}
			return others;
		}

		/// <summary>
		/// Room the city's OTHER claimed zones were last read holding for a harvest.
		/// <c>KingdomCrops.LarderRoomElsewhere</c>'s own contract, in the book.
		/// </summary>
		public static int LarderRoomElsewhere(KingdomSystem System, Zone Z)
		{
			KingdomCityBook book = (System == null) ? null : System.City;
			if (book == null)
			{
				return 0;
			}
			book.Normalize();
			string here = (Z == null) ? null : Z.ZoneID;
			long room = 0L;
			for (int i = 0; i < book.ZoneCount; i++)
			{
				if (book.ZoneLastReadTicks[i] <= 0L || string.Equals(book.ZoneIds[i], here, StringComparison.Ordinal))
				{
					continue;
				}
				long space = book.ZoneFoodCapacities[i] - book.ZoneFoodLevels[i];
				if (space > 0L)
				{
					room += space;
				}
			}
			return (int)Clamp(room);
		}

		// ==================================================================================
		// The reckoning, the reify, and the audit
		// ==================================================================================

		/// <summary>
		/// One city, one pass, through the executor and nowhere else. The receipt lands in the
		/// journal whether the job publishes or not, which is what makes a refusal legible instead
		/// of silent.
		/// </summary>
		private static KingdomCityState Reckon(KingdomSystem System, KingdomCityState state, long TimeTicks)
		{
			KingdomReckonJob job = new KingdomReckonJob(
				System.SeatName ?? state.SettlementId,
				new KingdomCityAdvanceable(KingdomRules.TicksPerDay, null, null));
			KingdomComputeResult<KingdomCityState> result = Executor.Submit(new KingdomReckonInput(state, TimeTicks), job);
			return result.Published ? result.Value : state;
		}

		/// <summary>
		/// One turn's amortised spend against this zone's standing debt, for the pump.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.5: <b>entry must cost O(budget), never O(elapsed)</b>,
		/// and the way that is achieved is <c>ZoneRepair</c>'s counter with the spend taken per turn
		/// instead of per activation. <c>ZoneRepair</c> applies its whole backlog in one loop
		/// (<c>D/XRL/World/ZoneParts/ZoneRepair.cs:87-97</c>) because its unit is a
		/// <c>Cell.AddObject</c>; ours is body moves and container fills, so we keep the counter
		/// and spend it on a per-turn budget. <b>That single change is the whole of Addendum
		/// 12(b)'s <i>reification is AMORTISED</i>.</b>
		/// </para>
		/// <para>
		/// Returns whether the budget was saturated, which is the one thing the prefetch needs to
		/// know: a turn that spent its whole allowance is a turn not to thaw a neighbour on.
		/// </para>
		/// </summary>
		public static bool SpendTurn(KingdomSystem System, Zone Z, long TimeTicks)
		{
			if (System == null || !System.Founded || Z == null || System.City == null)
			{
				return false;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!System.City.TryRead(out state, out fault))
			{
				return false;
			}
			int index;
			KingdomZoneRow row;
			if (!IndexOf(state, Z.ZoneID, out index) || !state.TryZone(index, out row)
				|| KingdomCityRules.CounterFor(row).IsSettled)
			{
				// A caught-up zone costs literally nothing, which is ZoneRepair's own self-removal
				// property kept rather than reimplemented (:99-102). The survey below is the
				// expensive part of this method and a settled zone never reaches it.
				return false;
			}
			if (Allowance(System, TimeTicks) <= 0)
			{
				// The turn's whole allowance is already spent, by the pass or by another zone. The
				// debt stays owed and lands next turn, which is the amortisation working.
				return true;
			}
			if (TimeTicks < System.ReifyQuietUntilTick)
			{
				return false;
			}
			Stopwatch watch = Stopwatch.StartNew();
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			KingdomReifySpend spend;
			KingdomCityState written = Reify(System, Z, survey, state, index, TimeTicks, false, out spend);
			watch.Stop();
			Publish(System, written);
			Receipt(Z.ZoneID, spend, watch, OwedThirds(System));
			if (spend.Units == 0)
			{
				// Nothing moved: the ground cannot serve this debt yet. Buy an hour of quiet rather
				// than paying for the same survey every turn to be told the same thing.
				System.ReifyQuietUntilTick = TimeTicks + KingdomBudgetRules.HeartbeatCadenceTicks;
			}
			return Allowance(System, TimeTicks) <= 0;
		}

		/// <summary>
		/// Model to ground: as much of what this zone owes as one turn's budget buys, paid onto
		/// real containers in dedication order, visible cells first.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.9, invariant I4, with &sect;3.5's budget over the top of
		/// it. A unit leaves the debt at the instant it LANDS, never at the instant it is scheduled,
		/// so re-entering, reloading or re-activating cannot pay the same debt twice. What the
		/// containers could not cover stays on the row and is told &mdash; never silently forgiven,
		/// and never silently repaired.
		/// </para>
		/// <para>
		/// <b>Visible cells first</b> is what makes the guarantee perceptual rather than merely
		/// amortised: what the founder is looking at catches up first, and the rest fills in behind
		/// them as they walk. Visibility is the engine's own answer &mdash; <c>Cell.IsVisible()</c>
		/// is <c>ParentZone.GetVisibility(X, Y)</c> (<c>D/XRL/World/Cell.cs:3490-3496</c>), the
		/// player's real field of view.
		/// </para>
		/// </summary>
		private static KingdomCityState Reify(KingdomSystem System, Zone Z, KingdomSurvey Survey, KingdomCityState state, int index, long TimeTicks, bool announce, out KingdomReifySpend spend)
		{
			spend = default(KingdomReifySpend);
			KingdomZoneRow row;
			if (!state.TryZone(index, out row))
			{
				return state;
			}
			// A unit is only worth spending on a kind the containers can actually move. A cistern
			// with nothing in it cannot pay a draw and a full larder cannot take a landing, and
			// spending a unit a turn to discover that again is both a wasted budget and — because
			// the shortfall wants telling — a homecoming report full of the same line.
			bool waterOwed = row.OwedWater != 0 && CanMoveWater(Survey, row.OwedWater);
			bool foodOwed = row.OwedFood != 0 && CanMoveFood(Survey, row.OwedFood);
			if (announce)
			{
				// Said once, at the pass, in the founder's own register: what the containers could
				// not cover stays on the row and is never silently forgiven (§3.9).
				Tell(System, 0, 0,
					(row.OwedWater != 0 && !waterOwed) ? row.OwedWater : 0,
					(row.OwedFood != 0 && !foodOwed) ? row.OwedFood : 0);
			}
			bool waterSeen = waterOwed && Visible(WaterCell(Survey));
			bool foodSeen = foodOwed && Visible(FoodCell(Survey));
			Dictionary<int, GameObject> stations = KingdomStations.Index(Z);
			List<GameObject> posted = Posted(Z, Survey, stations);
			int visibleHeavyWanted = VisibleCount(posted);
			KingdomReifyDemand demand = new KingdomReifyDemand(
				visibleHeavyWanted,
				(waterSeen ? 1 : 0) + (foodSeen ? 1 : 0),
				0,
				posted.Count - visibleHeavyWanted,
				(waterOwed && !waterSeen ? 1 : 0) + (foodOwed && !foodSeen ? 1 : 0),
				0);
			if (demand.IsEmpty)
			{
				return state;
			}
			KingdomCityFault fault;
			if (!KingdomCatchUpRules.TryPlanTurn(demand, Allowance(System, TimeTicks), HeavyAllowance(System, TimeTicks), out spend, out fault))
			{
				Refuse("reify", fault);
				return state;
			}
			Charge(System, TimeTicks, spend);
			// The budget's own order, reconstructed rather than re-decided: TryPlanTurn takes the
			// visible half of a tier before the rest of it, so this split is a minimum against the
			// plan and never a second opinion about precedence.
			int heavyVisible = (spend.Heavy < demand.VisibleHeavy) ? spend.Heavy : demand.VisibleHeavy;
			int mediumVisible = (spend.Medium < demand.VisibleMedium) ? spend.Medium : demand.VisibleMedium;
			Anchor(Z, posted, stations, heavyVisible, spend.Heavy - heavyVisible, TimeTicks);
			int water = row.OwedWater;
			int food = row.OwedFood;
			int seen = mediumVisible;
			int rest = spend.Medium - mediumVisible;
			bool waterSpent = waterOwed && Spendable(waterSeen, ref seen, ref rest);
			if (waterSpent)
			{
				water = SettleWater(Survey, water);
			}
			bool foodSpent = foodOwed && Spendable(foodSeen, ref seen, ref rest);
			if (foodSpent)
			{
				food = SettleFood(System, Survey, food);
			}
			KingdomCityState written;
			if (!state.TryWithZone(index, row.WithOwed(water, food, row.OwedMaterials), out written, out fault))
			{
				Refuse("reify", fault);
				return state;
			}
			// A debt that is DRAINING is not a shortfall, and saying so on each of the twenty-nine
			// turns a full backlog takes would fill the founder's report with the sound of the
			// thing working (STANDARDS 7b). What could not be covered was already said above, once.
			Tell(System, waterSpent ? (row.OwedWater - water) : 0, foodSpent ? (row.OwedFood - food) : 0, 0, 0);
			return written;
		}

		/// <summary>Whether this unit is inside the half of the plan its visibility belongs to,
		/// and spends it if it is. A visible unit may never be paid for out of the rest's
		/// allowance, or "visible first" would be a preference instead of an order.</summary>
		private static bool Spendable(bool visible, ref int seen, ref int rest)
		{
			if (visible)
			{
				if (seen <= 0)
				{
					return false;
				}
				seen--;
				return true;
			}
			if (rest <= 0)
			{
				return false;
			}
			rest--;
			return true;
		}

		/// <summary>
		/// The settlers whose bodies do not stand where the hour puts them. One heavy unit each
		/// (&sect;0.0(b)), and never more than four a turn, because the heavy tier's cap is a
		/// frame-cost ceiling rather than an ordering preference.
		/// </summary>
		private static List<GameObject> Posted(Zone Z, KingdomSurvey Survey, Dictionary<int, GameObject> stations)
		{
			List<GameObject> wanting = new List<GameObject>();
			long now = (The.Game != null) ? The.Game.TimeTicks : 0L;
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				GameObject settler = Survey.Settlers[i];
				if (GameObject.Validate(settler) && KingdomStations.Misplaced(settler, Z, now, stations))
				{
					wanting.Add(settler);
				}
			}
			return wanting;
		}

		/// <summary>Moves as many anchors as the budget bought, the visible ones first. Vanilla
		/// walks them the rest of the way (&sect;3.2(b)).</summary>
		private static void Anchor(Zone Z, List<GameObject> posted, Dictionary<int, GameObject> stations, int visible, int rest, long TimeTicks)
		{
			int spentVisible = 0;
			int spentRest = 0;
			for (int i = 0; i < posted.Count; i++)
			{
				bool seen = Visible(posted[i].CurrentCell);
				if (seen && spentVisible >= visible)
				{
					continue;
				}
				if (!seen && spentRest >= rest)
				{
					continue;
				}
				if (!KingdomStations.Place(Z, posted[i], TimeTicks, stations))
				{
					continue;
				}
				if (seen) { spentVisible++; } else { spentRest++; }
			}
		}

		private static int VisibleCount(List<GameObject> bodies)
		{
			int seen = 0;
			for (int i = 0; i < bodies.Count; i++)
			{
				if (Visible(bodies[i].CurrentCell))
				{
					seen++;
				}
			}
			return seen;
		}

		/// <summary>The player's own field of view, asked of the engine rather than approximated.
		/// A cell in a zone that is not active is never visible, which is exactly right for a
		/// prefetched zone: nothing in it is what the founder is looking at.</summary>
		private static bool Visible(Cell at)
		{
			return at != null && at.IsVisible();
		}

		private static Cell WaterCell(KingdomSurvey Survey)
		{
			List<LiquidVolume> vessels = Ordered(Survey.Stores);
			for (int i = 0; i < vessels.Count; i++)
			{
				GameObject parent = vessels[i].ParentObject;
				if (GameObject.Validate(parent) && parent.CurrentCell != null)
				{
					return parent.CurrentCell;
				}
			}
			return null;
		}

		private static Cell FoodCell(KingdomSurvey Survey)
		{
			List<GameObject> larders = Ordered(Survey.Larders);
			for (int i = 0; i < larders.Count; i++)
			{
				if (GameObject.Validate(larders[i]) && larders[i].CurrentCell != null)
				{
					return larders[i].CurrentCell;
				}
			}
			return null;
		}

		/// <summary>
		/// One medium unit of water: at most one vessel's worth, landed or drawn.
		/// LIVING-CITY-ARCHITECTURE &sect;0.0(b) prices a unit at <i>one item stack into one
		/// container</i>, so a debt bigger than one vessel takes more than one turn &mdash; which is
		/// the amortisation working, not a shortfall.
		/// </summary>
		private static int SettleWater(KingdomSurvey Survey, int owed)
		{
			if (owed > 0)
			{
				int room = FirstWaterRoom(Survey);
				int offer = (owed < room) ? owed : room;
				return (offer <= 0) ? owed : (owed - Survey.Store(offer));
			}
			if (owed >= 0)
			{
				return 0;
			}
			List<LiquidVolume> vessels = Ordered(Survey.Stores);
			int remaining = -owed;
			for (int i = 0; i < vessels.Count && remaining > 0; i++)
			{
				// Fresh water only: a drain may never launder brine into the books (STANDARDS §1).
				// The delta is measured rather than assumed, which is what LeakFrom does and the
				// only reason this does not call UseDrams itself.
				if (!KingdomLiquids.HasFreshWater(vessels[i]))
				{
					continue;
				}
				// One vessel is one unit. The oldest dedication pays first and the next one waits
				// for the next turn, which is what makes a season's drinking arrive as an amortised
				// drain rather than as one activation's spike.
				remaining -= Survey.LeakFrom(vessels[i], remaining);
				break;
			}
			return -remaining;
		}

		/// <summary>One medium unit of food: at most one larder's worth, landed or drawn, in
		/// dedication order.</summary>
		private static int SettleFood(KingdomSystem System, KingdomSurvey Survey, int owed)
		{
			if (owed > 0)
			{
				int room = FirstFoodRoom(Survey);
				int offer = (owed < room) ? owed : room;
				return (offer <= 0) ? owed : (owed - Survey.StoreFood(offer, CropOf(System)));
			}
			if (owed >= 0)
			{
				return 0;
			}
			List<GameObject> larders = Ordered(Survey.Larders);
			int remaining = -owed;
			for (int i = 0; i < larders.Count && remaining > 0; i++)
			{
				// SpoilFrom is the survey's own "take this many servings out of THIS container and
				// keep the counters right". Reusing it is deliberate: a second implementation of
				// how a food stack comes apart would be a second answer to the same question.
				remaining -= Survey.SpoilFrom(larders[i], remaining);
				break;
			}
			return -remaining;
		}

		/// <summary>Whether this zone's vessels can move a dram in the direction the row owes. A
		/// landing needs room; a draw needs fresh water actually standing in something.</summary>
		private static bool CanMoveWater(KingdomSurvey Survey, int owed)
		{
			if (owed > 0)
			{
				return FirstWaterRoom(Survey) > 0;
			}
			for (int i = 0; i < Survey.Stores.Count; i++)
			{
				if (Survey.Stores[i].Volume > 0 && KingdomLiquids.HasFreshWater(Survey.Stores[i]))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>The food half of <see cref="CanMoveWater"/>.</summary>
		private static bool CanMoveFood(KingdomSurvey Survey, int owed)
		{
			if (owed > 0)
			{
				return FirstFoodRoom(Survey) > 0;
			}
			for (int i = 0; i < Survey.Larders.Count; i++)
			{
				if (GameObject.Validate(Survey.Larders[i]) && KingdomSurvey.HeldIn(Survey.Larders[i]) > 0)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Room in the first vessel that can take fresh water: the size of one landing
		/// unit.</summary>
		private static int FirstWaterRoom(KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.Stores.Count; i++)
			{
				LiquidVolume store = Survey.Stores[i];
				if (store.Volume >= store.MaxVolume || !KingdomLiquids.CanReceiveFreshWater(store))
				{
					continue;
				}
				return store.MaxVolume - store.Volume;
			}
			return 0;
		}

		/// <summary>Room in the first larder that can take a stack: the size of one landing
		/// unit.</summary>
		private static int FirstFoodRoom(KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.Larders.Count; i++)
			{
				GameObject container = Survey.Larders[i];
				if (!GameObject.Validate(container) || container.Inventory == null)
				{
					continue;
				}
				int room = KingdomSurvey.CapacityOf(container) - KingdomSurvey.HeldIn(container);
				if (room > 0)
				{
					return room;
				}
			}
			return 0;
		}

		/// <summary>
		/// What is left of this turn's reify allowance, in weighted thirds.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;0.0: <b>eight units a turn</b>, and the turn is the unit
		/// rather than the call site. The pass reifies the seated zone, the pump reifies it again
		/// on the turn's own tick, and the prefetch reifies a neighbour; all three draw on this.
		/// </para>
		/// </summary>
		private static int Allowance(KingdomSystem System, long TimeTicks)
		{
			Roll(System, TimeTicks);
			int left = KingdomCatchUpRules.BudgetThirdsPerTurn - System.ReifyThirdsSpent;
			return (left > 0) ? left : 0;
		}

		/// <summary>What is left of this turn's body-mint ceiling. Its own figure, because four
		/// mints is a frame cost and not an ordering preference (&sect;0.0(b)).</summary>
		private static int HeavyAllowance(KingdomSystem System, long TimeTicks)
		{
			Roll(System, TimeTicks);
			int left = KingdomBudgetRules.ReifyHeavyMintsPerTurn - System.ReifyHeavySpent;
			return (left > 0) ? left : 0;
		}

		private static void Roll(KingdomSystem System, long TimeTicks)
		{
			if (System.ReifyTick == TimeTicks)
			{
				return;
			}
			System.ReifyTick = TimeTicks;
			System.ReifyThirdsSpent = 0;
			System.ReifyHeavySpent = 0;
		}

		private static void Charge(KingdomSystem System, long TimeTicks, KingdomReifySpend spend)
		{
			Roll(System, TimeTicks);
			System.ReifyThirdsSpent += spend.ThirdsSpent;
			System.ReifyHeavySpent += spend.Heavy;
		}

		/// <summary>The per-turn reify line of &sect;6.5's receipt, in the shape the log-watcher
		/// already reads.</summary>
		private static void Receipt(string zoneId, KingdomReifySpend spend, Stopwatch watch, int owed)
		{
			long microseconds = (watch.ElapsedTicks * 1000000L) / Stopwatch.Frequency;
			KingdomComputeCounters counters = new KingdomComputeCounters(0, spend.Visible, 0, spend.ThirdsSpent, 0L);
			Record(new KingdomPerfReceipt(
				KingdomBudgetLane.Reify,
				zoneId + " owed=" + owed,
				microseconds,
				counters,
				spend.Units,
				KingdomBudgetRules.JudgeMicroseconds(KingdomBudgetLane.Reify, microseconds),
				KingdomBudgetRules.JudgeCount(KingdomBudgetLane.Reify, spend.Units)));
		}

		/// <summary>
		/// Consumption anywhere draws on the same rows (&sect;1.2(a)), but a dram is drunk out of a
		/// particular urn (&sect;3.9). So when the seated zone cannot cover the day the settlement
		/// is about to be billed for, the city's own water and food are carried in from the zones
		/// that hold them, oldest dedication first, and those zones owe their vessels the
		/// difference the next time anybody opens them.
		/// <para>
		/// Nothing is created here and nothing is destroyed: what leaves a row arrives on another
		/// row as a debt against real containers, which is exactly what makes I1 hold across the
		/// carry. A one-zone city, and a city whose seated zone can pay its own bill, are untouched.
		/// </para>
		/// </summary>
		private static KingdomCityState Carry(KingdomSystem System, Zone Z, KingdomSurvey Survey, KingdomCityState state, long TimeTicks)
		{
			long elapsed = (System.LastHeartbeatTick > 0L) ? (TimeTicks - System.LastHeartbeatTick) : 0L;
			if (elapsed <= 0L || state.ZoneCount < 2)
			{
				return state;
			}
			KingdomCityState current = state;
			current = CarryKind(System, Z, current, KingdomStockKind.Water,
				KingdomRules.PolicyUpkeepForElapsed(System.Population, elapsed, System.Stores, System.Stage) - Survey.StoredWater,
				Survey.StorageSpace,
				Survey);
			current = CarryKind(System, Z, current, KingdomStockKind.Food,
				KingdomRules.RationsForElapsed(System.Population, elapsed) - Survey.FoodStored,
				Survey.FoodSpace,
				Survey);
			return current;
		}

		private static KingdomCityState CarryKind(KingdomSystem System, Zone Z, KingdomCityState state, KingdomStockKind kind, long demand, long room, KingdomSurvey Survey)
		{
			if (demand <= 0L || room <= 0L)
			{
				return state;
			}
			long[] moved = new long[state.ZoneCount];
			long total;
			KingdomCityFault fault;
			if (!KingdomCityRules.TryPlanTransfer(state, Z.ZoneID, kind, demand, room, moved, out total, out fault))
			{
				Refuse("carry", fault);
				return state;
			}
			if (total <= 0L)
			{
				return state;
			}
			int landed = (kind == KingdomStockKind.Water)
				? Survey.Store((int)Clamp(total))
				: Survey.StoreFood((int)Clamp(total), CropOf(System));
			if (landed <= 0)
			{
				return state;
			}
			KingdomCityState current;
			long applied;
			if (!KingdomCityRules.TryApplyTransfer(state, kind, moved, landed, out current, out applied, out fault))
			{
				Refuse("carry", fault);
				return state;
			}
			System.Ledger.Note("{{C|" + KingdomCityRules.CarryNote(kind, applied, System.KingdomDisplayName) + "}}");
			KingdomLog.Log("city: carried " + applied + " " + kind + " to " + Z.ZoneID + " from the city's other quarters");
			return current;
		}

		/// <summary>
		/// Ground to model (&sect;3.1). The ground wins for anything physical; the difference is
		/// attributed and told, never silently repaired. A cask with less water in it than the
		/// model expected means the founder poured some.
		/// </summary>
		private static KingdomCityState Reconcile(KingdomSystem System, Zone Z, KingdomSurvey Survey, KingdomCityState state, int index, long TimeTicks)
		{
			KingdomZoneRow row;
			if (!state.TryZone(index, out row))
			{
				return state;
			}
			KingdomStocks ground = Ground(Survey, row);
			if (row.LastReadTick > 0L)
			{
				long water = ground.Water.Level - row.Stocks.Water.Level;
				long food = ground.Food.Level - row.Stocks.Food.Level;
				string note = KingdomCityRules.ReconcileNote(water, food);
				if (note != null)
				{
					// Both directions are recorded; only a SHORTFALL reaches the founder's own
					// register. A cask holding less than the books had is something they can act
					// on — they poured it, or something took it — and a cask holding more is the
					// world working. STANDARDS 7b's other half: the ledger is for what the founder
					// can still do something about, and the log is for everything.
					if (water < 0L || food < 0L)
					{
						System.Ledger.Note("{{K|" + note + "}}");
					}
					KingdomLog.Log("city: reconcile " + Z.ZoneID + " water=" + water + " food=" + food);
				}
			}
			KingdomCityState written;
			KingdomCityFault fault;
			if (!state.TryWithZone(index, row.WithReading(TimeTicks, ground, row.Roofs, Survey.Defence(), row.WaterCarry, row.FoodCarry), out written, out fault))
			{
				Refuse("reconcile", fault);
				return state;
			}
			return written;
		}

		/// <summary>
		/// The work rows, rebuilt from the ground under the founder's feet. A work's row carries
		/// state the engine cannot carry for it and nothing else (&sect;1.2(c)); appearance, name,
		/// tile and contents stay on the object.
		/// </summary>
		private static KingdomCityState ReadWorks(KingdomCityState state, Zone Z, KingdomSurvey Survey)
		{
			List<KingdomWorkRow> kept = new List<KingdomWorkRow>();
			for (int i = 0; i < state.WorkCount; i++)
			{
				KingdomWorkRow row;
				if (state.TryWork(i, out row) && !string.Equals(row.ZoneId, Z.ZoneID, StringComparison.Ordinal))
				{
					kept.Add(row);
				}
			}
			for (int i = 0; i < Survey.Built.Count && kept.Count < KingdomCityState.MaxWorks; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work))
				{
					continue;
				}
				Cell at = work.CurrentCell;
				kept.Add(new KingdomWorkRow(
					// The object's own persistent id, folded by a written-out hash rather than the
					// runtime's: a runtime hash is not stable across processes, and a work id that
					// changes when the game restarts is not an id.
					KingdomCityRules.StableId(work.ID),
					Z.ZoneID,
					(short)((at != null) ? at.X : 0),
					(short)((at != null) ? at.Y : 0),
					work.Blueprint ?? "",
					100 - KingdomWear.WearOf(work),
					// Crew is a roster fact and the roster becomes rows in W2; until it does, the
					// column is honestly empty rather than filled with a proxy for it.
					0,
					(The.Game != null) ? The.Game.TimeTicks : 0L,
					RunStateOf(work)));
			}
			KingdomCityState rebuilt;
			KingdomCityFault fault;
			if (!Rebuild(state, kept, out rebuilt, out fault))
			{
				Refuse("works", fault);
				return state;
			}
			return rebuilt;
		}

		/// <summary>
		/// LIVING-CITY-ARCHITECTURE &sect;3.9's audit, in both directions: after an attended pass
		/// of a fully-visited city, model total == ground total, per stock kind. A mismatch is
		/// named rather than repaired.
		/// </summary>
		public static string AuditLine(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			KingdomCityBook book = (System == null) ? null : System.City;
			if (book == null || Z == null || Survey == null)
			{
				return null;
			}
			book.Normalize();
			int index;
			if (!book.TryZoneRow(Z.ZoneID, out index))
			{
				return null;
			}
			KingdomCatchUpCounter counter = CityCounter(book);
			return KingdomCityRules.AuditNote(
				book.ZoneWaterLevels[index], Survey.StoredWater,
				book.ZoneFoodLevels[index], Survey.FoodStored,
				counter.OwedThirds);
		}

		private static void Audit(KingdomSystem System, Zone Z, KingdomSurvey Survey, string step)
		{
			string line = AuditLine(System, Z, Survey);
			if (line != null)
			{
				KingdomLog.Log("city: " + step + " " + line);
			}
			// Invariant I3 beside invariant I1, and asserted rather than inferred: a registry that
			// has started answering one key with two bodies says so on the pass it happens rather
			// than on the pass a settler is finally seen twice.
			string bindings = KingdomResidents.AuditLine(System);
			if (bindings != null)
			{
				KingdomLog.Log("city: " + step + " " + bindings);
			}
		}

		/// <summary>Everything the city still owes the ground, in weighted thirds. The figure the
		/// receipt reports as <c>owed</c>.</summary>
		public static int OwedThirds(KingdomSystem System)
		{
			KingdomCityBook book = (System == null) ? null : System.City;
			return (book == null) ? 0 : CityCounter(book).OwedThirds;
		}

		// ==================================================================================
		// Small shared helpers
		// ==================================================================================

		private static KingdomCatchUpCounter CityCounter(KingdomCityBook book)
		{
			KingdomCityState state;
			KingdomCityFault fault;
			if (!book.TryRead(out state, out fault))
			{
				return new KingdomCatchUpCounter(0, 0);
			}
			return KingdomCityRules.CityCounter(state);
		}

		/// <summary>The book with a row for this zone, creating an unread one if the city has just
		/// claimed it. An unread row contributes nothing anywhere: nothing is invented for ground
		/// the game has never looked at.</summary>
		private static bool Ensure(KingdomSystem System, Zone Z, out KingdomCityState state, out KingdomCityFault fault)
		{
			state = null;
			if (!System.City.TryRead(out state, out fault))
			{
				return false;
			}
			int index;
			string district;
			System.ZoneDistricts.TryGetValue(Z.ZoneID, out district);
			int code = KingdomCityRules.DistrictCode(district);
			if (IndexOf(state, Z.ZoneID, out index))
			{
				KingdomZoneRow existing;
				state.TryZone(index, out existing);
				if (existing.DistrictCode == code)
				{
					return true;
				}
				KingdomCityState zoned;
				if (!state.TryWithZone(index, existing.WithDistrictCode(code), out zoned, out fault))
				{
					return false;
				}
				state = zoned;
				return true;
			}
			List<KingdomZoneRow> rows = new List<KingdomZoneRow>();
			for (int i = 0; i < state.ZoneCount; i++)
			{
				KingdomZoneRow row;
				if (state.TryZone(i, out row))
				{
					rows.Add(row);
				}
			}
			if (rows.Count >= KingdomCityState.MaxZones)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			rows.Add(new KingdomZoneRow(Z.ZoneID, code, 0L, default(KingdomStocks), 0, 0, 0, 0, 0, 0, 0));
			return Rebuild(state, rows, out state, out fault);
		}

		private static bool Rebuild(KingdomCityState state, List<KingdomZoneRow> zones, out KingdomCityState next, out KingdomCityFault fault)
		{
			KingdomWorkRow[] works = new KingdomWorkRow[state.WorkCount];
			for (int i = 0; i < works.Length; i++)
			{
				state.TryWork(i, out works[i]);
			}
			return Compose(state, zones.ToArray(), works, out next, out fault);
		}

		private static bool Rebuild(KingdomCityState state, List<KingdomWorkRow> works, out KingdomCityState next, out KingdomCityFault fault)
		{
			KingdomZoneRow[] zones = new KingdomZoneRow[state.ZoneCount];
			for (int i = 0; i < zones.Length; i++)
			{
				state.TryZone(i, out zones[i]);
			}
			return Compose(state, zones, works.ToArray(), out next, out fault);
		}

		private static bool Compose(KingdomCityState state, KingdomZoneRow[] zones, KingdomWorkRow[] works, out KingdomCityState next, out KingdomCityFault fault)
		{
			KingdomResidentRow[] residents = new KingdomResidentRow[state.ResidentCount];
			for (int i = 0; i < residents.Length; i++)
			{
				state.TryResident(i, out residents[i]);
			}
			KingdomClockRow[] clocks = new KingdomClockRow[state.ClockCount];
			for (int i = 0; i < clocks.Length; i++)
			{
				state.TryClock(i, out clocks[i]);
			}
			if (!KingdomCityState.TryCreate(state.SchemaVersion, state.RulesVersion, state.SettlementId,
				state.ProcessedThroughTick, state.Stocks, zones, works, residents, clocks, out next, out fault))
			{
				return false;
			}
			for (int i = 0; i < state.ToldCount; i++)
			{
				KingdomToldRow told;
				KingdomCityState carried;
				if (!state.TryTold(i, out told) || !next.TryTell(told, out carried, out fault))
				{
					return false;
				}
				next = carried;
			}
			return true;
		}

		/// <summary>The one publisher (&sect;1.3): the city's own totals are recomputed from its
		/// rows and the whole book is written in one assignment, after the rules have succeeded.</summary>
		private static void Publish(KingdomSystem System, KingdomCityState state)
		{
			KingdomStocks stocks;
			KingdomCityFault fault;
			KingdomCityState totalled = state;
			if (KingdomCityRules.TryCityStocks(state, out stocks))
			{
				KingdomCityState written;
				if (state.TryWithStocks(stocks, out written, out fault))
				{
					totalled = written;
				}
			}
			if (!System.City.TryPublish(totalled, out fault))
			{
				Refuse("publish", fault);
			}
		}

		private static KingdomStocks Ground(KingdomSurvey Survey, KingdomZoneRow row)
		{
			return new KingdomStocks(
				new KingdomStockPair(Floor(Survey.StoredWater), Floor(Survey.StorageCapacity)),
				new KingdomStockPair(Floor(Survey.FoodStored), Floor(Survey.FoodCapacity)),
				row.Stocks.Materials);
		}

		private static bool IndexOf(KingdomCityState state, string zoneId, out int index)
		{
			for (index = 0; index < state.ZoneCount; index++)
			{
				KingdomZoneRow row;
				if (state.TryZone(index, out row) && string.Equals(row.ZoneId, zoneId, StringComparison.Ordinal))
				{
					return true;
				}
			}
			index = -1;
			return false;
		}

		private static void StampDedicationOrder(KingdomSystem System, KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.Stores.Count; i++)
			{
				Stamp(System, Survey.Stores[i].ParentObject);
			}
			for (int i = 0; i < Survey.Larders.Count; i++)
			{
				Stamp(System, Survey.Larders[i]);
			}
		}

		private static void Stamp(KingdomSystem System, GameObject container)
		{
			if (!GameObject.Validate(container) || container.GetIntProperty(DedicationOrderProperty) > 0)
			{
				return;
			}
			System.DedicationCounter++;
			container.SetIntProperty(DedicationOrderProperty, System.DedicationCounter);
		}

		private static List<LiquidVolume> Ordered(List<LiquidVolume> stores)
		{
			List<LiquidVolume> ordered = new List<LiquidVolume>(stores);
			ordered.Sort(delegate(LiquidVolume left, LiquidVolume right)
			{
				return OrdinalOf(left.ParentObject).CompareTo(OrdinalOf(right.ParentObject));
			});
			return ordered;
		}

		private static List<GameObject> Ordered(List<GameObject> containers)
		{
			List<GameObject> ordered = new List<GameObject>(containers);
			ordered.Sort(delegate(GameObject left, GameObject right)
			{
				return OrdinalOf(left).CompareTo(OrdinalOf(right));
			});
			return ordered;
		}

		/// <summary>A container the city has never counted sorts LAST, not first: the drain order
		/// is a stored fact, and an unstamped vessel has no claim to being the oldest.</summary>
		private static int OrdinalOf(GameObject container)
		{
			if (!GameObject.Validate(container))
			{
				return int.MaxValue;
			}
			return KingdomCityRules.DrainOrdinal(container.GetIntProperty(DedicationOrderProperty));
		}

		private static KingdomWorkRunState RunStateOf(GameObject work)
		{
			r_KingdomPlot field = KingdomCrops.FieldOf(work);
			if (field != null)
			{
				return new KingdomWorkRunState(KingdomWorkKind.Growing, (byte)field.Stage, 0, field.NextStageTick);
			}
			if (work.GetIntProperty("KingdomStores") == 1 || work.GetIntProperty("KingdomLarder") == 1)
			{
				return new KingdomWorkRunState(KingdomWorkKind.Store, 0, 0, 0L);
			}
			return new KingdomWorkRunState(KingdomWorkKind.Other, 0, 0, 0L);
		}

		private static string CropOf(KingdomSystem System)
		{
			return KingdomCropRules.CropBlueprintForStyle(System.Style);
		}

		/// <summary>
		/// What one turn's spend moved, and what it could not.
		/// <para>
		/// <paramref name="waterLeft"/> and <paramref name="foodLeft"/> are non-zero only for a kind
		/// whose unit was spent and whose containers gave nothing back &mdash; which is the one thing
		/// LIVING-CITY-ARCHITECTURE &sect;3.9 requires be told, and never silently forgiven. A debt
		/// that is simply draining says nothing, because a debt draining is the design working.
		/// </para>
		/// </summary>
		private static void Tell(KingdomSystem System, int waterPaid, int foodPaid, int waterLeft, int foodLeft)
		{
			string note = KingdomCityRules.ShortfallNote(waterLeft, foodLeft);
			if (note != null)
			{
				System.Ledger.Note("{{r|" + note + "}}");
			}
			if (KingdomLog.Enabled && (waterPaid != 0 || foodPaid != 0 || note != null))
			{
				KingdomLog.Log("city: reify paid water=" + waterPaid + " food=" + foodPaid + " unpaid water=" + waterLeft + " food=" + foodLeft);
			}
		}

		private static void Refuse(string step, KingdomCityFault fault)
		{
			KingdomLog.Log("city: " + step + " refused (" + fault + "); the book is unchanged");
		}

		private static int Floor(int value)
		{
			return (value > 0) ? value : 0;
		}

		private static long Clamp(long value)
		{
			if (value <= 0L)
			{
				return 0L;
			}
			return (value > int.MaxValue) ? int.MaxValue : value;
		}

		/// <summary>A sighting tick, quantised to the day the retired game-state slot could hold.
		/// Kept so the staleness clause reads exactly as it did.</summary>
		private static long DayStamp(long TimeTicks)
		{
			return (long)KingdomSubsidence.SeenStamp(TimeTicks) * KingdomRules.TicksPerDay;
		}
	}
}
