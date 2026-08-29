using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		public List<string> OutsiderEntries = new List<string>();

		public Dictionary<string, int> OriginCounts = new Dictionary<string, int>();

		/// <summary>Vanilla cultures carried by living citizens of the seated city, read through
		/// <c>GameObject.GetCulture()</c>. Live access, not learned stock: the last bearer leaving
		/// removes the corresponding <c>culture:</c> source. Carried by seat exchange.</summary>
		public Dictionary<string, int> CultureCounts = new Dictionary<string, int>();

		/// <summary>Vanilla species carried by living citizen bodies of the seated city, read through
		/// <c>GameObject.GetSpecies()</c>. Separate from culture by Addendum 17.</summary>
		public Dictionary<string, int> SpeciesCounts = new Dictionary<string, int>();

		/// <summary>Live genotype/body and extension-owned roster keys carried by resident bodies.
		/// Keys remain in their namespaces and leave when the last exact body receipt leaves. Per-city
		/// and carried by seat exchange beside culture/species.</summary>
		public Dictionary<string, int> IdentityCounts = new Dictionary<string, int>();

		/// <summary>The seated city's own tally of settler creeds. See <see cref="KingdomCreed"/>.
		/// Per-city and swapped with the seat exactly like <see cref="OriginCounts"/> &mdash; the
		/// counterpart <see cref="KingdomSettlement.CreedCounts"/> is what the reflected carry
		/// checks this field against.</summary>
		public Dictionary<string, int> CreedCounts = new Dictionary<string, int>();

		/// <summary>
		/// The seated city's tally of creeds its people have HELD AND LEFT &mdash; the other half
		/// of <see cref="CreedCounts"/>, and the half no count of present belief can supply.
		/// Addendum 16: a settler's creed history is a recorded fact from now on, and the ALIGNMENT
		/// gate is satisfied by a builder who holds a creed <em>or has previously held it</em>.
		/// <para>
		/// Kept beside the present tally rather than derived from the people, because the people
		/// of an unseated city are not loaded and a gate must answer anyway. Written at exactly two
		/// seams: <c>KingdomCreed.RememberPast</c>, off the one conversion path, and
		/// <c>KingdomCreed.Forget</c>, when the person carrying the history leaves or dies.
		/// </para>
		/// <para>
		/// Per-city and swapped with the seat exactly like <see cref="CreedCounts"/>: what this
		/// city's people have believed is not what the other city's have.
		/// </para>
		/// </summary>
		public Dictionary<string, int> CreedPastCounts = new Dictionary<string, int>();

		/// <summary>
		/// Addendum 5: shared living each settler has accumulated toward somebody else's creed,
		/// keyed by the name they are carried on the roll under. Counted in ATTENDED passes and in
		/// witnessed meals, never in ticks &mdash; that is what makes conversion unspendable while
		/// the founder is away. Empty for nearly every settler in nearly every city.
		/// <para>
		/// Per-city, and swapped with the seat exactly like <see cref="OriginCounts"/>: which
		/// household is pulling at whom is a fact about one city. Paired with
		/// <see cref="ConversionToward"/>, which names the creed those points are toward.
		/// </para>
		/// </summary>
		public Dictionary<string, int> ConversionShared = new Dictionary<string, int>();

		/// <summary>The creed each entry of <see cref="ConversionShared"/> is accumulating toward.
		/// A settler is only ever pulled one way at a time; a second pull takes points off the first
		/// rather than opening a second tally (<c>KingdomConversionRules.Advance</c>).</summary>
		public Dictionary<string, string> ConversionToward = new Dictionary<string, string>();

		/// <summary>
		/// Addendum 5's exit, moderated by Addendum 10(a): the world-day each settler standing
		/// under a creed they resent was WARNED on. The warning starts the window
		/// (<c>KingdomConversionRules.ResentmentRunOut</c> says when it spends), the entry is also
		/// the once-only announce flag (STANDARDS 7b), and it is removed the moment the pressure
		/// lifts &mdash; unsaying what was said &mdash; so a founder who takes the shrine back out
		/// of somebody's quarter has genuinely taken it back out.
		/// </summary>
		public Dictionary<string, int> ConversionResented = new Dictionary<string, int>();

		/// <summary>The bounded exact collection of owned cities which are not currently seated.
		/// It is authoritative; seat exchange moves one member into the flat seat and the captured
		/// former seat back into this collection.</summary>
		public KingdomSettlementTopology SettlementTopology = new KingdomSettlementTopology();

		/// <summary>
		/// Serialized compatibility projection of the first immutable-id-ordered non-seat city.
		/// <para>
		/// Old saves carried their sole non-seat city here. Load normalization migrates it into
		/// <see cref="SettlementTopology"/> once; new saves refresh this field immediately before
		/// writing so older integrations retain their documented read surface. Runtime code must
		/// use the topology APIs and never treat this projection as ownership authority.
		/// </para>
		/// </summary>
		[Obsolete("Use NonSeatSettlements(), FindNonSeatSettlementByZone(), or the exact topology APIs.")]
		public KingdomSettlement Away;

		/// <summary>
		/// Serialized compatibility projection of <see cref="KingdomTradeBook.Manifest"/>.
		/// <para>
		/// This remains a field because this system opts out of engine field reflection and writes
		/// named fields explicitly; replacing it with a forwarding property would silently omit the
		/// old wire name and break existing saves. Runtime code never treats it as authority. Every
		/// Trade lease and cold-load normalization replaces it with a fresh value snapshot.
		/// </para>
		/// </summary>
		[Obsolete("Use KingdomTrade.CurrentManifest(KingdomSystem). This field is a serialized compatibility projection.")]
		public KingdomManifest Manifest;

		/// <summary>
		/// Mismatched pre-projection contents formerly stored in <see cref="Manifest"/>. Preserved
		/// separately before the public field is refreshed so old ambiguous name-based evidence still
		/// quarantines Trade instead of being promoted or discarded.
		/// </summary>
		public KingdomManifest LegacyManifestEvidence;

		/// <summary>The realm's one carry-sign haul in flight, or null when none is en route.
		/// Realm-level and never swapped, for the same reason <see cref="Manifest"/> is: it
		/// addresses a settlement by name rather than by seat/Away role. See
		/// <see cref="ThousandAndFirst.KingdomGuestbook"/> and <see cref="ThousandAndFirst.KingdomCarryHaul"/>.</summary>
		public KingdomCarryHaul Haul;

		/// <summary>Dormant realm-scope carry authority, exact-bound to <see cref="RealmId"/>.</summary>
		public KingdomCarryBook CarryBook = new KingdomCarryBook();

		/// <summary>Bounded realm-scoped state retained across exile. Cities and standings stay in
		/// their existing exile slots; this receipt owns the transactional close/restore phases.</summary>
		public KingdomRealmArchive ExiledRealmArchive;

		/// <summary>
		/// The realm that put the founder out, kept whole: its faction name, its display name, and
		/// both of its cities exactly as they stood on the day of the expulsion.
		/// <para>
		/// Exile is secession, realm-scoped. The realm and its cities are not deleted, not renamed
		/// and not unmade &mdash; a runtime faction cannot be unmade anyway, and every one of them
		/// is walked forever by the reputation screen, the endgame reputation pass, the
		/// water-ritual curse and every <c>*allvisiblefactions</c> effect, so this mod mints one
		/// per realm and no more. What ends is the founder's claim on it. Nothing physical is
		/// touched: no citizen's allegiance key moves, no zone is unclaimed, no vessel loses its
		/// dedication, and the ground still carries the old realm's faction property.
		/// </para>
		/// <para>
		/// An exiled realm is a frozen archive rather than an active city-book owner. Both cities
		/// keep their exact clocks; if the founder is taken back, the restored realm resumes under
		/// the current uncapped elapsed-time and witnessed-brink rules. It is not silently simulated
		/// as a rival polity while nobody holds its authority.
		/// </para>
		/// </summary>
		public string ExiledFactionName;

		/// <summary>The expelled-from realm's display name. See <see cref="ExiledFactionName"/>.</summary>
		public string ExiledDisplayName;

		/// <summary>When the expulsion happened, for the record and the dev log.</summary>
		public long ExiledTick;

		/// <summary>The clause naming what the realm counted against the founder, from
		/// <see cref="KingdomExileRules.DeedClause"/>. Deeds, never elapsed time.</summary>
		public string ExiledDeed;

		/// <summary>The city the founder was seated in when the realm put them out.</summary>
		public KingdomSettlement ExiledSeat;

		/// <summary>The expelled-from realm's other city, or null if it held only one.</summary>
		[Obsolete("Use ExiledSettlementTopology.")]
		public KingdomSettlement ExiledAway;

		/// <summary>Bounded non-seat topology mirror of the expelled-from realm.</summary>
		public KingdomSettlementTopology ExiledSettlementTopology =
			new KingdomSettlementTopology();

		/// <summary>
		/// The worst regard the realm has said out loud about the founder, as a
		/// <see cref="RealmRegard"/>. The hysteresis lives here: see
		/// <see cref="KingdomExileRules.RememberedRegard"/>. Stored as an int so the ladder can
		/// gain a rung without retyping a serialized field.
		/// </summary>
		public int RegardSpoken;

		/// <summary>How far apart the realm's two cities have grown over their creeds. Realm-level
		/// and never swapped &mdash; unlike <see cref="CreedCounts"/>, this is a property of the
		/// realm holding two cities, not of either city on its own. See <see cref="KingdomCreed"/>
		/// and <see cref="KingdomCreedRules"/>.</summary>
		public int Dissent;

		/// <summary>Immutable ordered city ids owning the one active inter-city dissent account.
		/// Empty together means no pair is frozen; partial or non-owned values fail closed.</summary>
		public string DissentSettlementAId;
		public string DissentSettlementBId;

		/// <summary>The worst <see cref="CityTemper"/> already spoken and chronicled, so the
		/// warning ladder only speaks once per tier. See <see cref="KingdomCreedRules.RememberedTemper"/>.</summary>
		public int DissentSpoken;

		/// <summary>Tick of the last attended creed pass. Zero means no checkpoint yet.</summary>
		public long LastDissentTick;

		/// <summary>The creed the founder declared the realm's own, or null. See
		/// <see cref="KingdomCreed.Declare"/>.</summary>
		public string DeclaredCreed;

		// --- The realm's own dish ----------------------------------------------------------
		//
		// Realm state and not city state, deliberately: the dish lives on the FACTION
		// (Faction.WaterRitualRecipe / ...Text, D/XRL/World/Faction.cs:72-76), and a realm has
		// exactly one faction however many cities it holds. These four fields are the mod's copy
		// of what was written there, so a pass can tell whether the people who live here have
		// changed their minds without re-deriving to find out, and so the ration draw knows what
		// to look for on the shelves. See KingdomRules.DeriveDish and KingdomDish.Ensure.

		/// <summary>What the realm's favourite dish is called, lower case
		/// (<c>KingdomRules.FavoredDish.Name</c>). Null until the realm is founded.</summary>
		public string DishName;

		/// <summary>The sentence a stranger asks for the recipe with at the water ritual. Written
		/// onto <c>Faction.WaterRitualRecipeText</c>; kept here so a load that finds the faction
		/// stripped can put it back.</summary>
		public string DishText;

		/// <summary>The preserved staple the dish is made of, and what the grinding mill makes:
		/// the one blueprint that ties the fields, the mill and the table together. The ration
		/// draw reaches for this first (<c>KingdomSurvey.ConsumeFood</c>).</summary>
		public string DishStaple;

		/// <summary>The creed dish this one's form was borrowed from, or empty for a realm of
		/// mixed people. Compared against the current creed each pass to notice a change of
		/// heart.</summary>
		public string DishSource;

		/// <summary>Tick of the last rite of shared water. See <see cref="KingdomCreed.HoldRite"/>.</summary>
		public long LastRiteTick;

		/// <summary>
		/// Tick of the last rite of shared water held with one of the realm's OWN settlers
		/// (Addendum 5's diplomacy channel). Realm-level and never swapped, exactly like
		/// <see cref="LastRiteTick"/> and for the same reason: the founder is one person, and
		/// pouring twice in one evening is a round of drinks whichever city they are standing in.
		/// Zero means never. See <see cref="KingdomWaterRite.OpenRite"/>.
		/// </summary>
		public long LastSoulRiteTick;

		/// <summary>The city that left the realm over its creed, kept whole, or null. See
		/// <see cref="KingdomCreed.Secede"/>. Realm-level: a settlement does not carry its own
		/// secession record, the realm does.</summary>
		public KingdomSettlement Seceded;

		/// <summary>When <see cref="Seceded"/> left, for the record and the dev log.</summary>
		public long SecededTick;

		/// <summary>
		/// The regard at which the founder was last asked whether they wanted to be taken back,
		/// or <c>int.MinValue</c> if they never have been. Refusing silences the question until
		/// the founder has changed the realm's mind, so it can never nag.
		/// </summary>
		public int ReturnAskedRegard = int.MinValue;

		/// <summary>Whether the founder has been told, once, that founding again shut the door on
		/// the realm that expelled them.</summary>
		public bool DoorClosedTold;
	}
}
