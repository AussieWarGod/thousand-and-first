using System;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		internal static bool TryPreviewObservedManifestRoute(KingdomSystem system,
			KingdomConstructionInputZoneObservation source, string ownerOperationId,
			string sourceObjectId, string sourceZoneId, int sourceX, int sourceY,
			string targetObjectId, string targetZoneId, int targetX, int targetY, long now,
			out long arrival, out KingdomCityFault fault)
		{
			KingdomLeg[] legs; int legCount, sourceEndpoint, targetEndpoint;
			return TryBuildObservedManifestRoute(system, source, ownerOperationId,
				sourceObjectId, sourceZoneId, sourceX, sourceY, targetObjectId, targetZoneId,
				targetX, targetY, now, out legs, out legCount, out arrival,
				out sourceEndpoint, out targetEndpoint, out fault);
		}

		private static bool TryBuildObservedManifestRoute(KingdomSystem system,
			KingdomConstructionInputZoneObservation source, string owner, string sourceObjectId,
			string sourceZoneId, int sourceX, int sourceY, string targetObjectId,
			string targetZoneId, int targetX, int targetY, long start, out KingdomLeg[] legs,
			out int legCount, out long arrival, out int sourceEndpointId,
			out int targetEndpointId, out KingdomCityFault fault)
		{
			legs = null; legCount = 0; arrival = start;
			sourceEndpointId = targetEndpointId = 0;
			KingdomDistanceCache cache = system?.City?.DistanceCache;
			if (cache == null || cache.Matrix == null
				|| !ObservedSourceMatches(source, sourceObjectId, sourceZoneId, sourceX, sourceY))
			{ fault = KingdomCityFault.OutsideItinerary; return false; }
			byte[] passableBytes = source.CopyPassable(), pavedBytes = source.CopyPaved();
			bool[] passable = new bool[passableBytes.Length];
			bool[] paved = new bool[pavedBytes.Length];
			for (int i = 0; i < passable.Length; i++)
			{ passable[i] = passableBytes[i] == 1; paved[i] = pavedBytes[i] == 1; }
			int targetIndex; KingdomDistanceEndpointState target;
			if (!cache.Matrix.Graph.TryIndexOf(targetZoneId, out targetIndex)
				|| !cache.TryEndpointAt(targetIndex, targetObjectId, targetX, targetY, out target))
			{ fault = KingdomCityFault.OutsideItinerary; return false; }
			targetEndpointId = target.EndpointId;
			sourceEndpointId = !string.IsNullOrEmpty(sourceObjectId)
				? KingdomCityRules.StableId(sourceObjectId)
				: KingdomCityRules.StableId("taf:carry:coordinate:" + owner + ":source");
			if (sourceEndpointId <= 0) { fault = KingdomCityFault.InvalidIndex; return false; }

			int sourceIndex;
			bool claimedSource = cache.Matrix.Graph.TryIndexOf(sourceZoneId, out sourceIndex);
			int ingress = sourceIndex, remoteHops = 0;
			KingdomZoneStep externalExit = KingdomZoneStep.None;
			KingdomZoneStep ingressEnter = KingdomZoneStep.None;
			if (!claimedSource && !TryExternalIngress(cache.Matrix.Graph, sourceZoneId,
				out ingress, out externalExit, out ingressEnter, out remoteHops))
			{ fault = KingdomCityFault.OutsideItinerary; return false; }
			int[] path = new int[KingdomDistanceRules.MaxNodes]; int pathCount;
			if (!cache.Matrix.Graph.TryPath(ingress, targetIndex, path, out pathCount, out fault)
				|| pathCount <= 0) return false;
			legCount = pathCount + (claimedSource ? 0 : 1);
			if (legCount > KingdomItineraryRules.MaxLegs)
			{ fault = KingdomCityFault.RowCapExceeded; return false; }
			legs = new KingdomLeg[legCount]; long depart = start; int write = 0;
			if (!claimedSource)
			{
				int local; short ex, ey; long ignored;
				if (!KingdomDistanceSliceRules.TryMeasurePointToEdge(passable, paved,
					source.Width, source.Height, sourceX, sourceY, externalExit,
					out local, out ex, out ey, out ignored))
				{ fault = KingdomCityFault.OutsideItinerary; return false; }
				long total = local + (long)Math.Max(remoteHops - 1, 0)
					* KingdomDistanceRules.ZoneTransitCells + 1L;
				if (!TryLeg(sourceZoneId, (short)sourceX, (short)sourceY, ex, ey, total,
					ref depart, out legs[write++], out fault)) return false;
			}
			for (int i = 0; i < pathCount; i++)
			{
				KingdomZoneNode node;
				if (!cache.Matrix.Graph.TryNode(path[i], out node))
				{ fault = KingdomCityFault.InvalidIndex; return false; }
				KingdomZoneStep arriving = i == 0 ? ingressEnter : KingdomZoneStep.None;
				KingdomZoneStep leaving = KingdomZoneStep.None;
				if (i > 0 && !cache.Matrix.Graph.TryStep(path[i], path[i - 1], out arriving))
				{ fault = KingdomCityFault.OutsideItinerary; return false; }
				if (i + 1 < pathCount
					&& !cache.Matrix.Graph.TryStep(path[i], path[i + 1], out leaving))
				{ fault = KingdomCityFault.OutsideItinerary; return false; }
				short enterX, enterY, exitX, exitY; int cells;
				if (claimedSource && i == 0)
				{
					enterX = (short)sourceX; enterY = (short)sourceY; long ignored;
					if (pathCount == 1)
					{
						exitX = (short)targetX; exitY = (short)targetY;
						if (!KingdomDistanceSliceRules.TryMeasurePointToPoint(passable, paved,
							source.Width, source.Height, sourceX, sourceY, targetX, targetY,
							out cells, out ignored))
						{ fault = KingdomCityFault.OutsideItinerary; return false; }
					}
					else if (!cache.TryPortal(path[i], leaving, out exitX, out exitY)
						|| !KingdomDistanceSliceRules.TryMeasurePointToPoint(passable, paved,
							source.Width, source.Height, sourceX, sourceY, exitX, exitY,
							out cells, out ignored))
					{ fault = KingdomCityFault.OutsideItinerary; return false; }
				}
				else
				{
					if (!cache.TryPortal(path[i], arriving, out enterX, out enterY))
					{ fault = KingdomCityFault.OutsideItinerary; return false; }
					if (i == pathCount - 1)
					{
						exitX = (short)targetX; exitY = (short)targetY;
						if (!cache.Matrix.TryWorkToEdge(path[i], targetEndpointId,
							arriving, out cells))
						{ fault = KingdomCityFault.OutsideItinerary; return false; }
					}
					else if (!cache.TryPortal(path[i], leaving, out exitX, out exitY)
						|| !cache.TryPortalPair(path[i], arriving, leaving, out cells))
					{ fault = KingdomCityFault.OutsideItinerary; return false; }
				}
				if (i + 1 < pathCount) cells++;
				if (!TryLeg(node.ZoneId, enterX, enterY, exitX, exitY, cells,
					ref depart, out legs[write++], out fault)) return false;
			}
			arrival = depart; fault = KingdomCityFault.None; return write == legCount;
		}

		private static bool ObservedSourceMatches(KingdomConstructionInputZoneObservation source,
			string sourceObjectId, string sourceZoneId, int x, int y)
		{
			if (!KingdomConstructionInputObservationRules.Valid(source)
				|| source.ZoneId != sourceZoneId) return false;
			for (int i = 0; i < source.LineCount; i++)
			{
				KingdomConstructionInputObservationLine line = source.LineAt(i);
				if (line.HolderId == sourceObjectId && line.X == x && line.Y == y) return true;
			}
			return false;
		}
	}
}
