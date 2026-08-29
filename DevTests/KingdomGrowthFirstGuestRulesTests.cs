#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomGrowthFirstGuestRulesTests
	{
		private const string Settlement = "taf:settlement:first-guest";
		private const string Realm = "taf:realm:first-guest";
		private const string Hash =
			"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

		[Test]
		public void OpportunityFreezesOneGrowthCauseWithoutCreatingAResident()
		{
			KingdomGrowthBook growth = EnabledGrowth();
			KingdomGrowthArrivalCandidate candidate = Prepare(growth);
			Assert.NotNull(candidate);
			Assert.AreEqual(1L, candidate.Sequence);
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.AwaitingChoice,
				candidate.Phase);
			Assert.IsNull(candidate.ObjectId);
			Assert.AreEqual(KingdomLifecyclePhysicalState.Prepared,
				candidate.CreateStep.State);
			Assert.AreEqual(1, candidate.FirstGuest.CohortSize);
			Assert.AreEqual(growth.NextArrivalTick, candidate.FirstGuest.CauseTick);
			Assert.AreEqual(growth.ArrivalIntervalTicks, candidate.FirstGuest.CadenceTicks);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(
				growth, candidate));
			Assert.IsNull(candidate.ObjectId);
			Assert.IsNull(growth.ArrivalOp,
				"presentation cannot create the parallel automatic settlement operation");

			KingdomGrowthBook restored = RoundTrip(growth);
			Assert.AreEqual(candidate.FirstGuest.OpportunityId,
				restored.ArrivalCandidate.FirstGuest.OpportunityId);
			Assert.AreEqual(candidate.PlannedOrigin,
				restored.ArrivalCandidate.PlannedOrigin);
			Assert.IsFalse(ReferenceEquals(candidate.FirstGuest,
				restored.ArrivalCandidate.FirstGuest));
		}

		[Test]
		public void DeferDeclineAndAdmitAreExactGrowthTransitions()
		{
			KingdomGrowthBook deferred = Published();
			KingdomGrowthArrivalCandidate candidate = deferred.ArrivalCandidate;
			int population = candidate.FirstGuest.PopulationBefore;
			Assert.IsTrue(KingdomLifecycleRules.TryDeferGrowthFirstGuest(
				deferred, candidate, 121L));
			byte[] once = Bytes(deferred);
			Assert.IsTrue(KingdomLifecycleRules.TryDeferGrowthFirstGuest(
				deferred, candidate, 122L));
			CollectionAssert.AreEqual(once, Bytes(deferred));
			Assert.AreEqual(population, candidate.FirstGuest.PopulationBefore);
			Assert.IsNull(candidate.ObjectId);

			KingdomGrowthBook declined = Published();
			candidate = declined.ArrivalCandidate;
			Assert.IsTrue(KingdomLifecycleRules.TryDeclineGrowthFirstGuest(
				declined, candidate, 121L));
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.Declined, candidate.Phase);
			Assert.AreEqual(KingdomGrowthArrivalDisposition.Declined, candidate.Disposition);
			Assert.IsNull(candidate.ObjectId);
			Assert.IsNull(candidate.FirstGuest.BodyReservationId);
			KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(
				declined, KingdomGrowthAction.Arrival, null, 122L);
			Assert.NotNull(op); op.ArrivalDisposition = KingdomGrowthArrivalDisposition.Declined;
			op.ArrivalCandidateId = candidate.Id;
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(declined, op));
			Assert.IsTrue(KingdomLifecycleRules.TryBindDeclinedFirstGuestOperation(
				declined, candidate, 122L));
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.Settled, candidate.Phase);
			Assert.IsNull(candidate.ObjectId);

			KingdomGrowthBook admitted = Published();
			candidate = admitted.ArrivalCandidate;
			KingdomExperienceBodyReservation lease = Body(candidate, 121L, 1L);
			KingdomExperienceBodyReservation wrong = Body(candidate, 121L, 1L);
			wrong.SourceId = "taf:foreign:first-guest";
			byte[] before = Bytes(admitted);
			Assert.IsFalse(KingdomLifecycleRules.TryAdmitGrowthFirstGuest(
				admitted, candidate, wrong, 121L));
			CollectionAssert.AreEqual(before, Bytes(admitted));
			Assert.IsTrue(KingdomLifecycleRules.TryAdmitGrowthFirstGuest(
				admitted, candidate, lease, 121L));
			Assert.AreEqual(KingdomGrowthFirstGuestChoiceState.Admitted,
				candidate.FirstGuest.ChoiceState);
			Assert.AreEqual(KingdomGrowthFirstGuestBodyLeaseState.Reserved,
				candidate.FirstGuest.BodyLeaseState);
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.Prepared, candidate.Phase);
			Assert.IsNull(candidate.ObjectId,
				"admission transfers authority but does not itself mint the body");
		}

		[Test]
		public void LegacyV3InterposesOnlyWithEveryDecodedZeroMutationProof()
		{
			KingdomGrowthBook current = Published();
			byte[] v3 = KingdomLifecycleWireCodec.GrowthV3PayloadFixture(current);
			KingdomGrowthBook legacy = KingdomLifecycleWireCodec.ReadGrowthPayload(v3);
			Assert.AreEqual(KingdomLifecycleRules.CurrentGrowthFormatVersion,
				legacy.FormatVersion);
			Assert.IsTrue(legacy.ArrivalCandidate.LegacyAutomaticRecovery);
			Assert.IsNull(legacy.ArrivalCandidate.FirstGuest);
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.Prepared,
				legacy.ArrivalCandidate.Phase);
			Assert.IsTrue(KingdomLifecycleRules.TryInterposeLegacyPreparedFirstGuest(
				legacy, legacy.ArrivalCandidate, true, true, true, true, true, 121L));
			Assert.AreEqual(KingdomGrowthFirstGuestFactsState.LegacyPartial,
				legacy.ArrivalCandidate.FirstGuest.FactsState);
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.AwaitingChoice,
				legacy.ArrivalCandidate.Phase);

			for (int omitted = 0; omitted < 5; omitted++)
			{
				legacy = KingdomLifecycleWireCodec.ReadGrowthPayload(v3);
				byte[] before = Bytes(legacy);
				bool[] proof = { true, true, true, true, true };
				proof[omitted] = false;
				Assert.IsFalse(KingdomLifecycleRules.TryInterposeLegacyPreparedFirstGuest(
					legacy, legacy.ArrivalCandidate, proof[0], proof[1], proof[2],
					proof[3], proof[4], 121L), "proof " + omitted);
				CollectionAssert.AreEqual(before, Bytes(legacy), "proof " + omitted);
				Assert.IsTrue(legacy.ArrivalCandidate.LegacyAutomaticRecovery);
			}
		}

		[Test]
		public void W0CapacityAndOptionRefusalsNeverMutateGrowthOpportunity()
		{
			KingdomGrowthBook growth = Published();
			KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
			KingdomExperienceBodyReservation request = Body(candidate, 121L, 1L);
			KingdomExperienceLedger full = Experience();
			Reserve(full, OtherBody("seven", 7));
			Reserve(full, OtherBody("fourteen", 7));
			Reserve(full, OtherBody("sixteen", 2));
			byte[] opportunity = Bytes(growth);
			Assert.IsFalse(KingdomExperienceRules.TryReserveBodies(full, full.Revision,
				request, 0, out KingdomExperienceCapacityFault fault, out string failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.LiveBodyCapacityFull, fault);
			CollectionAssert.AreEqual(opportunity, Bytes(growth));

			KingdomExperienceLedger available = Experience();
			Reserve(available, OtherBody("seven", 7));
			Reserve(available, OtherBody("fourteen", 7));
			Reserve(available, OtherBody("fifteen", 1));
			Assert.IsTrue(KingdomExperienceRules.TryReserveBodies(available,
				available.Revision, request, 0, out fault, out failure), failure);
			Assert.AreEqual(16, KingdomExperienceRules.ReservedBodies(available));

			KingdomExperienceLedger disabled = Experience();
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(disabled,
				disabled.Revision, false, true, true, 121L, out failure), failure);
			Assert.IsFalse(KingdomExperienceRules.TryReserveBodies(disabled,
				disabled.Revision, request, 0, out fault, out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.OptionDisabled, fault);
			CollectionAssert.AreEqual(opportunity, Bytes(growth));
		}

		[Test]
		public void FirstGuestAndCuratorShareOneAudienceWithoutSourceLossOrStarvation()
		{
			KingdomGrowthBook growth = Published();
			KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
			KingdomCivicMemoryAuthority knowledge = KnowledgeAuthority();
			KingdomCuriosityCause cause = KingdomCuriosityLeadCodecTests.Cause(
				"first-guest-collision");
			cause.SettlementId = Settlement; cause.CompletedTick = 121L;
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryPlanCuriosity(knowledge,
				cause, KingdomCuriosityLeadCodecTests.Notes(),
				out KingdomCuriosityLeadPlan plan, out string failure), failure);
			KingdomCuriosityReceipt curiosity = plan.CuriosityReceipt;
			KingdomExperienceLedger experience = Experience();
			KingdomExperienceAudienceReceipt guest = FirstGuestAudience(candidate,
				experience, 121L);
			KingdomExperienceAudienceReceipt curator = CuratorAudience(curiosity,
				experience, 121L);
			byte[] growthSource = Bytes(growth);
			long knowledgeSourceRevision = knowledge.Revision;

			Assert.IsTrue(KingdomExperienceRules.TryReserveAudience(experience,
				experience.Revision, guest, out KingdomExperienceCapacityFault fault,
				out failure), failure);
			byte[] guestAtCap = KingdomExperienceCodec.EncodeEnvelope(experience);
			Assert.IsFalse(KingdomExperienceRules.TryReserveAudience(experience,
				experience.Revision, curator, out fault, out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.AudienceCapacityFull, fault);
			Assert.IsFalse(KingdomCuriosityLeadTransactions.TryCommit(plan, knowledge,
				experience, out bool committed, out failure));
			CollectionAssert.AreEqual(guestAtCap,
				KingdomExperienceCodec.EncodeEnvelope(experience));
			CollectionAssert.AreEqual(growthSource, Bytes(growth));
			Assert.AreEqual(knowledgeSourceRevision, knowledge.Revision);
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryProveSourceAbsent(knowledge,
				curiosity.SourceId, out bool absent, out failure), failure);
			Assert.IsTrue(absent, "a refused curator presentation must not consume its source");

			experience = KingdomExperienceCodec.DecodeEnvelope(guestAtCap);
			Assert.IsTrue(KingdomExperienceRules.TryReleaseAudience(experience,
				experience.Revision, guest.ReservationId, guest.SourceId, out fault,
				out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryReserveAudience(experience,
				experience.Revision, curator, out fault, out failure), failure);
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryCommit(plan, knowledge,
				experience, out committed, out failure), failure);
			Assert.IsTrue(committed);
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryReadExactCuriosity(knowledge,
				curiosity, out KingdomCuriosityReceipt durable, out failure), failure);
			Assert.AreEqual(curiosity.NoteId, durable.NoteId);

			byte[] curatorAtCap = KingdomExperienceCodec.EncodeEnvelope(experience);
			Assert.IsFalse(KingdomExperienceRules.TryReserveAudience(experience,
				experience.Revision, guest, out fault, out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.AudienceCapacityFull, fault);
			CollectionAssert.AreEqual(curatorAtCap,
				KingdomExperienceCodec.EncodeEnvelope(experience));
			CollectionAssert.AreEqual(growthSource, Bytes(growth));
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryReadExactCuriosity(knowledge,
				curiosity, out durable, out failure), failure);

			experience = KingdomExperienceCodec.DecodeEnvelope(curatorAtCap);
			Assert.IsTrue(KingdomExperienceRules.TryReleaseAudience(experience,
				experience.Revision, curator.ReservationId, curator.SourceId, out fault,
				out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryReserveAudience(experience,
				experience.Revision, guest, out fault, out failure), failure);
			Assert.AreEqual(1, experience.Audiences.Count);
			Assert.AreEqual(KingdomExperienceLane.FirstGuest,
				experience.Audiences[0].Lane);
			Assert.AreEqual(guest.SourceId, experience.Audiences[0].SourceId);
			CollectionAssert.AreEqual(growthSource, Bytes(growth));
			Assert.IsTrue(KingdomCuriosityLeadTransactions.TryReadExactCuriosity(knowledge,
				curiosity, out durable, out failure), failure);
		}

		[Test]
		public void PhysicalAdmissionRequiresOneExactPreReservedBody()
		{
			KingdomGrowthBook growth = Published();
			KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
			byte[] growthBefore = Bytes(growth);
			Assert.IsFalse(KingdomLifecycleRules.TryAdmitGrowthFirstGuest(
				growth, candidate, 121L));
			CollectionAssert.AreEqual(growthBefore, Bytes(growth));

			KingdomExperienceLedger disabled = Experience();
			Reserve(disabled, OtherBody("full-seven", 7));
			Reserve(disabled, OtherBody("full-fourteen", 7));
			Reserve(disabled, OtherBody("full-sixteen", 2));
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(disabled,
				disabled.Revision, false, true, true, 121L, out string failure), failure);
			byte[] experienceBefore = KingdomExperienceCodec.EncodeEnvelope(disabled);
			KingdomExperienceBodyReservation body = Body(candidate, 121L, 1L);
			Assert.IsFalse(KingdomExperienceRules.TryReserveBodies(disabled, disabled.Revision,
				body, 0, out KingdomExperienceCapacityFault _, out failure));
			CollectionAssert.AreEqual(growthBefore, Bytes(growth));
			CollectionAssert.AreEqual(experienceBefore,
				KingdomExperienceCodec.EncodeEnvelope(disabled));

			KingdomExperienceLedger available = Experience();
			Assert.IsTrue(KingdomExperienceRules.TryReserveBodies(available, available.Revision,
				body, 0, out KingdomExperienceCapacityFault _, out failure), failure);
			byte[] reserved = KingdomExperienceCodec.EncodeEnvelope(available);
			Assert.IsTrue(KingdomLifecycleRules.TryAdmitGrowthFirstGuest(
				growth, candidate, body, 121L));
			Assert.AreEqual(KingdomGrowthFirstGuestBodyLeaseState.Reserved,
				candidate.FirstGuest.BodyLeaseState);
			Assert.AreEqual(KingdomGrowthFirstGuestGuestPhase.Preparing,
				candidate.FirstGuest.GuestPhase);
			CollectionAssert.AreEqual(reserved,
				KingdomExperienceCodec.EncodeEnvelope(available),
				"Growth records the lease but cannot mutate W0 capacity itself");
		}

		[Test]
		public void HostedGuestIsDurableIndefiniteAndStillOwnsItsBodyLease()
		{
			KingdomGrowthBook growth = Published();
			KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
			KingdomExperienceBodyReservation body = Body(candidate, 121L, 1L);
			Assert.IsTrue(KingdomLifecycleRules.TryAdmitGrowthFirstGuest(growth, candidate,
				body, 121L));
			MoveCandidateToHosted(growth, candidate, 122L);
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.GuestHosted, candidate.Phase);
			Assert.AreEqual(KingdomGrowthFirstGuestGuestPhase.Hosted,
				candidate.FirstGuest.GuestPhase);
			Assert.AreEqual(KingdomGrowthFirstGuestBodyLeaseState.Reserved,
				candidate.FirstGuest.BodyLeaseState);
			Assert.IsNull(growth.ArrivalOp);
			Assert.IsFalse(KingdomLifecycleRules.GrowthFirstGuestBodyReleaseReady(
				growth, candidate));
			Assert.IsTrue(KingdomLifecycleRules.GrowthFirstGuestBodyLeaseRecoveryRequired(
				growth, candidate));

			byte[] frozen = Bytes(growth);
			KingdomGrowthBook restored = RoundTrip(growth);
			candidate = restored.ArrivalCandidate;
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.GuestHosted, candidate.Phase);
			Assert.AreEqual(KingdomGrowthFirstGuestGuestPhase.Hosted,
				candidate.FirstGuest.GuestPhase);
			Assert.IsFalse(KingdomLifecycleRules.TryDeferGrowthFirstGuest(restored,
				candidate, 1000000L));
			Assert.IsFalse(KingdomLifecycleRules.TryDeclineGrowthFirstGuest(restored,
				candidate, 1000000L));
			CollectionAssert.AreEqual(frozen, Bytes(restored),
				"elapsed time and correspondence choices cannot advance a hosted guest");
		}

		[Test]
		public void LoadedDeathNeedsExactBodyMarkerAndZoneEvidence()
		{
			KingdomGrowthBook growth = Published();
			KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
			KingdomExperienceBodyReservation body = Body(candidate, 121L, 1L);
			Assert.IsTrue(KingdomLifecycleRules.TryAdmitGrowthFirstGuest(growth, candidate,
				body, 121L));
			MoveCandidateToHosted(growth, candidate, 122L);
			byte[] before = Bytes(growth);
			Assert.IsFalse(KingdomLifecycleRules.TryObserveGrowthFirstGuestTerminal(growth,
				candidate, "taf:object:decoy", candidate.Marker, candidate.LodgingZoneId,
				KingdomGrowthFirstGuestTerminalState.Died, 124L));
			Assert.IsFalse(KingdomLifecycleRules.TryObserveGrowthFirstGuestTerminal(growth,
				candidate, candidate.ObjectId, "taf:marker:decoy", candidate.LodgingZoneId,
				KingdomGrowthFirstGuestTerminalState.Died, 124L));
			Assert.IsFalse(KingdomLifecycleRules.TryObserveGrowthFirstGuestTerminal(growth,
				candidate, candidate.ObjectId, candidate.Marker, "zone-unloaded",
				KingdomGrowthFirstGuestTerminalState.Died, 124L));
			Assert.IsFalse(KingdomLifecycleRules.TryObserveGrowthFirstGuestTerminal(growth,
				candidate, candidate.ObjectId, candidate.Marker, candidate.LodgingZoneId,
				KingdomGrowthFirstGuestTerminalState.Citizen, 124L));
			CollectionAssert.AreEqual(before, Bytes(growth));

			Assert.IsTrue(KingdomLifecycleRules.TryObserveGrowthFirstGuestTerminal(growth,
				candidate, candidate.ObjectId, candidate.Marker, candidate.LodgingZoneId,
				KingdomGrowthFirstGuestTerminalState.Died, 124L));
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.GuestTerminal, candidate.Phase);
			Assert.AreEqual(KingdomGrowthFirstGuestTerminalState.Died,
				candidate.FirstGuest.GuestTerminalState);
			Assert.AreEqual(KingdomGrowthArrivalDisposition.Departed, candidate.Disposition);
			Assert.IsTrue(KingdomLifecycleRules.GrowthFirstGuestBodyReleaseReady(
				growth, candidate));
			Assert.AreEqual(KingdomGrowthFirstGuestTerminalState.Died,
				RoundTrip(growth).ArrivalCandidate.FirstGuest.GuestTerminalState);
		}

		[Test]
		public void ExplicitDepartureReleasesOnceAndProducesAZeroResidentTerminal()
		{
			KingdomGrowthBook growth = Published();
			KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
			KingdomExperienceBodyReservation body = Body(candidate, 121L, 1L);
			Assert.IsTrue(KingdomLifecycleRules.TryAdmitGrowthFirstGuest(growth, candidate,
				body, 121L));
			MoveCandidateToHosted(growth, candidate, 122L);
			Assert.IsTrue(KingdomLifecycleRules.TryBeginGrowthFirstGuestDeparture(growth,
				candidate, 124L));
			Assert.AreEqual(KingdomGrowthFirstGuestGuestPhase.DepartureIntent,
				candidate.FirstGuest.GuestPhase);
			Assert.IsTrue(KingdomLifecycleRules.TryObserveGrowthFirstGuestTerminal(growth,
				candidate, candidate.ObjectId, candidate.Marker, candidate.LodgingZoneId,
				KingdomGrowthFirstGuestTerminalState.Departed, 125L));
			Assert.IsTrue(KingdomLifecycleRules.TryMarkGrowthFirstGuestBodyReleased(growth,
				candidate, body.ReservationId, 126L));
			byte[] released = Bytes(growth);
			Assert.IsTrue(KingdomLifecycleRules.TryMarkGrowthFirstGuestBodyReleased(growth,
				candidate, body.ReservationId, 126L));
			CollectionAssert.AreEqual(released, Bytes(growth));
			Assert.IsFalse(KingdomLifecycleRules.GrowthFirstGuestBodyLeaseRecoveryRequired(
				growth, candidate), "a departed lease must never remint capacity");

			KingdomGrowthOperation operation = KingdomLifecycleRules.PrepareGrowthOperation(
				growth, KingdomGrowthAction.Arrival, null, 127L);
			Assert.NotNull(operation);
			operation.ArrivalDisposition = KingdomGrowthArrivalDisposition.Departed;
			operation.ArrivalCandidateId = candidate.Id;
			KingdomGrowthOutboxEvent notice =
				KingdomLifecycleRules.PrepareDeclaredGrowthOutboxEvent(operation, 0,
					"first-guest-departed", null, null, null, null, null, null, null,
					0, null, 0, null, 0, null, 0, null, 0, null, 0, null);
			Assert.NotNull(notice); operation.OutboxEvents.Add(notice);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, operation));
			Assert.IsTrue(KingdomLifecycleRules.TryBindDepartedFirstGuestOperation(growth,
				candidate, 128L));
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.Settled, candidate.Phase);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.ClockIntent, 129L));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(growth, operation,
				operation.ClockLease.Before));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(growth, operation,
				operation.ClockLease.After));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.Sinks, 130L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.Terminal, 131L));
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthFirstGuestTerminal(growth,
				candidate, operation, 0, 132L));
			Assert.AreEqual(KingdomGrowthArrivalDisposition.Departed,
				growth.FirstGuestTerminal.Result);
			Assert.AreEqual(0, growth.FirstGuestTerminal.ResidentId);
			Assert.AreEqual(KingdomGrowthFirstGuestTerminalState.Departed,
				growth.FirstGuestTerminal.Opportunity.GuestTerminalState);
		}

		[Test]
		public void WelcomeIntentSurvivesSaveAndDoesNotAutoCreateCitizenship()
		{
			KingdomGrowthBook growth = Published();
			KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
			KingdomExperienceBodyReservation body = Body(candidate, 121L, 1L);
			Assert.IsTrue(KingdomLifecycleRules.TryAdmitGrowthFirstGuest(growth, candidate,
				body, 121L));
			MoveCandidateToHosted(growth, candidate, 122L);
			Assert.IsTrue(KingdomLifecycleRules.TryBeginGrowthFirstGuestCitizenship(growth,
				candidate, 124L));
			growth = RoundTrip(growth); candidate = growth.ArrivalCandidate;
			Assert.AreEqual(KingdomGrowthFirstGuestGuestPhase.CitizenshipIntent,
				candidate.FirstGuest.GuestPhase);
			Assert.IsNull(growth.ArrivalOp);
			Assert.IsTrue(KingdomLifecycleRules.TryPrepareGrowthFirstGuestCitizenship(growth,
				candidate, 125L));
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.Escrowed, candidate.Phase);
			Assert.AreEqual(KingdomGrowthFirstGuestGuestPhase.CitizenshipPrepared,
				candidate.FirstGuest.GuestPhase);
			Assert.AreEqual(KingdomGrowthFirstGuestBodyLeaseState.Reserved,
				candidate.FirstGuest.BodyLeaseState);
			Assert.IsNull(growth.ArrivalOp,
				"citizenship begins only through the ordinary lodging/domain transaction");
		}

		[Test]
		public void HostedGuestCitizenshipRechecksCurrentCapacityWithoutMutation()
		{
			KingdomGrowthBook growth = Published();
			KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
			KingdomExperienceBodyReservation body = Body(candidate, 121L, 1L);
			Assert.IsTrue(KingdomLifecycleRules.TryAdmitGrowthFirstGuest(growth, candidate,
				body, 121L));
			MoveCandidateToHosted(growth, candidate, 122L);
			byte[] frozen = Bytes(growth);
			Assert.IsTrue(KingdomLifecycleRules.TryCheckGrowthFirstGuestCurrentApplicability(
				growth, candidate, 3, 20, 4, 10, 20, 2, out string failure), failure);
			Assert.IsFalse(KingdomLifecycleRules.TryCheckGrowthFirstGuestCurrentApplicability(
				growth, candidate, 20, 20, 4, 10, 20, 2, out failure));
			Assert.IsFalse(KingdomLifecycleRules.TryCheckGrowthFirstGuestCurrentApplicability(
				growth, candidate, 3, 20, 4, 3, 20, 2, out failure));
			Assert.IsFalse(KingdomLifecycleRules.TryCheckGrowthFirstGuestCurrentApplicability(
				growth, candidate, 3, 20, 4, 10, 1, 2, out failure));
			CollectionAssert.AreEqual(frozen, Bytes(growth));
			Assert.AreEqual(KingdomGrowthFirstGuestGuestPhase.Hosted,
				candidate.FirstGuest.GuestPhase);
		}

		[Test]
		public void GrowthV5DefaultsPhysicalEvidenceAndRefusesLossyDowngrade()
		{
			KingdomGrowthBook growth = Published();
			byte[] v5 = KingdomLifecycleWireCodec.GrowthV5PayloadFixture(growth);
			Assert.AreEqual(KingdomLifecycleRules.TerminalReceiptGrowthFormatVersion,
				BitConverter.ToInt32(v5, 4));
			KingdomGrowthBook restored = KingdomLifecycleWireCodec.ReadGrowthPayload(v5);
			Assert.AreEqual(KingdomLifecycleRules.CurrentGrowthFormatVersion,
				restored.FormatVersion);
			Assert.AreEqual(1, restored.ArrivalCandidate.FirstGuest.RulesVersion,
				"v5 migration must retain historical rules rather than infer physical authority");
			Assert.AreEqual(KingdomGrowthFirstGuestGuestPhase.None,
				restored.ArrivalCandidate.FirstGuest.GuestPhase);
			Assert.AreEqual(-1L, restored.ArrivalCandidate.FirstGuest.GuestActionTick);

			KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
			KingdomExperienceBodyReservation body = Body(candidate, 121L, 1L);
			Assert.IsTrue(KingdomLifecycleRules.TryAdmitGrowthFirstGuest(growth, candidate,
				body, 121L));
			MoveCandidateToHosted(growth, candidate, 122L);
			Assert.Throws<InvalidDataException>(() =>
				KingdomLifecycleWireCodec.GrowthV5PayloadFixture(growth));

			byte[] future = Bytes(growth);
			Buffer.BlockCopy(BitConverter.GetBytes(
				KingdomLifecycleRules.CurrentGrowthFormatVersion + 1), 0, future, 4, 4);
			KingdomGrowthBook opaque = KingdomLifecycleWireCodec.ReadGrowthPayload(future);
			Assert.IsTrue(opaque.Quarantined);
			Assert.AreEqual(KingdomLifecycleRules.CurrentGrowthFormatVersion + 1,
				opaque.OpaqueWireVersion);
			CollectionAssert.AreEqual(future,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(opaque));
		}

		[Test]
		public void TerminalReceiptFreezesPersonResultAndOutboxBeforeCandidateRetirement()
		{
			KingdomGrowthBook growth = Published();
			KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
			Assert.IsTrue(KingdomLifecycleRules.TryDeclineGrowthFirstGuest(growth, candidate, 121L));
			KingdomGrowthOperation operation = KingdomLifecycleRules.PrepareGrowthOperation(
				growth, KingdomGrowthAction.Arrival, null, 122L);
			operation.ArrivalDisposition = KingdomGrowthArrivalDisposition.Declined;
			operation.ArrivalCandidateId = candidate.Id;
			KingdomGrowthOutboxEvent notice =
				KingdomLifecycleRules.PrepareDeclaredGrowthOutboxEvent(operation, 0,
					"first-guest-declined", null, null, null, null, null, null, null,
					0, null, 0, null, 0, null, 0, null, 0, null, 0, null);
			Assert.NotNull(notice); operation.OutboxEvents.Add(notice);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, operation));
			Assert.IsTrue(KingdomLifecycleRules.TryBindDeclinedFirstGuestOperation(
				growth, candidate, 123L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.ClockIntent, 124L));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(growth, operation,
				operation.ClockLease.Before));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(growth, operation,
				operation.ClockLease.After));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.Sinks, 125L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.Terminal, 126L));
			byte[] beforeWrongResident = Bytes(growth);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowthFirstGuestTerminal(growth,
				candidate, operation, 1, 127L));
			CollectionAssert.AreEqual(beforeWrongResident, Bytes(growth));
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthFirstGuestTerminal(growth,
				candidate, operation, 0, 127L));
			KingdomGrowthFirstGuestTerminalReceipt terminal = growth.FirstGuestTerminal;
			Assert.AreEqual(candidate.Id, terminal.CandidateId);
			Assert.AreEqual(operation.OutboxEvents[0].EventId, terminal.ArrivalOutboxEventId);
			Assert.AreEqual(KingdomGrowthArrivalDisposition.Declined, terminal.Result);
			Assert.IsTrue(KingdomLifecycleRules.ValidGrowthFirstGuestTerminal(growth, terminal));
			byte[] frozen = Bytes(growth);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthFirstGuestTerminal(growth,
				candidate, operation, 0, 999L));
			CollectionAssert.AreEqual(frozen, Bytes(growth));
			Assert.IsTrue(KingdomLifecycleRules.RetireGrowth(growth, operation, 128L));
			Assert.IsTrue(KingdomLifecycleRules.RetireGrowthArrivalCandidate(growth, candidate));
			Assert.NotNull(growth.FirstGuestTerminal);
			KingdomGrowthBook restored = RoundTrip(growth);
			Assert.AreEqual(terminal.ReceiptId, restored.FirstGuestTerminal.ReceiptId);
			Assert.AreEqual(terminal.ArrivalOutboxEventId,
				restored.FirstGuestTerminal.ArrivalOutboxEventId);

			byte[] historicalGrowth = KingdomLifecycleWireCodec.GrowthV5PayloadFixture(restored);
			KingdomGrowthBook migratedGrowth =
				KingdomLifecycleWireCodec.ReadGrowthPayload(historicalGrowth);
			Assert.AreEqual(KingdomGrowthFirstGuestTerminalReceipt.CurrentVersion,
				migratedGrowth.FirstGuestTerminal.Version);
			Assert.AreEqual(1, migratedGrowth.FirstGuestTerminal.Opportunity.RulesVersion);
			KingdomLifecycleBook parent = EnabledParent(); parent.Growth = migratedGrowth;
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeFirstGuestV15ForTests(
				SettlementWith(parent), out byte[] v15, out string failure), failure);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v15,
				out KingdomSettlement archived, out int future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.AreEqual(KingdomGrowthFirstGuestTerminalReceipt.CurrentVersion,
				archived.LifecycleBook.Growth.FirstGuestTerminal.Version);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeFirstGuestV15ForTests(
				archived, out byte[] repeatedV15, out failure), failure);
			CollectionAssert.AreEqual(v15, repeatedV15);
		}

		[Test]
		public void FirstGuestIsOneOwnedPersonNotAGeneralOrForeignArrivalState()
		{
			KingdomGrowthBook growth = EnabledGrowth();
			Assert.IsNull(Prepare(growth, "snapjaw"));
			growth.ArrivalCandidateNextSequence = 2L;
			growth.ArrivalCandidateRetiredThrough = 1L;
			Assert.IsNull(Prepare(growth));

			growth = Published();
			growth.ArrivalCandidate.FirstGuest.CohortSize = 2;
			Assert.Throws<InvalidDataException>(() => Bytes(growth));
		}

		[Test]
		public void ArchiveV17CarriesCadenceWhileV16PhysicalGuestAndOlderDomainsStayFrozen()
		{
			KingdomLifecycleBook parent = EnabledParent();
			KingdomGrowthArrivalCandidate candidate = Prepare(parent.Growth);
			candidate.FirstGuest.RulesVersion = 1;
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(
				parent.Growth, candidate));
			KingdomSettlement historicalFirstGuest = SettlementWith(parent);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeFirstGuestV15ForTests(
				historicalFirstGuest, out byte[] v15, out string failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.FirstGuestVersion,
				BitConverter.ToInt32(v15, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v15,
				out KingdomSettlement restoredV15, out int future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.AreEqual(candidate.FirstGuest.OpportunityId,
				restoredV15.LifecycleBook.Growth.ArrivalCandidate.FirstGuest.OpportunityId);
			Assert.AreEqual(1,
				restoredV15.LifecycleBook.Growth.ArrivalCandidate.FirstGuest.RulesVersion);
			Assert.AreEqual(KingdomGrowthFirstGuestGuestPhase.None,
				restoredV15.LifecycleBook.Growth.ArrivalCandidate.FirstGuest.GuestPhase);
			Assert.AreEqual(KingdomLifecycleRules.CurrentGrowthFormatVersion,
				restoredV15.LifecycleBook.Growth.FormatVersion);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeFirstGuestV15ForTests(
				restoredV15, out byte[] repeatedV15, out failure), failure);
			CollectionAssert.AreEqual(v15, repeatedV15);

			Assert.IsFalse(KingdomArchivedSettlementCodec.TryEncodeCivicAuthorityV14ForTests(
				historicalFirstGuest, out byte[] _, out failure));
			StringAssert.Contains("historical arrival phase", failure);

			KingdomLifecycleBook physicalParent = EnabledParent();
			KingdomGrowthArrivalCandidate physical = Prepare(physicalParent.Growth);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(
				physicalParent.Growth, physical));
			KingdomExperienceBodyReservation body = Body(physical, 121L, 1L);
			Assert.IsTrue(KingdomLifecycleRules.TryAdmitGrowthFirstGuest(
				physicalParent.Growth, physical, body, 121L));
			MoveCandidateToHosted(physicalParent.Growth, physical, 122L);
			KingdomSettlement current = SettlementWith(physicalParent);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodePhysicalFirstGuestV16ForTests(
				current, out byte[] v16, out failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.PhysicalFirstGuestVersion,
				BitConverter.ToInt32(v16, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v16,
				out KingdomSettlement restoredV16, out future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.GuestHosted,
				restoredV16.LifecycleBook.Growth.ArrivalCandidate.Phase);
			Assert.AreEqual(KingdomGrowthFirstGuestGuestPhase.Hosted,
				restoredV16.LifecycleBook.Growth.ArrivalCandidate.FirstGuest.GuestPhase);
			Assert.AreEqual(KingdomGrowthFirstGuestBodyLeaseState.Reserved,
				restoredV16.LifecycleBook.Growth.ArrivalCandidate.FirstGuest.BodyLeaseState);
			Assert.IsTrue(restoredV16.LifecycleBook.Growth.ArrivalCadenceMigrationPending);
			Assert.AreEqual(1UL,
				restoredV16.LifecycleBook.Growth.ArrivalOrdinalHighWater);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(current,
				out byte[] v17, out failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.ArrivalCadenceVersion,
				BitConverter.ToInt32(v17, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v17,
				out KingdomSettlement restoredV17, out future, out failure), failure);
			Assert.AreEqual(physical.ArrivalOpportunityOrdinal,
				restoredV17.LifecycleBook.Growth.ArrivalCandidate.ArrivalOpportunityOrdinal);
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryEncodeFirstGuestV15ForTests(
				current, out byte[] _, out failure));
			StringAssert.Contains("historical physical", failure);

			byte[] futurePayload = (byte[])v17.Clone();
			Buffer.BlockCopy(BitConverter.GetBytes(
				KingdomArchivedSettlementCodec.CurrentVersion + 1), 0, futurePayload, 4, 4);
			Assert.IsFalse(KingdomArchivedSettlementCodec.TryDecode(futurePayload,
				out KingdomSettlement futureSettlement, out future, out failure));
			Assert.IsNull(futureSettlement);
			Assert.AreEqual(KingdomArchivedSettlementCodec.CurrentVersion + 1, future);

			KingdomGrowthBook historicalGrowth = KingdomLifecycleWireCodec.ReadGrowthPayload(
				KingdomLifecycleWireCodec.GrowthV3PayloadFixture(parent.Growth));
			KingdomLifecycleBook historicalParent = EnabledParent();
			historicalParent.Growth = historicalGrowth;
			KingdomSettlement historical = SettlementWith(historicalParent);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeCivicAuthorityV14ForTests(
				historical, out byte[] v14, out failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.CivicAuthorityVersion,
				BitConverter.ToInt32(v14, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v14,
				out KingdomSettlement migrated, out future, out failure), failure);
			Assert.IsTrue(migrated.LifecycleBook.Growth.ArrivalCandidate
				.LegacyAutomaticRecovery);
			Assert.IsNull(migrated.LifecycleBook.Growth.ArrivalCandidate.FirstGuest);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeCivicAuthorityV14ForTests(
				migrated, out byte[] repeated, out failure), failure);
			CollectionAssert.AreEqual(v14, repeated);
		}

		[Test]
		public void HealthySecondArrivalIsOrdinaryAndReachesDomainSettlement()
		{
			KingdomGrowthBook growth = Published();
			RetireDeclinedFirstGuest(growth);
			Assert.AreEqual(2L, growth.ArrivalCandidateNextSequence);
			Assert.AreEqual(2L, growth.ArrivalNextSequence);
			KingdomGrowthArrivalCandidate candidate = PrepareOrdinary(growth,
				growth.NextArrivalTick);
			Assert.NotNull(candidate);
			Assert.AreEqual(2L, candidate.Sequence);
			Assert.IsNull(candidate.FirstGuest);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(growth,
				candidate));
			Assert.IsFalse(KingdomLifecycleRules.GrowthFirstGuestBodyLeaseRecoveryRequired(
				growth, candidate), "ordinary arrivals never acquire a W0 body lease");

			MoveCandidateToObserved(growth, candidate, true, growth.NextArrivalTick);
			KingdomGrowthOperation operation = JoinedOperation(growth, candidate, 145L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, operation));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.WaterIntent, 146L));
			ProveWater(growth, operation, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.WaterSettled, 147L));
			SettleCandidateDisposition(growth, candidate, operation, true, 148L);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.DomainIntent, 150L));
			for (int i = 0; i < operation.DomainSteps.Count; i++)
				ProveDomain(growth, operation, i);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.DomainSettled, 151L));
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.Settled, candidate.Phase);
			Assert.AreEqual(5, operation.DomainCursor);
			Assert.IsNull(candidate.FirstGuest);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth,
				growth.SettlementId));
		}

		[Test]
		public void WrongZoneDefersByteExactlyAndV1BindsCurrentBeforeRecovery()
		{
			KingdomGrowthBook legacy = KingdomLifecycleWireCodec.ReadGrowthPayload(
				KingdomLifecycleWireCodec.GrowthV3PayloadFixture(Published()));
			KingdomGrowthArrivalCandidate candidate = legacy.ArrivalCandidate;
			byte[] beforeWrongZone = Bytes(legacy);
			Assert.IsFalse(KingdomLifecycleRules.GrowthArrivalCandidateBoundToZone(candidate,
				"zone-other"));
			CollectionAssert.AreEqual(beforeWrongZone, Bytes(legacy));
			Assert.IsTrue(KingdomLifecycleRules.GrowthArrivalCandidateBoundToZone(candidate,
				"zone-first"));
			Assert.IsTrue(KingdomLifecycleRules.TryInterposeLegacyPreparedFirstGuest(legacy,
				candidate, true, true, true, true, true, 130L));
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.AwaitingChoice,
				candidate.Phase, "the bound zone may run the exact five-proof recovery");

			KingdomGrowthBook v1Source = EnabledGrowth();
			KingdomGrowthArrivalCandidate current = PrepareOrdinary(v1Source,
				v1Source.NextArrivalTick);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(v1Source,
				current));
			KingdomGrowthBook v1 = KingdomLifecycleWireCodec.ReadGrowthPayload(
				KingdomLifecycleWireCodec.GrowthV1PayloadFixture(v1Source));
			Assert.IsTrue(v1.ArrivalCandidate.LegacyGrowthV1UnboundZone);
			Assert.IsNull(v1.ArrivalCandidate.LodgingZoneId);
			Assert.IsTrue(KingdomLifecycleRules.BindLegacyGrowthArrivalCandidateZone(v1,
				v1.ArrivalCandidate, "zone-current", 130L));
			Assert.IsTrue(KingdomLifecycleRules.GrowthArrivalCandidateBoundToZone(
				v1.ArrivalCandidate, "zone-current"));
			Assert.IsFalse(KingdomLifecycleRules.GrowthArrivalCandidateBoundToZone(
				v1.ArrivalCandidate, "zone-other"));
		}

		[Test]
		public void RichGrowthV2OutboxDecodesAndUpgradesWithoutMisalignment()
		{
			KingdomGrowthBook growth = EnabledGrowth();
			KingdomGrowthOperation operation = KingdomLifecycleRules.PrepareGrowthOperation(
				growth, KingdomGrowthAction.Arrival, null, growth.NextArrivalTick);
			Assert.NotNull(operation);
			operation.ArrivalDisposition = KingdomGrowthArrivalDisposition.NoGround;
			KingdomGrowthOutboxEvent notice =
				KingdomLifecycleRules.PrepareDeclaredGrowthOutboxEvent(operation, 0,
					"v2-rich", "chronicle", "official chronicle", "outsider chronicle",
					"ledger", null, null, null, 0, Digest('1'), 1, Digest('2'),
					0, Digest('3'), 1, Digest('4'), 0, Digest('5'), 1, Digest('6'));
			Assert.NotNull(notice);
			operation.OutboxEvents.Add(notice);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, operation));

			byte[] v2 = KingdomLifecycleWireCodec.GrowthV2PayloadFixture(growth);
			Assert.AreEqual(KingdomLifecycleRules.PreviousGrowthFormatVersion,
				BitConverter.ToInt32(v2, 4));
			KingdomGrowthBook loaded = KingdomLifecycleWireCodec.ReadGrowthPayload(v2);
			Assert.IsFalse(loaded.Quarantined, loaded.Fault);
			Assert.AreEqual(KingdomLifecycleRules.CurrentGrowthFormatVersion,
				loaded.FormatVersion);
			Assert.AreEqual("official chronicle",
				loaded.ArrivalOp.OutboxEvents[0].ChronicleOfficial);
			Assert.AreEqual("outsider chronicle",
				loaded.ArrivalOp.OutboxEvents[0].ChronicleOutsider);
			Assert.AreEqual(Digest('4'),
				loaded.ArrivalOp.OutboxEvents[0].OutsiderDeclaredAfterHash);
			KingdomGrowthBook current = RoundTrip(loaded);
			Assert.IsFalse(current.Quarantined, current.Fault);
			Assert.AreEqual(1, current.ArrivalOp.OutboxEvents.Count);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void PostW0ReleaseCutConvergesWithoutReacquiringFullCapacity(bool joined)
		{
			KingdomGrowthBook growth = Published();
			KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
			KingdomExperienceLedger experience = Experience();
			KingdomExperienceBodyReservation body = Body(candidate, 121L, 1L);
			Reserve(experience, body);
			Assert.IsTrue(KingdomLifecycleRules.TryAdmitGrowthFirstGuest(growth, candidate,
				body, 121L));
			MoveCandidateToObserved(growth, candidate, joined, 122L);
			KingdomGrowthOperation operation = joined
				? JoinedOperation(growth, candidate, 126L)
				: RefusedOperation(growth, candidate, 126L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, operation));
			if (joined)
			{
				Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
					KingdomGrowthPhase.WaterIntent, 127L));
				ProveWater(growth, operation, 0);
				Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
					KingdomGrowthPhase.WaterSettled, 128L));
				SettleCandidateDisposition(growth, candidate, operation, true, 129L);
				Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
					KingdomGrowthPhase.DomainIntent, 131L));
				for (int i = 0; i < operation.DomainSteps.Count; i++)
					ProveDomain(growth, operation, i);
				Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
					KingdomGrowthPhase.DomainSettled, 132L));
			}
			else SettleCandidateDisposition(growth, candidate, operation, false, 127L);
			Assert.IsTrue(KingdomLifecycleRules.GrowthFirstGuestBodyReleaseReady(growth,
				candidate));

			Assert.IsTrue(KingdomExperienceRules.TryReleaseBodies(experience,
				experience.Revision, body.ReservationId, body.SourceId,
				out KingdomExperienceCapacityFault _, out string failure), failure);
			Reserve(experience, OtherBody("cut-seven", 7));
			Reserve(experience, OtherBody("cut-fourteen", 7));
			Reserve(experience, OtherBody("cut-sixteen", 2));
			Assert.AreEqual(16, KingdomExperienceRules.ReservedBodies(experience));
			Assert.IsFalse(KingdomLifecycleRules.GrowthFirstGuestBodyLeaseRecoveryRequired(
				growth, candidate), "release-ready recovery must not recreate a missing lease");
			Assert.IsTrue(KingdomExperienceRules.TryReleaseBodies(experience,
				experience.Revision, body.ReservationId, body.SourceId,
				out KingdomExperienceCapacityFault _, out failure), failure);
			Assert.IsTrue(KingdomLifecycleRules.TryMarkGrowthFirstGuestBodyReleased(growth,
				candidate, body.ReservationId, 200L));
			Assert.AreEqual(KingdomGrowthFirstGuestBodyLeaseState.Released,
				candidate.FirstGuest.BodyLeaseState);
			Assert.AreEqual(16, KingdomExperienceRules.ReservedBodies(experience));
		}

		[Test]
		public void DeferredCurrentApplicabilityFailuresLeaveGrowthAndW0ByteExact()
		{
			KingdomGrowthBook growth = Published();
			KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
			Assert.IsTrue(KingdomLifecycleRules.TryDeferGrowthFirstGuest(growth, candidate,
				121L));
			KingdomExperienceLedger experience = Experience();
			byte[] growthBefore = Bytes(growth);
			byte[] experienceBefore = KingdomExperienceCodec.EncodeEnvelope(experience);
			int[][] changed =
			{
				new[] { 20, 20, 4, 10, 20, 2 },
				new[] { 4, 20, 2, 4, 20, 2 },
				new[] { 3, 20, 4, 10, 1, 2 }
			};
			for (int i = 0; i < changed.Length; i++)
			{
				int[] facts = changed[i];
				Assert.IsFalse(KingdomLifecycleRules.TryCheckGrowthFirstGuestCurrentApplicability(
					growth, candidate, facts[0], facts[1], facts[2], facts[3], facts[4],
					facts[5], out string failure), "changed fact " + i);
				Assert.IsNotEmpty(failure);
				CollectionAssert.AreEqual(growthBefore, Bytes(growth), "Growth " + i);
				CollectionAssert.AreEqual(experienceBefore,
					KingdomExperienceCodec.EncodeEnvelope(experience), "W0 " + i);
			}
			Assert.AreEqual(KingdomGrowthFirstGuestChoiceState.Deferred,
				candidate.FirstGuest.ChoiceState);
			Assert.AreEqual(0, experience.BodyReservations.Count);
		}

		private static void RetireDeclinedFirstGuest(KingdomGrowthBook growth)
		{
			KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
			Assert.IsTrue(KingdomLifecycleRules.TryDeclineGrowthFirstGuest(growth, candidate,
				121L));
			KingdomGrowthOperation operation = KingdomLifecycleRules.PrepareGrowthOperation(
				growth, KingdomGrowthAction.Arrival, null, 122L);
			Assert.NotNull(operation);
			operation.ArrivalDisposition = KingdomGrowthArrivalDisposition.Declined;
			operation.ArrivalCandidateId = candidate.Id;
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, operation));
			Assert.IsTrue(KingdomLifecycleRules.TryBindDeclinedFirstGuestOperation(growth,
				candidate, 122L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.ClockIntent, 123L));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(growth, operation,
				operation.ClockLease.Before));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(growth, operation,
				operation.ClockLease.After));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.Sinks, 124L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.Terminal, 125L));
			Assert.IsTrue(KingdomLifecycleRules.RetireGrowth(growth, operation, 126L));
			Assert.IsTrue(KingdomLifecycleRules.RetireGrowthArrivalCandidate(growth,
				candidate));
		}

		private static KingdomGrowthArrivalCandidate PrepareOrdinary(KingdomGrowthBook growth,
			long tick)
		{
			return KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth,
				"taf:marker:ordinary:" + growth.ArrivalCandidateNextSequence,
				"r_KingdomSettler",
				"taf:escrow:ordinary:" + growth.ArrivalCandidateNextSequence,
				"zone-first", tick, Digest('1'), Digest('2'), Digest('3'), 1,
				"taf:semantic:ordinary", 17U, "the salt dunes", "Water",
				"Bey", "the Ides of Uulu Ut, 218 AR", 4, 5);
		}

		private static void MoveCandidateToObserved(KingdomGrowthBook growth,
			KingdomGrowthArrivalCandidate candidate, bool joined, long tick)
		{
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateCreate(growth,
				candidate, tick));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalCandidateCreate(growth,
				candidate, "taf:object:" + candidate.Sequence, Digest('4'), Digest('5'),
				Digest('6'), Digest('7'), true, tick + 1L));
			if (candidate.FirstGuest?.RulesVersion == 2)
			{
				Assert.IsTrue(KingdomLifecycleRules.TryHostGrowthFirstGuest(growth,
					candidate, tick + 1L));
				Assert.IsTrue(KingdomLifecycleRules.TryBeginGrowthFirstGuestCitizenship(growth,
					candidate, tick + 1L));
				Assert.IsTrue(KingdomLifecycleRules.TryPrepareGrowthFirstGuestCitizenship(growth,
					candidate, tick + 1L));
			}
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalLodgingObservation(growth,
				candidate, "zone-first", 4, 5, Digest('8'), tick + 2L));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalLodgingObservation(growth,
				candidate, joined ? KingdomGrowthArrivalDisposition.Joined
					: KingdomGrowthArrivalDisposition.NoAcceptableHome,
				joined ? KingdomGrowthArrivalRefusalReason.None
					: KingdomGrowthArrivalRefusalReason.Refused,
				Digest('9'), Digest('a'), true, tick + 3L));
		}

		private static void MoveCandidateToHosted(KingdomGrowthBook growth,
			KingdomGrowthArrivalCandidate candidate, long tick)
		{
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateCreate(growth,
				candidate, tick));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalCandidateCreate(growth,
				candidate, "taf:object:" + candidate.Sequence, Digest('4'), Digest('5'),
				Digest('6'), Digest('7'), true, tick + 1L));
			Assert.IsTrue(KingdomLifecycleRules.TryHostGrowthFirstGuest(growth,
				candidate, tick + 1L));
		}

		private static KingdomGrowthOperation JoinedOperation(KingdomGrowthBook growth,
			KingdomGrowthArrivalCandidate candidate, long tick)
		{
			KingdomGrowthOperation operation = KingdomLifecycleRules.PrepareGrowthOperation(
				growth, KingdomGrowthAction.Arrival, null, tick);
			Assert.NotNull(operation);
			operation.ArrivalDisposition = KingdomGrowthArrivalDisposition.Joined;
			operation.ArrivalCandidateId = candidate.Id;
			operation.TargetId = candidate.ObjectId;
			operation.TargetMarker = candidate.Marker;
			operation.Blueprint = candidate.Blueprint;
			operation.ZoneId = candidate.LodgingZoneId;
			operation.TargetTopology = KingdomLifecycleTopology.Cell;
			operation.TargetLocation = KingdomGrowthLocationKind.Cell;
			operation.TargetOwnerId = null;
			operation.TargetX = candidate.LodgingX;
			operation.TargetY = candidate.LodgingY;
			operation.PopulationBefore = 3;
			operation.PopulationDelta = 1;
			operation.PopulationAfter = 4;
			operation.WaterLegs.Add(Water(growth, operation, "taf:water:" + candidate.Sequence));
			operation.DomainSteps.Add(Domain(growth, operation,
				KingdomGrowthDomainStepKind.Enrollment,
				KingdomGrowthDomainCallbackKind.Enroll, candidate.ObjectId, candidate.ObjectId,
				0L, 1L));
			operation.DomainSteps.Add(Domain(growth, operation,
				KingdomGrowthDomainStepKind.Roster,
				KingdomGrowthDomainCallbackKind.RosterAdd, candidate.ObjectId,
				candidate.ObjectId, 0L, 1L));
			operation.DomainSteps.Add(Domain(growth, operation,
				KingdomGrowthDomainStepKind.Creed,
				KingdomGrowthDomainCallbackKind.CreedSet, candidate.ObjectId,
				candidate.ObjectId, 0L, 1L));
			operation.DomainSteps.Add(Domain(growth, operation,
				KingdomGrowthDomainStepKind.Population,
				KingdomGrowthDomainCallbackKind.PopulationAdjust, candidate.ObjectId,
				growth.SettlementId, 3L, 4L));
			KingdomGrowthAccountingSnapshot after = new KingdomGrowthAccountingSnapshot
			{
				ArrivalCost = 2, Arrivals = 1
			};
			operation.DomainSteps.Add(Accounting(growth, operation,
				new KingdomGrowthAccountingSnapshot(), after));
			return operation;
		}

		private static KingdomGrowthOperation RefusedOperation(KingdomGrowthBook growth,
			KingdomGrowthArrivalCandidate candidate, long tick)
		{
			KingdomGrowthOperation operation = KingdomLifecycleRules.PrepareGrowthOperation(
				growth, KingdomGrowthAction.Arrival, null, tick);
			Assert.NotNull(operation);
			operation.ArrivalDisposition = KingdomGrowthArrivalDisposition.NoAcceptableHome;
			operation.ArrivalCandidateId = candidate.Id;
			return operation;
		}

		private static void SettleCandidateDisposition(KingdomGrowthBook growth,
			KingdomGrowthArrivalCandidate candidate, KingdomGrowthOperation operation,
			bool joined, long tick)
		{
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateDisposition(growth,
				candidate, operation.Id, joined ? KingdomGrowthObjectMutationKind.CellAdd
					: KingdomGrowthObjectMutationKind.Obliterate,
				joined ? KingdomGrowthLocationKind.Cell : KingdomGrowthLocationKind.Graveyard,
				null, joined ? candidate.LodgingZoneId : null,
				joined ? candidate.LodgingX : -1, joined ? candidate.LodgingY : -1,
				Digest('1'), Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6'),
				tick));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalCandidateDisposition(growth,
				candidate, Digest('7'), joined, tick + 1L));
		}

		private static KingdomGrowthWaterLeg Water(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, string containerId)
		{
			KingdomGrowthWaterLeg leg = KingdomLifecycleRules.PrepareGrowthWaterLeg(growth,
				operation, KingdomGrowthWaterMutationKind.Drain, containerId,
				KingdomLifecycleTopology.Cell, null, "Waterskin", "zone-first", 1, 1,
				10, 4, 2, "fresh", "fresh", Digest('1'), Digest('2'), Digest('3'),
				Digest('4'), Digest('5'), Digest('6'));
			Assert.NotNull(leg); return leg;
		}

		private static KingdomGrowthDomainStep Domain(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, KingdomGrowthDomainStepKind kind,
			KingdomGrowthDomainCallbackKind callback, string actor, string subject,
			long before, long after)
		{
			KingdomGrowthDomainStep step = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				operation, kind, callback, actor, subject, before, after, Digest('1'),
				Digest('2'), Digest('3'), Digest('4'), Digest('5'));
			Assert.NotNull(step); return step;
		}

		private static KingdomGrowthDomainStep Accounting(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, KingdomGrowthAccountingSnapshot before,
			KingdomGrowthAccountingSnapshot after)
		{
			KingdomGrowthDomainStep step = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				operation, KingdomGrowthDomainStepKind.Accounting,
				KingdomGrowthDomainCallbackKind.AccountingSet, growth.SettlementId,
				growth.SettlementId, operation.Sequence - 1L, operation.Sequence, Digest('1'),
				Digest('2'), Digest('3'), Digest('4'), Digest('5'), null, null, before, after);
			Assert.NotNull(step); return step;
		}

		private static void ProveWater(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, int ordinal)
		{
			KingdomGrowthWaterLeg leg = operation.WaterLegs[ordinal];
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthWaterCallback(growth, operation,
				ordinal));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthWaterCallback(growth, operation,
				ordinal, leg.ContainerId, Digest('a'), true, leg.AfterOwnerGraphHash,
				leg.AfterPartGraphHash, leg.AfterTopologyHash));
		}

		private static void ProveDomain(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, int ordinal)
		{
			KingdomGrowthDomainStep step = operation.DomainSteps[ordinal];
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthDomainCallback(growth, operation,
				ordinal));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthDomainCallback(growth, operation,
				ordinal, step.AfterValue, step.AfterGraphHash, step.AfterMapHash, null, null));
		}

		private static string Digest(char value)
		{
			return new string(value, 64);
		}

		private static KingdomSettlement SettlementWith(KingdomLifecycleBook parent)
		{
			KingdomSettlement value = new KingdomSettlement
			{
				SettlementName = "First Guest Test", LifecycleBook = parent
			};
			value.City.SettlementId = Settlement;
			return value;
		}

		private static KingdomGrowthBook Published()
		{
			KingdomGrowthBook growth = EnabledGrowth();
			KingdomGrowthArrivalCandidate candidate = Prepare(growth);
			Assert.NotNull(candidate);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(
				growth, candidate));
			Assert.AreEqual(1UL, growth.ArrivalOrdinalHighWater,
				"historical-cadence publication must atomically cover its live candidate");
			return growth;
		}

		private static KingdomGrowthArrivalCandidate Prepare(KingdomGrowthBook growth,
			string blueprint = "r_KingdomSettler")
		{
			return KingdomLifecycleRules.PrepareGrowthFirstGuestCandidate(growth,
				"taf:marker:first-guest", blueprint, "taf:escrow:first-guest", "zone-first",
				120L, Hash, Hash, Hash, 1, "taf:semantic:first-guest", 17U, "Joppa",
				"Water", "Ari", "the Ides of Uulu Ut, 218 AR", 4, 5,
				growth.NextArrivalTick, growth.ArrivalIntervalTicks, 3, 20, 4, 10, 20, 2);
		}

		private static KingdomLifecycleBook EnabledParent()
		{
			KingdomLifecycleBook source = new KingdomLifecycleBook();
			Assert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(source, Settlement,
				false, null, new List<string>()));
			byte[] v5;
			using (MemoryStream stream = new MemoryStream())
			{
				KingdomLifecycleWireCodec.WriteLifecycleV5Fixture(
					new BinaryWriter(stream), source);
				v5 = stream.ToArray();
			}
			KingdomLifecycleBook parent = new KingdomLifecycleBook();
			using (MemoryStream stream = new MemoryStream(v5, false))
				KingdomLifecycleWireCodec.ReadLifecycle(new BinaryReader(stream), parent,
					new KingdomGrowthMigrationInput
					{
						HasNow = true, Now = 100L, OptionEnabled = true,
						ScarcityEnabled = false, Healthy = true, ArrivalIntervalTicks = 20L
					});
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(parent));
			return parent;
		}

		private static KingdomGrowthBook EnabledGrowth()
		{
			return EnabledParent().Growth;
		}

		private static KingdomGrowthBook RoundTrip(KingdomGrowthBook growth)
		{
			return KingdomLifecycleWireCodec.ReadGrowthPayload(Bytes(growth));
		}

		private static byte[] Bytes(KingdomGrowthBook growth)
		{
			return KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
		}

		private static KingdomExperienceBodyReservation Body(
			KingdomGrowthArrivalCandidate candidate, long tick, long epoch)
		{
			return new KingdomExperienceBodyReservation
			{
				ReservationId = KingdomLifecycleRules.GrowthFirstGuestBodyReservationId(
					candidate.FirstGuest.OpportunityId), RealmId = Realm,
				SettlementId = Settlement, SourceId = candidate.FirstGuest.OpportunityId,
				Lane = KingdomExperienceLane.FirstGuest,
				OptionKind = KingdomExperienceOptionKind.CivicStory,
				CauseTick = tick, ReservedTick = tick, EnableEpoch = epoch, BodyCount = 1
			};
		}

		private static KingdomExperienceAudienceReceipt FirstGuestAudience(
			KingdomGrowthArrivalCandidate candidate, KingdomExperienceLedger experience,
			long tick)
		{
			return new KingdomExperienceAudienceReceipt
			{
				ReservationId = KingdomLifecycleRules.GrowthFirstGuestAudienceReservationId(
					candidate.FirstGuest.OpportunityId), RealmId = Realm,
				SettlementId = Settlement, SourceId = candidate.FirstGuest.OpportunityId,
				Lane = KingdomExperienceLane.FirstGuest,
				OptionKind = KingdomExperienceOptionKind.CivicStory,
				CauseTick = tick, ReservedTick = tick,
				EnableEpoch = experience.Story.EnableEpoch
			};
		}

		private static KingdomExperienceAudienceReceipt CuratorAudience(
			KingdomCuriosityReceipt curiosity, KingdomExperienceLedger experience, long tick)
		{
			return new KingdomExperienceAudienceReceipt
			{
				ReservationId = KingdomCuriosityRules.AttentionReservationId(curiosity.SourceId),
				RealmId = Realm, SettlementId = Settlement, SourceId = curiosity.SourceId,
				Lane = KingdomExperienceLane.Curator,
				OptionKind = KingdomExperienceOptionKind.CivicKnowledge,
				CauseTick = tick, ReservedTick = tick,
				EnableEpoch = experience.Knowledge.EnableEpoch
			};
		}

		private static KingdomCivicMemoryAuthority KnowledgeAuthority()
		{
			KingdomCivicMemoryFamilyTable table = new KingdomCivicMemoryFamilyTable();
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++)
				table.Add(id, id == KingdomCivicMemoryLimits.SectionCuriosity
					? ReadCuriosity : id == KingdomCivicMemoryLimits.SectionCivicLeads
						? ReadLeads : ReadAnything);
			return new KingdomCivicMemoryAuthority(table);
		}

		private static KingdomCivicMemoryNested ReadCuriosity(byte[] payload, out string fault)
		{
			return MemoryVerdict(KingdomCuriosityLeadCodec.DecodeCuriosity(payload).State,
				out fault);
		}

		private static KingdomCivicMemoryNested ReadLeads(byte[] payload, out string fault)
		{
			return MemoryVerdict(KingdomCuriosityLeadCodec.DecodeLeads(payload).State,
				out fault);
		}

		private static KingdomCivicMemoryNested MemoryVerdict(
			KingdomCuriosityBookState state, out string fault)
		{
			fault = state == KingdomCuriosityBookState.Quarantined ? "unreadable" : "";
			return state == KingdomCuriosityBookState.FutureOpaque
				? KingdomCivicMemoryNested.Future : state == KingdomCuriosityBookState.Quarantined
					? KingdomCivicMemoryNested.Malformed : KingdomCivicMemoryNested.Current;
		}

		private static KingdomCivicMemoryNested ReadAnything(byte[] payload, out string fault)
		{
			fault = ""; return KingdomCivicMemoryNested.Current;
		}

		private static KingdomExperienceBodyReservation OtherBody(string id, int count)
		{
			return new KingdomExperienceBodyReservation
			{
				ReservationId = "taf:experience-body:" + id, RealmId = Realm,
				SettlementId = Settlement, SourceId = "taf:source:" + id,
				Lane = KingdomExperienceLane.FirstFeast,
				OptionKind = KingdomExperienceOptionKind.CivicStory,
				CauseTick = 10L, ReservedTick = 10L, EnableEpoch = 1L, BodyCount = count
			};
		}

		private static KingdomExperienceLedger Experience()
		{
			KingdomExperienceLedger value = new KingdomExperienceLedger();
			Assert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(value, Realm,
				out string failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(value, value.Revision,
				true, true, true, 10L, out failure), failure);
			return value;
		}

		private static void Reserve(KingdomExperienceLedger ledger,
			KingdomExperienceBodyReservation row)
		{
			Assert.IsTrue(KingdomExperienceRules.TryReserveBodies(ledger, ledger.Revision,
				row, 0, out KingdomExperienceCapacityFault _, out string failure), failure);
		}
	}
}
#endif
