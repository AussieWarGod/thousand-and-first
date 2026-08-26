using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{
		private static void WriteCarryOperation(BinaryWriter w, KingdomCarryOperation o,
			bool IncludeV6)
		{
			w.Write(o != null);
			if (o == null) return;
			EnsureCount(o.Sources, KingdomLifecycleRules.MaxCarrySources, "carry sources");
			EnsureCount(o.Outputs, KingdomLifecycleRules.MaxCarryOutputs, "carry outputs");
			EnsureCount(o.SettlementIds, KingdomLifecycleRules.MaxSettlementIds,
				"frozen carry settlement ids");
			w.Write(o.Sequence); S(w, o.Id, true); S(w, o.PlanHash, true);
			w.Write((byte)o.Phase); w.Write(o.CreatedTick); w.Write(o.UpdatedTick);
			w.Write(o.SettlementIds.Count);
			for (int i = 0; i < o.SettlementIds.Count; i++) S(w, o.SettlementIds[i], true);
			S(w, o.RealmTopologyHash, true);
			S(w, o.OriginSettlementId, true); S(w, o.OriginZoneId, false);
			w.Write(o.OriginX); w.Write(o.OriginY);
			S(w, o.DestinationSettlementId, true); S(w, o.DestinationSettlementName, false);
			S(w, o.DestinationZoneId, false); w.Write((byte)o.DestinationTopology);
			S(w, o.DestinationOwnerId, true); w.Write(o.DestinationX); w.Write(o.DestinationY);
			w.Write(o.DueTick); w.Write(o.RiskFrozen); w.Write(o.LostOnRoad);
			w.Write(o.SourceIndex); w.Write(o.OutputIndex);
			WriteLease(w, o.ScheduleLease);
			S(w, o.ScheduleReceiptId, false); S(w, o.ScheduleTopologyId, false);
			w.Write(o.ScheduleBeforeMatches); w.Write(o.ScheduleAfterMatches);
			w.Write(o.ScheduleSameReference); S(w, o.ScheduleProofId, false);
			w.Write((byte)o.ScheduleReceiptState);
			w.Write(o.Sources.Count);
			for (int i = 0; i < o.Sources.Count; i++)
				WriteCarrySource(w, o.Sources[i], IncludeV6);
			w.Write(o.Outputs.Count);
			for (int i = 0; i < o.Outputs.Count; i++) WriteProjection(w, o.Outputs[i]);
			WriteSix(w, o.Mud, o.Brush, o.Timber, o.Stone, o.Marble, o.Scrap);
			WriteSix(w, o.EscrowMud, o.EscrowBrush, o.EscrowTimber, o.EscrowStone,
				o.EscrowMarble, o.EscrowScrap);
			WriteSix(w, o.DeliveredMud, o.DeliveredBrush, o.DeliveredTimber, o.DeliveredStone,
				o.DeliveredMarble, o.DeliveredScrap);
			WriteSix(w, o.LostMud, o.LostBrush, o.LostTimber, o.LostStone, o.LostMarble, o.LostScrap);
			WriteOutbox(w, o.Outbox); S(w, o.Fault, false, true);
			if (IncludeV6)
			{
				EnsureCount(o.JobIds, KingdomLifecycleRules.MaxCarryJobIds, "carry job ids");
				EnsureCount(o.TripIds, KingdomLifecycleRules.MaxCarryTripIds, "carry trip ids");
				w.Write((byte)o.AuthorityKind); w.Write(o.ManifestVersion);
				S(w, o.ManifestDigest, true); w.Write(o.ManifestRevision);
				w.Write(o.JobIds.Count); for (int i = 0; i < o.JobIds.Count; i++) w.Write(o.JobIds[i]);
				w.Write(o.TripIds.Count); for (int i = 0; i < o.TripIds.Count; i++) w.Write(o.TripIds[i]);
				S(w, o.SignObjectId, true); S(w, o.SignBlueprint, false);
				w.Write((byte)o.SignTopology); S(w, o.SignOwnerId, true); S(w, o.SignZoneId, false);
				w.Write(o.SignX); w.Write(o.SignY); w.Write(o.SignCount);
				S(w, o.SignReceiptId, true); w.Write(o.SignReceiptBeforeMatches);
				w.Write(o.SignReceiptAfterMatches); w.Write(o.SignReceiptBeforeCount);
				w.Write(o.SignReceiptAfterCount);
				w.Write(o.SignReceiptSameReference); S(w, o.SignReceiptProofId, true);
				w.Write((byte)o.SignReceiptState); w.Write(o.DestinationSafetyWaiting);
				w.Write(o.DestinationSafetyWaitTick); S(w, o.SpillZoneId, false);
				w.Write(o.SpillX); w.Write(o.SpillY);
			}
		}

		private static KingdomCarryOperation ReadCarryOperation(BinaryReader r, bool IncludeV6)
		{
			if (!ReadExactBoolean(r)) return null;
			KingdomCarryOperation o = new KingdomCarryOperation();
			o.Sequence = r.ReadInt64(); o.Id = S(r, true); o.PlanHash = S(r, true);
			o.Phase = (KingdomLifecyclePhase)r.ReadByte(); o.CreatedTick = r.ReadInt64();
			o.UpdatedTick = r.ReadInt64();
			int settlements = ReadCount(r, KingdomLifecycleRules.MaxSettlementIds);
			o.SettlementIds = new List<string>(settlements);
			for (int i = 0; i < settlements; i++) o.SettlementIds.Add(S(r, true));
			o.RealmTopologyHash = S(r, true); o.OriginSettlementId = S(r, true);
			o.OriginZoneId = S(r, false);
			o.OriginX = r.ReadInt32(); o.OriginY = r.ReadInt32();
			o.DestinationSettlementId = S(r, true); o.DestinationSettlementName = S(r, false);
			o.DestinationZoneId = S(r, false);
			o.DestinationTopology = (KingdomLifecycleTopology)r.ReadByte();
			o.DestinationOwnerId = S(r, true);
			o.DestinationX = r.ReadInt32(); o.DestinationY = r.ReadInt32();
			o.DueTick = r.ReadInt64(); o.RiskFrozen = ReadExactBoolean(r);
			o.LostOnRoad = ReadExactBoolean(r);
			o.SourceIndex = r.ReadInt32(); o.OutputIndex = r.ReadInt32();
			o.ScheduleLease = ReadLease(r, true);
			o.ScheduleReceiptId = S(r, false); o.ScheduleTopologyId = S(r, false);
			o.ScheduleBeforeMatches = r.ReadInt32(); o.ScheduleAfterMatches = r.ReadInt32();
			o.ScheduleSameReference = ReadExactBoolean(r); o.ScheduleProofId = S(r, false);
			o.ScheduleReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte();
			int sources = ReadCount(r, KingdomLifecycleRules.MaxCarrySources);
			o.Sources = new List<KingdomCarrySource>(sources);
			for (int i = 0; i < sources; i++) o.Sources.Add(ReadCarrySource(r, IncludeV6));
			int outputs = ReadCount(r, KingdomLifecycleRules.MaxCarryOutputs);
			o.Outputs = new List<KingdomLifecycleProjection>(outputs);
			for (int i = 0; i < outputs; i++) o.Outputs.Add(ReadProjection(r));
			ReadSix(r, out o.Mud, out o.Brush, out o.Timber, out o.Stone, out o.Marble, out o.Scrap);
			ReadSix(r, out o.EscrowMud, out o.EscrowBrush, out o.EscrowTimber, out o.EscrowStone,
				out o.EscrowMarble, out o.EscrowScrap);
			ReadSix(r, out o.DeliveredMud, out o.DeliveredBrush, out o.DeliveredTimber,
				out o.DeliveredStone, out o.DeliveredMarble, out o.DeliveredScrap);
			ReadSix(r, out o.LostMud, out o.LostBrush, out o.LostTimber, out o.LostStone,
				out o.LostMarble, out o.LostScrap);
			o.Outbox = ReadOutbox(r); o.Fault = S(r, false, true);
			if (IncludeV6)
			{
				o.AuthorityKind = (KingdomCarryAuthorityKind)r.ReadByte();
				o.ManifestVersion = r.ReadInt32(); o.ManifestDigest = S(r, true);
				o.ManifestRevision = r.ReadInt64();
				int jobs = ReadCount(r, KingdomLifecycleRules.MaxCarryJobIds);
				o.JobIds = new List<int>(jobs);
				for (int i = 0; i < jobs; i++) o.JobIds.Add(r.ReadInt32());
				int trips = ReadCount(r, KingdomLifecycleRules.MaxCarryTripIds);
				o.TripIds = new List<int>(trips);
				for (int i = 0; i < trips; i++) o.TripIds.Add(r.ReadInt32());
				o.SignObjectId = S(r, true); o.SignBlueprint = S(r, false);
				o.SignTopology = (KingdomLifecycleTopology)r.ReadByte();
				o.SignOwnerId = S(r, true); o.SignZoneId = S(r, false);
				o.SignX = r.ReadInt32(); o.SignY = r.ReadInt32(); o.SignCount = r.ReadInt32();
				o.SignReceiptId = S(r, true); o.SignReceiptBeforeMatches = r.ReadInt32();
				o.SignReceiptAfterMatches = r.ReadInt32();
				o.SignReceiptBeforeCount = r.ReadInt32();
				o.SignReceiptAfterCount = r.ReadInt32();
				o.SignReceiptSameReference = ReadExactBoolean(r);
				o.SignReceiptProofId = S(r, true);
				o.SignReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte();
				o.DestinationSafetyWaiting = ReadExactBoolean(r);
				o.DestinationSafetyWaitTick = r.ReadInt64(); o.SpillZoneId = S(r, false);
				o.SpillX = r.ReadInt32(); o.SpillY = r.ReadInt32();
			}
			return o;
		}

		private static void WriteCarrySource(BinaryWriter w, KingdomCarrySource x, bool IncludeV6)
		{
			if (x == null) throw new InvalidDataException("null carry source");
			S(w, x.OperationId, true); S(w, x.SourceEventId, true); S(w, x.ObjectId, true);
			S(w, x.Blueprint, false); w.Write((byte)x.Topology); S(w, x.OwnerId, true);
			S(w, x.ZoneId, false); w.Write(x.X); w.Write(x.Y); w.Write(x.Material);
			w.Write(x.OriginalCount); w.Write(x.PlannedCount); w.Write(x.Removed); w.Write(x.UnitCursor);
			w.Write(x.UnitBefore); w.Write(x.UnitAfter); S(w, x.UnitEventId, true);
			w.Write((byte)x.UnitState); S(w, x.ReceiptId, false); S(w, x.ReceiptTopologyId, false);
			w.Write(x.ReceiptBeforeIdMatches); w.Write(x.ReceiptAfterIdMatches);
			w.Write(x.ReceiptBeforeCount); w.Write(x.ReceiptAfterCount);
			w.Write(x.ReceiptSameReference); S(w, x.ReceiptProofId, false);
			S(w, x.ReceiptChainId, false); w.Write(x.ReceiptChainCount);
			w.Write((byte)x.ReceiptState); w.Write((byte)x.State);
			if (IncludeV6)
			{
				w.Write(x.LoadedCount); w.Write(x.DeliveredCount); w.Write(x.LostCount);
				w.Write(x.CurrentTripId); w.Write((byte)x.CurrentTopology);
				S(w, x.CurrentOwnerId, true); S(w, x.CurrentZoneId, false);
				w.Write(x.CurrentX); w.Write(x.CurrentY);
				w.Write((byte)x.PendingTransfer); w.Write((byte)x.PendingTopology);
				S(w, x.PendingOwnerId, true); S(w, x.PendingZoneId, false);
				w.Write(x.PendingX); w.Write(x.PendingY);
			}
		}

		private static KingdomCarrySource ReadCarrySource(BinaryReader r, bool IncludeV6)
		{
			KingdomCarrySource source = new KingdomCarrySource
			{
				OperationId = S(r, true), SourceEventId = S(r, true), ObjectId = S(r, true),
				Blueprint = S(r, false), Topology = (KingdomLifecycleTopology)r.ReadByte(),
				OwnerId = S(r, true), ZoneId = S(r, false), X = r.ReadInt32(), Y = r.ReadInt32(),
				Material = r.ReadInt32(), OriginalCount = r.ReadInt32(), PlannedCount = r.ReadInt32(),
				Removed = r.ReadInt32(),
				UnitCursor = r.ReadInt32(), UnitBefore = r.ReadInt32(), UnitAfter = r.ReadInt32(),
				UnitEventId = S(r, true), UnitState = (KingdomLifecyclePhysicalState)r.ReadByte(),
				ReceiptId = S(r, false), ReceiptTopologyId = S(r, false),
				ReceiptBeforeIdMatches = r.ReadInt32(), ReceiptAfterIdMatches = r.ReadInt32(),
				ReceiptBeforeCount = r.ReadInt32(), ReceiptAfterCount = r.ReadInt32(),
				ReceiptSameReference = ReadExactBoolean(r), ReceiptProofId = S(r, false),
				ReceiptChainId = S(r, false), ReceiptChainCount = r.ReadInt32(),
				ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte(),
				State = (KingdomLifecyclePhysicalState)r.ReadByte()
			};
			if (IncludeV6)
			{
				source.LoadedCount = r.ReadInt32(); source.DeliveredCount = r.ReadInt32();
				source.LostCount = r.ReadInt32(); source.CurrentTripId = r.ReadInt32();
				source.CurrentTopology = (KingdomLifecycleTopology)r.ReadByte();
				source.CurrentOwnerId = S(r, true); source.CurrentZoneId = S(r, false);
				source.CurrentX = r.ReadInt32(); source.CurrentY = r.ReadInt32();
				source.PendingTransfer = (KingdomCarryTransferKind)r.ReadByte();
				source.PendingTopology = (KingdomLifecycleTopology)r.ReadByte();
				source.PendingOwnerId = S(r, true); source.PendingZoneId = S(r, false);
				source.PendingX = r.ReadInt32(); source.PendingY = r.ReadInt32();
			}
			return source;
		}

	}
}
