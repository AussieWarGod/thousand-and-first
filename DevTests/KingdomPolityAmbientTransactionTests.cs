using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityAmbientTransactionTests
	{
		private const string Source =
			"taf:settlement:v1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		private const string Destination =
			"taf:settlement:v1:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
		private const string Cohort = "taf:cohort:zz-weekly-semantic";
		private const string SourceZone = "JoppaWorld.1.1.1.1.10";
		private const string DestinationZone = "JoppaWorld.1.1.1.2.10";

		[Test]
		public void CourierFreezeIsDeterministicBoundedAndTamperEvident()
		{
			KingdomPolityDueWork work = Work(KingdomPolityCohortPurpose.Courier);
			List<KingdomPolityEndpointFacts> endpoints = CourierEndpoints();
			ClassicAssert.IsTrue(KingdomPolityAmbientTransactionRules.TryFreeze(
				KingdomPolityTestData.Realm, KingdomPolityTestData.Realm, work, endpoints,
				out KingdomPolityAmbientTransaction first, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityAmbientTransactionRules.TryFreeze(
				KingdomPolityTestData.Realm, KingdomPolityTestData.Realm, work, endpoints,
				out KingdomPolityAmbientTransaction retry, out failure), failure);
			ClassicAssert.AreEqual(first.TransactionId, retry.TransactionId);
			ClassicAssert.AreEqual(first.FrozenDigest, retry.FrozenDigest);
			ClassicAssert.AreEqual(Source, first.SourceSettlementId);
			ClassicAssert.AreEqual(Destination, first.DestinationSettlementId);
			ClassicAssert.AreEqual("The northern cistern was reopened.", first.SafeDetail);
			ClassicAssert.LessOrEqual(first.FactRefs.Count,
				KingdomPolityAmbientTransactionRules.MaximumFacts);
			first.SafeDetail = "The road is safe.";
			ClassicAssert.IsFalse(KingdomPolityAmbientTransactionRules.Valid(first, Cohort,
				out failure));
			retry.FactRefs.AddRange(new[] { "taf:fact:x", "taf:fact:y", "taf:fact:z" });
			retry.FactRefs.Sort(System.StringComparer.Ordinal);
			ClassicAssert.IsFalse(KingdomPolityAmbientTransactionRules.Valid(retry, Cohort,
				out failure));
		}

		[Test]
		public void UnsupportedGuardPatrolAndRivalSourceFailClosed()
		{
			List<KingdomPolityEndpointFacts> endpoints = CourierEndpoints();
			ClassicAssert.IsFalse(KingdomPolityAmbientTransactionRules.TryFreeze(
				KingdomPolityTestData.Realm, KingdomPolityTestData.Realm,
				Work(KingdomPolityCohortPurpose.Guard), endpoints, out _, out _));
			ClassicAssert.IsFalse(KingdomPolityAmbientTransactionRules.TryFreeze(
				KingdomPolityTestData.Realm, KingdomPolityTestData.Realm,
				Work(KingdomPolityCohortPurpose.Patrol), endpoints, out _, out _));
			ClassicAssert.IsFalse(KingdomPolityAmbientTransactionRules.TryFreeze(
				KingdomPolityTestData.Realm, KingdomPolityTestData.Rival,
				Work(KingdomPolityCohortPurpose.Courier), endpoints, out _, out _));
		}

		[Test]
		public void V8RoundTripCommitsAmbientAndSafeDeedSummary()
		{
			KingdomPolityLedger ledger = LedgerWith(
				KingdomPolityCohortPurpose.Courier, CourierEndpoints());
			ledger.NamedFigures[0].DeedSummary = "Held the cistern through the siege.";
			byte[] first = KingdomPolityCodec.EncodeEnvelope(ledger);
			KingdomPolityLedger decoded = KingdomPolityCodec.DecodeEnvelope(first);
			ClassicAssert.AreEqual(KingdomPolityRules.CurrentFormatVersion, decoded.FormatVersion);
			ClassicAssert.AreEqual("Held the cistern through the siege.",
				decoded.NamedFigures[0].DeedSummary);
			ClassicAssert.AreEqual(ledger.Cohorts[1].AmbientTransaction.FrozenDigest,
				decoded.Cohorts[1].AmbientTransaction.FrozenDigest);
			CollectionAssert.AreEqual(first, KingdomPolityCodec.EncodeEnvelope(decoded));
		}

		[Test]
		public void V7MigrationPinsWeeklyRowUnresolvedWithoutInference()
		{
			KingdomPolityLedger ledger = LedgerWith(
				KingdomPolityCohortPurpose.Courier, CourierEndpoints());
			byte[] old = KingdomPolityCodec.EncodeEnvelopeV7Fixture(ledger);
			KingdomPolityLedger migrated = KingdomPolityCodec.DecodeEnvelope(old);
			KingdomPolityCohortPlan row = migrated.Cohorts[1];
			ClassicAssert.AreEqual(7, migrated.MigratedFromVersion);
			ClassicAssert.AreEqual(0, row.AmbientTransaction.Version);
			ClassicAssert.IsNull(row.AmbientTransaction.TransactionId);
			ClassicAssert.IsFalse(KingdomPolityAmbientTransactionRules.Valid(
				row.AmbientTransaction, row.CohortId, out _));
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(migrated, out string failure), failure);
		}

		[Test]
		public void UnfrozenRolloverIntentCannotBecomeAVisitClaim()
		{
			KingdomPolityDispatchState state = new KingdomPolityDispatchState();
			KingdomPolityDispatchOffer first = new KingdomPolityDispatchOffer
			{
				RealmId = KingdomPolityTestData.Realm, Tick = 0L,
				Endpoints = CourierEndpoints()
			};
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, first,
				out List<KingdomPolityDueWork> work, out string failure), failure);
			ClassicAssert.AreEqual(1, work.Count);
			KingdomPolityDispatchOffer next = new KingdomPolityDispatchOffer
			{
				RealmId = first.RealmId, Tick = KingdomPolityDispatchRules.PeriodTicks,
				Endpoints = CourierEndpoints()
			};
			ClassicAssert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, next,
				out _, out failure), failure);
			for (int i = 0; i < state.DirectRecords.Count; i++)
				ClassicAssert.IsFalse(KingdomPolityDispatchRules.IsKind(state.DirectRecords[i],
					KingdomPolityDispatchRules.DirectPrefix));
		}

		[Test]
		public void PetitionTerminalCreatesTypedHandoffButNoResidentIdentity()
		{
			List<KingdomPolityEndpointFacts> endpoints = MigrantEndpoints();
			KingdomPolityLedger ledger = LedgerWith(KingdomPolityCohortPurpose.Migrant, endpoints);
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryPrepareEndpointManifestation(ledger,
				ledger.Revision, Cohort, DestinationZone, 110L,
				out KingdomPolityPublicationResult prepared, out string failure), failure);
			KingdomPolityProjectionReceipt receipt = KingdomPolityAuthority.Projection(
				ledger, prepared.ProjectionId);
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryCommitEndpointManifestation(ledger,
				ledger.Revision, Cohort, receipt.ProjectionId, receipt.ObjectIds, 110L,
				out _, out failure), failure);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(ledger, Cohort);
			ClassicAssert.IsTrue(KingdomPolityAmbientTransactionRules.TryPrepareAdmissionHandoff(
				KingdomPolityTestData.Realm, cohort, cohort.ResolvedMembers[0].MemberKey,
				receipt.ObjectIds[0], DestinationZone, "Sif of Alpha", 120L,
				out KingdomPolityAdmissionHandoff handoff, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityAmbientTransactionRules.TryRecordTerminal(ledger,
				ledger.Revision, Cohort, KingdomPolityAmbientTerminalChoice.PetitionAccepted,
				120L, handoff, out _, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityAmbientTransactionRules.TryRecordTerminal(ledger,
				ledger.Revision, Cohort, KingdomPolityAmbientTerminalChoice.PetitionAccepted,
				120L, handoff, out KingdomPolityPublicationResult replay, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, replay.Outcome);
			cohort = KingdomPolityAuthority.Cohort(ledger, Cohort);
			ClassicAssert.AreEqual(KingdomPolityAdmissionDecision.Accepted,
				cohort.AmbientTransaction.AdmissionHandoff.Decision);
			ClassicAssert.AreEqual(KingdomPolityCohortPhase.Concluded, cohort.Phase);
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
		}

		private static KingdomPolityLedger LedgerWith(KingdomPolityCohortPurpose Purpose,
			List<KingdomPolityEndpointFacts> Endpoints)
		{
			KingdomPolityDueWork work = Work(Purpose);
			ClassicAssert.IsTrue(KingdomPolityAmbientTransactionRules.TryFreeze(
				KingdomPolityTestData.Realm, KingdomPolityTestData.Realm, work, Endpoints,
				out KingdomPolityAmbientTransaction transaction, out string failure), failure);
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			ledger.Cohorts.Add(new KingdomPolityCohortPlan
			{
				CohortId = Cohort, Purpose = Purpose, SourceRef = work.SourceRef,
				PolityId = KingdomPolityTestData.Realm, ProfileId = KingdomPolityTestData.CurrentProfile,
				ProfileRevision = 1, MinimumLevel = 1, MaximumLevel = 2, SurfaceRef = Destination,
				ScaleBudget = 1, RoleSlots = new List<string> { "visitor" },
				ResolvedMembers = new List<KingdomPolityCohortMember> { new KingdomPolityCohortMember
				{
					Ordinal = 0, MemberKey = "taf:cohort-member:weekly-visitor",
					BlueprintKey = "Water Baron", LoadoutKey = "visitor", SignatureKey = "visitor"
				} }, EventStreamId = work.EventStreamId, RulesVersion = 1, EventOrdinal = 0UL,
				PresentationOptionKind = KingdomExperienceOptionKind.AmbientUse,
				PresentationEnableEpoch = 1L, PresentationReservedTick = 100L,
				Phase = KingdomPolityCohortPhase.Planned, AmbientTransaction = transaction
			});
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
			return ledger;
		}

		private static KingdomPolityDueWork Work(KingdomPolityCohortPurpose Purpose)
		{
			return new KingdomPolityDueWork { CohortId = Cohort,
				EventStreamId = "taf:stream:polity-due:v1:weekly-semantic",
				SourceRef = "taf:event:polity-due:v1:weekly-semantic",
				SettlementId = Destination, Purpose = Purpose, CauseTick = 100L,
				CauseRef = Purpose == KingdomPolityCohortPurpose.Courier ?
					"taf:fact:courier:alpha-beta" : Purpose == KingdomPolityCohortPurpose.Migrant ?
					"taf:fact:petition:alpha-beta" : Purpose == KingdomPolityCohortPurpose.Guard ?
					"taf:fact:guard:absent" : "taf:fact:patrol:absent" };
		}

		private static List<KingdomPolityEndpointFacts> CourierEndpoints()
		{
			KingdomPolityEndpointFacts source = Base(Source, "Alpha", SourceZone);
			source.IsSeat = true;
			source.DeedFactRef = "taf:fact:deed:alpha";
			source.DeedSummary = "The northern cistern was reopened.";
			KingdomPolityEndpointFacts destination = Base(Destination, "Beta", DestinationZone);
			destination.CourierSourceSettlementId = Source;
			destination.CourierSourceZoneId = SourceZone;
			destination.CourierCauseRef = "taf:fact:courier:alpha-beta";
			return new List<KingdomPolityEndpointFacts> { source, destination };
		}

		private static List<KingdomPolityEndpointFacts> MigrantEndpoints()
		{
			KingdomPolityEndpointFacts source = Base(Source, "Alpha", SourceZone);
			source.IsSeat = true;
			source.PopulationFactRef = "taf:fact:population:alpha";
			KingdomPolityEndpointFacts destination = Base(Destination, "Beta", DestinationZone);
			destination.CapacityFactRef = "taf:fact:room:beta";
			destination.MigrantSourceSettlementId = Source;
			destination.MigrantSourceZoneId = SourceZone;
			destination.MigrantCauseRef = "taf:fact:petition:alpha-beta";
			return new List<KingdomPolityEndpointFacts> { source, destination };
		}

		private static KingdomPolityEndpointFacts Base(string Id, string Name, string Zone)
		{
			return new KingdomPolityEndpointFacts { SettlementId = Id,
				SettlementName = Name, ZoneId = Zone };
		}
	}
}
