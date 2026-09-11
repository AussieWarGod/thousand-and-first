using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The scenario verbs of ONE construction lifecycle, driven on an ordinary founded settlement
	/// rather than on the Quickstart boot: startup census, the production quote/CanPay/commission
	/// with its physical debit, then -- after real engine turns spent by the existing bounded
	/// <c>advance</c> verb between these verbs -- the completion of that exact job into a
	/// functional building, and a real save.
	///
	/// <para>WHY NOT ON THE QUICKSTART PROFILE. A Quickstart run carries no scenario auto-runner
	/// (Harness/KingdomQuickstartBootTest.cs:141 and :182 assert its absence, and
	/// DevTests/KingdomQuickstartRoundtripSourceTests.cs:45,110 pin that absence as a contract),
	/// and Tools/scenario_profile.py refuses a script that mixes a Quickstart verb with the
	/// auto-runner verbs. Nothing in a Quickstart run can therefore spend engine turns. These
	/// verbs take the other road: an ordinary <c>founding-first-city</c> profile whose production
	/// founding transaction the built-in <c>realize</c> verb drives, where <c>advance</c> already
	/// spends genuine turns through the player's own action opportunities
	/// (Harness/KingdomScenarioAdvance.cs:250-257), exactly as the water-maintenance persona does.
	/// No Quickstart invariant is touched, relaxed, or re-authorised.</para>
	///
	/// <para>NOTHING IS FABRICATED. No stock is minted, no job is forced into a phase, no target
	/// state is set. The settlement pays out of whatever its own economy has accumulated by the
	/// turn the verb runs; if it cannot pay, the CanPay row records the production blocker and the
	/// chain stops there. A turn budget that expires before the job completes lands a REFUSED
	/// row, never a pass.</para>
	/// </summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomQuickstartLifecycleProvider : IKingdomScenarioVerbProvider
	{
		internal const string OpenVerb = "lifecycle-open";
		internal const string BuildVerb = "lifecycle-build";
		internal const string GrownVerb = "lifecycle-grown";
		internal const string SaveVerb = "lifecycle-save";
		internal const string Receipt = "r_TAF_ScenarioLifecycle_v1";

		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }

		public IEnumerable<string> ScenarioVerbs
		{
			get { return new[] { OpenVerb, BuildVerb, GrownVerb, SaveVerb }; }
		}

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			if (!string.IsNullOrEmpty(Argument)) return Verb + " takes no arguments";
			XRLGame game = The.Game;
			Zone zone = The.Player?.CurrentZone;
			if (game == null || zone == null || The.ZoneManager == null
				|| !ReferenceEquals(The.ZoneManager.ActiveZone, zone))
				return "native lifecycle refused: no live owned player zone";
			KingdomSystem system = game.GetSystem<KingdomSystem>();
			if (system == null || !system.Founded)
				return "native lifecycle refused: this verb runs only on a founded settlement, "
					+ "after the production founding transaction";
			try
			{
				switch (Verb)
				{
					case OpenVerb: return KingdomQuickstartLifecycleSteps.Open(game, zone, system, out Ok);
					case BuildVerb: return KingdomQuickstartLifecycleSteps.Build(game, zone, system, out Ok);
					case GrownVerb: return KingdomQuickstartLifecycleSteps.Grown(game, zone, system, out Ok);
					case SaveVerb: return KingdomQuickstartLifecycleSteps.Save(game, zone, system, out Ok);
					default: return "unknown lifecycle verb";
				}
			}
			catch (Exception error)
			{
				Ok = false;
				return "native lifecycle refused: " + KingdomScenarioRules.Bounded(
					error.GetType().Name + ": " + error.Message);
			}
		}
	}
}
