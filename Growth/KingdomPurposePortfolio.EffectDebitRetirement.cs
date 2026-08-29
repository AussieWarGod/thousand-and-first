using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static bool TryRetirePublishedDebitReservation(
			KingdomPurposeEffectRuntimeContext Context,
			KingdomPurposeEffectAttempt Attempt, out string Failure)
		{
			Failure = null;
			string witness = KingdomPurposePortfolioRules.EncodeEffectAttempt(Attempt);
			if (string.IsNullOrEmpty(witness)) return false;
			bool ready = OwnedFieldPresent(Context.Work, PortfolioEffectReadyProperty);
			bool offered = OwnedFieldPresent(Context.Work, PortfolioEffectOfferProperty);
			if (ready && !ExactPurposeEffectReady(Context.Work, witness)
				|| offered && !ExactPurposeEffectOffer(Context.Work, witness))
				return Fail("The published debit checkpoints are torn.", out Failure);
			KingdomPhysicalLookupState state = FindPortfolioObject(Attempt.ObjectId,
				out GameObject item, out bool graveyard);
			if (state == KingdomPhysicalLookupState.Ambiguous) return false;
			bool reserved = state == KingdomPhysicalLookupState.Exact
				&& ExactPurposeEffectDebitReservation(item, witness);
			KingdomPurposeEffectRosterMode mode = reserved && !graveyard
				? KingdomPurposeEffectRosterMode.DebitReserved
				: KingdomPurposeEffectRosterMode.Exact;
			if (!TryCapturePurposeEffectRoster(Context, reserved && !graveyard
				? Attempt.ObjectId : null, mode, reserved && !graveyard ? witness : null,
				null, 0, out KingdomPurposeEffectRosterSnapshot roster, out Failure)
				|| roster.Digest != Attempt.AfterRosterDigest)
				return Fail(Failure ?? "The published debit no longer has its exact after roster.",
					out Failure);
			if (state == KingdomPhysicalLookupState.Exact && !graveyard)
			{
				if (Attempt.BeforeCount <= 1 || item.Count != Attempt.BeforeCount - 1)
					return Fail("The published debit survivor has the wrong count.", out Failure);
				if (reserved && !ClearPurposeEffectAttempt(item, witness))
					return Fail("The published debit reservation could not retire.", out Failure);
			}
			else if (Attempt.BeforeCount != 1)
				return Fail("The published debit target disappeared at a nonterminal stack count.",
					out Failure);
			else if (state == KingdomPhysicalLookupState.Exact && reserved
				&& !ClearPurposeEffectAttempt(item, witness)) return false;
			return ClearPurposeEffectOffer(Context.Work, witness)
				&& ClearPurposeEffectReady(Context.Work, witness)
				&& ClearPurposeEffectAttempt(Context.Work, witness)
				|| Fail("The published debit evidence could not retire idempotently.", out Failure);
		}
	}
}
