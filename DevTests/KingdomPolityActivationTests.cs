using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityActivationTests
	{
		private const string Realm =
			"taf:realm:v1:1111111111111111111111111111111111111111111111111111111111111111";
		private const string Settlement =
			"taf:settlement:v1:2222222222222222222222222222222222222222222222222222222222222222";

		[Test]
		public void FoundationCasPublishesOneFreshLatentLegacyAndIsIdempotent()
		{
			KingdomPolityLedger ledger = Ledger(KingdomPolityImportPolicy.LatestEligible);
			KingdomPolityFoundationFacts facts = Current(); KingdomPolityLegacySnapshot legacy = Legacy();
			long source = ledger.Revision;
			Assert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, source, facts, legacy,
				out KingdomPolityPublicationResult result, out string failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			Assert.AreEqual(source + 1, ledger.Revision); Assert.AreEqual(2, ledger.Polities.Count);
			KingdomPolityRecord imported = Imported(ledger);
			Assert.AreEqual(KingdomPolityLifecycle.Latent, imported.Lifecycle);
			Assert.AreEqual(KingdomPolitySource.ImportedLegacy, imported.Source);
			Assert.AreNotEqual(legacy.LegacyToken, imported.PolityId);
			StringAssert.DoesNotContain(legacy.LegacyToken, imported.PolityId);
			StringAssert.DoesNotContain(legacy.LegacyToken, imported.ProjectedFactionId);
			Assert.AreEqual(2, ledger.Relations.Count); Assert.AreEqual(1, ledger.NamedFigures.Count);
			Assert.AreEqual(KingdomPolityFigureOrigin.Claimant, ledger.NamedFigures[0].Origin);
			StringAssert.DoesNotContain(legacy.FounderName, ledger.NamedFigures[0].DisplayName);
			Assert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, source, facts, legacy,
				out result, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			Assert.AreEqual(source + 1, ledger.Revision);
		}

		[Test]
		public void PreparedFactionRecoversCommitThenOwnedTombstoneBecomesDormant()
		{
			KingdomPolityLedger ledger = Published(); long before = ledger.Revision;
			Assert.IsTrue(KingdomPolityRules.TryPrepareLegacyFaction(ledger, before, 40L,
				out KingdomPolityPublicationResult prepared, out string failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.Applied, prepared.Outcome);
			Assert.IsTrue(KingdomPolityRules.TryPrepareLegacyFaction(ledger, before, 99L,
				out KingdomPolityPublicationResult retry, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			Assert.AreEqual(prepared.ProjectionId, retry.ProjectionId);
			Assert.IsTrue(KingdomPolityRules.TryCommitLegacyFaction(ledger, ledger.Revision,
				prepared.ProjectionId, 41L, out KingdomPolityPublicationResult committed,
				out failure), failure);
			Assert.AreEqual(KingdomPolityLifecycle.Active, Imported(ledger).Lifecycle);
			Assert.IsTrue(KingdomPolityRules.TryPrepareLegacyFaction(ledger, before, 49L,
				out retry, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			Assert.IsTrue(KingdomPolityRules.TryGetImportedFactionProjection(ledger,
				out KingdomPolityFactionProjectionView view, out failure), failure);
			Assert.AreEqual(KingdomPolityProjectionPhase.Committed, view.Phase);
			Assert.IsTrue(KingdomPolityRules.TryPrepareLegacyFactionTombstone(ledger,
				ledger.Revision, 50L, out KingdomPolityPublicationResult tombstone, out failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryCommitLegacyFactionTombstone(ledger,
				ledger.Revision, tombstone.ProjectionId, 51L,
				out KingdomPolityPublicationResult _, out failure), failure);
			Assert.AreEqual(KingdomPolityLifecycle.Dormant, Imported(ledger).Lifecycle);
			Assert.IsTrue(KingdomPolityRules.TryPrepareLegacyFactionTombstone(ledger,
				before, 52L, out retry, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
		}

		[Test]
		public void ValidProfileMutationCannotMasqueradeAsFoundationIdempotence()
		{
			KingdomPolityLedger ledger = Published(); long revision = ledger.Revision;
			KingdomPolityProfileRevision current = null;
			for (int i = 0; i < ledger.Profiles.Count; i++)
				if (ledger.Profiles[i].PolityId == Realm) current = ledger.Profiles[i];
			Assert.IsNotNull(current); Assert.Greater(current.PracticeTags.Count, 0);
			current.PracticeTags[current.PracticeTags.Count - 1] = "zz-tampered";
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out string failure), failure);
			Assert.IsFalse(KingdomPolityRules.TryObserveCurrentFoundation(ledger,
				Realm, Realm, out failure));
			Assert.IsFalse(KingdomPolityRules.TryPublishFoundation(ledger, revision,
				Current(), Legacy(), out KingdomPolityPublicationResult refused, out failure));
			Assert.AreEqual(KingdomPolityCasOutcome.Refused, refused.Outcome);
			Assert.AreEqual(revision, ledger.Revision);
		}

		[Test]
		public void PublishedFoundationIsObservedWithoutReReadingMutableLiveFacts()
		{
			KingdomPolityLedger ledger = Published();
			Assert.IsTrue(KingdomPolityRules.TryObserveCurrentFoundation(ledger,
				Realm, Realm, out string failure), failure);
			KingdomPolityFoundationFacts changed = Current();
			changed.FounderName = "Ari-after-renaming"; changed.Stage = 5;
			changed.Population = 999;
			Assert.IsFalse(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision,
				changed, Legacy(), out KingdomPolityPublicationResult refused, out failure));
			Assert.AreEqual(KingdomPolityCasOutcome.Refused, refused.Outcome);
			Assert.IsTrue(KingdomPolityRules.TryObserveCurrentFoundation(ledger,
				Realm, Realm, out failure), failure);
		}

		[Test]
		public void GloballyValidButForeignFactionDigestNeverProjectsOrCommits()
		{
			KingdomPolityLedger ledger = Published();
			Assert.IsTrue(KingdomPolityRules.TryPrepareLegacyFaction(ledger, ledger.Revision,
				40L, out KingdomPolityPublicationResult prepared, out string failure), failure);
			KingdomPolityProjectionReceipt receipt = null;
			for (int i = 0; i < ledger.Projections.Count; i++)
				if (ledger.Projections[i].ProjectionId == prepared.ProjectionId)
					receipt = ledger.Projections[i];
			Assert.IsNotNull(receipt); receipt.AppliedDigest = KingdomPolityTestData.DigestB;
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
			Assert.IsFalse(KingdomPolityRules.TryGetImportedFactionProjection(ledger,
				out KingdomPolityFactionProjectionView _, out failure));
			Assert.IsFalse(KingdomPolityRules.TryCommitLegacyFaction(ledger, ledger.Revision,
				prepared.ProjectionId, 41L, out KingdomPolityPublicationResult refused,
				out failure));
			Assert.AreEqual(KingdomPolityCasOutcome.Refused, refused.Outcome);
			Assert.AreEqual(KingdomPolityLifecycle.Latent, Imported(ledger).Lifecycle);
		}

		[Test]
		public void RevisionConflictAndForeignPopulationNeverPartiallyPublish()
		{
			KingdomPolityLedger ledger = Ledger(KingdomPolityImportPolicy.LatestEligible);
			Assert.IsFalse(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision + 1,
				Current(), Legacy(), out KingdomPolityPublicationResult conflict, out string _));
			Assert.AreEqual(KingdomPolityCasOutcome.Conflict, conflict.Outcome);
			Assert.AreEqual(0, ledger.Polities.Count); Assert.AreEqual(0, ledger.Profiles.Count);
			KingdomPolityLedger populated = Published();
			KingdomPolityLegacySnapshot other = Legacy(); other.LegacyToken = "lgc-b-other";
			long revision = populated.Revision;
			Assert.IsFalse(KingdomPolityRules.TryPublishFoundation(populated, revision,
				Current(), other, out KingdomPolityPublicationResult refused, out string _));
			Assert.AreEqual(KingdomPolityCasOutcome.Refused, refused.Outcome);
			Assert.AreEqual(revision, populated.Revision); Assert.AreEqual(2, populated.Polities.Count);
		}

		[Test]
		public void SameFactsRegenerateSameProfilesAndPartnerNamesake()
		{
			KingdomPolityFoundationFacts facts = Current();
			KingdomPolityLegacySnapshot legacy = Legacy(); legacy.Style = facts.Style;
			legacy.FounderName = facts.FounderName;
			KingdomPolityLedger a = Ledger(KingdomPolityImportPolicy.LatestEligible);
			KingdomPolityLedger b = Ledger(KingdomPolityImportPolicy.LatestEligible);
			Assert.IsTrue(KingdomPolityRules.TryPublishFoundation(a, a.Revision, facts, legacy,
				out KingdomPolityPublicationResult _, out string failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryPublishFoundation(b, b.Revision, facts, legacy,
				out KingdomPolityPublicationResult _, out failure), failure);
			CollectionAssert.AreEqual(KingdomPolityCodec.EncodeEnvelope(a),
				KingdomPolityCodec.EncodeEnvelope(b));
			Assert.AreEqual(KingdomPolityRelationBand.Pact, a.Relations[0].Band);
			Assert.AreEqual(KingdomPolityFigureOrigin.Namesake, a.NamedFigures[0].Origin);
			Assert.AreEqual(KingdomPolityFigureOrigin.LegacyEnvoy,
				KingdomPolityRules.FigureOriginFor(KingdomPolityRelationBand.Contact, false));
			Assert.AreEqual(KingdomPolityFigureOrigin.LegacyEnvoy,
				KingdomPolityRules.FigureOriginFor(KingdomPolityRelationBand.Pact, false));
			Assert.AreEqual(KingdomPolityFigureOrigin.Namesake,
				KingdomPolityRules.FigureOriginFor(KingdomPolityRelationBand.Pact, true));
			Assert.AreEqual(KingdomPolityFigureOrigin.Successor,
				KingdomPolityRules.LegacyFigureOriginFor(
					KingdomPolityRelationBand.Pact, false, 0));
			Assert.AreEqual(KingdomPolityFigureOrigin.LegacyEnvoy,
				KingdomPolityRules.LegacyFigureOriginFor(
					KingdomPolityRelationBand.Pact, false, 1));
			Assert.AreEqual(KingdomPolityFigureOrigin.Successor,
				KingdomPolityFigureOrigin.Successor);
		}

		[Test]
		public void HeldPartnerCreatesInstitutionalSuccessorNotOldActor()
		{
			KingdomPolityFoundationFacts facts = Current();
			KingdomPolityLegacySnapshot legacy = Legacy(); legacy.Style = facts.Style;
			legacy.InheritedState = 0;
			KingdomPolityLedger ledger = Ledger(KingdomPolityImportPolicy.LatestEligible);
			Assert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision,
				facts, legacy, out KingdomPolityPublicationResult _, out string failure), failure);
			Assert.AreEqual(KingdomPolityFigureOrigin.Successor, ledger.NamedFigures[0].Origin);
			Assert.AreEqual("successor", ledger.NamedFigures[0].RoleKey);
			StringAssert.Contains("Successor", ledger.NamedFigures[0].DisplayName);
			Assert.IsNull(ledger.NamedFigures[0].ResidentSettlementId);
			Assert.AreEqual(0, ledger.NamedFigures[0].ResidentId);
		}

		[Test]
		public void LegacySnapshotSchemaCannotCarryOldRuntimeIdentities()
		{
			System.Type type = typeof(KingdomPolityLegacySnapshot);
			Assert.IsNull(type.GetField("RealmId")); Assert.IsNull(type.GetField("FactionId"));
			Assert.IsNull(type.GetField("SettlementId")); Assert.IsNull(type.GetField("ActorId"));
			Assert.IsNull(type.GetField("OriginGameId"));
		}

		[Test]
		public void ExileCasRetiresCurrentAndImportedWithBoundedRollbackReceipt()
		{
			KingdomPolityLedger ledger = ActivePublished();
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger); long revision = ledger.Revision;
			Assert.IsTrue(KingdomPolityRules.TryPrepareRealmExile(ledger, revision,
				ExileFacts(), out KingdomPolityRealmTransition transition,
				out KingdomPolityPublicationResult result, out string failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			Assert.AreEqual(revision + 1L, ledger.Revision);
			for (int i = 0; i < ledger.Polities.Count; i++)
				if (ledger.Polities[i].Source == KingdomPolitySource.CurrentRealm ||
					ledger.Polities[i].Source == KingdomPolitySource.ImportedLegacy)
				{
					Assert.AreEqual(KingdomPolityLifecycle.Ended, ledger.Polities[i].Lifecycle);
					Assert.AreEqual(80L, ledger.Polities[i].EndedTick);
				}
			Assert.IsTrue(KingdomPolityRules.TryValidateRealmTransition(transition,
				out failure), failure);
			CollectionAssert.AreEqual(before, transition.ReturnLedgerEnvelope);
			Assert.AreEqual(KingdomPolityRealmTransitionPhase.Prepared, transition.Phase);
			Assert.IsTrue(transition.OldImportedWasVisible);
			Assert.AreNotEqual(Realm, transition.Legacy.LegacyToken);
			Assert.IsNull(typeof(KingdomPolityRealmTransition).GetField("ActorId"));
		}

		[Test]
		public void ExileRevisionConflictChangesNeitherLedgerNorReceipt()
		{
			KingdomPolityLedger ledger = ActivePublished();
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomPolityRules.TryPrepareRealmExile(ledger,
				ledger.Revision + 1L, ExileFacts(), out KingdomPolityRealmTransition transition,
				out KingdomPolityPublicationResult result, out string _));
			Assert.AreEqual(KingdomPolityCasOutcome.Conflict, result.Outcome);
			Assert.IsNull(transition); CollectionAssert.AreEqual(before,
				KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void DetachedExileCanRestoreExactOldAuthority()
		{
			KingdomPolityLedger ledger = ActivePublished();
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			KingdomPolityRealmTransition transition = PrepareAndDetach(ledger);
			Assert.IsFalse(ledger.IdentityBound); Assert.AreEqual(0, ledger.Polities.Count);
			Assert.IsTrue(KingdomPolityRules.TryRestoreRealmReturn(ledger, ledger.Revision,
				transition, transition.Revision, Realm, out KingdomPolityPublicationResult result,
				out string failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			Assert.AreEqual(KingdomPolityRealmTransitionPhase.Restored, transition.Phase);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void RefoundConsumesOnlyLegacyFactsAndMintsFreshAuthority()
		{
			const string nextRealm =
				"taf:realm:v1:3333333333333333333333333333333333333333333333333333333333333333";
			const string nextSettlement =
				"taf:settlement:v1:4444444444444444444444444444444444444444444444444444444444444444";
			KingdomPolityLedger ledger = ActivePublished();
			KingdomPolityRealmTransition transition = PrepareAndDetach(ledger);
			Assert.IsTrue(KingdomPolityRules.TryBindIdentity(ledger, nextRealm,
				KingdomPolityImportPolicy.LatestEligible, out string failure), failure);
			KingdomPolityFoundationFacts next = Current();
			next.RealmId = nextRealm; next.FactionId = nextRealm;
			next.SettlementId = nextSettlement; next.FoundedTick = 90L;
			Assert.IsTrue(KingdomPolityRules.TryGetRealmTransitionLegacy(transition,
				out KingdomPolityLegacySnapshot legacy, out failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision,
				next, legacy, out KingdomPolityPublicationResult published, out failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryPrepareLegacyFaction(ledger, ledger.Revision, 91L,
				out KingdomPolityPublicationResult prepared, out failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryCommitLegacyFaction(ledger, ledger.Revision,
				prepared.ProjectionId, 92L, out KingdomPolityPublicationResult _, out failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryCommitRealmRefound(ledger, transition,
				transition.Revision, out KingdomPolityPublicationResult committed, out failure), failure);
			Assert.AreEqual(KingdomPolityRealmTransitionPhase.Rebound, transition.Phase);
			Assert.IsNull(transition.ReturnLedgerEnvelope);
			Assert.AreEqual(nextRealm, transition.ReboundRealmId);
			Assert.AreNotEqual(Realm, transition.ReboundFactionId);
			Assert.AreNotEqual(transition.OldImportedFactionId, transition.ReboundFactionId);
			Assert.AreEqual(published.ImportedPolityId, committed.ImportedPolityId);
			Assert.IsTrue(KingdomPolityRules.TryValidateRealmTransition(transition,
				out failure), failure);
		}

		[Test]
		public void ExileReceiptDeepCopiesFactsAndQuarantinesCorruptEscrow()
		{
			KingdomPolityLedger ledger = ActivePublished();
			KingdomPolityRealmExileFacts facts = ExileFacts();
			Assert.IsTrue(KingdomPolityRules.TryPrepareRealmExile(ledger, ledger.Revision,
				facts, out KingdomPolityRealmTransition transition,
				out KingdomPolityPublicationResult _, out string failure), failure);
			facts.Legacy.RollNames[0] = "changed outside receipt";
			Assert.AreNotEqual(facts.Legacy.RollNames[0], transition.Legacy.RollNames[0]);
			transition.OldImportedWasVisible = false;
			Assert.IsFalse(KingdomPolityRules.TryValidateRealmTransition(transition, out failure));
			transition.OldImportedWasVisible = true;
			transition.ReturnLedgerEnvelope[0] ^= 0x1;
			Assert.IsFalse(KingdomPolityRules.TryValidateRealmTransition(transition, out failure));
			KingdomPolityRules.NormalizeRealmTransition(transition);
			Assert.AreEqual(KingdomPolityRealmTransitionPhase.Quarantined, transition.Phase);
			Assert.IsNotNull(transition.Fault);
		}

		[Test]
		public void ReturnAndCompletionRetriesRequireExactAuthority()
		{
			KingdomPolityLedger ledger = ActivePublished();
			KingdomPolityRealmTransition transition = PrepareAndDetach(ledger);
			Assert.IsTrue(KingdomPolityRules.TryRestoreRealmReturn(ledger, ledger.Revision,
				transition, transition.Revision, Realm, out KingdomPolityPublicationResult first,
				out string failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryRestoreRealmReturn(ledger, 0L, transition, 0L,
				Realm, out KingdomPolityPublicationResult retry, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			Assert.IsTrue(KingdomPolityRules.TryCompleteRealmReturn(ledger, transition,
				transition.Revision, Realm, out first, out failure), failure);
			Assert.AreEqual(KingdomPolityRealmTransitionPhase.None, transition.Phase);
			Assert.IsTrue(KingdomPolityRules.TryCompleteRealmReturn(ledger, transition,
				transition.Revision, Realm, out retry, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);

			KingdomPolityLedger altered = ActivePublished();
			KingdomPolityRealmTransition alteredTransition = PrepareAndDetach(altered);
			Assert.IsTrue(KingdomPolityRules.TryRestoreRealmReturn(altered, altered.Revision,
				alteredTransition, alteredTransition.Revision, Realm, out first, out failure), failure);
			altered.Revision++;
			Assert.IsTrue(KingdomPolityRules.TryValidate(altered, out failure), failure);
			Assert.IsFalse(KingdomPolityRules.TryRestoreRealmReturn(altered, altered.Revision,
				alteredTransition, alteredTransition.Revision, Realm, out retry, out failure));
			Assert.AreEqual(KingdomPolityCasOutcome.Refused, retry.Outcome);
		}

		[Test]
		public void RefoundRetryRejectsLedgerDifferentFromCommittedReceipt()
		{
			const string nextRealm =
				"taf:realm:v1:3333333333333333333333333333333333333333333333333333333333333333";
			KingdomPolityLedger ledger = ActivePublished();
			KingdomPolityRealmTransition transition = PrepareAndDetach(ledger);
			Assert.IsTrue(KingdomPolityRules.TryBindIdentity(ledger, nextRealm,
				KingdomPolityImportPolicy.LatestEligible, out string failure), failure);
			KingdomPolityFoundationFacts facts = Current(); facts.RealmId = nextRealm;
			facts.FactionId = nextRealm; facts.SettlementId = Settlement; facts.FoundedTick = 90L;
			Assert.IsTrue(KingdomPolityRules.TryGetRealmTransitionLegacy(transition,
				out KingdomPolityLegacySnapshot legacy, out failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision,
				facts, legacy, out KingdomPolityPublicationResult _, out failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryPrepareLegacyFaction(ledger, ledger.Revision, 91L,
				out KingdomPolityPublicationResult prepared, out failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryCommitLegacyFaction(ledger, ledger.Revision,
				prepared.ProjectionId, 92L, out KingdomPolityPublicationResult _, out failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryCommitRealmRefound(ledger, transition,
				transition.Revision, out KingdomPolityPublicationResult first, out failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryCommitRealmRefound(ledger, transition, 0L,
				out KingdomPolityPublicationResult retry, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			ledger.Revision++;
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
			Assert.IsFalse(KingdomPolityRules.TryCommitRealmRefound(ledger, transition, 0L,
				out retry, out failure));
			Assert.AreEqual(KingdomPolityCasOutcome.Refused, retry.Outcome);
		}

		private static KingdomPolityRealmTransition PrepareAndDetach(KingdomPolityLedger ledger)
		{
			Assert.IsTrue(KingdomPolityRules.TryPrepareRealmExile(ledger, ledger.Revision,
				ExileFacts(), out KingdomPolityRealmTransition transition,
				out KingdomPolityPublicationResult _, out string failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryMarkRealmExileTombstoned(ledger, ledger.Revision,
				transition, transition.Revision, out KingdomPolityPublicationResult _, out failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryDetachRealmExile(ledger, ledger.Revision,
				transition, transition.Revision, out KingdomPolityPublicationResult _, out failure), failure);
			return transition;
		}

		private static KingdomPolityLedger ActivePublished()
		{
			KingdomPolityLedger ledger = Published();
			Assert.IsTrue(KingdomPolityRules.TryPrepareLegacyFaction(ledger, ledger.Revision,
				40L, out KingdomPolityPublicationResult prepared, out string failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryCommitLegacyFaction(ledger, ledger.Revision,
				prepared.ProjectionId, 41L, out KingdomPolityPublicationResult _, out failure), failure);
			return ledger;
		}

		private static KingdomPolityRealmExileFacts ExileFacts()
		{
			KingdomPolityLegacySnapshot legacy = Legacy();
			legacy.LegacyToken = "lgc-exile";
			legacy.LineageToken = "lin-exile";
			legacy.RealmName = "The Water Compact"; legacy.SettlementName = "New Ux";
			return new KingdomPolityRealmExileFacts
			{
				RealmId = Realm, FactionId = Realm, ClosedTick = 80L, Legacy = legacy
			};
		}

		private static KingdomPolityLedger Published()
		{
			KingdomPolityLedger ledger = Ledger(KingdomPolityImportPolicy.LatestEligible);
			Assert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision,
				Current(), Legacy(), out KingdomPolityPublicationResult _, out string failure), failure);
			return ledger;
		}

		private static KingdomPolityLedger Ledger(KingdomPolityImportPolicy Policy)
		{
			Assert.IsTrue(KingdomPolityRules.TryCreate(Realm, Policy,
				out KingdomPolityLedger ledger, out string failure), failure); return ledger;
		}

		private static KingdomPolityFoundationFacts Current()
		{
			return new KingdomPolityFoundationFacts
			{
				RealmId = Realm, FactionId = Realm, DisplayName = "The Water Compact",
				FounderName = "Ari", SettlementId = Settlement, Vocation = "holding",
				Style = "salt dunes", Creed = "the covenant", Stage = 1, Population = 7,
				FoundedTick = 30L, OriginKeys = new List<string> { "human" },
				CultureKeys = new List<string> { "Joppa" },
				SpeciesKeys = new List<string> { "human" }
			};
		}

		private static KingdomPolityLegacySnapshot Legacy()
		{
			return new KingdomPolityLegacySnapshot
			{
				LegacyToken = "lgc-a-0001", LineageToken = "lin-a-0001",
				FounderName = "Nara", RealmName = "The Returned Brass",
				SettlementName = "Old Ux", Vocation = "foundry", Style = "deep caves",
				Stage = 2, Population = 12, Defence = 5, StoredWater = 100,
				InheritedState = 1, RollNames = new List<string> { "Nara", "Otho" },
				OriginKeys = new List<string> { "goatfolk" }, OriginCounts = new List<int> { 12 },
				CreedKeys = new List<string> { "brass oath" }, CreedCounts = new List<int> { 12 }
			};
		}

		private static KingdomPolityRecord Imported(KingdomPolityLedger L)
		{
			for (int i = 0; i < L.Polities.Count; i++)
				if (L.Polities[i].Source == KingdomPolitySource.ImportedLegacy) return L.Polities[i];
			return null;
		}
	}
}
