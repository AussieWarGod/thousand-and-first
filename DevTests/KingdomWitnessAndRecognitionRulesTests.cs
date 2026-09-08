#if TAF_TESTS
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomWitnessAndRecognitionRulesTests
	{
		private const string Realm = "taf:realm:v1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		private const string OtherRealm = "taf:realm:v1:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

		[Test]
		public void WitnessWorkFreezesMakerEventAndIndependentFixedCarrier()
		{
			KingdomWitnessWorkBook book = new KingdomWitnessWorkBook();
			KingdomWitnessWorkSource source = Witness("taf:event:closed:1", 7, "Eshkind");
			ClassicAssert.IsTrue(KingdomWitnessWorkRules.TryCapture(book, 0, source,
				out KingdomWitnessWorkReceipt row, out string failure), failure);
			byte[] captured = KingdomWitnessWorkCodec.Encode(book);
			ClassicAssert.IsTrue(KingdomWitnessWorkRules.TryCapture(book, 0, source, out _, out failure), failure);
			CollectionAssert.AreEqual(captured, KingdomWitnessWorkCodec.Encode(book));
			ClassicAssert.IsTrue(KingdomWitnessWorkRules.TryPrepareCarrier(book, book.Revision,
				row.WorkId, "taf:object:witness:1", "taf:zone:seat",
				"taf:construction:surface-1", 4, 5, 11, out failure), failure);
			row = book.Rows[0];
			ClassicAssert.IsTrue(KingdomWitnessWorkRules.TryCommitCarrier(book, book.Revision,
				row.WorkId, row.CarrierReceiptId, 12, out failure), failure);
			ClassicAssert.IsTrue(row.Fixed); ClassicAssert.IsFalse(row.Portable); ClassicAssert.AreEqual(0, row.CommerceValue);
			StringAssert.Contains("Eshkind", row.Description);
			KingdomWitnessWorkBook loaded = KingdomWitnessWorkCodec.Decode(
				KingdomWitnessWorkCodec.Encode(book));
			ClassicAssert.AreEqual(KingdomWitnessWorkPhase.Projected, loaded.Rows[0].Phase);
			ClassicAssert.IsTrue(KingdomWitnessWorkRules.TryReconcileCarrier(loaded, loaded.Revision,
				row.WorkId, false, true, 13, out failure), failure);
			byte[] removed = KingdomWitnessWorkCodec.Encode(loaded);
			ClassicAssert.IsTrue(KingdomWitnessWorkRules.TryReconcileCarrier(loaded, 0,
				row.WorkId, false, true, 13, out failure), failure);
			CollectionAssert.AreEqual(removed, KingdomWitnessWorkCodec.Encode(loaded));
		}

		[Test]
		public void WitnessWorkRejectsDeathMissingMakerMutationCapacityAndFutureWire()
		{
			KingdomWitnessWorkBook book = new KingdomWitnessWorkBook();
			KingdomWitnessWorkSource invalid = Witness("taf:event:death:1", 0, null);
			invalid.EventKind = "death";
			invalid.SnapshotDigest = KingdomWitnessWorkRules.SnapshotDigest(invalid);
			ClassicAssert.IsFalse(KingdomWitnessWorkRules.TryCapture(book, 0, invalid, out _, out _));
			ClassicAssert.IsFalse(KingdomWitnessWorkRules.TryCapture(book, 0,
				Witness("taf:event:closed:maker-absent", 0, null), out _, out _));
			for (int i = 0; i < KingdomWitnessWorkRules.MaxRows; i++)
			{
				KingdomWitnessWorkSource source = Witness("taf:event:closed:" + i, i + 1, "Maker " + i);
				ClassicAssert.IsTrue(KingdomWitnessWorkRules.TryCapture(book, book.Revision, source,
					out _, out string failure), failure);
			}
			byte[] stable = KingdomWitnessWorkCodec.Encode(book);
			ClassicAssert.IsFalse(KingdomWitnessWorkRules.TryCapture(book, book.Revision,
				Witness("taf:event:closed:overflow", 99, "Overflow"), out _, out _));
			CollectionAssert.AreEqual(stable, KingdomWitnessWorkCodec.Encode(book));
			byte[] future = (byte[])stable.Clone(); future[4] = 2;
			Assert.Throws<InvalidDataException>(() => KingdomWitnessWorkCodec.Decode(future));
			book.Rows[0].Source.EventText = "rewritten";
			ClassicAssert.IsFalse(KingdomWitnessWorkRules.TryValidate(book, out _));
		}

		[Test]
		public void OneEventAdapterCannotBeReattributedOrMovedAfterDisclosure()
		{
			KingdomWitnessWorkBook book = new KingdomWitnessWorkBook();
			KingdomWitnessWorkSource source = Witness("taf:event:closed:one", 7, "Eshkind");
			ClassicAssert.IsTrue(KingdomWitnessWorkRules.TryCapture(book, 0L, source,
				out KingdomWitnessWorkReceipt row, out string failure), failure);
			KingdomWitnessWorkSource changed = Witness(source.EventId, 9, "Tzimtzlum");
			ClassicAssert.IsFalse(KingdomWitnessWorkRules.TryCapture(book, book.Revision, changed,
				out _, out failure));
			StringAssert.Contains("collides", failure);
			ClassicAssert.IsTrue(KingdomWitnessWorkRules.TryPrepareCarrier(book, book.Revision,
				row.WorkId, "taf:object:surface", "taf:zone:seat",
				"taf:construction:surface", 4, 5, 11L, out failure), failure);
			KingdomWitnessWorkBook saved = KingdomWitnessWorkCodec.Decode(
				KingdomWitnessWorkCodec.Encode(book));
			ClassicAssert.AreEqual("taf:construction:surface",
				saved.Rows[0].CarrierConstructionReceiptId);
			ClassicAssert.AreEqual(4, saved.Rows[0].CarrierX);
			ClassicAssert.AreEqual(5, saved.Rows[0].CarrierY);
			ClassicAssert.IsFalse(KingdomWitnessWorkRules.TryPrepareCarrier(saved, saved.Revision,
				row.WorkId, "taf:object:surface", "taf:zone:seat",
				"taf:construction:surface", 5, 5, 12L, out failure));
			StringAssert.Contains("changed identity", failure);
			KingdomWitnessWorkReceipt forged = saved.Rows[0];
			forged.CarrierReceiptId = "taf:experience:witness-carrier:"
				+ new string('a', 64);
			ClassicAssert.IsFalse(KingdomWitnessWorkRules.TryValidate(saved, out failure),
				"a typed but non-derived carrier receipt must not survive codec validation");
		}

		[Test]
		public void WitnessMarkerProofBindsEveryOwnedProjectionField()
		{
			string work = "taf:experience:witness-work:" + new string('a', 64);
			string digest = new string('b', 64);
			string carrier = "taf:experience:witness-carrier:" + new string('c', 64);
			string first = MarkerProof(1, Realm, "taf:settlement:seat", work, digest,
				carrier, "taf:object:surface", "surface", "taf:zone:seat",
				"taf:construction:surface", 4, 5, "fixed account");
			string[] changed = new string[]
			{
				MarkerProof(2, Realm, "taf:settlement:seat", work, digest, carrier,
					"taf:object:surface", "surface", "taf:zone:seat",
					"taf:construction:surface", 4, 5, "fixed account"),
				MarkerProof(1, OtherRealm, "taf:settlement:seat", work, digest, carrier,
					"taf:object:surface", "surface", "taf:zone:seat",
					"taf:construction:surface", 4, 5, "fixed account"),
				MarkerProof(1, Realm, "taf:settlement:other", work, digest, carrier,
					"taf:object:surface", "surface", "taf:zone:seat",
					"taf:construction:surface", 4, 5, "fixed account"),
				MarkerProof(1, Realm, "taf:settlement:seat", work + "x", digest, carrier,
					"taf:object:surface", "surface", "taf:zone:seat",
					"taf:construction:surface", 4, 5, "fixed account"),
				MarkerProof(1, Realm, "taf:settlement:seat", work, new string('d', 64), carrier,
					"taf:object:surface", "surface", "taf:zone:seat",
					"taf:construction:surface", 4, 5, "fixed account"),
				MarkerProof(1, Realm, "taf:settlement:seat", work, digest, carrier + "x",
					"taf:object:surface", "surface", "taf:zone:seat",
					"taf:construction:surface", 4, 5, "fixed account"),
				MarkerProof(1, Realm, "taf:settlement:seat", work, digest, carrier,
					"taf:object:other", "surface", "taf:zone:seat",
					"taf:construction:surface", 4, 5, "fixed account"),
				MarkerProof(1, Realm, "taf:settlement:seat", work, digest, carrier,
					"taf:object:surface", "other", "taf:zone:seat",
					"taf:construction:surface", 4, 5, "fixed account"),
				MarkerProof(1, Realm, "taf:settlement:seat", work, digest, carrier,
					"taf:object:surface", "surface", "taf:zone:other",
					"taf:construction:surface", 4, 5, "fixed account"),
				MarkerProof(1, Realm, "taf:settlement:seat", work, digest, carrier,
					"taf:object:surface", "surface", "taf:zone:seat",
					"taf:construction:other", 4, 5, "fixed account"),
				MarkerProof(1, Realm, "taf:settlement:seat", work, digest, carrier,
					"taf:object:surface", "surface", "taf:zone:seat",
					"taf:construction:surface", 5, 5, "fixed account"),
				MarkerProof(1, Realm, "taf:settlement:seat", work, digest, carrier,
					"taf:object:surface", "surface", "taf:zone:seat",
					"taf:construction:surface", 4, 6, "fixed account"),
				MarkerProof(1, Realm, "taf:settlement:seat", work, digest, carrier,
					"taf:object:surface", "surface", "taf:zone:seat",
					"taf:construction:surface", 4, 5, "rewritten account")
			};
			ClassicAssert.IsNotNull(first);
			for (int i = 0; i < changed.Length; i++) ClassicAssert.AreNotEqual(first, changed[i],
				"projection field " + i + " was not authenticated");
		}

		[Test]
		public void CapturedWitnessCanQuietlyDeclineWithoutCarrierOrRemint()
		{
			KingdomWitnessWorkBook book = new KingdomWitnessWorkBook();
			ClassicAssert.IsTrue(KingdomWitnessWorkRules.TryCapture(book, 0L,
				Witness("taf:event:closed:decline", 7, "Eshkind"),
				out KingdomWitnessWorkReceipt row, out string failure), failure);
			ClassicAssert.IsTrue(KingdomWitnessWorkRules.TryDecline(book, book.Revision,
				row.WorkId, 11L, out failure), failure);
			ClassicAssert.AreEqual(KingdomWitnessWorkPhase.Declined, book.Rows[0].Phase);
			ClassicAssert.IsNull(book.Rows[0].CarrierObjectId);
			byte[] terminal = KingdomWitnessWorkCodec.Encode(book);
			long revision = book.Revision;
			ClassicAssert.IsTrue(KingdomWitnessWorkRules.TryDecline(book, 0L,
				row.WorkId, 12L, out failure), failure);
			ClassicAssert.AreEqual(revision, book.Revision);
			CollectionAssert.AreEqual(terminal, KingdomWitnessWorkCodec.Encode(book));
			ClassicAssert.IsFalse(KingdomWitnessWorkRules.TryPrepareCarrier(book, book.Revision,
				row.WorkId, "taf:object:surface", "taf:zone:seat",
				"taf:construction:surface", 4, 5, 12L, out failure));
		}

		[Test]
		public void RecognitionDistinguishesNamesAndClonesWithoutCustody()
		{
			KingdomArtifactRecognitionBook book = new KingdomArtifactRecognitionBook();
			KingdomArtifactSnapshot first = Artifact("taf:object:1", "folded carbide sword");
			KingdomArtifactSnapshot clone = Artifact("taf:object:2", "folded carbide sword");
			ClassicAssert.IsTrue(KingdomArtifactRecognitionRules.TryRecognize(book, 0, first,
				KingdomArtifactRecognitionKind.Inscription, 9, "Yla Haj", 20,
				out KingdomArtifactRecognitionReceipt a, out string failure), failure);
			ClassicAssert.IsTrue(KingdomArtifactRecognitionRules.TryRecognize(book, book.Revision, clone,
				KingdomArtifactRecognitionKind.Inscription, 9, "Yla Haj", 20,
				out KingdomArtifactRecognitionReceipt b, out failure), failure);
			ClassicAssert.AreNotEqual(a.RecognitionId, b.RecognitionId);
			ClassicAssert.AreEqual(0, a.CommerceValue); ClassicAssert.IsFalse(a.CustodyClaimed);
			ClassicAssert.AreEqual("taf:owner:player", a.Source.OwnerId);
			byte[] saved = KingdomArtifactRecognitionCodec.Encode(book);
			KingdomArtifactRecognitionBook loaded = KingdomArtifactRecognitionCodec.Decode(saved);
			ClassicAssert.IsTrue(KingdomArtifactRecognitionRules.TryRecognize(loaded, 0, first,
				KingdomArtifactRecognitionKind.Inscription, 9, "Yla Haj", 20, out _, out failure), failure);
			CollectionAssert.AreEqual(saved, KingdomArtifactRecognitionCodec.Encode(loaded));
			long revision = loaded.Revision;
			ClassicAssert.IsTrue(KingdomArtifactRecognitionRules.TryDescribe(loaded,
				a.RecognitionId, out string text, out failure), failure);
			ClassicAssert.AreEqual(revision, loaded.Revision); StringAssert.Contains("Yla Haj", text);
		}

		[Test]
		public void RecognitionRejectsChangedAttributionCapacityAndMalformedWire()
		{
			KingdomArtifactRecognitionBook book = new KingdomArtifactRecognitionBook();
			for (int i = 0; i < KingdomArtifactRecognitionRules.MaxRows; i++)
			{
				KingdomArtifactSnapshot source = Artifact("taf:object:" + i, "relic " + i);
				ClassicAssert.IsTrue(KingdomArtifactRecognitionRules.TryRecognize(book, book.Revision,
					source, KingdomArtifactRecognitionKind.Representation, 0, null, 20,
					out _, out string failure), failure);
			}
			byte[] stable = KingdomArtifactRecognitionCodec.Encode(book);
			ClassicAssert.IsFalse(KingdomArtifactRecognitionRules.TryRecognize(book, book.Revision,
				Artifact("taf:object:overflow", "overflow"), KingdomArtifactRecognitionKind.Remark,
				0, null, 20, out _, out _));
			CollectionAssert.AreEqual(stable, KingdomArtifactRecognitionCodec.Encode(book));
			byte[] future = (byte[])stable.Clone(); future[4] = 2;
			Assert.Throws<InvalidDataException>(() => KingdomArtifactRecognitionCodec.Decode(future));
			book.Rows[0].CommerceValue = 1;
			ClassicAssert.IsFalse(KingdomArtifactRecognitionRules.TryValidate(book, out _));
		}

		[Test]
		public void Utf8AndDeclaredByteBudgetsAreExactAndBounded()
		{
			ClassicAssert.AreEqual(KingdomWitnessWorkCodec.BookHeaderBytes +
				KingdomWitnessWorkRules.MaxRows * (4 +
				KingdomWitnessWorkCodec.MaxRowEncodedBytes),
				KingdomWitnessWorkCodec.MaxBookEncodedBytes);
			ClassicAssert.AreEqual(KingdomCivicArtifactsCodec.MaxPayloadBytes +
				KingdomCivicArtifactsCodec.EnvelopeOverheadBytes,
				KingdomCivicArtifactsCodec.MaxEnvelopeBytes);
			KingdomWitnessWorkBook witnesses = new KingdomWitnessWorkBook();
			for (int i = 0; i < KingdomWitnessWorkRules.MaxRows; i++)
			{
				KingdomWitnessWorkSource s = Witness("taf:event:max:" + i, i + 1,
					new string('m', KingdomWitnessWorkRules.MaxTextBytes));
				s.EventKind = new string('k', KingdomWitnessWorkRules.MaxTextBytes);
				s.EventText = new string('e', KingdomWitnessWorkRules.MaxTextBytes);
				s.SnapshotDigest = KingdomWitnessWorkRules.SnapshotDigest(s);
				ClassicAssert.IsTrue(KingdomWitnessWorkRules.TryCapture(witnesses, witnesses.Revision,
					s, out _, out string failure), failure);
			}
			byte[] witnessBytes = KingdomWitnessWorkCodec.Encode(witnesses);
			ClassicAssert.LessOrEqual(witnessBytes.Length, KingdomWitnessWorkCodec.MaxBookEncodedBytes);
			KingdomWitnessWorkSource tooLong = Witness("taf:event:too-long", 1,
				new string('m', KingdomWitnessWorkRules.MaxTextBytes + 1));
			ClassicAssert.IsFalse(KingdomWitnessWorkRules.TryCapture(new KingdomWitnessWorkBook(),
				0, tooLong, out _, out _));
			KingdomWitnessWorkSource utf8Boundary = Witness("taf:event:utf8-boundary", 1,
				new string('\u00e9', KingdomWitnessWorkRules.MaxTextBytes / 2));
			ClassicAssert.IsTrue(KingdomWitnessWorkRules.TryCapture(new KingdomWitnessWorkBook(), 0,
				utf8Boundary, out _, out string utf8Failure), utf8Failure);
			KingdomWitnessWorkSource utf8PlusOne = Witness("taf:event:utf8-plus-one", 1,
				new string('\u00e9', KingdomWitnessWorkRules.MaxTextBytes / 2 + 1));
			ClassicAssert.IsFalse(KingdomWitnessWorkRules.TryCapture(new KingdomWitnessWorkBook(), 0,
				utf8PlusOne, out _, out _));
			KingdomWitnessWorkSource surrogate = Witness("taf:event:surrogate", 1, "maker");
			surrogate.EventText = "bad\ud800";
			Assert.DoesNotThrow(() => ClassicAssert.IsFalse(KingdomWitnessWorkRules.TryCapture(
				new KingdomWitnessWorkBook(), 0, surrogate, out _, out _)));

			KingdomArtifactRecognitionBook recognitions = new KingdomArtifactRecognitionBook();
			for (int i = 0; i < KingdomArtifactRecognitionRules.MaxRows; i++)
			{
				KingdomArtifactSnapshot s = Artifact("taf:object:max:" + i,
					new string('n', KingdomArtifactRecognitionRules.MaxTextBytes));
				s.Blueprint = new string('b', KingdomArtifactRecognitionRules.MaxTextBytes);
				s.DeedText = new string('d', KingdomArtifactRecognitionRules.MaxTextBytes);
				s.SnapshotDigest = KingdomArtifactRecognitionRules.SnapshotDigest(s);
				ClassicAssert.IsTrue(KingdomArtifactRecognitionRules.TryRecognize(recognitions,
					recognitions.Revision, s, KingdomArtifactRecognitionKind.Remark, 0, null,
					20, out _, out string failure), failure);
			}
			byte[] recognitionBytes = KingdomArtifactRecognitionCodec.Encode(recognitions);
			ClassicAssert.LessOrEqual(recognitionBytes.Length,
				KingdomArtifactRecognitionCodec.MaxBookEncodedBytes);
			KingdomArtifactSnapshot badArtifact = Artifact("taf:object:bad-utf8", "bad\ud800");
			Assert.DoesNotThrow(() => ClassicAssert.IsFalse(KingdomArtifactRecognitionRules.TryRecognize(
				new KingdomArtifactRecognitionBook(), 0, badArtifact,
				KingdomArtifactRecognitionKind.Remark, 0, null, 20, out _, out _)));
			KingdomCivicArtifactsEnvelope envelope = new KingdomCivicArtifactsEnvelope
				{ };
			ClassicAssert.IsTrue(envelope.TryBindEmptyIdentity(Realm, out string bindFailure), bindFailure);
			envelope.WitnessWorks = witnesses; envelope.Recognitions = recognitions;
			ClassicAssert.LessOrEqual(KingdomCivicArtifactsCodec.Encode(envelope).Length,
				KingdomCivicArtifactsCodec.MaxEnvelopeBytes);
		}

		[Test]
		public void CivicArtifactsV2BindsExactRealmAndLegacyAuthorityFailsClosed()
		{
			KingdomCivicArtifactsEnvelope current = new KingdomCivicArtifactsEnvelope();
			ClassicAssert.IsTrue(current.TryBindEmptyIdentity(Realm, out string failure), failure);
			byte[] currentBytes = KingdomCivicArtifactsCodec.Encode(current);
			ClassicAssert.AreEqual(2, BitConverter.ToInt32(currentBytes, 4));
			KingdomCivicArtifactsEnvelope loaded = KingdomCivicArtifactsCodec.Decode(currentBytes);
			ClassicAssert.IsTrue(loaded.IdentityBound); ClassicAssert.AreEqual(Realm, loaded.RealmId);
			ClassicAssert.AreNotSame(loaded.WitnessWorks, loaded.Copy().WitnessWorks);
			ClassicAssert.IsTrue(KingdomCivicArtifactsStore.ReadForRealm(currentBytes, OtherRealm,
				out failure).Quarantined); StringAssert.Contains("mismatch", failure);

			KingdomWitnessWorkBook witnesses = new KingdomWitnessWorkBook();
			ClassicAssert.IsTrue(KingdomWitnessWorkRules.TryCapture(witnesses, 0,
				Witness("taf:event:legacy:1", 1, "legacy maker"), out _, out failure), failure);
			byte[] legacy = LegacyEnvelope(KingdomWitnessWorkCodec.Encode(witnesses),
				KingdomArtifactRecognitionCodec.Encode(new KingdomArtifactRecognitionBook()));
			byte[] exact = (byte[])legacy.Clone();
			KingdomCivicArtifactsEnvelope unbound = KingdomCivicArtifactsCodec.Decode(legacy);
			ClassicAssert.IsFalse(unbound.IdentityBound);
			ClassicAssert.IsFalse(unbound.TryBindEmptyIdentity(Realm, out failure));
			ClassicAssert.IsTrue(KingdomCivicArtifactsStore.ReadForRealm(legacy, Realm,
				out failure).Quarantined); CollectionAssert.AreEqual(exact, legacy);

			byte[] emptyLegacy = LegacyEnvelope(KingdomWitnessWorkCodec.Encode(
				new KingdomWitnessWorkBook()), KingdomArtifactRecognitionCodec.Encode(
				new KingdomArtifactRecognitionBook()));
			KingdomCivicArtifactsEnvelope migrated = KingdomCivicArtifactsStore.ReadForRealm(
				emptyLegacy, Realm, out failure);
			ClassicAssert.IsNull(failure); ClassicAssert.IsTrue(migrated.IdentityBound);
			ClassicAssert.AreEqual(Realm, migrated.RealmId);
		}

		[Test]
		public void CivicArtifactsFutureEnvelopeIsOpaqueAuthenticatedAndByteStable()
		{
			KingdomCivicArtifactsEnvelope legacy = KingdomCivicArtifactsStore.ReadOrEmpty(
				null, out string legacyFailure);
			ClassicAssert.IsNotNull(legacy, legacyFailure);
			ClassicAssert.AreEqual(0, legacy.WitnessWorks.Rows.Count);
			ClassicAssert.AreEqual(0, legacy.Recognitions.Rows.Count);
			KingdomCivicArtifactsEnvelope future = new KingdomCivicArtifactsEnvelope
			{
				OpaqueFutureVersion = KingdomCivicArtifactsCodec.CurrentWireVersion + 1,
				OpaqueFuturePayload = new byte[] { 1, 2, 3, 4 }
			};
			ClassicAssert.IsTrue(KingdomCivicArtifactsStore.TryWrite(future, out byte[] bytes,
				out string writeFailure), writeFailure);
			KingdomCivicArtifactsEnvelope loaded = KingdomCivicArtifactsCodec.Decode(bytes);
			ClassicAssert.IsTrue(loaded.IsOpaqueFuture);
			CollectionAssert.AreEqual(bytes, KingdomCivicArtifactsCodec.Encode(loaded));
			byte[] corrupt = (byte[])bytes.Clone(); corrupt[12] ^= 1;
			Assert.Throws<InvalidDataException>(() =>
				KingdomCivicArtifactsCodec.Decode(corrupt));
			KingdomCivicArtifactsEnvelope quarantined =
				KingdomCivicArtifactsStore.ReadOrEmpty(corrupt, out string failure);
			ClassicAssert.IsTrue(quarantined.Quarantined); ClassicAssert.IsNotNull(failure);
			ClassicAssert.IsFalse(KingdomCivicArtifactsStore.TryWrite(quarantined, out _, out _));
		}

		private static KingdomWitnessWorkSource Witness(string EventId, int Maker, string Name)
		{
			KingdomWitnessWorkSource s = new KingdomWitnessWorkSource { EventId = EventId,
				SettlementId = "taf:settlement:seat",
				EventKind = KingdomWitnessWorkRules.RaisingAdapterKind,
				EventText = "the west cistern was sealed", ClosedTick = 10,
				MakerResidentId = Maker, MakerName = Name };
			s.SnapshotDigest = KingdomWitnessWorkRules.SnapshotDigest(s); return s;
		}

		private static KingdomArtifactSnapshot Artifact(string Id, string Name)
		{
			KingdomArtifactSnapshot s = new KingdomArtifactSnapshot { ObjectId = Id,
				Blueprint = "Fullerite Long Sword", DisplayName = Name,
				OwnerId = "taf:owner:player", LocationId = "taf:zone:seat:10:11",
				DeedId = "taf:deed:reef", DeedText = "the crossing of the rusted reef",
				ObservedTick = 15 };
			s.SnapshotDigest = KingdomArtifactRecognitionRules.SnapshotDigest(s); return s;
		}

		private static string MarkerProof(int Version, string RealmId, string SettlementId,
			string WorkId, string Digest, string CarrierReceiptId, string ObjectId,
			string EngineId, string ZoneId, string ConstructionReceiptId, int X, int Y,
			string Description)
		{
			return KingdomWitnessWorkRules.ProjectionProof(Version, RealmId, SettlementId,
				WorkId, Digest, CarrierReceiptId, ObjectId, EngineId, ZoneId,
				ConstructionReceiptId, X, Y, Description);
		}

		private static byte[] LegacyEnvelope(byte[] Witness, byte[] Recognition)
		{
			using (MemoryStream payload = new MemoryStream())
			using (BinaryWriter nested = new BinaryWriter(payload, new UTF8Encoding(false, true), true))
			{
				nested.Write(Witness.Length); nested.Write(Witness); nested.Write(Recognition.Length);
				nested.Write(Recognition); nested.Flush(); byte[] body = payload.ToArray();
				using (MemoryStream frame = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(frame))
				{
					writer.Write(0x41434654); writer.Write(1); writer.Write(body.Length);
					writer.Write(body); writer.Flush(); byte[] authenticated = frame.ToArray();
					using (SHA256 sha = SHA256.Create()) writer.Write(sha.ComputeHash(authenticated));
					writer.Flush(); return frame.ToArray();
				}
			}
		}
	}
}
#endif
