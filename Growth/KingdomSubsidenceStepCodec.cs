using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal static class KingdomSubsidenceStepCodec
	{
		internal const string FreshWire = "ss1:new";
		internal const string LegacyWire = "ss1:legacy";
		internal const int MaxWireChars = 1048576;
		private const int LegacyMagic = 0x31535354;
		private const int OptionMagic = 0x32535354;
		private const int BatchMagic = 0x33535354;
		private const int FailureMagic = 0x34535354;
		private const int Magic = 0x35535354;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static bool TryEncode(KingdomSubsidenceStepBook book, out string wire)
		{
			return TryEncodeVersion(book, 5, out wire);
		}

		private static bool TryEncodeVersion(KingdomSubsidenceStepBook book, int version,
			out string wire)
		{
			wire = null;
			if (!KingdomSubsidenceStepRules.Valid(book)
				|| version < 5 && book.AnnouncementModel != KingdomSubsidenceAnnouncementCodec.None
				|| version < 4 && book.FailureModel != KingdomSubsidenceReportArchive.None
				|| version == 1 && book.OptionModel != KingdomSubsidenceStepRules.NoOption
				|| version < 3 && book.BatchModel != KingdomSubsidenceBatchRules.None
				|| version < 3 && book.Active != null && book.Active.RungReportModel !=
					(book.Active.ReachedStage == book.Active.FromStage ? KingdomSubsidenceBatchRules.NoReport
						: KingdomSubsidenceBatchRules.PendingReport)) return false;
			if (book.Admission != KingdomSubsidenceAdmission.Admitted)
			{
				wire = book.Admission == KingdomSubsidenceAdmission.Fresh ? FreshWire : LegacyWire;
				return true;
			}
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Utf8, true))
				{
					writer.Write(VersionMagic(version));
					writer.Write(book.RealmId); writer.Write(book.SettlementId);
					writer.Write(book.Sequence); writer.Write(book.LastRetiredTick);
					if (version >= 2) writer.Write(book.OptionModel);
					if (version >= 3) writer.Write(book.BatchModel);
					if (version >= 4) writer.Write(book.FailureModel);
					if (version >= 5) writer.Write(book.AnnouncementModel);
					writer.Write((byte)(book.Active == null ? 0 : 1));
					if (book.Active != null) Write(writer, book.Active, version);
					writer.Flush();
					if (stream.Length > MaxWireChars / 4 * 3 - 3) return false;
					wire = (version == 1 ? "ss1:" : version == 2 ? "ss2:" : version == 3 ? "ss3:" : version == 4 ? "ss4:" : "ss5:")
						+ Convert.ToBase64String(stream.ToArray());
					return wire.Length <= MaxWireChars;
				}
			}
			catch { wire = null; return false; }
		}

		internal static bool TryDecode(string wire, out KingdomSubsidenceStepBook book)
		{
			book = null;
			if (wire == FreshWire || wire == LegacyWire)
			{
				book = new KingdomSubsidenceStepBook(wire == FreshWire
					? KingdomSubsidenceAdmission.Fresh : KingdomSubsidenceAdmission.Legacy,
					"", "", 0, null);
				return true;
			}
			if (string.IsNullOrEmpty(wire) || wire.Length > MaxWireChars) return false;
			int version = wire.StartsWith("ss1:", StringComparison.Ordinal) ? 1
				: wire.StartsWith("ss2:", StringComparison.Ordinal) ? 2
				: wire.StartsWith("ss3:", StringComparison.Ordinal) ? 3
				: wire.StartsWith("ss4:", StringComparison.Ordinal) ? 4
				: wire.StartsWith("ss5:", StringComparison.Ordinal) ? 5 : 0;
			if (version == 0) return false;
			try
			{
				byte[] bytes = Convert.FromBase64String(wire.Substring(4));
				using (MemoryStream stream = new MemoryStream(bytes, false))
				using (BinaryReader reader = new BinaryReader(stream, Utf8, true))
				{
					if (reader.ReadInt32() != VersionMagic(version)) return false;
					string realm = reader.ReadString(), settlement = reader.ReadString();
					long sequence = reader.ReadInt64();
					long lastRetiredTick = reader.ReadInt64();
					string option = version == 1 ? KingdomSubsidenceStepRules.NoOption : reader.ReadString();
					string batch = version < 3 ? KingdomSubsidenceBatchRules.None : reader.ReadString();
					string failures = version < 4 ? KingdomSubsidenceReportArchive.None : reader.ReadString();
					string announcement = version < 5 ? KingdomSubsidenceAnnouncementCodec.None : reader.ReadString();
					byte active = reader.ReadByte();
					if (active > 1) return false;
					KingdomSubsidenceStepOperation operation = active == 0 ? null : Read(reader, version);
					if (stream.Position != stream.Length) return false;
					KingdomSubsidenceStepBook value = new KingdomSubsidenceStepBook(
						KingdomSubsidenceAdmission.Admitted, realm, settlement, sequence, operation,
						lastRetiredTick, option, batch, failures, announcement);
					if (!TryEncodeVersion(value, version, out string canonical) || canonical != wire) return false;
					book = value; return true;
				}
			}
			catch { return false; }
		}

		private static int VersionMagic(int version)
		{
			return version == 1 ? LegacyMagic : version == 2 ? OptionMagic : version == 3 ? BatchMagic : version == 4 ? FailureMagic : Magic;
		}

		private static void Write(BinaryWriter writer, KingdomSubsidenceStepOperation op, int version)
		{
			writer.Write(op.Id); writer.Write(op.AnchorTick); writer.Write(op.DueTick);
			writer.Write((byte)op.FromStage); writer.Write((byte)op.ReachedStage);
			writer.Write(op.Quota); writer.Write(op.Completed); writer.Write((byte)op.Phase);
			writer.Write(op.PendingDepartureId); writer.Write((byte)(op.PendingCredited ? 1 : 0));
			writer.Write((byte)(op.CancelRequested ? 1 : 0)); writer.Write(op.CancelTick);
			writer.Write(op.CancelToken); writer.Write(op.RungModel); writer.Write(op.Fault);
			writer.Write(op.CreditedDepartureIds);
			writer.Write(op.StorageCapacity); writer.Write(op.BindingSupport);
			writer.Write(op.LastActivityTick);
			if (op.PendingIdentity != null)
			{
				writer.Write(op.PendingIdentity.ResidentId); writer.Write(op.PendingIdentity.BodyObjectId);
				writer.Write(op.PendingIdentity.ZoneId); writer.Write(op.PendingIdentity.PreparedTick);
			}
			if (version >= 3) writer.Write(op.RungReportModel);
		}

		private static KingdomSubsidenceStepOperation Read(BinaryReader reader, int version)
		{
			string id = reader.ReadString(); long anchor = reader.ReadInt64(), due = reader.ReadInt64();
			GrowthStage from = (GrowthStage)reader.ReadByte(), reached = (GrowthStage)reader.ReadByte();
			int quota = reader.ReadInt32(), completed = reader.ReadInt32();
			KingdomSubsidenceStepPhase phase = (KingdomSubsidenceStepPhase)reader.ReadByte();
			string pendingId = reader.ReadString();
			bool credited = ReadFlag(reader), cancelled = ReadFlag(reader);
			long cancelTick = reader.ReadInt64(), cancelToken = reader.ReadInt64();
			string rung = reader.ReadString(), fault = reader.ReadString(), credits = reader.ReadString();
			int storage = reader.ReadInt32(); string binding = reader.ReadString();
			long lastActivity = reader.ReadInt64();
			KingdomSubsidenceDepartureIdentity pending = pendingId == "" ? null
				: new KingdomSubsidenceDepartureIdentity(reader.ReadInt32(), reader.ReadString(),
					reader.ReadString(), reader.ReadInt64());
			string report = version < 3 ? null : reader.ReadString();
			return new KingdomSubsidenceStepOperation(id, anchor, due, from, reached, quota, completed,
				phase, pendingId, credited, cancelled, cancelTick, cancelToken, rung, fault,
				storage, binding, lastActivity, credits, pending, report);
		}

		private static bool ReadFlag(BinaryReader reader)
		{
			byte value = reader.ReadByte();
			if (value > 1) throw new InvalidDataException();
			return value == 1;
		}
	}
}
