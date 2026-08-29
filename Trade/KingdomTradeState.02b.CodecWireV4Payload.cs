using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeCodec
	{
		/// <summary>Frozen wire-v4 payload used only to authenticate exact migration evidence.</summary>
		internal static byte[] EncodePayloadV4ForMigration(KingdomTradeBook Book)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(Book.FormatVersion); writer.Write((byte)Book.SchemaState);
				WriteString(writer, Book.SchemaFault); writer.Write(Book.LegacyMigrated);
				writer.Write(Book.LegacyRejected); WriteString(writer, Book.RealmId);
				writer.Write(Book.IdentityBound);
				WriteStringList(writer, Book.SettlementIds, KingdomTradeRules.MaxSettlementIds);
				writer.Write((byte)Book.OptionState); writer.Write(Book.OptionObservedTick);
				writer.Write(Book.OptionEpoch); writer.Write(Book.RestampPending);
				writer.Write(Book.NextCharterSequence); writer.Write(Book.NextOperationSequence);
				writer.Write(Book.RetiredThrough);
				WriteList(writer, Book.Charters, KingdomTradeRules.MaxCharters, WriteCharter);
				WriteNullable(writer, Book.Manifest, WriteManifest);
				WriteNullable(writer, Book.OpenOperation, WriteOperationV4);
				WriteNullable(writer, Book.PendingRetirement, WriteProofV4);
				WriteList(writer, Book.RecentProofs, KingdomTradeRules.MaxRecentProofs, WriteProofV4);
				WriteList(writer, Book.CompactedProofs, KingdomTradeRules.MaxCompactedProofs,
					WriteProofCompaction);
				WriteString(writer, Book.ActiveProjectionId);
				WriteString(writer, Book.ActiveProjectionObjectId);
				WriteList(writer, Book.Projections, KingdomTradeRules.MaxProjectionRows, WriteProjection);
				writer.Write(Book.RetainedEscrowDrams);
				writer.Write(Book.UnattributedArchivedEscrowDrams);
				WriteList(writer, Book.Archives, KingdomTradeRules.MaxArchives, WriteArchive);
				WriteList(writer, Book.Incidents, KingdomTradeRules.MaxIncidents, WriteIncident);
				writer.Flush();
				if (stream.Length > MaxEnvelopeBytes - 12)
					throw new InvalidDataException("Trade wire-v4 payload exceeds hard bound.");
				return stream.ToArray();
			}
		}
	}
}
