using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.City;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRemembranceRuntime
	{
		internal const string MemorialForProperty = "KingdomMemorialFor";

		private sealed class CityContext
		{
			internal string SettlementId;
			internal string SettlementName;
			internal KingdomCityBook Book;
			internal bool Seated;
			internal KingdomSettlement Settlement;
			internal KingdomCityState State;
			internal Zone Zone;
			internal KingdomSurvey Survey;
		}

		private sealed class DeathChoice
		{
			internal KingdomResidentRow Row;
			internal KingdomOfficeRules.DeathCause Cause;
		}

		private static bool TryContext(KingdomSystem System, Zone Zone, KingdomSurvey Survey,
			out CityContext Context, out string Failure)
		{
			Context = null; Failure = null;
			if (System == null || !System.Founded || Zone == null || Survey == null
				|| !System.OwnedZone(Zone.ZoneID))
			{
				Failure = "Stand on an owned city's held ground."; return false;
			}
			bool seated = System.ClaimedZones != null && System.ClaimedZones.Contains(Zone.ZoneID);
			KingdomSettlement settlement = seated
				? null : System.FindNonSeatSettlementByZone(Zone.ZoneID);
			KingdomCityBook book = seated ? System.City : settlement?.City;
			string settlementId = book?.SettlementId;
			string settlementName = seated ? System.SeatName : settlement?.SettlementName;
			if (book == null || string.IsNullOrEmpty(settlementId)
				|| string.IsNullOrEmpty(settlementName))
			{
				Failure = "The exact owned city identity cannot be proved."; return false;
			}
			if (!book.TryRead(out KingdomCityState state, out KingdomCityFault fault))
			{
				Failure = "The exact city book is unreadable: " + fault; return false;
			}
			Context = new CityContext { SettlementId = settlementId,
				SettlementName = settlementName, Book = book, Seated = seated,
				Settlement = settlement, State = state, Zone = Zone, Survey = Survey };
			return true;
		}

		private static bool TryMourner(CityContext Context, out KingdomResidentRow Mourner)
		{
			Mourner = default(KingdomResidentRow); bool found = false;
			for (int i = 0; i < Context.State.ResidentCount; i++)
			{
				if (!Context.State.TryResident(i, out KingdomResidentRow row)
					|| row.Standing != KingdomResidentStanding.Resident) continue;
				GameObject body = Context.Survey.FindCitizen(row.ResidentId);
				if (!GameObject.Validate(body) || !body.IsAlive
					|| !ReferenceEquals(body.CurrentZone, Context.Zone)
					|| body.IsPlayer() || body.IsPlayerLed()) continue;
				if (!found || row.ArrivedTick < Mourner.ArrivedTick
					|| row.ArrivedTick == Mourner.ArrivedTick
						&& row.ResidentId < Mourner.ResidentId)
				{
					Mourner = row; found = true;
				}
			}
			return found;
		}

		private static bool TryExactDeath(CityContext Context, int ResidentId,
			out DeathChoice Choice)
		{
			for (int i = 0; Context?.State != null && i < Context.State.ResidentCount; i++)
			{
				if (!Context.State.TryResident(i, out KingdomResidentRow row)
					|| row.ResidentId != ResidentId
					|| row.Standing != KingdomResidentStanding.Dead
					|| !KingdomResidentRules.TryDeathCauseOrdinal(row.Cause, out int cause)) continue;
				Choice = new DeathChoice { Row = row,
					Cause = (KingdomOfficeRules.DeathCause)cause };
				return true;
			}
			Choice = null; return false;
		}

		internal static bool IsFixture(GameObject Item)
		{
			string blueprint = Item?.Blueprint;
			return blueprint == "r_KingdomCairn" || blueprint == "r_KingdomGraveGrove"
				|| blueprint == "r_KingdomNicheTomb";
		}
	}
}
