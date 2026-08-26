using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{
		public static int ReadCount(BinaryReader Reader, int Maximum)
		{
			int count = Reader.ReadInt32();
			if (count < 0 || count > Maximum)
				throw new InvalidDataException("bounded row count exceeded");
			return count;
		}

		private static bool ReadExactBoolean(BinaryReader Reader)
		{
			byte value = Reader.ReadByte();
			if (value > 1) throw new InvalidDataException("noncanonical boolean byte");
			return value == 1;
		}

		public static string ReadString(BinaryReader Reader, int MaximumBytes)
		{
			int length = Reader.ReadInt32();
			if (length == -1) return null;
			if (length < 0 || length > MaximumBytes)
				throw new InvalidDataException("bounded string length exceeded");
			byte[] bytes = Reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			return StrictUtf8.GetString(bytes);
		}

		public static void WriteString(BinaryWriter Writer, string Value, int MaximumBytes)
		{
			if (Value == null)
			{
				Writer.Write(-1);
				return;
			}
			int byteCount = StrictUtf8.GetByteCount(Value);
			if (byteCount > MaximumBytes)
				throw new InvalidDataException("bounded string length exceeded");
			byte[] bytes = StrictUtf8.GetBytes(Value);
			Writer.Write(byteCount);
			Writer.Write(bytes, 0, bytes.Length);
		}

		private static void WriteOperation(BinaryWriter w, KingdomLifecycleOperation o,
			int wireVersion)
		{
			w.Write(o != null);
			if (o == null) return;
			if (wireVersion < KingdomLifecycleRules.RaidLedgerLifecycleFormatVersion
				&& (byte)o.Action > (byte)KingdomLifecycleAction.PetitionExpire)
				throw new InvalidDataException(
					"historical lifecycle wire cannot encode appended raid action");
			EnsureCount(o.WaterLegs, KingdomLifecycleRules.MaxWaterLegs, "water legs");
			EnsureCount(o.Projections, KingdomLifecycleRules.MaxProjections, "projections");
			EnsureCount(o.ResourceLeases, KingdomLifecycleRules.MaxResourceLeases, "resource leases");
			w.Write(o.Sequence);
			S(w, o.Id, true); S(w, o.PlanHash, true);
			w.Write((byte)o.Lane); w.Write((byte)o.Action); w.Write((byte)o.Phase);
			w.Write(o.CreatedTick); w.Write(o.UpdatedTick);
			S(w, o.SettlementId, true); S(w, o.ZoneId, false); S(w, o.ObjectId, true);
			S(w, o.ObjectMarker, true); S(w, o.Blueprint, false);
			w.Write((byte)o.ObjectTopology); S(w, o.ObjectOwnerId, true);
			w.Write(o.ObjectX); w.Write(o.ObjectY); S(w, o.ObjectName, false);
			S(w, o.Origin, false); S(w, o.Faction, false); S(w, o.DisplayFaction, false);
			S(w, o.Detail, false, true); S(w, o.Creed, false);
			w.Write(o.Kind); w.Write(o.Target); w.Write(o.Count); w.Write(o.DepartedCount);
			w.Write(o.DueBefore); w.Write(o.DueAfter); w.Write(o.DepartTick);
			w.Write(o.WaterRequested); w.Write(o.WaterProved); w.Write(o.WaterOutstanding);
			w.Write(o.WaterLost); w.Write(o.WaterAmbiguous); w.Write((byte)o.WaterState);
			w.Write(o.WaterLegs.Count);
			for (int i = 0; i < o.WaterLegs.Count; i++) WriteWater(w, o.WaterLegs[i]);
			w.Write((byte)o.RemovalState);
			w.Write(o.Projections.Count);
			for (int i = 0; i < o.Projections.Count; i++) WriteProjection(w, o.Projections[i]);
			w.Write((byte)o.EffectState);
			w.Write(o.ResourceLeases.Count);
			for (int i = 0; i < o.ResourceLeases.Count; i++) WriteLease(w, o.ResourceLeases[i]);
			w.Write(o.Defence); w.Write(o.PartySize); w.Write(o.Spawned);
			w.Write(o.PlunderRequested); w.Write(o.PlunderProved);
			S(w, o.ArrivalText, false, true); WriteOutbox(w, o.Outbox); S(w, o.Fault, false, true);
		}

		private static KingdomLifecycleOperation ReadOperation(BinaryReader r)
		{
			return ReadOperation(r, KingdomLifecycleRules.CurrentFormatVersion);
		}

		private static KingdomLifecycleOperation ReadOperation(BinaryReader r, int wireVersion)
		{
			if (!ReadExactBoolean(r)) return null;
			KingdomLifecycleOperation o = new KingdomLifecycleOperation();
			o.Sequence = r.ReadInt64();
			o.Id = S(r, true); o.PlanHash = S(r, true);
			o.Lane = (KingdomLifecycleLane)r.ReadByte();
			o.Action = (KingdomLifecycleAction)r.ReadByte();
			if (wireVersion < KingdomLifecycleRules.RaidLedgerLifecycleFormatVersion
				&& (byte)o.Action > (byte)KingdomLifecycleAction.PetitionExpire)
				throw new InvalidDataException(
					"historical lifecycle wire contains appended raid action");
			o.Phase = (KingdomLifecyclePhase)r.ReadByte();
			o.CreatedTick = r.ReadInt64(); o.UpdatedTick = r.ReadInt64();
			o.SettlementId = S(r, true); o.ZoneId = S(r, false); o.ObjectId = S(r, true);
			o.ObjectMarker = S(r, true); o.Blueprint = S(r, false);
			o.ObjectTopology = (KingdomLifecycleTopology)r.ReadByte();
			o.ObjectOwnerId = S(r, true); o.ObjectX = r.ReadInt32(); o.ObjectY = r.ReadInt32();
			o.ObjectName = S(r, false);
			o.Origin = S(r, false); o.Faction = S(r, false); o.DisplayFaction = S(r, false);
			o.Detail = S(r, false, true); o.Creed = S(r, false);
			o.Kind = r.ReadInt32(); o.Target = r.ReadInt32(); o.Count = r.ReadInt32();
			o.DepartedCount = r.ReadInt32(); o.DueBefore = r.ReadInt64();
			o.DueAfter = r.ReadInt64(); o.DepartTick = r.ReadInt64();
			o.WaterRequested = r.ReadInt32(); o.WaterProved = r.ReadInt32();
			o.WaterOutstanding = r.ReadInt32(); o.WaterLost = r.ReadInt32();
			o.WaterAmbiguous = r.ReadInt32(); o.WaterState = (KingdomLifecyclePhysicalState)r.ReadByte();
			int water = ReadCount(r, KingdomLifecycleRules.MaxWaterLegs);
			o.WaterLegs = new List<KingdomLifecycleWaterLeg>(water);
			for (int i = 0; i < water; i++) o.WaterLegs.Add(ReadWater(r));
			o.RemovalState = (KingdomLifecyclePhysicalState)r.ReadByte();
			int projections = ReadCount(r, KingdomLifecycleRules.MaxProjections);
			o.Projections = new List<KingdomLifecycleProjection>(projections);
			for (int i = 0; i < projections; i++) o.Projections.Add(ReadProjection(r));
			o.EffectState = (KingdomLifecyclePhysicalState)r.ReadByte();
			int leases = ReadCount(r, KingdomLifecycleRules.MaxResourceLeases);
			o.ResourceLeases = new List<KingdomLifecycleResourceLease>(leases);
			for (int i = 0; i < leases; i++) o.ResourceLeases.Add(ReadLease(r,
				wireVersion == KingdomLifecycleRules.LegacyLifecycleFormatVersion));
			o.Defence = r.ReadInt32(); o.PartySize = r.ReadInt32(); o.Spawned = r.ReadInt32();
			o.PlunderRequested = r.ReadInt32(); o.PlunderProved = r.ReadInt32();
			o.ArrivalText = S(r, false, true); o.Outbox = ReadOutbox(r); o.Fault = S(r, false, true);
			return o;
		}

		private static void WriteWater(BinaryWriter w, KingdomLifecycleWaterLeg x)
		{
			if (x == null) throw new InvalidDataException("null water leg");
			S(w, x.OperationId, true); S(w, x.LeaseKey, true); S(w, x.OwnerId, true);
			S(w, x.Blueprint, false); S(w, x.ZoneId, false); w.Write(x.Capacity);
			w.Write(x.Before); w.Write(x.Delta);
			w.Write(x.After); S(w, x.Composition, false, true); S(w, x.ReceiptId, false);
			w.Write(x.ReceiptBeforeMatches); w.Write(x.ReceiptAfterMatches);
			w.Write(x.ReceiptSameReference); S(w, x.ReceiptProofId, false);
			w.Write((byte)x.ReceiptState); w.Write((byte)x.State);
		}

		private static KingdomLifecycleWaterLeg ReadWater(BinaryReader r)
		{
			return new KingdomLifecycleWaterLeg
			{
				OperationId = S(r, true), LeaseKey = S(r, true), OwnerId = S(r, true),
				Blueprint = S(r, false), ZoneId = S(r, false), Capacity = r.ReadInt32(),
				Before = r.ReadInt32(),
				Delta = r.ReadInt32(), After = r.ReadInt32(), Composition = S(r, false, true),
				ReceiptId = S(r, false), ReceiptBeforeMatches = r.ReadInt32(),
				ReceiptAfterMatches = r.ReadInt32(), ReceiptSameReference = ReadExactBoolean(r),
				ReceiptProofId = S(r, false),
				ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte(),
				State = (KingdomLifecyclePhysicalState)r.ReadByte()
			};
		}

		private static void WriteGrowthWater(BinaryWriter w, KingdomGrowthWaterLeg x)
		{
			if (x == null) throw new InvalidDataException("null growth water leg");
			S(w, x.OperationId, true); S(w, x.EventId, true); S(w, x.LeaseKey, true);
			w.Write((byte)x.MutationKind); w.Write((byte)x.ContainerKind);
			S(w, x.ContainerId, true); w.Write((byte)x.BeforeLocation);
			w.Write((byte)x.AfterLocation); S(w, x.BeforeOwnerId, true);
			S(w, x.AfterOwnerId, true); S(w, x.BeforeZoneId, false);
			S(w, x.AfterZoneId, false); w.Write(x.BeforeX); w.Write(x.BeforeY);
			w.Write(x.AfterX); w.Write(x.AfterY); w.Write(x.OwnerRemovedAfter);
			w.Write((byte)x.OwnerTopology); S(w, x.OwnerId, true);
			S(w, x.Blueprint, false); S(w, x.ZoneId, false); w.Write(x.X); w.Write(x.Y);
			w.Write(x.Capacity); w.Write(x.Before); w.Write(x.Delta); w.Write(x.After);
			S(w, x.BeforeComposition, false, true); S(w, x.AfterComposition, false, true);
			S(w, x.BeforeOwnerGraphHash, true);
			S(w, x.AfterOwnerGraphHash, true); S(w, x.BeforePartGraphHash, true);
			S(w, x.AfterPartGraphHash, true); S(w, x.BeforeTopologyHash, true);
			S(w, x.AfterTopologyHash, true); S(w, x.ReceiptId, false);
			w.Write(x.ReceiptBeforeMatches); w.Write(x.ReceiptAfterMatches);
			S(w, x.ReceiptBeforeOwnerGraphHash, true);
			S(w, x.ReceiptAfterOwnerGraphHash, true); S(w, x.ReceiptBeforePartGraphHash, true);
			S(w, x.ReceiptAfterPartGraphHash, true); S(w, x.ReceiptBeforeTopologyHash, true);
			S(w, x.ReceiptAfterTopologyHash, true); S(w, x.ReceiptCallbackContainerId, true);
			S(w, x.ReceiptCallbackReferenceHash, true); w.Write(x.ReceiptSameReference);
			S(w, x.ReceiptProofId, false);
			w.Write((byte)x.ReceiptState); w.Write((byte)x.State); WriteLease(w, x.Lease);
		}

		private static KingdomGrowthWaterLeg ReadGrowthWater(BinaryReader r)
		{
			return new KingdomGrowthWaterLeg
			{
				OperationId = S(r, true), EventId = S(r, true), LeaseKey = S(r, true),
				MutationKind = (KingdomGrowthWaterMutationKind)r.ReadByte(),
				ContainerKind = (KingdomGrowthWaterContainerKind)r.ReadByte(),
				ContainerId = S(r, true),
				BeforeLocation = (KingdomGrowthLocationKind)r.ReadByte(),
				AfterLocation = (KingdomGrowthLocationKind)r.ReadByte(),
				BeforeOwnerId = S(r, true), AfterOwnerId = S(r, true),
				BeforeZoneId = S(r, false), AfterZoneId = S(r, false),
				BeforeX = r.ReadInt32(), BeforeY = r.ReadInt32(),
				AfterX = r.ReadInt32(), AfterY = r.ReadInt32(),
				OwnerRemovedAfter = ReadExactBoolean(r),
				OwnerTopology = (KingdomLifecycleTopology)r.ReadByte(), OwnerId = S(r, true),
				Blueprint = S(r, false), ZoneId = S(r, false), X = r.ReadInt32(), Y = r.ReadInt32(),
				Capacity = r.ReadInt32(), Before = r.ReadInt32(), Delta = r.ReadInt32(),
				After = r.ReadInt32(), BeforeComposition = S(r, false, true),
				AfterComposition = S(r, false, true),
				BeforeOwnerGraphHash = S(r, true), AfterOwnerGraphHash = S(r, true),
				BeforePartGraphHash = S(r, true), AfterPartGraphHash = S(r, true),
				BeforeTopologyHash = S(r, true), AfterTopologyHash = S(r, true),
				ReceiptId = S(r, false), ReceiptBeforeMatches = r.ReadInt32(),
				ReceiptAfterMatches = r.ReadInt32(),
				ReceiptBeforeOwnerGraphHash = S(r, true), ReceiptAfterOwnerGraphHash = S(r, true),
				ReceiptBeforePartGraphHash = S(r, true), ReceiptAfterPartGraphHash = S(r, true),
				ReceiptBeforeTopologyHash = S(r, true), ReceiptAfterTopologyHash = S(r, true),
				ReceiptCallbackContainerId = S(r, true),
				ReceiptCallbackReferenceHash = S(r, true),
				ReceiptSameReference = ReadExactBoolean(r),
				ReceiptProofId = S(r, false),
				ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte(),
				State = (KingdomLifecyclePhysicalState)r.ReadByte(), Lease = ReadLease(r)
			};
		}

	}
}
