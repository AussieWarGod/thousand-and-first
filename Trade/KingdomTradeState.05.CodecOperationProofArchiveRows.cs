using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	public static partial class KingdomTradeCodec
	{

		private static void WriteOperation(BinaryWriter w, KingdomTradeOperation x)
		{
			if (x == null || (x.Kind == KingdomTradeOperationKind.CharterDelivery)
				!= (x.Pattern != null))
				throw new InvalidDataException("Trade operation pattern lane does not match its kind.");
			w.Write(x.Sequence); WriteString(w, x.Id); w.Write((byte)x.Kind); w.Write((byte)x.Phase);
			w.Write(x.CreatedTick); w.Write(x.UpdatedTick); WriteString(w, x.ZoneId); WriteString(w, x.SettlementId);
			WriteString(w, x.SettlementName); WriteString(w, x.CharterId); WriteString(w, x.ManifestId);
			WriteString(w, x.DealKey); WriteString(w, x.DealDisplayName); WriteString(w, x.Faction);
			w.Write(x.Cycles); w.Write(x.IncomePerCycle); w.Write(x.IntervalTicks); w.Write(x.DueBefore);
			w.Write(x.DueAfter); WriteString(w, x.CaravanBlueprint); WriteString(w, x.ProjectionId);
			WriteString(w, x.ProjectionObjectId); w.Write(x.ProjectionX); w.Write(x.ProjectionY);
			WriteString(w, x.PriorProjectionId); WriteString(w, x.PriorProjectionObjectId);
			WriteString(w, x.PriorProjectionZoneId); w.Write((byte)x.ProjectionState);
			w.Write((byte)x.PriorCleanupState); w.Write((byte)x.WaterDirection); w.Write(x.RequestedWater);
			w.Write(x.ProvedWater); w.Write(x.AmbiguousWater);
			WriteList(w, x.WaterLegs, KingdomTradeRules.MaxWaterLegs, WriteWater);
			WriteString(w, x.MaterialClaim); w.Write(x.MaterialRequested); w.Write(x.MaterialProved);
			WriteList(w, x.MaterialOutputs, KingdomTradeRules.MaxMaterialOutputs, WriteMaterial);
			WriteString(w, x.OriginId); WriteString(w, x.OriginName); WriteString(w, x.DestinationId);
			WriteString(w, x.DestinationName); w.Write(x.ManifestLoadedTick); w.Write(x.ManifestDeadlineTick);
			w.Write(x.ManifestEscrowBefore); w.Write(x.ManifestEscrowDebit); w.Write(x.ManifestEscrowAfter);
			w.Write((byte)x.ManifestEscrowState); w.Write(x.RetainedBefore); w.Write(x.RetainedDelta);
			w.Write(x.RetainedAfter); w.Write((byte)x.RetainedState);
			WriteNullable(w, x.Standing, WriteStanding); WriteNullable(w, x.Outbox, WriteOutbox);
			WriteNullable(w, x.Pattern, WritePattern);
			WriteNullable(w, x.PolityRecipient, WritePolityRecipient); WriteString(w, x.Fault);
		}

		private static KingdomTradeOperation ReadOperation(BinaryReader r)
		{
			KingdomTradeOperation x = new KingdomTradeOperation();
			x.Sequence = r.ReadInt64(); x.Id = ReadString(r); x.Kind = (KingdomTradeOperationKind)r.ReadByte();
			x.Phase = (KingdomTradePhase)r.ReadByte(); x.CreatedTick = r.ReadInt64(); x.UpdatedTick = r.ReadInt64();
			x.ZoneId = ReadString(r); x.SettlementId = ReadString(r); x.SettlementName = ReadString(r);
			x.CharterId = ReadString(r); x.ManifestId = ReadString(r); x.DealKey = ReadString(r);
			x.DealDisplayName = ReadString(r); x.Faction = ReadString(r); x.Cycles = r.ReadInt32();
			x.IncomePerCycle = r.ReadInt32(); x.IntervalTicks = r.ReadInt64(); x.DueBefore = r.ReadInt64();
			x.DueAfter = r.ReadInt64(); x.CaravanBlueprint = ReadString(r); x.ProjectionId = ReadString(r);
			x.ProjectionObjectId = ReadString(r); x.ProjectionX = r.ReadInt32(); x.ProjectionY = r.ReadInt32();
			x.PriorProjectionId = ReadString(r); x.PriorProjectionObjectId = ReadString(r);
			x.PriorProjectionZoneId = ReadString(r); x.ProjectionState = (KingdomTradePhysicalState)r.ReadByte();
			x.PriorCleanupState = (KingdomTradePhysicalState)r.ReadByte();
			x.WaterDirection = (KingdomTradeWaterDirection)r.ReadByte(); x.RequestedWater = r.ReadInt32();
			x.ProvedWater = r.ReadInt32(); x.AmbiguousWater = r.ReadInt32();
			x.WaterLegs = ReadList(r, KingdomTradeRules.MaxWaterLegs, ReadWater); x.MaterialClaim = ReadString(r);
			x.MaterialRequested = r.ReadInt32(); x.MaterialProved = r.ReadInt32();
			x.MaterialOutputs = ReadList(r, KingdomTradeRules.MaxMaterialOutputs, ReadMaterial);
			x.OriginId = ReadString(r); x.OriginName = ReadString(r); x.DestinationId = ReadString(r);
			x.DestinationName = ReadString(r); x.ManifestLoadedTick = r.ReadInt64();
			x.ManifestDeadlineTick = r.ReadInt64(); x.ManifestEscrowBefore = r.ReadInt32();
			x.ManifestEscrowDebit = r.ReadInt32(); x.ManifestEscrowAfter = r.ReadInt32();
			x.ManifestEscrowState = (KingdomTradePhysicalState)r.ReadByte(); x.RetainedBefore = r.ReadInt64();
			x.RetainedDelta = r.ReadInt64(); x.RetainedAfter = r.ReadInt64();
			x.RetainedState = (KingdomTradePhysicalState)r.ReadByte();
			x.Standing = ReadNullable(r, ReadStanding); x.Outbox = ReadNullable(r, ReadOutbox);
			x.Pattern = ReadNullable(r, ReadPattern);
			x.PolityRecipient = ReadNullable(r, ReadPolityRecipient); x.Fault = ReadString(r);
			if ((x.Kind == KingdomTradeOperationKind.CharterDelivery) != (x.Pattern != null))
				throw new InvalidDataException("Trade operation pattern lane does not match its kind.");
			return x;
		}

		// Frozen wire-v3 operation layout. Do not route this through the current writer.
		private static void WriteOperationV3(BinaryWriter w, KingdomTradeOperation x)
		{
			w.Write(x.Sequence); WriteString(w, x.Id); w.Write((byte)x.Kind); w.Write((byte)x.Phase);
			w.Write(x.CreatedTick); w.Write(x.UpdatedTick); WriteString(w, x.ZoneId); WriteString(w, x.SettlementId);
			WriteString(w, x.SettlementName); WriteString(w, x.CharterId); WriteString(w, x.ManifestId);
			WriteString(w, x.DealKey); WriteString(w, x.DealDisplayName); WriteString(w, x.Faction);
			w.Write(x.Cycles); w.Write(x.IncomePerCycle); w.Write(x.IntervalTicks); w.Write(x.DueBefore);
			w.Write(x.DueAfter); WriteString(w, x.CaravanBlueprint); WriteString(w, x.ProjectionId);
			WriteString(w, x.ProjectionObjectId); w.Write(x.ProjectionX); w.Write(x.ProjectionY);
			WriteString(w, x.PriorProjectionId); WriteString(w, x.PriorProjectionObjectId);
			WriteString(w, x.PriorProjectionZoneId); w.Write((byte)x.ProjectionState);
			w.Write((byte)x.PriorCleanupState); w.Write((byte)x.WaterDirection); w.Write(x.RequestedWater);
			w.Write(x.ProvedWater); w.Write(x.AmbiguousWater);
			WriteList(w, x.WaterLegs, KingdomTradeRules.MaxWaterLegs, WriteWater);
			WriteString(w, x.MaterialClaim); w.Write(x.MaterialRequested); w.Write(x.MaterialProved);
			WriteList(w, x.MaterialOutputs, KingdomTradeRules.MaxMaterialOutputs, WriteMaterial);
			WriteString(w, x.OriginId); WriteString(w, x.OriginName); WriteString(w, x.DestinationId);
			WriteString(w, x.DestinationName); w.Write(x.ManifestLoadedTick); w.Write(x.ManifestDeadlineTick);
			w.Write(x.ManifestEscrowBefore); w.Write(x.ManifestEscrowDebit); w.Write(x.ManifestEscrowAfter);
			w.Write((byte)x.ManifestEscrowState); w.Write(x.RetainedBefore); w.Write(x.RetainedDelta);
			w.Write(x.RetainedAfter); w.Write((byte)x.RetainedState);
			WriteNullable(w, x.Standing, WriteStanding); WriteNullable(w, x.Outbox, WriteOutbox);
			WriteString(w, x.Fault);
		}

		private static KingdomTradeOperation ReadOperationV3(BinaryReader r)
		{
			KingdomTradeOperation x = new KingdomTradeOperation();
			x.Sequence = r.ReadInt64(); x.Id = ReadString(r); x.Kind = (KingdomTradeOperationKind)r.ReadByte();
			x.Phase = (KingdomTradePhase)r.ReadByte(); x.CreatedTick = r.ReadInt64(); x.UpdatedTick = r.ReadInt64();
			x.ZoneId = ReadString(r); x.SettlementId = ReadString(r); x.SettlementName = ReadString(r);
			x.CharterId = ReadString(r); x.ManifestId = ReadString(r); x.DealKey = ReadString(r);
			x.DealDisplayName = ReadString(r); x.Faction = ReadString(r); x.Cycles = r.ReadInt32();
			x.IncomePerCycle = r.ReadInt32(); x.IntervalTicks = r.ReadInt64(); x.DueBefore = r.ReadInt64();
			x.DueAfter = r.ReadInt64(); x.CaravanBlueprint = ReadString(r); x.ProjectionId = ReadString(r);
			x.ProjectionObjectId = ReadString(r); x.ProjectionX = r.ReadInt32(); x.ProjectionY = r.ReadInt32();
			x.PriorProjectionId = ReadString(r); x.PriorProjectionObjectId = ReadString(r);
			x.PriorProjectionZoneId = ReadString(r); x.ProjectionState = (KingdomTradePhysicalState)r.ReadByte();
			x.PriorCleanupState = (KingdomTradePhysicalState)r.ReadByte();
			x.WaterDirection = (KingdomTradeWaterDirection)r.ReadByte(); x.RequestedWater = r.ReadInt32();
			x.ProvedWater = r.ReadInt32(); x.AmbiguousWater = r.ReadInt32();
			x.WaterLegs = ReadList(r, KingdomTradeRules.MaxWaterLegs, ReadWater); x.MaterialClaim = ReadString(r);
			x.MaterialRequested = r.ReadInt32(); x.MaterialProved = r.ReadInt32();
			x.MaterialOutputs = ReadList(r, KingdomTradeRules.MaxMaterialOutputs, ReadMaterial);
			x.OriginId = ReadString(r); x.OriginName = ReadString(r); x.DestinationId = ReadString(r);
			x.DestinationName = ReadString(r); x.ManifestLoadedTick = r.ReadInt64();
			x.ManifestDeadlineTick = r.ReadInt64(); x.ManifestEscrowBefore = r.ReadInt32();
			x.ManifestEscrowDebit = r.ReadInt32(); x.ManifestEscrowAfter = r.ReadInt32();
			x.ManifestEscrowState = (KingdomTradePhysicalState)r.ReadByte(); x.RetainedBefore = r.ReadInt64();
			x.RetainedDelta = r.ReadInt64(); x.RetainedAfter = r.ReadInt64();
			x.RetainedState = (KingdomTradePhysicalState)r.ReadByte();
			x.Standing = ReadNullable(r, ReadStanding); x.Outbox = ReadNullable(r, ReadOutbox);
			x.Fault = ReadString(r);
			return x;
		}

		private static void WriteProof(BinaryWriter w, KingdomTradeProof x)
		{
			WriteString(w, x.RealmId); w.Write(x.Sequence); WriteString(w, x.Id);
			WriteString(w, x.OperationEvidenceHash); w.Write((byte)x.Kind); w.Write((byte)x.Disposition);
			w.Write(x.ProvedWater); w.Write(x.AmbiguousWater); w.Write(x.RequestedWater);
			WriteString(w, x.SettlementId); WriteString(w, x.ManifestId); w.Write(x.ManifestEscrowBefore);
			w.Write(x.ManifestEscrowDebit); w.Write(x.ManifestEscrowAfter); w.Write((byte)x.ManifestEscrowState);
			w.Write(x.RetainedBefore); w.Write(x.RetainedDelta); w.Write(x.RetainedAfter); w.Write((byte)x.RetainedState);
			w.Write(x.MaterialRequested); w.Write(x.MaterialProved); w.Write((byte)x.ChronicleState);
			w.Write((byte)x.LedgerState); w.Write((byte)x.MessageState); w.Write((byte)x.DeedState);
			WriteNullable(w, x.PolityRecipient, WritePolityRecipient);
			w.Write(x.ManifestCleanup); w.Write(x.Tick); WriteString(w, x.Fault);
		}

		private static KingdomTradeProof ReadProof(BinaryReader r)
		{
			return new KingdomTradeProof { RealmId = ReadString(r), Sequence = r.ReadInt64(), Id = ReadString(r),
				OperationEvidenceHash = ReadString(r),
				Kind = (KingdomTradeOperationKind)r.ReadByte(), Disposition = (KingdomTradePhase)r.ReadByte(),
				ProvedWater = r.ReadInt32(), AmbiguousWater = r.ReadInt32(), RequestedWater = r.ReadInt32(),
				SettlementId = ReadString(r), ManifestId = ReadString(r), ManifestEscrowBefore = r.ReadInt32(),
				ManifestEscrowDebit = r.ReadInt32(), ManifestEscrowAfter = r.ReadInt32(),
				ManifestEscrowState = (KingdomTradePhysicalState)r.ReadByte(), RetainedBefore = r.ReadInt64(),
				RetainedDelta = r.ReadInt64(), RetainedAfter = r.ReadInt64(),
				RetainedState = (KingdomTradePhysicalState)r.ReadByte(), MaterialRequested = r.ReadInt32(),
					MaterialProved = r.ReadInt32(), ChronicleState = (KingdomTradeSinkState)r.ReadByte(),
					LedgerState = (KingdomTradeSinkState)r.ReadByte(), MessageState = (KingdomTradeSinkState)r.ReadByte(),
					DeedState = (KingdomTradeSinkState)r.ReadByte(),
					PolityRecipient = ReadNullable(r, ReadPolityRecipient),
					ManifestCleanup = ReadExactBoolean(r),
					Tick = r.ReadInt64(), Fault = ReadString(r) };
		}

		private static void WritePolityRecipient(BinaryWriter w,
			KingdomTradePolityRecipientWitness x)
		{
			WriteString(w, x.BodyId); WriteString(w, x.CohortId);
			WriteString(w, x.ProjectionId); WriteString(w, x.SurfaceRef);
			WriteString(w, x.RequestDigest); WriteString(w, x.WitnessDigest);
		}

		private static KingdomTradePolityRecipientWitness ReadPolityRecipient(BinaryReader r)
		{
			return new KingdomTradePolityRecipientWitness
			{
				BodyId = ReadString(r), CohortId = ReadString(r),
				ProjectionId = ReadString(r), SurfaceRef = ReadString(r),
				RequestDigest = ReadString(r), WitnessDigest = ReadString(r)
			};
		}

		private static void WriteArchive(BinaryWriter w, KingdomTradeArchive x)
		{
			WriteString(w, x.RealmId); WriteStringList(w, x.SettlementIds,
				KingdomTradeRules.MaxSettlementIds); w.Write(x.RetainedEscrowDrams);
			w.Write(x.ManifestEscrowDrams); WriteString(w, x.ManifestId);
			w.Write((byte)x.ManifestStatus); w.Write(x.CharterCount); w.Write(x.ProjectionCount);
			w.Write(x.ProofCount); WriteString(w, x.OpenOperationId);
			WriteString(w, x.PendingRetirementId); w.Write(x.OpenRequestedWater);
			w.Write(x.OpenProvedWater); w.Write(x.OpenAmbiguousWater); w.Write(x.RetiredThrough);
			WriteString(w, x.AuthorityEvidenceHash); w.Write(x.ClosedTick);
			WriteString(w, x.ReceiptEvidenceHash);
		}

		private static KingdomTradeArchive ReadArchive(BinaryReader r)
		{
			return new KingdomTradeArchive { RealmId = ReadString(r),
				SettlementIds = ReadStringList(r, KingdomTradeRules.MaxSettlementIds),
				RetainedEscrowDrams = r.ReadInt64(), ManifestEscrowDrams = r.ReadInt32(),
				ManifestId = ReadString(r), ManifestStatus = (KingdomTradeManifestStatus)r.ReadByte(),
				CharterCount = r.ReadInt32(), ProjectionCount = r.ReadInt32(),
				ProofCount = r.ReadInt32(), OpenOperationId = ReadString(r),
				PendingRetirementId = ReadString(r), OpenRequestedWater = r.ReadInt32(),
				OpenProvedWater = r.ReadInt32(), OpenAmbiguousWater = r.ReadInt32(),
				RetiredThrough = r.ReadInt64(), AuthorityEvidenceHash = ReadString(r),
				ClosedTick = r.ReadInt64(), ReceiptEvidenceHash = ReadString(r) };
		}

		private static void WriteProofCompaction(BinaryWriter w, KingdomTradeProofCompaction x)
		{
			WriteString(w, x.RealmId); w.Write(x.FirstSequence); w.Write(x.LastSequence);
			w.Write(x.ProofCount); WriteString(w, x.EvidenceHash);
		}

		private static KingdomTradeProofCompaction ReadProofCompaction(BinaryReader r)
		{
			return new KingdomTradeProofCompaction { RealmId = ReadString(r),
				FirstSequence = r.ReadInt64(), LastSequence = r.ReadInt64(),
				ProofCount = r.ReadInt32(), EvidenceHash = ReadString(r) };
		}

		private static void WriteIncident(BinaryWriter w, KingdomTradeIncident x)
		{
			WriteString(w, x.RealmId); w.Write(x.Sequence); WriteString(w, x.OperationId);
			WriteString(w, x.EvidenceHash); w.Write(x.Tick); WriteString(w, x.Fault);
		}

		private static KingdomTradeIncident ReadIncident(BinaryReader r)
		{
			return new KingdomTradeIncident { RealmId = ReadString(r), Sequence = r.ReadInt64(),
				OperationId = ReadString(r), EvidenceHash = ReadString(r), Tick = r.ReadInt64(), Fault = ReadString(r) };
		}
	}
}
