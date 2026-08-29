using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public enum KingdomWitnessWorkPhase : byte
	{
		None = 0, Captured = 1, CarrierPrepared = 2, Projected = 3,
		Removed = 4, Lost = 5, Declined = 6
	}

	[Serializable]
	public sealed class KingdomWitnessWorkSource
	{
		public string EventId;
		public string SettlementId;
		public string EventKind;
		public string EventText;
		public long ClosedTick;
		public int MakerResidentId;
		public string MakerName;
		public string SnapshotDigest;
	}

	[Serializable]
	public sealed class KingdomWitnessWorkReceipt
	{
		public const int CurrentVersion = 1;
		public int Version = CurrentVersion;
		public KingdomWitnessWorkPhase Phase;
		public string WorkId;
		public KingdomWitnessWorkSource Source;
		public string Description;
		public string CarrierReceiptId;
		public string CarrierObjectId;
		public string CarrierZoneId;
		public string CarrierConstructionReceiptId;
		public int CarrierX = -1;
		public int CarrierY = -1;
		public bool Fixed;
		public bool Portable;
		public int CommerceValue;
		public long ChangedTick;
		public string Fault;
	}

	[Serializable]
	public sealed class KingdomWitnessWorkBook
	{
		public long Revision;
		public List<KingdomWitnessWorkReceipt> Rows =
			new List<KingdomWitnessWorkReceipt>();
	}
}
