using System;
using System.Collections.Generic;

using XRL;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		/// <summary>Plans exact realm sources and leaves only neutral central route rows.
		/// The caller must publish the returned parent receipt before activation.</summary>
		internal static bool TryPrepareRoutedInputReceipt(KingdomSystem System,
			KingdomConstructionJob Job, string RequiredObjectId, long Now,
			out KingdomConstructionInputReceipt Receipt, out string Failure)
		{
			return TryPrepareRoutedInputReceiptWithRequiredObjects(System, Job,
				string.IsNullOrEmpty(RequiredObjectId) ? new string[0]
					: new[] { RequiredObjectId }, Now, out Receipt, out Failure);
		}

		internal static bool TryPrepareRoutedInputReceiptWithRequiredObjects(KingdomSystem System,
			KingdomConstructionJob Job, IList<string> RequiredObjectIds, long Now,
			out KingdomConstructionInputReceipt Receipt, out string Failure)
		{
			Receipt = null;
			Failure = null;
			if (!ValidRoutedInputRequest(System, Job, RequiredObjectIds, Now, out Failure))
				return false;
			KingdomCityFault orphanFault;
			if (!KingdomCentralLogistics.TrySweepUnadoptedConstructionInputOwner(
				System, Job.Id, out orphanFault))
			{
				Failure = "A prior neutral routed-input reservation cannot be recovered ("
					+ orphanFault + ").";
				return false;
			}
			int targetX, targetY;
			KingdomCityFault centralFault;
			if (!KingdomCentralLogistics.TryManifestSpillAnchor(System, Job.ZoneId,
				out targetX, out targetY, out centralFault))
			{
				Failure = "No exact construction-input landing anchor is observed ("
					+ centralFault + ").";
				return false;
			}
			KingdomConstructionInputLeaseSet leases;
			List<RoutedInputZone> zones;
			if (!TryInputLeases(out leases, out Failure)
				|| !TryInputZones(System, out zones, out Failure)) return false;
			List<KingdomConstructionInputCandidate> candidates;
			if (!TryScanInputCandidates(System, zones, leases, Job,
				RequiredObjectIds,
				Job.ZoneId, targetX, targetY, Now, out candidates, out Failure)) return false;
			KingdomConstructionInputPlan plan;
			KingdomConstructionInputPlanFault planFault;
			if (!KingdomConstructionInputPlanRules.TryPlanWithRequiredObjects(Job.Id,
				Job.Claims.WaterOutstanding, Job.Claims.MaterialOutstanding,
				RequiredObjectIds, candidates, out plan, out planFault))
			{
				Failure = "The exact realm sources cannot cover this construction input ("
					+ planFault + ").";
				return false;
			}
			KingdomConstructionInputIntent intent;
			string intentDigest;
			if (!KingdomConstructionRules.TryInputIntent(Job, plan.WaterRequested,
				plan.MaterialRequestedClaim, out intent, out intentDigest))
			{
				Failure = "The construction input intent no longer matches its job.";
				return false;
			}

			List<int> reserved = new List<int>();
			List<KingdomConstructionInputChild> children =
				new List<KingdomConstructionInputChild>();
			for (int i = 0; i < plan.ChildCount; i++)
			{
				KingdomConstructionInputPlannedChild draft = plan.ChildAt(i);
				KingdomConstructionInputZoneObservation sourceObservation =
					InputObservation(zones, draft.SourceZoneId);
				KingdomManifestReservation reservation;
				if (sourceObservation == null
					|| !KingdomCentralLogistics.TryPrepareConstructionInputReservation(System,
						sourceObservation, Job.Id, draft.SourceObjectId, draft.SourceZoneId,
						draft.SourceX, draft.SourceY, null, Job.ZoneId, targetX, targetY,
						draft.CargoStart, draft.CargoCount, Now, out reservation,
						out centralFault) || reservation.JobIds.Length != 1)
				{
					Failure = "A frozen construction-input route could not be reserved ("
						+ centralFault + ").";
					CancelPreparedRows(System, Job.Id, reserved, ref Failure);
					return false;
				}
				reserved.Add(reservation.JobIds[0]);
				KingdomConstructionInputRouteProof proof;
				if (!KingdomCentralLogistics.TryDescribeConstructionInputReservation(System,
					Job.Id, reservation.JobIds[0], out proof, out centralFault)
					|| proof.CargoStart != draft.CargoStart
					|| proof.CargoCount != draft.CargoCount
					|| proof.SourceZoneId != draft.SourceZoneId
					|| proof.SourceObjectId != draft.SourceObjectId
					|| proof.SourceX != draft.SourceX || proof.SourceY != draft.SourceY
					|| proof.TargetZoneId != Job.ZoneId || proof.TargetX != targetX
					|| proof.TargetY != targetY)
				{
					Failure = "A reserved construction-input route failed exact reproval ("
						+ centralFault + ").";
					CancelPreparedRows(System, Job.Id, reserved, ref Failure);
					return false;
				}
				children.Add(ChildFromProof(i, proof));
			}

			string receiptId = "ci-" + KingdomConstructionInputRules.HashBytes(
				KingdomConstructionInputRules.StrictUtf8.GetBytes(Job.Id + "\0receipt"));
			if (!KingdomConstructionInputPlanRules.TryCreateReceipt(plan, receiptId,
				Job.OwnerKey, System.FoundedTick, Job.ZoneId, targetX, targetY, intentDigest,
				1, Job.Claims.WaterSpent, Job.Claims.WaterLost, Job.Claims.MaterialSpent,
				Job.Claims.MaterialLost, children, out Receipt, out planFault))
			{
				Failure = "The exact routed-input receipt could not be created (" + planFault + ").";
				CancelPreparedRows(System, Job.Id, reserved, ref Failure);
				return false;
			}
			return true;
		}

		internal static bool TryActivatePreparedRoutedInput(KingdomSystem System,
			KingdomConstructionInputReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (System == null || Receipt == null
				|| Receipt.TxPhase != KingdomConstructionInputTxPhase.ReservationPrepared)
			{
				Failure = "The neutral routed-input receipt is absent.";
				return false;
			}
			int[] jobs = new int[Receipt.ChildCount];
			int[] trips = new int[Receipt.ChildCount];
			long[] arrivals = new long[Receipt.ChildCount];
			for (int i = 0; i < Receipt.ChildCount; i++)
			{
				KingdomConstructionInputChild child = Receipt.ChildAt(i);
				if (!KingdomConstructionInputRules.TryEffectiveArrivalTick(child.ArrivalTick,
					Receipt.PausedTicks, out arrivals[i]))
				{
					Failure = "A paused routed-input arrival exceeds the exact clock range.";
					return false;
				}
				jobs[i] = child.JobId;
				trips[i] = child.TripId;
			}
			KingdomCityFault fault;
			if (!KingdomCentralLogistics.TryActivateConstructionInputReservations(System,
				Receipt.ConstructionJobId, Receipt.Schema, Receipt.PlanDigest,
				Receipt.Revision, jobs, trips, arrivals, out fault))
			{
				Failure = "Central logistics refused routed-input activation (" + fault + ").";
				return false;
			}
			return true;
		}

		internal static bool TryCancelPreparedRoutedInput(KingdomSystem System,
			KingdomConstructionInputReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (System == null || Receipt == null || Receipt.ChildCount < 1)
			{
				Failure = "The neutral routed-input receipt is absent.";
				return false;
			}
			List<int> jobs = new List<int>();
			for (int i = 0; i < Receipt.ChildCount; i++) jobs.Add(Receipt.ChildAt(i).JobId);
			CancelPreparedRows(System, Receipt.ConstructionJobId, jobs, ref Failure);
			return Failure == null;
		}

		private static bool ValidRoutedInputRequest(KingdomSystem system,
			KingdomConstructionJob job, string requiredObjectId, long now, out string failure)
		{
			return ValidRoutedInputRequest(system, job,
				string.IsNullOrEmpty(requiredObjectId) ? new string[0]
					: new[] { requiredObjectId }, now, out failure);
		}

		private static bool ValidRoutedInputRequest(KingdomSystem system,
			KingdomConstructionJob job, IList<string> requiredObjectIds, long now,
			out string failure)
		{
			failure = null;
			string[] required;
			if (system == null || !system.Founded || job == null || job.Claims == null
				|| !job.Claims.Exact || now < 0L || job.OwnerKey != OwnerOf(system)
				|| string.IsNullOrEmpty(job.Id) || string.IsNullOrEmpty(job.ZoneId)
				|| The.ZoneManager == null || The.ZoneManager.ActiveZone == null
				|| The.ZoneManager.ActiveZone.ZoneID != job.ZoneId
				|| system.ClaimedZones == null || !system.ClaimedZones.Contains(job.ZoneId)
				|| (job.Phase != KingdomConstructionPhase.Published
					&& job.Phase != KingdomConstructionPhase.Outstanding)
				|| !string.IsNullOrEmpty(job.InputReceipt)
				|| !string.IsNullOrEmpty(job.InputReceiptHash)
				|| !KingdomConstructionInputRules.TryRequiredObjectIds(
					requiredObjectIds, out required)
				|| !KingdomPurpose.RequiredFundingObjectsMatch(job, required))
			{
				failure = "The construction job is not eligible for routed input.";
				return false;
			}
			return true;
		}

		private static KingdomConstructionInputZoneObservation InputObservation(
			IList<RoutedInputZone> zones, string zoneId)
		{
			for (int i = 0; i < zones.Count; i++)
				if (zones[i].ZoneId == zoneId) return zones[i].Observation;
			return null;
		}

		private static KingdomConstructionInputChild ChildFromProof(int ordinal,
			KingdomConstructionInputRouteProof proof)
		{
			return new KingdomConstructionInputChild(ordinal, proof.JobId, proof.TripId,
				proof.CargoStart, proof.CargoCount,
				KingdomConstructionInputCargoShape.OpaqueObjectManifest,
				proof.SourceEndpointId, proof.SourceObjectId, proof.SourceZoneId,
				proof.SourceX, proof.SourceY, proof.TargetEndpointId, proof.TargetObjectId,
				proof.TargetZoneId, proof.TargetX, proof.TargetY, proof.ArrivalTick,
				proof.RouteDigest, (int)KingdomDeliveryPhase.ReservationPrepared, 0L);
		}

		private static void CancelPreparedRows(KingdomSystem system, string owner,
			IList<int> rows, ref string failure)
		{
			if (rows.Count == 0) return;
			int[] ids = new int[rows.Count];
			for (int i = 0; i < rows.Count; i++) ids[i] = rows[i];
			KingdomCityFault fault;
			if (!KingdomCentralLogistics.TryCancelConstructionInputReservations(system,
				owner, ids, out fault))
				failure = (failure ?? "Routed-input preparation failed.")
					+ " Neutral route cleanup also failed (" + fault + ").";
		}
	}
}
