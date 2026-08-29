using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	internal static class KingdomLabCivicOwnerRules
	{
		internal const int CurrentVersion = 1;
		internal const int MaxRows = 3;
		internal const int MaxStringBytes = 512;
		internal const int MaxWireBytes = 8192;
		private const string Prefix = "TAFLC1:";
		private static readonly byte[] Magic = new byte[] { 0x54, 0x41, 0x46, 0x4c, 0x43, 0x31 };

		internal static bool Valid(KingdomLabCivicOwnerBook Book)
		{
			if (Book == null || Book.Version != CurrentVersion || Book.Rows == null
				|| Book.Rows.Count > MaxRows) return false;
			string prior = null;
			for (int i = 0; i < Book.Rows.Count; i++)
			{
				KingdomLabCivicOwnerRow row = Book.Rows[i];
				if (!Text(row?.RealmId) || !Text(row.SettlementId) || !Text(row.ZoneId)
					|| !Text(row.OwnerObjectId) || prior != null
					&& string.CompareOrdinal(prior, row.SettlementId) >= 0) return false;
				prior = row.SettlementId;
			}
			return true;
		}

		internal static KingdomLabCivicOwnerRow Find(KingdomLabCivicOwnerBook Book,
			string SettlementId)
		{
			if (!Valid(Book) || string.IsNullOrEmpty(SettlementId)) return null;
			for (int i = 0; i < Book.Rows.Count; i++)
				if (Book.Rows[i].SettlementId == SettlementId) return Book.Rows[i];
			return null;
		}

		internal static bool TryClaim(KingdomLabCivicOwnerBook Before,
			KingdomLabCivicOwnerRow Claim, out KingdomLabCivicOwnerBook After)
		{
			After = null;
			if (!Valid(Before) || Claim == null || !Text(Claim.RealmId)
				|| !Text(Claim.SettlementId) || !Text(Claim.ZoneId)
				|| !Text(Claim.OwnerObjectId)) return false;
			KingdomLabCivicOwnerRow held = Find(Before, Claim.SettlementId);
			if (held != null)
			{
				if (!Same(held, Claim)) return false;
				After = Before.Copy();
				return true;
			}
			if (Before.Rows.Count >= MaxRows) return false;
			After = Before.Copy();
			After.Rows.Add(Claim.Copy());
			After.Rows.Sort((a, b) => string.CompareOrdinal(a.SettlementId, b.SettlementId));
			return Valid(After);
		}

		internal static bool TryRelease(KingdomLabCivicOwnerBook Before,
			KingdomLabCivicOwnerRow Expected, out KingdomLabCivicOwnerBook After)
		{
			After = null;
			if (!Valid(Before) || Expected == null) return false;
			KingdomLabCivicOwnerRow held = Find(Before, Expected.SettlementId);
			if (held == null || !Same(held, Expected)) return false;
			After = Before.Copy();
			for (int i = 0; i < After.Rows.Count; i++)
				if (After.Rows[i].SettlementId == Expected.SettlementId)
				{ After.Rows.RemoveAt(i); break; }
			return Valid(After);
		}

		internal static string Encode(KingdomLabCivicOwnerBook Book)
		{
			if (!Valid(Book)) return null;
			try
			{
				byte[] payload;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream,
					new UTF8Encoding(false, true), true))
				{
					writer.Write(Magic); writer.Write(Book.Version); writer.Write(Book.Rows.Count);
					for (int i = 0; i < Book.Rows.Count; i++)
					{
						Write(writer, Book.Rows[i].RealmId); Write(writer, Book.Rows[i].SettlementId);
						Write(writer, Book.Rows[i].ZoneId); Write(writer, Book.Rows[i].OwnerObjectId);
					}
					writer.Flush(); payload = stream.ToArray();
				}
				using (SHA256 sha = SHA256.Create())
				{
					byte[] digest = sha.ComputeHash(payload);
					byte[] wire = new byte[payload.Length + digest.Length];
					Buffer.BlockCopy(payload, 0, wire, 0, payload.Length);
					Buffer.BlockCopy(digest, 0, wire, payload.Length, digest.Length);
					return wire.Length <= MaxWireBytes ? Prefix + Convert.ToBase64String(wire) : null;
				}
			}
			catch { return null; }
		}

		internal static bool TryDecode(string Wire, out KingdomLabCivicOwnerBook Book)
		{
			Book = null;
			if (string.IsNullOrEmpty(Wire)) { Book = new KingdomLabCivicOwnerBook(); return true; }
			if (!Wire.StartsWith(Prefix, StringComparison.Ordinal)) return false;
			try
			{
				byte[] wire = Convert.FromBase64String(Wire.Substring(Prefix.Length));
				if (wire.Length < Magic.Length + 8 + 32 || wire.Length > MaxWireBytes) return false;
				int payloadLength = wire.Length - 32;
				using (SHA256 sha = SHA256.Create())
				{
					byte[] digest = sha.ComputeHash(wire, 0, payloadLength);
					for (int i = 0; i < digest.Length; i++)
						if (digest[i] != wire[payloadLength + i]) return false;
				}
				using (MemoryStream stream = new MemoryStream(wire, 0, payloadLength, false))
				using (BinaryReader reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
				{
					for (int i = 0; i < Magic.Length; i++) if (reader.ReadByte() != Magic[i]) return false;
					KingdomLabCivicOwnerBook parsed = new KingdomLabCivicOwnerBook
						{ Version = reader.ReadInt32(), Rows = new List<KingdomLabCivicOwnerRow>() };
					int count = reader.ReadInt32();
					if (count < 0 || count > MaxRows) return false;
					for (int i = 0; i < count; i++) parsed.Rows.Add(new KingdomLabCivicOwnerRow
						{ RealmId = Read(reader), SettlementId = Read(reader),
							ZoneId = Read(reader), OwnerObjectId = Read(reader) });
					if (stream.Position != stream.Length || !Valid(parsed)
						|| !string.Equals(Encode(parsed), Wire, StringComparison.Ordinal)) return false;
					Book = parsed; return true;
				}
			}
			catch { return false; }
		}

		internal static bool Same(KingdomLabCivicOwnerRow A, KingdomLabCivicOwnerRow B)
		{
			return A != null && B != null && A.RealmId == B.RealmId
				&& A.SettlementId == B.SettlementId && A.ZoneId == B.ZoneId
				&& A.OwnerObjectId == B.OwnerObjectId;
		}

		private static bool Text(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Trim().Length != Value.Length) return false;
			return new UTF8Encoding(false, true).GetByteCount(Value) <= MaxStringBytes;
		}

		private static void Write(BinaryWriter Writer, string Value)
		{
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(Value);
			Writer.Write(bytes.Length); Writer.Write(bytes);
		}

		private static string Read(BinaryReader Reader)
		{
			int count = Reader.ReadInt32();
			if (count <= 0 || count > MaxStringBytes || Reader.BaseStream.Length
				- Reader.BaseStream.Position < count) throw new InvalidDataException();
			return new UTF8Encoding(false, true).GetString(Reader.ReadBytes(count));
		}
	}
}
