using System;

namespace ThousandAndFirst
{
	/// <summary>Closed set of ledger-backed grievance sources. No standing source exists.</summary>
	public enum KingdomPolityGrievanceSourceKind : byte
	{
		None = 0,
		ClaimDeparture = 1,
		WitnessedTrespass = 2,
		BrokenPact = 3,
		ResourceRefusal = 4,
		RefusedTerms = 5,
		DesignatedTheftReceipt = 6,
		WitnessedEnvoyHarm = 7
	}

	internal enum KingdomPolityEnvoyDeathOutcome : byte
	{
		Refused = 0,
		PendingRecovery = 1,
		Committed = 2
	}

	/// <summary>Selector for one existing authored receipt; all grievance fields are derived.</summary>
	[Serializable]
	public sealed class KingdomPolityGrievanceIngressRequest
	{
		public KingdomPolityGrievanceSourceKind SourceKind;
		public string SourceRef;
		public string SourceReceiptId;
		public string IssuerPolityId;
		public string TargetPolityId;
	}
}
