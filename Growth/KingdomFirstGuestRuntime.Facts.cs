using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomFirstGuestRuntime
	{
		private static string ComposeFacts(KingdomGrowthArrivalCandidate candidate,
			bool rendered, string presentationFailure)
		{
			KingdomGrowthFirstGuestOpportunity x = candidate.FirstGuest;
			string name = string.IsNullOrEmpty(candidate.PlannedName)
				? "An unnamed historical traveller" : KingdomPresentation.Rich(candidate.PlannedName);
			string origin = string.IsNullOrEmpty(candidate.PlannedOrigin)
				? "an unrecorded origin" : KingdomPresentation.Rich(candidate.PlannedOrigin);
			string person = name + " writes from " + origin
				+ (candidate.PlannedCreed == "-" ? "" : " and keeps "
					+ KingdomPresentation.Rich(candidate.PlannedCreed)) + ".";
			string cause = "\n\nGrowth cause: arrival window at tick "
				+ x.CauseTick.ToString(CultureInfo.InvariantCulture) + "; cadence "
				+ x.CadenceTicks.ToString(CultureInfo.InvariantCulture)
				+ " ticks; cohort exactly 1.";
			string consequence = x.FactsState == KingdomGrowthFirstGuestFactsState.Exact
				? "\nAdmission consequence: population " + x.PopulationBefore + " -> "
					+ (x.PopulationBefore + 1) + " of " + x.PopulationCap + "; support level "
					+ x.SupportedLevel + " permits below " + x.SupportCap + "; "
					+ x.WaterRequired + " drams required from " + x.WaterAvailable
					+ " witnessed at offer. Current Growth checks still apply when admitted."
				: "\nHistorical consequence snapshot is incomplete. No population, support, or water "
					+ "value is guessed; Growth rechecks exact current authority only after admission.";
			string mode = rendered
				? "\n\n{{C|Correspondence rendering available.}}"
				: "\n\n{{K|Direct Growth record: optional presentation unavailable"
					+ (string.IsNullOrEmpty(presentationFailure) ? "" : " (" + presentationFailure + ")")
					+ ". Choice authority is unchanged.}}";
			return person + cause + consequence + mode
				+ "\nDeferral has no expiry, charge, labor, service, reward, or hidden departure.";
		}
	}
}
