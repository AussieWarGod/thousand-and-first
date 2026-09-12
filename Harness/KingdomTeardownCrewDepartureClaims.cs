namespace ThousandAndFirst
{
	/// <summary>Engine-free predicate and diagnostic formatter for a labour crew that has fallen
	/// below its required size mid-scenario (review-3f010e3-teardown-findings.md finding 3: a
	/// genuine roofless-brink departure previously stalled both teardown cases at Phase 1 with
	/// Ok=true and no named diagnostic). Pure ints/string/long in, pure bool/string out -- no
	/// GameObject/Zone dependency, so it is value-testable without the game, mirroring
	/// KingdomTeardownStrikeRowClaims.</summary>
	internal static class KingdomTeardownCrewDepartureClaims
	{
		internal static bool HasDeparted(int OnRoll, int Wanted)
		{
			return OnRoll < Wanted;
		}

		internal static string Diagnostic(string CaseName, int OnRoll, int Wanted, long Tick)
		{
			return "case=" + CaseName + " outcome=crew-departed onRoll=" + OnRoll
				+ " wanted=" + Wanted + " tick=" + Tick;
		}
	}
}
