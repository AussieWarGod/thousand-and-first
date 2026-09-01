using System;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomResidentDepartureRuntime
	{
		private static bool TryCloseRoles(KingdomSystem System, GameObject Body,
			KingdomResidentDepartureOperation Operation,
			KingdomResidentDestructionAuthorization Authorization, out string Failure)
		{
			Failure = null;
			if (!KingdomOfficeRuntime.ObserveHolderLoss(System, Body,
				KingdomCivicOfficeVacancyCause.Departure, out Failure)) return false;
			if (!KingdomNamedCook.ObserveCookLoss(System, Body,
				KingdomNamedCookVacancyCause.Departure, out Failure)) return false;
			if (!KingdomPolityResidentTransition.TryConclude(System, Body,
				Operation.ResidentId, KingdomPolityResidentTransitionCause.Departure,
				out KingdomPolityResidentTransitionPreparation _, out Failure)) return false;
			return KingdomLabCivicRuntime.TryCompleteAuthorizedDeparture(System,
				Body.CurrentZone, Body, Operation.ResidentId, Authorization, out Failure);
		}

		private static bool TryPublishEffects(KingdomSystem System,
			KingdomResidentDepartureOperation Operation, out string Failure)
		{
			Failure = null;
			if (System?.Ledger == null || !KingdomResidentDepartureRules.Valid(Operation))
				return false;
			if (System.Ledger.Departures == Operation.DeparturesBefore)
				System.Ledger.Departures++;
			else if (System.Ledger.Departures != Operation.DeparturesBefore + 1)
			{
				Failure = "departure accounting lost its exact scalar CAS"; return false;
			}
			if (Operation.Chronicled)
			{
				if (!KingdomChronicle.RecordOnce(System,
					Operation.OperationId + ":chronicle", Operation.ChronicleLine))
				{
					Failure = "departure Chronicle receipt remains pending"; return false;
				}
				if (!System.Ledger.Notes.Contains(Operation.LedgerLine))
					System.Ledger.Note(Operation.LedgerLine);
			}
			return true;
		}

		private static bool TryDestroyBody(KingdomSystem System, GameObject Body,
			KingdomResidentDepartureOperation Operation, out string Failure)
		{
			Failure = null;
			r_KingdomResidentDeparture marker = Body?.GetPart<r_KingdomResidentDeparture>();
			if (marker?.Matches(Operation, Body) != true) return false;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Body.CurrentZone);
			try
			{
				if (!marker.TalliesClosed)
				{
					KingdomResidentIdentity.Forget(System, Body);
					KingdomCreed.Forget(System, Body);
					marker.TalliesClosed = true;
				}
				Body.Obliterate();
			}
			catch (Exception ex)
			{
				Failure = "departure body destruction threw " + ex.GetType().Name;
				return false;
			}
			finally
			{
				survey?.ObserveCurrentTopology(Body);
			}
			if (GameObject.Validate(Body))
			{
				Failure = "departure body remained valid after destruction"; return false;
			}
			System.ResidentDeparture = KingdomResidentDepartureRules.Empty();
			KingdomLog.Log("emigrate: pop now " + System.Population + " origin="
				+ (Operation.Origin ?? "-") + " cause=" + (Operation.Cause ?? "drought"));
			return true;
		}
	}
}
