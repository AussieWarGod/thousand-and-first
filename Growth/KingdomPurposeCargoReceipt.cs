namespace ThousandAndFirst
{
	/// <summary>Immutable identity and embodied content of one reciprocal cargo object.</summary>
	public sealed class KingdomPurposeCargoReceipt
	{
		public const int Schema = 1;
		public string PairId;
		public long PairEpoch;
		public string OperationId;
		public KingdomPurposeKind SourceKind;
		public KingdomPurposeKind DestinationKind;
		public bool BootstrapExemption;
		public bool ReturnExemption;
		public string SourceSettlementId;
		public string DestinationSettlementId;
		public string SourceWorkId;
		public string DestinationWorkId;
		public string CargoKey;
		public KingdomMaterial EmbodiedMaterial;
		public int EmbodiedUnits;
		public int CarriedFood;
		public string ObjectId;
		public string TransportJobId;
		public string RouteDigest;

		public KingdomPurposeCargoReceipt Copy()
		{
			return (KingdomPurposeCargoReceipt)MemberwiseClone();
		}
	}
}
