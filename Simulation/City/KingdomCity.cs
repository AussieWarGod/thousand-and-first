using System;
using System.Collections.Generic;

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
			state = Reify(System, Z, Survey, state, index);
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
		/// Model to ground: what this zone owes, paid onto real containers in dedication order.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.9, invariant I4. A unit leaves the debt at the instant
		/// it LANDS, never at the instant it is scheduled, so re-entering, reloading or
		/// re-activating cannot pay the same debt twice. What the containers could not cover stays
		/// on the row and is told — never silently forgiven, and never silently repaired.
		/// </para>
		/// </summary>
		private static KingdomCityState Reify(KingdomSystem System, Zone Z, KingdomSurvey Survey, KingdomCityState state, int index)
		{
			KingdomZoneRow row;
			if (!state.TryZone(index, out row) || (row.OwedWater == 0 && row.OwedFood == 0))
			{
				return state;
			}
			int water = SettleWater(System, Survey, row.OwedWater);
			int food = SettleFood(System, Survey, row.OwedFood);
			KingdomCityState written;
			KingdomCityFault fault;
			if (!state.TryWithZone(index, row.WithOwed(water, food, row.OwedMaterials), out written, out fault))
			{
				Refuse("reify", fault);
				return state;
			}
			Tell(System, row.OwedWater - water, row.OwedFood - food, water, food);
			return written;
		}

		/// <summary>Drams still owed after the vessels have paid what they can, in dedication
		/// order. Positive owed lands; negative owed draws.</summary>
		private static int SettleWater(KingdomSystem System, KingdomSurvey Survey, int owed)
		{
			if (owed > 0)
			{
				return owed - Survey.Store(owed);
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
				remaining -= Survey.LeakFrom(vessels[i], remaining);
			}
			return -remaining;
		}

		/// <summary>Servings still owed after the larders have paid what they can, in dedication
		/// order.</summary>
		private static int SettleFood(KingdomSystem System, KingdomSurvey Survey, int owed)
		{
			if (owed > 0)
			{
				return owed - Survey.StoreFood(owed, CropOf(System));
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
			}
			return -remaining;
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

		private static void Tell(KingdomSystem System, int waterPaid, int foodPaid, int waterLeft, int foodLeft)
		{
			string note = KingdomCityRules.ShortfallNote(waterLeft, foodLeft);
			if (note != null)
			{
				System.Ledger.Note("{{r|" + note + "}}");
			}
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("city: reify paid water=" + waterPaid + " food=" + foodPaid + " still-owed water=" + waterLeft + " food=" + foodLeft);
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
