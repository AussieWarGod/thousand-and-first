using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRoads
	{
		// --- The pass ---------------------------------------------------------------------

		/// <summary>
		/// Walks the settlement's own errands for the days since anyone last walked them, and
		/// lets the ground show it.
		/// <para>
		/// Called from <c>KingdomGrowth.OnZoneActivated</c> after everything that spends water and
		/// everything that spends hands, because wearing ground spends neither. Nobody is stood
		/// down off a work to make a path; the path is what is left behind by people going to the
		/// work they were already assigned to.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom. Does nothing when unfounded.</param>
		/// <param name="Z">The activated ground. Does nothing when it is not the kingdom's.</param>
		public static void OnSettlementPass(KingdomSystem System, Zone Z)
		{
			if (System == null || !System.Founded || Z == null || The.Game == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			KingdomElapsedOptionDecision option = ObserveOption(System, Z, timeTicks);
			if (!option.Valid) return;
			if (option.Action == KingdomElapsedOptionAction.AnchorDisabled
				|| option.Action == KingdomElapsedOptionAction.AnchorEnabled)
			{
				WriteTick(Z, WalkedProperty, timeTicks);
				CommitOption(System, Z, option.Record);
				return;
			}
			if (option.Action != KingdomElapsedOptionAction.Run) return;
			long walked = ReadTick(Z, WalkedProperty);
			if (walked <= 0L)
			{
				WriteTick(Z, WalkedProperty, timeTicks);
				return;
			}
			int days = KingdomRules.ElapsedDays(timeTicks - walked);
			if (days <= 0)
			{
				return;
			}
			WriteTick(Z, WalkedProperty, KingdomRules.AdvanceCheckpoint(walked, timeTicks));
			// Nobody living here is NOT a stall: an empty settlement has no errands, and an
			// announcement about it would be a complaint about a thing that is not wrong.
			if (System.Population <= 0)
			{
				return;
			}
			List<KingdomPlotRules.PlotRect> plots = KingdomPlots.ReadPlots(Z);
			List<Errand> errands = Errands(System, Z, plots);
			if (errands.Count == 0)
			{
				return;
			}
			List<KingdomRoadRules.WornCell> tally = ReadTally(Z);
			// One read per cell per pass however many errands cross it: 0 unknown, 1 walkable,
			// 2 not. Without this a settlement with eight errands over the same lane pays for
			// that lane eight times.
			byte[] cache = new byte[Z.Width * Z.Height];
			KingdomRoadRules.CellFilter passable = delegate(int x, int y)
			{
				int index = KingdomRoadRules.Pack(x, y, Z.Width);
				if (index < 0 || index >= cache.Length)
				{
					return false;
				}
				if (cache[index] == 0)
				{
					cache[index] = (byte)(Walkable(Z.GetCell(x, y)) ? 1 : 2);
				}
				return cache[index] == 1;
			};
			int start = KingdomRoadRules.RotationStart(timeTicks, errands.Count);
			int taken = (errands.Count < KingdomRoadRules.MaxRoutesPerPass) ? errands.Count : KingdomRoadRules.MaxRoutesPerPass;
			List<int> route = new List<int>();
			bool full = false;
			int laid = 0;
			for (int i = 0; i < taken; i++)
			{
				Errand errand = errands[(start + i) % errands.Count];
				int walkers = KingdomRoadRules.WalkersFor(errand.Kind, System.Population);
				int traffic = KingdomRoadRules.TrafficFor(walkers, days, errand.Kind);
				if (traffic <= 0)
				{
					continue;
				}
				bool traced = errand.ExactRoute == null
					? KingdomRoadRules.TryTrace(passable, Z.Width, Z.Height,
						errand.FromX, errand.FromY, errand.ToX, errand.ToY,
						KingdomRoadRules.MaxRouteCells, KingdomRoadRules.MaxExploreCells, route)
					: KingdomRoadRules.TryExactTrace(passable, Z.Width, Z.Height,
						errand.FromX, errand.FromY, errand.ToX, errand.ToY,
						KingdomRoadRules.MaxRouteCells, errand.ExactRoute, route);
				if (!traced)
				{
					continue;
				}
				for (int c = 0; c < route.Count; c++)
				{
					int x = KingdomRoadRules.UnpackX(route[c], Z.Width);
					int y = KingdomRoadRules.UnpackY(route[c], Z.Width);
					if (!Wearable(Z.GetCell(x, y), plots))
					{
						continue;
					}
					if (!KingdomRoadRules.Accrue(tally, x, y, traffic, out _))
					{
						full = true;
						continue;
					}
					laid++;
				}
			}
			KingdomRoadRules.WearState reached = Apply(Z, tally, plots);
			WriteTally(Z, tally);
			Announce(System, Z, reached, full, tally.Count);
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("roads: days=" + days + " errands=" + errands.Count + " walked=" + taken
					+ " cells=" + laid + " tracked=" + tally.Count + " reached=" + reached);
			}
		}

	}
}
