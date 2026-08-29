using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
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

		private static bool TryRewriteLegacyDeathIntent(Zone Zone, string LegacyWire,
			KingdomPolityDeathIntentRecord Intent, out string Failure)
		{
			Failure = null;
			if (!KingdomPolityDeathIntentRules.TryEncode(Intent, out string current, out Failure))
				return false;
			string key = KingdomPolityPhysicalCustodyRules.DeathIntentKey(Intent.ProjectionId,
				Intent.ObjectId);
			if (!TryReadExactDeathIntentSlot(Zone, key, out bool present, out bool exact,
				out string actual, out Failure) || !present || !exact || actual != LegacyWire)
				return FailPhysical("legacy death intent changed before migration", out Failure);
			try { Zone.SetZoneProperty(key, current); }
			catch (Exception ex)
			{
				bool read = TryReadExactDeathIntentSlot(Zone, key, out present, out exact,
					out actual, out string inspectFailure);
				KingdomPolityLegacyRewriteRecovery recovery =
					KingdomPolityPhysicalCustodyRules.ClassifyLegacyRewriteRecovery(read,
						present, exact, actual == current, actual == LegacyWire);
				if (recovery == KingdomPolityLegacyRewriteRecovery.Applied)
					{ Failure = null; return true; }
				if (recovery == KingdomPolityLegacyRewriteRecovery.OldBytesPreserved)
					return FailPhysical("legacy death intent migration failed before write: " +
						ex.Message, out Failure);
				return FailPhysical(inspectFailure ??
					"legacy death intent migration left ambiguous bytes", out Failure);
			}
			if (TryReadExactDeathIntentSlot(Zone, key, out present, out exact, out actual,
				out Failure) && present && exact && actual == current) return true;
			return FailPhysical(Failure ??
				"legacy death intent migration did not install exact current bytes", out Failure);
		}
	}
}
