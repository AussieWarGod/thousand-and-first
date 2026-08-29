using XRL;

namespace ThousandAndFirst
{
	public static partial class KingdomIdentityFenceRuntime
	{
		/// <summary>Read-only terminal proof. It never initializes, repairs, or clears a fault.</summary>
		public static bool TryVerifyPreparedRemoval(KingdomSystem System,
			KingdomRealmRetirementState State, out string Failure)
		{
			Failure = null;
			if (The.Game == null || System == null || State == null
				|| State.Phase != KingdomRealmRetirementPhase.PreparedForRemoval
				|| !KingdomRealmRetirementRules.Valid(State, out Failure))
				return Fail(Failure ?? "prepared retirement receipt is absent", out Failure);
			if (!TryRead(out string raw, out KingdomIdentityFence fence, out Failure)
				|| fence.Disposition != KingdomIdentityFenceDisposition.PreparedForRemoval
				|| fence.GameId != State.GameId || fence.GameId != The.Game.GameID)
				return Fail(Failure ?? "prepared identity fence is absent", out Failure);
			if (!TryRealmDigest(System, out string realm, out long incarnation,
				out string realmDigest, out Failure) || realm != State.RealmId
				|| incarnation != State.RealmIncarnation
				|| fence.NextRealmIncarnation != incarnation || fence.LastRealmId != realm
				|| fence.LastRealmDigest != realmDigest
				|| fence.PreparedReceiptDigest !=
					KingdomIdentityFenceReceiptRules.PreparedReceiptBinding(State))
				return Fail(Failure ?? "prepared identity fence and receipt diverged", out Failure);
			if (!KingdomIdentityFenceReceiptRules.PreparedProofMatches(fence, State,
				realmDigest, incarnation))
				return Fail("prepared identity fence tombstone is forged", out Failure);
			KingdomRemovalRecord record = FenceRecord(State);
			return record != null && record.Disposition == KingdomRemovalDisposition.Preserved
				&& record.BeforeDigest == fence.PreparedFromDigest
				&& record.AfterDigest == WireDigest(raw)
				|| Fail("prepared receipt lacks the exact retained fence record", out Failure);
		}
		private static KingdomRemovalRecord FenceRecord(KingdomRealmRetirementState State)
		{
			KingdomRemovalRecord found = null;
			for (int i = 0; i < (State?.Records?.Count ?? 0); i++)
			{
				KingdomRemovalRecord row = State.Records[i];
				if (row?.Kind != KingdomRemovalProjectionKind.GlobalState
					|| row.Id != KingdomRealmRetirementRules.FenceRecordId) continue;
				if (found != null) return null;
				found = row;
			}
			return found;
		}
	}
}
