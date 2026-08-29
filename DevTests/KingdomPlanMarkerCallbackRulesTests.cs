#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomPlanMarkerCallbackRulesTests
	{
		private const string MarkerId = "marker-1";
		private const string ZoneId = "JoppaWorld.11.22.1.1.10";
		private const string Target = "target";
		private static readonly string Owner =
			KingdomConstructionRules.OwnerKey("realm", 7L, "settlement");

		private enum CallbackOutcome
		{
			ReturnedTrueAfter,
			ReturnedFalseBefore,
			ReturnedFalseAfter,
			ThrewBefore,
			ThrewAfter,
			Moved,
			Replacement,
			Duplicate,
			Stacked,
			RegistryMutation,
			ReceiptCorruption,
			FrozenMutation,
			AuthorityMutation
		}

		private static KingdomConstructionJob Job(string id =
			"00000000000000000000000000000001")
		{
			return new KingdomConstructionJob
			{
				Id = id, OwnerKey = Owner, ZoneId = ZoneId,
				Route = KingdomConstructionRoute.PlanScaffold,
				Phase = KingdomConstructionPhase.Cancelled,
				Projection = KingdomConstructionProjection.Scaffold,
				X = 12, Y = 9, SourceId = MarkerId, SubjectId = MarkerId,
				TargetKey = Target, Payload = "skin", CreatedTick = 10L,
				StartedTick = 10L, DueTick = 20L, UpdatedTick = 10L,
				Revision = 1,
				Claims = KingdomConstructionRules.NewClaims(0,
					new KingdomMaterialDebitCost())
			};
		}

		[Test]
		public void ReceiptShapeRejectsEmptyWrongTypedAndContradictoryEvidence()
		{
			Assert.AreEqual(KingdomPlanReceiptShape.Absent,
				KingdomConstructionRules.PlanMarkerReceiptShape(false, null, false));
			Assert.AreEqual(KingdomPlanReceiptShape.Exact,
				KingdomConstructionRules.PlanMarkerReceiptShape(true, "receipt", false));
			foreach (var evidence in new[]
			{
				Tuple.Create(true, (string)null, false), Tuple.Create(true, "", false),
				Tuple.Create(false, (string)null, true), Tuple.Create(true, "receipt", true),
				Tuple.Create(false, "ghost", false)
			})
				Assert.AreEqual(KingdomPlanReceiptShape.Corrupt,
					KingdomConstructionRules.PlanMarkerReceiptShape(
						evidence.Item1, evidence.Item2, evidence.Item3));
		}

		[Test]
		public void RegistryUnreferencedScansAllFiveLanesAndRejectsMalformedOrDuplicateRows()
		{
			Assert.IsTrue(KingdomConstructionRules.PlanMarkerRegistryUnreferenced(
				new List<KingdomConstructionJob>(), MarkerId));
			foreach (Action<KingdomConstructionJob> bind in new Action<KingdomConstructionJob>[]
			{
				j => j.SourceId = MarkerId, j => j.SubjectId = MarkerId,
				j => j.OutputId = MarkerId, j => j.PhysicalItemId = MarkerId,
				j => j.PhysicalDestinationId = MarkerId
			})
			{
				KingdomConstructionJob row = Job();
				row.SourceId = "other-source";
				row.SubjectId = "other-subject";
				bind(row);
				Assert.IsTrue(KingdomConstructionRules.ValidJob(row));
				Assert.IsFalse(KingdomConstructionRules.PlanMarkerRegistryUnreferenced(
					new List<KingdomConstructionJob> { row }, MarkerId));
			}
			KingdomConstructionJob malformed = Job();
			malformed.SourceId = "other-source";
			malformed.SubjectId = "other-subject";
			malformed.Id = "bad-id";
			Assert.IsFalse(KingdomConstructionRules.PlanMarkerRegistryUnreferenced(
				new List<KingdomConstructionJob> { malformed }, MarkerId));
			KingdomConstructionJob first = Job();
			first.SourceId = "other-source"; first.SubjectId = "other-subject";
			KingdomConstructionJob duplicate = first.Copy();
			Assert.IsFalse(KingdomConstructionRules.PlanMarkerRegistryUnreferenced(
				new List<KingdomConstructionJob> { first, duplicate }, MarkerId));
		}

		[Test]
		public void CancellationRegistryRequiresAtMostOneExactMarkerRow()
		{
			KingdomConstructionJob first = Job();
			KingdomConstructionJob second = Job("00000000000000000000000000000002");
			Func<KingdomConstructionJob, bool> route = j =>
				KingdomConstructionRules.PlanMarkerRouteCoordinatesValid(
					j.Route, 12, 9, false, false, 0, 0, j.X, j.Y);
			Assert.IsFalse(KingdomConstructionRules.PlanMarkerCancellationAllowed(
				new List<KingdomConstructionJob> { first, second }, false, null,
				MarkerId, Owner, ZoneId, Target, route));
			Assert.IsFalse(KingdomConstructionRules.PlanMarkerCancellationAllowed(
				new List<KingdomConstructionJob> { first, second }, true, first.Id,
				MarkerId, Owner, ZoneId, Target, route));
		}

		[Test]
		public void RouteCoordinatesDistinguishStakeFromRealPlotMainAnchor()
		{
			Assert.IsTrue(KingdomConstructionRules.PlanMarkerRouteCoordinatesValid(
				KingdomConstructionRoute.PlanScaffold, 12, 9, false, false, 0, 0, 12, 9));
			Assert.IsFalse(KingdomConstructionRules.PlanMarkerRouteCoordinatesValid(
				KingdomConstructionRoute.PlanScaffold, 12, 9, false, false, 0, 0, 20, 15));
			Assert.IsTrue(KingdomConstructionRules.PlanMarkerRouteCoordinatesValid(
				KingdomConstructionRoute.PlotPlan, 12, 9, true, true, 20, 15, 20, 15));
			Assert.IsFalse(KingdomConstructionRules.PlanMarkerRouteCoordinatesValid(
				KingdomConstructionRoute.PlotPlan, 12, 9, true, true, 20, 15, 12, 9));
			Assert.IsFalse(KingdomConstructionRules.PlanMarkerRouteCoordinatesValid(
				KingdomConstructionRoute.PlotPlan, 12, 9, false, true, 20, 15, 20, 15));
			Assert.IsFalse(KingdomConstructionRules.PlanMarkerRouteCoordinatesValid(
				KingdomConstructionRoute.PlotPlan, 12, 9, true, false, 20, 15, 20, 15));
		}

		[Test]
		public void DirectGroundProofRejectsStacksContainersMovesReplacementsAndDuplicates()
		{
			Assert.IsTrue(DirectGround());
			Assert.IsFalse(DirectGround(count: 2));
			Assert.IsFalse(DirectGround(stacker: true));
			Assert.IsFalse(DirectGround(inventory: true));
			Assert.IsFalse(DirectGround(equipped: true));
			Assert.IsFalse(DirectGround(sameZone: false));
			Assert.IsFalse(DirectGround(sameCell: false));
			Assert.IsFalse(DirectGround(directReferences: 0));
			Assert.IsFalse(DirectGround(directReferences: 2));
			Assert.IsFalse(DirectGround(idState: KingdomPhysicalLookupState.Absent));
			Assert.IsFalse(DirectGround(idState: KingdomPhysicalLookupState.Ambiguous));
			Assert.IsFalse(DirectGround(exactReference: false));
		}

		[Test]
		public void AddObjectCommitUsesPostStateNotReturnOrThrow()
		{
			foreach (CallbackOutcome outcome in Enum.GetValues(typeof(CallbackOutcome)))
			{
				bool direct = outcome == CallbackOutcome.ReturnedTrueAfter
					|| outcome == CallbackOutcome.ReturnedFalseAfter
					|| outcome == CallbackOutcome.ThrewAfter
					|| outcome == CallbackOutcome.RegistryMutation
					|| outcome == CallbackOutcome.ReceiptCorruption
					|| outcome == CallbackOutcome.FrozenMutation
					|| outcome == CallbackOutcome.AuthorityMutation;
				bool expected = outcome == CallbackOutcome.ReturnedTrueAfter
					|| outcome == CallbackOutcome.ReturnedFalseAfter
					|| outcome == CallbackOutcome.ThrewAfter;
				bool actual = KingdomConstructionRules.PlanMarkerPlacementCommitAllowed(
					direct, outcome != CallbackOutcome.FrozenMutation,
					outcome == CallbackOutcome.ReceiptCorruption
						? KingdomPlanReceiptShape.Corrupt : KingdomPlanReceiptShape.Absent,
					outcome != CallbackOutcome.RegistryMutation,
					outcome != CallbackOutcome.AuthorityMutation);
				Assert.AreEqual(expected, actual, outcome.ToString());
			}
		}

		[Test]
		public void DestroyOutcomeUsesAbsenceOrExactSurvivorNotCallbackSignal()
		{
			AssertDestroy(CallbackOutcome.ReturnedFalseBefore, false, true);
			AssertDestroy(CallbackOutcome.ThrewBefore, false, true);
			AssertDestroy(CallbackOutcome.ReturnedFalseAfter, true, false);
			AssertDestroy(CallbackOutcome.ThrewAfter, true, false);
			AssertDestroy(CallbackOutcome.Moved, false, false);
			AssertDestroy(CallbackOutcome.Replacement, false, false);
			AssertDestroy(CallbackOutcome.Duplicate, false, false);
			AssertDestroy(CallbackOutcome.Stacked, false, false);
			AssertDestroy(CallbackOutcome.RegistryMutation, false, false);
			AssertDestroy(CallbackOutcome.AuthorityMutation, false, false);
		}

		private static bool DirectGround(int count = 1, bool stacker = false,
			bool inventory = false, bool equipped = false, bool sameZone = true,
			bool sameCell = true, int directReferences = 1,
			KingdomPhysicalLookupState idState = KingdomPhysicalLookupState.Exact,
			bool exactReference = true)
		{
			return KingdomConstructionRules.PlanMarkerDirectGroundProved(true, count, stacker,
				inventory, equipped, sameZone, sameCell, directReferences, idState, exactReference);
		}

		private static void AssertDestroy(CallbackOutcome Outcome, bool Removed,
			bool Survivor)
		{
			KingdomPhysicalLookupState state = Outcome == CallbackOutcome.ReturnedFalseAfter
				|| Outcome == CallbackOutcome.ThrewAfter
				|| Outcome == CallbackOutcome.RegistryMutation
				|| Outcome == CallbackOutcome.AuthorityMutation
				? KingdomPhysicalLookupState.Absent
				: Outcome == CallbackOutcome.Duplicate
					? KingdomPhysicalLookupState.Ambiguous : KingdomPhysicalLookupState.Exact;
			bool exactReference = Outcome == CallbackOutcome.ReturnedFalseBefore
				|| Outcome == CallbackOutcome.ThrewBefore || Outcome == CallbackOutcome.Moved
				|| Outcome == CallbackOutcome.Stacked;
			bool registry = Outcome != CallbackOutcome.RegistryMutation;
			bool authority = Outcome != CallbackOutcome.AuthorityMutation;
			bool removed = KingdomConstructionRules.PlanMarkerCancellationRemovalProved(
				exactReference, state, registry, authority);
			bool survivor = KingdomConstructionRules.PlanMarkerSurvivorProved(
				exactReference && Outcome != CallbackOutcome.Moved
					&& Outcome != CallbackOutcome.Stacked,
				exactReference, registry, authority);
			Assert.AreEqual(Removed, removed, Outcome + " removal");
			Assert.AreEqual(Survivor, survivor, Outcome + " survivor");
		}
	}
}
#endif
