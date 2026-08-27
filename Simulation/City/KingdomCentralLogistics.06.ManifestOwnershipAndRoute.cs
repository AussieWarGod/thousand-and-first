using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		private static List<KingdomJobRow> OwnerRows(KingdomJobTable table, string owner)
		{
			List<KingdomJobRow> rows = new List<KingdomJobRow>();
			if (table == null) return rows;
			for (int i = 0; i < table.Count; i++)
			{
				KingdomJobRow row;
				if (table.TryAt(i, out row)
					&& row.DeliveryCargoAuthority
						== KingdomDeliveryCargoAuthority.CarryBookManifest
					&& string.Equals(row.DeliveryOwnerOperationId, owner,
						StringComparison.Ordinal)) rows.Add(row);
			}
			rows.Sort(delegate(KingdomJobRow a, KingdomJobRow b)
			{
				return a.JobId.CompareTo(b.JobId);
			});
			return rows;
		}

		private static void QuarantineOwner(KingdomSystem system, KingdomJobTable table,
			string owner)
		{
			List<KingdomJobRow> rows = OwnerRows(table, owner);
			if (rows.Count == 0) return;
			KingdomJobRow[] held = new KingdomJobRow[rows.Count];
			for (int i = 0; i < rows.Count; i++)
				held[i] = rows[i].WithDeliveryPhase(KingdomDeliveryPhase.Quarantined);
			KingdomJobTable next;
			KingdomCityFault ignored;
			if (table.TryRewrite(held, held.Length, out next, out ignored))
				system.Jobs.TryPublish(next, out ignored);
		}

		private static bool TryBuildManifestRoute(KingdomSystem system, Zone liveSource,
			string owner, string sourceObjectId, string sourceZoneId, int sourceX, int sourceY,
			string targetObjectId, string targetZoneId, int targetX, int targetY, long start,
			out KingdomLeg[] legs, out int legCount, out long arrival,
			out int sourceEndpointId, out int targetEndpointId, out KingdomCityFault fault)
		{
			legs = null; legCount = 0; arrival = start;
			sourceEndpointId = targetEndpointId = 0;
			KingdomDistanceCache cache = system == null || system.City == null
				? null : system.City.DistanceCache;
			if (cache == null || cache.Matrix == null || liveSource == null)
			{ fault = KingdomCityFault.OutsideItinerary; return false; }
			if (!string.IsNullOrEmpty(sourceObjectId))
			{
				GameObject exact = liveSource.FindObjectByID(sourceObjectId);
				if (!GameObject.Validate(exact) || exact.CurrentCell == null
					|| exact.CurrentCell.X != sourceX || exact.CurrentCell.Y != sourceY)
				{ fault = KingdomCityFault.OutsideItinerary; return false; }
			}
			int targetIndex;
			KingdomDistanceEndpointState target;
			if (!cache.Matrix.Graph.TryIndexOf(targetZoneId, out targetIndex)
				|| !cache.TryEndpointAt(targetIndex, targetObjectId, targetX, targetY, out target))
			{ fault = KingdomCityFault.OutsideItinerary; return false; }
			targetEndpointId = target.EndpointId;
			sourceEndpointId = !string.IsNullOrEmpty(sourceObjectId)
				? KingdomCityRules.StableId(sourceObjectId)
				: KingdomCityRules.StableId("taf:carry:coordinate:" + owner + ":source");
			if (sourceEndpointId <= 0) { fault = KingdomCityFault.InvalidIndex; return false; }

			bool[] passable = new bool[liveSource.Width * liveSource.Height];
			bool[] paved = new bool[passable.Length];
			for (int y = 0; y < liveSource.Height; y++)
			for (int x = 0; x < liveSource.Width; x++)
			{
				int at = y * liveSource.Width + x;
				Cell cell = liveSource.GetCell(x, y);
				passable[at] = KingdomRoads.Walkable(cell);
				paved[at] = KingdomRoads.AppliedState(cell)
					== KingdomRoadRules.WearState.Paved;
			}
			int sourceIndex;
			bool claimedSource = cache.Matrix.Graph.TryIndexOf(sourceZoneId, out sourceIndex);
			int ingress = sourceIndex;
			KingdomZoneStep externalExit = KingdomZoneStep.None;
			KingdomZoneStep ingressEnter = KingdomZoneStep.None;
			int remoteHops = 0;
			if (!claimedSource && !TryExternalIngress(cache.Matrix.Graph, sourceZoneId,
				out ingress, out externalExit, out ingressEnter, out remoteHops))
			{ fault = KingdomCityFault.OutsideItinerary; return false; }
			int[] path = new int[KingdomDistanceRules.MaxNodes];
			int pathCount;
			if (!cache.Matrix.Graph.TryPath(ingress, targetIndex, path, out pathCount, out fault)
				|| pathCount <= 0) return false;
			legCount = pathCount + (claimedSource ? 0 : 1);
			if (legCount > KingdomItineraryRules.MaxLegs)
			{ fault = KingdomCityFault.RowCapExceeded; return false; }
			legs = new KingdomLeg[legCount];
			long depart = start;
			int write = 0;
			if (!claimedSource)
			{
				int local;
				short ex, ey;
				long ignored;
				if (!KingdomDistanceSliceRules.TryMeasurePointToEdge(passable, paved,
					liveSource.Width, liveSource.Height, sourceX, sourceY, externalExit,
					out local, out ex, out ey, out ignored))
				{ fault = KingdomCityFault.OutsideItinerary; return false; }
				long total = local + (long)Math.Max(remoteHops - 1, 0)
					* KingdomDistanceRules.ZoneTransitCells + 1L;
				if (!TryLeg(sourceZoneId, (short)sourceX, (short)sourceY, ex, ey,
					total, ref depart, out legs[write++], out fault)) return false;
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
				short enterX, enterY, exitX, exitY;
				int cells;
				if (claimedSource && i == 0)
				{
					enterX = (short)sourceX; enterY = (short)sourceY;
					long ignored;
					if (pathCount == 1)
					{
						exitX = (short)targetX; exitY = (short)targetY;
						if (!KingdomDistanceSliceRules.TryMeasurePointToPoint(passable, paved,
							liveSource.Width, liveSource.Height, sourceX, sourceY,
							targetX, targetY, out cells, out ignored))
						{ fault = KingdomCityFault.OutsideItinerary; return false; }
					}
					else if (!cache.TryPortal(path[i], leaving, out exitX, out exitY)
						|| !KingdomDistanceSliceRules.TryMeasurePointToPoint(passable, paved,
							liveSource.Width, liveSource.Height, sourceX, sourceY,
							exitX, exitY, out cells, out ignored))
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
			arrival = depart;
			fault = KingdomCityFault.None;
			return write == legCount;
		}
	}
}
