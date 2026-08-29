using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		private static readonly char[] ActivationHex = "0123456789abcdef".ToCharArray();

		internal static string ActivationDigest(string Domain, params string[] Values)
		{
			if (string.IsNullOrEmpty(Domain))
				throw new ArgumentException("A polity hash domain is required.", nameof(Domain));
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
			{
				WriteHashText(writer, Domain);
				writer.Write(Values == null ? 0 : Values.Length);
				for (int i = 0; Values != null && i < Values.Length; i++)
					WriteHashText(writer, Values[i] ?? "");
				writer.Flush();
				using (SHA256 sha = SHA256.Create())
				{
					if (sha == null) throw new CryptographicException("SHA-256 is unavailable.");
					return ActivationHexString(sha.ComputeHash(stream.ToArray()));
				}
			}
		}

		internal static string ActivationDigest(string Domain, IList<string> Values)
		{
			string[] copy = Values == null ? new string[0] : new string[Values.Count];
			for (int i = 0; i < copy.Length; i++) copy[i] = Values[i] ?? "";
			return ActivationDigest(Domain, copy);
		}

		internal static string ActivationId(string Prefix, string Domain, params string[] Values)
		{
			string id = Prefix + ActivationDigest(Domain, Values);
			if (!SemanticId(id)) throw new InvalidOperationException("Generated polity id is invalid.");
			return id;
		}

		private static void WriteHashText(BinaryWriter Writer, string Value)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(Value ?? "");
			Writer.Write(bytes.Length); Writer.Write(bytes, 0, bytes.Length);
		}

		private static string ActivationHexString(byte[] Bytes)
		{
			if (Bytes == null) throw new ArgumentNullException(nameof(Bytes));
			char[] chars = new char[Bytes.Length * 2];
			for (int i = 0; i < Bytes.Length; i++)
			{
				chars[i * 2] = ActivationHex[Bytes[i] >> 4];
				chars[i * 2 + 1] = ActivationHex[Bytes[i] & 15];
			}
			return new string(chars);
		}
	}
}
