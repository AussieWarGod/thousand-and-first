#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomPlanCancellationSourceTests
	{
		[Test]
		public void RegistryGateNamesEveryObjectLaneAndExactOwnerBinding()
		{
			string source = TestMain.ReadRepositoryText(
				"Growth/KingdomConstructionRules.PlanMarker.cs");
			string names = Between(source, "public static bool PlanMarkerNames(",
				"public static bool PlanMarkerRegistryUnreferenced(");
			foreach (string term in new[]
			{
				"Job.SourceId == MarkerId", "Job.SubjectId == MarkerId",
				"Job.OutputId == MarkerId", "Job.PhysicalItemId == MarkerId",
				"Job.PhysicalDestinationId == MarkerId"
			}) StringAssert.Contains(term, names);
			string gate = Between(source, "public static bool PlanMarkerCancellationAllowed(",
				"public static bool PlanMarkerCancellationRemovalProved(");
			foreach (string term in new[]
			{
				"job.SourceId == MarkerId", "job.SubjectId == MarkerId",
					"job.OwnerKey == OwnerKey",
					"job.ZoneId == ZoneId", "job.TargetKey == TargetKey",
					"PlanMarkerCancellationSettled(job)", "RouteProof(job)"
			}) StringAssert.Contains(term, gate);
			AssertOrdered(gate, "if (!ValidJob(job) || !ids.Add(job.Id)) return false;",
				"if (!PlanMarkerNames(job, MarkerId)) continue;", "bool exact",
				"if (!exact || ++named > 1");
		}

		[Test]
		public void DestroyCutReprovesTopologyAndRegistryBeforeSuccess()
		{
			string source = TestMain.ReadRepositoryText("Growth/KingdomPlanMarker.Commands.cs");
			string cancel = Between(source,
				"public static bool TryCancel(KingdomSystem System, GameObject Marker",
				"public static bool TryCancel(GameObject Marker");
			AssertOrdered(cancel, "TryPrepareCancellation(System, Marker",
				"Marker.Destroy(null, Silent: true)",
				"ObserveCurrentTopologyInActive(proof.Zone, Marker)",
				"KingdomConstruction.FindExactId(", "RegistryAllows(proof",
				"PlanMarkerCancellationRemovalProved(");
			string caught = Between(cancel, "catch (Exception ex)", "finally");
			StringAssert.Contains("callbackFailure = ex", caught);
			StringAssert.DoesNotContain("return", caught);
			StringAssert.Contains("The stake is gone, but durable construction state", cancel);
		}

		[Test]
		public void MenuKeepsConstructionBoundPlanVisibleAndRefusesBeforePrompt()
		{
			string commands = TestMain.ReadRepositoryText("Growth/KingdomPlanMarker.Commands.cs");
			string pending = Between(commands, "public static List<GameObject> FindPending(",
				"public static string Describe(");
			StringAssert.Contains("found.Add(item)", pending);
			StringAssert.DoesNotContain("CanCancel(", pending);

			string plans = TestMain.ReadRepositoryText("Core/KingdomCharterPart.Plans.cs");
			string manage = plans.Substring(plans.IndexOf("public void ManagePlans(",
				StringComparison.Ordinal));
			StringAssert.Contains("[construction-bound]", manage);
			AssertOrdered(manage, "KingdomPlanMarker.CanCancel(System, target",
				"Popup.Show(refusal", "Popup.ShowYesNo(",
				"KingdomPlanMarker.TryCancel(System, target");
		}

		[Test]
		public void NewStakeFreezesIdentityBeforeAnyPlacementCallback()
		{
			string plans = TestMain.ReadRepositoryText("Core/KingdomCharterPart.Plans.cs");
			string place = Between(plans, "public void PlaceBuildingPlan(",
				"public void ManagePlans(");
			AssertOrdered(place, "GameObject.Create(\"r_KingdomPlanMarker\")",
				"string markerId = marker.ID", "marker.IDIfAssigned != markerId",
				"TryPrepareNewMarker(System, marker, zone, cell",
				"accepted = cell.AddObject(marker)", "PlacementProved(System, marker");
			StringAssert.Contains("catch (Exception ex)", place);
			StringAssert.Contains("TryDiscardDetached(System, zone, marker, frozenMarker)", place);
			AssertOrdered(place, "PlacementProved(System, marker", "KingdomGovernanceScope.Commit(",
				"KingdomChronicle.Record(System");
		}

		[Test]
		public void LegacyIdentityMigrationRefusesAnyReceiptAndPrecedesJobPublication()
		{
			string custody = TestMain.ReadRepositoryText(
				"Growth/KingdomPlanMarker.CustodyAndRegistry.cs");
			string migration = Between(custody, "internal static bool EnsureLegacyProvenance(",
				"internal static bool PlacementProved(");
			AssertOrdered(migration, "HasProvenanceFragments(Marker)",
				"ReceiptShape(Marker", "BasicDirectGround(Marker",
				"if (string.IsNullOrEmpty(id)) id = Marker.ID",
				"PlanMarkerRegistryUnreferenced(jobs, id)", "TryStampProvenance(Marker",
				"ObserveChangedInActive");

			string runtime = TestMain.ReadRepositoryText("Growth/KingdomPlanMarker.cs");
			string pass = runtime.Substring(runtime.IndexOf("public static void OnSettlementPass(",
				StringComparison.Ordinal));
			AssertOrdered(pass, "EnsureLegacyProvenance(System, Z, item)",
				"KingdomConstruction.NewJob(System, Z");
		}

		[Test]
		public void AutomaticPublicationUsesFullRegistryFenceAtBothCuts()
		{
			string runtime = TestMain.ReadRepositoryText("Growth/KingdomPlanMarker.cs");
			string pass = runtime.Substring(runtime.IndexOf("public static void OnSettlementPass(",
				StringComparison.Ordinal));
			AssertOrdered(pass, "EnsureLegacyProvenance(System, Z, item)",
				"TryReleaseCleanReceipt(System, Z, item", "PublicationAllowed(System, Z, item",
				"KingdomConstruction.NewJob(System, Z", "ReserveExactWater(waterPrice)",
				"PublicationAllowed(System, Z, markerObject", "PreparedJobMatches(publicationProof, job)",
				"KingdomConstruction.TryFundNew(job");
			StringAssert.DoesNotContain("HasActiveSubject", pass);
			string custody = TestMain.ReadRepositoryText(
				"Growth/KingdomPlanMarker.CustodyAndRegistry.cs");
			string publication = Between(custody, "internal static bool PublicationAllowed(",
				"internal static bool PreparedJobMatches(");
			StringAssert.Contains("PlanMarkerRegistryUnreferenced(", publication);
			StringAssert.DoesNotContain("RegistryAllows(Proof, false", publication);
		}

		private static string Between(string Source, string Start, string End)
		{
			int start = Source.IndexOf(Start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, Start);
			int end = Source.IndexOf(End, start + Start.Length, StringComparison.Ordinal);
			Assert.Greater(end, start, End);
			return Source.Substring(start, end - start);
		}

		private static void AssertOrdered(string Source, params string[] Terms)
		{
			int cursor = -1;
			for (int i = 0; i < Terms.Length; i++)
			{
				int next = Source.IndexOf(Terms[i], cursor + 1, StringComparison.Ordinal);
				Assert.Greater(next, cursor, Terms[i]);
				cursor = next;
			}
		}
	}
}
#endif
