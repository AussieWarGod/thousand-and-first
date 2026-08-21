using System;
using System.Reflection;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What the living city costs in RAM, as a formula rather than a constant.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;0.0 states the ceiling and then says the thing that matters
	/// about it: <i>"the formula is the contract, not the constant"</i>. So the widths and counts
	/// of &sect;0.0(c)'s byte-by-byte table live here as named constants, the total is composed from
	/// them, and a caller — <c>kingdom:selftest</c> at W1 — checks a measurement against the
	/// formula evaluated at the LIVE caps, never against 53,444 or any other frozen figure.
	/// </para>
	/// <para>
	/// Pure, engine-free, allocation-free except for the reflected row sizer, which exists so the
	/// budgeted width of a row can be falsified by adding a field to it.
	/// </para>
	/// </summary>
	internal static class KingdomCityMemoryRules
	{
		// ---- LIVING-CITY-ARCHITECTURE §0.0(c), the table, row by row. -------------------------

		/// <summary>id ref 8 + district 4 + LastReadTick 8 + six stock/capacity longs 48 + roofs 4
		/// + defence 4 + pad 4. LIVING-CITY-ARCHITECTURE §0.0(c).</summary>
		internal const int ZoneRowBytes = 80;

		/// <summary>id 4 + zone ref 8 + anchor 4 + design ref 8 + condition 4 + crew 4 +
		/// RanThroughTick 8 + run-state 16 + pad 8. LIVING-CITY-ARCHITECTURE §0.0(c).</summary>
		internal const int WorkRowBytes = 64;

		/// <summary>The fields only. LIVING-CITY-ARCHITECTURE §0.0(c).</summary>
		internal const int ResidentRowStructBytes = 96;

		/// <summary>One unique heap string per resident. The only heap string in the model:
		/// zone ids and design keys are shared references. LIVING-CITY-ARCHITECTURE §0.0(c).</summary>
		internal const int ResidentNameBytes = 64;

		internal const int ResidentRowBytes = ResidentRowStructBytes + ResidentNameBytes;

		/// <summary>kind 4 + NextDueTick 8 + ordinal 4. LIVING-CITY-ARCHITECTURE §0.0(c).</summary>
		internal const int ClockRowBytes = 16;

		/// <summary>kind 4 + tick 8 + two subject ids 8 + place ref 8 + outcome 4.
		/// LIVING-CITY-ARCHITECTURE §0.0(c).</summary>
		internal const int ToldRowBytes = 32;

		/// <summary>Arrays and carrier headers, per city. LIVING-CITY-ARCHITECTURE §0.0(c).</summary>
		internal const int CityHeaderBytes = 256;

		/// <summary>LIVING-CITY-ARCHITECTURE §1.4, from <c>KingdomSettlement.MaxSettlements</c>.
		/// Copied by value here because the settlement type is not engine-free; the test asserts
		/// the copy still equals its source, which is the ladder idiom <c>KingdomExileRules</c>
		/// already uses for vanilla's reputation thresholds.</summary>
		internal const int CitiesPerRealm = 2;

		/// <summary>key 4 + kind 1 + zone ref 8 + object ref 8 + minted tick 8 + pad 3.
		/// LIVING-CITY-ARCHITECTURE §0.0(c) / §3.8.</summary>
		internal const int BindingRowBytes = 32;

		/// <summary>The registry's own headers, realm-scope. LIVING-CITY-ARCHITECTURE §0.0(c).</summary>
		internal const int BindingHeaderBytes = 128;

		/// <summary>LIVING-CITY-ARCHITECTURE §3.8: open jobs, realm-wide, and a closed job is
		/// evicted at once so absence from the registry is proof of closure.</summary>
		internal const int MaxOpenJobs = 16;

		/// <summary>LIVING-CITY-ARCHITECTURE §0.0(c) / §3.7: one job row's header.</summary>
		internal const int JobHeaderBytes = 64;

		/// <summary>zone ref 8 + enter 4 + exit 4 + length 4 + depart 8 + arrive 8.
		/// LIVING-CITY-ARCHITECTURE §0.0(c).</summary>
		internal const int LegBytes = 36;

		/// <summary>LIVING-CITY-ARCHITECTURE §3.7: a nine-zone city's diameter is four or five
		/// zone steps, and a job that wants more than six legs is refused at planning and told.</summary>
		internal const int MaxLegs = 6;

		internal const int JobRowBytes = JobHeaderBytes + (MaxLegs * LegBytes);

		/// <summary>A <c>ushort</c> per entry. LIVING-CITY-ARCHITECTURE §0.0(c) / §3.10.</summary>
		internal const int DistanceEntryBytes = 2;

		/// <summary>work-to-edge entries, per city. LIVING-CITY-ARCHITECTURE §0.0(c).</summary>
		internal const int DistanceWorkEdgeEntries = 540;

		/// <summary>same-zone work pairs, per city. LIVING-CITY-ARCHITECTURE §0.0(c).</summary>
		internal const int DistanceSameZoneEntries = 900;

		/// <summary>the zone all-pairs table: at most nine nodes, so at most eighty-one entries.
		/// LIVING-CITY-ARCHITECTURE §0.0(c) / §3.10.</summary>
		internal const int DistanceZonePairEntries = 81;

		/// <summary>LIVING-CITY-ARCHITECTURE §0.0(c) / §3.11.</summary>
		internal const int NetworkNodeBytes = 16;

		internal const int NetworkEdgeBytes = 16;

		internal const int NetworkHeaderBytes = 32;

		/// <summary>The nine-zone reading of LIVING-CITY-ARCHITECTURE §0.0(f): one whole parasang,
		/// caps scaled with it. Named so the formula can be evaluated at a size the rules do not
		/// permit today, which is the only way to show that nothing here scales with the city.</summary>
		internal const int FullParasangZones = 9;

		internal const int FullParasangWorks = 90;

		internal const int FullParasangResidents = 135;

		/// <summary>
		/// One city's book: rows plus the told-log ring plus the carrier headers.
		/// <para>
		/// Refuses a negative count rather than returning a smaller answer, because a memory
		/// formula that can under-report is worse than one that will not answer.
		/// </para>
		/// </summary>
		internal static bool TryCityModelBytes(int zones, int works, int residents, int clocks, out long bytes)
		{
			bytes = 0L;
			if (zones < 0 || works < 0 || residents < 0 || clocks < 0)
			{
				return false;
			}
			bytes = (long)zones * ZoneRowBytes
				+ (long)works * WorkRowBytes
				+ (long)residents * ResidentRowBytes
				+ (long)clocks * ClockRowBytes
				+ (long)KingdomCityState.MaxToldEntries * ToldRowBytes
				+ CityHeaderBytes;
			return true;
		}

		/// <summary>The binding registry, realm-scope: every city's residents plus the open jobs.
		/// LIVING-CITY-ARCHITECTURE §3.8.</summary>
		internal static bool TryRegistryBytes(int residentsPerCity, int cities, int openJobs, out long bytes)
		{
			bytes = 0L;
			if (residentsPerCity < 0 || cities < 0 || openJobs < 0)
			{
				return false;
			}
			bytes = ((long)residentsPerCity * cities + openJobs) * BindingRowBytes + BindingHeaderBytes;
			return true;
		}

		internal static bool TryJobBytes(int openJobs, out long bytes)
		{
			bytes = 0L;
			if (openJobs < 0)
			{
				return false;
			}
			bytes = (long)openJobs * JobRowBytes;
			return true;
		}

		/// <summary>
		/// The two-level distance matrix. Deliberately not <c>works²</c>: work-to-edge plus
		/// same-zone pairs plus the zone all-pairs table, from which any cross-zone distance
		/// composes in O(1). LIVING-CITY-ARCHITECTURE §3.10(2).
		/// </summary>
		internal static bool TryDistanceMatrixBytes(int cities, out long bytes)
		{
			bytes = 0L;
			if (cities < 0)
			{
				return false;
			}
			bytes = (long)cities
				* (DistanceWorkEdgeEntries + DistanceSameZoneEntries + DistanceZonePairEntries)
				* DistanceEntryBytes;
			return true;
		}

		internal static bool TryNetworkBytes(int cities, out long bytes)
		{
			bytes = 0L;
			if (cities < 0)
			{
				return false;
			}
			long perNetwork = (long)KingdomBudgetRules.NetworkMaxNodes * NetworkNodeBytes
				+ (long)KingdomBudgetRules.NetworkMaxEdges * NetworkEdgeBytes
				+ NetworkHeaderBytes;
			bytes = (long)cities * KingdomBudgetRules.NetworksPerCity * perNetwork;
			return true;
		}

		/// <summary>
		/// Everything &sect;0.0's "Model in RAM" row is answerable for: model + registry +
		/// itineraries + distance matrix + network graphs, per realm.
		/// </summary>
		internal static bool TryRealmBytes(
			int cities,
			int zonesPerCity,
			int worksPerCity,
			int residentsPerCity,
			int clocksPerCity,
			int openJobs,
			out long bytes)
		{
			bytes = 0L;
			long city;
			long registry;
			long jobs;
			long distance;
			long networks;
			if (cities < 0
				|| !TryCityModelBytes(zonesPerCity, worksPerCity, residentsPerCity, clocksPerCity, out city)
				|| !TryRegistryBytes(residentsPerCity, cities, openJobs, out registry)
				|| !TryJobBytes(openJobs, out jobs)
				|| !TryDistanceMatrixBytes(cities, out distance)
				|| !TryNetworkBytes(cities, out networks))
			{
				return false;
			}
			bytes = (long)cities * city + registry + jobs + distance + networks;
			return true;
		}

		/// <summary>The realm total at the caps the rules enforce today.
		/// LIVING-CITY-ARCHITECTURE §0.0(c).</summary>
		internal static bool TryRealmBytesAtTodaysCaps(out long bytes)
		{
			return TryRealmBytes(
				CitiesPerRealm,
				KingdomCityState.MaxZones,
				KingdomCityState.MaxWorks,
				KingdomCityState.MaxResidents,
				KingdomCityState.MaxClocks,
				MaxOpenJobs,
				out bytes);
		}

		/// <summary>The same formula at one whole parasang, caps scaled with it.
		/// LIVING-CITY-ARCHITECTURE §0.0(c) and §0.0(f).</summary>
		internal static bool TryRealmBytesAtFullParasang(out long bytes)
		{
			return TryRealmBytes(
				CitiesPerRealm,
				FullParasangZones,
				FullParasangWorks,
				FullParasangResidents,
				KingdomCityState.MaxClocks,
				MaxOpenJobs,
				out bytes);
		}

		/// <summary>
		/// What one row type actually declares, summed over its fields: eight bytes for a
		/// reference, the primitive's own width otherwise, recursing into nested value types.
		/// <para>
		/// Padding is not modelled, so this is a lower bound on the real width and is compared
		/// against the budget rather than equated to it. Its whole job is to make a budget
		/// falsifiable: add a <c>long</c> to a row and the row exceeds the width &sect;0.0(c)
		/// bought it, and something has to give — the field, or the table.
		/// </para>
		/// </summary>
		internal static bool TryMeasureDeclaredRowBytes(Type rowType, out int bytes)
		{
			bytes = 0;
			if (rowType == null)
			{
				return false;
			}
			return TryMeasure(rowType, 0, out bytes);
		}

		private static bool TryMeasure(Type type, int depth, out int bytes)
		{
			bytes = 0;
			if (depth > 4)
			{
				return false;
			}
			if (!type.IsValueType)
			{
				bytes = 8;
				return true;
			}
			if (type.IsEnum)
			{
				return TryMeasure(Enum.GetUnderlyingType(type), depth + 1, out bytes);
			}
			if (type.IsPrimitive)
			{
				bytes = PrimitiveBytes(type);
				return bytes > 0;
			}
			int total = 0;
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			for (int i = 0; i < fields.Length; i++)
			{
				int fieldBytes;
				if (!TryMeasure(fields[i].FieldType, depth + 1, out fieldBytes))
				{
					return false;
				}
				total += fieldBytes;
			}
			bytes = total;
			return true;
		}

		private static int PrimitiveBytes(Type type)
		{
			if (type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte))
			{
				return 1;
			}
			if (type == typeof(short) || type == typeof(ushort) || type == typeof(char))
			{
				return 2;
			}
			if (type == typeof(int) || type == typeof(uint) || type == typeof(float))
			{
				return 4;
			}
			if (type == typeof(long) || type == typeof(ulong) || type == typeof(double))
			{
				return 8;
			}
			return 0;
		}
	}
}
