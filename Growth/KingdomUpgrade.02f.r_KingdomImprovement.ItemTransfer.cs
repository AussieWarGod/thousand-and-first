using System;
using XRL;
using XRL.World;

namespace XRL.World.Parts
{
	public partial class r_KingdomImprovement
	{
		private static bool TryGetManifestCount(GameObject Target,
			r_KingdomImprovement Receipt, out int Count, out string Failure)
		{
			Count = 0;
			HandoverManifestState state;
			if (!TryReadManifest(Target, out state, out Failure)) return false;
			if (state.SourceId != Receipt?.HandoverSourceId
				|| state.TargetId != Receipt.HandoverTargetId
				|| state.ConstructionReceipt != Receipt.HandoverConstructionReceipt)
				return ManifestFailure(out Failure, "Inventory manifest authority changed.");
			Count = state.Count;
			return true;
		}

		private static bool TryGetNextManifestItem(GameObject Target,
			r_KingdomImprovement Receipt, out GameObject Item, out string Failure)
		{
			Item = null;
			HandoverManifestState state;
			if (!TryReadManifest(Target, out state, out Failure)) return false;
			int index = Receipt == null ? -1 : Receipt.HandoverMovedItems;
			if (Receipt.HandoverItemPhase != 0 || index < 0 || index >= state.Count)
				return ManifestFailure(out Failure, "Next inventory manifest index is invalid.");
			object rooted;
			if (The.Game == null || !The.Game.ObjectGameState.TryGetValue(state.Roots[index],
					out rooted) || (Item = rooted as GameObject) == null
				|| !ExactManifestItem(state, index, Item))
				return ManifestFailure(out Failure, "Next inventory manifest item lost custody.");
			return true;
		}

		private static bool TransferManifestItem(GameObject Source, GameObject Target,
			Cell Where, r_KingdomImprovement Receipt, GameObject Item)
		{
			string destination = Target?.Inventory != null ? Target.IDIfAssigned : CellKey(Where);
			if (!BoundedIdentity(destination)
				|| !TryPublishPendingItem(Source, Target, Where, Receipt, Item, destination))
				return false;
			Inventory sourceInventory = Source.Inventory;
			if (sourceInventory == null) return FailHandover(Receipt,
				"Frozen inventory source disappeared before item removal.");
			bool removed;
			try
			{
				removed = sourceInventory.RemoveObjectFromInventory(Item, null,
					Silent: true, NoStack: true);
			}
			catch (Exception exception)
			{
				ObserveHandoverMutation(Source, Target, Where, Item);
				if (!ReproveManifestAfterCallback(Source, Target, Where, Receipt)) return false;
				if (!ReferenceEquals(sourceInventory, Source.Inventory)
					|| !ExactHandoverObjects(Source, Target, Receipt))
					return FailHandover(Receipt,
						"Inventory removal changed an endpoint before throwing: "
						+ exception.Message);
				if (ExactItemOwner(Item, Source, Receipt))
				{
					if (!RetirePendingItem(Receipt, Item)) return false;
					Receipt.HandoverFailure =
						"Inventory removal threw before changing ownership: " + exception.Message;
					return false;
				}
				if (ExactDestination(Item, Target, Where, Receipt))
					return SettlePendingItem(Target, Where, Receipt, Item);
				if (ExactLooseItem(Item, Receipt))
				{
					Receipt.HandoverItemPhase = 2;
					return PlacePendingItem(Source, Target, Where, Receipt, Item);
				}
				return FailHandover(Receipt,
					"Inventory removal lost, moved, replaced, or restacked its source before throwing: "
					+ exception.Message);
			}
			ObserveHandoverMutation(Source, Target, Where, Item);
			if (!ReproveManifestAfterCallback(Source, Target, Where, Receipt)) return false;
			if (!ReferenceEquals(sourceInventory, Source.Inventory))
				return FailHandover(Receipt,
					"Inventory source part changed during removal callback.");
			if (!removed)
			{
				if (ExactItemOwner(Item, Source, Receipt))
				{
					if (!RetirePendingItem(Receipt, Item)) return false;
					Receipt.HandoverFailure = "Inventory removal refused without an exact effect.";
					return false;
				}
				if (ExactDestination(Item, Target, Where, Receipt))
					return SettlePendingItem(Target, Where, Receipt, Item);
				if (ExactLooseItem(Item, Receipt))
					return RestoreItem(Source, Target, Where, Receipt, Item,
						"Inventory removal refused after removing its exact source item.");
				return FailHandover(Receipt,
					"Inventory removal refused after changing exact ownership.");
			}
			if (ExactItemOwner(Item, Source, Receipt))
				return FailHandover(Receipt,
					"Inventory removal reported success without changing ownership.");
			if (ExactDestination(Item, Target, Where, Receipt))
				return SettlePendingItem(Target, Where, Receipt, Item);
			if (!ExactLooseItem(Item, Receipt)) return FailHandover(Receipt,
				"Inventory removal lost, moved, replaced, or restacked its source.");
			Receipt.HandoverItemPhase = 2;
			return PlacePendingItem(Source, Target, Where, Receipt, Item);
		}
	}
}
