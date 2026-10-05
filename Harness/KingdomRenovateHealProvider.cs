using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// #283 stuck-save heal route, session one's save verb. Answers only under the exact sealed
	/// heal script (<see cref="KingdomRenovateHealScript"/>) and only after the tier-upgrade
	/// seam's own begun check; see <see cref="KingdomTierUpgradeChecks.HealSave"/>. The renovate
	/// itself is driven by the real settlement pass, exactly as in the tier-upgrade persona.
	/// </summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomRenovateHealProvider : IKingdomScenarioVerbProvider
	{
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }

		public IEnumerable<string> ScenarioVerbs
		{
			get { return new[] { KingdomRenovateHealScript.SaveVerb }; }
		}

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				KingdomTierUpgradeProvider.Require(Verb == KingdomRenovateHealScript.SaveVerb
					&& string.IsNullOrEmpty(Argument), "renovate-heal-save takes no arguments");
				IList<string> script;
				KingdomTierUpgradeProvider.Require(KingdomScenarioScript.TryRead(out script, out _)
					&& KingdomRenovateHealScript.Matches(script),
					"the exact sealed heal script is absent or differs");
				XRLGame game = The.Game;
				Zone zone = The.Player?.CurrentZone;
				KingdomTierUpgradeProvider.Require(game != null && zone != null
					&& KingdomScenarioDurableState.ProvesExactText(KingdomTierUpgradeProvider.Receipt,
						"intent"), "the tier-upgrade owner intent is absent or torn");
				string result = KingdomTierUpgradeChecks.HealSave(game, zone);
				Ok = true;
				return result;
			}
			catch (Exception error)
			{
				return "renovate-heal-save refused; evidence retained: "
					+ KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
			}
		}
	}
}
