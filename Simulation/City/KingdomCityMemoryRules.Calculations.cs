using System;
using System.Reflection;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCityMemoryRules
	{
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
				+ (long)KingdomBudgetRules.NetworkMaxNodes * NetworkTraversalBytesPerNode
				+ NetworkHeaderBytes;
			bytes = (long)cities * KingdomBudgetRules.NetworksPerCity * perNetwork;
			return true;
		}

		/// <summary>
		/// What the keepers cost, per city: the header's seven fields, the bounded roster-string
		/// payload, plus the shelf at its cap.
		/// <para>
		/// Its own line rather than a term folded into <see cref="TryCityModelBytes"/>, because the
		/// state is not in the city's book &mdash; it sits on the settlement container, which is
		/// where Addendum 22 B1 sited it so that secession, rejoin and exile move the rolls by
		/// moving the container. Same shape as <see cref="TryNetworkBytes"/> beside it.
		/// </para>
		/// </summary>
		internal static bool TryResearchBytes(int cities, out long bytes)
		{
			bytes = 0L;
			if (cities < 0)
			{
				return false;
			}
			bytes = (long)cities * (ResearchHeaderBytes + ResearchRosterHeapBytes
				+ (long)ResearchShelfRows * ResearchShelfRowBytes);
			return true;
		}

		/// <summary>
		/// Everything &sect;0.0's "Model in RAM" row is answerable for: model + registry +
		/// itineraries + distance matrix + network graphs + the keepers, per realm.
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
			long research;
			if (cities < 0
				|| !TryCityModelBytes(zonesPerCity, worksPerCity, residentsPerCity, clocksPerCity, out city)
				|| !TryRegistryBytes(residentsPerCity, cities, openJobs, out registry)
				|| !TryJobBytes(openJobs, out jobs)
				|| !TryDistanceMatrixBytes(cities, out distance)
				|| !TryNetworkBytes(cities, out networks)
				|| !TryResearchBytes(cities, out research))
			{
				return false;
			}
			bytes = (long)cities * city + registry + jobs + distance + networks + research;
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

		/// <summary>
		/// What a NAMED set of fields on a type declares, summed the same way
		/// <see cref="TryMeasureDeclaredRowBytes"/> sums a whole row.
		/// <para>
		/// For a lane whose state hangs off a type this table does not own the whole of &mdash; the
		/// keepers' seven fields on the settlement container, which also carries everything else a
		/// settlement is. Measuring the whole type would price the wrong thing; restating the
		/// widths as constants would price nothing at all, because a restated constant goes on
		/// agreeing with itself after the field it stood for is gone.
		/// </para>
		/// <para>
		/// A name this type does not declare is a REFUSAL, not a zero: the whole value of the row
		/// is that it stops agreeing when the lane moves.
		/// </para>
		/// </summary>
		internal static bool TryMeasureDeclaredFieldBytes(Type type, string[] fieldNames, out int bytes)
		{
			bytes = 0;
			if (type == null || fieldNames == null)
			{
				return false;
			}
			int total = 0;
			for (int i = 0; i < fieldNames.Length; i++)
			{
				FieldInfo field = (fieldNames[i] == null)
					? null
					: type.GetField(fieldNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				int fieldBytes;
				if (field == null || !TryMeasure(field.FieldType, 0, out fieldBytes))
				{
					return false;
				}
				total += fieldBytes;
			}
			bytes = total;
			return true;
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
