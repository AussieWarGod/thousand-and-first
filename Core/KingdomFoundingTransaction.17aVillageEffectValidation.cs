using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransaction
	{
		private static bool VillageStandingEffectReceiptValid(r_FounderBasin basin,
			KingdomFoundingKind kind, KingdomFoundingPhase phase, out string failure)
		{
			failure = null;
			basin.ReadVillageEffect(out int state, out int before, out int beforeCarry,
				out int after, out int afterCarry, out string digest,
				out bool any, out bool complete);
			if (!any) return true; // admitted pre-v1 pending receipt; never publication proof
			if (kind != KingdomFoundingKind.VillageCharter || !complete)
			{
				failure = "village-standing effect receipt is partial or belongs to another kind";
				return false;
			}
			// PublicationCommitted + Prepared is the intentional save/exception cut after the exact
			// standing root swap and before the Applied marker. The current pair must still prove that
			// cut in Ensure/Detect; this structural validator never infers it from a threshold.
			bool phasePair = state == KingdomFoundingTransactionRules.VillageStandingEffectPrepared
				? phase == KingdomFoundingPhase.WaterCommitted ||
					phase == KingdomFoundingPhase.PublicationCommitted
				: state == KingdomFoundingTransactionRules.VillageStandingEffectApplied &&
					(phase == KingdomFoundingPhase.WaterCommitted ||
					 phase == KingdomFoundingPhase.PublicationCommitted ||
					 phase == KingdomFoundingPhase.Complete);
			string expected = KingdomFoundingTransactionRules.VillageStandingEffectDigest(
				basin.PendingTransactionID, basin.PendingAuthority,
				basin.PendingVillageFaction, basin.PendingVillageDisplayName,
				basin.PendingZoneID, before, beforeCarry, after, afterCarry);
			if (!phasePair || after != KingdomRules.VillageCharterSealedStanding ||
				afterCarry != 0 || expected == null || expected != digest)
			{
				failure = "village-standing effect receipt is noncanonical or not transaction-bound";
				return false;
			}
			return true;
		}
	}
}
