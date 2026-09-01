using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Exact reversible role preparation owned by one destructive resident transition.
	/// Empty prior snapshots mean the role was absent or already prepared by an earlier retry.</summary>
	internal readonly struct KingdomResidentDeparturePreparation
	{
		internal readonly KingdomNamedCookReceipt PriorCook;
		internal readonly KingdomCivicOfficeReceipt PriorOffice;
		internal readonly KingdomPolityResidentTransitionPreparation PriorPolity;

		private KingdomResidentDeparturePreparation(KingdomNamedCookReceipt priorCook,
			KingdomCivicOfficeReceipt priorOffice,
			KingdomPolityResidentTransitionPreparation priorPolity)
		{
			PriorCook = priorCook; PriorOffice = priorOffice; PriorPolity = priorPolity;
		}

		internal static bool TryPrepare(KingdomSystem System, GameObject Body,
			KingdomResidentDestructionAuthorization Authorization,
			KingdomResidentDepartureOperation Operation,
			out KingdomResidentDeparturePreparation Preparation, out string Failure)
		{
			Preparation = default(KingdomResidentDeparturePreparation); Failure = null;
			int residentId = KingdomResidents.IdOf(Body);
			if (!KingdomResidentTransitionAuthority.CanPrepareJournaledRoles(
				System, Body, Operation, Authorization, RolesPrepared: false))
			{
				Failure = "resident destruction preflight found an unclosable authority";
				return false;
			}
			if (!KingdomNamedCook.PrepareCookLoss(System, Body,
				KingdomNamedCookVacancyCause.Departure,
				out KingdomNamedCookReceipt priorCook, out Failure)) return false;
			if (!KingdomOfficeRuntime.TryPrepareHolderDeparture(System, Body,
				out KingdomCivicOfficeReceipt priorOffice, out Failure))
			{
				RollbackCook(System, Body, priorCook); return false;
			}
			if (!KingdomPolityResidentTransition.TryConclude(System, Body, residentId,
				KingdomPolityResidentTransitionCause.Departure,
				out KingdomPolityResidentTransitionPreparation priorPolity, out Failure))
			{
				RollbackRoles(System, Body, priorCook, priorOffice); return false;
			}
			Preparation = new KingdomResidentDeparturePreparation(priorCook, priorOffice,
				priorPolity);
			if (KingdomResidentTransitionAuthority.CanPrepareJournaledRoles(
				System, Body, Operation, Authorization, RolesPrepared: true)) return true;
			Failure = "resident authority changed after role preparation";
			Preparation.TryRollback(System, Body, out string _);
			Preparation = default(KingdomResidentDeparturePreparation); return false;
		}

		internal bool TryRollback(KingdomSystem System, GameObject Body, out string Failure)
		{
			Failure = null;
			bool polity = KingdomPolityResidentTransition.TryRollback(System, PriorPolity,
				out string polityFailure);
			bool office = KingdomOfficeRuntime.TryCancelHolderDeparture(System, Body,
				PriorOffice, out string officeFailure);
			bool cook = KingdomNamedCook.CancelPreparedCookLoss(System, Body, PriorCook,
				KingdomNamedCookVacancyCause.Departure, out string cookFailure);
			if (polity && office && cook) return true;
			Failure = !polity ? polityFailure : !office ? officeFailure : cookFailure;
			return false;
		}

		private static void RollbackRoles(KingdomSystem System, GameObject Body,
			KingdomNamedCookReceipt PriorCook, KingdomCivicOfficeReceipt PriorOffice)
		{
			if (!KingdomOfficeRuntime.TryCancelHolderDeparture(System, Body, PriorOffice,
				out string officeFailure))
				KingdomLog.Log("office: role-preflight rollback waits ("
					+ (officeFailure ?? "unknown failure") + ")");
			RollbackCook(System, Body, PriorCook);
		}

		private static void RollbackCook(KingdomSystem System, GameObject Body,
			KingdomNamedCookReceipt Prior)
		{
			if (!KingdomNamedCook.CancelPreparedCookLoss(System, Body, Prior,
				KingdomNamedCookVacancyCause.Departure, out string failure))
				KingdomLog.Log("named cook: role-preflight rollback waits ("
					+ (failure ?? "unknown failure") + ")");
		}
	}
}
