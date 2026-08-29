using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	/// <summary>
	/// Raised when a seat does not carry every field a settlement holds. Thrown before any
	/// value is written, so a mismatched seat is refused whole rather than half-filled.
	/// </summary>
	[Serializable]
	public class KingdomSeatMismatchException : Exception
	{
		public KingdomSeatMismatchException(string Message)
			: base(Message)
		{
		}
	}

	/// <summary>
	/// What a settlement is, apart from the founder standing in it: one city's whole state.
	/// <para>
	/// The realm keeps its faction, its standings and its chronicle; a city keeps its ground,
	/// its people, its water and its clocks. One faction, one history, two places it happened.
	/// </para>
	/// <para>
	/// The settlement the founder is standing in &mdash; the seat &mdash; lives in
	/// <see cref="KingdomSystem"/>'s flat fields, because that is what every consumer already
	/// reads. This record is the other city, and the two are exchanged when the founder walks
	/// from one to the other. The exchange is by field name (see <see cref="ReadFrom"/>), so a
	/// field added here is carried the moment a same-named field exists on the seat, and refused
	/// loudly when one does not.
	/// </para>
	/// </summary>
	[Serializable]
	public partial class KingdomSettlement
#if !TAF_TESTS
		: IComposite
#endif
	{
		/// <summary>How many cities one v1 realm may hold: one active seat and at most two
		/// immutable-id-ordered non-seat records.</summary>
		public const int MaxSettlements = KingdomSettlementTopologyRules.MaxOwnedSettlements;

		/// <summary>The vocation every site offers, and the one a founder who will not commit
		/// receives. It promises nothing, which is the point.</summary>
		public const string NeutralVocation = "holding";

		/// <summary>The vocations a second city may be founded for. Order is menu order;
		/// <see cref="NeutralVocation"/> comes last because it is the refusal.</summary>
		public static readonly string[] Vocations = new string[4] { "waystation", "refuge", "reliquary", NeutralVocation };

		/// <summary>What each vocation asks of the city, for the founding menu. One sentence of
		/// what it is, one of what it costs nobody.</summary>
		public static readonly string[] VocationBlurbs = new string[4]
		{
			"Water sold to whoever passes, and news given free. The city exists so the road has a middle.",
			"A wall and a bed for whoever arrives with nothing. The city exists to have room.",
			"What the realm means to keep is kept here. The city exists to remember.",
			"Ground held for its own sake, and no promise made about it. Nothing is closed off by this."
		};

		/// <summary>This settlement's own name. The realm's display name is a separate thing and
		/// does not change when a second city is founded.</summary>
		public string SettlementName;

		/// <summary>What the city was founded for, from <see cref="Vocations"/>, or null for a
		/// city founded before the realm had a second one to distinguish it from.</summary>
		public string Vocation;

		public string Style = "common";

		public string FoundingTerrainBlueprint;

		public string FoundingRegionName;

		public int FoundingZLevel;

		public long FoundedTick;

		public GrowthStage Stage = GrowthStage.Camp;

		public int Population;

		public int DryStreak;

		public bool Withered;

		/// <summary>Resolves in a row this city's ration bill went unpaid. Carried, so a city
		/// left hungry is still hungry when the founder walks back into it.</summary>
		public int HungerStreak;

		/// <summary>Whether this city stands marked by a long hunger. Carried beside
		/// <see cref="Withered"/>; both can be true at once.</summary>
		public bool Famished;

		public bool HasShopkeeper;

		public bool NoRoomAnnounced;

		public long LastHeartbeatTick;

		/// <summary>Tick this city's people last fetched water. Carried, so a dormant city does
		/// not fetch a windfall the moment it is seated.</summary>
		public long LastFetchTick;

		/// <summary>Tick this city's water works last produced. Carried, so a dormant city's
		/// works do not pour a windfall the moment it is seated.</summary>
		public long LastWaterWorkTick;

		/// <summary>Tick this city's fields last brought a harvest in. Carried for the reason
		/// <see cref="LastWaterWorkTick"/> is: a dormant city's fields do not fill a granary the
		/// moment it is seated.</summary>
		public long LastFoodWorkTick;

		/// <summary>Citizens of this city crewing works. Carried with the city.</summary>
		public int AssignedCrew;

		/// <summary>Settlers this city has on the water detail. Carried, so a city keeps its own
		/// standing orders while the founder is away in the other one.</summary>
		public int WaterCrew;

		public int IdleWorks;

		public int ShorthandedWorks;

		/// <summary>How many of the settlement's works stand damaged and run reduced
		/// (Addendum 7). Counted fresh by <c>KingdomWear</c> on every attended pass; carried
		/// between seats beside <see cref="IdleWorks"/> and <see cref="ShorthandedWorks"/>.
		/// </summary>
		public int DamagedWorks;

		public bool IdleWorksAnnounced;

		/// <summary>Whether this city has already been told its harvest has nowhere to go
		/// (STANDARDS 7b). Carried, so walking away and back does not re-say it.</summary>
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

		/// <summary>Read-compatible legacy field carried across seat swaps. Earlier saves priced a
		/// named notable into settlement capacity. Civic offices are now title-only, so normalization
		/// always retires this value to zero and no live formula reads it.</summary>
		public int NotableShade;


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

		/// <summary>Last completed once-per-world-day attended-semantic dispatch. Separate from
		/// <see cref="LastVisitTick"/>, which belongs to the homecoming report.</summary>
		public long LastSemanticTick;

		/// <summary>Receipt for a partially completed attended pass. Carried with the city so a
		/// seat swap cannot resume one settlement's work against another settlement's ground.</summary>
		public bool SemanticPassActive;

		public long SemanticPassStartedTick;

		public string SemanticPassZoneId;

		public long SemanticPassStartedMask;

		public long SemanticPassCompletedMask;

		public long LastVisitTick;

		/// <summary>Age of this city's unread ledger, never a realm-global presentation counter.</summary>
		public int HomecomingDays;

		public string LastDeed;

		public long LastDeedTick;

		public KingdomRules.GatePolicy Gate = KingdomRules.GatePolicy.Open;

		public KingdomRules.StoresPolicy Stores = KingdomRules.StoresPolicy.Plenty;

		public int RaidState;

		public string RaidFactionName;

		public long RaidDueTick;

		public long LastRaidTick;

		public int RaidTimesDeferred;

	}
}
