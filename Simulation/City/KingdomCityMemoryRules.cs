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
		/// + defence 4 + water carry 4 + food carry 4 + three signed owed figures 12 = 96.
		/// LIVING-CITY-ARCHITECTURE §0.0(c), as W1 widened it: the two carries because the
		/// <c>ZoneSighting</c> projection reads carries rather than levels, and the owed figures
		/// per stock kind because one net counter cannot say that a zone owes a food landing and a
		/// water draw at once. Sixteen bytes over the eighty §0.0(c) first budgeted, and the table
		/// carries the same edit.</summary>
		internal const int ZoneRowBytes = 96;

		/// <summary>id 4 + zone ref 8 + anchor 4 + design ref 8 + condition 4 + crew 4 +
		/// RanThroughTick 8 + run-state 16 + pad 8. LIVING-CITY-ARCHITECTURE §0.0(c).</summary>
		internal const int WorkRowBytes = 64;

		/// <summary>The fields only. LIVING-CITY-ARCHITECTURE §0.0(c), as the creed-gate wave
		/// widened it: the row gained one shared string reference for the creeds this person has
		/// held and left (Addendum 16's recorded fact), and ninety-six had only five bytes of
		/// headroom left in it. Eight bytes over the ninety-six §0.0(c) first budgeted, and the
		/// table carries the same edit — the formula is the contract, not the constant, and this
		/// is the second time a field bought a widening rather than a widening excusing a field.
		/// </summary>
		internal const int ResidentRowStructBytes = 104;

		/// <summary>One unique heap string per resident. The only heap string in the MODEL: zone ids
		/// and design keys are shared references. LIVING-CITY-ARCHITECTURE §0.0(c). (The knowledge
		/// siting put a second composed string on the settlement container beside the model — see
		/// <see cref="ResearchHeaderBytes"/>, which names it and does not yet price it.)</summary>
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

		/// <summary>WorkId 4 + role 1 + tier 1 + capacity 4 + rate 4 = 14 declared, in the sixteen
		/// LIVING-CITY-ARCHITECTURE §0.0(c) budgets. The two spare bytes are padding and are left
		/// as headroom rather than spent: a node deliberately carries no LEVEL, because a store's
		/// contents are the ground's and the model keeps one aggregate per network instead of a
		/// second copy per container (§3.9).</summary>
		internal const int NetworkNodeBytes = 16;

		/// <summary>NodeA 4 + NodeB 4 + capacity 4 + condition 4 = 16 declared, exactly the
		/// §0.0(c) budget. LIVING-CITY-ARCHITECTURE §3.11.</summary>
		internal const int NetworkEdgeBytes = 16;

		/// <summary>
		/// Per network: id 4 + kind 1 + liquid ref 8 + topology stamp 8 + four array refs 32 +
		/// the stock pair the (network, liquid) key holds 16 = 69, rounded to a 64-byte line plus
		/// its object header.
		/// <para>
		/// <b>§0.0(c) correction, W7 — the fifth, in the same spirit as W0's and W1's four.</b>
		/// The header was budgeted at 32 bytes before anything had been built to sit in it, and a
		/// row that holds four array references cannot be 32 bytes. The formula is the contract,
		/// so the table takes the edit rather than the row taking a squeeze.
		/// </para>
		/// </summary>
		internal const int NetworkHeaderBytes = 64;

		/// <summary>
		/// Per node of a network: one byte of traversal order, one byte of parent edge.
		/// <para>
		/// <b>§0.0(c) correction, W7 — and this one buys the budget it costs.</b> §3.11 prices the
		/// solve at <c>O(nodes + edges) &le; 80</c>, which a walk that has to find each node's
		/// neighbours by scanning the edge array cannot honour: that is <c>nodes &times; edges</c>,
		/// 1,536, nineteen times the ceiling. So the traversal ORDER is computed once when the
		/// topology is laid — off the ground, never at reckon — and stored, and the solve is then
		/// one linear pass. Two bytes a node (a node index is at most 31 and an edge index at most
		/// 47, with 255 free as the no-parent sentinel) is what the ceiling costs, and the
		/// alternative, a full adjacency index, costs 162.
		/// </para>
		/// </summary>
		internal const int NetworkTraversalBytesPerNode = 2;

		// ---- The keepers' own state, per city. -------------------------------------------------

		/// <summary>
		/// The seven fields the settlement container serializes for the keepers, by name.
		/// <para>
		/// The names are the contract, not the widths. <see cref="TryMeasureDeclaredFieldBytes"/>
		/// sums what these fields actually declare on the settlement type, so
		/// <see cref="ResearchHeaderBytes"/> is falsifiable the same way every row width above is:
		/// add a <c>long</c> to the lane and the header exceeds the budget; rename or retire a
		/// field and the measurement is REFUSED rather than quietly answering for six fields.
		/// A lane the receipt cannot see is a lane with no owner, and a lane the receipt sees
		/// through a restated constant is the same lane wearing a number.
		/// </para>
		/// <para>
		/// Held as names rather than as a type reference because the settlement type is not
		/// engine-free &mdash; the same reason <see cref="CitiesPerRealm"/> is a copy. The caller
		/// that owns the type hands it in.
		/// </para>
		/// </summary>
		internal static readonly string[] ResearchFields = new string[7]
		{
			"KeepersRoster",
			"ResearchSubject",
			"ResearchAccrued",
			"ResearchTakenUpTick",
			"ResearchStalledAnnounced",
			"ResearchShelf",
			"ResearchBestMind"
		};

		/// <summary>
		/// The keepers' header, per city: roster ref 8 + subject ref 8 + accrued 4 + taken-up tick
		/// 8 + stalled flag 1 + shelf ref 8 + best mind 4 = 41 declared, against the 48 the table
		/// budgets it &mdash; the padding headroom every row above carries, and room for one more
		/// reference before the table has to take an edit. LIVING-CITY-ARCHITECTURE &sect;0.0(c),
		/// as the research wave widened it.
		/// <para>
		/// <b>Reference slots, not the strings behind them</b> &mdash; which is what a "field" has
		/// meant in this table since &sect;0.0(c)'s first row ("id ref 8 … design ref 8"). The one
		/// exception the table has ever made is <see cref="ResidentNameBytes"/>, and the composed
		/// keepers' roster is now a second per-city heap string of the same kind.
		/// </para>
		/// <para>
		/// <b>OPEN, and named rather than quietly omitted:</b> the roster STRING is not priced
		/// here. The authored tree's own grants compose to about six hundred bytes, and disks,
		/// machines and patterns add to it without a cap; any width chosen for it moves the realm
		/// total across or under &sect;0.0's 56 KiB advisory rung, which is a ruling about the
		/// budget rather than an edit to a row. Until it is ruled the receipt answers for the
		/// slot and says so, because a lane the receipt cannot see is a lane with no owner and a
		/// lane it half-sees should at least say which half.
		/// </para>
		/// </summary>
		internal const int ResearchHeaderBytes = 48;

		/// <summary>One shelved subject: the node key as a shared reference 8 + the labour still
		/// standing on it 4. The key is shared with the loaded tree, exactly as zone ids and design
		/// keys are shared, so the shelf costs no characters of its own.
		/// LIVING-CITY-ARCHITECTURE &sect;0.0(c).</summary>
		internal const int ResearchShelfRowBytes = 12;

		/// <summary>The shelf's cap, from the rule that enforces it. Referenced rather than copied
		/// because <c>KingdomResearchRules</c> is itself engine-free, so this one cannot drift the
		/// way <see cref="CitiesPerRealm"/> could.</summary>
		internal const int ResearchShelfRows = KingdomResearchRules.ShelfRows;

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
				+ (long)KingdomBudgetRules.NetworkMaxNodes * NetworkTraversalBytesPerNode
				+ NetworkHeaderBytes;
			bytes = (long)cities * KingdomBudgetRules.NetworksPerCity * perNetwork;
			return true;
		}

		/// <summary>
		/// What the keepers cost, per city: the header's seven fields plus the shelf at its cap.
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
			bytes = (long)cities * (ResearchHeaderBytes + (long)ResearchShelfRows * ResearchShelfRowBytes);
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
