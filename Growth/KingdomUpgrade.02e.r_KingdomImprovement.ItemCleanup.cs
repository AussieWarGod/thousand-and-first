using System;
using XRL;
using XRL.World;

namespace XRL.World.Parts
{
	public partial class r_KingdomImprovement
	{
		private static bool BeginPendingItemCleanup(r_KingdomImprovement Receipt,
			GameObject Item)
		{
			GameObject owner = Receipt?.ParentObject;
			string key = Receipt?.HandoverItemEscrowKey;
			string itemId = Receipt?.HandoverItemId;
			int movedBefore = Receipt == null ? -1 : Receipt.HandoverItemMovedBefore;
			if (owner == null || !GameObject.Validate(Item) || !BoundedEscrowKey(key)
				|| !BoundedIdentity(itemId) || Item.IDIfAssigned != itemId || movedBefore < 0
				|| key != EscrowKeyFor(Receipt.HandoverSourceId, itemId, movedBefore)
				|| !ExactOrAbsentText(owner, HandoverPrefix + "ItemCleanupKey", key)
				|| !ExactOrAbsentText(owner, HandoverPrefix + "ItemCleanupId", itemId)
				|| !ExactOrAbsentInt(owner, "ItemCleanupMovedBefore", movedBefore)
				|| !ExactOrAbsentInt(owner, "ItemCleanupPhase", 0))
				return FailHandover(Receipt, "Inventory cleanup prefix carries a third value.");
			try
			{
				Receipt.HandoverText("ItemCleanupKey", key);
				Receipt.HandoverText("ItemCleanupId", itemId);
				Receipt.HandoverInt("ItemCleanupMovedBefore", movedBefore);
				Receipt.HandoverInt("ItemCleanupPhase", 1);
			}
			catch (Exception exception)
			{
				Receipt.HandoverFailure = "Inventory cleanup publication remains retryable: "
					+ exception.Message;
				return false;
			}
			return TryFinishPendingItemCleanup(Receipt, Item);
		}

		private static bool TryFinishPendingItemCleanup(r_KingdomImprovement Receipt,
			GameObject Expected)
		{
			GameObject owner = Receipt?.ParentObject;
			if (owner == null || owner.HasStringProperty(HandoverPrefix + "ItemCleanupPhase"))
				return FailHandover(Receipt, "Inventory cleanup phase is malformed.");
			int phase = Receipt.HandoverInt("ItemCleanupPhase");
			string key = Receipt.HandoverText("ItemCleanupKey");
			string itemId = Receipt.HandoverText("ItemCleanupId");
			int movedBefore = Receipt.HandoverInt("ItemCleanupMovedBefore");
			if (owner.HasIntProperty(HandoverPrefix + "ItemCleanupKey")
				|| owner.HasIntProperty(HandoverPrefix + "ItemCleanupId")
				|| owner.HasStringProperty(HandoverPrefix + "ItemCleanupMovedBefore")
				|| phase < 0 || phase > 3)
				return FailHandover(Receipt, "Inventory cleanup receipt is malformed.");
			if (phase == 0)
			{
				bool evidence = owner.HasStringProperty(HandoverPrefix + "ItemCleanupKey")
					|| owner.HasStringProperty(HandoverPrefix + "ItemCleanupId")
					|| owner.HasIntProperty(HandoverPrefix + "ItemCleanupMovedBefore");
				if (!evidence) return true;
				return (Receipt.HandoverItemPhase > 0
					&& ExactOrAbsentText(owner, HandoverPrefix + "ItemCleanupKey",
						Receipt.HandoverItemEscrowKey)
					&& ExactOrAbsentText(owner, HandoverPrefix + "ItemCleanupId",
						Receipt.HandoverItemId)
					&& ExactOrAbsentInt(owner, "ItemCleanupMovedBefore",
						Receipt.HandoverItemMovedBefore)) || FailHandover(Receipt,
						"Inventory cleanup prefix has no exact active item.");
			}
			if (The.Game == null)
				return FailHandover(Receipt, "Committed inventory cleanup has no game-state root.");
			if (phase == 1)
			{
				if (!BoundedEscrowKey(key) || !BoundedIdentity(itemId) || movedBefore < 0
					|| key != EscrowKeyFor(Receipt.HandoverSourceId, itemId, movedBefore)
					|| (Receipt.HandoverItemPhase != 0
						&& (key != Receipt.HandoverItemEscrowKey
							|| itemId != Receipt.HandoverItemId
							|| movedBefore != Receipt.HandoverItemMovedBefore)))
					return FailHandover(Receipt, "Committed inventory cleanup lost its identity.");
				if (!ExactCleanupItemState(Receipt)) return FailHandover(Receipt,
					"Committed inventory cleanup lost its exact physical item.");
				if (The.Game.ObjectGameState.TryGetValue(key, out object rooted))
				{
					GameObject exact = rooted as GameObject;
					if (!GameObject.Validate(exact) || exact.IDIfAssigned != itemId
						|| (Expected != null && !ReferenceEquals(exact, Expected)))
						return FailHandover(Receipt,
							"Inventory cleanup key points at foreign custody.");
					The.Game.ObjectGameState.Remove(key);
					if (The.Game.ObjectGameState.ContainsKey(key)) return FailHandover(Receipt,
						"Inventory cleanup could not retire its exact escrow root.");
				}
				Receipt.HandoverInt("ItemCleanupPhase", 2);
			}
			if (phase <= 2)
			{
				if (!ExactCleanupItemState(Receipt)) return FailHandover(Receipt,
					"Retired inventory custody lost its exact physical item.");
				Receipt.HandoverInt("ItemCleanupPhase", 3);
			}
			string retainedKey = BoundedEscrowKey(key)
				? key : Receipt.HandoverItemEscrowKey;
			if (BoundedEscrowKey(retainedKey)
				&& The.Game.ObjectGameState.ContainsKey(retainedKey))
				return FailHandover(Receipt, "Retired inventory escrow custody reappeared.");
			ClearPendingItem(Receipt);
			owner.RemoveStringProperty(HandoverPrefix + "ItemCleanupKey");
			owner.RemoveStringProperty(HandoverPrefix + "ItemCleanupId");
			owner.RemoveIntProperty(HandoverPrefix + "ItemCleanupMovedBefore");
			Receipt.HandoverInt("ItemCleanupPhase", 0);
			return true;
		}

		private static bool ExactPendingItemTypes(r_KingdomImprovement Receipt)
		{
			GameObject owner = Receipt.ParentObject;
			string[] ints = { "ItemCount", "ItemPhase", "ItemDestinationKind", "MovedItems",
				"ItemMovedBefore", "ItemMovedAfter", "InventoryDone" };
			string[] texts = { "ItemId", "ItemBlueprint", "ItemDestinationId", "ItemEscrowKey" };
			for (int i = 0; i < ints.Length; i++)
				if (owner.HasStringProperty(HandoverPrefix + ints[i]))
					return FailHandover(Receipt, "Inventory receipt has a string in an integer slot.");
			for (int i = 0; i < texts.Length; i++)
				if (owner.HasIntProperty(HandoverPrefix + texts[i]))
					return FailHandover(Receipt, "Inventory receipt has an integer in a text slot.");
			return (Receipt.HandoverItemPhase >= 0 && Receipt.HandoverItemPhase <= 4)
				|| FailHandover(Receipt, "Inventory item phase is outside its exact range.");
		}

		private static bool HasPendingItemPrefix(GameObject Owner)
		{
			return Owner != null && (Owner.HasStringProperty(HandoverPrefix + "ItemId")
				|| Owner.HasStringProperty(HandoverPrefix + "ItemEscrowKey")
				|| Owner.HasIntProperty(HandoverPrefix + "ItemCount"));
		}
	}
}
