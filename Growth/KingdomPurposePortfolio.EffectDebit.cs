using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static KingdomPurposeBodyDriveState DrivePurposeEffectDebit(
			KingdomPurposeEffectRuntimeContext Context, KingdomPurposeOperationReceipt Operation,
			KingdomPurposeEffectCallbackKind Callback, out int NextStep, out string Failure)
		{
			NextStep = Operation == null ? 0 : Operation.EffectStep;
			Failure = null;
			if (Context == null || Operation == null
				|| !TryPurposeEffectScope(Operation, out string receipt, out _))
				return KingdomPurposeBodyDriveState.Invalid;
			if (PurposeEffectIsFaulted(Context.Work))
				return InvalidEffect("A durable bounded-effect fault already stands.", out Failure);
			if (!PurposeEffectEvidenceOnlyOnWorkOrProducts(Context, out _, out Failure)
				|| !TryReadPurposeEffectAttempt(Context.Work, receipt,
					out KingdomPurposeEffectAttempt attempt, out bool present))
				return FaultedEffect(Context.Work, receipt, Operation.EffectStep,
					"debit-evidence", Failure ?? "The debit evidence is torn.", out Failure);

			if (present && attempt.Step < Operation.EffectStep)
			{
				if (!TryRetirePublishedEffectAttempt(Context, Operation, null, out Failure))
					return FaultedEffect(Context.Work, receipt, Operation.EffectStep,
						"retire-debit", Failure, out Failure);
				present = false;
				attempt = null;
			}
			if (present && (attempt.Step != Operation.EffectStep
				|| attempt.Callback != Callback))
				return FaultedEffect(Context.Work, receipt, Operation.EffectStep,
					"attempt-step", "The debit attempt names another boundary.", out Failure);

			if (!present)
			{
				if (!TryPurposeEffectDebitCensus(Context, Callback, out int total,
					out GameObject selected, out KingdomPurposeEffectRosterSnapshot before,
					out Failure)) return InvalidEffect(Failure, out Failure);
				if (selected == null) return WaitingEffect(MissingDebit(Context, Callback), out Failure);
				if (!TryPurposeEffectExpectedDebitAfter(before, selected.IDIfAssigned,
					out string afterDigest, out Failure)
					|| !KingdomPurposePortfolioRules.TryEffectAttempt(receipt,
						Operation.EffectStep, Callback, selected.IDIfAssigned, selected.Count,
						total, 0, before.Digest, afterDigest, out string witness)
					|| !StampPurposeEffectAttempt(Context.Work, witness)
					|| !KingdomPurposePortfolioRules.TryReadEffectAttempt(witness, receipt,
						out attempt))
					return FaultedEffect(Context.Work, receipt, Operation.EffectStep,
						"attempt-publish", Failure ?? "The debit attempt did not persist.",
						out Failure);
			}

			string encoded = KingdomPurposePortfolioRules.EncodeEffectAttempt(attempt);
			if (!EnsurePurposeEffectDebitReservation(Context, attempt, encoded, out Failure))
				return FaultedEffect(Context.Work, receipt, Operation.EffectStep,
					"debit-reservation", Failure, out Failure);
			if (!ObservePurposeEffectDebit(Context, attempt, out bool beforeExact,
				out bool afterExact, out Failure))
				return FaultedEffect(Context.Work, receipt, Operation.EffectStep,
					"attempt-observation", Failure, out Failure);
			KingdomPurposeEffectAttemptState state =
				KingdomPurposePortfolioRules.ClassifyEffectAttempt(true, true,
					beforeExact, afterExact, false);
			if (state == KingdomPurposeEffectAttemptState.Settled)
			{
				if (!ExactPurposeEffectOffer(Context.Work, encoded))
					return FaultedEffect(Context.Work, receipt, Operation.EffectStep,
						"debit-unoffered", "A physical debit appeared without its owned callback offer.",
						out Failure);
				NextStep = Operation.EffectStep + 1;
				return KingdomPurposeBodyDriveState.Applied;
			}
			if (state != KingdomPurposeEffectAttemptState.Before
				|| FindPortfolioObject(attempt.ObjectId, out GameObject item,
					out bool graveyard) != KingdomPhysicalLookupState.Exact || graveyard)
				return FaultedEffect(Context.Work, receipt, Operation.EffectStep,
					"attempt-aftermath", "The reserved debit has an unknown aftermath.",
					out Failure);
			if (!DebitCandidateStillAvailable(Context, item, Callback, encoded, out Failure))
				return FaultedEffect(Context.Work, receipt, Operation.EffectStep,
					"reserved-shape", Failure, out Failure);
			if (!StampPurposeEffectOffer(Context.Work, encoded))
				return FaultedEffect(Context.Work, receipt, Operation.EffectStep,
					"debit-offer", "The exact callback offer could not persist.", out Failure);

			bool threw = false;
			try { item.Destroy(null, Silent: true); }
			catch (Exception error)
			{
				threw = true;
				Failure = "The bounded-effect debit callback threw ("
					+ error.GetType().Name + ").";
			}
			KingdomSurvey.ObserveChangedInActive(Context.Zone, Context.Store);
			bool observed = ObservePurposeEffectDebit(Context, attempt,
				out beforeExact, out afterExact, out string observationFailure);
			KingdomPurposeEffectCallbackAftermath aftermath =
				KingdomPurposePortfolioRules.ClassifyEffectDebitAftermath(true, threw,
					observed && beforeExact, observed && afterExact);
			if (aftermath == KingdomPurposeEffectCallbackAftermath.Settled)
			{
				NextStep = Operation.EffectStep + 1;
				return KingdomPurposeBodyDriveState.Applied;
			}
			if (aftermath == KingdomPurposeEffectCallbackAftermath.Unavailable)
			{
				if (!ClearPurposeEffectOffer(Context.Work, encoded))
					return FaultedEffect(Context.Work, receipt, Operation.EffectStep,
						"debit-offer-retire", "The no-change callback offer could not retire.",
						out Failure);
				return WaitingEffect("The exact reserved debit made no physical change; retry it.",
					out Failure);
			}
			return FaultedEffect(Context.Work, receipt, Operation.EffectStep,
				"debit-aftermath", Failure ?? observationFailure
					?? "The debit callback reached an unknown aftermath.", out Failure);
		}
	}
}
