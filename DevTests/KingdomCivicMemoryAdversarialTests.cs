#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// One test per attack, each named for what it tries to do to the save rather than for the
	/// method it happens to call. Every one of these is a way somebody's records could be lost or
	/// falsified, and the assertion in each is the thing that stops it.
	/// </summary>
	[TestFixture]
	public class KingdomCivicMemoryAdversarialTests
	{
		private const int Artifacts = KingdomCivicMemoryLimits.SectionCivicArtifacts;
		private const int Practice = KingdomCivicMemoryLimits.SectionCivicPractice;
		private const int Treaty = KingdomCivicMemoryLimits.SectionTreaty;
		private static readonly int Stranger = KingdomCivicMemoryLimits.LastKnownSection + 1;

		private static KingdomCivicMemoryAuthority Fresh()
		{
			return new KingdomCivicMemoryAuthority(KingdomCivicMemoryTestFamilies.Table());
		}

		private static List<KingdomCivicMemorySection> Rows(params KingdomCivicMemorySection[] S)
		{
			return new List<KingdomCivicMemorySection>(S);
		}

		private static KingdomCivicMemorySection Row(int Id, byte Verdict, int Length)
		{
			return new KingdomCivicMemorySection(Id,
				KingdomCivicMemoryTestFamilies.Payload(Verdict, Length));
		}

		private static byte[] Envelope(params KingdomCivicMemorySection[] S)
		{
			return KingdomCivicMemoryCodec.Encode(KingdomCivicMemoryState.Of(Rows(S), 0L));
		}

		// ---- Item 1: every known nested codec is dispatched on adopt and on commit ----

		/// <summary>
		/// Item 1 + item 2. The envelope's own framing and hash are perfect; the payload inside a
		/// known section is not. Storing bytes without asking their owner would accept this.
		/// </summary>
		[Test]
		public void AttackSmugglesAMalformedKnownPayloadPastAValidOuterHash()
		{
			byte[] envelope = Envelope(Row(Artifacts, KingdomCivicMemoryTestFamilies.Malformed, 16));
			// The outer envelope really is well formed: framing-only decoding accepts it.
			Assert.DoesNotThrow(() => KingdomCivicMemoryCodec.Decode(envelope, 0L));

			KingdomCivicMemoryAuthority authority = Fresh();
			authority.AdoptSaved(envelope);

			Assert.IsTrue(authority.Quarantined, "the family refused it, so the authority must too");
			Assert.IsTrue(authority.Latch.Tripped,
				"a good hash cannot make a family-invalid payload usable: it must latch, "
					+ "not merely quarantine");
			Assert.IsTrue(authority.ReadOnly);
			CollectionAssert.AreEqual(envelope, authority.Read().RetainedPayload(),
				"the evidence must be kept whole");
		}

		/// <summary>Item 1, commit side. A malformed payload must not enter by the front door.</summary>
		[Test]
		public void AttackCommitsAPayloadItsOwnFamilyWouldRefuse()
		{
			KingdomCivicMemoryAuthority authority = Fresh();
			string failure;
			Assert.IsFalse(authority.TryCommit(
				Rows(Row(Practice, KingdomCivicMemoryTestFamilies.Malformed, 12)), 0L, out failure));
			StringAssert.Contains("refused", failure);
			Assert.AreEqual(0L, authority.Revision);
			Assert.IsTrue(authority.IsEmpty, "a refused commit must leave the authority untouched");
		}

		[Test]
		public void AttackUsesAnEmptySectionAsAStoreDefaultInsteadOfARealEnvelope()
		{
			KingdomCivicMemoryAuthority authority = Fresh();
			Assert.IsFalse(authority.TryCommit(Rows(
				new KingdomCivicMemorySection(Artifacts, new byte[0])), 0L,
				out string failure));
			StringAssert.Contains("no encoded payload", failure);

			byte[] envelope = Envelope(new KingdomCivicMemorySection(Artifacts, new byte[0]));
			authority = Fresh();
			authority.AdoptSaved(envelope);
			Assert.IsTrue(authority.Quarantined);
			Assert.IsTrue(authority.Latch.Tripped);
		}

		/// <summary>
		/// Item 1, fail-closed. A table assembled with a gap must refuse that section rather than
		/// treat "nobody objected" as "everybody approved".
		/// </summary>
		[Test]
		public void AttackUsesAnUnwiredFamilySlotToWaveAPayloadThrough()
		{
			KingdomCivicMemoryAuthority authority = new KingdomCivicMemoryAuthority(
				KingdomCivicMemoryTestFamilies.TableMissing(Artifacts));
			Assert.IsTrue(authority.ReadOnly);
			StringAssert.Contains("missing", authority.ReadOnlyReason);
			Assert.IsFalse(authority.TryCommit(
				Rows(Row(Practice, KingdomCivicMemoryTestFamilies.Current, 8)), 0L,
				out string commitFailure));
			StringAssert.Contains("missing a reader", commitFailure);
			authority.AdoptSaved(Envelope(Row(Artifacts, KingdomCivicMemoryTestFamilies.Current, 8)));

			Assert.IsTrue(authority.Quarantined,
				"an unwired section must fail closed, never open");
			Assert.IsTrue(authority.Latch.Tripped);
			StringAssert.Contains("no family reader is installed", authority.Latch.Reason);
		}

		// ---- Item 3: a future OUTER version is lawful, not damage ----

		/// <summary>
		/// Item 3. An envelope from a later build must survive untouched, stay read-only, and
		/// never be filed as corruption or as an empty save.
		/// </summary>
		[Test]
		public void AttackPassesAFutureOuterVersionOffAsCorruption()
		{
			byte[] future = FutureEnvelope(KingdomCivicMemoryCodec.CurrentWireVersion + 1);
			KingdomCivicMemoryAuthority authority = Fresh();
			authority.AdoptSaved(future);

			Assert.IsFalse(authority.Latch.Tripped,
				"a newer save is not a fault and must not latch the session");
			Assert.IsFalse(authority.Quarantined, "and must not be called malformed");
			Assert.IsFalse(authority.IsEmpty, "nor mistaken for a save that never had records");
			Assert.IsTrue(authority.IsFutureOuter);
			Assert.IsTrue(authority.ReadOnly, "but it is still not ours to change");
			StringAssert.Contains("newer build", authority.ReadOnlyReason);

			CollectionAssert.AreEqual(future, authority.Encode(),
				"a future envelope must go back to disk byte for byte");
			Assert.AreEqual(KingdomCivicMemoryCodec.CurrentWireVersion + 1,
				authority.Read().OuterVersion);

			string failure;
			Assert.IsFalse(authority.TryCommit(
				Rows(Row(Artifacts, KingdomCivicMemoryTestFamilies.Current, 8)), authority.Revision,
				out failure));
			StringAssert.Contains("newer build", failure);
			CollectionAssert.AreEqual(future, authority.Encode(),
				"and the refused commit must not have disturbed it");
		}

		/// <summary>Item 3. A future envelope with a broken hash is still just broken.</summary>
		[Test]
		public void AttackForgesAFutureOuterVersionWithoutAValidHash()
		{
			byte[] future = FutureEnvelope(KingdomCivicMemoryCodec.CurrentWireVersion + 1);
			future[future.Length - 1] ^= 0xFF;
			KingdomCivicMemoryAuthority authority = Fresh();
			authority.AdoptSaved(future);

			Assert.IsTrue(authority.Quarantined,
				"the version claim is only integrity-valid when the digest matches");
			Assert.IsFalse(authority.IsFutureOuter);
			Assert.IsTrue(authority.Latch.Tripped);
		}

		/// <summary>
		/// Builds an envelope whose outer version is <paramref name="Version"/>, integrity-checked
		/// the way the format's permanent 44-byte frame promises it always will be.
		/// </summary>
		private static byte[] FutureEnvelope(int Version)
		{
			byte[] current = Envelope(Row(Artifacts, KingdomCivicMemoryTestFamilies.Current, 8));
			byte[] framed = new byte[current.Length - 32];
			Buffer.BlockCopy(current, 0, framed, 0, framed.Length);
			framed[4] = (byte)Version;
			framed[5] = (byte)(Version >> 8);
			framed[6] = (byte)(Version >> 16);
			framed[7] = (byte)(Version >> 24);
			byte[] rebuilt = new byte[current.Length];
			Buffer.BlockCopy(framed, 0, rebuilt, 0, framed.Length);
			using (System.Security.Cryptography.SHA256 sha =
				System.Security.Cryptography.SHA256.Create())
				Buffer.BlockCopy(sha.ComputeHash(framed), 0, rebuilt, framed.Length, 32);
			return rebuilt;
		}

		// ---- Item 4: a commit preserves everything it did not name ----

		/// <summary>
		/// Item 4. A commit that names one section must not quietly drop the sections it did not
		/// name &mdash; least of all one whose id this build cannot even interpret.
		/// </summary>
		[Test]
		public void AttackUsesACommitToSilentlyDropAnUnknownSection()
		{
			byte[] stranger = { 3, 1, 4, 1, 5, 9, 2, 6 };
			KingdomCivicMemoryAuthority authority = Fresh();
			authority.AdoptSaved(Envelope(
				new KingdomCivicMemorySection(Stranger, stranger),
				Row(Artifacts, KingdomCivicMemoryTestFamilies.Current, 8)));
			Assert.IsFalse(authority.Quarantined);

			string failure;
			Assert.IsTrue(authority.TryCommit(
				Rows(Row(Artifacts, KingdomCivicMemoryTestFamilies.Current, 24)),
				authority.Revision, out failure), failure);

			KingdomCivicMemoryState after = authority.Read();
			Assert.AreEqual(2, after.Count, "the unnamed stranger must still be there");
			CollectionAssert.AreEqual(stranger, after.Section(Stranger).Payload(),
				"and must be byte-for-byte what it was");
			Assert.AreEqual(24, after.Section(Artifacts).Length, "while the named one did change");
		}

		/// <summary>
		/// Item 4. A known section already holding a payload its own family reports as newer than
		/// this build is that family's forward-compatibility promise. Nothing here may break it.
		/// </summary>
		[Test]
		public void AttackUsesACommitToOverwriteAKnownSectionsNestedFutureContent()
		{
			byte[] nestedFuture = KingdomCivicMemoryTestFamilies.Payload(
				KingdomCivicMemoryTestFamilies.Future, 20);
			KingdomCivicMemoryAuthority authority = Fresh();
			authority.AdoptSaved(Envelope(new KingdomCivicMemorySection(Treaty, nestedFuture)));
			Assert.IsFalse(authority.Quarantined, "nested-future content is lawful, not damage");

			string failure;
			Assert.IsFalse(authority.TryCommit(
				Rows(Row(Treaty, KingdomCivicMemoryTestFamilies.Current, 8)), authority.Revision,
				out failure), "replacing it would break the family's promise on its behalf");
			StringAssert.Contains("newer than this build", failure);
			CollectionAssert.AreEqual(nestedFuture, authority.Read().Section(Treaty).Payload());

			// And it survives a commit that touches a different section entirely.
			Assert.IsTrue(authority.TryCommit(
				Rows(Row(Practice, KingdomCivicMemoryTestFamilies.Current, 8)),
				authority.Revision, out failure), failure);
			CollectionAssert.AreEqual(nestedFuture, authority.Read().Section(Treaty).Payload(),
				"an unrelated commit must carry nested-future content through exactly");
		}

		// ---- Item 5: the reject list ----

		/// <summary>Item 5. An unknown id may be carried, but never authored.</summary>
		[Test]
		public void AttackAuthorsASectionIdThisBuildCannotInterpret()
		{
			KingdomCivicMemoryAuthority authority = Fresh();
			string failure;
			Assert.IsFalse(authority.TryCommit(
				Rows(new KingdomCivicMemorySection(Stranger, new byte[] { 1, 2 })), 0L,
				out failure));
			StringAssert.Contains("carried, never authored", failure);
			Assert.IsTrue(authority.IsEmpty);
		}

		/// <summary>Item 5. Non-positive ids are corruption, not a future allocation.</summary>
		[Test]
		public void AttackNumbersASectionZeroOrNegative()
		{
			foreach (int id in new[] { 0, -1, int.MinValue })
			{
				int subject = id;
				KingdomCivicMemoryAuthority authority = Fresh();
				string failure;
				Assert.IsFalse(authority.TryCommit(
					Rows(new KingdomCivicMemorySection(subject, new byte[] { 1 })), 0L,
					out failure), "section id " + subject + " must be refused");
				Assert.IsNotEmpty(failure);
			}
		}

		/// <summary>Item 5. A null row must be named, not dereferenced.</summary>
		[Test]
		public void AttackOffersANullSectionRow()
		{
			KingdomCivicMemoryAuthority authority = Fresh();
			string failure;
			Assert.IsFalse(authority.TryCommit(
				new List<KingdomCivicMemorySection>
				{
					Row(Artifacts, KingdomCivicMemoryTestFamilies.Current, 4), null
				}, 0L, out failure));
			StringAssert.Contains("absent section at position 1", failure);
			Assert.IsTrue(authority.IsEmpty);

			Assert.Throws<ArgumentException>(() => KingdomCivicMemoryState.Of(
				new List<KingdomCivicMemorySection> { null }, 0L),
				"the state must refuse a null row rather than carry it to the encoder");
		}

		/// <summary>Item 5. Two rows for one id in one commit: which was meant cannot be guessed.</summary>
		[Test]
		public void AttackOffersTheSameSectionTwiceInOneCommit()
		{
			KingdomCivicMemoryAuthority authority = Fresh();
			string failure;
			Assert.IsFalse(authority.TryCommit(
				Rows(Row(Artifacts, KingdomCivicMemoryTestFamilies.Current, 4),
					Row(Artifacts, KingdomCivicMemoryTestFamilies.Current, 8)), 0L, out failure));
			StringAssert.Contains("twice in one", failure);
			Assert.IsTrue(authority.IsEmpty);
		}

		/// <summary>
		/// Item 5. The revision counter is the whole basis of the staleness check, so it must
		/// refuse rather than wrap. A negative revision is a wrapped one and is not constructible.
		/// </summary>
		[Test]
		public void AttackDrivesTheRevisionCounterPastItsMaximum()
		{
			Assert.DoesNotThrow(() => KingdomCivicMemoryState.Of(
				new List<KingdomCivicMemorySection>(), long.MaxValue),
				"the last expressible revision is still a lawful one");
			foreach (long wrapped in new[] { -1L, long.MinValue })
			{
				long subject = wrapped;
				Assert.Throws<ArgumentOutOfRangeException>(
					() => KingdomCivicMemoryState.Of(new List<KingdomCivicMemorySection>(), subject),
					"revision " + subject + " has wrapped and must be refused, never clamped");
			}
		}

		/// <summary>
		/// Item 5, future-block ambiguity. A payload that reads as newer than this build cannot
		/// have been authored by a caller in this build, so accepting it would mean guessing which
		/// of the two it really was.
		/// </summary>
		[Test]
		public void AttackDisguisesAFuturePayloadAsSomethingThisBuildAuthored()
		{
			KingdomCivicMemoryAuthority authority = Fresh();
			string failure;
			Assert.IsFalse(authority.TryCommit(
				Rows(Row(Treaty, KingdomCivicMemoryTestFamilies.Future, 12)), 0L, out failure));
			StringAssert.Contains("no caller here can have", failure);
			Assert.IsTrue(authority.IsEmpty);
		}

		[Test]
		public void AttackReturnsAnUndefinedFamilyVerdict()
		{
			KingdomCivicMemoryFamilyReader invalid = delegate(byte[] payload, out string fault)
			{
				fault = "";
				return (KingdomCivicMemoryNested)99;
			};
			KingdomCivicMemoryFamilyTable table = new KingdomCivicMemoryFamilyTable();
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++) table.Add(id, invalid);

			KingdomCivicMemoryAuthority adopted = new KingdomCivicMemoryAuthority(table);
			adopted.AdoptSaved(Envelope(Row(Artifacts,
				KingdomCivicMemoryTestFamilies.Current, 8)));
			Assert.IsTrue(adopted.Quarantined);
			Assert.IsTrue(adopted.Latch.Tripped);
			StringAssert.Contains("unsupported verdict 99", adopted.Read().Fault);

			KingdomCivicMemoryAuthority committed = new KingdomCivicMemoryAuthority(table);
			Assert.IsFalse(committed.TryCommit(
				Rows(Row(Artifacts, KingdomCivicMemoryTestFamilies.Current, 8)), 0L,
				out string failure));
			StringAssert.Contains("unsupported verdict 99", failure);
			Assert.IsTrue(committed.IsEmpty);
		}

		[Test]
		public void StateFactoryRejectsNullInsteadOfInventingEmptyState()
		{
			Assert.Throws<ArgumentNullException>(() => KingdomCivicMemoryState.Of(null, 0L));
		}

		// ---- Reviewer finding: the framing path must keep the truth and the bytes ----

		/// <summary>
		/// The latch keeps its first cause, so a framing failure that quarantined through the
		/// ordinary decode route would latch with a complaint about a stand-in payload and lose
		/// both the real reason and the real bytes.
		/// </summary>
		[Test]
		public void AttackHidesTheRealFramingFaultBehindASyntheticOne()
		{
			byte[] recovered = { 0x54, 0x46, 0x43, 0x4E };
			KingdomCivicMemoryAuthority authority = Fresh();
			authority.AdoptUnreadableFraming(recovered,
				"the civic memory block's own framing could not be read (marker reads 0x4E434654)");

			Assert.IsTrue(authority.Latch.Tripped);
			StringAssert.Contains("marker reads 0x4E434654", authority.Latch.Reason,
				"the latch must carry the true framing cause, not a decode of a stand-in");
			Assert.IsFalse(authority.Latch.Reason.Contains("shorter than its frame"),
				"the synthetic complaint from decoding an empty payload must never appear");
			CollectionAssert.AreEqual(recovered, authority.Read().RetainedPayload(),
				"the bytes actually recovered from the save must be what is quarantined");
			Assert.IsTrue(authority.Quarantined);
			Assert.IsFalse(authority.IsEmpty);
		}
	}
}
#endif
