using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_KingdomImprovement
	{
		internal static bool CarryInventoryDurable(GameObject Source, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt, out int Moved)
		{
			Moved = Receipt == null ? 0 : Receipt.HandoverMovedItems;
			if (Receipt == null || !Receipt.HandoverFlagsValid())
				return FailHandover(Receipt, "Handover boolean flags are corrupt.");
			if (Receipt.HandoverQuarantined) return false;
			if (Receipt.HandoverMovedItems < 0) return FailHandover(Receipt,
				"Inventory moved count is corrupt.");
			if (!ExactHandoverObjects(Source, Target, Receipt))
				return FailHandover(Receipt, "Inventory endpoints changed before transfer.");
			if (Receipt.HandoverInventoryDone)
			{
				if (Receipt.HandoverItemPhase != 0
					|| !string.IsNullOrEmpty(Receipt.HandoverItemEscrowKey)
					|| (Source.Inventory != null && Source.Inventory.Objects.Count != 0))
					return FailHandover(Receipt,
						"Settled inventory handover no longer has an empty exact source.");
				Moved = Receipt.HandoverMovedItems;
				return true;
			}
			if (Source.Inventory == null)
			{
				if (Receipt.HandoverItemPhase != 0
					|| !string.IsNullOrEmpty(Receipt.HandoverItemEscrowKey))
					return FailHandover(Receipt,
						"Inventory source part disappeared with an item pending.");
				Receipt.HandoverInventoryDone = true;
				return true;
			}
			if (Receipt.HandoverItemPhase == 0
				&& !string.IsNullOrEmpty(Receipt.HandoverItemEscrowKey))
				return FailHandover(Receipt,
					"An inventory escrow root exists without its pending phase.");
			if (Receipt.HandoverItemPhase != 0
				&& !ResumePendingItem(Source, Target, Where, Receipt)) return false;
			List<GameObject> held = new List<GameObject>(Source.Inventory.Objects);
			for (int i = 0; i < held.Count; i++)
			{
				GameObject item = held[i];
				if (!ExactItemOwner(item, Source, Receipt: null))
					return FailHandover(Receipt, "Inventory source changed while enumerated.");
				if (!BoundedIdentity(item.ID) || string.IsNullOrEmpty(item.Blueprint)
					|| item.Blueprint.Length > 256 || item.Count <= 0
					|| Receipt.HandoverMovedItems == int.MaxValue)
					return FailHandover(Receipt, "Inventory item identity or count is out of bounds.");
				string destination = Target?.Inventory != null ? Target.ID : CellKey(Where);
				if (!BoundedIdentity(destination))
					return FailHandover(Receipt, "Inventory destination cannot be frozen exactly.");
				Receipt.HandoverItemId = item.ID;
				Receipt.HandoverItemBlueprint = item.Blueprint;
				Receipt.HandoverItemCount = item.Count;
				Receipt.HandoverItemDestinationKind = Target?.Inventory != null ? 1 : 2;
				Receipt.HandoverItemDestinationId = destination;
				Receipt.HandoverItemMovedBefore = Receipt.HandoverMovedItems;
				Receipt.HandoverItemMovedAfter = Receipt.HandoverMovedItems + 1;
				Receipt.HandoverItemEscrowKey = EscrowKeyFor(Source, item,
					Receipt.HandoverItemMovedBefore);
				if (!RootEscrowItem(Source, Target, Where, Receipt, item)) return false;
				Receipt.HandoverItemPhase = 1;
				Inventory sourceInventory = Source.Inventory;
				bool removed;
				try { removed = sourceInventory.RemoveObjectFromInventory(item, null,
					Silent: true, NoStack: true); }
				catch (System.Exception ex)
				{
					ObserveHandoverMutation(Source, Target, Where, item);
					if (!ReproveEscrowItem(Source, Target, Where, Receipt, item)) return false;
					if (!ReferenceEquals(sourceInventory, Source.Inventory)
						|| !ExactHandoverObjects(Source, Target, Receipt))
						return FailHandover(Receipt,
							"Inventory removal changed an endpoint before throwing: " + ex.Message);
					if (ExactItemOwner(item, Source, Receipt))
					{
						if (!RetirePendingItem(Receipt, item)) return false;
						Receipt.HandoverFailure = "Inventory removal threw before changing ownership: "
							+ ex.Message;
						return false;
					}
					if (ExactDestination(item, Target, Where, Receipt))
						return SettlePendingItem(Target, Where, Receipt, item);
					if (ExactLooseItem(item, Receipt))
					{
						Receipt.HandoverItemPhase = 2;
						return PlacePendingItem(Source, Target, Where, Receipt, item);
					}
					return FailHandover(Receipt,
						"Inventory removal lost, moved, replaced, or restacked its source before throwing: "
						+ ex.Message);
				}
				ObserveHandoverMutation(Source, Target, Where, item);
				if (!ReproveEscrowItem(Source, Target, Where, Receipt, item)) return false;
				if (!ReferenceEquals(sourceInventory, Source.Inventory))
					return FailHandover(Receipt,
						"Inventory source part changed during removal callback.");
				if (!removed)
				{
					if (ExactItemOwner(item, Source, Receipt))
					{
						if (!RetirePendingItem(Receipt, item)) return false;
						return false;
					}
					if (ExactDestination(item, Target, Where, Receipt))
						return SettlePendingItem(Target, Where, Receipt, item);
					if (ExactLooseItem(item, Receipt))
						return RestoreItem(Source, Target, Where, Receipt, item,
							"Inventory removal refused after removing its exact source item.");
					return FailHandover(Receipt,
						"Inventory removal refused after changing exact ownership.");
				}
				if (ExactItemOwner(item, Source, Receipt))
					return FailHandover(Receipt,
						"Inventory removal reported success without changing ownership.");
				if (ExactDestination(item, Target, Where, Receipt))
					return SettlePendingItem(Target, Where, Receipt, item);
				if (!ExactLooseItem(item, Receipt))
					return FailHandover(Receipt, "Inventory removal lost, moved, replaced, or restacked its source.");
				Receipt.HandoverItemPhase = 2;
				if (!PlacePendingItem(Source, Target, Where, Receipt, item)) return false;
			}
			if ((Source.Inventory != null && Source.Inventory.Objects.Count != 0)
				|| Receipt.HandoverItemPhase != 0)
				return FailHandover(Receipt,
					"Inventory source changed after its frozen items were transferred.");
			Moved = Receipt.HandoverMovedItems;
			Receipt.HandoverInventoryDone = true;
			return true;
		}

	}
}
