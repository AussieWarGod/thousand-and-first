using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Engine-free reconstruction of the receipt that authorized one fence CAS.</summary>
	public static class KingdomIdentityFenceReceiptRules
	{
		public static string PreparedReceiptBinding(KingdomRealmRetirementState State)
		{
			if (!TryReadySnapshot(State, out KingdomRealmRetirementState ready)) return null;
			return KingdomRetirementDigestRules.Evidence("prepared-retirement-receipt-v1",
				new List<string> { KingdomRealmRetirementCodec.Encode(ready) });
		}

		public static bool TryReadySnapshot(KingdomRealmRetirementState State,
			out KingdomRealmRetirementState Ready)
		{
			Ready = null;
			if (State == null) return false;
			KingdomRealmRetirementState ready = State.Clone();
			if (ready.Phase == KingdomRealmRetirementPhase.FenceCommitted
				|| ready.Phase == KingdomRealmRetirementPhase.PreparedForRemoval)
			{
				int delta = ready.Phase == KingdomRealmRetirementPhase.PreparedForRemoval ? 3 : 2;
				if (!RemoveSingleFenceRecord(ready) || ready.Revision <= delta) return false;
				ready.Phase = KingdomRealmRetirementPhase.ReadyForFence;
				ready.Revision -= delta;
			}
			if (ready.Phase != KingdomRealmRetirementPhase.ReadyForFence
				|| !KingdomRealmRetirementRules.Valid(ready, out string _)) return false;
			Ready = ready; return true;
		}

		public static bool PreparedProofMatches(KingdomIdentityFence Fence,
			KingdomRealmRetirementState State, string RealmDigest, long CurrentIncarnation)
		{
			return Fence != null && State != null
				&& KingdomIdentityFenceRules.Valid(Fence, out string _)
				&& Fence.Disposition == KingdomIdentityFenceDisposition.PreparedForRemoval
				&& Fence.LastRealmId == State.RealmId && Fence.LastRealmDigest == RealmDigest
				&& State.RealmIncarnation == CurrentIncarnation
				&& Fence.NextRealmIncarnation == CurrentIncarnation
				&& Fence.PreparedReceiptDigest == PreparedReceiptBinding(State)
				&& TryReadySnapshot(State, out KingdomRealmRetirementState ready)
				&& KingdomRetirementDigestRules.Tombstone(Fence.PreparedFromDigest,
					ready, RealmDigest) == Fence.TombstoneChainDigest;
		}

		private static bool RemoveSingleFenceRecord(KingdomRealmRetirementState State)
		{
			int found = -1;
			for (int i = 0; i < State.Records.Count; i++)
				if (State.Records[i]?.Kind == KingdomRemovalProjectionKind.GlobalState
					&& State.Records[i].Id == KingdomRealmRetirementRules.FenceRecordId)
				{
					if (found >= 0) return false;
					found = i;
				}
			if (found < 0) return false;
			State.Records.RemoveAt(found); return true;
		}
	}
}
