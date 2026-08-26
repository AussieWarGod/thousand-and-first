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
		private static List<Cell> DeterministicEntryCells(Zone zone, Cell objective, long seed)
		{
			if (zone == null || objective == null) return new List<Cell>();
			bool[,] reachable = new bool[zone.Width, zone.Height];
			Queue<Cell> pending = new Queue<Cell>();
			int[] dx = new int[4] { -1, 1, 0, 0 };
			int[] dy = new int[4] { 0, 0, -1, 1 };
			for (int i = 0; i < 4; i++)
			{
				int x = objective.X + dx[i];
				int y = objective.Y + dy[i];
				if (x < 0 || x >= zone.Width || y < 0 || y >= zone.Height) continue;
				Cell start = zone.GetCell(x, y);
				if (start == null || !start.IsPassable(null, false) || reachable[x, y]) continue;
				reachable[x, y] = true;
				pending.Enqueue(start);
			}
			while (pending.Count > 0)
			{
				Cell from = pending.Dequeue();
				for (int i = 0; i < 4; i++)
				{
					int x = from.X + dx[i];
					int y = from.Y + dy[i];
					if (x < 0 || x >= zone.Width || y < 0 || y >= zone.Height
						|| reachable[x, y]) continue;
					Cell next = zone.GetCell(x, y);
					if (next == null || !next.IsPassable(null, false)) continue;
					reachable[x, y] = true;
					pending.Enqueue(next);
				}
			}
			List<Cell> cells = zone.GetEmptyCells(delegate(Cell c)
			{
				return (c.X == 0 || c.X == zone.Width - 1 || c.Y == 0 || c.Y == zone.Height - 1)
					&& c.IsPassable(null, false) && reachable[c.X, c.Y];
			}) ?? new List<Cell>();
			cells.Sort(delegate(Cell a, Cell b)
			{
				int x = a.X.CompareTo(b.X); return x != 0 ? x : a.Y.CompareTo(b.Y);
			});
			if (cells.Count > 1)
			{
				int offset = (int)(seed % cells.Count);
				List<Cell> rotated = new List<Cell>(cells.Count);
				for (int i = 0; i < cells.Count; i++) rotated.Add(cells[(i + offset) % cells.Count]);
				return rotated;
			}
			return cells;
		}

		private static void CountProjection(Zone zone, KingdomLifecycleProjection projection,
			out int ids, out int markers, out GameObject exact)
		{
			ids = 0; markers = 0; exact = null;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(zone))
			{
				if (item.ID == projection.ObjectId) { ids++; exact = item; }
				if (item.GetStringProperty(ProjectionMarkerProperty) == projection.Marker) markers++;
			}
		}

		private static void CountDemandProjection(GameObject owner,
			KingdomLifecycleProjection projection, out int ids, out int markers,
			out GameObject exact)
		{
			ids = 0; markers = 0; exact = null;
			if (!GameObject.Validate(owner) || owner.Inventory == null
				|| owner.Inventory.Objects == null) return;
			for (int i = 0; i < owner.Inventory.Objects.Count; i++)
			{
				GameObject item = owner.Inventory.Objects[i];
				if (item.ID == projection.ObjectId) { ids++; exact = item; }
				if (item.GetStringProperty(ProjectionMarkerProperty) == projection.Marker) markers++;
			}
		}

		private static bool ResumeDemandProjection(KingdomSystem system, Zone zone,
			KingdomLifecycleOperation op)
		{
			GameObject owner = The.Player;
			if (system == null || zone == null || op == null || !GameObject.Validate(owner)
				|| op.Action != KingdomLifecycleAction.RaidDeliverDemand
				|| op.Projections.Count != 1 || owner.ID != op.Projections[0].OwnerId
				|| zone.ZoneID != op.Projections[0].ZoneId) return false;
			KingdomLifecycleProjection projection = op.Projections[0];
			for (int guard = 0; guard < 2; guard++)
			{
				GameObject exact;
				int ids;
				int markers;
				CountDemandProjection(owner, projection, out ids, out markers, out exact);
				if (projection.State == KingdomLifecyclePhysicalState.Proved)
					return ids == 1 && markers == 1 && ExactDemandBody(exact, op, projection);
				if (projection.State == KingdomLifecyclePhysicalState.Intent)
				{
					if (ids == 0 && markers == 0)
					{
						if (!KingdomLifecycleRules.RaidRuntimeAdapter.ResetAbsentProjectionIntent(
							system.LifecycleBook, op, projection, ids, markers)) return false;
						continue;
					}
					if (!ExactDemandBody(exact, op, projection)
						|| !KingdomLifecycleRules.RaidRuntimeAdapter.CommitProjection(
							system.LifecycleBook, op, projection, ids, markers, exact.Blueprint,
							owner.ID, zone.ZoneID, -1, -1))
					{
						KingdomLifecycleRules.Quarantine(op,
							"demand delivery intent had ambiguous physical evidence");
						return false;
					}
					return true;
				}
				if (projection.State != KingdomLifecyclePhysicalState.Prepared
					|| ids != 0 || markers != 0) return false;
				GameObject body = null;
				try { body = GameObject.Create(projection.Blueprint); } catch { }
				if (!GameObject.Validate(body) || body.Blueprint != projection.Blueprint) return false;
				body.ID = projection.ObjectId;
				PrepareDemandBody(body, op, projection);
				if (!KingdomLifecycleRules.RaidRuntimeAdapter.BeginProjection(
					system.LifecycleBook, op, projection, ids, markers)) return false;
				GameObject accepted = null;
				try { accepted = owner.Inventory.AddObject(body, null, Silent: true, NoStack: true); }
				catch { }
				KingdomSurvey.ObserveCurrentTopologyInActive(zone, owner);
				KingdomSurvey.ObserveAddResultInActive(zone, body, accepted);
				CountDemandProjection(owner, projection, out ids, out markers, out exact);
				if (!ReferenceEquals(accepted, body) || !ReferenceEquals(exact, body)
					|| !KingdomLifecycleRules.RaidRuntimeAdapter.CommitProjection(
						system.LifecycleBook, op, projection, ids, markers, body.Blueprint,
						owner.ID, zone.ZoneID, -1, -1)) return false;
				return true;
			}
			return false;
		}

		private static void PrepareDemandBody(GameObject body,
			KingdomLifecycleOperation op, KingdomLifecycleProjection projection)
		{
			body.SetStringProperty(ProjectionMarkerProperty, projection.Marker);
			r_KingdomRaidDemand part = body.RequirePart<r_KingdomRaidDemand>();
			part.IncidentId = op.ObjectId; part.ChannelId = op.Origin;
			part.Revision = op.Target; part.Inert = false;
		}

		private static bool ExactDemandBody(GameObject body,
			KingdomLifecycleOperation op, KingdomLifecycleProjection projection)
		{
			if (!GameObject.Validate(body) || body.ID != projection.ObjectId
				|| body.Blueprint != projection.Blueprint
				|| body.GetStringProperty(ProjectionMarkerProperty) != projection.Marker
				|| !ReferenceEquals(body.InInventory, The.Player)) return false;
			r_KingdomRaidDemand part = body.GetPart<r_KingdomRaidDemand>();
			return part != null && !part.Inert && part.IncidentId == op.ObjectId
				&& part.ChannelId == op.Origin && part.Revision == op.Target;
		}

	}
}
