using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.ZoneParts;

namespace ThousandAndFirst.Harness
{
	/// <summary>Observe genuine founding before any synthetic activation or visibility grant.</summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomQuickstartSightNativeProvider : IKingdomScenarioVerbProvider
	{
		private static readonly string[] Script = {
			"quickstart-lifecycle marsh yes", "quickstart-sight-start", "yield-frames 3",
			"quickstart-sight-check", "stagedigest"
		};
		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;
		public IEnumerable<string> ScenarioVerbs => new[] { "quickstart-sight-start", "quickstart-sight-check" };

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(string.IsNullOrEmpty(Argument), "sight checks take no arguments");
				Require(KingdomScenarioScript.TryRead(out IList<string> script, out _)
					&& script.Count == Script.Length, "exact sight script missing");
				for (int i = 0; i < Script.Length; i++) Require(script[i] == Script[i], "sight script changed");
				Zone zone = The.Player?.CurrentZone;
				KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
				Require(zone != null && ReferenceEquals(zone, The.ActiveZone) && system?.Founded == true
					&& system.ClaimedZones.Contains(zone.ZoneID) && KingdomClaimedGround.Enabled
					&& KingdomClaimedGroundLight.CitySightEnabled && !KingdomSurvey.HasBoundPass,
					"live claimed Quickstart and enabled sight required");
				KingdomClaimedGroundLight part = zone.GetPart<KingdomClaimedGroundLight>();
				Require(part != null && part.SettlementId == system.SettlementIdForOwnedZone(zone.ZoneID),
					"newly founded active zone has no correctly stamped city-sight part; no reactivation performed");
				if (Verb == "quickstart-sight-start")
				{
					KingdomQuickstartSightWitness.Start(zone, part);
					Ok = true;
					return "attached-at-founding=true; synthetic-activation=false; synthetic-visibility=false";
				}
				Require(Verb == "quickstart-sight-check", "unknown sight verb");
				string result = KingdomQuickstartSightWitness.Check(zone, part);
				Ok = true;
				return result;
			}
			catch (Exception error) { return "quickstart sight refused: " + KingdomScenarioRules.Bounded(error.Message); }
		}

		internal static void Require(bool Value, string Failure)
		{
			if (!Value) throw new InvalidOperationException(Failure);
		}
	}
}
