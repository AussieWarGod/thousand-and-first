using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>Physical entrance obstruction on a genuinely completed starter home. No turns,
	/// seal records, building components or population are edited; only owned test obstacles move.</summary>
	internal static class KingdomQuickstartIngressChecks
	{
		internal static bool Verify(XRLGame Game, Zone Zone, out string Failure)
		{
			Failure = null;
			if (!KingdomQuickstartRules.IsMode(Game.gameMode)) return true;
			try
			{
				var expected = KingdomQuickstartRules.ShelterLot(1);
				GameObject home = null;
				foreach (GameObject item in Zone.GetObjects())
					if (GameObject.Validate(item) && KingdomUpgrade.IsFunctionallyBuilt(item)
						&& item.GetStringProperty(KingdomUpgrade.BuildKeyProperty) == KingdomQuickstartRules.ShelterBuildKey
						&& KingdomPlots.TryReadRect(item, out var rect)
						&& rect.X1 == expected.X1 && rect.Y1 == expected.Y1
						&& rect.X2 == expected.X2 && rect.Y2 == expected.Y2)
					{
						Require(home == null, "duplicate starter home"); home = item;
					}
				Require(home != null, "completed starter home absent");
				Require(KingdomArchitectureStamper.TryVerifyComplete(home, Zone, out string reason), reason);
				KingdomSeal seal = Game.GetSystem<KingdomSeal>();
				Require(seal != null, "seal coordinator absent");
				string before = seal.NativePendingStageEvidence();
				long tick = Game.TimeTicks;
				// Read the reserved route whose authored geometry the ingress suite recomputes.
				// A former exterior coordinate can become interior when the minimum lot grows.
				Cell lane = ExteriorLane(Zone, expected);
				Require(KingdomRoads.Walkable(lane), "starter ingress was already obstructed");
				foreach (string blueprint in new[] { "r_KingdomStructureMudWall", "r_KingdomCaskRack" })
				{
					GameObject obstacle = GameObject.Create(blueprint);
					Require(GameObject.Validate(obstacle) && !string.IsNullOrEmpty(obstacle.ID), "obstacle creation failed");
					try
					{
						Require(ReferenceEquals(lane.AddObject(obstacle, NoStack: true), obstacle), "obstacle placement failed");
						Require(!KingdomRoads.Walkable(lane), "obstacle did not block the actual route");
						Require(!KingdomArchitectureStamper.TryVerifyComplete(home, Zone, out reason, out bool blocked)
							&& blocked, "obstructed ingress was not distinguished from damaged components: " + reason);
						Require(seal.NativeSpatialCaptureWaits(out reason), reason);
						Require(seal.NativePendingStageEvidence() == before, "obstruction changed the staged seal");
					}
					finally
					{
						if (ReferenceEquals(obstacle.CurrentCell, lane)) lane.RemoveObject(obstacle);
					}
					Require(obstacle.CurrentCell == null && KingdomRoads.Walkable(lane), "owned obstacle cleanup failed");
					Require(KingdomArchitectureStamper.TryVerifyComplete(home, Zone, out reason), reason);
				}
				var invalid = new ArchitectureLayoutSnapshot { Anchors = new List<ArchitectureAnchor>() };
				Require(!KingdomArchitectureRuntime.TryVerifyPhysicalIngressRoutes(Zone, expected, invalid,
					out reason, out bool invalidBlocked) && !invalidBlocked,
					"a missing authored entrance was misclassified as a physical obstruction");
				Require(Game.TimeTicks == tick && seal.NativePendingStageEvidence() == before,
					"entrance probe changed time or stored seal evidence");
			}
			catch (Exception error) { Failure = error.GetType().Name + ": " + error.Message; }
			KingdomScenarioJournal.Append("quickstart-ingress", Failure == null,
				Failure ?? "wall-and-liquid-carrier=passed; missing-entrance=refused; cleanup=exact; seal=unchanged; turns-spent=0");
			return Failure == null;
		}

		private static Cell ExteriorLane(Zone Zone, KingdomPlotRules.PlotRect Lot)
		{
			Cell lane = null;
			for (int i = 0; i < KingdomQuickstartRules.ShelterIngressCellCount; i++)
			{
				KingdomQuickstartRules.ShelterIngressCell(i, out int x, out int y);
				bool adjacent = x >= Lot.X1 && x <= Lot.X2 && (y == Lot.Y1 - 1 || y == Lot.Y2 + 1)
					|| y >= Lot.Y1 && y <= Lot.Y2 && (x == Lot.X1 - 1 || x == Lot.X2 + 1);
				if (!adjacent) continue;
				Require(lane == null, "starter home has more than one adjacent reserved exterior lane");
				lane = Zone.GetCell(x, y);
			}
			Require(lane != null, "starter home has no reserved exterior ingress cell");
			return lane;
		}

		private static void Require(bool Condition, string Failure)
		{
			if (!Condition) throw new InvalidOperationException(Failure);
		}
	}
}
