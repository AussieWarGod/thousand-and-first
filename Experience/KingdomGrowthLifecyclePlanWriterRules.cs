using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		private static void WriteGrowthFieldStatePlan(BinaryWriter w,
			KingdomGrowthFieldState x)
		{
			w.Write(x != null); if (x == null) return;
			CanonicalString(w, x.FieldId); CanonicalString(w, x.WorkObjectId);
			CanonicalString(w, x.WorkPartId); CanonicalString(w, x.Marker);
			CanonicalString(w, x.Blueprint); CanonicalString(w, x.ZoneId);
			w.Write(x.X); w.Write(x.Y); CanonicalString(w, x.CropBlueprint);
			w.Write(x.Stage); w.Write(x.NextStageTick); w.Write(x.SownTick);
			w.Write(x.Cycles); w.Write(x.SaidWant); w.Write(x.DeclaredRows);
			w.Write(x.EffectivenessPercent); w.Write(x.MethodPercent);
			w.Write(x.NoLarderAnnounced); CanonicalString(w, x.SeedBlueprint);
			CanonicalString(w, x.PartGraphHash); CanonicalString(w, x.ObjectGraphHash);
			CanonicalString(w, x.TopologyHash);
		}

		private static void WriteGrowthCropRowsPlan(BinaryWriter w,
			List<KingdomGrowthCropRow> rows)
		{
			w.Write(rows != null); if (rows == null) return;
			w.Write(rows.Count);
			for (int i = 0; i < rows.Count; i++) WriteGrowthCropRowPlan(w, rows[i]);
		}

		private static void WriteGrowthCropRowPlan(BinaryWriter w, KingdomGrowthCropRow x)
		{
			w.Write(x != null); if (x == null) return;
			CanonicalString(w, x.FieldId); CanonicalString(w, x.RowId);
			CanonicalString(w, x.ObjectId); CanonicalString(w, x.Marker);
			CanonicalString(w, x.Blueprint); CanonicalString(w, x.ZoneId);
			CanonicalString(w, x.OwnerId); w.Write(x.X); w.Write(x.Y); w.Write(x.Count);
			w.Write(x.HasHarvestable); w.Write(x.Ripe); w.Write(x.RegenTimer);
			CanonicalString(w, x.RegenTime); w.Write(x.TileIndex);
			CanonicalString(w, x.RenderTile); CanonicalString(w, x.RenderColor);
			CanonicalString(w, x.RenderDetail); CanonicalString(w, x.RenderString);
			CanonicalString(w, x.TileColor); CanonicalString(w, x.PartGraphHash);
			CanonicalString(w, x.ObjectGraphHash); CanonicalString(w, x.TopologyHash);
			w.Write(x.Revision); CanonicalString(w, x.LastOperationId);
		}

		private static void WriteGrowthScarcityPlan(BinaryWriter w,
			KingdomGrowthScarcitySnapshot x)
		{
			w.Write(x != null); if (x == null) return;
			w.Write(x.DryStreak); w.Write(x.Withered); w.Write(x.HungerStreak);
			w.Write(x.Famished); w.Write((int)x.LastMeal); w.Write(x.MealShade);
			w.Write(x.ScrapsAnnounced); w.Write(x.ElapsedTicks); w.Write(x.Days);
			w.Write(x.Population); w.Write(x.Stage); w.Write(x.UpkeepRequested);
			w.Write(x.WaterAvailable);
			w.Write(x.RationsAvailable); w.Write(x.Foraged); w.Write(x.Eaten);
			w.Write(x.FromDish); w.Write(x.Kitchens); CanonicalString(w, x.DishName);
			CanonicalString(w, x.DishText); CanonicalString(w, x.DishStaple);
			CanonicalString(w, x.DishSource);
			w.Write((byte)x.ComposedBite); w.Write(x.RequestedWater);
			w.Write(x.ProvedWater); w.Write(x.RequestedRations); w.Write(x.ProvedRations);
			w.Write(x.StoresPolicy); w.Write(x.DistrictPercent); w.Write((byte)x.ThirstOutcome);
			w.Write((byte)x.HungerOutcome); w.Write(x.Thirsting); w.Write(x.Starving);
			w.Write(x.Withering); w.Write(x.Famishing); w.Write(x.Healthy);
		}

		private static void WriteGrowthAccountingPlan(BinaryWriter w,
			KingdomGrowthAccountingSnapshot x)
		{
			w.Write(x != null); if (x == null) return;
			w.Write(x.Fetched); w.Write(x.UpkeepDrawn); w.Write(x.ArrivalCost);
			w.Write(x.Delivered); w.Write(x.Harvested); w.Write(x.Foraged);
			w.Write(x.RationsDrawn); w.Write(x.Milled); w.Write(x.HarvestLost);
			w.Write(x.Plundered); w.Write(x.Arrivals); w.Write(x.Departures);
		}

		public static bool TryPublishGrowth(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation)
		{
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| Operation == null || Operation.LegacyGrowthV1Plan) return false;
			KingdomGrowthSlotKind slot = SlotForGrowthAction(Operation.Action);
			KingdomGrowthFieldSlot field = slot == KingdomGrowthSlotKind.Field
				? FindGrowthField(Book, Operation.FieldId) : null;
			if (slot == KingdomGrowthSlotKind.None || (slot == KingdomGrowthSlotKind.Field
				&& (field == null || field.Quarantined)) || GetGrowthOperation(Book, slot,
					Operation.FieldId) != null || Operation.Sequence != GetGrowthNext(Book, slot, field)
				|| !IsExactSuccessor(Operation.Sequence, GetGrowthRetired(Book, slot, field))
				|| Operation.Sequence == long.MaxValue
				|| !GrowthOperationShape(Book, Operation, slot, Operation.FieldId, true)
				|| !GrowthPublicationSnapshotsMatch(Book, Operation, field)
				|| !GrowthActiveIdentityClaimsValid(Book, Operation)) return false;
			string hash;
			if (!TryGrowthPlanHash(Operation, out hash)) return false;
			List<KingdomLifecycleResourceLease> leases = GrowthLeases(Operation);
			if (leases == null) return false;
			List<KingdomLifecycleResourceRevision> rows =
				new List<KingdomLifecycleResourceRevision>(leases.Count);
			List<bool> createdRows = new List<bool>(leases.Count);
			List<string> activeBefore = new List<string>(leases.Count);
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			int newRows = 0;
			for (int i = 0; i < leases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = leases[i];
				if (!GrowthLeaseShape(lease, Operation.Id, true) || !keys.Add(lease.Key)) return false;
				KingdomLifecycleResourceRevision row = FindGrowthResource(Book, lease.Key);
				bool created = row == null;
				if (row == null)
				{
					row = new KingdomLifecycleResourceRevision
					{
						Kind = lease.Kind, ScopeId = lease.ScopeId, SubjectId = lease.SubjectId,
						Key = lease.Key, Revision = 0L
					};
					newRows++;
				}
				if (!GrowthResourceMatches(row, lease) || row.Revision != lease.BeforeRevision
					|| !string.IsNullOrEmpty(row.ActiveOperationId)
					|| string.Equals(row.LastOperationId, Operation.Id, StringComparison.Ordinal))
					return false;
				rows.Add(row);
				createdRows.Add(created);
				activeBefore.Add(row.ActiveOperationId);
			}
			if (Book.Resources.Count + newRows > MaxResourceRows) return false;
			string planHashBefore = Operation.PlanHash;
			Operation.PlanHash = hash;
			for (int i = 0; i < rows.Count; i++)
			{
				if (FindGrowthResource(Book, rows[i].Key) == null) Book.Resources.Add(rows[i]);
				rows[i].ActiveOperationId = Operation.Id;
			}
			SetGrowthNext(Book, slot, field, Operation.Sequence + 1L);
			SetGrowthOperation(Book, slot, field, Operation);
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			SetGrowthOperation(Book, slot, field, null);
			SetGrowthNext(Book, slot, field, Operation.Sequence);
			for (int i = rows.Count - 1; i >= 0; i--)
			{
				if (createdRows[i]) Book.Resources.Remove(rows[i]);
				else rows[i].ActiveOperationId = activeBefore[i];
			}
			Operation.PlanHash = planHashBefore;
			return false;
		}

		public static bool CanTransitionGrowth(KingdomGrowthAction Action,
			KingdomGrowthPhase From, KingdomGrowthPhase To)
		{
			// Quarantine publication owns a bounded fault and retained evidence. The generic
			// phase mover cannot construct that receipt, so it must never enter this phase.
			if (To == KingdomGrowthPhase.Quarantined) return false;
			KingdomGrowthPhase next;
			return TryNextGrowthPhase(Action, From, out next) && next == To;
		}

		public static bool CanTransitionGrowth(KingdomGrowthOperation Operation,
			KingdomGrowthPhase From, KingdomGrowthPhase To)
		{
			if (Operation == null || To == KingdomGrowthPhase.Quarantined) return false;
			KingdomGrowthPhase next;
			return TryNextGrowthPhase(Operation, From, out next) && next == To;
		}

		public static bool AdvanceGrowthPhase(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, KingdomGrowthPhase To, long Tick)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation) || Tick < Operation.UpdatedTick
				|| !CanTransitionGrowth(Operation, Operation.Phase, To)
				|| !GrowthTransitionReady(Book, Operation, To)) return false;
			KingdomGrowthPhase phaseBefore = Operation.Phase;
			long tickBefore = Operation.UpdatedTick;
			Operation.Phase = To; Operation.UpdatedTick = Tick;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			Operation.Phase = phaseBefore; Operation.UpdatedTick = tickBefore;
			return false;
		}

		public static bool QuarantineGrowthOperation(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, string Fault)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation)
				|| Operation.Phase == KingdomGrowthPhase.Quarantined
				|| string.IsNullOrEmpty(Fault) || TooLong(Fault, MaxTextChars)) return false;
			KingdomGrowthPhase before = Operation.Phase;
			string beforeFault = Operation.Fault;
			Operation.Phase = KingdomGrowthPhase.Quarantined;
			Operation.Fault = SafeFault(Fault);
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			Operation.Phase = before; Operation.Fault = beforeFault;
			return false;
		}

		public static KingdomLifecycleCasAction GrowthClockAction(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, long CurrentValue)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation)
				|| Operation.Phase != KingdomGrowthPhase.ClockIntent) return KingdomLifecycleCasAction.Quarantine;
			KingdomLifecycleResourceLease lease = Operation.ClockLease;
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, lease.Key);
			if (!GrowthResourceMatches(row, lease)
				|| !string.Equals(row.ActiveOperationId, Operation.Id, StringComparison.Ordinal)
				|| CurrentValue < GrowthClockValue(Book, Operation.Action,
					SlotForGrowthAction(Operation.Action) == KingdomGrowthSlotKind.Field
						? FindGrowthField(Book, Operation.FieldId) : null))
				return KingdomLifecycleCasAction.Quarantine;
			if (Operation.ClockState == KingdomLifecyclePhysicalState.Prepared
				&& lease.State == KingdomLifecycleLeaseState.Prepared)
				return CurrentValue == lease.Before && row.Revision == lease.BeforeRevision
					&& !string.Equals(row.LastOperationId, Operation.Id, StringComparison.Ordinal)
					? KingdomLifecycleCasAction.Apply : KingdomLifecycleCasAction.Quarantine;
			if ((Operation.ClockState == KingdomLifecyclePhysicalState.Intent
				|| Operation.ClockState == KingdomLifecyclePhysicalState.Proved)
				&& (lease.State == KingdomLifecycleLeaseState.Intent
					|| lease.State == KingdomLifecycleLeaseState.Proved))
				return CurrentValue == lease.After && (row.Revision == lease.BeforeRevision
					|| row.Revision == lease.AfterRevision) ? KingdomLifecycleCasAction.Confirm
					: KingdomLifecycleCasAction.Quarantine;
			return KingdomLifecycleCasAction.Quarantine;
		}

		internal static bool BeginGrowthClock(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, long CurrentValue)
		{
			if (GrowthClockAction(Book, Operation, CurrentValue) != KingdomLifecycleCasAction.Apply)
				return false;
			Operation.ClockState = KingdomLifecyclePhysicalState.Intent;
			Operation.ClockLease.State = KingdomLifecycleLeaseState.Intent;
			return true;
		}

		internal static bool CommitGrowthClockWitness(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, long CurrentValue)
		{
			if (GrowthClockAction(Book, Operation, CurrentValue) != KingdomLifecycleCasAction.Confirm
				|| Operation.ClockState != KingdomLifecyclePhysicalState.Intent
				|| Operation.ClockLease.State != KingdomLifecycleLeaseState.Intent) return false;
			KingdomLifecycleResourceLease lease = Operation.ClockLease;
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, lease.Key);
			if (row.Revision != lease.BeforeRevision
				|| string.Equals(row.LastOperationId, Operation.Id, StringComparison.Ordinal)) return false;
			long rowRevisionBefore = row.Revision;
			string rowLastOperationBefore = row.LastOperationId;
			KingdomLifecycleLeaseState leaseStateBefore = lease.State;
			KingdomLifecyclePhysicalState clockStateBefore = Operation.ClockState;
			long heartbeatBefore = Book.LastHeartbeatTick;
			long arrivalBefore = Book.NextArrivalTick;
			long fetchBefore = Book.LastFetchTick;
			long millBefore = Book.LastMillTick;
			long subsidenceBefore = Book.LastSubsidenceTick;
			long deliveryBefore = Book.LastDeliveryTick;
			long departureBefore = Book.LastDepartureTick;
			long effectiveBefore = Book.EffectiveWorkTick;
			KingdomGrowthFieldSlot field = SlotForGrowthAction(Operation.Action) ==
				KingdomGrowthSlotKind.Field ? FindGrowthField(Book, Operation.FieldId) : null;
			long fieldClockBefore = field == null ? 0L : field.ClockTick;
			long fieldRevisionBefore = field == null ? 0L : field.CommitRevision;
			string fieldLastOperationBefore = field == null ? null : field.LastOperationId;
			row.Revision = lease.AfterRevision; row.LastOperationId = Operation.Id;
			lease.State = KingdomLifecycleLeaseState.Proved;
			Operation.ClockState = KingdomLifecyclePhysicalState.Proved;
			ApplyGrowthClockValue(Book, Operation);
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			row.Revision = rowRevisionBefore; row.LastOperationId = rowLastOperationBefore;
			lease.State = leaseStateBefore; Operation.ClockState = clockStateBefore;
			Book.LastHeartbeatTick = heartbeatBefore; Book.NextArrivalTick = arrivalBefore;
			Book.LastFetchTick = fetchBefore; Book.LastMillTick = millBefore;
			Book.LastSubsidenceTick = subsidenceBefore;
			Book.LastDeliveryTick = deliveryBefore; Book.LastDepartureTick = departureBefore;
			Book.EffectiveWorkTick = effectiveBefore;
			if (field != null)
			{
				field.ClockTick = fieldClockBefore;
				field.CommitRevision = fieldRevisionBefore;
				field.LastOperationId = fieldLastOperationBefore;
			}
			return false;
		}

	}
}
