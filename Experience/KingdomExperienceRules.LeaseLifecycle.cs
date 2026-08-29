using System;

namespace ThousandAndFirst
{
	/// <summary>Bounded read surface for source recovery. Returned rows are defensive copies.</summary>
	public static partial class KingdomExperienceRules
	{
		public static bool TryReadAudienceLease(KingdomExperienceLedger Ledger,
			string ReservationId, out KingdomExperienceAudienceReceipt Receipt,
			out KingdomExperienceLeaseState State, out string Failure)
		{
			Receipt = null; State = KingdomExperienceLeaseState.Missing; Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			if (!TypedId(ReservationId, "taf:experience-audience:"))
				return Fail("audience lease identity is invalid", out Failure);
			int index = AudienceIndex(Ledger, ReservationId);
			if (index < 0) return true;
			Receipt = Copy(Ledger.Audiences[index]);
			State = CurrentLease(Ledger, Receipt.OptionKind, Receipt.CauseTick,
				Receipt.ReservedTick, Receipt.EnableEpoch)
				? KingdomExperienceLeaseState.Active : KingdomExperienceLeaseState.Retirement;
			return true;
		}

		public static bool TryReadBodyLease(KingdomExperienceLedger Ledger,
			string ReservationId, out KingdomExperienceBodyReservation Receipt,
			out KingdomExperienceLeaseState State, out string Failure)
		{
			Receipt = null; State = KingdomExperienceLeaseState.Missing; Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			if (!TypedId(ReservationId, "taf:experience-body:"))
				return Fail("body lease identity is invalid", out Failure);
			int index = BodyIndex(Ledger, ReservationId);
			if (index < 0) return true;
			Receipt = Copy(Ledger.BodyReservations[index]);
			State = CurrentLease(Ledger, Receipt.OptionKind, Receipt.CauseTick,
				Receipt.ReservedTick, Receipt.EnableEpoch)
				? KingdomExperienceLeaseState.Active : KingdomExperienceLeaseState.Retirement;
			return true;
		}

		/// <summary>Classifies an owner's exact persisted option triple without creating a row.
		/// This is the only supported missing-lease recovery classifier.</summary>
		public static bool TryClassifyLeaseProof(KingdomExperienceLedger Ledger,
			KingdomExperienceOptionKind Kind, long CauseTick, long ReservedTick,
			long EnableEpoch, out KingdomExperienceLeaseState State, out string Failure)
		{
			State = KingdomExperienceLeaseState.Missing; Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			if (!DefinedOption(Kind) || CauseTick < 0L || ReservedTick < CauseTick
				|| EnableEpoch < 1L || !ReceiptOptionValid(Ledger, Kind, CauseTick,
					ReservedTick, EnableEpoch))
				return Fail("experience lease proof is invalid", out Failure);
			State = CurrentLease(Ledger, Kind, CauseTick, ReservedTick, EnableEpoch)
				? KingdomExperienceLeaseState.Active : KingdomExperienceLeaseState.Retirement;
			return true;
		}

		private static bool CurrentLease(KingdomExperienceLedger Ledger,
			KingdomExperienceOptionKind Kind, long CauseTick, long ReservedTick, long Epoch)
		{
			KingdomExperienceOptionReceipt option = OptionFor(Ledger, Kind);
			return option != null && option.State == KingdomExperienceOptionState.Enabled
				&& Epoch == option.EnableEpoch && CauseTick >= option.FutureCauseFloorTick
				&& ReservedTick >= option.ObservedTick;
		}
	}
}
