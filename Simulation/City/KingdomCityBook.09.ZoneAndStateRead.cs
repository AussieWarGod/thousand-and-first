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
			if (!TryValidateColumnDomains(out fault))
			{
				return false;
			}
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

		/// <summary>
		/// Refuses a column value this build has no member for, BEFORE any cast reads one.
		/// <para>
		/// <see cref="Normalize"/> repairs shape &mdash; a null, ragged or over-cap column &mdash;
		/// and nothing else; a value is a different failure. The casts in <see cref="TryRead"/>
		/// narrow an <c>int</c> column into a byte-backed enum, a <c>byte</c> or a <c>short</c>,
		/// and an unchecked cast truncates: a standing of 259 would read as <c>Expedition</c> and
		/// load as a healthy row. That is a corrupt save, or one written by a later build, looking
		/// healthy &mdash; the very reading <c>KingdomCityState.TryWithProcessedThroughTick</c>
		/// refuses for the clock, and the one the wire codec refuses for an undefined enum byte.
		/// The fault is <see cref="KingdomCityFault.InvalidIndex"/>: the value indexes a
		/// vocabulary, or a slot width, that has no such member.
		/// </para>
		/// </summary>
		private bool TryValidateColumnDomains(out KingdomCityFault fault)
		{
			for (int i = 0; i < WorkIds.Count; i++)
			{
				if (!Within(WorkAnchorsX[i], short.MinValue, short.MaxValue)
					|| !Within(WorkAnchorsY[i], short.MinValue, short.MaxValue)
					|| !DefinedIn(typeof(KingdomWorkKind), WorkKinds[i])
					|| !Within(WorkStages[i], byte.MinValue, byte.MaxValue))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
			}
			for (int i = 0; i < ResidentIds.Count; i++)
			{
				if (!Within(ResidentJobRoles[i], byte.MinValue, byte.MaxValue)
					|| !DefinedIn(typeof(KingdomDayShape), ResidentDayShapes[i])
					|| !DefinedIn(typeof(KingdomResidentStanding), ResidentStandings[i])
					|| !DefinedIn(typeof(KingdomStandingCause), ResidentCauses[i])
					|| !Within(ResidentCreedChannels[i], byte.MinValue, byte.MaxValue))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
			}
			for (int i = 0; i < ClockKinds.Count; i++)
			{
				if (!DefinedIn(typeof(KingdomClockKind), ClockKinds[i]))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
			}
			for (int i = 0; i < ToldKinds.Count; i++)
			{
				if (!DefinedIn(typeof(KingdomToldKind), ToldKinds[i]))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
			}
			fault = KingdomCityFault.None;
			return true;
		}
	}
}
