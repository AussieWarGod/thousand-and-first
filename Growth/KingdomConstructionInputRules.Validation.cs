using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputRules
	{
		public static bool TryValidate(KingdomConstructionInputReceipt Receipt,
			out KingdomConstructionInputFault Fault)
		{
			if (Receipt == null) return Refuse(KingdomConstructionInputFault.Null, out Fault);
			if (Receipt.Schema > Schema) return Refuse(KingdomConstructionInputFault.FutureSchema, out Fault);
			if (Receipt.Schema < LegacySchema)
				return Refuse(KingdomConstructionInputFault.Schema, out Fault);
			if (Receipt.Schema == LegacySchema && Receipt.RequiredObjectCount > 1)
				return Refuse(KingdomConstructionInputFault.Schema, out Fault);
			if (!ValidText(Receipt.ReceiptId, MaxIdentityChars, false)
				|| !ValidText(Receipt.ConstructionJobId, MaxIdentityChars, false)
				|| !ValidText(Receipt.OwnerKey, MaxIdentityChars, false)
				|| !ValidText(Receipt.TargetZoneId, MaxIdentityChars, false)
				|| !ValidateRequiredObjects(Receipt))
				return Refuse(KingdomConstructionInputFault.Identity, out Fault);
			if (Receipt.OwnerEpoch < 0L) return Refuse(KingdomConstructionInputFault.Owner, out Fault);
			if (Receipt.TargetX < 0 || Receipt.TargetX > MaxCoordinate
				|| Receipt.TargetY < 0 || Receipt.TargetY > MaxCoordinate
				|| Receipt.WaterRequested < 0 || Receipt.WaterReserveFloor < 0
				|| Receipt.MaterialReservePolicyVersion < 1 || Receipt.PriorWaterSpent < 0
				|| Receipt.PriorWaterLost < Receipt.PriorWaterSpent)
				return Refuse(KingdomConstructionInputFault.Amount, out Fault);
			KingdomMaterialDebitCost requested;
			KingdomMaterialDebitCost priorSpent;
			KingdomMaterialDebitCost priorLost;
			if (!TryParseMaterialClaim(Receipt.MaterialRequestedClaim, out requested)
				|| !TryParseMaterialClaim(Receipt.PriorMaterialSpentClaim, out priorSpent)
				|| !TryParseMaterialClaim(Receipt.PriorMaterialLostClaim, out priorLost)
				|| !CostCovers(priorLost, priorSpent))
				return Refuse(KingdomConstructionInputFault.Claim, out Fault);
			if (!ValidDigest(Receipt.ConstructionIntentDigest) || !ValidDigest(Receipt.PlanDigest))
				return Refuse(KingdomConstructionInputFault.Digest, out Fault);
			if (!Defined(Receipt.TxPhase)) return Refuse(KingdomConstructionInputFault.Phase, out Fault);
			if (Receipt.Revision < 0 || Receipt.PauseStartedTick < -1L || Receipt.PausedTicks < 0L)
				return Refuse(KingdomConstructionInputFault.Revision, out Fault);
			if (Receipt.SourceCount < 1 || Receipt.SourceCount > MaxSourceLines
				|| Receipt.CargoCount < 1 || Receipt.CargoCount > MaxCargoLines
				|| Receipt.ChildCount < 1 || Receipt.ChildCount > MaxChildren)
				return Refuse(KingdomConstructionInputFault.Bounds, out Fault);
			if (!ValidateSources(Receipt, out Fault) || !ValidateChildren(Receipt, out Fault)
				|| !ValidateCargoRows(Receipt, out Fault) || !TryValidateMaterialPlan(Receipt, out Fault))
				return false;
			if (!ParentCoherent(Receipt))
				return Refuse(KingdomConstructionInputFault.Phase, out Fault);
			string digest;
			if (!TryPlanDigest(Receipt, out digest) || !FixedEquals(digest, Receipt.PlanDigest))
				return Refuse(KingdomConstructionInputFault.Digest, out Fault);
			Fault = KingdomConstructionInputFault.None;
			return true;
		}

		private static bool ValidateRequiredObjects(KingdomConstructionInputReceipt receipt)
		{
			if (receipt.RequiredObjectCount > MaxRequiredObjects) return false;
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < receipt.RequiredObjectCount; i++)
			{
				string value = receipt.RequiredObjectAt(i);
				if (!ValidText(value, MaxIdentityChars, false) || !seen.Add(value)) return false;
			}
			return receipt.RequiredObjectId == (receipt.RequiredObjectCount == 0
				? null : receipt.RequiredObjectAt(0));
		}

		private static bool ValidateSources(KingdomConstructionInputReceipt Receipt,
			out KingdomConstructionInputFault Fault)
		{
			Fault = KingdomConstructionInputFault.None;
			HashSet<string> lineIds = new HashSet<string>(StringComparer.Ordinal);
			HashSet<int> cargoOrdinals = new HashSet<int>();
			HashSet<string> materialObjects = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> materialObjectIds = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> waterObjectIds = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> remainders = new HashSet<string>(StringComparer.Ordinal);
			Dictionary<string, KingdomConstructionInputSourceLine> waterLast =
				new Dictionary<string, KingdomConstructionInputSourceLine>(StringComparer.Ordinal);
			Dictionary<string, SourceGroup> groups = new Dictionary<string, SourceGroup>(StringComparer.Ordinal);
			int water = 0;
			long waterFloor = 0L;
			for (int i = 0; i < Receipt.SourceCount; i++)
			{
				KingdomConstructionInputSourceLine line = Receipt.SourceAt(i);
				if (line == null || line.Ordinal != i || !ValidateSource(line, Receipt, out Fault)) return false;
				if (!lineIds.Add(line.LineId) || !cargoOrdinals.Add(line.CargoOrdinal))
					return Refuse(KingdomConstructionInputFault.Duplicate, out Fault);
				string physical = line.SourceZoneId + "\0" + line.HolderId + "\0" + line.SourceObjectId;
				if (line.Kind == KingdomConstructionInputKind.Water)
				{
					if (materialObjectIds.Contains(line.SourceObjectId))
						return Refuse(KingdomConstructionInputFault.Duplicate, out Fault);
					waterObjectIds.Add(line.SourceObjectId);
					if (waterLast.TryGetValue(physical, out KingdomConstructionInputSourceLine prior)
						&& !WaterChain(prior, line))
						return Refuse(KingdomConstructionInputFault.Overlap, out Fault);
					waterLast[physical] = line;
					long next = (long)water + line.Take;
					if (next > int.MaxValue) return Refuse(KingdomConstructionInputFault.Amount, out Fault);
					water = (int)next;
				}
				else
				{
					if (waterObjectIds.Contains(line.SourceObjectId)
						|| !materialObjectIds.Add(line.SourceObjectId))
						return Refuse(KingdomConstructionInputFault.Duplicate, out Fault);
					if (!materialObjects.Add(physical))
						return Refuse(KingdomConstructionInputFault.Overlap, out Fault);
				}
				if (!string.IsNullOrEmpty(line.RemainderObjectId)
					&& !remainders.Add(line.RemainderObjectId))
					return Refuse(KingdomConstructionInputFault.Duplicate, out Fault);
				string groupKey = line.Kind == KingdomConstructionInputKind.Water
					? line.SourceSettlementId + "\0water\0" + line.Classification
					: line.SourceSettlementId + "\0" + line.SourceZoneId + "\0"
						+ line.HolderId + "\0" + (int)line.Kind + "\0" + line.Classification;
				if (!groups.TryGetValue(groupKey, out SourceGroup group))
				{
					group = new SourceGroup(line.HolderStockBefore, line.PriorReserved, line.ReserveFloor);
					groups.Add(groupKey, group);
					if (line.Kind == KingdomConstructionInputKind.Water)
						waterFloor += line.ReserveFloor;
				}
				if (!group.Add(line)) return Refuse(KingdomConstructionInputFault.Amount, out Fault);
			}
			if (water != Receipt.WaterRequested
				|| (water > 0 && waterFloor != Receipt.WaterReserveFloor))
				return Refuse(KingdomConstructionInputFault.Claim, out Fault);
			foreach (SourceGroup group in groups.Values)
				if (!group.Valid()) return Refuse(KingdomConstructionInputFault.Amount, out Fault);
			Fault = KingdomConstructionInputFault.None;
			return true;
		}

		private static bool ValidateChildren(KingdomConstructionInputReceipt Receipt,
			out KingdomConstructionInputFault Fault)
		{
			HashSet<int> jobs = new HashSet<int>();
			HashSet<int> trips = new HashSet<int>();
			int covered = 0;
			for (int i = 0; i < Receipt.ChildCount; i++)
			{
				KingdomConstructionInputChild child = Receipt.ChildAt(i);
				if (child == null || child.Ordinal != i || child.JobId <= 0 || child.TripId <= 0
					|| !jobs.Add(child.JobId) || !trips.Add(child.TripId)
					|| child.CargoStart != covered || child.CargoCount < 1
					|| child.CargoCount > MaxCargoPerChild
					|| child.CargoShape != KingdomConstructionInputCargoShape.OpaqueObjectManifest
					|| child.SourceEndpointId == 0 || child.TargetEndpointId == 0
					|| !ValidText(child.SourceObjectId, MaxIdentityChars, true)
					|| !ValidText(child.SourceZoneId, MaxIdentityChars, false)
					|| !ValidText(child.TargetObjectId, MaxIdentityChars, true)
					|| child.TargetZoneId != Receipt.TargetZoneId
					|| child.SourceX < 0 || child.SourceX > MaxCoordinate
					|| child.SourceY < 0 || child.SourceY > MaxCoordinate
					|| child.TargetX != Receipt.TargetX || child.TargetY != Receipt.TargetY
					|| child.ArrivalTick < 0L || !ValidDigest(child.RouteDigest)
					|| child.CentralPhase < 0 || child.CentralRevision < 0L)
					return Refuse(KingdomConstructionInputFault.Child, out Fault);
				for (int cargo = child.CargoStart; cargo < child.CargoStart + child.CargoCount; cargo++)
					if (cargo >= Receipt.CargoCount || Receipt.CargoAt(cargo).ChildJobId != child.JobId
						|| Receipt.CargoAt(cargo).ChildTripId != child.TripId)
						return Refuse(KingdomConstructionInputFault.Child, out Fault);
				covered += child.CargoCount;
			}
			if (covered != Receipt.CargoCount) return Refuse(KingdomConstructionInputFault.Child, out Fault);
			Fault = KingdomConstructionInputFault.None;
			return true;
		}

		private static bool ValidateCargoRows(KingdomConstructionInputReceipt Receipt,
			out KingdomConstructionInputFault Fault)
		{
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> markers = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> objects = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> required = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Receipt.CargoCount; i++)
			{
				KingdomConstructionInputCargoLine cargo = Receipt.CargoAt(i);
				if (cargo == null || cargo.Ordinal != i || cargo.SourceLineOrdinal < 0
					|| cargo.SourceLineOrdinal >= Receipt.SourceCount)
					return Refuse(KingdomConstructionInputFault.Bounds, out Fault);
				KingdomConstructionInputSourceLine source = Receipt.SourceAt(cargo.SourceLineOrdinal);
				if (!ValidateCargo(cargo, Receipt, source, out Fault)) return false;
				if (!keys.Add(cargo.CargoKey) || !markers.Add(cargo.CreationMarker))
					return Refuse(KingdomConstructionInputFault.Duplicate, out Fault);
				if (!string.IsNullOrEmpty(cargo.ObjectId) && !objects.Add(cargo.ObjectId))
					return Refuse(KingdomConstructionInputFault.Overlap, out Fault);
				if (Receipt.RequiresObject(source.SourceObjectId)
					&& source.Kind != KingdomConstructionInputKind.Material)
					return Refuse(KingdomConstructionInputFault.CrossBinding, out Fault);
				if ((long)source.ProvedLost + cargo.Lost > cargo.Amount)
					return Refuse(KingdomConstructionInputFault.Conservation, out Fault);
				if (Receipt.RequiresObject(source.SourceObjectId)
					&& source.Kind == KingdomConstructionInputKind.Material
					&& cargo.ExpectedObjectId == source.SourceObjectId
					&& source.Take == source.Before && source.ResidualAfter == 0)
					required.Add(source.SourceObjectId);
			}
			if (required.Count != Receipt.RequiredObjectCount)
				return Refuse(KingdomConstructionInputFault.CrossBinding, out Fault);
			Fault = KingdomConstructionInputFault.None;
			return true;
		}

		private static bool WaterChain(KingdomConstructionInputSourceLine A,
			KingdomConstructionInputSourceLine B)
		{
			return B.Before == A.ResidualAfter && A.Classification == B.Classification
				&& A.SourceSettlementId == B.SourceSettlementId && A.SourceZoneId == B.SourceZoneId
				&& A.HolderId == B.HolderId && A.Topology == B.Topology && A.X == B.X && A.Y == B.Y
				&& A.Blueprint == B.Blueprint && A.HolderStockBefore == B.HolderStockBefore
				&& A.PriorReserved == B.PriorReserved && A.ReserveFloor == B.ReserveFloor
				&& A.RouteCost == B.RouteCost && A.DedicationOrdinal == B.DedicationOrdinal;
		}

		private sealed class SourceGroup
		{
			private readonly int Stock;
			private readonly int Prior;
			private readonly int Floor;
			private long Taken;
			internal SourceGroup(int stock, int prior, int floor) { Stock = stock; Prior = prior; Floor = floor; }
			internal bool Add(KingdomConstructionInputSourceLine line)
			{
				if (line.HolderStockBefore != Stock || line.PriorReserved != Prior
					|| line.ReserveFloor != Floor) return false;
				Taken += line.Take; return Taken <= int.MaxValue;
			}
			internal bool Valid() { return Prior >= 0 && Floor >= 0 && Taken <= (long)Stock - Prior - Floor; }
		}
	}
}
