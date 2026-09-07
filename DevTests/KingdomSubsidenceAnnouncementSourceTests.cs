#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Native wiring contracts only; no engine callback, rendered notice or game-save proof.</summary>
	[TestFixture]
	public sealed class KingdomSubsidenceAnnouncementSourceTests
	{
		private static string Read(string name) { return TestMain.ReadRepositoryText("Growth/" + name + ".cs"); }
		private static void Ordered(string source, params string[] tokens)
		{
			int offset = 0;
			foreach (string token in tokens)
			{
				int next = source.IndexOf(token, offset, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(next, offset, "Missing or misordered source boundary: " + token);
				offset = next + token.Length;
			}
		}

		[Test]
		public void ReckonerRecoversFrozenAnnouncementBeforeAnyNewSurveyWorkOrTransition()
		{
			string source = Read("KingdomSubsidence.Reckoning");
			Ordered(source, "TryResumeAnnouncement(system, now", "bool pending =", "TryOption(system, Enabled, now", "RecordZone(", "TryTell(");
			StringAssert.DoesNotContain("KingdomChronicle.Record(", source);
			StringAssert.DoesNotContain("SubsidenceAnnounced =", source);
			StringAssert.DoesNotContain("MessageQueue.AddPlayerMessage(", source);
			StringAssert.Contains("TryTell(system, zone, survey, now, true", source);
			StringAssert.Contains("TryTell(system, zone, survey, now, false", source);
			Ordered(source, "TryTell(system, zone, survey, now, true", "!sameSeat()", "TryPassGuard(system, zone, survey, out exact");
		}

		[Test]
		public void FrozenTextAndDateAreSavedBeforeFlagAndEveryQueueAttempt()
		{
			string runtime = Read("KingdomSubsidenceStepRuntime.Announcements");
			Ordered(runtime, "private static bool TryTellCore", "bool before = system.SubsidenceAnnounced; int population =",
				"KingdomPresentation.Rich(system.KingdomDisplayName)", "system.Population != population",
				"KingdomSubsidenceRules.BeganNote(", "KingdomSubsidenceRules.BeganChronicle(",
				"KingdomSubsidenceAnnouncementRules.TryPrepare(", "port.Publish(prior, pending)", "KingdomSubsidenceAnnouncementDriver.Resume(");
			Ordered(Read("KingdomSubsidenceAnnouncementDriver"), "private static bool ResumeCore", "port.WriteFlag(",
				"KingdomSubsidenceAnnouncementRules.TryProveFlag(", "Notice(port, KingdomSubsidenceNoticePhase.Intent)", "port.Message(op.Message)");
		}

		[Test]
		public void ActualNativePortPinsAllFiveTableReferencesAndUsesGuardedParentPublisher()
		{
			string source = Read("KingdomSubsidenceStepRuntime.Announcements");
			foreach (string table in new[] { "String", "Int", "Int64", "Object", "Boolean" })
				StringAssert.Contains("owner.Game." + table + "GameState", source);
			StringAssert.Contains("foreach (object table in Tables) if (table == null)", source);
			StringAssert.Contains("ReferenceEquals(Tables[4], Owner.Game.BooleanGameState)", source);
			StringAssert.Contains("ExecutionExact(Owner, Now)", source);
			StringAssert.Contains("ReferenceEquals(Book, expected) && SaveExecutingOption(Owner, next, Now) && Exact", source);
			StringAssert.Contains("AnnouncementFlagExact(Owner.System, Book)", source);
			StringAssert.DoesNotContain("SetStringGameState(", source);
			StringAssert.DoesNotContain("LastSubsidenceTick =", source);
			StringAssert.DoesNotContain("TimeTicks =", source);
		}

		[Test]
		public void QueueAttemptHasItsOwnSavedIntentAndUnconfirmedRecoveryNotSeenClaim()
		{
			string source = Read("KingdomSubsidenceAnnouncementDriver");
			Ordered(source, "if (op.Notice == KingdomSubsidenceNoticePhase.Pending)",
				"Notice(port, KingdomSubsidenceNoticePhase.Intent)", "port.Message(op.Message)",
				"KingdomSubsidenceNoticePhase.Returned : KingdomSubsidenceNoticePhase.Unconfirmed",
				"else if (op.Notice == KingdomSubsidenceNoticePhase.Intent)", "Notice(port, KingdomSubsidenceNoticePhase.Unconfirmed)");
			StringAssert.DoesNotContain("KingdomSubsidenceNoticePhase.Delivered", source);
			StringAssert.Contains("ReferenceEquals(port.Book, next)", source);
			StringAssert.Contains("KingdomSubsidenceAnnouncementRules.TryRetire(prior", source);
		}

		[Test]
		public void AnnouncementReportsUseExistingLedgerChronicleAndCapacityDeliveryProtocol()
		{
			string runtime = Read("KingdomSubsidenceStepRuntime.Announcements");
			StringAssert.Contains("ResumeReport(Owner, () => Exact, report, save, out refusal)", runtime);
			string report = Read("KingdomSubsidenceStepRuntime.ReportDelivery");
			Ordered(report, "TryProveLedger(", "TryObserveCapacityRefusalAt(owner.System", "TryPublishCapacity(",
				"RecordOnceAt(owner.System", "TryProveOnceAt(owner.System", "TryProveLostOnceAt(owner.System");
			StringAssert.Contains("ResumeReport(frame.Owner, () => DriverExact(frame)", report);
			string rules = Read("KingdomSubsidenceAnnouncementRules");
			Ordered(rules, "internal static bool TryRetire(", "KingdomSubsidenceReportArchive.TryRetain(", "return Store(retained,");
		}

		[Test]
		public void PendingAnnouncementBlocksOptionResetAndNewPhysicalPass()
		{
			string option = Read("KingdomSubsidenceStepRuntime.Options");
			Ordered(option, "TryExecutionFrame(system, now", "KingdomSubsidenceAnnouncementDriver.Resume(",
				"KingdomSubsidenceOptionRuntime.TryObserve(", "system.LastSubsidenceTick = checkpoint",
				"system.SubsidenceAnnounced = false");
			StringAssert.Contains("!KingdomSubsidenceAnnouncementRules.Pending(frame.Owner.Step)", Read("KingdomSubsidenceStepRuntime.Pass"));
			StringAssert.Contains("KingdomSubsidenceAnnouncementRules.MatchesShape(book)", Read("KingdomSubsidenceStepRules"));
		}

		[Test]
		public void HomecomingShowsFrozenNoticeThenAcknowledgesWithoutRetiringUnseenReportLosses()
		{
			string home = Read("KingdomSubsidenceStepRuntime.Homecoming");
			Ordered(home, "string noticeWarning = AnnouncementWarning(", "noticeWarning.Length == 0",
				"digest += warnings + noticeWarning", "show(digest)", "TryPrepareHomecoming(", "SaveOption(owner, next)", "ledger.Reset()");
			StringAssert.Contains("frame.Owner.System.SubsidenceAnnounced != frame.Announced", home);
			StringAssert.Contains("KingdomSubsidenceAnnouncementRules.TryAcknowledgeHomecoming(value, notes, out value)", home);
			string rules = Read("KingdomSubsidenceAnnouncementRules");
			string acknowledge = rules.Substring(rules.IndexOf("internal static bool TryAcknowledgeHomecoming", StringComparison.Ordinal));
			Ordered(acknowledge, "KingdomSubsidenceNoticePhase.Acknowledged", "KingdomSubsidenceReportRules.TryProveLedger(",
				"KingdomSubsidenceReportRules.TryLoseLedger(", "TryReport(book, settled");
			StringAssert.DoesNotContain("TryRetire(", acknowledge);
			StringAssert.DoesNotContain("WithFailures(", acknowledge);
		}

		[Test]
		public void NewExplicitStepWirePreservesOldVersionReadersAndRetirementFields()
		{
			string codec = Read("KingdomSubsidenceStepCodec");
			StringAssert.Contains("TryEncodeVersion(book, 5, out wire)", codec);
			StringAssert.Contains("version < 5 && book.AnnouncementModel != KingdomSubsidenceAnnouncementCodec.None", codec);
			StringAssert.Contains("version < 5 ? KingdomSubsidenceAnnouncementCodec.None : reader.ReadString()", codec);
			StringAssert.Contains("TryEncodeVersion(value, version, out string canonical) || canonical != wire", codec);
			foreach (string prefix in new[] { "ss1:", "ss2:", "ss3:", "ss4:", "ss5:" }) StringAssert.Contains(prefix, codec);
			StringAssert.Contains("retained.FailureModel, prior.AnnouncementModel", Read("KingdomSubsidenceStepRules.Departures"));
			StringAssert.Contains("current.Owner.Step.AnnouncementModel != held.Owner.Step.AnnouncementModel", Read("KingdomSubsidenceStepRuntime.DriverFrame"));
			StringAssert.Contains("announcement.Active.AtTick > actualNow", Read("KingdomSubsidenceExecutionClockRules"));
		}
	}
}
#endif
