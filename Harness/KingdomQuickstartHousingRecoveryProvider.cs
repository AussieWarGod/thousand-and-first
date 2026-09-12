using System;
using System.Collections.Generic;
using HarmonyLib;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomQuickstartHousingRecoveryProvider : IKingdomScenarioVerbProvider
	{
		internal static KingdomQuickstartHousingRecovery Active;
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs
		{
			get { return new[] { "housing-delay", "housing-retained", "housing-unblock", "housing-recovered", "housing-witness" }; }
		}
		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				KingdomQuickstartHousingRecovery.Require(string.IsNullOrEmpty(Argument), "housing verbs take no arguments");
				XRLGame game = The.Game;
				Zone zone = The.Player?.CurrentZone;
				KingdomSystem system = game?.GetSystem<KingdomSystem>();
				KingdomQuickstartHousingRecovery.Require(game != null && zone != null && system?.Founded == true
					&& ReferenceEquals(The.ZoneManager.ActiveZone, zone) && !KingdomScenarioAdvance.Pending
					&& KingdomScenarioScript.TryRead(out IList<string> script, out _)
					&& KingdomQuickstartBootRequest.TryParse(script, out var request) && request.Lifecycle,
					"requires genuine sealed Quickstart lifecycle ground between advances");
				if (Verb == "housing-delay")
				{
					KingdomQuickstartHousingRecovery.Require(Active == null, "the delay already owns a game");
					Active = new KingdomQuickstartHousingRecovery(game, zone, system);
				}
				KingdomQuickstartHousingRecovery.Require(Active != null && ReferenceEquals(Active.Game, game)
					&& ReferenceEquals(Active.Zone, zone) && ReferenceEquals(Active.System, system),
					"housing recovery owner changed");
				string result;
				switch (Verb)
				{
					case "housing-delay": result = Active.Begin(); break;
					case "housing-retained": result = Active.CheckRetained(); break;
					case "housing-unblock": result = Active.Unblock(); break;
					case "housing-recovered": result = Active.Recovered(); break;
					case "housing-witness": result = Active.SaveWitness(); break;
					default: throw new InvalidOperationException("unknown housing recovery verb");
				}
				Ok = true; return result;
			}
			catch (Exception error) { return "housing recovery refused: " + error.GetType().Name + ": " + error.Message; }
		}
	}

	/// <summary>Observation only: production decides the departure and its result.</summary>
	[HarmonyPatch(typeof(KingdomGrowth), "EmigrateAuthorized")]
	internal static class KingdomQuickstartHousingDepartureObserver
	{
		internal static void Prefix(GameObject Leaver, out string __state)
		{
			__state = Leaver?.IDIfAssigned;
		}
		internal static void Postfix(KingdomSystem System, Zone Z, string Cause, bool __result, string __state)
		{
			var owner = KingdomQuickstartHousingRecoveryProvider.Active;
			if (owner == null || !ReferenceEquals(owner.Game, The.Game)
				|| !ReferenceEquals(owner.System, System) || !ReferenceEquals(owner.Zone, Z)) return;
			owner.ObserveDeparture(__state, Cause, __result);
		}
	}
}
