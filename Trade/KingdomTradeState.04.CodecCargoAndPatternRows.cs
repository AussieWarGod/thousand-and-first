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

		private static void WriteWater(BinaryWriter w, KingdomTradeWaterLeg x)
		{
			WriteString(w, x.OwnerId); WriteString(w, x.ZoneId); w.Write(x.Capacity); w.Write(x.Before);
			w.Write(x.Delta); w.Write(x.After); WriteString(w, x.BeforeComposition);
			WriteString(w, x.AfterComposition); w.Write((byte)x.State);
		}

		private static KingdomTradeWaterLeg ReadWater(BinaryReader r)
		{
			return new KingdomTradeWaterLeg { OwnerId = ReadString(r), ZoneId = ReadString(r),
				Capacity = r.ReadInt32(), Before = r.ReadInt32(), Delta = r.ReadInt32(), After = r.ReadInt32(),
				BeforeComposition = ReadString(r), AfterComposition = ReadString(r),
				State = (KingdomTradePhysicalState)r.ReadByte() };
		}

		private static void WriteMaterial(BinaryWriter w, KingdomTradeMaterialOutput x)
		{
			WriteString(w, x.OutputId); WriteString(w, x.Marker); WriteString(w, x.Blueprint); w.Write(x.Count);
			WriteString(w, x.DestinationOwnerId); WriteString(w, x.ZoneId); w.Write((byte)x.State);
			w.Write((byte)x.CleanupState);
		}

		private static KingdomTradeMaterialOutput ReadMaterial(BinaryReader r)
		{
			return new KingdomTradeMaterialOutput { OutputId = ReadString(r), Marker = ReadString(r),
				Blueprint = ReadString(r), Count = r.ReadInt32(), DestinationOwnerId = ReadString(r),
				ZoneId = ReadString(r), State = (KingdomTradePhysicalState)r.ReadByte(),
				CleanupState = (KingdomTradePhysicalState)r.ReadByte() };
		}

		private static void WriteStanding(BinaryWriter w, KingdomTradeStandingCas x)
		{
			WriteString(w, x.Faction); w.Write(x.Before); w.Write(x.Delta); w.Write(x.After); w.Write((byte)x.State);
		}

		private static KingdomTradeStandingCas ReadStanding(BinaryReader r)
		{
			return new KingdomTradeStandingCas { Faction = ReadString(r), Before = r.ReadInt32(),
				Delta = r.ReadInt32(), After = r.ReadInt32(), State = (KingdomTradePhysicalState)r.ReadByte() };
		}

		private static void WriteOutbox(BinaryWriter w, KingdomTradeOutbox x)
		{
			WriteString(w, x.EventId); WriteString(w, x.Chronicle); w.Write((byte)x.ChronicleState);
			WriteString(w, x.LedgerNote); w.Write(x.LedgerDeliveredDelta); w.Write((byte)x.LedgerState);
			WriteString(w, x.Message); w.Write((byte)x.MessageState); WriteString(w, x.Deed); w.Write((byte)x.DeedState);
		}

		private static KingdomTradeOutbox ReadOutbox(BinaryReader r)
		{
			return new KingdomTradeOutbox { EventId = ReadString(r), Chronicle = ReadString(r),
				ChronicleState = (KingdomTradeSinkState)r.ReadByte(), LedgerNote = ReadString(r),
				LedgerDeliveredDelta = r.ReadInt32(), LedgerState = (KingdomTradeSinkState)r.ReadByte(),
				Message = ReadString(r), MessageState = (KingdomTradeSinkState)r.ReadByte(),
				Deed = ReadString(r), DeedState = (KingdomTradeSinkState)r.ReadByte() };
		}

		private static void WriteCharter(BinaryWriter w, KingdomTradeCharter x)
		{
			w.Write(x.Sequence); WriteString(w, x.Id); WriteString(w, x.DealKey); WriteString(w, x.Faction);
			w.Write(x.CreatedTick); w.Write(x.NextTick); w.Write(x.Quarantined); WriteString(w, x.Fault);
		}

		private static KingdomTradeCharter ReadCharter(BinaryReader r)
		{
			return new KingdomTradeCharter { Sequence = r.ReadInt64(), Id = ReadString(r), DealKey = ReadString(r),
				Faction = ReadString(r), CreatedTick = r.ReadInt64(), NextTick = r.ReadInt64(),
				Quarantined = ReadExactBoolean(r), Fault = ReadString(r) };
		}

		private static void WriteManifest(BinaryWriter w, KingdomTradeManifestState x)
		{
			w.Write(x.OperationSequence); WriteString(w, x.OperationId); WriteString(w, x.Id);
			WriteString(w, x.OriginId); WriteString(w, x.OriginName);
			WriteString(w, x.DestinationId); WriteString(w, x.DestinationName); w.Write(x.OriginalDrams);
			w.Write(x.EscrowDrams); w.Write(x.LoadedTick); w.Write(x.DeadlineTick); w.Write(x.TurnedBack);
			w.Write((byte)x.Status); WriteString(w, x.Fault);
		}

		private static KingdomTradeManifestState ReadManifest(BinaryReader r)
		{
			return new KingdomTradeManifestState { OperationSequence = r.ReadInt64(), OperationId = ReadString(r),
				Id = ReadString(r), OriginId = ReadString(r),
				OriginName = ReadString(r), DestinationId = ReadString(r), DestinationName = ReadString(r),
				OriginalDrams = r.ReadInt32(), EscrowDrams = r.ReadInt32(), LoadedTick = r.ReadInt64(),
				DeadlineTick = r.ReadInt64(), TurnedBack = ReadExactBoolean(r),
				Status = (KingdomTradeManifestStatus)r.ReadByte(), Fault = ReadString(r) };
		}

		private static void WriteProjection(BinaryWriter w, KingdomTradeProjectionRow x)
		{
			w.Write(x.OperationSequence); WriteString(w, x.OperationId);
			WriteString(w, x.SettlementId); WriteString(w, x.ZoneId);
			WriteString(w, x.ProjectionId); WriteString(w, x.ObjectId); w.Write(x.Quarantined); WriteString(w, x.Fault);
		}

		private static KingdomTradeProjectionRow ReadProjection(BinaryReader r)
		{
			return new KingdomTradeProjectionRow { OperationSequence = r.ReadInt64(),
				OperationId = ReadString(r), SettlementId = ReadString(r),
				ZoneId = ReadString(r), ProjectionId = ReadString(r), ObjectId = ReadString(r),
				Quarantined = ReadExactBoolean(r), Fault = ReadString(r) };
		}

		private static void WritePatternDesign(BinaryWriter w, KingdomTradePatternDesign x)
		{
			WriteString(w, x.BuildingKey); WriteString(w, x.LearnName); WriteString(w, x.Label);
		}

		private static KingdomTradePatternDesign ReadPatternDesign(BinaryReader r)
		{
			return new KingdomTradePatternDesign
			{
				BuildingKey = ReadString(r), LearnName = ReadString(r), Label = ReadString(r)
			};
		}

		private static void WritePattern(BinaryWriter w, KingdomTradePatternReceipt x)
		{
			if (!KingdomTradePatternRules.Valid(x))
				throw new InvalidDataException("Pattern receipt is malformed or exceeds bounds.");
			w.Write((byte)x.State);
			WriteList(w, x.Offers, KingdomTradePatternRules.MaxOffers, WritePatternDesign);
			w.Write(x.SelectedIndex); WriteString(w, x.RosterBefore); WriteString(w, x.RosterAfter);
			WriteString(w, x.Chronicle); w.Write((byte)x.ChronicleState);
			WriteString(w, x.Message); w.Write((byte)x.MessageState); WriteString(w, x.Fault);
		}

		private static KingdomTradePatternReceipt ReadPattern(BinaryReader r)
		{
			KingdomTradePatternReceipt receipt = new KingdomTradePatternReceipt
			{
				State = (KingdomTradePatternState)r.ReadByte(),
				Offers = ReadList(r, KingdomTradePatternRules.MaxOffers, ReadPatternDesign),
				SelectedIndex = r.ReadInt32(), RosterBefore = ReadString(r),
				RosterAfter = ReadString(r), Chronicle = ReadString(r),
				ChronicleState = (KingdomTradeSinkState)r.ReadByte(),
				Message = ReadString(r), MessageState = (KingdomTradeSinkState)r.ReadByte(),
				Fault = ReadString(r)
			};
			if (!KingdomTradePatternRules.Valid(receipt))
				throw new InvalidDataException("Pattern receipt is malformed or exceeds bounds.");
			return receipt;
		}
	}
}
