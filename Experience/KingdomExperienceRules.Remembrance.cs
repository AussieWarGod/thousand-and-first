namespace ThousandAndFirst
{
	public static partial class KingdomExperienceRules
	{
		public static bool TryCreateRemembranceEligibility(KingdomExperienceLedger Ledger,
			long ExpectedRevision, string SettlementId, string SettlementName,
			int SubjectResidentId, string SubjectName, long WitnessedTick, out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			if (!Ledger.IdentityBound)
				return Fail("remembrance ledger is not realm-bound", out Failure);
			int index = RemembranceIndex(Ledger, SettlementId);
			if (index >= 0)
			{
				KingdomRemembranceReceipt existing = Ledger.Remembrances[index];
				if (existing.Phase == KingdomRemembrancePhase.Eligible
					&& existing.SettlementName == SettlementName
					&& existing.SubjectResidentId == SubjectResidentId
					&& existing.SubjectName == SubjectName
					&& existing.DecidedTick == WitnessedTick) return true;
				return Fail("this settlement already owns one remembrance opportunity",
					out Failure);
			}
			if (ExpectedRevision != Ledger.Revision || WitnessedTick < 0L)
				return Fail("remembrance witness evidence changed", out Failure);
			KingdomRemembranceReceipt row = new KingdomRemembranceReceipt
			{
				Phase = KingdomRemembrancePhase.Eligible, Generation = 1,
				SettlementId = SettlementId, SettlementName = SettlementName,
				SubjectResidentId = SubjectResidentId, SubjectName = SubjectName,
				DecidedTick = WitnessedTick
			};
			if (!ValidRemembrance(row))
				return Fail("remembrance eligibility evidence is invalid", out Failure);
			return PublishRemembrance(Ledger, ExpectedRevision, row, out Failure);
		}

		public static bool TryPrepareRemembranceProjection(KingdomExperienceLedger Ledger,
			long ExpectedRevision, string SettlementId, string SettlementName,
			int SubjectResidentId, string SubjectName, int MournerResidentId, string MournerName,
			string CarrierObjectId, string CarrierZoneId, long Tick, out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			if (!Ledger.IdentityBound)
				return Fail("remembrance ledger is not realm-bound", out Failure);
			int index = RemembranceIndex(Ledger, SettlementId);
			KingdomRemembranceReceipt prior = index < 0 ? null : Ledger.Remembrances[index];
			if (SameRemembrance(prior, SettlementId, SettlementName, SubjectResidentId,
				SubjectName, MournerResidentId, MournerName, CarrierObjectId, CarrierZoneId,
				Tick)) return true;
			if (prior == null || prior.Phase != KingdomRemembrancePhase.Eligible
				|| prior.SettlementName != SettlementName
				|| prior.SubjectResidentId != SubjectResidentId
				|| prior.SubjectName != SubjectName || Tick < prior.DecidedTick)
				return Fail("no exact witnessed remembrance is eligible", out Failure);
			KingdomRemembranceReceipt row = CopyRemembrance(prior);
			row.Phase = KingdomRemembrancePhase.ProjectionPrepared;
			row.MournerResidentId = MournerResidentId; row.MournerName = MournerName;
			row.CarrierObjectId = CarrierObjectId; row.CarrierZoneId = CarrierZoneId;
			if (!ValidRemembrance(row))
				return Fail("remembrance projection evidence is invalid", out Failure);
			return PublishRemembrance(Ledger, ExpectedRevision, row, out Failure);
		}

		public static bool TryDeclineRemembrance(KingdomExperienceLedger Ledger,
			long ExpectedRevision, string SettlementId, string SettlementName,
			int SubjectResidentId, string SubjectName, int MournerResidentId, string MournerName,
			long Tick, out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			if (!Ledger.IdentityBound)
				return Fail("remembrance ledger is not realm-bound", out Failure);
			int index = RemembranceIndex(Ledger, SettlementId);
			if (index < 0) return Fail("no exact witnessed remembrance is eligible", out Failure);
			KingdomRemembranceReceipt existing = Ledger.Remembrances[index];
			if (existing.Phase == KingdomRemembrancePhase.Declined
				&& existing.SettlementName == SettlementName
				&& existing.SubjectResidentId == SubjectResidentId
				&& existing.SubjectName == SubjectName
				&& existing.MournerResidentId == MournerResidentId
				&& existing.MournerName == MournerName
				&& Tick >= existing.DecidedTick) return true;
			if (existing.Phase != KingdomRemembrancePhase.Eligible
				|| existing.SettlementName != SettlementName
				|| existing.SubjectResidentId != SubjectResidentId
				|| existing.SubjectName != SubjectName || Tick < existing.DecidedTick)
				return Fail("no exact witnessed remembrance is eligible", out Failure);
			KingdomRemembranceReceipt row = CopyRemembrance(existing);
			row.Phase = KingdomRemembrancePhase.Declined;
			row.MournerResidentId = MournerResidentId; row.MournerName = MournerName;
			if (!ValidRemembrance(row))
				return Fail("remembrance decline evidence is invalid", out Failure);
			return PublishRemembrance(Ledger, ExpectedRevision, row, out Failure);
		}

		public static bool TryCompleteRemembranceProjection(KingdomExperienceLedger Ledger,
			long ExpectedRevision, string SettlementId, int Generation, out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			int index = RemembranceIndex(Ledger, SettlementId);
			if (index < 0 || Ledger.Remembrances[index].Generation != Generation)
				return Fail("prepared remembrance projection is absent", out Failure);
			KingdomRemembranceReceipt row = Ledger.Remembrances[index];
			if (row.Phase == KingdomRemembrancePhase.Projected) return true;
			if (row.Phase != KingdomRemembrancePhase.ProjectionPrepared)
				return Fail("remembrance is not awaiting projection", out Failure);
			row = CopyRemembrance(row); row.Phase = KingdomRemembrancePhase.Projected;
			return PublishRemembrance(Ledger, ExpectedRevision, row, out Failure);
		}

		public static bool TryMarkRemembranceLost(KingdomExperienceLedger Ledger,
			long ExpectedRevision, string SettlementId, string CarrierObjectId,
			out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			int index = RemembranceIndex(Ledger, SettlementId);
			if (index < 0) return Fail("projected remembrance is absent", out Failure);
			KingdomRemembranceReceipt row = Ledger.Remembrances[index];
			if (row.Phase == KingdomRemembrancePhase.Lost
				&& row.CarrierObjectId == CarrierObjectId) return true;
			if ((row.Phase != KingdomRemembrancePhase.Projected
				&& row.Phase != KingdomRemembrancePhase.ProjectionPrepared)
				|| row.CarrierObjectId != CarrierObjectId)
				return Fail("remembrance carrier evidence changed", out Failure);
			row = CopyRemembrance(row); row.Phase = KingdomRemembrancePhase.Lost;
			return PublishRemembrance(Ledger, ExpectedRevision, row, out Failure);
		}

		private static bool SameRemembrance(KingdomRemembranceReceipt R,
			string SettlementId, string SettlementName, int SubjectResidentId,
			string SubjectName, int MournerResidentId, string MournerName,
			string CarrierObjectId, string CarrierZoneId, long Tick)
		{
			return R != null && (R.Phase == KingdomRemembrancePhase.ProjectionPrepared
				|| R.Phase == KingdomRemembrancePhase.Projected)
				&& R.SettlementId == SettlementId && R.SettlementName == SettlementName
				&& R.SubjectResidentId == SubjectResidentId && R.SubjectName == SubjectName
				&& R.MournerResidentId == MournerResidentId && R.MournerName == MournerName
				&& R.CarrierObjectId == CarrierObjectId && R.CarrierZoneId == CarrierZoneId
				&& Tick >= R.DecidedTick;
		}
	}
}
