#if !TAF_TESTS
using System;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		/// <summary>Read-only bridge for a commission that must bind one stable reign.</summary>
		internal bool TryReadStableRulerOrdinal(out int Ordinal, out string Failure)
		{
			Ordinal = -1;
			Failure = null;
			if (!KingdomSuccessionRules.SuccessionEnabled(LoadFailed, SuccessionDisabled))
				return Fail("succession authority is disabled or unreadable", out Failure);
			if (!KingdomSuccessionRules.TryValidateSavedState(SuccessionOrdinal,
				PendingDeathToken, CompletedDeathToken, PendingPhase, PendingDueTick,
				PendingRoad, PendingDays, PendingAccessionRepairResidentId != 0,
				PendingSealAccessionToken, out Failure)) return false;
			if (!string.IsNullOrEmpty(PendingDeathToken)
				|| PendingPhase == InterregnumPhase.WordOnTheRoad
				|| PendingPhase == InterregnumPhase.RiteDue
				|| PendingAccessionRepairResidentId != 0)
				return Fail("the ruler life is crossing an interregnum", out Failure);
			if (SuccessionOrdinal == 0)
			{
				if (PendingPhase != InterregnumPhase.None
					|| !string.IsNullOrEmpty(CompletedDeathToken))
					return Fail("the first ruler life is incoherent", out Failure);
			}
			else
			{
				if (PendingPhase != InterregnumPhase.Reigning
					|| !KingdomSuccessionRules.TryReadDeathToken(CompletedDeathToken,
						out int completed, out long _) || completed != SuccessionOrdinal)
					return Fail("the reigning ruler life is incoherent", out Failure);
			}
			Ordinal = SuccessionOrdinal;
			return true;
		}

		private static bool Fail(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}
	}
}
#endif
