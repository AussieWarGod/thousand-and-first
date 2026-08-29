#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomConstructionInputPlannerSourceTests
	{
		private static readonly string[] PlannerPaths = new[]
		{
			"Growth/KingdomConstructionInputPlan.Models.cs",
			"Growth/KingdomConstructionInputPlan.Leases.cs",
			"Growth/KingdomConstructionInputPlan.Validation.cs",
			"Growth/KingdomConstructionInputPlan.Planning.cs",
			"Growth/KingdomConstructionInputPlan.Receipt.cs",
			"Growth/KingdomConstruction.InputPlannerAuthority.cs",
			"Growth/KingdomConstruction.InputPlannerScan.cs",
			"Growth/KingdomConstruction.InputPlannerReservation.cs",
			"Growth/KingdomConstruction.InputObservationRegistry.cs",
			"Growth/KingdomConstructionInputObservation.Models.cs",
			"Growth/KingdomConstructionInputObservation.Rules.cs",
			"Growth/KingdomConstructionInputObservation.Codec.cs",
			"Simulation/City/KingdomCentralLogistics.18.ConstructionInputObservedRoute.cs",
			"Simulation/City/KingdomCentralLogistics.14.ConstructionInputCarrierAccess.cs"
		};

		[Test]
		public void ScanUsesOnlyDurableAttendedObservationsAndNeverLoadsClaimedGround()
		{
			string authority = Read("Growth/KingdomConstruction.InputPlannerAuthority.cs");
			StringAssert.Contains("TryExactSettlementIds(true", authority);
			StringAssert.Contains("AddInputZones(system.ClaimedZones", authority);
			StringAssert.Contains("system.NonSeatSettlements()", authority);
			StringAssert.Contains("for (int i = 0; i < nonSeat.Count; i++)", authority);
			StringAssert.Contains("AddInputZones(away.ClaimedZones", authority);
			StringAssert.Contains("TryReadInputObservations", authority);
			StringAssert.Contains("found[i].Observation = observations[j]", authority);
			StringAssert.DoesNotContain("GetZone(", authority);
			StringAssert.DoesNotContain("GetObjects(", authority);
			StringAssert.Contains("found.Sort", authority);
			StringAssert.Contains("KingdomRules.PolicyUpkeep", authority);
			StringAssert.Contains("KingdomRules.UpkeepDrams", authority);
			StringAssert.Contains("KingdomConstructionInputLeaseAuthority.TryCapture",
				authority);
			StringAssert.DoesNotContain("TryDecode(job.InputReceipt", authority);
		}

		[Test]
		public void ScanIsDedicatedExactBoundedAndNeverMintsAbstractStock()
		{
			string scan = Read("Growth/KingdomConstruction.InputPlannerScan.cs");
			StringAssert.Contains("observation.LineAt(i)", scan);
			StringAssert.Contains("line.HolderId", scan);
			StringAssert.Contains("line.SourceObjectId", scan);
			Ordered(scan, "KingdomMaterials.TryMaterialOf", "KingdomMaterials.TryExoticOf",
				"KingdomMaterials.UnitBits");
			StringAssert.Contains("leases.Contains(authority.ZoneId", scan);
			StringAssert.Contains("TryWaterReserveFloor", scan);
			StringAssert.Contains("InputGroupKey(row)", scan);
			StringAssert.Contains("TryPreviewObservedManifestRoute", scan);
			StringAssert.Contains("TryInputRouteCost", scan);
			StringAssert.Contains("MaxScannedCandidates", scan);
			StringAssert.Contains("nothing was truncated", scan);
			StringAssert.Contains("line.ProtectedCargo", scan);
			StringAssert.DoesNotContain("KingdomSurvey.Take", scan);
			StringAssert.DoesNotContain("KingdomMaterials.Stock", scan);
			StringAssert.DoesNotContain("GetZone(", scan);
			StringAssert.DoesNotContain("GetObjects(", scan);
			string capture = Read("Growth/KingdomConstruction.InputObservationRegistry.cs");
			StringAssert.Contains("ActiveInputGround(zone, survey)", capture);
			StringAssert.Contains("survey.Stores", capture);
			StringAssert.Contains("survey.MaterialStockpiles", capture);
			StringAssert.Contains("InputObservationStateKey", capture);
			StringAssert.DoesNotContain("MaterialStock.Put", scan);
			StringAssert.DoesNotContain("GameObject.Create", scan);
			StringAssert.DoesNotContain("Obliterate", scan);
		}

		[Test]
		public void ReserveFreezesEveryChildAndCleansOnlyKnownNeutralRows()
		{
			string source = Read("Growth/KingdomConstruction.InputPlannerReservation.cs");
			Ordered(source, "TryManifestSpillAnchor", "TryInputLeases", "TryInputZones",
				"TryScanInputCandidates", "KingdomConstructionInputPlanRules.TryPlan",
				"TryInputIntent", "TryPrepareConstructionInputReservation",
				"TryDescribeConstructionInputReservation", "TryCreateReceipt");
			StringAssert.Contains("draft.CargoStart, draft.CargoCount", source);
			StringAssert.Contains("CancelPreparedRows(System, Job.Id, reserved", source);
			StringAssert.Contains("TryCancelConstructionInputReservations", source);
			StringAssert.Contains("TryActivateConstructionInputReservations", source);
			StringAssert.Contains("Receipt.Schema, Receipt.PlanDigest", source);
			StringAssert.Contains("Job.Claims.WaterOutstanding", source);
			StringAssert.Contains("Job.Claims.MaterialOutstanding", source);
			StringAssert.Contains("RequiredObjectIds, candidates", source);
			StringAssert.DoesNotContain("MaterialStock.Put", source);
			StringAssert.DoesNotContain("TryPublish(Job", source);
		}

		[Test]
		public void PlanPacksOnlyConsecutiveHolderEndpointsAndUsesRealWaterVessels()
		{
			string planning = Read("Growth/KingdomConstructionInputPlan.Planning.cs");
			StringAssert.Contains("count < KingdomConstructionInputRules.MaxCargoPerChild", planning);
			StringAssert.Contains("left.SourceZoneId == right.SourceZoneId", planning);
			StringAssert.Contains("left.HolderId == right.HolderId", planning);
			StringAssert.Contains(
				"Math.Min(left, KingdomConstructionInputRules.WaterCargoCapacity)", planning);
			string receipt = Read("Growth/KingdomConstructionInputPlan.Receipt.cs");
			StringAssert.Contains("water ? WaterCargoBlueprint : source.Blueprint", receipt);
			StringAssert.Contains("water ? 64 : source.Count", receipt);
			StringAssert.Contains("source.Blueprint, line.Before", receipt);
		}

		[Test]
		public void SourceCarrierAccessorRequiresExactAuthorityBindingAndLoadedBody()
		{
			string source = Read(
				"Simulation/City/KingdomCentralLogistics.14.ConstructionInputCarrierAccess.cs");
			StringAssert.Contains("KingdomDeliveryCargoAuthority.ConstructionInput", source);
			StringAssert.Contains("KingdomDeliveryPhase.SourceDebitPrepared", source);
			StringAssert.Contains("bindings.TryGet(tripId, KingdomBindingKind.Transient", source);
			StringAssert.Contains("Zone zone = The.ZoneManager.ActiveZone", source);
			StringAssert.Contains("KingdomSurvey.ActiveFor(zone)", source);
			StringAssert.Contains("survey.FindBoundBody(binding.ObjectId", source);
			StringAssert.Contains("exact.Inventory == null", source);
			StringAssert.Contains("porter.JobId != tripId", source);
			StringAssert.Contains("exact.CurrentCell.X != row.DeliverySourceX", source);
			StringAssert.DoesNotContain("GetZone(", source);
			StringAssert.DoesNotContain("CachedZones", source);
			StringAssert.DoesNotContain("GameObject.Create", source);
		}

		[Test]
		public void PlannerAndAdapterShardsStayBelowStructuralGate()
		{
			for (int i = 0; i < PlannerPaths.Length; i++)
			{
				string source = Read(PlannerPaths[i]);
				Assert.LessOrEqual(source.Split('\n').Length, 300, PlannerPaths[i]);
			}
		}

		private static string Read(string path)
		{ return TestMain.ReadRepositoryText(path); }

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
