using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Resident-root collection for the locator. Kept apart from Locator.cs, whose
	/// source contract forbids zone-registry access.</summary>
	public static partial class KingdomPolityEndpointRuntime
	{
		private static bool TryCollectResidentRoots(out List<GameObject> Pending,
			out HashSet<GameObject> Excluded, out string Failure)
		{
			Pending = new List<GameObject>(); Excluded = new HashSet<GameObject>(); Failure = null;
			if (The.ZoneManager == null || The.ZoneManager.CachedZones == null ||
				The.Game?.ObjectGameState == null || The.ZoneManager.CachedZones.Count >
				MaximumResidentLookupObjects || The.Game.ObjectGameState.Count >
				MaximumResidentLookupObjects)
				return FailPhysical("resident object authority is unscannable", out Failure);
			HashSet<Zone> zones = new HashSet<Zone>();
			if (The.ZoneManager.ActiveZone != null) zones.Add(The.ZoneManager.ActiveZone);
			foreach (Zone zone in The.ZoneManager.CachedZones.Values)
				if (zone == null) return FailPhysical(
					"resident zone registry contains an ambiguous entry", out Failure);
				else zones.Add(zone);
			foreach (Zone zone in zones)
			{
				List<GameObject> roots = zone.GetObjects();
				if (roots == null || roots.Count > MaximumResidentLookupObjects - Pending.Count)
					return FailPhysical("resident object authority is unscannable", out Failure);
				Pending.AddRange(roots);
			}
			if (The.ZoneManager.Graveyard?.Objects != null)
			{
				if (The.ZoneManager.Graveyard.Objects.Count >
					MaximumResidentLookupObjects - Pending.Count)
					return FailPhysical("resident object authority is unscannable", out Failure);
				for (int i = 0; i < The.ZoneManager.Graveyard.Objects.Count; i++)
				{
					GameObject item = The.ZoneManager.Graveyard.Objects[i];
					if (item != null) { Pending.Add(item); Excluded.Add(item); }
				}
			}
			if (The.Player != null)
			{
				if (Pending.Count == MaximumResidentLookupObjects) return FailPhysical(
					"resident object authority is unscannable", out Failure);
				Pending.Add(The.Player);
			}
			foreach (KeyValuePair<string, object> row in The.Game.ObjectGameState)
				if (row.Value is GameObject item)
				{
					if (Pending.Count == MaximumResidentLookupObjects) return FailPhysical(
						"resident object authority is unscannable", out Failure);
					Pending.Add(item);
				}
			return true;
		}
	}
}
