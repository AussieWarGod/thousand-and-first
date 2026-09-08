#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source-only clock ownership contracts; no master transition or native recovery is executed.</summary>
	[TestFixture]
	public sealed class KingdomSubsidenceMasterClockSourceTests
	{
		private const string Settlement = "Core/KingdomMasterSettlementPlan.cs";
		private const string Recovery = "Core/KingdomMasterRecoveryPlans.cs";
		private const string GrowthResume = "Experience/KingdomMasterGrowthResumeRules.cs";

		[TestCase("KingdomSystem")]
		[TestCase("KingdomSettlement")]
		public void SourceContractSeatAndArchiveReadExistingSubsidenceCheckpoint(string ownerType)
		{
			string body = Method(Settlement, "internal static bool TryCreate(" + ownerType + " source,");
			string[] arguments = Arguments(body, "return TryCreateCore(");
			ClassicAssert.Greater(arguments.Length, 5);
			ClassicAssert.AreEqual("source.LastSubsidenceTick", arguments[4]);
			StringAssert.Contains("long oldFood, long oldSubsidence, bool semanticActive",
				Code(Settlement));
			ClassicAssert.IsFalse(Regex.IsMatch(body, @"\bsource\.LastSubsidenceTick\s*=(?!=)"),
				"Capturing a resume plan must not replace the source checkpoint.");
		}

		[Test]
		public void SourceContractPlanPreservesPendingCheckpointWithOrWithoutHeartbeat()
		{
			string body = Method(Settlement, "private static bool TryCreateCore(");
			string[] arguments = Arguments(body, "plan = new SettlementPlan(");
			ClassicAssert.Greater(arguments.Length, 6);
			ClassicAssert.AreEqual("lifecycle?.Growth?.HeartbeatOp == null ? now : oldHeartbeat", arguments[1],
				"Heartbeat retains its own existing lease policy.");
			ClassicAssert.AreEqual("oldSubsidence", arguments[5],
				"The pending-step anchor must be passed unchanged for both heartbeat states.");
			ClassicAssert.IsFalse(Regex.IsMatch(body, @"\boldSubsidence\s*(?:=(?!=)|\+=|-=|\+\+|--)"),
				"A direct constructor argument must not hide an earlier reanchor.");
			StringAssert.Contains("long foodWork, long subsidence, long semantic", Code(Settlement));
			ClassicAssert.AreEqual("subsidence", OnlyAssignment(
				Method(Settlement, "private SettlementPlan("), "Subsidence"));
		}

		[TestCase("KingdomSystem")]
		[TestCase("KingdomSettlement")]
		public void SourceContractSeatAndArchivePublishCapturedCheckpointBeforeRecovery(string ownerType)
		{
			string body = Method(Settlement, "internal void Publish(" + ownerType + " target)");
			ClassicAssert.AreEqual("Subsidence", OnlyAssignment(body, "target.LastSubsidenceTick"));
			int checkpoint = body.IndexOf("target.LastSubsidenceTick = Subsidence;", StringComparison.Ordinal);
			int recovery = body.IndexOf("Lifecycle?.Publish(target.LifecycleBook);", StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(checkpoint, 0);
			ClassicAssert.Greater(recovery, checkpoint,
				"The recovery publisher inspected below is reached after the preserved checkpoint write.");
			ClassicAssert.AreEqual("Heartbeat", OnlyAssignment(body, "target.LastHeartbeatTick"));
		}

		[Test]
		public void SourceContractLifecycleRecoveryHasNoSubsidenceReanchor()
		{
			string body = Method(Recovery, "internal void Publish(KingdomLifecycleBook book)");
			StringAssert.Contains("Growth.PublishPrevalidated();", body);
			string preparation = Method(GrowthResume, "internal static bool PrepareMasterGrowthResume(");
			StringAssert.Contains("if (book.HeartbeatOp == null) book.LastHeartbeatTick = now;", preparation);
			StringAssert.Contains("if (book.DepartureOp == null) book.LastDepartureTick = now;", preparation);
			string publication = Method(GrowthResume, "internal static void CopyMasterGrowthResumeScalars(");
			StringAssert.Contains("to.LastHeartbeatTick = from.LastHeartbeatTick;", publication);
			StringAssert.DoesNotContain("LastSubsidenceTick", publication,
				"The detached scalar publisher must retain subsidence's independent checkpoint.");
			StringAssert.DoesNotContain("LastSubsidenceTick", Code(Recovery),
				"No recovery-plan branch may independently reanchor the subsidence clock.");
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
			ClassicAssert.GreaterOrEqual(open, 0, path);
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
			}
			Assert.Fail("Unclosed method: " + path); return null;
		}

		private static string[] Arguments(string source, string call)
		{
			Match match = Regex.Match(source, Regex.Escape(call) + @"(?<arguments>[^;]*?)\);");
			ClassicAssert.IsTrue(match.Success, call);
			string[] arguments = match.Groups["arguments"].Value.Split(',');
			for (int i = 0; i < arguments.Length; i++) arguments[i] = arguments[i].Trim();
			return arguments;
		}

		private static string OnlyAssignment(string source, string target)
		{
			MatchCollection assignments = Regex.Matches(source,
				@"\b" + Regex.Escape(target) + @"\s*=(?!=)\s*(?<value>[^;]+);");
			ClassicAssert.AreEqual(1, assignments.Count, target + " must have one explicit publication.");
			return assignments[0].Groups["value"].Value.Trim();
		}
	}
}
#endif
