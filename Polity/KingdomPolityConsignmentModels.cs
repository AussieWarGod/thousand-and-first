using System;

namespace ThousandAndFirst
{
	public enum KingdomPolityCorrespondenceReplyKind : byte
	{
		None = 0,
		Fulfilled = 1,
		Declined = 2,
		Unfulfilled = 3,
		RecipientUnavailable = 4
	}

	internal sealed class KingdomPolityConsignmentAbsenceProof
	{
		internal string CorrespondencePlanId = null;
		internal string TermsPlanId = null;
		internal string RecipientCohortId = null;
		internal string ConsignmentId = null;
		internal string RequestDigest = null;
		internal string ProofDigest = null;
	}

	/// <summary>Exhaustive read result for one frozen Trade consignment identity.</summary>
	public enum KingdomTradePolityConsignmentReceiptKind : byte
	{
		Missing = 0,
		Landed = 1,
		TerminalFailed = 2,
		Invalid = 3
	}

	/// <summary>Immutable polity request. It owns no physical quantity or inventory row.</summary>
	[Serializable]
	public sealed class KingdomPolityConsignmentRequest
	{
		public string CorrespondencePlanId;
		public string CorrespondenceId;
		public string TermsPlanId;
		public string RecipientCohortId;
		public string CounterpartyPolityId;
		public string CurrentPolityId;
		public string SurfaceRef;
		public string NeedRef;
		public string ConsignmentId;
		public int RequestedDrams;
		public string RequestDigest;
	}

	/// <summary>Typed terminal proof produced only from Trade's physical operation receipt.</summary>
	[Serializable]
	public sealed class KingdomTradePolityConsignmentReceipt
	{
		public KingdomTradePolityConsignmentReceiptKind Kind;
		public string ReceiptId;
		public string TradeOperationId;
		public string TradeEvidenceHash;
		public string ConsignmentId;
		public string CorrespondencePlanId;
		public string CounterpartyPolityId;
		public string SurfaceRef;
		public string RecipientBodyId;
		public string RecipientCohortId;
		public string RecipientProjectionId;
		public string RecipientWitnessDigest;
		public int RequestedDrams;
		/// <summary>Physical source debit proved by Trade, including retained failed value.</summary>
		public int DebitedDrams;
		/// <summary>Quantity that reached the exact recipient; zero for terminal failure.</summary>
		public int DeliveredDrams;
		public int RetainedDrams;
		public string FailureText;
		public long CommitTick;
		public string ReceiptDigest;
	}
}
