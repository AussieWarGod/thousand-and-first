using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{
		private void Clear()
		{
			ZoneIds.Clear();
			ZoneDistrictCodes.Clear();
			ZoneLastReadTicks.Clear();
			ZoneWaterLevels.Clear();
			ZoneWaterCapacities.Clear();
			ZoneFoodLevels.Clear();
			ZoneFoodCapacities.Clear();
			ZoneMaterialsLevels.Clear();
			ZoneMaterialsCapacities.Clear();
			ZoneRoofs.Clear();
			ZoneDefences.Clear();
			ZoneWaterCarries.Clear();
			ZoneFoodCarries.Clear();
			ZoneOwedWater.Clear();
			ZoneOwedFood.Clear();
			ZoneOwedMaterials.Clear();
			WorkIds.Clear();
			WorkZoneIds.Clear();
			WorkAnchorsX.Clear();
			WorkAnchorsY.Clear();
			WorkDesignKeys.Clear();
			WorkConditions.Clear();
			WorkCrews.Clear();
			WorkRanThroughTicks.Clear();
			WorkKinds.Clear();
			WorkStages.Clear();
			WorkProgress.Clear();
			WorkNextTicks.Clear();
			ResidentIds.Clear();
			ResidentNames.Clear();
			ResidentOrigins.Clear();
			ResidentOriginCodes.Clear();
			ResidentCreedCodes.Clear();
			ResidentArrivedTicks.Clear();
			ResidentArrived.Clear();
			ResidentHomeWorkIds.Clear();
			ResidentJobWorkIds.Clear();
			ResidentJobRoles.Clear();
			ResidentDayShapes.Clear();
			ResidentStandings.Clear();
			ResidentCauses.Clear();
			ResidentBoundZoneIds.Clear();
			ResidentRoofStanding.Clear();
			ResidentRoofTicks.Clear();
			ResidentRoofWarnedTicks.Clear();
			ResidentCreedStanding.Clear();
			ResidentCreedTicks.Clear();
			ResidentCreedWarnedTicks.Clear();
			ResidentCreedToward.Clear();
			ResidentCreedChannels.Clear();
			ResidentKeptCreeds.Clear();
			ClockKinds.Clear();
			ClockNextDueTicks.Clear();
			ClockOrdinals.Clear();
			ToldKinds.Clear();
			ToldTicks.Clear();
			ToldSubjectsA.Clear();
			ToldSubjectsB.Clear();
			ToldPlaceZoneIds.Clear();
			ToldOutcomes.Clear();
		}

		private static List<T> Repair<T>(List<T> column)
		{
			return column ?? new List<T>();
		}

		private static int Shortest(int[] counts)
		{
			int shortest = int.MaxValue;
			for (int i = 0; i < counts.Length; i++)
			{
				if (counts[i] < shortest)
				{
					shortest = counts[i];
				}
			}
			return (shortest == int.MaxValue) ? 0 : shortest;
		}

		private static void Trim<T>(List<T> column, int count)
		{
			if (column.Count > count)
			{
				column.RemoveRange(count, column.Count - count);
			}
		}

		private static void DropOldest<T>(List<T> column, int count)
		{
			int drop = (count < column.Count) ? count : column.Count;
			if (drop > 0)
			{
				column.RemoveRange(0, drop);
			}
		}
	}
}
