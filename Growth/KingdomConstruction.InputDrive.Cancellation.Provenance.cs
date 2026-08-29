using System.Collections.Generic;

using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		/// <summary>Reads a marker only on current attended ground. Duplicate roots, aliases,
		/// cycles, bounds overflow, and a second matching object all refuse adoption.</summary>
		private static int FindActiveCancellationMarker(Zone zone, string marker,
			out GameObject exact)
		{
			exact = null;
			if (zone == null || string.IsNullOrEmpty(marker)
				|| KingdomSurvey.ActiveFor(zone) == null) return -1;
			HashSet<GameObject> seen = new HashSet<GameObject>();
			int matches = 0;
			int roots = 0;
			int nodes = 0;
			foreach (GameObject root in zone.GetObjects())
			{
				if (++roots > 1024) return -1;
				if (!GameObject.Validate(root) || root.CurrentZone != zone
					|| !KingdomOrdinaryCustody.TryCollect(root,
						out List<GameObject> graph, out string _)) return -1;
				for (int i = 0; i < graph.Count; i++)
				{
					if (++nodes > KingdomOrdinaryCustody.MaxNodes) return -1;
					GameObject item = graph[i];
					if (!seen.Add(item)) return -1;
					if (item.GetStringProperty(InputMarkerProperty) != marker) continue;
					matches++;
					if (matches == 1) exact = item;
				}
			}
			if (matches != 1) exact = null;
			return matches;
		}

		private static bool ExactCancellationSourceStanding(Zone zone,
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputSourceLine source,
			KingdomConstructionInputCargoLine cargo, GameObject carrier)
		{
			if (zone == null || source == null || zone.ZoneID != source.SourceZoneId
				|| KingdomSurvey.ActiveFor(zone) == null
				|| FindExactId(zone, source.HolderId, out GameObject holder)
					!= KingdomPhysicalLookupState.Exact
				|| !GameObject.Validate(holder) || holder.CurrentZone != zone
				|| holder.CurrentCell != zone.GetCell(source.X, source.Y)) return false;
			if (source.Kind == KingdomConstructionInputKind.Water)
			{
				XRL.World.Parts.LiquidVolume liquid = holder.GetPart<XRL.World.Parts.LiquidVolume>();
				if (!ExactRoutedInputWaterSource(zone, source, holder, liquid, -1)) return false;
				if (!GameObject.Validate(carrier)) return liquid.Volume == source.Before;
				int matches = FindCarrierMarker(carrier, cargo.CreationMarker,
					out GameObject cask);
				if (string.IsNullOrEmpty(cargo.ObjectId))
					return matches == 0 && liquid.Volume == source.Before
						|| matches == 1 && ExactRoutedInputCask(carrier, cask, job, receipt,
							cargo, liquid.Volume == source.Before ? 0 : source.Take);
				if (matches == 0 && liquid.Volume == source.Before
					&& (cargo.Phase == KingdomConstructionInputCargoPhase.ReleaseIntent
						|| cargo.Phase == KingdomConstructionInputCargoPhase.Released
						|| cargo.Phase == KingdomConstructionInputCargoPhase.CompensationIntent
						|| cargo.Phase == KingdomConstructionInputCargoPhase.Compensated))
					return FindGlobalInputId(receipt, cargo.ObjectId, out GameObject retired,
						out bool graveyard) == KingdomPhysicalLookupState.Exact && graveyard
						&& ExactGraveyardCancelledWaterCask(job, receipt, cargo, retired);
				return matches == 1 && cask.IDIfAssigned == cargo.ObjectId
					&& (liquid.Volume == source.Before
						&& ExactRoutedInputCask(carrier, cask, job, receipt, cargo, 0)
						|| liquid.Volume == source.ResidualAfter
						&& ExactRoutedInputCask(carrier, cask, job, receipt, cargo, source.Take));
			}
			if (holder.Inventory == null) return false;
			KingdomPhysicalLookupState state = FindGlobalInputId(receipt,
				cargo.ObjectId ?? source.SourceObjectId, out GameObject item, out bool graveyard);
			if (state != KingdomPhysicalLookupState.Exact || graveyard
				|| !GameObject.Validate(item)) return false;
			if (ReferenceEquals(item.InInventory, holder))
			{
				bool protectedCargo = KingdomPurpose.HasProtectedCargoEvidence(item);
				int ordinaryPolicy = protectedCargo ? -1 : 0;
				int routePolicy = protectedCargo ? -1 : 1;
				return ReferenceCount(holder.Inventory.Objects, item) == 1
					&& ExactInputDedication(zone, source, holder, item)
					&& (ExactRoutedMaterialObject(job, receipt, source, cargo, item,
						source.Before, null, ordinaryPolicy)
					|| (source.Phase == KingdomConstructionInputSourcePhase.RestoreIntent
						|| source.Phase == KingdomConstructionInputSourcePhase.CompensationIntent)
						&& (ExactRoutedMaterialObject(job, receipt, source, cargo, item,
							source.Before, cargo.CargoKey, routePolicy)
					|| source.Phase == KingdomConstructionInputSourcePhase.RestoreIntent
						&& cargo.Phase == KingdomConstructionInputCargoPhase.ReleaseIntent
						&& ExactCancellationSplitWriteAheadStanding(zone, holder, item,
							job, receipt, source, cargo)
					|| ExactRoutedMaterialObject(job, receipt, source, cargo, item,
							source.Take, cargo.CargoKey, routePolicy)));
			}
			return GameObject.Validate(carrier) && carrier.Inventory != null
				&& ReferenceEquals(item.InInventory, carrier)
				&& ReferenceCount(carrier.Inventory.Objects, item) == 1
				&& ExactRoutedMaterialObject(job, receipt, source, cargo, item,
					source.Take, cargo.CargoKey,
					KingdomPurpose.HasProtectedCargoEvidence(item) ? -1 : 1);
		}
	}
}
