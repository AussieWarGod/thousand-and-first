using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	[Serializable]
	public class KingdomSystem : IPlayerSystem
	{
		private const int SerializationMagic = 1413563987;

		/// <summary>
		/// Version 3 is the clock rework. Every stored clock stamp still has the same NAME and
		/// the same type, but not the same meaning: a version-2 stamp was written under the
		/// three-day forgiveness cap, so it could be arbitrarily stale and cost nothing, and
		/// resolving one under the uncapped rules would bill a season of upkeep in a single pass
		/// -- the exact unchosen debt Addendum 8 clause 4 forbids.
		/// <para>
		/// No migration machinery ships for it. The mod has never run; there are no version-2
		/// saves in the world (Addendum 9: "save compatibility is waived pre-release... version
		/// bumps stay clean and deliberate"), so <see cref="FirstNamedSerializationVersion"/>
		/// moves up with it and a pre-rework layout is refused at the gate by name rather than
		/// silently mis-resolved. The re-anchoring a real migration would have to do is written
		/// down where the retired cap lived, in <c>KingdomRules</c>, for the release-era harness
		/// to pick up.
		/// </para>
		/// </summary>
		/// <summary>
		/// Version 4 was the city book. <see cref="City"/> arrived on the system and on every
		/// settlement, the <c>r_TAF_Supports_*</c> and <c>r_TAF_Larders_*</c> game-state key
		/// families retired, and the realm gained a minted simulation seed.
		/// <para>
		/// Version 5 is residents as rows. The realm gains <see cref="Bindings"/> and
		/// <see cref="ResidentCounter"/>; the book's resident columns gain a cause, a standing flag
		/// per brink, and a warned TICK where a version-4 book carried only a flag; and the
		/// <c>KingdomBrinkRoof*</c> / <c>KingdomBrinkCreed*</c> settler properties retire into those
		/// columns. A version-4 save's settlers carry brinks nothing in this build reads and rows
		/// that cannot say when a window started, so it is refused by name rather than loaded as a
		/// city whose warnings have quietly lost their deadlines.
		/// </para>
		/// <para>
		/// Version 6 is the city that renders. The realm gains <see cref="Jobs"/> &mdash; the open
		/// itineraries LIVING-CITY-ARCHITECTURE &sect;3.7 puts a carrier on &mdash; and
		/// <see cref="LastSliceTick"/>, the heartbeat's own checkpoint. A version-5 save has
		/// bindings that could name a transient with no itinerary behind it, which is a carrier the
		/// model cannot say where is; so it is refused by name rather than loaded as a city with a
		/// porter nobody can place.
		/// </para>
		/// <para>
		/// Version 7 is the knowledge siting (Addendum 22 B1). Every city gains
		/// <see cref="KeepersRoster"/> and the lab's own header &mdash; subject, accrual, stamp,
		/// shelf &mdash; and the game-state key <c>r_TAF_KeepersRoster</c> is retired.
		/// </para>
		/// <para>
		/// <b>This bump does NOT move <see cref="FirstNamedSerializationVersion"/>, and that is the
		/// point of the difference.</b> Five and six were refused by name because a version-4 book
		/// had lost a warning's deadline and a version-5 binding could name a carrier nothing could
		/// place: state that could not be recovered. A version-6 save has lost nothing at all &mdash;
		/// its rolls are on the game, they are complete, and <c>KingdomZoning</c>'s one-time fold
		/// reads them into the seated city on first look. STANDARDS &sect;1 is explicit that a
		/// version at or below the current one which CAN be read must be read; refusing this one
		/// would turn a routine additive change into a save-wipe for nothing.
		/// </para>
		/// </summary>
		/// <summary>
		/// Version 8 is immutable runtime identity and the bounded realm archive. Version 7's
		/// newly custom Trade payload cannot be skipped safely by an older named-field reader, and
		/// it never persisted the founding transactions needed to distinguish a renamed realm from
		/// a new incarnation. This is still pre-release, so the compatibility boundary is deliberate:
		/// named versions before 8 are refused instead of manufacturing live authority from names.
		/// The separate reflected-v1 branch below is the only layout whose enclosing reader can
		/// still supply complete migration evidence; that branch performs one bounded migration.
		/// </summary>
		private const int CurrentSerializationVersion = 8;

		private const int FirstNamedSerializationVersion = 8;

		private const int LegacyReflectedSerializationVersion = 1;

		public int SerializationVersion = CurrentSerializationVersion;

		/// <summary>
		/// Set when <see cref="Read"/> could not interpret the saved state. Not serialized: it
		/// describes this load, not the kingdom. Cleared once the founder has been told.
		/// </summary>
		[NonSerialized]
		public bool LoadFailed;

		/// <summary>
		/// Days accounted for by this settlement's unread homecoming report. Carried with the
		/// settlement and serialized beside its ledger: swapping seats or saving cannot hand one
		/// city's age to another city's news.
		/// </summary>
		public int HomecomingDays;

		/// <summary>Immutable realm incarnation. Never derived from a current faction/display name.</summary>
		public string RealmId;

		public int RealmIdentityVersion;

		public KingdomIdentityOrigin RealmIdentityOrigin;

		/// <summary>The exact 32-lowerhex first-founding transaction, on the live-mint lane.</summary>
		public string RealmIdentityTransactionId;

		/// <summary>Frozen pre-identity faction evidence, populated only by reflected-v1 migration.</summary>
		public string RealmIdentityLegacyFaction;

		public long RealmIdentityFoundedTick;

		public ulong RealmIdentitySeedHigh;

		public ulong RealmIdentitySeedLow;

		public string RealmIdentityFirstClaimedZone;

		/// <summary>Nonempty permanently denies identity authority while preserving every source
		/// field for inspection. Normalization never repairs a partial/duplicate set first-wins.</summary>
		public string IdentityFault;

		/// <summary>One later-city identity frozen before monotone Trade expansion and cleared only
		/// after the exact city is present in the full topology. It is not a city and grants no
		/// draw/authority by itself.</summary>
		public string PendingSettlementId;

		public string PendingSettlementTransactionId;

		public string PendingSettlementZoneId;

		public string PendingSettlementAuthority;

		public string KingdomFactionName;

		public string KingdomDisplayName;

		/// <summary>
		/// The seated settlement's own name. Equal to <see cref="KingdomDisplayName"/> until a
		/// second city is founded, after which the realm keeps its name and each city keeps
		/// its own. Read through <see cref="SeatName"/>, which covers saves written before
		/// cities had names apart from the realm's.
		/// </summary>
		public string SettlementName;

		/// <summary>
		/// What the seated city was founded for, from <see cref="KingdomSettlement.Vocations"/>,
		/// or null for the realm's first city &mdash; which was founded before there was a second
		/// one to tell it from, and is not retroactively given a purpose.
		/// </summary>
		public string Vocation;

		public string Style = "common";

		/// <summary>
		/// The terrain blueprint read at the founding site, or null when the lookup was
		/// unavailable. Kept because <see cref="Style"/> is a conclusion and this is the evidence
		/// for it: a tester who disagrees with the style needs to see what the ground actually
		/// said. Serialization is by named fields, so a save written before this field existed
		/// simply arrives without it.
		/// </summary>
		public string FoundingTerrainBlueprint;

		/// <summary>Canonical terrain region of the founding site, or null. Evidence, as above.</summary>
		public string FoundingRegionName;

		/// <summary>Depth of the founding zone. Surface and strata read differently.</summary>
		public int FoundingZLevel;

		public long FoundedTick;

		public GrowthStage Stage = GrowthStage.Camp;

		public int Population;

		public int DryStreak;

		public bool Withered;

		/// <summary>
		/// Heartbeat resolves in a row the settlement's ration bill went unpaid. The food mirror
		/// of <see cref="DryStreak"/>, and a SEPARATE counter on purpose: the two ladders run at
		/// once and each keeps its own memory, so a settlement that fixes its water and not its
		/// fields is not quietly forgiven the second thing.
		/// <para>
		/// What stops the two costing double is <c>KingdomRules.ComposeScarcity</c>, which takes
		/// the WORSE of the two ladders and never their sum: one departure per resolve however
		/// many things are wrong.
		/// </para>
		/// </summary>
		public int HungerStreak;

		/// <summary>The food mirror of <see cref="Withered"/>: the settlement has been hungry
		/// long enough to be marked for it. Both marks can stand at once &mdash; they are states
		/// and not costs.</summary>
		public bool Famished;

		public bool HasShopkeeper;

		public bool NoRoomAnnounced;

		public long LastHeartbeatTick;

		/// <summary>
		/// Tick the settlement's people last carried water in from open water. Fetch is a rate
		/// like upkeep, so it needs its own checkpoint; without one it was charged per zone
		/// activation and could be farmed by walking out and back in.
		/// </summary>
		public long LastFetchTick;

		/// <summary>
		/// Tick the city model has paid the settlement's works through.
		/// <para>
		/// <b>W6 changed who writes this and left what it means alone.</b> It was the settlement
		/// pass's own checkpoint for the water works' daily make; that arithmetic moved onto the
		/// city model, per zone, off the model's single <c>ProcessedThroughTick</c>, because two
		/// owners of one day is a day paid twice. <c>KingdomCity.Stamp</c> now writes this field
		/// from that tick and nothing else writes it — it is the published mirror of the production
		/// clock, not a second one beside it.
		/// </para>
		/// </summary>
		public long LastWaterWorkTick;

		/// <summary>
		/// Tick the settlement's MILLS last ground through.
		/// <para>
		/// <b>W6 narrowed this to the mills, and the model deliberately does not touch it.</b> The
		/// fields' clocked make moved onto the city model with the water works' (see
		/// <see cref="LastWaterWorkTick"/>); the mills did not, and could not. A mill makes nothing
		/// out of the day &mdash; it takes real crops off real shelves and puts real staples back,
		/// on the ground where the shelves are &mdash; which is why
		/// <c>KingdomCrops.MilledFoodPerDay</c> has always been subtracted out of the model's own
		/// rate, and why the mills keep their own elapsed here.
		/// </para>
		/// <para>
		/// Advanced by the settlement pass with <c>KingdomRules.AdvanceCheckpoint</c> exactly as it
		/// always was, and planted before the first count for the same reason
		/// <see cref="LastFetchTick"/> is: unplanted, an uncapped read is the age of the world.
		/// Written from the reckon it would read <i>now</i> on every check-in and no mill would ever
		/// grind again.
		/// </para>
		/// </summary>
		public long LastFoodWorkTick;

		/// <summary>Citizens crewing works as of the last assignment pass. Hands on a mill are
		/// hands not carrying a bucket, which is what makes staffing a real choice.</summary>
		public int AssignedCrew;

		/// <summary>
		/// Settlers the founder has put on the water detail: they walk to open water and carry it
		/// back to the dedicated stores.
		/// <para>
		/// Zero by default, and deliberately. A settlement used to fetch for itself from the
		/// moment it was founded, which handed the player free automation they never chose and
		/// meant a site near a river watered itself forever. Until somebody is assigned, the
		/// settlement drinks what the founder pours in and what arrives under charter - which is
		/// the manual phase that teaches what a settlement costs before it can be automated away.
		/// </para>
		/// <para>
		/// Every settler here is one not manning a mill, a shop, or a wall. That is the whole
		/// point: hands are spent once.
		/// </para>
		/// </summary>
		public int WaterCrew;

		public int IdleWorks;

		public int ShorthandedWorks;

		/// <summary>How many of the settlement's works stand damaged and run reduced
		/// (Addendum 7). Counted fresh by <c>KingdomWear</c> on every attended pass; carried
		/// between seats beside <see cref="IdleWorks"/> and <see cref="ShorthandedWorks"/>.
		/// </summary>
		public int DamagedWorks;

		public bool IdleWorksAnnounced;

		/// <summary>
		/// STANDARDS 7b's once-flag for a harvest with nowhere to go: the fields made food and
		/// the settlement had no larder dedicated, or the larders it has are full. Set when the
		/// founder is first told, cleared the moment there is room again.
		/// </summary>
		public bool HarvestUnstoredAnnounced;

		/// <summary>
		/// Tick this settlement's level was last reckoned against its people
		/// (<c>KingdomSubsidence</c>). Uncapped world time: the slide runs whether the founder is
		/// there or not, and the stamp advances by exactly the steps a reckoning cashed, keeping
		/// the part-step remainder. Carried, so a dormant city does not settle a season's worth
		/// the moment it is seated.
		/// </summary>
		public long LastSubsidenceTick;

		/// <summary>
		/// STANDARDS 7b's once-flag for the slide, and the slide's own memory of being under way.
		/// Set when a settlement is first told it is settling back, cleared the moment it arrests
		/// &mdash; and read by <c>KingdomSubsidenceRules.Slide</c> as "already sliding", because a
		/// slide that has been announced converges to the level rather than stopping at the band's
		/// edge. The announcement and the hysteresis are the same fact, so they are one field.
		/// </summary>
		public bool SubsidenceAnnounced;

		/// <summary>
		/// People this settlement's finished works honestly carry, as of the last attended pass
		/// (<c>KingdomSubsidenceRules.SupportedLevel</c>). Knowledge, not truth: it is exactly as
		/// stale as the last visit. Zero on a settlement no pass has measured yet.
		/// </summary>
		public int SupportedLevel;

		/// <summary>
		/// Which of <c>KingdomCatalogueRules.BindingSupports</c> is holding
		/// <see cref="SupportedLevel"/> where it is, so the level can always say why (7b). Null
		/// until a pass has measured it, and read back through
		/// <c>KingdomSubsidenceRules.NormalizedBinding</c> rather than repaired in
		/// <c>Normalize</c> &mdash; the seat swap's contract is a byte-for-byte round trip, and
		/// what actually needs preventing is a sentence blaming a good this build cannot name.
		/// </summary>
		public string SubsidenceBinding;

		/// <summary>
		/// What this settlement's named notable is worth to the level
		/// (<c>KingdomCeremonyRules.NotableShade</c>): their met tastes, the net of their virtue
		/// and their flaw, and the <c>Prefers</c> their quarters happen to meet (Addendum 4).
		/// Written when the office is first filled or passes to somebody else, so it is exactly as
		/// stale as the last time it changed hands &mdash; knowledge, like
		/// <see cref="SupportedLevel"/>, rather than a meter. Zero for a settlement that has named
		/// nobody, which is every settlement until it has people enough to.
		/// </summary>
		public int NotableShade;

		/// <summary>
		/// Everything this settlement is shaded by, which is what the level actually reads: the
		/// named notable's standing worth plus whatever the last day's eating left behind. Summed
		/// here rather than at the four call sites in <c>KingdomSubsidence</c> so the two can
		/// never disagree about which shades count, and floored per half so neither can eat the
		/// other. <c>KingdomCatalogueRules.Equilibrium</c> caps the total again.
		/// </summary>
		public int Shade
		{
			get
			{
				return ((NotableShade < 0) ? 0 : NotableShade) + ((MealShade < 0) ? 0 : MealShade);
			}
		}

		/// <summary>
		/// What this settlement's last day's eating was worth to the level, for exactly the day
		/// it was earned (<c>KingdomRules.MealShadeFor</c>). Re-drawn every heartbeat: a
		/// settlement that ate its own dish yesterday and scraps today is worth the scraps. Rides
		/// the same lift term as <see cref="NotableShade"/> and is capped again with it by
		/// <c>KingdomCatalogueRules.LiftCapPercent</c>, so nobody eats their way past their own
		/// water. Carried, so a city left mid-feast is still well fed when the founder walks back
		/// into it.
		/// </summary>
		public int MealShade;

		/// <summary>What the settlement's last drawn day of rations actually was
		/// (<c>KingdomRules.JudgeMeal</c>). Knowledge for the report and the once-flag below;
		/// <see cref="KingdomRules.MealVerdict.None"/> on a settlement no heartbeat has billed
		/// yet.</summary>
		public KingdomRules.MealVerdict LastMeal = KingdomRules.MealVerdict.None;

		/// <summary>STANDARDS 7b's once-flag for a settlement whose larders gave nothing. Set
		/// when the sentence is said, cleared the moment the settlement eats out of its own
		/// stores again, so walking away and back does not re-say it.</summary>
		public bool ScrapsAnnounced;

		public int ShopTier;

		/// <summary>
		/// Last completed attended-semantic dispatch for this city. This is deliberately separate
		/// from <see cref="LastVisitTick"/>: the latter owns homecoming presentation, while this stamp
		/// owns the once-per-world-day simulation boundary. Carried with the settlement so changing
		/// seats cannot make either city repeat a day.
		/// </summary>
		public long LastSemanticTick;

		/// <summary>Durable identity of the attended pass currently being reconciled. A guarded
		/// subsystem fault leaves these receipts in place, so retry resumes the same ordered pass
		/// instead of rerunning every earlier subsystem or publishing a completed-day stamp.</summary>
		public bool SemanticPassActive;

		public long SemanticPassStartedTick;

		public string SemanticPassZoneId;

		public long SemanticPassStartedMask;

		public long SemanticPassCompletedMask;

		public long LastVisitTick;

		public string LastDeed;

		public long LastDeedTick;

		public KingdomRules.GatePolicy Gate = KingdomRules.GatePolicy.Open;

		public KingdomRules.StoresPolicy Stores = KingdomRules.StoresPolicy.Plenty;

		public int RaidTimesDeferred;

		public List<string> RosterNames = new List<string>();

		public List<string> RosterOrigins = new List<string>();

		public List<string> RosterArrived = new List<string>();

		public KingdomRules.PetitionKind PetitionKind = KingdomRules.PetitionKind.None;

		/// <summary>Explicit petition lifecycle; legacy non-empty petitions normalize to Offered.</summary>
		public PetitionLifecycle PetitionState = PetitionLifecycle.None;

		/// <summary>Immutable identity of the offered petition event.</summary>
		public string PetitionEventId;

		/// <summary>Settlement identity captured when the petition was offered.</summary>
		public string PetitionOriginSettlementId;

		/// <summary>Cause as spoken at offer time; later standings or labels do not rewrite it.</summary>
		public string PetitionCauseSnapshot;

		/// <summary>Canonical Qud month that last minted an offer. Negative means no evidence yet.</summary>
		public long LastPetitionMonthOrdinal = -1L;

		public string PetitionPetitioner;

		public string PetitionFaction;

		public int PetitionTarget;

		public long PetitionIssuedTick;

		public long LastPetitionTick;

		public int PetitionsMet;

		public int Dead;

		/// <summary>
		/// Every settler this settlement has lost, oldest first. Permanent: unlike
		/// <see cref="RosterNames"/> this roll is never trimmed, because a memorial does not stop
		/// being true once a cairn is finally raised for it. Written only by
		/// <c>KingdomOffices.RecordDeath</c>, from the engine's own death event &mdash; never from
		/// a census, which could not tell a dead settler from one who simply wandered to another
		/// claimed zone.
		/// </summary>
		public List<string> DeadNames = new List<string>();

		/// <summary>Parallel to <see cref="DeadNames"/>.</summary>
		public List<string> DeadOrigins = new List<string>();

		/// <summary>Parallel to <see cref="DeadNames"/>: the day each one arrived, carried over
		/// from <see cref="RosterArrived"/> at the moment of death.</summary>
		public List<string> DeadArrived = new List<string>();

		/// <summary>Parallel to <see cref="DeadNames"/>: how each death is told, from
		/// <c>KingdomOfficeRules.CauseClause</c> at the moment it happened.</summary>
		public List<string> DeadCauses = new List<string>();

		/// <summary>
		/// How many of <see cref="DeadNames"/>, oldest-first, already have a cairn cut with their
		/// name. Advances by one each time <c>KingdomOffices</c> links a newly built, unlinked
		/// cairn to the next unhonoured death; never decreases.
		/// </summary>
		public int MemorialsRaised;

		/// <summary>
		/// The settler currently named for the settlement's one office (see
		/// <c>KingdomOfficeRules</c>), or null when nobody is. The office itself is never chosen
		/// and stored here &mdash; it is always whoever heads <see cref="RosterNames"/>, the
		/// settler who has served longest. This field exists only so a change in who that is can
		/// be noticed and announced once, rather than every time the settlement's ground is
		/// walked onto.
		/// </summary>
		public string OfficeHolderName;

		/// <summary>
		/// Free space in the seated city's stores as of this pass. Carried with the settlement,
		/// so the city the founder is not standing in still knows what it had room for when they
		/// were last there. See <see cref="KingdomSettlement.LastKnownStorageSpace"/>.
		/// </summary>
		public int LastKnownStorageSpace;

		/// <summary>
		/// Servings of harvest this realm's cities owe their own pantries: gathered in one zone,
		/// credited to the city at once, and waiting to become real crop items in a larder whose
		/// zone nobody has walked into yet (Addendum 11(b-ii)).
		/// <para>
		/// PER-CITY, and carried by the seat swap on its own name
		/// (<see cref="KingdomSettlement.PendingCrop"/>): a harvest gathered in one city's outfield
		/// belongs in that city's pantries and never follows the founder to the other one. Nothing
		/// is touched in an unloaded zone, because nothing in an unloaded zone can be touched
		/// &mdash; the load simply waits for somebody to walk into a zone of its own city that has
		/// a dedicated larder in it.
		/// </para>
		/// </summary>
		public int PendingCrop;

		/// <summary>What the load on the road physically is, so it arrives as the crop that was
		/// actually grown rather than as whatever the receiving ground happens to favour. Null
		/// when nothing is in flight; a load that somehow lost its name arrives as the seated
		/// city's own crop rather than as nothing.</summary>
		public string PendingCropBlueprint;

		/// <summary>
		/// Which of the city's zones the load in flight came out of, so the carrier who renders it
		/// walks in by the edge that faces it. LIVING-CITY-ARCHITECTURE &sect;3.7 step 1: <i>mint
		/// the carrier at the edge &mdash; the zone edge nearest the source zone</i>. A fact and not
		/// a draw, which is what lets the estimate and the founder's own crossing agree.
		/// </summary>
		public string PendingCropZoneId;

		public KingdomLedger Ledger = new KingdomLedger();

		/// <summary>
		/// Records the kingdom's most recent notable act, which is what draws settlers and
		/// what arrival messages name. Deeds are forgotten after a while; reputation is not.
		/// </summary>
		/// <param name="Deed">Lower-case noun phrase, e.g. "the cistern you raised".</param>
		public void RecordDeed(string Deed)
		{
			LastDeed = Deed;
			LastDeedTick = The.Game.TimeTicks;
		}

		public long NextArrivalTick;

		public int RaidState;

		public string RaidFactionName;

		public long RaidDueTick;

		public long LastRaidTick;

		/// <summary>Tick the settlement may draw its next guest. See <see cref="ThousandAndFirst.KingdomLocus"/>.</summary>
		public long NextGuestTick;

		/// <summary>Tick the settlement's current guest gives up and leaves if never offered water. Zero when no guest is tracked.</summary>
		public long GuestDepartTick;

		/// <summary>True once this settlement has offered water to a guest at least once.</summary>
		public bool FirstGuestGreeted;

		/// <summary>Tick the settlement may draw its next notable guest. See
		/// <see cref="ThousandAndFirst.KingdomGuestbook"/>.</summary>
		public long NextNotableGuestTick;

		/// <summary>Tick the settlement's current notable guest gives up and leaves if never
		/// lodged. Zero when no notable guest is tracked.</summary>
		public long NotableGuestDepartTick;

		/// <summary>True once this settlement has lodged a notable guest at least once.</summary>
		public bool FirstNotableGuestLodged;

		/// <summary>The seated city's own guestbook: one line per notable guest who resolved,
		/// lodged or departed. See <see cref="ThousandAndFirst.KingdomGuestbook"/>.</summary>
		public List<string> GuestbookLines = new List<string>();

		public List<string> ClaimedZones = new List<string>();

		public Dictionary<string, string> ZoneDistricts = new Dictionary<string, string>();

		/// <summary>The seated city's own rolls. See <see cref="KingdomSettlement.KeepersRoster"/>;
		/// this is the flat field the seat swap carries them in, which is the whole of what makes
		/// secession, rejoin, exile and return handle knowledge without one line of their own.</summary>
		public string KeepersRoster;

		/// <summary>The seated city's current subject. See
		/// <see cref="KingdomSettlement.ResearchSubject"/>.</summary>
		public string ResearchSubject;

		/// <summary>See <see cref="KingdomSettlement.ResearchAccrued"/>.</summary>
		public int ResearchAccrued;

		/// <summary>See <see cref="KingdomSettlement.ResearchTakenUpTick"/>.</summary>
		public long ResearchTakenUpTick;

		/// <summary>See <see cref="KingdomSettlement.ResearchStalledAnnounced"/>.</summary>
		public bool ResearchStalledAnnounced;

		/// <summary>See <see cref="KingdomSettlement.ResearchShelf"/>.</summary>
		public Dictionary<string, int> ResearchShelf = new Dictionary<string, int>();

		/// <summary>See <see cref="KingdomSettlement.ResearchBestMind"/>.</summary>
		public int ResearchBestMind;

		/// <summary>Provenance for <see cref="City"/>'s immutable settlement id. These fields are
		/// city-carried and have exact counterparts on <see cref="KingdomSettlement"/>.</summary>
		public int SettlementIdentityVersion;

		public KingdomIdentityOrigin SettlementIdentityOrigin;

		public string SettlementIdentityTransactionId;

		public long SettlementIdentityFoundedTick;

		public string SettlementIdentityFirstClaimedZone;

		/// <summary>The retired pre-identity city label, retained only as migration evidence.</summary>
		public string SettlementIdentityLegacyId;

		/// <summary>Dormant per-city lifecycle authority, exact-bound during identity publication.
		/// No lane executes from it yet; carrying it now avoids another save-schema boundary.</summary>
		public KingdomLifecycleBook LifecycleBook = new KingdomLifecycleBook();

		/// <summary>The seated city's model. See <see cref="KingdomSettlement.City"/>; this is the
		/// flat field the seat swap carries it in.</summary>
		public Simulation.City.KingdomCityBook City = new Simulation.City.KingdomCityBook();

		/// <summary>
		/// The realm's simulation seed, minted once at founding and never re-minted.
		/// <para>
		/// Two <c>ulong</c> halves rather than a <c>KernelSeed128</c> field, because the kernel's
		/// seed type is an internal value type of the simulation slice and this is the engine's own
		/// serialized surface: the halves go out as plain numbers and
		/// <see cref="SimulationSeed"/> composes them back. Realm-scope, not per-city &mdash; the
		/// realm is the incarnation the kernel domain-separates on.
		/// </para>
		/// </summary>
		public ulong SimulationSeedHigh;

		/// <summary>See <see cref="SimulationSeedHigh"/>.</summary>
		public ulong SimulationSeedLow;

		/// <summary>
		/// One identity, at most one body. LIVING-CITY-ARCHITECTURE &sect;3.8's binding registry,
		/// keyed by <c>ResidentId</c> for people and by <c>JobId</c> for the carriers W3 mints.
		/// <para>
		/// <b>Realm-scope, and deliberately not on a settlement.</b> A bound body can be standing in
		/// the other city's ground or walked off the map entirely, so a registry a seat swap carried
		/// would answer for half the realm and lose the other half every time the founder crossed a
		/// zone line. It is therefore realm state, like the standings and the chronicle, and
		/// <c>SettlementSeatTests</c> asserts that no city carries it.
		/// </para>
		/// </summary>
		public Simulation.City.KingdomBindingRegistry Bindings = new Simulation.City.KingdomBindingRegistry();

		/// <summary>
		/// How many people the realm has ever enrolled. The next <c>KingdomResidentId</c>
		/// (<c>KingdomResidents.ResidentIdProperty</c>), minted in order and never reused.
		/// <para>
		/// Realm-scope for the reason the registry is: one identity must be unique across both
		/// cities, and two per-city counters would hand the same number to two people. A counter and
		/// not a draw &mdash; identity is a substrate, and a seeded id would make who-is-who depend
		/// on how many other things had been rolled first.
		/// </para>
		/// </summary>
		public int ResidentCounter;

		/// <summary>
		/// The realm's open itineraries. LIVING-CITY-ARCHITECTURE &sect;3.7: a job is a timed
		/// itinerary computed once at creation, and one pure function over it answers where the
		/// carrier is and what is on them at any tick &mdash; which is invariant I5, and why the
		/// body never has to literally traverse anything.
		/// <para>
		/// <b>Realm-scope, beside <see cref="Bindings"/>, and for the same reason.</b> A carrier's
		/// legs can cross into the other city's ground or off the map, and every job row is paired
		/// one-to-one with a transient binding that already lives here. &sect;0.0(c) prices the job
		/// rows realm-wide and &sect;3.8 caps them per realm, so this is where the constitution
		/// already put them.
		/// </para>
		/// </summary>
		public Simulation.City.KingdomJobRegistry Jobs = new Simulation.City.KingdomJobRegistry();

		/// <summary>
		/// When the heartbeat last advanced the realm's cities. LIVING-CITY-ARCHITECTURE &sect;3.6:
		/// the cadence is fifty ticks &mdash; one in-game hour, <c>Calendar.TurnsPerHour</c> &mdash;
		/// and a slice advances by <b>whatever elapsed</b>, so several boundaries crossed at once
		/// (a world-map step, a long rest) is one slightly larger slice rather than a special case.
		/// <c>N</c> decides how often we bother, never how much we advance.
		/// </summary>
		public long LastSliceTick;

		/// <summary>
		/// The one neighbouring zone the prefetch is holding resident, or null.
		/// <para>
		/// <b>Not serialized, and that is the honest shape.</b> A hold is a decision about this
		/// session's memory, not a fact about the realm: LIVING-CITY-ARCHITECTURE &sect;6.4's own
		/// invariant is that <i>a prefetched zone the founder never enters is indistinguishable
		/// from one that was never prefetched</i>, so a hold that lapses over a save is exactly as
		/// correct as one that does not.
		/// </para>
		/// </summary>
		[NonSerialized]
		public string PrefetchedZoneId;

		/// <summary>
		/// Which turn the realm's reify allowance is being counted against, and how much of it is
		/// gone.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;0.0: the budget is <b>eight units a turn</b>, of which at
		/// most four are body mints &mdash; per TURN, and not per call site. The homecoming pass,
		/// the pump and the prefetch all reify on the same turn, so three call sites each taking a
		/// full eight would be twenty-four and the receipt would be reporting a budget nobody was
		/// keeping. Not serialized: an allowance is a fact about this turn, and a saved one would
		/// arrive spent.
		/// </para>
		/// </summary>
		[NonSerialized]
		public long ReifyTick;

		/// <summary>See <see cref="ReifyTick"/>. Weighted thirds spent so far this turn.</summary>
		[NonSerialized]
		public int ReifyThirdsSpent;

		/// <summary>See <see cref="ReifyTick"/>. Body mints and moves spent so far this turn, which
		/// carries its own ceiling because it is a frame-cost rather than an ordering
		/// preference.</summary>
		[NonSerialized]
		public int ReifyHeavySpent;

		/// <summary>
		/// Until when the pump will not survey a zone for reify again.
		/// <para>
		/// A debt the ground cannot serve &mdash; a draw against an empty cistern, a landing with no
		/// larder standing &mdash; is still a debt, and it stays on the row until the founder does
		/// something about it. Retrying it every turn would pay a full zone survey for an answer
		/// that has not changed, so a spend that moved nothing buys an in-game hour of quiet. A new
		/// debt therefore waits at most one hour, which is nothing against the twenty-nine turns
		/// &sect;0.0(b) allows a full backlog. Not serialized: it is a fact about this session's
		/// turns.
		/// </para>
		/// </summary>
		[NonSerialized]
		public long ReifyQuietUntilTick;

		/// <summary>
		/// How many containers the realm has ever counted as its own. The next dedication ordinal
		/// (<c>KingdomCity.DedicationOrderProperty</c>), which is what makes the drain order of
		/// LIVING-CITY-ARCHITECTURE &sect;3.9 a stored fact rather than a ranking recomputed from
		/// contents. Realm-scope: ordinals only ever have to be comparable, never contiguous.
		/// </summary>
		public int DedicationCounter;

		public List<string> ActiveDealKeys = new List<string>();

		public List<string> ActiveDealFactions = new List<string>();

		public List<long> DealNextTicks = new List<long>();

		/// <summary>
		/// Realm trade authority. The three lists above and <see cref="Manifest"/> are retained
		/// only as bounded legacy evidence. Identity v8 never converts or clears their mutable-name
		/// authority; any nonempty row quarantines Trade for inspection.
		/// </summary>
		public KingdomTradeBook TradeBook = new KingdomTradeBook();

		public List<string> ChronicleEntries = new List<string>();

		public List<string> OutsiderEntries = new List<string>();

		public Dictionary<string, int> OriginCounts = new Dictionary<string, int>();

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

		public Dictionary<string, int> Standings = new Dictionary<string, int>();

		/// <summary>
		/// The city the founder is not standing in, or null until a second is founded.
		/// <para>
		/// Everything above this line describes the seat &mdash; the settlement the founder is
		/// currently in &mdash; and every physical consumer reads those fields directly. The other
		/// city's serialized seat mirror waits here, and the two are exchanged by
		/// <see cref="TrySeat"/> when the founder walks into its ground.
		/// </para>
		/// <para>
		/// The away city's <see cref="KingdomSettlement.City"/> book advances beside the seated
		/// book on <c>KingdomHeartbeat</c>; production, upkeep, bounded arrivals, brinks, and news
		/// therefore follow world time without loading its zone. Physical objects and advisory
		/// seat fields reconcile only after <see cref="TrySeat"/> obtains that ground as the fresh
		/// active seat. Its tick stamps travel with it so the projection cannot bill a modeled day
		/// twice.
		/// </para>
		/// </summary>
		public KingdomSettlement Away;

		/// <summary>
		/// The realm's one in-flight water manifest, or null when none is en route. Realm-level
		/// and never swapped: it addresses cities by settlement name rather than by seat/Away
		/// role, because those roles exchange on <see cref="TrySeat"/> and a manifest is
		/// addressed to a place, not a role. A save written before this field existed arrives
		/// with it null, which is exactly "no manifest in flight".
		/// </summary>
		public KingdomManifest Manifest;

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
		public KingdomSettlement ExiledAway;

		/// <summary>
		/// The expelled-from realm's own ledger of standings. Held apart from
		/// <see cref="Standings"/> so a realm founded afterwards cannot inherit the grudges and
		/// friendships of the one that disowned the founder &mdash; two realms sharing one
		/// standings pool would receive identical feelings from every third party, which is the
		/// exact opposite of the old realm keeping its own opinion.
		/// </summary>
		public Dictionary<string, int> ExiledStandings = new Dictionary<string, int>();

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

		public bool Founded => !string.IsNullOrEmpty(KingdomFactionName);

		/// <summary>Whether a realm has put the founder out and is remembered here.</summary>
		public bool Exiled => !string.IsNullOrEmpty(ExiledFactionName);

		/// <summary>How many cities the expelled-from realm holds, or 0 if there is none.</summary>
		public int ExiledSettlementCount => (!Exiled ? 0 : ((ExiledAway != null) ? 2 : 1));

		/// <summary>
		/// The seated settlement's name for prose. Falls back to the realm's display name for a
		/// save written before a city could be named apart from its realm.
		/// </summary>
		public string SeatName => string.IsNullOrEmpty(SettlementName) ? KingdomDisplayName : SettlementName;

		/// <summary>
		/// The realm's simulation seed, composed from its two stored halves.
		/// <para>
		/// Internal rather than public because <c>KernelSeed128</c> is the simulation slice's own
		/// value type and the kernel is deliberate about it: identity travels one way, through the
		/// canonical encoder, and a seed handed out on a public surface is a seed somebody keys a
		/// collection by. The two halves are the public, serialized surface.
		/// </para>
		/// </summary>
		internal Simulation.Kernel.KernelSeed128 SimulationSeed => new Simulation.Kernel.KernelSeed128(SimulationSeedHigh, SimulationSeedLow);

		/// <summary>The exact seated-city id, or null when any realm/city provenance or topology
		/// cannot be reproved whole. Callers must fail closed; there is deliberately no name fold.</summary>
		internal string CurrentSettlementId
		{
			get
			{
				string realm;
				string settlement;
				return TryGetCurrentIdentity(out realm, out settlement) ? settlement : null;
			}
		}

		/// <summary>The exact realm id under the same whole-topology proof as the current city.</summary>
		internal string CurrentRealmId
		{
			get
			{
				string realm;
				string settlement;
				return TryGetCurrentIdentity(out realm, out settlement) ? realm : null;
			}
		}

		internal bool TryGetCurrentIdentity(out string ExactRealmId,
			out string ExactSettlementId)
		{
			ExactRealmId = null;
			ExactSettlementId = null;
			if (RealmTransitionActive()) return false;
			List<string> settlements;
			string failure = null;
			if (!TryExactSettlementIds(RequirePublishedClaims: true, out settlements,
				out failure) || City == null || !settlements.Contains(City.SettlementId))
			{
				return false;
			}
			ExactRealmId = RealmId;
			ExactSettlementId = City.SettlementId;
			return true;
		}

		internal bool TryCaptureSealIdentity(out KingdomSealIdentity Identity,
			out string Failure)
		{
			Identity = null;
			Failure = null;
			if (!TryGetCurrentIdentity(out string realm, out string settlement) ||
				!TryExactSettlementIds(RequirePublishedClaims: true, out List<string> settlements,
					out Failure))
			{
				Failure = Failure ?? "current immutable realm topology cannot be proved";
				return false;
			}
			KingdomSettlement seat;
			try { seat = Capture(); }
			catch (Exception ex) { Failure = ex.Message; return false; }
			settlements.Sort(StringComparer.Ordinal);
			if (!TryBuildSealSettlementProvenance(settlements, seat, Away,
				out List<string> provenance, out Failure)) return false;
			KingdomSealIdentity candidate = new KingdomSealIdentity
			{
				RealmId = realm,
				SettlementId = settlement,
				SettlementIds = new List<string>(settlements),
				SettlementProvenanceRows = provenance,
				RealmIdentityVersion = RealmIdentityVersion,
				RealmIdentityOrigin = RealmIdentityOrigin,
				RealmIdentityTransactionId = RealmIdentityTransactionId,
				RealmIdentityLegacyFaction = RealmIdentityLegacyFaction,
				RealmIdentityFoundedTick = RealmIdentityFoundedTick,
				RealmIdentitySeedHigh = RealmIdentitySeedHigh,
				RealmIdentitySeedLow = RealmIdentitySeedLow,
				RealmIdentityFirstClaimedZone = RealmIdentityFirstClaimedZone,
				SettlementIdentityVersion = seat.SettlementIdentityVersion,
				SettlementIdentityOrigin = seat.SettlementIdentityOrigin,
				SettlementIdentityTransactionId = seat.SettlementIdentityTransactionId,
				SettlementIdentityFoundedTick = seat.SettlementIdentityFoundedTick,
				SettlementIdentityFirstClaimedZone = seat.SettlementIdentityFirstClaimedZone,
				SettlementIdentityLegacyId = seat.SettlementIdentityLegacyId
			};
			if (!KingdomSealRules.ExactIdentity(candidate, seat))
			{
				Failure = "current seal identity provenance cannot be reproved";
				return false;
			}
			Identity = candidate;
			return true;
		}

		private static bool TryBuildSealSettlementProvenance(IList<string> SettlementIds,
			KingdomSettlement Seat, KingdomSettlement Away, out List<string> Rows,
			out string Failure)
		{
			Rows = new List<string>();
			Failure = null;
			if (SettlementIds == null || Seat?.City == null)
			{
				Failure = "seal settlement topology is absent";
				return false;
			}
			for (int i = 0; i < SettlementIds.Count; i++)
			{
				KingdomSettlement source = null;
				if (Seat.City.SettlementId == SettlementIds[i]) source = Seat;
				if (Away?.City?.SettlementId == SettlementIds[i])
				{
					if (source != null)
					{
						Failure = "seal settlement topology has duplicate city identity";
						return false;
					}
					source = Away;
				}
				if (source == null || !KingdomSealRules.TryBuildSettlementProvenance(
					SettlementIds[i], source.SettlementIdentityVersion,
					source.SettlementIdentityOrigin, source.SettlementIdentityTransactionId,
					source.SettlementIdentityFoundedTick,
					source.SettlementIdentityFirstClaimedZone,
					source.SettlementIdentityLegacyId, out string row))
				{
					Failure = "seal settlement provenance cannot be bounded";
					return false;
				}
				Rows.Add(row);
			}
			return true;
		}

		internal bool SealIdentityStillMatches(KingdomSealIdentity Expected)
		{
			if (Expected == null || !TryCaptureSealIdentity(out KingdomSealIdentity current,
				out string _)) return false;
			if (Expected.RealmId != current.RealmId ||
				Expected.SettlementId != current.SettlementId ||
				Expected.RealmIdentityVersion != current.RealmIdentityVersion ||
				Expected.RealmIdentityOrigin != current.RealmIdentityOrigin ||
				Expected.RealmIdentityTransactionId != current.RealmIdentityTransactionId ||
				Expected.RealmIdentityLegacyFaction != current.RealmIdentityLegacyFaction ||
				Expected.RealmIdentityFoundedTick != current.RealmIdentityFoundedTick ||
				Expected.RealmIdentitySeedHigh != current.RealmIdentitySeedHigh ||
				Expected.RealmIdentitySeedLow != current.RealmIdentitySeedLow ||
				Expected.RealmIdentityFirstClaimedZone != current.RealmIdentityFirstClaimedZone ||
				Expected.SettlementIdentityVersion != current.SettlementIdentityVersion ||
				Expected.SettlementIdentityOrigin != current.SettlementIdentityOrigin ||
				Expected.SettlementIdentityTransactionId != current.SettlementIdentityTransactionId ||
				Expected.SettlementIdentityFoundedTick != current.SettlementIdentityFoundedTick ||
				Expected.SettlementIdentityFirstClaimedZone !=
					current.SettlementIdentityFirstClaimedZone ||
				Expected.SettlementIdentityLegacyId != current.SettlementIdentityLegacyId ||
				Expected.SettlementIds == null || current.SettlementIds == null ||
				Expected.SettlementIds.Count != current.SettlementIds.Count ||
				Expected.SettlementProvenanceRows == null ||
				current.SettlementProvenanceRows == null ||
				Expected.SettlementProvenanceRows.Count !=
					current.SettlementProvenanceRows.Count) return false;
			for (int i = 0; i < Expected.SettlementIds.Count; i++)
				if (Expected.SettlementIds[i] != current.SettlementIds[i] ||
					Expected.SettlementProvenanceRows[i] !=
						current.SettlementProvenanceRows[i]) return false;
			return true;
		}

		private bool RealmTransitionActive()
		{
			if (ExiledRealmArchive == null ||
				ExiledRealmArchive.Phase == KingdomRealmArchivePhase.Closed ||
				ExiledRealmArchive.Phase == KingdomRealmArchivePhase.Restored) return false;
			if (ExiledRealmArchive.Phase == KingdomRealmArchivePhase.Quarantined)
				return string.Equals(RealmId, ExiledRealmArchive.RealmId,
					StringComparison.Ordinal);
			return true;
		}

		/// <summary>Stages the first realm and city ids before faction registration or any engine
		/// callback. A retry accepts only the exact same transaction and founding ground.</summary>
		internal bool TryBindFirstFoundingIdentity(string TransactionId, string ZoneId,
			out string Failure)
		{
			Failure = null;
			string realm;
			string settlement;
			KingdomIdentityFault fault = KingdomIdentityFault.None;
			if (string.IsNullOrEmpty(ZoneId) || ZoneId.Length > 512 ||
				!KingdomIdentityRules.TryMintRealm(TransactionId, out realm, out fault) ||
				!KingdomIdentityRules.TryMintSettlement(realm, TransactionId,
					out settlement, out fault))
			{
				Failure = "The first founding transaction could not mint bounded immutable identity (" +
					fault + ").";
				return false;
			}
			if (FirstIdentityStateEmpty())
			{
				KingdomLifecycleBook preparedLifecycle;
				KingdomCarryBook preparedCarry;
				if (!KingdomLifecycleRules.TryPrepareFirstIdentityBooks(LifecycleBook,
					CarryBook, realm, settlement, out preparedLifecycle,
					out preparedCarry))
				{
					Failure = "Dormant lifecycle or carry evidence is not pristine.";
					return false;
				}
				RealmId = realm;
				RealmIdentityVersion = KingdomIdentityRules.RulesVersion;
				RealmIdentityOrigin = KingdomIdentityOrigin.FoundingTransaction;
				RealmIdentityTransactionId = TransactionId;
				RealmIdentityLegacyFaction = null;
				RealmIdentityFoundedTick = 0L;
				RealmIdentitySeedHigh = 0UL;
				RealmIdentitySeedLow = 0UL;
				RealmIdentityFirstClaimedZone = ZoneId;
				IdentityFault = null;
				if (City == null) City = new Simulation.City.KingdomCityBook();
				City.SettlementId = settlement;
				SettlementIdentityVersion = KingdomIdentityRules.RulesVersion;
				SettlementIdentityOrigin = KingdomIdentityOrigin.FoundingTransaction;
				SettlementIdentityTransactionId = TransactionId;
				SettlementIdentityFoundedTick = 0L;
				SettlementIdentityFirstClaimedZone = ZoneId;
				SettlementIdentityLegacyId = null;
				LifecycleBook = preparedLifecycle;
				CarryBook = preparedCarry;
			}
			if (TryBindDormantLifecycleIdentity(out Failure))
			{
				if (FirstIdentityMatches(TransactionId, ZoneId)) return true;
				// An exact pending tuple owned by another transaction is not corruption. Refuse
				// without poisoning the only authority that can resume it.
				if (FirstIdentityMatches(RealmIdentityTransactionId,
					RealmIdentityFirstClaimedZone))
				{
					Failure = "The immutable first founding belongs to another transaction or site.";
					return false;
				}
			}
			QuarantineIdentity("first-founding immutable identity is partial or replaced");
			Failure = IdentityFault;
			return false;
		}

		internal bool FirstIdentityMatches(string TransactionId, string ZoneId)
		{
			KingdomIdentityFault fault = KingdomIdentityFault.None;
			return string.IsNullOrEmpty(IdentityFault) && Away == null && City != null &&
				RealmIdentityOrigin == KingdomIdentityOrigin.FoundingTransaction &&
				SettlementIdentityOrigin == KingdomIdentityOrigin.FoundingTransaction &&
				RealmIdentityTransactionId == TransactionId &&
				SettlementIdentityTransactionId == TransactionId &&
				RealmIdentityFirstClaimedZone == ZoneId &&
				SettlementIdentityFirstClaimedZone == ZoneId &&
				KingdomIdentityRules.ReproveRealm(RealmId, RealmIdentityVersion,
					RealmIdentityOrigin, RealmIdentityTransactionId,
					RealmIdentityLegacyFaction, RealmIdentityFoundedTick,
					RealmIdentitySeedHigh, RealmIdentitySeedLow,
					RealmIdentityFirstClaimedZone, out fault) &&
				KingdomIdentityRules.ReproveSettlement(City.SettlementId, RealmId,
					SettlementIdentityVersion, SettlementIdentityOrigin,
					SettlementIdentityTransactionId, SettlementIdentityFoundedTick,
					SettlementIdentityFirstClaimedZone, out fault) &&
				LifecycleIdentityMatches(LifecycleBook, City.SettlementId) &&
				CarryIdentityMatches();
		}

		/// <summary>Computes (without publishing) the later city's exact id. The caller freezes it
		/// on the founding site before any permanent city marker or Away assignment.</summary>
		internal bool TryPrepareLaterSettlementIdentity(string TransactionId, string ZoneId,
			out string SettlementId, out string Failure)
		{
			SettlementId = null;
			Failure = null;
			List<string> current;
			if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
				IncludePending: false, out current, out Failure))
				return false;
			KingdomIdentityFault fault = KingdomIdentityFault.None;
			if (string.IsNullOrEmpty(ZoneId) || ZoneId.Length > 512 ||
				!KingdomIdentityRules.TryMintSettlement(RealmId, TransactionId,
					out SettlementId, out fault))
			{
				Failure = "The later founding transaction could not mint immutable city identity (" +
					fault + ").";
				SettlementId = null;
				return false;
			}
			if (current.Contains(SettlementId))
			{
				bool exactPendingPublication = PendingSettlementTupleValid(out string _) &&
					PendingSettlementId == SettlementId &&
					PendingSettlementTransactionId == TransactionId &&
					PendingSettlementZoneId == ZoneId &&
					!string.IsNullOrEmpty(PendingSettlementAuthority) &&
					(SeatedLaterIdentityMatches(SettlementId, TransactionId, ZoneId) ||
					 LaterSettlementIdentityMatches(Away, SettlementId, TransactionId, ZoneId));
				if (exactPendingPublication) return true;
				Failure = "The later founding transaction collides with an existing city identity.";
				SettlementId = null;
				return false;
			}
			return true;
		}

		/// <summary>Returns every other retained lifecycle identity that a binding must scan.
		/// Exact ids are de-duplicated because archived mirrors may name the same retained city.</summary>
		internal List<string> LifecycleCollisionIds(bool IncludeSeat, bool IncludeAway)
		{
			List<string> ids = new List<string>();
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			if (IncludeSeat) AddLifecycleCollisionId(ids, seen, City?.SettlementId);
			if (IncludeAway) AddLifecycleCollisionId(ids, seen, Away?.City?.SettlementId);
			AddLifecycleCollisionId(ids, seen, Seceded?.City?.SettlementId);
			AddLifecycleCollisionId(ids, seen, ExiledSeat?.City?.SettlementId);
			AddLifecycleCollisionId(ids, seen, ExiledAway?.City?.SettlementId);
			ids.Sort(StringComparer.Ordinal);
			return ids;
		}

		private static void AddLifecycleCollisionId(List<string> Ids,
			HashSet<string> Seen, string Id)
		{
			if (!string.IsNullOrEmpty(Id) && Seen.Add(Id)) Ids.Add(Id);
		}

		internal static bool TryBindSettlementIdentity(KingdomSettlement Settlement,
			string SettlementId, string TransactionId, string ZoneId, long FoundedTick,
			ICollection<string> ExistingSettlementIds, out string Failure)
		{
			Failure = null;
			if (Settlement == null)
			{
				Failure = "No settlement record was supplied for immutable identity.";
				return false;
			}
			if (Settlement.City == null)
				Settlement.City = new Simulation.City.KingdomCityBook();
			Settlement.City.SettlementId = SettlementId;
			Settlement.SettlementIdentityVersion = KingdomIdentityRules.RulesVersion;
			Settlement.SettlementIdentityOrigin = KingdomIdentityOrigin.FoundingTransaction;
			Settlement.SettlementIdentityTransactionId = TransactionId;
				Settlement.SettlementIdentityFoundedTick = FoundedTick;
				Settlement.SettlementIdentityFirstClaimedZone = ZoneId;
				Settlement.SettlementIdentityLegacyId = null;
			if (Settlement.LifecycleBook == null)
				Settlement.LifecycleBook = new KingdomLifecycleBook();
			KingdomLifecycleRules.Normalize(Settlement.LifecycleBook);
			if (KingdomLifecycleRules.BindSettlementIdentity(Settlement.LifecycleBook,
				SettlementId, LegacyMigration: false, MigrationKey: null,
				ExistingIds: ExistingSettlementIds)) return true;
			Settlement.LifecycleBook.Quarantined = true;
			Settlement.LifecycleBook.Fault =
				"lifecycle book could not bind the exact new settlement identity";
			Failure = Settlement.LifecycleBook.Fault;
			return false;
		}

		internal bool LaterSettlementIdentityMatches(KingdomSettlement Settlement,
			string ExpectedId, string TransactionId, string ZoneId)
		{
			if (Settlement == null || Settlement.City == null ||
				Settlement.SettlementIdentityFirstClaimedZone != ZoneId ||
				Settlement.ClaimedZones == null || !Settlement.ClaimedZones.Contains(ZoneId))
				return false;
			KingdomIdentityFault fault;
			return string.Equals(Settlement.City.SettlementId, ExpectedId,
					StringComparison.Ordinal) &&
				Settlement.SettlementIdentityOrigin ==
					KingdomIdentityOrigin.FoundingTransaction &&
				Settlement.SettlementIdentityTransactionId == TransactionId &&
				KingdomIdentityRules.ReproveSettlement(Settlement.City.SettlementId,
					RealmId, Settlement.SettlementIdentityVersion,
					Settlement.SettlementIdentityOrigin,
					Settlement.SettlementIdentityTransactionId,
					Settlement.SettlementIdentityFoundedTick,
					Settlement.SettlementIdentityFirstClaimedZone, out fault);
		}

		internal bool SeatedLaterIdentityMatches(string ExpectedId,
			string TransactionId, string ZoneId)
		{
			if (City == null || SettlementIdentityFirstClaimedZone != ZoneId ||
				ClaimedZones == null || !ClaimedZones.Contains(ZoneId)) return false;
			KingdomIdentityFault fault;
			return string.Equals(City.SettlementId, ExpectedId, StringComparison.Ordinal) &&
				SettlementIdentityOrigin == KingdomIdentityOrigin.FoundingTransaction &&
				SettlementIdentityTransactionId == TransactionId &&
				KingdomIdentityRules.ReproveSettlement(City.SettlementId, RealmId,
					SettlementIdentityVersion, SettlementIdentityOrigin,
					SettlementIdentityTransactionId, SettlementIdentityFoundedTick,
					SettlementIdentityFirstClaimedZone, out fault) &&
				LifecycleIdentityMatches(LifecycleBook, City.SettlementId);
		}

		/// <summary>Resolves a mutable city name only when it denotes exactly one proven current
		/// city. It returns the immutable id; no caller receives first-match authority.</summary>
		internal bool TryResolveSettlementIdByName(string Name, out string SettlementId)
		{
			SettlementId = null;
			if (string.IsNullOrEmpty(Name)) return false;
			List<string> identities;
			string failure;
			if (!TryExactSettlementIds(RequirePublishedClaims: true, out identities,
				out failure)) return false;
			List<string> names = new List<string> { SettlementName };
			List<string> ids = new List<string> { City.SettlementId };
			if (Away != null)
			{
				names.Add(Away.SettlementName);
				ids.Add(Away.City.SettlementId);
			}
			KingdomIdentityFault fault;
			return KingdomIdentityRules.TryResolveUniqueSettlementName(names, ids, Name,
				out SettlementId, out fault);
		}

		internal bool TryExactSettlementIds(bool RequirePublishedClaims,
			out List<string> SettlementIds, out string Failure)
		{
			SettlementIds = new List<string>();
			Failure = null;
			if (!string.IsNullOrEmpty(IdentityFault))
			{
				Failure = IdentityFault;
				return false;
			}
			KingdomIdentityFault fault;
			if (!KingdomIdentityRules.ReproveRealm(RealmId, RealmIdentityVersion,
				RealmIdentityOrigin, RealmIdentityTransactionId,
				RealmIdentityLegacyFaction, RealmIdentityFoundedTick,
				RealmIdentitySeedHigh, RealmIdentitySeedLow,
				RealmIdentityFirstClaimedZone, out fault) ||
				!SettlementIdentityMatches(City, SettlementIdentityVersion,
					SettlementIdentityOrigin, SettlementIdentityTransactionId,
					SettlementIdentityFoundedTick, SettlementIdentityFirstClaimedZone,
					RequirePublishedClaims, ClaimedZones, out fault) ||
				!LifecycleIdentityMatches(LifecycleBook, City?.SettlementId) ||
				!CarryIdentityMatches())
			{
				Failure = "The seated city identity cannot be reproved (" + fault + ").";
				return false;
			}
			SettlementIds.Add(City.SettlementId);
			if (Away != null)
			{
				if (!SettlementIdentityMatches(Away.City, Away.SettlementIdentityVersion,
					Away.SettlementIdentityOrigin, Away.SettlementIdentityTransactionId,
					Away.SettlementIdentityFoundedTick,
					Away.SettlementIdentityFirstClaimedZone, RequirePublishedClaims,
					Away.ClaimedZones, out fault) ||
					!LifecycleIdentityMatches(Away.LifecycleBook,
						Away.City?.SettlementId))
				{
					Failure = "The away city identity cannot be reproved (" + fault + ").";
					return false;
				}
				SettlementIds.Add(Away.City.SettlementId);
			}
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, SettlementIds,
				out fault))
			{
				Failure = "The complete city identity set is invalid (" + fault + ").";
				return false;
			}
			SettlementIds.Sort(StringComparer.Ordinal);
			return true;
		}

		/// <summary>Returns the monotone authority topology: active cities, any retained seceded
		/// city, and optionally the exact pending later-city tuple.</summary>
		internal bool TryRetainedSettlementIds(bool RequirePublishedClaims,
			bool IncludePending, out List<string> SettlementIds, out string Failure)
		{
			if (!TryExactSettlementIds(RequirePublishedClaims, out SettlementIds,
				out Failure)) return false;
			KingdomIdentityFault fault = KingdomIdentityFault.None;
			if (Seceded != null)
			{
				if (!SettlementIdentityMatches(Seceded.City,
					Seceded.SettlementIdentityVersion, Seceded.SettlementIdentityOrigin,
					Seceded.SettlementIdentityTransactionId,
					Seceded.SettlementIdentityFoundedTick,
					Seceded.SettlementIdentityFirstClaimedZone, RequirePublishedClaims,
					Seceded.ClaimedZones, out fault) ||
					!LifecycleIdentityMatches(Seceded.LifecycleBook,
						Seceded.City?.SettlementId))
				{
					Failure = "The retained seceded city identity cannot be reproved (" +
						fault + ").";
					return false;
				}
				SettlementIds.Add(Seceded.City.SettlementId);
			}
			if (IncludePending && (!string.IsNullOrEmpty(PendingSettlementId) ||
				!string.IsNullOrEmpty(PendingSettlementTransactionId) ||
				!string.IsNullOrEmpty(PendingSettlementZoneId) ||
				!string.IsNullOrEmpty(PendingSettlementAuthority)))
			{
				if (!PendingSettlementTupleValid(out Failure)) return false;
				if (!SettlementIds.Contains(PendingSettlementId))
					SettlementIds.Add(PendingSettlementId);
			}
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, SettlementIds,
				out fault))
			{
				Failure = "The retained city identity set is invalid (" + fault + ").";
				return false;
			}
			SettlementIds.Sort(StringComparer.Ordinal);
			return true;
		}

		private bool TryBindDormantLifecycleIdentity(out string Failure)
		{
			Failure = null;
			if (LifecycleBook == null) LifecycleBook = new KingdomLifecycleBook();
			KingdomLifecycleRules.Normalize(LifecycleBook);
			List<string> otherLifecycleIds = LifecycleCollisionIds(
				IncludeSeat: false, IncludeAway: true);
			if (!KingdomLifecycleRules.BindSettlementIdentity(LifecycleBook,
				City?.SettlementId, LegacyMigration: false, MigrationKey: null,
				ExistingIds: otherLifecycleIds))
			{
				LifecycleBook.Quarantined = true;
				LifecycleBook.Fault =
					"lifecycle book could not bind exact seated-city identity";
				Failure = LifecycleBook.Fault;
				return false;
			}
			if (CarryBook == null) CarryBook = new KingdomCarryBook();
			KingdomLifecycleRules.Normalize(CarryBook);
			List<string> carrySettlementIds;
			if (!TryExpectedCarryTopology(out carrySettlementIds, out Failure))
			{
				CarryBook.Quarantined = true;
				CarryBook.Fault =
					"carry book could not bind exact immutable realm topology";
				Failure = Failure ?? CarryBook.Fault;
				return false;
			}
			// Pending publication permits exactly two cut states: old retained topology or
			// expanded topology. Never ask BindCarryIdentity to reinterpret an already-bound
			// old book as corruption before the paired coordinator can recover forward.
			if (CarryIdentityMatches(carrySettlementIds)) return true;
			if (KingdomLifecycleRules.CanOwnAuthority(CarryBook) &&
				CarryIdentityMatches()) return true;
			if (KingdomLifecycleRules.BindCarryIdentity(CarryBook, RealmId,
				carrySettlementIds, LegacyMigration: false, MigrationKey: null))
			{
				KingdomLifecycleRules.Normalize(CarryBook);
				if (CarryIdentityMatches(carrySettlementIds)) return true;
			}
			CarryBook.Quarantined = true;
			CarryBook.Fault = "carry book could not bind exact immutable realm identity";
			Failure = CarryBook.Fault;
			return false;
		}

		private static bool LifecycleIdentityMatches(KingdomLifecycleBook Book,
			string SettlementId)
		{
			return Book != null && !Book.LegacyIdentity &&
				string.Equals(Book.SettlementId, SettlementId,
					StringComparison.Ordinal) &&
				KingdomLifecycleRules.CanOwnAuthority(Book);
		}

		private bool CarryIdentityMatches()
		{
			List<string> expected;
			string failure;
			if (!TryExpectedCarryTopology(out expected, out failure)) return false;
			if (CarryIdentityMatches(expected)) return true;
			// A proved pending later-city tuple is a durable redo barrier. Save cuts may
			// therefore retain either the old exact Carry set or the expanded exact set;
			// no third topology is accepted.
			if (!string.IsNullOrEmpty(PendingSettlementId) &&
				expected.Remove(PendingSettlementId))
			{
				KingdomIdentityFault fault;
				return KingdomIdentityRules.ValidateRealmTopology(RealmId, expected,
					out fault) && CarryIdentityMatches(expected);
			}
			return false;
		}

		private bool CarryIdentityMatches(IList<string> Expected)
		{
			if (CarryBook == null || CarryBook.LegacyIdentity || Expected == null ||
				CarryBook.SettlementIds == null ||
				CarryBook.SettlementIds.Count != Expected.Count ||
				!string.Equals(CarryBook.RealmId, RealmId, StringComparison.Ordinal) ||
				!KingdomLifecycleRules.CanOwnAuthority(CarryBook)) return false;
			for (int i = 0; i < Expected.Count; i++)
				if (!string.Equals(CarryBook.SettlementIds[i], Expected[i],
					StringComparison.Ordinal)) return false;
			return true;
		}

		private bool TryExpectedCarryTopology(out List<string> SettlementIds,
			out string Failure)
		{
			SettlementIds = new List<string>();
			Failure = null;
			AddLifecycleCollisionId(SettlementIds,
				new HashSet<string>(StringComparer.Ordinal), City?.SettlementId);
			HashSet<string> seen = new HashSet<string>(SettlementIds,
				StringComparer.Ordinal);
			AddLifecycleCollisionId(SettlementIds, seen, Away?.City?.SettlementId);
			AddLifecycleCollisionId(SettlementIds, seen, Seceded?.City?.SettlementId);
			bool hasPending = !string.IsNullOrEmpty(PendingSettlementId) ||
				!string.IsNullOrEmpty(PendingSettlementTransactionId) ||
				!string.IsNullOrEmpty(PendingSettlementZoneId) ||
				!string.IsNullOrEmpty(PendingSettlementAuthority);
			if (hasPending)
			{
				if (!PendingSettlementTupleValid(out Failure)) return false;
				AddLifecycleCollisionId(SettlementIds, seen, PendingSettlementId);
			}
			KingdomIdentityFault fault;
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, SettlementIds,
				out fault))
			{
				Failure = "The retained carry topology is invalid (" + fault + ").";
				return false;
			}
			SettlementIds.Sort(StringComparer.Ordinal);
			return true;
		}

		internal bool TryBindTradeIdentity(out string Failure)
		{
			List<string> settlements;
			if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
				IncludePending: true, out settlements, out Failure)) return false;
			if (TradeBook == null) TradeBook = new KingdomTradeBook();
			KingdomTradeRules.Normalize(TradeBook);
			return KingdomTradeRules.BindExactIdentity(TradeBook, RealmId, settlements,
				out Failure);
		}

		/// <summary>Freezes paired detached Trade and Carry replacements. No live authority
		/// changes until the basin has published its forward-recovery barrier.</summary>
		internal bool TryPrepareSecondCityTopology(string NewSettlementId,
			out KingdomSecondCityTopologyPlan Plan, out string Failure)
		{
			Plan = null;
			bool hasPending = !PendingSettlementIdentityAbsent();
			if (hasPending && (!PendingSettlementTupleValid(out Failure) ||
				PendingSettlementId != NewSettlementId))
			{
				Failure = Failure ??
					"Another pending city owns the topology publication barrier.";
				return false;
			}
			if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
				IncludePending: false, out List<string> current, out Failure)) return false;
			return KingdomSecondCityPublicationRules.TryPrepare(RealmId, current,
				NewSettlementId, TradeBook, CarryBook, out Plan, out Failure);
		}

		/// <summary>Publishes a prepared paired replacement after PublicationCommitted.
		/// Exact retries retain both original references and bytes.</summary>
		internal bool TryCommitSecondCityTopology(KingdomSecondCityTopologyPlan Plan,
			string TransactionId, string ZoneId, string Authority, out string Failure)
		{
			Failure = null;
			if (Plan == null || !PendingSettlementTupleMatches(TransactionId, ZoneId,
				Authority) || Plan.SettlementId != PendingSettlementId)
			{
				Failure = "The paired topology plan does not match the pending city tuple.";
				return false;
			}
			return KingdomSecondCityPublicationRules.TryCommit(Plan, ref TradeBook,
				ref CarryBook, out Failure);
		}

		internal bool TryProveSettledSecondCityTopology(out string Failure)
		{
			Failure = null;
			if (!PendingSettlementIdentityAbsent())
			{
				Failure = "The later-city pending tuple has not settled.";
				return false;
			}
			if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
				IncludePending: false, out List<string> published, out Failure)) return false;
			if (KingdomSecondCityPublicationRules.ExactTopology(published, RealmId,
				TradeBook, CarryBook)) return true;
			Failure = "Trade and Carry do not name the exact published city topology.";
			return false;
		}

		/// <summary>Verifies that published Carry authority names the complete live city set.</summary>
		internal bool TryBindCarryIdentity(out string Failure)
		{
			List<string> settlements;
			if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
				IncludePending: true, out settlements, out Failure)) return false;
			if (CarryBook == null)
			{
				Failure = "Carry identity is absent.";
				return false;
			}
			KingdomLifecycleRules.Normalize(CarryBook);
			if (KingdomLifecycleRules.BindCarryIdentity(CarryBook, RealmId, settlements,
				LegacyMigration: false, MigrationKey: null) && CarryIdentityMatches())
				return true;
			Failure = "Carry identity does not match the complete published city set.";
			return false;
		}

		internal bool TryStagePendingSettlementIdentity(string SettlementId,
			string TransactionId, string ZoneId, string Authority, out string Failure)
		{
			Failure = null;
			string expected;
			KingdomIdentityFault fault;
			KingdomFoundingAuthority parsed;
			if (!KingdomIdentityRules.TryMintSettlement(RealmId, TransactionId,
					out expected, out fault) || expected != SettlementId ||
				string.IsNullOrEmpty(ZoneId) || ZoneId.Length > 512 ||
				string.IsNullOrEmpty(Authority) || Authority.Length > 4096 ||
				!KingdomFoundingTransactionRules.TryParseAuthority(Authority, out parsed) ||
				parsed.Kind != KingdomFoundingKind.SecondCity ||
				parsed.TransactionID != TransactionId || parsed.ZoneID != ZoneId ||
				parsed.RealmFaction != KingdomFactionName)
			{
				Failure = "The pending city identity tuple is malformed.";
				return false;
			}
			if (string.IsNullOrEmpty(PendingSettlementId) &&
				string.IsNullOrEmpty(PendingSettlementTransactionId) &&
				string.IsNullOrEmpty(PendingSettlementZoneId) &&
				string.IsNullOrEmpty(PendingSettlementAuthority))
			{
				PendingSettlementId = SettlementId;
				PendingSettlementTransactionId = TransactionId;
				PendingSettlementZoneId = ZoneId;
				PendingSettlementAuthority = Authority;
			}
			if (PendingSettlementId == SettlementId &&
				PendingSettlementTransactionId == TransactionId &&
				PendingSettlementZoneId == ZoneId &&
				PendingSettlementAuthority == Authority) return true;
			QuarantineIdentity("pending later-city identity carries a third value");
			Failure = IdentityFault;
			return false;
		}

		internal bool TryAbortPendingSettlementIdentity(string TransactionId,
			string ZoneId, string Authority, out string Failure)
		{
			Failure = null;
			if (PendingSettlementIdentityAbsent()) return true;
			if (!PendingSettlementTupleMatches(TransactionId, ZoneId, Authority))
			{
				Failure = "The pending city tuple does not match abort authority.";
				return false;
			}
			if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
				IncludePending: false, out List<string> published, out Failure) ||
				!KingdomSecondCityPublicationRules.CanAbort(published,
					PendingSettlementId, RealmId, TradeBook, CarryBook))
			{
				Failure = Failure ??
					"Expanded or published city topology can only recover forward.";
				return false;
			}
			ClearPendingSettlementIdentityFields();
			return PendingSettlementIdentityAbsent();
		}

		internal bool TrySettlePendingSettlementIdentity(string TransactionId,
			string ZoneId, string Authority, out string Failure)
		{
			Failure = null;
			if (PendingSettlementIdentityAbsent())
				return TryProveSettledSecondCityTopology(out Failure);
			if (!PendingSettlementTupleMatches(TransactionId, ZoneId, Authority))
			{
				Failure = "The pending city tuple does not match settlement authority.";
				return false;
			}
			if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
				IncludePending: false, out List<string> published, out Failure) ||
				!KingdomSecondCityPublicationRules.CanSettle(published,
					PendingSettlementId, RealmId, TradeBook, CarryBook))
			{
				Failure = Failure ??
					"Published city, Trade, and Carry do not prove one exact topology.";
				return false;
			}
			ClearPendingSettlementIdentityFields();
			return TryProveSettledSecondCityTopology(out Failure);
		}

		private bool PendingSettlementTupleMatches(string TransactionId,
			string ZoneId, string Authority)
		{
			return PendingSettlementTupleValid(out string _) &&
				PendingSettlementTransactionId == TransactionId &&
				PendingSettlementZoneId == ZoneId &&
				PendingSettlementAuthority == Authority;
		}

		private bool PendingSettlementIdentityAbsent()
		{
			return string.IsNullOrEmpty(PendingSettlementId) &&
				string.IsNullOrEmpty(PendingSettlementTransactionId) &&
				string.IsNullOrEmpty(PendingSettlementZoneId) &&
				string.IsNullOrEmpty(PendingSettlementAuthority);
		}

		private void ClearPendingSettlementIdentityFields()
		{
			PendingSettlementId = null;
			PendingSettlementTransactionId = null;
			PendingSettlementZoneId = null;
			PendingSettlementAuthority = null;
		}

		private bool SettlementIdentityMatches(Simulation.City.KingdomCityBook Book,
			int Version, KingdomIdentityOrigin Origin, string TransactionId,
			long IdentityFoundedTick, string FirstClaimedZone, bool RequirePublishedClaim,
			List<string> Claims, out KingdomIdentityFault Fault)
		{
			if (Book == null || string.IsNullOrEmpty(FirstClaimedZone) ||
				(RequirePublishedClaim && (Claims == null ||
				 !Claims.Contains(FirstClaimedZone))))
			{
				Fault = KingdomIdentityFault.InvalidEvidence;
				return false;
			}
			return KingdomIdentityRules.ReproveSettlement(Book.SettlementId, RealmId,
				Version, Origin, TransactionId, IdentityFoundedTick, FirstClaimedZone,
				out Fault);
		}

		private bool FirstIdentityStateEmpty()
		{
			return string.IsNullOrEmpty(RealmId) && RealmIdentityVersion == 0 &&
				RealmIdentityOrigin == KingdomIdentityOrigin.None &&
				string.IsNullOrEmpty(RealmIdentityTransactionId) &&
				string.IsNullOrEmpty(RealmIdentityLegacyFaction) &&
				RealmIdentityFoundedTick == 0L && RealmIdentitySeedHigh == 0UL &&
				RealmIdentitySeedLow == 0UL &&
				string.IsNullOrEmpty(RealmIdentityFirstClaimedZone) &&
				string.IsNullOrEmpty(IdentityFault) && SettlementIdentityVersion == 0 &&
				SettlementIdentityOrigin == KingdomIdentityOrigin.None &&
				string.IsNullOrEmpty(SettlementIdentityTransactionId) &&
				SettlementIdentityFoundedTick == 0L &&
				string.IsNullOrEmpty(SettlementIdentityFirstClaimedZone) &&
				string.IsNullOrEmpty(SettlementIdentityLegacyId) &&
				(City == null || string.IsNullOrEmpty(City.SettlementId)) && Away == null;
		}

		internal void QuarantineIdentity(string Failure)
		{
			if (!string.IsNullOrEmpty(IdentityFault)) return;
			IdentityFault = string.IsNullOrEmpty(Failure)
				? "immutable identity requires inspection"
				: (Failure.Length > 512 ? Failure.Substring(0, 512) : Failure);
			KingdomLog.Log("identity: quarantined: " + IdentityFault);
		}

		/// <summary>
		/// Mints the realm's simulation seed, once, at founding.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE W0 deferred this to W1 and the kernel says what it has to be:
		/// "whatever mints it must domain-separate on realm incarnation". So it is a pure function
		/// of the world seed, the immutable realm id and the tick the water was poured &mdash; two realms
		/// in one world differ, and the same realm across a reload does not. Re-minting is refused
		/// rather than performed: a seed that moves is a history that did not happen.
		/// </para>
		/// </summary>
		internal bool MintSimulationSeed(int WorldSeed, string ExactRealmId, long FoundedTick)
		{
			if (SimulationSeedHigh != 0UL || SimulationSeedLow != 0UL)
			{
				return false;
			}
			Simulation.Kernel.KernelSeed128 seed;
			Simulation.City.KingdomCityFault fault = Simulation.City.KingdomCityFault.None;
			if (!KingdomIdentityRules.IsRealmId(ExactRealmId) ||
				!Simulation.City.KingdomCityRules.TryMintSeed(WorldSeed, ExactRealmId,
					FoundedTick, out seed, out fault))
			{
				KingdomLog.Log("seed: refused (" + fault + "); the realm runs unseeded until it is founded again");
				return false;
			}
			SimulationSeedHigh = seed.High;
			SimulationSeedLow = seed.Low;
			KingdomLog.Log("seed: minted for immutable realm " + ExactRealmId +
				" at tick " + FoundedTick);
			return true;
		}

		internal bool SimulationSeedMatches(int WorldSeed, string ExactRealmId,
			long FoundedTick)
		{
			Simulation.Kernel.KernelSeed128 expected;
			Simulation.City.KingdomCityFault fault;
			return KingdomIdentityRules.IsRealmId(ExactRealmId) && FoundedTick >= 0L &&
				Simulation.City.KingdomCityRules.TryMintSeed(WorldSeed, ExactRealmId,
					FoundedTick, out expected, out fault) &&
				SimulationSeedHigh == expected.High && SimulationSeedLow == expected.Low;
		}

		/// <summary>How many cities the realm holds, seat included.</summary>
		public int SettlementCount => (!Founded ? 0 : ((Away != null) ? 2 : 1));

		/// <summary>
		/// Copies the seated settlement out of the flat fields into a record. The flat fields are
		/// left as they are; the caller is expected to write another settlement over them
		/// immediately, because the two now share their rosters, ledger and claim lists.
		/// </summary>
		/// <returns>The seated settlement, never null.</returns>
		/// <exception cref="KingdomSeatMismatchException">A settlement field has no flat
		/// counterpart here. Nothing is read when this is thrown.</exception>
		public KingdomSettlement Capture()
		{
			KingdomSettlement settlement = new KingdomSettlement();
			settlement.ReadFrom(this);
			return settlement;
		}

		/// <summary>
		/// Seats a settlement: writes it over the flat fields, so every consumer that reads
		/// <c>Population</c>, <c>ClaimedZones</c> or <c>Ledger</c> is now reading this city.
		/// </summary>
		/// <param name="Settlement">The settlement to seat. Null is rejected.</param>
		/// <exception cref="KingdomSeatMismatchException">A settlement field has no flat
		/// counterpart here. Nothing is written when this is thrown.</exception>
		public void Restore(KingdomSettlement Settlement)
		{
			if (Settlement == null)
			{
				throw new KingdomSeatMismatchException("There is no settlement to seat.");
			}
			Settlement.WriteTo(this);
		}

		/// <summary>
		/// Exchanges the seat with <see cref="Away"/> when the activated zone is the other city's
		/// ground. Called before the claim guard in <see cref="HandleEvent(ZoneActivatedEvent)"/>,
		/// because until the exchange has happened the second city's ground is not in
		/// <see cref="ClaimedZones"/> and reads as a stranger's zone.
		/// </summary>
		/// <param name="Z">The activated zone. Null is tolerated.</param>
		/// <returns>True if the seat moved.</returns>
		public bool TrySeat(Zone Z)
		{
			if (!Founded || Z == null || Away == null || ClaimedZones.Contains(Z.ZoneID) || !Away.ClaimedZones.Contains(Z.ZoneID))
			{
				return false;
			}
			KingdomSettlement wasSeated = Capture();
			Restore(Away);
			Away = wasSeated;
			if (KingdomLog.Enabled) KingdomLog.Log("seat moved to " + SeatName + " (" + Z.ZoneID + "); away is now " + Away.Describe());
			return true;
		}

		/// <summary>
		/// The realm's regard for its founder, read from the founder's own reputation with the
		/// realm's faction &mdash; the one number the world, the reputation screen and this system
		/// already agree on. No second economy is kept for it.
		/// </summary>
		/// <returns>Raw reputation on the vanilla scale; 0 when nothing is founded.</returns>
		public int FounderRegard()
		{
			return RegardWith(KingdomFactionName);
		}

		/// <summary>The expelled-from realm's regard for the founder, or 0 if there is none.</summary>
		public int ExiledRealmRegard()
		{
			return RegardWith(ExiledFactionName);
		}

		/// <summary>Whether the expelled-from realm holds this ground.</summary>
		/// <param name="ZoneID">A zone id. Null and empty read as false.</param>
		public bool ExiledRealmHolds(string ZoneID)
		{
			if (!Exiled || string.IsNullOrEmpty(ZoneID))
			{
				return false;
			}
			return (ExiledSeat != null && ExiledSeat.ClaimedZones.Contains(ZoneID))
				|| (ExiledAway != null && ExiledAway.ClaimedZones.Contains(ZoneID));
		}

		/// <summary>Whether the expelled-from realm kept ground the founder could walk back to.</summary>
		public bool ExiledRealmKeptGround => Exiled
			&& ((ExiledSeat != null && ExiledSeat.ClaimedZones.Count > 0)
				|| (ExiledAway != null && ExiledAway.ClaimedZones.Count > 0));

		/// <summary>
		/// Puts the founder out of the realm they founded.
		/// <para>
		/// Preconditions: a realm is founded, and either the regard has reached
		/// <see cref="RealmRegard.Repudiated"/> or <paramref name="Forced"/> is set. Side effects:
		/// the realm's identity, both of its cities and its whole standings ledger move to the
		/// exile slot, the Charter ability is taken from the founder, both chronicle registers
		/// record the day in their own words, and a modal states what has changed. Failure mode:
		/// returns false with a founder-facing refusal and changes nothing.
		/// </para>
		/// <para>
		/// Deliberately does <b>not</b> write reputation. The realm's grudge is whatever the
		/// founder's own deeds already put in the engine's reputation cell; manufacturing a worse
		/// one here would turn every citizen hostile and wall off the return path, which is the one
		/// thing this feature may not do.
		/// </para>
		/// </summary>
		/// <param name="Deed">The clause naming what was counted against the founder, from
		/// <see cref="KingdomExileRules.DeedClause"/>. Empty takes the unnamed-deed clause.</param>
		/// <param name="Forced">True for the debug path, which skips the regard requirement and
		/// nothing else.</param>
		/// <param name="Refusal">Founder-facing reason, or empty on success.</param>
		/// <returns>True if the founder was put out.</returns>
		public bool Exile(string Deed, bool Forced, out string Refusal)
		{
			Refusal = "";
			if (ExiledRealmArchive != null &&
				(ExiledRealmArchive.Phase == KingdomRealmArchivePhase.TradeClosed ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.MirrorsPublished ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.ChronicleFrozen ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.ChronicleCleared ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.Resetting))
			{
				return ContinueExileTransition(out Refusal);
			}
			ExileVerdict verdict = KingdomExileRules.JudgeExile(Founded, Exiled, KingdomExileRules.ClassifyRegard(FounderRegard()), Forced);
			if (verdict != ExileVerdict.Warranted)
			{
				Refusal = ExileRefusal(verdict);
				return false;
			}
			string realmName = KingdomDisplayName;
			string deed = string.IsNullOrEmpty(Deed) ? KingdomExileRules.DeedClause(null) : Deed;
			int cities = SettlementCount;
			string chronicleRegistry;
			string chronicleFault;
			string archiveFailure;
			List<string> exactSettlements;
			long proposedTick = The.Game.TimeTicks;
			if (!KingdomChronicle.TryCaptureRealmRegistry(out chronicleRegistry,
				out chronicleFault, out archiveFailure) ||
				!TryExactSettlementIds(RequirePublishedClaims: true,
					out exactSettlements, out archiveFailure))
			{
				Refusal = "The realm's exact history cannot be archived: " + archiveFailure + ".";
				return false;
			}
			List<string> authoritySettlements;
			if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
				IncludePending: false, out authoritySettlements, out archiveFailure))
			{
				Refusal = "The realm's retained authority topology cannot be archived: " +
					archiveFailure + ".";
				return false;
			}
			// A save may cut after Trade atomically unbound the realm but before Core published its
			// archive. Authenticate that exact receipt first and reuse its original close tick; the
			// current wall clock is never substitute authority on retry.
			if (TradeBook != null && !TradeBook.IdentityBound &&
				!KingdomTradeRules.TryAuthenticateExactExileClosedTick(TradeBook, RealmId,
					authoritySettlements, out proposedTick, out archiveFailure))
			{
				Refusal = "The settled Trade exile receipt cannot be authenticated: " +
					archiveFailure + ".";
				return false;
			}
			if (!KingdomRealmArchive.TryCapture(this, chronicleRegistry, chronicleFault,
				proposedTick, deed, out KingdomRealmArchive archive, out archiveFailure))
			{
				Refusal = "The realm graph cannot be captured exactly: " + archiveFailure + ".";
				return false;
			}
			if (!ExactArchivedSettlements(archive.RealmId, archive.Seat, archive.Away,
				archive.SettlementIds) || !archive.CurrentGraphMatches(this, out archiveFailure) ||
				!TryExactSettlementIds(RequirePublishedClaims: true,
					out List<string> preTradeSettlements, out archiveFailure) ||
				!ExactStringRows(preTradeSettlements, exactSettlements) ||
				!ExactStringRows(preTradeSettlements, archive.SettlementIds))
			{
				Refusal = "The complete realm graph or city identity set changed during archive preparation: " +
					(archiveFailure ?? "exact topology differs") + ".";
				return false;
			}
			// Trade is the first mutating boundary. Its detached preflight either refuses with
			// the entire Core/Trade graph unchanged, or atomically replaces only TradeBook with
			// the exact old-realm receipt. No Chronicle callback or exile mirror exists before it.
			if (!KingdomTrade.TryOnExile(this, proposedTick, archive.RealmId,
				authoritySettlements, out long settledTick, out archiveFailure))
			{
				Refusal = "Trade could not close the exact realm; no realm state was changed: " +
					archiveFailure;
				return false;
			}
			if (settledTick < 0L || (TradeBook == null || TradeBook.IdentityBound) ||
				!KingdomTradeRules.TryAuthenticateExactExileClosedTick(TradeBook, archive.RealmId,
					authoritySettlements, out long provedTick, out archiveFailure) ||
				provedTick != settledTick || !archive.CurrentGraphMatches(this, out archiveFailure) ||
				!TryExactSettlementIds(RequirePublishedClaims: true,
					out List<string> postTradeSettlements, out archiveFailure) ||
				!ExactStringRows(postTradeSettlements, exactSettlements) ||
				!ExactStringRows(postTradeSettlements, archive.SettlementIds))
			{
				Refusal = "Trade closed, but its exact settled tick or unchanged Core graph cannot be reproved: " +
					archiveFailure + ".";
				return false;
			}
			archive.ClosedTick = settledTick;
			archive.Phase = KingdomRealmArchivePhase.TradeClosed;
			ExiledRealmArchive = archive;
			if (!ContinueExileTransition(out Refusal))
			{
				return false;
			}
			KingdomLog.Log("exile: " + ExiledFactionName + " (" + cities + " cities, " + ExiledStandings.Count + " standings) put the founder out at regard " + ExiledRealmRegard() + "; deed=" + deed);
			Popup.Show(KingdomExileRules.ExileNotice(realmName, deed, cities));
			return true;
		}

		private bool ContinueExileTransition(out string Refusal)
		{
			Refusal = "";
			KingdomRealmArchive archive = ExiledRealmArchive;
			string failure = null;
			if (archive == null || archive.Quarantined || !archive.Validate(out failure))
			{
				if (archive != null && !archive.Quarantined)
					archive.Quarantine(failure ?? "exile mirrors differ from archive intent");
				Refusal = "The exiled realm archive requires inspection.";
				return false;
			}
			if (!TradeTransitionProofMatches(archive, RequireBound: false, out failure))
			{
				archive.Quarantine(failure ??
					"Trade exile receipt no longer matches the archived close tick");
				Refusal = "The settled Trade exile receipt requires inspection.";
				return false;
			}
			if (archive.Phase != KingdomRealmArchivePhase.TradeClosed &&
				archive.Phase != KingdomRealmArchivePhase.MirrorsPublished &&
				archive.Phase != KingdomRealmArchivePhase.ChronicleFrozen &&
				archive.Phase != KingdomRealmArchivePhase.ChronicleCleared &&
				archive.Phase != KingdomRealmArchivePhase.Resetting &&
				archive.Phase != KingdomRealmArchivePhase.Closed)
			{
				archive.Quarantine("persisted exile phase predates the transactional Trade boundary");
				Refusal = "The exiled realm archive carries an impossible transition phase and requires inspection.";
				return false;
			}
			if (!TryEnsureExileMirrors(archive,
				AllowCanonicalMissing: archive.Phase == KingdomRealmArchivePhase.TradeClosed,
				out failure) || !ExactExileMirrors(archive))
			{
				archive.Quarantine(failure ?? "exile mirrors differ from archive intent");
				Refusal = "The exiled realm mirrors require inspection.";
				return false;
			}
			if (archive.Phase == KingdomRealmArchivePhase.TradeClosed)
				archive.Phase = KingdomRealmArchivePhase.MirrorsPublished;
			if (archive.Phase == KingdomRealmArchivePhase.MirrorsPublished)
			{
				if (!DispatchExileChronicle(archive, out Refusal) ||
					!archive.Validate(out failure))
				{
					if (string.IsNullOrEmpty(Refusal))
						Refusal = "The realm chronicle could not freeze exactly: " + failure + ".";
					return false;
				}
				// Publish the exact clear before/after tuple before the first registry setter.
				// A save after either half of that two-key CAS resumes from these frozen bytes;
				// it never rebuilds a shorter registry from the lone exile event.
				archive.Phase = KingdomRealmArchivePhase.ChronicleFrozen;
			}
			if (archive.Phase == KingdomRealmArchivePhase.ChronicleFrozen)
			{
				if (!KingdomChronicle.TryClearRealmRegistry(archive.ChronicleRegistry,
					archive.ChronicleRegistryFault, out failure))
				{
					Refusal = "The realm chronicle could not close exactly: " + failure + ".";
					return false;
				}
				archive.Phase = KingdomRealmArchivePhase.ChronicleCleared;
			}
			if (archive.Phase == KingdomRealmArchivePhase.ChronicleCleared)
			{
				if (!DispatchExileAbilityRemoval(archive, out Refusal)) return false;
				archive.Phase = KingdomRealmArchivePhase.Resetting;
			}
			if (archive.Phase == KingdomRealmArchivePhase.Resetting)
			{
				ResetCurrentRealmAfterExile();
				archive.Phase = KingdomRealmArchivePhase.Closed;
			}
			return archive.Phase == KingdomRealmArchivePhase.Closed;
		}

		private bool DispatchExileChronicle(KingdomRealmArchive Archive,
			out string Refusal)
		{
			string eventId = "taf:realm:exile:v1:" + Archive.RealmId;
			string telling = KingdomExileRules.ExileTelling(Archive.DisplayName,
				Archive.ExileDeed);
			return DispatchRealmChronicle(Archive, Archive.ExileChronicle, eventId, telling,
				"exile", out Refusal);
		}

		private bool DispatchRealmChronicle(KingdomRealmArchive Archive,
			KingdomRealmCallbackReceipt Receipt, string EventId, string Telling,
			string Context, out string Refusal)
		{
			Refusal = "";
			if (!KingdomChronicleReceiptRules.TryFingerprint(EventId, Telling, true, null,
				out string fingerprint) || !TryInspectChronicle(EventId, fingerprint,
				out string registryHash, out bool present, out bool terminal, out bool lost,
				out bool conflict, out string registry, out string registryFault,
				out string otherRegistryHash, out KingdomChronicleReceipt eventReceipt))
				return QuarantineReturn(Archive, Context + " Chronicle cannot be inspected", out Refusal);
			string expected = EventId + "|" + fingerprint;
			KingdomChronicleDeclaration declaration;
			string frozenRegistryHash;
			string frozenOtherHash;
			string frozenRegistryFault;
			string before;
			if (Receipt.Phase == KingdomRealmCallbackPhase.None)
			{
				if (present)
					return QuarantineReturn(Archive, Context +
						" Chronicle row exists without outer declaration intent", out Refusal);
				if (!KingdomChronicle.TryDeclareOnce(this, EventId, Telling, true, null,
					out declaration) || declaration.Fingerprint != fingerprint ||
					!TryCreateChronicleIntent(EventId, declaration, registryHash,
						otherRegistryHash, registryFault, out before))
					return QuarantineReturn(Archive, Context +
						" Chronicle declaration cannot be frozen", out Refusal);
				frozenRegistryHash = registryHash; frozenOtherHash = otherRegistryHash;
				frozenRegistryFault = registryFault;
			}
			else if (!TryParseChronicleIntent(Receipt.BeforeEffect, EventId, Telling, true,
				null, out declaration, out frozenRegistryHash, out frozenOtherHash,
				out frozenRegistryFault))
				return QuarantineReturn(Archive, Context +
					" Chronicle declaration receipt is malformed", out Refusal);
			else before = Receipt.BeforeEffect;
			if (Receipt.Phase != KingdomRealmCallbackPhase.None && Receipt.AfterEffect != expected)
				return QuarantineReturn(Archive,
					Context + " Chronicle intent conflicts with frozen content", out Refusal);
			if (!ChronicleDeclarationMatchesArchive(Archive, declaration, out string proofFailure) ||
				conflict || otherRegistryHash != frozenOtherHash ||
				!TryValidateChronicleLists(declaration, eventReceipt, present, terminal,
					out string officialHash, out string outsiderHash, out bool listLost) ||
				!KingdomRealmCallbackProofRules.ChronicleFaultMatches(present, terminal,
					eventReceipt == null ? KingdomChronicleSinkDisposition.Pending :
						eventReceipt.OfficialState,
					eventReceipt == null ? KingdomChronicleSinkDisposition.Pending :
						eventReceipt.OutsiderState,
					eventReceipt == null ? KingdomChronicleSinkDisposition.Pending :
						eventReceipt.JournalState, registryFault, frozenRegistryFault))
				return QuarantineReturn(Archive, proofFailure ?? Context +
					" Chronicle lists or unrelated rows reached a third state", out Refusal);
			string observed = terminal ? ChronicleObserved(registryHash, otherRegistryHash,
				officialHash, outsiderHash, eventReceipt) : null;
			if (Receipt.Phase == KingdomRealmCallbackPhase.Settled)
				return terminal && EnsureArchiveChronicleState(Archive, declaration,
					eventReceipt, registry, registryFault, frozenRegistryHash, out Refusal) &&
					SettledCallbackStillMatches(Archive, Receipt, observed, out Refusal);
			if (!PrepareReturnCallback(Archive, Receipt, KingdomRealmCallbackScope.Chronicle,
				before, expected,
				out bool invokeAuthorized, out Refusal)) return false;
			if (!present && (registryHash != frozenRegistryHash ||
				officialHash != declaration.OfficialBefore ||
				outsiderHash != declaration.OutsiderBefore))
				return QuarantineReturn(Archive,
					Context + " Chronicle reached a third prestate", out Refusal);
			if (!terminal)
			{
				if (!present && !invokeAuthorized)
					return QuarantineReturn(Archive,
						Context + " Chronicle callback was interrupted before receipt publication",
						out Refusal);
				if (!Archive.CurrentGraphMatchesExceptChronicle(this,
					out string graphFailure) || (!present &&
					(!KingdomRealmArchive.TryCurrentGraphHash(this, out string graph,
						out graphFailure) || graph != Receipt.BeforeGraph)))
					return QuarantineReturn(Archive, graphFailure ??
						Context + " Chronicle Core graph changed before callback", out Refusal);
				List<string> officialReference = ChronicleEntries;
				List<string> outsiderReference = OutsiderEntries;
				if (!KingdomChronicle.RecordDeclaredOnce(this, declaration) ||
					!ReferenceEquals(officialReference, ChronicleEntries) ||
					!ReferenceEquals(outsiderReference, OutsiderEntries) ||
					!Archive.CurrentGraphMatchesExceptChronicle(this, out graphFailure))
				{
					Refusal = "The " + Context + " telling remains in its exact Chronicle receipt.";
					return false;
				}
				if (!TryInspectChronicle(EventId, fingerprint, out registryHash, out present,
					out terminal, out lost, out conflict, out registry, out registryFault,
					out otherRegistryHash, out eventReceipt) || conflict || !terminal ||
					otherRegistryHash != frozenOtherHash ||
					!TryValidateChronicleLists(declaration, eventReceipt, true, true,
						out officialHash, out outsiderHash, out listLost) ||
					!KingdomRealmCallbackProofRules.ChronicleFaultMatches(true, true,
						eventReceipt.OfficialState, eventReceipt.OutsiderState,
						eventReceipt.JournalState, registryFault, frozenRegistryFault))
					return QuarantineReturn(Archive,
						Context + " Chronicle callback lacks exact terminal proof", out Refusal);
			}
			if (!EnsureArchiveChronicleState(Archive, declaration, eventReceipt, registry,
				registryFault, frozenRegistryHash, out Refusal)) return false;
			observed = ChronicleObserved(registryHash, otherRegistryHash, officialHash,
				outsiderHash, eventReceipt);
			return SettleReturnCallback(Archive, Receipt, (listLost ||
				eventReceipt.JournalState == KingdomChronicleSinkDisposition.Lost || lost)
				? KingdomRealmCallbackDisposition.Lost
				: KingdomRealmCallbackDisposition.Delivered,
				observed, out Refusal);
		}

		private bool DispatchExileAbilityRemoval(KingdomRealmArchive Archive,
			out string Refusal)
		{
			Refusal = "";
			if (!TryObserveCharterAbility(out CharterAbilityObservation observation))
				return QuarantineReturn(Archive, "charter removal graph cannot be bounded",
					out Refusal);
			KingdomRealmCallbackReceipt receipt = Archive.ExileAbility;
			string before = receipt.Phase == KingdomRealmCallbackPhase.None
				? AbilityEffect(observation) : receipt.BeforeEffect;
			string after = receipt.Phase == KingdomRealmCallbackPhase.None
				? AbilityIntent(observation.StableHash, observation.TargetTemplateHash,
					observation.State == "player-absent" ? "player-absent" : "removed")
				: receipt.AfterEffect;
			if (!TryParseAbilityEffect(before, out string beforeFull, out string frozenStable,
				out string frozenTemplate, out string beforeState) ||
				!TryParseAbilityEffect(after, out string ignoredFull, out string expectedStable,
					out string expectedTemplate, out string expectedState) ||
				frozenStable != expectedStable || frozenTemplate != expectedTemplate ||
				(expectedState != "removed" && expectedState != "player-absent"))
				return QuarantineReturn(Archive, "charter removal intent is malformed",
					out Refusal);
			if (receipt.Phase == KingdomRealmCallbackPhase.Settled)
				return observation.State == expectedState &&
					observation.StableHash == frozenStable &&
					SettledCallbackStillMatches(Archive, receipt,
						AbilityEffect(observation), out Refusal);
			if (!PrepareReturnCallback(Archive, receipt, KingdomRealmCallbackScope.Ability,
				before, after,
				out bool invokeAuthorized, out Refusal)) return false;
			if (!TryObserveCharterAbility(out observation) ||
				observation.StableHash != frozenStable)
				return QuarantineReturn(Archive,
					"charter removal changed unaffected ability or part graph", out Refusal);
			string current = AbilityEffect(observation);
			if (observation.State == expectedState)
				return SettleReturnCallback(Archive, receipt,
					before == current
						? KingdomRealmCallbackDisposition.Skipped
						: KingdomRealmCallbackDisposition.Delivered,
					current, out Refusal);
			if (!observation.Recoverable || current != before ||
				observation.State != beforeState || observation.FullHash != beforeFull)
				return QuarantineReturn(Archive, "charter removal found duplicate ability state",
					out Refusal);
			if (!invokeAuthorized)
				return QuarantineReturn(Archive,
					"charter removal was interrupted before exact poststate publication", out Refusal);
			if (!TryCaptureCharterReferences(out CharterReferenceSnapshot charterReferences))
				return QuarantineReturn(Archive, "charter removal reference graph is unbounded",
					out Refusal);
			The.Player?.GetPart<KingdomCharterPart>()?.RemoveAbility();
			if (!TryObserveCharterAbility(out observation) ||
				!CharterReferencesStillMatch(charterReferences, AllowPartCreation: false) ||
				observation.StableHash != frozenStable || observation.State != expectedState)
				return QuarantineReturn(Archive,
					"charter removal callback did not settle exact absence", out Refusal);
			return SettleReturnCallback(Archive, receipt,
				KingdomRealmCallbackDisposition.Delivered,
				AbilityEffect(observation), out Refusal);
		}

		private static bool CharterAbilityRemoved()
		{
			GameObject player = The.Player;
			if (player == null) return true;
			int partCount = 0;
			KingdomCharterPart part = null;
			for (int i = 0; i < player.PartsList.Count; i++)
			{
				IPart candidate = player.PartsList[i];
				if (candidate != null && candidate.GetType().Name == "KingdomCharterPart")
				{
					partCount++;
					if (candidate is KingdomCharterPart typed) part = typed;
				}
			}
			if (partCount > 1 || (partCount == 1 && (part == null ||
				!ReferenceEquals(part.ParentObject, player))) ||
				(part != null && part.ActivatedAbilityID != Guid.Empty)) return false;
			System.Collections.Generic.Dictionary<Guid, ActivatedAbilityEntry> abilities =
				player.ActivatedAbilities?.AbilityByGuid;
			if (abilities == null) return true;
			foreach (KeyValuePair<Guid, ActivatedAbilityEntry> row in abilities)
				if (row.Value != null && row.Value.Command == KingdomCharterPart.COMMAND) return false;
			return true;
		}

		private bool ExactExileMirrors(KingdomRealmArchive Archive)
		{
			if (Archive == null || ExiledSeat == null || string.IsNullOrEmpty(ExiledFactionName)
				|| ExiledStandings == null) return false;
			string failure;
			if (!Archive.ExactMirrors(ExiledFactionName, ExiledDisplayName, ExiledDeed,
				ExiledTick, ExiledSeat, ExiledAway, ExiledStandings, out failure)) return false;
			if (!ExactArchivedSettlements(Archive.RealmId, ExiledSeat, ExiledAway,
				Archive.SettlementIds)) return false;
			KingdomSettlement currentSeat;
			try { currentSeat = Capture(); }
			catch { return false; }
			object[] currentRoots = { currentSeat, Away, Seceded, Standings, Bindings, Jobs,
				ChronicleEntries, OutsiderEntries, Haul, CarryBook };
			object[] mirrorRoots = { ExiledSeat, ExiledAway, ExiledStandings };
			return KingdomArchivedSettlementCodec.DisjointMutableGraphs(currentRoots,
				mirrorRoots, out failure);
		}

		/// <summary>Clears the published exile mirror by exact-or-cleared CAS. Each assignment may
		/// be a save cut: a retry accepts the archive value or its canonical cleared value only.</summary>
		private bool TryClearExileMirrors(KingdomRealmArchive Archive, out string Failure)
		{
			Failure = null;
			if (Archive == null ||
				!ClearMirrorString(ref ExiledFactionName, Archive.FactionName) ||
				!ClearMirrorString(ref ExiledDisplayName, Archive.DisplayName) ||
				!ClearSettlementMirror(ref ExiledSeat, Archive.Seat, out Failure) ||
				!ClearSettlementMirror(ref ExiledAway, Archive.Away, out Failure) ||
				!ClearStandingsMirror(Archive.Standings, out Failure) ||
				!ClearMirrorString(ref ExiledDeed, Archive.ExileDeed) ||
				!ClearMirrorTick(ref ExiledTick, Archive.ClosedTick))
			{
				Failure = Failure ?? "return cleanup mirror reached a third value";
				return false;
			}
			return true;
		}

		private static bool ClearMirrorString(ref string Current, string Expected)
		{
			if (Current == null) return true;
			if (!string.Equals(Current, Expected, StringComparison.Ordinal)) return false;
			Current = null;
			return true;
		}

		private static bool ClearMirrorTick(ref long Current, long Expected)
		{
			if (Current == 0L) return true;
			if (Current != Expected) return false;
			Current = 0L;
			return true;
		}

		private static bool ClearSettlementMirror(ref KingdomSettlement Current,
			KingdomSettlement Expected, out string Failure)
		{
			Failure = null;
			if (Current == null) return true;
			if (Expected == null ||
				!KingdomArchivedSettlementCodec.ExactGraph(Expected, Current, out Failure) ||
				!KingdomArchivedSettlementCodec.DisjointMutableGraphs(
					new object[] { Expected }, new object[] { Current }, out Failure)) return false;
			Current = null;
			return true;
		}

		private bool ClearStandingsMirror(Dictionary<string, int> Expected,
			out string Failure)
		{
			Failure = null;
			if (ExiledStandings == null)
			{
				Failure = "return cleanup standings mirror is null";
				return false;
			}
			if (ReferenceEquals(Expected, ExiledStandings))
			{
				Failure = "return cleanup standings mirror aliases archive";
				return false;
			}
			if (ExiledStandings.Count == 0) return true;
			if (Expected == null ||
				!KingdomRealmArchive.ExactDictionary(Expected, ExiledStandings))
			{
				Failure = "return cleanup standings mirror reached a third value or alias";
				return false;
			}
			ExiledStandings = new Dictionary<string, int>();
			return true;
		}

		/// <summary>Completes only canonical missing writes from the authoritative TradeClosed
		/// archive. A third scalar, partial collection, or non-equal graph is never overwritten.</summary>
		private bool TryEnsureExileMirrors(KingdomRealmArchive Archive,
			bool AllowCanonicalMissing, out string Failure)
		{
			Failure = null;
			if (Archive == null) { Failure = "exile archive is absent"; return false; }
			if (!EnsureMirrorString(ref ExiledFactionName, Archive.FactionName,
				AllowCanonicalMissing) ||
				!EnsureMirrorString(ref ExiledDisplayName, Archive.DisplayName,
					AllowCanonicalMissing) ||
				!EnsureMirrorString(ref ExiledDeed, Archive.ExileDeed, AllowCanonicalMissing) ||
				!EnsureMirrorTick(ref ExiledTick, Archive.ClosedTick, AllowCanonicalMissing))
			{
				Failure = "exile scalar mirror reached a third value";
				return false;
			}
			if (!EnsureSettlementMirror(ref ExiledSeat, Archive.Seat, AllowCanonicalMissing,
				out Failure) || !EnsureSettlementMirror(ref ExiledAway, Archive.Away,
					AllowCanonicalMissing, out Failure)) return false;
			if (ExiledStandings == null ||
				(AllowCanonicalMissing && ExiledStandings.Count == 0 && Archive.Standings.Count != 0))
			{
				if (!AllowCanonicalMissing)
				{
					Failure = "exile standings mirror is absent";
					return false;
				}
				ExiledStandings = KingdomRealmArchive.CloneStandings(Archive.Standings);
			}
			else if (!KingdomRealmArchive.ExactDictionary(Archive.Standings, ExiledStandings))
			{
				Failure = "exile standings mirror reached a third value";
				return false;
			}
			return true;
		}

		private static bool EnsureMirrorString(ref string Current, string Expected,
			bool AllowCanonicalMissing)
		{
			if (string.Equals(Current, Expected, StringComparison.Ordinal)) return true;
			if (!AllowCanonicalMissing || Current != null) return false;
			Current = Expected;
			return true;
		}

		private static bool EnsureMirrorTick(ref long Current, long Expected,
			bool AllowCanonicalMissing)
		{
			if (Current == Expected) return true;
			if (!AllowCanonicalMissing || Current != 0L) return false;
			Current = Expected;
			return true;
		}

		private static bool EnsureSettlementMirror(ref KingdomSettlement Current,
			KingdomSettlement Expected, bool AllowCanonicalMissing, out string Failure)
		{
			Failure = null;
			if (Expected == null) return Current == null;
			if (Current == null)
			{
				if (!AllowCanonicalMissing)
				{
					Failure = "exile settlement mirror is absent";
					return false;
				}
				return KingdomArchivedSettlementCodec.TryClone(Expected, out Current, out Failure);
			}
			return KingdomArchivedSettlementCodec.ExactGraph(Expected, Current, out Failure);
		}

		private static bool ExactArchivedSettlements(string RealmId,
			KingdomSettlement Seat, KingdomSettlement Away,
			IList<string> ExpectedIds = null)
		{
			List<string> ids = new List<string>();
			if (!ArchivedSettlementMatches(RealmId, Seat, out string seatId))
				return false;
			ids.Add(seatId);
			if (Away != null)
			{
				if (!ArchivedSettlementMatches(RealmId, Away, out string awayId))
					return false;
				ids.Add(awayId);
			}
			KingdomIdentityFault fault;
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, ids, out fault)) return false;
			ids.Sort(StringComparer.Ordinal);
			if (ExpectedIds == null || ids.Count != ExpectedIds.Count) return ExpectedIds == null;
			for (int i = 0; i < ids.Count; i++)
				if (!string.Equals(ids[i], ExpectedIds[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool ArchivedSettlementMatches(string RealmId,
			KingdomSettlement Settlement, out string SettlementId)
		{
			SettlementId = Settlement?.City?.SettlementId;
			KingdomIdentityFault fault;
			return Settlement != null && Settlement.ClaimedZones != null &&
				Settlement.ClaimedZones.Contains(Settlement.SettlementIdentityFirstClaimedZone) &&
				KingdomIdentityRules.ReproveSettlement(SettlementId, RealmId,
					Settlement.SettlementIdentityVersion, Settlement.SettlementIdentityOrigin,
					Settlement.SettlementIdentityTransactionId,
					Settlement.SettlementIdentityFoundedTick,
					Settlement.SettlementIdentityFirstClaimedZone, out fault) &&
				Settlement.LifecycleBook != null && !Settlement.LifecycleBook.LegacyIdentity &&
				string.Equals(Settlement.LifecycleBook.SettlementId, SettlementId,
					StringComparison.Ordinal) &&
				KingdomLifecycleRules.CanOwnAuthority(Settlement.LifecycleBook);
		}

		private void ResetCurrentRealmAfterExile()
		{
			KingdomFactionName = null;
			KingdomDisplayName = null;
			Restore(new KingdomSettlement());
			Away = null;
			Standings = new Dictionary<string, int>();
			RealmId = null;
			RealmIdentityVersion = 0;
			RealmIdentityOrigin = KingdomIdentityOrigin.None;
			RealmIdentityTransactionId = null;
			RealmIdentityLegacyFaction = null;
			RealmIdentityFoundedTick = 0L;
			RealmIdentitySeedHigh = 0UL;
			RealmIdentitySeedLow = 0UL;
			RealmIdentityFirstClaimedZone = null;
			IdentityFault = null;
			PendingSettlementId = null;
			PendingSettlementTransactionId = null;
			PendingSettlementZoneId = null;
			PendingSettlementAuthority = null;
			SimulationSeedHigh = 0UL;
			SimulationSeedLow = 0UL;
			Bindings = new Simulation.City.KingdomBindingRegistry();
			ResidentCounter = 0;
			Jobs = new Simulation.City.KingdomJobRegistry();
			LastSliceTick = 0L;
			ReifyTick = 0L;
			ReifyThirdsSpent = 0;
			ReifyHeavySpent = 0;
			ReifyQuietUntilTick = 0L;
			DedicationCounter = 0;
			ChronicleEntries = new List<string>();
			OutsiderEntries = new List<string>();
			RegardSpoken = (int)RealmRegard.Beloved;
			Dissent = 0;
			DissentSpoken = 0;
			LastDissentTick = 0L;
			DeclaredCreed = null;
			DishName = null;
			DishText = null;
			DishStaple = null;
			DishSource = null;
			LastRiteTick = 0L;
			LastSoulRiteTick = 0L;
			Seceded = null;
			SecededTick = 0L;
			Haul = null;
			CarryBook = new KingdomCarryBook();
			ReturnAskedRegard = int.MinValue;
			DoorClosedTold = false;
		}

		/// <summary>
		/// Asks the realm that expelled the founder to take them back.
		/// <para>
		/// Preconditions: an expulsion is on the record, no realm has been founded since, the
		/// founder is standing on the old realm's own ground, and its regard for them is no longer
		/// <see cref="RealmRegard.Repudiated"/>. Side effects: the realm, both of its cities and
		/// its standings ledger are restored exactly as they stood, regard is raised to the
		/// indifference floor if it stands below it, the Charter comes back, and both registers
		/// record the day. Failure mode: returns false with a founder-facing refusal and changes
		/// nothing.
		/// </para>
		/// </summary>
		/// <param name="Site">The zone the founder is standing in. Null reads as the wrong ground.</param>
		/// <param name="Refusal">Founder-facing reason, or empty on success.</param>
		/// <returns>True if the founder was taken back.</returns>
		public bool TryReturn(Zone Site, out string Refusal)
		{
			Refusal = "";
			if (ExiledRealmArchive != null &&
				(ExiledRealmArchive.Phase == KingdomRealmArchivePhase.Restoring ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.Restored ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.ReturnCleaning))
				return ContinueReturnTransition(Site, out Refusal);
			int regard = ExiledRealmRegard();
			ReturnVerdict verdict = KingdomExileRules.JudgeReturn(Exiled, Founded, ExiledRealmKeptGround, Site != null && ExiledRealmHolds(Site.ZoneID), regard);
			if (verdict != ReturnVerdict.Allowed)
			{
				Refusal = KingdomExileRules.ReturnRefusal(verdict, ExiledDisplayName, KingdomDisplayName);
				return false;
			}
			KingdomRealmArchive archive = ExiledRealmArchive;
			string failure = null;
			if (archive == null || archive.Phase != KingdomRealmArchivePhase.Closed ||
				archive.Quarantined || !archive.Validate(out failure) ||
				!ExactExileMirrors(archive))
			{
				if (archive != null && !archive.Quarantined)
					archive.Quarantine(failure ?? "return mirrors differ from archived identity");
				Refusal = "The exiled realm archive cannot be reproved and requires inspection.";
				return false;
			}
			if (!CurrentRealmIsCanonicalBlank(archive))
			{
				archive.Quarantine("return found a third current-realm identity before intent");
				Refusal = "A different current realm state blocks exact return.";
				return false;
			}
			if (TradeBook == null || TradeBook.IdentityBound ||
				!KingdomTradeRules.TryAuthenticateExactExileClosedTick(TradeBook, archive.RealmId,
				archive.SettlementIds, out long provedClosedTick, out failure) ||
				provedClosedTick != archive.ClosedTick)
			{
				Refusal = "The settled Trade exile receipt cannot authorize return: " +
					(failure ?? "close tick differs") + ".";
				return false;
			}
			archive.ReturnRegard = KingdomExileRules.RegardOnReturn(regard);
			archive.Phase = KingdomRealmArchivePhase.Restoring;
			return ContinueReturnTransition(Site, out Refusal);
		}

		private bool ContinueReturnTransition(Zone Site, out string Refusal)
		{
			Refusal = "";
			KingdomRealmArchive archive = ExiledRealmArchive;
			string failure = null;
			if (archive == null || archive.Quarantined ||
				(archive.Phase != KingdomRealmArchivePhase.Restoring &&
				 archive.Phase != KingdomRealmArchivePhase.Restored &&
				 archive.Phase != KingdomRealmArchivePhase.ReturnCleaning) ||
				!archive.Validate(out failure) ||
				archive.ReturnRegard == int.MinValue)
			{
				if (archive != null && !archive.Quarantined)
					archive.Quarantine(failure ?? "return receipt or exact mirrors are malformed");
				Refusal = "The exiled realm return receipt requires inspection.";
				return false;
			}
			if (archive.Phase == KingdomRealmArchivePhase.ReturnCleaning)
			{
				if (!archive.CurrentGraphMatches(this, out failure) ||
					!TradeTransitionProofMatches(archive, RequireBound: true, out failure))
					return QuarantineReturn(archive, failure ??
						"return cleanup authority no longer matches", out Refusal);
				return FinishReturnCleanup(archive, out Refusal);
			}
			if (!ExactExileMirrors(archive))
				return QuarantineReturn(archive, "return mirrors differ from archive intent",
					out Refusal);
			if (TradeBook == null || TradeBook.IdentityBound ||
				!KingdomTradeRules.TryAuthenticateExactExileClosedTick(TradeBook, archive.RealmId,
				archive.SettlementIds, out long provedClosedTick, out failure) ||
				provedClosedTick != archive.ClosedTick)
				return QuarantineReturn(archive, failure ??
					"Trade exile receipt no longer matches the archived close tick", out Refusal);
			if (archive.Phase == KingdomRealmArchivePhase.Restoring)
			{
				if (!RestoreArchivedRealmCore(archive, out failure) ||
					!KingdomChronicle.TryRestoreRealmRegistry(archive.ChronicleRegistry,
					archive.ChronicleRegistryFault, out failure) ||
					!TryBindTradeIdentity(out failure) ||
					!TradeTransitionProofMatches(archive, RequireBound: true, out failure) ||
					!CurrentRealmMatchesArchive(archive))
				{
					Refusal = "The archived realm did not restore exactly: " + failure + ".";
					return false;
				}
				archive.Phase = KingdomRealmArchivePhase.Restored;
			}
			return FinishReturnedRealm(Site, archive, out Refusal);
		}

		private bool FinishReturnedRealm(Zone Site, KingdomRealmArchive Archive,
			out string Refusal)
		{
			Refusal = "";
			if (!DispatchReturnChronicle(Archive, out Refusal) ||
				!DispatchReturnReputation(Archive, out Refusal) ||
				!DispatchReturnFeelings(Archive, out Refusal) ||
				!DispatchReturnSeat(Site, Archive, out Refusal) ||
				!DispatchReturnAbility(Archive, out Refusal)) return false;
			string factionName = KingdomFactionName;
			string seatName = SeatName;
			string displayName = KingdomDisplayName;
			int restored = Archive.ReturnRegard;
			Archive.Phase = KingdomRealmArchivePhase.ReturnCleaning;
			return FinishReturnCleanup(Archive, out Refusal, factionName, seatName,
				displayName, restored);
		}

		private bool FinishReturnCleanup(KingdomRealmArchive Archive, out string Refusal,
			string FactionName = null, string SeatNameValue = null,
			string DisplayName = null, int Restored = int.MinValue)
		{
			Refusal = "";
			if (Archive == null || Archive.Phase != KingdomRealmArchivePhase.ReturnCleaning)
				return false;
			if (FactionName == null) FactionName = KingdomFactionName;
			if (SeatNameValue == null) SeatNameValue = SeatName;
			if (DisplayName == null) DisplayName = KingdomDisplayName;
			if (Restored == int.MinValue) Restored = Archive.ReturnRegard;
			if (!TryClearExileMirrors(Archive, out string failure))
				return QuarantineReturn(Archive, failure, out Refusal);
			ReturnAskedRegard = int.MinValue;
			DoorClosedTold = false;
			ExiledRealmArchive = null;
			KingdomLog.Log("return: " + FactionName + " took the founder back -> " + Restored
				+ "; seated " + SeatNameValue);
			Popup.Show(KingdomExileRules.ReturnNotice(DisplayName, SeatNameValue));
			return true;
		}

		private bool PrepareReturnCallback(KingdomRealmArchive Archive,
			KingdomRealmCallbackReceipt Receipt, KingdomRealmCallbackScope Scope,
			string BeforeEffect, string AfterEffect,
			out bool InvokeAuthorized, out string Refusal,
			int BeforeStamp = int.MinValue, int AfterStamp = int.MinValue)
		{
			InvokeAuthorized = false;
			Refusal = "";
			if (Archive == null || Receipt == null || Scope == KingdomRealmCallbackScope.None ||
				BeforeEffect == null || AfterEffect == null ||
				BeforeEffect.Length > KingdomRealmCallbackReceipt.MaxEffectChars ||
				AfterEffect.Length > KingdomRealmCallbackReceipt.MaxEffectChars)
				return QuarantineReturn(Archive, "callback intent is unbounded", out Refusal);
			if (Receipt.Phase == KingdomRealmCallbackPhase.None)
			{
				if (!Archive.CurrentGraphMatches(this, out string failure) ||
					!ExactExileMirrors(Archive) ||
					!TradeTransitionProofMatches(Archive,
						RequireBound: ReturnCallbackTradeBound(Archive), out failure) ||
					!KingdomRealmArchive.TryCurrentGraphHash(this, out string graph, out failure) ||
					!Archive.TryAuthorityHash(Receipt, Scope, out string archiveGraph, out failure))
					return QuarantineReturn(Archive, failure, out Refusal);
				Receipt.Scope = Scope;
				Receipt.BeforeGraph = graph;
				Receipt.BeforeArchiveGraph = archiveGraph;
				Receipt.BeforeEffect = BeforeEffect;
				Receipt.AfterEffect = AfterEffect;
				Receipt.BeforeStamp = BeforeStamp;
				Receipt.AfterStamp = AfterStamp;
				Receipt.Phase = KingdomRealmCallbackPhase.Intent;
			}
			if (!Receipt.Validate() || Receipt.Scope != Scope || Receipt.BeforeEffect != BeforeEffect ||
				Receipt.AfterEffect != AfterEffect || Receipt.BeforeStamp != BeforeStamp ||
				Receipt.AfterStamp != AfterStamp)
				return QuarantineReturn(Archive, "callback receipt conflicts with frozen intent",
					out Refusal);
			if (Receipt.Phase == KingdomRealmCallbackPhase.Intent)
			{
				if (!Archive.CurrentGraphMatches(this, out string failure) ||
					!ExactExileMirrors(Archive) ||
					!TradeTransitionProofMatches(Archive,
						RequireBound: ReturnCallbackTradeBound(Archive), out failure) ||
					!KingdomRealmArchive.TryCurrentGraphHash(this, out string graph, out failure) ||
					!Archive.TryAuthorityHash(Receipt, Scope, out string archiveGraph, out failure) ||
					!string.Equals(graph, Receipt.BeforeGraph, StringComparison.Ordinal) ||
					!string.Equals(archiveGraph, Receipt.BeforeArchiveGraph,
						StringComparison.Ordinal))
					return QuarantineReturn(Archive,
						failure ?? "callback graph changed before attempt", out Refusal);
				Receipt.Phase = KingdomRealmCallbackPhase.Attempting;
				InvokeAuthorized = true;
			}
			return true;
		}

		private bool SettleReturnCallback(KingdomRealmArchive Archive,
			KingdomRealmCallbackReceipt Receipt, KingdomRealmCallbackDisposition Disposition,
			string ObservedEffect, out string Refusal, bool SeatSwapped = false)
		{
			Refusal = "";
			string failure = null;
			string graph = null;
			string archiveGraph = null;
			if (Receipt == null || Receipt.Phase != KingdomRealmCallbackPhase.Attempting ||
				Disposition == KingdomRealmCallbackDisposition.None ||
				ObservedEffect == null ||
				ObservedEffect.Length > KingdomRealmCallbackReceipt.MaxEffectChars ||
				!(SeatSwapped ? Archive.CurrentGraphMatchesAfterSeat(this, true, out failure) :
					Archive.CurrentGraphMatches(this, out failure)) ||
				!ExactExileMirrors(Archive) ||
				!TradeTransitionProofMatches(Archive,
					RequireBound: ReturnCallbackTradeBound(Archive), out failure) ||
				!KingdomRealmArchive.TryCurrentGraphHash(this, out graph, out failure) ||
				!Archive.TryAuthorityHash(Receipt, Receipt.Scope, out archiveGraph, out failure) ||
				!string.Equals(archiveGraph, Receipt.BeforeArchiveGraph,
					StringComparison.Ordinal) ||
				((Receipt.Scope == KingdomRealmCallbackScope.Ability ||
				  Receipt.Scope == KingdomRealmCallbackScope.Reputation) &&
				 !string.Equals(graph, Receipt.BeforeGraph, StringComparison.Ordinal)))
				return QuarantineReturn(Archive, failure ?? "callback could not settle exact graph",
					out Refusal);
			Receipt.AfterGraph = graph;
			Receipt.AfterArchiveGraph = archiveGraph;
			Receipt.ObservedEffect = ObservedEffect;
			Receipt.Disposition = Disposition;
			Receipt.Phase = KingdomRealmCallbackPhase.Settled;
			return Receipt.Validate();
		}

		private bool SettledCallbackStillMatches(KingdomRealmArchive Archive,
			KingdomRealmCallbackReceipt Receipt, string ObservedEffect, out string Refusal)
		{
			Refusal = "";
			string failure = null;
			if (Receipt == null || !Receipt.Validate() ||
				Receipt.Phase != KingdomRealmCallbackPhase.Settled ||
				!string.Equals(ObservedEffect, Receipt.ObservedEffect, StringComparison.Ordinal) ||
				!Archive.CurrentGraphMatches(this, out failure) ||
				!ExactExileMirrors(Archive) ||
				!TradeTransitionProofMatches(Archive,
					RequireBound: ReturnCallbackTradeBound(Archive), out failure) ||
				!KingdomRealmArchive.TryCurrentGraphHash(this, out string graph, out failure) ||
				!Archive.TryAuthorityHash(Receipt, Receipt.Scope, out string archiveGraph, out failure) ||
				!string.Equals(graph, Receipt.AfterGraph, StringComparison.Ordinal) ||
				!string.Equals(archiveGraph, Receipt.AfterArchiveGraph,
					StringComparison.Ordinal))
				return QuarantineReturn(Archive, failure ??
					"settled callback proof no longer matches exact poststate", out Refusal);
			return true;
		}

		private bool TradeTransitionProofMatches(KingdomRealmArchive Archive,
			bool RequireBound, out string Failure)
		{
			Failure = null;
			if (Archive == null || TradeBook == null ||
				!KingdomTradeRules.TryAuthenticateExactExileClosedTick(TradeBook, Archive.RealmId,
					Archive.SettlementIds, out long closedTick, out Failure) ||
				closedTick != Archive.ClosedTick)
			{
				Failure = Failure ?? "Trade exile receipt differs from archive";
				return false;
			}
			if (!RequireBound)
			{
				if (!TradeBook.IdentityBound) return true;
				Failure = "Trade must remain unbound before returned realm publication";
				return false;
			}
			if (!KingdomTradeRules.BookUsable(TradeBook) ||
				!string.Equals(TradeBook.RealmId, Archive.RealmId, StringComparison.Ordinal) ||
				TradeBook.SettlementIds == null ||
				TradeBook.SettlementIds.Count != Archive.SettlementIds.Count)
			{
				Failure = "Trade is not bound to the returned exact realm topology";
				return false;
			}
			for (int i = 0; i < Archive.SettlementIds.Count; i++)
				if (!string.Equals(TradeBook.SettlementIds[i], Archive.SettlementIds[i],
					StringComparison.Ordinal))
				{
					Failure = "Trade returned settlement topology differs from archive";
					return false;
				}
			return true;
		}

		private static bool ExactStringRows(List<string> Left, List<string> Right)
		{
			if (Left == null || Right == null || Left.Count != Right.Count) return false;
			for (int i = 0; i < Left.Count; i++)
				if (!string.Equals(Left[i], Right[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool ReturnCallbackTradeBound(KingdomRealmArchive Archive)
		{
			return Archive != null &&
				(Archive.Phase == KingdomRealmArchivePhase.Restored ||
				 Archive.Phase == KingdomRealmArchivePhase.ReturnCleaning);
		}

		private bool DispatchReturnReputation(KingdomRealmArchive Archive, out string Refusal)
		{
			Refusal = "";
			Faction realm = Factions.GetIfExists(Archive.FactionName);
			if (!TryReputationEffect(realm, Archive, Desired: false, out string before) ||
				!TryReputationEffect(realm, Archive, Desired: true, out string after))
				return QuarantineReturn(Archive, "reputation graph cannot be bounded", out Refusal);
			KingdomRealmCallbackReceipt receipt = Archive.ReturnReputation;
			if (receipt.Phase == KingdomRealmCallbackPhase.Settled)
				return TryReputationEffect(realm, Archive, Desired: false, out string settled) &&
					string.Equals(settled, receipt.AfterEffect,
					StringComparison.Ordinal) && CurrentRealmMatchesArchive(Archive) &&
					SettledCallbackStillMatches(Archive, receipt, settled, out Refusal);
			if (receipt.Phase != KingdomRealmCallbackPhase.None)
			{
				before = receipt.BeforeEffect; after = receipt.AfterEffect;
			}
			if (!PrepareReturnCallback(Archive, receipt, KingdomRealmCallbackScope.Reputation,
				before, after,
				out bool invokeAuthorized, out Refusal)) return false;
			if (!TryReputationEffect(realm, Archive, Desired: false, out string current))
				return QuarantineReturn(Archive, "reputation graph cannot be inspected", out Refusal);
			if (current == after)
				return SettleReturnCallback(Archive, receipt,
					before == after ? KingdomRealmCallbackDisposition.Skipped :
					KingdomRealmCallbackDisposition.Delivered, current, out Refusal);
			if (current != before)
				return QuarantineReturn(Archive, "reputation callback reached a third value",
					out Refusal);
			if (realm == null)
				return SettleReturnCallback(Archive, receipt,
					KingdomRealmCallbackDisposition.Skipped, current, out Refusal);
			if (!invokeAuthorized)
				return QuarantineReturn(Archive,
					"reputation callback was interrupted before exact poststate publication",
					out Refusal);
			XRLGame gameReference = The.Game;
			Reputation reputationReference = gameReference.PlayerReputation;
			Dictionary<string, float> valuesReference = reputationReference.ReputationValues;
			Dictionary<string, string> ranksReference = reputationReference.FactionRanks;
			List<WorshipTracking> worshipReference = reputationReference.WorshipTracking;
			List<WorshipTracking> blasphemyReference = reputationReference.BlasphemyTracking;
			Dictionary<string, int> feelingReference = realm.FactionFeeling;
			The.Game.PlayerReputation.Set(realm, Archive.ReturnRegard);
			if (!ReferenceEquals(The.Game, gameReference) ||
				!ReferenceEquals(The.Game.PlayerReputation, reputationReference) ||
				!ReferenceEquals(reputationReference.ReputationValues, valuesReference) ||
				!ReferenceEquals(reputationReference.FactionRanks, ranksReference) ||
				!ReferenceEquals(reputationReference.WorshipTracking, worshipReference) ||
				!ReferenceEquals(reputationReference.BlasphemyTracking, blasphemyReference) ||
				!ReferenceEquals(Factions.GetIfExists(Archive.FactionName), realm) ||
				!ReferenceEquals(realm.FactionFeeling, feelingReference) ||
				!TryReputationEffect(realm, Archive, Desired: false, out current) || current != after)
				return QuarantineReturn(Archive, "reputation callback did not publish exact target",
					out Refusal);
			return SettleReturnCallback(Archive, receipt,
				KingdomRealmCallbackDisposition.Delivered, current, out Refusal);
		}

		private static bool TryReputationEffect(Faction Realm, KingdomRealmArchive Archive,
			bool Desired, out string Effect)
		{
			Effect = null;
			if (Realm == null) { Effect = "absent"; return true; }
			try
			{
				Reputation reputation = The.Game?.PlayerReputation;
				if (reputation?.ReputationValues == null || reputation.FactionRanks == null ||
					reputation.WorshipTracking == null || reputation.BlasphemyTracking == null ||
					Realm.FactionFeeling == null || reputation.ReputationValues.Count > 4096 ||
					reputation.FactionRanks.Count > 4096 ||
					reputation.WorshipTracking.Count > 4096 ||
					reputation.BlasphemyTracking.Count > 4096 || Realm.FactionFeeling.Count > 4096 ||
					!string.Equals(Realm.Name, Archive.FactionName, StringComparison.Ordinal)) return false;
				using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
				using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream,
					new System.Text.UTF8Encoding(false, true), true))
				{
					writer.Write(0x54525031); // TRP1
					WriteProofString(writer, Realm.Name); writer.Write(Realm.ID);
					List<string> valueKeys = new List<string>(reputation.ReputationValues.Keys);
					if (!valueKeys.Contains(Archive.FactionName)) valueKeys.Add(Archive.FactionName);
					valueKeys.Sort(StringComparer.Ordinal); writer.Write(valueKeys.Count);
					for (int i = 0; i < valueKeys.Count; i++)
					{
						WriteProofString(writer, valueKeys[i]);
						if (Desired && valueKeys[i] == Archive.FactionName)
							writer.Write((float)Archive.ReturnRegard);
						else if (reputation.ReputationValues.TryGetValue(valueKeys[i], out float value))
							writer.Write(value);
						else writer.Write(float.NaN);
					}
					WriteProofStringDictionary(writer, reputation.FactionRanks);
					WriteWorshipProof(writer, reputation.WorshipTracking);
					WriteWorshipProof(writer, reputation.BlasphemyTracking);
					List<string> feelingKeys = new List<string>(Realm.FactionFeeling.Keys);
					if (!feelingKeys.Contains("Player")) feelingKeys.Add("Player");
					feelingKeys.Sort(StringComparer.Ordinal); writer.Write(feelingKeys.Count);
					for (int i = 0; i < feelingKeys.Count; i++)
					{
						WriteProofString(writer, feelingKeys[i]);
						if (Desired && feelingKeys[i] == "Player")
							writer.Write(Reputation.GetFeeling((float)Archive.ReturnRegard));
						else if (Realm.FactionFeeling.TryGetValue(feelingKeys[i], out int value))
							writer.Write(value);
						else writer.Write(int.MinValue);
					}
					return FinishProofHash(stream, writer, out Effect);
				}
			}
			catch { return false; }
		}

		private bool DispatchReturnSeat(Zone Site, KingdomRealmArchive Archive,
			out string Refusal)
		{
			Refusal = "";
			string before = SeatEffect(City?.SettlementId, Away?.City?.SettlementId);
			bool shouldSwap = Site != null && Archive.Away != null &&
				Archive.Away.ClaimedZones != null && Archive.Away.ClaimedZones.Contains(Site.ZoneID) &&
				(Archive.Seat.ClaimedZones == null || !Archive.Seat.ClaimedZones.Contains(Site.ZoneID));
			string after = shouldSwap
				? SeatEffect(Archive.Away.City?.SettlementId, Archive.Seat.City?.SettlementId)
				: SeatEffect(Archive.Seat.City?.SettlementId, Archive.Away?.City?.SettlementId);
			KingdomRealmCallbackReceipt receipt = Archive.ReturnSeat;
			if (receipt.Phase != KingdomRealmCallbackPhase.None)
			{
				before = receipt.BeforeEffect; after = receipt.AfterEffect;
				shouldSwap = !string.Equals(before, after, StringComparison.Ordinal);
			}
			string current = SeatEffect(City?.SettlementId, Away?.City?.SettlementId);
			if (receipt.Phase == KingdomRealmCallbackPhase.Settled)
				return current == after && SettledCallbackStillMatches(Archive, receipt,
					current, out Refusal);
			if (!PrepareReturnCallback(Archive, receipt, KingdomRealmCallbackScope.Seat,
				before, after,
				out bool invokeAuthorized, out Refusal)) return false;
			current = SeatEffect(City?.SettlementId, Away?.City?.SettlementId);
			if (current == after)
			{
				if (!Archive.CurrentGraphMatchesAfterSeat(this, shouldSwap, out string failure))
					return QuarantineReturn(Archive, failure ?? "seat poststate differs from intent",
						out Refusal);
				return SettleReturnCallback(Archive, receipt, shouldSwap
					? KingdomRealmCallbackDisposition.Delivered
					: KingdomRealmCallbackDisposition.Skipped, current, out Refusal, shouldSwap);
			}
			string beforeFailure = null;
			if (current != before || !Archive.CurrentGraphMatchesAfterSeat(this, false,
				out beforeFailure))
				return QuarantineReturn(Archive, beforeFailure ??
					"seat callback reached a third topology", out Refusal);
			if (!shouldSwap)
				return SettleReturnCallback(Archive, receipt,
					KingdomRealmCallbackDisposition.Skipped, current, out Refusal);
			if (!invokeAuthorized)
				return QuarantineReturn(Archive,
					"seat callback was interrupted before exact topology publication", out Refusal);
			TrySeat(Site);
			current = SeatEffect(City?.SettlementId, Away?.City?.SettlementId);
			string afterFailure = null;
			if (current != after || !Archive.CurrentGraphMatchesAfterSeat(this, true,
				out afterFailure))
				return QuarantineReturn(Archive, afterFailure ??
					"seat callback did not publish exact frozen topology", out Refusal);
			return SettleReturnCallback(Archive, receipt,
				KingdomRealmCallbackDisposition.Delivered, current, out Refusal,
				SeatSwapped: true);
		}

		private static string SeatEffect(string SeatId, string AwayId)
		{
			return (SeatId ?? "-") + "|" + (AwayId ?? "-");
		}

		private bool DispatchReturnAbility(KingdomRealmArchive Archive,
			out string Refusal)
		{
			Refusal = "";
			if (!TryObserveCharterAbility(out CharterAbilityObservation observation))
				return QuarantineReturn(Archive, "charter return graph cannot be bounded",
					out Refusal);
			KingdomRealmCallbackReceipt receipt = Archive.ReturnAbility;
			string restoreTemplate = observation.TargetTemplateHash;
			if (receipt.Phase == KingdomRealmCallbackPhase.None &&
				observation.State != "player-absent" && restoreTemplate == null)
			{
				if (!TryParseAbilityEffect(Archive.ExileAbility?.BeforeEffect,
					out string ignoredExileFull, out string ignoredExileStable,
					out restoreTemplate, out string ignoredExileState) || restoreTemplate == null)
					return QuarantineReturn(Archive,
						"charter return lacks frozen exact target template", out Refusal);
			}
			string before = receipt.Phase == KingdomRealmCallbackPhase.None
				? AbilityEffect(observation) : receipt.BeforeEffect;
			string after = receipt.Phase == KingdomRealmCallbackPhase.None
				? AbilityIntent(observation.StableHash, restoreTemplate,
					observation.State == "player-absent" ? "player-absent" : "valid")
				: receipt.AfterEffect;
			if (!TryParseAbilityEffect(before, out string beforeFull, out string frozenStable,
				out string beforeTemplate, out string beforeState) ||
				!TryParseAbilityEffect(after, out string ignoredFull, out string expectedStable,
					out string expectedTemplate, out string expectedState) ||
				frozenStable != expectedStable ||
				(expectedState != "valid" && expectedState != "player-absent") ||
				(expectedState == "valid" && expectedTemplate == null))
				return QuarantineReturn(Archive, "charter return intent is malformed", out Refusal);
			if (receipt.Phase == KingdomRealmCallbackPhase.Settled)
				return observation.State == expectedState &&
					observation.StableHash == frozenStable &&
					(expectedState != "valid" ||
					 observation.TargetTemplateHash == expectedTemplate) &&
					SettledCallbackStillMatches(Archive, receipt,
						AbilityEffect(observation), out Refusal);
			if (!PrepareReturnCallback(Archive, receipt, KingdomRealmCallbackScope.Ability,
				before, after,
				out bool invokeAuthorized, out Refusal)) return false;
			if (!TryObserveCharterAbility(out observation) ||
				observation.StableHash != frozenStable)
				return QuarantineReturn(Archive,
					"charter return changed unaffected ability or part graph", out Refusal);
			string current = AbilityEffect(observation);
			if (observation.State == expectedState &&
				(expectedState != "valid" || observation.TargetTemplateHash == expectedTemplate))
				return SettleReturnCallback(Archive, receipt,
					current == before ? KingdomRealmCallbackDisposition.Skipped :
					KingdomRealmCallbackDisposition.Delivered, current, out Refusal);
			if (!observation.Recoverable || current != before ||
				observation.State != beforeState || observation.FullHash != beforeFull ||
				observation.TargetTemplateHash != beforeTemplate)
				return QuarantineReturn(Archive,
					"charter callback reached duplicate or foreign ability state", out Refusal);
			if (!invokeAuthorized)
				return QuarantineReturn(Archive,
					"charter callback was interrupted before exact poststate publication", out Refusal);
			if (!Archive.CurrentGraphMatches(this, out string failure))
				return QuarantineReturn(Archive, failure, out Refusal);
			if (!TryCaptureCharterReferences(out CharterReferenceSnapshot charterReferences))
				return QuarantineReturn(Archive, "charter reference graph is unbounded", out Refusal);
			The.Player.RequirePart<KingdomCharterPart>().EnsureAbility();
			if (!TryObserveCharterAbility(out observation) ||
				!CharterReferencesStillMatch(charterReferences, AllowPartCreation: true) ||
				observation.StableHash != frozenStable || observation.State != expectedState ||
				observation.TargetTemplateHash != expectedTemplate)
				return QuarantineReturn(Archive,
					"charter callback did not settle exact target-only graph", out Refusal);
			return SettleReturnCallback(Archive, receipt,
				KingdomRealmCallbackDisposition.Delivered,
				AbilityEffect(observation), out Refusal);
		}

		private static string InspectCharterAbility(out bool Valid, out bool Recoverable)
		{
			Valid = false;
			Recoverable = false;
			GameObject player = The.Player;
			if (player == null) return "player-absent";
			int partCount = 0;
			KingdomCharterPart exactPart = null;
			for (int i = 0; i < player.PartsList.Count; i++)
			{
				IPart part = player.PartsList[i];
				if (part != null && part.GetType().Name == "KingdomCharterPart")
				{
					partCount++;
					if (part is KingdomCharterPart typed) exactPart = typed;
				}
			}
			int commandCount = 0;
			Guid commandId = Guid.Empty;
			System.Collections.Generic.Dictionary<Guid, ActivatedAbilityEntry> abilities =
				player.ActivatedAbilities?.AbilityByGuid;
			if (abilities != null)
			{
				foreach (KeyValuePair<Guid, ActivatedAbilityEntry> row in abilities)
					if (row.Value != null && row.Value.Command == KingdomCharterPart.COMMAND)
					{
						commandCount++;
						commandId = row.Key;
					}
			}
			Guid pointer = exactPart == null ? Guid.Empty : exactPart.ActivatedAbilityID;
			Valid = partCount == 1 && exactPart != null &&
				ReferenceEquals(exactPart.ParentObject, player) && commandCount == 1 &&
				commandId != Guid.Empty && pointer == commandId;
			Recoverable = partCount <= 1 && (partCount == 0 || exactPart != null) &&
				commandCount <= 1 && (pointer == Guid.Empty ||
					(commandId != Guid.Empty && pointer == commandId));
			try
			{
				using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
				using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream,
					new System.Text.UTF8Encoding(false, true), true))
				{
					writer.Write(0x54414331); // TAC1
					WriteProofString(writer, player.IDIfAssigned);
					if (player.PartsList.Count > 4096) return null;
					writer.Write(player.PartsList.Count);
					for (int i = 0; i < player.PartsList.Count; i++)
					{
						IPart part = player.PartsList[i];
						WriteProofString(writer, part?.GetType().FullName);
						if (part != null && part.GetType().Name == "KingdomCharterPart")
							writer.Write((part as KingdomCharterPart)?.ActivatedAbilityID.ToByteArray()
								?? Guid.Empty.ToByteArray());
					}
					writer.Write(partCount); writer.Write(commandCount);
					writer.Write(pointer.ToByteArray()); writer.Write(commandId.ToByteArray());
					ActivatedAbilities activated = player.ActivatedAbilities;
					writer.Write(activated == null ? (byte)0 : (byte)1);
					if (activated != null)
					{
						writer.Write(activated.Silent);
						Dictionary<Guid, ActivatedAbilityEntry> map = activated.AbilityByGuid;
						if (map == null || map.Count > 4096) return null;
						writer.Write(map.Count);
						foreach (KeyValuePair<Guid, ActivatedAbilityEntry> row in map)
						{
							writer.Write(row.Key.ToByteArray());
							writer.Write(row.Value == null ? (byte)0 :
								ReferenceEquals(row.Value.Abilities, activated) ? (byte)1 : (byte)2);
							WriteActivatedAbilityProof(writer, row.Value);
						}
						List<CommandCooldown> cooldowns = activated.Cooldowns;
						if (cooldowns == null || cooldowns.Count > 4096) return null;
						writer.Write(cooldowns.Count);
						for (int i = 0; i < cooldowns.Count; i++)
						{
							CommandCooldown cooldown = cooldowns[i];
							writer.Write(cooldown == null ? (byte)0 : (byte)1);
							if (cooldown != null)
							{
								WriteProofString(writer, cooldown.Command);
								writer.Write(cooldown.Segments); writer.Write(cooldown.Token);
							}
						}
					}
					return FinishProofHash(stream, writer, out string hash) ? hash : null;
				}
			}
			catch { return null; }
		}

		private sealed class CharterAbilityObservation
		{
			public string FullHash;
			public string StableHash;
			public string TargetTemplateHash;
			public string State;
			public bool Recoverable;
		}

		private static bool TryObserveCharterAbility(out CharterAbilityObservation Observation)
		{
			Observation = null;
			string full = InspectCharterAbility(out bool valid, out bool recoverable);
			if (full == null) return false;
			if (The.Player == null)
			{
				Observation = new CharterAbilityObservation
				{
					FullHash = full, StableHash = "player-absent",
					TargetTemplateHash = null, State = "player-absent", Recoverable = true
				};
				return true;
			}
			if (!TryHashCharterInvariant(out string stable, out string targetTemplate,
				out bool exactTargetOwner)) return false;
			valid = valid && exactTargetOwner && targetTemplate != null;
			string state = valid ? "valid" : CharterAbilityRemoved() ? "removed" :
				recoverable ? "recoverable" : "invalid";
			Observation = new CharterAbilityObservation
			{
				FullHash = full, StableHash = stable, TargetTemplateHash = targetTemplate,
				State = state, Recoverable = recoverable
			};
			return true;
		}

		private static bool TryHashCharterInvariant(out string StableHash,
			out string TargetTemplateHash, out bool ExactTargetOwner)
		{
			StableHash = null; TargetTemplateHash = null; ExactTargetOwner = false;
			GameObject player = The.Player;
			if (player == null) { StableHash = "player-absent"; ExactTargetOwner = true; return true; }
			try
			{
				ActivatedAbilities activated = player.ActivatedAbilities;
				Dictionary<Guid, ActivatedAbilityEntry> map = activated?.AbilityByGuid;
				List<CommandCooldown> cooldowns = activated?.Cooldowns;
				if (player.PartsList == null || player.PartsList.Count > 4096 || map == null ||
					map.Count > 4096 || cooldowns == null || cooldowns.Count > 4096) return false;
				using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
				using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream,
					new System.Text.UTF8Encoding(false, true), true))
				{
					writer.Write(0x54414932); // TAI2
					List<object> referenceTopology = new List<object>();
					WriteReferenceTopologyProof(writer, player, referenceTopology);
					WriteReferenceTopologyProof(writer, player.PartsList, referenceTopology);
					WriteProofString(writer, player.IDIfAssigned);
					int otherParts = 0;
					for (int i = 0; i < player.PartsList.Count; i++)
						if (player.PartsList[i] == null ||
							player.PartsList[i].GetType().Name != "KingdomCharterPart") otherParts++;
					writer.Write(otherParts);
					for (int i = 0; i < player.PartsList.Count; i++)
					{
						IPart part = player.PartsList[i];
						if (part != null && part.GetType().Name == "KingdomCharterPart") continue;
						WriteReferenceTopologyProof(writer, part, referenceTopology);
						WriteReferenceTopologyProof(writer, part?.ParentObject, referenceTopology);
						WriteProofString(writer, part?.GetType().FullName);
					}
					WriteReferenceTopologyProof(writer, activated, referenceTopology);
					WriteReferenceTopologyProof(writer, map, referenceTopology);
					WriteReferenceTopologyProof(writer, cooldowns, referenceTopology);
					writer.Write(activated.Silent);
					int otherEntries = 0;
					foreach (KeyValuePair<Guid, ActivatedAbilityEntry> row in map)
						if (row.Value == null || row.Value.Command != KingdomCharterPart.COMMAND)
							otherEntries++;
					writer.Write(otherEntries);
					ActivatedAbilityEntry target = null;
					Guid targetId = Guid.Empty;
					int targetCount = 0;
					foreach (KeyValuePair<Guid, ActivatedAbilityEntry> row in map)
					{
						if (row.Value != null && row.Value.Command == KingdomCharterPart.COMMAND)
						{
							targetCount++;
							if (targetCount == 1) { target = row.Value; targetId = row.Key; }
							else { target = null; targetId = Guid.Empty; }
							continue;
						}
						writer.Write(row.Key.ToByteArray());
						WriteReferenceTopologyProof(writer, row.Value, referenceTopology);
						WriteReferenceTopologyProof(writer, row.Value?.Abilities,
							referenceTopology);
						WriteReferenceTopologyProof(writer, row.Value?.CommandCooldown,
							referenceTopology);
						WriteReferenceTopologyProof(writer, row.Value?.UITileDefault,
							referenceTopology);
						WriteReferenceTopologyProof(writer, row.Value?.UITileToggleOn,
							referenceTopology);
						WriteReferenceTopologyProof(writer, row.Value?.UITileDisabled,
							referenceTopology);
						WriteReferenceTopologyProof(writer, row.Value?.UITileCoolingDown,
							referenceTopology);
						writer.Write(row.Value == null ? (byte)0 :
							ReferenceEquals(row.Value.Abilities, activated) ? (byte)1 : (byte)2);
						WriteActivatedAbilityProof(writer, row.Value);
					}
					writer.Write(cooldowns.Count);
					for (int i = 0; i < cooldowns.Count; i++)
					{
						CommandCooldown cooldown = cooldowns[i];
						WriteReferenceTopologyProof(writer, cooldown, referenceTopology);
						writer.Write(cooldown == null ? (byte)0 : (byte)1);
						if (cooldown != null)
						{
							WriteProofString(writer, cooldown.Command);
							writer.Write(cooldown.Segments); writer.Write(cooldown.Token);
						}
					}
					if (!FinishProofHash(stream, writer, out StableHash)) return false;
					if (targetCount == 1 && target != null)
					{
						ExactTargetOwner = targetId != Guid.Empty && target.ID == targetId &&
							ReferenceEquals(target.Abilities, activated);
						using (System.IO.MemoryStream targetStream = new System.IO.MemoryStream())
						using (System.IO.BinaryWriter targetWriter = new System.IO.BinaryWriter(targetStream,
							new System.Text.UTF8Encoding(false, true), true))
						{
							targetWriter.Write(0x54415432); // TAT2
							WriteReferenceTopologyProof(targetWriter, target, referenceTopology);
							WriteReferenceTopologyProof(targetWriter, target.Abilities,
								referenceTopology);
							WriteReferenceTopologyProof(targetWriter, target.CommandCooldown,
								referenceTopology);
							WriteReferenceTopologyProof(targetWriter, target.UITileDefault,
								referenceTopology);
							WriteReferenceTopologyProof(targetWriter, target.UITileToggleOn,
								referenceTopology);
							WriteReferenceTopologyProof(targetWriter, target.UITileDisabled,
								referenceTopology);
							WriteReferenceTopologyProof(targetWriter, target.UITileCoolingDown,
								referenceTopology);
							WriteActivatedAbilityTemplateProof(targetWriter, target);
							if (!FinishProofHash(targetStream, targetWriter,
								out TargetTemplateHash)) return false;
						}
					}
					else ExactTargetOwner = targetCount == 0;
					return true;
				}
			}
			catch { return false; }
		}

		private const string AbilityEffectPrefix = "ability-v2";

		private static string AbilityEffect(CharterAbilityObservation Observation)
		{
			return Observation == null ? null : AbilityEffectPrefix + "|" +
				(Observation.FullHash ?? "-") + "|" + (Observation.StableHash ?? "-") + "|" +
				(Observation.TargetTemplateHash ?? "-") + "|" + (Observation.State ?? "-");
		}

		private static string AbilityIntent(string StableHash, string TargetTemplateHash,
			string TargetState)
		{
			return AbilityEffectPrefix + "|-|" + (StableHash ?? "-") + "|" +
				(TargetTemplateHash ?? "-") + "|" + (TargetState ?? "-");
		}

		private static bool TryParseAbilityEffect(string Value, out string FullHash,
			out string StableHash, out string TargetTemplateHash, out string State)
		{
			FullHash = null; StableHash = null; TargetTemplateHash = null; State = null;
			if (Value == null || Value.Length > 512) return false;
			string[] fields = Value.Split('|');
			if (fields.Length != 5 || fields[0] != AbilityEffectPrefix) return false;
			FullHash = fields[1] == "-" ? null : fields[1];
			StableHash = fields[2] == "-" ? null : fields[2];
			TargetTemplateHash = fields[3] == "-" ? null : fields[3];
			State = fields[4];
			return (FullHash == null || FullHash == "player-absent" ||
				ValidProofHash(FullHash)) &&
				(StableHash == "player-absent" || ValidProofHash(StableHash)) &&
				(TargetTemplateHash == null || ValidProofHash(TargetTemplateHash)) &&
				(State == "player-absent" || State == "valid" || State == "removed" ||
				 State == "recoverable" || State == "invalid");
		}

		private sealed class CharterReferenceSnapshot
		{
			public GameObject Player;
			public PartRack Parts;
			public ActivatedAbilities Abilities;
			public Dictionary<Guid, ActivatedAbilityEntry> Map;
			public List<CommandCooldown> Cooldowns;
			public KingdomCharterPart Part;
			public string StableHash;
			public List<IPart> OtherParts = new List<IPart>();
			public List<GameObject> OtherPartOwners = new List<GameObject>();
			public List<Guid> OtherIds = new List<Guid>();
			public List<ActivatedAbilityEntry> OtherEntries =
				new List<ActivatedAbilityEntry>();
			public List<ActivatedAbilities> OtherOwners = new List<ActivatedAbilities>();
			public List<CommandCooldown> OtherEntryCooldowns = new List<CommandCooldown>();
			public List<ConsoleLib.Console.Renderable> OtherTileDefaults =
				new List<ConsoleLib.Console.Renderable>();
			public List<ConsoleLib.Console.Renderable> OtherTileToggleOns =
				new List<ConsoleLib.Console.Renderable>();
			public List<ConsoleLib.Console.Renderable> OtherTileDisabled =
				new List<ConsoleLib.Console.Renderable>();
			public List<ConsoleLib.Console.Renderable> OtherTileCoolingDown =
				new List<ConsoleLib.Console.Renderable>();
			public List<CommandCooldown> CooldownRows = new List<CommandCooldown>();
		}

		private static bool TryCaptureCharterReferences(out CharterReferenceSnapshot Snapshot)
		{
			Snapshot = new CharterReferenceSnapshot { Player = The.Player };
			if (The.Player == null) return true;
			Snapshot.Parts = The.Player.PartsList;
			Snapshot.Abilities = The.Player.ActivatedAbilities;
			Snapshot.Map = Snapshot.Abilities?.AbilityByGuid;
			Snapshot.Cooldowns = Snapshot.Abilities?.Cooldowns;
			if (Snapshot.Parts == null || Snapshot.Parts.Count > 4096 || Snapshot.Map == null ||
				Snapshot.Map.Count > 4096 || Snapshot.Cooldowns == null ||
				Snapshot.Cooldowns.Count > 4096 ||
				!TryHashCharterInvariant(out Snapshot.StableHash,
					out string ignoredTarget, out bool ignoredOwner)) return false;
			for (int i = 0; i < Snapshot.Parts.Count; i++)
			{
				IPart part = Snapshot.Parts[i];
				if (part != null && part.GetType().Name == "KingdomCharterPart")
				{
					if (Snapshot.Part != null || !(part is KingdomCharterPart typed)) return false;
					Snapshot.Part = typed;
				}
				else
				{
					Snapshot.OtherParts.Add(part);
					Snapshot.OtherPartOwners.Add(part?.ParentObject);
				}
			}
			foreach (KeyValuePair<Guid, ActivatedAbilityEntry> row in Snapshot.Map)
				if (row.Value == null || row.Value.Command != KingdomCharterPart.COMMAND)
				{
					Snapshot.OtherIds.Add(row.Key); Snapshot.OtherEntries.Add(row.Value);
					Snapshot.OtherOwners.Add(row.Value?.Abilities);
					Snapshot.OtherEntryCooldowns.Add(row.Value?.CommandCooldown);
					Snapshot.OtherTileDefaults.Add(row.Value?.UITileDefault);
					Snapshot.OtherTileToggleOns.Add(row.Value?.UITileToggleOn);
					Snapshot.OtherTileDisabled.Add(row.Value?.UITileDisabled);
					Snapshot.OtherTileCoolingDown.Add(row.Value?.UITileCoolingDown);
				}
			for (int i = 0; i < Snapshot.Cooldowns.Count; i++)
				Snapshot.CooldownRows.Add(Snapshot.Cooldowns[i]);
			return true;
		}

		private static bool CharterReferencesStillMatch(CharterReferenceSnapshot Snapshot,
			bool AllowPartCreation)
		{
			if (Snapshot == null || !ReferenceEquals(The.Player, Snapshot.Player)) return false;
			if (Snapshot.Player == null) return true;
			if (!ReferenceEquals(The.Player.PartsList, Snapshot.Parts) ||
				!ReferenceEquals(The.Player.ActivatedAbilities, Snapshot.Abilities) ||
				!ReferenceEquals(Snapshot.Abilities?.AbilityByGuid, Snapshot.Map) ||
				!ReferenceEquals(Snapshot.Abilities?.Cooldowns, Snapshot.Cooldowns)) return false;
			if (!TryHashCharterInvariant(out string stableHash, out string ignoredTarget,
				out bool ignoredOwner) || stableHash != Snapshot.StableHash) return false;
			KingdomCharterPart currentPart = null;
			int otherPartIndex = 0;
			for (int i = 0; i < The.Player.PartsList.Count; i++)
			{
				IPart part = The.Player.PartsList[i];
				if (part != null && part.GetType().Name == "KingdomCharterPart")
				{
					if (currentPart != null || !(part is KingdomCharterPart typed)) return false;
					currentPart = typed;
				}
				else
				{
					if (otherPartIndex >= Snapshot.OtherParts.Count ||
						!ReferenceEquals(part, Snapshot.OtherParts[otherPartIndex]) ||
						!ReferenceEquals(part?.ParentObject,
							Snapshot.OtherPartOwners[otherPartIndex])) return false;
					otherPartIndex++;
				}
			}
			if (otherPartIndex != Snapshot.OtherParts.Count) return false;
			if (Snapshot.Part != null ? !ReferenceEquals(Snapshot.Part, currentPart) :
				(!AllowPartCreation && currentPart != null)) return false;
			int otherCount = 0;
			foreach (KeyValuePair<Guid, ActivatedAbilityEntry> row in Snapshot.Map)
				if (row.Value == null || row.Value.Command != KingdomCharterPart.COMMAND)
				{
					if (otherCount >= Snapshot.OtherEntries.Count ||
						row.Key != Snapshot.OtherIds[otherCount] ||
						!ReferenceEquals(row.Value, Snapshot.OtherEntries[otherCount]) ||
						!ReferenceEquals(row.Value?.Abilities, Snapshot.OtherOwners[otherCount]) ||
						!ReferenceEquals(row.Value?.CommandCooldown,
							Snapshot.OtherEntryCooldowns[otherCount]) ||
						!ReferenceEquals(row.Value?.UITileDefault,
							Snapshot.OtherTileDefaults[otherCount]) ||
						!ReferenceEquals(row.Value?.UITileToggleOn,
							Snapshot.OtherTileToggleOns[otherCount]) ||
						!ReferenceEquals(row.Value?.UITileDisabled,
							Snapshot.OtherTileDisabled[otherCount]) ||
						!ReferenceEquals(row.Value?.UITileCoolingDown,
							Snapshot.OtherTileCoolingDown[otherCount])) return false;
					otherCount++;
				}
			if (otherCount != Snapshot.OtherEntries.Count ||
				Snapshot.Cooldowns.Count != Snapshot.CooldownRows.Count) return false;
			for (int i = 0; i < Snapshot.Cooldowns.Count; i++)
				if (!ReferenceEquals(Snapshot.Cooldowns[i], Snapshot.CooldownRows[i])) return false;
			return true;
		}

		private bool DispatchReturnFeelings(KingdomRealmArchive Archive,
			out string Refusal)
		{
			Refusal = "";
			if (!TryFeelingEffect(Archive, Desired: false, out string before) ||
				!TryFeelingEffect(Archive, Desired: true, out string after))
				return QuarantineReturn(Archive, "feeling graph cannot be bounded", out Refusal);
			KingdomRealmCallbackReceipt receipt = Archive.ReturnFeelings;
			if (receipt.Phase != KingdomRealmCallbackPhase.None)
			{
				before = receipt.BeforeEffect; after = receipt.AfterEffect;
			}
			int targetSpoken = (int)KingdomExileRules.ClassifyRegard(Archive.ReturnRegard);
			int beforeSpoken = receipt.Phase == KingdomRealmCallbackPhase.None
				? Archive.RegardSpoken : receipt.BeforeStamp;
			if (!TryFeelingEffect(Archive, Desired: false, out string current))
				return QuarantineReturn(Archive, "feeling graph cannot be inspected", out Refusal);
			if (receipt.Phase == KingdomRealmCallbackPhase.Settled)
				return current == after && RegardSpoken == targetSpoken &&
					Archive.RegardSpoken == targetSpoken &&
					SettledCallbackStillMatches(Archive, receipt, current, out Refusal);
			if (!PrepareReturnCallback(Archive, receipt, KingdomRealmCallbackScope.Feelings,
				before, after,
				out bool invokeAuthorized, out Refusal, BeforeStamp: beforeSpoken,
				AfterStamp: targetSpoken)) return false;
			if (!TryFeelingEffect(Archive, Desired: false, out current))
				return QuarantineReturn(Archive, "feeling graph changed during intent", out Refusal);
			if (current != before && current != after)
				return QuarantineReturn(Archive, "feeling callback reached a third graph", out Refusal);
			bool stampBefore = RegardSpoken == beforeSpoken &&
				Archive.RegardSpoken == beforeSpoken;
			bool stampCut = RegardSpoken == targetSpoken &&
				Archive.RegardSpoken == beforeSpoken;
			bool stampAfter = RegardSpoken == targetSpoken &&
				Archive.RegardSpoken == targetSpoken;
			if (!stampBefore && !stampCut && !stampAfter)
				return QuarantineReturn(Archive,
					"feeling callback reached a third or reverse regard stamp", out Refusal);
			if (current == after)
			{
				if (!TrySettleFeelingStamp(Archive, beforeSpoken, targetSpoken))
					return QuarantineReturn(Archive,
						"feeling callback stamp could not settle exact poststate", out Refusal);
				return SettleReturnCallback(Archive, receipt,
					before == after && beforeSpoken == targetSpoken
						? KingdomRealmCallbackDisposition.Skipped :
					KingdomRealmCallbackDisposition.Delivered, current, out Refusal);
			}
			if (current != before)
				return QuarantineReturn(Archive,
					"feeling callback poststate lacks matching regard stamp", out Refusal);
			if (!stampBefore)
				return QuarantineReturn(Archive,
					"feeling callback stamp advanced without inspectable poststate", out Refusal);
			if (!invokeAuthorized)
				return QuarantineReturn(Archive,
					"feeling callback was interrupted before exact poststate publication", out Refusal);
			if (!TryCaptureFeelingReferences(out List<Faction> factionReferences,
				out List<Dictionary<string, int>> feelingReferences))
				return QuarantineReturn(Archive, "feeling reference graph cannot be bounded",
					out Refusal);
			ReassertFeelings();
			if (!FeelingReferencesStillMatch(factionReferences, feelingReferences) ||
				!TryFeelingEffect(Archive, Desired: false, out current) || current != after)
				return QuarantineReturn(Archive,
					"feeling callback did not publish the complete exact graph", out Refusal);
			if (!TrySettleFeelingStamp(Archive, beforeSpoken, targetSpoken))
				return QuarantineReturn(Archive,
					"feeling callback stamp could not publish exact poststate", out Refusal);
			return SettleReturnCallback(Archive, receipt,
				before == after && beforeSpoken == targetSpoken
					? KingdomRealmCallbackDisposition.Skipped :
				KingdomRealmCallbackDisposition.Delivered, current, out Refusal);
		}

		private bool TrySettleFeelingStamp(KingdomRealmArchive Archive, int Before, int After)
		{
			if (Archive == null || (RegardSpoken != Before && RegardSpoken != After) ||
				(Archive.RegardSpoken != Before && Archive.RegardSpoken != After) ||
				(Archive.RegardSpoken == After && RegardSpoken == Before)) return false;
			if (RegardSpoken == Before) RegardSpoken = After;
			if (Archive.RegardSpoken == Before) Archive.RegardSpoken = After;
			return RegardSpoken == After && Archive.RegardSpoken == After;
		}

		private bool TryFeelingEffect(KingdomRealmArchive Archive, bool Desired,
			out string Effect)
		{
			Effect = null;
			if (Archive?.Standings == null || Archive.Standings.Count > 512) return false;
			try
			{
				IReadOnlyList<Faction> source = Factions.GetList();
				if (source == null || source.Count > 4096) return false;
				List<Faction> factions = new List<Faction>(source.Count);
				for (int i = 0; i < source.Count; i++)
					if (source[i] != null) factions.Add(source[i]);
				factions.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
				using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
				using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream,
					new System.Text.UTF8Encoding(false, true), true))
				{
					writer.Write(0x54464631); // TFF1
					writer.Write(factions.Count);
					for (int i = 0; i < factions.Count; i++)
					{
						Faction faction = factions[i];
						if (i > 0 && faction.Name == factions[i - 1].Name) return false;
						if (faction.FactionFeeling == null || faction.FactionFeeling.Count > 4096)
							return false;
						WriteProofString(writer, faction.Name); writer.Write(faction.ID);
						List<string> keys = new List<string>(faction.FactionFeeling.Keys);
						bool mirrorsStanding = Archive.Standings.ContainsKey(faction.Name);
						if (Desired && mirrorsStanding && !keys.Contains(Archive.FactionName))
							keys.Add(Archive.FactionName);
						bool mirrorsPlayer = faction.Name == Archive.FactionName;
						if (Desired && mirrorsPlayer && !keys.Contains("Player")) keys.Add("Player");
						keys.Sort(StringComparer.Ordinal); writer.Write(keys.Count);
						for (int j = 0; j < keys.Count; j++)
						{
							WriteProofString(writer, keys[j]);
							if (Desired && mirrorsStanding && keys[j] == Archive.FactionName)
								writer.Write(Reputation.GetFeeling(
									(float)Archive.Standings[faction.Name]));
							else if (Desired && mirrorsPlayer && keys[j] == "Player")
								writer.Write(Reputation.GetFeeling((float)Archive.ReturnRegard));
							else writer.Write(faction.FactionFeeling[keys[j]]);
						}
					}
					return FinishProofHash(stream, writer, out Effect);
				}
			}
			catch { return false; }
		}

		private static bool TryCaptureFeelingReferences(out List<Faction> FactionReferences,
			out List<Dictionary<string, int>> FeelingReferences)
		{
			FactionReferences = new List<Faction>();
			FeelingReferences = new List<Dictionary<string, int>>();
			IReadOnlyList<Faction> source = Factions.GetList();
			if (source == null || source.Count > 4096) return false;
			for (int i = 0; i < source.Count; i++)
				if (source[i] != null) FactionReferences.Add(source[i]);
			FactionReferences.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
			for (int i = 0; i < FactionReferences.Count; i++)
			{
				if (FactionReferences[i].FactionFeeling == null ||
					(i > 0 && FactionReferences[i].Name == FactionReferences[i - 1].Name)) return false;
				FeelingReferences.Add(FactionReferences[i].FactionFeeling);
			}
			return true;
		}

		private static bool FeelingReferencesStillMatch(List<Faction> FactionReferences,
			List<Dictionary<string, int>> FeelingReferences)
		{
			if (!TryCaptureFeelingReferences(out List<Faction> currentFactions,
				out List<Dictionary<string, int>> currentFeelings) ||
				FactionReferences == null || FeelingReferences == null ||
				FactionReferences.Count != currentFactions.Count ||
				FeelingReferences.Count != currentFeelings.Count) return false;
			for (int i = 0; i < FactionReferences.Count; i++)
				if (!ReferenceEquals(FactionReferences[i], currentFactions[i]) ||
					!ReferenceEquals(FeelingReferences[i], currentFeelings[i])) return false;
			return true;
		}

		private bool DispatchReturnChronicle(KingdomRealmArchive Archive,
			out string Refusal)
		{
			string eventId = "taf:realm:return:v1:" + Archive.RealmId;
			string telling = KingdomExileRules.ReturnTelling(Archive.DisplayName);
			return DispatchRealmChronicle(Archive, Archive.ReturnChronicle, eventId, telling,
				"return", out Refusal);
		}

		private static bool ChronicleDeclarationMatchesArchive(KingdomRealmArchive Archive,
			KingdomChronicleDeclaration Declaration, out string Failure)
		{
			Failure = null;
			if (Archive == null || Declaration == null ||
				!DeclarationListMatches(Archive.ChronicleEntries, "official",
					Declaration.Official, Declaration.OfficialBefore,
					Declaration.OfficialAfter) ||
				!DeclarationListMatches(Archive.OutsiderEntries, "outsider",
					Declaration.Outsider, Declaration.OutsiderBefore,
					Declaration.OutsiderAfter))
			{
				Failure = "archived Chronicle declaration lists differ from frozen CAS";
				return false;
			}
			return true;
		}

		private static bool DeclarationListMatches(List<string> Values, string Domain,
			string DeclaredValue, string BeforeHash, string AfterHash)
		{
			if (Values == null || string.IsNullOrEmpty(DeclaredValue) ||
				!KingdomChronicleReceiptRules.TryHashList(Domain, Values,
					out string current)) return false;
			if (current == BeforeHash)
				return KingdomChronicleReceiptRules.TryHashAfter(Domain, Values, DeclaredValue,
					out string declaredAfter) && declaredAfter == AfterHash;
			return current == AfterHash && Values.Count > 0 &&
				string.Equals(Values[Values.Count - 1], DeclaredValue,
					StringComparison.Ordinal);
		}

		private bool TryValidateChronicleLists(KingdomChronicleDeclaration Declaration,
			KingdomChronicleReceipt EventReceipt, bool Present, bool Terminal,
			out string OfficialHash, out string OutsiderHash, out bool ListLost)
		{
			OfficialHash = null; OutsiderHash = null; ListLost = false;
			if (Declaration == null ||
				!DeclarationListMatches(ChronicleEntries, "official", Declaration.Official,
					Declaration.OfficialBefore, Declaration.OfficialAfter) ||
				!DeclarationListMatches(OutsiderEntries, "outsider", Declaration.Outsider,
					Declaration.OutsiderBefore, Declaration.OutsiderAfter) ||
				!KingdomChronicleReceiptRules.TryHashList("official", ChronicleEntries,
					out OfficialHash) ||
				!KingdomChronicleReceiptRules.TryHashList("outsider", OutsiderEntries,
					out OutsiderHash)) return false;
			if (!Present)
				return !Terminal && EventReceipt == null &&
					OfficialHash == Declaration.OfficialBefore &&
					OutsiderHash == Declaration.OutsiderBefore;
			if (EventReceipt == null ||
				(!EventReceipt.Compact &&
				 (!string.Equals(EventReceipt.Official, Declaration.Official,
					 StringComparison.Ordinal) ||
				  !string.Equals(EventReceipt.Outsider, Declaration.Outsider,
					 StringComparison.Ordinal) ||
				  EventReceipt.OfficialBefore != Declaration.OfficialBefore ||
				  EventReceipt.OfficialAfter != Declaration.OfficialAfter ||
				  EventReceipt.OutsiderBefore != Declaration.OutsiderBefore ||
				  EventReceipt.OutsiderAfter != Declaration.OutsiderAfter))) return false;
			return KingdomRealmCallbackProofRules.ChronicleListsMatch(
				EventReceipt.OfficialState, OfficialHash, Declaration.OfficialBefore,
				Declaration.OfficialAfter, EventReceipt.OutsiderState, OutsiderHash,
				Declaration.OutsiderBefore, Declaration.OutsiderAfter, Terminal,
				out ListLost);
		}

		private bool EnsureArchiveChronicleState(KingdomRealmArchive Archive,
			KingdomChronicleDeclaration Declaration, KingdomChronicleReceipt EventReceipt,
			string Registry, string RegistryFault, string FrozenRegistryHash,
			out string Refusal)
		{
			Refusal = "";
			if (Archive == null || Declaration == null || EventReceipt == null ||
				!KingdomChronicleReceiptRules.IsTerminal(EventReceipt) ||
				!KingdomChronicleReceiptRules.TryHashList("official", ChronicleEntries,
					out string officialLive) ||
				!KingdomChronicleReceiptRules.TryHashList("outsider", OutsiderEntries,
					out string outsiderLive))
				return QuarantineReturn(Archive,
					"Chronicle terminal archive state cannot be bounded", out Refusal);
			string officialExpected = EventReceipt.OfficialState ==
				KingdomChronicleSinkDisposition.Delivered ? Declaration.OfficialAfter :
				EventReceipt.OfficialState == KingdomChronicleSinkDisposition.Lost ?
				Declaration.OfficialBefore : null;
			string outsiderExpected = EventReceipt.OutsiderState ==
				KingdomChronicleSinkDisposition.Delivered ? Declaration.OutsiderAfter :
				EventReceipt.OutsiderState == KingdomChronicleSinkDisposition.Lost ?
				Declaration.OutsiderBefore : null;
			if (officialExpected == null || outsiderExpected == null ||
				officialLive != officialExpected || outsiderLive != outsiderExpected ||
				!TryHashTextPair(Registry, RegistryFault, out string desiredRegistryHash) ||
				!TryHashTextPair(Archive.ChronicleRegistry, Archive.ChronicleRegistryFault,
					out string archivedRegistryHash))
				return QuarantineReturn(Archive,
					"Chronicle terminal sinks do not match declared state", out Refusal);
			if (!KingdomChronicleReceiptRules.TryHashList("official", Archive.ChronicleEntries,
					out string archivedOfficial) ||
				(archivedOfficial != Declaration.OfficialBefore &&
				 archivedOfficial != officialExpected) ||
				!KingdomChronicleReceiptRules.TryHashList("outsider", Archive.OutsiderEntries,
					out string archivedOutsider) ||
				(archivedOutsider != Declaration.OutsiderBefore &&
				 archivedOutsider != outsiderExpected) ||
				(archivedRegistryHash != FrozenRegistryHash &&
				 archivedRegistryHash != desiredRegistryHash))
				return QuarantineReturn(Archive,
					"archived Chronicle CAS reached a third state", out Refusal);
			if (archivedOfficial == Declaration.OfficialBefore)
				Archive.ChronicleEntries = KingdomRealmArchive.CloneStrings(ChronicleEntries);
			if (archivedOutsider == Declaration.OutsiderBefore)
				Archive.OutsiderEntries = KingdomRealmArchive.CloneStrings(OutsiderEntries);
			if (archivedRegistryHash == FrozenRegistryHash)
			{
				Archive.ChronicleRegistry = Registry;
				Archive.ChronicleRegistryFault = RegistryFault;
			}
			return true;
		}

		private static bool TryInspectChronicle(string EventId, string Fingerprint,
			out string RegistryHash, out bool Present, out bool Terminal, out bool Lost,
			out bool Conflict, out string Registry, out string RegistryFault,
			out string OtherRegistryHash, out KingdomChronicleReceipt EventReceipt)
		{
			RegistryHash = null; Present = false; Terminal = false; Lost = false;
			Conflict = false; Registry = null; RegistryFault = null; OtherRegistryHash = null;
			EventReceipt = null;
			if (!KingdomChronicle.TryCaptureRealmRegistry(out Registry, out RegistryFault,
				out string failure) || !TryHashTextPair(Registry, RegistryFault, out RegistryHash) ||
				!KingdomChronicleReceiptRules.TryParseRegistry(Registry,
					out List<KingdomChronicleReceipt> rows, out bool migrated,
					out KingdomChronicleRegistryFault fault) || migrated) return false;
			List<KingdomChronicleReceipt> otherRows =
				new List<KingdomChronicleReceipt>(rows.Count);
			for (int i = 0; i < rows.Count; i++)
			{
				if (!string.Equals(rows[i].EventId, EventId, StringComparison.Ordinal))
				{
					otherRows.Add(rows[i].Copy());
					continue;
				}
				if (Present) { Conflict = true; return true; }
				Present = true;
				if (!string.Equals(rows[i].Fingerprint, Fingerprint, StringComparison.Ordinal))
				{
					Conflict = true; return true;
				}
				EventReceipt = rows[i].Copy();
				Terminal = KingdomChronicleReceiptRules.IsTerminal(rows[i]);
				Lost = rows[i].OfficialState == KingdomChronicleSinkDisposition.Lost ||
					rows[i].OutsiderState == KingdomChronicleSinkDisposition.Lost ||
					rows[i].JournalState == KingdomChronicleSinkDisposition.Lost;
			}
			return KingdomChronicleReceiptRules.TryWriteRegistry(otherRows,
				out string otherRegistry, out KingdomChronicleRegistryFault otherFault) &&
				otherFault == KingdomChronicleRegistryFault.None &&
				// Fault state is diagnostic output of this exact callback (not unrelated row
				// authority): an honest Lost sink may update it. Freeze only other receipt rows.
				TryHashTextPair(otherRegistry, null, out OtherRegistryHash);
		}

		private const string ChronicleIntentPrefix = "chronicle-v2";

		private static bool TryCreateChronicleIntent(string EventId,
			KingdomChronicleDeclaration Declaration, string RegistryHash,
			string OtherRegistryHash, string RegistryFault, out string Intent)
		{
			Intent = null;
			if (Declaration == null || !ValidProofHash(RegistryHash) ||
				!ValidProofHash(OtherRegistryHash) ||
				!ValidProofHash(Declaration.Fingerprint) ||
				!ValidProofHash(Declaration.OfficialBefore) ||
				!ValidProofHash(Declaration.OfficialAfter) ||
				!ValidProofHash(Declaration.OutsiderBefore) ||
				!ValidProofHash(Declaration.OutsiderAfter) || RegistryFault == null ||
				RegistryFault.Length > 160 ||
				!string.Equals(EventId, Declaration.EventId, StringComparison.Ordinal)) return false;
			try
			{
				System.Text.UTF8Encoding utf8 = new System.Text.UTF8Encoding(false, true);
				Intent = ChronicleIntentPrefix + "|" +
					Convert.ToBase64String(utf8.GetBytes(EventId)) + "|" +
					Declaration.Fingerprint + "|" + RegistryHash + "|" + OtherRegistryHash + "|" +
					Declaration.OfficialBefore + "|" + Declaration.OfficialAfter + "|" +
					Declaration.OutsiderBefore + "|" + Declaration.OutsiderAfter + "|" +
					Convert.ToBase64String(utf8.GetBytes(Declaration.Official)) + "|" +
					Convert.ToBase64String(utf8.GetBytes(Declaration.Outsider)) + "|" +
					Convert.ToBase64String(utf8.GetBytes(RegistryFault));
				return Intent.Length <= KingdomRealmCallbackReceipt.MaxEffectChars;
			}
			catch { Intent = null; return false; }
		}

		private static bool TryParseChronicleIntent(string Intent, string ExpectedEventId,
			string Text, bool Accomplishment, string MuralText,
			out KingdomChronicleDeclaration Declaration, out string RegistryHash,
			out string OtherRegistryHash, out string RegistryFault)
		{
			Declaration = null; RegistryHash = null; OtherRegistryHash = null;
			RegistryFault = null;
			if (Intent == null || Intent.Length > KingdomRealmCallbackReceipt.MaxEffectChars)
				return false;
			string[] fields = Intent.Split('|');
			if (fields.Length != 12 || fields[0] != ChronicleIntentPrefix ||
				fields[1].Length > KingdomChronicleReceiptRules.MaxEventIdChars * 6 ||
				fields[9].Length > KingdomChronicleReceiptRules.MaxEntryChars * 6 ||
				fields[10].Length > KingdomChronicleReceiptRules.MaxEntryChars * 6 ||
				fields[11].Length > 960 ||
				!ValidProofHash(fields[2]) || !ValidProofHash(fields[3]) ||
				!ValidProofHash(fields[4]) || !ValidProofHash(fields[5]) ||
				!ValidProofHash(fields[6]) || !ValidProofHash(fields[7]) ||
				!ValidProofHash(fields[8])) return false;
			try
			{
				System.Text.UTF8Encoding utf8 = new System.Text.UTF8Encoding(false, true);
				string eventId = utf8.GetString(Convert.FromBase64String(fields[1]));
				string official = utf8.GetString(Convert.FromBase64String(fields[9]));
				string outsider = utf8.GetString(Convert.FromBase64String(fields[10]));
				string registryFault = utf8.GetString(Convert.FromBase64String(fields[11]));
				if (!string.Equals(eventId, ExpectedEventId, StringComparison.Ordinal) ||
					string.IsNullOrEmpty(official) || string.IsNullOrEmpty(outsider) ||
					official.Length > KingdomChronicleReceiptRules.MaxEntryChars ||
					outsider.Length > KingdomChronicleReceiptRules.MaxEntryChars ||
					registryFault.Length > 160 ||
					!KingdomChronicleReceiptRules.TryFingerprint(eventId, Text, Accomplishment,
						MuralText, out string fingerprint) || fingerprint != fields[2]) return false;
				Declaration = new KingdomChronicleDeclaration(eventId, Text, Accomplishment,
					MuralText, fields[2], official, outsider, fields[5], fields[6],
					fields[7], fields[8]);
				RegistryHash = fields[3]; OtherRegistryHash = fields[4];
				RegistryFault = registryFault;
				return true;
			}
			catch { Declaration = null; RegistryFault = null; return false; }
		}

		private static string ChronicleObserved(string RegistryHash, string OtherRegistryHash,
			string OfficialHash, string OutsiderHash, KingdomChronicleReceipt Receipt)
		{
			return Receipt == null ? null : ChronicleIntentPrefix + "|observed|" + RegistryHash +
				"|" + OtherRegistryHash + "|" + OfficialHash + "|" + OutsiderHash + "|" +
				((int)Receipt.OfficialState).ToString() + "|" +
				((int)Receipt.OutsiderState).ToString() + "|" +
				((int)Receipt.JournalState).ToString();
		}

		private static bool ValidProofHash(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9') ||
					(Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}

		private static bool TryHashTextPair(string Left, string Right, out string Hash)
		{
			Hash = null;
			try
			{
				System.Text.UTF8Encoding utf8 = new System.Text.UTF8Encoding(false, true);
				using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
				using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream, utf8, true))
				{
					WriteHashText(writer, Left, utf8); WriteHashText(writer, Right, utf8);
					writer.Flush();
					if (stream.Length > KingdomChronicleReceiptRules.MaxRegistryChars * 4L + 1024L)
						return false;
					using (global::System.Security.Cryptography.SHA256 sha =
						global::System.Security.Cryptography.SHA256.Create())
					{
						byte[] digest = sha.ComputeHash(stream.ToArray());
						System.Text.StringBuilder text = new System.Text.StringBuilder(64);
						for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2"));
						Hash = text.ToString(); return true;
					}
				}
			}
			catch { return false; }
		}

		private static void WriteProofString(System.IO.BinaryWriter Writer, string Value)
		{
			if (Value == null) { Writer.Write(-1); return; }
			byte[] bytes = new System.Text.UTF8Encoding(false, true).GetBytes(Value);
			if (bytes.Length > 16384) throw new System.IO.InvalidDataException(
				"Engine callback proof string exceeds cap.");
			Writer.Write(bytes.Length); Writer.Write(bytes);
		}

		private static void WriteProofStringDictionary(System.IO.BinaryWriter Writer,
			Dictionary<string, string> Values)
		{
			if (Values == null || Values.Count > 4096) throw new System.IO.InvalidDataException(
				"Engine callback proof dictionary exceeds cap.");
			List<string> keys = new List<string>(Values.Keys);
			keys.Sort(StringComparer.Ordinal); Writer.Write(keys.Count);
			for (int i = 0; i < keys.Count; i++)
			{
				WriteProofString(Writer, keys[i]); WriteProofString(Writer, Values[keys[i]]);
			}
		}

		private static void WriteWorshipProof(System.IO.BinaryWriter Writer,
			List<WorshipTracking> Values)
		{
			if (Values == null || Values.Count > 4096) throw new System.IO.InvalidDataException(
				"Engine callback worship proof exceeds cap.");
			Writer.Write(Values.Count);
			for (int i = 0; i < Values.Count; i++)
			{
				WorshipTracking row = Values[i];
				Writer.Write(row == null ? (byte)0 : (byte)1);
				if (row == null) continue;
				WriteProofString(Writer, row.Name); WriteProofString(Writer, row.Faction);
				Writer.Write(row.Devoted); Writer.Write(row.Times);
				Writer.Write(row.First); Writer.Write(row.Last);
			}
		}

		private static void WriteActivatedAbilityProof(System.IO.BinaryWriter Writer,
			ActivatedAbilityEntry Value)
		{
			Writer.Write(Value == null ? (byte)0 : (byte)1);
			if (Value == null) return;
			Writer.Write(Value.ID.ToByteArray()); WriteProofString(Writer, Value.DisplayName);
			WriteProofString(Writer, Value.Command); WriteProofString(Writer, Value.Class);
			WriteProofString(Writer, Value.Description); WriteProofString(Writer, Value.Icon);
			WriteProofString(Writer, Value.DisabledMessage); Writer.Write(Value.Flags);
			WriteProofString(Writer, Value._DescriptionCommand);
			CommandCooldown cooldown = Value.CommandCooldown;
			Writer.Write(cooldown == null ? (byte)0 : (byte)1);
			if (cooldown != null)
			{
				WriteProofString(Writer, cooldown.Command); Writer.Write(cooldown.Segments);
				Writer.Write(cooldown.Token);
			}
			WriteRenderableProof(Writer, Value.UITileDefault);
			WriteRenderableProof(Writer, Value.UITileToggleOn);
			WriteRenderableProof(Writer, Value.UITileDisabled);
			WriteRenderableProof(Writer, Value.UITileCoolingDown);
		}

		private static void WriteReferenceTopologyProof(System.IO.BinaryWriter Writer,
			object Value, List<object> References)
		{
			if (Value == null) { Writer.Write(-1); return; }
			for (int i = 0; i < References.Count; i++)
				if (ReferenceEquals(References[i], Value)) { Writer.Write(i); return; }
			Writer.Write(-2 - References.Count);
			References.Add(Value);
		}

		private static void WriteActivatedAbilityTemplateProof(System.IO.BinaryWriter Writer,
			ActivatedAbilityEntry Value)
		{
			Writer.Write(Value == null ? (byte)0 : (byte)1);
			if (Value == null) return;
			WriteProofString(Writer, Value.DisplayName); WriteProofString(Writer, Value.Command);
			WriteProofString(Writer, Value.Class); WriteProofString(Writer, Value.Description);
			WriteProofString(Writer, Value.Icon); WriteProofString(Writer, Value.DisabledMessage);
			Writer.Write(Value.Flags); WriteProofString(Writer, Value._DescriptionCommand);
			CommandCooldown cooldown = Value.CommandCooldown;
			Writer.Write(cooldown == null ? (byte)0 : (byte)1);
			if (cooldown != null)
			{
				WriteProofString(Writer, cooldown.Command); Writer.Write(cooldown.Segments);
				Writer.Write(cooldown.Token);
			}
			WriteRenderableProof(Writer, Value.UITileDefault);
			WriteRenderableProof(Writer, Value.UITileToggleOn);
			WriteRenderableProof(Writer, Value.UITileDisabled);
			WriteRenderableProof(Writer, Value.UITileCoolingDown);
		}

		private static void WriteRenderableProof(System.IO.BinaryWriter Writer,
			ConsoleLib.Console.Renderable Value)
		{
			Writer.Write(Value == null ? (byte)0 : (byte)1);
			if (Value == null) return;
			WriteProofString(Writer, Value.Tile); WriteProofString(Writer, Value.RenderString);
			WriteProofString(Writer, Value.ColorString); WriteProofString(Writer, Value.TileColor);
			Writer.Write(Value.DetailColor);
		}

		private static bool FinishProofHash(System.IO.MemoryStream Stream,
			System.IO.BinaryWriter Writer, out string Hash)
		{
			Hash = null;
			Writer.Flush();
			if (Stream.Length > KingdomArchivedSettlementCodec.MaxPayloadBytes * 4L) return false;
			using (global::System.Security.Cryptography.SHA256 sha =
				global::System.Security.Cryptography.SHA256.Create())
			{
				byte[] digest = sha.ComputeHash(Stream.ToArray());
				System.Text.StringBuilder text = new System.Text.StringBuilder(64);
				for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2"));
				Hash = text.ToString();
				return true;
			}
		}

		private static void WriteHashText(System.IO.BinaryWriter Writer, string Value,
			System.Text.Encoding Utf8)
		{
			if (Value == null) { Writer.Write(-1); return; }
			int count = Utf8.GetByteCount(Value); Writer.Write(count);
			Writer.Write(Utf8.GetBytes(Value));
		}

		private bool QuarantineReturn(KingdomRealmArchive Archive, string Failure,
			out string Refusal)
		{
			Archive.Quarantine(Failure);
			Refusal = "The returned realm changed during an engine callback and requires inspection.";
			return false;
		}

		private bool CurrentRealmIsCanonicalBlank(KingdomRealmArchive Archive)
		{
			if (Archive == null || Founded || KingdomFactionName != null || RealmId != null ||
				KingdomDisplayName != null ||
				Away != null || Standings == null || Standings.Count != 0 ||
				RealmIdentityVersion != 0 || RealmIdentityOrigin != KingdomIdentityOrigin.None ||
				RealmIdentityTransactionId != null || RealmIdentityLegacyFaction != null ||
				RealmIdentityFoundedTick != 0L || RealmIdentitySeedHigh != 0UL ||
				RealmIdentitySeedLow != 0UL || RealmIdentityFirstClaimedZone != null ||
				IdentityFault != null || PendingSettlementId != null ||
				PendingSettlementTransactionId != null || PendingSettlementZoneId != null ||
				PendingSettlementAuthority != null || SimulationSeedHigh != 0UL ||
				SimulationSeedLow != 0UL || Bindings == null || ResidentCounter != 0 || Jobs == null ||
				LastSliceTick != 0L || ReifyTick != 0L || ReifyThirdsSpent != 0 ||
				ReifyHeavySpent != 0 || ReifyQuietUntilTick != 0L || DedicationCounter != 0 ||
				ChronicleEntries == null || ChronicleEntries.Count != 0 || OutsiderEntries == null ||
				OutsiderEntries.Count != 0 || RegardSpoken != (int)RealmRegard.Beloved ||
				Dissent != 0 || DissentSpoken != 0 || LastDissentTick != 0L ||
				DeclaredCreed != null || DishName != null || DishText != null ||
				DishStaple != null || DishSource != null || LastRiteTick != 0L ||
				LastSoulRiteTick != 0L || Seceded != null || SecededTick != 0L || Haul != null ||
				CarryBook == null || ReturnAskedRegard != int.MinValue || DoorClosedTold)
				return false;
			try
			{
				return KingdomArchivedSettlementCodec.ExactGraph(Capture(),
					new KingdomSettlement(), out string _) &&
					KingdomArchivedSettlementCodec.EmptyRegistries(Bindings, Jobs) &&
					KingdomArchivedSettlementCodec.EmptyCarry(CarryBook);
			}
			catch
			{
				return false;
			}
		}

		private bool RestoreArchivedRealmCore(KingdomRealmArchive Archive,
			out string Failure)
		{
			Failure = null;
			if (Archive == null ||
				!KingdomArchivedSettlementCodec.TryClone(Archive.Seat,
					out KingdomSettlement seat, out Failure) ||
				!KingdomArchivedSettlementCodec.TryClone(Archive.Away,
					out KingdomSettlement away, out Failure) ||
				!KingdomArchivedSettlementCodec.TryClone(Archive.Seceded,
					out KingdomSettlement seceded, out Failure) ||
				!KingdomRealmArchive.TryCloneCarry(Archive.CarryBook,
					out KingdomCarryBook carry, out Failure)) return false;
			Simulation.City.KingdomBindingRegistry bindings =
				KingdomRealmArchive.CloneBindings(Archive.Bindings);
			Simulation.City.KingdomJobRegistry jobs = KingdomRealmArchive.CloneJobs(Archive.Jobs);
			List<string> chronicle = KingdomRealmArchive.CloneStrings(Archive.ChronicleEntries);
			List<string> outsider = KingdomRealmArchive.CloneStrings(Archive.OutsiderEntries);
			Dictionary<string, int> standings = KingdomRealmArchive.CloneStandings(Archive.Standings);
			if (seat == null || bindings == null || jobs == null || chronicle == null ||
				outsider == null || standings == null)
			{
				Failure = "archived realm graph has a null required root";
				return false;
			}
			KingdomFactionName = Archive.FactionName;
			KingdomDisplayName = Archive.DisplayName;
			Restore(seat);
			Away = away;
			Standings = standings;
			RealmId = Archive.RealmId;
			RealmIdentityVersion = Archive.RealmIdentityVersion;
			RealmIdentityOrigin = Archive.RealmIdentityOrigin;
			RealmIdentityTransactionId = Archive.RealmIdentityTransactionId;
			RealmIdentityLegacyFaction = Archive.RealmIdentityLegacyFaction;
			RealmIdentityFoundedTick = Archive.RealmIdentityFoundedTick;
			RealmIdentitySeedHigh = Archive.RealmIdentitySeedHigh;
			RealmIdentitySeedLow = Archive.RealmIdentitySeedLow;
			RealmIdentityFirstClaimedZone = Archive.RealmIdentityFirstClaimedZone;
			IdentityFault = null;
			SimulationSeedHigh = Archive.SimulationSeedHigh;
			SimulationSeedLow = Archive.SimulationSeedLow;
			Bindings = bindings;
			ResidentCounter = Archive.ResidentCounter;
			Jobs = jobs;
			LastSliceTick = Archive.LastSliceTick;
			ReifyTick = Archive.ReifyTick;
			ReifyThirdsSpent = Archive.ReifyThirdsSpent;
			ReifyHeavySpent = Archive.ReifyHeavySpent;
			ReifyQuietUntilTick = Archive.ReifyQuietUntilTick;
			DedicationCounter = Archive.DedicationCounter;
			ChronicleEntries = chronicle;
			OutsiderEntries = outsider;
			RegardSpoken = Archive.RegardSpoken;
			Dissent = Archive.Dissent;
			DissentSpoken = Archive.DissentSpoken;
			LastDissentTick = Archive.LastDissentTick;
			DeclaredCreed = Archive.DeclaredCreed;
			DishName = Archive.DishName;
			DishText = Archive.DishText;
			DishStaple = Archive.DishStaple;
			DishSource = Archive.DishSource;
			LastRiteTick = Archive.LastRiteTick;
			LastSoulRiteTick = Archive.LastSoulRiteTick;
			Seceded = seceded;
			SecededTick = Archive.SecededTick;
			Haul = KingdomRealmArchive.CloneHaul(Archive.Haul);
			CarryBook = carry;
			PendingSettlementId = null;
			PendingSettlementTransactionId = null;
			PendingSettlementZoneId = null;
			PendingSettlementAuthority = null;
			ReturnAskedRegard = int.MinValue;
			DoorClosedTold = false;
			return true;
		}

		private bool CurrentRealmMatchesArchive(KingdomRealmArchive Archive)
		{
			List<string> ids;
			string failure;
			if (Archive == null || Archive.Quarantined ||
				!string.Equals(RealmId, Archive.RealmId, StringComparison.Ordinal) ||
				!TryExactSettlementIds(RequirePublishedClaims: true, out ids, out failure) ||
				Archive.SettlementIds == null || ids.Count != Archive.SettlementIds.Count)
				return false;
			for (int i = 0; i < ids.Count; i++)
				if (!string.Equals(ids[i], Archive.SettlementIds[i],
					StringComparison.Ordinal)) return false;
			return string.Equals(RealmId, Archive.RealmId, StringComparison.Ordinal) &&
				ExactArchivedSettlements(Archive.RealmId, ExiledSeat, ExiledAway,
					Archive.SettlementIds) && Archive.CurrentGraphMatches(this, out failure);
		}

		/// <summary>Founder-facing reason an expulsion did not proceed.</summary>
		private string ExileRefusal(ExileVerdict Verdict)
		{
			switch (Verdict)
			{
			case ExileVerdict.NothingFounded:
				return "You hold no realm. Nobody can put you out of ground that was never yours.";
			case ExileVerdict.AlreadyCastOut:
				return "{{C|" + (ExiledDisplayName ?? "The realm") + "}} has already put you out. It cannot do it twice.";
			case ExileVerdict.RegardHolds:
				return "{{C|" + (KingdomDisplayName ?? "The realm") + "}} holds you " + KingdomExileRules.RegardName(KingdomExileRules.ClassifyRegard(FounderRegard())) + ". Nobody there is calling for the gate to be shut behind you.";
			default:
				return "";
			}
		}

		/// <summary>
		/// Reads the realm's regard for the founder after it changed, and lets the realm answer:
		/// a murmur, a warning read aloud, or the gate. Keyed entirely on the deed that moved the
		/// reputation, never on how long the founder has been gone.
		/// </summary>
		/// <param name="ReputationType">The engine's own reason for the change, or null.</param>
		private void OnRealmRegardChanged(string ReputationType)
		{
			RealmRegard current = KingdomExileRules.ClassifyRegard(FounderRegard());
			RealmRegard spoken = (RealmRegard)RegardSpoken;
			RegardStep step = KingdomExileRules.JudgeRegardStep(current, spoken, Exiled);
			if (step == RegardStep.Expulsion)
			{
				Exile(KingdomExileRules.DeedClause(ReputationType), Forced: false, out var _);
				return;
			}
			RegardSpoken = (int)KingdomExileRules.RememberedRegard(current, spoken);
			if (step == RegardStep.Nothing)
			{
				return;
			}
			// Nonmodal on purpose: this is the city talking about you, not the city stopping you.
			XRL.Messages.MessageQueue.AddPlayerMessage(KingdomExileRules.RegardSpeech(step, SeatName));
			KingdomChronicle.Record(this, KingdomExileRules.RegardChronicle(step, SeatName));
		}

		/// <summary>
		/// What the old realm's ground has to say to a founder standing on it after being put out:
		/// the question, if it will hear it; why it will not, if it will not; and the closed door,
		/// once, to a founder who has since poured somewhere else.
		/// </summary>
		/// <param name="Z">The activated zone. Null is tolerated.</param>
		private void OnZoneActivatedWhileExiled(Zone Z)
		{
			if (!Exiled || Z == null || !ExiledRealmHolds(Z.ZoneID))
			{
				return;
			}
			if (Founded)
			{
				if (!DoorClosedTold)
				{
					DoorClosedTold = true;
					XRL.Messages.MessageQueue.AddPlayerMessage(KingdomExileRules.DoorClosedLine(ExiledDisplayName, KingdomDisplayName));
				}
				return;
			}
			int regard = ExiledRealmRegard();
			// Nothing is said again until the founder has actually changed the realm's mind about
			// them. A founder who walks away from the question is never asked it twice for free,
			// and a founder who ignores the whole feature is never spoken to at all.
			if (regard <= ReturnAskedRegard)
			{
				return;
			}
			ReturnAskedRegard = regard;
			ReturnVerdict verdict = KingdomExileRules.JudgeReturn(Exiled, Founded, ExiledRealmKeptGround, true, regard);
			if (verdict != ReturnVerdict.Allowed)
			{
				XRL.Messages.MessageQueue.AddPlayerMessage(KingdomExileRules.ReturnRefusal(verdict, ExiledDisplayName, KingdomDisplayName));
				return;
			}
			if (Popup.ShowYesNo("You are standing in {{C|" + ExiledDisplayName + "}}, which put you out.\n\nAsk to be taken back?") != DialogResult.Yes)
			{
				XRL.Messages.MessageQueue.AddPlayerMessage("You say nothing, and nobody asks you to.");
				return;
			}
			if (!TryReturn(Z, out var refusal))
			{
				Popup.Show(refusal);
			}
		}

		/// <summary>
		/// The founder's reputation with a named faction, tolerating a name no faction answers to.
		/// <c>Factions.Get</c> throws on an unknown name, which inside event dispatch would cost
		/// the whole step; <c>GetIfExists</c> and the null-tolerant reputation overload degrade to
		/// 0 instead.
		/// </summary>
		private static int RegardWith(string FactionName)
		{
			if (string.IsNullOrEmpty(FactionName))
			{
				return 0;
			}
			return The.Game.PlayerReputation.Get(Factions.GetIfExists(FactionName));
		}

		public override bool WantFieldReflection => false;

		public override void Write(SerializationWriter Writer)
		{
			SerializationVersion = CurrentSerializationVersion;
			Writer.Write(SerializationMagic);
			Writer.Write(CurrentSerializationVersion);
			Writer.WriteNamedFields(this, typeof(KingdomSystem));
		}

		/// <summary>
		/// Reads kingdom state, tolerating every layout this mod has ever written.
		/// <para>
		/// Two regimes meet here. Saves written before named fields arrived were emitted by the
		/// engine's positional reflection, so the engine has already filled every field by the
		/// time we are called &mdash; including <see cref="SerializationVersion"/>, which is how we
		/// recognise them. Nothing remains in the block to read, so we return.
		/// </para>
		/// <para>
		/// Named-field saves are self-describing: a reader may meet a field it does not know, and
		/// may miss one it expects, without either being an error. Any named-field version from
		/// the first through ours is therefore readable. Older positional versions and saves from
		/// a <i>newer</i> build are genuinely beyond this path.
		/// </para>
		/// <para>
		/// Throwing is the only way to reach the engine's block-skip recovery, so an unreadable
		/// save must throw &mdash; but it flags <see cref="LoadFailed"/> first, because the engine
		/// swallows the exception and hands back a blank system. Without the flag the founder's
		/// settlement would simply be gone, unremarked. See <see cref="ReportLoadFailure"/>.
		/// </para>
		/// </summary>
		public override void Read(SerializationReader Reader)
		{
			try
			{
				if (SerializationVersion == LegacyReflectedSerializationVersion)
				{
					SerializationVersion = CurrentSerializationVersion;
					NormalizeState(AllowLegacyIdentityMigration: true);
					return;
				}
				int magic = Reader.ReadInt32();
				if (magic != SerializationMagic)
				{
					throw new InvalidOperationException("Invalid ThousandAndFirst kingdom save marker.");
				}
				int version = Reader.ReadInt32();
				if (version < FirstNamedSerializationVersion || version > CurrentSerializationVersion)
				{
					throw new InvalidOperationException("Unsupported ThousandAndFirst kingdom save version " + version + "; this build reads named versions " + FirstNamedSerializationVersion + " through " + CurrentSerializationVersion + ".");
				}
				Reader.ReadNamedFields(this, typeof(KingdomSystem));
				SerializationVersion = CurrentSerializationVersion;
				NormalizeState(AllowLegacyIdentityMigration: false);
			}
			catch
			{
				LoadFailed = true;
				throw;
			}
		}

		/// <summary>
		/// Tells the founder, once, that the records could not be read. The engine catches
		/// deserialization failures and carries on with a blank system, so without this the loss
		/// would be visible only in the metrics log &mdash; the player would find the settlement
		/// unfounded and no reason given.
		/// </summary>
		private void ReportLoadFailure()
		{
			LoadFailed = false;
			MetricsManager.LogError("ThousandAndFirst: kingdom state could not be read; the settlement has been reset.");
			Popup.Show("The founding records cannot be read. Whatever kingdom you held is not recorded in this save, and the founding must begin again.\n\nYour game is otherwise unharmed.");
		}

		public override void AfterLoad(XRLGame Game)
		{
			base.AfterLoad(Game);
			// The research registry and everything it caches about the world are process statics,
			// so a second game in the same session would otherwise read the first one's quest
			// verdicts and believe its journal notes were already filed.
			KingdomResearch.Reload();
			NormalizeState(AllowLegacyIdentityMigration: false);
			if (ExiledRealmArchive != null &&
				(ExiledRealmArchive.Phase == KingdomRealmArchivePhase.Prepared ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.TradeClosed ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.MirrorsPublished ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.ChronicleFrozen ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.ChronicleCleared ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.Resetting))
			{
				string refusal;
				ContinueExileTransition(out refusal);
			}
		}

		public override void Register(XRLGame Game, IEventRegistrar Registrar)
		{
			Registrar.Register(AfterReputationChangeEvent.ID);
			Registrar.Register(AfterGameLoadedEvent.ID);
			Registrar.Register(ZoneActivatedEvent.ID);
			// The true last read (LIVING-CITY-ARCHITECTURE §3.4). ZoneDeactivatedEvent is only a
			// hint: a deactivated zone goes on simulating for up to forty more turns, so a reading
			// taken there would be wrong by whatever happened in the grace window. This fires from
			// SuspendZone BEFORE Suspended is set, for any zone, while its objects are still in RAM.
			Registrar.Register(SuspendingEvent.ID);
			// The pump, and the ONE per-turn cost this design adds anywhere (§0.0(e)). Game-level
			// EndTurnEvent.Send(game) is a single dispatch immediately before ProcessSingleTurn
			// (D/XRL/Core/ActionManager.cs:1644-1650), not the 2,000-cell broadcast a live zone
			// pays. It does not fire during world-map travel, which is exactly why §2.1 bans it as
			// the city's CLOCK -- but a founder on the world map is standing in no city zone and is
			// owed no reification, so the same blind spot is harmless in a pump.
			Registrar.Register(EndTurnEvent.ID);
			// The second reify hook (§3.5), and the one instant the stale-transient sweep may run
			// (§3.8 t3): any zone coming off disk, before intake and before anything looks at it.
			Registrar.Register(ZoneThawedEvent.ID);
			// Research quest locks are event-driven and cached, never polled. This fires AFTER all
			// quest state is consistent, which is why it and not QuestStepFinishedEvent is the hook.
			Registrar.Register(QuestFinishedEvent.ID);
		}

		/// <summary>Player-scoped events follow the active body. <see cref="IPlayerSystem"/>
		/// unregisters this system from the old body and registers it on the new one after
		/// domination, metempsychosis, or Kingdom succession.</summary>
		public override void RegisterPlayer(GameObject Player, IEventRegistrar Registrar)
		{
			// Vanilla exposes no ritual-completion event. Its player-dispatched start event carries
			// Initial, the exact first-sharing fact a rite source needs.
			Registrar.Register(WaterRitualStartEvent.ID);
		}

		/// <summary>
		/// The first time the founder shares water with one ritualist, remember that faction's way
		/// in the founder-held ledger. Vanilla owns the ritual and all of its awards; this is only
		/// the research source its start event makes observable and later projects at a seated city.
		/// </summary>
		public override bool HandleEvent(WaterRitualStartEvent E)
		{
			Guard("rite seed", delegate
			{
				// The record freezes the faction whose ritual actually paid reputation. Re-reading
				// conversation-global speaker state or its current allegiance can name another faction.
				KingdomResearch.RememberRite(this, E.Initial,
					(E.Record == null) ? null : E.Record.faction);
			});
			return base.HandleEvent(E);
		}

		/// <summary>
		/// A quest finished. The only thing in the world that can change whether a research node
		/// exists at all, so the cached verdicts are dropped here and nowhere else &mdash; there is
		/// no per-turn quest polling anywhere in this mod.
		/// </summary>
		public override bool HandleEvent(QuestFinishedEvent E)
		{
			Guard("quest", delegate
			{
				KingdomResearch.ForgetQuests();
			});
			return base.HandleEvent(E);
		}

		/// <summary>
		/// One turn of the city. Everything inside returns immediately when there is no seated
		/// claimed zone and no debt, which is what makes this affordable at all (&sect;0.0(e)).
		/// </summary>
		public override bool HandleEvent(EndTurnEvent E)
		{
			Guard("pump", delegate
			{
				Simulation.City.KingdomHeartbeat.OnEndTurn(this, AttendSeatedSemantics);
			});
			return base.HandleEvent(E);
		}

		/// <summary>
		/// A zone off disk. LIVING-CITY-ARCHITECTURE &sect;3.5 binds debt intake here and &sect;3.8
		/// binds the stale-transient sweep here; <c>TicksFrozen</c> is a cross-check on the counter
		/// and never its source, because it measures frozen time only (&sect;3.4).
		/// </summary>
		public override bool HandleEvent(ZoneThawedEvent E)
		{
			Guard("thaw", delegate
			{
				Simulation.City.KingdomHeartbeat.OnThawed(this, E.Zone, E.TicksFrozen);
			});
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(SuspendingEvent E)
		{
			Guard("check-out", delegate
			{
				Simulation.City.KingdomCity.OnSuspending(this, E.Zone);
			});
			if (Founded && E.Zone != null && (ClaimedZones.Contains(E.Zone.ZoneID)
				|| (Away != null && Away.ClaimedZones.Contains(E.Zone.ZoneID))))
			{
				Guard("seal final read", delegate
				{
					string failure;
					if (!KingdomSeal.TryStageSemanticSnapshot("zone final read", out failure))
					{
						KingdomLog.Log("seal: zone final read was not staged ("
							+ (string.IsNullOrEmpty(failure) ? "unknown failure" : failure) + ")");
					}
				});
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneActivatedEvent E)
		{
			// The seat moves first. A second city's ground belongs to Away, not to ClaimedZones,
			// so a swap tested after the guard below could never fire: walking into your own
			// second city would read as walking into a stranger's zone.
			Guard("seat", delegate
			{
				if (TrySeat(E.Zone))
				{
					XRL.Messages.MessageQueue.AddPlayerMessage("You are in {{C|" + SeatName + "}}" + KingdomSettlement.VocationSuffix(Vocation) + ".");
				}
			});
			// Before the claim guard, for the same reason the seat is: a realm that put the
			// founder out no longer owns anything in ClaimedZones, so its ground reads as a
			// stranger's and this would never fire below.
			Guard("exile", delegate
			{
				OnZoneActivatedWhileExiled(E.Zone);
			});
			// Before the claim guard, for the same reason exile is: ground a city seceded from
			// stops being in ClaimedZones the moment it leaves (KingdomCreed.Secede), so a founder
			// standing on it would never be told below.
			Guard("seceded", delegate
			{
				if (E.Zone != null && KingdomCreed.SecededHolds(this, E.Zone.ZoneID))
				{
					XRL.Messages.MessageQueue.AddPlayerMessage("{{K|This ground isn't yours to keep anymore. (Charter: how your cities hold each other)}}");
				}
			});
			Guard("semantic activation", delegate
			{
				Simulation.City.KingdomSemanticDispatcher.OnZoneActivated(this, E.Zone,
					The.Game.TimeTicks, AttendSeatedSemantics);
			});
			return base.HandleEvent(E);
		}

		/// <summary>
		/// The single ordered attended settlement pass. Zone activation and the stationary
		/// end-turn scheduler both enter through <see cref="Simulation.City.KingdomSemanticDispatcher"/>,
		/// so waiting and crossing a boundary cannot select different implementations.
		/// </summary>
		private const long SemanticStepCheckIn = 1L << 0;
		private const long SemanticStepTrade = 1L << 1;
		private const long SemanticStepGrowth = 1L << 2;
		private const long SemanticStepPetitions = 1L << 3;
		private const long SemanticStepImprovement = 1L << 4;
		private const long SemanticStepBounties = 1L << 5;
		private const long SemanticStepRaids = 1L << 6;
		private const long SemanticStepWear = 1L << 7;
		private const long SemanticStepOffices = 1L << 8;
		private const long SemanticStepReach = 1L << 9;
		private const long SemanticStepLocus = 1L << 10;
		private const long SemanticStepGuestbook = 1L << 11;
		private const long SemanticStepCreed = 1L << 12;
		private const long SemanticStepFaith = 1L << 13;
		private const long SemanticStepHappenings = 1L << 14;
		private const long SemanticStepCheckOut = 1L << 15;
		private const long SemanticStepDigest = 1L << 16;
		private const long SemanticStepSeal = 1L << 17;
		private const long SemanticStepLab = 1L << 18;
		private const long SemanticStepConstruction = 1L << 19;

		private const long SemanticRequiredMask = (1L << 20) - 1L;

		private bool AttendSeatedSemantics(Zone Z)
		{
			if (!Founded || Z == null || !ClaimedZones.Contains(Z.ZoneID))
			{
				return false;
			}
			if (!PrepareSemanticPass(Z, The.Game.TimeTicks))
			{
				return false;
			}
			KingdomSurvey survey = null;
			Guard("survey", delegate
			{
				// The district-aware overload: a garrison district trains the whole watch, so the
				// bonus has to be on the shared survey Raids later reads defence from.
				survey = KingdomSurvey.Take(Z, this);
			});
			if (survey == null)
			{
				return false;
			}
			// The ledger is an unread report, not one pass's scratch buffer. It is cleared only
			// after the founder opens the report in the Charter; stationary daily reconciliation
			// therefore appends instead of erasing yesterday's news.
			// After survey and before trade, and the order is the whole of LIVING-CITY-ARCHITECTURE
			// §3.1: the model is advanced to now, this zone's standing debt is paid onto its real
			// containers in dedication order, the city's own stock is carried to where the founder
			// is standing, and then the ground overwrites the row. Everything below reads a ground
			// the book has already made true.
			if (!TrySemanticStep(SemanticStepCheckIn, "check-in", delegate
			{
				Simulation.City.KingdomCity.CheckIn(this, Z, survey, The.Game.TimeTicks);
				// What this city has room for, remembered for as long as the founder is away from it.
				LastKnownStorageSpace = survey.StorageSpace;
			}))
			{
				return false;
			}
			// Trade runs BEFORE growth, and the order is load-bearing. Both draw on one shared
			// survey, and growth is where upkeep is taken and the thirst ladder resolves. Water
			// that arrived this pass - a caravan under charter, a manifest sent from the realm's
			// other city - has to be in the stores before anything is drawn from them, or a
			// delivery sent precisely to end a drought would arrive one step too late to stop the
			// emigration it was sent to prevent.
			if (!TrySemanticStep(SemanticStepTrade, "trade", delegate
			{
				KingdomTrade.OnZoneActivated(this, Z, survey);
			})) return false;
			if (!TrySemanticStep(SemanticStepGrowth, "growth", delegate
			{
				KingdomGrowth.OnZoneActivated(this, Z, survey);
			})) return false;
			// Costed construction is an independent semantic lane. Growth's option controls new
			// settler arrivals, not whether an already-paid scaffold, plot, road, conversion, upgrade,
			// or repair may recover. Run its durable receipt resolver after upkeep/work assignment and
			// before any later luxury lane can spend the same remaining stores.
			if (!TrySemanticStep(SemanticStepConstruction, "construction", delegate
			{
				KingdomConstruction.OnSettlementPass(this, Z, survey);
			})) return false;
			// Petitions own their own option and calendar. They are settlement asks, not a side
			// effect of population growth, so disabling Growth cannot silence an accepted promise.
			if (!TrySemanticStep(SemanticStepPetitions, "petitions", delegate
			{
				KingdomPetitions.OnSettlementPass(this, Z, survey);
			})) return false;
			// After growth, and the order is load-bearing for the same reason trade runs before it:
			// growth is where this pass's arrivals, upkeep, and work assignment land, so the free
			// hands and the stores an improvement is allowed to draw on are only true once growth
			// has finished with them. An improvement is a luxury paid out of what is left.
			if (!TrySemanticStep(SemanticStepImprovement, "improvement", delegate
			{
				KingdomUpgrade.OnZoneActivated(this, Z, survey);
			})) return false;
			// After improvement, and the order is load-bearing for the same reason improvement runs
			// after growth: a posted price is paid out of what the stores still hold once the
			// settlement's own upkeep and arrivals are done with them, and a manning notice can only
			// fill an idleness AssignWork has already finished measuring.
			if (!TrySemanticStep(SemanticStepBounties, "bounties", delegate
			{
				KingdomBounty.OnSettlementPass(this, Z, survey);
			})) return false;
			if (!TrySemanticStep(SemanticStepRaids, "raids", delegate
			{
				KingdomRaids.OnZoneActivated(this, Z, survey);
			})) return false;
			// After raids, and the order is load-bearing in both directions. After growth, because
			// hard running is read off the crew stretch KingdomGrowth.AssignWork stamps on
			// KingdomEffectiveness. After bounties and raids, because both move a work this pass
			// and wear must see the result: a work the raiders just broke is counted and queued
			// for mending now rather than a whole pass later. Condition is no longer folded back
			// into KingdomEffectiveness -- each consumer applies KingdomWearRules.WorkEffectiveness
			// itself (Addendum 10(b)), so the ordering no longer decides that arithmetic. Raid damage itself is a separate hook inside KingdomRaids.ExecuteRaid,
			// invoked from the "raids" step above -- it does not run from here. Before reach, so a
			// damaged great work shades its ground by what it is actually managing.
			if (!TrySemanticStep(SemanticStepWear, "wear", delegate
			{
				KingdomWear.OnZoneActivated(this, Z, survey);
			})) return false;
			// The Lab reads staffing after growth and condition after wear. Its persisted job clock
			// receives the pass's stable start tick, so a failed later step and retry cannot mint
			// another slice of staffed work from wall-clock time that elapsed between attempts.
			if (!TrySemanticStep(SemanticStepLab, "lab work", delegate
			{
				KingdomLab.OnSemanticStep(this, Z, survey, SemanticPassStartedTick);
			})) return false;
			if (!TrySemanticStep(SemanticStepOffices, "offices", delegate
			{
				KingdomOffices.OnZoneActivated(this, Z);
			})) return false;
			// A great work is an office SEAT (Addendum 6), so the settlement's own office settles
			// first and the faith pass below can already ask what reaches whom.
			if (!TrySemanticStep(SemanticStepReach, "reach", delegate
			{
				KingdomReach.OnZoneActivated(this, Z, survey);
			})) return false;
			if (!TrySemanticStep(SemanticStepLocus, "locus", delegate
			{
				KingdomLocus.OnZoneActivated(this, Z, survey);
			})) return false;
			if (!TrySemanticStep(SemanticStepGuestbook, "guestbook", delegate
			{
				KingdomGuestbook.OnZoneActivated(this, Z, survey);
			})) return false;
			if (!TrySemanticStep(SemanticStepCreed, "creed", delegate
			{
				KingdomCreed.OnZoneActivated(this, Z);
			})) return false;
			if (!TrySemanticStep(SemanticStepFaith, "faith", delegate
			{
				KingdomFaith.OnZoneActivated(this, Z, survey);
			})) return false;
			// W4. After faith, and last of the resolvers, because a happening is a RENDERING of
			// what the pass has already settled: the creed the city holds with, the works that are
			// still turning, and who is left on the roll. Running it earlier would tell the founder
			// about a city one step out of date.
			if (!TrySemanticStep(SemanticStepHappenings, "happenings", delegate
			{
				Simulation.City.KingdomHappenings.OnZoneActivated(this, Z);
			})) return false;
			// The cheaper last read, and the one that usually beats SuspendingEvent there: what
			// this zone actually holds once the day has been drawn and the works have run. A
			// missed check-out costs freshness, never correctness (§3.4).
			if (!TrySemanticStep(SemanticStepCheckOut, "check-out", delegate
			{
				Simulation.City.KingdomCity.CheckOut(this, Z, survey, The.Game.TimeTicks);
			})) return false;
			if (!TrySemanticStep(SemanticStepDigest, "digest", delegate
			{
				if (Simulation.City.KingdomSemanticDispatcher.IsStationaryDispatch)
				{
					// The founder remained on this ground. Keep the presentation clock current, but
					// do not turn a daily settlement resolve into news of an absence that never happened.
					LastVisitTick = The.Game.TimeTicks;
					if (!Ledger.Any)
					{
						HomecomingDays = 0;
					}
					return;
				}
				long elapsed = The.Game.TimeTicks - LastVisitTick;
				// W4. What the told-log ring holds since the founder last stood here, counted into
				// the ordinary note lane before the report announces itself. Read from the ring
				// and nowhere else, so a happening is remembered once and reported once.
				Simulation.City.KingdomHappenings.Digest(this, City, LastVisitTick);
				LastVisitTick = The.Game.TimeTicks;
				int newlyAccounted = KingdomRules.ElapsedDays(elapsed);
				long totalAccounted = (long)HomecomingDays + newlyAccounted;
				HomecomingDays = (totalAccounted > int.MaxValue) ? int.MaxValue : (int)totalAccounted;
				if (Ledger.Any && elapsed >= KingdomRules.TicksPerDay)
				{
					// Nonmodal on purpose. You come home to a report, not an inspection: the
					// settlement says it has news and waits to be asked, in the Charter.
					XRL.Messages.MessageQueue.AddPlayerMessage("{{C|" + SeatName + "}} has news of the "
						+ ((HomecomingDays == 1) ? "day" : HomecomingDays + " days") + " you were away. {{K|(Charter: what happened while you were away)}}");
				}
			})) return false;
			// This is the coherent boundary for a settlement visit: intake, simulation, ground
			// publication, chronicle, and digest have all finished. The profile journal compares the
			// semantic snapshot and writes only when one of those facts actually changed.
			if (!TrySemanticStep(SemanticStepSeal, "seal stage", delegate
			{
				string failure;
				if (!KingdomSeal.TryStageSemanticSnapshot("settlement pass", out failure))
				{
					KingdomLog.Log("seal: settlement pass was not staged ("
						+ (string.IsNullOrEmpty(failure) ? "unknown failure" : failure) + ")");
				}
			})) return false;
			return (SemanticPassCompletedMask & SemanticRequiredMask) == SemanticRequiredMask;
		}

		/// <summary>Starts a new durable pass only after the previous receipt was published. An
		/// unfinished pass is tied to its original ground and resumes there even after more world
		/// time elapsed; every subsystem owns its own absolute catch-up clock.</summary>
		private bool PrepareSemanticPass(Zone Z, long NowTick)
		{
			Simulation.City.KingdomSemanticPassReceiptVerdict verdict =
				Simulation.City.KingdomSemanticClockRules.ReceiptVerdict(
					SemanticPassActive, SemanticPassStartedTick, SemanticPassZoneId,
					SemanticPassCompletedMask, SemanticRequiredMask, LastSemanticTick, Z.ZoneID);
			if (verdict == Simulation.City.KingdomSemanticPassReceiptVerdict.Start)
			{
				SemanticPassActive = true;
				SemanticPassStartedTick = (NowTick > 0L) ? NowTick : 0L;
				SemanticPassZoneId = Z.ZoneID;
				SemanticPassStartedMask = 0L;
				SemanticPassCompletedMask = 0L;
				return true;
			}
			if (verdict == Simulation.City.KingdomSemanticPassReceiptVerdict.RefuseDifferentGround)
			{
				KingdomLog.Log("semantic: unfinished pass remains bound to "
					+ (SemanticPassZoneId ?? "?") + "; refused resume on " + Z.ZoneID);
				return false;
			}
			return true;
		}

		/// <summary>One named subsystem receipt. Started is written before the call and completed
		/// only after it returns. A throw stops the pass without advancing LastSemanticTick; retry
		/// skips every completed predecessor and re-enters only the incomplete step.</summary>
		private bool TrySemanticStep(long Bit, string Step, System.Action Action)
		{
			if ((SemanticPassCompletedMask & Bit) != 0L)
			{
				return true;
			}
			SemanticPassStartedMask |= Bit;
			try
			{
				Action();
				SemanticPassCompletedMask |= Bit;
				return true;
			}
			catch (System.Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: semantic " + Step
					+ " failed; the pass remains recoverable", ex);
				KingdomLog.Log("SEMANTIC caught in " + Step + ": " + ex.Message);
				return false;
			}
		}

		/// <summary>
		/// Runs an action inside the engine's event dispatch without letting it escape.
		/// A failure is logged and the step is skipped; the host game and other systems
		/// are never affected. All engine-invoked entry points must route through this.
		/// </summary>
		/// <param name="Step">Short label identifying the step, used in the error log.</param>
		/// <param name="Action">The work to perform.</param>
		public static void Guard(string Step, System.Action Action)
		{
			try
			{
				Action();
			}
			catch (System.Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: " + Step + " failed and was skipped", ex);
				KingdomLog.Log("GUARD caught in " + Step + ": " + ex.Message);
			}
		}

		public override bool HandleEvent(AfterReputationChangeEvent E)
		{
			// The realm's own faction is excluded from the mirror below — a polity does not hold a
			// standing with itself — but it is the one faction whose reputation cell says what the
			// realm thinks of its founder, so it is read here instead of ignored.
			Guard("realm regard", delegate
			{
				if (Founded && !E.Transient && E.Faction != null && E.Faction.Name == KingdomFactionName)
				{
					OnRealmRegardChanged(E.Type);
				}
			});
			Guard("reputation mirror", delegate
			{
				if (Founded && !E.Transient && E.Faction != null && E.Faction.Name != KingdomFactionName && E.Faction.Name != "Player")
				{
					int delta = KingdomRules.SpilloverDelta(E.To - E.From, Stage);
					AdjustStanding(E.Faction.Name, delta);
					KingdomLog.Log("mirror: " + E.Faction.Name + " rep " + E.From + "->" + E.To + " spillover=" + delta + " standing=" + GetStanding(E.Faction.Name));
				}
			});
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(AfterGameLoadedEvent E)
		{
			if (LoadFailed)
			{
				Guard("load failure report", ReportLoadFailure);
			}
			Guard("feeling re-assert", ReassertFeelings);
			return base.HandleEvent(E);
		}

		/// <summary>
		/// The kingdom's standing with a faction. This is the kingdom's own ledger, separate
		/// from the founder's personal reputation: a faction may love the founder and resent
		/// the polity, or the reverse.
		/// </summary>
		/// <param name="FactionName">Faction name (not display name).</param>
		/// <returns>Standing on the vanilla reputation scale; 0 if never recorded.</returns>
		public int GetStanding(string FactionName)
		{
			if (FactionName == null || !Standings.TryGetValue(FactionName, out var value))
			{
				return 0;
			}
			return value;
		}

		/// <summary>
		/// Sets the kingdom's standing with a faction and mirrors the result into that
		/// faction's feeling toward the kingdom, so NPC attitudes follow.
		/// </summary>
		/// <param name="FactionName">Faction name (not display name). Ignored if null.</param>
		/// <param name="Value">New standing on the vanilla reputation scale.</param>
		/// <param name="Mirror">False to defer the feeling write (bulk edits); the mirror is
		/// re-asserted on game load regardless.</param>
		public void SetStanding(string FactionName, int Value, bool Mirror = true)
		{
			if (FactionName == null)
			{
				return;
			}
			Standings[FactionName] = Value;
			if (Mirror)
			{
				MirrorFeeling(FactionName);
			}
		}

		/// <summary>
		/// Adjusts the kingdom's standing with a faction by a delta. Use this rather than
		/// writing <see cref="Standings"/> directly so the feeling mirror stays consistent.
		/// </summary>
		/// <param name="FactionName">Faction name (not display name). Ignored if null.</param>
		/// <param name="Delta">Signed change; zero is a no-op.</param>
		/// <param name="Mirror">False to defer the feeling write.</param>
		public void AdjustStanding(string FactionName, int Delta, bool Mirror = true)
		{
			if (Delta != 0)
			{
				SetStanding(FactionName, GetStanding(FactionName) + Delta, Mirror);
			}
		}

		/// <summary>
		/// Writes one faction's feeling toward the kingdom from its recorded standing.
		/// Safe to call when unfounded or for unknown factions; does nothing in those cases.
		/// </summary>
		/// <param name="FactionName">Faction name (not display name).</param>
		public void MirrorFeeling(string FactionName)
		{
			if (!Founded || string.IsNullOrEmpty(FactionName)
				|| FactionName == KingdomFactionName || FactionName == "Player")
			{
				return;
			}
			// One projection is never allowed to abort the rest of a load-time reassertion. The
			// standings dictionary is durable truth; a missing or hostile faction implementation
			// merely leaves its derived feeling stale until the next retry.
			Guard("feeling projection " + (FactionName ?? "?"), delegate
			{
				// GetIfExists, never Get: a standings key can outlive the faction it names when a
				// save moves between builds.
				Faction faction = Factions.GetIfExists(FactionName);
				if (faction != null)
				{
					faction.SetFactionFeeling(KingdomFactionName,
						Reputation.GetFeeling((float)GetStanding(FactionName)));
				}
			});
		}

		/// <summary>
		/// Rewrites, from recorded state, every faction feeling the kingdom depends on. Called
		/// after load because the engine rebuilds feelings from its own reputation table and knows
		/// nothing about the kingdom's separate standings ledger.
		/// </summary>
		public void ReassertFeelings()
		{
			if (!Founded)
			{
				return;
			}
			foreach (KeyValuePair<string, int> standing in Standings)
			{
				MirrorFeeling(standing.Key);
			}
			// Derived from the founder's actual reputation, never hardcoded to 100. A realm holds
			// whatever opinion of its founder their deeds earned it: stamping love here on every
			// load would silently undo a fall in regard the moment the save was reloaded, and the
			// expulsion ladder reads no other surface. The context-free overload is deliberate —
			// the engine's own rebuild uses the holy-place-sensitive one, which can materialise a
			// neutral value as -50 depending on where the founder happens to be standing.
			Faction realm = Factions.GetIfExists(KingdomFactionName);
			if (realm != null)
			{
				realm.SetFactionFeeling("Player", Reputation.GetFeeling((float)FounderRegard()));
			}
		}

		private void NormalizeState(bool AllowLegacyIdentityMigration)
		{
			if (!Enum.IsDefined(typeof(GrowthStage), Stage))
			{
				Stage = GrowthStage.Camp;
			}
			if (LastMeal != KingdomRules.MealVerdict.None &&
				LastMeal != KingdomRules.MealVerdict.Scraps &&
				LastMeal != KingdomRules.MealVerdict.Plain &&
				LastMeal != KingdomRules.MealVerdict.Favored)
			{
				LastMeal = KingdomRules.MealVerdict.None;
			}
			if (Gate != KingdomRules.GatePolicy.Open &&
				Gate != KingdomRules.GatePolicy.Guarded)
			{
				Gate = KingdomRules.GatePolicy.Open;
			}
			if (Stores != KingdomRules.StoresPolicy.Plenty &&
				Stores != KingdomRules.StoresPolicy.Thrift)
			{
				Stores = KingdomRules.StoresPolicy.Plenty;
			}
			if (!Enum.IsDefined(typeof(KingdomRules.PetitionKind), PetitionKind))
			{
				PetitionKind = KingdomRules.PetitionKind.None;
			}
			if (!Enum.IsDefined(typeof(PetitionLifecycle), PetitionState))
			{
				PetitionState = PetitionLifecycle.None;
			}
			if (RaidState != 0 && RaidState != 1)
			{
				RaidState = 0;
				RaidFactionName = null;
				RaidDueTick = 0L;
			}
			if (City == null)
			{
				City = new Simulation.City.KingdomCityBook();
			}
			City.Normalize();
			if (LifecycleBook == null)
			{
				LifecycleBook = new KingdomLifecycleBook();
			}
			KingdomLifecycleRules.Normalize(LifecycleBook);
			if (CarryBook == null)
			{
				CarryBook = new KingdomCarryBook();
			}
			KingdomLifecycleRules.Normalize(CarryBook);
			if (Jobs == null)
			{
				Jobs = new Simulation.City.KingdomJobRegistry();
			}
			Jobs.Normalize();
			if (LastSliceTick < 0L)
			{
				LastSliceTick = 0L;
			}
			if (Bindings == null)
			{
				Bindings = new Simulation.City.KingdomBindingRegistry();
			}
			Bindings.Normalize();
			// A counter below zero would hand out an id a body may already carry, and an id that is
			// not unique is not an identity. Fails closed to "nothing enrolled yet"; the ids already
			// on bodies keep working, and the next mint starts over rather than colliding with one
			// this realm has definitely issued.
			if (ResidentCounter < 0)
			{
				ResidentCounter = 0;
			}
			if (ResearchShelf == null)
			{
				ResearchShelf = new Dictionary<string, int>();
			}
			// The lab mints nothing, so a negative accrual or stamp is a corrupt reading rather
			// than a city that owes its own bench: both fail closed to "nothing worked out yet".
			if (ResearchAccrued < 0)
			{
				ResearchAccrued = 0;
			}
			if (ResearchTakenUpTick < 0L)
			{
				ResearchTakenUpTick = 0L;
			}
			// A founded save written before cities had names of their own carries only the realm's.
			// The seat is that first city, so it takes that name rather than arriving unnamed.
			if (Founded && string.IsNullOrEmpty(SettlementName))
			{
				SettlementName = KingdomDisplayName;
			}
			if (!string.IsNullOrEmpty(Vocation) && !KingdomSettlement.IsKnownVocation(Vocation))
			{
				Vocation = KingdomSettlement.NeutralVocation;
			}
			// A stored level or stamp below zero is a corrupt reading, not a settlement in
			// debt: subsidence mints nothing, so both fail closed to "nothing measured yet".
			if (LastSubsidenceTick < 0L)
			{
				LastSubsidenceTick = 0L;
			}
			if (LastSemanticTick < 0L)
			{
				LastSemanticTick = 0L;
			}
			if (HomecomingDays < 0)
			{
				HomecomingDays = 0;
			}
			if (!SemanticPassActive)
			{
				SemanticPassStartedTick = 0L;
				SemanticPassZoneId = null;
				SemanticPassStartedMask = 0L;
				SemanticPassCompletedMask = 0L;
			}
			else if (SemanticPassStartedTick < 0L || string.IsNullOrEmpty(SemanticPassZoneId)
				|| SemanticPassStartedMask < 0L || SemanticPassCompletedMask < 0L
				|| (SemanticPassCompletedMask & ~SemanticPassStartedMask) != 0L)
			{
				// A corrupt receipt cannot safely say which subsystem already mutated. Drop only
				// the scheduler receipt; every subsystem's own absolute clock remains authoritative.
				SemanticPassActive = false;
				SemanticPassStartedTick = 0L;
				SemanticPassZoneId = null;
				SemanticPassStartedMask = 0L;
				SemanticPassCompletedMask = 0L;
			}
			if (SupportedLevel < 0)
			{
				SupportedLevel = 0;
			}
			// A shade below zero is a corrupt reading too: a notable is texture and never a tax,
			// so the worst any of them can be worth is nothing. Nothing clamps it from above
			// here - a shade a later build writes wider is still a number this one can read, and
			// KingdomCatalogueRules.LiftCapPercent binds whatever it is against the water.
			if (NotableShade < 0)
			{
				NotableShade = 0;
			}
			// The meal shade fails closed the same way and for the same reason: a day's
			// eating is never a tax, so the worst a bad supper can be worth is nothing.
			if (MealShade < 0)
			{
				MealShade = 0;
			}
			Away?.Normalize();
			Seceded?.Normalize();
			if (Dissent < 0 || Dissent > KingdomCreedRules.DissentBreaking)
			{
				Dissent = (Dissent < 0) ? 0 : KingdomCreedRules.DissentBreaking;
			}
			if (DissentSpoken < 0 || DissentSpoken > (int)CityTemper.Secession)
			{
				DissentSpoken = 0;
			}
			if (ConversionShared == null)
			{
				ConversionShared = new Dictionary<string, int>();
			}
			if (ConversionToward == null)
			{
				ConversionToward = new Dictionary<string, string>();
			}
			if (ConversionResented == null)
			{
				ConversionResented = new Dictionary<string, int>();
			}
			bool archiveTransactionActive = ExiledRealmArchive != null &&
				ExiledRealmArchive.Phase != KingdomRealmArchivePhase.None;
			// Once an archive phase exists, only its explicit exact-or-missing mirror CAS may
			// publish or retire mirror fields. Generic load normalization must not promote,
			// clear, allocate, or normalize one half of that transaction.
			if (!archiveTransactionActive)
			{
				if (ExiledStandings == null)
				{
					ExiledStandings = new Dictionary<string, int>();
				}
				if (Exiled)
				{
					// Legacy saves without an archive may promote their sole remembered city.
					if (ExiledSeat == null)
					{
						ExiledSeat = ExiledAway ?? new KingdomSettlement();
						ExiledAway = null;
					}
				}
				else
				{
					ExiledDisplayName = null;
					ExiledDeed = null;
					ExiledSeat = null;
					ExiledAway = null;
					ExiledStandings.Clear();
				}
				ExiledSeat?.Normalize();
				ExiledAway?.Normalize();
			}
			if (ExiledRealmArchive != null && !ExiledRealmArchive.Quarantined)
			{
				string archiveFailure;
				if (!ExiledRealmArchive.Validate(out archiveFailure))
					ExiledRealmArchive.Quarantine(archiveFailure);
			}
			if (RegardSpoken < (int)RealmRegard.Beloved || RegardSpoken > (int)RealmRegard.Repudiated)
			{
				RegardSpoken = (int)RealmRegard.Beloved;
			}
			if (RosterNames == null)
			{
				RosterNames = new List<string>();
			}
			if (RosterOrigins == null)
			{
				RosterOrigins = new List<string>();
			}
			if (RosterArrived == null)
			{
				RosterArrived = new List<string>();
			}
			if (DeadNames == null)
			{
				DeadNames = new List<string>();
			}
			if (DeadOrigins == null)
			{
				DeadOrigins = new List<string>();
			}
			if (DeadArrived == null)
			{
				DeadArrived = new List<string>();
			}
			if (DeadCauses == null)
			{
				DeadCauses = new List<string>();
			}
			KingdomSettlement.TruncateParallelRows(
				RosterNames, RosterOrigins, RosterArrived);
			KingdomSettlement.TruncateParallelRows(
				DeadNames, DeadOrigins, DeadArrived, DeadCauses);
			if (Ledger == null)
			{
				Ledger = new KingdomLedger();
			}
			Ledger.Normalize();
			if (ClaimedZones == null)
			{
				ClaimedZones = new List<string>();
			}
			NormalizeIdentity(AllowLegacyIdentityMigration);
			if (ZoneDistricts == null)
			{
				ZoneDistricts = new Dictionary<string, string>();
			}
			if (ActiveDealKeys == null)
			{
				ActiveDealKeys = new List<string>();
			}
			if (ActiveDealFactions == null)
			{
				ActiveDealFactions = new List<string>();
			}
			if (DealNextTicks == null)
			{
				DealNextTicks = new List<long>();
			}
			NormalizeTradeBook();
			if (ChronicleEntries == null)
			{
				ChronicleEntries = new List<string>();
			}
			if (OutsiderEntries == null)
			{
				OutsiderEntries = new List<string>();
			}
			if (OriginCounts == null)
			{
				OriginCounts = new Dictionary<string, int>();
			}
			if (CreedPastCounts == null)
			{
				CreedPastCounts = new Dictionary<string, int>();
			}
			if (CreedCounts == null)
			{
				CreedCounts = new Dictionary<string, int>();
			}
			if (Standings == null)
			{
				Standings = new Dictionary<string, int>();
			}
		}

		private void NormalizeIdentity(bool AllowLegacyMigration)
		{
			if (!Founded)
			{
				// A first-founding callback may save after exact ids were written but before the
				// faction/name publication. That complete transaction tuple is recoverable; every
				// other current-realm fragment is quarantined rather than guessed into authority.
				if (NewIdentityEvidenceEmpty() &&
					string.IsNullOrEmpty(PendingSettlementId) &&
					string.IsNullOrEmpty(PendingSettlementTransactionId) &&
					string.IsNullOrEmpty(PendingSettlementZoneId) &&
					string.IsNullOrEmpty(PendingSettlementAuthority)) return;
				if (RealmIdentityOrigin == KingdomIdentityOrigin.FoundingTransaction &&
					FirstIdentityMatches(RealmIdentityTransactionId,
						RealmIdentityFirstClaimedZone)) return;
				QuarantineIdentity("unfounded state carries partial current-realm identity");
				return;
			}

			if (NewIdentityEvidenceEmpty())
			{
				string migrationFailure = null;
				if (!AllowLegacyMigration || !TryMigrateLegacyIdentity(out migrationFailure))
				{
					QuarantineIdentity(AllowLegacyMigration
						? migrationFailure
						: "this named save has no immutable identity; pre-v8 authority is not readable");
					return;
				}
			}
			string lifecycleFailure;
			if (!TryBindDormantLifecycleIdentity(out lifecycleFailure))
			{
				QuarantineIdentity(lifecycleFailure);
				return;
			}
			if (Away != null)
			{
				if (Away.LifecycleBook == null) Away.LifecycleBook = new KingdomLifecycleBook();
				KingdomLifecycleRules.Normalize(Away.LifecycleBook);
				List<string> seatedLifecycleIds = LifecycleCollisionIds(
					IncludeSeat: true, IncludeAway: false);
				if (!KingdomLifecycleRules.BindSettlementIdentity(Away.LifecycleBook,
					Away.City?.SettlementId, LegacyMigration: false, MigrationKey: null,
					ExistingIds: seatedLifecycleIds))
				{
					Away.LifecycleBook.Quarantined = true;
					Away.LifecycleBook.Fault =
						"away lifecycle book does not match immutable city identity";
					QuarantineIdentity(Away.LifecycleBook.Fault);
					return;
				}
			}
			List<string> current;
			string failure;
			if (!TryExactSettlementIds(RequirePublishedClaims: true, out current,
				out failure))
			{
				// The exact first transaction is permitted to wait for its claim callback. It
				// grants no CurrentSettlementId until the claim exists.
				if (FirstIdentityMatches(RealmIdentityTransactionId,
					RealmIdentityFirstClaimedZone)) return;
				QuarantineIdentity(failure);
				return;
			}
			if (!PendingSettlementTupleValid(out string pendingFailure))
			{
				QuarantineIdentity(pendingFailure);
				return;
			}
			if (!string.IsNullOrEmpty(PendingSettlementId) &&
				current.Contains(PendingSettlementId))
			{
				// City publication won the save cut. Only explicit forward settlement may erase
				// the redo tuple; normalization never grows either authority book independently.
				if (!TrySettlePendingSettlementIdentity(PendingSettlementTransactionId,
					PendingSettlementZoneId, PendingSettlementAuthority, out failure))
				{
					QuarantineIdentity("published pending city could not settle exact topology: " +
						failure);
				}
			}
		}

		private bool TryMigrateLegacyIdentity(out string Failure)
		{
			Failure = null;
			string seatZone;
			string awayZone = null;
			if (!TryFirstClaimEvidence(ClaimedZones, out seatZone) ||
				(Away != null && !TryFirstClaimEvidence(Away.ClaimedZones, out awayZone)) ||
				string.IsNullOrEmpty(KingdomFactionName) || KingdomFactionName.Length > 512 ||
				(City != null && City.SettlementId != null && City.SettlementId.Length > 256) ||
				(Away?.City != null && Away.City.SettlementId != null &&
				 Away.City.SettlementId.Length > 256))
			{
				Failure = "legacy identity evidence is partial or outside hard bounds";
				return false;
			}
			KingdomIdentityFault fault;
			string realm;
			string seatId;
			string awayId = null;
			if (!KingdomIdentityRules.TryMigrateRealm(KingdomFactionName, FoundedTick,
					SimulationSeedHigh, SimulationSeedLow, seatZone, out realm, out fault) ||
				!KingdomIdentityRules.TryMigrateSettlement(realm, FoundedTick, seatZone,
					out seatId, out fault) ||
				(Away != null && !KingdomIdentityRules.TryMigrateSettlement(realm,
					Away.FoundedTick, awayZone, out awayId, out fault)))
			{
				Failure = "legacy identity evidence could not mint a complete set (" + fault + ").";
				return false;
			}
			List<string> ids = new List<string> { seatId };
			if (awayId != null) ids.Add(awayId);
			if (!KingdomIdentityRules.ValidateRealmTopology(realm, ids, out fault))
			{
				Failure = "legacy identity set is duplicate or malformed (" + fault + ").";
				return false;
			}
			string oldSeatId = City?.SettlementId;
			string oldAwayId = Away?.City?.SettlementId;
			RealmId = realm;
			RealmIdentityVersion = KingdomIdentityRules.RulesVersion;
			RealmIdentityOrigin = KingdomIdentityOrigin.LegacyMigration;
			RealmIdentityTransactionId = null;
			RealmIdentityLegacyFaction = KingdomFactionName;
			RealmIdentityFoundedTick = FoundedTick;
			RealmIdentitySeedHigh = SimulationSeedHigh;
			RealmIdentitySeedLow = SimulationSeedLow;
			RealmIdentityFirstClaimedZone = seatZone;
			if (City == null) City = new Simulation.City.KingdomCityBook();
			City.SettlementId = seatId;
			SettlementIdentityVersion = KingdomIdentityRules.RulesVersion;
			SettlementIdentityOrigin = KingdomIdentityOrigin.LegacyMigration;
			SettlementIdentityTransactionId = null;
			SettlementIdentityFoundedTick = FoundedTick;
			SettlementIdentityFirstClaimedZone = seatZone;
			SettlementIdentityLegacyId = oldSeatId;
			if (Away != null)
			{
				if (Away.City == null) Away.City = new Simulation.City.KingdomCityBook();
				Away.City.SettlementId = awayId;
				Away.SettlementIdentityVersion = KingdomIdentityRules.RulesVersion;
				Away.SettlementIdentityOrigin = KingdomIdentityOrigin.LegacyMigration;
				Away.SettlementIdentityTransactionId = null;
				Away.SettlementIdentityFoundedTick = Away.FoundedTick;
				Away.SettlementIdentityFirstClaimedZone = awayZone;
				Away.SettlementIdentityLegacyId = oldAwayId;
			}
			IdentityFault = null;
			return true;
		}

		private static bool TryFirstClaimEvidence(List<string> Claims, out string ZoneId)
		{
			ZoneId = null;
			if (Claims == null || Claims.Count < 1 || Claims.Count > 4096) return false;
			string first = Claims[0];
			if (string.IsNullOrEmpty(first) || first.Length > 512) return false;
			for (int i = 0; i < Claims.Count; i++)
				if (string.IsNullOrEmpty(Claims[i]) || Claims[i].Length > 512) return false;
			ZoneId = first;
			return true;
		}

		private bool PendingSettlementTupleValid(out string Failure)
		{
			Failure = null;
			bool any = !string.IsNullOrEmpty(PendingSettlementId) ||
				!string.IsNullOrEmpty(PendingSettlementTransactionId) ||
				!string.IsNullOrEmpty(PendingSettlementZoneId) ||
				!string.IsNullOrEmpty(PendingSettlementAuthority);
			if (!any) return true;
			string expected;
			KingdomIdentityFault fault;
			KingdomFoundingAuthority authority;
			if (string.IsNullOrEmpty(PendingSettlementId) ||
				string.IsNullOrEmpty(PendingSettlementZoneId) ||
				PendingSettlementZoneId.Length > 512 ||
				string.IsNullOrEmpty(PendingSettlementAuthority) ||
				PendingSettlementAuthority.Length > 4096 ||
				!KingdomIdentityRules.TryMintSettlement(RealmId,
					PendingSettlementTransactionId, out expected, out fault) ||
				expected != PendingSettlementId ||
				!KingdomFoundingTransactionRules.TryParseAuthority(
					PendingSettlementAuthority, out authority) ||
				authority.Kind != KingdomFoundingKind.SecondCity ||
				authority.TransactionID != PendingSettlementTransactionId ||
				authority.ZoneID != PendingSettlementZoneId ||
				authority.RealmFaction != KingdomFactionName)
			{
				Failure = "pending settlement identity evidence is partial or malformed";
				return false;
			}
			return true;
		}

		private bool NewIdentityEvidenceEmpty()
		{
			return string.IsNullOrEmpty(RealmId) && RealmIdentityVersion == 0 &&
				RealmIdentityOrigin == KingdomIdentityOrigin.None &&
				string.IsNullOrEmpty(RealmIdentityTransactionId) &&
				string.IsNullOrEmpty(RealmIdentityLegacyFaction) &&
				RealmIdentityFoundedTick == 0L && RealmIdentitySeedHigh == 0UL &&
				RealmIdentitySeedLow == 0UL &&
				string.IsNullOrEmpty(RealmIdentityFirstClaimedZone) &&
				string.IsNullOrEmpty(IdentityFault) && SettlementIdentityVersion == 0 &&
				SettlementIdentityOrigin == KingdomIdentityOrigin.None &&
				string.IsNullOrEmpty(SettlementIdentityTransactionId) &&
				SettlementIdentityFoundedTick == 0L &&
				string.IsNullOrEmpty(SettlementIdentityFirstClaimedZone) &&
				string.IsNullOrEmpty(SettlementIdentityLegacyId);
		}

		/// <summary>Binds Trade only from the complete immutable topology. Positional name rows
		/// remain quarantined evidence and are never promoted into live charter/manifest authority.</summary>
		private void NormalizeTradeBook()
		{
			if (TradeBook == null)
			{
				TradeBook = new KingdomTradeBook();
			}
			bool hasLegacyTrade = ActiveDealKeys.Count > 0 || ActiveDealFactions.Count > 0
				|| DealNextTicks.Count > 0 || Manifest != null;
			// Detect the dual graph before Trade recovery can settle or retire anything. Both
			// source graphs remain present as quarantined evidence; neither may be normalized
			// into authority first.
			if (hasLegacyTrade)
			{
				if (TradeBook.FormatVersion == KingdomTradeRules.CurrentFormatVersion &&
					TradeBook.SchemaState == KingdomTradeSchemaState.Compatible)
					KingdomTradeRules.QuarantineBook(TradeBook,
						"legacy name-based trade rows were preserved but cannot become live authority");
				return;
			}
			KingdomTradeRules.Normalize(TradeBook);
			// Unknown-future and quarantined books are evidence, not authority this build may
			// reinterpret. Preserve both the named-field graph and the legacy source rows.
			if (TradeBook.FormatVersion != KingdomTradeRules.CurrentFormatVersion ||
				TradeBook.SchemaState != KingdomTradeSchemaState.Compatible)
			{
				return;
			}
			if (ExiledRealmArchive != null &&
				ExiledRealmArchive.Phase != KingdomRealmArchivePhase.None) return;
			if (!Founded || !string.IsNullOrEmpty(IdentityFault)) return;
			if (!PendingSettlementIdentityAbsent())
			{
				// Paired second-city coordinator owns all pending topology changes. Load-time
				// normalization may recover Trade receipts, but never expand or contract Trade
				// alone across a save cut.
				return;
			}
			List<string> exact;
			string failure;
			if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
				IncludePending: false, out exact, out failure)) return;
			if (!TradeBook.IdentityBound)
			{
				// Trade may be one callback ahead of Core after atomically closing exile.
				// Preserve that exact unbound receipt for Exile recovery; any malformed or
				// wrong-topology archive evidence is quarantine, never fresh bind authority.
				if (TradeBook.Archives != null && TradeBook.Archives.Count > 0)
				{
					if (KingdomTradeRules.TryAuthenticateExactExileClosedTick(TradeBook,
						RealmId, exact, out long ignoredClosedTick, out failure)) return;
					KingdomTradeRules.QuarantineBook(TradeBook,
						failure ?? "unbound Trade exile receipt cannot be authenticated");
					return;
				}
				if (!KingdomTradeRules.BindExactIdentity(TradeBook, RealmId, exact,
					out failure)) return;
			}
			KingdomTradeRules.BindExactIdentity(TradeBook, RealmId, exact,
				out failure);
		}

	}
}
