using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Pull view exposed to the CarryBook adapter. The central registry owns route/body;
	/// the opaque owner owns exact manifest references and callback receipts.</summary>
	internal readonly struct KingdomManifestTripView
	{
		internal readonly int JobId;
		internal readonly int TripId;
		internal readonly KingdomDeliveryPhase Phase;
		internal readonly int SourceStart;
		internal readonly int SourceCount;
		internal readonly string CarrierObjectId;
		internal readonly string CarrierZoneId;
		internal readonly KingdomLifecycleTopology CarrierTopology;
		internal readonly int CarrierX;
		internal readonly int CarrierY;
		internal readonly bool CarrierAvailable;

		internal KingdomManifestTripView(int jobId, int tripId, KingdomDeliveryPhase phase,
			int sourceStart, int sourceCount, string carrierObjectId, string carrierZoneId,
			KingdomLifecycleTopology carrierTopology, int carrierX, int carrierY,
			bool carrierAvailable)
		{
			JobId = jobId;
			TripId = tripId;
			Phase = phase;
			SourceStart = sourceStart;
			SourceCount = sourceCount;
			CarrierObjectId = carrierObjectId;
			CarrierZoneId = carrierZoneId;
			CarrierTopology = carrierTopology;
			CarrierX = carrierX;
			CarrierY = carrierY;
			CarrierAvailable = carrierAvailable;
		}
	}

	internal readonly struct KingdomManifestReservation
	{
		internal readonly int[] JobIds;
		internal readonly int[] TripIds;
		internal readonly long ArrivalTick;

		internal KingdomManifestReservation(int[] jobIds, int[] tripIds, long arrivalTick)
		{
			JobIds = jobIds == null ? new int[0] : (int[])jobIds.Clone();
			TripIds = tripIds == null ? new int[0] : (int[])tripIds.Clone();
			ArrivalTick = arrivalTick;
		}
	}

	/// <summary>Production §3.10 coordinator. Planning is one bounded frozen snapshot; this edge
	/// brackets exact holder callbacks and persists every route before physical mutation.</summary>
	internal static class KingdomCentralLogistics
	{
		internal const string TargetReceiptProperty = "KingdomDeliveryReceipt";
		internal const string FoodReceiptJobProperty = "KingdomDeliveryReceiptJob";

		/// <summary>Queues one scalar demand against the nearest exact observed source/target. Open
		/// rows reserve source cargo and target room, preventing repeated check-ins from authorizing
		/// the same physical units twice.</summary>
		internal static bool TryQueueScalar(KingdomSystem system, KingdomCityState state,
			string destinationZoneId, KingdomStockKind kind, long demand, long room,
			long now, out int queued, out KingdomCityFault fault)
		{
			queued = 0;
			if (system == null || system.City == null || system.Jobs == null || state == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			KingdomJobTable table;
			if (!system.Jobs.TryRead(out table, out fault)) return false;
			long destinationReserved = 0L;
			for (int i = 0; i < table.Count; i++)
			{
				KingdomJobRow row;
				if (!table.TryAt(i, out row) || !KingdomJobRules.IsCentralDelivery(row)
					|| row.DeliveryCargoAuthority != KingdomDeliveryCargoAuthority.ScalarStock
					|| row.Cargo != kind || row.CargoAmount <= 0
					|| !string.Equals(row.DestZoneId, destinationZoneId,
						StringComparison.Ordinal)) continue;
				destinationReserved += row.CargoAmount;
			}
			demand -= destinationReserved;
			room -= destinationReserved;
			if (demand <= 0L || room <= 0L || table.Count >= KingdomJobRules.MaxOpenJobs)
			{
				fault = KingdomCityFault.None;
				return true;
			}
			KingdomDistanceTransferPlan plan;
			if (!KingdomDistanceRuntime.TryPlan(system.City, state, destinationZoneId, kind,
				demand, room, out plan, out fault)) return false;
			if (plan.Amount <= 0L)
			{
				fault = KingdomCityFault.None;
				return true;
			}
			long sourceReserved = 0L;
			long targetReserved = 0L;
			for (int i = 0; i < table.Count; i++)
			{
				KingdomJobRow row;
				if (!table.TryAt(i, out row) || !KingdomJobRules.IsCentralDelivery(row)
					|| row.DeliveryCargoAuthority != KingdomDeliveryCargoAuthority.ScalarStock
					|| row.Cargo != kind || row.CargoAmount <= 0) continue;
				if (row.DeliverySourceEndpointId == plan.HolderId
					&& string.Equals(row.DeliverySourceObjectId, plan.HolderObjectId,
						StringComparison.Ordinal)) sourceReserved += row.CargoAmount;
				if (row.DeliveryTargetEndpointId == plan.TargetId
					&& string.Equals(row.DeliveryTargetObjectId, plan.TargetObjectId,
						StringComparison.Ordinal)) targetReserved += row.CargoAmount;
			}
			long amount = plan.Amount - sourceReserved;
			long targetLeft = room - targetReserved;
			if (amount > targetLeft) amount = targetLeft;
			if (amount > KingdomLogisticsRules.CarrierCapacity)
				amount = KingdomLogisticsRules.CarrierCapacity;
			if (amount <= 0L)
			{
				fault = KingdomCityFault.None;
				return true;
			}
			KingdomZoneRow sourceZone;
			if (!state.TryZone(plan.SourceZoneIndex, out sourceZone))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			int jobId = system.Jobs.MintJobId();
			KingdomJobRow opened = new KingdomJobRow(jobId, KingdomJobKind.Delivery,
				kind, (int)amount, sourceZone.ZoneId, destinationZoneId, now,
				KingdomItineraryRules.WalkTicksPerCellDefault, KingdomJobStatus.Open, 0, 0,
				new KingdomLeg[0], 0, deliverySourceEndpointId: plan.HolderId,
				deliverySourceObjectId: plan.HolderObjectId,
				deliverySourceX: plan.SourceX, deliverySourceY: plan.SourceY,
				deliveryTargetEndpointId: plan.TargetId,
				deliveryTargetObjectId: plan.TargetObjectId,
				deliveryTargetX: plan.TargetX, deliveryTargetY: plan.TargetY,
				deliveryPhase: KingdomDeliveryPhase.Planned);
			KingdomJobTable next;
			if (!table.TryOpen(opened, out next, out fault)
				|| !system.Jobs.TryPublish(next, out fault)) return false;
			queued = (int)amount;
			return true;
		}

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

		/// <summary>Deposits arrived scalar stops through exact marked target receipts. Earlier
		/// stops must already be proved; a later destination cannot leapfrog the frozen itinerary.</summary>
		internal static int SettleScalarArrivals(KingdomSystem system, Zone zone,
			KingdomSurvey survey, long now, string cropBlueprint)
		{
			KingdomJobTable table;
			KingdomCityFault fault;
			if (system == null || zone == null || survey == null || system.Jobs == null
				|| !system.Jobs.TryRead(out table, out fault)) return 0;
			int landedRows = 0;
			int[] ids = table.OpenIds();
			for (int n = 0; n < ids.Length; n++)
			{
				KingdomJobRow row;
				if (!table.TryGet(ids[n], out row) || row.DeliveryPhase != KingdomDeliveryPhase.InFlight
					|| row.DeliveryCargoAuthority != KingdomDeliveryCargoAuthority.ScalarStock
					|| row.CargoAmount <= 0
					|| !string.Equals(row.DestZoneId, zone.ZoneID, StringComparison.Ordinal)
					|| !PriorStopsLanded(table, row)) continue;
				KingdomItineraryFix fix;
				if (!KingdomItineraryRules.TryAt(row.Legs(), row.LegCount, now, out fix, out fault)
					|| fix.Phase != KingdomItineraryPhase.Delivered) continue;
				GameObject target;
				LiquidVolume water;
				long observed;
				if (!TryExactScalarTarget(survey, row, out target, out water, out observed)) continue;
				string receipt = Receipt(row);
				string standing = target.GetStringProperty(TargetReceiptProperty);
				if (row.DeliveryTargetReceiptState == KingdomDeliveryTargetReceiptState.None)
				{
					if (!string.IsNullOrEmpty(standing) && !string.Equals(standing, receipt,
						StringComparison.Ordinal)) continue;
					target.SetStringProperty(TargetReceiptProperty, receipt);
					KingdomJobRow prepared = row.WithTargetReceipt(observed,
						KingdomDeliveryTargetReceiptState.Prepared);
					KingdomJobTable next;
					if (!table.TryReplace(prepared, out next, out fault)
						|| !system.Jobs.TryPublish(next, out fault)) continue;
					table = next;
					row = prepared;
					standing = receipt;
				}
				int marked = row.Cargo == KingdomStockKind.Food
					? MarkedFood(target, row.JobId) : 0;
				KingdomScalarReceiptAction action;
				if (!KingdomScalarReceiptRules.TryRecover(row.Cargo,
					row.DeliveryTargetBeforeAmount, row.CargoAmount, observed,
					string.Equals(standing, receipt, StringComparison.Ordinal), marked,
					out action) || action == KingdomScalarReceiptAction.Interference) continue;
				int landed = row.CargoAmount;
				if (action == KingdomScalarReceiptAction.Apply)
				{
					landed = row.Cargo == KingdomStockKind.Water
						? survey.StoreIn(water, row.CargoAmount)
						: AddMarkedFood(survey, target, row.JobId, row.CargoAmount,
							cropBlueprint);
				}
				else if (action == KingdomScalarReceiptAction.ContinueFood)
					landed = marked + AddMarkedFood(survey, target, row.JobId,
						row.CargoAmount - marked, cropBlueprint);
				if (landed != row.CargoAmount) continue;
				KingdomJobRow closedStop = row.WithCargoLanded();
				KingdomJobTable replaced;
				if (!table.TryReplace(closedStop, out replaced, out fault)
					|| !system.Jobs.TryPublish(replaced, out fault)) continue;
				table = replaced;
				landedRows++;
				if (system.City != null && system.City.DistanceCache != null)
				{
					int targetZone;
					if (!system.City.DistanceCache.Matrix.Graph.TryIndexOf(zone.ZoneID,
						out targetZone) || !system.City.DistanceCache.TryFill(targetZone,
							row.DeliveryTargetEndpointId, row.Cargo, row.CargoAmount))
						system.City.DistanceCache = null;
				}
				if (TripLanded(table, row.DeliveryTripId))
				{
					KingdomJobTable without;
					KingdomJobRow[] closed;
					if (table.TryCloseTrip(row.DeliveryTripId, out without, out closed, out fault)
						&& system.Jobs.TryPublish(without, out fault))
					{
						table = without;
						KingdomPorters.RetireCentralCarrier(system, row.DeliveryTripId);
					}
				}
				system.Ledger.Note("{{C|" + KingdomCityRules.CarryNote(row.Cargo,
					row.CargoAmount, KingdomPresentation.Rich(system.KingdomDisplayName)) + "}}");
			}
			return landedRows;
		}

		/// <summary>Removes only stale receipt tags, never their objects. `_stock` remains vanilla's
		/// ownership marker; this merely prevents an old closed trip from blocking a future receipt.</summary>
		internal static void SweepReceiptMarkers(KingdomSystem system, KingdomSurvey survey)
		{
			KingdomJobTable table;
			KingdomCityFault fault;
			if (system == null || survey == null || system.Jobs == null
				|| !system.Jobs.TryRead(out table, out fault)) return;
			for (int i = 0; i < survey.Stores.Count; i++)
				SweepTarget(table, survey.Stores[i] == null ? null : survey.Stores[i].ParentObject);
			for (int i = 0; i < survey.Larders.Count; i++) SweepTarget(table, survey.Larders[i]);
		}

		// Pull-based CarryBook seam -------------------------------------------------------

		/// <summary>Read-only destination proof for a carry-sign. The frozen spill cell is
		/// one endpoint already measured on the destination ground; an absent distance slice
		/// refuses work instead of inventing a heart coordinate.</summary>
		internal static bool TryManifestSpillAnchor(KingdomSystem system, string targetZoneId,
			out int targetX, out int targetY, out KingdomCityFault fault)
		{
			targetX = targetY = -1;
			KingdomDistanceCache cache = system == null || system.City == null
				? null : system.City.DistanceCache;
			int zoneIndex;
			KingdomDistanceZoneCache zone;
			if (cache == null || cache.Matrix == null || string.IsNullOrEmpty(targetZoneId)
				|| !cache.Matrix.Graph.TryIndexOf(targetZoneId, out zoneIndex)
				|| !cache.TryZone(zoneIndex, out zone) || !zone.Observed
				|| zone.Endpoints == null || zone.Endpoints.Length == 0)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			int chosen = -1;
			for (int i = 0; i < zone.Endpoints.Length; i++)
			{
				KingdomDistanceEndpointState row = zone.Endpoints[i];
				if (row.X < 0 || row.Y < 0 || row.X >= KingdomJobRules.ZoneWidth
					|| row.Y >= KingdomJobRules.ZoneHeight) continue;
				if (chosen < 0 || row.EndpointId < zone.Endpoints[chosen].EndpointId)
					chosen = i;
			}
			if (chosen < 0)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			targetX = zone.Endpoints[chosen].X;
			targetY = zone.Endpoints[chosen].Y;
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>Read-only route preview used before founder consent. It shares the exact
		/// planner with reservation, but publishes no job, binding, body, or cargo mutation.</summary>
		internal static bool TryPreviewManifestRoute(KingdomSystem system, Zone liveSourceZone,
			string ownerOperationId, string sourceObjectId, string sourceZoneId, int sourceX,
			int sourceY, string targetObjectId, string targetZoneId, int targetX, int targetY,
			long now, out long arrivalTick, out KingdomCityFault fault)
		{
			arrivalTick = now;
			KingdomLeg[] route;
			int legCount;
			int sourceEndpointId;
			int targetEndpointId;
			return TryBuildManifestRoute(system, liveSourceZone, ownerOperationId,
				sourceObjectId ?? "", sourceZoneId, sourceX, sourceY, targetObjectId ?? "",
				targetZoneId, targetX, targetY, now, out route, out legCount, out arrivalTick,
				out sourceEndpointId, out targetEndpointId, out fault);
		}

		/// <summary>Phase one of exact CarryBook authority. Freezes ids, complete itinerary and
		/// arrival before CarryBook publishes, but creates no body and moves no cargo.</summary>
		internal static bool TryPrepareManifestReservation(KingdomSystem system, Zone liveSourceZone,
			string ownerOperationId, string sourceObjectId, string sourceZoneId, int sourceX,
			int sourceY, string targetObjectId, string targetZoneId, int targetX, int targetY,
			int sourceObjectCount, long now, out KingdomManifestReservation reservation,
			out KingdomCityFault fault)
		{
			reservation = default(KingdomManifestReservation);
			fault = KingdomCityFault.NullArgument;
			if (system == null || system.Jobs == null || string.IsNullOrEmpty(ownerOperationId)
				|| string.IsNullOrEmpty(sourceZoneId) || string.IsNullOrEmpty(targetZoneId)
				|| liveSourceZone == null || liveSourceZone.ZoneID != sourceZoneId
				|| sourceX < 0 || sourceX >= liveSourceZone.Width || sourceY < 0
				|| sourceY >= liveSourceZone.Height || targetX < 0
				|| targetX >= KingdomJobRules.ZoneWidth || targetY < 0
				|| targetY >= KingdomJobRules.ZoneHeight || sourceObjectCount <= 0) return false;
			int expected = (sourceObjectCount + KingdomLogisticsRules.CarrierCapacity - 1)
				/ KingdomLogisticsRules.CarrierCapacity;
			KingdomJobTable table;
			if (!system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> existing = OwnerRows(table, ownerOperationId);
			if (existing.Count > 0)
			{
				if (existing.Count != expected) { fault = KingdomCityFault.DuplicateBinding; return false; }
				int[] heldJobs = new int[existing.Count];
				int[] heldTrips = new int[existing.Count];
				long heldArrival = 0L;
				int heldCount = 0;
				for (int i = 0; i < existing.Count; i++)
				{
					KingdomJobRow row = existing[i];
					KingdomLeg last;
					if (row.DeliveryPhase != KingdomDeliveryPhase.ReservationPrepared
						|| row.SourceZoneId != sourceZoneId || row.DestZoneId != targetZoneId
						|| row.DeliverySourceX != sourceX || row.DeliverySourceY != sourceY
						|| row.DeliveryTargetX != targetX || row.DeliveryTargetY != targetY
						|| !string.Equals(row.DeliverySourceObjectId, sourceObjectId ?? "",
							StringComparison.Ordinal)
						|| !string.Equals(row.DeliveryTargetObjectId, targetObjectId ?? "",
							StringComparison.Ordinal) || !row.TryLeg(row.LegCount - 1, out last))
					{ fault = KingdomCityFault.DuplicateBinding; return false; }
					heldJobs[i] = row.JobId; heldTrips[i] = row.DeliveryTripId;
					heldCount += row.DeliveryManifestSourceCount;
					if (last.ArriveTick > heldArrival) heldArrival = last.ArriveTick;
				}
				if (heldCount != sourceObjectCount)
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
				reservation = new KingdomManifestReservation(heldJobs, heldTrips, heldArrival);
				fault = KingdomCityFault.None;
				return true;
			}
			if (expected <= 0 || expected > KingdomJobRules.MaxOpenJobs
				|| table.Count + expected > KingdomJobRules.MaxOpenJobs)
			{ fault = KingdomCityFault.RowCapExceeded; return false; }
			KingdomLeg[] route;
			int legCount;
			long arrival;
			int sourceEndpointId;
			int targetEndpointId;
			if (!TryBuildManifestRoute(system, liveSourceZone, ownerOperationId,
				sourceObjectId ?? "", sourceZoneId, sourceX, sourceY, targetObjectId ?? "",
				targetZoneId, targetX, targetY, now, out route, out legCount, out arrival,
				out sourceEndpointId, out targetEndpointId, out fault)) return false;
			int[] jobIds = new int[expected];
			int[] tripIds = new int[expected];
			int start = 0;
			for (int i = 0; i < expected; i++)
			{
				jobIds[i] = system.Jobs.MintJobId(); tripIds[i] = jobIds[i];
				int count = sourceObjectCount - start;
				if (count > KingdomLogisticsRules.CarrierCapacity)
					count = KingdomLogisticsRules.CarrierCapacity;
				KingdomJobRow row = new KingdomJobRow(jobIds[i], KingdomJobKind.Delivery,
					KingdomStockKind.OpaqueManifest, count, sourceZoneId, targetZoneId, now,
					KingdomItineraryRules.WalkTicksPerCellDefault, KingdomJobStatus.Open, 0,
					legCount - 1, route, legCount, deliverySourceEndpointId: sourceEndpointId,
					deliverySourceObjectId: sourceObjectId,
					deliverySourceX: sourceX, deliverySourceY: sourceY,
					deliveryTargetEndpointId: targetEndpointId,
					deliveryTargetObjectId: targetObjectId,
					deliveryTargetX: targetX, deliveryTargetY: targetY,
					deliveryTripId: tripIds[i], deliveryStopOrdinal: 1,
					deliveryPhase: KingdomDeliveryPhase.ReservationPrepared,
					deliveryCargoAuthority: KingdomDeliveryCargoAuthority.CarryBookManifest,
					deliveryOwnerOperationId: ownerOperationId,
					deliveryManifestSourceStart: start,
					deliveryManifestSourceCount: count);
				KingdomJobTable next;
				if (!table.TryOpen(row, out next, out fault)) return false;
				table = next;
				start += count;
			}
			if (start != sourceObjectCount || !system.Jobs.TryPublish(table, out fault)) return false;
			reservation = new KingdomManifestReservation(jobIds, tripIds, arrival);
			return true;
		}

		internal static bool TryActivateManifestReservation(KingdomSystem system,
			string ownerOperationId, int manifestVersion, string manifestDigest,
			long manifestRevision, int[] jobIds, int[] tripIds, long arrivalTick,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || string.IsNullOrEmpty(ownerOperationId)
				|| manifestVersion <= 0 || string.IsNullOrEmpty(manifestDigest)
				|| manifestRevision < 0L || jobIds == null || tripIds == null
				|| jobIds.Length == 0 || jobIds.Length != tripIds.Length
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			KingdomJobRow[] activated = new KingdomJobRow[jobIds.Length];
			bool exact = true;
			for (int i = 0; i < jobIds.Length; i++)
			{
				KingdomJobRow row;
				KingdomLeg last;
				if (!table.TryGet(jobIds[i], out row) || row.DeliveryTripId != tripIds[i]
					|| (row.DeliveryPhase != KingdomDeliveryPhase.ReservationPrepared
						&& row.DeliveryPhase != KingdomDeliveryPhase.SourceDebitPrepared
						&& row.DeliveryPhase != KingdomDeliveryPhase.InFlight)
					|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
						StringComparison.Ordinal) || !row.TryLeg(row.LegCount - 1, out last)
					|| last.ArriveTick != arrivalTick
					|| (row.DeliveryPhase != KingdomDeliveryPhase.ReservationPrepared
						&& (row.DeliveryOwnerManifestVersion != manifestVersion
							|| !string.Equals(row.DeliveryOwnerManifestDigest, manifestDigest,
								StringComparison.Ordinal)
							|| row.DeliveryOwnerManifestRevision > manifestRevision)))
				{ exact = false; break; }
				activated[i] = row.DeliveryPhase == KingdomDeliveryPhase.ReservationPrepared
					? row.WithManifestAuthority(manifestVersion, manifestDigest,
						manifestRevision, KingdomDeliveryPhase.SourceDebitPrepared) : row;
			}
			if (!exact)
			{
				QuarantineOwner(system, table, ownerOperationId);
				fault = KingdomCityFault.DuplicateBinding;
				return false;
			}
			KingdomJobTable next;
			return table.TryRewrite(activated, activated.Length, out next, out fault)
				&& system.Jobs.TryPublish(next, out fault);
		}

		internal static bool TryCancelManifestReservation(KingdomSystem system,
			string ownerOperationId, out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || string.IsNullOrEmpty(ownerOperationId)
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> rows = OwnerRows(table, ownerOperationId);
			for (int i = 0; i < rows.Count; i++)
				if (rows[i].DeliveryPhase != KingdomDeliveryPhase.ReservationPrepared)
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomJobTable next;
				KingdomJobRow closed;
				if (!table.TryClose(rows[i].JobId, out next, out closed, out fault)) return false;
				table = next;
			}
			return system.Jobs.TryPublish(table, out fault);
		}

		internal static bool TryManifestTrip(KingdomSystem system, string ownerOperationId,
			int sourceOrdinal, out KingdomManifestTripView view)
		{
			view = default(KingdomManifestTripView);
			KingdomJobTable jobs;
			KingdomCityFault fault;
			if (system == null || system.Jobs == null || sourceOrdinal < 0
				|| string.IsNullOrEmpty(ownerOperationId)
				|| !system.Jobs.TryRead(out jobs, out fault)) return false;
			for (int i = 0; i < jobs.Count; i++)
			{
				KingdomJobRow row;
				if (!jobs.TryAt(i, out row)
					|| row.DeliveryCargoAuthority != KingdomDeliveryCargoAuthority.CarryBookManifest
					|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
						StringComparison.Ordinal)
					|| sourceOrdinal < row.DeliveryManifestSourceStart
					|| sourceOrdinal >= row.DeliveryManifestSourceStart
						+ row.DeliveryManifestSourceCount) continue;
				string objectId = null;
				string zoneId = null;
				int x = -1;
				int y = -1;
				bool available = false;
				KingdomBindingTable bindings;
				if (system.Bindings != null && system.Bindings.TryRead(out bindings, out fault))
				{
					KingdomBinding binding;
					if (bindings.TryGet(row.DeliveryTripId, KingdomBindingKind.Transient, out binding))
					{
						objectId = binding.ObjectId;
						zoneId = binding.ZoneId;
						Zone live = The.Player == null ? null : The.Player.CurrentZone;
						GameObject carrier = live != null && live.ZoneID == zoneId
							? live.FindObjectByID(objectId) : null;
						if (GameObject.Validate(carrier) && carrier.CurrentCell != null)
						{
							x = carrier.CurrentCell.X; y = carrier.CurrentCell.Y; available = true;
						}
					}
				}
				view = new KingdomManifestTripView(row.JobId, row.DeliveryTripId,
					row.DeliveryPhase, row.DeliveryManifestSourceStart,
					row.DeliveryManifestSourceCount, objectId, zoneId,
					KingdomLifecycleTopology.Cell, x, y, available);
				return true;
			}
			return false;
		}

		/// <summary>At the frozen arrival tick, thaws and moves the same central porter body to
		/// the exact destination cell. This is the off-screen half of the itinerary: cargo remains
		/// in the body's real inventory and no replacement porter or item is minted.</summary>
		internal static bool TryMaterializeManifestArrival(KingdomSystem system,
			string ownerOperationId, int tripId, Zone liveDestination, long now,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || system.Bindings == null
				|| string.IsNullOrEmpty(ownerOperationId) || tripId <= 0
				|| liveDestination == null || The.ZoneManager == null
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> rows = TripRows(table, tripId);
			if (rows.Count != 1)
			{
				fault = KingdomCityFault.DuplicateBinding;
				return false;
			}
			KingdomJobRow row = rows[0];
			KingdomLeg last;
			if (row.DeliveryCargoAuthority != KingdomDeliveryCargoAuthority.CarryBookManifest
				|| row.DeliveryPhase != KingdomDeliveryPhase.InFlight
				|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
					StringComparison.Ordinal)
				|| !string.Equals(row.DestZoneId, liveDestination.ZoneID,
					StringComparison.Ordinal)
				|| !row.TryLeg(row.LegCount - 1, out last) || now < last.ArriveTick)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			Cell target = liveDestination.GetCell(row.DeliveryTargetX, row.DeliveryTargetY);
			KingdomBindingTable bindings;
			KingdomBinding binding;
			if (target == null || !system.Bindings.TryRead(out bindings, out fault)
				|| !bindings.TryGet(tripId, KingdomBindingKind.Transient, out binding)
				|| string.IsNullOrEmpty(binding.ObjectId) || string.IsNullOrEmpty(binding.ZoneId))
			{
				fault = KingdomCityFault.UnknownBinding;
				return false;
			}
			Zone heldZone = null;
			if (The.ZoneManager.CachedZones != null)
				The.ZoneManager.CachedZones.TryGetValue(binding.ZoneId, out heldZone);
			if (heldZone == null)
			{
				try { heldZone = The.ZoneManager.GetZone(binding.ZoneId); }
				catch { fault = KingdomCityFault.OutsideItinerary; return false; }
			}
			GameObject body = heldZone == null ? null : heldZone.FindObjectByID(binding.ObjectId);
			if (!GameObject.Validate(body) || !body.IsAlive || body.Inventory == null
				|| body.CurrentCell == null
				|| body.GetIntProperty(KingdomResidents.JobIdProperty) != tripId)
			{
				fault = KingdomCityFault.UnknownBinding;
				return false;
			}
			if (!ReferenceEquals(body.CurrentCell, target))
			{
				try
				{
					if (!body.SystemLongDistanceMoveTo(target, 0, forced: true,
						ignoreCombat: true) || !ReferenceEquals(body.CurrentCell, target))
					{
						fault = KingdomCityFault.OutsideItinerary;
						return false;
					}
				}
				catch { fault = KingdomCityFault.OutsideItinerary; return false; }
			}
			if (!KingdomResidents.Bind(system, tripId, KingdomBindingKind.Transient,
				liveDestination.ZoneID, body, now))
			{
				fault = KingdomCityFault.DuplicateBinding;
				return false;
			}
			fault = KingdomCityFault.None;
			return ReferenceEquals(body.CurrentCell, target)
				&& string.Equals(body.CurrentZone == null ? null : body.CurrentZone.ZoneID,
					liveDestination.ZoneID, StringComparison.Ordinal);
		}

		internal static bool TryAcknowledgeManifestPickup(KingdomSystem system,
			string ownerOperationId, int tripId, long provedManifestRevision,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || tripId <= 0
				|| string.IsNullOrEmpty(ownerOperationId) || provedManifestRevision <= 0L
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> rows = TripRows(table, tripId);
			if (rows.Count <= 0) { fault = KingdomCityFault.InvalidIndex; return false; }
			KingdomJobRow[] nextRows = new KingdomJobRow[rows.Count];
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomJobRow row = rows[i];
				if (row.DeliveryCargoAuthority != KingdomDeliveryCargoAuthority.CarryBookManifest
					|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
						StringComparison.Ordinal) || (row.DeliveryPhase
						!= KingdomDeliveryPhase.SourceDebitPrepared
						&& row.DeliveryPhase != KingdomDeliveryPhase.InFlight)
					|| provedManifestRevision < row.DeliveryOwnerManifestRevision)
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
				nextRows[i] = row.WithManifestRevision(provedManifestRevision,
					KingdomDeliveryPhase.InFlight);
			}
			KingdomJobTable next;
			return table.TryRewrite(nextRows, nextRows.Length, out next, out fault)
				&& system.Jobs.TryPublish(next, out fault);
		}

		internal static bool TryAcknowledgeManifestDelivered(KingdomSystem system,
			string ownerOperationId, int tripId, long provedManifestRevision,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || tripId <= 0
				|| string.IsNullOrEmpty(ownerOperationId) || provedManifestRevision <= 0L
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> rows = TripRows(table, tripId);
			if (rows.Count <= 0) { fault = KingdomCityFault.InvalidIndex; return false; }
			for (int i = 0; i < rows.Count; i++)
				if (rows[i].DeliveryCargoAuthority
						!= KingdomDeliveryCargoAuthority.CarryBookManifest
					|| rows[i].DeliveryPhase != KingdomDeliveryPhase.InFlight
					|| !string.Equals(rows[i].DeliveryOwnerOperationId, ownerOperationId,
						StringComparison.Ordinal)
					|| provedManifestRevision < rows[i].DeliveryOwnerManifestRevision)
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
			KingdomJobTable next;
			KingdomJobRow[] closed;
			if (!table.TryCloseTrip(tripId, out next, out closed, out fault)
				|| !system.Jobs.TryPublish(next, out fault)) return false;
			KingdomPorters.RetireCentralCarrier(system, tripId);
			return true;
		}

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

		private static bool TryExactScalarAmount(KingdomSurvey survey, KingdomJobRow row,
			bool source, out long amount)
		{
			amount = 0L;
			GameObject target;
			LiquidVolume water;
			return TryExactScalar(survey, row, source, out target, out water, out amount);
		}

		private static bool TryExactScalarTarget(KingdomSurvey survey, KingdomJobRow row,
			out GameObject target, out LiquidVolume water, out long amount)
		{
			return TryExactScalar(survey, row, source: false, out target, out water, out amount);
		}

		private static bool TryExactScalar(KingdomSurvey survey, KingdomJobRow row, bool source,
			out GameObject target, out LiquidVolume water, out long amount)
		{
			target = null; water = null; amount = 0L;
			if (survey == null) return false;
			int endpoint = source ? row.DeliverySourceEndpointId : row.DeliveryTargetEndpointId;
			string objectId = source ? row.DeliverySourceObjectId : row.DeliveryTargetObjectId;
			if (row.Cargo == KingdomStockKind.Water)
			{
				for (int i = 0; i < survey.Stores.Count; i++)
				{
					LiquidVolume candidate = survey.Stores[i];
					GameObject owner = candidate == null ? null : candidate.ParentObject;
					if (!GameObject.Validate(owner) || !string.Equals(owner.ID, objectId,
						StringComparison.Ordinal) || KingdomCityRules.StableId(owner.ID) != endpoint)
						continue;
					target = owner; water = candidate;
					amount = KingdomLiquids.HasFreshWater(candidate) ? candidate.Volume : 0L;
					return true;
				}
				return false;
			}
			if (row.Cargo == KingdomStockKind.Food)
			{
				for (int i = 0; i < survey.Larders.Count; i++)
				{
					GameObject candidate = survey.Larders[i];
					if (!GameObject.Validate(candidate) || !string.Equals(candidate.ID, objectId,
						StringComparison.Ordinal) || KingdomCityRules.StableId(candidate.ID) != endpoint)
						continue;
					target = candidate; amount = KingdomSurvey.HeldIn(candidate);
					return true;
				}
			}
			return false;
		}

		private static bool TryDebitScalar(KingdomSurvey survey, KingdomJobRow row,
			int amount, out int debited)
		{
			debited = 0;
			GameObject target;
			LiquidVolume water;
			long before;
			if (amount <= 0 || !TryExactScalar(survey, row, source: true,
				out target, out water, out before) || before < amount) return false;
			return row.Cargo == KingdomStockKind.Water
				? survey.TryLeakFromExact(water, amount, out debited)
				: survey.TrySpoilFromExact(target, amount, out debited);
		}

		private static int AddMarkedFood(KingdomSurvey survey, GameObject target, int jobId,
			int amount, string blueprint)
		{
			if (survey == null || !GameObject.Validate(target) || target.Inventory == null
				|| amount <= 0 || string.IsNullOrEmpty(blueprint)) return 0;
			int before = MarkedFood(target, jobId);
			try
			{
				for (int i = 0; i < amount; i++)
				{
					GameObject food = GameObject.Create(blueprint);
					if (!GameObject.Validate(food)
						|| (!food.HasPart("Food") && !food.HasPart("PreparedCookingIngredient")))
					{
						if (GameObject.Validate(food)) food.Obliterate();
						break;
					}
					food.SetIntProperty(KingdomPorters.StockProperty, 1);
					food.SetIntProperty(FoodReceiptJobProperty, jobId);
					target.Inventory.AddObject(food, Silent: true);
				}
			}
			catch
			{
				// Inventory callbacks may land one or more marked units before throwing.
				// Publish the measured receipt delta before the module guard sees the fault.
				PublishMarkedFoodDelta(survey, target, jobId, before);
				throw;
			}
			return PublishMarkedFoodDelta(survey, target, jobId, before);
		}

		private static int PublishMarkedFoodDelta(KingdomSurvey survey, GameObject target,
			int jobId, int before)
		{
			int added = MarkedFood(target, jobId) - before;
			if (added > 0)
			{
				survey.FoodStored += added;
				survey.FoodAbundance = KingdomRules.ClassifyPantry(survey.FoodStored);
				survey.SynchronizeReceiptObject(target);
			}
			return added;
		}

		private static int MarkedFood(GameObject target, int jobId)
		{
			int count = 0;
			List<GameObject> items = !GameObject.Validate(target) || target.Inventory == null
				? null : target.Inventory.GetObjects();
			for (int i = 0; items != null && i < items.Count; i++)
			{
				GameObject item = items[i];
				if (GameObject.Validate(item) && item.GetIntProperty(FoodReceiptJobProperty) == jobId
					&& item.GetIntProperty(KingdomPorters.StockProperty) == 1
					&& (item.HasPart("Food") || item.HasPart("PreparedCookingIngredient")))
					count += item.Count;
			}
			return count;
		}

		private static List<KingdomJobRow> TripRows(KingdomJobTable table, int tripId)
		{
			List<KingdomJobRow> rows = new List<KingdomJobRow>();
			for (int i = 0; table != null && i < table.Count; i++)
			{
				KingdomJobRow row;
				if (table.TryAt(i, out row) && row.DeliveryTripId == tripId) rows.Add(row);
			}
			rows.Sort(delegate(KingdomJobRow a, KingdomJobRow b)
			{
				return a.DeliveryStopOrdinal.CompareTo(b.DeliveryStopOrdinal);
			});
			return rows;
		}

		private static bool PriorStopsLanded(KingdomJobTable table, KingdomJobRow row)
		{
			List<KingdomJobRow> group = TripRows(table, row.DeliveryTripId);
			for (int i = 0; i < group.Count; i++)
				if (group[i].DeliveryStopOrdinal < row.DeliveryStopOrdinal
					&& group[i].CargoAmount > 0) return false;
			return true;
		}

		private static bool TripLanded(KingdomJobTable table, int tripId)
		{
			List<KingdomJobRow> rows = TripRows(table, tripId);
			if (rows.Count == 0) return false;
			for (int i = 0; i < rows.Count; i++) if (rows[i].CargoAmount > 0) return false;
			return true;
		}

		private static string Receipt(KingdomJobRow row)
		{
			return "taf:delivery:" + row.DeliveryTripId + ":" + row.JobId;
		}

		private static void SweepTarget(KingdomJobTable table, GameObject target)
		{
			if (!GameObject.Validate(target)) return;
			string marker = target.GetStringProperty(TargetReceiptProperty);
			bool active = false;
			for (int i = 0; !string.IsNullOrEmpty(marker) && table != null && i < table.Count; i++)
			{
				KingdomJobRow row;
				if (table.TryAt(i, out row) && string.Equals(row.DeliveryTargetObjectId,
					target.ID, StringComparison.Ordinal) && string.Equals(Receipt(row), marker,
					StringComparison.Ordinal)) { active = true; break; }
			}
			if (!active && !string.IsNullOrEmpty(marker)) target.RemoveStringProperty(TargetReceiptProperty);
			List<GameObject> items = target.Inventory == null ? null : target.Inventory.GetObjects();
			for (int i = 0; items != null && i < items.Count; i++)
			{
				int jobId = items[i].GetIntProperty(FoodReceiptJobProperty);
				if (jobId > 0 && (table == null || !table.Holds(jobId)))
					items[i].RemoveIntProperty(FoodReceiptJobProperty);
			}
		}
	}
}
