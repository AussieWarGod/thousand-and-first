#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
				ClassicAssert.AreEqual(KingdomVocationServiceOfferState.Available, offer.State);
				ClassicAssert.AreEqual(verbs[i], offer.Verb);
				StringAssert.Contains("C18", offer.Sink);
				StringAssert.Contains("once per exact source receipt", offer.Cadence);
				StringAssert.Contains("no item", offer.Closure);
				KingdomVocationServiceBook book = new KingdomVocationServiceBook();
				KingdomVocationServiceReceipt receipt = Append(book, offer, 10L);
				ClassicAssert.AreEqual(0, receipt.Request.InputUnits);
				ClassicAssert.AreEqual(0, receipt.OutputUnits);
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
			ClassicAssert.IsTrue(KingdomVocationServiceRules.TryBuildHoldingReport(
				"taf:settlement:seat", out KingdomVocationServiceOffer holding,
				out string failure), failure);
			ClassicAssert.AreEqual(KingdomVocationServiceOfferState.Neutral, holding.State);
			ClassicAssert.IsNull(holding.Verb);
			ClassicAssert.IsFalse(KingdomVocationServiceRules.TryPrepareRequest(
				new KingdomVocationServiceBook(), holding, 1L,
				out KingdomVocationServiceRequest _, out failure));
			ClassicAssert.IsTrue(KingdomVocationServiceRules.TryBuildUnavailable(
				"taf:settlement:seat", "refuge", "no completed shelter receipt",
				"complete an exact shelter", out KingdomVocationServiceOffer unavailable,
				out failure), failure);
			ClassicAssert.IsFalse(KingdomVocationServiceRules.TryPrepareRequest(
				new KingdomVocationServiceBook(), unavailable, 1L,
				out KingdomVocationServiceRequest _, out failure));
			KingdomVocationServiceOffer opened = Offer("waystation", 1);
			KingdomVocationServiceOffer moved = Offer("waystation", 2);
			ClassicAssert.IsFalse(KingdomVocationServiceRules.TryMatchAvailableOffers(
				opened, moved, out failure));
			StringAssert.Contains("changed", failure);
		}

		[Test]
		public void ExactRetryAndNewSourcesAdvanceCadenceWithoutEviction()
		{
			ClassicAssert.AreEqual(3 * KingdomVocationServiceRules.MaxRowsPerSeries,
				KingdomVocationServiceRules.MaxRows);
			KingdomVocationServiceBook book = new KingdomVocationServiceBook();
			KingdomVocationServiceOffer first = Offer("waystation", 0);
			KingdomVocationServiceReceipt original = Append(book, first, 10L);
			ClassicAssert.IsTrue(KingdomVocationServiceRules.TryPrepareRequest(book, first, 99L,
				out KingdomVocationServiceRequest retryRequest, out string failure), failure);
			ClassicAssert.AreEqual(10L, retryRequest.RequestedTick);
			ClassicAssert.IsTrue(KingdomVocationServiceRules.TryServe(book, 0L, retryRequest, 99L,
				out KingdomVocationServiceReceipt retry, out failure), failure);
			ClassicAssert.AreSame(original, retry);
			ClassicAssert.AreEqual(1L, book.Revision);
			for (int i = 1; i < KingdomVocationServiceRules.MaxRowsPerSeries; i++)
			{
				KingdomVocationServiceReceipt row = Append(book, Offer("waystation", i), 10L + i);
				ClassicAssert.AreEqual(i, row.Request.CadenceOrdinal);
			}
			ClassicAssert.IsFalse(KingdomVocationServiceRules.TryPrepareRequest(book,
				Offer("waystation", 16), 100L, out KingdomVocationServiceRequest _, out failure));
			StringAssert.Contains("city and vocation", failure);
			ClassicAssert.IsTrue(KingdomVocationServiceRules.TryInspect(book,
				Offer("waystation", 16), out KingdomVocationServiceStatus seriesClosed,
				out failure), failure);
			ClassicAssert.AreEqual(KingdomVocationServiceActionState.CapacityClosed, seriesClosed.State);
			ClassicAssert.AreEqual(16, seriesClosed.SeriesCount);
			ClassicAssert.AreEqual(16, seriesClosed.RealmCount);
			for (int i = 0; i < KingdomVocationServiceRules.MaxRowsPerSeries; i++)
			{
				ClassicAssert.AreEqual(i, Append(book, Offer("refuge", i,
					"taf:settlement:second"), 30L + i).Request.CadenceOrdinal);
				ClassicAssert.AreEqual(i, Append(book, Offer("reliquary", i,
					"taf:settlement:third"), 50L + i).Request.CadenceOrdinal);
			}
			string[] retained = new string[book.Rows.Count];
			for (int i = 0; i < book.Rows.Count; i++) retained[i] = book.Rows[i].ServiceId;
			ClassicAssert.IsFalse(KingdomVocationServiceRules.TryPrepareRequest(book,
				Offer("waystation", 0, "taf:settlement:fourth"), 100L,
				out KingdomVocationServiceRequest _, out failure));
			StringAssert.Contains("realm", failure);
			ClassicAssert.IsTrue(KingdomVocationServiceRules.TryInspect(book,
				Offer("waystation", 0, "taf:settlement:fourth"),
				out KingdomVocationServiceStatus realmClosed, out failure), failure);
			ClassicAssert.AreEqual(KingdomVocationServiceActionState.CapacityClosed, realmClosed.State);
			ClassicAssert.AreEqual(0, realmClosed.SeriesCount);
			ClassicAssert.AreEqual(48, realmClosed.RealmCount);
			ClassicAssert.AreEqual(KingdomVocationServiceRules.MaxRows, book.Rows.Count);
			for (int i = 0; i < book.Rows.Count; i++) ClassicAssert.AreEqual(retained[i], book.Rows[i].ServiceId);
			List<string> pages = new List<string>();
			int offset = 0;
			do
			{
				ClassicAssert.IsTrue(KingdomVocationServiceRules.TryDescribeRealmResults(book, offset,
					out string page, out int next, out failure), failure);
				pages.Add(page); offset = next;
			}
			while (offset >= 0);
			ClassicAssert.Greater(pages.Count, 1);
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
			ClassicAssert.IsFalse(first.Report.Contains(first.ResultText),
				"pre-choice report must not disclose actionable result");
			Append(book, first, 10L);
			ClassicAssert.IsTrue(KingdomVocationServiceRules.TryInspect(book, first,
				out KingdomVocationServiceStatus recorded, out string failure), failure);
			ClassicAssert.AreEqual(KingdomVocationServiceActionState.AlreadyRecorded, recorded.State);
			ClassicAssert.AreEqual(1, recorded.SeriesCount);
			ClassicAssert.AreEqual(1, recorded.RealmCount);
			StringAssert.Contains("route result first", recorded.ExistingReceiptText);

			ClassicAssert.IsTrue(KingdomVocationServiceRules.TryInspect(book, second,
				out KingdomVocationServiceStatus available, out failure), failure);
			ClassicAssert.AreEqual(KingdomVocationServiceActionState.Available, available.State);
			ClassicAssert.AreEqual(0, available.SeriesCount);
			ClassicAssert.AreEqual(1, available.RealmCount);
			Append(book, second, 11L);
			ClassicAssert.AreEqual(2, book.Rows.Count,
				"same source id in another settlement is a distinct service");
			ClassicAssert.IsTrue(KingdomVocationServiceRules.TryDescribeRealmResults(book,
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
			ClassicAssert.IsTrue(KingdomVocationServiceRules.TryPrepareRequest(book, staleOffer, 10L,
				out KingdomVocationServiceRequest stale, out string failure), failure);
			Append(book, Offer("refuge", 2), 10L);
			ClassicAssert.IsFalse(KingdomVocationServiceRules.TryServe(book, 0L, stale, 10L,
				out KingdomVocationServiceReceipt _, out failure));
			StringAssert.Contains("revision", failure);
			ClassicAssert.AreEqual(1, book.Rows.Count);

			KingdomVocationServiceBook overflow = new KingdomVocationServiceBook
				{ Revision = long.MaxValue };
			ClassicAssert.IsTrue(KingdomVocationServiceRules.TryPrepareRequest(overflow,
				Offer("reliquary", 3), 10L, out KingdomVocationServiceRequest request,
				out failure), failure);
			ClassicAssert.IsFalse(KingdomVocationServiceRules.TryServe(overflow, long.MaxValue,
				request, 10L, out KingdomVocationServiceReceipt _, out failure));
			StringAssert.Contains("cannot advance", failure);
			ClassicAssert.AreEqual(0, overflow.Rows.Count);
			request.InputUnits = 1;
			ClassicAssert.IsFalse(KingdomVocationServiceRules.TryServe(new KingdomVocationServiceBook(),
				0L, request, 10L, out KingdomVocationServiceReceipt _, out failure));
		}

		[Test]
		public void CurrentCodecRoundTripsAndCorruptionOrFutureFailsClosed()
		{
			KingdomCivicPracticeEnvelope envelope = new KingdomCivicPracticeEnvelope();
			ClassicAssert.IsTrue(envelope.TryBindEmptyIdentity(Realm, out string failure), failure);
			Append(envelope.VocationServices, Offer("reliquary", 7), 30L);
			byte[] bytes = KingdomCivicPracticeCodec.Encode(envelope);
			ClassicAssert.AreEqual(4, BitConverter.ToInt32(bytes, 4));
			KingdomCivicPracticeEnvelope loaded = KingdomCivicPracticeCodec.Decode(bytes);
			ClassicAssert.AreEqual(1, loaded.VocationServices.Rows.Count);
			ClassicAssert.AreEqual(0, loaded.VocationServices.Rows[0].OutputUnits);
			CollectionAssert.AreEqual(bytes, KingdomCivicPracticeCodec.Encode(loaded));
			byte[] corrupt = (byte[])bytes.Clone(); corrupt[corrupt.Length - 1] ^= 1;
			ClassicAssert.IsTrue(KingdomCivicPracticeStore.ReadForRealm(corrupt, Realm,
				out failure).Quarantined);
			KingdomCivicPracticeEnvelope future = new KingdomCivicPracticeEnvelope
			{
				OpaqueFutureVersion = KingdomCivicPracticeCodec.CurrentWireVersion + 1,
				OpaqueFuturePayload = new byte[] { 4, 3, 2, 1 }
			};
			byte[] futureBytes = KingdomCivicPracticeCodec.Encode(future);
			KingdomCivicPracticeEnvelope opaque = KingdomCivicPracticeCodec.Decode(futureBytes);
			ClassicAssert.IsTrue(opaque.IsOpaqueFuture);
			CollectionAssert.AreEqual(futureBytes, KingdomCivicPracticeCodec.Encode(opaque));
		}

		private static KingdomVocationServiceReceipt Append(KingdomVocationServiceBook book,
			KingdomVocationServiceOffer offer, long tick)
		{
			ClassicAssert.IsTrue(KingdomVocationServiceRules.TryPrepareRequest(book, offer, tick,
				out KingdomVocationServiceRequest request, out string failure), failure);
			ClassicAssert.IsTrue(KingdomVocationServiceRules.TryServe(book, book.Revision, request,
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
			ClassicAssert.IsTrue(KingdomVocationServiceRules.TryBuildAvailableOffer(source,
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
			ClassicAssert.IsTrue(KingdomVocationServiceRules.TryBuildAvailableOffer(source,
				out KingdomVocationServiceOffer offer, out string failure), failure);
			return offer;
		}
	}
}
#endif
