using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		/// <summary>One bounded live-custody walk for a frozen set of recovery subjects.
		/// Reuses the active semantic survey; never thaws zones or mints object identities.</summary>
		internal static bool TryCaptureGlobalLiveIds(ISet<string> ids,
			out Dictionary<string, GameObject> exact)
		{
			exact = null;
			if (ids == null || ids.Count > MaximumFoundingHeartCustodyObjects
				|| ids.Contains(null) || ids.Contains("") || The.ZoneManager == null) return false;
			List<GameObject> pending = new List<GameObject>();
			HashSet<GameObject> graveyard = new HashSet<GameObject>();
			if (!TryFoundingHeartCustodyRoots(pending, graveyard, ReuseSurvey: true)) return false;
			Dictionary<string, GameObject> found = new Dictionary<string, GameObject>(StringComparer.Ordinal);
			HashSet<GameObject> expanded = new HashSet<GameObject>();
			while (pending.Count != 0)
			{
				GameObject item = pending[pending.Count - 1];
				pending.RemoveAt(pending.Count - 1);
				if (item == null || !expanded.Add(item)) continue;
				if (expanded.Count > MaximumFoundingHeartCustodyObjects) return false;
				if (graveyard.Contains(item)) continue;
				string id = item.IDIfAssigned;
				if (id != null && ids.Contains(id))
				{
					if (!GameObject.Validate(item) || found.ContainsKey(id)) return false;
					found.Add(id, item);
				}
				List<GameObject> children;
				try { children = item.GetInventoryDirectAndEquipment(); }
				catch { return false; }
				if (children != null) pending.AddRange(children);
			}
			exact = found;
			return true;
		}
	}
}
