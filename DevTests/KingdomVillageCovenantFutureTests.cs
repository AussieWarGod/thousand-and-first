#if TAF_TESTS
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// What this build does with an archive it may not read, and what the envelope's arithmetic
	/// says about the room it takes.
	/// <para>
	/// The whole of the forward-compatibility promise is here. A build that mistakes a lawful
	/// successor for damage quarantines a founder's real records; a build that mistakes damage for
	/// a lawful successor carries corruption forward and calls it forward compatibility. The
	/// difference between those two is a digest checked before anything is interpreted, and
	/// nothing else.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomVillageCovenantFutureTests
	{
		/// <summary>Frames a payload the way a later build would: same magic, same digest tail,
		/// a revision this build has never heard of.</summary>
		internal static byte[] Forge(int version, byte[] payload)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream,
					new UTF8Encoding(false, true), true))
				{
					writer.Write(KingdomVillageCovenantCodec.Magic);
					writer.Write(version);
					writer.Write(payload.Length);
					writer.Write(payload);
					writer.Flush();
				}
				byte[] framed = stream.ToArray();
				byte[] whole = new byte[framed.Length + KingdomVillageCovenantCodec.DigestBytes];
				Buffer.BlockCopy(framed, 0, whole, 0, framed.Length);
				using (SHA256 sha = SHA256.Create())
				{
					byte[] digest = sha.ComputeHash(framed, 0, framed.Length);
					Buffer.BlockCopy(digest, 0, whole, framed.Length,
						KingdomVillageCovenantCodec.DigestBytes);
				}
				return whole;
			}
		}

		private static byte[] FutureBytes()
		{
			return Forge(KingdomVillageCovenantCodec.CurrentWireVersion + 1,
				new byte[] { 9, 8, 7, 6, 5 });
		}

		[Test]
		public void ALaterBuildsArchiveIsHeldWholeAndNeverCalledDamage()
		{
			byte[] bytes = FutureBytes();
			KingdomVillageCovenantArchive back = KingdomVillageCovenantCodec.Decode(bytes);
			Assert.AreEqual(KingdomVillageCovenantState.FutureOpaque, back.State, back.Fault);
			Assert.AreEqual(KingdomVillageCovenantCodec.CurrentWireVersion + 1, back.OpaqueVersion);
			Assert.AreEqual(0, back.Rows.Count);
			Assert.IsFalse(back.IdentityBound);
			StringAssert.Contains("keep but not read", back.Fault);
			Assert.IsTrue(KingdomVillageCovenantRules.TryValidate(back, out string failure),
				failure);
		}

		[Test]
		public void ALaterBuildsArchiveGoesBackToDiskByteForByte()
		{
			byte[] bytes = FutureBytes();
			KingdomVillageCovenantArchive back = KingdomVillageCovenantCodec.Decode(bytes);
			Assert.IsTrue(KingdomVillageCovenantCodec.TryEncode(back, out byte[] again,
				out string failure), failure);
			CollectionAssert.AreEqual(bytes, again);
			Assert.AreEqual(KingdomVillageCovenantState.FutureOpaque,
				KingdomVillageCovenantCodec.Decode(again).State);
		}

		/// <summary>
		/// A caller's claim that its bytes came from a later build is worth nothing. The retained
		/// bytes are re-inspected from scratch and must verify as a future in their own right.
		/// </summary>
		[Test]
		public void ForgedOpacityIsRefusedBecauseTheRetainedBytesAreRevalidated()
		{
			KingdomVillageCovenantArchive lying = new KingdomVillageCovenantArchive
			{
				State = KingdomVillageCovenantState.FutureOpaque,
				OpaqueVersion = KingdomVillageCovenantCodec.CurrentWireVersion + 1,
				OpaquePayload = new byte[] { 1, 2, 3, 4 },
				Fault = "a claim about bytes that cannot support it"
			};
			Assert.IsFalse(KingdomVillageCovenantCodec.TryEncode(lying, out byte[] bytes,
				out string failure));
			StringAssert.Contains("retained bytes do not verify", failure);
			Assert.IsNull(bytes);
		}

		[Test]
		public void AnArchiveClaimingTheWrongFutureRevisionIsRefused()
		{
			KingdomVillageCovenantArchive back =
				KingdomVillageCovenantCodec.Decode(FutureBytes());
			back.OpaqueVersion = KingdomVillageCovenantCodec.CurrentWireVersion + 2;
			Assert.IsFalse(KingdomVillageCovenantCodec.TryEncode(back, out _, out string failure));
			StringAssert.Contains("while its retained bytes declare", failure);
		}

		[Test]
		public void ThisBuildsOwnBytesCannotBePassedOffAsALaterBuildsAndReemitted()
		{
			KingdomVillageCovenantArchive lying = new KingdomVillageCovenantArchive
			{
				State = KingdomVillageCovenantState.FutureOpaque,
				OpaqueVersion = KingdomVillageCovenantCodec.CurrentWireVersion + 1,
				OpaquePayload = KingdomVillageCovenantArchiveTests.Encoded(),
				Fault = "our own bytes wearing a stranger's coat"
			};
			Assert.IsFalse(KingdomVillageCovenantCodec.TryEncode(lying, out _, out string failure));
			StringAssert.Contains("this build's own revision", failure);
		}

		[Test]
		public void QuarantinedEvidenceIsNeverWrittenBackOverTheRecordsItFailedToRead()
		{
			byte[] bytes = KingdomVillageCovenantArchiveTests.Encoded();
			bytes[0] ^= 0x01;
			KingdomVillageCovenantArchive back = KingdomVillageCovenantCodec.Decode(bytes);
			Assert.AreEqual(KingdomVillageCovenantState.Quarantined, back.State);
			Assert.IsFalse(KingdomVillageCovenantCodec.TryEncode(back, out byte[] again,
				out string failure));
			StringAssert.Contains("quarantined evidence", failure);
			Assert.IsNull(again);
		}

		[Test]
		public void ADamagedLaterRevisionIsDamageRatherThanAFuture()
		{
			byte[] bytes = FutureBytes();
			bytes[bytes.Length - 3] ^= 0x01;
			KingdomVillageCovenantArchive back = KingdomVillageCovenantCodec.Decode(bytes);
			Assert.AreEqual(KingdomVillageCovenantState.Quarantined, back.State,
				"a later revision whose digest fails is not a lawful successor");
		}

		[Test]
		public void ARevisionNoBuildCouldHaveAllocatedIsDamage()
		{
			foreach (int version in new[] { 0, -1, int.MinValue })
			{
				byte[] bytes = Forge(version, new byte[] { 1 });
				Assert.AreEqual(KingdomVillageCovenantState.Quarantined,
					KingdomVillageCovenantCodec.Decode(bytes).State,
					"wire revision " + version + " could never have been allocated");
			}
		}

		/// <summary>
		/// Frames a payload with a chosen magic, so a stranger's bytes can be offered with a digest
		/// that verifies. Without this, every tampering test is answered by the digest and the
		/// checks in front of it are never reached.
		/// </summary>
		private static byte[] ForgeWithMagic(int magic, int version, byte[] payload,
			int declaredLength)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream,
					new UTF8Encoding(false, true), true))
				{
					writer.Write(magic);
					writer.Write(version);
					writer.Write(declaredLength);
					writer.Write(payload);
					writer.Flush();
				}
				byte[] framed = stream.ToArray();
				byte[] whole = new byte[framed.Length + KingdomVillageCovenantCodec.DigestBytes];
				Buffer.BlockCopy(framed, 0, whole, 0, framed.Length);
				using (SHA256 sha = SHA256.Create())
					Buffer.BlockCopy(sha.ComputeHash(framed, 0, framed.Length), 0, whole,
						framed.Length, KingdomVillageCovenantCodec.DigestBytes);
				return whole;
			}
		}

		/// <summary>
		/// Every question the frame asks before the digest has to be asked, not merely present.
		/// These payloads all carry a digest that verifies, so the only thing standing between them
		/// and being believed is the check each one violates.
		/// </summary>
		[Test]
		public void AWellDigestedPayloadIsStillRefusedForEveryFramingFaultInTurn()
		{
			byte[] body = new byte[] { 1, 2, 3, 4 };
			KingdomVillageCovenantArchive foreign = KingdomVillageCovenantCodec.Decode(
				ForgeWithMagic(0x31424654, KingdomVillageCovenantCodec.CurrentWireVersion, body,
					body.Length));
			Assert.AreEqual(KingdomVillageCovenantState.Quarantined, foreign.State);
			StringAssert.Contains("magic is not this family's", foreign.Fault);

			KingdomVillageCovenantArchive impossible = KingdomVillageCovenantCodec.Decode(
				ForgeWithMagic(KingdomVillageCovenantCodec.Magic, 0, body, body.Length));
			Assert.AreEqual(KingdomVillageCovenantState.Quarantined, impossible.State);
			StringAssert.Contains("no build could have allocated", impossible.Fault);

			KingdomVillageCovenantArchive miscounted = KingdomVillageCovenantCodec.Decode(
				ForgeWithMagic(KingdomVillageCovenantCodec.Magic,
					KingdomVillageCovenantCodec.CurrentWireVersion, body, body.Length - 1));
			Assert.AreEqual(KingdomVillageCovenantState.Quarantined, miscounted.State);
			StringAssert.Contains("does not account for every byte", miscounted.Fault);

			KingdomVillageCovenantArchive overCap = KingdomVillageCovenantCodec.Decode(
				ForgeWithMagic(KingdomVillageCovenantCodec.Magic,
					KingdomVillageCovenantCodec.CurrentWireVersion, body,
					KingdomVillageCovenantCodec.MaxPayloadBytes + 1));
			Assert.AreEqual(KingdomVillageCovenantState.Quarantined, overCap.State);
			StringAssert.Contains("does not account for every byte", overCap.Fault);
		}

		/// <summary>
		/// The same technique inside the payload: the digest is recomputed after the edit, so the
		/// only thing that can refuse these is the reader's own arithmetic.
		/// </summary>
		[Test]
		public void AWellDigestedPayloadIsStillRefusedForEveryBodyFaultInTurn()
		{
			byte[] sound = KingdomVillageCovenantArchiveTests.Encoded();
			int start = KingdomVillageCovenantCodec.MagicBytes
				+ KingdomVillageCovenantCodec.VersionBytes
				+ KingdomVillageCovenantCodec.LengthBytes;
			int bound = start + 4 + KingdomVillageCovenantCodec.MaxRealmIdBytes;

			byte[] strangeFlag = Redigest(sound, bound, 2);
			KingdomVillageCovenantArchive flagged = KingdomVillageCovenantCodec.Decode(strangeFlag);
			Assert.AreEqual(KingdomVillageCovenantState.Quarantined, flagged.State);
			StringAssert.Contains("neither bound nor unbound", flagged.Fault);

			// The row length prefix sits after the identity frame and the twenty-byte header. A
			// row of zero bytes is refused by the row bound, before any attempt to read one.
			int rowLength = bound + 1 + KingdomVillageCovenantCodec.HeaderBytes;
			foreach (int declared in new[] { 0, KingdomVillageCovenantCodec.MaxRowBytes + 1 })
			{
				KingdomVillageCovenantArchive row = KingdomVillageCovenantCodec.Decode(
					RedigestInt32(sound, rowLength, declared));
				Assert.AreEqual(KingdomVillageCovenantState.Quarantined, row.State,
					"a row declaring " + declared + " bytes is outside the row bound");
				StringAssert.Contains("a covenant row declares " + declared, row.Fault);
			}
		}

		/// <summary>One edited four-byte value, and the digest recomputed over the result.</summary>
		private static byte[] RedigestInt32(byte[] bytes, int offset, int value)
		{
			byte[] edited = (byte[])bytes.Clone();
			for (int i = 0; i < 4; i++) edited[offset + i] = (byte)(value >> (i * 8));
			return Sealed(edited);
		}

		/// <summary>One edited byte, and the digest recomputed over the result.</summary>
		private static byte[] Redigest(byte[] bytes, int offset, byte value)
		{
			byte[] edited = (byte[])bytes.Clone();
			edited[offset] = value;
			return Sealed(edited);
		}

		private static byte[] Sealed(byte[] edited)
		{
			int body = edited.Length - KingdomVillageCovenantCodec.DigestBytes;
			using (SHA256 sha = SHA256.Create())
				Buffer.BlockCopy(sha.ComputeHash(edited, 0, body), 0, edited, body,
					KingdomVillageCovenantCodec.DigestBytes);
			return edited;
		}

		[Test]
		public void AFutureArchiveClaimingThisBuildsOwnRevisionIsNotAFuture()
		{
			KingdomVillageCovenantArchive lying = new KingdomVillageCovenantArchive
			{
				State = KingdomVillageCovenantState.FutureOpaque,
				OpaqueVersion = KingdomVillageCovenantCodec.CurrentWireVersion,
				OpaquePayload = new byte[] { 1 },
				Fault = "a future that is not later than the present"
			};
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidate(lying, out string failure));
			StringAssert.Contains("not later than this build's", failure);
		}

		[Test]
		public void AnUndefinedArchiveStateIsRefusedRatherThanFallingThroughToAnyBranch()
		{
			KingdomVillageCovenantArchive strange = new KingdomVillageCovenantArchive
			{
				State = (KingdomVillageCovenantState)77
			};
			Assert.IsFalse(KingdomVillageCovenantRules.Defined(strange.State));
			Assert.IsFalse(KingdomVillageCovenantCodec.TryEncode(strange, out _,
				out string failure));
			StringAssert.Contains("which this build does not define", failure);
			Assert.AreEqual(KingdomCivicMemoryNested.Malformed,
				KingdomVillageCovenantInspection.Inspect(new byte[] { 1, 2, 3 }, out _));
		}

		// ---- the envelope's arithmetic ---------------------------------------------------

		/// <summary>
		/// The cap is arithmetic, and the arithmetic is written out here so that moving one of its
		/// terms cannot quietly move the total.
		/// </summary>
		[Test]
		public void TheArchivesMaximumIsExactlyItsOwnArithmetic()
		{
			Assert.AreEqual(48, KingdomVillageCovenantArchive.MaxRows);
			Assert.AreEqual(4 + 8 * 4 + 88 + 32 + 77 + 2048 + 768 + 768 + 205 + 60 + 4 + 8,
				KingdomVillageCovenantCodec.MaxAuthoredRowBytes);
			Assert.AreEqual(4094, KingdomVillageCovenantCodec.MaxAuthoredRowBytes);
			Assert.AreEqual(4096, KingdomVillageCovenantCodec.MaxRowBytes);
			Assert.LessOrEqual(KingdomVillageCovenantCodec.MaxAuthoredRowBytes,
				KingdomVillageCovenantCodec.MaxRowBytes,
				"what this build authors must fit what it reserved room to read");
			Assert.AreEqual(88, KingdomVillageCovenantRules.MaxReceiptIdBytes);
			Assert.AreEqual(32, KingdomVillageCovenantRules.MaxTransactionIdBytes);
			Assert.AreEqual(77, KingdomVillageCovenantRules.MaxRealmIdBytes);
			Assert.AreEqual(2048, KingdomVillageCovenantRules.MaxAuthorityBytes);
			Assert.AreEqual(768, KingdomVillageCovenantRules.MaxFactionIdBytes);
			Assert.AreEqual(768, KingdomVillageCovenantRules.MaxDisplayNameBytes);
			Assert.AreEqual(205, KingdomVillageCovenantRules.MaxZoneIdBytes);
			Assert.AreEqual(60, KingdomVillageCovenantRules.MaxChronicleEventBytes);
			Assert.AreEqual(82, KingdomVillageCovenantCodec.IdentityFramingBytes);
			Assert.AreEqual(20, KingdomVillageCovenantCodec.HeaderBytes);
			Assert.AreEqual(44, KingdomVillageCovenantCodec.EnvelopeOverheadBytes);
			Assert.AreEqual(82 + 20 + 48 * (4 + 4096),
				KingdomVillageCovenantCodec.MaxPayloadBytes);
			Assert.AreEqual(196902, KingdomVillageCovenantCodec.MaxPayloadBytes);
			Assert.AreEqual(196946, KingdomVillageCovenantCodec.MaxEnvelopeBytes);
		}

		/// <summary>
		/// Teaching this build what section nine means may only narrow what a payload there can be.
		/// Before it knew, an unknown id was held to the widest known cap; if the new cap were
		/// wider, a payload that used to be refused would now be accepted on the strength of having
		/// been given a name.
		/// </summary>
		[Test]
		public void TheCovenantSectionSitsUnderTheCapAnUnknownSectionAlreadyHad()
		{
			Assert.AreEqual(9, KingdomCivicMemoryLimits.SectionVillageCovenant);
			Assert.AreEqual(KingdomCivicMemoryLimits.SectionVillageCovenant,
				KingdomCivicMemoryLimits.LastKnownSection);
			Assert.AreEqual(KingdomVillageCovenantCodec.MaxEnvelopeBytes,
				KingdomCivicMemoryLimits.MaxVillageCovenantBytes);
			Assert.AreEqual(KingdomCivicMemoryLimits.MaxVillageCovenantBytes,
				KingdomCivicMemoryLimits.SectionCap(
					KingdomCivicMemoryLimits.SectionVillageCovenant));
			Assert.Less(KingdomCivicMemoryLimits.MaxVillageCovenantBytes,
				KingdomCivicMemoryLimits.MaxTreatyBytes,
				"a newly known section must never be allowed more room than an unknown one had");
		}

		[Test]
		public void TheEnvelopesTotalsMovedByExactlyTheOneNewCap()
		{
			Assert.AreEqual(9, KingdomCivicMemoryLimits.KnownSectionCount);
			Assert.AreEqual(18, KingdomCivicMemoryLimits.MaxSections);
			Assert.AreEqual(642914 + 196946,
				KingdomCivicMemoryLimits.MaxCumulativePayloadBytes);
			Assert.AreEqual(839860, KingdomCivicMemoryLimits.MaxCumulativePayloadBytes);
			Assert.AreEqual(44 + 18 * 8 + 839860, KingdomCivicMemoryLimits.MaxEnvelopeBytes);
			Assert.AreEqual(840048, KingdomCivicMemoryLimits.MaxEnvelopeBytes);
			// Four maximal unknown sections must still exceed the whole budget: that is what stops
			// a stranger's payloads from together making a save larger than every known family could.
			Assert.Greater(4 * KingdomCivicMemoryLimits.MaxTreatyBytes,
				KingdomCivicMemoryLimits.MaxCumulativePayloadBytes);
		}

		/// <summary>
		/// A full archive of the widest covenants this build can write must still fit inside the
		/// cap that was reserved for it, with the row cap doing the bounding rather than luck.
		/// </summary>
		[Test]
		public void AFullArchiveOfWideCovenantsStillFitsItsOwnCap()
		{
			KingdomVillageCovenantArchive archive = KingdomVillageCovenantTests.Bound();
			// Every name at its own byte ceiling: 256 three-byte characters is 768 bytes, which is
			// exactly what the row was sized for.
			string wide = new string('\u4e00', KingdomVillageCovenantRules.MaxNameChars);
			for (int i = 0; i < KingdomVillageCovenantArchive.MaxRows; i++)
			{
				string transaction = i.ToString("x2") + "0123456789abcdef0123456789abcd";
				KingdomVillageCovenantReceipt row = KingdomVillageCovenantTests.Row(transaction,
					wide, wide);
				Assert.IsTrue(KingdomVillageCovenantRules.TryAppend(archive, row,
					KingdomVillageCovenantTests.Realm, out archive, out _, out _,
					out string failure), failure);
			}
			Assert.IsTrue(KingdomVillageCovenantCodec.TryEncode(archive, out byte[] bytes,
				out string encode), encode);
			Assert.LessOrEqual(bytes.Length, KingdomVillageCovenantCodec.MaxEnvelopeBytes);
			Assert.AreEqual(KingdomVillageCovenantArchive.MaxRows,
				KingdomVillageCovenantCodec.Decode(bytes).Rows.Count);
		}
	}
}
#endif
