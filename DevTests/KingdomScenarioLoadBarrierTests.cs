#if TAF_TESTS
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
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
			try { Assert.IsTrue(task.Wait(WaitMs), "test worker did not settle within bound"); }
			catch (AggregateException) { Assert.IsTrue(task.IsCompleted); }
		}

		[Test]
		public void NewBarrierExposesOnlyStablePendingTaskWithoutStartingWork()
		{
			var barrier = new KingdomScenarioLoadBarrier<object>();
			Assert.IsFalse(barrier.Claimed);
			Assert.IsNull(barrier.Work);
			Assert.AreSame(barrier.Pending, barrier.Pending);
			Assert.IsFalse(barrier.Pending.IsCompleted);
		}

		[TestCase(false, false)]
		[TestCase(false, true)]
		[TestCase(true, true)]
		public void InvalidStartRefusesWithoutSchedulingOrReleasing(bool claimed, bool nullWorker)
		{
			var barrier = new KingdomScenarioLoadBarrier<int>();
			if (claimed) Assert.IsTrue(barrier.TryClaim());
			int executions = 0;
			Func<Task> worker = nullWorker ? null : (Func<Task>)(() => {
				Interlocked.Increment(ref executions); return Task.CompletedTask;
			});
			if (nullWorker) Assert.Throws<ArgumentNullException>(() => barrier.Start(worker));
			else Assert.Throws<InvalidOperationException>(() => barrier.Start(worker));
			Assert.AreEqual(claimed, barrier.Claimed);
			Assert.AreEqual(0, executions);
			Assert.IsNull(barrier.Work);
			Assert.IsFalse(barrier.Pending.IsCompleted);
		}

		[TestCase(1)]
		[TestCase(2)]
		[TestCase(32)]
		public void ExactlyOneClaimDoesNotStartWork(int repeats)
		{
			var barrier = new KingdomScenarioLoadBarrier<int>();
			Assert.IsTrue(barrier.TryClaim());
			for (int i = 0; i < repeats; i++) Assert.IsFalse(barrier.TryClaim());
			Assert.IsTrue(barrier.Claimed);
			Assert.IsNull(barrier.Work);
			Assert.IsFalse(barrier.Pending.IsCompleted);
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
					Assert.IsTrue(ready.Wait(WaitMs));
					release.SetResult(true);
					Settle(Task.WhenAll(callers));
					foreach (Task caller in callers) Assert.AreEqual(TaskStatus.RanToCompletion, caller.Status);
					Assert.AreEqual(1, winners);
					Assert.IsTrue(barrier.Claimed);
					Assert.IsNull(barrier.Work);
					Assert.IsFalse(barrier.Pending.IsCompleted);
				}
				finally { release.TrySetResult(true); Settle(Task.WhenAll(callers)); }
			}
		}

		[TestCase(2)]
		[TestCase(8)]
		public void ConcurrentStartsRetainOneWorkerAndOnePendingTask(int count)
		{
			var barrier = new KingdomScenarioLoadBarrier<int>();
			Assert.IsTrue(barrier.TryClaim());
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
					Assert.IsTrue(ready.Wait(WaitMs));
					begin.SetResult(true);
					Settle(Task.WhenAll(callers));
					Assert.IsTrue(entered.Task.Wait(WaitMs));
					foreach (Task caller in callers) Assert.AreEqual(TaskStatus.RanToCompletion, caller.Status);
					foreach (Task<int> pending in returned) Assert.AreSame(barrier.Pending, pending);
					Assert.AreEqual(1, executions);
					Assert.IsFalse(barrier.Work.IsCompleted);
					Assert.IsFalse(barrier.Pending.IsCompleted);
					Task observed = barrier.Work;
					finish.SetResult(true); Settle(observed);
					Assert.AreEqual(TaskStatus.RanToCompletion, observed.Status);
					Assert.AreSame(barrier.Pending, barrier.Start(() => { executions++; return Task.CompletedTask; }));
					Assert.AreSame(observed, barrier.Work);
					Assert.AreEqual(1, executions);
					Assert.IsFalse(barrier.Pending.IsCompleted);
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
			Assert.IsTrue(barrier.TryClaim());
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
					Assert.IsTrue(entered.Wait(WaitMs));
					Settle(caller);
					Assert.AreEqual(TaskStatus.RanToCompletion, caller.Status);
					Assert.AreSame(barrier.Pending, returned);
					Assert.IsFalse(barrier.Work.IsCompleted);
					Assert.IsFalse(barrier.Pending.IsCompleted);
				}
				finally { release.Set(); Settle(caller); Settle(barrier.Work); }
				Assert.AreEqual(TaskStatus.RanToCompletion, barrier.Work.Status);
				Assert.IsFalse(barrier.Pending.IsCompleted);
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
			Assert.IsTrue(barrier.TryClaim());
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
			if (outcome == "success") Assert.AreEqual(TaskStatus.RanToCompletion, observed.Status);
			else if (outcome == "cancelled") Assert.IsTrue(observed.IsCanceled);
			else
			{
				Assert.IsTrue(observed.IsFaulted);
				Assert.IsInstanceOf<InvalidOperationException>(observed.Exception.InnerException);
				if (outcome != "null-task") Assert.AreSame(failure, observed.Exception.InnerException);
			}
			Assert.AreSame(pending, barrier.Pending);
			Assert.IsFalse(pending.IsCompleted);
			Assert.AreSame(pending, barrier.Start(() => { executions++; return Task.CompletedTask; }));
			Assert.AreSame(observed, barrier.Work);
			Assert.AreEqual(1, executions);
			Assert.IsFalse(barrier.TryClaim());
			Assert.IsFalse(pending.IsCompleted);
		}
	}
}
#endif
