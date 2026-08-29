using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionRules
	{
		public static KingdomConstructionJob Transition(KingdomConstructionJob Job,
			KingdomConstructionPhase Phase, long Tick, string Failure = null)
		{
			KingdomConstructionJob next = Job.Copy();
			next.Phase = Phase;
			next.UpdatedTick = Tick >= next.UpdatedTick ? Tick : next.UpdatedTick;
			next.Revision = next.Revision < int.MaxValue ? next.Revision + 1 : next.Revision;
			next.Failure = Limit(Failure, MaxFailureChars);
			return next;
		}

		public static bool ValidRegistryUpdate(KingdomConstructionJob Current,
			KingdomConstructionJob Next)
		{
			if (!ValidJob(Current) || !ValidJob(Next) || Current.Revision == int.MaxValue
				|| Next.Revision != Current.Revision + 1 || Current.Id != Next.Id
				|| Current.OwnerKey != Next.OwnerKey || Current.ZoneId != Next.ZoneId
				|| Current.Route != Next.Route || Current.Projection != Next.Projection
				|| Current.X != Next.X || Current.Y != Next.Y
				|| Current.TargetKey != Next.TargetKey
				|| Current.BuildTruthSchema != Next.BuildTruthSchema
				|| Current.BuildHasPlot != Next.BuildHasPlot
				|| Current.BuildFrontier != Next.BuildFrontier
				|| Current.BuildDefence != Next.BuildDefence
				|| !ValidInputRegistryUpdate(Current, Next)
				|| (!string.IsNullOrEmpty(Current.InputReceiptHash)
					&& string.IsNullOrEmpty(Next.InputReceiptHash))
				|| Current.CreatedTick != Next.CreatedTick
				|| Next.UpdatedTick < Current.UpdatedTick
				|| (RequiresFullFunding(Next.Phase) && !FullyFundedExact(Next))
				|| !ValidPhaseUpdate(Current, Next)
				|| (IsTerminal(Current.Phase) && Next.Phase != Current.Phase)
				|| (Current.Compacted && !Next.Compacted)) return false;
			return true;
		}

		private static bool RequiresFullFunding(KingdomConstructionPhase Phase)
		{
			return Phase == KingdomConstructionPhase.Funded
				|| Phase == KingdomConstructionPhase.ProjectionPending
				|| Phase == KingdomConstructionPhase.Projected
				|| Phase == KingdomConstructionPhase.Working
				|| Phase == KingdomConstructionPhase.Complete;
		}

		private static bool ValidPhaseUpdate(KingdomConstructionJob Current,
			KingdomConstructionJob Next)
		{
			if (Next.Phase == Current.Phase) return true;
			if (IsTerminal(Current.Phase)) return false;
			if (IsRoutedCommitUpdate(Current, Next)
				|| IsRoutedCompensationUpdate(Current, Next)) return true;
			if (Next.Phase == KingdomConstructionPhase.InspectionRequired
				|| Next.Phase == KingdomConstructionPhase.Cancelled) return true;
			if (Next.Phase == KingdomConstructionPhase.Complete)
				return FullyFundedExact(Next)
					&& (Current.Phase == KingdomConstructionPhase.Funded
						|| Current.Phase == KingdomConstructionPhase.ProjectionPending
						|| Current.Phase == KingdomConstructionPhase.Projected
						|| Current.Phase == KingdomConstructionPhase.Working
						|| Current.Phase == KingdomConstructionPhase.Outstanding);
			switch (Current.Phase)
			{
				case KingdomConstructionPhase.Published:
					return Next.Phase == KingdomConstructionPhase.WaterPending;
				case KingdomConstructionPhase.WaterPending:
					return Next.Phase == KingdomConstructionPhase.WaterSettled;
				case KingdomConstructionPhase.WaterSettled:
					return Next.Phase == KingdomConstructionPhase.WaterPending
						|| Next.Phase == KingdomConstructionPhase.MaterialPending
						|| Next.Phase == KingdomConstructionPhase.Compensated;
				case KingdomConstructionPhase.MaterialPending:
					return Next.Phase == KingdomConstructionPhase.Funded
						|| Next.Phase == KingdomConstructionPhase.Outstanding
						|| Next.Phase == KingdomConstructionPhase.CompensationPending;
				case KingdomConstructionPhase.CompensationPending:
					return Next.Phase == KingdomConstructionPhase.Compensated
						|| Next.Phase == KingdomConstructionPhase.Outstanding;
				case KingdomConstructionPhase.Funded:
				case KingdomConstructionPhase.Projected:
				case KingdomConstructionPhase.Working:
					return Next.Phase == KingdomConstructionPhase.ProjectionPending
						|| Next.Phase == KingdomConstructionPhase.Working
						|| Next.Phase == KingdomConstructionPhase.Outstanding;
				case KingdomConstructionPhase.ProjectionPending:
					return Next.Phase == KingdomConstructionPhase.Projected
						|| Next.Phase == KingdomConstructionPhase.Working
						|| Next.Phase == KingdomConstructionPhase.Outstanding;
				case KingdomConstructionPhase.Outstanding:
					return Next.Phase == KingdomConstructionPhase.WaterPending
						|| Next.Phase == KingdomConstructionPhase.ProjectionPending
						|| Next.Phase == KingdomConstructionPhase.Working;
				default:
					return false;
			}
		}

		public static KingdomPhysicalLookupState PhysicalLookupState(int Count,
			bool ExactShape)
		{
			return Count == 0 ? KingdomPhysicalLookupState.Absent
				: Count == 1 && ExactShape ? KingdomPhysicalLookupState.Exact
				: KingdomPhysicalLookupState.Ambiguous;
		}

		/// <param name="InventoryOwner">0 none, 1 source, 2 destination, 3 other.</param>
		/// <param name="CellOwner">0 none, 1 exact destination, 2 other.</param>
		public static KingdomHandoverItemTopology HandoverItemTopology(int SourceRefs,
			int DestinationRefs, int CellRefs, int IdOccurrences, int ExactOccurrences,
			int InventoryOwner, int CellOwner)
		{
			if (SourceRefs < 0 || DestinationRefs < 0 || CellRefs < 0
				|| IdOccurrences < 0 || ExactOccurrences < 0 || ExactOccurrences > IdOccurrences
				|| InventoryOwner < 0 || InventoryOwner > 3 || CellOwner < 0 || CellOwner > 2)
				return KingdomHandoverItemTopology.Invalid;
			if (SourceRefs == 1 && DestinationRefs == 0 && CellRefs == 0
				&& IdOccurrences == 1 && ExactOccurrences == 1
				&& InventoryOwner == 1 && CellOwner == 0)
				return KingdomHandoverItemTopology.Source;
			if (SourceRefs == 0 && DestinationRefs == 1 && CellRefs == 0
				&& IdOccurrences == 1 && ExactOccurrences == 1
				&& InventoryOwner == 2 && CellOwner == 0)
				return KingdomHandoverItemTopology.DestinationInventory;
			if (SourceRefs == 0 && DestinationRefs == 0 && CellRefs == 1
				&& IdOccurrences == 1 && ExactOccurrences == 1
				&& InventoryOwner == 0 && CellOwner == 1)
				return KingdomHandoverItemTopology.DestinationCell;
			if (SourceRefs == 0 && DestinationRefs == 0 && CellRefs == 0
				&& IdOccurrences == 0 && ExactOccurrences == 0 && InventoryOwner == 0)
				return CellOwner == 0 ? KingdomHandoverItemTopology.Loose
					: CellOwner == 1 ? KingdomHandoverItemTopology.EnteringCell
					: KingdomHandoverItemTopology.Invalid;
			return KingdomHandoverItemTopology.Invalid;
		}

		public static bool ValidJob(KingdomConstructionJob Job)
		{
			Guid ignored;
			if (Job == null || !Guid.TryParseExact(Job.Id, "N", out ignored)
				|| !TextLength(Job.OwnerKey, 1, MaxOwnerChars)
				|| !TextLength(Job.ZoneId, 1, MaxZoneChars)
				|| Job.Route <= KingdomConstructionRoute.None || Job.Route > KingdomConstructionRoute.HostedArcology
				|| Job.Phase <= KingdomConstructionPhase.Invalid || Job.Phase > KingdomConstructionPhase.InspectionRequired
				|| Job.Projection != ProjectionFor(Job.Route)
				|| Job.X < -1 || Job.X > 1023 || Job.Y < -1 || Job.Y > 1023
				|| !TextLength(Job.SubjectId, 0, MaxSubjectChars)
				|| !TextLength(Job.SourceId, 0, MaxSubjectChars)
				|| !TextLength(Job.OutputId, 0, MaxSubjectChars)
				|| Job.PhysicalPhase < KingdomPhysicalPhase.None
				|| Job.PhysicalPhase > KingdomPhysicalPhase.CargoDelivered
				|| Job.PhysicalIndex < 0 || Job.PhysicalIndex > 4096
				|| Job.PhysicalAmount < 0 || Job.PhysicalSpilled < 0
				|| !TextLength(Job.PhysicalItemId, 0, MaxSubjectChars)
				|| !TextLength(Job.PhysicalDestinationId, 0, MaxSubjectChars)
				|| !TextLength(Job.PhysicalReceipt, 0, MaxPhysicalReceiptChars)
				|| !TextLength(Job.TargetKey, 0, MaxTargetChars)
				|| !TextLength(Job.Payload, 0, MaxPayloadChars)
				|| !TextLength(Job.InputReceipt, 0, MaxInputReceiptChars)
				|| !TextLength(Job.InputReceiptHash, 0, 64)
				|| !ValidBuildTruth(Job)
				|| !TextLength(Job.Failure, 0, MaxFailureChars)
				|| Job.CreatedTick < 0L || Job.StartedTick < Job.CreatedTick
				|| Job.DueTick < Job.StartedTick
				|| Job.UpdatedTick < Job.CreatedTick || Job.Revision < 1
				|| !ValidateClaims(Job.Claims) || !ValidOutbox(Job.Outbox))
			{
				return false;
			}
			bool hasInput = !string.IsNullOrEmpty(Job.InputReceipt);
			bool hasInputHash = !string.IsNullOrEmpty(Job.InputReceiptHash);
			if (hasInput && (!hasInputHash || !IsSha256(Job.InputReceiptHash)
				|| Job.InputReceiptHash != Sha256(Job.InputReceipt))) return false;
			KingdomConstructionInputReceipt input;
			if (hasInput && (!TryGetInputReceipt(Job, out input)
				|| !ValidInputReceipt(Job, input))) return false;
			if (Job.Compacted)
			{
				return IsTerminal(Job.Phase) && Job.Outbox == null
					&& string.IsNullOrEmpty(Job.Payload)
					&& string.IsNullOrEmpty(Job.PhysicalReceipt)
					&& string.IsNullOrEmpty(Job.InputReceipt)
					&& string.IsNullOrEmpty(Job.Failure)
					&& (!hasInputHash || IsSha256(Job.InputReceiptHash))
					&& IsSha256(Job.CompactHash) && Job.CompactHash == CompactIdentityHash(Job);
			}
			if (hasInputHash != hasInput) return false;
			if (!string.IsNullOrEmpty(Job.CompactHash)) return false;
			return true;
		}

		public static bool ValidateClaims(KingdomConstructionClaims Claims)
		{
			if (Claims == null || Claims.WaterRequested < 0 || Claims.WaterSpent < 0
				|| Claims.WaterOutstanding < 0 || Claims.WaterLost < 0
				|| (long)Claims.WaterSpent + Claims.WaterOutstanding != Claims.WaterRequested
				|| Claims.WaterLost < Claims.WaterSpent)
			{
				return false;
			}
			KingdomMaterialDebitCost requested;
			KingdomMaterialDebitCost spent;
			KingdomMaterialDebitCost outstanding;
			KingdomMaterialDebitCost lost;
			if (!KingdomMaterialDebitCost.TryParseClaim(Claims.MaterialRequested, out requested)
				|| !KingdomMaterialDebitCost.TryParseClaim(Claims.MaterialSpent, out spent)
				|| !KingdomMaterialDebitCost.TryParseClaim(Claims.MaterialOutstanding, out outstanding)
				|| !KingdomMaterialDebitCost.TryParseClaim(Claims.MaterialLost, out lost))
			{
				return false;
			}
			return SumMatches(requested, spent, outstanding) && Covers(lost, spent);
		}

	}
}
