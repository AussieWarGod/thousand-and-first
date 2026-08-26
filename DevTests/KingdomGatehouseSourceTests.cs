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
				"for (int y = gatePlan.Y1; y <= gatePlan.Y2; y++)",
				"KingdomConstruction.HasActiveAt(System, zone, zone.GetCell(x, y))",
				"KingdomSurvey.Take(zone, System)",
				"ReserveExactWater(entry.CostDrams)",
				"KingdomMaterials.ReservePayment(zone, entry.Key)");
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
				"private static void ClearRootReceipt(");
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
		public void ProjectionPublishesSixExactOwnedOutputsAndCommitsSchemaLast()
		{
			string source = KingdomGatehouseLogicalSource.Read();
			string materialize = Slice(source,
				"internal static void MaterializeFromEnteredCell(",
				"private static bool TryExactSatellites(");
			Ordered(materialize,
				"KingdomConstruction.TryFind(receiptId",
				"ScaffoldMatches(scaffold, plan)",
				"TryAudit(Cell.ParentZone, plan, Root, scaffold",
				"GameObject.Create(spec.Blueprint)",
				"item.SetStringProperty(OwnerProperty, Root.ID)",
				"Cell.ParentZone.GetCell(spec.X, spec.Y).AddObject(item)",
				"Root.SetStringProperty(SatelliteIdProperty(i), created[i].ID)",
				"Root.SetIntProperty(SchemaProperty, Schema)",
				"TryExactSatellites(Root, Cell.ParentZone");
			StringAssert.Contains("Reload: never recreate outputs", materialize);
			StringAssert.Contains("created[i].Obliterate(null, Silent: true)", materialize);
			Assert.IsFalse(materialize.Contains("KingdomPlotPartProperty, 1"));
			StringAssert.Contains("KingdomPlots.StampRect(item", materialize);
			Assert.IsFalse(materialize.Contains("KingdomPlots.StampRect(Root"),
				"the non-stakeable Door root must not masquerade as a plot");
		}

		[Test]
		public void ProjectionPartKeepsSerializedAbiAndBaseDispatchOrder()
		{
			string source = KingdomGatehouseLogicalSource.Read();
			Ordered(source,
				"[Serializable]",
				"public sealed class r_KingdomGatehouse : IPart",
				"return base.WantEvent(ID, cascade) || ID == EnteredCellEvent.ID;",
				"KingdomGatehouse.MaterializeFromEnteredCell(ParentObject, E.Cell);",
				"return base.HandleEvent(E);");
			Assert.AreEqual(1, source.Split(new[]
			{
				"public sealed class r_KingdomGatehouse : IPart"
			}, StringSplitOptions.None).Length - 1);
		}

		[Test]
		public void StrikeFreezesTypedNonPlotSatellitesAndLeavesNoSuccessor()
		{
			string materials = Source(Path.Combine("Growth", "KingdomMaterials.cs"));
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

			string socket = Source(Path.Combine("Growth", "KingdomSocket.cs"));
			string successor = Slice(socket, "internal static bool ResumeStrikeSuccessor(",
				"private static bool HasStrikePlotParts(");
			Ordered(successor, "if (!Intent.HasPlot)", "return string.IsNullOrEmpty(Job.OutputId);");
		}

		[Test]
		public void OnlyTheTypedGatehouseMayCarryNonPlotTargetBoundsOnV2Wire()
		{
			string source = Source(Path.Combine("Growth", "KingdomConstructionRules.PayloadCodec.cs"));
			string encode = Slice(source, "public static bool TryEncodeStrikeIntent(",
				"public static bool TryDecodeStrikeIntent(");
			StringAssert.Contains("KingdomGatehouseRules.IsNetworkStrike(Intent.BuildKey", encode);
			StringAssert.Contains("else if (!networkStrike && (Intent.X1 != -1", encode);
			StringAssert.Contains("Intent.Targets.Count != 0)) return false;", encode);
		}

		[Test]
		public void MaterialDoctrineIsFourStoneWallsTwoTimberWatchBenchesAndOpenRoad()
		{
			string rules = Source(Path.Combine("Growth", "KingdomGatehouseRules.cs"));
			StringAssert.Contains("public const string StoneBlueprint = \"r_KingdomStructureSandstone\"",
				rules);
			StringAssert.Contains("public const string WatchBlueprint = \"r_KingdomFixtureBenchTimber\"",
				rules);
			StringAssert.Contains("public const int SatelliteCount = 6", rules);
			StringAssert.Contains("public const int PassageCount = 3", rules);
			StringAssert.Contains("int depth = (Index < 2) ? 0 : ((Index < 4) ? 1 : 2)", rules);
			StringAssert.Contains("string material = Index < 4 ? StoneBlueprint : WatchBlueprint", rules);
			Assert.IsFalse(rules.Contains("r_KingdomFirstBasin"));
		}
	}
}
#endif
