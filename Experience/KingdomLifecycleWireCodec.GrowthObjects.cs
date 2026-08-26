using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{
		private static void WriteGrowthObject(BinaryWriter w, KingdomGrowthObjectLeg x)
		{
			if (x == null) throw new InvalidDataException("null growth object leg");
			S(w, x.OperationId, true); S(w, x.EventId, true); S(w, x.ObjectId, true);
			S(w, x.Marker, true); S(w, x.Blueprint, false); S(w, x.ZoneId, false);
			w.Write((byte)x.Topology); S(w, x.OwnerId, true); w.Write(x.X); w.Write(x.Y);
			w.Write(x.BeforeCount); w.Write(x.Delta); w.Write(x.AfterCount); w.Write(x.NoStack);
			w.Write((byte)x.MutationKind); S(w, x.BeforeOwnerGraphHash, true);
			S(w, x.AfterOwnerGraphHash, true); S(w, x.BeforeObjectGraphHash, true);
			S(w, x.AfterObjectGraphHash, true); S(w, x.BeforeTopologyHash, true);
			S(w, x.AfterTopologyHash, true); S(w, x.CreatedMarker, true);
			S(w, x.DetachedMarker, true);
			w.Write((byte)x.BeforeLocation);
			w.Write((byte)x.AfterLocation); S(w, x.EscrowKey, true);
			w.Write(x.CallbackCursor);
			EnsureCount(x.Callbacks, KingdomLifecycleRules.MaxGrowthObjectCallbacks,
				"growth object callbacks");
			w.Write(x.Callbacks.Count);
			for (int i = 0; i < x.Callbacks.Count; i++) WriteGrowthObjectCallback(w,
				x.Callbacks[i]);
			WriteLease(w, x.Lease);
			w.Write((byte)x.State); S(w, x.ReceiptId, false); S(w, x.ReceiptTopologyId, false);
			w.Write(x.ReceiptBeforeIdMatches); w.Write(x.ReceiptBeforeMarkerMatches);
			w.Write(x.ReceiptBeforeCount); w.Write(x.ReceiptAfterIdMatches);
			w.Write(x.ReceiptAfterMarkerMatches); w.Write(x.ReceiptAfterCount);
			S(w, x.ReceiptBeforeOwnerGraphHash, true);
			S(w, x.ReceiptAfterOwnerGraphHash, true); S(w, x.ReceiptBeforeObjectGraphHash, true);
			S(w, x.ReceiptAfterObjectGraphHash, true); S(w, x.ReceiptBeforeTopologyHash, true);
			S(w, x.ReceiptAfterTopologyHash, true); S(w, x.ReceiptCallbackObjectId, true);
			S(w, x.ReceiptCallbackMarker, true); S(w, x.ReceiptCallbackReferenceHash, true);
			w.Write(x.ReceiptSameReference); S(w, x.ReceiptProofId, false);
			w.Write((byte)x.ReceiptState);
		}

		private static KingdomGrowthObjectLeg ReadGrowthObject(BinaryReader r)
		{
			KingdomGrowthObjectLeg x = new KingdomGrowthObjectLeg
			{
				OperationId = S(r, true), EventId = S(r, true), ObjectId = S(r, true),
				Marker = S(r, true), Blueprint = S(r, false), ZoneId = S(r, false),
				Topology = (KingdomLifecycleTopology)r.ReadByte(), OwnerId = S(r, true),
				X = r.ReadInt32(), Y = r.ReadInt32(), BeforeCount = r.ReadInt32(),
				Delta = r.ReadInt32(), AfterCount = r.ReadInt32(), NoStack = ReadExactBoolean(r),
				MutationKind = (KingdomGrowthObjectMutationKind)r.ReadByte(),
				BeforeOwnerGraphHash = S(r, true), AfterOwnerGraphHash = S(r, true),
				BeforeObjectGraphHash = S(r, true), AfterObjectGraphHash = S(r, true),
				BeforeTopologyHash = S(r, true), AfterTopologyHash = S(r, true),
				CreatedMarker = S(r, true), DetachedMarker = S(r, true),
				BeforeLocation = (KingdomGrowthLocationKind)r.ReadByte(),
				AfterLocation = (KingdomGrowthLocationKind)r.ReadByte(), EscrowKey = S(r, true),
				CallbackCursor = r.ReadInt32()
			};
			int callbacks = ReadCount(r, KingdomLifecycleRules.MaxGrowthObjectCallbacks);
			x.Callbacks = new List<KingdomGrowthObjectCallbackStep>(callbacks);
			for (int i = 0; i < callbacks; i++) x.Callbacks.Add(ReadGrowthObjectCallback(r));
			x.Lease = ReadLease(r);
			x.State = (KingdomLifecyclePhysicalState)r.ReadByte(); x.ReceiptId = S(r, false);
			x.ReceiptTopologyId = S(r, false); x.ReceiptBeforeIdMatches = r.ReadInt32();
			x.ReceiptBeforeMarkerMatches = r.ReadInt32(); x.ReceiptBeforeCount = r.ReadInt32();
			x.ReceiptAfterIdMatches = r.ReadInt32(); x.ReceiptAfterMarkerMatches = r.ReadInt32();
			x.ReceiptAfterCount = r.ReadInt32();
			x.ReceiptBeforeOwnerGraphHash = S(r, true); x.ReceiptAfterOwnerGraphHash = S(r, true);
			x.ReceiptBeforeObjectGraphHash = S(r, true); x.ReceiptAfterObjectGraphHash = S(r, true);
			x.ReceiptBeforeTopologyHash = S(r, true); x.ReceiptAfterTopologyHash = S(r, true);
			x.ReceiptCallbackObjectId = S(r, true); x.ReceiptCallbackMarker = S(r, true);
			x.ReceiptCallbackReferenceHash = S(r, true);
			x.ReceiptSameReference = ReadExactBoolean(r); x.ReceiptProofId = S(r, false);
			x.ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte();
			return x;
		}

		private static void WriteGrowthObjectCallback(BinaryWriter w,
			KingdomGrowthObjectCallbackStep x)
		{
			if (x == null) throw new InvalidDataException("null growth object callback");
			S(w, x.EventId, true); w.Write((byte)x.Kind); w.Write((byte)x.FromLocation);
			w.Write((byte)x.ToLocation); S(w, x.EscrowKey, true); S(w, x.BeforeOwnerId, true);
			S(w, x.AfterOwnerId, true); S(w, x.BeforeZoneId, false); S(w, x.AfterZoneId, false);
			w.Write(x.BeforeX); w.Write(x.BeforeY); w.Write(x.AfterX); w.Write(x.AfterY);
			w.Write(x.BeforeCount);
			w.Write(x.AfterCount); w.Write(x.NoStack); w.Write(x.BeforeHasHarvestable);
			w.Write(x.AfterHasHarvestable); w.Write(x.BeforeRipe); w.Write(x.AfterRipe);
			w.Write(x.BeforeRegenTimer); w.Write(x.AfterRegenTimer);
			S(w, x.BeforeRegenTime, false); S(w, x.AfterRegenTime, false);
			w.Write(x.BeforeTileIndex); w.Write(x.AfterTileIndex);
			S(w, x.BeforeRenderTile, false); S(w, x.AfterRenderTile, false);
			S(w, x.BeforeRenderColor, false); S(w, x.AfterRenderColor, false);
			S(w, x.BeforeRenderDetail, false); S(w, x.AfterRenderDetail, false);
			S(w, x.BeforeRenderString, false); S(w, x.AfterRenderString, false);
			S(w, x.BeforeTileColor, false); S(w, x.AfterTileColor, false);
			S(w, x.BeforeOwnerGraphHash, true);
			S(w, x.AfterOwnerGraphHash, true); S(w, x.BeforeObjectGraphHash, true);
			S(w, x.AfterObjectGraphHash, true); S(w, x.BeforeTopologyHash, true);
			S(w, x.AfterTopologyHash, true); w.Write((byte)x.State); S(w, x.ReceiptId, true);
			w.Write(x.ReceiptBeforeMatches); w.Write(x.ReceiptAfterMatches);
			w.Write(x.ReceiptBeforeCount); w.Write(x.ReceiptAfterCount);
			S(w, x.ReceiptCallbackObjectId, true); S(w, x.ReceiptCallbackMarker, true);
			S(w, x.ReceiptCallbackReferenceHash, true); w.Write(x.ReceiptSameReference);
			S(w, x.ReceiptBeforeOwnerGraphHash, true); S(w, x.ReceiptAfterOwnerGraphHash, true);
			S(w, x.ReceiptBeforeObjectGraphHash, true); S(w, x.ReceiptAfterObjectGraphHash, true);
			S(w, x.ReceiptBeforeTopologyHash, true); S(w, x.ReceiptAfterTopologyHash, true);
			S(w, x.ReceiptProofId, true); w.Write((byte)x.ReceiptState);
		}

		private static KingdomGrowthObjectCallbackStep ReadGrowthObjectCallback(BinaryReader r)
		{
			return new KingdomGrowthObjectCallbackStep
			{
				EventId = S(r, true), Kind = (KingdomGrowthObjectMutationKind)r.ReadByte(),
				FromLocation = (KingdomGrowthLocationKind)r.ReadByte(),
				ToLocation = (KingdomGrowthLocationKind)r.ReadByte(), EscrowKey = S(r, true),
				BeforeOwnerId = S(r, true), AfterOwnerId = S(r, true),
				BeforeZoneId = S(r, false), AfterZoneId = S(r, false),
				BeforeX = r.ReadInt32(), BeforeY = r.ReadInt32(),
				AfterX = r.ReadInt32(), AfterY = r.ReadInt32(),
				BeforeCount = r.ReadInt32(), AfterCount = r.ReadInt32(),
				NoStack = ReadExactBoolean(r), BeforeHasHarvestable = ReadExactBoolean(r),
				AfterHasHarvestable = ReadExactBoolean(r), BeforeRipe = ReadExactBoolean(r),
				AfterRipe = ReadExactBoolean(r), BeforeRegenTimer = r.ReadInt32(),
				AfterRegenTimer = r.ReadInt32(), BeforeRegenTime = S(r, false),
				AfterRegenTime = S(r, false), BeforeTileIndex = r.ReadInt32(),
				AfterTileIndex = r.ReadInt32(), BeforeRenderTile = S(r, false),
				AfterRenderTile = S(r, false), BeforeRenderColor = S(r, false),
				AfterRenderColor = S(r, false), BeforeRenderDetail = S(r, false),
				AfterRenderDetail = S(r, false), BeforeRenderString = S(r, false),
				AfterRenderString = S(r, false), BeforeTileColor = S(r, false),
				AfterTileColor = S(r, false),
				BeforeOwnerGraphHash = S(r, true), AfterOwnerGraphHash = S(r, true),
				BeforeObjectGraphHash = S(r, true), AfterObjectGraphHash = S(r, true),
				BeforeTopologyHash = S(r, true), AfterTopologyHash = S(r, true),
				State = (KingdomLifecyclePhysicalState)r.ReadByte(), ReceiptId = S(r, true),
				ReceiptBeforeMatches = r.ReadInt32(), ReceiptAfterMatches = r.ReadInt32(),
				ReceiptBeforeCount = r.ReadInt32(), ReceiptAfterCount = r.ReadInt32(),
				ReceiptCallbackObjectId = S(r, true), ReceiptCallbackMarker = S(r, true),
				ReceiptCallbackReferenceHash = S(r, true),
				ReceiptSameReference = ReadExactBoolean(r),
				ReceiptBeforeOwnerGraphHash = S(r, true), ReceiptAfterOwnerGraphHash = S(r, true),
				ReceiptBeforeObjectGraphHash = S(r, true), ReceiptAfterObjectGraphHash = S(r, true),
				ReceiptBeforeTopologyHash = S(r, true), ReceiptAfterTopologyHash = S(r, true),
				ReceiptProofId = S(r, true),
				ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte()
			};
		}

	}
}
