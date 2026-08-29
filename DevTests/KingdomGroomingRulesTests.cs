#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomGroomingRulesTests
	{
		private static KingdomGroomingRecord Record(int service = 0, int study = 0,
			int revision = 0)
		{
			Assert.IsTrue(KingdomGroomingRecord.TryCreate("realm:one", 7, "Çavuş",
				42L, service, study, revision, out KingdomGroomingRecord value));
			return value;
		}

		[Test]
		public void EvidenceLadderUsesAuthoredServiceAndSchoolingFacts()
		{
			Assert.AreEqual(0, KingdomGroomingRules.ServiceEvidence(false, 0));
			Assert.AreEqual(1, KingdomGroomingRules.ServiceEvidence(true, 0));
			Assert.AreEqual(2, KingdomGroomingRules.ServiceEvidence(false, 1));
			Assert.AreEqual(0, KingdomGroomingRules.StudyEvidence(false, true));
			Assert.AreEqual(1, KingdomGroomingRules.StudyEvidence(true, false));
			Assert.AreEqual(2, KingdomGroomingRules.StudyEvidence(true, true));
			Assert.IsFalse(KingdomGroomingRules.Ready(2, 1));
			Assert.IsTrue(KingdomGroomingRules.Ready(2, 2));
			StringAssert.Contains("service begun", KingdomGroomingRules.Progress(1, 0));
			StringAssert.Contains("schooling proven", KingdomGroomingRules.Progress(2, 2));
		}

		[Test]
		public void ProgressIsMonotonicBoundedAndRevisioned()
		{
			KingdomGroomingRecord start = Record(1, 0, 3);
			Assert.IsTrue(KingdomGroomingRecord.TryAdvance(start, 2, 1, out var next));
			Assert.AreEqual(2, next.ServiceMarks);
			Assert.AreEqual(1, next.StudyMarks);
			Assert.AreEqual(4, next.Revision);
			Assert.IsFalse(KingdomGroomingRecord.TryAdvance(next, 1, 0, out _));
			Assert.IsFalse(KingdomGroomingRecord.TryAdvance(next, 3, 1, out _));
			Assert.IsFalse(KingdomGroomingRecord.TryAdvance(Record(1, 1, int.MaxValue),
				2, 2, out _));
		}

		[Test]
		public void RecordCodecRoundTripsUnicodeAndRejectsNoncanonicalWire()
		{
			KingdomGroomingRecord value = Record(2, 2, 9);
			string wire = KingdomGroomingRecord.Encode(value);
			Assert.IsTrue(KingdomGroomingRecord.TryDecode(wire, out var decoded));
			Assert.AreEqual("realm:one", decoded.RealmId);
			Assert.AreEqual(7, decoded.ResidentId);
			Assert.AreEqual("Çavuş", decoded.NomineeName);
			Assert.AreEqual(42L, decoded.NominatedTick);
			Assert.IsTrue(decoded.Ready);
			Assert.AreEqual(wire, KingdomGroomingRecord.Encode(decoded));
			Assert.IsFalse(KingdomGroomingRecord.TryDecode(
				wire.Replace("|7|", "|07|"), out _));
			Assert.IsFalse(KingdomGroomingRecord.TryDecode(
				wire.Replace("v1|", "v2|"), out _));
			Assert.IsFalse(KingdomGroomingRecord.TryDecode(wire + "|tail", out _));
		}

		[Test]
		public void RecordIdentityAndAllBoundsAreStrict()
		{
			Assert.IsFalse(KingdomGroomingRecord.TryCreate("", 7, "Name", 0L,
				0, 0, 0, out _));
			Assert.IsFalse(KingdomGroomingRecord.TryCreate(new string('r', 257), 7,
				"Name", 0L, 0, 0, 0, out _));
			Assert.IsFalse(KingdomGroomingRecord.TryCreate("realm", 0, "Name", 0L,
				0, 0, 0, out _));
			Assert.IsFalse(KingdomGroomingRecord.TryCreate("realm", 7, "", 0L,
				0, 0, 0, out _));
			Assert.IsFalse(KingdomGroomingRecord.TryCreate("realm", 7,
				new string('n', 513), 0L, 0, 0, 0, out _));
			Assert.IsFalse(KingdomGroomingRecord.TryCreate("realm", 7, "Name", -1L,
				0, 0, 0, out _));
			Assert.IsFalse(KingdomGroomingRecord.TryCreate("realm", 7, "Name", 0L,
				-1, 0, 0, out _));
			Assert.IsFalse(KingdomGroomingRecord.TryCreate("realm", 7, "Name", 0L,
				0, 3, 0, out _));
			Assert.IsFalse(KingdomGroomingRecord.TryCreate("realm", 7, "Name", 0L,
				0, 0, -1, out _));
		}
	}
}
#endif
