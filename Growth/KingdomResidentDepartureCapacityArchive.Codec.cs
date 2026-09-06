using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomResidentDepartureCapacityArchive
	{
		private const int Magic = 0x31434454;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		private static bool TryRead(string wire, out List<Row> rows)
		{
			rows = null;
			if (wire == None) { rows = new List<Row>(); return true; }
			if (string.IsNullOrEmpty(wire) || wire.Length > MaximumWireChars
				|| !wire.StartsWith("dc1:", StringComparison.Ordinal)) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream(Convert.FromBase64String(wire.Substring(4)), false))
				using (BinaryReader reader = new BinaryReader(stream, Utf8, true))
				{
					if (reader.ReadInt32() != Magic) return false;
					int count = reader.ReadInt32();
					if (count < 1 || count > MaximumWarnings) return false;
					List<Row> values = new List<Row>();
					for (int i = 0; i < count; i++)
					{
						if (reader.ReadByte() != (byte)KingdomResidentDepartureChronicleDisposition.CapacityRefused) return false;
						values.Add(new Row
						{
							Realm = reader.ReadString(), Settlement = reader.ReadString(), OperationId = reader.ReadString(),
							Resident = reader.ReadInt32(), Body = reader.ReadString(), Zone = reader.ReadString(),
							Name = reader.ReadString(), Tick = reader.ReadInt64(), DeparturesBefore = reader.ReadInt32(),
							Text = reader.ReadString(), LedgerText = reader.ReadString(), Fingerprint = reader.ReadString(),
							Count = reader.ReadInt32(), Hash = reader.ReadString()
						});
					}
					if (stream.Position != stream.Length || !TryWrite(values, out string canonical)
						|| wire != canonical) return false;
					rows = values; return true;
				}
			}
			catch { return false; }
		}

		private static bool TryWrite(List<Row> rows, out string wire)
		{
			wire = null;
			if (rows == null || rows.Count > MaximumWarnings) return false;
			if (rows.Count == 0) { wire = None; return true; }
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Utf8, true))
				{
					writer.Write(Magic); writer.Write(rows.Count);
					foreach (Row row in rows)
					{
						if (!Valid(row) || !ids.Add(row.OperationId)) return false;
						writer.Write((byte)KingdomResidentDepartureChronicleDisposition.CapacityRefused);
						writer.Write(row.Realm); writer.Write(row.Settlement); writer.Write(row.OperationId);
						writer.Write(row.Resident); writer.Write(row.Body); writer.Write(row.Zone); writer.Write(row.Name);
						writer.Write(row.Tick); writer.Write(row.DeparturesBefore); writer.Write(row.Text); writer.Write(row.LedgerText);
						writer.Write(row.Fingerprint); writer.Write(row.Count); writer.Write(row.Hash);
						if (stream.Length > MaximumWireChars / 4 * 3 - 3) return false;
					}
					writer.Flush();
					string value = "dc1:" + Convert.ToBase64String(stream.ToArray());
					if (value.Length > MaximumWireChars) return false;
					wire = value; return true;
				}
			}
			catch { return false; }
		}
	}
}
