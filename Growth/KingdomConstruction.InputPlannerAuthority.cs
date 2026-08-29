using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		private sealed class RoutedInputZone
		{
			internal string SettlementId;
			internal string ZoneId;
			internal int DailyWaterUpkeep;
			internal KingdomConstructionInputZoneObservation Observation;
		}

		private static bool TryInputLeases(out KingdomConstructionInputLeaseSet leases,
			out string failure)
		{
			leases = null;
			KingdomConstructionInputLeaseSnapshot snapshot;
			if (!KingdomConstructionInputLeaseAuthority.TryCapture(
				out snapshot, out failure)) return false;
			leases = snapshot.Physical;
			return true;
		}

		private static bool TryInputZones(KingdomSystem system,
			out List<RoutedInputZone> zones, out string failure)
		{
			zones = null;
			failure = null;
			List<string> exact;
			if (system == null
				|| !system.TryExactSettlementIds(true, out exact, out failure)
				|| system.City == null || string.IsNullOrEmpty(system.City.SettlementId))
			{
				failure = failure ?? "The realm's exact settlement grounds are unavailable.";
				return false;
			}
			List<RoutedInputZone> found = new List<RoutedInputZone>();
			HashSet<string> seenZones = new HashSet<string>(StringComparer.Ordinal);
			int seatDaily = KingdomRules.PolicyUpkeep(
				KingdomRules.UpkeepDrams(system.Population, system.Stage), system.Stores);
			if (!AddInputZones(system.ClaimedZones, system.City.SettlementId,
				seatDaily, exact, seenZones, found, out failure)) return false;
			List<KingdomSettlement> nonSeat = system.NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++)
			{
				KingdomSettlement away = nonSeat[i];
				int awayDaily = KingdomRules.PolicyUpkeep(
					KingdomRules.UpkeepDrams(away.Population, away.Stage), away.Stores);
				if (away.City == null || !AddInputZones(away.ClaimedZones,
					away.City.SettlementId, awayDaily, exact, seenZones, found,
					out failure)) return false;
			}
			found.Sort(delegate(RoutedInputZone left, RoutedInputZone right)
			{
				int compare = string.CompareOrdinal(left.SettlementId, right.SettlementId);
				return compare != 0 ? compare : string.CompareOrdinal(left.ZoneId, right.ZoneId);
			});
			List<KingdomConstructionInputZoneObservation> observations;
			if (!TryReadInputObservations(system, out observations, out failure)) return false;
			List<RoutedInputZone> attended = new List<RoutedInputZone>();
			for (int i = 0; i < found.Count; i++)
				for (int j = 0; j < observations.Count; j++)
					if (found[i].ZoneId == observations[j].ZoneId
						&& found[i].SettlementId == observations[j].SettlementId)
					{
						if (found[i].Observation != null)
						{
							failure = "Durable source observation identity is ambiguous.";
							return false;
						}
						found[i].Observation = observations[j];
					}
			for (int i = 0; i < found.Count; i++)
				if (found[i].Observation != null) attended.Add(found[i]);
			zones = attended;
			return true;
		}

		private static bool AddInputZones(IList<string> claimed, string settlementId,
			int dailyWater, IList<string> exactSettlements, HashSet<string> seen,
			List<RoutedInputZone> into, out string failure)
		{
			failure = null;
			if (claimed == null || string.IsNullOrEmpty(settlementId)
				|| !exactSettlements.Contains(settlementId) || dailyWater < 0)
			{
				failure = "A settlement source authority is incomplete.";
				return false;
			}
			for (int i = 0; i < claimed.Count; i++)
			{
				string zoneId = claimed[i];
				if (string.IsNullOrEmpty(zoneId) || !seen.Add(zoneId))
				{
					failure = "Claimed source grounds overlap or lack identity.";
					return false;
				}
				into.Add(new RoutedInputZone { SettlementId = settlementId,
					ZoneId = zoneId, DailyWaterUpkeep = dailyWater });
			}
			return true;
		}
	}
}
