#if TAF_TESTS
using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomCivicPracticeCodec
	{
		/// <summary>Frozen outer-v2/service-v1 writer used only for migration goldens.</summary>
		internal static byte[] EncodeLegacyV2ForTests(KingdomCivicPracticeEnvelope value) =>
			EncodeOlderForTests(value, IdentityWireVersion,
				EncodeServicesLegacyV1(value?.VocationServices), "legacy-v2");

		/// <summary>Frozen outer-v3/service-v2 writer used only for migration tests.</summary>
		internal static byte[] EncodePriorV3ForTests(KingdomCivicPracticeEnvelope value) =>
			EncodeOlderForTests(value, PriorWireVersion,
				EncodeServicesPriorV2(value?.VocationServices), "prior-v3");

		private static byte[] EncodeOlderForTests(KingdomCivicPracticeEnvelope value,
			int version, byte[] services, string label)
		{
			string failure = null;
			if (value == null || value.Quarantined || value.IsOpaqueFuture ||
				!KingdomCivicPracticeStore.TryValidateIdentity(value, out failure) ||
				!value.IdentityBound)
				throw new InvalidDataException(failure ?? label + " fixture must be current and bound");
			byte[] sites = EncodeSites(value.SitePractices);
			byte[] payload;
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = Writer(stream))
			{
				WriteRealm(writer, value.RealmId); writer.Write(value.IdentityBound);
				writer.Write(sites.Length); writer.Write(sites);
				writer.Write(services.Length); writer.Write(services);
				writer.Flush(); payload = stream.ToArray();
			}
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = Writer(stream))
			{
				writer.Write(Magic); writer.Write(version); writer.Write(payload.Length);
				writer.Write(payload); writer.Write(Hash(version, payload));
				writer.Flush(); return stream.ToArray();
			}
		}
	}
}
#endif
