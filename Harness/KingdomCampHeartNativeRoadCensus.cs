using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			// Observe the refused approach; never lay a floor, invent traffic or run a road pass.
			private void RecordRoadCensus()
			{
				try
				{
					List<KingdomRoadRules.WornCell> tally = KingdomRoads.ReadTally(Zone);
					Evidence.Append("\nroad-census tick=").Append(Game.TimeTicks)
						.Append("; cells=").Append(tally.Count)
						.Append("; worn-threshold=").Append(KingdomRoadRules.WornTraffic);
					for (int i = 0; i < Math.Min(32, tally.Count); i++)
						Evidence.Append("\nroad-cell=").Append(tally[i].X).Append(',')
							.Append(tally[i].Y).Append("; traffic=").Append(tally[i].Traffic);
					Evidence.Append("\nroad-census-truncated=").Append(tally.Count > 32);
					if (!KingdomArchitectureRuntime.TryRead(Heart, out var before, out string failure)
						|| !KingdomArchitectureRuntime.TryPrepareSuccessor(System, Zone, before,
							SecondRungKey, out var after, out failure)
						|| !KingdomArchitectureRuntime.TryDecode(after, out var snapshot, out failure))
					{
						Evidence.Append("\nroad-target-unresolved=")
							.Append(KingdomScenarioRules.Bounded(failure));
						return;
					}
					int entrances = 0;
					foreach (ArchitectureAnchor anchor in snapshot.Anchors)
					{
						if (anchor?.Key == null || !(anchor.Key == "entrance:public"
							|| anchor.Key.StartsWith("entrance:public@", StringComparison.Ordinal))) continue;
						if (++entrances > 8) { Evidence.Append("\nroad-entrances-truncated=true"); break; }
						List<ArchitecturePoint> route = new List<ArchitecturePoint>();
						if (!KingdomRoadRules.TryAuthoredLane(snapshot, after.Rect, anchor, route,
							out _, out _, out int x, out int y))
						{
							Evidence.Append("\nroad-target-route-unresolved=").Append(anchor.Key);
							continue;
						}
						int traffic = 0;
						for (int i = 0; i < tally.Count; i++)
							if (tally[i].X == x && tally[i].Y == y) traffic = tally[i].Traffic;
						Cell lane = Zone.GetCell(x, y);
						var floor = KingdomRoads.FindOurFloor(lane, out _);
						Evidence.Append("\nroad-target-lane=").Append(x).Append(',').Append(y)
							.Append("; entrance=").Append(anchor.Key).Append("; traffic=").Append(traffic)
							.Append("; wear=").Append(KingdomRoadRules.WearAt(traffic))
							.Append("; floor=").Append(floor).Append("; walkable=")
							.Append(KingdomRoads.Walkable(lane));
					}
				}
				catch (Exception error)
				{
					// Diagnostics must not replace the original production refusal.
					Evidence.Append("\nroad-census-error=")
						.Append(KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message));
				}
			}
		}
	}
}
