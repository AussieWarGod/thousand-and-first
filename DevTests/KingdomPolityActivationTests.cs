using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, source, facts, legacy,
				out KingdomPolityPublicationResult result, out string failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			ClassicAssert.AreEqual(source + 1, ledger.Revision); ClassicAssert.AreEqual(2, ledger.Polities.Count);
			KingdomPolityRecord imported = Imported(ledger);
			ClassicAssert.AreEqual(KingdomPolityLifecycle.Latent, imported.Lifecycle);
			ClassicAssert.AreEqual(KingdomPolitySource.ImportedLegacy, imported.Source);
			ClassicAssert.AreNotEqual(legacy.LegacyToken, imported.PolityId);
			StringAssert.DoesNotContain(legacy.LegacyToken, imported.PolityId);
			StringAssert.DoesNotContain(legacy.LegacyToken, imported.ProjectedFactionId);
			ClassicAssert.AreEqual(2, ledger.Relations.Count); ClassicAssert.AreEqual(1, ledger.NamedFigures.Count);
			ClassicAssert.AreEqual(KingdomPolityFigureOrigin.Claimant, ledger.NamedFigures[0].Origin);
			StringAssert.DoesNotContain(legacy.FounderName, ledger.NamedFigures[0].DisplayName);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, source, facts, legacy,
				out result, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			ClassicAssert.AreEqual(source + 1, ledger.Revision);
		}

		[Test]
		public void PreparedFactionRecoversCommitThenOwnedTombstoneBecomesDormant()
		{
			KingdomPolityLedger ledger = Published(); long before = ledger.Revision;
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareLegacyFaction(ledger, before, 40L,
				out KingdomPolityPublicationResult prepared, out string failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Applied, prepared.Outcome);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareLegacyFaction(ledger, before, 99L,
				out KingdomPolityPublicationResult retry, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			ClassicAssert.AreEqual(prepared.ProjectionId, retry.ProjectionId);
			ClassicAssert.IsTrue(KingdomPolityRules.TryCommitLegacyFaction(ledger, ledger.Revision,
				prepared.ProjectionId, 41L, out KingdomPolityPublicationResult committed,
				out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityLifecycle.Active, Imported(ledger).Lifecycle);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareLegacyFaction(ledger, before, 49L,
				out retry, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			ClassicAssert.IsTrue(KingdomPolityRules.TryGetImportedFactionProjection(ledger,
				out KingdomPolityFactionProjectionView view, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityProjectionPhase.Committed, view.Phase);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareLegacyFactionTombstone(ledger,
				ledger.Revision, 50L, out KingdomPolityPublicationResult tombstone, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryCommitLegacyFactionTombstone(ledger,
				ledger.Revision, tombstone.ProjectionId, 51L,
				out KingdomPolityPublicationResult _, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityLifecycle.Dormant, Imported(ledger).Lifecycle);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareLegacyFactionTombstone(ledger,
				before, 52L, out retry, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
		}

		[Test]
		public void ValidProfileMutationCannotMasqueradeAsFoundationIdempotence()
		{
			KingdomPolityLedger ledger = Published(); long revision = ledger.Revision;
			KingdomPolityProfileRevision current = null;
			for (int i = 0; i < ledger.Profiles.Count; i++)
				if (ledger.Profiles[i].PolityId == Realm) current = ledger.Profiles[i];
			ClassicAssert.IsNotNull(current); ClassicAssert.Greater(current.PracticeTags.Count, 0);
			current.PracticeTags[current.PracticeTags.Count - 1] = "zz-tampered";
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out string failure), failure);
			ClassicAssert.IsFalse(KingdomPolityRules.TryObserveCurrentFoundation(ledger,
				Realm, Realm, out failure));
			ClassicAssert.IsFalse(KingdomPolityRules.TryPublishFoundation(ledger, revision,
				Current(), Legacy(), out KingdomPolityPublicationResult refused, out failure));
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Refused, refused.Outcome);
			ClassicAssert.AreEqual(revision, ledger.Revision);
		}

		[Test]
		public void PublishedFoundationIsObservedWithoutReReadingMutableLiveFacts()
		{
			KingdomPolityLedger ledger = Published();
			ClassicAssert.IsTrue(KingdomPolityRules.TryObserveCurrentFoundation(ledger,
				Realm, Realm, out string failure), failure);
			KingdomPolityFoundationFacts changed = Current();
			changed.FounderName = "Ari-after-renaming"; changed.Stage = 5;
			changed.Population = 999;
			ClassicAssert.IsFalse(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision,
				changed, Legacy(), out KingdomPolityPublicationResult refused, out failure));
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Refused, refused.Outcome);
			ClassicAssert.IsTrue(KingdomPolityRules.TryObserveCurrentFoundation(ledger,
				Realm, Realm, out failure), failure);
		}

		[Test]
		public void GloballyValidButForeignFactionDigestNeverProjectsOrCommits()
		{
			KingdomPolityLedger ledger = Published();
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareLegacyFaction(ledger, ledger.Revision,
				40L, out KingdomPolityPublicationResult prepared, out string failure), failure);
			KingdomPolityProjectionReceipt receipt = null;
			for (int i = 0; i < ledger.Projections.Count; i++)
				if (ledger.Projections[i].ProjectionId == prepared.ProjectionId)
					receipt = ledger.Projections[i];
			ClassicAssert.IsNotNull(receipt); receipt.AppliedDigest = KingdomPolityTestData.DigestB;
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
			ClassicAssert.IsFalse(KingdomPolityRules.TryGetImportedFactionProjection(ledger,
				out KingdomPolityFactionProjectionView _, out failure));
			ClassicAssert.IsFalse(KingdomPolityRules.TryCommitLegacyFaction(ledger, ledger.Revision,
				prepared.ProjectionId, 41L, out KingdomPolityPublicationResult refused,
				out failure));
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Refused, refused.Outcome);
			ClassicAssert.AreEqual(KingdomPolityLifecycle.Latent, Imported(ledger).Lifecycle);
		}

		[Test]
		public void RevisionConflictAndForeignPopulationNeverPartiallyPublish()
		{
			KingdomPolityLedger ledger = Ledger(KingdomPolityImportPolicy.LatestEligible);
			ClassicAssert.IsFalse(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision + 1,
				Current(), Legacy(), out KingdomPolityPublicationResult conflict, out string _));
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Conflict, conflict.Outcome);
			ClassicAssert.AreEqual(0, ledger.Polities.Count); ClassicAssert.AreEqual(0, ledger.Profiles.Count);
			KingdomPolityLedger populated = Published();
			KingdomPolityLegacySnapshot other = Legacy(); other.LegacyToken = "lgc-b-other";
			long revision = populated.Revision;
			ClassicAssert.IsFalse(KingdomPolityRules.TryPublishFoundation(populated, revision,
				Current(), other, out KingdomPolityPublicationResult refused, out string _));
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Refused, refused.Outcome);
			ClassicAssert.AreEqual(revision, populated.Revision); ClassicAssert.AreEqual(2, populated.Polities.Count);
		}

		[Test]
		public void SameFactsRegenerateSameProfilesAndPartnerNamesake()
		{
			KingdomPolityFoundationFacts facts = Current();
			KingdomPolityLegacySnapshot legacy = Legacy(); legacy.Style = facts.Style;
			legacy.FounderName = facts.FounderName;
			KingdomPolityLedger a = Ledger(KingdomPolityImportPolicy.LatestEligible);
			KingdomPolityLedger b = Ledger(KingdomPolityImportPolicy.LatestEligible);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPublishFoundation(a, a.Revision, facts, legacy,
				out KingdomPolityPublicationResult _, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPublishFoundation(b, b.Revision, facts, legacy,
				out KingdomPolityPublicationResult _, out failure), failure);
			CollectionAssert.AreEqual(KingdomPolityCodec.EncodeEnvelope(a),
				KingdomPolityCodec.EncodeEnvelope(b));
			ClassicAssert.AreEqual(KingdomPolityRelationBand.Pact, a.Relations[0].Band);
			ClassicAssert.AreEqual(KingdomPolityFigureOrigin.Namesake, a.NamedFigures[0].Origin);
			ClassicAssert.AreEqual(KingdomPolityFigureOrigin.LegacyEnvoy,
				KingdomPolityRules.FigureOriginFor(KingdomPolityRelationBand.Contact, false));
			ClassicAssert.AreEqual(KingdomPolityFigureOrigin.LegacyEnvoy,
				KingdomPolityRules.FigureOriginFor(KingdomPolityRelationBand.Pact, false));
			ClassicAssert.AreEqual(KingdomPolityFigureOrigin.Namesake,
				KingdomPolityRules.FigureOriginFor(KingdomPolityRelationBand.Pact, true));
			ClassicAssert.AreEqual(KingdomPolityFigureOrigin.Successor,
				KingdomPolityRules.LegacyFigureOriginFor(
					KingdomPolityRelationBand.Pact, false, 0));
			ClassicAssert.AreEqual(KingdomPolityFigureOrigin.LegacyEnvoy,
				KingdomPolityRules.LegacyFigureOriginFor(
					KingdomPolityRelationBand.Pact, false, 1));
			ClassicAssert.AreEqual(KingdomPolityFigureOrigin.Successor,
				KingdomPolityFigureOrigin.Successor);
		}

		[Test]
		public void CurrentBodyAndEquipmentUseOnlyAdmittedBodyAndExactCraftFacts()
		{
			KingdomPolityFoundationFacts facts = Current();
			facts.OriginKeys = new List<string> { "goatfolk" };
			facts.CultureKeys = new List<string> { "dromad" };
			facts.SpeciesKeys = new List<string> { "human" };
			facts.IdentityKeys = new List<string> { "body:wet-bodied" };
			facts.Stage = 5; facts.TechnologyBand = 2;
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(facts,
				out KingdomPolityProfileRevision profile, out string failure), failure);
			CollectionAssert.AreEqual(new[] { "human" }, profile.BodyKeys);
			ClassicAssert.AreEqual(2, profile.TechnologyBand);
			CollectionAssert.Contains(profile.GearKeys, "bronze-sword");
			CollectionAssert.DoesNotContain(profile.BodyKeys, "goatfolk");
			CollectionAssert.DoesNotContain(profile.BodyKeys, "dromad");

			facts.Stage = 0; facts.TechnologyBand = 6;
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(facts,
				out profile, out failure), failure);
			ClassicAssert.AreEqual(6, profile.TechnologyBand);
			CollectionAssert.Contains(profile.GearKeys, "steel-sword");

			facts.SpeciesKeys.Clear(); facts.IdentityKeys = new List<string> { "body:robot" };
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(facts,
				out profile, out failure), failure);
			CollectionAssert.AreEqual(new[] { "mechanical" }, profile.BodyKeys);

			facts.IdentityKeys = new List<string> { "extension:unproved-body" };
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(facts,
				out profile, out failure), failure);
			CollectionAssert.AreEqual(new[] { "unresolved" }, profile.BodyKeys);
			ClassicAssert.IsFalse(KingdomPolityNpcRules.TryResolve(profile, "guard", 0, 1, 4,
				out KingdomPolityNpcSpec _, out failure));
			StringAssert.Contains("no admissible manifested body", failure);
		}

		[Test]
		public void HeldPartnerCreatesInstitutionalSuccessorNotOldActor()
		{
			KingdomPolityFoundationFacts facts = Current();
			KingdomPolityLegacySnapshot legacy = Legacy(); legacy.Style = facts.Style;
			legacy.InheritedState = 0;
			KingdomPolityLedger ledger = Ledger(KingdomPolityImportPolicy.LatestEligible);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision,
				facts, legacy, out KingdomPolityPublicationResult _, out string failure), failure);
			ClassicAssert.AreEqual(KingdomPolityFigureOrigin.Successor, ledger.NamedFigures[0].Origin);
			ClassicAssert.AreEqual("successor", ledger.NamedFigures[0].RoleKey);
			StringAssert.Contains("Successor", ledger.NamedFigures[0].DisplayName);
			ClassicAssert.IsNull(ledger.NamedFigures[0].ResidentSettlementId);
			ClassicAssert.AreEqual(0, ledger.NamedFigures[0].ResidentId);
		}

		[Test]
		public void LegacySnapshotSchemaCannotCarryOldRuntimeIdentities()
		{
			System.Type type = typeof(KingdomPolityLegacySnapshot);
			ClassicAssert.IsNull(type.GetField("RealmId")); ClassicAssert.IsNull(type.GetField("FactionId"));
			ClassicAssert.IsNull(type.GetField("SettlementId")); ClassicAssert.IsNull(type.GetField("ActorId"));
			ClassicAssert.IsNull(type.GetField("OriginGameId"));
		}

		[Test]
		public void ExileCasRetiresCurrentAndImportedWithBoundedRollbackReceipt()
		{
			KingdomPolityLedger ledger = ActivePublished();
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger); long revision = ledger.Revision;
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareRealmExile(ledger, revision,
				ExileFacts(), out KingdomPolityRealmTransition transition,
				out KingdomPolityPublicationResult result, out string failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			ClassicAssert.AreEqual(revision + 1L, ledger.Revision);
			for (int i = 0; i < ledger.Polities.Count; i++)
				if (ledger.Polities[i].Source == KingdomPolitySource.CurrentRealm ||
					ledger.Polities[i].Source == KingdomPolitySource.ImportedLegacy)
				{
					ClassicAssert.AreEqual(KingdomPolityLifecycle.Ended, ledger.Polities[i].Lifecycle);
					ClassicAssert.AreEqual(80L, ledger.Polities[i].EndedTick);
				}
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidateRealmTransition(transition,
				out failure), failure);
			CollectionAssert.AreEqual(before, transition.ReturnLedgerEnvelope);
			ClassicAssert.AreEqual(KingdomPolityRealmTransitionPhase.Prepared, transition.Phase);
			ClassicAssert.IsTrue(transition.OldImportedWasVisible);
			ClassicAssert.AreNotEqual(Realm, transition.Legacy.LegacyToken);
			ClassicAssert.IsNull(typeof(KingdomPolityRealmTransition).GetField("ActorId"));
		}

		[Test]
		public void ExileRevisionConflictChangesNeitherLedgerNorReceipt()
		{
			KingdomPolityLedger ledger = ActivePublished();
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsFalse(KingdomPolityRules.TryPrepareRealmExile(ledger,
				ledger.Revision + 1L, ExileFacts(), out KingdomPolityRealmTransition transition,
				out KingdomPolityPublicationResult result, out string _));
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Conflict, result.Outcome);
			ClassicAssert.IsNull(transition); CollectionAssert.AreEqual(before,
				KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void NewExilePreparationRefusesUnresolvedOrForeignProfileSeal()
		{
			KingdomPolityLedger ledger = ActivePublished();
			KingdomPolityRealmExileFacts unresolved = ExileFacts();
			unresolved.Legacy.ProfileSchema =
				KingdomPolityProfileRules.UnresolvedLegacyProfileSchema;
			unresolved.Legacy.TechnologyBand = 0;
			unresolved.Legacy.CanonicalBodyKeys.Clear();
			unresolved.Legacy.SourceProfileDigest = null;
			unresolved.Legacy.ProfileProvenanceDigest = null;
			ClassicAssert.IsFalse(KingdomPolityRules.TryPrepareRealmExile(ledger, ledger.Revision,
				unresolved, out KingdomPolityRealmTransition _,
				out KingdomPolityPublicationResult refused, out string failure));
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Refused, refused.Outcome);
			StringAssert.Contains("lacks exact current profile provenance", failure);

			KingdomPolityRealmExileFacts foreign = ExileFacts();
			foreign.Legacy.SourceProfileDigest = KingdomPolityTestData.DigestB;
			foreign.Legacy.ProfileProvenanceDigest =
				KingdomPolityProfileRules.LegacyProfileProvenanceDigest(
					foreign.Legacy.ProfileSchema, foreign.Legacy.TechnologyBand,
					foreign.Legacy.CanonicalBodyKeys, foreign.Legacy.SourceProfileDigest);
			ClassicAssert.IsFalse(KingdomPolityRules.TryPrepareRealmExile(ledger, ledger.Revision,
				foreign, out _, out refused, out failure));
			StringAssert.Contains("lacks exact current profile provenance", failure);
		}

		[Test]
		public void DetachedExileCanRestoreExactOldAuthority()
		{
			KingdomPolityLedger ledger = ActivePublished();
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			KingdomPolityRealmTransition transition = PrepareAndDetach(ledger);
			ClassicAssert.IsFalse(ledger.IdentityBound); ClassicAssert.AreEqual(0, ledger.Polities.Count);
			ClassicAssert.IsTrue(KingdomPolityRules.TryRestoreRealmReturn(ledger, ledger.Revision,
				transition, transition.Revision, Realm, out KingdomPolityPublicationResult result,
				out string failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			ClassicAssert.AreEqual(KingdomPolityRealmTransitionPhase.Restored, transition.Phase);
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
			ClassicAssert.IsTrue(KingdomPolityRules.TryBindIdentity(ledger, nextRealm,
				KingdomPolityImportPolicy.LatestEligible, out string failure), failure);
			KingdomPolityFoundationFacts next = Current();
			next.RealmId = nextRealm; next.FactionId = nextRealm;
			next.SettlementId = nextSettlement; next.FoundedTick = 90L;
			ClassicAssert.IsTrue(KingdomPolityRules.TryGetRealmTransitionLegacy(transition,
				out KingdomPolityLegacySnapshot legacy, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision,
				next, legacy, out KingdomPolityPublicationResult published, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareLegacyFaction(ledger, ledger.Revision, 91L,
				out KingdomPolityPublicationResult prepared, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryCommitLegacyFaction(ledger, ledger.Revision,
				prepared.ProjectionId, 92L, out KingdomPolityPublicationResult _, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryCommitRealmRefound(ledger, transition,
				transition.Revision, out KingdomPolityPublicationResult committed, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityRealmTransitionPhase.Rebound, transition.Phase);
			ClassicAssert.IsNull(transition.ReturnLedgerEnvelope);
			ClassicAssert.AreEqual(nextRealm, transition.ReboundRealmId);
			ClassicAssert.AreNotEqual(Realm, transition.ReboundFactionId);
			ClassicAssert.AreNotEqual(transition.OldImportedFactionId, transition.ReboundFactionId);
			ClassicAssert.AreEqual(published.ImportedPolityId, committed.ImportedPolityId);
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidateRealmTransition(transition,
				out failure), failure);
		}

		[Test]
		public void ExileReceiptDeepCopiesFactsAndQuarantinesCorruptEscrow()
		{
			KingdomPolityLedger ledger = ActivePublished();
			KingdomPolityRealmExileFacts facts = ExileFacts();
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareRealmExile(ledger, ledger.Revision,
				facts, out KingdomPolityRealmTransition transition,
				out KingdomPolityPublicationResult _, out string failure), failure);
			facts.Legacy.RollNames[0] = "changed outside receipt";
			ClassicAssert.AreNotEqual(facts.Legacy.RollNames[0], transition.Legacy.RollNames[0]);
			transition.Legacy.TechnologyBand++;
			transition.Legacy.ProfileProvenanceDigest =
				KingdomPolityProfileRules.LegacyProfileProvenanceDigest(
					transition.Legacy.ProfileSchema, transition.Legacy.TechnologyBand,
					transition.Legacy.CanonicalBodyKeys, transition.Legacy.SourceProfileDigest);
			ClassicAssert.IsFalse(KingdomPolityRules.TryValidateRealmTransition(transition, out failure));
			transition.Legacy.TechnologyBand--;
			transition.Legacy.ProfileProvenanceDigest =
				KingdomPolityProfileRules.LegacyProfileProvenanceDigest(
					transition.Legacy.ProfileSchema, transition.Legacy.TechnologyBand,
					transition.Legacy.CanonicalBodyKeys, transition.Legacy.SourceProfileDigest);
			transition.OldImportedWasVisible = false;
			ClassicAssert.IsFalse(KingdomPolityRules.TryValidateRealmTransition(transition, out failure));
			transition.OldImportedWasVisible = true;
			transition.ReturnLedgerEnvelope[0] ^= 0x1;
			ClassicAssert.IsFalse(KingdomPolityRules.TryValidateRealmTransition(transition, out failure));
			KingdomPolityRules.NormalizeRealmTransition(transition);
			ClassicAssert.AreEqual(KingdomPolityRealmTransitionPhase.Quarantined, transition.Phase);
			ClassicAssert.IsNotNull(transition.Fault);
		}

		[Test]
		public void ReturnAndCompletionRetriesRequireExactAuthority()
		{
			KingdomPolityLedger ledger = ActivePublished();
			KingdomPolityRealmTransition transition = PrepareAndDetach(ledger);
			ClassicAssert.IsTrue(KingdomPolityRules.TryRestoreRealmReturn(ledger, ledger.Revision,
				transition, transition.Revision, Realm, out KingdomPolityPublicationResult first,
				out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryRestoreRealmReturn(ledger, 0L, transition, 0L,
				Realm, out KingdomPolityPublicationResult retry, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			ClassicAssert.IsTrue(KingdomPolityRules.TryCompleteRealmReturn(ledger, transition,
				transition.Revision, Realm, out first, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityRealmTransitionPhase.None, transition.Phase);
			ClassicAssert.IsTrue(KingdomPolityRules.TryCompleteRealmReturn(ledger, transition,
				transition.Revision, Realm, out retry, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);

			KingdomPolityLedger altered = ActivePublished();
			KingdomPolityRealmTransition alteredTransition = PrepareAndDetach(altered);
			ClassicAssert.IsTrue(KingdomPolityRules.TryRestoreRealmReturn(altered, altered.Revision,
				alteredTransition, alteredTransition.Revision, Realm, out first, out failure), failure);
			altered.Revision++;
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(altered, out failure), failure);
			ClassicAssert.IsFalse(KingdomPolityRules.TryRestoreRealmReturn(altered, altered.Revision,
				alteredTransition, alteredTransition.Revision, Realm, out retry, out failure));
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Refused, retry.Outcome);
		}

		[Test]
		public void RefoundRetryRejectsLedgerDifferentFromCommittedReceipt()
		{
			const string nextRealm =
				"taf:realm:v1:3333333333333333333333333333333333333333333333333333333333333333";
			KingdomPolityLedger ledger = ActivePublished();
			KingdomPolityRealmTransition transition = PrepareAndDetach(ledger);
			ClassicAssert.IsTrue(KingdomPolityRules.TryBindIdentity(ledger, nextRealm,
				KingdomPolityImportPolicy.LatestEligible, out string failure), failure);
			KingdomPolityFoundationFacts facts = Current(); facts.RealmId = nextRealm;
			facts.FactionId = nextRealm; facts.SettlementId = Settlement; facts.FoundedTick = 90L;
			ClassicAssert.IsTrue(KingdomPolityRules.TryGetRealmTransitionLegacy(transition,
				out KingdomPolityLegacySnapshot legacy, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision,
				facts, legacy, out KingdomPolityPublicationResult _, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareLegacyFaction(ledger, ledger.Revision, 91L,
				out KingdomPolityPublicationResult prepared, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryCommitLegacyFaction(ledger, ledger.Revision,
				prepared.ProjectionId, 92L, out KingdomPolityPublicationResult _, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryCommitRealmRefound(ledger, transition,
				transition.Revision, out KingdomPolityPublicationResult first, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryCommitRealmRefound(ledger, transition, 0L,
				out KingdomPolityPublicationResult retry, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			ledger.Revision++;
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
			ClassicAssert.IsFalse(KingdomPolityRules.TryCommitRealmRefound(ledger, transition, 0L,
				out retry, out failure));
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Refused, retry.Outcome);
		}

		private static KingdomPolityRealmTransition PrepareAndDetach(KingdomPolityLedger ledger)
		{
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareRealmExile(ledger, ledger.Revision,
				ExileFacts(), out KingdomPolityRealmTransition transition,
				out KingdomPolityPublicationResult _, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryMarkRealmExileTombstoned(ledger, ledger.Revision,
				transition, transition.Revision, out KingdomPolityPublicationResult _, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryDetachRealmExile(ledger, ledger.Revision,
				transition, transition.Revision, out KingdomPolityPublicationResult _, out failure), failure);
			return transition;
		}

		private static KingdomPolityLedger ActivePublished()
		{
			KingdomPolityLedger ledger = Published();
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareLegacyFaction(ledger, ledger.Revision,
				40L, out KingdomPolityPublicationResult prepared, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryCommitLegacyFaction(ledger, ledger.Revision,
				prepared.ProjectionId, 41L, out KingdomPolityPublicationResult _, out failure), failure);
			return ledger;
		}

		private static KingdomPolityRealmExileFacts ExileFacts()
		{
			KingdomPolityLegacySnapshot legacy = Legacy();
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(Current(),
				out KingdomPolityProfileRevision source, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCaptureLegacyProfile(legacy, source,
				out failure), failure);
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
			ClassicAssert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision,
				Current(), Legacy(), out KingdomPolityPublicationResult _, out string failure), failure);
			return ledger;
		}

		private static KingdomPolityLedger Ledger(KingdomPolityImportPolicy Policy)
		{
			ClassicAssert.IsTrue(KingdomPolityRules.TryCreate(Realm, Policy,
				out KingdomPolityLedger ledger, out string failure), failure); return ledger;
		}

		private static KingdomPolityFoundationFacts Current()
		{
			return new KingdomPolityFoundationFacts
			{
				RealmId = Realm, FactionId = Realm, DisplayName = "The Water Compact",
				FounderName = "Ari", SettlementId = Settlement, Vocation = "holding",
				Style = "salt dunes", Creed = "the covenant", Stage = 1, Population = 7,
				TechnologyBand = 2, FoundedTick = 30L, OriginKeys = new List<string> { "human" },
				CultureKeys = new List<string> { "Joppa" },
				SpeciesKeys = new List<string> { "human" }
			};
		}

		private static KingdomPolityLegacySnapshot Legacy()
		{
			KingdomPolityLegacySnapshot result = new KingdomPolityLegacySnapshot
			{
				ProfileSchema = KingdomPolityProfileRules.CurrentLegacyProfileSchema,
				TechnologyBand = 4, CanonicalBodyKeys = new List<string> { "goatfolk" },
				SourceProfileDigest = KingdomPolityTestData.DigestA,
				LegacyToken = "lgc-a-0001", LineageToken = "lin-a-0001",
				FounderName = "Nara", RealmName = "The Returned Brass",
				SettlementName = "Old Ux", Vocation = "foundry", Style = "deep caves",
				Stage = 2, Population = 12, Defence = 5, StoredWater = 100,
				InheritedState = 1, RollNames = new List<string> { "Nara", "Otho" },
				OriginKeys = new List<string> { "goatfolk" }, OriginCounts = new List<int> { 12 },
				CreedKeys = new List<string> { "brass oath" }, CreedCounts = new List<int> { 12 }
			};
			result.ProfileProvenanceDigest =
				KingdomPolityProfileRules.LegacyProfileProvenanceDigest(result.ProfileSchema,
					result.TechnologyBand, result.CanonicalBodyKeys, result.SourceProfileDigest);
			return result;
		}

		private static KingdomPolityRecord Imported(KingdomPolityLedger L)
		{
			for (int i = 0; i < L.Polities.Count; i++)
				if (L.Polities[i].Source == KingdomPolitySource.ImportedLegacy) return L.Polities[i];
			return null;
		}
	}
}
