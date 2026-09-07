#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomGroomingRulesTests
	{
		private static KingdomGroomingRecord Record(int service = 0, int study = 0,
			int revision = 0)
		{
			ClassicAssert.IsTrue(KingdomGroomingRecord.TryCreate("realm:one", 7, "Çavuş",
				42L, service, study, revision, out KingdomGroomingRecord value));
			return value;
		}

		[Test]
		public void EvidenceLadderUsesAuthoredServiceAndSchoolingFacts()
		{
			ClassicAssert.AreEqual(0, KingdomGroomingRules.ServiceEvidence(false, 0));
			ClassicAssert.AreEqual(1, KingdomGroomingRules.ServiceEvidence(true, 0));
			ClassicAssert.AreEqual(2, KingdomGroomingRules.ServiceEvidence(false, 1));
			ClassicAssert.AreEqual(0, KingdomGroomingRules.StudyEvidence(false, true));
			ClassicAssert.AreEqual(1, KingdomGroomingRules.StudyEvidence(true, false));
			ClassicAssert.AreEqual(2, KingdomGroomingRules.StudyEvidence(true, true));
			ClassicAssert.IsFalse(KingdomGroomingRules.Ready(2, 1));
			ClassicAssert.IsTrue(KingdomGroomingRules.Ready(2, 2));
			StringAssert.Contains("service begun", KingdomGroomingRules.Progress(1, 0));
			StringAssert.Contains("schooling proven", KingdomGroomingRules.Progress(2, 2));
		}

		[Test]
		public void ProgressIsMonotonicBoundedAndRevisioned()
		{
			KingdomGroomingRecord start = Record(1, 0, 3);
			ClassicAssert.IsTrue(KingdomGroomingRecord.TryAdvance(start, 2, 1, out var next));
			ClassicAssert.AreEqual(2, next.ServiceMarks);
			ClassicAssert.AreEqual(1, next.StudyMarks);
			ClassicAssert.AreEqual(4, next.Revision);
			ClassicAssert.IsFalse(KingdomGroomingRecord.TryAdvance(next, 1, 0, out _));
			ClassicAssert.IsFalse(KingdomGroomingRecord.TryAdvance(next, 3, 1, out _));
			ClassicAssert.IsFalse(KingdomGroomingRecord.TryAdvance(Record(1, 1, int.MaxValue),
				2, 2, out _));
		}

		[Test]
		public void RecordCodecRoundTripsUnicodeAndRejectsNoncanonicalWire()
		{
			KingdomGroomingRecord value = Record(2, 2, 9);
			string wire = KingdomGroomingRecord.Encode(value);
			ClassicAssert.IsTrue(KingdomGroomingRecord.TryDecode(wire, out var decoded));
			ClassicAssert.AreEqual("realm:one", decoded.RealmId);
			ClassicAssert.AreEqual(7, decoded.ResidentId);
			ClassicAssert.AreEqual("Çavuş", decoded.NomineeName);
			ClassicAssert.AreEqual(42L, decoded.NominatedTick);
			ClassicAssert.IsTrue(decoded.Ready);
			ClassicAssert.AreEqual(wire, KingdomGroomingRecord.Encode(decoded));
			ClassicAssert.IsFalse(KingdomGroomingRecord.TryDecode(
				wire.Replace("|7|", "|07|"), out _));
			ClassicAssert.IsFalse(KingdomGroomingRecord.TryDecode(
				wire.Replace("v1|", "v2|"), out _));
			ClassicAssert.IsFalse(KingdomGroomingRecord.TryDecode(wire + "|tail", out _));
		}

		[Test]
		public void RecordIdentityAndAllBoundsAreStrict()
		{
			ClassicAssert.IsFalse(KingdomGroomingRecord.TryCreate("", 7, "Name", 0L,
				0, 0, 0, out _));
			ClassicAssert.IsFalse(KingdomGroomingRecord.TryCreate(new string('r', 257), 7,
				"Name", 0L, 0, 0, 0, out _));
			ClassicAssert.IsFalse(KingdomGroomingRecord.TryCreate("realm", 0, "Name", 0L,
				0, 0, 0, out _));
			ClassicAssert.IsFalse(KingdomGroomingRecord.TryCreate("realm", 7, "", 0L,
				0, 0, 0, out _));
			ClassicAssert.IsFalse(KingdomGroomingRecord.TryCreate("realm", 7,
				new string('n', 513), 0L, 0, 0, 0, out _));
			ClassicAssert.IsFalse(KingdomGroomingRecord.TryCreate("realm", 7, "Name", -1L,
				0, 0, 0, out _));
			ClassicAssert.IsFalse(KingdomGroomingRecord.TryCreate("realm", 7, "Name", 0L,
				-1, 0, 0, out _));
			ClassicAssert.IsFalse(KingdomGroomingRecord.TryCreate("realm", 7, "Name", 0L,
				0, 3, 0, out _));
			ClassicAssert.IsFalse(KingdomGroomingRecord.TryCreate("realm", 7, "Name", 0L,
				0, 0, -1, out _));
		}
	}
}
#endif
