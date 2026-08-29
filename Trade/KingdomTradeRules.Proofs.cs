using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeRules
	{
		private static KingdomTradeProof ProofFor(KingdomTradeBook Book, KingdomTradeOperation Operation,
			KingdomTradePhase Disposition, long Tick, string Fault)
		{
			KingdomTradeOutbox box = Operation.Outbox;
			return new KingdomTradeProof
				{
					RealmId = Book.RealmId, Sequence = Operation.Sequence, Id = Operation.Id, Kind = Operation.Kind,
					OperationEvidenceHash = OperationEvidenceDigest(Operation),
				Disposition = Disposition, RequestedWater = Operation.RequestedWater,
				ProvedWater = Operation.ProvedWater, AmbiguousWater = Operation.AmbiguousWater,
				SettlementId = Operation.SettlementId, ManifestId = Operation.ManifestId,
				ManifestEscrowBefore = Operation.ManifestEscrowBefore,
				ManifestEscrowDebit = Operation.ManifestEscrowDebit,
				ManifestEscrowAfter = Operation.ManifestEscrowAfter,
				ManifestEscrowState = Operation.ManifestEscrowState,
				RetainedBefore = Operation.RetainedBefore, RetainedDelta = Operation.RetainedDelta,
				RetainedAfter = Operation.RetainedAfter, RetainedState = Operation.RetainedState,
				MaterialRequested = Operation.MaterialRequested, MaterialProved = Operation.MaterialProved,
				ChronicleState = box == null ? KingdomTradeSinkState.Skipped : box.ChronicleState,
				LedgerState = box == null ? KingdomTradeSinkState.Skipped : box.LedgerState,
					MessageState = box == null ? KingdomTradeSinkState.Skipped : box.MessageState,
					DeedState = box == null ? KingdomTradeSinkState.Skipped : box.DeedState,
					PolityRecipient = ClonePolityRecipientWitness(Operation.PolityRecipient),
					ManifestCleanup = (Operation.Kind == KingdomTradeOperationKind.ManifestDelivery
						&& Operation.ManifestEscrowAfter == 0)
						|| Operation.Kind == KingdomTradeOperationKind.ManifestLapse,
					Tick = Tick < 0L ? 0L : Tick, Fault = Bound(Fault, MaxTextChars)
			};
		}

		private static string OperationEvidenceDigest(KingdomTradeOperation Operation)
		{
			try
			{
				KingdomTradeBook evidence = new KingdomTradeBook { OpenOperation = Operation };
				byte[] bytes = KingdomTradeCodec.EncodePayload(evidence);
				string inner;
				using (SHA256 sha = SHA256.Create()) inner = Hex(sha.ComputeHash(bytes));
				return CanonicalId("operation-proof", Operation.Sequence, Operation.Id, inner);
			}
			catch { return null; }
		}

		private static string OperationEvidenceDigestV3(KingdomTradeOperation Operation)
		{
			try
			{
				KingdomTradeBook evidence = new KingdomTradeBook
				{
					FormatVersion = 4,
					OpenOperation = Operation
				};
				byte[] bytes = KingdomTradeCodec.EncodePayloadV3ForMigration(evidence);
				string inner;
				using (SHA256 sha = SHA256.Create()) inner = Hex(sha.ComputeHash(bytes));
				return CanonicalId("operation-proof", Operation.Sequence, Operation.Id, inner);
			}
			catch { return null; }
		}

		/// <summary>Exact wire-v3/format-4 adoption; no current writer calls this seam.</summary>
		internal static void MigrateWireV3(KingdomTradeBook Book)
		{
			if (Book == null || Book.FormatVersion != 4) return;
			KingdomTradeOperation operation = Book.OpenOperation;
			KingdomTradeProof pending = Book.PendingRetirement;
			// The frozen v3 writer ignores this additive field, so establish a terminal
			// migration value before either success or quarantine can expose the book.
			if (operation != null && operation.Kind == KingdomTradeOperationKind.CharterDelivery
				&& operation.Pattern == null)
				operation.Pattern = KingdomTradePatternRules.PriorWireDefault();
			bool migratePending = operation != null && pending != null;
			if (migratePending)
			{
				string oldDigest = OperationEvidenceDigestV3(operation);
				if (!ValidId(oldDigest) || !string.Equals(pending.OperationEvidenceHash,
					oldDigest, StringComparison.Ordinal))
				{
					Book.FormatVersion = CurrentFormatVersion;
					QuarantineBook(Book,
						"wire-v3 pending retirement did not authenticate its exact prior operation");
					return;
				}
			}
			Book.FormatVersion = CurrentFormatVersion;
			QuarantineLegacyConsignmentWithoutWitness(Book, "wire-v3");
			if (migratePending)
				pending.OperationEvidenceHash = OperationEvidenceDigest(operation);
		}

		private static bool CompletePendingRetirement(KingdomTradeBook Book)
		{
			KingdomTradeProof proof = Book?.PendingRetirement;
			if (proof == null || Book.RecentProofs == null || !ValidProof(Book, proof, false))
				return false;
			int matches = 0;
			KingdomTradeProof existing = null;
			for (int i = 0; i < Book.RecentProofs.Count; i++)
				if (Book.RecentProofs[i] != null
					&& (Book.RecentProofs[i].Sequence == proof.Sequence
						|| string.Equals(Book.RecentProofs[i].Id, proof.Id, StringComparison.Ordinal)))
				{
					matches++;
					existing = Book.RecentProofs[i];
				}
			if (matches > 1) { QuarantineBook(Book, "duplicate retirement receipt"); return false; }
			if (matches == 1 && !ExactProof(existing, proof))
			{
				QuarantineBook(Book, "colliding retirement receipt differs from pending evidence");
				return false;
			}
			if (Book.OpenOperation != null)
			{
				if (!ProofMatchesOperation(Book, proof, Book.OpenOperation)
					|| HasUnresolvedEffects(Book.OpenOperation)
					|| !DurableDomainSettled(Book, Book.OpenOperation)) return false;
			}
			else if (matches != 1 || Book.RetiredThrough < proof.Sequence) return false;
			if (!ManifestCleanupExactOrDone(Book, proof)) return false;
			if (matches == 0)
			{
				if (Book.RecentProofs.Count >= MaxRecentProofs) return false;
				Book.RecentProofs.Add(proof);
			}
			Book.RetiredThrough = Math.Max(Book.RetiredThrough, proof.Sequence);
			if (Book.OpenOperation != null) Book.OpenOperation = null;
			if (!CompleteManifestCleanup(Book, proof)) return false;
			Book.PendingRetirement = null;
			return true;
		}

		private static bool CompleteManifestCleanup(KingdomTradeBook Book,
			KingdomTradeProof Proof)
		{
			if (!ManifestCleanupExactOrDone(Book, Proof)) return false;
			if (!Proof.ManifestCleanup || Book.Manifest == null) return true;
			Book.Manifest = null;
			return true;
		}

		private static bool ManifestCleanupExactOrDone(KingdomTradeBook Book,
			KingdomTradeProof Proof)
		{
			if (!Proof.ManifestCleanup) return true;
			KingdomTradeManifestState manifest = Book.Manifest;
			if (manifest == null) return Book.OpenOperation == null;
			bool exact = string.Equals(manifest.Id, Proof.ManifestId, StringComparison.Ordinal);
			if (Proof.Kind == KingdomTradeOperationKind.ManifestDelivery)
				exact = exact && manifest.Status == KingdomTradeManifestStatus.Delivered
					&& manifest.EscrowDrams == 0 && Proof.ManifestEscrowAfter == 0;
			else if (Proof.Kind == KingdomTradeOperationKind.ManifestLapse)
				exact = exact && manifest.Status == KingdomTradeManifestStatus.Quarantined
					&& manifest.EscrowDrams == Proof.RequestedWater
					&& Proof.RetainedDelta == Proof.RequestedWater;
			else exact = false;
			return exact;
		}

		private static bool ProofMatchesOperation(KingdomTradeBook Book,
			KingdomTradeProof Proof, KingdomTradeOperation Operation)
		{
			if (Book == null || Proof == null || Operation == null) return false;
			KingdomTradeProof expected = ProofFor(Book, Operation, Proof.Disposition,
				Proof.Tick, Proof.Fault);
			return ExactProof(Proof, expected)
				&& Operation.Phase == Proof.Disposition
				&& Operation.UpdatedTick == Proof.Tick;
		}

		private static bool ExactProof(KingdomTradeProof Left, KingdomTradeProof Right)
		{
			return Left != null && Right != null
				&& string.Equals(Left.RealmId, Right.RealmId, StringComparison.Ordinal)
				&& Left.Sequence == Right.Sequence
				&& string.Equals(Left.Id, Right.Id, StringComparison.Ordinal)
				&& string.Equals(Left.OperationEvidenceHash, Right.OperationEvidenceHash,
					StringComparison.Ordinal)
				&& Left.Kind == Right.Kind && Left.Disposition == Right.Disposition
				&& Left.ProvedWater == Right.ProvedWater
				&& Left.AmbiguousWater == Right.AmbiguousWater
				&& Left.RequestedWater == Right.RequestedWater
				&& string.Equals(Left.SettlementId, Right.SettlementId, StringComparison.Ordinal)
				&& string.Equals(Left.ManifestId, Right.ManifestId, StringComparison.Ordinal)
				&& Left.ManifestEscrowBefore == Right.ManifestEscrowBefore
				&& Left.ManifestEscrowDebit == Right.ManifestEscrowDebit
				&& Left.ManifestEscrowAfter == Right.ManifestEscrowAfter
				&& Left.ManifestEscrowState == Right.ManifestEscrowState
				&& Left.RetainedBefore == Right.RetainedBefore
				&& Left.RetainedDelta == Right.RetainedDelta
				&& Left.RetainedAfter == Right.RetainedAfter
				&& Left.RetainedState == Right.RetainedState
				&& Left.MaterialRequested == Right.MaterialRequested
				&& Left.MaterialProved == Right.MaterialProved
				&& Left.ChronicleState == Right.ChronicleState
				&& Left.LedgerState == Right.LedgerState
				&& Left.MessageState == Right.MessageState
				&& Left.DeedState == Right.DeedState
				&& ((Left.PolityRecipient == null && Right.PolityRecipient == null) ||
					ExactPolityRecipientWitness(Left.PolityRecipient, Right.PolityRecipient))
				&& Left.ManifestCleanup == Right.ManifestCleanup
				&& Left.Tick == Right.Tick
				&& string.Equals(Left.Fault, Right.Fault, StringComparison.Ordinal);
		}

	}
}
