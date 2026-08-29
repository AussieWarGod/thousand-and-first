namespace ThousandAndFirst
{
	/// <summary>Single parent authority for a two-city reciprocal purpose pair.</summary>
	public sealed class KingdomPurposePairReceipt
	{
		public const int Schema = 1;
		public string PairId;
		public string RealmId;
		public long Epoch;
		public KingdomPurposeKind FirstKind;
		public KingdomPurposeKind SecondKind;
		public string FirstSettlementId;
		public string SecondSettlementId;
		public string FirstWorkId;
		public string SecondWorkId;
		public string FirstZoneId;
		public string SecondZoneId;
		public string FirstInputStoreId;
		public string FirstOutputStoreId;
		public string SecondInputStoreId;
		public string SecondOutputStoreId;
		public string FirstGateKey;
		public string SecondGateKey;
		public string RouteDigest;
		public bool BootstrapUsed;
		public bool ReturnUsed;
		public KingdomPurposeKind NextKind;
		public string CreditCargoId;
		public string CreditCargoReceipt;
		public KingdomPurposePairPhase Phase;
		public KingdomPurposePairPhase ResumePhase;
		public KingdomPurposeOperationReceipt Operation;
		public int NextOperationOrdinal;
		public int Revision;
		public string Fault;
		/// <summary>Read provenance only. Never encoded; first authorised mutation retires it.</summary>
		internal bool LegacyWire;

		public KingdomPurposePairReceipt Copy()
		{
			KingdomPurposePairReceipt copy = (KingdomPurposePairReceipt)MemberwiseClone();
			copy.Operation = Operation?.Copy();
			return copy;
		}
	}
}
