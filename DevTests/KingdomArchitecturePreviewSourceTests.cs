#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomArchitecturePreviewSourceTests
	{
		private static string Read(string Directory, string File)
		{
			return TestMain.ReadRepositoryText(Path.Combine(Directory, File));
		}

		[Test]
		public void PreviewIsRenderedOnlyFromTheFrozenProductionSnapshot()
		{
			string source = Read("Growth", "KingdomArchitecturePreview.cs");
			string frontage = Read("Growth", "KingdomArchitecturePreview.Frontage.cs");
			AssertOrdered(source,
				"KingdomArchitectureRuntime.TryDecode(Intent, out snapshot",
				"KingdomArchitectureRules.IsLatestSnapshotEncoding(Intent.EncodedSnapshot)",
				"KingdomArchitectureRules.TryWorldDimensions",
				"snapshot.Cells.Count", "snapshot.Placements.Count", "snapshot.Anchors.Count");
			StringAssert.Contains("KingdomArchitectureRules.TryToWorld", source);
			AssertOrdered(frontage,
				"KingdomArchitecture.TryGetMapping(Snapshot.BuildKey, Snapshot.LotType",
				"mapping.BindingKey != Snapshot.BindingKey",
				"mapping.Frontage == ArchitectureFrontage.Heart ? \"heart-facing\"",
				"mapping.Frontage == ArchitectureFrontage.Road ? \"road-facing\"");
			StringAssert.Contains(".Append(\", \").Append(frontage).Append(\", faces \")", source);
			StringAssert.Contains("@ building; + public door; # blocked; o fixture; ! use point", source);
			StringAssert.Contains("KingdomMaterials.CostFor(Entry.Key)?.Describe()", source);
			StringAssert.Contains("KingdomPlots.ChainOf(Entry)", source);
			StringAssert.Contains("Frozen production gates", source);
			StringAssert.Contains("IRREVERSIBLE CITY PURPOSE", source);
			Assert.IsFalse(source.Contains("GameObject.Create"));
			Assert.IsFalse(source.Contains("SetIntProperty"));
			Assert.IsFalse(source.Contains("SetStringProperty"));
		}

		[Test]
		public void SleepingCapacityUsesTypedAuthoredRoleNotBlueprintNames()
		{
			string source = Read("Growth", "KingdomArchitecturePreview.cs");
			StringAssert.Contains("anchor.Key != \"fixture:sleep\"", source);
			StringAssert.Contains("catalogue ceiling", source);
			StringAssert.DoesNotContain("r_KingdomFixtureBed", source);
		}

		[Test]
		public void NewPlanFreezesWholeLotMapPriceAndLabourBesideItsStake()
		{
			string plot = KingdomPlot2LogicalSource.Read();
			string quote = Between(plot, "public static bool TryQuotePlan(",
				"public static bool TryFreezePlan(");
			AssertOrdered(quote, "new GroundGrid(Z, StakeCell.X, StakeCell.Y)",
				"TryFindRect(Z, System, Entry, spec, staked, grid, StakeCell",
				"rect.Contains(StakeCell.X, StakeCell.Y)",
				"TryPreparePlotPayload(System, Z, rect",
				"KingdomPlotRules.RaiseTicks(", "Quote = new KingdomPlotQuote");
			StringAssert.Contains("MaterialClaim = new KingdomMaterialDebitCost", quote);
			Assert.IsFalse(quote.Contains("ReserveExactWater"));
			Assert.IsFalse(quote.Contains("ReservePayment"));
			Assert.IsFalse(quote.Contains("AddObject"));

			string freeze = Between(plot, "public static bool TryFreezePlan(",
				"internal static bool TryReadFrozenPlan(");
			AssertOrdered(freeze, "Marker.RemoveIntProperty(PlanSchemaProperty)",
				"StampRect(Marker, Quote.Rect)", "PlanPayloadProperty",
				"PlanLabourProperty", "PlanWaterProperty", "PlanMaterialProperty",
				"Marker.SetIntProperty(PlanSchemaProperty, PlanSchema)",
				"TryReadFrozenPlan(Marker, Entry, false");

			string read = Between(plot, "internal static bool TryReadFrozenPlan(",
				"private static bool RectOutsideZone(");
			StringAssert.Contains("TryDecodePlotPayload(Payload", read);
			StringAssert.Contains("KingdomMaterialDebitCost.TryParseClaim", read);
			StringAssert.Contains("Rect.Contains(stake.X, stake.Y)", read);
			StringAssert.Contains("Marker.GetPart<r_KingdomPlanMarker>().DesignKey != Entry.Key", read);
		}

		[Test]
		public void FounderConfirmsPreviewBeforeMarkerCreationOrDebit()
		{
			string charter = KingdomCharterPartLogicalSource.Read();
			string plan = Between(charter, "public void PlaceBuildingPlan(",
				"public void ManagePlans(");
			AssertOrdered(plan, "KingdomPlots.TryQuotePlan(",
				"KingdomArchitecturePreview.TryRender(",
				"Popup.PickOption(Title: \"Reserve exact plan",
				"if (confirmed < 0) return;", "GameObject.Create(\"r_KingdomPlanMarker\")",
				"KingdomPlots.TryFreezePlan(marker, chosen, quote",
				"cell.AddObject(marker)");
			Assert.IsFalse(plan.Contains("ReserveExactWater"));
			Assert.IsFalse(plan.Contains("ReservePayment"));

			string commission = Between(charter, "public void CommissionBuilding(",
				"public void PlaceBuildingPlan(");
			AssertOrdered(commission, "KingdomPlots.TryQuoteCommission(",
				"KingdomArchitecturePreview.TryRender(",
				"Popup.PickOption(Title: \"Production plan:",
				"KingdomCommission.Commission(System, available[num].Key, skin, stake",
				"quote, out var failure");
		}

		[Test]
		public void SocketChangesPreviewTheirPreparedProductionIntent()
		{
			string socket = KingdomSocketLogicalSource.Read();
			string menu = Between(socket, "public static void OpenConvert(",
				"public static void OpenRedress(");
			AssertOrdered(menu, "TryPrepareSocketBuild(System, zone, target",
				"KingdomArchitecturePreview.TryRender(socketBuild.Architecture",
				"socketBuild.LabourTicks",
				"Popup.PickOption(Title: \"Build exact plan:",
				"ExecuteSocketBuild(System, zone, target, socketBuild");
			AssertOrdered(menu, "TryPrepareConvert(System, zone, target",
				"KingdomArchitecturePreview.TryRenderTransition(conversion.Architecture",
				"Popup.PickOption(Title: \"Preview exact change:",
				"ExecutePreparedConvert(System, zone, target, conversion");
			StringAssert.Contains("Exact map delta: retain", Read("Growth", "KingdomArchitecturePreview.cs"));
		}

		[Test]
		public void FrozenPlanIsReprovedBeforeItsExactFrozenDebit()
		{
			string plot = KingdomPlot2LogicalSource.Read();
			string ready = Between(plot, "private static bool TryFrozenPlanReady(",
				"public static bool PlanBlocked(");
			AssertOrdered(ready, "TryReadFrozenPlan(Marker, Entry, true",
				"grid.AnyRefusal(Rect)", "KingdomArchitectureStamper.TryPreflight(",
				"KingdomConstruction.HasActiveAt(System, zone, main)");
			string prepare = Between(plot, "internal static bool TryPreparePlan(",
				"public static bool StakeFromPlan(KingdomSystem System, GameObject Marker");
			AssertOrdered(prepare, "Marker.HasIntProperty(PlanSchemaProperty)",
				"TryFrozenPlanReady(System, Marker, Entry", "MainX = frozen.MainWorldX");

			string marker = KingdomPlanMarkerLogicalSource.Read();
			string pass = Between(marker, "public static void OnSettlementPass(",
				"private static int CountBuilt(");
			AssertOrdered(pass, "KingdomPlots.TryPlanPrice(item, entry",
				"KingdomPlots.PlanBlocked(System, item, entry)",
				"KingdomPlots.PlanBlocked(System, markers[index], entries[index])",
				"KingdomPlots.TryPreparePlan(System, markerObject, entry",
				"Survey.ReserveExactWater(waterPrice)",
				"KingdomMaterials.ReserveComposite(Z, claim)",
				"KingdomConstruction.TryFundNew(job");
		}

		[Test]
		public void LegacyMarkersRetainDynamicCompatibilityOnlyWhenNoFrozenSchemaExists()
		{
			string plot = KingdomPlot2LogicalSource.Read();
			string price = Between(plot, "internal static bool TryPlanPrice(",
				"private static bool TryFrozenPlanReady(");
			StringAssert.Contains("if (!Marker.HasIntProperty(PlanSchemaProperty))", price);
			StringAssert.Contains("KingdomMaterials.CostFor(Entry.Key)", price);
			string prepare = Between(plot, "internal static bool TryPreparePlan(",
				"public static bool StakeFromPlan(KingdomSystem System, GameObject Marker");
			StringAssert.Contains("if (Marker.HasIntProperty(PlanSchemaProperty))", prepare);
			StringAssert.Contains("TryFindRect(zone, System, Entry, spec, grid, cell", prepare);
		}

		private static string Between(string Source, string Start, string End)
		{
			int begin = Source.IndexOf(Start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(begin, 0, "missing start: " + Start);
			int finish = Source.IndexOf(End, begin + Start.Length, StringComparison.Ordinal);
			Assert.Greater(finish, begin, "missing end: " + End);
			return Source.Substring(begin, finish - begin);
		}

		private static void AssertOrdered(string Source, params string[] Terms)
		{
			int at = -1;
			for (int i = 0; i < Terms.Length; i++)
			{
				int next = Source.IndexOf(Terms[i], at + 1, StringComparison.Ordinal);
				Assert.Greater(next, at, "missing or out-of-order source term: " + Terms[i]);
				at = next;
			}
		}
	}
}
#endif
