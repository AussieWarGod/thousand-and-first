using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{
		private static void WriteGrowthField(BinaryWriter w, KingdomGrowthFieldSlot x,
			int wireVersion)
		{
			if (x == null) throw new InvalidDataException("null growth field slot");
			S(w, x.FieldId, true); w.Write(x.NextSequence); w.Write(x.RetiredThrough);
			w.Write(x.ClockTick); w.Write(x.CommitRevision); S(w, x.LastOperationId, true);
			S(w, x.WorkObjectId, true); S(w, x.WorkPartId, true); S(w, x.Marker, true);
			S(w, x.Blueprint, false); S(w, x.ZoneId, false); w.Write(x.X); w.Write(x.Y);
			S(w, x.CropBlueprint, false); w.Write(x.Stage); w.Write(x.NextStageTick);
			w.Write(x.SownTick); w.Write(x.Cycles); w.Write(x.SaidWant);
			w.Write(x.DeclaredRows); w.Write(x.EffectivenessPercent); w.Write(x.MethodPercent);
			w.Write(x.NoLarderAnnounced); S(w, x.SeedBlueprint, false);
			S(w, x.PartGraphHash, true); S(w, x.ObjectGraphHash, true);
			S(w, x.TopologyHash, true);
			w.Write(x.Quarantined); S(w, x.Fault, false, true);
			WriteGrowthOperation(w, x.Operation, wireVersion);
		}

		private static void WriteGrowthFieldState(BinaryWriter w, KingdomGrowthFieldState x)
		{
			w.Write(x != null); if (x == null) return;
			S(w, x.FieldId, true); S(w, x.WorkObjectId, true); S(w, x.WorkPartId, true);
			S(w, x.Marker, true); S(w, x.Blueprint, false); S(w, x.ZoneId, false);
			w.Write(x.X); w.Write(x.Y); S(w, x.CropBlueprint, false); w.Write(x.Stage);
			w.Write(x.NextStageTick); w.Write(x.SownTick); w.Write(x.Cycles);
			w.Write(x.SaidWant); w.Write(x.DeclaredRows); w.Write(x.EffectivenessPercent);
			w.Write(x.MethodPercent); w.Write(x.NoLarderAnnounced);
			S(w, x.SeedBlueprint, false); S(w, x.PartGraphHash, true);
			S(w, x.ObjectGraphHash, true); S(w, x.TopologyHash, true);
		}

		private static KingdomGrowthFieldState ReadGrowthFieldState(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			return new KingdomGrowthFieldState
			{
				FieldId = S(r, true), WorkObjectId = S(r, true), WorkPartId = S(r, true),
				Marker = S(r, true), Blueprint = S(r, false), ZoneId = S(r, false),
				X = r.ReadInt32(), Y = r.ReadInt32(), CropBlueprint = S(r, false),
				Stage = r.ReadInt32(), NextStageTick = r.ReadInt64(), SownTick = r.ReadInt64(),
					Cycles = r.ReadInt32(), SaidWant = r.ReadInt32(), DeclaredRows = r.ReadInt32(),
					EffectivenessPercent = r.ReadInt32(), MethodPercent = r.ReadInt32(),
				NoLarderAnnounced = ReadExactBoolean(r), SeedBlueprint = S(r, false),
				PartGraphHash = S(r, true), ObjectGraphHash = S(r, true),
				TopologyHash = S(r, true)
			};
		}

		private static void WriteGrowthCropRows(BinaryWriter w,
			List<KingdomGrowthCropRow> rows)
		{
			w.Write(rows != null); if (rows == null) return;
			EnsureCount(rows, KingdomLifecycleRules.MaxGrowthCropRows, "growth domain crop rows");
			w.Write(rows.Count);
			for (int i = 0; i < rows.Count; i++) WriteCropRow(w, rows[i]);
		}

		private static List<KingdomGrowthCropRow> ReadGrowthCropRows(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			int count = ReadCount(r, KingdomLifecycleRules.MaxGrowthCropRows);
			List<KingdomGrowthCropRow> rows = new List<KingdomGrowthCropRow>(count);
			for (int i = 0; i < count; i++) rows.Add(ReadCropRow(r));
			return rows;
		}

		private static KingdomGrowthFieldSlot ReadGrowthField(BinaryReader r, int wireVersion)
		{
			return new KingdomGrowthFieldSlot
			{
				FieldId = S(r, true), NextSequence = r.ReadInt64(), RetiredThrough = r.ReadInt64(),
				ClockTick = r.ReadInt64(), CommitRevision = r.ReadInt64(),
				LastOperationId = S(r, true), WorkObjectId = S(r, true), WorkPartId = S(r, true),
				Marker = S(r, true), Blueprint = S(r, false), ZoneId = S(r, false),
				X = r.ReadInt32(), Y = r.ReadInt32(), CropBlueprint = S(r, false),
				Stage = r.ReadInt32(), NextStageTick = r.ReadInt64(), SownTick = r.ReadInt64(),
					Cycles = r.ReadInt32(), SaidWant = r.ReadInt32(), DeclaredRows = r.ReadInt32(),
					EffectivenessPercent = r.ReadInt32(), MethodPercent = r.ReadInt32(),
				NoLarderAnnounced = ReadExactBoolean(r), SeedBlueprint = S(r, false),
				PartGraphHash = S(r, true), ObjectGraphHash = S(r, true),
				TopologyHash = S(r, true),
				Quarantined = ReadExactBoolean(r), Fault = S(r, false, true),
				Operation = ReadGrowthOperation(r, wireVersion)
			};
		}

		private static void WriteCropRow(BinaryWriter w, KingdomGrowthCropRow x)
		{
			if (x == null) throw new InvalidDataException("null growth crop row");
			S(w, x.FieldId, true); S(w, x.RowId, true); S(w, x.ObjectId, true);
			S(w, x.Marker, true); S(w, x.Blueprint, false); S(w, x.ZoneId, false);
			S(w, x.OwnerId, true); w.Write(x.X); w.Write(x.Y); w.Write(x.Count);
			w.Write(x.HasHarvestable); w.Write(x.Ripe); w.Write(x.RegenTimer);
			S(w, x.RegenTime, false); w.Write(x.TileIndex); S(w, x.RenderTile, false);
			S(w, x.RenderColor, false); S(w, x.RenderDetail, false);
			S(w, x.RenderString, false); S(w, x.TileColor, false);
			S(w, x.PartGraphHash, true); S(w, x.ObjectGraphHash, true);
			S(w, x.TopologyHash, true); w.Write(x.Revision); S(w, x.LastOperationId, true);
		}

		private static KingdomGrowthCropRow ReadCropRow(BinaryReader r)
		{
			return new KingdomGrowthCropRow
			{
				FieldId = S(r, true), RowId = S(r, true), ObjectId = S(r, true),
				Marker = S(r, true), Blueprint = S(r, false), ZoneId = S(r, false),
				OwnerId = S(r, true), X = r.ReadInt32(), Y = r.ReadInt32(), Count = r.ReadInt32(),
				HasHarvestable = ReadExactBoolean(r), Ripe = ReadExactBoolean(r),
				RegenTimer = r.ReadInt32(), RegenTime = S(r, false), TileIndex = r.ReadInt32(),
				RenderTile = S(r, false), RenderColor = S(r, false), RenderDetail = S(r, false),
				RenderString = S(r, false), TileColor = S(r, false),
				PartGraphHash = S(r, true),
				ObjectGraphHash = S(r, true), TopologyHash = S(r, true),
				Revision = r.ReadInt64(), LastOperationId = S(r, true)
			};
		}

		private static void WriteGrowthProof(BinaryWriter w, KingdomGrowthProof x)
		{
			if (x == null) throw new InvalidDataException("null growth proof");
			w.Write((byte)x.Slot); S(w, x.FieldId, true); w.Write(x.Sequence);
			S(w, x.Id, true); S(w, x.PlanHash, true); w.Write((byte)x.Action); w.Write(x.Tick);
		}

		private static KingdomGrowthProof ReadGrowthProof(BinaryReader r)
		{
			return new KingdomGrowthProof
			{
				Slot = (KingdomGrowthSlotKind)r.ReadByte(), FieldId = S(r, true),
				Sequence = r.ReadInt64(), Id = S(r, true), PlanHash = S(r, true),
				Action = (KingdomGrowthAction)r.ReadByte(), Tick = r.ReadInt64()
			};
		}

	}
}
