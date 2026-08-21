using System;
using System.Collections.Generic;

using ThousandAndFirst.Simulation.Kernel;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What a carrier is out doing. LIVING-CITY-ARCHITECTURE &sect;3.7 names three &mdash;
	/// deliveries, repair crews and messengers &mdash; and rules that they are <i>"the same four
	/// steps with a different <c>DelegateGoal</c>"</i>.
	/// <para>
	/// <b>W3 ships exactly one.</b> &sect;7.4 gives W3 the itinerary, the matrix and the roads
	/// discount, and gives W6 <i>"nearest-holder sourcing and capacity-bound batching &hellip;
	/// because both only bite once many jobs compete over many holders"</i>. Minting a second and
	/// third kind before there is a planner to arbitrate between them would be the empty room
	/// &sect;7.4 says not to optimise. The enum names the one that ships and nothing else, so a
	/// later wave adds a member rather than reinterpreting one.
	/// </para>
	/// </summary>
	public enum KingdomJobKind : byte
	{
		/// <summary>Not a job.</summary>
		None = 0,

		/// <summary>A load of the city's own stock, carried to a store the founder is standing
		/// beside. Addendum 12(c)'s canonical image, and G2's pending harvest is the flow that
		/// already had the model credit this renders.</summary>
		Delivery = 1
	}

	/// <summary>Where a job stands. LIVING-CITY-ARCHITECTURE &sect;3.7.</summary>
	public enum KingdomJobStatus : byte
	{
		/// <summary>Running. Exactly the jobs the registry holds a row for.</summary>
		Open = 0,

		/// <summary>The cargo reached the store. The row closes and its binding is evicted.</summary>
		Delivered = 1,

		/// <summary>The job outlived twice its projected duration, or its carrier died. Told, never
		/// silently dropped.</summary>
		Failed = 2
	}

	/// <summary>
	/// One waypoint pair the planner turns into a leg: which ground, in by which cell, out by which
	/// cell, and how sinuous and how paved the ground between them is.
	/// </summary>
	internal readonly struct KingdomLegPlan
	{
		internal readonly string ZoneId;

		internal readonly short EnterX;

		internal readonly short EnterY;

		internal readonly short ExitX;

		internal readonly short ExitY;

		/// <summary>From <c>KingdomItineraryRules.SinuosityOpenPercent</c> or
		/// <c>SinuosityBuiltPercent</c>, by district.</summary>
		internal readonly int SinuosityPercent;

		/// <summary><c>KingdomItineraryRules.RoadDiscountPercent</c> where a road is laid along
		/// this leg, <c>NoRoadDiscountPercent</c> where none is.</summary>
		internal readonly int RoadDiscountPercent;

		internal KingdomLegPlan(string zoneId, short enterX, short enterY, short exitX, short exitY, int sinuosityPercent, int roadDiscountPercent)
		{
			ZoneId = zoneId;
			EnterX = enterX;
			EnterY = enterY;
			ExitX = exitX;
			ExitY = exitY;
			SinuosityPercent = sinuosityPercent;
			RoadDiscountPercent = roadDiscountPercent;
		}
	}

	/// <summary>
	/// One job and the timed itinerary that answers for it.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.7: <b>a job is a timed itinerary, computed once, at
	/// creation.</b> From this row one pure function answers where the carrier is and what is on
	/// them at any tick, and every zone renders that same answer &mdash; which is invariant I5, and
	/// which is why the body never has to literally traverse anything.
	/// </para>
	/// </summary>
	internal readonly struct KingdomJobRow
	{
		internal readonly int JobId;

		internal readonly KingdomJobKind Kind;

		internal readonly KingdomStockKind Cargo;

		internal readonly int CargoAmount;

		/// <summary>Where the load came from, for the edge it enters by and for the register. Never
		/// traversed as a leg: a load already in flight is not re-walked from its field.</summary>
		internal readonly string SourceZoneId;

		internal readonly string DestZoneId;

		internal readonly long StartTick;

		/// <summary>The carrier's real per-cell tick cost. At Speed 100 an actor covers exactly one
		/// cell per tick, so a founder walking beside a porter neither outpaces them nor falls
		/// behind (&sect;3.7).</summary>
		internal readonly int WalkTicksPerCell;

		internal readonly KingdomJobStatus Status;

		/// <summary>The carrier's origin, from <c>KingdomRules.Origins</c>, drawn once on the
		/// delivery lane so the same delivery yields the same carrier.</summary>
		internal readonly int OriginCode;

		/// <summary>The leg at whose END the cargo lands. Everything before it is carried;
		/// everything after it is the walk home.</summary>
		internal readonly int DepositLegIndex;

		private readonly KingdomLeg[] legs;

		private readonly int legCount;

		internal KingdomJobRow(
			int jobId,
			KingdomJobKind kind,
			KingdomStockKind cargo,
			int cargoAmount,
			string sourceZoneId,
			string destZoneId,
			long startTick,
			int walkTicksPerCell,
			KingdomJobStatus status,
			int originCode,
			int depositLegIndex,
			KingdomLeg[] legs,
			int legCount)
		{
			JobId = jobId;
			Kind = kind;
			Cargo = cargo;
			CargoAmount = cargoAmount;
			SourceZoneId = sourceZoneId;
			DestZoneId = destZoneId;
			StartTick = startTick;
			WalkTicksPerCell = walkTicksPerCell;
			Status = status;
			OriginCode = originCode;
			DepositLegIndex = depositLegIndex;
			this.legs = legs;
			this.legCount = legCount;
		}

		internal int LegCount
		{
			get { return legCount; }
		}

		internal bool TryLeg(int index, out KingdomLeg leg)
		{
			leg = default(KingdomLeg);
			if (legs == null || index < 0 || index >= legCount || index >= legs.Length)
			{
				return false;
			}
			leg = legs[index];
			return true;
		}

		/// <summary>The legs as the itinerary rules take them. A copy, because a row that handed out
		/// its own array would be a frozen row anybody could edit.</summary>
		internal KingdomLeg[] Legs()
		{
			KingdomLeg[] copy = new KingdomLeg[legCount];
			for (int i = 0; i < legCount && legs != null && i < legs.Length; i++)
			{
				copy[i] = legs[i];
			}
			return copy;
		}

		internal KingdomJobRow WithStatus(KingdomJobStatus status)
		{
			return new KingdomJobRow(JobId, Kind, Cargo, CargoAmount, SourceZoneId, DestZoneId, StartTick,
				WalkTicksPerCell, status, OriginCode, DepositLegIndex, legs, legCount);
		}

		internal KingdomJobRow WithLegs(KingdomLeg[] next, int count)
		{
			return new KingdomJobRow(JobId, Kind, Cargo, CargoAmount, SourceZoneId, DestZoneId, StartTick,
				WalkTicksPerCell, Status, OriginCode, DepositLegIndex, next, count);
		}

		/// <summary>
		/// The same job carrying more.
		/// <para>
		/// W6, LIVING-CITY-ARCHITECTURE &sect;3.10(4): capacity-bound batching is not a second
		/// planner bolted on beside the itinerary — it is a load added to a trip that is already
		/// running to the same store. The legs are untouched, because the route did not change;
		/// only what is on the carrier's back did.
		/// </para>
		/// </summary>
		internal KingdomJobRow WithCargo(int cargoAmount)
		{
			return new KingdomJobRow(JobId, Kind, Cargo, cargoAmount, SourceZoneId, DestZoneId, StartTick,
				WalkTicksPerCell, Status, OriginCode, DepositLegIndex, legs, LegCount);
		}

		/// <summary>The cargo has landed once the deposit leg has been finished. Copy-on-write, so
		/// the amount on the row is what LEFT and this is what is still on the carrier's back.</summary>
		internal KingdomJobRow WithCargoLanded()
		{
			return new KingdomJobRow(JobId, Kind, Cargo, 0, SourceZoneId, DestZoneId, StartTick,
				WalkTicksPerCell, Status, OriginCode, DepositLegIndex, legs, legCount);
		}
	}

	/// <summary>
	/// The pure half of the porter: planning an itinerary, reading a carrier's cargo off it, and
	/// the two draws a delivery is allowed.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.7 and &sect;3.10. Engine-free and total. <b>No draw
	/// anywhere in the routing</b> &mdash; routing is arithmetic, not chance (&sect;3.10(4),
	/// <c>KingdomBudgetRules.PlannerMaxDraws</c> is zero) &mdash; and the two draws that do exist
	/// are flavour: which edge cell the carrier walks in by, and where they say they are from.
	/// </para>
	/// </summary>
	internal static class KingdomJobRules
	{
		/// <summary>LIVING-CITY-ARCHITECTURE &sect;3.8: sixteen open jobs, realm-wide.</summary>
		internal const int MaxOpenJobs = KingdomCityMemoryRules.MaxOpenJobs;

		/// <summary>LIVING-CITY-ARCHITECTURE &sect;3.7, the delivery lane. Frozen at creation with
		/// the rules version, so the same delivery yields the same carrier whether the founder
		/// watches it or reads about it afterwards.</summary>
		internal const string DeliveryStreamId = "taf:stream:delivery";

		internal const uint DeliveryKindCode = 1u;

		/// <summary>Which cell along the entry edge the carrier walks in by.</summary>
		internal const uint EntryCellDrawIndex = 0u;

		/// <summary>Where the carrier says they are from.</summary>
		internal const uint OriginDrawIndex = 1u;

		/// <summary>
		/// LIVING-CITY-ARCHITECTURE &sect;3.7: <i>"a porter is two units"</i> &mdash; one body mint
		/// and one container fill, both out of the ordinary eight-unit budget.
		/// </summary>
		internal const int PorterUnits = 2;

		/// <summary>
		/// Turns a run of waypoints into a dated itinerary.
		/// <para>
		/// Each leg's length is <c>Chebyshev &times; Sinuosity &times; RoadDiscount</c> in integer
		/// percent, with <b>zero zone access</b> &mdash; that is &sect;3.7's absolute cost bound,
		/// and the reason the estimate is a prior that reality corrects rather than a pathfind.
		/// A leg of zero cells still costs one tick, because a carrier that arrives on the tick it
		/// departs has not walked.
		/// </para>
		/// </summary>
		internal static bool TryBuildLegs(KingdomLegPlan[] plans, int count, long startTick, int walkTicksPerCell, out KingdomLeg[] legs, out KingdomCityFault fault)
		{
			legs = null;
			if (plans == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > plans.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (count > KingdomItineraryRules.MaxLegs)
			{
				// §3.7: a job that wants more than six legs is refused at planning and told. It is
				// never truncated, because a truncated route is a carrier arriving somewhere else.
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			if (startTick < 0L || walkTicksPerCell <= 0)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			KingdomLeg[] built = new KingdomLeg[count];
			long depart = startTick;
			for (int i = 0; i < count; i++)
			{
				KingdomLegPlan plan = plans[i];
				if (string.IsNullOrEmpty(plan.ZoneId))
				{
					fault = KingdomCityFault.NullArgument;
					return false;
				}
				int chebyshev;
				if (!KingdomItineraryRules.TryChebyshev(plan.EnterX, plan.EnterY, plan.ExitX, plan.ExitY, out chebyshev, out fault))
				{
					return false;
				}
				int length;
				if (!KingdomItineraryRules.TryEstimatePathLength(chebyshev, plan.SinuosityPercent, plan.RoadDiscountPercent, out length, out fault))
				{
					return false;
				}
				long walk = (long)length * walkTicksPerCell;
				if (walk < 1L)
				{
					walk = 1L;
				}
				long arrive = depart + walk;
				if (arrive < depart)
				{
					fault = KingdomCityFault.ArithmeticOverflow;
					return false;
				}
				built[i] = new KingdomLeg(plan.ZoneId, plan.EnterX, plan.EnterY, plan.ExitX, plan.ExitY, length, depart, arrive);
				depart = arrive;
			}
			if (!KingdomItineraryRules.TryValidate(built, count, out fault))
			{
				return false;
			}
			legs = built;
			return true;
		}

		/// <summary>
		/// What is still on the carrier's back at this fix.
		/// <para>
		/// The one-event-two-renderings invariant in arithmetic: the cargo is on them until the
		/// deposit leg is finished and gone afterwards. <b>The stores were credited at the dated
		/// tick either way</b> &mdash; the porter is carrying goods that are already the city's, so
		/// this figure is what a zone DRAWS, never what the city OWNS.
		/// </para>
		/// </summary>
		internal static int CargoAt(KingdomJobRow job, KingdomItineraryFix fix)
		{
			if (job.CargoAmount <= 0)
			{
				return 0;
			}
			if (fix.Phase == KingdomItineraryPhase.Delivered)
			{
				return 0;
			}
			if (fix.Phase == KingdomItineraryPhase.Pending)
			{
				return job.CargoAmount;
			}
			// Handoff reports the PREVIOUS leg's exit, so a handoff at the deposit leg's end is a
			// carrier who has just put the load down.
			if (fix.LegIndex > job.DepositLegIndex)
			{
				return 0;
			}
			if (fix.LegIndex == job.DepositLegIndex && fix.Phase == KingdomItineraryPhase.Handoff)
			{
				return 0;
			}
			return job.CargoAmount;
		}

		/// <summary>Whether the carrier has finished the leg the load lands at the end of.</summary>
		internal static bool Deposited(KingdomJobRow job, long nowTick)
		{
			KingdomLeg leg;
			if (!job.TryLeg(job.DepositLegIndex, out leg))
			{
				return false;
			}
			return nowTick >= leg.ArriveTick;
		}

		/// <summary>The key every draw about one delivery hangs off. <c>rulesVersion</c> frozen at
		/// creation, the settlement's id, the delivery lane, and the job id as the occurrence
		/// ordinal (&sect;2.4).</summary>
		internal static bool TryKey(string settlementId, int jobId, out SemanticEventKey key, out KingdomCityFault fault)
		{
			key = default(SemanticEventKey);
			if (jobId <= 0)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			KernelFaultCode kernelFault;
			if (!SemanticEventKey.TryCreate(KingdomCityRules.RulesVersion, settlementId, DeliveryStreamId, DeliveryKindCode, (ulong)jobId, out key, out kernelFault))
			{
				fault = KingdomCityFaults.FromKernel(kernelFault);
				return false;
			}
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// Which cell along an edge the carrier walks in by, drawn on the delivery lane.
		/// <para>
		/// <b>The edge itself is not drawn</b> &mdash; it is the one facing the source, which is a
		/// fact rather than a choice, and a fact cannot disagree with where the founder comes out.
		/// What is drawn is where along that edge, which is flavour and is therefore allowed one.
		/// </para>
		/// </summary>
		internal static bool TryDrawEntryCell(KernelSeed128 seed, string settlementId, int jobId, KingdomZoneStep edge, int width, int height, out short x, out short y, out KingdomCityFault fault)
		{
			x = 0;
			y = 0;
			if (width <= 2 || height <= 2)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			SemanticEventKey key;
			if (!TryKey(settlementId, jobId, out key, out fault))
			{
				return false;
			}
			bool vertical = (edge == KingdomZoneStep.North || edge == KingdomZoneStep.South);
			int span = vertical ? width : height;
			ulong along;
			KernelFaultCode kernelFault;
			if (!CounterRandom.TryDrawBelow(seed, key, EntryCellDrawIndex, (ulong)(span - 2), out along, out kernelFault))
			{
				fault = KingdomCityFaults.FromKernel(kernelFault);
				return false;
			}
			int offset = (int)along + 1;
			switch (edge)
			{
			case KingdomZoneStep.North:
				x = (short)offset;
				y = 0;
				break;
			case KingdomZoneStep.South:
				x = (short)offset;
				y = (short)(height - 1);
				break;
			case KingdomZoneStep.West:
				x = 0;
				y = (short)offset;
				break;
			default:
				// East, and anything this build has no word for: a carrier with no named edge still
				// has to come in somewhere, and the east wall is a wall like any other.
				x = (short)(width - 1);
				y = (short)offset;
				break;
			}
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>A vanilla zone's own dimensions, for the edge arithmetic. Read off the live
		/// zone wherever one is in hand; these are what a zone that will not answer is taken to be.
		/// </summary>
		internal const int ZoneWidth = 80;

		internal const int ZoneHeight = 25;

		/// <summary>
		/// The cell the engine's own zone connection maps an exit cell to. Not a choice, so it
		/// needs no draw and cannot disagree with where the founder comes out (&sect;3.7).
		/// </summary>
		internal static void Mirror(short x, short y, KingdomZoneStep edge, int width, int height, out short mirrorX, out short mirrorY)
		{
			int w = (width > 2) ? width : ZoneWidth;
			int h = (height > 2) ? height : ZoneHeight;
			switch (edge)
			{
			case KingdomZoneStep.North:
				mirrorX = x;
				mirrorY = (short)(h - 1);
				return;
			case KingdomZoneStep.South:
				mirrorX = x;
				mirrorY = 0;
				return;
			case KingdomZoneStep.West:
				mirrorX = (short)(w - 1);
				mirrorY = y;
				return;
			default:
				mirrorX = 0;
				mirrorY = y;
				return;
			}
		}

		/// <summary>Which edge of this zone faces the ground the load came from. A fact from the
		/// two zone ids, never a draw &mdash; and <see cref="KingdomZoneStep.West"/> for a source
		/// this build cannot place, because a carrier still has to come in somewhere.</summary>
		internal static KingdomZoneStep EdgeToward(string here, string source)
		{
			string world;
			int hx;
			int hy;
			int hz;
			string otherWorld;
			int sx;
			int sy;
			int sz;
			if (string.IsNullOrEmpty(source)
				|| !KingdomRules.TryParseZoneID(here, out world, out hx, out hy, out hz)
				|| !KingdomRules.TryParseZoneID(source, out otherWorld, out sx, out sy, out sz)
				|| !string.Equals(world, otherWorld, StringComparison.Ordinal))
			{
				return KingdomZoneStep.West;
			}
			KingdomZoneStep step = KingdomDistanceRules.StepBetween(
				new KingdomZoneNode(here, hx, hy, hz),
				new KingdomZoneNode(source, sx, sy, sz));
			if (step != KingdomZoneStep.None && step != KingdomZoneStep.Up && step != KingdomZoneStep.Down)
			{
				return step;
			}
			// Not a neighbour, or straight up or down: a stairwell is not an edge cell, so the load
			// comes in by whichever wall lies toward it.
			int dx = sx - hx;
			int dy = sy - hy;
			if (dx == 0 && dy == 0)
			{
				return KingdomZoneStep.West;
			}
			int adx = (dx < 0) ? -dx : dx;
			int ady = (dy < 0) ? -dy : dy;
			if (adx >= ady)
			{
				return (dx > 0) ? KingdomZoneStep.East : KingdomZoneStep.West;
			}
			return (dy > 0) ? KingdomZoneStep.South : KingdomZoneStep.North;
		}

		/// <summary>Where the carrier says they are from, drawn on the same key at its own draw
		/// index &mdash; so adding or removing this draw cannot perturb the entry cell.</summary>
		internal static bool TryDrawOrigin(KernelSeed128 seed, string settlementId, int jobId, int originCount, out int originCode, out KingdomCityFault fault)
		{
			originCode = KingdomResidentRules.NoOrigin;
			if (originCount <= 0)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			SemanticEventKey key;
			if (!TryKey(settlementId, jobId, out key, out fault))
			{
				return false;
			}
			ulong drawn;
			KernelFaultCode kernelFault;
			if (!CounterRandom.TryDrawBelow(seed, key, OriginDrawIndex, (ulong)originCount, out drawn, out kernelFault))
			{
				fault = KingdomCityFaults.FromKernel(kernelFault);
				return false;
			}
			originCode = (int)drawn + 1;
			fault = KingdomCityFault.None;
			return true;
		}
	}

	/// <summary>
	/// The open jobs as the rules layer works on them: frozen, total, copy-on-write.
	/// <para>
	/// <b>Realm-scope, beside the binding registry rather than inside a city book</b>, and the
	/// reason is &sect;3.8's own: a carrier's legs can cross into the other city's ground or off the
	/// map, and a job a city carried would be lost on a seat swap exactly as a binding would.
	/// LIVING-CITY-ARCHITECTURE &sect;0.0(c) already prices the job rows <b>realm-wide</b> and
	/// &sect;3.8 caps them <b>per realm</b>, so this is where the budget already puts them.
	/// </para>
	/// <para>
	/// A closed job is evicted at once, so absence from this table is proof of closure &mdash; the
	/// same rule the binding registry keeps, for the same reason: there is no second list to fall
	/// out of step with the first.
	/// </para>
	/// </summary>
	internal sealed class KingdomJobTable
	{
		private readonly KingdomJobRow[] rows;

		private KingdomJobTable(KingdomJobRow[] rows)
		{
			this.rows = rows;
		}

		internal int Count
		{
			get { return rows.Length; }
		}

		internal static bool TryCreate(KingdomJobRow[] source, out KingdomJobTable table, out KingdomCityFault fault)
		{
			table = null;
			if (source == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (source.Length > KingdomJobRules.MaxOpenJobs)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			KingdomJobRow[] kept = new KingdomJobRow[source.Length];
			for (int i = 0; i < source.Length; i++)
			{
				if (source[i].JobId <= 0)
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				for (int j = 0; j < i; j++)
				{
					if (kept[j].JobId == source[i].JobId)
					{
						fault = KingdomCityFault.DuplicateBinding;
						return false;
					}
				}
				kept[i] = source[i];
			}
			table = new KingdomJobTable(kept);
			fault = KingdomCityFault.None;
			return true;
		}

		internal bool TryAt(int index, out KingdomJobRow row)
		{
			row = default(KingdomJobRow);
			if (index < 0 || index >= rows.Length)
			{
				return false;
			}
			row = rows[index];
			return true;
		}

		internal bool TryGet(int jobId, out KingdomJobRow row)
		{
			row = default(KingdomJobRow);
			for (int i = 0; i < rows.Length; i++)
			{
				if (rows[i].JobId == jobId)
				{
					row = rows[i];
					return true;
				}
			}
			return false;
		}

		internal bool Holds(int jobId)
		{
			KingdomJobRow row;
			return TryGet(jobId, out row);
		}

		/// <summary>Opens a job. Refuses a duplicate id and refuses past the cap; publishes nothing
		/// on either, so the caller's table stays byte-identical.</summary>
		internal bool TryOpen(KingdomJobRow row, out KingdomJobTable next, out KingdomCityFault fault)
		{
			next = null;
			if (row.JobId <= 0 || row.Kind == KingdomJobKind.None)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (Holds(row.JobId))
			{
				fault = KingdomCityFault.DuplicateBinding;
				return false;
			}
			if (rows.Length >= KingdomJobRules.MaxOpenJobs)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			KingdomJobRow[] grown = new KingdomJobRow[rows.Length + 1];
			Array.Copy(rows, grown, rows.Length);
			grown[rows.Length] = row;
			return TryCreate(grown, out next, out fault);
		}

		/// <summary>Rewrites one job's row in place of the old one &mdash; a re-projection, a
		/// landed cargo, a status.</summary>
		internal bool TryReplace(KingdomJobRow row, out KingdomJobTable next, out KingdomCityFault fault)
		{
			next = null;
			if (!Holds(row.JobId))
			{
				fault = KingdomCityFault.UnknownBinding;
				return false;
			}
			KingdomJobRow[] rewritten = new KingdomJobRow[rows.Length];
			for (int i = 0; i < rows.Length; i++)
			{
				rewritten[i] = (rows[i].JobId == row.JobId) ? row : rows[i];
			}
			return TryCreate(rewritten, out next, out fault);
		}

		/// <summary>Evicts a job. There is no closed list: the eviction IS the closure.</summary>
		internal bool TryClose(int jobId, out KingdomJobTable next, out KingdomJobRow closed, out KingdomCityFault fault)
		{
			next = null;
			closed = default(KingdomJobRow);
			if (!TryGet(jobId, out closed))
			{
				fault = KingdomCityFault.UnknownBinding;
				return false;
			}
			KingdomJobRow[] shrunk = new KingdomJobRow[rows.Length - 1];
			int at = 0;
			for (int i = 0; i < rows.Length; i++)
			{
				if (rows[i].JobId != jobId)
				{
					shrunk[at++] = rows[i];
				}
			}
			return TryCreate(shrunk, out next, out fault);
		}

		/// <summary>Every open job's id, oldest first. The order the pump renders them in, and
		/// stable so a save and reload resumes in exactly the same place.</summary>
		internal int[] OpenIds()
		{
			int[] ids = new int[rows.Length];
			for (int i = 0; i < rows.Length; i++)
			{
				ids[i] = rows[i].JobId;
			}
			return ids;
		}
	}

	/// <summary>
	/// The realm's open jobs, in the shape a save can hold them.
	/// <para>
	/// The same carrier/rules pairing <see cref="KingdomBindingRegistry"/> has, for the same reason
	/// (&sect;1.3): a named-field reader must assign fields and the rules layer must not. Legs are
	/// flattened into their own columns and each job says how many of them are its own, because a
	/// jagged list of lists is not something a named-field writer can hold.
	/// </para>
	/// </summary>
	[Serializable]
	public class KingdomJobRegistry
#if !TAF_TESTS
		: IComposite
#endif
	{
		/// <summary>The realm's job id counter. Never reused, never drawn: a job id is the ordinal
		/// its delivery's draws hang off (&sect;2.4), and a seeded id would make which carrier walks
		/// in depend on how many other things had been rolled first.</summary>
		public int JobCounter;

		public List<int> JobIds = new List<int>();

		public List<int> Kinds = new List<int>();

		public List<int> Cargos = new List<int>();

		public List<int> CargoAmounts = new List<int>();

		public List<string> SourceZoneIds = new List<string>();

		public List<string> DestZoneIds = new List<string>();

		public List<long> StartTicks = new List<long>();

		public List<int> WalkTicksPerCell = new List<int>();

		public List<int> Statuses = new List<int>();

		public List<int> OriginCodes = new List<int>();

		public List<int> DepositLegIndexes = new List<int>();

		public List<int> LegCounts = new List<int>();

		// ---- Legs, flattened in job order -----------------------------------------------------

		public List<string> LegZoneIds = new List<string>();

		public List<int> LegEnterX = new List<int>();

		public List<int> LegEnterY = new List<int>();

		public List<int> LegExitX = new List<int>();

		public List<int> LegExitY = new List<int>();

		public List<int> LegLengths = new List<int>();

		public List<long> LegDepartTicks = new List<long>();

		public List<long> LegArriveTicks = new List<long>();

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomJobRegistry));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomJobRegistry));
			Normalize();
		}
#endif

		public int Count => JobIds.Count;

		/// <summary>
		/// Repairs a registry read from a save written by an older build. Null columns become
		/// empty; ragged columns are truncated to the shortest; a duplicate or zero job id is
		/// dropped; a job whose declared legs are not all present is dropped whole, because half an
		/// itinerary is a carrier with no answer to where they are.
		/// </summary>
		public void Normalize()
		{
			JobIds = Repair(JobIds);
			Kinds = Repair(Kinds);
			Cargos = Repair(Cargos);
			CargoAmounts = Repair(CargoAmounts);
			SourceZoneIds = Repair(SourceZoneIds);
			DestZoneIds = Repair(DestZoneIds);
			StartTicks = Repair(StartTicks);
			WalkTicksPerCell = Repair(WalkTicksPerCell);
			Statuses = Repair(Statuses);
			OriginCodes = Repair(OriginCodes);
			DepositLegIndexes = Repair(DepositLegIndexes);
			LegCounts = Repair(LegCounts);
			LegZoneIds = Repair(LegZoneIds);
			LegEnterX = Repair(LegEnterX);
			LegEnterY = Repair(LegEnterY);
			LegExitX = Repair(LegExitX);
			LegExitY = Repair(LegExitY);
			LegLengths = Repair(LegLengths);
			LegDepartTicks = Repair(LegDepartTicks);
			LegArriveTicks = Repair(LegArriveTicks);
			if (JobCounter < 0)
			{
				JobCounter = 0;
			}

			int jobs = Shortest(new int[]
			{
				JobIds.Count, Kinds.Count, Cargos.Count, CargoAmounts.Count, SourceZoneIds.Count,
				DestZoneIds.Count, StartTicks.Count, WalkTicksPerCell.Count, Statuses.Count,
				OriginCodes.Count, DepositLegIndexes.Count, LegCounts.Count
			});
			Trim(JobIds, jobs);
			Trim(Kinds, jobs);
			Trim(Cargos, jobs);
			Trim(CargoAmounts, jobs);
			Trim(SourceZoneIds, jobs);
			Trim(DestZoneIds, jobs);
			Trim(StartTicks, jobs);
			Trim(WalkTicksPerCell, jobs);
			Trim(Statuses, jobs);
			Trim(OriginCodes, jobs);
			Trim(DepositLegIndexes, jobs);
			Trim(LegCounts, jobs);

			int legs = Shortest(new int[]
			{
				LegZoneIds.Count, LegEnterX.Count, LegEnterY.Count, LegExitX.Count, LegExitY.Count,
				LegLengths.Count, LegDepartTicks.Count, LegArriveTicks.Count
			});
			Trim(LegZoneIds, legs);
			Trim(LegEnterX, legs);
			Trim(LegEnterY, legs);
			Trim(LegExitX, legs);
			Trim(LegExitY, legs);
			Trim(LegLengths, legs);
			Trim(LegDepartTicks, legs);
			Trim(LegArriveTicks, legs);

			int consumed = 0;
			for (int i = 0; i < JobIds.Count; i++)
			{
				int count = LegCounts[i];
				if (count < 0 || count > KingdomItineraryRules.MaxLegs)
				{
					count = 0;
					LegCounts[i] = 0;
				}
				bool broken = JobIds[i] <= 0 || Duplicated(i) || consumed + count > legs;
				if (!broken)
				{
					if (SourceZoneIds[i] == null) { SourceZoneIds[i] = ""; }
					if (DestZoneIds[i] == null) { DestZoneIds[i] = ""; }
					if (StartTicks[i] < 0L) { StartTicks[i] = 0L; }
					if (WalkTicksPerCell[i] <= 0) { WalkTicksPerCell[i] = KingdomItineraryRules.WalkTicksPerCellDefault; }
					if (DepositLegIndexes[i] < 0) { DepositLegIndexes[i] = 0; }
					consumed += count;
					continue;
				}
				RemoveLegs(consumed, count);
				RemoveJob(i);
				legs -= count;
				i--;
			}
			// Legs past the last job's share belong to no job and are dropped rather than kept for
			// a job that might be added later: an itinerary nobody claims is not an itinerary.
			if (LegZoneIds.Count > consumed)
			{
				RemoveLegs(consumed, LegZoneIds.Count - consumed);
			}
			DropOverCap();
		}

		/// <summary>The registry as the frozen table the rules layer works on.</summary>
		internal bool TryRead(out KingdomJobTable table, out KingdomCityFault fault)
		{
			Normalize();
			KingdomJobRow[] rows = new KingdomJobRow[JobIds.Count];
			int at = 0;
			for (int i = 0; i < rows.Length; i++)
			{
				int count = LegCounts[i];
				KingdomLeg[] legs = new KingdomLeg[count];
				for (int j = 0; j < count; j++)
				{
					legs[j] = new KingdomLeg(
						LegZoneIds[at + j] ?? "",
						(short)LegEnterX[at + j], (short)LegEnterY[at + j],
						(short)LegExitX[at + j], (short)LegExitY[at + j],
						LegLengths[at + j], LegDepartTicks[at + j], LegArriveTicks[at + j]);
				}
				at += count;
				rows[i] = new KingdomJobRow(
					JobIds[i],
					KindOf(Kinds[i]),
					CargoOf(Cargos[i]),
					CargoAmounts[i],
					SourceZoneIds[i],
					DestZoneIds[i],
					StartTicks[i],
					WalkTicksPerCell[i],
					StatusOf(Statuses[i]),
					OriginCodes[i],
					DepositLegIndexes[i],
					legs,
					count);
			}
			return KingdomJobTable.TryCreate(rows, out table, out fault);
		}

		/// <summary>Writes one frozen table into the columns, in one call and after the rules have
		/// succeeded. The single publisher &sect;1.3 requires.</summary>
		internal bool TryPublish(KingdomJobTable table, out KingdomCityFault fault)
		{
			if (table == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			JobIds.Clear(); Kinds.Clear(); Cargos.Clear(); CargoAmounts.Clear();
			SourceZoneIds.Clear(); DestZoneIds.Clear(); StartTicks.Clear(); WalkTicksPerCell.Clear();
			Statuses.Clear(); OriginCodes.Clear(); DepositLegIndexes.Clear(); LegCounts.Clear();
			LegZoneIds.Clear(); LegEnterX.Clear(); LegEnterY.Clear(); LegExitX.Clear();
			LegExitY.Clear(); LegLengths.Clear(); LegDepartTicks.Clear(); LegArriveTicks.Clear();
			for (int i = 0; i < table.Count; i++)
			{
				KingdomJobRow row;
				if (!table.TryAt(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				JobIds.Add(row.JobId);
				Kinds.Add((int)row.Kind);
				Cargos.Add((int)row.Cargo);
				CargoAmounts.Add(row.CargoAmount);
				SourceZoneIds.Add(row.SourceZoneId ?? "");
				DestZoneIds.Add(row.DestZoneId ?? "");
				StartTicks.Add(row.StartTick);
				WalkTicksPerCell.Add(row.WalkTicksPerCell);
				Statuses.Add((int)row.Status);
				OriginCodes.Add(row.OriginCode);
				DepositLegIndexes.Add(row.DepositLegIndex);
				LegCounts.Add(row.LegCount);
				for (int j = 0; j < row.LegCount; j++)
				{
					KingdomLeg leg;
					if (!row.TryLeg(j, out leg))
					{
						fault = KingdomCityFault.InvalidIndex;
						return false;
					}
					LegZoneIds.Add(leg.ZoneId ?? "");
					LegEnterX.Add(leg.EnterX);
					LegEnterY.Add(leg.EnterY);
					LegExitX.Add(leg.ExitX);
					LegExitY.Add(leg.ExitY);
					LegLengths.Add(leg.PathLength);
					LegDepartTicks.Add(leg.DepartTick);
					LegArriveTicks.Add(leg.ArriveTick);
				}
			}
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>The next job id, minted off the realm's counter.</summary>
		public int MintJobId()
		{
			JobCounter++;
			return JobCounter;
		}

		private static KingdomJobKind KindOf(int stored)
		{
			return (stored == (int)KingdomJobKind.Delivery) ? KingdomJobKind.Delivery : KingdomJobKind.None;
		}

		private static KingdomStockKind CargoOf(int stored)
		{
			if (stored == (int)KingdomStockKind.Food) { return KingdomStockKind.Food; }
			if (stored == (int)KingdomStockKind.Materials) { return KingdomStockKind.Materials; }
			return KingdomStockKind.Water;
		}

		private static KingdomJobStatus StatusOf(int stored)
		{
			if (stored == (int)KingdomJobStatus.Delivered) { return KingdomJobStatus.Delivered; }
			if (stored == (int)KingdomJobStatus.Failed) { return KingdomJobStatus.Failed; }
			return KingdomJobStatus.Open;
		}

		private bool Duplicated(int index)
		{
			for (int i = 0; i < index; i++)
			{
				if (JobIds[i] == JobIds[index])
				{
					return true;
				}
			}
			return false;
		}

		private void DropOverCap()
		{
			int consumed = 0;
			for (int i = 0; i < JobIds.Count; i++)
			{
				if (i < KingdomJobRules.MaxOpenJobs)
				{
					consumed += LegCounts[i];
					continue;
				}
				RemoveLegs(consumed, LegCounts[i]);
				RemoveJob(i);
				i--;
			}
		}

		private void RemoveJob(int index)
		{
			JobIds.RemoveAt(index);
			Kinds.RemoveAt(index);
			Cargos.RemoveAt(index);
			CargoAmounts.RemoveAt(index);
			SourceZoneIds.RemoveAt(index);
			DestZoneIds.RemoveAt(index);
			StartTicks.RemoveAt(index);
			WalkTicksPerCell.RemoveAt(index);
			Statuses.RemoveAt(index);
			OriginCodes.RemoveAt(index);
			DepositLegIndexes.RemoveAt(index);
			LegCounts.RemoveAt(index);
		}

		private void RemoveLegs(int from, int count)
		{
			if (count <= 0 || from < 0 || from >= LegZoneIds.Count)
			{
				return;
			}
			int take = (from + count > LegZoneIds.Count) ? (LegZoneIds.Count - from) : count;
			LegZoneIds.RemoveRange(from, take);
			LegEnterX.RemoveRange(from, take);
			LegEnterY.RemoveRange(from, take);
			LegExitX.RemoveRange(from, take);
			LegExitY.RemoveRange(from, take);
			LegLengths.RemoveRange(from, take);
			LegDepartTicks.RemoveRange(from, take);
			LegArriveTicks.RemoveRange(from, take);
		}

		private static List<T> Repair<T>(List<T> column)
		{
			return column ?? new List<T>();
		}

		private static int Shortest(int[] counts)
		{
			int shortest = int.MaxValue;
			for (int i = 0; i < counts.Length; i++)
			{
				if (counts[i] < shortest)
				{
					shortest = counts[i];
				}
			}
			return (shortest == int.MaxValue) ? 0 : shortest;
		}

		private static void Trim<T>(List<T> column, int count)
		{
			if (column.Count > count)
			{
				column.RemoveRange(count, column.Count - count);
			}
		}
	}
}
