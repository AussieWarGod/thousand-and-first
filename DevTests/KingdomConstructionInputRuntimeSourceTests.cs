#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomConstructionInputRuntimeSourceTests
	{
		private static readonly string[] RuntimePaths = new[]
		{
			"Growth/KingdomConstruction.InputDrive.Open.cs",
			"Growth/KingdomConstruction.InputDrive.Cas.cs",
			"Growth/KingdomConstruction.InputDrive.Source.cs",
			"Growth/KingdomConstruction.InputDrive.SourcePreflight.cs",
			"Growth/KingdomConstruction.InputDrive.SourceManifest.cs",
			"Growth/KingdomConstruction.InputDrive.SourcePhysical.cs",
			"Growth/KingdomConstruction.InputDrive.SourceSplitRecovery.cs",
			"Growth/KingdomConstruction.InputDrive.Water.cs",
			"Growth/KingdomConstruction.InputDrive.Arrival.cs",
			"Growth/KingdomConstruction.InputDrive.Debit.cs",
			"Growth/KingdomConstruction.InputDrive.DebitManifest.cs",
			"Growth/KingdomConstruction.InputDrive.Close.cs",
			"Growth/KingdomConstruction.InputDrive.Cancellation.cs",
			"Growth/KingdomConstruction.InputDrive.Cancellation.Manifest.cs",
			"Growth/KingdomConstruction.InputDrive.Cancellation.Water.cs",
			"Growth/KingdomConstruction.InputDrive.Cancellation.Partition.cs",
			"Growth/KingdomConstruction.InputDrive.Cancellation.Provenance.cs",
			"Growth/KingdomConstruction.InputDrive.Cancellation.Split.cs",
			"Growth/KingdomConstruction.InputDrive.RemainderRelease.cs",
			"Simulation/City/KingdomCentralLogistics.15.ConstructionInputTargetCarrierAccess.cs",
			"Simulation/City/KingdomCentralLogistics.16.ConstructionInputMasterPause.cs",
			"Simulation/City/KingdomCentralLogistics.17.ConstructionInputOrphanRecovery.cs",
			"Simulation/City/KingdomCentralLogistics.19.ConstructionInputTransitCustody.cs",
			"Simulation/City/KingdomCentralLogistics.20.ConstructionInputCancellationSource.cs",
			"Simulation/City/KingdomCentralLogistics.21.ConstructionInputRetirement.cs",
			"Simulation/City/KingdomCentralLogistics.22.ConstructionInputCancellationManifest.cs",
			"Simulation/City/KingdomCentralLogistics.23.ConstructionInputRootedPickup.cs",
			"Simulation/City/KingdomCentralLogistics.24.ConstructionInputCancellationTargetCut.cs",
			"Simulation/City/KingdomCentralLogistics.25.ConstructionInputPendingRetirement.cs",
			"Simulation/City/KingdomCentralLogistics.26.ConstructionInputExileBindingGate.cs",
			"Growth/KingdomConstruction.LostAuthorityRecovery.cs",
			"Growth/KingdomConstruction.GlobalRecovery.cs"
		};

		[Test]
		public void LocalRefusalFallsBackToOnePublishedRealmReceipt()
		{
			string funding = Read("Growth/KingdomConstruction.Funding.cs");
			StringAssert.Contains("Material.FrozenRequiredItemId", funding);
			StringAssert.Contains("TryBeginRoutedFunding(Job", funding);
			string open = Read(RuntimePaths[0]);
			Ordered(open, "TryPrepareRoutedInputReceipt", "UpdateInputReceipt",
				"TryPublish(adopted", "TryActivatePreparedRoutedInput",
				"KingdomConstructionInputTxPhase.Reserved");
			StringAssert.Contains("TryCancelPreparedRoutedInput", open);
			StringAssert.Contains("IsCurrent(current)", open);
		}

		[Test]
		public void SplitAndWaterMutationHaveDurableBeforeAfterIntents()
		{
			string split = Read("Growth/KingdomConstruction.InputDrive.SourcePhysical.cs");
			Ordered(split, "string.IsNullOrEmpty(source.BeforeWitnessHash)",
				"string.IsNullOrEmpty(source.AfterWitnessHash)",
				"DecidePhysicalMutation", "item.SplitStack(source.Take");
			StringAssert.Contains("NoRemove: true", split);
			StringAssert.Contains("source.RemainderObjectId != remainder.ID", split);
			StringAssert.Contains("NoStack: true", split);
			string water = Read("Growth/KingdomConstruction.InputDrive.Water.cs");
			Ordered(water, "KingdomConstructionInputCargoPhase.CreateIntent",
				"GameObject.Create(cargo.Blueprint)", "string.IsNullOrEmpty(cargo.ObjectId)",
				"KingdomConstructionInputCargoPhase.AtSource");
			Ordered(water, "string.IsNullOrEmpty(source.BeforeWitnessHash)",
				"string.IsNullOrEmpty(source.AfterWitnessHash)",
				"DecidePhysicalMutation", "target.MixWith(sourceLiquid");
			StringAssert.Contains("UseTempSplit: true", water);
			StringAssert.Contains("AddObjectToInventory(item", split);
			Ordered(split, "AddObjectToInventory(item", "atHolder = ReferenceEquals",
				"atCarrier = ReferenceEquals");
		}

		[Test]
		public void RoutedPhysicalCallbacksReproveAuthorityAndStackProvenance()
		{
			string split = Read("Growth/KingdomConstruction.InputDrive.SourcePhysical.cs");
			Ordered(split, "item.SetStringProperty(InputMarkerProperty, source.RemainderMarker);",
				"if (ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source,",
				"remainder = item.SplitStack(source.Take",
				"KingdomPurpose.HasProtectedCargoEvidence(item)",
				"ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source,");
			Ordered(split, "if (!protectedCargo) item.SetIntProperty(\"NeverStack\", 1);",
				"item.SetStringProperty(InputMarkerProperty, cargo.CargoKey);",
				"if (ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source,",
				"accepted = carrier.Inventory.AddObjectToInventory(item",
				"ExactRoutedMaterialAtCarrier(zone, carrier, item, job, receipt, source,");
			StringAssert.Contains("int routeNeverStack = protectedCargo ? -1 : 1;", split);
			StringAssert.Contains("cargo.CargoKey, routeNeverStack", split);
			string preflight = Read("Growth/KingdomConstruction.InputDrive.SourcePreflight.cs");
			StringAssert.Contains("protectedCargo || item.GetIntProperty(\"NeverStack\") == 1",
				preflight);
			string manifest = Read("Growth/KingdomConstruction.InputDrive.SourceManifest.cs");
			Ordered(manifest, "KingdomOrdinaryCustody.TryCollect(carrier",
				"source.Phase == KingdomConstructionInputSourcePhase.Debited",
				"ExactCurrentPickupCut", "graph.Count == expected + 1");
			StringAssert.Contains("ExactLoadedPickupCargo", manifest);
			Ordered(split, "ExactSourcePickupManifest(zone, carrier, job, receipt, callbackCargo",
				"item.SetStringProperty(InputMarkerProperty, source.RemainderMarker);",
				"ExactSourcePickupManifest(zone, carrier, job, receipt, callbackCargo");

			string water = Read("Growth/KingdomConstruction.InputDrive.Water.cs");
			StringAssert.Contains("KingdomPurpose.HasProtectedCargoEvidence(vessel)", water);
			Ordered(water, "cask.SetIntProperty(\"NeverStack\", 1);",
				"ExactNewRoutedInputCask(cask, job, receipt, cargo)",
				"carrier.Inventory.AddObject(cask");
			Ordered(water, "private static bool ExactNewRoutedInputCask(",
				"RoutedInputItemAuthorized(job, receipt, cask)");
			water = water.Substring(water.IndexOf(
				"private static bool DriveInputWaterPour(", StringComparison.Ordinal));
			Ordered(water, "if (!KingdomMaster.NewWorkAllowed(system)) return false;",
				"!ExactSourcePickupManifest(zone, carrier, job, receipt, cargo, source)",
				"!ExactRoutedInputWaterSource(zone, source, vesselObject, sourceLiquid,",
				"!ExactRoutedInputCask(carrier, cask, job, receipt, cargo, 0)",
				"target.MixWith(sourceLiquid",
				"!ExactRoutedInputWaterSource(zone, source, vesselObject, sourceLiquid,",
				"!ExactRoutedInputCask(carrier, cask, job, receipt, cargo, source.Take)");
			string remainder = Read(
				"Growth/KingdomConstruction.InputDrive.RemainderRelease.cs");
			StringAssert.Contains("ExactRoutedSplitRemainderState(active, holder", remainder);
			remainder = remainder.Substring(remainder.IndexOf(
				"private static bool ExactRoutedSplitRemainderState", StringComparison.Ordinal));
			Ordered(remainder, "source.HolderId",
				"ReferenceEquals(remainder.InInventory, holder)",
				"MaterialStockpiles[source.DedicationOrdinal]",
				"remainder.GetIntProperty(\"NeverStack\") == expectedNeverStack",
				"!KingdomPurpose.HasProtectedCargoEvidence(remainder)",
				"KingdomOrdinaryCustody.TryProveEmpty(remainder",
				"TryInputClassification(remainder");
			StringAssert.DoesNotContain("ExactInputDedication", remainder);

			string cancel = Read("Growth/KingdomConstruction.InputDrive.Cancellation.Split.cs");
			cancel = cancel.Substring(cancel.IndexOf(
				"private static bool RestoreCancelledSplit(", StringComparison.Ordinal));
			// Both destructive callbacks reprove authority and exact routed material at the
			// holder immediately before they run: the remainder obliteration, then the count
			// restoration. Neither may fire on stale evidence.
			Ordered(cancel,
				"if (!ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source,",
				"|| !ExactRoutedSplitRemainder(zone, holder, job, receipt, source, remainder))",
				"if (!KingdomMaster.NewWorkAllowed(system)) return false;",
				"if (!ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source,",
				"|| !ExactRoutedSplitRemainder(zone, holder, job, receipt, source, remainder)",
				"remainder.Obliterate", "FindGlobalInputId(receipt, source.RemainderObjectId",
				"if (!KingdomMaster.NewWorkAllowed(system)",
				"|| !ExactRoutedMaterialAtHolder(zone, holder, item, job, receipt, source,",
				"item.Count = source.Before");

			string close = Read("Growth/KingdomConstruction.InputDrive.Close.cs");
			// Terminal release drops only the policy this route set: protected cargo keeps its
			// own NeverStack, and the remainder is proved route-owned before and after the cut.
			Ordered(close, "bool protectedCargo = KingdomPurpose.HasProtectedCargoEvidence(source);",
				"source.RemoveStringProperty(InputMarkerProperty);",
				"if (!protectedCargo) source.RemoveIntProperty(\"NeverStack\");");
			Ordered(close, "if (ExactRoutedSplitRemainderState(zone, holder, job, receipt, line,",
				"remainder, line.RemainderMarker, 1))",
				"remainder.RemoveStringProperty(InputMarkerProperty);",
				"remainder.RemoveIntProperty(\"NeverStack\");",
				"return ExactRoutedSplitRemainderState(zone, holder, job, receipt, line,",
				"remainder, null, 0);");
		}

		[Test]
		public void WaterDebitAndCancellationReproveBeforeEveryDestructiveCallback()
		{
			string debit = Read("Growth/KingdomConstruction.InputDrive.Debit.cs");
			StringAssert.Contains("ExactConsumedCargoEvidence", debit);
			StringAssert.Contains("ExactDebitChildManifest", debit);
			StringAssert.DoesNotContain("QuarantineInput", debit);
			Ordered(debit, "state == KingdomPhysicalLookupState.Exact && graveyard",
				"ExactConsumedCargoEvidence", "InputWitness(\"consume\"");
			string manifest = Read("Growth/KingdomConstruction.InputDrive.DebitManifest.cs");
			StringAssert.Contains("KingdomOrdinaryCustody.TryCollect", manifest);
			StringAssert.Contains("graph.Count != active + 1", manifest);
			StringAssert.Contains("ExactConsumedCargoEvidence", manifest);
			Ordered(debit, "if (!ExactDebitChildManifest(system, target, carrier, job, receipt, cargo)",
				"|| !ExactInputCargo(target, carrier, job, receipt, cargo, out exact))",
				"Routed cargo changed immediately before its exact debit callback.",
				"KingdomLiquids.Drain(liquid, cargo.Amount)",
				"ExactInputCargo(target, carrier, job, receipt, cargo, 0, out exact)",
				"exact.Obliterate");
			string cancel = Read("Growth/KingdomConstruction.InputDrive.Cancellation.Water.cs");
			Ordered(cancel, "if (receipt.Paused || !KingdomMaster.NewWorkAllowed(system))",
				"if (!ExactRoutedInputWaterSource(zone, source, vessel, sourceLiquid,",
				"!ExactCancelledWaterCask(job, receipt, cargo, carrier, cask, caskLiquid,",
				"sourceLiquid.MixWith(caskLiquid",
				"!ExactRoutedInputWaterSource(zone, source, vessel, sourceLiquid,",
				"!ExactCancelledWaterCask(job, receipt, cargo, carrier, cask, caskLiquid, 0)",
				"if (!KingdomMaster.NewWorkAllowed(system)) return false;",
				"!ExactCancelledWaterCask(job, receipt, cargo, carrier, cask, caskLiquid, 0)",
				"cask.Obliterate");
		}

		[Test]
		public void PreparedCarrierAndMasterPauseCannotAdvanceRouteTime()
		{
			string render = Read("Simulation/City/KingdomPorters.02.CarrierRendering.cs");
			StringAssert.Contains("KingdomDeliveryPhase.SourceDebitPrepared", render);
			StringAssert.Contains("part.DestX = row.DeliverySourceX", render);
			StringAssert.Contains("KingdomOrdinaryCustody.TryProveEmpty(body", render);
			string dedicated = Read(
				"Simulation/City/KingdomPorters.02a.ConstructionInputSourceRendering.cs");
			Ordered(dedicated, "TryProveConstructionInputSourceRow", "KingdomResidents.Judge",
				"Mint(system", "TryResolveConstructionInputSourceCarrier",
				"!minted || KingdomOrdinaryCustody.TryProveEmpty");
			string handoff = Read(
				"Simulation/City/KingdomPorters.03.ClosingAndCustodyHandoff.cs");
			StringAssert.Contains("fix = new KingdomItineraryFix", handoff);
			StringAssert.Contains("row.DeliverySourceX", handoff);
			string step = Read(
				"Simulation/City/KingdomPorters.01.RenderingSteppingAndRetirement.cs");
			// A prepared construction-input carrier leaves Step before any movement or handoff:
			// both the durable row and the active trip row refuse ahead of the arrival test.
			Ordered(step, "if (KingdomJobRules.IsCentralDelivery(row))",
				"== KingdomDeliveryCargoAuthority.ConstructionInput) return;",
				"if (!TryActiveTripRow(table, Part.JobId, TimeTick, out active, out centralFix)) return;",
				"== KingdomDeliveryCargoAuthority.ConstructionInput) return;",
				"if (Near(Body, Part.DestX");

			string master = Read("Core/KingdomMaster.cs") + "\n"
				+ Read("Core/KingdomMaster.ResumeAtomicity.cs");
			Ordered(master, "KingdomConstruction.TryPrepareMasterResume",
				"TryPrepareConstructionInputMasterResume");
			Ordered(master, "Sources.JobsMatch(System, ConstructionRoutes)",
				"System.Jobs.PublishPrevalidated(ConstructionRoutes)");
			string routePause = Read(
				"Simulation/City/KingdomCentralLogistics.16.ConstructionInputMasterPause.cs");
			StringAssert.Contains("target.DesiredArrivalTick - last.ArriveTick", routePause);
			StringAssert.Contains("if (delta == 0L) continue", routePause);
			string arrival = Read("Growth/KingdomConstruction.InputDrive.Arrival.cs");
			StringAssert.DoesNotContain("QuarantineInput", arrival);
			StringAssert.Contains("TryEffectiveArrivalTick", arrival);
			StringAssert.Contains("receipt.PausedTicks", arrival);
		}

		[Test]
		public void EachSourceChildRootsBeforeTheNextVisitedSource()
		{
			string source = Read("Growth/KingdomConstruction.InputDrive.Source.cs");
			Ordered(source, "for (int i = 0; i < receipt.ChildCount; i++)",
				"source.Phase == KingdomConstructionInputSourcePhase.Debited",
				"cargo.Phase == KingdomConstructionInputCargoPhase.PickupIntent",
				"TryRootConstructionInputTransitCarrier", "TryAcknowledgeConstructionInputPickup",
				"InputChildEvidence");
			string transit = Read(
				"Simulation/City/KingdomCentralLogistics.19.ConstructionInputTransitCustody.cs");
			StringAssert.Contains("row.DeliveryPhase != KingdomDeliveryPhase.InFlight", transit);
			Ordered(transit, "TryExactTransitRoot(ownerOperationId, tripId, out carrier)",
				"if (row.DeliveryPhase == KingdomDeliveryPhase.InFlight)");
		}

		[Test]
		public void ArrivalRebindRecoversRootedPlacementBeforeBindCrashCut()
		{
			string central = Read(
				"Simulation/City/KingdomCentralLogistics.11.ConstructionInputArrivalAndAcknowledgements.cs");
			Ordered(central, "GameObject rooted", "GameObject targetBody",
				"!ReferenceEquals(rooted, targetBody)",
				"GameObject body = GameObject.Validate(targetBody)",
				"target.AddObject(body", "KingdomResidents.Bind(system, tripId",
				"ReleaseTransitRoot(ownerOperationId");
			StringAssert.DoesNotContain("GetZone(", central);
			StringAssert.DoesNotContain("SystemLongDistanceMoveTo", central);
		}

		[Test]
		public void RealmFundingEntryPointsDoNotPreRejectLocalShortfalls()
		{
			string[] paths =
			{
				"Growth/KingdomCommission.cs", "Growth/KingdomPlot2.10.Commission.cs",
				"Growth/KingdomSocket.05.ConversionPreparation.cs",
				"Growth/KingdomSocket.09.SocketBuildExecution.cs"
			};
			for (int i = 0; i < paths.Length; i++)
			{
				string source = Read(paths[i]);
				StringAssert.DoesNotContain("KingdomGrowth.CountStoredWater", source, paths[i]);
				StringAssert.DoesNotContain("KingdomMaterials.CanPay", source, paths[i]);
				StringAssert.Contains("KingdomConstruction.TryFundNew", source, paths[i]);
			}
		}

		[Test]
		public void CentralOwnsOnlyRouteAndSameBodyWhileParentOwnsObjects()
		{
			string sources = Read("Growth/KingdomConstruction.InputDrive.Source.cs");
			StringAssert.Contains("TryResolveConstructionInputSourceCarrier", sources);
			StringAssert.Contains("TryAcknowledgeConstructionInputPickup", sources);
			string arrival = Read("Growth/KingdomConstruction.InputDrive.Arrival.cs");
			Ordered(arrival, "TryMaterializeConstructionInputArrival",
				"TryResolveConstructionInputTargetCarrier", "ExactInputCargo",
				"KingdomConstructionInputCargoPhase.Landed",
				"TryAcknowledgeConstructionInputLanded");
			StringAssert.Contains("KingdomConstructionInputTopology.LandingEscrow", arrival);
			string all = sources + arrival;
			StringAssert.DoesNotContain("MaterialStock.Put", all);
			StringAssert.DoesNotContain("ReserveComposite", all);
		}

		[Test]
		public void PhysicalConsumptionClosesBeforeAtomicFundingProjection()
		{
			string debit = Read("Growth/KingdomConstruction.InputDrive.Debit.cs");
			Ordered(debit, "KingdomConstructionInputCargoPhase.DebitIntent",
				"string.IsNullOrEmpty(cargo.BeforeWitnessHash)",
				"string.IsNullOrEmpty(cargo.AfterWitnessHash)",
				"DecidePhysicalMutation", "KingdomLiquids.Drain",
				"KingdomConstructionInputCargoPhase.Spent");
			StringAssert.Contains("exact.Obliterate", debit);
			string close = Read("Growth/KingdomConstruction.InputDrive.Close.cs");
			Ordered(close, "TryCloseConstructionInputTrip", "TryCommittedClaims",
				"KingdomConstructionInputTxPhase.Committed",
				"KingdomConstructionPhase.Funded", "TryUpdate(next",
				"ReleaseInputRemainders");
			StringAssert.DoesNotContain("QuarantineInput", close);
			for (int i = 0; i < RuntimePaths.Length; i++)
				StringAssert.DoesNotContain("QuarantineInput", Read(RuntimePaths[i]), RuntimePaths[i]);
		}

		[Test]
		public void GlobalRecoveryIsBoundedSemanticOnlyAndNeverLoadsGround()
		{
			string events = Read("Core/KingdomSystem.z20.Events.cs");
			Ordered(events, "Guard(\"pump\"", "KingdomConstruction.OnGlobalRecoveryPass(this)",
				"KingdomHeartbeat.OnEndTurn");
			string recovery = Read("Growth/KingdomConstruction.GlobalRecovery.cs");
			StringAssert.Contains("MaxGlobalInputReceiptsPerTurn", recovery);
			StringAssert.Contains("SourcesRemainClaimed", recovery);
			StringAssert.Contains("TrySweepOrphanedConstructionInputReservations", recovery);
			StringAssert.Contains("Cancel(ref job", recovery);
			StringAssert.DoesNotContain("DriveRoutedInput", recovery);
			StringAssert.DoesNotContain("GetZone(", recovery);
			StringAssert.DoesNotContain("GetObjects(", recovery);
			StringAssert.DoesNotContain("OnSettlementPass", recovery);
		}

		[Test]
		public void LostAuthorityRecoverySkipsNoopsAndWrongPartitionsFairly()
		{
			string global = Read("Growth/KingdomConstruction.GlobalRecovery.cs");
			Ordered(global, "if (KingdomConstructionInputRules.IsTerminal(receipt))",
				"Quarantine(ref job", "attended++;");
			Ordered(global, "if (!authorized", "Cancel(ref job", "attended++;");
			string attended = Read("Growth/KingdomConstruction.LostAuthorityRecovery.cs");
			Ordered(attended, "LostAuthorityPartitionActionable", "continue;",
				"PublishLostAuthorityCursor(job.Id)", "attended++;", "DriveRoutedInput");
			StringAssert.Contains("LostAuthorityCursorKey", attended);
			Ordered(attended, "TryReadLostAuthorityCursor", "int start = 0",
				"(start + offset) % jobs.Count");
			StringAssert.Contains("CancellationTargetPartitionRequired", attended);
			StringAssert.Contains("NextCancellationSourceOrdinal", attended);
			Ordered(attended, "InputCommitBoundaryWon(receipt)", "if (!cancellation && !commitWon)",
				"if (!Cancel(ref job", "continue;");
			StringAssert.Contains("KingdomConstructionInputCargoPhase.DebitIntent", attended);
			StringAssert.Contains("KingdomConstructionInputCargoPhase.Spent", attended);
			StringAssert.DoesNotContain("DriveRoutedInput(system, zone, ref job, out _);\n"
				+ "\t\t\t\t\treturn;", attended);
		}

		[Test]
		public void CancellationAndPickupCutsKeepExactDurableCustody()
		{
			string cancel = Read("Growth/KingdomConstruction.InputDrive.Cancellation.cs");
			string partition = Read(
				"Growth/KingdomConstruction.InputDrive.Cancellation.Partition.cs");
			Ordered(cancel, "CancellationTargetPartitionRequired", "receipt.TargetZoneId",
				"TryRetractConstructionInputTargetCarrier", "return false;");
			StringAssert.Contains("ConstructionInputTransitRootSettled", partition);
			StringAssert.Contains("arrivalCut != KingdomPhysicalLookupState.Absent", partition);
			string targetCut = Read(
				"Simulation/City/KingdomCentralLogistics.24.ConstructionInputCancellationTargetCut.cs");
			Ordered(targetCut, "LookupTransitRoot(owner, tripId", "root == KingdomPhysicalLookupState.Exact",
				"body.CurrentZone != liveTarget", "body.TryRemoveFromContext()",
				"TryExactConstructionInputTransitCarrier");
			StringAssert.Contains("KingdomDeliveryPhase.SourceDebitPrepared", targetCut);
			StringAssert.Contains("binding.ZoneId == row.SourceZoneId", targetCut);
			StringAssert.Contains("body.CurrentZone.ZoneID == row.SourceZoneId", targetCut);
			string materialize = Read(
				"Simulation/City/KingdomCentralLogistics.20.ConstructionInputCancellationSource.cs");
			StringAssert.Contains("binding.ZoneId != liveSource.ZoneID", materialize);
			StringAssert.Contains(
				"binding.ZoneId != row.SourceZoneId && binding.ZoneId != row.DestZoneId",
				materialize);
			StringAssert.DoesNotContain("receiptSchema != 2", materialize);
			string cancelManifest = Read(
				"Growth/KingdomConstruction.InputDrive.Cancellation.Manifest.cs");
			StringAssert.Contains("TryInspectConstructionInputCancellationCarrier", cancelManifest);
			StringAssert.Contains("ReferenceEquals(exact, carrier)", cancelManifest);
			Ordered(cancel, "TryMaterializeConstructionInputCancellationSource",
				"ExactCancellationCarrierManifest", "PrepareCancellationCargo");
			string pickup = Read("Growth/KingdomConstruction.InputDrive.Source.cs");
			Ordered(pickup, "TryResolveConstructionInputRootedPickup",
				"TryAcknowledgeConstructionInputPickup", "InputChildEvidence");
			string rooted = Read(
				"Simulation/City/KingdomCentralLogistics.23.ConstructionInputRootedPickup.cs");
			Ordered(rooted, "LookupTransitRoot", "KingdomPhysicalLookupState.Ambiguous",
				"TripRows(jobs, tripId).Count != 1", "binding.ObjectId != body.IDIfAssigned",
				"ExactConstructionInputTransitManifest");
			StringAssert.Contains("binding.ZoneId != row.SourceZoneId", rooted);
			string arrival = Read(
				"Simulation/City/KingdomCentralLogistics.11.ConstructionInputArrivalAndAcknowledgements.cs");
			StringAssert.Contains(
				"binding.ZoneId != row.SourceZoneId && binding.ZoneId != row.DestZoneId", arrival);
			string transit = Read(
				"Simulation/City/KingdomCentralLogistics.19.ConstructionInputTransitCustody.cs");
			StringAssert.Contains(
				"binding.ZoneId != row.SourceZoneId && binding.ZoneId != row.DestZoneId", transit);
			StringAssert.Contains("binding.ZoneId != row.SourceZoneId", transit);
			StringAssert.Contains("binding.ZoneId == row.SourceZoneId", transit);
			string physical = Read("Growth/KingdomConstruction.InputDrive.SourcePhysical.cs");
			Ordered(physical, "if (atHolder && !atCarrier)",
				"ExactLiveRoutedSplitRemainder(zone, holder", "item.SetStringProperty",
				"ExactLiveRoutedSplitRemainder(zone, holder",
				"AddObjectToInventory(item");
			Ordered(cancel, "if (!ReferenceEquals(item.InInventory, holder)",
				"ExactLiveRoutedSplitRemainder(zone, holder", "AddObjectToInventory(item");
		}

		[Test]
		public void CancellationDestructionAndRetirementNeverGuessPlainAbsence()
		{
			string water = Read("Growth/KingdomConstruction.InputDrive.Cancellation.Water.cs");
			StringAssert.Contains("caskState != KingdomPhysicalLookupState.Exact || !graveyard",
				water);
			string split = Read("Growth/KingdomConstruction.InputDrive.Cancellation.Split.cs");
			StringAssert.Contains("state == KingdomPhysicalLookupState.Exact && graveyard", split);
			StringAssert.Contains("ExactGraveyardSplitRemainder", split);
			StringAssert.Contains("TryProveRetiredEmpty(remainder", split);
			StringAssert.Contains("TryProveRetiredEmpty(exact",
				Read("Growth/KingdomConstruction.InputDrive.Debit.cs"));
			StringAssert.Contains("TryProveRetiredEmpty(cask", water);
			StringAssert.Contains("TryProveRetiredEmpty(exact", Read(
				"Simulation/City/KingdomCentralLogistics.22.ConstructionInputCancellationManifest.cs"));
			string retirement = Read(
				"Simulation/City/KingdomCentralLogistics.21.ConstructionInputRetirement.cs");
			StringAssert.Contains("LookupRetirement", retirement);
			Ordered(retirement, "binding.ZoneId != zone.ZoneID", "LookupRetirement",
				"PublishRetirement(owner, tripId");
			StringAssert.Contains("ExactRetirementMarker", retirement);
			StringAssert.Contains("ExactRetiredCarrierEvidence(objectId, tripId, null)", retirement);
			StringAssert.Contains("exact.GetIntProperty(KingdomResidents.JobIdProperty) == tripId",
				retirement);
			Ordered(retirement, "internal static bool TryClearConstructionInputRetirement",
				"KingdomConstructionInputRules.IsTerminal", "bindings.Holds",
				"LookupTransitRoot", "DeliveryCargoAuthority.ConstructionInput",
				"ClearRetirement(owner, tripId)");
			StringAssert.DoesNotContain("ReadRetirement(owner, tripId)?.StartsWith", retirement);
		}

		[Test]
		public void CancellationAdoptsOrProvesAbsentPreBindMintOnlyAtItsSource()
		{
			string pending = Read(
				"Simulation/City/KingdomCentralLogistics.25.ConstructionInputPendingRetirement.cs");
			Ordered(pending, "ExactCancellationRetirementRow", "LookupTransitRoot",
				"CountExactPendingCancellationBodies", "LookupRetirement",
				"KingdomResidents.Bind", "TryRetireActiveConstructionInputCarrier");
			StringAssert.Contains("KingdomOrdinaryCustody.TryProveEmpty(body", pending);
			StringAssert.Contains("body.CurrentCell == source.GetCell(x, y)", pending);
			StringAssert.Contains("PublishRetirement(owner, tripId, UnprojectedState", pending);
			StringAssert.Contains("row.DeliveryPhase != KingdomDeliveryPhase.SourceDebitPrepared",
				pending);
			StringAssert.Contains("KingdomConstructionInputCargoPhase.Planned", pending);
			StringAssert.Contains("KingdomConstructionInputSourcePhase.Reserved", pending);
			StringAssert.Contains("CountGraveyardTripBodies(tripId) == 0", pending);
			StringAssert.DoesNotContain("Mint(", pending);
			string close = Read(
				"Simulation/City/KingdomCentralLogistics.12.ConstructionInputRecovery.cs");
			StringAssert.Contains("bool adoptedNeutral", close);
			StringAssert.Contains("ExactNeverProjectedCancellation(receipt, expectedChild, rows[i])",
				close);
			StringAssert.Contains("ExactUnprojectedMarker(ownerOperationId, trips[i])", close);
			string retirement = Read(
				"Simulation/City/KingdomCentralLogistics.21.ConstructionInputRetirement.cs");
			StringAssert.Contains("ExactUnprojectedMarker(owner, tripId)", retirement);
		}

		[Test]
		public void ExileRefusesEveryRowlessOrMalformedTransientBinding()
		{
			string gate = Read(
				"Simulation/City/KingdomCentralLogistics.26.ConstructionInputExileBindingGate.cs");
			Ordered(gate, "bindings.TryAt", "KingdomBindingKind.Transient",
				"binding.BindingKey <= 0", "row.JobId != binding.BindingKey",
				"if (!attributed) return true;");
			StringAssert.Contains("AnyUnattributedTransientBinding(system, jobs)", Read(
				"Simulation/City/KingdomCentralLogistics.21.ConstructionInputRetirement.cs"));
		}

		[Test]
		public void CancellationReleasesEachSourceMarkerBeforeItsTerminalPhase()
		{
			string split = Read("Growth/KingdomConstruction.InputDrive.Cancellation.Split.cs");
			Ordered(split, "source.Phase == KingdomConstructionInputSourcePhase.RestoreIntent",
				"ReleaseInputLineMarkers", "KingdomConstructionInputSourcePhase.Restored");
			Ordered(split, "source.Phase == KingdomConstructionInputSourcePhase.CompensationIntent",
				"ReleaseInputLineMarkers", "KingdomConstructionInputSourcePhase.Compensated");
			string close = Read("Growth/KingdomConstruction.InputDrive.Close.cs");
			Ordered(close, "private static bool ReleaseInputLineMarkers",
				"ExactRoutedMaterialAtHolder(zone, holder, source",
				"source.RemoveStringProperty",
				"source.RemoveIntProperty",
				"ExactRoutedMaterialAtHolder(zone, holder, source");
			StringAssert.Contains("ExactRoutedSplitRemainderState(zone, holder", close);
			string provenance = Read(
				"Growth/KingdomConstruction.InputDrive.Cancellation.Provenance.cs");
			StringAssert.Contains("int ordinaryPolicy = protectedCargo ? -1 : 0", provenance);
			StringAssert.Contains("KingdomConstructionInputSourcePhase.CompensationIntent", provenance);
			string cancel = Read("Growth/KingdomConstruction.InputDrive.Cancellation.cs");
			Ordered(cancel, "case KingdomConstructionInputCargoPhase.DebitIntent:",
				"next = KingdomConstructionInputCargoPhase.CompensationIntent");
		}

		[Test]
		public void RuntimeShardsStayBoundedAndAvoidAbstractStockMutation()
		{
			for (int i = 0; i < RuntimePaths.Length; i++)
			{
				string source = Read(RuntimePaths[i]);
				Assert.LessOrEqual(source.Split('\n').Length, 300, RuntimePaths[i]);
				StringAssert.DoesNotContain("MaterialStock.Put", source, RuntimePaths[i]);
			}
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }

		private static void Ordered(string source, params string[] markers)
		{
			int at = -1;
			for (int i = 0; i < markers.Length; i++)
			{
				int next = source.IndexOf(markers[i], at + 1, StringComparison.Ordinal);
				Assert.Greater(next, at, markers[i]);
				at = next;
			}
		}
	}
}
#endif
