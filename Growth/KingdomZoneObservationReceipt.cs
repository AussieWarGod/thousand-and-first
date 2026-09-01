namespace ThousandAndFirst
{
	/// <summary>
	/// One bounded observation of one exact zone. The envelope is purpose-separated so unrelated
	/// systems can share the codec without sharing authority.
	/// </summary>
	public sealed class KingdomZoneObservationReceipt
	{
		public int Version;
		public string Purpose;
		public string RealmId;
		public string SettlementId;
		public string ZoneId;
		public string OwnerId;
		public string SourceRevision;
		public string SourceDigest;
		public long ObservedTick;
		public string Payload;
	}
}
