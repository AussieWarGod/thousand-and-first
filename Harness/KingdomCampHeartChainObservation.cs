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
				// The founder's cell, derived from the catalogue tent lot (Medium since 561bffd3),
				// steers the founder-ground rule to the pinned source tent (#282).
				Require(KingdomPlots.TryGetSpec(ClaimKey, out var spec), "source tent lot spec absent");
				Require(KingdomPlotRules.TryDimensions(spec.Size, out int width, out int height),
					"source tent lot dimensions absent");
				int destination = KingdomCampHeartChainGrid.CommissionX(outer, width);
				int row = KingdomCampHeartChainGrid.CommissionY(height);
				var player = The.Player;
				Require(destination >= 1 && row >= 1 && row < Zone.Height - 1 && GameObject.Validate(player)
					&& player.CurrentZone == Zone && player.CurrentCell != null, "chain commission has no western approach");
				long tick = Game.TimeTicks;
				int moves = 0, rowMoves = 0;
				while (player.CurrentCell.X > destination)
					ChainStep(player, "W", -1, 0, ++moves);
				while (player.CurrentCell.Y != row)
					ChainStep(player, player.CurrentCell.Y < row ? "S" : "N", 0,
						player.CurrentCell.Y < row ? 1 : -1, moves + ++rowMoves);
				Require(Game.TimeTicks == tick && ReferenceEquals(The.Game, Game),
					"commission approach changed the game or clock");
				Evidence.Append("\nchain-commission normal-west-moves=").Append(moves)
					.Append("; row-moves=").Append(rowMoves)
					.Append("; founder-cell=").Append(player.CurrentCell.X).Append(",").Append(player.CurrentCell.Y);
			}

			private void ChainStep(GameObject Player, string Direction, int DX, int DY, int Moves)
			{
				int x = Player.CurrentCell.X, y = Player.CurrentCell.Y;
				Require(Moves <= 40 && Player.Move(Direction, AllowDashing: false, DoConfirmations: false)
					&& ReferenceEquals(The.Player, Player) && Player.CurrentZone == Zone
					&& Player.CurrentCell.X == x + DX && Player.CurrentCell.Y == y + DY,
					"founder could not walk to the chain commission approach");
			}

			/// <summary>The chain's paid tent is locked to its quote before anything is minted or
			/// paid: the pinned source tent, and labour inside the one paid daily window before
			/// <c>HoldChainTent</c> (the first window after a commission may be unpriced).</summary>
			private void RequireChainQuote(KingdomPlotQuote Quote)
			{
				if (!ChainCommission) return;
				var tent = KingdomCampHeartChainGrid.SourceTent;
				Require(Same(Quote.Rect, tent), "chain tent quote " + Quote.Rect.X1 + "," + Quote.Rect.Y1
					+ " " + Quote.Rect.X2 + "," + Quote.Rect.Y2 + " differs from the source tent "
					+ tent.X1 + "," + tent.Y1 + " " + tent.X2 + "," + tent.Y2 + "; nothing was paid");
				Require(Quote.LabourTicks >= 1 && Quote.LabourTicks <= KingdomRules.TicksPerDay,
					"chain tent labour " + Quote.LabourTicks + " exceeds the one paid day before its hold");
				Evidence.Append("; one-day-labour-window=").Append(KingdomRules.TicksPerDay);
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
				Require(!KingdomSurvey.HasBoundPass, "support observation found an outstanding survey");
				Require(KingdomSurvey.TryBindLocalOperation(Zone, System, out var scope,
					out string failure), failure);
				using (scope) RequireChainSupportInPass(KingdomSurvey.ActiveFor(Zone));
				Require(!KingdomSurvey.HasBoundPass, "support observation left its survey bound");
			}

			private void RequireChainSupportInPass(KingdomSurvey survey)
			{
				RequireChainWaterSupport(survey);
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
