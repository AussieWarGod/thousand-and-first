#if TAF_TESTS
using System;
using System.IO;

namespace ThousandAndFirst
{
	internal static partial class KingdomPolityDeathIntentRules
	{
		internal static string EncodeV1Fixture(KingdomPolityDeathIntentRecord Record)
		{
			byte[] payload;
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8))
			{
				writer.Write((byte)1); WriteText(writer, Record.Kind); WriteText(writer, Record.RealmId);
				WriteText(writer, Record.CohortId); WriteText(writer, Record.ProjectionId);
				WriteText(writer, Record.ZoneId); WriteText(writer, Record.ObjectId);
				writer.Write(Record.Ordinal); writer.Write((byte)Record.Purpose);
				writer.Write(Record.Representative ? (byte)1 : (byte)0); writer.Write(Record.Tick);
				writer.Write((byte)Record.Attribution); writer.Write((byte)Record.Visibility);
				writer.Flush(); payload = stream.ToArray();
			}
			string body = Convert.ToBase64String(payload);
			string prefix = Prefix(KingdomPolityDeathIntentProvenance.LegacyV1, out string domain);
			return prefix + body + ":" + KingdomPolityRules.ActivationDigest(domain, body);
		}
	}
}
#endif
