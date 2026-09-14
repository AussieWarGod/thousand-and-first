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
		}

		// Uses operable physical sleeping providers. A catalogue capacity is not a bed location.
		public static Reading Measure(IReadOnlyList<KingdomBenefitCell> Designation,
			IReadOnlyList<SleepingPlace> Places, KingdomAdoptRules.ExactCellLookup Lookup)
		{
			Reading result = new Reading();
			if (Designation == null || Places == null || Lookup == null
				|| Designation.Count > 4000 || Places.Count > 4000) return result;
			HashSet<long> scope = new HashSet<long>();
			for (int i = 0; i < Designation.Count; i++)
				if ((Designation[i].Use & KingdomBenefitCellUse.Plot) != 0)
					scope.Add(Pack(Designation[i].X, Designation[i].Y));
			Dictionary<long, KingdomAdoptRules.CellObservation> observed =
				new Dictionary<long, KingdomAdoptRules.CellObservation>();
			KingdomAdoptRules.ExactCellLookup bounded = delegate(int x, int y)
			{
				long key = Pack(x, y);
				if (!observed.TryGetValue(key, out var cell))
				{
					cell = Lookup(x, y);
					// Adopted rooms designate their floor, not the neighboring walls they retain.
					if (!scope.Contains(key) && cell.Region == KingdomAdoptRules.EnclosureRegion.Membership)
						cell = new KingdomAdoptRules.CellObservation(KingdomAdoptRules.EnclosureRegion.Outside);
					observed.Add(key, cell);
				}
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
					if (room.Shape.FloorCells != null)
						for (int c = 0; c < room.Shape.FloorCells.Count; c++)
						{
							ArchitecturePoint point = room.Shape.FloorCells[c];
							byCell[Pack(point.X, point.Y)] = room;
						}
					byCell[key] = room;
				}
				room.Places += place.Capacity;
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

		private static long Pack(int X, int Y) => ((long)X << 32) | (uint)Y;
	}
}
