using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Observes only the player's exact currently loaded owned settlement.</summary>
	internal static class KingdomPolityLoadedEndpointRuntime
	{
		internal static bool TryObserve(KingdomSystem System, out Zone Zone,
			out string SettlementId, out bool Available, out string Failure)
		{
			Zone = The.Player?.CurrentZone; SettlementId = null; Available = false;
			Failure = null;
			if (System == null || !System.Founded || System.City == null)
			{
				Failure = "loaded polity endpoint has no current realm authority"; return false;
			}
			if (Zone == null || The.Player?.CurrentCell == null) return true;
			if (!ReferenceEquals(The.Player.CurrentCell.ParentZone, Zone))
			{
				Failure = "player cell and loaded polity zone disagree"; return false;
			}
			if (!System.TryExactSettlementIds(RequirePublishedClaims: true,
				out List<string> exact, out Failure)) return false;
			List<KingdomSettlement> rows = System.NonSeatSettlements();
			List<string> ids = new List<string>();
			List<IList<string>> claims = new List<IList<string>>();
			for (int i = 0; i < rows.Count; i++)
			{
				ids.Add(rows[i]?.City?.SettlementId);
				claims.Add(rows[i]?.ClaimedZones);
			}
			if (!KingdomPolityLoadedEndpointRules.TryResolve(Zone.ZoneID,
				System.City.SettlementId, System.ClaimedZones, ids, claims,
				out SettlementId, out bool owned, out Failure)) return false;
			if (!owned) return true;
			if (!exact.Contains(SettlementId) ||
				!string.Equals(System.SettlementIdForOwnedZone(Zone.ZoneID), SettlementId,
					StringComparison.Ordinal) || !KingdomWord.StandsIn(Zone))
			{
				SettlementId = null;
				Failure = "loaded polity endpoint cannot be reproved from exact ownership";
				return false;
			}
			Available = true; return true;
		}
	}
}
