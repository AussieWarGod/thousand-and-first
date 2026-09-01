using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Bounded in-memory custody graph shared by market and lifecycle recovery.
	/// Absence is never terminal proof; callers require their own durable receipt.</summary>
	internal static class KingdomMarketHandoffGlobalIndex
	{
		private const int MaximumObjects = 65536;

		internal static bool TryLoaded(out IList<GameObject> loaded)
		{
			loaded = null;
			if (The.ZoneManager == null || The.Game?.ObjectGameState == null
				|| The.Game.ObjectGameState.Count > MaximumObjects) return false;
			List<GameObject> pending = new List<GameObject>();
			try
			{
				HashSet<Zone> zones = new HashSet<Zone>();
				if (The.ZoneManager.ActiveZone != null) zones.Add(The.ZoneManager.ActiveZone);
				if (The.ZoneManager.CachedZones != null)
					foreach (Zone held in The.ZoneManager.CachedZones.Values)
						if (held != null) zones.Add(held);
				foreach (Zone held in zones)
				{
					List<GameObject> roots = held.GetObjects();
					if (roots == null) return false;
					for (int i = 0; i < roots.Count; i++) pending.Add(roots[i]);
				}
				if (The.ZoneManager.Graveyard?.Objects != null)
					for (int i = 0; i < The.ZoneManager.Graveyard.Objects.Count; i++)
						pending.Add(The.ZoneManager.Graveyard.Objects[i]);
				if (The.Player != null) pending.Add(The.Player);
				foreach (KeyValuePair<string, object> row in The.Game.ObjectGameState)
					if (row.Value is GameObject root) pending.Add(root);
			}
			catch { return false; }
			List<GameObject> result = new List<GameObject>();
			HashSet<GameObject> seen = new HashSet<GameObject>();
			while (pending.Count > 0)
			{
				GameObject candidate = pending[pending.Count - 1];
				pending.RemoveAt(pending.Count - 1);
				if (candidate == null || !seen.Add(candidate)) continue;
				if (seen.Count > MaximumObjects) return false;
				result.Add(candidate);
				List<GameObject> children;
				try { children = candidate.GetInventoryDirectAndEquipment(); }
				catch { return false; }
				if (children != null)
					for (int i = 0; i < children.Count; i++) pending.Add(children[i]);
			}
			loaded = result;
			return true;
		}
	}
}
