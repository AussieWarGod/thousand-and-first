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
	public static partial class KingdomChronicleReceiptRules
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
			// Slot zero is the constitutional root of the register: on a current realm it is the
			// first-water founding entry. Keep that one named milestone while ordinary later news
			// rotates. Legacy lists without a typed pin still preserve their oldest surviving row,
			// which is safer than silently inventing which historical entry was constitutional.
			if (Values.Count > MaxEntries) Values.RemoveAt(Values.Count > 1 ? 1 : 0);
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

	}
}
