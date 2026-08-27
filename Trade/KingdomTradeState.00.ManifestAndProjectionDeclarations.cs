using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{

	public enum KingdomTradeManifestStatus : byte
	{
		None = 0,
		InFlight = 1,
		Delivered = 2,
		Quarantined = 3
	}

	[Serializable]
	public sealed class KingdomTradeWaterLeg
	{
		public string OwnerId;
		public string ZoneId;
		public int Capacity;
		public int Before;
		public int Delta;
		public int After;
		public string BeforeComposition;
		public string AfterComposition;
		public KingdomTradePhysicalState State;

	}

	[Serializable]
	public sealed class KingdomTradeMaterialOutput
	{
		public string OutputId;
		public string Marker;
		public string Blueprint;
		public int Count;
		public string DestinationOwnerId;
		public string ZoneId;
		public KingdomTradePhysicalState State;
		public KingdomTradePhysicalState CleanupState;

	}

	[Serializable]
	public sealed class KingdomTradeStandingCas
	{
		public string Faction;
		public int Before;
		public int Delta;
		public int After;
		public KingdomTradePhysicalState State;

	}

	[Serializable]
	public sealed class KingdomTradeOutbox
	{
		public string EventId;
		public string Chronicle;
		public KingdomTradeSinkState ChronicleState;
		public string LedgerNote;
		public int LedgerDeliveredDelta;
		public KingdomTradeSinkState LedgerState;
		public string Message;
		public KingdomTradeSinkState MessageState;
		public string Deed;
		public KingdomTradeSinkState DeedState;

	}

	[Serializable]
	public sealed class KingdomTradeCharter
	{
		public long Sequence;
		public string Id;
		public string DealKey;
		public string Faction;
		public long CreatedTick;
		public long NextTick;
		public bool Quarantined;
		public string Fault;

	}

	[Serializable]
	public sealed class KingdomTradeManifestState
	{
		public long OperationSequence;
		public string OperationId;
		public string Id;
		public string OriginId;
		public string OriginName;
		public string DestinationId;
		public string DestinationName;
		public int OriginalDrams;
		public int EscrowDrams;
		public long LoadedTick;
		public long DeadlineTick;
		public bool TurnedBack;
		public KingdomTradeManifestStatus Status;
		public string Fault;

	}

	/// <summary>One city's exact active caravan projection authority.</summary>
	[Serializable]
	public sealed class KingdomTradeProjectionRow
	{
		public long OperationSequence;
		public string OperationId;
		public string SettlementId;
		public string ZoneId;
		public string ProjectionId;
		public string ObjectId;
		public bool Quarantined;
		public string Fault;

	}
}
