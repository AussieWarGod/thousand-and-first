#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPolityClosureSourceTests
	{
		private static string Read(string Directory, string Name)
		{
			return TestMain.ReadRepositoryText(Path.Combine(Directory, Name));
		}

		[Test]
		public void DispatchFreezesFullFactsAndEveryDirectMutationUsesCloneCasCommit()
		{
			string intent = Read("Polity", "KingdomPolityDispatchRules.Intents.cs");
			string facts = Read("Polity", "KingdomPolityDispatchRules.Facts.cs");
			string direct = Read("Polity", "KingdomPolityDispatchRules.DirectRecords.cs");
			string state = Read("Polity", "KingdomPolityDispatchRules.State.cs");
			foreach (string fact in new[] { "Population", "Stage", "ShopTier",
				"KnownStorageSpace", "GuardCauseRef", "PatrolCauseRef", "CourierCauseRef",
					"TraderCauseRef", "MigrantCauseRef", "topology=", "source-digest=", "event=" })
				StringAssert.Contains(fact, intent + facts);
			StringAssert.Contains("DueSourceDigest", facts);
			StringAssert.Contains("expectedEvent", facts);
			StringAssert.Contains("expectedCohort", facts);
			StringAssert.Contains("long ExpectedRevision", direct);
			StringAssert.Contains("CloneState(State)", direct);
			StringAssert.Contains("TryCommitState(State, candidate, ExpectedRevision", direct);
			StringAssert.Contains("SameStoredRecord", direct);
			StringAssert.Contains("State.Revision != ExpectedRevision", state);
			AssertBefore(direct, "if (State.Revision == long.MaxValue)",
				"KingdomPolityDispatchState candidate = CloneState(State)");
		}

		[Test]
		public void ForeignCorruptAuthorityIsPreservedAndNeverReset()
		{
			string dispatch = Read("Polity", "KingdomPolityDispatchRules.cs");
			string recovery = Read("Polity", "KingdomPolityDispatchRules.Recovery.cs");
			StringAssert.Contains("belongs to another realm", dispatch);
			StringAssert.Contains("quarantined and was preserved", recovery);
			StringAssert.DoesNotContain("Reset(", dispatch + recovery);
			StringAssert.DoesNotContain("DirectRecords.Clear", dispatch + recovery);
		}

		[Test]
		public void MasterResumeRequiresValidRealmBoundDispatchAndExactSourceCas()
		{
			string options = Read("Polity", "KingdomPolityRules.Options.cs");
			StringAssert.Contains("KingdomPolityDispatchRules.ValidState(Dispatch", options);
			StringAssert.Contains("Dispatch.RealmId != Ledger.RealmId", options);
			StringAssert.Contains("SameDispatch(Dispatch, Plan.SourceDispatch)", options);
			StringAssert.DoesNotContain("reanchored invalid dispatch evidence", options);
			AssertBefore(options, "ValidState(Dispatch", "CloneDispatch(Dispatch)");
		}

		[Test]
		public void CapUsesExplicitSupersessionAndRetirementRetainsExactSeal()
		{
			string direct = Read("Polity", "KingdomPolityDispatchRules.DirectRecords.cs");
			string aggregate = Read("Polity", "KingdomPolityDispatchRules.Aggregates.cs");
			string retirement = Read("Polity", "KingdomPolityDispatchRules.Retirement.cs");
			StringAssert.Contains("MaximumDirectRecords = 12", direct);
			StringAssert.Contains("FoldOldestDetailed", aggregate);
			StringAssert.Contains("polity-direct-supersession-v1", aggregate);
			StringAssert.Contains("ReadableDirectRecords", direct);
			StringAssert.Contains("DirectAuthorityDigest", retirement);
			StringAssert.Contains("polity dispatch was retired by another receipt", retirement);
			StringAssert.DoesNotContain("DirectRecords.Clear", retirement);
		}

		[Test]
		public void DirectRecordsAreOnDemandOnlyAndNeverAutoAcknowledged()
		{
			string scheduler = Read("Polity", "KingdomPolitySchedulerRuntime.cs");
			string view = Read("Polity", "KingdomPolitySchedulerRuntime.DirectRecords.cs");
			StringAssert.Contains("ReadDirectRecordsOnDemand", view);
			StringAssert.Contains("TryAcknowledgeDirectRecordOnDemand", view);
			StringAssert.DoesNotContain("TryPresentDirectRecords", scheduler + view);
			StringAssert.DoesNotContain("ReadDirectRecordsOnDemand(", scheduler);
			StringAssert.DoesNotContain("TryAcknowledgeDirectRecordOnDemand(", scheduler);
			StringAssert.DoesNotContain("MessageQueue", view);
			StringAssert.DoesNotContain("AddPlayerMessage", view);
		}

		[Test]
		public void AmbientAndDirectedCutsReserveW0BeforeSemanticCommit()
		{
			string scheduler = Read("Polity", "KingdomPolitySchedulerRuntime.cs");
			string visit = Read("Polity", "KingdomPolityVisitRuntime.cs");
			AssertBefore(scheduler, "KingdomPolityDispatchRules.TryOpen",
				"TryReserveAmbientPlan");
			AssertBefore(scheduler, "TryReserveAmbientPlan", "KingdomPolityCohortRules.TryPlan");
			StringAssert.DoesNotContain("row = assignment.Work", scheduler);
			string envoy = visit.Substring(visit.IndexOf(
				"if (!KingdomPolityExperienceRuntime.TryReserveDirectedPlan",
				StringComparison.Ordinal));
			AssertBefore(envoy, "TryReserveDirectedPlan", "EnsureRoute(L, P, Tick");
			AssertBefore(envoy, "EnsureRoute(L, P, Tick", "KingdomPolityCohortRules.TryPlan");
		}

		[Test]
		public void W0OrphansAreAuthenticatedThenAdoptedOrReleased()
		{
			string recovery = Read("Polity", "KingdomPolityExperienceRuntime.Recovery.cs");
			string intents = Read("Polity", "KingdomPolityExperienceRuntime.Intents.cs");
			string visit = Read("Polity", "KingdomPolityVisitRuntime.cs");
			StringAssert.Contains("TryReconcileOrphanSource", recovery + intents);
			StringAssert.Contains("TryReadPresentationSource", intents);
			StringAssert.Contains("TryDirectedPlanForLease", intents);
			StringAssert.Contains("active && AllowNew || TryReleaseAmbient", intents);
			StringAssert.Contains("route == null", intents);
			StringAssert.Contains("return !terminalRoute || TryReleaseDirected", intents);
			StringAssert.Contains("TryPinnedDirectedCause", visit);
		}

		[Test]
		public void FairnessIsCommonDeterministicAndOwnsNoQueue()
		{
			string fairness = Read("Experience", "KingdomExperienceRules.Fairness.cs");
			string scheduler = Read("Polity", "KingdomPolitySchedulerRuntime.Fairness.cs");
			StringAssert.Contains("ExactRetry", fairness);
			StringAssert.Contains("HasDirectFallback", fairness);
			StringAssert.Contains("WindowOrdinal", fairness);
			StringAssert.Contains("KingdomExperienceFairnessRules.TryOrder", scheduler);
			StringAssert.Contains("ExactRetry = leaseState", scheduler);
			StringAssert.DoesNotContain("Queue<", fairness + scheduler);
			StringAssert.DoesNotContain("Random", fairness + scheduler);
		}

		[Test]
		public void GroundAndFinalCutsPlanAuthorizeRevalidateThenApply()
		{
			string ground = Read("Core", "KingdomRealmRetirementGround.Drive.cs");
			string final = Read("Core", "KingdomRealmRetirementAuthority.Finalize.cs");
			AssertBefore(ground, "KingdomRealmRetirementGround.TryPrepare",
				"TryPrepareLoadedGroundRetirement");
			AssertBefore(ground, "TryAuthorizeRecords", "TryApplyLoadedGroundRetirement");
			AssertBefore(ground, "TryApplyLoadedGroundRetirement",
				"KingdomRealmRetirementGround.TryApply");
			AssertBefore(final, "TryBuildFinalPlan(System, State",
				"TryPrepareFinalRetirement");
			AssertBefore(final, "TryPrepareFinalRetirement", "TryApplyFinalRetirement");
			StringAssert.Contains("TryBuildFinalPlan(System, State", final);
		}

		[Test]
		public void SettlementReceiptIsPinnedBeforeSemanticRetry()
		{
			string rules = Read("Polity", "KingdomPolityRemovalRules.Settlement.cs");
			string runtime = Read("Polity", "KingdomPolityRemovalRuntime.Retirement.cs");
			AssertBefore(rules, "ExactRetirementReceipt", "KingdomPolityRules.Clone(Ledger)");
			AssertBefore(runtime, "KingdomPolityDispatchRules.TryRetire",
				"TrySettleBodylessRetirement(System.PolityLedger");
			StringAssert.Contains("LedgerBefore", runtime);
			StringAssert.Contains("DispatchBefore", runtime);
			StringAssert.Contains("ExperienceBefore", runtime);
		}

		[Test]
		public void RetirementNeverLoadsRemoteGroundAndW0InspectionIsExact()
		{
			string runtime = Read("Polity", "KingdomPolityRemovalRuntime.Retirement.cs");
			string experience = Read("Experience", "KingdomExperienceRules.Retirement.cs");
			foreach (string field in new[] { "ReservationId", "RealmId", "SettlementId",
				"SourceId", "Lane", "OptionKind", "CauseTick", "ReservedTick", "EnableEpoch",
				"BodyCount" }) StringAssert.Contains(field, experience);
			StringAssert.DoesNotContain("GetZone", runtime);
			StringAssert.DoesNotContain("ZoneManager", runtime);
			StringAssert.DoesNotContain("Obliterate", runtime);
		}

		private static void AssertBefore(string Source, string First, string Second)
		{
			int first = Source.IndexOf(First, StringComparison.Ordinal);
			int second = Source.IndexOf(Second, StringComparison.Ordinal);
			Assert.GreaterOrEqual(first, 0, First); Assert.Greater(second, first, Second);
		}
	}
}
#endif
