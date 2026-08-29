using System.Collections.Generic;

using XRL.World;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		/// <summary>Proves every landed child's complete phase-aware custody graph. A spent or
		/// callback-complete DebitIntent line must have exact graveyard evidence; every other
		/// line remains one direct exact carrier child and no extra object is permitted.</summary>
		private static bool ExactDebitChildManifest(KingdomSystem system, Zone target,
			GameObject carrier, KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputCargoLine current)
		{
			if (receipt == null || current == null) return false;
			int currentMatches = 0;
			for (int childOrdinal = 0; childOrdinal < receipt.ChildCount; childOrdinal++)
			{
				KingdomConstructionInputChild child = receipt.ChildAt(childOrdinal);
				if (!KingdomCentralLogistics.TryResolveConstructionInputTargetCarrier(system,
					job.Id, child.JobId, child.TripId, receipt.Schema, receipt.PlanDigest,
					receipt.Revision, target, out GameObject exactCarrier, out KingdomCityFault _)
					|| !ExactDebitCarrierManifest(target, exactCarrier, job, receipt, child))
					return false;
				if (child.TripId == current.ChildTripId)
				{
					currentMatches++;
					if (child.JobId != current.ChildJobId
						|| !ReferenceEquals(exactCarrier, carrier)) return false;
				}
			}
			return currentMatches == 1;
		}

		private static bool ExactDebitCarrierManifest(Zone target, GameObject carrier,
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputChild child)
		{
			if (!KingdomOrdinaryCustody.TryCollect(carrier,
				out List<GameObject> graph, out string _)) return false;
			int active = 0;
			for (int i = child.CargoStart; i < child.CargoStart + child.CargoCount; i++)
			{
				KingdomConstructionInputCargoLine cargo = receipt.CargoAt(i);
				KingdomPhysicalLookupState state = FindGlobalInputId(receipt, cargo.ObjectId,
					out GameObject exact, out bool graveyard);
				bool retired = state == KingdomPhysicalLookupState.Exact && graveyard
					&& ExactConsumedCargoEvidence(job, receipt, cargo, exact);
				if (cargo.Phase == KingdomConstructionInputCargoPhase.Spent
					|| cargo.Phase == KingdomConstructionInputCargoPhase.DebitIntent && retired)
				{
					if (!retired) return false;
					continue;
				}
				bool exactActive = cargo.Kind == KingdomConstructionInputKind.Water
					&& cargo.Phase == KingdomConstructionInputCargoPhase.DebitIntent
					&& exact?.GetPart<XRL.World.Parts.LiquidVolume>()?.Volume == 0
						? ExactInputCargo(target, carrier, job, receipt, cargo, 0, out exact)
						: ExactInputCargo(target, carrier, job, receipt, cargo, out exact);
				if ((cargo.Phase != KingdomConstructionInputCargoPhase.Landed
						&& cargo.Phase != KingdomConstructionInputCargoPhase.DebitIntent)
					|| state != KingdomPhysicalLookupState.Exact || graveyard
					|| !exactActive)
					return false;
				active++;
			}
			if (graph.Count != active + 1) return false;
			for (int i = 1; i < graph.Count; i++)
			{
				GameObject item = graph[i]; int matches = 0;
				for (int j = child.CargoStart; j < child.CargoStart + child.CargoCount; j++)
				{
					KingdomConstructionInputCargoLine cargo = receipt.CargoAt(j);
					if (cargo.Phase != KingdomConstructionInputCargoPhase.Spent
						&& item.IDIfAssigned == cargo.ObjectId) matches++;
				}
				if (matches != 1 || !ReferenceEquals(item.InInventory, carrier)
					|| ReferenceCount(carrier.Inventory.Objects, item) != 1) return false;
			}
			return true;
		}
	}
}
