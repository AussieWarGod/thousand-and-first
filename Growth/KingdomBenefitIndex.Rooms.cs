using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		private readonly Dictionary<string, KingdomLodgingRoomRules.Reading> SleepingRooms =
			new Dictionary<string, KingdomLodgingRoomRules.Reading>(StringComparer.Ordinal);

		private void MeasureSleepingRooms(Zone Z)
		{
			foreach (KeyValuePair<string, Aggregate> pair in ByRoot)
			{
				Aggregate row = pair.Value;
				if (row.SleepingPlaces.Count == 0 || AmountForRoot(pair.Key, "roof") == 0) continue;
				SleepingRooms.Add(pair.Key, KingdomLodgingRoomRules.Measure(
					row.Reading.Designation.Cells, row.SleepingPlaces,
					(x, y) => KingdomAdopt.ReadCellObservation(Z, x, y)));
			}
		}

		public KingdomLodgingRoomRules.Reading RoomReadingForRoot(string RootId)
		{
			if (!SleepingRooms.TryGetValue(RootId ?? "", out var reading))
				return new KingdomLodgingRoomRules.Reading();
			return new KingdomLodgingRoomRules.Reading {
				SleepingRooms = reading.SleepingRooms, SleepingPlaces = reading.SleepingPlaces,
				ExposedPlaces = reading.ExposedPlaces, UsableFloorCells = reading.UsableFloorCells,
				UnusablePlaces = reading.UnusablePlaces,
				Quarters = reading.Quarters };
		}
	}
}
