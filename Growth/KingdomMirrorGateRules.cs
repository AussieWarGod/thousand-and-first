using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// What one attempt to key an arch came to.
	/// <para>
	/// A refusal is reserved for something the founder actually asked for and cannot have, so that
	/// the telling means something when it fires &mdash; the same shape
	/// <c>KingdomJoinVerdict</c> keeps one lane over.
	/// </para>
	/// </summary>
	internal enum KingdomGateVerdict : byte
	{
		/// <summary>This end is keyed and stands waiting for a twin in another city.</summary>
		Offered = 0,

		/// <summary>The two ends answer each other. The crossing exists.</summary>
		Joined = 1,

		/// <summary>An arch was taken back out of the register.</summary>
		Released = 2,

		/// <summary>This city already keeps a keyed arch. One crossing to a city: a second arch in
		/// the same place would answer the same ground the first one does.</summary>
		RefusedCityKeyed = 3,

		/// <summary>This very arch is already in the register. Release it before keying it again;
		/// the caller offers that instead of refusing outright.</summary>
		RefusedAlreadyKeyed = 4,

		/// <summary>Nothing to release: this arch was never keyed.</summary>
		RefusedUnkeyed = 5,

		/// <summary>The register will hold no more. Bounded because it is a string the game state
		/// carries, and hostile-input discipline applies to our own writing too.</summary>
		RefusedFull = 6,

		/// <summary>The key or the city could not be written down &mdash; empty, or carrying one of
		/// the register's own separators. Refused rather than escaped: a name that cannot be stored
		/// whole is a name the register would give back wrong.</summary>
		RefusedNamed = 7
	}

	/// <summary>
	/// What became of the standing draw over one span of days.
	/// </summary>
	internal enum KingdomGateHold : byte
	{
		/// <summary>No day boundary was crossed, so nothing was owed and nothing is decided.</summary>
		Unchanged = 0,

		/// <summary>The works paid the whole of it. The arch stands open.</summary>
		Held = 1,

		/// <summary>The works could not pay it. The arch closes and says so.</summary>
		Lost = 2
	}

	/// <summary>
	/// One arch in the realm's register: which gate it is, which city keeps it, and which other
	/// arch it answers.
	/// <para>
	/// <b>The register is the pairing, and that is the whole of the QB-1 seam.</b> An arch cannot
	/// write on its twin, because its twin is usually standing in a zone nobody has loaded; so
	/// neither end stores the other. Both ends store nothing but their own key, and the register
	/// &mdash; one string the game carries, exactly as the keepers' knowledge roster is carried
	/// (<c>KingdomZoning.Roster</c>) &mdash; says who answers whom. Re-keying the realm onto a
	/// capital hub when the crown wave lands is then a rewrite of the <see cref="Partner"/> column
	/// and nothing else: no arch is visited, no arch is rebuilt, and nothing is lost.
	/// </para>
	/// <para>
	/// Frozen, and every transition below returns a new array rather than editing this one.
	/// </para>
	/// </summary>
	internal readonly struct KingdomGateRow
	{
		/// <summary>The game-state key under which this arch publishes its own cell address.
		/// Composed from the ground it stands on (<see cref="KingdomMirrorGateRules.ComposeLocationKey"/>),
		/// so it is the same key after a reload and the same key after a rebuild on the same
		/// cell.</summary>
		internal readonly string Key;

		/// <summary>The city this arch stands in, as the founder names it.</summary>
		internal readonly string City;

		/// <summary>The key of the arch this one answers, or the empty string while it waits.
		/// Never this row's own key.</summary>
		internal readonly string Partner;

		internal KingdomGateRow(string key, string city, string partner)
		{
			Key = key ?? "";
			City = city ?? "";
			Partner = partner ?? "";
		}

		/// <summary>This row with a different partner. Copy-on-write: the old row is untouched.</summary>
		internal KingdomGateRow WithPartner(string partner)
		{
			return new KingdomGateRow(Key, City, partner);
		}
	}

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
	internal static class KingdomMirrorGateRules
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
		internal static string PartnerOf(KingdomGateRow[] rows, string key)
		{
			int at = IndexOfKey(rows, key);
			return (at < 0) ? "" : rows[at].Partner;
		}

		/// <summary>
		/// Keys an arch: enters it in the register, and joins it to whatever is already waiting.
		/// <para>
		/// <b>One keyed arch to a city.</b> A crossing is between two of the founder's cities
		/// (END-STATE-CITIES-RESEARCH &sect;4.4), so a second arch keyed in a city already keeping
		/// one would answer ground the first one already answers. It is refused, and the refusal
		/// names the arch that is in the way.
		/// </para>
		/// <para>
		/// The waiting arch this one joins is the <b>first unpaired row in another city, in
		/// register order</b>. Deterministic without a sort and without a draw: at v1's two cities
		/// there is at most one, and the rule still reads the same when there are more.
		/// </para>
		/// </summary>
		/// <param name="rows">The register as it stands. Not modified.</param>
		/// <param name="key">This arch's location key.</param>
		/// <param name="city">The city it stands in.</param>
		/// <param name="next">The register as it stands afterwards; the input array on any refusal.</param>
		/// <param name="partner">The key this arch was joined to, or the empty string when it is
		/// only waiting.</param>
		/// <returns>The verdict. Never throws.</returns>
		internal static KingdomGateVerdict TryDedicate(KingdomGateRow[] rows, string key, string city, out KingdomGateRow[] next, out string partner)
		{
			next = rows ?? new KingdomGateRow[0];
			partner = "";
			if (!Storable(key) || !Storable(city))
			{
				return KingdomGateVerdict.RefusedNamed;
			}
			if (IndexOfKey(next, key) >= 0)
			{
				return KingdomGateVerdict.RefusedAlreadyKeyed;
			}
			if (IndexOfCity(next, city) >= 0)
			{
				return KingdomGateVerdict.RefusedCityKeyed;
			}
			if (next.Length >= MaxGates)
			{
				return KingdomGateVerdict.RefusedFull;
			}
			int waiting = -1;
			for (int i = 0; i < next.Length; i++)
			{
				if (next[i].Partner.Length == 0 && !string.Equals(next[i].City, city, StringComparison.OrdinalIgnoreCase))
				{
					waiting = i;
					break;
				}
			}
			KingdomGateRow[] built = new KingdomGateRow[next.Length + 1];
			Array.Copy(next, built, next.Length);
			built[next.Length] = new KingdomGateRow(key, city, (waiting >= 0) ? built[waiting].Key : "");
			if (waiting < 0)
			{
				next = built;
				return KingdomGateVerdict.Offered;
			}
			partner = built[waiting].Key;
			built[waiting] = built[waiting].WithPartner(key);
			next = built;
			return KingdomGateVerdict.Joined;
		}

		/// <summary>
		/// Points two arches at each other, whatever they were pointed at before.
		/// <para>
		/// <b>This is the re-keying seam QB-1 names.</b> Pairwise v1 calls it with the two ends of
		/// one crossing. The capital wave calls it once per arch with the capital's own key, and
		/// the realm becomes a hub without a single arch being visited, rebuilt, or forgotten
		/// &mdash; which is why the register carries the pairing and neither arch does.
		/// </para>
		/// <para>
		/// Whatever either end answered before is released in the same breath, on both sides, so
		/// the register can never hold a row pointing at an arch that is pointing somewhere else.
		/// </para>
		/// </summary>
		/// <param name="rows">The register as it stands. Not modified.</param>
		/// <param name="keyA">One arch.</param>
		/// <param name="keyB">The other. Must be a different arch in the register.</param>
		/// <param name="next">The register afterwards; the input array on any refusal.</param>
		/// <returns><see cref="KingdomGateVerdict.Joined"/>, or a refusal naming what was wrong.</returns>
		internal static KingdomGateVerdict TryPair(KingdomGateRow[] rows, string keyA, string keyB, out KingdomGateRow[] next)
		{
			next = rows ?? new KingdomGateRow[0];
			int a = IndexOfKey(next, keyA);
			int b = IndexOfKey(next, keyB);
			if (a < 0 || b < 0)
			{
				return KingdomGateVerdict.RefusedUnkeyed;
			}
			if (a == b)
			{
				// An arch that answered itself would teleport a founder to where they stand, which
				// is not a crossing and would read as a broken one.
				return KingdomGateVerdict.RefusedNamed;
			}
			if (string.Equals(next[a].City, next[b].City, StringComparison.OrdinalIgnoreCase))
			{
				return KingdomGateVerdict.RefusedCityKeyed;
			}
			KingdomGateRow[] built = new KingdomGateRow[next.Length];
			Array.Copy(next, built, next.Length);
			for (int i = 0; i < built.Length; i++)
			{
				if (i == a || i == b)
				{
					continue;
				}
				// Anything that answered either end is unkeyed by the same act, so no third arch is
				// left holding a key that now answers somebody else.
				if (string.Equals(built[i].Partner, keyA, StringComparison.Ordinal) || string.Equals(built[i].Partner, keyB, StringComparison.Ordinal))
				{
					built[i] = built[i].WithPartner("");
				}
			}
			built[a] = built[a].WithPartner(built[b].Key);
			built[b] = built[b].WithPartner(built[a].Key);
			next = built;
			return KingdomGateVerdict.Joined;
		}

		/// <summary>
		/// Re-keys the whole realm onto one city's arch: every other arch answers the capital, and
		/// the capital answers back.
		/// <para>
		/// <b>This is QB-1 cashed in, and it is one pass over one column.</b> The provisional this
		/// register was built under said the hub constraint would be "retrofitted as a re-keying
		/// when the capital exists (a gate re-dedication, not a data loss)". It is: no arch is
		/// visited, no zone is loaded, no row is added and no row is dropped &mdash;
		/// <c>next.Length</c> is always <c>rows.Length</c>, and every row keeps the key and the city
		/// it came in with, in the order it came in. Only <see cref="KingdomGateRow.Partner"/> moves.
		/// </para>
		/// <para>
		/// <b>Cities without arches are untouched, and so is a realm whose capital has none.</b> A
		/// capital that keeps no arch is not a hub, and forcing one would mean either inventing a
		/// row for a building that does not exist or tearing down crossings the founder built and
		/// is still using. So the register is handed back exactly as it stands, and the caller says
		/// so once (STANDARDS 7b) rather than leaving a founder to notice that a political act did
		/// nothing.
		/// </para>
		/// <para>
		/// <b>Why the hub answers ONE spoke rather than all of them.</b> A row carries a single
		/// partner because vanilla's own <c>DestinationKey</c> is a single game-state key: an arch
		/// goes one place. Every spoke landing at the capital is therefore exact, and the capital's
		/// own arch has to pick, so it picks the first spoke in register order &mdash; deterministic
		/// without a sort and without a draw, which is the rule <see cref="TryDedicate"/> already
		/// keeps. At the two cities the roster can hold today a hub IS a pair and nothing is lost;
		/// what a three-city realm should be offered when it steps into the hub is a design question
		/// and is in the backlog, not improvised here.
		/// </para>
		/// <para>
		/// <b>Nothing goes dark.</b> Darkness in this file means the works could not pay
		/// (<see cref="JudgeHold"/>), and inventing a dark span for a political act would be a timer
		/// of our own wearing a costume, which Addendum 8 forbids. The re-key's cost is real and is
		/// paid at once: a crossing the founder built between two other cities now lands somewhere
		/// else, and they are told that before they consent.
		/// </para>
		/// </summary>
		/// <param name="rows">The register as it stands. Not modified.</param>
		/// <param name="CapitalCity">The city keeping the crown.</param>
		/// <param name="next">The register afterwards; the input array on any refusal.</param>
		/// <param name="rekeyed">How many rows changed what they answer. Zero when the realm was
		/// already hubbed here, which is the ordinary case for a re-run.</param>
		/// <param name="hubKey">The capital's own arch, or the empty string on a refusal.</param>
		/// <returns><see cref="KingdomGateVerdict.Joined"/> when the realm is hubbed;
		/// <see cref="KingdomGateVerdict.Offered"/> when the capital's arch is the only one there is;
		/// <see cref="KingdomGateVerdict.RefusedUnkeyed"/> when the capital keeps no arch;
		/// <see cref="KingdomGateVerdict.RefusedNamed"/> when the city could not be read.</returns>
		internal static KingdomGateVerdict TryHub(KingdomGateRow[] rows, string CapitalCity, out KingdomGateRow[] next, out int rekeyed, out string hubKey)
		{
			next = rows ?? new KingdomGateRow[0];
			rekeyed = 0;
			hubKey = "";
			if (!Storable(CapitalCity))
			{
				return KingdomGateVerdict.RefusedNamed;
			}
			int hub = IndexOfCity(next, CapitalCity);
			if (hub < 0)
			{
				return KingdomGateVerdict.RefusedUnkeyed;
			}
			hubKey = next[hub].Key;
			KingdomGateRow[] built = new KingdomGateRow[next.Length];
			Array.Copy(next, built, next.Length);
			string firstSpoke = "";
			for (int i = 0; i < built.Length; i++)
			{
				if (i == hub)
				{
					continue;
				}
				if (firstSpoke.Length == 0)
				{
					firstSpoke = built[i].Key;
				}
				if (!string.Equals(built[i].Partner, hubKey, StringComparison.Ordinal))
				{
					built[i] = built[i].WithPartner(hubKey);
					rekeyed++;
				}
			}
			if (!string.Equals(built[hub].Partner, firstSpoke, StringComparison.Ordinal))
			{
				built[hub] = built[hub].WithPartner(firstSpoke);
				rekeyed++;
			}
			next = built;
			return (firstSpoke.Length == 0) ? KingdomGateVerdict.Offered : KingdomGateVerdict.Joined;
		}

		/// <summary>
		/// What the founder is told when the realm's arches have been re-keyed onto the capital.
		/// Says the NUMBER, because a founder who has just moved a crown wants to know how much of
		/// their road network moved with it, and says it only when something actually moved.
		/// </summary>
		/// <param name="Capital">The city the arches now answer.</param>
		/// <param name="Rekeyed">How many arches changed what they answer.</param>
		internal static string HubbedLine(string Capital, int Rekeyed)
		{
			if (Rekeyed <= 0)
			{
				return "";
			}
			return "{{C|" + Rekeyed + ((Rekeyed == 1) ? " arch is" : " arches are") + " re-keyed. The realm's crossings answer "
				+ Named(Capital) + " now, and not one of them was touched to do it.}}";
		}

		/// <summary>The same moment, dated, for the chronicle.</summary>
		internal static string HubbedTelling(string Capital)
		{
			return "the realm's arches turned to face " + Named(Capital) + ", and every road in the kingdom became a road to one place";
		}

		/// <summary>
		/// STANDARDS 7b for the one way a re-key can do nothing: the capital keeps no arch, so
		/// there is no hub to hang the realm on. Named rather than passed over, because the founder
		/// asked for a thing and would otherwise be left to work out that it did not happen.
		/// </summary>
		internal static string NoArchAtCapitalLine(string Capital)
		{
			return "{{y|" + Named(Capital) + " keeps no arch, so the realm's crossings are left exactly as they were. "
				+ "Raise a mirror-gate in the capital and key it, and every other arch will answer it.}}";
		}

		/// <summary>
		/// Takes an arch back out of the register, and unkeys whatever was answering it.
		/// <para>
		/// The arch itself stands exactly where it stands &mdash; nothing player-placed is destroyed
		/// or moved by any of this (the protection law); the crossing simply stops answering.
		/// </para>
		/// </summary>
		/// <param name="rows">The register as it stands. Not modified.</param>
		/// <param name="key">The arch to unkey.</param>
		/// <param name="next">The register afterwards; the input array on any refusal.</param>
		/// <param name="orphan">The key of the arch left waiting, or the empty string when none
		/// was answering. The caller tells that city, once.</param>
		internal static KingdomGateVerdict TryRelease(KingdomGateRow[] rows, string key, out KingdomGateRow[] next, out string orphan)
		{
			next = rows ?? new KingdomGateRow[0];
			orphan = "";
			int at = IndexOfKey(next, key);
			if (at < 0)
			{
				return KingdomGateVerdict.RefusedUnkeyed;
			}
			KingdomGateRow[] built = new KingdomGateRow[next.Length - 1];
			int kept = 0;
			for (int i = 0; i < next.Length; i++)
			{
				if (i == at)
				{
					continue;
				}
				KingdomGateRow row = next[i];
				if (string.Equals(row.Partner, key, StringComparison.Ordinal))
				{
					orphan = row.Key;
					row = row.WithPartner("");
				}
				built[kept++] = row;
			}
			next = built;
			return KingdomGateVerdict.Released;
		}

		/// <summary>
		/// What an open arch owes for a span of days.
		/// <para>
		/// The full elapsed, uncapped (Addendum 8 clause 1, STANDARDS &sect;8): a crossing left open
		/// across a season costs a season, and there is no forgiveness anywhere in it. What bounds
		/// it is not a ceiling but the city's own salt &mdash; nothing can pay a hundred days of
		/// this out of a store built to hold one, so an arch left open across a long absence is
		/// found dark, and one day's works light it again.
		/// </para>
		/// </summary>
		/// <param name="days">Whole world-day boundaries, from
		/// <c>KingdomProductionRules.TryDaysBetween</c>. Zero or fewer owes nothing.</param>
		/// <returns>Charge owed, saturating rather than wrapping.</returns>
		internal static int DrawForDays(long days)
		{
			if (days <= 0L)
			{
				return 0;
			}
			long owed = days * OpenChargePerDay;
			return (owed > int.MaxValue || owed < 0L) ? int.MaxValue : (int)owed;
		}

		/// <summary>
		/// Whether the works paid what the arch owed.
		/// <para>
		/// A span that crossed no day boundary decides nothing &mdash; an arch is not closed by
		/// being looked at between days &mdash; which is why this has three answers and not two.
		/// </para>
		/// </summary>
		/// <param name="owedCharge">From <see cref="DrawForDays"/>.</param>
		/// <param name="availableCharge">What the arch can actually draw on, measured off the real
		/// vessels rather than assumed (STANDARDS &sect;1).</param>
		internal static KingdomGateHold JudgeHold(int owedCharge, int availableCharge)
		{
			if (owedCharge <= 0)
			{
				return KingdomGateHold.Unchanged;
			}
			return (availableCharge >= owedCharge) ? KingdomGateHold.Held : KingdomGateHold.Lost;
		}

		/// <summary>
		/// The sentence a refused keying is told with. Every one names the fix, and none of them
		/// says "that failed" (STANDARDS 7b).
		/// <para>
		/// Empty for the three verdicts that are not refusals: nothing has gone wrong, and 7b
		/// forbids telling somebody about the absence of a problem.
		/// </para>
		/// </summary>
		/// <param name="verdict">What <see cref="TryDedicate"/>, <see cref="TryPair"/> or
		/// <see cref="TryRelease"/> answered.</param>
		/// <param name="city">The city already keeping an arch, where the verdict names one.</param>
		internal static string RefusalLine(KingdomGateVerdict verdict, string city)
		{
			switch (verdict)
			{
			case KingdomGateVerdict.RefusedCityKeyed:
				return "The arch at " + Named(city) + " already answers for that city. Release it, and this one can take its place.";
			case KingdomGateVerdict.RefusedAlreadyKeyed:
				return "This arch is already keyed.";
			case KingdomGateVerdict.RefusedUnkeyed:
				return "This arch was never keyed, so there is nothing to give up.";
			case KingdomGateVerdict.RefusedFull:
				return "The realm's register will carry no more arches. Release one you no longer cross.";
			case KingdomGateVerdict.RefusedNamed:
				return "This ground cannot be written down, so the crossing cannot be kept honestly. Raise the arch somewhere the realm can name.";
			default:
				return "";
			}
		}

		/// <summary>What the founder is told when this end is keyed and nothing answers it yet.</summary>
		internal static string OfferedLine(string city)
		{
			return "The arch is keyed to " + Named(city) + ". Raise its twin in another of your cities and key that one, and the two become one place.";
		}

		/// <summary>What the founder is told the moment a crossing exists.</summary>
		internal static string JoinedLine(string here, string there)
		{
			return Named(here) + " and " + Named(there) + " stand one step apart.";
		}

		/// <summary>The same moment, dated, for the chronicle.</summary>
		internal static string JoinedTelling(string here, string there)
		{
			return "the arch at " + Named(here) + " found its twin at " + Named(there) + ", and the road between them stopped being a road";
		}

		/// <summary>What the founder is told when a crossing is given up. Said plainly: the arch is
		/// not touched, and a founder who has just spent a hundred and twenty drams on one needs to
		/// hear that in the same breath.</summary>
		internal static string ReleasedLine(string city)
		{
			return "The arch at " + Named(city) + " is unkeyed. It stands exactly where it stands; the crossing simply no longer answers.";
		}

		/// <summary>What the city at the other end is left with. Told once, because a founder who
		/// unkeys one end has silently unkeyed two.</summary>
		internal static string OrphanedLine(string city)
		{
			return "The arch at " + Named(city) + " is left waiting. Nothing answers it now.";
		}

		/// <summary>
		/// STANDARDS 7b, for the one stall this building can have: the works could not hold it
		/// open. Names the arrest rather than only the doom.
		/// </summary>
		internal static string WentDarkLine(string city)
		{
			return "The arch at " + Named(city) + " has gone dark. It draws " + OpenChargePerDay
				+ " charge a day to stand open and the works could not pay it; another wheel, or a deeper bed of salt, and it lights again.";
		}

		/// <summary>The same, dated, for the chronicle.</summary>
		internal static string WentDarkTelling(string city, string realm)
		{
			return "the arch at " + Named(city) + " went dark, and " + Named(realm) + " was two journeys wide again";
		}

		/// <summary>The line the arch carries in its own description, so the state of the crossing
		/// is legible where a founder actually looks for it.</summary>
		/// <param name="keyed">Whether this arch is in the register at all.</param>
		/// <param name="answering">The city at the other end, or null when nothing answers.</param>
		/// <param name="dark">Whether the works failed to hold it open.</param>
		internal static string DescriptionLine(bool keyed, string answering, bool dark)
		{
			if (!keyed)
			{
				return "\n{{rules|Unkeyed. It draws nothing and goes nowhere.}}";
			}
			if (string.IsNullOrEmpty(answering))
			{
				return "\n{{rules|Keyed, and waiting for a twin in another city. It draws nothing until one answers.}}";
			}
			if (dark)
			{
				return "\n{{r|Dark. It answers " + Named(answering) + " when the works can pay the " + OpenChargePerDay + " charge a day it wants.}}";
			}
			return "\n{{G|Open onto " + Named(answering) + ", and drawing " + OpenChargePerDay + " charge a day to stay that way.}}";
		}

		/// <summary>
		/// What the founder is asked before a keying, and it is the whole of the cost, disclosed
		/// before anything is committed &mdash; the shape the Charter's own dedications keep.
		/// </summary>
		internal static string DedicationPrompt(string city)
		{
			return "Key this arch for " + Named(city) + "?\n\nWhile it answers another city it draws {{C|" + OpenChargePerDay
				+ "}} charge a day from this city's works, whether anyone crosses or not. Crossing costs nothing beyond that. When the works cannot pay it the arch goes dark, and lights again the day they can.";
		}

		/// <summary>A city as a founder would say it, or an honest word when the realm never named
		/// one.</summary>
		internal static string Named(string city)
		{
			return string.IsNullOrEmpty(city) ? "the city" : city.Trim();
		}
	}
}
