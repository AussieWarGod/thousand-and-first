using System;

using XRL;
using XRL.UI;
using XRL.World;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The happenings layer at the engine edge: the draws, the telling, and the ring.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;7.4 W4 &mdash; <i>"the generator, the shared telling budget,
	/// the told-log ring &hellip; surfaced through ledger / KingdomWord / chronicle."</i> Every
	/// verdict this file acts on comes from <see cref="KingdomHappeningRules"/>, which is pure;
	/// everything this file adds is the three things a pure rule cannot reach &mdash; the kernel
	/// key, the clock, and the surfaces the founder actually reads.
	/// </para>
	/// <para>
	/// <b>The mesh condition</b> (BUILDING-CATALOGUE-BRIEF Addendum 13). Domain outcomes still
	/// belong to the city rows that generated them. A bounded lifecycle sidecar holds only the
	/// temporary authority required to stage exact resident bodies at an authored locus, resume
	/// across a save, restore their ordinary schedules, and deliver the existing surfaces once.
	/// It is not a second happening history or message channel: pushes go through
	/// <c>KingdomWord</c>, the book goes through <c>KingdomChronicle</c>, and the told-log ring is
	/// still the announce-once authority.
	/// </para>
	/// <para>
	/// <b>The budget.</b> Recording is unbudgeted, because the chronicle and the ring are PULL
	/// surfaces the founder opens when they choose. Only the PUSH is budgeted, and it is budgeted
	/// out of the heartbeat's own &le; 1 told line an in-game hour (&sect;3.6): a happening never
	/// gets a line of its own beside the slice's.
	/// </para>
	/// </summary>
	public static partial class KingdomHappenings
	{
		/// <summary>
		/// The gate. Live the moment a checkbox line for it lands in <c>Options.xml</c> beside the
		/// mod's others; until then it reads its own default, which is <b>Yes</b> like every other
		/// option this mod ships except the prefetch.
		/// </summary>
		public const string HappeningsOption = "r_TAF_OptionHappenings";

		/// <summary>What the gate reads while <c>Options.xml</c> has no line for it.</summary>
		public const string HappeningsDefault = "Yes";

		public static bool Enabled
		{
			get { return Options.GetOption(HappeningsOption, HappeningsDefault) != "No"; }
		}

		// ==================================================================================
		// Determinism — the kernel keys (§2.4)
		// ==================================================================================

		/// <summary>Rules version pinned into every happening draw's <see cref="SemanticEventKey"/>.
		/// The key owns its rules version forever, so this moves only if the draw is redefined in
		/// a way that must not compare equal to what came before.</summary>
		private const int HappeningRulesVersion = 1;

		/// <summary>Ordinal lane for wedding draws. Distinct from every other kernel-backed draw
		/// in the mod, so a wedding and an outsider rumour can never shift each other.</summary>
		private const string WeddingStreamId = "taf:happening:wedding:v1";

		private const uint WeddingEventKind = 1u;

		private const uint WeddingDrawIndex = 0u;

		/// <summary>Fixed, all-zero seed, for the reason <c>KingdomChronicle</c> gives at length:
		/// domain separation comes from the settlement id, stream, kind and ordinal baked into the
		/// key, and who marries whom does not need to be unguessable.</summary>
		private static readonly KernelSeed128 HappeningSeed = default(KernelSeed128);

		// ==================================================================================
		// Bounds (§0.0(a): per happening, never per day)
		// ==================================================================================

		/// <summary>
		/// Draws the wedding lane may make in one reckoning.
		/// <para>
		/// &sect;0.0(a) caps a city pass at 512 draws and requires that draws be per happening and
		/// never per day; eight is per <i>candidate pair</i>, and a pair is only a candidate when
		/// two rows already share a roof. The scan itself is a single integer comparison over
		/// pairs of resident rows &mdash; at the model's cap, 60 rows, that is 1,770 comparisons,
		/// inside the 14,848 row-visits the reckon budget allows and unaffected by how long the
		/// founder has been gone.
		/// </para>
		/// </summary>
		internal const int MaxWeddingDraws = 8;

		/// <summary>Weddings one reckoning may find. The rest wait for the next pass rather than
		/// all happening at once off-screen.</summary>
		internal const int MaxWeddingsPerReckon = 2;

		/// <summary>Works one reckoning will judge for a breakdown. The rest wait for the next
		/// pass rather than all changing at once off-screen &mdash; <c>KingdomOffices</c>'s own
		/// discipline for cairns.</summary>
		internal const int MaxBreakdownsPerReckon = 2;

		/// <summary>
		/// Feasts a reckoning will walk forward through before it stops walking and JUMPS to the
		/// most recent one. Sixteen is fifteen months, so an ordinary long absence is recorded
		/// feast by feast and a decade-long one costs exactly the same.
		/// </summary>
		internal const int MaxFestivalScan = 16;

		// ==================================================================================
		// The reckon-time pass
		// ==================================================================================

		/// <summary>
		/// One city's happenings, at the tick the model has just been advanced to.
		/// <para>
		/// Called from the heartbeat slice for every city and from the settlement pass for the
		/// seated one, which is the whole of &sect;3.6's <i>"all cities, not just the seated
		/// one"</i>: the second city's wedding reaches a founder standing in the first.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="book">The city's book, already advanced and published.</param>
		/// <param name="label">The city's display name, for the away framing.</param>
		/// <param name="here">Whether the founder is standing in this city.</param>
		/// <param name="nowTick">The tick reckoned to.</param>
		/// <param name="pushBudget">Told lines this call may PUSH. Recording is unbudgeted.</param>
		/// <returns>How many lines were pushed.</returns>
		internal static int Reckon(KingdomSystem System, KingdomCityBook book, string label, bool here, long nowTick, int pushBudget)
		{
			if (System == null || !System.Founded || book == null || nowTick <= 0L)
			{
				return 0;
			}
			// Lifecycle recovery is independent of the option gate. Turning happenings off cannot
			// strand a resident's post receipt or leave a completed rite half-published.
			int pushed = KingdomPhysicalHappenings.Drive(System, book, label, here, nowTick,
				pushBudget);
			if (!Enabled) return pushed;
			KingdomCityState state;
			KingdomCityFault fault;
			if (!book.TryRead(out state, out fault))
			{
				return 0;
			}
			KingdomCityState opened = state;
			state = Festivals(System, book, state, label, here, nowTick, ref pushed, pushBudget);
			state = Funerals(System, book, state, label, here, nowTick, ref pushed, pushBudget);
			state = Weddings(System, book, state, label, here, nowTick, ref pushed, pushBudget);
			state = Breakdowns(System, state, label, here, nowTick, ref pushed, pushBudget);
			// Publish only when the ring actually gained a line. Every transition here is
			// copy-on-write (§1.3), so an unchanged book is the SAME reference — and a slice that
			// rewrote every column of every city every fifty ticks to record nothing would be a
			// cost the constitution never budgeted.
			if (!ReferenceEquals(state, opened) && !book.TryPublish(state, out fault))
			{
				KingdomLog.Log("city: happenings refused (" + fault + "); the book is unchanged");
			}
			return pushed;
		}

		/// <summary>
		/// The settlement pass's own happenings step: the seated city, reckoned to now, with the
		/// founder standing in it.
		/// <para>
		/// Runs last of the resolvers because a happening is a rendering of what the pass has
		/// already settled. The push budget is two &mdash; a founder who has just walked in is
		/// already being handed a homecoming report, and four interruptions on one pass is a
		/// notification channel rather than a city.
		/// </para>
		/// </summary>
		public static void OnZoneActivated(KingdomSystem System, Zone Z)
		{
			if (System == null || !System.Founded || Z == null || The.Game == null
				|| System.ClaimedZones == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			int told = Reckon(System, System.City, System.SeatName, true, The.Game.TimeTicks, SeatedPushBudget);
			// Lane 2 before lane 3: what the city's creed makes of the founder is news the first
			// time it is true and nothing at all afterwards, and the hour's texture is always there
			// to be said tomorrow.
			told += KingdomAmbient.Regard(System, System.City, KingdomCreed.SeatCreed(System), System.SeatName, true);
			// The published lane last, and out of the SAME budget: LIVING-CITY-ARCHITECTURE §6.6
			// clause 4 says an extension cannot flood the register any more than we can, and the
			// only way to mean that is to spend from one purse. What the city itself has to say
			// outranks what a mod taught it, on a pass where only one line fits.
			told += Extensions(System, System.City, System.SeatName, true, The.Game.TimeTicks, SeatedPushBudget - told);
			if (told <= 0)
			{
				KingdomAmbient.Speak(System, System.City, System.SeatName, true, The.Game.TimeTicks);
			}
		}

		/// <summary>
		/// Asks every registered happening source what has happened since it was last asked, and
		/// surfaces the answers through the city's own surfaces.
		/// <para>
		/// Preconditions: a founded realm with a book. Side effects: advances each source's bounded
		/// <c>KingdomCityBook.ExtensionHappeningCursors</c> receipt, records to the chronicle, and pushes at most
		/// <paramref name="spare"/> spoken lines. Failure mode: a source that throws or runs long
		/// stalls itself; the cursor still advances, because a source that cannot answer for a
		/// window is not owed that window forever.
		/// </para>
		/// </summary>
		private static int Extensions(KingdomSystem System, KingdomCityBook book, string label, bool here, long nowTick, int spare)
		{
			if (book == null || nowTick <= 0L || !Api.KingdomExtensions.AnyHappeningSource())
			{
				return 0;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!book.TryRead(out state, out fault))
			{
				// Logged rather than announced, and deliberately on the same terms as
				// KingdomHappenings.Reckon's own unreadable-book return above: one unreadable book
				// is one fault, and it belongs in one place. The founder meets it where they would
				// look for it - the city book and the asks board both say so out loud.
				KingdomLog.Log("city: the extension lane found the book unreadable (" + fault + ")");
				return 0;
			}
			// Retain the legacy aggregate clock for old diagnostics and snapshots. An absent v12
			// cursor uses its previous value once to seed exact active-source receipts; after that it
			// never authorizes a source window.
			long legacySinceTick = book.LastExtensionTick;
			book.LastExtensionTick = nowTick;
			return Api.KingdomExtensions.Happenings(System,
				KingdomReadingRules.Project(label, state, book.ExtensionModel), label, here,
				book.ExtensionHappeningCursors,
				delegate(string replacement) { book.ExtensionHappeningCursors = replacement; },
				legacySinceTick, nowTick, (spare > 0) ? spare : 0);
		}

		/// <summary>Told lines the settlement pass may push about happenings. Two, and the
		/// ambience only speaks when neither of them did.</summary>
		internal const int SeatedPushBudget = 2;
	}
}
