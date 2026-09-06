using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal static class KingdomSubsidenceRungCodec
	{
		internal const int MaxWireChars = 524288;
		private const int LegacyMagic = 0x31525354;
		private const int Magic = 0x32525354;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static bool TryEncode(KingdomSubsidenceRungPlan plan, out string wire)
		{
			return TryEncodeVersion(plan, 2, out wire);
		}

		private static bool TryEncodeVersion(KingdomSubsidenceRungPlan plan, int version, out string wire)
		{
			wire = null;
			if (version != 1 && version != 2 || !KingdomSubsidenceRungRules.Valid(plan)) return false;
			if (version == 1)
				foreach (KingdomSubsidenceRungWork work in plan.Works)
					if (work.ReleasePhase != KingdomSubsidenceReleasePhase.Pending
						|| work.ReleaseBefore != null || work.ReleaseAfter != null) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Utf8, true))
				{
					writer.Write(version == 1 ? LegacyMagic : Magic); writer.Write(plan.StepId); writer.Write(plan.RealmId);
					writer.Write(plan.SettlementId); writer.Write(plan.ZoneId);
					writer.Write((byte)plan.From); writer.Write((byte)plan.To);
					writer.Write(plan.DueTick); writer.Write(plan.PreparedTick); writer.Write(plan.Departed);
					writer.Write(plan.Works.Count);
					foreach (KingdomSubsidenceRungWork work in plan.Works) Write(writer, work, version);
					writer.Flush();
					if (stream.Length > MaxWireChars / 4 * 3 - 3) return false;
					wire = (version == 1 ? "sr1:" : "sr2:") + Convert.ToBase64String(stream.ToArray());
					return wire.Length <= MaxWireChars;
				}
			}
			catch { wire = null; return false; }
		}

		internal static bool TryDecode(string wire, out KingdomSubsidenceRungPlan plan)
		{
			plan = null;
			if (string.IsNullOrEmpty(wire) || wire.Length > MaxWireChars) return false;
			int version = wire.StartsWith("sr1:", StringComparison.Ordinal) ? 1
				: wire.StartsWith("sr2:", StringComparison.Ordinal) ? 2 : 0;
			if (version == 0) return false;
			try
			{
				byte[] bytes = Convert.FromBase64String(wire.Substring(4));
				using (MemoryStream stream = new MemoryStream(bytes, false))
				using (BinaryReader reader = new BinaryReader(stream, Utf8, true))
				{
					if (reader.ReadInt32() != (version == 1 ? LegacyMagic : Magic)) return false;
					string step = reader.ReadString(), realm = reader.ReadString();
					string settlement = reader.ReadString(), zone = reader.ReadString();
					GrowthStage from = (GrowthStage)reader.ReadByte(), to = (GrowthStage)reader.ReadByte();
					long due = reader.ReadInt64(), prepared = reader.ReadInt64();
					int departed = reader.ReadInt32(), count = Count(reader, KingdomSubsidenceRungRules.MaxWorks);
					List<KingdomSubsidenceRungWork> works = new List<KingdomSubsidenceRungWork>();
					int roofCount = 0;
					for (int i = 0; i < count; i++) works.Add(Read(reader, ref roofCount, version));
					if (stream.Position != stream.Length) return false;
					KingdomSubsidenceRungPlan value = new KingdomSubsidenceRungPlan(step, realm,
						settlement, zone, from, to, due, prepared, departed, works);
					if (!TryEncodeVersion(value, version, out string canonical) || canonical != wire) return false;
					plan = value; return true;
				}
			}
			catch { return false; }
		}

		private static void Write(BinaryWriter writer, KingdomSubsidenceRungWork work, int version)
		{
			writer.Write(work.WorkId); writer.Write(work.ObjectId); writer.Write(work.Blueprint);
			writer.Write(work.PlotId); writer.Write((byte)(work.DesignStamp == null ? 0 : 1));
			if (work.DesignStamp != null) writer.Write(work.DesignStamp);
			writer.Write(work.Name); writer.Write(work.X); writer.Write(work.Y);
			writer.Write((byte)(work.HadWearPart ? 1 : 0)); writer.Write(work.BeforeWear);
			writer.Write(work.AfterWear); writer.Write((byte)work.WearPhase); writer.Write(work.Roofs.Count);
			foreach (KingdomSubsidenceRungRoof roof in work.Roofs)
			{
				writer.Write(roof.ResidentId); writer.Write(roof.BodyObjectId);
				writer.Write((byte)(roof.BeforeStanding ? 1 : 0)); writer.Write(roof.BeforeReached);
				writer.Write(roof.BeforeWarned); writer.Write((byte)roof.Phase);
			}
			if (version == 1) return;
			writer.Write((byte)work.ReleasePhase);
			if (work.ReleasePhase == KingdomSubsidenceReleasePhase.Pending)
			{
				if (work.ReleaseBefore != null || work.ReleaseAfter != null) throw new InvalidDataException();
				return;
			}
			if (work.ReleasePhase != KingdomSubsidenceReleasePhase.Intent
				&& work.ReleasePhase != KingdomSubsidenceReleasePhase.Released
				|| work.ReleaseBefore == null || work.ReleaseAfter == null) throw new InvalidDataException();
			WriteReceipt(writer, work.ReleaseBefore); WriteReceipt(writer, work.ReleaseAfter);
		}

		private static KingdomSubsidenceRungWork Read(BinaryReader reader, ref int roofCount, int version)
		{
			int work = reader.ReadInt32(); string body = reader.ReadString(), blueprint = reader.ReadString();
			string plot = reader.ReadString(), design = Flag(reader) ? reader.ReadString() : null;
			string name = reader.ReadString(); int x = reader.ReadInt32(), y = reader.ReadInt32();
			bool part = Flag(reader); int before = reader.ReadInt32(), after = reader.ReadInt32();
			KingdomSubsidenceEffectPhase phase = (KingdomSubsidenceEffectPhase)reader.ReadByte();
			int count = Count(reader, KingdomSubsidenceRungRules.MaxRoofs);
			roofCount += count;
			if (roofCount > KingdomSubsidenceRungRules.MaxRoofs) throw new InvalidDataException();
			List<KingdomSubsidenceRungRoof> roofs = new List<KingdomSubsidenceRungRoof>();
			for (int i = 0; i < count; i++) roofs.Add(new KingdomSubsidenceRungRoof(
				reader.ReadInt32(), reader.ReadString(), Flag(reader), reader.ReadInt64(),
				reader.ReadInt64(), (KingdomSubsidenceEffectPhase)reader.ReadByte()));
			KingdomSubsidenceReleasePhase release = KingdomSubsidenceReleasePhase.Pending;
			KingdomSubsidenceWearReceipt releaseBefore = null, releaseAfter = null;
			if (version == 2)
			{
				release = (KingdomSubsidenceReleasePhase)reader.ReadByte();
				if (release > KingdomSubsidenceReleasePhase.Released) throw new InvalidDataException();
				if (release != KingdomSubsidenceReleasePhase.Pending)
				{
					releaseBefore = ReadReceipt(reader); releaseAfter = ReadReceipt(reader);
				}
			}
			return new KingdomSubsidenceRungWork(work, body, blueprint, plot, design, name, x, y,
				part, before, after, phase, roofs, release, releaseBefore, releaseAfter);
		}

		private static void WriteReceipt(BinaryWriter writer, KingdomSubsidenceWearReceipt receipt)
		{
			writer.Write(receipt.Phase); WriteNullable(writer, receipt.Id); writer.Write(receipt.Cause);
			writer.Write(receipt.BeforeWear); writer.Write(receipt.AfterWear); writer.Write(receipt.Wear);
			writer.Write(receipt.LastCause); WriteNullable(writer, receipt.LastCompletedId);
			WriteNullable(writer, receipt.Line); writer.Write(receipt.MessageState);
		}

		private static KingdomSubsidenceWearReceipt ReadReceipt(BinaryReader reader)
		{
			return new KingdomSubsidenceWearReceipt(reader.ReadInt32(), ReadNullable(reader), reader.ReadInt32(),
				reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
				ReadNullable(reader), ReadNullable(reader), reader.ReadInt32());
		}

		private static void WriteNullable(BinaryWriter writer, string value)
		{
			writer.Write((byte)(value == null ? 0 : 1));
			if (value != null) writer.Write(value);
		}

		private static string ReadNullable(BinaryReader reader)
		{
			return Flag(reader) ? reader.ReadString() : null;
		}

		private static bool Flag(BinaryReader reader)
		{
			byte value = reader.ReadByte();
			if (value > 1) throw new InvalidDataException();
			return value == 1;
		}

		private static int Count(BinaryReader reader, int maximum)
		{
			int value = reader.ReadInt32();
			if (value < 0 || value > maximum) throw new InvalidDataException();
			return value;
		}
	}
}
