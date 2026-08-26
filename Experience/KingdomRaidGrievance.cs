using System;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomRaidGrievance
	{
		public string Id;
		public string IssuerFactionId;
		public string TargetSettlementId;
		public string TargetZoneId;
		public string CauseCode;
		public string SourceEventId;
		public long SourceTick;
		public string SourceZoneId;
		public int Severity;
		public string EvidenceText;
		public KingdomRaidGrievanceStatus Status;
		public string ResolutionId;
	}
}
