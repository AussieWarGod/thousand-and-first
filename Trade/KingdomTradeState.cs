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
		ManifestLapse = 5,
		/// <summary>One witnessed polity request; direct debit lands only under terminal proof.</summary>
		PolityConsignmentDelivery = 6
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

	/// <summary>Exact reconstruction of a persisted consignment water intent.</summary>
	public enum KingdomTradeWaterIntentResolution : byte
	{
		Invalid = 0,
		Before = 1,
		After = 2,
		Ambiguous = 3
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

	/// <summary>Durable pattern-book lane inside one CharterDelivery receipt.</summary>
	public enum KingdomTradePatternState : byte
	{
		None = 0,
		NoCandidates = 1,
		ChanceMiss = 2,
		Offered = 3,
		ChoiceIntent = 4,
		Declined = 5,
		Selected = 6,
		RosterIntent = 7,
		Learned = 8,
		AlreadyKnown = 9,
		Conflict = 10
	}

	/// <summary>One exact catalogue row frozen before a caravan mutates anything.</summary>
	[Serializable]
	public sealed class KingdomTradePatternDesign
	{
		public string BuildingKey;
		public string LearnName;
		public string Label;
	}

	/// <summary>
	/// Optional pattern-book work owned by its CharterDelivery operation. The offer, choice,
	/// knowledge CAS, and both external sinks have no parallel GameState authority.
	/// </summary>
	[Serializable]
	public sealed class KingdomTradePatternReceipt
	{
		public KingdomTradePatternState State;
		public List<KingdomTradePatternDesign> Offers = new List<KingdomTradePatternDesign>();
		public int SelectedIndex = -1;
		public string RosterBefore;
		public string RosterAfter;
		public string Chronicle;
		public KingdomTradeSinkState ChronicleState = KingdomTradeSinkState.Skipped;
		public string Message;
		public KingdomTradeSinkState MessageState = KingdomTradeSinkState.Skipped;
		public string Fault;
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
	public static partial class KingdomTradeCodec
	{
	}
}
