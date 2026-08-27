
namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{
		private void NormalizeSidecarFields()
		{
			// Null is an absent named field from a pre-v3 save. Non-empty data, including malformed
			// data, is retained for the behaviour host to diagnose rather than repaired here.
			if (ExtensionModel == null)
			{
				ExtensionModel = "";
			}
			if (ExtensionHappeningCursors == null)
			{
				ExtensionHappeningCursors = "";
			}
			if (HappeningModel == null)
			{
				HappeningModel = "";
			}
		}

		private void NormalizeZoneColumns()
		{
			ZoneIds = Repair(ZoneIds);
			ZoneDistrictCodes = Repair(ZoneDistrictCodes);
			ZoneLastReadTicks = Repair(ZoneLastReadTicks);
			ZoneWaterLevels = Repair(ZoneWaterLevels);
			ZoneWaterCapacities = Repair(ZoneWaterCapacities);
			ZoneFoodLevels = Repair(ZoneFoodLevels);
			ZoneFoodCapacities = Repair(ZoneFoodCapacities);
			ZoneMaterialsLevels = Repair(ZoneMaterialsLevels);
			ZoneMaterialsCapacities = Repair(ZoneMaterialsCapacities);
			ZoneRoofs = Repair(ZoneRoofs);
			ZoneDefences = Repair(ZoneDefences);
			ZoneWaterCarries = Repair(ZoneWaterCarries);
			ZoneFoodCarries = Repair(ZoneFoodCarries);
			ZoneOwedWater = Repair(ZoneOwedWater);
			ZoneOwedFood = Repair(ZoneOwedFood);
			ZoneOwedMaterials = Repair(ZoneOwedMaterials);
			int zones = Shortest(new int[16]
			{
				ZoneIds.Count, ZoneDistrictCodes.Count, ZoneLastReadTicks.Count,
				ZoneWaterLevels.Count, ZoneWaterCapacities.Count, ZoneFoodLevels.Count,
				ZoneFoodCapacities.Count, ZoneMaterialsLevels.Count, ZoneMaterialsCapacities.Count,
				ZoneRoofs.Count, ZoneDefences.Count, ZoneWaterCarries.Count, ZoneFoodCarries.Count,
				ZoneOwedWater.Count, ZoneOwedFood.Count, ZoneOwedMaterials.Count
			});
			if (zones > KingdomCityState.MaxZones)
			{
				zones = KingdomCityState.MaxZones;
			}
			Trim(ZoneIds, zones);
			Trim(ZoneDistrictCodes, zones);
			Trim(ZoneLastReadTicks, zones);
			Trim(ZoneWaterLevels, zones);
			Trim(ZoneWaterCapacities, zones);
			Trim(ZoneFoodLevels, zones);
			Trim(ZoneFoodCapacities, zones);
			Trim(ZoneMaterialsLevels, zones);
			Trim(ZoneMaterialsCapacities, zones);
			Trim(ZoneRoofs, zones);
			Trim(ZoneDefences, zones);
			Trim(ZoneWaterCarries, zones);
			Trim(ZoneFoodCarries, zones);
			Trim(ZoneOwedWater, zones);
			Trim(ZoneOwedFood, zones);
			Trim(ZoneOwedMaterials, zones);
		}

		private void NormalizeWorkColumns()
		{
			WorkIds = Repair(WorkIds);
			WorkZoneIds = Repair(WorkZoneIds);
			WorkAnchorsX = Repair(WorkAnchorsX);
			WorkAnchorsY = Repair(WorkAnchorsY);
			WorkDesignKeys = Repair(WorkDesignKeys);
			WorkConditions = Repair(WorkConditions);
			WorkCrews = Repair(WorkCrews);
			WorkRanThroughTicks = Repair(WorkRanThroughTicks);
			WorkKinds = Repair(WorkKinds);
			WorkStages = Repair(WorkStages);
			WorkProgress = Repair(WorkProgress);
			WorkNextTicks = Repair(WorkNextTicks);
			int works = Shortest(new int[12]
			{
				WorkIds.Count, WorkZoneIds.Count, WorkAnchorsX.Count, WorkAnchorsY.Count,
				WorkDesignKeys.Count, WorkConditions.Count, WorkCrews.Count, WorkRanThroughTicks.Count,
				WorkKinds.Count, WorkStages.Count, WorkProgress.Count, WorkNextTicks.Count
			});
			if (works > KingdomCityState.MaxWorks)
			{
				works = KingdomCityState.MaxWorks;
			}
			Trim(WorkIds, works);
			Trim(WorkZoneIds, works);
			Trim(WorkAnchorsX, works);
			Trim(WorkAnchorsY, works);
			Trim(WorkDesignKeys, works);
			Trim(WorkConditions, works);
			Trim(WorkCrews, works);
			Trim(WorkRanThroughTicks, works);
			Trim(WorkKinds, works);
			Trim(WorkStages, works);
			Trim(WorkProgress, works);
			Trim(WorkNextTicks, works);
		}
	}
}
