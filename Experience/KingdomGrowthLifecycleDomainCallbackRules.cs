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
		internal static bool BeginGrowthDomainCallback(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int Ordinal)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation)
				|| Operation.Phase != KingdomGrowthPhase.DomainIntent
				|| Ordinal != Operation.DomainCursor || Ordinal < 0
				|| Ordinal >= Operation.DomainSteps.Count) return false;
			KingdomGrowthDomainStep step = Operation.DomainSteps[Ordinal];
			if (step.State != KingdomLifecyclePhysicalState.Prepared
				|| step.ReceiptState != KingdomLifecyclePhysicalState.Prepared
				|| step.Lease.State != KingdomLifecycleLeaseState.Prepared) return false;
			step.State = KingdomLifecyclePhysicalState.Intent;
			step.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			step.Lease.State = KingdomLifecycleLeaseState.Intent;
			step.ReceiptBeforeValue = step.BeforeValue;
			step.ReceiptBeforeGraphHash = step.BeforeGraphHash;
			step.ReceiptBeforeMapHash = step.BeforeMapHash;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			step.State = KingdomLifecyclePhysicalState.Prepared;
			step.ReceiptState = KingdomLifecyclePhysicalState.Prepared;
			step.Lease.State = KingdomLifecycleLeaseState.Prepared;
			step.ReceiptBeforeValue = 0L;
			step.ReceiptBeforeGraphHash = null; step.ReceiptBeforeMapHash = null;
			return false;
		}

		internal static bool CommitGrowthDomainCallback(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int Ordinal, long ObservedAfterValue,
			string ObservedAfterGraphHash, string ObservedAfterMapHash,
			KingdomGrowthFieldState ObservedFieldAfter = null,
			List<KingdomGrowthCropRow> ObservedCropRowsAfter = null)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation)
				|| Operation.Phase != KingdomGrowthPhase.DomainIntent
				|| Ordinal != Operation.DomainCursor || Ordinal < 0
				|| Ordinal >= Operation.DomainSteps.Count) return false;
			KingdomGrowthDomainStep step = Operation.DomainSteps[Ordinal];
			if (step.State != KingdomLifecyclePhysicalState.Intent
				|| step.ReceiptState != KingdomLifecyclePhysicalState.Intent
				|| step.Lease.State != KingdomLifecycleLeaseState.Intent
				|| ObservedAfterValue != step.AfterValue
				|| !string.Equals(ObservedAfterGraphHash, step.AfterGraphHash,
					StringComparison.Ordinal)
				|| !string.Equals(ObservedAfterMapHash, step.AfterMapHash,
					StringComparison.Ordinal)) return false;
			KingdomGrowthFieldSlot field = SlotForGrowthAction(Operation.Action)
				== KingdomGrowthSlotKind.Field ? FindGrowthField(Book, Operation.FieldId) : null;
			if (step.Kind == KingdomGrowthDomainStepKind.Field)
			{
				if (!GrowthFieldStateEquals(ObservedFieldAfter, step.FieldAfter)
					|| ObservedCropRowsAfter != null
					|| !GrowthFieldMatchesState(field, step.FieldBefore)) return false;
			}
			else if (step.Kind == KingdomGrowthDomainStepKind.CropRegistry)
			{
				if (ObservedFieldAfter != null
					|| !GrowthCropRowsEqual(Book.CropRows, step.CropRowsBefore)
					|| !GrowthCropDeclarationMatchesObserved(Operation,
						step.CropRowsDeclaredAfter, ObservedCropRowsAfter)) return false;
			}
			else if (ObservedFieldAfter != null || ObservedCropRowsAfter != null) return false;
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, step.Lease.Key);
			if (!GrowthResourceMatches(row, step.Lease)
				|| row.Revision != step.Lease.BeforeRevision
				|| !string.Equals(row.ActiveOperationId, Operation.Id,
					StringComparison.Ordinal)) return false;
			long oldRevision = row.Revision; string oldLast = row.LastOperationId;
			int oldPending = Book.PendingCrop;
			string oldPendingBlueprint = Book.PendingCropBlueprint;
			string oldPendingZone = Book.PendingCropZoneId;
			long oldSubsidence = Book.LastSubsidenceTick;
			KingdomGrowthFieldState oldField = field == null ? null : GrowthFieldState(field);
			List<KingdomGrowthCropRow> oldRows = new List<KingdomGrowthCropRow>(Book.CropRows);
			List<KingdomGrowthCropRow> oldStepRowsAfter = step.CropRowsAfter;
			if (step.Kind == KingdomGrowthDomainStepKind.Field)
				ApplyGrowthFieldState(field, ObservedFieldAfter);
			if (step.Kind == KingdomGrowthDomainStepKind.CropRegistry)
			{
				step.CropRowsAfter = CloneGrowthCropRows(ObservedCropRowsAfter);
				Book.CropRows.Clear();
				Book.CropRows.AddRange(CloneGrowthCropRows(ObservedCropRowsAfter));
			}
			step.State = KingdomLifecyclePhysicalState.Proved;
			step.ReceiptState = KingdomLifecyclePhysicalState.Proved;
			step.Lease.State = KingdomLifecycleLeaseState.Proved;
			step.ReceiptAfterValue = ObservedAfterValue;
			step.ReceiptAfterGraphHash = ObservedAfterGraphHash;
			step.ReceiptAfterMapHash = ObservedAfterMapHash;
			step.ReceiptProofId = GrowthDomainReceiptProof(Operation, step, Ordinal);
			row.Revision = step.Lease.AfterRevision; row.LastOperationId = Operation.Id;
			Operation.DomainCursor++;
			if (step.Kind == KingdomGrowthDomainStepKind.PendingCrop)
			{
				Book.PendingCrop = Operation.PendingCropAfter;
				Book.PendingCropBlueprint = Operation.PendingCropBlueprintAfter;
				Book.PendingCropZoneId = Operation.PendingCropZoneIdAfter;
			}
			if (step.Kind == KingdomGrowthDomainStepKind.SubsidenceSchedule)
				Book.LastSubsidenceTick = Operation.SubsidenceAfter;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			Operation.DomainCursor--; row.Revision = oldRevision; row.LastOperationId = oldLast;
			Book.PendingCrop = oldPending; Book.PendingCropBlueprint = oldPendingBlueprint;
			Book.PendingCropZoneId = oldPendingZone; Book.LastSubsidenceTick = oldSubsidence;
			if (field != null && oldField != null) ApplyGrowthFieldState(field, oldField);
			Book.CropRows.Clear(); Book.CropRows.AddRange(oldRows);
			step.CropRowsAfter = oldStepRowsAfter;
			step.State = KingdomLifecyclePhysicalState.Intent;
			step.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			step.Lease.State = KingdomLifecycleLeaseState.Intent;
			step.ReceiptAfterValue = 0L; step.ReceiptAfterGraphHash = null;
			step.ReceiptAfterMapHash = null; step.ReceiptProofId = null;
			return false;
		}

		private static KingdomGrowthScarcitySnapshot CloneGrowthScarcity(
			KingdomGrowthScarcitySnapshot x)
		{
			if (x == null) return null;
			return new KingdomGrowthScarcitySnapshot
			{
				DryStreak = x.DryStreak, Withered = x.Withered,
				HungerStreak = x.HungerStreak, Famished = x.Famished,
				LastMeal = x.LastMeal, MealShade = x.MealShade,
				ScrapsAnnounced = x.ScrapsAnnounced, ElapsedTicks = x.ElapsedTicks,
				Days = x.Days, Population = x.Population, Stage = x.Stage,
				UpkeepRequested = x.UpkeepRequested, WaterAvailable = x.WaterAvailable,
				RationsAvailable = x.RationsAvailable, Foraged = x.Foraged, Eaten = x.Eaten,
				FromDish = x.FromDish, Kitchens = x.Kitchens, DishName = x.DishName,
				DishText = x.DishText, DishStaple = x.DishStaple, DishSource = x.DishSource,
				ComposedBite = x.ComposedBite, RequestedWater = x.RequestedWater,
				ProvedWater = x.ProvedWater, RequestedRations = x.RequestedRations,
				ProvedRations = x.ProvedRations, StoresPolicy = x.StoresPolicy,
				DistrictPercent = x.DistrictPercent, ThirstOutcome = x.ThirstOutcome,
				HungerOutcome = x.HungerOutcome, Thirsting = x.Thirsting,
				Starving = x.Starving, Withering = x.Withering,
				Famishing = x.Famishing, Healthy = x.Healthy
			};
		}

		private static KingdomGrowthAccountingSnapshot CloneGrowthAccounting(
			KingdomGrowthAccountingSnapshot x)
		{
			if (x == null) return null;
			return new KingdomGrowthAccountingSnapshot
			{
				Fetched = x.Fetched, UpkeepDrawn = x.UpkeepDrawn, ArrivalCost = x.ArrivalCost,
				Delivered = x.Delivered, Harvested = x.Harvested, Foraged = x.Foraged,
				RationsDrawn = x.RationsDrawn, Milled = x.Milled,
				HarvestLost = x.HarvestLost, Plundered = x.Plundered,
				Arrivals = x.Arrivals, Departures = x.Departures
			};
		}

		private static KingdomGrowthFieldState CloneGrowthFieldState(KingdomGrowthFieldState x)
		{
			if (x == null) return null;
			return new KingdomGrowthFieldState
			{
				FieldId = x.FieldId, WorkObjectId = x.WorkObjectId,
				WorkPartId = x.WorkPartId, Marker = x.Marker, Blueprint = x.Blueprint,
				ZoneId = x.ZoneId, X = x.X, Y = x.Y, CropBlueprint = x.CropBlueprint,
				Stage = x.Stage, NextStageTick = x.NextStageTick, SownTick = x.SownTick,
				Cycles = x.Cycles, SaidWant = x.SaidWant, DeclaredRows = x.DeclaredRows,
				EffectivenessPercent = x.EffectivenessPercent,
				MethodPercent = x.MethodPercent,
				NoLarderAnnounced = x.NoLarderAnnounced, SeedBlueprint = x.SeedBlueprint,
				PartGraphHash = x.PartGraphHash, ObjectGraphHash = x.ObjectGraphHash,
				TopologyHash = x.TopologyHash
			};
		}

		private static KingdomGrowthCropRow CloneGrowthCropRow(KingdomGrowthCropRow x)
		{
			if (x == null) return null;
			return new KingdomGrowthCropRow
			{
				FieldId = x.FieldId, RowId = x.RowId, ObjectId = x.ObjectId,
				Marker = x.Marker, Blueprint = x.Blueprint, ZoneId = x.ZoneId,
				OwnerId = x.OwnerId, X = x.X, Y = x.Y, Count = x.Count,
				HasHarvestable = x.HasHarvestable, Ripe = x.Ripe,
				RegenTimer = x.RegenTimer, RegenTime = x.RegenTime,
				TileIndex = x.TileIndex, RenderTile = x.RenderTile,
				RenderColor = x.RenderColor, RenderDetail = x.RenderDetail,
				RenderString = x.RenderString, TileColor = x.TileColor,
				PartGraphHash = x.PartGraphHash, ObjectGraphHash = x.ObjectGraphHash,
				TopologyHash = x.TopologyHash, Revision = x.Revision,
				LastOperationId = x.LastOperationId
			};
		}

		private static List<KingdomGrowthCropRow> CloneGrowthCropRows(
			List<KingdomGrowthCropRow> rows)
		{
			if (rows == null) return null;
			List<KingdomGrowthCropRow> clone = new List<KingdomGrowthCropRow>(rows.Count);
			for (int i = 0; i < rows.Count; i++) clone.Add(CloneGrowthCropRow(rows[i]));
			return clone;
		}

		private static KingdomGrowthFieldState GrowthFieldState(KingdomGrowthFieldSlot field)
		{
			if (field == null) return null;
			return new KingdomGrowthFieldState
			{
				FieldId = field.FieldId, WorkObjectId = field.WorkObjectId,
				WorkPartId = field.WorkPartId, Marker = field.Marker,
				Blueprint = field.Blueprint, ZoneId = field.ZoneId, X = field.X, Y = field.Y,
				CropBlueprint = field.CropBlueprint, Stage = field.Stage,
				NextStageTick = field.NextStageTick, SownTick = field.SownTick,
				Cycles = field.Cycles, SaidWant = field.SaidWant,
				DeclaredRows = field.DeclaredRows,
				EffectivenessPercent = field.EffectivenessPercent,
				MethodPercent = field.MethodPercent,
				NoLarderAnnounced = field.NoLarderAnnounced,
				SeedBlueprint = field.SeedBlueprint, PartGraphHash = field.PartGraphHash,
				ObjectGraphHash = field.ObjectGraphHash, TopologyHash = field.TopologyHash
			};
		}

		private static bool GrowthFieldStateShape(KingdomGrowthFieldState state,
			string fieldId)
		{
			if (state == null || !string.Equals(state.FieldId, fieldId, StringComparison.Ordinal)
				|| state.NextStageTick < 0L || state.SownTick < 0L || state.Cycles < 0
				|| state.SaidWant < 0 || state.SaidWant > 4 || state.DeclaredRows < 0
				|| state.DeclaredRows > MaxGrowthCropRows) return false;
			bool dormant = state.WorkObjectId == null && state.WorkPartId == null
				&& state.Marker == null && state.Blueprint == null && state.ZoneId == null
				&& state.X == -1 && state.Y == -1 && state.CropBlueprint == null
				&& state.Stage == 0 && state.NextStageTick == 0L && state.SownTick == 0L
				&& state.Cycles == 0 && state.SaidWant == 0 && state.DeclaredRows == 0
				&& state.EffectivenessPercent == 0 && state.MethodPercent == 0
				&& !state.NoLarderAnnounced && state.SeedBlueprint == null
				&& state.PartGraphHash == null && state.ObjectGraphHash == null
				&& state.TopologyHash == null;
			if (dormant) return true;
			return ValidRootId(state.WorkObjectId) && ValidRootId(state.WorkPartId)
				&& ValidRootId(state.Marker) && ValidName(state.Blueprint)
				&& ValidName(state.ZoneId) && state.X >= 0 && state.X <= MaxCoordinate
				&& state.Y >= 0 && state.Y <= MaxCoordinate
				&& ValidName(state.CropBlueprint) && state.Stage >= 0 && state.Stage <= 255
				&& state.EffectivenessPercent > 0 && state.EffectivenessPercent <= 100
				&& state.MethodPercent >= 100
				&& state.MethodPercent <= KingdomResearchRules.MaxMethodPercent
				&& ValidName(state.SeedBlueprint) && GrowthWitnessHash(state.PartGraphHash)
				&& GrowthWitnessHash(state.ObjectGraphHash)
				&& GrowthWitnessHash(state.TopologyHash);
		}

		private static bool GrowthFieldStateEquals(KingdomGrowthFieldState a,
			KingdomGrowthFieldState b)
		{
			return a != null && b != null
				&& string.Equals(a.FieldId, b.FieldId, StringComparison.Ordinal)
				&& string.Equals(a.WorkObjectId, b.WorkObjectId, StringComparison.Ordinal)
				&& string.Equals(a.WorkPartId, b.WorkPartId, StringComparison.Ordinal)
				&& string.Equals(a.Marker, b.Marker, StringComparison.Ordinal)
				&& string.Equals(a.Blueprint, b.Blueprint, StringComparison.Ordinal)
				&& string.Equals(a.ZoneId, b.ZoneId, StringComparison.Ordinal)
				&& a.X == b.X && a.Y == b.Y
				&& string.Equals(a.CropBlueprint, b.CropBlueprint, StringComparison.Ordinal)
				&& a.Stage == b.Stage && a.NextStageTick == b.NextStageTick
				&& a.SownTick == b.SownTick && a.Cycles == b.Cycles
				&& a.SaidWant == b.SaidWant && a.DeclaredRows == b.DeclaredRows
				&& a.EffectivenessPercent == b.EffectivenessPercent
				&& a.MethodPercent == b.MethodPercent
				&& a.NoLarderAnnounced == b.NoLarderAnnounced
				&& string.Equals(a.SeedBlueprint, b.SeedBlueprint, StringComparison.Ordinal)
				&& string.Equals(a.PartGraphHash, b.PartGraphHash, StringComparison.Ordinal)
				&& string.Equals(a.ObjectGraphHash, b.ObjectGraphHash, StringComparison.Ordinal)
				&& string.Equals(a.TopologyHash, b.TopologyHash, StringComparison.Ordinal);
		}

		private static bool GrowthFieldMatchesState(KingdomGrowthFieldSlot field,
			KingdomGrowthFieldState state)
		{
			return field != null && GrowthFieldStateEquals(GrowthFieldState(field), state);
		}

	}
}
