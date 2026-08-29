#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomGatehouseSourceTests
	{
		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static string Slice(string source, string start, string end)
		{
			int at = source.IndexOf(start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(at, 0, start);
			int until = source.IndexOf(end, at + start.Length, StringComparison.Ordinal);
			Assert.Greater(until, at, end);
			return source.Substring(at, until - at);
		}

		private static void Ordered(string source, params string[] terms)
		{
			int cursor = -1;
			for (int i = 0; i < terms.Length; i++)
			{
				int next = source.IndexOf(terms[i], cursor + 1, StringComparison.Ordinal);
				Assert.Greater(next, cursor, terms[i]);
				cursor = next;
			}
		}

		[Test]
		public void CommissionAuditsAndFreezesAllNineCellsBeforeAnyDebit()
		{
			string source = KingdomCommissionLogicalSource.Read();
			string commission = Slice(source,
				"public static bool Commission(KingdomSystem System, string Key, string SkinKey, KingdomPlotRules.PlotSize Stake, out string Failure)",
				"internal static void RetryConstruction(");
			Ordered(commission,
				"KingdomGatehouse.TryPlan(zone, System, out gatePlan, out Failure)",
				"KingdomGatehouseRules.TryEncode(gatePlan, out payload)",
				"KingdomGatehouseRules.TryMaterialCost(gatePlan, out claim)",
				"for (int y = gatePlan.Y1; y <= gatePlan.Y2; y++)",
				"KingdomConstruction.HasActiveAt(System, zone, zone.GetCell(x, y))",
				"KingdomGatehouseRules.MaterialClaimMatches(gatePlan",
				"KingdomSurvey.Take(zone, System)",
				"ReserveExactWater(entry.CostDrams)",
				"KingdomMaterials.ReserveComposite(zone, claim)");
			StringAssert.Contains("System.Style, out Plan", KingdomGatehouseLogicalSource.Read(),
				"style must become paid form truth during preflight");
			StringAssert.Contains("KingdomConstructionRoute.CommissionScaffold, cell, null, entry.Key, payload",
				commission);
			Assert.IsFalse(commission.Contains("PlaceHut"));
			Assert.IsFalse(commission.Contains("ClearRect"));
		}

		[Test]
		public void RuntimeRefusesOccupantsAndObstructionsWithoutClearingOrDisplacement()
		{
			string source = KingdomGatehouseLogicalSource.Read();
			string audit = Slice(source, "public static bool TryAudit(",
				"public static bool TryReadPlan(");
			string cellAudit = Slice(source, "private static bool AuditFootprintCell(",
				"private static bool RecognizedProjectionSatellite(");
			StringAssert.Contains("KingdomPlots.TryReadRect(item", audit);
			StringAssert.Contains("KingdomPlotRules.Overlaps(proposed, laid)", audit);
			StringAssert.Contains("item.IsPlayer() || item.IsCreature", cellAudit);
			StringAssert.Contains("KingdomPlots.ReadObject(item) != KingdomPlotRules.GroundKind.Bare",
				cellAudit);
			StringAssert.Contains("!cell.IsPassable()", audit);
			StringAssert.Contains("cell.HasObjectWithPart(\"LiquidVolume\")", audit);
			Assert.IsFalse((audit + cellAudit).Contains("Destroy("));
			Assert.IsFalse((audit + cellAudit).Contains("Obliterate("));
			Assert.IsFalse((audit + cellAudit).Contains("AddObject("));
			Assert.IsFalse((audit + cellAudit).Contains("Reserve"));
		}

		[Test]
		public void ProjectionPublishesPerSlotCustodyAndCommitsSchemaLast()
		{
			string source = KingdomGatehouseLogicalSource.Read();
			string materialize = Slice(source,
				"internal static void MaterializeFromEnteredCell(",
				"internal static bool TryResumeProjection(");
			Ordered(materialize,
				"TryProjectionContext(Root, Cell",
				"Root.SetStringProperty(PlanProperty, encoded)",
				"TryDriveProjectionSlot(Root, Cell, job, scaffold",
				"AllProjectionSlotsSettled(Root, part)",
				"TryExactSatellites(Root, Cell.ParentZone, finalPlan",
				"Root.SetIntProperty(SchemaProperty, Schema)",
				"TryExactSatellites(Root, Cell.ParentZone");

			string drive = Source(Path.Combine("Growth",
				"KingdomGatehouse.ProjectionEvidence.cs"));
			string driver = Slice(drive, "private static bool TryDriveProjectionSlot(",
				"private static bool TryCreateStagedSatellite(");
			Ordered(driver, "Root.SetStringProperty(idKey, expectedId)",
				"Root.SetIntProperty(stateKey, (int)KingdomGatehouseSlotState.Pending)",
				"TryCreateStagedSatellite(Root", "TryPlaceStagedSatellite(Root",
				"SettleProjectionSlot(Root");
			string create = Slice(drive, "private static bool TryCreateStagedSatellite(",
				"private static bool TryPlaceStagedSatellite(");
			Ordered(create, "GameObject.Create(Spec.Blueprint)",
				"TryApplySatellitePalette(item, Plan, Index)", "item.ID = ExpectedId",
				"StampProjectionSatellite(item", "CanSerializeDeterministicCustody(",
				"ExactProjectionMarks(item", "SetProjectionCustody(Part, Index, item)",
				"TryProjectionEvidence(Root, Z, Plan");
			string place = Slice(drive, "private static bool TryPlaceStagedSatellite(",
				"private static bool TryRetireStagedSatellite(");
			Ordered(place, ".AddObject(Item, NoStack: true)",
				"KingdomSurvey.ObserveAddResultInActive",
				"ProjectionCallbackAuthorityStillExact(Root",
				"TryProjectionEvidence(Root, Z, Plan",
				"KingdomGatehouseSlotEvidence.ExactPlacement", "SettleProjectionSlot(");
			StringAssert.Contains("Item.Obliterate(null, Silent: true)", drive);
			StringAssert.Contains("CanClearCustody(", drive);
			StringAssert.Contains("GetInventoryDirectAndEquipment()", source,
				"duplicate identity proof must include equipped custody");
			Assert.IsFalse(source.Contains("ClearRootReceipt"));
			Assert.IsFalse(source.Contains("RemoveStringProperty(SatelliteIdProperty"));
			Assert.IsFalse(source.Contains("RemoveIntProperty(SatelliteStateProperty"));
			Assert.IsFalse(source.Contains("KingdomPlotPartProperty, 1"));
			StringAssert.Contains("KingdomPlots.StampRect(Item", drive);
			Assert.IsFalse(source.Contains("KingdomPlots.StampRect(Root"),
				"the non-stakeable Door root must not masquerade as a plot");
		}

		[Test]
		public void EveryCallbackBoundaryReprovesEnvelopeFootprintAndDerivedIdentities()
		{
			string projection = Source(Path.Combine("Growth",
				"KingdomGatehouse.Projection.cs"));
			string loop = Slice(projection, "for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)",
				"Root.SetIntProperty(SchemaProperty, Schema)");
			Ordered(loop, "bool driven = TryDriveProjectionSlot(",
				"ProjectionAuthorityStillExact(Root", "RetirePrematureSchema(Root)",
				"if (!driven)", "TryDecode(Root.GetStringProperty(PlanProperty)",
				"TryExactSatellites(Root");
			string authority = Slice(projection,
				"private static bool ProjectionAuthorityStillExact(",
				"private static void RetirePrematureSchema(");
			StringAssert.Contains("TryAudit(Cell.ParentZone, Plan, Root, Scaffold", authority);
			StringAssert.Contains("ExactPendingEnvelope(", authority);

			string validation = Source(Path.Combine("Growth",
				"KingdomGatehouse.Validation.cs"));
			string exact = Slice(validation, "private static bool TryExactSatellites(",
				"private static GameObject FindExactScaffold(");
			StringAssert.Contains("ExactStoredSatelliteId(", exact);
			StringAssert.Contains("TryExactSatelliteReceipts(Root, Plan", exact);
			StringAssert.Contains("TryProjectionEvidence(Root, Z, Plan", exact);
			StringAssert.DoesNotContain("GameObject.FindByID", exact);
			StringAssert.DoesNotContain("FindObjectByID", KingdomGatehouseLogicalSource.Read());

			string callbacks = Source(Path.Combine("Growth",
				"KingdomGatehouse.ProjectionEvidence.cs"));
			string create = Slice(callbacks, "private static bool TryCreateStagedSatellite(",
				"private static bool TryPlaceStagedSatellite(");
			Ordered(create, "GameObject.Create(Spec.Blueprint)",
				"ProjectionCallbackAuthorityStillExact(Root", "item.ID = ExpectedId",
				"StampProjectionSatellite(item", "CanSerializeDeterministicCustody(",
				"SetProjectionCustody(Part, Index, item)");
			string place = Slice(callbacks, "private static bool TryPlaceStagedSatellite(",
				"private static bool TryRetireStagedSatellite(");
			Ordered(place, ".AddObject(Item, NoStack: true)",
				"ProjectionCallbackAuthorityStillExact(Root", "TryProjectionEvidence(Root");
			string cleanup = Slice(callbacks, "private static bool TryRetireStagedSatellite(",
				"private static bool SettleProjectionSlot(");
			Ordered(cleanup, "Item.Obliterate(null, Silent: true)",
				"ProjectionCallbackAuthorityStillExact(Root", "TryProjectionEvidence(Root");
		}

		[Test]
		public void ScaffoldRetainsLandedGatehouseAndResumesBeforePredecessorRemoval()
		{
			string proof = Source(Path.Combine("Growth", "KingdomScaffold.SuccessorProof.cs"));
			string cleanup = Slice(proof, "private static void QuarantineOrRetryAfterAdd(",
				"private bool ExactPredecessor(");
			Ordered(cleanup, "KingdomGatehouse.HasProjectionCustody(Successor)",
				"KingdomSurvey.ObserveCurrentTopologyInActive", "return;", "Successor.Obliterate");

			string durable = Source(Path.Combine("Growth", "KingdomScaffold.Durable.cs"));
			string continuation = Slice(durable, "private void ContinueDurable(",
				"private void ReturnToOutstanding(");
			Ordered(continuation, "cell.AddObject(successor)",
				"KingdomGatehouse.TryResumeProjection(successor, cell)",
				"KingdomGatehouse.ProjectionComplete(successor, Z)",
				"ParentObject.Destroy(null, Silent: true)");
		}

		[Test]
		public void LegacyHookStaysZeroFieldAndV2CustodyHasItsOwnFrozenLayout()
		{
			string source = Source(Path.Combine("Growth", "r_KingdomGatehouse.cs"));
			string legacy = Slice(source,
				"public sealed class r_KingdomGatehouse : IPart",
				"public sealed class r_KingdomGatehouseProjectionV2 : IPart");
			Ordered(legacy,
				"return base.WantEvent(ID, cascade) || ID == EnteredCellEvent.ID;",
				"KingdomGatehouse.MaterializeFromEnteredCell(ParentObject, E.Cell);",
				"return base.HandleEvent(E);");
			StringAssert.DoesNotContain("public GameObject", legacy,
				"historical v1 saves deserialize the shipped zero-field positional part");
			StringAssert.DoesNotContain("ProjectionCustody", legacy);

			string v2 = Slice(source,
				"public sealed class r_KingdomGatehouseProjectionV2 : IPart",
				"public sealed class r_KingdomGatehouseProjectionV1Pending : IPart");
			Ordered(v2, "public GameObject SatelliteCustody0;",
				"public GameObject SatelliteCustody1;",
				"public GameObject SatelliteCustody2;",
				"public GameObject SatelliteCustody3;",
				"public GameObject SatelliteCustody4;",
				"public GameObject SatelliteCustody5;",
				"internal GameObject ProjectionCustody(int Index)");
			Assert.AreEqual(6, v2.Split(new[] { "public GameObject SatelliteCustody" },
				StringSplitOptions.None).Length - 1);
			string pendingV1 = source.Substring(source.IndexOf(
				"public sealed class r_KingdomGatehouseProjectionV1Pending : IPart",
				StringComparison.Ordinal));
			Assert.AreEqual(6, pendingV1.Split(new[]
			{
				"public GameObject SatelliteCustody"
			}, StringSplitOptions.None).Length - 1);

			string runtime = Source(Path.Combine("Growth", "KingdomGatehouse.cs"));
			string apply = Slice(runtime, "internal static bool TryApplyRootForm(",
				"private static bool TryAttachV2ProjectionCustody(");
			Ordered(apply, "TryDecode(PlanReceipt", "plan.ReceiptVersion == 1",
				"TryAttachV1PendingProjectionCustody(Root", "plan.ReceiptVersion != 2",
				"TryAttachV2ProjectionCustody(Root", "ExactRootPalette(Root, plan)");
			string attachV2 = Slice(runtime,
				"private static bool TryAttachV2ProjectionCustody(",
				"private static bool TryAttachV1PendingProjectionCustody(");
			string attachV1 = Slice(runtime,
				"private static bool TryAttachV1PendingProjectionCustody(",
				"private static bool ProjectionPartMatches(");
			StringAssert.Contains("Root.AddPart(staged)", attachV2);
			StringAssert.Contains("Root.AddPart(staged)", attachV1);
			Assert.AreEqual(1, source.Split(new[]
			{
				"public sealed class r_KingdomGatehouse : IPart"
			}, StringSplitOptions.None).Length - 1);
			Assert.AreEqual(1, source.Split(new[]
			{
				"public sealed class r_KingdomGatehouseProjectionV2 : IPart"
			}, StringSplitOptions.None).Length - 1);
			Assert.AreEqual(1, source.Split(new[]
			{
				"public sealed class r_KingdomGatehouseProjectionV1Pending : IPart"
			}, StringSplitOptions.None).Length - 1);
		}

		[Test]
		public void StrikeFreezesTypedNonPlotSatellitesAndLeavesNoSuccessor()
		{
			string materials = KingdomMaterialsLogicalSource.Read();
			string order = Slice(materials, "private static bool OrderStrikeDurable(",
				"private static bool ResumeStrikeStamp(");
			Ordered(order,
				"HasPlot = false",
				"Building.HasIntProperty(KingdomGatehouse.SchemaProperty)",
				"KingdomGatehouse.TryFreezeStrikeTargets(Building, Z",
				"intent.PlotId = Building.ID",
				"intent.Targets = gateTargets",
				"TryEncodeStrikeIntent(intent");
			StringAssert.Contains("KingdomGatehouseRules.IsNetworkStrike", materials);
			StringAssert.Contains("KingdomGatehouse.IsOwnedSatellite", materials);
			StringAssert.Contains("KingdomGatehouse.TryResolveStrikeSatellite", materials);
			StringAssert.Contains("KingdomGatehouse.TryStrikeReceipt", materials);
			StringAssert.Contains("KingdomGatehouse.LoadedIdentityAbsent", materials);
			string strikeReceipt = Source(Path.Combine("Growth",
				"KingdomGatehouse.ProjectionEvidenceScan.cs"));
			StringAssert.Contains("TryExactSatelliteReceipts(Root, plan", strikeReceipt);
			StringAssert.Contains("ProjectionStateReceiptExact(Root", strikeReceipt);

			string socket = KingdomSocketLogicalSource.Read();
			string successor = Slice(socket, "internal static bool ResumeStrikeSuccessor(",
				"private static bool HasStrikePlotParts(");
			Ordered(successor, "if (!Intent.HasPlot)", "return string.IsNullOrEmpty(Job.OutputId);");
		}

		[Test]
		public void OnlyTheTypedGatehouseMayCarryNonPlotTargetBoundsOnCurrentStrikeWire()
		{
			string source = Source(Path.Combine("Growth", "KingdomConstructionRules.PayloadCodec.cs"));
			string encode = Slice(source, "public static bool TryEncodeStrikeIntent(",
				"public static bool TryDecodeStrikeIntent(");
			StringAssert.Contains("KingdomGatehouseRules.IsNetworkStrike(Intent.BuildKey", encode);
			StringAssert.Contains("else if (!networkStrike && (Intent.X1 != -1", encode);
			StringAssert.Contains("Intent.Targets.Count != 0)) return false;", encode);
		}

		[Test]
		public void LegacyAndV2MaterialDoctrineKeepSixOutputsAndOpenRoad()
		{
			string rules = Source(Path.Combine("Growth", "KingdomGatehouseRules.cs"));
			StringAssert.Contains("public const string StoneBlueprint = \"r_KingdomStructureSandstone\"",
				rules);
			StringAssert.Contains("public const string WatchBlueprint = \"r_KingdomFixtureBenchTimber\"",
				rules);
			StringAssert.Contains("public const int SatelliteCount = KingdomGatehouseTopology.SatelliteCount",
				rules);
			StringAssert.Contains("public const int SatelliteCount = 6",
				Source(Path.Combine("Growth", "KingdomGatehouseDeclarations.cs")));
			StringAssert.Contains("public const int PassageCount = 3", rules);
			StringAssert.Contains("int depth = (Index < 2) ? 0 : ((Index < 4) ? 1 : 2)", rules);
			StringAssert.Contains("Plan.ReceiptVersion == 2", rules);
			StringAssert.Contains("Plan.WallBlueprint : Plan.WatchBlueprint", rules);
			StringAssert.Contains("Index < 4 ? StoneBlueprint : WatchBlueprint", rules);
			StringAssert.Contains("KnownForm(Plan.FormKey", rules);
			Assert.IsFalse(rules.Contains("r_KingdomFirstBasin"));
		}

		[Test]
		public void FrozenFormDrivesRootSatelliteReloadAndStrikeWithoutCatalogueReread()
		{
			string successor = Source(Path.Combine("Growth",
				"KingdomScaffold.SuccessorProof.cs"));
			string prepare = Slice(successor, "private void PrepareSuccessor(",
				"private static void QuarantineOrRetryAfterAdd(");
			Ordered(prepare, "KingdomDesign.ApplyRenderOverrides(Successor",
				"KingdomGatehouse.TryApplyRootForm(Successor, Job.Payload)");

			string durable = Source(Path.Combine("Growth", "KingdomScaffold.Durable.cs"));
			string continuation = Slice(durable, "private void ContinueDurable(",
				"private void ReturnToOutstanding(");
			Ordered(continuation, "PrepareSuccessor(successor, current)",
				"cell.AddObject(successor)", "KingdomGatehouse.TryResumeProjection(successor, cell)");

			string callbacks = Source(Path.Combine("Growth",
				"KingdomGatehouse.ProjectionEvidence.cs"));
			string create = Slice(callbacks, "private static bool TryCreateStagedSatellite(",
				"private static bool TryPlaceStagedSatellite(");
			Ordered(create, "GameObject.Create(Spec.Blueprint)",
				"ProjectionCallbackAuthorityStillExact(Root",
				"TryApplySatellitePalette(item, Plan, Index)", "item.ID = ExpectedId",
				"StampProjectionSatellite(item", "ExactProjectionMarks(item",
				"SetProjectionCustody(Part, Index, item)");
			StringAssert.Contains("TrySatelliteRender(Plan, Index", callbacks);
			string gateRuntime = Source(Path.Combine("Growth", "KingdomGatehouse.cs"));
			string rootPalette = Slice(gateRuntime,
				"private static bool ExactRootPalette(",
				"internal static bool TryApplyRootForm(");
			StringAssert.Contains("return render != null && door != null", rootPalette);
			StringAssert.Contains("KingdomGatehouseRules.TryRootRender(Plan", rootPalette);
			StringAssert.Contains("ExactLiveDoorRender(door.Open", rootPalette);
			StringAssert.Contains("door.SyncRender, render.RenderString, render.Tile", rootPalette);
			StringAssert.DoesNotContain("render.Tile == closedTile", rootPalette);
			StringAssert.DoesNotContain("render.RenderString == glyph", rootPalette);
			string applyRoot = Slice(gateRuntime,
				"internal static bool TryApplyRootForm(",
				"private static bool TryAttachV2ProjectionCustody(");
			Ordered(applyRoot, "door.ClosedDisplay = glyph",
				"door.OpenDisplay = RootOpenRenderString",
				"door.ClosedTile = closedTile", "door.OpenTile = openTile",
				"door.SyncRender = true",
				"render.RenderString = door.Open ? RootOpenRenderString : glyph",
				"render.Tile = door.Open ? openTile : closedTile",
				"ExactRootPalette(Root, plan)");

			string scan = Source(Path.Combine("Growth",
				"KingdomGatehouse.ProjectionEvidenceScan.cs"));
			StringAssert.Contains("!ExactSatellitePalette(Item, Plan, Index)", scan);
			string recovery = Source(Path.Combine("Growth", "KingdomGatehouse.Projection.cs"));
			StringAssert.Contains("MaterialClaimMatches(Plan", recovery);
			StringAssert.Contains("ExactRootPalette(Root, Plan)", recovery);
			string readPlan = Slice(gateRuntime, "public static bool TryReadPlan(",
				"public static bool TryFreezeStrikeTargets(");
			StringAssert.Contains("Root.GetPart<Door>() == null", readPlan);
			StringAssert.Contains("!ExactRootPalette(Root, Plan)", readPlan);
			StringAssert.DoesNotContain("System.Style", recovery);
			StringAssert.DoesNotContain("KingdomData", recovery);
			string scaffold = Source(Path.Combine("Growth", "KingdomCommission.Projection.cs"));
			StringAssert.Contains("? KingdomGatehouseRules.RootBlueprint : Entry.Blueprint",
				scaffold);
		}

		[Test]
		public void HistoricalV1IdsReloadAndStrikeAsExactStoredTruthWithoutFormRewrite()
		{
			string validation = Source(Path.Combine("Growth", "KingdomGatehouse.Validation.cs"));
			string receipts = Slice(validation, "private static bool TryExactSatelliteReceipts(",
				"private static GameObject FindExactScaffold(");
			StringAssert.Contains("ExactStoredSatelliteId(", receipts);
			StringAssert.Contains("Plan.ReceiptVersion == 2", receipts);
			string exact = Slice(validation, "private static bool TryExactSatellites(",
				"private static bool NoExtraOwnedSatellites(");
			Ordered(exact, "TryExactSatelliteReceipts(Root, Plan",
				"TryProjectionEvidence(Root, Z, Plan", "ExactProjectionMarks(item",
				"NoExtraOwnedSatellites(Z, Root.IDIfAssigned, ids)");

			string strike = Source(Path.Combine("Growth",
				"KingdomGatehouse.ProjectionEvidenceScan.cs"));
			Ordered(Slice(strike, "internal static bool TryStrikeReceipt(",
				"internal static bool TryResolveStrikeSatellite("),
				"TryExactSatelliteReceipts(Root, plan", "string storedId = Root.GetStringProperty",
				"target.Id != storedId", "ExactStoredSatelliteId(");
			StringAssert.Contains("root.GetStringProperty(SatelliteIdProperty(Index)) != Id",
				strike);
			StringAssert.DoesNotContain("TryResolveForm", validation + strike);
			StringAssert.DoesNotContain("CopyForm", validation + strike);

			string runtime = Source(Path.Combine("Growth", "KingdomGatehouse.cs"));
			string read = Slice(runtime, "public static bool TryReadPlan(",
				"public static bool TryFreezeStrikeTargets(");
			// The stored receipt version decides which custody part may be present: a v2 form
			// requires the V2 part and forbids the v1 pending carrier; a v1 form forbids both.
			Ordered(read, "(Plan.ReceiptVersion == 2",
				"? Root.GetPart<r_KingdomGatehouseProjectionV2>() == null",
				"|| Root.GetPart<r_KingdomGatehouseProjectionV1Pending>() != null",
				": Root.GetPart<r_KingdomGatehouseProjectionV2>() != null",
				"|| Root.GetPart<r_KingdomGatehouseProjectionV1Pending>() != null)");
			StringAssert.Contains("Root.GetPart<r_KingdomGatehouseProjectionV2>() != null", read);
			string projection = Source(Path.Combine("Growth",
				"KingdomGatehouse.Projection.cs"));
			string state = Slice(projection,
				"private static bool ProjectionStateReceiptExact(",
				"private static bool TryProjectionContext(");
			Ordered(state, "Plan.ReceiptVersion == 1", "if (Part != null",
				"bool anyState = false", "Old completion has no state fields",
				"Plan.ReceiptVersion != 2",
				"AllProjectionSlotsSettled(Root, Part)");
		}

		[Test]
		public void PaidPendingV1LandsStoredEngineIdsThenRetiresOnlyItsMigrationCarrier()
		{
			string runtime = Source(Path.Combine("Growth", "KingdomGatehouse.cs"));
			string apply = Slice(runtime, "internal static bool TryApplyRootForm(",
				"public static bool TryAudit(");
			Ordered(apply, "plan.ReceiptVersion == 1",
				"TryAttachV1PendingProjectionCustody(Root",
				"new r_KingdomGatehouseProjectionV1Pending()", "Root.AddPart(staged)");

			string projection = Source(Path.Combine("Growth",
				"KingdomGatehouse.Projection.cs"));
			string context = Slice(projection, "private static bool TryProjectionContext(",
				"private static bool ProjectionAuthorityStillExact(");
			Ordered(context, "TryDecode(Job.Payload, out Plan)",
				"TryPendingProjectionPart(Root, Plan, out Part)",
				"MaterialClaimMatches(Plan", "ScaffoldMatches(Scaffold, Plan)");
			StringAssert.DoesNotContain("Plan.ReceiptVersion != 2", context);

			string evidence = Source(Path.Combine("Growth",
				"KingdomGatehouse.ProjectionEvidence.cs"));
			string legacyDriver = Slice(evidence,
				"private static bool TryDriveLegacyProjectionSlot(",
				"private static bool TryAdoptUnpublishedLegacyCustody(");
			Ordered(legacyDriver, "GameObject staged = ProjectionCustody(Part, Index)",
				"TryAdoptUnpublishedLegacyCustody(Root", "continue;");
			StringAssert.Contains("ResolveLegacyPublicationCut(Index, state, true, true",
				legacyDriver);
			string adoption = Slice(evidence,
				"private static bool TryAdoptUnpublishedLegacyCustody(",
				"private static bool LegacyUnpublishedIdentityUnique(");
			Ordered(adoption, "ProjectionCallbackAuthorityStillExact(Root",
				"LegacyUnpublishedIdentityUnique(Z, Part, Index",
				"CompatibleUnpublishedLegacyMarks(Staged, Root",
				"ResolveLegacyPublicationCut(Index",
				"TryApplySatellitePalette(Staged, Plan, Index)",
				"StampProjectionSatellite(Staged", "ExactProjectionMarks(Staged",
				"Root.SetStringProperty(SatelliteIdProperty(Index)",
				"Root.SetIntProperty(SatelliteStateProperty(Index)");
			StringAssert.Contains("CountLoadedIdentity(Z, StagedId, out _) != 0",
				evidence);
			string legacy = Slice(evidence,
				"private static bool TryCreateLegacyStagedSatellite(",
				"private static bool TryCreateStagedSatellite(");
			Ordered(legacy, "GameObject.Create(Spec.Blueprint)",
				"ProjectionCallbackAuthorityStillExact(Root", "string generatedId = item.ID",
				"ExactStoredSatelliteId(false", "SetProjectionCustody(Part, Index, item)",
				"StampProjectionSatellite(item", "Root.SetStringProperty(SatelliteIdProperty(Index)",
				"Root.SetIntProperty(SatelliteStateProperty(Index)",
				"TryProjectionEvidence(Root, Z, Plan, Part, Index, generatedId");
			StringAssert.DoesNotContain("StableSatelliteId", legacy);
			string place = Slice(evidence, "private static bool TryPlaceStagedSatellite(",
				"private static bool TryRetireStagedSatellite(");
			Ordered(place, "KingdomGatehouseSlotEvidence.Staged",
				"Plan.ReceiptVersion == 1", "Exact serialized custody was retained for retry.",
				"TryRetireStagedSatellite(Root");

			string materialize = Slice(projection,
				"internal static void MaterializeFromEnteredCell(",
				"internal static bool TryResumeProjection(");
			Ordered(materialize, "AllProjectionSlotsSettled(Root, part)",
				"TryExactSatellites(Root, Cell.ParentZone, finalPlan",
				"TryRetireV1PendingProjectionCustody(Root, finalPlan, part)",
				"ProjectionAuthorityStillExact(Root", "AllProjectionSlotsSettled(Root, part)",
				"TryExactSatellites(Root, Cell.ParentZone, finalPlan",
				"CanResumeLegacySchemaCut(",
				"Root.SetIntProperty(SchemaProperty, Schema)");
			string settled = Slice(evidence, "private static bool AllProjectionSlotsSettled(",
				"private static bool ContestSlot(");
			StringAssert.DoesNotContain("Part == null", settled,
				"the cut after pending-v1 carrier retirement must finish from six exact states");
			StringAssert.DoesNotContain("TryResolveForm", projection + evidence);
			StringAssert.DoesNotContain("System.Style", projection + evidence);

			string custody = Slice(runtime, "public static bool HasProjectionCustody(",
				"private static bool SettledV1PendingRootEnvelope(");
			Ordered(custody, "if (v2 == null && v1 == null)",
				"ProjectionComplete(Root, Root.CurrentZone)",
				"SettledV1PendingRootEnvelope(Root)");
			string cutEnvelope = Slice(runtime,
				"private static bool SettledV1PendingRootEnvelope(",
				"private static bool TryProjectionStateCounts(");
			StringAssert.Contains("TryExactSatelliteReceipts(Root, plan", cutEnvelope);
			StringAssert.Contains("MustRetainLegacyOwnerAcrossSchemaCut(", cutEnvelope);
			StringAssert.DoesNotContain("TryExactSatellites", cutEnvelope,
				"body faults block resume but cannot make cleanup orphan the landed outputs");
		}

		[Test]
		public void GatehouseRepairUsesExactPaidFormTruthBeforeDebitAndOnlyProvedV1Fallback()
		{
			string source = Source(Path.Combine("Growth",
				"KingdomWear.12.RepairProjection.cs"));
			string truth = Slice(source, "private static bool TryGatehouseRepairTruth(",
				"private static void StartRepair(");
			Ordered(truth,
				"KingdomGatehouse.TryReadPlan(Work",
				"Work.HasStringProperty(KingdomConstruction.PaidBuildSchemaProperty)",
				"schema == KingdomConstruction.PaidBuildSchema",
				"KingdomConstruction.TryReadPaidBuild(Work",
				"PaidBuildMaterialProperty)",
				"paid.Material.ToClaimString()",
				"PaidBuildWorkProperty)",
				"paid.WorkTicks.ToString(",
				"KingdomGatehouseRules.MaterialClaimMatches(plan",
				"Materials = paid.Material.Materials",
				"plan.ReceiptVersion == 2",
				"KingdomGatehouse.ProjectionComplete(Work, Work.CurrentZone)",
				"KingdomMaterials.CostFor(KingdomGatehouseRules.BuildKey)");
			StringAssert.Contains(
				"Work.HasIntProperty(KingdomConstruction.PaidBuildMaterialProperty)", truth);
			StringAssert.Contains(
				"Work.HasStringProperty(KingdomConstruction.PaidBuildWaterProperty)", truth);
			StringAssert.Contains(
				"Work.HasIntProperty(KingdomConstruction.PaidBuildWorkProperty)", truth);

			string start = Slice(source, "private static void StartRepair(",
				"private static bool ProjectRepair(");
			Ordered(start, "TryBuildTallies(Work, WearPart.Wear",
				"ReserveExactWater(0)", "KingdomMaterials.ReserveComposite(zone, claim)");
			string covers = Slice(source, "private static bool Covers(",
				"private static bool TryBuildTallies(");
			Ordered(covers, "TryBuildTallies(Work, Wear", "KingdomMaterials.Stock(Z)");
		}
	}
}
#endif
