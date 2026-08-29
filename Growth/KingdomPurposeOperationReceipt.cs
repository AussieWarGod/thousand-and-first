namespace ThousandAndFirst
{
	/// <summary>One bounded operation. Requested, spent, lost, and outstanding always reconcile.</summary>
	public sealed class KingdomPurposeOperationReceipt
	{
		public const int Schema = 2;
		public string PairId;
		public long PairEpoch;
		public string OperationId;
		public int Ordinal;
		public KingdomPurposeKind SourceKind;
		public KingdomPurposeKind DestinationKind;
		public string SourceSettlementId;
		public string DestinationSettlementId;
		public string SourceWorkId;
		public string DestinationWorkId;
		public KingdomPurposeOperationPhase Phase;
		public bool BootstrapExemption;
		public bool ReturnExemption;
		public string InputCargoId;
		public string InputCargoReceipt;
		public string OutputCargoId;
		public string OutputCargoReceipt;
		public string SourceZoneId;
		public string DestinationZoneId;
		public string SourceInputStoreId;
		public string SourceOutputStoreId;
		public string DestinationInputStoreId;
		public string SourceGateKey;
		public string DestinationGateKey;
		public string RouteDigest;
		public string TransportJobId;
		public int WaterRequested;
		public int WaterSpent;
		public int WaterLost;
		public int FoodRequested;
		public int FoodSpent;
		public int FoodLost;
		public string MaterialRequested;
		public string MaterialSpent;
		public string MaterialLost;
		public string LocalDebitReceipt;
		public string ProcedureKey;
		public string ProcedureReceipt;
		public string EffectBeforeDigest;
		public string EffectAfterDigest;
		/// <summary>Generic persisted value interpreted only through SourceKind's typed ladder.</summary>
		public int EffectStep;
		public string InputBeforeDigest;
		public string InputAfterDigest;
		public string OutputBeforeDigest;
		public string OutputAfterDigest;
		public int Revision;

		public KingdomPurposeOperationReceipt Copy()
		{
			return (KingdomPurposeOperationReceipt)MemberwiseClone();
		}
	}
}
