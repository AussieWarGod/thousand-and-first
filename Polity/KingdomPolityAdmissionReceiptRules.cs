using System.Globalization;

namespace ThousandAndFirst
{
	internal static class KingdomPolityAdmissionReceiptRules
	{
		internal static string ReceiptId(string OperationId, string HandoffId)
		{
			return KingdomPolityRules.ActivationId("taf:admission-receipt:v1:",
				"polity-admission-receipt-v1", OperationId, HandoffId);
		}

		internal static string BodyReceipt(KingdomPolityAdmissionReceipt R)
		{
			return KingdomPolityRules.ActivationId("taf:admission-body:v1:",
				"polity-admission-body-v1", R.ReceiptId, R.SourceObjectId,
				R.ResidentId.ToString(CultureInfo.InvariantCulture));
		}

		internal static string Digest(KingdomPolityAdmissionReceipt R)
		{
			return KingdomPolityRules.ActivationDigest("polity-admission-receipt-digest-v1",
				R.Version.ToString(CultureInfo.InvariantCulture), R.ReceiptId ?? "",
				R.OperationId ?? "", R.HandoffId ?? "", R.RealmId ?? "",
				R.SourcePolityId ?? "", R.CohortId ?? "", R.MemberId ?? "",
				R.TargetSettlementId ?? "", R.SourceObjectId ?? "", R.SourceZoneId ?? "",
				((byte)R.Phase).ToString(CultureInfo.InvariantCulture),
				R.PreparedTick.ToString(CultureInfo.InvariantCulture),
				R.DecidedTick.ToString(CultureInfo.InvariantCulture),
				R.ResidentId.ToString(CultureInfo.InvariantCulture), R.BodyReceiptId ?? "",
				R.Fault ?? "");
		}

		internal static bool Valid(KingdomPolityAdmissionReceipt R,
			KingdomPolityAdmissionHandoff H)
		{
			if (R == null || H == null || R.Version !=
				KingdomPolityAdmissionReceipt.CurrentVersion ||
				!KingdomPolityRules.TypedId(R.ReceiptId, "taf:admission-receipt:v1:") ||
				!KingdomPolityRules.TypedId(R.OperationId, "taf:resident-admission:v1:") ||
				R.ReceiptId != ReceiptId(R.OperationId, R.HandoffId) ||
				R.HandoffId != H.HandoffId || R.RealmId != H.RealmId ||
				R.SourcePolityId != H.PolityId || R.CohortId != H.CohortId ||
				R.MemberId != H.MemberId || R.TargetSettlementId != H.TargetSettlementId ||
				R.SourceObjectId != H.SourceObjectId || R.SourceZoneId != H.SourceZoneId ||
				R.PreparedTick < H.DecidedTick || R.DecidedTick < 0L ||
				!KingdomPolityAmbientTransactionRules.SafeText(R.Fault, false) ||
				R.Digest != Digest(R)) return false;
			switch (R.Phase)
			{
			case KingdomPolityAdmissionReceiptPhase.Prepared:
				return R.DecidedTick == 0L && R.ResidentId == 0 &&
					string.IsNullOrEmpty(R.BodyReceiptId) && string.IsNullOrEmpty(R.Fault);
			case KingdomPolityAdmissionReceiptPhase.Committed:
				return R.DecidedTick >= R.PreparedTick && R.ResidentId > 0 &&
					R.BodyReceiptId == BodyReceipt(R) && string.IsNullOrEmpty(R.Fault);
			case KingdomPolityAdmissionReceiptPhase.Rejected:
			case KingdomPolityAdmissionReceiptPhase.RolledBack:
			case KingdomPolityAdmissionReceiptPhase.Faulted:
				return R.DecidedTick >= R.PreparedTick && R.ResidentId == 0 &&
					string.IsNullOrEmpty(R.BodyReceiptId) && !string.IsNullOrEmpty(R.Fault);
			default: return false;
			}
		}

		internal static bool Same(KingdomPolityAdmissionReceipt A,
			KingdomPolityAdmissionReceipt B)
		{
			return A == null ? B == null : B != null && A.Digest == B.Digest &&
				A.ReceiptId == B.ReceiptId;
		}
	}
}
