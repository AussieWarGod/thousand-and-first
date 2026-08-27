using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		/// <summary>Runs the production frozen-snapshot planner for Planned rows whose exact source
		/// is on this rendered ground. Scalar trips debit the exact holder after Prepared publishes;
		/// manifest trips stop at Prepared for the pull-based CarryBook adapter.</summary>
		internal static int StartPlanned(KingdomSystem system, Zone zone,
			KingdomSurvey survey, long now)
		{
			KingdomJobTable table;
			KingdomCityFault fault;
			if (system == null || zone == null || survey == null || system.City == null
				|| system.Jobs == null || !system.Jobs.TryRead(out table, out fault)) return 0;
			List<KingdomJobRow> rows = new List<KingdomJobRow>();
			for (int i = 0; i < table.Count && rows.Count < KingdomLogisticsRules.MaxJobsConsidered; i++)
			{
				KingdomJobRow row;
				if (table.TryAt(i, out row) && KingdomJobRules.IsCentralDelivery(row)
					&& row.DeliveryPhase == KingdomDeliveryPhase.Planned
					&& string.Equals(row.SourceZoneId, zone.ZoneID, StringComparison.Ordinal))
					rows.Add(row);
			}
			rows.Sort(delegate(KingdomJobRow a, KingdomJobRow b) { return a.JobId.CompareTo(b.JobId); });
			if (rows.Count == 0) return 0;
			KingdomLogisticsRequest[] requests = new KingdomLogisticsRequest[rows.Count];
			for (int i = 0; i < rows.Count; i++)
				if (!KingdomDistanceRuntime.TryFreezeRequest(system.City, rows[i],
					out requests[i], out fault)) return 0;
			int[] between;
			if (!KingdomDistanceRuntime.TryTargetMetric(system.City, requests, rows.Count,
				out between, out fault)) return 0;
			KingdomLogisticsSnapshotPlan plan;
			if (!KingdomLogisticsRules.TryPlanSnapshot(requests, rows.Count, between,
				KingdomLogisticsRules.CarrierCapacity, out plan, out fault)) return 0;

			int started = 0;
			for (int trip = 0; trip < plan.TripCount; trip++)
			{
				List<int> members = new List<int>();
				for (int i = 0; i < plan.ConsideredCount; i++)
					if (plan.TripIndexes[i] == trip) members.Add(i);
				members.Sort(delegate(int a, int b)
				{
					return plan.StopOrdinals[a].CompareTo(plan.StopOrdinals[b]);
				});
				if (members.Count == 0) continue;
				KingdomJobRow seed = rows[members[0]];
				int total = 0;
				for (int i = 0; i < members.Count; i++) total += rows[members[i]].CargoAmount;
				long sourceBefore = 0L;
				if (seed.DeliveryCargoAuthority == KingdomDeliveryCargoAuthority.ScalarStock
					&& (!TryExactScalarAmount(survey, seed, source: true, out sourceBefore)
						|| sourceBefore < total)) continue;
				int origin = KingdomResidentRules.NoOrigin;
				KingdomJobRules.TryDrawOrigin(system.SimulationSeed,
					KingdomChronicle.SettlementId(system), plan.TripLeaderJobIds[members[0]],
					KingdomRules.Origins.Length, out origin, out fault);
				KingdomJobRow[] prepared = new KingdomJobRow[members.Count];
				string fromZone = seed.SourceZoneId;
				int fromEndpoint = seed.DeliverySourceEndpointId;
				string fromObject = seed.DeliverySourceObjectId;
				long depart = now;
				bool routeOk = true;
				for (int ordinal = 0; ordinal < members.Count; ordinal++)
				{
					KingdomJobRow row = rows[members[ordinal]];
					KingdomLeg[] legs;
					int legCount;
					long arrive;
					if (!TryBuildSegment(system, plan.TripLeaderJobIds[members[0]],
						fromZone, fromEndpoint, fromObject, row.DestZoneId,
						row.DeliveryTargetEndpointId, row.DeliveryTargetObjectId, depart,
						out legs, out legCount, out arrive, out fault))
					{
						routeOk = false;
						break;
					}
					prepared[ordinal] = row.WithDeliveryPlan(now, origin, legs, legCount,
						seed.DeliveryCargoAuthority == KingdomDeliveryCargoAuthority.ScalarStock
							? sourceBefore : 0L,
						plan.TripLeaderJobIds[members[0]], ordinal + 1,
						KingdomDeliveryPhase.SourceDebitPrepared);
					fromZone = row.DestZoneId;
					fromEndpoint = row.DeliveryTargetEndpointId;
					fromObject = row.DeliveryTargetObjectId;
					depart = arrive;
				}
				if (!routeOk) continue;
				KingdomJobTable next;
				if (!table.TryRewrite(prepared, prepared.Length, out next, out fault)
					|| !system.Jobs.TryPublish(next, out fault)) continue;
				table = next;
				if (seed.DeliveryCargoAuthority == KingdomDeliveryCargoAuthority.CarryBookManifest)
				{
					started += prepared.Length;
					continue;
				}
				int debited;
				if (!TryDebitScalar(survey, seed, total, out debited) || debited != total)
					continue;
				KingdomJobRow[] inFlight = new KingdomJobRow[prepared.Length];
				for (int i = 0; i < prepared.Length; i++)
					inFlight[i] = prepared[i].WithDeliveryPhase(KingdomDeliveryPhase.InFlight);
				if (!table.TryRewrite(inFlight, inFlight.Length, out next, out fault)
					|| !system.Jobs.TryPublish(next, out fault)) continue;
				table = next;
				int sourceIndex = requests[members[0]].SourceZoneIndex;
				if (system.City.DistanceCache == null
					|| !system.City.DistanceCache.TrySpend(sourceIndex,
						seed.DeliverySourceEndpointId, seed.Cargo, total))
					system.City.DistanceCache = null;
				started += inFlight.Length;
			}
			return started;
		}

		/// <summary>Recovers scalar source callbacks bracketed by SourceDebitPrepared. Exact before
		/// or exact after are the only accepted observations; partial/interfered holders stay frozen.</summary>
		internal static int RecoverPreparedSources(KingdomSystem system, Zone zone,
			KingdomSurvey survey)
		{
			KingdomJobTable table;
			KingdomCityFault fault;
			if (system == null || zone == null || survey == null || system.Jobs == null
				|| !system.Jobs.TryRead(out table, out fault)) return 0;
			int recovered = 0;
			for (int i = 0; i < table.Count; i++)
			{
				KingdomJobRow leader;
				if (!table.TryAt(i, out leader) || leader.DeliveryPhase
						!= KingdomDeliveryPhase.SourceDebitPrepared
					|| leader.DeliveryCargoAuthority != KingdomDeliveryCargoAuthority.ScalarStock
					|| leader.JobId != leader.DeliveryTripId
					|| !string.Equals(leader.SourceZoneId, zone.ZoneID,
						StringComparison.Ordinal)) continue;
				List<KingdomJobRow> group = TripRows(table, leader.DeliveryTripId);
				int total = 0;
				for (int j = 0; j < group.Count; j++) total += group[j].CargoAmount;
				long observed;
				if (!TryExactScalarAmount(survey, leader, source: true, out observed)) continue;
				if (observed == leader.DeliverySourceBeforeAmount)
				{
					int debited;
					if (!TryDebitScalar(survey, leader, total, out debited) || debited != total)
						continue;
				}
				else if (observed != leader.DeliverySourceBeforeAmount - total) continue;
				KingdomJobRow[] replacements = new KingdomJobRow[group.Count];
				for (int j = 0; j < group.Count; j++)
					replacements[j] = group[j].WithDeliveryPhase(KingdomDeliveryPhase.InFlight);
				KingdomJobTable next;
				if (!table.TryRewrite(replacements, replacements.Length, out next, out fault)
					|| !system.Jobs.TryPublish(next, out fault)) continue;
				table = next;
				recovered += replacements.Length;
			}
			return recovered;
		}
	}
}
