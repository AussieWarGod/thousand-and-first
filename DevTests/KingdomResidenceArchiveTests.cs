#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
namespace ThousandAndFirst.Tests
{
	public partial class KingdomResidenceTests
	{
		[Test]
		public void CurrentArchiveKeepsHomeFactsAndRejectsLossyHistoricalHashProjection()
		{
			var settlement = new KingdomSettlement { City = Book() };
			ClassicAssert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(settlement, out byte[] bytes,
				out string failure), failure);
			ClassicAssert.AreEqual(20, BitConverter.ToInt32(bytes, 4));
			ClassicAssert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(bytes, out var loaded,
				out _, out failure), failure);
			CollectionAssert.AreEqual(settlement.City.ResidentResidences, loaded.City.ResidentResidences);
			ClassicAssert.AreNotSame(settlement.City.ResidentResidences, loaded.City.ResidentResidences);
			ClassicAssert.AreEqual("away", loaded.City.ResidentBoundZoneIds[0]);
			ClassicAssert.IsFalse(KingdomArchivedSettlementCodec.TryEncodeVersion(settlement, 19,
				out byte[] discarded, out failure), "old hash basis must not silently drop homes");
			ClassicAssert.IsNull(discarded);
			loaded.City.ResidentResidences[0] = "malformed";
			ClassicAssert.IsFalse(KingdomArchivedSettlementCodec.TryEncode(loaded, out _, out _));
		}

		[Test]
		public void Archive19MigratesAllResidentsAndReproducesItsOriginalHashBasis()
		{
			var settlement = new KingdomSettlement { City = Book() };
			settlement.City.ResidentResidences[0] = "";
			ClassicAssert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeVersion(settlement, 19,
				out byte[] historical, out string failure), failure);
			ClassicAssert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(historical, out var loaded,
				out _, out failure), failure);
			CollectionAssert.AreEqual(new[] { 7, 8 }, loaded.City.ResidentIds);
			CollectionAssert.AreEqual(new[] { "", "" }, loaded.City.ResidentResidences);
			CollectionAssert.AreEqual(settlement.City.ResidentHomeWorkIds, loaded.City.ResidentHomeWorkIds);
			ClassicAssert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeVersion(loaded, 19,
				out byte[] reproduced, out failure), failure);
			CollectionAssert.AreEqual(historical, reproduced);
		}

	}
}
#endif
