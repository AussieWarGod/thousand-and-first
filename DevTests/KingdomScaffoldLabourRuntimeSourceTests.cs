#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
		public void RemovalIntentPrecedesDestroyAndGlobalAftermathCommitsProof()
		{
			string durable = Read("Growth/KingdomScaffold.Durable.cs");
			AssertOrdered(durable,
				"TryPublishScaffoldRemovalIntent(successor, predecessorId)",
				"ParentObject.Destroy(null, Silent: true)",
				"KingdomSurvey.ObserveCurrentTopologyInActive(Z, ParentObject)",
				"KingdomConstruction.FindGlobalLiveId(",
				"KingdomConstructionRules.ScaffoldRemovalAftermath(",
				"KingdomExactRemovalAction.InvokeOnce",
				"KingdomExactRemovalAction.ProvedAbsent",
				"KingdomSurvey.ObserveRemovedFromActive(Z, ParentObject)",
				"TryCommitScaffoldRemovalProof(");
			StringAssert.Contains("originalValid", durable,
				"changed-ID live reference must contradict old-ID absence");

			string proof = Read("Growth/KingdomScaffold.RemovalProof.cs");
			AssertOrdered(proof,
				"ScaffoldRemovalIntentIdProperty =",
				"ScaffoldRemovalIntentSchemaProperty =",
				"TryPublishScaffoldRemovalIntent(",
				"HasStringProperty(ScaffoldRemovalIntentSchemaProperty)",
				"HasIntProperty(ScaffoldRemovalIntentIdProperty)",
				"SetStringProperty(ScaffoldRemovalIntentIdProperty, ScaffoldId)",
				"SetIntProperty(ScaffoldRemovalIntentSchemaProperty",
				"HasExactScaffoldRemovalIntent(Successor, ScaffoldId)",
				"TryCommitScaffoldRemovalProof(",
				"KingdomConstruction.FindGlobalLiveId(",
				"GameObject.Validate(ExpectedPredecessor)",
				"ExactScaffoldReceiptClosure(Job, Successor, improvement, Z, cell)",
				"Successor.SetStringProperty(RemovalProofProperty, ScaffoldId)",
				"KingdomConstruction.TryFind(Job.Id, out refreshed)",
				"SameFinalProjectionIdentity(Job, refreshed)");
			StringAssert.Contains("FindGlobalLiveReceipt(Job.Id, Successor", proof);
			StringAssert.Contains("FindGlobalLiveId(Job.SubjectId, out allowedSubject)", proof);
			StringAssert.DoesNotContain(
				"ScaffoldRemovalIntentIdProperty = RemovalProofProperty", proof);

			string handover = Read("Growth/KingdomUpgrade.20.HandOver.cs");
			AssertOrdered(handover, "TryCommitScaffoldRemovalProof(ownerSystem",
				"ExactPendingRemovalProof(Successor, intent.Scaffold.IDIfAssigned");
			StringAssert.Contains("TryCommitScaffoldRemovalProof(System, Z,",
				Read("Growth/KingdomCommission.Recovery.cs"));
			StringAssert.Contains("TryCommitScaffoldRemovalProof(System, Z,",
				Read("Growth/KingdomPlanMarker.RecoveryAndInspection.cs"));
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
				"Growth/KingdomScaffold.RemovalProof.cs",
				"Growth/KingdomScaffold.SuccessorProof.cs",
				"Growth/KingdomScaffold.CompletionAndLegacy.cs",
				"Growth/KingdomScaffoldLabourRules.cs"
			};
			for (int i = 0; i < paths.Length; i++)
				ClassicAssert.Less(Read(paths[i]).Split('\n').Length, 300, paths[i]);
		}

		[Test]
		public void RemovalProofNamesTheFirstFailedIdentityPredicateAndLogsEveryRefusal()
		{
			// Issue #212: HandOver at f93450c5 entered InspectionRequired with the shared
			// sentence and nothing in Player.log said which identity field had changed. The
			// proof now walks the same predicates in the same short-circuit order, names the
			// first failure, and every refusal is logged once with job, phase and identities.
			string proof = Read("Growth/KingdomScaffold.RemovalProof.cs");
			string commit = Between(proof, "public static bool TryCommitScaffoldRemovalProof(",
				"private static string IdentityRefusal(");
			AssertOrdered(commit,
				"IdentityRefusal(System, Z, cell, Successor, Blueprint, ScaffoldId,",
				"if (refused != null)",
				"KingdomConstructionRules.ScaffoldRemovalIdentityRefusal(refused)",
				"Scaffold absence is not globally exact.",
				"A renamed, moved, or duplicate scaffold still carries the receipt.",
				"Scaffold-removal proof carries foreign or opposite-typed evidence.",
				"Scaffold-removal proof changed during registry reproof.");
			ClassicAssert.AreEqual(5, Count(commit, "return Refuse(Job, ScaffoldId, Successor,"),
				"every refusal of the proof is named in the log");
			StringAssert.DoesNotContain("return Fail(", commit);
			StringAssert.DoesNotContain(
				"\"Scaffold-removal intent or successor identity changed.\"", proof);

			string walk = Between(proof, "private static string IdentityRefusal(",
				"private static bool Refuse(");
			AssertOrdered(walk,
				"if (Cell == null) return KingdomConstructionRules.ScaffoldRemovalCellPredicate;",
				"string.IsNullOrEmpty(Blueprint)", "ScaffoldRemovalBlueprintPredicate",
				"!ScaffoldRoute && !Improvement", "ScaffoldRemovalRoutePredicate",
				"KingdomConstructionRules.ScaffoldRemovalPhaseAdmitted(Job.Phase,",
				"HasRemovalProof(Successor, ScaffoldId)", "ScaffoldRemovalPhasePredicate",
				"KingdomConstruction.Owns(System, Z, Job)", "ScaffoldRemovalOwnerPredicate",
				"KingdomConstruction.IsCurrent(Job)", "ScaffoldRemovalCurrentPredicate",
				"HasExactScaffoldRemovalIntent(Successor, ScaffoldId)", "ScaffoldRemovalIntentPredicate",
				"IsExactSuccessor(Successor, Z, Cell, Job, Blueprint)", "ScaffoldRemovalSuccessorPredicate",
				"KingdomGatehouseRules.IsGatehouse(Job.TargetKey)",
				"KingdomGatehouse.ProjectionComplete(Successor, Z)", "ScaffoldRemovalGatehousePredicate",
				"KingdomConstruction.FindExactId(Z, Job.OutputId, out exactSuccessor)",
				"ScaffoldRemovalOutputPredicate",
				"!ReferenceEquals(exactSuccessor, Successor)", "ScaffoldRemovalSameOutputPredicate",
				"return null;");
			foreach (string forbidden in new[] { "SetIntProperty", "SetStringProperty",
				"RemoveIntProperty", "RemoveStringProperty", "Destroy(", "AddObject(" })
				StringAssert.DoesNotContain(forbidden, walk);

			string refuse = proof.Substring(proof.IndexOf("private static bool Refuse(",
				StringComparison.Ordinal));
			AssertOrdered(refuse,
				"KingdomLog.Log(\"construction: scaffold-removal proof refused: \" + Message",
				"\" job=\"", "\" phase=\"", "\" physical=\"", "\" route=\"", "\" subject=\"",
				"\" output=\"", "\" scaffold=\"", "\" successor=\"",
				"return Fail(Message, out Failure);");

			// The Harness trace that names the same predicates for the paid chain still exists.
			StringAssert.Contains("\"; admitted-phase=\"",
				Read("Harness/KingdomCampHeartChainRemovalTrace.cs"));
		}

		private static int Count(string Source, string Term)
		{
			int count = 0;
			for (int at = Source.IndexOf(Term, StringComparison.Ordinal); at >= 0;
				at = Source.IndexOf(Term, at + Term.Length, StringComparison.Ordinal)) count++;
			return count;
		}

		private static string Read(string Path)
		{
			return TestMain.ReadRepositoryText(Path);
		}

		private static string Between(string Source, string Start, string End)
		{
			int start = Source.IndexOf(Start, StringComparison.Ordinal);
			int end = Source.IndexOf(End, start + Start.Length, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, Start);
			ClassicAssert.Greater(end, start, End);
			return Source.Substring(start, end - start);
		}

		private static void AssertOrdered(string Source, params string[] Terms)
		{
			int offset = 0;
			for (int i = 0; i < Terms.Length; i++)
			{
				int found = Source.IndexOf(Terms[i], offset, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(found, 0, "missing ordered source term: " + Terms[i]);
				offset = found + Terms[i].Length;
			}
		}
	}
}
#endif
