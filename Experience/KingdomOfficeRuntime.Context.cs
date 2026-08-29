using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.City;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomOfficeRuntime
	{
		private sealed class CityContext
		{
			internal string SettlementId;
			internal string SettlementName;
			internal KingdomCityBook Book;
			internal bool Seated;
			internal KingdomSettlement Settlement;
			internal KingdomCityState State;
			internal int WorkId;
			internal Zone Zone;
			internal KingdomSurvey Survey;
		}

		internal static string RoleFor(KingdomCivicOfficeReceipt Receipt)
		{
			return Receipt == null ? null : KingdomOfficeRules.ChooseTitle(Receipt.SettlementName)
				+ " of " + Receipt.SettlementName;
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
			if (!TryLocus(state, out int workId))
			{
				Failure = "The city has no finished civic or heart work to serve as this office's locus.";
				return false;
			}
			Context = new CityContext { SettlementId = settlementId,
				SettlementName = settlementName, Book = book, Seated = seated,
				Settlement = settlement, State = state, WorkId = workId,
				Zone = Zone, Survey = Survey };
			return true;
		}

		private static bool TryLocus(KingdomCityState State, out int WorkId)
		{
			WorkId = 0; int priority = int.MaxValue;
			for (int i = 0; i < State.WorkCount; i++)
			{
				if (!State.TryWork(i, out KingdomWorkRow row) || row.WorkId <= 0
					|| string.IsNullOrEmpty(row.DesignKey)) continue;
				int rung = KingdomPlotRules.HeartRungOf(row.DesignKey);
				int next = rung > 0 ? 0 : CivicDesign(row.DesignKey) ? 1 : 2;
				if (next < priority || next == priority && row.WorkId < WorkId)
				{
					priority = next; WorkId = row.WorkId;
				}
			}
			return WorkId > 0;
		}

		private static bool CivicDesign(string Key)
		{
			return Key == "hall" || Key == "archive" || Key == "shrine"
				|| Key == "market" || Key == "guesthouse";
		}

		private static bool TryOffer(CityContext Context, out KingdomOfficeCandidate First,
			out KingdomOfficeCandidate Second)
		{
			List<KingdomOfficeCandidate> rows = new List<KingdomOfficeCandidate>();
			for (int i = 0; i < Context.State.ResidentCount; i++)
			{
				if (!Context.State.TryResident(i, out KingdomResidentRow row)) continue;
				GameObject body = Context.Survey.FindCitizen(row.ResidentId);
				bool loaded = GameObject.Validate(body) && body.IsAlive
					&& ReferenceEquals(body.CurrentZone, Context.Zone)
					&& !body.IsPlayer() && !body.IsPlayerLed();
				rows.Add(new KingdomOfficeCandidate { ResidentId = row.ResidentId,
					Name = row.Name, Origin = row.Origin, ArrivedTick = row.ArrivedTick,
					Eligible = row.Standing == KingdomResidentStanding.Resident && loaded });
			}
			return KingdomOfficeOfferRules.TryOffer(rows.ToArray(), out First, out Second);
		}

		private static bool ExactCandidate(KingdomSystem System, CityContext Context,
			KingdomOfficeCandidate Expected, out GameObject Body, out string Failure)
		{
			Body = null; Failure = null;
			if (!TryOffer(Context, out KingdomOfficeCandidate first,
				out KingdomOfficeCandidate second)
				|| Expected == null || Expected.ResidentId != first.ResidentId
					&& Expected.ResidentId != second.ResidentId)
			{
				Failure = "The two-name office offer changed; review it again."; return false;
			}
			if (!KingdomResidents.TryResolveBoundBody(System, Expected.ResidentId, false,
				out Body, out string _) || !GameObject.Validate(Body) || !Body.IsAlive
				|| !ReferenceEquals(Body.CurrentZone, Context.Zone)
				|| Body.IsPlayer() || Body.IsPlayerLed())
			{
				Failure = Expected.Name + " is no longer an exact loaded eligible resident.";
				return false;
			}
			return true;
		}

		private static bool HasWork(KingdomCityState State, int WorkId)
		{
			for (int i = 0; State != null && i < State.WorkCount; i++)
				if (State.TryWork(i, out KingdomWorkRow row) && row.WorkId == WorkId) return true;
			return false;
		}
	}
}
