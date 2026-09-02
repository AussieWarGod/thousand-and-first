using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Canonical encoding and digest for one realized architecture lot.
	/// <para>
	/// Engine-free, so every collision, coverage, and bound case executes without a live game.
	/// </para>
	/// <para>
	/// The grammar is LENGTH-PREFIXED and therefore injective. Each field is either the single
	/// character <c>-</c> for an absent value, or its character count, a colon, and the value
	/// verbatim. Absent and the literal string <c>-</c> are consequently distinct, and no tile,
	/// colour, slot, or blueprint value can imitate a field boundary however it is spelled. A
	/// separator-joined grammar cannot make that promise: it only has to meet one value carrying
	/// its separator, or its own absent sentinel, to make two different lots digest alike.
	/// </para>
	/// </summary>
	public static class KingdomRealizedCaptureRules
	{
		public const string Tag = "rc1";

		/// <summary>Bounded so a hostile or corrupt live value cannot make the canonical text huge.</summary>
		public const int MaxToken = 512;

		public const int MaxCells = 4096;
		public const int MaxObjects = 2048;

		private const string AbsentToken = "-";

		/// <summary>SHA-256 over the canonical text, or null when the lot cannot be canonicalized.</summary>
		/// <summary>
		/// The same facts with the moment-of-capture look removed from what is expected to move: a
		/// placement bound by a stateful anchor (a fire that flickers, a sling that is slept in) and
		/// a door (open or shut). Identity, position, slot, layer, physics, and liquid still bind.
		/// A framing verb compares two captures across a walk with this; the digest of record for a
		/// realized lot is always the live one.
		/// </summary>
		public static List<KingdomRealizedObjectFact> Stabilized(
			IList<KingdomRealizedObjectFact> Objects)
		{
			List<KingdomRealizedObjectFact> stable = new List<KingdomRealizedObjectFact>();
			if (Objects == null) return stable;
			for (int i = 0; i < Objects.Count; i++)
			{
				KingdomRealizedObjectFact fact = Objects[i];
				if (fact == null) continue;
				bool moves = fact.Anchor != null || fact.Door;
				stable.Add(new KingdomRealizedObjectFact
				{
					X = fact.X, Y = fact.Y, Blueprint = fact.Blueprint, Slot = fact.Slot,
					Layer = fact.Layer, Anchor = fact.Anchor, AuthorityProved = fact.AuthorityProved,
					Existing = fact.Existing, Owner = fact.Owner, PhysicsPresent = fact.PhysicsPresent,
					Solid = fact.Solid, BlueprintSolid = fact.BlueprintSolid, Door = fact.Door,
					Liquid = fact.Liquid,
					Tile = moves ? null : fact.Tile,
					RenderString = moves ? null : fact.RenderString,
					ColorString = moves ? null : fact.ColorString,
					DetailColor = moves ? null : fact.DetailColor,
					TileColor = moves ? null : fact.TileColor,
					RenderLayer = moves ? 0 : fact.RenderLayer,
					PathState = fact.PathState
				});
			}
			return stable;
		}

		public static string Digest(int Width, int Height, IList<KingdomRealizedCellFact> Cells,
			IList<KingdomRealizedObjectFact> Objects)
		{
			string canonical = Canonical(Width, Height, Cells, Objects);
			return canonical == null ? null : Sha256(canonical);
		}

		/// <summary>
		/// Total: returns null rather than throwing for every malformed lot, including one built by
		/// direct construction rather than by the reader.
		/// </summary>
		public static string Canonical(int Width, int Height, IList<KingdomRealizedCellFact> Cells,
			IList<KingdomRealizedObjectFact> Objects)
		{
			if (Width < 1 || Height < 1 || Cells == null || Objects == null) return null;
			// Overflow-safe: a hostile rect must not multiply into a small positive int.
			long area = (long)Width * (long)Height;
			if (area > MaxCells || Cells.Count != area) return null;
			if (Objects.Count > MaxObjects) return null;
			List<string> cellRows = CellRows(Width, Height, Cells);
			if (cellRows == null) return null;
			List<string> objectRows = ObjectRows(Width, Height, Objects);
			if (objectRows == null) return null;
			cellRows.Sort(StringComparer.Ordinal);
			objectRows.Sort(StringComparer.Ordinal);
			StringBuilder sb = new StringBuilder(Tag);
			if (!Append(sb, Number(Width)) || !Append(sb, Number(Height))
				|| !Append(sb, Number(cellRows.Count))) return null;
			for (int i = 0; i < cellRows.Count; i++) sb.Append(cellRows[i]);
			if (!Append(sb, Number(objectRows.Count))) return null;
			for (int i = 0; i < objectRows.Count; i++) sb.Append(objectRows[i]);
			return sb.ToString();
		}

		/// <summary>
		/// Every in-bounds coordinate exactly once. A duplicate coordinate paired with a missing one
		/// keeps the total count right while measuring a different lot, so counting is not coverage.
		/// </summary>
		private static List<string> CellRows(int Width, int Height,
			IList<KingdomRealizedCellFact> Cells)
		{
			bool[] seen = new bool[Width * Height];
			List<string> rows = new List<string>(Cells.Count);
			for (int i = 0; i < Cells.Count; i++)
			{
				KingdomRealizedCellFact cell = Cells[i];
				if (cell == null || cell.X < 0 || cell.Y < 0 || cell.X >= Width || cell.Y >= Height)
					return null;
				int index = (cell.Y * Width) + cell.X;
				if (seen[index]) return null;
				seen[index] = true;
				string row = CellRow(cell);
				if (row == null) return null;
				rows.Add(row);
			}
			for (int i = 0; i < seen.Length; i++) if (!seen[i]) return null;
			return rows;
		}

		private static string CellRow(KingdomRealizedCellFact Cell)
		{
			StringBuilder sb = new StringBuilder();
			if (Cell.Components < 0 || !Append(sb, Number(Cell.X)) || !Append(sb, Number(Cell.Y))
				|| !Append(sb, Flag(Cell.Owner)) || !Append(sb, Number(Cell.Components))
				|| !Append(sb, Flag(Cell.Blocking)) || !Append(sb, Flag(Cell.Door))
				|| !Append(sb, Flag(Cell.Liquid))) return null;
			return sb.ToString();
		}

		private static List<string> ObjectRows(int Width, int Height,
			IList<KingdomRealizedObjectFact> Objects)
		{
			List<string> rows = new List<string>(Objects.Count);
			for (int i = 0; i < Objects.Count; i++)
			{
				KingdomRealizedObjectFact item = Objects[i];
				if (item == null || string.IsNullOrEmpty(item.Blueprint)
					|| item.X < 0 || item.Y < 0 || item.X >= Width || item.Y >= Height) return null;
				string row = ObjectRow(item);
				if (row == null) return null;
				rows.Add(row);
			}
			return rows;
		}

		private static string ObjectRow(KingdomRealizedObjectFact Item)
		{
			StringBuilder sb = new StringBuilder();
			// No component token: it hashes the lot id, and two lawful builds of one design hold
			// different lot ids. Authority is proved at capture and enters as a normalized fact.
			if (!Append(sb, Number(Item.X)) || !Append(sb, Number(Item.Y))
				|| !Append(sb, Text(Item.Blueprint)) || !Append(sb, Text(Item.Slot))
				|| !Append(sb, Number(Item.Layer)) || !Append(sb, Text(Item.Anchor))
				|| !Append(sb, Flag(Item.AuthorityProved)) || !Append(sb, Flag(Item.Existing))
				|| !Append(sb, Flag(Item.Owner)) || !Append(sb, Flag(Item.PhysicsPresent))
				|| !Append(sb, Flag(Item.Solid)) || !Append(sb, Flag(Item.BlueprintSolid))
				|| !Append(sb, Flag(Item.Door)) || !Append(sb, Text(Item.Liquid))
				|| !Append(sb, Text(Item.Tile)) || !Append(sb, RenderText(Item.RenderString))
				|| !Append(sb, Text(Item.ColorString)) || !Append(sb, Text(Item.DetailColor))
				|| !Append(sb, Text(Item.TileColor)) || !Append(sb, Number(Item.RenderLayer))
				|| !Append(sb, Number(Item.PathState))) return null;
			return sb.ToString();
		}

		private static bool Append(StringBuilder Builder, string Token)
		{
			if (Token == null) return false;
			Builder.Append(Token);
			return true;
		}

		/// <summary>
		/// One self-delimiting field. Null encodes as the absent marker; every present value carries
		/// its exact character count, so a value spelled like the marker is still a present value.
		/// <para>
		/// The canonical text is hashed as UTF-8, so injectivity has to survive that encoding too.
		/// Ordinary text control values and all unpaired surrogates are refused: .NET's default UTF-8
		/// encoder substitutes U+FFFD for a lone surrogate, which would fold two distinct live values
		/// onto identical bytes and hand back one digest for two different lots. RenderString uses the
		/// dedicated control-glyph path below.
		/// </para>
		/// </summary>
		private static string Text(string Value)
		{
			return FramedText(Value, AllowControls: false);
		}

		/// <summary>
		/// Qud converts numeric render declarations such as <c>009</c> into their one-character
		/// CP437 glyph, which is U+0009 in the live <c>RenderString</c>. These are lawful visual
		/// values, not record delimiters. The length prefix keeps them self-delimiting while strict
		/// UTF-16 validation still refuses values that would collide under UTF-8 substitution.
		/// </summary>
		private static string RenderText(string Value)
		{
			return FramedText(Value, AllowControls: true);
		}

		private static string FramedText(string Value, bool AllowControls)
		{
			if (Value == null) return AbsentToken;
			if (Value.Length > MaxToken) return null;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				// Outside RenderString, a live control value is corrupt, not a short field.
				if (!AllowControls && (c < ' ' || (c >= (char)0x7F && c <= (char)0x9F)))
					return null;
				if (char.IsLowSurrogate(c)) return null;
				if (char.IsHighSurrogate(c))
				{
					if (i + 1 >= Value.Length || !char.IsLowSurrogate(Value[i + 1])) return null;
					i++;
				}
			}
			return Value.Length.ToString(CultureInfo.InvariantCulture) + ":" + Value;
		}

		/// <summary>One length-prefixed key/count pair for a nested subgrammar.</summary>
		public static string Pair(string Key, int Value)
		{
			string key = Text(Key);
			string value = Number(Value);
			return key == null || value == null ? null : key + value;
		}

		/// <summary>
		/// The liquid subgrammar. Framed exactly like the outer one, because a component liquid key
		/// is a live string: joining live strings with separators would put back the collision the
		/// outer framing removes.
		/// </summary>
		public static string Liquid(int Volume, int Maximum, int Flags, IList<string> Components)
		{
			if (Components == null) return null;
			StringBuilder sb = new StringBuilder();
			if (!Append(sb, Number(Volume)) || !Append(sb, Number(Maximum))
				|| !Append(sb, Number(Flags)) || !Append(sb, Number(Components.Count))) return null;
			for (int i = 0; i < Components.Count; i++)
				if (!Append(sb, Components[i])) return null;
			return sb.Length > MaxToken ? null : sb.ToString();
		}

		private static string Number(int Value)
		{
			return Text(Value.ToString(CultureInfo.InvariantCulture));
		}

		private static string Flag(bool Value)
		{
			return Text(Value ? "1" : "0");
		}

		/// <summary>
		/// Strict UTF-8: throwing rather than substituting, so a value that somehow reached here
		/// malformed refuses instead of folding onto another value's bytes.
		/// </summary>
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
