using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPolityHospitalitySourceTests
	{
		[Test]
		public void LoadedHospitalityPublishesBeforeExactDebitAndNeverInventsStock()
		{
			string entry = Read("Polity/KingdomPolityHospitalityRuntime.cs");
			string planning = Read("Polity/KingdomPolityHospitalityRuntime.Planning.cs");
			string debit = Read("Polity/KingdomPolityHospitalityRuntime.Debit.cs");
			string interaction = Read("Polity/KingdomPolityVisitInteraction.cs");
			string newPlan = entry.Substring(entry.IndexOf("if (!TryBuildRequest",
				System.StringComparison.Ordinal));
			StringAssert.Contains("TryPlanDebit(System.PolityLedger", newPlan);
			AssertBefore(newPlan, "TryPlanDebit(System.PolityLedger",
				"TryDrive(System, transaction");
			StringAssert.Contains("TryReserveExactWater(1", planning);
			AssertBefore(planning, "TryDescribe", "debit.Rollback()");
			StringAssert.Contains("TryObjectAvailableForLocalDebit", planning + debit);
			AssertBefore(debit, "if (!Bind(item, T)", "item.Destroy(null, Silent: true)");
			AssertBefore(debit, "if (!Bind(item, T)", "KingdomLiquids.Drain");
			StringAssert.Contains("TryCommitDebit(System.PolityLedger", debit);
			StringAssert.Contains("Ordinary diplomacy remains available", debit);
			StringAssert.Contains("This is optional", interaction);
			StringAssert.DoesNotContain("CreateObject", planning + debit);
			StringAssert.DoesNotContain("GetZone", planning + debit);
			StringAssert.DoesNotContain("ConsumeFood", planning + debit);
		}

		[Test]
		public void HospitalityWireOwnsPlanProofAndAppliedConclusion()
		{
			string model = Read("Polity/KingdomPolityHospitalityModels.cs");
			string rules = Read("Polity/KingdomPolityHospitalityRules.cs") +
				Read("Polity/KingdomPolityHospitalityRules.Transactions.cs");
			string codec = Read("Polity/KingdomPolityCodec.IncidentRows.cs");
			string answer = Read("Polity/KingdomPolityDiplomacyRules.Answer.cs");
			foreach (string field in new[] { "TransactionId", "TermsPlanId", "SurfaceRef",
				"ZoneId", "PlanDigest", "Proof", "Fault" })
				StringAssert.Contains(field, model + codec);
			StringAssert.Contains("KingdomPolityHospitalityPhase.Planned", rules);
			StringAssert.Contains("KingdomPolityHospitalityPhase.Debited", rules);
			StringAssert.Contains("KingdomPolityHospitalityPhase.Applied", answer);
			StringAssert.Contains("Hospitality.ObservedFactId", answer);
			StringAssert.Contains("Hospitality.ReceiptId", answer);
		}

		private static string Read(string Relative)
		{
			return TestMain.ReadRepositoryText(Relative);
		}

		private static void AssertBefore(string Source, string First, string Second)
		{
			int first = Source.IndexOf(First, System.StringComparison.Ordinal);
			int second = Source.IndexOf(Second, System.StringComparison.Ordinal);
			Assert.GreaterOrEqual(first, 0, First);
			Assert.Greater(second, first, Second);
		}
	}
}
