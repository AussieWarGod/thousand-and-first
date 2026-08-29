using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRemovalRuntime
	{
		public static bool TryDescribeRealmRemovalBlocker(KingdomSystem System,
			out string Blocker, out string Failure)
		{
			Blocker = null;
			Failure = null;
			if (System == null)
			{
				Failure = "kingdom system is absent";
				return false;
			}
			return TryDescribeRealmRemovalBlocker(System,
				Math.Max(0L, The.Game?.TimeTicks ?? 0L),
				out List<KingdomExperienceRetirementLeaseAllowance> _, out Blocker, out Failure);
		}

		internal static bool TryDescribeRealmRemovalBlocker(KingdomSystem System, long Tick,
			out List<KingdomExperienceRetirementLeaseAllowance> Allowances,
			out string Blocker, out string Failure)
		{
			Allowances = new List<KingdomExperienceRetirementLeaseAllowance>();
			Blocker = null; Failure = null;
			if (System == null || !TryGroundLocators(System,
				out List<KingdomPolityRetirementGroundLocator> locators, out Failure)) return false;
			if (!KingdomPolityRemovalRules.TryDescribeRealmRemovalBlocker(System.PolityLedger,
				System.PolityDispatch, System.PolityTransition, locators,
				out Blocker, out Failure) || Blocker != null) return Failure == null;
			return KingdomPolityExperienceRuntime.TryBuildRetirementAllowances(System, Tick,
				out Allowances, out Blocker, out Failure);
		}

		private static bool TryGroundLocators(KingdomSystem System,
			out List<KingdomPolityRetirementGroundLocator> Locators, out string Failure)
		{
			Locators = new List<KingdomPolityRetirementGroundLocator>(); Failure = null;
			if (System?.City == null || !System.TryExactSettlementIds(true,
				out List<string> exact, out Failure)) return false;
			Dictionary<string, string> rows = new Dictionary<string, string>(
				StringComparer.Ordinal);
			if (!AddGround(rows, System.City.SettlementId, System.ClaimedZones, out Failure)) return false;
			List<KingdomSettlement> settlements = System.NonSeatSettlements();
			for (int i = 0; i < settlements.Count; i++)
				if (!AddGround(rows, settlements[i]?.City?.SettlementId,
					settlements[i]?.ClaimedZones, out Failure)) return false;
			foreach (KeyValuePair<string, string> row in rows)
			{
				if (!exact.Contains(row.Value) || System.SettlementIdForOwnedZone(row.Key) != row.Value)
				{
					Failure = "polity retirement ground ownership is ambiguous at " + row.Key; return false;
				}
				Locators.Add(new KingdomPolityRetirementGroundLocator
					{ ZoneId = row.Key, SettlementId = row.Value });
			}
			Locators.Sort((a, b) => string.CompareOrdinal(a.ZoneId, b.ZoneId)); return true;
		}

		private static bool AddGround(Dictionary<string, string> Rows, string SettlementId,
			IList<string> Zones, out string Failure)
		{
			Failure = null;
			if (!KingdomPolityRules.TypedId(SettlementId, "taf:settlement:v1:") || Zones == null)
			{
				Failure = "polity retirement settlement ground is incomplete"; return false;
			}
			for (int i = 0; i < Zones.Count; i++)
			{
				string zoneId = Zones[i];
				if (!KingdomPolityRules.Text(zoneId, true))
				{
					Failure = "polity retirement settlement has an invalid claimed-zone locator";
					return false;
				}
				if (Rows.TryGetValue(zoneId, out string owner) && owner != SettlementId)
				{
					Failure = "polity retirement ground has two settlement owners"; return false;
				}
				Rows[zoneId] = SettlementId;
			}
			return true;
		}
	}
}
