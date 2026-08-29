using System.Collections.Generic;

using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		/// <summary>Proves the complete attended pickup graph before any source callback.
		/// Earlier lines must already be exact direct cargo; the one active line may be
		/// either untouched at source or in one of its durable callback cuts.</summary>
		private static bool ExactSourcePickupManifest(Zone zone, GameObject carrier,
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputCargoLine current,
			KingdomConstructionInputSourceLine currentSource)
		{
			if (zone == null || !GameObject.Validate(carrier) || carrier.Inventory == null
				|| receipt == null || current == null || currentSource == null
				|| !KingdomOrdinaryCustody.TryCollect(carrier,
					out List<GameObject> graph, out string _)) return false;
			KingdomConstructionInputChild child = null;
			int childMatches = 0;
			for (int i = 0; i < receipt.ChildCount; i++)
			{
				KingdomConstructionInputChild candidate = receipt.ChildAt(i);
				if (candidate.TripId != current.ChildTripId
					|| current.Ordinal < candidate.CargoStart
					|| current.Ordinal >= candidate.CargoStart + candidate.CargoCount) continue;
				child = candidate;
				childMatches++;
			}
			if (childMatches != 1) return false;

			int expected = 0;
			for (int i = child.CargoStart; i < child.CargoStart + child.CargoCount; i++)
			{
				KingdomConstructionInputCargoLine cargo = receipt.CargoAt(i);
				KingdomConstructionInputSourceLine source =
					receipt.SourceAt(cargo.SourceLineOrdinal);
				bool isCurrent = cargo.Ordinal == current.Ordinal
					&& source.Ordinal == currentSource.Ordinal;
				bool mustBePresent = source.Phase == KingdomConstructionInputSourcePhase.Debited;
				if (mustBePresent && string.IsNullOrEmpty(cargo.ObjectId)) return false;
				GameObject item;
				int matches = DirectPickupCargoMatches(carrier, cargo, out item);
				if (!mustBePresent && isCurrent && matches == 1)
					mustBePresent = ExactCurrentPickupCut(zone, carrier, item, job, receipt,
						cargo, source);
				if (!mustBePresent)
				{
					if (matches != 0) return false;
					continue;
				}
				if (matches != 1 || !ExactLoadedPickupCargo(zone, carrier, item, job,
					receipt, cargo, source, source.Phase ==
						KingdomConstructionInputSourcePhase.Debited)) return false;
				expected++;
			}
			return graph.Count == expected + 1;
		}

		private static int DirectPickupCargoMatches(GameObject carrier,
			KingdomConstructionInputCargoLine cargo, out GameObject exact)
		{
			exact = null;
			int matches = 0;
			for (int i = 0; carrier?.Inventory != null
				&& i < carrier.Inventory.Objects.Count; i++)
			{
				GameObject item = carrier.Inventory.Objects[i];
				bool same = !string.IsNullOrEmpty(cargo.ObjectId)
					? item?.IDIfAssigned == cargo.ObjectId
					: cargo.Kind == KingdomConstructionInputKind.Water
						&& item?.GetStringProperty(InputMarkerProperty) == cargo.CreationMarker;
				if (!same) continue;
				matches++;
				if (matches == 1) exact = item;
			}
			return matches;
		}

		private static bool ExactCurrentPickupCut(Zone zone, GameObject carrier,
			GameObject item, KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputCargoLine cargo,
			KingdomConstructionInputSourceLine source)
		{
			if (cargo.Kind == KingdomConstructionInputKind.Water)
			{
				if (source.Phase == KingdomConstructionInputSourcePhase.Reserved)
					return (cargo.Phase == KingdomConstructionInputCargoPhase.CreateIntent
						|| cargo.Phase == KingdomConstructionInputCargoPhase.AtSource
							&& !string.IsNullOrEmpty(cargo.ObjectId))
						&& ExactRoutedInputCask(carrier, item, job, receipt, cargo, 0);
				if (source.Phase != KingdomConstructionInputSourcePhase.TransferIntent
					|| cargo.Phase != KingdomConstructionInputCargoPhase.AtSource
						&& cargo.Phase != KingdomConstructionInputCargoPhase.PickupIntent
					|| string.IsNullOrEmpty(cargo.ObjectId))
					return false;
				GameObject vessel;
				FindExactId(zone, source.SourceObjectId, out vessel);
				LiquidVolume liquid = vessel?.GetPart<LiquidVolume>();
				return ExactRoutedInputWaterSource(zone, source, vessel, liquid, source.Before)
					&& ExactRoutedInputCask(carrier, item, job, receipt, cargo, 0)
					|| cargo.Phase == KingdomConstructionInputCargoPhase.PickupIntent
						&& ExactRoutedInputWaterSource(zone, source, vessel, liquid,
							source.ResidualAfter)
						&& ExactRoutedInputCask(carrier, item, job, receipt, cargo, source.Take);
			}
			if (source.Phase != KingdomConstructionInputSourcePhase.TransferIntent
				|| cargo.Phase != KingdomConstructionInputCargoPhase.PickupIntent) return false;
			return ExactRoutedMaterialAtCarrier(zone, carrier, item, job, receipt,
				source, cargo, cargo.CargoKey,
				KingdomPurpose.HasProtectedCargoEvidence(item) ? -1 : 1);
		}

		private static bool ExactLoadedPickupCargo(Zone zone, GameObject carrier,
			GameObject item, KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputCargoLine cargo,
			KingdomConstructionInputSourceLine source, bool debited)
		{
			if (!ReferenceEquals(item?.InInventory, carrier)
				|| ReferenceCount(carrier.Inventory.Objects, item) != 1) return false;
			if (cargo.Kind != KingdomConstructionInputKind.Water)
				return ExactRoutedMaterialAtCarrier(zone, carrier, item, job, receipt,
					source, cargo, cargo.CargoKey,
					KingdomPurpose.HasProtectedCargoEvidence(item) ? -1 : 1);
			if (debited)
				return ExactRoutedInputCask(carrier, item, job, receipt, cargo, source.Take);
			return ExactCurrentPickupCut(zone, carrier, item, job, receipt, cargo, source);
		}
	}
}
