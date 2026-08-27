using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		private static bool TryLeg(string zoneId, short enterX, short enterY, short exitX,
			short exitY, long cells, ref long depart, out KingdomLeg leg,
			out KingdomCityFault fault)
		{
			leg = default(KingdomLeg);
			if (cells < 1L) cells = 1L;
			long duration = cells * KingdomItineraryRules.WalkTicksPerCellDefault;
			if (duration <= 0L || depart > long.MaxValue - duration || cells > int.MaxValue)
			{ fault = KingdomCityFault.ArithmeticOverflow; return false; }
			long arrive = depart + duration;
			leg = new KingdomLeg(zoneId, enterX, enterY, exitX, exitY,
				(int)cells, depart, arrive);
			depart = arrive;
			fault = KingdomCityFault.None;
			return true;
		}

		private static bool TryExternalIngress(KingdomZoneGraph graph, string sourceZoneId,
			out int ingress, out KingdomZoneStep sourceExit, out KingdomZoneStep ingressEnter,
			out int hops)
		{
			ingress = -1; sourceExit = ingressEnter = KingdomZoneStep.None; hops = 0;
			string world;
			int sx, sy, sz;
			if (graph == null || !KingdomRules.TryParseZoneID(sourceZoneId, out world,
				out sx, out sy, out sz)) return false;
			int best = int.MaxValue;
			for (int i = 0; i < graph.Count; i++)
			{
				KingdomZoneNode node;
				string other;
				int gx, gy, z;
				if (!graph.TryNode(i, out node) || !KingdomRules.TryParseZoneID(node.ZoneId,
					out other, out gx, out gy, out z) || other != world || z != sz) continue;
				int distance = Math.Abs(gx - sx) + Math.Abs(gy - sy);
				if (distance <= 0 || distance > best) continue;
				if (distance == best && ingress >= 0)
				{
					KingdomZoneNode held;
					graph.TryNode(ingress, out held);
					if (string.CompareOrdinal(node.ZoneId, held.ZoneId) >= 0) continue;
				}
				best = distance; ingress = i;
			}
			if (ingress < 0) return false;
			KingdomZoneNode target;
			graph.TryNode(ingress, out target);
			int dx = target.GlobalX - sx;
			int dy = target.GlobalY - sy;
			sourceExit = dx > 0 ? KingdomZoneStep.East : (dx < 0 ? KingdomZoneStep.West
				: (dy > 0 ? KingdomZoneStep.South : KingdomZoneStep.North));
			ingressEnter = dy > 0 ? KingdomZoneStep.North : (dy < 0 ? KingdomZoneStep.South
				: (dx > 0 ? KingdomZoneStep.West : KingdomZoneStep.East));
			hops = best;
			return true;
		}

		private static bool TryBuildSegment(KingdomSystem system, int tripId,
			string fromZoneId, int fromEndpointId, string fromObjectId,
			string toZoneId, int toEndpointId, string toObjectId, long start,
			out KingdomLeg[] legs, out int legCount, out long arrive,
			out KingdomCityFault fault)
		{
			legs = null; legCount = 0; arrive = start;
			KingdomDistanceCache cache = system == null || system.City == null
				? null : system.City.DistanceCache;
			if (cache == null || cache.Matrix == null)
			{ fault = KingdomCityFault.OutsideItinerary; return false; }
			int from;
			int to;
			KingdomDistanceEndpointState first;
			KingdomDistanceEndpointState last;
			if (!cache.Matrix.Graph.TryIndexOf(fromZoneId, out from)
				|| !cache.Matrix.Graph.TryIndexOf(toZoneId, out to)
				|| !cache.TryEndpoint(from, fromEndpointId, out first)
				|| !cache.TryEndpoint(to, toEndpointId, out last)
				|| !string.Equals(first.ObjectId, fromObjectId, StringComparison.Ordinal)
				|| !string.Equals(last.ObjectId, toObjectId, StringComparison.Ordinal))
			{ fault = KingdomCityFault.OutsideItinerary; return false; }
			int[] path = new int[KingdomDistanceRules.MaxNodes];
			if (!cache.Matrix.Graph.TryPath(from, to, path, out legCount, out fault)
				|| legCount <= 0 || legCount > KingdomItineraryRules.MaxLegs) return false;
			short[] enterX = new short[legCount];
			short[] enterY = new short[legCount];
			short[] exitX = new short[legCount];
			short[] exitY = new short[legCount];
			enterX[0] = first.X; enterY[0] = first.Y;
			for (int i = 0; i < legCount - 1; i++)
			{
				KingdomZoneStep leaving;
				KingdomZoneStep arriving;
				if (!cache.Matrix.Graph.TryStep(path[i], path[i + 1], out leaving)
					|| !cache.Matrix.Graph.TryStep(path[i + 1], path[i], out arriving)
					|| !cache.TryPortal(path[i], leaving, out exitX[i], out exitY[i])
					|| !cache.TryPortal(path[i + 1], arriving,
						out enterX[i + 1], out enterY[i + 1]))
				{ fault = KingdomCityFault.OutsideItinerary; return false; }
			}
			exitX[legCount - 1] = last.X; exitY[legCount - 1] = last.Y;
			int[] lengths = new int[legCount];
			if (legCount == 1)
			{
				if (!cache.Matrix.TrySameZone(from, fromEndpointId, toEndpointId,
					out lengths[0])) { fault = KingdomCityFault.OutsideItinerary; return false; }
			}
			else
			{
				for (int i = 0; i < legCount; i++)
				{
					KingdomZoneStep leaving = KingdomZoneStep.None;
					KingdomZoneStep arriving = KingdomZoneStep.None;
					if (i + 1 < legCount
						&& !cache.Matrix.Graph.TryStep(path[i], path[i + 1], out leaving))
					{ fault = KingdomCityFault.OutsideItinerary; return false; }
					if (i > 0 && !cache.Matrix.Graph.TryStep(path[i], path[i - 1], out arriving))
					{ fault = KingdomCityFault.OutsideItinerary; return false; }
					if (i == 0)
					{
						if (!cache.Matrix.TryWorkToEdge(path[i], fromEndpointId, leaving,
							out lengths[i]))
						{ fault = KingdomCityFault.OutsideItinerary; return false; }
					}
					else if (i == legCount - 1)
					{
						if (!cache.Matrix.TryWorkToEdge(path[i], toEndpointId, arriving,
							out lengths[i]))
						{ fault = KingdomCityFault.OutsideItinerary; return false; }
					}
					else if (!cache.TryPortalPair(path[i], arriving, leaving, out lengths[i]))
					{ fault = KingdomCityFault.OutsideItinerary; return false; }
					if (i + 1 < legCount) lengths[i]++;
				}
			}
			legs = new KingdomLeg[legCount];
			long depart = start;
			for (int i = 0; i < legCount; i++)
			{
				KingdomZoneNode node;
				if (!cache.Matrix.Graph.TryNode(path[i], out node))
				{ fault = KingdomCityFault.InvalidIndex; return false; }
				long duration = (long)lengths[i] * KingdomItineraryRules.WalkTicksPerCellDefault;
				if (duration < 1L) duration = 1L;
				if (depart > long.MaxValue - duration)
				{ fault = KingdomCityFault.ArithmeticOverflow; return false; }
				arrive = depart + duration;
				legs[i] = new KingdomLeg(node.ZoneId, enterX[i], enterY[i], exitX[i], exitY[i],
					lengths[i], depart, arrive);
				depart = arrive;
			}
			fault = KingdomCityFault.None;
			return true;
		}

		private static bool TryPassage(KingdomSystem system, KingdomZoneGraph graph,
			int fromIndex, int toIndex, int tripId, out short exitX, out short exitY,
			out short enterX, out short enterY, out KingdomZoneStep step,
			out KingdomCityFault fault)
		{
			exitX = exitY = enterX = enterY = 0; step = KingdomZoneStep.None;
			KingdomZoneNode from;
			KingdomZoneNode to;
			if (graph == null || !graph.TryNode(fromIndex, out from) || !graph.TryNode(toIndex, out to)
				|| !graph.TryStep(fromIndex, toIndex, out step))
			{ fault = KingdomCityFault.OutsideItinerary; return false; }
			if (step != KingdomZoneStep.Up && step != KingdomZoneStep.Down)
			{
				if (!KingdomJobRules.TryDrawEntryCell(system.SimulationSeed,
					KingdomChronicle.SettlementId(system), tripId, step,
					KingdomJobRules.ZoneWidth, KingdomJobRules.ZoneHeight,
					out exitX, out exitY, out fault)
					|| !KingdomJobRules.TryMirror(exitX, exitY, step,
						KingdomJobRules.ZoneWidth, KingdomJobRules.ZoneHeight,
						out enterX, out enterY)) return false;
				return true;
			}
			KingdomZoneNode head = from.Stratum < to.Stratum ? from : to;
			KingdomZoneNode foot = from.Stratum < to.Stratum ? to : from;
			KingdomDelveLinkReceipt receipt;
			if (!KingdomDelveLink.TryReadPhysicalReceipt(head.ZoneId, out receipt)
				|| !string.Equals(receipt.FootZoneId, foot.ZoneId, StringComparison.Ordinal)
				|| receipt.X < 0 || receipt.X >= KingdomJobRules.ZoneWidth
				|| receipt.Y < 0 || receipt.Y >= KingdomJobRules.ZoneHeight)
			{ fault = KingdomCityFault.OutsideItinerary; return false; }
			exitX = enterX = (short)receipt.X; exitY = enterY = (short)receipt.Y;
			fault = KingdomCityFault.None;
			return true;
		}
	}
}
