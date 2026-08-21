using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What kind of thing happened. LIVING-CITY-ARCHITECTURE &sect;7.4 W4.
	/// <para>
	/// Four kinds and no fifth, because these are the four things the model can already SEE
	/// without anybody inventing a new dimension for them: two rows sharing a roof, a row that
	/// went <c>Dead</c>, a clock that came due on Qud's own calendar, and a work row that stopped.
	/// A happening is therefore always a <b>rendering of model state</b> (BUILDING-CATALOGUE-BRIEF
	/// Addendum 13, THE MESH CONDITION) and never a generator with a table of its own.
	/// </para>
	/// </summary>
	internal enum KingdomHappeningKind : byte
	{
		None = 0,

		/// <summary>Two resident rows who already share a roof, and whose creeds do not hold it
		/// against each other.</summary>
		Wedding = 1,

		/// <summary>A resident row that went <c>Dead</c>. Told exactly once, by the memory
		/// machinery that already tells it &mdash; see <see cref="FuneralClause"/>.</summary>
		Funeral = 2,

		/// <summary>Qud's own calendar came round. Never an invented holiday.</summary>
		Festival = 3,

		/// <summary>A work row stopped running, or fell under the condemned line.</summary>
		Breakdown = 4
	}

	/// <summary>
	/// Which day of Qud's own calendar a feast is anchored to.
	/// <para>
	/// <b>Both of these are vanilla's, and there are only two, because vanilla only has two.</b>
	/// A survey of <c>D/XRL/World/Calendar.cs</c> found no holiday machinery at all: no
	/// <c>Holiday</c> type, no <c>HolyDay</c>, no date-pinned event, and not one place in the whole
	/// engine that branches on <c>GetMonth()</c> or <c>GetDay()</c>. What vanilla does have is a
	/// thirteen-month year with one intercalary month and one named day a month, and those are the
	/// two anchors this enum carries. Addendum 13 lane 4 asks for <i>"festivals and rites anchored
	/// to vanilla months and holy days, never invented holidays"</i>; this is the whole of what
	/// there is to anchor to.
	/// </para>
	/// </summary>
	internal enum KingdomFestivalAnchor : byte
	{
		None = 0,

		/// <summary>
		/// The Ides. The one day of the month Qud declines to number: <c>Calendar.GetDay</c>
		/// returns the literal string <c>"Ides"</c> for the fifteenth
		/// (<c>D/XRL/World/Calendar.cs:223</c>) and an ordinal for every other day. Twelve a year,
		/// one per numbered month.
		/// </summary>
		Ides = 1,

		/// <summary>
		/// The festival of Ut yara Ux. Qud's one canonical named festival, and the only one:
		/// <c>D/Qud/API/JournalAPI.cs:467</c> ("Since the first festival of Ut yara Ux, the
		/// villagers of Joppa have feasted on warm apple matz"),
		/// <c>D/XRL/World/Parts/GenerateFriendOrFoe.cs:54</c> ("ruining the festival of Ut yara
		/// Ux"), and <c>B/Books.xml:499, 1323, 1368, 1631</c>. It shares its name with the
		/// five-day intercalary month it falls in (<c>D/XRL/World/Calendar.cs:87-89</c>), which is
		/// what makes it datable at all.
		/// </summary>
		UtYaraUx = 2
	}

	/// <summary>
	/// One happening, in the shape the told-log ring stores it. LIVING-CITY-ARCHITECTURE
	/// &sect;1.2(f).
	/// <para>
	/// Six fields and not one more, because the ring is thirty-two bytes a line and the prose is
	/// derived rather than stored &mdash; the same discipline that makes a district a code and not
	/// a sentence.
	/// </para>
	/// </summary>
	internal readonly struct KingdomHappening
	{
		internal readonly KingdomHappeningKind Kind;

		internal readonly long Tick;

		/// <summary>The resident id, or the work id, this happening is about. Zero for a
		/// happening the whole city is the subject of.</summary>
		internal readonly int SubjectA;

		/// <summary>The second party of a wedding; zero everywhere else.</summary>
		internal readonly int SubjectB;

		internal readonly string PlaceZoneId;

		/// <summary>The kind's own small integer: a festival's anchor, a breakdown's condition, a
		/// funeral's death cause ordinal.</summary>
		internal readonly int Outcome;

		internal KingdomHappening(KingdomHappeningKind kind, long tick, int subjectA, int subjectB, string placeZoneId, int outcome)
		{
			Kind = kind;
			Tick = tick;
			SubjectA = subjectA;
			SubjectB = subjectB;
			PlaceZoneId = placeZoneId;
			Outcome = outcome;
		}

		/// <summary>Nothing happened. What every eligibility check that failed returns.</summary>
		internal static KingdomHappening None
		{
			get { return new KingdomHappening(KingdomHappeningKind.None, 0L, 0, 0, null, 0); }
		}

		internal bool Stands
		{
			get { return Kind != KingdomHappeningKind.None; }
		}

		/// <summary>The told-log line this happening is. One ring, one vocabulary: the happening
		/// layer stores nothing of its own.</summary>
		internal KingdomToldRow ToldRow
		{
			get { return new KingdomToldRow(KingdomHappeningRules.ToldKindOf(Kind), Tick, SubjectA, SubjectB, PlaceZoneId, Outcome); }
		}
	}

	/// <summary>
	/// The happenings layer, as arithmetic and prose. Pure, engine-free, total.
	/// <para>
	/// <b>The mesh condition is the whole design of this file</b> (BUILDING-CATALOGUE-BRIEF
	/// Addendum 13): every predicate here reads rows the model already keeps for other reasons,
	/// and every string here composes through vocabulary that already exists &mdash;
	/// <c>KingdomOfficeRules.CauseClause</c> for a death, the lodging machinery's own refusal
	/// floor for whether two people can stand each other, <c>Calendar</c>'s own months for when a
	/// feast is. Nothing in this file is a table of happenings; there is no such table.
	/// </para>
	/// <para>
	/// <b>Nothing here draws and nothing here loops over days.</b> The eligibility predicates are
	/// verdicts about rows and the calendar arithmetic is closed-form, so a reckoning of one day
	/// and a reckoning of ninety perform the same work &mdash; LIVING-CITY-ARCHITECTURE &sect;0.0(a),
	/// <i>"draws are per happening, never per day"</i>. The draws themselves belong to the engine
	/// edge, which owns the kernel keys.
	/// </para>
	/// </summary>
	internal static class KingdomHappeningRules
	{
		// ==================================================================================
		// Qud's calendar, mirrored with its citations (D/XRL/World/Calendar.cs)
		// ==================================================================================

		/// <summary><c>Calendar.TurnsPerYear</c> (<c>D/XRL/World/Calendar.cs:11</c>). Mirrored
		/// rather than referenced because this file is engine-free by construction; the engine
		/// edge asserts the two agree.</summary>
		internal const long TicksPerYear = 438000L;

		/// <summary><c>Calendar.TurnsPerDay</c> (<c>D/XRL/World/Calendar.cs:13</c>).</summary>
		internal const long TicksPerDay = 1200L;

		/// <summary>Thirty days, which is what every numbered month of Qud's year is: the
		/// <c>GetMonth</c> chain steps in 36,000s (<c>D/XRL/World/Calendar.cs:57-112</c>).</summary>
		internal const long TicksPerMonth = 36000L;

		/// <summary>Numbered months in a Qud year: six <c>Ut</c> and six <c>Ux</c>. The
		/// thirteenth, <c>Ut yara Ux</c>, is intercalary and is not one of them &mdash;
		/// <c>Calendar.GetDay</c> gives it its own five-day branch and then subtracts its length
		/// back out of the rest of the year (<c>D/XRL/World/Calendar.cs:135-163</c>).</summary>
		internal const int NumberedMonths = 12;

		/// <summary>First tick of Ut yara Ux, from <c>Calendar.GetDay</c>'s own test
		/// <c>TimeOfYear &gt; 216000</c> (<c>D/XRL/World/Calendar.cs:136</c>).</summary>
		internal const long IntercalaryFirstTick = 216001L;

		/// <summary>Last tick of Ut yara Ux, from <c>TimeOfYear &lt; 222001</c>
		/// (<c>D/XRL/World/Calendar.cs:136</c>). Five days.</summary>
		internal const long IntercalaryLastTick = 222000L;

		/// <summary>Ut yara Ux's length, which <c>Calendar.GetDay</c> subtracts from every later
		/// tick of the year (<c>D/XRL/World/Calendar.cs:160-163</c>).</summary>
		internal const long IntercalaryTicks = 6000L;

		/// <summary>First tick of the Ides within a month, from <c>Calendar.GetDay</c>'s
		/// <c>num &lt; 18000</c> branch reached past <c>num &lt; 16800</c>
		/// (<c>D/XRL/World/Calendar.cs:219-223</c>).</summary>
		internal const long IdesFromTick = 16800L;

		/// <summary>One past the last tick of the Ides (<c>D/XRL/World/Calendar.cs:223</c>).</summary>
		internal const long IdesToTick = 18000L;

		/// <summary>
		/// Where in the year a tick falls, in Qud's own arithmetic: <c>Time % 438000</c>, the
		/// <c>TimeOfYear</c> every <c>Calendar</c> overload is written against
		/// (<c>D/XRL/World/Calendar.cs:54, 131, 295</c>). Negative input folds forward rather than
		/// answering with a negative year-tick, because there is no such position in a year.
		/// </summary>
		internal static long YearTick(long tick)
		{
			long year = tick % TicksPerYear;
			return (year < 0L) ? (year + TicksPerYear) : year;
		}

		/// <summary>
		/// The first tick of the Ides of one numbered month, as a position in the year.
		/// <para>
		/// The six months after the intercalary sit six thousand ticks later than their month
		/// index alone would put them, because <c>Calendar.GetDay</c> shifts them back by exactly
		/// that before taking its modulus (<c>D/XRL/World/Calendar.cs:160-163</c>). Getting this
		/// wrong would put the city's feast on a day the status bar calls the 10th.
		/// </para>
		/// </summary>
		/// <param name="monthOrdinal">0 for Nivvun Ut through 11 for Uru Ux. Out of range answers
		/// -1.</param>
		internal static long IdesTickOfMonth(int monthOrdinal)
		{
			if (monthOrdinal < 0 || monthOrdinal >= NumberedMonths)
			{
				return -1L;
			}
			long plain = ((long)monthOrdinal * TicksPerMonth) + IdesFromTick;
			return (monthOrdinal < 6) ? plain : (plain + IntercalaryTicks);
		}

		/// <summary>
		/// Which anchor, if any, this position in the year stands on. The engine's own tests, in
		/// the engine's own order.
		/// </summary>
		internal static KingdomFestivalAnchor AnchorAt(long yearTick)
		{
			if (yearTick >= IntercalaryFirstTick && yearTick <= IntercalaryLastTick)
			{
				return KingdomFestivalAnchor.UtYaraUx;
			}
			long shifted = (yearTick > IntercalaryLastTick) ? (yearTick - IntercalaryTicks) : yearTick;
			long inMonth = shifted % TicksPerMonth;
			return (inMonth >= IdesFromTick && inMonth < IdesToTick) ? KingdomFestivalAnchor.Ides : KingdomFestivalAnchor.None;
		}

		/// <summary>
		/// The next feast strictly after <paramref name="fromTick"/>, closed-form.
		/// <para>
		/// Thirteen candidate offsets a year &mdash; twelve Ides and one Ut yara Ux &mdash; so this
		/// is O(13) whatever the span, and a city left alone for a season costs exactly what a
		/// city left alone for a day costs. That is not a nicety: LIVING-CITY-ARCHITECTURE
		/// &sect;0.0(a) bans any term containing the elapsed, and a loop over days would be one.
		/// </para>
		/// <para>
		/// Vanilla starts every game on a uniformly random day of the year
		/// (<c>D/XRL/XRLGame.cs:1536</c>: <c>TimeOffset = Stat.Random(0, 365) * 1200 + 325</c>), so
		/// there is no founding day to count from and this arithmetic is the only honest way to
		/// know when the next feast is.
		/// </para>
		/// </summary>
		/// <param name="fromTick">The tick already accounted for. A feast exactly on it is behind
		/// us.</param>
		/// <param name="dueTick">The absolute tick the feast falls on.</param>
		/// <param name="anchor">Which feast it is.</param>
		internal static bool TryNextFestival(long fromTick, out long dueTick, out KingdomFestivalAnchor anchor)
		{
			dueTick = 0L;
			anchor = KingdomFestivalAnchor.None;
			if (fromTick < 0L)
			{
				return false;
			}
			long yearStart = fromTick - YearTick(fromTick);
			long here = YearTick(fromTick);
			long bestOffset = long.MaxValue;
			Consider(IntercalaryFirstTick, here, ref bestOffset);
			for (int month = 0; month < NumberedMonths; month++)
			{
				Consider(IdesTickOfMonth(month), here, ref bestOffset);
			}
			if (bestOffset != long.MaxValue)
			{
				dueTick = yearStart + bestOffset;
				// AnchorAt is the ONE definition of which feast a day is. The search finds the
				// day; it never labels it, so the forward walk, the backward jump and the test
				// that checks both against the engine's own windows cannot drift apart.
				anchor = AnchorAt(bestOffset);
				return true;
			}
			// Past the last Ides of the year: the next feast is the first one of the next year,
			// which is Nivvun Ut's Ides. Wrapping is arithmetic, never a second search.
			dueTick = yearStart + TicksPerYear + IdesTickOfMonth(0);
			anchor = KingdomFestivalAnchor.Ides;
			return true;
		}

		/// <summary>
		/// The most recent feast at or before <paramref name="atTick"/>, closed-form.
		/// <para>
		/// The other half of <see cref="TryNextFestival"/>, and it exists for the one case that
		/// would otherwise reintroduce a per-day term: a founder who has been gone for years. The
		/// caller walks forward a bounded number of feasts and then JUMPS here, so an absence of a
		/// season and an absence of a decade cost the same O(13).
		/// </para>
		/// </summary>
		internal static bool TryLastFestival(long atTick, out long dueTick, out KingdomFestivalAnchor anchor)
		{
			dueTick = 0L;
			anchor = KingdomFestivalAnchor.None;
			if (atTick < 0L)
			{
				return false;
			}
			long yearStart = atTick - YearTick(atTick);
			long here = YearTick(atTick);
			long bestOffset = -1L;
			ConsiderBack(IntercalaryFirstTick, here, ref bestOffset);
			for (int month = 0; month < NumberedMonths; month++)
			{
				ConsiderBack(IdesTickOfMonth(month), here, ref bestOffset);
			}
			if (bestOffset >= 0L)
			{
				dueTick = yearStart + bestOffset;
				anchor = AnchorAt(bestOffset);
				return true;
			}
			// Before this year's first Ides: the last feast was the last year's last Ides.
			dueTick = yearStart - TicksPerYear + IdesTickOfMonth(NumberedMonths - 1);
			anchor = KingdomFestivalAnchor.Ides;
			return dueTick >= 0L;
		}

		private static void ConsiderBack(long offset, long here, ref long bestOffset)
		{
			if (offset <= here && offset > bestOffset)
			{
				bestOffset = offset;
			}
		}

		private static void Consider(long offset, long here, ref long bestOffset)
		{
			if (offset > here && offset < bestOffset)
			{
				bestOffset = offset;
			}
		}

		// ==================================================================================
		// Weddings — cohabitation rows plus creed compatibility
		// ==================================================================================

		/// <summary>
		/// How long two people must have shared a roof before the city expects a wedding of them.
		/// <para>
		/// <c>KingdomBrinkRules.CreedBrinkWindowDays</c>, borrowed rather than invented: it is
		/// already the number of world-days this mod treats as long enough for two people's
		/// feelings about each other to be settled rather than fresh, and a second number meaning
		/// the same thing is a number that will drift.
		/// </para>
		/// </summary>
		internal const int CourtshipDays = KingdomBrinkRules.CreedBrinkWindowDays;

		/// <summary>
		/// The chance, per eligible pair per reckoning, that this is the reckoning they marry on.
		/// <para>
		/// One draw per PAIR (&sect;0.0(a): per happening, never per day), and low enough that a
		/// city does not marry itself off in a fortnight. A pair that does not marry this pass is
		/// eligible again next pass; nothing is remembered and nothing accumulates.
		/// </para>
		/// </summary>
		internal const int WeddingChancePercent = 20;

		/// <summary>
		/// The hostility at which two people will not stand each other under one roof, borrowed
		/// from the lodging vocabulary that already decides it
		/// (<c>KingdomLodgingRules.CreedRefusalHostilityFloor</c>). A pair the housing machinery
		/// would not have put together in the first place is not a pair the city marries.
		/// </summary>
		internal const int WeddingHostilityCeiling = KingdomLodgingRules.CreedRefusalHostilityFloor;

		/// <summary>
		/// What two settlers whose creeds the model cannot compare are worth.
		/// <para>
		/// <b>A row carries a creed CODE, and the code is one-way</b>
		/// (<c>KingdomCityRules.StableId</c> is FNV-1a and there is no inverse), so the model can
		/// prove two people hold with the same thing and can never prove two different things get
		/// on. Rather than invent a number for the pair, the model declines: this value is one
		/// past the ceiling, so an unprovable pair is simply not married. <b>The city does not
		/// marry on an assumption</b> &mdash; and it costs nothing, because a mixed household the
		/// lodging machinery DID put together still weds the moment they hold with the same thing
		/// or one of them holds with nothing.
		/// </para>
		/// </summary>
		internal const int UnknownCreedHostility = WeddingHostilityCeiling + 1;

		/// <summary>
		/// What the pair's creed codes are worth to a wedding.
		/// <para>
		/// Same code is one creed and no quarrel. A zero code is a settler who holds with nothing
		/// in particular (<c>StableId</c> answers zero for an empty string, which is what a
		/// settler with no <c>KingdomCreed</c> property has), and a person who holds with nothing
		/// has nothing to hold against anybody. Everything else is
		/// <see cref="UnknownCreedHostility"/>.
		/// </para>
		/// </summary>
		internal static int CreedHostility(int creedCodeA, int creedCodeB)
		{
			if (creedCodeA == creedCodeB || creedCodeA == 0 || creedCodeB == 0)
			{
				return 0;
			}
			return UnknownCreedHostility;
		}

		/// <summary>
		/// Whether these two rows are a wedding waiting for a draw.
		/// <para>
		/// <b>Every clause is a row the model already keeps.</b> They are both on the roll and
		/// standing here; they are two different people; they share a home work id, which is the
		/// model's own record that the lodging machinery already judged them able to live together
		/// (Addendum 4c's closeness ladder did the compatibility work, and re-deciding it here
		/// would be the parallel machinery Addendum 13 forbids); they have both been here long
		/// enough; and their creeds do not hold it against them.
		/// </para>
		/// </summary>
		/// <param name="a">One resident row.</param>
		/// <param name="b">The other.</param>
		/// <param name="creedHostility">From <c>KingdomCreed.HostilityBetween</c> &mdash; the
		/// engine's own faction feelings, never a grudge table of ours.</param>
		/// <param name="nowTick">The tick being reckoned to.</param>
		internal static bool WeddingEligible(KingdomResidentRow a, KingdomResidentRow b, int creedHostility, long nowTick)
		{
			if (a.ResidentId == b.ResidentId || a.ResidentId <= 0 || b.ResidentId <= 0)
			{
				return false;
			}
			if (a.Standing != KingdomResidentStanding.Resident || b.Standing != KingdomResidentStanding.Resident)
			{
				return false;
			}
			if (a.HomeWorkId <= 0 || a.HomeWorkId != b.HomeWorkId)
			{
				return false;
			}
			if (creedHostility > WeddingHostilityCeiling)
			{
				return false;
			}
			long settled = (long)CourtshipDays * TicksPerDay;
			return (nowTick - a.ArrivedTick) >= settled && (nowTick - b.ArrivedTick) >= settled;
		}

		/// <summary>
		/// The order two people's ids go into the ring in: lower first, always.
		/// <para>
		/// The ring is what answers <i>"have we already said this"</i>
		/// (<see cref="AlreadyTold"/>), and a pair stored one way round and asked the other way
		/// round is a second wedding for the same two people. Row order is not stable &mdash; the
		/// roster is rebuilt from the ground every pass &mdash; so the pair has to be ordered by
		/// something that is, and an id is.
		/// </para>
		/// </summary>
		internal static void PairOrder(int idA, int idB, out int first, out int second)
		{
			bool ascending = idA < idB;
			first = ascending ? idA : idB;
			second = ascending ? idB : idA;
		}

		// ==================================================================================
		// Funerals — one telling, and it is the one the city already gives
		// ==================================================================================

		/// <summary>
		/// Whether this row's person is owed a funeral: dead, with a cause the memory machinery
		/// can name.
		/// <para>
		/// <b>The one-telling rule is structural, not a guard.</b> A death is announced exactly
		/// once, by <c>KingdomOffices.RecordDeath</c>, at the one moment the engine reports the
		/// body died &mdash; and W4 does not add a second announcement beside it. What W4 adds is
		/// the RITE inside that same telling: the clause below is folded into the line
		/// <c>RecordDeath</c> was already going to print, and the told-log row is written in the
		/// same call. There is no code path that can speak about a death twice, because there is
		/// only one path that speaks about a death at all.
		/// </para>
		/// </summary>
		internal static bool FuneralDue(KingdomResidentRow row)
		{
			int ordinal;
			return row.Standing == KingdomResidentStanding.Dead
				&& KingdomResidentRules.TryDeathCauseOrdinal(row.Cause, out ordinal);
		}

		// ==================================================================================
		// Breakdowns — a stopped work is a small drama with a name on it
		// ==================================================================================

		/// <summary>
		/// The condition at or under which a work has stopped being one.
		/// <para>
		/// The lodging vocabulary's own line, read from the other side: a home is condemned at
		/// <c>KingdomLodgingRules.CondemnedWearPercent</c> of WEAR, and condition is what is left
		/// after wear, so the same building is condemned by the housing machinery and broken by
		/// the happenings layer at exactly the same number. Both are
		/// <c>KingdomRules.RuinStandingCeilingPercent</c>, and there is only the one constant.
		/// </para>
		/// </summary>
		internal const int BreakdownConditionFloor = 100 - KingdomLodgingRules.CondemnedWearPercent;

		/// <summary>
		/// What this work row is worth saying, given what the city last said about it.
		/// <list type="bullet">
		/// <item><description>A work the city believed was fine and is not any more is a
		/// breakdown, and the city says so once.</description></item>
		/// <item><description>A work the city believed was broken and that is running again is the
		/// UNSAYING &mdash; <c>KingdomWord.Unsay</c>'s own lane, for the founder who came home and
		/// mended it (Addendum 10(a): a warning withdrawn is owed from the same distance it was
		/// given).</description></item>
		/// <item><description>Anything the city already believes correctly is silence.</description></item>
		/// </list>
		/// <para>
		/// <b>The belief is the told-log ring's, not a snapshot's</b>, which is what makes this a
		/// rendering rather than a diff engine: there is no "before" state kept anywhere, only
		/// what the city has already told the founder, and that is already stored.
		/// </para>
		/// </summary>
		/// <returns>The happening, or <see cref="KingdomHappening.None"/>. Outcome carries the
		/// work's condition, and a NEGATIVE outcome marks the unsaying.</returns>
		internal static KingdomHappening Judge(KingdomWorkRow row, bool believedBroken, long nowTick)
		{
			if (row.WorkId <= 0)
			{
				return KingdomHappening.None;
			}
			bool broken = Broken(row);
			if (broken == believedBroken)
			{
				return KingdomHappening.None;
			}
			return new KingdomHappening(
				KingdomHappeningKind.Breakdown,
				nowTick,
				row.WorkId,
				0,
				row.ZoneId,
				broken ? row.ConditionPercent : (-1 - row.ConditionPercent));
		}

		/// <summary>
		/// Whether the row reads as a work that has stopped being one: worn past the condemned
		/// line, or standing with nobody on it when its kind cannot run without hands.
		/// <para>
		/// Both clauses are rows the model already keeps, which is the whole of why this is a
		/// rendering rather than a second wear system. <b>The crew clause is gated on kind on
		/// purpose:</b> a larder with nobody standing in it is a larder, and a growing ground
		/// grows whether or not anyone is watching it, so calling either of them broken would
		/// announce a drama that is not happening.
		/// </para>
		/// </summary>
		internal static bool Broken(KingdomWorkRow row)
		{
			return row.ConditionPercent <= BreakdownConditionFloor
				|| (NeedsHands(row.RunState.Kind) && row.CrewAssigned <= 0);
		}

		/// <summary>
		/// Whether a work of this kind stops when nobody is on it. A producer, a refiner and a
		/// power work are all a pair of hands away from silence; a store and a growing ground are
		/// not, and <c>Other</c> is not claimed either way because the model does not know what it
		/// is.
		/// </summary>
		internal static bool NeedsHands(KingdomWorkKind kind)
		{
			return kind == KingdomWorkKind.Producer || kind == KingdomWorkKind.Refiner || kind == KingdomWorkKind.Power;
		}

		/// <summary>Whether an outcome written by <see cref="JudgeWork"/> is the unsaying rather
		/// than the breakdown. The sign is the whole encoding, so the ring stays thirty-two
		/// bytes.</summary>
		internal static bool IsMending(int outcome)
		{
			return outcome < 0;
		}

		/// <summary>The condition an outcome written by <see cref="JudgeWork"/> carries, whichever
		/// side of the line it was written on.</summary>
		internal static int ConditionOf(int outcome)
		{
			return IsMending(outcome) ? (-1 - outcome) : outcome;
		}

		// ==================================================================================
		// The told-log vocabulary
		// ==================================================================================

		/// <summary>The ring's kind for a happening kind. One vocabulary, mapped rather than
		/// duplicated.</summary>
		internal static KingdomToldKind ToldKindOf(KingdomHappeningKind kind)
		{
			switch (kind)
			{
			case KingdomHappeningKind.Wedding:
				return KingdomToldKind.Wedding;
			case KingdomHappeningKind.Funeral:
				return KingdomToldKind.Funeral;
			case KingdomHappeningKind.Festival:
				return KingdomToldKind.Festival;
			case KingdomHappeningKind.Breakdown:
				return KingdomToldKind.Breakdown;
			default:
				return KingdomToldKind.None;
			}
		}

		/// <summary>The inverse, for a ring read back off a save.</summary>
		internal static KingdomHappeningKind KindOf(KingdomToldKind told)
		{
			switch (told)
			{
			case KingdomToldKind.Wedding:
				return KingdomHappeningKind.Wedding;
			case KingdomToldKind.Funeral:
				return KingdomHappeningKind.Funeral;
			case KingdomToldKind.Festival:
				return KingdomHappeningKind.Festival;
			case KingdomToldKind.Breakdown:
				return KingdomHappeningKind.Breakdown;
			default:
				return KingdomHappeningKind.None;
			}
		}

		/// <summary>
		/// Whether a happening of this kind about these subjects is already in the ring &mdash; the
		/// announce-once check (STANDARDS 7b), asked of the ring rather than of a second ledger.
		/// </summary>
		/// <param name="state">The city's book.</param>
		/// <param name="kind">What is about to be told.</param>
		/// <param name="subjectA">Its first subject.</param>
		/// <param name="subjectB">Its second, or zero.</param>
		internal static bool AlreadyTold(KingdomCityState state, KingdomHappeningKind kind, int subjectA, int subjectB)
		{
			if (state == null || kind == KingdomHappeningKind.None)
			{
				return false;
			}
			KingdomToldKind wanted = ToldKindOf(kind);
			for (int i = 0; i < state.ToldCount; i++)
			{
				KingdomToldRow row;
				if (state.TryTold(i, out row) && row.Kind == wanted && row.SubjectA == subjectA && row.SubjectB == subjectB)
				{
					return true;
				}
			}
			return false;
		}

		// ==================================================================================
		// The prose
		// ==================================================================================

		/// <summary>Qud's own name for the day a feast is anchored to, as
		/// <c>Calendar.GetMonth</c> and <c>Calendar.GetDay</c> spell them.</summary>
		internal static string AnchorName(KingdomFestivalAnchor anchor)
		{
			switch (anchor)
			{
			case KingdomFestivalAnchor.UtYaraUx:
				return "the festival of Ut yara Ux";
			case KingdomFestivalAnchor.Ides:
				return "the Ides";
			default:
				return "";
			}
		}

		/// <summary>
		/// The chronicle's telling of a feast: what day it was, and what the settlement put on the
		/// table. The dish is the realm's own &mdash; <c>Faction.WaterRitualRecipeText</c> as
		/// <c>KingdomDish</c> stamped it &mdash; so the feast serves what the creed already eats
		/// rather than a menu invented for the occasion.
		/// </summary>
		internal static string FestivalTelling(KingdomFestivalAnchor anchor, string settlementName, string dishName, int mouths)
		{
			string day = AnchorName(anchor);
			string what = string.IsNullOrEmpty(dishName) ? "what the larders held" : dishName;
			string who = (mouths > 0)
				? (mouths == 1 ? "one of them ate" : (mouths.ToString() + " of them ate"))
				: "the tables were bare";
			return settlementName + " kept " + day + ", and " + who + " " + what;
		}

		/// <summary>The line the founder is handed when the feast happens somewhere they are
		/// not.</summary>
		internal static string FestivalNotice(KingdomFestivalAnchor anchor, string settlementName, string dishName)
		{
			string what = string.IsNullOrEmpty(dishName) ? "what the larders held" : dishName;
			return settlementName + " kept " + AnchorName(anchor) + ", and set out " + what + ".";
		}

		/// <summary>
		/// The chronicle's telling of a wedding. Named for the roof rather than for a ceremony
		/// this mod does not simulate: what the model knows is that these two share a home, and
		/// the prose says exactly that much and no more.
		/// </summary>
		internal static string WeddingTelling(string oneName, string otherName, string settlementName)
		{
			return oneName + " and " + otherName + " were married under the roof they already shared, and " + settlementName + " drank to it";
		}

		/// <summary>The wedding as the founder hears it, wherever they are standing.</summary>
		internal static string WeddingNotice(string oneName, string otherName)
		{
			return oneName + " and " + otherName + " were married, and the water was shared.";
		}

		/// <summary>
		/// The rite clause the city's one telling of a death carries: where they were laid, and
		/// who spoke.
		/// <para>
		/// <b>A clause, not a sentence, and that is the point.</b> It is appended to the line
		/// <c>KingdomOfficeRules.MourningChronicle</c> already composes, so the death is told once
		/// and the funeral is part of that telling rather than a second one following it.
		/// </para>
		/// </summary>
		/// <param name="officeTitle">The settlement's office, from
		/// <c>KingdomOfficeRules.ChooseTitle</c>, or empty when nobody holds it.</param>
		/// <param name="officeHolder">Who holds it, or empty.</param>
		internal static string FuneralClause(string officeTitle, string officeHolder)
		{
			if (string.IsNullOrEmpty(officeHolder) || string.IsNullOrEmpty(officeTitle))
			{
				// A settlement of one, or one that has just lost the only person who could have
				// spoken. Said plainly rather than dressed up: nobody spoke.
				return ", and there was no one left to speak the water over them";
			}
			return ", and " + officeHolder + ", " + officeTitle + ", spoke the water over them";
		}

		/// <summary>
		/// The chronicle's telling of a work that stopped, named rather than logged: which work,
		/// where, and what condition it was in when it went.
		/// </summary>
		internal static string BreakdownTelling(string workName, string settlementName, int conditionPercent)
		{
			return "the " + Named(workName) + " at " + settlementName + " went still at " + conditionPercent + " parts in a hundred, and the hands stood about";
		}

		/// <summary>The breakdown as the founder hears it. One line, once, and it names the thing
		/// that stopped (STANDARDS 7b).</summary>
		internal static string BreakdownNotice(string workName, int conditionPercent)
		{
			return "The " + Named(workName) + " has stopped. " + conditionPercent + " parts in a hundred are left of it.";
		}

		/// <summary>
		/// The unsaying: the work turns again. <c>KingdomWord.Unsay</c>'s own lane, because a
		/// founder told from a distance that their mill had stopped is owed the withdrawal from
		/// the same distance.
		/// </summary>
		internal static string MendedNotice(string workName, int conditionPercent)
		{
			return "The " + Named(workName) + " turns again, at " + conditionPercent + " parts in a hundred.";
		}

		/// <summary>
		/// The homecoming report's one line for a happening the founder was not there for. The
		/// told-log ring is what the report reads, so this is the only place a stored line becomes
		/// prose.
		/// </summary>
		internal static string ToldLine(KingdomToldKind kind, int count)
		{
			if (count <= 0)
			{
				return "";
			}
			bool one = count == 1;
			switch (kind)
			{
			case KingdomToldKind.Wedding:
				return one ? "There was a wedding." : (count + " couples were married.");
			case KingdomToldKind.Funeral:
				return one ? "One of yours was buried." : (count + " of yours were buried.");
			case KingdomToldKind.Festival:
				return one ? "A feast was kept." : (count + " feasts were kept.");
			case KingdomToldKind.Breakdown:
				return one ? "Something stopped working." : (count + " works stopped.");
			default:
				return "";
			}
		}

		/// <summary>A work's display name, or the plainest honest noun for one nobody named.</summary>
		private static string Named(string workName)
		{
			return string.IsNullOrEmpty(workName) ? "works" : workName;
		}
	}
}
