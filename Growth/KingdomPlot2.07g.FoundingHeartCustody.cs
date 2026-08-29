using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		private static bool ExactFoundingHeartObjectGameState(KingdomFoundingHeartPlan Plan,
			int Slot, GameObject Expected, bool CanonicalPresent)
		{
			string id = KingdomFoundingHeartRules.SlotId(Plan, Slot);
			string key = FoundingHeartRootKey(Plan, Slot);
			if (The.Game?.ObjectGameState == null || id == null || key == null) return false;
			int matches = 0;
			int visited = 0;
			try
			{
				foreach (KeyValuePair<string, object> row in The.Game.ObjectGameState)
				{
					GameObject root = row.Value as GameObject;
					if (root == null) continue;
					List<GameObject> pending = new List<GameObject> { root };
					HashSet<GameObject> expanded = new HashSet<GameObject>();
					while (pending.Count > 0)
					{
						GameObject item = pending[pending.Count - 1];
						pending.RemoveAt(pending.Count - 1);
						if (item == null || !GameObject.Validate(item)) continue;
						if (++visited > MaximumFoundingHeartCustodyObjects) return false;
						if (item.IDIfAssigned == id)
						{
							matches++;
							if (row.Key != key || !ReferenceEquals(root, Expected)
								|| !ReferenceEquals(item, Expected)) return false;
						}
						if (!expanded.Add(item)) continue;
						List<GameObject> children = item.GetInventoryDirectAndEquipment();
						if (children != null) for (int i = 0; i < children.Count; i++)
							pending.Add(children[i]);
					}
				}
			}
			catch { return false; }
			if (!CanonicalPresent)
				return matches == 0 && !The.Game.ObjectGameState.ContainsKey(key);
			return matches == 1 && The.Game.ObjectGameState.TryGetValue(key, out object exact)
				&& ReferenceEquals(exact, Expected);
		}

		private static int FoundingHeartLoadedReferenceCount(GameObject Expected)
		{
			if (!GameObject.Validate(Expected) || The.ZoneManager == null) return -1;
			List<GameObject> roots = new List<GameObject>();
			try
			{
				HashSet<Zone> zones = new HashSet<Zone>();
				if (The.ZoneManager.ActiveZone != null) zones.Add(The.ZoneManager.ActiveZone);
				if (The.ZoneManager.CachedZones != null)
					foreach (Zone zone in The.ZoneManager.CachedZones.Values)
						if (zone != null) zones.Add(zone);
				foreach (Zone zone in zones)
				{
					List<GameObject> items = zone.GetObjects();
					if (items == null) return -1;
					for (int i = 0; i < items.Count; i++) roots.Add(items[i]);
				}
				bool playerRooted = false;
				for (int i = 0; i < roots.Count; i++)
					if (ReferenceEquals(roots[i], The.Player)) playerRooted = true;
				if (The.Player != null && !playerRooted) roots.Add(The.Player);
				if (The.ZoneManager.Graveyard?.Objects != null)
					for (int i = 0; i < The.ZoneManager.Graveyard.Objects.Count; i++)
						roots.Add(The.ZoneManager.Graveyard.Objects[i]);
				int count = 0;
				int visited = 0;
				for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++)
				{
					List<GameObject> pending = new List<GameObject> { roots[rootIndex] };
					HashSet<GameObject> expanded = new HashSet<GameObject>();
					while (pending.Count > 0)
					{
						GameObject item = pending[pending.Count - 1];
						pending.RemoveAt(pending.Count - 1);
						if (item == null) continue;
						if (++visited > MaximumFoundingHeartCustodyObjects) return -1;
						if (ReferenceEquals(item, Expected)) count++;
						if (!expanded.Add(item)) continue;
						List<GameObject> children = item.GetInventoryDirectAndEquipment();
						if (children != null) for (int i = 0; i < children.Count; i++)
							pending.Add(children[i]);
					}
				}
				return count;
			}
			catch { return -1; }
		}

		private static bool ExactFoundingHeartOwnedRoster(KingdomFoundingHeartPlan Plan)
		{
			if (!KingdomFoundingHeartRules.Valid(Plan)) return false;
			List<GameObject> pending = new List<GameObject>();
			HashSet<GameObject> graveyard = new HashSet<GameObject>();
			if (!TryFoundingHeartCustodyRoots(pending, graveyard)) return false;
			HashSet<GameObject> expanded = new HashSet<GameObject>();
			try
			{
				while (pending.Count > 0)
				{
					GameObject item = pending[pending.Count - 1];
					pending.RemoveAt(pending.Count - 1);
					if (item == null || !expanded.Add(item)) continue;
					if (expanded.Count > MaximumFoundingHeartCustodyObjects) return false;
					if (graveyard.Contains(item) && !GameObject.Validate(item))
					{
						if (!TryClassifyFoundingHeartTombstone(item, Plan,
							out bool relevant, out bool exact) || relevant && !exact) return false;
						continue;
					}
					int expectedSlot = -1;
					for (int slot = 0; slot < KingdomFoundingHeartRules.SlotCount; slot++)
						if (item.IDIfAssigned == KingdomFoundingHeartRules.SlotId(Plan, slot))
							expectedSlot = slot;
					bool owned = item.GetStringProperty(FoundingHeartOwnerProperty)
						== Plan.TransactionId;
					bool tombstone = false;
					if ((owned || expectedSlot >= 0) && (expectedSlot < 0
						|| !tombstone && !FoundingHeartIdentity(item, Plan, expectedSlot))) return false;
					if (tombstone) continue;
					List<GameObject> children = item.GetInventoryDirectAndEquipment();
					if (children != null) for (int i = 0; i < children.Count; i++)
						pending.Add(children[i]);
				}
				return true;
			}
			catch { return false; }
		}

		private static bool ExactFoundingHeartFinalCustody(KingdomFoundingHeartPlan Plan)
		{
			if (!ExactFoundingHeartOwnedRoster(Plan)) return false;
			for (int slot = 0; slot < KingdomFoundingHeartRules.SlotCount; slot++)
			{
				if (FindGlobalFoundingHeartId(KingdomFoundingHeartRules.SlotId(Plan, slot),
					out GameObject exact, out bool graveyard) != KingdomPhysicalLookupState.Exact
					|| graveyard || FoundingHeartLoadedReferenceCount(exact) != 1
					|| !ExactFoundingHeartObjectGameState(Plan, slot, exact, false)) return false;
			}
			return true;
		}

		private static bool ExactFoundingHeartRetiredCustody(KingdomFoundingHeartPlan Plan)
		{
			if (!ExactFoundingHeartOwnedRoster(Plan)) return false;
			for (int slot = 0; slot < KingdomFoundingHeartRules.WorksSlot; slot++)
			{
				if (FindGlobalFoundingHeartId(KingdomFoundingHeartRules.SlotId(Plan, slot),
					out GameObject exact, out bool graveyard) != KingdomPhysicalLookupState.Exact
					|| graveyard || FoundingHeartLoadedReferenceCount(exact) != 1
					|| !ExactFoundingHeartObjectGameState(Plan, slot, exact, false)) return false;
			}
			int works = KingdomFoundingHeartRules.WorksSlot;
			string worksId = KingdomFoundingHeartRules.SlotId(Plan, works);
			return ExactFoundingHeartLiveAbsence(worksId)
				&& ExactFoundingHeartGraveyardTombstone(Plan, works, out _)
				&& ExactFoundingHeartObjectGameState(Plan, works, null, false);
		}

		private static bool HasGlobalFoundingHeartTransactionEvidence(string Transaction,
			string ZoneId)
		{
			if (!KingdomIdentityRules.IsFoundingTransaction(Transaction)
				|| string.IsNullOrEmpty(ZoneId)) return true;
			for (int slot = 0; slot < KingdomFoundingHeartRules.SlotCount; slot++)
			{
				string id = KingdomFoundingHeartRules.StableId(Transaction, ZoneId, "slot-" + slot);
				if (FindGlobalFoundingHeartId(id, out _, out _)
					!= KingdomPhysicalLookupState.Absent
					|| The.Game?.ObjectGameState.ContainsKey(FoundingHeartRootPrefix + id) == true
					|| The.Game?.HasStringGameState(FoundingHeartReservationPrefix + id) == true)
					return true;
			}
			string final = KingdomFoundingHeartRules.StableId(Transaction, ZoneId, "final");
			if (The.Game?.HasStringGameState(FoundingHeartReservationPrefix + final) == true
				|| The.Game?.ObjectGameState.ContainsKey(FoundingHeartFinalRootPrefix + final) == true
				|| FindGlobalFoundingHeartId(final, out _, out _)
					!= KingdomPhysicalLookupState.Absent) return true;
			return HasGlobalFoundingHeartOwner(Transaction);
		}

		private static bool HasFoundingHeartEvidenceInZone(Zone Z)
		{
			if (Z == null) return true;
			List<GameObject> pending;
			HashSet<GameObject> graveyard = new HashSet<GameObject>();
			try
			{
				pending = Z.GetObjects();
				if (The.ZoneManager?.Graveyard?.Objects != null)
				{
					if (The.ZoneManager.Graveyard.Objects.Count
						> MaximumFoundingHeartCustodyObjects) return true;
					for (int i = 0; i < The.ZoneManager.Graveyard.Objects.Count; i++)
						if (The.ZoneManager.Graveyard.Objects[i] != null)
							graveyard.Add(The.ZoneManager.Graveyard.Objects[i]);
				}
			}
			catch { return true; }
			if (pending == null) return true;
			pending = new List<GameObject>(pending);
			HashSet<GameObject> expanded = new HashSet<GameObject>();
			try
			{
				while (pending.Count > 0)
				{
					GameObject item = pending[pending.Count - 1];
					pending.RemoveAt(pending.Count - 1);
					if (item == null || !expanded.Add(item)) continue;
					if (expanded.Count > MaximumFoundingHeartCustodyObjects) return true;
					if (graveyard.Contains(item) && !GameObject.Validate(item)) continue;
					if (item.HasStringProperty(FoundingHeartOwnerProperty)
						|| item.HasIntProperty(FoundingHeartOwnerProperty)
						|| item.HasStringProperty(FoundingHeartSlotProperty)
						|| item.HasIntProperty(FoundingHeartSlotProperty)
						|| item.HasStringProperty(HeartRelicProperty)
						|| item.HasIntProperty(HeartRelicProperty)
						|| item.HasStringProperty(HeartStakeProperty)
						|| item.HasIntProperty(HeartStakeProperty)
						|| item.HasStringProperty(HeartPlotProperty)
						|| item.HasIntProperty(HeartPlotProperty)
						|| item.GetPart<XRL.World.Parts.r_KingdomPlotWorks>()?.DesignKey
							== "heartbasin") return true;
					List<GameObject> children = item.GetInventoryDirectAndEquipment();
					if (children != null) for (int i = 0; i < children.Count; i++)
						pending.Add(children[i]);
				}
				return false;
			}
			catch { return true; }
		}

		private static bool HasGlobalFoundingHeartOwner(string Transaction)
		{
			List<GameObject> pending = new List<GameObject>();
			HashSet<GameObject> graveyard = new HashSet<GameObject>();
			if (!TryFoundingHeartCustodyRoots(pending, graveyard)) return true;
			HashSet<GameObject> expanded = new HashSet<GameObject>();
			try
			{
				while (pending.Count > 0)
				{
					GameObject item = pending[pending.Count - 1];
					pending.RemoveAt(pending.Count - 1);
					if (item == null || !expanded.Add(item)) continue;
					if (expanded.Count > MaximumFoundingHeartCustodyObjects) return true;
					if (item.GetStringProperty(FoundingHeartOwnerProperty) == Transaction) return true;
					List<GameObject> children = item.GetInventoryDirectAndEquipment();
					if (children != null) for (int i = 0; i < children.Count; i++)
						pending.Add(children[i]);
				}
				return false;
			}
			catch { return true; }
		}
	}
}
