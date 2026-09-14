using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomLodgingRoomNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "lodging-room-native";
		private const string Receipt = "r_TAF_ScenarioLodgingRoom_v1";
		private static KingdomLodgingRoomNativeFixture Attempt;
		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;
		public IEnumerable<string> ScenarioVerbs => new[] { Verb };

		public string RunScenarioVerb(string Name, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(Name == Verb && string.IsNullOrEmpty(Argument), "room verb takes no arguments");
				Require(KingdomScenarioScript.TryRead(out IList<string> script, out _)
					&& script.Count == 4 && script[0] == "quickstart-lifecycle dunes yes"
					&& script[1] == "stagedigest" && script[2] == Verb && script[3] == "stagedigest"
					&& KingdomQuickstartBootRequest.TryParse(script, out var boot) && boot.Lifecycle,
					"exact sealed Quickstart room script absent");
				XRLGame game = The.Game;
				Zone zone = The.Player?.CurrentZone;
				KingdomSystem system = game?.GetSystem<KingdomSystem>();
				Require(Attempt == null && game != null && zone != null && system?.Founded == true
					&& ReferenceEquals(The.ZoneManager?.ActiveZone, zone)
					&& !KingdomSurvey.HasBoundPass && !KingdomScenarioAdvance.Pending
					&& KingdomLodging.Enabled && !KingdomNativeRegressionContext.HasAnyState(game, Receipt),
					"requires one fresh founded Quickstart with lodging enabled and no active pass");
				Require(KingdomQuickstartSettlementChecks.Observe(game, zone, system, "startup", out string failure), failure);
				game.SetStringGameState(Receipt, "intent");
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "room intent readback failed");
				Attempt = new KingdomLodgingRoomNativeFixture(game, zone, system);
				string result = Attempt.Run();
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "room intent changed");
				game.SetStringGameState(Receipt, result);
				Require(KingdomScenarioDurableState.ProvesExactText(Receipt, result), "room completion readback failed");
				Ok = true; return result;
			}
			catch (Exception error)
			{
				string failure = "native-lodging-room retained failure: " + error;
				KingdomLog.Log(failure);
				return failure;
			}
		}

		internal static void Require(bool Condition, string Failure)
		{
			if (!Condition) throw new InvalidOperationException(Failure);
		}
	}
}
