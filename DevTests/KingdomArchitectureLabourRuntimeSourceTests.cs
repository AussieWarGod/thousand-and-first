#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomArchitectureLabourRuntimeSourceTests
	{
		[Test]
		public void PlotWakeConsumesPriorWitnessBeforeCapturingCurrentLoadedCrew()
		{
			string plot = KingdomPlot2LogicalSource.Read();
			AssertOrdered(plot,
				"KingdomPlotLabourRules.Assess(receipt, TimeTick)",
				"if (TimeTick < receipt.LastTick) return true",
				"if (TimeTick == receipt.LastTick)",
				"KingdomPlotLabourWindowRules.TryForInterval(",
				"receipt.LastTick, out prior)",
				"KingdomPlotLabourRules.Advance(receipt, TimeTick,",
				"witnessed ? prior.LabourPercent : 0",
				"witnessed ? prior.InfrastructurePercent : 0",
				"SetPlotWorkLong(parent, PlotWorkLastTickProperty, step.NextTick)",
				"SetPlotWorkLong(parent, PlotWorkRemainingProperty, step.RemainingTicks)",
				"if (step.Complete) return true",
				"return CaptureCurrentWitness",
				"? TryCapturePlotLabourWindow(parent, System, TimeTick",
				"KingdomConstructionPresence.EffectivenessOf(Root, System",
				"KingdomPlotLabourWindowRules.TryEncode(current, out string encoded)",
				"Root.SetStringProperty(PlotWorkWindowProperty, encoded)",
				"SayPlotInfrastructure(System, Root",
				"if (selected) SayPlotWorkShortfall");
			StringAssert.Contains("parent.RemoveStringProperty(PlotWorkWindowProperty)", plot);
		}

		[Test]
		public void PlotWitnessRequiresExactLoadedCrewSchema()
		{
			string window = TestMain.ReadRepositoryText(
				"Growth/KingdomPlot2.26b.LabourWindow.cs");
			AssertOrdered(window,
				"KingdomConstructionPresence.EffectivenessOf(Root, System",
				"KingdomConstructionPresence.SchemaProperty",
				"!= KingdomConstructionPresenceRules.Schema",
				"effectiveness = 0", "freeHands = 0", "selected = false",
				"KingdomPlotLabourWindowRules.TryEncode(current, out string encoded)");
		}

		[Test]
		public void DirectPlotStakeRemainsReceiptlessLegacyCalendar()
		{
			string stake = TestMain.ReadRepositoryText("Growth/KingdomPlot2.11.Stake.cs");
			AssertOrdered(stake, "if (Job != null)",
				"works.SetIntProperty(PlotWorkSchemaProperty, PlotWorkSchema)",
				"SetPlotWorkLong(works, PlotWorkRequiredProperty, part.TotalTicks)");
			Assert.AreEqual(1, Count(stake,
				"works.SetIntProperty(PlotWorkSchemaProperty, PlotWorkSchema)"));
		}

		[Test]
		public void PlotInfrastructureUsesOnlyFrozenReceiptsAndLoadedBooleanYardLaw()
		{
			string construction = KingdomConstructionLogicalSource.Read();
			AssertOrdered(construction,
				"KingdomConstructionPresence.Assign(System, Survey)",
				"KingdomMaterials.YardsStanding(Z)",
				"PlotInfrastructurePercent(plot, works, labourJob",
				"KingdomPlots.Advance(works, System, The.Game.TimeTicks");
			string authority = TestMain.ReadRepositoryText(
				"Growth/KingdomConstruction.PlotLabour.cs");
			AssertOrdered(authority, "Job.Phase != KingdomConstructionPhase.Working",
				"!Owns(System, Z, Job)", "!IsCurrent(Job)",
				"Job.Route != KingdomConstructionRoute.PlotCommission",
				"Job.Route != KingdomConstructionRoute.PlotPlan",
				"KingdomConstructionRules.TryReadBuildTruth(Job,",
				"out bool hasPlot", "!hasPlot",
				"Root.IDIfAssigned != Job.OutputId", "Root.IDIfAssigned != Job.SubjectId",
				"TryFind(Job.Id, out KingdomConstructionJob current)",
				"current.Revision != Job.Revision", "current.Payload != Job.Payload",
				"current.OutputId != Job.OutputId",
				"FindExactId(Z, Job.OutputId", "FindReceipt(Z, Job",
				"Root.CurrentCell != expected",
				"KingdomConstructionRules.TryPaidBuildReceipt(Job, null",
				"KingdomPlots.TryDecodePlotPayload(Job.Payload",
				"architecture.LotSize",
				"KingdomMaterialRules.AllowsBuild(size, paid.Material.Materials, Yards");
			StringAssert.DoesNotContain("KingdomMaterials.CostFor", authority);
			StringAssert.DoesNotContain("KingdomPlots.TryGetSpec", authority);
			AssertOrdered(construction, "TryPlotLabourAuthority(System, Z, plot",
				"KingdomPlots.ConsumePlotLabourAtZero(works, System",
				"PlotInfrastructurePercent(plot, works, labourJob",
				"KingdomPlots.Advance(works, System, The.Game.TimeTicks");
			string plot = KingdomPlot2LogicalSource.Read();
			AssertOrdered(plot, "bool PricePriorWitness = true",
				"PricePriorWitness && KingdomPlotLabourWindowRules.TryForInterval(",
				"internal static void ConsumePlotLabourAtZero(",
				"out ignored, false, false)");
		}

		[Test]
		public void UnauthorizedEqualAndForwardPassesPublishOnlyTickMatchedZero()
		{
			string labour = TestMain.ReadRepositoryText("Growth/KingdomPlot2.26.Labour.cs");
			AssertOrdered(labour, "bool CaptureCurrentWitness = true",
				"if (TimeTick == receipt.LastTick)", "return CaptureCurrentWitness",
				": TryCaptureZeroPlotLabourWindow(parent, System, TimeTick",
				"SetPlotWorkLong(parent, PlotWorkLastTickProperty, step.NextTick)",
				"return CaptureCurrentWitness",
				": TryCaptureZeroPlotLabourWindow(parent, System, TimeTick",
				"internal static void ConsumePlotLabourAtZero(",
				"out ignored, false, false)");
			string window = TestMain.ReadRepositoryText(
				"Growth/KingdomPlot2.26b.LabourWindow.cs");
			string zero = window.Substring(window.IndexOf(
				"private static bool TryCaptureZeroPlotLabourWindow", StringComparison.Ordinal));
			AssertOrdered(zero, "Tick = TimeTick", "LabourPercent = 0",
				"InfrastructureUnavailable", "Hands = 0", "Selected = false",
				"TryEncode(zero, out string encoded)",
				"Root.SetStringProperty(PlotWorkWindowProperty, encoded)",
				"Root.GetStringProperty(PlotWorkWindowProperty) != encoded");
			StringAssert.DoesNotContain("EffectivenessOf", zero);
		}

		[Test]
		public void UpgradeInfrastructureIsFrozenBeforeRequirementAndDebitAssessment()
		{
			string upgrade = KingdomUpgradeLogicalSource.Read();
			string assessment = Between(upgrade, "public static Assessment Assess(",
				"public static bool ContentsWouldFit(");
			AssertOrdered(assessment,
				"KingdomRules.DistrictsBuildPercent(",
				"KingdomUpgradeRules.BuildTicks(",
				"MeasureRequirements(System, Z, predecessor",
				"KingdomUpgradeRules.Assess(");
		}

		private static string Between(string source, string start, string end)
		{
			int first = source.IndexOf(start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(first, 0, "missing source boundary: " + start);
			int last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
			Assert.Greater(last, first, "missing source boundary: " + end);
			return source.Substring(first, last - first);
		}

		private static void AssertOrdered(string source, params string[] terms)
		{
			int offset = 0;
			for (int i = 0; i < terms.Length; i++)
			{
				int found = source.IndexOf(terms[i], offset, StringComparison.Ordinal);
				Assert.GreaterOrEqual(found, 0, "missing ordered source term: " + terms[i]);
				offset = found + terms[i].Length;
			}
		}

		private static int Count(string source, string term)
		{
			int count = 0;
			int offset = 0;
			while ((offset = source.IndexOf(term, offset, StringComparison.Ordinal)) >= 0)
			{
				count++;
				offset += term.Length;
			}
			return count;
		}
	}
}
#endif
