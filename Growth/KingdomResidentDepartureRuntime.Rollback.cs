using System;
using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomResidentDepartureRuntime
	{
		private static bool TryRollbackPrepared(KingdomSystem System, GameObject Body,
			KingdomResidentDepartureOperation Operation, out string Failure,
			bool RequireMarker = true)
		{
			Failure = null;
			if (!KingdomResidentDepartureRules.Valid(Operation)
				|| Operation.Phase != (int)KingdomResidentDeparturePhase.Prepared
					&& Operation.Phase != (int)KingdomResidentDeparturePhase.RolesPrepared
				|| !KingdomCitizenship.BelongsTo(System, Body)) return false;
			bool polity = RollbackPolity(System, Operation, out string polityFailure);
			bool office = RollbackOffice(System, Body, Operation, out string officeFailure);
			bool cook = RollbackCook(System, Body, Operation, out string cookFailure);
			if (!polity || !office || !cook)
			{
				Failure = !polity ? polityFailure : !office ? officeFailure : cookFailure;
				return false;
			}
			KingdomResidentDestructionAuthorization authorization = AuthorizationOf(Operation);
			r_KingdomResidentDeparture marker = Body.GetPart<r_KingdomResidentDeparture>();
			bool authority = marker != null
				? KingdomResidentTransitionAuthority.CanPrepareJournaledRoles(
					System, Body, Operation, authorization, RolesPrepared: false)
				: !RequireMarker && KingdomResidentTransitionAuthority
					.CanPrepareResidentBodyDestruction(System, Body,
						Operation.ResidentId, authorization);
			if (!authority)
			{
				Failure = "rolled-back departure did not restore exact resident authority";
				return false;
			}
			if (marker == null && !RequireMarker)
			{
				System.ResidentDeparture = KingdomResidentDepartureRules.Empty(); return true;
			}
			if (marker == null || !marker.Matches(Operation, Body))
			{
				Failure = "departure rollback lost its exact body marker"; return false;
			}
			try { Body.RemovePart(marker); }
			catch (Exception ex)
			{
				Failure = "departure rollback marker removal threw "
					+ ex.GetType().Name; return false;
			}
			if (Body.GetPart<r_KingdomResidentDeparture>() != null)
			{
				Failure = "departure rollback marker removal left residue"; return false;
			}
			System.ResidentDeparture = KingdomResidentDepartureRules.Empty(); return true;
		}

		private static bool RollbackCook(KingdomSystem System, GameObject Body,
			KingdomResidentDepartureOperation Operation, out string Failure)
		{
			Failure = null; KingdomNamedCookReceipt prior = Operation.PriorCook;
			if (prior == null) return true;
			KingdomCityBook match = null;
			List<KingdomCityBook> books = System.OwnedCityBooks();
			for (int i = 0; books != null && i < books.Count; i++)
				if (books[i]?.NamedCook?.ResidentId == Operation.ResidentId
					|| books[i]?.NamedCook?.BodyObjectId == Operation.BodyObjectId)
				{
					if (match != null) return false; match = books[i];
				}
			if (match == null) return false;
			if (SameCook(match.NamedCook, prior)) return true;
			return KingdomNamedCook.CancelPreparedCookLoss(System, Body, prior,
				KingdomNamedCookVacancyCause.Departure, out Failure);
		}

		private static bool RollbackOffice(KingdomSystem System, GameObject Body,
			KingdomResidentDepartureOperation Operation, out string Failure)
		{
			Failure = null; KingdomCivicOfficeReceipt prior = Operation.PriorOffice;
			if (prior == null) return true;
			KingdomCivicOfficeReceipt current = null;
			for (int i = 0; System.Experience != null
				&& i < System.Experience.Offices.Count; i++)
			{
				KingdomCivicOfficeReceipt row = System.Experience.Offices[i];
				if (row?.SettlementId != prior.SettlementId) continue;
				if (current != null) return false; current = row;
			}
			if (SameOffice(current, prior)) return true;
			return KingdomOfficeRuntime.TryCancelHolderDeparture(System, Body, prior,
				out Failure);
		}

		private static bool RollbackPolity(KingdomSystem System,
			KingdomResidentDepartureOperation Operation, out string Failure)
		{
			Failure = null;
			if (Operation.PriorPolity == null) return true;
			KingdomPolityNamedFigureRecord current = null;
			for (int i = 0; System.PolityLedger != null
				&& i < System.PolityLedger.NamedFigures.Count; i++)
				if (System.PolityLedger.NamedFigures[i]?.FigureId
					== Operation.PriorPolity.FigureId)
				{
					if (current != null) return false;
					current = System.PolityLedger.NamedFigures[i];
				}
			if (SamePolity(current, Operation.PriorPolity)) return true;
			return KingdomPolityResidentTransition.TryRollback(System,
				new KingdomPolityResidentTransitionPreparation(Operation.PriorPolity,
					Operation.PolityConclusionRef), out Failure);
		}
	}
}
