using System;
using System.Collections.Generic;
using System.Globalization;

using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.AI;
using XRL.World.AI.GoalHandlers;
using XRL.World.AI.Pathfinding;
using XRL.World.Effects;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomPhysicalHappenings
	{
		private static GameObject FindFixture(Zone zone, KingdomPhysicalHappeningKind kind)
		{
			GameObject best = null;
			int bestPriority = int.MaxValue;
			foreach (GameObject candidate in KingdomSurvey.ObjectsFor(zone))
			{
				if (!FunctionalFixture(kind, candidate) || candidate.CurrentCell == null) continue;
				int priority = FixturePriority(kind, candidate);
				if (best == null || priority < bestPriority
					|| (priority == bestPriority && (candidate.CurrentCell.Y < best.CurrentCell.Y
						|| (candidate.CurrentCell.Y == best.CurrentCell.Y
							&& candidate.CurrentCell.X < best.CurrentCell.X))))
				{
					best = candidate;
					bestPriority = priority;
				}
			}
			return best;
		}

		private static bool FunctionalFixture(KingdomPhysicalHappeningKind kind,
			GameObject fixture)
		{
			if (!GameObject.Validate(fixture) || fixture.CurrentCell == null) return false;
			bool authored = fixture.GetIntProperty("KingdomBuilt") == 1
				|| (fixture.Blueprint ?? "").StartsWith("r_Kingdom", StringComparison.Ordinal);
			if (!authored) return false;
			switch (kind)
			{
			case KingdomPhysicalHappeningKind.Wedding:
				return fixture.GetPart<Chair>() != null;
			case KingdomPhysicalHappeningKind.Funeral:
				return fixture.HasPart("Shrine");
			case KingdomPhysicalHappeningKind.Feast:
				return fixture.HasPart("Campfire");
			case KingdomPhysicalHappeningKind.CommunalRite:
				return fixture.Blueprint == "r_KingdomBench" && fixture.HasPart("Chair")
					|| fixture.Blueprint == "r_KingdomFirstBasin"
						&& fixture.HasPart("LiquidVolume");
			case KingdomPhysicalHappeningKind.Raising:
				return fixture.Blueprint == "r_KingdomFirstBasin"
					&& fixture.HasPart("LiquidVolume");
			default:
				return false;
			}
		}

		private static int FixturePriority(KingdomPhysicalHappeningKind kind, GameObject fixture)
		{
			if (kind == KingdomPhysicalHappeningKind.Feast)
				return fixture.Blueprint == "r_KingdomOven" ? 0 : 1;
			if (kind == KingdomPhysicalHappeningKind.CommunalRite)
				return fixture.Blueprint == "r_KingdomBench" ? 0 : 1;
			if (kind == KingdomPhysicalHappeningKind.Funeral)
			{
				if (fixture.Blueprint == "r_KingdomShrine") return 0;
				if (fixture.Blueprint == "r_KingdomShrineGarth") return 1;
				if (fixture.Blueprint == "r_KingdomTemple") return 2;
			}
			if (kind == KingdomPhysicalHappeningKind.Wedding)
				return fixture.Blueprint == "r_KingdomBench" ? 0 : 1;
			return 0;
		}

		private static List<Cell> OpenCells(Zone zone, GameObject fixtureObject,
			KingdomPhysicalHappeningKind kind)
		{
			List<Cell> result = new List<Cell>();
			Cell fixture = fixtureObject.CurrentCell;
			if ((kind == KingdomPhysicalHappeningKind.Wedding
				|| kind == KingdomPhysicalHappeningKind.CommunalRite)
				&& fixtureObject.GetPart<Chair>() != null
				&& ActivityCell(fixture, fixtureObject)) result.Add(fixture);
			for (int radius = 1; radius <= 4
				&& result.Count < KingdomHappeningLifecycleRules.MaxParticipants * 3; radius++)
			{
				for (int y = fixture.Y - radius; y <= fixture.Y + radius; y++)
				for (int x = fixture.X - radius; x <= fixture.X + radius; x++)
				{
					if (Math.Max(Math.Abs(x - fixture.X), Math.Abs(y - fixture.Y)) != radius)
						continue;
					Cell cell = zone.GetCell(x, y);
					if (ActivityCell(cell, fixtureObject)) result.Add(cell);
				}
			}
			return result;
		}

		private static bool ActivityCell(Cell cell, GameObject fixture)
		{
			if (cell == null || !cell.IsPassable() || !cell.IsEmptyOfSolid()) return false;
			for (int i = 0; i < cell.Objects.Count; i++)
			{
				GameObject item = cell.Objects[i];
				if (ReferenceEquals(item, fixture)) continue;
				if (item.IsCreature || (item.Physics != null && item.Physics.Solid)) return false;
			}
			return true;
		}

		private static bool CanWalk(GameObject body, Cell target)
		{
			if (!GameObject.Validate(body) || body.CurrentCell == null || target == null
				|| body.CurrentZone != target.ParentZone) return false;
			if (body.CurrentCell == target) return true;
			FindPath path = new FindPath(body.CurrentZone.ZoneID, body.CurrentCell.X,
				body.CurrentCell.Y, target.ParentZone.ZoneID, target.X, target.Y,
				PathGlobal: false, PathUnlimited: false, Looker: body, Juggernaut: false,
				IgnoreCreatures: false, IgnoreGases: false, FlexPhase: false, MaxWeight: 95);
			return path.Usable && path.Directions.Count <= MaxPathSteps;
		}
	}
}
