using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomRealmRetirementAuthority
	{
		private static bool TryBuildLocators(KingdomSystem System,
			out List<KingdomRemovalLocator> Locators, out string Failure)
		{
			Locators = new List<KingdomRemovalLocator>(); Failure = null;
			Dictionary<string, string> seen = new Dictionary<string, string>(
				StringComparer.Ordinal);
			if (!AddGround(System.ClaimedZones, System.City?.SettlementId, seen, out Failure))
				return false;
			List<KingdomSettlement> others = System.NonSeatSettlements();
			for (int i = 0; i < others.Count; i++)
				if (!AddGround(others[i]?.ClaimedZones, others[i]?.City?.SettlementId,
					seen, out Failure)) return false;
			if (System.Seceded != null && !AddGround(System.Seceded.ClaimedZones,
				System.Seceded.City?.SettlementId, seen, out Failure)) return false;
			KingdomInheritanceState inheritance = KingdomInheritanceState.Instance;
			if (inheritance != null && inheritance.Phase == KingdomInheritancePhase.Committed
				&& !string.IsNullOrEmpty(inheritance.SelectedZoneId))
			{
				if (seen.TryGetValue(inheritance.SelectedZoneId, out string owner)
					&& !string.IsNullOrEmpty(owner))
					return Fail("inheritance ground overlaps a settlement under a different owner",
						out Failure);
				seen[inheritance.SelectedZoneId] = "";
			}
			if (seen.Count == 0 || seen.Count > KingdomRealmRetirementState.MaxLocators)
				return Fail("tracked ground is empty or exceeds the removal locator cap", out Failure);
			List<string> zones = new List<string>(seen.Keys);
			zones.Sort(StringComparer.Ordinal);
			for (int i = 0; i < zones.Count; i++)
				Locators.Add(new KingdomRemovalLocator
				{
					ZoneId = zones[i], SettlementId = seen[zones[i]] ?? "",
					State = KingdomRemovalLocatorState.OutstandingVisit
				});
			return true;
		}

		private static bool AddGround(IList<string> Zones, string SettlementId,
			Dictionary<string, string> Seen, out string Failure)
		{
			Failure = null;
			if (Zones == null || string.IsNullOrEmpty(SettlementId))
				return Fail("a retained settlement has no exact ground or identity", out Failure);
			for (int i = 0; i < Zones.Count; i++)
			{
				string zone = Zones[i];
				if (string.IsNullOrEmpty(zone))
					return Fail("a retained ground locator is empty", out Failure);
				if (Seen.TryGetValue(zone, out string owner) && owner != SettlementId)
					return Fail("two settlement identities claim the same tracked ground", out Failure);
				Seen[zone] = SettlementId;
			}
			return true;
		}

		private static void InspectLifecycle(KingdomSystem System,
			KingdomRealmRetirementReport Report)
		{
			InspectLifecycleBook(System.LifecycleBook, System.SeatName, Report);
			List<KingdomSettlement> others = System.NonSeatSettlements();
			for (int i = 0; i < others.Count; i++)
				InspectLifecycleBook(others[i]?.LifecycleBook,
					others[i]?.SettlementName ?? "non-seat city", Report);
			if (System.Seceded != null)
				InspectLifecycleBook(System.Seceded.LifecycleBook,
					System.Seceded.SettlementName ?? "seceded city", Report);
		}

		private static void InspectLifecycleBook(KingdomLifecycleBook Book, string Name,
			KingdomRealmRetirementReport Report)
		{
			string label = string.IsNullOrEmpty(Name) ? "a retained city" : Name;
			if (!KingdomLifecycleRules.CanOwnAuthority(Book))
			{
				Report.Blockers.Add(label + " has malformed or quarantined lifecycle authority.");
				return;
			}
			if (Book.PlainGuest != null || Book.NotableGuest != null || Book.Raid != null
				|| Book.Petition != null)
				Report.Blockers.Add(label + " has a guest, raid, or petition receipt in flight.");
			KingdomGrowthBook growth = Book.Growth;
			if (growth == null || growth.Quarantined || growth.OpaqueWireVersion != 0
				|| growth.OpaquePayload != null)
			{
				Report.Blockers.Add(label + " has unreadable Growth authority."); return;
			}
			if (growth.HeartbeatOp != null || growth.ArrivalOp != null
				|| growth.DepartureOp != null || growth.DeliveryOp != null
				|| growth.FetchOp != null || growth.MillOp != null
				|| growth.ArrivalCandidate != null || growth.MigrationPending)
				Report.Blockers.Add(label + " has Growth work or a candidate body in flight.");
			for (int i = 0; i < (growth.FieldOps?.Count ?? 0); i++)
				if (growth.FieldOps[i]?.Operation != null)
				{
					Report.Blockers.Add(label + " has a field operation in flight."); break;
				}
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
