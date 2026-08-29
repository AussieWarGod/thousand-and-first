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
					System.PendingCropBlueprint = KingdomData.CropForStyle(System.Style);
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

		/// <summary>Closes central trip's one physical projection after its exact destination
		/// receipt has already published. Any unrelated protected inventory spills before removal.</summary>
		internal static void RetireCentralCarrier(KingdomSystem system, int tripId)
		{
			if (system == null || tripId <= 0) return;
			Release(system, tripId, Resolve(tripId), KingdomUnbindCause.JobClosed);
		}

		internal static void RetireCentralCarrier(KingdomSystem system, int tripId,
			GameObject exactRootedBody)
		{
			if (system == null || tripId <= 0) return;
			Release(system, tripId, exactRootedBody, KingdomUnbindCause.JobClosed);
		}

		/// <summary>Central trips keep one physical body and its exact manifest inventory across
		/// zones. Long-distance movement transfers that same GameObject to the next frozen leg; it
		/// never spills, deletes, or re-mints cargo at a boundary.</summary>
		private static void HandoffCentral(KingdomSystem system, int tripId, GameObject body,
			KingdomJobRow standing, KingdomItineraryFix standingFix, long now)
		{
			if (!GameObject.Validate(body) || system == null || system.Jobs == null
				|| standing.DeliveryCargoAuthority
					== KingdomDeliveryCargoAuthority.ConstructionInput) return;
			if (standingFix.LegIndex >= 0)
			{
				KingdomLeg leg;
				if (standing.TryLeg(standingFix.LegIndex, out leg) && now < leg.ArriveTick) return;
			}
			KingdomJobTable table;
			KingdomCityFault fault;
			KingdomJobRow nextRow;
			KingdomItineraryFix nextFix;
			if (!system.Jobs.TryRead(out table, out fault)
				|| !TryActiveTripRow(table, tripId, now, out nextRow, out nextFix)
				|| nextRow.DeliveryCargoAuthority
					== KingdomDeliveryCargoAuthority.ConstructionInput) return;
			if (nextRow.JobId == standing.JobId && nextRow.CargoAmount > 0
				&& nextFix.Phase == KingdomItineraryPhase.Delivered) return;
			Zone nextZone;
			try { nextZone = The.ZoneManager == null ? null : The.ZoneManager.GetZone(nextFix.ZoneId); }
			catch { return; }
			Cell nextCell = nextZone == null ? null : Standing(nextZone, nextFix.X, nextFix.Y);
			if (nextCell == null) return;
			if (!ReferenceEquals(body.CurrentCell, nextCell))
			{
				try
				{
					if (!body.SystemLongDistanceMoveTo(nextCell, 0, forced: true,
						ignoreCombat: true) || !ReferenceEquals(body.CurrentCell, nextCell)) return;
				}
				catch { return; }
			}
			if (!KingdomResidents.Bind(system, tripId, KingdomBindingKind.Transient,
				nextZone.ZoneID, body, now)) return;
			r_KingdomPorter part = body.RequirePart<r_KingdomPorter>();
			part.JobId = tripId;
			KingdomLeg nextLeg;
			if (nextRow.TryLeg(nextFix.LegIndex < 0 ? 0 : nextFix.LegIndex, out nextLeg))
			{
				part.DestX = nextLeg.ExitX; part.DestY = nextLeg.ExitY;
				part.ExitX = nextLeg.ExitX; part.ExitY = nextLeg.ExitY;
				Walk(body, nextZone, nextLeg.ExitX, nextLeg.ExitY);
			}
			KingdomLog.Log("porter: trip " + tripId + " moved the same body and cargo into "
				+ nextZone.ZoneID);
		}

		private static bool TryActiveTripRow(KingdomJobTable table, int tripId, long now,
			out KingdomJobRow row, out KingdomItineraryFix fix)
		{
			row = default(KingdomJobRow);
			fix = default(KingdomItineraryFix);
			List<KingdomJobRow> group = new List<KingdomJobRow>();
			for (int i = 0; table != null && i < table.Count; i++)
			{
				KingdomJobRow candidate;
				if (table.TryAt(i, out candidate) && candidate.DeliveryTripId == tripId
					&& KingdomJobRules.IsCentralDelivery(candidate)
					&& (candidate.DeliveryPhase == KingdomDeliveryPhase.SourceDebitPrepared
						|| candidate.DeliveryPhase == KingdomDeliveryPhase.InFlight))
					group.Add(candidate);
			}
			group.Sort(delegate(KingdomJobRow a, KingdomJobRow b)
			{
				return a.DeliveryStopOrdinal.CompareTo(b.DeliveryStopOrdinal);
			});
			KingdomCityFault fault;
			for (int i = 0; i < group.Count; i++)
			{
				if (group[i].DeliveryCargoAuthority == KingdomDeliveryCargoAuthority.ScalarStock
					&& group[i].CargoAmount <= 0) continue;
				if (group[i].DeliveryCargoAuthority
						== KingdomDeliveryCargoAuthority.ConstructionInput
					&& group[i].DeliveryPhase == KingdomDeliveryPhase.SourceDebitPrepared)
				{
					row = group[i];
					fix = new KingdomItineraryFix(KingdomItineraryPhase.EnRoute, 0,
						row.SourceZoneId, (short)row.DeliverySourceX,
						(short)row.DeliverySourceY, 0);
					return true;
				}
				if (!KingdomItineraryRules.TryAt(group[i].Legs(), group[i].LegCount, now,
					out fix, out fault)) return false;
				row = group[i];
				return true;
			}
			return false;
		}

	}
}
