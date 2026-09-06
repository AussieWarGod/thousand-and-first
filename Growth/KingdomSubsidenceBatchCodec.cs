using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal static class KingdomSubsidenceBatchCodec
	{
		private const int Magic = 0x31425354;
		private const int MaxWireChars = 262144;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static bool TryEncode(KingdomSubsidenceBatch batch, out string wire)
		{
			wire = null;
			if (!KingdomSubsidenceBatchRules.Valid(batch)) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Utf8, true))
				{
					writer.Write(Magic); writer.Write(batch.Id); writer.Write(batch.RealmId);
					writer.Write(batch.SettlementId); writer.Write(batch.Name); writer.Write(batch.Binding);
					writer.Write(batch.FirstSequence); writer.Write(batch.AnchorTick); writer.Write(batch.ThroughTick);
					writer.Write(batch.Wanted); writer.Write(batch.Departed); writer.Write((byte)(batch.Closing ? 1 : 0));
					writer.Write(batch.ClosedTick); writer.Write(batch.ReportModel); writer.Flush();
					if (stream.Length > MaxWireChars / 4 * 3 - 3) return false;
					wire = "sb1:" + Convert.ToBase64String(stream.ToArray());
					return wire.Length <= MaxWireChars;
				}
			}
			catch { wire = null; return false; }
		}

		internal static bool TryDecode(string wire, out KingdomSubsidenceBatch batch)
		{
			batch = null;
			if (string.IsNullOrEmpty(wire) || wire.Length > MaxWireChars
				|| !wire.StartsWith("sb1:", StringComparison.Ordinal)) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream(Convert.FromBase64String(wire.Substring(4)), false))
				using (BinaryReader reader = new BinaryReader(stream, Utf8, true))
				{
					if (reader.ReadInt32() != Magic) return false;
					string id = reader.ReadString(), realm = reader.ReadString(), settlement = reader.ReadString();
					string name = reader.ReadString(), binding = reader.ReadString();
					long first = reader.ReadInt64(), anchor = reader.ReadInt64(), through = reader.ReadInt64();
					int wanted = reader.ReadInt32(), departed = reader.ReadInt32();
					byte closing = reader.ReadByte();
					if (closing > 1) return false;
					KingdomSubsidenceBatch value = new KingdomSubsidenceBatch(id, realm, settlement, name, binding,
						first, anchor, through, wanted, departed, closing == 1, reader.ReadInt64(), reader.ReadString());
					if (stream.Position != stream.Length || !TryEncode(value, out string canonical)
						|| canonical != wire) return false;
					batch = value; return true;
				}
			}
			catch { return false; }
		}
	}
}
