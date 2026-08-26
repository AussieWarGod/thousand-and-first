using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		/// <summary>
		/// Every cell of a zone read once, with a running count of refusing cells so a rect can be
		/// rejected without walking it. Built once per siting pass: an XL plot has 280 cells and a
		/// surface zone has sixteen hundred anchors, and surveying each anchor's rect on its own
		/// would read the same cell hundreds of times.
		/// </summary>
		public sealed class GroundGrid
		{
			public int Width;

			public int Height;

			private readonly KingdomPlotRules.GroundKind[] Kinds;

			private readonly string[] Blockers;

			// Inclusive-exclusive prefix sums of refusing cells, (Width+1) by (Height+1).
			private readonly int[] Refusals;

			public GroundGrid(Zone Z)
				: this(Z, -1, -1)
			{
			}

			/// <summary>
			/// Reads the zone while treating one future plan-stake cell as held. This is a
			/// mutation-free way to site a reserved lot beside its physical survey stake: the marker
			/// itself is not created until the production preview has been accepted, and it must never
			/// become an obstruction inside the map it later authorizes.
			/// </summary>
			public GroundGrid(Zone Z, int FutureStakeX, int FutureStakeY)
			{
				Width = (Z == null) ? 0 : Z.Width;
				Height = (Z == null) ? 0 : Z.Height;
				Kinds = new KingdomPlotRules.GroundKind[Width * Height];
				Blockers = new string[Width * Height];
				Refusals = new int[(Width + 1) * (Height + 1)];
				for (int y = 0; y < Height; y++)
				{
					for (int x = 0; x < Width; x++)
					{
						KingdomPlotRules.GroundKind kind;
						string blocker;
						if (x == FutureStakeX && y == FutureStakeY)
						{
							kind = KingdomPlotRules.GroundKind.Held;
							blocker = "the plan stake";
						}
						else kind = ReadGround(Z.GetCell(x, y), out blocker);
						Kinds[y * Width + x] = kind;
						Blockers[y * Width + x] = blocker;
						Refusals[(y + 1) * (Width + 1) + (x + 1)] =
							Refusals[y * (Width + 1) + (x + 1)]
							+ Refusals[(y + 1) * (Width + 1) + x]
							- Refusals[y * (Width + 1) + x]
							+ (KingdomPlotRules.Refuses(kind) ? 1 : 0);
					}
				}
			}

			public KingdomPlotRules.GroundKind KindAt(int X, int Y)
			{
				if (X < 0 || Y < 0 || X >= Width || Y >= Height)
				{
					return KingdomPlotRules.GroundKind.Held;
				}
				return Kinds[Y * Width + X];
			}

			/// <summary>Whether any cell of a rect refuses the plot. O(1).</summary>
			public bool AnyRefusal(KingdomPlotRules.PlotRect Rect)
			{
				if (Rect.X1 < 0 || Rect.Y1 < 0 || Rect.X2 >= Width || Rect.Y2 >= Height)
				{
					return true;
				}
				int stride = Width + 1;
				int total = Refusals[(Rect.Y2 + 1) * stride + (Rect.X2 + 1)]
					- Refusals[Rect.Y1 * stride + (Rect.X2 + 1)]
					- Refusals[(Rect.Y2 + 1) * stride + Rect.X1]
					+ Refusals[Rect.Y1 * stride + Rect.X1];
				return total > 0;
			}

			/// <summary>The first refusing cell of a rect, reading north-then-west, and what
			/// stands there. Walks the rect, so it is only ever called on the one rect whose
			/// refusal the founder is about to be told about.</summary>
			public bool TryFirstRefusal(KingdomPlotRules.PlotRect Rect, out int X, out int Y, out KingdomPlotRules.GroundKind Kind, out string Blocker)
			{
				for (int y = Rect.Y1; y <= Rect.Y2; y++)
				{
					for (int x = Rect.X1; x <= Rect.X2; x++)
					{
						KingdomPlotRules.GroundKind kind = KindAt(x, y);
						if (KingdomPlotRules.Refuses(kind))
						{
							X = x;
							Y = y;
							Kind = kind;
							Blocker = (x < 0 || y < 0 || x >= Width || y >= Height) ? "the edge of the zone" : Blockers[y * Width + x];
							return true;
						}
					}
				}
				X = 0;
				Y = 0;
				Kind = KingdomPlotRules.GroundKind.Bare;
				Blocker = null;
				return false;
			}

			/// <summary>Every cell of a rect, in the clearance table's terms.</summary>
			public List<KingdomPlotRules.GroundKind> CellsOf(KingdomPlotRules.PlotRect Rect)
			{
				List<KingdomPlotRules.GroundKind> cells = new List<KingdomPlotRules.GroundKind>(Rect.Area);
				for (int y = Rect.Y1; y <= Rect.Y2; y++)
				{
					for (int x = Rect.X1; x <= Rect.X2; x++)
					{
						cells.Add(KindAt(x, y));
					}
				}
				return cells;
			}
		}

		private sealed class GrowthRow
		{
			public int Kind;
			public int X;
			public int Y;
			public string Blueprint;
			public string Id;
			public int State;
		}

		private sealed class GrowthPlan
		{
			public string PredecessorId;
			public string SuccessorId;
			public string SuccessorKey;
			public string PlotId;
			public KingdomPlotRules.PlotRect Old;
			public KingdomPlotRules.PlotRect Grown;
			public KingdomPlotRules.RoofState Roof;
			public int HeartX;
			public int HeartY;
			public bool KeepInner;
			public string Wall;
			public bool Done;
			public List<GrowthRow> Rows;
		}

		/// <summary>
		/// Furnishes a finished plot from its design's own population table, the way vanilla huts
		/// are furnished (<c>ZoneBuilderSandbox.PlaceHut</c>'s own last step) &mdash; but only ever
		/// into interior cells the plot itself laid empty, never over anything.
		/// </summary>
		private sealed class FurnishRow
		{
			public string Blueprint;
			public int X;
			public int Y;
			public string Id;
			public bool Settled;
		}
	}
}
