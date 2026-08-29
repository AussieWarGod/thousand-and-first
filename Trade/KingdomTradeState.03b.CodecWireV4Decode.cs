using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeCodec
	{
		private static KingdomTradeBook DecodePayloadV4(byte[] Payload)
		{
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader reader = new BinaryReader(stream))
			{
				KingdomTradeBook book = new KingdomTradeBook
				{
					FormatVersion = reader.ReadInt32(),
					SchemaState = (KingdomTradeSchemaState)reader.ReadByte(),
					SchemaFault = ReadString(reader), LegacyMigrated = ReadExactBoolean(reader),
					LegacyRejected = reader.ReadInt32(), RealmId = ReadString(reader),
					IdentityBound = ReadExactBoolean(reader),
					SettlementIds = ReadStringList(reader, KingdomTradeRules.MaxSettlementIds),
					OptionState = (KingdomTradeOptionState)reader.ReadByte(),
					OptionObservedTick = reader.ReadInt64(), OptionEpoch = reader.ReadInt64(),
					RestampPending = ReadExactBoolean(reader),
					NextCharterSequence = reader.ReadInt64(),
					NextOperationSequence = reader.ReadInt64(), RetiredThrough = reader.ReadInt64(),
					Charters = ReadList(reader, KingdomTradeRules.MaxCharters, ReadCharter),
					Manifest = ReadNullable(reader, ReadManifest),
					OpenOperation = ReadNullable(reader, ReadOperationV4),
					PendingRetirement = ReadNullable(reader, ReadProofV4),
					RecentProofs = ReadList(reader, KingdomTradeRules.MaxRecentProofs, ReadProofV4),
					CompactedProofs = ReadList(reader, KingdomTradeRules.MaxCompactedProofs,
						ReadProofCompaction), ActiveProjectionId = ReadString(reader),
					ActiveProjectionObjectId = ReadString(reader),
					Projections = ReadList(reader, KingdomTradeRules.MaxProjectionRows, ReadProjection),
					RetainedEscrowDrams = reader.ReadInt64(),
					UnattributedArchivedEscrowDrams = reader.ReadInt64(),
					Archives = ReadList(reader, KingdomTradeRules.MaxArchives, ReadArchive),
					Incidents = ReadList(reader, KingdomTradeRules.MaxIncidents, ReadIncident)
				};
				if (stream.Position != stream.Length)
					throw new InvalidDataException("Trailing Trade wire-v4 payload bytes.");
				KingdomTradeRules.MigrateWireV4(book);
				return book;
			}
		}
	}
}
