using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	[Serializable]
	public sealed class KingdomGrowthArrivalDebtRange
	{
		public int RulesVersionAtCreation;
		public long RateEpoch;
		public int Cohort;
		public ulong FirstOrdinal;
		public ulong Count;
		public long FirstDueTick;
		public long IntervalTicks;
	}

	/// <summary>Semantic head of the arrival debt. Catalog facts are final before any zone,
	/// placement, lodging, water, or actor callback is consulted.</summary>
	[Serializable]
	public sealed class KingdomGrowthArrivalOpportunity
	{
		public int RulesVersionAtCreation;
		public long RateEpoch;
		public int Cohort;
		public ulong Ordinal;
		public long DueTick;
		public long IntervalTicks;
		public string SettlementId;
		public string EventStreamId;
		public uint EventKindCode;
		public string EventId;
		public bool FirstGuest;
		public string Blueprint;
		public string Origin;
		public string Creed;
		public string PersonName;
		public string Arrived;
		public string PayloadHash;
	}

	public static partial class KingdomLifecycleRules
	{
		public const int MaxGrowthArrivalDebtRanges = 64;
		public const string GrowthArrivalEventStreamId =
			"taf:semantic:growth-arrival:v1";
		public const uint GrowthArrivalEventKindCode = 1U;
	}
}
