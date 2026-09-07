using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityAdversarialTests
	{
		[Test]
		public void ResidentBridgeIsExactCurrentActiveAndUniquelyOwned()
		{
			KingdomPolityLedger l = KingdomPolityTestData.Full();
			l.NamedFigures[0].ResidentSettlementId = null;
			ClassicAssert.IsFalse(KingdomPolityRules.TryValidate(l, out string _),
				"partial resident bridge must fail");

			l = KingdomPolityTestData.Full();
			l.NamedFigures[0].ResidentSettlementId = "taf:settlement:legacy";
			ClassicAssert.IsFalse(KingdomPolityRules.TryValidate(l, out string _),
				"resident bridge requires minted v1 settlement identity");

			l = KingdomPolityTestData.Full();
			l.NamedFigures[1].ResidentId = l.NamedFigures[0].ResidentId;
			l.NamedFigures[1].ResidentSettlementId = l.NamedFigures[0].ResidentSettlementId;
			l.NamedFigures[0].ResidentId = 0; l.NamedFigures[0].ResidentSettlementId = null;
			ClassicAssert.IsFalse(KingdomPolityRules.TryValidate(l, out string _),
				"imported actor cannot become current resident authority");

			l = KingdomPolityTestData.Full();
			l.NamedFigures[0].Phase = KingdomPolityFigurePhase.Retired;
			l.NamedFigures[0].ConclusionRef = "taf:conclusion:successor-retired";
			ClassicAssert.IsFalse(KingdomPolityRules.TryValidate(l, out string _),
				"inactive figure cannot retain resident bridge");

			l = KingdomPolityTestData.Full();
			l.NamedFigures.Insert(1, new KingdomPolityNamedFigureRecord
			{
				FigureId = "taf:figure:current-successor-second",
				PolityId = KingdomPolityTestData.Realm, DisplayName = "Ira Twice Recorded",
				RoleKey = "officeholder", Origin = KingdomPolityFigureOrigin.Officeholder,
				Phase = KingdomPolityFigurePhase.Active, CauseRef = "taf:event:duplicate-resident",
				ResidentId = 17, ResidentSettlementId = KingdomPolityTestData.Settlement
			});
			ClassicAssert.IsFalse(KingdomPolityRules.TryValidate(l, out string _),
				"one resident cannot authorize two named figures");
		}

		[Test]
		public void CanonicalOrderDuplicateEdgesAndSecondLiveRivalFailWhole()
		{
			KingdomPolityLedger l = KingdomPolityTestData.Full();
			KingdomPolityRelation first = l.Relations[0];
			l.Relations[0] = l.Relations[1]; l.Relations[1] = first;
			ClassicAssert.IsFalse(KingdomPolityRules.TryValidate(l, out string _));

			l = KingdomPolityTestData.Full();
			l.Relations[1].FromPolityId = l.Relations[0].FromPolityId;
			l.Relations[1].ToPolityId = l.Relations[0].ToPolityId;
			ClassicAssert.IsFalse(KingdomPolityRules.TryValidate(l, out string _));

			l = KingdomPolityTestData.Full();
			l.Polities.Insert(1, new KingdomPolityRecord
			{
				PolityId = "taf:polity:second", DisplayName = "A second rival", NameRevision = 1,
				Source = KingdomPolitySource.AuthoredRival, Lifecycle = KingdomPolityLifecycle.Latent
			});
			ClassicAssert.IsFalse(KingdomPolityRules.TryValidate(l, out string _));
		}

		[Test]
		public void PinnedProfilesSurviveDeclaredCompaction()
		{
			KingdomPolityLedger l = KingdomPolityTestData.Full();
			KingdomPolityProfileRevision retired = KingdomPolityTestData.Profile(
				KingdomPolityTestData.CurrentProfile, KingdomPolityTestData.Realm,
				"taf:fact:current-school", "settler", "warden",
				KingdomPolityLoadoutPolicyKind.StockPreserve);
			retired.Revision = 2; retired.EffectiveTick = 100L;
			KingdomPolityProfileRevision current = KingdomPolityTestData.Profile(
				KingdomPolityTestData.CurrentProfile, KingdomPolityTestData.Realm,
				"taf:fact:current-school-current", "settler", "warden",
				KingdomPolityLoadoutPolicyKind.StockPreserve);
			current.Revision = 3; current.EffectiveTick = 150L;
			l.Profiles.Insert(1, retired); l.Profiles.Insert(2, current);
			l.Polities[1].ProfileRevision = 3;
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(l, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryCompactRetiredProfiles(l,
				"taf:compaction:profiles-1", 200L, out failure), failure);
			ClassicAssert.AreEqual(3, l.Profiles.Count);
			ClassicAssert.AreEqual(1, l.Profiles[0].Revision,
				"foundation projection root must survive later revisions");
			ClassicAssert.AreEqual(3, l.Profiles[1].Revision);
			ClassicAssert.AreEqual(KingdomPolityTestData.RivalProfile, l.Profiles[2].ProfileId,
				"cohort-pinned rival revision must survive");
			ClassicAssert.AreEqual(1, l.Compactions.Count);
			ClassicAssert.AreEqual(KingdomPolityTestData.CurrentProfile,
				l.Compactions[0].RemovedProfiles[0].ProfileId);
			ClassicAssert.AreEqual(2, l.Compactions[0].RemovedProfiles[0].Revision);
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(l, out failure), failure);
		}

		[Test]
		public void PresentationOptionNeverBacklogsDisabledCauses()
		{
			KingdomPolityLedger l = KingdomPolityTestData.Full(); string failure;
			ClassicAssert.IsTrue(KingdomPolityRules.TryObservePresentation(l,
				KingdomPolityPresentationState.Enabled, 100L, out failure), failure);
			ClassicAssert.IsFalse(KingdomPolityRules.CanEmitOptionalProjection(l, 99L));
			ClassicAssert.IsTrue(KingdomPolityRules.CanEmitOptionalProjection(l, 100L));
			ClassicAssert.IsTrue(KingdomPolityRules.TryObservePresentation(l,
				KingdomPolityPresentationState.Disabled, 200L, out failure), failure);
			ClassicAssert.IsFalse(KingdomPolityRules.CanEmitOptionalProjection(l, long.MaxValue));
			ClassicAssert.IsTrue(KingdomPolityRules.TryObservePresentation(l,
				KingdomPolityPresentationState.Enabled, 300L, out failure), failure);
			ClassicAssert.AreEqual(2L, l.Options.EnableEpoch);
			ClassicAssert.IsFalse(KingdomPolityRules.CanEmitOptionalProjection(l, 299L));
			ClassicAssert.IsTrue(KingdomPolityRules.CanEmitOptionalProjection(l, 300L));
		}

		[Test]
		public void IncidentHasOneWitnessedConclusionAndEscrowCannotAuthorWarLoss()
		{
			KingdomPolityLedger l = KingdomPolityTestData.Full();
			l.Projections.RemoveAt(1);
			l.Incidents[0].Conclusion = new KingdomPolityIncidentConclusion
			{
				ConclusionId = "taf:conclusion:crossing", ResolutionKind = KingdomPolityResolutionKind.LiveScene,
				CommitTick = 500L, ObservedFactIds = new List<string> { "taf:fact:actors-survived" },
				ReceiptRefs = new List<string> { "taf:receipt:live-scene" }
			};
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(l, out string failure), failure);
			l.Incidents[0].Conclusion.ObservedFactIds.Clear();
			ClassicAssert.IsFalse(KingdomPolityRules.TryValidate(l, out failure),
				"a live-scene conclusion needs at least one freshly observed fact");
			l.Incidents[0].Conclusion.ObservedFactIds.Add("taf:fact:actors-survived");

			KingdomPolityIncidentConclusion c = l.Incidents[0].Conclusion;
			c.ResolutionKind = KingdomPolityResolutionKind.ConsentedEscrow;
			c.ObservedFactIds.Clear(); c.ConsentReceiptId = "taf:receipt:consent";
			c.EscrowReceiptId = "taf:receipt:escrow";
			c.SnapshotReceiptId = "taf:receipt:snapshot";
			c.ReceiptRefs = new List<string>
			{
				"taf:receipt:consent", "taf:receipt:escrow", "taf:receipt:snapshot"
			};
			c.SystemicDeltas.Add(new KingdomPolitySystemicDelta
			{
				Kind = KingdomPolitySystemicDeltaKind.Relation,
				TargetId = "taf:relation:current-rival", Amount = -1,
				ReceiptId = "taf:receipt:relation-loss"
			});
			ClassicAssert.IsFalse(KingdomPolityRules.TryValidate(l, out failure),
				"consented escrow permits only exact reserved stake and one reversible wound");
			c.SystemicDeltas[0].Kind = KingdomPolitySystemicDeltaKind.ReversibleWound;
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(l, out failure), failure);

			c.RelationDeltas.Add(new KingdomPolityRelationDelta
			{
				RelationId = "taf:relation:current-rival",
				Before = KingdomPolityRelationBand.Rival, After = KingdomPolityRelationBand.Hostile,
				ReceiptId = "taf:receipt:relation-delta"
			});
			ClassicAssert.IsFalse(KingdomPolityRules.TryValidate(l, out failure),
				"consented escrow cannot author a relation delta");
			c.RelationDeltas.Clear(); c.SystemicDeltas[0].Kind =
				KingdomPolitySystemicDeltaKind.ReservedStake;
			c.SystemicDeltas[0].TargetId = "taf:stake:not-disclosed";
			ClassicAssert.IsFalse(KingdomPolityRules.TryValidate(l, out failure),
				"consented escrow cannot spend an undisclosed stake");
		}

		[Test]
		public void InvalidCurrentGraphQuarantinesAndRemainsInspectable()
		{
			KingdomPolityLedger l = KingdomPolityTestData.Full();
			l.Relations[0].ToPolityId = "taf:polity:missing";
			KingdomPolityRules.Normalize(l);
			ClassicAssert.AreEqual(KingdomPolitySchemaState.Quarantined, l.SchemaState);
			ClassicAssert.IsFalse(KingdomPolityRules.Usable(l));
			KingdomPolityLedger decoded = KingdomPolityCodec.DecodeEnvelope(
				KingdomPolityCodec.EncodeEnvelope(l));
			ClassicAssert.AreEqual(KingdomPolitySchemaState.Quarantined, decoded.SchemaState);
			ClassicAssert.AreEqual("taf:polity:missing", decoded.Relations[0].ToPolityId);

			KingdomPolityLedger falseFuture = KingdomPolityTestData.Full();
			falseFuture.SchemaState = KingdomPolitySchemaState.Unknown;
			KingdomPolityRules.Normalize(falseFuture);
			ClassicAssert.AreEqual(KingdomPolitySchemaState.Quarantined, falseFuture.SchemaState);
		}

		[Test]
		public void HostileEnvelopeLengthsAndTrailingPayloadRefuseBeforeAuthority()
		{
			byte[] envelope = KingdomPolityCodec.EncodeEnvelope(KingdomPolityTestData.Full());
			byte[] mismatch = (byte[])envelope.Clone(); mismatch[8] = 0; mismatch[9] = 0;
			mismatch[10] = 0; mismatch[11] = 0;
			Assert.Throws<InvalidDataException>(() => KingdomPolityCodec.DecodeEnvelope(mismatch));

			byte[] trailing = new byte[envelope.Length + 1];
			Buffer.BlockCopy(envelope, 0, trailing, 0, envelope.Length);
			int length = envelope.Length - 12 + 1;
			byte[] encodedLength = BitConverter.GetBytes(length);
			Buffer.BlockCopy(encodedLength, 0, trailing, 8, 4);
			Assert.Throws<InvalidDataException>(() => KingdomPolityCodec.DecodeEnvelope(trailing));
		}

		[Test]
		public void RealmRebindAllowsOnlyEvidenceFreeLedger()
		{
			ClassicAssert.IsTrue(KingdomPolityRules.TryCreate(KingdomPolityTestData.Realm,
				KingdomPolityImportPolicy.Off, out KingdomPolityLedger empty, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryRebindEmptyIdentity(empty,
				"taf:realm:v1:refounded", KingdomPolityImportPolicy.Off, out failure), failure);
			ClassicAssert.AreEqual("taf:realm:v1:refounded", empty.RealmId);
			ClassicAssert.AreEqual(KingdomPolityPresentationState.Unobserved, empty.Options.Presentation);

			KingdomPolityLedger populated = KingdomPolityTestData.Full();
			ClassicAssert.IsFalse(KingdomPolityRules.TryRebindEmptyIdentity(populated,
				"taf:realm:v1:refounded", KingdomPolityImportPolicy.Off, out failure));
			ClassicAssert.AreEqual(KingdomPolityTestData.Realm, populated.RealmId);
			StringAssert.Contains("explicit realm transition", failure);
		}
	}
}
