#if TAF_TESTS
using System;
using NUnit.Framework;
using Announcements = ThousandAndFirst.KingdomSubsidenceAnnouncementRules;
using Driver = ThousandAndFirst.KingdomSubsidenceAnnouncementDriver;

namespace ThousandAndFirst.Tests
{
	/// <summary>Actual pure announcement coordinator with injected sinks and actual receipt laws.
	/// Does not execute XRL, a real game save, a display event or the native Chronicle publisher.</summary>
	[TestFixture]
	public sealed partial class KingdomSubsidenceAnnouncementTests
	{
		[Test]
		public void ThreeAlternatingTransitionsAtSameTickHaveDistinctDurableIdentities()
		{
			var port = new Port(Prepare()); var ids = new System.Collections.Generic.HashSet<string>();
			for (int index = 0; index < 3; index++)
			{
				if (index > 0) port.Install(Prepare(port.Book, port.Announced));
				Assert.IsTrue(ids.Add(Announcement(port.Book).Active.Id));
				Assert.IsTrue(Driver.Resume(port, out string refusal), refusal); port.Load();
				Assert.AreEqual(index + 1, Announcement(port.Book).Ordinal); Assert.AreEqual(100, Announcement(port.Book).LastTick);
				Assert.IsNull(Announcement(port.Book).Active); Assert.AreEqual(index % 2 == 0, port.Announced);
			}
			Assert.AreEqual(3, port.Official.Count); Assert.AreEqual(3, port.Outsider.Count);
			Assert.AreEqual(3, port.Messages.Count); Assert.AreEqual(3, port.Ledger.Notes.Count); Assert.AreEqual(3, port.FlagWrites);
			Assert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(port.Registry, out var rows, out _, out _)); Assert.AreEqual(3, rows.Count);
			foreach (var row in rows) Assert.IsTrue(KingdomChronicleReceiptRules.IsTerminal(row));
		}

		[TestCase(false)] [TestCase(true)]
		public void BeginAndArrestOfficialAppendCutRecoverOneOutsiderWithoutDuplicatingOfficial(bool before)
		{
			var port = new Port(Prepare(before: before), before) { Cut = "official:after" };
			string id = Announcement(port.Book).Active.Id, reportWire = Announcement(port.Book).Active.ReportModel;
			Assert.IsFalse(Driver.Resume(port, out string refusal)); Assert.IsNotEmpty(refusal);
			Assert.AreEqual(1, port.Official.Count); Assert.AreEqual(0, port.Outsider.Count);
			Assert.AreEqual(!before, port.Announced); Assert.IsNotNull(Announcement(port.Book).Active);
			Assert.IsFalse(Announcements.TryPrepare(port.Book, !before, before, 101, "changed", "changed", out _));
			port.Load(); Assert.AreEqual(id, Announcement(port.Book).Active.Id);
			Assert.IsTrue(KingdomSubsidenceReportCodec.TryDecode(reportWire, out var frozen));
			Assert.IsTrue(Driver.Resume(port, out refusal), refusal);
			Assert.AreEqual(1, port.Official.Count); Assert.AreEqual(1, port.Outsider.Count);
			StringAssert.Contains(frozen.Entries[0].Text, port.Official[0]); StringAssert.Contains("at 100:", port.Outsider[0]);
			Assert.AreEqual(1, port.Messages.Count); Assert.AreEqual(1, port.Ledger.Notes.Count);
			string completed = Wire(port.Book), registry = port.Registry;
			Assert.IsTrue(Driver.Resume(port, out refusal), refusal); Assert.AreEqual(completed, Wire(port.Book)); Assert.AreEqual(registry, port.Registry);
		}

		[TestCase("flag:before")] [TestCase("flag:after")]
		[TestCase("message:before")] [TestCase("message:after")]
		[TestCase("ledger:before")] [TestCase("ledger:after")]
		[TestCase("official:before")] [TestCase("official:after")]
		[TestCase("outsider:before")] [TestCase("outsider:after")]
		[TestCase("save:1:before")] [TestCase("save:1:after")]
		[TestCase("save:2:before")] [TestCase("save:2:after")]
		[TestCase("save:3:before")] [TestCase("save:3:after")]
		[TestCase("save:4:before")] [TestCase("save:4:after")]
		[TestCase("save:5:before")] [TestCase("save:5:after")]
		[TestCase("save:6:before")] [TestCase("save:6:after")]
		[TestCase("save:7:before")] [TestCase("save:7:after")]
		public void EveryCoordinatorPublicationAndEffectCutRetainsRecoverableIdentity(string cut)
		{
			var port = new Port(Prepare()) { Cut = cut }; string id = Announcement(port.Book).Active.Id;
			Assert.IsFalse(Driver.Resume(port, out string refusal), cut); Assert.IsNotEmpty(refusal); Assert.IsNull(port.Cut, "cut must actually execute");
			port.Load(); var state = Announcement(port.Book);
			Assert.AreEqual(1, state.Ordinal); if (state.Active != null) Assert.AreEqual(id, state.Active.Id);
			if (state.Active != null && (state.Active.Notice == KingdomSubsidenceNoticePhase.Intent || state.Active.Notice == KingdomSubsidenceNoticePhase.Unconfirmed))
			{
				// Explicit pure acknowledgement. Native guarded display is covered separately by source contracts.
				Assert.IsTrue(Announcements.TryNotice(port.Book, KingdomSubsidenceNoticePhase.Acknowledged, out var acknowledged)); port.Install(Reload(acknowledged));
			}
			Assert.IsTrue(Driver.Resume(port, out refusal), refusal);
			Assert.IsNull(Announcement(port.Book).Active); Assert.AreEqual(1, Announcement(port.Book).Ordinal);
			Assert.AreEqual(1, port.FlagWrites); Assert.AreEqual(1, port.Official.Count); Assert.AreEqual(1, port.Outsider.Count);
			Assert.AreEqual(1, port.Ledger.Notes.Count); Assert.LessOrEqual(port.Messages.Count, 1);
			int queue = port.Messages.Count, publications = port.Publishes; string wire = Wire(port.Book), registry = port.Registry;
			Assert.IsTrue(Driver.Resume(port, out refusal)); Assert.AreEqual(queue, port.Messages.Count); Assert.AreEqual(publications, port.Publishes);
			Assert.AreEqual(wire, Wire(port.Book)); Assert.AreEqual(registry, port.Registry);
		}

		[TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
		public void TrueWithoutParentWriteCannotAuthorizeFurtherProgress(int publication)
		{
			var port = new Port(Prepare()) { NoWriteAt = publication };
			Assert.IsFalse(Driver.Resume(port, out string refusal)); Assert.IsNotEmpty(refusal);
			Assert.AreEqual(publication, port.Publishes); Assert.IsNotNull(Announcement(port.Book).Active);
			if (publication < 3) Assert.AreEqual(0, port.Messages.Count);
			port.NoWriteAt = 0;
			if (Announcement(port.Book).Active.Notice == KingdomSubsidenceNoticePhase.Intent)
			{ Assert.IsTrue(Announcements.TryNotice(port.Book, KingdomSubsidenceNoticePhase.Acknowledged, out var ack)); port.Install(ack); }
			Assert.IsTrue(Driver.Resume(port, out refusal), refusal);
			Assert.AreEqual(1, port.Official.Count); Assert.AreEqual(1, port.Outsider.Count); Assert.AreEqual(1, port.Ledger.Notes.Count);
			Assert.LessOrEqual(port.Messages.Count, 1);
		}

		[Test]
		public void InterruptedNoticeMustBeAcknowledgedRatherThanReplayedOrCalledDisplayed()
		{
			var port = new Port(Prepare()) { Cut = "message:after" };
			Assert.IsFalse(Driver.Resume(port, out _)); port.Load(); string held = Wire(port.Book);
			Assert.AreEqual(KingdomSubsidenceNoticePhase.Unconfirmed, Announcement(port.Book).Active.Notice);
			Assert.IsFalse(Driver.Resume(port, out string refusal)); StringAssert.Contains("homecoming", refusal);
			Assert.AreEqual(held, Wire(port.Book)); Assert.AreEqual(1, port.Messages.Count);
			Assert.IsFalse(Announcements.TryNotice(port.Book, KingdomSubsidenceNoticePhase.Returned, out _));
			Assert.IsTrue(Announcements.TryNotice(port.Book, KingdomSubsidenceNoticePhase.Acknowledged, out var ack)); port.Install(ack);
			Assert.IsTrue(Driver.Resume(port, out refusal), refusal); Assert.AreEqual(1, port.Messages.Count);
		}

		[Test]
		public void LostChronicleReceiptIsArchivedWithoutPruningForeignListContents()
		{
			var port = new Port(Prepare()) { Cut = "official:after" };
			Assert.IsFalse(Driver.Resume(port, out _)); port.Official.Add("foreign subsequent entry");
			Assert.IsTrue(Driver.Resume(port, out string refusal), refusal);
			Assert.AreEqual(2, port.Official.Count); Assert.AreEqual("foreign subsequent entry", port.Official[1]);
			Assert.AreEqual(1, port.Outsider.Count);
			Assert.IsTrue(KingdomSubsidenceReportArchive.TryRead(port.Book.FailureModel, out var failures)); Assert.AreEqual(1, failures.Count);
			Assert.IsTrue(KingdomSubsidenceReportCodec.TryDecode(failures[0], out var report));
			Assert.IsTrue(report.Entries[0].ChronicleLost); Assert.IsFalse(KingdomSubsidenceReportRules.Complete(report));
		}

		[Test]
		public void OwnerLossAfterMessageCannotWriteReportOrReplaceTheHeldIntent()
		{
			var port = new Port(Prepare()); port.OnMessage = () => port.Owned = false;
			Assert.IsFalse(Driver.Resume(port, out string refusal)); Assert.IsNotEmpty(refusal);
			Assert.AreEqual(KingdomSubsidenceNoticePhase.Intent, Announcement(port.Book).Active.Notice);
			Assert.AreEqual(0, port.Ledger.Notes.Count); Assert.AreEqual(0, port.Official.Count); Assert.AreEqual(0, port.Outsider.Count);
			string wire = Wire(port.Book); Assert.IsFalse(Driver.Resume(port, out _)); Assert.AreEqual(wire, Wire(port.Book));
		}

		[Test]
		public void TrueReportResultWithoutDurableProofDoesNotRetireAnnouncement()
		{
			var port = new Port(Prepare()) { SkipReport = true };
			Assert.IsFalse(Driver.Resume(port, out string refusal)); Assert.IsNotEmpty(refusal);
			Assert.IsNotNull(Announcement(port.Book).Active); Assert.AreEqual(0, port.Official.Count);
			port.SkipReport = false; Assert.IsTrue(Driver.Resume(port, out refusal), refusal);
			Assert.AreEqual(1, port.Messages.Count); Assert.AreEqual(1, port.Official.Count); Assert.AreEqual(1, port.Outsider.Count);
		}

		[Test]
		public void FullChronicleRetainsExactCapacityRefusalAndNeverInventsDeliveredOrLostSink()
		{
			var port = new Port(Prepare()) { Registry = ChronicleCapacityFixture.Full }; string registry = port.Registry;
			Assert.IsTrue(Driver.Resume(port, out string refusal), refusal); port.Load();
			Assert.IsNull(Announcement(port.Book).Active); Assert.AreEqual(registry, port.Registry);
			Assert.AreEqual(0, port.Official.Count); Assert.AreEqual(0, port.Outsider.Count); Assert.AreEqual(1, port.Ledger.Notes.Count);
			Assert.IsTrue(KingdomSubsidenceReportArchive.TryRead(port.Book.FailureModel, out var rows)); Assert.AreEqual(1, rows.Count);
			Assert.IsTrue(KingdomSubsidenceReportCodec.TryDecode(rows[0], out var report)); var entry = report.Entries[0];
			Assert.IsTrue(entry.CapacityRefused); Assert.IsFalse(entry.ChronicleProved); Assert.IsFalse(entry.ChronicleLost);
			Assert.AreEqual(4096, entry.CapacityCount); StringAssert.Contains("registry full", KingdomSubsidenceReportArchive.Digest(port.Book));
			string held = Wire(port.Book); Assert.IsTrue(Driver.Resume(port, out refusal)); Assert.AreEqual(held, Wire(port.Book));
		}

		[Test]
		public void FullFailureArchiveRetainsNinthTransitionUntilPriorWarningsAreExplicitlyAcknowledged()
		{
			var port = new Port(Prepare()) { Registry = ChronicleCapacityFixture.Full };
			for (int i = 0; i < KingdomSubsidenceReportArchive.MaxReports; i++)
			{
				if (i != 0) port.Install(Prepare(port.Book, port.Announced));
				Assert.IsTrue(Driver.Resume(port, out string refusal), refusal);
			}
			string archive = port.Book.FailureModel, registry = port.Registry;
			port.Install(Prepare(port.Book, port.Announced));
			Assert.IsFalse(Driver.Resume(port, out string reason)); StringAssert.Contains("archive", reason);
			Assert.AreEqual(archive, port.Book.FailureModel); Assert.AreEqual(registry, port.Registry);
			Assert.AreEqual(9, Announcement(port.Book).Ordinal); Assert.IsNotNull(Announcement(port.Book).Active);
			string pending = Wire(port.Book); Assert.IsFalse(Driver.Resume(port, out _)); Assert.AreEqual(pending, Wire(port.Book));
			// This pure acknowledgement is not a native display proof. Runtime must first show the exact archive.
			port.Install(Reload(port.Book.WithFailures(KingdomSubsidenceReportArchive.None)));
			Assert.IsTrue(Driver.Resume(port, out reason), reason); Assert.IsNull(Announcement(port.Book).Active);
			Assert.IsTrue(KingdomSubsidenceReportArchive.TryRead(port.Book.FailureModel, out var rows)); Assert.AreEqual(1, rows.Count);
			Assert.AreEqual(9, port.Messages.Count); Assert.AreEqual(9, port.Ledger.Notes.Count); Assert.AreEqual(registry, port.Registry);
		}

		[TestCase("before")] [TestCase("after")] [TestCase("foreign")]
		public void ActualHomecomingRuleSettlesResetSensitiveLedgerIntentWithoutLaunderingItsLoss(string cut)
		{
			var port = new Port(Prepare()) { Cut = "ledger:" + (cut == "before" ? "before" : "after") };
			Assert.IsFalse(Driver.Resume(port, out _)); if (cut == "foreign") port.Ledger.Notes.Add("unrelated retained note");
			var notes = port.Ledger.Notes; string[] contents = notes.ToArray(); string held = Wire(port.Book);
			Assert.IsTrue(Announcements.TryAcknowledgeHomecoming(port.Book, notes, out var acknowledged));
			Assert.AreEqual(held, Wire(port.Book)); Assert.AreSame(notes, port.Ledger.Notes); CollectionAssert.AreEqual(contents, notes);
			Assert.IsNotNull(Announcement(acknowledged).Active); Assert.AreEqual(port.Book.FailureModel, acknowledged.FailureModel);
			Assert.IsTrue(KingdomSubsidenceReportCodec.TryDecode(Announcement(acknowledged).Active.ReportModel, out var report));
			Assert.AreEqual(cut == "after" ? ReportLedgerPhase.Proved : ReportLedgerPhase.Lost, report.Entries[0].LedgerPhase);
			Assert.IsFalse(report.Entries[0].ChronicleProved); Assert.IsFalse(report.Entries[0].ChronicleLost);
			port.Install(Reload(acknowledged)); port.Ledger.Reset();
			Assert.IsTrue(Driver.Resume(port, out string refusal), refusal);
			Assert.AreEqual(0, notes.Count, "reading cannot cause a saved ledger line to be appended again");
			Assert.AreEqual(1, port.Official.Count); Assert.AreEqual(1, port.Outsider.Count);
			Assert.AreEqual(cut == "after", port.Book.FailureModel == KingdomSubsidenceReportArchive.None);
		}
	}
}
#endif
