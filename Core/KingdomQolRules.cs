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
	/// that would please them, and the tags they will not live beside. Plus the two derived facts
	/// the settlement's own food and water accounting wants to know about them.
	/// <para>
	/// Every list is a set of folded tag strings and is never null. Nothing here is a level, a
	/// meter, or a score: a profile is a set of <em>placement constraints</em>, read fresh every
	/// time it is asked for, and it holds no history of how well anyone has been housed.
	/// </para>
	/// </summary>
	public sealed class QolProfile
	{
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
	public static class KingdomQolRules
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
		public static string[] ParseTags(string Source)
		{
			if (string.IsNullOrEmpty(Source) || Source.Trim().Length == 0)
			{
				return NoTags;
			}
			List<string> tags = new List<string>();
			string[] parts = Source.Split(ListSeparator);
			for (int i = 0; i < parts.Length; i++)
			{
				string tag = Fold(parts[i]);
				if (tag.Length == 0 || (tag.Length == 1 && tag[0] == RemovePrefix))
				{
					continue;
				}
				if (!tags.Contains(tag))
				{
					tags.Add(tag);
				}
			}
			return (tags.Count == 0) ? NoTags : tags.ToArray();
		}

		/// <summary>Whether a set holds a tag. A null or empty set holds nothing; a blank tag is
		/// held by nothing.</summary>
		public static bool Has(string[] Set, string Tag)
		{
			string tag = Fold(Tag);
			if (Set == null || tag.Length == 0)
			{
				return false;
			}
			for (int i = 0; i < Set.Length; i++)
			{
				if (Fold(Set[i]) == tag)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Folds an authored refinement into a derived list: a plain tag is added, a tag written
		/// <c>-like-this</c> removes the derived tag of that name, and anything the refinement does
		/// not mention survives untouched.
		/// <para>
		/// Order inside the result is derived-first, then whatever the refinement added, so a
		/// reader of the log sees where each tag came from.
		/// </para>
		/// </summary>
		/// <param name="Derived">The list <see cref="Derive"/> produced. Null is empty.</param>
		/// <param name="Refinement">The authored list. Null or empty changes nothing.</param>
		/// <returns>A fresh array; neither argument is modified.</returns>
		public static string[] Merge(string[] Derived, string[] Refinement)
		{
			List<string> merged = new List<string>();
			if (Derived != null)
			{
				for (int i = 0; i < Derived.Length; i++)
				{
					string tag = Fold(Derived[i]);
					if (tag.Length != 0 && !merged.Contains(tag))
					{
						merged.Add(tag);
					}
				}
			}
			if (Refinement == null)
			{
				return (merged.Count == 0) ? NoTags : merged.ToArray();
			}
			for (int i = 0; i < Refinement.Length; i++)
			{
				string tag = Fold(Refinement[i]);
				if (tag.Length == 0)
				{
					continue;
				}
				if (tag[0] == RemovePrefix)
				{
					merged.Remove(tag.Substring(1));
					continue;
				}
				if (!merged.Contains(tag))
				{
					merged.Add(tag);
				}
			}
			return (merged.Count == 0) ? NoTags : merged.ToArray();
		}

		// --- Derive before authoring ----------------------------------------------------------

		/// <summary>
		/// The derivation table: what the game already knows about a creature, turned into what
		/// that creature asks of a place to live. The whole of the "correct resident before its
		/// author writes one tag" promise.
		/// <list type="bullet">
		/// <item><b>Robot</b> needs <see cref="TagCharge"/> and eats nothing. Vanilla's own
		/// <c>Robot</c> object removes the <c>Stomach</c> part and sets <c>Physics Organic=false</c>,
		/// and <c>Stomach.IsFamished()</c> returns false for anything carrying the <c>Robot</c>
		/// tag or property, so "does not eat" is read twice from the game and agreed both
		/// times.</item>
		/// <item><b>Water-bound</b> (and not flying) needs <see cref="TagOpenWater"/>, because
		/// <c>Brain.LimitToAquatic</c> makes every step it takes require a cell with aquatic
		/// support. Flying cancels it, exactly as that method does.</item>
		/// <item><b>Fungal</b> needs <see cref="TagDamp"/> and prefers <see cref="TagDark"/>. The
		/// need is the fungus's condition; the dark is a want, so a fungal settler can still take
		/// a lit room and simply be no happier for it.</item>
		/// <item><b>Photosynthetic</b> needs <see cref="TagSky"/>, which is
		/// <c>PhotosyntheticSkin.HasSunlight</c> stated as a placement constraint. Canvas admits
		/// sky (<c>KingdomPlotRules.AdmitsSky</c>), so a tent houses them, a sealed room does not,
		/// and nothing underground does whatever its roof says.</item>
		/// <item><b>Inorganic</b> eats and drinks nothing, whatever parts it happens to carry:
		/// <c>GameObject.IsAlive</c> requires <c>IsOrganic</c>, and the food and water ladders are
		/// a living body's.</item>
		/// </list>
		/// <para>
		/// Nothing here derives a <c>Refuses</c>. A refusal is a person's own line and is either
		/// authored on the blueprint or, for the ideological cases, read off the engine's faction
		/// feelings (<c>KingdomLodgingRules.Conflicts</c> against
		/// <c>KingdomLodgingRules.RefusalHostility</c> &mdash; the ladder, not the superseded flat
		/// floor <see cref="JudgeCohabitation"/> still applies) &mdash; it is not something a body
		/// plan implies.
		/// </para>
		/// </summary>
		/// <param name="Truth">What <c>KingdomQol</c> read off the creature.</param>
		/// <returns>A fresh profile. Never null, and never shares its arrays with another
		/// profile.</returns>
		public static QolProfile Derive(ResidentTruth Truth)
		{
			List<string> needs = new List<string>();
			List<string> prefers = new List<string>();
			if (Truth.Robot)
			{
				needs.Add(TagCharge);
			}
			if (Truth.Aquatic && !Truth.Flying)
			{
				needs.Add(TagOpenWater);
			}
			if (Truth.Fungal)
			{
				needs.Add(TagDamp);
				prefers.Add(TagDark);
			}
			if (Truth.Photosynthetic)
			{
				needs.Add(TagSky);
			}
			bool feeds = Truth.HasStomach && !Truth.Robot && !Truth.Inorganic;
			return new QolProfile
			{
				Needs = (needs.Count == 0) ? NoTags : needs.ToArray(),
				Prefers = (prefers.Count == 0) ? NoTags : prefers.ToArray(),
				Refuses = NoTags,
				EatsFood = feeds,
				DrinksWater = feeds
			};
		}

		/// <summary>
		/// Applies a creature blueprint's authored refinement to a derived profile. Each list
		/// merges by <see cref="Merge"/>, so authoring adds to the derivation rather than
		/// replacing it, and a mod that disagrees with a derived tag removes it by name with the
		/// <see cref="RemovePrefix"/>.
		/// <para>
		/// Vanilla's blueprint tags are a dictionary, so a child blueprint that re-declares
		/// <c>r_TAF_Needs</c> overrides its parent's whole string rather than appending to it. That
		/// is the game's mechanism and it is left alone; the removal prefix is what makes it
		/// workable, and MODDING.md says so.
		/// </para>
		/// </summary>
		/// <param name="Derived">From <see cref="Derive"/>. Null reads as
		/// <see cref="QolProfile.Ordinary"/>.</param>
		/// <param name="Needs">Raw <c>r_TAF_Needs</c>.</param>
		/// <param name="Prefers">Raw <c>r_TAF_Prefers</c>.</param>
		/// <param name="Refuses">Raw <c>r_TAF_Refuses</c>.</param>
		/// <returns>A fresh profile; the argument is not modified.</returns>
		public static QolProfile Refine(QolProfile Derived, string Needs, string Prefers, string Refuses)
		{
			QolProfile derived = Derived ?? QolProfile.Ordinary;
			return new QolProfile
			{
				Needs = Merge(derived.Needs, ParseTags(Needs)),
				Prefers = Merge(derived.Prefers, ParseTags(Prefers)),
				Refuses = Merge(derived.Refuses, ParseTags(Refuses)),
				EatsFood = derived.EatsFood,
				DrinksWater = derived.DrinksWater
			};
		}

		/// <summary>
		/// What sharing a roof with this resident does to the room, when the blueprint has not said
		/// otherwise: a household keeps the conditions its people need. The fungal settler's cellar
		/// is damp, the water-bound one's is flooded, the robot's has a cradle in it.
		/// <para>
		/// This is what makes cohabitation one rule rather than a second system &mdash; a
		/// neighbour's household is judged with exactly the same <see cref="Judge"/> a building is.
		/// </para>
		/// </summary>
		/// <param name="Profile">The resident. Null provides nothing.</param>
		/// <param name="Authored">Raw <c>r_TAF_Provides</c> from the blueprint, which refines this
		/// the same way everything else refines: adds, and removes with the prefix.</param>
		public static string[] HouseholdProvides(QolProfile Profile, string Authored = null)
		{
			string[] derived = (Profile == null) ? NoTags : Profile.Needs;
			return Merge(derived, ParseTags(Authored));
		}

		// --- The building's side --------------------------------------------------------------

		/// <summary>
		/// What a tier's roof provides on its own, before any author writes a <c>Provides</c>: sky
		/// for anything weather reaches under, dark for anything it does not. Read straight off
		/// <c>KingdomPlotRules.AdmitsSky</c>, so canvas gives sky and a carved room does not, and
		/// the two can never drift apart.
		/// <para>
		/// <b>Sky is weather-reach, and weather reaches nothing under rock.</b> Underground every
		/// roof reads dark, the open plot included. That is not a contradiction of
		/// <c>KingdomPlotRules.RoofOnGround</c>, which correctly leaves an open plot open down
		/// there: Open is a claim about walls, and a field cut into the deep raises none, exactly
		/// as it raises none in the sun. It simply has a hill over it. Reading the roof state
		/// alone would have a photosynthetic settler housed in a cellar-field on the strength of a
		/// sky that is several hundred feet of rock away.
		/// </para>
		/// </summary>
		/// <param name="Roof">The tier's roof state, as declared.</param>
		/// <param name="Underground">Whether this ground is below
		/// <c>KingdomRules.SurfaceZLevel</c>. Handed in rather than derived: this file has no
		/// engine to ask, and <c>KingdomPlotRules.IsUnderground</c> is the one read that answers
		/// it.</param>
		public static string[] ProvidedByRoof(KingdomPlotRules.RoofState Roof, bool Underground)
		{
			return new string[1] { (!Underground && KingdomPlotRules.AdmitsSky(Roof)) ? TagSky : TagDark };
		}

		/// <summary>What a roof provides on the surface, where weather does reach.
		/// <see cref="ProvidedByRoof(KingdomPlotRules.RoofState, bool)"/> is the whole rule.
		/// </summary>
		public static string[] ProvidedByRoof(KingdomPlotRules.RoofState Roof)
		{
			return ProvidedByRoof(Roof, Underground: false);
		}

		/// <summary>
		/// Everything one design offers a resident: what its author declared, plus what its roof
		/// gives whether they thought about it or not.
		/// </summary>
		/// <param name="Declared">Raw <c>Provides</c> from the registry.</param>
		/// <param name="Roof">The tier's roof state.</param>
		/// <param name="IsPlot">False for a design that takes no ground at all, whose roof state is
		/// not a claim about anything; such a design offers only what it declared.</param>
		/// <param name="Underground">The stratum this design is standing in, for
		/// <see cref="ProvidedByRoof(KingdomPlotRules.RoofState, bool)"/>. One design raised on two
		/// strata offers two different things, which is why the caller says where.</param>
		public static string[] DesignOffer(string Declared, KingdomPlotRules.RoofState Roof, bool IsPlot, bool Underground)
		{
			string[] declared = ParseTags(Declared);
			return IsPlot ? Merge(ProvidedByRoof(Roof, Underground), declared) : declared;
		}

		/// <summary>The same offer, from a design standing on the surface.</summary>
		public static string[] DesignOffer(string Declared, KingdomPlotRules.RoofState Roof, bool IsPlot)
		{
			return DesignOffer(Declared, Roof, IsPlot, Underground: false);
		}

		// --- The match ------------------------------------------------------------------------

		/// <summary>
		/// Whether this resident lives here.
		/// <para>
		/// Refusals are checked before needs, deliberately. Both are hard, but an unmet need is a
		/// thing the founder can go and build and a refusal is not, so naming the refusal first
		/// never sends anyone off to raise a charging post for somebody who was never going to
		/// sleep there.
		/// </para>
		/// </summary>
		/// <param name="Offer">What the place provides. Null or empty provides nothing, which is
		/// fine for a resident who asks nothing.</param>
		/// <param name="Profile">The resident. Null asks nothing and always matches.</param>
		/// <param name="Tag">The tag that decided it: the one refused, or the first need missing.
		/// Empty on a match.</param>
		public static QolVerdict Judge(string[] Offer, QolProfile Profile, out string Tag)
		{
			Tag = "";
			if (Profile == null)
			{
				return QolVerdict.Match;
			}
			if (Profile.Refuses != null)
			{
				for (int i = 0; i < Profile.Refuses.Length; i++)
				{
					if (Has(Offer, Profile.Refuses[i]))
					{
						Tag = Fold(Profile.Refuses[i]);
						return QolVerdict.Refused;
					}
				}
			}
			if (Profile.Needs != null)
			{
				for (int i = 0; i < Profile.Needs.Length; i++)
				{
					if (!Has(Offer, Profile.Needs[i]))
					{
						Tag = Fold(Profile.Needs[i]);
						return QolVerdict.NeedUnmet;
					}
				}
			}
			return QolVerdict.Match;
		}

		/// <summary>Whether a verdict means the match happens.</summary>
		public static bool IsMatch(QolVerdict Verdict)
		{
			return Verdict == QolVerdict.Match;
		}

		/// <summary>Whether a verdict is the STANDARDS 7b kind that owes the founder a sentence.
		/// Both refusals do; a match says nothing, correctly.</summary>
		public static bool IsBlocked(QolVerdict Verdict)
		{
			return Verdict != QolVerdict.Match;
		}

		// --- Prefers: the small, capped, tastes-shaped half -------------------------------------

		/// <summary>
		/// How many met Prefers are ever counted. Two, which is the number of tastes a settling
		/// notable states, so a person with a long authored wish-list cannot out-shade a person
		/// with the ordinary one or two.
		/// </summary>
		public const int MaxPrefersCounted = 2;

		/// <summary>
		/// Which of this resident's Prefers the place meets, as the met/unmet flags
		/// <c>KingdomCeremonyRules.TasteShade</c> already takes. Capped at
		/// <see cref="MaxPrefersCounted"/> entries.
		/// <para>
		/// Deliberately this shape rather than a number of its own: the tastes machinery is the
		/// mod's one and only route from "this person found what they wanted here" to equilibrium,
		/// and a second route would be a second balance to keep.
		/// </para>
		/// </summary>
		/// <returns>Never null; empty for a resident who prefers nothing.</returns>
		public static List<bool> PreferFlags(string[] Offer, QolProfile Profile)
		{
			List<bool> flags = new List<bool>();
			if (Profile == null || Profile.Prefers == null)
			{
				return flags;
			}
			for (int i = 0; i < Profile.Prefers.Length && flags.Count < MaxPrefersCounted; i++)
			{
				flags.Add(Has(Offer, Profile.Prefers[i]));
			}
			return flags;
		}

		/// <summary>
		/// Equilibrium points this resident's met Prefers are worth, through the tastes machinery
		/// and capped by it: <c>KingdomCeremonyRules.TasteShadeAmount</c> a piece, never more than
		/// <see cref="MaxPrefersCounted"/> pieces.
		/// <para>
		/// There is no matching negative anywhere in this file. An unmet Prefers subtracts nothing
		/// and is not recorded; it simply means this person lives the way they would have lived
		/// anywhere.
		/// </para>
		/// </summary>
		public static int PreferShade(string[] Offer, QolProfile Profile)
		{
			return KingdomCeremonyRules.TasteShade(PreferFlags(Offer, Profile));
		}

		/// <summary>The most any one resident's Prefers can ever be worth.</summary>
		public static int MaxPreferShade
		{
			get
			{
				return MaxPrefersCounted * KingdomCeremonyRules.TasteShadeAmount;
			}
		}

		// --- Cohabitation ---------------------------------------------------------------------

		/// <summary>
		/// <b>Superseded</b> by <c>KingdomLodgingRules.RefusalHostility</c> (the closeness ladder,
		/// brief Addendum 4c) under the fault-line ceiling (4d), and kept only until its last
		/// caller is moved. Do not write new callers against it. What the hundred means is still
		/// exactly this:
		/// <para>
		/// Hostility at which two creeds will not share a roof, on the 0..100 scale
		/// <c>KingdomCreedRules.Hostility</c> returns. A hundred: only the game's own flat -100
		/// fault lines &mdash; the Templar and the Girsh, the Barathrumites and the Templar &mdash;
		/// and never the standing -50 several factions hold toward everyone they have not troubled
		/// to name, which would otherwise stop half a mixed settlement from sharing a wall.
		/// Everything milder is texture, and texture is what <c>Refuses</c> tags are for.
		/// </para>
		/// </summary>
		public const int CohabitHostility = 100;

		/// <summary>
		/// Whether two people share a roof <b>under the superseded flat floor</b>. The live answer
		/// is <c>KingdomLodgingRules</c>, which scales the same two sources by the quarters
		/// (Addendum 4c) under the fault-line ceiling (4d); this remains only until its last caller
		/// is moved. One rule, two sources: the engine's faction feelings for the ideological case,
		/// and the tag vocabulary for everything else &mdash; the neighbour's household is judged
		/// by exactly the <see cref="Judge"/> a building is.
		/// </summary>
		/// <param name="Profile">The person being moved in. Null refuses nobody.</param>
		/// <param name="TheirHousehold">What the household already there provides, from
		/// <see cref="HouseholdProvides"/>.</param>
		/// <param name="CreedHostility">From <c>KingdomCreed.HostilityBetween</c>. Zero for
		/// creedless people, people of one creed, and creeds that get on.</param>
		/// <param name="Tag">The tag refused, or empty when the creed decided it or nothing did.
		/// </param>
		/// <returns><see cref="QolVerdict.Refused"/> for a creed clash, so the caller can tell the
		/// two cases apart by <paramref name="Tag"/> being empty.</returns>
		public static QolVerdict JudgeCohabitation(QolProfile Profile, string[] TheirHousehold, int CreedHostility, out string Tag)
		{
			Tag = "";
			if (CreedHostility >= CohabitHostility)
			{
				return QolVerdict.Refused;
			}
			QolVerdict verdict = Judge(TheirHousehold, Profile, out Tag);
			// A neighbour is not a landlord: their household cannot supply what this person needs,
			// so an unmet need says nothing at all about whether the two can live together.
			if (verdict == QolVerdict.NeedUnmet)
			{
				Tag = "";
				return QolVerdict.Match;
			}
			return verdict;
		}

		// --- Saying so (STANDARDS 7b) -----------------------------------------------------------

		/// <summary>
		/// One tag as a sentence can hold it. Our own six read as plain English; anything else
		/// &mdash; another mod's, or one of ours a later wave adds &mdash; is quoted verbatim,
		/// which is both honest and the only thing that could be right about a word this file has
		/// never heard.
		/// </summary>
		public static string TagPhrase(string Tag)
		{
			switch (Fold(Tag))
			{
			case TagCharge:
				return "place to draw charge";
			case TagOpenWater:
				return "open water at the door";
			case TagDamp:
				return "damp";
			case TagDark:
				return "shade from the sun";
			case TagSky:
				return "sky overhead";
			case TagQuiet:
				return "quiet";
			default:
				return string.IsNullOrEmpty(Fold(Tag)) ? "what is wanted" : ("\"" + Fold(Tag) + "\"");
			}
		}

		/// <summary>The same tag from the other side, for a thing somebody will not live beside.
		/// </summary>
		public static string TagObjection(string Tag)
		{
			switch (Fold(Tag))
			{
			case TagCharge:
				return "the hum of the cradles";
			case TagOpenWater:
				return "the standing water";
			case TagDamp:
				return "the damp";
			case TagDark:
				return "the dark";
			case TagSky:
				return "the open sky";
			case TagQuiet:
				return "the quiet";
			default:
				return string.IsNullOrEmpty(Fold(Tag)) ? "what is there" : ("\"" + Fold(Tag) + "\"");
			}
		}

		/// <summary>
		/// The author's own sentence for a refusal, and the shortest true thing this system can
		/// say: <c>Vashti will not sleep beside the fungal cellar.</c> Names the person and the
		/// place, and nothing else.
		/// </summary>
		/// <param name="Who">The resident's name. Blank becomes "the newcomer".</param>
		/// <param name="Where">What they will not sleep beside. Blank becomes "what stands
		/// there".</param>
		public static string WillNotSleepBeside(string Who, string Where)
		{
			return Person(Who) + " will not sleep beside " + Place(Where) + ".";
		}

		/// <summary>
		/// The one sentence a refused match owes the founder. Every line names the person, the
		/// place, and the tag that decided it, and a line about an unmet need also names what would
		/// lift it &mdash; because a settler who quietly never moves in, for a reason nobody can
		/// see, is the exact failure STANDARDS 7b exists to prevent.
		/// </summary>
		/// <param name="Verdict">From <see cref="Judge"/>.</param>
		/// <param name="Who">The resident's name.</param>
		/// <param name="Where">The building, quarters, or household.</param>
		/// <param name="Tag">The tag <see cref="Judge"/> named. Empty is accepted and reads as a
		/// creed clash.</param>
		/// <returns>A player-facing line, or null for a match, which correctly says nothing.
		/// </returns>
		public static string RefusalLine(QolVerdict Verdict, string Who, string Where, string Tag)
		{
			switch (Verdict)
			{
			case QolVerdict.NeedUnmet:
				return Person(Who) + " will not live in " + Place(Where) + ": it has no "
					+ TagPhrase(Tag) + ". Give it that, or offer other quarters.";
			case QolVerdict.Refused:
				if (string.IsNullOrEmpty(Fold(Tag)))
				{
					return Person(Who) + " will not share a roof with the people already in "
						+ Place(Where) + ": what each of them believes leaves no room for the other.";
				}
				return WillNotSleepBeside(Who, Where) + " It is " + TagObjection(Tag) + ".";
			default:
				return null;
			}
		}

		/// <summary>
		/// The line for a resident the whole settlement has nowhere to put, which is the one a
		/// founder actually needs: a single refused building is ordinary, and a person who can live
		/// in none of them is a thing to go and fix. Said once, by a caller holding an announced
		/// flag, exactly like every other 7b line in the mod.
		/// </summary>
		/// <param name="Who">The resident's name.</param>
		/// <param name="Settlement">The settlement's display name. Blank reads as "this
		/// settlement".</param>
		/// <param name="Tag">The need nothing there meets.</param>
		public static string NowhereLine(string Who, string Settlement, string Tag)
		{
			string where = string.IsNullOrWhiteSpace(Settlement) ? "this settlement" : Settlement.Trim();
			return "Nothing standing in " + where + " has " + TagPhrase(Tag) + ", so "
				+ Person(Who) + " has nowhere in it to live. Build somewhere that does.";
		}

		private static string Person(string Name)
		{
			return string.IsNullOrWhiteSpace(Name) ? "the newcomer" : Name.Trim();
		}

		private static string Place(string Name)
		{
			return string.IsNullOrWhiteSpace(Name) ? "what stands there" : ("the " + Name.Trim());
		}
	}
}
