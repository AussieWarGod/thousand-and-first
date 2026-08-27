
namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{
		/// <summary>
		/// Writes one frozen snapshot into the columns, in one call and after the rules have
		/// succeeded. The single publisher &sect;1.3 requires.
		/// </summary>
		internal bool TryPublish(KingdomCityState state, out KingdomCityFault fault)
		{
			if (state == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			SchemaVersion = state.SchemaVersion;
			RulesVersion = state.RulesVersion;
			SettlementId = state.SettlementId ?? "";
			ProcessedThroughTick = state.ProcessedThroughTick;
			WaterLevel = state.Stocks.Water.Level;
			WaterCapacity = state.Stocks.Water.Capacity;
			FoodLevel = state.Stocks.Food.Level;
			FoodCapacity = state.Stocks.Food.Capacity;
			MaterialsLevel = state.Stocks.Materials.Level;
			MaterialsCapacity = state.Stocks.Materials.Capacity;

			Clear();
			for (int i = 0; i < state.ZoneCount; i++)
			{
				KingdomZoneRow row;
				if (!state.TryZone(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				ZoneIds.Add(row.ZoneId ?? "");
				ZoneDistrictCodes.Add(row.DistrictCode);
				ZoneLastReadTicks.Add(row.LastReadTick);
				ZoneWaterLevels.Add(row.Stocks.Water.Level);
				ZoneWaterCapacities.Add(row.Stocks.Water.Capacity);
				ZoneFoodLevels.Add(row.Stocks.Food.Level);
				ZoneFoodCapacities.Add(row.Stocks.Food.Capacity);
				ZoneMaterialsLevels.Add(row.Stocks.Materials.Level);
				ZoneMaterialsCapacities.Add(row.Stocks.Materials.Capacity);
				ZoneRoofs.Add(row.Roofs);
				ZoneDefences.Add(row.Defence);
				ZoneWaterCarries.Add(row.WaterCarry);
				ZoneFoodCarries.Add(row.FoodCarry);
				ZoneOwedWater.Add(row.OwedWater);
				ZoneOwedFood.Add(row.OwedFood);
				ZoneOwedMaterials.Add(row.OwedMaterials);
			}
			for (int i = 0; i < state.WorkCount; i++)
			{
				KingdomWorkRow row;
				if (!state.TryWork(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				WorkIds.Add(row.WorkId);
				WorkZoneIds.Add(row.ZoneId ?? "");
				WorkAnchorsX.Add(row.AnchorX);
				WorkAnchorsY.Add(row.AnchorY);
				WorkDesignKeys.Add(row.DesignKey ?? "");
				WorkConditions.Add(row.ConditionPercent);
				WorkCrews.Add(row.CrewAssigned);
				WorkRanThroughTicks.Add(row.RanThroughTick);
				WorkKinds.Add((int)row.RunState.Kind);
				WorkStages.Add(row.RunState.Stage);
				WorkProgress.Add(row.RunState.Progress);
				WorkNextTicks.Add(row.RunState.NextTick);
			}
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				ResidentIds.Add(row.ResidentId);
				ResidentNames.Add(row.Name ?? "");
				ResidentOrigins.Add(row.Origin ?? "");
				ResidentOriginCodes.Add(row.OriginCode);
				ResidentCreedCodes.Add(row.CreedCode);
				ResidentArrivedTicks.Add(row.ArrivedTick);
				ResidentArrived.Add(row.Arrived ?? "");
				ResidentHomeWorkIds.Add(row.HomeWorkId);
				ResidentJobWorkIds.Add(row.JobWorkId);
				ResidentJobRoles.Add(row.JobRole);
				ResidentDayShapes.Add((int)row.DayShape);
				ResidentStandings.Add((int)row.Standing);
				ResidentCauses.Add((int)row.Cause);
				ResidentBoundZoneIds.Add(row.BoundZoneId ?? "");
				ResidentRoofStanding.Add(row.RoofBrink.Stands ? 1 : 0);
				ResidentRoofTicks.Add(row.RoofBrink.ReachedTick);
				ResidentRoofWarnedTicks.Add(row.RoofBrink.WarnedTick);
				ResidentCreedStanding.Add(row.CreedBrink.Stands ? 1 : 0);
				ResidentCreedTicks.Add(row.CreedBrink.ReachedTick);
				ResidentCreedWarnedTicks.Add(row.CreedBrink.WarnedTick);
				ResidentCreedToward.Add(row.CreedToward ?? "");
				ResidentCreedChannels.Add(row.CreedChannel);
				ResidentKeptCreeds.Add(row.KeptCreeds ?? "");
			}
			for (int i = 0; i < state.ClockCount; i++)
			{
				KingdomClockRow row;
				if (!state.TryClock(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				ClockKinds.Add((int)row.Kind);
				ClockNextDueTicks.Add(row.NextDueTick);
				ClockOrdinals.Add(row.Ordinal);
			}
			for (int i = 0; i < state.ToldCount; i++)
			{
				KingdomToldRow row;
				if (!state.TryTold(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				ToldKinds.Add((int)row.Kind);
				ToldTicks.Add(row.Tick);
				ToldSubjectsA.Add(row.SubjectA);
				ToldSubjectsB.Add(row.SubjectB);
				ToldPlaceZoneIds.Add(row.PlaceZoneId ?? "");
				ToldOutcomes.Add(row.Outcome);
			}
			fault = KingdomCityFault.None;
			return true;
		}
	}
}
