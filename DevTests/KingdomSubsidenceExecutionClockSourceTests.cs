#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	// Source contracts only; the pure clock table and actual native refusal probes are separate.
	[TestFixture]
	public sealed class KingdomSubsidenceExecutionClockSourceTests
	{
		private const string Prefix = "Growth/KingdomSubsidenceStepRuntime.";

		[Test]
		public void SourceContract_AdmissionRequiresActualNowAndUnmodifiedSavedCheckpoint()
		{
			string source = Method(Prefix + "Execution.cs", "private static bool TryExecutionFrame(");
			Ordered(source, "The.Game == null || now < 0 || now != The.Game.TimeTicks",
				"!TryOptionFrame(system, out OptionFrame owner)",
				"KingdomSubsidenceExecutionClockRules.TryValidate(now, system.LastSubsidenceTick, owner.Owner.Step, out refusal)",
				"!ExecutionExact(owner, now)", "frame = owner;", "refusal = null;");
			string exact = Method(Prefix + "Execution.cs", "private static bool ExecutionExact(");
			Ordered(exact, "OptionExact(frame)", "frame.Game.TimeTicks == now",
				"KingdomSubsidenceExecutionClockRules.TryValidate(now, frame.System.LastSubsidenceTick, frame.Owner.Step, out _)");
			ClassicAssert.IsFalse(Regex.IsMatch(Code(Prefix + "Execution.cs"),
				@"\b(?:LastSubsidenceTick|TimeTicks)\s*(?:=(?!=)|\+=|-=|\+\+|--)"));
		}

		[Test]
		public void SourceContract_ExecutionPublisherProvesBothBooksBeforeWritingAndRechecksAfterward()
		{
			Ordered(Method(Prefix + "Execution.cs", "private static bool SaveExecutingOption("),
				"ExecutionExact(frame, now)",
				"KingdomSubsidenceExecutionClockRules.TryValidate(now, frame.System.LastSubsidenceTick, next, out _)",
				"SaveOption(frame, next, admit)", "ExecutionExact(frame, now)");
		}

		[Test]
		public void SourceContract_AllOptionPublicationsUseExecutionBoundary()
		{
			string core = Method(Prefix + "Options.cs", "private static bool TryOptionCore(");
			Ordered(core, "TryExecutionFrame(system, now, out OptionFrame frame, out refusal)",
				"KingdomSubsidenceOptionRuntime.TryObserve(", "!ExecutionExact(frame, now)");
			foreach (string next in new[] { "admitted", "frozen", "cancelled", "finished" })
				StringAssert.Contains("SaveExecutingOption(frame, " + next + ", now", core);
			StringAssert.DoesNotContain("SaveOption(", core);
			StringAssert.DoesNotContain("OptionExact(", core);
			Ordered(core, "KingdomSubsidenceOptionRuntime.TryPublish(", "!ExecutionExact(frame, now)",
				"system.LastSubsidenceTick = checkpoint;", "!ExecutionExact(frame, now)",
				"KingdomSubsidenceStepRules.TryFinishOption(", "SaveExecutingOption(frame, finished, now)");
		}

		[Test]
		public void SourceContract_DriverAdmissionPublicationAndChildRefreshUseExecutionBoundary()
		{
			StringAssert.Contains("TryExecutionFrame(system, now, out OptionFrame owner, out refusal)",
				Method(Prefix + "DriverFrame.cs", "private static bool TryDriverFrame("));
			StringAssert.Contains("ExecutionExact(frame.Owner, frame.Now)",
				Method(Prefix + "DriverFrame.cs", "private static bool DriverExact("));
			Ordered(Method(Prefix + "DriverFrame.cs", "private static bool SaveDriver("),
				"DriverExact(frame)", "SaveExecutingOption(frame.Owner, next, frame.Now)", "DriverExact(frame)");
			StringAssert.Contains("TryExecutionFrame(held.System, frame.Now, out OptionFrame current, out _)",
				Method(Prefix + "DriverFrame.cs", "private static bool RefreshDriver("));
			StringAssert.Contains("SaveExecutingOption(frame.Owner, admitted, now, true)", Code(Prefix + "Driver.cs"));
			StringAssert.DoesNotContain("SaveOption(", Code(Prefix + "Driver.cs"));
		}

		[Test]
		public void SourceContract_OrdinaryAndPendingPassesCannotBypassClockAdmission()
		{
			Ordered(Method(Prefix + "Pass.cs", "internal static bool TryBeforePass("),
				"TryExecutionFrame(system, XRL.The.Game?.TimeTicks ?? -1L, out _, out refusal)", "HasPending(system)");
			StringAssert.Contains("TryExecutionFrame(system, XRL.The.Game?.TimeTicks ?? -1L, out OptionFrame frame, out _)",
				Method(Prefix + "Pass.cs", "internal static bool CanStartReckoning("));
			Ordered(Method("Growth/KingdomSubsidence.Reckoning.cs", "internal static bool TryReckon("),
				"now != The.Game.TimeTicks", "KingdomSubsidenceStepRuntime.HasPending(system)",
				"KingdomSubsidenceStepRuntime.TryPassGuard(", "out refusal)", "RecordZone(",
				"system.LastSubsidenceTick = Checkpoint(");
		}

		[Test]
		public void SourceContract_EarnedStepCheckpointStillPrecedesRetirementWithExactChecks()
		{
			Ordered(Method(Prefix + "Step.cs", "private static bool SettleStep("),
				"KingdomSubsidenceStepRules.TryCheckpoint(", "system.LastSubsidenceTick = checkpoint;",
				"!DriverExact(frame)", "system.LastSubsidenceTick != checkpoint",
				"KingdomSubsidenceStepRules.TryRetire(", "SaveDriver(frame, retired)");
		}

		[Test]
		public void SourceContract_HomecomingRetainsStructuralAdmissionAndReportAcknowledgment()
		{
			string source = Code(Prefix + "Homecoming.cs");
			foreach (string forbidden in new[] { "TryExecutionFrame(", "ExecutionExact(",
				"SaveExecutingOption(", "KingdomSubsidenceExecutionClockRules" })
				StringAssert.DoesNotContain(forbidden, source);
			Ordered(Method(Prefix + "Homecoming.cs", "internal static bool TryReadHomecoming("),
				"TryOptionFrame(system, out OptionFrame owner)", "show(digest);",
				"SaveOption(owner, next)", "HomecomingExact(frame)", "ledger.Reset();");
		}

		private static string Code(string path)
		{
			string source = TestMain.ReadRepositoryText(path);
			source = Regex.Replace(source, @"//[^\r\n]*|/\*[\s\S]*?\*/", " ");
			return Regex.Replace(source, @"\s+", " ");
		}

		private static string Method(string path, string signature)
		{
			string source = Code(path);
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, path + ": " + signature);
			int open = source.IndexOf('{', start), depth = 0;
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
			}
			Assert.Fail("Unclosed method: " + signature); return null;
		}

		private static void Ordered(string source, params string[] tokens)
		{
			int cursor = 0;
			foreach (string token in tokens)
			{
				int at = source.IndexOf(token, cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(at, cursor, "Missing or reordered: " + token);
				cursor = at + token.Length;
			}
		}
	}
}
#endif
