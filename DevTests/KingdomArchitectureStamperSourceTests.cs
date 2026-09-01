#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomArchitectureStamperSourceTests
	{
		private static string Stamper()
		{
			return KingdomArchitectureStamperLogicalSource.Read();
		}

		private static string Plot()
		{
			return KingdomPlot2LogicalSource.Read();
		}

		private static string Upgrade()
		{
			return KingdomUpgradeLogicalSource.Read();
		}

		private static string Socket()
		{
			return KingdomSocketLogicalSource.Read();
		}

		private static string Materials()
		{
			return KingdomMaterialsLogicalSource.Read();
		}

		[Test]
		public void LogicalAuthorityKeepsReceiptAbiAndMethodOrder()
		{
			string source = Stamper();
			StringAssert.Contains("public static partial class KingdomArchitectureStamper", source);
			AssertOrdered(source,
				"public const int LayoutSchema = 1;",
				"public const int ComponentSchema = 1;",
				"public const int MaxFailureChars = 512;",
				"private const int MaxLotIdChars = 256;",
				"public const string SchemaProperty = \"r_TAF_LayoutSchema\";",
				"public const string OutputStatePrefix = \"r_TAF_LayoutOutputState_\";",
				"public const string ComponentSchemaProperty = \"r_TAF_LayoutComponentSchema\";",
				"public const int UpgradeSchema = 2;",
				"public const string UpgradeRetainPrefix = \"r_TAF_LayoutUpgradeRetain_\";");
			AssertOrdered(source, "public static bool TryPreflight(",
				"public static bool TryPreflightUpgrade(",
				"public static bool TryPreflightStrike(",
				"public static bool TryValidateFrozenUpgrade(",
				"public static bool TryInitializeOwner(",
				"public static bool TryStageLayer(",
				"private static bool TryVerifyLayer(",
				"private static bool TryPlacementClaim(",
				"private static bool TryAuthorizedTransition(",
				"private static bool TryBeginUpgradeReceipt(",
				"private static bool TryBlueprintPassAudit(",
				"private static bool TryRollbackNewLayout(",
				"private static string Bounded(");
		}

		[Test]
		public void PreflightProvesCurrentFrozenTruthAndProtectionWithoutMutation()
		{
			string source = Stamper();
			string preflight = Between(source, "public static bool TryPreflight(",
				"public static bool TryPreflightUpgrade(");
			AssertOrdered(preflight, "KingdomArchitectureRuntime.TryDecode(Intent",
				"KingdomArchitectureRules.IsLatestSnapshotEncoding(Intent.EncodedSnapshot)",
				"GameObjectFactory.Factory.HasBlueprint(placement.Blueprint)",
				"KingdomArchitectureRules.TryParseTech(placement.MinTech",
				"KingdomZoningRules.MissingKnowledge(roster, placement.Knowledge)",
				"placement.Power", "KingdomMaterialRules.TryParseMaterial(placement.Material",
				"PaidClaim.Materials.Get(material) <= 0");
			StringAssert.Contains("legacy architecture snapshots are read-only", preflight);
			StringAssert.Contains("TryManagedCells(Intent, Z", preflight);
			StringAssert.Contains("TryExistingBindings(Z, snapshot, Intent.Rect", preflight);
			StringAssert.Contains("TryBlueprintPassAudit(snapshot", preflight);
			StringAssert.Contains("ConnectionCells(Z)", preflight);
			StringAssert.Contains("cell.HasStairs()", preflight);
			StringAssert.Contains("cell.HasOpenLiquidVolume()", preflight);
			StringAssert.Contains("KingdomConstruction.HasActiveAt(System, Z, cell)", preflight);
			StringAssert.Contains("item.IsCreature || item.IsPlayer()", preflight);
			StringAssert.Contains("KingdomMaterials.IsProtected(item, out reason)", preflight);
			StringAssert.Contains("KingdomPlotRules.Refuses(ground)", preflight);
			Assert.IsFalse(preflight.Contains("SetIntProperty"));
			Assert.IsFalse(preflight.Contains("SetStringProperty"));
			Assert.IsFalse(preflight.Contains("GameObject.Create"));
			Assert.IsFalse(preflight.Contains("AddObject"));
			Assert.IsFalse(preflight.Contains("Reserve"));
			Assert.IsFalse(preflight.Contains("TryDebit"));
			Assert.IsFalse(preflight.Contains("CommitDebit"));
		}

		[Test]
		public void OwnerReceiptIsSchemaLastCurrentOnlyAndCopiedFromFrozenAuthority()
		{
			string source = Stamper();
			string initialize = Between(source, "public static bool TryInitializeOwner(",
				"public static bool TryReadOwner(");
			AssertOrdered(initialize, "KingdomArchitectureRuntime.TryDecode(Intent",
				"KingdomArchitectureRules.IsManagedSnapshotEncoding(Intent.EncodedSnapshot)",
				"TryAcceptNewOwnerPrefix(Owner, Intent, snapshot, LotId, 0, false, null",
				"Owner.SetStringProperty(LotIdProperty, LotId)",
				"Owner.SetStringProperty(HashProperty, Intent.SnapshotHash)",
				"Owner.SetIntProperty(NextLayerProperty, 0)",
				"Owner.SetIntProperty(SchemaProperty, LayoutSchema)",
				"TryReadOwner(Owner, out readIntent");
			Assert.AreEqual(initialize.IndexOf(
				"Owner.SetIntProperty(SchemaProperty, LayoutSchema);", StringComparison.Ordinal),
				initialize.LastIndexOf("Owner.Set", StringComparison.Ordinal),
				"layout schema must be the final receipt write");

			string read = Between(source, "public static bool TryReadOwner(",
				"public static bool TryCopyFrozenOwner(");
			string header = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.OwnerReceiptPrefixes.cs");
			StringAssert.Contains("receipt is absent, partial, or unknown", header);
			StringAssert.Contains("layout owner is quarantined", header);
			StringAssert.Contains("KingdomArchitectureRuntime.TryRead(Owner", header);
			StringAssert.Contains("KingdomArchitectureRuntime.TryDecode(Intent", header);
			StringAssert.Contains("KingdomArchitectureRules.IsManagedSnapshotEncoding", header);
			StringAssert.Contains("hash != Intent.SnapshotHash", header);
			StringAssert.Contains("placement.Layer < next && state != 2", read);

			string copy = Between(source, "public static bool TryCopyFrozenOwner(",
				"public static bool TryManagedCells(");
			AssertOrdered(copy, "Source.GetIntProperty(NextLayerProperty) != 3",
				"TryAcceptNewOwnerPrefix(Target, intent, snapshot, lot, 3, true, Source",
				"KingdomArchitectureRuntime.TryCopyFrozen(Source, Target",
				"Target.SetStringProperty(OutputId(placement)",
				"Target.SetIntProperty(OutputState(placement), 2)",
				"Target.SetIntProperty(SchemaProperty, LayoutSchema)",
				"ExactCopiedOwner(Target, Source");

			string durable = source.Substring(source.IndexOf(
				"public static bool TryInitializeOwner(", StringComparison.Ordinal));
			Assert.IsFalse(durable.Contains("KingdomArchitecture.TryGetMapping"));
			Assert.IsFalse(durable.Contains("KingdomArchitecture.TryResolve"));
			Assert.IsFalse(durable.Contains("KingdomData"));
		}

		[Test]
		public void LayersPublishExactPerSlotIdentityBeforeInsertionAndFailClosedOnInterruption()
		{
			string source = Stamper();
			string stage = Between(source, "public static bool TryStageLayer(",
				"public static bool TryVerifyComplete(");
			StringAssert.Contains("next > target", stage);
			StringAssert.Contains("layout layers must settle ground, structure, then object", stage);
			AssertOrdered(stage, "TrySettlePlacement(Owner, Z",
				"Owner.SetIntProperty(NextLayerProperty, target + 1)",
				"TryVerifyLayer(Owner, Z");

			string settle = Between(source, "private static bool TrySettlePlacement(",
				"private static bool TryVerifyLayer(");
			StringAssert.Contains("if (state == 2)", settle);
			StringAssert.Contains("KingdomConstruction.FindExactId(Z", settle);
			StringAssert.Contains("lost its published output before settlement", settle);
			StringAssert.Contains("changed after output publication", settle);
			AssertOrdered(settle, "CanInsert(Owner, Z, cell", "GameObject.Create(Placement.Blueprint)",
					"StampComponent(Owner, placed, Lot, Intent.SnapshotHash, Placement)",
					"RootStagingOutput(placed)", "Owner.SetStringProperty(idProperty, placed.ID)",
					"Owner.SetIntProperty(stateProperty, 1)",
					"cell.AddObject(placed",
				"bool exactEndpoint = ExactComponent(Owner, placed, Z, Intent, Lot, Placement",
				"bool exactCustody = Placement.ExistingAuthority",
				"KingdomFoundingHeartTerminalRules.ExactAddCut(callbackReturned",
				"Owner.SetIntProperty(stateProperty, 2)");
			StringAssert.Contains("object.ReferenceEquals(accepted, placed)", settle);
			StringAssert.Contains("object.ReferenceEquals(rootedOutput, placed)", settle);

			string exact = Between(source, "private static bool ExactComponent(",
				"private static bool CanInsert(");
			StringAssert.Contains("KingdomPlots.PlotIdProperty", exact);
			StringAssert.Contains("ComponentSlotProperty", exact);
			StringAssert.Contains("ComponentLayerProperty", exact);
			StringAssert.Contains("ComponentAnchorProperty", exact);
			StringAssert.Contains("ComponentHashProperty", exact);
			StringAssert.Contains("ComponentTokenProperty", exact);
			StringAssert.Contains(
				"ExactComponentInt(Item, ComponentSchemaProperty, ComponentSchema)", exact);
			StringAssert.Contains(
				"ExactComponentString(Item, KingdomPlots.PlotIdProperty, Lot)", exact);
			StringAssert.Contains(
				"ExactComponentString(Item, ComponentSlotProperty, Placement.Slot)", exact);
			StringAssert.Contains(
				"ExactComponentInt(Item, ComponentLayerProperty, (int)Placement.Layer)", exact);
			StringAssert.Contains(
				"ExactComponentString(Item, ComponentHashProperty, Intent.SnapshotHash)", exact);
			StringAssert.Contains(
				"ExactComponentString(Item, ComponentTokenProperty", exact);
			StringAssert.Contains(
				"ExactComponentInt(Item, ComponentExistingProperty", exact);
			StringAssert.Contains(
				"ExactComponentInt(Item, KingdomPlots.PlotPartProperty", exact);
			StringAssert.Contains(
				"ExactOptionalComponentString(Item, ComponentAnchorProperty", exact);
			StringAssert.Contains(
				"ExactOptionalComponentInt(Item, ComponentCarriedProperty, 1)", exact);
			StringAssert.Contains("ExactPendingComponentState(Owner, Item, Intent)", exact);
			StringAssert.Contains("KingdomArchitectureRuntime.TryWorldPlacement", exact);
			StringAssert.Contains("return count == 1", exact);
			StringAssert.Contains("Owner.SetStringProperty(FaultProperty, Failure)", source);
			Assert.IsFalse(source.Contains("Stat.Random"));
			Assert.IsFalse(source.Contains("GetRandomElement"));
		}

		[Test]
		public void PlotPreflightsThenStagesAuthoredLayersWithoutProceduralShellOrFurnishing()
		{
			string source = Plot();
			string prepare = Between(source,
				"internal static bool TryPreparePlotPayload(KingdomSystem System, Zone Z,\n\t\t\tKingdomPlotRules.PlotRect Rect, string BuildKey, string LotType, string SkinKey,",
				"internal static bool TryEncodePlotPayload(");
			AssertOrdered(prepare, "KingdomArchitectureRuntime.TryPrepare(System, Z, Rect, BuildKey, LotType",
				"new KingdomMaterialDebitCost(",
				"KingdomArchitectureStamper.TryPreflight(System, Z, prepared, claim",
				"TryEncodePlotPayload(Rect, SkinKey, prepared");
			Assert.IsFalse(prepare.Contains("ReserveExactWater"));
			Assert.IsFalse(prepare.Contains("ReservePayment"));

			string stake = Between(source, "private static GameObject Stake(",
				"private static bool RemoveCreatedWorks(");
			AssertOrdered(stake, "KingdomArchitectureRuntime.TryFreeze(",
				"KingdomArchitectureStamper.TryInitializeOwner(",
				"KingdomConstruction.UpdateOutput(ref Job, works.ID)",
				"cell.AddObject(works, NoStack: Heart != null)");

			string apply = Between(source, "private static bool Apply(",
				"private static void PrepareFinalBuilding(");
			StringAssert.Contains("KingdomArchitectureStamper.TryReadOwner(parent", apply);
			AssertOrdered(apply, "KingdomArchitectureStamper.TryManagedCells(authored",
				"ClearGround(Works, zone, plot, footprint, roof, managed)",
				"ArchitectureLayer.Ground");
			StringAssert.Contains("ArchitectureLayer.Structure", apply);
			StringAssert.Contains("ArchitectureLayer.Object", apply);
			StringAssert.Contains("else RaiseFrame(Works, zone, footprint, roof)", apply);
			StringAssert.Contains("else RaiseWalls(Works, zone, footprint, roof)", apply);
			StringAssert.Contains("KingdomArchitectureStamper.TryVerifyComplete", apply);

			string clear = Between(source, "private static bool ClearGround(",
				"private static bool ExactClearSource(");
			StringAssert.Contains("HashSet<int> AuthoredCells = null", clear);
			StringAssert.Contains("AuthoredCells != null && !AuthoredCells.Contains", clear);

			string finish = Between(source, "private static bool Finish(r_KingdomPlotWorks Works,",
				"private static bool FinishPlotEffects(");
			StringAssert.Contains("KingdomArchitectureStamper.TryVerifyComplete(parent", finish);
			StringAssert.Contains("KingdomArchitectureStamper.TryCopyFrozenOwner(parent, building", finish);
			StringAssert.Contains("else if (!FurnishDurable", finish);
			StringAssert.Contains("else if (!currentAuthored && !FurnishLegacyDurable", finish);

			string finalProof = Between(source, "private static bool ExactFinalBuilding(",
				"private static bool ClearGround(");
			StringAssert.Contains("KingdomArchitectureStamper.TryVerifyComplete(Building, Z", finalProof);
		}

		[Test]
		public void UpgradeUsesFrozenSuccessorDeltaAndNeverProceduralGrowthForA2()
		{
			string stamper = Stamper();
			string apply = Between(stamper, "public static bool TryApplyUpgrade(",
				"public static bool TryInitializeOwner(");
			AssertOrdered(apply, "TryUpgradeBase(Owner, Z, Successor",
				"TryBeginUpgradeReceipt(Owner, Target, Successor",
				"TryRemoveUpgradeSlot(Owner", "TryCarryUpgradeSlot(Owner, Target",
				"delta.Retained[i], delta.RetainedAfter[i]",
				"TryStageLayer(Target, Z, ArchitectureLayer.Ground",
				"TryVerifyComplete(Target, Z");
			Assert.IsFalse(apply.Contains("KingdomArchitecture.TryResolve"));
			Assert.IsFalse(apply.Contains("KingdomData"));
			Assert.IsFalse(apply.Contains("GrowInPlace"));

			string upgrade = Upgrade();
			string prepare = Between(upgrade, "private static bool TryPrepareImprovementPayload(",
				"private static bool TryReadImprovementArchitecture(");
			AssertOrdered(prepare, "KingdomArchitectureRuntime.TryRead(Work",
				"KingdomArchitectureRuntime.TryPrepareSuccessorForUpgrade(System, Z, Work",
				"KingdomArchitectureStamper.TryPreflightUpgrade(System, Z, Work, successor",
				"KingdomPlots.TryEncodePlotPayload(successor.Rect");
			Assert.IsFalse(prepare.Contains("Reserve"));
			string projection = Between(upgrade, "private static bool ProjectImprovement(",
				"private static bool ExpectedImprovementScaffold(");
			AssertOrdered(projection, "TryReadImprovementArchitecture(Work, Job",
				"KingdomArchitectureRuntime.TryFreeze(scaffold, architecture",
				"KingdomConstruction.UpdateOutput(ref Updated, scaffold.ID)",
				"cell.AddObject(scaffold)");
			Assert.IsFalse(projection.Contains("KingdomArchitectureRuntime.TryPrepare"));
			string handover = upgrade.Substring(upgrade.IndexOf(
				"public static void HandOver(", StringComparison.Ordinal));
			StringAssert.Contains("KingdomArchitectureStamper.TryApplyUpgrade", handover);
			StringAssert.Contains("KingdomPlots.TryStampAuthoredGrowth", handover);
		}

		[Test]
		public void PlanChangeAuthorityPrecedesDebitAndRebindsEveryPaidApplication()
		{
			string transition = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.Transitions.cs");
			StringAssert.Contains("AuthorizesFixedLotTransition", transition);
			StringAssert.Contains("needsRouteAuthority && !AllowPlanChange", transition);
			StringAssert.Contains("KingdomSocketTransitions.Authorizes(Owner", transition);
			StringAssert.DoesNotContain(
				"Before == null || After == null || Before.PlanKey != After.PlanKey", transition);
			AssertOrdered(transition, "Before.LotType == After.LotType",
				"Before.LotSize == After.LotSize",
				"SameRect(BeforeIntent.Rect, AfterIntent.Rect)",
				"Before.Facing == After.Facing",
				"BeforeIntent.MainWorldX == AfterIntent.MainWorldX",
				"ValidLotId(Owner.GetStringProperty(LotIdProperty))");

			string preflight = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.UpgradePreflight.cs");
			string declared = Between(preflight,
				"public static bool TryPreflightPlanTransition(",
				"private static bool TryPreflightUpgradeCore(");
			AssertOrdered(declared, "TryReadOwner(Owner",
				"KingdomSocketTransitions.TryResolveCurrent(Transition",
				"ExactTransitionClaim(PaidClaim, declared.Materials)",
				"return TryPreflightUpgradeCore(System, Z, Owner, Successor, PaidClaim, true");

			string prepare = TestMain.ReadRepositoryText("Growth/KingdomUpgrade.15.Prepare.cs");
			StringAssert.Contains("TryPreflightPlanTransition(System, Z, Work", prepare);
			string begin = TestMain.ReadRepositoryText("Growth/KingdomUpgrade.14.Begin.cs");
			AssertOrdered(begin, "TryReprovePreparedImprovement(System, Z, Work",
				"Survey.ReserveExactWater(A.CostDrams)");

			string components = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.Components.cs");
			StringAssert.Contains("TryAuthorizedTransition(Owner, Z, BeforeIntent, Before, " +
				"Successor, After,", components);
			StringAssert.Contains("false, out heartAccretion", components);
			string application = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.UpgradeApplication.cs");
			AssertOrdered(application, "TryUpgradeBase(Owner, Z, Successor",
				"TryBeginUpgradeReceipt(Owner, Target, Successor",
				"TryRemoveUpgradeSlot(Owner", "TryCarryUpgradeSlot(Owner, Target",
				"TryStageLayer(Target, Z, ArchitectureLayer.Ground");
			string receipts = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.UpgradeReceipts.cs");
			AssertOrdered(receipts,
				"KingdomArchitectureReceiptPrefixRules.LegalRetainedTarget(state",
				"TrySetUpgradeInt(Target, OutputState(AfterPlacement), 1",
				"TrySetUpgradeString(Target, OutputId(AfterPlacement), id",
				"TrySetUpgradeInt(Owner, stateProperty, 1",
				"TryRetagUpgradeComponent(Owner, exact, Z, Before, After, Lot",
				"TrySetUpgradeInt(Target, OutputState(AfterPlacement), 2",
				"TrySetUpgradeInt(Owner, stateProperty, 2");
			string retag = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.UpgradeRetag.cs");
			AssertOrdered(retag, "Item.RemoveIntProperty(ComponentSchemaProperty)",
				"Item.SetIntProperty(ComponentCarriedProperty, 1)",
				"Item.SetIntProperty(r_KingdomScaffold.PendingImprovementSuccessorProperty, 1)",
				"Item.SetIntProperty(ComponentSchemaProperty, ComponentSchema)");
		}

		[Test]
		public void UpgradeComponentsRemainInertUntilSchemaLastRemovalRetirement()
		{
			string pending = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.PendingComponents.cs");
			StringAssert.Contains("A bare carried flag is never activation authority", pending);
			AssertOrdered(pending, "HasCommittedImprovementRemoval(Owner)",
				"CommitPendingRetirement(Owner, 1", "item.RemoveIntProperty(",
				"CommitPendingRetirement(Owner, 2", "RetirePendingRoot(Owner",
				"TryVerifyComplete(Owner, Z");
			StringAssert.Contains("phase == 0", pending);
			StringAssert.Contains("phase == 2", pending);
			StringAssert.Contains("IsExactPendingImprovementSuccessor(item)", pending);
			StringAssert.DoesNotContain("if (carried)", pending);

			string survey = TestMain.ReadRepositoryText(
				"Growth/KingdomSurvey.01b.PendingUpgradeComponents.cs");
			StringAssert.Contains("HasPendingImprovementSuccessorEvidence(Item)", survey);
			StringAssert.Contains("pendingTarget", survey);
			StringAssert.Contains("predecessorReceipt", survey);
		}

		[Test]
		public void PaidEnvelopeRetriesAlwaysReproveCurrentGroundAndExactRetagPrefixes()
		{
			string application = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.UpgradeApplication.cs");
			string apply = Between(application, "public static bool TryApplyUpgrade(", "\n\t}\n}");
			AssertOrdered(apply, "TryReadUpgradeReceipt(Owner, Target, Successor, lot, delta",
				"TryProveEnvelopeGrowth(system, Z, Owner, Target, Successor, true",
				"TryBeginUpgradeReceipt(Owner, Target, Successor");
			StringAssert.DoesNotContain("standingPhase", apply);
			StringAssert.DoesNotContain("TryAcceptFrozenEnvelope", apply);

			string settled = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.EnvelopeSettledOutputs.cs");
			AssertOrdered(settled, "LegalRetainedTarget(retain, target)",
				"KingdomConstruction.FindExactId(Z, id", "target == ArchitectureOutputPrefix.Published",
				"retain == 1", "TryExactRetagPrefix(exact, Z, BeforeIntent, Successor",
				"Settled.Add(exact)");
			StringAssert.Contains("UpgradeQuarantine(Owner", settled);
			StringAssert.Contains("absent, duplicated", settled);

			string receipts = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.UpgradeReceipts.cs");
			string read = Between(receipts, "private static bool TryReadUpgradeReceipt(",
				"private static bool ExactUpgradeState(");
			StringAssert.Contains("ExactUpgradeState(Owner, UpgradeRemove", read);
			StringAssert.Contains("ExactUpgradeState(Owner, UpgradeRetain", read);
			string remove = Between(receipts, "private static bool TryRemoveUpgradeSlot(",
				"private static bool TryCarryUpgradeSlot(");
			StringAssert.Contains("!Owner.HasIntProperty(stateProperty)", remove);
			string carry = Between(receipts, "private static bool TryCarryUpgradeSlot(",
				"\n\t\t}\n\n\t}");
			StringAssert.Contains("!Owner.HasIntProperty(stateProperty)", carry);
		}

		[Test]
		public void UpgradeRemovalSettlesOnlyAfterGlobalLiveIdAbsence()
		{
			string receipts = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.UpgradeReceipts.cs");
			string remove = Between(receipts, "private static bool TryRemoveUpgradeSlot(",
				"private static bool TryCarryUpgradeSlot(");
			Assert.AreEqual(4, remove.Split(new[] { "FindGlobalLiveId" },
				StringSplitOptions.None).Length - 1);
			Assert.AreEqual(2, remove.Split(new[] { "GlobalRemovalAftermath" },
				StringSplitOptions.None).Length - 1);
			StringAssert.DoesNotContain("FindExactId", remove);
			AssertOrdered(remove, "FindGlobalLiveId(id, out exact)",
				"TryRemovableComponent(exact", "Owner.SetIntProperty(stateProperty, 1)",
				"exact.Destroy(");
			string callback = remove.Substring(remove.IndexOf("exact.Destroy(",
				StringComparison.Ordinal));
			AssertOrdered(callback, "ObserveCurrentTopologyInActive", "FindGlobalLiveId",
				"GlobalRemovalAftermath", "KingdomExactRemovalAction.ProvedAbsent");
			StringAssert.Contains("threw after ambiguous physical change", callback);
			StringAssert.Contains("changed ambiguously during callback", callback);
		}

		[Test]
		public void OwnerAndUpgradeWritersRecoverOnlyTypeSafeExactPrefixes()
		{
			string owner = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.OwnerReceiptPrefixes.cs");
			StringAssert.Contains("ExactOrAbsentString", owner);
			StringAssert.Contains("ExactOrAbsentInt", owner);
			StringAssert.Contains("ArchitectureOutputPrefix.IdOnly", owner);

			string recovery = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.OutputRecovery.cs");
			AssertOrdered(recovery, "prefix == ArchitectureOutputPrefix.IdOnly",
				"TryProveIdFirstOutput(Owner, Z", "Owner.SetIntProperty(OutputState(placement), 1)",
				"TryReadOwner(Owner, out Intent");
			StringAssert.Contains("prefix == ArchitectureOutputPrefix.StateOnly", recovery);
			StringAssert.Contains("Quarantine(Owner", recovery);

			string upgrade = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.UpgradeReceiptPrefixes.cs");
			AssertOrdered(upgrade, "TryAcceptUpgradeHeaderPrefix(",
				"Owner.HasStringProperty(UpgradeSchemaProperty)",
				"UpgradeStringPrefix(Owner, UpgradeTargetProperty",
				"UpgradeIntPrefix(Owner, UpgradePhaseProperty, 0)");
			StringAssert.Contains("Owner.HasIntProperty(UpgradeFaultProperty)", upgrade);
			string quarantine = Between(upgrade,
				"internal static bool IsUpgradeQuarantined(",
				"internal static bool TryQuarantineUpgrade(");
			StringAssert.Contains("ClassifyUpgradeFault(", quarantine);
			StringAssert.Contains("ArchitectureUpgradeFaultEvidence.Collision", quarantine);
			StringAssert.Contains("ArchitectureUpgradeFaultEvidence.Integer", quarantine);
			StringAssert.Contains("empty or malformed string evidence", quarantine);
			StringAssert.Contains("return true", quarantine);
		}

		[Test]
		public void StrikeAndRestakeProveExactOwnershipAndProtectedStateBeforeMutation()
		{
			string stamper = Stamper();
			string strike = Between(stamper, "public static bool TryPreflightStrike(",
				"public static bool TryPreflightRestake(");
			AssertOrdered(strike, "TryReadOwner(Owner", "TryExactOutput(Owner, Z",
				"TryStrikeRemovable(exact", "removableIds.Add(exact.ID)",
				"KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z)",
				"foreach (GameObject item in survey.PlotParts)", "removableIds.Remove(item.ID)");
			StringAssert.DoesNotContain("Z.GetObjects()", strike);
			StringAssert.Contains("Owner.Inventory.Objects.Count != 0", strike);
			StringAssert.Contains("ownerLiquid.Volume > 0", strike);
			StringAssert.Contains("HeartRelicProperty", strike);
			Assert.IsFalse(strike.Contains("Obliterate"));
			Assert.IsFalse(strike.Contains("Destroy("));

			string restake = Between(stamper, "public static bool TryPreflightRestake(",
				"public static bool TryValidateFrozenUpgrade(");
			AssertOrdered(restake, "TryPreflightStrike(Owner, Z",
				"Owner.GetIntProperty(KingdomPlots.HeartPlotProperty)",
				"TryPlacementClaim(snapshot.Placements[i]", "TryBlueprintPassAudit(snapshot",
				"TryManagedCells(Intent, Z", "KingdomConstruction.HasActiveAt(System, Z, cell)");
			StringAssert.Contains("oldOwned.Contains(item)", restake);
			StringAssert.DoesNotContain("socket restake would move the behavior root", restake);
			Assert.IsFalse(restake.Contains("GameObject.Create"));
			Assert.IsFalse(restake.Contains("Reserve"));

			string materials = Materials();
			int read = materials.IndexOf("KingdomArchitectureRuntime.TryRead(Building",
				StringComparison.Ordinal);
			int preflight = materials.IndexOf(
				"KingdomArchitectureStamper.TryPreflightStrike(Building, Z", read,
				StringComparison.Ordinal);
			int intent = materials.IndexOf("KingdomStrikeIntent intent =", preflight,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(read, 0);
			Assert.Greater(preflight, read);
			Assert.Greater(intent, preflight);

			string socket = Socket();
			string preparation = Between(socket, "private static bool TryPrepareConvert(",
				"public static bool ExecuteConvert(");
			AssertOrdered(preparation, "KingdomArchitectureRuntime.TryPrepare(System, Z, context.TargetRect",
				"KingdomArchitectureStamper.TryPreflightRestake(System, Z, Building",
				"KingdomPlots.TryEncodePlotPayload(context.TargetRect");
			Assert.IsFalse(preparation.Contains("ReserveExactWater"));
			string conversion = Between(socket, "private static bool ExecutePreparedConvert(",
				"private static bool ProjectConvertOrder(");
			AssertOrdered(conversion,
				"KingdomArchitectureStamper.TryPreflightRestake(System, Z, Building",
				"survey.ReserveExactWater");
			Assert.IsFalse(conversion.Contains("KingdomArchitectureRuntime.TryPrepare"));
			StringAssert.Contains("TrySweepLegacyPlotParts", socket);
			Assert.IsFalse(socket.Contains("private static void SweepPlotParts"));
		}

		private static string Between(string Source, string Start, string End)
		{
			int start = Source.IndexOf(Start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, "missing source boundary: " + Start);
			int end = Source.IndexOf(End, start + Start.Length, StringComparison.Ordinal);
			Assert.Greater(end, start, "missing source boundary: " + End);
			return Source.Substring(start, end - start);
		}

		private static void AssertOrdered(string Source, params string[] Terms)
		{
			int previous = -1;
			for (int i = 0; i < Terms.Length; i++)
			{
				int found = Source.IndexOf(Terms[i], previous + 1, StringComparison.Ordinal);
				Assert.Greater(found, previous, "missing or out-of-order source term: " + Terms[i]);
				previous = found;
			}
		}
	}
}
#endif
