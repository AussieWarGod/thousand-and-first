using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.AI.GoalHandlers;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The porter, at the engine's edge: minted at the edge of the zone the founder is standing in,
	/// walked by vanilla, putting real goods into a real container, and gone.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.7, and it is nearly free because every piece of it already
	/// stands. <b>Total cost: two reify units and a walk vanilla was going to do anyway.</b>
	/// </para>
	/// <para>
	/// Engine-coupled by design and paired with <see cref="KingdomJobRules"/> the way
	/// <c>KingdomCity</c> is paired with <c>KingdomCityRules</c>: nothing here decides anything. It
	/// reads the ground, asks the rules, obeys the binding registry, and applies the answer.
	/// </para>
	/// <para>
	/// <b>What W3 ships is one flow.</b> &sect;7.4 gives W6 nearest-holder sourcing and
	/// capacity-bound batching, <i>"because both only bite once many jobs compete over many
	/// holders"</i>. So there is one job kind here &mdash; the harvest already in flight, whose
	/// model credit G2 shipped &mdash; and the planner is the itinerary, not a 2-opt over an empty
	/// room.
	/// </para>
	/// </summary>
	public static class KingdomPorters
	{
		/// <summary>
		/// Vanilla's own "the simulation created this; the simulation may remove it".
		/// <c>D/XRL/World/Parts/GenericInventoryRestocker.cs:229, 257</c> &mdash; and the removal
		/// side of that protocol is exactly the licence the stale-transient sweep needs.
		/// </summary>
		public const string StockProperty = "_stock";

		/// <summary>Vanilla's "never touch, whoever put it here".</summary>
		public const string NoRestockProperty = "norestock";

		/// <summary>
		/// One trip's load, in servings.
		/// <para>
		/// A stand-in for W6's capacity-bound batching (&sect;3.10(4)) and named as one. It is a
		/// <b>reify</b> figure rather than a fiction about how much a person can lift: one medium
		/// unit is <i>one item stack into one container</i> (&sect;0.0(b)), and a load that minted a
		/// hundred objects on the turn it was created would break the per-turn budget the whole
		/// wave is about. What does not fit stays on the road and the next porter carries it.
		/// </para>
		/// </summary>
		public const int LoadPerTrip = 12;

		// ==================================================================================
		// Opening a delivery
		// ==================================================================================

		/// <summary>
		/// Embodies a load that is already on the road, into the zone the founder is standing in.
		/// <para>
		/// The destination's zone is the attended one at the moment the unit is reified, which is
		/// exactly the condition &sect;3.7 puts on an embodied rendering. Returns the servings that
		/// left the road onto a real back; zero means nothing was embodied and the ordinary
		/// materialisation path still owns the load, which is I2's second rendering and not a
		/// failure.
		/// </para>
		/// </summary>
		public static int Embody(KingdomSystem System, Zone Z, KingdomSurvey Survey, string SourceZoneId, string Blueprint, int Amount, long TimeTicks)
		{
			if (System == null || !System.Founded || Z == null || Survey == null || System.Jobs == null
				|| Amount <= 0 || string.IsNullOrEmpty(Blueprint) || TimeTicks <= 0L)
			{
				return 0;
			}
			if (!System.ClaimedZones.Contains(Z.ZoneID) || !KingdomWord.StandsIn(Z))
			{
				// A porter nobody is standing there to watch is a body minted for an empty room.
				// The load stays on the road and the plain rendering keeps it.
				return 0;
			}
			GameObject larder = NearestLarderWithRoom(Survey);
			Cell destination = (larder == null) ? null : larder.CurrentCell;
			if (destination == null)
			{
				return 0;
			}
			KingdomJobTable table;
			KingdomCityFault fault;
			if (!System.Jobs.TryRead(out table, out fault))
			{
				Refuse("read", fault);
				return 0;
			}
			// W6, LIVING-CITY-ARCHITECTURE §3.10(4). Capacity-bound batching, at the one moment it
			// can actually prevent the pathology: BEFORE a second carrier exists. A trip already
			// running to this ground with room on its back takes the load, and no second porter is
			// minted to walk the same road half empty. That is assertion 2 of §3.10 made true by
			// construction rather than checked afterwards — KingdomLogisticsRules.TryNoTwoHalfEmptyTrips
			// is the same rule written as a predicate, and the tests hold this path to it.
			int folded = Fold(System, Z, table, Blueprint, Amount, TimeTicks);
			if (folded > 0)
			{
				return folded;
			}
			if (table.Count >= KingdomJobRules.MaxOpenJobs)
			{
				// §3.8's cap, and a refusal rather than a queue: the load is not lost, it is simply
				// still on the road, which is where it already was.
				return 0;
			}
			int load = (Amount < LoadPerTrip) ? Amount : LoadPerTrip;
			int jobId = System.Jobs.MintJobId();
			KingdomZoneStep edge = KingdomJobRules.EdgeToward(Z.ZoneID, SourceZoneId);
			int width = (Z.Width > 2) ? Z.Width : KingdomJobRules.ZoneWidth;
			int height = (Z.Height > 2) ? Z.Height : KingdomJobRules.ZoneHeight;
			short entryX;
			short entryY;
			if (!KingdomJobRules.TryDrawEntryCell(System.SimulationSeed, SeedLabel(System), jobId, edge, width, height, out entryX, out entryY, out fault))
			{
				Refuse("entry", fault);
				return 0;
			}
			int originCode;
			if (!KingdomJobRules.TryDrawOrigin(System.SimulationSeed, SeedLabel(System), jobId, KingdomRules.Origins.Length, out originCode, out fault))
			{
				originCode = KingdomResidentRules.NoOrigin;
			}
			KingdomLeg[] legs;
			int legCount;
			if (!TryPlan(System, Z, entryX, entryY, (short)destination.X, (short)destination.Y, edge, TimeTicks, SourceZoneId, out legs, out legCount, out fault))
			{
				Refuse("plan", fault);
				return 0;
			}
			GameObject body = Mint(System, Z, jobId, entryX, entryY, originCode, TimeTicks);
			if (body == null)
			{
				return 0;
			}
			int carried = Load(body, Blueprint, load);
			if (carried <= 0)
			{
				Release(System, jobId, body, KingdomUnbindCause.JobClosed);
				return 0;
			}
			KingdomJobRow row = new KingdomJobRow(
				jobId,
				KingdomJobKind.Delivery,
				KingdomStockKind.Food,
				carried,
				SourceZoneId ?? "",
				Z.ZoneID,
				TimeTicks,
				KingdomItineraryRules.WalkTicksPerCellDefault,
				KingdomJobStatus.Open,
				originCode,
				0,
				legs,
				legCount);
			KingdomJobTable opened;
			if (!table.TryOpen(row, out opened, out fault) || !System.Jobs.TryPublish(opened, out fault))
			{
				Refuse("open", fault);
				Release(System, jobId, body, KingdomUnbindCause.JobClosed);
				return 0;
			}
			r_KingdomPorter part = body.RequirePart<r_KingdomPorter>();
			part.JobId = jobId;
			part.DestX = destination.X;
			part.DestY = destination.Y;
			part.ExitX = entryX;
			part.ExitY = entryY;
			Walk(body, Z, destination.X, destination.Y);
			KingdomLog.Log("porter: job " + jobId + " carries " + carried + " into " + Z.ZoneID
				+ " by the " + edge + " edge, " + legCount + " legs");
			return carried;
		}

		/// <summary>
		/// Adds a load to a trip that is already running to this ground, or returns zero.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.10(4): <i>"group by carrier capacity and route
		/// overlap"</i>. Route overlap is taken at the granularity the model has — two loads bound
		/// for the same ground share the whole road — and capacity is <see cref="LoadPerTrip"/>,
		/// the same reify-denominated figure a single trip already carried.
		/// </para>
		/// <para>
		/// The candidate is the LOWEST open job id that fits, which is the seed order the whole
		/// planner is written against (&sect;3.10(4)), so the fold is deterministic and has no draw
		/// in it. The route is not re-planned: the carrier is already walking to the same larder,
		/// so the legs are still true and only the back is heavier. A carrier that has already
		/// deposited is passed over — its cargo is zero and it is on its way out.
		/// </para>
		/// </summary>
		private static int Fold(KingdomSystem System, Zone Z, KingdomJobTable table, string Blueprint, int Amount, long TimeTicks)
		{
			// One walk of the ground, not one per candidate: the job cap is sixteen and a zone is
			// hundreds of objects, so a lookup inside the loop would be the expensive part of a
			// step that exists to make the pass cheaper.
			List<GameObject> standing = null;
			for (int i = 0; i < table.Count; i++)
			{
				KingdomJobRow row;
				if (!table.TryAt(i, out row)
					|| row.Status != KingdomJobStatus.Open
					|| row.Cargo != KingdomStockKind.Food
					|| row.CargoAmount <= 0
					|| row.CargoAmount >= LoadPerTrip
					|| !string.Equals(row.DestZoneId, Z.ZoneID, StringComparison.Ordinal)
					|| KingdomJobRules.Deposited(row, TimeTicks))
				{
					continue;
				}
				if (standing == null)
				{
					standing = Z.GetObjects();
				}
				GameObject body = Carrier(standing, row.JobId);
				if (body == null)
				{
					continue;
				}
				int room = LoadPerTrip - row.CargoAmount;
				int added = Load(body, Blueprint, (Amount < room) ? Amount : room);
				if (added <= 0)
				{
					continue;
				}
				KingdomJobTable next;
				KingdomCityFault fault;
				if (!table.TryReplace(row.WithCargo(row.CargoAmount + added), out next, out fault)
					|| !System.Jobs.TryPublish(next, out fault))
				{
					Refuse("fold", fault);
					return 0;
				}
				KingdomLog.Log("porter: job " + row.JobId + " takes " + added + " more, now carrying "
					+ (row.CargoAmount + added) + " of " + LoadPerTrip + " into " + Z.ZoneID);
				return added;
			}
			return 0;
		}

		/// <summary>The body walking one job in this ground, or null. The registry owns minting and
		/// this only finds what it already minted, so there is still exactly one path to a
		/// body.</summary>
		private static GameObject Carrier(List<GameObject> found, int JobId)
		{
			for (int i = 0; found != null && i < found.Count; i++)
			{
				GameObject body = found[i];
				if (!GameObject.Validate(body))
				{
					continue;
				}
				r_KingdomPorter part = body.GetPart<r_KingdomPorter>();
				if (part != null && part.JobId == JobId)
				{
					return body;
				}
			}
			return null;
		}

		// ==================================================================================
		// Rendering, stepping, and closing
		// ==================================================================================

		/// <summary>
		/// Puts every open job's carrier where the model says it is, in the zone that has just
		/// become attended.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.7, the edge handoff: <b>materialisation places the
		/// carrier at <c>At(job, now)</c></b>, and because <c>now</c> is barely past the leg's
		/// departure that is just inside the entry edge, a cell or two along. Cross slower and the
		/// porter is further on; both are correct renderings of the same one answer, which is the
		/// whole of I5.
		/// </para>
		/// </summary>
		public static void Render(KingdomSystem System, Zone Z, long TimeTicks)
		{
			KingdomJobTable table;
			KingdomCityFault fault;
			if (System == null || !System.Founded || Z == null || System.Jobs == null || System.Jobs.Count == 0
				|| !System.Jobs.TryRead(out table, out fault))
			{
				return;
			}
			for (int i = 0; i < table.Count; i++)
			{
				KingdomJobRow row;
				if (!table.TryAt(i, out row))
				{
					continue;
				}
				KingdomItineraryFix fix;
				if (!KingdomItineraryRules.TryAt(row.Legs(), row.LegCount, TimeTicks, out fix, out fault))
				{
					continue;
				}
				if (!string.Equals(fix.ZoneId, Z.ZoneID, StringComparison.Ordinal)
					|| fix.Phase == KingdomItineraryPhase.Delivered
					|| fix.Phase == KingdomItineraryPhase.Pending)
				{
					continue;
				}
				Place(System, Z, row, fix, TimeTicks);
			}
		}

		/// <summary>
		/// One porter's turn: deposit on arrival, then leave, then be gone.
		/// <para>
		/// A part rather than a <c>DelegateGoal</c> chain, and deliberately: <c>DelegateGoal</c>'s
		/// three delegates are all <c>[NonSerialized]</c>
		/// (<c>D/XRL/World/AI/GoalHandlers/DelegateGoal.cs:8-19</c>), so a save taken mid-walk would
		/// come back with a carrier who has forgotten what they were carrying it for. The walk is
		/// still vanilla's &mdash; <c>Brain.PushGoal(new MoveTo(...))</c> and nothing else &mdash;
		/// and this only decides what happens when it ends.
		/// </para>
		/// </summary>
		internal static void Step(GameObject Body, r_KingdomPorter Part, long TimeTick)
		{
			KingdomSystem system = (The.Game == null) ? null : The.Game.RequireSystem<KingdomSystem>();
			Zone zone = (Body == null) ? null : Body.CurrentZone;
			if (system == null || !system.Founded || zone == null || Part == null || Part.JobId == 0 || system.Jobs == null)
			{
				return;
			}
			KingdomJobTable table;
			KingdomCityFault fault;
			if (!system.Jobs.TryRead(out table, out fault))
			{
				return;
			}
			KingdomJobRow row;
			if (!table.TryGet(Part.JobId, out row))
			{
				// The model closed this job while the ground was elsewhere. The sweep is the place
				// that removes the body; a turn tick is not, because a body that deleted itself
				// mid-turn is a body the engine is still iterating over.
				return;
			}
			if (row.CargoAmount > 0 && Near(Body, Part.DestX, Part.DestY))
			{
				Deposit(system, zone, Body, Part, row, TimeTick);
				return;
			}
			if (row.CargoAmount <= 0 && Near(Body, Part.ExitX, Part.ExitY))
			{
				Close(system, Part.JobId, "the load reached the store and the carrier went back the way they came");
				return;
			}
			bool overrun;
			if (KingdomItineraryRules.TryHasOverrun(row.Legs(), row.LegCount, TimeTick, out overrun, out fault) && overrun)
			{
				// §3.7: a job whose elapsed exceeds twice its projected duration FAILS and is told,
				// so a founder who blocks a doorway forever produces a story, not an unbounded job
				// set. The cargo is real items and stays exactly where it is.
				Fail(system, Part.JobId);
			}
		}

		/// <summary>
		/// The stale-transient sweep. LIVING-CITY-ARCHITECTURE &sect;3.8's t3, and <b>this is the
		/// wave that lands the despawn</b>: W2 shipped the verdict.
		/// <para>
		/// Runs at <c>ZoneThawedEvent</c>, before intake and before any reify, because that is the
		/// one instant the goods could exist twice &mdash; in the larder and in a frozen pack. What
		/// it removes is <c>_stock</c>: items the simulation made and may remove, vanilla's own
		/// protocol. Anything that is not <c>_stock</c>, or that answers <c>IsImportant()</c>, is
		/// <b>dropped to the cell first and never destroyed.</b>
		/// </para>
		/// <para>
		/// Licensed for transients only. A resident is a person, and &sect;8.3's <i>materialisation
		/// may never remove a body</i> stands untouched &mdash; there is no input for it here,
		/// because the sweep is keyed on a job id and a person does not have one.
		/// </para>
		/// </summary>
		public static int Sweep(KingdomSystem System, Zone Z)
		{
			if (System == null || !System.Founded || Z == null)
			{
				return 0;
			}
			List<GameObject> found = Z.GetObjects();
			int swept = 0;
			for (int i = 0; found != null && i < found.Count; i++)
			{
				GameObject body = found[i];
				if (!GameObject.Validate(body) || KingdomResidents.SweepVerdict(System, body) != KingdomSweepVerdict.Stale)
				{
					continue;
				}
				Spill(body);
				KingdomLog.Log("porter: swept a stale carrier out of " + Z.ZoneID + " (job "
					+ body.GetIntProperty(KingdomResidents.JobIdProperty) + ")");
				body.Obliterate();
				swept++;
			}
			if (swept > 0)
			{
				System.Ledger.Note("{{K|" + KingdomCityRules.SweptNote(swept) + "}}");
			}
			return swept;
		}

		/// <summary>
		/// Closes every job whose itinerary has run out while nobody was there to render it, and
		/// puts what the carrier was still holding back on the road.
		/// <para>
		/// &sect;3.8's t2, exactly: the model closes the job, evicts the binding, and
		/// <b>re-attributes the outstanding deposit unit from the porter to the ordinary
		/// materialisation path</b>. Which is what makes the sweep at t3 deduplication rather than
		/// destruction of property &mdash; the load is back in the city's own books before the
		/// frozen pack is ever opened.
		/// </para>
		/// </summary>
		public static int Retire(KingdomSystem System, long TimeTicks)
		{
			KingdomJobTable table;
			KingdomCityFault fault;
			if (System == null || !System.Founded || System.Jobs == null || System.Jobs.Count == 0
				|| !System.Jobs.TryRead(out table, out fault))
			{
				// The ordinary case, and the one that has to cost nothing: a realm with no carrier
				// on the road does not read a registry to find that out.
				return 0;
			}
			int[] ids = table.OpenIds();
			int retired = 0;
			for (int i = 0; i < ids.Length; i++)
			{
				KingdomJobRow row;
				if (!table.TryGet(ids[i], out row))
				{
					continue;
				}
				KingdomItineraryFix fix;
				bool overrun;
				bool done = KingdomItineraryRules.TryAt(row.Legs(), row.LegCount, TimeTicks, out fix, out fault)
					&& fix.Phase == KingdomItineraryPhase.Delivered;
				if (!done && (!KingdomItineraryRules.TryHasOverrun(row.Legs(), row.LegCount, TimeTicks, out overrun, out fault) || !overrun))
				{
					continue;
				}
				KingdomBodyPresence presence = KingdomResidents.PresenceOfKey(System, ids[i], KingdomBindingKind.Transient, row.DestZoneId);
				if (presence == KingdomBodyPresence.Here || presence == KingdomBodyPresence.Elsewhere)
				{
					// §3.8 t2 is about a carrier the model has outlived, and a carrier still on
					// resident ground has not been outlived: they are walking, and KingdomPorters
					// Step closes them where they stand. Closing one here would delete a body in
					// front of the founder for arithmetic they cannot see.
					continue;
				}
				if (Close(System, ids[i], null))
				{
					retired++;
				}
			}
			return retired;
		}

		// ==================================================================================
		// The pieces
		// ==================================================================================

		/// <summary>Mint-or-move, and never anything else: the registry is the only path to a body
		/// (&sect;3.8). A verdict that is not <c>Mint</c> for a transient is a refusal, and the
		/// debt stays owed.</summary>
		private static GameObject Mint(KingdomSystem System, Zone Z, int jobId, short x, short y, int originCode, long TimeTicks)
		{
			if (KingdomResidents.Judge(System, jobId, KingdomBindingKind.Transient, Z.ZoneID) != KingdomBindingVerdict.Mint)
			{
				return null;
			}
			Cell at = Standing(Z, x, y);
			if (at == null)
			{
				return null;
			}
			GameObject body = GameObject.Create(KingdomGrowth.SettlerBlueprint());
			if (body == null)
			{
				return null;
			}
			at.AddObject(body);
			body.MakeActive();
			// A carrier is a visitor, not a resident: never enrolled, never named on the roll,
			// never counted in the population. The job id is the whole of their identity and it is
			// what the sweep is keyed on.
			body.SetIntProperty(KingdomResidents.JobIdProperty, jobId);
			Settle(body);
			string origin = KingdomResidentRules.OriginKey(originCode);
			if (!string.IsNullOrEmpty(origin))
			{
				body.SetStringProperty("KingdomOrigin", origin);
			}
			Render render = body.Render;
			if (render != null)
			{
				render.DisplayName = "porter";
			}
			KingdomResidents.Bind(System, jobId, KingdomBindingKind.Transient, Z.ZoneID, body, TimeTicks);
			return body;
		}

		/// <summary>Puts a carrier where the model says they are, minting one if the registry says
		/// there is none and moving the one there is if there is.</summary>
		private static void Place(KingdomSystem System, Zone Z, KingdomJobRow row, KingdomItineraryFix fix, long TimeTicks)
		{
			KingdomBindingVerdict verdict = KingdomResidents.Judge(System, row.JobId, KingdomBindingKind.Transient, Z.ZoneID);
			if (verdict == KingdomBindingVerdict.Refuse)
			{
				return;
			}
			if (verdict == KingdomBindingVerdict.Move)
			{
				// Already standing here and already walking. The model's answer and the ground's
				// may have drifted while the founder was in the room, so this is where the ground
				// wins and the remainder of the itinerary shifts to match it (§3.7).
				Reproject(System, Z, row, fix, TimeTicks);
				return;
			}
			GameObject body = Mint(System, Z, row.JobId, fix.X, fix.Y, row.OriginCode, TimeTicks);
			if (body == null)
			{
				return;
			}
			if (row.CargoAmount > 0)
			{
				Load(body, KingdomCropRules.CropBlueprintForStyle(System.Style), row.CargoAmount);
			}
			r_KingdomPorter part = body.RequirePart<r_KingdomPorter>();
			part.JobId = row.JobId;
			KingdomLeg leg;
			if (row.TryLeg((fix.LegIndex < 0) ? 0 : fix.LegIndex, out leg))
			{
				part.DestX = leg.ExitX;
				part.DestY = leg.ExitY;
				// The way OUT of this ground is the last leg that runs through it, never the leg
				// they are on: a carrier whose exit was their next waypoint would close the job on
				// the larder's own cell and vanish in front of the founder.
				KingdomLeg last = leg;
				for (int i = row.LegCount - 1; i >= 0; i--)
				{
					KingdomLeg candidate;
					if (row.TryLeg(i, out candidate) && string.Equals(candidate.ZoneId, Z.ZoneID, StringComparison.Ordinal))
					{
						last = candidate;
						break;
					}
				}
				part.ExitX = last.ExitX;
				part.ExitY = last.ExitY;
				Walk(body, Z, leg.ExitX, leg.ExitY);
			}
			KingdomLog.Log("porter: job " + row.JobId + " walks into " + Z.ZoneID + " at " + fix.X + "," + fix.Y);
		}

		/// <summary>
		/// The re-projection rule, at the one place &sect;3.7 puts it: check-in, where the ground
		/// already wins.
		/// <para>
		/// <b>Only the unstarted remainder of an itinerary may move.</b> A leg already begun keeps
		/// its <c>DepartTick</c>; the current leg's <c>ArriveTick</c> and every later leg shift by
		/// the same signed delta. So a porter the founder body-blocks for ten turns arrives ten
		/// turns later and everything downstream shifts by ten &mdash; no rubber-banding, no
		/// catch-up sprint, no time travel.
		/// </para>
		/// <para>
		/// Bounded at <b>one re-projection per leg</b>, and a job whose elapsed exceeds twice its
		/// projected duration fails instead (<see cref="Fail"/>) &mdash; so a founder who blocks a
		/// doorway forever produces a story and not an unbounded job set.
		/// </para>
		/// </summary>
		private static void Reproject(KingdomSystem System, Zone Z, KingdomJobRow row, KingdomItineraryFix fix, long TimeTicks)
		{
			GameObject body = Resolve(row.JobId);
			r_KingdomPorter part = (body == null) ? null : body.GetPart<r_KingdomPorter>();
			Cell at = (body == null) ? null : body.CurrentCell;
			if (part == null || at == null || fix.LegIndex < 0 || part.ReprojectedLeg == fix.LegIndex + 1)
			{
				return;
			}
			int behind;
			KingdomCityFault fault;
			if (!KingdomItineraryRules.TryChebyshev(at.X, at.Y, fix.X, fix.Y, out behind, out fault) || behind <= 0)
			{
				return;
			}
			int perCell = (row.WalkTicksPerCell > 0) ? row.WalkTicksPerCell : KingdomItineraryRules.WalkTicksPerCellDefault;
			KingdomLeg[] shifted;
			if (!KingdomItineraryRules.TryReproject(row.Legs(), row.LegCount, fix.LegIndex, (long)behind * perCell, out shifted, out fault))
			{
				return;
			}
			KingdomJobTable table;
			KingdomJobTable next;
			if (!System.Jobs.TryRead(out table, out fault)
				|| !table.TryReplace(row.WithLegs(shifted, row.LegCount), out next, out fault)
				|| !System.Jobs.TryPublish(next, out fault))
			{
				Refuse("reproject", fault);
				return;
			}
			part.ReprojectedLeg = fix.LegIndex + 1;
			KingdomLog.Log("porter: job " + row.JobId + " re-projected by " + ((long)behind * perCell)
				+ " ticks on leg " + fix.LegIndex + "; the remainder shifts and nothing sprints");
		}

		/// <summary>The load, put into the real container it was carried to. One medium reify unit
		/// (&sect;0.0(b)): one item stack into one container.</summary>
		private static void Deposit(KingdomSystem System, Zone Z, GameObject Body, r_KingdomPorter Part, KingdomJobRow row, long TimeTick)
		{
			Cell at = Z.GetCell(Part.DestX, Part.DestY);
			GameObject store = LarderAt(at);
			if (store == null || store.Inventory == null)
			{
				// The larder was struck while the porter was walking to it. The goods are real and
				// stay on their back; the overrun rule closes the job and tells it.
				return;
			}
			List<GameObject> carried = Body.Inventory == null ? null : Body.Inventory.GetObjects();
			int landed = 0;
			for (int i = 0; carried != null && i < carried.Count; i++)
			{
				GameObject item = carried[i];
				if (!GameObject.Validate(item))
				{
					continue;
				}
				Body.Inventory.RemoveObject(item);
				store.Inventory.AddObject(item, Silent: true);
				landed++;
			}
			KingdomJobTable table;
			KingdomJobTable next;
			KingdomCityFault fault;
			if (System.Jobs.TryRead(out table, out fault)
				&& table.TryReplace(row.WithCargoLanded(), out next, out fault))
			{
				System.Jobs.TryPublish(next, out fault);
			}
			System.Ledger.Note("{{G|" + KingdomCityRules.PorterNote(landed, store.ShortDisplayName) + "}}");
			XRL.Messages.MessageQueue.AddPlayerMessage("{{G|" + KingdomCityRules.PorterNote(landed, store.ShortDisplayName) + "}}");
			KingdomLog.Log("porter: job " + row.JobId + " deposited " + landed + " into " + store.ShortDisplayName);
			Walk(Body, Z, Part.ExitX, Part.ExitY);
		}

		/// <summary>Closes a job: the row is evicted, the binding with it, and anything still on
		/// the carrier's back goes back on the road. Absence from the registry is proof of
		/// closure, so there is no second list to keep in step.</summary>
		private static bool Close(KingdomSystem System, int jobId, string Telling)
		{
			KingdomJobTable table;
			KingdomJobTable next;
			KingdomJobRow closed;
			KingdomCityFault fault;
			if (!System.Jobs.TryRead(out table, out fault) || !table.TryClose(jobId, out next, out closed, out fault))
			{
				return false;
			}
			if (!System.Jobs.TryPublish(next, out fault))
			{
				Refuse("close", fault);
				return false;
			}
			GameObject standing = Resolve(jobId);
			bool removed = GameObject.Validate(standing);
			// Re-attributed to the ordinary materialisation path (§3.8 t2) EXACTLY when the goods
			// go away with a body: now, because we are about to remove it, or later, because it is
			// frozen and the sweep will. A carrier that was killed leaves its load on the ground
			// where it fell — its binding no longer resolves and its ground is resident, and those
			// two facts together are the difference between deduplication and inventing a harvest.
			bool goodsLeaveWithIt = removed || OnDisk(System, jobId);
			if (closed.CargoAmount > 0 && goodsLeaveWithIt)
			{
				System.PendingCrop += closed.CargoAmount;
				if (string.IsNullOrEmpty(System.PendingCropBlueprint))
				{
					System.PendingCropBlueprint = KingdomCropRules.CropBlueprintForStyle(System.Style);
				}
			}
			Release(System, jobId, standing, KingdomUnbindCause.JobClosed);
			if (closed.CargoAmount > 0 && removed)
			{
				// The one ledger line §3.8 owes when a load changes hands mid-journey. Said here
				// only when a body was actually taken off the ground; a carrier still frozen
				// somewhere gets the same line from the sweep, once, when their ground opens.
				System.Ledger.Note("{{K|" + KingdomCityRules.SweptNote(1) + "}}");
			}
			if (!string.IsNullOrEmpty(Telling))
			{
				KingdomLog.Log("porter: job " + jobId + " closed - " + Telling);
			}
			return true;
		}

		/// <summary>
		/// A carrier that could not get through. LIVING-CITY-ARCHITECTURE &sect;3.7: the job
		/// <b>fails</b> and is told, and <b>the cargo stays where it fell as real items under the
		/// protection law</b> &mdash; so a founder who blocks a doorway forever produces a story
		/// rather than an unbounded job set.
		/// <para>
		/// The load is set down first and its <c>_stock</c> mark taken off it, which is what makes
		/// it the founder's rather than the simulation's: nothing this mod does may ever remove it
		/// again, and the row is zeroed so the closure below has nothing left to re-attribute.
		/// </para>
		/// </summary>
		private static void Fail(KingdomSystem System, int jobId)
		{
			KingdomJobTable table;
			KingdomJobRow row;
			KingdomCityFault fault;
			if (!System.Jobs.TryRead(out table, out fault) || !table.TryGet(jobId, out row))
			{
				return;
			}
			GameObject body = Resolve(jobId);
			int dropped = Abandon(body);
			KingdomJobTable next;
			if (table.TryReplace(row.WithCargoLanded(), out next, out fault))
			{
				System.Jobs.TryPublish(next, out fault);
			}
			KingdomWord.Ambient(System, System.SeatName, KingdomWord.StandsIn(body == null ? null : body.CurrentZone),
				KingdomCityRules.PorterFailedNote((dropped > 0) ? dropped : row.CargoAmount));
			Close(System, jobId, "outlived twice its projected duration");
		}

		/// <summary>Sets a carrier's whole load down where they stand and hands it to the founder:
		/// the <c>_stock</c> mark comes off, so the sweep's licence no longer covers it and nothing
		/// this mod does can take it again.</summary>
		private static int Abandon(GameObject Body)
		{
			if (!GameObject.Validate(Body) || Body.Inventory == null || Body.CurrentCell == null)
			{
				return 0;
			}
			Cell at = Body.CurrentCell;
			List<GameObject> held = Body.Inventory.GetObjects();
			int dropped = 0;
			for (int i = 0; held != null && i < held.Count; i++)
			{
				GameObject item = held[i];
				if (!GameObject.Validate(item))
				{
					continue;
				}
				item.RemoveIntProperty(StockProperty);
				Body.Inventory.RemoveObject(item);
				at.AddObject(item);
				dropped++;
			}
			return dropped;
		}

		/// <summary>Whether this job's carrier is on ground that has gone to disk. A body that no
		/// longer resolves in a RESIDENT zone was destroyed, and a destroyed carrier's load is
		/// already lying somewhere.</summary>
		private static bool OnDisk(KingdomSystem System, int jobId)
		{
			string zoneId;
			if (!KingdomResidents.TryBoundZone(System, jobId, KingdomBindingKind.Transient, out zoneId))
			{
				return false;
			}
			return The.ZoneManager == null || !The.ZoneManager.CachedZonesContains(zoneId);
		}

		/// <summary>Unbinds and removes one transient body. Never a resident: this is only ever
		/// called with a body this file minted, and the binding kind says so.</summary>
		private static void Release(KingdomSystem System, int jobId, GameObject Body, KingdomUnbindCause cause)
		{
			KingdomResidents.Unbind(System, jobId, KingdomBindingKind.Transient, cause);
			if (GameObject.Validate(Body))
			{
				Spill(Body);
				Body.Obliterate();
			}
		}

		/// <summary>
		/// Everything on a body that the sweep's licence does not cover, put on the ground before
		/// the body goes. The protection law is not bent for our convenience: what is not
		/// <c>_stock</c>, or what answers <c>IsImportant()</c>, is dropped to the cell and never
		/// destroyed.
		/// </summary>
		private static void Spill(GameObject Body)
		{
			Cell at = Body.CurrentCell;
			List<GameObject> held = (Body.Inventory == null) ? null : Body.Inventory.GetObjects();
			for (int i = 0; held != null && i < held.Count; i++)
			{
				GameObject item = held[i];
				if (!GameObject.Validate(item))
				{
					continue;
				}
				if (item.HasProperty(StockProperty) && !item.HasPropertyOrTag(NoRestockProperty) && !item.IsImportant())
				{
					continue;
				}
				Body.Inventory.RemoveObject(item);
				if (at != null)
				{
					at.AddObject(item);
				}
			}
		}

		/// <summary>The legs: in by the edge to the store, back out by the same edge, and on into
		/// the ground the load came from when that ground is the city's own.</summary>
		private static bool TryPlan(KingdomSystem System, Zone Z, short entryX, short entryY, short destX, short destY, KingdomZoneStep edge, long TimeTicks, string sourceZoneId, out KingdomLeg[] legs, out int count, out KingdomCityFault fault)
		{
			int sinuosity = KingdomItineraryRules.SinuosityBuiltPercent;
			int road = Paved(Z) ? KingdomItineraryRules.RoadDiscountPercent : KingdomItineraryRules.NoRoadDiscountPercent;
			List<KingdomLegPlan> plans = new List<KingdomLegPlan>();
			plans.Add(new KingdomLegPlan(Z.ZoneID, entryX, entryY, destX, destY, sinuosity, road));
			plans.Add(new KingdomLegPlan(Z.ZoneID, destX, destY, entryX, entryY, sinuosity, road));
			if (!string.IsNullOrEmpty(sourceZoneId)
				&& !string.Equals(sourceZoneId, Z.ZoneID, StringComparison.Ordinal)
				&& System.ClaimedZones.Contains(sourceZoneId)
				&& edge != KingdomZoneStep.None)
			{
				// The onward leg is what makes following one across an edge work: the founder who
				// walks out behind a porter comes out beside them, because the model already had an
				// answer for where they would be (I5).
				short mirrorX;
				short mirrorY;
				KingdomJobRules.Mirror(entryX, entryY, edge, Z.Width, Z.Height, out mirrorX, out mirrorY);
				plans.Add(new KingdomLegPlan(sourceZoneId, mirrorX, mirrorY,
					(short)(Z.Width / 2), (short)(Z.Height / 2),
					KingdomItineraryRules.SinuosityOpenPercent, KingdomItineraryRules.NoRoadDiscountPercent));
			}
			count = plans.Count;
			return KingdomJobRules.TryBuildLegs(plans.ToArray(), count, TimeTicks, KingdomItineraryRules.WalkTicksPerCellDefault, out legs, out fault);
		}

		/// <summary>
		/// Whether a road is laid across this ground, and therefore whether the roads discount
		/// applies to its legs (&sect;3.10(3)).
		/// <para>
		/// Read off <c>KingdomRoads</c>'s own per-zone tally rather than re-derived, so laying a
		/// road and shortening an itinerary are the same fact rather than two that can disagree.
		/// The discount is applied identically here and to any later measured length, which is the
		/// clause that keeps a road from making the estimate and the measurement diverge.
		/// </para>
		/// </summary>
		private static bool Paved(Zone Z)
		{
			if (!KingdomRoads.Enabled)
			{
				return false;
			}
			List<KingdomRoadRules.WornCell> laid = KingdomRoads.ReadTally(Z);
			return laid != null && laid.Count > 0;
		}

		/// <summary>The larder with room that the city dedicated first. A stored fact and not a
		/// ranking recomputed from contents, so a reload picks the same one (&sect;3.9).</summary>
		private static GameObject NearestLarderWithRoom(KingdomSurvey Survey)
		{
			GameObject best = null;
			int bestOrdinal = int.MaxValue;
			for (int i = 0; i < Survey.Larders.Count; i++)
			{
				GameObject container = Survey.Larders[i];
				if (!GameObject.Validate(container) || container.Inventory == null || container.CurrentCell == null)
				{
					continue;
				}
				if (KingdomSurvey.CapacityOf(container) - KingdomSurvey.HeldIn(container) <= 0)
				{
					continue;
				}
				int ordinal = KingdomCityRules.DrainOrdinal(container.GetIntProperty(KingdomCity.DedicationOrderProperty));
				if (ordinal < bestOrdinal)
				{
					bestOrdinal = ordinal;
					best = container;
				}
			}
			return best;
		}

		private static GameObject LarderAt(Cell at)
		{
			if (at == null)
			{
				return null;
			}
			return at.GetFirstObjectWithPart("Inventory", delegate(GameObject candidate)
			{
				return GameObject.Validate(candidate) && candidate.GetIntProperty(KingdomAdopt.LarderProperty) == 1;
			});
		}

		/// <summary>The real crop, minted onto a real back and marked as the simulation's own
		/// (&sect;3.2(a)). Refuses a blueprint that is not food for the same reason
		/// <c>KingdomSurvey.StoreFood</c> does: an unbounded spawn of a thing nothing counts.</summary>
		private static int Load(GameObject Body, string Blueprint, int Amount)
		{
			if (!GameObject.Validate(Body) || Body.Inventory == null || string.IsNullOrEmpty(Blueprint) || Amount <= 0)
			{
				return 0;
			}
			int carried = 0;
			for (int i = 0; i < Amount; i++)
			{
				GameObject food = GameObject.Create(Blueprint);
				if (food == null)
				{
					break;
				}
				if (!food.HasPart("Food") && !food.HasPart("PreparedCookingIngredient"))
				{
					food.Obliterate();
					break;
				}
				food.SetIntProperty(StockProperty, 1);
				Body.Inventory.AddObject(food, Silent: true);
				carried++;
			}
			return carried;
		}

		/// <summary>The anchor discipline &sect;3.2(b) rides: a carrier that wanders is a carrier
		/// vanilla will not walk back to anything.</summary>
		private static void Settle(GameObject Body)
		{
			Brain brain = Body.Brain;
			if (brain == null)
			{
				return;
			}
			brain.Wanders = false;
			brain.WandersRandomly = false;
			brain.Hostile = false;
		}

		private static void Walk(GameObject Body, Zone Z, int x, int y)
		{
			Cell target = Z.GetCell(x, y);
			Brain brain = Body.Brain;
			if (target == null || brain == null)
			{
				return;
			}
			brain.Stay(target);
			brain.PushGoal(new MoveTo(target, careful: true));
		}

		private static bool Near(GameObject Body, int x, int y)
		{
			Cell at = Body.CurrentCell;
			if (at == null)
			{
				return false;
			}
			int dx = at.X - x;
			int dy = at.Y - y;
			if (dx < 0) { dx = -dx; }
			if (dy < 0) { dy = -dy; }
			return dx <= 1 && dy <= 1;
		}

		private static Cell Standing(Zone Z, short x, short y)
		{
			Cell at = Z.GetCell(x, y);
			if (at != null && at.IsPassable() && at.IsEmptyOfSolid())
			{
				return at;
			}
			List<Cell> open = Z.GetEmptyCells(delegate(Cell c) { return c.IsPassable(); });
			for (int i = 0; open != null && i < open.Count; i++)
			{
				// Stable and undrawn: the first passable cell nearest the drawn one, scanned in the
				// zone's own order, so two runs of the same delivery put the carrier in the same
				// place.
				if (Near(open[i], x, y))
				{
					return open[i];
				}
			}
			return (open != null && open.Count > 0) ? open[0] : null;
		}

		private static bool Near(Cell at, int x, int y)
		{
			int dx = at.X - x;
			int dy = at.Y - y;
			if (dx < 0) { dx = -dx; }
			if (dy < 0) { dy = -dy; }
			return dx <= 2 && dy <= 2;
		}

		/// <summary>The carrier for this job, if their ground is where the founder is standing.
		/// A body whose zone is on disk does not resolve, and that is the whole point: the sweep
		/// deals with those and a close does not reach into a frozen zone.</summary>
		private static GameObject Resolve(int jobId)
		{
			Zone zone = (The.Player == null) ? null : The.Player.CurrentZone;
			List<GameObject> found = (zone == null) ? null : zone.GetObjects();
			for (int i = 0; found != null && i < found.Count; i++)
			{
				if (GameObject.Validate(found[i]) && found[i].GetIntProperty(KingdomResidents.JobIdProperty) == jobId)
				{
					return found[i];
				}
			}
			return null;
		}

		/// <summary>
		/// The settlement id every draw about this city's deliveries hangs off.
		/// <para>
		/// <c>KingdomChronicle.SettlementId</c>'s own folding, and not a second one: a semantic id
		/// has a grammar (<c>KernelSemanticId.IsValid</c>) and a second encoder is a second chance
		/// to produce a string the kernel refuses. Keyed on the SEAT's name rather than the realm's,
		/// so a realm's two cities domain-separate and one city's deliveries cannot draw the other's
		/// carriers.
		/// </para>
		/// </summary>
		private static string SeedLabel(KingdomSystem System)
		{
			string name = System.SeatName;
			return KingdomChronicle.SettlementId(string.IsNullOrEmpty(name) ? System.KingdomFactionName : name);
		}

		private static void Refuse(string step, KingdomCityFault fault)
		{
			KingdomLog.Log("porter: " + step + " refused (" + fault + "); nothing was minted");
		}
	}
}
