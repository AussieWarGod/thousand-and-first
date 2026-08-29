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
		internal static KingdomGrowthOutboxEvent PrepareDeclaredGrowthOutboxEvent(
			KingdomGrowthOperation Operation, int Ordinal, string Kind, string Chronicle,
			string ChronicleOfficial, string ChronicleOutsider, string Ledger, string Message,
			string Deed, string Guestbook,
			int ChronicleBeforeCount, string ChronicleBeforeHash,
			int ChronicleDeclaredAfterCount, string ChronicleDeclaredAfterHash,
			int OutsiderBeforeCount, string OutsiderBeforeHash,
			int OutsiderDeclaredAfterCount, string OutsiderDeclaredAfterHash,
			int LedgerBeforeCount, string LedgerBeforeHash,
			int LedgerDeclaredAfterCount, string LedgerDeclaredAfterHash)
		{
			if (Chronicle != null && Chronicle.Length == 0) Chronicle = null;
			if (Ledger != null && Ledger.Length == 0) Ledger = null;
			if (Message != null && Message.Length == 0) Message = null;
			if (Deed != null && Deed.Length == 0) Deed = null;
			if (Guestbook != null && Guestbook.Length == 0) Guestbook = null;
			if (Operation == null || Ordinal < 0 || Ordinal >= MaxGrowthOutboxEvents
				|| !ValidName(Kind)
				|| (Chronicle == null ? ChronicleOfficial != null || ChronicleOutsider != null
					: string.IsNullOrEmpty(ChronicleOfficial)
						|| ChronicleOfficial.Length > KingdomChronicleReceiptRules.MaxEntryChars
						|| string.IsNullOrEmpty(ChronicleOutsider)
						|| ChronicleOutsider.Length > KingdomChronicleReceiptRules.MaxEntryChars)
				|| !GrowthSinkDeclarationShape(Chronicle, ChronicleBeforeCount,
					ChronicleBeforeHash, ChronicleDeclaredAfterCount,
					ChronicleDeclaredAfterHash, KingdomChronicleReceiptRules.MaxEntries)
				|| !GrowthSinkDeclarationShape(Chronicle, OutsiderBeforeCount,
					OutsiderBeforeHash, OutsiderDeclaredAfterCount,
					OutsiderDeclaredAfterHash, KingdomChronicleReceiptRules.MaxEntries)
				|| !GrowthSinkDeclarationShape(Ledger, LedgerBeforeCount, LedgerBeforeHash,
					LedgerDeclaredAfterCount, LedgerDeclaredAfterHash)) return null;
			KingdomLifecycleOutbox box = PrepareGrowthOutbox(Operation, Chronicle, Ledger,
				Message, Deed, Guestbook);
			if (box == null) return null;
			string eventId = ChildId(Operation.Id, "outbox-event", Ordinal);
			box.EventId = eventId;
			box.ChronicleReceiptId = ChildId(eventId, "chronicle", 0);
			return new KingdomGrowthOutboxEvent
			{
				EventId = eventId, Kind = Kind, Outbox = box,
				ChronicleBeforeCount = ChronicleBeforeCount,
				ChronicleBeforeHash = ChronicleBeforeHash,
				ChronicleDeclaredAfterCount = ChronicleDeclaredAfterCount,
				ChronicleDeclaredAfterHash = ChronicleDeclaredAfterHash,
				ChronicleOfficial = ChronicleOfficial,
				ChronicleOutsider = ChronicleOutsider,
				OutsiderBeforeCount = OutsiderBeforeCount,
				OutsiderBeforeHash = OutsiderBeforeHash,
				OutsiderDeclaredAfterCount = OutsiderDeclaredAfterCount,
				OutsiderDeclaredAfterHash = OutsiderDeclaredAfterHash,
				LedgerBeforeCount = LedgerBeforeCount, LedgerBeforeHash = LedgerBeforeHash,
				LedgerDeclaredAfterCount = LedgerDeclaredAfterCount,
				LedgerDeclaredAfterHash = LedgerDeclaredAfterHash
			};
		}

		private static bool GrowthSinkDeclarationShape(string text, int beforeCount,
			string beforeHash, int afterCount, string afterHash, int boundedCount = -1)
		{
			if (text == null) return beforeCount == 0 && afterCount == 0
				&& beforeHash == null && afterHash == null;
			bool countShape = boundedCount < 0
				? beforeCount >= 0 && beforeCount < int.MaxValue
					&& afterCount == beforeCount + 1
				: beforeCount >= 0 && beforeCount <= boundedCount
					&& (beforeCount < boundedCount ? afterCount == beforeCount + 1
						: afterCount == beforeCount);
			return countShape && GrowthWitnessHash(beforeHash)
				&& GrowthWitnessHash(afterHash)
				&& !string.Equals(beforeHash, afterHash, StringComparison.Ordinal);
		}

		public static bool TryGrowthPlanHash(KingdomGrowthOperation Operation, out string Hash)
		{
			Hash = null;
			if (Operation == null) return false;
			try
			{
				Hash = HashId("growth-plan", delegate(BinaryWriter w)
				{
					w.Write(Operation.Sequence); CanonicalString(w, Operation.Id);
					w.Write((byte)Operation.Action); w.Write(Operation.CreatedTick);
					CanonicalString(w, Operation.SettlementId);
					CanonicalString(w, Operation.FieldId); CanonicalString(w, Operation.ZoneId);
					CanonicalString(w, Operation.TargetId); CanonicalString(w, Operation.TargetMarker);
					CanonicalString(w, Operation.Blueprint); w.Write((byte)Operation.TargetTopology);
					w.Write((byte)Operation.TargetLocation);
					CanonicalString(w, Operation.TargetOwnerId); w.Write(Operation.TargetX);
					w.Write(Operation.TargetY); w.Write((byte)Operation.OptionState);
					w.Write(Operation.OptionTick); w.Write((byte)Operation.HealthState);
					w.Write(Operation.HealthTick); w.Write(Operation.EffectiveWorkBefore);
					w.Write(Operation.EffectiveWorkAfter); w.Write(Operation.FieldClockBefore);
					w.Write(Operation.FieldClockAfter); w.Write(Operation.HeartbeatBefore);
					w.Write(Operation.HeartbeatAfter); w.Write(Operation.ArrivalBefore);
					w.Write(Operation.ArrivalAfter); w.Write(Operation.FetchBefore);
					w.Write(Operation.FetchAfter); w.Write(Operation.MillBefore);
					w.Write(Operation.MillAfter);
					CanonicalString(w, Operation.MillCropBlueprint);
					CanonicalString(w, Operation.MillStapleBlueprint);
					w.Write(Operation.SubsidenceBefore);
					w.Write(Operation.SubsidenceAfter); w.Write(Operation.DeliveryBefore);
					w.Write(Operation.DeliveryAfter); w.Write(Operation.DepartureBefore);
					w.Write(Operation.DepartureAfter); w.Write((byte)Operation.ArrivalDisposition);
					CanonicalString(w, Operation.ArrivalCandidateId);
					if (Operation.ArrivalOpportunityOrdinal != 0UL)
					{
						CanonicalString(w, "arrival-opportunity-v1");
						w.Write(Operation.ArrivalOpportunityOrdinal);
						w.Write(Operation.ArrivalOpportunityDueTick);
						w.Write(Operation.ArrivalOpportunityRateEpoch);
						CanonicalString(w, Operation.ArrivalOpportunityPayloadHash);
					}
					w.Write((byte)Operation.DeliveryMode);
					w.Write((byte)Operation.DepartureCauseKind);
					CanonicalString(w, Operation.DepartureCause);
					CanonicalString(w, Operation.DepartureNote);
					CanonicalString(w, Operation.DepartureName);
					CanonicalString(w, Operation.DepartureOrigin);
					w.Write(Operation.DepartureArrivedTick);
					CanonicalString(w, Operation.DepartureCreed);
					w.Write(Operation.DepartureChronicled);
					CanonicalString(w, Operation.TriggeredByOperationId);
					w.Write((byte)Operation.ScarcityOptionState);
					w.Write(Operation.ScarcityOptionTick); w.Write(Operation.PendingCropBefore);
					w.Write(Operation.PendingCropDelta); w.Write(Operation.PendingCropAfter);
					CanonicalString(w, Operation.PendingCropBlueprintBefore);
					CanonicalString(w, Operation.PendingCropZoneIdBefore);
					CanonicalString(w, Operation.PendingCropBlueprintAfter);
					CanonicalString(w, Operation.PendingCropZoneIdAfter);
					w.Write(Operation.PopulationBefore); w.Write(Operation.PopulationDelta);
					w.Write(Operation.PopulationAfter);
					w.Write(Operation.HarvestStandingRows);
					w.Write(Operation.HarvestRipeRows); w.Write(Operation.HarvestCycles);
					w.Write(Operation.HarvestCountsRipeLast);
					w.Write(Operation.HarvestEffectivenessPercent);
					w.Write(Operation.HarvestMethodPercent);
					w.Write(Operation.HarvestFirstOrdinal);
					CanonicalString(w, Operation.HarvestCropBlueprint);
					CanonicalString(w, Operation.HarvestSeedBlueprint);
					w.Write(Operation.WaterLegs == null ? -1 : Operation.WaterLegs.Count);
					if (Operation.WaterLegs != null) for (int i = 0;
						i < Operation.WaterLegs.Count; i++) WriteGrowthWaterPlan(w,
							Operation.WaterLegs[i]);
					w.Write(Operation.Sources == null ? -1 : Operation.Sources.Count);
					if (Operation.Sources != null) for (int i = 0; i < Operation.Sources.Count; i++)
						WriteGrowthObjectPlan(w, Operation.Sources[i]);
					w.Write(Operation.Outputs == null ? -1 : Operation.Outputs.Count);
					if (Operation.Outputs != null) for (int i = 0; i < Operation.Outputs.Count; i++)
						WriteGrowthObjectPlan(w, Operation.Outputs[i]);
					w.Write(Operation.DomainSteps == null ? -1 : Operation.DomainSteps.Count);
					if (Operation.DomainSteps != null) for (int i = 0;
						i < Operation.DomainSteps.Count; i++) WriteGrowthDomainPlan(w,
							Operation.DomainSteps[i]);
					WriteLeasePlan(w, Operation.ClockLease);
					w.Write(Operation.OutboxEvents == null ? -1 : Operation.OutboxEvents.Count);
					if (Operation.OutboxEvents != null) for (int i = 0;
						i < Operation.OutboxEvents.Count; i++) WriteGrowthOutboxEventPlan(w,
							Operation.OutboxEvents[i], Operation.LegacyGrowthV1Plan);
				});
				return ValidHashNamespace(Hash, "growth-plan");
			}
			catch (Exception)
			{
				Hash = null;
				return false;
			}
		}

		private static void WriteGrowthOutboxEventPlan(BinaryWriter w,
			KingdomGrowthOutboxEvent x, bool legacyV1 = false)
		{
			CanonicalString(w, x.EventId); CanonicalString(w, x.Kind);
			w.Write(x.ChronicleBeforeCount); w.Write(x.ChronicleDeclaredAfterCount);
			CanonicalString(w, x.ChronicleBeforeHash);
			CanonicalString(w, x.ChronicleDeclaredAfterHash);
			if (!legacyV1)
			{
				w.Write(x.LegacySingleRegisterChronicle);
				CanonicalString(w, x.ChronicleOfficial);
				CanonicalString(w, x.ChronicleOutsider);
				w.Write(x.OutsiderBeforeCount); w.Write(x.OutsiderDeclaredAfterCount);
				CanonicalString(w, x.OutsiderBeforeHash);
				CanonicalString(w, x.OutsiderDeclaredAfterHash);
			}
			w.Write(x.LedgerBeforeCount); w.Write(x.LedgerDeclaredAfterCount);
			CanonicalString(w, x.LedgerBeforeHash);
			CanonicalString(w, x.LedgerDeclaredAfterHash);
			WriteOutboxPlan(w, x.Outbox);
		}

		private static void WriteGrowthWaterPlan(BinaryWriter w, KingdomGrowthWaterLeg x)
		{
			CanonicalString(w, x.OperationId); CanonicalString(w, x.EventId);
			CanonicalString(w, x.LeaseKey); w.Write((byte)x.MutationKind);
			w.Write((byte)x.ContainerKind); CanonicalString(w, x.ContainerId);
			w.Write((byte)x.BeforeLocation); w.Write((byte)x.AfterLocation);
			CanonicalString(w, x.BeforeOwnerId); CanonicalString(w, x.AfterOwnerId);
			CanonicalString(w, x.BeforeZoneId); CanonicalString(w, x.AfterZoneId);
			w.Write(x.BeforeX); w.Write(x.BeforeY); w.Write(x.AfterX); w.Write(x.AfterY);
			w.Write(x.OwnerRemovedAfter);
			w.Write((byte)x.OwnerTopology); CanonicalString(w, x.OwnerId);
			CanonicalString(w, x.Blueprint); CanonicalString(w, x.ZoneId); w.Write(x.X);
			w.Write(x.Y); w.Write(x.Capacity); w.Write(x.Before); w.Write(x.Delta);
			w.Write(x.After); CanonicalString(w, x.BeforeComposition);
			CanonicalString(w, x.AfterComposition);
			CanonicalString(w, x.BeforeOwnerGraphHash); CanonicalString(w, x.AfterOwnerGraphHash);
			CanonicalString(w, x.BeforePartGraphHash); CanonicalString(w, x.AfterPartGraphHash);
			CanonicalString(w, x.BeforeTopologyHash); CanonicalString(w, x.AfterTopologyHash);
			CanonicalString(w, x.ReceiptId); WriteLeasePlan(w, x.Lease);
		}

		private static void WriteGrowthObjectPlan(BinaryWriter w, KingdomGrowthObjectLeg x)
		{
			CanonicalString(w, x.OperationId); CanonicalString(w, x.EventId);
			CanonicalString(w, x.MutationKind == KingdomGrowthObjectMutationKind.Create
				? null : x.ObjectId); CanonicalString(w, x.Marker);
			CanonicalString(w, x.Blueprint); CanonicalString(w, x.ZoneId);
			w.Write((byte)x.Topology); CanonicalString(w, x.OwnerId); w.Write(x.X); w.Write(x.Y);
			w.Write(x.BeforeCount); w.Write(x.Delta); w.Write(x.AfterCount); w.Write(x.NoStack);
			w.Write((byte)x.MutationKind); CanonicalString(w, x.BeforeOwnerGraphHash);
			CanonicalString(w, x.MutationKind == KingdomGrowthObjectMutationKind.Create
				? null : x.AfterOwnerGraphHash); CanonicalString(w, x.BeforeObjectGraphHash);
			CanonicalString(w, x.MutationKind == KingdomGrowthObjectMutationKind.Create
				? null : x.AfterObjectGraphHash); CanonicalString(w, x.BeforeTopologyHash);
			CanonicalString(w, x.MutationKind == KingdomGrowthObjectMutationKind.Create
				? null : x.AfterTopologyHash); CanonicalString(w, x.CreatedMarker);
			CanonicalString(w, x.DetachedMarker); CanonicalString(w, x.ReceiptId);
			CanonicalString(w, x.ReceiptTopologyId); w.Write((byte)x.BeforeLocation);
			w.Write((byte)x.AfterLocation); CanonicalString(w, x.EscrowKey);
			w.Write(x.Callbacks == null ? -1 : x.Callbacks.Count);
			if (x.Callbacks != null) for (int i = 0; i < x.Callbacks.Count; i++)
				WriteGrowthObjectCallbackPlan(w, x.Callbacks[i]);
			WriteLeasePlan(w, x.Lease);
		}

		private static void WriteGrowthObjectCallbackPlan(BinaryWriter w,
			KingdomGrowthObjectCallbackStep x)
		{
			CanonicalString(w, x.EventId); w.Write((byte)x.Kind); w.Write((byte)x.FromLocation);
			w.Write((byte)x.ToLocation); CanonicalString(w, x.EscrowKey);
			CanonicalString(w, x.BeforeOwnerId); CanonicalString(w, x.AfterOwnerId);
			CanonicalString(w, x.BeforeZoneId); CanonicalString(w, x.AfterZoneId);
			w.Write(x.BeforeX); w.Write(x.BeforeY); w.Write(x.AfterX); w.Write(x.AfterY);
			w.Write(x.BeforeCount); w.Write(x.AfterCount); w.Write(x.NoStack);
			w.Write(x.BeforeHasHarvestable); w.Write(x.AfterHasHarvestable);
			w.Write(x.BeforeRipe); w.Write(x.AfterRipe);
			w.Write(x.BeforeRegenTimer); w.Write(x.AfterRegenTimer);
			CanonicalString(w, x.BeforeRegenTime); CanonicalString(w, x.AfterRegenTime);
			w.Write(x.BeforeTileIndex); w.Write(x.AfterTileIndex);
			CanonicalString(w, x.BeforeRenderTile); CanonicalString(w, x.AfterRenderTile);
			CanonicalString(w, x.BeforeRenderColor); CanonicalString(w, x.AfterRenderColor);
			CanonicalString(w, x.BeforeRenderDetail); CanonicalString(w, x.AfterRenderDetail);
			CanonicalString(w, x.BeforeRenderString); CanonicalString(w, x.AfterRenderString);
			CanonicalString(w, x.BeforeTileColor); CanonicalString(w, x.AfterTileColor);
			// Callback graph witnesses are frozen immediately before each callback. They are
			// deliberately outside the operation plan because Create/replacement determines the
			// exact object graph and later placement witnesses only after the created ref exists.
			CanonicalString(w, x.ReceiptId);
		}

		private static void WriteGrowthDomainPlan(BinaryWriter w, KingdomGrowthDomainStep x)
		{
			w.Write((byte)x.Kind); w.Write((byte)x.CallbackKind);
			CanonicalString(w, x.CallbackBodyHash); CanonicalString(w, x.EventId);
			CanonicalString(w, x.ActorId);
			CanonicalString(w, x.SubjectId); w.Write(x.BeforeValue); w.Write(x.AfterValue);
			CanonicalString(w, x.BeforeGraphHash); CanonicalString(w, x.AfterGraphHash);
			CanonicalString(w, x.BeforeMapHash); CanonicalString(w, x.AfterMapHash);
			CanonicalString(w, x.ReceiptId); WriteLeasePlan(w, x.Lease);
			WriteGrowthScarcityPlan(w, x.ScarcityBefore);
			WriteGrowthScarcityPlan(w, x.ScarcityAfter);
			WriteGrowthAccountingPlan(w, x.AccountingBefore);
			WriteGrowthAccountingPlan(w, x.AccountingAfter);
			WriteGrowthFieldStatePlan(w, x.FieldBefore);
			WriteGrowthFieldStatePlan(w, x.FieldAfter);
			WriteGrowthCropRowsPlan(w, x.CropRowsBefore);
			WriteGrowthCropRowsPlan(w, x.CropRowsDeclaredAfter);
		}

	}
}
