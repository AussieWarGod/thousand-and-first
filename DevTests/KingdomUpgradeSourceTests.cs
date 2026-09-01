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
			Assert.AreEqual(15, Count(source, "public partial class r_KingdomImprovement"));
			Assert.AreEqual(21, Count(source, "public static partial class KingdomUpgrade"));
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
				"public long BuildTicks;", "public KingdomUpgradeRules.ImprovementDemand Demand;",
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
				"private static bool TryPublishLiquidIntent(",
				"private static bool ReconcileLiquidPhase(",
				"private static bool TryPreviewLiquidAfter(",
				"internal static bool CarryInventoryDurable(",
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
				"private static bool ProjectImprovement(", "public static void HandOver(",
				"private static bool ExactHandoverEndpointsAfterCallback(",
				"public static int CarryLiquid(", "public static int CarryInventory(",
				"public static void CarryMarks(", "public static void ShowImprovements(",
				"public static string EntryLine(", "private static bool TryCarryHandoverContents(",
				"private static bool TryRemoveHandoverPredecessor(");
		}

		[Test]
		public void FirstImprovementNoticeReturnsBeforeStartingOrDebiting()
		{
			string resolve = Between(Upgrade(), "private static void Resolve(",
				"public static bool GiveFirstNotice(");
			AssertOrdered(resolve, "if (readyWork != null && GiveFirstNotice(System)) return;",
				"Begin(System, Z, readyWork, readyAssessment, Survey)");
			StringAssert.DoesNotContain("anyImprovable", resolve);
		}

		[Test]
		public void CraftRefusalCarriesExactZoningDetailIntoFounderProse()
		{
			string source = Upgrade();
			string requirements = Between(source, "public static KingdomUpgradeRules.ImprovementDemand MeasureRequirements(",
				"public static bool CraftReaches(KingdomSystem System, Zone Z, string Key)");
			AssertOrdered(requirements, "out ZoningJudgement judgement",
				"demand.CraftDetail = judgement.Detail",
				"demand.KnowledgeMissing = judgement.Verdict == ZoningVerdict.RefusedUnlearned");
			string assessment = Between(source, "public static Assessment Assess(",
				"public static bool ContentsWouldFit(");
			AssertOrdered(assessment, "assessment.Demand = MeasureRequirements(",
				"assessment.Demand.CraftDetail", "assessment.Demand.KnowledgeMissing");
			string reach = Between(source,
				"public static bool CraftReaches(KingdomSystem System, Zone Z, string Key,",
				"The settlement's improvement pass:");
			StringAssert.Contains(
				"KingdomUpgradeRules.CraftGateAdmits(Judgement.Verdict)", reach);
			StringAssert.DoesNotContain(
				"Judgement.Verdict != ZoningVerdict.RefusedUnlearned", reach);
		}

		[Test]
		public void HandoverContinuationCallsPreserveTransactionOrder()
		{
			string source = Upgrade();
			string exactFailure = Between(source,
				"private static bool TryResolveExactHandoverJob(",
				"private static void FailExactHandover(");
			AssertOrdered(exactFailure, "sourceReceipt != targetReceipt",
				"KingdomConstruction.TryFind(sourceReceipt", "job.SubjectId != Predecessor.IDIfAssigned",
				"job.OutputId != Successor.IDIfAssigned", "KingdomConstruction.Owns(system, zone, job)",
				"KingdomConstruction.IsCurrent(job)", "IsImprovementPredecessorIdentity(",
				"IsExactPendingImprovementSuccessor(Successor)", "IsExactSuccessor(Successor",
				"FindExactId(zone, Predecessor.IDIfAssigned", "ReferenceEquals(exactSuccessor, Successor)");
			string failure = Between(source, "private static void FailExactHandover(",
				"public static void HandOver(");
			AssertOrdered(failure, "TryResolveExactHandoverJob(",
				"r_KingdomImprovement.FailHandover(intent, Failure)",
				"if (exact) KingdomConstruction.Quarantine(ref job, Failure)");
			string handover = Between(source, "public static void HandOver(",
				"private static bool ExactHandoverEndpointsAfterCallback(");
			Assert.GreaterOrEqual(Count(handover, "FailExactHandover("), 6);
			AssertOrdered(handover, "HandoverFlagsValid()", "TryPublishHandoverEndpoints(",
				"KingdomConstruction.TryFind(receipt",
				"KingdomConstruction.Owns(ownerSystem", "TryReadImprovementArchitecture(",
				"KingdomConstruction.BeginProjection(ref job", "KingdomConstruction.Bind(Successor",
				"ExactHandoverEndpointsAfterCallback(", "TryCarryHandoverContents(",
				"TryRemoveHandoverPredecessor(", "MessageQueue.AddPlayerMessage(",
				"KingdomLog.Log(\"improvement handover:", "KingdomSystem.Guard(");

			string contents = Between(source, "private static bool TryCarryHandoverContents(",
				"private static bool TryRemoveHandoverPredecessor(");
			AssertOrdered(contents, "TryPublishInventoryManifest(Predecessor, Successor",
				"CarryLiquidDurable(Predecessor, Successor", "TryPublishLiquidCustody(",
				"ExactHandoverEndpointsAfterCallback(", "CarryInventoryDurable(Predecessor",
				"ExactHandoverEndpointsAfterCallback(", "VerifyHandoverContentCustody(",
				"HandoverEffectsDone",
				"KingdomArchitectureStamper.TryApplyUpgrade(", "KingdomPlots.GrowInPlace(",
				"CarryMarks(Predecessor, Successor", "ExactCarriedMarks(",
				"intent.HandoverEffectsDone = true");

			string removal = Between(source, "private static bool TryRemoveHandoverPredecessor(",
				"public partial class r_KingdomImprovement");
			AssertOrdered(removal, "activeSurvey.ObserveChanged(Successor)",
				"Predecessor.GetPart<LiquidVolume>()", "KingdomConstruction.IsCurrent(job)",
				"TryPublishRemovalIntent(", "SetStringProperty(",
				"RemovalProofProperty", "Predecessor.Destroy(",
				"KingdomSurvey.ObserveCurrentTopologyInActive(",
				"KingdomPhysicalLookupState.Absent", "KingdomPhysicalPhase.FinalRemoved",
				"TryRetirePendingUpgradeComponents(", "active.ObserveChanged(Successor)",
				"KingdomConstruction.Complete(ref Job)", "r_KingdomScaffold.TellCompletion(");
		}

		[Test]
		public void HandoverStateMachinesCoverEveryDurableMutationCut()
		{
			Assert.AreEqual(1, AdvanceLiquid(1, LiquidTopology.Before));
			Assert.AreEqual(2, AdvanceLiquid(1, LiquidTopology.Drained));
			Assert.AreEqual(2, AdvanceLiquid(2, LiquidTopology.Drained));
			Assert.AreEqual(3, AdvanceLiquid(2, LiquidTopology.Settled));
			Assert.AreEqual(3, AdvanceLiquid(3, LiquidTopology.Settled));
			Assert.AreEqual(-1, AdvanceLiquid(1, LiquidTopology.Settled));
			Assert.AreEqual(-1, AdvanceLiquid(2, LiquidTopology.Before));
			Assert.AreEqual(-1, AdvanceLiquid(3, LiquidTopology.Foreign));

			Assert.AreEqual(0, AdvanceCleanup(0, false, true, true));
			Assert.AreEqual(2, AdvanceCleanup(1, true, true, true));
			Assert.AreEqual(2, AdvanceCleanup(1, false, true, true));
			Assert.AreEqual(3, AdvanceCleanup(2, false, true, true));
			Assert.AreEqual(0, AdvanceCleanup(3, false, true, false));
			Assert.AreEqual(-1, AdvanceCleanup(1, true, false, true));
			Assert.AreEqual(-1, AdvanceCleanup(1, true, true, false));
			Assert.AreEqual(-1, AdvanceCleanup(3, true, true, true));

			Assert.AreEqual(RemovalCut.Pending, AdvanceRemoval(RemovalCut.None, true, true));
			Assert.AreEqual(RemovalCut.Pending, AdvanceRemoval(RemovalCut.Pending, true, true));
			Assert.AreEqual(RemovalCut.Removed, AdvanceRemoval(RemovalCut.Pending, false, true));
			Assert.AreEqual(RemovalCut.Complete, AdvanceRemoval(RemovalCut.Removed, false, true));
			Assert.AreEqual(RemovalCut.Invalid, AdvanceRemoval(RemovalCut.Pending, false, false));
			Assert.AreEqual(RemovalCut.Invalid, AdvanceRemoval(RemovalCut.None, false, true));
		}

		[Test]
		public void FullContentManifestSurvivesCallbacksUntilTerminalCleanup()
		{
			string source = Upgrade();
			string publish = Between(source, "internal static bool TryPublishInventoryManifest(",
				"internal static bool VerifyHandoverContentCustody(");
			AssertOrdered(publish, "ManifestCardinalityValid(count)",
				"FindGlobalLiveId(item.IDIfAssigned", "SetObjectGameState(roots[i], items[i])",
				"owner.SetStringProperty(ManifestEntryKey", "owner.SetIntProperty(schemaKey, 1)");
			StringAssert.Contains("Improvement inventory exceeds the 4096-item custody limit", publish);
			string verify = Between(source, "internal static bool VerifyHandoverContentCustody(",
				"internal static bool TryPublishLiquidCustody(");
			StringAssert.Contains("ExpectedSlot(i", verify);
			StringAssert.Contains("FindGlobalLiveId(state.ItemIds[i]", verify);
			StringAssert.Contains("A moved manifest item left its exact destination", verify);
			StringAssert.Contains("VerifyLiquidCustody", verify);

			string liquid = Between(source, "private static bool TryPublishLiquidIntent(",
				"private static bool ReconcileLiquidPhase(");
			AssertOrdered(liquid, "LiquidEndpointSafe(Source.MaxVolume",
				"LiquidEndpointHasContextRisk(Source)", "TryPreviewLiquidAfter(");
			StringAssert.Contains("string.Equals(id, \"neutronflux\"", source);
			StringAssert.Contains("owner.HasRegisteredEvent(\"LiquidMixed\")", source);
			string fit = Between(source, "public static bool ContentsWouldFit(",
				"public static void OnZoneActivated(");
			AssertOrdered(fit, "volume.Volume > 0", "LiquidEndpointSafe(volume.MaxVolume",
				"LiquidEndpointHasContextRisk(volume)", "ManifestCardinalityValid(heldItems)");
			string begin = Between(source, "private static bool BeginCore(",
				"public static bool TryPrepareImprovement(");
			AssertOrdered(begin, "if (!ContentsWouldFit(Work, A.Successor.Blueprint))",
				"Survey.ReserveExactWater(A.CostDrams)");

			string place = Between(source, "private static bool PlacePendingItem(",
				"private static bool RestoreItem(");
			AssertOrdered(place, "ReproveManifestAfterCallback(",
				"if (ExactDestination(Item", "accepted == null", "ReferenceEquals(accepted, Item)");
			string cleanup = Between(source,
				"internal static bool TryRetireHandoverContentCustody(",
				"private static bool RetryOrQuarantineAuthoredLayout(");
			StringAssert.Contains("Job.Phase == KingdomConstructionPhase.Complete", cleanup);
			StringAssert.Contains("VerifySettledHandoverContentCustody(", cleanup);
			AssertOrdered(cleanup, "ObjectGameState.Remove(state.Roots[i])",
				"ManifestKey(\"CleanupCount\")", "ManifestKey(\"CleanupReceipt\")");
			string finish = Between(cleanup, "private static bool FinishManifestCleanup(",
				"private static bool HasRetiredManifestEvidence(");
			AssertOrdered(finish, "CleanupReceipt", "SetStringProperty(ManifestKey(\"RetiredReceipt\")",
				"SetIntProperty(ManifestKey(\"RetiredSchema\")");
			AssertOrdered(cleanup, "if (phase == 4) return FinishManifestCleanup(",
				"if (HasRetiredManifestEvidence(Successor))");
			StringAssert.Contains("ExactRetiredManifestOnly(Successor, Job.Id)", cleanup);
			StringAssert.Contains("if (!ManifestPayloadAbsent(Successor, count))", cleanup);
			StringAssert.Contains("key.StartsWith(HandoverManifestPrefix, StringComparison.Ordinal)", cleanup);
			AssertOrdered(cleanup, "ExactZeroContentLegacyAuthority(Successor, current)",
				"TryPublishZeroContentLegacyRetirement(Successor, current.Id");
			StringAssert.Contains("Job.PhysicalReceipt == LegacyContentRemovalReceipt", cleanup);
			StringAssert.Contains("Job.PhysicalIndex == 0 && Job.PhysicalAmount == 0", cleanup);
			StringAssert.Contains("items != current.PhysicalIndex || liquid != current.PhysicalAmount",
				cleanup);
		}

		[Test]
		public void DurableHeadersCommitLastAndRecoveryNeverCompensatesValue()
		{
			string source = Upgrade();
			string carry = Between(source, "internal static bool CarryLiquidDurable(",
				"private static bool ResumeDrainedLiquid(");
			AssertOrdered(carry, "TryPublishLiquidIntent(", "ReconcileLiquidPhase(",
				"KingdomLiquids.Drain", "ReconcileLiquidPhase(");
			string resume = Between(source, "private static bool ResumeDrainedLiquid(",
				"private static bool TryPublishLiquidIntent(");
			AssertOrdered(resume, "Target.MixWith", "ReconcileLiquidPhase(");
			string publish = Between(source, "private static bool TryPublishLiquidIntent(",
				"private static bool ReconcileLiquidPhase(");
			AssertOrdered(publish, "TargetCompositionExpected", "LiquidIntentDigest",
				"HandoverPhase = 1");
			string reconcile = Between(source, "private static bool ReconcileLiquidPhase(",
				"private static bool TryPreviewLiquidAfter(");
			AssertOrdered(reconcile, "ExactLiquidReceiptTypes(Receipt)",
				"TargetCompositionAfter", "HandoverPhase = 3");
			StringAssert.DoesNotContain("CompensateLiquid", source);
			string liquidTypes = Between(source, "private static bool ExactLiquidReceiptTypes(",
				"private static bool HasLiquidIntentEvidence(");
			StringAssert.Contains("!owner.HasIntProperty(property) || owner.HasStringProperty(property)",
				liquidTypes);
			StringAssert.Contains("!owner.HasStringProperty(property) || owner.HasIntProperty(property)",
				liquidTypes);
			StringAssert.Contains("if (owner.HasIntProperty(after)) return false", liquidTypes);
			StringAssert.Contains("owner.GetStringProperty(after) == Receipt.HandoverText(\"TargetCompositionExpected\")",
				liquidTypes);
			StringAssert.Contains("return !owner.HasStringProperty(after)", liquidTypes);
			StringAssert.Contains("TargetCompositionExpected", liquidTypes);

			string endpoints = Between(source, "private static bool ExactHandoverEndpointReceipt(",
				"private static bool ExactOrAbsentText(");
			StringAssert.Contains("owner.HasIntProperty(HandoverPrefix + \"EndpointSchema\")", endpoints);
			StringAssert.Contains("!owner.HasIntProperty(HandoverPrefix + \"SourceId\")", endpoints);
			StringAssert.Contains("!owner.HasIntProperty(HandoverPrefix + \"TargetId\")", endpoints);
			StringAssert.Contains("!owner.HasIntProperty(HandoverPrefix + \"ConstructionReceipt\")",
				endpoints);

			string inventory = Between(source, "private static bool TryPublishPendingItem(",
				"private static bool ResumePendingItem(");
			AssertOrdered(inventory, "HandoverItemEscrowKey = escrow", "RootEscrowItem(",
				"HandoverItemPhase = 1", "ItemCleanupKey", "ItemCleanupId",
				"ItemCleanupMovedBefore", "ItemCleanupPhase\", 1", "ObjectGameState.Remove(key)",
				"ItemCleanupPhase\", 2", "ExactCleanupItemState(",
				"ItemCleanupPhase\", 3", "ClearPendingItem(", "ItemCleanupPhase\", 0");

			string removal = Between(source, "private static bool TryRemoveHandoverPredecessor(",
				"public partial class r_KingdomImprovement");
			AssertOrdered(removal, "TryPublishRemovalIntent(", "RemovalProofProperty",
				"Predecessor.Destroy(", "ObserveCurrentTopologyInActive(",
				"TryRecoverAbsentHandover(", "KingdomPhysicalPhase.FinalRemoved",
				"TryRetirePendingUpgradeComponents(", "active.ObserveChanged(Successor)",
				"KingdomConstruction.Complete(ref Job)");
		}

		[Test]
		public void PredecessorRemovalAndRecoveryRequireGlobalLiveIdProof()
		{
			string source = Upgrade();
			string removal = Between(source, "private static bool TryRemoveHandoverPredecessor(",
				"public partial class r_KingdomImprovement");
			AssertOrdered(removal, "FindGlobalPredecessorAuthority(",
				"TryPublishRemovalIntent(", "Predecessor.Destroy(",
				"FindGlobalPredecessorAuthority(job, Successor",
				"GameObject.Validate(Predecessor)", "ImprovementRemovalAftermath(",
				"TryRecoverAbsentHandover(", "FindGlobalPredecessorAuthority(Job, Successor",
				"KingdomPhysicalPhase.FinalRemoved");
			StringAssert.DoesNotContain("FindExactId(Successor.CurrentZone, predecessorId", removal);
			StringAssert.DoesNotContain("FindExactId(Z, Job.SubjectId", removal);
			StringAssert.Contains("aftermath != KingdomExactRemovalAction.ProvedAbsent", removal);
			StringAssert.Contains("Improvement removal moved or ambiguously changed an endpoint", removal);
			StringAssert.Contains("!ExactRecoverableRemovalReceipt(Job)", removal);
			string legacy = Between(removal, "private static bool ExactRecoverableRemovalReceipt(",
				"private static bool ExactPendingRemovalProof(");
			StringAssert.Contains("Job.PhysicalPhase == KingdomPhysicalPhase.FinalRemoved", legacy);
			StringAssert.Contains("Job.PhysicalIndex == 0 && Job.PhysicalAmount == 0", legacy);
			StringAssert.Contains("Job.PhysicalReceipt == LegacyImprovementRemovalReceipt", legacy);
		}

		[Test]
		public void InspectionResumesDestroyedPredecessorAndRepairsTerminalRemovalCuts()
		{
			string source = Upgrade();
			string inspect = Between(source, "internal static void InspectConstruction(",
				"public static void ClearChains(");
			string terminal = Between(inspect,
				"if (Job.Phase == KingdomConstructionPhase.Complete)", "GameObject work;");
			AssertOrdered(terminal, "FindExactSuccessors(Z, Job",
				"FinishAbsentImprovement(System, Z, completed");
			StringAssert.DoesNotContain("TellCompletion(System, completed, Job)", terminal);

			string absent = Between(inspect,
				"if (!EnsureExactImprovementPredecessor(System, Z, work, Job))",
				"r_KingdomImprovement carriedIntent");
			AssertOrdered(absent, "FindExactSuccessors(Z, Job",
				"if (completedCount == 1)",
				"FinishAbsentImprovement(System, Z, completed");
			StringAssert.DoesNotContain("KingdomConstruction.Complete(ref absent)", absent);

			string finish = Between(inspect, "private static void FinishAbsentImprovement(",
				"private static bool HasActiveConstruction(");
			AssertOrdered(finish, "RequiresAbsentHandoverRecovery(Job.PhysicalPhase)",
				"TryRecoverAbsentHandover(System, Z, Successor, ref Job",
				"Job.Phase == KingdomConstructionPhase.Complete",
				"KingdomConstruction.TryFind(Job.Id, out current)",
				"KingdomConstruction.Complete(ref Job, recoveryFailure)",
				"HasRemovalProof(Successor, Job.SubjectId)",
				"bool completed = Job.Phase == KingdomConstructionPhase.Complete",
				"KingdomConstruction.Complete(ref Job)",
				"TellCompletion(System, Successor, Job)",
				"TryRetireHandoverContentCustody(Successor, Job",
				"KingdomConstruction.TryFind(Job.Id, out current)",
				"KingdomConstruction.Complete(ref Job, cleanupFailure)");
		}

		private enum LiquidTopology { Before, Drained, Settled, Foreign }
		private enum RemovalCut { Invalid = -1, None, Pending, Removed, Complete }

		private static int AdvanceLiquid(int phase, LiquidTopology topology)
		{
			if (phase == 1 && topology == LiquidTopology.Before) return 1;
			if (phase == 1 && topology == LiquidTopology.Drained) return 2;
			if (phase == 2 && topology == LiquidTopology.Drained) return 2;
			if (phase == 2 && topology == LiquidTopology.Settled) return 3;
			return phase == 3 && topology == LiquidTopology.Settled ? 3 : -1;
		}

		private static int AdvanceCleanup(int phase, bool rooted, bool receiptExact,
			bool itemExact)
		{
			if (phase == 0) return receiptExact ? 0 : -1;
			if (phase == 1) return receiptExact && itemExact ? 2 : -1;
			if (phase == 2) return !rooted && receiptExact && itemExact ? 3 : -1;
			return phase == 3 && !rooted && receiptExact ? 0 : -1;
		}

		private static RemovalCut AdvanceRemoval(RemovalCut phase, bool predecessor,
			bool proof)
		{
			if (phase == RemovalCut.None) return predecessor && proof
				? RemovalCut.Pending : RemovalCut.Invalid;
			if (phase == RemovalCut.Pending) return !proof ? RemovalCut.Invalid
				: predecessor ? RemovalCut.Pending : RemovalCut.Removed;
			return phase == RemovalCut.Removed && !predecessor && proof
				? RemovalCut.Complete : RemovalCut.Invalid;
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
