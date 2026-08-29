using System;

namespace ThousandAndFirst
{
	[Serializable]
	public sealed class KingdomCivicArtifactsEnvelope
	{
		public string RealmId;
		public bool IdentityBound;
		public KingdomWitnessWorkBook WitnessWorks = new KingdomWitnessWorkBook();
		public KingdomArtifactRecognitionBook Recognitions =
			new KingdomArtifactRecognitionBook();
		public int OpaqueFutureVersion;
		public byte[] OpaqueFuturePayload;
		public bool Quarantined;
		public string Fault;

		public bool IsOpaqueFuture => !Quarantined && OpaqueFutureVersion >
			KingdomCivicArtifactsCodec.CurrentWireVersion;

		public KingdomCivicArtifactsEnvelope Copy() =>
			KingdomCivicArtifactsStore.Copy(this);
		public bool TryValidateIdentity(out string Failure) =>
			KingdomCivicArtifactsStore.TryValidateIdentity(this, out Failure);
		public bool TryBindEmptyIdentity(string ExactRealmId, out string Failure) =>
			KingdomCivicArtifactsStore.TryBindEmptyIdentity(this, ExactRealmId, out Failure);
	}
}
