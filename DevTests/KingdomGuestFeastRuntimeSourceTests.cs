#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomGuestFeastRuntimeSourceTests
	{
		private static string Source(string path) => TestMain.ReadRepositoryText(path);

		[Test]
		public void OptionalObservationNeverGatesGrowthConsumptionOrRetirement()
		{
			string reconcile = Source("Growth/KingdomGrowth.z05.ArrivalStartAndReconcile.cs");
			int choice = reconcile.IndexOf(
				"candidate.Phase == KingdomGrowthArrivalCandidatePhase.AwaitingChoice",
				StringComparison.Ordinal);
			int observe = reconcile.IndexOf("TryObserveAndProveGrowthDecision", choice,
				StringComparison.Ordinal);
			int decline = reconcile.IndexOf(
				"candidate.Phase == KingdomGrowthArrivalCandidatePhase.Declined", observe,
				StringComparison.Ordinal);
			int create = reconcile.IndexOf("BeginGrowthArrivalCandidateCreate", observe,
				StringComparison.Ordinal);
			Assert.That(observe, Is.GreaterThan(choice));
			Assert.That(decline, Is.GreaterThan(observe));
			Assert.That(create, Is.GreaterThan(observe));
			string failureSlice = reconcile.Substring(observe,
				reconcile.IndexOf("candidate.Phase == KingdomGrowthArrivalCandidatePhase.Declined",
					observe, StringComparison.Ordinal) - observe);
			StringAssert.Contains("KingdomLog.Log", failureSlice);
			StringAssert.DoesNotContain("return ArrivalResult.Deferred", failureSlice);
			StringAssert.DoesNotContain("CandidateFault", failureSlice);

			string release = Source("Growth/KingdomGrowth.FirstGuestCompletion.cs");
			StringAssert.DoesNotContain("ExactGrowthDecisionObserved", release);
			string retire = Source("Growth/KingdomGrowth.z09.ArrivalRetirementAndCandidate.cs");
			StringAssert.DoesNotContain("ExactGrowthDecisionObserved", retire);
			StringAssert.Contains("RetireGrowthArrivalCandidate", retire);
		}

		[Test]
		public void DecisionAdapterUsesExactSectionLeaseAndReprovesFrozenGrowthBytes()
		{
			string store = Source("Experience/KingdomGuestFeastRuntime.Store.cs");
			StringAssert.Contains("SectionGuestFeast", store);
			StringAssert.Contains("TryReadSection(SectionId", store);
			StringAssert.Contains("TryCommitSection(lease, payload", store);
			StringAssert.Contains("candidate.GetType() == typeof(KingdomCivicMemorySystem)", store);
			StringAssert.Contains("string.Equals(book.RealmId,", store);
			StringAssert.Contains("lease.Present", store);
			StringAssert.DoesNotContain("RequireSystem", store);
			StringAssert.DoesNotContain("memory.TryCommit(", store);
			string growth = Source("Experience/KingdomGuestFeastRuntime.Growth.cs");
			StringAssert.Contains("ReferenceEquals(growth?.ArrivalCandidate, candidate)", growth);
			StringAssert.Contains("ExactGuestReference(row, opportunity)", growth);
			StringAssert.Contains("TryBeginPresentedOpportunity", growth);
			StringAssert.Contains("TryObserveGuestDecision(next", growth);
			StringAssert.Contains("TryObserveGrowthTerminalBestEffort", growth);
			StringAssert.Contains("TryObserveGuestTerminal", growth);
			StringAssert.Contains("catch (Exception error)", growth);
		}

		[Test]
		public void FirstFeastFanoutOccursAfterOwningDecisionAndNeverRollsItBack()
		{
			string source = Source("Experience/KingdomFirstFeastRuntime.Open.cs");
			int decide = source.IndexOf("TryDecideFirstFeast", StringComparison.Ordinal);
			int observe = source.IndexOf("KingdomGuestFeastRuntime.TryObservePractice",
				StringComparison.Ordinal);
			int governance = source.IndexOf("KingdomGovernanceScope.Commit", StringComparison.Ordinal);
			Assert.That(observe, Is.GreaterThan(decide));
			Assert.That(governance, Is.GreaterThan(decide));
			Assert.That(observe, Is.GreaterThan(governance));
			string slice = source.Substring(observe,
				source.IndexOf("if (decision == KingdomFirstFeastChoice.Defer", observe,
					StringComparison.Ordinal) - observe);
			StringAssert.Contains("KingdomLog.Log", slice);
			StringAssert.DoesNotContain("return;", slice);
			StringAssert.Contains("if (committed && (decision == KingdomFirstFeastChoice.Adopt",
				source);
			StringAssert.Contains("adopt First Feast practice", source);
			StringAssert.Contains("adapt First Feast practice", source);
			StringAssert.DoesNotContain("refuse First Feast practice", source);
		}

		[Test]
		public void EnteredCellCountsOnlyProvenZoneEdgesAndDoesNoRemoteWork()
		{
			string source = Source("Experience/KingdomGuestFeastRuntime.EnteredCell.cs");
			StringAssert.Contains("HarmonyPatch(typeof(GameObject), \"HandleEvent\"", source);
			StringAssert.Contains("typeof(EnteredCellEvent)", source);
			StringAssert.Contains("string.Equals(stamp.ZoneId, zone.ZoneID", source);
			StringAssert.Contains("homeOnly && (!atHome || !row.AwayArmed)", source);
			StringAssert.Contains("TryObserveZoneCycle", source);
			StringAssert.Contains("Cell.cs:3404-3408", source);
			StringAssert.Contains("GameObject.cs:16557-16572", source);
			StringAssert.Contains("EnteredCellEvent.cs:56-71", source);
			int postfix = source.IndexOf("internal static void Postfix", StringComparison.Ordinal);
			Assert.That(postfix, Is.GreaterThanOrEqualTo(0));
			int master = source.IndexOf("!KingdomMaster.NewWorkAllowed(system)", postfix,
				StringComparison.Ordinal);
			int resume = source.IndexOf("stamp.ResumeToken != system.MasterResumeToken",
				StringComparison.Ordinal);
			int option = source.IndexOf("TryStoryState(system", postfix,
				StringComparison.Ordinal);
			int sameZone = source.IndexOf("if (sameZone) return;", StringComparison.Ordinal);
			Assert.That(master, Is.GreaterThanOrEqualTo(0));
			Assert.That(sameZone, Is.GreaterThan(postfix));
			Assert.That(master, Is.GreaterThan(sameZone));
			Assert.That(resume, Is.GreaterThan(master));
			Assert.That(option, Is.GreaterThan(resume),
				"same-zone entered-cell hot path must stop before option or authority scans");
			StringAssert.Contains("first && system.MasterResumeToken != 0L", source);
			StringAssert.Contains("!story || storyEpoch > 1L", source);
			StringAssert.Contains("option-cycle disarm retained", source);
			StringAssert.Contains("TryDisarmCycles", source);
			StringAssert.DoesNotContain("GetZone(", source);
			StringAssert.DoesNotContain("ZoneThawed", source);
			StringAssert.DoesNotContain("SuspendZone", source);
			StringAssert.DoesNotContain("TicksFrozen", source);
			StringAssert.DoesNotContain("Queue<", source);
		}

		[Test]
		public void LocusAndPracticeAreReferencesToExactExistingOwners()
		{
			string source = Source("Experience/KingdomGuestFeastRuntime.Observations.cs");
			StringAssert.Contains("KingdomLocusRules.SelectLocusWork", source);
			StringAssert.Contains("item.GetIntProperty(\"KingdomBuilt\") == 1", source);
			StringAssert.Contains("bench.GetIntProperty(\"KingdomStaffed\") != 1", source);
			StringAssert.Contains("KingdomStations.PostOf(body) == workId", source);
			StringAssert.Contains("keepers == 1", source);
			StringAssert.Contains("TryGetFirstFeast", source);
			StringAssert.Contains("TryObservePractice", source);
			StringAssert.Contains("now <= practiceTick", source);
			StringAssert.Contains("TryCaptureReadyLocus", source);
			StringAssert.Contains("TryLoseLocus", source);
			StringAssert.DoesNotContain("GameObject.Create", source);
			StringAssert.DoesNotContain("KingdomSurvey.Take", source);
			string telling = Source("Experience/KingdomFirstFeastRuntime.Telling.cs");
			StringAssert.Contains("guest feast: load observation retained", telling);
			StringAssert.Contains("KingdomGuestFeastRuntime.TryObservePractice", telling);
			StringAssert.Contains("KingdomGuestFeastRuntime.TryTrace", telling);
			StringAssert.Contains("KingdomGuestFeastRules.TryTrace",
				Source("Experience/KingdomGuestFeastRuntime.Presentation.cs"));
		}

		[Test]
		public void RuntimeOwnsNoTimerRewardJournalOrSecondSystem()
		{
			string[] files = { "Experience/KingdomGuestFeastRuntime.Store.cs",
				"Experience/KingdomGuestFeastRuntime.Growth.cs",
				"Experience/KingdomGuestFeastRuntime.Observations.cs",
				"Experience/KingdomGuestFeastRuntime.EnteredCell.cs",
				"Experience/KingdomGuestFeastRuntime.Presentation.cs" };
			string all = "";
			for (int i = 0; i < files.Length; i++)
			{
				string source = Source(files[i]); all += source;
				Assert.Less(source.Split('\n').Length, 300, files[i]);
			}
			string[] forbidden = { "RequireSystem", "GameObject.Create",
				"JournalAPI", "CookingGameState", "AddXP", "Reputation", "Buff",
				"KingdomGovernanceScope.Commit", "Timer", "TimeSpan", "TicksFrozen" };
			for (int i = 0; i < forbidden.Length; i++)
				StringAssert.DoesNotContain(forbidden[i], all, forbidden[i]);
			StringAssert.DoesNotContain(": IGameSystem", all);
		}

		[Test]
		public void ExternalRecordIsAlwaysReadOnlyAndByteIdentical()
		{
			string source = Source("Experience/KingdomGuestFeastRuntime.Presentation.cs");
			StringAssert.Contains("TryDescribe", source);
			StringAssert.Contains("Popup.Show(description)", source);
			StringAssert.DoesNotContain("TryReconcileOwners", source);
			StringAssert.DoesNotContain("TryRecord", source);
			StringAssert.DoesNotContain("TryPublish", source);
			StringAssert.DoesNotContain("TryCommit", source);
		}

		[Test]
		public void D8AndO11TelemetryCoverExposureCommitClosureCyclesAblationAndQuietEnd()
		{
			string guest = Source("Experience/KingdomGuestFeastRuntime.Growth.cs")
				+ Source("Experience/KingdomGuestFeastRuntime.Observations.cs")
				+ Source("Experience/KingdomGuestFeastRuntime.EnteredCell.cs");
			StringAssert.Contains("KingdomExperienceObservationKind.Exposed", guest);
			StringAssert.Contains("KingdomExperienceObservationKind.Committed", guest);
			StringAssert.Contains("KingdomExperienceObservationKind.Closed", guest);
			StringAssert.Contains("KingdomExperienceObservationKind.Viewed", guest);
			StringAssert.Contains("KingdomExperienceObservationKind.QuietCompletion", guest);
			StringAssert.Contains("KingdomExperienceTrialArm.FactsOnly", guest);
			string rite = Source("Experience/KingdomCommunalRiteRuntime.Recovery.cs")
				+ Source("Experience/KingdomCommunalRiteRuntime.Terminal.cs");
			StringAssert.Contains("KingdomExperienceObservationKind.Exposed", rite);
			StringAssert.Contains("KingdomExperienceObservationKind.Committed", rite);
			StringAssert.Contains("KingdomExperienceObservationKind.Closed", rite);
			StringAssert.Contains("KingdomExperienceObservationKind.QuietCompletion", rite);
			StringAssert.Contains("KingdomExperienceTrialArm.FactsOnly", rite);
		}
	}
}
#endif
