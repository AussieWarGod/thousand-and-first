#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomSuccessionConfigurationSourceTests
	{
		private static string Source(string path) => TestMain.ReadRepositoryText(path);

		[Test]
		public void CharterUsesPreviewConfirmCancelAndStableResidentContext()
		{
			string ui = Source("Core/KingdomCharterPart.Succession.cs");
			int residents = ui.IndexOf("TryGetSuccessionResidents", StringComparison.Ordinal);
			int preview = ui.IndexOf("TryDescribeSuccessionCustom", residents,
				StringComparison.Ordinal);
			int confirm = ui.IndexOf("Popup.ShowYesNo(preview", preview,
				StringComparison.Ordinal);
			int change = ui.IndexOf("TryChangeSuccessionCustom", confirm,
				StringComparison.Ordinal);
			int commit = ui.IndexOf("KingdomGovernanceScope.Commit", change,
				StringComparison.Ordinal);
			Assert.Greater(residents, 0);
			Assert.Greater(preview, residents);
			Assert.Greater(confirm, preview);
			Assert.Greater(change, confirm);
			Assert.Greater(commit, change);
			StringAssert.Contains("resident \" + ResidentId", Source(
				"Experience/KingdomSuccession.TellingAndModels.cs"));
			StringAssert.Contains("Homes, cities, and tenure", ui);
			StringAssert.Contains("if (pick < 0", ui);
			StringAssert.Contains("if (cost < 0)", ui);
		}

		[Test]
		public void DeathFreezesConfiguredExactIdBeforeAnyRiteOrClockMutation()
		{
			string selection = Source("Experience/KingdomSuccession.DeathSelection.cs");
			string execution = Source("Experience/KingdomSuccession.DeathExecution.cs");
			int config = selection.IndexOf("TryGetCurrentConfiguration", StringComparison.Ordinal);
			int resolve = selection.IndexOf("TryResolveConfiguredHeir", config,
				StringComparison.Ordinal);
			int receipt = selection.IndexOf("KingdomSuccessionSelectionReceipt.TryCreate",
				resolve, StringComparison.Ordinal);
			int body = selection.IndexOf("KingdomResidents.TryResolveBoundBody", receipt,
				StringComparison.Ordinal);
			int carry = selection.IndexOf("CarryFounderSuccession", body,
				StringComparison.Ordinal);
			Assert.Greater(resolve, config);
			Assert.Greater(receipt, resolve);
			Assert.Greater(body, receipt);
			Assert.Greater(carry, body);
			int freeze = execution.IndexOf("PendingSelectionReceipt = selectionReceipt",
				StringComparison.Ordinal);
			int clock = execution.IndexOf("game.TimeTicks = dueTick", freeze,
				StringComparison.Ordinal);
			Assert.Greater(clock, freeze);
			StringAssert.Contains("no substitute was tried", selection);
			StringAssert.DoesNotContain("chosen.Rule.Name, out", selection);
		}

		[Test]
		public void SeatCostIsOneReceiptBackedExileAndTrustedReturn()
		{
			string seat = Source("Experience/KingdomSuccession.SeatConsequence.cs");
			string loader = Source("Core/KingdomLoader.cs");
			string begin = Source("Core/KingdomSystem.z11.Return.Begin.cs");
			string zone = Source("Core/KingdomSystem.z19.PersistenceAndCallbacks.cs");
			int active = seat.IndexOf("ActiveSeatClimbRealmId = receipt.RealmId",
				StringComparison.Ordinal);
			int exile = seat.IndexOf("System.Exile(ChosenSeatDeed, Forced: true", active,
				StringComparison.Ordinal);
			int completed = seat.IndexOf("CompletedSeatConsequenceToken = receipt.DeathToken",
				exile, StringComparison.Ordinal);
			Assert.Greater(exile, active);
			Assert.Greater(completed, exile);
			StringAssert.Contains("CompletedSeatConsequenceToken", seat);
			StringAssert.Contains("WithholdsCharter", loader);
			StringAssert.Contains("ChosenSeatBlocksReturn", begin);
			StringAssert.Contains("CompleteChosenSeatClimb", begin);
			StringAssert.Contains("ChosenSeatBlocksReturn", zone);
			StringAssert.Contains("ChosenSeatMayReturn(true", seat);
		}

		[Test]
		public void VersionFourMigratesV3WithoutInventingGrooming()
		{
			string root = Source("Experience/KingdomSuccession.cs");
			string migration = Source("Experience/KingdomSuccession.PendingSeal.cs");
			string validation = Source("Experience/KingdomSuccession.SaveValidation.cs");
			string groomingValidation = Source(
				"Experience/KingdomSuccession.GroomingValidation.cs");
			StringAssert.Contains("CurrentSerializationVersion = 4", root);
			StringAssert.Contains("if (Version < 4) GroomingRecordWire = \"\"", migration);
			StringAssert.Contains("HeirChoice.Law, false", migration);
			StringAssert.Contains("SuccessionSelectionReason.Seniority", migration);
			StringAssert.Contains("KingdomSuccessionConfiguration.TryDecode", validation);
			StringAssert.Contains("ValidateGroomingState", validation);
			StringAssert.Contains("KingdomGroomingRecord.TryDecode", groomingValidation);
			StringAssert.Contains("grooming record does not match", groomingValidation);
			StringAssert.Contains("KingdomSuccessionSelectionReceipt.TryDecode", validation);
			StringAssert.Contains("chosen-seat climb receipt", validation);
		}

		[Test]
		public void GroomingUsesResidentIdAuthoredProofAndDeathTimeFallback()
		{
			string ui = Source("Core/KingdomCharterPart.Succession.cs");
			string heirs = Source("Experience/KingdomSuccession.HeirsAndNews.cs");
			string runtime = Source("Experience/KingdomSuccession.Grooming.cs");
			string death = Source("Experience/KingdomSuccession.DeathSelection.cs");
			StringAssert.Contains("ReviewGroomedSuccession", ui);
			StringAssert.Contains("Revoke the nomination and restore seniority", ui);
			StringAssert.Contains("GroomingLabel", ui);
			StringAssert.Contains("KingdomResearch.HeldIn", heirs);
			StringAssert.Contains("SchoolingHeld && EducationPost(System, state, row.JobWorkId", heirs);
			StringAssert.Contains("KingdomEducationPostObservationRuntime.Proves", heirs);
			StringAssert.Contains("KingdomSuccessionRules.MonthsServed", heirs);
			StringAssert.Contains("Record.ResidentId != config.ChosenResidentId", runtime);
			StringAssert.Contains("if (CommitProgress) GroomingRecordWire = wire", runtime);
			int refresh = death.IndexOf("TryRefreshGrooming", StringComparison.Ordinal);
			int resolve = death.IndexOf("TryResolveConfiguredHeir", refresh,
				StringComparison.Ordinal);
			Assert.Greater(refresh, 0);
			Assert.Greater(resolve, refresh);
			StringAssert.Contains("GroomedUnready", death);
		}

		[Test]
		public void NewProductionShardsStayUnderThreeHundredLines()
		{
			string[] paths =
			{
				"Experience/KingdomSuccessionConfiguration.cs",
				"Experience/KingdomGroomingRecord.cs",
				"Experience/KingdomGroomingRules.cs",
				"Experience/KingdomSuccessionConfigurationRules.cs",
				"Experience/KingdomSuccessionSelectionReceipt.cs",
				"Experience/KingdomSuccession.Configuration.cs",
				"Experience/KingdomSuccession.Grooming.cs",
				"Experience/KingdomSuccession.GroomingValidation.cs",
				"Experience/KingdomSuccession.SeatConsequence.cs",
				"Core/KingdomCharterPart.Succession.cs"
			};
			for (int i = 0; i < paths.Length; i++)
				Assert.Less(Source(paths[i]).Split('\n').Length, 301, paths[i]);
		}
	}
}
#endif
