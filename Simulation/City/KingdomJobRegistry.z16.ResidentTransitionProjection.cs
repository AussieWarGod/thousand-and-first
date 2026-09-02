using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomJobRegistry
	{
		/// <summary>Non-normalizing projection for resident destruction. Ragged, duplicate,
		/// over-cap, or structurally impossible jobs fail closed without repairing save state.</summary>
		internal bool TryProjectResidentTransition(int ResidentId, out bool Expedition)
		{
			Expedition = false;
			if (ResidentId <= 0 || JobIds == null) return false;
			int jobs = JobIds.Count;
			if (jobs > KingdomCityMemoryRules.MaxOpenJobs
				|| !JobColumnsSquare(jobs) || !LegColumnsSquare(out int legs)) return false;
			HashSet<int> ids = new HashSet<int>();
			long consumed = 0L;
			for (int i = 0; i < jobs; i++)
			{
				int legCount = LegCounts[i];
				if (JobIds[i] <= 0 || !ids.Add(JobIds[i]) || legCount < 0
					|| legCount > KingdomItineraryRules.MaxLegs
					|| consumed + legCount > legs
					|| SubjectIds[i] < 0
					|| Kinds[i] < 0 || Kinds[i] > byte.MaxValue
					|| !Enum.IsDefined(typeof(KingdomJobKind), (byte)Kinds[i])) return false;
				consumed += legCount;
				if ((KingdomJobKind)Kinds[i] == KingdomJobKind.Expedition
					&& SubjectIds[i] == ResidentId) Expedition = true;
			}
			return consumed == legs;
		}

		private bool JobColumnsSquare(int Count)
		{
			int[] counts =
			{
				Kinds?.Count ?? -1, Cargos?.Count ?? -1, CargoAmounts?.Count ?? -1,
				SourceZoneIds?.Count ?? -1, DestZoneIds?.Count ?? -1,
				StartTicks?.Count ?? -1, WalkTicksPerCell?.Count ?? -1,
				Statuses?.Count ?? -1, OriginCodes?.Count ?? -1,
				DepositLegIndexes?.Count ?? -1, SubjectIds?.Count ?? -1,
				SubjectNames?.Count ?? -1, TargetNames?.Count ?? -1,
				DueTicks?.Count ?? -1, WaterCosts?.Count ?? -1,
				ProvisionCosts?.Count ?? -1, OutcomeCodes?.Count ?? -1,
				DeliverySourceEndpointIds?.Count ?? -1,
				DeliverySourceObjectIds?.Count ?? -1, DeliverySourceXs?.Count ?? -1,
				DeliverySourceYs?.Count ?? -1, DeliveryTargetEndpointIds?.Count ?? -1,
				DeliveryTargetObjectIds?.Count ?? -1, DeliveryTargetXs?.Count ?? -1,
				DeliveryTargetYs?.Count ?? -1,
				DeliverySourceBeforeAmounts?.Count ?? -1, DeliveryTripIds?.Count ?? -1,
				DeliveryStopOrdinals?.Count ?? -1, DeliveryPhases?.Count ?? -1,
				DeliveryCargoAuthorityKinds?.Count ?? -1,
				DeliveryOwnerOperationIds?.Count ?? -1,
				DeliveryOwnerManifestVersions?.Count ?? -1,
				DeliveryOwnerManifestDigests?.Count ?? -1,
				DeliveryOwnerManifestRevisions?.Count ?? -1,
				DeliveryManifestSourceStarts?.Count ?? -1,
				DeliveryManifestSourceCounts?.Count ?? -1,
				DeliveryTargetBeforeAmounts?.Count ?? -1,
				DeliveryTargetReceiptStates?.Count ?? -1,
				LegCounts?.Count ?? -1
			};
			for (int i = 0; i < counts.Length; i++) if (counts[i] != Count) return false;
			return true;
		}

		private bool LegColumnsSquare(out int Count)
		{
			Count = LegZoneIds?.Count ?? -1;
			return Count >= 0 && LegEnterX?.Count == Count && LegEnterY?.Count == Count
				&& LegExitX?.Count == Count && LegExitY?.Count == Count
				&& LegLengths?.Count == Count && LegDepartTicks?.Count == Count
				&& LegArriveTicks?.Count == Count;
		}
	}
}
