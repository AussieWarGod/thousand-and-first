using System;

namespace ThousandAndFirst
{
	public static partial class KingdomExperienceRules
	{
		private static bool ValidateCivicRows(KingdomExperienceLedger Ledger,
			out string Failure)
		{
			Failure = null;
			string prior = null;
			for (int i = 0; i < Ledger.Offices.Count; i++)
			{
				KingdomCivicOfficeReceipt row = Ledger.Offices[i];
				if (!ValidOffice(row) || !After(prior, row.SettlementId))
					return Fail("civic office receipt is invalid", out Failure);
				prior = row.SettlementId;
			}
			prior = null;
			for (int i = 0; i < Ledger.Remembrances.Count; i++)
			{
				KingdomRemembranceReceipt row = Ledger.Remembrances[i];
				if (!ValidRemembrance(row) || !After(prior, row.SettlementId))
					return Fail("remembrance receipt is invalid", out Failure);
				prior = row.SettlementId;
			}
			for (int i = 0; i < Ledger.Offices.Count; i++)
			{
				KingdomCivicOfficeReceipt office = Ledger.Offices[i];
				for (int j = 0; j < i; j++)
				{
					KingdomCivicOfficeReceipt other = Ledger.Offices[j];
					if (!string.IsNullOrEmpty(office.HolderObjectId)
						&& office.HolderObjectId == other.HolderObjectId)
						return Fail("one body is claimed by two civic offices", out Failure);
					if (office.HolderResidentId > 0
						&& office.HolderResidentId == other.HolderResidentId)
						return Fail("one resident is claimed by two civic offices", out Failure);
				}
				for (int j = 0; j < Ledger.Remembrances.Count; j++)
				{
					KingdomRemembranceReceipt remembrance = Ledger.Remembrances[j];
					if (!string.IsNullOrEmpty(office.HolderObjectId)
						&& office.HolderObjectId == remembrance.CarrierObjectId)
						return Fail("one object is claimed by office and remembrance", out Failure);
					if (office.HolderResidentId > 0
						&& office.HolderResidentId == remembrance.SubjectResidentId)
						return Fail("one resident is both office holder and remembrance subject",
							out Failure);
				}
			}
			for (int i = 0; i < Ledger.Remembrances.Count; i++)
				for (int j = 0; j < i; j++)
				{
					KingdomRemembranceReceipt row = Ledger.Remembrances[i];
					KingdomRemembranceReceipt other = Ledger.Remembrances[j];
					if (row.SubjectResidentId == other.SubjectResidentId)
						return Fail("one death has two remembrance receipts", out Failure);
					if (!string.IsNullOrEmpty(row.CarrierObjectId)
						&& row.CarrierObjectId == other.CarrierObjectId)
						return Fail("one object has two remembrance receipts", out Failure);
				}
			return true;
		}

		private static bool ValidateVoices(KingdomExperienceLedger Ledger,
			out string Failure)
		{
			Failure = null;
			KingdomCivicVoiceFixture prior = KingdomCivicVoiceFixture.None;
			for (int i = 0; i < Ledger.Voices.Count; i++)
			{
				KingdomCivicVoiceReceipt row = Ledger.Voices[i];
				if (!KingdomCivicVoiceRules.Valid(row) || row.Fixture <= prior
					|| !ReceiptOptionValid(Ledger, KingdomExperienceOptionKind.CivicStory,
						row.CauseTick, row.CauseTick, row.EnableEpoch))
					return Fail("civic voice receipt is invalid", out Failure);
				for (int j = 0; j < i; j++)
					if (Ledger.Voices[j].SourceId == row.SourceId)
						return Fail("one civic source has two voice receipts", out Failure);
				prior = row.Fixture;
			}
			return true;
		}

		internal static bool ValidOffice(KingdomCivicOfficeReceipt Row)
		{
			if (Row == null || Row.Version != KingdomCivicOfficeReceipt.CurrentVersion
				|| Row.Generation < 1 || Row.ChangedTick < 0L
				|| !TypedId(Row.SettlementId, "taf:settlement:")
				|| !CivicText(Row.SettlementId, true)
				|| !CivicText(Row.SettlementName, true) || Row.WorkId <= 0
				|| !Enum.IsDefined(typeof(KingdomCivicOfficePhase), Row.Phase)
				|| !Enum.IsDefined(typeof(KingdomCivicOfficeVacancyCause), Row.VacancyCause))
				return false;
			if (Row.Phase == KingdomCivicOfficePhase.Quarantined)
				return CivicText(Row.Fault, true) && BoundedOfficeResidue(Row);
			if (!string.IsNullOrEmpty(Row.Fault)) return false;
			if (Row.Phase == KingdomCivicOfficePhase.Vacant)
				return Row.VacancyCause != KingdomCivicOfficeVacancyCause.None
					&& EmptyHolder(Row) && ValidPredecessor(Row);
			if (Row.Phase != KingdomCivicOfficePhase.AppointmentPrepared
				&& Row.Phase != KingdomCivicOfficePhase.Held
				&& Row.Phase != KingdomCivicOfficePhase.VacancyPrepared) return false;
			if (Row.HolderResidentId <= 0 || !CivicText(Row.HolderName, true)
				|| !CivicText(Row.HolderObjectId, true)) return false;
			if (Row.Phase == KingdomCivicOfficePhase.VacancyPrepared)
				return Row.VacancyCause != KingdomCivicOfficeVacancyCause.None
					&& Row.PredecessorResidentId == Row.HolderResidentId
					&& Row.PredecessorName == Row.HolderName;
			return Row.VacancyCause == KingdomCivicOfficeVacancyCause.None
				&& Row.PredecessorResidentId == 0 && string.IsNullOrEmpty(Row.PredecessorName);
		}

		internal static bool ValidRemembrance(KingdomRemembranceReceipt Row)
		{
			if (Row == null || Row.Version != KingdomRemembranceReceipt.CurrentVersion
				|| Row.Generation < 1 || Row.DecidedTick < 0L
				|| !TypedId(Row.SettlementId, "taf:settlement:")
				|| !CivicText(Row.SettlementId, true)
				|| !CivicText(Row.SettlementName, true) || Row.SubjectResidentId <= 0
				|| !CivicText(Row.SubjectName, true)
				|| !Enum.IsDefined(typeof(KingdomRemembrancePhase), Row.Phase)) return false;
			bool mourner = Row.MournerResidentId > 0 && CivicText(Row.MournerName, true);
			bool noMourner = Row.MournerResidentId == 0 && string.IsNullOrEmpty(Row.MournerName);
			if (Row.Phase == KingdomRemembrancePhase.Quarantined)
				return CivicText(Row.Fault, true) && (mourner || noMourner)
					&& CivicText(Row.CarrierObjectId, false)
					&& CivicText(Row.CarrierZoneId, false);
			if (!string.IsNullOrEmpty(Row.Fault)) return false;
			if (Row.Phase == KingdomRemembrancePhase.Eligible)
				return noMourner && string.IsNullOrEmpty(Row.CarrierObjectId)
					&& string.IsNullOrEmpty(Row.CarrierZoneId);
			bool carrier = Row.Phase == KingdomRemembrancePhase.ProjectionPrepared
				|| Row.Phase == KingdomRemembrancePhase.Projected
				|| Row.Phase == KingdomRemembrancePhase.Lost;
			return mourner && (carrier
				? CivicText(Row.CarrierObjectId, true) && CivicText(Row.CarrierZoneId, true)
				: Row.Phase == KingdomRemembrancePhase.Declined
					&& string.IsNullOrEmpty(Row.CarrierObjectId)
					&& string.IsNullOrEmpty(Row.CarrierZoneId));
		}

		private static bool EmptyHolder(KingdomCivicOfficeReceipt Row)
		{
			return Row.HolderResidentId == 0 && string.IsNullOrEmpty(Row.HolderName)
				&& string.IsNullOrEmpty(Row.HolderObjectId) && !Row.OwnsRole;
		}

		private static bool ValidPredecessor(KingdomCivicOfficeReceipt Row)
		{
			return Row.PredecessorResidentId > 0 && CivicText(Row.PredecessorName, true);
		}

		private static bool BoundedOfficeResidue(KingdomCivicOfficeReceipt Row)
		{
			return CivicText(Row.HolderName, false) && CivicText(Row.HolderObjectId, false)
				&& CivicText(Row.PredecessorName, false);
		}

		internal static bool CivicText(string Value, bool Required)
		{
			if (Value == null) return !Required;
			if (Required && Value.Length == 0) return false;
			try { if (StrictUtf8.GetByteCount(Value) > MaxCivicTextBytes) return false; }
			catch (System.Text.EncoderFallbackException) { return false; }
			for (int i = 0; i < Value.Length; i++) if (char.IsControl(Value[i])) return false;
			return true;
		}
	}
}
