using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRuntime
	{
		/// <summary>Re-proves the frozen entrance routes against current ground without requiring
		/// road evidence. Construction calls this before debit and after every final stamp.</summary>
		internal static bool TryVerifyPhysicalIngressRoutes(Zone Z,
			KingdomPlotRules.PlotRect Rect, ArchitectureLayoutSnapshot Snapshot,
			out string Failure)
		{
			List<ArchitecturePoint> lanes = new List<ArchitecturePoint>();
			return TryPhysicalRoadIngressLanes(Z, Rect, Snapshot, lanes, out Failure);
		}

		/// <summary>
		/// Resolves every authored public threshold to its exact exterior lane. The route is the
		/// same immutable DoorToLane route later walked by settlement errands: canonical snapshot
		/// cells, the reserved road margin, then its cardinal lane endpoint. Every intermediate
		/// cell must be physically walkable now; a blocked or liquid approach refuses the whole
		/// frontage instead of searching for a nearby road.
		/// </summary>
		private static bool TryPhysicalRoadIngressLanes(Zone Z,
			KingdomPlotRules.PlotRect Rect, ArchitectureLayoutSnapshot Snapshot,
			IList<ArchitecturePoint> Lanes, out string Failure)
		{
			Failure = null;
			if (Lanes == null) return Fail("road ingress has no result buffer", out Failure);
			Lanes.Clear();
			if (Z == null || Snapshot == null || Snapshot.Anchors == null)
				return Fail("road ingress architecture or zone is absent", out Failure);

			List<ArchitecturePoint> resolved = new List<ArchitecturePoint>();
			HashSet<int> unique = new HashSet<int>();
			bool foundEntrance = false;
			for (int i = 0; i < Snapshot.Anchors.Count; i++)
			{
				ArchitectureAnchor anchor = Snapshot.Anchors[i];
				if (anchor == null || anchor.Key == null || !(anchor.Key == "entrance:public"
					|| anchor.Key.StartsWith("entrance:public@",
						System.StringComparison.Ordinal))) continue;
				foundEntrance = true;
				List<ArchitecturePoint> route = new List<ArchitecturePoint>();
				if (!KingdomRoadRules.TryAuthoredLane(Snapshot, Rect, anchor, route,
					out _, out _, out int laneX, out int laneY))
					return Fail("public entrance has no exact authored DoorToLane route",
						out Failure);
				for (int r = 0; r < route.Count; r++)
				{
					ArchitecturePoint point = route[r];
					if (!KingdomRoadRules.InBounds(point.X, point.Y, Z.Width, Z.Height)
						|| !KingdomRoads.Walkable(Z.GetCell(point.X, point.Y)))
						return Fail("authored public ingress is physically blocked at "
							+ point.X + "," + point.Y, out Failure);
				}
				if (!KingdomRoadRules.InBounds(laneX, laneY, Z.Width, Z.Height)
					|| !KingdomRoads.Walkable(Z.GetCell(laneX, laneY)))
					return Fail("authored public ingress lane is physically blocked or outside the zone",
						out Failure);
				int packed = KingdomRoadRules.Pack(laneX, laneY, Z.Width);
				if (unique.Add(packed)) resolved.Add(new ArchitecturePoint(laneX, laneY));
			}
			if (!foundEntrance)
				return Fail("road-facing architecture has no entrance:public anchor", out Failure);
			for (int i = 0; i < resolved.Count; i++) Lanes.Add(resolved[i]);
			return true;
		}
	}
}
