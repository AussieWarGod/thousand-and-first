using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeRules
	{
		public static string LegacySettlementId(string RealmId, string Name)
		{
			return CanonicalId("settlement", 0L, RealmId, Name);
		}

		public static string LegacyCharterId(string RealmId, string Deal, string Faction, int Row)
		{
			return CanonicalId("legacy-charter", Row + 1L, RealmId, Deal, Faction);
		}

		public static string LegacyManifestId(string RealmId, string Origin, string Destination,
			long LoadedTick)
		{
			return CanonicalId("legacy-manifest", LoadedTick, RealmId, Origin, Destination);
		}

		public static string CharterId(string RealmId, long Sequence)
		{
			return CanonicalId("charter", Sequence, RealmId);
		}

		public static string OperationId(string RealmId, long Sequence)
		{
			return CanonicalId("operation", Sequence, RealmId);
		}

		public static string ProjectionId(string OperationId)
		{
			return CanonicalId("projection", 0L, OperationId);
		}

		public static string ManifestId(string OperationId)
		{
			return CanonicalId("manifest", 0L, OperationId);
		}

		public static string MaterialMarker(string OperationId, int Kind)
		{
			return CanonicalId("material", Kind, OperationId);
		}

		private static string CanonicalId(string Lane, long Number, params string[] Fields)
		{
			try
			{
				if (string.IsNullOrEmpty(Lane) || Lane.Length > 64
					|| Fields == null || Fields.Length > 8) return null;
				using (MemoryStream canonical = new MemoryStream())
				{
					if (!WriteCanonicalField(canonical, IdentityNamespace)
						|| !WriteCanonicalField(canonical, Lane)) return null;
					WriteInt64(canonical, Number);
					WriteInt32(canonical, Fields.Length);
					for (int i = 0; i < Fields.Length; i++)
						if (!WriteCanonicalField(canonical, Fields[i] ?? "")) return null;
					byte[] digest;
					using (SHA256 sha = SHA256.Create())
					{
						if (sha == null) return null;
						digest = sha.ComputeHash(canonical.ToArray());
					}
					char[] hex = new char[digest.Length * 2];
					const string alphabet = "0123456789abcdef";
					for (int i = 0; i < digest.Length; i++)
					{
						hex[i * 2] = alphabet[digest[i] >> 4];
						hex[i * 2 + 1] = alphabet[digest[i] & 15];
					}
					return new string(hex);
				}
			}
			catch
			{
				return null;
			}
		}

		private static bool WriteCanonicalField(Stream Stream, string Value)
		{
			if (Stream == null || Value == null || Value.Length > MaxTextChars) return false;
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(Value);
			WriteInt32(Stream, bytes.Length);
			Stream.Write(bytes, 0, bytes.Length);
			return true;
		}

		private static bool WriteCanonicalNullableField(Stream Stream, string Value)
		{
			if (Stream == null) return false;
			if (Value == null)
			{
				WriteInt32(Stream, -1);
				return true;
			}
			return WriteCanonicalField(Stream, Value);
		}

		private static void WriteInt32(Stream Stream, int Value)
		{
			Stream.WriteByte((byte)(Value >> 24));
			Stream.WriteByte((byte)(Value >> 16));
			Stream.WriteByte((byte)(Value >> 8));
			Stream.WriteByte((byte)Value);
		}

		private static void WriteInt64(Stream Stream, long Value)
		{
			ulong bits = unchecked((ulong)Value);
			for (int shift = 56; shift >= 0; shift -= 8)
				Stream.WriteByte((byte)(bits >> shift));
		}

	}
}
