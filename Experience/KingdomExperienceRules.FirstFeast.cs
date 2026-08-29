using System;

namespace ThousandAndFirst
{
	public static partial class KingdomExperienceRules
	{
		public static bool TryGetFirstFeast(KingdomExperienceLedger Ledger,
			string SettlementId, out KingdomFirstFeastReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			int index = FirstFeastIndex(Ledger, SettlementId);
			if (index >= 0) Receipt = Ledger.FirstFeasts[index].Copy();
			return true;
		}

		public static bool TryPublishFirstFeastOffer(KingdomExperienceLedger Ledger,
			long ExpectedRevision, KingdomFirstFeastReceipt Offer, out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure) || !KingdomFirstFeastRules.Valid(Offer)
				|| Offer.Phase != KingdomFirstFeastPhase.Offered)
				return Fail(Failure ?? "first-feast offer is invalid", out Failure);
			int at = FirstFeastIndex(Ledger, Offer.SettlementId);
			if (at >= 0)
				return KingdomFirstFeastRules.SameOfferSource(Ledger.FirstFeasts[at], Offer)
					|| Fail("first-feast deed already names different evidence", out Failure);
			if (!CanEmit(Ledger, KingdomExperienceOptionKind.CivicStory, Offer.DeedTick)
				|| Offer.EnableEpoch != Ledger.Story.EnableEpoch)
				return Fail("first-feast deed is outside the enabled story epoch", out Failure);
			if (ExpectedRevision != Ledger.Revision)
				return Fail("first-feast offer revision conflict", out Failure);
			if (Ledger.FirstFeasts.Count >= MaxFirstFeastReceipts)
				return Fail("first-feast capacity is full", out Failure);
			if (Ledger.Revision == long.MaxValue)
				return Fail("experience revision is exhausted", out Failure);
			KingdomExperienceLedger next = Clone(Ledger);
			next.FirstFeasts.Add(Offer.Copy());
			next.FirstFeasts.Sort((A, B) => string.CompareOrdinal(A.SettlementId,
				B.SettlementId));
			next.Revision++;
			if (!TryValidate(next, out Failure)) return false;
			Ledger.CopyFrom(next); return true;
		}

		/// <summary>Returns MutationCommitted only for the exact revision-changing close. Defer and
		/// exact retries succeed with false, and must not consume governance energy.</summary>
		public static bool TryDecideFirstFeast(KingdomExperienceLedger Ledger,
			long ExpectedRevision, string SettlementId, KingdomFirstFeastChoice Choice,
			string AdaptedDedication, long DecidedTick, out bool MutationCommitted,
			out KingdomFirstFeastReceipt Receipt, out string Failure)
		{
			MutationCommitted = false; Receipt = null; Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			int at = FirstFeastIndex(Ledger, SettlementId);
			if (at < 0) return Fail("first-feast proposal is absent", out Failure);
			if (!KingdomFirstFeastRules.TryDecide(Ledger.FirstFeasts[at], Choice,
				AdaptedDedication, DecidedTick, out KingdomFirstFeastReceipt decided,
				out bool changed, out Failure)) return false;
			if (!changed)
			{
				Receipt = decided; return true;
			}
			if (ExpectedRevision != Ledger.Revision)
				return Fail("first-feast decision revision conflict", out Failure);
			if (Ledger.Revision == long.MaxValue)
				return Fail("experience revision is exhausted", out Failure);
			KingdomExperienceLedger next = Clone(Ledger);
			next.FirstFeasts[at] = decided; next.Revision++;
			if (!TryValidate(next, out Failure)) return false;
			Ledger.CopyFrom(next); MutationCommitted = true; Receipt = decided.Copy(); return true;
		}

		public static bool TryArchiveFirstFeastOffer(KingdomExperienceLedger Ledger,
			long ExpectedRevision, string SettlementId, long ArchivedTick,
			out bool MutationCommitted, out KingdomFirstFeastReceipt Receipt,
			out string Failure)
		{
			MutationCommitted = false; Receipt = null; Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			int at = FirstFeastIndex(Ledger, SettlementId);
			if (at < 0) return Fail("first-feast proposal is absent", out Failure);
			KingdomFirstFeastReceipt standing = Ledger.FirstFeasts[at];
			if (standing.Phase == KingdomFirstFeastPhase.Archived)
			{
				Receipt = standing.Copy(); return true;
			}
			if (standing.Phase != KingdomFirstFeastPhase.Offered
				|| ArchivedTick < standing.OfferedTick)
				return Fail("only an unaccepted First Feast offer can be archived", out Failure);
			if (ExpectedRevision != Ledger.Revision || Ledger.Revision == long.MaxValue)
				return Fail("first-feast archive revision conflict", out Failure);
			KingdomExperienceLedger next = Clone(Ledger);
			KingdomFirstFeastReceipt archived = next.FirstFeasts[at];
			archived.Phase = KingdomFirstFeastPhase.Archived;
			archived.DecidedTick = ArchivedTick; next.Revision++;
			if (!TryValidate(next, out Failure)) return false;
			Ledger.CopyFrom(next); MutationCommitted = true;
			Receipt = archived.Copy(); return true;
		}

		internal static bool ValidateFirstFeasts(KingdomExperienceLedger Ledger,
			out string Failure)
		{
			Failure = null; string prior = null;
			for (int i = 0; i < Ledger.FirstFeasts.Count; i++)
			{
				KingdomFirstFeastReceipt row = Ledger.FirstFeasts[i];
				if (!KingdomFirstFeastRules.Valid(row) || !After(prior, row.SettlementId)
					|| !ReceiptOptionValid(Ledger, KingdomExperienceOptionKind.CivicStory,
						row.DeedTick, row.OfferedTick, row.EnableEpoch))
					return Fail("first-feast receipt is invalid", out Failure);
				for (int j = 0; j < i; j++)
				{
					KingdomFirstFeastReceipt other = Ledger.FirstFeasts[j];
					if (other.DeedId == row.DeedId)
						return Fail("one founding deed has two feast proposals", out Failure);
					if (!string.IsNullOrEmpty(row.PracticeId)
						&& row.PracticeId == other.PracticeId)
						return Fail("one feast practice has two receipts", out Failure);
				}
				prior = row.SettlementId;
			}
			return true;
		}

		internal static int FirstFeastIndex(KingdomExperienceLedger L, string SettlementId)
		{
			if (L?.FirstFeasts == null || SettlementId == null) return -1;
			for (int i = 0; i < L.FirstFeasts.Count; i++)
				if (L.FirstFeasts[i].SettlementId == SettlementId) return i;
			return -1;
		}
	}
}
