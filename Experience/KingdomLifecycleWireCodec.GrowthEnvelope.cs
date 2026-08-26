using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{

		public static byte[] GrowthPayloadForWrite(KingdomGrowthBook Book)
		{
			if (Book == null) throw new InvalidDataException("growth authority is absent");
			if (Book.OpaquePayload != null)
			{
				if (!KingdomLifecycleRules.GrowthEnvelopeWritable(Book))
					throw new InvalidDataException("opaque growth envelope is malformed");
				return (byte[])Book.OpaquePayload.Clone();
			}
			if (!KingdomLifecycleRules.GrowthEnvelopeWritable(Book))
				throw new InvalidDataException("growth envelope is not bounded and writable");
			using (GrowthCappedWriteStream stream =
				new GrowthCappedWriteStream(KingdomLifecycleRules.MaxGrowthSectionBytes))
			using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
			{
				WriteGrowth(writer, Book);
				writer.Flush();
				return stream.ToArray();
			}
		}

		/// <summary>Test/migration fixture writer for the exact historical Growth-v1 layout.
		/// Production writers always emit the current version.</summary>
		internal static byte[] GrowthV1PayloadFixture(KingdomGrowthBook Book)
		{
			if (Book == null || Book.OpaquePayload != null
				|| !KingdomLifecycleRules.GrowthEnvelopeWritable(Book))
				throw new InvalidDataException("growth v1 fixture source is malformed");
			KingdomGrowthBook fixture = ReadGrowthPayload(GrowthPayloadForWrite(Book));
			if (fixture == null || fixture.Quarantined || fixture.OpaquePayload != null
				|| !GrowthV1OperationsRepresentable(fixture)
				|| !KingdomLifecycleRules.DowngradeGrowthArrivalCandidateForV1Fixture(
					fixture.ArrivalCandidate))
				throw new InvalidDataException("growth v1 candidate fixture could not downgrade");
			using (GrowthCappedWriteStream stream =
				new GrowthCappedWriteStream(KingdomLifecycleRules.MaxGrowthSectionBytes))
			using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
			{
				WriteGrowth(writer, fixture, KingdomLifecycleRules.LegacyGrowthFormatVersion);
				writer.Flush(); return stream.ToArray();
			}
		}

		/// <summary>Exact Growth-v2 fixture. Semantic person payloads did not exist in v2, so
		/// only a null or explicitly legacy candidate is representable.</summary>
		internal static byte[] GrowthV2PayloadFixture(KingdomGrowthBook Book)
		{
			if (Book == null || Book.OpaquePayload != null
				|| !KingdomLifecycleRules.GrowthEnvelopeWritable(Book))
				throw new InvalidDataException("growth v2 fixture source is malformed");
			KingdomGrowthBook fixture = ReadGrowthPayload(GrowthPayloadForWrite(Book));
			if (fixture == null || fixture.Quarantined || fixture.OpaquePayload != null
				|| fixture.ArrivalCandidate != null
					&& !fixture.ArrivalCandidate.LegacySemanticPlan)
				throw new InvalidDataException("growth v2 cannot encode a semantic person plan");
			using (GrowthCappedWriteStream stream =
				new GrowthCappedWriteStream(KingdomLifecycleRules.MaxGrowthSectionBytes))
			using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
			{
				WriteGrowth(writer, fixture, KingdomLifecycleRules.PreviousGrowthFormatVersion);
				writer.Flush(); return stream.ToArray();
			}
		}

		private static bool GrowthV1OperationsRepresentable(KingdomGrowthBook Book)
		{
			if (Book == null) return false;
			KingdomGrowthOperation[] direct = { Book.HeartbeatOp, Book.ArrivalOp,
				Book.DepartureOp, Book.DeliveryOp, Book.FetchOp, Book.MillOp };
			for (int i = 0; i < direct.Length; i++)
				if (!GrowthV1OperationRepresentable(direct[i])) return false;
			if (Book.FieldOps == null) return false;
			for (int i = 0; i < Book.FieldOps.Count; i++)
				if (Book.FieldOps[i] != null
					&& !GrowthV1OperationRepresentable(Book.FieldOps[i].Operation)) return false;
			return true;
		}

		private static bool GrowthV1OperationRepresentable(KingdomGrowthOperation Operation)
		{
			return Operation == null || Operation.LegacyGrowthV1Plan
				|| Operation.OutboxEvents != null && Operation.OutboxEvents.Count == 0;
		}

		internal static bool GrowthPayloadFitsAggregateCap(KingdomGrowthBook Book)
		{
			if (Book == null || Book.OpaquePayload != null) return false;
			try
			{
				using (GrowthCappedWriteStream stream =
					new GrowthCappedWriteStream(KingdomLifecycleRules.MaxGrowthSectionBytes))
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					WriteGrowth(writer, Book); writer.Flush();
					return stream.Length <= KingdomLifecycleRules.MaxGrowthSectionBytes;
				}
			}
			catch (Exception ex) when (ex is InvalidDataException || ex is IOException
				|| ex is EncoderFallbackException || ex is ArgumentException)
			{
				return false;
			}
		}

		internal static bool OpaqueGrowthEnvelopeWritable(KingdomGrowthBook Book)
		{
			if (Book == null || !Book.Quarantined || Book.OpaquePayload == null
				|| string.IsNullOrEmpty(Book.Fault)
				|| Book.OpaquePayload.Length > KingdomLifecycleRules.MaxGrowthSectionBytes) return false;
			try
			{
				KingdomGrowthBook derived = ReadGrowthPayload(Book.OpaquePayload);
				if (derived.OpaquePayload == null
					|| derived.OpaqueWireVersion != Book.OpaqueWireVersion
					|| !string.Equals(derived.Fault, Book.Fault, StringComparison.Ordinal)
					|| derived.OpaquePayload.Length != Book.OpaquePayload.Length) return false;
				for (int i = 0; i < Book.OpaquePayload.Length; i++)
					if (derived.OpaquePayload[i] != Book.OpaquePayload[i]) return false;
				return KingdomLifecycleRules.OpaqueGrowthParsedStateIsPristine(Book);
			}
			catch (Exception) { return false; }
		}

		public static KingdomGrowthBook ReadGrowthPayload(byte[] Payload)
		{
			if (Payload == null || Payload.Length > KingdomLifecycleRules.MaxGrowthSectionBytes)
				throw new InvalidDataException("growth payload framing is malformed");
			if (Payload.Length < 8)
				return OpaqueGrowth(Payload, 0, "growth payload is too short");
			int headerVersion = 0;
			bool hasHeaderVersion = false;
			try
			{
				using (MemoryStream stream = new MemoryStream(Payload, false))
				using (BinaryReader reader = new BinaryReader(stream, StrictUtf8, true))
				{
					if (reader.ReadInt32() != GrowthMagic)
						return OpaqueGrowth(Payload, 0, "growth payload marker is malformed");
					int version = reader.ReadInt32();
					headerVersion = version;
					hasHeaderVersion = true;
					if (version > KingdomLifecycleRules.CurrentGrowthFormatVersion)
						return OpaqueGrowth(Payload, version,
							"future growth payload preserved as opaque evidence");
					if (version != KingdomLifecycleRules.CurrentGrowthFormatVersion
						&& version != KingdomLifecycleRules.PreviousGrowthFormatVersion
						&& version != KingdomLifecycleRules.LegacyGrowthFormatVersion)
						return OpaqueGrowth(Payload, version,
							"growth payload version is unsupported");
					KingdomGrowthBook value = ReadGrowth(reader, version);
					if (stream.Position != stream.Length)
						return OpaqueGrowth(Payload, version,
							"growth payload has trailing bytes");
					if (!KingdomLifecycleRules.GrowthEnvelopeWritable(value))
						return OpaqueGrowth(Payload, version,
							"malformed current growth payload preserved as opaque evidence");
					return value;
				}
			}
			catch (Exception ex)
			{
				return OpaqueGrowth(Payload, hasHeaderVersion ? headerVersion : 0,
					"malformed growth payload: " + BoundFault(ex.Message));
			}
		}

		private static KingdomGrowthBook ReadGrowthSection(BinaryReader Reader)
		{
			int length = Reader.ReadInt32();
			if (length < 0 || length > KingdomLifecycleRules.MaxGrowthSectionBytes)
				throw new InvalidDataException("growth section length exceeds framing bounds");
			byte[] payload = Reader.ReadBytes(length);
			if (payload.Length != length)
				throw new EndOfStreamException("growth section is truncated");
			return ReadGrowthPayload(payload);
		}
	}
}
