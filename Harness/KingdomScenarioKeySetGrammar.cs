using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The key-set digest grammar. Split from the anchor law only to hold the house line cap.
	/// <para>
	/// This decides whether a scenario matched ordinary play, so it has to be injective over live
	/// measured values. The previous grammar joined key and value with two control separators and
	/// never proved the value excluded them, which is the same collision the realized-capture
	/// grammar removed one authority over.
	/// </para>
	/// </summary>
	internal static partial class KingdomScenarioAnchorRules
	{
		/// <summary>Bounded so one hostile measured value cannot make the canonical text huge.</summary>
		internal const int MaxFieldChars = 512;

		private const string Grammar = "ks1";

		/// <summary>
		/// One self-delimiting field: its exact character count, a colon, then the value.
		/// <para>
		/// The previous grammar joined key and value with two control separators and never proved
		/// the value excluded them, so a measured property carrying a separator could imitate a
		/// different key/value sequence and two different captures could digest alike. Control
		/// values and unpaired surrogates are refused outright: the default UTF-8 encoder maps a
		/// lone surrogate to U+FFFD, which folds distinct captures onto identical bytes.
		/// </para>
		/// </summary>
		private static string Field(string Value)
		{
			if (Value == null || Value.Length > MaxFieldChars) return null;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (c < ' ' || (c >= (char)0x7F && c <= (char)0x9F)) return null;
				if (char.IsLowSurrogate(c)) return null;
				if (char.IsHighSurrogate(c))
				{
					if (i + 1 >= Value.Length || !char.IsLowSurrogate(Value[i + 1])) return null;
					i++;
				}
			}
			return Value.Length.ToString(CultureInfo.InvariantCulture) + ":" + Value;
		}

		/// <summary>Strict UTF-8: throwing rather than substituting, so nothing folds silently.</summary>
		private static string Sha256(string Value)
		{
			byte[] bytes;
			try
			{
				bytes = new UTF8Encoding(false, true).GetBytes(Value);
			}
			catch (EncoderFallbackException)
			{
				return null;
			}
			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(bytes);
				StringBuilder text = new StringBuilder(hash.Length * 2);
				for (int i = 0; i < hash.Length; i++)
					text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
				return text.ToString();
			}
		}
	}
}
