using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One city's whole book: stocks, zone rows, work rows, resident rows, clocks, and the
	/// told-log ring. LIVING-CITY-ARCHITECTURE &sect;1.2.
	/// <para>
	/// Frozen by the &sect;1.3 doctrine, in the shape this codebase already uses for
	/// <c>FixedPeriodToyState</c>: sealed, <c>readonly struct</c> rows, every array copied in and
	/// never handed back, every transition copy-on-write. Nothing here is ever partially
	/// incremented, so a fault leaves the caller's state byte-identical.
	/// </para>
	/// <para>
	/// This is the pure model, engine-free by construction. The serialized carrier that will hold
	/// it on <c>KingdomSettlement</c> is a W1 deliverable and lives outside this type: an
	/// <c>IComposite</c> must assign fields, and the rules layer must not.
	/// </para>
	/// </summary>
	internal sealed partial class KingdomCityState
	{
		/// <summary>LIVING-CITY-ARCHITECTURE &sect;1.4: at most four claimed zones today, from
		/// <c>KingdomZoningRules.ZonesForStage</c> at City. A stage-gate constant, never an
		/// architectural limit — raising it raises R linearly and changes nothing else.</summary>
		internal const int MaxZones = 4;

		/// <summary>Every plot the largest currently claimable city can hold: the per-zone City
		/// bound across all four claimed zones. A smaller proxy silently drops later works from
		/// <c>KingdomCity.ReadWorks</c> and makes their behaviour disappear from the model.</summary>
		internal static readonly int MaxWorks = MaxZones
			* KingdomRules.MaxBuildingsForStage(GrowthStage.City);

		/// <summary>LIVING-CITY-ARCHITECTURE &sect;1.4, from <c>KingdomRules.MaxPopulation</c>.</summary>
		internal const int MaxResidents = 60;

		/// <summary>LIVING-CITY-ARCHITECTURE &sect;1.4: a fixed, named set.</summary>
		internal const int MaxClocks = 12;

		/// <summary>LIVING-CITY-ARCHITECTURE &sect;1.2(f) / &sect;1.4: K is 32, and it is a ring.</summary>
		internal const int MaxToldEntries = 32;

		internal readonly int SchemaVersion;

		internal readonly int RulesVersion;

		internal readonly string SettlementId;

		/// <summary>
		/// How far the model has been advanced. Advanced by whole units consumed with the
		/// remainder kept, never re-anchored to now (LIVING-CITY-ARCHITECTURE &sect;2.2), which is
		/// what makes <c>TryAdvance</c> idempotent at a repeated tick and a mid-pass reload safe.
		/// </summary>
		internal readonly long ProcessedThroughTick;

		internal readonly KingdomStocks Stocks;

		private readonly KingdomZoneRow[] zones;

		private readonly KingdomWorkRow[] works;

		private readonly KingdomResidentRow[] residents;

		private readonly KingdomClockRow[] clocks;

		private readonly KingdomToldRow[] told;

		private readonly int toldCount;

		private readonly int toldNext;

		private KingdomCityState(
			int schemaVersion,
			int rulesVersion,
			string settlementId,
			long processedThroughTick,
			KingdomStocks stocks,
			KingdomZoneRow[] zones,
			KingdomWorkRow[] works,
			KingdomResidentRow[] residents,
			KingdomClockRow[] clocks,
			KingdomToldRow[] told,
			int toldCount,
			int toldNext)
		{
			SchemaVersion = schemaVersion;
			RulesVersion = rulesVersion;
			SettlementId = settlementId;
			ProcessedThroughTick = processedThroughTick;
			Stocks = stocks;
			this.zones = zones;
			this.works = works;
			this.residents = residents;
			this.clocks = clocks;
			this.told = told;
			this.toldCount = toldCount;
			this.toldNext = toldNext;
		}

		/// <summary>
		/// Builds a city book, or refuses and publishes nothing.
		/// <para>
		/// Every array is copied, so a caller that keeps its own reference and mutates it later
		/// cannot reach inside a published model. A null array reads as an empty one — a city with
		/// no works yet is an ordinary state, not a fault — but a null settlement id is not.
		/// </para>
		/// </summary>
		internal static bool TryCreate(
			int schemaVersion,
			int rulesVersion,
			string settlementId,
			long processedThroughTick,
			KingdomStocks stocks,
			KingdomZoneRow[] zones,
			KingdomWorkRow[] works,
			KingdomResidentRow[] residents,
			KingdomClockRow[] clocks,
			out KingdomCityState state,
			out KingdomCityFault fault)
		{
			state = null;
			if (settlementId == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (processedThroughTick < 0L)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			if (Length(zones) > MaxZones || Length(works) > MaxWorks
				|| Length(residents) > MaxResidents || Length(clocks) > MaxClocks)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			state = new KingdomCityState(
				schemaVersion,
				rulesVersion,
				settlementId,
				processedThroughTick,
				stocks,
				Copy(zones),
				Copy(works),
				Copy(residents),
				Copy(clocks),
				new KingdomToldRow[MaxToldEntries],
				0,
				0);
			fault = KingdomCityFault.None;
			return true;
		}

		internal int ZoneCount
		{
			get { return zones.Length; }
		}

		internal int WorkCount
		{
			get { return works.Length; }
		}

		internal int ResidentCount
		{
			get { return residents.Length; }
		}

		internal int ClockCount
		{
			get { return clocks.Length; }
		}

		internal int ToldCount
		{
			get { return toldCount; }
		}

		/// <summary>
		/// The live <c>R</c> of LIVING-CITY-ARCHITECTURE &sect;0.0(f): zone rows + work rows +
		/// resident rows + clocks. The told-log is not in it, because a told line is never
		/// proposed against or integrated — it is what an integration left behind.
		/// </summary>
		internal int RowCount
		{
			get { return zones.Length + works.Length + residents.Length + clocks.Length; }
		}

	}
}
