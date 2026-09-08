#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.IsTrue(KingdomSubsidenceReportRules.TryArmLedger(report, 0, new string[0], out report));
			if (loss) ClassicAssert.IsTrue(KingdomSubsidenceReportRules.TryLoseChronicle(report, 0, out report));
			else ClassicAssert.IsTrue(KingdomSubsidenceReportRules.TryProveChronicle(report, 0, out report));
			ClassicAssert.IsTrue(KingdomSubsidenceReportCodec.TryEncode(report, out string wire)); return wire;
		}

		[Test]
		public void FailedTellingSurvivesBookCopiesCodecAndExplicitReadAcknowledgement()
		{
			KingdomSubsidenceStepBook before = Book();
			string report = Report(1);
			ClassicAssert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(before, report, out KingdomSubsidenceStepBook saved));
			ClassicAssert.AreEqual(KingdomSubsidenceReportArchive.None, before.FailureModel);
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(saved, out string wire));
			ClassicAssert.IsTrue(wire.StartsWith("ss5:", StringComparison.Ordinal));
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook restored));
			ClassicAssert.AreEqual(saved.FailureModel, restored.FailureModel);
			ClassicAssert.IsTrue(KingdomSubsidenceReportArchive.TryRead(restored.FailureModel, out var rows));
			CollectionAssert.AreEqual(new[] { report }, rows);
			ClassicAssert.AreEqual(saved.FailureModel, restored.With(null, restored.Sequence).FailureModel);
			ClassicAssert.AreEqual(saved.FailureModel, restored.WithOption(restored.OptionModel).FailureModel);
			ClassicAssert.AreEqual(saved.FailureModel, restored.WithBatch(restored.BatchModel).FailureModel);
			StringAssert.Contains("delivery is not claimed", KingdomSubsidenceReportArchive.Digest(restored));
			StringAssert.Contains("A lost wall.", KingdomSubsidenceReportArchive.Digest(restored));
			StringAssert.Contains("Chronicle", KingdomSubsidenceReportArchive.Digest(restored));
			KingdomSubsidenceStepBook acknowledged = restored.WithFailures(KingdomSubsidenceReportArchive.None);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.Valid(acknowledged));
			ClassicAssert.AreEqual(restored.Sequence, acknowledged.Sequence);
			ClassicAssert.AreEqual(restored.LastRetiredTick, acknowledged.LastRetiredTick);
			ClassicAssert.AreEqual("", KingdomSubsidenceReportArchive.Digest(acknowledged));
			ClassicAssert.AreNotEqual(acknowledged.FailureModel, restored.FailureModel);
		}

		[Test]
		public void FullArchiveRefusesNewFailureWithoutEvictingUnreadEvidence()
		{
			KingdomSubsidenceStepBook book = Book();
			for (int i = 1; i <= KingdomSubsidenceReportArchive.MaxReports; i++)
				ClassicAssert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(book, Report(i), out book));
			string before = book.FailureModel;
			ClassicAssert.IsFalse(KingdomSubsidenceReportArchive.TryRetain(book, Report(9), out var refused));
			ClassicAssert.IsNull(refused); ClassicAssert.AreEqual(before, book.FailureModel);
			ClassicAssert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(book, Report(1), out var repeat));
			ClassicAssert.AreSame(book, repeat);
			ClassicAssert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(book, Report(10, false), out repeat));
			ClassicAssert.AreSame(book, repeat, "fully delivered reports need no failure storage");
			ClassicAssert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(book.WithFailures(KingdomSubsidenceReportArchive.None),
				Report(9), out _), "reading retained failures gives future failures space without changing the clock");
		}

		[TestCase(null)] [TestCase("")] [TestCase("sf1:")] [TestCase("sf2:none")]
		[TestCase("sf1:none ")]
		public void MalformedArchiveNeverDefaultsToAnEmptyAcknowledgedHistory(string wire)
		{
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.Valid(Book().WithFailures(wire)));
			ClassicAssert.IsFalse(KingdomSubsidenceReportArchive.TryRead(wire, out var rows)); ClassicAssert.IsNull(rows);
		}

		[Test]
		public void ForeignFailureAndChangedRepeatCannotReplaceOwnedEvidence()
		{
			ClassicAssert.IsFalse(KingdomSubsidenceReportArchive.TryRetain(Book(), Report(1, true,
				KingdomIdentityRules.SettlementPrefix + new string('c', 64)), out _));
			ClassicAssert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(Book(), Report(1), out var saved));
			ClassicAssert.IsTrue(KingdomSubsidenceReportCodec.TryDecode(Report(1), out var original));
			var changed = new KingdomSubsidenceReportPlan(original.OwnerId, Realm, Settlement,
				new[] { new KingdomSubsidenceReportEntry("Different event.", "", 100000) });
			ClassicAssert.IsTrue(KingdomSubsidenceReportRules.TryArmLedger(changed, 0, new string[0], out changed));
			ClassicAssert.IsTrue(KingdomSubsidenceReportRules.TryLoseChronicle(changed, 0, out changed));
			ClassicAssert.IsTrue(KingdomSubsidenceReportCodec.TryEncode(changed, out string wire));
			ClassicAssert.IsFalse(KingdomSubsidenceReportArchive.TryRetain(saved, wire, out _));
		}

		[Test]
		public void EveryTruncationTrailingByteAndForeignOwnerArchiveRefuses()
		{
			ClassicAssert.IsTrue(KingdomSubsidenceReportArchive.TryRetain(Book(), Report(1), out var saved));
			byte[] bytes = Convert.FromBase64String(saved.FailureModel.Substring(4));
			for (int i = 0; i < bytes.Length; i++)
				ClassicAssert.IsFalse(KingdomSubsidenceReportArchive.TryRead("sf1:" + Convert.ToBase64String(bytes, 0, i), out _));
			Array.Resize(ref bytes, bytes.Length + 1);
			ClassicAssert.IsFalse(KingdomSubsidenceReportArchive.TryRead("sf1:" + Convert.ToBase64String(bytes), out _));
			var foreign = new KingdomSubsidenceStepBook(saved.Admission, saved.RealmId,
				KingdomIdentityRules.SettlementPrefix + new string('c', 64), saved.Sequence, null,
				saved.LastRetiredTick, failureModel: saved.FailureModel);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.Valid(foreign));
		}
	}
}
#endif
