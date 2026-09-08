#if TAF_TESTS
using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.IsTrue(KingdomPolityConflictRules.TryPrepareConsentedEscrow(ledger,
				before, request, out string projectionId,
				out KingdomPolityPublicationResult result, out string failure), failure);
			ClassicAssert.AreEqual(before + 1L, ledger.Revision);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			KingdomPolityProjectionReceipt projection =
				KingdomPolityGapTestData.Projection(ledger, projectionId);
			ClassicAssert.AreEqual(KingdomPolityProjectionKind.ConsentedEscrow, projection.Kind);
			ClassicAssert.AreEqual(KingdomPolityProjectionPhase.Prepared, projection.Phase);
			ClassicAssert.AreEqual(request.CollateralObjectId, projection.ObjectIds[0]);
			ClassicAssert.AreEqual(request.SnapshotDigest, projection.PriorDigest);
			ClassicAssert.AreEqual(KingdomPolityInterventionChoice.ConsentAbstractResolution,
				Clash(ledger).Intervention.Choice);

			byte[] prepared = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsTrue(KingdomPolityConflictRules.TryPrepareConsentedEscrow(ledger,
				before, request, out string retryId, out result, out failure), failure);
			ClassicAssert.AreEqual(projectionId, retryId);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			CollectionAssert.AreEqual(prepared, KingdomPolityCodec.EncodeEnvelope(ledger));

			ClassicAssert.IsTrue(KingdomPolityConflictRules.TryCreateEscrowCustodyProof(ledger,
				projectionId, 221L, out KingdomPolityEscrowCustodyProof custody,
				out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityConflictRules.TryCommitConsentedEscrowCustody(ledger,
				ledger.Revision, custody, out result, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityProjectionPhase.Committed,
				KingdomPolityGapTestData.Projection(ledger, projectionId).Phase);
			ClassicAssert.IsTrue(KingdomPolityConflictRules.TryConcludeConsentedEscrow(ledger,
				ledger.Revision, projectionId, 222L, out result, out failure), failure);

			KingdomPolityIncidentRecord clash = Clash(ledger);
			ClassicAssert.AreEqual(KingdomPolityResolutionKind.ConsentedEscrow,
				clash.Conclusion.ResolutionKind);
			ClassicAssert.AreEqual(0, clash.Conclusion.ObservedFactIds.Count);
			ClassicAssert.AreEqual(0, clash.Conclusion.RelationDeltas.Count);
			ClassicAssert.AreEqual(1, clash.Conclusion.SystemicDeltas.Count);
			ClassicAssert.AreEqual(KingdomPolitySystemicDeltaKind.ReservedStake,
				clash.Conclusion.SystemicDeltas[0].Kind);
			ClassicAssert.AreEqual(-1, clash.Conclusion.SystemicDeltas[0].Amount);
			ClassicAssert.AreEqual(KingdomPolityAftermathKind.ConsentedResolution,
				clash.Aftermath.Kind);
			ClassicAssert.AreEqual(KingdomPolityRoutePhase.Blocked,
				KingdomPolityGapTestData.RouteRecord(ledger).Phase);
			ClassicAssert.AreEqual(relationBefore, KingdomPolityGapTestData.Relation(ledger).Band);

			ClassicAssert.IsTrue(KingdomPolityConflictRules.TryCreateEscrowRefundProof(ledger,
				projectionId, 223L, out KingdomPolityEscrowRefundProof refund,
				out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityConflictRules.TryReleaseConsentedEscrow(ledger,
				ledger.Revision, refund, out result, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityProjectionPhase.Cleaned,
				KingdomPolityGapTestData.Projection(ledger, projectionId).Phase);
			ClassicAssert.AreEqual(KingdomPolityRoutePhase.AvailableToWitness,
				KingdomPolityGapTestData.RouteRecord(ledger).Phase);
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
			KingdomPolityLedger decoded = KingdomPolityCodec.DecodeEnvelope(
				KingdomPolityCodec.EncodeEnvelope(ledger));
			ClassicAssert.AreEqual(KingdomPolityProjectionKind.ConsentedEscrow,
				KingdomPolityGapTestData.Projection(decoded, projectionId).Kind);
			ClassicAssert.AreEqual(KingdomPolityResolutionKind.ConsentedEscrow,
				Clash(decoded).Conclusion.ResolutionKind);
		}

		[Test]
		public void PreparedEscrowCanCancelWithoutConclusionOrRouteLoss()
		{
			KingdomPolityLedger ledger = Open();
			KingdomPolityConsentedEscrowRequest request = Request(ledger, 220L);
			KingdomPolityRoutePhase routeBefore =
				KingdomPolityGapTestData.RouteRecord(ledger).Phase;
			ClassicAssert.IsTrue(KingdomPolityConflictRules.TryPrepareConsentedEscrow(ledger,
				ledger.Revision, request, out string projectionId, out _,
				out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityConflictRules.TryCreateEscrowRefundProof(ledger,
				projectionId, 221L, out KingdomPolityEscrowRefundProof refund,
				out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityConflictRules.TryReleaseConsentedEscrow(ledger,
				ledger.Revision, refund, out KingdomPolityPublicationResult result,
				out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityProjectionPhase.Cancelled,
				KingdomPolityGapTestData.Projection(ledger, projectionId).Phase);
			ClassicAssert.IsNull(Clash(ledger).Conclusion);
			ClassicAssert.IsNull(Clash(ledger).Intervention);
			ClassicAssert.AreEqual(routeBefore, KingdomPolityGapTestData.RouteRecord(ledger).Phase);
			Assert.Throws<InvalidDataException>(() =>
				KingdomPolityCodec.EncodeEnvelopeV4Fixture(ledger));
			byte[] cancelled = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsTrue(KingdomPolityConflictRules.TryReleaseConsentedEscrow(ledger,
				ledger.Revision - 1L, refund, out result, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			CollectionAssert.AreEqual(cancelled, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void ForgedConsentCustodyAndCasLeaveAuthorityByteIdentical()
		{
			KingdomPolityLedger ledger = Open();
			KingdomPolityConsentedEscrowRequest request = Request(ledger, 220L);
			byte[] open = KingdomPolityCodec.EncodeEnvelope(ledger);
			request.ConsentFactId = "taf:fact:inferred:consent";
			ClassicAssert.IsFalse(KingdomPolityConflictRules.TryPrepareConsentedEscrow(ledger,
				ledger.Revision, request, out string _, out _, out string failure));
			CollectionAssert.AreEqual(open, KingdomPolityCodec.EncodeEnvelope(ledger));
			request = Request(ledger, 220L);
			ClassicAssert.IsFalse(KingdomPolityConflictRules.TryPrepareConsentedEscrow(ledger,
				ledger.Revision - 1L, request, out _, out _, out failure));
			CollectionAssert.AreEqual(open, KingdomPolityCodec.EncodeEnvelope(ledger));
			ClassicAssert.IsTrue(KingdomPolityConflictRules.TryPrepareConsentedEscrow(ledger,
				ledger.Revision, request, out string projectionId, out _, out failure), failure);
			byte[] prepared = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsFalse(KingdomPolityConflictRules.TryConcludeConsentedEscrow(ledger,
				ledger.Revision, projectionId, 221L, out _, out failure));
			ClassicAssert.IsTrue(KingdomPolityConflictRules.TryCreateEscrowCustodyProof(ledger,
				projectionId, 221L, out KingdomPolityEscrowCustodyProof forged,
				out failure), failure);
			forged.CollateralObjectId = "foreign-collateral";
			ClassicAssert.IsFalse(KingdomPolityConflictRules.TryCommitConsentedEscrowCustody(ledger,
				ledger.Revision, forged, out _, out failure));
			CollectionAssert.AreEqual(prepared, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void OldWireGoldenKeepsValuesAndFutureProjectionKindFailsClosed()
		{
			ClassicAssert.AreEqual(1, (byte)KingdomPolityProjectionKind.Faction);
			ClassicAssert.AreEqual(7, (byte)KingdomPolityProjectionKind.FactionTombstone);
			ClassicAssert.AreEqual(8, (byte)KingdomPolityProjectionKind.ConsentedEscrow);
			ClassicAssert.AreEqual(1, (byte)KingdomPolityResolutionKind.LiveScene);
			ClassicAssert.AreEqual(2, (byte)KingdomPolityResolutionKind.ConsentedEscrow);
			ClassicAssert.AreEqual(4, (byte)KingdomPolityInterventionChoice.Observe);
			ClassicAssert.AreEqual(5,
				(byte)KingdomPolityInterventionChoice.ConsentAbstractResolution);
			ClassicAssert.AreEqual(2, (byte)KingdomPolityAftermathKind.WitnessedWithdrawal);
			ClassicAssert.AreEqual(3, (byte)KingdomPolityAftermathKind.ConsentedResolution);
			ClassicAssert.AreEqual(7, (byte)KingdomPolityGrievanceCause.RefusedTerms);
			ClassicAssert.AreEqual(8, (byte)KingdomPolityGrievanceCause.ResourceRefusal);

			byte[] old = KingdomPolityCodec.EncodeEnvelopeV4Fixture(
				KingdomPolityTestData.Full());
			ClassicAssert.AreEqual(KingdomPolityCodec.PriorWireVersion,
				BitConverter.ToInt32(old, 4));
			ClassicAssert.AreEqual((byte)KingdomPolityProjectionKind.Faction,
				ByteAfterUniqueText(old, "taf:projection:faction-rival"));
			ClassicAssert.AreEqual((byte)KingdomPolityProjectionKind.IncidentView,
				ByteAfterUniqueText(old, "taf:projection:incident-view"));
			KingdomPolityLedger migrated = KingdomPolityCodec.DecodeEnvelope(old);
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(migrated,
				out string failure), failure);

			KingdomPolityLedger ledger = Open();
			KingdomPolityConsentedEscrowRequest request = Request(ledger, 220L);
			ClassicAssert.IsTrue(KingdomPolityConflictRules.TryPrepareConsentedEscrow(ledger,
				ledger.Revision, request, out string projectionId, out _, out failure), failure);
			byte[] current = KingdomPolityCodec.EncodeEnvelope(ledger);
			int kind = IndexAfterUniqueText(current, projectionId);
			ClassicAssert.AreEqual((byte)KingdomPolityProjectionKind.ConsentedEscrow, current[kind]);
			current[kind] = 9;
			KingdomPolityLedger unknown = KingdomPolityCodec.DecodeEnvelope(current);
			ClassicAssert.AreEqual(KingdomPolitySchemaState.Quarantined, unknown.SchemaState);
			ClassicAssert.IsFalse(KingdomPolityRules.Usable(unknown));
			ClassicAssert.AreEqual(9, (byte)KingdomPolityGapTestData.Projection(unknown,
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
