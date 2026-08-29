using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Canonical, engine-free evidence digests for C2 receipts and the base fence.</summary>
	public static class KingdomRetirementDigestRules
	{
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
		private static readonly char[] Hex = "0123456789abcdef".ToCharArray();

		public static string Realm(string GameId, string RealmId, long Incarnation,
			string FactionId, string FoundingTransaction, long FoundedTick,
			IList<string> SettlementIds)
		{
			return Hash("realm-authority-v1", delegate(BinaryWriter writer)
			{
				S(writer, GameId); S(writer, RealmId); writer.Write(Incarnation);
				S(writer, FactionId); S(writer, FoundingTransaction);
				writer.Write(FoundedTick); Sorted(writer, SettlementIds);
			});
		}

		public static string RetirementAuthority(string RealmDigest,
			IList<KingdomRemovalLocator> Locators)
		{
			return Hash("retirement-authority-v1", delegate(BinaryWriter writer)
			{
				S(writer, RealmDigest); writer.Write(Locators?.Count ?? -1);
				for (int i = 0; i < (Locators?.Count ?? 0); i++)
				{
					S(writer, Locators[i]?.ZoneId);
					S(writer, Locators[i]?.SettlementId);
				}
			});
		}

		public static string Evidence(string Domain, IList<string> Rows)
		{
			return Hash("retirement-evidence-v1", delegate(BinaryWriter writer)
			{
				S(writer, Domain); Sorted(writer, Rows);
			});
		}

		public static string Tombstone(string PredecessorWireDigest,
			KingdomRealmRetirementState State, string RealmDigest)
		{
			return Hash("realm-tombstone-v2", delegate(BinaryWriter writer)
			{
				S(writer, PredecessorWireDigest); S(writer, RealmDigest);
				S(writer, KingdomIdentityFenceReceiptRules.PreparedReceiptBinding(State));
			});
		}

		private static void Sorted(BinaryWriter Writer, IList<string> Rows)
		{
			if (Rows == null) { Writer.Write(-1); return; }
			List<string> copy = new List<string>(Rows);
			copy.Sort(StringComparer.Ordinal);
			Writer.Write(copy.Count);
			for (int i = 0; i < copy.Count; i++) S(Writer, copy[i]);
		}

		private static string Hash(string Domain, Action<BinaryWriter> Write)
		{
			byte[] bytes;
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, Utf8))
			{
				S(writer, Domain); Write(writer); writer.Flush(); bytes = stream.ToArray();
			}
			byte[] digest;
			using (SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(bytes);
			char[] encoded = new char[digest.Length * 2];
			for (int i = 0; i < digest.Length; i++)
			{
				encoded[i * 2] = Hex[digest[i] >> 4];
				encoded[i * 2 + 1] = Hex[digest[i] & 15];
			}
			return new string(encoded);
		}

		private static void S(BinaryWriter Writer, string Value)
		{
			if (Value == null) { Writer.Write(-1); return; }
			byte[] bytes = Utf8.GetBytes(Value);
			Writer.Write(bytes.Length); Writer.Write(bytes);
		}
	}
}
