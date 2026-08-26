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
		private static void NormalizeSink(ref KingdomTradeSinkState State,
			bool HasPayload, ref bool Malformed)
		{
			if (!Enum.IsDefined(typeof(KingdomTradeSinkState), State))
			{
				Malformed = true;
				return;
			}
			if (State == KingdomTradeSinkState.None)
			{
				if (HasPayload) Malformed = true;
				else State = KingdomTradeSinkState.Skipped;
				return;
			}
			if (HasPayload == (State == KingdomTradeSinkState.Skipped)) Malformed = true;
		}

		private static void NormalizeProofs(KingdomTradeBook Book)
		{
			if (Book.RecentProofs == null || Book.RecentProofs.Count > MaxRecentProofs)
			{
				QuarantineBook(Book, "retirement proof list is missing or oversized");
				return;
			}
			for (int i = 0; i < Book.RecentProofs.Count; i++)
			{
				KingdomTradeProof proof = Book.RecentProofs[i];
				bool exactPending = Book.PendingRetirement != null
					&& ExactProof(proof, Book.PendingRetirement);
				if (!ValidProof(Book, proof, !exactPending))
				{
					QuarantineBook(Book, "malformed retirement proof was preserved");
					return;
				}
				for (int j = 0; j < i; j++)
					if (Book.RecentProofs[j].Sequence == proof.Sequence
						|| string.Equals(Book.RecentProofs[j].Id, proof.Id, StringComparison.Ordinal))
					{
						QuarantineBook(Book, "duplicate retirement proofs were preserved symmetrically");
						return;
					}
			}
		}

		private static void NormalizeProofCompactions(KingdomTradeBook Book)
		{
			if (Book.CompactedProofs == null
				|| Book.CompactedProofs.Count > MaxCompactedProofs)
			{
				QuarantineBook(Book, "compacted retirement proof list is missing or oversized");
				return;
			}
			for (int i = 0; i < Book.CompactedProofs.Count; i++)
				if (!ValidProofCompaction(Book.CompactedProofs[i]))
				{
					QuarantineBook(Book, "malformed compacted retirement proof was preserved");
					return;
				}
		}

		private static bool ValidProofCompaction(KingdomTradeProofCompaction Row)
		{
			return Row != null && ValidId(Row.RealmId) && Row.FirstSequence > 0L
				&& Row.LastSequence >= Row.FirstSequence && Row.ProofCount > 0
				&& ValidId(Row.EvidenceHash);
		}

		private static void NormalizeArchives(KingdomTradeBook Book)
		{
			if (Book.Archives == null || Book.Archives.Count > MaxArchives)
			{
				QuarantineBook(Book, "archive evidence list is missing or oversized");
				return;
			}
			for (int i = 0; i < Book.Archives.Count; i++)
			{
				KingdomTradeArchive row = Book.Archives[i];
				if (!ValidArchiveEvidence(row))
				{
					QuarantineBook(Book, "malformed archive evidence was preserved");
					return;
				}
				for (int j = 0; j < i; j++)
					if (string.Equals(Book.Archives[j].RealmId, row.RealmId,
						StringComparison.Ordinal))
					{
						QuarantineBook(Book, "duplicate realm archive evidence was preserved symmetrically");
						return;
					}
			}
		}

		private static bool ValidArchiveEvidence(KingdomTradeArchive Row)
		{
			return Row != null && !TooLong(Row.RealmId, MaxIdChars) && ValidId(Row.RealmId)
				&& ValidSettlementSet(Row.SettlementIds)
				&& Row.RetainedEscrowDrams >= 0L && Row.ManifestEscrowDrams >= 0
				&& Enum.IsDefined(typeof(KingdomTradeManifestStatus), Row.ManifestStatus)
				&& (Row.ManifestStatus == KingdomTradeManifestStatus.None
					? string.IsNullOrEmpty(Row.ManifestId) && Row.ManifestEscrowDrams == 0
					: ValidId(Row.ManifestId))
				&& Row.CharterCount >= 0 && Row.CharterCount <= MaxCharters
				&& Row.ProjectionCount >= 0 && Row.ProjectionCount <= MaxProjectionRows
				&& Row.ProofCount >= 0 && Row.OpenRequestedWater >= 0
				&& Row.OpenProvedWater >= 0 && Row.OpenProvedWater <= Row.OpenRequestedWater
				&& Row.OpenAmbiguousWater >= 0 && Row.RetiredThrough >= 0L
				&& (string.IsNullOrEmpty(Row.OpenOperationId) || ValidId(Row.OpenOperationId))
				&& (string.IsNullOrEmpty(Row.PendingRetirementId)
					|| ValidId(Row.PendingRetirementId))
				&& CanonicalSha256(Row.AuthorityEvidenceHash) && Row.ClosedTick >= 0L
				&& CanonicalSha256(Row.ReceiptEvidenceHash)
				&& string.Equals(Row.ReceiptEvidenceHash, ArchiveReceiptDigest(Row),
					StringComparison.Ordinal);
		}

		private static void NormalizeIncidents(KingdomTradeBook Book)
		{
			if (Book.Incidents == null || Book.Incidents.Count > MaxIncidents)
			{
				QuarantineBook(Book, "incident evidence list is missing or oversized");
				return;
			}
			for (int i = 0; i < Book.Incidents.Count; i++)
			{
				KingdomTradeIncident row = Book.Incidents[i];
				if (!ValidIncidentEvidence(row))
				{
					QuarantineBook(Book, "malformed incident evidence was preserved");
					return;
				}
			}
		}

		private static bool ValidIncidentEvidence(KingdomTradeIncident Row)
		{
			return Row != null && ValidId(Row.RealmId) && Row.Sequence >= 0L
				&& (Row.Sequence == 0L ? string.IsNullOrEmpty(Row.OperationId)
					: string.Equals(Row.OperationId, OperationId(Row.RealmId, Row.Sequence),
						StringComparison.Ordinal))
				&& ValidId(Row.EvidenceHash) && Row.Tick >= 0L
				&& !TooLong(Row.Fault, MaxTextChars);
		}

		private static void NormalizePendingRetirement(KingdomTradeBook Book)
		{
			KingdomTradeProof pending = Book.PendingRetirement;
			if (pending == null) return;
			if (!ValidProof(Book, pending, false)
				|| !string.Equals(pending.RealmId, Book.RealmId, StringComparison.Ordinal))
			{
				QuarantineBook(Book, "partial retirement evidence could not be completed exactly");
				return;
			}
			if (!CompletePendingRetirement(Book))
				QuarantineBook(Book, "partial retirement evidence could not be completed exactly");
		}

		private static bool ValidProof(KingdomTradeBook Book, KingdomTradeProof Proof,
			bool RequireRetired)
		{
			if (Proof == null || Proof.Sequence <= 0L || !ValidId(Proof.RealmId)
				|| !ValidId(Proof.OperationEvidenceHash)
				|| (RequireRetired && Proof.Sequence > Book.RetiredThrough)
				|| !string.Equals(Proof.Id, OperationId(Proof.RealmId, Proof.Sequence), StringComparison.Ordinal)
				|| Proof.Kind == KingdomTradeOperationKind.None
				|| (Proof.Disposition != KingdomTradePhase.Terminal
					&& Proof.Disposition != KingdomTradePhase.Quarantined)
				|| Proof.RequestedWater < 0 || Proof.RequestedWater > MaxOperationWater
				|| Proof.ProvedWater < 0
				|| Proof.ProvedWater > Proof.RequestedWater || Proof.AmbiguousWater < 0
				|| Proof.AmbiguousWater != 0
				|| Proof.MaterialRequested < 0 || Proof.MaterialProved < 0
				|| Proof.MaterialProved != Proof.MaterialRequested
				|| Proof.ManifestEscrowBefore < 0 || Proof.ManifestEscrowDebit < 0
				|| Proof.ManifestEscrowDebit > Proof.ManifestEscrowBefore
				|| Proof.ManifestEscrowAfter != Proof.ManifestEscrowBefore - Proof.ManifestEscrowDebit
				|| Proof.RetainedBefore < 0L || Proof.RetainedDelta < 0L
				|| Proof.RetainedDelta > long.MaxValue - Proof.RetainedBefore
				|| Proof.RetainedAfter != Proof.RetainedBefore + Proof.RetainedDelta
				|| !ValidId(Proof.SettlementId) || Proof.Tick < 0L
				|| (Proof.Kind != KingdomTradeOperationKind.CharterDelivery
					&& !ValidId(Proof.ManifestId))
				|| !SinkClean(Proof.ChronicleState) || !SinkClean(Proof.LedgerState)
				|| !SinkClean(Proof.MessageState) || !SinkClean(Proof.DeedState)
				|| ((Proof.Kind == KingdomTradeOperationKind.CharterDelivery
						|| Proof.Kind == KingdomTradeOperationKind.ManifestLoad)
					&& Proof.ProvedWater != Proof.RequestedWater)
				|| (Proof.Kind == KingdomTradeOperationKind.CharterDelivery
					&& (Proof.ChronicleState != KingdomTradeSinkState.Delivered
						|| Proof.LedgerState != KingdomTradeSinkState.Delivered
						|| Proof.MessageState != KingdomTradeSinkState.Delivered
						|| (Proof.Disposition == KingdomTradePhase.Terminal
							? Proof.DeedState != KingdomTradeSinkState.Delivered
							: Proof.DeedState != KingdomTradeSinkState.Delivered
								&& Proof.DeedState != KingdomTradeSinkState.Skipped)))
				|| (Proof.Kind == KingdomTradeOperationKind.ManifestDelivery
					&& (Proof.ManifestEscrowBefore != Proof.RequestedWater
						|| Proof.ManifestEscrowDebit != Proof.ProvedWater
						|| Proof.ManifestEscrowState != KingdomTradePhysicalState.Proved
						|| Proof.RetainedBefore != 0L || Proof.RetainedDelta != 0L
						|| Proof.RetainedAfter != 0L
						|| Proof.RetainedState != KingdomTradePhysicalState.None))
				|| (Proof.Kind == KingdomTradeOperationKind.ManifestLapse
					&& (Proof.RetainedDelta != Proof.RequestedWater
						|| Proof.RetainedState != KingdomTradePhysicalState.Proved
						|| Proof.ManifestEscrowBefore != 0 || Proof.ManifestEscrowDebit != 0
						|| Proof.ManifestEscrowAfter != 0
						|| Proof.ManifestEscrowState != KingdomTradePhysicalState.None))
				|| ((Proof.Kind == KingdomTradeOperationKind.CharterDelivery
						|| Proof.Kind == KingdomTradeOperationKind.ManifestLoad
						|| Proof.Kind == KingdomTradeOperationKind.ManifestTurnback)
					&& (Proof.ManifestEscrowBefore != 0 || Proof.ManifestEscrowDebit != 0
						|| Proof.ManifestEscrowAfter != 0
						|| Proof.ManifestEscrowState != KingdomTradePhysicalState.None
						|| Proof.RetainedBefore != 0L || Proof.RetainedDelta != 0L
						|| Proof.RetainedAfter != 0L
						|| Proof.RetainedState != KingdomTradePhysicalState.None))
				|| (Proof.ManifestCleanup != (Proof.Kind == KingdomTradeOperationKind.ManifestLapse
					|| (Proof.Kind == KingdomTradeOperationKind.ManifestDelivery
						&& Proof.ManifestEscrowAfter == 0)))
				|| TooLong(Proof.Fault, MaxTextChars)) return false;
			return Enum.IsDefined(typeof(KingdomTradeOperationKind), Proof.Kind)
				&& Enum.IsDefined(typeof(KingdomTradePhase), Proof.Disposition)
				&& Enum.IsDefined(typeof(KingdomTradePhysicalState), Proof.ManifestEscrowState)
				&& Enum.IsDefined(typeof(KingdomTradePhysicalState), Proof.RetainedState)
				&& Enum.IsDefined(typeof(KingdomTradeSinkState), Proof.ChronicleState)
				&& Enum.IsDefined(typeof(KingdomTradeSinkState), Proof.LedgerState)
				&& Enum.IsDefined(typeof(KingdomTradeSinkState), Proof.MessageState)
				&& Enum.IsDefined(typeof(KingdomTradeSinkState), Proof.DeedState);
		}

	}
}
