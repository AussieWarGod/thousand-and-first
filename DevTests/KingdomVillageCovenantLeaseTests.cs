#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The seam between the archive and the authority that keeps it for a save.
	/// <para>
	/// One lease spans a whole recording. Everything decided while the archive was being read has
	/// to still be true of the archive being written, and the only instrument that guarantees that
	/// is the lease the read handed back. These cases prove the transition rides that one object,
	/// that a retry of the same rite costs the save nothing, and that a conflict costs it nothing
	/// either.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomVillageCovenantLeaseTests
	{
		private const string Realm = KingdomVillageCovenantTests.Realm;
		private const string OtherRealm = KingdomVillageCovenantTests.OtherRealm;
		private const int Section = KingdomCivicMemoryLimits.SectionVillageCovenant;

		private static KingdomCivicMemoryNested Anything(byte[] Payload, out string Fault)
		{
			Fault = "";
			return KingdomCivicMemoryNested.Current;
		}

		/// <summary>Section nine answered by its real family; the other eight by a stand-in that is
		/// never given anything to read.</summary>
		private static KingdomCivicMemoryFamilyTable Table()
		{
			KingdomCivicMemoryFamilyTable table = new KingdomCivicMemoryFamilyTable();
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++)
				table.Add(id, id == Section
					? (KingdomCivicMemoryFamilyReader)KingdomVillageCovenantInspection.Inspect
					: Anything);
			return table;
		}

		private static KingdomCivicMemoryAuthority Empty()
		{
			KingdomCivicMemoryAuthority authority = new KingdomCivicMemoryAuthority(Table());
			authority.AdoptAbsent();
			return authority;
		}

		private static KingdomCivicMemoryAuthority Holding(byte[] payload)
		{
			KingdomCivicMemoryAuthority authority = new KingdomCivicMemoryAuthority(Table());
			authority.AdoptSaved(KingdomCivicMemoryCodec.Encode(KingdomCivicMemoryState.Of(
				new List<KingdomCivicMemorySection>
				{
					new KingdomCivicMemorySection(Section, payload)
				}, 0L)));
			return authority;
		}

		private static byte[] Bytes(KingdomVillageCovenantArchive archive)
		{
			Assert.IsTrue(KingdomVillageCovenantCodec.TryEncode(archive, out byte[] bytes,
				out string failure), failure);
			return bytes;
		}

		private static bool Record(KingdomCivicMemoryAuthority authority,
			KingdomVillageCovenantReceipt row, out KingdomVillageCovenantAppend outcome,
			out string failure)
		{
			return Record(authority, row, out outcome, out _, out failure);
		}

		/// <summary>The whole recording, under one lease, exactly as the runtime performs it.</summary>
		private static bool Record(KingdomCivicMemoryAuthority authority,
			KingdomVillageCovenantReceipt row, out KingdomVillageCovenantAppend outcome,
			out KingdomVillageCovenantReceipt effective, out string failure)
		{
			outcome = KingdomVillageCovenantAppend.AlreadyRecorded;
			effective = null;
			return KingdomVillageCovenantLease.TryReadArchive(authority, Realm,
					out KingdomCivicMemorySectionLease lease, out _, out failure)
				&& KingdomVillageCovenantLease.TryCommitAppended(authority, lease, Realm, row,
					out outcome, out effective, out failure)
				&& KingdomVillageCovenantLease.TryConfirm(authority, Realm, effective, out failure);
		}

		// ---- the preflight, before a dram is spent ---------------------------------------

		[Test]
		public void APreflightOnAFreshSaveSucceedsAndSpendsNothing()
		{
			KingdomCivicMemoryAuthority authority = Empty();
			long before = authority.Revision;
			Assert.IsTrue(KingdomVillageCovenantLease.TryPreflight(authority, Realm, null,
				out string failure), failure);
			Assert.AreEqual(before, authority.Revision, "a preflight must not write");
		}

		[Test]
		public void APreflightRefusesWhenTheAuthorityHasGoneReadOnly()
		{
			KingdomCivicMemoryAuthority authority = new KingdomCivicMemoryAuthority(Table());
			authority.AdoptUnreadableFraming(new byte[] { 1, 2 }, "the block framing was garbage");
			Assert.IsTrue(authority.ReadOnly);
			Assert.IsFalse(KingdomVillageCovenantLease.TryPreflight(authority, Realm, null,
				out string failure));
			StringAssert.Contains("read-only", failure);
		}

		[Test]
		public void APreflightRefusesWhenTheArchiveIsAlreadyFull()
		{
			KingdomVillageCovenantArchive archive = KingdomVillageCovenantTests.Bound();
			for (int i = 0; i < KingdomVillageCovenantArchive.MaxRows; i++)
			{
				string transaction = i.ToString("x2") + "0123456789abcdef0123456789abcd";
				Assert.IsTrue(KingdomVillageCovenantRules.TryAppend(archive,
					KingdomVillageCovenantTests.Row(transaction, "Village" + i, "Village " + i),
					Realm, out archive, out _, out _, out string failure), failure);
			}
			KingdomCivicMemoryAuthority authority = Holding(Bytes(archive));
			Assert.IsFalse(KingdomVillageCovenantLease.TryPreflight(authority, Realm, null,
				out string full));
			StringAssert.Contains("is full at", full);
		}

		[Test]
		public void APreflightRefusesAnArchiveThatBelongsToAnotherRealm()
		{
			KingdomVillageCovenantArchive foreign =
				KingdomVillageCovenantTests.Bound(OtherRealm);
			KingdomCivicMemoryAuthority authority = Holding(Bytes(foreign));
			Assert.IsFalse(KingdomVillageCovenantLease.TryPreflight(authority, Realm, null,
				out string failure));
			StringAssert.Contains("belongs to another realm", failure);
		}

		[Test]
		public void APreflightRefusesARealmIdThatIsNotCanonical()
		{
			Assert.IsFalse(KingdomVillageCovenantLease.TryPreflight(Empty(), "taf:realm:v1:nope", null,
				out string failure));
			StringAssert.Contains("not canonical", failure);
		}

		// ---- recording ------------------------------------------------------------------

		[Test]
		public void RecordingOneCovenantWritesItAndTheSaveCanBeAskedToConfirmIt()
		{
			KingdomCivicMemoryAuthority authority = Empty();
			KingdomVillageCovenantReceipt row = KingdomVillageCovenantTests.Row();
			long before = authority.Revision;
			Assert.IsTrue(Record(authority, row, out KingdomVillageCovenantAppend outcome,
				out string failure), failure);
			Assert.AreEqual(KingdomVillageCovenantAppend.Recorded, outcome);
			Assert.AreEqual(before + 1L, authority.Revision);
			Assert.IsTrue(KingdomVillageCovenantLease.TryConfirm(authority, Realm, row,
				out string confirm), confirm);
		}

		[Test]
		public void RetryingTheSameRiteIsIdempotentAndSpendsNoRevision()
		{
			KingdomCivicMemoryAuthority authority = Empty();
			KingdomVillageCovenantReceipt row = KingdomVillageCovenantTests.Row();
			Assert.IsTrue(Record(authority, row, out _, out string first), first);
			long after = authority.Revision;

			Assert.IsTrue(Record(authority, row.Copy(),
				out KingdomVillageCovenantAppend outcome, out string second), second);
			Assert.AreEqual(KingdomVillageCovenantAppend.AlreadyRecorded, outcome);
			Assert.AreEqual(after, authority.Revision,
				"a retry of the same exact rite must cost the save nothing");
			Assert.IsTrue(KingdomVillageCovenantLease.TryConfirm(authority, Realm, row, out _));
		}

		[Test]
		public void MigratedRealmRecordsAndRetriesThroughTheActualRuntimeCut()
		{
			const string legacyFactionKey = "Kavvat";
			KingdomCivicMemoryAuthority authority = Empty();
			KingdomVillageCovenantReceipt row = KingdomVillageCovenantRules.Receipt(Realm,
				KingdomVillageCovenantTests.Transaction,
				KingdomVillageCovenantTests.Authority(KingdomVillageCovenantTests.Transaction,
					legacyFactionKey, KingdomVillageCovenantTests.Zone),
				KingdomVillageCovenantTests.FactionId, KingdomVillageCovenantTests.Display,
				KingdomVillageCovenantTests.Zone,
				KingdomVillageCovenantTests.Event(KingdomVillageCovenantTests.Transaction),
				KingdomVillageCovenantTests.Sealed, KingdomVillageCovenantTests.Tick);

			Assert.AreNotEqual(row.RealmId, legacyFactionKey);
			Assert.IsTrue(KingdomVillageCovenantRuntimeCut.TryRecord(authority, Realm,
				legacyFactionKey, row, out KingdomVillageCovenantAppend first,
				out KingdomVillageCovenantReceipt effective, out string failure), failure);
			Assert.AreEqual(KingdomVillageCovenantAppend.Recorded, first);
			Assert.AreEqual(row.RealmId, effective.RealmId);
			Assert.AreEqual(row.FoundingAuthority, effective.FoundingAuthority);
			long after = authority.Revision;

			Assert.IsTrue(KingdomVillageCovenantRuntimeCut.TryRecord(authority, Realm,
				legacyFactionKey, row.Copy(), out KingdomVillageCovenantAppend retry,
				out KingdomVillageCovenantReceipt retried, out failure), failure);
			Assert.AreEqual(KingdomVillageCovenantAppend.AlreadyRecorded, retry);
			Assert.AreEqual(after, authority.Revision);
			Assert.AreEqual(effective.ReceiptId, retried.ReceiptId);
			Assert.IsFalse(KingdomVillageCovenantRuntimeCut.TryRecord(authority, Realm,
				Realm, row.Copy(), out _, out _, out string wrong));
			StringAssert.Contains("minted under another realm", wrong);
		}

		[Test]
		public void AConflictingCovenantIsRefusedAndTheRecordedOneSurvives()
		{
			KingdomCivicMemoryAuthority authority = Empty();
			KingdomVillageCovenantReceipt row = KingdomVillageCovenantTests.Row();
			Assert.IsTrue(Record(authority, row, out _, out string first), first);
			long after = authority.Revision;

			Assert.IsFalse(Record(authority,
				KingdomVillageCovenantTests.Row(display: "a village that never agreed"),
				out _, out string failure));
			StringAssert.Contains("kept rather than replaced", failure);
			Assert.AreEqual(after, authority.Revision);
			Assert.IsTrue(KingdomVillageCovenantLease.TryConfirm(authority, Realm, row,
				out string confirm), confirm);
		}

		/// <summary>
		/// Confirmation asks the save and believes nothing else. A save that has no archive at all
		/// and a save whose archive simply lacks this covenant are both refusals, and they say
		/// different things: the first has nothing bound to this realm, the second has an archive
		/// bound to it that does not contain the rite being claimed.
		/// </summary>
		[Test]
		public void ConfirmingACovenantTheSaveNeverTookIsARefusal()
		{
			Assert.IsFalse(KingdomVillageCovenantLease.TryConfirm(Empty(), Realm,
				KingdomVillageCovenantTests.Row(), out string unbound));
			StringAssert.Contains("not bound to this realm", unbound);

			KingdomCivicMemoryAuthority authority = Empty();
			Assert.IsTrue(Record(authority, KingdomVillageCovenantTests.Row(), out _,
				out string recorded), recorded);
			Assert.IsFalse(KingdomVillageCovenantLease.TryConfirm(authority, Realm,
				KingdomVillageCovenantTests.Row("fedcba9876543210fedcba9876543210", "Kyakukya",
					"the people of Kyakukya"), out string missing));
			StringAssert.Contains("no covenant matching this exact founding transaction", missing);

			Assert.IsFalse(KingdomVillageCovenantLease.TryConfirm(authority, Realm, null,
				out string nothing));
			StringAssert.Contains("no covenant to confirm", nothing);
		}

		/// <summary>
		/// The lease is the whole guarantee. Once the save has moved, the lease that was read
		/// before it moved may not be committed under, and the archive is left exactly as the
		/// commit that did land left it.
		/// </summary>
		[Test]
		public void AStaleLeaseIsRefusedAfterTheSaveHasMovedUnderneathIt()
		{
			KingdomCivicMemoryAuthority authority = Empty();
			Assert.IsTrue(KingdomVillageCovenantLease.TryReadArchive(authority, Realm,
				out KingdomCivicMemorySectionLease stale, out _, out string read), read);

			KingdomVillageCovenantReceipt landed = KingdomVillageCovenantTests.Row();
			Assert.IsTrue(Record(authority, landed, out _, out string first), first);

			Assert.IsFalse(KingdomVillageCovenantLease.TryCommitAppended(authority, stale, Realm,
				KingdomVillageCovenantTests.Row("fedcba9876543210fedcba9876543210", "Kyakukya",
					"the people of Kyakukya"), out _, out _, out string failure));
			StringAssert.Contains("moved to revision", failure);
			Assert.IsTrue(KingdomVillageCovenantLease.TryConfirm(authority, Realm, landed, out _));
		}

		[Test]
		public void ALeaseFromAnotherAuthorityIsRefused()
		{
			KingdomCivicMemoryAuthority mine = Empty();
			KingdomCivicMemoryAuthority theirs = Empty();
			Assert.IsTrue(KingdomVillageCovenantLease.TryReadArchive(theirs, Realm,
				out KingdomCivicMemorySectionLease foreign, out _, out string read), read);
			Assert.IsFalse(KingdomVillageCovenantLease.TryCommitAppended(mine, foreign, Realm,
				KingdomVillageCovenantTests.Row(), out _, out _, out string failure));
			StringAssert.Contains("issued by another authority", failure);
		}

		[Test]
		public void RecordingWithoutAnAuthorityOrALeaseIsRefusedRatherThanAssumed()
		{
			Assert.IsFalse(KingdomVillageCovenantLease.TryReadArchive(null, Realm, out _, out _,
				out string noAuthority));
			StringAssert.Contains("no civic-memory authority", noAuthority);
			Assert.IsFalse(KingdomVillageCovenantLease.TryCommitAppended(Empty(), null, Realm,
				KingdomVillageCovenantTests.Row(), out _, out _, out string noLease));
			StringAssert.Contains("no covenant-archive lease", noLease);
		}

		[Test]
		public void ASecondCovenantJoinsTheFirstRatherThanReplacingIt()
		{
			KingdomCivicMemoryAuthority authority = Empty();
			KingdomVillageCovenantReceipt first = KingdomVillageCovenantTests.Row();
			KingdomVillageCovenantReceipt second = KingdomVillageCovenantTests.Row(
				"fedcba9876543210fedcba9876543210", "Kyakukya", "the people of Kyakukya");
			Assert.IsTrue(Record(authority, first, out _, out string a), a);
			Assert.IsTrue(Record(authority, second, out _, out string b), b);
			Assert.IsTrue(KingdomVillageCovenantLease.TryReadArchive(authority, Realm, out _,
				out KingdomVillageCovenantArchive archive, out string read), read);
			Assert.AreEqual(2, archive.Rows.Count);
			Assert.IsTrue(KingdomVillageCovenantLease.TryConfirm(authority, Realm, first, out _));
			Assert.IsTrue(KingdomVillageCovenantLease.TryConfirm(authority, Realm, second, out _));
		}

		// ---- recovery: the archive is re-proved, never rewritten from today's ledger ------

		/// <summary>
		/// The crash between the archive commit and the rite finishing.
		/// <para>
		/// The covenant is durably recorded and the basin never got to clear its receipt. The next
		/// run replays the same transaction and must simply find its work already done: no second
		/// row, no spent revision, and the archived covenant still standing as the thing that can
		/// be confirmed.
		/// </para>
		/// </summary>
		[Test]
		public void ARetryAfterACrashBetweenTheArchiveAndCompletionFindsItsWorkDone()
		{
			KingdomCivicMemoryAuthority authority = Empty();
			KingdomVillageCovenantReceipt row = KingdomVillageCovenantTests.Row();
			Assert.IsTrue(Record(authority, row, out _, out string first), first);
			long after = authority.Revision;

			Assert.IsTrue(Record(authority, KingdomVillageCovenantTests.Row(),
				out KingdomVillageCovenantAppend outcome,
				out KingdomVillageCovenantReceipt effective, out string retry), retry);
			Assert.AreEqual(KingdomVillageCovenantAppend.AlreadyRecorded, outcome);
			Assert.AreEqual(after, authority.Revision);
			Assert.IsTrue(KingdomVillageCovenantRules.Same(row, effective));

			Assert.IsTrue(KingdomVillageCovenantLease.TryReadArchive(authority, Realm, out _,
				out KingdomVillageCovenantArchive archive, out string read), read);
			Assert.AreEqual(1, archive.Rows.Count);
		}

		/// <summary>
		/// The same crash, with the world having moved underneath it.
		/// <para>
		/// Between the archive commit and the basin finishing, something nudged the realm's
		/// standing with that village. A retry rebuilds its candidate from today's ledger and so
		/// arrives with a different sealed standing &mdash; and must still finish. The covenant is
		/// what the archive froze, not what today says: the row keeps its original standing, no
		/// second row appears, no revision is spent, and the covenant the caller must confirm is
		/// the archived one rather than the one it just built.
		/// </para>
		/// </summary>
		[Test]
		public void ARetryAfterTheStandingMovedFinishesAgainstTheArchivedCovenant()
		{
			KingdomCivicMemoryAuthority authority = Empty();
			KingdomVillageCovenantReceipt sealedAt600 = KingdomVillageCovenantTests.Row();
			Assert.IsTrue(Record(authority, sealedAt600, out _, out string first), first);
			long after = authority.Revision;

			KingdomVillageCovenantReceipt rebuiltAt750 =
				KingdomVillageCovenantTests.Row(sealedStanding: 750, tick: 9999L);
			Assert.AreNotEqual(sealedAt600.ReceiptId, rebuiltAt750.ReceiptId,
				"a moved standing really does produce a differently-named candidate");

			Assert.IsTrue(Record(authority, rebuiltAt750, out KingdomVillageCovenantAppend outcome,
				out KingdomVillageCovenantReceipt effective, out string retry), retry);
			Assert.AreEqual(KingdomVillageCovenantAppend.AlreadyRecorded, outcome);
			Assert.AreEqual(after, authority.Revision, "a recovery must not spend a revision");
			Assert.IsTrue(KingdomVillageCovenantRules.Same(sealedAt600, effective),
				"the covenant that stands is the one the archive froze");
			Assert.AreEqual(KingdomVillageCovenantTests.Sealed, effective.SealedStanding);
			Assert.AreEqual(KingdomVillageCovenantTests.Tick, effective.ReservationTick);

			Assert.IsTrue(KingdomVillageCovenantLease.TryReadArchive(authority, Realm, out _,
				out KingdomVillageCovenantArchive archive, out string read), read);
			Assert.AreEqual(1, archive.Rows.Count);
			Assert.AreEqual(KingdomVillageCovenantTests.Sealed, archive.Rows[0].SealedStanding);
			Assert.IsFalse(KingdomVillageCovenantLease.TryConfirm(authority, Realm, rebuiltAt750,
				out _), "the rebuilt candidate is not what the save holds, and says so");
		}

		/// <summary>
		/// A field that cannot move is still a conflict. Only the standing and the reservation tick
		/// are read from a world that keeps moving; everything else disagreeing means this is a
		/// different covenant wearing the same transaction.
		/// </summary>
		[Test]
		public void ARetryThatDisagreesAboutAnythingThatCannotMoveIsStillAConflict()
		{
			KingdomCivicMemoryAuthority authority = Empty();
			Assert.IsTrue(Record(authority, KingdomVillageCovenantTests.Row(), out _,
				out string first), first);
			long after = authority.Revision;
			Assert.IsFalse(Record(authority,
				KingdomVillageCovenantTests.Row(display: "a village that never agreed",
					sealedStanding: 750), out _, out string failure));
			StringAssert.Contains("kept rather than replaced", failure);
			Assert.AreEqual(after, authority.Revision);
		}

		// ---- the pre-debit candidate refusal ---------------------------------------------

		/// <summary>
		/// The preflight proves this exact covenant would encode, not merely that there is room for
		/// one. A faction whose display name is lawful to charter and too wide to record must be
		/// refused while the water is still in the basin.
		/// </summary>
		[Test]
		public void APreflightRefusesACandidateThatCouldNotBeRecorded()
		{
			KingdomCivicMemoryAuthority authority = Empty();
			long before = authority.Revision;
			string tooWide = new string('\u4e00', KingdomVillageCovenantRules.MaxNameChars + 1);
			Assert.IsFalse(KingdomVillageCovenantLease.TryPreflight(authority, Realm,
				KingdomVillageCovenantTests.Row(display: tooWide), out string failure));
			StringAssert.Contains("display-name snapshot is unusable", failure);
			Assert.AreEqual(before, authority.Revision, "a refused preflight must not write");

			Assert.IsTrue(KingdomVillageCovenantLease.TryPreflight(authority, Realm,
				KingdomVillageCovenantTests.Row(), out string lawful), lawful);
			Assert.AreEqual(before, authority.Revision,
				"a preflight that passes must not write either");
		}

		[Test]
		public void APreflightRefusesACandidateBelongingToAnotherRealm()
		{
			Assert.IsFalse(KingdomVillageCovenantLease.TryPreflight(Empty(), Realm,
				KingdomVillageCovenantTests.Row(realm: OtherRealm), out string failure));
			StringAssert.Contains("names another realm than the archive recording it", failure);
		}

		// ---- what the envelope does with a section it cannot read ------------------------

		[Test]
		public void ALaterBuildsCovenantSectionIsCarriedAndNeverOpenedForWriting()
		{
			byte[] future = KingdomVillageCovenantFutureTests.Forge(
				KingdomVillageCovenantCodec.CurrentWireVersion + 1, new byte[] { 4, 5, 6 });
			KingdomCivicMemoryAuthority authority = Holding(future);
			Assert.IsFalse(authority.Quarantined, "a lawful successor is not damage");
			Assert.IsFalse(KingdomVillageCovenantLease.TryReadArchive(authority, Realm, out _,
				out _, out string failure));
			StringAssert.Contains("newer family version", failure);
			CollectionAssert.AreEqual(future,
				authority.Read().Section(Section).Payload(),
				"the bytes a later build wrote are carried unchanged");
		}

		[Test]
		public void ACovenantSectionThatWillNotReadQuarantinesTheWholeSaveRatherThanBeingReplaced()
		{
			byte[] bytes = KingdomVillageCovenantArchiveTests.Encoded();
			bytes[0] ^= 0x01;
			KingdomCivicMemoryAuthority authority = Holding(bytes);
			Assert.IsTrue(authority.Quarantined);
			Assert.IsTrue(authority.ReadOnly);
			Assert.IsFalse(KingdomVillageCovenantLease.TryPreflight(authority, Realm, null,
				out string failure));
			StringAssert.Contains("read-only", failure);
		}

		[Test]
		public void TheFamilyTableGivesSectionNineTheRealCovenantVerdicts()
		{
			KingdomCivicMemoryFamilyTable table = Table();
			Assert.IsTrue(table.Complete);
			Assert.AreEqual(KingdomCivicMemoryNested.Current,
				table.Inspect(Section, KingdomVillageCovenantArchiveTests.Encoded(), out _));
			Assert.AreEqual(KingdomCivicMemoryNested.Future, table.Inspect(Section,
				KingdomVillageCovenantFutureTests.Forge(
					KingdomVillageCovenantCodec.CurrentWireVersion + 1, new byte[] { 1 }), out _));
			Assert.AreEqual(KingdomCivicMemoryNested.Malformed,
				table.Inspect(Section, new byte[] { 1, 2, 3 }, out string fault));
			StringAssert.Contains("village-covenant archive", fault);
		}
	}
}
#endif
