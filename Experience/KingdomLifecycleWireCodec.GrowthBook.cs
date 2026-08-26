using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{

		private static void WriteGrowth(BinaryWriter w, KingdomGrowthBook b)
		{
			WriteGrowth(w, b, KingdomLifecycleRules.CurrentGrowthFormatVersion);
		}

		private static void WriteGrowth(BinaryWriter w, KingdomGrowthBook b, int wireVersion)
		{
			if (wireVersion != KingdomLifecycleRules.CurrentGrowthFormatVersion
				&& wireVersion != KingdomLifecycleRules.PreviousGrowthFormatVersion
				&& wireVersion != KingdomLifecycleRules.LegacyGrowthFormatVersion)
				throw new InvalidDataException("unsupported growth fixture version");
			EnsureCount(b.FieldOps, KingdomLifecycleRules.MaxGrowthFields, "growth field slots");
			EnsureCount(b.CropRows, KingdomLifecycleRules.MaxGrowthCropRows, "growth crop rows");
			EnsureCount(b.Resources, KingdomLifecycleRules.MaxResourceRows, "growth resources");
			EnsureCount(b.RecentProofs, KingdomLifecycleRules.MaxRecentProofs, "growth proofs");
			w.Write(GrowthMagic); w.Write(wireVersion);
			w.Write(b.Quarantined); S(w, b.Fault, false, true);
			S(w, b.SettlementId, true); w.Write(b.IdentityBound); S(w, b.IdentityProof, true);
			w.Write(b.MigratedFromLifecycleVersion); w.Write(b.MigrationPending);
			w.Write(b.MigrationTick);
			w.Write((byte)b.OptionState); w.Write(b.OptionTick);
			w.Write((byte)b.HealthState); w.Write(b.HealthTick);
			w.Write((byte)b.ScarcityOptionState); w.Write(b.ScarcityOptionTick);
			w.Write(b.WorkPaused);
			w.Write(b.WorkPauseStartedTick); w.Write(b.WorkPausedTicks); w.Write(b.EffectiveWorkTick);
			w.Write(b.LastHeartbeatTick); w.Write(b.NextArrivalTick);
			w.Write(b.ArrivalIntervalTicks); w.Write(b.LastFetchTick);
			w.Write(b.LastMillTick); w.Write(b.LastSubsidenceTick);
			w.Write(b.LastDeliveryTick); w.Write(b.LastDepartureTick); w.Write(b.PendingCrop);
			S(w, b.PendingCropBlueprint, false); S(w, b.PendingCropZoneId, false);
			w.Write(b.HeartbeatNextSequence); w.Write(b.HeartbeatRetiredThrough);
			w.Write(b.ArrivalNextSequence); w.Write(b.ArrivalRetiredThrough);
			w.Write(b.DepartureNextSequence); w.Write(b.DepartureRetiredThrough);
			w.Write(b.DeliveryNextSequence); w.Write(b.DeliveryRetiredThrough);
			w.Write(b.FetchNextSequence); w.Write(b.FetchRetiredThrough);
			w.Write(b.MillNextSequence); w.Write(b.MillRetiredThrough);
			w.Write(b.ArrivalCandidateNextSequence); w.Write(b.ArrivalCandidateRetiredThrough);
			WriteGrowthOperation(w, b.HeartbeatOp, wireVersion);
			WriteGrowthOperation(w, b.ArrivalOp, wireVersion);
			WriteGrowthOperation(w, b.DepartureOp, wireVersion);
			WriteGrowthOperation(w, b.DeliveryOp, wireVersion);
			WriteGrowthOperation(w, b.FetchOp, wireVersion);
			WriteGrowthOperation(w, b.MillOp, wireVersion);
			WriteGrowthArrivalCandidate(w, b.ArrivalCandidate, wireVersion);
			w.Write(b.FieldOps.Count);
			for (int i = 0; i < b.FieldOps.Count; i++)
				WriteGrowthField(w, b.FieldOps[i], wireVersion);
			w.Write(b.CropRows.Count);
			for (int i = 0; i < b.CropRows.Count; i++) WriteCropRow(w, b.CropRows[i]);
			w.Write(b.Resources.Count);
			for (int i = 0; i < b.Resources.Count; i++) WriteResource(w, b.Resources[i]);
			w.Write(b.RecentProofs.Count);
			for (int i = 0; i < b.RecentProofs.Count; i++) WriteGrowthProof(w, b.RecentProofs[i]);
		}

		private static KingdomGrowthBook ReadGrowth(BinaryReader r, int wireVersion)
		{
			KingdomGrowthBook b = new KingdomGrowthBook
			{
				FormatVersion = KingdomLifecycleRules.CurrentGrowthFormatVersion,
				Quarantined = ReadExactBoolean(r), Fault = S(r, false, true),
				SettlementId = S(r, true), IdentityBound = ReadExactBoolean(r),
				IdentityProof = S(r, true), MigratedFromLifecycleVersion = r.ReadInt32(),
				MigrationPending = ReadExactBoolean(r), MigrationTick = r.ReadInt64(),
				OptionState = (KingdomLifecycleOptionState)r.ReadByte(),
				OptionTick = r.ReadInt64(), HealthState = (KingdomGrowthHealthState)r.ReadByte(),
				HealthTick = r.ReadInt64(),
				ScarcityOptionState = (KingdomLifecycleOptionState)r.ReadByte(),
				ScarcityOptionTick = r.ReadInt64(), WorkPaused = ReadExactBoolean(r),
				WorkPauseStartedTick = r.ReadInt64(), WorkPausedTicks = r.ReadInt64(),
				EffectiveWorkTick = r.ReadInt64(),
				LastHeartbeatTick = r.ReadInt64(), NextArrivalTick = r.ReadInt64(),
				ArrivalIntervalTicks = r.ReadInt64(), LastFetchTick = r.ReadInt64(),
				LastMillTick = r.ReadInt64(),
				LastSubsidenceTick = r.ReadInt64(), LastDeliveryTick = r.ReadInt64(),
				LastDepartureTick = r.ReadInt64(), PendingCrop = r.ReadInt32(),
				PendingCropBlueprint = S(r, false), PendingCropZoneId = S(r, false),
				HeartbeatNextSequence = r.ReadInt64(), HeartbeatRetiredThrough = r.ReadInt64(),
				ArrivalNextSequence = r.ReadInt64(), ArrivalRetiredThrough = r.ReadInt64(),
				DepartureNextSequence = r.ReadInt64(), DepartureRetiredThrough = r.ReadInt64(),
				DeliveryNextSequence = r.ReadInt64(), DeliveryRetiredThrough = r.ReadInt64(),
				FetchNextSequence = r.ReadInt64(), FetchRetiredThrough = r.ReadInt64(),
				MillNextSequence = r.ReadInt64(), MillRetiredThrough = r.ReadInt64(),
				ArrivalCandidateNextSequence = r.ReadInt64(),
				ArrivalCandidateRetiredThrough = r.ReadInt64(),
				HeartbeatOp = ReadGrowthOperation(r, wireVersion),
				ArrivalOp = ReadGrowthOperation(r, wireVersion),
				DepartureOp = ReadGrowthOperation(r, wireVersion),
				DeliveryOp = ReadGrowthOperation(r, wireVersion),
				FetchOp = ReadGrowthOperation(r, wireVersion),
				MillOp = ReadGrowthOperation(r, wireVersion),
				ArrivalCandidate = ReadGrowthArrivalCandidate(r, wireVersion)
			};
			int fields = ReadCount(r, KingdomLifecycleRules.MaxGrowthFields);
			b.FieldOps = new List<KingdomGrowthFieldSlot>(fields);
			for (int i = 0; i < fields; i++) b.FieldOps.Add(ReadGrowthField(r, wireVersion));
			int crops = ReadCount(r, KingdomLifecycleRules.MaxGrowthCropRows);
			b.CropRows = new List<KingdomGrowthCropRow>(crops);
			for (int i = 0; i < crops; i++) b.CropRows.Add(ReadCropRow(r));
			int resources = ReadCount(r, KingdomLifecycleRules.MaxResourceRows);
			b.Resources = new List<KingdomLifecycleResourceRevision>(resources);
			for (int i = 0; i < resources; i++) b.Resources.Add(ReadResource(r));
			int proofs = ReadCount(r, KingdomLifecycleRules.MaxRecentProofs);
			b.RecentProofs = new List<KingdomGrowthProof>(proofs);
			for (int i = 0; i < proofs; i++) b.RecentProofs.Add(ReadGrowthProof(r));
			if (wireVersion == KingdomLifecycleRules.LegacyGrowthFormatVersion
				&& !KingdomLifecycleRules.UpgradeLegacyGrowthArrivalCandidate(
					b.ArrivalCandidate))
				throw new InvalidDataException("legacy growth arrival candidate cannot migrate");
			return b;
		}
	}
}
