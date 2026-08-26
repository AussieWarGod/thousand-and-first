#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomUpgradeSourceTests
	{
		private static string Upgrade()
		{
			return KingdomUpgradeLogicalSource.Read();
		}

		[Test]
		public void PartAndNestedDeclarationAbiRemainExact()
		{
			string source = Upgrade();
			Assert.AreEqual(8, Count(source, "public partial class r_KingdomImprovement"));
			Assert.AreEqual(20, Count(source, "public static partial class KingdomUpgrade"));
			StringAssert.Contains(
				"[Serializable]\n\tpublic partial class r_KingdomImprovement : IPart", source);
			string part = Between(source,
				"public partial class r_KingdomImprovement : IPart",
				"private const string HandoverPrefix");
			AssertOrdered(part, "public string SuccessorKey;", "public string SuccessorBlueprint;",
				"public bool Held;", "public bool Working;", "public GameObject Scaffold;",
				"public long WorkCompleteTick;", "public int AnnouncedReason;");
			AssertOrdered(source, "internal int HandoverPhase", "internal bool HandoverQuarantined",
				"internal string HandoverFailure", "internal string HandoverSourceId",
				"internal string HandoverTargetId", "internal string HandoverConstructionReceipt",
				"internal int HandoverSourceVolumeBefore", "internal int HandoverSourceVolumeAfter",
				"internal int HandoverTargetVolumeBefore", "internal int HandoverTargetVolumeAfter",
				"internal int HandoverTargetCapacity", "internal string HandoverSourceComposition",
				"internal string HandoverTargetCompositionBefore",
				"internal string HandoverTargetCompositionAfter", "internal string HandoverItemId",
				"internal string HandoverItemBlueprint", "internal string HandoverItemDestinationId",
				"internal string HandoverItemEscrowKey", "internal int HandoverItemCount",
				"internal int HandoverItemPhase", "internal int HandoverItemDestinationKind",
				"internal int HandoverMovedItems", "internal int HandoverItemMovedBefore",
				"internal int HandoverItemMovedAfter", "internal bool HandoverInventoryDone",
				"internal bool HandoverEffectsDone");

			string assessment = Between(source, "public struct Assessment",
				"public sealed class PreparedImprovement");
			AssertOrdered(assessment, "public bool Valid;",
				"public KingdomUpgradeRules.UpgradeVerdict Verdict;", "public string Key;",
				"public string SuccessorKey;", "public KingdomRules.BuildEntry Successor;",
				"public int CostDrams;", "public int Reserve;", "public int Shortfall;",
				"public int CrewNeeded;", "public GrowthStage StageNeeded;",
				"public long BuildTicks;", "public int SupportPerDay;", "public int OutputLost;",
				"public int Margin;", "public KingdomUpgradeRules.AbsorptionDemand Demand;",
				"public string Reason;", "public KingdomSocketTransition Transition;");
			string prepared = Between(source, "public sealed class PreparedImprovement",
				"public static Assessment Assess(");
			AssertOrdered(prepared, "internal string WorkId;", "internal string SourceKey;",
				"internal string SuccessorKey;", "internal string Payload;",
				"internal bool Legacy;", "public KingdomArchitectureIntent Architecture;",
				"public ArchitectureLayoutDelta Delta;");
		}

		[Test]
		public void ConstantsRegistryAndOriginalMemberOrderRemainExact()
		{
			string source = Upgrade();
			AssertOrdered(source,
				"private const string HandoverPrefix = \"r_TAF_ImprovementHandover:\";",
				"private const string HandoverEscrowPrefix = \"r_TAF_ImprovementItemEscrow:\";",
				"private const int MaxHandoverText = 4096;",
				"private const int MaxHandoverComponents = 64;",
				"private const int MaxHandoverTopologyObjects = 4096;",
				"public const long AbandonGraceTicks = 2400L;",
				"public const string BuiltProperty = \"KingdomBuilt\";",
				"public const string AdoptedProperty = \"KingdomAdopted\";",
				"public const string BuildKeyProperty = \"KingdomBuildKey\";",
				"public const string NoticedState = \"r_TAF_ImprovementNoticed\";",
				"public const string GroundHeldState = \"r_TAF_ImprovementHeld:\";",
				"private static readonly Dictionary<string, KingdomUpgradeRules.UpgradeChain> _chains",
				"public static Dictionary<string, KingdomUpgradeRules.UpgradeChain> Chains");

			AssertOrdered(source, "public override bool WantEvent(",
				"internal static bool CarryLiquidDurable(", "private static bool ResumeDrainedLiquid(",
				"private static bool CompensateLiquid(", "internal static bool CarryInventoryDurable(",
				"private static bool ResumePendingItem(", "private static bool PlacePendingItem(",
				"private static bool RestoreItem(", "private static bool ExactHandoverObjects(",
				"private static bool BoundedIdentity(", "private static bool RootEscrowItem(",
				"private static bool SettlePendingItem(", "internal static bool FailHandover(",
				"private static string EncodeEmptyLiquid(", "public override bool HandleEvent(",
				"public void PollHandover(", "public KingdomPhysicalLookupState FindSuccessor(",
				"internal static void RetryConstruction(", "internal static void InspectConstruction(",
				"public static void ClearChains(", "public static void RegisterChain(",
				"public static void Reload(", "public static bool TryGetChain(",
				"public static string DesignKeyOf(", "public static Assessment Assess(",
				"public static bool ContentsWouldFit(", "public static void OnZoneActivated(",
				"private static void Resolve(", "public static bool Begin(",
				"public static bool TryPrepareImprovement(", "public static bool BeginPlanChange(",
				"private static bool ProjectImprovement(", "public static bool Force(",
				"public static bool OpenHeldOffer(", "public static void HandOver(",
				"private static bool ExactHandoverEndpointsAfterCallback(",
				"public static int CarryLiquid(", "public static int CarryInventory(",
				"public static void CarryMarks(", "public static void ShowImprovements(",
				"public static string EntryLine(", "private static bool TryCarryHandoverContents(",
				"private static bool TryRemoveHandoverPredecessor(");
		}

		[Test]
		public void HandoverContinuationCallsPreserveTransactionOrder()
		{
			string source = Upgrade();
			string handover = Between(source, "public static void HandOver(",
				"private static bool ExactHandoverEndpointsAfterCallback(");
			AssertOrdered(handover, "HandoverFlagsValid()", "HandoverSourceId = Predecessor.ID",
				"HandoverConstructionReceipt = receipt", "KingdomConstruction.TryFind(receipt",
				"KingdomConstruction.Owns(ownerSystem", "TryReadImprovementArchitecture(",
				"KingdomConstruction.BeginProjection(ref job", "KingdomConstruction.Bind(Successor",
				"ExactHandoverEndpointsAfterCallback(", "TryCarryHandoverContents(",
				"TryRemoveHandoverPredecessor(", "MessageQueue.AddPlayerMessage(",
				"KingdomLog.Log(\"improvement handover:", "KingdomSystem.Guard(");

			string contents = Between(source, "private static bool TryCarryHandoverContents(",
				"private static bool TryRemoveHandoverPredecessor(");
			AssertOrdered(contents, "CarryLiquidDurable(Predecessor, Successor",
				"ExactHandoverEndpointsAfterCallback(", "CarryInventoryDurable(Predecessor",
				"ExactHandoverEndpointsAfterCallback(", "HandoverEffectsDone",
				"KingdomArchitectureStamper.TryApplyUpgrade(", "KingdomPlots.GrowInPlace(",
				"CarryMarks(Predecessor, Successor", "ExactCarriedMarks(",
				"intent.HandoverEffectsDone = true");

			string removal = Between(source, "private static bool TryRemoveHandoverPredecessor(",
				"public partial class r_KingdomImprovement");
			AssertOrdered(removal, "activeSurvey.ObserveChanged(Successor)",
				"Predecessor.GetPart<LiquidVolume>()", "KingdomConstruction.IsCurrent(job)",
				"KingdomPhysicalPhase.FinalRemovalPending", "Predecessor.Destroy(",
				"KingdomSurvey.ObserveRemovedFromActive(", "KingdomPhysicalLookupState.Absent",
				"RemovalProofProperty", "KingdomPhysicalPhase.FinalRemoved",
				"KingdomConstruction.Complete(ref job)", "r_KingdomScaffold.TellCompletion(");
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
