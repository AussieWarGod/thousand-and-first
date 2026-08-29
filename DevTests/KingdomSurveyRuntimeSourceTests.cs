#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomSurveyRuntimeSourceTests
	{
		[Test]
		public void AttendedPassTakesAndBindsExactlyOneSurvey()
		{
			string system = KingdomSystemLogicalSource.Read();
			string pass = Between(system, "private bool AttendSeatedSemantics(Zone Z)",
				"private bool PrepareSemanticPass(Zone Z, long NowTick)");
			Assert.AreEqual(1, Count(pass, "KingdomSurvey.Take(Z, this)"));
			Assert.AreEqual(1, Count(pass, "survey.BindPass()"));
			StringAssert.Contains("KingdomExpeditions.OnSettlementPass(this, Z, survey);", pass);
			StringAssert.DoesNotContain("survey = KingdomSurvey.Take", Between(pass,
				"KingdomExpeditions.OnSettlementPass", "SemanticStepHappenings"));
		}

		[Test]
		public void SurveyBuildsOneBoundedRootSnapshotAndMaintainsItExplicitly()
		{
			string survey = KingdomSurveyLogicalSource.Read();
			string take = Between(survey, "public static KingdomSurvey Take(Zone Z)",
				"public static IEnumerable<GameObject> ObjectsFor(Zone Z)");
			Assert.AreEqual(1, Count(take, "Z.GetObjects()"));
			StringAssert.Contains("KingdomSurvey bound = ActiveFor(Z);", take);
			StringAssert.Contains("survey.AddRoot(item, citizenshipSystem);", take);
			StringAssert.Contains("public bool ObserveAdded(GameObject Item)", survey);
			StringAssert.Contains("public bool ObserveChanged(GameObject Item)", survey);
			StringAssert.Contains("public bool ObserveCurrentTopology(GameObject Item)", survey);
			StringAssert.Contains("ObserveCurrentTopologyInActive(Zone Z, GameObject Item)", survey);
			StringAssert.Contains("ObserveAddResultInActive(Zone Z, GameObject Attempted", survey);
			StringAssert.Contains("public bool ObserveRemoved(GameObject Item)", survey);
			StringAssert.Contains("MaxIndexedObjects", survey);
			StringAssert.Contains("LoadedIndexComplete", survey);
		}

		[Test]
		public void ActiveConsumersUseSharedIndexesInsteadOfSecondZoneWalks()
		{
			string offices = Source("Experience", "KingdomOffices.cs") + "\n"
				+ Source("Experience", "KingdomOfficeRuntime.Context.cs") + "\n"
				+ Source("Experience", "KingdomOfficeRuntime.Reconcile.cs") + "\n"
				+ Source("Experience", "KingdomRemembranceRuntime.Open.cs");
			StringAssert.DoesNotContain("Z.GetObjects()", offices);
			StringAssert.Contains("Survey.CitizenBodies", offices);
			StringAssert.Contains("Survey.Cairns", offices);
			StringAssert.Contains("Survey.FindCitizen", offices);

			string construction = KingdomConstructionLogicalSource.Read();
			string constructionPass = Between(construction,
				"public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)",
				"private static void RetryProjection(");
			StringAssert.DoesNotContain("Z.GetObjects()", constructionPass);
			StringAssert.Contains("Survey.PlotWorks", constructionPass);
			StringAssert.Contains("active.TryLoaded(out Loaded)", construction);

			string upgrade = KingdomUpgradeLogicalSource.Read();
			string resolve = Between(upgrade, "private static void Resolve(KingdomSystem System",
				"public static bool GiveFirstNotice(");
			StringAssert.DoesNotContain("Z.GetObjects()", resolve);
			StringAssert.Contains("Survey.Improvements", resolve);
			StringAssert.Contains("Survey.Built", resolve);

			string residents = KingdomResidentsLogicalSource.Read();
			string roster = Between(residents,
				"internal static KingdomCityState ReadRoster(",
				"private static int ClaimIdFor(");
			StringAssert.DoesNotContain("GetObjects()", roster);
			StringAssert.Contains("HomeWorkIds(Survey)", roster);
			StringAssert.Contains("Witnessed(System, Z, Survey", roster);

			string presence = PresenceSource();
			StringAssert.DoesNotContain("GetObjects()", presence);
			StringAssert.Contains("Survey.ConstructionRoots", presence);

			string lab = KingdomLabLogicalSource.Read();
			string labPass = Between(lab, "internal static void OnSemanticStep(",
				"private static void ManageJob(");
			StringAssert.DoesNotContain("GetObjects()", labPass);
			StringAssert.Contains("Survey.LabJobs", labPass);
		}

		[Test]
		public void SecondaryClassifiersConsumeNamedMaintainedIndexes()
		{
			string survey = KingdomSurveyLogicalSource.Read();
			foreach (string index in new[] { "ConstructionRoots", "PlotRoots", "LayoutRoots",
				"CropRows", "NetworkPieces", "LabJobs", "VisualRoots", "PlotParts",
				"ArchitectureComponents", "GatehouseSatellites", "DelveEndpoints",
				"Furnishings", "HeartRelics", "MaterialStockpiles", "ResidentBodies",
				"Transients" })
				StringAssert.Contains("List<GameObject> " + index, survey);

			string layout = Source("Growth", "KingdomLayout.cs");
			string marks = Between(layout, "public static List<KingdomLayoutRules.LayoutMark> ReadMarks",
				"public static Cell ChooseCell(");
			StringAssert.Contains("survey.LayoutRoots", marks);
			StringAssert.DoesNotContain("Z.GetObjects()", marks);

			string crops = KingdomCropsLogicalSource.Read();
			string rows = Between(crops, "public static List<GameObject> RowsOf",
				"public static void ClearRows");
			StringAssert.Contains("survey.CropRows", rows);
			StringAssert.DoesNotContain("Z.GetObjects()", rows);

			string networks = NetworksSource();
			string compose = Between(networks, "private static KingdomZoneLine[] Compose(Zone Z)",
				"private static int Find(");
			StringAssert.Contains("survey.NetworkPieces", compose);
			StringAssert.DoesNotContain("Z.GetObjects()", compose);
		}

		[Test]
		public void NetworkLogicalSourceKeepsOnePartialAuthorityAndZoneLineIdentity()
		{
			string networks = NetworksSource();
			Assert.AreEqual(4, Count(networks, "public static partial class KingdomNetworks"));
			Assert.AreEqual(1, Count(networks, "internal sealed class KingdomZoneLine"));
			StringAssert.DoesNotContain("public static class KingdomNetworks", networks);
			Assert.Less(networks.IndexOf("internal static KingdomCityState Run", StringComparison.Ordinal),
				networks.IndexOf("public static int Attend", StringComparison.Ordinal));
			Assert.Less(networks.IndexOf("public static int Attend", StringComparison.Ordinal),
				networks.IndexOf("private static bool TryComposeGraphs", StringComparison.Ordinal));
			Assert.Less(networks.IndexOf("private static bool TryComposeGraphs", StringComparison.Ordinal),
				networks.IndexOf("private static int Through", StringComparison.Ordinal));
		}

		[Test]
		public void ConstructionPresenceLogicalSourceKeepsOnePartialAuthority()
		{
			string presence = PresenceSource();
			Assert.AreEqual(2, Count(presence,
				"public static partial class KingdomConstructionPresence"));
			StringAssert.DoesNotContain("public static class KingdomConstructionPresence", presence);
			StringAssert.Contains("public static int Assign", presence);
			StringAssert.Contains("private static void Reset", presence);
			StringAssert.Contains("private static bool NeedsLabour", presence);
			StringAssert.Contains("private static long Started", presence);
			StringAssert.Contains("private static Zone GroundOf", presence);
		}

		[Test]
		public void PassReceiptExposesClassificationReuseAndMutationBudget()
		{
			string survey = KingdomSurveyLogicalSource.Read();
			string take = Between(survey, "public static KingdomSurvey Take(Zone Z)",
				"public static IEnumerable<GameObject> ObjectsFor(Zone Z)");
			StringAssert.Contains("BoundSurvey.ForeignClassifications++;", take);
			string receipt = Between(survey, "private void EmitPassReceipt()",
				"public GameObject FindCitizen(");
			foreach (string field in new[] { "classifications=", "foreign=", "roots=", "indexed=",
				"reuses=", "added=", "changed=", "removed=" })
				StringAssert.Contains(field, receipt);
		}

		[Test]
		public void ActiveCrossZoneTransactionsUseExactAuthorityNotRemoteSurveys()
		{
			string purpose = KingdomPurposeLogicalSource.Read();
			string endpoints = Between(purpose, "private static bool ExactEndpoints(",
				"private static GameObject CreateCargo(");
			StringAssert.Contains("FindExactKnown(", endpoints);
			StringAssert.DoesNotContain("KingdomSurvey.Take", endpoints);
			string delve = KingdomDelveLinkLogicalSource.Read();
			string token = Between(delve, "private static int FindEndpointByToken(",
				"private static int CountEndpointAt(");
			StringAssert.Contains("cell?.GetObjects()", token);
			StringAssert.DoesNotContain("KingdomSurvey.Take", token);
		}

		[Test]
		public void PhysicalMutationSeamsUpdateSameSurveyBeforeLaterDecisions()
		{
			string growth = KingdomGrowthLogicalSource.Read();
			StringAssert.Contains("KingdomSurvey.ObserveAddResultInActive(zone, settler, accepted);",
				growth);
			int bind = growth.IndexOf("KingdomResidents.TryEnsureRow", StringComparison.Ordinal);
			int refresh = growth.IndexOf("survey.ObserveChanged(settler)", bind,
				StringComparison.Ordinal);
			int clockIntent = growth.IndexOf("KingdomGrowthPhase.ClockIntent", refresh,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(bind, 0);
			Assert.Greater(refresh, bind);
			Assert.Greater(clockIntent, refresh);
			StringAssert.Contains("Survey?.ObserveCurrentTopology(leaver);", growth);

			string water = KingdomWaterDebitLogicalSource.Read();
			StringAssert.Contains("SynchronizeCachedRows();", water);
			StringAssert.Contains("ReconcilePhysicalRows();", water);

			string guests = KingdomGuestLifecycleLogicalSource.Read();
			StringAssert.Contains("KingdomSurvey.ObserveCurrentTopologyInActive(Zone, body);", guests);

			string architecture = KingdomArchitectureStamperLogicalSource.Read();
			StringAssert.Contains("KingdomSurvey.ObserveAddResultInActive(Z, placed, accepted);",
				architecture);
			StringAssert.Contains("KingdomSurvey.ObserveRemovedFromActive(Z, exact);", architecture);
			StringAssert.Contains("KingdomSurvey.ObserveChangedInActive(Z, placed);", architecture);

			string upgrade = KingdomUpgradeLogicalSource.Read();
			string handover = Between(upgrade,
				"public static void HandOver(GameObject Predecessor",
				"private static bool ExactHandoverEndpointsAfterCallback(");
			int carryPhase = handover.IndexOf("TryCarryHandoverContents(",
				StringComparison.Ordinal);
			int removalPhase = handover.IndexOf("TryRemoveHandoverPredecessor(",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(carryPhase, 0);
			Assert.Greater(removalPhase, carryPhase);
			string carryBody = Between(upgrade,
				"private static bool TryCarryHandoverContents(",
				"private static bool TryRemoveHandoverPredecessor(");
			string removalBody = Between(upgrade,
				"private static bool TryRemoveHandoverPredecessor(",
				"public partial class r_KingdomImprovement");
			int carry = carryBody.IndexOf("CarryMarks", StringComparison.Ordinal);
			int successorRefresh = removalBody.IndexOf("activeSurvey.ObserveChanged(Successor)",
				StringComparison.Ordinal);
			int predecessorRemoval = removalBody.IndexOf("Predecessor.Destroy", StringComparison.Ordinal);
			Assert.GreaterOrEqual(carry, 0);
			Assert.GreaterOrEqual(successorRefresh, 0);
			Assert.Greater(predecessorRemoval, successorRefresh);

			string gatehouse = KingdomGatehouseLogicalSource.Read();
			StringAssert.Contains("KingdomSurvey.ObserveAddResultInActive(Z, Item, accepted);",
				gatehouse);
			StringAssert.Contains("KingdomSurvey.ObserveCurrentTopologyInActive(Z, Item);",
				gatehouse);
			StringAssert.DoesNotContain("ObserveRemovedFromActive", gatehouse);
		}

		[Test]
		public void AppliedThenThrewPhysicalCallbacksReproveActualTopology()
		{
			foreach (string source in new[]
			{
				KingdomScaffoldLogicalSource.Read(),
				KingdomPlot2LogicalSource.Read(),
				KingdomRoadsLogicalSource.Read(),
				KingdomMaterialsLogicalSource.Read(),
				KingdomCarryRuntimeLogicalSource.Read(),
				KingdomExpeditionsLogicalSource.Read(),
				Source("Simulation/City", "KingdomBehaviourRuntime.cs")
			})
				StringAssert.Contains("ObserveCurrentTopologyInActive", source);

			string trade = KingdomTradeLogicalSource.Read();
			StringAssert.Contains("BoundTradeSurvey(Z)?.ObserveCurrentTopology(witness.Owner);", trade);
			StringAssert.Contains("BoundTradeSurvey(Z)?.ObserveCurrentTopology(inventory.Owner);", trade);
			StringAssert.Contains("KingdomSurvey.ObserveAddResultInActive(Z, caravan, added);", trade);
			StringAssert.Contains("BoundTradeSurvey(Z)?.ObserveCurrentTopology(old);", trade);

			string logistics = KingdomCentralLogisticsLogicalSource.Read();
			StringAssert.Contains("PublishMarkedFoodDelta(survey, target, jobId, before);", logistics);
			StringAssert.Contains("survey.SynchronizeReceiptObject(target);", logistics);
		}

		[Test]
		public void TradeTopologyUsesOnlyBoundActiveGroundAndMaintainsItsIndex()
		{
			string trade = KingdomTradeLogicalSource.Read();
			string capture = Between(trade,
				"private static LoadedTopologyWitness CaptureLoadedTopology()",
				"private static bool TryBindTopologyGround(");
			StringAssert.Contains("TryBoundTopologyGround(out manager, out zone, out survey)",
				capture);
			StringAssert.Contains("RootList = survey.Objects", capture);
			StringAssert.DoesNotContain("CachedZones", capture);
			StringAssert.DoesNotContain("KingdomSurvey.ObjectsFor", capture);
			StringAssert.DoesNotContain("GetObjects()", capture);

			string continuation = Between(trade,
				"private static void ContinueOperation(KingdomSystem System",
				"private static bool SettleResources(");
			int bind = continuation.IndexOf("TryBindTopologyGround(System, Z, Survey)",
				StringComparison.Ordinal);
			int frame = continuation.IndexOf("TryBindFrame(System, Book, operation, Z",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(bind, 0);
			Assert.Greater(frame, bind);
			StringAssert.Contains("BoundTradeSurvey(Z)?.ObserveCurrentTopology(inventory.Owner);", trade);
			StringAssert.Contains("KingdomSurvey.ObserveAddResultInActive(Z, caravan, added);", trade);
			StringAssert.Contains("BoundTradeSurvey(Z)?.ObserveCurrentTopology(old);", trade);
		}

		[Test]
		public void TradeProjectionChoosesOneDeterministicBoundedCell()
		{
			string trade = KingdomTradeLogicalSource.Read();
			string settle = Between(trade,
				"private static void SettleProjection(KingdomTradeOperation Operation",
				"private static bool TryChooseProjectionCell(");
			StringAssert.Contains("TryChooseProjectionCell(Z, out cell)", settle);
			StringAssert.DoesNotContain("GetEmptyCells", settle);
			string choose = Between(trade,
				"private static bool TryChooseProjectionCell(Zone Z, out Cell Cell)",
				"private static bool ExactEmptyProjectionCell(");
			StringAssert.Contains("MaxProjectionCellProbes", choose);
			StringAssert.Contains("Z.GetCell(x, y)", choose);
			StringAssert.DoesNotContain("GetEmptyCells", choose);
		}

		private static string Source(string folder, string file)
		{
			return TestMain.ReadRepositoryText(Path.Combine(folder, file));
		}

		private static string NetworksSource()
		{
			return string.Join("\n", new[]
			{
				Source("Simulation/City", "KingdomNetworks.Declarations.cs"),
				Source("Simulation/City", "KingdomNetworks.cs"),
				Source("Simulation/City", "KingdomNetworks.AttendanceAndStar.cs"),
				Source("Simulation/City", "KingdomNetworks.GraphComposition.cs"),
				Source("Simulation/City", "KingdomNetworks.GroundHelpers.cs")
			});
		}

		private static string PresenceSource()
		{
			return string.Join("\n", new[]
			{
				Source("Growth", "KingdomConstructionPresence.cs"),
				Source("Growth", "KingdomConstructionPresence.Helpers.cs")
			});
		}

		private static string Between(string source, string startTerm, string endTerm)
		{
			int start = source.IndexOf(startTerm, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, "missing source boundary: " + startTerm);
			int end = source.IndexOf(endTerm, start + startTerm.Length, StringComparison.Ordinal);
			Assert.Greater(end, start, "missing source boundary: " + endTerm);
			return source.Substring(start, end - start);
		}

		private static int Count(string source, string term)
		{
			int count = 0;
			int at = 0;
			while ((at = source.IndexOf(term, at, StringComparison.Ordinal)) >= 0)
			{
				count++;
				at += term.Length;
			}
			return count;
		}
	}
}
#endif
