using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static class KingdomLodgingRoomRules
	{
		public readonly struct SleepingPlace
		{
			public readonly int X;
			public readonly int Y;
			public readonly int Capacity;

			public SleepingPlace(int X, int Y, int Capacity)
			{
				this.X = X; this.Y = Y; this.Capacity = Capacity;
			}
		}

		public sealed class Reading
		{
			public int SleepingRooms;
			public int SleepingPlaces;
			public int ExposedPlaces;
			public int UnusablePlaces;
			public int UsableFloorCells;
			public KingdomLodgingRules.Closeness Quarters;
		}

		private sealed class Room
		{
			internal KingdomAdoptRules.EnclosureMeasurement Shape;
			internal int Places;
			internal readonly HashSet<long> Reached = new HashSet<long>();
		}

		// Uses operable physical sleeping providers. A catalogue capacity is not a bed location.
		public static Reading Measure(IReadOnlyList<KingdomBenefitCell> Designation,
			IReadOnlyList<SleepingPlace> Places, KingdomAdoptRules.ExactCellLookup Lookup)
		{
			Reading result = new Reading();
			if (Designation == null || Places == null || Lookup == null
				|| Designation.Count > 4000 || Places.Count > 4000) return result;
			HashSet<long> scope = new HashSet<long>();
			HashSet<long> beds = new HashSet<long>();
			for (int i = 0; i < Places.Count; i++) beds.Add(Pack(Places[i].X, Places[i].Y));
			for (int i = 0; i < Designation.Count; i++)
				if ((Designation[i].Use & KingdomBenefitCellUse.Plot) != 0)
					scope.Add(Pack(Designation[i].X, Designation[i].Y));
			Dictionary<long, KingdomAdoptRules.CellObservation> observed =
				new Dictionary<long, KingdomAdoptRules.CellObservation>();
			KingdomAdoptRules.ExactCellLookup cached = delegate(int x, int y)
			{
				long key = Pack(x, y);
				if (!observed.TryGetValue(key, out var cell))
				{
					cell = Lookup(x, y);
					if (beds.Contains(key)) cell.Usable = false;
					if (beds.Contains(key) && cell.Region == KingdomAdoptRules.EnclosureRegion.Ingress)
						cell.Region = KingdomAdoptRules.EnclosureRegion.Shell;
					observed.Add(key, cell);
				}
				return cell;
			};
			HashSet<long> accessible = ReachFromBoundary(scope, cached);
			KingdomAdoptRules.ExactCellLookup bounded = delegate(int x, int y)
			{
				var cell = cached(x, y);
				// Adopted rooms designate their floor, not the neighboring walls they retain.
				if (!scope.Contains(Pack(x, y)) && cell.Region == KingdomAdoptRules.EnclosureRegion.Membership)
					return new KingdomAdoptRules.CellObservation(KingdomAdoptRules.EnclosureRegion.Outside);
				return cell;
			};
			Dictionary<long, Room> byCell = new Dictionary<long, Room>();
			List<Room> rooms = new List<Room>();
			for (int i = 0; i < Places.Count; i++)
			{
				SleepingPlace place = Places[i];
				if (place.Capacity <= 0 || place.Capacity > 4000
					|| result.SleepingPlaces > 4000 - place.Capacity) return new Reading();
				result.SleepingPlaces += place.Capacity;
				long key = Pack(place.X, place.Y);
				if (!byCell.TryGetValue(key, out Room room))
				{
					room = new Room { Shape = KingdomAdoptRules.MeasureExactEnclosure(
						place.X, place.Y, bounded, scope.Count) };
					rooms.Add(room);
					if (room.Shape.UsableFloorCells != null)
						foreach (ArchitecturePoint point in room.Shape.UsableFloorCells)
							if (accessible.Contains(Pack(point.X, point.Y)))
								room.Reached.Add(Pack(point.X, point.Y));
					room.Shape.UsableCells = room.Reached.Count;
					if (room.Shape.FloorCells != null)
						for (int c = 0; c < room.Shape.FloorCells.Count; c++)
						{
							ArchitecturePoint point = room.Shape.FloorCells[c];
							byCell[Pack(point.X, point.Y)] = room;
						}
					byCell[key] = room;
				}
				room.Places += place.Capacity;
				if (room.Shape.Bounded && room.Shape.DoorCells > 0
					&& room.Shape.RoomCells >= KingdomAdoptRules.MinEnclosedRoomCells
					&& !room.Reached.Contains(Pack(place.X - 1, place.Y))
					&& !room.Reached.Contains(Pack(place.X + 1, place.Y))
					&& !room.Reached.Contains(Pack(place.X, place.Y - 1))
					&& !room.Reached.Contains(Pack(place.X, place.Y + 1)))
					result.UnusablePlaces += place.Capacity;
			}
			bool single = true;
			bool paired = true;
			KingdomLodgingRules.Closeness density = KingdomLodgingRules.Closeness.Private;
			for (int i = 0; i < rooms.Count; i++)
			{
				Room room = rooms[i];
				if (!room.Shape.Bounded)
				{
					result.ExposedPlaces += room.Places;
					continue;
				}
				result.SleepingRooms++;
				if (room.Shape.DoorCells == 0
					|| room.Shape.RoomCells < KingdomAdoptRules.MinEnclosedRoomCells)
					result.UnusablePlaces += room.Places;
				result.UsableFloorCells += room.Shape.UsableCells;
				single &= room.Places == 1;
				paired &= room.Places <= 2;
				var measured = KingdomLodgingRules.ClosenessFromDensity(
					room.Shape.UsableCells, room.Places);
				if (measured < density) density = measured;
			}
			if (result.SleepingPlaces == 0 || result.ExposedPlaces != 0
				|| result.UnusablePlaces != 0) return result;
			var separation = single ? KingdomLodgingRules.Closeness.Private
				: paired && result.SleepingRooms > 1 ? KingdomLodgingRules.Closeness.Roomed
				: KingdomLodgingRules.Closeness.Close;
			result.Quarters = density < separation ? density : separation;
			return result;
		}

		// Internal doors connect rooms; only a clear approach outside the designation seeds access.
		private static HashSet<long> ReachFromBoundary(HashSet<long> Scope,
			KingdomAdoptRules.ExactCellLookup Lookup)
		{
			HashSet<long> allowed = new HashSet<long>();
			foreach (long key in Scope)
			{
				int x = (int)(key >> 32), y = (int)key;
				var cell = Lookup(x, y);
				if (cell.Region == KingdomAdoptRules.EnclosureRegion.Ingress
					|| cell.Region == KingdomAdoptRules.EnclosureRegion.Membership && cell.Usable)
					allowed.Add(key);
				foreach (long neighbor in Neighbors(x, y))
					if (!Scope.Contains(neighbor) && Lookup((int)(neighbor >> 32), (int)neighbor).Region
						== KingdomAdoptRules.EnclosureRegion.Ingress) allowed.Add(neighbor);
			}
			HashSet<long> reached = new HashSet<long>();
			Queue<long> frontier = new Queue<long>();
			foreach (long key in allowed)
				foreach (long neighbor in Neighbors((int)(key >> 32), (int)key))
				{
					if (Scope.Contains(neighbor) || allowed.Contains(neighbor)) continue;
					var cell = Lookup((int)(neighbor >> 32), (int)neighbor);
					if (cell.Region == KingdomAdoptRules.EnclosureRegion.Membership
						&& cell.Usable && reached.Add(key)) frontier.Enqueue(key);
				}
			while (frontier.Count != 0)
			{
				long key = frontier.Dequeue();
				foreach (long neighbor in Neighbors((int)(key >> 32), (int)key))
					if (allowed.Contains(neighbor) && reached.Add(neighbor)) frontier.Enqueue(neighbor);
			}
			return reached;
		}

		private static IEnumerable<long> Neighbors(int X, int Y)
		{
			yield return Pack(X - 1, Y); yield return Pack(X + 1, Y);
			yield return Pack(X, Y - 1); yield return Pack(X, Y + 1);
		}

		private static long Pack(int X, int Y) => ((long)X << 32) | (uint)Y;
	}
}
