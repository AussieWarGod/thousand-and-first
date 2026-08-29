using System;
using System.Collections.Generic;

using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomDistanceRuntime
	{

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
			KingdomConstructionInputLeaseSnapshot leases;
			string authorityFailure;
			if (!KingdomOrdinaryFoodAuthority.TryCapture(out leases, out authorityFailure))
			{
				candidates = null;
				return false;
			}
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
				int physical = KingdomSurvey.HeldIn(larder);
				row.FoodAmount = KingdomOrdinaryFoodAuthority.AvailableIn(larder, leases);
				row.FoodRoom = KingdomSurvey.CapacityOf(larder) - physical;
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
			string objectId = obj.IDIfAssigned;
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
	}
}
