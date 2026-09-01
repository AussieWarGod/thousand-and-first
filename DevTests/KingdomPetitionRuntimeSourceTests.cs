#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomPetitionRuntimeSourceTests
	{
		private static string Runtime => KingdomPetitionLifecycleLogicalSource.Read();

		private static string Facade => TestMain.ReadRepositoryText(
			"Quests/KingdomPetitions.cs");

		[Test]
		public void Runtime_UsesSerializedPetitionLaneAndAllFiveActions()
		{
			string source = Runtime;
			StringAssert.Contains("book.Petition", source);
			StringAssert.Contains("KingdomLifecycleLane.Petition", source);
			StringAssert.Contains("KingdomLifecycleAction.PetitionOffer", source);
			StringAssert.Contains("KingdomLifecycleAction.PetitionAccept", source);
			StringAssert.Contains("KingdomLifecycleAction.PetitionDecline", source);
			StringAssert.Contains("KingdomLifecycleAction.PetitionResolve", source);
			StringAssert.Contains("KingdomLifecycleAction.PetitionExpire", source);
			StringAssert.Contains("PetitionRuntimeAdapter.PrepareLeases", source);
		}

		[Test]
		public void Runtime_FreezesRealRequesterBodyAndExactSeatedOrigin()
		{
			string source = Runtime;
			StringAssert.Contains("survey.Settlers", source);
			StringAssert.Contains("candidate.GetIntProperty(\"KingdomCitizen\") != 1", source);
			StringAssert.Contains("candidate.CurrentZone != survey.Ground", source);
			StringAssert.Contains("candidate.ID", source);
			StringAssert.Contains("candidate.Blueprint", source);
			StringAssert.Contains("candidate.GetStringProperty(\"KingdomName\")", source);
			StringAssert.Contains("op.Origin = settlementId", source);
			StringAssert.Contains("system.LifecycleBook.SettlementId", source);
			StringAssert.Contains("system.CurrentSettlementId", source);
			StringAssert.DoesNotContain("RosterNames", source);
			StringAssert.DoesNotContain("GetRandomElement", source);
		}

		[Test]
		public void Runtime_HasDurablePhasesOutboxRecoveryAndBoundedDriver()
		{
			string source = Runtime;
			StringAssert.Contains("KingdomLifecyclePhase.Prepared", source);
			StringAssert.Contains("KingdomLifecyclePhase.DomainIntent", source);
			StringAssert.Contains("KingdomLifecyclePhase.DomainSettled", source);
			StringAssert.Contains("KingdomLifecyclePhase.Sinks", source);
			StringAssert.Contains("KingdomLifecyclePhase.ScheduleIntent", source);
			StringAssert.Contains("KingdomLifecyclePhase.Terminal", source);
			StringAssert.Contains("RecoverOutbox(book, op)", source);
			StringAssert.Contains("KingdomChronicle.RecordOnce", source);
			StringAssert.Contains("for (int guard = 0; guard < 12; guard++)", source);
		}

		[Test]
		public void Runtime_NeverUsesVanillaQuestOrPreacceptFulfilment()
		{
			string source = Runtime + Facade;
			StringAssert.DoesNotContain("StartQuest(", source);
			StringAssert.DoesNotContain("FinishQuest(", source);
			StringAssert.DoesNotContain("FailQuest(", source);
			string check = Slice(Runtime, "internal static void Check(",
				"internal static PetitionLifecycle Status(");
			StringAssert.Contains("state == PetitionLifecycle.Accepted", check);
			StringAssert.Contains("CanResolve(state", check);
			StringAssert.DoesNotContain("state == PetitionLifecycle.Offered\n\t\t\t{\n", check);
		}

		[Test]
		public void ShelterUsesCurrentPhysicalRoofEvidence()
		{
			string source = Runtime;
			StringAssert.Contains("Survey.TryBenefits(out KingdomBenefitIndex benefits",
				source);
			StringAssert.Contains("benefits.Total(\"roof\")", source);
			StringAssert.Contains("shelter evidence paused", source);
			StringAssert.DoesNotContain("survey.Beds", source);
		}

		[Test]
		public void OptionOff_ClosesOfferedPausesAcceptedAndResumesFutureClock()
		{
			string source = Runtime;
			string disabled = Slice(source, "private static bool ReconcileDisabled(",
				"private static bool ResumeAccepted(");
			StringAssert.Contains("state == PetitionLifecycle.Offered", disabled);
			StringAssert.Contains("PetitionExpire", disabled);
			StringAssert.Contains("state != PetitionLifecycle.Accepted", disabled);
			StringAssert.Contains("PauseRemaining", disabled);
			StringAssert.Contains("PausedClock", disabled);
			string resume = Slice(source, "private static bool ResumeAccepted(",
				"private static bool ObserveOption(");
			StringAssert.Contains("TryResumeDeadline", resume);
			StringAssert.Contains("PetitionAccept", resume);
			StringAssert.Contains("ActiveClock", resume);
		}

		[Test]
		public void LegacyPath_AdoptsOnlyCompleteEvidenceAndRetainsMalformedFields()
		{
			string source = Runtime;
			StringAssert.Contains("TryRequester(system, survey, system.PetitionPetitioner", source);
			StringAssert.Contains("LegacyShape(system, book)", source);
			StringAssert.Contains("KingdomPetitionRules.TargetValid(system.PetitionKind", source);
			StringAssert.Contains("malformed legacy petition evidence was retained", source);
			StringAssert.Contains("book.Quarantined = true", source);
			StringAssert.DoesNotContain("system.PetitionKind = KingdomRules.PetitionKind.None", source);
			StringAssert.DoesNotContain("system.PetitionEventId = null", source);
			StringAssert.DoesNotContain("system.PetitionPetitioner = null", source);
		}

		[Test]
		public void LegacyScalars_AreProjectionAndMigrationOnlyNotSchedulingAuthority()
		{
			string source = Runtime;
			string canStart = Slice(source, "private static bool CanStart(",
				"private static bool AdoptLegacy(");
			StringAssert.Contains("book.Petition", canStart);
			StringAssert.Contains("book.PetitionOptionTick", canStart);
			StringAssert.DoesNotContain("system.PetitionState", canStart);
			StringAssert.DoesNotContain("system.LastPetitionTick", canStart);
			StringAssert.DoesNotContain("system.LastPetitionMonthOrdinal", canStart);
			string project = Slice(source, "private static void Project(",
				"private static KingdomLifecycleBook Authority(");
			StringAssert.Contains("system.PetitionState =", project);
			StringAssert.Contains("system.PetitionKind =", project);
		}

		[Test]
		public void PublicSurface_IsThinGuardedFacade()
		{
			string source = Facade;
			Assert.Less(source.Split('\n').Length, 240);
			StringAssert.Contains("KingdomPetitionLifecycle.OnSettlementPass", source);
			StringAssert.Contains("KingdomPetitionLifecycle.Issue", source);
			StringAssert.Contains("KingdomPetitionLifecycle.Accept", source);
			StringAssert.Contains("KingdomPetitionLifecycle.Decline", source);
			StringAssert.Contains("KingdomPetitionLifecycle.Check", source);
			StringAssert.Contains("MetricsManager.LogError", source);
			StringAssert.DoesNotContain("PetitionState =", source);
			StringAssert.DoesNotContain("PetitionKind =", source);
		}

		private static string Slice(string source, string start, string end)
		{
			int a = source.IndexOf(start, StringComparison.Ordinal);
			int b = source.IndexOf(end, a + start.Length, StringComparison.Ordinal);
			Assert.GreaterOrEqual(a, 0, start);
			Assert.Greater(b, a, end);
			return source.Substring(a, b - a);
		}
	}
}
#endif
