using System;

namespace ThousandAndFirst
{
	/// <summary>Exact body callback or resident-row bridge for terminal Lodge recovery.</summary>
	[Serializable]
	public sealed class KingdomLifecycleLodgeTerminalReceipt
	{
		public const int CurrentVersion = 1;
		public const int MarketNone = 0;
		public const int MarketPrepared = 1;
		public const int MarketCommitted = 2;
		public const int MarketSourceDead = 3;

		public int Version = CurrentVersion;
		public string OperationId;
		public string ReceiptId;
		public string PlanHash;
		public string SettlementId;
		public string ObjectId;
		public string Blueprint;
		public int ResidentId;
		public string ResidentName;
		public string ResidentOrigin;
		public string ResidentArrival;
		public long ResidentArrivalTick;
		public string ResidentBoundZoneId;
		public int MarketSourcePrepared;
		public string MarketSourceBodyObjectId;
		public int MarketSourceResidentId;
		public int MarketTier;
		public string MarketIntent;
		public string MarketSourceProofId;
		public KingdomLifecycleLodgeTerminalState State;
		public byte DeathCause;
		public long TerminalTick;
		public string SourceProofId;
		public string DeathProofId;
	}
}
