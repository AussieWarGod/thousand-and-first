#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomPlanCancellationTests
	{
		private const string MarkerId = "marker-1";
		private static readonly string Owner =
			KingdomConstructionRules.OwnerKey("realm", 7L, "settlement");

		private static KingdomMaterialDebitCost MaterialCost(int Timber)
		{
			KingdomMaterialTally materials = new KingdomMaterialTally();
			materials.Set(KingdomMaterial.Timber, Timber);
			return new KingdomMaterialDebitCost(materials, null, null);
		}

		private static KingdomConstructionJob Job(KingdomConstructionRoute Route,
			KingdomConstructionPhase Phase, int Water = 0,
			KingdomMaterialDebitCost Material = null)
		{
			return new KingdomConstructionJob
			{
				Id = "00000000000000000000000000000001",
				OwnerKey = Owner,
				ZoneId = "JoppaWorld.11.22.1.1.10",
				Route = Route,
				Phase = Phase,
				Projection = KingdomConstructionRules.ProjectionFor(Route),
					X = Route == KingdomConstructionRoute.PlotPlan ? 20 : 12,
					Y = Route == KingdomConstructionRoute.PlotPlan ? 15 : 9,
				SubjectId = MarkerId,
				SourceId = MarkerId,
				TargetKey = "target",
				Payload = "payload",
				CreatedTick = 10L,
				StartedTick = 10L,
				DueTick = 20L,
				UpdatedTick = 10L,
				Revision = 1,
				Claims = KingdomConstructionRules.NewClaims(Water,
					Material ?? new KingdomMaterialDebitCost())
			};
		}

		private static void AssertBlocked(KingdomConstructionJob Job)
		{
			Assert.IsTrue(KingdomConstructionRules.ValidJob(Job));
			Assert.IsFalse(KingdomConstructionRules.PlanMarkerCancellationSettled(Job));
		}

		[Test]
		public void OnlyCleanCompensatedOrCancelledPlanRoutePhasePairsReleaseMarker()
		{
			foreach (KingdomConstructionRoute route in
				Enum.GetValues(typeof(KingdomConstructionRoute)))
			{
				if (route == KingdomConstructionRoute.None) continue;
				foreach (KingdomConstructionPhase phase in
					Enum.GetValues(typeof(KingdomConstructionPhase)))
				{
					if (phase == KingdomConstructionPhase.Invalid) continue;
					KingdomConstructionJob job = Job(route, phase);
					Assert.IsTrue(KingdomConstructionRules.ValidJob(job), route + " " + phase);
					bool expected = (route == KingdomConstructionRoute.PlanScaffold
						|| route == KingdomConstructionRoute.PlotPlan)
						&& (phase == KingdomConstructionPhase.Compensated
							|| phase == KingdomConstructionPhase.Cancelled);
					Assert.AreEqual(expected,
						KingdomConstructionRules.PlanMarkerCancellationSettled(job),
						route + " " + phase);
				}
			}
		}

		[Test]
		public void BothPlanRoutesKeepCleanTerminalRetrySemantics()
		{
			foreach (KingdomConstructionRoute route in new[]
			{
				KingdomConstructionRoute.PlanScaffold,
				KingdomConstructionRoute.PlotPlan
			})
			foreach (KingdomConstructionPhase phase in new[]
			{
				KingdomConstructionPhase.Compensated,
				KingdomConstructionPhase.Cancelled
			})
			{
				KingdomConstructionJob job = Job(route, phase, 5, MaterialCost(2));
				Assert.IsTrue(KingdomConstructionRules.ValidJob(job));
				Assert.IsTrue(KingdomConstructionRules.PlanMarkerCancellationSettled(job),
					route + " " + phase);
			}
		}

		[Test]
		public void PartialWaterDebitBlocksActiveAndForgedTerminalReceipts()
		{
			KingdomConstructionJob job = Job(KingdomConstructionRoute.PlanScaffold,
				KingdomConstructionPhase.Outstanding, Water: 5);
			Assert.IsTrue(KingdomConstructionRules.TryApplyWaterAttempt(job.Claims,
				5, 2, 3, 2, true, out KingdomConstructionClaims partial));
			job.Claims = partial;
			AssertBlocked(job);
			job.Phase = KingdomConstructionPhase.Compensated;
			AssertBlocked(job);
			job.Phase = KingdomConstructionPhase.Cancelled;
			AssertBlocked(job);
		}

		[Test]
		public void PartialMaterialDebitBlocksActiveAndForgedTerminalReceipts()
		{
			KingdomConstructionJob job = Job(KingdomConstructionRoute.PlotPlan,
				KingdomConstructionPhase.Outstanding, Material: MaterialCost(2));
			job.Claims.MaterialSpent = MaterialCost(1).ToClaimString();
			job.Claims.MaterialOutstanding = MaterialCost(1).ToClaimString();
			job.Claims.MaterialLost = MaterialCost(1).ToClaimString();
			AssertBlocked(job);
			job.Phase = KingdomConstructionPhase.Compensated;
			AssertBlocked(job);
			job.Phase = KingdomConstructionPhase.Cancelled;
			AssertBlocked(job);
		}

		[Test]
		public void CommittedClaimsOrPhysicalEffectsAlwaysBlockTerminalReceipt()
		{
			KingdomConstructionJob water = Job(KingdomConstructionRoute.PlanScaffold,
				KingdomConstructionPhase.Compensated, Water: 3);
			water.Claims.WaterSpent = 3;
			water.Claims.WaterOutstanding = 0;
			water.Claims.WaterLost = 3;
			AssertBlocked(water);
			water.Claims.WaterSpent = 0;
			water.Claims.WaterOutstanding = 3;
			AssertBlocked(water);

			KingdomConstructionJob material = Job(KingdomConstructionRoute.PlanScaffold,
				KingdomConstructionPhase.Cancelled, Material: MaterialCost(2));
			material.Claims.MaterialSpent = MaterialCost(2).ToClaimString();
			material.Claims.MaterialOutstanding = MaterialCost(0).ToClaimString();
			material.Claims.MaterialLost = MaterialCost(2).ToClaimString();
			AssertBlocked(material);
			material.Claims.MaterialSpent = MaterialCost(0).ToClaimString();
			material.Claims.MaterialOutstanding = MaterialCost(2).ToClaimString();
			AssertBlocked(material);

			KingdomConstructionJob output = Job(KingdomConstructionRoute.PlotPlan,
				KingdomConstructionPhase.Compensated);
			output.OutputId = "output-1";
			AssertBlocked(output);
			output.OutputId = null;
			output.PhysicalPhase = KingdomPhysicalPhase.OutputIntent;
			AssertBlocked(output);
			output.PhysicalPhase = KingdomPhysicalPhase.None;
			output.PhysicalIndex = 1;
			AssertBlocked(output);
			output.PhysicalIndex = 0;
			output.PhysicalAmount = 1;
			AssertBlocked(output);
			output.PhysicalAmount = 0;
			output.PhysicalSpilled = 1;
			AssertBlocked(output);
			output.PhysicalSpilled = 0;
			output.PhysicalItemId = "item-1";
			AssertBlocked(output);
			output.PhysicalItemId = null;
			output.PhysicalDestinationId = "destination-1";
			AssertBlocked(output);
			output.PhysicalDestinationId = null;
			output.PhysicalReceipt = "physical-proof";
			AssertBlocked(output);
		}

		[Test]
		public void InexactMalformedCompleteOrForeignReceiptsFailClosed()
		{
			KingdomConstructionJob complete = Job(KingdomConstructionRoute.PlanScaffold,
				KingdomConstructionPhase.Complete);
			AssertBlocked(complete);
			KingdomConstructionJob foreign = Job(KingdomConstructionRoute.CommissionScaffold,
				KingdomConstructionPhase.Compensated);
			AssertBlocked(foreign);
			KingdomConstructionJob inexact = Job(KingdomConstructionRoute.PlotPlan,
				KingdomConstructionPhase.Cancelled);
			inexact.Claims.Exact = false;
			AssertBlocked(inexact);
			KingdomConstructionJob malformed = Job(KingdomConstructionRoute.PlotPlan,
				KingdomConstructionPhase.Cancelled);
			malformed.Claims.MaterialSpent = "not-a-claim";
			Assert.IsFalse(KingdomConstructionRules.ValidJob(malformed));
			Assert.IsFalse(KingdomConstructionRules.PlanMarkerCancellationSettled(malformed));
			Assert.IsFalse(KingdomConstructionRules.PlanMarkerCancellationSettled(null));
		}

		[Test]
		public void ReceiptlessMarkerRejectsEveryForeignDurableIdentityLane()
		{
			KingdomConstructionJob safe = Job(KingdomConstructionRoute.PlanScaffold,
				KingdomConstructionPhase.Compensated);
			List<KingdomConstructionJob> jobs = new List<KingdomConstructionJob> { safe };
			Assert.IsTrue(Allowed(jobs, false, null));
			Assert.IsTrue(Allowed(jobs, true, safe.Id));
			Assert.IsFalse(Allowed(jobs, true, "00000000000000000000000000000002"));

			KingdomConstructionJob foreign = Job(KingdomConstructionRoute.RoadPaving,
				KingdomConstructionPhase.Compensated);
			Assert.IsFalse(Allowed(new List<KingdomConstructionJob> { foreign }, false, null));
			foreign.SourceId = "road-source";
			foreign.SubjectId = "road-subject";
			foreign.OutputId = MarkerId;
			Assert.IsFalse(Allowed(new List<KingdomConstructionJob> { foreign }, false, null));
			foreign.OutputId = null;
			foreign.PhysicalItemId = MarkerId;
			Assert.IsFalse(Allowed(new List<KingdomConstructionJob> { foreign }, false, null));
			foreign.PhysicalItemId = null;
			foreign.PhysicalDestinationId = MarkerId;
			Assert.IsFalse(Allowed(new List<KingdomConstructionJob> { foreign }, false, null));
			foreign.PhysicalDestinationId = null;
			Assert.IsTrue(Allowed(new List<KingdomConstructionJob> { foreign }, false, null));
		}

		[Test]
		public void CleanRowMustBindExactOwnerSourceSubjectGroundAndDesign()
		{
			KingdomConstructionJob row = Job(KingdomConstructionRoute.PlanScaffold,
				KingdomConstructionPhase.Compensated);
			Assert.IsTrue(Allowed(new List<KingdomConstructionJob> { row }, false, null));
			row.OwnerKey = KingdomConstructionRules.OwnerKey("other", 7L, "settlement");
			Assert.IsFalse(Allowed(new List<KingdomConstructionJob> { row }, false, null));
			row = Job(KingdomConstructionRoute.PlanScaffold,
				KingdomConstructionPhase.Compensated); row.SourceId = "other";
			Assert.IsFalse(Allowed(new List<KingdomConstructionJob> { row }, false, null));
			row = Job(KingdomConstructionRoute.PlanScaffold,
				KingdomConstructionPhase.Compensated); row.SubjectId = "other";
			Assert.IsFalse(Allowed(new List<KingdomConstructionJob> { row }, false, null));
			row = Job(KingdomConstructionRoute.PlanScaffold,
				KingdomConstructionPhase.Compensated); row.ZoneId = "other-zone";
			Assert.IsFalse(Allowed(new List<KingdomConstructionJob> { row }, false, null));
			row = Job(KingdomConstructionRoute.PlanScaffold,
				KingdomConstructionPhase.Compensated); row.X++;
			Assert.IsFalse(Allowed(new List<KingdomConstructionJob> { row }, false, null));
			row = Job(KingdomConstructionRoute.PlanScaffold,
				KingdomConstructionPhase.Compensated); row.TargetKey = "other-design";
			Assert.IsFalse(Allowed(new List<KingdomConstructionJob> { row }, false, null));
		}

		[Test]
		public void PlotPlanRowUsesFrozenMainAnchorRatherThanOutsideStake()
		{
			KingdomConstructionJob row = Job(KingdomConstructionRoute.PlotPlan,
				KingdomConstructionPhase.Compensated);
			Assert.IsTrue(Allowed(new List<KingdomConstructionJob> { row }, true, row.Id));
			row.X = 12;
			row.Y = 9;
			Assert.IsFalse(Allowed(new List<KingdomConstructionJob> { row }, true, row.Id));
		}

		[Test]
		public void EveryObjectReferenceLaneOnAnotherRowBlocksReceiptlessMarker()
		{
			KingdomConstructionJob safe = Job(KingdomConstructionRoute.PlanScaffold,
				KingdomConstructionPhase.Compensated);
			foreach (Action<KingdomConstructionJob> nameMarker in new Action<KingdomConstructionJob>[]
			{
				j => j.SourceId = MarkerId,
				j => j.SubjectId = MarkerId,
				j => j.OutputId = MarkerId,
				j => j.PhysicalItemId = MarkerId,
				j => j.PhysicalDestinationId = MarkerId
			})
			{
				KingdomConstructionJob other = Job(KingdomConstructionRoute.RoadPaving,
					KingdomConstructionPhase.Compensated);
				other.SourceId = "road-source";
				other.SubjectId = "road-subject";
				nameMarker(other);
				Assert.IsTrue(KingdomConstructionRules.ValidJob(other));
				Assert.IsFalse(Allowed(new List<KingdomConstructionJob> { safe, other },
					false, null));
			}
		}

		[Test]
		public void MalformedUnrelatedRowAndUnsettledSemanticStateFailClosed()
		{
			KingdomConstructionJob clean = Job(KingdomConstructionRoute.PlanScaffold,
				KingdomConstructionPhase.Cancelled);
			KingdomConstructionJob malformed = Job(KingdomConstructionRoute.RoadPaving,
				KingdomConstructionPhase.Compensated);
			malformed.SourceId = "road-source";
			malformed.SubjectId = "road-subject";
			malformed.Id = "not-a-guid";
			Assert.IsFalse(Allowed(new List<KingdomConstructionJob> { clean, malformed },
				false, null));
			clean.Outbox = new KingdomConstructionOutbox();
			Assert.IsFalse(KingdomConstructionRules.PlanMarkerCancellationSettled(clean));
		}

		[Test]
		public void PostCallbackProofUsesAbsenceAndRegistryNotCallbackReturn()
		{
			foreach (bool registrySafe in new[] { false, true })
			foreach (bool authoritySafe in new[] { false, true })
			foreach (KingdomPhysicalLookupState state in
				Enum.GetValues(typeof(KingdomPhysicalLookupState)))
			foreach (bool referenceValid in new[] { false, true })
			{
				bool expected = registrySafe && authoritySafe && !referenceValid
					&& state == KingdomPhysicalLookupState.Absent;
				Assert.AreEqual(expected,
					KingdomConstructionRules.PlanMarkerCancellationRemovalProved(
						referenceValid, state, registrySafe, authoritySafe),
					state + " ref=" + referenceValid + " registry=" + registrySafe
						+ " authority=" + authoritySafe);
			}
		}

		private static bool Allowed(IList<KingdomConstructionJob> Jobs, bool HasReceipt,
			string Receipt)
		{
			return KingdomConstructionRules.PlanMarkerCancellationAllowed(Jobs, HasReceipt,
				Receipt, MarkerId, Owner, "JoppaWorld.11.22.1.1.10", "target",
				job => KingdomConstructionRules.PlanMarkerRouteCoordinatesValid(job.Route,
					12, 9, true, true, 20, 15, job.X, job.Y));
		}

		[Test]
		public void RuntimeFiltersAndDirectCancelShareOneCutSafeGuard()
		{
			string marker = TestMain.ReadRepositoryText(
				"Growth/KingdomPlanMarker.Commands.cs");
			string plans = TestMain.ReadRepositoryText("Core/KingdomCharterPart.Plans.cs");
			string pending = Between(marker, "public static List<GameObject> FindPending(",
				"public static string Describe(");
			StringAssert.Contains("found.Add(item)", pending);
			StringAssert.DoesNotContain("CanCancel(", pending);

			string guard = Between(marker, "public static bool CanCancel(",
				"public static bool CanCancel(GameObject Marker");
			StringAssert.Contains("TryPrepareCancellation(System, Marker", guard);

			string cancel = Between(marker,
				"public static bool TryCancel(KingdomSystem System, GameObject Marker",
				"public static bool TryCancel(GameObject Marker");
			AssertOrdered(cancel, "TryPrepareCancellation(System, Marker",
				"Marker.Destroy(null, Silent: true)",
				"ObserveCurrentTopologyInActive(proof.Zone, Marker)",
				"RegistryAllows(proof", "PlanMarkerCancellationRemovalProved(");
			StringAssert.Contains("catch (Exception ex)", cancel);
			StringAssert.DoesNotContain("return false;\n\t\t\t}",
				Between(cancel, "catch (Exception ex)", "finally"));

			string direct = marker.Substring(marker.IndexOf("public static void Cancel(",
				StringComparison.Ordinal));
			StringAssert.Contains("TryCancel(Marker, out _)", direct);
			StringAssert.DoesNotContain("Marker.Destroy", direct);

			string manage = plans.Substring(plans.IndexOf("public void ManagePlans(",
				StringComparison.Ordinal));
			AssertOrdered(manage, "KingdomPlanMarker.CanCancel(System, target",
				"KingdomPlanMarker.TryCancel(System, target, out string failure)",
				"KingdomChronicle.Record(System");
			StringAssert.Contains("Popup.Show(failure", manage);
			StringAssert.Contains("[construction-bound]", manage);
			StringAssert.DoesNotContain("Nothing was spent", plans);
			StringAssert.DoesNotContain("nothing was taken", plans);
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
