using System;

namespace ThousandAndFirst
{
	/// <summary>Detached result offered to C18 only after the body family validates it.</summary>
	internal sealed class KingdomBodyHistoryPreparation
	{
		public bool AlreadyDurable;
		public byte[] ReplacementPayload;
		public KingdomBodyHistoryReceipt Receipt;
	}

	internal enum KingdomBodyHistoryPreparationBlock : byte
	{
		None = 0,
		Retryable = 1,
		Capacity = 2,
		OpaqueFuture = 3,
		Quarantined = 4
	}

	/// <summary>Engine-free section-3 preparation and exact durable-read verification.</summary>
	internal static class KingdomBodyHistoryTransactions
	{
		internal static bool TryPrepare(byte[] CurrentPayload, string ExactRealmId,
			KingdomWitnessedBodyEventEvidence Evidence,
			out KingdomBodyHistoryPreparation Preparation, out string Failure)
		{
			return TryPrepare(CurrentPayload, ExactRealmId, Evidence, out Preparation,
				out KingdomBodyHistoryPreparationBlock _, out Failure);
		}

		internal static bool TryPrepare(byte[] CurrentPayload, string ExactRealmId,
			KingdomWitnessedBodyEventEvidence Evidence,
			out KingdomBodyHistoryPreparation Preparation,
			out KingdomBodyHistoryPreparationBlock Block, out string Failure)
		{
			Preparation = null;
			Block = KingdomBodyHistoryPreparationBlock.None;
			Failure = null;
			KingdomBodyHistoryEnvelope envelope = KingdomBodyHistoryStore.ReadForRealm(
				CurrentPayload, ExactRealmId, out Failure);
			if (Failure != null || envelope == null || envelope.Quarantined)
			{
				Block = KingdomBodyHistoryPreparationBlock.Quarantined;
				return Fail(Failure ?? "body history section is quarantined", out Failure);
			}
			if (envelope.IsOpaqueFuture)
			{
				Block = KingdomBodyHistoryPreparationBlock.OpaqueFuture;
				return Fail("body history section was written by a newer version", out Failure);
			}

			long before = envelope.Book.Revision;
			if (!KingdomBodyHistoryRules.TryRecordWitnessedProcedure(envelope.Book, before,
				Evidence, out KingdomBodyHistoryReceipt receipt, out Failure))
			{
				Block = envelope.Book.Rows.Count >= KingdomBodyHistoryRules.MaxRows
					? KingdomBodyHistoryPreparationBlock.Capacity
					: KingdomBodyHistoryPreparationBlock.Retryable;
				return false;
			}
			if (envelope.Book.Revision == before)
			{
				Preparation = new KingdomBodyHistoryPreparation
				{
					AlreadyDurable = true,
					Receipt = receipt.Copy()
				};
				return true;
			}

			if (!KingdomBodyHistoryStore.TryWrite(envelope, out byte[] replacement,
				out Failure))
			{
				Block = KingdomBodyHistoryPreparationBlock.Retryable;
				return false;
			}
			if (replacement == null || replacement.Length == 0
				|| replacement.Length > KingdomBodyHistoryCodec.MaxEnvelopeBytes)
			{
				Block = KingdomBodyHistoryPreparationBlock.Capacity;
				return Fail("body history replacement exceeds its section cap", out Failure);
			}
			Preparation = new KingdomBodyHistoryPreparation
			{
				ReplacementPayload = replacement,
				Receipt = receipt.Copy()
			};
			return true;
		}

		internal static bool ContainsExact(byte[] Payload, string ExactRealmId,
			KingdomWitnessedBodyEventEvidence Evidence,
			out KingdomBodyHistoryReceipt Receipt, out string Failure)
		{
			Receipt = null;
			if (!TryPrepare(Payload, ExactRealmId, Evidence,
				out KingdomBodyHistoryPreparation result, out Failure)) return false;
			if (!result.AlreadyDurable)
				return Fail("body history readback does not contain the exact receipt", out Failure);
			Receipt = result.Receipt.Copy();
			return true;
		}

		private static bool Fail(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}
	}
}
