using System;
using XRL;
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
			if (Receipt == null || !Receipt.HandoverFlagsValid()
				|| !ExactHandoverControlTypes(Receipt))
				return FailHandover(Receipt, "Handover boolean flags are corrupt.");
			if (Receipt.HandoverQuarantined) return false;
			if (Receipt.HandoverMovedItems < 0
				|| Receipt.HandoverMovedItems > MaxHandoverTopologyObjects)
				return FailHandover(Receipt, "Inventory moved count is corrupt.");
			if (!ExactHandoverObjects(Source, Target, Receipt))
				return FailHandover(Receipt, "Inventory endpoints changed before transfer.");
			if (!TryPublishInventoryManifest(Source, Target, Where, Receipt)) return false;
			if (!TryFinishPendingItemCleanup(Receipt, null) || !ExactPendingItemTypes(Receipt))
				return false;
			string manifestFailure;
			int manifestCount;
			if (!TryGetManifestCount(Target, Receipt, out manifestCount, out manifestFailure))
				return FailHandover(Receipt, manifestFailure);
			if (Receipt.HandoverInventoryDone)
			{
				if (Receipt.HandoverItemPhase != 0
					|| !string.IsNullOrEmpty(Receipt.HandoverItemEscrowKey)
					|| HasPendingItemPrefix(Receipt.ParentObject)
					|| Receipt.HandoverMovedItems != manifestCount
					|| !VerifyHandoverContentCustody(Source, Target, Where, Receipt, true,
						out manifestFailure))
					return FailHandover(Receipt,
						manifestFailure ?? "Settled inventory custody changed.");
				Moved = Receipt.HandoverMovedItems;
				return true;
			}
			if (Receipt.HandoverItemPhase != 0
				&& !ResumePendingItem(Source, Target, Where, Receipt)) return false;
			if (!ReproveManifestAfterCallback(Source, Target, Where, Receipt)) return false;
			while (Receipt.HandoverMovedItems < manifestCount)
			{
				GameObject item;
				if (!TryGetNextManifestItem(Target, Receipt, out item, out manifestFailure))
					return FailHandover(Receipt, manifestFailure);
				if (!TransferManifestItem(Source, Target, Where, Receipt, item)) return false;
				if (!ReproveManifestAfterCallback(Source, Target, Where, Receipt)) return false;
			}
			if (Receipt.HandoverItemPhase != 0 || HasPendingItemPrefix(Receipt.ParentObject)
				|| !VerifyHandoverContentCustody(Source, Target, Where, Receipt, false,
					out manifestFailure))
				return FailHandover(Receipt,
					manifestFailure ?? "Inventory manifest did not settle exactly.");
			Receipt.HandoverInventoryDone = true;
			if (!VerifyHandoverContentCustody(Source, Target, Where, Receipt, true,
				out manifestFailure)) return FailHandover(Receipt, manifestFailure);
			Moved = Receipt.HandoverMovedItems;
			return Moved == manifestCount;
		}

		private static bool TryPublishPendingItem(GameObject Source, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt, GameObject Item, string Destination)
		{
			GameObject owner = Receipt.ParentObject;
			int destinationKind = Target?.Inventory != null ? 1 : 2;
			int before = Receipt.HandoverMovedItems;
			string escrow = EscrowKeyFor(Source, Item, before);
			if (Receipt.HandoverItemPhase != 0 || !BoundedEscrowKey(escrow)
				|| !ExactOrAbsentText(owner, HandoverPrefix + "ItemId", Item.ID)
				|| !ExactOrAbsentText(owner, HandoverPrefix + "ItemBlueprint", Item.Blueprint)
				|| !ExactOrAbsentText(owner, HandoverPrefix + "ItemDestinationId", Destination)
				|| !ExactOrAbsentText(owner, HandoverPrefix + "ItemEscrowKey", escrow)
				|| !ExactOrAbsentInt(owner, "ItemCount", Item.Count)
				|| !ExactOrAbsentInt(owner, "ItemDestinationKind", destinationKind)
				|| !ExactOrAbsentInt(owner, "ItemMovedBefore", before)
				|| !ExactOrAbsentInt(owner, "ItemMovedAfter", before + 1))
				return FailHandover(Receipt,
					"Pending inventory publication carries a third or malformed value.");
			try
			{
				Receipt.HandoverItemId = Item.ID;
				Receipt.HandoverItemBlueprint = Item.Blueprint;
				Receipt.HandoverItemCount = Item.Count;
				Receipt.HandoverItemDestinationKind = destinationKind;
				Receipt.HandoverItemDestinationId = Destination;
				Receipt.HandoverItemMovedBefore = before;
				Receipt.HandoverItemMovedAfter = before + 1;
				Receipt.HandoverItemEscrowKey = escrow;
				if (!RootEscrowItem(Source, Target, Where, Receipt, Item)) return false;
				Receipt.HandoverItemPhase = 1;
				return true;
			}
			catch (Exception exception)
			{
				Receipt.HandoverFailure = "Inventory intent publication remains retryable: "
					+ exception.Message;
				return false;
			}
		}

	}
}
