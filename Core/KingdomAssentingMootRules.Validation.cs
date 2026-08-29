using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomAssentingMootRules
	{
		public static bool Validate(KingdomAssentingMootReceipt Receipt, out string Failure)
		{
			Failure = "";
			if (Receipt == null || Receipt.Version != CurrentReceiptVersion
				|| !Enum.IsDefined(typeof(KingdomAssentingMootPhase), Receipt.Phase))
				return Fail("unknown assenting-moot receipt version or phase", out Failure);
			Receipt.Normalize();
			if (Receipt.Phase == KingdomAssentingMootPhase.None)
				return Empty(Receipt) || Fail("idle assenting-moot receipt carries residue", out Failure);
			if (Receipt.Phase == KingdomAssentingMootPhase.Quarantined)
				return ValidateQuarantine(Receipt, out Failure);
			if (Receipt.Generation <= 0 || Receipt.BaselineHitpoints <= 0
				|| Receipt.PreparedTick < 0L || Receipt.AppliedTick < 0L
				|| Receipt.SuspendedTick < 0L
				|| !Bounded(Receipt.RealmId, MaxIdentityChars)
				|| !Bounded(Receipt.SettlementId, MaxIdentityChars)
				|| !Bounded(Receipt.SettlementName, MaxNameChars)
				|| !Bounded(Receipt.ZoneId, MaxIdentityChars)
				|| !Bounded(Receipt.BuildingObjectId, MaxIdentityChars)
				|| !Bounded(Receipt.LotId, MaxIdentityChars)
				|| !Bounded(Receipt.AuthorityId, MaxIdentityChars)
				|| !Bounded(Receipt.MembershipFingerprint, MaxIdentityChars))
				return Fail("assenting-moot receipt has malformed bounded identity", out Failure);
			if (!ValidRows(Receipt.AssentResidentIds, Receipt.AssentResidentNames,
				Receipt.AssentBodyObjectIds, MaxAssents)
				|| !ValidRows(Receipt.ExemptResidentIds, Receipt.ExemptResidentNames,
					Receipt.ExemptBodyObjectIds, MaxExemptions))
				return Fail("assenting-moot membership is ragged, duplicated, or unbounded", out Failure);
			KingdomAssentingMootReceipt sealedCopy = Receipt.Copy();
			Seal(sealedCopy);
			if (!string.Equals(sealedCopy.AuthorityId, Receipt.AuthorityId,
				StringComparison.Ordinal) || !string.Equals(sealedCopy.MembershipFingerprint,
				Receipt.MembershipFingerprint, StringComparison.Ordinal))
				return Fail("assenting-moot identity or membership fingerprint diverged", out Failure);
			int maximum = StrengthFor(Receipt.AssentResidentIds.Count,
				Receipt.ExemptResidentIds.Count);
			if (Receipt.Strength < 0 || Receipt.Strength > maximum
				|| Receipt.Strength % StrengthPerAssent != 0)
				return Fail("assenting-moot strength exceeds its named voices", out Failure);
			return ValidatePhase(Receipt, out Failure);
		}

		private static bool ValidatePhase(KingdomAssentingMootReceipt R, out string Failure)
		{
			Failure = "";
			if (!string.IsNullOrEmpty(R.Fault))
				return Fail("non-quarantined assenting-moot receipt carries a fault", out Failure);
			if (R.Phase == KingdomAssentingMootPhase.Applied)
			{
				return R.Strength > 0 && R.AppliedTick >= R.PreparedTick
					&& R.SuspendedTick == 0L && string.IsNullOrEmpty(R.SuspendedReason)
					|| Fail("applied assenting-moot receipt lacks current projection proof", out Failure);
			}
			if (R.Phase == KingdomAssentingMootPhase.Suspended)
			{
				return R.Strength == 0 && R.SuspendedTick >= R.PreparedTick
					&& R.AppliedTick <= R.SuspendedTick
					&& Bounded(R.SuspendedReason, MaxReasonChars)
					|| Fail("suspended assenting-moot receipt lacks its dated reason", out Failure);
			}
			return R.Phase == KingdomAssentingMootPhase.Prepared && R.Strength == 0
				&& R.AppliedTick == 0L && R.SuspendedTick == 0L
				&& string.IsNullOrEmpty(R.SuspendedReason)
				|| Fail("prepared assenting-moot receipt carries projected state", out Failure);
		}

		private static bool ValidRows(List<int> Ids, List<string> Names,
			List<string> Bodies, int Maximum)
		{
			if (Ids == null || Names == null || Bodies == null || Ids.Count > Maximum
				|| Ids.Count != Names.Count || Ids.Count != Bodies.Count) return false;
			for (int i = 0; i < Ids.Count; i++)
			{
				if (Ids[i] <= 0 || (i > 0 && Ids[i - 1] >= Ids[i])
					|| !Bounded(Names[i], MaxNameChars)
					|| !Bounded(Bodies[i], MaxIdentityChars)) return false;
			}
			return true;
		}

		private static bool ValidateQuarantine(KingdomAssentingMootReceipt R,
			out string Failure)
		{
			bool valid = Bounded(R.Fault, MaxFaultChars)
				&& OptionalBounded(R.RealmId, MaxIdentityChars)
				&& OptionalBounded(R.SettlementId, MaxIdentityChars)
				&& OptionalBounded(R.SettlementName, MaxNameChars)
				&& OptionalBounded(R.ZoneId, MaxIdentityChars)
				&& OptionalBounded(R.BuildingObjectId, MaxIdentityChars)
				&& OptionalBounded(R.LotId, MaxIdentityChars)
				&& BoundedLists(R.AssentResidentIds, R.AssentResidentNames,
					R.AssentBodyObjectIds, MaxAssents)
				&& BoundedLists(R.ExemptResidentIds, R.ExemptResidentNames,
					R.ExemptBodyObjectIds, MaxExemptions);
			Failure = valid ? "" : "quarantined assenting-moot evidence is not bounded";
			return valid;
		}

		private static bool BoundedLists(List<int> Ids, List<string> Names,
			List<string> Bodies, int Maximum)
		{
			if (Ids == null || Names == null || Bodies == null || Ids.Count > Maximum
				|| Names.Count > Maximum || Bodies.Count > Maximum) return false;
			for (int i = 0; i < Names.Count; i++)
				if (!OptionalBounded(Names[i], MaxNameChars)) return false;
			for (int i = 0; i < Bodies.Count; i++)
				if (!OptionalBounded(Bodies[i], MaxIdentityChars)) return false;
			return true;
		}

		private static bool Empty(KingdomAssentingMootReceipt R)
		{
			return R.Generation == 0 && R.BaselineHitpoints == 0 && R.Strength == 0
				&& R.PreparedTick == 0L && R.AppliedTick == 0L && R.SuspendedTick == 0L
				&& string.IsNullOrEmpty(R.RealmId) && string.IsNullOrEmpty(R.SettlementId)
				&& string.IsNullOrEmpty(R.SettlementName) && string.IsNullOrEmpty(R.ZoneId)
				&& string.IsNullOrEmpty(R.BuildingObjectId) && string.IsNullOrEmpty(R.LotId)
				&& string.IsNullOrEmpty(R.AuthorityId)
				&& string.IsNullOrEmpty(R.MembershipFingerprint)
				&& R.AssentResidentIds.Count == 0 && R.AssentResidentNames.Count == 0
				&& R.AssentBodyObjectIds.Count == 0 && R.ExemptResidentIds.Count == 0
				&& R.ExemptResidentNames.Count == 0 && R.ExemptBodyObjectIds.Count == 0
				&& string.IsNullOrEmpty(R.SuspendedReason) && string.IsNullOrEmpty(R.Fault);
		}

		private static bool Fail(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}
	}
}
