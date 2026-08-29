using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	public static partial class KingdomTradeCodec
	{
		public const int Magic = 0x54414654;
		public const int PriorWireVersion = 3;
		public const int ImmediatePriorWireVersion = 4;
		public const int CurrentWireVersion = 5;
		public const int MaxEnvelopeBytes = 1024 * 1024;
		public const int MaxStringBytes = 65536;
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static byte[] EncodeEnvelope(KingdomTradeBook Book)
		{
			if (Book == null) throw new ArgumentNullException(nameof(Book));
			int wire = CurrentWireVersion;
			byte[] payload;
			if (Book.SchemaState == KingdomTradeSchemaState.Unknown
				&& Book.OpaqueFuturePayload != null)
			{
				wire = Book.OpaqueWireVersion;
				if (wire <= 0 || wire == CurrentWireVersion)
					throw new InvalidDataException("Opaque Trade wire version is not distinct and positive.");
				payload = (byte[])Book.OpaqueFuturePayload.Clone();
			}
			else payload = EncodePayload(Book);
			if (payload.Length > MaxEnvelopeBytes - 12)
				throw new InvalidDataException("Trade payload exceeds hard bound.");
			using (MemoryStream stream = new MemoryStream(12 + payload.Length))
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(Magic);
				writer.Write(wire);
				writer.Write(payload.Length);
				writer.Write(payload, 0, payload.Length);
				return stream.ToArray();
			}
		}

		/// <summary>Compatibility name for structural decode. Never performs semantic recovery.</summary>
		public static KingdomTradeBook DecodeEnvelope(byte[] Envelope)
		{
			return DecodeEnvelopeRaw(Envelope);
		}

		/// <summary>
		/// Total bounded structural decode. Core must inspect coexistence with legacy graphs before
		/// explicitly invoking KingdomTradeRules.Normalize; save loading cannot settle receipts.
		/// </summary>
		public static KingdomTradeBook DecodeEnvelopeRaw(byte[] Envelope)
		{
			if (Envelope == null || Envelope.Length < 12 || Envelope.Length > MaxEnvelopeBytes)
				throw new InvalidDataException("Trade envelope length is invalid.");
			using (MemoryStream stream = new MemoryStream(Envelope, false))
			using (BinaryReader reader = new BinaryReader(stream))
			{
				if (reader.ReadInt32() != Magic)
					throw new InvalidDataException("Unsupported pre-release named-field Trade encoding; unsafe migration refused.");
				int wire = reader.ReadInt32();
				int length = ReadCount(reader, MaxEnvelopeBytes - 12, "payload bytes");
				if (length != stream.Length - stream.Position)
					throw new InvalidDataException("Trade envelope payload length mismatch.");
				byte[] payload = reader.ReadBytes(length);
				if (payload.Length != length) throw new EndOfStreamException("Truncated Trade payload.");
				if (wire == 1)
					throw new InvalidDataException("Unsafe pre-release Trade wire v1 migration refused.");
				if (wire == PriorWireVersion)
					return DecodePayloadV3(payload);
				if (wire == ImmediatePriorWireVersion)
					return DecodePayloadV4(payload);
				if (wire != CurrentWireVersion)
				{
					if (wire <= 0) throw new InvalidDataException("Invalid Trade wire version.");
					return new KingdomTradeBook
					{
						FormatVersion = KingdomTradeRules.CurrentFormatVersion,
						SchemaState = KingdomTradeSchemaState.Unknown,
						SchemaFault = "Unsupported bounded Trade wire preserved as opaque non-authoritative evidence.",
						OpaqueWireVersion = wire,
						OpaqueFuturePayload = payload,
						IdentityBound = false
					};
				}
				return DecodePayload(payload);
			}
		}

		/// <summary>Deterministic authority bytes used by hostile-callback witnesses.</summary>
		public static byte[] EncodePayload(KingdomTradeBook Book)
		{
			if (Book == null) throw new ArgumentNullException(nameof(Book));
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(Book.FormatVersion);
				writer.Write((byte)Book.SchemaState);
				WriteString(writer, Book.SchemaFault);
				writer.Write(Book.LegacyMigrated);
				writer.Write(Book.LegacyRejected);
				WriteString(writer, Book.RealmId);
				writer.Write(Book.IdentityBound);
				WriteStringList(writer, Book.SettlementIds, KingdomTradeRules.MaxSettlementIds);
				writer.Write((byte)Book.OptionState);
				writer.Write(Book.OptionObservedTick);
				writer.Write(Book.OptionEpoch);
				writer.Write(Book.RestampPending);
				writer.Write(Book.NextCharterSequence);
				writer.Write(Book.NextOperationSequence);
				writer.Write(Book.RetiredThrough);
				WriteList(writer, Book.Charters, KingdomTradeRules.MaxCharters, WriteCharter);
				WriteNullable(writer, Book.Manifest, WriteManifest);
				WriteNullable(writer, Book.OpenOperation, WriteOperation);
				WriteNullable(writer, Book.PendingRetirement, WriteProof);
				WriteList(writer, Book.RecentProofs, KingdomTradeRules.MaxRecentProofs, WriteProof);
				WriteList(writer, Book.CompactedProofs, KingdomTradeRules.MaxCompactedProofs,
					WriteProofCompaction);
				WriteString(writer, Book.ActiveProjectionId);
				WriteString(writer, Book.ActiveProjectionObjectId);
				WriteList(writer, Book.Projections, KingdomTradeRules.MaxProjectionRows, WriteProjection);
				writer.Write(Book.RetainedEscrowDrams);
				writer.Write(Book.UnattributedArchivedEscrowDrams);
				WriteList(writer, Book.Archives, KingdomTradeRules.MaxArchives, WriteArchive);
				WriteList(writer, Book.Incidents, KingdomTradeRules.MaxIncidents, WriteIncident);
				writer.Flush();
				if (stream.Length > MaxEnvelopeBytes - 12)
					throw new InvalidDataException("Trade payload exceeds hard bound.");
				return stream.ToArray();
			}
		}

		/// <summary>Frozen wire-v3 payload writer used only to authenticate migration evidence.</summary>
		internal static byte[] EncodePayloadV3ForMigration(KingdomTradeBook Book)
		{
			if (Book == null) throw new ArgumentNullException(nameof(Book));
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(Book.FormatVersion);
				writer.Write((byte)Book.SchemaState);
				WriteString(writer, Book.SchemaFault);
				writer.Write(Book.LegacyMigrated);
				writer.Write(Book.LegacyRejected);
				WriteString(writer, Book.RealmId);
				writer.Write(Book.IdentityBound);
				WriteStringList(writer, Book.SettlementIds, KingdomTradeRules.MaxSettlementIds);
				writer.Write((byte)Book.OptionState);
				writer.Write(Book.OptionObservedTick);
				writer.Write(Book.OptionEpoch);
				writer.Write(Book.RestampPending);
				writer.Write(Book.NextCharterSequence);
				writer.Write(Book.NextOperationSequence);
				writer.Write(Book.RetiredThrough);
				WriteList(writer, Book.Charters, KingdomTradeRules.MaxCharters, WriteCharter);
				WriteNullable(writer, Book.Manifest, WriteManifest);
				WriteNullable(writer, Book.OpenOperation, WriteOperationV3);
				WriteNullable(writer, Book.PendingRetirement, WriteProofV4);
				WriteList(writer, Book.RecentProofs, KingdomTradeRules.MaxRecentProofs, WriteProofV4);
				WriteList(writer, Book.CompactedProofs, KingdomTradeRules.MaxCompactedProofs,
					WriteProofCompaction);
				WriteString(writer, Book.ActiveProjectionId);
				WriteString(writer, Book.ActiveProjectionObjectId);
				WriteList(writer, Book.Projections, KingdomTradeRules.MaxProjectionRows, WriteProjection);
				writer.Write(Book.RetainedEscrowDrams);
				writer.Write(Book.UnattributedArchivedEscrowDrams);
				WriteList(writer, Book.Archives, KingdomTradeRules.MaxArchives, WriteArchive);
				WriteList(writer, Book.Incidents, KingdomTradeRules.MaxIncidents, WriteIncident);
				writer.Flush();
				if (stream.Length > MaxEnvelopeBytes - 12)
					throw new InvalidDataException("Trade v3 payload exceeds hard bound.");
				return stream.ToArray();
			}
		}

#if TAF_TESTS
		/// <summary>Fixture-only exact prior writer; production saves always use current wire.</summary>
		public static byte[] EncodeEnvelopeV3Fixture(KingdomTradeBook Book)
		{
			byte[] payload = EncodePayloadV3ForMigration(Book);
			using (MemoryStream stream = new MemoryStream(12 + payload.Length))
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(Magic); writer.Write(PriorWireVersion); writer.Write(payload.Length);
				writer.Write(payload, 0, payload.Length);
				return stream.ToArray();
			}
		}

		/// <summary>Fixture-only exact wire-v4 writer; recipient witnesses did not exist.</summary>
		public static byte[] EncodeEnvelopeV4Fixture(KingdomTradeBook Book)
		{
			byte[] payload = EncodePayloadV4ForMigration(Book);
			using (MemoryStream stream = new MemoryStream(12 + payload.Length))
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(Magic); writer.Write(ImmediatePriorWireVersion);
				writer.Write(payload.Length); writer.Write(payload, 0, payload.Length);
				return stream.ToArray();
			}
		}

		/// <summary>Fixture-only current nested receipt framing for hostile-bound tests.</summary>
		public static byte[] EncodePatternFixture(KingdomTradePatternReceipt Receipt)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				WritePattern(writer, Receipt);
				writer.Flush();
				return stream.ToArray();
			}
		}

		/// <summary>Fixture-only current nested receipt decoder; trailing bytes are corrupt.</summary>
		public static KingdomTradePatternReceipt DecodePatternFixture(byte[] Payload)
		{
			if (Payload == null || Payload.Length > MaxEnvelopeBytes - 12)
				throw new InvalidDataException("Pattern fixture length is invalid.");
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader reader = new BinaryReader(stream))
			{
				KingdomTradePatternReceipt receipt = ReadPattern(reader);
				if (stream.Position != stream.Length)
					throw new InvalidDataException("Trailing pattern fixture bytes.");
				return receipt;
			}
		}
#endif
	}
}
