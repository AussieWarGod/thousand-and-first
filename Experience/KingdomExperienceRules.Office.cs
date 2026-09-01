using System;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomExperienceRules
	{
		public static bool TryPrepareOfficeAppointment(KingdomExperienceLedger Ledger,
			long ExpectedRevision, string SettlementId, string SettlementName, int WorkId,
			int ResidentId, string ResidentName, string BodyObjectId, bool OwnsRole, long Tick,
			out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			if (!Ledger.IdentityBound)
				return Fail("civic office ledger is not realm-bound", out Failure);
			int index = OfficeIndex(Ledger, SettlementId);
			KingdomCivicOfficeReceipt prior = index < 0 ? null : Ledger.Offices[index];
			if (SameAppointment(prior, SettlementId, SettlementName, WorkId, ResidentId,
				ResidentName, BodyObjectId, OwnsRole)) return true;
			if (prior != null && prior.Phase != KingdomCivicOfficePhase.Vacant)
				return Fail("civic office is not vacant", out Failure);
			if (ExpectedRevision != Ledger.Revision || Tick < 0L
				|| prior != null && Tick < prior.ChangedTick)
				return Fail("civic office preparation evidence changed", out Failure);
			if (prior != null && prior.Generation == int.MaxValue)
				return Fail("civic office generation is exhausted", out Failure);
			int generation = prior == null ? 1 : prior.Generation + 1;
			KingdomCivicOfficeReceipt row = new KingdomCivicOfficeReceipt
			{
				Phase = KingdomCivicOfficePhase.AppointmentPrepared,
				Generation = generation, SettlementId = SettlementId,
				SettlementName = SettlementName, WorkId = WorkId,
				HolderResidentId = ResidentId, HolderName = ResidentName,
				HolderObjectId = BodyObjectId, OwnsRole = OwnsRole, ChangedTick = Tick
			};
			if (!ValidOffice(row)) return Fail("civic office appointment is invalid", out Failure);
			return PublishOffice(Ledger, ExpectedRevision, row, out Failure);
		}

		public static bool TryCompleteOfficeAppointment(KingdomExperienceLedger Ledger,
			long ExpectedRevision, string SettlementId, int Generation, out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			int index = OfficeIndex(Ledger, SettlementId);
			if (index < 0 || Ledger.Offices[index].Generation != Generation)
				return Fail("prepared civic office is absent", out Failure);
			KingdomCivicOfficeReceipt row = Ledger.Offices[index];
			if (row.Phase == KingdomCivicOfficePhase.Held) return true;
			if (row.Phase != KingdomCivicOfficePhase.AppointmentPrepared)
				return Fail("civic office is not awaiting projection", out Failure);
			row = CopyOffice(row); row.Phase = KingdomCivicOfficePhase.Held;
			return PublishOffice(Ledger, ExpectedRevision, row, out Failure);
		}

		public static bool TryPrepareOfficeVacancy(KingdomExperienceLedger Ledger,
			long ExpectedRevision, string SettlementId, int HolderResidentId,
			KingdomCivicOfficeVacancyCause Cause, long Tick, out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			int index = OfficeIndex(Ledger, SettlementId);
			if (index < 0) return Fail("held civic office is absent", out Failure);
			KingdomCivicOfficeReceipt row = Ledger.Offices[index];
			if (row.Phase == KingdomCivicOfficePhase.Vacant
				&& row.PredecessorResidentId == HolderResidentId && row.VacancyCause == Cause)
				return true;
			if (row.Phase == KingdomCivicOfficePhase.VacancyPrepared
				&& row.HolderResidentId == HolderResidentId && row.VacancyCause == Cause)
				return true;
			if ((row.Phase != KingdomCivicOfficePhase.Held
				&& row.Phase != KingdomCivicOfficePhase.AppointmentPrepared)
				|| row.HolderResidentId != HolderResidentId
				|| Cause == KingdomCivicOfficeVacancyCause.None || Tick < row.ChangedTick)
				return Fail("civic office vacancy evidence changed", out Failure);
			row = CopyOffice(row); row.Phase = KingdomCivicOfficePhase.VacancyPrepared;
			row.VacancyCause = Cause; row.PredecessorResidentId = row.HolderResidentId;
			row.PredecessorName = row.HolderName; row.ChangedTick = Tick;
			return PublishOffice(Ledger, ExpectedRevision, row, out Failure);
		}

		public static bool TryCompleteOfficeVacancy(KingdomExperienceLedger Ledger,
			long ExpectedRevision, string SettlementId, int Generation, out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			int index = OfficeIndex(Ledger, SettlementId);
			if (index < 0 || Ledger.Offices[index].Generation != Generation)
				return Fail("prepared civic office vacancy is absent", out Failure);
			KingdomCivicOfficeReceipt row = Ledger.Offices[index];
			if (row.Phase == KingdomCivicOfficePhase.Vacant) return true;
			if (row.Phase != KingdomCivicOfficePhase.VacancyPrepared)
				return Fail("civic office is not awaiting title removal", out Failure);
			if (row.VacancyCause == KingdomCivicOfficeVacancyCause.Death)
				return Fail("civic office death requires its exact terminal resident row",
					out Failure);
			return CompleteOfficeVacancy(Ledger, ExpectedRevision, row, out Failure);
		}

		/// <summary>Authorizes a witnessed death vacancy independently of physical title cleanup.
		/// The exact same-settlement resident row is mandatory; body absence, an <c>Abroad</c> row,
		/// or any non-death vacancy cause can never become death evidence.</summary>
		internal static bool CanCompleteOfficeDeathVacancy(KingdomExperienceLedger Ledger,
			long ExpectedRevision, string SettlementId, int Generation, KingdomCityState State,
			out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			int index = OfficeIndex(Ledger, SettlementId);
			if (index < 0 || Ledger.Offices[index].Generation != Generation)
				return Fail("prepared civic office death vacancy is absent", out Failure);
			KingdomCivicOfficeReceipt row = Ledger.Offices[index];
			int residentId = row.Phase == KingdomCivicOfficePhase.Vacant
				? row.PredecessorResidentId : row.HolderResidentId;
			if (State == null || State.SettlementId != SettlementId
				|| !State.TryResidentIndex(residentId, out int residentIndex)
				|| !State.TryResident(residentIndex, out KingdomResidentRow resident)
				|| resident.ResidentId != residentId
				|| resident.Standing != KingdomResidentStanding.Dead
				|| row.VacancyCause != KingdomCivicOfficeVacancyCause.Death)
				return Fail("civic office death lacks its exact terminal resident row", out Failure);
			if (row.Phase == KingdomCivicOfficePhase.Vacant) return true;
			if (row.Phase != KingdomCivicOfficePhase.VacancyPrepared
				|| row.PredecessorResidentId != resident.ResidentId
				|| ExpectedRevision != Ledger.Revision)
				return Fail("civic office is not awaiting exact death closure", out Failure);
			return true;
		}

		internal static bool TryCompleteOfficeDeathVacancy(KingdomExperienceLedger Ledger,
			long ExpectedRevision, string SettlementId, int Generation, KingdomCityState State,
			out string Failure)
		{
			if (!CanCompleteOfficeDeathVacancy(Ledger, ExpectedRevision, SettlementId,
				Generation, State, out Failure)) return false;
			KingdomCivicOfficeReceipt row = Ledger.Offices[OfficeIndex(Ledger, SettlementId)];
			if (row.Phase == KingdomCivicOfficePhase.Vacant) return true;
			return CompleteOfficeVacancy(Ledger, ExpectedRevision, row, out Failure);
		}

		private static bool CompleteOfficeVacancy(KingdomExperienceLedger Ledger,
			long ExpectedRevision, KingdomCivicOfficeReceipt Row, out string Failure)
		{
			KingdomCivicOfficeReceipt row = CopyOffice(Row);
			row.Phase = KingdomCivicOfficePhase.Vacant;
			row.HolderResidentId = 0; row.HolderName = null; row.HolderObjectId = null;
			row.OwnsRole = false;
			return PublishOffice(Ledger, ExpectedRevision, row, out Failure);
		}

		private static bool SameAppointment(KingdomCivicOfficeReceipt R, string SettlementId,
			string SettlementName, int WorkId, int ResidentId, string ResidentName,
			string BodyObjectId, bool OwnsRole)
		{
			return R != null && (R.Phase == KingdomCivicOfficePhase.AppointmentPrepared
				|| R.Phase == KingdomCivicOfficePhase.Held) && R.SettlementId == SettlementId
				&& R.SettlementName == SettlementName && R.WorkId == WorkId
				&& R.HolderResidentId == ResidentId && R.HolderName == ResidentName
				&& R.HolderObjectId == BodyObjectId && R.OwnsRole == OwnsRole;
		}
	}
}
