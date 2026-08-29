using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;
using XRL.World.ZoneParts;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomAssentingMoot
	{
		internal static void ReconcileAll(KingdomSystem System, bool LoadMemberZones)
		{
			if (System == null || !System.Founded) return;
			List<KingdomCityBook> books = System.OwnedCityBooks();
			if (System.Seceded?.City != null) books.Add(System.Seceded.City);
			for (int i = 0; i < books.Count; i++)
			{
				KingdomCityBook book = books[i];
				book?.Normalize();
				KingdomAssentingMootReceipt receipt = book?.AssentingMoot;
				if (receipt == null || receipt.Phase == KingdomAssentingMootPhase.None) continue;
				if (!TryCachedZone(receipt.ZoneId, out Zone zone)) continue;
				GameObject building;
				TryExactBuilding(receipt, out building);
				string failure;
				Reconcile(System, book, building, LoadMemberZones, out failure);
			}
		}

		internal static void ReconcileZone(KingdomSystem System, Zone Zone)
		{
			if (System == null || Zone == null) return;
			PruneLoadedMemberProjections(System, Zone);
			KingdomAssentingWardAuthority marker =
				Zone.GetPart<KingdomAssentingWardAuthority>();
			KingdomCityBook book = BookForZone(System, Zone.ZoneID);
			if (book == null)
			{
				if (marker != null)
				{
					string ignored;
					RemoveZoneProjection(Zone, null, out ignored);
				}
				return;
			}
			book.Normalize();
			KingdomAssentingMootReceipt receipt = book.AssentingMoot;
			if (receipt.Phase == KingdomAssentingMootPhase.None
				|| !string.Equals(receipt.ZoneId, Zone.ZoneID, StringComparison.Ordinal))
			{
				if (marker != null)
				{
					string ignored;
					RemoveZoneProjection(Zone, null, out ignored);
				}
				return;
			}
			GameObject building;
			TryExactBuilding(receipt, out building);
			string failure;
			Reconcile(System, book, building, false, out failure);
		}

		internal static void OnMemberDeath(r_KingdomAssentingMootMember Marker,
			GameObject Body)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (system == null || Marker == null || Body == null
				|| !TryBook(system, Marker.SettlementId, out KingdomCityBook book,
					out bool owned)) return;
			book.Normalize();
			KingdomAssentingMootReceipt receipt = book.AssentingMoot;
			if (!MarkerAuthorityMatches(Marker, receipt, Body)) return;
			GameObject building;
			if (TryExactBuilding(receipt, out building)
				&& TryContext(system, building, out KingdomAssentingMootContext context,
					out string _))
			{
				string failure;
				Suspend(context, receipt, "A named moot member died.", out failure);
			}
			else
			{
				string failure;
				SuspendBook(book, receipt, "A named moot member died.", out failure);
			}
		}

		private static KingdomCityBook BookForZone(KingdomSystem System, string ZoneId)
		{
			if (System.ClaimedZones != null && System.ClaimedZones.Contains(ZoneId))
				return System.City;
			KingdomSettlement other = System.FindNonSeatSettlementByZone(ZoneId);
			if (other?.City != null) return other.City;
			if (System.Seceded?.ClaimedZones != null
				&& System.Seceded.ClaimedZones.Contains(ZoneId)) return System.Seceded.City;
			return null;
		}
	}
}
