
namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{

		// ==================================================================================
		// W4 memos — what the city has already said, so it does not say it again
		// ==================================================================================

		/// <summary>
		/// The last ambient line this city said, as <c>KingdomAmbientRules</c> keys them.
		/// <para>
		/// Not part of the frozen model and deliberately so: it is not a fact about the city, it
		/// is a fact about the TELLING, and LIVING-CITY-ARCHITECTURE &sect;1.2 keeps those apart.
		/// It lives on the carrier because that is the thing that survives a zone going to disk,
		/// which is exactly the span over which "do not say it twice" has to hold.
		/// </para>
		/// </summary>
		public int AmbientKey;

		/// <summary>The world-day the ambient line was said on. A line repeats across a day
		/// boundary and never inside one &mdash; BUILDING-CATALOGUE-BRIEF Addendum 13 lane 3's
		/// "a line per state-change or per day, never per slice".</summary>
		public long AmbientDayOrdinal = -1L;

		/// <summary>What this city's creed last said about the founder's own body, as
		/// <c>KingdomNatureRules.RegardKey</c> folds it. Said once per state-change (Addendum 13
		/// lane 2), and a change is a different creed, part, sign, or chrome.</summary>
		public int RegardKey;

		/// <summary>
		/// The feast this city has already kept. Everything at or before it is behind us, so a
		/// season away replays no feasts and a fresh book keeps none it slept through.
		/// <para>
		/// Zero means "never looked", which the happenings layer answers by stamping the current
		/// tick rather than by firing a backlog: a city founded in Tebet Ux did not miss the Ides
		/// of Nivvun Ut, it did not exist for them.
		/// </para>
		/// </summary>
		public long LastFestivalTick;

		/// <summary>Qualifying disputed stories accumulated toward the next pilgrim. This is a
		/// city fact, not the realm-wide outsider register's current length.</summary>
		public int PilgrimLoudness;

		/// <summary><see cref="ThousandAndFirst.KingdomLocusRules.PilgrimState"/> stored as an int
		/// for named-field compatibility.</summary>
		public int PilgrimState;

		/// <summary>Monotonic city-local identity for causal pilgrim receipts.</summary>
		public int PilgrimSequence;

		/// <summary>Exact dated history which authorized the open opportunity.</summary>
		public long PilgrimCauseTick;

		public string PilgrimCause = "";

		/// <summary>Exact live body's engine id while the pilgrim stands at the heart.</summary>
		public string PilgrimObjectId = "";

		/// <summary>Frozen display identity once the opportunity first becomes a body. Keeping it
		/// in the city book makes a Chronicle receipt retry byte-identical after that body has left.</summary>
		public string PilgrimName = "";

		/// <summary>City display name at the causing history tick. Names may change; a pending
		/// receipt may not, so the visit tells the place it actually heard about.</summary>
		public string PilgrimPlaceName = "";

		/// <summary>One when the exact visitor received water. This outcome survives a partial
		/// Chronicle publication so recovery cannot rewrite a greeted visit as an unattended one.</summary>
		public int PilgrimGreeted;

		/// <summary>
		/// The epithet the city knows its office holder by, minted through vanilla's own
		/// <c>NameMaker</c> (<c>KingdomNotables</c>). Remembered here rather than only on the
		/// body, so a happening told while that body is two zones away and on disk can still name
		/// them properly.
		/// </summary>
		public string OfficeEpithet = "";

		/// <summary>
		/// Legacy aggregate tick for the published extension lane. Current source windows use
		/// <see cref="ExtensionHappeningCursors"/>; this value remains for old diagnostics and seeds
		/// exact active-source receipts once when an upgraded save has no per-source wire.
		/// <para>
		/// Zero means the retired aggregate lane never ran. A nonzero value never overrides a
		/// nonempty current wire.
		/// </para>
		/// </summary>
		public long LastExtensionTick;

		/// <summary>Bounded per-source last-ask receipts for the published happening API. Each row is
		/// keyed by immutable manifest ID plus exact assembly/type, so installing or faulting one
		/// source cannot move another source's window. Empty is fresh unless the retired aggregate
		/// receipt proves an upgrade; malformed non-empty data is retained and refused loudly rather
		/// than reset.</summary>
		public string ExtensionHappeningCursors = "";

		/// <summary>Largest canonical cursor wire accepted by the per-source codec.</summary>
		public const int MaxExtensionHappeningCursorChars =
			ThousandAndFirst.Api.KingdomHappeningCursorRules.MaxChars;

		/// <summary>
		/// Canonical API-v3 behaviour sidecar. The ordinary city model remains closed and frozen;
		/// extension resources, jobs, networks, and work run-state persist here as one bounded,
		/// versioned wire owned by <c>KingdomBehaviourRules</c>.
		/// <para>
		/// The carrier deliberately does not decode or repair this value. A malformed wire must be
		/// retained and refused loudly by the behaviour host, not silently converted into an empty
		/// city. Empty means no extension behaviour has ever been admitted for this settlement.
		/// </para>
		/// </summary>
		public string ExtensionModel = "";

		/// <summary>Largest canonical base64 carrier for the decoded sidecar cap.</summary>
		public const int MaxExtensionModelChars =
			((ThousandAndFirst.Api.KingdomApiRules.MaxBehaviourModelBytes + 2) / 3) * 4;

		/// <summary>
		/// Bounded lifecycle authority for one physically staged city happening. Resident and
		/// fixture properties are projections of this wire: deleting a fixture or interrupting a
		/// walk therefore degrades to a dated unattended report and schedule restoration rather
		/// than losing the semantic event or leaving a body posted forever.
		/// </summary>
		public string HappeningModel = "";

		/// <summary>Largest canonical base64 carrier for the physical-happening sidecar.</summary>
		public const int MaxHappeningModelChars = KingdomHappeningLifecycleRules.MaxWireChars;

		/// <summary>
		/// The blocking verdict the citizen rite last reported, plus one, so that zero &mdash; what
		/// an absent field reads as &mdash; means "nothing was blocked". STANDARDS &sect;7b's
		/// announce-once flag for lane 1, kept on the book rather than on the body because the
		/// block is a fact about the realm's own faction and not about any one settler.
		/// </summary>
		public int RiteBlocked;
	}
}
