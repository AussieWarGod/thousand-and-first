using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomOfficeRuntime
	{
		internal static bool TryPrepareHolderDeparture(KingdomSystem System, GameObject Body,
			out KingdomCivicOfficeReceipt Prior, out string Failure)
		{
			Prior = null; Failure = null;
			if (System?.Experience == null || !GameObject.Validate(Body)) return false;
			int residentId = KingdomResidents.IdOf(Body);
			string objectId = Body.IDIfAssigned;
			KingdomCivicOfficeReceipt exact = null;
			for (int i = 0; i < System.Experience.Offices.Count; i++)
			{
				KingdomCivicOfficeReceipt row = System.Experience.Offices[i];
				if (row == null || row.Phase == KingdomCivicOfficePhase.None
					|| row.Phase == KingdomCivicOfficePhase.Vacant) continue;
				if (row.HolderResidentId != residentId && row.HolderObjectId != objectId)
					continue;
				if (exact != null || row.HolderResidentId != residentId
					|| row.HolderObjectId != objectId)
				{
					Failure = "office departure has divergent resident authority"; return false;
				}
				exact = row;
			}
			r_KingdomOfficeProjection marker = Body.GetPart<r_KingdomOfficeProjection>();
			if (exact == null)
			{
				if (marker == null) return true;
				Failure = "office departure found an orphan body projection"; return false;
			}
			if (!KingdomExperienceRules.ValidOffice(exact) || marker == null
				|| !marker.Matches(System, exact, Body))
			{
				Failure = "office departure lacks its exact receipt and projection"; return false;
			}
			if (exact.Phase == KingdomCivicOfficePhase.VacancyPrepared)
			{
				if (exact.VacancyCause == KingdomCivicOfficeVacancyCause.Departure) return true;
				Failure = "office is already closing for another witnessed cause"; return false;
			}
			if (exact.Phase != KingdomCivicOfficePhase.Held
				&& exact.Phase != KingdomCivicOfficePhase.AppointmentPrepared)
			{
				Failure = "office is not departure-preparable"; return false;
			}
			Prior = KingdomExperienceRules.CopyOffice(exact);
			return KingdomExperienceRules.TryPrepareOfficeVacancy(System.Experience,
				System.Experience.Revision, exact.SettlementId, residentId,
				KingdomCivicOfficeVacancyCause.Departure, Now(), out Failure);
		}

		internal static bool TryCancelHolderDeparture(KingdomSystem System, GameObject Body,
			KingdomCivicOfficeReceipt Prior, out string Failure)
		{
			Failure = null;
			if (Prior == null) return true;
			if (System?.Experience == null || !GameObject.Validate(Body)
				|| Prior.HolderResidentId != KingdomResidents.IdOf(Body)
				|| Prior.HolderObjectId != Body.IDIfAssigned)
			{
				Failure = "office departure rollback lost its exact body"; return false;
			}
			return KingdomExperienceRules.TryCancelOfficeDeparture(System.Experience,
				System.Experience.Revision, Prior, out Failure);
		}
	}
}
