using System;
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst
{
	/// <summary>Exact raw-CAS edge for the base StringGameState identity fence.</summary>
	public static partial class KingdomIdentityFenceRuntime
	{
		/// <summary>Read-only fence proof for previews and reports. It never initializes, repairs,
		/// or clears a fault; ordinary load/founding reconciliation owns those mutations.</summary>
		public static bool TryVerify(KingdomSystem System, out string Failure)
		{
			Failure = null;
			if (The.Game == null || System == null)
				return Fail("the game or realm system is absent", out Failure);
			string raw = ReadRaw();
			if (string.IsNullOrEmpty(raw))
				return Fail("the base identity fence is absent", out Failure);
			if (!KingdomRealmRetirementCodec.TryDecodeFence(raw,
				out KingdomIdentityFence fence, out Failure)) return false;
			if (fence.GameId != The.Game.GameID)
				return Fail("identity fence belongs to another game", out Failure);
			if (!TryRealmDigest(System, out string realm, out long incarnation,
				out string digest, out Failure)) return false;
			if (fence.LastRealmId != realm || fence.LastRealmDigest != digest
				|| incarnation <= 0L || incarnation != fence.NextRealmIncarnation)
				return Fail("live realm and base identity fence diverged", out Failure);
			if (fence.Disposition == KingdomIdentityFenceDisposition.PreparedForRemoval)
			{
				if (!System.TryReadRealmRetirement(out KingdomRealmRetirementState receipt,
					out Failure) || receipt == null
					|| (receipt.Phase != KingdomRealmRetirementPhase.ReadyForFence
						&& receipt.Phase != KingdomRealmRetirementPhase.FenceCommitted
						&& receipt.Phase != KingdomRealmRetirementPhase.PreparedForRemoval))
					return Fail(Failure ?? "prepared base fence lacks retirement authority",
						out Failure);
				return true;
			}
			return fence.Disposition == KingdomIdentityFenceDisposition.Operational
				|| Fail("live realm is paired with a non-operational identity fence", out Failure);
		}

		public static bool TryReconcile(KingdomSystem System, out string Failure)
		{
			Failure = null;
			if (The.Game == null || System == null)
				return Fail("the game or realm system is absent", out Failure);
			string raw = ReadRaw();
			if (string.IsNullOrEmpty(raw)) return Initialize(System, raw, out Failure);
			if (!KingdomRealmRetirementCodec.TryDecodeFence(raw,
				out KingdomIdentityFence fence, out Failure))
				return Fault(System, Failure, out Failure);
			if (fence.GameId != The.Game.GameID)
				return Fault(System, "identity fence belongs to another game", out Failure);

			bool authority = TryRealmDigest(System, out string realm, out long incarnation,
				out string digest, out string authorityFailure);
			if (!authority)
			{
				if (System.Founded)
					return Fault(System, authorityFailure, out Failure);
				if (fence.Disposition == KingdomIdentityFenceDisposition.Operational)
					return Fault(System, "operational identity fence has no decoded realm authority",
						out Failure);
				System.RealmIdentityFenceFault = null;
				return true;
			}
			if (fence.LastRealmId != realm || fence.LastRealmDigest != digest
				|| incarnation <= 0L || incarnation != fence.NextRealmIncarnation)
				return Fault(System, "live realm and base identity fence diverged", out Failure);
			if (fence.Disposition == KingdomIdentityFenceDisposition.PreparedForRemoval)
			{
				if (!System.TryReadRealmRetirement(out KingdomRealmRetirementState receipt,
					out authorityFailure) || receipt == null
					|| (receipt.Phase != KingdomRealmRetirementPhase.ReadyForFence
						&& receipt.Phase != KingdomRealmRetirementPhase.FenceCommitted
						&& receipt.Phase != KingdomRealmRetirementPhase.PreparedForRemoval))
					return Fault(System, "prepared base fence lacks recoverable retirement authority",
						out Failure);
			}
			else if (fence.Disposition != KingdomIdentityFenceDisposition.Operational)
				return Fault(System, "live realm is paired with a non-operational identity fence",
					out Failure);
			System.RealmIdentityFenceFault = null;
			return true;
		}

		public static bool TryReserveFounding(KingdomSystem System, string TransactionId,
			out long Incarnation, out string Failure)
		{
			Incarnation = 0L;
			Failure = null;
			if (The.Game == null || System == null || System.Founded
				|| !KingdomIdentityRules.IsFoundingTransaction(TransactionId))
				return Fail("founding fence reservation context is invalid", out Failure);
			if (!TryRead(out string raw, out KingdomIdentityFence fence, out Failure))
			{
				if (!string.IsNullOrEmpty(raw)) return false;
				if (!KingdomIdentityFenceRules.TryInitialize(The.Game.GameID,
					KingdomIdentityFenceDisposition.Unfounded, 0L, null, null,
					out fence, out Failure) || !TryWriteRaw(raw,
					KingdomRealmRetirementCodec.EncodeFence(fence), out Failure)) return false;
				raw = ReadRaw();
			}
			if (!KingdomIdentityFenceRules.TryReserveIncarnation(fence, fence.Revision,
				TransactionId, out KingdomIdentityFence next, out Incarnation, out Failure))
				return false;
			if (!TryWriteRaw(raw, KingdomRealmRetirementCodec.EncodeFence(next), out Failure))
				return false;
			System.PendingRealmIncarnationTransaction = TransactionId;
			System.PendingRealmIncarnation = Incarnation;
			return true;
		}

		public static bool TryCommitFounding(KingdomSystem System, string TransactionId,
			out string Failure)
		{
			Failure = null;
			if (!TryRealmDigest(System, out string realm, out long incarnation,
				out string digest, out Failure) || System.PendingRealmIncarnation != incarnation
				|| System.PendingRealmIncarnationTransaction != TransactionId
				|| !TryRead(out string raw, out KingdomIdentityFence fence, out Failure)) return false;
			if (fence.Disposition == KingdomIdentityFenceDisposition.Operational
				&& fence.LastRealmId == realm && fence.LastRealmDigest == digest)
				return ClearFoundingReservation(System);
			if (!KingdomIdentityFenceRules.TryCommitOperational(fence, fence.Revision,
				TransactionId, realm, digest, out KingdomIdentityFence next, out Failure)
				|| !TryWriteRaw(raw, KingdomRealmRetirementCodec.EncodeFence(next), out Failure))
				return false;
			return ClearFoundingReservation(System);
		}

		public static bool TryCommitRemovalFence(KingdomSystem System,
			KingdomRealmRetirementState Current, long Tick,
			out KingdomRealmRetirementState Prepared, out string Failure)
		{
			Prepared = null;
			Failure = null;
			if (System == null || Current == null
				|| Current.Phase != KingdomRealmRetirementPhase.ReadyForFence
				|| Tick < Current.UpdatedTick
				|| !TryRealmDigest(System, out string realm, out long incarnation,
					out string realmDigest, out Failure)
				|| realm != Current.RealmId || incarnation != Current.RealmIncarnation
				|| !TryRead(out string raw, out KingdomIdentityFence fence, out Failure)) return false;
			string beforeRaw = raw;
			string receiptDigest = KingdomIdentityFenceReceiptRules.PreparedReceiptBinding(Current);
			if (!KingdomRealmRetirementRules.Digest(receiptDigest))
				return Fail("prepared receipt binding could not be formed", out Failure);
			if (fence.Disposition == KingdomIdentityFenceDisposition.Operational)
			{
				string predecessor = WireDigest(raw);
				string tombstone = KingdomRetirementDigestRules.Tombstone(
					predecessor, Current, realmDigest);
				if (!KingdomIdentityFenceRules.TryPrepareRemoval(fence, fence.Revision,
					Current.GameId, realm, realmDigest, tombstone, predecessor, receiptDigest,
					out KingdomIdentityFence next, out Failure)) return false;
				string afterRaw = KingdomRealmRetirementCodec.EncodeFence(next);
				if (!TryWriteRaw(raw, afterRaw, out Failure)) return false;
				fence = next; raw = afterRaw;
			}
			else if (fence.Disposition == KingdomIdentityFenceDisposition.PreparedForRemoval)
			{
				if (fence.LastRealmId != realm || fence.LastRealmDigest != realmDigest
					|| fence.PreparedReceiptDigest != receiptDigest
					|| !KingdomRealmRetirementRules.Digest(fence.TombstoneChainDigest)
					|| !KingdomRealmRetirementRules.Digest(fence.PreparedFromDigest))
					return Fail("prepared fence does not match this retirement", out Failure);
				beforeRaw = null;
			}
			else return Fail("identity fence is not operational or recoverably prepared",
				out Failure);

			KingdomRemovalRecord record = new KingdomRemovalRecord
			{
				Kind = KingdomRemovalProjectionKind.GlobalState,
				Id = KingdomRealmRetirementRules.FenceRecordId,
				Disposition = KingdomRemovalDisposition.Preserved,
				BeforeDigest = beforeRaw == null ? fence.PreparedFromDigest : WireDigest(beforeRaw),
				AfterDigest = WireDigest(raw),
				Detail = "base StringGameState fence committed by exact raw CAS"
			};
			// The binding names Current exactly. Keep its timestamp through the two synthetic
			// fence transitions so a later read-only verifier can reconstruct that same receipt.
			long receiptTick = Current.UpdatedTick;
			if (!KingdomRealmRetirementRules.TryRecord(Current, Current.Revision, record, receiptTick,
				out KingdomRealmRetirementState recorded, out Failure)
				|| !KingdomRealmRetirementRules.TrySetPhase(recorded, recorded.Revision,
					KingdomRealmRetirementPhase.ReadyForFence,
					KingdomRealmRetirementPhase.FenceCommitted, receiptTick,
					out Prepared, out Failure))
				return false;
			return true;
		}

		internal static bool TryRealmDigest(KingdomSystem System, out string Realm,
			out long Incarnation, out string Digest, out string Failure)
		{
			Realm = null; Digest = null; Incarnation = 0L; Failure = null;
			if (The.Game == null || System == null || !System.TryExactSettlementIds(true,
				out List<string> settlements, out Failure) || !System.Founded)
				return Fail(Failure ?? "exact live realm authority is absent", out Failure);
			Realm = System.RealmId; Incarnation = System.RealmIncarnation;
			Digest = KingdomRetirementDigestRules.Realm(The.Game.GameID, Realm, Incarnation,
				System.KingdomFactionName, System.RealmIdentityTransactionId,
				System.RealmIdentityFoundedTick, settlements);
			return KingdomRealmRetirementRules.Digest(Digest);
		}

		private static bool Initialize(KingdomSystem System, string ExpectedRaw,
			out string Failure)
		{
			Failure = null;
			if (!System.Founded)
			{
				if (!KingdomIdentityFenceRules.TryInitialize(The.Game.GameID,
					KingdomIdentityFenceDisposition.Unfounded, 0L, null, null,
					out KingdomIdentityFence empty, out Failure)) return false;
				return TryWriteRaw(ExpectedRaw,
					KingdomRealmRetirementCodec.EncodeFence(empty), out Failure);
			}
			if (System.RealmIncarnation <= 0L) System.RealmIncarnation = 1L;
			if (!TryRealmDigest(System, out string realm, out long incarnation,
				out string digest, out Failure)
				|| !KingdomIdentityFenceRules.TryInitialize(The.Game.GameID,
					KingdomIdentityFenceDisposition.Operational, incarnation, realm, digest,
					out KingdomIdentityFence live, out Failure)) return false;
			return TryWriteRaw(ExpectedRaw, KingdomRealmRetirementCodec.EncodeFence(live),
				out Failure);
		}

		private static bool TryRead(out string Raw, out KingdomIdentityFence Fence,
			out string Failure)
		{
			Raw = ReadRaw(); Fence = null; Failure = null;
			if (string.IsNullOrEmpty(Raw)) return false;
			return KingdomRealmRetirementCodec.TryDecodeFence(Raw, out Fence, out Failure)
				&& Fence.GameId == The.Game?.GameID;
		}

		private static bool TryWriteRaw(string Expected, string Next, out string Failure)
		{
			Failure = null;
			if (The.Game == null || ReadRaw() != Expected)
				return Fail("identity fence changed before compare-and-swap", out Failure);
			The.Game.SetStringGameState(KingdomIdentityFenceRules.StateKey, Next);
			return ReadRaw() == Next || Fail("identity fence did not retain its CAS write", out Failure);
		}

		private static string ReadRaw()
		{
			return The.Game?.GetStringGameState(KingdomIdentityFenceRules.StateKey, null);
		}

		private static string WireDigest(string Raw)
		{
			return KingdomRetirementDigestRules.Evidence("identity-fence-wire",
				new List<string> { Raw ?? "" });
		}

		private static bool ClearFoundingReservation(KingdomSystem System)
		{
			System.PendingRealmIncarnationTransaction = null;
			System.PendingRealmIncarnation = 0L;
			return true;
		}

		private static bool Fault(KingdomSystem System, string Message, out string Failure)
		{
			System.RealmIdentityFenceFault = Message ?? "identity fence fault";
			Failure = System.RealmIdentityFenceFault;
			return false;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
