using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static class KingdomPolityAdmissionTransferRules
	{
		internal const string TransferWitnessKind = "admission-transfer";

		internal static bool ExactMarker(r_KingdomPolityAdmissionBody M,
			KingdomPolityAdmissionReceipt R, string ProjectionId)
		{
			return M != null && !M.Inert && R != null &&
				R.Phase == KingdomPolityAdmissionReceiptPhase.Committed &&
				M.Version == r_KingdomPolityAdmissionBody.CurrentVersion &&
				M.RealmId == R.RealmId && M.SettlementId == R.TargetSettlementId &&
				M.HandoffId == R.HandoffId && M.CohortId == R.CohortId &&
				M.MemberId == R.MemberId && M.ProjectionId == ProjectionId &&
				M.SourceZoneId == R.SourceZoneId && M.BodyObjectId == R.SourceObjectId &&
				M.ResidentId == R.ResidentId && M.AdmissionReceiptId == R.ReceiptId &&
				M.BodyReceiptId == R.BodyReceiptId;
		}

		internal static bool ExactOperationMarker(r_KingdomPolityAdmissionBody M,
			KingdomResidentAdmissionOperation O)
		{
			return M != null && !M.Inert && O != null &&
				M.Version == r_KingdomPolityAdmissionBody.CurrentVersion &&
				M.RealmId == O.RealmId && M.SettlementId == O.SettlementId &&
				M.HandoffId == O.HandoffId && M.CohortId == O.CohortId &&
				M.MemberId == O.MemberId && M.ProjectionId == O.ProjectionId &&
				M.SourceZoneId == O.SourceZoneId && M.BodyObjectId == O.BodyObjectId &&
				M.ResidentId == O.ResidentId;
		}
	}
}
