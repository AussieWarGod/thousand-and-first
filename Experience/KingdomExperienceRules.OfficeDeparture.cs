namespace ThousandAndFirst
{
	public static partial class KingdomExperienceRules
	{
		/// <summary>CAS-restores the exact held/appointment row when its owning departure did
		/// not publish resident carriers. It cannot reopen any independently advanced vacancy.</summary>
		internal static bool TryCancelOfficeDeparture(KingdomExperienceLedger Ledger,
			long ExpectedRevision, KingdomCivicOfficeReceipt Prior, out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure) || !ValidOffice(Prior)
				|| Prior.Phase != KingdomCivicOfficePhase.Held
					&& Prior.Phase != KingdomCivicOfficePhase.AppointmentPrepared)
				return Fail(Failure ?? "office departure rollback lacks its exact prior row",
					out Failure);
			int index = OfficeIndex(Ledger, Prior.SettlementId);
			if (index < 0) return Fail("prepared office departure is absent", out Failure);
			KingdomCivicOfficeReceipt current = Ledger.Offices[index];
			if (ExpectedRevision != Ledger.Revision
				|| current.Phase != KingdomCivicOfficePhase.VacancyPrepared
				|| current.VacancyCause != KingdomCivicOfficeVacancyCause.Departure
				|| !SameDepartureAppointment(current, Prior))
				return Fail("prepared office departure lost its rollback CAS", out Failure);
			return PublishOffice(Ledger, ExpectedRevision, CopyOffice(Prior), out Failure);
		}

		private static bool SameDepartureAppointment(KingdomCivicOfficeReceipt A,
			KingdomCivicOfficeReceipt B)
		{
			return A != null && B != null && A.Version == B.Version
				&& A.Generation == B.Generation && A.SettlementId == B.SettlementId
				&& A.SettlementName == B.SettlementName && A.WorkId == B.WorkId
				&& A.HolderResidentId == B.HolderResidentId
				&& A.HolderName == B.HolderName
				&& A.HolderObjectId == B.HolderObjectId && A.OwnsRole == B.OwnsRole
				&& A.PredecessorResidentId == B.HolderResidentId
				&& A.PredecessorName == B.HolderName && string.IsNullOrEmpty(A.Fault)
				&& string.IsNullOrEmpty(B.Fault);
		}
	}
}
