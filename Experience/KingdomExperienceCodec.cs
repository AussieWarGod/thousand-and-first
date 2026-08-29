using System;
using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomExperienceCodec
	{
		private const int Magic = 0x54414531; // TAE1
		public const int CurrentWireVersion = 4;
		public const int MaxEnvelopeBytes = 24 * 1024;

		public static byte[] EncodeEnvelope(KingdomExperienceLedger Ledger)
		{
			if (Ledger == null) throw new ArgumentNullException(nameof(Ledger));
			int wire = CurrentWireVersion; byte[] payload;
			if (Ledger.SchemaState == KingdomExperienceSchemaState.Unknown)
			{
				wire = Ledger.OpaqueWireVersion;
				if (wire <= CurrentWireVersion || Ledger.OpaqueFuturePayload == null
					|| Ledger.OpaqueFuturePayload.Length > MaxEnvelopeBytes - 12)
					throw new InvalidDataException("Opaque experience wire evidence is invalid.");
				payload = (byte[])Ledger.OpaqueFuturePayload.Clone();
			}
			else if (Ledger.SchemaState == KingdomExperienceSchemaState.Quarantined
				&& Ledger.OpaqueWireVersion > 0
				&& Ledger.OpaqueWireVersion <= CurrentWireVersion
				&& Ledger.OpaqueFuturePayload != null)
			{
				wire = Ledger.OpaqueWireVersion;
				if (Ledger.OpaqueFuturePayload.Length > MaxEnvelopeBytes - 12)
					throw new InvalidDataException(
						"Quarantined experience wire evidence exceeds hard bound.");
				payload = (byte[])Ledger.OpaqueFuturePayload.Clone();
			}
			else
			{
				RequireEncodable(Ledger); payload = EncodePayload(Ledger);
			}
			return Frame(wire, payload);
		}

		public static KingdomExperienceLedger DecodeEnvelope(byte[] Envelope)
		{
			KingdomExperienceLedger ledger = DecodeEnvelopeRaw(Envelope);
			KingdomExperienceRules.Normalize(ledger); return ledger;
		}

		public static KingdomExperienceLedger DecodeEnvelopeRaw(byte[] Envelope)
		{
			if (Envelope == null || Envelope.Length < 12 || Envelope.Length > MaxEnvelopeBytes)
				throw new InvalidDataException("Experience envelope length is invalid.");
			using (MemoryStream stream = new MemoryStream(Envelope, false))
			using (BinaryReader reader = new BinaryReader(stream))
			{
				if (reader.ReadInt32() != Magic)
					throw new InvalidDataException("Experience marker is invalid.");
				int wire = reader.ReadInt32();
				int length = ReadCount(reader, MaxEnvelopeBytes - 12, "payload bytes");
				if (length != stream.Length - stream.Position)
					throw new InvalidDataException("Experience payload length mismatch.");
				byte[] payload = reader.ReadBytes(length);
				if (payload.Length != length)
					throw new EndOfStreamException("Truncated experience payload.");
				if (wire == CurrentWireVersion) return DecodePayload(payload);
				if (wire == 3) return DecodePayloadV3(payload);
				if (wire == 2) return DecodePayloadV2(payload);
				if (wire == 1) return DecodePayloadV1(payload);
				if (wire <= 0) throw new InvalidDataException("Experience wire version is invalid.");
				return new KingdomExperienceLedger
				{
					SchemaState = KingdomExperienceSchemaState.Unknown,
					SchemaFault = "Unsupported bounded experience wire preserved as opaque evidence.",
					OpaqueWireVersion = wire,
					OpaqueFuturePayload = payload
				};
			}
		}

#if TAF_TESTS
		public static byte[] EncodeFutureFixture(int WireVersion, byte[] Payload)
		{
			if (WireVersion <= CurrentWireVersion) throw new ArgumentOutOfRangeException(nameof(WireVersion));
			return Frame(WireVersion, Payload);
		}
#endif

		private static byte[] Frame(int Wire, byte[] Payload)
		{
			if (Payload == null || Payload.Length > MaxEnvelopeBytes - 12)
				throw new InvalidDataException("Experience payload exceeds hard bound.");
			using (MemoryStream stream = new MemoryStream(12 + Payload.Length))
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(Magic); writer.Write(Wire); writer.Write(Payload.Length);
				writer.Write(Payload, 0, Payload.Length); return stream.ToArray();
			}
		}

		private static void RequireEncodable(KingdomExperienceLedger L)
		{
			if (L.SchemaState == KingdomExperienceSchemaState.Compatible)
			{
				if (!KingdomExperienceRules.TryValidate(L, out string failure))
					throw new InvalidDataException("Invalid experience authority: " + failure);
				return;
			}
			if (L.SchemaState != KingdomExperienceSchemaState.Quarantined
				|| !KingdomExperienceRules.Text(L.SchemaFault, true) || !BoundedTop(L))
				throw new InvalidDataException("Experience quarantine shape is invalid.");
		}

		private static bool BoundedTop(KingdomExperienceLedger L)
		{
			return L.Story != null && L.Knowledge != null && L.Ambient != null
				&& L.Audiences != null
				&& L.Audiences.Count <= KingdomExperienceRules.MaxAudienceReceipts
				&& L.BodyReservations != null
				&& L.BodyReservations.Count <= KingdomExperienceRules.MaxBodyReservations
				&& L.Offices != null
				&& L.Offices.Count <= KingdomExperienceRules.MaxOfficeReceipts
				&& L.Remembrances != null
				&& L.Remembrances.Count <= KingdomExperienceRules.MaxRemembranceReceipts
				&& L.Voices != null
				&& L.Voices.Count <= KingdomExperienceRules.MaxVoiceReceipts
				&& L.FirstFeasts != null
				&& L.FirstFeasts.Count <= KingdomExperienceRules.MaxFirstFeastReceipts;
		}
	}
}
