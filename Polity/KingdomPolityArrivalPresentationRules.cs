namespace ThousandAndFirst
{
	/// <summary>Player prose for a scheduled cohort's first bodily arrival at the loaded endpoint.
	/// Pure composition: the scheduler resolves the endpoint name and pushes the line.</summary>
	internal static class KingdomPolityArrivalPresentationRules
	{
		/// <summary>Builds the arrival line, or returns false when a transactionless visit has no
		/// due verb to announce honestly.</summary>
		public static bool TryBuild(KingdomPolityCohortPlan Cohort, string LoadedEndpointName,
			out string Line)
		{
			Line = null;
			KingdomPolityAmbientTransaction t = Cohort.AmbientTransaction;
			if (!KingdomPolityAmbientTransactionRules.Valid(t, Cohort.CohortId, out _))
			{
				// A weekly visit without a valid frozen transaction (pre-schema stub or
				// transactionless plan) still announces its bodily arrival with the due verb.
				string verb = KingdomPolityDispatchRules.EndpointVerb(Cohort.Purpose);
				if (string.IsNullOrEmpty(verb)) return false;
				Line = "{{C|" + KingdomPresentation.Rich(LoadedEndpointName) +
					"}}: the visiting company " + verb + ".";
				return true;
			}
			string purpose = Cohort.Purpose == KingdomPolityCohortPurpose.Courier ? "message" :
				Cohort.Purpose == KingdomPolityCohortPurpose.Trader ? "no-stock market notice" :
				Cohort.Purpose == KingdomPolityCohortPurpose.Migrant ? "petition" :
				Cohort.Purpose == KingdomPolityCohortPurpose.Guard ? "witnessed watch report" :
				"caused condition report";
			Line = "{{C|" + KingdomPresentation.Rich(t.DestinationSettlementName) +
				"}} receives a " + purpose + " from {{C|" +
				KingdomPresentation.Rich(t.SourceSettlementName) + "}}.";
			return true;
		}
	}
}
