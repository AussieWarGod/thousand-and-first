#if TAF_TESTS
using System;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	public class KingdomSubsidenceArchiveMigrationTests
	{
		[TestCase(false)]
		[TestCase(true)]
		public void InterruptedNamedReadCannotBeRepublishedAsHealthyArchive(bool afterFields)
		{
			KingdomSettlement source = new KingdomSettlement { LastSubsidenceTick = 1234L };
			Assert.Throws<InvalidOperationException>(() => source.City.ReadNamedState(() =>
			{
				if (afterFields) { source.City.SchemaVersion = 4; source.City.SubsidenceModel = "ss1:new"; }
				throw new InvalidOperationException("interrupted named reader");
			}));
			KingdomSettlement captured = new KingdomSettlement();
			captured.ReadFrom(source);
			KingdomSettlement restored = new KingdomSettlement();
			captured.WriteTo(restored);
			ClassicAssert.AreSame(source.City, restored.City);
			ClassicAssert.IsTrue(restored.City.SubsidenceReadFailed);
			ClassicAssert.IsFalse(KingdomArchivedSettlementCodec.TryEncode(restored, out byte[] _, out string _));
			ClassicAssert.AreEqual(1234L, restored.LastSubsidenceTick);
		}

		[TestCase(17)]
		[TestCase(18)]
		public void HistoricalArchiveOmitsNewFieldAndPreservesCheckpointThroughCurrentRoundTrip(int version)
		{
			KingdomSettlement source = new KingdomSettlement { LastSubsidenceTick = 9876L };
			byte[] historical; string failure;
			bool encoded = version == 17
				? KingdomArchivedSettlementCodec.TryEncodeArrivalCadenceV17ForTests(source, out historical, out failure)
				: KingdomArchivedSettlementCodec.TryEncodeExpeditionResultV18ForTests(source, out historical, out failure);
			ClassicAssert.IsTrue(encoded, failure);
			ClassicAssert.AreEqual(version, BitConverter.ToInt32(historical, 4));
			StringAssert.DoesNotContain("SubsidenceModel", Encoding.UTF8.GetString(historical));
			ClassicAssert.AreEqual("ss1:new", source.City.SubsidenceModel);
			ClassicAssert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(historical,
				out KingdomSettlement migrated, out int future, out failure), failure);
			ClassicAssert.AreEqual(0, future);
			ClassicAssert.AreEqual(4, migrated.City.SchemaVersion);
			ClassicAssert.AreEqual("ss1:legacy", migrated.City.SubsidenceModel);
			ClassicAssert.AreEqual(9876L, migrated.LastSubsidenceTick);
			ClassicAssert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(migrated, out byte[] current, out failure), failure);
			ClassicAssert.AreEqual(19, BitConverter.ToInt32(current, 4));
			ClassicAssert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(current,
				out KingdomSettlement restored, out future, out failure), failure);
			ClassicAssert.AreEqual("ss1:legacy", restored.City.SubsidenceModel);
			ClassicAssert.AreEqual(9876L, restored.LastSubsidenceTick);
			ClassicAssert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(restored, out byte[] repeated, out failure), failure);
			CollectionAssert.AreEqual(current, repeated);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("ss1:bad")]
		public void CurrentArchiveCannotPublishMissingOrMalformedStorage(string wire)
		{
			KingdomSettlement source = new KingdomSettlement { LastSubsidenceTick = 55L };
			source.City.SubsidenceModel = wire;
			ClassicAssert.IsFalse(KingdomArchivedSettlementCodec.TryEncode(source, out byte[] _, out string _));
			ClassicAssert.AreEqual(wire, source.City.SubsidenceModel);
			ClassicAssert.AreEqual(55L, source.LastSubsidenceTick);
		}

		[Test]
		public void CurrentArchiveReaderRejectsTamperedWireWithoutLegacyFallback()
		{
			ClassicAssert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(new KingdomSettlement(),
				out byte[] payload, out string failure), failure);
			byte[] expected = Encoding.UTF8.GetBytes("ss1:new");
			int matches = 0;
			for (int i = 0; i <= payload.Length - expected.Length; i++)
			{
				bool same = true;
				for (int j = 0; j < expected.Length; j++) same &= payload[i + j] == expected[j];
				if (!same) continue;
				matches++;
				payload[i + 4] = (byte)'b'; payload[i + 5] = (byte)'a'; payload[i + 6] = (byte)'d';
			}
			ClassicAssert.AreEqual(1, matches);
			ClassicAssert.IsFalse(KingdomArchivedSettlementCodec.TryDecode(payload,
				out KingdomSettlement restored, out int future, out failure));
			ClassicAssert.IsNull(restored);
			ClassicAssert.AreEqual(0, future);
			StringAssert.Contains("subsidence", failure);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void CurrentArchivePreservesPendingAndQuarantinedWireExactly(bool faulted)
		{
			string wire = KingdomSubsidenceStorageMigrationTests.PendingWire(faulted);
			KingdomSettlement source = new KingdomSettlement { LastSubsidenceTick = 9876L };
			source.City.SubsidenceModel = wire;
			ClassicAssert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(source, out byte[] payload,
				out string failure), failure);
			ClassicAssert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(payload, out KingdomSettlement restored,
				out int future, out failure), failure);
			ClassicAssert.AreEqual(0, future);
			ClassicAssert.AreEqual(wire, restored.City.SubsidenceModel);
			ClassicAssert.AreEqual(9876L, restored.LastSubsidenceTick);
			ClassicAssert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(restored, out byte[] repeated,
				out failure), failure);
			CollectionAssert.AreEqual(payload, repeated);
		}
	}
}
#endif
