namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		/// <summary>True only for an identity-unbound book whose whole state survives the
		/// growth-free v5 frame unchanged: pristine growth, no lane operations, resources or
		/// proofs, a constructor-default raid ledger, and either the pristine shape or the
		/// canonical unbound quarantine. The v5 reader rebuilds exactly this state (#272).</summary>
		internal static bool DormantLifecycleWireExact(KingdomLifecycleBook book)
		{
			if (book == null || book.WireRejected || book.FormatVersion != CurrentFormatVersion
				|| book.IdentityBound || !PristineGrowthBook(book.Growth)
				|| book.PlainGuest != null || book.NotableGuest != null
				|| book.Raid != null || book.Petition != null
				|| book.Resources == null || book.Resources.Count != 0
				|| book.RecentProofs == null || book.RecentProofs.Count != 0
				|| !ConstructorDefaultRaidLedger(book.RaidLedger)) return false;
			return PristineLifecycleBook(book) || CanonicalLifecycleQuarantine(book);
		}

		private static bool ConstructorDefaultRaidLedger(KingdomRaidLedger ledger)
		{
			return ledger != null && ledger.Version == KingdomRaidLedger.CurrentVersion
				&& ledger.StateRevision == 0L && ledger.ScheduleRevision == 0L
				&& ledger.Grievances != null && ledger.Grievances.Count == 0
				&& ledger.Incidents != null && ledger.Incidents.Count == 0
				&& ledger.ActiveIncidentId == null && !ledger.LegacyEvidenceArchived
				&& ledger.LegacyRaidState == 0 && ledger.LegacyFaction == null
				&& ledger.LegacyDueTick == 0L && ledger.LegacyLastTick == 0L
				&& ledger.LegacyTimesDeferred == 0 && ledger.OpaqueFuturePayload == null;
		}
	}
}
