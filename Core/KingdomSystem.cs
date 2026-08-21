using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	[Serializable]
	public class KingdomSystem : IGameSystem
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
		private const int CurrentSerializationVersion = 3;

		private const int FirstNamedSerializationVersion = 3;

		private const int LegacyReflectedSerializationVersion = 1;

		public int SerializationVersion = CurrentSerializationVersion;

		/// <summary>
		/// Set when <see cref="Read"/> could not interpret the saved state. Not serialized: it
		/// describes this load, not the kingdom. Cleared once the founder has been told.
		/// </summary>
		[NonSerialized]
		public bool LoadFailed;

		/// <summary>
		/// Days accounted for by the homecoming report now waiting in the Charter. Not
		/// serialized: a homecoming is news about this visit, and a saved one would be told
		/// twice on the next load.
		/// </summary>
		[NonSerialized]
		public int HomecomingDays;

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
		/// Tick the settlement's water works last poured a day's making into the stores. Same
		/// checkpoint discipline as <see cref="LastFetchTick"/>, and planted before the first
		/// count for the same reason: unplanted, an uncapped read is the age of the world.
		/// </summary>
		public long LastWaterWorkTick;

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

		public int ShopTier;

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

		public List<string> ActiveDealKeys = new List<string>();

		public List<string> ActiveDealFactions = new List<string>();

		public List<long> DealNextTicks = new List<long>();

		public List<string> ChronicleEntries = new List<string>();

		public List<string> OutsiderEntries = new List<string>();

		public Dictionary<string, int> OriginCounts = new Dictionary<string, int>();

		/// <summary>The seated city's own tally of settler creeds. See <see cref="KingdomCreed"/>.
		/// Per-city and swapped with the seat exactly like <see cref="OriginCounts"/> &mdash; the
		/// counterpart <see cref="KingdomSettlement.CreedCounts"/> is what the reflected carry
		/// checks this field against.</summary>
		public Dictionary<string, int> CreedCounts = new Dictionary<string, int>();

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
		/// currently in &mdash; and every consumer reads those fields directly. The other city
		/// waits here, and the two are exchanged by <see cref="TrySeat"/> when the founder walks
		/// into its ground.
		/// </para>
		/// <para>
		/// A dormant city needs no clock. Its <c>LastHeartbeatTick</c> and <c>LastVisitTick</c>
		/// travel with it, so the ordinary catch-up in <c>KingdomGrowth</c> resolves the whole
		/// absence the moment it is seated &mdash; the lazy tick-stamp idiom vanilla uses for
		/// zone repair. The water half of that catch-up is now honest rather than capped
		/// (<see cref="KingdomRules.ElapsedDays"/>): a city dormant for a season drinks a season
		/// and fetches a season, and the two net. What is still bounded per visit is arrivals
		/// (<see cref="KingdomRules.MaxArrivalsPerVisit"/>), so a dormant city cannot arrive with
		/// a season of settlers, and the thirst ladder, which steps once per resolve however long
		/// the absence.
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
		/// A dormant realm needs no clock, exactly as <see cref="Away"/> does not: both cities keep
		/// their own <c>LastHeartbeatTick</c> and <c>LastVisitTick</c>, so a founder who is taken
		/// back resolves the whole absence through the ordinary capped catch-up rather than
		/// arriving to a season of settlers at once.
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
			ExileVerdict verdict = KingdomExileRules.JudgeExile(Founded, Exiled, KingdomExileRules.ClassifyRegard(FounderRegard()), Forced);
			if (verdict != ExileVerdict.Warranted)
			{
				Refusal = ExileRefusal(verdict);
				return false;
			}
			string realmName = KingdomDisplayName;
			string deed = string.IsNullOrEmpty(Deed) ? KingdomExileRules.DeedClause(null) : Deed;
			int cities = SettlementCount;
			// Written while the realm still stands, so the entry keys to it rather than to the
			// founder's unfounded interval, and so the book reads in the order the day happened.
			KingdomChronicle.RecordDisputed(this, KingdomExileRules.ExileTelling(realmName, deed), KingdomExileRules.ExileRumour(realmName, KingdomChronicle.FounderName()), Accomplishment: true);
			ExiledFactionName = KingdomFactionName;
			ExiledDisplayName = KingdomDisplayName;
			ExiledTick = The.Game.TimeTicks;
			ExiledDeed = deed;
			ExiledSeat = Capture();
			ExiledAway = Away;
			ExiledStandings = Standings;
			// A manifest belongs to the realm that loaded it. Left alone it would outlive that
			// realm on this singleton system, and the next realm's Charter would refuse to send
			// water while quoting two cities that are no longer the founder's.
			if (Manifest != null)
			{
				KingdomChronicle.Record(this, KingdomManifestRules.ManifestLapseDeed(Manifest.OriginName, Manifest.DestinationName, Manifest.Drams));
				KingdomLog.Log("manifest: voided by exile, " + Manifest.Drams + " drams " + Manifest.OriginName + " -> " + Manifest.DestinationName);
				Manifest = null;
			}
			KingdomFactionName = null;
			KingdomDisplayName = null;
			// Seating a blank settlement clears every per-settlement field there is, so a field
			// added later cannot be forgotten here.
			Restore(new KingdomSettlement());
			Away = null;
			Standings = new Dictionary<string, int>();
			RegardSpoken = (int)RealmRegard.Beloved;
			ReturnAskedRegard = int.MinValue;
			DoorClosedTold = false;
			The.Player?.GetPart<KingdomCharterPart>()?.RemoveAbility();
			KingdomLog.Log("exile: " + ExiledFactionName + " (" + cities + " cities, " + ExiledStandings.Count + " standings) put the founder out at regard " + ExiledRealmRegard() + "; deed=" + deed);
			Popup.Show(KingdomExileRules.ExileNotice(realmName, deed, cities));
			return true;
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
			int regard = ExiledRealmRegard();
			ReturnVerdict verdict = KingdomExileRules.JudgeReturn(Exiled, Founded, ExiledRealmKeptGround, Site != null && ExiledRealmHolds(Site.ZoneID), regard);
			if (verdict != ReturnVerdict.Allowed)
			{
				Refusal = KingdomExileRules.ReturnRefusal(verdict, ExiledDisplayName, KingdomDisplayName);
				return false;
			}
			// A remembered realm with no seat cannot be restored into. Promoting its other city
			// beats writing a null over the flat fields; only a save mangled elsewhere gets here.
			if (ExiledSeat == null)
			{
				ExiledSeat = ExiledAway ?? new KingdomSettlement();
				ExiledAway = null;
			}
			int restored = KingdomExileRules.RegardOnReturn(regard);
			KingdomFactionName = ExiledFactionName;
			KingdomDisplayName = ExiledDisplayName;
			Restore(ExiledSeat);
			Away = ExiledAway;
			Standings = ExiledStandings;
			ExiledFactionName = null;
			ExiledDisplayName = null;
			ExiledSeat = null;
			ExiledAway = null;
			ExiledStandings = new Dictionary<string, int>();
			ExiledDeed = null;
			ExiledTick = 0L;
			ReturnAskedRegard = int.MinValue;
			DoorClosedTold = false;
			RegardSpoken = (int)KingdomExileRules.ClassifyRegard(restored);
			Faction realm = Factions.GetIfExists(KingdomFactionName);
			if (realm != null)
			{
				// Set, never Modify. Modify is an award pipeline: it fires pre- and post-change
				// events, queues sounds, can pop up, and can file a journal accomplishment and a
				// persistent BecameLoved property. Being let back through a gate is a civic act,
				// and the mod says so in its own words rather than through that machinery.
				The.Game.PlayerReputation.Set(realm, restored);
			}
			ReassertFeelings();
			// The seat is whichever city was seated on the day of the expulsion; a founder who
			// walked back into the other one is corrected here rather than on the next zone change.
			TrySeat(Site);
			The.Player?.RequirePart<KingdomCharterPart>().EnsureAbility();
			KingdomChronicle.RecordDisputed(this, KingdomExileRules.ReturnTelling(KingdomDisplayName), KingdomExileRules.ReturnRumour(KingdomDisplayName, KingdomChronicle.FounderName()), Accomplishment: true);
			KingdomLog.Log("return: " + KingdomFactionName + " took the founder back at regard " + regard + " -> " + restored + "; seated " + SeatName);
			Popup.Show(KingdomExileRules.ReturnNotice(KingdomDisplayName, SeatName));
			return true;
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
					NormalizeState();
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
				NormalizeState();
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
			NormalizeState();
		}

		public override void Register(XRLGame Game, IEventRegistrar Registrar)
		{
			Registrar.Register(AfterReputationChangeEvent.ID);
			Registrar.Register(AfterGameLoadedEvent.ID);
			Registrar.Register(ZoneActivatedEvent.ID);
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
			if (!Founded || E.Zone == null || !ClaimedZones.Contains(E.Zone.ZoneID))
			{
				return base.HandleEvent(E);
			}
			KingdomSurvey survey = null;
			Guard("survey", delegate
			{
				// The district-aware overload: a garrison district trains the whole watch, so the
				// bonus has to be on the shared survey Raids later reads defence from.
				survey = KingdomSurvey.Take(E.Zone, this);
			});
			if (survey == null)
			{
				return base.HandleEvent(E);
			}
			Ledger.Reset();
			// What this city has room for, remembered for as long as the founder is away from it.
			LastKnownStorageSpace = survey.StorageSpace;
			// Trade runs BEFORE growth, and the order is load-bearing. Both draw on one shared
			// survey, and growth is where upkeep is taken and the thirst ladder resolves. Water
			// that arrived this pass - a caravan under charter, a manifest sent from the realm's
			// other city - has to be in the stores before anything is drawn from them, or a
			// delivery sent precisely to end a drought would arrive one step too late to stop the
			// emigration it was sent to prevent.
			Guard("trade", delegate
			{
				KingdomTrade.OnZoneActivated(this, E.Zone, survey);
			});
			Guard("growth", delegate
			{
				KingdomGrowth.OnZoneActivated(this, E.Zone, survey);
			});
			// After growth, and the order is load-bearing for the same reason trade runs before it:
			// growth is where this pass's arrivals, upkeep, and work assignment land, so the free
			// hands and the stores an improvement is allowed to draw on are only true once growth
			// has finished with them. An improvement is a luxury paid out of what is left.
			Guard("improvement", delegate
			{
				KingdomUpgrade.OnZoneActivated(this, E.Zone, survey);
			});
			// After improvement, and the order is load-bearing for the same reason improvement runs
			// after growth: a posted price is paid out of what the stores still hold once the
			// settlement's own upkeep and arrivals are done with them, and a manning notice can only
			// fill an idleness AssignWork has already finished measuring.
			Guard("bounties", delegate
			{
				KingdomBounty.OnSettlementPass(this, E.Zone, survey);
			});
			Guard("raids", delegate
			{
				KingdomRaids.OnZoneActivated(this, E.Zone, survey);
			});
			// After raids, and the order is load-bearing in both directions. After growth, because
			// hard running is read off the crew stretch KingdomGrowth.AssignWork stamps on
			// KingdomEffectiveness. After bounties and raids, because both move a work this pass
			// and wear must see the result: a work the raiders just broke is counted and queued
			// for mending now rather than a whole pass later. Condition is no longer folded back
			// into KingdomEffectiveness -- each consumer applies KingdomWearRules.WorkEffectiveness
			// itself (Addendum 10(b)), so the ordering no longer decides that arithmetic. Raid damage itself is a separate hook inside KingdomRaids.ExecuteRaid,
			// invoked from the "raids" step above -- it does not run from here. Before reach, so a
			// damaged great work shades its ground by what it is actually managing.
			Guard("wear", delegate
			{
				KingdomWear.OnZoneActivated(this, E.Zone, survey);
			});
			Guard("offices", delegate
			{
				KingdomOffices.OnZoneActivated(this, E.Zone);
			});
			// A great work is an office SEAT (Addendum 6), so the settlement's own office settles
			// first and the faith pass below can already ask what reaches whom.
			Guard("reach", delegate
			{
				KingdomReach.OnZoneActivated(this, E.Zone, survey);
			});
			Guard("locus", delegate
			{
				KingdomLocus.OnZoneActivated(this, E.Zone, survey);
			});
			Guard("guestbook", delegate
			{
				KingdomGuestbook.OnZoneActivated(this, E.Zone, survey);
			});
			Guard("creed", delegate
			{
				KingdomCreed.OnZoneActivated(this, E.Zone);
			});
			Guard("faith", delegate
			{
				KingdomFaith.OnZoneActivated(this, E.Zone, survey);
			});
			Guard("digest", delegate
			{
				long elapsed = The.Game.TimeTicks - LastVisitTick;
				LastVisitTick = The.Game.TimeTicks;
				HomecomingDays = KingdomRules.ElapsedDays(elapsed);
				if (Ledger.Any && elapsed >= KingdomRules.TicksPerDay)
				{
					// Nonmodal on purpose. You come home to a report, not an inspection: the
					// settlement says it has news and waits to be asked, in the Charter.
					XRL.Messages.MessageQueue.AddPlayerMessage("{{C|" + SeatName + "}} has news of the "
						+ ((HomecomingDays == 1) ? "day" : HomecomingDays + " days") + " you were away. {{K|(Charter: what happened while you were away)}}");
				}
			});
			return base.HandleEvent(E);
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
			if (!Founded || FactionName == KingdomFactionName || FactionName == "Player")
			{
				return;
			}
			// GetIfExists, never Get: Factions.Get throws on an unknown name, and a standings key
			// can outlive the faction it names when a save moves between builds. A throw here
			// would abort the whole re-assert loop, not just this one faction.
			Faction faction = Factions.GetIfExists(FactionName);
			if (faction != null)
			{
				faction.SetFactionFeeling(KingdomFactionName, Reputation.GetFeeling((float)GetStanding(FactionName)));
			}
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

		private void NormalizeState()
		{
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
			Away?.Normalize();
			Seceded?.Normalize();
			if (Dissent < 0 || Dissent > KingdomCreedRules.DissentBreaking)
			{
				Dissent = (Dissent < 0) ? 0 : KingdomCreedRules.DissentBreaking;
			}
			if (DissentSpoken < 0 || DissentSpoken > (int)CityTemper.Secession)
			{
				DissentSpoken = (DissentSpoken < 0) ? 0 : (int)CityTemper.Secession;
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
			if (ExiledStandings == null)
			{
				ExiledStandings = new Dictionary<string, int>();
			}
			if (Exiled)
			{
				// A remembered realm must have a seat to be restored into. Promoting its other
				// city beats refusing the return outright, and beats restoring a null.
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
			if (Ledger == null)
			{
				Ledger = new KingdomLedger();
			}
			Ledger.Normalize();
			if (ClaimedZones == null)
			{
				ClaimedZones = new List<string>();
			}
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
			int dealCount = Math.Min(ActiveDealKeys.Count, Math.Min(ActiveDealFactions.Count, DealNextTicks.Count));
			if (ActiveDealKeys.Count > dealCount)
			{
				ActiveDealKeys.RemoveRange(dealCount, ActiveDealKeys.Count - dealCount);
			}
			if (ActiveDealFactions.Count > dealCount)
			{
				ActiveDealFactions.RemoveRange(dealCount, ActiveDealFactions.Count - dealCount);
			}
			if (DealNextTicks.Count > dealCount)
			{
				DealNextTicks.RemoveRange(dealCount, DealNextTicks.Count - dealCount);
			}
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
			if (CreedCounts == null)
			{
				CreedCounts = new Dictionary<string, int>();
			}
			if (Standings == null)
			{
				Standings = new Dictionary<string, int>();
			}
		}
	}
}
