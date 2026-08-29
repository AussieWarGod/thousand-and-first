using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputPlanRules
	{
		public static bool TryCreateReceipt(KingdomConstructionInputPlan Plan,
			string ReceiptId, string OwnerKey, long OwnerEpoch, string TargetZoneId,
			int TargetX, int TargetY, string ConstructionIntentDigest,
			int MaterialReservePolicyVersion, int PriorWaterSpent, int PriorWaterLost,
			string PriorMaterialSpentClaim, string PriorMaterialLostClaim,
			IList<KingdomConstructionInputChild> Children,
			out KingdomConstructionInputReceipt Receipt,
			out KingdomConstructionInputPlanFault Fault)
		{
			Receipt = null;
			if (Plan == null || Children == null)
				return Refuse(KingdomConstructionInputPlanFault.Null, out Fault);
			if (Children.Count != Plan.ChildCount)
				return Refuse(KingdomConstructionInputPlanFault.Child, out Fault);
			int[] childJobs = new int[Plan.LineCount];
			int[] childTrips = new int[Plan.LineCount];
			for (int i = 0; i < Children.Count; i++)
			{
				KingdomConstructionInputChild child = Children[i];
				KingdomConstructionInputPlannedChild expected = Plan.ChildAt(i);
				if (child == null || child.Ordinal != i
					|| child.CargoStart != expected.CargoStart
					|| child.CargoCount != expected.CargoCount
					|| child.SourceZoneId != expected.SourceZoneId
					|| child.SourceObjectId != expected.SourceObjectId
					|| child.SourceX != expected.SourceX || child.SourceY != expected.SourceY
					|| child.TargetZoneId != TargetZoneId || child.TargetX != TargetX
					|| child.TargetY != TargetY)
					return Refuse(KingdomConstructionInputPlanFault.Child, out Fault);
				for (int j = child.CargoStart; j < child.CargoStart + child.CargoCount; j++)
				{
					if (j < 0 || j >= Plan.LineCount || childJobs[j] != 0)
						return Refuse(KingdomConstructionInputPlanFault.Child, out Fault);
					childJobs[j] = child.JobId;
					childTrips[j] = child.TripId;
				}
			}

			List<KingdomConstructionInputSourceLine> sources =
				new List<KingdomConstructionInputSourceLine>(Plan.LineCount);
			List<KingdomConstructionInputCargoLine> cargo =
				new List<KingdomConstructionInputCargoLine>(Plan.LineCount);
			for (int i = 0; i < Plan.LineCount; i++)
			{
				KingdomConstructionInputPlannedLine line = Plan.LineAt(i);
				KingdomConstructionInputCandidate source = line.Candidate;
				int residual = line.Before - line.Take;
				sources.Add(new KingdomConstructionInputSourceLine(i, line.LineId,
					source.Kind, source.Classification, source.SourceSettlementId,
					source.SourceZoneId, source.HolderId, source.SourceObjectId,
					source.Topology, source.X, source.Y, source.Blueprint, line.Before,
					line.Take, residual, source.HolderStockBefore, source.PriorReserved,
					source.ReserveFloor, i, source.RouteCost, source.DedicationOrdinal,
					line.RemainderMarker, KingdomConstructionInputSourcePhase.Reserved,
					null, null, null, 0));
				bool water = source.Kind == KingdomConstructionInputKind.Water;
				cargo.Add(new KingdomConstructionInputCargoLine(i, line.CargoKey,
					line.CreationMarker, source.Kind, source.Classification, line.Take,
					water ? WaterCargoBlueprint : source.Blueprint,
					water ? 64 : source.Count, i,
					water ? null : source.SourceObjectId, childJobs[i], childTrips[i],
					null, KingdomConstructionInputCargoPhase.Planned,
					KingdomConstructionInputTopology.Invalid, null, null, -1, -1,
					null, null, 0, 0));
			}
			KingdomConstructionInputFault receiptFault;
			if (!KingdomConstructionInputRules.TryCreateWithRequiredObjects(ReceiptId,
				Plan.OperationId,
				OwnerKey, OwnerEpoch, TargetZoneId, TargetX, TargetY,
				ConstructionIntentDigest, Plan.CopyRequiredObjectIds(), Plan.WaterRequested,
				Plan.MaterialRequestedClaim, Plan.DailyWaterUpkeep,
				MaterialReservePolicyVersion, PriorWaterSpent, PriorWaterLost,
				PriorMaterialSpentClaim, PriorMaterialLostClaim, sources, cargo, Children,
				out Receipt, out receiptFault))
				return Refuse(KingdomConstructionInputPlanFault.Receipt, out Fault);
			Fault = KingdomConstructionInputPlanFault.None;
			return true;
		}
	}
}
