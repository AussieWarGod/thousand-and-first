using System;

namespace ThousandAndFirst.Simulation.City
{
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
	internal static partial class KingdomHappeningRules
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

	}
}
