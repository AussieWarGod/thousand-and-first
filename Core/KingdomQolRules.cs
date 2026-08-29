using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>What came of asking whether one person will live in one place.</summary>
	public enum QolVerdict
	{
		/// <summary>Every need is met and nothing there is refused. The ordinary answer, and the
		/// answer for a resident who asks nothing of a place at all.</summary>
		Match = 0,

		/// <summary>Something the resident cannot do without is not there. The match does not
		/// happen, and the tag that would lift it is named (STANDARDS 7b).</summary>
		NeedUnmet = 1,

		/// <summary>Something the resident will not live beside IS there. Also no match, and also
		/// named &mdash; but this one is not a thing the settlement can go and build.</summary>
		Refused = 2
	}

	/// <summary>
	/// What one resident asks of the place they are put: the tags they cannot do without, the tags
	/// that would please them, and the tags they will not live beside. Plus vanilla species and the
	/// two derived facts the settlement's own food and water accounting wants to know about them.
	/// <para>
	/// Every list is a set of folded tag strings and is never null. Nothing here is a level, a
	/// meter, or a score: a profile is a set of <em>placement constraints</em>, read fresh every
	/// time it is asked for, and it holds no history of how well anyone has been housed.
	/// </para>
	/// </summary>
	public sealed class QolProfile
	{
		/// <summary>The resident's vanilla species. Kept beside the placement tags so the existing
		/// QoL/cohabitation lane can expose <c>species:&lt;name&gt;</c> without a species table.</summary>
		public string Species;

		/// <summary>Hard. A place that does not provide all of these is not a place this resident
		/// moves into, and no job there is theirs either.</summary>
		public string[] Needs;

		/// <summary>Soft. Met ones shade the settlement's equilibrium up by a small capped amount
		/// through the tastes machinery; unmet ones mean the resident's default and never a
		/// penalty (VISION: an unmet Prefers is a default, never a penalty).</summary>
		public string[] Prefers;

		/// <summary>Hard and negative. A place that provides any of these is refused, however
		/// well it meets the needs.</summary>
		public string[] Refuses;

		/// <summary>Whether this resident draws on the settlement's larder at all.</summary>
		public bool EatsFood;

		/// <summary>Whether this resident drinks the settlement's water.</summary>
		public bool DrinksWater;

		/// <summary>An ordinary person: asks nothing in particular, eats and drinks. What every
		/// settler in every settlement raised before this vocabulary existed reads as.</summary>
		public static QolProfile Ordinary
		{
			get
			{
				return new QolProfile
				{
					Species = "",
					Needs = KingdomQolRules.NoTags,
					Prefers = KingdomQolRules.NoTags,
					Refuses = KingdomQolRules.NoTags,
					EatsFood = true,
					DrinksWater = true
				};
			}
		}
	}

	/// <summary>
	/// What the engine already knows about a creature, read off parts and tags vanilla itself
	/// reads, and handed to <see cref="KingdomQolRules.Derive"/> as plain booleans so the
	/// derivation table is testable without a running game.
	/// <para>
	/// Each field names the exact vanilla read that fills it; <c>KingdomQol</c> is the half that
	/// performs those reads. <c>default(ResidentTruth)</c> is a creature with no stomach and no
	/// special condition &mdash; true to what the fields say, and never what a real creature
	/// produces, because the engine half fills every field from the object.
	/// </para>
	/// </summary>
	public struct ResidentTruth
	{
		/// <summary><c>GameObject.GetSpecies()</c>. Qud guarantees a fallback to the stripped short
		/// display name, so every real creature supplies an open, mod-extensible body identity.</summary>
		public string Species;

		/// <summary><c>HasPart&lt;Robot&gt;()</c> or the <c>Robot</c> tag/property. Vanilla reads
		/// both: <c>Stomach.IsFamished()</c> short-circuits on <c>HasPropertyOrTag("Robot")</c>,
		/// <c>Effects/Asleep.cs</c> on <c>HasTag("Robot")</c>, and a dozen Sifrah and damage paths
		/// on the part.</summary>
		public bool Robot;

		/// <summary><c>HasPart&lt;Aquatic&gt;()</c> or <c>Brain.Aquatic</c>. The confining one is
		/// the Brain flag: <c>Brain.LimitToAquatic()</c> makes every step check
		/// <c>Cell.HasAquaticSupportFor</c>, so such a creature genuinely cannot cross dry
		/// ground.</summary>
		public bool Aquatic;

		/// <summary><c>GameObject.IsFlying</c>. Vanilla's own exemption:
		/// <c>Brain.LimitToAquatic()</c> returns false for a flier, so a flying aquatic creature is
		/// not water-bound and must not be housed as if it were.</summary>
		public bool Flying;

		/// <summary><c>HasTagOrProperty("Gigantic")</c>. Vanilla's body/equipment scale signal makes
		/// broad portals and turning clearance appropriate. It is a capability condition, not a
		/// species whitelist.</summary>
		public bool BroadBodied;

		/// <summary><c>HasTagOrProperty("LiveFungus")</c> &mdash; vanilla's own fungal read, used
		/// by <c>GameObject.IsAlive</c> and <c>BodyPartCategory</c>, and carried by
		/// <c>BaseFungus</c> and everything that inherits it, ours or another mod's.</summary>
		public bool Fungal;

		/// <summary><c>HasPart&lt;PhotosyntheticSkin&gt;()</c>. Its own
		/// <c>HasSunlight =&gt; IsUnderSky() &amp;&amp; IsDay()</c> is the whole reason this
		/// matters to a roof.</summary>
		public bool Photosynthetic;

		/// <summary><c>HasPart&lt;Inorganic&gt;()</c>, or <c>Physics.Organic == false</c> which is
		/// what <c>GameObject.IsOrganic</c> reads. Vanilla's <c>Robot</c> base object sets exactly
		/// that.</summary>
		public bool Inorganic;

		/// <summary><c>HasPart&lt;Stomach&gt;()</c>. The general read for "does this thing eat":
		/// vanilla's <c>Robot</c> base object carries <c>&lt;removepart Name="Stomach"/&gt;</c>,
		/// so a creature from any mod that likewise has no stomach is correctly not fed.</summary>
		public bool HasStomach;

		/// <summary>An ordinary person: a stomach and nothing else remarkable.</summary>
		public static ResidentTruth Person
		{
			get
			{
				ResidentTruth truth = default(ResidentTruth);
				truth.HasStomach = true;
				return truth;
			}
		}
	}

	/// <summary>
	/// The quality-of-life vocabulary (BUILDING-CATALOGUE-BRIEF.md, Addendum 4): one open set of
	/// namespaced tag strings that replaces three private systems. Buildings <b>Provide</b> tags;
	/// residents <b>Need</b> them (hard), <b>Prefer</b> them (soft), or <b>Refuse</b> them (hard
	/// and negative). This file is the whole of the matching, and it is engine-free; the reads
	/// against real creatures and real registries are <c>KingdomQol</c>, beside it.
	/// <para>
	/// <b>Derive before authoring.</b> The resident half is filled first from vanilla truth
	/// (<see cref="Derive"/>): a robot from any mod needs charge and not food because vanilla's own
	/// <c>Robot</c> object removes its stomach; a water-bound creature needs open water because
	/// <c>Brain.LimitToAquatic</c> will not let it walk; a fungus wants damp and dark because
	/// <c>LiveFungus</c> says what it is; a photosynthetic settler needs sky because
	/// <c>PhotosyntheticSkin.HasSunlight</c> asks for it by name. A modded creature is therefore a
	/// correct resident before its author has written a single tag of ours, and
	/// <c>r_TAF_Needs</c>/<c>r_TAF_Prefers</c>/<c>r_TAF_Refuses</c> on the blueprint only
	/// <em>refine</em> that (<see cref="Refine"/>).
	/// </para>
	/// <para>
	/// <b>Pillar guards.</b> Placement constraints, never meters. An unmet Need means the match
	/// does not happen and is <em>named</em> (STANDARDS 7b, <see cref="RefusalLine"/>); it is not a
	/// penalty applied to anybody. An unmet Prefers is simply the resident's default. Nothing in
	/// this file accumulates, decays, or remembers: every answer is computed from the tags in hand,
	/// so a city that houses people badly is one certain people pass through, not a punished one.
	/// </para>
	/// <para>
	/// <b>Unknown tags are inert.</b> A tag nobody consumes is not an error and never will be: a
	/// mod may ship <c>Provides="theirmod:hearthfire"</c> years before anything needs it, and a
	/// resident may need a tag no building in this game provides &mdash; which refuses those
	/// buildings by name, and says which tag, rather than failing silently or at load.
	/// </para>
	/// </summary>
	public static partial class KingdomQolRules
	{
		// --- The vocabulary ------------------------------------------------------------------

		/// <summary>Our own namespace. Every tag this mod ships is prefixed with it, and every
		/// other mod is asked for the same courtesy, so two mods that both mean "damp" can decide
		/// to agree rather than collide by accident.</summary>
		public const string Namespace = "taf:";

		/// <summary>Somewhere to draw charge. What a robot resident cannot do without, and what
		/// the charging post exists to give (<c>KingdomPowerRules</c>).</summary>
		public const string TagCharge = "taf:charge";

		/// <summary>Open water at the door: a reservoir, a river-side plot, a flooded cellar.
		/// </summary>
		public const string TagOpenWater = "taf:openwater";

		/// <summary>Damp: a cellar, a cistern room, a fungal bed.</summary>
		public const string TagDamp = "taf:damp";

		/// <summary>Out of the sun. Derived by any tier that encloses, and by every tier
		/// underground, where the hill encloses on the settlement's behalf
		/// (<see cref="ProvidedByRoof(KingdomPlotRules.RoofState, bool)"/>).</summary>
		public const string TagDark = "taf:dark";

		/// <summary>Open sky overhead. Derived by any tier weather reaches under, canvas
		/// included &mdash; and only above ground, because weather reaches nothing under
		/// rock.</summary>
		public const string TagSky = "taf:sky";

		/// <summary>A room away from the noise of the day &mdash; the same want the housing taste
		/// states in prose.</summary>
		public const string TagQuiet = "taf:quiet";

		/// <summary>The tags this mod itself ships, in the order they are documented. Nothing is
		/// restricted to this list: it is what <see cref="TagPhrase"/> can put into a sentence and
		/// what MODDING.md promises to keep meaning the same thing.</summary>
		public static readonly string[] OwnTags = new string[6]
		{
			TagCharge, TagOpenWater, TagDamp, TagDark, TagSky, TagQuiet
		};

		/// <summary>An empty tag set. Shared, and never handed out to be written into: every
		/// method here builds a new array rather than editing one it was given.</summary>
		public static readonly string[] NoTags = new string[0];

		/// <summary>The blueprint tag a creature's hard requirements are authored on.</summary>
		public const string NeedsTagName = "r_TAF_Needs";

		/// <summary>The blueprint tag a creature's soft wants are authored on.</summary>
		public const string PrefersTagName = "r_TAF_Prefers";

		/// <summary>The blueprint tag a creature's refusals are authored on.</summary>
		public const string RefusesTagName = "r_TAF_Refuses";

		/// <summary>The blueprint tag a creature's own household conditions are authored on, for
		/// the rare case where what a resident brings to a room is not simply what they need
		/// (<see cref="HouseholdProvides"/>).</summary>
		public const string ProvidesTagName = "r_TAF_Provides";

		/// <summary>The <c>&lt;building&gt;</c> attribute buildings declare their tags on.
		/// </summary>
		public const string ProvidesAttribute = "Provides";

		/// <summary>Separator between tags in every one of these lists.</summary>
		public const char ListSeparator = ',';

		/// <summary>A tag written with this in front of it in an authored refinement REMOVES the
		/// derived tag of that name instead of adding one. The escape hatch a mod needs when its
		/// own species genuinely contradicts the derivation &mdash; a photosynthetic people who
		/// live indoors quite happily &mdash; and the only way to argue with
		/// <see cref="Derive"/>.</summary>
		public const char RemovePrefix = '-';

		/// <summary>Roster/QoL namespace for Qud's open species vocabulary.</summary>
		public const string SpeciesNamespace = "species:";

		/// <summary>Longest species identity carried by this lane. Matches the live roster receipt.</summary>
		public const int MaxSpeciesLength = 128;

		// --- Folding and parsing --------------------------------------------------------------

		/// <summary>
		/// One tag as everything here compares it: trimmed and lower-cased, with null and
		/// whitespace both folding to the empty string. Invariant case on purpose &mdash; a Turkish
		/// locale must not make <c>taf:Illumination</c> stop matching itself.
		/// </summary>
		public static string Fold(string Tag)
		{
			return string.IsNullOrWhiteSpace(Tag) ? "" : Tag.Trim().ToLowerInvariant();
		}

		/// <summary>Whether a tag carries a namespace at all. Advisory only: an un-namespaced tag
		/// works exactly as well as any other, and is merely likelier to collide with somebody
		/// else's idea of the same word.</summary>
		public static bool IsNamespaced(string Tag)
		{
			string tag = Fold(Tag);
			int colon = tag.IndexOf(':');
			return colon > 0 && colon < tag.Length - 1;
		}

		/// <summary>
		/// Reads a comma list of tags. Never fails and never reports: blanks are dropped, repeats
		/// are dropped, everything is folded, and an unknown tag is kept exactly as written because
		/// somebody else's vocabulary is not ours to refuse.
		/// </summary>
		/// <param name="Source">The raw attribute or tag value. Null is an empty set.</param>
		/// <returns>A fresh array, in the order written. Never null.</returns>
	}
}
