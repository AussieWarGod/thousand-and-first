using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Harness
{
	// Heart and reservation text is opaque here; native witnesses prove its owner and meaning.
	internal static class KingdomQuickstartSaveSnapshotCodec
	{
		internal const string Prefix = "taf-quickstart-save-v1:";
		internal const int Magic = 0x31535154;
		internal const int Version = 1;
		internal const int MaxWireChars = 2097152;
		internal const int MaxSeedChars = 97;
		internal const int MaxFounderIdChars = 512;
		internal const int MaxReceiptChars = 4096;
		internal const int MaxHeartReceiptChars = 524288;
		internal const int MaxHeartTerminalChars = 131072;
		internal const int MaxReservationsChars = 65536;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static bool MatchesPrefix(string wire)
		{
			return wire != null && wire.StartsWith(Prefix, StringComparison.Ordinal);
		}

		internal static bool Valid(KingdomQuickstartSaveSnapshot value)
		{
			try
			{
				if (value == null || !Text(value.GameId, 36, false) || value.GameId.Length != 36
					|| !Guid.TryParseExact(value.GameId, "D", out Guid gameId)
					|| gameId.ToString("D") != value.GameId
					|| !Text(value.Seed, MaxSeedChars, false) || !KingdomScenarioRules.ValidSeed(value.Seed)
					|| !Text(value.ProfileKey, 6, false)
					|| !KingdomQuickstartRules.TryProfile(value.ProfileKey, out KingdomQuickstartProfile profile)
					|| value.FounderBaseId <= 0 || !Text(value.FounderId, MaxFounderIdChars, true)
					|| value.Turns < 0 || value.TimeTicks < 0 || value.ActionTicks < 0 || value.PlayerActionTicks < 0
					|| !Text(value.ReceiptWire, MaxReceiptChars, false)
					|| !KingdomQuickstartRules.TryDecode(value.ReceiptWire, out KingdomQuickstartReceipt receipt)
					|| receipt.Phase != KingdomQuickstartPhase.Complete || receipt.ProfileKey != value.ProfileKey
					|| receipt.ZoneId != profile.ZoneId
					|| receipt.AdvisorDisposition != (value.Advisor ? KingdomQuickstartAdvisorDisposition.Included
						: KingdomQuickstartAdvisorDisposition.Omitted)
					|| KingdomQuickstartRules.Encode(receipt) != value.ReceiptWire
					|| !Text(value.HeartReceipt, MaxHeartReceiptChars, false) || !Seal(value.HeartSeal)
					|| !Text(value.HeartTerminal, MaxHeartTerminalChars, true)
					|| !Text(value.ReservationsWire, MaxReservationsChars, false)) return false;
				long bytes = 8L + 1L + 4L + 4L * 8L;
				bytes += FrameBytes(value.GameId) + FrameBytes(value.Seed) + FrameBytes(value.ProfileKey)
					+ FrameBytes(value.FounderId) + FrameBytes(value.ReceiptWire) + FrameBytes(value.HeartReceipt)
					+ FrameBytes(value.HeartSeal) + FrameBytes(value.HeartTerminal) + FrameBytes(value.ReservationsWire);
				return Prefix.Length + ((bytes + 2L) / 3L) * 4L <= MaxWireChars;
			}
			catch (Exception) { return false; }
		}

		internal static bool TryEncode(KingdomQuickstartSaveSnapshot value, out string wire)
		{
			wire = null;
			if (!Valid(value)) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Utf8, true))
				{
					writer.Write(Magic); writer.Write(Version);
					WriteText(writer, value.GameId); WriteText(writer, value.Seed); WriteText(writer, value.ProfileKey);
					writer.Write((byte)(value.Advisor ? 1 : 0)); writer.Write(value.FounderBaseId);
					WriteText(writer, value.FounderId);
					writer.Write(value.Turns); writer.Write(value.TimeTicks);
					writer.Write(value.ActionTicks); writer.Write(value.PlayerActionTicks);
					WriteText(writer, value.ReceiptWire); WriteText(writer, value.HeartReceipt);
					WriteText(writer, value.HeartSeal); WriteText(writer, value.HeartTerminal);
					WriteText(writer, value.ReservationsWire); writer.Flush();
					string encoded = Prefix + Convert.ToBase64String(stream.ToArray());
					if (encoded.Length > MaxWireChars) return false;
					wire = encoded; return true;
				}
			}
			catch (Exception) { return false; }
		}

		internal static bool TryDecode(string wire, out KingdomQuickstartSaveSnapshot value)
		{
			value = null;
			if (wire == null || wire.Length > MaxWireChars || !MatchesPrefix(wire)) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream(Convert.FromBase64String(wire.Substring(Prefix.Length)), false))
				using (BinaryReader reader = new BinaryReader(stream, Utf8, true))
				{
					if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version) return false;
					string gameId = ReadText(reader, 36, false), seed = ReadText(reader, MaxSeedChars, false);
					string profile = ReadText(reader, 6, false);
					byte advisor = reader.ReadByte();
					if (advisor > 1) return false;
					int founderBaseId = reader.ReadInt32();
					string founderId = ReadText(reader, MaxFounderIdChars, true);
					long turns = reader.ReadInt64(), time = reader.ReadInt64();
					long action = reader.ReadInt64(), playerAction = reader.ReadInt64();
					string receipt = ReadText(reader, MaxReceiptChars, false);
					string heart = ReadText(reader, MaxHeartReceiptChars, false), seal = ReadText(reader, 68, false);
					string terminal = ReadText(reader, MaxHeartTerminalChars, true);
					string reservations = ReadText(reader, MaxReservationsChars, false);
					KingdomQuickstartSaveSnapshot decoded = new KingdomQuickstartSaveSnapshot(gameId, seed, profile,
						advisor == 1, founderBaseId, founderId, turns, time, action, playerAction,
						receipt, heart, seal, terminal, reservations);
					if (stream.Position != stream.Length || !TryEncode(decoded, out string canonical) || canonical != wire)
						return false;
					value = decoded; return true;
				}
			}
			catch (Exception) { return false; }
		}

		private static bool Seal(string value)
		{
			if (value == null || value.Length != 68 || !value.StartsWith("hs1-", StringComparison.Ordinal)) return false;
			for (int i = 4; i < value.Length; i++)
				if (!(value[i] >= '0' && value[i] <= '9' || value[i] >= 'a' && value[i] <= 'f')) return false;
			return true;
		}

		// Optional text preserves both absence (-1) and present empty text (0).
		private static bool Text(string value, int maximum, bool optional)
		{
			if (value == null || value.Length == 0) return optional;
			if (value.Length > maximum) return false;
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if (char.IsControl(c)) return false;
				if (char.IsHighSurrogate(c))
				{
					if (i + 1 == value.Length || !char.IsLowSurrogate(value[++i])) return false;
				}
				else if (char.IsLowSurrogate(c)) return false;
			}
			return true;
		}

		private static long FrameBytes(string value)
		{
			return 4L + (value == null ? 0L : Utf8.GetByteCount(value));
		}

		private static void WriteText(BinaryWriter writer, string value)
		{
			if (value == null) { writer.Write(-1); return; }
			byte[] bytes = Utf8.GetBytes(value);
			writer.Write(bytes.Length); writer.Write(bytes);
		}

		private static string ReadText(BinaryReader reader, int maximum, bool optional)
		{
			int length = reader.ReadInt32();
			if (length == -1)
			{
				if (!optional) throw new InvalidDataException();
				return null;
			}
			if (length < 0 || (long)length > (long)maximum * 4L
				|| length > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException();
			byte[] bytes = reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			string value = Utf8.GetString(bytes);
			if (!Text(value, maximum, optional)) throw new InvalidDataException();
			return value;
		}
	}
}
