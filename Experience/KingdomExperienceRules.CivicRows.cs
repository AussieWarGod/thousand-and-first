using System;

namespace ThousandAndFirst
{
	public static partial class KingdomExperienceRules
	{
		public static bool TryGetOffice(KingdomExperienceLedger Ledger, string SettlementId,
			out KingdomCivicOfficeReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			int index = OfficeIndex(Ledger, SettlementId);
			if (index < 0) return true;
			Receipt = CopyOffice(Ledger.Offices[index]); return true;
		}

		public static bool TryGetRemembrance(KingdomExperienceLedger Ledger,
			string SettlementId, out KingdomRemembranceReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			int index = RemembranceIndex(Ledger, SettlementId);
			if (index < 0) return true;
			Receipt = CopyRemembrance(Ledger.Remembrances[index]); return true;
		}

		private static int OfficeIndex(KingdomExperienceLedger L, string SettlementId)
		{
			if (L?.Offices == null || SettlementId == null) return -1;
			for (int i = 0; i < L.Offices.Count; i++)
				if (L.Offices[i].SettlementId == SettlementId) return i;
			return -1;
		}

		private static int RemembranceIndex(KingdomExperienceLedger L, string SettlementId)
		{
			if (L?.Remembrances == null || SettlementId == null) return -1;
			for (int i = 0; i < L.Remembrances.Count; i++)
				if (L.Remembrances[i].SettlementId == SettlementId) return i;
			return -1;
		}

		private static bool PublishOffice(KingdomExperienceLedger Ledger, long ExpectedRevision,
			KingdomCivicOfficeReceipt Row, out string Failure)
		{
			Failure = null;
			if (ExpectedRevision != Ledger.Revision)
				return Fail("civic office revision conflict", out Failure);
			if (Ledger.Revision == long.MaxValue)
				return Fail("experience revision is exhausted", out Failure);
			KingdomExperienceLedger next = Clone(Ledger);
			int index = OfficeIndex(next, Row.SettlementId);
			if (index < 0)
			{
				if (next.Offices.Count >= MaxOfficeReceipts)
					return Fail("civic office capacity is full", out Failure);
				next.Offices.Add(CopyOffice(Row));
			}
			else next.Offices[index] = CopyOffice(Row);
			next.Offices.Sort((A, B) => string.CompareOrdinal(A.SettlementId, B.SettlementId));
			next.Revision++;
			if (!TryValidate(next, out Failure)) return false;
			Ledger.CopyFrom(next); return true;
		}

		private static bool PublishRemembrance(KingdomExperienceLedger Ledger,
			long ExpectedRevision, KingdomRemembranceReceipt Row, out string Failure)
		{
			Failure = null;
			if (ExpectedRevision != Ledger.Revision)
				return Fail("remembrance revision conflict", out Failure);
			if (Ledger.Revision == long.MaxValue)
				return Fail("experience revision is exhausted", out Failure);
			KingdomExperienceLedger next = Clone(Ledger);
			int index = RemembranceIndex(next, Row.SettlementId);
			if (index < 0)
			{
				if (next.Remembrances.Count >= MaxRemembranceReceipts)
					return Fail("remembrance capacity is full", out Failure);
				next.Remembrances.Add(CopyRemembrance(Row));
			}
			else next.Remembrances[index] = CopyRemembrance(Row);
			next.Remembrances.Sort((A, B) => string.CompareOrdinal(A.SettlementId,
				B.SettlementId));
			next.Revision++;
			if (!TryValidate(next, out Failure)) return false;
			Ledger.CopyFrom(next); return true;
		}

		internal static KingdomCivicOfficeReceipt CopyOffice(KingdomCivicOfficeReceipt R)
		{
			return R == null ? null : new KingdomCivicOfficeReceipt
			{
				Version = R.Version, Phase = R.Phase, VacancyCause = R.VacancyCause,
				Generation = R.Generation, SettlementId = R.SettlementId,
				SettlementName = R.SettlementName, WorkId = R.WorkId,
				HolderResidentId = R.HolderResidentId, HolderName = R.HolderName,
				HolderObjectId = R.HolderObjectId, OwnsRole = R.OwnsRole,
				PredecessorResidentId = R.PredecessorResidentId,
				PredecessorName = R.PredecessorName, ChangedTick = R.ChangedTick, Fault = R.Fault
			};
		}

		internal static KingdomRemembranceReceipt CopyRemembrance(KingdomRemembranceReceipt R)
		{
			return R == null ? null : new KingdomRemembranceReceipt
			{
				Version = R.Version, Phase = R.Phase, Generation = R.Generation,
				SettlementId = R.SettlementId, SettlementName = R.SettlementName,
				SubjectResidentId = R.SubjectResidentId, SubjectName = R.SubjectName,
				MournerResidentId = R.MournerResidentId, MournerName = R.MournerName,
				CarrierObjectId = R.CarrierObjectId, CarrierZoneId = R.CarrierZoneId,
				DecidedTick = R.DecidedTick, Fault = R.Fault
			};
		}
	}
}
