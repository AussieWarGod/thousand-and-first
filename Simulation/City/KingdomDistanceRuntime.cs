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
	internal static class KingdomDistanceRuntime
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

		/// <summary>Plans one exact holder→target carry. Any source row with physical stock but no
		/// rendered holder observation makes the whole question refuse; row order is never used as
		/// a fake distance.</summary>
		internal static bool TryPlan(KingdomCityBook book, KingdomCityState state,
			string destinationZoneId, KingdomStockKind kind, long demand, long room,
			out KingdomDistanceTransferPlan plan, out KingdomCityFault fault)
		{
			plan = default(KingdomDistanceTransferPlan);
			fault = KingdomCityFault.NullArgument;
			KingdomDistanceCache cache = (book == null) ? null : book.DistanceCache;
			if (cache == null || cache.Matrix == null || state == null) return false;
			int destination;
			KingdomDistanceZoneCache destinationCache;
			if (!cache.Matrix.Graph.TryIndexOf(destinationZoneId, out destination)
				|| !cache.TryZone(destination, out destinationCache) || !destinationCache.Observed)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			if (demand <= 0L || room <= 0L)
			{
				fault = KingdomCityFault.None;
				return true;
			}
			KingdomHolderRow[] holders = new KingdomHolderRow[state.ZoneCount];
			int[] distances = new int[state.ZoneCount];
			int[] targets = new int[state.ZoneCount];
			long[] available = new long[state.ZoneCount];
			int count = 0;
			for (int source = 0; source < state.ZoneCount; source++)
			{
				if (source == destination) continue;
				KingdomZoneRow zoneRow;
				KingdomStockPair stock;
				if (!state.TryZone(source, out zoneRow) || !zoneRow.Stocks.TryGet(kind, out stock))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				long physical = stock.Level - Math.Max(zoneRow.OwedOf(kind), 0);
				if (physical <= 0L) continue;
				KingdomDistanceZoneCache sourceCache;
				KingdomZoneStep leaving;
				KingdomZoneStep arriving;
				if (!cache.TryZone(source, out sourceCache) || !sourceCache.Observed
					|| !cache.Matrix.Graph.TryRouteSteps(source, destination, out leaving, out arriving))
				{
					fault = KingdomCityFault.OutsideItinerary;
					return false;
				}
				int holder = Winner(sourceCache, kind, leaving, holder: true);
				int target = Winner(destinationCache, kind, arriving, holder: false);
				if (holder < 0 || target < 0)
				{
					fault = KingdomCityFault.OutsideItinerary;
					return false;
				}
				KingdomDistanceEndpointState sourceEndpoint = sourceCache.Endpoints[holder];
				KingdomDistanceEndpointState targetEndpoint = destinationCache.Endpoints[target];
				int cells;
				if (!cache.TryCompose(source, sourceEndpoint.EndpointId, destination,
					targetEndpoint.EndpointId, out cells, out fault)) return false;
				long amount = sourceEndpoint.Amount(kind);
				if (amount > physical) amount = physical;
				long targetRoom = targetEndpoint.Room(kind);
				if (amount > targetRoom) amount = targetRoom;
				if (amount <= 0L)
				{
					fault = KingdomCityFault.OutsideItinerary;
					return false;
				}
				holders[count] = new KingdomHolderRow(sourceEndpoint.EndpointId, source, -1,
					sourceEndpoint.DedicationOrdinal, kind, amount);
				distances[count] = cells;
				targets[count] = targetEndpoint.EndpointId;
				available[count] = amount;
				count++;
			}
			int chosen;
			if (!KingdomLogisticsRules.TryNearestHolder(holders, count, distances, kind,
				out chosen, out fault))
			{
				if (fault == KingdomCityFault.None) return true;
				return false;
			}
			bool held;
			int offender;
			if (!KingdomLogisticsRules.TryNoNearerHolder(holders, count, distances, kind,
				holders[chosen].HolderId, out held, out offender, out fault) || !held)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			long amountChosen = available[chosen];
			if (amountChosen > demand) amountChosen = demand;
			if (amountChosen > room) amountChosen = room;
			KingdomDistanceEndpointState chosenSource;
			KingdomDistanceEndpointState chosenTarget;
			if (!cache.TryEndpoint(holders[chosen].ZoneIndex, holders[chosen].HolderId,
				out chosenSource) || !cache.TryEndpoint(destination, targets[chosen], out chosenTarget))
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			plan = new KingdomDistanceTransferPlan(holders[chosen].ZoneIndex,
				holders[chosen].HolderId, chosenSource.ObjectId,
				targets[chosen], chosenTarget.ObjectId,
				chosenSource.X, chosenSource.Y, chosenTarget.X, chosenTarget.Y,
				distances[chosen], amountChosen);
			fault = KingdomCityFault.None;
			return true;
		}

		internal static int Land(KingdomSurvey survey, KingdomDistanceTransferPlan plan,
			KingdomStockKind kind, string crop)
		{
			if (survey == null || plan.Amount <= 0L) return 0;
			int offer = (plan.Amount > int.MaxValue) ? int.MaxValue : (int)plan.Amount;
			if (kind == KingdomStockKind.Water)
			{
				for (int i = 0; i < survey.Stores.Count; i++)
				{
					LiquidVolume store = survey.Stores[i];
					GameObject owner = (store == null) ? null : store.ParentObject;
					if (GameObject.Validate(owner) && KingdomCityRules.StableId(owner.ID) == plan.TargetId)
						return survey.StoreIn(store, offer);
				}
			}
			else if (kind == KingdomStockKind.Food)
			{
				for (int i = 0; i < survey.Larders.Count; i++)
				{
					GameObject larder = survey.Larders[i];
					if (GameObject.Validate(larder) && KingdomCityRules.StableId(larder.ID) == plan.TargetId)
						return survey.StoreFoodIn(larder, offer, crop);
				}
			}
			return 0;
		}

		internal static bool Commit(KingdomCityBook book, string destinationZoneId,
			KingdomDistanceTransferPlan plan, KingdomStockKind kind, long amount)
		{
			KingdomDistanceCache cache = (book == null) ? null : book.DistanceCache;
			int destination;
			return cache != null && cache.Matrix != null && amount >= 0L
				&& cache.Matrix.Graph.TryIndexOf(destinationZoneId, out destination)
				&& cache.TrySpend(plan.SourceZoneIndex, plan.HolderId, kind, amount)
				&& cache.TryFill(destination, plan.TargetId, kind, amount);
		}

		/// <summary>Rehydrates one persisted Planned row into the exact immutable request consumed
		/// by the central batch planner. Every endpoint and every zone on the route must have a
		/// trusted post-load observation; cold load waits rather than substituting a row proxy.</summary>
		internal static bool TryFreezeRequest(KingdomCityBook book, KingdomJobRow row,
			out KingdomLogisticsRequest request, out KingdomCityFault fault)
		{
			request = default(KingdomLogisticsRequest);
			KingdomDistanceCache cache = book == null ? null : book.DistanceCache;
			if (cache == null || cache.Matrix == null)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			int source;
			int target;
			KingdomDistanceEndpointState sourceEndpoint;
			KingdomDistanceEndpointState targetEndpoint;
			if (!cache.Matrix.Graph.TryIndexOf(row.SourceZoneId, out source)
				|| !cache.Matrix.Graph.TryIndexOf(row.DestZoneId, out target)
				|| !cache.TryEndpoint(source, row.DeliverySourceEndpointId, out sourceEndpoint)
				|| !cache.TryEndpoint(target, row.DeliveryTargetEndpointId, out targetEndpoint)
				|| (!string.IsNullOrEmpty(row.DeliverySourceObjectId)
					&& !string.Equals(sourceEndpoint.ObjectId, row.DeliverySourceObjectId,
						StringComparison.Ordinal))
				|| (!string.IsNullOrEmpty(row.DeliveryTargetObjectId)
					&& !string.Equals(targetEndpoint.ObjectId, row.DeliveryTargetObjectId,
						StringComparison.Ordinal))
				|| sourceEndpoint.X != row.DeliverySourceX
				|| sourceEndpoint.Y != row.DeliverySourceY
				|| targetEndpoint.X != row.DeliveryTargetX
				|| targetEndpoint.Y != row.DeliveryTargetY)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			int[] route = new int[KingdomDistanceRules.MaxNodes];
			int routeCount;
			if (!cache.Matrix.Graph.TryPath(source, target, route, out routeCount, out fault)
				|| routeCount < 2) return false;
			for (int i = 0; i < routeCount; i++)
			{
				KingdomDistanceZoneCache observed;
				if (!cache.TryZone(route[i], out observed) || !observed.Observed)
				{
					fault = KingdomCityFault.OutsideItinerary;
					return false;
				}
			}
			int cells;
			if (!cache.TryCompose(source, row.DeliverySourceEndpointId, target,
				row.DeliveryTargetEndpointId, out cells, out fault)) return false;
			int load = row.DeliveryCargoAuthority == KingdomDeliveryCargoAuthority.CarryBookManifest
				? row.DeliveryManifestSourceCount : row.CargoAmount;
			request = new KingdomLogisticsRequest(row.JobId, row.DeliverySourceEndpointId,
				source, row.DeliveryTargetEndpointId, target, row.Cargo, load, cells,
				route, routeCount, row.DeliveryCargoAuthority, row.DeliveryOwnerOperationId);
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>Exact target-to-target metric for one frozen planner snapshot.</summary>
		internal static bool TryTargetMetric(KingdomCityBook book,
			KingdomLogisticsRequest[] requests, int count, out int[] between,
			out KingdomCityFault fault)
		{
			between = null;
			KingdomDistanceCache cache = book == null ? null : book.DistanceCache;
			if (cache == null || cache.Matrix == null || requests == null
				|| count < 0 || count > requests.Length)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			between = new int[count * count];
			for (int i = 0; i < count; i++)
			for (int j = 0; j < count; j++)
			{
				if (i == j)
				{
					between[i * count + j] = 0;
					continue;
				}
				int cells;
				if (!cache.TryCompose(requests[i].DestinationZoneIndex,
					requests[i].DestinationEndpointId, requests[j].DestinationZoneIndex,
					requests[j].DestinationEndpointId, out cells, out fault)) return false;
				between[i * count + j] = cells;
			}
			fault = KingdomCityFault.None;
			return true;
		}

		private static bool TryCache(KingdomSystem system, KingdomCityState state,
			out KingdomDistanceCache cache, out KingdomCityFault fault)
		{
			cache = null;
			string[] shafts = KingdomDelve.DelvedZones(system.ClaimedZones).ToArray();
			KingdomZoneGraph graph;
			if (!KingdomCityRules.TryZoneGraph(state, shafts, out graph, out fault)) return false;
			ulong a = OffsetA;
			ulong b = OffsetB;
			for (int i = 0; i < graph.Count; i++)
			{
				KingdomZoneNode node;
				if (!graph.TryNode(i, out node)) { fault = KingdomCityFault.InvalidIndex; return false; }
				Mix(ref a, ref b, node.ZoneId);
				Mix(ref a, ref b, node.GlobalX);
				Mix(ref a, ref b, node.GlobalY);
				Mix(ref a, ref b, node.Stratum);
				Mix(ref a, ref b, node.Shaft ? 1 : 0);
			}
			cache = system.City.DistanceCache;
			if (cache != null && cache.Matrix != null && cache.GraphA == a && cache.GraphB == b)
			{
				fault = KingdomCityFault.None;
				return true;
			}
			KingdomDistanceMatrix matrix;
			if (!KingdomDistanceMatrix.TryCreate(graph, out matrix, out fault)) return false;
			cache = new KingdomDistanceCache
			{
				GraphA = a, GraphB = b, Matrix = matrix,
				Zones = new KingdomDistanceZoneCache[graph.Count]
			};
			for (int i = 0; i < cache.Zones.Length; i++) cache.Zones[i] = new KingdomDistanceZoneCache();
			system.City.DistanceCache = cache;
			fault = KingdomCityFault.None;
			return true;
		}

		private static bool TryCandidates(KingdomSystem system, Zone zone, KingdomSurvey survey,
			out List<Candidate> candidates)
		{
			Dictionary<int, Candidate> indexed = new Dictionary<int, Candidate>();
			for (int i = 0; i < survey.Built.Count; i++)
			{
				Candidate row;
				if (!Merge(indexed, survey.Built[i], out row)) { candidates = null; return false; }
				if (row != null) row.Built = true;
			}
			for (int i = 0; i < survey.Stores.Count; i++)
			{
				LiquidVolume store = survey.Stores[i];
				GameObject owner = (store == null) ? null : store.ParentObject;
				Candidate row;
				if (!Merge(indexed, owner, out row)) { candidates = null; return false; }
				if (row == null) continue;
				row.WaterAmount = KingdomLiquids.HasFreshWater(store) ? store.Volume : 0L;
				row.WaterRoom = (store.MaxVolume >= 0 && store.Volume < store.MaxVolume
					&& KingdomLiquids.CanReceiveFreshWater(store)) ? store.MaxVolume - store.Volume : 0L;
			}
			for (int i = 0; i < survey.Larders.Count; i++)
			{
				GameObject larder = survey.Larders[i];
				Candidate row;
				if (!Merge(indexed, larder, out row)) { candidates = null; return false; }
				if (row == null) continue;
				row.FoodAmount = KingdomSurvey.HeldIn(larder);
				row.FoodRoom = KingdomSurvey.CapacityOf(larder) - row.FoodAmount;
				if (row.FoodRoom < 0L) row.FoodRoom = 0L;
			}
			KingdomJobTable jobs;
			KingdomCityFault fault;
			if (system != null && system.Jobs != null && system.Jobs.TryRead(out jobs, out fault))
			{
				for (int i = 0; i < jobs.Count; i++)
				{
					KingdomJobRow job;
					if (!jobs.TryAt(i, out job) || !KingdomJobRules.IsCentralDelivery(job)) continue;
					if (string.Equals(job.SourceZoneId, zone.ZoneID, StringComparison.Ordinal)
						&& !MergeRequired(indexed, zone, job.DeliverySourceEndpointId,
							job.DeliverySourceObjectId, job.DeliverySourceX,
							job.DeliverySourceY)) { candidates = null; return false; }
					if (string.Equals(job.DestZoneId, zone.ZoneID, StringComparison.Ordinal)
						&& !MergeRequired(indexed, zone, job.DeliveryTargetEndpointId,
							job.DeliveryTargetObjectId, job.DeliveryTargetX,
							job.DeliveryTargetY)) { candidates = null; return false; }
				}
			}
			candidates = new List<Candidate>(indexed.Values);
			candidates.Sort(delegate(Candidate left, Candidate right)
			{
				int byId = left.Id.CompareTo(right.Id);
				return byId != 0 ? byId : string.CompareOrdinal(left.ObjectId, right.ObjectId);
			});
			return candidates.Count <= KingdomDistanceSliceRules.MaxCandidateEndpoints;
		}

		private static bool MergeRequired(Dictionary<int, Candidate> indexed, Zone zone, int id,
			string objectId, int x, int y)
		{
			if (id <= 0 || x < 0 || y < 0 || zone == null || x >= zone.Width || y >= zone.Height)
				return false;
			if (!string.IsNullOrEmpty(objectId))
			{
				GameObject exact = zone.FindObjectByID(objectId);
				if (!GameObject.Validate(exact) || exact.CurrentCell == null
					|| exact.CurrentCell.X != x || exact.CurrentCell.Y != y) return false;
			}
			Candidate row;
			if (indexed.TryGetValue(id, out row))
			{
				// Blank object authority means an exact coordinate spill, not an object claim.
				// It may reuse a measured civic endpoint at that same cell without pretending
				// the endpoint object will receive the cargo.
				if ((!string.IsNullOrEmpty(objectId)
						&& !string.Equals(row.ObjectId ?? "", objectId,
							StringComparison.Ordinal))
					|| row.X != x || row.Y != y) return false;
				row.Required = true;
				return true;
			}
			indexed.Add(id, new Candidate
			{
				Id = id, ObjectId = objectId ?? "", X = (short)x, Y = (short)y,
				Ordinal = 0, Built = true, Required = true,
				WaterAmount = -1L, FoodAmount = -1L, WaterRoom = -1L, FoodRoom = -1L
			});
			return true;
		}

		private static bool Merge(Dictionary<int, Candidate> indexed, GameObject obj, out Candidate row)
		{
			row = null;
			if (!GameObject.Validate(obj) || obj.CurrentCell == null) return true;
			string objectId = obj.ID;
			int id = KingdomCityRules.StableId(objectId);
			if (id <= 0) return true;
			if (indexed.TryGetValue(id, out row))
				return string.Equals(row.ObjectId, objectId, StringComparison.Ordinal);
			row = new Candidate
			{
				Id = id, ObjectId = objectId, X = (short)obj.CurrentCell.X,
				Y = (short)obj.CurrentCell.Y,
				Ordinal = KingdomCityRules.DrainOrdinal(obj.GetIntProperty(KingdomCity.DedicationOrderProperty)),
				WaterAmount = -1L, FoodAmount = -1L, WaterRoom = -1L, FoodRoom = -1L
			};
			indexed.Add(id, row);
			return true;
		}

		private static void Winner(List<Candidate> rows, ushort[] edges, KingdomZoneStep edge,
			KingdomStockKind kind, bool holder, byte[] masks)
		{
			int best = -1;
			int bestCells = KingdomDistanceRules.NoRoute;
			for (int i = 0; i < rows.Count; i++)
			{
				Candidate row = rows[i];
				long value = holder
					? ((kind == KingdomStockKind.Water) ? row.WaterAmount : row.FoodAmount)
					: ((kind == KingdomStockKind.Water) ? row.WaterRoom : row.FoodRoom);
				int cells = edges[i * KingdomDistanceRules.EdgesPerZone + (int)edge];
				if (value <= 0L || cells >= KingdomDistanceRules.NoRoute) continue;
				if (best < 0 || cells < bestCells || (cells == bestCells
					&& (row.Id < rows[best].Id || (row.Id == rows[best].Id
					&& row.Ordinal < rows[best].Ordinal))))
				{
					best = i;
					bestCells = cells;
				}
			}
			if (best >= 0) masks[best] |= (byte)(1 << (int)edge);
		}

		private static int Select(bool[] selected, byte[] first, byte[] second, int cap, int count)
		{
			for (int i = 0; i < selected.Length && count < cap; i++)
				if (!selected[i] && (first[i] != 0 || second[i] != 0)) { selected[i] = true; count++; }
			return count;
		}

		private static int Winner(KingdomDistanceZoneCache zone, KingdomStockKind kind,
			KingdomZoneStep edge, bool holder)
		{
			for (int i = 0; i < zone.Endpoints.Length; i++)
			{
				KingdomDistanceEndpointState row = zone.Endpoints[i];
				if (holder ? (row.Amount(kind) > 0L && row.WinsHolder(kind, edge))
					: (row.Room(kind) > 0L && row.WinsTarget(kind, edge))) return i;
			}
			return -1;
		}

		private static void Refresh(KingdomDistanceZoneCache cache, List<Candidate> candidates)
		{
			int at = 0;
			for (int i = 0; i < cache.Endpoints.Length; i++)
			{
				while (at < candidates.Count && candidates[at].Id < cache.Endpoints[i].EndpointId) at++;
				if (at >= candidates.Count || candidates[at].Id != cache.Endpoints[i].EndpointId) continue;
				Candidate source = candidates[at];
				KingdomDistanceEndpointState row = cache.Endpoints[i];
				row.WaterAmount = Math.Max(source.WaterAmount, 0L);
				row.FoodAmount = Math.Max(source.FoodAmount, 0L);
				row.WaterRoom = Math.Max(source.WaterRoom, 0L);
				row.FoodRoom = Math.Max(source.FoodRoom, 0L);
				cache.Endpoints[i] = row;
			}
		}

		private static void Shafts(KingdomZoneGraph graph, int zoneIndex,
			out int upX, out int upY, out int downX, out int downY)
		{
			upX = upY = downX = downY = -1;
			KingdomZoneNode here;
			if (!graph.TryNode(zoneIndex, out here)) return;
			for (int i = 0; i < graph.Count; i++)
			{
				if (i == zoneIndex) continue;
				KingdomZoneNode other;
				KingdomZoneStep step;
				if (!graph.TryNode(i, out other) || !graph.TryStep(zoneIndex, i, out step)
					|| (step != KingdomZoneStep.Up && step != KingdomZoneStep.Down)) continue;
				KingdomZoneNode head = (here.Stratum < other.Stratum) ? here : other;
				KingdomZoneNode foot = (here.Stratum < other.Stratum) ? other : here;
				KingdomDelveLinkReceipt receipt;
				if (!KingdomDelveLink.TryReadPhysicalReceipt(head.ZoneId, out receipt)
					|| receipt.FootZoneId != foot.ZoneId) continue;
				if (step == KingdomZoneStep.Up) { upX = receipt.X; upY = receipt.Y; }
				else { downX = receipt.X; downY = receipt.Y; }
			}
		}

		private static void WriteBoundaries(KingdomDistanceZoneCache zone, bool[] passable,
			bool[] paved, int width, int height)
		{
			Array.Clear(zone.BoundaryPassable, 0, zone.BoundaryPassable.Length);
			Array.Clear(zone.BoundaryPaved, 0, zone.BoundaryPaved.Length);
			for (int x = 0; x < width; x++)
			{
				BoundaryBit(zone, KingdomZoneStep.North, x, passable[x], paved[x]);
				int south = (height - 1) * width + x;
				BoundaryBit(zone, KingdomZoneStep.South, x, passable[south], paved[south]);
			}
			for (int y = 0; y < height; y++)
			{
				int west = y * width;
				int east = west + width - 1;
				BoundaryBit(zone, KingdomZoneStep.West, y, passable[west], paved[west]);
				BoundaryBit(zone, KingdomZoneStep.East, y, passable[east], paved[east]);
			}
		}

		private static void BoundaryBit(KingdomDistanceZoneCache zone, KingdomZoneStep edge,
			int offset, bool passable, bool paved)
		{
			if (offset < 0 || offset >= 128) return;
			int at = (int)edge * 2 + offset / 64;
			ulong bit = 1UL << (offset % 64);
			if (passable) zone.BoundaryPassable[at] |= bit;
			if (paved) zone.BoundaryPaved[at] |= bit;
		}

		private static void ReconcilePortals(KingdomDistanceCache cache)
		{
			if (cache == null || cache.Matrix == null) return;
			for (int a = 0; a < cache.Matrix.ZoneCount; a++)
			for (int b = a + 1; b < cache.Matrix.ZoneCount; b++)
			{
				KingdomZoneStep ab;
				KingdomZoneStep ba;
				KingdomDistanceZoneCache left;
				KingdomDistanceZoneCache right;
				if (!cache.Matrix.Graph.TryStep(a, b, out ab)
					|| !cache.Matrix.Graph.TryStep(b, a, out ba)
					|| (ab == KingdomZoneStep.Up || ab == KingdomZoneStep.Down)
					|| !cache.TryZone(a, out left) || !cache.TryZone(b, out right)
					|| !left.BoundaryObserved || !right.BoundaryObserved) continue;
				int limit = (ab == KingdomZoneStep.North || ab == KingdomZoneStep.South)
					? Math.Min(left.Width, right.Width) : Math.Min(left.Height, right.Height);
				int offset = MutualOffset(left, ab, right, ba, limit, paved: true);
				if (offset < 0) offset = MutualOffset(left, ab, right, ba, limit, paved: false);
				int ax = -1, ay = -1, bx = -1, by = -1;
				if (offset >= 0)
				{
					PortalCell(left, ab, offset, out ax, out ay);
					PortalCell(right, ba, offset, out bx, out by);
				}
				SetPortal(cache, a, ab, ax, ay);
				SetPortal(cache, b, ba, bx, by);
			}
		}

		private static void SetDefaultPortal(KingdomDistanceCache cache, int zoneIndex,
			KingdomZoneStep edge)
		{
			KingdomDistanceZoneCache zone;
			if (!cache.TryZone(zoneIndex, out zone)) return;
			int limit = (edge == KingdomZoneStep.North || edge == KingdomZoneStep.South)
				? zone.Width : zone.Height;
			int offset = SingleOffset(zone, edge, limit, paved: true);
			if (offset < 0) offset = SingleOffset(zone, edge, limit, paved: false);
			int x, y;
			PortalCell(zone, edge, offset, out x, out y);
			SetPortal(cache, zoneIndex, edge, x, y);
		}

		private static int SingleOffset(KingdomDistanceZoneCache zone, KingdomZoneStep edge,
			int limit, bool paved)
		{
			if (limit <= 0) return -1;
			ulong[] words = paved ? zone.BoundaryPaved : zone.BoundaryPassable;
			for (int offset = 0; offset < limit; offset++)
			{
				ulong bit = 1UL << (offset % 64);
				if ((words[(int)edge * 2 + offset / 64] & bit) != 0) return offset;
			}
			return -1;
		}

		private static int MutualOffset(KingdomDistanceZoneCache left, KingdomZoneStep leftEdge,
			KingdomDistanceZoneCache right, KingdomZoneStep rightEdge, int limit, bool paved)
		{
			ulong[] l = paved ? left.BoundaryPaved : left.BoundaryPassable;
			ulong[] r = paved ? right.BoundaryPaved : right.BoundaryPassable;
			for (int offset = 0; offset < limit; offset++)
			{
				int word = offset / 64;
				ulong bit = 1UL << (offset % 64);
				if ((l[(int)leftEdge * 2 + word] & bit) != 0
					&& (r[(int)rightEdge * 2 + word] & bit) != 0) return offset;
			}
			return -1;
		}

		private static void PortalCell(KingdomDistanceZoneCache zone, KingdomZoneStep edge,
			int offset, out int x, out int y)
		{
			x = y = -1;
			if (offset < 0) return;
			if (edge == KingdomZoneStep.North) { x = offset; y = 0; }
			else if (edge == KingdomZoneStep.South) { x = offset; y = zone.Height - 1; }
			else if (edge == KingdomZoneStep.West) { x = 0; y = offset; }
			else if (edge == KingdomZoneStep.East) { x = zone.Width - 1; y = offset; }
		}

		private static void SetPortal(KingdomDistanceCache cache, int zoneIndex,
			KingdomZoneStep edge, int x, int y)
		{
			KingdomDistanceZoneCache zone;
			int at = (int)edge;
			if (cache == null || at < 0 || at >= KingdomDistanceRules.EdgesPerZone
				|| !cache.TryZone(zoneIndex, out zone)) return;
			short sx = (short)x;
			short sy = (short)y;
			if (zone.PortalX[at] == sx && zone.PortalY[at] == sy) return;
			zone.PortalX[at] = sx;
			zone.PortalY[at] = sy;
			zone.Observed = false;
			cache.Matrix.MarkDirty(zone.ZoneId);
		}

		private static void Mix(ref ulong a, ref ulong b, string value)
		{
			if (value == null) { Mix(ref a, ref b, -1); return; }
			Mix(ref a, ref b, value.Length);
			for (int i = 0; i < value.Length; i++) Mix(ref a, ref b, value[i]);
		}

		private static void Mix(ref ulong a, ref ulong b, int value)
		{
			unchecked
			{
				a = (a ^ (uint)value) * PrimeA;
				b = (b ^ ((uint)value + 0x9E3779B9u)) * PrimeB;
			}
		}
	}
}
