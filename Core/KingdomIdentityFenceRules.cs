using System;

namespace ThousandAndFirst
{
	public static class KingdomIdentityFenceRules
	{
		public const string StateKey = "r_TAF_RealmIdentityFence_v1";

		public static bool Valid(KingdomIdentityFence Fence, out string Failure)
		{
			Failure = null;
			if (Fence == null || Fence.Version != KingdomIdentityFence.CurrentVersion
				|| Fence.Revision <= 0 || string.IsNullOrEmpty(Fence.GameId)
				|| Fence.GameId.Length > 512 || Fence.NextRealmIncarnation < 0L
				|| Fence.PendingIncarnation < 0L
				|| !Enum.IsDefined(typeof(KingdomIdentityFenceDisposition), Fence.Disposition)
				|| !OptionalText(Fence.LastRealmId, 512)
				|| !OptionalDigest(Fence.LastRealmDigest)
				|| !OptionalDigest(Fence.TombstoneChainDigest)
				|| !OptionalDigest(Fence.PreparedFromDigest)
				|| !OptionalDigest(Fence.PreparedReceiptDigest)
				|| !OptionalTransaction(Fence.PendingTransactionId))
				return Fail("identity fence is malformed or outside its bounds", out Failure);
			bool pending = !string.IsNullOrEmpty(Fence.PendingTransactionId);
			if (pending != (Fence.PendingIncarnation > 0L)
				|| (pending && Fence.PendingIncarnation != Fence.NextRealmIncarnation))
				return Fail("identity fence pending incarnation is partial", out Failure);
			if (pending && Fence.Disposition != KingdomIdentityFenceDisposition.Unfounded)
				return Fail("only an unfounded fence may hold a founding reservation", out Failure);
			if (Fence.Disposition == KingdomIdentityFenceDisposition.Unfounded
				&& (Fence.PreparedFromDigest != null || Fence.PreparedReceiptDigest != null))
				return Fail("unfounded identity fence retains terminal predecessor evidence",
					out Failure);
			if (Fence.Disposition == KingdomIdentityFenceDisposition.Operational
				&& (string.IsNullOrEmpty(Fence.LastRealmId)
					|| string.IsNullOrEmpty(Fence.LastRealmDigest)
					|| pending || Fence.PreparedFromDigest != null
					|| Fence.PreparedReceiptDigest != null))
				return Fail("operational identity fence lacks exact realm evidence", out Failure);
			if ((Fence.Disposition == KingdomIdentityFenceDisposition.PreparedForRemoval
				|| Fence.Disposition == KingdomIdentityFenceDisposition.RetiredOrAbandoned)
				&& (string.IsNullOrEmpty(Fence.LastRealmId)
					|| string.IsNullOrEmpty(Fence.LastRealmDigest)
					|| !KingdomRealmRetirementRules.Digest(Fence.TombstoneChainDigest)
					|| !KingdomRealmRetirementRules.Digest(Fence.PreparedFromDigest)
					|| !KingdomRealmRetirementRules.Digest(Fence.PreparedReceiptDigest)
					|| pending))
				return Fail("prepared identity fence lacks tombstone or predecessor evidence",
					out Failure);
			return true;
		}

		public static bool TryInitialize(string GameId, KingdomIdentityFenceDisposition Disposition,
			long HighWater, string RealmId, string RealmDigest,
			out KingdomIdentityFence Fence, out string Failure)
		{
			Fence = new KingdomIdentityFence
			{
				Revision = 1,
				GameId = GameId,
				NextRealmIncarnation = HighWater,
				LastRealmId = RealmId,
				LastRealmDigest = RealmDigest,
				TombstoneChainDigest = null,
					PreparedFromDigest = null,
					PreparedReceiptDigest = null,
				Disposition = Disposition,
				PendingTransactionId = null,
				PendingIncarnation = 0L
			};
			if (Disposition != KingdomIdentityFenceDisposition.Unfounded
				&& Disposition != KingdomIdentityFenceDisposition.Operational)
				return Fail("identity fence initialization cannot forge a terminal state",
					out Failure);
			if (Disposition == KingdomIdentityFenceDisposition.Operational
				&& HighWater == 0L) Fence.NextRealmIncarnation = 1L;
			return Valid(Fence, out Failure);
		}

		public static bool TryReserveIncarnation(KingdomIdentityFence Current,
			int ExpectedRevision, string TransactionId, out KingdomIdentityFence Updated,
			out long Incarnation, out string Failure)
		{
			Updated = null;
			Incarnation = 0L;
			if (!Valid(Current, out Failure) || !KingdomIdentityRules.IsFoundingTransaction(TransactionId))
				return Fail(Failure ?? "founding transaction is invalid", out Failure);
			if (Current.PendingTransactionId == TransactionId)
			{
				Updated = Current.Clone();
				Incarnation = Current.PendingIncarnation;
				return true;
			}
			if (Current.Revision != ExpectedRevision || !string.IsNullOrEmpty(Current.PendingTransactionId)
				|| Current.Disposition == KingdomIdentityFenceDisposition.Operational
				|| Current.NextRealmIncarnation == long.MaxValue)
				return Fail("identity fence cannot reserve another incarnation", out Failure);
			Updated = Current.Clone();
			Incarnation = ++Updated.NextRealmIncarnation;
			Updated.PendingTransactionId = TransactionId;
			Updated.PendingIncarnation = Incarnation;
			Updated.Disposition = KingdomIdentityFenceDisposition.Unfounded;
			Updated.PreparedFromDigest = null;
			Updated.PreparedReceiptDigest = null;
			Updated.Revision++;
			return Valid(Updated, out Failure);
		}

		public static bool TryCommitOperational(KingdomIdentityFence Current,
			int ExpectedRevision, string TransactionId, string RealmId, string RealmDigest,
			out KingdomIdentityFence Updated, out string Failure)
		{
			Updated = null;
			if (!Valid(Current, out Failure) || Current.Revision != ExpectedRevision
				|| Current.PendingTransactionId != TransactionId || Current.PendingIncarnation <= 0L
				|| string.IsNullOrEmpty(RealmId) || RealmId.Length > 512
				|| !KingdomRealmRetirementRules.Digest(RealmDigest))
				return Fail(Failure ?? "identity fence operational CAS refused", out Failure);
			Updated = Current.Clone();
			Updated.LastRealmId = RealmId;
			Updated.LastRealmDigest = RealmDigest;
			Updated.Disposition = KingdomIdentityFenceDisposition.Operational;
			Updated.PendingTransactionId = null;
			Updated.PendingIncarnation = 0L;
			Updated.PreparedFromDigest = null;
			Updated.PreparedReceiptDigest = null;
			Updated.Revision++;
			return Valid(Updated, out Failure);
		}

		public static bool TryPrepareRemoval(KingdomIdentityFence Current,
			int ExpectedRevision, string GameId, string RealmId, string RealmDigest,
			string TombstoneDigest, string PredecessorDigest, string ReceiptDigest,
			out KingdomIdentityFence Updated, out string Failure)
		{
			Updated = null;
			if (!Valid(Current, out Failure) || Current.Revision != ExpectedRevision
				|| Current.GameId != GameId || Current.LastRealmId != RealmId
				|| Current.LastRealmDigest != RealmDigest
				|| Current.Disposition != KingdomIdentityFenceDisposition.Operational
				|| !string.IsNullOrEmpty(Current.PendingTransactionId)
				|| !KingdomRealmRetirementRules.Digest(TombstoneDigest)
				|| !KingdomRealmRetirementRules.Digest(PredecessorDigest)
				|| !KingdomRealmRetirementRules.Digest(ReceiptDigest))
				return Fail(Failure ?? "identity fence removal CAS refused", out Failure);
			Updated = Current.Clone();
			Updated.Disposition = KingdomIdentityFenceDisposition.PreparedForRemoval;
			Updated.TombstoneChainDigest = TombstoneDigest;
			Updated.PreparedFromDigest = PredecessorDigest;
			Updated.PreparedReceiptDigest = ReceiptDigest;
			Updated.Revision++;
			return Valid(Updated, out Failure);
		}

		public static KingdomIdentityFenceObservation Observe(KingdomIdentityFence Fence,
			string GameId, bool SystemAuthorityPresent)
		{
			if (Fence == null) return KingdomIdentityFenceObservation.Absent;
			if (!Valid(Fence, out string _)) return KingdomIdentityFenceObservation.Malformed;
			if (Fence.GameId != GameId) return KingdomIdentityFenceObservation.WrongGame;
			if (SystemAuthorityPresent)
				return Fence.Disposition == KingdomIdentityFenceDisposition.Operational
					? KingdomIdentityFenceObservation.Operational
					: Fence.Disposition == KingdomIdentityFenceDisposition.Unfounded
						? KingdomIdentityFenceObservation.Unfounded
						: KingdomIdentityFenceObservation.Prepared;
			return Fence.Disposition == KingdomIdentityFenceDisposition.Operational
				? KingdomIdentityFenceObservation.LostAuthority
				: Fence.Disposition == KingdomIdentityFenceDisposition.Unfounded
					? KingdomIdentityFenceObservation.Unfounded
					: KingdomIdentityFenceObservation.Prepared;
		}

		private static bool OptionalText(string Value, int Max)
		{
			return Value == null || Value.Length <= Max;
		}

		private static bool OptionalDigest(string Value)
		{
			return Value == null || KingdomRealmRetirementRules.Digest(Value);
		}

		private static bool OptionalTransaction(string Value)
		{
			return Value == null || KingdomIdentityRules.IsFoundingTransaction(Value);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
