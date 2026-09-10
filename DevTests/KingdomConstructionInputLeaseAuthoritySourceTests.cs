#if TAF_TESTS
using System;

using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	public class KingdomConstructionInputLeaseAuthoritySourceTests
	{
		[Test]
		public void ExplicitLocalOperationReusesPassAndRejectsForeignAuthority()
		{
			string scope = Read("Growth/KingdomSurvey.LocalOperation.cs");
			Ordered(scope, "ReferenceEquals(The.ZoneManager?.ActiveZone, zone)",
				"KingdomSurvey survey = ActiveFor(zone)", "survey == null && HasBoundPass",
				"if (survey == null) survey = Take(zone, system)",
				"!ReferenceEquals(ActiveFor(zone), survey)", "scope = survey.BindPass()");
			StringAssert.DoesNotContain("GetObjects(", scope);
			StringAssert.DoesNotContain("BoundSurvey =", scope);
			StringAssert.DoesNotContain(".ID", scope);
		}

		[TestCase("Growth/KingdomCommission.cs", "CommissionInLocalPass")]
		[TestCase("Growth/KingdomPlot2.10.Commission.cs", "CommissionInLocalPass")]
		[TestCase("Growth/KingdomMaterials.05.StockpileAndPaymentGates.cs", "StockInLocalPass")]
		[TestCase("Growth/KingdomMaterials.05.StockpileAndPaymentGates.cs", "StockForExactContainerInLocalPass")]
		public void PublicLocalOperationsOwnDisposableScope(string path, string body)
		{
			string source = Read(path);
			Ordered(source, "KingdomSurvey.TryBindLocalOperation(", "using (scope) return " + body);
		}

		[Test]
		public void LocalScopesDoNotWeakenMaterialCustodyOrLeaseReproof()
		{
			string source = Read("Growth/KingdomConstructionInputLeaseAuthority.cs");
			foreach (string guard in new[] { "ActiveLocalCustody(item)", "!IsLeased(snapshot, item)",
				"KingdomOrdinaryCustody.TryProveEmpty(item, out _)", "!KingdomPurpose.HasProtectedCargoEvidence(item)",
				"item.GetIntProperty(\"NeverStack\") == 0", "!item.IsImportant()",
				"item.Equipped == null", "item.IsTakeable()", "!survey.TryLoaded(out loaded)",
				"ReferenceEquals(loaded[i], item)" }) StringAssert.Contains(guard, source);
			StringAssert.DoesNotContain("KingdomSurvey.Take", source);
			StringAssert.Contains("CurrentLeaseAuthorityAllowsPlan()",
				Read("Growth/KingdomMaterialDebit.Commit.cs"));
		}

		[Test]
		public void OneFailClosedAuthorityUsesOnlyCurrentActiveWaterCustody()
		{
			string authority = Read("Growth/KingdomConstructionInputLeaseAuthority.cs");
			Ordered(authority, "KingdomConstruction.TryRead", "TryGetInputReceipt",
				"KingdomConstructionInputLeaseRules.TryBuild");
			StringAssert.Contains("ReferenceEquals(The.ZoneManager.ActiveZone, survey.Ground)",
				authority);
			StringAssert.Contains("KingdomSurvey.ActiveFor(survey.Ground) != survey", authority);
			StringAssert.Contains("for (int i = 0; i < survey.Stores.Count; i++)", authority);
			StringAssert.Contains("IsLeased(snapshot, holder)", authority);
			StringAssert.Contains("TryAvailableWater(spendable", authority);
			StringAssert.DoesNotContain("GetZone(", authority);
			StringAssert.DoesNotContain("GetObjects(", authority);
			StringAssert.DoesNotContain("KingdomSurvey.Take", authority);
			ClassicAssert.LessOrEqual(authority.Split('\n').Length, 300);

			string planner = Read("Growth/KingdomConstruction.InputPlannerAuthority.cs");
			StringAssert.Contains("KingdomConstructionInputLeaseAuthority.TryCapture", planner);
			StringAssert.Contains("KingdomConstructionInputTxPhase.Cancelled", Read(
				"Growth/KingdomConstructionInputLeaseRules.cs"));
		}

		[Test]
		public void EveryLocalMaterialSelectionAndCommitExcludesDurableIdentity()
		{
			string stock = Read("Growth/KingdomMaterials.05.StockpileAndPaymentGates.cs");
			Ordered(stock, "TryCapture(", "CanUseMaterial(", "stock.Tally.Add");
			StringAssert.Contains("StockForExactContainer", stock);
			string sources = Read("Growth/KingdomMaterialDebit.Sources.cs");
			StringAssert.Contains("Stock.InputLeases, RequiredItem", sources);
			StringAssert.Contains("Stock.InputLeases, Item", sources);
			string commit = Read("Growth/KingdomMaterialDebit.Commit.cs");
			StringAssert.Contains("CurrentLeaseAuthorityAllowsPlan()", commit);
			string compensation = Read("Growth/KingdomMaterialDebit.FailureRecovery.cs");
			Ordered(compensation, "TryObjectAvailableForLocalDebit", "entry.Item.Count =");
			string legacy = Read("Growth/KingdomMaterials.04.MaterialStock.cs");
			Ordered(legacy, "KingdomConstructionInputLeaseAuthority.TryCapture",
				"KingdomConstructionInputLeaseAuthority.CanUseMaterial", "item.Destroy");
			string authority = Read("Growth/KingdomConstructionInputLeaseAuthority.cs");
			StringAssert.Contains("!item.HasStringProperty(KingdomConstruction.InputMarkerProperty)",
				authority);
			StringAssert.Contains("!item.HasIntProperty(KingdomConstruction.InputMarkerProperty)",
				authority);
		}

		[Test]
		public void ExactAndPartialWaterDrawsShareLeasesWhileUpkeepMayUseOnlyTheFloor()
		{
			string debit = KingdomWaterDebitLogicalSource.Read();
			Ordered(debit, "KingdomConstructionInputLeaseAuthority.TryCapture",
				"TryWaterAllowance", "TryPlan(Amount");
			StringAssert.Contains("!KingdomConstructionInputLeaseAuthority.IsLeased(leases, owner)",
				debit);
			StringAssert.Contains("CurrentLeaseAuthorityAllowsDebit()", debit);
			Ordered(debit, "private bool RestoreAll", "TryObjectAvailableForLocalDebit",
				"TryAssignSnapshot(entry)");
			string survey = Read("Growth/KingdomSurvey.03.LookupAndWaterDebit.cs");
			StringAssert.Contains("ConsumeAvailable(Drams, true)", survey);
			StringAssert.Contains("ConsumeAvailable(Drams, false)", survey);
			StringAssert.Contains("KingdomConstructionInputLeaseAuthority.IsLeased", survey);
			string upkeep = Read("Growth/KingdomGrowth.z02.ScarcityHeartbeat.cs");
			StringAssert.Contains("Survey.ConsumeUpkeep(upkeep)", upkeep);
			string arrival = Read("Growth/KingdomGrowth.z06.ArrivalPreparation.cs");
			Ordered(arrival, "TryWaterAllowance", "IsLeased(leases, owner)",
				"PrepareGrowthWaterLeg");
			string arrivalCommit = Read("Growth/KingdomGrowth.z07.ArrivalCompletion.cs");
			Ordered(arrivalCommit, "TryObjectAvailableForLocalDebit",
				"KingdomLiquids.Drain(vessel, leg.Delta)");
			string leak = Read("Growth/KingdomSurvey.07.ExactLeakage.cs");
			Ordered(leak, "TryObjectAvailableForLocalDebit",
				"KingdomLiquids.Drain(Store, Drams)");
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }

		private static void Ordered(string source, params string[] markers)
		{
			int at = -1;
			for (int i = 0; i < markers.Length; i++)
			{
				int next = source.IndexOf(markers[i], at + 1, StringComparison.Ordinal);
				ClassicAssert.Greater(next, at, markers[i]);
				at = next;
			}
		}
	}
}
#endif
