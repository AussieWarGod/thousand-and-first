using System;

namespace ThousandAndFirst
{
	[Serializable]
	public sealed class KingdomCivicPracticeEnvelope
	{
		public string RealmId;
		public bool IdentityBound;
		public KingdomSitePracticeBook SitePractices = new KingdomSitePracticeBook();
		public KingdomVocationServiceBook VocationServices = new KingdomVocationServiceBook();
		public int OpaqueFutureVersion;
		public byte[] OpaqueFuturePayload;
		public bool Quarantined;
		public string Fault;

		public bool IsOpaqueFuture => !Quarantined &&
			OpaqueFutureVersion > KingdomCivicPracticeCodec.CurrentWireVersion;

		public KingdomCivicPracticeEnvelope Copy() => KingdomCivicPracticeStore.Copy(this);
		public bool TryValidateIdentity(out string failure) =>
			KingdomCivicPracticeStore.TryValidateIdentity(this, out failure);
		public bool TryBindEmptyIdentity(string exactRealmId, out string failure) =>
			KingdomCivicPracticeStore.TryBindEmptyIdentity(this, exactRealmId, out failure);
	}
}
