using System;

namespace ThousandAndFirst
{
	[Serializable]
	public sealed class KingdomBodyHistoryEnvelope
	{
		public string RealmId;
		public bool IdentityBound;
		public KingdomBodyHistoryBook Book = new KingdomBodyHistoryBook();
		public int OpaqueFutureVersion;
		public byte[] OpaqueFuturePayload;
		public bool Quarantined;
		public string Fault = "";

		public bool IsOpaqueFuture
		{
			get
			{
				return !Quarantined
					&& OpaqueFutureVersion > KingdomBodyHistoryCodec.CurrentWireVersion;
			}
		}

		public KingdomBodyHistoryEnvelope Copy() => KingdomBodyHistoryStore.Copy(this);
		public bool TryValidateIdentity(out string Failure) =>
			KingdomBodyHistoryStore.TryValidateIdentity(this, out Failure);
		public bool TryBindEmptyIdentity(string ExactRealmId, out string Failure) =>
			KingdomBodyHistoryStore.TryBindEmptyIdentity(this, ExactRealmId, out Failure);
	}
}
