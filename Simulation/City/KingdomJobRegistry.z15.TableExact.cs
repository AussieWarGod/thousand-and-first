using System;

namespace ThousandAndFirst.Simulation.City
{
	internal sealed partial class KingdomJobTable
	{
		internal static bool Exact(KingdomJobTable Left, KingdomJobTable Right)
		{
			if (Left == null || Right == null || Left.Count != Right.Count) return false;
			for (int i = 0; i < Left.Count; i++)
			{
				if (!Left.TryAt(i, out KingdomJobRow a) || !Right.TryAt(i,
					out KingdomJobRow b) || !Exact(a, b)) return false;
			}
			return true;
		}

		internal static bool Exact(KingdomJobRow A, KingdomJobRow B)
		{
			if (A.JobId != B.JobId || A.Kind != B.Kind || A.Cargo != B.Cargo
				|| A.CargoAmount != B.CargoAmount || A.SourceZoneId != B.SourceZoneId
				|| A.DestZoneId != B.DestZoneId || A.StartTick != B.StartTick
				|| A.WalkTicksPerCell != B.WalkTicksPerCell || A.Status != B.Status
				|| A.OriginCode != B.OriginCode || A.DepositLegIndex != B.DepositLegIndex
				|| A.SubjectId != B.SubjectId || A.SubjectName != B.SubjectName
				|| A.TargetName != B.TargetName || A.DueTick != B.DueTick
				|| A.WaterCost != B.WaterCost || A.ProvisionCost != B.ProvisionCost
				|| A.OutcomeCode != B.OutcomeCode
				|| A.ExpeditionDeedDisposition != B.ExpeditionDeedDisposition
				|| A.ExpeditionDeedPolityId != B.ExpeditionDeedPolityId
				|| A.ExpeditionDeedCauseRef != B.ExpeditionDeedCauseRef
				|| A.ExpeditionDeedFigureRef != B.ExpeditionDeedFigureRef
				|| A.DeliverySourceEndpointId != B.DeliverySourceEndpointId
				|| A.DeliverySourceObjectId != B.DeliverySourceObjectId
				|| A.DeliverySourceX != B.DeliverySourceX
				|| A.DeliverySourceY != B.DeliverySourceY
				|| A.DeliveryTargetEndpointId != B.DeliveryTargetEndpointId
				|| A.DeliveryTargetObjectId != B.DeliveryTargetObjectId
				|| A.DeliveryTargetX != B.DeliveryTargetX
				|| A.DeliveryTargetY != B.DeliveryTargetY
				|| A.DeliverySourceBeforeAmount != B.DeliverySourceBeforeAmount
				|| A.DeliveryTripId != B.DeliveryTripId
				|| A.DeliveryStopOrdinal != B.DeliveryStopOrdinal
				|| A.DeliveryPhase != B.DeliveryPhase
				|| A.DeliveryCargoAuthority != B.DeliveryCargoAuthority
				|| A.DeliveryOwnerOperationId != B.DeliveryOwnerOperationId
				|| A.DeliveryOwnerManifestVersion != B.DeliveryOwnerManifestVersion
				|| A.DeliveryOwnerManifestDigest != B.DeliveryOwnerManifestDigest
				|| A.DeliveryOwnerManifestRevision != B.DeliveryOwnerManifestRevision
				|| A.DeliveryManifestSourceStart != B.DeliveryManifestSourceStart
				|| A.DeliveryManifestSourceCount != B.DeliveryManifestSourceCount
				|| A.DeliveryTargetBeforeAmount != B.DeliveryTargetBeforeAmount
				|| A.DeliveryTargetReceiptState != B.DeliveryTargetReceiptState
				|| A.LegCount != B.LegCount) return false;
			for (int i = 0; i < A.LegCount; i++)
			{
				if (!A.TryLeg(i, out KingdomLeg x) || !B.TryLeg(i, out KingdomLeg y)
					|| x.ZoneId != y.ZoneId || x.EnterX != y.EnterX || x.EnterY != y.EnterY
					|| x.ExitX != y.ExitX || x.ExitY != y.ExitY
					|| x.PathLength != y.PathLength || x.DepartTick != y.DepartTick
					|| x.ArriveTick != y.ArriveTick) return false;
			}
			return true;
		}
	}

	public partial class KingdomJobRegistry
	{
		internal bool CanPublish(KingdomJobTable Table, out KingdomCityFault Fault)
		{
			Fault = KingdomCityFault.None;
			if (Table == null) { Fault = KingdomCityFault.NullArgument; return false; }
			for (int i = 0; i < Table.Count; i++)
			{
				if (!Table.TryAt(i, out KingdomJobRow row))
					{ Fault = KingdomCityFault.InvalidIndex; return false; }
				for (int j = 0; j < row.LegCount; j++)
					if (!row.TryLeg(j, out KingdomLeg _))
						{ Fault = KingdomCityFault.InvalidIndex; return false; }
			}
			return true;
		}
	}
}
