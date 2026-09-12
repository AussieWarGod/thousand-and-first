using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Where a seeded town lot may stand (native run 34). The first lot finder judged only bare
	/// ground, crowding and the heart reserve, then let <c>KingdomPlots.Stake</c> refuse at 53,1:
	/// the tentrow's authored public ingress lane fell outside the zone at the top edge. The judge
	/// of a candidate is now production's own plot preflight, <c>KingdomPlots.TryPreparePlotPayload</c>
	/// (typed architecture pose, the physical DoorToLane ingress routes, stamper and delve-link
	/// preflights) -- the very read Stake makes -- asked BEFORE Stake, so a rect it would refuse is
	/// never staked. Candidates also stay off every zone edge by the lane depth the road rule
	/// declares: the lane endpoint is one cell beyond the reserved road margin
	/// (<c>KingdomRoadRules.TryAuthoredLane</c>), so that depth is <c>RoadMargin + 1</c>, derived,
	/// not typed. Every refusal is journaled (bounded), and the seed only refuses when the
	/// candidate list is exhausted.
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		/// <summary>Refusal reasons journaled in full per lot; the rest are counted.</summary>
		internal const int JournaledLotRefusals = 6;

		private sealed partial class Frame
		{
			/// <summary>Built roots whose public ingress lanes later lots must not cover.</summary>
			internal readonly List<GameObject> SeededRoots = new List<GameObject>();
			/// <summary>How many lane cells the last candidate search held reserved.</summary>
			internal int ReservedLaneCells;

			/// <summary>Exact rects Stake itself refused after the preflight passed; never retried.</summary>
			internal readonly List<KingdomPlotRules.PlotRect> RefusedLots =
				new List<KingdomPlotRules.PlotRect>();

			/// <summary>The lane depth the authored road rule declares beyond a lot's own rect.</summary>
			internal static int LaneDepth { get { return KingdomPlotRules.RoadMargin + 1; } }

			/// <summary>The first candidate rect production's own preflight accepts for Entry, clear
			/// of every existing work's lanes and of the heart's future envelopes, with
			/// the prepared intent it accepted. False when every candidate was refused; the
			/// refusals are in the journal either way.</summary>
			internal bool TryFindLot(KingdomPlots.GroundGrid Grid, KingdomRules.BuildEntry Entry,
				int Width, int Height, string SkinKey, out KingdomPlotRules.PlotRect Lot,
				out KingdomArchitectureIntent Intent)
			{
				KingdomPlotRules.PlotRect heart;
				Require(KingdomPlots.TryReadRect(Heart, out heart),
					"taf-camp-town-seed-heart-rect: the founded heart's rect could not be read");
				int depth = LaneDepth;
				int refused = 0;
				// Native run 42: a later lot walled over an earlier lot's public ingress lane and
				// the daily seal refused every pass after. Every existing work's lane cells (door
				// through the lane endpoint, read by production's own authored-lane reader) are
				// held reserved, and a candidate's own lanes may not land inside any existing rect.
				HashSet<int> reserved = ReservedLanes(heart);
				ReservedLaneCells = reserved.Count;
				// Native run 44: the heart's FUTURE envelopes, one per rung this persona climbs,
				// derived from the authored chain, with their own lanes folded into the reserve.
				List<KingdomPlotRules.PlotRect> envelopes = HeartEnvelopes(heart, reserved);
				List<KingdomPlotRules.PlotRect> existing = new List<KingdomPlotRules.PlotRect>(SeededLots);
				existing.AddRange(envelopes);
				List<KingdomPlotRules.PlotRect> candidates = InteriorFirst(Width, Height, depth);
				for (int i = 0; i < candidates.Count; i++)
				{
					KingdomPlotRules.PlotRect candidate = candidates[i];
					if (CrowdsEnvelope(candidate, envelopes)
						|| KingdomPlotRules.CrowdsExisting(candidate, SeededLots)
						|| IsRefused(candidate) || Grid.AnyRefusal(candidate)
						|| CoversLane(candidate, reserved)
						|| !BareAndLifeless(Grid, candidate)) continue;
					string failure;
					string payload;
					if (KingdomPlots.TryPreparePlotPayload(System, Zone, candidate, Entry.Key,
						Entry.Category, SkinKey, out Intent, out payload, out failure)
						&& OwnLanesClear(Intent, candidate, existing, out failure))
					{
						Lot = candidate;
						return true;
					}
					refused++;
					if (refused <= JournaledLotRefusals)
						Evidence.Append("\nsynthetic-town-lot-refused key=").Append(Entry.Key)
							.Append("; rect=").Append(candidate.X1).Append(',').Append(candidate.Y1)
							.Append(' ').Append(candidate.X2).Append(',').Append(candidate.Y2)
							.Append("; edge distance=").Append(EdgeDistance(candidate))
							.Append("; preflight=").Append(KingdomScenarioRules.Bounded(failure));
				}
				Evidence.Append("\nsynthetic-town-lot-exhausted key=").Append(Entry.Key)
					.Append("; lane-reserved=").Append(reserved.Count)
					.Append("; heart-envelope-reserved=").Append(ReservedEnvelopeCells)
					.Append("; preflight refusals=").Append(refused)
					.Append("; stake refusals=").Append(RefusedLots.Count)
					.Append("; seeded=").Append(SeededLots.Count);
				Lot = default(KingdomPlotRules.PlotRect);
				Intent = null;
				return false;
			}

			/// <summary>Every Width-by-Height rect at least Depth cells inside the zone, deepest
			/// interior first (native run 40 staked the first accepted lot edge-adjacent at 4,2),
			/// ties broken by row then column so the order is deterministic.</summary>
			private List<KingdomPlotRules.PlotRect> InteriorFirst(int Width, int Height, int Depth)
			{
				List<KingdomPlotRules.PlotRect> candidates = new List<KingdomPlotRules.PlotRect>();
				for (int y = Depth; y + Height + Depth <= Zone.Height; y++)
					for (int x = Depth; x + Width + Depth <= Zone.Width; x++)
						candidates.Add(new KingdomPlotRules.PlotRect(
							x, y, x + Width - 1, y + Height - 1));
				candidates.Sort(delegate(KingdomPlotRules.PlotRect a, KingdomPlotRules.PlotRect b)
				{
					int byDepth = EdgeDistance(b).CompareTo(EdgeDistance(a));
					if (byDepth != 0) return byDepth;
					int byRow = a.Y1.CompareTo(b.Y1);
					return byRow != 0 ? byRow : a.X1.CompareTo(b.X1);
				});
				return candidates;
			}

			/// <summary>How many cells a rect stands inside the nearest zone edge.</summary>
			private int EdgeDistance(KingdomPlotRules.PlotRect Rect)
			{
				return Math.Min(Math.Min(Rect.X1, Rect.Y1),
					Math.Min(Zone.Width - 1 - Rect.X2, Zone.Height - 1 - Rect.Y2));
			}

			/// <summary>Every public ingress lane cell of the heart and of every seeded root: the
			/// canonical route from the door, the reserved margin cell and the lane endpoint, read
			/// through production's own receipt reader and authored-lane rule. A root whose
			/// receipt cannot be read refuses: a lane that cannot be read cannot be protected.
			/// </summary>
			private HashSet<int> ReservedLanes(KingdomPlotRules.PlotRect HeartRect)
			{
				HashSet<int> cells = new HashSet<int>();
				AddLanes(Heart, HeartRect, cells);
				for (int i = 0; i < SeededRoots.Count; i++)
				{
					KingdomPlotRules.PlotRect rect = default(KingdomPlotRules.PlotRect);
					Require(GameObject.Validate(SeededRoots[i])
						&& KingdomPlots.TryReadRect(SeededRoots[i], out rect),
						"taf-camp-town-seed-lane-rect: a seeded root lost its rect");
					AddLanes(SeededRoots[i], rect, cells);
				}
				return cells;
			}

			private void AddLanes(GameObject Root, KingdomPlotRules.PlotRect Rect, HashSet<int> Cells)
			{
				string failure;
				KingdomArchitectureIntent intent = null;
				ArchitectureLayoutSnapshot snapshot = null;
				Require(KingdomArchitectureRuntime.TryRead(Root, out intent, out failure)
					&& KingdomArchitectureRuntime.TryDecode(intent, out snapshot, out failure),
					"taf-camp-town-seed-lane-unreadable: " + KingdomScenarioRules.Bounded(failure));
				List<ArchitecturePoint> lanes = new List<ArchitecturePoint>();
				LanesOf(snapshot, Rect, lanes);
				for (int i = 0; i < lanes.Count; i++) Cells.Add(Pack(lanes[i].X, lanes[i].Y));
			}

			/// <summary>Door, route and lane endpoint of every public entrance, by the same
			/// authored-lane rule <c>KingdomArchitectureRuntime.RoadIngress</c> verifies.</summary>
			private void LanesOf(ArchitectureLayoutSnapshot Snapshot, KingdomPlotRules.PlotRect Rect,
				List<ArchitecturePoint> Lanes)
			{
				if (Snapshot == null || Snapshot.Anchors == null) return;
				for (int i = 0; i < Snapshot.Anchors.Count; i++)
				{
					ArchitectureAnchor anchor = Snapshot.Anchors[i];
					if (anchor == null || anchor.Key == null || !(anchor.Key == "entrance:public"
						|| anchor.Key.StartsWith("entrance:public@", StringComparison.Ordinal)))
						continue;
					List<ArchitecturePoint> route = new List<ArchitecturePoint>();
					int doorX, doorY, laneX, laneY;
					Require(KingdomRoadRules.TryAuthoredLane(Snapshot, Rect, anchor, route,
						out doorX, out doorY, out laneX, out laneY),
						"taf-camp-town-seed-lane-unresolved: a public entrance has no authored lane");
					Lanes.Add(new ArchitecturePoint(doorX, doorY));
					Lanes.AddRange(route);
					Lanes.Add(new ArchitecturePoint(laneX, laneY));
				}
			}

			private bool CoversLane(KingdomPlotRules.PlotRect Candidate, HashSet<int> Reserved)
			{
				for (int y = Candidate.Y1; y <= Candidate.Y2; y++)
					for (int x = Candidate.X1; x <= Candidate.X2; x++)
						if (Reserved.Contains(Pack(x, y))) return true;
				return false;
			}

			/// <summary>The candidate's own lanes, decoded from the intent the preflight prepared,
			/// must not land inside any existing work's rect.</summary>
			private bool OwnLanesClear(KingdomArchitectureIntent Intent, KingdomPlotRules.PlotRect Candidate,
				List<KingdomPlotRules.PlotRect> Existing, out string Failure)
			{
				ArchitectureLayoutSnapshot snapshot;
				if (!KingdomArchitectureRuntime.TryDecode(Intent, out snapshot, out Failure)) return false;
				List<ArchitecturePoint> lanes = new List<ArchitecturePoint>();
				LanesOf(snapshot, Candidate, lanes);
				for (int i = 0; i < lanes.Count; i++)
					for (int e = 0; e < Existing.Count; e++)
						if (Existing[e].Contains(lanes[i].X, lanes[i].Y))
						{
							Failure = "own ingress lane cell " + lanes[i].X + "," + lanes[i].Y
								+ " lies inside an existing work";
							return false;
						}
				Failure = null;
				return true;
			}

			private int Pack(int X, int Y) { return Y * Zone.Width + X; }

			private bool IsRefused(KingdomPlotRules.PlotRect Candidate)
			{
				for (int i = 0; i < RefusedLots.Count; i++)
					if (RefusedLots[i].X1 == Candidate.X1 && RefusedLots[i].Y1 == Candidate.Y1
						&& RefusedLots[i].X2 == Candidate.X2 && RefusedLots[i].Y2 == Candidate.Y2)
						return true;
				return false;
			}

			private bool BareAndLifeless(KingdomPlots.GroundGrid Grid, KingdomPlotRules.PlotRect Lot)
			{
				for (int y = Lot.Y1; y <= Lot.Y2; y++)
					for (int x = Lot.X1; x <= Lot.X2; x++)
					{
						if (Grid.KindAt(x, y) != KingdomPlotRules.GroundKind.Bare) return false;
						Cell cell = Zone.GetCell(x, y);
						if (cell == null) return false;
						foreach (GameObject item in cell.GetObjects())
							if (GameObject.Validate(item) && (item.IsAlive || item.IsPlayer()))
								return false;
					}
				return true;
			}
		}
	}
}
