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
		private static bool GrowthFieldActionAuthorityShape(KingdomGrowthOperation operation)
		{
			if (!IsGrowthFieldAction(operation.Action)) return true;
			KingdomGrowthDomainStep field = FindGrowthDomain(operation,
				KingdomGrowthDomainStepKind.Field);
			if (field == null || field.FieldBefore == null || field.FieldAfter == null) return false;
			if (operation.Action == KingdomGrowthAction.Irrigate)
				return GrowthFieldIdentityStable(field.FieldBefore, field.FieldAfter)
					&& GrowthOperationTargetsField(operation, field.FieldBefore);
			KingdomGrowthDomainStep registry = FindGrowthDomain(operation,
				KingdomGrowthDomainStepKind.CropRegistry);
			if (registry == null || registry.CropRowsBefore == null
				|| registry.CropRowsDeclaredAfter == null) return false;
			switch (operation.Action)
			{
			case KingdomGrowthAction.Sow:
				return GrowthSowAuthorityShape(operation, field, registry);
			case KingdomGrowthAction.Withdraw:
				return GrowthWithdrawAuthorityShape(operation, field, registry);
			case KingdomGrowthAction.Ripen:
				return GrowthRipenAuthorityShape(operation, field, registry);
			case KingdomGrowthAction.Harvest:
				return true; // The richer harvest oracle binds every row and callback above.
			default: return false;
			}
		}

		private static bool GrowthSowAuthorityShape(KingdomGrowthOperation operation,
			KingdomGrowthDomainStep field, KingdomGrowthDomainStep registry)
		{
			if (!GrowthFieldStateDormant(field.FieldBefore)
				|| !GrowthOperationTargetsField(operation, field.FieldAfter)
				|| operation.Sources.Count != 1 || operation.Sources[0].BeforeCount != 1
				|| operation.Sources[0].AfterCount != 0
				|| !string.Equals(field.FieldAfter.SeedBlueprint,
					operation.Sources[0].Blueprint, StringComparison.Ordinal)) return false;
			int beforeCount = GrowthRowsForField(registry.CropRowsBefore, operation.FieldId);
			int afterCount = GrowthRowsForField(registry.CropRowsDeclaredAfter, operation.FieldId);
			if (beforeCount != 0 || afterCount <= 0 || afterCount != operation.Outputs.Count
				|| field.FieldAfter.DeclaredRows != afterCount) return false;
			for (int i = 0; i < operation.Outputs.Count; i++)
			{
				KingdomGrowthObjectLeg output = operation.Outputs[i];
				KingdomGrowthCropRow row = FindGrowthCropRowByMarker(
					registry.CropRowsDeclaredAfter, output.Marker);
				if (output.BeforeCount != 0 || output.AfterCount != 1 || row == null
					|| !string.Equals(row.FieldId, operation.FieldId, StringComparison.Ordinal)
					|| row.ObjectId != null || row.Count != 1
					|| !string.Equals(row.Blueprint, output.Blueprint, StringComparison.Ordinal)
					|| !string.Equals(row.Blueprint, field.FieldAfter.CropBlueprint,
						StringComparison.Ordinal)) return false;
			}
			return GrowthNonTargetCropRowsStable(registry, operation.FieldId);
		}

		private static bool GrowthWithdrawAuthorityShape(KingdomGrowthOperation operation,
			KingdomGrowthDomainStep field, KingdomGrowthDomainStep registry)
		{
			if (!GrowthOperationTargetsField(operation, field.FieldBefore)
				|| !GrowthFieldStateDormant(field.FieldAfter)
				|| GrowthRowsForField(registry.CropRowsDeclaredAfter, operation.FieldId) != 0
				|| operation.Sources.Count != GrowthRowsForField(registry.CropRowsBefore,
					operation.FieldId) || operation.Outputs.Count > 1) return false;
			for (int i = 0; i < operation.Sources.Count; i++)
			{
				KingdomGrowthObjectLeg source = operation.Sources[i];
				KingdomGrowthCropRow row = FindGrowthCropRowByObject(registry.CropRowsBefore,
					source.ObjectId, source.Marker);
				if (row == null || source.MutationKind != KingdomGrowthObjectMutationKind.Obliterate
					|| source.BeforeCount != row.Count || source.AfterCount != 0) return false;
			}
			return (operation.Outputs.Count == 0 || operation.Outputs[0].BeforeCount == 0
				&& operation.Outputs[0].AfterCount == 1
				&& string.Equals(operation.Outputs[0].Blueprint, field.FieldBefore.SeedBlueprint,
					StringComparison.Ordinal))
				&& GrowthNonTargetCropRowsStable(registry, operation.FieldId);
		}

		private static bool GrowthRipenAuthorityShape(KingdomGrowthOperation operation,
			KingdomGrowthDomainStep field, KingdomGrowthDomainStep registry)
		{
			if (!GrowthOperationTargetsField(operation, field.FieldBefore)
				|| !GrowthFieldIdentityStable(field.FieldBefore, field.FieldAfter)
				|| operation.Sources.Count != GrowthRowsForField(registry.CropRowsBefore,
					operation.FieldId)) return false;
			for (int i = 0; i < registry.CropRowsBefore.Count; i++)
			{
				KingdomGrowthCropRow before = registry.CropRowsBefore[i];
				KingdomGrowthCropRow after = FindGrowthCropRow(registry.CropRowsDeclaredAfter,
					before.RowId);
				if (!string.Equals(before.FieldId, operation.FieldId, StringComparison.Ordinal))
				{
					if (!GrowthCropRowEquals(before, after)) return false;
					continue;
				}
				KingdomGrowthObjectLeg source = FindGrowthObjectLeg(operation.Sources,
					before.ObjectId, before.Marker);
				if (after == null || source == null || before.Ripe || !after.Ripe
					|| !GrowthHarvestableMutationMatches(before, after, source)) return false;
			}
			return registry.CropRowsBefore.Count == registry.CropRowsDeclaredAfter.Count;
		}

		private static bool GrowthOperationTargetsField(KingdomGrowthOperation operation,
			KingdomGrowthFieldState field)
		{
			return field != null
				&& string.Equals(operation.TargetId, field.WorkObjectId, StringComparison.Ordinal)
				&& string.Equals(operation.TargetMarker, field.Marker, StringComparison.Ordinal)
				&& string.Equals(operation.Blueprint, field.Blueprint, StringComparison.Ordinal)
				&& string.Equals(operation.ZoneId, field.ZoneId, StringComparison.Ordinal)
				&& operation.TargetX == field.X && operation.TargetY == field.Y;
		}

		private static bool GrowthFieldIdentityStable(KingdomGrowthFieldState before,
			KingdomGrowthFieldState after)
		{
			return before != null && after != null
				&& string.Equals(before.FieldId, after.FieldId, StringComparison.Ordinal)
				&& string.Equals(before.WorkObjectId, after.WorkObjectId, StringComparison.Ordinal)
				&& string.Equals(before.WorkPartId, after.WorkPartId, StringComparison.Ordinal)
				&& string.Equals(before.Marker, after.Marker, StringComparison.Ordinal)
				&& string.Equals(before.Blueprint, after.Blueprint, StringComparison.Ordinal)
				&& string.Equals(before.ZoneId, after.ZoneId, StringComparison.Ordinal)
				&& before.X == after.X && before.Y == after.Y;
		}

		private static bool GrowthFieldStateDormant(KingdomGrowthFieldState state)
		{
			return state != null && state.WorkObjectId == null && state.WorkPartId == null
				&& state.Marker == null && state.Blueprint == null && state.ZoneId == null
				&& state.X == -1 && state.Y == -1 && state.CropBlueprint == null
				&& state.Stage == 0 && state.NextStageTick == 0L && state.SownTick == 0L
				&& state.Cycles == 0 && state.SaidWant == 0 && state.DeclaredRows == 0
				&& state.EffectivenessPercent == 0 && state.MethodPercent == 0
				&& !state.NoLarderAnnounced && state.SeedBlueprint == null
				&& state.PartGraphHash == null && state.ObjectGraphHash == null
				&& state.TopologyHash == null;
		}

		private static int GrowthRowsForField(List<KingdomGrowthCropRow> rows, string fieldId)
		{
			int count = 0;
			for (int i = 0; i < rows.Count; i++)
				if (string.Equals(rows[i].FieldId, fieldId, StringComparison.Ordinal)) count++;
			return count;
		}

		private static KingdomGrowthCropRow FindGrowthCropRowByMarker(
			List<KingdomGrowthCropRow> rows, string marker)
		{
			KingdomGrowthCropRow found = null;
			for (int i = 0; i < rows.Count; i++)
				if (string.Equals(rows[i].Marker, marker, StringComparison.Ordinal))
				{
					if (found != null) return null; found = rows[i];
				}
			return found;
		}

		private static KingdomGrowthCropRow FindGrowthCropRowByObject(
			List<KingdomGrowthCropRow> rows, string objectId, string marker)
		{
			for (int i = 0; i < rows.Count; i++)
				if (string.Equals(rows[i].ObjectId, objectId, StringComparison.Ordinal)
					&& string.Equals(rows[i].Marker, marker, StringComparison.Ordinal)) return rows[i];
			return null;
		}

		private static bool GrowthNonTargetCropRowsStable(KingdomGrowthDomainStep registry,
			string fieldId)
		{
			int beforeTarget = GrowthRowsForField(registry.CropRowsBefore, fieldId);
			int afterTarget = GrowthRowsForField(registry.CropRowsDeclaredAfter, fieldId);
			if (registry.CropRowsDeclaredAfter.Count
				!= registry.CropRowsBefore.Count - beforeTarget + afterTarget) return false;
			for (int i = 0; i < registry.CropRowsBefore.Count; i++)
			{
				KingdomGrowthCropRow before = registry.CropRowsBefore[i];
				if (string.Equals(before.FieldId, fieldId, StringComparison.Ordinal)) continue;
				if (!GrowthCropRowEquals(before, FindGrowthCropRow(
					registry.CropRowsDeclaredAfter, before.RowId))) return false;
			}
			return true;
		}

		private static KingdomGrowthCropRow FindGrowthCropRow(
			List<KingdomGrowthCropRow> rows, string rowId)
		{
			KingdomGrowthCropRow found = null;
			if (rows == null) return null;
			for (int i = 0; i < rows.Count; i++)
				if (rows[i] != null && string.Equals(rows[i].RowId, rowId,
					StringComparison.Ordinal))
				{
					if (found != null) return null;
					found = rows[i];
				}
			return found;
		}

		private static KingdomGrowthObjectLeg FindGrowthObjectLeg(
			List<KingdomGrowthObjectLeg> legs, string objectId, string marker)
		{
			KingdomGrowthObjectLeg found = null;
			for (int i = 0; i < legs.Count; i++)
				if (legs[i] != null && string.Equals(legs[i].ObjectId, objectId,
					StringComparison.Ordinal) && string.Equals(legs[i].Marker, marker,
					StringComparison.Ordinal))
				{
					if (found != null) return null;
					found = legs[i];
				}
			return found;
		}

		private static bool GrowthHarvestableMutationMatches(KingdomGrowthCropRow before,
			KingdomGrowthCropRow after, KingdomGrowthObjectLeg leg)
		{
			if (leg.MutationKind != KingdomGrowthObjectMutationKind.HarvestableRipeSet
				|| leg.Callbacks == null || leg.Callbacks.Count != 1
				|| leg.BeforeCount != before.Count || leg.AfterCount != after.Count
				|| !string.Equals(leg.Blueprint, before.Blueprint, StringComparison.Ordinal)
				|| !string.Equals(leg.ZoneId, before.ZoneId, StringComparison.Ordinal)
				|| leg.X != before.X || leg.Y != before.Y) return false;
			KingdomGrowthObjectCallbackStep step = leg.Callbacks[0];
			return step.Kind == KingdomGrowthObjectMutationKind.HarvestableRipeSet
				&& step.BeforeHasHarvestable == before.HasHarvestable
				&& step.AfterHasHarvestable == after.HasHarvestable
				&& step.BeforeRipe == before.Ripe && step.AfterRipe == after.Ripe
				&& step.BeforeRegenTimer == before.RegenTimer
				&& step.AfterRegenTimer == after.RegenTimer
				&& string.Equals(step.BeforeRegenTime, before.RegenTime,
					StringComparison.Ordinal)
				&& string.Equals(step.AfterRegenTime, after.RegenTime,
					StringComparison.Ordinal)
				&& step.BeforeTileIndex == before.TileIndex && step.AfterTileIndex == after.TileIndex
				&& string.Equals(step.BeforeRenderTile, before.RenderTile, StringComparison.Ordinal)
				&& string.Equals(step.AfterRenderTile, after.RenderTile, StringComparison.Ordinal)
				&& string.Equals(step.BeforeRenderColor, before.RenderColor,
					StringComparison.Ordinal)
				&& string.Equals(step.AfterRenderColor, after.RenderColor,
					StringComparison.Ordinal)
				&& string.Equals(step.BeforeRenderDetail, before.RenderDetail,
					StringComparison.Ordinal)
				&& string.Equals(step.AfterRenderDetail, after.RenderDetail,
					StringComparison.Ordinal)
				&& string.Equals(step.BeforeRenderString, before.RenderString,
					StringComparison.Ordinal)
				&& string.Equals(step.AfterRenderString, after.RenderString,
					StringComparison.Ordinal)
				&& string.Equals(step.BeforeTileColor, before.TileColor,
					StringComparison.Ordinal)
				&& string.Equals(step.AfterTileColor, after.TileColor,
					StringComparison.Ordinal)
				&& string.Equals(step.BeforeObjectGraphHash, before.ObjectGraphHash,
					StringComparison.Ordinal)
				&& string.Equals(step.AfterObjectGraphHash, after.ObjectGraphHash,
					StringComparison.Ordinal)
				&& string.Equals(step.BeforeTopologyHash, before.TopologyHash,
					StringComparison.Ordinal)
				&& string.Equals(step.AfterTopologyHash, after.TopologyHash,
					StringComparison.Ordinal);
		}

	}
}
