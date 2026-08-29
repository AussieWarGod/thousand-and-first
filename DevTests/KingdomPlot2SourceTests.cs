#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPlot2SourceTests
	{
		private static string Plot()
		{
			return KingdomPlot2LogicalSource.Read();
		}

		[Test]
		public void TopLevelAndNestedSerializedIdentitiesRemainExact()
		{
			string source = Plot();
			Assert.AreEqual(53, Count(source, "public static partial class KingdomPlots"));
			StringAssert.DoesNotContain("public static class KingdomPlots", source);
			string yielding = Between(source, "[Serializable]\n\tpublic class r_KingdomYielding : IPart",
				"[Serializable]\n\tpublic class r_KingdomPlotWorks : IPart");
			AssertOrdered(yielding, "public override bool WantEvent(",
				"public override bool HandleEvent(GetShortDescriptionEvent E)");
			string works = Between(source, "[Serializable]\n\tpublic class r_KingdomPlotWorks : IPart",
				"public sealed class KingdomPlotQuote");
			AssertOrdered(works, "public string DesignKey;", "public string DisplayName;",
				"public int X1;", "public int Y1;", "public int X2;", "public int Y2;",
				"public long StartTick;", "public long TotalTicks;", "public int StageApplied;",
				"public bool Open;", "public bool Carved;", "public string WallBlueprint;",
				"public string ContentsTable;", "public int StaffNeeded;",
				"public bool ThresholdManning;", "public int DefencePending;",
				"public bool HasDoor;", "public int DoorX;", "public int DoorY;",
				"public KingdomPlotRules.PlotRect Rect()");
			string quote = Between(source, "public sealed class KingdomPlotQuote",
				"public static partial class KingdomPlots");
			AssertOrdered(quote, "public KingdomPlotRules.PlotRect Rect;",
				"public KingdomPlotRules.PlotSize StakedSize;",
				"public KingdomLayoutRules.LayoutOutcome Outcome;",
				"public KingdomArchitectureIntent Architecture;", "public string Payload;",
				"public long LabourTicks;", "public int WaterDrams;",
				"public KingdomMaterialDebitCost MaterialClaim;", "public int MainX;",
				"public int MainY;", "public string PurposeReceipt;");
			AssertOrdered(source, "public sealed class GroundGrid", "private sealed class GrowthRow",
				"private sealed class GrowthPlan", "private sealed class FurnishRow");
			string ground = Between(source, "public sealed class GroundGrid",
				"private sealed class GrowthRow");
			AssertOrdered(ground, "public int Width;", "public int Height;",
				"private readonly KingdomPlotRules.GroundKind[] Kinds;",
				"private readonly string[] Blockers;", "private readonly int[] Refusals;");
			string growthRow = Between(source, "private sealed class GrowthRow",
				"private sealed class GrowthPlan");
			AssertOrdered(growthRow, "public int Kind;", "public int X;", "public int Y;",
				"public string Blueprint;", "public string Id;", "public int State;");
			string growthPlan = Between(source, "private sealed class GrowthPlan",
				"private sealed class FurnishRow");
			AssertOrdered(growthPlan, "public string PredecessorId;", "public string SuccessorId;",
				"public string SuccessorKey;", "public string PlotId;",
				"public KingdomPlotRules.PlotRect Old;", "public KingdomPlotRules.PlotRect Grown;",
				"public KingdomPlotRules.RoofState Roof;", "public int HeartX;",
				"public int HeartY;", "public bool KeepInner;", "public string Wall;",
				"public bool Done;", "public List<GrowthRow> Rows;");
			string furnishRow = Between(source, "private sealed class FurnishRow",
				"private static bool SameRect(");
			AssertOrdered(furnishRow, "public string Blueprint;", "public int X;",
				"public int Y;", "public string Id;", "public bool Settled;");
		}

		[Test]
		public void DurablePropertyAndSchemaConstantsRemainExactAndOrdered()
		{
			AssertOrdered(Plot(),
				"public const string FrontierWorkProperty = \"KingdomFrontierWork\";",
				"public const string AdoptedPlotProperty = \"KingdomAdoptedPlot\";",
				"public const string PlotPartProperty = \"KingdomPlotPart\";",
				"public const string PlotIdProperty = \"KingdomPlotId\";",
				"public const string PlotX1Property = \"KingdomPlotX1\";",
				"public const string PlotY1Property = \"KingdomPlotY1\";",
				"public const string PlotX2Property = \"KingdomPlotX2\";",
				"public const string PlotY2Property = \"KingdomPlotY2\";",
				"public const string FootX1Property = \"KingdomFootX1\";",
				"public const string FootY1Property = \"KingdomFootY1\";",
				"public const string FootX2Property = \"KingdomFootX2\";",
				"public const string FootY2Property = \"KingdomFootY2\";",
				"public const string PlotRoofProperty = \"KingdomPlotRoof\";",
				"public const string BlockAnnouncedProperty = \"KingdomPlotBlockSaid\";",
				"public const string PlanSchemaProperty = \"r_TAF_PlanPlotSchema\";",
				"public const string PlanPayloadProperty = \"r_TAF_PlanPlotPayload\";",
				"public const string PlanLabourProperty = \"r_TAF_PlanPlotLabour\";",
				"public const string PlanWaterProperty = \"r_TAF_PlanPlotWater\";",
				"public const string PlanMaterialProperty = \"r_TAF_PlanPlotMaterial\";",
				"public const int PlanSchema = 1;", "public const string RiteXProperty = \"r_TAF_RiteX\";",
				"public const string RiteYProperty = \"r_TAF_RiteY\";",
				"public const string SurveyX1Property = \"r_TAF_HeartSurveyX1\";",
				"public const string SurveyY1Property = \"r_TAF_HeartSurveyY1\";",
				"public const string SurveyX2Property = \"r_TAF_HeartSurveyX2\";",
				"public const string SurveyY2Property = \"r_TAF_HeartSurveyY2\";",
				"public const string HeartRungProperty = \"r_TAF_HeartRung\";",
				"public const string HeartPlotProperty = \"r_TAF_HeartPlot\";",
				"public const string HeartStakeProperty = \"r_TAF_HeartStake\";",
				"public const string HeartRelicProperty = \"r_TAF_HeartRelic\";",
				"public const string YieldingProperty = \"r_TAF_Yielding\";",
				"internal const string FurnishReceiptProperty = \"r_TAF_ConstructionFurnishReceipt\";",
				"private const string GrowthReceiptProperty = \"r_TAF_ImprovementGrowthReceipt\";",
				"public const string PlotWorkSchemaProperty = \"r_TAF_PlotWorkSchema\";",
				"public const string PlotWorkRequiredProperty = \"r_TAF_PlotWorkRequired\";",
				"public const string PlotWorkRemainingProperty = \"r_TAF_PlotWorkRemaining\";",
				"public const string PlotWorkLastTickProperty = \"r_TAF_PlotWorkLastTick\";",
				"public const string PlotWorkCompletedTickProperty = \"r_TAF_PlotWorkCompletedTick\";",
				"public const string PlotWorkShortfallSaidProperty = \"r_TAF_PlotWorkShortfallSaid\";",
				"public const string PlotWorkFaultSaidProperty = \"r_TAF_PlotWorkFaultSaid\";",
				"public const int PlotWorkSchema = 2;", "private const int MaxFurnishItems = 64;",
				"private const int MaxGrowthRows = 512;", "private const int MaxPlotSkinChars = 256;",
				"public const string WorksBlueprint = \"r_KingdomPlotWorks\";",
				"public const string FrameBlueprint = \"r_KingdomPlotFrame\";",
				"public const string FloorBlueprint = \"DirtPath\";",
				"public const string DoorBlueprint = \"Door\";");
		}

		[Test]
		public void PublicAndCrossAuthorityMethodOrderRemainsExact()
		{
			AssertOrdered(Plot(), "public static void ClearSpecs(",
				"public static void RegisterSpec(", "public static void RegisterSpec(",
				"public static bool TryGetSpec(", "public static bool IsPlotDesign(",
				"public static KingdomPlotRules.GroundKind ReadGround(",
				"public static KingdomPlotRules.GroundKind ReadObject(",
				"public static KingdomPlotRules.GroundKind WallGround(",
				"public sealed class GroundGrid", "public GroundGrid(Zone Z)",
				"public GroundGrid(Zone Z, int FutureStakeX, int FutureStakeY)",
				"public KingdomPlotRules.GroundKind KindAt(", "public bool AnyRefusal(",
				"public bool TryFirstRefusal(", "public List<KingdomPlotRules.GroundKind> CellsOf(",
				"public static bool TryReadRect(", "public static List<KingdomPlotRules.PlotRect> ReadPlots(",
				"public static void StampRect(", "public static void StampFootprint(",
				"public static bool TryReadFootprint(", "public static KingdomPlotRules.RoofState RoofOf(",
				"public static void HeartFor(", "public static KingdomPlotRules.PlotRect FootprintFor(",
				"public static KingdomPlotRules.PlotRect HeartFootprintFor(",
				"public static List<KingdomPlotRules.PlotRect> YardRects(",
				"public static bool TryRiteGround(", "public static bool TrySurveyedHeart(",
				"public static int HeartRung(", "public static int RiteWeight(",
				"public static bool SurveyHeart(", "public static GameObject StakeHeartRung(",
				"public static bool TryFindRect(", "public static bool TryFindRect(",
				"public static int NearestIndex(", "public static bool TryQuoteCommission(",
				"public static bool Commission(", "public static bool Commission(",
				"public static bool Commission(", "public static GameObject Stake(",
				"internal static bool ProjectOnRect(", "internal static bool ExpectedArchitectureReceipt(",
				"internal static bool TryPreparePlotPayload(",
				"internal static bool TryPreparePlotPayload(", "internal static bool TryEncodePlotPayload(",
				"internal static bool TryDecodePlotPayload(", "internal static void RetryConstruction(",
				"internal static void InspectConstruction(", "public static int CountBuilt(Zone Z)",
				"public static int CountBuilt(IEnumerable<GameObject> Objects)",
				"public static bool IsFrontierWork(", "public static bool TryQuotePlan(",
				"public static bool TryFreezePlan(", "internal static bool TryReadFrozenPlan(",
				"internal static bool TryPlanPrice(", "public static bool PlanBlocked(",
				"internal static bool TryPreparePlan(", "public static bool StakeFromPlan(",
				"public static bool StakeFromPlan(", "public static bool StampAdopted(",
				"public static void ReleaseAdoptedPlot(", "public static KingdomPlotRules.PlotSize StakedSize(",
				"public static List<KingdomPlotRules.ChainStep> ChainOf(",
				"public static List<KingdomPlotRules.PlotSize> StakeableSizes(",
				"public static string ForesightFor(", "internal static bool TryStampAuthoredGrowth(",
				"public static bool GrowRefused(", "public static bool IsHeartPlot(",
				"public static bool IsYielding(", "public static List<GameObject> FindYielding(",
				"public static bool TryHeartRectFor(", "public static bool GrowInPlace(",
				"public static void Advance(", "public static int MaterialsHeld(");
		}

		[Test]
		public void PaidProjectionClearanceGrowthAndFinishKeepTransactionOrder()
		{
			string source = Plot();
			AssertOrdered(Between(source, "public static bool Commission(KingdomSystem System, Zone Z,\n\t\t\tKingdomRules.BuildEntry Entry",
				"private static KingdomPlotRules.PlotRect PlannedFootprint("),
				"Failure = KingdomCommission.StageRefusal(System, Entry)",
				"TryFindRect(Z, System, Entry", "TryPreparePlotPayload(System, Z, rect",
				"Expected != null", "KingdomPurpose.ResolveCommitCargo",
				"KingdomConstruction.NewJob(System, Z", "KingdomConstruction.FreezeBuildTruth(job",
				"KingdomConstruction.TryFundNew(job", "ProjectPlot(System, Z",
				"KingdomGovernanceScope.Commit(\"commission building\")");
			AssertOrdered(Between(source, "private static bool ResumeClearPayout(",
				"private static bool ExactClearOutput("), "KingdomConstruction.FindExactId(Z",
				"ExactClearSource(Works, Z", "exact.Destroy",
				"KingdomSurvey.ObserveCurrentTopologyInActive(Z, exact)",
				"SettleClearRemovalTopology(Works, Z",
				"PrepareClearOutput(Works, Z", "PlaceOrProveClearOutput(Works, Z",
				"SetClearTally(Works");
			AssertOrdered(Between(source, "public static bool GrowInPlace(",
				"private static bool ApplyGrowthPlan("), "TryBuildGrowthPlan(",
				"SetStringProperty(GrowthReceiptProperty", "RequirePart<r_KingdomYielding>",
				"ApplyGrowthPlan(");
			AssertOrdered(Between(source, "private static bool Finish(",
				"private static bool FinishPlotEffects("), "TryVerifyComplete(parent, Z",
				"TryFinishOutput(Works, Z", "TryFinishRemoval(Z, cell, Footprint",
				"TryFinishEffects(Z, cell",
				"TryCopyFrozenOwner(parent, building", "PrepareFinalBuilding(building",
				"FreezePaidBuild(building, construction", "RootPlotFinalOutput(expectedOutput",
				"UpdateFinalOutput(ref construction", "cell.AddObject(building)",
				"ExactFinalBuilding(building, Z",
				"FurnishDurable(Z", "FinalRemovalPending", "parent.Destroy",
				"RemovalProofProperty", "KingdomDelveLink.TrySettle",
				"KingdomConstruction.Complete(ref construction)", "FinishPlotEffects(system, Z");
		}

		private static string Between(string source, string start, string end)
		{
			int first = source.IndexOf(start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(first, 0, "missing source boundary: " + start);
			int last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
			Assert.Greater(last, first, "missing source boundary: " + end);
			return source.Substring(first, last - first);
		}

		private static void AssertOrdered(string source, params string[] terms)
		{
			int offset = 0;
			for (int i = 0; i < terms.Length; i++)
			{
				int found = source.IndexOf(terms[i], offset, StringComparison.Ordinal);
				Assert.GreaterOrEqual(found, 0, "missing ordered source term: " + terms[i]);
				offset = found + terms[i].Length;
			}
		}

		private static int Count(string source, string term)
		{
			int count = 0;
			for (int offset = 0; (offset = source.IndexOf(term, offset,
				StringComparison.Ordinal)) >= 0; offset += term.Length) count++;
			return count;
		}
	}
}
#endif
