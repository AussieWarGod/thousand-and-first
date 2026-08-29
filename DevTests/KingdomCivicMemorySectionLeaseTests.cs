#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomCivicMemorySectionLeaseTests
	{
		private const int Section = KingdomCivicMemoryLimits.SectionCivicArtifacts;
		private const int Sibling = KingdomCivicMemoryLimits.SectionCivicPractice;

		[Test]
		public void MissingSectionIsExplicitAndCanBeCreatedOnce()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			Assert.That(authority.TryReadSection(Section,
				out KingdomCivicMemorySectionLease lease, out string failure), Is.True, failure);
			Assert.That(lease.Present, Is.False);
			Assert.That(lease.ExpectedRevision, Is.Zero);
			Assert.That(lease.Payload(), Is.Empty);

			Assert.That(authority.TryCommitSection(lease,
				KingdomCivicMemoryTestFamilies.Sound(5), out failure), Is.True, failure);
			Assert.That(authority.Revision, Is.EqualTo(1L));
			Assert.That(authority.TryCommitSection(lease,
				KingdomCivicMemoryTestFamilies.Sound(6), out failure), Is.False);
			StringAssert.Contains("moved to revision", failure);
		}

		[Test]
		public void LeasePayloadIsCopiedInBothDirections()
		{
			byte[] source = KingdomCivicMemoryTestFamilies.Sound(7);
			KingdomCivicMemoryAuthority authority = Loaded(source,
				KingdomCivicMemoryTestFamilies.Table());
			source[0] = KingdomCivicMemoryTestFamilies.Malformed;
			Assert.That(authority.TryReadSection(Section,
				out KingdomCivicMemorySectionLease lease, out string failure), Is.True, failure);
			byte[] first = lease.Payload();
			first[0] = KingdomCivicMemoryTestFamilies.Malformed;
			Assert.That(lease.Payload()[0], Is.EqualTo(KingdomCivicMemoryTestFamilies.Current));
			byte[] replacement = KingdomCivicMemoryTestFamilies.Sound(8);
			byte[] expected = (byte[])replacement.Clone();
			Assert.That(authority.TryCommitSection(lease, replacement, out failure), Is.True, failure);
			replacement[0] = KingdomCivicMemoryTestFamilies.Malformed;
			CollectionAssert.AreEqual(expected, authority.Read().Section(Section).Payload());
		}

		[Test]
		public void LeaseCannotCrossAuthoritiesEvenAtTheSameRevision()
		{
			KingdomCivicMemoryAuthority first = Authority();
			KingdomCivicMemoryAuthority second = Authority();
			Assert.That(first.TryReadSection(Section,
				out KingdomCivicMemorySectionLease lease, out string failure), Is.True, failure);
			Assert.That(second.TryCommitSection(lease,
				KingdomCivicMemoryTestFamilies.Sound(4), out failure), Is.False);
			StringAssert.Contains("another authority", failure);
			Assert.That(second.IsEmpty, Is.True);
		}

		[Test]
		public void NestedFutureIsCarriedButNeverLeasedToAMutator()
		{
			KingdomCivicMemoryAuthority authority = Loaded(
				KingdomCivicMemoryTestFamilies.Payload(
					KingdomCivicMemoryTestFamilies.Future, 4),
				KingdomCivicMemoryTestFamilies.Table());
			Assert.That(authority.ReadOnly, Is.False);
			Assert.That(authority.TryReadSection(Section,
				out KingdomCivicMemorySectionLease lease, out string failure), Is.False);
			Assert.That(lease, Is.Null);
			StringAssert.Contains("newer family version", failure);
			Assert.That(authority.ReadOnly, Is.False);
			Assert.That(authority.TryReadSection(Sibling,
				out KingdomCivicMemorySectionLease sibling, out failure), Is.True, failure);
			Assert.That(sibling.Present, Is.False,
				"the future refusal must release the scoped guard for the next independent read");
		}

		[Test]
		public void ReaderChangingItsMindLatchesInsteadOfLeasingRefusedBytes()
		{
			bool accept = true;
			KingdomCivicMemoryFamilyReader changing = delegate(byte[] payload, out string fault)
			{
				fault = accept ? "" : "reader now refuses";
				return accept ? KingdomCivicMemoryNested.Current
					: KingdomCivicMemoryNested.Malformed;
			};
			KingdomCivicMemoryFamilyTable table = Table(changing);
			KingdomCivicMemoryAuthority authority = Loaded(
				KingdomCivicMemoryTestFamilies.Sound(4), table);
			accept = false;
			Assert.That(authority.TryReadSection(Section,
				out KingdomCivicMemorySectionLease lease, out string failure), Is.False);
			Assert.That(lease, Is.Null);
			StringAssert.Contains("reader now refuses", failure);
			Assert.That(authority.ReadOnly, Is.True);
		}

		[Test]
		public void ReaderCannotMutateItsInspectionCopyIntoAcceptedEvidence()
		{
			bool mutate = false;
			KingdomCivicMemoryFamilyReader changing = delegate(byte[] payload, out string fault)
			{
				fault = "";
				if (mutate) payload[payload.Length - 1] ^= 0x7F;
				return KingdomCivicMemoryNested.Current;
			};
			KingdomCivicMemoryAuthority authority = Loaded(
				KingdomCivicMemoryTestFamilies.Sound(5), Table(changing));
			mutate = true;
			Assert.That(authority.TryReadSection(Section,
				out KingdomCivicMemorySectionLease lease, out string failure), Is.False);
			Assert.That(lease, Is.Null);
			StringAssert.Contains("changed the inspection copy", failure);
			Assert.That(authority.ReadOnly, Is.True);
		}

		[Test]
		public void ReaderCannotReenterTheSameSectionRead()
		{
			KingdomCivicMemoryAuthority authority = null;
			bool reenter = false;
			bool nestedAccepted = true;
			string nestedFailure = null;
			KingdomCivicMemoryFamilyReader reader = delegate(byte[] payload, out string fault)
			{
				fault = "";
				if (reenter)
				{
					// Turn the callback off first so a broken guard fails the assertion, not the process.
					reenter = false;
					nestedAccepted = authority.TryReadSection(Section,
						out KingdomCivicMemorySectionLease nested, out nestedFailure);
				}
				return KingdomCivicMemoryNested.Current;
			};
			authority = new KingdomCivicMemoryAuthority(Table(reader));
			authority.AdoptSaved(Saved(KingdomCivicMemoryTestFamilies.Sound(5)));

			reenter = true;
			Assert.That(authority.TryReadSection(Section,
				out KingdomCivicMemorySectionLease lease, out string failure), Is.False);
			Assert.That(nestedAccepted, Is.False);
			StringAssert.Contains("re-entrant", nestedFailure);
			StringAssert.Contains("re-entrant", failure);
			Assert.That(lease, Is.Null);
			Assert.That(authority.Latch.Tripped, Is.True);
			Assert.That(authority.Revision, Is.EqualTo(1L));
		}

		[Test]
		public void ReaderCannotCommitASiblingWhileASectionReadIsOpen()
		{
			KingdomCivicMemoryAuthority authority = null;
			KingdomCivicMemorySectionLease siblingLease = null;
			bool commit = false;
			bool nestedAccepted = true;
			string nestedFailure = null;
			KingdomCivicMemoryFamilyReader reader = delegate(byte[] payload, out string fault)
			{
				fault = "";
				if (commit)
				{
					commit = false;
					nestedAccepted = authority.TryCommitSection(siblingLease,
						KingdomCivicMemoryTestFamilies.Sound(6), out nestedFailure);
				}
				return KingdomCivicMemoryNested.Current;
			};
			authority = new KingdomCivicMemoryAuthority(Table(reader));
			authority.AdoptSaved(Saved(KingdomCivicMemoryTestFamilies.Sound(5)));
			Assert.That(authority.TryReadSection(Sibling, out siblingLease,
				out string openingFailure), Is.True, openingFailure);

			commit = true;
			Assert.That(authority.TryReadSection(Section,
				out KingdomCivicMemorySectionLease lease, out string failure), Is.False);
			Assert.That(nestedAccepted, Is.False);
			StringAssert.Contains("re-entrant", nestedFailure);
			StringAssert.Contains("re-entrant", failure);
			Assert.That(lease, Is.Null);
			Assert.That(authority.Revision, Is.EqualTo(1L));
			Assert.That(authority.Read().Section(Sibling), Is.Null);
		}

		[Test]
		public void CommitReaderCannotOpenASectionLease()
		{
			KingdomCivicMemoryAuthority authority = null;
			bool inspect = true;
			bool nestedAccepted = true;
			string nestedFailure = null;
			KingdomCivicMemoryFamilyReader reader = delegate(byte[] payload, out string fault)
			{
				fault = "";
				if (inspect)
				{
					inspect = false;
					nestedAccepted = authority.TryReadSection(Sibling,
						out KingdomCivicMemorySectionLease nested, out nestedFailure);
				}
				return KingdomCivicMemoryNested.Current;
			};
			authority = new KingdomCivicMemoryAuthority(Table(reader));

			Assert.That(authority.TryCommit(new List<KingdomCivicMemorySection>
			{
				new KingdomCivicMemorySection(Section,
					KingdomCivicMemoryTestFamilies.Sound(4))
			}, 0L, out string failure), Is.False);
			Assert.That(nestedAccepted, Is.False);
			StringAssert.Contains("re-entrant", nestedFailure);
			StringAssert.Contains("re-entrant", failure);
			Assert.That(authority.Latch.Tripped, Is.True);
			Assert.That(authority.IsEmpty, Is.True);
		}

		[Test]
		public void UnrelatedSectionCommitMakesEveryPeerLeaseStale()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			Assert.That(authority.TryReadSection(Section,
				out KingdomCivicMemorySectionLease first, out string failure), Is.True, failure);
			Assert.That(authority.TryReadSection(Sibling,
				out KingdomCivicMemorySectionLease second, out failure), Is.True, failure);

			Assert.That(authority.TryCommitSection(second,
				KingdomCivicMemoryTestFamilies.Sound(5), out failure), Is.True, failure);
			Assert.That(authority.TryCommitSection(first,
				KingdomCivicMemoryTestFamilies.Sound(6), out failure), Is.False);
			StringAssert.Contains("moved to revision", failure);
			Assert.That(authority.Read().Section(Section), Is.Null);
			Assert.That(authority.Read().Section(Sibling), Is.Not.Null);
		}

		[Test]
		public void InvalidReaderVerdictFailsClosedOnLeaseRead()
		{
			bool invalid = false;
			KingdomCivicMemoryFamilyReader reader = delegate(byte[] payload, out string fault)
			{
				fault = "";
				return invalid ? (KingdomCivicMemoryNested)99 : KingdomCivicMemoryNested.Current;
			};
			KingdomCivicMemoryAuthority authority = Loaded(
				KingdomCivicMemoryTestFamilies.Sound(4), Table(reader));
			invalid = true;

			Assert.That(authority.TryReadSection(Section,
				out KingdomCivicMemorySectionLease lease, out string failure), Is.False);
			Assert.That(lease, Is.Null);
			StringAssert.Contains("unsupported family verdict 99", failure);
			Assert.That(authority.Latch.Tripped, Is.True);
		}

		[Test]
		public void QuarantinedAuthorityNeverIssuesASectionLease()
		{
			byte[] evidence = { 1, 2, 3 };
			KingdomCivicMemoryAuthority authority = Authority();
			authority.AdoptSaved(evidence);

			Assert.That(authority.TryReadSection(Section,
				out KingdomCivicMemorySectionLease lease, out string failure), Is.False);
			Assert.That(lease, Is.Null);
			StringAssert.Contains("read-only", failure);
			CollectionAssert.AreEqual(evidence, authority.Read().RetainedPayload());
		}

		[Test]
		public void FutureOuterAuthorityNeverIssuesASectionLease()
		{
			byte[] future = FutureSaved(KingdomCivicMemoryTestFamilies.Sound(4));
			KingdomCivicMemoryAuthority authority = Authority();
			authority.AdoptSaved(future);

			Assert.That(authority.TryReadSection(Section,
				out KingdomCivicMemorySectionLease lease, out string failure), Is.False);
			Assert.That(lease, Is.Null);
			StringAssert.Contains("newer build", failure);
			Assert.That(authority.IsFutureOuter, Is.True);
			Assert.That(authority.Latch.Tripped, Is.False);
			CollectionAssert.AreEqual(future, authority.Encode());
		}

		[Test]
		public void InvalidInputsNeverChangeAuthority()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			Assert.That(authority.TryReadSection(0,
				out KingdomCivicMemorySectionLease lease, out string failure), Is.False);
			Assert.That(lease, Is.Null);
			Assert.That(authority.TryCommitSection(null, new byte[] { 1 }, out failure), Is.False);
			Assert.That(authority.TryReadSection(Section, out lease, out failure), Is.True, failure);
			Assert.That(authority.TryCommitSection(lease, null, out failure), Is.False);
			Assert.That(authority.Revision, Is.Zero);
			Assert.That(authority.IsEmpty, Is.True);
		}

		private static KingdomCivicMemoryAuthority Authority()
		{
			return new KingdomCivicMemoryAuthority(KingdomCivicMemoryTestFamilies.Table());
		}

		private static KingdomCivicMemoryAuthority Loaded(byte[] payload,
			KingdomCivicMemoryFamilyTable table)
		{
			KingdomCivicMemoryAuthority authority = new KingdomCivicMemoryAuthority(table);
			authority.AdoptSaved(Saved(payload));
			return authority;
		}

		private static byte[] Saved(byte[] payload)
		{
			return KingdomCivicMemoryCodec.Encode(KingdomCivicMemoryState.Of(
				new List<KingdomCivicMemorySection>
				{
					new KingdomCivicMemorySection(Section, payload)
				}, 0L));
		}

		private static byte[] FutureSaved(byte[] payload)
		{
			byte[] envelope = Saved(payload);
			int version = KingdomCivicMemoryCodec.CurrentWireVersion + 1;
			envelope[4] = (byte)version;
			envelope[5] = (byte)(version >> 8);
			envelope[6] = (byte)(version >> 16);
			envelope[7] = (byte)(version >> 24);
			byte[] body = new byte[envelope.Length - 32];
			System.Buffer.BlockCopy(envelope, 0, body, 0, body.Length);
			using (System.Security.Cryptography.SHA256 sha =
				System.Security.Cryptography.SHA256.Create())
				System.Buffer.BlockCopy(sha.ComputeHash(body), 0, envelope, body.Length, 32);
			return envelope;
		}

		private static KingdomCivicMemoryFamilyTable Table(
			KingdomCivicMemoryFamilyReader sectionReader)
		{
			KingdomCivicMemoryFamilyTable table = new KingdomCivicMemoryFamilyTable();
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++)
				table.Add(id, id == Section ? sectionReader : AlwaysCurrent);
			return table;
		}

		private static KingdomCivicMemoryNested AlwaysCurrent(byte[] payload, out string fault)
		{
			fault = "";
			return KingdomCivicMemoryNested.Current;
		}
	}
}
#endif
