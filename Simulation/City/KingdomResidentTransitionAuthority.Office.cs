using System;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomResidentTransitionAuthority
	{
		private static bool TryProjectOfficeClaims(KingdomSystem System, GameObject Body,
			int ResidentId, ref KingdomResidentTransitionClaim Claims)
		{
			if (System.Experience?.Offices == null) return false;
			r_KingdomOfficeProjection marker = Body.GetPart<r_KingdomOfficeProjection>();
			KingdomCivicOfficeReceipt exact = null;
			int claimed = 0;
			for (int i = 0; i < System.Experience.Offices.Count; i++)
			{
				KingdomCivicOfficeReceipt row = System.Experience.Offices[i];
				if (!OfficeClaimsResident(row) || row.HolderResidentId != ResidentId
					&& !string.Equals(row.HolderObjectId, Body.IDIfAssigned,
						StringComparison.Ordinal)) continue;
				claimed++;
				Claims |= KingdomResidentTransitionClaim.CivicOffice;
				if (AccessionClosableOffice(row) && row.HolderResidentId == ResidentId
					&& string.Equals(row.HolderObjectId, Body.IDIfAssigned,
						StringComparison.Ordinal)) exact = row;
			}
			if (marker == null && claimed == 0) return true;
			if (claimed != 1 || exact == null || exact.VacancyCause
				== KingdomCivicOfficeVacancyCause.Death
				|| marker != null && !marker.Matches(System, exact, Body))
				Claims |= KingdomResidentTransitionClaim.AuthorityUnproved;
			else if (exact.Phase == KingdomCivicOfficePhase.VacancyPrepared
				&& exact.VacancyCause == KingdomCivicOfficeVacancyCause.Departure
				&& marker != null)
				Claims |= KingdomResidentTransitionClaim.OfficeDeparturePrepared;
			else if (exact.Phase == KingdomCivicOfficePhase.VacancyPrepared)
				Claims |= KingdomResidentTransitionClaim.AuthorityUnproved;
			return true;
		}

		private static bool OfficeClaimsResident(KingdomCivicOfficeReceipt Receipt)
		{
			return Receipt != null && Receipt.Phase != KingdomCivicOfficePhase.None
				&& Receipt.Phase != KingdomCivicOfficePhase.Vacant;
		}

		private static bool AccessionClosableOffice(KingdomCivicOfficeReceipt Receipt)
		{
			return Receipt != null && (Receipt.Phase == KingdomCivicOfficePhase.Held
				|| Receipt.Phase == KingdomCivicOfficePhase.AppointmentPrepared
				|| Receipt.Phase == KingdomCivicOfficePhase.VacancyPrepared);
		}
	}
}
