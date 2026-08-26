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
				"public const int UpgradeSchema = 1;",
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
				"KingdomArchitectureRules.IsCurrentSnapshotEncoding(Intent.EncodedSnapshot)",
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
				"KingdomArchitectureRules.IsCurrentSnapshotEncoding(Intent.EncodedSnapshot)",
				"Owner.RemoveIntProperty(SchemaProperty)",
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
			StringAssert.Contains("receipt is absent, partial, or unknown", read);
			StringAssert.Contains("layout owner is quarantined", read);
			StringAssert.Contains("KingdomArchitectureRuntime.TryRead(Owner", read);
			StringAssert.Contains("KingdomArchitectureRuntime.TryDecode(Intent", read);
			StringAssert.Contains("KingdomArchitectureRules.IsCurrentSnapshotEncoding", read);
			StringAssert.Contains("hash != Intent.SnapshotHash", read);
			StringAssert.Contains("placement.Layer < next && state != 2", read);

			string copy = Between(source, "public static bool TryCopyFrozenOwner(",
				"public static bool TryManagedCells(");
			AssertOrdered(copy, "Source.GetIntProperty(NextLayerProperty) != 3",
				"KingdomArchitectureRuntime.TryCopyFrozen(Source, Target",
				"Target.RemoveIntProperty(SchemaProperty)",
				"Target.SetStringProperty(OutputId(placement)",
				"Target.SetIntProperty(OutputState(placement), 2)",
				"Target.SetIntProperty(SchemaProperty, LayoutSchema)",
				"TryReadOwner(Target");

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
				"StampComponent(placed, Lot, Intent.SnapshotHash, Placement)",
				"Owner.SetStringProperty(idProperty, placed.ID)",
				"Owner.SetIntProperty(stateProperty, 1)", "cell.AddObject(placed",
				"ExactComponent(placed, Z, Intent, Lot, Placement",
				"Owner.SetIntProperty(stateProperty, 2)");

			string exact = Between(source, "private static bool ExactComponent(",
				"private static bool CanInsert(");
			StringAssert.Contains("KingdomPlots.PlotIdProperty", exact);
			StringAssert.Contains("ComponentSlotProperty", exact);
			StringAssert.Contains("ComponentLayerProperty", exact);
			StringAssert.Contains("ComponentAnchorProperty", exact);
			StringAssert.Contains("ComponentHashProperty", exact);
			StringAssert.Contains("ComponentTokenProperty", exact);
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
				"KingdomConstruction.UpdateOutput(ref Job, works.ID)", "cell.AddObject(works)");

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
				"KingdomArchitectureRuntime.TryPrepareSuccessor(System, Z, before",
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
