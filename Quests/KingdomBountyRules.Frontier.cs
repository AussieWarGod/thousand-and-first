using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomBountyRules
	{
		// ==================================================================================
		// The frontier edge
		// ==================================================================================

		/// <summary>Zones to a parasang on each axis, matching the engine's own zone-ID
		/// grammar.</summary>
		public const int ZonesPerParasang = 3;

		/// <summary>Neighbours a zone has on its own stratum.</summary>
		public const int NeighbourCount = 8;

		private static readonly int[] NeighbourDX = new int[NeighbourCount] { 0, 1, 1, 1, 0, -1, -1, -1 };

		private static readonly int[] NeighbourDY = new int[NeighbourCount] { -1, -1, 0, 1, 1, 1, 0, -1 };

		/// <summary>
		/// One of the eight neighbours of a zone, in a fixed order (north, then clockwise), given
		/// its position on the world's continuous zone grid &mdash; the parasang and in-parasang
		/// coordinates folded together as <c>parasang * 3 + zone</c>, which is the same fold
		/// <c>KingdomFounding.ZonesAdjacent</c> uses.
		/// </summary>
		/// <param name="GlobalX">Continuous zone X.</param>
		/// <param name="GlobalY">Continuous zone Y.</param>
		/// <param name="Step">0 through 7.</param>
		/// <param name="NeighbourX">Continuous zone X of the neighbour.</param>
		/// <param name="NeighbourY">Continuous zone Y of the neighbour.</param>
		/// <returns>False for a step outside 0..7, or a neighbour that would fall off the north or
		/// west edge of the world. Nothing is written when it does.</returns>
		public static bool TryNeighbour(int GlobalX, int GlobalY, int Step, out int NeighbourX, out int NeighbourY)
		{
			NeighbourX = 0;
			NeighbourY = 0;
			if (Step < 0 || Step >= NeighbourCount)
			{
				return false;
			}
			int x = GlobalX + NeighbourDX[Step];
			int y = GlobalY + NeighbourDY[Step];
			if (x < 0 || y < 0)
			{
				return false;
			}
			NeighbourX = x;
			NeighbourY = y;
			return true;
		}

		/// <summary>Splits a continuous zone coordinate back into a parasang and an in-parasang
		/// zone. Negative input is refused rather than floored, because the world has no ground
		/// there and a wrong-signed remainder would name a zone that exists.</summary>
		public static bool TrySplitGlobal(int Global, out int Parasang, out int Zone)
		{
			Parasang = 0;
			Zone = 0;
			if (Global < 0)
			{
				return false;
			}
			Parasang = Global / ZonesPerParasang;
			Zone = Global % ZonesPerParasang;
			return true;
		}

		/// <summary>Picks one of the frontier zones a scout could walk to, deterministically.</summary>
		/// <param name="SettlementId">The settlement's kernel id.</param>
		/// <param name="PostedTick">The notice's posted tick.</param>
		/// <param name="PassIndex">The pass being resolved, so a scout sent later reports
		/// different ground.</param>
		/// <param name="Count">Candidates the caller found. Zero or less yields false.</param>
		/// <param name="Index">Index in <c>[0, Count)</c>.</param>
		/// <returns>False only when there was nothing to pick from; a refusing kernel falls back
		/// to index zero, which is a real candidate rather than a sentinel.</returns>
		public static bool TryPickFrontier(string SettlementId, long PostedTick, int PassIndex, int Count, out int Index)
		{
			Index = 0;
			if (Count <= 0)
			{
				return false;
			}
			int pass = (PassIndex > 0) ? PassIndex : 0;
			if (pass > MaxPasses)
			{
				pass = MaxPasses;
			}
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(BountyRulesVersion, SettlementId, FrontierEventStreamId, NoticeEventKind, (ulong)((PostedTick > 0L) ? PostedTick : 0L), out key, out fault))
			{
				return true;
			}
			ulong value;
			if (CounterRandom.TryDrawBelow(BountySeed, key, (uint)pass, (ulong)Count, out value, out fault))
			{
				Index = (int)value;
			}
			return true;
		}

	}
}
