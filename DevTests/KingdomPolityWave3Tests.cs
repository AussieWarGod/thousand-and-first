using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityWave3Tests
	{
		private const string B =
			"taf:settlement:v1:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
		private const string C =
			"taf:settlement:v1:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";

		[Test]
		public void ImmutableRevisionCasPinsRootsAndCohortProfiles()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			MakeResolverProfile(ledger, KingdomPolityTestData.CurrentProfile);
			KingdomPolityProfileFactSet first = Facts(1, "a", 20L);
			long expected = ledger.Revision;
			Assert.IsTrue(KingdomPolityProfileRules.TryRevise(ledger, expected, first,
				out KingdomPolityPublicationResult applied, out string failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.Applied, applied.Outcome);
			Assert.IsTrue(KingdomPolityProfileRules.TryRevise(ledger, expected, first,
				out KingdomPolityPublicationResult retry, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			KingdomPolityProfileFactSet stable = Facts(2, "a", 25L);
			Assert.IsTrue(KingdomPolityProfileRules.TryRevise(ledger, ledger.Revision, stable,
				out retry, out failure), failure);
			Assert.AreEqual(2, Current(ledger).ProfileRevision,
				"unchanged facts must not mint calendar-only revisions");

			KingdomPolityCohortPlanRequest request = Request("taf:cohort:profile-pin", 2,
				KingdomPolityCohortPurpose.Guard);
			Assert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, request,
				out _, out failure), failure);
			Assert.AreEqual(2, FindCohort(ledger, request.CohortId).ProfileRevision);
			Assert.IsTrue(KingdomPolityProfileRules.TryRevise(ledger, ledger.Revision,
				Facts(2, "b", 30L), out _, out failure), failure);
			Assert.IsTrue(KingdomPolityProfileRules.TryRevise(ledger, ledger.Revision,
				Facts(3, "c", 40L), out _, out failure), failure);
			Assert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, request,
				out KingdomPolityPublicationResult pinnedRetry, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, pinnedRetry.Outcome,
				"retry must not resolve against a newer mutable profile pointer");
			Assert.IsTrue(KingdomPolityRules.TryCompactRetiredProfiles(ledger,
				"taf:compaction:wave3-pins", 50L, out failure), failure);
			Assert.IsNotNull(FindProfile(ledger, KingdomPolityTestData.CurrentProfile, 1));
			Assert.IsNotNull(FindProfile(ledger, KingdomPolityTestData.CurrentProfile, 2));
			Assert.IsNull(FindProfile(ledger, KingdomPolityTestData.CurrentProfile, 3));
			Assert.IsNotNull(FindProfile(ledger, KingdomPolityTestData.CurrentProfile, 4));
		}

		[Test]
		public void RevisionFactsRequireCanonicalTypedConcreteEvidenceAndCas()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			KingdomPolityProfileFactSet invalid = Facts(1, "a", 20L);
			invalid.Facts.Reverse();
			Assert.IsFalse(KingdomPolityProfileRules.TryRevise(ledger, ledger.Revision,
				invalid, out _, out string _));
			invalid = Facts(1, "a", 20L); invalid.Facts[1].Kind =
				KingdomPolityProfileFactKind.None;
			Assert.IsFalse(KingdomPolityProfileRules.TryRevise(ledger, ledger.Revision,
				invalid, out _, out _));
			KingdomPolityProfileFactSet valid = Facts(1, "a", 20L);
			Assert.IsFalse(KingdomPolityProfileRules.TryRevise(ledger, ledger.Revision + 1,
				valid, out KingdomPolityPublicationResult conflict, out _));
			Assert.AreEqual(KingdomPolityCasOutcome.Conflict, conflict.Outcome);
			Assert.AreEqual(1, Current(ledger).ProfileRevision);
		}

		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		public void DispatcherCoversExactTopologyOnceWithoutCatchUp(int endpointCount)
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			KingdomPolityDispatchOffer offer = Offer(endpointCount,
				KingdomPolityDispatchRules.PeriodTicks * 20L);
			Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer,
				out List<KingdomPolityDueWork> work, out string failure), failure);
			Assert.AreEqual(endpointCount, work.Count);
			for (int i = 0; i < work.Count; i++)
			{
				Assert.AreEqual(i, work[i].EndpointOrdinal);
				Assert.IsTrue(KingdomPolityDispatchRules.TryComplete(state,
					work[i].WindowOrdinal, i, out failure), failure);
			}
			Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer,
				out work, out failure), failure); Assert.AreEqual(0, work.Count);
			offer.Tick = KingdomPolityDispatchRules.PeriodTicks * 100L;
			Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer,
				out work, out failure), failure);
			Assert.AreEqual(endpointCount, work.Count, "missed windows must not replay");
			offer.Tick = KingdomPolityDispatchRules.PeriodTicks * 99L;
			Assert.IsFalse(KingdomPolityDispatchRules.TryOpen(state, offer,
				out work, out failure));
		}

		[Test]
		public void ProductionSelectorReachesAllFivePurposesAcrossEligibleWindows()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			KingdomPolityCohortPurpose[] expected =
			{
				KingdomPolityCohortPurpose.Guard,
				KingdomPolityCohortPurpose.Patrol,
				KingdomPolityCohortPurpose.Courier,
				KingdomPolityCohortPurpose.Trader,
				KingdomPolityCohortPurpose.Migrant
			};
			HashSet<KingdomPolityCohortPurpose> seen =
				new HashSet<KingdomPolityCohortPurpose>();
			for (int window = 0; window < expected.Length; window++)
			{
				KingdomPolityDispatchOffer offer = Offer(3,
					KingdomPolityDispatchRules.PeriodTicks * window);
				Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer,
					out List<KingdomPolityDueWork> work, out string failure), failure);
				Assert.AreEqual(3, work.Count);
				Assert.AreEqual(0, work[0].EndpointOrdinal);
				Assert.AreEqual(expected[window], work[0].Purpose,
					"the production selector did not rotate its first endpoint");
				Assert.IsTrue(seen.Add(work[0].Purpose));
				for (int endpoint = 0; endpoint < work.Count; endpoint++)
					Assert.IsTrue(KingdomPolityDispatchRules.TryComplete(state,
						work[endpoint].WindowOrdinal, work[endpoint].EndpointOrdinal,
						out failure), failure);
			}
			CollectionAssert.AreEquivalent(expected, seen);
		}

		[Test]
		public void FiveSchedulersHaveDistinctCausesVerbsAndBoundedMembers()
		{
			KingdomPolityEndpointFacts endpoint = Endpoint(
				KingdomPolityTestData.Settlement, true);
			HashSet<string> sources = new HashSet<string>();
			HashSet<string> verbs = new HashSet<string>();
			for (int raw = (int)KingdomPolityCohortPurpose.Guard;
				raw <= (int)KingdomPolityCohortPurpose.Migrant; raw++)
			{
				KingdomPolityCohortPurpose purpose = (KingdomPolityCohortPurpose)raw;
				if (purpose == KingdomPolityCohortPurpose.Envoy ||
					purpose == KingdomPolityCohortPurpose.Warband) continue;
				Assert.IsTrue(KingdomPolityDispatchRules.TryCreateForPurpose(
					KingdomPolityTestData.Realm, endpoint, 3, 2UL, 16800L, purpose,
					out KingdomPolityDueWork work, out string failure), failure);
				Assert.IsTrue(sources.Add(work.SourceRef)); Assert.IsTrue(verbs.Add(work.EndpointVerb));
				Assert.GreaterOrEqual(work.MemberCount, 1); Assert.LessOrEqual(work.MemberCount, 2);
				Assert.AreEqual(19200L, work.StayUntilTick);
			}
			Assert.AreEqual(5, sources.Count); Assert.AreEqual(5, verbs.Count);
		}

		[Test]
		public void SaveCutAndTerminalPruneCannotRemintSameWindow()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			MakeResolverProfile(ledger, KingdomPolityTestData.CurrentProfile);
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			KingdomPolityDispatchOffer offer = Offer(1, 0L);
			Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer,
				out List<KingdomPolityDueWork> first, out string failure), failure);
			Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer,
				out List<KingdomPolityDueWork> recovered, out failure), failure);
			Assert.AreEqual(first[0].CohortId, recovered[0].CohortId);
			KingdomPolityCohortPlanRequest request = FromDue(first[0]);
			Assert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, request,
				out _, out failure), failure);
			Assert.IsTrue(KingdomPolityDispatchRules.TryComplete(state, 0UL, 0, out failure), failure);
			Assert.IsTrue(KingdomPolityCohortRules.TryCancelExpiredScheduled(ledger,
				ledger.Revision, request.CohortId, KingdomPolityDispatchRules.StayTicks,
				out _, out failure), failure);
			Assert.IsTrue(KingdomPolityCohortRules.TryPruneScheduledTerminals(ledger,
				ledger.Revision, 1UL, out _, out failure), failure);
			Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer,
				out recovered, out failure), failure); Assert.AreEqual(0, recovered.Count);
		}

		[Test]
		public void CorruptDerivedDispatchIsPreservedAndRefused()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState { Version = 99,
				RealmId = "old-actor-or-realm-id", CompletedMask = int.MaxValue };
			Assert.IsFalse(KingdomPolityDispatchRules.TryRecover(state,
				KingdomPolityTestData.Realm, "unsupported dispatch wire", out string failure));
			Assert.AreEqual(99, state.Version); Assert.AreEqual("old-actor-or-realm-id", state.RealmId);
			Assert.AreEqual(int.MaxValue, state.CompletedMask); Assert.IsNull(state.Fault);
		}

		[Test]
		public void MasterResumeReanchorsBothPolityGatesWithoutRewritingFrozenProof()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			Assert.IsTrue(KingdomPolityRules.TryObservePresentation(ledger,
				KingdomPolityPresentationState.Enabled, 20L, out string failure), failure);
			KingdomPolityCohortPlan before = FindCohort(ledger, KingdomPolityTestData.Cohort);
			KingdomExperienceOptionKind option = before.PresentationOptionKind;
			long proofEpoch = before.PresentationEnableEpoch;
			long proofTick = before.PresentationReservedTick;
			long priorEpoch = ledger.Options.EnableEpoch;
			KingdomPolityDispatchState dispatch = new KingdomPolityDispatchState();
			long period = KingdomPolityDispatchRules.PeriodTicks;
			Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(dispatch, Offer(1, period * 5L),
				out List<KingdomPolityDueWork> oldWork, out failure), failure);
			Assert.AreEqual(1, oldWork.Count);
			long resume = period * 5L + 100L;
			Assert.IsTrue(KingdomPolityRules.TryPrepareMasterResume(ledger, dispatch,
				ledger.Revision, KingdomPolityPresentationState.Enabled, resume,
				out KingdomPolityMasterResumePlan plan, out failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryPublishMasterResume(ledger, dispatch, plan,
				out failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryPublishMasterResume(ledger, dispatch, plan,
				out failure), failure);
			Assert.AreEqual(priorEpoch + 1L, ledger.Options.EnableEpoch);
			Assert.AreEqual(resume, ledger.Options.FutureCauseFloorTick);
			Assert.AreEqual(resume, dispatch.FutureCauseFloorTick);
			Assert.IsTrue(dispatch.HasWindow);
			Assert.AreEqual(1, dispatch.CompletedMask);
			Assert.AreEqual(0, dispatch.DirectRecords.Count);
			KingdomPolityCohortPlan after = FindCohort(ledger, KingdomPolityTestData.Cohort);
			Assert.AreEqual(option, after.PresentationOptionKind);
			Assert.AreEqual(proofEpoch, after.PresentationEnableEpoch);
			Assert.AreEqual(proofTick, after.PresentationReservedTick);
			Assert.IsFalse(KingdomPolityRules.CanEmitOptionalProjection(ledger, period * 5L));
			Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(dispatch, Offer(1, resume + 1L),
				out List<KingdomPolityDueWork> skipped, out failure), failure);
			Assert.AreEqual(0, skipped.Count, "resume must not replay the partly elapsed window");
			Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(dispatch, Offer(1, period * 6L),
				out List<KingdomPolityDueWork> next, out failure), failure);
			Assert.AreEqual(1, next.Count); Assert.AreEqual(period * 6L, next[0].CauseTick);
		}

		[Test]
		public void MasterResumeRefusesValidExhaustedDispatchRevision()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			KingdomPolityDispatchState dispatch = new KingdomPolityDispatchState
			{
				RealmId = KingdomPolityTestData.Realm, Revision = long.MaxValue
			};
			Assert.IsTrue(KingdomPolityDispatchRules.ValidState(dispatch, out string failure), failure);
			Assert.IsFalse(KingdomPolityRules.TryPrepareMasterResume(ledger, dispatch,
				ledger.Revision, KingdomPolityPresentationState.Enabled, 100L,
				out KingdomPolityMasterResumePlan _, out failure));
			StringAssert.Contains("revision is exhausted", failure);
		}

		[Test]
		public void MasterResumeCasRefusesDriftWithoutPartialPolityPublication()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			KingdomPolityDispatchState dispatch = new KingdomPolityDispatchState
				{ RealmId = KingdomPolityTestData.Realm };
			Assert.IsTrue(KingdomPolityRules.TryPrepareMasterResume(ledger, dispatch,
				ledger.Revision, KingdomPolityPresentationState.Enabled, 100L,
				out KingdomPolityMasterResumePlan plan, out string failure), failure);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			dispatch.Revision++;
			Assert.IsFalse(KingdomPolityRules.TryPublishMasterResume(ledger, dispatch, plan,
				out failure));
			StringAssert.Contains("staged CAS", failure);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void MasterResumeSeparatesReadOnlyPreflightFromCopyOnlyPublication()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			KingdomPolityDispatchState dispatch = new KingdomPolityDispatchState
				{ RealmId = KingdomPolityTestData.Realm };
			Assert.IsTrue(KingdomPolityRules.TryPrepareMasterResume(ledger, dispatch,
				ledger.Revision, KingdomPolityPresentationState.Enabled, 100L,
				out KingdomPolityMasterResumePlan plan, out string failure), failure);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsTrue(KingdomPolityRules.CanPublishMasterResume(ledger, dispatch,
				plan, out failure), failure);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
			KingdomPolityRules.PublishMasterResumePrevalidated(ledger, dispatch, plan);
			Assert.AreEqual(100L, ledger.Options.FutureCauseFloorTick);
			Assert.AreEqual(100L, dispatch.FutureCauseFloorTick);
			Assert.IsFalse(KingdomPolityRules.CanPublishMasterResume(ledger, dispatch,
				plan, out failure), "source-only preflight must not admit a second write");
		}

		[Test]
		public void MasterResumePreservesAndRefusesInvalidDerivedDispatch()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			KingdomPolityDispatchState dispatch = new KingdomPolityDispatchState
				{ Version = 99, RealmId = "old-realm-or-actor" };
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomPolityRules.TryPrepareMasterResume(ledger, dispatch,
				ledger.Revision, KingdomPolityPresentationState.Enabled, 100L,
				out KingdomPolityMasterResumePlan plan, out string failure));
			Assert.IsNull(plan); CollectionAssert.AreEqual(before,
				KingdomPolityCodec.EncodeEnvelope(ledger));
			Assert.AreEqual(99, dispatch.Version);
			Assert.AreEqual("old-realm-or-actor", dispatch.RealmId);
			Assert.AreEqual(0L, dispatch.FutureCauseFloorTick);
		}

		[Test]
		public void CohortPlanPinsExactPresentationTripleAndRejectsRetryDrift()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			MakeResolverProfile(ledger, KingdomPolityTestData.CurrentProfile);
			KingdomPolityCohortPlanRequest request = Request(
				"taf:cohort:authority-pin", 2, KingdomPolityCohortPurpose.Trader);
			request.PresentationAuthority.EnableEpoch = 7L;
			request.PresentationAuthority.ReservedTick = 123L;
			Assert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, request,
				out KingdomPolityPublicationResult _, out string failure), failure);
			KingdomPolityCohortPlan row = FindCohort(ledger, request.CohortId);
			Assert.AreEqual(KingdomExperienceOptionKind.AmbientUse,
				row.PresentationOptionKind);
			Assert.AreEqual(7L, row.PresentationEnableEpoch);
			Assert.AreEqual(123L, row.PresentationReservedTick);
			request.PresentationAuthority.ReservedTick++;
			Assert.IsFalse(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, request,
				out _, out failure));
		}

		[Test]
		public void RecoveryDispositionSeparatesFrozenLoadedAndLegacyAuthority()
		{
			KingdomPolityCohortPlan row = new KingdomPolityCohortPlan
			{
				SurfaceRef = KingdomPolityTestData.Settlement,
				Phase = KingdomPolityCohortPhase.Planned,
				PresentationOptionKind = KingdomExperienceOptionKind.AmbientUse,
				PresentationEnableEpoch = 1L, PresentationReservedTick = 10L
			};
			Assert.AreEqual(KingdomPolityLeaseRecoveryAction.EnsureCurrentPlan,
				KingdomPolityExperienceRecoveryRules.Decide(row, null, true));
			Assert.AreEqual(KingdomPolityLeaseRecoveryAction.CancelUnpresented,
				KingdomPolityExperienceRecoveryRules.Decide(row, null, false));
			row.ManifestationReceiptId = "taf:projection:recovery-disposition";
			row.Phase = KingdomPolityCohortPhase.Materialized;
			Assert.AreEqual(KingdomPolityLeaseRecoveryAction.EnsureThenRetainFrozen,
				KingdomPolityExperienceRecoveryRules.Decide(row, null, false));
			Assert.AreEqual(KingdomPolityLeaseRecoveryAction.EnsureThenWithdrawLoaded,
				KingdomPolityExperienceRecoveryRules.Decide(row, row.SurfaceRef, false));
			row.Phase = KingdomPolityCohortPhase.Concluded;
			Assert.AreEqual(KingdomPolityLeaseRecoveryAction.EnsureThenCleanupLoaded,
				KingdomPolityExperienceRecoveryRules.Decide(row, row.SurfaceRef, false));
			row.ManifestationReceiptId = null; row.Phase = KingdomPolityCohortPhase.Planned;
			row.PresentationOptionKind = KingdomExperienceOptionKind.None;
			Assert.AreEqual(KingdomPolityLeaseRecoveryAction.CancelUnpresented,
				KingdomPolityExperienceRecoveryRules.Decide(row, null, true));
			row.ManifestationReceiptId = "taf:projection:legacy-ambiguous";
			row.Phase = KingdomPolityCohortPhase.Cleaned;
			Assert.AreEqual(KingdomPolityLeaseRecoveryAction.Invalid,
				KingdomPolityExperienceRecoveryRules.Decide(row, row.SurfaceRef, false));
		}

		[Test]
		public void FrozenRetirementReconstructsExactProofAndStillConsumesSharedCap()
		{
			const string realm = "taf:realm:polity-wave3-retirement";
			const string settlement = "taf:settlement:polity-wave3-retirement";
			const string source = "taf:cohort:polity-wave3-retirement";
			KingdomExperienceLedger experience = new KingdomExperienceLedger();
			Assert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(experience, realm,
				out string failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(experience,
				experience.Revision, true, true, true, 10L, out failure), failure);
			KingdomExperienceAudienceReceipt audience = ExperienceAudience(realm, settlement,
				"retirement", source, KingdomExperienceOptionKind.AmbientUse);
			KingdomExperienceBodyReservation bodies = ExperienceBody(realm, settlement,
				"retirement", source, 2, KingdomExperienceOptionKind.AmbientUse);
			Assert.IsTrue(KingdomExperienceRules.TryReservePresentation(experience,
				experience.Revision, audience, bodies, 0, out _, out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(experience,
				experience.Revision, true, true, false, 20L, out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryReleasePresentation(experience,
				experience.Revision, audience.ReservationId, bodies.ReservationId, source,
				out _, out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryRecoverDurablePresentation(experience,
				experience.Revision, audience, bodies, 0, out _, out failure), failure);
			byte[] recovered = KingdomExperienceCodec.EncodeEnvelope(experience);
			Assert.IsTrue(KingdomExperienceRules.TryRecoverDurablePresentation(experience,
				0L, audience, bodies, 0, out _, out failure), failure);
			CollectionAssert.AreEqual(recovered, KingdomExperienceCodec.EncodeEnvelope(experience));
			KingdomPolityCohortPlan frozen = new KingdomPolityCohortPlan
			{
				SurfaceRef = settlement, Phase = KingdomPolityCohortPhase.Materialized,
				ManifestationReceiptId = "taf:projection:polity-wave3-retirement",
				PresentationOptionKind = KingdomExperienceOptionKind.AmbientUse,
				PresentationEnableEpoch = 1L, PresentationReservedTick = 10L
			};
			Assert.AreEqual(KingdomPolityLeaseRecoveryAction.EnsureThenRetainFrozen,
				KingdomPolityExperienceRecoveryRules.Decide(frozen, null, false));
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(experience,
				experience.Revision, true, true, true, 30L, out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryReadBodyLease(experience,
				bodies.ReservationId, out _, out KingdomExperienceLeaseState state, out failure), failure);
			Assert.AreEqual(KingdomExperienceLeaseState.Retirement, state);
			Assert.IsTrue(KingdomExperienceRules.TryClassifyLeaseProof(experience,
				frozen.PresentationOptionKind, bodies.CauseTick,
				frozen.PresentationReservedTick, frozen.PresentationEnableEpoch,
				out state, out failure), failure);
			Assert.AreEqual(KingdomExperienceLeaseState.Retirement, state);
			Assert.IsTrue(KingdomExperienceRules.TryReserveBodies(experience, experience.Revision,
				ExperienceBody(realm, settlement, "fill-a", "taf:cohort:fill-a", 7,
					KingdomExperienceOptionKind.CivicStory), 0, out _, out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryReserveBodies(experience, experience.Revision,
				ExperienceBody(realm, settlement, "fill-b", "taf:cohort:fill-b", 7,
					KingdomExperienceOptionKind.CivicStory), 0, out _, out failure), failure);
			Assert.AreEqual(16, KingdomExperienceRules.ReservedBodies(experience));
			Assert.IsFalse(KingdomExperienceRules.TryReserveBodies(experience,
				experience.Revision, ExperienceBody(realm, settlement, "cap-plus-one-retired",
					"taf:cohort:cap-plus-one-retired", 1,
					KingdomExperienceOptionKind.CivicStory), 0,
				out KingdomExperienceCapacityFault fault, out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.LiveBodyCapacityFull, fault);
		}

		[Test]
		public void SharedBudgetIncludesLegacyAndNewPurposes()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			MakeResolverProfile(ledger, KingdomPolityTestData.RivalProfile);
			Assert.IsTrue(KingdomPolityAttentionRules.TryAdmitPlan(ledger, 5,
				out string failure), failure);
			Assert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision,
				Request("taf:cohort:budget-warband", 5, KingdomPolityCohortPurpose.Warband,
					KingdomPolityTestData.Rival), out _, out failure), failure);
			Assert.IsFalse(KingdomPolityAttentionRules.TryAdmitPlan(ledger, 1, out failure));
			StringAssert.Contains("shared polity", failure);
		}

		[Test]
		public void AmbientAndDirectedModesShareBodyCapWithoutSharingAudience()
		{
			const string realm = "taf:realm:polity-wave3-budget";
			const string settlement = "taf:settlement:polity-wave3-budget";
			KingdomExperienceLedger experience = new KingdomExperienceLedger();
			Assert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(experience, realm,
				out string failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(experience,
				experience.Revision, true, true, true, 10L, out failure), failure);
			KingdomExperienceAudienceReceipt ambientAudience = new KingdomExperienceAudienceReceipt
			{
				ReservationId = "taf:experience-audience:polity-wave3-ambient",
				RealmId = realm, SettlementId = settlement,
				SourceId = "taf:cohort:polity-wave3-ambient",
				Lane = KingdomExperienceLane.PolityCohort,
				OptionKind = KingdomExperienceOptionKind.AmbientUse,
				CauseTick = 10L, ReservedTick = 10L, EnableEpoch = 1L
			};
			KingdomExperienceBodyReservation ambient = ExperienceBody(realm, settlement,
				"ambient", "taf:cohort:polity-wave3-ambient", 7,
				KingdomExperienceOptionKind.AmbientUse);
			Assert.IsTrue(KingdomExperienceRules.TryReservePresentation(experience,
				experience.Revision, ambientAudience, ambient, 0,
				out KingdomExperienceCapacityFault _, out failure), failure);
			KingdomExperienceBodyReservation directed = ExperienceBody(realm, settlement,
				"directed", "taf:cohort:polity-wave3-directed", 7,
				KingdomExperienceOptionKind.CivicStory);
			Assert.IsTrue(KingdomExperienceRules.TryReserveBodies(experience,
				experience.Revision, directed, 0, out _, out failure), failure);
			Assert.AreEqual(1, experience.Audiences.Count,
				"directed conversation/threat must not consume unsolicited audience capacity");
			Assert.IsTrue(KingdomExperienceRules.TryReserveBodies(experience,
				experience.Revision, ExperienceBody(realm, settlement, "other",
					"taf:event:polity-wave3-other", 2,
					KingdomExperienceOptionKind.CivicStory,
					KingdomExperienceLane.CivicVoices), 0, out _, out failure), failure);
			Assert.AreEqual(16, KingdomExperienceRules.ReservedBodies(experience));
			byte[] atCap = KingdomExperienceCodec.EncodeEnvelope(experience);
			Assert.IsFalse(KingdomExperienceRules.TryReserveBodies(experience,
				experience.Revision, ExperienceBody(realm, settlement, "cap-plus-one",
					"taf:event:polity-wave3-cap-plus-one", 1,
					KingdomExperienceOptionKind.CivicStory,
					KingdomExperienceLane.FirstGuest), 0,
				out KingdomExperienceCapacityFault fault, out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.LiveBodyCapacityFull, fault);
			CollectionAssert.AreEqual(atCap, KingdomExperienceCodec.EncodeEnvelope(experience));
		}

		[Test]
		public void ExactUnpresentedCancellationReleasesLocalAttentionWithoutBacklog()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			MakeResolverProfile(ledger, KingdomPolityTestData.CurrentProfile);
			KingdomPolityCohortPlanRequest request = Request(
				"taf:cohort:polity-wave3-lapsed", 7, KingdomPolityCohortPurpose.Guard);
			Assert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, request,
				out _, out string failure), failure);
			Assert.IsFalse(KingdomPolityAttentionRules.TryAdmitPlan(ledger, 1, out failure));
			const string cancellation = "taf:event:polity-presentation-lapse:test";
			Assert.IsTrue(KingdomPolityCohortRules.TryCancelUnpresented(ledger,
				ledger.Revision, request.CohortId, cancellation,
				out KingdomPolityPublicationResult result, out failure), failure);
			Assert.IsTrue(KingdomPolityCohortRules.TryCancelUnpresented(ledger, 0L,
				request.CohortId, cancellation, out result, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			Assert.IsTrue(KingdomPolityAttentionRules.TryAdmitPlan(ledger, 1, out failure), failure);
		}

		[Test]
		public void CivicOfficeIsTitleOnlyWhileDeedPromotionRequiresExactEvidence()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			MakeResolverProfile(ledger, KingdomPolityTestData.CurrentProfile);
			KingdomPolityFigurePromotionFacts first = Promotion(18, "Iri", "taf:fact:office:first");
			Assert.IsFalse(KingdomPolityRules.TryPromoteNamedFigure(ledger, ledger.Revision,
				first, out KingdomPolityPublicationResult _, out string failure),
				"civic title cannot publish polity rank, role, profile, or gear eligibility");
			first.Origin = KingdomPolityFigureOrigin.PromotedByDeed;
			first.RoleKey = "guard";
			Assert.IsFalse(KingdomPolityRules.TryPromoteNamedFigure(ledger, ledger.Revision,
				first, out _, out failure), "deed promotion cannot borrow office evidence");
			first.CauseRef = "taf:fact:deed:first";
			Assert.IsTrue(KingdomPolityRules.TryPromoteNamedFigure(ledger, ledger.Revision,
				first, out KingdomPolityPublicationResult result, out failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryPromoteNamedFigure(ledger, ledger.Revision,
				first, out result, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			KingdomPolityNamedFigureRecord legacyOffice = new KingdomPolityNamedFigureRecord
			{
				FigureId = "taf:figure:legacy-office-title", PolityId = KingdomPolityTestData.Realm,
				DisplayName = "Ara", RoleKey = "guard",
				Origin = KingdomPolityFigureOrigin.Officeholder,
				Phase = KingdomPolityFigurePhase.Active, CauseRef = "taf:fact:office:legacy"
			};
			ledger.NamedFigures.Add(legacyOffice);
			ledger.NamedFigures.Sort((a, b) => string.CompareOrdinal(a.FigureId, b.FigureId));
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
			KingdomPolityCohortPlanRequest guard = Request(
				"taf:cohort:legacy-office-cannot-guard", 1, KingdomPolityCohortPurpose.Guard);
			guard.NamedFigureId = legacyOffice.FigureId;
			Assert.IsFalse(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision,
				guard, out _, out failure), "old office rows cannot imply combat capability");
		}

		[Test]
		public void LegacyOfficeRetirementIsGlobalIdempotentAndFreesAttention()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			ledger.NamedFigures.Add(LegacyOffice("current-stale", KingdomPolityTestData.Realm,
				41, "taf:settlement:v1:stale-office-bridge"));
			ledger.NamedFigures.Add(LegacyOffice("external-no-bridge",
				KingdomPolityTestData.Rival, 0, null));
			ledger.NamedFigures.Sort((a, b) => string.CompareOrdinal(a.FigureId, b.FigureId));
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out string failure), failure);
			Assert.AreEqual(2, ActiveOffices(ledger));
			Assert.AreEqual(2, KingdomPolityAttentionRules.ActiveNamedFigures(ledger,
				KingdomPolityTestData.Realm));

			const string cause = "taf:fact:office-retirement:v1:test-global";
			Assert.IsTrue(KingdomPolityRules.TryRetireAllOfficeFigures(ledger,
				ledger.Revision, cause, out KingdomPolityPublicationResult result,
				out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			Assert.AreEqual(0, ActiveOffices(ledger));
			Assert.AreEqual(1, KingdomPolityAttentionRules.ActiveNamedFigures(ledger,
				KingdomPolityTestData.Realm));
			for (int i = 0; i < ledger.NamedFigures.Count; i++)
				if (ledger.NamedFigures[i].Origin == KingdomPolityFigureOrigin.Officeholder)
				{
					Assert.AreEqual(KingdomPolityFigurePhase.Transferred,
						ledger.NamedFigures[i].Phase);
					Assert.AreEqual(0, ledger.NamedFigures[i].ResidentId);
					Assert.IsNull(ledger.NamedFigures[i].ResidentSettlementId);
					StringAssert.StartsWith("taf:conclusion:office:v1:",
						ledger.NamedFigures[i].ConclusionRef);
				}
			for (int i = 0; i < 3; i++)
			{
				KingdomPolityFigurePromotionFacts deed = Promotion(60 + i, "Deed " + i,
					"taf:fact:deed:post-office-" + i);
				deed.Origin = KingdomPolityFigureOrigin.PromotedByDeed;
				deed.RoleKey = "courier";
				Assert.IsTrue(KingdomPolityRules.TryPromoteNamedFigure(ledger,
					ledger.Revision, deed, out _, out failure), failure);
			}
			Assert.AreEqual(KingdomPolityAttentionRules.MaximumActiveNamedFigures,
				KingdomPolityAttentionRules.ActiveNamedFigures(ledger,
					KingdomPolityTestData.Realm));
			byte[] stable = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsTrue(KingdomPolityRules.TryRetireAllOfficeFigures(ledger, 0L, cause,
				out result, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			CollectionAssert.AreEqual(stable, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		private static KingdomPolityNamedFigureRecord LegacyOffice(string suffix,
			string polity, int resident, string settlement)
		{
			return new KingdomPolityNamedFigureRecord
			{
				FigureId = "taf:figure:legacy-office-" + suffix, PolityId = polity,
				DisplayName = "Legacy " + suffix, RoleKey = "guard",
				Origin = KingdomPolityFigureOrigin.Officeholder,
				Phase = KingdomPolityFigurePhase.Active,
				CauseRef = "taf:fact:office:legacy-" + suffix,
				ResidentId = resident, ResidentSettlementId = settlement
			};
		}

		private static int ActiveOffices(KingdomPolityLedger ledger)
		{
			int result = 0;
			for (int i = 0; i < ledger.NamedFigures.Count; i++)
				if (ledger.NamedFigures[i].Origin == KingdomPolityFigureOrigin.Officeholder &&
					ledger.NamedFigures[i].Phase == KingdomPolityFigurePhase.Active) result++;
			return result;
		}

		private static KingdomPolityProfileFactSet Facts(int previous, string token, long tick)
		{
			return new KingdomPolityProfileFactSet
			{
				PolityId = KingdomPolityTestData.Realm,
				ProfileId = KingdomPolityTestData.CurrentProfile, PreviousRevision = previous,
				EffectiveTick = tick, TechnologyBand = 3,
				Facts = new List<KingdomPolityProfileFact>
				{
					new KingdomPolityProfileFact { FactId = "taf:fact:profile:a-" + token,
						Kind = KingdomPolityProfileFactKind.Decision, ValueKey = "gate=" + token,
						SourceRef = KingdomPolityTestData.Settlement },
					new KingdomPolityProfileFact { FactId = "taf:fact:profile:z-" + token,
						Kind = KingdomPolityProfileFactKind.Technology, ValueKey = "band=3",
						SourceRef = KingdomPolityTestData.Settlement }
				}
			};
		}

		private static KingdomPolityDispatchOffer Offer(int count, long tick)
		{
			List<KingdomPolityEndpointFacts> rows = new List<KingdomPolityEndpointFacts>
				{ Endpoint(KingdomPolityTestData.Settlement, true) };
			if (count > 1) rows.Add(Endpoint(B, false)); if (count > 2) rows.Add(Endpoint(C, false));
			return new KingdomPolityDispatchOffer { RealmId = KingdomPolityTestData.Realm,
				Tick = tick, Endpoints = rows };
		}

		private static KingdomPolityEndpointFacts Endpoint(string id, bool seat)
		{
			return new KingdomPolityEndpointFacts { SettlementId = id, IsSeat = seat,
				Population = 4, Stage = 4, ShopTier = 8, KnownStorageSpace = 10,
				GuardCauseRef = "taf:fact:watch:" + id, PatrolCauseRef = "taf:fact:patrol:" + id,
				CourierCauseRef = "taf:fact:courier:" + id, TraderCauseRef = "taf:fact:market:" + id,
				MigrantCauseRef = "taf:fact:room:" + id };
		}

		private static KingdomPolityCohortPlanRequest FromDue(KingdomPolityDueWork work)
		{
			return new KingdomPolityCohortPlanRequest { CohortId = work.CohortId,
				Purpose = work.Purpose, SourceRef = work.SourceRef,
				PolityId = KingdomPolityTestData.Realm, SurfaceRef = work.SettlementId,
				MemberCount = work.MemberCount, EventStreamId = work.EventStreamId,
				RulesVersion = KingdomPolityNpcRules.RulesVersion, EventOrdinal = work.WindowOrdinal,
				PresentationAuthority = Authority(work.Purpose, work.CauseTick) };
		}

		private static KingdomPolityCohortPlanRequest Request(string id, int members,
			KingdomPolityCohortPurpose purpose, string polity = KingdomPolityTestData.Realm)
		{
			return new KingdomPolityCohortPlanRequest { CohortId = id, Purpose = purpose,
				SourceRef = "taf:event:" + id, PolityId = polity,
				SurfaceRef = KingdomPolityTestData.Settlement, MemberCount = members,
				EventStreamId = "taf:stream:" + id, RulesVersion = 1, EventOrdinal = 2UL,
				PresentationAuthority = Authority(purpose, 10L) };
		}

		private static KingdomPolityPresentationAuthorityProof Authority(
			KingdomPolityCohortPurpose purpose, long reserved)
		{
			return new KingdomPolityPresentationAuthorityProof
			{
				OptionKind = purpose == KingdomPolityCohortPurpose.Envoy ||
					purpose == KingdomPolityCohortPurpose.Warband
						? KingdomExperienceOptionKind.CivicStory
						: KingdomExperienceOptionKind.AmbientUse,
				EnableEpoch = 1L, ReservedTick = reserved
			};
		}

		private static void MakeResolverProfile(KingdomPolityLedger ledger, string id)
		{
			KingdomPolityProfileRevision p = FindProfile(ledger, id, 1);
			p.BodyKeys = new List<string> { "human" };
			p.RoleKeys = new List<string> { "claimant", "cook", "courier", "envoy", "guard",
				"migrant", "namesake", "patrol", "successor", "trader", "warband" };
		}

		private static KingdomPolityProfileRevision FindProfile(KingdomPolityLedger l,
			string id, int revision)
		{
			for (int i = 0; i < l.Profiles.Count; i++) if (l.Profiles[i].ProfileId == id &&
				l.Profiles[i].Revision == revision) return l.Profiles[i]; return null;
		}

		private static KingdomPolityCohortPlan FindCohort(KingdomPolityLedger l, string id)
		{
			for (int i = 0; i < l.Cohorts.Count; i++) if (l.Cohorts[i].CohortId == id)
				return l.Cohorts[i]; return null;
		}

		private static KingdomPolityRecord Current(KingdomPolityLedger l)
		{
			for (int i = 0; i < l.Polities.Count; i++) if (l.Polities[i].Source ==
				KingdomPolitySource.CurrentRealm) return l.Polities[i]; return null;
		}

		private static KingdomPolityFigurePromotionFacts Promotion(int resident, string name,
			string cause)
		{
			return new KingdomPolityFigurePromotionFacts { PolityId = KingdomPolityTestData.Realm,
				SettlementId = KingdomPolityTestData.Settlement, ResidentId = resident,
				DisplayName = name, RoleKey = "officeholder",
				Origin = KingdomPolityFigureOrigin.Officeholder,
				CauseRef = cause };
		}

		private static KingdomExperienceBodyReservation ExperienceBody(string realm,
			string settlement, string suffix, string source, int count,
			KingdomExperienceOptionKind option,
			KingdomExperienceLane lane = KingdomExperienceLane.PolityCohort)
		{
			return new KingdomExperienceBodyReservation
			{
				ReservationId = "taf:experience-body:polity-wave3-" + suffix,
				RealmId = realm, SettlementId = settlement, SourceId = source,
				Lane = lane, OptionKind = option,
				CauseTick = 10L, ReservedTick = 10L, EnableEpoch = 1L, BodyCount = count
			};
		}

		private static KingdomExperienceAudienceReceipt ExperienceAudience(string realm,
			string settlement, string suffix, string source,
			KingdomExperienceOptionKind option)
		{
			return new KingdomExperienceAudienceReceipt
			{
				ReservationId = "taf:experience-audience:polity-wave3-" + suffix,
				RealmId = realm, SettlementId = settlement, SourceId = source,
				Lane = KingdomExperienceLane.PolityCohort, OptionKind = option,
				CauseTick = 10L, ReservedTick = 10L, EnableEpoch = 1L
			};
		}
	}
}
