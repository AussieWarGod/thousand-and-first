using System;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Identity of the developer scenario harness.
	/// <para>
	/// Containment constraint: this tree is absent from <c>manifest.json</c> Directories, so Qud
	/// never compiles it, and is listed in <c>Tools/stage.sh</c> EXCLUDE_DIRS, so it never reaches
	/// the live mod folder or the Workshop package. The three reflection scans a harness would need
	/// (<c>WishManager</c> wish discovery, <c>PlayerMutator</c> type discovery, and EmbarkModules
	/// type resolution) therefore find nothing in an ordinary build.
	/// </para>
	/// <para>
	/// No production file may reference <c>ThousandAndFirst.Harness</c>. Production reads scenario
	/// provenance through <see cref="ThousandAndFirst.KingdomScenarioProvenance"/>, which is always
	/// compiled and never writes.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioHarness
	{
		/// <summary>Registry root element and provenance grammar tag are versioned together.</summary>
		internal const int Schema = 1;

		/// <summary>Suite token for checkpoint and evidence rows. Must satisfy SafeToken.</summary>
		internal const string Suite = "scenario";

		/// <summary>Registry root element for <c>DataManager.YieldXMLStreamsWithRoot</c>.</summary>
		internal const string RegistryRoot = "KingdomScenarios";

		/// <summary>
		/// Bounded RNG bracket label. Every realization step runs inside
		/// <c>Stat.PushState</c>/<c>Stat.PopState</c> under this prefix so scenario setup cannot
		/// perturb the main sequence.
		/// </summary>
		internal const string SeedPrefix = "TAF-SCENARIO:";
	}
}
