using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst.Harness
{
	/// <summary>Canonical, retained rows behind each higher-heart snapshot digest.</summary>
	internal static class KingdomCampHeartChainFacts
	{
		internal const int MaxRows = 4096, MaxFields = 64, MaxFieldChars = 524288, MaxBytes = 4194304;
		internal const string Prefix = "taf-heart-chain-facts-v1:";
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static bool TryCapture(string Domain, IReadOnlyList<string[]> Rows,
			out string Wire, out string Digest)
		{
			Wire = null; Digest = null;
			if (string.IsNullOrEmpty(Domain) || Domain.Length > 64 || Rows == null
				|| Rows.Count > MaxRows) return false;
			try
			{
				var sorted = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
				long bytes = 8 + Size(Domain);
				foreach (var row in Rows)
				{
					if (row == null || row.Length == 0 || row.Length > MaxFields
						|| string.IsNullOrEmpty(row[0]) || row[0].Length > 1024
						|| sorted.ContainsKey(row[0])) return false;
					bytes += 4;
					foreach (string field in row) bytes += Size(field);
					if (bytes > MaxBytes) return false;
					sorted.Add(row[0], row);
				}
				using (var stream = new MemoryStream())
				using (var writer = new BinaryWriter(stream, Utf8, true))
				using (var sha = SHA256.Create())
				{
					writer.Write(1); Write(writer, Domain); writer.Write(sorted.Count);
					foreach (var row in sorted.Values)
					{
						writer.Write(row.Length);
						foreach (string field in row) Write(writer, field);
					}
					writer.Flush();
					byte[] payload = stream.ToArray();
					if (payload.Length != bytes || payload.Length > MaxBytes) return false;
					Wire = Prefix + Convert.ToBase64String(payload);
					Digest = BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(Wire)))
						.Replace("-", "").ToLowerInvariant();
					return true;
				}
			}
			catch (Exception) { Wire = null; Digest = null; return false; }
		}

		private static int Size(string Value)
		{
			if (Value == null) return 4;
			if (Value.Length > MaxFieldChars) throw new InvalidDataException("heart fact field too large");
			foreach (char letter in Value)
				if (char.IsControl(letter)) throw new InvalidDataException("heart fact contains a control character");
			return checked(4 + Utf8.GetByteCount(Value));
		}

		private static void Write(BinaryWriter Writer, string Value)
		{
			if (Value == null) { Writer.Write(-1); return; }
			byte[] bytes = Utf8.GetBytes(Value); Writer.Write(bytes.Length); Writer.Write(bytes);
		}
	}
}
