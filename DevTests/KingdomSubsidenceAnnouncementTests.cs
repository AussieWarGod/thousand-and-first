#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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
				ClassicAssert.IsTrue(ids.Add(Announcement(port.Book).Active.Id));
				ClassicAssert.IsTrue(Driver.Resume(port, out string refusal), refusal); port.Load();
				ClassicAssert.AreEqual(index + 1, Announcement(port.Book).Ordinal); ClassicAssert.AreEqual(100, Announcement(port.Book).LastTick);
				ClassicAssert.IsNull(Announcement(port.Book).Active); ClassicAssert.AreEqual(index % 2 == 0, port.Announced);
			}
			ClassicAssert.AreEqual(3, port.Official.Count); ClassicAssert.AreEqual(3, port.Outsider.Count);
			ClassicAssert.AreEqual(3, port.Messages.Count); ClassicAssert.AreEqual(3, port.Ledger.Notes.Count); ClassicAssert.AreEqual(3, port.FlagWrites);
			ClassicAssert.IsTrue(KingdomChronicleReceiptRules.TryParseRegistry(port.Registry, out var rows, out _, out _)); ClassicAssert.AreEqual(3, rows.Count);
			foreach (var row in rows) ClassicAssert.IsTrue(KingdomChronicleReceiptRules.IsTerminal(row));
		}

		[TestCase(false)] [TestCase(true)]
		public void BeginAndArrestOfficialAppendCutRecoverOneOutsiderWithoutDuplicatingOfficial(bool before)
		{
			var port = new Port(Prepare(before: before), before) { Cut = "official:after" };
			string id = Announcement(port.Book).Active.Id, reportWire = Announcement(port.Book).Active.ReportModel;
			ClassicAssert.IsFalse(Driver.Resume(port, out string refusal)); ClassicAssert.IsNotEmpty(refusal);
			ClassicAssert.AreEqual(1, port.Official.Count); ClassicAssert.AreEqual(0, port.Outsider.Count);
			ClassicAssert.AreEqual(!before, port.Announced); ClassicAssert.IsNotNull(Announcement(port.Book).Active);
			ClassicAssert.IsFalse(Announcements.TryPrepare(port.Book, !before, before, 101, "changed", "changed", out _));
			port.Load(); ClassicAssert.AreEqual(id, Announcement(port.Book).Active.Id);
			ClassicAssert.IsTrue(KingdomSubsidenceReportCodec.TryDecode(reportWire, out var frozen));
			ClassicAssert.IsTrue(Driver.Resume(port, out refusal), refusal);
			ClassicAssert.AreEqual(1, port.Official.Count); ClassicAssert.AreEqual(1, port.Outsider.Count);
			StringAssert.Contains(frozen.Entries[0].Text, port.Official[0]); StringAssert.Contains("at 100:", port.Outsider[0]);
			ClassicAssert.AreEqual(1, port.Messages.Count); ClassicAssert.AreEqual(1, port.Ledger.Notes.Count);
			string completed = Wire(port.Book), registry = port.Registry;
			ClassicAssert.IsTrue(Driver.Resume(port, out refusal), refusal); ClassicAssert.AreEqual(completed, Wire(port.Book)); ClassicAssert.AreEqual(registry, port.Registry);
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
			ClassicAssert.IsFalse(Driver.Resume(port, out string refusal), cut); ClassicAssert.IsNotEmpty(refusal); ClassicAssert.IsNull(port.Cut, "cut must actually execute");
			port.Load(); var state = Announcement(port.Book);
			ClassicAssert.AreEqual(1, state.Ordinal); if (state.Active != null) ClassicAssert.AreEqual(id, state.Active.Id);
			if (state.Active != null && (state.Active.Notice == KingdomSubsidenceNoticePhase.Intent || state.Active.Notice == KingdomSubsidenceNoticePhase.Unconfirmed))
			{
				// Explicit pure acknowledgement. Native guarded display is covered separately by source contracts.
				ClassicAssert.IsTrue(Announcements.TryNotice(port.Book, KingdomSubsidenceNoticePhase.Acknowledged, out var acknowledged)); port.Install(Reload(acknowledged));
			}
			ClassicAssert.IsTrue(Driver.Resume(port, out refusal), refusal);
			ClassicAssert.IsNull(Announcement(port.Book).Active); ClassicAssert.AreEqual(1, Announcement(port.Book).Ordinal);
			ClassicAssert.AreEqual(1, port.FlagWrites); ClassicAssert.AreEqual(1, port.Official.Count); ClassicAssert.AreEqual(1, port.Outsider.Count);
			ClassicAssert.AreEqual(1, port.Ledger.Notes.Count); ClassicAssert.LessOrEqual(port.Messages.Count, 1);
			int queue = port.Messages.Count, publications = port.Publishes; string wire = Wire(port.Book), registry = port.Registry;
			ClassicAssert.IsTrue(Driver.Resume(port, out refusal)); ClassicAssert.AreEqual(queue, port.Messages.Count); ClassicAssert.AreEqual(publications, port.Publishes);
			ClassicAssert.AreEqual(wire, Wire(port.Book)); ClassicAssert.AreEqual(registry, port.Registry);
		}

		[TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
		public void TrueWithoutParentWriteCannotAuthorizeFurtherProgress(int publication)
		{
			var port = new Port(Prepare()) { NoWriteAt = publication };
			ClassicAssert.IsFalse(Driver.Resume(port, out string refusal)); ClassicAssert.IsNotEmpty(refusal);
			ClassicAssert.AreEqual(publication, port.Publishes); ClassicAssert.IsNotNull(Announcement(port.Book).Active);
			if (publication < 3) ClassicAssert.AreEqual(0, port.Messages.Count);
			port.NoWriteAt = 0;
			if (Announcement(port.Book).Active.Notice == KingdomSubsidenceNoticePhase.Intent)
			{ ClassicAssert.IsTrue(Announcements.TryNotice(port.Book, KingdomSubsidenceNoticePhase.Acknowledged, out var ack)); port.Install(ack); }
			ClassicAssert.IsTrue(Driver.Resume(port, out refusal), refusal);
			ClassicAssert.AreEqual(1, port.Official.Count); ClassicAssert.AreEqual(1, port.Outsider.Count); ClassicAssert.AreEqual(1, port.Ledger.Notes.Count);
			ClassicAssert.LessOrEqual(port.Messages.Count, 1);
		}

		[Test]
		public void InterruptedNoticeMustBeAcknowledgedRatherThanReplayedOrCalledDisplayed()
		{
			var port = new Port(Prepare()) { Cut = "message:after" };
			ClassicAssert.IsFalse(Driver.Resume(port, out _)); port.Load(); string held = Wire(port.Book);
			ClassicAssert.AreEqual(KingdomSubsidenceNoticePhase.Unconfirmed, Announcement(port.Book).Active.Notice);
			ClassicAssert.IsFalse(Driver.Resume(port, out string refusal)); StringAssert.Contains("homecoming", refusal);
			ClassicAssert.AreEqual(held, Wire(port.Book)); ClassicAssert.AreEqual(1, port.Messages.Count);
			ClassicAssert.IsFalse(Announcements.TryNotice(port.Book, KingdomSubsidenceNoticePhase.Returned, out _));
			ClassicAssert.IsTrue(Announcements.TryNotice(port.Book, KingdomSubsidenceNoticePhase.Acknowledged, out var ack)); port.Install(ack);
			ClassicAssert.IsTrue(Driver.Resume(port, out refusal), refusal); ClassicAssert.AreEqual(1, port.Messages.Count);
		}

		[Test]
		public void LostChronicleReceiptIsArchivedWithoutPruningForeignListContents()
		{
			var port = new Port(Prepare()) { Cut = "official:after" };
			ClassicAssert.IsFalse(Driver.Resume(port, out _)); port.Official.Add("foreign subsequent entry");
			ClassicAssert.IsTrue(Driver.Resume(port, out string refusal), refusal);
			ClassicAssert.AreEqual(2, port.Official.Count); ClassicAssert.AreEqual("foreign subsequent entry", port.Official[1]);
			ClassicAssert.AreEqual(1, port.Outsider.Count);
			ClassicAssert.IsTrue(KingdomSubsidenceReportArchive.TryRead(port.Book.FailureModel, out var failures)); ClassicAssert.AreEqual(1, failures.Count);
			ClassicAssert.IsTrue(KingdomSubsidenceReportCodec.TryDecode(failures[0], out var report));
			ClassicAssert.IsTrue(report.Entries[0].ChronicleLost); ClassicAssert.IsFalse(KingdomSubsidenceReportRules.Complete(report));
		}

		[Test]
		public void OwnerLossAfterMessageCannotWriteReportOrReplaceTheHeldIntent()
		{
			var port = new Port(Prepare()); port.OnMessage = () => port.Owned = false;
			ClassicAssert.IsFalse(Driver.Resume(port, out string refusal)); ClassicAssert.IsNotEmpty(refusal);
			ClassicAssert.AreEqual(KingdomSubsidenceNoticePhase.Intent, Announcement(port.Book).Active.Notice);
			ClassicAssert.AreEqual(0, port.Ledger.Notes.Count); ClassicAssert.AreEqual(0, port.Official.Count); ClassicAssert.AreEqual(0, port.Outsider.Count);
			string wire = Wire(port.Book); ClassicAssert.IsFalse(Driver.Resume(port, out _)); ClassicAssert.AreEqual(wire, Wire(port.Book));
		}

		[Test]
		public void TrueReportResultWithoutDurableProofDoesNotRetireAnnouncement()
		{
			var port = new Port(Prepare()) { SkipReport = true };
			ClassicAssert.IsFalse(Driver.Resume(port, out string refusal)); ClassicAssert.IsNotEmpty(refusal);
			ClassicAssert.IsNotNull(Announcement(port.Book).Active); ClassicAssert.AreEqual(0, port.Official.Count);
			port.SkipReport = false; ClassicAssert.IsTrue(Driver.Resume(port, out refusal), refusal);
			ClassicAssert.AreEqual(1, port.Messages.Count); ClassicAssert.AreEqual(1, port.Official.Count); ClassicAssert.AreEqual(1, port.Outsider.Count);
		}

		[Test]
		public void FullChronicleRetainsExactCapacityRefusalAndNeverInventsDeliveredOrLostSink()
		{
			var port = new Port(Prepare()) { Registry = ChronicleCapacityFixture.Full }; string registry = port.Registry;
			ClassicAssert.IsTrue(Driver.Resume(port, out string refusal), refusal); port.Load();
			ClassicAssert.IsNull(Announcement(port.Book).Active); ClassicAssert.AreEqual(registry, port.Registry);
			ClassicAssert.AreEqual(0, port.Official.Count); ClassicAssert.AreEqual(0, port.Outsider.Count); ClassicAssert.AreEqual(1, port.Ledger.Notes.Count);
			ClassicAssert.IsTrue(KingdomSubsidenceReportArchive.TryRead(port.Book.FailureModel, out var rows)); ClassicAssert.AreEqual(1, rows.Count);
			ClassicAssert.IsTrue(KingdomSubsidenceReportCodec.TryDecode(rows[0], out var report)); var entry = report.Entries[0];
			ClassicAssert.IsTrue(entry.CapacityRefused); ClassicAssert.IsFalse(entry.ChronicleProved); ClassicAssert.IsFalse(entry.ChronicleLost);
			ClassicAssert.AreEqual(4096, entry.CapacityCount); StringAssert.Contains("registry full", KingdomSubsidenceReportArchive.Digest(port.Book));
			string held = Wire(port.Book); ClassicAssert.IsTrue(Driver.Resume(port, out refusal)); ClassicAssert.AreEqual(held, Wire(port.Book));
		}

		[Test]
		public void FullFailureArchiveRetainsNinthTransitionUntilPriorWarningsAreExplicitlyAcknowledged()
		{
			var port = new Port(Prepare()) { Registry = ChronicleCapacityFixture.Full };
			for (int i = 0; i < KingdomSubsidenceReportArchive.MaxReports; i++)
			{
				if (i != 0) port.Install(Prepare(port.Book, port.Announced));
				ClassicAssert.IsTrue(Driver.Resume(port, out string refusal), refusal);
			}
			string archive = port.Book.FailureModel, registry = port.Registry;
			port.Install(Prepare(port.Book, port.Announced));
			ClassicAssert.IsFalse(Driver.Resume(port, out string reason)); StringAssert.Contains("archive", reason);
			ClassicAssert.AreEqual(archive, port.Book.FailureModel); ClassicAssert.AreEqual(registry, port.Registry);
			ClassicAssert.AreEqual(9, Announcement(port.Book).Ordinal); ClassicAssert.IsNotNull(Announcement(port.Book).Active);
			string pending = Wire(port.Book); ClassicAssert.IsFalse(Driver.Resume(port, out _)); ClassicAssert.AreEqual(pending, Wire(port.Book));
			// This pure acknowledgement is not a native display proof. Runtime must first show the exact archive.
			port.Install(Reload(port.Book.WithFailures(KingdomSubsidenceReportArchive.None)));
			ClassicAssert.IsTrue(Driver.Resume(port, out reason), reason); ClassicAssert.IsNull(Announcement(port.Book).Active);
			ClassicAssert.IsTrue(KingdomSubsidenceReportArchive.TryRead(port.Book.FailureModel, out var rows)); ClassicAssert.AreEqual(1, rows.Count);
			ClassicAssert.AreEqual(9, port.Messages.Count); ClassicAssert.AreEqual(9, port.Ledger.Notes.Count); ClassicAssert.AreEqual(registry, port.Registry);
		}

		[TestCase("before")] [TestCase("after")] [TestCase("foreign")]
		public void ActualHomecomingRuleSettlesResetSensitiveLedgerIntentWithoutLaunderingItsLoss(string cut)
		{
			var port = new Port(Prepare()) { Cut = "ledger:" + (cut == "before" ? "before" : "after") };
			ClassicAssert.IsFalse(Driver.Resume(port, out _)); if (cut == "foreign") port.Ledger.Notes.Add("unrelated retained note");
			var notes = port.Ledger.Notes; string[] contents = notes.ToArray(); string held = Wire(port.Book);
			ClassicAssert.IsTrue(Announcements.TryAcknowledgeHomecoming(port.Book, notes, out var acknowledged));
			ClassicAssert.AreEqual(held, Wire(port.Book)); ClassicAssert.AreSame(notes, port.Ledger.Notes); CollectionAssert.AreEqual(contents, notes);
			ClassicAssert.IsNotNull(Announcement(acknowledged).Active); ClassicAssert.AreEqual(port.Book.FailureModel, acknowledged.FailureModel);
			ClassicAssert.IsTrue(KingdomSubsidenceReportCodec.TryDecode(Announcement(acknowledged).Active.ReportModel, out var report));
			ClassicAssert.AreEqual(cut == "after" ? ReportLedgerPhase.Proved : ReportLedgerPhase.Lost, report.Entries[0].LedgerPhase);
			ClassicAssert.IsFalse(report.Entries[0].ChronicleProved); ClassicAssert.IsFalse(report.Entries[0].ChronicleLost);
			port.Install(Reload(acknowledged)); port.Ledger.Reset();
			ClassicAssert.IsTrue(Driver.Resume(port, out string refusal), refusal);
			ClassicAssert.AreEqual(0, notes.Count, "reading cannot cause a saved ledger line to be appended again");
			ClassicAssert.AreEqual(1, port.Official.Count); ClassicAssert.AreEqual(1, port.Outsider.Count);
			ClassicAssert.AreEqual(cut == "after", port.Book.FailureModel == KingdomSubsidenceReportArchive.None);
		}
	}
}
#endif
