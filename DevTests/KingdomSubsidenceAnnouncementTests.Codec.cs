#if TAF_TESTS
using System;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Book = ThousandAndFirst.KingdomSubsidenceStepBook;
using Announcements = ThousandAndFirst.KingdomSubsidenceAnnouncementRules;

namespace ThousandAndFirst.Tests
{
	public sealed partial class KingdomSubsidenceAnnouncementTests
	{
		[TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
		public void HistoricalStepWiresKeepTheirOwnExactCanonicalBytesAndAdmitNoInventedTransition(int version)
		{
			string historical;
			using (var stream = new MemoryStream())
			using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
			{
				writer.Write(0x30535354 + version * 0x01000000); writer.Write(Realm); writer.Write(Settlement);
				writer.Write(1L); writer.Write(100L);
				if (version >= 2) writer.Write(KingdomSubsidenceStepRules.NoOption);
				if (version >= 3) writer.Write(KingdomSubsidenceBatchRules.None);
				if (version >= 4) writer.Write(KingdomSubsidenceReportArchive.None);
				writer.Write((byte)0); writer.Flush(); historical = "ss" + version + ":" + Convert.ToBase64String(stream.ToArray());
			}
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(historical, out var old));
			Assert.AreEqual(KingdomSubsidenceAnnouncementCodec.None, old.AnnouncementModel);
			Assert.AreEqual(1, old.Sequence); Assert.AreEqual(100, old.LastRetiredTick);
			MethodInfo encode = typeof(KingdomSubsidenceStepCodec).GetMethod("TryEncodeVersion", BindingFlags.NonPublic | BindingFlags.Static);
			object[] args = { old, version, null }; Assert.IsTrue((bool)encode.Invoke(null, args)); Assert.AreEqual(historical, args[2]);
			StringAssert.StartsWith("ss5:", Wire(old));
			Book prospectiveArrest = Prepare(old, true); var announcement = Announcement(prospectiveArrest);
			Assert.AreEqual(1, announcement.Ordinal); Assert.IsTrue(announcement.Active.Before); Assert.IsFalse(announcement.Active.After);
			Assert.AreEqual(old.LastRetiredTick, prospectiveArrest.LastRetiredTick);
			args = new object[] { prospectiveArrest, version, null }; Assert.IsFalse((bool)encode.Invoke(null, args)); Assert.IsNull(args[2]);
		}

		[TestCase("ss1:new")] [TestCase("ss1:legacy")]
		public void UnadmittedNativeMigrationSentinelsRemainExact(string wire)
		{
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out var book));
			Assert.AreEqual(wire, Wire(book)); Assert.AreEqual(KingdomSubsidenceAnnouncementCodec.None, book.AnnouncementModel);
			Assert.IsFalse(Announcements.TryPrepare(book, false, true, 100, "notice", "chronicle", out _));
		}

		[TestCase(null)] [TestCase("")] [TestCase("sa1:")] [TestCase("sa2:none")] [TestCase("sa1:none ")]
		public void MissingOrCorruptCurrentAnnouncementStorageNeverDefaultsToNoPending(string wire)
		{
			var malformed = Empty().WithAnnouncement(wire);
			Assert.IsFalse(KingdomSubsidenceStepRules.Valid(malformed)); Assert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(malformed, out _));
			Assert.IsFalse(KingdomSubsidenceAnnouncementCodec.TryDecode(wire, out var value)); Assert.IsNull(value);
		}

		[Test]
		public void PendingBookRoundtripPreservesEveryFrozenAnnouncementFieldAndBookCopy()
		{
			Book before = Prepare(); string wire = Wire(before); Book restored = Reload(before);
			Assert.AreEqual(wire, Wire(restored)); var a = Announcement(before); var b = Announcement(restored);
			Assert.AreEqual(a.Ordinal, b.Ordinal); Assert.AreEqual(a.LastTick, b.LastTick); Assert.AreEqual(a.Active.Id, b.Active.Id);
			Assert.AreEqual(a.Active.AtTick, b.Active.AtTick); Assert.AreEqual(a.Active.Before, b.Active.Before); Assert.AreEqual(a.Active.After, b.Active.After);
			Assert.AreEqual(a.Active.Message, b.Active.Message); Assert.AreEqual(a.Active.ReportModel, b.Active.ReportModel);
			Assert.AreEqual(a.Active.FlagProved, b.Active.FlagProved); Assert.AreEqual(a.Active.Notice, b.Active.Notice);
			Assert.AreEqual(before.AnnouncementModel, before.With(before.Active, before.Sequence).AnnouncementModel);
			Assert.AreEqual(before.AnnouncementModel, before.WithFailures(before.FailureModel).AnnouncementModel);
			Assert.AreEqual(before.AnnouncementModel, before.WithOption(before.OptionModel).AnnouncementModel);
			Assert.AreEqual(before.AnnouncementModel, before.WithBatch(before.BatchModel).AnnouncementModel);
		}

		[Test]
		public void PendingAnnouncementBlocksNewPhysicalStepWithoutResettingItsClock()
		{
			Book before = Prepare(); string held = Wire(before); long due = 100 + KingdomSubsidenceStepRules.StepTicks;
			Assert.IsTrue(KingdomSubsidenceStepRules.TryBegin(Empty(), 100, due, GrowthStage.City, 5, out _, 0, "water"));
			Assert.IsFalse(KingdomSubsidenceStepRules.TryBegin(before, 100, due, GrowthStage.City, 5, out _, 0, "water"));
			Assert.AreEqual(held, Wire(before)); Assert.AreEqual(0, before.LastRetiredTick); Assert.AreEqual(0, before.Sequence);
		}

		[Test]
		public void MonotoneOrdinalAndTimestampRefuseOverflowAndBackwardClockWithoutRepair()
		{
			var maximum = new KingdomSubsidenceAnnouncement(Realm, Settlement, long.MaxValue, 100, null);
			Assert.IsTrue(KingdomSubsidenceAnnouncementCodec.TryEncode(maximum, out string wire));
			var book = Empty().WithAnnouncement(wire); string held = Wire(book);
			Assert.IsFalse(Announcements.TryPrepare(book, false, true, 100, "notice", "chronicle", out _)); Assert.AreEqual(held, Wire(book));
			var port = new Port(Prepare()); Assert.IsTrue(KingdomSubsidenceAnnouncementDriver.Resume(port, out _));
			Assert.IsFalse(Announcements.TryPrepare(port.Book, true, false, 99, "notice", "chronicle", out _));
			Assert.IsTrue(Announcements.TryPrepare(port.Book, true, false, 100, "notice", "chronicle", out var sameTick));
			Assert.AreEqual(2, Announcement(sameTick).Ordinal);
			Assert.IsFalse(KingdomSubsidenceExecutionClockRules.TryValidate(99, 0, port.Book, out _));
			Assert.IsTrue(KingdomSubsidenceExecutionClockRules.TryValidate(100, 0, port.Book, out _));
		}

		[TestCase("realm")] [TestCase("settlement")] [TestCase("id")] [TestCase("ordinal")]
		[TestCase("equal-flags")] [TestCase("notice")] [TestCase("unproved-notice")] [TestCase("message")]
		public void ForeignIdentityAndImpossibleAnnouncementShapesAreRejected(string field)
		{
			var a = Announcement(Prepare()); var op = a.Active;
			var changed = new KingdomSubsidenceAnnouncementOperation(field == "id" ? op.Id + "0" : op.Id,
				op.AtTick, op.Before, field == "equal-flags" ? op.Before : op.After,
				field == "message" ? "foreign note" : op.Message, op.ReportModel, op.FlagProved,
				field == "notice" ? (KingdomSubsidenceNoticePhase)5 : field == "unproved-notice" ? KingdomSubsidenceNoticePhase.Returned : op.Notice);
			var bad = new KingdomSubsidenceAnnouncement(field == "realm" ? KingdomIdentityRules.RealmPrefix + new string('c', 64) : a.RealmId,
				field == "settlement" ? KingdomIdentityRules.SettlementPrefix + new string('c', 64) : a.SettlementId,
				field == "ordinal" ? a.Ordinal + 1 : a.Ordinal, a.LastTick, changed);
			Assert.IsFalse(Announcements.Valid(bad)); Assert.IsFalse(KingdomSubsidenceAnnouncementCodec.TryEncode(bad, out _));
		}

		[Test]
		public void AnnouncementTextUsesStrictUtf8AndBoundsWithPairedUnicodeAccepted()
		{
			foreach (string text in new[] { null, "", "bad\ntext", new string((char)0xD800, 1), new string('x', 8193) })
				Assert.IsFalse(Announcements.TryPrepare(Empty(), false, true, 100, text, "chronicle", out _));
			foreach (string text in new[] { null, "", "bad\rtext", new string((char)0xDC00, 1), new string('x', 4097) })
				Assert.IsFalse(Announcements.TryPrepare(Empty(), false, true, 100, "notice", text, out _));
			Assert.IsTrue(Announcements.TryPrepare(Empty(), false, true, 100, "paired \U0001F600", "paired \U0001F600", out var valid));
			Assert.AreEqual(Wire(valid), Wire(Reload(valid)));
		}

		[Test]
		public void ProvedReportCannotBeRewrittenWhileUnconfirmedMessageAwaitsAcknowledgement()
		{
			var port = new Port(Prepare()) { Cut = "message:after" }; Assert.IsFalse(KingdomSubsidenceAnnouncementDriver.Resume(port, out _));
			string held = Wire(port.Book);
			Assert.IsTrue(KingdomSubsidenceReportCodec.TryDecode(Announcement(port.Book).Active.ReportModel, out var report));
			Assert.IsTrue(report.Entries[0].ChronicleProved);
			Assert.IsFalse(Announcements.TryReport(port.Book, report.With(0, report.Entries[0].WithChronicle(false)), out _));
			Assert.AreEqual(held, Wire(port.Book));
		}

		[Test]
		public void EveryAnnouncementTruncationTrailingByteAndPrefixMismatchRefuses()
		{
			string wire = Prepare().AnnouncementModel; byte[] bytes = Convert.FromBase64String(wire.Substring(4));
			for (int cut = 0; cut < bytes.Length; cut++)
				Assert.IsFalse(KingdomSubsidenceAnnouncementCodec.TryDecode("sa1:" + Convert.ToBase64String(bytes, 0, cut), out _), "cut " + cut);
			Array.Resize(ref bytes, bytes.Length + 1); Assert.IsFalse(KingdomSubsidenceAnnouncementCodec.TryDecode("sa1:" + Convert.ToBase64String(bytes), out _));
			Assert.IsFalse(KingdomSubsidenceAnnouncementCodec.TryDecode(wire + "\n", out _));
			Assert.IsFalse(KingdomSubsidenceAnnouncementCodec.TryDecode("sa2:" + wire.Substring(4), out _));
			string step = Wire(Prepare()); Assert.IsFalse(KingdomSubsidenceStepCodec.TryDecode("ss4:" + step.Substring(4), out _));
		}

		[TestCase("active")] [TestCase("before")] [TestCase("after")] [TestCase("proved")] [TestCase("notice")]
		public void NoncanonicalFlagBytesAndUnknownNoticePhaseRefuseWithoutNormalizing(string field)
		{
			byte[] bytes = Convert.FromBase64String(Prepare().AnnouncementModel.Substring(4)); int offset;
			using (var stream = new MemoryStream(bytes, false))
			using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true), true))
			{
				reader.ReadInt32(); reader.ReadString(); reader.ReadString(); reader.ReadInt64(); reader.ReadInt64();
				offset = (int)stream.Position;
				if (field != "active")
				{
					reader.ReadByte(); reader.ReadString(); reader.ReadInt64(); offset = (int)stream.Position;
					if (field != "before")
					{
						reader.ReadByte(); offset = (int)stream.Position;
						if (field != "after")
						{
							reader.ReadByte(); reader.ReadString(); reader.ReadString(); offset = (int)stream.Position;
							if (field == "notice") offset++;
						}
					}
				}
			}
			bytes[offset] = field == "notice" ? (byte)5 : (byte)2;
			Assert.IsFalse(KingdomSubsidenceAnnouncementCodec.TryDecode("sa1:" + Convert.ToBase64String(bytes), out var value));
			Assert.IsNull(value);
		}
	}
}
#endif
