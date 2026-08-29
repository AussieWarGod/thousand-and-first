using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	public partial class KingdomSettlement
	{
		/// <summary>Obsolete save-ABI projection of this city's resident rows.</summary>
		[Obsolete("Compatibility projection only; use resident-row authority.", false)]
		public List<string> RosterNames = new List<string>();

		[Obsolete("Compatibility projection only; use resident-row authority.", false)]
		public List<string> RosterOrigins = new List<string>();

		[Obsolete("Compatibility projection only; use resident-row authority.", false)]
		public List<string> RosterArrived = new List<string>();

		public Dictionary<string, int> OriginCounts = new Dictionary<string, int>();

		/// <summary>Live vanilla cultures borne by this city's citizens. Stored on the settlement
		/// so seat swap, secession, exile, return, and archive carry access with the people.</summary>
		public Dictionary<string, int> CultureCounts = new Dictionary<string, int>();

		/// <summary>Live vanilla species borne by this city's citizen bodies. Deliberately distinct
		/// from <see cref="CultureCounts"/>: culture says what a people knows; species what a body is.</summary>
		public Dictionary<string, int> SpeciesCounts = new Dictionary<string, int>();

		/// <summary>Live genotype/body and extension-owned identity keys carried by this city's
		/// resident bodies. Stored with the city so an away/seceded/archive roster answers without
		/// loading bodies.</summary>
		public Dictionary<string, int> IdentityCounts = new Dictionary<string, int>();

		/// <summary>This city's own tally of settler creeds. See <see cref="KingdomCreed"/>.
		/// Per-city, so it is carried by the seat swap like <see cref="OriginCounts"/>; the realm's
		/// dissent, declared creed, and secession state are not (see <c>KingdomSystem</c>, which
		/// carries those only on itself).</summary>
		public Dictionary<string, int> CreedCounts = new Dictionary<string, int>();

		/// <summary>
		/// Creeds this settlement's people have HELD AND LEFT. Per-city, so it is carried by the
		/// seat swap like <see cref="CreedCounts"/>; the realm's own belief is
		/// <c>KingdomSystem.DeclaredCreed</c> and is not this. See
		/// <c>KingdomSystem.CreedPastCounts</c> for what writes it and why it is a tally rather
		/// than a walk over the people.
		/// </summary>
		public Dictionary<string, int> CreedPastCounts = new Dictionary<string, int>();

		/// <summary>See <see cref="KingdomSystem.ConversionShared"/>. Per-city, so a founder who
		/// walks to the other city does not carry this one's half-turned believers with them.</summary>
		public Dictionary<string, int> ConversionShared = new Dictionary<string, int>();

		/// <summary>See <see cref="KingdomSystem.ConversionToward"/>.</summary>
		public Dictionary<string, string> ConversionToward = new Dictionary<string, string>();

		/// <summary>See <see cref="KingdomSystem.ConversionResented"/>.</summary>
		public Dictionary<string, int> ConversionResented = new Dictionary<string, int>();

		public KingdomRules.PetitionKind PetitionKind = KingdomRules.PetitionKind.None;

		public PetitionLifecycle PetitionState = PetitionLifecycle.None;

		public string PetitionEventId;

		public string PetitionOriginSettlementId;

		public string PetitionCauseSnapshot;

		public long LastPetitionMonthOrdinal = -1L;

		public string PetitionPetitioner;

		public string PetitionFaction;

		public int PetitionTarget;

		public long PetitionIssuedTick;

		public long LastPetitionTick;

		public int PetitionsMet;

		public int Dead;

		/// <summary>See <see cref="KingdomSystem.DeadNames"/>.</summary>
		public List<string> DeadNames = new List<string>();

		/// <summary>See <see cref="KingdomSystem.DeadOrigins"/>.</summary>
		public List<string> DeadOrigins = new List<string>();

		/// <summary>See <see cref="KingdomSystem.DeadArrived"/>.</summary>
		public List<string> DeadArrived = new List<string>();

		/// <summary>See <see cref="KingdomSystem.DeadCauses"/>.</summary>
		public List<string> DeadCauses = new List<string>();

		/// <summary>See <see cref="KingdomSystem.MemorialsRaised"/>.</summary>
		public int MemorialsRaised;

		/// <summary>See <see cref="KingdomSystem.OfficeHolderName"/>.</summary>
		public string OfficeHolderName;

		/// <summary>See <see cref="KingdomSystem.OfficeHolderResidentId"/>.</summary>
		public int OfficeHolderResidentId;

		/// <summary>
		/// Free space in this city's dedicated stores as of the last attended pass. Knowledge,
		/// not truth: it is exactly as stale as the founder's last visit, which is the whole
		/// point &mdash; a manifest is loaded against what the realm BELIEVES the other city can
		/// take. When the belief turns out wrong, water arrives with nowhere to go, and that is a
		/// story rather than a bug.
		/// </summary>
		public int LastKnownStorageSpace;

		/// <summary>Servings of this city's own harvest still on the road to one of its pantries.
		/// See <see cref="KingdomSystem.PendingCrop"/>. Carried, so a load gathered in the city
		/// the founder walked out of is still waiting there when they walk back in.</summary>
		public int PendingCrop;

		/// <summary>What that load physically is. See
		/// <see cref="KingdomSystem.PendingCropBlueprint"/>.</summary>
		public string PendingCropBlueprint;

		public KingdomLedger Ledger = new KingdomLedger();

		public long NextArrivalTick;

		public long NextGuestTick;

		public long GuestDepartTick;

		public bool FirstGuestGreeted;

		public long NextNotableGuestTick;

		public long NotableGuestDepartTick;

		public bool FirstNotableGuestLodged;

		public List<string> GuestbookLines = new List<string>();

		public List<string> ClaimedZones = new List<string>();

		public Dictionary<string, string> ZoneDistricts = new Dictionary<string, string>();

		/// <summary>
		/// What THIS city's keepers were taught, certified, and worked out: the encoded roster of
		/// <c>disk:</c>, <c>machine:</c>, <c>pattern:</c> and <c>node:</c> keys
		/// (<c>KingdomZoningRules.EncodeRoster</c>).
		/// <para>
		/// Addendum 22 B1, the knowledge siting. This used to be one game-global string, which meant
		/// a seceding city walked away with none of what it had itself learned and an exiled founder
		/// walked away with all of it. Sited here, secession, rejoin, exile and return handle
		/// knowledge with no knowledge-specific code in any of the four paths: the container goes,
		/// and the rolls go with it. Rejoin restores them whole and free, because rejoin restores
		/// the container (B6).
		/// </para>
		/// <para>
		/// The founder's own ledger of the world is not here: heard-of nodes live in the vanilla
		/// journal, while permanent <c>rite:</c> sources live in a bounded founder game-state ledger.
		/// Both survive secession, exile, and refounding (B2, B3). Cities keep rolls; the founder
		/// keeps leads and remembered covenants.
		/// </para>
		/// </summary>
		public string KeepersRoster;

		/// <summary>The one node this city's lab is working out, or null. One subject at a time:
		/// there is no queue, so there is nothing to schedule and nothing to optimise.</summary>
		public string ResearchSubject;

		/// <summary>Labour ticks banked against <see cref="ResearchSubject"/>.</summary>
		public int ResearchAccrued;

		/// <summary>
		/// Tick this city took up <see cref="ResearchSubject"/>. Nothing before it is ever banked:
		/// a bench that stood unlooked-at for a season and a subject set this morning charge from
		/// the same instant, so a city cannot bank an absence and cash it as a burst of thinking.
		/// Each lab keeps its own last-worked stamp and charges from whichever of the two is later.
		/// </summary>
		public long ResearchTakenUpTick;

		/// <summary>STANDARDS 7b's once-flag for a lab that will not progress &mdash; nobody at the
		/// bench, nobody clever enough, or nowhere to think at all. Cleared the moment the block
		/// lifts, so the sentence is unsaid as well as said.</summary>
		public bool ResearchStalledAnnounced;

		/// <summary>
		/// Subjects this city shelved, and the labour still standing on each. Shelving is memory,
		/// not a queue: nothing here progresses, and the founder can neither order it nor spend
		/// against it. Capped at <c>KingdomResearchRules.ShelfRows</c>; the row a ninth shelving
		/// pushes off is the least advanced, named once.
		/// </summary>
		public Dictionary<string, int> ResearchShelf = new Dictionary<string, int>();

		/// <summary>
		/// The highest Intelligence among this city's people as of the last attended pass, which is
		/// what decides which research tier its bench may work at (verdict 5). Knowledge, not truth:
		/// exactly as stale as the founder's last visit, like <see cref="SupportedLevel"/> and
		/// <see cref="LastKnownStorageSpace"/> beside it. Zero for a city no pass has measured.
		/// </summary>
		public int ResearchBestMind;

		/// <summary>Frozen provenance for <see cref="City"/>'s immutable settlement id. The same
		/// fields exist on <c>KingdomSystem</c>, so seat exchange carries identity without deriving
		/// it from the city's mutable name or current seat.</summary>
		public int SettlementIdentityVersion;

		public KingdomIdentityOrigin SettlementIdentityOrigin;

		public string SettlementIdentityTransactionId;

		public long SettlementIdentityFoundedTick;

		public string SettlementIdentityFirstClaimedZone;

		/// <summary>The retired pre-identity city label, retained only as migration evidence.</summary>
		public string SettlementIdentityLegacyId;

		/// <summary>Durable lifecycle authority carried with this exact city. Plain/notable guests,
		/// petitions, raids, and carry work publish and resume through their lane here; legacy
		/// scalars are migration/projection evidence only. Seat exchange moves this book with its
		/// settlement, never with the currently displayed city name.</summary>
		public KingdomLifecycleBook LifecycleBook = new KingdomLifecycleBook();

		/// <summary>
		/// This city's whole model: stocks, zone rows, work rows, and what each zone still owes its
		/// own containers. LIVING-CITY-ARCHITECTURE &sect;1.3 &mdash; one book per settlement, on
		/// the settlement, as a named-field composite.
		/// <para>
		/// Carried by the seat swap like <see cref="Ledger"/> is, and by exactly the same
		/// mechanism: a same-named field of the same type on <c>KingdomSystem</c>. This is what
		/// retired the <c>r_TAF_Supports_*</c> and <c>r_TAF_Larders_*</c> game-state key families
		/// &mdash; five ints per zone that had to be readable without loading a zone were the right
		/// answer for five ints, and the wrong answer for a hundred typed rows.
		/// </para>
		/// </summary>
		public Simulation.City.KingdomCityBook City = new Simulation.City.KingdomCityBook();
	}
}
