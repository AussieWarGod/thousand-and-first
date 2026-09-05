using System;

namespace ThousandAndFirst
{
	internal static class KingdomSubsidenceCompletionRules
	{
		internal static void Complete(Action CommitBookkeeping, Action ApplyReachedRungs,
			Action PresentSummary, Action<Exception> Diagnose)
		{
			if (CommitBookkeeping == null) throw new ArgumentNullException(nameof(CommitBookkeeping));
			if (ApplyReachedRungs == null) throw new ArgumentNullException(nameof(ApplyReachedRungs));
			if (PresentSummary == null) throw new ArgumentNullException(nameof(PresentSummary));
			CommitBookkeeping();
			ApplyReachedRungs();
			try { PresentSummary(); }
			catch (Exception error)
			{
				// Summary formatting and diagnostics can both invoke foreign code.
				try { Diagnose?.Invoke(error); }
				catch { }
			}
		}
	}
}
