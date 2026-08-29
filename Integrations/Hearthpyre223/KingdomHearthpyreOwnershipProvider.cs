using System;
using Hearthpyre;
using Hearthpyre.Realm;
using ThousandAndFirst.Api;
using XRL.World;

namespace ThousandAndFirst.Integrations.Hearthpyre223
{
	[KingdomExternalOwnershipProvider]
	public sealed class KingdomHearthpyreOwnershipProvider
		: IKingdomExternalOwnershipProvider
	{
		public string ProviderId => "Hearthpyre";
		public string ProviderVersion => "2.2.3";

		public bool TryObserve(Zone ActiveZone,
			out KingdomExternalOwnershipObservation Observation, out string Failure)
		{
			Observation = null;
			Failure = null;
			if (ActiveZone == null)
			{
				Failure = "active zone is missing";
				return false;
			}
			string parasang = ActiveZone.ZoneWorld + "." + ActiveZone.wX + "." + ActiveZone.wY;
			RealmSystem.SettlementsByCellID.TryGetValue(parasang, out Settlement settlement);
			RealmSystem.SectorsByZoneID.TryGetValue(ActiveZone.ZoneID, out Sector sector);
			Sector settlementSector = null;
			if (settlement != null)
				settlement.SectorsByZoneID.TryGetValue(
					ActiveZone.ZoneID, out settlementSector);
			if ((sector == null) != (settlementSector == null)
				|| (sector != null && !ReferenceEquals(sector, settlementSector)))
			{
				Failure = "global and settlement sector registries do not match";
				return false;
			}
			if (sector != null)
			{
				if (sector.ZoneID != ActiveZone.ZoneID || sector.Settlement == null)
				{
					Failure = "sector registry row is incomplete or addresses different ground";
					return false;
				}
				if (settlement == null)
				{
					Failure = "sector registry has no matching parasang settlement row";
					return false;
				}
				if (!ReferenceEquals(settlement, sector.Settlement))
				{
					Failure = "parasang and sector registries name different settlements";
					return false;
				}
			}
			if (settlement == null) return false;
			if (!RealmSystem.Settlements.TryGetValue(settlement.ID, out Settlement byId)
				|| !ReferenceEquals(byId, settlement))
			{
				Failure = "settlement GUID registry does not match its parasang row";
				return false;
			}
			if (sector != null &&
				(!RealmSystem.Sectors.TryGetValue(sector.ID, out Sector sectorById)
					|| !ReferenceEquals(sectorById, sector)))
			{
				Failure = "sector GUID registry does not match its zone row";
				return false;
			}
			Observation = new KingdomExternalOwnershipObservation
			{
				ProviderId = ProviderId,
				ProviderVersion = ProviderVersion,
				OwnerGuid = settlement.ID.ToString("D"),
				SectorGuid = sector?.ID.ToString("D"),
				Evidence = sector == null ? "settlement" : "settlement+sector",
				ZoneId = ActiveZone.ZoneID,
				ParasangId = parasang
			};
			return true;
		}
	}
}
