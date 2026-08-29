#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The seam to civic memory: which of the two books this build is entitled to write, and what
	/// happens to the one it is not.
	/// </summary>
	[TestFixture]
	public sealed class KingdomCuriosityLeadCommitTests
	{
		[Test]
		public void BothCompatibleBooksAreOfferedAndLandUnderTheirOwnSectionIds()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), Leads(),
				authority.Revision, out KingdomCuriosityLeadCommitReport report,
				out string failure), failure);
			Assert.AreEqual(KingdomCuriosityLeadCarriage.Offered, report.Curiosity);
			Assert.AreEqual(KingdomCuriosityLeadCarriage.Offered, report.CivicLeads);
			KingdomCivicMemoryState state = authority.Read();
			Assert.IsNotNull(Section(state, KingdomCivicMemoryLimits.SectionCuriosity));
			Assert.IsNotNull(Section(state, KingdomCivicMemoryLimits.SectionCivicLeads));
		}

		/// <summary>
		/// A future book is withheld rather than re-emitted, and the authority's own upsert
		/// carries the bytes already in the save. The proof is that the section still holds the
		/// exact payload the save arrived with, untouched by this commit.
		/// </summary>
		[Test]
		public void AFutureBookIsWithheldAndItsExistingBytesAreCarriedThrough()
		{
			byte[] future = KingdomCuriosityLeadCodecTests.Future(0x31554354, 7);
			KingdomCivicMemoryAuthority authority = Authority(
				new KingdomCivicMemorySection(KingdomCivicMemoryLimits.SectionCuriosity, future));
			KingdomCuriosityBook curiosity = KingdomCuriosityLeadCodec.DecodeCuriosity(future);
			Assert.AreEqual(KingdomCuriosityBookState.FutureOpaque, curiosity.State);

			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(authority, curiosity, Leads(),
				authority.Revision, out KingdomCuriosityLeadCommitReport report,
				out string failure), failure);
			Assert.AreEqual(KingdomCuriosityLeadCarriage.Withheld, report.Curiosity);
			StringAssert.Contains("later build", report.CuriosityReason);
			Assert.AreEqual(KingdomCuriosityLeadCarriage.Offered, report.CivicLeads);
			CollectionAssert.AreEqual(future,
				Section(authority.Read(), KingdomCivicMemoryLimits.SectionCuriosity).Payload(),
				"the later build's bytes must come through this commit unchanged");
		}

		/// <summary>
		/// A quarantined book cannot reach this seam from a real save, and the reason is worth
		/// stating rather than assuming.
		/// <para>
		/// If a save carries a curiosity payload this family refuses, the family says so while the
		/// envelope is being adopted, and civic memory quarantines the whole envelope and latches.
		/// The authority is then read-only, so the commit is refused before either book's state is
		/// even consulted &mdash; and the save keeps the exact bytes it arrived with. Withholding
		/// a quarantined book is defended in the carriage rules, but this is the path a founder
		/// actually meets, and it refuses earlier and harder.
		/// </para>
		/// </summary>
		[Test]
		public void ASaveHoldingAnUnreadableCuriosityPayloadGoesReadOnlyRatherThanBeingWrittenOver()
		{
			byte[] damaged = { 1, 2, 3, 4, 5, 6, 7, 8 };
			KingdomCuriosityBook curiosity = KingdomCuriosityLeadCodec.DecodeCuriosity(damaged);
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, curiosity.State);

			KingdomCivicMemoryAuthority authority = new KingdomCivicMemoryAuthority(Table());
			authority.AdoptSaved(KingdomCivicMemoryCodec.Encode(KingdomCivicMemoryState.Of(
				new List<KingdomCivicMemorySection>
				{
					new KingdomCivicMemorySection(KingdomCivicMemoryLimits.SectionCuriosity,
						damaged)
				}, 1L)));
			Assert.IsTrue(authority.ReadOnly,
				"a family that refuses its own payload must put civic memory beyond writing");

			long revision = authority.Revision;
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommit(authority, curiosity, Leads(),
				revision, out _, out string failure));
			StringAssert.Contains("read-only", failure);
			Assert.AreEqual(revision, authority.Revision);
			CollectionAssert.AreEqual(damaged, curiosity.OpaquePayload,
				"the refused payload is kept whole as the evidence of what went wrong");
		}

		/// <summary>
		/// The carriage rule for a quarantined book, exercised on its own.
		/// <para>
		/// This one authority is built with a permissive family table, and deliberately. With the
		/// real reader wired, a section this family refuses can never be held at all &mdash; civic
		/// memory quarantines the whole envelope on the way in, as the test above shows &mdash; so
		/// the carriage rule would be unreachable and untested. It is defence in depth behind an
		/// earlier and harder refusal, and this is the only way to prove it works.
		/// </para>
		/// </summary>
		[Test]
		public void AQuarantinedBookIsWithheldOnlyWhileTheSaveStillHoldsItsEvidence()
		{
			byte[] damaged = { 1, 2, 3, 4, 5, 6, 7, 8 };
			KingdomCuriosityBook curiosity = KingdomCuriosityLeadCodec.DecodeCuriosity(damaged);
			Assert.AreEqual(KingdomCuriosityBookState.Quarantined, curiosity.State);

			KingdomCivicMemoryAuthority holding = Permissive();
			Assert.IsTrue(holding.TryCommit(new List<KingdomCivicMemorySection>
			{
				new KingdomCivicMemorySection(KingdomCivicMemoryLimits.SectionCuriosity, damaged)
			}, holding.Revision, out string seeded), seeded);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(holding, curiosity, Leads(),
				holding.Revision, out KingdomCuriosityLeadCommitReport report,
				out string failure), failure);
			Assert.AreEqual(KingdomCuriosityLeadCarriage.Withheld, report.Curiosity);
			StringAssert.Contains("evidence", report.CuriosityReason);
			CollectionAssert.AreEqual(damaged,
				Section(holding.Read(), KingdomCivicMemoryLimits.SectionCuriosity).Payload(),
				"the original evidence must still stand in the save after this commit");

			KingdomCivicMemoryAuthority empty = Permissive();
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommit(empty, curiosity, Leads(),
				empty.Revision, out report, out failure));
			StringAssert.Contains("holds no such section", report.CuriosityReason);
		}

		private static KingdomCivicMemoryAuthority Permissive()
		{
			KingdomCivicMemoryFamilyTable table = new KingdomCivicMemoryFamilyTable();
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++) table.Add(id, Anything);
			return new KingdomCivicMemoryAuthority(table);
		}

		/// <summary>
		/// Withholding is a claim that the save already holds these records, and a claim that is
		/// checked. If the section is not there, withholding would leave the founder's records
		/// nowhere at all while reporting success, so the whole commit refuses instead.
		/// </summary>
		[Test]
		public void WithholdingRefusesWhenTheSaveHoldsNoSuchSection()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			long revision = authority.Revision;
			KingdomCuriosityBook future = KingdomCuriosityLeadCodec.DecodeCuriosity(
				KingdomCuriosityLeadCodecTests.Future(0x31554354, 7));
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommit(authority, future, Leads(),
				revision, out KingdomCuriosityLeadCommitReport report, out string failure));
			Assert.AreEqual(KingdomCuriosityLeadCarriage.Withheld, report.Curiosity);
			StringAssert.Contains("holds no such section", report.CuriosityReason);
			Assert.IsNotEmpty(failure);
			Assert.AreEqual(revision, authority.Revision);
			Assert.IsNull(Section(authority.Read(), KingdomCivicMemoryLimits.SectionCivicLeads),
				"the lead book must not land while the curiosity book stops the commit");
		}

		/// <summary>Withholding against a section holding <i>different</i> bytes is refused too:
		/// carrying those through would leave the wrong records standing.</summary>
		[Test]
		public void WithholdingRefusesWhenTheSavesSectionIsNotThePayloadTheBookCameFrom()
		{
			byte[] mine = KingdomCuriosityLeadCodecTests.Future(0x31554354, 7);
			byte[] theirs = KingdomCuriosityLeadCodecTests.Future(0x31554354, 9);
			KingdomCivicMemoryAuthority authority = Authority(new KingdomCivicMemorySection(
				KingdomCivicMemoryLimits.SectionCuriosity, theirs));
			KingdomCuriosityBook future = KingdomCuriosityLeadCodec.DecodeCuriosity(mine);
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommit(authority, future, Leads(),
				authority.Revision, out KingdomCuriosityLeadCommitReport report, out string _));
			StringAssert.Contains("not the payload this book was read from",
				report.CuriosityReason);
			CollectionAssert.AreEqual(theirs,
				Section(authority.Read(), KingdomCivicMemoryLimits.SectionCuriosity).Payload());
		}

		/// <summary>
		/// A prepared lead is durable only once it has come through this seam. The standing is read
		/// out of a lease on the committed section, so a receipt that was never committed, or read
		/// at a revision the save has moved past, cannot justify writing into a founder's journal.
		/// </summary>
		[Test]
		public void DurableStandingIsReadFromALeaseOnTheCommittedSection()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			KingdomCivicLeadBook leads = new KingdomCivicLeadBook();
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(leads, 0L,
				KingdomCuriosityLeadCodecTests.LeadCause(0), 0, true,
				out KingdomCivicLeadReceipt receipt, out string failure), failure);

			Assert.IsFalse(KingdomCuriosityLeadCommit.TryReadDurableStanding(authority,
				authority.Revision, receipt, out _, out _, out failure),
				"an uncommitted prepared row is not durable");
			StringAssert.Contains("no civic-lead section", failure);

			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), leads,
				authority.Revision, out _, out failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryReadDurableStanding(authority,
				authority.Revision, receipt, out KingdomCivicMemorySectionLease lease,
				out KingdomCivicLeadDurableStanding standing, out failure), failure);
			Assert.AreEqual(KingdomCivicLeadDurableStanding.Prepared, standing);
			Assert.AreEqual(authority.Revision, lease.ExpectedRevision,
				"the lease carries the revision its bytes were read at");
			Assert.AreEqual(KingdomCivicMemoryLimits.SectionCivicLeads, lease.SectionId);

			Assert.IsFalse(KingdomCuriosityLeadCommit.TryReadDurableStanding(authority,
				authority.Revision - 1, receipt, out _, out _, out failure),
				"a stale civic-memory revision must not vouch for durability");
			StringAssert.Contains("the caller read at", failure);

			KingdomCivicLeadReceipt fabricated = receipt.Copy();
			fabricated.Title += " and a little more";
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryReadDurableStanding(authority,
				authority.Revision, fabricated, out _, out _, out failure));
			StringAssert.Contains("differs from the row this authority holds", failure);

			KingdomCivicLeadReceipt foreign = receipt.Copy();
			foreign.SourceId = "taf:delve:never-prepared";
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryReadDurableStanding(authority,
				authority.Revision, foreign, out _, out _, out failure));
			StringAssert.Contains("holds no civic lead for that source", failure);
		}

		/// <summary>
		/// The commit rides the lease it was handed and never opens the section again. Given a
		/// lease, it needs no revision argument at all &mdash; and one issued by another authority
		/// is refused by the authority itself.
		/// </summary>
		[Test]
		public void TheCommitRidesTheLeaseItWasGivenAndOpensNothing()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			KingdomCivicLeadBook leads = new KingdomCivicLeadBook();
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(leads, 0L,
				KingdomCuriosityLeadCodecTests.LeadCause(0), 0, true,
				out KingdomCivicLeadReceipt receipt, out string failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), leads,
				authority.Revision, out _, out failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryReadDurableStanding(authority,
				authority.Revision, receipt, out KingdomCivicMemorySectionLease lease,
				out _, out failure), failure);

			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommitProjectedLead(authority, null,
				receipt, out failure));
			StringAssert.Contains("no civic-lead lease", failure);

			KingdomCivicMemoryAuthority other = Authority();
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(other, Curiosity(), Leads(),
				other.Revision, out _, out failure), failure);
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommitProjectedLead(other, lease,
				receipt, out failure),
				"a lease belongs to the authority that issued it");
			StringAssert.Contains("another authority", failure);

			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommitProjectedLead(authority, lease,
				receipt, out failure), failure);
			Assert.AreEqual(KingdomCivicLeadPhase.Projected, Durable(authority).Rows[0].Phase);
		}

		/// <summary>
		/// A lease that has gone stale under the caller is refused at the commit, and the durable
		/// row stays exactly as it was.
		/// </summary>
		[Test]
		public void AStaleLeaseIsRefusedAtTheCommitAndChangesNothing()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			KingdomCivicLeadBook leads = new KingdomCivicLeadBook();
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(leads, 0L,
				KingdomCuriosityLeadCodecTests.LeadCause(0), 0, true,
				out KingdomCivicLeadReceipt receipt, out string failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), leads,
				authority.Revision, out _, out failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryReadDurableStanding(authority,
				authority.Revision, receipt, out KingdomCivicMemorySectionLease lease,
				out _, out failure), failure);

			// Something else commits while this lease is being carried across the journal.
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), leads,
				authority.Revision, out _, out failure), failure);

			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommitProjectedLead(authority, lease,
				receipt, out failure), "a lease overtaken by another commit must be refused");
			Assert.AreEqual(KingdomCivicLeadPhase.Prepared, Durable(authority).Rows[0].Phase,
				"a refused commit leaves the durable row prepared, ready for the retry");
		}

		/// <summary>
		/// The prepared-row match names the revision it was read at, and refuses one it was not.
		/// A receipt read before the book moved is a receipt about a book that no longer exists,
		/// however exactly its fields still line up.
		/// </summary>
		[Test]
		public void ThePreparedRowMatchNamesTheRevisionItWasReadAt()
		{
			KingdomCivicLeadBook book = new KingdomCivicLeadBook();
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(book, 0L,
				KingdomCuriosityLeadCodecTests.LeadCause(0), 0, true,
				out KingdomCivicLeadReceipt receipt, out string failure), failure);
			Assert.IsTrue(KingdomCivicLeadRules.TryMatchPreparedRow(book, book.Revision, receipt,
				out failure), failure);
			Assert.IsFalse(KingdomCivicLeadRules.TryMatchPreparedRow(book, book.Revision - 1,
				receipt, out failure));
			StringAssert.Contains("now stands at", failure);
			Assert.IsFalse(KingdomCivicLeadRules.TryMatchPreparedRow(book, book.Revision + 1,
				receipt, out failure));
			StringAssert.Contains("now stands at", failure);

			// And the revision the caller names really is the book's, not merely any number:
			// preparing a second lead moves the book and the old reading stops matching.
			long stale = book.Revision;
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(book, book.Revision,
				KingdomCuriosityLeadCodecTests.LeadCause(1), 0, true, out _, out failure),
				failure);
			Assert.IsFalse(KingdomCivicLeadRules.TryMatchPreparedRow(book, stale, receipt,
				out failure));
			Assert.IsTrue(KingdomCivicLeadRules.TryMatchPreparedRow(book, book.Revision, receipt,
				out failure), failure);
		}

		/// <summary>A lead already durably projected reports that standing rather than being
		/// mistaken for a prepared row still awaiting its note.</summary>
		[Test]
		public void AProjectedLeadReportsProjectedRatherThanPrepared()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			KingdomCivicLeadBook leads = new KingdomCivicLeadBook();
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(leads, 0L,
				KingdomCuriosityLeadCodecTests.LeadCause(0), 0, true,
				out KingdomCivicLeadReceipt receipt, out string failure), failure);
			Assert.IsTrue(KingdomCivicLeadRules.TryMarkProjected(leads, leads.Revision,
				receipt.SourceId, receipt.LeadId, receipt.Locator, out failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), leads,
				authority.Revision, out _, out failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryReadDurableStanding(authority,
				authority.Revision, receipt, out _,
				out KingdomCivicLeadDurableStanding standing, out failure), failure);
			Assert.AreEqual(KingdomCivicLeadDurableStanding.Projected, standing);
		}

		/// <summary>A commit built against a revision the authority has moved past is refused
		/// before any section is even encoded.</summary>
		[Test]
		public void ACommitBuiltAgainstAStaleAuthorityRevisionIsRefused()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), Leads(),
				authority.Revision, out _, out string failure), failure);
			long moved = authority.Revision;
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), Leads(),
				moved - 1, out _, out failure));
			StringAssert.Contains("now stands at", failure);
			Assert.AreEqual(moved, authority.Revision);
		}

		/// <summary>An undefined state is refused at the seam as well as at the writer, so a
		/// book that cannot say what it is never reaches civic memory.</summary>
		[Test]
		public void AnUndefinedBookStateStopsTheCommitAtTheSeam()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			long revision = authority.Revision;
			KingdomCuriosityBook curiosity = Curiosity();
			curiosity.State = (KingdomCuriosityBookState)9;
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommit(authority, curiosity, Leads(),
				revision, out _, out string failure));
			StringAssert.Contains("does not define", failure);
			Assert.AreEqual(revision, authority.Revision);

			KingdomCivicLeadBook leads = Leads();
			leads.State = (KingdomCuriosityBookState)9;
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), leads,
				revision, out _, out failure));
			StringAssert.Contains("does not define", failure);
			Assert.AreEqual(revision, authority.Revision);
		}

		/// <summary>
		/// All or nothing. A book this build believes it owns and cannot write stops the whole
		/// commit, because a civic memory half-updated is worse than one left alone.
		/// </summary>
		[Test]
		public void ABookThisBuildOwnsAndCannotWriteStopsTheWholeCommit()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			long revision = authority.Revision;
			KingdomCuriosityBook broken = Curiosity();
			broken.Rows[0].Reason = "bad\ud800";
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommit(authority, broken, Leads(),
				revision, out KingdomCuriosityLeadCommitReport report, out string failure));
			Assert.AreEqual(KingdomCuriosityLeadCarriage.Unwritable, report.Curiosity);
			Assert.AreEqual(KingdomCuriosityLeadCarriage.Withheld, report.CivicLeads);
			Assert.IsNotEmpty(failure);
			Assert.AreEqual(revision, authority.Revision);
			KingdomCivicMemoryState state = authority.Read();
			Assert.IsNull(Section(state, KingdomCivicMemoryLimits.SectionCuriosity));
			Assert.IsNull(Section(state, KingdomCivicMemoryLimits.SectionCivicLeads),
				"the lead book must not land while the curiosity book is refused");
		}

		[Test]
		public void AStaleRevisionIsRefusedAndNothingIsWritten()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), Leads(),
				authority.Revision, out _, out string failure), failure);
			long current = authority.Revision;
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), Leads(),
				current - 1, out _, out failure));
			Assert.IsNotEmpty(failure);
			Assert.AreEqual(current, authority.Revision);
		}

		/// <summary>
		/// An authority that has gone read-only is never written to, whatever the books say. This
		/// is the one refusal that does not depend on either book's own state: both are perfectly
		/// good here, and the commit is still refused, because the records this session would be
		/// writing over are ones it could not read.
		/// </summary>
		[Test]
		public void AReadOnlyAuthorityIsNeverCommittedToEvenWithTwoGoodBooks()
		{
			KingdomCivicMemoryAuthority authority = Latched();
			Assert.IsTrue(authority.ReadOnly);
			long revision = authority.Revision;
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), Leads(),
				revision, out _, out string failure));
			StringAssert.Contains("read-only", failure);
			StringAssert.Contains(authority.ReadOnlyReason, failure);
			Assert.AreEqual(revision, authority.Revision);
			Assert.IsNull(authority.Read().Section(KingdomCivicMemoryLimits.SectionCuriosity));
		}

		[Test]
		public void AnAbsentAuthorityOrBookIsRefusedRatherThanSkipped()
		{
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommit(null, Curiosity(), Leads(), 0L,
				out _, out string failure));
			Assert.IsNotEmpty(failure);
			KingdomCivicMemoryAuthority authority = Authority();
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommit(authority, null, Leads(),
				authority.Revision, out _, out failure));
			Assert.IsNotEmpty(failure);
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), null,
				authority.Revision, out _, out failure));
			Assert.IsNotEmpty(failure);
		}

		/// <summary>Two books that are both beyond this build's authorship name no section, and
		/// succeed only because the save already holds both of them exactly.</summary>
		[Test]
		public void TwoWithheldBooksNameNoSectionButMustBothAlreadyBeHeld()
		{
			byte[] futureCuriosity = KingdomCuriosityLeadCodecTests.Future(0x31554354, 7);
			byte[] futureLeads = KingdomCuriosityLeadCodecTests.Future(0x314C4354, 4);
			KingdomCivicMemoryAuthority authority = Authority(
				new KingdomCivicMemorySection(KingdomCivicMemoryLimits.SectionCuriosity,
					futureCuriosity),
				new KingdomCivicMemorySection(KingdomCivicMemoryLimits.SectionCivicLeads,
					futureLeads));
			long revision = authority.Revision;
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(authority,
				KingdomCuriosityLeadCodec.DecodeCuriosity(futureCuriosity),
				KingdomCuriosityLeadCodec.DecodeLeads(futureLeads),
				revision, out KingdomCuriosityLeadCommitReport report, out string failure),
				failure);
			Assert.AreEqual(KingdomCuriosityLeadCarriage.Withheld, report.Curiosity);
			Assert.AreEqual(KingdomCuriosityLeadCarriage.Withheld, report.CivicLeads);
			Assert.AreEqual(revision, authority.Revision,
				"a commit that names nothing must not advance the authority");
			CollectionAssert.AreEqual(futureCuriosity,
				Section(authority.Read(), KingdomCivicMemoryLimits.SectionCuriosity).Payload());
			CollectionAssert.AreEqual(futureLeads,
				Section(authority.Read(), KingdomCivicMemoryLimits.SectionCivicLeads).Payload());
		}

		/// <summary>
		/// The second durable cut, made on a private decode of the leased bytes and taken by the
		/// authority before anyone is told it happened. A caller's own book is never committed.
		/// </summary>
		[Test]
		public void TheProjectedRowIsCommittedFromAPrivateDecodeOfTheLease()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			KingdomCivicLeadBook mine = new KingdomCivicLeadBook();
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(mine, 0L,
				KingdomCuriosityLeadCodecTests.LeadCause(0), 0, true,
				out KingdomCivicLeadReceipt receipt, out string failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), mine,
				authority.Revision, out _, out failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryReadDurableStanding(authority,
				authority.Revision, receipt, out KingdomCivicMemorySectionLease lease,
				out KingdomCivicLeadDurableStanding standing, out failure), failure);
			Assert.AreEqual(KingdomCivicLeadDurableStanding.Prepared, standing);

			long before = authority.Revision;
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommitProjectedLead(authority, lease,
				receipt, out failure), failure);
			Assert.Greater(authority.Revision, before, "the save must actually have taken it");
			Assert.AreEqual(KingdomCivicLeadPhase.Prepared, mine.Rows[0].Phase,
				"the caller's own book is not what was committed");
			Assert.AreEqual(KingdomCivicLeadPhase.Projected, Durable(authority).Rows[0].Phase);
		}

		/// <summary>
		/// A save cut after the journal note but before the record: the durable row is still
		/// Prepared, the note stands, and a fresh lease finishes it. This is the shape a crash
		/// between the two cuts leaves, and it must complete rather than fail for ever.
		/// </summary>
		[Test]
		public void ACutBetweenTheNoteAndTheRecordIsFinishedByTheRetry()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			KingdomCivicLeadBook mine = new KingdomCivicLeadBook();
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(mine, 0L,
				KingdomCuriosityLeadCodecTests.LeadCause(0), 0, true,
				out KingdomCivicLeadReceipt receipt, out string failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), mine,
				authority.Revision, out _, out failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryReadDurableStanding(authority,
				authority.Revision, receipt, out KingdomCivicMemorySectionLease carried,
				out _, out failure), failure);

			// The first attempt's commit is overtaken and refused.
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), mine,
				authority.Revision, out _, out failure), failure);
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommitProjectedLead(authority, carried,
				receipt, out failure));
			Assert.AreEqual(KingdomCivicLeadPhase.Prepared, Durable(authority).Rows[0].Phase,
				"a refused commit leaves the durable row prepared on purpose");

			// The retry opens a fresh lease on the same standing, and finishes.
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryReadDurableStanding(authority,
				authority.Revision, receipt, out KingdomCivicMemorySectionLease lease,
				out KingdomCivicLeadDurableStanding standing, out failure), failure);
			Assert.AreEqual(KingdomCivicLeadDurableStanding.Prepared, standing);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommitProjectedLead(authority, lease,
				receipt, out failure), failure);
			Assert.AreEqual(KingdomCivicLeadPhase.Projected, Durable(authority).Rows[0].Phase);
		}

		/// <summary>
		/// And a cut after the record: the durable row is already Projected, which is completed
		/// work rather than an error. The standing is reported, the commit is a no-op, and the
		/// authority does not move again.
		/// </summary>
		[Test]
		public void AnAlreadyProjectedDurableRowIsACompletedCommitAndNotASecondOne()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			KingdomCivicLeadReceipt receipt = Projected(authority);

			long settled = authority.Revision;
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryReadDurableStanding(authority, settled,
				receipt, out KingdomCivicMemorySectionLease lease,
				out KingdomCivicLeadDurableStanding standing, out string failure), failure);
			Assert.AreEqual(KingdomCivicLeadDurableStanding.Projected, standing);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommitProjectedLead(authority, lease,
				receipt, out failure), failure);
			Assert.AreEqual(settled, authority.Revision,
				"a completed projection must not spend another revision");
		}

		/// <summary>A receipt whose fields do not match the durable projected row is refused, so
		/// an already-finished lead cannot wave a different one through.</summary>
		[Test]
		public void AProjectedStandingStillComparesEveryFrozenField()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			KingdomCivicLeadReceipt receipt = Projected(authority);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryReadDurableStanding(authority,
				authority.Revision, receipt, out KingdomCivicMemorySectionLease lease,
				out _, out string failure), failure);

			KingdomCivicLeadReceipt altered = receipt.Copy();
			altered.Title += " and another thing";
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryReadDurableStanding(authority,
				authority.Revision, altered, out _, out _, out failure));
			StringAssert.Contains("differs from this receipt", failure);
			Assert.IsFalse(KingdomCuriosityLeadCommit.TryCommitProjectedLead(authority, lease,
				altered, out failure));
		}

		/// <summary>A lead carried all the way to a durable Projected row.</summary>
		private static KingdomCivicLeadReceipt Projected(KingdomCivicMemoryAuthority authority)
		{
			KingdomCivicLeadBook mine = new KingdomCivicLeadBook();
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(mine, 0L,
				KingdomCuriosityLeadCodecTests.LeadCause(0), 0, true,
				out KingdomCivicLeadReceipt receipt, out string failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommit(authority, Curiosity(), mine,
				authority.Revision, out _, out failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryReadDurableStanding(authority,
				authority.Revision, receipt, out KingdomCivicMemorySectionLease lease,
				out _, out failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadCommit.TryCommitProjectedLead(authority, lease,
				receipt, out failure), failure);
			return receipt;
		}

		private static KingdomCivicLeadBook Durable(KingdomCivicMemoryAuthority authority)
		{
			KingdomCivicMemorySection section = authority.Read()
				.Section(KingdomCivicMemoryLimits.SectionCivicLeads);
			Assert.IsNotNull(section);
			KingdomCivicLeadBook book = KingdomCuriosityLeadCodec.DecodeLeads(section.Payload());
			Assert.AreEqual(KingdomCuriosityBookState.Compatible, book.State, book.Fault);
			return book;
		}

		private static KingdomCivicMemorySection Section(KingdomCivicMemoryState state, int id)
		{
			return state.Section(id);
		}

		/// <summary>
		/// An authority wired with the <b>real</b> O6/D7 readers, not the marker-byte stand-ins.
		/// This is the seam under test: the family table is what decides whether a section's bytes
		/// are believed on the way in, so a commit that the real codec would refuse must be
		/// refused here too, and a later build's payload must be recognised as a future rather
		/// than as damage.
		/// </summary>
		/// <summary>The real O6/D7 readers, with the other six families answering for anything.</summary>
		private static KingdomCivicMemoryFamilyTable Table()
		{
			KingdomCivicMemoryFamilyTable table = new KingdomCivicMemoryFamilyTable();
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++)
			{
				if (id == KingdomCivicMemoryLimits.SectionCuriosity) table.Add(id, ReadCuriosity);
				else if (id == KingdomCivicMemoryLimits.SectionCivicLeads) table.Add(id, ReadLeads);
				else table.Add(id, Anything);
			}
			return table;
		}

		private static KingdomCivicMemoryAuthority Authority(
			params KingdomCivicMemorySection[] seeded)
		{
			KingdomCivicMemoryAuthority authority = new KingdomCivicMemoryAuthority(Table());
			if (seeded != null && seeded.Length > 0)
			{
				// Seeded through the save path, not through a commit. A later build's payload is
				// exactly what no caller in this build is allowed to author, so the authority
				// rightly refuses to accept one as a commit; it can only arrive from disk.
				authority.AdoptSaved(KingdomCivicMemoryCodec.Encode(
					KingdomCivicMemoryState.Of(new List<KingdomCivicMemorySection>(seeded), 1L)));
			}
			Assert.IsFalse(authority.ReadOnly, authority.ReadOnlyReason);
			return authority;
		}

		/// <summary>An authority that met a save block it could not read, and latched.</summary>
		private static KingdomCivicMemoryAuthority Latched()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			authority.AdoptSaved(new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 });
			return authority;
		}

		private static KingdomCivicMemoryNested ReadCuriosity(byte[] payload, out string fault)
		{
			return Verdict(KingdomCuriosityLeadCodec.DecodeCuriosity(payload).State, out fault);
		}

		private static KingdomCivicMemoryNested ReadLeads(byte[] payload, out string fault)
		{
			return Verdict(KingdomCuriosityLeadCodec.DecodeLeads(payload).State, out fault);
		}

		private static KingdomCivicMemoryNested Verdict(KingdomCuriosityBookState state,
			out string fault)
		{
			fault = "";
			if (state == KingdomCuriosityBookState.FutureOpaque)
				return KingdomCivicMemoryNested.Future;
			if (state == KingdomCuriosityBookState.Quarantined)
			{
				fault = "the book would not read";
				return KingdomCivicMemoryNested.Malformed;
			}
			return KingdomCivicMemoryNested.Current;
		}

		private static KingdomCivicMemoryNested Anything(byte[] payload, out string fault)
		{
			fault = ""; return KingdomCivicMemoryNested.Current;
		}

		private static KingdomCuriosityBook Curiosity()
		{
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			Assert.IsTrue(KingdomCuriosityRules.TryPrepare(book, 0L,
				KingdomCuriosityLeadCodecTests.Cause("one"),
				KingdomCuriosityLeadCodecTests.Notes(), out _, out string failure), failure);
			return book;
		}

		private static KingdomCivicLeadBook Leads()
		{
			KingdomCivicLeadBook book = new KingdomCivicLeadBook();
			Assert.IsTrue(KingdomCivicLeadRules.TryPrepare(book, 0L,
				KingdomCuriosityLeadCodecTests.LeadCause(0), 0, true, out _, out string failure),
				failure);
			return book;
		}
	}
}
#endif
