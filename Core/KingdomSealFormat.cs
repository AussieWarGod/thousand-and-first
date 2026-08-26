using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Why a seal file was refused. Every refusal is named, because a seal is refused whole or
	/// accepted whole and the founder is owed the reason either way.
	/// </summary>
	internal enum KingdomSealFault
	{
		None = 0,
		/// <summary>Nothing there, or nothing but whitespace.</summary>
		Empty,
		/// <summary>The first line is not this format's name.</summary>
		NotASeal,
		/// <summary>A schema this build does not read. The version gate.</summary>
		UnsupportedSchema,
		/// <summary>A framing line is missing or malformed.</summary>
		MalformedFraming,
		/// <summary>The payload is not the length the framing declared.</summary>
		LengthMismatch,
		/// <summary>The payload does not hash to the digest the framing declared.</summary>
		ChecksumMismatch,
		/// <summary>Bytes past the declared payload.</summary>
		TrailingData,
		/// <summary>Larger than any honest seal.</summary>
		TooLarge,
		/// <summary>The payload is not this format's strict subset of JSON.</summary>
		Malformed,
		/// <summary>The same key twice.</summary>
		DuplicateKey,
		/// <summary>A key this schema does not define.</summary>
		UnknownKey,
		/// <summary>A key this schema requires, absent.</summary>
		MissingKey,
		/// <summary>A key present with the wrong shape.</summary>
		WrongKind,
		/// <summary>A number, string, or list past its declared bound.</summary>
		OutOfBounds,
		/// <summary>No digest provider. Never worked around.</summary>
		DigestUnavailable
	}

	/// <summary>
	/// The kind a value in a seal payload has. Deliberately four and no more: bounded primitives
	/// and lists of them, which is the whole of what may cross runs.
	/// <para>
	/// <see cref="EmptyList"/> exists because <c>[]</c> cannot say whether it wanted numbers or
	/// text, and inventing an answer at parse time is how a reader starts disagreeing with a
	/// writer. It answers to both accessors and to neither type.
	/// </para>
	/// </summary>
	internal enum KingdomSealKind
	{
		Text = 0,
		Number = 1,
		TextList = 2,
		NumberList = 3,
		EmptyList = 4
	}

	/// <summary>
	/// One flat seal payload: ordered keys over bounded primitives, and nothing else.
	/// <para>
	/// Ordered because the canonical form must be reproducible: the same facts written twice
	/// produce the same bytes, and a reader that re-writes what it read produces the file it was
	/// given. Ordering is insertion order, which is the writer's declared order.
	/// </para>
	/// </summary>
	internal sealed class KingdomSealBody
	{
		private readonly List<string> _order = new List<string>();

		private readonly Dictionary<string, KingdomSealKind> _kinds = new Dictionary<string, KingdomSealKind>();

		private readonly Dictionary<string, string> _text = new Dictionary<string, string>();

		private readonly Dictionary<string, long> _number = new Dictionary<string, long>();

		private readonly Dictionary<string, List<string>> _textList = new Dictionary<string, List<string>>();

		private readonly Dictionary<string, List<long>> _numberList = new Dictionary<string, List<long>>();

		/// <summary>The keys, in the order they were written.</summary>
		public IList<string> Keys => _order;

		public int Count => _order.Count;

		public bool Has(string Key)
		{
			return Key != null && _kinds.ContainsKey(Key);
		}

		public KingdomSealKind KindOf(string Key)
		{
			KingdomSealKind kind;
			return (Key != null && _kinds.TryGetValue(Key, out kind)) ? kind : KingdomSealKind.Text;
		}

		/// <summary>
		/// Writes a text value. A null is written as empty rather than refused: an absent founder
		/// name is a fact about a seal, not a corruption of one.
		/// </summary>
		/// <exception cref="ArgumentException">The key is null, empty, or already written.</exception>
		public void Put(string Key, string Value)
		{
			Claim(Key, KingdomSealKind.Text);
			_text[Key] = Value ?? "";
		}

		/// <exception cref="ArgumentException">The key is null, empty, or already written.</exception>
		public void Put(string Key, long Value)
		{
			Claim(Key, KingdomSealKind.Number);
			_number[Key] = Value;
		}

		/// <exception cref="ArgumentException">The key is null, empty, or already written.</exception>
		public void PutList(string Key, IList<string> Values)
		{
			if (Values == null || Values.Count == 0)
			{
				Claim(Key, KingdomSealKind.EmptyList);
				return;
			}
			Claim(Key, KingdomSealKind.TextList);
			List<string> copy = new List<string>(Values.Count);
			for (int i = 0; i < Values.Count; i++)
			{
				copy.Add(Values[i] ?? "");
			}
			_textList[Key] = copy;
		}

		/// <exception cref="ArgumentException">The key is null, empty, or already written.</exception>
		public void PutList(string Key, IList<long> Values)
		{
			if (Values == null || Values.Count == 0)
			{
				Claim(Key, KingdomSealKind.EmptyList);
				return;
			}
			Claim(Key, KingdomSealKind.NumberList);
			_numberList[Key] = new List<long>(Values);
		}

		/// <summary>The text at <paramref name="Key"/>, or null when absent or another kind.</summary>
		public string Text(string Key)
		{
			string value;
			return (Key != null && _text.TryGetValue(Key, out value)) ? value : null;
		}

		/// <summary>The number at <paramref name="Key"/>, or <paramref name="Fallback"/>.</summary>
		public long Number(string Key, long Fallback = 0L)
		{
			long value;
			return (Key != null && _number.TryGetValue(Key, out value)) ? value : Fallback;
		}

		/// <summary>The text list at <paramref name="Key"/>; empty for an empty list; null when
		/// absent or of the other kind.</summary>
		public List<string> TextList(string Key)
		{
			if (Key == null)
			{
				return null;
			}
			List<string> value;
			if (_textList.TryGetValue(Key, out value))
			{
				return value;
			}
			return (KindOf(Key) == KingdomSealKind.EmptyList && _kinds.ContainsKey(Key)) ? new List<string>() : null;
		}

		/// <summary>The number list at <paramref name="Key"/>; empty for an empty list; null when
		/// absent or of the other kind.</summary>
		public List<long> NumberList(string Key)
		{
			if (Key == null)
			{
				return null;
			}
			List<long> value;
			if (_numberList.TryGetValue(Key, out value))
			{
				return value;
			}
			return (KindOf(Key) == KingdomSealKind.EmptyList && _kinds.ContainsKey(Key)) ? new List<long>() : null;
		}

		internal void Adopt(string Key, KingdomSealKind Kind, string Text, long Number, List<string> Texts, List<long> Numbers)
		{
			Claim(Key, Kind);
			switch (Kind)
			{
			case KingdomSealKind.Text:
				_text[Key] = Text;
				break;
			case KingdomSealKind.Number:
				_number[Key] = Number;
				break;
			case KingdomSealKind.TextList:
				_textList[Key] = Texts;
				break;
			case KingdomSealKind.NumberList:
				_numberList[Key] = Numbers;
				break;
			}
		}

		private void Claim(string Key, KingdomSealKind Kind)
		{
			if (string.IsNullOrEmpty(Key))
			{
				throw new ArgumentException("A seal key may not be empty.");
			}
			if (_kinds.ContainsKey(Key))
			{
				throw new ArgumentException("The seal key '" + Key + "' was written twice.");
			}
			_kinds[Key] = Kind;
			_order.Add(Key);
		}
	}

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
	internal static class KingdomSealFormat
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

		private static bool ParsePayload(string Payload, out KingdomSealBody Body, out KingdomSealFault Fault, out string Detail)
		{
			Body = null;
			Fault = KingdomSealFault.Malformed;
			Detail = "the seal's record is malformed";
			int at = 0;
			KingdomSealBody body = new KingdomSealBody();
			HashSet<string> seen = new HashSet<string>();
			SkipSpace(Payload, ref at);
			if (!Take(Payload, ref at, '{'))
			{
				return false;
			}
			SkipSpace(Payload, ref at);
			if (Take(Payload, ref at, '}'))
			{
				SkipSpace(Payload, ref at);
				if (at != Payload.Length)
				{
					Fault = KingdomSealFault.TrailingData;
					Detail = "there is more after the seal's record";
					return false;
				}
				Body = body;
				Fault = KingdomSealFault.None;
				Detail = "";
				return true;
			}
			while (true)
			{
				Fault = KingdomSealFault.Malformed;
				Detail = "the seal's record is malformed";
				if (body.Count >= MaxKeys)
				{
					Fault = KingdomSealFault.OutOfBounds;
					Detail = "the seal's record carries too many fields";
					return false;
				}
				SkipSpace(Payload, ref at);
				string key;
				if (!ReadText(Payload, ref at, MaxKeyChars, out key) || key.Length == 0)
				{
					Detail = "a key in the seal's record is malformed";
					return false;
				}
				if (!seen.Add(key))
				{
					Fault = KingdomSealFault.DuplicateKey;
					Detail = "the key '" + key + "' appears twice";
					return false;
				}
				SkipSpace(Payload, ref at);
				if (!Take(Payload, ref at, ':'))
				{
					return false;
				}
				SkipSpace(Payload, ref at);
				if (!ReadValue(Payload, ref at, body, key, out Fault, out Detail))
				{
					return false;
				}
				SkipSpace(Payload, ref at);
				if (Take(Payload, ref at, ','))
				{
					continue;
				}
				if (Take(Payload, ref at, '}'))
				{
					break;
				}
				Fault = KingdomSealFault.Malformed;
				Detail = "the seal's record does not close";
				return false;
			}
			SkipSpace(Payload, ref at);
			if (at != Payload.Length)
			{
				Fault = KingdomSealFault.TrailingData;
				Detail = "there is more after the seal's record";
				return false;
			}
			Body = body;
			Fault = KingdomSealFault.None;
			Detail = "";
			return true;
		}

		private static bool ReadValue(string S, ref int At, KingdomSealBody Body, string Key, out KingdomSealFault Fault, out string Detail)
		{
			Fault = KingdomSealFault.Malformed;
			Detail = "the value of '" + Key + "' is malformed";
			if (At >= S.Length)
			{
				return false;
			}
			char c = S[At];
			if (c == '"')
			{
				string text;
				if (!ReadText(S, ref At, MaxValueChars, out text))
				{
					return false;
				}
				Body.Adopt(Key, KingdomSealKind.Text, text, 0L, null, null);
				Fault = KingdomSealFault.None;
				Detail = "";
				return true;
			}
			if (c == '-' || (c >= '0' && c <= '9'))
			{
				long number;
				if (!ReadNumber(S, ref At, out number))
				{
					return false;
				}
				Body.Adopt(Key, KingdomSealKind.Number, null, number, null, null);
				Fault = KingdomSealFault.None;
				Detail = "";
				return true;
			}
			if (c != '[')
			{
				return false;
			}
			At++;
			SkipSpace(S, ref At);
			if (At >= S.Length)
			{
				return false;
			}
			if (Take(S, ref At, ']'))
			{
				Body.Adopt(Key, KingdomSealKind.EmptyList, null, 0L, null, null);
				Fault = KingdomSealFault.None;
				Detail = "";
				return true;
			}
			bool textList = S[At] == '"';
			List<string> texts = textList ? new List<string>() : null;
			List<long> numbers = textList ? null : new List<long>();
			while (true)
			{
				SkipSpace(S, ref At);
				if (At >= S.Length)
				{
					return false;
				}
				if (textList)
				{
					string item;
					if (S[At] != '"' || !ReadText(S, ref At, MaxValueChars, out item))
					{
						return false;
					}
					texts.Add(item);
				}
				else
				{
					long item;
					if (!ReadNumber(S, ref At, out item))
					{
						return false;
					}
					numbers.Add(item);
				}
				if ((textList ? texts.Count : numbers.Count) > MaxArrayItems)
				{
					Fault = KingdomSealFault.OutOfBounds;
					Detail = "the list in '" + Key + "' carries too many entries";
					return false;
				}
				SkipSpace(S, ref At);
				if (Take(S, ref At, ','))
				{
					continue;
				}
				if (Take(S, ref At, ']'))
				{
					break;
				}
				return false;
			}
			Body.Adopt(Key, textList ? KingdomSealKind.TextList : KingdomSealKind.NumberList, null, 0L, texts, numbers);
			Fault = KingdomSealFault.None;
			Detail = "";
			return true;
		}

		private static bool ReadText(string S, ref int At, int MaxChars, out string Value)
		{
			Value = null;
			if (At >= S.Length || S[At] != '"')
			{
				return false;
			}
			At++;
			StringBuilder sb = new StringBuilder();
			while (true)
			{
				if (At >= S.Length)
				{
					return false;
				}
				char c = S[At++];
				if (c == '"')
				{
					if (sb.Length > MaxChars)
					{
						return false;
					}
					Value = sb.ToString();
					return true;
				}
				// A raw control character is a corrupt file, never a value: the writer escapes
				// every one of them, so meeting one means something else edited this.
				if (c < ' ')
				{
					return false;
				}
				if (c != '\\')
				{
					sb.Append(c);
					if (sb.Length > MaxChars)
					{
						return false;
					}
					continue;
				}
				if (At >= S.Length)
				{
					return false;
				}
				char escape = S[At++];
				if (escape == '"')
				{
					sb.Append('"');
				}
				else if (escape == '\\')
				{
					sb.Append('\\');
				}
				else if (escape == 'u')
				{
					if (At + 4 > S.Length)
					{
						return false;
					}
					int code = 0;
					for (int i = 0; i < 4; i++)
					{
						int digit = HexDigit(S[At + i]);
						if (digit < 0)
						{
							return false;
						}
						code = code * 16 + digit;
					}
					At += 4;
					sb.Append((char)code);
				}
				else
				{
					// Every other escape JSON defines is refused rather than honoured. The writer
					// emits none of them, so an input carrying one did not come from here.
					return false;
				}
				if (sb.Length > MaxChars)
				{
					return false;
				}
			}
		}

		private static bool ReadNumber(string S, ref int At, out long Value)
		{
			Value = 0L;
			int start = At;
			if (At < S.Length && S[At] == '-')
			{
				At++;
			}
			int digits = 0;
			while (At < S.Length && S[At] >= '0' && S[At] <= '9')
			{
				At++;
				digits++;
			}
			if (digits == 0)
			{
				return false;
			}
			// Leading zeros, a decimal point, and an exponent are all outside this grammar. Each
			// would give one value two spellings, and a canonical form cannot have two spellings.
			if (digits > 1 && S[At - digits] == '0')
			{
				return false;
			}
			if (At < S.Length && (S[At] == '.' || S[At] == 'e' || S[At] == 'E'))
			{
				return false;
			}
			return long.TryParse(S.Substring(start, At - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out Value);
		}

		private static int HexDigit(char C)
		{
			if (C >= '0' && C <= '9')
			{
				return C - '0';
			}
			if (C >= 'a' && C <= 'f')
			{
				return C - 'a' + 10;
			}
			if (C >= 'A' && C <= 'F')
			{
				return C - 'A' + 10;
			}
			return -1;
		}

		private static void SkipSpace(string S, ref int At)
		{
			while (At < S.Length && (S[At] == ' ' || S[At] == '\t'))
			{
				At++;
			}
		}

		private static bool Take(string S, ref int At, char C)
		{
			if (At < S.Length && S[At] == C)
			{
				At++;
				return true;
			}
			return false;
		}
	}
}
