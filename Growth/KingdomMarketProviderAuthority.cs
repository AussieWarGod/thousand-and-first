using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>Re-observes the physical provider at the player-facing ingress seam. The growth
	/// pass establishes civic service; this proof prevents a removed, disabled, moved, or
	/// redesignated provider from lending stale authority later in the same tick.</summary>
	internal static class KingdomMarketProviderAuthority
	{
		internal static bool TryProve(KingdomSystem System, GameObject Body,
			int ProjectedTier, out string Failure)
		{
			return TryObserve(System, Body, ProjectedTier, true, out Failure);
		}

		internal static bool TryProveProjection(KingdomSystem System, GameObject Body,
			int ProjectedTier, out string Failure)
		{
			return TryObserve(System, Body, ProjectedTier, false, out Failure);
		}

		internal static bool TryProveLegendary(KingdomSystem System, GameObject Body,
			int ProjectedTier, out string Failure)
		{
			if (!TryProve(System, Body, ProjectedTier, out Failure)) return false;
			string settlement = System.SettlementIdForOwnedZone(Body.CurrentZone?.ZoneID);
			if (!KingdomExperienceRules.TryGetOffice(System.Experience, settlement,
				out KingdomCivicOfficeReceipt receipt, out Failure)
				|| receipt == null || receipt.Phase != KingdomCivicOfficePhase.Held)
			{
				Failure = Failure ?? "legendary civic trade lacks a current held office";
				return false;
			}
			GameObject exact = null;
			foreach (GameObject candidate in KingdomSurvey.ObjectsFor(Body.CurrentZone))
			{
				r_KingdomOfficeProjection marker =
					candidate?.GetPart<r_KingdomOfficeProjection>();
				if (marker == null || !marker.Matches(System, receipt, candidate)
					|| !LiveResident(System, settlement, candidate,
						receipt.HolderResidentId)) continue;
				if (exact != null) { Failure = "legendary market office is ambiguous"; return false; }
				exact = candidate;
			}
			if (exact != null) return true;
			Failure = "legendary civic trade lacks its exact held office projection";
			return false;
		}

		internal static bool LiveResident(KingdomSystem System, string Settlement,
			GameObject Body, int ResidentId)
		{
			if (System == null || !GameObject.Validate(Body) || !Body.IsAlive || Body.IsPlayer()
				|| ResidentId <= 0 || KingdomResidents.IdOf(Body) != ResidentId
				|| !KingdomCitizenship.BelongsTo(System, Body)
				|| System.SettlementIdForOwnedZone(Body.CurrentZone?.ZoneID) != Settlement)
				return false;
			List<KingdomCityBook> books = System.OwnedCityBooks(); int matches = 0;
			KingdomResidentRow found = default(KingdomResidentRow);
			for (int i = 0; books != null && i < books.Count; i++)
				if (books[i]?.SettlementId == Settlement && KingdomResidents.TryResident(
					books[i], ResidentId, out KingdomResidentRow row)) { found = row; matches++; }
			return matches == 1 && KingdomResidentRules.OnTheRoll(found);
		}

		private static bool TryObserve(KingdomSystem System, GameObject Body,
			int ProjectedTier, bool RequireRecordedTier, out string Failure)
		{
			Failure = null;
			Zone zone = Body?.CurrentZone;
			if (System == null || !GameObject.Validate(Body) || !Body.IsAlive || Body.IsPlayer()
				|| zone == null
				|| string.IsNullOrEmpty(System.SettlementIdForOwnedZone(zone.ZoneID)))
			{
				Failure = "market ingress has no exact living owned ground"; return false;
			}
			KingdomSurvey survey = KingdomSurvey.Take(zone);
			// A bound pass normally shares one observation. Player trade is a new semantic
			// operation, so provider state is deliberately re-read before granting authority.
			survey.InvalidateBenefits();
			if (!KingdomGrowth.TryMarketServiceStanding(System, survey,
				out int observedTier, out bool liveCapability, out Failure)) return false;
			bool exact = RequireRecordedTier
				? KingdomMarketProviderRules.ExactLiveAuthority(liveCapability,
					observedTier, ProjectedTier, System.ShopTier)
				: KingdomMarketProviderRules.ExactLiveProjection(liveCapability,
					observedTier, ProjectedTier);
			if (!exact)
			{
				Failure = "the exact live market provider no longer proves this service tier";
				return false;
			}
			return true;
		}
	}
}
