using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Bounded canonical encoding of the higher-heart evidence record. Refuses aliases, torn
	/// records and unknown versions; never falls back to the rung-two witness or fills defaults.
	/// This validates evidence syntax, not a game save or native behavioral acceptance.
	/// </summary>
	internal static class KingdomCampHeartChainSnapshotCodec
	{
		internal const string Prefix = "taf-camp-heart-chain-save-v1:";
		internal const int MaxWireChars = 32768;
		private const int Magic = 0x31434354;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static bool Valid(KingdomCampHeartChainSnapshot Value)
		{
			if (Value == null || !Guid.TryParseExact(Value.GameId, "D", out var id)
				|| id.ToString("D") != Value.GameId || (Value.Rung != 3 && Value.Rung != 4)
				|| Value.Population <= 0 || Value.Water < 0 || Value.Food < 0
				|| Value.Turns < 0 || Value.TimeTicks < 0) return false;
			foreach (string field in Fields(Value)) if (!Text(field)) return false;
			foreach (string digest in new[] { Value.JobsDigest, Value.ResidentsDigest,
				Value.SupportDigest, Value.CustodyDigest }) if (!Digest(digest)) return false;
			var ids = new HashSet<string>(StringComparer.Ordinal) { Value.ResidentId };
			foreach (var anchor in Anchors(Value))
				if (anchor == null || !Text(anchor.Id) || !ids.Add(anchor.Id)
					|| anchor.X < 0 || anchor.X >= 4096 || anchor.Y < 0 || anchor.Y >= 4096) return false;
			return true;
		}

		internal static bool TryEncode(KingdomCampHeartChainSnapshot Value, out string Wire)
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
					foreach (var anchor in Anchors(Value))
					{ Write(writer, anchor.Id); writer.Write(anchor.X); writer.Write(anchor.Y); }
					writer.Write(Value.Rung); writer.Write(Value.Population);
					writer.Write(Value.Water); writer.Write(Value.Food);
					writer.Write(Value.Turns); writer.Write(Value.TimeTicks);
					string wire = Prefix + Convert.ToBase64String(stream.ToArray());
					if (wire.Length > MaxWireChars) return false;
					Wire = wire; return true;
				}
			}
			catch (Exception) { return false; }
		}

		internal static bool TryDecode(string Wire, out KingdomCampHeartChainSnapshot Value)
		{
			Value = null;
			if (Wire == null || Wire.Length > MaxWireChars
				|| !Wire.StartsWith(Prefix, StringComparison.Ordinal)) return false;
			try
			{
				byte[] bytes = Convert.FromBase64String(Wire.Substring(Prefix.Length));
				if (Prefix + Convert.ToBase64String(bytes) != Wire) return false;
				using (var stream = new MemoryStream(bytes, false))
				using (var reader = new BinaryReader(stream, Utf8))
				{
					if (reader.ReadInt32() != Magic || reader.ReadInt32() != 1) return false;
					var fields = new string[10];
					for (int i = 0; i < fields.Length; i++) fields[i] = Read(reader);
					var anchors = new KingdomCampHeartChainAnchor[4];
					for (int i = 0; i < anchors.Length; i++)
						anchors[i] = new KingdomCampHeartChainAnchor(Read(reader), reader.ReadInt32(), reader.ReadInt32());
					var result = new KingdomCampHeartChainSnapshot(fields[0], fields[1], fields[2], fields[3],
						reader.ReadInt32(), anchors[0], anchors[1], anchors[2], anchors[3], fields[4], fields[5],
						fields[6], fields[7], fields[8], fields[9], reader.ReadInt32(), reader.ReadInt32(),
						reader.ReadInt32(), reader.ReadInt64(), reader.ReadInt64());
					if (stream.Position != stream.Length || !Valid(result)) return false;
					Value = result; return true;
				}
			}
			catch (Exception) { return false; }
		}

		private static string[] Fields(KingdomCampHeartChainSnapshot Value) => new[]
		{
			Value.GameId, Value.RealmId, Value.CityId, Value.ZoneId, Value.ResidentId, Value.JobId,
			Value.JobsDigest, Value.ResidentsDigest, Value.SupportDigest, Value.CustodyDigest
		};
		private static KingdomCampHeartChainAnchor[] Anchors(KingdomCampHeartChainSnapshot Value)
			=> new[] { Value.Heart, Value.Basin, Value.Store, Value.Track };
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
			foreach (char letter in Value)
				if (!(letter >= '0' && letter <= '9') && !(letter >= 'a' && letter <= 'f')) return false;
			return true;
		}
		private static void Write(BinaryWriter Writer, string Value)
		{
			byte[] bytes = Utf8.GetBytes(Value); Writer.Write(bytes.Length); Writer.Write(bytes);
		}
		private static string Read(BinaryReader Reader)
		{
			int length = Reader.ReadInt32();
			if (length < 1 || length > 4096 || length > Reader.BaseStream.Length - Reader.BaseStream.Position)
				throw new InvalidDataException("invalid higher-heart snapshot field length");
			string value = Utf8.GetString(Reader.ReadBytes(length));
			if (!Text(value)) throw new InvalidDataException("invalid higher-heart snapshot field");
			return value;
		}
	}
}
