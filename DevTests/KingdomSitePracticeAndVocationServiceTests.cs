#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomSitePracticeAndVocationServiceTests
	{
		private const int Practice = KingdomCivicMemoryLimits.SectionCivicPractice;
		private const string Realm =
			"taf:realm:v1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		private const string OtherRealm =
			"taf:realm:v1:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

		private const string LegacyGolden =
			"VEZTUAEAAAC0AwAASwIAAFRGU1ABAAAAAQAAAAAAAAABAAAAMwIAAAEAAAABUnRhZjpzaXRlLXByYWN0" +
			"aWNlOjg5ZTI2YWNjZDhhMmQwODk0NTUyODBkZDEzNTEzMDlmMjM4MmEzMjExMDZjMjQ2ZDZiNmE5ODVk" +
			"OGJiYzNkNmQBE3RhZjpzZXR0bGVtZW50OnNlYXQBCndheXN0YXRpb24BCXNhbHQtZHVuZQEKc2FsdCBk" +
			"dW5lcwEVdGhlIEdyZWF0IFNhbHQgRGVzZXJ0AQ90YWY6Y3JlZWQ6d2F0ZXIBEHRhZjp3b3JrOmNpc3Rl" +
			"cm4BEXRhZjpkZWVkOmZvdW5kaW5nARJ0aGUgc2VhbGVkIGNpc3Rlcm4BEXRoZSBmaXJzdCBzaGVsdGVy" +
			"CgAAAAAAAAAPAAAAAAAAAAFANDA3ODQzMjY2MTg4N2FmYjQyOGY1NTgxYmE2NjcwNWUyNDZiZTNjYjRj" +
			"ZDVlOTUzZTkyZjhkMDZhNWY3NjFiNQEAAAABInNhbHQgZHVuZXMgLyB0aGUgR3JlYXQgU2FsdCBEZXNl" +
			"cnQBG3NhbHQtZHVuZSAvIHRhZjpjcmVlZDp3YXRlcgEWS2VlcCB0aGUgbG9jYWwgYWNjb3VudAF2dGFm" +
			"OnNldHRsZW1lbnQ6c2VhdCBrZWVwcyBhIHZpc2libGUgcHJhY3RpY2Ugb2Ygc2FsdCBkdW5lcyAvIHRo" +
			"ZSBHcmVhdCBTYWx0IERlc2VydCBiZXNpZGUgc2FsdC1kdW5lIC8gdGFmOmNyZWVkOndhdGVyLhQAAAAA" +
			"AAAAYQEAAFRGU1ABAAAAAQAAAAAAAAABAAAASQEAAAEAAAABVXRhZjp2b2NhdGlvbi1zZXJ2aWNlOmMz" +
			"NDYzYTI1M2JhMDY4NGE0YThjOGEzNzk1YWE5OTI2ZjUyNGU3ZjA2MWE3NjNiZWNjMzQyMWZlMGZlOTRm" +
			"YTMBE3RhZjpzZXR0bGVtZW50OnNlYXQBCndheXN0YXRpb24BAQx0YWY6c291cmNlOjEBFGV4YWN0IGxv" +
			"Y2FsIHNvdXJjZSAxAQp0YWY6c2luazoxAQAAAAEAAAAAAAAAGQAAAAAAAAABQDBhNjBjZjhlYWIwODIx" +
			"ZGYxZjEzZmExZDg2NTZkMGMxODEwYmUyODU2OTE2ZDlhYTkxNmNkZjk2NDVkODc4MDEBFUFzayBmb3Ig" +
			"YSByb3V0ZSBicmllZgEhUm91dGUgYnJpZWY6IGV4YWN0IGxvY2FsIHNvdXJjZSAxAQAAAB4AAAAAAAAA" +
			"G7KJmy/uXZy3MQPzIZDTD7AEDP2n2ClZNAonvid2jvE=";

		private const string LegacyV2Golden =
			"VEZTUAIAAAAWAgAATQAAAHRhZjpyZWFsbTp2MTphYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFh" +
			"YWFhYWFhYWFhYWFhYWFhYWFhARQAAABURlNQAQAAAAAAAAAAAAAAAAAAAKgBAABURlNQAQAAAAEAAAAAAAAAAQAAAJABAAABAAAA" +
			"AVV0YWY6dm9jYXRpb24tc2VydmljZTo5NzhmYzJhN2ZhMmQ5MGI0M2E3ZTdkMjkzODU1NDljOWRmYzM0ZjdhNzIzZjIxOGY0ZTdk" +
			"ZmM4NjU1MDNhMmZjARN0YWY6c2V0dGxlbWVudDpzZWF0AQp3YXlzdGF0aW9uAQEXdGFmOnNvdXJjZTp3YXlzdGF0aW9uOjEBDmV4" +
			"YWN0IHNvdXJjZSAxAVJ0YWY6dm9jYXRpb24tc2luazoyZWU4YzhjMGFkYWNkZTM3NDA1NzQyMmJhODNlMDIzOWI3ZTRhNWM4MGUw" +
			"YmFhMGFkMDZjNzkyOWM0NjBhN2MxAQAAAAAAAAAAAAAACgAAAAAAAAABQGNiNGRjZGNlNDQyNGY4MjJlODc2ODliNTNmZjFhYTRi" +
			"YzkzOTg2N2QzMDNiNWFlNWM3YjIxMjljZGRjZmQ1YTABFUFzayBmb3IgYSByb3V0ZSBicmllZgEbUm91dGUgYnJpZWY6IGV4YWN0" +
			"IHNvdXJjZSAxAQAAAAoAAAAAAAAAJ5BlP7pqUASst6EnIwSxwMMcGSXq3qZZ+2z4/BjVtZE=";

		[Test]
		public void SiteReadingIsDeterministicRetrySafeAndDoesNotRewriteVocation()
		{
			KingdomSiteEvidenceSnapshot snapshot = Site();
			KingdomSitePracticeBook book = new KingdomSitePracticeBook();
			Assert.IsTrue(KingdomSitePracticeRules.TryPreview(snapshot,
				out string first, out string second, out string failure), failure);
			Assert.AreNotEqual(first, second);
			Assert.IsTrue(KingdomSitePracticeRules.TryRead(book, 0L, snapshot, 2, 20L,
				out KingdomSitePracticeReceipt receipt, out failure), failure);
			Assert.AreEqual("waystation", receipt.Source.Vocation);
			long revision = book.Revision;
			Assert.IsTrue(KingdomSitePracticeRules.TryRead(book, 0L, snapshot, 2, 99L,
				out KingdomSitePracticeReceipt retry, out failure), failure);
			Assert.AreSame(receipt, retry);
			Assert.AreEqual(revision, book.Revision);
			Assert.IsFalse(KingdomSitePracticeRules.TryRead(book, revision, snapshot, 1,
				99L, out KingdomSitePracticeReceipt _, out failure));
			Assert.AreEqual(revision, book.Revision);
			StringAssert.Contains("chosen vocation remains", receipt.Description);
		}

		[Test]
		public void SiteReadingFailsClosedForMissingEvidenceCapacityAndUtf8()
		{
			KingdomSitePracticeBook book = new KingdomSitePracticeBook();
			KingdomSiteEvidenceSnapshot bad = Site();
			bad.WorkReceiptId = null;
			Assert.IsFalse(KingdomSitePracticeRules.TryRead(book, 0L, bad, 1, 20L,
				out KingdomSitePracticeReceipt _, out string _));
			for (int i = 0; i < KingdomSitePracticeRules.MaxRows; i++)
			{
				KingdomSiteEvidenceSnapshot row = Site();
				row.SettlementId = "taf:settlement:capacity:" + i;
				row.Digest = KingdomSitePracticeRules.SnapshotDigest(row);
				Assert.IsTrue(KingdomSitePracticeRules.TryRead(book, book.Revision, row, 1,
					20L, out KingdomSitePracticeReceipt _, out string failure), failure);
			}
			Assert.IsFalse(KingdomSitePracticeRules.TryRead(book, book.Revision, Site(), 1,
				20L, out KingdomSitePracticeReceipt _, out string _));
			KingdomSiteEvidenceSnapshot malformed = Site();
			malformed.DeedText = "bad\ud800";
			Assert.DoesNotThrow(() => Assert.IsFalse(KingdomSitePracticeRules.TryRead(
				new KingdomSitePracticeBook(), 0L, malformed, 1, 20L,
				out KingdomSitePracticeReceipt _, out string _)));
		}

		[Test]
		public void SiteContextSelectsOneStableExactWorkAndShowsBothReadings()
		{
			KingdomSiteFoundingEvidence founding = new KingdomSiteFoundingEvidence
			{
				SettlementId = "taf:settlement:seat", Vocation = "waystation",
				Style = "salt-dune", Terrain = "TerrainSaltDunes",
				Region = "the Great Salt Desert",
				DeedReceiptId = "taf:founding:v1:2:abc:chronicle",
				DeedText = "the founding transaction at JoppaWorld.53.3.1.1.10",
				FoundedTick = 10L
			};
			KingdomSiteBuiltWorkEvidence first = Work(founding.SettlementId,
				"taf:construction:aaa", "taf:object:work-a", 20L);
			KingdomSiteBuiltWorkEvidence second = Work(founding.SettlementId,
				"taf:construction:bbb", "taf:object:work-b", 30L);
			Assert.IsTrue(KingdomSitePracticeRules.TryBuildPreview(founding,
				new List<KingdomSiteBuiltWorkEvidence> { second, first },
				out KingdomSitePracticePreview left, out string failure), failure);
			Assert.IsTrue(KingdomSitePracticeRules.TryBuildPreview(founding,
				new List<KingdomSiteBuiltWorkEvidence> { first, second },
				out KingdomSitePracticePreview right, out failure), failure);
			Assert.AreEqual("taf:construction:aaa", left.Snapshot.WorkReceiptId);
			Assert.AreEqual(left.Snapshot.Digest, right.Snapshot.Digest);
			Assert.AreEqual(20L, left.Snapshot.ObservedTick);
			Assert.AreNotEqual(left.FirstReading, left.SecondReading);
			second.SettlementId = "taf:settlement:foreign";
			Assert.IsFalse(KingdomSitePracticeRules.TryBuildPreview(founding,
				new List<KingdomSiteBuiltWorkEvidence> { first, second },
				out KingdomSitePracticePreview _, out string _));
		}

		[Test]
		public void D12OffersAreExplicitTypedAndAlwaysZeroEconomy()
		{
			string[] vocations = { "waystation", "refuge", "reliquary" };
			KingdomVocationServiceKind[] kinds = {
				KingdomVocationServiceKind.RouteBrief,
				KingdomVocationServiceKind.SanctuaryTitle,
				KingdomVocationServiceKind.ProvenanceReading };
			KingdomVocationServiceAuthority[] authorities = {
				KingdomVocationServiceAuthority.PolityRoute,
				KingdomVocationServiceAuthority.BuiltShelter,
				KingdomVocationServiceAuthority.ArtifactRecognition };
			for (int i = 0; i < vocations.Length; i++)
			{
				KingdomVocationServiceSource source = new KingdomVocationServiceSource(
					"taf:settlement:seat", vocations[i], kinds[i], authorities[i],
					"taf:evidence:" + i, "exact source fact " + i,
					"useful result " + i);
				Assert.IsTrue(KingdomVocationServiceRules.TryBuildAvailableOffer(source,
					out KingdomVocationServiceOffer offer, out string failure), failure);
				Assert.AreEqual(KingdomVocationServiceOfferState.Available, offer.State);
				Assert.AreEqual(authorities[i], offer.Authority);
				Assert.AreEqual(0, offer.InputUnits);
				Assert.AreEqual(0, offer.OutputUnits);
				Assert.IsFalse(offer.MutatesSource);
				Assert.IsTrue(KingdomVocationServiceRules.TryValidateOffer(offer,
					out failure), failure);
			}
			Assert.IsTrue(KingdomVocationServiceRules.TryBuildHoldingReport(
				"taf:settlement:seat", out KingdomVocationServiceOffer neutral,
				out string holdingFailure), holdingFailure);
			Assert.AreEqual(KingdomVocationServiceOfferState.Neutral, neutral.State);
			Assert.AreEqual(KingdomVocationServiceAuthority.None, neutral.Authority);
			Assert.IsNull(neutral.Verb);
			Assert.IsNull(neutral.SourceReceiptId);
			StringAssert.Contains("promises no vocation service", neutral.Report);
			foreach (PropertyInfo property in typeof(KingdomVocationServiceOffer).GetProperties())
				Assert.IsNull(property.GetSetMethod(true), property.Name + " must be get-only");
		}

		[Test]
		public void D12UnavailableAndInvalidEnumsFailClosed()
		{
			Assert.IsTrue(KingdomVocationServiceRules.TryBuildUnavailable(
				"taf:settlement:seat", "reliquary", "authority is absent",
				"restore exact authority", out KingdomVocationServiceOffer unavailable,
				out string failure), failure);
			Assert.AreEqual(KingdomVocationServiceOfferState.Unavailable,
				unavailable.State);
			Assert.AreEqual(KingdomVocationServiceAuthority.ArtifactRecognition,
				unavailable.Authority);
			Assert.AreEqual(0, unavailable.InputUnits);
			Assert.IsFalse(KingdomVocationServiceRules.TryBuildUnavailable(
				"bad", "reliquary", null, "repair", out unavailable, out failure));
			KingdomVocationServiceOffer invalid = new KingdomVocationServiceOffer(
				(KingdomVocationServiceOfferState)99, "taf:settlement:seat", "waystation",
				KingdomVocationServiceKind.RouteBrief,
				KingdomVocationServiceAuthority.PolityRoute, "Ask for a route brief",
				"polity route authority", "taf:evidence:1", "exact source", "result", "view",
				"on request", "dismissed", "report", null, null);
			Assert.IsFalse(KingdomVocationServiceRules.TryValidateOffer(invalid,
				out failure));
			KingdomVocationServiceBook legacy = LegacyEnvelope().VocationServices;
			legacy.Rows[0].Request.Kind = (KingdomVocationServiceKind)99;
			Assert.IsFalse(KingdomVocationServiceRules.TryValidate(legacy, out failure));
		}

		[Test]
		public void TryServeAppendsOneZeroEconomyReceiptAndExactRetryIsStable()
		{
			KingdomVocationServiceBook book = new KingdomVocationServiceBook();
			KingdomVocationServiceSource source = new KingdomVocationServiceSource(
				"taf:settlement:seat", "waystation", KingdomVocationServiceKind.RouteBrief,
				KingdomVocationServiceAuthority.PolityRoute, "taf:receipt:route:1",
				"exact current-realm caravan route", "exact route result");
			Assert.IsTrue(KingdomVocationServiceRules.TryBuildAvailableOffer(source,
				out KingdomVocationServiceOffer offer, out string failure), failure);
			Assert.IsTrue(KingdomVocationServiceRules.TryPrepareRequest(book, offer, 25L,
				out KingdomVocationServiceRequest request, out failure), failure);
			Assert.IsTrue(KingdomVocationServiceRules.TryServe(book, 0L, request, 30L,
				out KingdomVocationServiceReceipt receipt, out failure), failure);
			Assert.AreEqual(1L, book.Revision);
			Assert.AreEqual(0, receipt.Request.InputUnits);
			Assert.AreEqual(0, receipt.OutputUnits);
			Assert.IsTrue(KingdomVocationServiceRules.TryServe(book, 0L, request, 99L,
				out KingdomVocationServiceReceipt retry, out failure), failure);
			Assert.AreSame(receipt, retry);
			Assert.AreEqual(1L, book.Revision);
		}

		[Test]
		public void IndependentEnvelopeIsBoundAuthenticatedAndFutureOpaque()
		{
			KingdomCivicPracticeEnvelope envelope = new KingdomCivicPracticeEnvelope();
			Assert.IsTrue(envelope.TryBindEmptyIdentity(Realm, out string failure), failure);
			Assert.IsTrue(KingdomSitePracticeRules.TryRead(envelope.SitePractices, 0L,
				Site(), 1, 20L, out KingdomSitePracticeReceipt _, out failure), failure);
			byte[] bytes = KingdomCivicPracticeCodec.Encode(envelope);
			Assert.LessOrEqual(bytes.Length, KingdomCivicPracticeCodec.MaxEnvelopeBytes);
			KingdomCivicPracticeEnvelope loaded = KingdomCivicPracticeCodec.Decode(bytes);
			Assert.AreEqual(1, loaded.SitePractices.Rows.Count);
			Assert.AreEqual(0, loaded.VocationServices.Rows.Count);
			CollectionAssert.AreEqual(bytes, KingdomCivicPracticeCodec.Encode(loaded));
			Assert.IsTrue(KingdomCivicPracticeStore.ReadForRealm(bytes, OtherRealm,
				out failure).Quarantined);
			KingdomCivicPracticeEnvelope future = new KingdomCivicPracticeEnvelope
			{
				OpaqueFutureVersion = KingdomCivicPracticeCodec.CurrentWireVersion + 1,
				OpaqueFuturePayload = new byte[] { 1, 2, 3 }
			};
			byte[] futureBytes = KingdomCivicPracticeCodec.Encode(future);
			KingdomCivicPracticeEnvelope opaque = KingdomCivicPracticeCodec.Decode(futureBytes);
			Assert.IsTrue(opaque.IsOpaqueFuture);
			CollectionAssert.AreEqual(futureBytes, KingdomCivicPracticeCodec.Encode(opaque));
		}

		[Test]
		public void WireV1GoldenMigratesOnlyWhenAuthorityIsEmpty()
		{
			byte[] bytes = Convert.FromBase64String(LegacyGolden);
			Assert.AreEqual(992, bytes.Length);
			KingdomCivicPracticeEnvelope legacy = KingdomCivicPracticeCodec.Decode(bytes);
			Assert.IsFalse(legacy.IdentityBound);
			Assert.AreEqual(1, legacy.SitePractices.Rows.Count);
			Assert.AreEqual("taf:work:cistern",
				legacy.SitePractices.Rows[0].Source.WorkReceiptId);
			Assert.AreEqual(1, legacy.VocationServices.Rows.Count);
			Assert.AreEqual("Ask for a route brief", legacy.VocationServices.Rows[0].Verb);
			Assert.AreEqual(KingdomVocationServiceReceipt.CurrentVersion,
				legacy.VocationServices.Rows[0].Version);
			Assert.AreEqual(0, legacy.VocationServices.Rows[0].Request.InputUnits);
			Assert.AreEqual(0, legacy.VocationServices.Rows[0].OutputUnits);
			Assert.IsFalse(legacy.TryBindEmptyIdentity(Realm, out string failure));
			byte[] ingress = (byte[])bytes.Clone();
			KingdomCivicPracticeEnvelope quarantined =
				KingdomCivicPracticeStore.ReadForRealm(bytes, Realm, out failure);
			Assert.IsTrue(quarantined.Quarantined);
			CollectionAssert.AreEqual(ingress, bytes);
			byte[] emptyV1 = Convert.FromBase64String(
				"VEZTUAEAAAAwAAAAFAAAAFRGU1ABAAAAAAAAAAAAAAAAAAAAFAAAAFRGU1ABAAAAAAAAAAAAAAAAAAAA4jHwsfuc9LX/1cXX6xJRYtEJVELb9TQVng09DGPYx1w=");
			KingdomCivicPracticeEnvelope migrated =
				KingdomCivicPracticeStore.ReadForRealm(emptyV1, Realm, out failure);
			Assert.IsNull(failure);
			Assert.IsTrue(migrated.IdentityBound);
			Assert.AreEqual(4,
				BitConverter.ToInt32(KingdomCivicPracticeCodec.Encode(migrated), 4));
		}

		[Test]
		public void WireV2GoldenMigratesServiceV1AndCurrentWriterUsesV4()
		{
			KingdomVocationServiceSource source = new KingdomVocationServiceSource(
				"taf:settlement:seat", "waystation", KingdomVocationServiceKind.RouteBrief,
				KingdomVocationServiceAuthority.PolityRoute, "taf:source:waystation:1",
				"exact source 1", "exact route result 1");
			Assert.IsTrue(KingdomVocationServiceRules.TryBuildAvailableOffer(source,
				out KingdomVocationServiceOffer offer, out string failure), failure);
			KingdomCivicPracticeEnvelope current = new KingdomCivicPracticeEnvelope();
			Assert.IsTrue(current.TryBindEmptyIdentity(Realm, out failure), failure);
			Assert.IsTrue(KingdomVocationServiceRules.TryPrepareRequest(
				current.VocationServices, offer, 10L,
				out KingdomVocationServiceRequest request, out failure), failure);
			Assert.IsTrue(KingdomVocationServiceRules.TryServe(current.VocationServices,
				0L, request, 10L, out KingdomVocationServiceReceipt _, out failure), failure);
			byte[] frozen = Convert.FromBase64String(LegacyV2Golden);
			Assert.AreEqual(578, frozen.Length);
			CollectionAssert.AreEqual(frozen,
				KingdomCivicPracticeCodec.EncodeLegacyV2ForTests(current));
			KingdomCivicPracticeEnvelope migrated = KingdomCivicPracticeCodec.Decode(frozen);
			Assert.IsTrue(migrated.IdentityBound);
			Assert.AreEqual(Realm, migrated.RealmId);
			Assert.AreEqual(KingdomVocationServiceReceipt.CurrentVersion,
				migrated.VocationServices.Rows[0].Version);
			Assert.AreEqual(0, migrated.VocationServices.Rows[0].Request.InputUnits);
			Assert.AreEqual(0, migrated.VocationServices.Rows[0].OutputUnits);
			Assert.AreEqual(4, BitConverter.ToInt32(
				KingdomCivicPracticeCodec.Encode(migrated), 4));
		}

		[Test]
		public void WireV3ServiceV2MigratesUsefulResultWithoutDroppingItsRow()
		{
			KingdomVocationServiceSource source = new KingdomVocationServiceSource(
				"taf:settlement:seat", "waystation", KingdomVocationServiceKind.RouteBrief,
				KingdomVocationServiceAuthority.PolityRoute, "taf:source:prior:1",
				"exact prior route", "current-only route endpoints and stage");
			Assert.IsTrue(KingdomVocationServiceRules.TryBuildAvailableOffer(source,
				out KingdomVocationServiceOffer offer, out string failure), failure);
			KingdomCivicPracticeEnvelope current = new KingdomCivicPracticeEnvelope();
			Assert.IsTrue(current.TryBindEmptyIdentity(Realm, out failure), failure);
			Assert.IsTrue(KingdomVocationServiceRules.TryPrepareRequest(
				current.VocationServices, offer, 17L,
				out KingdomVocationServiceRequest request, out failure), failure);
			Assert.IsTrue(KingdomVocationServiceRules.TryServe(current.VocationServices,
				0L, request, 19L, out KingdomVocationServiceReceipt _, out failure), failure);

			byte[] prior = KingdomCivicPracticeCodec.EncodePriorV3ForTests(current);
			Assert.AreEqual(3, BitConverter.ToInt32(prior, 4));
			KingdomCivicPracticeEnvelope migrated = KingdomCivicPracticeCodec.Decode(prior);
			Assert.AreEqual(1, migrated.VocationServices.Rows.Count);
			KingdomVocationServiceReceipt row = migrated.VocationServices.Rows[0];
			Assert.AreEqual(KingdomVocationServiceReceipt.CurrentVersion, row.Version);
			Assert.AreEqual("taf:source:prior:1", row.Request.SourceReceiptId);
			Assert.AreEqual(19L, row.CompletedTick);
			StringAssert.Contains("Legacy route brief", row.Request.ResultText);
			StringAssert.Contains(row.Request.ResultText, row.OutputText);
			Assert.AreEqual(4, BitConverter.ToInt32(
				KingdomCivicPracticeCodec.Encode(migrated), 4));
		}

		[Test]
		public void D1C18CommitPreservesServiceBytesAndExactRetryDoesNotMoveOuterRevision()
		{
			KingdomCivicPracticeEnvelope initial = BoundLegacyEnvelope(Realm);
			byte[] initialBytes = KingdomCivicPracticeCodec.Encode(initial);
			byte[] serviceBytes = ExtractServiceBytes(initialBytes);
			KingdomCivicMemoryAuthority authority = AuthorityHolding(initialBytes);
			AuthorityPort port = new AuthorityPort(authority);
			KingdomSitePracticeChoiceView view = View(Site());

			Assert.IsTrue(KingdomCivicPracticeTransactions.TryChoose(port, Realm, view, 1,
				20L, out KingdomCivicPracticeCommitResult result, out string failure), failure);
			Assert.IsTrue(result.Changed);
			Assert.AreEqual(1, port.CommitCalls);
			long outerRevision = authority.Revision;
			byte[] afterBytes = SectionBytes(authority, Practice);
			KingdomCivicPracticeEnvelope after = KingdomCivicPracticeCodec.Decode(afterBytes);
			Assert.AreEqual(1, after.SitePractices.Rows.Count);
			Assert.AreEqual("waystation", after.SitePractices.Rows[0].Source.Vocation);
			Assert.AreEqual(1, after.VocationServices.Rows.Count);
			CollectionAssert.AreEqual(serviceBytes, ExtractServiceBytes(afterBytes));

			Assert.IsTrue(KingdomCivicPracticeTransactions.TryChoose(port, Realm, view, 1,
				200L, out KingdomCivicPracticeCommitResult retry, out failure), failure);
			Assert.IsFalse(retry.Changed);
			Assert.AreEqual(result.PracticeId, retry.PracticeId);
			Assert.AreEqual(result.ChosenTick, retry.ChosenTick);
			Assert.AreEqual(outerRevision, authority.Revision);
			Assert.AreEqual(1, port.CommitCalls);
		}

		[Test]
		public void D1RejectsPreviewDriftBeforeLeaseAndStaleOuterCas()
		{
			KingdomSiteEvidenceSnapshot opened = Site();
			KingdomSitePracticeChoiceView view = View(opened);
			string openedDigest = view.EvidenceDigest;
			opened.WorkText = "mutated caller evidence";
			opened.Digest = KingdomSitePracticeRules.SnapshotDigest(opened);
			Assert.AreEqual(openedDigest, view.EvidenceDigest);
			KingdomSiteEvidenceSnapshot changed = Site();
			changed.WorkText = "a different exact completed work";
			changed.Digest = KingdomSitePracticeRules.SnapshotDigest(changed);
			Assert.IsFalse(view.Matches(Realm, Preview(changed), out string failure));

			KingdomCivicMemoryAuthority untouched = Authority();
			AuthorityPort guardPort = new AuthorityPort(untouched);
			Assert.IsFalse(KingdomCivicPracticeTransactions.TryChoose(guardPort, Realm,
				view, 3, 20L, out KingdomCivicPracticeCommitResult _, out failure));
			Assert.AreEqual(0, guardPort.ReadCalls);

			KingdomCivicMemoryAuthority moved = Authority();
			StalePort stale = new StalePort(moved);
			Assert.IsFalse(KingdomCivicPracticeTransactions.TryChoose(stale, Realm, view,
				1, 20L, out KingdomCivicPracticeCommitResult _, out failure));
			StringAssert.Contains("revision", failure);
			Assert.IsNull(moved.Read().Section(Practice));
		}

		[Test]
		public void D1FailsClosedForCapacityWrongRealmNestedFutureQuarantineAndFutureOuter()
		{
			KingdomCivicPracticeEnvelope full = new KingdomCivicPracticeEnvelope();
			Assert.IsTrue(full.TryBindEmptyIdentity(Realm, out string failure), failure);
			for (int i = 0; i < KingdomSitePracticeRules.MaxRows; i++)
			{
				KingdomSiteEvidenceSnapshot row = Site();
				row.SettlementId = "taf:settlement:full:" + i;
				row.Digest = KingdomSitePracticeRules.SnapshotDigest(row);
				Assert.IsTrue(KingdomSitePracticeRules.TryRead(full.SitePractices,
					full.SitePractices.Revision, row, 1, 20L,
					out KingdomSitePracticeReceipt _, out failure), failure);
			}
			AssertRefusedUnchanged(AuthorityHolding(KingdomCivicPracticeCodec.Encode(full)),
				View(Site()), "capacity");

			KingdomCivicPracticeEnvelope foreign = new KingdomCivicPracticeEnvelope();
			Assert.IsTrue(foreign.TryBindEmptyIdentity(OtherRealm, out failure), failure);
			AssertRefusedUnchanged(AuthorityHolding(KingdomCivicPracticeCodec.Encode(foreign)),
				View(Site()), "realm");

			KingdomCivicPracticeEnvelope nestedFuture = new KingdomCivicPracticeEnvelope
			{
				OpaqueFutureVersion = KingdomCivicPracticeCodec.CurrentWireVersion + 1,
				OpaqueFuturePayload = new byte[] { 9, 8, 7 }
			};
			KingdomCivicMemoryAuthority futureSection = Authority();
			futureSection.AdoptSaved(KingdomCivicMemoryCodec.Encode(
				KingdomCivicMemoryState.Of(One(Practice,
					KingdomCivicPracticeCodec.Encode(nestedFuture)), 0L)));
			AssertRefusedUnchanged(futureSection, View(Site()), "newer");

			KingdomCivicMemoryAuthority quarantined = Authority();
			quarantined.AdoptSaved(new byte[] { 1, 2, 3, 4 });
			AssertRefusedUnchanged(quarantined, View(Site()), "read-only");

			KingdomCivicMemoryAuthority futureOuter = Authority();
			futureOuter.AdoptSaved(FutureOuterBytes());
			Assert.IsTrue(futureOuter.IsFutureOuter);
			AssertRefusedUnchanged(futureOuter, View(Site()), "read-only");
		}

		[Test]
		public void ExactCapArithmeticStillMatchesBothLegacyBooks()
		{
			Assert.AreEqual(20 + 8 * (4 + 4096),
				KingdomCivicPracticeCodec.MaxSiteBookBytes);
			Assert.AreEqual(20 + 48 * (4 + 4096),
				KingdomCivicPracticeCodec.MaxServiceBookBytes);
			Assert.AreEqual(229774, KingdomCivicPracticeCodec.MaxEnvelopeBytes);
			Assert.AreEqual(KingdomCivicPracticeCodec.MaxEnvelopeBytes,
				KingdomCivicMemoryLimits.MaxCivicPracticeBytes);
			Assert.AreEqual(839860, KingdomCivicMemoryLimits.MaxCumulativePayloadBytes);
			Assert.AreEqual(840048, KingdomCivicMemoryLimits.MaxEnvelopeBytes);
			Assert.LessOrEqual(KingdomCivicPracticeCodec.Encode(
				BoundLegacyEnvelope(Realm)).Length,
				KingdomCivicPracticeCodec.MaxEnvelopeBytes);
		}

		private static void AssertRefusedUnchanged(KingdomCivicMemoryAuthority authority,
			KingdomSitePracticeChoiceView view, string expectedFailure)
		{
			long revision = authority.Revision;
			byte[] before = AuthorityEvidence(authority);
			AuthorityPort port = new AuthorityPort(authority);
			Assert.IsFalse(KingdomCivicPracticeTransactions.TryChoose(port, Realm, view, 1,
				20L, out KingdomCivicPracticeCommitResult _, out string failure));
			Assert.IsNotNull(failure);
			StringAssert.Contains(expectedFailure, failure.ToLowerInvariant());
			Assert.AreEqual(revision, authority.Revision);
			CollectionAssert.AreEqual(before, AuthorityEvidence(authority));
			Assert.AreEqual(0, port.CommitCalls);
		}

		private static KingdomSitePracticeChoiceView View(
			KingdomSiteEvidenceSnapshot snapshot)
		{
			Assert.IsTrue(KingdomSitePracticeChoiceView.TryCreate(Realm, Preview(snapshot),
				out KingdomSitePracticeChoiceView view, out string failure), failure);
			return view;
		}

		private static KingdomSitePracticePreview Preview(
			KingdomSiteEvidenceSnapshot snapshot)
		{
			Assert.IsTrue(KingdomSitePracticeRules.TryPreview(snapshot,
				out string first, out string second, out string failure), failure);
			return new KingdomSitePracticePreview
			{
				Snapshot = snapshot,
				SourceSummary = "Source: exact current loaded-city evidence.",
				FirstTitle = "Keep the local account",
				FirstReading = first,
				SecondTitle = "Set the founding beside later work",
				SecondReading = second,
				VocationNotice = "The explicit vocation remains unchanged."
			};
		}

		private static KingdomSiteEvidenceSnapshot Site()
		{
			KingdomSiteEvidenceSnapshot snapshot = new KingdomSiteEvidenceSnapshot
			{
				SettlementId = "taf:settlement:seat", Vocation = "waystation",
				Style = "salt-dune", Terrain = "salt dunes",
				Region = "the Great Salt Desert", Creed = "taf:creed:water",
				WorkReceiptId = "taf:work:cistern", DeedReceiptId = "taf:deed:founding",
				WorkText = "the sealed cistern", DeedText = "the first shelter",
				FoundedTick = 10L, ObservedTick = 15L
			};
			snapshot.Digest = KingdomSitePracticeRules.SnapshotDigest(snapshot);
			return snapshot;
		}

		private static KingdomSiteBuiltWorkEvidence Work(string settlementId,
			string receiptId, string objectId, long tick)
		{
			return new KingdomSiteBuiltWorkEvidence
			{
				SettlementId = settlementId, ZoneId = "JoppaWorld.53.3.1.1.10",
				ObjectId = objectId, DesignKey = "cistern", WorkReceiptId = receiptId,
				DisplayName = "sealed cistern", CompletedTick = tick
			};
		}

		private static KingdomCivicPracticeEnvelope LegacyEnvelope()
		{
			return KingdomCivicPracticeCodec.Decode(Convert.FromBase64String(LegacyGolden));
		}

		private static KingdomCivicPracticeEnvelope BoundLegacyEnvelope(string realmId)
		{
			KingdomCivicPracticeEnvelope legacy = LegacyEnvelope();
			return new KingdomCivicPracticeEnvelope
			{
				RealmId = realmId,
				IdentityBound = true,
				SitePractices = new KingdomSitePracticeBook(),
				VocationServices = legacy.VocationServices
			};
		}

		private static byte[] ExtractServiceBytes(byte[] envelope)
		{
			using (MemoryStream stream = new MemoryStream(envelope, false))
			using (BinaryReader reader = new BinaryReader(stream))
			{
				reader.ReadInt32();
				Assert.AreEqual(KingdomCivicPracticeCodec.CurrentWireVersion,
					reader.ReadInt32());
				int payloadLength = reader.ReadInt32();
				byte[] payload = reader.ReadBytes(payloadLength);
				using (MemoryStream payloadStream = new MemoryStream(payload, false))
				using (BinaryReader nested = new BinaryReader(payloadStream))
				{
					int realmLength = nested.ReadInt32();
					nested.ReadBytes(realmLength);
					nested.ReadByte();
					int sitesLength = nested.ReadInt32();
					nested.ReadBytes(sitesLength);
					int servicesLength = nested.ReadInt32();
					return nested.ReadBytes(servicesLength);
				}
			}
		}

		private static KingdomCivicMemoryAuthority Authority()
		{
			KingdomCivicMemoryFamilyTable table = new KingdomCivicMemoryFamilyTable();
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++)
				table.Add(id, id == Practice
					? (KingdomCivicMemoryFamilyReader)ReadPractice : ReadAnything);
			return new KingdomCivicMemoryAuthority(table);
		}

		private static KingdomCivicMemoryAuthority AuthorityHolding(byte[] practiceBytes)
		{
			KingdomCivicMemoryAuthority authority = Authority();
			Assert.IsTrue(authority.TryCommit(One(Practice, practiceBytes), 0L,
				out string failure), failure);
			return authority;
		}

		private static List<KingdomCivicMemorySection> One(int id, byte[] payload)
		{
			return new List<KingdomCivicMemorySection>
			{
				new KingdomCivicMemorySection(id, payload)
			};
		}

		private static byte[] SectionBytes(KingdomCivicMemoryAuthority authority, int id)
		{
			return authority.Read().Section(id).Payload();
		}

		private static byte[] AuthorityEvidence(KingdomCivicMemoryAuthority authority)
		{
			KingdomCivicMemoryState state = authority.Read();
			return state.Quarantined || state.IsFutureOuter
				? state.RetainedPayload() : authority.Encode();
		}

		private static KingdomCivicMemoryNested ReadPractice(byte[] payload,
			out string failure)
		{
			try
			{
				KingdomCivicPracticeEnvelope envelope =
					KingdomCivicPracticeCodec.Decode(payload);
				if (envelope.IsOpaqueFuture)
				{
					failure = "";
					return KingdomCivicMemoryNested.Future;
				}
				if (!KingdomCivicPracticeStore.TryValidateIdentity(envelope, out failure))
					return KingdomCivicMemoryNested.Malformed;
				failure = "";
				return KingdomCivicMemoryNested.Current;
			}
			catch (Exception error)
			{
				failure = error.Message;
				return KingdomCivicMemoryNested.Malformed;
			}
		}

		private static KingdomCivicMemoryNested ReadAnything(byte[] payload,
			out string failure)
		{
			failure = payload == null || payload.Length == 0 ? "empty test payload" : "";
			return string.IsNullOrEmpty(failure) ? KingdomCivicMemoryNested.Current :
				KingdomCivicMemoryNested.Malformed;
		}

		private static byte[] FutureOuterBytes()
		{
			byte[] bytes = KingdomCivicMemoryCodec.Encode(KingdomCivicMemoryState.Of(
				One(KingdomCivicMemoryLimits.SectionCivicArtifacts, new byte[] { 1 }), 0L));
			bytes[4] = (byte)(KingdomCivicMemoryCodec.CurrentWireVersion + 1);
			using (SHA256 sha = SHA256.Create())
			{
				byte[] body = new byte[bytes.Length - 32];
				Buffer.BlockCopy(bytes, 0, body, 0, body.Length);
				Buffer.BlockCopy(sha.ComputeHash(body), 0, bytes, body.Length, 32);
			}
			return bytes;
		}

		private class AuthorityPort : IKingdomCivicPracticeSectionPort
		{
			protected readonly KingdomCivicMemoryAuthority Authority;
			internal int ReadCalls;
			internal int CommitCalls;

			internal AuthorityPort(KingdomCivicMemoryAuthority authority)
			{
				Authority = authority;
			}

			public bool TryReadSection(int sectionId,
				out KingdomCivicMemorySectionLease lease, out string failure)
			{
				ReadCalls++;
				return Authority.TryReadSection(sectionId, out lease, out failure);
			}

			public virtual bool TryCommitSection(KingdomCivicMemorySectionLease lease,
				byte[] payload, out string failure)
			{
				CommitCalls++;
				return Authority.TryCommitSection(lease, payload, out failure);
			}
		}

		private sealed class StalePort : AuthorityPort
		{
			private bool Moved;

			internal StalePort(KingdomCivicMemoryAuthority authority) : base(authority) { }

			public override bool TryCommitSection(KingdomCivicMemorySectionLease lease,
				byte[] payload, out string failure)
			{
				if (!Moved)
				{
					Moved = true;
					if (!Authority.TryCommit(One(
						KingdomCivicMemoryLimits.SectionCivicArtifacts,
						new byte[] { 1 }), Authority.Revision, out failure)) return false;
				}
				return base.TryCommitSection(lease, payload, out failure);
			}
		}
	}
}
#endif
