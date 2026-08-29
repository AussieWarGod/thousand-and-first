using System;

namespace ThousandAndFirst
{
	public static partial class KingdomFirstFeastRules
	{
		public static bool TryPrepare(KingdomFirstFeastDeed Deed,
			KingdomFirstFeastCandidate[] Candidates, long OfferedTick, long EnableEpoch,
			out KingdomFirstFeastReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (!ValidDeed(Deed) || OfferedTick < Deed.DeedTick || EnableEpoch < 1L)
				return Fail("first-feast deed or offer timing is invalid", out Failure);
			if (!TryPair(Candidates, out KingdomFirstFeastCandidate proposer,
				out KingdomFirstFeastCandidate witness, out Failure)) return false;
			Receipt = new KingdomFirstFeastReceipt
			{
				Phase = KingdomFirstFeastPhase.Offered,
				Choice = KingdomFirstFeastChoice.None,
				Generation = 1,
				SettlementId = Deed.SettlementId,
				SettlementName = Deed.SettlementName,
				DeedId = Deed.DeedId,
				DeedText = Deed.DeedText,
				DeedTick = Deed.DeedTick,
				GuestTerminalReceiptId = Deed.GuestTerminalReceiptId,
				GuestTerminalDigest = Deed.GuestTerminalDigest,
				GuestTerminalTick = Deed.GuestTerminalTick,
				AdventureEventId = Deed.AdventureEventId,
				AdventureFingerprint = Deed.AdventureFingerprint,
				ProposerResidentId = proposer.ResidentId,
				ProposerName = proposer.Name,
				WitnessResidentId = witness.ResidentId,
				WitnessName = witness.Name,
				DishName = AuthoredDish,
				Ingredients = AuthoredIngredients,
				OfferedDedication = OfferedDedication,
				OfferedTick = OfferedTick,
				EnableEpoch = EnableEpoch
			};
			return Valid(Receipt) || Fail("prepared first-feast receipt is invalid", out Failure);
		}

		/// <summary>Affirmative and refusal choices close once. Defer returns the same receipt and
		/// Changed=false, so a caller cannot write a revision, action, timer, or expiry for it.</summary>
		public static bool TryDecide(KingdomFirstFeastReceipt Current,
			KingdomFirstFeastChoice Choice, string AdaptedDedication, long DecidedTick,
			out KingdomFirstFeastReceipt Next, out bool Changed, out string Failure)
		{
			Next = null; Changed = false; Failure = null;
			if (!Valid(Current) || Current.Phase == KingdomFirstFeastPhase.Quarantined)
				return Fail("first-feast receipt cannot accept a decision", out Failure);
			if (Current.Phase != KingdomFirstFeastPhase.Offered)
			{
				if (ExactDecision(Current, Choice, AdaptedDedication))
				{
					Next = Current.Copy(); return true;
				}
				return Fail("first-feast proposal is already closed", out Failure);
			}
			if (Choice == KingdomFirstFeastChoice.Defer)
			{
				if (!string.IsNullOrEmpty(AdaptedDedication))
					return Fail("defer cannot carry a dedication", out Failure);
				Next = Current.Copy(); return true;
			}
			if (DecidedTick < Current.OfferedTick)
				return Fail("first-feast decision predates its offer", out Failure);
			if (Choice != KingdomFirstFeastChoice.Adopt
				&& Choice != KingdomFirstFeastChoice.Adapt
				&& Choice != KingdomFirstFeastChoice.Refuse)
				return Fail("first-feast decision is invalid", out Failure);
			if (Choice == KingdomFirstFeastChoice.Adapt)
			{
				if (!IsAdaptation(AdaptedDedication))
					return Fail("adapted dedication is not authored", out Failure);
			}
			else if (!string.IsNullOrEmpty(AdaptedDedication))
				return Fail("only adapt may carry another dedication", out Failure);

			Next = Current.Copy(); Next.Choice = Choice; Next.DecidedTick = DecidedTick;
			if (Choice == KingdomFirstFeastChoice.Refuse)
			{
				Next.Phase = KingdomFirstFeastPhase.Refused;
			}
			else
			{
				if (!TryBuildPracticeId(Current.DeedId, out Next.PracticeId))
					return Fail("first-feast practice identity is unavailable", out Failure);
				Next.Phase = Choice == KingdomFirstFeastChoice.Adopt
					? KingdomFirstFeastPhase.Adopted : KingdomFirstFeastPhase.Adapted;
				Next.AdaptedDedication = Choice == KingdomFirstFeastChoice.Adapt
					? AdaptedDedication : null;
			}
			if (!Valid(Next)) return Fail("decided first-feast receipt is invalid", out Failure);
			Changed = true; return true;
		}

		public static bool SameOfferSource(KingdomFirstFeastReceipt A,
			KingdomFirstFeastReceipt B)
		{
			return Valid(A) && Valid(B) && A.Version == B.Version
				&& A.Generation == B.Generation && A.SettlementId == B.SettlementId
				&& A.SettlementName == B.SettlementName && A.DeedId == B.DeedId
				&& A.DeedText == B.DeedText && A.DeedTick == B.DeedTick
				&& A.GuestTerminalReceiptId == B.GuestTerminalReceiptId
				&& A.GuestTerminalDigest == B.GuestTerminalDigest
				&& A.GuestTerminalTick == B.GuestTerminalTick
				&& A.AdventureEventId == B.AdventureEventId
				&& A.AdventureFingerprint == B.AdventureFingerprint
				&& A.ProposerResidentId == B.ProposerResidentId
				&& A.ProposerName == B.ProposerName
				&& A.WitnessResidentId == B.WitnessResidentId
				&& A.WitnessName == B.WitnessName && A.DishName == B.DishName
				&& A.Ingredients == B.Ingredients
				&& A.OfferedDedication == B.OfferedDedication
				&& A.OfferedTick == B.OfferedTick && A.EnableEpoch == B.EnableEpoch;
		}

		private static bool ValidDeed(KingdomFirstFeastDeed D)
		{
			return D != null && KingdomExperienceRules.TypedId(D.SettlementId, "taf:settlement:")
				&& KingdomExperienceRules.CivicText(D.SettlementName, true)
				&& D.DeedText == AuthoredDeed && D.DeedTick > D.GuestTerminalTick
				&& ExactDeedIdentity(D);
		}

		private static bool ExactDeedIdentity(KingdomFirstFeastDeed D)
		{
			string supplied = D.DeedId;
			return TryBuildDeedId(D, out string expected) && supplied == expected;
		}

		private static bool TryPair(KingdomFirstFeastCandidate[] Candidates,
			out KingdomFirstFeastCandidate Proposer, out KingdomFirstFeastCandidate Witness,
			out string Failure)
		{
			Proposer = default(KingdomFirstFeastCandidate);
			Witness = default(KingdomFirstFeastCandidate); Failure = null;
			if (Candidates == null || Candidates.Length < 2 || Candidates.Length > MaxCandidates)
				return Fail("two bounded standing residents are required", out Failure);
			KingdomFirstFeastCandidate[] rows =
				(KingdomFirstFeastCandidate[])Candidates.Clone();
			Array.Sort(rows, (A, B) => A.ResidentId.CompareTo(B.ResidentId));
			for (int i = 0; i < rows.Length; i++)
				if (rows[i].ResidentId <= 0
					|| !KingdomExperienceRules.CivicText(rows[i].Name, true)
					|| i > 0 && rows[i - 1].ResidentId == rows[i].ResidentId)
					return Fail("first-feast resident evidence is invalid", out Failure);
			Proposer = rows[0]; Witness = rows[1]; return true;
		}

		private static bool ExactDecision(KingdomFirstFeastReceipt R,
			KingdomFirstFeastChoice Choice, string Adaptation)
		{
			if (R.Choice != Choice) return false;
			return Choice == KingdomFirstFeastChoice.Adapt
				? R.AdaptedDedication == Adaptation : string.IsNullOrEmpty(Adaptation);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
