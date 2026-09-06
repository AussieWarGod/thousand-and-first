using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal static class KingdomSubsidenceAnnouncementCodec
	{
		internal const string None = "sa1:none";
		internal const int MaxWireChars = 262144;
		private const int Magic = 0x31415354;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
		internal static bool TryEncode(KingdomSubsidenceAnnouncement value, out string wire)
		{
			wire = null;
			if (!KingdomSubsidenceAnnouncementRules.Valid(value)) return false;
			if (value.Ordinal == 0) { wire = None; return true; }
			try
			{
				using (var stream = new MemoryStream())
				using (var writer = new BinaryWriter(stream, Utf8, true))
				{
					writer.Write(Magic); writer.Write(value.RealmId); writer.Write(value.SettlementId);
					writer.Write(value.Ordinal); writer.Write(value.LastTick); writer.Write((byte)(value.Active == null ? 0 : 1));
					if (value.Active != null)
					{
						var op = value.Active;
						writer.Write(op.Id); writer.Write(op.AtTick); writer.Write((byte)(op.Before ? 1 : 0));
						writer.Write((byte)(op.After ? 1 : 0)); writer.Write(op.Message); writer.Write(op.ReportModel);
						writer.Write((byte)(op.FlagProved ? 1 : 0)); writer.Write((byte)op.Notice);
					}
					writer.Flush();
					if (stream.Length > MaxWireChars / 4 * 3 - 3) return false;
					string result = "sa1:" + Convert.ToBase64String(stream.ToArray());
					if (result.Length > MaxWireChars) return false;
					wire = result; return true;
				}
			}
			catch { return false; }
		}
		internal static bool TryDecode(string wire, out KingdomSubsidenceAnnouncement value)
		{
			value = null;
			if (wire == None) { value = new KingdomSubsidenceAnnouncement("", "", 0, 0, null); return true; }
			if (wire == null || wire.Length > MaxWireChars || !wire.StartsWith("sa1:", StringComparison.Ordinal)) return false;
			try
			{
				using (var stream = new MemoryStream(Convert.FromBase64String(wire.Substring(4)), false))
				using (var reader = new BinaryReader(stream, Utf8, true))
				{
					if (reader.ReadInt32() != Magic) return false;
					string realm = reader.ReadString(), settlement = reader.ReadString();
					long ordinal = reader.ReadInt64(), lastTick = reader.ReadInt64();
					KingdomSubsidenceAnnouncementOperation op = null;
					if (Flag(reader)) op = new KingdomSubsidenceAnnouncementOperation(reader.ReadString(), reader.ReadInt64(),
						Flag(reader), Flag(reader), reader.ReadString(), reader.ReadString(), Flag(reader),
						(KingdomSubsidenceNoticePhase)reader.ReadByte());
					var parsed = new KingdomSubsidenceAnnouncement(realm, settlement, ordinal, lastTick, op);
					if (stream.Position != stream.Length || !TryEncode(parsed, out string canonical) || canonical != wire) return false;
					value = parsed; return true;
				}
			}
			catch { return false; }
		}
		private static bool Flag(BinaryReader reader)
		{ byte flag = reader.ReadByte(); if (flag > 1) throw new InvalidDataException(); return flag == 1; }
	}
}
