using System;

namespace ThousandAndFirst
{
	internal static partial class KingdomLabCivicRules
	{
		internal static bool TryChoose(KingdomLabCivicReceipt Before, bool Grant,
			long Tick, out KingdomLabCivicReceipt After, out string Failure)
		{
			After = null;
			if (!Valid(Before, out Failure)) return false;
			if (Before.Phase == KingdomLabCivicPhase.Closed)
			{
				KingdomLabCivicChoice expected = Grant
					? KingdomLabCivicChoice.Granted : KingdomLabCivicChoice.Refused;
				if (Before.Choice == expected) { After = Before.Copy(); return true; }
				return Fail("The savant's answered request is immutable.", out Failure);
			}
			if (Before.Kind != KingdomLabCivicKind.SavantPrice
				|| Before.Phase != KingdomLabCivicPhase.Prepared)
				return Fail("Only a prepared savant request can be answered.", out Failure);
			After = Before.Copy();
			After.Choice = Grant ? KingdomLabCivicChoice.Granted
				: KingdomLabCivicChoice.Refused;
			if (!Grant)
			{
				After.Phase = KingdomLabCivicPhase.Closed;
				After.Closure = KingdomLabCivicClosure.Refused;
				After.ClosedTick = Math.Max(Before.CreatedTick, Tick);
			}
			else After.Phase = Before.Request == KingdomLabCivicRequest.ShrineUnconsecrated
				? KingdomLabCivicPhase.Active : KingdomLabCivicPhase.ChoicePrepared;
			return Valid(After, out Failure);
		}

		internal static bool TryClose(KingdomLabCivicReceipt Before,
			KingdomLabCivicClosure Closure, long Tick, out KingdomLabCivicReceipt After,
			out string Failure)
		{
			After = null;
			if (!Valid(Before, out Failure)) return false;
			if (Closure == KingdomLabCivicClosure.None
				|| Closure == KingdomLabCivicClosure.Refused)
				return Fail("That is not a runtime closure.", out Failure);
			if (Before.Phase == KingdomLabCivicPhase.Closed)
			{
				if (Before.Closure == Closure) { After = Before.Copy(); return true; }
				return Fail("The civic closure is immutable.", out Failure);
			}
			bool allowed = Before.Kind == KingdomLabCivicKind.RefusalDeparture
				? Before.Phase == KingdomLabCivicPhase.Active
					&& (Closure == KingdomLabCivicClosure.Rehoused
						|| Closure == KingdomLabCivicClosure.Departed
						|| Closure == KingdomLabCivicClosure.CauseGone
						|| Closure == KingdomLabCivicClosure.OwnerGone)
				: (Before.Phase == KingdomLabCivicPhase.Prepared
					|| Before.Phase == KingdomLabCivicPhase.ChoicePrepared
					|| Before.Phase == KingdomLabCivicPhase.Active)
					&& (Closure == KingdomLabCivicClosure.Rehoused
						&& Before.Request == KingdomLabCivicRequest.NeighbourRehoused
						|| Closure == KingdomLabCivicClosure.CauseGone
						|| Closure == KingdomLabCivicClosure.OwnerGone);
			if (!allowed) return Fail("That closure does not follow this cause.", out Failure);
			After = Before.Copy();
			After.Phase = KingdomLabCivicPhase.Closed;
			After.Closure = Closure;
			After.ClosedTick = Math.Max(Before.CreatedTick, Tick);
			return Valid(After, out Failure);
		}

		internal static KingdomLabCivicReceipt Quarantine(KingdomLabCivicReceipt Before,
			string Reason)
		{
			if (Before == null) return null;
			KingdomLabCivicReceipt after = Before.Copy();
			after.Phase = KingdomLabCivicPhase.Quarantined;
			after.Choice = KingdomLabCivicChoice.None;
			after.Closure = KingdomLabCivicClosure.None;
			after.ClosedTick = 0L;
			after.Fault = Bounded(Reason, "The civic receipt diverged.");
			return after;
		}

		internal static KingdomLabDepartureProjection ClassifyDepartureProjection(
			KingdomLabCivicReceipt Receipt, string CurrentPlot, string EventId,
			string OwnerId, string Digest)
		{
			if (!Valid(Receipt, out _) || Receipt.Kind != KingdomLabCivicKind.RefusalDeparture
				|| Receipt.Phase != KingdomLabCivicPhase.Active)
				return KingdomLabDepartureProjection.Diverged;
			bool compatible = MarkerFieldCompatible(EventId, Receipt.EventId)
				&& MarkerFieldCompatible(OwnerId, Receipt.OwnerObjectId)
				&& MarkerFieldCompatible(Digest, Receipt.CauseDigest);
			if (string.Equals(CurrentPlot, Receipt.SourcePlotId, StringComparison.Ordinal)
				&& compatible) return KingdomLabDepartureProjection.RecoverableAtSource;
			bool exact = string.Equals(EventId, Receipt.EventId, StringComparison.Ordinal)
				&& string.Equals(OwnerId, Receipt.OwnerObjectId, StringComparison.Ordinal)
				&& string.Equals(Digest, Receipt.CauseDigest, StringComparison.Ordinal);
			return string.IsNullOrEmpty(CurrentPlot) && exact
				? KingdomLabDepartureProjection.Active
				: KingdomLabDepartureProjection.Diverged;
		}

		internal static bool ClosedMarkerCleanupAllowed(KingdomLabCivicReceipt Receipt,
			string EventId, string OwnerId, string Digest)
		{
			return Valid(Receipt, out _)
				&& Receipt.Kind == KingdomLabCivicKind.RefusalDeparture
				&& Receipt.Phase == KingdomLabCivicPhase.Closed
				&& MarkerFieldCompatible(EventId, Receipt.EventId)
				&& MarkerFieldCompatible(OwnerId, Receipt.OwnerObjectId)
				&& MarkerFieldCompatible(Digest, Receipt.CauseDigest);
		}

		internal static KingdomLabObjectMatch ClassifyObjectMatches(int Matches)
		{
			return Matches <= 0 ? KingdomLabObjectMatch.Missing
				: Matches == 1 ? KingdomLabObjectMatch.Unique
				: KingdomLabObjectMatch.Duplicate;
		}

		private static bool MarkerFieldCompatible(string Actual, string Expected)
		{
			return string.IsNullOrEmpty(Actual)
				|| string.Equals(Actual, Expected, StringComparison.Ordinal);
		}

		private static string Bounded(string Value, string Fallback)
		{
			string value = string.IsNullOrWhiteSpace(Value) ? Fallback : Value.Trim();
			return value.Length <= MaxTextChars ? value : value.Substring(0, MaxTextChars);
		}
	}
}
