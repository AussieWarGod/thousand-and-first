#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Executes the production completion boundary, not Qud or departure accounting.</summary>
	[TestFixture]
	public sealed class KingdomSubsidenceCompletionRulesTests
	{
		[Test]
		public void SuccessfulCompletionCommitsThenAppliesReachedRungsThenPresents()
		{
			var trace = new List<string>();
			KingdomSubsidenceCompletionRules.Complete(() => trace.Add("bookkeeping"),
				() => trace.Add("reached-rungs"), () => trace.Add("summary"),
				_ => trace.Add("diagnostic"));
			CollectionAssert.AreEqual(new[] { "bookkeeping", "reached-rungs", "summary" }, trace);
		}

		[TestCase("format")]
		[TestCase("ledger")]
		[TestCase("chronicle")]
		public void SummaryInterruptionCannotPreventCommittedStateOrReachedRungEffects(string cut)
		{
			long checkpoint = 100;
			int stage = 4, wear = 0;
			var trace = new List<string>();
			Exception fault = new InvalidOperationException(cut);
			Exception observed = null;
			bool completedBeforeSummary = false;
			Assert.DoesNotThrow(() => KingdomSubsidenceCompletionRules.Complete(() =>
			{
				checkpoint = 500; stage = 3;
			}, () => wear += 7, () =>
			{
				completedBeforeSummary = checkpoint == 500 && stage == 3 && wear == 7;
				foreach (string sink in new[] { "format", "ledger", "chronicle" })
				{
					trace.Add(sink);
					if (sink == cut) throw fault;
				}
			}, error => observed = error));
			Assert.AreEqual(500L, checkpoint);
			Assert.AreEqual(3, stage);
			Assert.AreEqual(7, wear);
			Assert.AreSame(fault, observed);
			Assert.IsTrue(completedBeforeSummary);
			Assert.AreEqual(cut, trace[trace.Count - 1]);
			Assert.AreEqual(Array.IndexOf(new[] { "format", "ledger", "chronicle" }, cut) + 1,
				trace.Count, "a partially delivered summary must not be replayed by this boundary");
		}

		[TestCase(false)]
		[TestCase(true)]
		public void DiagnosticOrHostileMessageFailureCannotInterruptCompletion(bool hostileMessage)
		{
			int committed = 0, physical = 0, diagnostics = 0, messageReads = 0;
			bool completeAtMessageRead = false, completeAtDiagnostic = false, sameError = false;
			Exception error = hostileMessage ? (Exception)new HostileMessageException(() =>
			{
				messageReads++;
				completeAtMessageRead = committed == 1 && physical == 1;
			}) : new InvalidOperationException("summary failure");
			Assert.DoesNotThrow(() => KingdomSubsidenceCompletionRules.Complete(
				() => committed++, () => physical++, () => { throw error; }, failure =>
				{
					diagnostics++;
					sameError = ReferenceEquals(error, failure);
					completeAtDiagnostic = committed == 1 && physical == 1;
					string detail = failure.Message;
					throw new InvalidOperationException(detail);
				}));
			Assert.AreEqual(1, committed);
			Assert.AreEqual(1, physical);
			Assert.AreEqual(1, diagnostics);
			Assert.IsTrue(sameError && completeAtDiagnostic);
			Assert.AreEqual(hostileMessage, completeAtMessageRead);
			Assert.AreEqual(hostileMessage ? 1 : 0, messageReads);
		}

		[Test]
		public void AbsentDiagnosticDoesNotCauseRetryOrReadTheException()
		{
			int attempts = 0, messageReads = 0;
			Assert.DoesNotThrow(() => KingdomSubsidenceCompletionRules.Complete(() => { },
				() => { }, () =>
				{
					attempts++;
					throw new HostileMessageException(() => messageReads++);
				}, null));
			Assert.AreEqual(1, attempts);
			Assert.AreEqual(0, messageReads);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void RequiredWorkFailureIsNotLaunderedAsPresentationFailure(bool physicalFailure)
		{
			var trace = new List<string>();
			var fault = new InvalidOperationException("required work failed");
			Exception observed = Assert.Throws<InvalidOperationException>(() =>
				KingdomSubsidenceCompletionRules.Complete(() =>
				{
					trace.Add("bookkeeping");
					if (!physicalFailure) throw fault;
				}, () =>
				{
					trace.Add("reached-rungs");
					throw fault;
				}, () => trace.Add("summary"), _ => trace.Add("diagnostic")));
			Assert.AreSame(fault, observed);
			CollectionAssert.AreEqual(physicalFailure
				? new[] { "bookkeeping", "reached-rungs" } : new[] { "bookkeeping" }, trace);
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		public void MissingRequiredPortRefusesBeforeAnyCallback(int missing)
		{
			int calls = 0;
			Action count = () => calls++;
			Assert.Throws<ArgumentNullException>(() => KingdomSubsidenceCompletionRules.Complete(
				missing == 0 ? null : count, missing == 1 ? null : count,
				missing == 2 ? null : count, _ => calls++));
			Assert.AreEqual(0, calls);
		}

		[Test]
		public void SourceContractRuntimeUsesDurableStepBeforeBatchSummary()
		{
			string source = TestMain.ReadRepositoryText("Growth/KingdomSubsidence.Reckoning.cs");
			StringAssert.Contains("KingdomSubsidenceStepRuntime.TryDrive(", source);
			StringAssert.DoesNotContain("KingdomSubsidenceCompletionRules.Complete(", source);
			StringAssert.DoesNotContain("KingdomGrowth.Emigrate(", source);
			string step = TestMain.ReadRepositoryText("Growth/KingdomSubsidenceStepRuntime.Step.cs");
			int report = step.IndexOf("ResumeRungReport(frame, out refusal)", StringComparison.Ordinal);
			int checkpoint = step.IndexOf("system.LastSubsidenceTick = checkpoint;", StringComparison.Ordinal);
			int retire = step.IndexOf("KingdomSubsidenceStepRules.TryRetire(", StringComparison.Ordinal);
			Assert.IsTrue(report >= 0 && checkpoint > report && retire > checkpoint,
				"Source contract only: reporting precedes checkpoint and atomic retirement.");
			string driver = TestMain.ReadRepositoryText("Growth/KingdomSubsidenceStepRuntime.Driver.cs");
			int resume = driver.IndexOf("ResumeStep(frame, readSupports, out refusal)", StringComparison.Ordinal);
			Assert.Greater(driver.IndexOf("ResumeBatchReport(frame, batch, out refusal)",
				StringComparison.Ordinal), resume);
		}

		private sealed class HostileMessageException : Exception
		{
			private readonly Action observe;
			internal HostileMessageException(Action observe) { this.observe = observe; }
			public override string Message
			{
				get { observe(); throw new InvalidOperationException("hostile Message getter"); }
			}
		}
	}
}
#endif
