#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPlotEnvelopeGrowthSourceTests
	{
		[Test]
		public void ExpandingResolutionUsesAdjacentLargerExactLineageAndFrozenPose()
		{
			string source = Read("Growth/KingdomArchitecture.ExpansionResolution.cs");
			AssertOrdered(source,
				"(ArchitectureLotSize)((int)StandingLotSize + 1)",
				"ExactRecordKey(SuccessorBuildKey, type,",
				"target.View.PlanKey != PlanKey || Fold(target.View.TypeKey) != type",
				"target.Tier.Level != predecessor.Tier.Level + 1",
				"KingdomArchitectureTransitionRules.AllowsLotExpansion(",
				"TrySelectFrozenSuccessorVariant(",
				"CompileFrozen(frozen, target, variant, Facing");
			StringAssert.DoesNotContain("for (int size", source);
			StringAssert.DoesNotContain("TrySelectVariant(", source);
			StringAssert.DoesNotContain("target.View.BindingKey != BindingKey", source);
		}

		[Test]
		public void CatalogueAllowsOnlyAnAdjacentFullyFrozenExpansionEdge()
		{
			string resolution = Read("Growth/KingdomArchitecture.ExpansionResolution.cs");
			AssertOrdered(resolution,
				"HasAuthorizedEnvelopeSuccessor(",
				"(int)afterSize != (int)beforeSize + 1",
				"before.View.PlanKey != after.View.PlanKey",
				"after.Tier.Level != before.Tier.Level + 1",
				"KingdomArchitectureTransitionRules.AllowsLotExpansion(",
				"TrySelectFrozenSuccessorVariant(");
			string catalogue = Read("Core/KingdomData.Catalogue.cs");
			StringAssert.Contains("SuccessorEnvelopeGrowth = chain != null && spec != null",
				catalogue);
			StringAssert.Contains("KingdomArchitecture.HasAuthorizedEnvelopeSuccessor(",
				catalogue);
			string validation = Read("Growth/KingdomCatalogueRules.ChainValidation.cs");
			StringAssert.Contains("!IsAuthoredEnvelopeGrowth(Entry, successor)", validation);
			StringAssert.Contains("(int)Successor.Plot == (int)Entry.Plot + 1", validation);
		}

		[Test]
		public void UpgradePreparationFreezesFirstValidContainingPoseWithoutLayoutScoring()
		{
			string source = Read("Growth/KingdomArchitectureRuntime.EnvelopeGrowth.cs");
			AssertOrdered(source,
				"TryPrepareSuccessor(System, Z, Before, SuccessorBuildKey",
				"KingdomArchitecture.HasExactOrdinarySuccessor(before.BuildKey,",
				"TryResolveExpandingSuccessor(before.BuildKey,",
				"TryBuildDelta(before, after,",
				"KingdomPlotPoseSitingRules.EnumerateContaining(",
				"TryWorldCoordinate(after, rect, after.MainX, after.MainY,",
				"mainX != Before.MainWorldX || mainY != Before.MainWorldY",
				"probe.TryAcceptExact(rect, after, true",
				"TryProveEnvelopeGrowth(System, Z, Owner, null,",
				"Intent = prepared;");
			StringAssert.DoesNotContain("ChooseRect", source);
			StringAssert.DoesNotContain("TryFindRect", source);
		}

		[Test]
		public void EnvelopeProofIsReadOnlyAndRejectsCrowdingRoadsForeignStateAndLife()
		{
			string source = Read("Growth/KingdomArchitectureStamper.EnvelopeGrowth.cs");
			AssertOrdered(source,
				"System.ClaimedZones.Contains(Z.ZoneID)",
				"TryAuthorizedEnvelopeExpansion(Owner, Z, beforeIntent, before, Successor,",
				"KingdomPlotRules.Fits(Successor.Rect, interior)",
				"survey.PlotRoots",
				"KingdomPlotRules.Reserved(other)",
				"KingdomPlotRules.PlotAreaAllowance(Z.Width, Z.Height)",
				"if (!AllowSettledSuccessor)",
				"probe.TryAcceptExact(Successor.Rect, after, true",
				"TryAcceptFrozenEnvelope(Z, Successor.Rect,",
				"ConnectionCells(Z)",
				"ReadWornRoadCells(Z)",
				"KingdomRoads.FindOurFloor(cell, out road)",
				"item.IsCreature || item.IsPlayer()",
				"KingdomPlots.ReadObject(item) == KingdomPlotRules.GroundKind.Bare");
			StringAssert.Contains("KingdomArchitectureTransitionRules.AllowsLotExpansion(", source);
			StringAssert.Contains("survey.Objects", source);
			StringAssert.Contains("KingdomPlots.HasRectEvidence(candidate)", source);
			StringAssert.Contains("malformed or out-of-zone plot geometry", source);
			StringAssert.DoesNotContain(
				"if (!KingdomPlots.TryReadRect(root, out other)) continue;", source);
			StringAssert.DoesNotContain(
				"== ArchitectureTransitionMode.RenovateExpand", source);
			StringAssert.DoesNotContain("ReserveExactWater", source);
			StringAssert.DoesNotContain("ReservePayment", source);
			StringAssert.DoesNotContain("SetIntProperty", source);
			StringAssert.DoesNotContain("SetStringProperty", source);
			StringAssert.DoesNotContain("Destroy(", source);
			StringAssert.DoesNotContain("AddObject(", source);
		}

		[Test]
		public void PaidExpansionRetryCannotRereadMutableArchitectureSelection()
		{
			string proof = Read("Growth/KingdomArchitectureStamper.EnvelopeGrowth.cs");
			int paid = proof.IndexOf(
				"else if (!KingdomArchitectureRuntime.TryAcceptFrozenEnvelope(",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(paid, 0);
			int settled = proof.IndexOf("HashSet<GameObject> settled", paid,
				StringComparison.Ordinal);
			Assert.Greater(settled, paid);
			string paidBranch = proof.Substring(paid, settled - paid);
			StringAssert.DoesNotContain("TryCreateSitingProbe", paidBranch);
			StringAssert.DoesNotContain("KingdomArchitecture.", paidBranch);

			string frozen = Read("Growth/KingdomArchitectureRuntime.FrozenEnvelope.cs");
			StringAssert.Contains("TryValidateFrozenSnapshot(Snapshot", frozen);
			StringAssert.Contains("TryPhysicalRoadIngressScore(Z, Rect, Snapshot", frozen);
			StringAssert.Contains("Snapshot.LotSize", frozen);
			StringAssert.DoesNotContain("KingdomArchitecture.", frozen);
			StringAssert.DoesNotContain("TrySelectionContext", frozen);
			StringAssert.DoesNotContain("KingdomArchitectureMapping", frozen);
		}

		[Test]
		public void PreDebitReproofAndApplicationBothRunBeforeMutation()
		{
			string assessment = Read("Growth/KingdomUpgrade.10.Assessment.cs");
			AssertOrdered(assessment,
				"KingdomUpgradeRules.IsReady(assessment.Verdict)",
				"ImprovementGroundRefused(System, Z, Work, assessment,",
				"TryPrepareImprovementPayload(System, Z, Work, A,",
				"return legacy && KingdomPlots.GrowRefused(");
			string preparation = Read("Growth/KingdomUpgrade.15.Prepare.cs");
			StringAssert.Contains("TryPrepareSuccessorForUpgrade(System, Z, Work,", preparation);
			string preflight = Read("Growth/KingdomArchitectureStamper.UpgradePreflight.cs");
			AssertOrdered(preflight,
				"TryAuthorizedTransition(Owner, Z, beforeIntent, before, Successor, after,",
				"TryProveEnvelopeGrowth(System, Z, Owner, null, Successor, false,",
				"TryBuildDelta(before, after");
			string application = Read("Growth/KingdomArchitectureStamper.UpgradeApplication.cs");
			AssertOrdered(application,
				"TryUpgradeBase(Owner, Z, Successor,",
				"TryProveEnvelopeGrowth(system, Z, Owner, Target, Successor, true,",
				"TryBeginUpgradeReceipt(Owner, Target, Successor",
				"TryReserveAuthoredGrowthEnvelope(Owner, Target, Successor,",
				"TryRemoveUpgradeSlot(Owner",
				"TryStageLayer(Target, Z, ArchitectureLayer.Ground");
			string authority = Read("Growth/KingdomArchitectureStamper.Transitions.cs");
			AssertOrdered(authority,
				"if (!SameRect(BeforeIntent.Rect, AfterIntent.Rect))",
				"TryAuthorizedEnvelopeExpansion(Owner, Z, BeforeIntent, Before,",
				"KingdomSocketTransitionRules.AuthorizesFixedLotTransition(");
		}

		[Test]
		public void PaidExpansionPublishesReservationBeforeSceneryAndRetriesExactCuts()
		{
			string application = Read(
				"Growth/KingdomArchitectureStamper.UpgradeApplication.cs");
			AssertOrdered(application,
				"TryBeginUpgradeReceipt(Owner, Target, Successor",
				"TryReserveAuthoredGrowthEnvelope(Owner, Target, Successor,",
				"Owner.SetIntProperty(UpgradePhaseProperty, 2)",
				"TryRemoveUpgradeSlot(Owner",
				"TryCarryUpgradeSlot(Owner, Target",
				"TryStageLayer(Target, Z, ArchitectureLayer.Ground",
				"Owner.SetIntProperty(UpgradePhaseProperty, 5)");

			string reservation = Read(
				"Growth/KingdomPlot2.20b.AuthoredGrowthReservation.cs");
			AssertOrdered(reservation,
				"ExactOrAbsentString(Successor, PlotIdProperty, plotId)",
				"ExactOrAbsentInt(Successor, PlotX1Property, Intent.Rect.X1)",
				"Successor.SetStringProperty(PlotIdProperty, plotId)",
				"Successor.SetIntProperty(PlotX2Property, Intent.Rect.X2)",
				"TryReadRect(Successor, out observed)");
			StringAssert.Contains("out bool Divergent", reservation);

			string receipts = Read(
				"Growth/KingdomArchitectureStamper.UpgradeReceipts.cs");
			StringAssert.Contains("state == 1 && found == KingdomPhysicalLookupState.Absent",
				receipts);
			StringAssert.Contains("threw before changing exact state", receipts);
			StringAssert.Contains("UpgradeQuarantine(Owner", receipts);
			StringAssert.Contains("phase < 0 || phase > 5", receipts);

			string retag = Read("Growth/KingdomArchitectureStamper.UpgradeRetag.cs");
			AssertOrdered(retag,
				"TryExactRetagPrefix(Item, Z, Before, After, Lot",
				"Item.RemoveIntProperty(ComponentSchemaProperty)",
				"Item.SetIntProperty(ComponentCarriedProperty, 1)",
				"Item.SetIntProperty(ComponentSchemaProperty, ComponentSchema)");
			StringAssert.Contains("OldOrNewString(Item, ComponentHashProperty", retag);

			string handover = Read("Growth/KingdomUpgrade.24.HandoverContents.cs");
			StringAssert.Contains("RetryOrQuarantineAuthoredLayout", handover);
			StringAssert.Contains("KingdomConstruction.FinishProjection(ref Job, false, false",
				handover);
		}

		private static string Read(string Relative)
		{
			return TestMain.ReadRepositoryText(Relative);
		}

		private static void AssertOrdered(string Source, params string[] Terms)
		{
			int offset = 0;
			for (int i = 0; i < Terms.Length; i++)
			{
				int found = Source.IndexOf(Terms[i], offset, StringComparison.Ordinal);
				Assert.GreaterOrEqual(found, 0, "missing ordered term: " + Terms[i]);
				offset = found + Terms[i].Length;
			}
		}
	}
}
#endif
