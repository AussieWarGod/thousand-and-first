using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.AI.GoalHandlers;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomRaids
	{
		private static bool ResumeAttackProjections(KingdomSystem system, Zone zone,
			KingdomLifecycleOperation op)
		{
			if (system == null || zone == null || op == null
				|| !string.Equals(zone.ZoneID, op.ZoneId, StringComparison.Ordinal)) return false;
			GameObject objective = FindExact(zone, op.Origin);
			if (!GameObject.Validate(objective) || objective.CurrentCell == null
				|| objective.CurrentCell.X != op.Target || objective.CurrentCell.Y != op.Count
				|| objective.GetIntProperty("KingdomStores") != 1)
			{
				KingdomLifecycleRules.Quarantine(op,
					"raid projection recovery lost its frozen objective witness");
				return false;
			}
			for (int i = 0; i < op.Projections.Count; i++)
			{
				KingdomLifecycleProjection projection = op.Projections[i];
				if (projection.State == KingdomLifecyclePhysicalState.Proved) continue;
				GameObject exact;
				int ids;
				int markers;
				CountProjection(zone, projection, out ids, out markers, out exact);
				if (projection.State == KingdomLifecyclePhysicalState.Intent)
				{
					if (ids == 0 && markers == 0)
					{
						if (!KingdomLifecycleRules.RaidRuntimeAdapter.ResetAbsentProjectionIntent(
							system.LifecycleBook, op, projection, ids, markers))
						{
							KingdomLifecycleRules.Quarantine(op,
								"absent raid projection intent could not be retried exactly");
							return false;
						}
					}
					else
					{
						if (!ExactRaiderBody(exact, op, projection)
							|| !KingdomLifecycleRules.RaidRuntimeAdapter.CommitProjection(
								system.LifecycleBook, op, projection, ids, markers,
								exact.Blueprint, zone.ZoneID, exact.CurrentCell.X,
								exact.CurrentCell.Y))
						{
							KingdomLifecycleRules.Quarantine(op,
								"raid projection intent had ambiguous physical evidence");
							return false;
						}
						ActivateRaiderBody(exact, system, objective);
						continue;
					}
				}
				if (projection.State != KingdomLifecyclePhysicalState.Prepared
					|| ids != 0 || markers != 0)
				{
					KingdomLifecycleRules.Quarantine(op,
						"prepared raid projection had non-pristine physical evidence");
					return false;
				}
				GameObject body = null;
				try { body = GameObject.Create(projection.Blueprint); } catch { }
				Cell cell = zone.GetCell(projection.X, projection.Y);
				if (!GameObject.Validate(body) || body.Blueprint != projection.Blueprint
					|| cell == null || !cell.IsPassable(null, false))
				{
					KingdomLifecycleRules.Quarantine(op,
						"raid projection body or frozen entry cell could not be recreated");
					return false;
				}
				body.ID = projection.ObjectId;
				PrepareRaiderBody(body, op, projection, op.ObjectId);
				if (!KingdomLifecycleRules.RaidRuntimeAdapter.BeginProjection(
					system.LifecycleBook, op, projection, ids, markers)) return false;
				GameObject accepted = null;
				try { accepted = cell.AddObject(body); } catch { }
				KingdomSurvey.ObserveAddResultInActive(zone, body, accepted);
				CountProjection(zone, projection, out ids, out markers, out exact);
				if (!ReferenceEquals(accepted, body) || !ReferenceEquals(exact, body)
					|| !KingdomLifecycleRules.RaidRuntimeAdapter.CommitProjection(
						system.LifecycleBook, op, projection, ids, markers, body.Blueprint,
						zone.ZoneID, cell.X, cell.Y)) return false;
				ActivateRaiderBody(body, system, objective);
			}
			return AllProjectionsProved(op);
		}

		private static void PrepareRaiderBody(GameObject body,
			KingdomLifecycleOperation op, KingdomLifecycleProjection projection,
			string incidentId)
		{
			body.SetStringProperty(ProjectionMarkerProperty, projection.Marker);
			body.SetIntProperty("KingdomRaider", 1);
			body.RequirePart<NoXPGain>();
			body.AddPart(new r_KingdomRaiderObjective(op.Id, incidentId,
				op.Origin, op.Target, op.Count));
		}

		private static bool ExactRaiderBody(GameObject body,
			KingdomLifecycleOperation op, KingdomLifecycleProjection projection)
		{
			if (!GameObject.Validate(body) || body.CurrentCell == null
				|| body.ID != projection.ObjectId || body.Blueprint != projection.Blueprint
				|| body.CurrentZone?.ZoneID != projection.ZoneId
				|| body.CurrentCell.X != projection.X || body.CurrentCell.Y != projection.Y
				|| body.GetStringProperty(ProjectionMarkerProperty) != projection.Marker
				|| body.GetIntProperty("KingdomRaider") != 1
				|| body.GetPart<NoXPGain>() == null) return false;
			r_KingdomRaiderObjective part = body.GetPart<r_KingdomRaiderObjective>();
			return part != null && part.OperationId == op.Id && part.IncidentId == op.ObjectId
				&& part.TargetObjectId == op.Origin && part.TargetX == op.Target
				&& part.TargetY == op.Count;
		}

		private static void ActivateRaiderBody(GameObject body, KingdomSystem system,
			GameObject objective)
		{
			body.MakeActive();
			if (body.Brain == null || objective?.CurrentCell == null) return;
			body.Brain.Allegiance["Player"] = -100;
			if (!string.IsNullOrEmpty(system.KingdomFactionName))
				body.Brain.Allegiance[system.KingdomFactionName] = -100;
			body.Brain.PushGoal(new MoveTo(objective.CurrentCell, careful: true));
		}

		private static bool AllProjectionsProved(KingdomLifecycleOperation op)
		{
			for (int i = 0; i < op.Projections.Count; i++)
				if (op.Projections[i].State != KingdomLifecyclePhysicalState.Proved) return false;
			return op.Projections.Count > 0;
		}

		private static GameObject FindExact(Zone zone, string id)
		{
			GameObject found = null;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(zone))
				if (item.ID == id) { if (found != null) return null; found = item; }
			return found;
		}

		private static int CountLiveRaiders(Zone zone, string operationId,
			GameObject excluded = null)
		{
			int count = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(zone))
			{
				r_KingdomRaiderObjective part = item.GetPart<r_KingdomRaiderObjective>();
				if (!ReferenceEquals(item, excluded) && part != null
					&& part.OperationId == operationId && GameObject.Validate(item)
					&& item.IsAlive) count++;
			}
			return count;
		}

		private static bool RestoreDebitOrQuarantine(KingdomSystem system,
			KingdomLifecycleOperation op, KingdomWaterDebit debit, string fault)
		{
			if (debit == null || debit.Rollback() || debit.RestorationExact) return true;
			KingdomLifecycleBook book = system?.LifecycleBook;
			if (op != null && ReferenceEquals(book?.Raid, op))
				KingdomLifecycleRules.Quarantine(op, fault);
			if (book != null)
			{
				book.Quarantined = true;
				book.Fault = fault;
			}
			return false;
		}

		private static string DisplayFaction(string faction)
		{
			try { return Faction.GetFormattedName(faction); }
			catch { return faction ?? "unknown raiders"; }
		}

		private static long SafeAdd(long value, long delta)
		{
			if (value < 0L || delta < 0L || value > long.MaxValue - delta) return long.MaxValue;
			return value + delta;
		}
	}
}
