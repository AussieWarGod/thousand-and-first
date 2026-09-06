#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomSubsidenceReportArchiveTests
	{
		private static readonly string Realm = KingdomIdentityRules.RealmPrefix + new string('a', 64);
		private static readonly string Settlement = KingdomIdentityRules.SettlementPrefix + new string('b', 64);
		private static KingdomSubsidenceStepBook Book()
		{
			return new KingdomSubsidenceStepBook(KingdomSubsidenceAdmission.Admitted, Realm, Settlement, 1,
				null, 100000);
		}
		private static string Report(int id, bool loss = true, string settlement = null)
		{
			KingdomSubsidenceReportPlan report = new KingdomSubsidenceReportPlan(
				"taf:subsidence-step:v1:" + id.ToString("x64"), Realm, settlement ?? Settlement,
				new[] { new KingdomSubsidenceReportEntry("A lost wall.", "", 100000) });
			Assert.IsTrue(KingdomSubsidenceReportRules.TryArmLedger(report, 0, new string[0], out report));
			if (loss) Assert.IsTrue(KingdomSubsidenceReportRules.TryLoseChronicle(report, 0, out report));
			else Assert.IsTrue(KingdomSubsidenceReportRules.TryProveChronicle(report, 0, out report));
			Assert.IsTrue(KingdomSubsidenceReportCodec.TryEncode(report, out string wire)); return wire;
		}

		[Test]
		public void FailedTellingSurvivesBookCopiesCodecAndExplicitReadAcknowledgement()
		{
			KingdomSubsidenceStepBook before = Book();
			string report = Report(1);
			Assert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(before, report, out KingdomSubsidenceStepBook saved));
			Assert.AreEqual(KingdomSubsidenceReportArchive.None, before.FailureModel);
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(saved, out string wire));
			Assert.IsTrue(wire.StartsWith("ss5:", StringComparison.Ordinal));
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook restored));
			Assert.AreEqual(saved.FailureModel, restored.FailureModel);
			Assert.IsTrue(KingdomSubsidenceReportArchive.TryRead(restored.FailureModel, out var rows));
			CollectionAssert.AreEqual(new[] { report }, rows);
			Assert.AreEqual(saved.FailureModel, restored.With(null, restored.Sequence).FailureModel);
			Assert.AreEqual(saved.FailureModel, restored.WithOption(restored.OptionModel).FailureModel);
			Assert.AreEqual(saved.FailureModel, restored.WithBatch(restored.BatchModel).FailureModel);
			StringAssert.Contains("delivery is not claimed", KingdomSubsidenceReportArchive.Digest(restored));
			StringAssert.Contains("A lost wall.", KingdomSubsidenceReportArchive.Digest(restored));
			StringAssert.Contains("Chronicle", KingdomSubsidenceReportArchive.Digest(restored));
			KingdomSubsidenceStepBook acknowledged = restored.WithFailures(KingdomSubsidenceReportArchive.None);
			Assert.IsTrue(KingdomSubsidenceStepRules.Valid(acknowledged));
			Assert.AreEqual(restored.Sequence, acknowledged.Sequence);
			Assert.AreEqual(restored.LastRetiredTick, acknowledged.LastRetiredTick);
			Assert.AreEqual("", KingdomSubsidenceReportArchive.Digest(acknowledged));
			Assert.AreNotEqual(acknowledged.FailureModel, restored.FailureModel);
		}

		[Test]
		public void FullArchiveRefusesNewFailureWithoutEvictingUnreadEvidence()
		{
			KingdomSubsidenceStepBook book = Book();
			for (int i = 1; i <= KingdomSubsidenceReportArchive.MaxReports; i++)
				Assert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(book, Report(i), out book));
			string before = book.FailureModel;
			Assert.IsFalse(KingdomSubsidenceReportArchive.TryRetain(book, Report(9), out var refused));
			Assert.IsNull(refused); Assert.AreEqual(before, book.FailureModel);
			Assert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(book, Report(1), out var repeat));
			Assert.AreSame(book, repeat);
			Assert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(book, Report(10, false), out repeat));
			Assert.AreSame(book, repeat, "fully delivered reports need no failure storage");
			Assert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(book.WithFailures(KingdomSubsidenceReportArchive.None),
				Report(9), out _), "reading retained failures gives future failures space without changing the clock");
		}

		[TestCase(null)] [TestCase("")] [TestCase("sf1:")] [TestCase("sf2:none")]
		[TestCase("sf1:none ")]
		public void MalformedArchiveNeverDefaultsToAnEmptyAcknowledgedHistory(string wire)
		{
			Assert.IsFalse(KingdomSubsidenceStepRules.Valid(Book().WithFailures(wire)));
			Assert.IsFalse(KingdomSubsidenceReportArchive.TryRead(wire, out var rows)); Assert.IsNull(rows);
		}

		[Test]
		public void ForeignFailureAndChangedRepeatCannotReplaceOwnedEvidence()
		{
			Assert.IsFalse(KingdomSubsidenceReportArchive.TryRetain(Book(), Report(1, true,
				KingdomIdentityRules.SettlementPrefix + new string('c', 64)), out _));
			Assert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(Book(), Report(1), out var saved));
			Assert.IsTrue(KingdomSubsidenceReportCodec.TryDecode(Report(1), out var original));
			var changed = new KingdomSubsidenceReportPlan(original.OwnerId, Realm, Settlement,
				new[] { new KingdomSubsidenceReportEntry("Different event.", "", 100000) });
			Assert.IsTrue(KingdomSubsidenceReportRules.TryArmLedger(changed, 0, new string[0], out changed));
			Assert.IsTrue(KingdomSubsidenceReportRules.TryLoseChronicle(changed, 0, out changed));
			Assert.IsTrue(KingdomSubsidenceReportCodec.TryEncode(changed, out string wire));
			Assert.IsFalse(KingdomSubsidenceReportArchive.TryRetain(saved, wire, out _));
		}

		[Test]
		public void EveryTruncationTrailingByteAndForeignOwnerArchiveRefuses()
		{
			Assert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(Book(), Report(1), out var saved));
			byte[] bytes = Convert.FromBase64String(saved.FailureModel.Substring(4));
			for (int i = 0; i < bytes.Length; i++)
				Assert.IsFalse(KingdomSubsidenceReportArchive.TryRead("sf1:" + Convert.ToBase64String(bytes, 0, i), out _));
			Array.Resize(ref bytes, bytes.Length + 1);
			Assert.IsFalse(KingdomSubsidenceReportArchive.TryRead("sf1:" + Convert.ToBase64String(bytes), out _));
			var foreign = new KingdomSubsidenceStepBook(saved.Admission, saved.RealmId,
				KingdomIdentityRules.SettlementPrefix + new string('c', 64), saved.Sequence, null,
				saved.LastRetiredTick, failureModel: saved.FailureModel);
			Assert.IsFalse(KingdomSubsidenceStepRules.Valid(foreign));
		}
	}
}
#endif
