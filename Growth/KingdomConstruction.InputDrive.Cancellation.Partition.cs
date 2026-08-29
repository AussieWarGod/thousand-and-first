using XRL.World;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		/// <summary>Landed cancellation first retracts visible target carriers into exact transit
		/// roots. Later source attendance may restore cargo without loading target ground.</summary>
		private static bool CancellationTargetPartitionRequired(
			KingdomSystem system, KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt,
			out KingdomConstructionInputChild required,
			out int requiredOrdinal)
		{
			required = null;
			requiredOrdinal = -1;
			if (receipt == null) return false;
			for (int i = 0; i < receipt.ChildCount; i++)
			{
				KingdomConstructionInputChild child = receipt.ChildAt(i);
				bool landed = false;
				bool inFlight = false;
				for (int j = child.CargoStart; j < child.CargoStart + child.CargoCount; j++)
				{
					KingdomConstructionInputCargoLine cargo = receipt.CargoAt(j);
					if (cargo.Phase == KingdomConstructionInputCargoPhase.InFlight)
						inFlight = true;
					if (cargo.CustodyTopology == KingdomConstructionInputTopology.LandingEscrow
						&& cargo.Phase != KingdomConstructionInputCargoPhase.Spent)
					{ landed = true; break; }
				}
				KingdomPhysicalLookupState arrivalCut = inFlight
					? KingdomCentralLogistics.LookupConstructionInputCancellationTargetCut(
						system, job.Id, child.JobId, child.TripId, receipt.Schema,
						receipt.PlanDigest, receipt.Revision, job, receipt, i)
					: KingdomPhysicalLookupState.Absent;
				bool targetCut = arrivalCut != KingdomPhysicalLookupState.Absent;
				if (!targetCut && (!landed
					|| KingdomCentralLogistics.ConstructionInputTransitRootSettled(
						receipt.ConstructionJobId, child.TripId)
					|| KingdomCentralLogistics.ConstructionInputCancellationSourceProjected(
						system, job.Id, child.JobId, child.TripId, child.SourceZoneId))) continue;
				required = child;
				requiredOrdinal = i;
				return true;
			}
			return false;
		}

		private static bool RetireCompletedCancellationCarriers(KingdomSystem system,
			Zone active, KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			out string failure)
		{
			failure = null;
			for (int i = 0; i < receipt.ChildCount; i++)
			{
				KingdomConstructionInputChild child = receipt.ChildAt(i);
				bool complete = true;
				for (int j = child.CargoStart; j < child.CargoStart + child.CargoCount; j++)
				{
					KingdomConstructionInputCargoLine cargo = receipt.CargoAt(j);
					if (!CancellationLineComplete(receipt.SourceAt(cargo.SourceLineOrdinal), cargo))
					{ complete = false; break; }
				}
				if (!complete) return true;
				if (!KingdomCentralLogistics.ConstructionInputCarrierCustodyExists(system,
					job.Id, child.TripId)) continue;
				if (active == null || active.ZoneID != child.SourceZoneId) return false;
				bool sourceExact = true;
				for (int j = child.CargoStart; j < child.CargoStart + child.CargoCount; j++)
					if (!ExactCompletedCancellationSourceGroup(active, job, receipt,
						receipt.CargoAt(j)))
					{ sourceExact = false; break; }
				if (!sourceExact) return false;
				if (!KingdomCentralLogistics.TryRetireConstructionInputCancellationSource(
					system, job.Id, receipt.Schema, receipt.PlanDigest, receipt.Revision,
					child.JobId, child.TripId, job, receipt, i, active,
					out KingdomCityFault fault))
				{
					failure = "Empty cancelled carrier could not retire with whole-custody proof ("
						+ fault + ").";
					return false;
				}
				return false;
			}
			return true;
		}

		private static bool ExactCompletedCancellationSourceGroup(Zone active,
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputCargoLine cargo)
		{
			KingdomConstructionInputSourceLine source =
				receipt.SourceAt(cargo.SourceLineOrdinal);
			if (source.Kind != KingdomConstructionInputKind.Water)
				return ExactCancellationSourceStanding(active, job, receipt, source, cargo, null);
			KingdomConstructionInputSourceLine first = null;
			int priorResidual = -1;
			for (int i = 0; i < receipt.SourceCount; i++)
			{
				KingdomConstructionInputSourceLine line = receipt.SourceAt(i);
				if (line.SourceObjectId != source.SourceObjectId) continue;
				if (line.Kind != KingdomConstructionInputKind.Water
					|| line.SourceZoneId != source.SourceZoneId
					|| line.HolderId != source.HolderId || line.Before - line.Take != line.ResidualAfter
					|| !CancellationLineComplete(line, receipt.CargoAt(line.CargoOrdinal)))
					return false;
				if (first == null) first = line;
				else if (line.Before != priorResidual) return false;
				priorResidual = line.ResidualAfter;
			}
			if (first == null || active == null || active.ZoneID != first.SourceZoneId
				|| FindExactId(active, first.SourceObjectId, out GameObject vessel)
					!= KingdomPhysicalLookupState.Exact) return false;
			return ExactRoutedInputWaterSource(active, first, vessel,
				vessel.GetPart<XRL.World.Parts.LiquidVolume>(), first.Before);
		}
	}
}
