using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomHappeningLifecycleRules
	{
		internal static bool TryEncode(KingdomHappeningLifecycleBook book, out string wire)
		{
			return TryEncodeVersion(book, CurrentVersion, out wire);
		}

		internal static bool TryEncodeV1ForTests(KingdomHappeningLifecycleBook book,
			out string wire)
		{
			return TryEncodeVersion(book, PreviousVersion, out wire);
		}

		private static bool TryEncodeVersion(KingdomHappeningLifecycleBook book, int version,
			out string wire)
		{
			wire = null;
			if (!ValidBook(book) || (version != PreviousVersion && version != CurrentVersion)
				|| (version == PreviousVersion && book.SemanticReceipts.Length != 0)) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(Magic);
					writer.Write(version);
					writer.Write(book.Sequence);
					writer.Write(book.Active != null ? (byte)1 : (byte)0);
					if (book.Active != null) WriteOperation(writer, book.Active, version);
					if (version >= CurrentVersion)
					{
						writer.Write(book.SemanticReceipts.Length);
						for (int i = 0; i < book.SemanticReceipts.Length; i++)
						{
							writer.Write((byte)book.SemanticReceipts[i].Kind);
							writer.Write(book.SemanticReceipts[i].SubjectA);
							writer.Write(book.SemanticReceipts[i].SubjectB);
						}
					}
					writer.Flush();
					if (stream.Length > MaxPayloadBytes) return false;
					wire = Convert.ToBase64String(stream.ToArray());
					return wire.Length <= MaxWireChars;
				}
			}
			catch { wire = null; return false; }
		}

		internal static bool TryDecode(string wire, out KingdomHappeningLifecycleBook book,
			out KingdomHappeningLifecycleFault fault)
		{
			book = null;
			fault = KingdomHappeningLifecycleFault.Malformed;
			if (string.IsNullOrEmpty(wire))
			{
				book = KingdomHappeningLifecycleBook.Empty;
				fault = KingdomHappeningLifecycleFault.None;
				return true;
			}
			if (wire.Length > MaxWireChars) { fault = KingdomHappeningLifecycleFault.OverBudget; return false; }
			try
			{
				byte[] payload = Convert.FromBase64String(wire);
				if (payload.Length > MaxPayloadBytes)
				{
					fault = KingdomHappeningLifecycleFault.OverBudget;
					return false;
				}
				using (MemoryStream stream = new MemoryStream(payload, false))
				using (BinaryReader reader = new BinaryReader(stream, StrictUtf8, true))
				{
					if (reader.ReadInt32() != Magic) return false;
					int version = reader.ReadInt32();
					if (version != PreviousVersion && version != CurrentVersion)
					{
						fault = KingdomHappeningLifecycleFault.UnsupportedVersion;
						return false;
					}
					int sequence = reader.ReadInt32();
					byte present = reader.ReadByte();
					if (present > 1) return false;
					KingdomHappeningOperation active = present == 1
						? ReadOperation(reader, version) : null;
					KingdomHappeningSemanticReceipt[] receipts =
						new KingdomHappeningSemanticReceipt[0];
					if (version >= CurrentVersion)
					{
						int count = reader.ReadInt32();
						if (count < 0 || count > MaxSemanticReceipts)
							throw new InvalidDataException();
						receipts = new KingdomHappeningSemanticReceipt[count];
						for (int i = 0; i < count; i++)
							receipts[i] = new KingdomHappeningSemanticReceipt(
								ReadEnum<KingdomPhysicalHappeningKind>(reader),
								reader.ReadInt32(), reader.ReadInt32());
					}
					if (stream.Position != stream.Length) return false;
					KingdomHappeningLifecycleBook decoded = new KingdomHappeningLifecycleBook(sequence,
						active, receipts);
					if (!ValidBook(decoded)) return false;
					book = decoded;
					fault = KingdomHappeningLifecycleFault.None;
					return true;
				}
			}
			catch { return false; }
		}

	}
}
