using System;

namespace ThousandAndFirst
{
	internal static partial class KingdomLabCivicRules
	{
		internal static bool Valid(KingdomLabCivicReceipt R, out string Failure)
		{
			Failure = null;
			if (R == null || R.Version != CurrentVersion
				|| R.Kind == KingdomLabCivicKind.None || R.Kind > KingdomLabCivicKind.RefusalDeparture
				|| R.Phase == KingdomLabCivicPhase.None || R.Phase > KingdomLabCivicPhase.Quarantined
				|| R.Request == KingdomLabCivicRequest.None || R.Request > KingdomLabCivicRequest.RoofRefusal
				|| !Text(R.RealmId) || !Text(R.SettlementId) || !Text(R.ZoneId)
				|| !Text(R.OwnerObjectId) || !Text(R.SubjectObjectId)
				|| R.SubjectResidentId <= 0 || !Text(R.SubjectName)
				|| R.CreatedTick < 0L || R.ClosedTick < 0L
				|| R.CloseRecorded && R.Phase != KingdomLabCivicPhase.Closed
				|| !Text(R.CauseDigest) || R.CauseDigest.Length != 64
				|| !string.Equals(R.CauseDigest, Digest(R), StringComparison.Ordinal)
				|| !string.Equals(R.EventId, "taf:lab-civic:" + ((int)R.Kind) + ":"
					+ R.CauseDigest.Substring(0, 24), StringComparison.Ordinal))
				return Fail("The civic receipt identity or schema is malformed.", out Failure);
			if (R.Kind == KingdomLabCivicKind.SavantPrice && !ValidSavant(R))
				return Fail("The savant-price evidence is incomplete.", out Failure);
			if (R.Kind == KingdomLabCivicKind.RefusalDeparture && !ValidDeparture(R))
				return Fail("The authored-refusal evidence is incomplete.", out Failure);
			if (!ValidPhase(R)) return Fail("The civic receipt phase is unlawful.", out Failure);
			return true;
		}

		private static bool ValidSavant(KingdomLabCivicReceipt R)
		{
			bool baseFacts = R.Request != KingdomLabCivicRequest.RoofRefusal
				&& Text(R.SubjectCreed) && Text(R.CityCreed)
				&& !string.Equals(R.SubjectCreed, R.CityCreed, StringComparison.OrdinalIgnoreCase)
				&& NotableLodgeReceipt(R.NotableLodgeReceiptId)
				&& R.TasteOrdinal > 0L && R.TasteSource == TasteOrdinalSource
				&& R.TasteIndex >= 0 && R.TasteIndex < 10 && Text(R.TasteTag)
				&& R.Request == RequestForTaste(R.TasteIndex) && Text(R.SourcePlotId)
				&& Text(R.SourceHomeName)
				&& Absent(R.RefusedTag);
			if (!baseFacts) return false;
			if (R.Request == KingdomLabCivicRequest.ShrineUnconsecrated)
				return Text(R.TargetObjectId) && Absent(R.TargetHomeObjectId)
					&& R.TargetResidentId == 0
					&& Text(R.TargetName) && Absent(R.TargetPlotId)
					&& Absent(R.TargetHomeName);
			return R.Request == KingdomLabCivicRequest.NeighbourRehoused
				&& R.TargetResidentId > 0 && Text(R.TargetObjectId)
				&& Text(R.TargetHomeObjectId) && Text(R.TargetName)
				&& Text(R.SourcePlotId) && Text(R.TargetPlotId)
				&& Text(R.TargetHomeName)
				&& !string.Equals(R.SourcePlotId, R.TargetPlotId, StringComparison.Ordinal);
		}

		private static bool ValidDeparture(KingdomLabCivicReceipt R)
		{
			return R.Request == KingdomLabCivicRequest.RoofRefusal
				&& Text(R.SourcePlotId) && Text(R.RefusedTag)
				&& Absent(R.SubjectCreed) && Absent(R.CityCreed)
				&& Absent(R.NotableLodgeReceiptId) && R.TasteOrdinal == 0L
				&& Absent(R.TasteSource) && R.TasteIndex == 0
				&& Absent(R.TasteTag) && Absent(R.TargetObjectId)
				&& Absent(R.TargetHomeObjectId)
				&& R.TargetResidentId == 0 && Absent(R.TargetName)
				&& Absent(R.TargetPlotId) && Absent(R.SourceHomeName)
				&& Absent(R.TargetHomeName);
		}

		private static bool ValidPhase(KingdomLabCivicReceipt R)
		{
			if (R.Phase == KingdomLabCivicPhase.Quarantined)
				return Text(R.Fault) && R.ClosedTick == 0L && R.Closure == KingdomLabCivicClosure.None;
			if (!Absent(R.Fault)) return false;
			if (R.Phase == KingdomLabCivicPhase.Prepared)
				return R.Kind == KingdomLabCivicKind.SavantPrice
					&& R.Choice == KingdomLabCivicChoice.None
					&& R.Closure == KingdomLabCivicClosure.None && R.ClosedTick == 0L;
			if (R.Phase == KingdomLabCivicPhase.ChoicePrepared)
				return R.Kind == KingdomLabCivicKind.SavantPrice
					&& R.Request == KingdomLabCivicRequest.NeighbourRehoused
					&& R.Choice == KingdomLabCivicChoice.Granted
					&& R.Closure == KingdomLabCivicClosure.None && R.ClosedTick == 0L;
			if (R.Phase == KingdomLabCivicPhase.Active)
				return R.Closure == KingdomLabCivicClosure.None && R.ClosedTick == 0L
					&& (R.Kind == KingdomLabCivicKind.RefusalDeparture
						? R.Choice == KingdomLabCivicChoice.None
						: R.Request == KingdomLabCivicRequest.ShrineUnconsecrated
							&& R.Choice == KingdomLabCivicChoice.Granted);
			if (R.Phase != KingdomLabCivicPhase.Closed || R.ClosedTick < R.CreatedTick
				|| R.Closure == KingdomLabCivicClosure.None) return false;
			if (R.Closure == KingdomLabCivicClosure.Refused)
				return R.Kind == KingdomLabCivicKind.SavantPrice
					&& R.Choice == KingdomLabCivicChoice.Refused;
			if (R.Closure == KingdomLabCivicClosure.Rehoused)
				return R.Choice == (R.Kind == KingdomLabCivicKind.SavantPrice
					? KingdomLabCivicChoice.Granted : KingdomLabCivicChoice.None)
					&& (R.Kind != KingdomLabCivicKind.SavantPrice
						|| R.Request == KingdomLabCivicRequest.NeighbourRehoused);
			if (R.Kind == KingdomLabCivicKind.SavantPrice)
				return (R.Closure == KingdomLabCivicClosure.CauseGone
					|| R.Closure == KingdomLabCivicClosure.OwnerGone)
					&& (R.Choice == KingdomLabCivicChoice.None
						|| R.Choice == KingdomLabCivicChoice.Granted);
			return R.Choice == KingdomLabCivicChoice.None
				&& (R.Closure == KingdomLabCivicClosure.Departed
					|| R.Closure == KingdomLabCivicClosure.CauseGone
					|| R.Closure == KingdomLabCivicClosure.OwnerGone);
		}

		private static bool Text(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return false;
			return Value.Length <= MaxTextChars && Value.Trim().Length == Value.Length;
		}

		private static bool Absent(string Value)
		{
			return string.IsNullOrEmpty(Value);
		}

		private static bool NotableLodgeReceipt(string Value)
		{
			const string prefix = "taf:operation:";
			if (string.IsNullOrEmpty(Value) || !Value.StartsWith(prefix,
				StringComparison.Ordinal) || Value.Length != prefix.Length + 64) return false;
			for (int i = prefix.Length; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9')
					|| (Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
