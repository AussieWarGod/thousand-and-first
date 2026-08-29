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
	public partial class KingdomSystem : IPlayerSystem
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
		/// <para>Version 9 separates faction-to-realm regard from realm-to-faction policy and
		/// preserves only provable explicit outbound edges from version 8. Missing or ambiguous
		/// edges remain unspecified; signed spillover carry starts empty.</para>
		/// </summary>
		private const int CurrentSerializationVersion = 9;

		private const int FirstNamedSerializationVersion = 8;

		private const int LegacyReflectedSerializationVersion = 1;

		public int SerializationVersion = CurrentSerializationVersion;

		/// <summary>Persisted realm-wide master-option observation. Unknown is the additive-save
		/// default; first observation initializes it without inventing an offline transition.</summary>
		public KingdomMasterLatchValue MasterOption;

		/// <summary>Tick of the last observed master transition.</summary>
		public long MasterOptionTick;

		/// <summary>Monotone resume identity issued only for Disabled -&gt; Enabled.</summary>
		public long MasterResumeToken;

		/// <summary>Last resume whose module reanchors all published successfully.</summary>
		public long MasterAppliedResumeToken;

		/// <summary>One latest successful load whose inheritance recovery waits for the master
		/// transition and its consumed wake. Named fields make this additive and reload-safe.</summary>
		public bool InheritanceResumePending;

		public int InheritancePendingLoadKindValue;

		public string InheritancePendingLoadSourceFailure;

		/// <summary>
		/// Set when the engine or <see cref="Read"/> could not interpret saved state. Not serialized:
		/// it describes this load, not the kingdom, and remains latched for the whole session.
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

	}
}
