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
					|| string.IsNullOrEmpty(item.ID)) return false;
				for (int j = 0; j < i; j++) if (ReferenceEquals(items[j], item)) return false;
				ids[i] = item.ID;
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
				Id = Owner.ID
			};
			return true;
		}

		private static bool InventoryHeaderExact(InventoryFrame Frame)
		{
			return Frame != null && GameObject.Validate(Frame.Owner) && Frame.Part != null
				&& Frame.Owner.ID == Frame.Id
				&& Frame.Owner.CurrentZone == Frame.Zone && Frame.Owner.CurrentCell == Frame.Cell
				&& Frame.Cell != null && Frame.Cell.ParentZone == Frame.Zone
				&& Frame.Part.ParentObject == Frame.Owner
				&& ReferenceEquals(Frame.Owner.Inventory, Frame.Part)
				&& ReferenceEquals(Frame.Part.Objects, Frame.List);
		}

		private static bool InventoryOriginalExact(InventoryFrame Frame)
		{
			if (!InventoryHeaderExact(Frame) || Frame.List.Count != Frame.Items.Length) return false;
			for (int i = 0; i < Frame.Items.Length; i++)
			{
				GameObject item = Frame.Items[i];
				if (!ReferenceEquals(Frame.List[i], item) || !GameObject.Validate(item)
					|| item.ID != Frame.ItemIds[i] || item.Count != Frame.Counts[i]
					|| item.InInventory != Frame.Owner
					|| item.CurrentCell != null) return false;
			}
			return true;
		}

		private static bool InventoryMinusExact(InventoryFrame Frame, GameObject Removed,
			int Units)
		{
			if (!InventoryHeaderExact(Frame) || !GameObject.Validate(Removed)
				|| Removed.Count != Units || Removed.InInventory != null
				|| Removed.CurrentCell != null || Frame.List.Contains(Removed)) return false;
			int removedIndex = -1;
			for (int i = 0; i < Frame.Items.Length; i++)
				if (ReferenceEquals(Frame.Items[i], Removed))
				{
					if (removedIndex >= 0) return false;
					removedIndex = i;
				}
			if (removedIndex < 0 || Frame.List.Count != Frame.Items.Length - 1) return false;
			int current = 0;
			for (int i = 0; i < Frame.Items.Length; i++)
			{
				if (i == removedIndex) continue;
				GameObject item = Frame.Items[i];
				if (!ReferenceEquals(Frame.List[current++], item) || !GameObject.Validate(item)
					|| item.ID != Frame.ItemIds[i] || item.Count != Frame.Counts[i]
					|| item.InInventory != Frame.Owner
					|| item.CurrentCell != null) return false;
			}
			return true;
		}

		private static bool InventoryPlusExact(InventoryFrame Frame, GameObject Added,
			int Units)
		{
			if (!InventoryHeaderExact(Frame) || !GameObject.Validate(Added)
				|| Added.Count != Units || Added.InInventory != Frame.Owner
				|| Added.CurrentCell != null || Frame.List.Count != Frame.Items.Length + 1) return false;
			for (int i = 0; i < Frame.Items.Length; i++)
			{
				GameObject item = Frame.Items[i];
				if (!ReferenceEquals(Frame.List[i], item) || !GameObject.Validate(item)
					|| item.ID != Frame.ItemIds[i] || item.Count != Frame.Counts[i]
					|| item.InInventory != Frame.Owner
					|| item.CurrentCell != null) return false;
			}
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
				|| destinationFrame.List.Contains(item))
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
				|| item.ID != itemId
				|| !NoticeBindingExact(Notice, Data, Z, noticeCell)
				|| !InventoryMinusExact(sourceFrame, item, units)
				|| !InventoryOriginalExact(destinationFrame))
			{
				Quarantine(Data, "The fetch removal callback changed an exact item, inventory, list, owner, cell, zone, notice, or count witness.");
				Data.TransferPhase = (int)BountyTransferPhase.Quarantined;
				return false;
			}
			Data.TransferPhase = (int)BountyTransferPhase.Detached;
			Data.TransferPhase = (int)BountyTransferPhase.AddIntent;
			GameObject accepted = null;
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
				|| item.ID != itemId
				|| !NoticeBindingExact(Notice, Data, Z, noticeCell)
				|| !InventoryMinusExact(sourceFrame, item, units)
				|| !InventoryPlusExact(destinationFrame, item, units))
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
