using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		private static void ApplyGrowthFieldState(KingdomGrowthFieldSlot field,
			KingdomGrowthFieldState state)
		{
			field.WorkObjectId = state.WorkObjectId; field.WorkPartId = state.WorkPartId;
			field.Marker = state.Marker; field.Blueprint = state.Blueprint;
			field.ZoneId = state.ZoneId; field.X = state.X; field.Y = state.Y;
			field.CropBlueprint = state.CropBlueprint; field.Stage = state.Stage;
			field.NextStageTick = state.NextStageTick; field.SownTick = state.SownTick;
			field.Cycles = state.Cycles; field.SaidWant = state.SaidWant;
			field.DeclaredRows = state.DeclaredRows;
			field.EffectivenessPercent = state.EffectivenessPercent;
			field.MethodPercent = state.MethodPercent;
			field.NoLarderAnnounced = state.NoLarderAnnounced;
			field.SeedBlueprint = state.SeedBlueprint; field.PartGraphHash = state.PartGraphHash;
			field.ObjectGraphHash = state.ObjectGraphHash;
			field.TopologyHash = state.TopologyHash;
		}

		private static bool GrowthCropRowScalarShape(KingdomGrowthCropRow row,
			string fieldId, bool allowCreateDeclaration, KingdomGrowthOperation operation)
		{
			if (row == null || !string.Equals(row.FieldId, fieldId, StringComparison.Ordinal)
				|| !ValidRootId(row.RowId) || !ValidRootId(row.Marker)
				|| !ValidName(row.Blueprint) || !ValidName(row.ZoneId)
				|| !ValidRootId(row.OwnerId) || row.X < 0 || row.X > MaxCoordinate
				|| row.Y < 0 || row.Y > MaxCoordinate || row.Count <= 0
				|| row.Count > MaxPhysicalCount || !row.HasHarvestable || row.RegenTimer < 0
				|| !string.Equals(row.RegenTime, string.Empty, StringComparison.Ordinal)
				|| row.TileIndex < -1 || !GrowthBoundedPresentString(row.RenderTile)
				|| !GrowthBoundedPresentString(row.RenderColor)
				|| !GrowthBoundedPresentString(row.RenderDetail)
				|| !GrowthBoundedPresentString(row.RenderString)
				|| !GrowthBoundedPresentString(row.TileColor) || row.Revision < 0L
				|| (row.LastOperationId != null && !ValidGeneratedId(row.LastOperationId))) return false;
			if (row.ObjectId != null) return ValidRootId(row.ObjectId)
				&& GrowthWitnessHash(row.PartGraphHash) && GrowthWitnessHash(row.ObjectGraphHash)
				&& GrowthWitnessHash(row.TopologyHash);
			if (!allowCreateDeclaration || row.PartGraphHash != null
				|| row.ObjectGraphHash != null || row.TopologyHash != null
				|| operation == null || !string.Equals(row.LastOperationId, operation.Id,
					StringComparison.Ordinal)) return false;
			for (int i = 0; i < operation.Outputs.Count; i++)
			{
				KingdomGrowthObjectLeg output = operation.Outputs[i];
				if (output != null && output.MutationKind == KingdomGrowthObjectMutationKind.Create
					&& string.Equals(output.Marker, row.Marker, StringComparison.Ordinal)
					&& string.Equals(output.Blueprint, row.Blueprint, StringComparison.Ordinal)
					&& string.Equals(output.ZoneId, row.ZoneId, StringComparison.Ordinal)
					&& output.X == row.X && output.Y == row.Y && output.AfterCount == row.Count)
					return true;
			}
			return false;
		}

		private static bool GrowthCropRowsShape(List<KingdomGrowthCropRow> rows,
			string fieldId, bool allowCreateDeclaration, KingdomGrowthOperation operation)
		{
			if (rows == null || rows.Count > MaxGrowthCropRows) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> objects = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> markers = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomGrowthCropRow row = rows[i];
				if (!GrowthCropRowScalarShape(row, row == null ? null : row.FieldId,
					allowCreateDeclaration, operation) || !ids.Add(row.RowId)
					|| !markers.Add(row.Marker) || row.ObjectId != null && !objects.Add(row.ObjectId))
					return false;
			}
			return true;
		}

		private static bool GrowthCropRowEquals(KingdomGrowthCropRow a,
			KingdomGrowthCropRow b)
		{
			return a != null && b != null
				&& string.Equals(a.FieldId, b.FieldId, StringComparison.Ordinal)
				&& string.Equals(a.RowId, b.RowId, StringComparison.Ordinal)
				&& string.Equals(a.ObjectId, b.ObjectId, StringComparison.Ordinal)
				&& string.Equals(a.Marker, b.Marker, StringComparison.Ordinal)
				&& string.Equals(a.Blueprint, b.Blueprint, StringComparison.Ordinal)
				&& string.Equals(a.ZoneId, b.ZoneId, StringComparison.Ordinal)
				&& string.Equals(a.OwnerId, b.OwnerId, StringComparison.Ordinal)
				&& a.X == b.X && a.Y == b.Y && a.Count == b.Count
				&& a.HasHarvestable == b.HasHarvestable && a.Ripe == b.Ripe
				&& a.RegenTimer == b.RegenTimer
				&& string.Equals(a.RegenTime, b.RegenTime, StringComparison.Ordinal)
				&& a.TileIndex == b.TileIndex
				&& string.Equals(a.RenderTile, b.RenderTile, StringComparison.Ordinal)
				&& string.Equals(a.RenderColor, b.RenderColor, StringComparison.Ordinal)
				&& string.Equals(a.RenderDetail, b.RenderDetail, StringComparison.Ordinal)
				&& string.Equals(a.RenderString, b.RenderString, StringComparison.Ordinal)
				&& string.Equals(a.TileColor, b.TileColor, StringComparison.Ordinal)
				&& string.Equals(a.PartGraphHash, b.PartGraphHash, StringComparison.Ordinal)
				&& string.Equals(a.ObjectGraphHash, b.ObjectGraphHash, StringComparison.Ordinal)
				&& string.Equals(a.TopologyHash, b.TopologyHash, StringComparison.Ordinal)
				&& a.Revision == b.Revision
				&& string.Equals(a.LastOperationId, b.LastOperationId, StringComparison.Ordinal);
		}

		private static bool GrowthCropRowsEqual(List<KingdomGrowthCropRow> a,
			List<KingdomGrowthCropRow> b)
		{
			if (a == null || b == null || a.Count != b.Count) return false;
			for (int i = 0; i < a.Count; i++)
				if (!GrowthCropRowEquals(a[i], b[i])) return false;
			return true;
		}

		private static bool GrowthCropDeclarationMatchesObserved(
			KingdomGrowthOperation operation, List<KingdomGrowthCropRow> declared,
			List<KingdomGrowthCropRow> observed)
		{
			if (!GrowthCropRowsShape(observed, operation.FieldId, false, operation)
				|| declared == null || declared.Count != observed.Count) return false;
			for (int i = 0; i < declared.Count; i++)
			{
				KingdomGrowthCropRow plan = declared[i]; KingdomGrowthCropRow actual = observed[i];
				if (plan == null || actual == null) return false;
				if (plan.ObjectId != null)
				{
					if (!GrowthCropRowEquals(plan, actual)) return false;
					continue;
				}
				KingdomGrowthCropRow stable = CloneGrowthCropRow(actual);
				stable.ObjectId = null; stable.PartGraphHash = null;
				stable.ObjectGraphHash = null; stable.TopologyHash = null;
				if (!GrowthCropRowEquals(plan, stable)) return false;
				KingdomGrowthObjectLeg output = null;
				for (int j = 0; j < operation.Outputs.Count; j++)
					if (string.Equals(operation.Outputs[j].Marker, plan.Marker,
						StringComparison.Ordinal)) { output = operation.Outputs[j]; break; }
				if (output == null || output.State != KingdomLifecyclePhysicalState.Proved
					|| !string.Equals(output.ObjectId, actual.ObjectId, StringComparison.Ordinal)
					|| !string.Equals(output.ReceiptAfterObjectGraphHash,
						actual.ObjectGraphHash, StringComparison.Ordinal)
					|| !string.Equals(output.ReceiptAfterTopologyHash,
						actual.TopologyHash, StringComparison.Ordinal)) return false;
			}
			return true;
		}

		private static bool GrowthPublicationSnapshotsMatch(KingdomGrowthBook book,
			KingdomGrowthOperation operation, KingdomGrowthFieldSlot field)
		{
			if (operation.OptionState != book.OptionState || operation.OptionTick != book.OptionTick
				|| operation.HealthState != book.HealthState || operation.HealthTick != book.HealthTick
				|| operation.ScarcityOptionState != book.ScarcityOptionState
				|| operation.ScarcityOptionTick != book.ScarcityOptionTick
				|| operation.EffectiveWorkBefore != book.EffectiveWorkTick
				|| operation.HeartbeatBefore != book.LastHeartbeatTick
				|| operation.ArrivalBefore != book.NextArrivalTick
				|| operation.FetchBefore != book.LastFetchTick
				|| operation.MillBefore != book.LastMillTick
				|| operation.SubsidenceBefore != book.LastSubsidenceTick
				|| operation.DeliveryBefore != book.LastDeliveryTick
				|| operation.DepartureBefore != book.LastDepartureTick
				|| operation.PendingCropBefore != book.PendingCrop
				|| !string.Equals(operation.PendingCropBlueprintBefore, book.PendingCropBlueprint,
					StringComparison.Ordinal)
				|| !string.Equals(operation.PendingCropZoneIdBefore, book.PendingCropZoneId,
					StringComparison.Ordinal)) return false;
			if (operation.Action == KingdomGrowthAction.Arrival)
			{
				long after;
				if (book.ArrivalIntervalTicks <= 0L || operation.CreatedTick < book.NextArrivalTick
					|| !CheckedAdd(operation.CreatedTick, book.ArrivalIntervalTicks, out after)
					|| operation.ArrivalAfter != after) return false;
			}
			if (field != null && (operation.ClockLease.Before != field.CommitRevision
				|| operation.FieldClockBefore != field.ClockTick)) return false;
			for (int i = 0; i < operation.DomainSteps.Count; i++)
			{
				KingdomGrowthDomainStep step = operation.DomainSteps[i];
				if (step.Kind == KingdomGrowthDomainStepKind.Field
					&& !GrowthFieldMatchesState(field, step.FieldBefore)) return false;
				if (step.Kind == KingdomGrowthDomainStepKind.CropRegistry
					&& !GrowthCropRowsEqual(book.CropRows, step.CropRowsBefore)) return false;
			}
			return GrowthNonTargetScalarsFrozen(operation);
		}

		private static bool GrowthNonTargetScalarsFrozen(KingdomGrowthOperation operation)
		{
			if (operation.Action != KingdomGrowthAction.Heartbeat
				&& operation.HeartbeatAfter != operation.HeartbeatBefore) return false;
			if (operation.Action != KingdomGrowthAction.Arrival
				&& operation.ArrivalAfter != operation.ArrivalBefore) return false;
			if (operation.Action != KingdomGrowthAction.Fetch
				&& operation.FetchAfter != operation.FetchBefore) return false;
			if (operation.Action != KingdomGrowthAction.Mill
				&& operation.MillAfter != operation.MillBefore) return false;
			bool subsidence = operation.Action == KingdomGrowthAction.Departure
				&& operation.DepartureCauseKind == KingdomGrowthDepartureCauseKind.Subsidence;
			if (subsidence ? operation.SubsidenceAfter <= operation.SubsidenceBefore
				: operation.SubsidenceAfter != operation.SubsidenceBefore) return false;
			if (operation.Action != KingdomGrowthAction.Delivery
				&& operation.DeliveryAfter != operation.DeliveryBefore) return false;
			if (operation.Action != KingdomGrowthAction.Departure
				&& operation.DepartureAfter != operation.DepartureBefore) return false;
			if (!IsGrowthFieldAction(operation.Action)
				&& operation.EffectiveWorkAfter != operation.EffectiveWorkBefore) return false;
			if (operation.Action == KingdomGrowthAction.Heartbeat)
				return operation.PendingCropDelta == 0 && operation.PopulationDelta <= 0;
			if (operation.Action == KingdomGrowthAction.Delivery
				|| operation.Action == KingdomGrowthAction.Harvest)
				return operation.PopulationDelta == 0;
			if (operation.Action == KingdomGrowthAction.Arrival)
				return operation.PendingCropDelta == 0
					&& (operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.Joined
						? operation.PopulationDelta > 0 : operation.PopulationDelta == 0);
			if (operation.Action == KingdomGrowthAction.Departure)
				return operation.PendingCropDelta == 0 && operation.PopulationDelta < 0;
			return operation.PendingCropDelta == 0 && operation.PopulationDelta == 0;
		}

		private static bool IsGrowthFieldAction(KingdomGrowthAction action)
		{
			return action == KingdomGrowthAction.Sow || action == KingdomGrowthAction.Withdraw
				|| action == KingdomGrowthAction.Ripen || action == KingdomGrowthAction.Harvest
				|| action == KingdomGrowthAction.Irrigate;
		}

		private static List<KingdomLifecycleResourceLease> GrowthLeases(
			KingdomGrowthOperation operation)
		{
			if (operation == null || operation.ClockLease == null || operation.WaterLegs == null
				|| operation.Sources == null || operation.Outputs == null
				|| operation.DomainSteps == null) return null;
			List<KingdomLifecycleResourceLease> result =
				new List<KingdomLifecycleResourceLease>(1 + operation.WaterLegs.Count
					+ operation.Sources.Count + operation.Outputs.Count
					+ operation.DomainSteps.Count);
			for (int i = 0; i < operation.WaterLegs.Count; i++)
				if (operation.WaterLegs[i] == null || operation.WaterLegs[i].Lease == null) return null;
				else result.Add(operation.WaterLegs[i].Lease);
			for (int i = 0; i < operation.Sources.Count; i++)
				if (operation.Sources[i] == null || operation.Sources[i].Lease == null) return null;
				else result.Add(operation.Sources[i].Lease);
			for (int i = 0; i < operation.Outputs.Count; i++)
				if (operation.Outputs[i] == null || operation.Outputs[i].Lease == null) return null;
				else result.Add(operation.Outputs[i].Lease);
			for (int i = 0; i < operation.DomainSteps.Count; i++)
				if (operation.DomainSteps[i] == null || operation.DomainSteps[i].Lease == null) return null;
				else result.Add(operation.DomainSteps[i].Lease);
			result.Add(operation.ClockLease);
			return result;
		}

		private static bool GrowthResourceMatches(KingdomLifecycleResourceRevision row,
			KingdomLifecycleResourceLease lease)
		{
			return row != null && lease != null && row.Kind == lease.Kind
				&& string.Equals(row.ScopeId, lease.ScopeId, StringComparison.Ordinal)
				&& string.Equals(row.SubjectId, lease.SubjectId, StringComparison.Ordinal)
				&& string.Equals(row.Key, lease.Key, StringComparison.Ordinal);
		}

	}
}
