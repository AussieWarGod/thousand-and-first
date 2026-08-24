using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	/// <summary>Whether this build can interpret the book as mutation authority.</summary>
	public enum KingdomTradeSchemaState : byte
	{
		Unknown = 0,
		Compatible = 1,
		Quarantined = 2
	}

	/// <summary>Persisted observation of the trade option. Values are save format.</summary>
	public enum KingdomTradeOptionState : byte
	{
		Unknown = 0,
		Disabled = 1,
		Enabled = 2
	}

	/// <summary>One durable trade operation. Values are append-only save format.</summary>
	public enum KingdomTradeOperationKind : byte
	{
		None = 0,
		CharterDelivery = 1,
		ManifestLoad = 2,
		ManifestDelivery = 3,
		ManifestTurnback = 4,
		ManifestLapse = 5
	}

	/// <summary>Prepared precedes every physical mutation; terminal rows never regain authority.</summary>
	public enum KingdomTradePhase : byte
	{
		Invalid = 0,
		Prepared = 1,
		ResourceIntent = 2,
		ResourceSettled = 3,
		ProjectionIntent = 4,
		ProjectionSettled = 5,
		DomainIntent = 6,
		DomainSettled = 7,
		Sinks = 8,
		ScheduleIntent = 9,
		Terminal = 10,
		Quarantined = 11,
		/// <summary>All lanes are exact terminal and retirement capacity was reserved.</summary>
		RetirementReady = 12
	}

	public enum KingdomTradeWaterDirection : byte
	{
		None = 0,
		Debit = 1,
		Credit = 2
	}

	public enum KingdomTradePhysicalState : byte
	{
		None = 0,
		Prepared = 1,
		Intent = 2,
		Proved = 3,
		Skipped = 4,
		Lost = 5,
		CreateIntent = 6,
		CleanupIntent = 7
	}

	public enum KingdomTradeSinkState : byte
	{
		None = 0,
		Pending = 1,
		Intent = 2,
		Delivered = 3,
		Skipped = 4,
		Lost = 5
	}

	public enum KingdomTradeManifestStatus : byte
	{
		None = 0,
		InFlight = 1,
		Delivered = 2,
		Quarantined = 3
	}

	[Serializable]
	public sealed class KingdomTradeWaterLeg
	{
		public string OwnerId;
		public string ZoneId;
		public int Capacity;
		public int Before;
		public int Delta;
		public int After;
		public string BeforeComposition;
		public string AfterComposition;
		public KingdomTradePhysicalState State;

	}

	[Serializable]
	public sealed class KingdomTradeMaterialOutput
	{
		public string OutputId;
		public string Marker;
		public string Blueprint;
		public int Count;
		public string DestinationOwnerId;
		public string ZoneId;
		public KingdomTradePhysicalState State;
		public KingdomTradePhysicalState CleanupState;

	}

	[Serializable]
	public sealed class KingdomTradeStandingCas
	{
		public string Faction;
		public int Before;
		public int Delta;
		public int After;
		public KingdomTradePhysicalState State;

	}

	[Serializable]
	public sealed class KingdomTradeOutbox
	{
		public string EventId;
		public string Chronicle;
		public KingdomTradeSinkState ChronicleState;
		public string LedgerNote;
		public int LedgerDeliveredDelta;
		public KingdomTradeSinkState LedgerState;
		public string Message;
		public KingdomTradeSinkState MessageState;
		public string Deed;
		public KingdomTradeSinkState DeedState;

	}

	[Serializable]
	public sealed class KingdomTradeCharter
	{
		public long Sequence;
		public string Id;
		public string DealKey;
		public string Faction;
		public long CreatedTick;
		public long NextTick;
		public bool Quarantined;
		public string Fault;

	}

	[Serializable]
	public sealed class KingdomTradeManifestState
	{
		public long OperationSequence;
		public string OperationId;
		public string Id;
		public string OriginId;
		public string OriginName;
		public string DestinationId;
		public string DestinationName;
		public int OriginalDrams;
		public int EscrowDrams;
		public long LoadedTick;
		public long DeadlineTick;
		public bool TurnedBack;
		public KingdomTradeManifestStatus Status;
		public string Fault;

	}

	/// <summary>One city's exact active caravan projection authority.</summary>
	[Serializable]
	public sealed class KingdomTradeProjectionRow
	{
		public long OperationSequence;
		public string OperationId;
		public string SettlementId;
		public string ZoneId;
		public string ProjectionId;
		public string ObjectId;
		public bool Quarantined;
		public string Fault;

	}

	[Serializable]
	public sealed class KingdomTradeOperation
	{
		public long Sequence;
		public string Id;
		public KingdomTradeOperationKind Kind;
		public KingdomTradePhase Phase;
		public long CreatedTick;
		public long UpdatedTick;
		public string ZoneId;
		public string SettlementId;
		public string SettlementName;
		public string CharterId;
		public string ManifestId;
		public string DealKey;
		public string DealDisplayName;
		public string Faction;
		public int Cycles;
		public int IncomePerCycle;
		public long IntervalTicks;
		public long DueBefore;
		public long DueAfter;
		public string CaravanBlueprint;
		public string ProjectionId;
		public string ProjectionObjectId;
		public int ProjectionX;
		public int ProjectionY;
		public string PriorProjectionId;
		public string PriorProjectionObjectId;
		public string PriorProjectionZoneId;
		public KingdomTradePhysicalState ProjectionState;
		public KingdomTradePhysicalState PriorCleanupState;
		public KingdomTradeWaterDirection WaterDirection;
		public int RequestedWater;
		public int ProvedWater;
		public int AmbiguousWater;
		public List<KingdomTradeWaterLeg> WaterLegs = new List<KingdomTradeWaterLeg>();
		public string MaterialClaim;
		public int MaterialRequested;
		public int MaterialProved;
		public List<KingdomTradeMaterialOutput> MaterialOutputs = new List<KingdomTradeMaterialOutput>();
		public string OriginId;
		public string OriginName;
		public string DestinationId;
		public string DestinationName;
		public long ManifestLoadedTick;
		public long ManifestDeadlineTick;
		public int ManifestEscrowBefore;
		public int ManifestEscrowDebit;
		public int ManifestEscrowAfter;
		public KingdomTradePhysicalState ManifestEscrowState;
		public long RetainedBefore;
		public long RetainedDelta;
		public long RetainedAfter;
		public KingdomTradePhysicalState RetainedState;
		public KingdomTradeStandingCas Standing;
		public KingdomTradeOutbox Outbox;
		public string Fault;

	}

	[Serializable]
	public sealed class KingdomTradeProof
	{
		public string RealmId;
		public long Sequence;
		public string Id;
		public string OperationEvidenceHash;
		public KingdomTradeOperationKind Kind;
		public KingdomTradePhase Disposition;
		public int ProvedWater;
		public int AmbiguousWater;
		public int RequestedWater;
		public string SettlementId;
		public string ManifestId;
		public int ManifestEscrowBefore;
		public int ManifestEscrowDebit;
		public int ManifestEscrowAfter;
		public KingdomTradePhysicalState ManifestEscrowState;
		public long RetainedBefore;
		public long RetainedDelta;
		public long RetainedAfter;
		public KingdomTradePhysicalState RetainedState;
		public int MaterialRequested;
		public int MaterialProved;
		public KingdomTradeSinkState ChronicleState;
		public KingdomTradeSinkState LedgerState;
		public KingdomTradeSinkState MessageState;
		public KingdomTradeSinkState DeedState;
		/// <summary>Receipt owns removal of its exact terminal manifest row.</summary>
		public bool ManifestCleanup;
		public long Tick;
		public string Fault;
	}

	[Serializable]
	public sealed class KingdomTradeArchive
	{
		public string RealmId;
		public List<string> SettlementIds = new List<string>();
		public long RetainedEscrowDrams;
		public int ManifestEscrowDrams;
		public string ManifestId;
		public KingdomTradeManifestStatus ManifestStatus;
		public int CharterCount;
		public int ProjectionCount;
		public int ProofCount;
		public string OpenOperationId;
		public string PendingRetirementId;
		public int OpenRequestedWater;
		public int OpenProvedWater;
		public int OpenAmbiguousWater;
		public long RetiredThrough;
		public string AuthorityEvidenceHash;
		public long ClosedTick;
		/// <summary>Domain-separated digest of every archive field above, including close tick.</summary>
		public string ReceiptEvidenceHash;
	}

	[Serializable]
	public sealed class KingdomTradeProofCompaction
	{
		public string RealmId;
		public long FirstSequence;
		public long LastSequence;
		public int ProofCount;
		public string EvidenceHash;
	}

	[Serializable]
	public sealed class KingdomTradeIncident
	{
		public string RealmId;
		public long Sequence;
		public string OperationId;
		public string EvidenceHash;
		public long Tick;
		public string Fault;
	}

	public sealed class KingdomTradeAuthoritySeal
	{
		internal byte[] BookBytes;
		internal IList<string> ClaimedZones;
		internal string[] ClaimedRows;
		internal IList<string> CityZones;
		internal string[] CityRows;
	}

	/// <summary>In-memory only witness for exact mutable object identity across a callback cut.</summary>
	public sealed class KingdomTradeReferenceSeal
	{
		internal object[] Rows;
	}

	/// <summary>
	/// Realm trade authority encoded only through the bounded versioned envelope below.
	/// Pre-release nested named-field graphs are intentionally incompatible because the engine
	/// allocates their reflected collections before Trade can validate raw counts.
	/// </summary>
	[Serializable]
	public sealed class KingdomTradeBook
#if !TAF_TESTS
		: IComposite
#endif
	{
		public int FormatVersion = KingdomTradeRules.CurrentFormatVersion;
		public KingdomTradeSchemaState SchemaState = KingdomTradeSchemaState.Compatible;
		public string SchemaFault;
		public bool LegacyMigrated;
		public int LegacyRejected;
		public string RealmId;
		public bool IdentityBound;
		public List<string> SettlementIds = new List<string>();
		public KingdomTradeOptionState OptionState;
		public long OptionObservedTick;
		public long OptionEpoch;
		public bool RestampPending;
		public long NextCharterSequence = 1L;
		public long NextOperationSequence = 1L;
		public long RetiredThrough;
		public List<KingdomTradeCharter> Charters = new List<KingdomTradeCharter>();
		public KingdomTradeManifestState Manifest;
		public KingdomTradeOperation OpenOperation;
		public KingdomTradeProof PendingRetirement;
		public List<KingdomTradeProof> RecentProofs = new List<KingdomTradeProof>();
		public List<KingdomTradeProofCompaction> CompactedProofs = new List<KingdomTradeProofCompaction>();
		public string ActiveProjectionId;
		public string ActiveProjectionObjectId;
		public List<KingdomTradeProjectionRow> Projections = new List<KingdomTradeProjectionRow>();
		public long RetainedEscrowDrams;
		public long UnattributedArchivedEscrowDrams;
		public List<KingdomTradeArchive> Archives = new List<KingdomTradeArchive>();
		public List<KingdomTradeIncident> Incidents = new List<KingdomTradeIncident>();
		public int OpaqueWireVersion;
		public byte[] OpaqueFuturePayload;

#if !TAF_TESTS
		public bool WantFieldReflection => false;
		public void Write(SerializationWriter Writer)
		{
			byte[] envelope = KingdomTradeCodec.EncodeEnvelope(this);
			Writer.Write(envelope.Length);
			Writer.Write(envelope, 0, envelope.Length);
		}
		public void Read(SerializationReader Reader)
		{
			int length = Reader.ReadInt32();
			if (length < 0 || length > KingdomTradeCodec.MaxEnvelopeBytes)
				throw new InvalidDataException("Trade envelope length exceeds hard bound; pre-release named-field v2 cannot be migrated safely.");
			byte[] envelope = Reader.ReadBytesDirect(length);
			if (envelope.Length != length) throw new EndOfStreamException("Truncated trade envelope.");
			CopyFrom(KingdomTradeCodec.DecodeEnvelopeRaw(envelope));
		}
#endif

		internal void CopyFrom(KingdomTradeBook Source)
		{
			if (Source == null) throw new ArgumentNullException(nameof(Source));
			FormatVersion = Source.FormatVersion; SchemaState = Source.SchemaState;
			SchemaFault = Source.SchemaFault; LegacyMigrated = Source.LegacyMigrated;
			LegacyRejected = Source.LegacyRejected; RealmId = Source.RealmId;
			IdentityBound = Source.IdentityBound; SettlementIds = Source.SettlementIds;
			OptionState = Source.OptionState; OptionObservedTick = Source.OptionObservedTick;
			OptionEpoch = Source.OptionEpoch; RestampPending = Source.RestampPending;
			NextCharterSequence = Source.NextCharterSequence;
			NextOperationSequence = Source.NextOperationSequence; RetiredThrough = Source.RetiredThrough;
			Charters = Source.Charters; Manifest = Source.Manifest; OpenOperation = Source.OpenOperation;
			PendingRetirement = Source.PendingRetirement; RecentProofs = Source.RecentProofs;
			CompactedProofs = Source.CompactedProofs;
			ActiveProjectionId = Source.ActiveProjectionId;
			ActiveProjectionObjectId = Source.ActiveProjectionObjectId; Projections = Source.Projections;
			RetainedEscrowDrams = Source.RetainedEscrowDrams;
			UnattributedArchivedEscrowDrams = Source.UnattributedArchivedEscrowDrams;
			Archives = Source.Archives; Incidents = Source.Incidents;
			OpaqueWireVersion = Source.OpaqueWireVersion;
			OpaqueFuturePayload = Source.OpaqueFuturePayload;
		}
	}

	/// <summary>
	/// Total bounded wire codec. Pre-release named-field Trade graphs are intentionally unsupported:
	/// engine ReadObject allocates their raw list/string lengths before Trade can validate them.
	/// </summary>
	public static class KingdomTradeCodec
	{
		public const int Magic = 0x54414654;
		public const int CurrentWireVersion = 3;
		public const int MaxEnvelopeBytes = 1024 * 1024;
		public const int MaxStringBytes = 65536;
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static byte[] EncodeEnvelope(KingdomTradeBook Book)
		{
			if (Book == null) throw new ArgumentNullException(nameof(Book));
			int wire = CurrentWireVersion;
			byte[] payload;
			if (Book.SchemaState == KingdomTradeSchemaState.Unknown
				&& Book.OpaqueFuturePayload != null)
			{
				wire = Book.OpaqueWireVersion;
				if (wire <= 0 || wire == CurrentWireVersion)
					throw new InvalidDataException("Opaque Trade wire version is not distinct and positive.");
				payload = (byte[])Book.OpaqueFuturePayload.Clone();
			}
			else payload = EncodePayload(Book);
			if (payload.Length > MaxEnvelopeBytes - 12)
				throw new InvalidDataException("Trade payload exceeds hard bound.");
			using (MemoryStream stream = new MemoryStream(12 + payload.Length))
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(Magic);
				writer.Write(wire);
				writer.Write(payload.Length);
				writer.Write(payload, 0, payload.Length);
				return stream.ToArray();
			}
		}

		/// <summary>Compatibility name for structural decode. Never performs semantic recovery.</summary>
		public static KingdomTradeBook DecodeEnvelope(byte[] Envelope)
		{
			return DecodeEnvelopeRaw(Envelope);
		}

		/// <summary>
		/// Total bounded structural decode. Core must inspect coexistence with legacy graphs before
		/// explicitly invoking KingdomTradeRules.Normalize; save loading cannot settle receipts.
		/// </summary>
		public static KingdomTradeBook DecodeEnvelopeRaw(byte[] Envelope)
		{
			if (Envelope == null || Envelope.Length < 12 || Envelope.Length > MaxEnvelopeBytes)
				throw new InvalidDataException("Trade envelope length is invalid.");
			using (MemoryStream stream = new MemoryStream(Envelope, false))
			using (BinaryReader reader = new BinaryReader(stream))
			{
				if (reader.ReadInt32() != Magic)
					throw new InvalidDataException("Unsupported pre-release named-field Trade encoding; unsafe migration refused.");
				int wire = reader.ReadInt32();
				int length = ReadCount(reader, MaxEnvelopeBytes - 12, "payload bytes");
				if (length != stream.Length - stream.Position)
					throw new InvalidDataException("Trade envelope payload length mismatch.");
				byte[] payload = reader.ReadBytes(length);
				if (payload.Length != length) throw new EndOfStreamException("Truncated Trade payload.");
				if (wire == 1)
					throw new InvalidDataException("Unsafe pre-release Trade wire v1 migration refused.");
				if (wire != CurrentWireVersion)
				{
					if (wire <= 0) throw new InvalidDataException("Invalid Trade wire version.");
					return new KingdomTradeBook
					{
						FormatVersion = KingdomTradeRules.CurrentFormatVersion,
						SchemaState = KingdomTradeSchemaState.Unknown,
						SchemaFault = "Unsupported bounded Trade wire preserved as opaque non-authoritative evidence.",
						OpaqueWireVersion = wire,
						OpaqueFuturePayload = payload,
						IdentityBound = false
					};
				}
				return DecodePayload(payload);
			}
		}

		/// <summary>Deterministic authority bytes used by hostile-callback witnesses.</summary>
		public static byte[] EncodePayload(KingdomTradeBook Book)
		{
			if (Book == null) throw new ArgumentNullException(nameof(Book));
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(Book.FormatVersion);
				writer.Write((byte)Book.SchemaState);
				WriteString(writer, Book.SchemaFault);
				writer.Write(Book.LegacyMigrated);
				writer.Write(Book.LegacyRejected);
				WriteString(writer, Book.RealmId);
				writer.Write(Book.IdentityBound);
				WriteStringList(writer, Book.SettlementIds, KingdomTradeRules.MaxSettlementIds);
				writer.Write((byte)Book.OptionState);
				writer.Write(Book.OptionObservedTick);
				writer.Write(Book.OptionEpoch);
				writer.Write(Book.RestampPending);
				writer.Write(Book.NextCharterSequence);
				writer.Write(Book.NextOperationSequence);
				writer.Write(Book.RetiredThrough);
				WriteList(writer, Book.Charters, KingdomTradeRules.MaxCharters, WriteCharter);
				WriteNullable(writer, Book.Manifest, WriteManifest);
				WriteNullable(writer, Book.OpenOperation, WriteOperation);
				WriteNullable(writer, Book.PendingRetirement, WriteProof);
				WriteList(writer, Book.RecentProofs, KingdomTradeRules.MaxRecentProofs, WriteProof);
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
					throw new InvalidDataException("Trade payload exceeds hard bound.");
				return stream.ToArray();
			}
		}

		private static KingdomTradeBook DecodePayload(byte[] Payload)
		{
			using (MemoryStream stream = new MemoryStream(Payload, false))
			using (BinaryReader reader = new BinaryReader(stream))
			{
				KingdomTradeBook book = new KingdomTradeBook
				{
					FormatVersion = reader.ReadInt32(),
					SchemaState = (KingdomTradeSchemaState)reader.ReadByte(),
					SchemaFault = ReadString(reader),
					LegacyMigrated = ReadExactBoolean(reader),
					LegacyRejected = reader.ReadInt32(),
					RealmId = ReadString(reader),
					IdentityBound = ReadExactBoolean(reader),
					SettlementIds = ReadStringList(reader, KingdomTradeRules.MaxSettlementIds),
					OptionState = (KingdomTradeOptionState)reader.ReadByte(),
					OptionObservedTick = reader.ReadInt64(),
					OptionEpoch = reader.ReadInt64(),
					RestampPending = ReadExactBoolean(reader),
					NextCharterSequence = reader.ReadInt64(),
					NextOperationSequence = reader.ReadInt64(),
					RetiredThrough = reader.ReadInt64(),
					Charters = ReadList(reader, KingdomTradeRules.MaxCharters, ReadCharter),
					Manifest = ReadNullable(reader, ReadManifest),
					OpenOperation = ReadNullable(reader, ReadOperation),
					PendingRetirement = ReadNullable(reader, ReadProof),
					RecentProofs = ReadList(reader, KingdomTradeRules.MaxRecentProofs, ReadProof),
					CompactedProofs = ReadList(reader, KingdomTradeRules.MaxCompactedProofs,
						ReadProofCompaction),
					ActiveProjectionId = ReadString(reader),
					ActiveProjectionObjectId = ReadString(reader),
					Projections = ReadList(reader, KingdomTradeRules.MaxProjectionRows, ReadProjection),
					RetainedEscrowDrams = reader.ReadInt64(),
					UnattributedArchivedEscrowDrams = reader.ReadInt64(),
					Archives = ReadList(reader, KingdomTradeRules.MaxArchives, ReadArchive),
					Incidents = ReadList(reader, KingdomTradeRules.MaxIncidents, ReadIncident)
				};
				if (stream.Position != stream.Length) throw new InvalidDataException("Trailing Trade payload bytes.");
				return book;
			}
		}

		private delegate void RowWriter<T>(BinaryWriter Writer, T Row);
		private delegate T RowReader<T>(BinaryReader Reader);

		private static void WriteNullable<T>(BinaryWriter Writer, T Row, RowWriter<T> WriteRow)
			where T : class
		{
			Writer.Write(Row != null);
			if (Row != null) WriteRow(Writer, Row);
		}

		private static T ReadNullable<T>(BinaryReader Reader, RowReader<T> ReadRow)
			where T : class
		{
			return ReadExactBoolean(Reader) ? ReadRow(Reader) : null;
		}

		private static bool ReadExactBoolean(BinaryReader Reader)
		{
			byte value = Reader.ReadByte();
			if (value > 1) throw new InvalidDataException("Trade boolean is not canonical 0/1.");
			return value == 1;
		}

		private static void WriteList<T>(BinaryWriter Writer, List<T> Rows, int Maximum,
			RowWriter<T> WriteRow) where T : class
		{
			if (Rows == null) throw new InvalidDataException("Missing Trade evidence list.");
			int count = Rows.Count;
			if (count < 0 || count > Maximum) throw new InvalidDataException("Trade list exceeds hard bound.");
			Writer.Write(count);
			for (int i = 0; i < count; i++) WriteNullable(Writer, Rows[i], WriteRow);
		}

		private static List<T> ReadList<T>(BinaryReader Reader, int Maximum, RowReader<T> ReadRow)
			where T : class
		{
			int count = ReadCount(Reader, Maximum, "list rows");
			List<T> rows = new List<T>(count);
			for (int i = 0; i < count; i++) rows.Add(ReadNullable(Reader, ReadRow));
			return rows;
		}

		private static void WriteStringList(BinaryWriter Writer, List<string> Rows, int Maximum)
		{
			if (Rows == null) throw new InvalidDataException("Missing Trade string evidence list.");
			int count = Rows.Count;
			if (count < 0 || count > Maximum) throw new InvalidDataException("Trade string list exceeds hard bound.");
			Writer.Write(count);
			for (int i = 0; i < count; i++) WriteString(Writer, Rows[i]);
		}

		private static List<string> ReadStringList(BinaryReader Reader, int Maximum)
		{
			int count = ReadCount(Reader, Maximum, "string list rows");
			List<string> rows = new List<string>(count);
			for (int i = 0; i < count; i++) rows.Add(ReadString(Reader));
			return rows;
		}

		private static void WriteString(BinaryWriter Writer, string Value)
		{
			if (Value == null) { Writer.Write(-1); return; }
			if (Value.Length > MaxStringBytes
				|| StrictUtf8.GetByteCount(Value) > MaxStringBytes)
				throw new InvalidDataException("Trade string exceeds hard byte bound.");
			byte[] bytes = StrictUtf8.GetBytes(Value);
			Writer.Write(bytes.Length);
			Writer.Write(bytes, 0, bytes.Length);
		}

		private static string ReadString(BinaryReader Reader)
		{
			int length = Reader.ReadInt32();
			if (length == -1) return null;
			if (length < 0 || length > MaxStringBytes) throw new InvalidDataException("Trade string exceeds hard byte bound.");
			byte[] bytes = Reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException("Truncated Trade string.");
			return StrictUtf8.GetString(bytes);
		}

		private static int ReadCount(BinaryReader Reader, int Maximum, string Name)
		{
			int count = Reader.ReadInt32();
			if (count < 0 || count > Maximum) throw new InvalidDataException("Trade " + Name + " exceeds hard bound.");
			return count;
		}

		private static void WriteWater(BinaryWriter w, KingdomTradeWaterLeg x)
		{
			WriteString(w, x.OwnerId); WriteString(w, x.ZoneId); w.Write(x.Capacity); w.Write(x.Before);
			w.Write(x.Delta); w.Write(x.After); WriteString(w, x.BeforeComposition);
			WriteString(w, x.AfterComposition); w.Write((byte)x.State);
		}

		private static KingdomTradeWaterLeg ReadWater(BinaryReader r)
		{
			return new KingdomTradeWaterLeg { OwnerId = ReadString(r), ZoneId = ReadString(r),
				Capacity = r.ReadInt32(), Before = r.ReadInt32(), Delta = r.ReadInt32(), After = r.ReadInt32(),
				BeforeComposition = ReadString(r), AfterComposition = ReadString(r),
				State = (KingdomTradePhysicalState)r.ReadByte() };
		}

		private static void WriteMaterial(BinaryWriter w, KingdomTradeMaterialOutput x)
		{
			WriteString(w, x.OutputId); WriteString(w, x.Marker); WriteString(w, x.Blueprint); w.Write(x.Count);
			WriteString(w, x.DestinationOwnerId); WriteString(w, x.ZoneId); w.Write((byte)x.State);
			w.Write((byte)x.CleanupState);
		}

		private static KingdomTradeMaterialOutput ReadMaterial(BinaryReader r)
		{
			return new KingdomTradeMaterialOutput { OutputId = ReadString(r), Marker = ReadString(r),
				Blueprint = ReadString(r), Count = r.ReadInt32(), DestinationOwnerId = ReadString(r),
				ZoneId = ReadString(r), State = (KingdomTradePhysicalState)r.ReadByte(),
				CleanupState = (KingdomTradePhysicalState)r.ReadByte() };
		}

		private static void WriteStanding(BinaryWriter w, KingdomTradeStandingCas x)
		{
			WriteString(w, x.Faction); w.Write(x.Before); w.Write(x.Delta); w.Write(x.After); w.Write((byte)x.State);
		}

		private static KingdomTradeStandingCas ReadStanding(BinaryReader r)
		{
			return new KingdomTradeStandingCas { Faction = ReadString(r), Before = r.ReadInt32(),
				Delta = r.ReadInt32(), After = r.ReadInt32(), State = (KingdomTradePhysicalState)r.ReadByte() };
		}

		private static void WriteOutbox(BinaryWriter w, KingdomTradeOutbox x)
		{
			WriteString(w, x.EventId); WriteString(w, x.Chronicle); w.Write((byte)x.ChronicleState);
			WriteString(w, x.LedgerNote); w.Write(x.LedgerDeliveredDelta); w.Write((byte)x.LedgerState);
			WriteString(w, x.Message); w.Write((byte)x.MessageState); WriteString(w, x.Deed); w.Write((byte)x.DeedState);
		}

		private static KingdomTradeOutbox ReadOutbox(BinaryReader r)
		{
			return new KingdomTradeOutbox { EventId = ReadString(r), Chronicle = ReadString(r),
				ChronicleState = (KingdomTradeSinkState)r.ReadByte(), LedgerNote = ReadString(r),
				LedgerDeliveredDelta = r.ReadInt32(), LedgerState = (KingdomTradeSinkState)r.ReadByte(),
				Message = ReadString(r), MessageState = (KingdomTradeSinkState)r.ReadByte(),
				Deed = ReadString(r), DeedState = (KingdomTradeSinkState)r.ReadByte() };
		}

		private static void WriteCharter(BinaryWriter w, KingdomTradeCharter x)
		{
			w.Write(x.Sequence); WriteString(w, x.Id); WriteString(w, x.DealKey); WriteString(w, x.Faction);
			w.Write(x.CreatedTick); w.Write(x.NextTick); w.Write(x.Quarantined); WriteString(w, x.Fault);
		}

		private static KingdomTradeCharter ReadCharter(BinaryReader r)
		{
			return new KingdomTradeCharter { Sequence = r.ReadInt64(), Id = ReadString(r), DealKey = ReadString(r),
				Faction = ReadString(r), CreatedTick = r.ReadInt64(), NextTick = r.ReadInt64(),
				Quarantined = ReadExactBoolean(r), Fault = ReadString(r) };
		}

		private static void WriteManifest(BinaryWriter w, KingdomTradeManifestState x)
		{
			w.Write(x.OperationSequence); WriteString(w, x.OperationId); WriteString(w, x.Id);
			WriteString(w, x.OriginId); WriteString(w, x.OriginName);
			WriteString(w, x.DestinationId); WriteString(w, x.DestinationName); w.Write(x.OriginalDrams);
			w.Write(x.EscrowDrams); w.Write(x.LoadedTick); w.Write(x.DeadlineTick); w.Write(x.TurnedBack);
			w.Write((byte)x.Status); WriteString(w, x.Fault);
		}

		private static KingdomTradeManifestState ReadManifest(BinaryReader r)
		{
			return new KingdomTradeManifestState { OperationSequence = r.ReadInt64(), OperationId = ReadString(r),
				Id = ReadString(r), OriginId = ReadString(r),
				OriginName = ReadString(r), DestinationId = ReadString(r), DestinationName = ReadString(r),
				OriginalDrams = r.ReadInt32(), EscrowDrams = r.ReadInt32(), LoadedTick = r.ReadInt64(),
				DeadlineTick = r.ReadInt64(), TurnedBack = ReadExactBoolean(r),
				Status = (KingdomTradeManifestStatus)r.ReadByte(), Fault = ReadString(r) };
		}

		private static void WriteProjection(BinaryWriter w, KingdomTradeProjectionRow x)
		{
			w.Write(x.OperationSequence); WriteString(w, x.OperationId);
			WriteString(w, x.SettlementId); WriteString(w, x.ZoneId);
			WriteString(w, x.ProjectionId); WriteString(w, x.ObjectId); w.Write(x.Quarantined); WriteString(w, x.Fault);
		}

		private static KingdomTradeProjectionRow ReadProjection(BinaryReader r)
		{
			return new KingdomTradeProjectionRow { OperationSequence = r.ReadInt64(),
				OperationId = ReadString(r), SettlementId = ReadString(r),
				ZoneId = ReadString(r), ProjectionId = ReadString(r), ObjectId = ReadString(r),
				Quarantined = ReadExactBoolean(r), Fault = ReadString(r) };
		}

		private static void WriteOperation(BinaryWriter w, KingdomTradeOperation x)
		{
			w.Write(x.Sequence); WriteString(w, x.Id); w.Write((byte)x.Kind); w.Write((byte)x.Phase);
			w.Write(x.CreatedTick); w.Write(x.UpdatedTick); WriteString(w, x.ZoneId); WriteString(w, x.SettlementId);
			WriteString(w, x.SettlementName); WriteString(w, x.CharterId); WriteString(w, x.ManifestId);
			WriteString(w, x.DealKey); WriteString(w, x.DealDisplayName); WriteString(w, x.Faction);
			w.Write(x.Cycles); w.Write(x.IncomePerCycle); w.Write(x.IntervalTicks); w.Write(x.DueBefore);
			w.Write(x.DueAfter); WriteString(w, x.CaravanBlueprint); WriteString(w, x.ProjectionId);
			WriteString(w, x.ProjectionObjectId); w.Write(x.ProjectionX); w.Write(x.ProjectionY);
			WriteString(w, x.PriorProjectionId); WriteString(w, x.PriorProjectionObjectId);
			WriteString(w, x.PriorProjectionZoneId); w.Write((byte)x.ProjectionState);
			w.Write((byte)x.PriorCleanupState); w.Write((byte)x.WaterDirection); w.Write(x.RequestedWater);
			w.Write(x.ProvedWater); w.Write(x.AmbiguousWater);
			WriteList(w, x.WaterLegs, KingdomTradeRules.MaxWaterLegs, WriteWater);
			WriteString(w, x.MaterialClaim); w.Write(x.MaterialRequested); w.Write(x.MaterialProved);
			WriteList(w, x.MaterialOutputs, KingdomTradeRules.MaxMaterialOutputs, WriteMaterial);
			WriteString(w, x.OriginId); WriteString(w, x.OriginName); WriteString(w, x.DestinationId);
			WriteString(w, x.DestinationName); w.Write(x.ManifestLoadedTick); w.Write(x.ManifestDeadlineTick);
			w.Write(x.ManifestEscrowBefore); w.Write(x.ManifestEscrowDebit); w.Write(x.ManifestEscrowAfter);
			w.Write((byte)x.ManifestEscrowState); w.Write(x.RetainedBefore); w.Write(x.RetainedDelta);
			w.Write(x.RetainedAfter); w.Write((byte)x.RetainedState);
			WriteNullable(w, x.Standing, WriteStanding); WriteNullable(w, x.Outbox, WriteOutbox); WriteString(w, x.Fault);
		}

		private static KingdomTradeOperation ReadOperation(BinaryReader r)
		{
			KingdomTradeOperation x = new KingdomTradeOperation();
			x.Sequence = r.ReadInt64(); x.Id = ReadString(r); x.Kind = (KingdomTradeOperationKind)r.ReadByte();
			x.Phase = (KingdomTradePhase)r.ReadByte(); x.CreatedTick = r.ReadInt64(); x.UpdatedTick = r.ReadInt64();
			x.ZoneId = ReadString(r); x.SettlementId = ReadString(r); x.SettlementName = ReadString(r);
			x.CharterId = ReadString(r); x.ManifestId = ReadString(r); x.DealKey = ReadString(r);
			x.DealDisplayName = ReadString(r); x.Faction = ReadString(r); x.Cycles = r.ReadInt32();
			x.IncomePerCycle = r.ReadInt32(); x.IntervalTicks = r.ReadInt64(); x.DueBefore = r.ReadInt64();
			x.DueAfter = r.ReadInt64(); x.CaravanBlueprint = ReadString(r); x.ProjectionId = ReadString(r);
			x.ProjectionObjectId = ReadString(r); x.ProjectionX = r.ReadInt32(); x.ProjectionY = r.ReadInt32();
			x.PriorProjectionId = ReadString(r); x.PriorProjectionObjectId = ReadString(r);
			x.PriorProjectionZoneId = ReadString(r); x.ProjectionState = (KingdomTradePhysicalState)r.ReadByte();
			x.PriorCleanupState = (KingdomTradePhysicalState)r.ReadByte();
			x.WaterDirection = (KingdomTradeWaterDirection)r.ReadByte(); x.RequestedWater = r.ReadInt32();
			x.ProvedWater = r.ReadInt32(); x.AmbiguousWater = r.ReadInt32();
			x.WaterLegs = ReadList(r, KingdomTradeRules.MaxWaterLegs, ReadWater); x.MaterialClaim = ReadString(r);
			x.MaterialRequested = r.ReadInt32(); x.MaterialProved = r.ReadInt32();
			x.MaterialOutputs = ReadList(r, KingdomTradeRules.MaxMaterialOutputs, ReadMaterial);
			x.OriginId = ReadString(r); x.OriginName = ReadString(r); x.DestinationId = ReadString(r);
			x.DestinationName = ReadString(r); x.ManifestLoadedTick = r.ReadInt64();
			x.ManifestDeadlineTick = r.ReadInt64(); x.ManifestEscrowBefore = r.ReadInt32();
			x.ManifestEscrowDebit = r.ReadInt32(); x.ManifestEscrowAfter = r.ReadInt32();
			x.ManifestEscrowState = (KingdomTradePhysicalState)r.ReadByte(); x.RetainedBefore = r.ReadInt64();
			x.RetainedDelta = r.ReadInt64(); x.RetainedAfter = r.ReadInt64();
			x.RetainedState = (KingdomTradePhysicalState)r.ReadByte();
			x.Standing = ReadNullable(r, ReadStanding); x.Outbox = ReadNullable(r, ReadOutbox); x.Fault = ReadString(r);
			return x;
		}

		private static void WriteProof(BinaryWriter w, KingdomTradeProof x)
		{
			WriteString(w, x.RealmId); w.Write(x.Sequence); WriteString(w, x.Id);
			WriteString(w, x.OperationEvidenceHash); w.Write((byte)x.Kind); w.Write((byte)x.Disposition);
			w.Write(x.ProvedWater); w.Write(x.AmbiguousWater); w.Write(x.RequestedWater);
			WriteString(w, x.SettlementId); WriteString(w, x.ManifestId); w.Write(x.ManifestEscrowBefore);
			w.Write(x.ManifestEscrowDebit); w.Write(x.ManifestEscrowAfter); w.Write((byte)x.ManifestEscrowState);
			w.Write(x.RetainedBefore); w.Write(x.RetainedDelta); w.Write(x.RetainedAfter); w.Write((byte)x.RetainedState);
			w.Write(x.MaterialRequested); w.Write(x.MaterialProved); w.Write((byte)x.ChronicleState);
			w.Write((byte)x.LedgerState); w.Write((byte)x.MessageState); w.Write((byte)x.DeedState);
			w.Write(x.ManifestCleanup); w.Write(x.Tick); WriteString(w, x.Fault);
		}

		private static KingdomTradeProof ReadProof(BinaryReader r)
		{
			return new KingdomTradeProof { RealmId = ReadString(r), Sequence = r.ReadInt64(), Id = ReadString(r),
				OperationEvidenceHash = ReadString(r),
				Kind = (KingdomTradeOperationKind)r.ReadByte(), Disposition = (KingdomTradePhase)r.ReadByte(),
				ProvedWater = r.ReadInt32(), AmbiguousWater = r.ReadInt32(), RequestedWater = r.ReadInt32(),
				SettlementId = ReadString(r), ManifestId = ReadString(r), ManifestEscrowBefore = r.ReadInt32(),
				ManifestEscrowDebit = r.ReadInt32(), ManifestEscrowAfter = r.ReadInt32(),
				ManifestEscrowState = (KingdomTradePhysicalState)r.ReadByte(), RetainedBefore = r.ReadInt64(),
				RetainedDelta = r.ReadInt64(), RetainedAfter = r.ReadInt64(),
				RetainedState = (KingdomTradePhysicalState)r.ReadByte(), MaterialRequested = r.ReadInt32(),
					MaterialProved = r.ReadInt32(), ChronicleState = (KingdomTradeSinkState)r.ReadByte(),
					LedgerState = (KingdomTradeSinkState)r.ReadByte(), MessageState = (KingdomTradeSinkState)r.ReadByte(),
					DeedState = (KingdomTradeSinkState)r.ReadByte(), ManifestCleanup = ReadExactBoolean(r),
					Tick = r.ReadInt64(), Fault = ReadString(r) };
		}

		private static void WriteArchive(BinaryWriter w, KingdomTradeArchive x)
		{
			WriteString(w, x.RealmId); WriteStringList(w, x.SettlementIds,
				KingdomTradeRules.MaxSettlementIds); w.Write(x.RetainedEscrowDrams);
			w.Write(x.ManifestEscrowDrams); WriteString(w, x.ManifestId);
			w.Write((byte)x.ManifestStatus); w.Write(x.CharterCount); w.Write(x.ProjectionCount);
			w.Write(x.ProofCount); WriteString(w, x.OpenOperationId);
			WriteString(w, x.PendingRetirementId); w.Write(x.OpenRequestedWater);
			w.Write(x.OpenProvedWater); w.Write(x.OpenAmbiguousWater); w.Write(x.RetiredThrough);
			WriteString(w, x.AuthorityEvidenceHash); w.Write(x.ClosedTick);
			WriteString(w, x.ReceiptEvidenceHash);
		}

		private static KingdomTradeArchive ReadArchive(BinaryReader r)
		{
			return new KingdomTradeArchive { RealmId = ReadString(r),
				SettlementIds = ReadStringList(r, KingdomTradeRules.MaxSettlementIds),
				RetainedEscrowDrams = r.ReadInt64(), ManifestEscrowDrams = r.ReadInt32(),
				ManifestId = ReadString(r), ManifestStatus = (KingdomTradeManifestStatus)r.ReadByte(),
				CharterCount = r.ReadInt32(), ProjectionCount = r.ReadInt32(),
				ProofCount = r.ReadInt32(), OpenOperationId = ReadString(r),
				PendingRetirementId = ReadString(r), OpenRequestedWater = r.ReadInt32(),
				OpenProvedWater = r.ReadInt32(), OpenAmbiguousWater = r.ReadInt32(),
				RetiredThrough = r.ReadInt64(), AuthorityEvidenceHash = ReadString(r),
				ClosedTick = r.ReadInt64(), ReceiptEvidenceHash = ReadString(r) };
		}

		private static void WriteProofCompaction(BinaryWriter w, KingdomTradeProofCompaction x)
		{
			WriteString(w, x.RealmId); w.Write(x.FirstSequence); w.Write(x.LastSequence);
			w.Write(x.ProofCount); WriteString(w, x.EvidenceHash);
		}

		private static KingdomTradeProofCompaction ReadProofCompaction(BinaryReader r)
		{
			return new KingdomTradeProofCompaction { RealmId = ReadString(r),
				FirstSequence = r.ReadInt64(), LastSequence = r.ReadInt64(),
				ProofCount = r.ReadInt32(), EvidenceHash = ReadString(r) };
		}

		private static void WriteIncident(BinaryWriter w, KingdomTradeIncident x)
		{
			WriteString(w, x.RealmId); w.Write(x.Sequence); WriteString(w, x.OperationId);
			WriteString(w, x.EvidenceHash); w.Write(x.Tick); WriteString(w, x.Fault);
		}

		private static KingdomTradeIncident ReadIncident(BinaryReader r)
		{
			return new KingdomTradeIncident { RealmId = ReadString(r), Sequence = r.ReadInt64(),
				OperationId = ReadString(r), EvidenceHash = ReadString(r), Tick = r.ReadInt64(), Fault = ReadString(r) };
		}
	}
}
