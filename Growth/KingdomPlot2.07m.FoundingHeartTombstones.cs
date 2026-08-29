using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		private static bool FoundingHeartTombstoneIdentity(GameObject Object,
			KingdomFoundingHeartPlan Plan, int Slot)
		{
			if (Object == null || GameObject.Validate(Object)) return false;
			try
			{
				return Object.IDIfAssigned == KingdomFoundingHeartRules.SlotId(Plan, Slot)
					&& Object.GetStringProperty(FoundingHeartOwnerProperty) == Plan.TransactionId
					&& Object.GetIntProperty(FoundingHeartSlotProperty) == Slot + 1;
			}
			catch
			{
				KingdomLog.Log("founding heart: native graveyard identity is unreadable");
				return false;
			}
		}

		private static bool TryClassifyFoundingHeartTombstone(GameObject Object,
			KingdomFoundingHeartPlan Plan, out bool Relevant, out bool Exact)
		{
			Relevant = false;
			Exact = false;
			try
			{
				int slot = -1;
				for (int i = 0; i < KingdomFoundingHeartRules.SlotCount; i++)
					if (Object.IDIfAssigned == KingdomFoundingHeartRules.SlotId(Plan, i)) slot = i;
				Relevant = slot >= 0 || Object.GetStringProperty(FoundingHeartOwnerProperty)
					== Plan.TransactionId;
				Exact = slot == KingdomFoundingHeartRules.WorksSlot
					&& FoundingHeartTombstoneIdentity(Object, Plan, slot);
				return true;
			}
			catch
			{
				KingdomLog.Log("founding heart: native graveyard classification is unreadable");
				return false;
			}
		}

		private static bool ExactFoundingHeartGraveyardTombstone(KingdomFoundingHeartPlan Plan,
			int Slot, out GameObject Tombstone)
		{
			Tombstone = null;
			string id = KingdomFoundingHeartRules.SlotId(Plan, Slot);
			if (!ExactGraveyardTombstone(id, null, out Tombstone)) return false;
			return !GameObject.Validate(Tombstone)
				&& FoundingHeartTombstoneIdentity(Tombstone, Plan, Slot);
		}

		private static bool ExactFoundingHeartLiveAbsence(string Id)
		{
			if (string.IsNullOrEmpty(Id)) return false;
			List<GameObject> pending = new List<GameObject>();
			HashSet<GameObject> graveyard = new HashSet<GameObject>();
			if (!TryFoundingHeartCustodyRoots(pending, graveyard)) return false;
			HashSet<GameObject> expanded = new HashSet<GameObject>();
			while (pending.Count > 0)
			{
				GameObject item = pending[pending.Count - 1];
				pending.RemoveAt(pending.Count - 1);
				if (item == null || !expanded.Add(item) || graveyard.Contains(item)) continue;
				if (expanded.Count > MaximumFoundingHeartCustodyObjects
					|| item.IDIfAssigned == Id) return false;
				List<GameObject> children;
				try { children = item.GetInventoryDirectAndEquipment(); }
				catch { return false; }
				if (children != null) for (int i = 0; i < children.Count; i++) pending.Add(children[i]);
			}
			return true;
		}
	}
}
