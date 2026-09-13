using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomCampHeartSaveSnapshotCodec
	{
		internal const string Prefix = "taf-camp-heart-save-v1:";
		internal const int MaxWireChars = 32768;
		private const int Magic = 0x31484354;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static bool Valid(KingdomCampHeartSaveSnapshot Value)
		{
			if (Value == null || !Guid.TryParseExact(Value.GameId, "D", out var id)
				|| id.ToString("D") != Value.GameId || Value.Turns < 0 || Value.Water < 0) return false;
			foreach (string text in Fields(Value)) if (!Text(text)) return false;
			foreach (int coordinate in Coordinates(Value)) if (coordinate < 0 || coordinate >= 4096) return false;
			var objects = new HashSet<string>(StringComparer.Ordinal)
				{ Value.HeartId, Value.StoreId, Value.FireId, Value.TimberId };
			return objects.Count == 4 && Value.TentJobId != Value.UpgradeJobId && Digest(Value.ContentsDigest) && Digest(Value.BrushDigest);
		}

		internal static bool TryEncode(KingdomCampHeartSaveSnapshot Value, out string Wire)
		{
			Wire = null;
			if (!Valid(Value)) return false;
			try
			{
				using (var stream = new MemoryStream())
				using (var writer = new BinaryWriter(stream, Utf8, true))
				{
					writer.Write(Magic); writer.Write(1);
					foreach (string field in Fields(Value)) Write(writer, field);
					foreach (int coordinate in Coordinates(Value)) writer.Write(coordinate);
					writer.Write(Value.Water); writer.Write(Value.Turns);
					string result = Prefix + Convert.ToBase64String(stream.ToArray());
					if (result.Length > MaxWireChars) return false;
					Wire = result;
					return true;
				}
			}
			catch (Exception) { return false; }
		}

		internal static bool TryDecode(string Wire, out KingdomCampHeartSaveSnapshot Value)
		{
			Value = null;
			if (Wire == null || Wire.Length > MaxWireChars || !Wire.StartsWith(Prefix, StringComparison.Ordinal)) return false;
			try
			{
				byte[] bytes = Convert.FromBase64String(Wire.Substring(Prefix.Length));
				if (Prefix + Convert.ToBase64String(bytes) != Wire) return false;
				using (var stream = new MemoryStream(bytes, false))
				using (var reader = new BinaryReader(stream, Utf8))
				{
					if (reader.ReadInt32() != Magic || reader.ReadInt32() != 1) return false;
					var fields = new string[12];
					for (int i = 0; i < fields.Length; i++) fields[i] = Read(reader);
					var coordinates = new int[6];
					for (int i = 0; i < coordinates.Length; i++) coordinates[i] = reader.ReadInt32();
					var result = new KingdomCampHeartSaveSnapshot(fields[0], fields[1], fields[2], fields[3],
						fields[4], fields[5], fields[6], fields[7], fields[8], fields[9], fields[10], fields[11],
						coordinates[0], coordinates[1], coordinates[2], coordinates[3], coordinates[4],
						coordinates[5], reader.ReadInt32(), reader.ReadInt64());
					if (stream.Position != stream.Length || !Valid(result)) return false;
					Value = result;
					return true;
				}
			}
			catch (Exception) { return false; }
		}

		internal static string CustodyDigest(IReadOnlyList<KingdomCampHeartNativeCensus.Unit> Units)
		{
			if (Units == null || Units.Count > 48) throw new InvalidDataException("camp custody is unbounded");
			var sorted = new SortedDictionary<string, KingdomCampHeartNativeCensus.Unit>(StringComparer.Ordinal);
			foreach (var unit in Units)
			{
				if (unit == null || !Text(unit.Id) || !Text(unit.Blueprint) || !Text(unit.Holder)
					|| unit.RawCount <= 0 || sorted.ContainsKey(unit.Id))
					throw new InvalidDataException("camp custody contains an invalid or duplicate unit");
				sorted.Add(unit.Id, unit);
			}
			using (var stream = new MemoryStream())
			using (var writer = new BinaryWriter(stream, Utf8, true))
			using (var sha = SHA256.Create())
			{
				writer.Write(sorted.Count);
				foreach (var pair in sorted)
				{
					Write(writer, pair.Value.Id); Write(writer, pair.Value.Blueprint);
					Write(writer, pair.Value.Holder); writer.Write(pair.Value.RawCount);
				}
				return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
			}
		}

		private static string[] Fields(KingdomCampHeartSaveSnapshot Value) => new[]
		{
			Value.GameId, Value.RealmId, Value.CityId, Value.ZoneId, Value.HeartId, Value.UpgradeJobId,
			Value.StoreId, Value.FireId, Value.TimberId, Value.ContentsDigest, Value.BrushDigest, Value.TentJobId
		};
		private static int[] Coordinates(KingdomCampHeartSaveSnapshot Value) => new[]
			{ Value.HeartX, Value.HeartY, Value.StoreX, Value.StoreY, Value.FireX, Value.FireY };
		private static bool Text(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > 1024) return false;
			foreach (char letter in Value) if (char.IsControl(letter)) return false;
			try { return Utf8.GetByteCount(Value) <= 4096; }
			catch (EncoderFallbackException) { return false; }
		}
		private static bool Digest(string Value)
		{
			if (Value.Length != 64) return false;
			foreach (char value in Value)
				if (!(value >= '0' && value <= '9') && !(value >= 'a' && value <= 'f')) return false;
			return true;
		}
		private static void Write(BinaryWriter Writer, string Value)
		{
			byte[] bytes = Utf8.GetBytes(Value);
			Writer.Write(bytes.Length); Writer.Write(bytes);
		}
		private static string Read(BinaryReader Reader)
		{
			int length = Reader.ReadInt32();
			if (length < 1 || length > 4096 || length > Reader.BaseStream.Length - Reader.BaseStream.Position)
				throw new InvalidDataException("invalid camp snapshot field length");
			string value = Utf8.GetString(Reader.ReadBytes(length));
			if (!Text(value)) throw new InvalidDataException("invalid camp snapshot field");
			return value;
		}
	}
}
