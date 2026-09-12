#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
				// Issue #141: the authority call here is now TryAuthorizedTransition, which
				// dispatches heart and ordinary; the pin moves with the code, not weakened.
				"TryAuthorizedTransition(Owner, Z, beforeIntent, before, Successor, after,",
				"KingdomPlotRules.Fits(Successor.Rect, interior)",
				"survey.PlotRoots",
				"KingdomPlotRules.Reserved(other)",
				"KingdomPlotRules.PlotAreaAllowance(Z.Width, Z.Height)",
				"if (!AllowSettledSuccessor)",
				"probe.TryAcceptExact(Successor.Rect, after, requireExistingRoadEvidence",
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
			ClassicAssert.GreaterOrEqual(paid, 0);
			int settled = proof.IndexOf("HashSet<GameObject> settled", paid,
				StringComparison.Ordinal);
			ClassicAssert.Greater(settled, paid);
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
		public void EnvelopeGrowthAsksTheFoundingAuthorityWhichDispatchesHeartAndOrdinary()
		{
			// Issue #141 (PARTIAL): the old call went straight to TryAuthorizedEnvelopeExpansion,
			// which refuses every heart by design, so a heart whose rects differ - rungs one to
			// four, all of them - could never prove growth. TryAuthorizedTransition already
			// dispatches, and a differing non-heart rect still reaches the expansion authority
			// inside it, so the ordinary route is unchanged.
			string growth = Read("Growth/KingdomArchitectureStamper.EnvelopeGrowth.cs");
			AssertOrdered(growth,
				"if (SameRect(beforeIntent.Rect, Successor.Rect)) return true;",
				"if (!TryAuthorizedTransition(Owner, Z, beforeIntent, before, Successor, after,",
				"false, out bool heartAccretion, out Failure))",
				"return false;");
			// The old direct call to the ordinary authority is gone from this function.
			StringAssert.DoesNotContain(
				"!TryAuthorizedEnvelopeExpansion(Owner, Z, beforeIntent, before, Successor,",
				growth);
			// No caller-supplied bypass, no new parameter, no new field, no plan change, no
			// same-rect shortcut beyond the one that was already there.
			foreach (string forbidden in new[] { "bool HeartAuthority", "AllowHeartAuthority",
				"bool SkipAuthority", "HeartAccretion = true", "AllowPlanChange: true",
				"Successor, after, true, out", "IsHeartTransitionEndpoints" })
				StringAssert.DoesNotContain(forbidden, growth);
			// The ordinary expansion authority still refuses hearts; it was not relaxed.
			AssertOrdered(growth,
				"KingdomPlotRules.HeartRungOf(Before.BuildKey) != 0",
				"KingdomPlotRules.HeartRungOf(After.BuildKey) != 0",
				"Owner.GetIntProperty(KingdomPlots.HeartPlotProperty) == 1",
				"ordinary plot-envelope growth cannot claim founding-heart authority");
		}

		[Test]
		public void EverySixLaterEnvelopeCheckStillRunsInOrderAfterTheAuthorityBlock()
		{
			// Both authorities still prove bounds, ownership and protected ground. Authorized
			// hearts prove physical ingress; ordinary envelopes additionally need road evidence.
			string growth = Read("Growth/KingdomArchitectureStamper.EnvelopeGrowth.cs");
			AssertOrdered(growth,
				"if (!TryAuthorizedTransition(Owner, Z, beforeIntent, before, Successor, after,",
				// 1. interior fit
				"KingdomPlotRules.TryInterior(Z.Width, Z.Height, out interior)",
				"the enlarged authored lot does not fit settlement interior ground",
				// 2. malformed / out-of-zone geometry
				"the loaded zone carries malformed or out-of-zone plot geometry",
				// 3. plot overlap and road budget
				"the enlarged authored lot would consume the reserved lane of ",
				"standing plot ownership is absent or ambiguous in the loaded zone",
				"KingdomPlotRules.PlotAreaAllowance(Z.Width, Z.Height)",
				"the enlarged authored lot would spend settlement road ground",
				// 4. siting probe / frozen envelope, both branches
				"KingdomArchitectureRuntime.TryCreateSitingProbe(System, Z, Successor.Rect,",
				"probe.TryAcceptExact(Successor.Rect, after, requireExistingRoadEvidence,",
				"KingdomArchitectureRuntime.TryAcceptFrozenEnvelope(Z, Successor.Rect,",
				// 5. settled outputs
				"TryReadSettledExpansionOutputs(Owner, SuccessorOwner, Z, beforeIntent,",
				// 6. per-cell sweep, ending on the founding-heart ground refusal it always made
				"plot-envelope growth would cover stairs or a zone connection at ",
				"plot-envelope growth would cover open liquid at ",
				"plot-envelope growth overlaps another active paid construction at ",
				"plot-envelope growth would absorb public road ground at ",
				"a living occupant stands on plot-envelope growth ground at ",
				"founding-heart ground occupies plot-envelope growth at ");
		}

		/// <summary>
		/// A body only stands in the way of an annexed cell the successor map declares Blocked, and
		/// the non-mutating preflight tolerates one the crew may lawfully stand aside, because the
		/// mutating path clears it before proving this again. Run 42's NoGroundToGrow was the
		/// founder standing on annexed YARD ground of the rung-3 moot.
		/// Mutation: dropping the SlotBlocksOccupant clause refuses every annexed cell a body
		/// stands on again; dropping TolerateMovableOccupants makes Assess refuse ground Begin is
		/// about to clear, so the climb never starts.
		/// </summary>
		[Test]
		public void OnlyABlockedAnnexedCellIsBlockedByABody()
		{
			string envelope = Read("Growth/KingdomArchitectureStamper.EnvelopeGrowth.cs");
			AssertOrdered(envelope,
				"bool TolerateMovableOccupants = false)",
				"if (!TryPlacementPassability(Successor, Z,",
				"out Dictionary<int, ArchitecturePassability> successorSlots, out Failure)",
				"if (item.IsCreature || item.IsPlayer())",
				"if (!successorSlots.TryGetValue(packed, out declared)",
				"|| !KingdomPlotRules.SlotBlocksOccupant(declared)) continue;",
				"if (TolerateMovableOccupants",
				"&& KingdomPlots.IsMovableEnvelopeOccupant(System, Z, item)) continue;",
				"KingdomPlots.NameEnvelopeOccupant(System, Z, item, declared);",
				"a living occupant stands on plot-envelope growth ground at ");
			// Only the preflight tolerates them; the mutating application path does not.
			string preflight = Read("Growth/KingdomArchitectureStamper.UpgradePreflight.cs");
			StringAssert.Contains("out Failure, TolerateMovableOccupants: true)) return false;",
				preflight);
			string application = Read("Growth/KingdomArchitectureStamper.UpgradeApplication.cs");
			StringAssert.DoesNotContain("TolerateMovableOccupants", application);
		}

		/// <summary>
		/// The improvement route clears its annexed ground through the plot clearance, not a copy
		/// of it: same ladder, same destinations, same walk-back, same sentences. Only annexed,
		/// blocked cells are scanned, and the whole successor rect is excluded as a destination.
		/// </summary>
		[Test]
		public void TheImprovementRouteClearsGroundThroughThePlotClearance()
		{
			string clearance = Read("Growth/KingdomPlot2.26e.EnvelopeClearance.cs");
			AssertOrdered(clearance,
				"internal static bool TryClearEnvelopeOccupants(",
				"KingdomArchitectureStamper.TryPlacementPassability(Successor, Z,",
				"if (!KingdomPlotRules.SlotBlocksOccupant(slot.Value)) continue;",
				"if (Before.Contains(slot.Key % Z.Width, slot.Key / Z.Width)) continue;",
				"return TryClearManagedOccupants(System, Z, Owner, new HashSet<int>(annexed.Keys),",
				"annexed, Successor.Rect, out Moved, out Beasts, out Post,",
				"internal static void SayEnvelopeCleared(",
				"SayPlotWorkCleared(System, Owner, Name, Moved - Beasts, Raised, Fault);",
				"SayPlotBeastsDriven(System, Owner, Name, Beasts, Raised, Fault);",
				"internal static bool IsMovableEnvelopeOccupant(",
				"KingdomPlotRules.IsMovableOccupant(ReasonFor(System, survey, Body))",
				"internal static void NameEnvelopeOccupant(");
			StringAssert.DoesNotContain("SystemLongDistanceMoveTo", clearance);
			// Begin is the mutating path, so the clearance runs there and never in Assess.
			string begin = Read("Growth/KingdomUpgrade.14.Begin.cs");
			AssertOrdered(begin,
				"KingdomArchitectureIntent successorLayout = Prepared?.Architecture;",
				"out successorLayout, out _, out _, out architectureFailure)",
				"ClearImprovementGround(System, Z, Work, successorLayout, Cleared);");
			// The sentence waits for the improvement's own outcome: the clearance only records
			// what it did, and BeginCore says it after BeginCommit has returned.
			AssertOrdered(begin,
				"bool begun = BeginCommit(System, Z, Work, A, Survey, Prepared, cleared);",
				"if (cleared.Moved > 0 || cleared.Post != null)",
				"KingdomPlots.SayEnvelopeCleared(System, Work, Work.ShortDisplayName,",
				"cleared.Moved, cleared.Beasts, cleared.Post, begun, cleared.Fault);",
				"return begun;",
				"private static bool BeginCommit(");
			StringAssert.DoesNotContain("SayEnvelopeCleared(System, Work, Work.ShortDisplayName, moved, beasts,",
				begin);
			AssertOrdered(begin,
				"private static void ClearImprovementGround(",
				"KingdomPlots.TryClearEnvelopeOccupants(System, Z, Work, Successor, before,",
				"KingdomLog.Log(\"architecture: improvement ground clearance refused: \" + refusal)",
				"Cleared.Moved = moved;",
				"Cleared.Beasts = beasts;",
				"Cleared.Post = post;",
				"KingdomLog.Log(\"architecture: improvement ground cleared: \"");
			string assess = Read("Growth/KingdomUpgrade.10.Assessment.cs");
			StringAssert.DoesNotContain("TryClearEnvelopeOccupants", assess);
			StringAssert.DoesNotContain("ClearImprovementGround", assess);
		}

		[Test]
		public void EveryRetryCallSiteStillProvesEnvelopeGrowthUnconditionally()
		{
			// Pre-debit and paid application both reach the proof, and neither gained a
			// heart-shaped exemption from it.
			string preflight = Read("Growth/KingdomArchitectureStamper.UpgradePreflight.cs");
			StringAssert.Contains(
				"TryProveEnvelopeGrowth(System, Z, Owner, null, Successor, false,", preflight);
			string application = Read(
				"Growth/KingdomArchitectureStamper.UpgradeApplication.cs");
			StringAssert.Contains(
				"TryProveEnvelopeGrowth(system, Z, Owner, Target, Successor, true,", application);
			foreach (string source in new[] { preflight, application })
				foreach (string forbidden in new[] { "heartAccretion &&", "!heartAccretion &&",
					"HeartRungOf", "HeartPlotProperty" })
					StringAssert.DoesNotContain(forbidden, source);
		}

		[Test]
		public void SurveyedHeartRequiresPhysicalIngressBeforeBothProofBranches()
		{
			// Wiring tripwire, not a native reachability claim. The real camp persona must
			// prove the roadless heart transition; protected-ground checks remain below.
			string growth = Read("Growth/KingdomArchitectureStamper.EnvelopeGrowth.cs");
			AssertOrdered(growth,
				"false, out bool heartAccretion, out Failure))",
				"if (heartAccretion && !KingdomArchitectureRuntime.TryVerifyPhysicalIngressRoutes(",
				"Z, Successor.Rect, after, out Failure)) return false;",
				"bool requireExistingRoadEvidence = !heartAccretion;",
				"if (!AllowSettledSuccessor)",
				"probe.TryAcceptExact(Successor.Rect, after, requireExistingRoadEvidence,",
				"after, requireExistingRoadEvidence, out Failure)) return false;",
				"plot-envelope growth would absorb public road ground at ");
			StringAssert.DoesNotContain("requireExistingRoadEvidence = false", growth);
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
			string removal = Read(
				"Growth/KingdomArchitectureStamper.UpgradeRemoval.cs");
			StringAssert.Contains("state == 1 && found == KingdomPhysicalLookupState.Absent",
				removal);
			StringAssert.Contains("threw before changing exact state", removal);
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
				ClassicAssert.GreaterOrEqual(found, 0, "missing ordered term: " + Terms[i]);
				offset = found + Terms[i].Length;
			}
		}
	}
}
#endif
