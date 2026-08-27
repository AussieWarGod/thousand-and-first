using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.AI.GoalHandlers;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomPorters
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
			int folded = Fold(System, Z, Survey, table, Blueprint, Amount, TimeTicks);
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
			int width = (Z.Width > 2) ? Z.Width : KingdomJobRules.ZoneWidth;
			int height = (Z.Height > 2) ? Z.Height : KingdomJobRules.ZoneHeight;
			short entryX;
			short entryY;
			int originCode;
			if (!KingdomJobRules.TryDrawOrigin(System.SimulationSeed, SeedLabel(System), jobId, KingdomRules.Origins.Length, out originCode, out fault))
			{
				originCode = KingdomResidentRules.NoOrigin;
			}
			KingdomLeg[] legs;
			int legCount;
			KingdomZoneStep arrival;
			if (!TryPlan(System, Z, jobId, (short)destination.X, (short)destination.Y,
				TimeTicks, SourceZoneId, out entryX, out entryY, out arrival,
				out legs, out legCount, out fault))
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
				+ ((arrival == KingdomZoneStep.Up || arrival == KingdomZoneStep.Down)
					? " by the paired shaft, " : (" by the " + arrival + " edge, "))
				+ legCount + " legs");
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
		private static int Fold(KingdomSystem System, Zone Z, KingdomSurvey Survey,
			KingdomJobTable table, string Blueprint, int Amount, long TimeTicks)
		{
			for (int i = 0; i < table.Count; i++)
			{
				KingdomJobRow row;
				if (!table.TryAt(i, out row)
					|| row.Kind != KingdomJobKind.Delivery
					|| row.Status != KingdomJobStatus.Open
					|| row.Cargo != KingdomStockKind.Food
					|| row.CargoAmount <= 0
					|| row.CargoAmount >= LoadPerTrip
					|| !string.Equals(row.DestZoneId, Z.ZoneID, StringComparison.Ordinal)
					|| KingdomJobRules.Deposited(row, TimeTicks))
				{
					continue;
				}
				GameObject body = Survey.FindTransient(row.JobId);
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
	}
}
