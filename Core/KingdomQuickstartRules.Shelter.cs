using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// The founding shelter's own ground: the lots the quickstart stakes, and the exterior cells
	/// of each lot's authored ingress route. The stake's public-ingress preflight walks that route
	/// and refuses a cell that is not physically walkable, so the camp must bare it too; on the
	/// dunes it happened to be bare already, on the marsh and in the canyon it was not.
	/// </summary>
	public static partial class KingdomQuickstartRules
	{
		/// <summary>One lot's authored public threshold and the way its route leaves the lot.</summary>
		private readonly struct ShelterThreshold
		{
			internal readonly int X;
			internal readonly int Y;
			internal readonly int StepX;
			internal readonly int StepY;

			internal ShelterThreshold(int X, int Y, int StepX, int StepY)
			{
				this.X = X;
				this.Y = Y;
				this.StepX = StepX;
				this.StepY = StepY;
			}
		}

		// The shelter lots: two Small plots (6x4) stacked west of the supply column, clear of the
		// reserved role cells at x=28, of the founder's start cell, and of the heart's extreme
		// survey (which begins at x=31 on an 80-wide zone), so neither row is ever marked yielding
		// and neither contends with a heart rung for its ground. Two tent rows carry three beds
		// each, six in all, so arrivals are not refused for want of room on the day the rows
		// finish.
		private static readonly KingdomPlotRules.PlotRect[] ShelterLots = BuildShelterLots();

		// The tent row carries one authored public threshold: the '+' at canonical (3,3) of the
		// 6x4 map housing-tentrow-s1, the middle of its southern edge. The heart frontage law
		// poses each lot towards the founding heart, which stands south of lot A's centre and
		// north of lot B's, so lot A is laid facing south and lot B facing north; the threshold
		// therefore lands on each lot's outward edge and its route leaves straight out from there.
		// This authority is engine-free and may not read the plot machinery to say so, so
		// DevTests/KingdomQuickstartShelterIngressTests.cs recomputes every one of these from the
		// shipped architecture with the same KingdomRoadRules.TryAuthoredLane the stake walks, and
		// refuses any drift between the two.
		private static readonly ShelterThreshold[] ShelterThresholds =
		{
			new ShelterThreshold(23, 9, 0, -1),
			new ShelterThreshold(24, 16, 0, 1)
		};

		/// <summary>
		/// How far outside its lot one route runs: the reserved road margin, then the lane endpoint
		/// one cell beyond it. This is <c>KingdomPlotRules.RoadMargin + 1</c>, pinned by the same
		/// recompute test.
		/// </summary>
		private const int ShelterIngressReach = 2;

		// Built from private copies rather than from the fields above, so no initialiser here
		// depends on the order two parts of one partial class happen to be compiled in.
		private static readonly ArchitecturePoint[] ShelterIngress =
			BuildShelterIngress(BuildShelterLots());

		/// <summary>How many shelter lots the founding pass stakes.</summary>
		public static int ShelterLotCount
		{
			get { return ShelterLots.Length; }
		}

		/// <summary>
		/// One reserved shelter lot, by index. <c>PlotRect</c> is a value, so a caller reads a
		/// copy and no caller can move the reservation this authority declares.
		/// </summary>
		public static KingdomPlotRules.PlotRect ShelterLot(int Index)
		{
			if (Index < 0 || Index >= ShelterLots.Length)
				throw new ArgumentOutOfRangeException("Index", "The quickstart reserves "
					+ ShelterLots.Length + " shelter lots.");
			return ShelterLots[Index];
		}

		/// <summary>How many cells outside the lots the shelter's authored ingress needs bared.</summary>
		public static int ShelterIngressCellCount
		{
			get { return ShelterIngress.Length; }
		}

		/// <summary>One derived exterior ingress cell, by index.</summary>
		public static void ShelterIngressCell(int Index, out int X, out int Y)
		{
			if (Index < 0 || Index >= ShelterIngress.Length)
				throw new ArgumentOutOfRangeException("Index", "The quickstart shelter walks "
					+ ShelterIngress.Length + " exterior ingress cells.");
			X = ShelterIngress[Index].X;
			Y = ShelterIngress[Index].Y;
		}

		/// <summary>The authored public threshold of one shelter lot, and the way out of it.</summary>
		public static void ShelterThresholdCell(int Index, out int X, out int Y,
			out int StepX, out int StepY)
		{
			if (Index < 0 || Index >= ShelterThresholds.Length)
				throw new ArgumentOutOfRangeException("Index", "The quickstart reserves "
					+ ShelterThresholds.Length + " shelter lots.");
			X = ShelterThresholds[Index].X;
			Y = ShelterThresholds[Index].Y;
			StepX = ShelterThresholds[Index].StepX;
			StepY = ShelterThresholds[Index].StepY;
		}

		/// <summary>
		/// The shelter's whole claim on prepared ground: both lot rectangles, because the
		/// authored-ground preflight refuses a lot holding a creature, an item, or open liquid;
		/// and every exterior cell of each lot's DoorToLane route, because the stake's public
		/// ingress preflight refuses a route cell that is not physically walkable, and unbared
		/// wilderness is not walkable where the ground is reed, water, or rock.
		/// </summary>
		internal static bool RequiresShelterGround(int X, int Y)
		{
			for (int i = 0; i < ShelterLots.Length; i++)
				if (ShelterLots[i].Contains(X, Y)) return true;
			for (int i = 0; i < ShelterIngress.Length; i++)
				if (ShelterIngress[i].X == X && ShelterIngress[i].Y == Y) return true;
			return false;
		}

		private static KingdomPlotRules.PlotRect[] BuildShelterLots()
		{
			return new KingdomPlotRules.PlotRect[]
			{
				new KingdomPlotRules.PlotRect(21, 9, 26, 12),
				new KingdomPlotRules.PlotRect(21, 13, 26, 16)
			};
		}

		/// <summary>
		/// Walks each declared threshold outward the way the route runs: the reserved margin, then
		/// the lane endpoint beyond it. A threshold that does not stand on its own lot's edge, or
		/// a step that does not leave the lot, is a declaration that no longer describes the
		/// ground, and this authority refuses to bare anything on it rather than bare the wrong
		/// cells.
		/// </summary>
		private static ArchitecturePoint[] BuildShelterIngress(
			KingdomPlotRules.PlotRect[] Lots)
		{
			if (Lots.Length != ShelterThresholds.Length)
				throw new InvalidOperationException(
					"Every quickstart shelter lot owes exactly one authored threshold.");
			List<ArchitecturePoint> cells = new List<ArchitecturePoint>();
			for (int i = 0; i < Lots.Length; i++)
			{
				ShelterThreshold door = ShelterThresholds[i];
				if (!Lots[i].Contains(door.X, door.Y)
					|| Math.Abs(door.StepX) + Math.Abs(door.StepY) != 1
					|| Lots[i].Contains(door.X + door.StepX, door.Y + door.StepY))
					throw new InvalidOperationException("The quickstart shelter lot at ("
						+ Lots[i].X1 + "," + Lots[i].Y1
						+ ") no longer carries its authored threshold on its own outward edge.");
				for (int distance = 1; distance <= ShelterIngressReach; distance++)
				{
					int x = door.X + door.StepX * distance;
					int y = door.Y + door.StepY * distance;
					for (int lot = 0; lot < Lots.Length; lot++)
						if (Lots[lot].Contains(x, y))
							throw new InvalidOperationException("The quickstart shelter route at ("
								+ x + "," + y + ") runs back through a reserved lot.");
					cells.Add(new ArchitecturePoint(x, y));
				}
			}
			return cells.ToArray();
		}

	}
}
