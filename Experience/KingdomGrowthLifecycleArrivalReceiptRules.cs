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
		private static bool GrowthArrivalDispositionStepShape(
			KingdomGrowthArrivalCandidate candidate, bool proved)
		{
			KingdomGrowthObjectCallbackStep step = candidate.DispositionStep;
			if (step == null || !GrowthObjectCallbackStepShape(step, candidate.Id,
				candidate.ObjectId, candidate.Marker, 1,
				candidate.Disposition == KingdomGrowthArrivalDisposition.NoAcceptableHome)
				|| step.FromLocation != KingdomGrowthLocationKind.Escrow
				|| !string.Equals(step.EscrowKey, candidate.EscrowKey, StringComparison.Ordinal)
				|| step.BeforeOwnerId != null || step.BeforeZoneId != null
				|| step.BeforeX != -1 || step.BeforeY != -1 || step.BeforeCount != 1
				|| (candidate.Disposition == KingdomGrowthArrivalDisposition.Joined
					? step.Kind != KingdomGrowthObjectMutationKind.CellAdd
						|| step.ToLocation != KingdomGrowthLocationKind.Cell
						|| step.AfterOwnerId != null
						|| !string.Equals(step.AfterZoneId, candidate.LodgingZoneId,
							StringComparison.Ordinal)
						|| step.AfterX != candidate.LodgingX || step.AfterY != candidate.LodgingY
						|| step.AfterCount != 1 || !step.NoStack
					: step.Kind != KingdomGrowthObjectMutationKind.Obliterate
						|| step.ToLocation != KingdomGrowthLocationKind.Graveyard
						|| step.AfterOwnerId != null || step.AfterZoneId != null
						|| step.AfterX != -1 || step.AfterY != -1 || step.AfterCount != 0
						|| step.NoStack)) return false;
			return proved ? step.State == KingdomLifecyclePhysicalState.Proved
				&& step.ReceiptSameReference == (candidate.Disposition ==
					KingdomGrowthArrivalDisposition.Joined)
				&& string.Equals(step.ReceiptProofId,
					GrowthArrivalCandidateCallbackProof(candidate, step, 1),
					StringComparison.Ordinal)
				: step.State == KingdomLifecyclePhysicalState.Intent;
		}

		private static bool GrowthObjectCallbackSettledForCandidate(
			KingdomGrowthArrivalCandidate candidate)
		{
			return candidate.DispositionStep != null
				&& candidate.DispositionStep.State == KingdomLifecyclePhysicalState.Proved
				&& candidate.DispositionStep.ReceiptState == KingdomLifecyclePhysicalState.Proved;
		}

		private static bool GrowthFieldRowsValid(KingdomGrowthBook book)
		{
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < book.FieldOps.Count; i++)
			{
				KingdomGrowthFieldSlot field = book.FieldOps[i];
				if (field == null || !ValidRootId(field.FieldId) || !ids.Add(field.FieldId)
					|| !CounterShape(field.NextSequence, field.RetiredThrough) || field.ClockTick < 0L
					|| field.CommitRevision < 0L || field.NextStageTick < 0L || field.SownTick < 0L
					|| field.Cycles < 0 || TooLong(field.Fault, MaxTextChars)
					|| !GrowthFieldAuthorityShape(field)) return false;
				if (field.Quarantined)
				{
					if (string.IsNullOrEmpty(field.Fault)
						|| !GrowthOperationEvidenceBounded(field.Operation)) return false;
				}
				else if (field.Fault != null) return false;
			}
			return true;
		}

		private static bool GrowthFieldAuthorityShape(KingdomGrowthFieldSlot field)
		{
			bool dormant = field.WorkObjectId == null && field.WorkPartId == null
				&& field.Marker == null && field.Blueprint == null && field.ZoneId == null
				&& field.X == -1 && field.Y == -1 && field.CropBlueprint == null
				&& field.Stage == 0 && field.NextStageTick == 0L && field.SownTick == 0L
				&& field.Cycles == 0 && field.SaidWant == 0 && field.DeclaredRows == 0
				&& field.EffectivenessPercent == 0 && field.MethodPercent == 0
				&& !field.NoLarderAnnounced
				&& field.SeedBlueprint == null && field.PartGraphHash == null
				&& field.ObjectGraphHash == null && field.TopologyHash == null
				&& field.CommitRevision == 0L && field.LastOperationId == null;
			if (dormant) return true;
			return ValidRootId(field.WorkObjectId) && ValidRootId(field.WorkPartId)
				&& ValidRootId(field.Marker) && ValidName(field.Blueprint) && ValidName(field.ZoneId)
				&& field.X >= 0 && field.X <= MaxCoordinate && field.Y >= 0
				&& field.Y <= MaxCoordinate && ValidName(field.CropBlueprint)
				&& field.Stage >= 0 && field.Stage <= 255 && field.SaidWant >= 0
				&& field.SaidWant <= 4 && field.DeclaredRows >= 0
				&& field.DeclaredRows <= MaxGrowthCropRows
				&& field.EffectivenessPercent > 0 && field.EffectivenessPercent <= 100
				&& field.MethodPercent >= 100
				&& field.MethodPercent <= KingdomResearchRules.MaxMethodPercent
				&& ValidName(field.SeedBlueprint)
				&& GrowthWitnessHash(field.PartGraphHash)
				&& GrowthWitnessHash(field.ObjectGraphHash)
				&& GrowthWitnessHash(field.TopologyHash)
				&& (field.LastOperationId == null || ValidGeneratedId(field.LastOperationId));
		}

		private static bool GrowthCropRowsValid(KingdomGrowthBook book)
		{
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> objects = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> markers = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < book.CropRows.Count; i++)
			{
				KingdomGrowthCropRow row = book.CropRows[i];
				if (!GrowthCropRowShape(book, row, false) || !ids.Add(row.RowId)
					|| !objects.Add(row.ObjectId) || !markers.Add(row.Marker)) return false;
			}
			return true;
		}

		private static bool GrowthResourceRowsValid(KingdomGrowthBook book)
		{
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < book.Resources.Count; i++)
				if (!GrowthResourceShape(book.Resources[i]) || !keys.Add(book.Resources[i].Key))
					return false;
			return true;
		}

		private static bool GrowthActiveResourcesValid(KingdomGrowthBook book)
		{
			Dictionary<string, string> expected = new Dictionary<string, string>(StringComparer.Ordinal);
			List<KingdomGrowthOperation> operations = new List<KingdomGrowthOperation>();
			if (book.HeartbeatOp != null) operations.Add(book.HeartbeatOp);
			if (book.ArrivalOp != null) operations.Add(book.ArrivalOp);
			if (book.DepartureOp != null) operations.Add(book.DepartureOp);
			if (book.DeliveryOp != null) operations.Add(book.DeliveryOp);
			if (book.FetchOp != null) operations.Add(book.FetchOp);
			if (book.MillOp != null) operations.Add(book.MillOp);
			for (int i = 0; i < book.FieldOps.Count; i++)
				if (book.FieldOps[i].Operation != null && (!book.FieldOps[i].Quarantined
					|| ValidHashNamespace(book.FieldOps[i].Operation.PlanHash, "growth-plan")))
					operations.Add(book.FieldOps[i].Operation);
			for (int i = 0; i < operations.Count; i++)
			{
				KingdomGrowthOperation operation = operations[i];
				List<KingdomLifecycleResourceLease> leases = GrowthLeases(operation);
				if (leases == null) return false;
				for (int j = 0; j < leases.Count; j++)
				{
					KingdomLifecycleResourceLease lease = leases[j];
					KingdomLifecycleResourceRevision row = FindGrowthResource(book, lease.Key);
					if (!GrowthResourceMatches(row, lease) || expected.ContainsKey(lease.Key)
						|| !string.Equals(row.ActiveOperationId, operation.Id,
							StringComparison.Ordinal)) return false;
					if (lease.State == KingdomLifecycleLeaseState.Proved)
					{
						if (row.Revision != lease.AfterRevision || !string.Equals(row.LastOperationId,
							operation.Id, StringComparison.Ordinal)) return false;
					}
					else if (lease.State == KingdomLifecycleLeaseState.Prepared
						|| lease.State == KingdomLifecycleLeaseState.Intent)
					{
						if (row.Revision != lease.BeforeRevision || string.Equals(row.LastOperationId,
							operation.Id, StringComparison.Ordinal)) return false;
					}
					else return false;
					expected.Add(lease.Key, operation.Id);
				}
			}
			if (book.ArrivalCandidate != null)
			{
				KingdomLifecycleResourceLease[] candidateLeases =
				{
					book.ArrivalCandidate.CandidateLease,
					book.ArrivalCandidate.LodgingLease,
					book.ArrivalCandidate.EscrowLease
				};
				for (int i = 0; i < candidateLeases.Length; i++)
				{
					KingdomLifecycleResourceLease lease = candidateLeases[i];
					KingdomLifecycleResourceRevision row = FindGrowthResource(book,
						lease == null ? null : lease.Key);
					if (!GrowthResourceMatches(row, lease) || expected.ContainsKey(lease.Key)
						|| !string.Equals(row.ActiveOperationId, book.ArrivalCandidate.Id,
							StringComparison.Ordinal)) return false;
					if (lease.State == KingdomLifecycleLeaseState.Proved)
					{
						if (row.Revision != lease.AfterRevision || !string.Equals(
							row.LastOperationId, book.ArrivalCandidate.Id,
							StringComparison.Ordinal)) return false;
					}
					else if (lease.State == KingdomLifecycleLeaseState.Prepared
						|| lease.State == KingdomLifecycleLeaseState.Intent)
					{
						if (row.Revision != lease.BeforeRevision || string.Equals(
							row.LastOperationId, book.ArrivalCandidate.Id,
							StringComparison.Ordinal)) return false;
					}
					else return false;
					expected.Add(lease.Key, book.ArrivalCandidate.Id);
				}
			}
			for (int i = 0; i < book.Resources.Count; i++)
			{
				KingdomLifecycleResourceRevision row = book.Resources[i];
				if (string.IsNullOrEmpty(row.ActiveOperationId)) continue;
				if (!expected.TryGetValue(row.Key, out string operationId)
					|| !string.Equals(operationId, row.ActiveOperationId, StringComparison.Ordinal))
					return false;
			}
			return true;
		}

		private static bool GrowthActiveIdentityClaimsValid(KingdomGrowthBook book,
			KingdomGrowthOperation candidate)
		{
			if (book == null) return false;
			KingdomGrowthOperation arrivalOwner = book.ArrivalOp;
			if (arrivalOwner == null && candidate != null
				&& candidate.Action == KingdomGrowthAction.Arrival)
				arrivalOwner = candidate;
			Dictionary<string, string> claims =
				new Dictionary<string, string>(StringComparer.Ordinal);
			if (!ClaimGrowthOperationIdentities(claims, book.HeartbeatOp)
				|| !ClaimGrowthOperationIdentities(claims, book.ArrivalOp)
				|| !ClaimGrowthOperationIdentities(claims, book.DepartureOp)
				|| !ClaimGrowthOperationIdentities(claims, book.DeliveryOp)
				|| !ClaimGrowthOperationIdentities(claims, book.FetchOp)
				|| !ClaimGrowthOperationIdentities(claims, book.MillOp)
				|| !ClaimGrowthArrivalCandidateIdentities(claims,
					book.ArrivalCandidate, arrivalOwner)) return false;
			for (int i = 0; i < book.FieldOps.Count; i++)
			{
				KingdomGrowthFieldSlot field = book.FieldOps[i];
				if (field == null || field.Operation == null) continue;
				if (field.Quarantined
					&& !ValidHashNamespace(field.Operation.PlanHash, "growth-plan")) continue;
				if (!ClaimGrowthOperationIdentities(claims, field.Operation)) return false;
			}
			if (candidate != null && (!GrowthOperationAlreadyPresent(book, candidate)
				&& !ClaimGrowthOperationIdentities(claims, candidate))) return false;
			for (int i = 0; i < book.CropRows.Count; i++)
			{
				KingdomGrowthCropRow row = book.CropRows[i];
				string owner = "crop-row:" + (row == null ? "?" : row.RowId);
				KingdomGrowthOperation fieldOperation = row == null ? null
					: GetGrowthOperation(book, KingdomGrowthSlotKind.Field, row.FieldId);
				if (candidate != null && IsGrowthFieldAction(candidate.Action)
					&& string.Equals(candidate.FieldId, row == null ? null : row.FieldId,
						StringComparison.Ordinal)) fieldOperation = candidate;
				if (fieldOperation != null && GrowthOperationUsesCropRow(fieldOperation, row))
					owner = fieldOperation.Id;
				if (row == null || !ClaimGrowthIdentity(claims, "object", row.ObjectId, owner)
					|| !ClaimGrowthIdentity(claims, "marker", row.Marker, owner)) return false;
			}
			return true;
		}

		private static bool GrowthOperationUsesCropRow(KingdomGrowthOperation operation,
			KingdomGrowthCropRow row)
		{
			return operation != null && row != null
				&& (GrowthObjectLegsUseCropRow(operation.Sources, row)
					|| GrowthObjectLegsUseCropRow(operation.Outputs, row));
		}

		private static bool GrowthObjectLegsUseCropRow(List<KingdomGrowthObjectLeg> legs,
			KingdomGrowthCropRow row)
		{
			if (legs == null) return false;
			for (int i = 0; i < legs.Count; i++)
				if (string.Equals(legs[i].ObjectId, row.ObjectId, StringComparison.Ordinal)
					&& string.Equals(legs[i].Marker, row.Marker, StringComparison.Ordinal)) return true;
			return false;
		}

	}
}
