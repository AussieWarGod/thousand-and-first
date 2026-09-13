#if TAF_TESTS
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	public class KingdomScenarioLoadResumeTests
	{
		[TestCase("success")]
		[TestCase("fault")]
		[TestCase("cancel")]
		public async Task PreparedContinuationWaitsForSuccessfulWorkerAndReturnsExactValue(string outcome)
		{
			var barrier = new KingdomScenarioLoadBarrier<object>();
			var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			var prepared = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			var value = new object();
			ClassicAssert.IsTrue(barrier.TryClaim());
			int executions = 0;
			_ = barrier.Start(async () => {
				Interlocked.Increment(ref executions);
				barrier.PrepareResume(value);
				prepared.SetResult(true);
				await finish.Task;
			});
			try
			{
				ClassicAssert.AreSame(prepared.Task, await Task.WhenAny(prepared.Task, Task.Delay(5000)));
				await prepared.Task;
				ClassicAssert.IsFalse(barrier.Pending.IsCompleted);
				Assert.Throws<InvalidOperationException>(() => barrier.PrepareResume(new object()));
				if (outcome == "success") finish.SetResult(true);
				else if (outcome == "fault") finish.SetException(new InvalidOperationException("worker failed after preparation"));
				else finish.SetCanceled();
				ClassicAssert.AreSame(barrier.Work, await Task.WhenAny(barrier.Work, Task.Delay(5000)));
				if (outcome == "success") ClassicAssert.AreSame(value, await barrier.Pending);
				else
				{
					try { await barrier.Work; } catch (Exception) { }
					ClassicAssert.IsFalse(barrier.Pending.IsCompleted);
				}
				ClassicAssert.AreSame(barrier.Pending, barrier.Start(() => { executions++; return Task.CompletedTask; }));
				ClassicAssert.AreEqual(1, executions);
				ClassicAssert.IsFalse(barrier.TryClaim());
			}
			finally { finish.TrySetResult(true); }
		}

		[Test]
		public async Task CannotResumeAnUnstartedOrAlreadyFinishedTerminalFixture()
		{
			var barrier = new KingdomScenarioLoadBarrier<object>();
			Assert.Throws<ArgumentNullException>(() => barrier.PrepareResume(null));
			Assert.Throws<InvalidOperationException>(() => barrier.PrepareResume(new object()));
			barrier.TryClaim();
			Assert.Throws<InvalidOperationException>(() => barrier.PrepareResume(new object()));
			_ = barrier.Start(() => Task.CompletedTask);
			ClassicAssert.AreSame(barrier.Work, await Task.WhenAny(barrier.Work, Task.Delay(5000)));
			await barrier.Work;
			Assert.Throws<InvalidOperationException>(() => barrier.PrepareResume(new object()));
			ClassicAssert.IsFalse(barrier.Pending.IsCompleted);
		}
	}
}
#endif
