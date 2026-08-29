#if !TAF_TESTS
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomLab
	{
		/// <summary>History delivery never rolls back or replays completed physical/value effects.</summary>
		private static void SettleCompletedBodyHistory(GameObject Actor,
			KingdomSystem System, r_KingdomLabJob Job)
		{
			if (Job == null || !Job.BodyHistoryRequiresRulerLife
				|| Job.BodyHistoryState == KingdomLabBodyHistoryPhase.Applied
				|| Job.BodyHistoryState == KingdomLabBodyHistoryPhase.OmittedPreservingMemory)
				return;
			if (!TryFreezeCompletedBodyHistoryWitness(Actor, Job, out string freezeFailure))
			{
				Job.BodyHistoryState = KingdomLabBodyHistoryPhase.Pending;
				Job.BodyHistoryFault = freezeFailure ?? "exact witness is temporarily unavailable";
				return;
			}
			if (TryCommitCompletedBodyHistory(Actor, System, Job,
				out KingdomBodyHistoryReceipt _,
				out KingdomBodyHistoryDeliveryResult result, out string failure))
			{
				Job.BodyHistoryState = KingdomLabBodyHistoryPhase.Applied;
				Job.BodyHistoryFault = "";
				return;
			}
			Job.BodyHistoryState = result
				== KingdomBodyHistoryDeliveryResult.OmittedPreservingMemory
				? KingdomLabBodyHistoryPhase.OmittedPreservingMemory
				: KingdomLabBodyHistoryPhase.Pending;
			Job.BodyHistoryFault = failure ?? "civic-memory delivery did not complete";
		}

		private static string BodyHistoryStatus(r_KingdomLabJob Job)
		{
			if (Job == null || !Job.BodyHistoryRequiresRulerLife)
				return "legacy physical-only; no civic body history was claimed";
			switch (Job.BodyHistoryState)
			{
				case KingdomLabBodyHistoryPhase.Applied:
					return "applied to witnessed civic body history";
				case KingdomLabBodyHistoryPhase.OmittedPreservingMemory:
					return "not recorded; civic memory was preserved unchanged";
				default:
					return "pending exact civic-memory delivery";
			}
		}

		private static void OmitPendingBodyHistory(r_KingdomLabJob Job)
		{
			if (Job == null || Job.BodyHistoryState != KingdomLabBodyHistoryPhase.Pending) return;
			Job.BodyHistoryState = KingdomLabBodyHistoryPhase.OmittedPreservingMemory;
			Job.BodyHistoryFault = "Player finished physical receipt without a civic-history row.";
		}
	}
}
#endif
