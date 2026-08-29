using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRoads
	{
		// Everything anyone in this settlement has a reason to walk between, in a stable order:
		// home to the nearest work, each work to the heart, the heart to the way out, and every
		// plot's own door to the lane beside it.
		private static List<Errand> Errands(KingdomSystem System, Zone Z, IList<KingdomPlotRules.PlotRect> Plots)
		{
			List<Errand> errands = new List<Errand>();
			if (Z == null)
			{
				return errands;
			}
			List<KingdomLayoutRules.LayoutMark> marks = KingdomLayout.ReadMarks(Z);
			List<KingdomLayoutRules.LayoutMark> homes = new List<KingdomLayoutRules.LayoutMark>();
			List<KingdomLayoutRules.LayoutMark> works = new List<KingdomLayoutRules.LayoutMark>();
			for (int i = 0; i < marks.Count; i++)
			{
				KingdomLayoutRules.LayoutMark mark = marks[i];
				if (mark.Purpose == KingdomLayoutRules.LayoutPurpose.Housing)
				{
					homes.Add(mark);
				}
				else if (mark.Purpose == KingdomLayoutRules.LayoutPurpose.Civic
					|| mark.Purpose == KingdomLayoutRules.LayoutPurpose.Field
					|| mark.Purpose == KingdomLayoutRules.LayoutPurpose.Storage
					|| mark.Purpose == KingdomLayoutRules.LayoutPurpose.Sited)
				{
					works.Add(mark);
				}
			}
			// The route loop below is the bounded work. Its input must still describe the whole
			// legal city: truncating here made every work and plot after the first twelve invisible
			// forever rather than queued. Canonical coordinate order also keeps the rotating window
			// stable when Qud enumerates objects differently after a reload.
			homes.Sort(CompareMarks);
			works.Sort(CompareMarks);
			bool hasRite = KingdomPlots.TryRiteGround(Z, out var riteX, out var riteY);
			bool hasHeart = KingdomPlotRules.TryHeart(marks, hasRite, riteX, riteY, out var heartX, out var heartY);
			for (int i = 0; i < homes.Count; i++)
			{
				int nearest = Nearest(works, homes[i].X, homes[i].Y);
				if (nearest >= 0)
				{
					errands.Add(new Errand(homes[i].X, homes[i].Y, works[nearest].X, works[nearest].Y,
						KingdomRoadRules.RouteKind.HomeToWork));
				}
			}
			if (hasHeart)
			{
				for (int i = 0; i < works.Count; i++)
				{
					errands.Add(new Errand(works[i].X, works[i].Y, heartX, heartY, KingdomRoadRules.RouteKind.WorkToHeart));
				}
				KingdomRules.Frontier edges = (System != null)
					? KingdomRules.FrontierEdges(Z.ZoneID, System.ClaimedZones)
					: KingdomRules.Frontier.None;
				if (KingdomRoadRules.TryGate(Z.Width, Z.Height, edges, heartX, heartY, out var gateX, out var gateY))
				{
					errands.Add(new Errand(heartX, heartY, gateX, gateY, KingdomRoadRules.RouteKind.HeartToGate));
				}
				AddEntranceErrands(Z, Plots, heartX, heartY, errands);
			}
			return errands;
		}

		private static int CompareMarks(KingdomLayoutRules.LayoutMark A,
			KingdomLayoutRules.LayoutMark B)
		{
			int byY = A.Y.CompareTo(B.Y);
			if (byY != 0) return byY;
			int byX = A.X.CompareTo(B.X);
			if (byX != 0) return byX;
			return ((int)A.Purpose).CompareTo((int)B.Purpose);
		}

		/// <summary>
		/// Adds every current authored public entrance to the road rotation. The immutable
		/// architecture receipt is authority for a finished current-schema building; inventing a
		/// heart-facing door from its rectangle can aim the road at a wall. Receipt-less old plots
		/// retain the deterministic geometric door as their compatibility path. A partial or corrupt
		/// receipt fails closed instead of silently becoming a different building.
		/// </summary>
		private static void AddEntranceErrands(Zone Z, IList<KingdomPlotRules.PlotRect> Plots,
			int HeartX, int HeartY, IList<Errand> Errands)
		{
			if (Z == null || Plots == null || Errands == null) return;
			Dictionary<string, List<GameObject>> roots =
				new Dictionary<string, List<GameObject>>(System.StringComparer.Ordinal);
			KingdomSurvey survey = KingdomSurvey.Take(Z);
			for (int i = 0; i < survey.PlotRoots.Count; i++)
			{
				GameObject item = survey.PlotRoots[i];
				KingdomPlotRules.PlotRect rect;
				if (!KingdomPlots.TryReadRect(item, out rect)) continue;
				string key = RectKey(rect);
				List<GameObject> objects;
				if (!roots.TryGetValue(key, out objects))
				{
					objects = new List<GameObject>();
					roots.Add(key, objects);
				}
				objects.Add(item);
			}

			List<KingdomPlotRules.PlotRect> unique = new List<KingdomPlotRules.PlotRect>();
			HashSet<string> plotKeys = new HashSet<string>(System.StringComparer.Ordinal);
			for (int i = 0; i < Plots.Count; i++)
			{
				string key = RectKey(Plots[i]);
				if (plotKeys.Add(key)) unique.Add(Plots[i]);
			}
			unique.Sort(CompareRects);
			HashSet<string> routes = new HashSet<string>(System.StringComparer.Ordinal);

			for (int p = 0; p < unique.Count; p++)
			{
				KingdomPlotRules.PlotRect rect = unique[p];
				List<GameObject> objects;
				bool receiptEvidence = false;
				bool exactReceipt = false;
				bool legacyReceiptFallback = false;
				if (roots.TryGetValue(RectKey(rect), out objects))
				{
					for (int o = 0; o < objects.Count; o++)
					{
						GameObject root = objects[o];
						if (HasArchitectureReceiptEvidence(root)) receiptEvidence = true;
						KingdomArchitectureIntent intent;
						ArchitectureLayoutSnapshot snapshot;
						string failure;
						if (!KingdomArchitectureRuntime.TryRead(root, out intent, out failure)
							|| !KingdomArchitectureRuntime.TryDecode(intent, out snapshot, out failure))
							continue;
						exactReceipt = true;
						bool currentSnapshot = KingdomArchitectureRules.IsCurrentSnapshotEncoding(
							intent.EncodedSnapshot);
						for (int a = 0; a < snapshot.Anchors.Count; a++)
						{
							ArchitectureAnchor anchor = snapshot.Anchors[a];
							if (anchor == null || !(anchor.Key == "entrance:public"
								|| anchor.Key.StartsWith("entrance:public@",
									System.StringComparison.Ordinal))) continue;
							if (!AddAuthoredEntranceErrand(Z, rect, snapshot, anchor,
								routes, Errands) && !currentSnapshot) legacyReceiptFallback = true;
						}
					}
				}
				if (legacyReceiptFallback)
				{
					// An a1 snapshot predates the exterior-route invariant. Preserve its old
					// heart-facing geometric path if its authored boundary cannot name one.
					if (KingdomPlotRules.TryDoor(rect, HeartX, HeartY,
						out int oldDoorX, out int oldDoorY))
						AddEntranceErrand(rect, oldDoorX, oldDoorY, routes, Errands);
				}
				if (exactReceipt || receiptEvidence) continue;

				// Pre-schema plots and a currently staked plan have no frozen receipt to read.
				// Preserve their old deterministic geometry until the real authored receipt exists.
				int legacyDoorX;
				int legacyDoorY;
				if (KingdomPlotRules.TryDoor(rect, HeartX, HeartY,
					out legacyDoorX, out legacyDoorY))
					AddEntranceErrand(rect, legacyDoorX, legacyDoorY, routes, Errands);
			}
		}

		private static bool HasArchitectureReceiptEvidence(GameObject Object)
		{
			return Object != null && (Object.HasIntProperty(KingdomArchitectureRuntime.SchemaProperty)
				|| Object.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty)
				|| Object.HasIntProperty(KingdomArchitectureRuntime.BuildKeyProperty)
				|| Object.HasStringProperty(KingdomArchitectureRuntime.BuildKeyProperty)
				|| Object.HasIntProperty(KingdomArchitectureRuntime.SnapshotProperty)
				|| Object.HasStringProperty(KingdomArchitectureRuntime.SnapshotProperty)
				|| Object.HasIntProperty(KingdomArchitectureRuntime.HashProperty)
				|| Object.HasStringProperty(KingdomArchitectureRuntime.HashProperty));
		}

		private static void AddEntranceErrand(KingdomPlotRules.PlotRect Rect, int DoorX,
			int DoorY, ISet<string> Routes, IList<Errand> Errands)
		{
			int laneX;
			int laneY;
			if (!KingdomRoadRules.TryLane(Rect, DoorX, DoorY, out laneX, out laneY)) return;
			string key = DoorX + "," + DoorY + ">" + laneX + "," + laneY;
			if (!Routes.Add(key)) return;
			Errands.Add(new Errand(DoorX, DoorY, laneX, laneY,
				KingdomRoadRules.RouteKind.DoorToLane));
		}

		private static bool AddAuthoredEntranceErrand(Zone Z,
			KingdomPlotRules.PlotRect Rect, ArchitectureLayoutSnapshot Snapshot,
			ArchitectureAnchor Entrance, ISet<string> Routes, IList<Errand> Errands)
		{
			List<ArchitecturePoint> exact = new List<ArchitecturePoint>();
			if (!KingdomRoadRules.TryAuthoredLane(Snapshot, Rect, Entrance, exact,
				out int doorX, out int doorY, out int laneX, out int laneY)
				|| !InZone(Z, doorX, doorY) || !InZone(Z, laneX, laneY)) return false;
			for (int i = 0; i < exact.Count; i++)
				if (!InZone(Z, exact[i].X, exact[i].Y)) return false;
			string key = doorX + "," + doorY + ">" + laneX + "," + laneY;
			if (!Routes.Add(key)) return true;
			Errands.Add(new Errand(doorX, doorY, laneX, laneY,
				KingdomRoadRules.RouteKind.DoorToLane, exact));
			return true;
		}

		private static bool InZone(Zone Z, int X, int Y)
		{
			return Z != null && X >= 0 && X < Z.Width && Y >= 0 && Y < Z.Height;
		}

		private static int CompareRects(KingdomPlotRules.PlotRect A,
			KingdomPlotRules.PlotRect B)
		{
			int byY = A.Y1.CompareTo(B.Y1);
			if (byY != 0) return byY;
			int byX = A.X1.CompareTo(B.X1);
			if (byX != 0) return byX;
			int byY2 = A.Y2.CompareTo(B.Y2);
			return byY2 != 0 ? byY2 : A.X2.CompareTo(B.X2);
		}

		private static string RectKey(KingdomPlotRules.PlotRect Rect)
		{
			return Rect.X1 + "," + Rect.Y1 + "," + Rect.X2 + "," + Rect.Y2;
		}

		private static int Nearest(IList<KingdomLayoutRules.LayoutMark> Marks, int X, int Y)
		{
			int best = -1;
			int bestDistance = 0;
			for (int i = 0; i < Marks.Count; i++)
			{
				int distance = KingdomLayoutRules.Chebyshev(X, Y, Marks[i].X, Marks[i].Y);
				if (distance == 0)
				{
					continue;
				}
				if (best < 0 || distance < bestDistance)
				{
					best = i;
					bestDistance = distance;
				}
			}
			return best;
		}

	}
}
