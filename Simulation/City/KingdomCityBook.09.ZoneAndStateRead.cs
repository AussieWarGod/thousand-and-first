using System;

namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{
		/// <summary>The zone row for this id, or false. The one lookup every re-plumbed sighting
		/// reader goes through.</summary>
		public bool TryZoneRow(string zoneId, out int index)
		{
			index = -1;
			if (zoneId == null)
			{
				return false;
			}
			for (int i = 0; i < ZoneIds.Count; i++)
			{
				if (string.Equals(ZoneIds[i], zoneId, StringComparison.Ordinal))
				{
					index = i;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// The book as the frozen model the rules layer works on. Refuses and publishes nothing
		/// rather than handing back a half-built city.
		/// </summary>
		internal bool TryRead(out KingdomCityState state, out KingdomCityFault fault)
		{
			state = null;
			Normalize();
			KingdomZoneRow[] zones = new KingdomZoneRow[ZoneIds.Count];
			for (int i = 0; i < zones.Length; i++)
			{
				zones[i] = new KingdomZoneRow(
					ZoneIds[i],
					ZoneDistrictCodes[i],
					ZoneLastReadTicks[i],
					new KingdomStocks(
						new KingdomStockPair(ZoneWaterLevels[i], ZoneWaterCapacities[i]),
						new KingdomStockPair(ZoneFoodLevels[i], ZoneFoodCapacities[i]),
						new KingdomStockPair(ZoneMaterialsLevels[i], ZoneMaterialsCapacities[i])),
					ZoneRoofs[i],
					ZoneDefences[i],
					ZoneWaterCarries[i],
					ZoneFoodCarries[i],
					ZoneOwedWater[i],
					ZoneOwedFood[i],
					ZoneOwedMaterials[i]);
			}
			KingdomWorkRow[] works = new KingdomWorkRow[WorkIds.Count];
			for (int i = 0; i < works.Length; i++)
			{
				works[i] = new KingdomWorkRow(
					WorkIds[i],
					WorkZoneIds[i],
					(short)WorkAnchorsX[i],
					(short)WorkAnchorsY[i],
					WorkDesignKeys[i],
					WorkConditions[i],
					WorkCrews[i],
					WorkRanThroughTicks[i],
					new KingdomWorkRunState((KingdomWorkKind)WorkKinds[i], (byte)WorkStages[i], WorkProgress[i], WorkNextTicks[i]));
			}
			KingdomResidentRow[] residents = new KingdomResidentRow[ResidentIds.Count];
			for (int i = 0; i < residents.Length; i++)
			{
				residents[i] = new KingdomResidentRow(
					ResidentIds[i],
					ResidentNames[i],
					ResidentOriginCodes[i],
					ResidentCreedCodes[i],
					ResidentArrivedTicks[i],
					ResidentHomeWorkIds[i],
					ResidentJobWorkIds[i],
					(byte)ResidentJobRoles[i],
					(KingdomDayShape)ResidentDayShapes[i],
					(KingdomResidentStanding)ResidentStandings[i],
					(KingdomStandingCause)ResidentCauses[i],
					ResidentBoundZoneIds[i],
					new KingdomBrinkWindow(ResidentRoofStanding[i] != 0, ResidentRoofTicks[i], ResidentRoofWarnedTicks[i]),
					new KingdomBrinkWindow(ResidentCreedStanding[i] != 0, ResidentCreedTicks[i], ResidentCreedWarnedTicks[i]),
					ResidentCreedToward[i],
					(byte)ResidentCreedChannels[i],
					ResidentKeptCreeds[i],
					ResidentOrigins[i],
					ResidentArrived[i]);
			}
			KingdomClockRow[] clocks = new KingdomClockRow[ClockKinds.Count];
			for (int i = 0; i < clocks.Length; i++)
			{
				clocks[i] = new KingdomClockRow((KingdomClockKind)ClockKinds[i], ClockNextDueTicks[i], ClockOrdinals[i]);
			}
			KingdomCityState built;
			if (!KingdomCityState.TryCreate(
				SchemaVersion,
				RulesVersion,
				SettlementId,
				ProcessedThroughTick,
				new KingdomStocks(
					new KingdomStockPair(WaterLevel, WaterCapacity),
					new KingdomStockPair(FoodLevel, FoodCapacity),
					new KingdomStockPair(MaterialsLevel, MaterialsCapacity)),
				zones,
				works,
				residents,
				clocks,
				out built,
				out fault))
			{
				return false;
			}
			for (int i = 0; i < ToldKinds.Count; i++)
			{
				KingdomCityState told;
				if (!built.TryTell(
					new KingdomToldRow((KingdomToldKind)ToldKinds[i], ToldTicks[i], ToldSubjectsA[i], ToldSubjectsB[i], ToldPlaceZoneIds[i], ToldOutcomes[i]),
					out told,
					out fault))
				{
					return false;
				}
				built = told;
			}
			state = built;
			fault = KingdomCityFault.None;
			return true;
		}
	}
}
