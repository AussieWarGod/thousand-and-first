#if TAF_TESTS
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	public class KingdomScenarioLoadBarrierTests
	{
		private const int WaitMs = 5000;

		private static TaskCompletionSource<bool> Latch()
		{
			return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		}

		private static void Settle(Task task)
		{
			if (task == null) return;
			try { ClassicAssert.IsTrue(task.Wait(WaitMs), "test worker did not settle within bound"); }
			catch (AggregateException) { ClassicAssert.IsTrue(task.IsCompleted); }
		}

		[Test]
		public void NewBarrierExposesOnlyStablePendingTaskWithoutStartingWork()
		{
			var barrier = new KingdomScenarioLoadBarrier<object>();
			ClassicAssert.IsFalse(barrier.Claimed);
			ClassicAssert.IsNull(barrier.Work);
			ClassicAssert.AreSame(barrier.Pending, barrier.Pending);
			ClassicAssert.IsFalse(barrier.Pending.IsCompleted);
		}

		[TestCase(false, false)]
		[TestCase(false, true)]
		[TestCase(true, true)]
		public void InvalidStartRefusesWithoutSchedulingOrReleasing(bool claimed, bool nullWorker)
		{
			var barrier = new KingdomScenarioLoadBarrier<int>();
			if (claimed) ClassicAssert.IsTrue(barrier.TryClaim());
			int executions = 0;
			Func<Task> worker = nullWorker ? null : (Func<Task>)(() => {
				Interlocked.Increment(ref executions); return Task.CompletedTask;
			});
			if (nullWorker) Assert.Throws<ArgumentNullException>(() => barrier.Start(worker));
			else Assert.Throws<InvalidOperationException>(() => barrier.Start(worker));
			ClassicAssert.AreEqual(claimed, barrier.Claimed);
			ClassicAssert.AreEqual(0, executions);
			ClassicAssert.IsNull(barrier.Work);
			ClassicAssert.IsFalse(barrier.Pending.IsCompleted);
		}

		[TestCase(1)]
		[TestCase(2)]
		[TestCase(32)]
		public void ExactlyOneClaimDoesNotStartWork(int repeats)
		{
			var barrier = new KingdomScenarioLoadBarrier<int>();
			ClassicAssert.IsTrue(barrier.TryClaim());
			for (int i = 0; i < repeats; i++) ClassicAssert.IsFalse(barrier.TryClaim());
			ClassicAssert.IsTrue(barrier.Claimed);
			ClassicAssert.IsNull(barrier.Work);
			ClassicAssert.IsFalse(barrier.Pending.IsCompleted);
		}

		[Test]
		public void ConcurrentClaimsHaveExactlyOneWinner()
		{
			var barrier = new KingdomScenarioLoadBarrier<int>();
			var release = Latch();
			int winners = 0;
			Task[] callers = new Task[8];
			using (var ready = new CountdownEvent(callers.Length))
			{
				try
				{
					for (int i = 0; i < callers.Length; i++)
						callers[i] = Task.Run(async delegate {
							ready.Signal(); await release.Task.ConfigureAwait(false);
							if (barrier.TryClaim()) Interlocked.Increment(ref winners);
						});
					ClassicAssert.IsTrue(ready.Wait(WaitMs));
					release.SetResult(true);
					Settle(Task.WhenAll(callers));
					foreach (Task caller in callers) ClassicAssert.AreEqual(TaskStatus.RanToCompletion, caller.Status);
					ClassicAssert.AreEqual(1, winners);
					ClassicAssert.IsTrue(barrier.Claimed);
					ClassicAssert.IsNull(barrier.Work);
					ClassicAssert.IsFalse(barrier.Pending.IsCompleted);
				}
				finally { release.TrySetResult(true); Settle(Task.WhenAll(callers)); }
			}
		}

		[TestCase(2)]
		[TestCase(8)]
		public void ConcurrentStartsRetainOneWorkerAndOnePendingTask(int count)
		{
			var barrier = new KingdomScenarioLoadBarrier<int>();
			ClassicAssert.IsTrue(barrier.TryClaim());
			var begin = Latch(); var entered = Latch(); var finish = Latch();
			int executions = 0;
			Task[] callers = new Task[count];
			Task<int>[] returned = new Task<int>[count];
			using (var ready = new CountdownEvent(count))
			{
				try
				{
					for (int i = 0; i < count; i++)
					{
						int index = i;
						callers[i] = Task.Run(async delegate {
							ready.Signal(); await begin.Task.ConfigureAwait(false);
							returned[index] = barrier.Start(() => {
								Interlocked.Increment(ref executions); entered.TrySetResult(true);
								return finish.Task;
							});
						});
					}
					ClassicAssert.IsTrue(ready.Wait(WaitMs));
					begin.SetResult(true);
					Settle(Task.WhenAll(callers));
					ClassicAssert.IsTrue(entered.Task.Wait(WaitMs));
					foreach (Task caller in callers) ClassicAssert.AreEqual(TaskStatus.RanToCompletion, caller.Status);
					foreach (Task<int> pending in returned) ClassicAssert.AreSame(barrier.Pending, pending);
					ClassicAssert.AreEqual(1, executions);
					ClassicAssert.IsFalse(barrier.Work.IsCompleted);
					ClassicAssert.IsFalse(barrier.Pending.IsCompleted);
					Task observed = barrier.Work;
					finish.SetResult(true); Settle(observed);
					ClassicAssert.AreEqual(TaskStatus.RanToCompletion, observed.Status);
					ClassicAssert.AreSame(barrier.Pending, barrier.Start(() => { executions++; return Task.CompletedTask; }));
					ClassicAssert.AreSame(observed, barrier.Work);
					ClassicAssert.AreEqual(1, executions);
					ClassicAssert.IsFalse(barrier.Pending.IsCompleted);
				}
				finally
				{
					begin.TrySetResult(true); finish.TrySetResult(true);
					Settle(Task.WhenAll(callers)); Settle(barrier.Work);
				}
			}
		}

		[Test]
		public void SynchronouslyPausedWorkerDoesNotBlockStartCallerOrReleasePending()
		{
			var barrier = new KingdomScenarioLoadBarrier<int>();
			ClassicAssert.IsTrue(barrier.TryClaim());
			using (var entered = new ManualResetEventSlim(false))
			using (var release = new ManualResetEventSlim(false))
			{
				Task<int> returned = null;
				Task caller = Task.Run(delegate {
					returned = barrier.Start(delegate {
						entered.Set();
						if (!release.Wait(WaitMs)) throw new TimeoutException("test latch expired");
						return Task.CompletedTask;
					});
				});
				try
				{
					ClassicAssert.IsTrue(entered.Wait(WaitMs));
					Settle(caller);
					ClassicAssert.AreEqual(TaskStatus.RanToCompletion, caller.Status);
					ClassicAssert.AreSame(barrier.Pending, returned);
					ClassicAssert.IsFalse(barrier.Work.IsCompleted);
					ClassicAssert.IsFalse(barrier.Pending.IsCompleted);
				}
				finally { release.Set(); Settle(caller); Settle(barrier.Work); }
				ClassicAssert.AreEqual(TaskStatus.RanToCompletion, barrier.Work.Status);
				ClassicAssert.IsFalse(barrier.Pending.IsCompleted);
			}
		}

		[TestCase("success")]
		[TestCase("synchronous-throw")]
		[TestCase("returned-fault")]
		[TestCase("null-task")]
		[TestCase("cancelled")]
		public void EveryWorkerOutcomeStaysObservableWithoutReleasingDispatch(string outcome)
		{
			var barrier = new KingdomScenarioLoadBarrier<string>();
			ClassicAssert.IsTrue(barrier.TryClaim());
			var failure = new InvalidOperationException("owned test failure");
			int executions = 0;
			Task<string> pending = barrier.Start(delegate {
				Interlocked.Increment(ref executions);
				switch (outcome)
				{
					case "success": return Task.CompletedTask;
					case "synchronous-throw": throw failure;
					case "returned-fault": return Task.FromException(failure);
					case "null-task": return null;
					case "cancelled": return Task.FromCanceled(new CancellationToken(true));
					default: throw new ArgumentException(outcome);
				}
			});
			Task observed = barrier.Work;
			Settle(observed);
			if (outcome == "success") ClassicAssert.AreEqual(TaskStatus.RanToCompletion, observed.Status);
			else if (outcome == "cancelled") ClassicAssert.IsTrue(observed.IsCanceled);
			else
			{
				ClassicAssert.IsTrue(observed.IsFaulted);
				ClassicAssert.IsInstanceOf<InvalidOperationException>(observed.Exception.InnerException);
				if (outcome != "null-task") ClassicAssert.AreSame(failure, observed.Exception.InnerException);
			}
			ClassicAssert.AreSame(pending, barrier.Pending);
			ClassicAssert.IsFalse(pending.IsCompleted);
			ClassicAssert.AreSame(pending, barrier.Start(() => { executions++; return Task.CompletedTask; }));
			ClassicAssert.AreSame(observed, barrier.Work);
			ClassicAssert.AreEqual(1, executions);
			ClassicAssert.IsFalse(barrier.TryClaim());
			ClassicAssert.IsFalse(pending.IsCompleted);
		}
	}
}
#endif
