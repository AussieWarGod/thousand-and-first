#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// D6 end to end against a real civic-memory authority: the exact path the Charter action
	/// takes, minus the popups it cannot run without a game.
	/// </summary>
	[TestFixture]
	public sealed class KingdomArtifactRecognitionServiceTests
	{
		internal const string Realm =
			"taf:realm:v1:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
		/// <summary>A non-seat settlement's own name, so nothing here can pass by naming the seat.</summary>
		internal const string City = "Second Cistern";

		internal const string OtherRealm =
			"taf:realm:v1:fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";

		/// <summary>
		/// The point of the lane. An empty save reaches civic memory, takes one row, and the row is
		/// found again by asking the save rather than by remembering what was offered to it.
		/// </summary>
		[Test]
		public void CharterSeamReachesCivicMemoryAndReadsTheRowBackFromTheSave()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			long before = authority.Revision;
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease lease,
				out KingdomCivicArtifactsEnvelope held, out string failure), failure);
			Assert.AreEqual(0, held.Recognitions.Rows.Count);
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryPlan(held, City, Artifact(), Kind, 7,
				"Eshkind", 40L, out KingdomArtifactRecognitionPlan plan, out failure), failure);
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, lease,
				Realm, Artifact(), Kind, 7, "Eshkind", 40L,
				out KingdomArtifactRecognitionReceipt receipt,
				out KingdomArtifactRecognitionOutcome outcome, out failure), failure);
			Assert.AreEqual(KingdomArtifactRecognitionOutcome.Recorded, outcome);
			Assert.AreEqual(before + 1L, authority.Revision);
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadBackRow(authority, Realm,
				receipt.RecognitionId, out KingdomArtifactRecognitionReceipt kept, out failure),
				failure);
			Assert.AreEqual(plan.Text, kept.Text);
			Assert.AreEqual(plan.RecognitionId, kept.RecognitionId);
			Assert.AreEqual(0, kept.CommerceValue);
			Assert.IsFalse(kept.CustodyClaimed);
			Assert.AreEqual(7, kept.AttributedResidentId);
		}

		/// <summary>
		/// One lease means one lease: the commit path opens section one exactly once and offers its
		/// payload back under that same lease object, not a fresh reading of the same bytes.
		/// </summary>
		[Test]
		public void CommitPathOpensTheSectionOnceAndCommitsUnderThatSameLease()
		{
			CountingAuthority counting = new CountingAuthority(Authority());
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(counting, Realm,
				out KingdomCivicMemorySectionLease lease, out KingdomCivicArtifactsEnvelope _,
				out string failure), failure);
			Assert.AreEqual(1, counting.Reads);
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryCommitPlanned(counting, lease,
				Realm, Artifact(), Kind, 0, null, 40L, out _, out _, out failure), failure);
			Assert.AreEqual(1, counting.Reads, "the commit path may not open the section again");
			Assert.AreEqual(1, counting.Commits);
			Assert.IsTrue(ReferenceEquals(lease, counting.LastLease),
				"the committed lease must be the very object the read produced");
		}

		/// <summary>An exact repeat is free: no row, no revision, and no byte moves.</summary>
		[Test]
		public void ExactRetryRecordsNothingAndSpendsNoRevision()
		{
			KingdomCivicMemoryAuthority authority = Recorded(out string id);
			long after = authority.Revision;
			byte[] before = authority.Encode();
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease lease, out _, out string failure), failure);
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, lease,
				Realm, Artifact(), Kind, 7, "Eshkind", 90L,
				out KingdomArtifactRecognitionReceipt receipt,
				out KingdomArtifactRecognitionOutcome outcome, out failure), failure);
			Assert.AreEqual(KingdomArtifactRecognitionOutcome.AlreadyKept, outcome);
			Assert.AreEqual(id, receipt.RecognitionId);
			Assert.AreEqual(after, authority.Revision);
			CollectionAssert.AreEqual(before, authority.Encode());
		}

		/// <summary>A retry cannot quietly re-attribute what the city already said.</summary>
		[Test]
		public void RetryUnderADifferentAttributionIsRefusedAndChangesNothing()
		{
			KingdomCivicMemoryAuthority authority = Recorded(out _);
			byte[] before = authority.Encode();
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease lease, out _, out string failure), failure);
			Assert.IsFalse(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, lease,
				Realm, Artifact(), Kind, 9, "Tzimtzlum", 90L, out KingdomArtifactRecognitionReceipt
				receipt, out _, out failure));
			StringAssert.Contains("already recognized", failure);
			Assert.IsNull(receipt);
			CollectionAssert.AreEqual(before, authority.Encode());
		}

		/// <summary>Capacity refuses. It never deletes evidence to make room.</summary>
		[Test]
		public void FullAuthorityRefusesTheNinthAndKeepsAllEightRows()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			for (int i = 0; i < KingdomArtifactRecognitionRules.MaxRows; i++)
			{
				Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
					out KingdomCivicMemorySectionLease lease, out _, out string step), step);
				Assert.IsTrue(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, lease,
					Realm, Artifact("artifact-" + i), Kind, 0, null, 40L, out _, out _, out step),
					step);
			}
			byte[] before = authority.Encode();
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease ninth,
				out KingdomCivicArtifactsEnvelope full, out string failure), failure);
			Assert.AreEqual(KingdomArtifactRecognitionRules.MaxRows, full.Recognitions.Rows.Count);
			Assert.IsFalse(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, ninth,
				Realm, Artifact("artifact-overflow"), Kind, 0, null, 40L, out _, out _,
				out failure));
			StringAssert.Contains("capacity", failure);
			CollectionAssert.AreEqual(before, authority.Encode());
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadBack(authority, Realm,
				out KingdomCivicArtifactsEnvelope kept, out failure), failure);
			Assert.AreEqual(KingdomArtifactRecognitionRules.MaxRows, kept.Recognitions.Rows.Count);
		}

		/// <summary>A lease that was overtaken writes nothing.</summary>
		[Test]
		public void StaleLeaseCommitsNothing()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease stale, out _, out string failure), failure);
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease fresh, out _, out failure), failure);
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, fresh,
				Realm, Artifact("artifact-first"), Kind, 0, null, 40L, out _, out _, out failure),
				failure);
			byte[] before = authority.Encode();
			Assert.IsFalse(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, stale,
				Realm, Artifact("artifact-second"), Kind, 0, null, 40L, out _, out _, out failure));
			StringAssert.Contains("revision", failure);
			CollectionAssert.AreEqual(before, authority.Encode());
		}

		/// <summary>Planning is a reading. It reaches nothing and spends nothing.</summary>
		[Test]
		public void PlanningLeavesTheSaveByteIdentical()
		{
			KingdomCivicMemoryAuthority authority = Authority();
			byte[] before = authority.Encode();
			long revision = authority.Revision;
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadBack(authority, Realm,
				out KingdomCivicArtifactsEnvelope held, out string failure), failure);
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryPlan(held, City, Artifact(), Kind, 0, null,
				40L, out KingdomArtifactRecognitionPlan plan, out failure), failure);
			StringAssert.Contains("Commerce value: 0", plan.Disclosure());
			StringAssert.Contains("Custody: none taken", plan.Disclosure());
			StringAssert.Contains(plan.Text, plan.Disclosure());
			Assert.AreEqual(0, plan.CommerceValue);
			Assert.AreEqual(revision, authority.Revision);
			CollectionAssert.AreEqual(before, authority.Encode());
			Assert.AreEqual(0, held.Recognitions.Rows.Count,
				"planning must not add a row to the authority it read");
		}

		internal const KingdomArtifactRecognitionKind Kind =
			KingdomArtifactRecognitionKind.Inscription;

		internal static KingdomArtifactSnapshot Artifact(string Id = "artifact-1",
			string Display = "folded fullerite sword", string Owner = "taf:owner:player",
			string Location = "taf:zone:JoppaWorld.11.22.1.1.10:4:5", long Tick = 40L,
			string DeedId = null, string DeedText = null)
		{
			KingdomArtifactSnapshot value = new KingdomArtifactSnapshot
			{
				ObjectId = "taf:object:" + Id,
				Blueprint = "Fullerite Long Sword",
				DisplayName = Display,
				OwnerId = Owner,
				LocationId = Location,
				DeedId = DeedId,
				DeedText = DeedText,
				ObservedTick = Tick
			};
			value.SnapshotDigest = KingdomArtifactRecognitionRules.SnapshotDigest(value);
			return value;
		}

		internal static KingdomCivicMemoryAuthority Authority()
		{
			KingdomCivicMemoryAuthority authority =
				new KingdomCivicMemoryAuthority(Families());
			authority.AdoptAbsent();
			return authority;
		}

		internal static KingdomCivicMemoryAuthority Recorded(out string RecognitionId)
		{
			KingdomCivicMemoryAuthority authority = Authority();
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease lease, out _, out string failure), failure);
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, lease,
				Realm, Artifact(), Kind, 7, "Eshkind", 40L,
				out KingdomArtifactRecognitionReceipt receipt, out _, out failure), failure);
			RecognitionId = receipt.RecognitionId;
			return authority;
		}

		/// <summary>
		/// Section one answered by its real codec and store, exactly as
		/// <c>KingdomCivicMemoryFamilyBindings</c> wires it in the game. That binding is engine-side
		/// and compiled out of the pure projects, so the wiring itself is pinned against the source
		/// by <c>KingdomCivicMemorySourceTests</c>; what runs here is the same pair of calls.
		/// </summary>
		internal static KingdomCivicMemoryFamilyTable Families()
		{
			KingdomCivicMemoryFamilyTable table = new KingdomCivicMemoryFamilyTable();
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++)
				table.Add(id, id == KingdomCivicMemoryLimits.SectionCivicArtifacts
					? (KingdomCivicMemoryFamilyReader)Artifacts
					: (KingdomCivicMemoryFamilyReader)Anything);
			return table;
		}

		private static KingdomCivicMemoryNested Artifacts(byte[] Payload, out string Fault)
		{
			try
			{
				KingdomCivicArtifactsEnvelope envelope =
					KingdomCivicArtifactsCodec.Decode(Payload);
				if (envelope == null)
				{
					Fault = "the civic artifacts codec returned nothing at all";
					return KingdomCivicMemoryNested.Malformed;
				}
				if (envelope.IsOpaqueFuture)
				{
					Fault = "";
					return KingdomCivicMemoryNested.Future;
				}
				if (!envelope.Quarantined && KingdomCivicArtifactsStore.TryValidateIdentity(
					envelope, out Fault))
				{
					Fault = "";
					return KingdomCivicMemoryNested.Current;
				}
				Fault = envelope.Fault ?? "the civic artifacts identity is invalid";
				return KingdomCivicMemoryNested.Malformed;
			}
			catch (Exception e) when (e is IOException || e is InvalidDataException
				|| e is ArgumentException || e is NotSupportedException)
			{
				Fault = e.Message;
				return KingdomCivicMemoryNested.Malformed;
			}
		}

		private static KingdomCivicMemoryNested Anything(byte[] Payload, out string Fault)
		{
			Fault = "";
			return KingdomCivicMemoryNested.Current;
		}

		/// <summary>Counts section reads and remembers which lease object reached the commit.</summary>
		internal sealed class CountingAuthority : IKingdomCivicMemoryAuthority
		{
			private readonly IKingdomCivicMemoryAuthority Inner;
			internal int Reads;
			internal int Commits;
			internal KingdomCivicMemorySectionLease LastLease;

			internal CountingAuthority(IKingdomCivicMemoryAuthority Inner) { this.Inner = Inner; }

			public long Revision { get { return Inner.Revision; } }
			public bool ReadOnly { get { return Inner.ReadOnly; } }
			public string ReadOnlyReason { get { return Inner.ReadOnlyReason; } }
			public KingdomCivicMemoryState Read() { return Inner.Read(); }

			public bool TryCommit(IList<KingdomCivicMemorySection> Candidate,
				long ExpectedRevision, out string Failure)
			{
				return Inner.TryCommit(Candidate, ExpectedRevision, out Failure);
			}

			public bool TryReadSection(int SectionId, out KingdomCivicMemorySectionLease Lease,
				out string Failure)
			{
				Reads++;
				return Inner.TryReadSection(SectionId, out Lease, out Failure);
			}

			public bool TryCommitSection(KingdomCivicMemorySectionLease Lease, byte[] Payload,
				out string Failure)
			{
				Commits++;
				LastLease = Lease;
				return Inner.TryCommitSection(Lease, Payload, out Failure);
			}
		}
	}
}
#endif
