using System;

namespace ThousandAndFirst.Harness
{
	// Expected report counts only; this grants no release, delivery, or retirement authority.
	internal static class KingdomSubsidenceRungSaveForecast
	{
		internal static bool TryClosingBatch(KingdomSubsidenceStepBook book,
			out KingdomSubsidenceBatch projected)
		{
			projected = null;
			if (!KingdomSubsidenceStepRules.Valid(book) || book.Active == null
				|| book.Active.Phase != KingdomSubsidenceStepPhase.Settling || book.Active.Completed <= 0
				|| book.Active.PendingDepartureId != "" || book.Active.CancelRequested
				|| book.OptionModel != KingdomSubsidenceStepRules.NoOption
				|| !KingdomSubsidenceBatchCodec.TryDecode(book.BatchModel, out KingdomSubsidenceBatch batch)
				|| !KingdomSubsidenceBatchRules.MatchesShape(batch, book)) return false;
			int departed;
			try { departed = checked(batch.Departed + book.Active.Completed); }
			catch (OverflowException) { return false; }
			if (book.Active.DueTick < batch.ThroughTick && departed < batch.Wanted) return false;
			KingdomSubsidenceBatch closing = batch.Copy(departed: departed, closing: true,
				closedTick: book.Active.DueTick);
			if (!KingdomSubsidenceBatchRules.Valid(closing)) return false;
			projected = closing;
			return true;
		}
	}
}
