using System;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomAssentingMoot
	{
		/// <summary>Reads one loaded moot and its native report without normalizing its book.</summary>
		internal static KingdomJointCivicOwnerView ReadOwnerForJointView(
			KingdomSystem System, Zone LoadedZone, GameObject Building)
		{
			if (System == null || !System.Founded || LoadedZone == null
				|| !System.OwnedZone(LoadedZone.ZoneID))
				return KingdomJointCivicViewAdapters.Invalid("moot",
					"The requested reading is not on exact current owned ground.");
			if (!System.TryGetCurrentIdentity(out string realmId, out _))
				return KingdomJointCivicViewAdapters.Invalid("moot",
					"The current realm identity is unavailable.");

			string settlementId = System.SettlementIdForOwnedZone(LoadedZone.ZoneID);
			if (string.IsNullOrEmpty(settlementId)
				|| !System.TryFindSettlement(settlementId, out bool seated,
					out KingdomSettlement settlement))
				return KingdomJointCivicViewAdapters.Invalid("moot",
					"The moot is not on exact current owned ground.");
			KingdomCityBook book = seated ? System.City : settlement?.City;

			KingdomAssentingMootReceipt stored = book.AssentingMoot;
			if (stored == null || stored.Phase == KingdomAssentingMootPhase.None)
				return KingdomJointCivicViewAdapters.Missing("moot",
					"No assenting moot is recorded.");
			Zone zone = Building?.CurrentZone;
			string buildingId = Building?.IDIfAssigned;
			if (!GameObject.Validate(Building) || !ReferenceEquals(zone, LoadedZone)
				|| string.IsNullOrEmpty(buildingId)
				|| Building.GetPart<r_KingdomAssentingMoot>() == null)
				return KingdomJointCivicViewAdapters.Invalid("moot",
					"The exact loaded assenting-moot building is unavailable.");
			KingdomAssentingMootReceipt copy = stored.Copy();
			if (!KingdomAssentingMootRules.Validate(copy, out string failure))
				return KingdomJointCivicViewAdapters.Invalid("moot", failure);
			if (copy.Phase == KingdomAssentingMootPhase.Quarantined)
				return KingdomJointCivicViewAdapters.Invalid("moot", copy.Fault);
			if (!string.Equals(copy.RealmId, realmId, StringComparison.Ordinal)
				|| !string.Equals(copy.SettlementId, settlementId, StringComparison.Ordinal)
				|| !string.Equals(copy.ZoneId, zone.ZoneID, StringComparison.Ordinal)
				|| !string.Equals(copy.BuildingObjectId, buildingId, StringComparison.Ordinal)
				|| !string.Equals(copy.LotId,
					Building.GetStringProperty(KingdomPlots.PlotIdProperty),
					StringComparison.Ordinal)
				|| Building.GetIntProperty("KingdomBuilt") != 1)
				return KingdomJointCivicViewAdapters.Invalid("moot",
					"The moot receipt does not own this exact loaded building and ground.");
			return KingdomJointCivicViewAdapters.Moot(copy, Status(null, copy));
		}
	}
}
