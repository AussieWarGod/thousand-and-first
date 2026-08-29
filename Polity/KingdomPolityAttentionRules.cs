namespace ThousandAndFirst
{
	/// <summary>One cross-role polity attention budget; plans are reservations, not armies.</summary>
	public static class KingdomPolityAttentionRules
	{
		public const int MaximumTransientBodies = 7;
		public const int MaximumUnsolicitedCohorts = 3;
		public const int MaximumActiveNamedFigures = 4;

		public static bool TryAdmitPlan(KingdomPolityLedger Ledger, int MemberCount,
			out string Failure)
		{
			Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || MemberCount < 1 ||
				MemberCount > KingdomPolityRules.MaxCohortMembers) return false;
			int bodies = 0, cohorts = 0;
			for (int i = 0; i < Ledger.Cohorts.Count; i++)
			{
				KingdomPolityCohortPlan row = Ledger.Cohorts[i];
				if (!Reserves(row)) continue;
				bodies += row.ScaleBudget; cohorts++;
			}
			if (bodies > MaximumTransientBodies - MemberCount ||
				cohorts >= MaximumUnsolicitedCohorts)
			{
				Failure = "shared polity transient-body or presentation budget is full"; return false;
			}
			return true;
		}

		public static bool TryAdmitManifestation(KingdomPolityLedger Ledger,
			KingdomPolityCohortPlan Candidate, out string Failure)
		{
			Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || Candidate == null)
				return false;
			int bodies = 0, cohorts = 0;
			for (int i = 0; i < Ledger.Cohorts.Count; i++)
			{
				KingdomPolityCohortPlan row = Ledger.Cohorts[i];
				if (row.CohortId == Candidate.CohortId || !Occupies(row)) continue;
				bodies += row.ScaleBudget; cohorts++;
			}
			if (bodies > MaximumTransientBodies - Candidate.ScaleBudget ||
				cohorts >= MaximumUnsolicitedCohorts)
			{
				Failure = "shared polity manifestation budget is full"; return false;
			}
			return true;
		}

		public static int ActiveNamedFigures(KingdomPolityLedger Ledger, string PolityId)
		{
			int count = 0;
			for (int i = 0; Ledger != null && i < Ledger.NamedFigures.Count; i++)
				if (Ledger.NamedFigures[i].PolityId == PolityId &&
					Ledger.NamedFigures[i].Phase == KingdomPolityFigurePhase.Active) count++;
			return count;
		}

		private static bool Reserves(KingdomPolityCohortPlan C)
		{
			return C.Phase == KingdomPolityCohortPhase.Planned || Occupies(C);
		}

		private static bool Occupies(KingdomPolityCohortPlan C)
		{
			return (C.Phase == KingdomPolityCohortPhase.Planned &&
				!string.IsNullOrEmpty(C.ManifestationReceiptId)) ||
				C.Phase == KingdomPolityCohortPhase.Materialized ||
				C.Phase == KingdomPolityCohortPhase.Concluded;
		}
	}
}
