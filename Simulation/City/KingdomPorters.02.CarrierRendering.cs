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
			GameObject pending;
			int pendingCount = ExactPendingMint(Z, jobId, at, out pending);
			if (pendingCount != 0)
			{
				if (pendingCount != 1 || !KingdomResidents.Bind(System, jobId,
					KingdomBindingKind.Transient, Z.ZoneID, pending, TimeTicks)) return null;
				KingdomSurvey.ObserveAddedToActive(Z, pending);
				return pending;
			}
			GameObject body = GameObject.Create(KingdomGrowth.DefaultSettlerBlueprint);
			if (body == null)
			{
				return null;
			}
			// A carrier is a visitor, not a resident: never enrolled, never named on the roll,
			// never counted in the population. The job id is the whole of their identity and it is
			// what the sweep is keyed on.
			body.SetIntProperty(KingdomResidents.JobIdProperty, jobId);
			body.RequirePart<r_KingdomPorter>().JobId = jobId;
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
			GameObject accepted = null;
			try { accepted = at.AddObject(body); } catch { }
			finally { KingdomSurvey.ObserveAddResultInActive(Z, body, accepted); }
			if (!ReferenceEquals(accepted, body) || !ReferenceEquals(body.CurrentCell, at))
				return null;
			body.MakeActive();
			if (!KingdomResidents.Bind(System, jobId, KingdomBindingKind.Transient,
				Z.ZoneID, body, TimeTicks))
			{
				KingdomLog.Log("porter: stamped unbound body waits for visible recovery");
				return null;
			}
			KingdomSurvey.ObserveAddedToActive(Z, body);
			return body;
		}

		private static int ExactPendingMint(Zone zone, int jobId, Cell standing,
			out GameObject exact)
		{
			exact = null;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(zone);
			if (survey == null || !survey.TryLoaded(out IList<GameObject> loaded)) return -1;
			int count = 0;
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject body = loaded[i];
				int propertyStamp = body?.GetIntProperty(KingdomResidents.JobIdProperty) ?? 0;
				int partStamp = body?.GetPart<r_KingdomPorter>()?.JobId ?? 0;
				if (propertyStamp != jobId && partStamp != jobId) continue;
				count++;
				if (count == 1 && GameObject.Validate(body)
					&& propertyStamp == jobId && partStamp == jobId
					&& body.IsAlive && !body.IsPlayer() && !body.IsPlayerLed()
					&& body.Blueprint == KingdomGrowth.DefaultSettlerBlueprint
					&& ReferenceEquals(body.CurrentCell, standing)
					&& KingdomOrdinaryCustody.TryProveEmpty(body, out string _)) exact = body;
			}
			if (count != 1) exact = null;
			return count;
		}

		/// <summary>Puts a carrier where the model says they are, minting one if the registry says
		/// there is none and moving the one there is if there is.</summary>
		private static void Place(KingdomSystem System, Zone Z, KingdomJobRow row,
			KingdomItineraryFix fix, long TimeTicks, int bindingId, bool central)
		{
			KingdomBindingVerdict verdict = KingdomResidents.Judge(System, bindingId,
				KingdomBindingKind.Transient, Z.ZoneID);
			if (verdict == KingdomBindingVerdict.Refuse)
			{
				return;
			}
			if (verdict == KingdomBindingVerdict.Move)
			{
				if (row.DeliveryCargoAuthority
						== KingdomDeliveryCargoAuthority.ConstructionInput
					&& row.DeliveryPhase == KingdomDeliveryPhase.SourceDebitPrepared)
				{
					KeepExactGoal(Z, bindingId);
					return;
				}
				if (!KingdomPorterRouteRules.ReprojectsOnMove(row.DeliveryCargoAuthority))
				{
					KeepExactGoal(Z, bindingId);
					return;
				}
				// Already standing here and already walking. The model's answer and the ground's
				// may have drifted while the founder was in the room, so this is where the ground
				// wins and the remainder of the itinerary shifts to match it (§3.7).
				Reproject(System, Z, row, fix, TimeTicks, bindingId);
				return;
			}
			GameObject body = Mint(System, Z, bindingId, fix.X, fix.Y, row.OriginCode, TimeTicks);
			if (body == null)
			{
				return;
			}
			r_KingdomPorter part = body.RequirePart<r_KingdomPorter>();
			part.JobId = bindingId;
			if (!central && row.CargoAmount > 0)
			{
				if (Load(body, KingdomData.CropForStyle(System.Style), row.CargoAmount,
					row.JobId) != row.CargoAmount)
				{
					Fail(System, row.JobId);
					return;
				}
			}
			if (row.DeliveryCargoAuthority
					== KingdomDeliveryCargoAuthority.ConstructionInput
				&& row.DeliveryPhase == KingdomDeliveryPhase.SourceDebitPrepared)
			{
				part.DestX = row.DeliverySourceX;
				part.DestY = row.DeliverySourceY;
				part.ExitX = row.DeliverySourceX;
				part.ExitY = row.DeliverySourceY;
				return;
			}
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
			KingdomLog.Log("porter: trip " + bindingId + " walks into " + Z.ZoneID
				+ " at " + fix.X + "," + fix.Y);
		}

		/// <summary>Authority-2 movement is already frozen by its parent receipt. Wake the same
		/// body and retain its exact persisted destination; only restore that goal if engine state
		/// lost it. This never rewrites job rows, route timing, or owner evidence.</summary>
		private static void KeepExactGoal(Zone Z, int bindingId)
		{
			GameObject body = Resolve(bindingId);
			r_KingdomPorter part = body == null ? null : body.GetPart<r_KingdomPorter>();
			Brain brain = body == null ? null : body.Brain;
			Cell target = part == null || Z == null ? null : Z.GetCell(part.DestX, part.DestY);
			if (brain == null || target == null) return;
			brain.Wake();
			Cell moving = brain.MovingTo();
			if ((moving != null && moving.X == target.X && moving.Y == target.Y
					&& moving.ParentZone != null && string.Equals(moving.ParentZone.ZoneID,
						Z.ZoneID, StringComparison.Ordinal))
				|| ReferenceEquals(body.CurrentCell, target)) return;
			Walk(body, Z, part.DestX, part.DestY);
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
		private static void Reproject(KingdomSystem System, Zone Z, KingdomJobRow row,
			KingdomItineraryFix fix, long TimeTicks, int bindingId)
		{
			if (!KingdomPorterRouteRules.ReprojectsOnMove(row.DeliveryCargoAuthority))
			{
				KeepExactGoal(Z, bindingId);
				return;
			}
			GameObject body = Resolve(bindingId);
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

	}
}
