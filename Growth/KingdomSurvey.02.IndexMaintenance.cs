using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSurvey
	{

		private static bool BelongsToRealm(KingdomSystem citizenshipSystem, GameObject item)
		{
			return KingdomCitizenship.BelongsTo(citizenshipSystem, item);
		}

		private void Publish(IndexedRow Row, bool Add)
		{
			int sign = Add ? 1 : -1;
			Citizens += sign * (Row.Citizen ? 1 : 0);
			TradePosts += sign * (Row.TradePost ? 1 : 0);
			Beds += sign * (Row.Bed ? 1 : 0);
			Kitchens += sign * (Row.Kitchen ? 1 : 0);
			FoodStored += sign * Row.FoodStored;
			FoodCapacity += sign * Row.FoodCapacity;
			StoredWater += sign * Row.StoredWater;
			OpenWater += sign * Row.OpenWater;
			StorageSpace += sign * Row.StorageSpace;
			StorageCapacity += sign * Row.StorageCapacity;
			Publish(CitizenBodies, Row, Row.Citizen, Add);
			Publish(Settlers, Row, Row.Settler, Add);
			Publish(Built, Row, Row.Built, Add);
			Publish(Works, Row, Row.Work, Add);
			Publish(Defences, Row, Row.Defence, Add);
			Publish(Larders, Row, Row.Larder, Add);
			Publish(Raiders, Row, Row.Raider, Add);
			Publish(Cairns, Row, Row.Cairn, Add);
			Publish(PlotWorks, Row, Row.PlotWorks, Add);
			Publish(Improvements, Row, Row.Improvement, Add);
			Publish(Notices, Row, Row.Notice, Add);
			Publish(Shrines, Row, Row.Shrine, Add);
			Publish(Guests, Row, Row.Guest, Add);
			Publish(NotableGuests, Row, Row.NotableGuest, Add);
			Publish(CausalPilgrims, Row, Row.CausalPilgrim, Add);
			Publish(Clearances, Row, Row.Clearance, Add);
			Publish(ConstructionRoots, Row, Row.ConstructionRoot, Add);
			Publish(PlotRoots, Row, Row.PlotRoot, Add);
			Publish(LayoutRoots, Row, Row.LayoutRoot, Add);
			Publish(CropRows, Row, Row.CropRow, Add);
			Publish(NetworkPieces, Row, Row.NetworkPiece, Add);
			Publish(LabJobs, Row, Row.LabJob, Add);
			Publish(VisualRoots, Row, Row.VisualRoot, Add);
			Publish(PlotParts, Row, Row.PlotPart, Add);
			Publish(ArchitectureComponents, Row, Row.ArchitectureComponent, Add);
			Publish(GatehouseSatellites, Row, Row.GatehouseSatellite, Add);
			Publish(DelveEndpoints, Row, Row.DelveEndpoint, Add);
			Publish(Furnishings, Row, Row.Furnishing, Add);
			Publish(HeartRelics, Row, Row.HeartRelic, Add);
			Publish(MaterialStockpiles, Row, Row.MaterialStockpile, Add);
			Publish(ResidentBodies, Row, Row.ResidentId > 0, Add);
			Publish(Transients, Row, Row.Transient, Add);
			Publish(Stores, Row, Row.Store, Add);
			Publish(Pools, Row, Row.Pool, Add);
			FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
		}

		private void Publish(List<GameObject> List, IndexedRow Row, bool Member, bool Add)
		{
			if (!Member) return;
			if (!Add)
			{
				List.Remove(Row.Item);
				return;
			}
			int low = 0;
			int high = List.Count;
			while (low < high)
			{
				int middle = low + ((high - low) / 2);
				IndexedRow existing;
				if (!Rows.TryGetValue(List[middle], out existing)
					|| KingdomSurveyIndexRules.ComesBeforeOrEqual(existing.Order, Row.Order)) low = middle + 1;
				else high = middle;
			}
			List.Insert(low, Row.Item);
		}

		private void Publish(List<LiquidVolume> List, IndexedRow Row, bool Member, bool Add)
		{
			if (!Member || Row.Liquid == null) return;
			if (!Add)
			{
				List.Remove(Row.Liquid);
				return;
			}
			int low = 0;
			int high = List.Count;
			while (low < high)
			{
				int middle = low + ((high - low) / 2);
				IndexedRow existing;
				GameObject owner = List[middle]?.ParentObject;
				if (owner == null || !Rows.TryGetValue(owner, out existing)
					|| KingdomSurveyIndexRules.ComesBeforeOrEqual(existing.Order, Row.Order)) low = middle + 1;
				else high = middle;
			}
			List.Insert(low, Row.Liquid);
		}

		private void IndexLoadedBranch(IndexedRow Row)
		{
			List<GameObject> pending = new List<GameObject> { Row.Item };
			for (int cursor = 0; cursor < pending.Count; cursor++)
			{
				GameObject item = pending[cursor];
				if (!GameObject.Validate(item) || !LoadedSet.Add(item))
				{
					LoadedIndexComplete = false;
					continue;
				}
				if (LoadedObjects.Count >= MaxIndexedObjects)
				{
					LoadedIndexComplete = false;
					LoadedSet.Remove(item);
					continue;
				}
				LoadedObjects.Add(item);
				Row.Loaded.Add(item);
				Inventory inventory = item.Inventory;
				if (inventory == null || inventory.Objects == null) continue;
				for (int i = 0; i < inventory.Objects.Count; i++) pending.Add(inventory.Objects[i]);
			}
		}

		private void RemoveLoadedBranch(IndexedRow Row)
		{
			for (int i = 0; i < Row.Loaded.Count; i++)
			{
				LoadedSet.Remove(Row.Loaded[i]);
				LoadedObjects.Remove(Row.Loaded[i]);
			}
			Row.Loaded.Clear();
		}

		/// <summary>Publishes one new root into every index. Unknown off-ground objects are refused.</summary>
		public bool ObserveAdded(GameObject Item)
		{
			bool known = Item != null && Rows.ContainsKey(Item);
			bool valid = GameObject.Validate(Item);
			bool here = valid && Ground != null && ReferenceEquals(Item.CurrentZone, Ground)
				&& Item.CurrentCell != null && ReferenceEquals(Item.CurrentCell.ParentZone, Ground);
			KingdomSurveyIndexRules.Mutation action = KingdomSurveyIndexRules.Classify(known, valid, here);
			if (action == KingdomSurveyIndexRules.Mutation.Refresh) return ObserveChanged(Item);
			if (action != KingdomSurveyIndexRules.Mutation.Add) return false;
			AddRoot(Item, The.Game?.GetSystem<KingdomSystem>());
			bool added = Rows.ContainsKey(Item);
			if (added) AddedMutations++;
			return added;
		}

		/// <summary>Reclassifies one exact known root after a physical/property commit.</summary>
		public bool ObserveChanged(GameObject Item)
		{
			IndexedRow old = null;
			bool known = Item != null && Rows.TryGetValue(Item, out old);
			bool valid = GameObject.Validate(Item);
			bool here = valid && Ground != null && ReferenceEquals(Item.CurrentZone, Ground)
				&& Item.CurrentCell != null && ReferenceEquals(Item.CurrentCell.ParentZone, Ground);
			KingdomSurveyIndexRules.Mutation action = KingdomSurveyIndexRules.Classify(known, valid, here);
			if (action == KingdomSurveyIndexRules.Mutation.Remove) return ObserveRemoved(Item);
			if (action == KingdomSurveyIndexRules.Mutation.Add) return ObserveAdded(Item);
			if (action != KingdomSurveyIndexRules.Mutation.Refresh) return false;
			Publish(old, false);
			RemoveLoadedBranch(old);
			IndexedRow fresh = Capture(Item, The.Game?.GetSystem<KingdomSystem>(), old.Order);
			if (old.Work != fresh.Work || old.Settler != fresh.Settler
				|| old.ResidentId != fresh.ResidentId)
				Simulation.City.KingdomStations.TouchAvailability(Item);
			Rows[Item] = fresh;
			Publish(fresh, true);
			IndexLoadedBranch(fresh);
			ChangedMutations++;
			return true;
		}

		/// <summary>Re-proves the actual topology after an engine callback threw. Qud callbacks may
		/// apply their physical effect before raising: a known survivor refreshes, a known absence
		/// removes, and an unknown object that actually landed on this ground is added.</summary>
		public bool ObserveCurrentTopology(GameObject Item)
		{
			return ObserveChanged(Item);
		}

		/// <summary>Removes one known root after its exact destruction/move commits.</summary>
		public bool ObserveRemoved(GameObject Item)
		{
			IndexedRow row;
			if (Item == null || !Rows.TryGetValue(Item, out row)) return false;
			// A moved endpoint can return before the next crew pass. Preserve that interruption
			// on the object itself instead of inferring continuity from its restored identity.
			if (row.Work || row.ResidentId > 0)
				Simulation.City.KingdomStations.TouchAvailability(Item);
			Publish(row, false);
			RemoveLoadedBranch(row);
			Rows.Remove(Item);
			Objects.Remove(Item);
			RemovedMutations++;
			return true;
		}

		/// <summary>Updates a receipt-bound object's cached contribution after the caller already
		/// published the exact aggregate delta. Category changes are refused as mixed evidence.</summary>
		internal bool SynchronizeReceiptObject(GameObject Item)
		{
			IndexedRow old;
			if (Item == null || !Rows.TryGetValue(Item, out old) || !GameObject.Validate(Item)) return false;
			IndexedRow fresh = Capture(Item, The.Game?.GetSystem<KingdomSystem>(), old.Order);
			if (!SameShape(old, fresh)) return false;
			RemoveLoadedBranch(old);
			Rows[Item] = fresh;
			IndexLoadedBranch(fresh);
			return true;
		}

		private static bool SameShape(IndexedRow A, IndexedRow B)
		{
			return A.Citizen == B.Citizen && A.Settler == B.Settler
				&& A.TradePost == B.TradePost && A.Built == B.Built && A.Bed == B.Bed
				&& A.Kitchen == B.Kitchen && A.Work == B.Work && A.Defence == B.Defence
				&& A.Larder == B.Larder && A.Pool == B.Pool && A.Store == B.Store
				&& A.Raider == B.Raider && A.Cairn == B.Cairn && A.PlotWorks == B.PlotWorks
				&& A.Improvement == B.Improvement && A.Notice == B.Notice
				&& A.Shrine == B.Shrine && A.Guest == B.Guest
				&& A.NotableGuest == B.NotableGuest && A.CausalPilgrim == B.CausalPilgrim
				&& A.Clearance == B.Clearance && A.ConstructionRoot == B.ConstructionRoot
				&& A.PlotRoot == B.PlotRoot && A.LayoutRoot == B.LayoutRoot
				&& A.CropRow == B.CropRow && A.NetworkPiece == B.NetworkPiece
				&& A.LabJob == B.LabJob && A.VisualRoot == B.VisualRoot
				&& A.PlotPart == B.PlotPart
				&& A.ArchitectureComponent == B.ArchitectureComponent
				&& A.GatehouseSatellite == B.GatehouseSatellite
				&& A.DelveEndpoint == B.DelveEndpoint
				&& A.Furnishing == B.Furnishing && A.HeartRelic == B.HeartRelic
				&& A.MaterialStockpile == B.MaterialStockpile
				&& A.Transient == B.Transient && A.ResidentId == B.ResidentId
				&& ReferenceEquals(A.Liquid, B.Liquid);
		}

		internal bool TryLoaded(out IList<GameObject> Loaded)
		{
			Loaded = LoadedObjects;
			return LoadedIndexComplete;
		}

		private void EmitPassReceipt()
		{
			if (!KingdomLog.Enabled) return;
			KingdomLog.Log("survey: zone=" + (Ground?.ZoneID ?? "<none>")
				+ " classifications=" + ClassificationPasses
				+ " foreign=" + ForeignClassifications
				+ " roots=" + ClassifiedRoots + " indexed=" + Objects.Count
				+ " reuses=" + ActiveReuses + " added=" + AddedMutations
				+ " changed=" + ChangedMutations + " removed=" + RemovedMutations);
		}

	}
}
