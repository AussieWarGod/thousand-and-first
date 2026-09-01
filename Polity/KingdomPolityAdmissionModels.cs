using System;

namespace ThousandAndFirst
{
	public enum KingdomPolityAdmissionReceiptPhase : byte
	{
		Prepared = 1,
		Committed = 2,
		Rejected = 3,
		RolledBack = 4,
		Faulted = 5
	}

	/// <summary>Durable, handoff-owned result of resident ingress. Its stable id names the
	/// operation; its digest commits every mutable phase field.</summary>
	[Serializable]
	public sealed class KingdomPolityAdmissionReceipt
	{
		public const int CurrentVersion = 1;
		public int Version = CurrentVersion;
		public string ReceiptId;
		public string OperationId;
		public string HandoffId;
		public string RealmId;
		public string SourcePolityId;
		public string CohortId;
		public string MemberId;
		public string TargetSettlementId;
		public string SourceObjectId;
		public string SourceZoneId;
		public KingdomPolityAdmissionReceiptPhase Phase;
		public long PreparedTick;
		public long DecidedTick;
		public int ResidentId;
		public string BodyReceiptId;
		public string Fault;
		public string Digest;

		public KingdomPolityAdmissionReceipt Copy()
		{
			return new KingdomPolityAdmissionReceipt
			{
				Version = Version, ReceiptId = ReceiptId, OperationId = OperationId,
				HandoffId = HandoffId, RealmId = RealmId, SourcePolityId = SourcePolityId,
				CohortId = CohortId, MemberId = MemberId,
				TargetSettlementId = TargetSettlementId, SourceObjectId = SourceObjectId,
				SourceZoneId = SourceZoneId, Phase = Phase, PreparedTick = PreparedTick,
				DecidedTick = DecidedTick, ResidentId = ResidentId,
				BodyReceiptId = BodyReceiptId, Fault = Fault, Digest = Digest
			};
		}
	}
}
