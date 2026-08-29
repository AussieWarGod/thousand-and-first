using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomGuestLifecycle
	{
		private sealed class GuestWorld : IKingdomLifecycleTrustedWorld
		{
			private readonly KingdomSystem System;
			private readonly Zone Zone;
			private readonly KingdomLifecycleBook Book;
			private readonly KingdomLifecycleOperation Operation;
			private readonly ScheduleReference Schedule = new ScheduleReference();
			private List<IKingdomLifecycleTrustedObservation> Cached;
			private GameObject Tombstone;

			internal GuestWorld(KingdomSystem system, Zone zone, KingdomLifecycleBook book,
				KingdomLifecycleOperation operation)
			{
				System = system; Zone = zone; Book = book; Operation = operation;
				Schedule.Value = CurrentSchedule(system, operation);
				KingdomLifecycleResourceLease lease = ScheduleLease(operation);
				Schedule.Revision = lease == null ? 0L : lease.BeforeRevision;
				for (int i = Book.RecentProofs.Count - 1; i >= 0; i--)
					if (Book.RecentProofs[i] != null && Book.RecentProofs[i].Lane == Operation.Lane)
					{
						Schedule.LastOperationId = Book.RecentProofs[i].Id;
						break;
					}
			}

			public int ObservationCount { get { Cached = Build(); return Cached.Count; } }

			public IKingdomLifecycleTrustedObservation Observe(int index)
			{
				return Cached[index];
			}

			public object InvokeCarryOutput(KingdomLifecycleProjection output) { return null; }

			public object InvokeWater(object vesselReference, int amount)
			{
				GameObject owner = vesselReference as GameObject;
				LiquidVolume liquid = owner == null ? null : owner.GetPart<LiquidVolume>();
				int drained;
				try { drained = KingdomLiquids.Drain(liquid, amount); }
				finally { KingdomSurvey.ObserveCurrentTopologyInActive(Zone, owner); }
				if (drained != amount) return null;
				return owner;
			}

			public object InvokeSchedule(object scheduleReference, long dueTick, string operationId)
			{
				if (!ReferenceEquals(scheduleReference, Schedule) || Operation.Id != operationId)
					return null;
				SetSchedule(System, Operation, dueTick);
				Schedule.Value = dueTick;
				Schedule.Revision++;
				Schedule.LastOperationId = operationId;
				return Schedule;
			}

			public object InvokeCarryRemoval(object sourceReference, int count, string eventId)
			{
				return null;
			}

			public object InvokeCarrySignRemoval(object signReference, int count, string receiptId)
			{
				return null;
			}

			public object InvokeCarryMove(object sourceReference, int tripId,
				KingdomLifecycleTopology targetTopology, string targetOwnerId,
				string targetZoneId, int targetX, int targetY, string receiptId)
			{
				return null;
			}

			public object InvokeLifecycleProjection(KingdomLifecycleProjection projection)
			{
				GameObject body = Operation.Lane == KingdomLifecycleLane.PlainGuest
					? KingdomLocus.CreateLifecycleGuest(Operation, projection)
					: KingdomGuestbook.CreateLifecycleNotable(Operation, projection);
				if (!GameObject.Validate(body)) return null;
				Cell cell = Zone.GetCell(projection.X, projection.Y);
				if (cell == null) { body.Obliterate(); return null; }
				body.ID = projection.ObjectId;
				body.SetStringProperty(MarkerProperty, projection.Marker);
				body.SetStringProperty(OperationProperty, Operation.Id);
				GameObject accepted = null;
				try { accepted = cell.AddObject(body); }
				finally { KingdomSurvey.ObserveAddResultInActive(Zone, body, accepted); }
				if (!ReferenceEquals(accepted, body) || body.CurrentCell != cell)
					return null;
				body.MakeActive();
				return body;
			}

			public object InvokeLifecycleRemoval(object objectReference, int count,
				string operationId)
			{
				GameObject body = objectReference as GameObject;
				if (!GameObject.Validate(body) || count != 1 || operationId != Operation.Id)
					return null;
				bool removed;
				try { removed = body.Obliterate(); }
				finally { KingdomSurvey.ObserveCurrentTopologyInActive(Zone, body); }
				if (!removed || GameObject.Validate(body)) return null;
				Tombstone = body;
				return body;
			}

			private List<IKingdomLifecycleTrustedObservation> Build()
			{
				List<IKingdomLifecycleTrustedObservation> rows =
					new List<IKingdomLifecycleTrustedObservation>();
				KingdomLifecycleResourceLease scheduleLease = ScheduleLease(Operation);
				if (scheduleLease != null)
				{
					rows.Add(new Observation(Schedule, scheduleLease.Key, null, "Schedule",
						Book.SettlementId, null, Operation.ZoneId,
						KingdomLifecycleTopology.Cell, 0, 0, 0, 0, null,
						Schedule.Value, Schedule.Revision, Schedule.LastOperationId));
				}
				foreach (GameObject item in KingdomSurvey.ObjectsFor(Zone))
				{
					if (!GameObject.Validate(item) || item.CurrentCell == null) continue;
					LiquidVolume liquid = item.GetPart<LiquidVolume>();
					rows.Add(new Observation(item, item.IDIfAssigned,
						item.GetStringProperty(MarkerProperty), item.Blueprint, Book.SettlementId,
						null, Zone.ZoneID, KingdomLifecycleTopology.Cell,
						item.CurrentCell.X, item.CurrentCell.Y, 1,
						liquid == null || liquid.MaxVolume < 0 ? 0 : liquid.MaxVolume,
						liquid == null ? null : "water", liquid == null ? 0L : liquid.Volume,
						0L, null));
				}
				if (Tombstone != null)
					rows.Add(new Observation(Tombstone, Operation.ObjectId,
						Tombstone.GetStringProperty(MarkerProperty), Operation.Blueprint,
						Book.SettlementId, null, Operation.ZoneId, Operation.ObjectTopology,
						Operation.ObjectX, Operation.ObjectY, 0, 0, null, 0L, 0L, null));
				return rows;
			}
		}

	}
}
