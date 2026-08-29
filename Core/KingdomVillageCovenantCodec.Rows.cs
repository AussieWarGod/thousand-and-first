using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// The body of the archive: the realm it is bound to, then the covenants themselves.
	/// <para>
	/// Every string on this wire is length-prefixed, capped in bytes rather than characters, and
	/// encoded in strict UTF-8 &mdash; strict because a lone surrogate is a perfectly ordinary
	/// <c>char</c> and a perfectly impossible piece of text. Refusing it at the writer's door means
	/// such a name is declined while it is still a candidate, instead of at the moment an archive
	/// that has already advanced its revision is asked to write itself out.
	/// </para>
	/// <para>
	/// Every row and the payload as a whole end where they say they end. A reader that stopped
	/// short of its own declared length would be quietly ignoring bytes somebody wrote, and a row
	/// with something after it is not a row this build produced.
	/// </para>
	/// </summary>
	public static partial class KingdomVillageCovenantCodec
	{
		internal static bool TryWrite(KingdomVillageCovenantArchive archive, out byte[] bytes,
			out string failure)
		{
			bytes = null;
			try
			{
				byte[] payload = Payload(archive);
				if (payload.Length > MaxPayloadBytes)
					return KingdomVillageCovenantRules.Fail("the covenant archive is "
						+ payload.Length + " payload bytes, past the " + MaxPayloadBytes
						+ " this build can lawfully write", out failure);
				using (MemoryStream stream = new MemoryStream())
				{
					using (BinaryWriter writer = Writer(stream))
					{
						writer.Write(Magic);
						writer.Write(CurrentWireVersion);
						writer.Write(payload.Length);
						writer.Write(payload);
						writer.Flush();
					}
					byte[] framed = stream.ToArray();
					byte[] digest = Digest(framed, framed.Length);
					byte[] whole = new byte[framed.Length + DigestBytes];
					Buffer.BlockCopy(framed, 0, whole, 0, framed.Length);
					Buffer.BlockCopy(digest, 0, whole, framed.Length, DigestBytes);
					if (whole.Length > MaxEnvelopeBytes)
						return KingdomVillageCovenantRules.Fail("the covenant archive is "
							+ whole.Length + " bytes, past the " + MaxEnvelopeBytes
							+ " this build can lawfully write", out failure);
					bytes = whole;
					failure = "";
					return true;
				}
			}
			catch (Exception error) when (WireFault(error))
			{
				return KingdomVillageCovenantRules.Fail("the covenant archive would not encode: "
					+ error.Message, out failure);
			}
		}

		private static byte[] Payload(KingdomVillageCovenantArchive archive)
		{
			using (MemoryStream body = new MemoryStream())
			{
				using (BinaryWriter writer = Writer(body))
				{
					WriteIdentity(writer, archive);
					writer.Write(Magic);
					writer.Write(CurrentWireVersion);
					writer.Write(archive.Revision);
					writer.Write(archive.Rows.Count);
					for (int i = 0; i < archive.Rows.Count; i++)
					{
						byte[] row = EncodeRow(archive.Rows[i]);
						if (row.Length > MaxAuthoredRowBytes)
							throw new InvalidDataException("a covenant row is " + row.Length
								+ " bytes, past the " + MaxAuthoredRowBytes
								+ " this build can lawfully author");
						writer.Write(row.Length);
						writer.Write(row);
					}
					writer.Flush();
				}
				return body.ToArray();
			}
		}

		internal static KingdomVillageCovenantArchive Read(byte[] snapshot,
			KingdomVillageCovenantFrame frame)
		{
			try
			{
				KingdomVillageCovenantArchive archive = new KingdomVillageCovenantArchive();
				using (MemoryStream body = new MemoryStream(snapshot, frame.PayloadStart,
					frame.PayloadLength, false))
				using (BinaryReader reader = Reader(body))
				{
					ReadIdentity(reader, archive);
					if (reader.ReadInt32() != Magic)
						throw new InvalidDataException("the nested archive magic is not this "
							+ "family's");
					int book = reader.ReadInt32();
					if (book != CurrentWireVersion)
						throw new InvalidDataException("the nested archive declares revision "
							+ book);
					archive.Revision = reader.ReadInt64();
					int count = reader.ReadInt32();
					if (count < 0 || count > KingdomVillageCovenantArchive.MaxRows)
						throw new InvalidDataException("the archive declares " + count
							+ " covenants");
					for (int i = 0; i < count; i++) archive.Rows.Add(ReadRow(reader));
					if (body.Position != body.Length)
						throw new InvalidDataException("there are bytes after the last covenant");
				}
				if (!KingdomVillageCovenantRules.TryValidate(archive, out string failure))
					throw new InvalidDataException(failure);
				return archive;
			}
			catch (Exception error) when (WireFault(error))
			{
				return Quarantine(snapshot, error.Message);
			}
		}

		private static byte[] EncodeRow(KingdomVillageCovenantReceipt row)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = Writer(stream))
			{
				writer.Write(row.Version);
				PutString(writer, row.RealmId, KingdomVillageCovenantCodec.MaxRealmIdBytes);
				PutString(writer, row.ReceiptId, KingdomVillageCovenantRules.MaxReceiptIdBytes);
				PutString(writer, row.TransactionId,
					KingdomVillageCovenantRules.MaxTransactionIdBytes);
				PutString(writer, row.FoundingAuthority,
					KingdomVillageCovenantRules.MaxAuthorityBytes);
				PutString(writer, row.VillageFactionId,
					KingdomVillageCovenantRules.MaxFactionIdBytes);
				PutString(writer, row.VillageDisplayName,
					KingdomVillageCovenantRules.MaxDisplayNameBytes);
				PutString(writer, row.SiteZoneId, KingdomVillageCovenantRules.MaxZoneIdBytes);
				PutString(writer, row.ChronicleEventId,
					KingdomVillageCovenantRules.MaxChronicleEventBytes);
				writer.Write(row.SealedStanding);
				writer.Write(row.ReservationTick);
				writer.Flush();
				return stream.ToArray();
			}
		}

		private static KingdomVillageCovenantReceipt ReadRow(BinaryReader reader)
		{
			int length = reader.ReadInt32();
			if (length < 1 || length > MaxRowBytes)
				throw new InvalidDataException("a covenant row declares " + length + " bytes");
			byte[] bytes = reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			using (MemoryStream stream = new MemoryStream(bytes, false))
			using (BinaryReader row = Reader(stream))
			{
				KingdomVillageCovenantReceipt receipt = new KingdomVillageCovenantReceipt
				{
					Version = row.ReadInt32(),
					RealmId = GetString(row, MaxRealmIdBytes),
					ReceiptId = GetString(row, KingdomVillageCovenantRules.MaxReceiptIdBytes),
					TransactionId = GetString(row,
						KingdomVillageCovenantRules.MaxTransactionIdBytes),
					FoundingAuthority = GetString(row,
						KingdomVillageCovenantRules.MaxAuthorityBytes),
					VillageFactionId = GetString(row,
						KingdomVillageCovenantRules.MaxFactionIdBytes),
					VillageDisplayName = GetString(row,
						KingdomVillageCovenantRules.MaxDisplayNameBytes),
					SiteZoneId = GetString(row, KingdomVillageCovenantRules.MaxZoneIdBytes),
					ChronicleEventId = GetString(row,
						KingdomVillageCovenantRules.MaxChronicleEventBytes),
					SealedStanding = row.ReadInt32(),
					ReservationTick = row.ReadInt64()
				};
				if (stream.Position != stream.Length)
					throw new InvalidDataException("there are bytes after the end of a covenant row");
				return receipt;
			}
		}

		private static void WriteIdentity(BinaryWriter writer,
			KingdomVillageCovenantArchive archive)
		{
			byte[] bytes = archive.RealmId == null
				? new byte[0] : Strict().GetBytes(archive.RealmId);
			if (bytes.Length > MaxRealmIdBytes)
				throw new InvalidDataException("the covenant archive's realm id is "
					+ bytes.Length + " bytes, past the " + MaxRealmIdBytes + " one can occupy");
			writer.Write(bytes.Length);
			writer.Write(bytes);
			writer.Write(archive.IdentityBound ? (byte)1 : (byte)0);
		}

		private static void ReadIdentity(BinaryReader reader,
			KingdomVillageCovenantArchive archive)
		{
			int length = reader.ReadInt32();
			if (length < 0 || length > MaxRealmIdBytes)
				throw new InvalidDataException("the covenant archive's realm id declares "
					+ length + " bytes");
			byte[] bytes = reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			archive.RealmId = length == 0 ? null : Strict().GetString(bytes);
			byte bound = reader.ReadByte();
			if (bound > 1)
				throw new InvalidDataException("the covenant archive's binding flag is neither "
					+ "bound nor unbound");
			archive.IdentityBound = bound == 1;
		}

		private static void PutString(BinaryWriter writer, string value, int maximum)
		{
			if (value == null) throw new InvalidDataException("a covenant field is absent");
			byte[] bytes = Strict().GetBytes(value);
			if (bytes.Length > maximum)
				throw new InvalidDataException("a covenant field is " + bytes.Length
					+ " bytes, past the " + maximum + " it may occupy");
			writer.Write(bytes.Length);
			writer.Write(bytes);
		}

		private static string GetString(BinaryReader reader, int maximum)
		{
			int length = reader.ReadInt32();
			if (length < 0 || length > maximum)
				throw new InvalidDataException("a covenant field declares " + length + " bytes");
			byte[] bytes = reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			return Strict().GetString(bytes);
		}

		private static UTF8Encoding Strict() { return new UTF8Encoding(false, true); }

		private static BinaryWriter Writer(Stream stream)
		{
			return new BinaryWriter(stream, Strict(), true);
		}

		private static BinaryReader Reader(Stream stream)
		{
			return new BinaryReader(stream, Strict(), true);
		}

		/// <summary>
		/// The fault set a payload problem may raise, and nothing wider. Anything outside it is a
		/// defect in this file rather than a fact about the bytes, and must not be caught here.
		/// </summary>
		internal static bool WireFault(Exception error)
		{
			return error is InvalidDataException || error is EndOfStreamException
				|| error is IOException || error is DecoderFallbackException
				|| error is EncoderFallbackException || error is ArgumentException
				|| error is OverflowException;
		}
	}
}
