#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Real execution against the engine-free case runner, not a source pin: drives
	/// KingdomQuoteSitingOccupancyCaseRunner.Run with a throwing delegate directly and asserts
	/// the actual counters and Ok flag it produces. This is the value test for the review nit
	/// that the setup verb's own cases=/passed=/failed= line -- and its Ok out-parameter -- must
	/// stay truthful on a real per-case failure, not merely pinned as string literals.
	/// </summary>
	public class KingdomQuoteSitingOccupancyCaseRunnerTests
	{
		[Test]
		public void APassingCaseIncrementsPassedAndLeavesOkTrue()
		{
			var runner = new KingdomQuoteSitingOccupancyCaseRunner();
			runner.Run("case-a", () => { });
			Assert.That(runner.Passed, Is.EqualTo(1));
			Assert.That(runner.Failed, Is.EqualTo(0));
			Assert.That(runner.Ok, Is.True);
			Assert.That(runner.Evidence.ToString(), Is.Empty);
		}

		[Test]
		public void AThrowingCaseIsCountedFailedWithoutMovingPassedAndTurnsOkFalse()
		{
			var runner = new KingdomQuoteSitingOccupancyCaseRunner();
			runner.Run("case-a", () => { });
			runner.Run("case-b", () => { throw new System.InvalidOperationException("boom"); });

			Assert.That(runner.Failed, Is.EqualTo(1));
			Assert.That(runner.Passed, Is.EqualTo(1),
				"a failing case must not move Passed -- the counters must never be fabricated");
			Assert.That(runner.Ok, Is.False,
				"one real case failure must turn the verb-level Ok false (REFUSED)");
			string evidence = runner.Evidence.ToString();
			Assert.That(evidence, Does.Contain("case=case-b"));
			Assert.That(evidence, Does.Contain("outcome=FAILED"));
			Assert.That(evidence, Does.Contain("InvalidOperationException"));
			Assert.That(evidence, Does.Contain("boom"));
		}

		[Test]
		public void ALaterPassingCaseNeverErasesAnEarlierFailure()
		{
			var runner = new KingdomQuoteSitingOccupancyCaseRunner();
			runner.Run("case-a", () => { throw new System.Exception("first"); });
			runner.Run("case-b", () => { });
			runner.Run("case-c", () => { });

			Assert.That(runner.Passed, Is.EqualTo(2));
			Assert.That(runner.Failed, Is.EqualTo(1));
			Assert.That(runner.Ok, Is.False,
				"Ok must stay false once any case has failed, even after later cases pass");
			Assert.That(runner.Evidence.ToString(), Does.Contain("case=case-a"));
		}

		[Test]
		public void NoCasesRunLeavesZeroCountersAndOkTrue()
		{
			var runner = new KingdomQuoteSitingOccupancyCaseRunner();
			Assert.That(runner.Passed, Is.EqualTo(0));
			Assert.That(runner.Failed, Is.EqualTo(0));
			Assert.That(runner.Ok, Is.True);
		}
	}
}
#endif
