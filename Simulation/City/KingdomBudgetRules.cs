using System;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// A row of the performance constitution. One lane, one budget, one place the numbers live.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;0.0 is the table; this enum is its index. Nothing in the city
	/// may time or count itself against a figure that is not a row here, because a budget quoted at
	/// a call site is a budget nobody can find when it moves.
	/// </para>
	/// </summary>
	internal enum KingdomBudgetLane : byte
	{
		/// <summary>One city, one pass. LIVING-CITY-ARCHITECTURE &sect;2.1.</summary>
		Reckon = 0,

		/// <summary>One turn's amortised spend while a debt stands. LIVING-CITY-ARCHITECTURE &sect;3.5.</summary>
		Reify = 1,

		/// <summary>One micro-reckon slice. LIVING-CITY-ARCHITECTURE &sect;3.6.</summary>
		Heartbeat = 2,

		/// <summary>The heartbeat's per-turn amortisation. LIVING-CITY-ARCHITECTURE &sect;3.6.</summary>
		HeartbeatAmortised = 3,

		/// <summary>Turns to drain the worst backlog. LIVING-CITY-ARCHITECTURE &sect;3.5.</summary>
		CatchUpDrain = 4,

		/// <summary>Model plus registry plus itineraries plus matrix, in RAM. LIVING-CITY-ARCHITECTURE &sect;0.0(c).</summary>
		ModelBytes = 5,

		/// <summary>The same, serialized on the write path. LIVING-CITY-ARCHITECTURE &sect;6.5.</summary>
		SaveBytes = 6,

		/// <summary>One route plan, per slice. LIVING-CITY-ARCHITECTURE &sect;3.10.</summary>
		RoutePlan = 7,

		/// <summary>One network flow solve, per city, per reckon. LIVING-CITY-ARCHITECTURE &sect;3.11.</summary>
		NetworkSolve = 8,

		/// <summary>Zones held resident beyond the seated one. LIVING-CITY-ARCHITECTURE &sect;6.4.</summary>
		ResidentZones = 9,

		/// <summary>One prefetch thaw. Timed, never budgeted. LIVING-CITY-ARCHITECTURE &sect;6.5.</summary>
		Thaw = 10
	}

	/// <summary>Where a measurement sits against its lane's budget.</summary>
	internal enum KingdomBudgetVerdict : byte
	{
		Within = 0,
		Warn = 1,
		Over = 2
	}

	/// <summary>
	/// One lane's budget. A threshold of <see cref="KingdomBudgetRules.NoLimit"/> is a rung that
	/// never fires — the constitution gives several lanes a warn without a numeric fail, and a
	/// sentinel says so instead of a zero that would fail everything.
	/// </summary>
	internal readonly struct KingdomBudgetRow
	{
		internal readonly KingdomBudgetLane Lane;

		/// <summary>The name this lane writes in the log, per LIVING-CITY-ARCHITECTURE &sect;6.5.</summary>
		internal readonly string LogName;

		internal readonly long WarnMicroseconds;

		internal readonly long FailMicroseconds;

		/// <summary>What the lane's primary count counts, for the log line and the BUDGET line.</summary>
		internal readonly string CountName;

		internal readonly long WarnCount;

		internal readonly long FailCount;

		internal KingdomBudgetRow(
			KingdomBudgetLane lane,
			string logName,
			long warnMicroseconds,
			long failMicroseconds,
			string countName,
			long warnCount,
			long failCount)
		{
			Lane = lane;
			LogName = logName;
			WarnMicroseconds = warnMicroseconds;
			FailMicroseconds = failMicroseconds;
			CountName = countName;
			WarnCount = warnCount;
			FailCount = failCount;
		}
	}

	/// <summary>
	/// What one submitted computation actually did. Counts, not only milliseconds: a timing is
	/// hardware and a count is a contract (LIVING-CITY-ARCHITECTURE &sect;6.5), which is what lets a
	/// tester on a slow machine still prove that a ninety-day reckoning did the same row-visits as
	/// a one-day one.
	/// </summary>
	internal readonly struct KingdomComputeCounters
	{
		internal readonly int BreakpointSteps;

		internal readonly long RowVisits;

		internal readonly int Draws;

		/// <summary>Reify units spent, weighted in thirds (LIVING-CITY-ARCHITECTURE &sect;0.0(b)).</summary>
		internal readonly int UnitThirds;

		internal readonly long Bytes;

		internal KingdomComputeCounters(int breakpointSteps, long rowVisits, int draws, int unitThirds, long bytes)
		{
			BreakpointSteps = breakpointSteps;
			RowVisits = rowVisits;
			Draws = draws;
			UnitThirds = unitThirds;
			Bytes = bytes;
		}

		internal static KingdomComputeCounters None
		{
			get { return new KingdomComputeCounters(0, 0L, 0, 0, 0L); }
		}
	}

	/// <summary>
	/// One timing receipt: what was measured, against which row of the constitution, and how it
	/// judged. Immutable, engine-free, and carrying no reference the caller can mutate afterwards.
	/// </summary>
	internal readonly struct KingdomPerfReceipt
	{
		internal readonly KingdomBudgetLane Lane;

		/// <summary>Which city, zone or job this measured. Never null once published.</summary>
		internal readonly string Label;

		internal readonly long Microseconds;

		internal readonly KingdomComputeCounters Counters;

		/// <summary>The lane's primary count for this measurement, in the lane's own unit.</summary>
		internal readonly long PrimaryCount;

		internal readonly KingdomBudgetVerdict TimeVerdict;

		internal readonly KingdomBudgetVerdict CountVerdict;

		internal KingdomPerfReceipt(
			KingdomBudgetLane lane,
			string label,
			long microseconds,
			KingdomComputeCounters counters,
			long primaryCount,
			KingdomBudgetVerdict timeVerdict,
			KingdomBudgetVerdict countVerdict)
		{
			Lane = lane;
			Label = label;
			Microseconds = microseconds;
			Counters = counters;
			PrimaryCount = primaryCount;
			TimeVerdict = timeVerdict;
			CountVerdict = countVerdict;
		}

		/// <summary>The worse of the two rungs. This is the figure a playtest log is read for.</summary>
		internal KingdomBudgetVerdict Verdict
		{
			get { return KingdomBudgetRules.Worse(TimeVerdict, CountVerdict); }
		}
	}

	/// <summary>
	/// The performance constitution as arithmetic. Pure, engine-free, total: every judge is defined
	/// over every representable input, and no threshold is written anywhere but here.
	/// <para>
	/// A verdict is reached by strict comparison — <c>Warn</c> above the warn rung, <c>Over</c>
	/// above the fail rung — so a budget of eight units passes at eight and fails at nine.
	/// </para>
	/// </summary>
	internal static class KingdomBudgetRules
	{
		/// <summary>A rung the constitution does not give this lane a number for.</summary>
		internal const long NoLimit = -1L;

		/// <summary>What <c>KingdomLog</c> stamps on every line it writes. Named here so the
		/// receipt's own shape and the log-watcher's grep have one source.</summary>
		internal const string LogPrefix = "[TAF] ";

		// ---- LIVING-CITY-ARCHITECTURE §0.0, the table, row by row. ----------------------------

		/// <summary>LIVING-CITY-ARCHITECTURE §0.0(a) / §2.3: breakpoints per reckoning, with an
		/// honest overflow to the fixed point rather than a silent truncation.</summary>
		internal const int MaxBreakpoints = 64;

		/// <summary>LIVING-CITY-ARCHITECTURE §0.0(a): draws are per happening, never per day.</summary>
		internal const int MaxDrawsPerCityPass = 512;

		/// <summary>LIVING-CITY-ARCHITECTURE §0.0(b): eight units a turn, visible cells first.</summary>
		internal const int ReifyUnitsPerTurn = 8;

		/// <summary>LIVING-CITY-ARCHITECTURE §0.0(b): of which at most four may be body mints.</summary>
		internal const int ReifyHeavyMintsPerTurn = 4;

		/// <summary>LIVING-CITY-ARCHITECTURE §0.0(b): light units are a third apiece, so twenty-four.</summary>
		internal const int ReifyLightUnitsPerTurn = 24;

		/// <summary>LIVING-CITY-ARCHITECTURE §3.6: one in-game hour, Calendar.TurnsPerHour.</summary>
		internal const int HeartbeatCadenceTicks = 50;

		/// <summary>LIVING-CITY-ARCHITECTURE §0.0 / §3.6: at most four breakpoint steps a slice.</summary>
		internal const int HeartbeatStepsPerSlice = 4;

		/// <summary>LIVING-CITY-ARCHITECTURE §3.6: at most one ambient line an in-game hour, city-wide.</summary>
		internal const int HeartbeatToldLinesPerSlice = 1;

		/// <summary>LIVING-CITY-ARCHITECTURE §0.0: the ceiling the model in RAM answers to.</summary>
		internal const long ModelBytesCeiling = 256L * 1024L;

		/// <summary>Advisory rung under the model ceiling. Repinned with the pre-release retirement
		/// of the flat forty-work proxy: the bound now prices every City-stage plot in four zones,
		/// and the current composed realm remains below this rung. Resident-row authority added two
		/// shared evidence references per row and moved the honest formula above 192 KiB, so the
		/// advisory rung moves to 208 KiB; the 256-KiB failure ceiling does not move.</summary>
		internal const long ModelBytesWarn = 208L * 1024L;

		/// <summary>LIVING-CITY-ARCHITECTURE §3.10: jobs considered by one planning slice.</summary>
		internal const int PlannerMaxJobs = 16;

		/// <summary>LIVING-CITY-ARCHITECTURE §3.10: stops in one trip.</summary>
		internal const int PlannerMaxStops = 8;

		/// <summary>LIVING-CITY-ARCHITECTURE §3.10: 2-opt swap tests, a hard iteration cap.</summary>
		internal const int PlannerMaxSwapTests = 50;

		/// <summary>LIVING-CITY-ARCHITECTURE §3.10: routing is arithmetic, not chance. Any draw in
		/// the planner is a failure, not a warning.</summary>
		internal const int PlannerMaxDraws = 0;

		/// <summary>LIVING-CITY-ARCHITECTURE §3.11: networks per city.</summary>
		internal const int NetworksPerCity = 4;

		/// <summary>LIVING-CITY-ARCHITECTURE §3.11: nodes per network.</summary>
		internal const int NetworkMaxNodes = 32;

		/// <summary>LIVING-CITY-ARCHITECTURE §3.11: edges per network.</summary>
		internal const int NetworkMaxEdges = 48;

		private static readonly KingdomBudgetRow[] Rows = new KingdomBudgetRow[11]
		{
			// LIVING-CITY-ARCHITECTURE §0.0: > 2 ms warns, > 8 ms per realm pass fails; draws
			// are capped per city pass because they are per happening, never per day.
			new KingdomBudgetRow(KingdomBudgetLane.Reckon, "reckon", 2000L, 8000L, "draws", NoLimit, MaxDrawsPerCityPass),
			// LIVING-CITY-ARCHITECTURE §0.0: > 1 ms warns, > 2 ms or over budget at all fails.
			new KingdomBudgetRow(KingdomBudgetLane.Reify, "reify", 1000L, 2000L, "units", NoLimit, ReifyUnitsPerTurn),
			// LIVING-CITY-ARCHITECTURE §0.0: > 0.3 ms warns, > 0.5 ms or > 4 steps fails.
			new KingdomBudgetRow(KingdomBudgetLane.Heartbeat, "slice", 300L, 500L, "steps", NoLimit, HeartbeatStepsPerSlice),
			// Full four-zone City envelope: 2R/50 is 38 row-visits per turn; > 40 warns, > 80 fails.
			new KingdomBudgetRow(KingdomBudgetLane.HeartbeatAmortised, "slicerate", NoLimit, NoLimit, "rows", 40L, 80L),
			// LIVING-CITY-ARCHITECTURE §0.0: ≤ 39 turns at 8/turn; > 40 warns; the fail is a
			// counter that never reaches zero, which is a shape rather than a number.
			new KingdomBudgetRow(KingdomBudgetLane.CatchUpDrain, "drain", NoLimit, NoLimit, "turns", 40L, NoLimit),
			// Full live City envelope across two settlements composes to about 193 KiB. Warn at
			// 208 KiB, fail at 256 KiB; a permanently-lit warning is noise, not evidence.
			new KingdomBudgetRow(KingdomBudgetLane.ModelBytes, "model", NoLimit, NoLimit, "bytes", ModelBytesWarn, ModelBytesCeiling),
			// Named-field save has larger per-row framing than the RAM table: > 256 KiB warns,
			// > 1 MiB fails at the public baseline.
			new KingdomBudgetRow(KingdomBudgetLane.SaveBytes, "bytes", NoLimit, NoLimit, "bytes", 256L * 1024L, 1024L * 1024L),
			// LIVING-CITY-ARCHITECTURE §0.0: ≲ 1,000 int ops; > 2,000 warns. The fail rung of this
			// lane is a draw, not an op count, and PlannerMaxDraws carries it.
			new KingdomBudgetRow(KingdomBudgetLane.RoutePlan, "plan", NoLimit, NoLimit, "ops", 2000L, NoLimit),
			// LIVING-CITY-ARCHITECTURE §0.0: ≤ 5,120 node-visits; > 8,000 warns, > 12,000 fails.
			new KingdomBudgetRow(KingdomBudgetLane.NetworkSolve, "network", NoLimit, NoLimit, "nodes", 8000L, 12000L),
			// LIVING-CITY-ARCHITECTURE §0.0 / §6.4: ≤ 1 beyond the seated zone; 2 warns, > 2 fails.
			new KingdomBudgetRow(KingdomBudgetLane.ResidentZones, "resident", NoLimit, NoLimit, "held", 1L, 2L),
			// LIVING-CITY-ARCHITECTURE §6.5: the thaw is timed so a prefetch can be seen, and
			// budgeted nowhere — the engine's own read cost is not ours to rule on.
			new KingdomBudgetRow(KingdomBudgetLane.Thaw, "thaw", NoLimit, NoLimit, "ms", NoLimit, NoLimit)
		};

		internal static int LaneCount
		{
			get { return Rows.Length; }
		}

		internal static bool TryRow(KingdomBudgetLane lane, out KingdomBudgetRow row)
		{
			int index = (int)lane;
			if (index < 0 || index >= Rows.Length)
			{
				row = default(KingdomBudgetRow);
				return false;
			}
			row = Rows[index];
			return row.Lane == lane;
		}

		/// <summary>
		/// The reckon lane's row-visit ceiling, computed from the LIVE row count rather than from
		/// the 14,848 the constitution quotes for today's caps.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;0.0(f): "The receipt checks the row-visit count against the
		/// live <c>R</c>, not against 14,848, so the assertion survives the cap moving." A propose
		/// pass and an apply pass visit every row, so one step is <c>2R</c>.
		/// </para>
		/// </summary>
		internal static bool TryMaxRowVisits(int rows, out long maxRowVisits)
		{
			maxRowVisits = 0L;
			if (rows < 0)
			{
				return false;
			}
			maxRowVisits = (long)MaxBreakpoints * 2L * (long)rows;
			return true;
		}

		internal static KingdomBudgetVerdict JudgeMicroseconds(KingdomBudgetLane lane, long microseconds)
		{
			KingdomBudgetRow row;
			if (!TryRow(lane, out row))
			{
				return KingdomBudgetVerdict.Within;
			}
			return Judge(microseconds, row.WarnMicroseconds, row.FailMicroseconds);
		}

		internal static KingdomBudgetVerdict JudgeCount(KingdomBudgetLane lane, long count)
		{
			KingdomBudgetRow row;
			if (!TryRow(lane, out row))
			{
				return KingdomBudgetVerdict.Within;
			}
			return Judge(count, row.WarnCount, row.FailCount);
		}

		internal static KingdomBudgetVerdict Judge(long measured, long warnLimit, long failLimit)
		{
			if (failLimit != NoLimit && measured > failLimit)
			{
				return KingdomBudgetVerdict.Over;
			}
			if (warnLimit != NoLimit && measured > warnLimit)
			{
				return KingdomBudgetVerdict.Warn;
			}
			return KingdomBudgetVerdict.Within;
		}

		internal static KingdomBudgetVerdict Worse(KingdomBudgetVerdict left, KingdomBudgetVerdict right)
		{
			return (left >= right) ? left : right;
		}

		/// <summary>
		/// The receipt as one greppable line, in the shape LIVING-CITY-ARCHITECTURE &sect;6.5 fixes
		/// for the log-watcher. A figure that crossed a budget is prefixed <c>BUDGET</c> and names
		/// the budget it broke, so a failure is legible without the tester holding the table in
		/// their head. Counters that are zero are omitted; the millisecond figure never is.
		/// </summary>
		internal static string FormatReceipt(KingdomPerfReceipt receipt)
		{
			return LogPrefix + FormatReceiptBody(receipt);
		}

		/// <summary>
		/// The same line without the <c>[TAF] </c> prefix, for the one writer that adds the prefix
		/// itself. <c>KingdomLog.Log</c> stamps every line it writes, so a journal that used
		/// <see cref="FormatReceipt"/> would emit the tag twice and the log-watcher's own grep
		/// would stop matching.
		/// </summary>
		internal static string FormatReceiptBody(KingdomPerfReceipt receipt)
		{
			KingdomBudgetRow row;
			bool known = TryRow(receipt.Lane, out row);
			string lane = known ? row.LogName : receipt.Lane.ToString();
			StringBuilder line = new StringBuilder(96);
			line.Append("perf ");
			if (receipt.Verdict == KingdomBudgetVerdict.Over)
			{
				line.Append("BUDGET ");
			}
			line.Append(lane);
			if (!string.IsNullOrEmpty(receipt.Label))
			{
				line.Append(" label=").Append(receipt.Label);
			}
			string countName = known ? row.CountName : null;
			bool primaryAlreadyNamed = false;
			primaryAlreadyNamed |= AppendCount(line, "steps", receipt.Counters.BreakpointSteps, countName);
			primaryAlreadyNamed |= AppendCount(line, "rows", receipt.Counters.RowVisits, countName);
			primaryAlreadyNamed |= AppendCount(line, "draws", receipt.Counters.Draws, countName);
			primaryAlreadyNamed |= AppendCount(line, "thirds", receipt.Counters.UnitThirds, countName);
			primaryAlreadyNamed |= AppendCount(line, "bytes", receipt.Counters.Bytes, countName);
			if (known && !primaryAlreadyNamed && receipt.PrimaryCount != 0L)
			{
				line.Append(' ').Append(countName).Append('=').Append(receipt.PrimaryCount.ToString(CultureInfo.InvariantCulture));
			}
			line.Append(" ms=").Append(FormatMilliseconds(receipt.Microseconds));
			if (!known)
			{
				return line.ToString();
			}
			if (receipt.TimeVerdict == KingdomBudgetVerdict.Over)
			{
				line.Append(" over=").Append(FormatMilliseconds(row.FailMicroseconds));
			}
			else if (receipt.CountVerdict == KingdomBudgetVerdict.Over)
			{
				line.Append(" over=").Append(row.FailCount.ToString(CultureInfo.InvariantCulture));
			}
			return line.ToString();
		}

		/// <summary>Microseconds as the millisecond figure the log prints: at most two decimals,
		/// invariant, so a receipt reads the same on every machine.</summary>
		internal static string FormatMilliseconds(long microseconds)
		{
			return (microseconds / 1000.0).ToString("0.##", CultureInfo.InvariantCulture);
		}

		/// <summary>Appends one counter if it is worth printing, and reports whether it was the
		/// lane's own primary count &mdash; so the primary is never printed twice.</summary>
		private static bool AppendCount(StringBuilder line, string name, long value, string primaryName)
		{
			if (value == 0L)
			{
				return false;
			}
			line.Append(' ').Append(name).Append('=').Append(value.ToString(CultureInfo.InvariantCulture));
			return string.Equals(name, primaryName, StringComparison.Ordinal);
		}
	}
}
