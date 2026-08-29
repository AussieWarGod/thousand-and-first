#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomCommunalRiteRuntimeSourceTests
	{
		private static string Source(string path) => TestMain.ReadRepositoryText(path);

		[Test]
		public void PreparedAndCommittedC18CutsBothPrecedePhysicalQueue()
		{
			string source = Source("Experience/KingdomCommunalRiteRuntime.Recovery.cs");
			int prepare = source.IndexOf("KingdomCommunalRiteRules.TryPrepare(prepared",
				StringComparison.Ordinal);
			int preparedCut = source.IndexOf("TryPublish(system, prepared",
				StringComparison.Ordinal);
			int commit = source.IndexOf("KingdomCommunalRiteRules.TryCommit(committed",
				StringComparison.Ordinal);
			int committedCut = source.IndexOf("TryPublish(system, committed",
				StringComparison.Ordinal);
			int queue = source.IndexOf("KingdomPhysicalHappenings.QueueCommunalRite",
				StringComparison.Ordinal);
			Assert.That(prepare, Is.GreaterThanOrEqualTo(0));
			Assert.That(preparedCut, Is.GreaterThan(prepare));
			Assert.That(commit, Is.GreaterThan(preparedCut));
			Assert.That(committedCut, Is.GreaterThan(commit));
			Assert.That(queue, Is.GreaterThan(committedCut));
		}

		[Test]
		public void TerminalC18CutPrecedesEveryPhysicalAcknowledgeOrCancel()
		{
			string source = Source("Experience/KingdomCommunalRiteRuntime.Terminal.cs");
			int attended = source.IndexOf("TryFinish(system, row, true", StringComparison.Ordinal);
			int acknowledge = source.IndexOf("AcknowledgeCommunalRite", attended,
				StringComparison.Ordinal);
			int suppressed = source.IndexOf("TryFinish(system, row, false", StringComparison.Ordinal);
			int cancel = source.IndexOf("CancelCommunalRite", suppressed,
				StringComparison.Ordinal);
			Assert.That(attended, Is.GreaterThanOrEqualTo(0));
			Assert.That(acknowledge, Is.GreaterThan(attended));
			Assert.That(suppressed, Is.GreaterThanOrEqualTo(0));
			Assert.That(cancel, Is.GreaterThan(suppressed));
			StringAssert.Contains("physical == KingdomCommunalRitePhysicalState.Ready",
				source);
			StringAssert.Contains("cannot be renamed cancellation",
				Source("Experience/KingdomCommunalRiteRuntime.Terminal.cs"));
		}

		[Test]
		public void PhysicalRecoveryUsesTheExactOwningSettlementName()
		{
			string source = Source(
				"Simulation/City/KingdomPhysicalHappenings.07.CommunalRite.cs");
			StringAssert.Contains("system.TryFindSettlement(book", source);
			StringAssert.Contains("seated ? system.SeatName : settlement?.SettlementName", source);
			StringAssert.Contains("DriveCore(system, book, settlementName", source);
			StringAssert.DoesNotContain("DriveCore(system, book, system.SeatName", source);
		}

		[Test]
		public void SectionAdapterUsesExactLeaseRealmAndMultiplicityWithoutWholeEnvelopeWrite()
		{
			string store = Source("Experience/KingdomCommunalRiteRuntime.Store.cs");
			StringAssert.Contains("SectionCommunalRite", store);
			StringAssert.Contains("TryReadSection(SectionId", store);
			StringAssert.Contains("TryCommitSection(lease, payload", store);
			StringAssert.Contains("candidate.GetType() == typeof(KingdomCivicMemorySystem)", store);
			StringAssert.Contains("string.Equals(book.RealmId,", store);
			StringAssert.Contains("lease.Present", store);
			StringAssert.DoesNotContain("RequireSystem", store);
			StringAssert.DoesNotContain("memory.TryCommit(", store);
		}

		[Test]
		public void ReadyProofIsHeldAndMissingOrRestoringNeverFabricatesAttendance()
		{
			string rule = Source(
				"Simulation/City/KingdomHappeningLifecycleRules.Recovery.cs");
			StringAssert.Contains("operation.Kind != KingdomPhysicalHappeningKind.CommunalRite",
				rule);
			StringAssert.Contains("holds it until C18 has published", rule);
			string runtime = Source("Experience/KingdomCommunalRiteRuntime.Recovery.cs")
				+ Source("Experience/KingdomCommunalRiteRuntime.Terminal.cs");
			StringAssert.Contains("physical == KingdomCommunalRitePhysicalState.Ready", runtime);
			StringAssert.Contains("Restoring)\n\t\t\t\treturn Fail", runtime);
			StringAssert.Contains("state == KingdomCommunalRitePhysicalState.Missing) return true",
				runtime);
			StringAssert.Contains("TryRecoverReady", runtime);
			StringAssert.DoesNotContain("Missing)\n\t\t\t\treturn TryPublishTerminal", runtime);
		}

		[Test]
		public void CharterMakesStartResumeCancelVisibleWithoutRewardOrThirdGovernanceCommit()
		{
			string open = Source("Experience/KingdomCommunalRiteRuntime.Open.cs");
			StringAssert.Contains("Start a communal expression", open);
			StringAssert.Contains("Resume the communal expression", open);
			StringAssert.Contains("Cancel the unfinished expression", open);
			StringAssert.Contains("zero-benefit gathering", open);
			StringAssert.Contains("Physical recovery remains pending", open);
			StringAssert.Contains("TryResume(system, context, practice, now", open);
			string all = open + Source("Experience/KingdomCommunalRiteRuntime.Recovery.cs")
				+ Source("Experience/KingdomCommunalRiteRuntime.Terminal.cs");
			StringAssert.DoesNotContain("KingdomGovernanceScope.Commit", all);
			StringAssert.DoesNotContain("AddXP", all);
			StringAssert.DoesNotContain("Reputation", all);
			StringAssert.DoesNotContain("Buff", all);
		}

		[Test]
		public void NewProductionShardsRemainBelowThreeHundredLines()
		{
			string[] files = { "Experience/KingdomCommunalRiteRuntime.Store.cs",
				"Experience/KingdomCommunalRiteRuntime.Recovery.cs",
				"Experience/KingdomCommunalRiteRuntime.Terminal.cs",
				"Experience/KingdomCommunalRiteRuntime.Open.cs" };
			for (int i = 0; i < files.Length; i++)
				Assert.Less(Source(files[i]).Split('\n').Length, 300, files[i]);
		}

		[Test]
		public void OptionEpochAndExternalPhysicalOwnerFailClosedWithoutBorrowingBusyBodies()
		{
			string recovery = Source("Experience/KingdomCommunalRiteRuntime.Recovery.cs");
			StringAssert.Contains("KingdomCommunalRiteOptionDisposition.Unreadable", recovery);
			StringAssert.Contains("KingdomCommunalRiteOptionDisposition.Current", recovery);
			Assert.Less(recovery.IndexOf("ObserveOption(", StringComparison.Ordinal),
				recovery.IndexOf("KingdomCommunalRiteRules.TryCommit(committed",
					StringComparison.Ordinal));
			string drive = Source("Simulation/City/KingdomPhysicalHappenings.cs");
			StringAssert.Contains("if (CommunalRiteActive(book)) return 0;", drive);
			string people = Source(
				"Simulation/City/KingdomPhysicalHappenings.03.RestoreAndParticipants.cs");
			StringAssert.Contains("!SafeToStage(candidate)", people);
			StringAssert.Contains("body.GetEffect<Sitting>() == null", people);
			StringAssert.Contains("body.Brain.Goals.Items.Count == 0", people);
			StringAssert.Contains("ProtectedForCommunalRite(candidate, zone)", people);
			StringAssert.Contains("KingdomKeeper", people);
			StringAssert.Contains("r_KingdomNamedCook", people);
			StringAssert.Contains("r_KingdomOfficeProjection", people);
			StringAssert.Contains("KingdomStations.PostOf(body) != 0", people);
			StringAssert.Contains("KingdomExpeditions.ResidentJobProperty", people);
			StringAssert.Contains("notice.ManningAssigned", people);
			string prepare = Source(
				"Simulation/City/KingdomPhysicalHappenings.02.ObservePrepareAndUse.cs");
			StringAssert.Contains(
				"operation.Kind != KingdomPhysicalHappeningKind.CommunalRite", prepare);
			StringAssert.Contains("KingdomStations.TouchAvailability(body)", prepare);
			string restore = Source(
				"Simulation/City/KingdomPhysicalHappenings.05.BodyReceiptsAndProjectionRestore.cs");
			StringAssert.Contains("A foreign sitting effect is never ours to remove", restore);
			StringAssert.DoesNotContain("RemoveEffect(sitting)", restore);
			string codec = Source("Experience/KingdomCommunalRiteCodec.cs");
			StringAssert.Contains("new MemoryStream(exact, false)", codec);
		}
	}
}
#endif
