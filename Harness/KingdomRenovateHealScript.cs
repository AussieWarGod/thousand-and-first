using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// #283 stuck-save heal route, engine-free so both test projects can compare it with the host
	/// grammar (Tools/personas/persona_matrix.py RENOVATE_HEAL_RELOAD_SCRIPT). Session one runs on
	/// a build WITHOUT the fix: the ordinary tier upgrade's first two legs, then its third wait,
	/// into the retired handover stall, and a real save of that stall. Session two cold-loads the
	/// save on the fixed build and resumes ordinary turns until the once-only readmission has
	/// finished the handover, then saves again.
	/// <para>
	/// The two failure texts are spelled here, not read from production, because session one's
	/// build predates the constants; DevTests pin them equal to the production literals in
	/// <c>Growth/KingdomConstructionRules.Readmission.cs</c>.
	/// </para>
	/// </summary>
	internal static class KingdomRenovateHealScript
	{
		internal const string SaveVerb = "renovate-heal-save";

		/// <summary>Ordinary turns session two resumes for: two daily settlement passes, the
		/// first of which readmits and hands over.</summary>
		internal const int HealTurns = 2400;

		internal const string MarksFailure = "Founder marks did not settle exactly on the successor.";
		internal const string EndpointsFailure =
			"The paid improvement job no longer matches its exact physical endpoints.";

		private static readonly string[] Steps = {
			"stagedigest", "tier-upgrade-setup", "advance 3600", "tier-upgrade-check",
			"tier-upgrade-short", "advance 2400", "tier-upgrade-check", "advance 3600", SaveVerb };

		internal static bool Matches(IList<string> Script)
		{
			if (Script == null || Script.Count != Steps.Length) return false;
			for (int i = 0; i < Steps.Length; i++) if (Script[i] != Steps[i]) return false;
			return true;
		}

		/// <summary>"A" for the founder-marks text, "B" for the endpoints text, else null.</summary>
		internal static string DefectOf(string Failure)
		{
			return Failure == MarksFailure ? "A" : Failure == EndpointsFailure ? "B" : null;
		}
	}
}
