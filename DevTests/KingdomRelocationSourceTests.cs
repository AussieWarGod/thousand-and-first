#if TAF_TESTS
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomRelocationSourceTests
	{
		private static string Relocation()
		{
			string folder = Path.Combine(TestMain.RepositoryRoot, "Growth");
			return string.Join("\n", Directory.GetFiles(folder, "KingdomRelocation*.cs")
				.OrderBy(x => x, StringComparer.Ordinal).Select(File.ReadAllText));
		}

		[Test] public void ConsentPrecedesEveryMutationAndDebitDoesNotExist()
		{
			string source = Relocation(); string ui = TestMain.ReadRepositoryText("Growth/KingdomRelocation.UI.cs");
			Assert.Less(ui.IndexOf("Consent to this complete plan", StringComparison.Ordinal),
				ui.IndexOf("TryOpen(Zone, exact.Receipt", StringComparison.Ordinal));
			StringAssert.Contains("ReproveApproved", ui);
			StringAssert.DoesNotContain("Consume(", source);
			StringAssert.DoesNotContain("KingdomWaterDebit", source);
			StringAssert.DoesNotContain("KingdomMaterialDebit", source);
			StringAssert.DoesNotContain("DrawFrom", source);
		}

		[Test] public void HandoverMovesOriginalsAndNeverClonesOrRestakesPlots()
		{
			string source = Relocation(); string objects = TestMain.ReadRepositoryText(
				"Growth/KingdomRelocation.HandoverObjects.cs");
			StringAssert.Contains("RootEscrow(Receipt, item", objects);
			StringAssert.Contains("destination.AddObject(item", objects);
			StringAssert.Contains("Row.State = KingdomRelocationRowState.Rooted", objects);
			Assert.Less(objects.IndexOf("Row.State = KingdomRelocationRowState.Rooted", StringComparison.Ordinal),
				objects.IndexOf("RemoveForHandover", StringComparison.Ordinal));
			StringAssert.DoesNotContain("Clone(", source);
			StringAssert.DoesNotContain("KingdomPlots.Stake", source);
			StringAssert.DoesNotContain("Strike", source);
		}

		[Test] public void EvidenceCarriesWholeLotAndRootMovesLast()
		{
			string evidence = TestMain.ReadRepositoryText("Growth/KingdomRelocation.Evidence.cs");
			StringAssert.Contains("PlotPartProperty", evidence);
			StringAssert.Contains("PlotIdProperty) == lot", evidence);
			StringAssert.Contains("A.Root ? 1 : -1", evidence);
			StringAssert.Contains("item.ID", evidence);
			StringAssert.Contains("item.Blueprint", evidence);
			StringAssert.Contains("Root.GetPart<r_KingdomPlotWorks>() != null", evidence);
		}

		[Test] public void ArchitectureIdentityPoseAndHashAreFrozenThenRetargeted()
		{
			string architecture = TestMain.ReadRepositoryText("Growth/KingdomRelocation.Architecture.cs");
			StringAssert.Contains("before.EncodedSnapshot", architecture);
			StringAssert.Contains("before.SnapshotHash", architecture);
			StringAssert.Contains("before.Facing", architecture);
			StringAssert.Contains("placement.ExistingAuthority", architecture);
			StringAssert.Contains("KingdomArchitectureIntent.CreateRaw", architecture);
			StringAssert.DoesNotContain("TryResolve", architecture);
		}

		[Test] public void OneCurrentMoveOwnsOneVisibleCrewFrame()
		{
			string frames = TestMain.ReadRepositoryText("Growth/KingdomRelocation.Frames.cs");
			string activation = TestMain.ReadRepositoryText("Growth/KingdomRelocation.Activation.cs");
			StringAssert.Contains("Receipt.Moves[Receipt.CurrentMove]", frames);
			StringAssert.Contains("i == 0 ? FrameBlueprint : StakeBlueprint", frames);
			StringAssert.Contains("KingdomConstructionPresence.EffectivenessOf(frame", activation);
			StringAssert.Contains("Receipt.CurrentMove++", TestMain.ReadRepositoryText(
				"Growth/KingdomRelocation.Handover.cs"));
			StringAssert.Contains("CleanCompletedArtifacts", activation);
		}

		[Test] public void InterruptedHandoverResumesBeforeAnyNewLabourAdvance()
		{
			string activation = TestMain.ReadRepositoryText("Growth/KingdomRelocation.Activation.cs");
			int resume = activation.IndexOf("move.Phase == KingdomRelocationMovePhase.Handover",
				StringComparison.Ordinal);
			int advance = activation.IndexOf("KingdomArchitectureRules.AdvanceLabour",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(resume, 0); Assert.Greater(advance, resume);
			StringAssert.Contains("TryHandOver(System, Zone, ref expected", activation);
		}

		[Test] public void RecoveryCoversCasEscrowCallbacksRollbackAndSecession()
		{
			string source = Relocation();
			StringAssert.Contains("compare-and-swap", source);
			StringAssert.Contains("ObjectGameState", source);
			StringAssert.Contains("ObserveCurrentTopologyInActive", source);
			StringAssert.Contains("RollbackAndQuarantine", source);
			StringAssert.Contains("BeforeOwnershipLoss", source);
			StringAssert.Contains("ZoneThawedEvent.ID", TestMain.ReadRepositoryText(
				"Growth/r_KingdomRelocationFrame.cs"));
			string activation = TestMain.ReadRepositoryText("Growth/KingdomRelocation.Activation.cs");
			StringAssert.Contains("KingdomRelocationMovePhase.RollingBack", activation);
			StringAssert.Contains("KingdomRelocationMovePhase.RolledBack", activation);
			string rollback = TestMain.ReadRepositoryText("Growth/KingdomRelocation.Rollback.cs");
			Assert.Less(rollback.IndexOf("Receipt.Failure = Bounded(Reason)", StringComparison.Ordinal),
				rollback.IndexOf("move.Phase = KingdomRelocationMovePhase.RollingBack", StringComparison.Ordinal));
			StringAssert.Contains("RestoreCellReady", rollback);
		}

		[Test] public void ObstructionLawNeverDisplacesCreaturesOrProtectedObjects()
		{
			string ground = TestMain.ReadRepositoryText("Growth/KingdomRelocation.HandoverGround.cs");
			StringAssert.Contains("item.IsCreature || item.IsPlayer()", ground);
			StringAssert.Contains("Nothing is displaced", TestMain.ReadRepositoryText(
				"Growth/KingdomRelocation.Handover.cs"));
			StringAssert.DoesNotContain("SystemMoveTo", ground);
		}

		[Test] public void PlotIdentityHousingRoofWorkAndNetworksAreCarriedNotRecreated()
		{
			string source = Relocation();
			StringAssert.Contains("KingdomPlots.StampRect(root, Runtime(Move.Destination))", source);
			StringAssert.Contains("KingdomPlots.StampFootprint(root", source);
			StringAssert.Contains("KingdomNetworks.MarkTopologyChanged", source);
			StringAssert.Contains("declared networks rejoin from the new ground", source);
			StringAssert.DoesNotContain("HomePlotIdProperty", source);
			StringAssert.DoesNotContain("KingdomBrink", source);
			StringAssert.DoesNotContain("SetStringProperty(KingdomPlots.PlotIdProperty", source);
			StringAssert.DoesNotContain("RemovePart<r_Kingdom", source);
		}

		[Test] public void ExistingLayoutScorerAndLawfulManualOverrideShareOnePlanner()
		{
			string siting = TestMain.ReadRepositoryText("Growth/KingdomRelocation.Siting.cs");
			StringAssert.Contains("KingdomPlotRules.ChooseRect", siting);
			StringAssert.Contains("Overrides.TryGetValue", siting);
			StringAssert.Contains("GroundCanReceive", siting);
			StringAssert.Contains("TryArchitectureDestination", siting);
			string proof = TestMain.ReadRepositoryText("Growth/KingdomRelocation.UIProof.cs");
			StringAssert.Contains("SamePlan", proof);
		}

		[Test] public void MasterAndModulePausesCannotBankElapsedTime()
		{
			string activation = TestMain.ReadRepositoryText("Growth/KingdomRelocation.Activation.cs");
			StringAssert.Contains("move.LastTick <= System.MasterOptionTick", activation);
			StringAssert.Contains("!KingdomUpgrade.Enabled", activation);
			StringAssert.Contains("PauseClock", activation);
			string presence = TestMain.ReadRepositoryText("Growth/KingdomRelocation.Presence.cs");
			StringAssert.Contains("KingdomUpgrade.Enabled && KingdomMaster.AutomaticWorkAllowed", presence);
		}

		[Test] public void HeartMenuRoutesYieldingBlockerToCompletePlan()
		{
			string menu = TestMain.ReadRepositoryText("Growth/KingdomUpgrade.23.Menu.cs");
			StringAssert.Contains("KingdomRelocation.CanOffer", menu);
			StringAssert.Contains("KingdomRelocation.OpenHeartRingCall", menu);
			string wording = TestMain.ReadRepositoryText("Growth/KingdomPlotHeartSurveyRules.cs");
			StringAssert.Contains("same whole lot", wording);
			StringAssert.Contains("no stores", wording);
		}

		[Test] public void FrameUsesVanillaArtAndAllProductionShardsStayBounded()
		{
			string blueprints = TestMain.ReadRepositoryText("ObjectBlueprints.xml");
			StringAssert.Contains("Name=\"r_KingdomRelocationFrame\"", blueprints);
			StringAssert.Contains("Tile=\"Items/sw_fence_gates_2_open.bmp\"", blueprints);
			StringAssert.Contains("Name=\"r_KingdomRelocationStake\" Inherits=\"Sign\"", blueprints);
			foreach (string path in Directory.GetFiles(Path.Combine(TestMain.RepositoryRoot, "Growth"),
				"KingdomRelocation*.cs"))
				Assert.Less(File.ReadLines(path).Count(), 300, Path.GetFileName(path));
		}
	}
}
#endif
