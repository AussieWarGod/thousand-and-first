using System;
using HarmonyLib;
using XRL;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomScenarioTravelDriver
	{
		internal static bool Pump(out bool Faulted)
		{
			Faulted = false;
			if (!KingdomScenarioTravel.Active) return false;
			try
			{
				KingdomScenarioTravel.Observe();
				return KingdomScenarioTravel.Pending && KingdomScenarioTravel.Pump(out Faulted);
			}
			catch (Exception error)
			{
				KingdomScenarioTravel.Fault = error.Message;
				KingdomScenarioTravel.State = KingdomScenarioTravel.Phase.Failed;
				KingdomScenarioJournal.Append("travel-refused", false, error.Message);
				Faulted = true; return false;
			}
		}

		internal static void Stop()
		{
			if (KingdomScenarioTravel.Active) KingdomScenarioTravel.State = KingdomScenarioTravel.Phase.Failed;
		}
	}

	/// <summary>Read-only, exact-game observer; never suppresses or changes production calls.</summary>
	[HarmonyPatch(typeof(KingdomCity), "Charge")]
	internal static class KingdomScenarioTravelBudgetObserver
	{
		internal static void Postfix(KingdomSystem System)
		{
			if (!KingdomScenarioTravel.Active || !ReferenceEquals(System, KingdomScenarioTravel.System)) return;
			KingdomScenarioTravel.PeakThirds = Math.Max(KingdomScenarioTravel.PeakThirds, System.ReifyThirdsSpent);
			KingdomScenarioTravel.PeakHeavy = Math.Max(KingdomScenarioTravel.PeakHeavy, System.ReifyHeavySpent);
			if (!KingdomScenarioTravelRules.Budget(System.ReifyThirdsSpent, System.ReifyHeavySpent))
				KingdomScenarioTravel.Fault = "reify exceeded shared 24-thirds/four-heavy turn budget";
		}
	}

	[HarmonyPatch(typeof(KingdomCity), "Receipt")]
	internal static class KingdomScenarioTravelDemandObserver
	{
		internal static void Postfix(string zoneId, int owed)
		{
			if (!KingdomScenarioTravel.Active || zoneId != KingdomScenarioTravel.Home) return;
			KingdomScenarioTravel.DemandObserved = true;
			KingdomScenarioTravel.PeakDemand = Math.Max(KingdomScenarioTravel.PeakDemand, owed);
			if ((KingdomScenarioTravel.State == KingdomScenarioTravel.Phase.Returning
				|| KingdomScenarioTravel.State == KingdomScenarioTravel.Phase.Draining)
				&& The.Player?.CurrentZone?.ZoneID == zoneId)
			{
				KingdomScenarioTravel.ReturnDemandObserved = true;
				KingdomScenarioTravel.RemainingDemand = owed;
				if (KingdomScenarioTravel.FirstHomeTurn < 0) KingdomScenarioTravel.FirstHomeTurn = The.Game.Turns;
				if (owed != 0) KingdomScenarioTravel.ZeroTurn = -1;
				else if (KingdomScenarioTravel.ZeroTurn < 0) KingdomScenarioTravel.ZeroTurn = The.Game.Turns;
			}
			if (owed < 0 || owed > 312 * 3) KingdomScenarioTravel.Fault = "physical demand exceeds 312-unit envelope";
		}
	}
}
