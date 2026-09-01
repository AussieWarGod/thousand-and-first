using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Hearthpyre;
using Hearthpyre.Realm;
using XRL.World;

namespace ThousandAndFirst.Integrations.Hearthpyre223
{
	/// <summary>Read-only, bounded one-to-one proof for Hearthpyre's public reverse registries.</summary>
	internal static class KingdomHearthpyreFootprintCustody
	{
		internal static bool TryResolveSector(Zone ActiveZone,
			KingdomHearthpyreFootprintScanBudget Budget, out Sector Sector,
			out bool Absent, out string Failure)
		{
			Sector = null; Absent = false; Failure = null;
			if (Budget == null)
				return Fail(KingdomHearthpyreFootprintScanBudget.LimitFailure, out Failure);
			if (ActiveZone == null || string.IsNullOrEmpty(ActiveZone.ZoneID))
				return Fail("active zone identity is absent", out Failure);
			if (!Bounded(RealmSystem.Settlements) || !Bounded(RealmSystem.SettlementsByCellID)
				|| !Bounded(RealmSystem.Sectors) || !Bounded(RealmSystem.SectorsByZoneID)
				|| !Bounded(RealmSystem.Homes))
				return Fail("Hearthpyre reverse registry is absent or over-bound", out Failure);
			if (!RealmSystem.SectorsByZoneID.TryGetValue(ActiveZone.ZoneID, out Sector sector))
			{
				if (!ProveSectorAbsent(ActiveZone.ZoneID, Budget, out Failure)) return false;
				Absent = true; return true;
			}
			if (!ExactSector(ActiveZone, sector, Budget, out Failure)) return false;
			Sector = sector; return true;
		}

		internal static bool TryProveHome(Sector Sector, Home Home,
			KingdomHearthpyreFootprintScanBudget Budget, out string Failure)
		{
			Failure = null;
			if (Sector == null || Sector.Homes == null
				|| Sector.Homes.Count > KingdomDesignationRules.MaxDesignationsPerZone
				|| Home == null || Home.ID == Guid.Empty || !ReferenceEquals(Home.Sector, Sector)
				|| !RealmSystem.Homes.TryGetValue(Home.ID, out Home byId)
				|| !ReferenceEquals(byId, Home))
				return Fail("Home registry or sector backlink is not one-to-one", out Failure);
			if (!TryCountValue(RealmSystem.Homes, Home, Budget,
				out int global, out Failure)
				|| !TryCountReference(Sector.Homes, Home, Budget,
					out int local, out Failure)) return false;
			if (global != 1 || local != 1)
				return Fail("Home registry or sector backlink is not one-to-one", out Failure);
			return true;
		}

		/// <summary>Freezes the sector's declared roster plus any globally indexed Home whose
		/// backlink names this sector. Static disagreement is per-Home evidence; only an absent or
		/// over-bound roster prevents a coherent provider snapshot.</summary>
		internal static bool TrySnapshotHomes(Sector Sector,
			KingdomHearthpyreFootprintScanBudget Budget, out Home[] Homes,
			out string Failure)
		{
			Homes = null; Failure = null;
			if (Sector?.Homes == null
				|| Sector.Homes.Count > KingdomDesignationRules.MaxDesignationsPerZone
				|| !Bounded(RealmSystem.Homes))
				return Fail("sector Home roster is absent or over-bound", out Failure);
			if (!TryCharge(Budget, Sector.Homes.Count, out Failure)
				|| !TryCharge(Budget, RealmSystem.Homes.Count, out Failure)) return false;
			List<Home> rows = new List<Home>(Sector.Homes);
			HashSet<Home> members = new HashSet<Home>(HomeReferenceComparer.Instance);
			for (int i = 0; i < rows.Count; i++) members.Add(rows[i]);
			Dictionary<Home, Guid> indexed =
				new Dictionary<Home, Guid>(HomeReferenceComparer.Instance);
			foreach (KeyValuePair<Guid, Home> pair in RealmSystem.Homes)
				if (ReferenceEquals(pair.Value?.Sector, Sector)
					&& !members.Contains(pair.Value)
					&& (!indexed.TryGetValue(pair.Value, out Guid first)
						|| pair.Key.CompareTo(first) < 0))
					indexed[pair.Value] = pair.Key;
			if (rows.Count + indexed.Count > KingdomDesignationRules.MaxDesignationsPerZone)
				return Fail("sector Home union exceeds its bounded roster", out Failure);
			List<KeyValuePair<Guid, Home>> extras =
				new List<KeyValuePair<Guid, Home>>(indexed.Count);
			foreach (KeyValuePair<Home, Guid> pair in indexed)
				extras.Add(new KeyValuePair<Guid, Home>(pair.Value, pair.Key));
			extras.Sort((a, b) => a.Key.CompareTo(b.Key));
			for (int i = 0; i < extras.Count; i++)
			{
				if (!members.Add(extras[i].Value)) continue;
				rows.Add(extras[i].Value);
			}
			Homes = rows.ToArray(); return true;
		}

		private static bool ExactSector(Zone ActiveZone, Sector Sector,
			KingdomHearthpyreFootprintScanBudget Budget, out string Failure)
		{
			Failure = null;
			if (Sector == null || Sector.ID == Guid.Empty || Sector.ZoneID != ActiveZone.ZoneID
				|| !RealmSystem.Sectors.TryGetValue(Sector.ID, out Sector byId)
				|| !ReferenceEquals(byId, Sector)
				|| !RealmSystem.SectorsByZoneID.TryGetValue(ActiveZone.ZoneID,
					out Sector byZone) || !ReferenceEquals(byZone, Sector))
				return Fail("sector GUID and zone registries are not one-to-one", out Failure);
			if (!TryCountValue(RealmSystem.Sectors, Sector, Budget,
				out int sectorIds, out Failure)
				|| !TryCountValue(RealmSystem.SectorsByZoneID, Sector, Budget,
					out int sectorZones, out Failure)) return false;
			if (sectorIds != 1 || sectorZones != 1)
				return Fail("sector GUID and zone registries are not one-to-one", out Failure);
			Settlement settlement = Sector.Settlement;
			if (settlement == null || settlement.ID == Guid.Empty
				|| !RealmSystem.Settlements.TryGetValue(settlement.ID, out Settlement bySettlementId)
				|| !ReferenceEquals(bySettlementId, settlement)
				|| settlement.SectorsByZoneID == null
				|| settlement.SectorsByZoneID.Count
					> KingdomHearthpyreFootprintScanBudget.MaxRegistryEntries
				|| !settlement.SectorsByZoneID.TryGetValue(ActiveZone.ZoneID,
					out Sector localSector) || !ReferenceEquals(localSector, Sector))
				return Fail("sector settlement registry is not one-to-one", out Failure);
			if (!TryCountValue(RealmSystem.Settlements, settlement, Budget,
				out int settlementIds, out Failure)
				|| !TryCountValue(settlement.SectorsByZoneID, Sector, Budget,
					out int localSectors, out Failure)
				|| !NoOtherSettlementClaims(ActiveZone.ZoneID, settlement,
					Budget, out Failure)) return false;
			if (settlementIds != 1 || localSectors != 1)
				return Fail("sector settlement registry is not one-to-one", out Failure);
			string cellId = ActiveZone.ZoneWorld + "."
				+ ActiveZone.wX.ToString(CultureInfo.InvariantCulture) + "."
				+ ActiveZone.wY.ToString(CultureInfo.InvariantCulture);
			if (!RealmSystem.SettlementsByCellID.TryGetValue(cellId, out Settlement byCell)
				|| !ReferenceEquals(byCell, settlement))
				return Fail("settlement and active world cell are not one-to-one", out Failure);
			if (!TryCountValue(RealmSystem.SettlementsByCellID, settlement, Budget,
				out int settlementCells, out Failure)) return false;
			if (settlementCells != 1)
				return Fail("settlement and active world cell are not one-to-one", out Failure);
			if (Sector.Homes == null
				|| Sector.Homes.Count > KingdomDesignationRules.MaxDesignationsPerZone)
				return Fail("sector Home roster is absent or over-bound", out Failure);
			return true;
		}

		private static bool ProveSectorAbsent(string ZoneId,
			KingdomHearthpyreFootprintScanBudget Budget, out string Failure)
		{
			Failure = null;
			if (!TryCharge(Budget, RealmSystem.Sectors.Count, out Failure)) return false;
			foreach (KeyValuePair<Guid, Sector> pair in RealmSystem.Sectors)
				if (pair.Value != null && pair.Value.ZoneID == ZoneId)
					return Fail("zone sector exists outside its canonical registry", out Failure);
			if (!TryCharge(Budget, RealmSystem.SectorsByZoneID.Count, out Failure)) return false;
			foreach (KeyValuePair<string, Sector> pair in RealmSystem.SectorsByZoneID)
				if ((pair.Key == ZoneId || pair.Value?.ZoneID == ZoneId))
					return Fail("zone sector registry contains inconsistent evidence", out Failure);
			if (!TryAnySettlementClaims(RealmSystem.Settlements, ZoneId,
				Budget, out Failure)
				|| !TryAnySettlementClaims(RealmSystem.SettlementsByCellID, ZoneId,
					Budget, out Failure)) return false;
			if (!TryCharge(Budget, RealmSystem.Homes.Count, out Failure)) return false;
			foreach (KeyValuePair<Guid, Home> pair in RealmSystem.Homes)
				if (pair.Value?.Sector?.ZoneID == ZoneId)
					return Fail("Home retains an unindexed zone sector", out Failure);
			return true;
		}

		private static bool TryAnySettlementClaims<TKey>(Dictionary<TKey, Settlement> Rows,
			string ZoneId, KingdomHearthpyreFootprintScanBudget Budget, out string Failure)
		{
			Failure = null;
			if (!TryCharge(Budget, Rows?.Count ?? -1, out Failure)) return false;
			foreach (KeyValuePair<TKey, Settlement> pair in Rows)
			{
				if (!TryClaimsZone(pair.Value, ZoneId, Budget,
					out bool claims, out Failure)) return false;
				if (claims) return Fail("settlement retains an unindexed zone sector", out Failure);
			}
			return true;
		}

		private static bool TryClaimsZone(Settlement Settlement, string ZoneId,
			KingdomHearthpyreFootprintScanBudget Budget,
			out bool Claims, out string Failure)
		{
			Claims = false; Failure = null;
			Dictionary<string, Sector> sectors = Settlement?.SectorsByZoneID;
			if (sectors == null) return true;
			if (!TryCharge(Budget, sectors.Count, out Failure)) return false;
			foreach (KeyValuePair<string, Sector> pair in sectors)
				if (pair.Key == ZoneId || pair.Value?.ZoneID == ZoneId)
				{ Claims = true; return true; }
			return true;
		}

		private static bool NoOtherSettlementClaims(string ZoneId, Settlement Expected,
			KingdomHearthpyreFootprintScanBudget Budget, out string Failure)
		{
			Failure = null;
			if (!TryCharge(Budget, RealmSystem.Settlements.Count, out Failure)) return false;
			foreach (KeyValuePair<Guid, Settlement> pair in RealmSystem.Settlements)
				if (!ReferenceEquals(pair.Value, Expected))
				{
					if (!TryClaimsZone(pair.Value, ZoneId, Budget,
						out bool claims, out Failure)) return false;
					if (claims) return Fail("another settlement claims the active zone", out Failure);
				}
			if (!TryCharge(Budget, RealmSystem.SettlementsByCellID.Count, out Failure)) return false;
			foreach (KeyValuePair<string, Settlement> pair in RealmSystem.SettlementsByCellID)
				if (!ReferenceEquals(pair.Value, Expected))
				{
					if (!TryClaimsZone(pair.Value, ZoneId, Budget,
						out bool claims, out Failure)) return false;
					if (claims) return Fail("another settlement claims the active zone", out Failure);
				}
			return true;
		}

		private sealed class HomeReferenceComparer : IEqualityComparer<Home>
		{
			internal static readonly HomeReferenceComparer Instance =
				new HomeReferenceComparer();
			public bool Equals(Home A, Home B) => ReferenceEquals(A, B);
			public int GetHashCode(Home Value) => Value == null
				? 0 : RuntimeHelpers.GetHashCode(Value);
		}

		private static bool TryCountReference(List<Home> Rows, Home Wanted,
			KingdomHearthpyreFootprintScanBudget Budget,
			out int Count, out string Failure)
		{
			Count = 0; Failure = null;
			if (Rows == null || Rows.Count > KingdomDesignationRules.MaxDesignationsPerZone)
				return Fail("Home registry or sector backlink is not one-to-one", out Failure);
			if (!TryCharge(Budget, Rows.Count, out Failure)) return false;
			for (int i = 0; i < Rows.Count; i++)
				if (ReferenceEquals(Rows[i], Wanted)) Count++;
			return true;
		}

		private static bool TryCountValue<TKey, TValue>(Dictionary<TKey, TValue> Rows,
			TValue Wanted, KingdomHearthpyreFootprintScanBudget Budget,
			out int Count, out string Failure)
			where TValue : class
		{
			Count = 0; Failure = null;
			if (!Bounded(Rows))
				return Fail("Hearthpyre reverse registry is absent or over-bound", out Failure);
			if (!TryCharge(Budget, Rows.Count, out Failure)) return false;
			foreach (KeyValuePair<TKey, TValue> pair in Rows)
				if (ReferenceEquals(pair.Value, Wanted)) Count++;
			return true;
		}

		private static bool Bounded<TKey, TValue>(Dictionary<TKey, TValue> Rows)
		{
			return Rows != null && Rows.Count
				<= KingdomHearthpyreFootprintScanBudget.MaxRegistryEntries;
		}

		private static bool TryCharge(KingdomHearthpyreFootprintScanBudget Budget,
			int Entries, out string Failure)
		{
			Failure = null;
			return Budget != null && Budget.TryCharge(Entries)
				|| Fail(KingdomHearthpyreFootprintScanBudget.LimitFailure, out Failure);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
