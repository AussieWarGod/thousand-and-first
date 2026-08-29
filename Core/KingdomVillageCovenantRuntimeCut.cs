using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free half of the living founding cut. The runtime supplies the one current civic
	/// authority and both lawful realm names; this cut proves, commits, and reads back one row.
	/// </summary>
	public static class KingdomVillageCovenantRuntimeCut
	{
		public static bool TryRecord(IKingdomCivicMemoryAuthority Authority, string RealmId,
			string CurrentFactionKey, KingdomVillageCovenantReceipt Receipt,
			out KingdomVillageCovenantAppend Outcome,
			out KingdomVillageCovenantReceipt Effective, out string Failure)
		{
			Outcome = KingdomVillageCovenantAppend.AlreadyRecorded;
			Effective = null;
			Failure = null;
			if (Authority == null || !KingdomIdentityRules.IsRealmId(RealmId)
				|| Receipt == null
				|| !string.Equals(Receipt.RealmId, RealmId, StringComparison.Ordinal))
				return KingdomVillageCovenantRules.Fail(
					"the covenant runtime cut has no exact current realm authority", out Failure);
			if (!KingdomVillageCovenantRules.AuthorityBelongsToRealm(
				Receipt.FoundingAuthority, CurrentFactionKey, out Failure)
				|| !KingdomVillageCovenantRules.TryValidateRow(Receipt, out Failure)) return false;
			if (!KingdomVillageCovenantLease.TryReadArchive(Authority, RealmId,
				out KingdomCivicMemorySectionLease lease, out _, out Failure)) return false;
			if (!KingdomVillageCovenantLease.TryCommitAppended(Authority, lease, RealmId,
				Receipt, out Outcome, out Effective, out Failure)) return false;
			return KingdomVillageCovenantLease.TryConfirm(Authority, RealmId, Effective,
				out Failure);
		}
	}
}
