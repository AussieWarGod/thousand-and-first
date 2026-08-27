using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.AI.GoalHandlers;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomPorters
	{

		/// <summary>Freezes the complete destination-to-source graph path. Every intermediate
		/// claimed zone becomes one dated leg; horizontal transitions use the engine's mirrored
		/// boundary cell and vertical transitions use the canonical paired-shaft coordinate.</summary>
		private static bool TryPlan(KingdomSystem System, Zone Z, int jobId, short destX,
			short destY, long TimeTicks, string sourceZoneId, out short entryX,
			out short entryY, out KingdomZoneStep arrival, out KingdomLeg[] legs,
			out int count, out KingdomCityFault fault)
		{
			entryX = 0;
			entryY = 0;
			arrival = KingdomZoneStep.None;
			legs = null;
			count = 0;
			fault = KingdomCityFault.NullArgument;
			KingdomCityState state;
			KingdomZoneGraph graph;
			if (System == null || System.City == null
				|| !System.City.TryRead(out state, out fault)
				|| !KingdomCityRules.TryZoneGraph(state,
					KingdomDelve.DelvedZones(System.ClaimedZones).ToArray(), out graph, out fault))
			{
				return false;
			}
			int[] path;
			int pathCount;
			if (!KingdomJobRules.TryPorterPath(graph, Z.ZoneID, sourceZoneId,
				out path, out pathCount, out fault))
			{
				return false;
			}
			if (pathCount < 2)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			short nextEnterX;
			short nextEnterY;
			if (!TryPassage(System, graph, path[0], path[1], jobId, Z.Width, Z.Height,
				out entryX, out entryY, out nextEnterX, out nextEnterY, out arrival,
				out fault)) return false;

			List<KingdomLegPlan> plans = new List<KingdomLegPlan>();
			int inboundRoad = RoadDiscount(Z, entryX, entryY, destX, destY);
			int outboundRoad = RoadDiscount(Z, destX, destY, entryX, entryY);
			plans.Add(new KingdomLegPlan(Z.ZoneID, entryX, entryY, destX, destY,
				KingdomItineraryRules.SinuosityBuiltPercent, inboundRoad));
			plans.Add(new KingdomLegPlan(Z.ZoneID, destX, destY, entryX, entryY,
				KingdomItineraryRules.SinuosityBuiltPercent, outboundRoad));
			short sourceX;
			short sourceY;
			SourceAnchor(state, sourceZoneId, out sourceX, out sourceY);
			for (int i = 1; i < pathCount; i++)
			{
				KingdomZoneNode node;
				if (!graph.TryNode(path[i], out node))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				short exitX;
				short exitY;
				short followingX = 0;
				short followingY = 0;
				if (i == pathCount - 1)
				{
					exitX = sourceX;
					exitY = sourceY;
				}
				else
				{
					KingdomZoneStep ignored;
					if (!TryPassage(System, graph, path[i], path[i + 1], jobId,
						KingdomJobRules.ZoneWidth, KingdomJobRules.ZoneHeight,
						out exitX, out exitY, out followingX, out followingY,
						out ignored, out fault)) return false;
				}
				Zone resident = ResidentZone(node.ZoneId, Z);
				plans.Add(new KingdomLegPlan(node.ZoneId, nextEnterX, nextEnterY, exitX, exitY,
					KingdomItineraryRules.SinuosityOpenPercent,
					RoadDiscount(resident, nextEnterX, nextEnterY, exitX, exitY)));
				nextEnterX = followingX;
				nextEnterY = followingY;
			}
			count = plans.Count;
			return KingdomJobRules.TryBuildLegs(plans.ToArray(), count, TimeTicks,
				KingdomItineraryRules.WalkTicksPerCellDefault, out legs, out fault);
		}

		private static bool TryPassage(KingdomSystem System, KingdomZoneGraph Graph,
			int From, int To, int JobId, int Width, int Height, out short ExitX,
			out short ExitY, out short EnterX, out short EnterY,
			out KingdomZoneStep Step, out KingdomCityFault Fault)
		{
			ExitX = ExitY = EnterX = EnterY = 0;
			Step = KingdomZoneStep.None;
			KingdomZoneNode from;
			KingdomZoneNode to;
			if (Graph == null || !Graph.TryNode(From, out from) || !Graph.TryNode(To, out to)
				|| !Graph.TryStep(From, To, out Step))
			{
				Fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			if (Step != KingdomZoneStep.Up && Step != KingdomZoneStep.Down)
			{
				if (!KingdomJobRules.TryDrawEntryCell(System.SimulationSeed, SeedLabel(System),
					JobId, Step, Width, Height, out ExitX, out ExitY, out Fault)
					|| !KingdomJobRules.TryMirror(ExitX, ExitY, Step, Width, Height,
						out EnterX, out EnterY)) return false;
				return true;
			}
			KingdomZoneNode head = (from.Stratum < to.Stratum) ? from : to;
			KingdomZoneNode foot = (from.Stratum < to.Stratum) ? to : from;
			KingdomDelveLinkReceipt receipt;
			if (!KingdomDelveLink.TryReadPhysicalReceipt(head.ZoneId, out receipt)
				|| receipt.FootZoneId != foot.ZoneId || receipt.X < 0 || receipt.Y < 0
				|| receipt.X >= Width || receipt.Y >= Height)
			{
				Fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			ExitX = EnterX = (short)receipt.X;
			ExitY = EnterY = (short)receipt.Y;
			Fault = KingdomCityFault.None;
			return true;
		}

		private static void SourceAnchor(KingdomCityState State, string ZoneId,
			out short X, out short Y)
		{
			X = (short)(KingdomJobRules.ZoneWidth / 2);
			Y = (short)(KingdomJobRules.ZoneHeight / 2);
			int best = int.MaxValue;
			for (int i = 0; State != null && i < State.WorkCount; i++)
			{
				KingdomWorkRow work;
				if (!State.TryWork(i, out work) || work.WorkId >= best
					|| !string.Equals(work.ZoneId, ZoneId, StringComparison.Ordinal)
					|| work.RunState.Kind != KingdomWorkKind.Growing) continue;
				best = work.WorkId;
				X = work.AnchorX;
				Y = work.AnchorY;
			}
		}

		/// <summary>
		/// Whether a road is laid across this ground, and therefore whether the roads discount
		/// applies to its legs (&sect;3.10(3)).
		/// <para>
		/// Read off <c>KingdomRoads</c>'s own per-zone tally rather than re-derived, so laying a
		/// road and shortening an itinerary are the same fact rather than two that can disagree.
		/// The discount is applied identically here and to any later measured length, which is the
		/// clause that keeps a road from making the estimate and the measurement diverge.
		/// </para>
		/// </summary>
		private static int RoadDiscount(Zone Z, int FromX, int FromY, int ToX, int ToY)
		{
			if (!KingdomRoads.Enabled || Z == null)
			{
				return KingdomItineraryRules.NoRoadDiscountPercent;
			}
			List<int> route = new List<int>();
			KingdomRoadRules.CellFilter passable = delegate(int x, int y)
			{
				Cell cell = Z.GetCell(x, y);
				return cell != null && cell.IsPassable();
			};
			if (!KingdomRoadRules.TryTrace(passable, Z.Width, Z.Height, FromX, FromY,
				ToX, ToY, KingdomRoadRules.MaxRouteCells, KingdomRoadRules.MaxExploreCells,
				route) || route.Count == 0) return KingdomItineraryRules.NoRoadDiscountPercent;
			int paved = 0;
			for (int i = 0; i < route.Count; i++)
			{
				Cell cell = Z.GetCell(KingdomRoadRules.UnpackX(route[i], Z.Width),
					KingdomRoadRules.UnpackY(route[i], Z.Width));
				if (KingdomRoads.AppliedState(cell) == KingdomRoadRules.WearState.Paved) paved++;
			}
			if (paved <= 0) return KingdomItineraryRules.NoRoadDiscountPercent;
			long weighted = (long)paved * KingdomItineraryRules.RoadDiscountPercent
				+ (long)(route.Count - paved) * KingdomItineraryRules.NoRoadDiscountPercent;
			return (int)((weighted + route.Count - 1L) / route.Count);
		}

		private static Zone ResidentZone(string ZoneId, Zone Current)
		{
			if (Current != null && string.Equals(Current.ZoneID, ZoneId, StringComparison.Ordinal))
				return Current;
			if (The.ZoneManager == null || string.IsNullOrEmpty(ZoneId)
				|| !The.ZoneManager.CachedZonesContains(ZoneId)) return null;
			return The.ZoneManager.GetZone(ZoneId);
		}
	}
}
