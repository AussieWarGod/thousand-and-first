using System;
using ThousandAndFirst.Simulation.City;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Exact hosted provenance projected beside, never inside, ordinary zone memory.</summary>
	public static partial class KingdomHostedArcology
	{
		internal static void AddBindingProjection(KingdomSystem System, Zone CurrentZone,
			ref KingdomCatalogueRules.SupportTally Tally)
		{
			string zoneId = CurrentZone?.ZoneID;
			string settlement = System?.SettlementIdForOwnedZone(zoneId);
			if (string.IsNullOrEmpty(settlement)) return;
			Tally.Roof = BoundedProjectionAdd(Tally.Roof, BindingOverlay(System,
				KingdomCatalogueRules.SupportRoof, settlement, zoneId));
			Tally.Food = BoundedProjectionAdd(Tally.Food, BindingOverlay(System,
				KingdomCatalogueRules.SupportFood, settlement, zoneId));
		}

		internal static int[] FoodRateOverrides(KingdomSystem System, KingdomCityState State)
		{
			if (System == null || State == null
				|| !TryCurrentAuthoritySlot(System, out int slot,
					out KingdomHostedArcologyAuthority authority, out string failure)
				|| authority.Phase != KingdomHostedAuthorityPhase.Active
				|| authority.SettlementId != State.SettlementId
				|| !TryReadDeparture(slot, KingdomHostedArcologyTopology.TerraceLotKey,
					out KingdomHostedDepartureState projection, out failure)
				|| !KingdomHostedDepartureRules.Matches(projection, slot, authority,
					KingdomHostedArcologyTopology.TerraceLotKey)
				|| projection.Phase != KingdomHostedDeparturePhase.Settled
				|| projection.Food <= 0) return null;
			int[] rates = new int[State.ZoneCount]; bool found = false;
			for (int i = 0; i < State.ZoneCount; i++)
			{
				if (!State.TryZone(i, out KingdomZoneRow row)) return null;
				rates[i] = row.FoodCarry;
				if (row.ZoneId == projection.ExteriorZoneId)
				{
					rates[i] = BoundedProjectionAdd(rates[i], projection.Food);
					found = true;
				}
			}
			return found ? rates : null;
		}

		internal static bool RefreshActiveProjection(KingdomSystem System, Zone Z,
			KingdomBenefitIndex Benefits, bool FreshWater, out string Failure)
		{
			Failure = null;
			if (System == null || Z == null) return true;
			if (!TryCurrentAuthoritySlot(System, out int slot,
				out KingdomHostedArcologyAuthority authority, out Failure)) return false;
			if (authority.ZoneId != Z.ZoneID) return true;
			if (authority.Phase != KingdomHostedAuthorityPhase.Active)
				return ClearHostedProjectionSlots(slot, out Failure);
			KingdomBenefitReading reading = Benefits?.ReadingForRoot(authority.CarrierId);
			if (reading?.Designation == null
				|| KingdomConstruction.FindExactId(Z, authority.CarrierId,
					out GameObject shell) != KingdomPhysicalLookupState.Exact
				|| shell.GetPart<XRL.World.Parts.r_KingdomArcology>() == null)
			{
				ClearHostedProjectionSlots(slot, out string ignored);
				return DepartureStoreFail(
					"hosted exterior lacks its exact physical designation", out Failure);
			}
			return RefreshHostedProjections(System, shell, reading, FreshWater, out Failure);
		}

		internal static int ReachOverlay(KingdomSystem System, string Kind,
			string SettlementId, string ExceptZoneId)
		{
			if (Kind != "luxury" || System == null || string.IsNullOrEmpty(SettlementId)) return 0;
			if (!TryCurrentAuthoritySlot(System, out int slot,
				out KingdomHostedArcologyAuthority authority, out string failure)
				|| authority.Phase != KingdomHostedAuthorityPhase.Active
				|| !TryReadDeparture(slot, KingdomHostedArcologyTopology.WardLotKey,
					out KingdomHostedDepartureState state, out failure))
			{
				KingdomLog.Log("hosted reach overlay refused (" + (failure ?? "invalid store") + ")");
				return 0;
			}
			return KingdomHostedDepartureRules.Matches(state, slot, authority,
				KingdomHostedArcologyTopology.WardLotKey)
				? KingdomHostedDepartureRules.LuxuryFor(state, SettlementId, ExceptZoneId) : 0;
		}

		internal static int BindingOverlay(KingdomSystem System, string Kind,
			string SettlementId, string ExceptZoneId)
		{
			if (System == null || (Kind != KingdomCatalogueRules.SupportRoof
				&& Kind != KingdomCatalogueRules.SupportFood)) return 0;
			if (!TryCurrentAuthoritySlot(System, out int slot,
				out KingdomHostedArcologyAuthority authority, out string failure)
				|| authority.Phase != KingdomHostedAuthorityPhase.Active) return 0;
			long total = 0L;
			string[] lots = { KingdomHostedArcologyTopology.WardLotKey,
				KingdomHostedArcologyTopology.TerraceLotKey };
			for (int i = 0; i < lots.Length; i++)
			{
				if (!TryReadDeparture(slot, lots[i], out KingdomHostedDepartureState state,
					out failure)) return 0;
				if (KingdomHostedDepartureRules.Matches(state, slot, authority, lots[i]))
					total += KingdomHostedDepartureRules.BindingFor(
						state, Kind, SettlementId, ExceptZoneId);
			}
			return total >= int.MaxValue ? int.MaxValue : (int)total;
		}

		private static int BoundedProjectionAdd(int Left, int Right)
		{
			long value = (long)Math.Max(0, Left) + Math.Max(0, Right);
			return value >= int.MaxValue ? int.MaxValue : (int)value;
		}
	}
}
