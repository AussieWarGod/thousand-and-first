namespace ThousandAndFirst
{
	public sealed partial class KingdomSeal
	{
		/// <summary>Closes only the seal's own pending write before C2 freezes new work.</summary>
		internal bool TryPrepareRealmRemoval(out string Failure)
		{
			Failure = null;
			if (LoadFailed || !AuthorityEnabled)
			{
				Failure = "profile seal authority is unavailable or unreadable";
				return false;
			}
			if (FlushInProgress || ReconcileInProgress
				|| !string.IsNullOrEmpty(PendingAccessionToken))
			{
				Failure = "profile seal has an accession, flush, or reconciliation in flight";
				return false;
			}
			if (Dirty && !TryFlushLiving("prepare save for realm removal",
				ProbeEvenIfClean: true, out Failure)) return false;
			if (FlushInProgress || ReconcileInProgress || Dirty
				|| !string.IsNullOrEmpty(PendingAccessionToken))
			{
				Failure = "profile seal did not become quiescent";
				return false;
			}
			return true;
		}
	}
}
