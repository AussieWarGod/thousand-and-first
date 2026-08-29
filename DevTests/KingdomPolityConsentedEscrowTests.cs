#if TAF_TESTS
using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityConsentedEscrowTests
	{
		[Test]
		public void ExplicitEscrowCommitsConcludesAndRefundsExactStake()
		{
			KingdomPolityLedger ledger = Open();
			KingdomPolityConsentedEscrowRequest request = Request(ledger, 220L);
			KingdomPolityRelationBand relationBefore =
				KingdomPolityGapTestData.Relation(ledger).Band;
			long before = ledger.Revision;
			Assert.IsTrue(KingdomPolityConflictRules.TryPrepareConsentedEscrow(ledger,
				before, request, out string projectionId,
				out KingdomPolityPublicationResult result, out string failure), failure);
			Assert.AreEqual(before + 1L, ledger.Revision);
			Assert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			KingdomPolityProjectionReceipt projection =
				KingdomPolityGapTestData.Projection(ledger, projectionId);
			Assert.AreEqual(KingdomPolityProjectionKind.ConsentedEscrow, projection.Kind);
			Assert.AreEqual(KingdomPolityProjectionPhase.Prepared, projection.Phase);
			Assert.AreEqual(request.CollateralObjectId, projection.ObjectIds[0]);
			Assert.AreEqual(request.SnapshotDigest, projection.PriorDigest);
			Assert.AreEqual(KingdomPolityInterventionChoice.ConsentAbstractResolution,
				Clash(ledger).Intervention.Choice);

			byte[] prepared = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsTrue(KingdomPolityConflictRules.TryPrepareConsentedEscrow(ledger,
				before, request, out string retryId, out result, out failure), failure);
			Assert.AreEqual(projectionId, retryId);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			CollectionAssert.AreEqual(prepared, KingdomPolityCodec.EncodeEnvelope(ledger));

			Assert.IsTrue(KingdomPolityConflictRules.TryCreateEscrowCustodyProof(ledger,
				projectionId, 221L, out KingdomPolityEscrowCustodyProof custody,
				out failure), failure);
			Assert.IsTrue(KingdomPolityConflictRules.TryCommitConsentedEscrowCustody(ledger,
				ledger.Revision, custody, out result, out failure), failure);
			Assert.AreEqual(KingdomPolityProjectionPhase.Committed,
				KingdomPolityGapTestData.Projection(ledger, projectionId).Phase);
			Assert.IsTrue(KingdomPolityConflictRules.TryConcludeConsentedEscrow(ledger,
				ledger.Revision, projectionId, 222L, out result, out failure), failure);

			KingdomPolityIncidentRecord clash = Clash(ledger);
			Assert.AreEqual(KingdomPolityResolutionKind.ConsentedEscrow,
				clash.Conclusion.ResolutionKind);
			Assert.AreEqual(0, clash.Conclusion.ObservedFactIds.Count);
			Assert.AreEqual(0, clash.Conclusion.RelationDeltas.Count);
			Assert.AreEqual(1, clash.Conclusion.SystemicDeltas.Count);
			Assert.AreEqual(KingdomPolitySystemicDeltaKind.ReservedStake,
				clash.Conclusion.SystemicDeltas[0].Kind);
			Assert.AreEqual(-1, clash.Conclusion.SystemicDeltas[0].Amount);
			Assert.AreEqual(KingdomPolityAftermathKind.ConsentedResolution,
				clash.Aftermath.Kind);
			Assert.AreEqual(KingdomPolityRoutePhase.Blocked,
				KingdomPolityGapTestData.RouteRecord(ledger).Phase);
			Assert.AreEqual(relationBefore, KingdomPolityGapTestData.Relation(ledger).Band);

			Assert.IsTrue(KingdomPolityConflictRules.TryCreateEscrowRefundProof(ledger,
				projectionId, 223L, out KingdomPolityEscrowRefundProof refund,
				out failure), failure);
			Assert.IsTrue(KingdomPolityConflictRules.TryReleaseConsentedEscrow(ledger,
				ledger.Revision, refund, out result, out failure), failure);
			Assert.AreEqual(KingdomPolityProjectionPhase.Cleaned,
				KingdomPolityGapTestData.Projection(ledger, projectionId).Phase);
			Assert.AreEqual(KingdomPolityRoutePhase.AvailableToWitness,
				KingdomPolityGapTestData.RouteRecord(ledger).Phase);
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
			KingdomPolityLedger decoded = KingdomPolityCodec.DecodeEnvelope(
				KingdomPolityCodec.EncodeEnvelope(ledger));
			Assert.AreEqual(KingdomPolityProjectionKind.ConsentedEscrow,
				KingdomPolityGapTestData.Projection(decoded, projectionId).Kind);
			Assert.AreEqual(KingdomPolityResolutionKind.ConsentedEscrow,
				Clash(decoded).Conclusion.ResolutionKind);
		}

		[Test]
		public void PreparedEscrowCanCancelWithoutConclusionOrRouteLoss()
		{
			KingdomPolityLedger ledger = Open();
			KingdomPolityConsentedEscrowRequest request = Request(ledger, 220L);
			KingdomPolityRoutePhase routeBefore =
				KingdomPolityGapTestData.RouteRecord(ledger).Phase;
			Assert.IsTrue(KingdomPolityConflictRules.TryPrepareConsentedEscrow(ledger,
				ledger.Revision, request, out string projectionId, out _,
				out string failure), failure);
			Assert.IsTrue(KingdomPolityConflictRules.TryCreateEscrowRefundProof(ledger,
				projectionId, 221L, out KingdomPolityEscrowRefundProof refund,
				out failure), failure);
			Assert.IsTrue(KingdomPolityConflictRules.TryReleaseConsentedEscrow(ledger,
				ledger.Revision, refund, out KingdomPolityPublicationResult result,
				out failure), failure);
			Assert.AreEqual(KingdomPolityProjectionPhase.Cancelled,
				KingdomPolityGapTestData.Projection(ledger, projectionId).Phase);
			Assert.IsNull(Clash(ledger).Conclusion);
			Assert.IsNull(Clash(ledger).Intervention);
			Assert.AreEqual(routeBefore, KingdomPolityGapTestData.RouteRecord(ledger).Phase);
			Assert.Throws<InvalidDataException>(() =>
				KingdomPolityCodec.EncodeEnvelopeV4Fixture(ledger));
			byte[] cancelled = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsTrue(KingdomPolityConflictRules.TryReleaseConsentedEscrow(ledger,
				ledger.Revision - 1L, refund, out result, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			CollectionAssert.AreEqual(cancelled, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void ForgedConsentCustodyAndCasLeaveAuthorityByteIdentical()
		{
			KingdomPolityLedger ledger = Open();
			KingdomPolityConsentedEscrowRequest request = Request(ledger, 220L);
			byte[] open = KingdomPolityCodec.EncodeEnvelope(ledger);
			request.ConsentFactId = "taf:fact:inferred:consent";
			Assert.IsFalse(KingdomPolityConflictRules.TryPrepareConsentedEscrow(ledger,
				ledger.Revision, request, out string _, out _, out string failure));
			CollectionAssert.AreEqual(open, KingdomPolityCodec.EncodeEnvelope(ledger));
			request = Request(ledger, 220L);
			Assert.IsFalse(KingdomPolityConflictRules.TryPrepareConsentedEscrow(ledger,
				ledger.Revision - 1L, request, out _, out _, out failure));
			CollectionAssert.AreEqual(open, KingdomPolityCodec.EncodeEnvelope(ledger));
			Assert.IsTrue(KingdomPolityConflictRules.TryPrepareConsentedEscrow(ledger,
				ledger.Revision, request, out string projectionId, out _, out failure), failure);
			byte[] prepared = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomPolityConflictRules.TryConcludeConsentedEscrow(ledger,
				ledger.Revision, projectionId, 221L, out _, out failure));
			Assert.IsTrue(KingdomPolityConflictRules.TryCreateEscrowCustodyProof(ledger,
				projectionId, 221L, out KingdomPolityEscrowCustodyProof forged,
				out failure), failure);
			forged.CollateralObjectId = "foreign-collateral";
			Assert.IsFalse(KingdomPolityConflictRules.TryCommitConsentedEscrowCustody(ledger,
				ledger.Revision, forged, out _, out failure));
			CollectionAssert.AreEqual(prepared, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void OldWireGoldenKeepsValuesAndFutureProjectionKindFailsClosed()
		{
			Assert.AreEqual(1, (byte)KingdomPolityProjectionKind.Faction);
			Assert.AreEqual(7, (byte)KingdomPolityProjectionKind.FactionTombstone);
			Assert.AreEqual(8, (byte)KingdomPolityProjectionKind.ConsentedEscrow);
			Assert.AreEqual(1, (byte)KingdomPolityResolutionKind.LiveScene);
			Assert.AreEqual(2, (byte)KingdomPolityResolutionKind.ConsentedEscrow);
			Assert.AreEqual(4, (byte)KingdomPolityInterventionChoice.Observe);
			Assert.AreEqual(5,
				(byte)KingdomPolityInterventionChoice.ConsentAbstractResolution);
			Assert.AreEqual(2, (byte)KingdomPolityAftermathKind.WitnessedWithdrawal);
			Assert.AreEqual(3, (byte)KingdomPolityAftermathKind.ConsentedResolution);
			Assert.AreEqual(7, (byte)KingdomPolityGrievanceCause.RefusedTerms);
			Assert.AreEqual(8, (byte)KingdomPolityGrievanceCause.ResourceRefusal);

			byte[] old = KingdomPolityCodec.EncodeEnvelopeV4Fixture(
				KingdomPolityTestData.Full());
			Assert.AreEqual(KingdomPolityCodec.PriorWireVersion,
				BitConverter.ToInt32(old, 4));
			Assert.AreEqual((byte)KingdomPolityProjectionKind.Faction,
				ByteAfterUniqueText(old, "taf:projection:faction-rival"));
			Assert.AreEqual((byte)KingdomPolityProjectionKind.IncidentView,
				ByteAfterUniqueText(old, "taf:projection:incident-view"));
			KingdomPolityLedger migrated = KingdomPolityCodec.DecodeEnvelope(old);
			Assert.IsTrue(KingdomPolityRules.TryValidate(migrated,
				out string failure), failure);

			KingdomPolityLedger ledger = Open();
			KingdomPolityConsentedEscrowRequest request = Request(ledger, 220L);
			Assert.IsTrue(KingdomPolityConflictRules.TryPrepareConsentedEscrow(ledger,
				ledger.Revision, request, out string projectionId, out _, out failure), failure);
			byte[] current = KingdomPolityCodec.EncodeEnvelope(ledger);
			int kind = IndexAfterUniqueText(current, projectionId);
			Assert.AreEqual((byte)KingdomPolityProjectionKind.ConsentedEscrow, current[kind]);
			current[kind] = 9;
			KingdomPolityLedger unknown = KingdomPolityCodec.DecodeEnvelope(current);
			Assert.AreEqual(KingdomPolitySchemaState.Quarantined, unknown.SchemaState);
			Assert.IsFalse(KingdomPolityRules.Usable(unknown));
			Assert.AreEqual(9, (byte)KingdomPolityGapTestData.Projection(unknown,
				projectionId).Kind, "reader must preserve, then quarantine, the unknown value");
			StringAssert.Contains("projection", unknown.SchemaFault.ToLowerInvariant());
		}

		private static KingdomPolityLedger Open()
		{
			return KingdomPolityGapTestData.OpenClash(KingdomPolityRelationBand.Contact);
		}

		private static KingdomPolityIncidentRecord Clash(KingdomPolityLedger L)
		{
			return KingdomPolityGapTestData.Incident(L, KingdomPolityGapTestData.ClashPlan);
		}

		private static KingdomPolityConsentedEscrowRequest Request(
			KingdomPolityLedger L, long Tick)
		{
			return KingdomPolityGapTestData.EscrowRequest(L, "ground-collateral-1",
				KingdomPolityTestData.DigestA, Tick);
		}

		private static byte ByteAfterUniqueText(byte[] Bytes, string Text)
		{
			return Bytes[IndexAfterUniqueText(Bytes, Text)];
		}

		private static int IndexAfterUniqueText(byte[] Bytes, string Text)
		{
			byte[] needle = Encoding.UTF8.GetBytes(Text); int found = -1;
			for (int i = 0; i <= Bytes.Length - needle.Length; i++)
			{
				int j = 0; while (j < needle.Length && Bytes[i + j] == needle[j]) j++;
				if (j != needle.Length) continue;
				if (found >= 0) throw new InvalidDataException("golden text is not unique");
				found = i + needle.Length;
			}
			if (found < 0 || found >= Bytes.Length)
				throw new InvalidDataException("golden text is absent");
			return found;
		}
	}
}
#endif
