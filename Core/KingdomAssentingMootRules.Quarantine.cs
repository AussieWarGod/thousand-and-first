using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomAssentingMootRules
	{
		public static KingdomAssentingMootReceipt Quarantined(
			KingdomAssentingMootReceipt Current, string Fault)
		{
			if (Current == null || Current.Phase == KingdomAssentingMootPhase.None) return null;
			KingdomAssentingMootReceipt copy = Current.Copy();
			copy.Phase = KingdomAssentingMootPhase.Quarantined;
			copy.Strength = 0;
			copy.RealmId = SingleLine(copy.RealmId, MaxIdentityChars);
			copy.SettlementId = SingleLine(copy.SettlementId, MaxIdentityChars);
			copy.SettlementName = SingleLine(copy.SettlementName, MaxNameChars);
			copy.ZoneId = SingleLine(copy.ZoneId, MaxIdentityChars);
			copy.BuildingObjectId = SingleLine(copy.BuildingObjectId, MaxIdentityChars);
			copy.LotId = SingleLine(copy.LotId, MaxIdentityChars);
			copy.AuthorityId = SingleLine(copy.AuthorityId, MaxIdentityChars);
			copy.MembershipFingerprint = SingleLine(copy.MembershipFingerprint, MaxIdentityChars);
			BoundRows(copy.AssentResidentIds, copy.AssentResidentNames,
				copy.AssentBodyObjectIds, MaxAssents);
			BoundRows(copy.ExemptResidentIds, copy.ExemptResidentNames,
				copy.ExemptBodyObjectIds, MaxExemptions);
			copy.SuspendedReason = SingleLine(copy.SuspendedReason, MaxReasonChars);
			copy.Fault = SingleLine(Fault, MaxFaultChars);
			if (string.IsNullOrEmpty(copy.Fault))
				copy.Fault = "assenting-moot evidence diverged";
			return copy;
		}

		private static void BoundRows(List<int> Ids, List<string> Names,
			List<string> Bodies, int Maximum)
		{
			Trim(Ids, Maximum);
			Trim(Names, Maximum);
			Trim(Bodies, Maximum);
			for (int i = 0; i < Names.Count; i++)
				Names[i] = SingleLine(Names[i], MaxNameChars);
			for (int i = 0; i < Bodies.Count; i++)
				Bodies[i] = SingleLine(Bodies[i], MaxIdentityChars);
		}

		private static void Trim<T>(List<T> Values, int Maximum)
		{
			if (Values == null) return;
			while (Values.Count > Maximum) Values.RemoveAt(Values.Count - 1);
		}
	}
}
