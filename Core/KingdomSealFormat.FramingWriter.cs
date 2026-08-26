using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomSealFormat
	{
		/// <summary>The founder-facing sentence for a refusal. One line, no jargon, no path.</summary>
		public static string RefusalLine(KingdomSealFault Fault)
		{
			switch (Fault)
			{
			case KingdomSealFault.None:
				return "";
			case KingdomSealFault.Empty:
				return "There is nothing written there.";
			case KingdomSealFault.NotASeal:
				return "That is not a sealed chronicle.";
			case KingdomSealFault.UnsupportedSchema:
				return "That chronicle was sealed by a different telling of this history, and cannot be read here.";
			case KingdomSealFault.TooLarge:
				return "That chronicle is longer than any realm ever was.";
			case KingdomSealFault.DigestUnavailable:
				return "The seal cannot be checked here, and an unchecked seal is not opened.";
			default:
				return "The seal is broken. What it held is not recoverable, and nothing half-read will be used.";
			}
		}

		private static bool ParseFraming(string Line, string Word, out string Rest)
		{
			Rest = null;
			if (Line == null || Line.Length <= Word.Length + 1)
			{
				return false;
			}
			if (!Line.StartsWith(Word, StringComparison.Ordinal) || Line[Word.Length] != ' ')
			{
				return false;
			}
			Rest = Line.Substring(Word.Length + 1);
			return Rest.Length > 0;
		}

		private static bool IsLowerHex(string Value, int Length)
		{
			if (Value == null || Value.Length != Length)
			{
				return false;
			}
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if ((c < '0' || c > '9') && (c < 'a' || c > 'f'))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// The digest of a payload, or false when the platform has no provider. A refusing provider
		/// is reported and never worked around: substituting another algorithm would silently make
		/// every seal ever written unreadable while looking like it worked.
		/// </summary>
		private static bool TryDigest(byte[] Bytes, out string Digest)
		{
			Digest = null;
			try
			{
				using (SHA256 provider = SHA256.Create())
				{
					if (provider == null)
					{
						return false;
					}
					byte[] hash = provider.ComputeHash(Bytes);
					StringBuilder sb = new StringBuilder(hash.Length * 2);
					for (int i = 0; i < hash.Length; i++)
					{
						sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
					}
					Digest = sb.ToString();
					return true;
				}
			}
			catch (CryptographicException)
			{
				return false;
			}
			catch (PlatformNotSupportedException)
			{
				return false;
			}
		}

		private static string WritePayload(KingdomSealBody Body)
		{
			StringBuilder sb = new StringBuilder(1024);
			sb.Append('{');
			IList<string> keys = Body.Keys;
			for (int i = 0; i < keys.Count; i++)
			{
				if (i > 0)
				{
					sb.Append(',');
				}
				string key = keys[i];
				WriteText(sb, key);
				sb.Append(':');
				switch (Body.KindOf(key))
				{
				case KingdomSealKind.Text:
					WriteText(sb, Body.Text(key));
					break;
				case KingdomSealKind.Number:
					sb.Append(Body.Number(key).ToString(CultureInfo.InvariantCulture));
					break;
				case KingdomSealKind.EmptyList:
					sb.Append("[]");
					break;
				case KingdomSealKind.TextList:
				{
					List<string> values = Body.TextList(key);
					sb.Append('[');
					for (int v = 0; v < values.Count; v++)
					{
						if (v > 0)
						{
							sb.Append(',');
						}
						WriteText(sb, values[v]);
					}
					sb.Append(']');
					break;
				}
				default:
				{
					List<long> numbers = Body.NumberList(key);
					sb.Append('[');
					for (int v = 0; v < numbers.Count; v++)
					{
						if (v > 0)
						{
							sb.Append(',');
						}
						sb.Append(numbers[v].ToString(CultureInfo.InvariantCulture));
					}
					sb.Append(']');
					break;
				}
				}
			}
			sb.Append('}');
			return sb.ToString();
		}

		/// <summary>
		/// Escapes exactly what the grammar allows back in: quote, backslash, and every character
		/// below space as a four-digit escape. Nothing else is escaped, so the canonical form of a
		/// given string is one string and a round trip is byte-identical.
		/// </summary>
		private static void WriteText(StringBuilder Sb, string Value)
		{
			Sb.Append('"');
			string value = Value ?? "";
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if (c == '"')
				{
					Sb.Append("\\\"");
				}
				else if (c == '\\')
				{
					Sb.Append("\\\\");
				}
				else if (c < ' ' || c == '\u007F')
				{
					Sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
				}
				else
				{
					Sb.Append(c);
				}
			}
			Sb.Append('"');
		}
	}
}
