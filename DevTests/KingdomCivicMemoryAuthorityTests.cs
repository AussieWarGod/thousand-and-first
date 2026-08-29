#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Behavioural coverage for how civic memory changes hands: copy-on-read, revision-checked
	/// commits, and the dispositions that must never be confused with each other.
	/// </summary>
	[TestFixture]
	public class KingdomCivicMemoryAuthorityTests
	{
		private const int Artifacts = KingdomCivicMemoryLimits.SectionCivicArtifacts;
		private const int Practice = KingdomCivicMemoryLimits.SectionCivicPractice;
		private const int Treaty = KingdomCivicMemoryLimits.SectionTreaty;

		private static KingdomCivicMemoryAuthority Fresh()
		{
			return new KingdomCivicMemoryAuthority(KingdomCivicMemoryTestFamilies.Table());
		}

		private static List<KingdomCivicMemorySection> One(int Id, byte[] Payload)
		{
			return new List<KingdomCivicMemorySection>
			{
				new KingdomCivicMemorySection(Id, Payload)
			};
		}

		private static KingdomCivicMemoryAuthority Holding(int Id, byte[] Payload)
		{
			KingdomCivicMemoryAuthority authority = Fresh();
			string failure;
			Assert.IsTrue(authority.TryCommit(One(Id, Payload), 0L, out failure), failure);
			return authority;
		}

		[Test]
		public void AFreshAuthorityIsEmptyAndNotQuarantined()
		{
			KingdomCivicMemoryAuthority authority = Fresh();
			Assert.IsTrue(authority.IsEmpty);
			Assert.IsFalse(authority.Quarantined);
			Assert.IsFalse(authority.IsFutureOuter);
			Assert.IsFalse(authority.ReadOnly);
			Assert.AreEqual(0L, authority.Revision);
		}

		[Test]
		public void MutatingWhatAReadReturnedNeverReachesTheAuthority()
		{
			byte[] sound = KingdomCivicMemoryTestFamilies.Sound(4);
			KingdomCivicMemoryAuthority authority = Holding(Artifacts, sound);

			KingdomCivicMemoryState read = authority.Read();
			List<KingdomCivicMemorySection> sections = read.Sections();
			sections.Clear();
			sections.Add(new KingdomCivicMemorySection(Treaty, new byte[] { 9, 9 }));
			byte[] payload = read.Section(Artifacts).Payload();
			payload[0] = 0xFF;

			KingdomCivicMemoryState again = authority.Read();
			Assert.AreEqual(1, again.Count, "clearing a returned list must not empty the authority");
			CollectionAssert.AreEqual(sound, again.Section(Artifacts).Payload(),
				"editing a returned payload must not edit the authority's copy");
			Assert.IsNull(again.Section(Treaty));
		}

		[Test]
		public void TwoReadsShareNoSectionObjects()
		{
			KingdomCivicMemoryAuthority authority =
				Holding(Practice, KingdomCivicMemoryTestFamilies.Sound(4));
			Assert.AreNotSame(authority.Read().Section(Practice),
				authority.Read().Section(Practice));
		}

		[Test]
		public void ACommitAdvancesTheRevisionAndAStaleOneIsRefused()
		{
			KingdomCivicMemoryAuthority authority = Fresh();
			string failure;
			byte[] first = KingdomCivicMemoryTestFamilies.Sound(4);
			byte[] second = KingdomCivicMemoryTestFamilies.Sound(6);

			Assert.IsTrue(authority.TryCommit(One(Artifacts, first), 0L, out failure), failure);
			Assert.AreEqual(1L, authority.Revision);
			Assert.AreEqual("", failure);

			Assert.IsFalse(authority.TryCommit(One(Artifacts, second), 0L, out failure),
				"a commit built against revision 0 must not land on revision 1");
			StringAssert.Contains("revision", failure);
			Assert.AreEqual(1L, authority.Revision, "a refused commit must not move the revision");
			CollectionAssert.AreEqual(first, authority.Read().Section(Artifacts).Payload(),
				"a refused commit must not change a single byte of the authority");

			Assert.IsTrue(authority.TryCommit(One(Artifacts, second), 1L, out failure), failure);
			Assert.AreEqual(2L, authority.Revision);
			CollectionAssert.AreEqual(second, authority.Read().Section(Artifacts).Payload());
		}

		[Test]
		public void AdoptionAdvancesGenerationAndRefusesAPreAdoptionCommit()
		{
			KingdomCivicMemoryAuthority authority = Fresh();
			long preparedAgainst = authority.Revision;
			byte[] loaded = KingdomCivicMemoryTestFamilies.Sound(12);
			authority.AdoptSaved(KingdomCivicMemoryCodec.Encode(
				KingdomCivicMemoryState.Of(One(Artifacts, loaded), 0L)));

			Assert.AreEqual(preparedAgainst + 1L, authority.Revision);
			Assert.IsFalse(authority.TryCommit(
				One(Artifacts, KingdomCivicMemoryTestFamilies.Sound(4)), preparedAgainst,
				out string failure));
			StringAssert.Contains("revision", failure);
			CollectionAssert.AreEqual(loaded, authority.Read().Section(Artifacts).Payload(),
				"a commit prepared before load adoption must never overwrite loaded records");
		}

		[Test]
		public void ACommitThatCouldNotBeSavedIsRefusedAndChangesNothing()
		{
			byte[] sound = KingdomCivicMemoryTestFamilies.Sound(4);
			KingdomCivicMemoryAuthority authority = Holding(Artifacts, sound);
			string failure;
			List<KingdomCivicMemorySection> oversize = One(KingdomCivicMemoryLimits.SectionBodyHistory,
				KingdomCivicMemoryTestFamilies.Sound(
					KingdomCivicMemoryLimits.MaxBodyHistoryBytes + 1));

			Assert.IsFalse(authority.TryCommit(oversize, 1L, out failure));
			Assert.IsNotEmpty(failure);
			Assert.AreEqual(1L, authority.Revision);
			Assert.AreEqual(1, authority.Read().Count);
			CollectionAssert.AreEqual(sound, authority.Read().Section(Artifacts).Payload());
		}

		[Test]
		public void AnUnreadablePayloadIsKeptWholeAndTellsTheLatch()
		{
			KingdomCivicMemoryAuthority authority = Fresh();
			byte[] rubbish = { 9, 9, 9, 9, 1, 1, 1, 1 };
			authority.AdoptSaved(rubbish);

			Assert.IsTrue(authority.Quarantined);
			Assert.IsTrue(authority.Latch.Tripped);
			Assert.IsTrue(authority.ReadOnly);
			Assert.IsNotEmpty(authority.Latch.Reason);
			CollectionAssert.AreEqual(rubbish, authority.Read().RetainedPayload(),
				"the refused bytes are the only copy left and must be kept exactly");
		}

		[Test]
		public void QuarantinedIsNeverReportedAsEmpty()
		{
			KingdomCivicMemoryAuthority quarantined = Fresh();
			quarantined.AdoptSaved(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
			KingdomCivicMemoryAuthority absent = Fresh();
			absent.AdoptAbsent();

			Assert.IsTrue(absent.IsEmpty);
			Assert.IsFalse(absent.Quarantined);
			Assert.IsFalse(quarantined.IsEmpty,
				"a save whose records were refused must never look like one that never had any");
			Assert.IsTrue(quarantined.Quarantined);
			Assert.IsNotEmpty(quarantined.Read().Fault);
			Assert.AreEqual("", absent.Read().Fault);
		}

		[Test]
		public void QuarantinedRecordsRefuseToBeOverwritten()
		{
			KingdomCivicMemoryAuthority authority = Fresh();
			authority.AdoptSaved(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });
			byte[] kept = authority.Read().RetainedPayload();

			string failure;
			Assert.IsFalse(authority.TryCommit(
				One(Artifacts, KingdomCivicMemoryTestFamilies.Sound(4)), authority.Revision,
				out failure), "a well-formed commit must not replace unreadable evidence");
			StringAssert.Contains("overwriting them would destroy the only copy", failure,
				"the founder must be told their records are being defended, not merely that the "
				+ "session is read-only");
			Assert.IsTrue(authority.Quarantined);
			CollectionAssert.AreEqual(kept, authority.Read().RetainedPayload());
		}

		[Test]
		public void TheLatchIsOneWayAndSurvivesEveryLaterSuccess()
		{
			KingdomCivicMemoryAuthority authority = Fresh();
			byte[] refused = { 4, 4, 4, 4 };
			authority.AdoptSaved(refused);
			Assert.IsTrue(authority.Latch.Tripped);

			string first = authority.Latch.Reason;
			authority.Latch.Trip("something vaguer that happened afterwards");
			Assert.AreEqual(first, authority.Latch.Reason,
				"the first cause is the one worth keeping");

			// A later lawful read does not retire the latch: this session stays read-only.
			authority.AdoptSaved(KingdomCivicMemoryCodec.Encode(KingdomCivicMemoryState.Of(
				One(Artifacts, KingdomCivicMemoryTestFamilies.Sound(4)), 0L)));
			Assert.IsTrue(authority.Latch.Tripped,
				"reading a good payload later must not dismiss an earlier failure");
			Assert.IsTrue(authority.ReadOnly);
			Assert.IsTrue(authority.Quarantined,
				"a later adoption must not replace the state defended by the latch");
			CollectionAssert.AreEqual(refused, authority.Read().RetainedPayload());

			string failure;
			Assert.IsFalse(authority.TryCommit(
				One(Artifacts, KingdomCivicMemoryTestFamilies.Sound(6)), authority.Revision,
				out failure));
			StringAssert.Contains("overwriting them would destroy the only copy", failure,
				"quarantine should report the defended evidence before the broader latch state");
		}

		[Test]
		public void ASecondAdoptionCannotReplaceFutureOrQuarantinedEvidence()
		{
			KingdomCivicMemoryAuthority quarantined = Fresh();
			byte[] refused = { 8, 7, 6, 5 };
			quarantined.AdoptSaved(refused);
			quarantined.AdoptAbsent();
			Assert.IsTrue(quarantined.Quarantined);
			CollectionAssert.AreEqual(refused, quarantined.Read().RetainedPayload());

			byte[] future = KingdomCivicMemoryCodec.Encode(KingdomCivicMemoryState.Of(
				One(Artifacts, KingdomCivicMemoryTestFamilies.Sound(4)), 0L));
			future[4] = (byte)(KingdomCivicMemoryCodec.CurrentWireVersion + 1);
			using (System.Security.Cryptography.SHA256 sha =
				System.Security.Cryptography.SHA256.Create())
			{
				byte[] body = new byte[future.Length - 32];
				System.Buffer.BlockCopy(future, 0, body, 0, body.Length);
				System.Buffer.BlockCopy(sha.ComputeHash(body), 0, future, body.Length, 32);
			}
			KingdomCivicMemoryAuthority newer = Fresh();
			newer.AdoptSaved(future);
			newer.AdoptAbsent();
			Assert.IsTrue(newer.IsFutureOuter);
			Assert.IsTrue(newer.Latch.Tripped,
				"a contradictory second adoption is a session fault, not permission to reset");
			CollectionAssert.AreEqual(future, newer.Read().RetainedPayload());
		}

		[Test]
		public void AnUnexpectedFamilyInspectionFailureStillLatchesAndKeepsTheEnvelope()
		{
			byte[] envelope = KingdomCivicMemoryCodec.Encode(KingdomCivicMemoryState.Of(
				One(Artifacts, KingdomCivicMemoryTestFamilies.Sound(4)), 0L));
			KingdomCivicMemoryAuthority authority = new KingdomCivicMemoryAuthority(
				KingdomCivicMemoryTestFamilies.TableThrowing(Artifacts));
			Assert.DoesNotThrow(() => authority.AdoptSaved(envelope));
			Assert.IsTrue(authority.Quarantined);
			Assert.IsTrue(authority.Latch.Tripped);
			StringAssert.Contains("inspection exploded", authority.Latch.Reason);
			CollectionAssert.AreEqual(envelope, authority.Read().RetainedPayload());
		}

		/// <summary>
		/// Why the quarantine refusal in the commit path is belt-and-braces rather than the only
		/// thing standing between a bad load and an overwrite: every path that quarantines also
		/// latches. If that ever stops being true, this test is the one that says so.
		/// </summary>
		[Test]
		public void EveryPathThatQuarantinesAlsoLatches()
		{
			foreach (byte[] bad in new[]
			{
				new byte[] { 1, 2, 3 },
				new byte[0],
				KingdomCivicMemoryTestFamilies.Payload(KingdomCivicMemoryTestFamilies.Malformed, 8)
			})
			{
				KingdomCivicMemoryAuthority authority = Fresh();
				authority.AdoptSaved(bad);
				Assert.IsTrue(authority.Quarantined);
				Assert.IsTrue(authority.Latch.Tripped,
					"a quarantined authority must always be a latched one");
			}
			KingdomCivicMemoryAuthority framing = Fresh();
			framing.AdoptUnreadableFraming(new byte[] { 7 }, "bad framing");
			Assert.IsTrue(framing.Quarantined);
			Assert.IsTrue(framing.Latch.Tripped);
		}

		[Test]
		public void AFutureSectionIsCarriedThroughAuthorityByteForByte()
		{
			int futureId = KingdomCivicMemoryLimits.LastKnownSection + 1;
			byte[] stranger = { 11, 22, 33, 44, 55 };
			KingdomCivicMemoryAuthority authority = Fresh();
			authority.AdoptSaved(KingdomCivicMemoryCodec.Encode(
				KingdomCivicMemoryState.Of(One(futureId, stranger), 0L)));

			KingdomCivicMemoryState held = authority.Read();
			Assert.IsFalse(authority.Quarantined, "an unknown id is not a malformed payload");
			Assert.IsTrue(held.HasFutureSections);
			Assert.IsFalse(held.Section(futureId).KnownToThisBuild);
			CollectionAssert.AreEqual(stranger, held.Section(futureId).Payload());

			CollectionAssert.AreEqual(stranger,
				KingdomCivicMemoryCodec.Decode(authority.Encode(), 0L)
					.Section(futureId).Payload());
		}
	}
}
#endif
