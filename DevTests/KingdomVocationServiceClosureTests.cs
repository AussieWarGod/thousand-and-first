#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomVocationServiceClosureTests
	{
		private const string Realm =
			"taf:realm:v1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

		[Test]
		public void ThreeExplicitVerbsCreateTruthfulZeroValueReceipts()
		{
			string[] vocations = { "waystation", "refuge", "reliquary" };
			string[] verbs = { "Ask for a route brief", "Read a shelter title",
				"Request a provenance reading" };
			for (int i = 0; i < vocations.Length; i++)
			{
				KingdomVocationServiceOffer offer = Offer(vocations[i], i);
				Assert.AreEqual(KingdomVocationServiceOfferState.Available, offer.State);
				Assert.AreEqual(verbs[i], offer.Verb);
				StringAssert.Contains("C18", offer.Sink);
				StringAssert.Contains("once per exact source receipt", offer.Cadence);
				StringAssert.Contains("no item", offer.Closure);
				KingdomVocationServiceBook book = new KingdomVocationServiceBook();
				KingdomVocationServiceReceipt receipt = Append(book, offer, 10L);
				Assert.AreEqual(0, receipt.Request.InputUnits);
				Assert.AreEqual(0, receipt.OutputUnits);
				StringAssert.Contains(receipt.Request.SourceReceiptId, receipt.OutputText);
				StringAssert.Contains(receipt.Request.SinkReceiptId, receipt.OutputText);
				StringAssert.Contains("Occurrence 1/16", receipt.OutputText);
				StringAssert.Contains("useful result", receipt.OutputText);
				StringAssert.Contains("no passive effect", receipt.OutputText);
			}
		}

		[Test]
		public void HoldingMissingAndStaleSourcesOpenNoOperation()
		{
			Assert.IsTrue(KingdomVocationServiceRules.TryBuildHoldingReport(
				"taf:settlement:seat", out KingdomVocationServiceOffer holding,
				out string failure), failure);
			Assert.AreEqual(KingdomVocationServiceOfferState.Neutral, holding.State);
			Assert.IsNull(holding.Verb);
			Assert.IsFalse(KingdomVocationServiceRules.TryPrepareRequest(
				new KingdomVocationServiceBook(), holding, 1L,
				out KingdomVocationServiceRequest _, out failure));
			Assert.IsTrue(KingdomVocationServiceRules.TryBuildUnavailable(
				"taf:settlement:seat", "refuge", "no completed shelter receipt",
				"complete an exact shelter", out KingdomVocationServiceOffer unavailable,
				out failure), failure);
			Assert.IsFalse(KingdomVocationServiceRules.TryPrepareRequest(
				new KingdomVocationServiceBook(), unavailable, 1L,
				out KingdomVocationServiceRequest _, out failure));
			KingdomVocationServiceOffer opened = Offer("waystation", 1);
			KingdomVocationServiceOffer moved = Offer("waystation", 2);
			Assert.IsFalse(KingdomVocationServiceRules.TryMatchAvailableOffers(
				opened, moved, out failure));
			StringAssert.Contains("changed", failure);
		}

		[Test]
		public void ExactRetryAndNewSourcesAdvanceCadenceWithoutEviction()
		{
			Assert.AreEqual(3 * KingdomVocationServiceRules.MaxRowsPerSeries,
				KingdomVocationServiceRules.MaxRows);
			KingdomVocationServiceBook book = new KingdomVocationServiceBook();
			KingdomVocationServiceOffer first = Offer("waystation", 0);
			KingdomVocationServiceReceipt original = Append(book, first, 10L);
			Assert.IsTrue(KingdomVocationServiceRules.TryPrepareRequest(book, first, 99L,
				out KingdomVocationServiceRequest retryRequest, out string failure), failure);
			Assert.AreEqual(10L, retryRequest.RequestedTick);
			Assert.IsTrue(KingdomVocationServiceRules.TryServe(book, 0L, retryRequest, 99L,
				out KingdomVocationServiceReceipt retry, out failure), failure);
			Assert.AreSame(original, retry);
			Assert.AreEqual(1L, book.Revision);
			for (int i = 1; i < KingdomVocationServiceRules.MaxRowsPerSeries; i++)
			{
				KingdomVocationServiceReceipt row = Append(book, Offer("waystation", i), 10L + i);
				Assert.AreEqual(i, row.Request.CadenceOrdinal);
			}
			Assert.IsFalse(KingdomVocationServiceRules.TryPrepareRequest(book,
				Offer("waystation", 16), 100L, out KingdomVocationServiceRequest _, out failure));
			StringAssert.Contains("city and vocation", failure);
			Assert.IsTrue(KingdomVocationServiceRules.TryInspect(book,
				Offer("waystation", 16), out KingdomVocationServiceStatus seriesClosed,
				out failure), failure);
			Assert.AreEqual(KingdomVocationServiceActionState.CapacityClosed, seriesClosed.State);
			Assert.AreEqual(16, seriesClosed.SeriesCount);
			Assert.AreEqual(16, seriesClosed.RealmCount);
			for (int i = 0; i < KingdomVocationServiceRules.MaxRowsPerSeries; i++)
			{
				Assert.AreEqual(i, Append(book, Offer("refuge", i,
					"taf:settlement:second"), 30L + i).Request.CadenceOrdinal);
				Assert.AreEqual(i, Append(book, Offer("reliquary", i,
					"taf:settlement:third"), 50L + i).Request.CadenceOrdinal);
			}
			string[] retained = new string[book.Rows.Count];
			for (int i = 0; i < book.Rows.Count; i++) retained[i] = book.Rows[i].ServiceId;
			Assert.IsFalse(KingdomVocationServiceRules.TryPrepareRequest(book,
				Offer("waystation", 0, "taf:settlement:fourth"), 100L,
				out KingdomVocationServiceRequest _, out failure));
			StringAssert.Contains("realm", failure);
			Assert.IsTrue(KingdomVocationServiceRules.TryInspect(book,
				Offer("waystation", 0, "taf:settlement:fourth"),
				out KingdomVocationServiceStatus realmClosed, out failure), failure);
			Assert.AreEqual(KingdomVocationServiceActionState.CapacityClosed, realmClosed.State);
			Assert.AreEqual(0, realmClosed.SeriesCount);
			Assert.AreEqual(48, realmClosed.RealmCount);
			Assert.AreEqual(KingdomVocationServiceRules.MaxRows, book.Rows.Count);
			for (int i = 0; i < book.Rows.Count; i++) Assert.AreEqual(retained[i], book.Rows[i].ServiceId);
			List<string> pages = new List<string>();
			int offset = 0;
			do
			{
				Assert.IsTrue(KingdomVocationServiceRules.TryDescribeRealmResults(book, offset,
					out string page, out int next, out failure), failure);
				pages.Add(page); offset = next;
			}
			while (offset >= 0);
			Assert.Greater(pages.Count, 1);
			string allPages = string.Join("\n", pages.ToArray());
			for (int i = 0; i < book.Rows.Count; i++)
				StringAssert.Contains(book.Rows[i].Request.ResultText, allPages);
		}

		[Test]
		public void ExactSourceDedupeIsScopedAndReadViewsExposeOnlyDurableResults()
		{
			KingdomVocationServiceBook book = new KingdomVocationServiceBook();
			KingdomVocationServiceOffer first = OfferExact("waystation", "taf:settlement:first",
				"taf:source:shared", "safe route authority", "route result first");
			KingdomVocationServiceOffer second = OfferExact("waystation", "taf:settlement:second",
				"taf:source:shared", "safe route authority", "route result second");
			Assert.IsFalse(first.Report.Contains(first.ResultText),
				"pre-choice report must not disclose actionable result");
			Append(book, first, 10L);
			Assert.IsTrue(KingdomVocationServiceRules.TryInspect(book, first,
				out KingdomVocationServiceStatus recorded, out string failure), failure);
			Assert.AreEqual(KingdomVocationServiceActionState.AlreadyRecorded, recorded.State);
			Assert.AreEqual(1, recorded.SeriesCount);
			Assert.AreEqual(1, recorded.RealmCount);
			StringAssert.Contains("route result first", recorded.ExistingReceiptText);

			Assert.IsTrue(KingdomVocationServiceRules.TryInspect(book, second,
				out KingdomVocationServiceStatus available, out failure), failure);
			Assert.AreEqual(KingdomVocationServiceActionState.Available, available.State);
			Assert.AreEqual(0, available.SeriesCount);
			Assert.AreEqual(1, available.RealmCount);
			Append(book, second, 11L);
			Assert.AreEqual(2, book.Rows.Count,
				"same source id in another settlement is a distinct service");
			Assert.IsTrue(KingdomVocationServiceRules.TryDescribeRealmResults(book,
				out string results, out failure), failure);
			StringAssert.Contains(
				"taf:settlement:first / waystation / taf:source:shared: route result first", results);
			StringAssert.Contains(
				"taf:settlement:second / waystation / taf:source:shared: route result second", results);
		}

		[Test]
		public void StaleRevisionOverflowAndMalformedRequestsLeaveBookUnchanged()
		{
			KingdomVocationServiceBook book = new KingdomVocationServiceBook();
			KingdomVocationServiceOffer staleOffer = Offer("waystation", 1);
			Assert.IsTrue(KingdomVocationServiceRules.TryPrepareRequest(book, staleOffer, 10L,
				out KingdomVocationServiceRequest stale, out string failure), failure);
			Append(book, Offer("refuge", 2), 10L);
			Assert.IsFalse(KingdomVocationServiceRules.TryServe(book, 0L, stale, 10L,
				out KingdomVocationServiceReceipt _, out failure));
			StringAssert.Contains("revision", failure);
			Assert.AreEqual(1, book.Rows.Count);

			KingdomVocationServiceBook overflow = new KingdomVocationServiceBook
				{ Revision = long.MaxValue };
			Assert.IsTrue(KingdomVocationServiceRules.TryPrepareRequest(overflow,
				Offer("reliquary", 3), 10L, out KingdomVocationServiceRequest request,
				out failure), failure);
			Assert.IsFalse(KingdomVocationServiceRules.TryServe(overflow, long.MaxValue,
				request, 10L, out KingdomVocationServiceReceipt _, out failure));
			StringAssert.Contains("cannot advance", failure);
			Assert.AreEqual(0, overflow.Rows.Count);
			request.InputUnits = 1;
			Assert.IsFalse(KingdomVocationServiceRules.TryServe(new KingdomVocationServiceBook(),
				0L, request, 10L, out KingdomVocationServiceReceipt _, out failure));
		}

		[Test]
		public void CurrentCodecRoundTripsAndCorruptionOrFutureFailsClosed()
		{
			KingdomCivicPracticeEnvelope envelope = new KingdomCivicPracticeEnvelope();
			Assert.IsTrue(envelope.TryBindEmptyIdentity(Realm, out string failure), failure);
			Append(envelope.VocationServices, Offer("reliquary", 7), 30L);
			byte[] bytes = KingdomCivicPracticeCodec.Encode(envelope);
			Assert.AreEqual(4, BitConverter.ToInt32(bytes, 4));
			KingdomCivicPracticeEnvelope loaded = KingdomCivicPracticeCodec.Decode(bytes);
			Assert.AreEqual(1, loaded.VocationServices.Rows.Count);
			Assert.AreEqual(0, loaded.VocationServices.Rows[0].OutputUnits);
			CollectionAssert.AreEqual(bytes, KingdomCivicPracticeCodec.Encode(loaded));
			byte[] corrupt = (byte[])bytes.Clone(); corrupt[corrupt.Length - 1] ^= 1;
			Assert.IsTrue(KingdomCivicPracticeStore.ReadForRealm(corrupt, Realm,
				out failure).Quarantined);
			KingdomCivicPracticeEnvelope future = new KingdomCivicPracticeEnvelope
			{
				OpaqueFutureVersion = KingdomCivicPracticeCodec.CurrentWireVersion + 1,
				OpaqueFuturePayload = new byte[] { 4, 3, 2, 1 }
			};
			byte[] futureBytes = KingdomCivicPracticeCodec.Encode(future);
			KingdomCivicPracticeEnvelope opaque = KingdomCivicPracticeCodec.Decode(futureBytes);
			Assert.IsTrue(opaque.IsOpaqueFuture);
			CollectionAssert.AreEqual(futureBytes, KingdomCivicPracticeCodec.Encode(opaque));
		}

		private static KingdomVocationServiceReceipt Append(KingdomVocationServiceBook book,
			KingdomVocationServiceOffer offer, long tick)
		{
			Assert.IsTrue(KingdomVocationServiceRules.TryPrepareRequest(book, offer, tick,
				out KingdomVocationServiceRequest request, out string failure), failure);
			Assert.IsTrue(KingdomVocationServiceRules.TryServe(book, book.Revision, request,
				tick, out KingdomVocationServiceReceipt receipt, out failure), failure);
			return receipt;
		}

		private static KingdomVocationServiceOffer Offer(string vocation, int ordinal,
			string settlement = "taf:settlement:seat")
		{
			KingdomVocationServiceKind kind = KingdomVocationServiceRules.KindFor(vocation);
			KingdomVocationServiceAuthority authority = kind == KingdomVocationServiceKind.RouteBrief
				? KingdomVocationServiceAuthority.PolityRoute :
				kind == KingdomVocationServiceKind.SanctuaryTitle
					? KingdomVocationServiceAuthority.BuiltShelter :
					KingdomVocationServiceAuthority.ArtifactRecognition;
			KingdomVocationServiceSource source = new KingdomVocationServiceSource(
				settlement, vocation, kind, authority,
				"taf:source:" + vocation + ":" + ordinal, "exact source " + ordinal,
				"useful result " + vocation + " " + ordinal);
			Assert.IsTrue(KingdomVocationServiceRules.TryBuildAvailableOffer(source,
				out KingdomVocationServiceOffer offer, out string failure), failure);
			return offer;
		}

		private static KingdomVocationServiceOffer OfferExact(string vocation, string settlement,
			string receipt, string description, string result)
		{
			KingdomVocationServiceKind kind = KingdomVocationServiceRules.KindFor(vocation);
			KingdomVocationServiceSource source = new KingdomVocationServiceSource(
				settlement, vocation, kind, KingdomVocationServiceAuthority.PolityRoute,
				receipt, description, result);
			Assert.IsTrue(KingdomVocationServiceRules.TryBuildAvailableOffer(source,
				out KingdomVocationServiceOffer offer, out string failure), failure);
			return offer;
		}
	}
}
#endif
