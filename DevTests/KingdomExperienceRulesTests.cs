#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomExperienceRulesTests
	{
		private const string Realm = "taf:realm:experience-test";

		private static KingdomExperienceLedger Enabled(long tick = 10L)
		{
			KingdomExperienceLedger l = new KingdomExperienceLedger();
			Assert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(l, Realm,
				out string failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(l, l.Revision,
				true, true, true, tick, out failure), failure);
			return l;
		}

		private static KingdomExperienceAudienceReceipt Audience(string suffix,
			string settlement = "taf:settlement:one", long cause = 10L, long reserved = 10L,
			long epoch = 1L, KingdomExperienceLane lane = KingdomExperienceLane.CivicVoices,
			KingdomExperienceOptionKind option = KingdomExperienceOptionKind.CivicStory,
			string source = null)
		{
			return new KingdomExperienceAudienceReceipt
			{
				ReservationId = "taf:experience-audience:" + suffix,
				RealmId = Realm,
				SettlementId = settlement, SourceId = source ?? "taf:event:" + suffix,
				Lane = lane, OptionKind = option,
				CauseTick = cause, ReservedTick = reserved, EnableEpoch = epoch
			};
		}

		private static KingdomExperienceBodyReservation Body(string suffix, int count = 1,
			long cause = 10L, long reserved = 10L, long epoch = 1L,
			KingdomExperienceLane lane = KingdomExperienceLane.FirstGuest,
			KingdomExperienceOptionKind option = KingdomExperienceOptionKind.CivicStory,
			string source = null, string settlement = "taf:settlement:one")
		{
			return new KingdomExperienceBodyReservation
			{
				ReservationId = "taf:experience-body:" + suffix,
				RealmId = Realm,
				SettlementId = settlement, SourceId = source ?? "taf:event:" + suffix,
				Lane = lane, OptionKind = option,
				CauseTick = cause, ReservedTick = reserved, EnableEpoch = epoch,
				BodyCount = count
			};
		}

		[Test]
		public void W0BudgetsAreExactAndDeclared()
		{
			Assert.AreEqual(3, KingdomExperienceRules.MaxSettlements);
			Assert.AreEqual(1, KingdomExperienceRules.MaxAudienceReceipts /
				KingdomExperienceRules.MaxSettlements);
			Assert.AreEqual(16, KingdomExperienceRules.MaxTransientBodySlots);
			Assert.AreEqual(16, KingdomExperienceRules.MaxBodyReservations);
			Assert.AreEqual(7, KingdomExperienceRules.MaxBodiesPerReservation);
			Assert.Less(KingdomExperienceRules.MaxDeclaredPayloadBytes,
				KingdomExperienceCodec.MaxEnvelopeBytes - 12);
		}

		[Test]
		public void LoadRoutingRunsOnlyCommittedCapacityWhileCentralWorkIsBlocked()
		{
			Assert.AreEqual(KingdomLoadReconciliationMode.None,
				KingdomLoadReconciliationRules.Select(false, false));
			Assert.AreEqual(KingdomLoadReconciliationMode.None,
				KingdomLoadReconciliationRules.Select(false, true));
			Assert.AreEqual(KingdomLoadReconciliationMode.CommittedCapacityOnly,
				KingdomLoadReconciliationRules.Select(true, false));
			Assert.AreEqual(KingdomLoadReconciliationMode.Full,
				KingdomLoadReconciliationRules.Select(true, true));
		}

		[Test]
		public void MasterPublicationGateRefusesEveryBoundaryBeforeAnyOwnerChanges()
		{
			const int count = KingdomMasterPublicationGate.MaxParticipants;
			bool[] exact = new bool[count];
			byte[][] owners = new byte[count][];
			byte[][] targets = new byte[count][];
			for (int i = 0; i < count; i++)
			{
				exact[i] = true; owners[i] = new byte[] { (byte)i };
				targets[i] = new byte[] { (byte)(i + 20) };
			}
			for (int injected = 0; injected < count; injected++)
			{
				byte[][] before = CloneOwners(owners);
				Assert.IsFalse(KingdomMasterPublicationGate.TryOpen(exact, count,
					injected, out string _));
				AssertOwners(before, owners);
			}
			Assert.IsTrue(KingdomMasterPublicationGate.TryOpen(exact, count, -1,
				out string failure), failure);
			for (int i = 0; i < count; i++) owners[i] = (byte[])targets[i].Clone();
			AssertOwners(targets, owners);
		}

		[Test]
		public void SharedBodyUnionProtectsQueuedDeliveriesWithoutDoubleCountingBindings()
		{
			int[] bindings = new int[] { 1, 2 };
			int[] deliveries = new int[] { 2, 3, 4 };
			Assert.IsTrue(KingdomSharedBodyCapacityRules.TryCountFoundationClaims(bindings,
				deliveries, out int count, out KingdomExperienceCapacityFault fault,
				out string failure), failure);
			Assert.AreEqual(4, count);
			Assert.IsTrue(KingdomSharedBodyCapacityRules.TryAdmitFoundationClaims(bindings,
				deliveries, new int[] { 4 }, 12, out count, out fault, out failure), failure);
			Assert.AreEqual(4, count, "binding a queued delivery must consume its existing claim");
			Assert.IsFalse(KingdomSharedBodyCapacityRules.TryAdmitFoundationClaims(bindings,
				deliveries, new int[] { 5 }, 12, out count, out fault, out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.LiveBodyCapacityFull, fault);
			Assert.IsFalse(KingdomSharedBodyCapacityRules.TryAdmitNewFoundationClaims(bindings,
				deliveries, 1, 12, out count, out fault, out failure));
			Assert.AreEqual("CapacityFull(foundation-bodies:realm)", failure);

			KingdomExperienceLedger ledger = Enabled();
			Reserve(ledger, Body("optional-twelve", 7), 4);
			Reserve(ledger, Body("optional-five", 5, source: "taf:event:optional-five"), 4);
			byte[] atCap = KingdomExperienceCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomExperienceRules.TryReserveBodies(ledger, ledger.Revision,
				Body("optional-cap-plus-one"), 4, out fault, out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.LiveBodyCapacityFull, fault);
			CollectionAssert.AreEqual(atCap, KingdomExperienceCodec.EncodeEnvelope(ledger));

			int[] legacyBindings = new int[16];
			int[] legacyDeliveries = new int[16];
			for (int i = 0; i < 16; i++)
			{
				legacyBindings[i] = i + 1; legacyDeliveries[i] = i + 17;
			}
			Assert.IsTrue(KingdomSharedBodyCapacityRules.TryCountFoundationClaims(
				legacyBindings, legacyDeliveries, out count, out fault, out failure), failure);
			Assert.AreEqual(32, count);
			Assert.IsTrue(KingdomSharedBodyCapacityRules.TryAdmitFoundationClaims(
				legacyBindings, legacyDeliveries, new int[] { 32 }, 0, out count,
				out fault, out failure), failure);
			Assert.IsFalse(KingdomSharedBodyCapacityRules.TryAdmitFoundationClaims(
				legacyBindings, legacyDeliveries, new int[] { 33 }, 0, out count,
				out fault, out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.LiveBodyCapacityFull, fault);
			Assert.IsFalse(KingdomSharedBodyCapacityRules.TryAdmitNewFoundationClaims(
				legacyBindings, legacyDeliveries, 1, 0, out count, out fault, out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.LiveBodyCapacityFull, fault);
		}

		[TestCase(17)]
		[TestCase(32)]
		public void LegacyOvercapFoundationUnionAllowsOnlyExactRetirementAndRelease(
			int protectedFoundationBodies)
		{
			KingdomExperienceLedger ledger = Enabled();
			const string source = "taf:cohort:legacy-overcap";
			KingdomExperienceAudienceReceipt audience = Audience("legacy-overcap",
				lane: KingdomExperienceLane.PolityCohort,
				option: KingdomExperienceOptionKind.AmbientUse, source: source);
			KingdomExperienceBodyReservation bodies = Body("legacy-overcap", 2,
				lane: KingdomExperienceLane.PolityCohort,
				option: KingdomExperienceOptionKind.AmbientUse, source: source);
			Reserve(ledger, audience, bodies, 0);
			byte[] active = KingdomExperienceCodec.EncodeEnvelope(ledger);
			Assert.IsTrue(KingdomExperienceRules.TryReservePresentation(ledger,
				ledger.Revision, audience, bodies, protectedFoundationBodies,
				out KingdomExperienceCapacityFault fault, out string failure), failure);
			CollectionAssert.AreEqual(active, KingdomExperienceCodec.EncodeEnvelope(ledger));
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(ledger, ledger.Revision,
				true, true, false, 20L, out failure), failure);
			byte[] retirement = KingdomExperienceCodec.EncodeEnvelope(ledger);
			Assert.IsTrue(KingdomExperienceRules.TryRecoverRetirementBodies(ledger,
				ledger.Revision, bodies, protectedFoundationBodies, out fault, out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryRecoverRetirementPresentation(ledger,
				ledger.Revision, audience, bodies, protectedFoundationBodies,
				out fault, out failure), failure);
			CollectionAssert.AreEqual(retirement, KingdomExperienceCodec.EncodeEnvelope(ledger));
			Assert.IsFalse(KingdomExperienceRules.TryRecoverDurableBodies(ledger,
				ledger.Revision, Body("legacy-overcap-missing"), protectedFoundationBodies,
				out fault, out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.LiveBodyCapacityFull, fault);
			CollectionAssert.AreEqual(retirement, KingdomExperienceCodec.EncodeEnvelope(ledger));
			Assert.IsTrue(KingdomExperienceRules.TryReleasePresentation(ledger,
				ledger.Revision, audience.ReservationId, bodies.ReservationId, source,
				out fault, out failure), failure);
			Assert.AreEqual(0, ledger.Audiences.Count);
			Assert.AreEqual(0, ledger.BodyReservations.Count);
		}

		[Test]
		public void MasterResumeReanchorsEveryConfiguredEpochWithoutReplayingPausedCauses()
		{
			KingdomExperienceLedger ledger = Enabled(10L);
			Reserve(ledger, Body("before-master-pause", cause: 10L, reserved: 10L), 0);
			byte[] source = KingdomExperienceCodec.EncodeEnvelope(ledger);
			Assert.IsTrue(KingdomExperienceRules.TryPrepareMasterResume(ledger, Realm,
				20L, 30L, true, false, true, out KingdomExperienceMasterResumePlan plan,
				out string failure), failure);
			CollectionAssert.AreEqual(source, KingdomExperienceCodec.EncodeEnvelope(ledger));
			Assert.IsTrue(KingdomExperienceRules.TryPublishMasterResume(ledger, plan,
				out failure), failure);
			Assert.AreEqual(2L, ledger.Story.EnableEpoch);
			Assert.AreEqual(30L, ledger.Story.FutureCauseFloorTick);
			Assert.AreEqual(KingdomExperienceOptionState.Disabled, ledger.Knowledge.State);
			Assert.IsFalse(KingdomExperienceRules.CanEmit(ledger,
				KingdomExperienceOptionKind.CivicStory, 29L));
			Assert.IsTrue(KingdomExperienceRules.CanEmit(ledger,
				KingdomExperienceOptionKind.CivicStory, 30L));
			Assert.IsTrue(KingdomExperienceRules.TryReadBodyLease(ledger,
				"taf:experience-body:before-master-pause", out KingdomExperienceBodyReservation _,
				out KingdomExperienceLeaseState state, out failure), failure);
			Assert.AreEqual(KingdomExperienceLeaseState.Retirement, state);
			byte[] published = KingdomExperienceCodec.EncodeEnvelope(ledger);
			Assert.IsTrue(KingdomExperienceRules.TryPublishMasterResume(ledger, plan,
				out failure), failure);
			CollectionAssert.AreEqual(published, KingdomExperienceCodec.EncodeEnvelope(ledger));

			KingdomExperienceLedger impossible = Enabled(10L);
			Reserve(impossible, Body("during-pause", cause: 10L, reserved: 25L), 0);
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(impossible);
			Assert.IsFalse(KingdomExperienceRules.TryPrepareMasterResume(impossible, Realm,
				20L, 30L, true, true, true, out plan, out failure));
			StringAssert.Contains("during the master pause", failure);
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(impossible));

			KingdomExperienceLedger equalTick = Enabled(10L);
			Reserve(equalTick, Body("equal-disable-tick", cause: 20L, reserved: 20L), 0);
			Assert.IsTrue(KingdomExperienceRules.TryPrepareMasterResume(equalTick, Realm,
				20L, 20L, true, true, true, out plan, out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryPublishMasterResume(equalTick, plan,
				out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryReadBodyLease(equalTick,
				"taf:experience-body:equal-disable-tick", out KingdomExperienceBodyReservation _,
				out state, out failure), failure);
			Assert.AreEqual(KingdomExperienceLeaseState.Retirement, state,
				"equal tick means committed before the transition that consumes that wake");

			KingdomExperienceLedger divergent = Enabled(10L);
			Assert.IsTrue(KingdomExperienceRules.TryPrepareMasterResume(divergent, Realm,
				20L, 30L, true, true, true, out plan, out failure), failure);
			long sameRevision = divergent.Revision;
			divergent.Ambient.State = KingdomExperienceOptionState.Disabled;
			divergent.Ambient.FutureCauseFloorTick = long.MaxValue;
			Assert.AreEqual(sameRevision, divergent.Revision);
			Assert.IsTrue(KingdomExperienceRules.TryValidate(divergent, out failure), failure);
			Assert.IsFalse(KingdomExperienceRules.TryPublishMasterResume(divergent, plan,
				out failure));
			StringAssert.Contains("staged CAS", failure);
		}

		[Test]
		public void DurableRecoveryRepairsExactProjectedProofInActiveOrRetirementEpoch()
		{
			KingdomExperienceLedger ledger = Enabled(10L);
			KingdomExperienceBodyReservation request = Body("durable-repair", 3,
				lane: KingdomExperienceLane.PolityCohort,
				source: "taf:cohort:durable-repair");
			Assert.IsTrue(KingdomExperienceRules.TryClassifyLeaseProof(ledger,
				request.OptionKind, request.CauseTick, request.ReservedTick,
				request.EnableEpoch, out KingdomExperienceLeaseState proofState,
				out string failure), failure);
			Assert.AreEqual(KingdomExperienceLeaseState.Active, proofState);
			Assert.IsFalse(KingdomExperienceRules.TryRecoverRetirementBodies(ledger,
				ledger.Revision, request, 2, out KingdomExperienceCapacityFault _,
				out string _));
			Assert.IsTrue(KingdomExperienceRules.TryRecoverDurableBodies(ledger,
				ledger.Revision, request, 2, out _, out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryReleaseBodies(ledger, ledger.Revision,
				request.ReservationId, request.SourceId, out _, out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(ledger, ledger.Revision,
				false, true, true, 20L, out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryClassifyLeaseProof(ledger,
				request.OptionKind, request.CauseTick, request.ReservedTick,
				request.EnableEpoch, out proofState, out failure), failure);
			Assert.AreEqual(KingdomExperienceLeaseState.Retirement, proofState);
			Assert.IsTrue(KingdomExperienceRules.TryRecoverDurableBodies(ledger,
				ledger.Revision, request, 2, out _, out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryReadBodyLease(ledger, request.ReservationId,
				out KingdomExperienceBodyReservation _, out KingdomExperienceLeaseState state,
				out failure), failure);
			Assert.AreEqual(KingdomExperienceLeaseState.Retirement, state);
		}

		[Test]
		public void BlankAndBoundLedgersRoundTripCanonically()
		{
			KingdomExperienceLedger blank = new KingdomExperienceLedger();
			Assert.IsTrue(KingdomExperienceRules.TryValidate(blank, out string failure), failure);
			byte[] blankWire = KingdomExperienceCodec.EncodeEnvelope(blank);
			CollectionAssert.AreEqual(blankWire, KingdomExperienceCodec.EncodeEnvelope(
				KingdomExperienceCodec.DecodeEnvelope(blankWire)));

			KingdomExperienceLedger enabled = Enabled();
			byte[] wire = KingdomExperienceCodec.EncodeEnvelope(enabled);
			KingdomExperienceLedger read = KingdomExperienceCodec.DecodeEnvelope(wire);
			Assert.AreEqual(Realm, read.RealmId);
			Assert.AreEqual(KingdomExperienceOptionState.Enabled, read.Story.State);
			CollectionAssert.AreEqual(wire, KingdomExperienceCodec.EncodeEnvelope(read));
		}

		[Test]
		public void OptionsDisableWithoutBacklogAndRetainSourceOwnedCapacityLeases()
		{
			KingdomExperienceLedger premature = Enabled(10L);
			Reserve(premature, Body("future-reservation", cause: 10L, reserved: 15L), 0);
			byte[] prematureBytes = KingdomExperienceCodec.EncodeEnvelope(premature);
			Assert.IsFalse(KingdomExperienceRules.TryObserveOptions(premature,
				premature.Revision, false, true, true, 14L, out string prematureFailure));
			StringAssert.Contains("reservation is invalid", prematureFailure);
			CollectionAssert.AreEqual(prematureBytes,
				KingdomExperienceCodec.EncodeEnvelope(premature));

			KingdomExperienceLedger l = Enabled(100L);
			Assert.IsFalse(KingdomExperienceRules.CanEmit(l,
				KingdomExperienceOptionKind.CivicStory, 99L));
			Assert.IsTrue(KingdomExperienceRules.CanEmit(l,
				KingdomExperienceOptionKind.CivicStory, 100L));
			Assert.IsTrue(KingdomExperienceRules.TryGetEnableEpoch(l,
				KingdomExperienceOptionKind.CivicStory, 100L, out long epoch,
				out string epochFailure), epochFailure);
			Assert.AreEqual(1L, epoch);
			Reserve(l, Audience("old", cause: 100L, reserved: 100L));
			Reserve(l, Body("old", cause: 100L, reserved: 100L), 0);
			Assert.AreEqual(1, l.Audiences.Count); Assert.AreEqual(1, l.BodyReservations.Count);

			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(l, l.Revision,
				false, true, true, 200L, out string failure), failure);
			Assert.AreEqual(1, l.Audiences.Count); Assert.AreEqual(1, l.BodyReservations.Count);
			byte[] disabled = KingdomExperienceCodec.EncodeEnvelope(l);
			CollectionAssert.AreEqual(disabled, KingdomExperienceCodec.EncodeEnvelope(
				KingdomExperienceCodec.DecodeEnvelope(disabled)));
			Assert.IsFalse(KingdomExperienceRules.CanEmit(l,
				KingdomExperienceOptionKind.CivicStory, long.MaxValue));
			Assert.IsFalse(KingdomExperienceRules.TryReserveBodies(l, l.Revision,
				Body("disabled-retry", cause: 200L, reserved: 200L), 0,
				out KingdomExperienceCapacityFault disabledFault, out string disabledFailure));
			Assert.AreEqual(KingdomExperienceCapacityFault.OptionDisabled, disabledFault);
			CollectionAssert.AreEqual(disabled, KingdomExperienceCodec.EncodeEnvelope(l));
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(l, l.Revision,
				true, true, true, 300L, out failure), failure);
			Assert.AreEqual(1, l.Audiences.Count); Assert.AreEqual(1, l.BodyReservations.Count);
			Assert.IsFalse(KingdomExperienceRules.CanEmit(l,
				KingdomExperienceOptionKind.CivicStory, 299L));
			Assert.IsTrue(KingdomExperienceRules.CanEmit(l,
				KingdomExperienceOptionKind.CivicStory, 300L));
			Assert.IsTrue(KingdomExperienceRules.TryGetEnableEpoch(l,
				KingdomExperienceOptionKind.CivicStory, 300L, out epoch,
				out epochFailure), epochFailure);
			Assert.AreEqual(2L, epoch);
			byte[] beforeStaleRetry = KingdomExperienceCodec.EncodeEnvelope(l);
			Assert.IsFalse(KingdomExperienceRules.TryReserveAudience(l, l.Revision,
				Audience("old", cause: 100L, reserved: 100L, epoch: 1L),
				out KingdomExperienceCapacityFault fault, out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.CauseBeforeEnable, fault);
			CollectionAssert.AreEqual(beforeStaleRetry, KingdomExperienceCodec.EncodeEnvelope(l));
			Assert.IsTrue(KingdomExperienceRules.TryReleaseAudience(l, l.Revision,
				"taf:experience-audience:old", "taf:event:old", out fault, out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryReleaseBodies(l, l.Revision,
				"taf:experience-body:old", "taf:event:old", out fault, out failure), failure);
			Assert.AreEqual(0, l.Audiences.Count); Assert.AreEqual(0, l.BodyReservations.Count);
			Reserve(l, Audience("new", cause: 300L, reserved: 300L, epoch: 2L));
		}

		[Test]
		public void RetirementLeasesRemainInTheSharedCapAcrossDisableAndReenable()
		{
			KingdomExperienceLedger l = Enabled(10L);
			for (int i = 0; i < 16; i++) Reserve(l, Body("retired-" + i), 0);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(l, l.Revision,
				false, true, true, 20L, out string failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(l, l.Revision,
				true, true, true, 30L, out failure), failure);
			Assert.AreEqual(16, KingdomExperienceRules.ReservedBodies(l));
			byte[] atCap = KingdomExperienceCodec.EncodeEnvelope(l);
			Assert.IsFalse(KingdomExperienceRules.TryReserveBodies(l, l.Revision,
				Body("current-cap-plus-one", cause: 30L, reserved: 30L, epoch: 2L), 0,
				out KingdomExperienceCapacityFault fault, out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.LiveBodyCapacityFull, fault);
			CollectionAssert.AreEqual(atCap, KingdomExperienceCodec.EncodeEnvelope(l));
			CollectionAssert.AreEqual(atCap, KingdomExperienceCodec.EncodeEnvelope(
				KingdomExperienceCodec.DecodeEnvelope(atCap)));
		}

		[Test]
		public void OptionDisableBeforeAReservedTickRefusesByteStably()
		{
			KingdomExperienceLedger l = Enabled(10L);
			Reserve(l, Body("future-reserved", cause: 10L, reserved: 100L), 0);
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(l);
			Assert.IsFalse(KingdomExperienceRules.TryObserveOptions(l, l.Revision,
				false, true, true, 50L, out string failure));
			StringAssert.Contains("body reservation is invalid", failure);
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(l));
		}

		[Test]
		public void ExactRetirementRecoveryIsCapacityOnlyAtomicAndClassified()
		{
			KingdomExperienceLedger l = Enabled(10L);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(l, l.Revision,
				true, true, false, 20L, out string failure), failure);
			const string source = "taf:cohort:retirement-recovery";
			KingdomExperienceAudienceReceipt audience = Audience("retirement-recovery",
				lane: KingdomExperienceLane.PolityCohort,
				option: KingdomExperienceOptionKind.AmbientUse, source: source);
			KingdomExperienceBodyReservation bodies = Body("retirement-recovery", 3,
				lane: KingdomExperienceLane.PolityCohort,
				option: KingdomExperienceOptionKind.AmbientUse, source: source);
			Assert.IsTrue(KingdomExperienceRules.TryRecoverRetirementPresentation(l,
				l.Revision, audience, bodies, 4, out KingdomExperienceCapacityFault fault,
				out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryReadAudienceLease(l,
				audience.ReservationId, out KingdomExperienceAudienceReceipt readAudience,
				out KingdomExperienceLeaseState audienceState, out failure), failure);
			Assert.AreEqual(KingdomExperienceLeaseState.Retirement, audienceState);
			Assert.AreEqual(source, readAudience.SourceId);
			readAudience.SourceId = "taf:event:mutated-copy";
			Assert.AreEqual(source, l.Audiences[0].SourceId);
			Assert.IsTrue(KingdomExperienceRules.TryReadBodyLease(l, bodies.ReservationId,
				out KingdomExperienceBodyReservation readBodies,
				out KingdomExperienceLeaseState bodyState, out failure), failure);
			Assert.AreEqual(KingdomExperienceLeaseState.Retirement, bodyState);
			Assert.AreEqual(3, readBodies.BodyCount);
			long stable = l.Revision;
			Assert.IsTrue(KingdomExperienceRules.TryRecoverRetirementPresentation(l, 0L,
				audience, bodies, 4, out fault, out failure), failure);
			Assert.AreEqual(stable, l.Revision);
			Assert.IsFalse(KingdomExperienceRules.TryReservePresentation(l, l.Revision,
				audience, bodies, 4, out fault, out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.OptionDisabled, fault);
		}

		[Test]
		public void RealmRemovalBlocksEveryLiveW0LeaseAndRefusesUnknownOwnership()
		{
			KingdomExperienceLedger empty = Enabled();
			Assert.IsTrue(KingdomExperienceRules.TryDescribeRealmRemovalBlocker(empty, Realm,
				out string blocker, out string failure), failure);
			Assert.IsNull(blocker);

			KingdomExperienceLedger live = Enabled();
			Reserve(live, Audience("removal-audience"));
			Reserve(live, Body("removal-body"), 0);
			Assert.IsTrue(KingdomExperienceRules.TryDescribeRealmRemovalBlocker(live, Realm,
				out blocker, out failure), failure);
			StringAssert.Contains("1 civic audience lease", blocker);
			StringAssert.Contains("1 transient-body lease", blocker);
			StringAssert.Contains("named source lanes", blocker);

			Assert.IsFalse(KingdomExperienceRules.TryDescribeRealmRemovalBlocker(live,
				"taf:realm:other", out blocker, out failure));
			StringAssert.Contains("another realm", failure);
			Assert.IsFalse(KingdomExperienceRules.TryDescribeRealmRemovalBlocker(null, Realm,
				out blocker, out failure));
			StringAssert.Contains("absent", failure);
		}

		[Test]
		public void RetirementPresentationCapFailureCreatesNoPartialAudience()
		{
			KingdomExperienceLedger l = Enabled(10L);
			for (int i = 0; i < 16; i++) Reserve(l, Body("occupied-" + i), 0);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(l, l.Revision,
				true, true, false, 20L, out string failure), failure);
			const string source = "taf:cohort:retirement-full";
			KingdomExperienceAudienceReceipt audience = Audience("retirement-full",
				"taf:settlement:two", lane: KingdomExperienceLane.PolityCohort,
				option: KingdomExperienceOptionKind.AmbientUse, source: source);
			KingdomExperienceBodyReservation bodies = Body("retirement-full", 1,
				lane: KingdomExperienceLane.PolityCohort,
				option: KingdomExperienceOptionKind.AmbientUse, source: source,
				settlement: "taf:settlement:two");
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(l);
			Assert.IsFalse(KingdomExperienceRules.TryRecoverRetirementPresentation(l,
				l.Revision, audience, bodies, 0, out KingdomExperienceCapacityFault fault,
				out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.LiveBodyCapacityFull, fault);
			Assert.AreEqual(0, l.Audiences.Count);
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(l));
		}

		[Test]
		public void OneAudiencePerSettlementNeverEvictsOrMutatesOnFailure()
		{
			KingdomExperienceLedger l = Enabled();
			Reserve(l, Audience("a"));
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(l);
			Assert.IsFalse(KingdomExperienceRules.TryReserveAudience(l, l.Revision,
				Audience("b"), out KingdomExperienceCapacityFault fault, out string failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.AudienceCapacityFull, fault);
			StringAssert.StartsWith("CapacityFull(audience:", failure);
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(l));

			Reserve(l, Audience("c", "taf:settlement:two"));
			Reserve(l, Audience("d", "taf:settlement:three"));
			Assert.AreEqual(3, l.Audiences.Count);
		}

		[Test]
		public void IdenticalRetryIsNoOpAndIdentityMismatchFailsClosed()
		{
			KingdomExperienceLedger l = Enabled();
			KingdomExperienceAudienceReceipt request = Audience("same");
			long before = l.Revision; Reserve(l, request); long published = l.Revision;
			Assert.Greater(published, before);
			Assert.IsTrue(KingdomExperienceRules.TryReserveAudience(l, before, request,
				out KingdomExperienceCapacityFault fault, out string failure), failure);
			Assert.AreEqual(published, l.Revision);
			KingdomExperienceAudienceReceipt mismatch = Audience("same");
			mismatch.SourceId = "taf:event:different";
			Assert.IsFalse(KingdomExperienceRules.TryReserveAudience(l, l.Revision, mismatch,
				out fault, out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.DuplicateMismatch, fault);
		}

		[Test]
		public void BodyLeaseSharesSixteenSlotLiveBindingCeiling()
		{
			KingdomExperienceLedger l = Enabled();
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(l);
			Assert.IsFalse(KingdomExperienceRules.TryReserveBodies(l, l.Revision, Body("seven", 7),
				10, out KingdomExperienceCapacityFault fault, out string failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.LiveBodyCapacityFull, fault);
			Assert.AreEqual("CapacityFull(live-bodies:realm)", failure);
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(l));

			Reserve(l, Body("six", 6), 10);
			Assert.AreEqual(6, KingdomExperienceRules.ReservedBodies(l));
			Assert.IsFalse(KingdomExperienceRules.TryReserveBodies(l, l.Revision, Body("one", 1),
				10, out fault, out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.LiveBodyCapacityFull, fault);
		}

		[Test]
		public void PolityPresentationAtomicallySharesAudienceAndSixteenBodyAuthority()
		{
			KingdomExperienceLedger l = Enabled();
			Reserve(l, Body("guest-seven", 7), 0);
			Reserve(l, Body("voices-two", 2, lane: KingdomExperienceLane.CivicVoices), 0);
			const string source = "taf:cohort:polity-seven";
			KingdomExperienceAudienceReceipt audience = Audience("polity-seven",
				"taf:settlement:two", lane: KingdomExperienceLane.PolityCohort,
				option: KingdomExperienceOptionKind.AmbientUse, source: source);
			KingdomExperienceBodyReservation polity = Body("polity-seven", 7,
				lane: KingdomExperienceLane.PolityCohort,
				option: KingdomExperienceOptionKind.AmbientUse,
				source: source, settlement: "taf:settlement:two");
			Reserve(l, audience, polity, 0);
			Assert.AreEqual(1, l.Audiences.Count);
			Assert.AreEqual(16, KingdomExperienceRules.ReservedBodies(l));
			byte[] atCap = KingdomExperienceCodec.EncodeEnvelope(l);

			KingdomExperienceAudienceReceipt extraAudience = Audience("cap-plus-one",
				"taf:settlement:three", lane: KingdomExperienceLane.PolityCohort,
				option: KingdomExperienceOptionKind.AmbientUse,
				source: "taf:cohort:cap-plus-one");
			KingdomExperienceBodyReservation extraBody = Body("cap-plus-one",
				lane: KingdomExperienceLane.PolityCohort,
				option: KingdomExperienceOptionKind.AmbientUse,
				source: "taf:cohort:cap-plus-one", settlement: "taf:settlement:three");
			Assert.IsFalse(KingdomExperienceRules.TryReservePresentation(l, l.Revision,
				extraAudience, extraBody, 0,
				out KingdomExperienceCapacityFault fault, out string failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.LiveBodyCapacityFull, fault);
			Assert.AreEqual("CapacityFull(live-bodies:realm)", failure);
			CollectionAssert.AreEqual(atCap, KingdomExperienceCodec.EncodeEnvelope(l));
		}

		[Test]
		public void DirectedBodyBypassSkipsAudienceCompetitionButNeverBodyCapacity()
		{
			KingdomExperienceLedger l = Enabled();
			const string ambientSource = "taf:cohort:ambient";
			Reserve(l, Audience("ambient", source: ambientSource,
				lane: KingdomExperienceLane.PolityCohort,
				option: KingdomExperienceOptionKind.AmbientUse),
				Body("ambient", 7, source: ambientSource,
					lane: KingdomExperienceLane.PolityCohort,
					option: KingdomExperienceOptionKind.AmbientUse), 0);
			byte[] ambientOnly = KingdomExperienceCodec.EncodeEnvelope(l);
			long ambientRevision = l.Revision;
			Reserve(l, Body("directed-seven", 7, source: "taf:cohort:directed-seven",
				lane: KingdomExperienceLane.PolityCohort), 0);
			Reserve(l, Body("directed-two", 2, source: "taf:cohort:directed-two",
				lane: KingdomExperienceLane.PolityCohort), 0);
			Assert.AreEqual(1, l.Audiences.Count);
			Assert.AreEqual(16, KingdomExperienceRules.ReservedBodies(l));
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(l);
			Assert.IsFalse(KingdomExperienceRules.TryReserveBodies(l, l.Revision,
				Body("directed-cap-plus-one", source: "taf:cohort:directed-cap-plus-one",
					lane: KingdomExperienceLane.PolityCohort), 0,
				out KingdomExperienceCapacityFault fault, out string failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.LiveBodyCapacityFull, fault);
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(l));
			Assert.IsFalse(KingdomExperienceRules.TryReleaseAudience(l, l.Revision,
				"taf:experience-audience:ambient", "taf:cohort:directed-seven",
				out fault, out failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.OwnershipMismatch, fault);
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(l));
			Assert.IsTrue(KingdomExperienceRules.TryReleaseBodies(l, l.Revision,
				"taf:experience-body:directed-seven", "taf:cohort:directed-seven",
				out fault, out failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryReleaseBodies(l, l.Revision,
				"taf:experience-body:directed-two", "taf:cohort:directed-two",
				out fault, out failure), failure);
			KingdomExperienceLedger withoutDirected = KingdomExperienceRules.Clone(l);
			withoutDirected.Revision = ambientRevision;
			CollectionAssert.AreEqual(ambientOnly,
				KingdomExperienceCodec.EncodeEnvelope(withoutDirected));
		}

		[Test]
		public void PolityPresentationRoundTripsAndOnlyItsExactCohortCanReleaseIt()
		{
			KingdomExperienceLedger l = Enabled();
			const string source = "taf:cohort:polity-recovery";
			KingdomExperienceAudienceReceipt audience = Audience("polity-recovery",
				lane: KingdomExperienceLane.PolityCohort,
				option: KingdomExperienceOptionKind.AmbientUse, source: source);
			KingdomExperienceBodyReservation polity = Body("polity-recovery", 3,
				lane: KingdomExperienceLane.PolityCohort,
				option: KingdomExperienceOptionKind.AmbientUse,
				source: source);
			Reserve(l, audience, polity, 0);
			KingdomExperienceLedger recovered = KingdomExperienceCodec.DecodeEnvelope(
				KingdomExperienceCodec.EncodeEnvelope(l));
			Assert.AreEqual(KingdomExperienceLane.PolityCohort,
				recovered.Audiences[0].Lane);
			Assert.AreEqual(KingdomExperienceLane.PolityCohort,
				recovered.BodyReservations[0].Lane);
			long stableRevision = recovered.Revision;
			Assert.IsTrue(KingdomExperienceRules.TryReservePresentation(recovered, 0L,
				audience, polity, 0, out KingdomExperienceCapacityFault retryFault,
				out string retryFailure), retryFailure);
			Assert.AreEqual(KingdomExperienceCapacityFault.None, retryFault);
			Assert.AreEqual(stableRevision, recovered.Revision);
			Assert.IsFalse(KingdomExperienceRules.TryReleasePresentation(recovered,
				recovered.Revision, audience.ReservationId, polity.ReservationId,
				"taf:cohort:foreign",
				out KingdomExperienceCapacityFault fault, out string failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.OwnershipMismatch, fault);
			Assert.IsTrue(KingdomExperienceRules.TryReleasePresentation(recovered,
				recovered.Revision, audience.ReservationId, polity.ReservationId,
				polity.SourceId, out fault, out failure), failure);
			Assert.AreEqual(0, recovered.Audiences.Count);
			Assert.AreEqual(0, KingdomExperienceRules.ReservedBodies(recovered));
		}

		[Test]
		public void ReservationReleaseIsOwnedAndIdempotent()
		{
			KingdomExperienceLedger l = Enabled(); Reserve(l, Body("owned", 2), 0);
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(l);
			Assert.IsFalse(KingdomExperienceRules.TryReleaseBodies(l, l.Revision,
				"taf:experience-body:owned", "taf:event:other",
				out KingdomExperienceCapacityFault fault, out string failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.OwnershipMismatch, fault);
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(l));
			Assert.IsTrue(KingdomExperienceRules.TryReleaseBodies(l, l.Revision,
				"taf:experience-body:owned", "taf:event:owned", out fault, out failure), failure);
			long closed = l.Revision;
			Assert.IsTrue(KingdomExperienceRules.TryReleaseBodies(l, 0L,
				"taf:experience-body:owned", "taf:event:owned", out fault, out failure), failure);
			Assert.AreEqual(closed, l.Revision);
		}

		[Test]
		public void ActiveLeasesPreventImplicitRealmRebinding()
		{
			KingdomExperienceLedger l = Enabled(); Reserve(l, Audience("bound"));
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(l);
			Assert.IsFalse(KingdomExperienceRules.TryRebindEmptyIdentity(l,
				"taf:realm:new", out string failure));
			StringAssert.Contains("explicit realm retirement", failure);
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(l));
		}

		[Test]
		public void CrossRealmRequestFailsWithoutChangingAuthority()
		{
			KingdomExperienceLedger l = Enabled();
			KingdomExperienceAudienceReceipt request = Audience("foreign");
			request.RealmId = "taf:realm:foreign";
			byte[] before = KingdomExperienceCodec.EncodeEnvelope(l);
			Assert.IsFalse(KingdomExperienceRules.TryReserveAudience(l, l.Revision, request,
				out KingdomExperienceCapacityFault fault, out string failure));
			Assert.AreEqual(KingdomExperienceCapacityFault.WrongRealm, fault);
			StringAssert.Contains("another realm", failure);
			CollectionAssert.AreEqual(before, KingdomExperienceCodec.EncodeEnvelope(l));
		}

		[Test]
		public void CanonicalOrderingIgnoresReservationArrivalOrder()
		{
			KingdomExperienceLedger first = Enabled(); KingdomExperienceLedger second = Enabled();
			Reserve(first, Audience("b", "taf:settlement:two"));
			Reserve(first, Audience("a", "taf:settlement:one"));
			Reserve(second, Audience("a", "taf:settlement:one"));
			Reserve(second, Audience("b", "taf:settlement:two"));
			CollectionAssert.AreEqual(KingdomExperienceCodec.EncodeEnvelope(first),
				KingdomExperienceCodec.EncodeEnvelope(second));
		}

		[Test]
		public void FutureWireIsBoundedOpaqueAndRoundTripsExactly()
		{
			byte[] future = KingdomExperienceCodec.EncodeFutureFixture(9,
				new byte[] { 2, 4, 6, 8 });
			KingdomExperienceLedger read = KingdomExperienceCodec.DecodeEnvelope(future);
			Assert.AreEqual(KingdomExperienceSchemaState.Unknown, read.SchemaState);
			Assert.IsFalse(KingdomExperienceRules.CanEmit(read,
				KingdomExperienceOptionKind.CivicStory, long.MaxValue));
			CollectionAssert.AreEqual(future, KingdomExperienceCodec.EncodeEnvelope(read));
			Assert.Throws<InvalidDataException>(() => KingdomExperienceCodec.DecodeEnvelope(
				new byte[] { 1, 2, 3 }));
		}

		[Test]
		public void MalformedCurrentWireQuarantinesAndPreservesExactBoundedEvidence()
		{
			byte[] malformed = KingdomExperienceCodec.EncodeEnvelope(Enabled());
			// Envelope (12), payload format (4), then schema-state byte.
			malformed[16] = 99;
			KingdomExperienceLedger read = KingdomExperienceCodec.DecodeEnvelope(malformed);
			Assert.AreEqual(KingdomExperienceSchemaState.Quarantined, read.SchemaState);
			Assert.IsFalse(KingdomExperienceRules.CanEmit(read,
				KingdomExperienceOptionKind.CivicStory, long.MaxValue));
			CollectionAssert.AreEqual(malformed, KingdomExperienceCodec.EncodeEnvelope(read));
		}

		[Test]
		public void MaximumLegalRowsRemainInsideDeclaredEnvelope()
		{
			KingdomExperienceLedger l = Enabled();
			for (int i = 0; i < 3; i++)
				Reserve(l, Audience("max" + i, "taf:settlement:max" + i));
			for (int i = 0; i < 16; i++) Reserve(l, Body("max" + i), 0);
			Assert.AreEqual(16, KingdomExperienceRules.ReservedBodies(l));
			byte[] envelope = KingdomExperienceCodec.EncodeEnvelope(l);
			Assert.LessOrEqual(envelope.Length,
				KingdomExperienceRules.MaxDeclaredPayloadBytes + 12);
			Assert.IsTrue(KingdomExperienceRules.TryValidate(l, out string failure), failure);
		}

		private static void Reserve(KingdomExperienceLedger L,
			KingdomExperienceAudienceReceipt R)
		{
			Assert.IsTrue(KingdomExperienceRules.TryReserveAudience(L, L.Revision, R,
				out KingdomExperienceCapacityFault _, out string failure), failure);
		}

		private static void Reserve(KingdomExperienceLedger L,
			KingdomExperienceBodyReservation R, int live)
		{
			Assert.IsTrue(KingdomExperienceRules.TryReserveBodies(L, L.Revision, R, live,
				out KingdomExperienceCapacityFault _, out string failure), failure);
		}

		private static void Reserve(KingdomExperienceLedger L,
			KingdomExperienceAudienceReceipt A, KingdomExperienceBodyReservation B, int live)
		{
			Assert.IsTrue(KingdomExperienceRules.TryReservePresentation(L, L.Revision, A, B, live,
				out KingdomExperienceCapacityFault _, out string failure), failure);
		}

		private static byte[][] CloneOwners(byte[][] Values)
		{
			byte[][] copy = new byte[Values.Length][];
			for (int i = 0; i < Values.Length; i++) copy[i] = (byte[])Values[i].Clone();
			return copy;
		}

		private static void AssertOwners(byte[][] Expected, byte[][] Actual)
		{
			Assert.AreEqual(Expected.Length, Actual.Length);
			for (int i = 0; i < Expected.Length; i++)
				CollectionAssert.AreEqual(Expected[i], Actual[i], "owner " + i);
		}
	}
}
#endif
