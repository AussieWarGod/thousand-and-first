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
				Zone zone = Body.CurrentZone;
				Spill(Body);
				Body.Obliterate();
				KingdomSurvey.ObserveRemovedFromActive(zone, Body);
			}
		}

		/// <summary>Closes central trip's one physical projection after its exact destination
		/// receipt has already published. Any unrelated protected inventory spills before removal.</summary>
		internal static void RetireCentralCarrier(KingdomSystem system, int tripId)
		{
			if (system == null || tripId <= 0) return;
			Release(system, tripId, Resolve(tripId), KingdomUnbindCause.JobClosed);
		}

		/// <summary>Completes one exact zone leg without closing its job. The job row remains the
		/// sole route authority; removing this rendering before unbinding means the next zone may
		/// mint exactly one body and a stale copy can never survive both sides of the handoff.</summary>
		private static void Handoff(KingdomSystem System, int jobId, GameObject Body, string ZoneId)
		{
			if (!GameObject.Validate(Body)) return;
			// Publish the licence to mint the next rendering before removing this one. If the
			// registry refuses, leave the live body in place: a visible delayed handoff is repairable;
			// a deleted body behind an open binding would refuse every later rendering forever.
			if (!KingdomResidents.Unbind(System, jobId, KingdomBindingKind.Transient,
				KingdomUnbindCause.ZoneHandoff)) return;
			Zone zone = Body.CurrentZone;
			Spill(Body);
			Body.Obliterate();
			KingdomSurvey.ObserveRemovedFromActive(zone, Body);
			KingdomLog.Log("porter: job " + jobId + " handed off at the exact exit from " + ZoneId);
		}

		/// <summary>Central trips keep one physical body and its exact manifest inventory across
		/// zones. Long-distance movement transfers that same GameObject to the next frozen leg; it
		/// never spills, deletes, or re-mints cargo at a boundary.</summary>
		private static void HandoffCentral(KingdomSystem system, int tripId, GameObject body,
			KingdomJobRow standing, KingdomItineraryFix standingFix, long now)
		{
			if (!GameObject.Validate(body) || system == null || system.Jobs == null) return;
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
				|| !TryActiveTripRow(table, tripId, now, out nextRow, out nextFix)) return;
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
				if (!KingdomItineraryRules.TryAt(group[i].Legs(), group[i].LegCount, now,
					out fix, out fault)) return false;
				row = group[i];
				return true;
			}
			return false;
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
	}
}
