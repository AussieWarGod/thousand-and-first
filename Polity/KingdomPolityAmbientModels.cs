using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public enum KingdomPolityAmbientTerminalChoice : byte
	{
		None = 0,
		Acknowledged = 1,
		AcknowledgedNoTrade = 2,
		PetitionAccepted = 3,
		PetitionRejected = 4
	}

	public enum KingdomPolityAdmissionDecision : byte
	{
		Pending = 0,
		Accepted = 1,
		Rejected = 2
	}

	[Serializable]
	public sealed class KingdomPolityAdmissionHandoff
	{
		public const int CurrentVersion = 1;
		public int Version = CurrentVersion;
		public string HandoffId;
		public string RealmId;
		public string PolityId;
		public string CohortId;
		public string MemberId;
		public string TargetSettlementId;
		public string SourceObjectId;
		public string SourceZoneId;
		public string ProposedResidentName;
		public KingdomPolityAdmissionDecision Decision;
		public long PreparedTick;
		public long DecidedTick;
		public string CauseDigest;
		/// <summary>Consumer-owned resident admission result. Null means the accepted handoff has
		/// not yet entered resident authority; the ambient transaction never infers consumption.</summary>
		public KingdomPolityAdmissionReceipt AdmissionReceipt;
		public string Fault;
	}

	[Serializable]
	public sealed class KingdomPolityAmbientTransaction
	{
		public const int CurrentVersion = 1;
		public int Version = CurrentVersion;
		public string TransactionId;
		public KingdomPolityCohortPurpose Purpose;
		public string SourcePolityId;
		public string SourceSettlementId;
		public string SourceSettlementName;
		public string SourceZoneId;
		public string DestinationSettlementId;
		public string DestinationSettlementName;
		public string DestinationZoneId;
		public string LocalLocusRef;
		public List<string> FactRefs = new List<string>();
		public string SafeDetail;
		public List<string> ManifestRefs = new List<string>();
		public List<string> PhysicalStockObjectIds = new List<string>();
		public string NewsRef;
		public long PreparedTick;
		public string FrozenDigest;
		public KingdomPolityAmbientTerminalChoice TerminalChoice;
		public long TerminalTick;
		public string TerminalReceiptId;
		public KingdomPolityAdmissionHandoff AdmissionHandoff;
		public string Fault;
	}
}
