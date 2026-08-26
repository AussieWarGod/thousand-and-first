using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomCommission
	{
		/// <summary>
		/// Build ticks after a craft district's discount. Floors at one tick and falls back to
		/// the undiscounted time on a hostile or missing percent rather than ever completing a
		/// build instantly or in the past.
		/// </summary>
		public static long CraftBuildTicks(long BaseTicks, IEnumerable<string> Districts)
		{
			int percent = KingdomRules.DistrictsBuildPercent(Districts);
			if (percent <= 0)
			{
				percent = 100;
			}
			long ticks = BaseTicks * percent / 100;
			return (ticks < 1) ? 1 : ticks;
		}

		/// <summary>
		/// Where a commissioned work is raised, by what it is for, read against everything the
		/// settlement already has standing here.
		/// <para>
		/// The settlement's own plan (<c>KingdomLayout</c>) is asked first: casks gather by the
		/// water, houses gather by the houses and stand back from the wall, the civic ground
		/// thickens where the settlement already lives, fields lie out past the last roof, and a
		/// wall extends the wall. It grows with the city because every one of those answers is a
		/// function of what is already built, so the same commission lands somewhere different
		/// in a camp and in a town.
		/// </para>
		/// <para>
		/// The plan is allowed to have nothing to say &mdash; on empty ground it always does,
		/// because there are no neighbours to reason from &mdash; and it never fights the
		/// founder over ground it does not care about. Either way the fallback below is the
		/// placement this mod always had.
		/// </para>
		/// <para>
		/// Failure here degrades to that fallback rather than escaping into the engine: a
		/// commission the plan cannot site is still a commission.
		/// </para>
		/// </summary>
		/// <param name="Z">Zone to build in.</param>
		/// <param name="System">The realm, for its claim.</param>
		/// <param name="Entry">The design being raised.</param>
		/// <param name="Outcome">What the plan did, for the message the founder reads. Reports
		/// <c>Defer</c> whenever the fallback placed the work, whatever the reason.</param>
		public static Cell FindBuildCell(Zone Z, KingdomSystem System, KingdomRules.BuildEntry Entry, out KingdomLayoutRules.LayoutOutcome Outcome)
		{
			return FindBuildCell(Z, System, Entry, null, out Outcome);
		}

		internal static Cell FindBuildCell(Zone Z, KingdomSystem System,
			KingdomRules.BuildEntry Entry, string PlacementOwnerId,
			out KingdomLayoutRules.LayoutOutcome Outcome)
		{
			KingdomLayoutRules.LayoutOutcome planned = KingdomLayoutRules.LayoutOutcome.Defer;
			Cell cell = null;
			// Asked before the plan, because the gatehouse is the one design whose ground is a
			// rule rather than a preference: it belongs where the wall meets the road, and the
			// plan has no way to know that. Everything else, the plan answers for.
			KingdomSystem.Guard("gatehouse siting", delegate
			{
				cell = FindGateCell(Z, System, Entry);
			});
			if (cell != null)
			{
				// Grammar, not Founder: the settlement's own shape chose this ground, and the
				// founder's standing on it had nothing to do with it. The clause the founder
				// reads for a defensive work chosen by the plan is "on the line", which is
				// exactly where a gatehouse went.
				Outcome = KingdomLayoutRules.LayoutOutcome.Grammar;
				return cell;
			}
			// A gatehouse with no valid road/frontier footprint is a refusal, never an excuse to
			// drop the network piece onto an ordinary defensive or founder-adjacent cell.
			if (Entry != null && KingdomGatehouseRules.IsGatehouse(Entry.Key))
			{
				Outcome = KingdomLayoutRules.LayoutOutcome.Defer;
				return null;
			}
			KingdomSystem.Guard("settlement layout", delegate
			{
				cell = KingdomLayout.ChooseCell(Z, System, Entry, out planned);
			});
			if (cell != null)
			{
				Outcome = planned;
				return cell;
			}
			Outcome = KingdomLayoutRules.LayoutOutcome.Defer;
			return FindBuildCell(Z, System, Entry != null
				&& KingdomRules.IsFrontierWork(Entry.Defence,
					KingdomPlots.IsPlotDesign(Entry.Key)),
				PlacementOwnerId);
		}

		/// <summary>
		/// Where a commissioned work is raised when the settlement's plan has no opinion.
		/// <para>
		/// Defensive works go on the frontier: the edges of this zone that face ground the realm
		/// does not hold. That is what makes a wall a wall rather than a post in a field, and it
		/// is why walls must be sited against the WHOLE claim rather than one zone - a camp
		/// becomes a city across several zones, and the edge that needed a wall yesterday is
		/// interior once the neighbour is claimed. Nothing is moved or torn down when that
		/// happens; the old line simply becomes an inner wall.
		/// </para>
		/// <para>
		/// Everything else is raised where the founder is standing, which is the closest thing to
		/// intent the mod can read without a placement UI.
		/// </para>
		/// </summary>
		/// <param name="Z">Zone to build in.</param>
		/// <param name="System">The realm, for its claim. Null falls back to founder-adjacent.</param>
		/// <param name="Defensive">True to site this on the frontier.</param>
		public static Cell FindBuildCell(Zone Z, KingdomSystem System, bool Defensive)
		{
			return FindBuildCell(Z, System, Defensive, null);
		}

		private static Cell FindBuildCell(Zone Z, KingdomSystem System, bool Defensive,
			string PlacementOwnerId)
		{
			if (Z == null)
			{
				return null;
			}
			int start = 0;
			if (PlacementOwnerId != null
				&& !TryPlacementProbe(System, Z, PlacementOwnerId, out start)) return null;
			if (Defensive && System != null)
			{
				KingdomRules.Frontier edges = KingdomRules.FrontierEdges(Z.ZoneID, System.ClaimedZones);
				if (edges != KingdomRules.Frontier.None)
				{
					Cell frontier = ProbeBuildCell(Z, start, edges, true, false);
					if (frontier != null) return frontier;
				}
			}
			Cell founder = ProbeBuildCell(Z, start, KingdomRules.Frontier.None,
				false, true);
			return founder ?? ProbeBuildCell(Z, start, KingdomRules.Frontier.None,
				false, false);
		}

		/// <summary>
		/// The gatehouse's own ground: the exact frontier way out
		/// (<c>KingdomRoadRules.TryGate</c>), which is the cell the settlement's own
		/// <c>HeartToGate</c> route is already walked to. Its full frozen 3x3 topology must pass
		/// the obstruction and passage audit; it never moves sideways to evade a blocker. The brief's ruling &mdash;
		/// "a placement rule, not a size: on the frontier wall, astride a road".
		/// <para>
		/// Null for every other design, for a zone with no frontier left to have a way out of,
		/// and for a settlement with no heart yet to aim from. Every one of those falls through
		/// to the ordinary plan, which is what sited a gatehouse before this existed.
		/// </para>
		/// </summary>
		public static Cell FindGateCell(Zone Z, KingdomSystem System, KingdomRules.BuildEntry Entry)
		{
			if (Z == null || System == null || Entry == null || !KingdomRoadRules.SitesAtGate(Entry.Key))
			{
				return null;
			}
			if (!KingdomGatehouse.TryPlan(Z, System, out KingdomGatehousePlan plan,
				out string failure))
			{
				KingdomLog.Log("gatehouse: " + failure);
				return null;
			}
			KingdomLog.Log("gatehouse: exact road root=" + plan.GateX + "," + plan.GateY
				+ " footprint=" + plan.X1 + "," + plan.Y1 + ".." + plan.X2 + "," + plan.Y2
				+ " facing=" + plan.Orientation);
			return Z.GetCell(plan.GateX, plan.GateY);
		}

		public static Cell FindBuildCell(Zone Z)
		{
			if (Z == null) return null;
			Cell founder = ProbeBuildCell(Z, 0, KingdomRules.Frontier.None, false, true);
			return founder ?? ProbeBuildCell(Z, 0, KingdomRules.Frontier.None, false, false);
		}

		private static bool TryPlacementProbe(KingdomSystem System, Zone Z,
			string PlacementOwnerId, out int Start)
		{
			Start = -1;
			string streamId;
			SemanticEventKey key;
			KernelFaultCode kernelFault;
			KingdomSemanticSelectionFault semanticFault;
			if (System == null || string.IsNullOrEmpty(System.CurrentSettlementId)
				|| !KingdomSemanticSelectionRules.TryOwnerStreamId("commission-placement",
					PlacementOwnerId, out streamId)
				|| !SemanticEventKey.TryCreate(KingdomSemanticSelectionRules.RulesVersion,
					System.CurrentSettlementId, streamId, PlacementEventKind, 1UL,
					out key, out kernelFault)
				|| !KingdomSemanticSelectionRules.TryProbeStart(System.SimulationSeed, key,
					PlacementDraw, Z.Width, Z.Height, out Start, out semanticFault))
			{
				KingdomLog.Log("commission placement: deterministic probe identity refused");
				return false;
			}
			return true;
		}

		private static Cell ProbeBuildCell(Zone Z, int Start, KingdomRules.Frontier Edges,
			bool FrontierOnly, bool FounderAdjacentOnly)
		{
			if (Z == null || Z.Width <= 0 || Z.Height <= 0) return null;
			Cell player = The.Player?.CurrentCell;
			if (FounderAdjacentOnly && (player == null || player.ParentZone != Z)) return null;
			int count = Z.Width * Z.Height;
			for (int offset = 0; offset < count; offset++)
			{
				int at = KingdomSemanticSelectionRules.ProbeIndex(Start, offset, count);
				Cell cell = Z.GetCell(at % Z.Width, at / Z.Width);
				if (cell == null || !cell.IsEmpty() || !cell.IsPassable()
					|| cell.HasObjectWithPart("LiquidVolume")) continue;
				if (FrontierOnly && !KingdomRules.IsOnFrontier(cell.X, cell.Y,
					Z.Width, Z.Height, Edges)) continue;
				if (FounderAdjacentOnly && KingdomLayoutRules.Chebyshev(cell.X, cell.Y,
					player.X, player.Y) != 1) continue;
				return cell;
			}
			return null;
		}
	}
}
