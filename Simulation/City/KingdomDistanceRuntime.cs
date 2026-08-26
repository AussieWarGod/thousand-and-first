using System;
using System.Collections.Generic;

using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Engine edge for §3.10. Reads one rendered zone into a bounded sparse matrix slice, then
	/// plans carries only from exact observed holders through that matrix. Reckon never calls this.
	/// </summary>
	internal static partial class KingdomDistanceRuntime
	{
		private sealed class Candidate
		{
			internal int Id;
			internal string ObjectId;
			internal short X;
			internal short Y;
			internal int Ordinal;
			internal bool Built;
			internal bool Required;
			internal long WaterAmount;
			internal long FoodAmount;
			internal long WaterRoom;
			internal long FoodRoom;
		}

		private const ulong OffsetA = 1469598103934665603UL;
		private const ulong PrimeA = 1099511628211UL;
		private const ulong OffsetB = 7809847782465536322UL;
		private const ulong PrimeB = 14029467366897019727UL;

		/// <summary>Observe/rebuild one slice. Exact structure and zero-crossing signatures avoid
		/// recomputation on time or ordinary stock amount changes. Placement/removal/passability,
		/// road state, shaft coordinates, or holder eligibility changes rebuild on this render.</summary>
		internal static bool Observe(KingdomSystem system, Zone zone, KingdomSurvey survey,
			KingdomCityState state, out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			if (system == null || system.City == null || zone == null || survey == null || state == null)
				return false;
			KingdomDistanceCache cache;
			if (!TryCache(system, state, out cache, out fault)) return false;
			int zoneIndex;
			if (!cache.Matrix.Graph.TryIndexOf(zone.ZoneID, out zoneIndex))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			List<Candidate> candidates;
			if (!TryCandidates(system, zone, survey, out candidates))
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			bool[] passable = new bool[zone.Width * zone.Height];
			bool[] paved = new bool[passable.Length];
			for (int y = 0; y < zone.Height; y++)
			for (int x = 0; x < zone.Width; x++)
			{
				int at = y * zone.Width + x;
				Cell cell = zone.GetCell(x, y);
				passable[at] = KingdomRoads.Walkable(cell);
				paved[at] = KingdomRoads.AppliedState(cell) == KingdomRoadRules.WearState.Paved;
			}
			int upX;
			int upY;
			int downX;
			int downY;
			Shafts(cache.Matrix.Graph, zoneIndex, out upX, out upY, out downX, out downY);
			KingdomDistanceZoneCache prior = cache.Zones[zoneIndex]
				?? new KingdomDistanceZoneCache();
			prior.ZoneId = zone.ZoneID;
			prior.Width = zone.Width;
			prior.Height = zone.Height;
			WriteBoundaries(prior, passable, paved, zone.Width, zone.Height);
			prior.BoundaryObserved = true;
			SetDefaultPortal(cache, zoneIndex, KingdomZoneStep.North);
			SetDefaultPortal(cache, zoneIndex, KingdomZoneStep.South);
			SetDefaultPortal(cache, zoneIndex, KingdomZoneStep.East);
			SetDefaultPortal(cache, zoneIndex, KingdomZoneStep.West);
			SetPortal(cache, zoneIndex, KingdomZoneStep.Up, upX, upY);
			SetPortal(cache, zoneIndex, KingdomZoneStep.Down, downX, downY);
			cache.Zones[zoneIndex] = prior;
			ReconcilePortals(cache);

			ulong structureA = OffsetA;
			ulong structureB = OffsetB;
			Mix(ref structureA, ref structureB, zone.Width);
			Mix(ref structureA, ref structureB, zone.Height);
			Mix(ref structureA, ref structureB, upX);
			Mix(ref structureA, ref structureB, upY);
			Mix(ref structureA, ref structureB, downX);
			Mix(ref structureA, ref structureB, downY);
			for (int i = 0; i < passable.Length; i++)
				Mix(ref structureA, ref structureB, (passable[i] ? 1 : 0) | (paved[i] ? 2 : 0));
			for (int i = 0; i < candidates.Count; i++)
			{
				Candidate row = candidates[i];
				Mix(ref structureA, ref structureB, row.ObjectId);
				Mix(ref structureA, ref structureB, row.X);
				Mix(ref structureA, ref structureB, row.Y);
				Mix(ref structureA, ref structureB, row.Built ? 1 : 0);
				Mix(ref structureA, ref structureB, row.WaterAmount >= 0L ? 1 : 0);
				Mix(ref structureA, ref structureB, row.FoodAmount >= 0L ? 1 : 0);
			}
			ulong eligibleA = OffsetA;
			ulong eligibleB = OffsetB;
			for (int i = 0; i < candidates.Count; i++)
			{
				Candidate row = candidates[i];
				Mix(ref eligibleA, ref eligibleB, row.Id);
				Mix(ref eligibleA, ref eligibleB, row.WaterAmount > 0L ? 1 : 0);
				Mix(ref eligibleA, ref eligibleB, row.FoodAmount > 0L ? 1 : 0);
				Mix(ref eligibleA, ref eligibleB, row.WaterRoom > 0L ? 1 : 0);
				Mix(ref eligibleA, ref eligibleB, row.FoodRoom > 0L ? 1 : 0);
			}
			if (prior.Observed && !cache.Matrix.IsDirty(zoneIndex)
				&& prior.StructureA == structureA
				&& prior.StructureB == structureB && prior.EligibilityA == eligibleA
				&& prior.EligibilityB == eligibleB)
			{
				Refresh(prior, candidates);
				fault = KingdomCityFault.None;
				return true;
			}

			cache.Matrix.MarkDirty(zone.ZoneID);
			KingdomDistancePoint[] broad = new KingdomDistancePoint[candidates.Count];
			for (int i = 0; i < broad.Length; i++)
				broad[i] = new KingdomDistancePoint(candidates[i].Id, candidates[i].X, candidates[i].Y);
			ushort[] broadEdges;
			long broadOperations;
			ushort[] ignoredPairs;
			ushort[] broadPortalPairs;
			if (!KingdomDistanceSliceRules.TryMeasureExact(passable, paved, zone.Width, zone.Height,
				broad, broad.Length, prior.PortalX, prior.PortalY, includePairs: false,
				out broadEdges, out ignoredPairs, out broadPortalPairs,
				out broadOperations, out fault)) return false;

			byte[] waterHolders = new byte[candidates.Count];
			byte[] foodHolders = new byte[candidates.Count];
			byte[] waterTargets = new byte[candidates.Count];
			byte[] foodTargets = new byte[candidates.Count];
			for (int edge = 0; edge < KingdomDistanceRules.EdgesPerZone; edge++)
			{
				Winner(candidates, broadEdges, (KingdomZoneStep)edge, KingdomStockKind.Water,
					holder: true, waterHolders);
				Winner(candidates, broadEdges, (KingdomZoneStep)edge, KingdomStockKind.Food,
					holder: true, foodHolders);
				Winner(candidates, broadEdges, (KingdomZoneStep)edge, KingdomStockKind.Water,
					holder: false, waterTargets);
				Winner(candidates, broadEdges, (KingdomZoneStep)edge, KingdomStockKind.Food,
					holder: false, foodTargets);
			}
			int share = cache.Matrix.MaxEndpointsForZone(zoneIndex);
			bool[] selected = new bool[candidates.Count];
			int selectedCount = 0;
			for (int i = 0; i < candidates.Count; i++)
				if (candidates[i].Required)
				{
					if (selectedCount >= share) { fault = KingdomCityFault.RowCapExceeded; return false; }
					selected[i] = true; selectedCount++;
				}
			selectedCount = Select(selected, waterHolders, foodHolders, share, selectedCount);
			selectedCount = Select(selected, waterTargets, foodTargets, share, selectedCount);
			for (int i = 0; i < candidates.Count && selectedCount < share; i++)
				if (candidates[i].Built && !selected[i]) { selected[i] = true; selectedCount++; }
			KingdomDistancePoint[] points = new KingdomDistancePoint[selectedCount];
			KingdomDistanceEndpointState[] retained = new KingdomDistanceEndpointState[selectedCount];
			int write = 0;
			for (int i = 0; i < candidates.Count; i++)
			{
				if (!selected[i]) continue;
				Candidate row = candidates[i];
				points[write] = new KingdomDistancePoint(row.Id, row.X, row.Y);
				retained[write] = new KingdomDistanceEndpointState
				{
					EndpointId = row.Id, ObjectId = row.ObjectId, X = row.X, Y = row.Y,
					DedicationOrdinal = row.Ordinal,
					WaterAmount = row.WaterAmount < 0L ? 0L : row.WaterAmount,
					FoodAmount = row.FoodAmount < 0L ? 0L : row.FoodAmount,
					WaterRoom = row.WaterRoom < 0L ? 0L : row.WaterRoom,
					FoodRoom = row.FoodRoom < 0L ? 0L : row.FoodRoom,
					WaterHolderEdges = waterHolders[i], FoodHolderEdges = foodHolders[i],
					WaterTargetEdges = waterTargets[i], FoodTargetEdges = foodTargets[i]
				};
				write++;
			}
			ushort[] edges;
			ushort[] pairs;
			ushort[] portalPairs;
			long operations;
			if (!KingdomDistanceSliceRules.TryMeasureExact(passable, paved, zone.Width, zone.Height,
				points, points.Length, prior.PortalX, prior.PortalY, includePairs: true,
				out edges, out pairs, out portalPairs, out operations, out fault)) return false;
			int[] ids = new int[points.Length];
			for (int i = 0; i < ids.Length; i++) ids[i] = points[i].Id;
			if (!cache.Matrix.TryWriteZone(zoneIndex, ids, edges, pairs, out fault)) return false;
			prior.StructureA = structureA; prior.StructureB = structureB;
			prior.EligibilityA = eligibleA; prior.EligibilityB = eligibleB;
			prior.Observed = true; prior.Endpoints = retained; prior.PortalPairs = portalPairs;
			KingdomLog.Log("distance: measured " + zone.ZoneID + " candidates=" + candidates.Count
				+ " retained=" + retained.Length + " operations=" + (broadOperations + operations));
			fault = KingdomCityFault.None;
			return true;
		}
	}
}
