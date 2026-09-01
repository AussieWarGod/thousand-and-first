using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomAdoptRules
	{
		/// <summary>The four independent spatial facts read by exact room adoption.</summary>
		public enum EnclosureRegion
		{
			Membership = 0,
			Shell = 1,
			Ingress = 2,
			Outside = 3
		}

		/// <summary>One cell's structural role and whether a resident may safely use its floor.
		/// Furniture, dropped items, and occupants never change <see cref="Region"/>.</summary>
		public struct CellObservation
		{
			public EnclosureRegion Region;
			public bool Usable;

			public CellObservation(EnclosureRegion Region, bool Usable = false)
			{
				this.Region = Region; this.Usable = Usable;
			}
		}

		public delegate CellObservation ExactCellLookup(int X, int Y);

		// Compatibility vocabulary for pure callers written before usable-floor evidence existed.
		public enum CellKind { Open = 0, Wall = 1, Door = 2, Outside = 3 }
		public delegate CellKind CellLookup(int X, int Y);
		public const int MinEnclosedRoomCells = 4;
		public const int MaxEnclosedRoomCells = 200;

		public struct EnclosureMeasurement
		{
			public bool Bounded;
			/// <summary>Exact structural membership count. Furniture never changes it.</summary>
			public int RoomCells;
			/// <summary>Safe ingress count. Locked or permanently blocked doors are shell.</summary>
			public int DoorCells;
			/// <summary>Ingress-reachable, currently safe and passable floor count.</summary>
			public int UsableCells;
			/// <summary>Exact membership, sorted by Y then X; this is durable d1 authority.</summary>
			public List<ArchitecturePoint> FloorCells;
			public List<ArchitecturePoint> MembershipCells
			{
				get { return FloorCells; }
				set { FloorCells = value; }
			}
			public List<ArchitecturePoint> ShellCells;
			public List<ArchitecturePoint> IngressCells;
			public List<ArchitecturePoint> UsableFloorCells;
		}

		/// <summary>Legacy pure adapter: open cells are usable membership.</summary>
		public static EnclosureMeasurement MeasureEnclosure(int StartX, int StartY,
			CellLookup Lookup)
		{
			if (Lookup == null) return default(EnclosureMeasurement);
			return MeasureExactEnclosure(StartX, StartY, delegate(int x, int y)
			{
				switch (Lookup(x, y))
				{
				case CellKind.Wall:
					return new CellObservation(EnclosureRegion.Shell);
				case CellKind.Door:
					return new CellObservation(EnclosureRegion.Ingress);
				case CellKind.Outside:
					return new CellObservation(EnclosureRegion.Outside);
				default:
					return new CellObservation(EnclosureRegion.Membership, true);
				}
			});
		}

		/// <summary>Bounded structural flood plus an ingress-seeded safe-floor flood. Structural
		/// membership ignores furnishings and occupants; usability does not.</summary>
		public static EnclosureMeasurement MeasureExactEnclosure(int StartX, int StartY,
			ExactCellLookup Lookup)
		{
			if (Lookup == null) return default(EnclosureMeasurement);
			CellObservation start = Lookup(StartX, StartY);
			if (start.Region != EnclosureRegion.Membership)
				return default(EnclosureMeasurement);

			HashSet<long> visited = new HashSet<long>();
			HashSet<long> usable = new HashSet<long>();
			Queue<long> frontier = new Queue<long>();
			List<ArchitecturePoint> floors = new List<ArchitecturePoint>();
			List<ArchitecturePoint> shell = new List<ArchitecturePoint>();
			List<ArchitecturePoint> ingress = new List<ArchitecturePoint>();
			long first = PackCell(StartX, StartY);
			visited.Add(first); frontier.Enqueue(first);
			if (start.Usable) usable.Add(first);
			bool leaked = false;

			while (frontier.Count > 0)
			{
				if (floors.Count >= MaxEnclosedRoomCells)
					return Finish(false, floors, shell, ingress, usable);
				long packed = frontier.Dequeue();
				int x = (int)(packed >> 32); int y = (int)packed;
				floors.Add(new ArchitecturePoint(x, y));
				VisitNeighbor(x + 1, y, Lookup, visited, usable, frontier,
					shell, ingress, ref leaked);
				VisitNeighbor(x - 1, y, Lookup, visited, usable, frontier,
					shell, ingress, ref leaked);
				VisitNeighbor(x, y + 1, Lookup, visited, usable, frontier,
					shell, ingress, ref leaked);
				VisitNeighbor(x, y - 1, Lookup, visited, usable, frontier,
					shell, ingress, ref leaked);
			}
			return Finish(!leaked, floors, shell, ingress, usable);
		}

		private static void VisitNeighbor(int X, int Y, ExactCellLookup Lookup,
			HashSet<long> Visited, HashSet<long> Usable, Queue<long> Frontier,
			List<ArchitecturePoint> Shell, List<ArchitecturePoint> Ingress, ref bool Leaked)
		{
			long key = PackCell(X, Y);
			if (!Visited.Add(key)) return;
			CellObservation observed = Lookup(X, Y);
			switch (observed.Region)
			{
			case EnclosureRegion.Shell:
				Shell.Add(new ArchitecturePoint(X, Y)); return;
			case EnclosureRegion.Ingress:
				Ingress.Add(new ArchitecturePoint(X, Y)); return;
			case EnclosureRegion.Outside:
				Leaked = true; return;
			default:
				Frontier.Enqueue(key);
				if (observed.Usable) Usable.Add(key);
				return;
			}
		}

		private static EnclosureMeasurement Finish(bool Bounded,
			List<ArchitecturePoint> Floors, List<ArchitecturePoint> Shell,
			List<ArchitecturePoint> Ingress, HashSet<long> Usable)
		{
			Floors.Sort(ComparePoints); Shell.Sort(ComparePoints); Ingress.Sort(ComparePoints);
			List<ArchitecturePoint> reached = ReachableUsable(Usable, Ingress);
			return new EnclosureMeasurement {
				Bounded = Bounded, RoomCells = Floors.Count, DoorCells = Ingress.Count,
				UsableCells = reached.Count, FloorCells = Floors, ShellCells = Shell,
				IngressCells = Ingress, UsableFloorCells = reached
			};
		}

		private static List<ArchitecturePoint> ReachableUsable(HashSet<long> Usable,
			List<ArchitecturePoint> Ingress)
		{
			HashSet<long> reached = new HashSet<long>();
			Queue<long> frontier = new Queue<long>();
			for (int i = 0; i < Ingress.Count; i++)
			{
				Seed(Ingress[i].X + 1, Ingress[i].Y, Usable, reached, frontier);
				Seed(Ingress[i].X - 1, Ingress[i].Y, Usable, reached, frontier);
				Seed(Ingress[i].X, Ingress[i].Y + 1, Usable, reached, frontier);
				Seed(Ingress[i].X, Ingress[i].Y - 1, Usable, reached, frontier);
			}
			while (frontier.Count > 0)
			{
				long packed = frontier.Dequeue();
				int x = (int)(packed >> 32); int y = (int)packed;
				Seed(x + 1, y, Usable, reached, frontier);
				Seed(x - 1, y, Usable, reached, frontier);
				Seed(x, y + 1, Usable, reached, frontier);
				Seed(x, y - 1, Usable, reached, frontier);
			}
			List<ArchitecturePoint> result = new List<ArchitecturePoint>();
			foreach (long packed in reached)
				result.Add(new ArchitecturePoint((int)(packed >> 32), (int)packed));
			result.Sort(ComparePoints); return result;
		}

		private static void Seed(int X, int Y, HashSet<long> Usable,
			HashSet<long> Reached, Queue<long> Frontier)
		{
			long key = PackCell(X, Y);
			if (Usable.Contains(key) && Reached.Add(key)) Frontier.Enqueue(key);
		}

		public static bool SameMembership(IReadOnlyList<ArchitecturePoint> A,
			IReadOnlyList<ArchitecturePoint> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++) if (!A[i].Equals(B[i])) return false;
			return true;
		}

		private static int ComparePoints(ArchitecturePoint A, ArchitecturePoint B)
		{
			int y = A.Y.CompareTo(B.Y); return y != 0 ? y : A.X.CompareTo(B.X);
		}

		private static long PackCell(int X, int Y)
		{
			return ((long)X << 32) | (uint)Y;
		}
	}
}
