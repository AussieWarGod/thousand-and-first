using System;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomGrowthOutboxEvent
	{
		public string EventId;
		public string Kind;
		/// <summary>True only on a Growth-v1 event migrated from the historical single-register
		/// Chronicle receipt. New v2 plans may not mint this compatibility shape.</summary>
		public bool LegacySingleRegisterChronicle;
		public int ChronicleBeforeCount;
		public int ChronicleDeclaredAfterCount;
		public int ChronicleObservedCount = -1;
		public string ChronicleBeforeHash;
		public string ChronicleDeclaredAfterHash;
		public string ChronicleObservedHash;
		/// <summary>Exact rendered entries frozen before a v2 dual-register Chronicle callback.
		/// Historical v1 single-register evidence leaves both null.</summary>
		public string ChronicleOfficial;
		public string ChronicleOutsider;
		public int OutsiderBeforeCount;
		public int OutsiderDeclaredAfterCount;
		public int OutsiderObservedCount = -1;
		public string OutsiderBeforeHash;
		public string OutsiderDeclaredAfterHash;
		public string OutsiderObservedHash;
		public int LedgerBeforeCount;
		public int LedgerDeclaredAfterCount;
		public int LedgerObservedCount = -1;
		public string LedgerBeforeHash;
		public string LedgerDeclaredAfterHash;
		public string LedgerObservedHash;
		public KingdomLifecycleOutbox Outbox;
	}
}
