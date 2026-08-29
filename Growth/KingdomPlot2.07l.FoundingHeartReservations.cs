using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		public const string FoundingHeartReservationPrefix = "r_TAF_FoundingHeartReserved:";
		// Persisted authority makes receiptless recovery fail closed. ZoneActivated audits every
		// reserved ID after a cold zone becomes observable; allocation in a still-unloaded zone cannot
		// be intercepted by the native API and is therefore deliberately not claimed as preempted.

		private static string FoundingHeartReservation(KingdomFoundingHeartPlan Plan, string Id,
			string Role)
		{
			if (!KingdomFoundingHeartRules.Valid(Plan) || string.IsNullOrEmpty(Id)
				|| string.IsNullOrEmpty(Role)) return null;
			return "hr1|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Plan.TransactionId))
				+ "|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Plan.ZoneId)) + "|"
				+ Convert.ToBase64String(Encoding.UTF8.GetBytes(Role)) + "|" + Id + "|"
				+ KingdomFoundingHeartRules.CompletionSeal(CompletedReservationPlan(Plan));
		}

		private static KingdomFoundingHeartPlan CompletedReservationPlan(
			KingdomFoundingHeartPlan Plan)
		{
			KingdomFoundingHeartPlan copy = Plan.Copy();
			for (int i = 0; i < copy.States.Length; i++) copy.States[i] = 2;
			return copy;
		}

		private static bool EnsureFoundingHeartReservations(KingdomFoundingHeartPlan Plan)
		{
			if (The.Game == null || !KingdomFoundingHeartRules.Valid(Plan)) return false;
			for (int slot = 0; slot < KingdomFoundingHeartRules.SlotCount; slot++)
				if (!EnsureFoundingHeartReservation(Plan,
					KingdomFoundingHeartRules.SlotId(Plan, slot), "slot-" + slot)) return false;
			return EnsureFoundingHeartReservation(Plan, FoundingHeartFinalId(Plan), "final");
		}

		private static bool EnsureFoundingHeartReservation(KingdomFoundingHeartPlan Plan,
			string Id, string Role)
		{
			string key = FoundingHeartReservationPrefix + Id;
			string expected = FoundingHeartReservation(Plan, Id, Role);
			string current = The.Game?.GetStringGameState(key, null);
			if (expected == null || !string.IsNullOrEmpty(current) && current != expected) return false;
			if (string.IsNullOrEmpty(current))
			{
				try { The.Game.SetStringGameState(key, expected); }
				catch
				{
					if (The.Game.GetStringGameState(key, null) != expected) return false;
				}
			}
			return The.Game.GetStringGameState(key, null) == expected;
		}

		private static bool ExactFoundingHeartReservations(KingdomFoundingHeartPlan Plan)
		{
			if (The.Game == null || !KingdomFoundingHeartRules.Valid(Plan)) return false;
			for (int slot = 0; slot < KingdomFoundingHeartRules.SlotCount; slot++)
			{
				string id = KingdomFoundingHeartRules.SlotId(Plan, slot);
				if (The.Game.GetStringGameState(FoundingHeartReservationPrefix + id, null)
					!= FoundingHeartReservation(Plan, id, "slot-" + slot)) return false;
			}
			string final = FoundingHeartFinalId(Plan);
			return The.Game.GetStringGameState(FoundingHeartReservationPrefix + final, null)
				== FoundingHeartReservation(Plan, final, "final");
		}

		/// <summary>Audits only the supplied activated zone and already-loaded global custody.
		/// It never asks ZoneManager to load, fetch, or thaw an offscreen zone.</summary>
		internal static bool AuditFoundingHeartReservations(KingdomSystem System, XRL.World.Zone Z)
		{
			if (System == null || Z == null || The.Game?.StringGameState == null
				|| The.Game.StringGameState.Count > MaximumFoundingHeartCustodyObjects) return false;
			Dictionary<string, string> reservations = new Dictionary<string, string>(
				StringComparer.Ordinal);
			try
			{
				foreach (KeyValuePair<string, string> row in The.Game.StringGameState)
				{
					if (!row.Key.StartsWith(FoundingHeartReservationPrefix,
						StringComparison.Ordinal)) continue;
					if (!TryReadFoundingHeartReservation(row.Key, row.Value, out _,
						out _, out string id) || reservations.ContainsKey(id)) return false;
					reservations[id] = row.Value;
				}
			}
			catch { return false; }
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
			return !reserved || RecoverFoundingHeart(System, Z);
		}

		private static bool TryReadFoundingHeartReservation(string Key, string Raw,
			out string Transaction, out string ZoneId, out string Id)
		{
			Transaction = null; ZoneId = null; Id = null;
			if (string.IsNullOrEmpty(Key) || Key.Length > 2048
				|| string.IsNullOrEmpty(Raw) || Raw.Length > 4096) return false;
			string[] p = Raw.Split('|');
			try
			{
				if (p.Length != 6 || p[0] != "hr1") return false;
				Transaction = Encoding.UTF8.GetString(Convert.FromBase64String(p[1]));
				ZoneId = Encoding.UTF8.GetString(Convert.FromBase64String(p[2]));
				string role = Encoding.UTF8.GetString(Convert.FromBase64String(p[3])); Id = p[4];
				if (!KingdomIdentityRules.IsFoundingTransaction(Transaction)
					|| string.IsNullOrEmpty(ZoneId) || string.IsNullOrEmpty(Id)
					|| Key != FoundingHeartReservationPrefix + Id
					|| KingdomFoundingHeartRules.StableId(Transaction, ZoneId, role) != Id
					|| p[5].Length != 64) return false;
				for (int i = 0; i < p[5].Length; i++)
					if (!((p[5][i] >= '0' && p[5][i] <= '9')
						|| (p[5][i] >= 'a' && p[5][i] <= 'f'))) return false;
				return role == "final" || role.StartsWith("slot-", StringComparison.Ordinal)
					&& int.TryParse(role.Substring(5), out int slot) && slot >= 0
					&& slot < KingdomFoundingHeartRules.SlotCount;
			}
			catch { return false; }
		}
	}
}
