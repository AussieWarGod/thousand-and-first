#if !TAF_TESTS
using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomLab
	{
		/// <summary>
		/// Commits section 3 under one C18 lease, then proves the exact row from a fresh
		/// section read. A successful earlier attempt is an idempotent durable read.
		/// </summary>
		private static bool TryCommitCompletedBodyHistory(GameObject Actor,
			KingdomSystem System, r_KingdomLabJob Job,
			out KingdomBodyHistoryReceipt Receipt,
			out KingdomBodyHistoryDeliveryResult Result, out string Failure)
		{
			Receipt = null;
			Result = KingdomBodyHistoryDeliveryResult.Retryable;
			Failure = null;
			if (Job == null || !Job.BodyHistoryRequiresRulerLife)
				return FailBodyCommit("this physical-only legacy job owns no history",
					out Failure);
			if (!TryBuildCompletedBodyHistoryEvidence(Actor, System, Job,
				out KingdomWitnessedBodyEventEvidence evidence, out Failure)) return false;
			KingdomCivicMemorySystem memory = The.Game?.GetSystem<KingdomCivicMemorySystem>();
			if (memory == null)
				return FailBodyCommit("civic-memory authority is unavailable", out Failure);
			if (!memory.TryReadSection(KingdomCivicMemoryLimits.SectionBodyHistory,
				out KingdomCivicMemorySectionLease lease, out Failure)) return false;
			byte[] held = lease.Present ? lease.Payload() : null;
			if (!KingdomBodyHistoryTransactions.TryPrepare(held, Job.RealmId, evidence,
				out KingdomBodyHistoryPreparation prepared,
				out KingdomBodyHistoryPreparationBlock block, out Failure))
			{
				if (KingdomLabBodyHistoryContractRules.AfterFailure(block)
					== KingdomLabBodyHistoryPhase.OmittedPreservingMemory)
					Result = KingdomBodyHistoryDeliveryResult.OmittedPreservingMemory;
				return false;
			}
			if (prepared.AlreadyDurable)
			{
				Receipt = prepared.Receipt.Copy();
				Result = KingdomBodyHistoryDeliveryResult.Applied;
				return true;
			}
			if (prepared.ReplacementPayload == null
				|| !memory.TryCommitSection(lease, prepared.ReplacementPayload,
					out Failure)) return false;

			if (!memory.TryReadSection(KingdomCivicMemoryLimits.SectionBodyHistory,
				out KingdomCivicMemorySectionLease readback, out Failure)) return false;
			if (readback.ExpectedRevision <= lease.ExpectedRevision)
				return FailBodyCommit("civic-memory revision did not advance", out Failure);
			if (!KingdomBodyHistoryTransactions.ContainsExact(readback.Payload(),
				Job.RealmId, evidence, out KingdomBodyHistoryReceipt exact, out Failure)) return false;
			if (!string.Equals(exact.ReceiptId, prepared.Receipt.ReceiptId,
				StringComparison.Ordinal))
				return FailBodyCommit("body-history readback changed receipt identity", out Failure);
			Receipt = exact.Copy();
			Result = KingdomBodyHistoryDeliveryResult.Applied;
			return true;
		}

		private static bool FailBodyCommit(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}
	}
}
#endif
