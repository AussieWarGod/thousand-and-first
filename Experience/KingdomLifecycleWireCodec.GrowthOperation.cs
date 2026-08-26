using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{

		private static void WriteGrowthOperation(BinaryWriter w, KingdomGrowthOperation o,
			int wireVersion)
		{
			w.Write(o != null); if (o == null) return;
			EnsureCount(o.WaterLegs, KingdomLifecycleRules.MaxWaterLegs, "growth water legs");
			EnsureCount(o.Sources, KingdomLifecycleRules.MaxGrowthSources, "growth sources");
			EnsureCount(o.Outputs, KingdomLifecycleRules.MaxGrowthOutputs, "growth outputs");
			EnsureCount(o.DomainSteps, KingdomLifecycleRules.MaxResourceLeases,
				"growth domain leases");
			EnsureCount(o.OutboxEvents, KingdomLifecycleRules.MaxGrowthOutboxEvents,
				"growth outbox events");
			w.Write(o.Sequence); S(w, o.Id, true); S(w, o.PlanHash, true);
			if (wireVersion >= KingdomLifecycleRules.PreviousGrowthFormatVersion)
				w.Write(o.LegacyGrowthV1Plan);
			w.Write((byte)o.Action); w.Write((byte)o.Phase); w.Write(o.CreatedTick);
			w.Write(o.UpdatedTick); S(w, o.SettlementId, true); S(w, o.FieldId, true);
			S(w, o.ZoneId, false); S(w, o.TargetId, true); S(w, o.TargetMarker, true);
			S(w, o.Blueprint, false); w.Write((byte)o.TargetTopology);
			w.Write((byte)o.TargetLocation);
			S(w, o.TargetOwnerId, true); w.Write(o.TargetX); w.Write(o.TargetY);
			w.Write((byte)o.OptionState); w.Write(o.OptionTick); w.Write((byte)o.HealthState);
			w.Write(o.HealthTick); w.Write(o.EffectiveWorkBefore); w.Write(o.EffectiveWorkAfter);
			w.Write(o.FieldClockBefore); w.Write(o.FieldClockAfter);
			w.Write(o.HeartbeatBefore); w.Write(o.HeartbeatAfter); w.Write(o.ArrivalBefore);
			w.Write(o.ArrivalAfter); w.Write(o.FetchBefore); w.Write(o.FetchAfter);
			w.Write(o.MillBefore); w.Write(o.MillAfter); S(w, o.MillCropBlueprint, false);
			S(w, o.MillStapleBlueprint, false); w.Write(o.SubsidenceBefore);
			w.Write(o.SubsidenceAfter); w.Write(o.DeliveryBefore); w.Write(o.DeliveryAfter);
			w.Write(o.DepartureBefore); w.Write(o.DepartureAfter);
			w.Write((byte)o.ArrivalDisposition); S(w, o.ArrivalCandidateId, true);
			w.Write((byte)o.DeliveryMode); w.Write((byte)o.DepartureCauseKind);
			S(w, o.DepartureCause, false); S(w, o.DepartureNote, false, true);
			S(w, o.DepartureName, false); S(w, o.DepartureOrigin, false);
			w.Write(o.DepartureArrivedTick); S(w, o.DepartureCreed, false);
			w.Write(o.DepartureChronicled); S(w, o.TriggeredByOperationId, true);
			w.Write((byte)o.ScarcityOptionState); w.Write(o.ScarcityOptionTick);
			w.Write(o.PendingCropBefore);
			w.Write(o.PendingCropDelta); w.Write(o.PendingCropAfter);
			S(w, o.PendingCropBlueprintBefore, false); S(w, o.PendingCropZoneIdBefore, false);
			S(w, o.PendingCropBlueprintAfter, false); S(w, o.PendingCropZoneIdAfter, false);
			w.Write(o.PopulationBefore); w.Write(o.PopulationDelta); w.Write(o.PopulationAfter);
			w.Write(o.HarvestStandingRows); w.Write(o.HarvestRipeRows);
			w.Write(o.HarvestCycles); w.Write(o.HarvestCountsRipeLast);
			w.Write(o.HarvestEffectivenessPercent); w.Write(o.HarvestMethodPercent);
			w.Write(o.HarvestFirstOrdinal);
			S(w, o.HarvestCropBlueprint, false); S(w, o.HarvestSeedBlueprint, false);
			w.Write(o.WaterCursor); w.Write(o.WaterLegs.Count);
			for (int i = 0; i < o.WaterLegs.Count; i++) WriteGrowthWater(w, o.WaterLegs[i]);
			w.Write(o.SourceCursor); w.Write(o.Sources.Count);
			for (int i = 0; i < o.Sources.Count; i++) WriteGrowthObject(w, o.Sources[i]);
			w.Write(o.OutputCursor); w.Write(o.Outputs.Count);
			for (int i = 0; i < o.Outputs.Count; i++) WriteGrowthObject(w, o.Outputs[i]);
			w.Write(o.DomainCursor); w.Write(o.DomainSteps.Count);
			for (int i = 0; i < o.DomainSteps.Count; i++)
			{
				if (o.DomainSteps[i] == null)
					throw new InvalidDataException("null growth domain step");
				KingdomGrowthDomainStep d = o.DomainSteps[i];
				w.Write((byte)d.Kind); w.Write((byte)d.CallbackKind);
				S(w, d.CallbackBodyHash, true); S(w, d.EventId, true); S(w, d.ActorId, true);
				S(w, d.SubjectId, true); w.Write(d.BeforeValue); w.Write(d.AfterValue);
				S(w, d.BeforeGraphHash, true); S(w, d.AfterGraphHash, true);
				S(w, d.BeforeMapHash, true); S(w, d.AfterMapHash, true);
				w.Write((byte)d.State); S(w, d.ReceiptId, true);
				w.Write(d.ReceiptBeforeValue); w.Write(d.ReceiptAfterValue);
				S(w, d.ReceiptBeforeGraphHash, true); S(w, d.ReceiptAfterGraphHash, true);
				S(w, d.ReceiptBeforeMapHash, true); S(w, d.ReceiptAfterMapHash, true);
				S(w, d.ReceiptProofId, true); w.Write((byte)d.ReceiptState);
				WriteLease(w, d.Lease); WriteGrowthScarcity(w, d.ScarcityBefore);
				WriteGrowthScarcity(w, d.ScarcityAfter);
				WriteGrowthAccounting(w, d.AccountingBefore);
				WriteGrowthAccounting(w, d.AccountingAfter);
				WriteGrowthFieldState(w, d.FieldBefore);
				WriteGrowthFieldState(w, d.FieldAfter);
				WriteGrowthCropRows(w, d.CropRowsBefore);
				WriteGrowthCropRows(w, d.CropRowsDeclaredAfter);
				WriteGrowthCropRows(w, d.CropRowsAfter);
			}
			WriteLease(w, o.ClockLease);
			w.Write((byte)o.ClockState);
			w.Write(o.OutboxEvents.Count);
			for (int i = 0; i < o.OutboxEvents.Count; i++) WriteGrowthOutboxEvent(w,
				o.OutboxEvents[i], wireVersion);
			S(w, o.Fault, false, true);
		}

		private static KingdomGrowthOperation ReadGrowthOperation(BinaryReader r,
			int wireVersion)
		{
			if (!ReadExactBoolean(r)) return null;
			long sequence = r.ReadInt64();
			string id = S(r, true);
			string planHash = S(r, true);
			bool legacyV1 = wireVersion == KingdomLifecycleRules.LegacyGrowthFormatVersion
				|| ReadExactBoolean(r);
			KingdomGrowthOperation o = new KingdomGrowthOperation
			{
				Sequence = sequence, Id = id, PlanHash = planHash,
				LegacyGrowthV1Plan = legacyV1,
				Action = (KingdomGrowthAction)r.ReadByte(), Phase = (KingdomGrowthPhase)r.ReadByte(),
				CreatedTick = r.ReadInt64(), UpdatedTick = r.ReadInt64(), SettlementId = S(r, true),
				FieldId = S(r, true), ZoneId = S(r, false), TargetId = S(r, true),
				TargetMarker = S(r, true), Blueprint = S(r, false),
				TargetTopology = (KingdomLifecycleTopology)r.ReadByte(),
				TargetLocation = (KingdomGrowthLocationKind)r.ReadByte(), TargetOwnerId = S(r, true),
				TargetX = r.ReadInt32(), TargetY = r.ReadInt32(),
				OptionState = (KingdomLifecycleOptionState)r.ReadByte(), OptionTick = r.ReadInt64(),
				HealthState = (KingdomGrowthHealthState)r.ReadByte(), HealthTick = r.ReadInt64(),
				EffectiveWorkBefore = r.ReadInt64(), EffectiveWorkAfter = r.ReadInt64(),
				FieldClockBefore = r.ReadInt64(), FieldClockAfter = r.ReadInt64(),
				HeartbeatBefore = r.ReadInt64(), HeartbeatAfter = r.ReadInt64(),
				ArrivalBefore = r.ReadInt64(), ArrivalAfter = r.ReadInt64(),
				FetchBefore = r.ReadInt64(), FetchAfter = r.ReadInt64(),
				MillBefore = r.ReadInt64(), MillAfter = r.ReadInt64(),
				MillCropBlueprint = S(r, false), MillStapleBlueprint = S(r, false),
				SubsidenceBefore = r.ReadInt64(), SubsidenceAfter = r.ReadInt64(),
				DeliveryBefore = r.ReadInt64(), DeliveryAfter = r.ReadInt64(),
				DepartureBefore = r.ReadInt64(), DepartureAfter = r.ReadInt64(),
				ArrivalDisposition = (KingdomGrowthArrivalDisposition)r.ReadByte(),
				ArrivalCandidateId = S(r, true),
				DeliveryMode = (KingdomGrowthDeliveryMode)r.ReadByte(),
				DepartureCauseKind = (KingdomGrowthDepartureCauseKind)r.ReadByte(),
				DepartureCause = S(r, false), DepartureNote = S(r, false, true),
				DepartureName = S(r, false), DepartureOrigin = S(r, false),
				DepartureArrivedTick = r.ReadInt64(), DepartureCreed = S(r, false),
				DepartureChronicled = ReadExactBoolean(r), TriggeredByOperationId = S(r, true),
				ScarcityOptionState = (KingdomLifecycleOptionState)r.ReadByte(),
				ScarcityOptionTick = r.ReadInt64(),
				PendingCropBefore = r.ReadInt32(), PendingCropDelta = r.ReadInt32(),
				PendingCropAfter = r.ReadInt32(), PendingCropBlueprintBefore = S(r, false),
				PendingCropZoneIdBefore = S(r, false), PendingCropBlueprintAfter = S(r, false),
				PendingCropZoneIdAfter = S(r, false), PopulationBefore = r.ReadInt32(),
				PopulationDelta = r.ReadInt32(), PopulationAfter = r.ReadInt32(),
				HarvestStandingRows = r.ReadInt32(), HarvestRipeRows = r.ReadInt32(),
				HarvestCycles = r.ReadInt32(), HarvestCountsRipeLast = ReadExactBoolean(r),
				HarvestEffectivenessPercent = r.ReadInt32(),
				HarvestMethodPercent = r.ReadInt32(),
				HarvestFirstOrdinal = r.ReadUInt64(),
				HarvestCropBlueprint = S(r, false), HarvestSeedBlueprint = S(r, false)
			};
			o.WaterCursor = r.ReadInt32();
			int water = ReadCount(r, KingdomLifecycleRules.MaxWaterLegs);
			o.WaterLegs = new List<KingdomGrowthWaterLeg>(water);
			for (int i = 0; i < water; i++) o.WaterLegs.Add(ReadGrowthWater(r));
			o.SourceCursor = r.ReadInt32();
			int sources = ReadCount(r, KingdomLifecycleRules.MaxGrowthSources);
			o.Sources = new List<KingdomGrowthObjectLeg>(sources);
			for (int i = 0; i < sources; i++) o.Sources.Add(ReadGrowthObject(r));
			o.OutputCursor = r.ReadInt32();
			int outputs = ReadCount(r, KingdomLifecycleRules.MaxGrowthOutputs);
			o.Outputs = new List<KingdomGrowthObjectLeg>(outputs);
			for (int i = 0; i < outputs; i++) o.Outputs.Add(ReadGrowthObject(r));
			o.DomainCursor = r.ReadInt32();
			int leases = ReadCount(r, KingdomLifecycleRules.MaxResourceLeases);
			o.DomainSteps = new List<KingdomGrowthDomainStep>(leases);
			for (int i = 0; i < leases; i++) o.DomainSteps.Add(new KingdomGrowthDomainStep
			{
				Kind = (KingdomGrowthDomainStepKind)r.ReadByte(),
				CallbackKind = (KingdomGrowthDomainCallbackKind)r.ReadByte(),
				CallbackBodyHash = S(r, true), EventId = S(r, true),
				ActorId = S(r, true), SubjectId = S(r, true), BeforeValue = r.ReadInt64(),
				AfterValue = r.ReadInt64(), BeforeGraphHash = S(r, true),
				AfterGraphHash = S(r, true), BeforeMapHash = S(r, true),
				AfterMapHash = S(r, true),
				State = (KingdomLifecyclePhysicalState)r.ReadByte(), ReceiptId = S(r, true),
				ReceiptBeforeValue = r.ReadInt64(), ReceiptAfterValue = r.ReadInt64(),
				ReceiptBeforeGraphHash = S(r, true), ReceiptAfterGraphHash = S(r, true),
				ReceiptBeforeMapHash = S(r, true), ReceiptAfterMapHash = S(r, true),
				ReceiptProofId = S(r, true),
				ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte(), Lease = ReadLease(r),
				ScarcityBefore = ReadGrowthScarcity(r), ScarcityAfter = ReadGrowthScarcity(r),
				AccountingBefore = ReadGrowthAccounting(r),
				AccountingAfter = ReadGrowthAccounting(r),
				FieldBefore = ReadGrowthFieldState(r), FieldAfter = ReadGrowthFieldState(r),
				CropRowsBefore = ReadGrowthCropRows(r),
				CropRowsDeclaredAfter = ReadGrowthCropRows(r),
				CropRowsAfter = ReadGrowthCropRows(r)
			});
			o.ClockLease = ReadLease(r);
			o.ClockState = (KingdomLifecyclePhysicalState)r.ReadByte();
			int outbox = ReadCount(r, KingdomLifecycleRules.MaxGrowthOutboxEvents);
			o.OutboxEvents = new List<KingdomGrowthOutboxEvent>(outbox);
			for (int i = 0; i < outbox; i++)
				o.OutboxEvents.Add(ReadGrowthOutboxEvent(r, wireVersion));
			o.Fault = S(r, false, true); return o;
		}

	}
}
