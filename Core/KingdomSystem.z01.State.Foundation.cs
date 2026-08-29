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

		/// <summary>Read-compatible legacy field. Earlier saves priced a named notable into the
		/// settlement level. Civic offices are title-only in v1, so normalization always retires
		/// this value to zero and no live formula reads it. Keep the field for save/seat ABI.</summary>
		public int NotableShade;

		/// <summary>The settlement's live subsistence lift. Only its last attended meal contributes;
		/// an optional civic title never grants population, service, capability, or economy.</summary>
		public int Shade
		{
			get
			{
				return (MealShade < 0) ? 0 : MealShade;
			}
		}

		/// <summary>
		/// What this settlement's last day's eating was worth to the level, for exactly the day
		/// it was earned (<c>KingdomRules.MealShadeFor</c>). Re-drawn every heartbeat: a
		/// settlement that ate its own dish yesterday and scraps today is worth the scraps. It is
		/// capped by <c>KingdomCatalogueRules.LiftCapPercent</c>, so nobody eats past their own
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

		/// <summary>Obsolete save-ABI projection of the seated city's resident rows. Runtime code
		/// must read <c>KingdomResidents</c>; only load migration and one-way projection write this.</summary>
		[Obsolete("Compatibility projection only; use KingdomResidents resident-row APIs.", false)]
		public List<string> RosterNames = new List<string>();

		/// <summary>Obsolete compatibility column parallel to <see cref="RosterNames"/>.</summary>
		[Obsolete("Compatibility projection only; use KingdomResidents resident-row APIs.", false)]
		public List<string> RosterOrigins = new List<string>();

		/// <summary>Obsolete compatibility column parallel to <see cref="RosterNames"/>.</summary>
		[Obsolete("Compatibility projection only; use KingdomResidents resident-row APIs.", false)]
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
		/// the living resident-row projection this roll is never trimmed, because a memorial does not stop
		/// being true once a cairn is finally raised for it. Written only by
		/// <c>KingdomOffices.RecordDeath</c>, from the engine's own death event &mdash; never from
		/// a census, which could not tell a dead settler from one who simply wandered to another
		/// claimed zone.
		/// </summary>
		public List<string> DeadNames = new List<string>();

		/// <summary>Parallel to <see cref="DeadNames"/>.</summary>
		public List<string> DeadOrigins = new List<string>();

		/// <summary>Parallel to <see cref="DeadNames"/>: the day each one arrived, carried over
		/// from the exact resident row at the moment of death.</summary>
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
		/// and stored here &mdash; it is always the oldest on-roll resident row, with ResidentId as
		/// deterministic tie-break. This field exists only so a change in who that is can
		/// be noticed and announced once, rather than every time the settlement's ground is
		/// walked onto.
		/// </summary>
		public string OfficeHolderName;

		/// <summary>Stable identity of <see cref="OfficeHolderName"/>. Zero is the old-save
		/// migration boundary; the next office pass adopts the exact matching head row silently.</summary>
		public int OfficeHolderResidentId;

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

	}
}
