using System;

namespace ThousandAndFirst
{
	/// <summary>One nonvaluable civic cause carried by its exact laboratory work.</summary>
	[Serializable]
	public sealed class KingdomLabCivicReceipt
	{
		public int Version;
		public KingdomLabCivicKind Kind;
		public KingdomLabCivicPhase Phase;
		public KingdomLabCivicRequest Request;
		public KingdomLabCivicChoice Choice;
		public KingdomLabCivicClosure Closure;
		public string EventId;
		public string CauseDigest;
		public string RealmId;
		public string SettlementId;
		public string ZoneId;
		public string OwnerObjectId;
		public string SubjectObjectId;
		public int SubjectResidentId;
		public string SubjectName;
		public string SubjectCreed;
		public string CityCreed;
		public string NotableLodgeReceiptId;
		public long TasteOrdinal;
		public string TasteSource;
		public int TasteIndex;
		public string TasteTag;
		public string TargetObjectId;
		public string TargetHomeObjectId;
		public int TargetResidentId;
		public string TargetName;
		public string SourcePlotId;
		public string SourceHomeName;
		public string TargetPlotId;
		public string TargetHomeName;
		public string RefusedTag;
		public long CreatedTick;
		public long ClosedTick;
		public bool OpenRecorded;
		public bool CloseRecorded;
		public string Fault;

		public KingdomLabCivicReceipt Copy()
		{
			return (KingdomLabCivicReceipt)MemberwiseClone();
		}
	}
}
