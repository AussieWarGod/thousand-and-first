using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// The file a sealed realm is written as, and the only reader for it.
	/// <para>
	/// <b>Framing before parsing.</b> A seal is three plain lines and then one line of payload:
	/// the format's name and schema, the payload's digest, the payload's byte length. Integrity is
	/// settled before a single character of the payload is interpreted, so a truncated,
	/// half-written, or edited file is refused as a torn seal rather than half-understood as a
	/// kingdom. <c>INHERITANCE-SEAMS.md</c> requires complete-or-absent; this is where that is
	/// enforced.
	/// </para>
	/// <para>
	/// <b>A strict subset of JSON, and never a JSON library.</b> The payload is one flat object
	/// whose values are text, whole numbers, or lists of those. No nesting, no floating point, no
	/// null, no boolean, no duplicate key, no unknown escape, no control character. That is the
	/// whole grammar, and everything outside it is a refusal rather than a best guess &mdash;
	/// which is what makes this safe to point at a file the player, the cloud, or another program
	/// could have touched. The envelope contract in <c>INHERITANCE-SEAMS.md:120-149</c> asks for a
	/// strict JSON DTO of bounded primitives and semantic ids; this is that, with the generality
	/// that would make it dangerous removed rather than merely unused.
	/// </para>
	/// <para>
	/// The payload is written as a single line on purpose. Every newline inside a value is escaped,
	/// so nothing but the three framing lines is sensitive to how a file transfer treats line
	/// endings, and a stray carriage return cannot cost a founder their realm.
	/// </para>
	/// </summary>
	internal static partial class KingdomSealFormat
	{
		/// <summary>The first token of every seal file. Not a path, not a type name: a word.</summary>
		public const string FormatName = "taf-seal";

		/// <summary>Nothing honest is bigger. A settlement is forty works and sixty people.</summary>
		public const int MaxFileChars = 262144;

		internal const int MaxPayloadBytes = 262000;

		internal const int MaxFramingLineChars = 128;

		internal const int MaxKeys = 96;

		internal const int MaxKeyChars = 64;

		internal const int MaxValueChars = KingdomArchitectureRules.MaxSnapshotChars;

		internal const int MaxArrayItems = 128;

		private const string DigestPrefix = "sha256";

		private const string LengthPrefix = "length";

		/// <summary>
		/// Writes a seal file: framing, then the canonical payload.
		/// </summary>
		/// <param name="Schema">The schema version this payload is written to.</param>
		/// <param name="Body">The payload. Written in its own key order.</param>
		/// <returns>The whole file's text, ending in a newline.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="Body"/> is null.</exception>
		/// <exception cref="InvalidOperationException">No digest provider; nothing is written.</exception>
		public static string Compose(int Schema, KingdomSealBody Body)
		{
			if (Body == null)
			{
				throw new ArgumentNullException("Body");
			}
			string payload = WritePayload(Body);
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(payload);
			if (bytes.Length > MaxPayloadBytes)
			{
				throw new InvalidOperationException("A seal payload may not exceed " + MaxPayloadBytes + " bytes.");
			}
			string digest;
			if (!TryDigest(bytes, out digest))
			{
				throw new InvalidOperationException("No SHA-256 provider is available; a seal cannot be written without one.");
			}
			StringBuilder sb = new StringBuilder(payload.Length + 128);
			sb.Append(FormatName).Append(' ').Append(Schema.ToString(CultureInfo.InvariantCulture)).Append('\n');
			sb.Append(DigestPrefix).Append(' ').Append(digest).Append('\n');
			sb.Append(LengthPrefix).Append(' ').Append(bytes.Length.ToString(CultureInfo.InvariantCulture)).Append('\n');
			sb.Append(payload).Append('\n');
			return sb.ToString();
		}

		/// <summary>
		/// Reads a seal file, refusing anything it cannot vouch for whole.
		/// <para>
		/// Side effects: none. Failure mode: returns false with <paramref name="Fault"/> set and
		/// <paramref name="Body"/> null &mdash; never a partially populated payload, and never an
		/// exception for ordinary bad input.
		/// </para>
		/// </summary>
		/// <param name="FileText">The file's whole text.</param>
		/// <param name="MinSchema">Oldest schema this build reads.</param>
		/// <param name="MaxSchema">Newest schema this build reads.</param>
		/// <param name="Schema">The schema the file declared, on success.</param>
		/// <param name="Body">The parsed payload, on success.</param>
		/// <param name="Fault">Why it was refused, on failure.</param>
		/// <param name="Detail">A line naming the refusal for the log; never null.</param>
		public static bool TryParse(string FileText, int MinSchema, int MaxSchema, out int Schema, out KingdomSealBody Body, out KingdomSealFault Fault, out string Detail)
		{
			try
			{
				return TryParseCore(FileText, MinSchema, MaxSchema, out Schema, out Body, out Fault, out Detail);
			}
			catch (Exception)
			{
				Schema = 0;
				Body = null;
				Fault = KingdomSealFault.Malformed;
				Detail = "the seal's record is malformed";
				return false;
			}
		}

		private static bool TryParseCore(string FileText, int MinSchema, int MaxSchema, out int Schema, out KingdomSealBody Body, out KingdomSealFault Fault, out string Detail)
		{
			Schema = 0;
			Body = null;
			Fault = KingdomSealFault.None;
			Detail = "";
			if (string.IsNullOrEmpty(FileText))
			{
				Fault = KingdomSealFault.Empty;
				Detail = "the file is empty";
				return false;
			}
			if (FileText.Length > MaxFileChars)
			{
				Fault = KingdomSealFault.TooLarge;
				Detail = "the file is " + FileText.Length + " characters; no seal is larger than " + MaxFileChars;
				return false;
			}
			if (FileText.Trim().Length == 0)
			{
				Fault = KingdomSealFault.Empty;
				Detail = "the file is empty";
				return false;
			}
			// A byte-order mark is legal in a UTF-8 file and would otherwise turn the format name
			// into a word nothing recognises.
			string text = (FileText[0] == '\uFEFF') ? FileText.Substring(1) : FileText;

			int cut = 0;
			string[] framing = new string[3];
			for (int line = 0; line < 3; line++)
			{
				int end = text.IndexOf('\n', cut);
				if (end < 0)
				{
					Fault = KingdomSealFault.MalformedFraming;
					Detail = "the seal ends before its framing does";
					return false;
				}
				if (end - cut > MaxFramingLineChars)
				{
					Fault = KingdomSealFault.MalformedFraming;
					Detail = "a framing line is too long";
					return false;
				}
				framing[line] = text.Substring(cut, end - cut).TrimEnd('\r');
				cut = end + 1;
			}

			if (!ParseFraming(framing[0], FormatName, out string schemaText))
			{
				Fault = KingdomSealFault.NotASeal;
				Detail = "the first line does not name this format";
				return false;
			}
			int schema;
			if (!int.TryParse(schemaText, NumberStyles.None, CultureInfo.InvariantCulture, out schema))
			{
				Fault = KingdomSealFault.MalformedFraming;
				Detail = "the schema is not a number";
				return false;
			}
			if (schema < MinSchema || schema > MaxSchema)
			{
				Fault = KingdomSealFault.UnsupportedSchema;
				Detail = "this build reads seal schemas " + MinSchema + " through " + MaxSchema + ", and this one is " + schema;
				return false;
			}
			if (!ParseFraming(framing[1], DigestPrefix, out string digestText) || !IsLowerHex(digestText, 64))
			{
				Fault = KingdomSealFault.MalformedFraming;
				Detail = "the digest line is malformed";
				return false;
			}
			int declared;
			if (!ParseFraming(framing[2], LengthPrefix, out string lengthText)
				|| !int.TryParse(lengthText, NumberStyles.None, CultureInfo.InvariantCulture, out declared)
				|| declared < 0 || declared > MaxPayloadBytes)
			{
				Fault = KingdomSealFault.MalformedFraming;
				Detail = "the length line is malformed";
				return false;
			}

			string payload = text.Substring(cut);
			if (payload.EndsWith("\n", StringComparison.Ordinal))
			{
				payload = payload.Substring(0, payload.Length - 1);
			}
			if (payload.EndsWith("\r", StringComparison.Ordinal))
			{
				payload = payload.Substring(0, payload.Length - 1);
			}
			if (payload.IndexOf('\n') >= 0)
			{
				Fault = KingdomSealFault.TrailingData;
				Detail = "there is more in the file than the seal";
				return false;
			}
			byte[] bytes;
			try
			{
				bytes = new UTF8Encoding(false, true).GetBytes(payload);
			}
			catch (EncoderFallbackException)
			{
				Fault = KingdomSealFault.Malformed;
				Detail = "the payload is not valid text";
				return false;
			}
			if (bytes.Length != declared)
			{
				Fault = KingdomSealFault.LengthMismatch;
				Detail = "the seal declares " + declared + " bytes and carries " + bytes.Length;
				return false;
			}
			string actual;
			if (!TryDigest(bytes, out actual))
			{
				Fault = KingdomSealFault.DigestUnavailable;
				Detail = "no SHA-256 provider is available to check the seal";
				return false;
			}
			if (actual != digestText)
			{
				Fault = KingdomSealFault.ChecksumMismatch;
				Detail = "the seal does not match its own digest";
				return false;
			}

			KingdomSealBody body;
			if (!ParsePayload(payload, out body, out Fault, out Detail))
			{
				return false;
			}
			Schema = schema;
			Body = body;
			Fault = KingdomSealFault.None;
			Detail = "";
			return true;
		}
	}
}
