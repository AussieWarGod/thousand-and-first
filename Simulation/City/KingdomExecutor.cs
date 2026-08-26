using System;
using System.Diagnostics;

namespace ThousandAndFirst.Simulation.City
{
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
