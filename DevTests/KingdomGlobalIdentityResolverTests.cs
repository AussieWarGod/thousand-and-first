#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomGlobalIdentityResolverTests
	{
		private static string Source(string directory, string file)
		{
			return TestMain.ReadRepositoryText(Path.Combine(directory, file));
		}

		private static string Between(string source, string start, string end)
		{
			int first = source.IndexOf(start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(first, 0, start);
			int last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
			Assert.Greater(last, first, end);
			return source.Substring(first, last - first);
		}

		private static int MintingReads(string source)
		{
			return Regex.Matches(source, @"\.\s*ID\b").Count;
		}

		[Test]
		public void GlobalAndRecursiveResolversUseOnlyAssignedIdentity()
		{
			string escrow = Source("Growth",
				"KingdomUpgrade.04.r_KingdomImprovement.Escrow.cs");
			string construction = Between(Source("Growth", "KingdomConstruction.Physical.cs"),
				"public static KingdomPhysicalLookupState FindExactId(",
				"public static KingdomPhysicalLookupState FindReceipt(");
			string trade = Between(Source("Trade", "KingdomTrade.04.LoadedTopologyResolution.cs"),
				"private static LoadedObjectResolution ResolveLoadedObject(",
				"private static bool ExactLoadedTopologyWithDelta(");
			string upgrade = Between(escrow,
				"private static bool CountZoneIdentity(",
				"private static bool ExactEnteringCell(");

			Assert.AreEqual(0, MintingReads(construction), "construction loaded graph");
			Assert.AreEqual(0, MintingReads(trade), "trade loaded topology");
			Assert.AreEqual(0, MintingReads(upgrade), "upgrade root/inventory graph");
			StringAssert.Contains("item.IDIfAssigned != Id", construction);
			StringAssert.Contains("row => row.Object.IDIfAssigned", trade);
			Assert.AreEqual(2, Regex.Matches(upgrade, @"item\.IDIfAssigned").Count);
			Assert.AreEqual(2, MintingReads(escrow),
				"only selected source/item escrow-key creation may assign identity");
			StringAssert.Contains("Source?.IDIfAssigned", escrow);
			StringAssert.Contains("Target.IDIfAssigned", escrow);
			StringAssert.Contains("count, Exact != null", construction);
			StringAssert.Contains("LoadedObjectResolution.Ambiguous", trade);
		}

		[Test]
		public void DestructiveGlobalLookupReusesBoundedLiveCustodyProof()
		{
			string physical = Source("Growth", "KingdomConstruction.Physical.cs");
			string lookup = Between(physical,
				"public static KingdomPhysicalLookupState FindGlobalLiveId(",
				"private static bool TryLoadedZoneObjects(");
			Assert.AreEqual(0, MintingReads(lookup));
			StringAssert.Contains("KingdomPlots.FindGlobalFoundingHeartId(Id", lookup);
			StringAssert.Contains("state == KingdomPhysicalLookupState.Exact && graveyard", lookup);
			StringAssert.Contains("Exact = null", lookup);

			string custody = Source("Growth", "KingdomPlot2.07b.FoundingHeartIdentity.cs");
			foreach (string evidence in new[] { "ActiveZone", "CachedZones", "Graveyard",
				"ObjectGameState", "GetInventoryDirectAndEquipment",
				"MaximumFoundingHeartCustodyObjects" })
				StringAssert.Contains(evidence, custody);
		}

		[Test]
		public void ImprovementRemovalAuthorityCoversReloadCustodyAndIgnoresTombstones()
		{
			string authority = Source("Growth", "KingdomConstruction.RemovalAuthority.cs");
			Assert.AreEqual(0, MintingReads(authority));
			foreach (string evidence in new[] { "ActiveZone", "CachedZones", "Graveyard",
				"The.Player", "ObjectGameState", "GetInventoryDirectAndEquipment",
				"MaxGlobalRemovalAuthorityObjects", "graveyard.Contains(candidate)",
				"ImprovementPredecessorAuthority" })
				StringAssert.Contains(evidence, authority);
			string removal = Source("Growth", "KingdomUpgrade.25.HandoverRemoval.cs");
			string recovery = Between(removal, "internal static bool TryRecoverAbsentHandover(",
				"private static bool TryPublishRemovalIntent(");
			StringAssert.Contains(
				"FindGlobalPredecessorAuthority(Job, Successor, out _)", recovery);
			StringAssert.DoesNotContain("FindGlobalLiveId(Job.SubjectId", recovery);
		}

		[Test]
		public void ObservationAndRaidResolversProveMarkersThenReadAssignedIdentity()
		{
			string polity = Between(Source("Polity", "KingdomPolityEndpointRuntime.Helpers.cs"),
				"private static bool TryObserve(",
				"private static bool TryBindExactLegacyOwners(");
			string guest = Between(Source("Experience", "KingdomGuestLifecycle.TrustedWorld.cs"),
				"private List<IKingdomLifecycleTrustedObservation> Build()", "return rows;");
			string demand = Between(Source("Raids", "KingdomRaids.03.DemandChannel.cs"),
				"internal static bool HasExactDemandWitness(",
				"internal static bool TryAcknowledgeDemand(");
			string projection = Between(Source("Raids", "KingdomRaids.08.DemandProjection.cs"),
				"private static void CountProjection(",
				"private static bool ResumeDemandProjection(");
			string attackRuntime = Source("Raids",
				"KingdomRaids.09.AttackProjectionAndHelpers.cs");
			string attack = Between(attackRuntime,
				"private static GameObject FindExact(",
				"private static int CountLiveRaiders(");

			foreach (string scan in new[] { polity, guest, demand, projection, attack })
				Assert.AreEqual(0, MintingReads(scan));
			Assert.Less(polity.IndexOf("bool marked", StringComparison.Ordinal),
				polity.IndexOf("body.IDIfAssigned", StringComparison.Ordinal));
			Assert.Less(demand.IndexOf("GetPart<r_KingdomRaidDemand>", StringComparison.Ordinal),
				demand.IndexOf("item.IDIfAssigned", StringComparison.Ordinal));
			Assert.Less(projection.IndexOf("GetStringProperty(ProjectionMarkerProperty)",
				StringComparison.Ordinal), projection.IndexOf("item.IDIfAssigned",
				StringComparison.Ordinal));
			StringAssert.Contains("new Observation(item, item.IDIfAssigned", guest);
			StringAssert.Contains("item.IDIfAssigned == id", attack);
			string demandProjection = Source("Raids", "KingdomRaids.08.DemandProjection.cs");
			StringAssert.DoesNotContain("owner.ID !=", demandProjection);
			StringAssert.DoesNotContain("owner.ID, zone.ZoneID", demandProjection);
			StringAssert.DoesNotContain("body.ID != projection.ObjectId", demandProjection);
			StringAssert.Contains("owner.IDIfAssigned", demandProjection);
			StringAssert.Contains("body.IDIfAssigned != projection.ObjectId", attackRuntime);
		}

		[Test]
		public void PreviewPlanningAndSelectionPathsNeverMintIdentity()
		{
			string relocationPlanning = Source("Growth", "KingdomRelocation.Planning.cs");
			string relocationEvidence = Source("Growth", "KingdomRelocation.Evidence.cs");
			string relocationClearance = Source("Growth", "KingdomRelocation.Clearance.cs");
			string relocationRollback = Between(Source("Growth", "KingdomRelocation.Rollback.cs"),
				"private static bool RestoreCellReady(", "\n\t}\n}");
			string purposePairing = Source("Growth", "KingdomPurposePortfolio.Pairing.cs");
			string purposeHelpers = Source("Growth", "KingdomPurposePortfolio.PairingHelpers.cs");
			string purposePlan = Source("Growth", "KingdomPurposePortfolio.LocalPlan.cs");
			string purposeSiting = Source("Growth", "KingdomPurpose.06.PortfolioSiting.cs");
			string bountySelection = Between(Source("Quests",
				"KingdomBounty.ManningSelection.cs"), "private static List<GameObject> ManningCandidates(",
				"private static void BindManningTarget(");
			string bountyLookup = Source("Quests", "KingdomBounty.Manning.cs");
			string labSelection = Source("Growth", "KingdomLab.CivicSelection.cs");
			string hostedInteraction = Source("Growth", "KingdomHostedArcology.Interaction.cs");
			string hostedVisual = Source("Growth", "KingdomHostedArcology.Visual.cs");

			foreach (string preview in new[] { relocationPlanning, relocationEvidence,
				relocationClearance, relocationRollback, purposePairing, purposeHelpers,
				purposePlan, purposeSiting, bountySelection, bountyLookup, labSelection,
				hostedInteraction }) Assert.AreEqual(0, MintingReads(preview));
			Assert.AreEqual(1, MintingReads(hostedVisual),
				"only explicit hosted child identity assignment may remain");
			StringAssert.Contains("Prepared[i].Output.ID = Prepared[i].Id", hostedVisual);
			StringAssert.Contains("Prepared[i].Output.IDIfAssigned != Prepared[i].Id", hostedVisual);
			StringAssert.Contains("item.IDIfAssigned == id", hostedVisual);
			StringAssert.Contains("string workId = work?.IDIfAssigned", bountySelection);
			StringAssert.Contains("string rootId = Root.IDIfAssigned", relocationEvidence);
			StringAssert.Contains("Purpose water lacks assigned container identity", purposePlan);
		}

		[Test]
		public void RecoveryPresentationAndPlannerScansNeverMintIdentity()
		{
			string scalar = Source("Simulation/City",
				"KingdomCentralLogistics.08.ScalarCustodyAndReceiptHelpers.cs");
			string[] pure = {
				Source("Experience", "KingdomCivicKnowledgeRuntime.Presentation.cs"),
				Source("Experience", "KingdomGuestFeastRuntime.Observations.cs"),
				Source("Experience", "KingdomLocus.z00.Keeper.cs"),
				Source("Quests", "r_KingdomNotice.Serialization.cs"),
				Source("Growth", "KingdomConstruction.InputPlannerScan.cs"),
				Source("Growth", "KingdomPlanMarker.LookupAndCommands.cs"),
				Source("Growth", "KingdomGatehouse.Validation.cs"),
				Source("Growth", "KingdomPurpose.04.DeliveryAndLookup.cs"),
				Source("Growth", "KingdomMaterials.12.StrikeContinuation.cs"),
				Source("Simulation/City", "KingdomDistanceRuntime.CandidatesAndSelection.cs"),
				Source("Simulation/City", "KingdomDistanceRuntime.PlanningAndTransfer.cs"),
				Source("Simulation/City", "KingdomResidents.06.Helpers.cs")
			};
			foreach (string scan in pure) Assert.AreEqual(0, MintingReads(scan));
			Assert.AreEqual(1, MintingReads(scalar),
				"only committed newly-created food receipt identity assignment may remain");
			StringAssert.Contains("string foodId = food.ID;", scalar);
			StringAssert.Contains("food.IDIfAssigned != foodId", scalar);
			string presence = Source("Growth", "KingdomConstructionPresence.cs");
			Assert.AreEqual(1, MintingReads(presence),
				"only committed selected construction-work identity creation may remain");
			StringAssert.Contains("selected.Root.ID", presence);
			StringAssert.Contains("string itemId = item.IDIfAssigned", presence);
		}

		[Test]
		public void GrowthTransferAndReceiptRecoveryNeverMintIdentity()
		{
			string[] pure = {
				Source("Growth", "KingdomPlot2.24.GrowthProofs.cs"),
				Source("Growth", "KingdomLab.VatReceipts.cs"),
				Source("Quests", "KingdomBounty.Transfer.cs"),
				Source("Growth", "KingdomPlot2.23.GrowthPlanning.cs"),
				Source("Growth", "KingdomPlanMarker.RecoveryAndInspection.cs")
			};
			foreach (string scan in pure) Assert.AreEqual(0, MintingReads(scan));
			Assert.AreEqual(2, MintingReads(Source("Growth",
				"KingdomProcedures.07.RebuildAndSnapshots.cs")),
				"anatomical BodyPart IDs are not GameObject identity reads");
			string builder = Source("World", "KingdomHostedArcologyBuilder.cs");
			Assert.AreEqual(1, MintingReads(builder));
			StringAssert.Contains(
				"string id = KingdomHostedArcologyRules.StableChildId(RootId, Role)", builder);
			StringAssert.Contains("item.ID = id", builder);
			StringAssert.Contains("candidate.IDIfAssigned == id", builder);
			string hosted = Source("Growth", "KingdomHostedArcology.Construction.cs");
			Assert.AreEqual(1, MintingReads(hosted), "only consented hosted job publication");
			string presence = Source("Growth", "KingdomConstructionPresence.cs");
			Assert.AreEqual(1, MintingReads(presence));
		}

		[Test]
		public void OwnershipFoodDebitAndContinuationScansNeverMintIdentity()
		{
			string[] pure = {
				Source("Growth", "KingdomProcedures.08.OwnershipClassification.cs"),
				Source("Growth", "KingdomHostedArcology.Authority.cs"),
				Source("Growth", "KingdomConstruction.InputDrive.SourcePhysical.cs"),
				Source("Growth", "KingdomUpgrade.25.HandoverRemoval.cs"),
				Source("Growth", "KingdomSurvey.06.ExactSpoilage.cs"),
				Source("Growth", "KingdomSocket.03.ConstructionContinuation.cs"),
				Source("Growth", "KingdomMaterials.08.StrikeOrdering.cs"),
				Source("Growth", "KingdomLabCivicOwnership.cs")
			};
			foreach (string scan in pure) Assert.AreEqual(0, MintingReads(scan));
			Assert.AreEqual(3, MintingReads(Source("Growth",
				"KingdomProcedures.04.GrantRouting.cs")),
				"anatomical slot IDs are not GameObject identity reads");
			Assert.AreEqual(3, MintingReads(Source("Growth",
				"KingdomProcedures.05.GrantExecution.cs")),
				"anatomical BodyPart and slot IDs are not GameObject identity reads");
			string realization = Source("Growth", "KingdomPlanMarker.Realization.cs");
			Assert.AreEqual(1, MintingReads(realization), "new scaffold publication only");
		}

		[Test]
		public void ReturnSelectionAndPlotRecoveryScansNeverMintIdentity()
		{
			string[] pure = {
				Source("Experience", "KingdomOfficeRuntime.Reconcile.cs"),
				Source("Growth", "KingdomAnnexe.Lookup.cs"),
				Source("Growth", "KingdomCommission.Recovery.cs"),
				Source("Growth", "KingdomLab.ApplicationRecovery.cs"),
				Source("Growth", "KingdomLab.CivicReconciliation.cs"),
				Source("Growth", "KingdomLodging.BrinkAndObservation.cs"),
				Source("Growth", "KingdomMaterialDebit.Validation.cs"),
				Source("Growth", "KingdomMaterials.11.StrikeWorkAndRecoveryEntry.cs"),
				Source("Growth", "KingdomPlot2.15.RecoveryRetry.cs"),
				Source("Growth", "KingdomPlot2.16.RecoveryInspect.cs"),
				Source("Growth", "KingdomPlot2.19.PlanStaking.cs")
			};
			foreach (string scan in pure) Assert.AreEqual(0, MintingReads(scan));
			Assert.AreEqual(1, MintingReads(Source("Core",
				"KingdomSystem.z14.Return.AbilityProof.cs")),
				"an ActivatedAbilityEntry Guid is not GameObject identity");
			Assert.AreEqual(2, MintingReads(Source("Growth",
				"KingdomLab.PurposeSelection.cs")),
				"anatomical BodyPart IDs are not GameObject identity reads");
			Assert.AreEqual(1, MintingReads(Source("Experience",
				"KingdomSuccession.DeathSelection.cs")), "committed founder-death token only");
		}

		[Test]
		public void RoadPaymentRaidAndTradeRecoveryScansNeverMintObjectIdentity()
		{
			string[] pure = {
				Source("Growth", "KingdomRoads.08.RoadReceiptHelpersAndStatus.cs"),
				Source("Growth", "KingdomScaffold.SuccessorProof.cs"),
				Source("Growth", "KingdomUpgrade.08.ConstructionInspect.cs"),
				Source("Growth", "KingdomUpgrade.18.ProjectionProofs.cs"),
				Source("Growth", "KingdomUpgrade.21.HandoverProofs.cs"),
				Source("Growth", "KingdomWear.01.RepairRecovery.cs"),
				Source("Polity", "KingdomPolityHospitalityRuntime.Planning.cs"),
				Source("Quests", "KingdomBounty.PaymentObservation.cs"),
				Source("Quests", "KingdomBounty.PaymentPlanning.cs"),
				Source("Quests", "KingdomBounty.Publication.cs"),
				Source("Quests", "KingdomBounty.Sinks.cs"),
				Source("Quests", "KingdomBounty.Cleanup.cs"),
				Source("Quests", "KingdomBounty.Schedule.cs"),
				Source("Quests", "KingdomBounty.ReadingGround.cs"),
				Source("Trade", "KingdomTrade.15.MaterialRecovery.cs"),
				Source("Trade", "KingdomTrade.17.ProjectionRecovery.cs")
			};
			foreach (string scan in pure) Assert.AreEqual(0, MintingReads(scan));
			Assert.AreEqual(3, MintingReads(Source("Raids",
				"KingdomRaids.04.RecoveryAndFortify.cs")), "Quest and QuestStep IDs only");
		}

		[Test]
		public void MarketLabWaterAndFinalizationRecoveryScansNeverMintIdentity()
		{
			string[] pure = {
				Source("Experience", "KingdomGuestbook.z01b.MarketHandoff.cs"),
				Source("Growth", "KingdomLab.Governance.cs"),
				Source("Growth", "KingdomConstruction.InputDrive.Water.cs"),
				Source("Trade", "KingdomTrade.12.WaterMutation.cs"),
				Source("Quests", "KingdomPetitionLifecycle.SnapshotAndOutbox.cs"),
				Source("Growth", "KingdomWear.10.LeakOutputs.cs"),
				Source("Growth", "KingdomPurposePortfolio.Funding.cs"),
				Source("Growth", "KingdomPlot2.32.FinishRemoval.cs"),
				Source("Growth", "KingdomPlot2.27.FinalBuilding.cs")
			};
			foreach (string scan in pure) Assert.AreEqual(0, MintingReads(scan));
			Assert.AreEqual(2, MintingReads(Source("Growth", "KingdomLab.Slate.cs")),
				"anatomical BodyPart IDs are not GameObject identity reads");
			Assert.AreEqual(0, MintingReads(KingdomGatehouseLogicalSource.ReadProjection()),
				"gatehouse recovery observes only already-assigned root identity");
			Assert.AreEqual(2, MintingReads(
				KingdomGatehouseLogicalSource.ReadProjectionEvidence()),
				"one legacy engine-assigned satellite identity read at creation, and one "
					+ "explicit deterministic satellite identity setter");
			Assert.AreEqual(1, MintingReads(Source("Growth", "KingdomSocket.06.ConversionProjection.cs")));
			Assert.AreEqual(11, MintingReads(Source("Growth", "KingdomPurpose.01.Transport.cs")));
			string handover = Source("Growth", "KingdomUpgrade.20.HandOver.cs");
			Assert.AreEqual(2, MintingReads(handover),
				"the committed predecessor identity is established once, then re-read for its job proof");
			StringAssert.Contains("string predecessorId = Predecessor.ID", handover);
			StringAssert.Contains("job.SubjectId != Predecessor.ID", handover);
			Assert.AreEqual(5, MintingReads(Source("Growth", "KingdomPlot2.28.ClearPayout.cs")));
		}

		[Test]
		public void RegistryLodgingLabAndRaidRecoveryScansNeverMintIdentity()
		{
			string[] pure = {
				Source("Growth", "KingdomGrowth.z14.HashUtilities.cs"),
				Source("Growth", "KingdomConstruction.Registry.cs"),
				Source("Growth", "KingdomLodging.cs"),
				Source("Growth", "KingdomArchitectureStamper.UpgradeReceipts.cs"),
				Source("Experience", "KingdomExpeditions.DebitReceipts.cs")
			};
			foreach (string scan in pure) Assert.AreEqual(0, MintingReads(scan));
			Assert.AreEqual(1, MintingReads(Source("Growth",
				"KingdomLab.PurposeRuntime.cs")),
				"an anatomical BodyPart ID is not GameObject identity");
			Assert.AreEqual(1, MintingReads(Source("Raids",
				"KingdomRaids.05.AttackLaunchAndResume.cs")), "explicit projected raider ID setter");
			Assert.AreEqual(2, MintingReads(Source("Growth", "KingdomCommission.Projection.cs")));
			Assert.AreEqual(4, MintingReads(Source("Growth", "KingdomLab.Preparation.cs")));
			Assert.AreEqual(4, MintingReads(Source("Growth", "KingdomLab.Commission.cs")));
		}

		[Test]
		public void PlotStrikeBountyAndLeakRecoveryNeverMintIdentity()
		{
			string[] pure = {
				Source("Growth", "KingdomPlot2.12.Projection.cs"),
				Source("Growth", "KingdomMaterials.09.StrikeStampAndCancellation.cs"),
				Source("Quests", "KingdomBounty.WorkAndCarry.cs"),
				Source("Quests", "KingdomBounty.PassAndSchedule.cs"),
				Source("Growth", "KingdomWear.08.LeakFrame.cs")
			};
			foreach (string scan in pure) Assert.AreEqual(0, MintingReads(scan));
			StringAssert.Contains("settlement.ID.ToString", Source("Integrations/Hearthpyre223",
				"KingdomHearthpyreOwnershipProvider.cs"));
			StringAssert.Contains("BeforeApplyDamageEvent.ID", Source("Polity",
				"r_KingdomPolityEscrow.cs"));
		}

		[Test]
		public void StasisRoadAndPurposeRecoveryNeverMintIdentity()
		{
			string[] pure = {
				Source("Growth", "KingdomStasisVault.Entry.cs"),
				Source("Growth", "KingdomSocket.07.ClearanceAndSockets.cs"),
				Source("Growth", "KingdomRoads.07.RoadReceiptCodec.cs"),
				Source("Growth", "KingdomPurposePortfolio.OperationControl.cs"),
				Source("Growth", "KingdomPurpose.05.Siting.cs"),
				Source("Growth", "KingdomPurpose.03.CargoIdentityAndEscrow.cs"),
				Source("Growth", "KingdomPurpose.02.Commitments.cs")
			};
			foreach (string scan in pure) Assert.AreEqual(0, MintingReads(scan));
			Assert.AreEqual(2, MintingReads(Source("Growth",
				"KingdomPurposePortfolio.OutputRuntime.cs")), "new cargo publication only");
			string handoverEndpoints = Source("Growth",
				"KingdomUpgrade.03.r_KingdomImprovement.PendingItems.cs");
			Assert.AreEqual(6, MintingReads(handoverEndpoints),
				"only explicit durable handover endpoint publication may assign identity");
			StringAssert.Contains("Receipt.HandoverSourceId = Source.ID", handoverEndpoints);
			StringAssert.Contains("Receipt.HandoverTargetId = Target.ID", handoverEndpoints);
			string pendingItem = Source("Growth",
				"KingdomUpgrade.02.r_KingdomImprovement.Inventory.cs");
			Assert.AreEqual(2, MintingReads(pendingItem),
				"only explicit pending-item identity publication may assign identity");
			StringAssert.Contains("Receipt.HandoverItemId = Item.ID", pendingItem);
		}

		[Test]
		public void PowerDelveGuestAndHospitalityObservationNeverMintIdentity()
		{
			string[] pure = {
				Source("Growth", "KingdomPower.FlowResolution.cs"),
				Source("Growth", "KingdomLodging.LabFriction.cs"),
				Source("Growth", "KingdomDelveLink.01.SettlementAndStrikePreflight.cs"),
				Source("Growth", "KingdomDelveLink.05.ConnectionStrikeAndFaultHelpers.cs"),
				Source("Experience", "KingdomLocus.z00a.KeeperProjection.cs"),
				Source("Experience", "KingdomGuestLifecycle.SinksScheduleAndAuthority.cs"),
				Source("Experience", "KingdomGuestLifecycle.RemovalAndLodge.cs"),
				Source("Polity", "KingdomPolityHospitalityRuntime.Debit.cs"),
				Source("Growth", "KingdomSurvey.07.ExactLeakage.cs")
			};
			foreach (string scan in pure) Assert.AreEqual(0, MintingReads(scan));
			Assert.AreEqual(1, MintingReads(Source("Growth",
				"KingdomDelveLink.04.ReceiptAndEndpointCustody.cs")),
				"new paired endpoint publication only");
			Assert.AreEqual(1, MintingReads(Source("Growth",
				"KingdomPlot2.34.EffectsAndFurnishing.cs")),
				"new furnishing publication only");
			Assert.AreEqual(2, MintingReads(Source("Trade",
				"KingdomTrade.16.ProjectionMutation.cs")),
				"new caravan publication only");
		}

#if !TAF_CONSTRUCTION_INPUT_PORTABLE
		private static int NativeSequence;

		private sealed class NativeIdentityDecoy
		{
			internal int BaseId;
			internal string IdProperty;

			internal NativeIdentityDecoy(string assigned = null)
			{
				IdProperty = assigned;
			}

			internal string IDIfAssigned { get { return IdProperty; } }

			internal string ID
			{
				get
				{
					if (IdProperty == null)
					{
						BaseId = ++NativeSequence;
						IdProperty = BaseId.ToString();
					}
					return IdProperty;
				}
			}
		}

		private sealed class LookupRow
		{
			internal NativeIdentityDecoy Object;
			internal string Topology;
		}

		[Test]
		public void NativeDecoysBeforeTargetKeepBaseIdPropertyAndSequencePure()
		{
			const string targetId = "prepared-target";
			NativeSequence = 70;
			NativeIdentityDecoy root = new NativeIdentityDecoy();
			NativeIdentityDecoy nested = new NativeIdentityDecoy();
			LookupRow target = new LookupRow
			{
				Object = new NativeIdentityDecoy(targetId), Topology = "root"
			};
			List<LookupRow> loaded = new List<LookupRow>
			{
				new LookupRow { Object = root, Topology = "root" },
				new LookupRow { Object = nested, Topology = "nested-inventory" },
				target
			};

			LookupRow exact;
			Assert.AreEqual(KingdomTradeExactLookup.ExactUnique,
				KingdomTradeRules.ResolveExactUnique(loaded, targetId,
					row => row.Object.IDIfAssigned, out exact));
			Assert.AreSame(target, exact);
			Assert.AreEqual(KingdomTradeExactLookup.Missing,
				KingdomTradeRules.ResolveExactUnique(loaded, "missing-target",
					row => row.Object.IDIfAssigned, out exact));
			loaded.Add(new LookupRow
			{
				Object = new NativeIdentityDecoy(targetId), Topology = "nested-inventory"
			});
			Assert.AreEqual(KingdomTradeExactLookup.Ambiguous,
				KingdomTradeRules.ResolveExactUnique(loaded, targetId,
					row => row.Object.IDIfAssigned, out exact));
			Assert.IsNull(exact);
			Assert.AreEqual(70, NativeSequence);
			Assert.AreEqual(0, root.BaseId);
			Assert.AreEqual(0, nested.BaseId);
			Assert.IsNull(root.IdProperty);
			Assert.IsNull(nested.IdProperty);
		}

		[Test]
		public void NativeDecoyWrongGetterTripsMutationTrap()
		{
			NativeSequence = 10;
			NativeIdentityDecoy decoy = new NativeIdentityDecoy();
			Assert.AreEqual("11", decoy.ID);
			Assert.AreEqual(11, decoy.BaseId);
			Assert.AreEqual("11", decoy.IdProperty);
			Assert.AreEqual(11, NativeSequence);
		}

		[Test]
		public void NativePreviewListAndCancelSkipUnassignedWithoutMutation()
		{
			NativeSequence = 90;
			NativeIdentityDecoy root = new NativeIdentityDecoy();
			NativeIdentityDecoy nested = new NativeIdentityDecoy();
			List<NativeIdentityDecoy> candidates = new List<NativeIdentityDecoy> {
				root, new NativeIdentityDecoy("b"), nested, new NativeIdentityDecoy("a")
			};
			List<NativeIdentityDecoy> preview = candidates.FindAll(
				candidate => !string.IsNullOrEmpty(candidate.IDIfAssigned));
			preview.Sort((a, b) => string.CompareOrdinal(a.IDIfAssigned, b.IDIfAssigned));
			Assert.AreEqual(new[] { "a", "b" }, preview.ConvertAll(x => x.IDIfAssigned));
			// Cancelling the preview performs no committed identity boundary.
			Assert.AreEqual(90, NativeSequence);
			Assert.AreEqual(0, root.BaseId);
			Assert.AreEqual(0, nested.BaseId);
			Assert.IsNull(root.IdProperty);
			Assert.IsNull(nested.IdProperty);
		}
#endif
	}
}
#endif
