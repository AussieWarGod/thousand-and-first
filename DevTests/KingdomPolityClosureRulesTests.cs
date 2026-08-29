#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityClosureRulesTests
	{
		[Test]
		public void DirectFallbackUsesCasAndForgedFactsLeaveAuthorityByteIdentical()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, state.Revision, Offer(0),
				out List<KingdomPolityDueWork> work, out string failure), failure);
			string before = Snapshot(state);
			Assert.IsFalse(KingdomPolityDispatchRules.TryRecordCapacityFallback(state,
				state.Revision - 1L, work[0], out _, out failure));
			Assert.AreEqual(before, Snapshot(state));
			KingdomPolityDueWork forged = Copy(work[0]); forged.DueFacts += " forged";
			Assert.IsFalse(KingdomPolityDispatchRules.TryRecordCapacityFallback(state,
				state.Revision, forged, out _, out failure));
			Assert.AreEqual(before, Snapshot(state));
			Action<KingdomPolityDueWork>[] forgeries =
			{
				x => x.CauseRef = "taf:fact:forged-cause",
				x => x.SourceRef = "taf:event:forged-source",
				x => x.EventStreamId = "taf:stream:forged-stream",
				x => x.FairnessTicket = "taf:experience-fairness:v1:" + new string('a', 64),
				x => x.StayUntilTick++, x => x.MemberCount++
			};
			for (int i = 0; i < forgeries.Length; i++)
			{
				forged = Copy(work[0]); forgeries[i](forged);
				Assert.IsFalse(KingdomPolityDispatchRules.TryRecordCapacityFallback(state,
					state.Revision, forged, out _, out failure));
				Assert.AreEqual(before, Snapshot(state));
			}
			KingdomPolityDispatchState raw = KingdomPolityDispatchRules.CloneState(state);
			KingdomPolityDirectRecord intent = KingdomPolityDispatchRules.FindIntent(raw, 0);
			string falseDigest = new string('a', 64);
			string ordinal = work[0].WindowOrdinal.ToString();
			string token = ((byte)work[0].Purpose).ToString();
			string falseEvent = DueId("taf:event:polity-due:v1:", "event", work[0],
				ordinal, token, falseDigest);
			string falseCohort = DueId("taf:cohort:polity-due:v1:", "cohort", work[0],
				ordinal, token, falseDigest);
			intent.EndpointVerb = ReplaceFact(intent.EndpointVerb, "; source-digest=", "; cause=",
				falseDigest);
			intent.EndpointVerb = ReplaceFact(intent.EndpointVerb, "; event=", null, falseEvent);
			intent.SourceRef = falseCohort;
			intent.RecordId = KingdomPolityDispatchRules.StoredId(
				KingdomPolityDispatchRules.IntentPrefix, "polity-intent-v1", intent,
				raw.EndpointDigest);
			KingdomPolityDispatchRules.SortRecords(raw.DirectRecords);
			Assert.IsFalse(KingdomPolityDispatchRules.ValidState(raw, out failure),
				"fully rehashed source-digest forgery must not become authority");
			Assert.AreEqual(before, Snapshot(state));

			Assert.IsTrue(KingdomPolityDispatchRules.TryRecordCapacityFallback(state,
				state.Revision, work[0], out KingdomPolityDirectRecord record, out failure), failure);
			long applied = state.Revision;
			Assert.IsTrue(KingdomPolityDispatchRules.TryRecordCapacityFallback(state,
				0L, work[0], out KingdomPolityDirectRecord retry, out failure), failure);
			Assert.AreEqual(record.RecordId, retry.RecordId); Assert.AreEqual(applied, state.Revision);

			before = Snapshot(state);
			Assert.IsFalse(KingdomPolityDispatchRules.TryAcknowledgeDirectRecord(state,
				state.Revision - 1L, record.RecordId, record.SettlementId, 1L, out failure));
			Assert.AreEqual(before, Snapshot(state));
			state.Revision = long.MaxValue; before = Snapshot(state);
			Assert.IsFalse(KingdomPolityDispatchRules.TryAcknowledgeDirectRecord(state,
				state.Revision, record.RecordId, record.SettlementId, 1L, out failure));
			StringAssert.Contains("exhausted", failure); Assert.AreEqual(before, Snapshot(state));
		}

		[Test]
		public void CapPlusOneCreatesExplicitAggregateAndNextWindowStillOpens()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			for (int window = 0; window <= KingdomPolityDispatchRules.MaximumDirectRecords; window++)
			{
				Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, state.Revision, Offer(window),
					out List<KingdomPolityDueWork> work, out string failure), failure);
				Assert.IsTrue(KingdomPolityDispatchRules.TryRecordCapacityFallback(state,
					state.Revision, work[0], out _, out failure), failure);
			}
			int detail = 0, aggregate = 0;
			for (int i = 0; i < state.DirectRecords.Count; i++)
			{
				if (state.DirectRecords[i].RecordId.StartsWith(
					KingdomPolityDispatchRules.DirectPrefix)) detail++;
				if (state.DirectRecords[i].RecordId.StartsWith(
					KingdomPolityDispatchRules.AggregatePrefix)) aggregate++;
			}
			Assert.AreEqual(KingdomPolityDispatchRules.MaximumDirectRecords, detail);
			Assert.AreEqual(1, aggregate);
			Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, state.Revision,
				Offer(KingdomPolityDispatchRules.MaximumDirectRecords + 1),
				out List<KingdomPolityDueWork> next, out string nextFailure), nextFailure);
			Assert.AreEqual(1, next.Count);
		}

		[Test]
		public void DisabledAndPreFloorWindowsLeaveNoIntentOrDirectBacklog()
		{
			KingdomPolityDispatchState disabled = new KingdomPolityDispatchState();
			Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(disabled, disabled.Revision,
				Offer(0), false, out List<KingdomPolityDueWork> work, out string failure), failure);
			Assert.AreEqual(0, work.Count); Assert.AreEqual(0, disabled.DirectRecords.Count);
			Assert.AreEqual(1, disabled.CompletedMask);
			Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(disabled, disabled.Revision,
				Offer(1), true, out work, out failure), failure);
			Assert.AreEqual(1, work.Count);

			KingdomPolityDispatchState floor = new KingdomPolityDispatchState
				{ FutureCauseFloorTick = 1L };
			Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(floor, floor.Revision,
				Offer(0), out work, out failure), failure);
			Assert.AreEqual(0, work.Count); Assert.AreEqual(0, floor.DirectRecords.Count);
		}

		[Test]
		public void ForeignFutureAndRetirementReceiptRefusalsPreserveRawAuthority()
		{
			KingdomPolityDispatchState foreign = new KingdomPolityDispatchState
				{ RealmId = "taf:realm:foreign" };
			string before = Snapshot(foreign);
			Assert.IsFalse(KingdomPolityDispatchRules.TryOpen(foreign, foreign.Revision, Offer(0),
				out _, out string failure));
			Assert.AreEqual(before, Snapshot(foreign));
			KingdomPolityDispatchState future = new KingdomPolityDispatchState { Version = 99 };
			before = Snapshot(future);
			Assert.IsFalse(KingdomPolityDispatchRules.TryRecover(future,
				KingdomPolityTestData.Realm, "future wire", out failure));
			Assert.AreEqual(before, Snapshot(future));

			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, state.Revision, Offer(0),
				out List<KingdomPolityDueWork> work, out failure), failure);
			Assert.IsTrue(KingdomPolityDispatchRules.TryRecordCapacityFallback(state,
				state.Revision, work[0], out _, out failure), failure);
			int retained = state.DirectRecords.Count;
			Assert.IsTrue(KingdomPolityDispatchRules.TryRetire(state, state.Revision,
				KingdomPolityTestData.Realm, "receipt-a", out failure), failure);
			Assert.AreEqual(retained + 1, state.DirectRecords.Count);
			before = Snapshot(state);
			Assert.IsFalse(KingdomPolityDispatchRules.TryAcknowledgeDirectRecord(state,
				state.Revision, state.DirectRecords.Find(x => x.RecordId.StartsWith(
					KingdomPolityDispatchRules.DirectPrefix)).RecordId,
				KingdomPolityTestData.Settlement, 1L, out failure));
			Assert.AreEqual(before, Snapshot(state));
			Assert.IsFalse(KingdomPolityDispatchRules.TryRetire(state, state.Revision,
				KingdomPolityTestData.Realm, "receipt-b", out failure));
			Assert.AreEqual(before, Snapshot(state));
		}

		[Test]
		public void MasterResumeRefusesForeignFutureAndCorruptDispatchByteIdentically()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			KingdomPolityDispatchState[] refused =
			{
				new KingdomPolityDispatchState { RealmId = "taf:realm:foreign" },
				new KingdomPolityDispatchState { Version = 99,
					RealmId = KingdomPolityTestData.Realm },
				new KingdomPolityDispatchState { RealmId = KingdomPolityTestData.Realm,
					CompletedMask = 1 }
			};
			for (int i = 0; i < refused.Length; i++)
			{
				string dispatchBefore = Snapshot(refused[i]);
				byte[] ledgerBefore = KingdomPolityCodec.EncodeEnvelope(ledger);
				Assert.IsFalse(KingdomPolityRules.TryPrepareMasterResume(ledger, refused[i],
					ledger.Revision, KingdomPolityPresentationState.Enabled, 100L,
					out KingdomPolityMasterResumePlan plan, out string _));
				Assert.IsNull(plan); Assert.AreEqual(dispatchBefore, Snapshot(refused[i]));
				CollectionAssert.AreEqual(ledgerBefore, KingdomPolityCodec.EncodeEnvelope(ledger));
			}
		}

		[Test]
		public void FairnessHasExplicitPriorityStableTieBreakAndWindowRotation()
		{
			List<KingdomExperienceAdmissionCandidate> rows = FairRows(0UL);
			rows[2].ExactRetry = true;
			Assert.IsTrue(KingdomExperienceFairnessRules.TryOrder(rows,
				out List<KingdomExperienceAdmissionCandidate> ordered, out string failure), failure);
			Assert.AreSame(rows[2], ordered[0]);
			rows[2].ExactRetry = false; rows[0].HasDirectFallback = true;
			Assert.IsTrue(KingdomExperienceFairnessRules.TryOrder(rows, out ordered, out failure),
				failure); Assert.AreNotSame(rows[0], ordered[0]);
			string first = ordered[0].SourceId;
			rows = FairRows(6UL);
			Assert.IsTrue(KingdomExperienceFairnessRules.TryOrder(rows, out ordered, out failure),
				failure); Assert.AreNotEqual(first, ordered[0].SourceId);
		}

		[Test]
		public void ExactPolityAllowancesIgnoreOnlyFullMatchingRows()
		{
			KingdomExperienceLedger ledger = EnabledExperience();
			KingdomExperienceAudienceReceipt audience = Audience();
			KingdomExperienceBodyReservation bodies = Bodies();
			Assert.IsTrue(KingdomExperienceRules.TryReservePresentation(ledger, ledger.Revision,
				audience, bodies, 0, out _, out string failure), failure);
			List<KingdomExperienceRetirementLeaseAllowance> exact = new List<
				KingdomExperienceRetirementLeaseAllowance>
			{
				new KingdomExperienceRetirementLeaseAllowance { Audience = audience },
				new KingdomExperienceRetirementLeaseAllowance { Bodies = bodies }
			};
			Assert.IsTrue(KingdomExperienceRules.TryDescribeRealmRemovalBlocker(ledger,
				KingdomPolityTestData.Realm, exact, out string blocker, out failure), failure);
			Assert.IsNull(blocker);
			exact.Add(new KingdomExperienceRetirementLeaseAllowance { Bodies = bodies });
			Assert.IsTrue(KingdomExperienceRules.TryDescribeRealmRemovalBlocker(ledger,
				KingdomPolityTestData.Realm, exact, out blocker, out failure), failure);
			StringAssert.Contains("malformed, duplicate", blocker);
		}

		[Test]
		public void SettlementRetryRequiresSamePinnedDispatchReceipt()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			KingdomPolityDispatchState dispatch = new KingdomPolityDispatchState();
			const string receipt = "0123456789abcdef0123456789abcdef";
			Assert.IsTrue(KingdomPolityDispatchRules.TryRetire(dispatch, dispatch.Revision,
				KingdomPolityTestData.Realm, receipt, out string failure), failure);
			Assert.IsTrue(KingdomPolityRemovalRules.TrySettleBodylessRetirement(ledger, dispatch,
				ledger.Revision, receipt, out KingdomPolityPublicationResult result, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			long settled = ledger.Revision;
			Assert.IsTrue(KingdomPolityRemovalRules.TrySettleBodylessRetirement(ledger, dispatch,
				0L, receipt, out result, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			Assert.IsFalse(KingdomPolityRemovalRules.TrySettleBodylessRetirement(ledger, dispatch,
				settled, "ffffffffffffffffffffffffffffffff", out result, out failure));
			Assert.AreEqual(settled, ledger.Revision);
		}

		private static List<KingdomExperienceAdmissionCandidate> FairRows(ulong Window)
		{
			return new List<KingdomExperienceAdmissionCandidate>
			{
				Fair(KingdomExperienceLane.PolityCohort, "taf:source:polity", Window),
				Fair(KingdomExperienceLane.FirstGuest, "taf:source:guest", Window),
				Fair(KingdomExperienceLane.CommunalRite, "taf:source:rite", Window)
			};
		}

		private static KingdomExperienceAdmissionCandidate Fair(KingdomExperienceLane Lane,
			string Source, ulong Window)
		{
			return new KingdomExperienceAdmissionCandidate { Lane = Lane,
				SettlementId = KingdomPolityTestData.Settlement, SourceId = Source,
				CauseTick = 10L, WindowOrdinal = Window, BodyCount = 1 };
		}

		private static KingdomPolityDueWork Copy(KingdomPolityDueWork x)
		{
			return new KingdomPolityDueWork { EndpointOrdinal = x.EndpointOrdinal,
				EndpointDigest = x.EndpointDigest, CauseRef = x.CauseRef, DueFacts = x.DueFacts,
				FairnessTicket = x.FairnessTicket, CohortId = x.CohortId,
				EventStreamId = x.EventStreamId, SourceRef = x.SourceRef,
				SettlementId = x.SettlementId, Purpose = x.Purpose,
				WindowOrdinal = x.WindowOrdinal, CauseTick = x.CauseTick,
				StayUntilTick = x.StayUntilTick, MemberCount = x.MemberCount,
				EndpointVerb = x.EndpointVerb };
		}

		private static string DueId(string Prefix, string Kind, KingdomPolityDueWork Work,
			string Ordinal, string Token, string Digest)
		{
			return KingdomPolityRules.ActivationId(Prefix, "polity-due-work-v2", Kind,
				KingdomPolityTestData.Realm, Work.SettlementId, Ordinal, Token, Work.CauseRef, Digest);
		}

		private static string ReplaceFact(string Source, string Start, string End, string Value)
		{
			int at = Source.IndexOf(Start, StringComparison.Ordinal) + Start.Length;
			int end = End == null ? Source.Length : Source.IndexOf(End, at, StringComparison.Ordinal);
			return Source.Substring(0, at) + Value + Source.Substring(end);
		}

		private static string Snapshot(KingdomPolityDispatchState s)
		{
			List<string> rows = new List<string> { s.Version.ToString(), s.RealmId ?? "",
				s.Revision.ToString(), s.HasWindow.ToString(), s.LastWindowOrdinal.ToString(),
				s.WindowCauseTick.ToString(), s.FutureCauseFloorTick.ToString(),
				s.EndpointDigest ?? "", s.EndpointCount.ToString(), s.CompletedMask.ToString(),
				s.Fault ?? "" };
			for (int i = 0; i < (s.DirectRecords?.Count ?? 0); i++) rows.Add(
				s.DirectRecords[i].RecordId + "|" + s.DirectRecords[i].SourceRef + "|"
				+ s.DirectRecords[i].SettlementId + "|" + s.DirectRecords[i].Purpose + "|"
				+ s.DirectRecords[i].WindowOrdinal + "|" + s.DirectRecords[i].CauseTick + "|"
				+ s.DirectRecords[i].EndpointVerb + "|" + s.DirectRecords[i].AcknowledgedTick);
			return string.Join("\n", rows);
		}

		private static KingdomPolityDispatchOffer Offer(int Window)
		{
			return new KingdomPolityDispatchOffer { RealmId = KingdomPolityTestData.Realm,
				Tick = Window * KingdomPolityDispatchRules.PeriodTicks,
				Endpoints = new List<KingdomPolityEndpointFacts> { new KingdomPolityEndpointFacts
				{ SettlementId = KingdomPolityTestData.Settlement, IsSeat = true, Population = 20,
					Stage = 2, ShopTier = 2, KnownStorageSpace = 3,
					GuardCauseRef = "taf:fact:guard", PatrolCauseRef = "taf:fact:patrol",
					CourierCauseRef = "taf:fact:courier", TraderCauseRef = "taf:fact:trader",
					MigrantCauseRef = "taf:fact:migrant" } } };
		}

		private static KingdomExperienceLedger EnabledExperience()
		{
			KingdomExperienceLedger ledger = new KingdomExperienceLedger();
			Assert.IsTrue(KingdomExperienceRules.TryBindEmptyIdentity(ledger,
				KingdomPolityTestData.Realm, out string failure), failure);
			Assert.IsTrue(KingdomExperienceRules.TryObserveOptions(ledger, ledger.Revision,
				true, true, true, 10L, out failure), failure); return ledger;
		}

		private static KingdomExperienceAudienceReceipt Audience()
		{
			return new KingdomExperienceAudienceReceipt
			{
				ReservationId = "taf:experience-audience:polity:closure",
				RealmId = KingdomPolityTestData.Realm, SettlementId = KingdomPolityTestData.Settlement,
				SourceId = "taf:cohort:polity-closure", Lane = KingdomExperienceLane.PolityCohort,
				OptionKind = KingdomExperienceOptionKind.AmbientUse, CauseTick = 10L,
				ReservedTick = 10L, EnableEpoch = 1L
			};
		}

		private static KingdomExperienceBodyReservation Bodies()
		{
			return new KingdomExperienceBodyReservation
			{
				ReservationId = "taf:experience-body:polity:closure",
				RealmId = KingdomPolityTestData.Realm, SettlementId = KingdomPolityTestData.Settlement,
				SourceId = "taf:cohort:polity-closure", Lane = KingdomExperienceLane.PolityCohort,
				OptionKind = KingdomExperienceOptionKind.AmbientUse, CauseTick = 10L,
				ReservedTick = 10L, EnableEpoch = 1L, BodyCount = 2
			};
		}
	}
}
#endif
