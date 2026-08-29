using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>
	/// The canonical full zone locator, and nothing that merely resembles one.
	/// <para>
	/// A locator is the only thing in this family that names a place on the map, so it is the only
	/// thing that can quietly send a founder somewhere they never went. Two engine facts make
	/// loose parsing worse than useless here.
	/// </para>
	/// <para>
	/// The first is that the engine's parser forgives. <c>XRL/World/ZoneID.cs:109-121</c> wraps
	/// all five <c>int.Parse</c> calls in one <c>try</c> whose <c>catch</c> logs to
	/// <c>MetricsManager</c> and then <b>returns true anyway</b>, leaving every component at its
	/// initialised -1. Hand the engine a malformed locator and it does not refuse it; it agrees
	/// to a place that does not exist.
	/// </para>
	/// <para>
	/// The second is that a locator does not survive a save unchanged. <c>JournalMapNote</c> holds
	/// its assembled string in <c>[NonSerialized] private string _ZoneID</c> and writes only the
	/// six components (<c>Qud/API/JournalMapNote.cs</c>, generated <c>Write</c>/<c>Read</c>); on
	/// reload the getter finds <c>_ZoneID == null</c> and rebuilds the string from those
	/// components. So a non-canonical locator matches itself all session and stops matching after
	/// the first save. Anything this build stores as evidence has to be the string the engine
	/// would rebuild, or the evidence expires quietly.
	/// </para>
	/// <para>
	/// Hence one rule, and it is an identity rather than a grammar: parse the six components, hold
	/// each to its real range, reassemble them exactly as <c>ZoneID.Assemble</c> does
	/// (<c>XRL/World/ZoneID.cs:26-38</c>) and require the result to equal the input, byte for
	/// byte. Leading zeroes, a leading plus, a stray minus, surrounding or embedded whitespace and
	/// any trailing suffix all fail that identity without needing a clause of their own.
	/// </para>
	/// </summary>
	public static partial class KingdomCuriosityRules
	{
		/// <summary>
		/// The only number on this page the engine does not state. World names ship at ten
		/// characters at the longest &mdash; <c>JoppaWorld</c> and <c>NorthSheva</c> in
		/// <c>Base/Worlds.xml</c>, then <c>Tzimtzlum</c>, <c>ThinWorld</c>, <c>Interior</c> &mdash;
		/// and this is set far above them so a mod's world is not refused for being wordy. It is
		/// a chosen bound, and it is here rather than in the byte arithmetic so that the cap can
		/// quote it instead of guessing.
		/// </summary>
		public const int MaxWorldIdChars = 64;

		/// <summary>
		/// The engine's world grid is three zones wide and three tall:
		/// <c>XRL/World/Definitions.cs:5,7</c> (<c>Width = 3</c>, <c>Height = 3</c>), the same two
		/// numbers that dimension <c>CellBlueprint.LevelBlueprint</c>
		/// (<c>XRL/World/CellBlueprint.cs:19</c>), and the same divisor
		/// <c>ZoneID.Assemble</c> uses when it splits a location into parasang and zone
		/// (<c>XRL/World/ZoneID.cs:15-21</c>, <c>Location.X / 3</c> and <c>Location.X % 3</c>).
		/// </summary>
		public const int ZonesPerParasang = 3;
		public const int MaxZoneX = ZonesPerParasang - 1;
		public const int MaxZoneY = ZonesPerParasang - 1;

		/// <summary>
		/// Strata run from zero to one below <c>Definitions.Layers</c>
		/// (<c>XRL/World/Definitions.cs:9</c>, <c>Layers = 50</c>), which is the bound
		/// <c>ZoneManager</c> itself enforces when it walks up or down: below zero wraps to
		/// <c>Layers - 1</c> and at or above <c>Layers</c> wraps to zero
		/// (<c>XRL/World/ZoneManager.cs:1660-1666</c>).
		/// </summary>
		public const int LayerCount = 50;
		public const int MaxZoneZ = LayerCount - 1;

		/// <summary>
		/// Parasangs are bounded by the grid a map note is actually plotted on.
		/// <c>JournalMapNote.ResolvedLocation</c> is <c>Location2D.Get(ParasangX * 3 + ZoneX,
		/// ParasangY * 3 + ZoneY)</c>, and <c>Location2D.Get</c> hands back <b>null</b> outside
		/// <c>MaxX = 250</c> by <c>MaxY = 85</c> (<c>Genkit/Location2D.cs:94-95,154-161</c>);
		/// the journal's own line data plots a note through exactly that call
		/// (<c>Qud/UI/JournalLineData.cs:51</c>). A locator past these is not a distant place,
		/// it is a note with no position at all.
		/// </summary>
		public const int ResolvedWidth = 250;
		public const int ResolvedHeight = 85;
		public const int MaxParasangX = (ResolvedWidth - 1) / ZonesPerParasang;
		public const int MaxParasangY = (ResolvedHeight - 1) / ZonesPerParasang;

		/// <summary>Five separators, and the widest each numeric component can render.</summary>
		public const int LocatorSeparators = 5;
		public const int MaxLocatorNumericChars = 2 + 2 + 1 + 1 + 2;
		public const int MaxLocatorChars =
			MaxWorldIdChars + LocatorSeparators + MaxLocatorNumericChars;

		/// <summary>Whether a string is the exact canonical locator of a real zone.</summary>
		public static bool TryFullLocator(string value)
		{
			int px, py, zx, zy, zz;
			string world;
			return TryFullLocator(value, out world, out px, out py, out zx, out zy, out zz);
		}

		/// <summary>The same judgement, handing back the components it proved.</summary>
		public static bool TryFullLocator(string value, out string world, out int parasangX,
			out int parasangY, out int zoneX, out int zoneY, out int zoneZ)
		{
			world = null; parasangX = -1; parasangY = -1; zoneX = -1; zoneY = -1; zoneZ = -1;
			if (string.IsNullOrEmpty(value) || value.Length > MaxLocatorChars) return false;
			string[] parts = value.Split('.');
			if (parts.Length != 6 || !ValidWorldId(parts[0])) return false;
			// Held locally until every question is answered. A caller that reads these on a
			// refusal must find nothing it could mistake for a place.
			int px, py, zx, zy, zz;
			if (!Component(parts[1], MaxParasangX, out px)
				|| !Component(parts[2], MaxParasangY, out py)
				|| !Component(parts[3], MaxZoneX, out zx)
				|| !Component(parts[4], MaxZoneY, out zy)
				|| !Component(parts[5], MaxZoneZ, out zz)) return false;
			if (px * ZonesPerParasang + zx >= ResolvedWidth
				|| py * ZonesPerParasang + zy >= ResolvedHeight) return false;
			if (!string.Equals(value, Assemble(parts[0], px, py, zx, zy, zz),
				System.StringComparison.Ordinal)) return false;
			world = parts[0];
			parasangX = px; parasangY = py; zoneX = zx; zoneY = zy; zoneZ = zz;
			return true;
		}

		/// <summary>
		/// The engine's own canonical rendering, transcribed rather than called: this file is
		/// compiled into test projects that have no game to link against.
		/// <c>XRL/World/ZoneID.cs:26-38</c>, and the identical reassembly in the
		/// <c>JournalMapNote.ZoneID</c> getter.
		/// </summary>
		public static string Assemble(string world, int parasangX, int parasangY, int zoneX,
			int zoneY, int zoneZ)
		{
			return world + "." + parasangX + "." + parasangY + "." + zoneX + "." + zoneY
				+ "." + zoneZ;
		}

		/// <summary>
		/// A world segment must be a plain name. The separator is excluded for the obvious reason;
		/// '@' is excluded because <c>ZoneID.Parse</c> reads everything after it as a blueprint
		/// and instance and drops both when <c>JournalMapNote</c> reassembles the string
		/// (<c>XRL/World/ZoneID.cs:53-77</c>), so an '@' locator cannot round-trip.
		/// <para>
		/// The rest is a Unicode judgement rather than an ASCII one. Refusing only C0 and DEL
		/// would let a zero-width joiner, a right-to-left override, a non-breaking space or a lone
		/// surrogate sit inside a world name that then looks identical to a real one in every
		/// place a founder can see it. Whitespace, control characters, format characters and
		/// unpaired surrogates are all refused, so two locators that read the same are the same.
		/// </para>
		/// </summary>
		public static bool ValidWorldId(string world)
		{
			if (string.IsNullOrEmpty(world) || world.Length > MaxWorldIdChars) return false;
			for (int i = 0; i < world.Length; i++)
			{
				char c = world[i];
				if (c == '.' || c == '@' || char.IsWhiteSpace(c) || char.IsControl(c)
					|| char.GetUnicodeCategory(c) == UnicodeCategory.Format) return false;
			}
			return Utf8Encodable(world);
		}

		/// <summary>
		/// The bound a locator read out of an existing save is held to, which is not the bound new
		/// authorship is held to.
		/// <para>
		/// The first build accepted any non-empty world, any non-negative parasang, zone offsets
		/// nought to two and a stratum nought to 255, inside 256 characters. Those saves exist.
		/// Judging them by today's canonical grammar would quarantine a founder's real records for
		/// the crime of having been written correctly at the time, which is the opposite of what
		/// quarantine is for.
		/// </para>
		/// <para>
		/// What the older grammar never did is turn prose into a place: "the salt dunes" has no
		/// six dot-separated parts and failed then exactly as it fails now. So this is a wider
		/// door, not an open one, and nothing that was refused before is admitted here.
		/// </para>
		/// </summary>
		public const int MaxLegacyLocatorChars = 256;
		public const int MaxLegacyZoneZ = 255;

		public static bool LegacyFullLocator(string value)
		{
			if (string.IsNullOrEmpty(value) || value.Length > MaxLegacyLocatorChars) return false;
			string[] parts = value.Split('.');
			if (parts.Length != 6 || parts[0].Length == 0) return false;
			int px, py, zx, zy, zz;
			return int.TryParse(parts[1], out px) && int.TryParse(parts[2], out py)
				&& int.TryParse(parts[3], out zx) && int.TryParse(parts[4], out zy)
				&& int.TryParse(parts[5], out zz)
				&& px >= 0 && py >= 0 && zx >= 0 && zx <= MaxZoneX && zy >= 0 && zy <= MaxZoneY
				&& zz >= 0 && zz <= MaxLegacyZoneZ && Utf8Encodable(value);
		}

		/// <summary>
		/// Whether a string can survive the wire at all.
		/// <para>
		/// A lone surrogate is a perfectly ordinary <c>char</c> and a perfectly impossible piece
		/// of text: strict UTF-8 refuses to encode it. Checking here means such a string is
		/// refused while it is still just a candidate, instead of at the moment a book that has
		/// already advanced its revision is asked to write itself out.
		/// </para>
		/// </summary>
		public static bool Utf8Encodable(string value)
		{
			if (value == null) return false;
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if (!char.IsSurrogate(c)) continue;
				if (!char.IsHighSurrogate(c) || i + 1 >= value.Length
					|| !char.IsLowSurrogate(value[i + 1])) return false;
				i++;
			}
			return true;
		}

		/// <summary>
		/// One numeric component: digits only, and inside its real range.
		/// <para>
		/// Parsed by hand because <c>int.TryParse</c> accepts a leading '+', a leading '-' and
		/// surrounding whitespace, and because accumulating digit by digit against the ceiling
		/// means an eleven-digit component is refused rather than overflowed into range.
		/// </para>
		/// <para>
		/// Padding is deliberately <b>not</b> judged here. A component of "010" is a number in
		/// range, and what is wrong with it is that it is not how this value is written; that is a
		/// question about the whole string, and it is answered once, by the reassembly identity
		/// above. Refusing it in both places would leave that identity unreachable, and an
		/// unreachable guard is one nobody can prove still works.
		/// </para>
		/// </summary>
		private static bool Component(string text, int max, out int value)
		{
			value = -1;
			if (string.IsNullOrEmpty(text) || text.Length > 10) return false;
			int parsed = 0;
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (c < '0' || c > '9') return false;
				parsed = parsed * 10 + (c - '0');
				if (parsed > max) return false;
			}
			value = parsed;
			return true;
		}
	}
}
