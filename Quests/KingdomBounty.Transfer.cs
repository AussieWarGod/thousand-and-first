using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomBounty
	{
		private sealed class InventoryFrame
		{
			internal GameObject Owner;
			internal Inventory Part;
			internal List<GameObject> List;
			internal GameObject[] Items;
			internal string[] ItemIds;
			internal int[] Counts;
			internal Zone Zone;
			internal Cell Cell;
			internal string Id;
		}

		private static bool TryCaptureInventory(GameObject Owner, Zone Z,
			out InventoryFrame Frame)
		{
			Frame = null;
			Inventory part = GameObject.Validate(Owner) ? Owner.Inventory : null;
			if (part == null || part.Objects == null || part.ParentObject != Owner
				|| Z == null || Owner.CurrentZone != Z || Owner.CurrentCell == null
				|| Owner.CurrentCell.ParentZone != Z) return false;
			GameObject[] items = part.Objects.ToArray();
			string[] ids = new string[items.Length];
			int[] counts = new int[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				GameObject item = items[i];
				if (!GameObject.Validate(item) || item.Physics == null || item.InInventory != Owner
					|| item.CurrentCell != null || item.Count <= 0
					|| string.IsNullOrEmpty(item.IDIfAssigned)) return false;
				for (int j = 0; j < i; j++) if (ReferenceEquals(items[j], item)) return false;
				ids[i] = item.IDIfAssigned;
				counts[i] = item.Count;
			}
			Frame = new InventoryFrame
			{
				Owner = Owner,
				Part = part,
				List = part.Objects,
				Items = items,
				ItemIds = ids,
				Counts = counts,
				Zone = Z,
				Cell = Owner.CurrentCell,
				Id = Owner.IDIfAssigned
			};
			return true;
		}

		private static bool InventoryHeaderExact(InventoryFrame Frame)
		{
			return Frame != null && GameObject.Validate(Frame.Owner) && Frame.Part != null
				&& Frame.Owner.IDIfAssigned == Frame.Id
				&& Frame.Owner.CurrentZone == Frame.Zone && Frame.Owner.CurrentCell == Frame.Cell
				&& Frame.Cell != null && Frame.Cell.ParentZone == Frame.Zone
				&& Frame.Part.ParentObject == Frame.Owner
				&& ReferenceEquals(Frame.Owner.Inventory, Frame.Part)
				&& ReferenceEquals(Frame.Part.Objects, Frame.List);
		}

		/// <summary>
		/// Every captured slot except the one at SkipIndex, still in order and still the same
		/// object: same reference, identity, count, holder and detached cell. Pass -1 to skip
		/// nothing. Callers prove the list length first, so the walk cannot run off the end.
		/// </summary>
		private static bool SlotsExact(InventoryFrame Frame, int SkipIndex)
		{
			int current = 0;
			for (int i = 0; i < Frame.Items.Length; i++)
			{
				if (i == SkipIndex) continue;
				GameObject item = Frame.Items[i];
				if (!ReferenceEquals(Frame.List[current++], item) || !GameObject.Validate(item)
					|| item.IDIfAssigned != Frame.ItemIds[i] || item.Count != Frame.Counts[i]
					|| item.InInventory != Frame.Owner
					|| item.CurrentCell != null) return false;
			}
			return true;
		}

		/// <summary>The rows the live list carries right now, for the pure subtraction law.</summary>
		private static bool ObservedRows(InventoryFrame Frame, out string[] Ids, out int[] Counts)
		{
			Ids = new string[Frame.List.Count];
			Counts = new int[Frame.List.Count];
			for (int i = 0; i < Frame.List.Count; i++)
			{
				GameObject item = Frame.List[i];
				if (!GameObject.Validate(item) || string.IsNullOrEmpty(item.IDIfAssigned)) return false;
				Ids[i] = item.IDIfAssigned;
				Counts[i] = item.Count;
			}
			return true;
		}

		/// <summary>Where a moved object's holder sits relative to one captured frame.</summary>
		private static BountyTransferLocation FrameOwnerLocation(GameObject Item,
			InventoryFrame Frame, BountyTransferLocation Held)
		{
			if (!GameObject.Validate(Item) || Frame == null) return BountyTransferLocation.Missing;
			GameObject holder = Item.InInventory;
			if (holder == null) return BountyTransferLocation.Detached;
			return (holder == Frame.Owner) ? Held : BountyTransferLocation.Elsewhere;
		}

		private static bool InventoryOriginalExact(InventoryFrame Frame)
		{
			return InventoryHeaderExact(Frame) && Frame.List.Count == Frame.Items.Length
				&& SlotsExact(Frame, -1);
		}

		/// <summary>
		/// The source list lost exactly the moved object and nothing else: values by the pure
		/// subtraction law, references and holders by the slot walk.
		///
		/// The moved object's OWN holder is deliberately not decided here. Inventory.AddObject
		/// assigns the destination as that holder before it returns, so this witness stays
		/// provable after the add; the detached holder is the removal step's proof alone and
		/// lives in InventoryMinusExact below.
		/// </summary>
		private static bool InventoryMinusListExact(InventoryFrame Frame, GameObject Removed,
			int Units)
		{
			if (!InventoryHeaderExact(Frame) || !GameObject.Validate(Removed)
				|| Removed.Count != Units || Removed.CurrentCell != null
				|| Frame.List.Contains(Removed)) return false;
			string[] ids;
			int[] counts;
			int removedIndex;
			if (!ObservedRows(Frame, out ids, out counts)
				|| !KingdomBountyRules.TransferRowsMinus(Frame.ItemIds, Frame.Counts, ids, counts,
					Removed.IDIfAssigned, Units, out removedIndex)) return false;
			return ReferenceEquals(Frame.Items[removedIndex], Removed)
				&& SlotsExact(Frame, removedIndex);
		}

		/// <summary>The source-minus witness plus the detached holder the removal step proves.</summary>
		private static bool InventoryMinusExact(InventoryFrame Frame, GameObject Removed,
			int Units)
		{
			return InventoryMinusListExact(Frame, Removed, Units)
				&& KingdomBountyRules.TransferOwnerExact(BountyTransferPhase.RemoveIntent,
					FrameOwnerLocation(Removed, Frame, BountyTransferLocation.SourceOnly));
		}

		private static bool InventoryPlusExact(InventoryFrame Frame, GameObject Added,
			int Units)
		{
			if (!InventoryHeaderExact(Frame) || !GameObject.Validate(Added)
				|| Added.Count != Units || Added.CurrentCell != null
				|| !KingdomBountyRules.TransferOwnerExact(BountyTransferPhase.AddIntent,
					FrameOwnerLocation(Added, Frame, BountyTransferLocation.DestinationOnly))
				|| Frame.List.Count != Frame.Items.Length + 1
				|| !SlotsExact(Frame, -1)) return false;
			return ReferenceEquals(Frame.List[Frame.Items.Length], Added);
		}

		private static bool ContinueTransfer(Zone Z, GameObject Notice, r_KingdomNotice Data)
		{
			if ((BountyTransferPhase)Data.TransferPhase != BountyTransferPhase.Bound)
			{
				Quarantine(Data, "A fetch transfer reloaded after a mutation intent; neither callback was repeated.");
				Data.TransferPhase = (int)BountyTransferPhase.Quarantined;
				return false;
			}
			GameObject item = GameObject.FindByID(Data.TransferItemId);
			GameObject source = GameObject.FindByID(Data.TransferSourceId);
			GameObject destination = GameObject.FindByID(Data.TransferDestinationId);
			Cell noticeCell = (Notice != null) ? Notice.CurrentCell : null;
			InventoryFrame sourceFrame;
			InventoryFrame destinationFrame;
			if (!GameObject.Validate(item) || source == destination || Data.TransferUnits <= 0
				|| item.Count != Data.TransferUnits || !NoticeBindingExact(Notice, Data, Z, noticeCell)
				|| !TryCaptureInventory(source, Z, out sourceFrame)
				|| !TryCaptureInventory(destination, Z, out destinationFrame)
				|| !sourceFrame.List.Contains(item) || item.InInventory != source
				|| destinationFrame.List.Contains(item)
				|| !KingdomConstructionInputLeaseAuthority
					.TryObjectGraphAvailableForOrdinaryTransfer(item, out _))
			{
				Quarantine(Data, "A bound fetch item or container can no longer be proved.");
				Data.TransferPhase = (int)BountyTransferPhase.Quarantined;
				return false;
			}
			string itemId = Data.TransferItemId;
			string sourceId = Data.TransferSourceId;
			string destinationId = Data.TransferDestinationId;
			int units = Data.TransferUnits;
			int totalBefore = Data.TransferTotalBefore;
			int creditedBefore = Data.TransferredUnits;
			if (totalBefore != creditedBefore)
			{
				Quarantine(Data, "A bound fetch transfer no longer matches its credited total.");
				Data.TransferPhase = (int)BountyTransferPhase.Quarantined;
				return false;
			}
			if (!KingdomConstructionInputLeaseAuthority
				.TryObjectGraphAvailableForOrdinaryTransfer(item, out _))
			{
				Quarantine(Data, "The bound fetch item became protected purpose cargo before removal.");
				Data.TransferPhase = (int)BountyTransferPhase.Quarantined;
				return false;
			}
			Data.TransferPhase = (int)BountyTransferPhase.RemoveIntent;
			try
			{
				sourceFrame.Part.RemoveObject(item);
			}
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst bounty fetch removal", error);
			}
			KingdomSurvey.ObserveCurrentTopologyInActive(Z, sourceFrame.Owner);
			if (!TransferReceiptExact(Data, BountyTransferPhase.RemoveIntent, itemId,
				sourceId, destinationId, units, totalBefore, creditedBefore)
				|| item.IDIfAssigned != itemId
				|| !NoticeBindingExact(Notice, Data, Z, noticeCell)
				|| !InventoryMinusExact(sourceFrame, item, units)
				|| !InventoryOriginalExact(destinationFrame)
				|| !KingdomConstructionInputLeaseAuthority
					.TryObjectGraphAvailableForOrdinaryTransfer(item, out _))
			{
				Quarantine(Data, "The fetch removal callback changed an exact item, inventory, list, owner, cell, zone, notice, or count witness.");
				Data.TransferPhase = (int)BountyTransferPhase.Quarantined;
				return false;
			}
			Data.TransferPhase = (int)BountyTransferPhase.Detached;
			Data.TransferPhase = (int)BountyTransferPhase.AddIntent;
			GameObject accepted = null;
			if (!KingdomConstructionInputLeaseAuthority
				.TryObjectGraphAvailableForOrdinaryTransfer(item, out _))
			{
				Quarantine(Data, "The detached fetch custody graph became protected before addition.");
				Data.TransferPhase = (int)BountyTransferPhase.Quarantined;
				return false;
			}
			try
			{
				accepted = destinationFrame.Part.AddObject(item, Silent: true, NoStack: true);
			}
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst bounty fetch addition", error);
			}
			KingdomSurvey.ObserveCurrentTopologyInActive(Z, destinationFrame.Owner);
			KingdomSurvey.ObserveAddResultInActive(Z, item, accepted);
			if (!TransferReceiptExact(Data, BountyTransferPhase.AddIntent, itemId,
				sourceId, destinationId, units, totalBefore, creditedBefore)
				|| item.IDIfAssigned != itemId
				|| !NoticeBindingExact(Notice, Data, Z, noticeCell)
				|| !InventoryMinusListExact(sourceFrame, item, units)
				|| !InventoryPlusExact(destinationFrame, item, units)
				|| !KingdomConstructionInputLeaseAuthority
					.TryObjectGraphAvailableForOrdinaryTransfer(item, out _))
			{
				Quarantine(Data, "The fetch addition callback changed an exact item, inventory, list, owner, cell, zone, notice, or count witness.");
				Data.TransferPhase = (int)BountyTransferPhase.Quarantined;
				return false;
			}
			Data.TransferPhase = (int)BountyTransferPhase.Arrived;
			Data.TransferredUnits = totalBefore + units;
			Data.TransferPhase = (int)BountyTransferPhase.None;
			Data.TransferItemId = null;
			Data.TransferSourceId = null;
			Data.TransferDestinationId = null;
			Data.TransferUnits = 0;
			return true;
		}

		private static bool TransferReceiptExact(r_KingdomNotice Data,
			BountyTransferPhase Phase, string ItemId, string SourceId, string DestinationId,
			int Units, int TotalBefore, int CreditedBefore)
		{
			return Data != null && (BountyTransferPhase)Data.TransferPhase == Phase
				&& Data.TransferItemId == ItemId && Data.TransferSourceId == SourceId
				&& Data.TransferDestinationId == DestinationId && Data.TransferUnits == Units
				&& Data.TransferTotalBefore == TotalBefore
				&& Data.TransferredUnits == CreditedBefore;
		}

	}
}
