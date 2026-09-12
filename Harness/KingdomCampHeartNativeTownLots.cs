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
			/// <summary>Exact rects Stake itself refused after the preflight passed; never retried.</summary>
			internal readonly List<KingdomPlotRules.PlotRect> RefusedLots =
				new List<KingdomPlotRules.PlotRect>();

			/// <summary>The lane depth the authored road rule declares beyond a lot's own rect.</summary>
			internal static int LaneDepth { get { return KingdomPlotRules.RoadMargin + 1; } }

			/// <summary>The first candidate rect production's own preflight accepts for Entry, with
			/// the prepared intent it accepted. False when every candidate was refused; the
			/// refusals are in the journal either way.</summary>
			internal bool TryFindLot(KingdomPlots.GroundGrid Grid, KingdomRules.BuildEntry Entry,
				int Width, int Height, string SkinKey, out KingdomPlotRules.PlotRect Lot,
				out KingdomArchitectureIntent Intent)
			{
				KingdomPlotRules.PlotRect heart;
				Require(KingdomPlots.TryReadRect(Heart, out heart),
					"taf-camp-town-seed-heart-rect: the founded heart's rect could not be read");
				KingdomPlotRules.PlotRect reserve = new KingdomPlotRules.PlotRect(
					heart.X1 - HeartGrowthMarginX - KingdomPlotRules.RoadMargin,
					heart.Y1 - HeartGrowthMarginY - KingdomPlotRules.RoadMargin,
					heart.X2 + HeartGrowthMarginX + KingdomPlotRules.RoadMargin,
					heart.Y2 + HeartGrowthMarginY + KingdomPlotRules.RoadMargin);
				int depth = LaneDepth;
				int refused = 0;
				List<KingdomPlotRules.PlotRect> candidates = InteriorFirst(Width, Height, depth);
				for (int i = 0; i < candidates.Count; i++)
				{
					KingdomPlotRules.PlotRect candidate = candidates[i];
					// The reserve already carries the road margin, so the lot itself is tested.
					if (KingdomPlotRules.Overlaps(candidate, reserve)
						|| KingdomPlotRules.CrowdsExisting(candidate, SeededLots)
						|| IsRefused(candidate) || Grid.AnyRefusal(candidate)
						|| !BareAndLifeless(Grid, candidate)) continue;
					string failure;
					string payload;
					if (KingdomPlots.TryPreparePlotPayload(System, Zone, candidate, Entry.Key,
						Entry.Category, SkinKey, out Intent, out payload, out failure))
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
