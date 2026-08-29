#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomScaffoldLabourRuntimeSourceTests
	{
		[Test]
		public void CurrentWakePricesPriorWitnessBeforeCapturingCurrentLoadedCrew()
		{
			string source = Read("Growth/KingdomScaffold.LabourWindow.cs");
			string advance = Between(source, "private bool AdvanceLabour(long TimeTick)",
				"private void CaptureCurrentLabourWindow(long TimeTick)");
			AssertOrdered(advance,
				"int schema = ReceiptLabourSchema();",
				"if (TimeTick < previous) return false;",
				"if (TimeTick == previous)",
				"if (windowed) CaptureCurrentLabourWindow(TimeTick);",
				"KingdomScaffoldLabourWindowRules.TryForInterval(",
				"previous, out prior)",
				"int pricedEffectiveness = 0;",
				"if (schema == 0)",
				"else if (witnessed) pricedEffectiveness = prior.EffectivenessPercent;",
				"KingdomScaffoldLabourRules.Advance(",
				"LastWorkedTick = progress.NextTick;",
				"RemainingTicks = progress.RemainingTicks;",
				"ParentObject.RemoveStringProperty(WorkWindowProperty)",
				"if (windowed) CaptureCurrentLabourWindow(TimeTick);");
			string capture = source.Substring(source.IndexOf(
				"private void CaptureCurrentLabourWindow", StringComparison.Ordinal));
			AssertOrdered(capture, "EffectivenessOf(out int freeHands",
				"KingdomConstructionPresence.SchemaProperty",
				"KingdomScaffoldLabourWindowRules.TryEncode(current, out string encoded)",
				"current.EffectivenessPercent = 0",
				"freeHands = current.Hands = 0",
				"selected = current.Selected = false",
				"KingdomScaffoldLabourWindowRules.TryEncode(current, out encoded)",
				"ParentObject.SetStringProperty(WorkWindowProperty, encoded)",
				"ParentObject.GetStringProperty(WorkWindowProperty) != encoded",
				"current.EffectivenessPercent = 0", "current.Hands = 0",
				"current.Selected = false", "ParentObject.SetStringProperty",
				"ParentObject.GetStringProperty(WorkWindowProperty) == encoded",
				"ParentObject.RemoveStringProperty(WorkWindowProperty)",
				"if (selected) Say(system, freeHands)");
		}

		[Test]
		public void ReceiptSchemaSeparatesCurrentWindowFromExactLegacyCompatibility()
		{
			string source = Read("Growth/KingdomScaffold.LabourWindow.cs");
			AssertOrdered(source,
				"if (string.IsNullOrEmpty(receipt)) return 0;",
				"KingdomConstruction.TryFind(receipt, out job)",
				"KingdomConstruction.IsCurrent(job)",
				"KingdomConstruction.HasReceipt(ParentObject, job)",
				"if (job.BuildTruthSchema == 0) return 0;",
				"job.BuildTruthSchema == KingdomConstructionRules.BuildTruthSchema");
			StringAssert.Contains("if (schema == 0)", source);
			StringAssert.Contains("else if (witnessed) pricedEffectiveness", source);
		}

		[Test]
		public void CompletionSearchHasNoOverflowingCeilingExpression()
		{
			string rules = Read("Growth/KingdomScaffoldLabourRules.cs");
			StringAssert.DoesNotContain("RemainingTicks * 100", KingdomScaffoldLogicalSource.Read());
			AssertOrdered(rules,
				"long low = 1L;", "long high = elapsed;", "while (low < high)",
				"KingdomRules.LabouredTicks(middle, effectiveness)",
				"result.CompletionTick = LastTick + low;");
		}

		[Test]
		public void RemovalProofPrecedesRegistryReproofAfterDestroyCallback()
		{
			string durable = Read("Growth/KingdomScaffold.Durable.cs");
			AssertOrdered(durable,
				"KingdomSurvey.ObserveRemovedFromActive(Z, ParentObject)",
				"predecessorState = KingdomConstruction.FindExactId(",
				"successor.SetStringProperty(RemovalProofProperty, predecessorId)",
				"successor.GetStringProperty(RemovalProofProperty) != predecessorId",
				"KingdomConstruction.TryFind(current.Id, out refreshed)",
				"SameFinalProjectionIdentity(current, refreshed)",
				"KingdomConstruction.IsCurrent(refreshed)",
				"current = refreshed");
		}

		[Test]
		public void PlanMarkerAbsenceIsStampedBeforeRegistryReproofAndRequiredOnRecovery()
		{
			string helper = Read("Growth/KingdomPlanMarker.LookupAndCommands.cs");
			AssertOrdered(helper, "private static bool TryProveMarkerRemoval(",
				"KingdomConstruction.FindExactId", "IsExactPlanScaffold(",
				"Scaffold.SetStringProperty(r_KingdomScaffold.RemovalProofProperty, MarkerId)",
				"r_KingdomScaffold.HasRemovalProof(Scaffold, MarkerId)",
				"KingdomConstruction.TryFind(Current.Id", "SamePlanProjection(",
				"KingdomConstruction.IsCurrent(refreshed)", "Current = refreshed");
			string realization = Read("Growth/KingdomPlanMarker.Realization.cs");
			string removed = Between(realization,
				"KingdomSurvey.ObserveRemovedFromActive(zone, MarkerObject)",
				"if (!KingdomConstruction.UpdateSubject(ref Updated");
			AssertOrdered(removed, "ExactRemovalAction(", "TryProveMarkerRemoval(");
			StringAssert.DoesNotContain("IsCurrent(", removed);
			string retry = Read("Growth/KingdomPlanMarker.RecoveryAndInspection.cs");
			string retried = Between(retry,
				"KingdomSurvey.ObserveRemovedFromActive(Z, marker)",
				"if (!GameObject.Validate(existing)");
			AssertOrdered(retried, "ExactRemovalAction(", "TryProveMarkerRemoval(");
			StringAssert.DoesNotContain("IsCurrent(", retried);
		}

		[Test]
		public void PlotPlanMarkerProofIsSeparatePersistentAndRequiredBeforeSubjectRewrite()
		{
			string helper = Read("Growth/KingdomPlot2.19b.PlanRemovalProof.cs");
			AssertOrdered(helper,
				"PlotPlanMarkerRemovalProofProperty = \"r_TAF_PlotPlanMarkerRemoved\"",
				"private static bool TryProvePlotPlanMarkerRemoval(",
				"FindConstructionResult(Z, Current", "Output.SetStringProperty(",
				"HasPlotPlanMarkerRemovalProof(Output, MarkerId)",
				"KingdomConstruction.TryFind(Current.Id", "SamePlotPlanProjection(",
				"KingdomConstruction.IsCurrent(refreshed)", "Current = refreshed");
			string staking = Read("Growth/KingdomPlot2.19.PlanStaking.cs");
			string removed = Between(staking,
				"KingdomSurvey.ObserveRemovedFromActive(zone, Marker)",
				"if (!KingdomConstruction.UpdateSubject(ref current");
			AssertOrdered(removed, "ExactRemovalAction(",
				"TryProvePlotPlanMarkerRemoval(");
			StringAssert.DoesNotContain("IsCurrent(", removed);
			string retry = Read("Growth/KingdomPlot2.15.RecoveryRetry.cs");
			AssertOrdered(retry, "TryProvePlotPlanMarkerRemoval(System, Z, works",
				"HasPlotPlanMarkerRemovalProof(works, recovered.SubjectId)",
				"KingdomConstruction.UpdateSubject(ref recovered, works.IDIfAssigned)");
			string inspect = Read("Growth/KingdomPlot2.16.RecoveryInspect.cs");
			AssertOrdered(inspect,
				"HasPlotPlanMarkerRemovalProof(result, inspected.SubjectId)",
				"KingdomConstruction.UpdateSubject(ref inspected, result.IDIfAssigned)");
			string output = Read("Growth/KingdomPlot2.31.FinishOutput.cs");
			AssertOrdered(output, "TryCopyPlotPlanMarkerRemovalProof(parent, building)",
				"KingdomConstruction.UpdateFinalOutput(ref construction",
				"PlotPlanMarkerRemovalProofMatches(parent, building)");
		}

		[Test]
		public void ScaffoldProductionShardsStayStrictlyUnderPhysicalLineLimit()
		{
			string[] paths = new string[]
			{
				"Growth/KingdomScaffold.cs",
				"Growth/KingdomScaffold.LabourWindow.cs",
				"Growth/KingdomScaffold.WorkInitialization.cs",
				"Growth/KingdomScaffold.Durable.cs",
				"Growth/KingdomScaffold.SuccessorProof.cs",
				"Growth/KingdomScaffold.CompletionAndLegacy.cs",
				"Growth/KingdomScaffoldLabourRules.cs"
			};
			for (int i = 0; i < paths.Length; i++)
				Assert.Less(Read(paths[i]).Split('\n').Length, 300, paths[i]);
		}

		private static string Read(string Path)
		{
			return TestMain.ReadRepositoryText(Path);
		}

		private static string Between(string Source, string Start, string End)
		{
			int start = Source.IndexOf(Start, StringComparison.Ordinal);
			int end = Source.IndexOf(End, start + Start.Length, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, Start);
			Assert.Greater(end, start, End);
			return Source.Substring(start, end - start);
		}

		private static void AssertOrdered(string Source, params string[] Terms)
		{
			int offset = 0;
			for (int i = 0; i < Terms.Length; i++)
			{
				int found = Source.IndexOf(Terms[i], offset, StringComparison.Ordinal);
				Assert.GreaterOrEqual(found, 0, "missing ordered source term: " + Terms[i]);
				offset = found + Terms[i].Length;
			}
		}
	}
}
#endif
