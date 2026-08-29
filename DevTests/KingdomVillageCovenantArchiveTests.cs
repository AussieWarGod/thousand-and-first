#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The archive as a whole: what it will hold, what it refuses, and what it does to its own
	/// bytes on the way to and from a save.
	/// </summary>
	[TestFixture]
	public sealed class KingdomVillageCovenantArchiveTests
	{
		private const string Realm = KingdomVillageCovenantTests.Realm;
		private const string OtherRealm = KingdomVillageCovenantTests.OtherRealm;

		/// <summary>A distinct canonical founding transaction per seed: thirty-two lower-case hex
		/// digits, the first eight of them the seed itself so no two ever collide.</summary>
		private static string Nonce(int seed)
		{
			return seed.ToString("x8") + "0123456789abcdef01234567";
		}

		private static KingdomVillageCovenantReceipt Nth(int seed)
		{
			return KingdomVillageCovenantTests.Row(Nonce(seed), "Village" + seed,
				"the people of Village " + seed);
		}

		// ---- binding --------------------------------------------------------------------

		[Test]
		public void AnEmptyArchiveBindsOnceAndThenOnlyConfirmsTheSameRealm()
		{
			KingdomVillageCovenantArchive archive = KingdomVillageCovenantTests.Bound();
			Assert.AreEqual(Realm, archive.RealmId);
			Assert.IsTrue(archive.IdentityBound);
			Assert.IsTrue(KingdomVillageCovenantRules.TryBindEmptyIdentity(archive, Realm,
				out string same), same);
			Assert.IsFalse(KingdomVillageCovenantRules.TryBindEmptyIdentity(archive, OtherRealm,
				out string other));
			StringAssert.Contains("belongs to another realm", other);
		}

		[Test]
		public void AnArchiveCarryingCovenantsCannotBeAdoptedByAnotherRealm()
		{
			KingdomVillageCovenantArchive archive =
				KingdomVillageCovenantTests.With(KingdomVillageCovenantTests.Row());
			Assert.IsFalse(KingdomVillageCovenantRules.TryBindEmptyIdentity(archive, OtherRealm,
				out string failure));
			StringAssert.Contains("belongs to another realm", failure);
		}

		[Test]
		public void AnUnboundArchiveCarryingCovenantsIsRefusedOutright()
		{
			KingdomVillageCovenantArchive archive = new KingdomVillageCovenantArchive();
			archive.Rows.Add(KingdomVillageCovenantTests.Row());
			archive.Revision = archive.Rows.Count;
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidate(archive, out string failure));
			StringAssert.Contains("unbound covenant archive is carrying covenants", failure);
		}

		// ---- appending ------------------------------------------------------------------

		[Test]
		public void AnExactReplayOfOneRiteChangesNothingAtAllIncludingTheRevision()
		{
			KingdomVillageCovenantReceipt row = KingdomVillageCovenantTests.Row();
			KingdomVillageCovenantArchive first = KingdomVillageCovenantTests.With(row);
			Assert.AreEqual(1L, first.Revision);

			Assert.IsTrue(KingdomVillageCovenantRules.TryAppend(first, row.Copy(), Realm,
				out KingdomVillageCovenantArchive again, out KingdomVillageCovenantAppend outcome,
				out KingdomVillageCovenantReceipt effective, out string failure), failure);
			Assert.IsTrue(KingdomVillageCovenantRules.Same(row, effective));
			Assert.AreEqual(KingdomVillageCovenantAppend.AlreadyRecorded, outcome);
			Assert.AreSame(first, again, "an exact replay must not build a new archive");
			Assert.AreEqual(1L, again.Revision, "an exact replay must spend no revision");
			Assert.AreEqual(1, again.Rows.Count);
		}

		[Test]
		public void ADifferentCovenantUnderTheSameTransactionIsRefusedAndTheOriginalIsKept()
		{
			KingdomVillageCovenantReceipt row = KingdomVillageCovenantTests.Row();
			KingdomVillageCovenantArchive archive = KingdomVillageCovenantTests.With(row);
			KingdomVillageCovenantReceipt conflicting = KingdomVillageCovenantTests.Row(
				display: "a village that never agreed");
			Assert.IsFalse(KingdomVillageCovenantRules.TryAppend(archive, conflicting, Realm,
				out KingdomVillageCovenantArchive next, out _, out _, out string failure));
			StringAssert.Contains("kept rather than replaced", failure);
			Assert.IsNull(next);
			Assert.AreEqual(1, archive.Rows.Count);
			Assert.AreEqual(row.ReceiptId, archive.Rows[0].ReceiptId);
			Assert.AreEqual(1L, archive.Revision);
		}

		[Test]
		public void AppendingLeavesTheArchiveItWasGivenExactlyAsItFoundIt()
		{
			KingdomVillageCovenantArchive first =
				KingdomVillageCovenantTests.With(KingdomVillageCovenantTests.Row());
			Assert.IsTrue(KingdomVillageCovenantRules.TryAppend(first, Nth(3), Realm,
				out KingdomVillageCovenantArchive second, out _, out _, out string failure), failure);
			Assert.AreEqual(1, first.Rows.Count, "the archive read from must not have grown");
			Assert.AreEqual(1L, first.Revision);
			Assert.AreEqual(2, second.Rows.Count);
			Assert.AreEqual(2L, second.Revision);
		}

		[Test]
		public void CovenantsAreHeldInOneCanonicalOrderWhateverOrderTheyArrivedIn()
		{
			KingdomVillageCovenantReceipt a = Nth(1);
			KingdomVillageCovenantReceipt b = Nth(2);
			KingdomVillageCovenantReceipt c = Nth(3);
			KingdomVillageCovenantArchive forward = KingdomVillageCovenantTests.With(a, b, c);
			KingdomVillageCovenantArchive backward = KingdomVillageCovenantTests.With(c, b, a);
			Assert.AreEqual(3, forward.Rows.Count);
			for (int i = 0; i < forward.Rows.Count; i++)
				Assert.AreEqual(forward.Rows[i].ReceiptId, backward.Rows[i].ReceiptId,
					"the same covenants must sit in the same order however they arrived");
			Assert.IsTrue(KingdomVillageCovenantCodec.TryEncode(forward, out byte[] one,
				out string first), first);
			Assert.IsTrue(KingdomVillageCovenantCodec.TryEncode(backward, out byte[] two,
				out string second), second);
			CollectionAssert.AreEqual(one, two, "one set of covenants has one set of bytes");
		}

		[Test]
		public void AnArchiveWhoseRowsAreOutOfOrderIsRefused()
		{
			KingdomVillageCovenantArchive archive =
				KingdomVillageCovenantTests.With(Nth(1), Nth(2));
			KingdomVillageCovenantReceipt swap = archive.Rows[0];
			archive.Rows[0] = archive.Rows[1];
			archive.Rows[1] = swap;
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidate(archive, out string failure));
			StringAssert.Contains("out of their canonical order", failure);
		}

		[Test]
		public void AnArchiveHoldingOneReceiptTwiceIsRefused()
		{
			KingdomVillageCovenantArchive archive =
				KingdomVillageCovenantTests.With(KingdomVillageCovenantTests.Row());
			archive.Rows.Add(archive.Rows[0].Copy());
			archive.Revision = archive.Rows.Count;
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidate(archive, out string failure));
			StringAssert.Contains("same receipt twice", failure);
		}

		/// <summary>
		/// Two different covenants under one founding transaction are two accounts of one rite, and
		/// the archive holds neither over the other. Their receipt ids differ, so the sorted-unique
		/// rule cannot catch this; the transaction rule is the only thing that does.
		/// </summary>
		[Test]
		public void TwoCovenantsClaimingOneFoundingTransactionAreRefused()
		{
			KingdomVillageCovenantArchive archive =
				KingdomVillageCovenantTests.With(KingdomVillageCovenantTests.Row());
			KingdomVillageCovenantReceipt twin =
				KingdomVillageCovenantTests.Row(display: "a village that never agreed");
			Assert.AreNotEqual(archive.Rows[0].ReceiptId, twin.ReceiptId,
				"the two rows really do have different names");
			archive.Rows.Add(twin);
			archive.Rows.Sort((a, b) => string.CompareOrdinal(a.ReceiptId, b.ReceiptId));
			archive.Revision = archive.Rows.Count;
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidate(archive, out string failure));
			StringAssert.Contains("claim one founding transaction", failure);
		}

		/// <summary>
		/// A row keeps its own realm, and an archive will not hold one that names a different one.
		/// A covenant that took its realm from whatever archive it was sitting in would change
		/// hands by being moved.
		/// </summary>
		[Test]
		public void AnArchiveRefusesARowThatNamesAnotherRealm()
		{
			KingdomVillageCovenantArchive archive = KingdomVillageCovenantTests.Bound();
			archive.Rows.Add(KingdomVillageCovenantTests.Row(realm: OtherRealm));
			archive.Revision = archive.Rows.Count;
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidate(archive, out string failure));
			StringAssert.Contains("names another realm than the archive holding it", failure);
		}

		/// <summary>
		/// A payload with something after its last covenant, and a covenant with something after
		/// its last field. Both carry a digest that verifies, so only the reader's own
		/// end-of-stream checks can refuse them.
		/// </summary>
		[Test]
		public void BytesAfterTheLastCovenantOrTheEndOfOneAreRefused()
		{
			byte[] sound = Encoded();
			int lengthAt = KingdomVillageCovenantCodec.MagicBytes
				+ KingdomVillageCovenantCodec.VersionBytes;
			int start = lengthAt + KingdomVillageCovenantCodec.LengthBytes;
			int declared = KingdomVillageCovenantCodec.ReadInt32(sound, lengthAt);

			// One byte more in the payload than the covenants account for.
			byte[] trailing = new byte[sound.Length + 1];
			Array.Copy(sound, 0, trailing, 0, start + declared);
			trailing[start + declared] = 0x7F;
			Array.Copy(sound, start + declared, trailing, start + declared + 1,
				KingdomVillageCovenantCodec.DigestBytes);
			Write(trailing, lengthAt, declared + 1);
			KingdomVillageCovenantArchive afterCovenants =
				KingdomVillageCovenantCodec.Decode(Reseal(trailing));
			Assert.AreEqual(KingdomVillageCovenantState.Quarantined, afterCovenants.State);
			StringAssert.Contains("bytes after the last covenant", afterCovenants.Fault);

			// The same byte, but claimed by the row rather than left after it.
			int rowAt = start + 4 + KingdomVillageCovenantCodec.MaxRealmIdBytes + 1
				+ KingdomVillageCovenantCodec.HeaderBytes;
			byte[] fatRow = (byte[])trailing.Clone();
			Write(fatRow, rowAt, KingdomVillageCovenantCodec.ReadInt32(fatRow, rowAt) + 1);
			KingdomVillageCovenantArchive afterFields =
				KingdomVillageCovenantCodec.Decode(Reseal(fatRow));
			Assert.AreEqual(KingdomVillageCovenantState.Quarantined, afterFields.State);
			StringAssert.Contains("bytes after the end of a covenant row", afterFields.Fault);
		}

		private static void Write(byte[] bytes, int offset, int value)
		{
			for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (i * 8));
		}

		private static byte[] Reseal(byte[] bytes)
		{
			byte[] edited = (byte[])bytes.Clone();
			int body = edited.Length - KingdomVillageCovenantCodec.DigestBytes;
			using (System.Security.Cryptography.SHA256 sha =
				System.Security.Cryptography.SHA256.Create())
				Buffer.BlockCopy(sha.ComputeHash(edited, 0, body), 0, edited, body,
					KingdomVillageCovenantCodec.DigestBytes);
			return edited;
		}

		[Test]
		public void TheArchiveFillsUpAndRefusesTheNextWithoutLosingOneEarlierCovenant()
		{
			KingdomVillageCovenantArchive archive = KingdomVillageCovenantTests.Bound();
			List<string> recorded = new List<string>();
			for (int i = 0; i < KingdomVillageCovenantArchive.MaxRows; i++)
			{
				KingdomVillageCovenantReceipt row = Nth(i);
				Assert.IsTrue(KingdomVillageCovenantRules.TryAppend(archive, row, Realm,
					out archive, out _, out _, out string failure), failure);
				recorded.Add(row.ReceiptId);
			}
			Assert.AreEqual(KingdomVillageCovenantArchive.MaxRows, archive.Rows.Count);

			Assert.IsFalse(KingdomVillageCovenantRules.TryAppend(archive,
				Nth(KingdomVillageCovenantArchive.MaxRows), Realm,
				out KingdomVillageCovenantArchive next, out _, out _, out string full));
			StringAssert.Contains("is full at", full);
			StringAssert.Contains("every earlier one is kept", full);
			Assert.IsNull(next);
			Assert.AreEqual(KingdomVillageCovenantArchive.MaxRows, archive.Rows.Count);
			for (int i = 0; i < recorded.Count; i++)
				Assert.IsTrue(archive.Rows.Exists(r => r.ReceiptId == recorded[i]),
					"a full archive must still hold every covenant it already had");
		}

		[Test]
		public void AFullArchiveStillRecognisesAReplayOfSomethingItAlreadyHolds()
		{
			KingdomVillageCovenantArchive archive = KingdomVillageCovenantTests.Bound();
			KingdomVillageCovenantReceipt first = Nth(0);
			for (int i = 0; i < KingdomVillageCovenantArchive.MaxRows; i++)
				Assert.IsTrue(KingdomVillageCovenantRules.TryAppend(archive,
					i == 0 ? first : Nth(i), Realm, out archive, out _, out _, out string failure),
					failure);
			Assert.IsTrue(KingdomVillageCovenantRules.TryAppend(archive, first.Copy(), Realm,
				out KingdomVillageCovenantArchive same, out KingdomVillageCovenantAppend outcome,
				out _, out string replay), replay);
			Assert.AreEqual(KingdomVillageCovenantAppend.AlreadyRecorded, outcome);
			Assert.AreSame(archive, same);
		}

		/// <summary>
		/// The revision is the archive's own length and nothing else. An append-only history that
		/// only ever grows by one has exactly one honest counter, so a forged counter &mdash; an
		/// exhausted one most of all &mdash; is refused rather than believed, and one set of
		/// covenants can only ever be spelled one way on the wire.
		/// </summary>
		[Test]
		public void TheRevisionIsTheArchivesOwnLengthAndAForgedOneIsRefused()
		{
			KingdomVillageCovenantArchive empty = KingdomVillageCovenantTests.Bound();
			Assert.AreEqual(0L, empty.Revision);
			KingdomVillageCovenantArchive one =
				KingdomVillageCovenantTests.With(KingdomVillageCovenantTests.Row());
			Assert.AreEqual(1L, one.Revision);
			KingdomVillageCovenantArchive two = KingdomVillageCovenantTests.With(Nth(1), Nth(2));
			Assert.AreEqual(2L, two.Revision);

			foreach (long forged in new[] { long.MaxValue, 7L, -1L })
			{
				KingdomVillageCovenantArchive archive = KingdomVillageCovenantTests.Bound();
				archive.Revision = forged;
				Assert.IsFalse(KingdomVillageCovenantRules.TryValidate(archive, out string invalid),
					"revision " + forged + " must not validate against an empty archive");
				StringAssert.Contains("it is its own length", invalid);
				Assert.IsFalse(KingdomVillageCovenantRules.TryAppend(archive,
					KingdomVillageCovenantTests.Row(), Realm,
					out KingdomVillageCovenantArchive next, out _, out _, out string failure));
				StringAssert.Contains("it is its own length", failure);
				Assert.IsNull(next);
			}
		}

		[Test]
		public void AnAppendToAnArchiveBoundToAnotherRealmIsRefused()
		{
			KingdomVillageCovenantArchive archive = KingdomVillageCovenantTests.Bound(OtherRealm);
			Assert.IsFalse(KingdomVillageCovenantRules.TryAppend(archive,
				KingdomVillageCovenantTests.Row(), Realm, out _, out _, out _, out string failure));
			StringAssert.Contains("not bound to this exact realm", failure);
		}

		// ---- the wire -------------------------------------------------------------------

		[Test]
		public void AnArchiveSurvivesItsOwnWireExactly()
		{
			KingdomVillageCovenantArchive archive =
				KingdomVillageCovenantTests.With(Nth(1), Nth(2), Nth(3));
			Assert.IsTrue(KingdomVillageCovenantCodec.TryEncode(archive, out byte[] bytes,
				out string failure), failure);
			KingdomVillageCovenantArchive back = KingdomVillageCovenantCodec.Decode(bytes);
			Assert.AreEqual(KingdomVillageCovenantState.Compatible, back.State, back.Fault);
			Assert.AreEqual(archive.RealmId, back.RealmId);
			Assert.AreEqual(archive.Revision, back.Revision);
			Assert.AreEqual(archive.Rows.Count, back.Rows.Count);
			for (int i = 0; i < archive.Rows.Count; i++)
				Assert.IsTrue(KingdomVillageCovenantRules.Same(archive.Rows[i], back.Rows[i]));
			Assert.IsTrue(KingdomVillageCovenantCodec.TryEncode(back, out byte[] again,
				out string second), second);
			CollectionAssert.AreEqual(bytes, again);
		}

		[Test]
		public void AnUnboundArchiveHasNoRealmToBeSavedUnderAndIsNotWritten()
		{
			Assert.IsFalse(KingdomVillageCovenantCodec.TryEncode(
				new KingdomVillageCovenantArchive(), out byte[] bytes, out string failure));
			StringAssert.Contains("no realm to be saved under", failure);
			Assert.IsNull(bytes);
		}

		[TestCase(0, TestName = "the magic")]
		[TestCase(5, TestName = "the wire revision")]
		[TestCase(9, TestName = "the framed length")]
		[TestCase(60, TestName = "a byte of the payload")]
		public void OneChangedByteAnywhereInTheFrameQuarantinesTheArchive(int offset)
		{
			byte[] bytes = Encoded();
			bytes[offset] ^= 0x01;
			KingdomVillageCovenantArchive back = KingdomVillageCovenantCodec.Decode(bytes);
			Assert.AreEqual(KingdomVillageCovenantState.Quarantined, back.State);
			StringAssert.Contains("would not read", back.Fault);
			CollectionAssert.AreEqual(bytes, back.OpaquePayload,
				"a refusal keeps the real bytes as its evidence");
		}

		[Test]
		public void AChangedDigestTailQuarantinesRatherThanBeingRecomputed()
		{
			byte[] bytes = Encoded();
			bytes[bytes.Length - 1] ^= 0x01;
			KingdomVillageCovenantArchive back = KingdomVillageCovenantCodec.Decode(bytes);
			Assert.AreEqual(KingdomVillageCovenantState.Quarantined, back.State);
			StringAssert.Contains("digest no longer covers", back.Fault);
		}

		[Test]
		public void TrailingOrMissingBytesAreBothRefused()
		{
			byte[] bytes = Encoded();
			byte[] longer = new byte[bytes.Length + 1];
			Array.Copy(bytes, longer, bytes.Length);
			Assert.AreEqual(KingdomVillageCovenantState.Quarantined,
				KingdomVillageCovenantCodec.Decode(longer).State);
			byte[] shorter = new byte[bytes.Length - 1];
			Array.Copy(bytes, shorter, shorter.Length);
			Assert.AreEqual(KingdomVillageCovenantState.Quarantined,
				KingdomVillageCovenantCodec.Decode(shorter).State);
		}

		[Test]
		public void BytesPastTheCapAreRefusedWithoutBeingCopiedAtAll()
		{
			KingdomVillageCovenantArchive back = KingdomVillageCovenantCodec.Decode(
				new byte[KingdomVillageCovenantCodec.MaxEnvelopeBytes + 1]);
			Assert.AreEqual(KingdomVillageCovenantState.Quarantined, back.State);
			StringAssert.Contains("-byte cap this family accepts", back.Fault);
			Assert.IsNull(back.OpaquePayload,
				"an over-cap payload is refused before anything is allocated for it");
		}

		[Test]
		public void NoBytesAtAllIsARefusalRatherThanAnEmptyArchive()
		{
			KingdomVillageCovenantArchive back = KingdomVillageCovenantCodec.Decode(null);
			Assert.AreEqual(KingdomVillageCovenantState.Quarantined, back.State);
			StringAssert.Contains("no bytes at all", back.Fault);
		}

		/// <summary>
		/// The decoder takes one copy before it judges anything, so a caller that keeps hold of the
		/// array it handed over cannot change what was read after the digest was checked.
		/// </summary>
		[Test]
		public void EditingTheCallersArrayAfterDecodingChangesNothingThatWasRead()
		{
			byte[] bytes = Encoded();
			KingdomVillageCovenantArchive back = KingdomVillageCovenantCodec.Decode(bytes);
			string realm = back.RealmId;
			string receipt = back.Rows[0].ReceiptId;
			for (int i = 0; i < bytes.Length; i++) bytes[i] = 0x5A;
			Assert.AreEqual(KingdomVillageCovenantState.Compatible, back.State);
			Assert.AreEqual(realm, back.RealmId);
			Assert.AreEqual(receipt, back.Rows[0].ReceiptId);
		}

		[Test]
		public void AQuarantinesEvidenceIsItsOwnCopyAndNotTheCallersArray()
		{
			byte[] bytes = Encoded();
			bytes[0] ^= 0x01;
			KingdomVillageCovenantArchive back = KingdomVillageCovenantCodec.Decode(bytes);
			Assert.AreEqual(KingdomVillageCovenantState.Quarantined, back.State);
			byte first = back.OpaquePayload[0];
			bytes[0] ^= 0x40;
			Assert.AreEqual(first, back.OpaquePayload[0],
				"evidence a caller can still edit is not evidence");
		}

		internal static byte[] Encoded()
		{
			KingdomVillageCovenantArchive archive =
				KingdomVillageCovenantTests.With(KingdomVillageCovenantTests.Row());
			Assert.IsTrue(KingdomVillageCovenantCodec.TryEncode(archive, out byte[] bytes,
				out string failure), failure);
			return bytes;
		}
	}
}
#endif
