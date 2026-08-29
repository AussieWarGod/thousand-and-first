using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouse
	{
		/// <summary>Drive one already-paid gatehouse from durable per-slot evidence.</summary>
		internal static void MaterializeFromEnteredCell(GameObject Root, Cell Cell)
		{
			if (!GameObject.Validate(Root) || Cell == null || Root.CurrentCell != Cell) return;
			if (Root.GetIntProperty(SchemaProperty) == Schema
				&& !Root.HasStringProperty(SchemaProperty)) return;
			if (Root.GetIntProperty(SchemaProperty) != 0
				|| Root.HasStringProperty(SchemaProperty))
				throw ProjectionException(Root,
					"The gatehouse projection schema is malformed or unsupported.");

			if (!TryProjectionContext(Root, Cell, out KingdomConstructionJob job,
				out KingdomGatehousePlan plan, out GameObject scaffold,
				out string encoded, out IPart part,
				out string failure))
				throw ProjectionException(Root, failure);
			if (Root.HasIntProperty(PlanProperty))
				throw ProjectionException(Root,
					"The gatehouse's frozen plan property has the wrong value type.");
			string frozen = Root.GetStringProperty(PlanProperty);
			if (string.IsNullOrEmpty(frozen))
			{
				Root.SetStringProperty(PlanProperty, encoded);
				frozen = Root.GetStringProperty(PlanProperty);
			}
			if (frozen != encoded)
				throw ProjectionException(Root,
					"The gatehouse's durable plan differs from its paid construction receipt.");

			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
			{
				bool driven = TryDriveProjectionSlot(Root, Cell, job, scaffold,
					Cell.ParentZone, plan, encoded, part, i, out failure);
				if (!ProjectionAuthorityStillExact(Root, Cell, job, scaffold, plan,
					encoded, out string authorityFailure))
				{
					RetirePrematureSchema(Root);
					throw ProjectionException(Root, authorityFailure);
				}
				if (!driven) throw ProjectionException(Root, failure);
			}
			if (!ProjectionAuthorityStillExact(Root, Cell, job, scaffold, plan,
				encoded, out failure)
				|| !KingdomGatehouseRules.TryDecode(Root.GetStringProperty(PlanProperty),
					out KingdomGatehousePlan finalPlan)
				|| !AllProjectionSlotsSettled(Root, part)
				|| !TryExactSatellites(Root, Cell.ParentZone, finalPlan, out _, out failure))
				throw ProjectionException(Root, failure
					?? "The six paid gatehouse satellites did not settle exactly.");
			if (!TryRetireV1PendingProjectionCustody(Root, finalPlan, part))
				throw ProjectionException(Root,
					"The settled legacy gatehouse still retains callback custody.");
			if (finalPlan.ReceiptVersion == 1) part = null;
			if (!ProjectionAuthorityStillExact(Root, Cell, job, scaffold, finalPlan,
				encoded, out failure)
				|| !AllProjectionSlotsSettled(Root, part))
				throw ProjectionException(Root, failure
					?? "The gatehouse changed while callback custody was retired.");
			bool exactFinalBodies = TryExactSatellites(Root, Cell.ParentZone, finalPlan,
				out _, out failure);
			if (!exactFinalBodies)
				throw ProjectionException(Root, failure
					?? "The gatehouse bodies changed while callback custody was retired.");
			if (finalPlan.ReceiptVersion == 1
				&& (!TryProjectionStateCounts(Root, out int finalStateFields,
					out int finalSettledStates)
					|| !KingdomGatehouseProjectionRules.CanResumeLegacySchemaCut(
						false, false, false, false, finalStateFields,
						finalSettledStates, CanonicalPlan: true,
						SixUniqueStoredIds: true, ExactSixBodies: exactFinalBodies)))
				throw ProjectionException(Root,
					"The legacy carrier-removal cut cannot publish its schema.");

			Root.SetIntProperty(SchemaProperty, Schema); // final commit marker
			if (Root.HasStringProperty(SchemaProperty)
				|| Root.GetIntProperty(SchemaProperty) != Schema
				|| !TryReadPlan(Root, out KingdomGatehousePlan read, out failure)
				|| !ProjectionStateReceiptExact(Root, read, part)
				|| !TryExactSatellites(Root, Cell.ParentZone, read, out _, out failure))
				throw ProjectionException(Root, failure
					?? "The completed gatehouse receipt did not read back exactly.");
			Root.RemoveStringProperty(ProjectionFaultProperty);
		}

		/// <summary>Scaffold retry entry. Failure keeps predecessor, receipt and exact custody.</summary>
		internal static bool TryResumeProjection(GameObject Root, Cell Cell)
		{
			if (ProjectionComplete(Root, Cell?.ParentZone)) return true;
			try { MaterializeFromEnteredCell(Root, Cell); }
			catch (Exception) { return false; }
			return ProjectionComplete(Root, Cell?.ParentZone);
		}

		internal static bool ProjectionComplete(GameObject Root, Zone Z)
		{
			if (!GameObject.Validate(Root) || Z == null || Root.CurrentZone != Z
				|| Root.HasStringProperty(SchemaProperty)
				|| Root.GetIntProperty(SchemaProperty) != Schema
				|| !TryReadPlan(Root, out KingdomGatehousePlan plan, out _)
				|| !TryExactSatellites(Root, Z, plan, out _, out _)) return false;
			IPart part = plan.ReceiptVersion == 2
				? (IPart)Root.GetPart<r_KingdomGatehouseProjectionV2>() : null;
			return ProjectionStateReceiptExact(Root, plan, part);
		}

		private static bool ProjectionStateReceiptExact(GameObject Root,
			KingdomGatehousePlan Plan, IPart Part)
		{
			if (!GameObject.Validate(Root) || Plan == null) return false;
			if (Plan.ReceiptVersion == 1)
			{
				if (Part != null
					|| Root.GetPart<r_KingdomGatehouseProjectionV2>() != null
					|| Root.GetPart<r_KingdomGatehouseProjectionV1Pending>() != null) return false;
				bool anyState = false;
				bool allState = true;
				for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
				{
					string key = SatelliteStateProperty(i);
					if (Root.HasStringProperty(key)) return false;
					if (!Root.HasIntProperty(key)) allState = false;
					else
					{
						anyState = true;
						if (Root.GetIntProperty(key)
							!= (int)KingdomGatehouseSlotState.Settled) return false;
					}
				}
				// Old completion has no state fields; migrated pending-v1 completion has all six.
				return !anyState || allState;
			}
			if (Plan.ReceiptVersion != 2
				|| !ProjectionPartMatches(Root, Plan, Part, false)) return false;
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
			{
				string key = SatelliteStateProperty(i);
				if (Root.HasStringProperty(key) || !Root.HasIntProperty(key)
					|| Root.GetIntProperty(key)
						!= (int)KingdomGatehouseSlotState.Settled) return false;
			}
			return AllProjectionSlotsSettled(Root, Part);
		}

		private static bool TryProjectionContext(GameObject Root, Cell Cell,
			out KingdomConstructionJob Job, out KingdomGatehousePlan Plan,
			out GameObject Scaffold, out string Encoded,
			out IPart Part,
			out string Failure)
		{
			Job = null; Plan = null; Scaffold = null; Encoded = null;
			Part = null; Failure = null;
			string rootId = Root.IDIfAssigned;
			string receiptId = Root.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (string.IsNullOrEmpty(rootId) || string.IsNullOrEmpty(receiptId)
				|| Root.HasIntProperty(KingdomConstruction.ReceiptProperty)
				|| Root.HasIntProperty(KingdomUpgrade.BuildKeyProperty)
				|| Root.GetStringProperty(KingdomUpgrade.BuildKeyProperty)
					!= KingdomGatehouseRules.BuildKey
				|| !KingdomConstruction.TryFind(receiptId, out Job)
				|| Job.Phase != KingdomConstructionPhase.ProjectionPending
				|| Job.Route != KingdomConstructionRoute.CommissionScaffold
				|| Job.TargetKey != KingdomGatehouseRules.BuildKey
				|| Job.OutputId != rootId || Job.X != Cell.X || Job.Y != Cell.Y
				|| !KingdomConstruction.HasReceipt(Root, Job)
				|| !KingdomConstruction.PaidBuildMatches(Root, Job)
				|| !KingdomConstruction.IsCurrent(Job)
				|| !KingdomGatehouseRules.TryDecode(Job.Payload, out Plan)
				|| !TryPendingProjectionPart(Root, Plan, out Part)
				|| !KingdomGatehouseRules.TryEncode(Plan, out Encoded)
				|| !KingdomGatehouseRules.MaterialClaimMatches(Plan,
					Job.Claims?.MaterialRequested)
				|| !ExactRootPalette(Root, Plan))
			{
				Failure = "The gatehouse entered without its exact current paid construction plan.";
				return false;
			}
			Scaffold = FindExactScaffold(Cell, Job);
			if (!GameObject.Validate(Scaffold) || !ScaffoldMatches(Scaffold, Plan)
				|| !TryAudit(Cell.ParentZone, Plan, Root, Scaffold, out Failure))
			{
				Failure = Failure ?? "The gatehouse footprint changed before final projection.";
				return false;
			}
			return true;
		}

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
