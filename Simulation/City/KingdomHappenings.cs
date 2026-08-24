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
	/// <b>The mesh condition, as a list of things this file does not have</b>
	/// (BUILDING-CATALOGUE-BRIEF Addendum 13). No event queue. No happening table. No second
	/// message channel: pushes go through <c>KingdomWord</c>, which the brink lane already owns,
	/// and the book gets its lines through <c>KingdomChronicle</c>, which already writes both
	/// registers &mdash; so a feast reaching the outsider register (lane 6) costs nothing but
	/// recording the feast. No second announce-once ledger: the told-log ring is asked.
	/// </para>
	/// <para>
	/// <b>The budget.</b> Recording is unbudgeted, because the chronicle and the ring are PULL
	/// surfaces the founder opens when they choose. Only the PUSH is budgeted, and it is budgeted
	/// out of the heartbeat's own &le; 1 told line an in-game hour (&sect;3.6): a happening never
	/// gets a line of its own beside the slice's.
	/// </para>
	/// </summary>
	public static class KingdomHappenings
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
			if (!Enabled || System == null || !System.Founded || book == null || nowTick <= 0L)
			{
				return 0;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!book.TryRead(out state, out fault))
			{
				return 0;
			}
			int pushed = 0;
			KingdomCityState opened = state;
			state = Festivals(System, book, state, label, here, nowTick, ref pushed, pushBudget);
			state = Funerals(System, state, label, here, nowTick, ref pushed, pushBudget);
			state = Weddings(System, state, label, here, nowTick, ref pushed, pushBudget);
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
			if (!Enabled || System == null || !System.Founded || Z == null || The.Game == null
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
		/// Preconditions: a founded realm with a book. Side effects: advances
		/// <c>KingdomCityBook.LastExtensionTick</c>, records to the chronicle, and pushes at most
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
			if (book.LastExtensionTick <= 0L)
			{
				// Never asked. Stamp and keep nothing, exactly as the festival cursor does: an
				// extension installed today did not miss last year.
				book.LastExtensionTick = nowTick;
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
			long since = book.LastExtensionTick;
			book.LastExtensionTick = nowTick;
			return Api.KingdomExtensions.Happenings(System,
				KingdomReadingRules.Project(label, state), label, here, since, nowTick, (spare > 0) ? spare : 0);
		}

		/// <summary>Told lines the settlement pass may push about happenings. Two, and the
		/// ambience only speaks when neither of them did.</summary>
		internal const int SeatedPushBudget = 2;

		// ==================================================================================
		// Festivals — Qud's own calendar, never an invented holiday
		// ==================================================================================

		private static KingdomCityState Festivals(KingdomSystem System, KingdomCityBook book, KingdomCityState state, string label, bool here, long nowTick, ref int pushed, int pushBudget)
		{
			if (book.LastFestivalTick <= 0L)
			{
				// Never looked. Stamp now and keep nothing: a city founded in Tebet Ux did not
				// miss the Ides of Nivvun Ut, it did not exist for them.
				book.LastFestivalTick = nowTick;
				return state;
			}
			long cursor = book.LastFestivalTick;
			int kept = 0;
			long due;
			KingdomFestivalAnchor anchor;
			while (kept < MaxFestivalScan
				&& KingdomHappeningRules.TryNextFestival(cursor, out due, out anchor)
				&& due <= nowTick)
			{
				cursor = due;
				kept++;
				state = KeepFeast(System, state, label, here, due, anchor, ref pushed, pushBudget);
			}
			if (kept >= MaxFestivalScan)
			{
				// Out of scan and still behind. Jump, closed-form, rather than keep walking:
				// §0.0(a) bans any term containing the elapsed, and the walk is the term.
				long last;
				KingdomFestivalAnchor lastAnchor;
				if (KingdomHappeningRules.TryLastFestival(nowTick, out last, out lastAnchor) && last > cursor)
				{
					cursor = last;
				}
			}
			book.LastFestivalTick = cursor;
			return state;
		}

		private static KingdomCityState KeepFeast(KingdomSystem System, KingdomCityState state, string label, bool here, long tick, KingdomFestivalAnchor anchor, ref int pushed, int pushBudget)
		{
			int mouths = OnTheRoll(state);
			string dish = (System.DishName ?? "").Trim();
			string telling = KingdomHappeningRules.FestivalTelling(anchor, KingdomWord.CityName(System, label), dish, mouths);
			// RecordDisputed, not Record: a feast is exactly the kind of thing the roads hear a
			// different version of, and the outsider register is what lane 6 asks us to feed. The
			// clause is already third person, so no founder's voice gets into the rumour.
			KingdomChronicle.RecordDisputed(System, telling, KingdomHappeningRules.AnchorName(anchor)
				+ " was kept at " + KingdomWord.CityName(System, label) + ", and there was enough of it to talk about");
			state = Tell(state, new KingdomHappening(KingdomHappeningKind.Festival, tick, 0, 0, null, (int)anchor));
			if (pushed < pushBudget)
			{
				// In a settler's mouth, through the voice machinery that already wraps the
				// settlement's announcements: the news is the same, and somebody who lives there
				// says it.
				KingdomWord.Ambient(System, label, here, KingdomVoices.Say(System, VoiceOccasion.Feast,
					KingdomHappeningRules.FestivalNotice(anchor, KingdomWord.CityName(System, label), dish)));
				pushed++;
			}
			KingdomLog.Log("happening: feast " + anchor + " at " + label + " dish=" + (string.IsNullOrEmpty(dish) ? "-" : dish) + " mouths=" + mouths);
			return state;
		}

		// ==================================================================================
		// Weddings — two rows that already share a roof
		// ==================================================================================

		private static KingdomCityState Weddings(KingdomSystem System, KingdomCityState state, string label, bool here, long nowTick, ref int pushed, int pushBudget)
		{
			int draws = 0;
			int found = 0;
			for (int i = 0; i < state.ResidentCount && found < MaxWeddingsPerReckon && draws < MaxWeddingDraws; i++)
			{
				KingdomResidentRow a;
				if (!state.TryResident(i, out a) || a.Standing != KingdomResidentStanding.Resident || a.HomeWorkId <= 0)
				{
					continue;
				}
				for (int j = i + 1; j < state.ResidentCount && found < MaxWeddingsPerReckon && draws < MaxWeddingDraws; j++)
				{
					KingdomResidentRow b;
					// One integer comparison rejects every pair that does not already share a
					// roof, which is nearly all of them, before anything more expensive happens.
					if (!state.TryResident(j, out b) || b.HomeWorkId != a.HomeWorkId)
					{
						continue;
					}
					int hostility = KingdomHappeningRules.CreedHostility(a.CreedCode, b.CreedCode);
					if (!KingdomHappeningRules.WeddingEligible(a, b, hostility, nowTick))
					{
						continue;
					}
					int first;
					int second;
					KingdomHappeningRules.PairOrder(a.ResidentId, b.ResidentId, out first, out second);
					if (KingdomHappeningRules.AlreadyTold(state, KingdomHappeningKind.Wedding, first, second))
					{
						continue;
					}
					draws++;
					// The ordinal is the WORLD-DAY, not the tick. A slice runs every fifty ticks,
					// so a per-tick ordinal would give a settled pair twenty-four rolls a day and
					// marry the whole city inside a week. One roll a day per pair is the honest
					// reading of "a chance per pair per reckoning", and it is still reload-stable:
					// the same day and the same pair always draw the same answer.
					if (!Drawn(System, WeddingStreamId, WeddingEventKind, WeddingDrawIndex,
						unchecked((ulong)(((long)first << 20) ^ second ^ (KingdomAmbientRules.DayOrdinal(nowTick) << 40))),
						KingdomHappeningRules.WeddingChancePercent))
					{
						continue;
					}
					found++;
					state = Marry(System, state, label, here, nowTick, a, b, first, second, ref pushed, pushBudget);
				}
			}
			return state;
		}

		private static KingdomCityState Marry(KingdomSystem System, KingdomCityState state, string label, bool here, long nowTick, KingdomResidentRow a, KingdomResidentRow b, int first, int second, ref int pushed, int pushBudget)
		{
			string one = Named(a.Name);
			string other = Named(b.Name);
			KingdomChronicle.Record(System, KingdomHappeningRules.WeddingTelling(one, other, KingdomWord.CityName(System, label)));
			// Lower id first, always: the ring is what answers "have we already said this", and a
			// pair stored one way round and asked the other way round would be a second wedding.
			state = Tell(state, new KingdomHappening(KingdomHappeningKind.Wedding, nowTick, first, second, a.BoundZoneId, 0));
			if (pushed < pushBudget)
			{
				KingdomWord.Ambient(System, label, here, KingdomVoices.Say(System, VoiceOccasion.Wedding,
					KingdomHappeningRules.WeddingNotice(one, other)));
				pushed++;
			}
			KingdomLog.Log("happening: wedding " + one + " + " + other + " at " + label);
			return state;
		}

		// ==================================================================================
		// Breakdowns — announce once, and unsay when it turns again
		// ==================================================================================

		private static KingdomCityState Breakdowns(KingdomSystem System, KingdomCityState state, string label, bool here, long nowTick, ref int pushed, int pushBudget)
		{
			int found = 0;
			for (int i = 0; i < state.WorkCount && found < MaxBreakdownsPerReckon; i++)
			{
				KingdomWorkRow row;
				if (!state.TryWork(i, out row) || row.WorkId <= 0)
				{
					continue;
				}
				KingdomHappening happening = KingdomHappeningRules.Judge(row, BelievedBroken(state, row.WorkId), nowTick);
				if (!happening.Stands)
				{
					continue;
				}
				found++;
				bool broken = !KingdomHappeningRules.IsMending(happening.Outcome);
				string name = KingdomUpgrade.DisplayNameOf(row.DesignKey);
				state = Tell(state, happening);
				if (broken)
				{
					KingdomChronicle.Record(System, KingdomHappeningRules.BreakdownTelling(name, KingdomWord.CityName(System, label), row.ConditionPercent));
					if (pushed < pushBudget)
					{
						KingdomWord.Ambient(System, label, here, KingdomHappeningRules.BreakdownNotice(name, row.ConditionPercent));
						pushed++;
					}
				}
				else if (pushed < pushBudget)
				{
					// The unsaying, in the lane Addendum 10(a) built for it: a founder told from a
					// distance that their mill had stopped is owed the withdrawal from the same
					// distance. Not a chronicle entry - the book records what happened, and a
					// thing that stopped happening is news for the report.
					KingdomWord.Unsay(System, label, here, KingdomHappeningRules.MendedNotice(name, row.ConditionPercent));
					pushed++;
				}
				KingdomLog.Log("happening: " + (broken ? "breakdown " : "mended ") + name + " work=" + row.WorkId + " condition=" + row.ConditionPercent);
			}
			return state;
		}

		/// <summary>
		/// What the city last said about this work, read off the told-log ring rather than off a
		/// second ledger kept for the purpose.
		/// <para>
		/// The ring is bounded at thirty-two lines and overwrites its oldest, so a belief can be
		/// forgotten &mdash; and a forgotten belief reads as "not broken", which is the safe
		/// direction: the worst it can cost is one repeated announcement about a work that has
		/// been broken for thirty-two happenings, and never a mending that is never announced.
		/// </para>
		/// </summary>
		private static bool BelievedBroken(KingdomCityState state, int workId)
		{
			bool believed = false;
			for (int i = 0; i < state.ToldCount; i++)
			{
				KingdomToldRow row;
				if (!state.TryTold(i, out row) || row.Kind != KingdomToldKind.Breakdown || row.SubjectA != workId)
				{
					continue;
				}
				believed = !KingdomHappeningRules.IsMending(row.Outcome);
			}
			return believed;
		}

		// ==================================================================================
		// Funerals — the one telling a death gets, enriched rather than duplicated
		// ==================================================================================

		/// <summary>
		/// The rite clause the settlement's own death telling carries, and the told-log row that
		/// goes with it.
		/// <para>
		/// <b>Called from <c>KingdomOffices.RecordDeath</c> and from nowhere else</b>, which is
		/// what makes "one telling per death, never double" structural rather than guarded:
		/// <c>RecordDeath</c> is already the single place a settler leaves the living roll, it is
		/// already the single place the chronicle and the message queue hear about it, and this
		/// adds a clause to that line instead of a line beside it. There is no second code path
		/// that can speak about a death, because there is no second code path that knows about
		/// one.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Name">The settler who died, as the roll carried them.</param>
		/// <param name="Cause">The cause the memory machinery classified.</param>
		/// <param name="Z">Where they were, or null.</param>
		/// <returns>The clause to append to the death's own telling. Empty when happenings are
		/// off, which leaves the existing telling exactly as it was.</returns>
		public static string FuneralClause(KingdomSystem System, string Name, KingdomOfficeRules.DeathCause Cause, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || string.IsNullOrEmpty(Name))
			{
				return "";
			}
			// The office holder as the city knows them, epithet and all (lane 5): the names lane 5
			// mints are the names the happenings use, which is the whole point of minting them.
			string clause = KingdomHappeningRules.FuneralClause(
				KingdomOfficeRules.ChooseTitle(System.SeatName),
				KingdomNotables.HolderName(System));
			RecordFuneral(System, Name, Cause, Z);
			return clause;
		}

		/// <summary>
		/// The safety net, and the reason <c>KingdomHappeningRules.FuneralDue</c> exists: a row the
		/// model found dead that the memory machinery never heard about.
		/// <para>
		/// <c>r_KingdomCitizenLegacy</c> is attached on a settlement pass, so a settler killed
		/// before this mod ever tagged them dies without <c>RecordDeath</c> running &mdash; and the
		/// row still goes <c>Dead</c> when the roster is next read. Without this the city would
		/// lose somebody in silence, which STANDARDS 7b does not allow.
		/// </para>
		/// <para>
		/// <b>It cannot double-tell.</b> Two independent guards have to both fail before a second
		/// telling is possible: the told-log ring already carries a <c>Funeral</c> line for this
		/// resident (written by <see cref="RecordFuneral"/> in the same call that announced the
		/// death), and the dead roll already carries the name. The roll is the stronger of the two
		/// because it is unbounded where the ring is thirty-two lines, and it is
		/// <c>KingdomOffices</c>' own record rather than a copy of it.
		/// </para>
		/// </summary>
		private static KingdomCityState Funerals(KingdomSystem System, KingdomCityState state, string label, bool here, long nowTick, ref int pushed, int pushBudget)
		{
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row) || row.ResidentId <= 0 || !KingdomHappeningRules.FuneralDue(row))
				{
					continue;
				}
				if (KingdomHappeningRules.AlreadyTold(state, KingdomHappeningKind.Funeral, row.ResidentId, 0)
					|| (System.DeadNames != null && System.DeadNames.Contains(row.Name)))
				{
					continue;
				}
				int ordinal;
				KingdomOfficeRules.DeathCause cause = KingdomResidentRules.TryDeathCauseOrdinal(row.Cause, out ordinal)
					? (KingdomOfficeRules.DeathCause)ordinal
					: KingdomOfficeRules.DeathCause.Unknown;
				string name = Named(row.Name);
				string rite = KingdomHappeningRules.FuneralClause(
					KingdomOfficeRules.ChooseTitle(System.SeatName),
					KingdomNotables.HolderName(System));
				KingdomChronicle.Record(System, KingdomOfficeRules.MourningChronicle(name, "", KingdomWord.CityName(System, label), cause) + rite);
				state = Tell(state, new KingdomHappening(KingdomHappeningKind.Funeral, nowTick, row.ResidentId, 0, row.BoundZoneId, (int)cause));
				if (pushed < pushBudget)
				{
					KingdomWord.Ambient(System, label, here, KingdomVoices.Say(System, VoiceOccasion.CitizenLost,
						KingdomOfficeRules.MourningMessage(name, cause)));
					pushed++;
				}
				KingdomLog.Log("happening: funeral " + name + " cause=" + cause + " at " + label + " (the roll never heard of this death)");
				// One a pass, the same discipline KingdomOffices uses for cairns: a city that lost
				// several people off-screen tells them one visit at a time rather than all at once.
				return state;
			}
			return state;
		}

		private static void RecordFuneral(KingdomSystem System, string Name, KingdomOfficeRules.DeathCause Cause, Zone Z)
		{
			if (System.City == null || The.Game == null)
			{
				return;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!System.City.TryRead(out state, out fault))
			{
				return;
			}
			int residentId = ResidentIdOf(state, Name);
			state = Tell(state, new KingdomHappening(
				KingdomHappeningKind.Funeral,
				The.Game.TimeTicks,
				residentId,
				0,
				(Z == null) ? null : Z.ZoneID,
				(int)Cause));
			if (!System.City.TryPublish(state, out fault))
			{
				KingdomLog.Log("city: funeral refused (" + fault + "); the book is unchanged");
			}
		}

		// ==================================================================================
		// The homecoming report — what the ring adds up to
		// ==================================================================================

		/// <summary>
		/// One line a piece for what the founder missed, out of the told-log ring and nowhere
		/// else. Written into the ledger's ordinary note lane, under the brink lines, because a
		/// wedding is not an arrestable window.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="book">The city's book.</param>
		/// <param name="sinceTick">Told lines older than this are last visit's news.</param>
		public static void Digest(KingdomSystem System, KingdomCityBook book, long sinceTick)
		{
			if (!Enabled || System == null || book == null)
			{
				return;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!book.TryRead(out state, out fault))
			{
				return;
			}
			int weddings = 0;
			int funerals = 0;
			int festivals = 0;
			int breakdowns = 0;
			for (int i = 0; i < state.ToldCount; i++)
			{
				KingdomToldRow row;
				if (!state.TryTold(i, out row) || row.Tick < sinceTick)
				{
					continue;
				}
				switch (row.Kind)
				{
				case KingdomToldKind.Wedding:
					weddings++;
					break;
				case KingdomToldKind.Funeral:
					funerals++;
					break;
				case KingdomToldKind.Festival:
					festivals++;
					break;
				case KingdomToldKind.Breakdown:
					if (!KingdomHappeningRules.IsMending(row.Outcome))
					{
						breakdowns++;
					}
					break;
				}
			}
			// ONE note, not four. KingdomLedger.Note caps the ordinary lane at twelve lines and
			// drops the rest silently, and the happenings arrive last of everything on the pass -
			// four lines of ours could push four of the settlement's own arithmetic off the end of
			// the report. Joined into a sentence, they cost one.
			string joined = Join(KingdomToldKind.Festival, festivals, KingdomToldKind.Wedding, weddings,
				KingdomToldKind.Funeral, funerals, KingdomToldKind.Breakdown, breakdowns);
			if (!string.IsNullOrEmpty(joined))
			{
				System.Ledger.Note("{{K|" + joined + "}}");
			}
		}

		private static string Join(KingdomToldKind a, int countA, KingdomToldKind b, int countB, KingdomToldKind c, int countC, KingdomToldKind d, int countD)
		{
			System.Text.StringBuilder builder = new System.Text.StringBuilder();
			Append(builder, a, countA);
			Append(builder, b, countB);
			Append(builder, c, countC);
			Append(builder, d, countD);
			return builder.ToString();
		}

		private static void Append(System.Text.StringBuilder builder, KingdomToldKind kind, int count)
		{
			string line = KingdomHappeningRules.ToldLine(kind, count);
			if (string.IsNullOrEmpty(line))
			{
				return;
			}
			if (builder.Length > 0)
			{
				builder.Append(' ');
			}
			builder.Append(line);
		}

		// ==================================================================================
		// Shared plumbing
		// ==================================================================================

		/// <summary>
		/// Writes a brownout into the city's ring.
		/// <para>
		/// W7. The ANNOUNCE-ONCE latch is not here and must not be: it lives on the object that
		/// went quiet, so that recovery can unsay it (Addendum 12(c)) and the next failure can be
		/// told again. What the ring is for is the other half &mdash; the dated line a founder
		/// three zones away reads at the homecoming, and the digest reads afterwards. The ring
		/// forgets by age, which is right for history and wrong for a latch, and this is the
		/// history.
		/// </para>
		/// </summary>
		/// <param name="WorkId">The work that stopped.</param>
		/// <param name="Tier">The brownout ladder rung it stopped on, so the ring remembers how far
		/// down the city had to go and not only that the lights went out.</param>
		internal static void TellBrownout(KingdomSystem System, int WorkId, int Tier, string ZoneId, long TimeTicks)
		{
			if (!Enabled || System == null || !System.Founded || System.City == null || TimeTicks <= 0L)
			{
				return;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!System.City.TryRead(out state, out fault))
			{
				return;
			}
			KingdomCityState next;
			if (!state.TryTell(new KingdomToldRow(KingdomToldKind.Brownout, TimeTicks, WorkId, 0, ZoneId, Tier), out next, out fault))
			{
				KingdomLog.Log("city: brownout refused (" + fault + "); the ring is unchanged");
				return;
			}
			if (!System.City.TryPublish(next, out fault))
			{
				KingdomLog.Log("city: brownout refused (" + fault + "); the book is unchanged");
			}
		}

		private static KingdomCityState Tell(KingdomCityState state, KingdomHappening happening)
		{
			KingdomCityState next;
			KingdomCityFault fault;
			return state.TryTell(happening.ToldRow, out next, out fault) ? next : state;
		}

		/// <summary>
		/// One draw, keyed so a reload never re-rolls a happening the founder has already read
		/// about. LIVING-CITY-ARCHITECTURE &sect;2.4.
		/// </summary>
		private static bool Drawn(KingdomSystem System, string stream, uint kind, uint index, ulong ordinal, int chancePercent)
		{
			SemanticEventKey key;
			KernelFaultCode fault;
			ulong value;
			if (SemanticEventKey.TryCreate(HappeningRulesVersion, KingdomChronicle.SettlementId(System), stream, kind, ordinal, out key, out fault)
				&& CounterRandom.TryDrawBelow(HappeningSeed, key, index, 100uL, out value, out fault))
			{
				return (int)value < chancePercent;
			}
			// The kernel refused - no settlement name yet, or this machine's crypto provider is
			// failing. A happening that cannot be drawn reproducibly does not happen: silence is
			// the honest answer, and unlike flavour text a wedding is not something to fall back
			// to an unstable roll for.
			KingdomLog.Log("happening: draw refused (" + fault + ") on " + stream);
			return false;
		}

		private static int OnTheRoll(KingdomCityState state)
		{
			int count = 0;
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (state.TryResident(i, out row) && KingdomResidentRules.OnTheRoll(row))
				{
					count++;
				}
			}
			return count;
		}

		private static int ResidentIdOf(KingdomCityState state, string name)
		{
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (state.TryResident(i, out row) && string.Equals(row.Name, name, StringComparison.Ordinal))
				{
					return row.ResidentId;
				}
			}
			return 0;
		}

		private static string Named(string name)
		{
			return string.IsNullOrEmpty(name) ? "a settler" : name;
		}
	}
}
