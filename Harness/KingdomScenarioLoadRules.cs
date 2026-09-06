using System;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Immutable request naming the four artifacts a scenario load binds together: the game the
	/// load was prepared against and the primary, info, cache and snapshot content each keyed by
	/// its own SHA-256. Every field is stored exactly as given; nothing here trims, lowers, or
	/// otherwise repairs a value on the way in.
	/// </summary>
	internal sealed class KingdomScenarioLoadRequest
	{
		internal readonly string GameId;
		internal readonly string PrimarySha256;
		internal readonly string InfoSha256;
		internal readonly string CacheSha256;
		internal readonly string SnapshotSha256;

		internal KingdomScenarioLoadRequest(string GameId, string PrimarySha256, string InfoSha256,
			string CacheSha256, string SnapshotSha256)
		{
			this.GameId = GameId;
			this.PrimarySha256 = PrimarySha256;
			this.InfoSha256 = InfoSha256;
			this.CacheSha256 = CacheSha256;
			this.SnapshotSha256 = SnapshotSha256;
		}
	}

	/// <summary>
	/// Pure codec and validity law for the <c>scenario-load.txt</c> wire.
	/// <para>
	/// The wire is exactly six LF-terminated lines: a fixed header, the canonical lowercase Guid
	/// "D" form of the game id, then the four SHA-256 hashes in a fixed order. Total and
	/// fail-closed: every malformed shape is refused rather than trimmed, lowered, or otherwise
	/// repaired, so a torn or hand-edited file can never be silently coerced into a request naming
	/// artifacts nobody wrote.
	/// </para>
	/// <para>
	/// Pure Harness law: no file IO, no path resolution, and no engine (XRL) reference anywhere in
	/// this file. Callers own reading <see cref="FileName"/> and handing the raw text here.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioLoadRules
	{
		/// <summary>Fixed name of the file this wire is read from and written to.</summary>
		internal const string FileName = "scenario-load.txt";

		/// <summary>First line of every wire; refused on any other value, including a case or
		/// spelling variant.</summary>
		internal const string Header = "taf-scenario-load-v1";

		/// <summary>Whole-wire cap, proved BEFORE any split allocates.</summary>
		internal const int MaxChars = 512;

		private const int GuidChars = 36;
		private const int HashChars = 64;
		private const int WireLines = 6;

		/// <summary>
		/// True only when every field is exactly well formed: a canonical lowercase Guid "D" game
		/// id and four exact 64-character lowercase-hex hashes, with the encoded wire still within
		/// <see cref="MaxChars"/>. Checked by character-class pattern, never by round-tripping
		/// through <see cref="Guid"/> parsing and reformatting, so an accepted value has no chance
		/// to be silently reformatted into the shape this law expects.
		/// </summary>
		internal static bool Valid(KingdomScenarioLoadRequest Request)
		{
			return Request != null
				&& ValidGameId(Request.GameId)
				&& ValidHash(Request.PrimarySha256)
				&& ValidHash(Request.InfoSha256)
				&& ValidHash(Request.CacheSha256)
				&& ValidHash(Request.SnapshotSha256)
				&& WireLength(Request) <= MaxChars;
		}

		/// <summary>Encodes a valid request to the exact six-line LF wire. Refuses (null, false)
		/// for any request <see cref="Valid"/> already refuses; never partially encodes.</summary>
		internal static bool TryEncode(KingdomScenarioLoadRequest Request, out string Wire)
		{
			Wire = null;
			if (!Valid(Request)) return false;
			string wire = Header + "\n" + Request.GameId + "\n" + Request.PrimarySha256 + "\n"
				+ Request.InfoSha256 + "\n" + Request.CacheSha256 + "\n" + Request.SnapshotSha256
				+ "\n";
			if (wire.Length > MaxChars) return false;
			Wire = wire;
			return true;
		}

		/// <summary>
		/// Parses the exact six-line LF wire. Refuses on a null or over-cap text, any carriage
		/// return, a text that does not end with exactly one final line feed, a text that does not
		/// split into exactly <see cref="WireLines"/> content lines (so a missing line, an extra
		/// line, and a duplicated header line are all refused the same way: the line count no
		/// longer matches), a first line other than <see cref="Header"/> exactly, or any field that
		/// fails the same predicates <see cref="Valid"/> applies. On success the request holds the
		/// exact substrings the wire carried; nothing is trimmed or lowered.
		/// </summary>
		internal static bool TryParse(string Text, out KingdomScenarioLoadRequest Request)
		{
			Request = null;
			if (Text == null || Text.IndexOf('\r') >= 0 || Text.Length > MaxChars) return false;
			string[] lines = Text.Split('\n');
			// WireLines content lines plus the empty tail Split leaves after the one required final
			// '\n'. A missing line, an extra line, and a second trailing '\n' are all caught here
			// because each shifts this count away from exactly WireLines + 1.
			if (lines.Length != WireLines + 1 || lines[WireLines].Length != 0) return false;
			if (!string.Equals(lines[0], Header, StringComparison.Ordinal)) return false;
			KingdomScenarioLoadRequest candidate = new KingdomScenarioLoadRequest(
				lines[1], lines[2], lines[3], lines[4], lines[5]);
			if (!Valid(candidate)) return false;
			Request = candidate;
			return true;
		}

		private static int WireLength(KingdomScenarioLoadRequest Request)
		{
			return Header.Length + 1 + Request.GameId.Length + 1 + Request.PrimarySha256.Length + 1
				+ Request.InfoSha256.Length + 1 + Request.CacheSha256.Length + 1
				+ Request.SnapshotSha256.Length + 1;
		}

		/// <summary>Canonical lowercase Guid "D" form only: 8-4-4-4-12 lowercase hex grouped by
		/// hyphens at exactly the four fixed positions. A brace, an "N"/"B"/"P" layout, upper case,
		/// or embedded whitespace all fail this pattern directly, with no Guid parse involved.
		/// </summary>
		private static bool ValidGameId(string Value)
		{
			if (Value == null || Value.Length != GuidChars) return false;
			for (int i = 0; i < GuidChars; i++)
			{
				if (i == 8 || i == 13 || i == 18 || i == 23)
				{
					if (Value[i] != '-') return false;
				}
				else if (!IsLowerHex(Value[i])) return false;
			}
			return true;
		}

		/// <summary>Exactly 64 lowercase hex characters; nothing else, of any length.</summary>
		private static bool ValidHash(string Value)
		{
			if (Value == null || Value.Length != HashChars) return false;
			for (int i = 0; i < HashChars; i++)
				if (!IsLowerHex(Value[i])) return false;
			return true;
		}

		private static bool IsLowerHex(char Character)
		{
			return (Character >= '0' && Character <= '9') || (Character >= 'a' && Character <= 'f');
		}
	}
}
