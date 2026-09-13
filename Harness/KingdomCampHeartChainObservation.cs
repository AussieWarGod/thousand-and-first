using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			private bool ChainCommission;

			private void PrepareChainCommission()
			{
				Require(KingdomScenarioScript.TryRead(out var script, out string failure), failure);
				ChainCommission = KingdomCampHeartChainScript.Matches(script);
				if (!ChainCommission) return;
				Require(KingdomPlots.TryHeartRectFor(Zone, 4, out var outer), "final heart envelope absent");
				int destination = outer.X1 - KingdomPlotRules.SmallWidth - KingdomPlotRules.RoadMargin - 2;
				var player = The.Player;
				Require(destination >= 1 && GameObject.Validate(player) && player.CurrentZone == Zone
					&& player.CurrentCell != null, "chain commission has no western approach");
				long tick = Game.TimeTicks;
				int moves = 0;
				while (player.CurrentCell.X > destination)
				{
					int x = player.CurrentCell.X, y = player.CurrentCell.Y;
					Require(++moves <= 40 && player.Move("W", AllowDashing: false, DoConfirmations: false)
						&& ReferenceEquals(The.Player, player) && player.CurrentZone == Zone
						&& player.CurrentCell.X == x - 1 && player.CurrentCell.Y == y,
						"founder could not walk to the chain commission approach");
				}
				Require(Game.TimeTicks == tick && ReferenceEquals(The.Game, Game),
					"commission approach changed the game or clock");
				Evidence.Append("\nchain-commission normal-west-moves=").Append(moves)
					.Append("; founder-cell=").Append(player.CurrentCell.X).Append(",").Append(player.CurrentCell.Y);
			}

			private void RequireChainCommissionClear(KingdomPlotRules.PlotRect Plot)
			{
				if (!ChainCommission) return;
				Require(KingdomPlots.TryHeartRectFor(Zone, 4, out var outer)
					&& !KingdomPlotRules.Overlaps(outer, KingdomPlotRules.Reserved(Plot)),
					"source tent reserves ground needed by a later heart rung");
				Evidence.Append("\nchain-commission final-heart-reserved-lane-clear=true");
			}

			private List<GameObject> ChainResidentBodies(KingdomSurvey Survey)
			{
				var bodies = new List<GameObject>();
				var ids = new HashSet<int>();
				foreach (var body in Survey.Objects)
				{
					if (!KingdomCitizenship.BelongsTo(System, body)) continue;
					Require(KingdomResidents.TryLocate(System, body, out var book, out int id)
						&& ReferenceEquals(book, System.City) && id > 0 && ids.Add(id),
						"physical citizen lacks a unique exact resident row");
					bodies.Add(body);
				}
				return bodies;
			}

			private void RequireChainSupport()
			{
				var survey = Census();
				var current = ChainResidentBodies(survey);
				Require(System.Population >= 50 && current.Count == System.Population
					&& current.Count == KingdomResidents.OnRollCount(System)
					&& survey.StorageCapacity >= 1024 && survey.StoredWater > 0 && survey.FoodStored > 0,
					"city support lost population, water or food: population=" + System.Population
					+ "; bodies=" + current.Count + "; water=" + survey.StoredWater + "; food=" + survey.FoodStored);
				Require(survey.TryBenefits(out var benefits, out string failure), failure);
				var capacities = new Dictionary<string, int>(StringComparer.Ordinal);
				foreach (var home in survey.Built)
				{
					int capacity = KingdomLodging.RoofCapacity(home, benefits);
					if (capacity <= 0 || KingdomLodging.IsCondemned(home)) continue;
					Require(KingdomLodging.TryHomeReading(home, benefits, out _, out string plot)
						&& !capacities.ContainsKey(plot), "home lacks a unique physical benefit reading");
					capacities.Add(plot, capacity);
				}
				var occupancy = new Dictionary<string, int>(StringComparer.Ordinal);
				foreach (var resident in current)
				{
					Require(GameObject.Validate(resident) && resident.IsAlive && resident.CurrentZone == Zone,
						"resident is dead or left its exact city ground");
					string home = resident.GetStringProperty(KingdomLodging.HomePlotIdProperty);
					Require(!string.IsNullOrEmpty(home) && capacities.ContainsKey(home),
						"resident has no functional physical roof: " + resident.IDIfAssigned);
					int count = occupancy.TryGetValue(home, out int held) ? held + 1 : 1;
					Require(count <= capacities[home], "assigned home exceeds its physical bed capacity");
					occupancy[home] = count;
				}
				foreach (var original in ChainResidents)
					Require(current.Contains(original), "an original city fixture body was lost or replaced");
			}

			private void ClearChainFounder()
			{
				var player = The.Player;
				Require(GameObject.Validate(player) && player.CurrentZone == Zone && player.CurrentCell != null, "founder absent from exact ground");
				Require(KingdomPlots.TryHeartRectFor(Zone, 4, out var outer), "final heart envelope absent");
				int moves = 0;
				long tick = Game.TimeTicks;
				while (outer.Contains(player.CurrentCell.X, player.CurrentCell.Y))
				{
					int x = player.CurrentCell.X, y = player.CurrentCell.Y;
					Require(x > 1 && ++moves <= 32 && player.Move("W", AllowDashing: false, DoConfirmations: false)
						&& ReferenceEquals(The.Player, player) && player.CurrentZone == Zone
						&& player.CurrentCell.X == x - 1 && player.CurrentCell.Y == y,
						"founder could not walk clear of the final heart envelope");
				}
				Require(Game.TimeTicks == tick && ReferenceEquals(The.Game, Game), "founder walk changed the game clock");
				Require(KingdomScenarioJournal.Append("camp-heart-chain-founder", true,
					"normal-west-moves=" + moves + "; outside-rung4=true; cell=" + player.CurrentCell.X
					+ "," + player.CurrentCell.Y) == null, "founder clearance journal unavailable");
			}
		}
	}
}
