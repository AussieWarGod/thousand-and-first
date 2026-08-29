using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.AI.Pathfinding;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityEndpointRuntime
	{
		private const int MaximumResidentLookupObjects = 65536;

		private static bool TryPlanDistinctReachableCells(Zone Zone, GameObject[] Observed,
			out Cell[] Cells, out string Failure)
		{
			Cells = Observed == null ? null : new Cell[Observed.Length]; Failure = null;
			Cell ingress = The.Player?.CurrentCell;
			if (Zone == null || ingress == null || !ReferenceEquals(ingress.ParentZone, Zone) ||
				Observed == null)
			{
				Failure = "endpoint party lacks a current loaded encounter route"; return false;
			}
			HashSet<Cell> reserved = new HashSet<Cell>();
			for (int i = 0; i < Observed.Length; i++)
			{
				if (!GameObject.Validate(Observed[i])) continue;
				Cell occupied = Observed[i].CurrentCell;
				if (occupied == null || !ReferenceEquals(occupied.ParentZone, Zone) ||
					!reserved.Add(occupied) || !RouteProved(ingress, occupied))
				{
					Failure = "existing endpoint bodies do not occupy distinct reachable cells";
					return false;
				}
				Cells[i] = occupied;
			}
			for (int i = 0; i < Observed.Length; i++)
			{
				if (Cells[i] != null) continue;
				Cells[i] = FindLocalCell(Zone, ingress, reserved);
				if (Cells[i] == null)
				{
					Failure = "no distinct bounded cell is reachable from the current encounter route";
					return false;
				}
				reserved.Add(Cells[i]);
			}
			return true;
		}

		private static Cell FindLocalCell(Zone Zone, Cell Ingress, HashSet<Cell> Reserved)
		{
			const int maximumRadius = 12;
			for (int radius = 1; radius <= maximumRadius; radius++)
			{
				int x1 = Math.Max(0, Ingress.X - radius);
				int x2 = Math.Min(Zone.Width - 1, Ingress.X + radius);
				int y1 = Math.Max(0, Ingress.Y - radius);
				int y2 = Math.Min(Zone.Height - 1, Ingress.Y + radius);
				for (int y = y1; y <= y2; y++) for (int x = x1; x <= x2; x++)
				{
					if (Math.Max(Math.Abs(x - Ingress.X), Math.Abs(y - Ingress.Y)) != radius)
						continue;
					Cell cell = Zone.GetCell(x, y);
					if (cell != null && KingdomPolityPhysicalCustodyRules.CandidateCellAllowed(
						!Reserved.Contains(cell), cell.IsPassable(), cell.IsEmptyOfSolid(),
						RouteProved(Ingress, cell))) return cell;
				}
			}
			return null;
		}

		private static bool RouteProved(Cell Ingress, Cell Candidate)
		{
			if (Ingress == null || Candidate == null ||
				!ReferenceEquals(Ingress.ParentZone, Candidate.ParentZone)) return false;
			if (ReferenceEquals(Ingress, Candidate)) return true;
			try
			{
				FindPath path = new FindPath(Ingress, Candidate, PathGlobal: false,
					PathUnlimited: true, Looker: The.Player, MaxWeight: 95,
					ExploredOnly: false, Juggernaut: false, IgnoreCreatures: true);
				return path.Usable;
			}
			catch (Exception) { return false; }
		}

		private static void TryPlaceContestedPreparedBody(Cell Cell, GameObject Body)
		{
			if (!GameObject.Validate(Body) || Body.CurrentCell != null || Body.InInventory != null ||
				Body.Equipped != null || Cell == null) return;
			try { Cell.AddObject(Body, Silent: true, NoStack: true); }
			catch (Exception) { }
		}

		private static bool TryMarkContestedPreparedBody(Cell Cell, GameObject Body, string RealmId,
			KingdomPolityCohortPlan Cohort, KingdomPolityProjectionReceipt Receipt, int Ordinal,
			out string Failure)
		{
			Failure = null;
			try
			{
				if (GameObject.Validate(Body))
					Body.SetIntProperty(KingdomPolityNpcRuntime.ContestedProperty, 1);
				Zone zone = Cell?.ParentZone;
				if (zone == null || Cohort == null || Receipt == null || Ordinal < 0 ||
					Ordinal >= Cohort.ResolvedMembers.Count)
					return FailPhysical("contested body lacks exact quarantine authority", out Failure);
				string objectId = KingdomPolityCohortRules.PreparedObjectId(Cohort, Ordinal);
				string key = KingdomPolityPhysicalCustodyRules.ContestedWitnessKey(
					Receipt.ProjectionId, objectId);
				string expected = KingdomPolityPhysicalCustodyRules.ContestedWitness(RealmId,
					Cohort.CohortId, Receipt.ProjectionId, zone.ZoneID, objectId, Ordinal);
				KingdomPolityCleanupEvidenceProof prior = InspectUniqueRawZoneSlot(zone, key,
					expected, out string _, out Failure);
				if (prior == KingdomPolityCleanupEvidenceProof.Exact) return true;
				if (prior != KingdomPolityCleanupEvidenceProof.Absent) return FailPhysical(Failure ??
					"contested witness slot contains foreign or ambiguous authority", out Failure);
				try { zone.SetZoneProperty(key, expected); }
				catch (Exception ex)
				{
					if (InspectUniqueRawZoneSlot(zone, key, expected, out string _, out string _) ==
						KingdomPolityCleanupEvidenceProof.Exact) { Failure = null; return true; }
					return FailPhysical("contested witness write failed: " + ex.Message, out Failure);
				}
				return InspectUniqueRawZoneSlot(zone, key, expected, out string _, out Failure) ==
					KingdomPolityCleanupEvidenceProof.Exact || FailPhysical(Failure ??
						"contested witness did not survive exact writeback", out Failure);
			}
			catch (Exception ex)
			{
				return FailPhysical("contested body quarantine failed: " + ex.Message, out Failure);
			}
		}

		private static bool HasContestedPreparedBody(Zone Zone,
			KingdomPolityProjectionReceipt Receipt, string ObjectId)
		{
			return Zone != null && Receipt != null && Zone.HasZoneProperty(
				KingdomPolityPhysicalCustodyRules.ContestedWitnessKey(Receipt.ProjectionId, ObjectId));
		}

		private static bool TryFindResidentObject(string ObjectId, out GameObject Found,
			out string Failure)
		{
			Found = null; Failure = null;
			if (string.IsNullOrEmpty(ObjectId))
				return FailPhysical("resident object lookup lacks an exact id", out Failure);
			try
			{
				if (!TryCollectResidentRoots(out List<GameObject> pending,
					out HashSet<GameObject> excluded, out Failure) ||
					!TryScanResidentRoots(pending, excluded, ObjectId, out GameObject exact,
						out int matches, out Failure)) return false;
				KingdomPolityCleanupEvidenceProof lookup =
					KingdomPolityPhysicalCustodyRules.ClassifyResidentEvidence(true, matches);
				if (lookup == KingdomPolityCleanupEvidenceProof.Ambiguous) return FailPhysical(
					"resident object id is globally duplicated", out Failure);
				GameObject indexed = GameObject.FindByID(ObjectId);
				if ((GameObject.Validate(indexed) && indexed.IDIfAssigned != ObjectId) ||
					(matches == 0 && GameObject.Validate(indexed)) || (matches == 1 &&
					(!GameObject.Validate(indexed) || !ReferenceEquals(indexed, exact))))
					return FailPhysical("resident object index disagrees with bounded global proof",
						out Failure);
				Found = lookup == KingdomPolityCleanupEvidenceProof.Exact ? exact : null; return true;
			}
			catch (Exception ex)
			{
				return FailPhysical("resident object lookup failed: " + ex.Message, out Failure);
			}
		}

		private static bool TryProveLocalObjectAbsence(Zone Zone, string ObjectId,
			out string Failure)
		{
			Failure = null;
			try
			{
				List<GameObject> roots = Zone?.GetObjects();
				if (roots == null || roots.Count > MaximumResidentLookupObjects)
					return FailPhysical("local resident object ground is unscannable", out Failure);
				return TryScanResidentRoots(new List<GameObject>(roots), new HashSet<GameObject>(),
					ObjectId, out GameObject _, out int matches, out Failure) && matches == 0 ||
					FailPhysical(Failure ?? "prepared cleanup lacks exact local-zone absence",
						out Failure);
			}
			catch (Exception ex) { return FailPhysical(
				"local resident object lookup failed: " + ex.Message, out Failure); }
		}

		private static bool TryScanResidentRoots(List<GameObject> Pending,
			HashSet<GameObject> Excluded, string ObjectId, out GameObject Exact,
			out int Matches, out string Failure)
		{
			Exact = null; Matches = 0; Failure = null;
			HashSet<GameObject> expanded = new HashSet<GameObject>();
			HashSet<GameObject> found = new HashSet<GameObject>();
			while (Pending.Count > 0)
			{
				GameObject candidate = Pending[Pending.Count - 1]; Pending.RemoveAt(Pending.Count - 1);
				if (candidate == null || !expanded.Add(candidate) || Excluded.Contains(candidate)) continue;
				if (expanded.Count > MaximumResidentLookupObjects)
					return FailPhysical("resident object scan capacity is exhausted", out Failure);
				if (candidate.IDIfAssigned == ObjectId)
				{
					if (!GameObject.Validate(candidate)) return FailPhysical(
						"resident object id belongs to invalid live custody", out Failure);
					found.Add(candidate);
				}
				List<GameObject> children = candidate.GetInventoryDirectAndEquipment();
				if (children != null)
				{
					if (children.Count > MaximumResidentLookupObjects - Pending.Count)
						return FailPhysical("resident object scan capacity is exhausted", out Failure);
					Pending.AddRange(children);
				}
			}
			Matches = found.Count;
			if (Matches == 1) foreach (GameObject item in found) Exact = item;
			return true;
		}
	}
}
