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
					(x, y) => ReadFurnishedRoomCell(Z, x, y)));
			}
		}

		// Architectural circulation reserves furniture even when Qud lets bodies walk over it.
		// Keep the adoption reader's structural authority and native movement rules unchanged.
		internal static KingdomAdoptRules.CellObservation ReadFurnishedRoomCell(Zone Z, int X, int Y)
		{
			var reading = KingdomAdopt.ReadCellObservation(Z, X, Y);
			Cell cell = Z.GetCell(X, Y);
			if (cell == null) return reading;
			foreach (GameObject item in cell.GetObjects())
			{
				if (!GameObject.Validate(item) || item.IsCreature || item.IsPlayer() || item.IsDoor()) continue;
				if (!item.HasTagOrProperty("Furniture") && !item.HasPart("Bed") && !item.HasPart("Chair")) continue;
				reading.Usable = false;
				if (reading.Region == KingdomAdoptRules.EnclosureRegion.Ingress)
					reading.Region = KingdomAdoptRules.EnclosureRegion.Shell;
				break;
			}
			return reading;
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
