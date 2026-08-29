#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The disclosure and the commit are one transaction. These are the ways that transaction is
	/// raced, mis-attributed, or asked to exceed itself, and what the save looks like afterwards.
	/// </summary>
	[TestFixture]
	public sealed class KingdomArtifactRecognitionTransactionTests
	{
		private const string Realm = KingdomArtifactRecognitionServiceTests.Realm;
		private const KingdomArtifactRecognitionKind Kind =
			KingdomArtifactRecognitionServiceTests.Kind;
		private const string City = KingdomArtifactRecognitionServiceTests.City;

		/// <summary>
		/// A write that lands between what the founder was shown and what the founder confirmed
		/// makes the confirmation fail, byte for byte, and a fresh disclosure then succeeds.
		/// </summary>
		[Test]
		public void ConcurrentSectionWriteBetweenDisclosureAndConfirmationChangesNothing()
		{
			KingdomCivicMemoryAuthority authority = Seeded();
			byte[] witnessBefore = WitnessBytes(authority);

			// The disclosure lease: everything the founder is shown is decided about this payload.
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease disclosure,
				out KingdomCivicArtifactsEnvelope held, out string failure), failure);
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryPlan(held, City,
				KingdomArtifactRecognitionServiceTests.Artifact("artifact-shown"), Kind, 0, null,
				40L, out KingdomArtifactRecognitionPlan plan, out failure), failure);

			// Somebody else writes the same section while the founder is still reading.
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease other, out _, out failure), failure);
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, other,
				Realm, KingdomArtifactRecognitionServiceTests.Artifact("artifact-elsewhere"), Kind,
				0, null, 41L, out _, out KingdomArtifactRecognitionOutcome interloper,
				out failure), failure);
			Assert.AreEqual(KingdomArtifactRecognitionOutcome.Recorded, interloper);
			byte[] afterInterloper = authority.Encode();

			// The founder now says yes to what they were shown. It is stale, and it is refused.
			Assert.IsFalse(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, disclosure,
				Realm, KingdomArtifactRecognitionServiceTests.Artifact("artifact-shown"), Kind, 0,
				null, 42L, out KingdomArtifactRecognitionReceipt refused, out _, out failure));
			StringAssert.Contains("revision", failure);
			Assert.IsNull(refused);
			CollectionAssert.AreEqual(afterInterloper, authority.Encode());

			// A fresh disclosure sees the moved save and may then record the same recognition.
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease again, out KingdomCivicArtifactsEnvelope moved,
				out failure), failure);
			Assert.AreEqual(1, moved.Recognitions.Rows.Count,
				"only the interloper's row is kept; the stale attempt wrote nothing");
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, again,
				Realm, KingdomArtifactRecognitionServiceTests.Artifact("artifact-shown"), Kind, 0,
				null, 43L, out KingdomArtifactRecognitionReceipt kept,
				out KingdomArtifactRecognitionOutcome outcome, out failure), failure);
			Assert.AreEqual(KingdomArtifactRecognitionOutcome.Recorded, outcome);
			Assert.AreEqual(plan.RecognitionId, kept.RecognitionId,
				"the same object and form must still name the same row");
			CollectionAssert.AreEqual(witnessBefore, WitnessBytes(authority),
				"the sibling witness book in section one must survive every step");
		}

		/// <summary>
		/// The same unchanged object, read again an hour later, is still the same recognition.
		/// <para>
		/// The second reading has a different observation tick and therefore a different digest and
		/// a different derived id, but nothing about the thing has changed. Matching on the digest
		/// would call that a rewrite and refuse it, which would make the free retry impossible for
		/// anyone who did not confirm within the same tick.
		/// </para>
		/// </summary>
		[Test]
		public void ALaterReadingOfTheUnchangedObjectIsStillTheFreeRetry()
		{
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Recorded(out string id);
			long revision = authority.Revision;
			byte[] before = authority.Encode();
			KingdomArtifactSnapshot later = KingdomArtifactRecognitionServiceTests.Artifact(
				"artifact-1", "folded fullerite sword", "taf:owner:player",
				"taf:zone:JoppaWorld.11.22.1.1.10:4:5", 9000L);
			Assert.AreNotEqual(id, "taf:artifact-recognition:" + later.SnapshotDigest,
				"the later reading really does digest differently");
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease lease, out KingdomCivicArtifactsEnvelope held,
				out string failure), failure);
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryPlan(held, City, later, Kind, 7, "Eshkind",
				9000L, out KingdomArtifactRecognitionPlan plan, out failure), failure);
			Assert.IsTrue(plan.AlreadyKept, "the disclosure must say this is already written down");
			StringAssert.Contains("already written down", plan.Disclosure());
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, lease,
				Realm, later, Kind, 7, "Eshkind", 9000L,
				out KingdomArtifactRecognitionReceipt receipt,
				out KingdomArtifactRecognitionOutcome outcome, out failure), failure);
			Assert.AreEqual(KingdomArtifactRecognitionOutcome.AlreadyKept, outcome);
			Assert.AreEqual(id, receipt.RecognitionId, "the row kept is the row already kept");
			Assert.AreEqual(40L, receipt.Source.ObservedTick, "its own tick is not rewritten");
			Assert.AreEqual(revision, authority.Revision);
			CollectionAssert.AreEqual(before, authority.Encode());
		}

		/// <summary>
		/// The free retry survives a full register. Capacity refuses new rows, not repeats, so the
		/// eighth-row realm can still confirm something it already holds.
		/// </summary>
		[Test]
		public void ALaterReadingRetryIsStillFreeWithAllEightRowsKept()
		{
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Authority();
			for (int i = 0; i < KingdomArtifactRecognitionRules.MaxRows; i++)
			{
				Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
					out KingdomCivicMemorySectionLease fill, out _, out string step), step);
				Assert.IsTrue(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, fill,
					Realm, KingdomArtifactRecognitionServiceTests.Artifact("artifact-" + i), Kind,
					0, null, 40L, out _, out _, out step), step);
			}
			long revision = authority.Revision;
			byte[] before = authority.Encode();
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease lease, out KingdomCivicArtifactsEnvelope held,
				out string failure), failure);
			Assert.AreEqual(KingdomArtifactRecognitionRules.MaxRows,
				held.Recognitions.Rows.Count, "the register really is full");
			KingdomArtifactSnapshot later = KingdomArtifactRecognitionServiceTests.Artifact(
				"artifact-3", "folded fullerite sword", "taf:owner:player",
				"taf:zone:JoppaWorld.11.22.1.1.10:4:5", 9000L);
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryPlan(held, City, later, Kind, 0, null,
				9000L, out KingdomArtifactRecognitionPlan plan, out failure), failure);
			Assert.IsTrue(plan.AlreadyKept);
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, lease,
				Realm, later, Kind, 0, null, 9000L, out _,
				out KingdomArtifactRecognitionOutcome outcome, out failure), failure);
			Assert.AreEqual(KingdomArtifactRecognitionOutcome.AlreadyKept, outcome);
			Assert.AreEqual(revision, authority.Revision);
			CollectionAssert.AreEqual(before, authority.Encode());

			// A genuinely new subject is still refused, and still evicts nothing.
			Assert.IsFalse(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, lease,
				Realm, KingdomArtifactRecognitionServiceTests.Artifact("artifact-new"), Kind, 0,
				null, 9000L, out _, out _, out failure));
			StringAssert.Contains("capacity", failure);
			CollectionAssert.AreEqual(before, authority.Encode());
		}

		/// <summary>
		/// A reading from before the one already recorded is not a retry. Only a later look at the
		/// same facts is tolerated; anything else is a claim about a state the row already passed.
		/// </summary>
		[Test]
		public void AnEarlierReadingIsNotARetryAndChangesNothing()
		{
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Recorded(out _);
			byte[] before = authority.Encode();
			KingdomArtifactSnapshot earlier = KingdomArtifactRecognitionServiceTests.Artifact(
				"artifact-1", "folded fullerite sword", "taf:owner:player",
				"taf:zone:JoppaWorld.11.22.1.1.10:4:5", 39L);
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease lease, out _, out string failure), failure);
			Assert.IsFalse(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, lease,
				Realm, earlier, Kind, 7, "Eshkind", 39L,
				out KingdomArtifactRecognitionReceipt refused, out _, out failure));
			StringAssert.Contains("already recognized", failure);
			Assert.IsNull(refused);
			CollectionAssert.AreEqual(before, authority.Encode());
		}

		/// <summary>
		/// A recognition made on a non-seat settlement's ground names THAT settlement, in the
		/// disclosure and in the receipt line the founder is shown afterwards.
		/// <para>
		/// The seat is a cursor that moves with the founder, so naming it would mean a realm's
		/// second city had its own recognitions attributed to its first. The name carried here is
		/// the one the ground's own settlement answers to.
		/// </para>
		/// </summary>
		[Test]
		public void ANonSeatSettlementIsNamedInItsOwnDisclosureAndReceipt()
		{
			const string seat = "Ezra Wells";
			const string nonSeat = City;
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Authority();
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease lease, out KingdomCivicArtifactsEnvelope held,
				out string failure), failure);
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryPlan(held, nonSeat,
				KingdomArtifactRecognitionServiceTests.Artifact(), Kind, 0, null, 40L,
				out KingdomArtifactRecognitionPlan plan, out failure), failure);
			Assert.AreEqual(nonSeat, plan.SettlementName);
			string disclosure = plan.Disclosure();
			StringAssert.Contains("What " + nonSeat + " would write down", disclosure);
			StringAssert.Contains("Kept by: " + nonSeat, disclosure);
			StringAssert.DoesNotContain(seat, disclosure,
				"the seat's name must never reach a non-seat city's disclosure");
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, lease,
				Realm, KingdomArtifactRecognitionServiceTests.Artifact(), Kind, 0, null, 40L,
				out KingdomArtifactRecognitionReceipt receipt, out _, out failure), failure);
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadBackRow(authority, Realm,
				receipt.RecognitionId, out KingdomArtifactRecognitionReceipt kept, out failure),
				failure);
			// The durable row is settlement-neutral by design; it is placed by its own location,
			// which is what the Reliquary reads back. The name belongs to the presentation.
			StringAssert.DoesNotContain(seat, kept.Text);
			StringAssert.Contains("taf:zone:", kept.Source.LocationId);
		}

		/// <summary>
		/// A city the realm cannot name is a recognition the realm cannot place, so nothing is
		/// disclosed and nothing is offered.
		/// </summary>
		[TestCase(null, TestName = "no name at all")]
		[TestCase("", TestName = "an empty name")]
		[TestCase("   ", TestName = "whitespace only")]
		public void ARecognitionTheRealmCannotPlaceIsNeverDisclosed(string Unresolved)
		{
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Authority();
			byte[] before = authority.Encode();
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadBack(authority, Realm,
				out KingdomCivicArtifactsEnvelope held, out string failure), failure);
			Assert.IsFalse(KingdomArtifactRecognitionCommit.TryPlan(held, Unresolved,
				KingdomArtifactRecognitionServiceTests.Artifact(), Kind, 0, null, 40L,
				out KingdomArtifactRecognitionPlan plan, out failure));
			Assert.IsNull(plan);
			StringAssert.Contains("no name of its own", failure);
			CollectionAssert.AreEqual(before, authority.Encode());
		}

		/// <summary>Two settlers may share a name; the roll identity is what is proved.</summary>
		[Test]
		public void DuplicateResidentNamesAreSeparatedByRollIdentity()
		{
			List<int> ids = new List<int> { 4, 9 };
			List<string> names = new List<string> { "Eshkind", "Eshkind" };
			Assert.IsTrue(KingdomArtifactRecognitionAttribution.TryProveResident(4, "Eshkind",
				ids, names, out string failure), failure);
			Assert.IsTrue(KingdomArtifactRecognitionAttribution.TryProveResident(9, "Eshkind",
				ids, names, out failure), failure);
			Assert.IsFalse(KingdomArtifactRecognitionAttribution.TryProveResident(5, "Eshkind",
				ids, names, out failure));
			StringAssert.Contains("no longer on this settlement's roll", failure);
		}

		/// <summary>Two rows claiming one identity cannot be told apart, so neither speaks.</summary>
		[Test]
		public void DuplicateRollIdentityRefusesAttribution()
		{
			Assert.IsFalse(KingdomArtifactRecognitionAttribution.TryProveResident(4, "Eshkind",
				new List<int> { 4, 4 }, new List<string> { "Eshkind", "Eshkind" },
				out string failure));
			StringAssert.Contains("two settlers", failure);
		}

		/// <summary>A settler who left, or who is now called something else, cannot be quoted.</summary>
		[TestCase(4, "Eshkind", 0, TestName = "departed settler")]
		[TestCase(4, "Tzimtzlum", 1, TestName = "renamed settler")]
		public void DepartedOrRenamedSettlerRefusesAttribution(int ResidentId, string Name,
			int Shape)
		{
			List<int> ids = Shape == 0 ? new List<int> { 9 } : new List<int> { 4 };
			List<string> names = Shape == 0
				? new List<string> { "Tzimtzlum" } : new List<string> { "Eshkind" };
			Assert.IsFalse(KingdomArtifactRecognitionAttribution.TryProveResident(ResidentId, Name,
				ids, names, out string failure));
			Assert.IsNotNull(failure);
		}

		/// <summary>Naming nobody is a complete answer, and naming nobody loudly is not.</summary>
		[Test]
		public void UnattributedRecognitionIsLawfulAndAHalfNamedOneIsNot()
		{
			Assert.IsTrue(KingdomArtifactRecognitionAttribution.TryProveResident(0, null,
				new List<int>(), new List<string>(), out string failure), failure);
			Assert.IsFalse(KingdomArtifactRecognitionAttribution.TryProveResident(0, "Eshkind",
				new List<int>(), new List<string>(), out failure));
			Assert.IsFalse(KingdomArtifactRecognitionAttribution.TryProveResident(4, null,
				new List<int> { 4 }, new List<string> { "Eshkind" }, out failure));
			Assert.IsFalse(KingdomArtifactRecognitionAttribution.TryProveResident(-1, null,
				new List<int>(), new List<string>(), out failure));
		}

		/// <summary>
		/// Designated realm property is not a prerequisite and never becomes one: an object with no
		/// recorded owner at all is recognizable, and recognition adds no owner to it.
		/// </summary>
		[Test]
		public void ObjectWithNoRecordedOwnerIsRecognizableAndGainsNone()
		{
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Authority();
			KingdomArtifactSnapshot ownerless = KingdomArtifactRecognitionServiceTests.Artifact(
				"artifact-ownerless", "a plain stone", null);
			Assert.IsNull(ownerless.OwnerId);
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease lease, out _, out string failure), failure);
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, lease,
				Realm, ownerless, Kind, 0, null, 40L,
				out KingdomArtifactRecognitionReceipt receipt, out _, out failure), failure);
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadBackRow(authority, Realm,
				receipt.RecognitionId, out KingdomArtifactRecognitionReceipt kept, out failure),
				failure);
			Assert.IsNull(kept.Source.OwnerId, "recognition must not invent an owner");
			Assert.AreEqual(0, kept.CommerceValue);
			Assert.IsFalse(kept.CustodyClaimed);
		}

		/// <summary>
		/// A recognition whose durable text will not fit its own wire is refused while planning,
		/// before the founder has agreed to anything. The stranded rite is the failure this cut
		/// exists to make impossible.
		/// </summary>
		[Test]
		public void OversizeAttributionIsRefusedWhilePlanningAndSpendsNothing()
		{
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Authority();
			byte[] before = authority.Encode();
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadBack(authority, Realm,
				out KingdomCivicArtifactsEnvelope held, out string failure), failure);
			string wide = new string('e', KingdomArtifactRecognitionRules.MaxTextBytes);
			KingdomArtifactSnapshot broad = KingdomArtifactRecognitionServiceTests.Artifact(
				"artifact-wide", new string('o', KingdomArtifactRecognitionRules.MaxTextBytes),
				"taf:owner:player", "taf:zone:JoppaWorld.11.22.1.1.10:4:5", 40L, "taf:deed:reef",
				new string('d', KingdomArtifactRecognitionRules.MaxTextBytes));
			Assert.IsFalse(KingdomArtifactRecognitionCommit.TryPlan(held, City, broad, Kind, 3, wide,
				40L, out KingdomArtifactRecognitionPlan plan, out failure));
			Assert.IsNull(plan);
			Assert.IsNotNull(failure);
			CollectionAssert.AreEqual(before, authority.Encode());
		}

		/// <summary>The register names every retained row, and says so plainly when there are none.</summary>
		[Test]
		public void RegisterNamesEveryRetainedRowAndStatesAbsenceOutright()
		{
			KingdomArtifactRecognitionBook empty = new KingdomArtifactRecognitionBook();
			StringAssert.Contains("recognized nothing yet",
				KingdomArtifactRecognitionRegister.Register(empty));
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Recorded(out _);
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadBack(authority, Realm,
				out KingdomCivicArtifactsEnvelope held, out string failure), failure);
			string register = KingdomArtifactRecognitionRegister.Register(held.Recognitions);
			StringAssert.Contains("Kept recognitions: 1 of 8", register);
			StringAssert.Contains("taf:object:artifact-1", register);
			StringAssert.Contains("no custody claimed", register);
			StringAssert.Contains(held.Recognitions.Rows[0].Text, register);
			StringAssert.DoesNotContain("recognized nothing yet", register);
		}

		/// <summary>
		/// The new Charter verb creates work, so it must stay off the paused-surface whitelist.
		/// Adding it there would let a paused realm record a recognition.
		/// </summary>
		[Test]
		public void RecognizeArtifactIsNeverAvailableWhileSimulationIsPaused()
		{
			Assert.IsFalse(KingdomCharterMenuRules.AvailableWhileSimulationPaused(
				KingdomCharterAction.RecognizeArtifact));
		}

		/// <summary>An authority already carrying a witness work, so siblings can be watched.</summary>
		private static KingdomCivicMemoryAuthority Seeded()
		{
			KingdomWitnessWorkSource source = new KingdomWitnessWorkSource
			{
				EventId = "taf:event:closed:1",
				SettlementId = "taf:settlement:seat",
				EventKind = KingdomWitnessWorkRules.RaisingAdapterKind,
				EventText = "the west cistern was sealed",
				ClosedTick = 10L,
				MakerResidentId = 7,
				MakerName = "Eshkind"
			};
			source.SnapshotDigest = KingdomWitnessWorkRules.SnapshotDigest(source);
			KingdomCivicArtifactsEnvelope envelope = new KingdomCivicArtifactsEnvelope
			{
				RealmId = Realm,
				IdentityBound = true
			};
			Assert.IsTrue(KingdomWitnessWorkRules.TryCapture(envelope.WitnessWorks, 0L, source,
				out _, out string failure), failure);
			Assert.IsTrue(KingdomCivicArtifactsStore.TryWrite(envelope, out byte[] payload,
				out failure), failure);
			KingdomCivicMemoryAuthority authority = new KingdomCivicMemoryAuthority(
				KingdomArtifactRecognitionServiceTests.Families());
			authority.AdoptSaved(KingdomCivicMemoryCodec.Encode(KingdomCivicMemoryState.Of(
				new List<KingdomCivicMemorySection>
				{
					new KingdomCivicMemorySection(
						KingdomCivicMemoryLimits.SectionCivicArtifacts, payload)
				}, 0L)));
			Assert.IsFalse(authority.Quarantined, authority.ReadOnlyReason);
			return authority;
		}

		private static byte[] WitnessBytes(IKingdomCivicMemoryAuthority Authority)
		{
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadBack(Authority, Realm,
				out KingdomCivicArtifactsEnvelope held, out string failure), failure);
			return KingdomWitnessWorkCodec.Encode(held.WitnessWorks);
		}
	}
}
#endif
