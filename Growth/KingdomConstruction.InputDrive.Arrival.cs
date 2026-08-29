using System;

using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		private static bool DriveInputArrivals(KingdomSystem system, Zone target,
			ref KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			out string failure)
		{
			failure = null;
			long now = The.Game == null ? 0L : The.Game.TimeTicks;
			for (int childOrdinal = 0; childOrdinal < receipt.ChildCount; childOrdinal++)
			{
				KingdomConstructionInputChild child = receipt.ChildAt(childOrdinal);
				bool cargoPending = false;
				for (int i = child.CargoStart; i < child.CargoStart + child.CargoCount; i++)
					if (receipt.CargoAt(i).Phase == KingdomConstructionInputCargoPhase.InFlight)
					{ cargoPending = true; break; }
				if (cargoPending)
				{
					if (!KingdomConstructionInputRules.TryEffectiveArrivalTick(
						child.ArrivalTick, receipt.PausedTicks, out long arrival))
					{
						failure = "The routed construction arrival clock overflowed.";
						return false;
					}
					if (now < arrival) return false;
					KingdomCityFault central;
					if (child.CentralPhase != (int)KingdomDeliveryPhase.LandedAwaitingOwner
						&& !KingdomCentralLogistics.TryMaterializeConstructionInputArrival(
							system, job.Id, child.JobId, child.TripId, receipt.Schema,
							receipt.PlanDigest, job, receipt, childOrdinal, target, now, out central))
					{
						failure = "The exact construction carrier could not materialize at "
							+ "its frozen destination (" + central + ").";
						return false;
					}
					GameObject carrier;
					if (!KingdomCentralLogistics.TryResolveConstructionInputTargetCarrier(
						system, job.Id, child.JobId, child.TripId, receipt.Schema,
						receipt.PlanDigest, receipt.Revision, target,
						out carrier, out central))
					{
						failure = "The landed construction carrier is absent or ambiguous ("
							+ central + ").";
						return false;
					}
					for (int i = child.CargoStart; i < child.CargoStart + child.CargoCount; i++)
					{
						KingdomConstructionInputCargoLine cargo = receipt.CargoAt(i);
						if (cargo.Phase != KingdomConstructionInputCargoPhase.InFlight) continue;
						GameObject exact;
						if (!ExactInputCargo(target, carrier, job, receipt, cargo, out exact))
						{
							failure = "The carrier's opaque manifest no longer contains its exact cargo.";
							return false;
						}
						return InputCargoPhaseEvidence(ref job, receipt, i,
							KingdomConstructionInputCargoPhase.Landed, cargo.ObjectId,
							KingdomConstructionInputTopology.LandingEscrow, carrier.ID,
							receipt.TargetZoneId, receipt.TargetX, receipt.TargetY,
							cargo.BeforeWitnessHash, cargo.AfterWitnessHash,
							cargo.Spent, cargo.Lost, out failure);
					}
				}
				if (child.CentralPhase == (int)KingdomDeliveryPhase.LandedAwaitingOwner)
					continue;
				KingdomCityFault landedFault;
				if (!KingdomCentralLogistics.TryAcknowledgeConstructionInputLanded(system,
					job.Id, child.JobId, child.TripId, receipt.Schema, receipt.PlanDigest,
					target, receipt.Revision, out landedFault))
				{
					failure = "Central logistics could not acknowledge exact landing ("
						+ landedFault + ").";
					return false;
				}
				return InputChildEvidence(ref job, receipt, childOrdinal,
					(int)KingdomDeliveryPhase.LandedAwaitingOwner,
					receipt.Revision, out failure);
			}
			for (int i = 0; i < receipt.CargoCount; i++)
				if (receipt.CargoAt(i).Phase != KingdomConstructionInputCargoPhase.Landed)
				{
					failure = "Not every routed construction object reached landing escrow.";
					return false;
				}
			return TransitionInputTx(ref job, receipt,
				KingdomConstructionInputTxPhase.LandedAwaitingOwner, out failure);
		}

		private static bool ExactInputCargo(Zone zone, GameObject carrier,
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputCargoLine cargo, out GameObject exact)
		{
			return ExactInputCargo(zone, carrier, job, receipt, cargo, cargo.Amount, out exact);
		}

		private static bool ExactInputCargo(Zone zone, GameObject carrier,
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputCargoLine cargo, int expectedWaterVolume,
			out GameObject exact)
		{
			exact = null;
			string marker = cargo.Kind == KingdomConstructionInputKind.Water
				? cargo.CreationMarker : cargo.CargoKey;
			if (!GameObject.Validate(carrier) || carrier.Inventory == null
				|| FindExactId(zone, cargo.ObjectId, out exact) != KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exact.InInventory, carrier)
				|| ReferenceCount(carrier.Inventory.Objects, exact) != 1
				|| exact.Blueprint != cargo.Blueprint
				|| !exact.HasStringProperty(InputMarkerProperty)
				|| exact.HasIntProperty(InputMarkerProperty)
				|| exact.GetStringProperty(InputMarkerProperty) != marker
				|| exact.IsImportant() || exact.Equipped != null
				|| !exact.IsTakeable() || exact.HasTag("AlwaysStack")
				|| !RoutedInputItemAuthorized(job, receipt, exact)) return false;
			bool protectedCargo = KingdomPurpose.HasProtectedCargoEvidence(exact);
			if (!protectedCargo && exact.GetIntProperty("NeverStack") != 1) return false;
			if (cargo.Kind == KingdomConstructionInputKind.Water
				&& exact.GetIntProperty(KingdomPorters.StockProperty) != 1) return false;
			if (cargo.Kind != KingdomConstructionInputKind.Water)
			{
				return exact.Count == cargo.Amount
					&& TryInputClassification(exact, out KingdomConstructionInputKind kind,
						out string classification)
					&& kind == cargo.Kind && classification == cargo.Classification;
			}
			LiquidVolume liquid = exact.GetPart<LiquidVolume>();
			return liquid != null && !liquid.Sealed && liquid.MaxVolume == cargo.Capacity
				&& liquid.Volume == expectedWaterVolume
				&& (liquid.Volume == 0 || KingdomLiquids.HasFreshWater(liquid));
		}

		internal static bool RoutedInputItemAuthorized(KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, GameObject item)
		{
			if (!GameObject.Validate(item) || receipt == null) return false;
			bool protectedCargo = KingdomPurpose.HasProtectedCargoEvidence(item);
			bool required = receipt.RequiresObject(item.IDIfAssigned);
			if (!protectedCargo && !required) return true;
			return KingdomPurpose.ExactProtectedFundingAuthorization(job,
				receipt.CopyRequiredObjectIds(), item);
		}

		/// <summary>Reproves the durable required-object vector after Obliterate invalidated
		/// the exact same GameObject. Shape, marker, classification, and ID are checked by caller.</summary>
		internal static bool RetiredRoutedInputItemAuthorized(KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, GameObject item)
		{
			if (item == null || receipt == null) return false;
			bool protectedCargo = KingdomPurpose.HasProtectedCargoEvidence(item);
			bool required = receipt.RequiresObject(item.IDIfAssigned);
			if (!protectedCargo && !required) return true;
			if (!required || !KingdomPurpose.RequiredFundingObjectsMatch(job,
				receipt.CopyRequiredObjectIds())) return false;
			int matches = 0;
			for (int i = 0; i < receipt.RequiredObjectCount; i++)
				if (receipt.RequiredObjectAt(i) == item.IDIfAssigned) matches++;
			return matches == 1;
		}
	}
}
