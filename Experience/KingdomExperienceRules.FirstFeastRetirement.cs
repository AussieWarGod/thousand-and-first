namespace ThousandAndFirst
{
	public static partial class KingdomExperienceRules
	{
		/// <summary>Bodyless semantic retirement. No recipe, locus, or other projection is owned.</summary>
		public static bool TryRetireFirstFeasts(KingdomExperienceLedger Ledger,
			string RealmId, long ExpectedRevision, out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure)
				|| !string.Equals(Ledger.RealmId, RealmId, System.StringComparison.Ordinal))
				return Fail(Failure ?? "first-feast retirement belongs to another realm", out Failure);
			if (Ledger.FirstFeasts.Count == 0) return true;
			if (ExpectedRevision != Ledger.Revision || Ledger.Revision == long.MaxValue)
				return Fail("first-feast retirement revision is unavailable", out Failure);
			KingdomExperienceLedger next = Clone(Ledger);
			next.FirstFeasts.Clear(); next.Revision++;
			if (!TryValidate(next, out Failure)) return false;
			Ledger.CopyFrom(next); return true;
		}
	}
}
