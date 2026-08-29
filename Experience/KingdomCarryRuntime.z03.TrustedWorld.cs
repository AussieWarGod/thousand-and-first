using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomCarryRuntime
	{

		private sealed class CarryWorld : IKingdomLifecycleTrustedWorld
		{
			private readonly KingdomSystem System;
			private readonly Zone Zone;
			private readonly KingdomCarryBook Book;
			private readonly KingdomCarryOperation Operation;
			private readonly ScheduleReference Schedule = new ScheduleReference();
			private List<IKingdomLifecycleTrustedObservation> Cached;

			internal CarryWorld(KingdomSystem system, Zone zone, KingdomCarryBook book,
				KingdomCarryOperation operation)
			{
				System = system; Zone = zone; Book = book; Operation = operation;
				KingdomLifecycleResourceRevision row = ScheduleRow(book, operation);
				KingdomLifecycleResourceLease lease = operation == null
					? null : operation.ScheduleLease;
				bool applied = MatchesScheduleProjection(system == null ? null : system.Haul,
					operation);
				if (lease != null && applied && (lease.State == KingdomLifecycleLeaseState.Intent
					|| lease.State == KingdomLifecycleLeaseState.Proved))
				{
					Schedule.Value = lease.After;
					Schedule.Revision = lease.AfterRevision;
					Schedule.LastOperationId = operation.Id;
				}
				else
				{
					Schedule.Value = lease == null ? PriorScheduleValue(system == null
						? null : system.Haul) : lease.Before;
					Schedule.Revision = lease == null ? (row == null ? 0L : row.Revision)
						: lease.BeforeRevision;
					Schedule.LastOperationId = row == null ? null : row.LastOperationId;
				}
			}

			public int ObservationCount { get { Cached = Build(); return Cached.Count; } }

			public IKingdomLifecycleTrustedObservation Observe(int index) { return Cached[index]; }

			public object InvokeCarryOutput(KingdomLifecycleProjection output) { return null; }

			public object InvokeWater(object vesselReference, int amount) { return null; }

			public object InvokeSchedule(object scheduleReference, long dueTick,
				string operationId)
			{
				if (!ReferenceEquals(scheduleReference, Schedule) || Operation == null
					|| !string.Equals(Operation.Id, operationId, StringComparison.Ordinal)
					|| Operation.ScheduleLease == null
					|| dueTick != Operation.ScheduleLease.After
					|| LegacyMaterialUnits(System.Haul) > 0) return null;
				System.Haul = new KingdomCarryHaul
				{
					OriginZoneID = Operation.OriginZoneId, OriginX = Operation.OriginX,
					OriginY = Operation.OriginY,
					DestinationSettlementId = Operation.DestinationSettlementId,
					DestinationSettlementName = Operation.DestinationSettlementName,
					PlantedTick = Operation.CreatedTick, DueTick = dueTick
				};
				Schedule.Value = dueTick;
				Schedule.Revision = Operation.ScheduleLease.AfterRevision;
				Schedule.LastOperationId = operationId;
				return Schedule;
			}

			public object InvokeCarryRemoval(object sourceReference, int count,
				string eventId) { return null; }

			public object InvokeCarrySignRemoval(object signReference, int count,
				string receiptId)
			{
				GameObject sign = signReference as GameObject;
				GameObject owner = FindOwner(Operation.SignOwnerId, Operation.SignZoneId);
				if (!GameObject.Validate(sign) || owner == null || owner.Inventory == null
					|| count != 1 || !string.Equals(receiptId, Operation.SignReceiptId,
						StringComparison.Ordinal)
					|| sign.IDIfAssigned != Operation.SignObjectId
					|| sign.Blueprint != Operation.SignBlueprint
					|| sign.InInventory != owner || sign.Equipped != null
					|| sign.Count != Operation.SignCount
					|| ReferenceCount(owner.Inventory.Objects, sign) != 1
					|| !KingdomConstructionInputLeaseAuthority
						.TryObjectAvailableForLocalDebit(sign, out _)) return null;
				int before = sign.Count;
				Zone ownerZone = owner.CurrentZone;
				try { sign.Destroy(null, Silent: true); }
				finally { KingdomSurvey.ObserveCurrentTopologyInActive(ownerZone, owner); }
				if (GameObject.Validate(sign) ? sign.Count != before - 1 : before != 1) return null;
				return sign;
			}

			public object InvokeCarryMove(object sourceReference, int tripId,
				KingdomLifecycleTopology targetTopology, string targetOwnerId,
				string targetZoneId, int targetX, int targetY, string receiptId)
			{
				GameObject item = sourceReference as GameObject;
				KingdomCarrySource source = SourceFor(item, receiptId);
				if (source == null || !GameObject.Validate(item) || item.IsImportant()
					|| item.Equipped != null || !item.IsTakeable() || item.Count != source.PlannedCount
					|| source.CurrentTripId != tripId) return null;
				Zone beforeZone = item.CurrentZone;
				GameObject beforeOwner = item.InInventory;
				GameObject accepted = null;
				if (targetTopology == KingdomLifecycleTopology.Inventory)
				{
					GameObject owner = FindOwner(targetOwnerId, targetZoneId);
					if (!GameObject.Validate(owner) || !owner.IsAlive
						|| owner.GetIntProperty(KingdomResidents.JobIdProperty) != tripId
						|| owner.Inventory == null
						|| ReferenceCount(owner.Inventory.Objects, item) != 0) return null;
					if (!KingdomConstructionInputLeaseAuthority
						.TryObjectGraphAvailableForOrdinaryTransfer(item, out _)) return null;
					try { accepted = owner.Inventory.AddObject(item, null, Silent: true, NoStack: true); }
					finally
					{
						KingdomSurvey.ObserveCurrentTopologyInActive(beforeZone, beforeOwner);
						KingdomSurvey.ObserveCurrentTopologyInActive(owner.CurrentZone, owner);
						KingdomSurvey.ObserveAddResultInActive(owner.CurrentZone, item, accepted);
						if (!ReferenceEquals(beforeZone, owner.CurrentZone))
							KingdomSurvey.ObserveAddResultInActive(beforeZone, item, accepted);
					}
					if (!ReferenceEquals(accepted, item) || item.InInventory != owner
						|| ReferenceCount(owner.Inventory.Objects, item) != 1
						|| !KingdomConstructionInputLeaseAuthority
							.TryObjectGraphAvailableForOrdinaryTransfer(item, out _)) return null;
				}
				else if (targetTopology == KingdomLifecycleTopology.Cell)
				{
					if (Zone == null || !string.Equals(Zone.ZoneID, targetZoneId,
						StringComparison.Ordinal)) return null;
					Cell cell = Zone.GetCell(targetX, targetY);
					if (cell == null || ReferenceCount(cell.GetObjects(), item) != 0) return null;
					if (!KingdomConstructionInputLeaseAuthority
						.TryObjectGraphAvailableForOrdinaryTransfer(item, out _)) return null;
					try { accepted = cell.AddObject(item, NoStack: true, Silent: true); }
					finally
					{
						KingdomSurvey.ObserveCurrentTopologyInActive(beforeZone, beforeOwner);
						KingdomSurvey.ObserveAddResultInActive(beforeZone, item, accepted);
						if (!ReferenceEquals(beforeZone, Zone))
							KingdomSurvey.ObserveAddResultInActive(Zone, item, accepted);
					}
					if (!ReferenceEquals(accepted, item) || item.InInventory != null
						|| !ReferenceEquals(item.CurrentCell, cell)
						|| ReferenceCount(cell.GetObjects(), item) != 1
						|| !KingdomConstructionInputLeaseAuthority
							.TryObjectGraphAvailableForOrdinaryTransfer(item, out _)) return null;
				}
				else return null;
				return item;
			}

			public object InvokeLifecycleProjection(KingdomLifecycleProjection projection)
			{
				return null;
			}

			public object InvokeLifecycleRemoval(object objectReference, int count,
				string operationId) { return null; }

			private List<IKingdomLifecycleTrustedObservation> Build()
			{
				List<IKingdomLifecycleTrustedObservation> rows =
					new List<IKingdomLifecycleTrustedObservation>();
				if (Operation != null)
				{
					string scheduleKey = Operation.ScheduleLease == null
						? KingdomLifecycleRules.ResourceKey(KingdomLifecycleResourceKind.Schedule,
							Book.RealmId, Operation.DestinationSettlementId)
						: Operation.ScheduleLease.Key;
					rows.Add(new Observation(Schedule, scheduleKey, "Schedule",
						Operation.DestinationSettlementId, Operation.DestinationOwnerId,
						Operation.DestinationZoneId, Operation.DestinationTopology,
						Operation.DestinationX, Operation.DestinationY, 0,
						Schedule.Value, Schedule.Revision, Schedule.LastOperationId));
				}
				HashSet<GameObject> seen = new HashSet<GameObject>();
				AddAt(rows, seen, Operation.SignObjectId, Operation.SignTopology,
					Operation.SignOwnerId, Operation.SignZoneId, Operation.SignX, Operation.SignY);
				for (int i = 0; Operation.Sources != null && i < Operation.Sources.Count; i++)
				{
					KingdomCarrySource source = Operation.Sources[i];
					AddAt(rows, seen, source.ObjectId, source.CurrentTopology,
						source.CurrentOwnerId, source.CurrentZoneId, source.CurrentX, source.CurrentY);
					if (source.PendingTransfer != KingdomCarryTransferKind.None)
						AddAt(rows, seen, source.ObjectId, source.PendingTopology,
							source.PendingOwnerId, source.PendingZoneId,
							source.PendingX, source.PendingY);
				}
				return rows;
			}

			private void AddAt(List<IKingdomLifecycleTrustedObservation> rows,
				HashSet<GameObject> seen, string objectId, KingdomLifecycleTopology topology,
				string ownerId, string zoneId, int x, int y)
			{
				if (string.IsNullOrEmpty(objectId)) return;
				if (topology == KingdomLifecycleTopology.Inventory)
				{
					GameObject owner = FindOwner(ownerId, zoneId);
					for (int i = 0; owner != null && owner.Inventory != null
						&& i < owner.Inventory.Objects.Count; i++)
					{
						GameObject item = owner.Inventory.Objects[i];
							if (GameObject.Validate(item)
								&& KingdomConstructionInputLeaseAuthority
									.TryObjectGraphAvailableForOrdinaryTransfer(item, out _)
							&& item.IDIfAssigned == objectId
							&& seen.Add(item))
							rows.Add(ObjectObservation(item, topology, ownerId, zoneId, -1, -1));
					}
				}
				else if (topology == KingdomLifecycleTopology.Cell && Zone != null
					&& string.Equals(Zone.ZoneID, zoneId, StringComparison.Ordinal))
				{
					Cell cell = Zone.GetCell(x, y);
					List<GameObject> found = cell == null ? null : cell.GetObjects();
					for (int i = 0; found != null && i < found.Count; i++)
					{
						GameObject item = found[i];
							if (GameObject.Validate(item)
								&& KingdomConstructionInputLeaseAuthority
									.TryObjectGraphAvailableForOrdinaryTransfer(item, out _)
							&& item.IDIfAssigned == objectId
							&& seen.Add(item))
							rows.Add(ObjectObservation(item, topology, null, zoneId, x, y));
					}
				}
			}

			private Observation ObjectObservation(GameObject item,
				KingdomLifecycleTopology topology, string ownerId, string zoneId, int x, int y)
			{
				return new Observation(item, item.IDIfAssigned, item.Blueprint,
					Operation.DestinationSettlementId, ownerId, zoneId, topology,
					x, y, item.Count, 0L, 0L, null);
			}

			private GameObject FindOwner(string ownerId, string zoneId)
			{
				if (string.IsNullOrEmpty(ownerId)) return null;
				if (The.Player != null && The.Player.IDIfAssigned == ownerId
					&& The.Player.CurrentZone != null
					&& The.Player.CurrentZone.ZoneID == zoneId) return The.Player;
				GameObject found = GameObject.FindByID(ownerId);
				return GameObject.Validate(found) && found.CurrentZone != null
					&& found.CurrentZone.ZoneID == zoneId ? found : null;
			}

			private KingdomCarrySource SourceFor(GameObject item, string receiptId)
			{
				for (int i = 0; item != null && Operation.Sources != null
					&& i < Operation.Sources.Count; i++)
				{
					KingdomCarrySource source = Operation.Sources[i];
					if (source != null && source.ObjectId == item.IDIfAssigned
						&& (source.ReceiptId == receiptId
							|| i < Operation.Outputs.Count
								&& Operation.Outputs[i].ReceiptId == receiptId)) return source;
				}
				return null;
			}
		}
	}
}
