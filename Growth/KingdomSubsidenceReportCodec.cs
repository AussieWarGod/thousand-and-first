using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>The one canonical wire a report plan may be stored as. Length-framed strict UTF-8
	/// fields under an explicit magic: no separator sentinel exists, so no field content can forge
	/// a boundary. A decode that does not re-encode byte-identically is refused outright, which
	/// leaves exactly one wire per plan and no room for padding, truncation or trailing bytes.
	/// <para><c>st1:</c> is the original layout and cannot say a loss at all;
	/// <c>st2:</c> appends the two loss bytes to every entry. The writer is always the current
	/// version - a plan is never encoded at the lowest version that happens to fit it, because
	/// that makes the wire a function of the plan's contents rather than of the law, and an
	/// upgrade would then silently rewrite stored bytes. Each version keeps its own magic and its
	/// own decoder, and each decode is checked against its own encoder, so a body cannot be
	/// relabelled from one version to the other. <c>st3:</c> additionally frames the explicit
	/// capacity-refusal reason, count, registry hash and event fingerprint.</para></summary>
	internal static class KingdomSubsidenceReportCodec
	{
		internal const int MaxWireChars = 131072;
		private const string PrefixV1 = "st1:";
		private const string PrefixV2 = "st2:";
		private const string PrefixV3 = "st3:";
		private const int MagicV1 = 0x31545253;
		private const int MagicV2 = 0x32545253;
		private const int MagicV3 = 0x33545253;
		private const int VersionOne = 1;
		private const int VersionTwo = 2;
		private const int VersionThree = 3;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		/// <summary>Writes the one current wire. Older wires are read, never written.</summary>
		internal static bool TryEncode(KingdomSubsidenceReportPlan plan, out string wire)
		{
			return Encode(plan, VersionThree, out wire);
		}

		internal static bool TryDecode(string wire, out KingdomSubsidenceReportPlan plan)
		{
			plan = null;
			if (string.IsNullOrEmpty(wire) || wire.Length > MaxWireChars) return false;
			int version = wire.StartsWith(PrefixV3, StringComparison.Ordinal) ? VersionThree
				: wire.StartsWith(PrefixV2, StringComparison.Ordinal) ? VersionTwo
				: wire.StartsWith(PrefixV1, StringComparison.Ordinal) ? VersionOne : 0;
			if (version == 0) return false;
			try
			{
				byte[] bytes = Convert.FromBase64String(wire.Substring(PrefixV2.Length));
				using (MemoryStream stream = new MemoryStream(bytes, false))
				using (BinaryReader reader = new BinaryReader(stream, Utf8, true))
				{
					if (reader.ReadInt32() != Magic(version))
						return false;
					string owner = reader.ReadString(), realm = reader.ReadString();
					string settlement = reader.ReadString();
					int count = Count(reader, KingdomSubsidenceReportRules.MaxEntries);
					List<KingdomSubsidenceReportEntry> entries
						= new List<KingdomSubsidenceReportEntry>();
					for (int i = 0; i < count; i++) entries.Add(Read(reader, version));
					if (stream.Position != stream.Length) return false;
					KingdomSubsidenceReportPlan value = new KingdomSubsidenceReportPlan(owner,
						realm, settlement, entries);
					// Valid is re-run by the encoder, so an invalid frontier, an impossible loss
					// or a chronicle told and lost at once cannot survive a decode however the
					// bytes were assembled. The check is against this wire's own version.
					string canonical;
					bool framed = Encode(value, version, out canonical);
					if (!framed || !string.Equals(canonical, wire, StringComparison.Ordinal))
						return false;
					plan = value; return true;
				}
			}
			catch { plan = null; return false; }
		}

		private static bool EncodeV1(KingdomSubsidenceReportPlan plan, out string wire)
		{
			return Encode(plan, VersionOne, out wire);
		}

		private static bool EncodeV2(KingdomSubsidenceReportPlan plan, out string wire)
		{
			return Encode(plan, VersionTwo, out wire);
		}

		private static bool Encode(KingdomSubsidenceReportPlan plan, int version, out string wire)
		{
			wire = null;
			if (!KingdomSubsidenceReportRules.Valid(plan)) return false;
			// The first wire has no way to say a loss, so it is never asked to hold one: a lossy
			// encode would drop the very fact the plan exists to keep.
			if (version == VersionOne && KingdomSubsidenceReportRules.HasLoss(plan)) return false;
			if (version != VersionThree)
				foreach (KingdomSubsidenceReportEntry entry in plan.Entries)
					if (entry.CapacityRefused) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Utf8, true))
				{
					writer.Write(Magic(version));
					writer.Write(plan.OwnerId); writer.Write(plan.RealmId);
					writer.Write(plan.SettlementId); writer.Write(plan.Entries.Count);
					foreach (KingdomSubsidenceReportEntry entry in plan.Entries)
					{
						writer.Write(entry.Text); writer.Write(entry.LedgerText);
						writer.Write(entry.AtTick); writer.Write((byte)entry.LedgerPhase);
						writer.Write((byte)(entry.ChronicleProved ? 1 : 0));
						writer.Write(entry.BeforeCount); writer.Write(entry.BeforeHash);
						writer.Write(entry.AfterCount); writer.Write(entry.AfterHash);
						if (version >= VersionTwo)
						{
							writer.Write((byte)entry.LedgerLoss);
							writer.Write((byte)(entry.ChronicleLost ? 1 : 0));
						}
						if (version == VersionThree)
						{
							writer.Write((byte)entry.ChronicleRefusal); writer.Write(entry.CapacityCount);
							writer.Write(entry.CapacityHash); writer.Write(entry.CapacityFingerprint);
						}
					}
					writer.Flush();
					if (stream.Length > MaxWireChars / 4 * 3 - 3) return false;
					string value = (version == VersionThree ? PrefixV3 : version == VersionTwo ? PrefixV2 : PrefixV1)
						+ Convert.ToBase64String(stream.ToArray());
					if (value.Length > MaxWireChars) return false;
					wire = value; return true;
				}
			}
			catch { wire = null; return false; }
		}

		private static KingdomSubsidenceReportEntry Read(BinaryReader reader, int version)
		{
			string text = reader.ReadString(), ledger = reader.ReadString();
			long tick = reader.ReadInt64();
			ReportLedgerPhase phase = Phase(reader);
			bool chronicle = Flag(reader);
			int before = reader.ReadInt32();
			string beforeHash = reader.ReadString();
			int after = reader.ReadInt32();
			string afterHash = reader.ReadString();
			LedgerLossKind loss = version >= VersionTwo ? Loss(reader) : LedgerLossKind.None;
			bool chronicleLost = version >= VersionTwo && Flag(reader);
			ReportChronicleRefusal refusal = version == VersionThree ? Refusal(reader) : ReportChronicleRefusal.None;
			int capacityCount = version == VersionThree ? reader.ReadInt32() : 0;
			string capacityHash = version == VersionThree ? reader.ReadString() : "";
			string capacityFingerprint = version == VersionThree ? reader.ReadString() : "";
			return new KingdomSubsidenceReportEntry(text, ledger, tick, phase, chronicle, before,
				beforeHash, after, afterHash, loss, chronicleLost, refusal, capacityCount, capacityHash, capacityFingerprint);
		}

		private static int Magic(int version)
		{ return version == VersionThree ? MagicV3 : version == VersionTwo ? MagicV2 : MagicV1; }

		private static ReportChronicleRefusal Refusal(BinaryReader reader)
		{
			byte value = reader.ReadByte();
			if (value > (byte)ReportChronicleRefusal.CapacityRefused) throw new InvalidDataException();
			return (ReportChronicleRefusal)value;
		}

		private static ReportLedgerPhase Phase(BinaryReader reader)
		{
			byte value = reader.ReadByte();
			if (value > (byte)ReportLedgerPhase.Lost) throw new InvalidDataException();
			return (ReportLedgerPhase)value;
		}

		private static LedgerLossKind Loss(BinaryReader reader)
		{
			byte value = reader.ReadByte();
			if (value > (byte)LedgerLossKind.HomecomingReset) throw new InvalidDataException();
			return (LedgerLossKind)value;
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
