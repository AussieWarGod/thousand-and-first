using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// The crown: which of the founder's cities is the capital, how that city came to be it, and
	/// what moving it costs.
	/// <para>
	/// <b>The crown is a BUILDING</b> (Addendum 22 A4), and this file never uses the word
	/// <c>Seat</c> for it. <c>Seat</c> is taken, with a different meaning and a different lifetime:
	/// it is the hot/cold swap role, the settlement the founder is presently standing in, and it
	/// exchanges every time they walk between cities (<c>KingdomSystem.TrySeat</c>). A capital that
	/// moved when the founder walked would not be a capital; it would be a cursor. So the capital
	/// is the city where the crown is set down, the crown is a hall the founder RAISED, and the
	/// only thing that moves it is raising another one and going there.
	/// </para>
	/// <para>
	/// <b>Where "which city is the capital" actually lives, and why.</b> One string in the game's
	/// own state (<see cref="RegisterStateKey"/>), exactly as the arches' register and the keepers'
	/// knowledge roster are carried &mdash; because it is a REALM fact that must be readable while
	/// every city but one is dormant, and a serialized field on the seat could not be, while a
	/// field on the settlement record would be a per-city flag that two cities could both hold.
	/// The string is not trusted, though: <see cref="Resolve"/> checks it against the halls that
	/// actually stand and repairs it when the founder has struck their own crown hall down. That is
	/// the whole of the bargain the megastructure derivation makes one lane over
	/// (<c>KingdomZoning.KeptMegastructure</c>): the world is the record, and the register is only a
	/// tie-break for the one question the world cannot answer &mdash; which of two standing halls
	/// the crown is IN.
	/// </para>
	/// <para>
	/// Engine-free, so the whole of it is tabled.
	/// </para>
	/// </summary>
	public static partial class KingdomCrownRules
	{
		/// <summary>
		/// The catalogue key of the hall the crown is set down in. Held here as well as in the XML
		/// for the reason <c>KingdomLabRules</c> holds the lab's four: the arithmetic over which
		/// halls stand is testable and the registry is still the authority on what one costs.
		/// </summary>
		public const string CrownKey = "crownhall";

		/// <summary>
		/// Where the realm's crown is recorded. One string in game state, the same way the arches'
		/// register (<c>KingdomMirrorGateRules.RegisterStateKey</c>) and the keepers' roster
		/// (<c>KingdomZoning.RosterState</c>) are carried, and for the same reason: a dormant city
		/// must be able to be the capital.
		/// </summary>
		public const string RegisterStateKey = "r_TAF_Crown";

		/// <summary>Between the record's two columns &mdash; the city, and the hall's own location
		/// key. The arches' register's separator, deliberately: two records the founder will never
		/// see should not be written in two grammars.</summary>
		public const char FieldSeparator = '^';

		/// <summary>Prefix of a crown hall's own location key, so a hall knows whether the crown is
		/// in IT rather than merely in its city.</summary>
		public const string LocationKeyPrefix = "r_TAF_Crown_";

		/// <summary>Whether a name can be written into the record whole. A name that cannot be
		/// stored is refused rather than escaped: a name the record would give back wrong is worse
		/// than a name it refused to take.</summary>
		public static bool Storable(string Text)
		{
			return !string.IsNullOrEmpty(Text) && Text.IndexOf(FieldSeparator) < 0;
		}

		/// <summary>
		/// The location key a crown hall on this ground publishes itself under. Composed from the
		/// zone and the cell and nothing else, so it survives a reload and so a hall rebuilt on the
		/// ground a ruined one stood on inherits the crown rather than orphaning it &mdash; the
		/// arch's own composition rule, for the same reasons.
		/// </summary>
		/// <returns>Null when the ground could not be named.</returns>
		public static string ComposeLocationKey(string ZoneId, int X, int Y)
		{
			if (string.IsNullOrEmpty(ZoneId) || X < 0 || Y < 0 || ZoneId.IndexOf(FieldSeparator) >= 0)
			{
				return null;
			}
			return LocationKeyPrefix + ZoneId + "_" + X + "," + Y;
		}

		/// <summary>The record as one string, ready to be carried in game state.</summary>
		public static string FormatCrown(string City, string Key)
		{
			if (!Storable(City))
			{
				return "";
			}
			return City + FieldSeparator + (Key ?? "");
		}

		/// <summary>
		/// Reads the record. Untrusted, because a save is untrusted and our own older writing is
		/// untrusted with it.
		/// </summary>
		/// <param name="Text">Record text; null and empty both read as a realm with no capital,
		/// which is every realm until the first crown hall is raised.</param>
		/// <param name="City">The city named, or null.</param>
		/// <param name="Key">The hall named, or the empty string when the record predates one.</param>
		/// <returns>False when there was text and it could not be read, so the caller can repair it
		/// and say so once (STANDARDS 7b).</returns>
		public static bool TryParseCrown(string Text, out string City, out string Key)
		{
			City = null;
			Key = "";
			if (string.IsNullOrEmpty(Text))
			{
				return true;
			}
			string[] columns = Text.Split(FieldSeparator);
			if (columns.Length < 1 || columns.Length > 2 || columns[0].Length == 0)
			{
				return false;
			}
			City = columns[0];
			Key = (columns.Length == 2) ? columns[1] : "";
			return true;
		}

		/// <summary>
		/// Which city is the capital, given what the record claims and which cities actually keep a
		/// standing crown hall.
		/// <para>
		/// <b>The world outranks the record.</b> A founder may strike their own crown hall down
		/// &mdash; nothing in the protection law stops them taking apart a thing they built &mdash;
		/// and a realm that went on calling that city its capital would be lying to itself in a
		/// menu. So the record is only believed when the hall it names is still standing somewhere
		/// in that city; otherwise the standing halls decide, and the record is repaired to match.
		/// </para>
		/// <para>
		/// <b>The tie-break never flickers.</b> When the record is no help and several halls stand,
		/// the answer is the first city in the order the caller passed &mdash; which is name order,
		/// never seat order, because seat order changes every time the founder walks through a door
		/// (END-STATE-CITIES-RESEARCH &sect;5.1) and a capital that changed with it would be the
		/// exact bug that section exists to prevent.
		/// </para>
		/// </summary>
		/// <param name="Registered">The city the record names, from <see cref="TryParseCrown"/>.</param>
		/// <param name="CrownCities">Cities keeping a standing crown hall, in a deterministic order
		/// the caller chose and can defend. Null reads as none.</param>
		/// <param name="Capital">The capital, or null when the realm has none.</param>
		/// <returns>True when the record already agreed with the ground and needs no rewrite.</returns>
		public static bool Resolve(string Registered, IList<string> CrownCities, out string Capital)
		{
			int count = (CrownCities == null) ? 0 : CrownCities.Count;
			if (!string.IsNullOrEmpty(Registered))
			{
				for (int i = 0; i < count; i++)
				{
					if (string.Equals(CrownCities[i], Registered, StringComparison.OrdinalIgnoreCase))
					{
						Capital = CrownCities[i];
						return true;
					}
				}
			}
			if (count == 0)
			{
				Capital = null;
				// Nothing to repair only when nothing was claimed. A record naming a city whose hall
				// is gone is a record that must be rubbed out, and the founder is owed the sentence.
				return string.IsNullOrEmpty(Registered);
			}
			Capital = CrownCities[0];
			return false;
		}

		/// <summary>
		/// Whether the crown may be set down in this hall, and what happens if it is.
		/// <para>
		/// Ordered from the fact nothing can change to the one the founder can answer by walking:
		/// there is no realm, then this is not our ground, then we did not build this, then the
		/// name will not store &mdash; and only then the two answers that are events rather than
		/// refusals.
		/// </para>
		/// </summary>
		/// <param name="Founded">Whether there is a realm at all.</param>
		/// <param name="OurGround">Whether the realm holds the zone this hall stands in.</param>
		/// <param name="OurWork">Whether the settlement raised this hall.</param>
		/// <param name="Crowned">The city that presently keeps the crown, or null when none does.</param>
		/// <param name="Here">The city this hall stands in.</param>
		public static KingdomCrownVerdict JudgeTakeUp(bool Founded, bool OurGround, bool OurWork, string Crowned, string Here)
		{
			if (!Founded)
			{
				return KingdomCrownVerdict.RefusedUnfounded;
			}
			if (!OurGround)
			{
				return KingdomCrownVerdict.RefusedNotOurGround;
			}
			if (!OurWork)
			{
				return KingdomCrownVerdict.RefusedNotOurWork;
			}
			if (!Storable(Here))
			{
				return KingdomCrownVerdict.RefusedNamed;
			}
			if (string.IsNullOrEmpty(Crowned))
			{
				return KingdomCrownVerdict.Crowns;
			}
			return string.Equals(Crowned, Here, StringComparison.OrdinalIgnoreCase)
				? KingdomCrownVerdict.AlreadyHere
				: KingdomCrownVerdict.Moves;
		}
	}
}
