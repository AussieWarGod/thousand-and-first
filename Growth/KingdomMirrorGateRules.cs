using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// The mirror-gate's engine-free rules: what an arch costs to hold open, who answers whom, and
	/// every sentence the crossing says to the founder.
	/// <para>
	/// <b>Why there is a rules file at all for a rendering.</b> The crossing itself is vanilla's
	/// &mdash; <c>TeleporterPair</c> ships the keys, the hostiles check and the zone teleport, and
	/// Addendum 11(c) says inherit and extend rather than reinvent. What vanilla has no opinion
	/// about is the part this mod added: an arch is a settlement work with a standing draw on the
	/// 12(g) power lane, and a brownout closes it. That arithmetic and that vocabulary are here,
	/// where they can be tabled.
	/// </para>
	/// <para>
	/// The engine-coupled half is <c>r_KingdomMirrorGate</c> and <c>KingdomMirrorGate</c>, in the
	/// same folder, exactly as <c>KingdomPowerRules</c> sits beside <c>KingdomPower</c>.
	/// </para>
	/// </summary>
	internal static partial class KingdomMirrorGateRules
	{
		/// <summary>
		/// What an open arch costs its own city in a day, in vanilla charge units.
		/// <para>
		/// Three charging posts' worth &mdash; <c>KingdomPowerRules.PostDailyNeedCharge</c> is one
		/// cradle-full and is the unit the settlement's whole power report is written in, so the
		/// price of the crossing is stated in the only currency the founder already reads. At the
		/// shipped ratings that is two water wheels and a crank mill turning all day for one arch,
		/// and both ends pay their own: a crossing wants power in BOTH cities or it wants neither.
		/// </para>
		/// <para>
		/// Sanity against vanilla's own gate, which is where the figure has to answer: the ring
		/// gate spends <c>ChargeUse="50000"</c> on a single traversal
		/// (<c>B/ObjectBlueprints/Furniture.xml:2326</c>). Ours stands open all day for a quarter
		/// of one of those steps, and charges nothing at all for a step, because Addendum 22 A2
		/// rules the draw the whole price of the crossing and forbids a second toll. A settlement
		/// arch between two of your own cities is a smaller thing than a Spindle-grade secant, and
		/// it is priced like one.
		/// </para>
		/// </summary>
		internal const int OpenChargePerDay = 3 * KingdomPowerRules.PostDailyNeedCharge;

		/// <summary>
		/// How many arches the realm's register will carry. Not a design statement about how many
		/// crossings a realm may hold &mdash; the one-arch-per-city rule below is that statement
		/// &mdash; but a bound on a string, because a register that grew without limit would be a
		/// game-state value that grew without limit.
		/// </summary>
		internal const int MaxGates = 8;

		/// <summary>Where the realm's register is carried. One string in the game's own state, the
		/// same way the keepers' knowledge roster is (<c>KingdomZoning.RosterState</c>), because
		/// both are realm facts that must be readable while every city but one is dormant.</summary>
		internal const string RegisterStateKey = "r_TAF_MirrorGates";

		/// <summary>Prefix of every arch's own game-state key. Vanilla writes a cell address under
		/// this key and reads it back at the far end (<c>TeleporterPair.SyncLocation</c> /
		/// <c>AttemptTeleport</c>, <c>D/XRL/World/Parts/TeleporterPair.cs:213-222, :166</c>); ours
		/// only has to be unique and stable.</summary>
		internal const string LocationKeyPrefix = "r_TAF_MirrorGate_";

		/// <summary>Between rows.</summary>
		internal const char RowSeparator = '|';

		/// <summary>Between a row's three columns.</summary>
		internal const char FieldSeparator = '^';

		internal const string UnkeyedLine =
			"This arch answers nothing yet. Key it, and key its twin in another of your cities, and the road between them stops being a road.";

		internal const string DarkLine =
			"The arch stands dark. There is nothing in it but the far wall.";

		internal const string NoPowerLine =
			"This settlement keeps no power at all, so nothing holds the arch open.";

		internal const string NotOurGroundLine =
			"An arch is keyed on the kingdom's own ground, not in other people's houses.";

		internal const string NotOurWorkLine =
			"The settlement never raised this. Adopt it first, and then it can be keyed.";

		/// <summary>
		/// The game-state key an arch on this ground publishes its address under. Composed from the
		/// zone and the cell and nothing else, so it survives a reload, and so an arch rebuilt on
		/// the cell a ruined one stood on inherits the crossing rather than orphaning it.
		/// </summary>
		/// <param name="zoneId">Zone the arch stands in.</param>
		/// <param name="x">Cell column.</param>
		/// <param name="y">Cell row.</param>
		/// <returns>A key, or null when the zone id is missing or carries a separator this register
		/// could not store it beside.</returns>
		internal static string ComposeLocationKey(string zoneId, int x, int y)
		{
			if (string.IsNullOrEmpty(zoneId) || x < 0 || y < 0)
			{
				return null;
			}
			if (zoneId.IndexOf(RowSeparator) >= 0 || zoneId.IndexOf(FieldSeparator) >= 0)
			{
				return null;
			}
			return LocationKeyPrefix + zoneId + "_" + x + "," + y;
		}

		/// <summary>Inverse of <see cref="ComposeLocationKey"/> for physical connection proof.</summary>
		internal static bool TryParseLocationKey(string Key, out string ZoneId,
			out int X, out int Y)
		{
			ZoneId = null;
			X = Y = -1;
			if (string.IsNullOrEmpty(Key)
				|| !Key.StartsWith(LocationKeyPrefix, StringComparison.Ordinal)) return false;
			int comma = Key.LastIndexOf(',');
			int split = comma < 0 ? -1 : Key.LastIndexOf('_', comma);
			if (split <= LocationKeyPrefix.Length || comma <= split + 1
				|| comma >= Key.Length - 1) return false;
			string zone = Key.Substring(LocationKeyPrefix.Length,
				split - LocationKeyPrefix.Length);
			if (!int.TryParse(Key.Substring(split + 1, comma - split - 1), out int x)
				|| !int.TryParse(Key.Substring(comma + 1), out int y) || x < 0 || y < 0
				|| ComposeLocationKey(zone, x, y) != Key) return false;
			ZoneId = zone;
			X = x;
			Y = y;
			return true;
		}

		/// <summary>Whether a name can be stored in the register whole.</summary>
		internal static bool Storable(string text)
		{
			return !string.IsNullOrEmpty(text)
				&& text.IndexOf(RowSeparator) < 0
				&& text.IndexOf(FieldSeparator) < 0;
		}

		/// <summary>
		/// Reads the register. Untrusted, because a save is untrusted and our own older writing is
		/// untrusted with it.
		/// <para>
		/// An unreadable row is <b>dropped and counted</b> rather than taken as a reason to throw
		/// the whole register away: one corrupt row must not cost the founder a crossing that is
		/// standing perfectly well at the other end. The count is reported so the caller can say so
		/// once (STANDARDS 7b) instead of losing it in silence.
		/// </para>
		/// </summary>
		/// <param name="text">Register text; null and empty both read as no arches at all, which is
		/// the ordinary state of a realm that has never keyed one.</param>
		/// <param name="rows">Rows in register order. Never null.</param>
		/// <param name="dropped">Rows that could not be read.</param>
		/// <returns>True when nothing was dropped.</returns>
		internal static bool TryParseRegister(string text, out KingdomGateRow[] rows, out int dropped)
		{
			dropped = 0;
			if (string.IsNullOrEmpty(text))
			{
				rows = new KingdomGateRow[0];
				return true;
			}
			string[] parts = text.Split(RowSeparator);
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
				read[kept++] = new KingdomGateRow(columns[0], columns[1], columns[2]);
			}
			rows = new KingdomGateRow[kept];
			Array.Copy(read, rows, kept);
			return dropped == 0;
		}

		/// <summary>The register as one string, ready to be carried in game state.</summary>
		internal static string FormatRegister(KingdomGateRow[] rows)
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

		/// <summary>Index of an arch by key, or -1.</summary>
		internal static int IndexOfKey(KingdomGateRow[] rows, string key)
		{
			return IndexOfKey(rows, (rows == null) ? 0 : rows.Length, key);
		}

		private static int IndexOfKey(KingdomGateRow[] rows, int count, string key)
		{
			if (rows == null || string.IsNullOrEmpty(key))
			{
				return -1;
			}
			for (int i = 0; i < count && i < rows.Length; i++)
			{
				if (string.Equals(rows[i].Key, key, StringComparison.Ordinal))
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>Index of the arch a city keeps, or -1. Cities are matched the way a founder
		/// reads them &mdash; case-insensitively &mdash; because a city renamed in different case is
		/// the same city.</summary>
		internal static int IndexOfCity(KingdomGateRow[] rows, string city)
		{
			if (rows == null || string.IsNullOrEmpty(city))
			{
				return -1;
			}
			for (int i = 0; i < rows.Length; i++)
			{
				if (string.Equals(rows[i].City, city, StringComparison.OrdinalIgnoreCase))
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>The key of whatever this arch answers, or the empty string. What every gate
		/// reads to fill its own <c>DestinationKey</c>, and the only thing it ever reads.</summary>
	}
}
