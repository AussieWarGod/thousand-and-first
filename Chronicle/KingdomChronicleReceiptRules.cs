using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public enum KingdomChronicleSinkDisposition : byte
	{
		None = 0,
		Pending = 1,
		Attempting = 2,
		Delivered = 3,
		Skipped = 4,
		Lost = 5
	}

	public enum KingdomChronicleListAction : byte
	{
		Settled = 0,
		Append = 1,
		ConfirmDelivered = 2,
		MarkLost = 3
	}

	public enum KingdomChronicleRegistryFault : byte
	{
		None = 0,
		RawTooLong = 1,
		TooManyRows = 2,
		UnknownVersion = 3,
		MalformedHeader = 4,
		MalformedRow = 5,
		DuplicateIdentity = 6,
		RegistryTooLong = 7,
		CryptoUnavailable = 8
	}

	/// <summary>One exact keyed chronicle receipt. Compact rows retain replay identity,
	/// fingerprint, dispositions, and nothing needed only while a sink is active.</summary>
	public sealed class KingdomChronicleReceipt
	{
		public string EventId;
		public string Fingerprint;
		public string Official;
		public string Outsider;
		public string OfficialBefore;
		public string OfficialAfter;
		public string OutsiderBefore;
		public string OutsiderAfter;
		public KingdomChronicleSinkDisposition OfficialState;
		public KingdomChronicleSinkDisposition OutsiderState;
		public KingdomChronicleSinkDisposition JournalState;
		public long Updated;
		public bool Compact;
		/// <summary>A v1 row retained exactly by EventId but never authorized by its old FNV
		/// fingerprints. Exact construction identities may settle Lost to release their job.</summary>
		public bool LegacyBlocked;

		public KingdomChronicleReceipt Copy()
		{
			return new KingdomChronicleReceipt
			{
				EventId = EventId,
				Fingerprint = Fingerprint,
				Official = Official,
				Outsider = Outsider,
				OfficialBefore = OfficialBefore,
				OfficialAfter = OfficialAfter,
				OutsiderBefore = OutsiderBefore,
				OutsiderAfter = OutsiderAfter,
				OfficialState = OfficialState,
				OutsiderState = OutsiderState,
				JournalState = JournalState,
				Updated = Updated,
				Compact = Compact,
				LegacyBlocked = LegacyBlocked
			};
		}
	}

	/// <summary>Pure v3 codec and hash law for the engine-coupled chronicle shell.</summary>
	public static class KingdomChronicleReceiptRules
	{
		public const string Header = "taf-chronicle|3";
		public const int MaxReceipts = 4096;
		public const int MaxEventIdChars = 256;
		public const int MaxEventTextChars = 4096;
		public const int MaxMuralTextChars = 8192;
		public const int MaxEntryChars = 8192;
		public const int MaxEntries = 200;
		public const int MaxConstructionCoordinateChars = 128;
		public const int MaxRegistryChars = 4500000;
		public const int MaxActiveRowChars = 70000;
		public const int MaxTerminalRowChars = 4096;
		public const int MaxEncodedIdChars = 2048;
		public const int MaxEncodedEntryChars = 32768;
		public const int Sha256HexChars = 64;
		private const int LegacyMaxReceipts = 64;
		private const int LegacyHashChars = 16;
		private const int LegacyMaxHashEncodedChars = 128;
		private const int LegacyMaxRowChars = 70000;
		private const int MaxHashBytes = 2000000;
		private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static bool IsKnown(KingdomChronicleSinkDisposition State)
		{
			return State >= KingdomChronicleSinkDisposition.None
				&& State <= KingdomChronicleSinkDisposition.Lost;
		}

		public static bool IsSettled(KingdomChronicleSinkDisposition State)
		{
			return State == KingdomChronicleSinkDisposition.Delivered
				|| State == KingdomChronicleSinkDisposition.Skipped
				|| State == KingdomChronicleSinkDisposition.Lost;
		}

		public static bool IsTerminal(KingdomChronicleReceipt Receipt)
		{
			return Receipt != null && IsSettled(Receipt.OfficialState)
				&& IsSettled(Receipt.OutsiderState) && IsSettled(Receipt.JournalState);
		}

		public static bool IsListDisposition(KingdomChronicleSinkDisposition State)
		{
			return State == KingdomChronicleSinkDisposition.Pending
				|| State == KingdomChronicleSinkDisposition.Attempting
				|| State == KingdomChronicleSinkDisposition.Delivered
				|| State == KingdomChronicleSinkDisposition.Lost;
		}

		public static bool IsJournalDisposition(KingdomChronicleSinkDisposition State)
		{
			return State == KingdomChronicleSinkDisposition.Pending
				|| State == KingdomChronicleSinkDisposition.Attempting
				|| State == KingdomChronicleSinkDisposition.Delivered
				|| State == KingdomChronicleSinkDisposition.Skipped
				|| State == KingdomChronicleSinkDisposition.Lost;
		}

		public static KingdomChronicleSinkDisposition RecoverUninspectable(
			KingdomChronicleSinkDisposition State)
		{
			return State == KingdomChronicleSinkDisposition.Attempting
				? KingdomChronicleSinkDisposition.Lost : State;
		}

		public static KingdomChronicleListAction ListAction(
			KingdomChronicleSinkDisposition State, string CurrentHash,
			string BeforeHash, string AfterHash)
		{
			if (!IsKnown(State) || !IsSha256(CurrentHash) || !IsSha256(BeforeHash)
				|| !IsSha256(AfterHash)) return KingdomChronicleListAction.MarkLost;
			if (IsSettled(State)) return KingdomChronicleListAction.Settled;
			// A full bounded list can be a fixed point (for example 200 identical
			// values). Equal before/after hashes cannot distinguish no append from a
			// completed append, so they authorize neither confirmation nor retry.
			if (string.Equals(BeforeHash, AfterHash, StringComparison.Ordinal))
				return KingdomChronicleListAction.MarkLost;
			if (string.Equals(CurrentHash, AfterHash, StringComparison.Ordinal))
				return KingdomChronicleListAction.ConfirmDelivered;
			if (string.Equals(CurrentHash, BeforeHash, StringComparison.Ordinal))
				return KingdomChronicleListAction.Append;
			return KingdomChronicleListAction.MarkLost;
		}

		public static bool TryFingerprint(string EventId, string Text, bool Accomplishment,
			string MuralText, out string Fingerprint)
		{
			Fingerprint = null;
			if (string.IsNullOrEmpty(EventId) || EventId.Length > MaxEventIdChars
				|| Text == null || Text.Length > MaxEventTextChars
				|| (MuralText != null && MuralText.Length > MaxMuralTextChars)) return false;
			return TryCanonicalHash("taf-chronicle-fingerprint-v3",
				new string[4] { EventId, Text, Accomplishment ? "1" : "0", MuralText },
				out Fingerprint);
		}

		public static bool TryHashList(string Register, IList<string> Values, out string Hash)
		{
			Hash = null;
			if ((Register != "official" && Register != "outsider") || Values == null
				|| Values.Count > MaxEntries) return false;
			string[] fields = new string[Values.Count];
			for (int i = 0; i < Values.Count; i++)
			{
				if (Values[i] == null || Values[i].Length > MaxEntryChars) return false;
				fields[i] = Values[i];
			}
			return TryCanonicalHash("taf-chronicle-list-v3:" + Register, fields, out Hash);
		}

		public static bool TryHashAfter(string Register, IList<string> Values, string Value,
			out string Hash)
		{
			Hash = null;
			if (Values == null || Values.Count > MaxEntries || Value == null
				|| Value.Length > MaxEntryChars) return false;
			List<string> copy = new List<string>(Values);
			AppendBounded(copy, Value);
			return TryHashList(Register, copy, out Hash);
		}

		public static void AppendBounded(List<string> Values, string Value)
		{
			Values.Add(Value);
			if (Values.Count > MaxEntries) Values.RemoveAt(0);
		}

		/// <summary>SHA-256 over a versioned domain and fields encoded as exact UTF-8 byte
		/// lengths. Null is distinct from empty; field boundaries cannot alias.</summary>
		public static bool TryCanonicalHash(string Domain, IList<string> Fields, out string Hash)
		{
			Hash = null;
			if (string.IsNullOrEmpty(Domain) || Domain.Length > 128 || Fields == null
				|| Fields.Count > MaxEntries + 8) return false;
			try
			{
				using (MemoryStream bytes = new MemoryStream())
				{
					WriteField(bytes, "TAF-CHRONICLE-HASH-V3");
					WriteField(bytes, Domain);
					WriteUInt32(bytes, (uint)Fields.Count);
					for (int i = 0; i < Fields.Count; i++)
					{
						if (Fields[i] != null)
						{
							int byteCount = StrictUtf8.GetByteCount(Fields[i]);
							if (byteCount > MaxHashBytes
								|| bytes.Length + byteCount + 4L > MaxHashBytes) return false;
						}
						WriteField(bytes, Fields[i]);
						if (bytes.Length > MaxHashBytes) return false;
					}
					using (SHA256 sha = SHA256.Create())
					{
						if (sha == null) return false;
						Hash = LowerHex(sha.ComputeHash(bytes.ToArray()));
						return IsSha256(Hash);
					}
				}
			}
			catch
			{
				Hash = null;
				return false;
			}
		}

		private static void WriteField(Stream Stream, string Value)
		{
			if (Value == null)
			{
				WriteUInt32(Stream, uint.MaxValue);
				return;
			}
			byte[] bytes = StrictUtf8.GetBytes(Value);
			WriteUInt32(Stream, (uint)bytes.Length);
			Stream.Write(bytes, 0, bytes.Length);
		}

		private static void WriteUInt32(Stream Stream, uint Value)
		{
			Stream.WriteByte((byte)(Value >> 24));
			Stream.WriteByte((byte)(Value >> 16));
			Stream.WriteByte((byte)(Value >> 8));
			Stream.WriteByte((byte)Value);
		}

		private static string LowerHex(byte[] Bytes)
		{
			char[] text = new char[Bytes.Length * 2];
			const string hex = "0123456789abcdef";
			for (int i = 0; i < Bytes.Length; i++)
			{
				text[i * 2] = hex[Bytes[i] >> 4];
				text[i * 2 + 1] = hex[Bytes[i] & 15];
			}
			return new string(text);
		}

		public static bool TryConstructionIdentity(string EventId, out string JobId,
			out string Coordinate)
		{
			JobId = null;
			Coordinate = null;
			const string prefix = "construction:";
			if (string.IsNullOrEmpty(EventId) || EventId.Length > MaxEventIdChars
				|| !EventId.StartsWith(prefix, StringComparison.Ordinal)
				|| EventId.Length <= prefix.Length + 33) return false;
			string job = EventId.Substring(prefix.Length, 32);
			if (!IsLowerHex(job, 32) || EventId[prefix.Length + 32] != ':') return false;
			string coordinate = EventId.Substring(prefix.Length + 33);
			if (string.IsNullOrEmpty(coordinate)
				|| coordinate.Length > MaxConstructionCoordinateChars) return false;
			JobId = job;
			Coordinate = coordinate;
			return string.Equals(EventId, prefix + JobId + ":" + Coordinate,
				StringComparison.Ordinal);
		}

		public static KingdomChronicleReceipt Compact(KingdomChronicleReceipt Receipt)
		{
			if (!IsTerminal(Receipt)) return null;
			KingdomChronicleReceipt copy = Receipt.Copy();
			copy.Compact = true;
			copy.Official = null;
			copy.Outsider = null;
			copy.OfficialBefore = null;
			copy.OfficialAfter = null;
			copy.OutsiderBefore = null;
			copy.OutsiderAfter = null;
			return copy;
		}

		public static bool TryParseRegistry(string Text,
			out List<KingdomChronicleReceipt> Receipts, out bool MigratedLegacy,
			out KingdomChronicleRegistryFault Fault)
		{
			Receipts = new List<KingdomChronicleReceipt>();
			MigratedLegacy = false;
			Fault = KingdomChronicleRegistryFault.None;
			if (string.IsNullOrEmpty(Text)) return true;
			if (Text.Length > MaxRegistryChars)
			{
				Fault = KingdomChronicleRegistryFault.RawTooLong;
				return false;
			}
			try
			{
				if (Text == "v1" || Text.StartsWith("v1\n", StringComparison.Ordinal))
				{
					bool valid = TryParseLegacy(Text, Receipts, out Fault);
					MigratedLegacy = valid;
					return valid;
				}
				if (!(Text == Header || Text.StartsWith(Header + "\n", StringComparison.Ordinal)))
				{
					Fault = Text.StartsWith("taf-chronicle|", StringComparison.Ordinal)
						? KingdomChronicleRegistryFault.UnknownVersion
						: KingdomChronicleRegistryFault.MalformedHeader;
					return false;
				}
				return TryParseV3(Text, Receipts, out Fault);
			}
			catch
			{
				Receipts.Clear();
				MigratedLegacy = false;
				Fault = KingdomChronicleRegistryFault.MalformedRow;
				return false;
			}
		}

		private static bool TryParseV3(string Text, List<KingdomChronicleReceipt> Receipts,
			out KingdomChronicleRegistryFault Fault)
		{
			Fault = KingdomChronicleRegistryFault.None;
			int separators = Count(Text, '\n');
			if (separators > MaxReceipts)
			{
				Fault = KingdomChronicleRegistryFault.TooManyRows;
				return false;
			}
			string[] lines = Text.Split('\n');
			if (lines.Length != separators + 1 || lines[0] != Header)
			{
				Fault = KingdomChronicleRegistryFault.MalformedHeader;
				return false;
			}
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 1; i < lines.Length; i++)
			{
				KingdomChronicleReceipt receipt;
				if (!TryParseV3Row(lines[i], out receipt))
				{
					Fault = KingdomChronicleRegistryFault.MalformedRow;
					Receipts.Clear();
					return false;
				}
				if (!ids.Add(receipt.EventId))
				{
					Fault = KingdomChronicleRegistryFault.DuplicateIdentity;
					Receipts.Clear();
					return false;
				}
				Receipts.Add(receipt);
			}
			return true;
		}

		private static bool TryParseV3Row(string Line, out KingdomChronicleReceipt Receipt)
		{
			Receipt = null;
			if (string.IsNullOrEmpty(Line) || Line.Length > MaxActiveRowChars) return false;
			if (Line.StartsWith("a|", StringComparison.Ordinal))
			{
				if (Count(Line, '|') != 13) return false;
				string[] field = Line.Split('|');
				string id, official, outsider;
				KingdomChronicleSinkDisposition officialState, outsiderState, journalState;
				long updated;
				bool legacy;
				if (field.Length != 14 || !Decode(field[1], MaxEncodedIdChars,
						MaxEventIdChars, 1024, out id)
					|| !Decode(field[3], MaxEncodedEntryChars, MaxEntryChars, 24576,
						out official)
					|| !Decode(field[4], MaxEncodedEntryChars, MaxEntryChars, 24576,
						out outsider)
					|| !TryState(field[9], out officialState)
					|| !TryState(field[10], out outsiderState)
					|| !TryState(field[11], out journalState)
					|| !TryLong(field[12], out updated) || !TryBool(field[13], out legacy)) return false;
				Receipt = new KingdomChronicleReceipt
				{
					EventId = id,
					Fingerprint = field[2],
					Official = official,
					Outsider = outsider,
					OfficialBefore = field[5],
					OfficialAfter = field[6],
					OutsiderBefore = field[7],
					OutsiderAfter = field[8],
					OfficialState = officialState,
					OutsiderState = outsiderState,
					JournalState = journalState,
					Updated = updated,
					Compact = false,
					LegacyBlocked = legacy
				};
				return ReceiptValid(Receipt);
			}
			if (Line.StartsWith("tg|", StringComparison.Ordinal))
			{
				if (Line.Length > MaxTerminalRowChars || Count(Line, '|') != 7) return false;
				string[] field = Line.Split('|');
				string id;
				if (field.Length != 8 || !Decode(field[1], MaxEncodedIdChars,
					MaxEventIdChars, 1024, out id)) return false;
				return TryBuildTerminal(id, field[2], field[3], field[4], field[5],
					field[6], field[7], out Receipt);
			}
			if (Line.StartsWith("tc|", StringComparison.Ordinal))
			{
				if (Line.Length > MaxTerminalRowChars || Count(Line, '|') != 8) return false;
				string[] field = Line.Split('|');
				string coordinate;
				if (field.Length != 9 || !IsLowerHex(field[1], 32)
					|| !Decode(field[2], MaxEncodedIdChars, MaxConstructionCoordinateChars,
						1024, out coordinate)) return false;
				string id = "construction:" + field[1] + ":" + coordinate;
				string job;
				string exactCoordinate;
				if (!TryConstructionIdentity(id, out job, out exactCoordinate)
					|| job != field[1] || exactCoordinate != coordinate) return false;
				return TryBuildTerminal(id, field[3], field[4], field[5], field[6],
					field[7], field[8], out Receipt);
			}
			return false;
		}

		private static bool TryBuildTerminal(string Id, string Fingerprint,
			string OfficialStateText, string OutsiderStateText, string JournalStateText,
			string UpdatedText, string LegacyText, out KingdomChronicleReceipt Receipt)
		{
			Receipt = null;
			KingdomChronicleSinkDisposition official, outsider, journal;
			long updated;
			bool legacy;
			if (!TryState(OfficialStateText, out official)
				|| !TryState(OutsiderStateText, out outsider)
				|| !TryState(JournalStateText, out journal)
				|| !TryLong(UpdatedText, out updated) || !TryBool(LegacyText, out legacy)) return false;
			Receipt = new KingdomChronicleReceipt
			{
				EventId = Id,
				Fingerprint = Fingerprint == "-" ? null : Fingerprint,
				OfficialState = official,
				OutsiderState = outsider,
				JournalState = journal,
				Updated = updated,
				Compact = true,
				LegacyBlocked = legacy
			};
			return ReceiptValid(Receipt);
		}

		private static bool TryParseLegacy(string Text, List<KingdomChronicleReceipt> Receipts,
			out KingdomChronicleRegistryFault Fault)
		{
			Fault = KingdomChronicleRegistryFault.None;
			int separators = Count(Text, '\n');
			if (separators > LegacyMaxReceipts)
			{
				Fault = KingdomChronicleRegistryFault.TooManyRows;
				return false;
			}
			string[] lines = Text.Split('\n');
			if (lines.Length != separators + 1 || lines[0] != "v1")
			{
				Fault = KingdomChronicleRegistryFault.MalformedHeader;
				return false;
			}
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 1; i < lines.Length; i++)
			{
				string line = lines[i];
				if (string.IsNullOrEmpty(line) || line.Length > LegacyMaxRowChars
					|| Count(line, '|') != 9)
				{
					Fault = KingdomChronicleRegistryFault.MalformedRow;
					Receipts.Clear();
					return false;
				}
				string[] field = line.Split('|');
				string id, fingerprint, official, outsider, ob, oa, ub, ua;
				int phase;
				long updated;
				if (field.Length != 10
					|| !Decode(field[0], MaxEncodedIdChars, MaxEventIdChars, 1024, out id)
					|| !Decode(field[1], LegacyMaxHashEncodedChars, LegacyHashChars, 64,
						out fingerprint)
					|| !Decode(field[2], MaxEncodedEntryChars, MaxEntryChars, 24576,
						out official)
					|| !Decode(field[3], MaxEncodedEntryChars, MaxEntryChars, 24576,
						out outsider)
					|| !Decode(field[4], LegacyMaxHashEncodedChars, LegacyHashChars, 64, out ob)
					|| !Decode(field[5], LegacyMaxHashEncodedChars, LegacyHashChars, 64, out oa)
					|| !Decode(field[6], LegacyMaxHashEncodedChars, LegacyHashChars, 64, out ub)
					|| !Decode(field[7], LegacyMaxHashEncodedChars, LegacyHashChars, 64, out ua)
					|| !TryInt(field[8], out phase) || phase < 0 || phase > 31
					|| ((phase & 2) != 0 && (phase & 1) == 0)
					|| ((phase & 8) != 0 && (phase & 4) == 0)
					|| !TryLong(field[9], out updated) || string.IsNullOrEmpty(id)
					|| string.IsNullOrEmpty(official) || string.IsNullOrEmpty(outsider)
					|| !IsLowerHex(fingerprint, LegacyHashChars)
					|| !IsLowerHex(ob, LegacyHashChars) || !IsLowerHex(oa, LegacyHashChars)
					|| !IsLowerHex(ub, LegacyHashChars) || !IsLowerHex(ua, LegacyHashChars)
					|| !ids.Add(id))
				{
					Fault = KingdomChronicleRegistryFault.MalformedRow;
					Receipts.Clear();
					return false;
				}
				Receipts.Add(new KingdomChronicleReceipt
				{
					EventId = id,
					OfficialState = KingdomChronicleSinkDisposition.Lost,
					OutsiderState = KingdomChronicleSinkDisposition.Lost,
					JournalState = KingdomChronicleSinkDisposition.Lost,
					Updated = updated,
					Compact = true,
					LegacyBlocked = true
				});
			}
			return true;
		}

		public static bool TryWriteRegistry(IList<KingdomChronicleReceipt> Receipts,
			out string Text, out KingdomChronicleRegistryFault Fault)
		{
			Text = null;
			Fault = KingdomChronicleRegistryFault.None;
			if (Receipts == null || Receipts.Count > MaxReceipts)
			{
				Fault = KingdomChronicleRegistryFault.TooManyRows;
				return false;
			}
			try
			{
				StringBuilder result = new StringBuilder(Header);
				HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
				for (int i = 0; i < Receipts.Count; i++)
				{
					KingdomChronicleReceipt receipt = Receipts[i];
					if (!ReceiptValid(receipt) || !ids.Add(receipt.EventId))
					{
						Fault = KingdomChronicleRegistryFault.MalformedRow;
						return false;
					}
					string row = WriteRow(receipt);
					if (row == null || row.Length > MaxActiveRowChars)
					{
						Fault = KingdomChronicleRegistryFault.MalformedRow;
						return false;
					}
					if ((long)result.Length + row.Length + 1L > MaxRegistryChars)
					{
						Fault = KingdomChronicleRegistryFault.RegistryTooLong;
						return false;
					}
					result.Append('\n').Append(row);
				}
				Text = result.ToString();
				return true;
			}
			catch
			{
				Fault = KingdomChronicleRegistryFault.MalformedRow;
				Text = null;
				return false;
			}
		}

		private static string WriteRow(KingdomChronicleReceipt Receipt)
		{
			string fingerprint = Receipt.LegacyBlocked ? "-" : Receipt.Fingerprint;
			if (!Receipt.Compact)
			{
				return "a|" + Encode(Receipt.EventId) + "|" + fingerprint + "|"
					+ Encode(Receipt.Official) + "|" + Encode(Receipt.Outsider) + "|"
					+ Receipt.OfficialBefore + "|" + Receipt.OfficialAfter + "|"
					+ Receipt.OutsiderBefore + "|" + Receipt.OutsiderAfter + "|"
					+ ((int)Receipt.OfficialState).ToString(CultureInfo.InvariantCulture) + "|"
					+ ((int)Receipt.OutsiderState).ToString(CultureInfo.InvariantCulture) + "|"
					+ ((int)Receipt.JournalState).ToString(CultureInfo.InvariantCulture) + "|"
					+ Receipt.Updated.ToString(CultureInfo.InvariantCulture) + "|"
					+ (Receipt.LegacyBlocked ? "1" : "0");
			}
			string job;
			string coordinate;
			string tail = "|" + fingerprint + "|"
				+ ((int)Receipt.OfficialState).ToString(CultureInfo.InvariantCulture) + "|"
				+ ((int)Receipt.OutsiderState).ToString(CultureInfo.InvariantCulture) + "|"
				+ ((int)Receipt.JournalState).ToString(CultureInfo.InvariantCulture) + "|"
				+ Receipt.Updated.ToString(CultureInfo.InvariantCulture) + "|"
				+ (Receipt.LegacyBlocked ? "1" : "0");
			if (TryConstructionIdentity(Receipt.EventId, out job, out coordinate))
				return "tc|" + job + "|" + Encode(coordinate) + tail;
			return "tg|" + Encode(Receipt.EventId) + tail;
		}

		public static bool ReceiptValid(KingdomChronicleReceipt Receipt)
		{
			if (Receipt == null || string.IsNullOrEmpty(Receipt.EventId)
				|| Receipt.EventId.Length > MaxEventIdChars || !EncodedFits(Receipt.EventId,
					MaxEncodedIdChars) || !IsListDisposition(Receipt.OfficialState)
				|| !IsListDisposition(Receipt.OutsiderState)
				|| !IsJournalDisposition(Receipt.JournalState)
				|| Receipt.Updated < 0L) return false;
			if (Receipt.LegacyBlocked)
			{
				return Receipt.Compact && Receipt.Fingerprint == null
					&& Receipt.OfficialState == KingdomChronicleSinkDisposition.Lost
					&& Receipt.OutsiderState == KingdomChronicleSinkDisposition.Lost
					&& Receipt.JournalState == KingdomChronicleSinkDisposition.Lost
					&& PayloadEmpty(Receipt);
			}
			if (!IsSha256(Receipt.Fingerprint)) return false;
			if (Receipt.Compact)
				return IsTerminal(Receipt) && PayloadEmpty(Receipt);
			// A terminal active row is valid recovery input. The engine writer compacts it
			// before persistence, but accepting it keeps a manually interrupted/older v3
			// write from poisoning the whole exact registry.
			return !string.IsNullOrEmpty(Receipt.Official)
				&& Receipt.Official.Length <= MaxEntryChars
				&& EncodedFits(Receipt.Official, MaxEncodedEntryChars)
				&& !string.IsNullOrEmpty(Receipt.Outsider)
				&& Receipt.Outsider.Length <= MaxEntryChars
				&& EncodedFits(Receipt.Outsider, MaxEncodedEntryChars)
				&& IsSha256(Receipt.OfficialBefore) && IsSha256(Receipt.OfficialAfter)
				&& IsSha256(Receipt.OutsiderBefore) && IsSha256(Receipt.OutsiderAfter);
		}

		private static bool PayloadEmpty(KingdomChronicleReceipt Receipt)
		{
			return Receipt.Official == null && Receipt.Outsider == null
				&& Receipt.OfficialBefore == null && Receipt.OfficialAfter == null
				&& Receipt.OutsiderBefore == null && Receipt.OutsiderAfter == null;
		}

		private static string Encode(string Value)
		{
			return Convert.ToBase64String(StrictUtf8.GetBytes(Value ?? ""));
		}

		private static bool Decode(string Value, int MaxEncodedChars, int MaxDecodedChars,
			int MaxDecodedBytes, out string Result)
		{
			Result = null;
			if (Value == null || Value.Length > MaxEncodedChars || (Value.Length & 3) != 0)
				return false;
			try
			{
				byte[] bytes = Convert.FromBase64String(Value);
				if (bytes.Length > MaxDecodedBytes) return false;
				Result = StrictUtf8.GetString(bytes);
				return Result.Length <= MaxDecodedChars;
			}
			catch
			{
				Result = null;
				return false;
			}
		}

		private static bool EncodedFits(string Value, int Maximum)
		{
			if (Value == null) return false;
			try
			{
				long bytes = StrictUtf8.GetByteCount(Value);
				return ((bytes + 2L) / 3L) * 4L <= Maximum;
			}
			catch { return false; }
		}

		private static bool TryState(string Text, out KingdomChronicleSinkDisposition State)
		{
			State = KingdomChronicleSinkDisposition.None;
			int raw;
			if (!TryInt(Text, out raw) || raw < 0 || raw > 5) return false;
			State = (KingdomChronicleSinkDisposition)raw;
			return true;
		}

		private static bool TryBool(string Text, out bool Value)
		{
			Value = false;
			if (Text == "0") return true;
			if (Text == "1")
			{
				Value = true;
				return true;
			}
			return false;
		}

		private static bool TryInt(string Text, out int Value)
		{
			Value = 0;
			return !string.IsNullOrEmpty(Text) && Text.Length <= 10
				&& int.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value)
				&& Value >= 0 && Value.ToString(CultureInfo.InvariantCulture) == Text;
		}

		private static bool TryLong(string Text, out long Value)
		{
			Value = 0L;
			return !string.IsNullOrEmpty(Text) && Text.Length <= 19
				&& long.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value)
				&& Value >= 0L && Value.ToString(CultureInfo.InvariantCulture) == Text;
		}

		public static bool IsSha256(string Value)
		{
			return IsLowerHex(Value, Sha256HexChars);
		}

		private static bool IsLowerHex(string Value, int Length)
		{
			if (Value == null || Value.Length != Length) return false;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
			}
			return true;
		}

		private static int Count(string Text, char Character)
		{
			int count = 0;
			for (int i = 0; i < Text.Length; i++) if (Text[i] == Character) count++;
			return count;
		}
	}
}
