using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal sealed class KingdomAssentingMootContext
	{
		internal KingdomSystem System;
		internal KingdomCityBook Book;
		internal KingdomSettlement Settlement;
		internal GameObject Building;
		internal Zone Zone;
		internal string RealmId;
		internal string SettlementId;
		internal string SettlementName;
		internal bool Owned;
		internal bool Seated;
	}

	/// <summary>Engine adapter for one exact assenting-moot authority and native ward.</summary>
	public static partial class KingdomAssentingMoot
	{
		internal static bool TryContext(KingdomSystem System, GameObject Building,
			out KingdomAssentingMootContext Context, out string Failure)
		{
			Context = null;
			Failure = "The exact assenting moot and city cannot be proved.";
			Zone zone = Building?.CurrentZone;
			if (System == null || !System.Founded || !GameObject.Validate(Building)
				|| zone == null || Building.GetPart<r_KingdomAssentingMoot>() == null) return false;
			string realm;
			string seatId;
			if (!System.TryGetCurrentIdentity(out realm, out seatId)) return false;
			bool seated = System.ClaimedZones != null
				&& System.ClaimedZones.Contains(zone.ZoneID);
			KingdomSettlement settlement = seated ? null
				: System.FindNonSeatSettlementByZone(zone.ZoneID);
			bool owned = seated || settlement != null;
			if (!owned && System.Seceded != null && System.Seceded.ClaimedZones != null
				&& System.Seceded.ClaimedZones.Contains(zone.ZoneID)) settlement = System.Seceded;
			KingdomCityBook book = seated ? System.City : settlement?.City;
			string id = book?.SettlementId;
			string name = seated ? System.SeatName : settlement?.SettlementName;
			if (book == null || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name)) return false;
			book.Normalize();
			Context = new KingdomAssentingMootContext
			{
				System = System,
				Book = book,
				Settlement = settlement,
				Building = Building,
				Zone = zone,
				RealmId = realm,
				SettlementId = id,
				SettlementName = name,
				Owned = owned,
				Seated = seated
			};
			return true;
		}

		internal static bool TryBook(KingdomSystem System, string SettlementId,
			out KingdomCityBook Book, out bool Owned)
		{
			Book = null;
			Owned = false;
			if (System == null || string.IsNullOrEmpty(SettlementId)) return false;
			bool seated;
			KingdomSettlement settlement;
			if (System.TryFindSettlement(SettlementId, out seated, out settlement))
			{
				Book = seated ? System.City : settlement?.City;
				Owned = Book != null;
				return Book != null;
			}
			if (System.Seceded?.City != null && string.Equals(
				System.Seceded.City.SettlementId, SettlementId, StringComparison.Ordinal))
			{
				Book = System.Seceded.City;
				return true;
			}
			return false;
		}

		internal static bool TryExactBuilding(KingdomAssentingMootReceipt Receipt,
			out GameObject Building)
		{
			Building = null;
			if (Receipt == null || string.IsNullOrEmpty(Receipt.BuildingObjectId)) return false;
			GameObject exact = GameObject.FindByID(Receipt.BuildingObjectId);
			if (!GameObject.Validate(exact) || exact.CurrentZone == null
				|| !string.Equals(exact.IDIfAssigned, Receipt.BuildingObjectId,
					StringComparison.Ordinal)
				|| !string.Equals(exact.CurrentZone.ZoneID, Receipt.ZoneId,
					StringComparison.Ordinal)
				|| !string.Equals(exact.GetStringProperty(KingdomPlots.PlotIdProperty),
					Receipt.LotId, StringComparison.Ordinal)
				|| exact.GetIntProperty("KingdomBuilt") != 1
				|| exact.GetPart<r_KingdomAssentingMoot>() == null) return false;
			Building = exact;
			return true;
		}

		internal static long Now(long Minimum = 0L)
		{
			long now = The.Game == null || The.Game.TimeTicks < 0L ? 0L : The.Game.TimeTicks;
			return now < Minimum ? Minimum : now;
		}
	}
}
