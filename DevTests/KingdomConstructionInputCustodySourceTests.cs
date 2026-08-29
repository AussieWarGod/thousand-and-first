#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomConstructionInputCustodySourceTests
	{
		private static readonly string[] NoLoadPaths =
		{
			"Growth/KingdomConstruction.InputPlannerAuthority.cs",
			"Growth/KingdomConstruction.InputPlannerScan.cs",
			"Growth/KingdomConstruction.InputPlannerReservation.cs",
			"Growth/KingdomConstructionInputLeaseAuthority.cs",
			"Growth/KingdomConstruction.GlobalRecovery.cs",
			"Simulation/City/KingdomCentralLogistics.03.ManifestPlanningAndReservation.cs",
			"Simulation/City/KingdomCentralLogistics.09.ConstructionInputReservation.cs",
			"Simulation/City/KingdomCentralLogistics.18.ConstructionInputObservedRoute.cs"
		};

		[Test]
		public void PlanningAllowanceAndGlobalAuditHaveNoGroundLoadCallGraph()
		{
			for (int i = 0; i < NoLoadPaths.Length; i++)
			{
				string source = Read(NoLoadPaths[i]);
				StringAssert.DoesNotContain("GetZone(", source, NoLoadPaths[i]);
				StringAssert.DoesNotContain("GetObjects(", source, NoLoadPaths[i]);
				StringAssert.DoesNotContain("KingdomSurvey.Take(", source, NoLoadPaths[i]);
				StringAssert.DoesNotContain("CachedZones", source, NoLoadPaths[i]);
				StringAssert.DoesNotContain("SystemLongDistanceMoveTo", source, NoLoadPaths[i]);
			}
			string allowance = Read("Growth/KingdomConstructionInputLeaseAuthority.cs");
			StringAssert.DoesNotContain("InputObservation", allowance);
			StringAssert.Contains("ActiveLocalCustody", allowance);
		}

		[Test]
		public void ObservationMigrationIsEmptyAndVisitsReplaceOnlyTheirZone()
		{
			string source = Read("Growth/KingdomConstruction.InputObservationRegistry.cs");
			Ordered(source, "string.IsNullOrEmpty(raw)", "EmptyInputObservationBook(system)");
			Ordered(source, "book.RealmId != system.RealmId",
				"book = EmptyInputObservationBook(system)");
			Ordered(source, "if (book.ZoneAt(i).ZoneId != observed.ZoneId)",
				"zones.Add(observed)", "zones.Sort", "TryEncode(next",
				"SetStringGameState", "GetStringGameState");
			StringAssert.Contains("ActiveInputGround(zone, survey)", source);
			StringAssert.Contains("owner.HasStringProperty(InputMarkerProperty)", source);
			StringAssert.Contains("owner.HasIntProperty(InputMarkerProperty)", source);
			StringAssert.Contains("item.HasStringProperty(InputMarkerProperty)", source);
			StringAssert.Contains("item.HasIntProperty(InputMarkerProperty)", source);
			string water = Read("Growth/KingdomConstruction.InputDrive.Water.cs");
			StringAssert.Contains("!vessel.HasStringProperty(InputMarkerProperty)", water);
			StringAssert.Contains("!vessel.HasIntProperty(InputMarkerProperty)", water);
			StringAssert.DoesNotContain("GetZone(", source);
			StringAssert.DoesNotContain("GetObjects(", source);
		}

		[Test]
		public void PickupTransitLandingAndCancellationStayVisitPartitioned()
		{
			string source = Read("Growth/KingdomConstruction.InputDrive.Source.cs");
			Ordered(source, "for (int i = 0; i < receipt.ChildCount; i++)",
				"source.Phase == KingdomConstructionInputSourcePhase.Debited",
				"cargo.Phase == KingdomConstructionInputCargoPhase.PickupIntent",
				"active.ZoneID != child.SourceZoneId",
				"TryRootConstructionInputTransitCarrier", "TryAcknowledgeConstructionInputPickup");
			string transit = Read(
				"Simulation/City/KingdomCentralLogistics.19.ConstructionInputTransitCustody.cs");
			Ordered(transit, "RootTransit(ownerOperationId, tripId, carrier)",
				"carrier.TryRemoveFromContext()", "carrier.RemoveFromContext()");
			string arrival = Read(
				"Simulation/City/KingdomCentralLogistics.11.ConstructionInputArrivalAndAcknowledgements.cs");
			Ordered(arrival, "ReferenceEquals(The.ZoneManager.ActiveZone, liveDestination)",
				"LookupTransitRoot", "KingdomPhysicalLookupState.Ambiguous",
				"target.AddObject(body", "KingdomResidents.Bind",
				"ReleaseTransitRoot");
			string cancellation = Read(
				"Growth/KingdomConstruction.InputDrive.Cancellation.cs") + "\n" + Read(
				"Growth/KingdomConstruction.InputDrive.Cancellation.Partition.cs");
			Ordered(cancellation, "CancellationTargetPartitionRequired",
				"active.ZoneID != receipt.TargetZoneId",
				"TryRetractConstructionInputTargetCarrier", "return false;");
			StringAssert.Contains("active.ZoneID != source.SourceZoneId", cancellation);
			StringAssert.Contains("TryRetireConstructionInputCancellationSource", cancellation);
			string close = Read(
				"Simulation/City/KingdomCentralLogistics.12.ConstructionInputRecovery.cs");
			close = close.Substring(close.IndexOf(
				"internal static bool TryCloseCancelledConstructionInputOwner(",
				StringComparison.Ordinal));
			Ordered(close, "bindings.Holds(trips[i]", "ConstructionInputTransitRootExists",
				"table.TryClose(rows[i].JobId");
			StringAssert.DoesNotContain("GetZone(", close);
		}

		[Test]
		public void EveryConstructionCustodyPhaseHasNoGenericActorSimulation()
		{
			string source = Read(
				"Simulation/City/KingdomPorters.01.RenderingSteppingAndRetirement.cs");
			string sweepGuard = Read(
				"Simulation/City/KingdomPorters.01a.ConstructionInputSweepGuard.cs");
			Ordered(source, "KingdomDeliveryCargoAuthority.ConstructionInput",
				"continue;");
			StringAssert.DoesNotContain("ConstructionInput\n"
				+ "\t\t\t\t\t\t&& row.DeliveryPhase", source);
			string handoff = Read(
				"Simulation/City/KingdomPorters.03.ClosingAndCustodyHandoff.cs");
			Ordered(handoff, "private static void HandoffCentral(",
				"KingdomDeliveryCargoAuthority.ConstructionInput", "return;", "GetZone(");
			StringAssert.Contains("ConstructionInputSweepProtected(System, body)", source);
			StringAssert.Contains("KingdomConstructionRules.TryGetInputReceipt", sweepGuard);
			StringAssert.Contains("InputMarkerProperty", sweepGuard);
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }

		private static void Ordered(string source, params string[] markers)
		{
			int at = -1;
			for (int i = 0; i < markers.Length; i++)
			{
				int next = source.IndexOf(markers[i], at + 1, StringComparison.Ordinal);
				Assert.Greater(next, at, markers[i]); at = next;
			}
		}
	}
}
#endif
