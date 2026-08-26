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
		public static bool TryPrepareExile(KingdomTradeBook Source, long Tick,
			string ExactRealmId, List<string> ExactSettlementIds,
			out KingdomTradeBook Replacement, out long SettledTick, out string Failure)
		{
			Replacement = null;
			SettledTick = -1L;
			Failure = null;
			if (Source == null || Tick < 0L || !ValidId(ExactRealmId))
			{
				Failure = "Trade exile requires an exact realm id and nonnegative tick.";
				return false;
			}
			List<string> exact;
			if (!TryExactSettlementSet(ExactSettlementIds, out exact))
			{
				Failure = "Trade exile requires the complete exact settlement topology.";
				return false;
			}
			if (!Source.IdentityBound)
			{
				long closedTick;
				if (TryGetExactExileClosedTick(Source, ExactRealmId, exact,
					out closedTick, out Failure))
				{
					Replacement = Source;
					SettledTick = closedTick;
					return true;
				}
				return false;
			}
			KingdomTradeBook copy;
			string originalEvidence = EvidenceDigest(Source);
			try
			{
				KingdomTradeCodec.EncodePayload(Source);
				copy = KingdomTradeCodec.DecodeEnvelopeRaw(
					KingdomTradeCodec.EncodeEnvelope(Source));
				// Detached exile preparation is an explicit semantic authority caller. Raw save decode
				// itself never repairs; Core first gets to reject coexisting legacy rows.
				Normalize(copy);
			}
			catch (Exception error)
			{
				Failure = "Trade exile could not freeze bounded authority: " + Bound(error.Message, 256);
				return false;
			}
			if (copy.Archives == null || copy.Archives.Count >= MaxArchives)
			{
				Failure = "Trade exile archive capacity is full.";
				return false;
			}
			if (!BookUsable(copy) || !string.Equals(copy.RealmId, ExactRealmId,
					StringComparison.Ordinal) || !ExactStringSet(copy.SettlementIds, exact))
			{
				Failure = "Trade exile exact realm or settlement topology does not match live authority.";
				return false;
			}
			for (int i = 0; i < copy.Archives.Count; i++)
				if (string.Equals(copy.Archives[i].RealmId, ExactRealmId,
					StringComparison.Ordinal))
				{
					Failure = "Trade exile collides with existing archive evidence for this realm.";
					return false;
				}
			int proofCount = copy.RecentProofs.Count;
			for (int i = 0; i < copy.CompactedProofs.Count; i++)
			{
				if (!ValidProofCompaction(copy.CompactedProofs[i])
					|| copy.CompactedProofs[i].ProofCount > int.MaxValue - proofCount)
				{
					Failure = "Trade exile proof accounting is malformed or overflowing.";
					return false;
				}
				proofCount += copy.CompactedProofs[i].ProofCount;
			}
			long archived;
			if (!TryAddEscrow(copy.RetainedEscrowDrams,
				copy.UnattributedArchivedEscrowDrams, out archived))
			{
				Failure = "Trade exile unattributed escrow accounting overflowed.";
				return false;
			}
			int manifestEscrow = copy.Manifest?.EscrowDrams ?? 0;
			KingdomTradeOperation open = copy.OpenOperation;
			bool manifestAlreadyRetained = open != null
				&& open.Kind == KingdomTradeOperationKind.ManifestLapse
				&& open.RetainedState == KingdomTradePhysicalState.Proved
				&& copy.Manifest != null && open.RetainedDelta == manifestEscrow
				&& copy.RetainedEscrowDrams == open.RetainedAfter;
			if (!manifestAlreadyRetained
				&& !TryAddEscrow(archived, manifestEscrow, out archived))
			{
				Failure = "Trade exile escrow accounting overflowed.";
				return false;
			}
			int orphanedLoad = open != null && open.Kind == KingdomTradeOperationKind.ManifestLoad
				&& (copy.Manifest == null || !string.Equals(copy.Manifest.OperationId,
					open.Id, StringComparison.Ordinal)) ? open.ProvedWater : 0;
			if (!TryAddEscrow(archived, orphanedLoad, out archived))
			{
				Failure = "Trade exile orphaned manifest accounting overflowed.";
				return false;
			}
			string evidence = originalEvidence;
			if (!ValidId(evidence))
			{
				Failure = "Trade exile could not authenticate its complete authority graph.";
				return false;
			}
			KingdomTradeArchive archive = new KingdomTradeArchive
			{
				RealmId = ExactRealmId,
				SettlementIds = new List<string>(exact),
				RetainedEscrowDrams = archived,
				ManifestEscrowDrams = manifestEscrow,
				ManifestId = copy.Manifest?.Id,
				ManifestStatus = copy.Manifest?.Status ?? KingdomTradeManifestStatus.None,
				CharterCount = copy.Charters.Count,
				ProjectionCount = copy.Projections.Count,
				ProofCount = proofCount,
				OpenOperationId = open?.Id,
				PendingRetirementId = copy.PendingRetirement?.Id,
				OpenRequestedWater = open?.RequestedWater ?? 0,
				OpenProvedWater = open?.ProvedWater ?? 0,
				OpenAmbiguousWater = open?.AmbiguousWater ?? 0,
				RetiredThrough = copy.RetiredThrough,
				AuthorityEvidenceHash = evidence,
				ClosedTick = Tick
			};
			archive.ReceiptEvidenceHash = ArchiveReceiptDigest(archive);
			if (!CanonicalSha256(archive.AuthorityEvidenceHash)
				|| !CanonicalSha256(archive.ReceiptEvidenceHash))
			{
				Failure = "Trade exile could not authenticate its canonical archive receipt.";
				return false;
			}
			copy.Archives.Add(archive);
			copy.Charters = new List<KingdomTradeCharter>();
			copy.Manifest = null;
			copy.OpenOperation = null;
			copy.PendingRetirement = null;
			copy.RecentProofs = new List<KingdomTradeProof>();
			copy.CompactedProofs = new List<KingdomTradeProofCompaction>();
			copy.ActiveProjectionId = null;
			copy.ActiveProjectionObjectId = null;
			copy.Projections = new List<KingdomTradeProjectionRow>();
			copy.RetainedEscrowDrams = 0L;
			copy.UnattributedArchivedEscrowDrams = 0L;
			copy.RealmId = null;
			copy.IdentityBound = false;
			copy.SettlementIds = new List<string>();
			copy.OptionState = KingdomTradeOptionState.Unknown;
			copy.OptionObservedTick = Tick;
			copy.RestampPending = false;
			copy.NextCharterSequence = 1L;
			copy.NextOperationSequence = 1L;
			copy.RetiredThrough = 0L;
			try { KingdomTradeCodec.EncodePayload(copy); }
			catch
			{
				Failure = "Trade exile replacement exceeded bounded persistence capacity.";
				return false;
			}
			long authenticatedTick;
			string receiptFailure;
			if (!TryGetExactExileClosedTick(copy, ExactRealmId, exact,
				out authenticatedTick, out receiptFailure) || authenticatedTick != Tick)
			{
				Failure = "Trade exile replacement did not authenticate its exact durable receipt: "
					+ receiptFailure;
				return false;
			}
			Replacement = copy;
			SettledTick = authenticatedTick;
			return true;
		}

		/// <summary>Compatibility seam for engine-free callers using a concrete array.</summary>
		public static bool TryPrepareExile(KingdomTradeBook Source, long Tick,
			string ExactRealmId, string[] ExactSettlementIds,
			out KingdomTradeBook Replacement, out string Failure)
		{
			List<string> exact;
			if (!TryExactSettlementSet(ExactSettlementIds, out exact))
			{
				Replacement = null;
				Failure = "Trade exile requires the complete exact settlement topology.";
				return false;
			}
			long ignoredTick;
			return TryPrepareExile(Source, Tick, ExactRealmId, exact,
				out Replacement, out ignoredTick, out Failure);
		}

		/// <summary>
		/// Authenticates one exact settled exile receipt without mutating Book. The receipt may be
		/// observed either before return binding or immediately after the same exact identity was
		/// rebound. No active Trade authority or changed close clock may coexist with this proof.
		/// </summary>
	}
}
