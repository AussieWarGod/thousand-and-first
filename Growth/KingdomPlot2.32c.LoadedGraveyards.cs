using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		// Destroy sends placed bodies to Zone.Graveyard, detached bodies to the manager's.
		// Inspect only already loaded zones. These bounded, pooled records are evidence while
		// retained; an absent, unloaded or recycled body never establishes destruction.
		private static bool TryLoadedPlotTombstones(out List<GameObject> Tombstones)
		{
			Tombstones = new List<GameObject>();
			var manager = The.ZoneManager;
			if (manager == null) return false;
			try
			{
				HashSet<Zone> zones = new HashSet<Zone>();
				if (manager.ActiveZone != null) zones.Add(manager.ActiveZone);
				if (manager.CachedZones != null)
				{
					if (manager.CachedZones.Count > MaximumFoundingHeartCustodyObjects) return false;
					foreach (Zone zone in manager.CachedZones.Values)
						if (zone != null) zones.Add(zone);
				}
				if (zones.Count > MaximumFoundingHeartCustodyObjects) return false;
				HashSet<Graveyard> graveyards = new HashSet<Graveyard>();
				graveyards.Add(manager.Graveyard);
				foreach (Zone zone in zones) graveyards.Add(zone.Graveyard);
				foreach (Graveyard graveyard in graveyards)
				{
					if (graveyard?.Objects == null || graveyard.Objects.Count
						> MaximumFoundingHeartCustodyObjects - Tombstones.Count) return false;
					for (int i = 0; i < graveyard.Objects.Count; i++)
						Tombstones.Add(graveyard.Objects[i]);
				}
				// Preserve repeated body references: exact-ID callers must reject duplicate entries.
				return ReferenceEquals(manager, The.ZoneManager);
			}
			catch
			{
				KingdomLog.Log("plot removal: loaded graveyards are unreadable");
				return false;
			}
		}
	}
}
