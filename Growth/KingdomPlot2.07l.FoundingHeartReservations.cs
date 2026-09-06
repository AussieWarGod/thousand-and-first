using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		public const string FoundingHeartReservationPrefix = KingdomFoundingHeartReservationRules.Prefix;
		// Persisted authority makes receiptless recovery fail closed. ZoneActivated audits every
		// reserved ID after a cold zone becomes observable; allocation in a still-unloaded zone cannot
		// be intercepted by the native API and is therefore deliberately not claimed as preempted.

		private static string FoundingHeartReservation(KingdomFoundingHeartPlan Plan, string Id,
			string Role)
		{
			return KingdomFoundingHeartReservationRules.Encode(Plan, Id, Role);
		}

		private static bool EnsureFoundingHeartReservations(KingdomFoundingHeartPlan Plan)
		{
			FoundingHeartReservationStore store = new FoundingHeartReservationStore();
			if (!store.CheckPlan(Plan, AllowAbsent: true)) return false;
			for (int slot = 0; slot < KingdomFoundingHeartRules.SlotCount; slot++)
				if (!store.Current || !EnsureFoundingHeartReservation(store, Plan,
					KingdomFoundingHeartRules.SlotId(Plan, slot), "slot-" + slot)) return false;
			return store.Current && EnsureFoundingHeartReservation(store, Plan, FoundingHeartFinalId(Plan), "final")
				&& store.CheckPlan(Plan);
		}

		private static bool EnsureFoundingHeartReservation(FoundingHeartReservationStore Store, KingdomFoundingHeartPlan Plan,
			string Id, string Role)
		{
			string key = FoundingHeartReservationPrefix + Id;
			string expected = FoundingHeartReservation(Plan, Id, Role);
			return Store.Ensure(key, expected);
		}

		private static bool ExactFoundingHeartReservations(KingdomFoundingHeartPlan Plan)
		{
			return new FoundingHeartReservationStore().CheckPlan(Plan);
		}

		/// <summary>Audits only the supplied activated zone and already-loaded global custody.
		/// It never asks ZoneManager to load, fetch, or thaw an offscreen zone.</summary>
		internal static bool AuditFoundingHeartReservations(KingdomSystem System, XRL.World.Zone Z)
		{
			FoundingHeartReservationStore store = new FoundingHeartReservationStore();
			if (System == null || Z == null || !store.TryAudit(out Dictionary<string, string> reservations)) return false;
			bool owns = TryFoundingHeartTransaction(System, Z, out string transaction);
			bool reserved = false;
			foreach (KeyValuePair<string, string> row in reservations)
				if (TryReadFoundingHeartReservation(FoundingHeartReservationPrefix + row.Key,
					row.Value, out string owner, out string zone, out _)
					&& owns && owner == transaction && zone == Z.ZoneID) reserved = true;
			List<GameObject> pending;
			try { pending = Z.GetObjects(); }
			catch { return false; }
			if (pending == null) return false;
			pending = new List<GameObject>(pending);
			HashSet<GameObject> expanded = new HashSet<GameObject>();
			try
			{
				while (pending.Count > 0)
				{
					GameObject item = pending[pending.Count - 1]; pending.RemoveAt(pending.Count - 1);
					if (item == null || !expanded.Add(item)) continue;
					if (expanded.Count > MaximumFoundingHeartCustodyObjects
						|| !GameObject.Validate(item)) return false;
					string id = item.IDIfAssigned;
					if (!string.IsNullOrEmpty(id) && reservations.TryGetValue(id, out string raw))
					{
						if (!TryReadFoundingHeartReservation(FoundingHeartReservationPrefix + id,
							raw, out string owner, out string zone, out _)
							|| !owns || owner != transaction || zone != Z.ZoneID)
						{
							KingdomLog.Log("founding heart: activated zone contains a foreign reserved ID");
							return false;
						}
						reserved = true;
					}
					List<GameObject> children = item.GetInventoryDirectAndEquipment();
					if (children != null) for (int i = 0; i < children.Count; i++) pending.Add(children[i]);
				}
			}
			catch { return false; }
			return store.Retains(reservations, AllowAdditional: false)
				&& (!reserved || RecoverFoundingHeart(System, Z))
				&& store.Retains(reservations, AllowAdditional: true);
		}

		private static bool TryReadFoundingHeartReservation(string Key, string Raw,
			out string Transaction, out string ZoneId, out string Id)
		{
			return KingdomFoundingHeartReservationRules.TryRead(Key, Raw, out Transaction, out ZoneId, out Id);
		}
	}
}
