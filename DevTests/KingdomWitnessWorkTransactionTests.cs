#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomWitnessWorkTransactionTests
	{
		private const string Realm = KingdomArtifactRecognitionServiceTests.Realm;

		[Test]
		public void ClosedCaptureAndCommissionPreserveRecognitionSibling()
		{
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Recorded(out string recognitionId);
			byte[] recognition = Recognition(authority);
			ClassicAssert.IsTrue(KingdomWitnessWorkCommit.TryCaptureClosed(authority, Realm, Source(),
				out bool captured, out KingdomWitnessWorkReceipt row, out string failure), failure);
			ClassicAssert.IsTrue(captured);
			CollectionAssert.AreEqual(recognition, Recognition(authority));
			ClassicAssert.IsTrue(KingdomWitnessWorkLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease lease,
				out KingdomCivicArtifactsEnvelope held, out failure), failure);
			ClassicAssert.IsTrue(KingdomWitnessWorkCommit.TryPlan(held, row.WorkId,
				"taf:object:surface", "taf:zone:seat", "taf:construction:surface",
				4, 5, 20L, out KingdomWitnessWorkPlan plan, out failure), failure);
			ClassicAssert.IsTrue(KingdomWitnessWorkCommit.TryPreparePlanned(authority, lease, Realm,
				plan, out KingdomWitnessWorkReceipt prepared, out bool recorded, out failure), failure);
			ClassicAssert.IsTrue(recorded);
			ClassicAssert.IsTrue(KingdomWitnessWorkCommit.TryCommitCarrier(authority, Realm,
				prepared.WorkId, prepared.CarrierReceiptId, 21L, out failure), failure);
			ClassicAssert.IsTrue(KingdomWitnessWorkLease.TryReadBackRow(authority, Realm,
				prepared.WorkId, out KingdomWitnessWorkReceipt kept, out failure), failure);
			ClassicAssert.AreEqual(KingdomWitnessWorkPhase.Projected, kept.Phase);
			CollectionAssert.AreEqual(recognition, Recognition(authority));
			ClassicAssert.IsTrue(KingdomArtifactRecognitionLease.TryReadBackRow(authority, Realm,
				recognitionId, out _, out failure), failure);
		}

		[Test]
		public void CancelAndStaleLeaseLeaveAuthorityByteIdentical()
		{
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Authority();
			ClassicAssert.IsTrue(KingdomWitnessWorkCommit.TryCaptureClosed(authority, Realm, Source(),
				out _, out KingdomWitnessWorkReceipt row, out string failure), failure);
			ClassicAssert.IsTrue(KingdomWitnessWorkLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease stale,
				out KingdomCivicArtifactsEnvelope held, out failure), failure);
			byte[] beforePlan = authority.Encode();
			ClassicAssert.IsTrue(KingdomWitnessWorkCommit.TryPlan(held, row.WorkId,
				"taf:object:surface", "taf:zone:seat", "taf:construction:surface",
				4, 5, 20L, out KingdomWitnessWorkPlan plan, out failure), failure);
			CollectionAssert.AreEqual(beforePlan, authority.Encode(), "planning/cancel is read-only");
			ClassicAssert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease moved, out _, out failure), failure);
			KingdomArtifactSnapshot race =
				KingdomArtifactRecognitionServiceTests.Artifact("race", Tick: 22L);
			ClassicAssert.IsTrue(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, moved,
				Realm, race, KingdomArtifactRecognitionServiceTests.Kind,
				7, "Eshkind", 22L, out _, out _,
				out failure), failure);
			byte[] afterRace = authority.Encode();
			ClassicAssert.IsFalse(KingdomWitnessWorkCommit.TryDeclinePlanned(authority, stale,
				Realm, row.WorkId, 23L, out _, out failure));
			StringAssert.Contains("revision", failure);
			CollectionAssert.AreEqual(afterRace, authority.Encode());
			ClassicAssert.IsFalse(KingdomWitnessWorkCommit.TryPreparePlanned(authority, stale, Realm,
				plan, out _, out _, out failure));
			StringAssert.Contains("revision", failure);
			CollectionAssert.AreEqual(afterRace, authority.Encode());
		}

		[Test]
		public void ExactCaptureRetryAndTerminalRecoveryDoNotRemint()
		{
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Authority();
			ClassicAssert.IsTrue(KingdomWitnessWorkCommit.TryCaptureClosed(authority, Realm, Source(),
				out bool first, out KingdomWitnessWorkReceipt row, out string failure), failure);
			long revision = authority.Revision;
			ClassicAssert.IsTrue(KingdomWitnessWorkCommit.TryCaptureClosed(authority, Realm, Source(),
				out bool retry, out _, out failure), failure);
			ClassicAssert.IsTrue(first); ClassicAssert.IsFalse(retry); ClassicAssert.AreEqual(revision, authority.Revision);
			ClassicAssert.IsTrue(KingdomWitnessWorkLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease lease,
				out KingdomCivicArtifactsEnvelope held, out failure), failure);
			ClassicAssert.IsTrue(KingdomWitnessWorkCommit.TryPlan(held, row.WorkId,
				"taf:object:surface", "taf:zone:seat", "taf:construction:surface",
				4, 5, 20L, out KingdomWitnessWorkPlan plan, out failure), failure);
			ClassicAssert.IsTrue(KingdomWitnessWorkCommit.TryPreparePlanned(authority, lease, Realm,
				plan, out _, out _, out failure), failure);
			ClassicAssert.IsTrue(KingdomWitnessWorkCommit.TryReconcile(authority, Realm, row.WorkId,
				false, false, 21L, out failure), failure);
			ClassicAssert.IsTrue(KingdomWitnessWorkLease.TryReadBackRow(authority, Realm, row.WorkId,
				out KingdomWitnessWorkReceipt lost, out failure), failure);
			ClassicAssert.AreEqual(KingdomWitnessWorkPhase.Lost, lost.Phase);
			long terminal = authority.Revision;
			ClassicAssert.IsTrue(KingdomWitnessWorkCommit.TryReconcile(authority, Realm, row.WorkId,
				false, false, 22L, out failure), failure);
			ClassicAssert.AreEqual(terminal, authority.Revision);
		}

		[Test]
		public void QuietDeclineIsTerminalAndPreservesRecognitionSibling()
		{
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Recorded(out string recognitionId);
			byte[] recognition = Recognition(authority);
			ClassicAssert.IsTrue(KingdomWitnessWorkCommit.TryCaptureClosed(authority, Realm, Source(),
				out _, out KingdomWitnessWorkReceipt row, out string failure), failure);
			ClassicAssert.IsTrue(KingdomWitnessWorkLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease disclosure, out _, out failure), failure);
			ClassicAssert.IsTrue(KingdomWitnessWorkCommit.TryDeclinePlanned(authority, disclosure,
				Realm, row.WorkId, 20L, out bool recorded, out failure), failure);
			ClassicAssert.IsTrue(recorded);
			ClassicAssert.IsTrue(KingdomWitnessWorkLease.TryReadBackRow(authority, Realm,
				row.WorkId, out KingdomWitnessWorkReceipt declined, out failure), failure);
			ClassicAssert.AreEqual(KingdomWitnessWorkPhase.Declined, declined.Phase);
			ClassicAssert.IsNull(declined.CarrierReceiptId);
			long terminal = authority.Revision;
			ClassicAssert.IsTrue(KingdomWitnessWorkCommit.TryDecline(authority, Realm,
				row.WorkId, 21L, out failure), failure);
			ClassicAssert.AreEqual(terminal, authority.Revision);
			CollectionAssert.AreEqual(recognition, Recognition(authority));
			ClassicAssert.IsTrue(KingdomArtifactRecognitionLease.TryReadBackRow(authority, Realm,
				recognitionId, out _, out failure), failure);
		}

		private static KingdomWitnessWorkSource Source()
		{
			KingdomWitnessWorkSource source = new KingdomWitnessWorkSource
			{
				EventId = "taf:happening:seat:4:10:7:0:0",
				SettlementId = "taf:settlement:seat",
				EventKind = KingdomWitnessWorkRules.RaisingAdapterKind,
				EventText = "the west cistern was raised", ClosedTick = 10L,
				MakerResidentId = 7, MakerName = "Eshkind"
			};
			source.SnapshotDigest = KingdomWitnessWorkRules.SnapshotDigest(source); return source;
		}

		private static byte[] Recognition(IKingdomCivicMemoryAuthority Authority)
		{
			ClassicAssert.IsTrue(KingdomWitnessWorkLease.TryReadAuthority(Authority, Realm,
				out _, out KingdomCivicArtifactsEnvelope held, out string failure), failure);
			return KingdomArtifactRecognitionCodec.Encode(held.Recognitions);
		}
	}
}
#endif
