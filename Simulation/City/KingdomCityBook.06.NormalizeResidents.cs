
namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{
		private void NormalizeResidentColumns()
		{
			ResidentIds = Repair(ResidentIds);
			ResidentNames = Repair(ResidentNames);
			ResidentOrigins = Repair(ResidentOrigins);
			ResidentOriginCodes = Repair(ResidentOriginCodes);
			ResidentCreedCodes = Repair(ResidentCreedCodes);
			ResidentArrivedTicks = Repair(ResidentArrivedTicks);
			ResidentArrived = Repair(ResidentArrived);
			ResidentHomeWorkIds = Repair(ResidentHomeWorkIds);
			ResidentJobWorkIds = Repair(ResidentJobWorkIds);
			ResidentJobRoles = Repair(ResidentJobRoles);
			ResidentDayShapes = Repair(ResidentDayShapes);
			ResidentStandings = Repair(ResidentStandings);
			ResidentCauses = Repair(ResidentCauses);
			ResidentBoundZoneIds = Repair(ResidentBoundZoneIds);
			ResidentRoofStanding = Repair(ResidentRoofStanding);
			ResidentRoofTicks = Repair(ResidentRoofTicks);
			ResidentRoofWarnedTicks = Repair(ResidentRoofWarnedTicks);
			ResidentCreedStanding = Repair(ResidentCreedStanding);
			ResidentCreedTicks = Repair(ResidentCreedTicks);
			ResidentCreedWarnedTicks = Repair(ResidentCreedWarnedTicks);
			ResidentCreedToward = Repair(ResidentCreedToward);
			ResidentCreedChannels = Repair(ResidentCreedChannels);
			ResidentKeptCreeds = Repair(ResidentKeptCreeds);
			// V2 had the exact origin only as a closed code and no frozen arrival label. Fill the two
			// new V3 presentation columns from what V2 can prove before the ordinary square-column
			// normalization runs. No tick is parsed or invented.
			if (SchemaVersion < 3)
			{
				int oldRows = Shortest(new int[21]
				{
					ResidentIds.Count, ResidentNames.Count, ResidentOriginCodes.Count,
					ResidentCreedCodes.Count, ResidentArrivedTicks.Count, ResidentHomeWorkIds.Count,
					ResidentJobWorkIds.Count, ResidentJobRoles.Count, ResidentDayShapes.Count,
					ResidentStandings.Count, ResidentCauses.Count, ResidentBoundZoneIds.Count,
					ResidentRoofStanding.Count, ResidentRoofTicks.Count, ResidentRoofWarnedTicks.Count,
					ResidentCreedStanding.Count, ResidentCreedTicks.Count, ResidentCreedWarnedTicks.Count,
					ResidentCreedToward.Count, ResidentCreedChannels.Count, ResidentKeptCreeds.Count
				});
				while (ResidentOrigins.Count < oldRows)
				{
					ResidentOrigins.Add(KingdomResidentRules.OriginKey(
						ResidentOriginCodes[ResidentOrigins.Count]) ?? "");
				}
				while (ResidentArrived.Count < oldRows) ResidentArrived.Add("");
				SchemaVersion = KingdomCityRules.SchemaVersion;
			}
			int residents = Shortest(new int[23]
			{
				ResidentIds.Count, ResidentNames.Count, ResidentOrigins.Count,
				ResidentOriginCodes.Count, ResidentCreedCodes.Count, ResidentArrivedTicks.Count,
				ResidentArrived.Count, ResidentHomeWorkIds.Count,
				ResidentJobWorkIds.Count, ResidentJobRoles.Count, ResidentDayShapes.Count,
				ResidentStandings.Count, ResidentCauses.Count, ResidentBoundZoneIds.Count,
				ResidentRoofStanding.Count, ResidentRoofTicks.Count, ResidentRoofWarnedTicks.Count,
				ResidentCreedStanding.Count, ResidentCreedTicks.Count, ResidentCreedWarnedTicks.Count,
				ResidentCreedToward.Count, ResidentCreedChannels.Count, ResidentKeptCreeds.Count
			});
			if (residents > KingdomCityState.MaxResidents)
			{
				residents = KingdomCityState.MaxResidents;
			}
			Trim(ResidentIds, residents);
			Trim(ResidentNames, residents);
			Trim(ResidentOrigins, residents);
			Trim(ResidentOriginCodes, residents);
			Trim(ResidentCreedCodes, residents);
			Trim(ResidentArrivedTicks, residents);
			Trim(ResidentArrived, residents);
			Trim(ResidentHomeWorkIds, residents);
			Trim(ResidentJobWorkIds, residents);
			Trim(ResidentJobRoles, residents);
			Trim(ResidentDayShapes, residents);
			Trim(ResidentStandings, residents);
			Trim(ResidentCauses, residents);
			Trim(ResidentBoundZoneIds, residents);
			Trim(ResidentRoofStanding, residents);
			Trim(ResidentRoofTicks, residents);
			Trim(ResidentRoofWarnedTicks, residents);
			Trim(ResidentCreedStanding, residents);
			Trim(ResidentCreedTicks, residents);
			Trim(ResidentCreedWarnedTicks, residents);
			Trim(ResidentCreedToward, residents);
			Trim(ResidentCreedChannels, residents);
			Trim(ResidentKeptCreeds, residents);
			for (int i = 0; i < residents; i++)
			{
				if (ResidentNames[i] == null)
				{
					ResidentNames[i] = "";
				}
				if (ResidentOrigins[i] == null) ResidentOrigins[i] = "";
				if (ResidentArrived[i] == null) ResidentArrived[i] = "";
				if (ResidentBoundZoneIds[i] == null)
				{
					ResidentBoundZoneIds[i] = "";
				}
				// A row whose standing and cause disagree is repaired toward the STANDING, because
				// the standing is what every consumer branches on and a mismatched cause would let
				// a living settler carry a death clause into a memorial. A standing or cause this
				// build has no member for is not read here at all: the cast would truncate it into
				// a member it never was, and TryRead refuses the row rather than repairing it.
				if (DefinedIn(typeof(KingdomResidentStanding), ResidentStandings[i])
					&& DefinedIn(typeof(KingdomStandingCause), ResidentCauses[i])
					&& !KingdomResidentRules.CauseFits((KingdomResidentStanding)ResidentStandings[i], (KingdomStandingCause)ResidentCauses[i]))
				{
					ResidentCauses[i] = (int)DefaultCauseFor((KingdomResidentStanding)ResidentStandings[i]);
				}
			}
		}
	}
}
