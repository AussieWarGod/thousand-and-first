using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryRevise(ledger, expected, first,
				out KingdomPolityPublicationResult applied, out string failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Applied, applied.Outcome);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryRevise(ledger, expected, first,
				out KingdomPolityPublicationResult retry, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			KingdomPolityProfileFactSet stable = Facts(2, "a", 25L);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryRevise(ledger, ledger.Revision, stable,
				out retry, out failure), failure);
			ClassicAssert.AreEqual(2, Current(ledger).ProfileRevision,
				"unchanged facts must not mint calendar-only revisions");

			KingdomPolityCohortPlanRequest request = Request(ledger, "taf:cohort:profile-pin", 2,
				KingdomPolityCohortPurpose.Guard);
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, request,
				out _, out failure), failure);
			ClassicAssert.AreEqual(2, FindCohort(ledger, request.CohortId).ProfileRevision);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryRevise(ledger, ledger.Revision,
				Facts(2, "b", 30L), out _, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryRevise(ledger, ledger.Revision,
				Facts(3, "c", 40L), out _, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, request,
				out KingdomPolityPublicationResult pinnedRetry, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, pinnedRetry.Outcome,
				"retry must not resolve against a newer mutable profile pointer");
			ClassicAssert.IsTrue(KingdomPolityRules.TryCompactRetiredProfiles(ledger,
				"taf:compaction:wave3-pins", 50L, out failure), failure);
			ClassicAssert.IsNotNull(FindProfile(ledger, KingdomPolityTestData.CurrentProfile, 1));
			ClassicAssert.IsNotNull(FindProfile(ledger, KingdomPolityTestData.CurrentProfile, 2));
			ClassicAssert.IsNull(FindProfile(ledger, KingdomPolityTestData.CurrentProfile, 3));
			ClassicAssert.IsNotNull(FindProfile(ledger, KingdomPolityTestData.CurrentProfile, 4));
		}

		[Test]
		public void RevisionFactsRequireCanonicalTypedConcreteEvidenceAndCas()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			KingdomPolityProfileFactSet invalid = Facts(1, "a", 20L);
			invalid.Facts.Reverse();
			ClassicAssert.IsFalse(KingdomPolityProfileRules.TryRevise(ledger, ledger.Revision,
				invalid, out _, out string _));
			invalid = Facts(1, "a", 20L); invalid.Facts[1].Kind =
				KingdomPolityProfileFactKind.None;
			ClassicAssert.IsFalse(KingdomPolityProfileRules.TryRevise(ledger, ledger.Revision,
				invalid, out _, out _));
			KingdomPolityProfileFactSet valid = Facts(1, "a", 20L);
			ClassicAssert.IsFalse(KingdomPolityProfileRules.TryRevise(ledger, ledger.Revision + 1,
				valid, out KingdomPolityPublicationResult conflict, out _));
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Conflict, conflict.Outcome);
			ClassicAssert.AreEqual(1, Current(ledger).ProfileRevision);
		}

		[TestCase("population=none", "none")]
		[TestCase("population=unresolved", "unresolved")]
		public void PopulationFactsReplaceBodyPoolAndUnresolvedBodiesRefuseCohorts(
			string terminalPopulation, string token)
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			MakeResolverProfile(ledger, KingdomPolityTestData.CurrentProfile);
			KingdomPolityProfileFactSet manifested = FactsWithPopulation(
				1, "goatfolk-" + token, 20L, "body=goatfolk");
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryRevise(ledger, ledger.Revision,
				manifested, out _, out string failure), failure);
			KingdomPolityProfileRevision replaced = FindProfile(ledger,
				KingdomPolityTestData.CurrentProfile, 2);
			CollectionAssert.AreEqual(new[] { "goatfolk" }, replaced.BodyKeys,
				"observed population must replace, not merge with, the prior body pool");

			KingdomPolityProfileFactSet unmanifested = FactsWithPopulation(
				2, token, 30L, terminalPopulation);
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryRevise(ledger, ledger.Revision,
				unmanifested, out _, out failure), failure);
			KingdomPolityProfileRevision unresolved = FindProfile(ledger,
				KingdomPolityTestData.CurrentProfile, 3);
			CollectionAssert.AreEqual(new[] { "unresolved" }, unresolved.BodyKeys);

			KingdomPolityCohortPlanRequest request = Request(ledger,
				"taf:cohort:unresolved-population-" + token, 1,
				KingdomPolityCohortPurpose.Guard);
			ClassicAssert.IsFalse(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision,
				request, out _, out failure));
			StringAssert.Contains("no admissible manifested body", failure);
			ClassicAssert.IsNull(FindCohort(ledger, request.CohortId),
				"resolver refusal must not publish a partial cohort");
		}

		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		public void DispatcherCoversExactTopologyOnceWithoutCatchUp(int endpointCount)
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			KingdomPolityDispatchOffer offer = Offer(endpointCount,
				KingdomPolityDispatchRules.PeriodTicks * 20L);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer,
				out List<KingdomPolityDueWork> work, out string failure), failure);
			ClassicAssert.AreEqual(endpointCount, work.Count);
			for (int i = 0; i < work.Count; i++)
			{
				ClassicAssert.AreEqual(i, work[i].EndpointOrdinal);
				ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryComplete(state,
					work[i].WindowOrdinal, i, out failure), failure);
			}
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer,
				out work, out failure), failure); ClassicAssert.AreEqual(0, work.Count);
			offer.Tick = KingdomPolityDispatchRules.PeriodTicks * 100L;
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer,
				out work, out failure), failure);
			ClassicAssert.AreEqual(endpointCount, work.Count, "missed windows must not replay");
			offer.Tick = KingdomPolityDispatchRules.PeriodTicks * 99L;
			ClassicAssert.IsFalse(KingdomPolityDispatchRules.TryOpen(state, offer,
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
				ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer,
					out List<KingdomPolityDueWork> work, out string failure), failure);
				ClassicAssert.AreEqual(3, work.Count);
				ClassicAssert.AreEqual(0, work[0].EndpointOrdinal);
				ClassicAssert.AreEqual(expected[window], work[0].Purpose,
					"the production selector did not rotate its first endpoint");
				ClassicAssert.IsTrue(seen.Add(work[0].Purpose));
				for (int endpoint = 0; endpoint < work.Count; endpoint++)
					ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryComplete(state,
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
				ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryCreateForPurpose(
					KingdomPolityTestData.Realm, endpoint, 3, 2UL, 16800L, purpose,
					out KingdomPolityDueWork work, out string failure), failure);
				ClassicAssert.IsTrue(sources.Add(work.SourceRef)); ClassicAssert.IsTrue(verbs.Add(work.EndpointVerb));
				ClassicAssert.GreaterOrEqual(work.MemberCount, 1); ClassicAssert.LessOrEqual(work.MemberCount, 2);
				ClassicAssert.AreEqual(19200L, work.StayUntilTick);
			}
			ClassicAssert.AreEqual(5, sources.Count); ClassicAssert.AreEqual(5, verbs.Count);
		}

		[Test]
		public void SaveCutAndTerminalPruneCannotRemintSameWindow()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			MakeResolverProfile(ledger, KingdomPolityTestData.CurrentProfile);
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			KingdomPolityDispatchOffer offer = Offer(1, 0L);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer,
				out List<KingdomPolityDueWork> first, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer,
				out List<KingdomPolityDueWork> recovered, out failure), failure);
			ClassicAssert.AreEqual(first[0].CohortId, recovered[0].CohortId);
			KingdomPolityCohortPlanRequest request = FromDue(ledger, first[0]);
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, request,
				out _, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryComplete(state, 0UL, 0, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryCancelExpiredScheduled(ledger,
				ledger.Revision, request.CohortId, KingdomPolityDispatchRules.StayTicks,
				out _, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryPruneScheduledTerminals(ledger,
				ledger.Revision, 1UL, out _, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, offer,
				out recovered, out failure), failure); ClassicAssert.AreEqual(0, recovered.Count);
		}

		[Test]
		public void CorruptDerivedDispatchIsPreservedAndRefused()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState { Version = 99,
				RealmId = "old-actor-or-realm-id", CompletedMask = int.MaxValue };
			ClassicAssert.IsFalse(KingdomPolityDispatchRules.TryRecover(state,
				KingdomPolityTestData.Realm, "unsupported dispatch wire", out string failure));
			ClassicAssert.AreEqual(99, state.Version); ClassicAssert.AreEqual("old-actor-or-realm-id", state.RealmId);
			ClassicAssert.AreEqual(int.MaxValue, state.CompletedMask); ClassicAssert.IsNull(state.Fault);
		}

		[Test]
		public void MasterResumeReanchorsBothPolityGatesWithoutRewritingFrozenProof()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			ClassicAssert.IsTrue(KingdomPolityRules.TryObservePresentation(ledger,
				KingdomPolityPresentationState.Enabled, 20L, out string failure), failure);
			KingdomPolityCohortPlan before = FindCohort(ledger, KingdomPolityTestData.Cohort);
			KingdomExperienceOptionKind option = before.PresentationOptionKind;
			long proofEpoch = before.PresentationEnableEpoch;
			long proofTick = before.PresentationReservedTick;
			long priorEpoch = ledger.Options.EnableEpoch;
			KingdomPolityDispatchState dispatch = new KingdomPolityDispatchState();
			long period = KingdomPolityDispatchRules.PeriodTicks;
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(dispatch, Offer(1, period * 5L),
				out List<KingdomPolityDueWork> oldWork, out failure), failure);
			ClassicAssert.AreEqual(1, oldWork.Count);
			long resume = period * 5L + 100L;
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareMasterResume(ledger, dispatch,
				ledger.Revision, KingdomPolityPresentationState.Enabled, resume,
				out KingdomPolityMasterResumePlan plan, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPublishMasterResume(ledger, dispatch, plan,
				out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPublishMasterResume(ledger, dispatch, plan,
				out failure), failure);
			ClassicAssert.AreEqual(priorEpoch + 1L, ledger.Options.EnableEpoch);
			ClassicAssert.AreEqual(resume, ledger.Options.FutureCauseFloorTick);
			ClassicAssert.AreEqual(resume, dispatch.FutureCauseFloorTick);
			ClassicAssert.IsTrue(dispatch.HasWindow);
			ClassicAssert.AreEqual(1, dispatch.CompletedMask);
			ClassicAssert.AreEqual(0, dispatch.DirectRecords.Count);
			KingdomPolityCohortPlan after = FindCohort(ledger, KingdomPolityTestData.Cohort);
			ClassicAssert.AreEqual(option, after.PresentationOptionKind);
			ClassicAssert.AreEqual(proofEpoch, after.PresentationEnableEpoch);
			ClassicAssert.AreEqual(proofTick, after.PresentationReservedTick);
			ClassicAssert.IsFalse(KingdomPolityRules.CanEmitOptionalProjection(ledger, period * 5L));
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(dispatch, Offer(1, resume + 1L),
				out List<KingdomPolityDueWork> skipped, out failure), failure);
			ClassicAssert.AreEqual(0, skipped.Count, "resume must not replay the partly elapsed window");
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(dispatch, Offer(1, period * 6L),
				out List<KingdomPolityDueWork> next, out failure), failure);
			ClassicAssert.AreEqual(1, next.Count); ClassicAssert.AreEqual(period * 6L, next[0].CauseTick);
		}

		[Test]
		public void MasterResumeRefusesValidExhaustedDispatchRevision()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			KingdomPolityDispatchState dispatch = new KingdomPolityDispatchState
			{
				RealmId = KingdomPolityTestData.Realm, Revision = long.MaxValue
			};
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.ValidState(dispatch, out string failure), failure);
			ClassicAssert.IsFalse(KingdomPolityRules.TryPrepareMasterResume(ledger, dispatch,
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
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareMasterResume(ledger, dispatch,
				ledger.Revision, KingdomPolityPresentationState.Enabled, 100L,
				out KingdomPolityMasterResumePlan plan, out string failure), failure);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			dispatch.Revision++;
			ClassicAssert.IsFalse(KingdomPolityRules.TryPublishMasterResume(ledger, dispatch, plan,
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
			ClassicAssert.IsTrue(KingdomPolityRules.TryPrepareMasterResume(ledger, dispatch,
				ledger.Revision, KingdomPolityPresentationState.Enabled, 100L,
				out KingdomPolityMasterResumePlan plan, out string failure), failure);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsTrue(KingdomPolityRules.CanPublishMasterResume(ledger, dispatch,
				plan, out failure), failure);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
			KingdomPolityRules.PublishMasterResumePrevalidated(ledger, dispatch, plan);
			ClassicAssert.AreEqual(100L, ledger.Options.FutureCauseFloorTick);
			ClassicAssert.AreEqual(100L, dispatch.FutureCauseFloorTick);
			ClassicAssert.IsFalse(KingdomPolityRules.CanPublishMasterResume(ledger, dispatch,
				plan, out failure), "source-only preflight must not admit a second write");
		}

		[Test]
		public void MasterResumePreservesAndRefusesInvalidDerivedDispatch()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			KingdomPolityDispatchState dispatch = new KingdomPolityDispatchState
				{ Version = 99, RealmId = "old-realm-or-actor" };
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsFalse(KingdomPolityRules.TryPrepareMasterResume(ledger, dispatch,
				ledger.Revision, KingdomPolityPresentationState.Enabled, 100L,
				out KingdomPolityMasterResumePlan plan, out string failure));
			ClassicAssert.IsNull(plan); CollectionAssert.AreEqual(before,
				KingdomPolityCodec.EncodeEnvelope(ledger));
			ClassicAssert.AreEqual(99, dispatch.Version);
			ClassicAssert.AreEqual("old-realm-or-actor", dispatch.RealmId);
			ClassicAssert.AreEqual(0L, dispatch.FutureCauseFloorTick);
		}

		[Test]
		public void CohortPlanPinsExactPresentationTripleAndRejectsRetryDrift()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			MakeResolverProfile(ledger, KingdomPolityTestData.CurrentProfile);
			KingdomPolityCohortPlanRequest request = Request(ledger,
				"taf:cohort:authority-pin", 2, KingdomPolityCohortPurpose.Trader);
			request.PresentationAuthority.EnableEpoch = 7L;
			request.PresentationAuthority.ReservedTick = 123L;
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, request,
				out KingdomPolityPublicationResult _, out string failure), failure);
			KingdomPolityCohortPlan row = FindCohort(ledger, request.CohortId);
			ClassicAssert.AreEqual(KingdomExperienceOptionKind.AmbientUse,
				row.PresentationOptionKind);
			ClassicAssert.AreEqual(7L, row.PresentationEnableEpoch);
			ClassicAssert.AreEqual(123L, row.PresentationReservedTick);
			request.PresentationAuthority.ReservedTick++;
			ClassicAssert.IsFalse(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, request,
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
			ClassicAssert.AreEqual(KingdomPolityLeaseRecoveryAction.EnsureCurrentPlan,
				KingdomPolityExperienceRecoveryRules.Decide(row, null, true));
			ClassicAssert.AreEqual(KingdomPolityLeaseRecoveryAction.CancelUnpresented,
				KingdomPolityExperienceRecoveryRules.Decide(row, null, false));
			row.ManifestationReceiptId = "taf:projection:recovery-disposition";
			row.Phase = KingdomPolityCohortPhase.Materialized;
			ClassicAssert.AreEqual(KingdomPolityLeaseRecoveryAction.EnsureThenRetainFrozen,
				KingdomPolityExperienceRecoveryRules.Decide(row, null, false));
			ClassicAssert.AreEqual(KingdomPolityLeaseRecoveryAction.EnsureThenWithdrawLoaded,
				KingdomPolityExperienceRecoveryRules.Decide(row, row.SurfaceRef, false));
			row.Phase = KingdomPolityCohortPhase.Concluded;
			ClassicAssert.AreEqual(KingdomPolityLeaseRecoveryAction.EnsureThenCleanupLoaded,
				KingdomPolityExperienceRecoveryRules.Decide(row, row.SurfaceRef, false));
			row.ManifestationReceiptId = null; row.Phase = KingdomPolityCohortPhase.Planned;
			row.PresentationOptionKind = KingdomExperienceOptionKind.None;
			ClassicAssert.AreEqual(KingdomPolityLeaseRecoveryAction.CancelUnpresented,
				KingdomPolityExperienceRecoveryRules.Decide(row, null, true));
			row.ManifestationReceiptId = "taf:projection:legacy-ambiguous";
			row.Phase = KingdomPolityCohortPhase.Cleaned;
			ClassicAssert.AreEqual(KingdomPolityLeaseRecoveryAction.Invalid,
				KingdomPolityExperienceRecoveryRules.Decide(row, row.SurfaceRef, false));
		}

		[Test]
		public void FrozenRetirementReconstructsExactProofAndStillConsumesSharedCap()
		{
			const string realm = "taf:realm:polity-wave3-retirement";
			const string settlement = "taf:settlement:polity-wave3-retirement";
			const string source = "taf:cohort:polity-wave3-retirement";
			KingdomExperienceLedger experience = new KingdomExperienceLedger();
			ClassicAssert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(experience, realm,
				out string failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryObserveOptions(experience,
				experience.Revision, true, true, true, 10L, out failure), failure);
			KingdomExperienceAudienceReceipt audience = ExperienceAudience(realm, settlement,
				"retirement", source, KingdomExperienceOptionKind.AmbientUse);
			KingdomExperienceBodyReservation bodies = ExperienceBody(realm, settlement,
				"retirement", source, 2, KingdomExperienceOptionKind.AmbientUse);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryReservePresentation(experience,
				experience.Revision, audience, bodies, 0, out _, out failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryObserveOptions(experience,
				experience.Revision, true, true, false, 20L, out failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryReleasePresentation(experience,
				experience.Revision, audience.ReservationId, bodies.ReservationId, source,
				out _, out failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryRecoverDurablePresentation(experience,
				experience.Revision, audience, bodies, 0, out _, out failure), failure);
			byte[] recovered = KingdomExperienceCodec.EncodeEnvelope(experience);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryRecoverDurablePresentation(experience,
				0L, audience, bodies, 0, out _, out failure), failure);
			CollectionAssert.AreEqual(recovered, KingdomExperienceCodec.EncodeEnvelope(experience));
			KingdomPolityCohortPlan frozen = new KingdomPolityCohortPlan
			{
				SurfaceRef = settlement, Phase = KingdomPolityCohortPhase.Materialized,
				ManifestationReceiptId = "taf:projection:polity-wave3-retirement",
				PresentationOptionKind = KingdomExperienceOptionKind.AmbientUse,
				PresentationEnableEpoch = 1L, PresentationReservedTick = 10L
			};
			ClassicAssert.AreEqual(KingdomPolityLeaseRecoveryAction.EnsureThenRetainFrozen,
				KingdomPolityExperienceRecoveryRules.Decide(frozen, null, false));
			ClassicAssert.IsTrue(KingdomExperienceRules.TryObserveOptions(experience,
				experience.Revision, true, true, true, 30L, out failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryReadBodyLease(experience,
				bodies.ReservationId, out _, out KingdomExperienceLeaseState state, out failure), failure);
			ClassicAssert.AreEqual(KingdomExperienceLeaseState.Retirement, state);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryClassifyLeaseProof(experience,
				frozen.PresentationOptionKind, bodies.CauseTick,
				frozen.PresentationReservedTick, frozen.PresentationEnableEpoch,
				out state, out failure), failure);
			ClassicAssert.AreEqual(KingdomExperienceLeaseState.Retirement, state);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryReserveBodies(experience, experience.Revision,
				ExperienceBody(realm, settlement, "fill-a", "taf:cohort:fill-a", 7,
					KingdomExperienceOptionKind.CivicStory), 0, out _, out failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryReserveBodies(experience, experience.Revision,
				ExperienceBody(realm, settlement, "fill-b", "taf:cohort:fill-b", 7,
					KingdomExperienceOptionKind.CivicStory), 0, out _, out failure), failure);
			ClassicAssert.AreEqual(16, KingdomExperienceRules.ReservedBodies(experience));
			ClassicAssert.IsFalse(KingdomExperienceRules.TryReserveBodies(experience,
				experience.Revision, ExperienceBody(realm, settlement, "cap-plus-one-retired",
					"taf:cohort:cap-plus-one-retired", 1,
					KingdomExperienceOptionKind.CivicStory), 0,
				out KingdomExperienceCapacityFault fault, out failure));
			ClassicAssert.AreEqual(KingdomExperienceCapacityFault.LiveBodyCapacityFull, fault);
		}

		[Test]
		public void SharedBudgetIncludesLegacyAndNewPurposes()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			MakeResolverProfile(ledger, KingdomPolityTestData.RivalProfile);
			ClassicAssert.IsTrue(KingdomPolityAttentionRules.TryAdmitPlan(ledger, 5,
				out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision,
				Request(ledger, "taf:cohort:budget-warband", 5, KingdomPolityCohortPurpose.Warband,
					KingdomPolityTestData.Rival), out _, out failure), failure);
			ClassicAssert.IsFalse(KingdomPolityAttentionRules.TryAdmitPlan(ledger, 1, out failure));
			StringAssert.Contains("shared polity", failure);
		}

		[Test]
		public void AmbientAndDirectedModesShareBodyCapWithoutSharingAudience()
		{
			const string realm = "taf:realm:polity-wave3-budget";
			const string settlement = "taf:settlement:polity-wave3-budget";
			KingdomExperienceLedger experience = new KingdomExperienceLedger();
			ClassicAssert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(experience, realm,
				out string failure), failure);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryObserveOptions(experience,
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
			ClassicAssert.IsTrue(KingdomExperienceRules.TryReservePresentation(experience,
				experience.Revision, ambientAudience, ambient, 0,
				out KingdomExperienceCapacityFault _, out failure), failure);
			KingdomExperienceBodyReservation directed = ExperienceBody(realm, settlement,
				"directed", "taf:cohort:polity-wave3-directed", 7,
				KingdomExperienceOptionKind.CivicStory);
			ClassicAssert.IsTrue(KingdomExperienceRules.TryReserveBodies(experience,
				experience.Revision, directed, 0, out _, out failure), failure);
			ClassicAssert.AreEqual(1, experience.Audiences.Count,
				"directed conversation/threat must not consume unsolicited audience capacity");
			ClassicAssert.IsTrue(KingdomExperienceRules.TryReserveBodies(experience,
				experience.Revision, ExperienceBody(realm, settlement, "other",
					"taf:event:polity-wave3-other", 2,
					KingdomExperienceOptionKind.CivicStory,
					KingdomExperienceLane.CivicVoices), 0, out _, out failure), failure);
			ClassicAssert.AreEqual(16, KingdomExperienceRules.ReservedBodies(experience));
			byte[] atCap = KingdomExperienceCodec.EncodeEnvelope(experience);
			ClassicAssert.IsFalse(KingdomExperienceRules.TryReserveBodies(experience,
				experience.Revision, ExperienceBody(realm, settlement, "cap-plus-one",
					"taf:event:polity-wave3-cap-plus-one", 1,
					KingdomExperienceOptionKind.CivicStory,
					KingdomExperienceLane.FirstGuest), 0,
				out KingdomExperienceCapacityFault fault, out failure));
			ClassicAssert.AreEqual(KingdomExperienceCapacityFault.LiveBodyCapacityFull, fault);
			CollectionAssert.AreEqual(atCap, KingdomExperienceCodec.EncodeEnvelope(experience));
		}

		[Test]
		public void ExactUnpresentedCancellationReleasesLocalAttentionWithoutBacklog()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			MakeResolverProfile(ledger, KingdomPolityTestData.CurrentProfile);
			KingdomPolityCohortPlanRequest request = Request(ledger,
				"taf:cohort:polity-wave3-lapsed", 7, KingdomPolityCohortPurpose.Guard);
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, request,
				out _, out string failure), failure);
			ClassicAssert.IsFalse(KingdomPolityAttentionRules.TryAdmitPlan(ledger, 1, out failure));
			const string cancellation = "taf:event:polity-presentation-lapse:test";
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryCancelUnpresented(ledger,
				ledger.Revision, request.CohortId, cancellation,
				out KingdomPolityPublicationResult result, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryCancelUnpresented(ledger, 0L,
				request.CohortId, cancellation, out result, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			ClassicAssert.IsTrue(KingdomPolityAttentionRules.TryAdmitPlan(ledger, 1, out failure), failure);
		}

		[Test]
		public void CivicOfficeIsTitleOnlyWhileDeedPromotionRequiresExactEvidence()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			MakeResolverProfile(ledger, KingdomPolityTestData.CurrentProfile);
			KingdomPolityFigurePromotionFacts first = Promotion(18, "Iri", "taf:fact:office:first");
			ClassicAssert.IsFalse(KingdomPolityRules.TryPromoteNamedFigure(ledger, ledger.Revision,
				first, out KingdomPolityPublicationResult _, out string failure),
				"civic title cannot publish polity rank, role, profile, or gear eligibility");
			first.Origin = KingdomPolityFigureOrigin.PromotedByDeed;
			first.RoleKey = "guard";
			ClassicAssert.IsFalse(KingdomPolityRules.TryPromoteNamedFigure(ledger, ledger.Revision,
				first, out _, out failure), "deed promotion cannot borrow office evidence");
			first.CauseRef = "taf:fact:deed:first";
			ClassicAssert.IsTrue(KingdomPolityRules.TryPromoteNamedFigure(ledger, ledger.Revision,
				first, out KingdomPolityPublicationResult result, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryPromoteNamedFigure(ledger, ledger.Revision,
				first, out result, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			KingdomPolityNamedFigureRecord legacyOffice = new KingdomPolityNamedFigureRecord
			{
				FigureId = "taf:figure:legacy-office-title", PolityId = KingdomPolityTestData.Realm,
				DisplayName = "Ara", RoleKey = "guard",
				Origin = KingdomPolityFigureOrigin.Officeholder,
				Phase = KingdomPolityFigurePhase.Active, CauseRef = "taf:fact:office:legacy"
			};
			ledger.NamedFigures.Add(legacyOffice);
			ledger.NamedFigures.Sort((a, b) => string.CompareOrdinal(a.FigureId, b.FigureId));
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
			KingdomPolityCohortPlanRequest guard = Request(ledger,
				"taf:cohort:legacy-office-cannot-guard", 1, KingdomPolityCohortPurpose.Guard);
			guard.NamedFigureId = legacyOffice.FigureId;
			ClassicAssert.IsFalse(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision,
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
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out string failure), failure);
			ClassicAssert.AreEqual(2, ActiveOffices(ledger));
			ClassicAssert.AreEqual(2, KingdomPolityAttentionRules.ActiveNamedFigures(ledger,
				KingdomPolityTestData.Realm));

			const string cause = "taf:fact:office-retirement:v1:test-global";
			ClassicAssert.IsTrue(KingdomPolityRules.TryRetireAllOfficeFigures(ledger,
				ledger.Revision, cause, out KingdomPolityPublicationResult result,
				out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			ClassicAssert.AreEqual(0, ActiveOffices(ledger));
			ClassicAssert.AreEqual(1, KingdomPolityAttentionRules.ActiveNamedFigures(ledger,
				KingdomPolityTestData.Realm));
			for (int i = 0; i < ledger.NamedFigures.Count; i++)
				if (ledger.NamedFigures[i].Origin == KingdomPolityFigureOrigin.Officeholder)
				{
					ClassicAssert.AreEqual(KingdomPolityFigurePhase.Transferred,
						ledger.NamedFigures[i].Phase);
					ClassicAssert.AreEqual(0, ledger.NamedFigures[i].ResidentId);
					ClassicAssert.IsNull(ledger.NamedFigures[i].ResidentSettlementId);
					StringAssert.StartsWith("taf:conclusion:office:v1:",
						ledger.NamedFigures[i].ConclusionRef);
				}
			for (int i = 0; i < 3; i++)
			{
				KingdomPolityFigurePromotionFacts deed = Promotion(60 + i, "Deed " + i,
					"taf:fact:deed:post-office-" + i);
				deed.Origin = KingdomPolityFigureOrigin.PromotedByDeed;
				deed.RoleKey = "courier";
				ClassicAssert.IsTrue(KingdomPolityRules.TryPromoteNamedFigure(ledger,
					ledger.Revision, deed, out _, out failure), failure);
			}
			ClassicAssert.AreEqual(KingdomPolityAttentionRules.MaximumActiveNamedFigures,
				KingdomPolityAttentionRules.ActiveNamedFigures(ledger,
					KingdomPolityTestData.Realm));
			byte[] stable = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsTrue(KingdomPolityRules.TryRetireAllOfficeFigures(ledger, 0L, cause,
				out result, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
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

		private static KingdomPolityProfileFactSet FactsWithPopulation(int previous,
			string token, long tick, string population)
		{
			KingdomPolityProfileFactSet result = Facts(previous, token, tick);
			result.Facts.Add(new KingdomPolityProfileFact
			{
				FactId = "taf:fact:profile:m-" + token,
				Kind = KingdomPolityProfileFactKind.Population,
				ValueKey = population,
				SourceRef = KingdomPolityTestData.Settlement
			});
			result.Facts.Sort((a, b) => string.CompareOrdinal(a.FactId, b.FactId));
			return result;
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

		private static KingdomPolityCohortPlanRequest FromDue(KingdomPolityLedger Ledger,
			KingdomPolityDueWork work)
		{
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryResolverContract(Ledger,
				KingdomPolityTestData.Realm, work.Purpose, out int resolverRulesVersion,
				out int minimum, out int maximum,
				out string failure), failure);
			return new KingdomPolityCohortPlanRequest { CohortId = work.CohortId,
				Purpose = work.Purpose, SourceRef = work.SourceRef,
				PolityId = KingdomPolityTestData.Realm, SurfaceRef = work.SettlementId,
				MemberCount = work.MemberCount, MinimumLevel = minimum, MaximumLevel = maximum,
				EventStreamId = work.EventStreamId,
				RulesVersion = resolverRulesVersion, EventOrdinal = work.WindowOrdinal,
				PresentationAuthority = Authority(work.Purpose, work.CauseTick) };
		}

		private static KingdomPolityCohortPlanRequest Request(KingdomPolityLedger Ledger,
			string id, int members,
			KingdomPolityCohortPurpose purpose, string polity = KingdomPolityTestData.Realm)
		{
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryResolverContract(Ledger, polity, purpose,
				out int resolverRulesVersion, out int minimum, out int maximum,
				out string failure), failure);
			return new KingdomPolityCohortPlanRequest { CohortId = id, Purpose = purpose,
				SourceRef = "taf:event:" + id, PolityId = polity,
				SurfaceRef = KingdomPolityTestData.Settlement, MemberCount = members,
				MinimumLevel = minimum, MaximumLevel = maximum,
				EventStreamId = "taf:stream:" + id,
				RulesVersion = resolverRulesVersion, EventOrdinal = 2UL,
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
				CauseRef = cause, DeedSummary = "completed a proved civic deed" };
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
