using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;
using XRL.World.AI;
using XRL.World.AI.GoalHandlers;
using XRL.World.AI.Pathfinding;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomSuccessionRite
	{
		private static bool Walk(Walker walker, Cell destination)
		{
			GameObject body = walker?.Body;
			if (!GameObject.Validate(body) || body.Brain == null || destination == null
				|| body.CurrentZone != destination.ParentZone) return false;
			if (body.CurrentCell == destination) return true;
			List<GoalHandler> goals = new List<GoalHandler>(body.Brain.Goals.Items);
			GlobalLocation anchor = body.Brain.StartingCell == null ? null
				: new GlobalLocation(body.Brain.StartingCell.ToString());
			bool staying = body.Brain.Staying;
			try
			{
				body.Brain.PushGoal(new MoveTo(destination, careful: true,
					overridesCombat: true, AbortIfMoreSteps: MaxWalkSteps));
				FindPath path = new FindPath(body.CurrentZone.ZoneID, body.CurrentCell.X,
					body.CurrentCell.Y, destination.ParentZone.ZoneID, destination.X,
					destination.Y, PathGlobal: false, PathUnlimited: false, Looker: body,
					Juggernaut: false, IgnoreCreatures: false, IgnoreGases: false,
					FlexPhase: false, MaxWeight: 95);
				if (!path.Usable || path.Directions.Count > MaxWalkSteps) return false;
				for (int i = 0; i < path.Directions.Count; i++)
				{
					if (!body.Move(path.Directions[i], Forced: false, System: false,
						AllowDashing: false, DoConfirmations: false, EnergyCost: 0,
						Type: "KingdomMourningProcession", Peaceful: true)) return false;
				}
				return body.CurrentCell == destination;
			}
			finally
			{
				body.Brain.Goals.Items.Clear();
				body.Brain.Goals.Items.AddRange(goals);
				body.Brain.StartingCell = anchor;
				body.Brain.Staying = staying;
			}
		}

		private static bool CanWalk(GameObject body, Cell destination)
		{
			if (!GameObject.Validate(body) || body.CurrentCell == null || destination == null) return false;
			if (body.CurrentCell == destination) return true;
			FindPath path = new FindPath(body.CurrentZone.ZoneID, body.CurrentCell.X,
				body.CurrentCell.Y, destination.ParentZone.ZoneID, destination.X, destination.Y,
				PathGlobal: false, PathUnlimited: false, Looker: body, Juggernaut: false,
				IgnoreCreatures: false, IgnoreGases: false, FlexPhase: false, MaxWeight: 95);
			return path.Usable && path.Directions.Count <= MaxWalkSteps;
		}

		private static void ReturnAll(List<Walker> walkers, bool includeHeir)
		{
			for (int i = walkers.Count - 1; i >= (includeHeir ? 0 : 1); i--)
			{
				try { Walk(walkers[i], walkers[i].OriginalCell); } catch { }
			}
		}

		private static bool UnchangedPosts(GameObject body, KingdomRiteAttendee row)
		{
			return string.Equals(PostReceipt(body), row.Post, StringComparison.Ordinal)
				&& string.Equals(body.GetStringProperty(KingdomLodging.HomePlotIdProperty) ?? "",
					row.Home, StringComparison.Ordinal);
		}

		private static string PostReceipt(GameObject body)
		{
			return KingdomStations.PostOf(body).ToString(CultureInfo.InvariantCulture) + "/"
				+ body.GetIntProperty(KingdomStations.PostKindProperty).ToString(CultureInfo.InvariantCulture);
		}

	}
}
