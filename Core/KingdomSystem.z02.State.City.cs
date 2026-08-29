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
		/// Realm trade authority. The three lists above and <see cref="LegacyManifestEvidence"/>
		/// retain bounded legacy evidence. <see cref="Manifest"/> is now only the serialized public
		/// compatibility projection of this book's manifest.
		/// </summary>
		public KingdomTradeBook TradeBook = new KingdomTradeBook();

		/// <summary>Realm polity truth. Factions, parties, encounters, and map marks are receipted
		/// projections of this bounded explicit-codec ledger, never alternate authority.</summary>
		public KingdomPolityLedger PolityLedger = new KingdomPolityLedger();

		public List<string> ChronicleEntries = new List<string>();

	}
}
