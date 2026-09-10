using System;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Real, bounded walking between surface parasangs. No zone loads or coordinate writes.</summary>
	internal static class KingdomScenarioTravel
	{
		internal enum Phase { None, Outbound, Waiting, Returning, Draining, Complete, Failed }
		internal static XRLGame Game;
		internal static KingdomSystem System;
		internal static KingdomCityBook Book;
		internal static GameObject Player;
		internal static string Home, PlayerId, Fault, Seed;
		internal static Phase State;
		internal static bool Away;
		internal static long BeginTurn, WaitTurn, ReturnTurn, ArrivedTurn, FirstHomeTurn = -1, ZeroTurn = -1;
		internal static long Processed, Semantic;
		internal static int HomeX, HomeY, OutSteps, BackSteps, PeakThirds, PeakHeavy, PeakDemand, Containers;
		internal static bool DemandObserved, ReturnDemandObserved;
		internal static int RemainingDemand = -1;
		private const string Intent = "r_TAF_BetaTravel_v1";
		internal static bool Active => Game != null && ReferenceEquals(Game, The.Game)
			&& State != Phase.Complete && State != Phase.Failed;
		internal static bool Pending => Active && (State == Phase.Outbound || State == Phase.Returning);

		internal static void Require(bool Value, string Reason)
		{
			if (!Value) throw new InvalidOperationException("taf-travel-refused: " + Reason);
		}

		internal static string Begin(bool IsAway)
		{
			Require(Game == null, "prior travel evidence remains retained; use a fresh profile");
			var game = The.Game;
			var player = The.Player;
			var system = game?.GetSystem<KingdomSystem>();
			Zone zone = player?.CurrentZone;
			Require(game?.GetSystem<KingdomScenarioAutoRunner>()?.HasConsideredScript == true
				&& system?.Founded == true && zone != null && zone == The.ZoneManager.ActiveZone
				&& system.ClaimedZones.Contains(zone.ZoneID), "requires a sealed runner on its founded ground");
			Require(!KingdomScenarioAdvance.Pending && !KingdomScenarioFrames.Pending, "another pump is pending");
			Require(zone.Width == 80 && zone.Height == 25
				&& KingdomScenarioTravelRules.Zone(zone.ZoneID, out _, out int wx, out _, out _, out _, out _)
				&& wx > 0, "requires an ordinary surface zone with a western parasang");
			Require(!KingdomNativeRegressionContext.HasAnyState(game, Intent), "travel intent already exists");
			Require(system.City != null && system.City.TryReadExact(out _, out _), "city book cannot be read exactly");
			Require(system.City.TryZoneRow(zone.ZoneID, out _),
				"home row missing before departure; ordinary settlement reconciliation has not initialized this fixture");
			game.SetStringGameState(Intent, IsAway ? "away" : "present");
			Require(KingdomScenarioDurableState.ProvesExactText(Intent, IsAway ? "away" : "present"), "intent did not persist");
			Game = game; Player = player; PlayerId = player.ID; System = system; Book = system.City;
			Home = zone.ZoneID; HomeX = player.CurrentCell.X; HomeY = player.CurrentCell.Y; Away = IsAway;
			BeginTurn = WaitTurn = game.Turns; Processed = Book.ProcessedThroughTick; Semantic = System.LastSemanticTick;
			State = IsAway ? Phase.Outbound : Phase.Waiting;
			Observe();
			if (IsAway) { Pump(out bool failed); Require(!failed, Fault); }
			return "taf-travel-began mode=" + (Away ? "away" : "present") + "; home=" + Home;
		}

		internal static string Return()
		{
			Observe();
			Require(State == Phase.Waiting && Game.Turns - WaitTurn == KingdomScenarioTravelRules.WaitTurns,
				"return requires exactly 1200 completed advance turns");
			ReturnTurn = Game.Turns;
			if (Away)
			{
				Require(KingdomScenarioTravelRules.DifferentParasang(Home, Player.CurrentZone.ZoneID)
					&& !System.ClaimedZones.Contains(Player.CurrentZone.ZoneID), "away parasang is not proved");
				Require(The.ZoneManager.CachedZones != null && !The.ZoneManager.CachedZones.ContainsKey(Home),
					"home remains cached; unloaded absence is unproved (no forced eviction)");
				State = Phase.Returning;
				Pump(out bool failed); Require(!failed, Fault);
			}
			else
			{
				Require(Player.CurrentZone.ZoneID == Home, "present leg left its home");
				State = Phase.Draining; ArrivedTurn = ReturnTurn; Observe();
			}
			return "taf-travel-return-began; wait-turns=" + (ReturnTurn - WaitTurn);
		}

		internal static bool Pump(out bool Failed)
		{
			Failed = false;
			try
			{
				Observe();
				if (!Pending) return false;
				bool west = State == Phase.Outbound;
				Require((west ? OutSteps : BackSteps) < KingdomScenarioTravelRules.MaxSteps, "route exceeds step bound");
				string before = Player.CurrentZone.ZoneID;
				int x = Player.CurrentCell.X, y = Player.CurrentCell.Y;
				Require(Player.Move(west ? "W" : "E", AllowDashing: false, DoConfirmations: false),
					"normal walking was blocked; no clearing or teleport fallback");
				Observe();
				Require(KingdomScenarioTravelRules.Step(before, x, y, Player.CurrentZone.ZoneID,
					Player.CurrentCell.X, Player.CurrentCell.Y, west), "walking changed unexpected cell/zone custody");
				if (west) OutSteps++; else BackSteps++;
				if (west && KingdomScenarioTravelRules.DifferentParasang(Home, Player.CurrentZone.ZoneID))
				{
					Require(!System.ClaimedZones.Contains(Player.CurrentZone.ZoneID), "destination parasang is claimed");
					State = Phase.Waiting; WaitTurn = Game.Turns;
					KingdomScenarioJournal.Append("travel-out-complete", true, "normal-walk=true; steps=" + OutSteps);
				}
				else if (!west && Player.CurrentZone.ZoneID == Home && Player.CurrentCell.X == HomeX
					&& Player.CurrentCell.Y == HomeY)
				{
					Require(BackSteps == OutSteps, "return route length differs");
					State = Phase.Draining; ArrivedTurn = Game.Turns;
					KingdomScenarioJournal.Append("travel-return-complete", true, "normal-walk=true; steps=" + BackSteps);
				}
				// Spend remaining energy by real waits so fast actors cannot fall into unattended input.
				for (int i = 0; i < 8 && Player.Energy != null && Player.Energy.Value >= 1000; i++) Player.PassTurn();
				Require(Player.Energy == null || Player.Energy.Value < 1000, "movement did not spend action energy");
				return Pending;
			}
			catch (Exception error)
			{
				Fault = error.Message; State = Phase.Failed; Failed = true;
				KingdomScenarioJournal.Append("travel-refused", false, Fault);
				return false;
			}
		}

		internal static void Observe()
		{
			Require(Active && ReferenceEquals(The.Player, Player) && GameObject.Validate(Player)
				&& Player.ID == PlayerId && Player.CurrentCell != null && Player.CurrentZone != null
				&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), System) && ReferenceEquals(System.City, Book)
				&& System.Founded && System.ClaimedZones.Contains(Home), "travel owner or founded home changed");
			Require(Fault == null, Fault);
			if (KingdomScenarioPauseController.Active)
				Require(KingdomScenarioPauseWitness.Current.Fault == null, KingdomScenarioPauseWitness.Current.Fault);
			Require(Book.TryReadExact(out _, out _) && KingdomScenarioTravelRules.Clock(Processed, Book.ProcessedThroughTick, Game.TimeTicks)
				&& KingdomScenarioTravelRules.Clock(Semantic, System.LastSemanticTick, Game.TimeTicks), "city/semantic clock moved backwards or ahead");
			Require(System.LastWaterWorkTick == Book.ProcessedThroughTick, "growth mirror diverged; possible duplicate day billing");
			Processed = Book.ProcessedThroughTick; Semantic = System.LastSemanticTick;
			KingdomScenarioTravelSchedule.Observe(Book);
			if ((State == Phase.Returning || State == Phase.Draining) && Player.CurrentZone.ZoneID == Home)
			{
				if (FirstHomeTurn < 0) FirstHomeTurn = Game.Turns;
				KingdomScenarioPauseController.AtHome();
				Require(Book.TryZoneRow(Home, out int row), "home row missing");
				if (Book.ZoneOwedWater[row] != 0 || Book.ZoneOwedFood[row] != 0 || Book.ZoneOwedMaterials[row] != 0)
					ZeroTurn = -1;
				KingdomScenarioTravelDemandObserver.ObserveGround();
			}
		}

		internal static string Check()
		{
			Observe();
			Require(State == Phase.Draining && Player.CurrentZone.ZoneID == Home
				&& KingdomScenarioTravelRules.DrainObservationReady(ArrivedTurn, Game.Turns),
				"drain observation precedes the requested 39 turns");
			Require(Book.TryZoneRow(Home, out int row), "home row missing");
			int owed = Math.Abs(Book.ZoneOwedWater[row]) + Math.Abs(Book.ZoneOwedFood[row]) + Math.Abs(Book.ZoneOwedMaterials[row]);
			Require(ReturnDemandObserved && RemainingDemand == 0
				&& KingdomScenarioTravelRules.Drained(FirstHomeTurn, ZeroTurn, owed),
				"physical catch-up within 39 turns was not proved by a post-return demand observation"
				+ "; observed=" + ReturnDemandObserved + "; remaining=" + RemainingDemand
				+ "; home-turn=" + FirstHomeTurn + "; zero-turn=" + ZeroTurn + "; owed=" + owed);
			var survey = KingdomSurvey.Take(Player.CurrentZone, System);
			Containers = survey.Stores.Count + survey.Larders.Count;
			Require(Containers <= KingdomScenarioTravelRules.CivicEnvelope && KingdomRules.MaxCivicContainersPerZone == 252
				&& KingdomCatchUpRules.WorstBacklogUnits == 312 && KingdomBudgetRules.ReifyUnitsPerTurn == 8,
				"physical envelope or production budget pins changed");
			string result = "taf-travel-checked mode=" + (Away ? "away" : "present")
				+ "; seed=" + Seed + "; home=" + Home + "; observed-tick=" + Game.TimeTicks
				+ "; wait-turns=" + (ReturnTurn - WaitTurn) + "; travel-turns=" + (WaitTurn - BeginTurn + ArrivedTurn - ReturnTurn)
				+ "; containers=" + Containers + "; envelope=252; drain-turns=" + (ZeroTurn - FirstHomeTurn)
				+ "; peak-thirds=" + PeakThirds + "; peak-heavy=" + PeakHeavy + "; measured-demand=" + PeakDemand
				+ "; demand-observed=" + DemandObserved + "; processed=" + Processed + "; semantic=" + Semantic
				+ "; growth-mirror=" + System.LastWaterWorkTick + "; schedule-observations=" + KingdomScenarioTravelSchedule.Observations
				+ "; remaining-demand=" + RemainingDemand + KingdomScenarioPauseController.Check()
				+ KingdomScenarioContainerStress.Check() + "; ordinary-acceptance=false";
			State = Phase.Complete;
			return result;
		}
	}
}
