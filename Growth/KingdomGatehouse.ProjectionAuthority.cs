using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouse
	{
		private static bool TryPendingProjectionPart(GameObject Root,
			KingdomGatehousePlan Plan, out IPart Part)
		{
			Part = null;
			if (!GameObject.Validate(Root) || Plan == null) return false;
			Part = Plan.ReceiptVersion == 2
				? (IPart)Root.GetPart<r_KingdomGatehouseProjectionV2>()
				: Root.GetPart<r_KingdomGatehouseProjectionV1Pending>();
			return ProjectionPartMatches(Root, Plan, Part, true);
		}

		private static bool ProjectionAuthorityStillExact(GameObject Root, Cell Cell,
			KingdomConstructionJob Job, GameObject Scaffold, KingdomGatehousePlan Plan,
			string Encoded, out string Failure)
		{
			Failure = null;
			bool endpoints = GameObject.Validate(Root) && Root.CurrentCell == Cell
				&& Root.CurrentZone == Cell?.ParentZone && Root.IDIfAssigned == Job?.OutputId
				&& GameObject.Validate(Scaffold) && Scaffold.CurrentCell == Cell
				&& Scaffold.IDIfAssigned == Job?.SubjectId
				&& !Root.HasIntProperty(KingdomConstruction.ReceiptProperty)
				&& !Scaffold.HasIntProperty(KingdomConstruction.ReceiptProperty)
				&& !Root.HasIntProperty(KingdomUpgrade.BuildKeyProperty)
				&& Root.GetStringProperty(KingdomUpgrade.BuildKeyProperty)
					== KingdomGatehouseRules.BuildKey
				&& ProjectionJobStillExact(Job, Cell, Root, Scaffold, Encoded)
				&& KingdomConstruction.HasReceipt(Root, Job)
				&& KingdomConstruction.HasReceipt(Scaffold, Job)
				&& KingdomConstruction.PaidBuildMatches(Root, Job)
				&& KingdomGatehouseRules.MaterialClaimMatches(Plan,
					Job.Claims?.MaterialRequested)
				&& ExactRootPalette(Root, Plan)
				&& ScaffoldMatches(Scaffold, Plan);
			if (!endpoints)
			{
				Failure = "Gatehouse construction authority changed between satellite callbacks.";
				return false;
			}
			bool footprint = TryAudit(Cell.ParentZone, Plan, Root, Scaffold, out Failure);
			if (!KingdomGatehouseProjectionRules.ExactPendingEnvelope(
				Root.HasIntProperty(SchemaProperty), Root.HasStringProperty(SchemaProperty),
				Root.HasIntProperty(PlanProperty), Root.GetStringProperty(PlanProperty),
				Encoded, footprint))
			{
				Failure = Failure
					?? "Gatehouse root receipt or frozen footprint changed during a callback.";
				return false;
			}
			return true;
		}

		private static bool ProjectionCallbackAuthorityStillExact(GameObject Root, Cell Cell,
			KingdomConstructionJob Job, GameObject Scaffold, KingdomGatehousePlan Plan,
			string Encoded, out string Failure)
		{
			if (ProjectionAuthorityStillExact(Root, Cell, Job, Scaffold, Plan,
				Encoded, out Failure)) return true;
			RetirePrematureSchema(Root);
			return false;
		}

		private static bool ProjectionJobStillExact(KingdomConstructionJob Frozen, Cell Cell,
			GameObject Root, GameObject Scaffold, string Encoded)
		{
			if (Frozen == null || Cell == null || !KingdomConstruction.IsCurrent(Frozen)
				|| !KingdomConstruction.TryFind(Frozen.Id, out KingdomConstructionJob current)
				|| current.Id != Frozen.Id || current.OwnerKey != Frozen.OwnerKey
				|| current.ZoneId != Frozen.ZoneId || current.Route != Frozen.Route
				|| current.Phase != Frozen.Phase || current.Projection != Frozen.Projection
				|| current.X != Frozen.X || current.Y != Frozen.Y
				|| current.SubjectId != Frozen.SubjectId || current.SourceId != Frozen.SourceId
				|| current.OutputId != Frozen.OutputId
				|| current.PhysicalPhase != Frozen.PhysicalPhase
				|| current.PhysicalIndex != Frozen.PhysicalIndex
				|| current.PhysicalAmount != Frozen.PhysicalAmount
				|| current.PhysicalSpilled != Frozen.PhysicalSpilled
				|| current.PhysicalItemId != Frozen.PhysicalItemId
				|| current.PhysicalDestinationId != Frozen.PhysicalDestinationId
				|| current.PhysicalReceipt != Frozen.PhysicalReceipt
				|| current.TargetKey != Frozen.TargetKey || current.Payload != Frozen.Payload
				|| current.InputReceipt != Frozen.InputReceipt
				|| current.InputReceiptHash != Frozen.InputReceiptHash
				|| current.BuildTruthSchema != Frozen.BuildTruthSchema
				|| current.BuildHasPlot != Frozen.BuildHasPlot
				|| current.BuildFrontier != Frozen.BuildFrontier
				|| current.BuildDefence != Frozen.BuildDefence
				|| current.CreatedTick != Frozen.CreatedTick
				|| current.StartedTick != Frozen.StartedTick || current.DueTick != Frozen.DueTick
				|| current.UpdatedTick != Frozen.UpdatedTick || current.Revision != Frozen.Revision
				|| current.Failure != Frozen.Failure || current.Compacted != Frozen.Compacted
				|| current.CompactHash != Frozen.CompactHash || current.Outbox != null
				|| Frozen.Outbox != null || !SameProjectionClaims(current.Claims, Frozen.Claims))
				return false;
			return current.Phase == KingdomConstructionPhase.ProjectionPending
				&& current.Route == KingdomConstructionRoute.CommissionScaffold
				&& current.TargetKey == KingdomGatehouseRules.BuildKey
				&& current.Payload == Encoded && current.X == Cell.X && current.Y == Cell.Y
				&& current.OutputId == Root.IDIfAssigned
				&& current.SubjectId == Scaffold.IDIfAssigned;
		}

		private static bool SameProjectionClaims(KingdomConstructionClaims A,
			KingdomConstructionClaims B)
		{
			return A != null && B != null && A.WaterRequested == B.WaterRequested
				&& A.WaterSpent == B.WaterSpent && A.WaterOutstanding == B.WaterOutstanding
				&& A.WaterLost == B.WaterLost && A.Exact == B.Exact
				&& A.MaterialRequested == B.MaterialRequested
				&& A.MaterialSpent == B.MaterialSpent
				&& A.MaterialOutstanding == B.MaterialOutstanding
				&& A.MaterialLost == B.MaterialLost;
		}

		private static void RetirePrematureSchema(GameObject Root)
		{
			if (!GameObject.Validate(Root) || !Root.HasIntProperty(SchemaProperty)
				|| Root.HasStringProperty(SchemaProperty)
				|| Root.GetIntProperty(SchemaProperty) != Schema) return;
			Root.RemoveIntProperty(SchemaProperty);
		}

		private static InvalidOperationException ProjectionException(GameObject Root,
			string Failure)
		{
			string message = string.IsNullOrEmpty(Failure)
				? "The gatehouse projection lost exact physical evidence." : Failure;
			if (message.Length > 512) message = message.Substring(0, 512);
			if (GameObject.Validate(Root)) Root.SetStringProperty(ProjectionFaultProperty, message);
			return new InvalidOperationException(message);
		}
	}
}
