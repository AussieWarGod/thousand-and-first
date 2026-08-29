using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		internal static void OnPhysicalFirstGuestSuspending(KingdomSystem system, Zone zone)
		{
			KingdomGrowthArrivalCandidate candidate =
				system?.LifecycleBook?.Growth?.ArrivalCandidate;
			if (!PhysicalFirstGuestNeedsCustody(candidate, zone)) return;
			if (!TryRetractPhysicalFirstGuest(candidate, zone, out GameObject _, out string failure))
				KingdomLog.Log("first guest suspension retained loaded evidence: " + failure);
		}

		internal static void OnPhysicalFirstGuestZoneActivated(KingdomSystem system, Zone zone)
		{
			KingdomGrowthArrivalCandidate candidate =
				system?.LifecycleBook?.Growth?.ArrivalCandidate;
			if (!PhysicalFirstGuestNeedsCustody(candidate, zone)) return;
			if (!TryProjectPhysicalFirstGuest(candidate, zone, out GameObject body,
				out string failure))
			{
				KingdomLog.Log("first guest projection remains pending: " + failure); return;
			}
			ContinueCommittedPhysicalFirstGuestAction(system, zone, candidate, body);
		}

		private static bool PhysicalFirstGuestNeedsCustody(
			KingdomGrowthArrivalCandidate candidate, Zone zone)
		{
			return candidate?.FirstGuest?.RulesVersion == 2 && zone != null
				&& candidate.LodgingZoneId == zone.ZoneID
				&& candidate.Phase == KingdomGrowthArrivalCandidatePhase.GuestHosted;
		}

		private static bool TryProjectPhysicalFirstGuest(KingdomGrowthArrivalCandidate candidate,
			Zone zone, out GameObject body, out string failure)
		{
			body = null; failure = null;
			if (TryExactLoadedPhysicalFirstGuest(candidate, zone, out body)) return true;
			if (TryExactLoadedPhysicalFirstGuest(candidate, zone, out body, true)
				&& !TryRetractPhysicalFirstGuest(candidate, zone, out body, out failure)) return false;
			if (CountArrivalMarker(zone, candidate?.Marker) != 0)
				return FailFirstGuest("loaded marker count is ambiguous", out failure);
			if (!TryExactPhysicalFirstGuestEscrow(candidate, out body))
				return FailFirstGuest("exact escrow body is absent or replaced", out failure);
			Cell cell = zone.GetCell(candidate.ArrivalX, candidate.ArrivalY);
			if (!ArrivalCellIsStillOpen(cell))
				return FailFirstGuest("exact arrival cell is occupied", out failure);
			GameObject accepted = null;
			try { accepted = cell.AddObject(body, NoStack: true, Silent: true); }
			finally { KingdomSurvey.ObserveAddResultInActive(zone, body, accepted); }
			if (!ReferenceEquals(accepted, body))
			{
				if (body.CurrentCell != null) body.RemoveFromContext();
				return FailFirstGuest("zone refused the exact guest body", out failure);
			}
			body.MakeActive();
			if (!TryExactLoadedPhysicalFirstGuest(candidate, zone, out GameObject loaded, true)
				|| !ReferenceEquals(loaded, body))
			{
				body.RemoveFromContext();
				return FailFirstGuest("guest placement endpoint did not prove", out failure);
			}
			object rooted;
			if (!The.Game.ObjectGameState.TryGetValue(candidate.EscrowKey, out rooted)
				|| !ReferenceEquals(rooted, body))
			{
				body.RemoveFromContext();
				return FailFirstGuest("guest escrow changed during projection", out failure);
			}
			The.Game.ObjectGameState.Remove(candidate.EscrowKey);
			if (The.Game.ObjectGameState.ContainsKey(candidate.EscrowKey))
			{
				body.RemoveFromContext(); return FailFirstGuest(
					"guest escrow could not release after placement", out failure);
			}
			return true;
		}

		private static bool TryRetractPhysicalFirstGuest(KingdomGrowthArrivalCandidate candidate,
			Zone zone, out GameObject body, out string failure)
		{
			body = null; failure = null;
			if (TryExactPhysicalFirstGuestEscrow(candidate, out body)
				&& CountArrivalMarker(zone, candidate.Marker) == 0) return true;
			if (!TryExactLoadedPhysicalFirstGuest(candidate, zone, out body, true))
				return FailFirstGuest("exact loaded guest body is absent or ambiguous", out failure);
			object oldRoot;
			bool hadRoot = The.Game.ObjectGameState.TryGetValue(candidate.EscrowKey, out oldRoot);
			if (hadRoot && !ReferenceEquals(oldRoot, body))
				return FailFirstGuest("guest escrow belongs to another object", out failure);
			if (!RootArrivalCandidate(candidate, body))
				return FailFirstGuest("guest escrow root refused custody", out failure);
			if (!body.TryRemoveFromContext())
			{
				if (TryExactLoadedPhysicalFirstGuest(candidate, zone,
					out GameObject stillLoaded, true) && ReferenceEquals(stillLoaded, body))
					The.Game.ObjectGameState.Remove(candidate.EscrowKey);
				return FailFirstGuest("guest body refused context removal", out failure);
			}
			body.RemoveFromContext(); KingdomSurvey.ObserveCurrentTopologyInActive(zone, body);
			if (!TryExactPhysicalFirstGuestEscrow(candidate, out GameObject escrowed)
				|| !ReferenceEquals(escrowed, body) || CountArrivalMarker(zone, candidate.Marker) != 0)
			{
				return FailFirstGuest("guest retraction endpoint did not prove", out failure);
			}
			return true;
		}

		private static bool TryExactLoadedPhysicalFirstGuest(
			KingdomGrowthArrivalCandidate candidate, Zone zone, out GameObject body,
			bool AllowEscrowRoot = false)
		{
			body = null;
			if (candidate == null || zone == null || zone.ZoneID != candidate.LodgingZoneId
				|| CountArrivalMarker(zone, candidate.Marker) != 1) return false;
			body = zone.FindObjectByID(candidate.ObjectId);
			return ExactFirstGuestBodyIdentity(body, candidate) && body.CurrentCell != null
				&& ReferenceEquals(body.CurrentZone, zone)
				&& (AllowEscrowRoot || !The.Game.ObjectGameState.ContainsKey(candidate.EscrowKey));
		}

		private static bool TryExactPhysicalFirstGuestEscrow(
			KingdomGrowthArrivalCandidate candidate, out GameObject body)
		{
			body = null; object rooted;
			if (The.Game == null || candidate == null
				|| !The.Game.ObjectGameState.TryGetValue(candidate.EscrowKey, out rooted)) return false;
			body = rooted as GameObject;
			return ExactFirstGuestBodyIdentity(body, candidate) && body.CurrentCell == null
				&& (body.Physics == null || body.Physics.InInventory == null);
		}

		private static bool ExactFirstGuestBodyIdentity(GameObject body,
			KingdomGrowthArrivalCandidate candidate)
		{
			return candidate != null && GameObject.Validate(body)
				&& body.IDIfAssigned == candidate.ObjectId && body.Blueprint == candidate.Blueprint
				&& body.Count == 1 && body.GetStringProperty(ArrivalMarkerProperty) == candidate.Marker;
		}
	}
}
