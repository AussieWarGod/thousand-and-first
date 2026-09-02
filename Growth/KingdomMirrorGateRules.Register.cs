using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// The register's wire: how the realm's arches are written into one game-state string and
	/// read back out of it. Engine-free, so the codec can be tabled against golden text.
	/// <para>
	/// <b>Shape.</b> A version token, then rows: <c>v1|key^city^partner|key^city^partner</c>.
	/// The token is the first thing in the string and the only thing in it that is not a row.
	/// A register written before there was a token has none, and reads as version 1, so no save
	/// is asked to migrate on the day it is opened; it is carried forward in the current shape
	/// the first time something legitimately writes it, and never merely because it was read.
	/// </para>
	/// <para>
	/// <b>Why a token at all.</b> Without one, a row this build does not understand &mdash; a
	/// column a later build added, or something else's write &mdash; is indistinguishable from
	/// corruption, and the repair below would drop it and rewrite the register on the spot,
	/// silently unkeying arches. With one, a later build's register is refused whole and left
	/// exactly as it was (ARCHITECTURE, "Persistence and wire format").
	/// </para>
	/// </summary>
	internal static partial class KingdomMirrorGateRules
	{
		/// <summary>The token every register this build writes opens with. Exactly <c>v1</c>,
		/// followed by one <see cref="RowSeparator"/>.</summary>
		internal const string RegisterVersionToken = "v1";

		/// <summary>Longest thing that reads as a version token: <c>v</c> and a few digits.</summary>
		private const int MaxVersionTokenLength = 8;

		/// <summary>What the founder is told when the register belongs to a newer build. It is not
		/// damage and it is not repaired; the text waits, untouched, for the build that wrote it.</summary>
		internal const string FutureVersionLine =
			"The realm's record of its arches was written by a newer version of this mod. It is left exactly as it was: nothing here will read, repair, or re-key it.";

		/// <summary>
		/// Reads the register. Untrusted, because a save is untrusted and our own older writing is
		/// untrusted with it.
		/// <para>
		/// An unreadable row is <b>dropped and counted</b> rather than taken as a reason to throw
		/// the whole register away: one corrupt row must not cost the founder a crossing that is
		/// standing perfectly well at the other end. The count is reported so the caller can say so
		/// once (STANDARDS 7b) instead of losing it in silence.
		/// </para>
		/// <para>
		/// That repair is only ever applied to a version this build recognises. Text opening with a
		/// token it does not know is a newer build's register: nothing is read from it, nothing is
		/// dropped, <paramref name="futureVersion"/> says so, and the caller must leave the stored
		/// text exactly as it found it.
		/// </para>
		/// </summary>
		/// <param name="text">Register text; null and empty both read as no arches at all, which is
		/// the ordinary state of a realm that has never keyed one.</param>
		/// <param name="rows">Rows in register order. Never null.</param>
		/// <param name="dropped">Rows that could not be read.</param>
		/// <param name="futureVersion">True when the text opens with a version token this build does
		/// not know. Distinct from corruption: nothing about the rows can be said.</param>
		/// <returns>True when the version was known and nothing was dropped.</returns>
		internal static bool TryParseRegister(string text, out KingdomGateRow[] rows, out int dropped,
			out bool futureVersion)
		{
			dropped = 0;
			futureVersion = false;
			if (string.IsNullOrEmpty(text))
			{
				rows = new KingdomGateRow[0];
				return true;
			}
			int first = ReadVersionToken(text, out futureVersion);
			if (futureVersion)
			{
				rows = new KingdomGateRow[0];
				return false;
			}
			string[] parts = text.Substring(first).Split(RowSeparator);
			KingdomGateRow[] read = new KingdomGateRow[(parts.Length < MaxGates) ? parts.Length : MaxGates];
			int kept = 0;
			for (int i = 0; i < parts.Length; i++)
			{
				if (parts[i].Length == 0)
				{
					continue;
				}
				if (kept >= MaxGates)
				{
					dropped++;
					continue;
				}
				string[] columns = parts[i].Split(FieldSeparator);
				if (columns.Length != 3 || columns[0].Length == 0 || columns[1].Length == 0)
				{
					dropped++;
					continue;
				}
				// A key twice over is a corrupt register, not two arches: the second reading would
				// silently win every lookup below and the founder would never learn which is which.
				if (IndexOfKey(read, kept, columns[0]) >= 0)
				{
					dropped++;
					continue;
				}
				// One keyed arch per city is also save authority. A hostile duplicate city must
				// not become a second destination merely because its key differs.
				if (IndexOfCity(read, columns[1]) >= 0)
				{
					dropped++;
					continue;
				}
				read[kept++] = new KingdomGateRow(columns[0], columns[1], columns[2]);
			}
			rows = new KingdomGateRow[kept];
			Array.Copy(read, rows, kept);
			return dropped == 0;
		}

		/// <summary>The same reading for callers that only ever needed rows and a count. A newer
		/// build's register reads as refused, with no rows and nothing dropped.</summary>
		internal static bool TryParseRegister(string text, out KingdomGateRow[] rows, out int dropped)
		{
			return TryParseRegister(text, out rows, out dropped, out bool _);
		}

		/// <summary>
		/// Where the rows begin: after a leading version token, or at the start when there is
		/// none. Only <c>v</c> and digits, standing alone before the first row separator, read as
		/// a token. A row always carries a field separator, so no row can be mistaken for one.
		/// </summary>
		private static int ReadVersionToken(string text, out bool futureVersion)
		{
			futureVersion = false;
			int end = text.IndexOf(RowSeparator);
			string token = (end < 0) ? text : text.Substring(0, end);
			if (token.Length < 2 || token.Length > MaxVersionTokenLength || token[0] != 'v')
			{
				return 0;
			}
			for (int i = 1; i < token.Length; i++)
			{
				if (token[i] < '0' || token[i] > '9')
				{
					return 0;
				}
			}
			futureVersion = !string.Equals(token, RegisterVersionToken, StringComparison.Ordinal);
			return (end < 0) ? text.Length : end + 1;
		}

		/// <summary>The register as one string, ready to be carried in game state: the version
		/// token, then the rows. No arches is the empty string, exactly as it always was, so a realm
		/// that has never keyed an arch never carries a token either.</summary>
		internal static string FormatRegister(KingdomGateRow[] rows)
		{
			string body = LegacyRegisterText(rows);
			return (body.Length == 0) ? "" : RegisterVersionToken + RowSeparator + body;
		}

		/// <summary>
		/// The rows exactly as a pre-version build wrote them, with no token. Read-side only: it
		/// lets a caller prove that an old save's text is byte-for-byte what that build wrote before
		/// trusting it as exact. Nothing writes this; every write goes through
		/// <see cref="FormatRegister"/>.
		/// </summary>
		internal static string LegacyRegisterText(KingdomGateRow[] rows)
		{
			if (rows == null || rows.Length == 0)
			{
				return "";
			}
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			for (int i = 0; i < rows.Length; i++)
			{
				if (text.Length > 0)
				{
					text.Append(RowSeparator);
				}
				text.Append(rows[i].Key).Append(FieldSeparator).Append(rows[i].City).Append(FieldSeparator).Append(rows[i].Partner);
			}
			return text.ToString();
		}
	}
}
