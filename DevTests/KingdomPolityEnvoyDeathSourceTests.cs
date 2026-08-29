#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPolityEnvoyDeathSourceTests
	{
		[Test]
		public void NativeCallbackPersistsPhysicalIntentAndHasNoCohortTickFallback()
		{
			string body = Read("Polity/r_KingdomPolityCohortBody.cs");
			string runtime = Read("Polity/KingdomPolityVisitInteraction.Harm.cs");
			string death = Read("Polity/KingdomPolityEndpointRuntime.Death.cs");
			StringAssert.Contains("EarlyBeforeDeathRemovalEvent.ID", body);
			StringAssert.Contains("BeforeDestroyObjectEvent.ID", body);
			StringAssert.Contains("OnDestroyObjectEvent.ID", body);
			StringAssert.Contains("TryPrepareVisibleDeath(", body);
			StringAssert.Contains("TryCommitVisibleDeathWitness(", body);
			StringAssert.DoesNotContain("TryResolveCommittedDeathIntent(", body);
			StringAssert.Contains("DeathCallbackInFlight = true", body);
			StringAssert.DoesNotContain("WitnessCohortDeath(", body);
			StringAssert.DoesNotContain("KingdomPolityActiveRuntime", body);
			StringAssert.Contains("E.Killer", body);
			StringAssert.Contains("ExactPhysicalDeathBinding", death);
			AssertBefore(death, "TryWriteDeathIntent(zone, intent",
				"TryReleaseFrozenCustody(ledger, RealmId");
			StringAssert.Contains("TryWriteRemovalWitness", death);
			StringAssert.Contains("Intent.Attribution", runtime);
			StringAssert.DoesNotContain("Killer.IsPlayer()", runtime);
			StringAssert.DoesNotContain("CurrentCell.IsVisible()", runtime);
			StringAssert.Contains("for (int attempt = 0; attempt < 2; attempt++)", runtime);
		}

		[Test]
		public void TrustedRulesRequireCommittedExactBodyAndCausalTick()
		{
			string common = Read("Polity/KingdomPolityDiplomacyRules.EnvoyDeath.cs");
			string harm = Read("Polity/KingdomPolityDiplomacyRules.WitnessedHarm.cs");
			StringAssert.Contains("internal static bool TryConcludeNeutralEnvoyDeath", common);
			StringAssert.Contains("internal static bool TryRecordWitnessedEnvoyHarm", harm);
			StringAssert.Contains("projection.Phase !=", common);
			StringAssert.Contains("KingdomPolityProjectionPhase.Committed", common);
			StringAssert.Contains("bodies != 1", common);
			StringAssert.Contains("Tick < projection.CommittedTick", common);
			StringAssert.Contains("TargetPolityId", common);
			StringAssert.Contains("IsUniqueCurrentPolity", common);
		}

		[Test]
		public void TerminalOwnerRecoversCapacityAndCorrespondenceWithoutInventedTradeAuthority()
		{
			string recovery = Read(
				"Polity/KingdomPolityDiplomacyRules.EnvoyDeathRecovery.cs");
			string correspondence = Read(
				"Polity/KingdomPolityCorrespondenceRules.RecipientUnavailable.cs");
			string absence = Read("Trade/KingdomTradeRules.PolityConsignmentAbsence.cs");
			string active = Read("Polity/KingdomPolityActiveRuntime.cs");
			StringAssert.Contains("candidate.Incidents.Count", recovery);
			StringAssert.Contains("MaxGrievances", recovery);
			StringAssert.Contains("PendingCount++", recovery);
			StringAssert.Contains("TryApplyRecipientUnavailable", correspondence);
			StringAssert.Contains("Proof == null", correspondence);
			StringAssert.Contains("Held = true", correspondence);
			StringAssert.Contains("TryProveNoPolityConsignmentCustody", absence);
			StringAssert.Contains("TouchesRequest(Book.OpenOperation", absence);
			StringAssert.Contains("TouchesRequest(Book.PendingRetirement", absence);
			StringAssert.DoesNotContain("TryConsumeTradeReceipt", correspondence + absence);
			StringAssert.DoesNotContain("TryDeclineConsignment", correspondence + absence);
			StringAssert.DoesNotContain("KingdomTradePolityConsignmentReceipt", correspondence);
			AssertBefore(active, "TryRecoverTradeReceipts", "TryRecoverEnvoyDeaths");
		}

		[Test]
		public void HospitalityAndUiReachTerminalProofBeforeAnyPlayerMessage()
		{
			string hospitality = Read("Polity/KingdomPolityHospitalityRuntime.cs");
			string runtime = Read("Polity/KingdomPolityVisitInteraction.Harm.cs");
			StringAssert.Contains("TryPrepareForEnvoyDeath", hospitality);
			StringAssert.Contains("KingdomPolityHospitalityPhase.Debited", hospitality);
			StringAssert.Contains("KingdomPolityHospitalityPhase.Quarantined", hospitality);
			StringAssert.Contains("TryQuarantineDebit", hospitality);
			AssertBefore(runtime, "TryRecordWitnessedEnvoyHarm(",
				"MessageQueue.AddPlayerMessage");
			AssertBefore(runtime, "TryConcludeNeutralEnvoyDeath(",
				"MessageQueue.AddPlayerMessage");
			AssertBefore(runtime, "TryCleanupApplied", "MessageQueue.AddPlayerMessage");
		}

		private static string Read(string Path) => TestMain.ReadRepositoryText(Path);

		private static void AssertBefore(string Source, string Earlier, string Later)
		{
			int a = Source.IndexOf(Earlier, StringComparison.Ordinal);
			int b = Source.IndexOf(Later, StringComparison.Ordinal);
			Assert.GreaterOrEqual(a, 0, Earlier); Assert.Greater(b, a, Later);
		}
	}
}
#endif
