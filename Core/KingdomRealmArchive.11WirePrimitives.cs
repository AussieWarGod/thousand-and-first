using System;
using System.Collections.Generic;
using System.IO;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	public sealed partial class KingdomRealmArchive
	{
#if !TAF_TESTS
		private static void WriteString(SerializationWriter Writer, string Value, int MaxChars)
		{
			if (Value == null) { Writer.Write(-1); return; }
			if (Value.Length > MaxChars) throw new InvalidDataException("Realm archive string exceeds cap.");
			byte[] bytes = StrictUtf8.GetBytes(Value);
			if (bytes.Length > MaxTextBytes * 1024) throw new InvalidDataException("Realm archive UTF-8 exceeds cap.");
			Writer.Write(bytes.Length); Writer.Write(bytes, 0, bytes.Length);
		}

		private static string ReadString(SerializationReader Reader, int MaxChars)
		{
			int length = Reader.ReadInt32();
			if (length == -1) return null;
			int maxBytes = Math.Min(MaxTextBytes * 1024, checked(MaxChars * 4));
			if (length < 0 || length > maxBytes) throw new InvalidDataException("Realm archive string length exceeds cap.");
			byte[] bytes = Reader.ReadBytesDirect(length);
			if (bytes.Length != length) throw new EndOfStreamException("Truncated realm archive string.");
			string value = StrictUtf8.GetString(bytes);
			if (value.Length > MaxChars) throw new InvalidDataException("Realm archive decoded string exceeds cap.");
			return value;
		}

		private static void WriteStrings(SerializationWriter Writer, List<string> Values,
			int MaxCount, int MaxChars)
		{
			if (Values == null || Values.Count > MaxCount) throw new InvalidDataException("Realm archive list exceeds cap.");
			Writer.Write(Values.Count);
			for (int i = 0; i < Values.Count; i++) WriteString(Writer, Values[i], MaxChars);
		}

		private static List<string> ReadStrings(SerializationReader Reader, int MaxCount,
			int MaxChars)
		{
			int count = Reader.ReadInt32();
			if (count < 0 || count > MaxCount) throw new InvalidDataException("Realm archive list count exceeds cap.");
			List<string> values = new List<string>(count);
			for (int i = 0; i < count; i++) values.Add(ReadString(Reader, MaxChars));
			return values;
		}

		private static void WriteArchivedSettlement(SerializationWriter Writer,
			KingdomSettlement Value, byte[] Opaque)
		{
			byte[] payload = Opaque;
			if (payload == null && !KingdomArchivedSettlementCodec.TryEncode(Value,
				out payload, out string failure)) throw new InvalidDataException(failure);
			if (payload == null || payload.Length < 8 ||
				payload.Length > KingdomArchivedSettlementCodec.MaxPayloadBytes)
				throw new InvalidDataException("Archived settlement payload exceeds cap.");
			Writer.Write(payload.Length);
			Writer.Write(payload, 0, payload.Length);
		}

		private static KingdomSettlement ReadArchivedSettlement(SerializationReader Reader,
			out byte[] Opaque, out int WireVersion)
		{
			Opaque = null;
			WireVersion = 0;
			int length = Reader.ReadInt32();
			if (length < 8 || length > KingdomArchivedSettlementCodec.MaxPayloadBytes)
				throw new InvalidDataException("Archived settlement raw length exceeds cap.");
			byte[] payload = Reader.ReadBytesDirect(length);
			if (payload.Length != length)
				throw new EndOfStreamException("Archived settlement payload is truncated.");
			if (KingdomArchivedSettlementCodec.TryDecode(payload, out KingdomSettlement value,
				out int future, out string failure))
			{
				WireVersion = KingdomArchivedSettlementCodec.CurrentVersion;
				return value;
			}
			if (future > KingdomArchivedSettlementCodec.CurrentVersion)
			{
				Opaque = payload;
				WireVersion = future;
				return null;
			}
			throw new InvalidDataException(failure);
		}

		private static void WriteStandings(SerializationWriter Writer,
			Dictionary<string, int> Value)
		{
			if (!BoundedStandings(Value))
				throw new InvalidDataException("Archived standings exceed cap.");
			List<string> keys = new List<string>(Value.Keys);
			keys.Sort(StringComparer.Ordinal);
			Writer.Write(keys.Count);
			for (int i = 0; i < keys.Count; i++)
			{
				WriteString(Writer, keys[i], 512);
				Writer.Write(Value[keys[i]]);
			}
		}

		private static Dictionary<string, int> ReadStandings(SerializationReader Reader)
		{
			int count = Reader.ReadInt32();
			if (count < 0 || count > 512)
				throw new InvalidDataException("Archived standings count exceeds cap.");
			Dictionary<string, int> value = new Dictionary<string, int>(count,
				StringComparer.Ordinal);
			string previous = null;
			for (int i = 0; i < count; i++)
			{
				string key = ReadString(Reader, 512);
				if (key == null || (previous != null &&
					string.CompareOrdinal(previous, key) >= 0))
					throw new InvalidDataException("Archived standings order is noncanonical.");
				value.Add(key, Reader.ReadInt32());
				previous = key;
			}
			return value;
		}

		private static void WriteCallback(SerializationWriter Writer,
			KingdomRealmCallbackReceipt Value)
		{
			if (!ValidCallbackEnvelope(Value))
				throw new InvalidDataException("Archived callback receipt exceeds cap.");
			Writer.Write((byte)Value.Phase); Writer.Write((byte)Value.Disposition);
			Writer.Write((byte)Value.Scope);
			WriteString(Writer, Value.BeforeGraph, 64); WriteString(Writer, Value.AfterGraph, 64);
			WriteString(Writer, Value.BeforeArchiveGraph, 64);
			WriteString(Writer, Value.AfterArchiveGraph, 64);
			WriteString(Writer, Value.BeforeEffect, KingdomRealmCallbackReceipt.MaxEffectChars);
			WriteString(Writer, Value.AfterEffect, KingdomRealmCallbackReceipt.MaxEffectChars);
			WriteString(Writer, Value.ObservedEffect, KingdomRealmCallbackReceipt.MaxEffectChars);
			Writer.Write(Value.BeforeStamp); Writer.Write(Value.AfterStamp);
		}

		private static KingdomRealmCallbackReceipt ReadCallback(SerializationReader Reader)
		{
			KingdomRealmCallbackReceipt value = new KingdomRealmCallbackReceipt
			{
				Phase = (KingdomRealmCallbackPhase)Reader.ReadByte(),
				Disposition = (KingdomRealmCallbackDisposition)Reader.ReadByte(),
				Scope = (KingdomRealmCallbackScope)Reader.ReadByte(),
				BeforeGraph = ReadString(Reader, 64),
				AfterGraph = ReadString(Reader, 64),
				BeforeArchiveGraph = ReadString(Reader, 64),
				AfterArchiveGraph = ReadString(Reader, 64),
				BeforeEffect = ReadString(Reader, KingdomRealmCallbackReceipt.MaxEffectChars),
				AfterEffect = ReadString(Reader, KingdomRealmCallbackReceipt.MaxEffectChars),
				ObservedEffect = ReadString(Reader, KingdomRealmCallbackReceipt.MaxEffectChars),
				BeforeStamp = Reader.ReadInt32(),
				AfterStamp = Reader.ReadInt32()
			};
			if (!ValidCallbackEnvelope(value))
				throw new InvalidDataException("Archived callback receipt is malformed.");
			return value;
		}

#endif
	}
}
