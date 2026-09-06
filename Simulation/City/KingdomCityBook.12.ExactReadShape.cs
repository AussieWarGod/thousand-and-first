using System.Collections;
using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{
		/// <summary>Authority read without migration, truncation, repair or metadata mutation.
		/// Every projected column must already have its exact bounded shape.</summary>
		internal bool TryReadExact(out KingdomCityState state, out KingdomCityFault fault)
		{
			state = null; fault = KingdomCityFault.InvalidIndex;
			if (!ExactColumns(KingdomCityState.MaxZones, ZoneIds, ZoneDistrictCodes,
				ZoneLastReadTicks, ZoneWaterLevels, ZoneWaterCapacities, ZoneFoodLevels,
				ZoneFoodCapacities, ZoneMaterialsLevels, ZoneMaterialsCapacities,
				ZoneRoofs, ZoneDefences, ZoneWaterCarries, ZoneFoodCarries,
				ZoneOwedWater, ZoneOwedFood, ZoneOwedMaterials)
				|| !ExactColumns(KingdomCityState.MaxWorks, WorkIds, WorkZoneIds, WorkAnchorsX,
					WorkAnchorsY, WorkDesignKeys, WorkConditions, WorkCrews, WorkRanThroughTicks,
					WorkKinds, WorkStages, WorkProgress, WorkNextTicks)
				|| !ExactColumns(KingdomCityState.MaxResidents, ResidentIds, ResidentNames, ResidentOrigins,
					ResidentOriginCodes, ResidentCreedCodes, ResidentKeptCreeds, ResidentArrivedTicks,
					ResidentArrived, ResidentHomeWorkIds, ResidentJobWorkIds, ResidentJobRoles,
					ResidentDayShapes, ResidentStandings, ResidentCauses, ResidentBoundZoneIds,
					ResidentRoofStanding, ResidentRoofTicks, ResidentRoofWarnedTicks, ResidentCreedStanding,
					ResidentCreedTicks, ResidentCreedWarnedTicks, ResidentCreedToward, ResidentCreedChannels)
				|| !ExactColumns(KingdomCityState.MaxClocks, ClockKinds, ClockNextDueTicks, ClockOrdinals)
				|| !ExactColumns(KingdomCityState.MaxToldEntries, ToldKinds, ToldTicks,
					ToldSubjectsA, ToldSubjectsB, ToldPlaceZoneIds, ToldOutcomes)) return false;
			if (!ExactResidentColumns()) return false;
			return TryReadProjected(out state, out fault);
		}

		private bool ExactResidentColumns()
		{
			HashSet<int> identities = new HashSet<int>();
			for (int i = 0; i < ResidentIds.Count; i++)
			{
				if (ResidentIds[i] <= 0 || !identities.Add(ResidentIds[i])
					|| ResidentNames[i] == null || ResidentOrigins[i] == null
					|| ResidentArrived[i] == null || ResidentBoundZoneIds[i] == null
					|| ResidentCreedToward[i] == null || ResidentKeptCreeds[i] == null
					|| !DefinedIn(typeof(KingdomResidentStanding), ResidentStandings[i])
					|| !DefinedIn(typeof(KingdomStandingCause), ResidentCauses[i])
					|| !KingdomResidentRules.CauseFits((KingdomResidentStanding)ResidentStandings[i],
						(KingdomStandingCause)ResidentCauses[i])
					|| !ExactBrink(ResidentRoofStanding[i], ResidentRoofTicks[i], ResidentRoofWarnedTicks[i])
					|| !ExactBrink(ResidentCreedStanding[i], ResidentCreedTicks[i], ResidentCreedWarnedTicks[i])
					|| ResidentCreedStanding[i] == 0
						&& (ResidentCreedToward[i] != "" || ResidentCreedChannels[i] != 0)) return false;
			}
			return true;
		}

		private static bool ExactBrink(int standing, long tick, long warned)
		{
			return standing == 1 || standing == 0 && tick == 0 && warned == 0;
		}

		private static bool ExactColumns(int cap, params ICollection[] columns)
		{
			int count = columns[0]?.Count ?? -1;
			if (count < 0 || count > cap) return false;
			for (int i = 1; i < columns.Length; i++)
				if (columns[i] == null || columns[i].Count != count) return false;
			return true;
		}
	}
}
