using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		private const int MaximumFoundingHeartCustodyObjects = 65536;

		private static bool NewHeartIdentitiesAreEmpty(KingdomFoundingHeartPlan Plan)
		{
			if (!KingdomFoundingHeartRules.Valid(Plan) || The.Game == null) return false;
			for (int slot = 0; slot < KingdomFoundingHeartRules.SlotCount; slot++)
			{
				string id = KingdomFoundingHeartRules.SlotId(Plan, slot);
				if (FindGlobalFoundingHeartId(id, out _, out _)
					!= KingdomPhysicalLookupState.Absent
					|| The.Game.ObjectGameState.ContainsKey(FoundingHeartRootKey(Plan, slot)))
						return false;
			}
			string final = FoundingHeartFinalId(Plan);
			return FindGlobalFoundingHeartId(final, out _, out _)
				== KingdomPhysicalLookupState.Absent
				&& !The.Game.ObjectGameState.ContainsKey(FoundingHeartFinalRootKey(Plan));
		}

		internal static KingdomPhysicalLookupState FindGlobalFoundingHeartId(string Id,
			out GameObject Exact, out bool Graveyard)
		{
			Exact = null;
			Graveyard = false;
			if (string.IsNullOrEmpty(Id) || The.ZoneManager == null)
				return KingdomPhysicalLookupState.Ambiguous;
			List<GameObject> pending = new List<GameObject>();
			HashSet<GameObject> graveyard = new HashSet<GameObject>();
			if (!TryFoundingHeartCustodyRoots(pending, graveyard))
				return KingdomPhysicalLookupState.Ambiguous;
			HashSet<GameObject> expanded = new HashSet<GameObject>();
			HashSet<GameObject> found = new HashSet<GameObject>();
			while (pending.Count > 0)
			{
				GameObject candidate = pending[pending.Count - 1];
				pending.RemoveAt(pending.Count - 1);
				if (candidate == null || !expanded.Add(candidate)) continue;
				if (expanded.Count > MaximumFoundingHeartCustodyObjects)
					return KingdomPhysicalLookupState.Ambiguous;
				if (graveyard.Contains(candidate)) continue;
				if (candidate.IDIfAssigned == Id)
				{
					// Live identity and retirement authority are intentionally separate. Native
					// Destroy keeps an invalid exact object in the graveyard; tombstone callers
					// classify that reference directly instead of poisoning the live lookup.
					if (!graveyard.Contains(candidate))
					{
						if (!GameObject.Validate(candidate)) return KingdomPhysicalLookupState.Ambiguous;
						found.Add(candidate);
					}
				}
				List<GameObject> children;
				try { children = candidate.GetInventoryDirectAndEquipment(); }
				catch { return KingdomPhysicalLookupState.Ambiguous; }
				if (children != null) for (int i = 0; i < children.Count; i++)
				{
					pending.Add(children[i]);
					if (graveyard.Contains(candidate) && children[i] != null)
						graveyard.Add(children[i]);
				}
			}
			if (found.Count == 0) return KingdomPhysicalLookupState.Absent;
			if (found.Count != 1) return KingdomPhysicalLookupState.Ambiguous;
			foreach (GameObject item in found) Exact = item;
			Graveyard = false;
			return KingdomPhysicalLookupState.Exact;
		}

		private static bool TryFoundingHeartCustodyRoots(List<GameObject> Pending,
			HashSet<GameObject> Graveyard)
		{
			try
			{
				HashSet<Zone> zones = new HashSet<Zone>();
				if (The.ZoneManager.ActiveZone != null) zones.Add(The.ZoneManager.ActiveZone);
				if (The.ZoneManager.CachedZones != null)
					foreach (Zone zone in The.ZoneManager.CachedZones.Values)
						if (zone != null) zones.Add(zone);
				foreach (Zone zone in zones)
				{
					List<GameObject> roots = zone.GetObjects();
					if (roots == null) return false;
					for (int i = 0; i < roots.Count; i++) Pending.Add(roots[i]);
				}
				if (The.ZoneManager.Graveyard?.Objects != null)
					for (int i = 0; i < The.ZoneManager.Graveyard.Objects.Count; i++)
					{
						GameObject item = The.ZoneManager.Graveyard.Objects[i];
						if (item != null) { Pending.Add(item); Graveyard.Add(item); }
					}
				if (The.Player != null) Pending.Add(The.Player);
				if (The.Game?.ObjectGameState == null
					|| The.Game.ObjectGameState.Count > MaximumFoundingHeartCustodyObjects) return false;
				foreach (KeyValuePair<string, object> row in The.Game.ObjectGameState)
					if (row.Value is GameObject item) Pending.Add(item);
				return true;
			}
			catch { return false; }
		}

		private static string FoundingHeartRootKey(KingdomFoundingHeartPlan Plan, int Slot)
		{
			string id = KingdomFoundingHeartRules.SlotId(Plan, Slot);
			return id == null ? null : FoundingHeartRootPrefix + id;
		}

		private static bool RootFoundingHeartOutput(KingdomFoundingHeartPlan Plan,
			int Slot, GameObject Output)
		{
			string key = FoundingHeartRootKey(Plan, Slot);
			if (The.Game == null || key == null || !FoundingHeartIdentity(Output, Plan, Slot))
				return false;
			if (The.Game.ObjectGameState.TryGetValue(key, out object existing)
				&& !ReferenceEquals(existing, Output)) return false;
			try { The.Game.SetObjectGameState(key, Output); }
			catch { return false; }
			return The.Game.ObjectGameState.TryGetValue(key, out existing)
				&& ReferenceEquals(existing, Output)
				&& ExactFoundingHeartObjectGameState(Plan, Slot, Output, true);
		}

		private static bool TryFoundingHeartRoot(KingdomFoundingHeartPlan Plan,
			int Slot, out GameObject Output)
		{
			Output = null;
			string key = FoundingHeartRootKey(Plan, Slot);
			if (The.Game == null || key == null
				|| !The.Game.ObjectGameState.TryGetValue(key, out object rooted)) return false;
			Output = rooted as GameObject;
			return FoundingHeartIdentity(Output, Plan, Slot);
		}

		private static bool FoundingHeartRootAbsent(KingdomFoundingHeartPlan Plan, int Slot)
		{
			string key = FoundingHeartRootKey(Plan, Slot);
			return The.Game != null && key != null && !The.Game.ObjectGameState.ContainsKey(key);
		}

		private static bool RetireFoundingHeartRoot(KingdomFoundingHeartPlan Plan,
			int Slot, GameObject Expected)
		{
			string key = FoundingHeartRootKey(Plan, Slot);
			if (The.Game == null || key == null) return false;
			if (!The.Game.ObjectGameState.TryGetValue(key, out object rooted))
				return ExactFoundingHeartObjectGameState(Plan, Slot, Expected, false);
			if (!ReferenceEquals(rooted, Expected)) return false;
			The.Game.ObjectGameState.Remove(key);
			return !The.Game.ObjectGameState.ContainsKey(key)
				&& ExactFoundingHeartObjectGameState(Plan, Slot, Expected, false);
		}

		private static bool StageFoundingHeartIdentity(GameObject Object,
			KingdomFoundingHeartPlan Plan, int Slot)
		{
			if (!GameObject.Validate(Object)) return false;
			Object.SetStringProperty(FoundingHeartOwnerProperty, Plan.TransactionId);
			Object.SetIntProperty(FoundingHeartSlotProperty, Slot + 1);
			Object.IDIfAssigned = KingdomFoundingHeartRules.SlotId(Plan, Slot);
			return FoundingHeartIdentity(Object, Plan, Slot);
		}

		private static bool FoundingHeartIdentity(GameObject Object,
			KingdomFoundingHeartPlan Plan, int Slot)
		{
			return GameObject.Validate(Object) && KingdomFoundingHeartRules.Valid(Plan)
					&& Slot >= 0 && Slot < KingdomFoundingHeartRules.SlotCount
					&& Object.IDIfAssigned == KingdomFoundingHeartRules.SlotId(Plan, Slot)
					&& Object.HasStringProperty(FoundingHeartOwnerProperty)
					&& !Object.HasIntProperty(FoundingHeartOwnerProperty)
					&& Object.GetStringProperty(FoundingHeartOwnerProperty) == Plan.TransactionId
					&& Object.HasIntProperty(FoundingHeartSlotProperty)
					&& !Object.HasStringProperty(FoundingHeartSlotProperty)
					&& Object.GetIntProperty(FoundingHeartSlotProperty) == Slot + 1;
		}

		private static bool AdvanceFoundingHeart(Zone Z, FoundingHeartContext Context,
			int Slot, int Expected, int Next)
		{
			KingdomFoundingHeartPlan Plan = Context?.Plan;
			string receipt = KingdomFoundingHeartRules.Encode(Plan);
			if (receipt == null || Z?.GetZoneProperty(FoundingHeartReceiptProperty, null)
				!= receipt || Context.Receipt != receipt
				|| !ExactFoundingHeartZoneTruth(Z, Plan)
				|| !ExactFoundingHeartReservations(Plan)
				|| !PreflightFoundingHeartWorld(Z, Context)) return false;
			if (!KingdomFoundingHeartRules.TryAdvance(Plan, Slot, Expected, Next)) return false;
			if (PublishFoundingHeartPlan(Z, receipt, Plan))
			{
				Context.Receipt = KingdomFoundingHeartRules.Encode(Plan);
				return true;
			}
			Plan.States[Slot] = Expected;
			return false;
		}

		private static void FoundingHeartSlotGround(KingdomFoundingHeartPlan Plan, int Slot,
			out int X, out int Y)
		{
			X = Plan.RiteX;
			Y = Plan.RiteY;
			switch (Slot)
			{
				case KingdomFoundingHeartRules.NorthWestStakeSlot:
					X = Plan.SurveyX1; Y = Plan.SurveyY1; break;
				case KingdomFoundingHeartRules.NorthEastStakeSlot:
					X = Plan.SurveyX2; Y = Plan.SurveyY1; break;
				case KingdomFoundingHeartRules.SouthWestStakeSlot:
					X = Plan.SurveyX1; Y = Plan.SurveyY2; break;
				case KingdomFoundingHeartRules.SouthEastStakeSlot:
					X = Plan.SurveyX2; Y = Plan.SurveyY2; break;
			}
		}

		private static string FoundingHeartSlotBlueprint(int Slot)
		{
			return Slot == KingdomFoundingHeartRules.RelicSlot
				? HeartRelicBlueprint : SurveyStakeBlueprint;
		}

		private static string FoundingHeartSlotMark(int Slot)
		{
			return Slot == KingdomFoundingHeartRules.RelicSlot
				? HeartRelicProperty : HeartStakeProperty;
		}
	}
}
