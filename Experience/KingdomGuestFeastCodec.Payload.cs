using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestFeastCodec
	{
		private static byte[] EncodePayload(KingdomGuestFeastBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = Writer(stream))
			{
				writer.Write(Magic); writer.Write(CurrentWireVersion);
				WriteString(writer, book.RealmId, KingdomGuestFeastRules.MaxRealmIdBytes);
				writer.Write(book.IdentityBound);
				writer.Write(book.Revision); writer.Write(book.Rows.Count);
				for (int i = 0; i < book.Rows.Count; i++) WriteRow(writer, book.Rows[i]);
				writer.Flush(); byte[] payload = stream.ToArray();
				if (payload.Length > MaxPayloadBytes)
					throw new InvalidDataException("guest-feast payload exceeds hard bound");
				return payload;
			}
		}

		private static KingdomGuestFeastBook DecodePayload(byte[] payload, int wireVersion)
		{
			using (MemoryStream stream = new MemoryStream(payload, false))
			using (BinaryReader reader = Reader(stream))
			{
				if (reader.ReadInt32() != Magic || reader.ReadInt32() != wireVersion)
					throw new InvalidDataException("nested guest-feast header is invalid");
				KingdomGuestFeastBook book = new KingdomGuestFeastBook
				{
					RealmId = ReadString(reader, KingdomGuestFeastRules.MaxRealmIdBytes),
					IdentityBound = ReadBool(reader),
					Revision = reader.ReadInt64()
				};
				int count = reader.ReadInt32();
				if (count < 0 || count > KingdomGuestFeastRules.MaxRows)
					throw new InvalidDataException("guest-feast row count exceeds cap");
				for (int i = 0; i < count; i++) book.Rows.Add(ReadRow(reader, wireVersion));
				if (stream.Position != stream.Length)
					throw new InvalidDataException("guest-feast payload has trailing bytes");
				if (!KingdomGuestFeastRules.TryValidate(book, out string failure))
					throw new InvalidDataException(failure);
				return book;
			}
		}

		private static void WriteRow(BinaryWriter writer, KingdomGuestFeastReceipt row)
		{
			writer.Write(row.Version); writer.Write((byte)row.Phase);
			WriteString(writer, row.SettlementId, KingdomGuestFeastRules.MaxSettlementIdBytes);
			WriteString(writer, row.OpportunityId, KingdomGuestFeastRules.MaxOpportunityIdBytes);
			WriteString(writer, row.CauseId, KingdomGuestFeastRules.MaxCauseIdBytes);
			WriteString(writer, row.GuestDecisionReceiptId,
				KingdomGuestFeastRules.MaxGuestDecisionIdBytes);
			WriteString(writer, row.DeedId, KingdomGuestFeastRules.MaxDeedIdBytes);
			WriteString(writer, row.PracticeId, KingdomGuestFeastRules.MaxPracticeIdBytes);
			WriteString(writer, row.PointerSourceId, KingdomGuestFeastRules.MaxPracticeIdBytes);
			WriteString(writer, row.PointerTargetId, KingdomGuestFeastRules.MaxStringBytes);
			writer.Write(row.CauseTick); writer.Write(row.GuestDecisionTick);
			writer.Write(row.PracticeDecisionTick); writer.Write(row.PointerTick);
			writer.Write(row.HomeCycles);
			WriteString(writer, row.LocusProjectionId, 86);
			WriteString(writer, row.LocusRealmId, KingdomGuestFeastRules.MaxRealmIdBytes);
			WriteString(writer, row.LocusSettlementId,
				KingdomGuestFeastRules.MaxSettlementIdBytes);
			writer.Write(row.LocusWorkId);
			WriteString(writer, row.LocusObjectId, KingdomGuestFeastRules.MaxStringBytes);
			WriteString(writer, row.LocusZoneId, KingdomGuestFeastRules.MaxStringBytes);
			WriteString(writer, row.LocusBlueprint, KingdomGuestFeastRules.MaxStringBytes);
			writer.Write(row.LocusObservedTick);
			writer.Write(row.AwayArmed); writer.Write((byte)row.PointerKind);
			WriteString(writer, row.GrowthTerminalReceiptId,
				KingdomGuestFeastRules.MaxTerminalReceiptIdBytes);
			WriteString(writer, row.GuestCandidateId, KingdomGuestFeastRules.MaxStringBytes);
			WriteString(writer, row.GuestObjectId, KingdomGuestFeastRules.MaxStringBytes);
			WriteString(writer, row.GuestArrivalOperationId,
				KingdomGuestFeastRules.MaxStringBytes);
			WriteString(writer, row.GuestArrivalOutboxEventId,
				KingdomGuestFeastRules.MaxStringBytes);
			WriteString(writer, row.GuestName, KingdomGuestFeastRules.MaxStringBytes);
			WriteString(writer, row.GuestOrigin, KingdomGuestFeastRules.MaxStringBytes);
			WriteString(writer, row.GuestCreed, KingdomGuestFeastRules.MaxStringBytes);
			writer.Write(row.GuestTerminalTick); writer.Write(row.GuestResidentId);
			writer.Write((byte)row.GuestResult); writer.Write((byte)row.PracticeOutcome);
		}

		private static KingdomGuestFeastReceipt ReadRow(BinaryReader reader, int wireVersion)
		{
			KingdomGuestFeastReceipt row = new KingdomGuestFeastReceipt
			{
				Version = reader.ReadInt32(), Phase = (KingdomGuestFeastPhase)reader.ReadByte(),
				SettlementId = ReadString(reader, KingdomGuestFeastRules.MaxSettlementIdBytes),
				OpportunityId = ReadString(reader, KingdomGuestFeastRules.MaxOpportunityIdBytes),
				CauseId = ReadString(reader, KingdomGuestFeastRules.MaxCauseIdBytes),
				GuestDecisionReceiptId = ReadString(reader,
					KingdomGuestFeastRules.MaxGuestDecisionIdBytes),
				DeedId = ReadString(reader, KingdomGuestFeastRules.MaxDeedIdBytes),
				PracticeId = ReadString(reader, KingdomGuestFeastRules.MaxPracticeIdBytes),
				PointerSourceId = ReadString(reader, KingdomGuestFeastRules.MaxPracticeIdBytes),
				PointerTargetId = ReadString(reader, KingdomGuestFeastRules.MaxStringBytes),
				CauseTick = reader.ReadInt64(), GuestDecisionTick = reader.ReadInt64(),
				PracticeDecisionTick = reader.ReadInt64(), PointerTick = reader.ReadInt64(),
				HomeCycles = reader.ReadInt32()
			};
			if (wireVersion >= 3)
			{
				row.LocusProjectionId = ReadString(reader, 86);
				row.LocusRealmId = ReadString(reader, KingdomGuestFeastRules.MaxRealmIdBytes);
				row.LocusSettlementId = ReadString(reader,
					KingdomGuestFeastRules.MaxSettlementIdBytes);
				row.LocusWorkId = reader.ReadInt32();
				row.LocusObjectId = ReadString(reader, KingdomGuestFeastRules.MaxStringBytes);
				row.LocusZoneId = ReadString(reader, KingdomGuestFeastRules.MaxStringBytes);
				row.LocusBlueprint = ReadString(reader, KingdomGuestFeastRules.MaxStringBytes);
				row.LocusObservedTick = reader.ReadInt64();
			}
			else ReadBool(reader);
			row.AwayArmed = ReadBool(reader);
			row.PointerKind = (KingdomGuestFeastPointerKind)reader.ReadByte();
			if (wireVersion >= 2)
			{
				row.GrowthTerminalReceiptId = ReadString(reader,
					KingdomGuestFeastRules.MaxTerminalReceiptIdBytes);
				row.GuestCandidateId = ReadString(reader, KingdomGuestFeastRules.MaxStringBytes);
				row.GuestObjectId = ReadString(reader, KingdomGuestFeastRules.MaxStringBytes);
				row.GuestArrivalOperationId = ReadString(reader,
					KingdomGuestFeastRules.MaxStringBytes);
				row.GuestArrivalOutboxEventId = ReadString(reader,
					KingdomGuestFeastRules.MaxStringBytes);
				row.GuestName = ReadString(reader, KingdomGuestFeastRules.MaxStringBytes);
				row.GuestOrigin = ReadString(reader, KingdomGuestFeastRules.MaxStringBytes);
				row.GuestCreed = ReadString(reader, KingdomGuestFeastRules.MaxStringBytes);
				row.GuestTerminalTick = reader.ReadInt64();
				row.GuestResidentId = reader.ReadInt32();
				row.GuestResult = (KingdomGrowthArrivalDisposition)reader.ReadByte();
				row.PracticeOutcome = (KingdomFirstFeastPhase)reader.ReadByte();
			}
			else UpgradeLegacyRow(row);
			row.Version = KingdomGuestFeastReceipt.CurrentVersion;
			if (wireVersion == 2 && row.GuestResult == KingdomGrowthArrivalDisposition.Joined)
			{
				// Old v2 practice/locus rows predate the joined-terminal + later-deed contract.
				// Preserve the exact guest terminal, but never promote their guessed causal trace.
				row.Phase = KingdomGuestFeastPhase.AwaitingPractice;
				row.DeedId = null; row.PracticeId = null; row.PracticeDecisionTick = -1L;
				row.PracticeOutcome = KingdomFirstFeastPhase.None;
				row.HomeCycles = 0; row.AwayArmed = false; row.PointerKind = KingdomGuestFeastPointerKind.None;
				row.PointerSourceId = null; row.PointerTargetId = null; row.PointerTick = -1L;
			}
			return row;
		}

		private static void UpgradeLegacyRow(KingdomGuestFeastReceipt row)
		{
			// Wire v1 carried no exact Guest terminal. Therefore none of its practice,
			// locus, cycle, or pointer guesses can satisfy the v3 causal contract.
			row.DeedId = null; row.PracticeId = null; row.PracticeDecisionTick = -1L;
			row.PracticeOutcome = KingdomFirstFeastPhase.None;
			row.PointerSourceId = null; row.PointerTargetId = null; row.PointerTick = -1L;
			row.PointerKind = KingdomGuestFeastPointerKind.None;
			row.HomeCycles = 0; row.AwayArmed = false;
			row.LocusProjectionId = null; row.LocusRealmId = null;
			row.LocusSettlementId = null; row.LocusWorkId = 0; row.LocusObjectId = null;
			row.LocusZoneId = null; row.LocusBlueprint = null; row.LocusObservedTick = -1L;
			row.Phase = row.GuestDecisionReceiptId == null
				? KingdomGuestFeastPhase.AwaitingGuestChoice
				: KingdomGuestFeastPhase.AwaitingGuestResult;
		}
	}
}
