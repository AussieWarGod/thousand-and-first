using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomCommunalRiteRulesTests
	{
		private const string RealmTransaction = "ffffffffffffffffffffffffffffffff";

		private static string Realm()
		{
			Assert.IsTrue(KingdomIdentityRules.TryMintRealm(RealmTransaction,
				out string realm, out KingdomIdentityFault fault), fault.ToString());
			return realm;
		}

		private static KingdomFirstFeastReceipt Practice(string transaction, long decided = 20L)
		{
			Assert.IsTrue(KingdomIdentityRules.TryMintSettlement(Realm(), transaction,
				out string settlement, out KingdomIdentityFault fault), fault.ToString());
			KingdomFirstFeastDeed deed = new KingdomFirstFeastDeed {
				SettlementId = settlement, SettlementName = "Kyakukya",
				DeedText = KingdomFirstFeastRules.AuthoredDeed, DeedTick = 11L,
				GuestTerminalReceiptId = "taf:growth-first-guest-terminal:" + new string('a', 64),
				GuestTerminalDigest = new string('b', 64), GuestTerminalTick = 10L,
				AdventureEventId = "taf:adventure:" + transaction,
				AdventureFingerprint = new string('c', 64) };
			Assert.IsTrue(KingdomFirstFeastRules.TryBuildDeedId(deed, out deed.DeedId));
			return new KingdomFirstFeastReceipt
			{
				Phase = KingdomFirstFeastPhase.Adopted, Choice = KingdomFirstFeastChoice.Adopt,
				Generation = 1, SettlementId = settlement, SettlementName = "Kyakukya",
				DeedId = deed.DeedId, DeedText = deed.DeedText, DeedTick = deed.DeedTick,
				GuestTerminalReceiptId = deed.GuestTerminalReceiptId,
				GuestTerminalDigest = deed.GuestTerminalDigest,
				GuestTerminalTick = deed.GuestTerminalTick,
				AdventureEventId = deed.AdventureEventId,
				AdventureFingerprint = deed.AdventureFingerprint,
				ProposerResidentId = 1, ProposerName = "Ava", WitnessResidentId = 2,
				WitnessName = "Yla", DishName = KingdomFirstFeastRules.AuthoredDish,
				Ingredients = KingdomFirstFeastRules.AuthoredIngredients,
				OfferedDedication = KingdomFirstFeastRules.OfferedDedication,
				PracticeId = KingdomFirstFeastRules.PracticePrefix
					+ deed.DeedId.Substring(KingdomFirstFeastRules.DeedPrefix.Length),
				OfferedTick = 12L, DecidedTick = decided, EnableEpoch = 1L
			};
		}

		private static KingdomCommunalRiteBook Bound()
		{
			KingdomCommunalRiteBook book = new KingdomCommunalRiteBook();
			Assert.IsTrue(KingdomCommunalRiteRules.TryBindEmptyIdentity(book, Realm(),
				out string failure), failure);
			return book;
		}

		[Test]
		public void ExactAffirmativePracticePreparesCommitsThenEndsAttendedOrSuppressed()
		{
			KingdomCommunalRiteBook book = Bound();
			KingdomFirstFeastReceipt practice = Practice("00000000000000000000000000000001");
			Assert.IsTrue(KingdomCommunalRiteRules.TryPracticeSubject(practice.PracticeId,
				out int subject));
			string eventId = KingdomCommunalRiteRules.EventId(practice.SettlementId, 30L, subject);
			Assert.IsTrue(KingdomCommunalRiteRules.TryPrepare(book, book.Revision, practice,
				eventId, 30L, 2L, out KingdomCommunalRiteReceipt committed,
				out string failure), failure);
			Assert.AreEqual(KingdomCommunalRitePhase.Prepared, committed.Phase);
			long revision = book.Revision;
			Assert.IsTrue(KingdomCommunalRiteRules.TryPrepare(book, 0L, practice, eventId,
				30L, 2L, out _, out failure), failure);
			Assert.AreEqual(revision, book.Revision);
			Assert.IsFalse(KingdomCommunalRiteRules.TryFinish(book, book.Revision,
				practice.PracticeId, eventId, true, 31L, out _, out _),
				"physical attendance cannot be published from Prepared");
			Assert.IsFalse(KingdomCommunalRiteRules.TryCommit(book, book.Revision + 1L,
				practice.PracticeId, eventId, out _, out _));
			Assert.AreEqual(KingdomCommunalRitePhase.Prepared, book.Rows[0].Phase);
			Assert.IsTrue(KingdomCommunalRiteRules.TryCommit(book, book.Revision,
				practice.PracticeId, eventId, out committed, out failure), failure);
			Assert.AreEqual(KingdomCommunalRitePhase.Committed, committed.Phase);
			Assert.IsTrue(KingdomCommunalRiteRules.TryFinish(book, book.Revision,
				practice.PracticeId, eventId, true, 40L, out KingdomCommunalRiteReceipt ended,
				out failure), failure);
			Assert.AreEqual(KingdomCommunalRitePhase.Attended, ended.Phase);
			Assert.IsFalse(KingdomCommunalRiteRules.TryFinish(book, book.Revision,
				practice.PracticeId, eventId, false, 41L, out _, out _));
			Assert.IsTrue(KingdomCommunalRiteRules.TryValidate(book, out failure), failure);
		}

		[Test]
		public void PreparedCancellationIsTerminalAndCannotLaterBecomeAttendance()
		{
			KingdomCommunalRiteBook book = Bound();
			KingdomFirstFeastReceipt practice = Practice("00000000000000000000000000000009");
			KingdomCommunalRiteRules.TryPracticeSubject(practice.PracticeId, out int subject);
			string eventId = KingdomCommunalRiteRules.EventId(practice.SettlementId, 30L, subject);
			Assert.IsTrue(KingdomCommunalRiteRules.TryPrepare(book, book.Revision, practice,
				eventId, 30L, 1L, out _, out string failure), failure);
			Assert.IsTrue(KingdomCommunalRiteRules.TryFinish(book, book.Revision,
				practice.PracticeId, eventId, false, 31L,
				out KingdomCommunalRiteReceipt cancelled, out failure), failure);
			Assert.AreEqual(KingdomCommunalRitePhase.Suppressed, cancelled.Phase);
			long revision = book.Revision;
			Assert.IsTrue(KingdomCommunalRiteRules.TryFinish(book, 0L,
				practice.PracticeId, eventId, false, 99L, out _, out failure), failure);
			Assert.AreEqual(revision, book.Revision);
			Assert.IsFalse(KingdomCommunalRiteRules.TryFinish(book, book.Revision,
				practice.PracticeId, eventId, true, 100L, out _, out _));
		}

		[Test]
		public void ExactReadyRecoveryCanCorrectSuppressionRaceOnlyThroughRecoveryAuthority()
		{
			KingdomCommunalRiteBook book = Bound();
			KingdomFirstFeastReceipt practice = Practice("00000000000000000000000000000019");
			KingdomCommunalRiteRules.TryPracticeSubject(practice.PracticeId, out int subject);
			string eventId = KingdomCommunalRiteRules.EventId(practice.SettlementId, 30L, subject);
			Assert.IsTrue(KingdomCommunalRiteRules.TryPrepare(book, book.Revision, practice,
				eventId, 30L, 1L, out _, out string failure), failure);
			Assert.IsTrue(KingdomCommunalRiteRules.TryCommit(book, book.Revision,
				practice.PracticeId, eventId, out _, out failure), failure);
			Assert.IsTrue(KingdomCommunalRiteRules.TryFinish(book, book.Revision,
				practice.PracticeId, eventId, false, 31L, out _, out failure), failure);
			long revision = book.Revision;
			Assert.IsFalse(KingdomCommunalRiteRules.TryRecoverReady(book, revision + 1L,
				practice.PracticeId, eventId, 32L, out _, out _));
			Assert.IsTrue(KingdomCommunalRiteRules.TryRecoverReady(book, revision,
				practice.PracticeId, eventId, 32L,
				out KingdomCommunalRiteReceipt recovered, out failure), failure);
			Assert.AreEqual(KingdomCommunalRitePhase.Attended, recovered.Phase);
			Assert.IsTrue(KingdomCommunalRiteRules.TryValidate(book, out failure), failure);
		}

		[Test]
		public void CodecRoundTripsAndCurrentMalformedQuarantinesByteExactly()
		{
			KingdomCommunalRiteBook book = Bound();
			KingdomFirstFeastReceipt practice = Practice("00000000000000000000000000000002");
			KingdomCommunalRiteRules.TryPracticeSubject(practice.PracticeId, out int subject);
			string eventId = KingdomCommunalRiteRules.EventId(practice.SettlementId, 30L, subject);
			Assert.IsTrue(KingdomCommunalRiteRules.TryPrepare(book, book.Revision, practice,
				eventId, 30L, 1L, out _, out string failure), failure);
			Assert.IsTrue(KingdomCommunalRiteRules.TryCommit(book, book.Revision,
				practice.PracticeId, eventId, out _, out failure), failure);
			byte[] wire = KingdomCommunalRiteCodec.EncodeEnvelope(book);
			KingdomCommunalRiteBook restored = KingdomCommunalRiteCodec.DecodeEnvelope(wire);
			Assert.AreEqual(book.Revision, restored.Revision);
			CollectionAssert.AreEqual(wire, KingdomCommunalRiteCodec.EncodeEnvelope(restored));
			byte[] corrupt = (byte[])wire.Clone(); corrupt[corrupt.Length - 1] ^= 1;
			KingdomCommunalRiteBook quarantined =
				KingdomCommunalRiteCodec.DecodeEnvelope(corrupt);
			Assert.AreEqual(KingdomExperienceSchemaState.Quarantined,
				quarantined.SchemaState);
			CollectionAssert.AreEqual(corrupt,
				KingdomCommunalRiteCodec.EncodeEnvelope(quarantined));
		}

		[Test]
		public void FutureWireStaysOpaqueAndHardCapRejectsBeforeDecode()
		{
			MethodInfo frame = typeof(KingdomCommunalRiteCodec).GetMethod("Frame",
				BindingFlags.NonPublic | BindingFlags.Static);
			byte[] future = (byte[])frame.Invoke(null, new object[] { 2, new byte[] { 1, 2, 3 } });
			KingdomCommunalRiteBook unknown = KingdomCommunalRiteCodec.DecodeEnvelope(future);
			Assert.AreEqual(KingdomExperienceSchemaState.Unknown, unknown.SchemaState);
			CollectionAssert.AreEqual(future, KingdomCommunalRiteCodec.EncodeEnvelope(unknown));
			KingdomCommunalRiteBook forged = new KingdomCommunalRiteBook
			{
				SchemaState = KingdomExperienceSchemaState.Unknown, OpaqueWireVersion = 2,
				OpaqueFuturePayload = new byte[] { 1, 2, 3 },
				OpaqueEnvelope = KingdomCommunalRiteCodec.EncodeEnvelope(
					new KingdomCommunalRiteBook())
			};
			Assert.Throws<InvalidDataException>(
				() => KingdomCommunalRiteCodec.EncodeEnvelope(forged));
			Assert.Throws<InvalidDataException>(() => KingdomCommunalRiteCodec.DecodeEnvelope(
				new byte[KingdomCommunalRiteCodec.MaxEnvelopeBytes + 1]));
		}

		[Test]
		public void DeclaredEnvelopeMaximumIsExactAndLeavesExperienceV4Untouched()
		{
			KingdomCommunalRiteBook book = Bound(); int found = 0;
			for (int value = 1; found < KingdomCommunalRiteRules.MaxRows && value < 10000; value++)
			{
				string transaction = value.ToString("x32");
				KingdomFirstFeastReceipt practice = Practice(transaction, long.MaxValue - 1L);
				KingdomCommunalRiteRules.TryPracticeSubject(practice.PracticeId, out int subject);
				if (subject < 1000000000) continue;
				string eventId = KingdomCommunalRiteRules.EventId(practice.SettlementId,
					long.MaxValue, subject);
				Assert.AreEqual(KingdomCommunalRiteRules.MaxEventIdBytes, eventId.Length);
				Assert.IsTrue(KingdomCommunalRiteRules.TryPrepare(book, book.Revision, practice,
					eventId, long.MaxValue, long.MaxValue, out _, out string failure), failure);
				found++;
			}
			Assert.AreEqual(KingdomCommunalRiteRules.MaxRows, found);
			byte[] maximum = KingdomCommunalRiteCodec.EncodeEnvelope(book);
			Assert.AreEqual(KingdomCommunalRiteCodec.MaxEnvelopeBytes, maximum.Length);
			Assert.AreEqual(24 * 1024, KingdomExperienceCodec.MaxEnvelopeBytes);
		}

		[Test]
		public void ByteCasRejectsDriftAndAcceptsOneRevisionOnly()
		{
			byte[] current = KingdomCommunalRiteCodec.EncodeEnvelope(
				new KingdomCommunalRiteBook());
			KingdomCommunalRiteBook next = new KingdomCommunalRiteBook();
			Assert.IsTrue(KingdomCommunalRiteRules.TryBindEmptyIdentity(next, Realm(),
				out string failure), failure);
			Assert.IsFalse(KingdomCommunalRiteCodec.TryPrepareCas(current, "wrong", next,
				out _, out _, out _));
			Assert.IsTrue(KingdomCommunalRiteCodec.TryPrepareCas(current,
				KingdomCommunalRiteCodec.DigestHex(current), next, out byte[] replacement,
				out string digest, out failure), failure);
			Assert.AreEqual(digest, KingdomCommunalRiteCodec.DigestHex(replacement));

			KingdomCommunalRiteBook exhausted = Bound(); exhausted.Revision = long.MaxValue;
			byte[] last = KingdomCommunalRiteCodec.EncodeEnvelope(exhausted);
			KingdomCommunalRiteBook impossible = Bound(); impossible.Revision = long.MaxValue;
			Assert.IsFalse(KingdomCommunalRiteCodec.TryPrepareCas(last,
				KingdomCommunalRiteCodec.DigestHex(last), impossible, out _, out _, out _),
				"byte CAS must refuse the last revision before addition can wrap");
		}

		[Test]
		public void CharterRiteLeavesBothW0CollectionsByteIdentical()
		{
			KingdomExperienceLedger w0 = new KingdomExperienceLedger();
			Assert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(w0, Realm(),
				out string failure), failure);
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(w0);
			KingdomCommunalRiteBook rites = Bound();
			KingdomFirstFeastReceipt practice = Practice(
				"00000000000000000000000000000003");
			Assert.IsTrue(KingdomCommunalRiteRules.TryPracticeSubject(practice.PracticeId,
				out int subject));
			string eventId = KingdomCommunalRiteRules.EventId(practice.SettlementId, 30L,
				subject);
			Assert.IsTrue(KingdomCommunalRiteRules.TryPrepare(rites, rites.Revision, practice,
				eventId, 30L, 1L, out _, out failure), failure);
			Assert.IsTrue(KingdomCommunalRiteRules.TryCommit(rites, rites.Revision,
				practice.PracticeId, eventId, out _, out failure), failure);
			Assert.IsTrue(KingdomCommunalRiteRules.TryFinish(rites, rites.Revision,
				practice.PracticeId, eventId, true, 31L, out _, out failure), failure);
			Assert.AreEqual(0, w0.Audiences.Count);
			Assert.AreEqual(0, w0.BodyReservations.Count);
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(w0));
		}

		[Test]
		public void OptionObservationIsTriStateAndUnreadableCutsPreservePreparedBytes()
		{
			Assert.AreEqual(KingdomCommunalRiteOptionDisposition.Unreadable,
				KingdomCommunalRiteRules.OptionDisposition(false, true, 2L, 2L));
			Assert.AreEqual(KingdomCommunalRiteOptionDisposition.Disabled,
				KingdomCommunalRiteRules.OptionDisposition(true, false, 0L, 2L));
			Assert.AreEqual(KingdomCommunalRiteOptionDisposition.Unreadable,
				KingdomCommunalRiteRules.OptionDisposition(true, true, 0L, 2L));
			Assert.AreEqual(KingdomCommunalRiteOptionDisposition.Current,
				KingdomCommunalRiteRules.OptionDisposition(true, true, 2L, 2L));
			Assert.AreEqual(KingdomCommunalRiteOptionDisposition.SupersededEpoch,
				KingdomCommunalRiteRules.OptionDisposition(true, true, 3L, 2L));

			KingdomCommunalRiteBook book = Bound();
			KingdomFirstFeastReceipt practice = Practice(
				"00000000000000000000000000000004");
			KingdomCommunalRiteRules.TryPracticeSubject(practice.PracticeId, out int subject);
			string eventId = KingdomCommunalRiteRules.EventId(practice.SettlementId, 30L, subject);
			Assert.IsTrue(KingdomCommunalRiteRules.TryPrepare(book, book.Revision, practice,
				eventId, 30L, 2L, out _, out string failure), failure);
			byte[] frozen = KingdomCommunalRiteCodec.EncodeEnvelope(book);
			Assert.AreEqual(KingdomCommunalRiteOptionDisposition.Unreadable,
				KingdomCommunalRiteRules.OptionDisposition(false, true, 2L, 2L));
			CollectionAssert.AreEqual(frozen, KingdomCommunalRiteCodec.EncodeEnvelope(book));
		}
	}
}
