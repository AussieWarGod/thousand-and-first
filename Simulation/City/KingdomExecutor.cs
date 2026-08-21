using System;
using System.Diagnostics;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>How a submitted computation ended.</summary>
	internal enum KingdomComputeStatus : byte
	{
		/// <summary>Ran, stayed inside its budget, and published a value.</summary>
		Ok = 0,

		/// <summary>The seam would not run it. Nothing was invoked.</summary>
		Refused = 1,

		/// <summary>Ran and returned false, or threw. Nothing is published.</summary>
		Faulted = 2,

		/// <summary>Ran and exceeded its lane's budget. Nothing is published.</summary>
		OverBudget = 3
	}

	/// <summary>
	/// Why the seam refused or abandoned a computation. Distinct from
	/// <see cref="KingdomCityFault"/>, which is what a rule says about its own arithmetic: this is
	/// what the boundary says about the job.
	/// </summary>
	internal enum KingdomComputeRefusal : byte
	{
		None = 0,
		NullJob = 1,
		NullClock = 2,

		/// <summary>The job threw. It stalls itself, never the city and never the turn.</summary>
		Threw = 3,

		/// <summary>A type from a Qud assembly appears in the boundary's type closure.</summary>
		EngineTypeAtBoundary = 4,

		/// <summary>A boundary type carries a field that is not <c>readonly</c>.</summary>
		MutableField = 5,

		/// <summary>A boundary type carries a static that is not <c>readonly</c> or <c>const</c>.</summary>
		MutableStatic = 6,

		/// <summary>The closure is deeper or wider than the walker will follow, which is a refusal
		/// rather than a pass: an unwalkable boundary has not been shown to be clean.</summary>
		ClosureTooLarge = 7
	}

	/// <summary>
	/// One piece of model computation, in the only shape the seam accepts.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;2.5: immutable in, immutable out; no engine type crosses;
	/// budget and timeout belong to the seam, not the job; and <b>a job may not read the clock</b> —
	/// <c>nowTick</c> is an input, never an ambient read, which is also what makes a job replayable
	/// in a test.
	/// </para>
	/// <para>
	/// A structural interface, never a base class. Third-party computations implement the same one
	/// and inherit the same budget, timeout and isolation.
	/// </para>
	/// </summary>
	internal interface IKingdomComputation<TIn, TOut>
	{
		/// <summary>What this job is, for the receipt line. Never null.</summary>
		string Label { get; }

		/// <summary>Which row of the performance constitution this job answers to.</summary>
		KingdomBudgetLane Lane { get; }

		/// <summary>
		/// Runs over the frozen input and produces a new frozen value.
		/// <para>
		/// Total over representable input: returns false with a fault and publishes nothing rather
		/// than throwing. The seam catches a throw anyway, because a third-party job is not
		/// obliged to keep a promise the seam can enforce.
		/// </para>
		/// </summary>
		bool TryRun(TIn input, out TOut output, out KingdomComputeCounters counters, out KingdomCityFault fault);
	}

	/// <summary>What one submission produced, and the receipt for it.</summary>
	internal readonly struct KingdomComputeResult<TOut>
	{
		internal readonly KingdomComputeStatus Status;

		/// <summary>The new frozen value. Default unless <see cref="Status"/> is
		/// <see cref="KingdomComputeStatus.Ok"/> — nothing is published on a fault or over budget,
		/// so the caller's state stays byte-identical.</summary>
		internal readonly TOut Value;

		internal readonly KingdomCityFault Fault;

		internal readonly KingdomComputeRefusal Refusal;

		internal readonly KingdomPerfReceipt Receipt;

		internal KingdomComputeResult(
			KingdomComputeStatus status,
			TOut value,
			KingdomCityFault fault,
			KingdomComputeRefusal refusal,
			KingdomPerfReceipt receipt)
		{
			Status = status;
			Value = value;
			Fault = fault;
			Refusal = refusal;
			Receipt = receipt;
		}

		/// <summary>Whether the caller may publish. The one question a call site asks.</summary>
		internal bool Published
		{
			get { return Status == KingdomComputeStatus.Ok; }
		}
	}

	/// <summary>
	/// The seam's own clock, and the only one in the room. A job may not read a clock; the seam
	/// must, to time it. Injected rather than ambient so a test can drive a job over a budget edge
	/// without waiting for one.
	/// </summary>
	internal interface IKingdomComputeClock
	{
		long NowMicroseconds();
	}

	/// <summary>The live clock. Monotonic, and never the game's tick clock — an elapsed
	/// wall-measurement is not world time and must never be mistaken for it.</summary>
	internal sealed class KingdomStopwatchClock : IKingdomComputeClock
	{
		private static readonly double MicrosecondsPerTimestamp = 1000000.0 / Stopwatch.Frequency;

		public long NowMicroseconds()
		{
			return (long)(Stopwatch.GetTimestamp() * MicrosecondsPerTimestamp);
		}
	}

	/// <summary>Where receipts go. W1 binds one that writes <c>KingdomLog</c>'s <c>[TAF]</c> lines;
	/// W0 ships the ring, so the seam has somewhere to put a receipt from its first commit.</summary>
	internal interface IKingdomComputeJournal
	{
		void Record(KingdomPerfReceipt receipt);
	}

	/// <summary>
	/// A bounded ring of the most recent receipts, plus the session worst per lane — which is what
	/// LIVING-CITY-ARCHITECTURE &sect;6.5 appends to <c>kingdom:dump</c>.
	/// <para>
	/// A ring rather than a list, for the same reason the told-log is one: a season of receipts and
	/// a day of them must differ in what is remembered and never in what is held.
	/// </para>
	/// </summary>
	internal sealed class KingdomComputeJournalRing : IKingdomComputeJournal
	{
		internal const int Capacity = 32;

		private readonly KingdomPerfReceipt[] entries;

		private readonly KingdomPerfReceipt[] worst;

		private readonly bool[] worstSeen;

		private int count;

		private int cursor;

		internal KingdomComputeJournalRing()
		{
			entries = new KingdomPerfReceipt[Capacity];
			worst = new KingdomPerfReceipt[KingdomBudgetRules.LaneCount];
			worstSeen = new bool[KingdomBudgetRules.LaneCount];
			count = 0;
			cursor = 0;
		}

		internal int Count
		{
			get { return count; }
		}

		public void Record(KingdomPerfReceipt receipt)
		{
			entries[cursor] = receipt;
			cursor = (cursor + 1) % Capacity;
			if (count < Capacity)
			{
				count++;
			}
			int lane = (int)receipt.Lane;
			if (lane < 0 || lane >= worst.Length)
			{
				return;
			}
			if (!worstSeen[lane] || receipt.Microseconds > worst[lane].Microseconds)
			{
				worst[lane] = receipt;
				worstSeen[lane] = true;
			}
		}

		/// <summary>The ring, oldest first.</summary>
		internal bool TryGet(int ordinalFromOldest, out KingdomPerfReceipt receipt)
		{
			receipt = default(KingdomPerfReceipt);
			if (ordinalFromOldest < 0 || ordinalFromOldest >= count)
			{
				return false;
			}
			int oldest = (count < Capacity) ? 0 : cursor;
			receipt = entries[(oldest + ordinalFromOldest) % Capacity];
			return true;
		}

		internal bool TryWorst(KingdomBudgetLane lane, out KingdomPerfReceipt receipt)
		{
			receipt = default(KingdomPerfReceipt);
			int index = (int)lane;
			if (index < 0 || index >= worst.Length || !worstSeen[index])
			{
				return false;
			}
			receipt = worst[index];
			return true;
		}
	}

	/// <summary>
	/// The one choke point every piece of model computation goes through, and there is never a
	/// second path.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;2.5: <c>TryAdvance</c>, the micro-reckon slice, the route plan
	/// and the network solve all submit here. Synchronous today — it invokes the job inline and
	/// returns — and that earns its place immediately regardless of threading, because it is where
	/// the timers, the budget checks and the fault handling stop being copied to four call sites.
	/// </para>
	/// <para>
	/// The swap path is genuinely cheap: a threaded executor replaces this body with a queue and a
	/// worker. The input is already immutable, so there is nothing to lock; the result is already
	/// published in one assignment on the caller's side, so publication stays on the main thread
	/// where Qud's serialization expects it; and no call site changes.
	/// </para>
	/// <para>
	/// <b>What "abandoned" means while the executor is synchronous.</b> An inline invoker cannot
	/// pre-empt a running job, so a job that overruns its budget is not stopped mid-flight — it is
	/// <i>refused publication</i>. The caller's state is byte-identical either way, which is the
	/// property &sect;2.5 actually asks for; the threaded executor tightens the same guarantee into
	/// a real timeout without changing what a call site sees.
	/// </para>
	/// </summary>
	internal sealed class KingdomExecutor
	{
		private readonly IKingdomComputeClock clock;

		private readonly IKingdomComputeJournal journal;

		internal KingdomExecutor(IKingdomComputeClock clock, IKingdomComputeJournal journal)
		{
			this.clock = clock;
			this.journal = journal;
		}

		/// <summary>The live seam: a stopwatch and a ring.</summary>
		internal static KingdomExecutor CreateSynchronous()
		{
			return new KingdomExecutor(new KingdomStopwatchClock(), new KingdomComputeJournalRing());
		}

		/// <summary>
		/// Runs one computation over one frozen snapshot, times it, judges it against its lane, and
		/// publishes a value only if it both succeeded and stayed inside its budget.
		/// <para>
		/// A job that throws is caught here and nowhere else: a misbehaving job stalls itself,
		/// never the city and never the turn. That is a property no amount of documentation could
		/// give a direct call.
		/// </para>
		/// </summary>
		internal KingdomComputeResult<TOut> Submit<TIn, TOut>(TIn snapshot, IKingdomComputation<TIn, TOut> job)
		{
			if (job == null)
			{
				return Refuse<TOut>(KingdomComputeRefusal.NullJob, KingdomBudgetLane.Reckon, "null");
			}
			if (clock == null)
			{
				return Refuse<TOut>(KingdomComputeRefusal.NullClock, job.Lane, job.Label);
			}

			TOut output = default(TOut);
			KingdomComputeCounters counters = KingdomComputeCounters.None;
			KingdomCityFault fault = KingdomCityFault.None;
			KingdomComputeRefusal refusal = KingdomComputeRefusal.None;
			bool ran;
			long started = clock.NowMicroseconds();
			try
			{
				ran = job.TryRun(snapshot, out output, out counters, out fault);
			}
			catch (Exception)
			{
				ran = false;
				output = default(TOut);
				counters = KingdomComputeCounters.None;
				refusal = KingdomComputeRefusal.Threw;
			}
			long elapsed = clock.NowMicroseconds() - started;
			if (elapsed < 0L)
			{
				elapsed = 0L;
			}

			long primary = PrimaryCount(job.Lane, counters);
			KingdomPerfReceipt receipt = new KingdomPerfReceipt(
				job.Lane,
				job.Label,
				elapsed,
				counters,
				primary,
				KingdomBudgetRules.JudgeMicroseconds(job.Lane, elapsed),
				KingdomBudgetRules.JudgeCount(job.Lane, primary));
			if (journal != null)
			{
				journal.Record(receipt);
			}

			if (!ran)
			{
				return new KingdomComputeResult<TOut>(
					KingdomComputeStatus.Faulted,
					default(TOut),
					fault,
					refusal,
					receipt);
			}
			if (receipt.Verdict == KingdomBudgetVerdict.Over)
			{
				return new KingdomComputeResult<TOut>(
					KingdomComputeStatus.OverBudget,
					default(TOut),
					KingdomCityFault.None,
					KingdomComputeRefusal.None,
					receipt);
			}
			return new KingdomComputeResult<TOut>(
				KingdomComputeStatus.Ok,
				output,
				KingdomCityFault.None,
				KingdomComputeRefusal.None,
				receipt);
		}

		/// <summary>Which counter a lane is judged on. One primary count per lane, because a lane
		/// with two ceilings has none a reader can hold in their head.</summary>
		private static long PrimaryCount(KingdomBudgetLane lane, KingdomComputeCounters counters)
		{
			switch (lane)
			{
			case KingdomBudgetLane.Reckon:
				return counters.Draws;
			case KingdomBudgetLane.Heartbeat:
				return counters.BreakpointSteps;
			case KingdomBudgetLane.Reify:
				return counters.UnitThirds / KingdomCatchUpRules.ThirdsPerUnit;
			case KingdomBudgetLane.HeartbeatAmortised:
				return counters.RowVisits;
			case KingdomBudgetLane.ModelBytes:
			case KingdomBudgetLane.SaveBytes:
				return counters.Bytes;
			case KingdomBudgetLane.RoutePlan:
			case KingdomBudgetLane.NetworkSolve:
				return counters.RowVisits;
			default:
				return 0L;
			}
		}

		private static KingdomComputeResult<TOut> Refuse<TOut>(KingdomComputeRefusal refusal, KingdomBudgetLane lane, string label)
		{
			KingdomPerfReceipt receipt = new KingdomPerfReceipt(
				lane,
				label,
				0L,
				KingdomComputeCounters.None,
				0L,
				KingdomBudgetVerdict.Within,
				KingdomBudgetVerdict.Within);
			return new KingdomComputeResult<TOut>(
				KingdomComputeStatus.Refused,
				default(TOut),
				KingdomCityFault.None,
				refusal,
				receipt);
		}
	}
}
