using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public enum KingdomPlanReceiptShape : byte
	{
		Absent = 0,
		Exact = 1,
		Corrupt = 2
	}

	public static partial class KingdomConstructionRules
	{
		/// <summary>Only a clean terminal plan receipt may release or retry its marker.</summary>
		public static bool PlanMarkerCancellationSettled(KingdomConstructionJob Job)
		{
			if (!ValidJob(Job) || (Job.Route != KingdomConstructionRoute.PlanScaffold
				&& Job.Route != KingdomConstructionRoute.PlotPlan)
				|| (Job.Phase != KingdomConstructionPhase.Compensated
					&& Job.Phase != KingdomConstructionPhase.Cancelled)
				|| !Job.Claims.Exact || Job.Claims.WaterSpent != 0
				|| Job.Claims.WaterLost != 0 || !string.IsNullOrEmpty(Job.OutputId)
				|| Job.PhysicalPhase != KingdomPhysicalPhase.None || Job.PhysicalIndex != 0
				|| Job.PhysicalAmount != 0 || Job.PhysicalSpilled != 0
				|| !string.IsNullOrEmpty(Job.PhysicalItemId)
				|| !string.IsNullOrEmpty(Job.PhysicalDestinationId)
				|| !string.IsNullOrEmpty(Job.PhysicalReceipt)
				|| !string.IsNullOrEmpty(Job.InputReceipt)
				|| !string.IsNullOrEmpty(Job.InputReceiptHash) || Job.Outbox != null)
			{
				return false;
			}
			return KingdomMaterialDebitCost.TryParseClaim(Job.Claims.MaterialSpent,
				out KingdomMaterialDebitCost spent) && spent.IsEmpty
				&& KingdomMaterialDebitCost.TryParseClaim(Job.Claims.MaterialLost,
					out KingdomMaterialDebitCost lost) && lost.IsEmpty;
		}

		public static KingdomPlanReceiptShape PlanMarkerReceiptShape(bool HasString,
			string Receipt, bool HasInt)
		{
			if (!HasString && !HasInt) return string.IsNullOrEmpty(Receipt)
				? KingdomPlanReceiptShape.Absent : KingdomPlanReceiptShape.Corrupt;
			return HasString && !HasInt && !string.IsNullOrEmpty(Receipt)
				? KingdomPlanReceiptShape.Exact : KingdomPlanReceiptShape.Corrupt;
		}

		public static bool PlanMarkerNames(KingdomConstructionJob Job, string MarkerId)
		{
			return Job != null && !string.IsNullOrEmpty(MarkerId)
				&& (Job.SourceId == MarkerId || Job.SubjectId == MarkerId
					|| Job.OutputId == MarkerId || Job.PhysicalItemId == MarkerId
					|| Job.PhysicalDestinationId == MarkerId);
		}

		/// <summary>Legacy migration requires a valid registry with zero durable identity claims.</summary>
		public static bool PlanMarkerRegistryUnreferenced(IList<KingdomConstructionJob> Jobs,
			string MarkerId)
		{
			if (Jobs == null || string.IsNullOrEmpty(MarkerId)) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Jobs.Count; i++)
				if (!ValidJob(Jobs[i]) || !ids.Add(Jobs[i].Id)
					|| PlanMarkerNames(Jobs[i], MarkerId)) return false;
			return true;
		}

		/// <summary>Fails closed when any durable row names the marker without exact clean proof.</summary>
		public static bool PlanMarkerCancellationAllowed(IList<KingdomConstructionJob> Jobs,
			bool HasReceipt, string ReceiptId, string MarkerId, string OwnerKey,
			string ZoneId, string TargetKey, Func<KingdomConstructionJob, bool> RouteProof)
		{
			if (Jobs == null || string.IsNullOrEmpty(MarkerId) || string.IsNullOrEmpty(ZoneId)
				|| string.IsNullOrEmpty(OwnerKey) || string.IsNullOrEmpty(TargetKey)
				|| RouteProof == null || (HasReceipt && string.IsNullOrEmpty(ReceiptId)))
				return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			int named = 0;
			for (int i = 0; i < Jobs.Count; i++)
			{
				KingdomConstructionJob job = Jobs[i];
				if (!ValidJob(job) || !ids.Add(job.Id)) return false;
				if (!PlanMarkerNames(job, MarkerId)) continue;
				bool exact = job.SourceId == MarkerId && job.SubjectId == MarkerId
					&& job.OwnerKey == OwnerKey && job.ZoneId == ZoneId
					&& job.TargetKey == TargetKey && PlanMarkerCancellationSettled(job)
					&& RouteProof(job);
				if (!exact || ++named > 1 || (HasReceipt && job.Id != ReceiptId)) return false;
			}
			return !HasReceipt || named == 1;
		}

		public static bool PlanMarkerRouteCoordinatesValid(KingdomConstructionRoute Route,
			int StakeX, int StakeY, bool PlotReceiptExact, bool StakeOutsidePlot,
			int MainX, int MainY, int JobX, int JobY)
		{
			if (Route == KingdomConstructionRoute.PlanScaffold)
				return JobX == StakeX && JobY == StakeY;
			return Route == KingdomConstructionRoute.PlotPlan && PlotReceiptExact
				&& StakeOutsidePlot && JobX == MainX && JobY == MainY;
		}

		public static bool PlanMarkerDirectGroundProved(bool Valid, int Count, bool Stacker,
			bool InInventory, bool Equipped, bool SameZone, bool SameCell, int DirectReferences,
			KingdomPhysicalLookupState IdState, bool ExactReference)
		{
			return Valid && Count == 1 && !Stacker && !InInventory && !Equipped
				&& SameZone && SameCell && DirectReferences == 1
				&& IdState == KingdomPhysicalLookupState.Exact && ExactReference;
		}

		/// <summary>Engine callback return/throw is advisory; exact post-state is authority.</summary>
		public static bool PlanMarkerPlacementProved(bool DirectGround, bool FrozenBytesEqual)
		{
			return DirectGround && FrozenBytesEqual;
		}

		public static bool PlanMarkerPlacementCommitAllowed(bool DirectGround,
			bool FrozenBytesEqual, KingdomPlanReceiptShape ReceiptShape,
			bool RegistryUnreferenced, bool AuthoritySafe)
		{
			return PlanMarkerPlacementProved(DirectGround, FrozenBytesEqual)
				&& ReceiptShape == KingdomPlanReceiptShape.Absent
				&& RegistryUnreferenced && AuthoritySafe;
		}

		public static bool PlanMarkerCancellationRemovalProved(bool ExactReferenceValid,
			KingdomPhysicalLookupState IdState, bool RegistrySafe, bool AuthoritySafe)
		{
			return !ExactReferenceValid && IdState == KingdomPhysicalLookupState.Absent
				&& RegistrySafe && AuthoritySafe;
		}

		public static bool PlanMarkerSurvivorProved(bool DirectGround, bool FrozenBytesEqual,
			bool RegistrySafe, bool AuthoritySafe)
		{
			return DirectGround && FrozenBytesEqual && RegistrySafe && AuthoritySafe;
		}
	}
}
