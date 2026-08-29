using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		private static bool ExactInputSourcePreflight(KingdomSystem system, Zone zone,
			GameObject holder,
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputCargoLine cargo, KingdomConstructionInputSourceLine source)
		{
			if (source.Kind == KingdomConstructionInputKind.Water)
			{
				LiquidVolume liquid = holder.GetPart<LiquidVolume>();
				if (!ExactRoutedInputWaterSource(zone, source, holder, liquid, -1)) return false;
				if (liquid.Volume == source.Before) return true;
				if (liquid.Volume != source.ResidualAfter
					|| source.Phase != KingdomConstructionInputSourcePhase.TransferIntent
					|| cargo.Phase != KingdomConstructionInputCargoPhase.PickupIntent
					|| string.IsNullOrEmpty(cargo.ObjectId)
					|| string.IsNullOrEmpty(source.BeforeWitnessHash)
					|| string.IsNullOrEmpty(source.AfterWitnessHash)) return false;
				if (!Simulation.City.KingdomCentralLogistics
					.TryResolveConstructionInputSourceCarrier(system, job.Id, cargo.ChildJobId,
						cargo.ChildTripId, receipt.Schema, receipt.PlanDigest, receipt.Revision,
						out GameObject waterCarrier, out _)
					|| FindCarrierMarker(waterCarrier, cargo.CreationMarker,
						out GameObject cask) != 1) return false;
				return ExactRoutedInputCask(waterCarrier, cask, job, receipt, cargo, source.Take);
			}
			if (holder.Inventory == null
				|| FindExactId(zone, source.SourceObjectId, out GameObject item)
					!= KingdomPhysicalLookupState.Exact
				|| item.Blueprint != source.Blueprint || item.IsImportant()
				|| item.Equipped != null || !item.IsTakeable() || item.HasTag("AlwaysStack")
				|| !KingdomOrdinaryCustody.TryProveEmpty(item, out string _)
				|| KingdomPurpose.HasProtectedCargoEvidence(item)
					&& !RoutedInputItemAuthorized(job, receipt, item)
				|| !TryInputClassification(item, out KingdomConstructionInputKind kind,
					out string classification) || kind != source.Kind
				|| classification != source.Classification) return false;
			if (!ReferenceEquals(item.InInventory, holder))
			{
				return Simulation.City.KingdomCentralLogistics
					.TryResolveConstructionInputSourceCarrier(system, job.Id, cargo.ChildJobId,
						cargo.ChildTripId, receipt.Schema, receipt.PlanDigest, receipt.Revision,
						out GameObject carrier, out _)
					&& ExactRoutedMaterialAtCarrier(zone, carrier, item, job, receipt,
						source, cargo, cargo.CargoKey,
						KingdomPurpose.HasProtectedCargoEvidence(item) ? -1 : 1);
			}
			if (ReferenceCount(holder.Inventory.Objects, item) != 1
				|| !ExactInputDedication(zone, source, holder, item)) return false;
			if (item.Count == source.Before)
			{
				bool protectedCargo = KingdomPurpose.HasProtectedCargoEvidence(item);
				if (!item.HasStringProperty(InputMarkerProperty)
					&& !item.HasIntProperty(InputMarkerProperty)) return true;
				if (!item.HasStringProperty(InputMarkerProperty)
					|| item.HasIntProperty(InputMarkerProperty)) return false;
				string marker = item.GetStringProperty(InputMarkerProperty);
				return !protectedCargo && item.GetIntProperty("NeverStack") == 1
						&& source.Phase == KingdomConstructionInputSourcePhase.SplitIntent
						&& marker == source.RemainderMarker
					|| source.Phase == KingdomConstructionInputSourcePhase.TransferIntent
						&& cargo.Phase == KingdomConstructionInputCargoPhase.PickupIntent
						&& marker == cargo.CargoKey
						&& (protectedCargo || item.GetIntProperty("NeverStack") == 1);
			}
			return item.Count == source.Take && source.ResidualAfter > 0
				&& (source.Phase == KingdomConstructionInputSourcePhase.SplitIntent
					|| source.Phase == KingdomConstructionInputSourcePhase.SplitProved
					|| source.Phase == KingdomConstructionInputSourcePhase.TransferIntent)
				&& item.HasStringProperty(InputMarkerProperty)
				&& !item.HasIntProperty(InputMarkerProperty)
				&& (item.GetStringProperty(InputMarkerProperty) == source.RemainderMarker
					|| item.GetStringProperty(InputMarkerProperty) == cargo.CargoKey)
				&& item.GetIntProperty("NeverStack") == 1;
		}
	}
}
