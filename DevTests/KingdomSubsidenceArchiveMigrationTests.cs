#if TAF_TESTS
using System;
using System.Text;
using NUnit.Framework;

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
			Assert.AreSame(source.City, restored.City);
			Assert.IsTrue(restored.City.SubsidenceReadFailed);
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryEncode(restored, out byte[] _, out string _));
			Assert.AreEqual(1234L, restored.LastSubsidenceTick);
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
			Assert.IsTrue(encoded, failure);
			Assert.AreEqual(version, BitConverter.ToInt32(historical, 4));
			StringAssert.DoesNotContain("SubsidenceModel", Encoding.UTF8.GetString(historical));
			Assert.AreEqual("ss1:new", source.City.SubsidenceModel);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(historical,
				out KingdomSettlement migrated, out int future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.AreEqual(4, migrated.City.SchemaVersion);
			Assert.AreEqual("ss1:legacy", migrated.City.SubsidenceModel);
			Assert.AreEqual(9876L, migrated.LastSubsidenceTick);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(migrated, out byte[] current, out failure), failure);
			Assert.AreEqual(19, BitConverter.ToInt32(current, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(current,
				out KingdomSettlement restored, out future, out failure), failure);
			Assert.AreEqual("ss1:legacy", restored.City.SubsidenceModel);
			Assert.AreEqual(9876L, restored.LastSubsidenceTick);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(restored, out byte[] repeated, out failure), failure);
			CollectionAssert.AreEqual(current, repeated);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("ss1:bad")]
		public void CurrentArchiveCannotPublishMissingOrMalformedStorage(string wire)
		{
			KingdomSettlement source = new KingdomSettlement { LastSubsidenceTick = 55L };
			source.City.SubsidenceModel = wire;
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryEncode(source, out byte[] _, out string _));
			Assert.AreEqual(wire, source.City.SubsidenceModel);
			Assert.AreEqual(55L, source.LastSubsidenceTick);
		}

		[Test]
		public void CurrentArchiveReaderRejectsTamperedWireWithoutLegacyFallback()
		{
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(new KingdomSettlement(),
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
			Assert.AreEqual(1, matches);
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryDecode(payload,
				out KingdomSettlement restored, out int future, out failure));
			Assert.IsNull(restored);
			Assert.AreEqual(0, future);
			StringAssert.Contains("subsidence", failure);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void CurrentArchivePreservesPendingAndQuarantinedWireExactly(bool faulted)
		{
			string wire = KingdomSubsidenceStorageMigrationTests.PendingWire(faulted);
			KingdomSettlement source = new KingdomSettlement { LastSubsidenceTick = 9876L };
			source.City.SubsidenceModel = wire;
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(source, out byte[] payload,
				out string failure), failure);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(payload, out KingdomSettlement restored,
				out int future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.AreEqual(wire, restored.City.SubsidenceModel);
			Assert.AreEqual(9876L, restored.LastSubsidenceTick);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(restored, out byte[] repeated,
				out failure), failure);
			CollectionAssert.AreEqual(payload, repeated);
		}
	}
}
#endif
