using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	// Numeric values are an append-only wire contract.
	public enum KingdomLifecycleLane : byte
	{
		None = 0,
		PlainGuest = 1,
		NotableGuest = 2,
		Raid = 3,
		Petition = 4
	}

	public enum KingdomLifecycleAction : byte
	{
		None = 0,
		Passages = 1,
		Spawn = 2,
		Depart = 3,
		OfferWater = 4,
		Lodge = 5,
		RaidWarning = 6,
		RaidRewarning = 7,
		RaidTribute = 8,
		RaidTalkDown = 9,
		RaidAttack = 10,
		RaidCancel = 11,
		PetitionOffer = 12,
		PetitionAccept = 13,
		PetitionDecline = 14,
		PetitionResolve = 15,
		PetitionExpire = 16
	}

	public enum KingdomLifecyclePhase : byte
	{
		Invalid = 0,
		Prepared = 1,
		ProjectionIntent = 2,
		Projected = 3,
		WaterIntent = 4,
		WaterSettled = 5,
		RemovalIntent = 6,
		Removed = 7,
		DomainIntent = 8,
		DomainSettled = 9,
		EffectIntent = 10,
		EffectsSettled = 11,
		Sinks = 12,
		ScheduleIntent = 13,
		Terminal = 14,
		Quarantined = 15
	}

	public enum KingdomLifecyclePhysicalState : byte
	{
		None = 0,
		Prepared = 1,
		Intent = 2,
		Proved = 3,
		Skipped = 4,
		Lost = 5
	}

	public enum KingdomLifecycleSinkState : byte
	{
		None = 0,
		Pending = 1,
		Intent = 2,
		Delivered = 3,
		Skipped = 4,
		Lost = 5
	}

	public enum KingdomLifecycleSinkDisposition : byte
	{
		Unknown = 0,
		Deliver = 1,
		Skip = 2
	}

	public enum KingdomLifecycleOptionState : byte
	{
		Unknown = 0,
		Disabled = 1,
		Enabled = 2
	}

	public enum KingdomLifecycleTopology : byte
	{
		None = 0,
		Cell = 1,
		Inventory = 2
	}

	public enum KingdomLifecycleResourceKind : byte
	{
		None = 0,
		Population = 1,
		Roster = 2,
		OriginRoster = 3,
		CreedRoster = 4,
		Standing = 5,
		Schedule = 6,
		WaterVessel = 7,
		Object = 8,
		Projection = 9,
		Petition = 10,
		Raid = 11
	}

	public enum KingdomLifecycleLeaseState : byte
	{
		None = 0,
		Prepared = 1,
		Intent = 2,
		Proved = 3,
		Skipped = 4
	}

	[Serializable]
	public sealed class KingdomLifecycleWaterLeg
	{
		public string OperationId;
		public string LeaseKey;
		public string OwnerId;
		public string Blueprint;
		public string ZoneId;
		public int Capacity;
		public int Before;
		public int Delta;
		public int After;
		public string Composition;
		public string ReceiptId;
		public int ReceiptBeforeMatches = -1;
		public int ReceiptAfterMatches = -1;
		public bool ReceiptSameReference;
		public string ReceiptProofId;
		public KingdomLifecyclePhysicalState ReceiptState;
		public KingdomLifecyclePhysicalState State;

		[NonSerialized]
		internal object LiveAuthority;
	}

	[Serializable]
	public sealed class KingdomLifecycleProjection
	{
		public string OperationId;
		public string EventId;
		public string ObjectId;
		public string Marker;
		public string Blueprint;
		public string ZoneId;
		public KingdomLifecycleTopology Topology;
		public string OwnerId;
		public int X = -1;
		public int Y = -1;
		public int Material = -1;
		public int Count;
		public bool NoStack;
		public KingdomLifecyclePhysicalState State;
		// Carry output callback receipt. The id/topology are frozen by the plan; the
		// observations are written around the callback and never inferred from Count.
		public string ReceiptId;
		public string ReceiptTopologyId;
		public int ReceiptBeforeIdMatches = -1;
		public int ReceiptBeforeMarkerMatches = -1;
		public int ReceiptBeforeCount = -1;
		public int ReceiptAfterIdMatches = -1;
		public int ReceiptAfterMarkerMatches = -1;
		public int ReceiptAfterCount = -1;
		public bool ReceiptSameReference;
		public string ReceiptProofId;
		public KingdomLifecyclePhysicalState ReceiptState;

		[NonSerialized]
		internal object LiveAuthority;
	}

	[Serializable]
	public sealed class KingdomLifecycleOutbox
	{
		public string OperationId;
		public string EventId;
		public string ChronicleReceiptId;
		public string Chronicle;
		public bool ChronicleAccomplishment;
		public KingdomLifecycleSinkDisposition ChronicleDisposition;
		public KingdomLifecycleSinkState ChronicleState;
		public string Ledger;
		public KingdomLifecycleSinkDisposition LedgerDisposition;
		public KingdomLifecycleSinkState LedgerState;
		public string Message;
		public KingdomLifecycleSinkDisposition MessageDisposition;
		public KingdomLifecycleSinkState MessageState;
		public string Deed;
		public KingdomLifecycleSinkDisposition DeedDisposition;
		public KingdomLifecycleSinkState DeedState;
		public string GuestbookLine;
		public KingdomLifecycleSinkDisposition GuestbookDisposition;
		public KingdomLifecycleSinkState GuestbookState;
	}

	[Serializable]
	public sealed class KingdomLifecycleResourceLease
	{
		public string OperationId;
		public KingdomLifecycleResourceKind Kind;
		public string ScopeId;
		public string SubjectId;
		public string Key;
		public long Before;
		public long Delta;
		public long After;
		public long BeforeRevision;
		public long AfterRevision;
		public KingdomLifecycleLeaseState State;
	}

	/// <summary>
	/// Shared typed CAS witness. LastOperationId is the proof which disambiguates equal scalar
	/// values produced by different lanes. ActiveOperationId is a persisted exclusive lease.
	/// Rows are never evicted; hitting the bounded cap refuses new work.
	/// </summary>
	[Serializable]
	public sealed class KingdomLifecycleResourceRevision
	{
		public KingdomLifecycleResourceKind Kind;
		public string ScopeId;
		public string SubjectId;
		public string Key;
		public long Revision;
		public string ActiveOperationId;
		public string LastOperationId;
	}

	[Serializable]
	public sealed class KingdomLifecycleOperation
	{
		public long Sequence;
		public string Id;
		public string PlanHash;
		public KingdomLifecycleLane Lane;
		public KingdomLifecycleAction Action;
		public KingdomLifecyclePhase Phase;
		public long CreatedTick;
		public long UpdatedTick;
		public string SettlementId;
		public string ZoneId;
		public string ObjectId;
		public string ObjectMarker;
		public string Blueprint;
		public KingdomLifecycleTopology ObjectTopology;
		public string ObjectOwnerId;
		public int ObjectX = -1;
		public int ObjectY = -1;
		public string ObjectName;
		public string Origin;
		public string Faction;
		public string DisplayFaction;
		public string Detail;
		public string Creed;
		public int Kind;
		public int Target;
		public int Count;
		public int DepartedCount;
		public long DueBefore;
		public long DueAfter;
		public long DepartTick;
		public int WaterRequested;
		public int WaterProved;
		public int WaterOutstanding;
		public int WaterLost;
		public int WaterAmbiguous;
		public KingdomLifecyclePhysicalState WaterState;
		public List<KingdomLifecycleWaterLeg> WaterLegs = new List<KingdomLifecycleWaterLeg>();
		public KingdomLifecyclePhysicalState RemovalState;
		public List<KingdomLifecycleProjection> Projections = new List<KingdomLifecycleProjection>();
		public KingdomLifecyclePhysicalState EffectState;
		public List<KingdomLifecycleResourceLease> ResourceLeases =
			new List<KingdomLifecycleResourceLease>();
		public int Defence;
		public int PartySize;
		public int Spawned;
		public int PlunderRequested;
		public int PlunderProved;
		public string ArrivalText;
		public KingdomLifecycleOutbox Outbox;
		public string Fault;

		[NonSerialized]
		public object LiveAuthority;
	}

	[Serializable]
	public sealed class KingdomLifecycleProof
	{
		public long Sequence;
		public string Id;
		public string PlanHash;
		public KingdomLifecycleLane Lane;
		public KingdomLifecycleAction Action;
		public long Tick;
	}

	/// <summary>Per-settlement authority. Every lane has its own monotone replay barrier.</summary>
	[Serializable]
	public sealed class KingdomLifecycleBook
#if !TAF_TESTS
		: IComposite
#endif
	{
		public int FormatVersion = KingdomLifecycleRules.CurrentFormatVersion;
		public bool LegacyIdentity;
		public string LegacyMigrationKey;
		public bool Quarantined;
		public string Fault;
		public string SettlementId;
		public bool IdentityBound;
		public string IdentityProof;
		public long PlainGuestNextSequence = 1L;
		public long PlainGuestRetiredThrough;
		public long NotableGuestNextSequence = 1L;
		public long NotableGuestRetiredThrough;
		public long RaidNextSequence = 1L;
		public long RaidRetiredThrough;
		public long PetitionNextSequence = 1L;
		public long PetitionRetiredThrough;
		public KingdomLifecycleOptionState LocusOption;
		public long LocusOptionTick;
		public KingdomLifecycleOptionState NotableOption;
		public long NotableOptionTick;
		public KingdomLifecycleOptionState RaidOption;
		public long RaidOptionTick;
		public KingdomLifecycleOptionState PetitionOption;
		public long PetitionOptionTick;
		public KingdomLifecycleOperation PlainGuest;
		public KingdomLifecycleOperation NotableGuest;
		public KingdomLifecycleOperation Raid;
		public KingdomLifecycleOperation Petition;
		public List<KingdomLifecycleResourceRevision> Resources =
			new List<KingdomLifecycleResourceRevision>();
		public List<KingdomLifecycleProof> RecentProofs = new List<KingdomLifecycleProof>();

		[NonSerialized]
		public bool WireRejected;

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			KingdomLifecycleWireCodec.WriteLifecycle(Writer, this);
		}

		public void Read(SerializationReader Reader)
		{
			try
			{
				KingdomLifecycleWireCodec.ReadLifecycle(Reader, this);
			}
			catch (Exception)
			{
				WireRejected = true;
				Quarantined = true;
				PlainGuest = null;
				NotableGuest = null;
				Raid = null;
				Petition = null;
				Resources = new List<KingdomLifecycleResourceRevision>();
				RecentProofs = new List<KingdomLifecycleProof>();
				throw;
			}
		}
#endif
	}

	[Serializable]
	public sealed class KingdomCarrySource
	{
		public string OperationId;
		public string SourceEventId;
		public string ObjectId;
		public string Blueprint;
		public KingdomLifecycleTopology Topology;
		public string OwnerId;
		public string ZoneId;
		public int X = -1;
		public int Y = -1;
		public int Material;
		public int OriginalCount;
		public int PlannedCount;
		public int Removed;
		public int UnitCursor;
		public int UnitBefore;
		public int UnitAfter;
		public string UnitEventId;
		public KingdomLifecyclePhysicalState UnitState;
		public string ReceiptId;
		public string ReceiptTopologyId;
		public int ReceiptBeforeIdMatches = -1;
		public int ReceiptAfterIdMatches = -1;
		public int ReceiptBeforeCount = -1;
		public int ReceiptAfterCount = -1;
		public bool ReceiptSameReference;
		public string ReceiptProofId;
		public string ReceiptChainId;
		public int ReceiptChainCount;
		public KingdomLifecyclePhysicalState ReceiptState;
		public KingdomLifecyclePhysicalState State;

		[NonSerialized]
		internal object LiveAuthority;
	}

	[Serializable]
	public sealed class KingdomCarryOperation
	{
		public long Sequence;
		public string Id;
		public string PlanHash;
		public KingdomLifecyclePhase Phase;
		public long CreatedTick;
		public long UpdatedTick;
		public string OriginSettlementId;
		public string OriginZoneId;
		public int OriginX;
		public int OriginY;
		public string DestinationSettlementId;
		public string DestinationSettlementName;
		public List<string> SettlementIds = new List<string>();
		public string RealmTopologyHash;
		public string DestinationZoneId;
		public KingdomLifecycleTopology DestinationTopology;
		public string DestinationOwnerId;
		public int DestinationX = -1;
		public int DestinationY = -1;
		public long DueTick;
		public bool RiskFrozen;
		public bool LostOnRoad;
		public int SourceIndex;
		public int OutputIndex;
		public KingdomLifecycleResourceLease ScheduleLease;
		public string ScheduleReceiptId;
		public string ScheduleTopologyId;
		public int ScheduleBeforeMatches = -1;
		public int ScheduleAfterMatches = -1;
		public bool ScheduleSameReference;
		public string ScheduleProofId;
		public KingdomLifecyclePhysicalState ScheduleReceiptState;
		public List<KingdomCarrySource> Sources = new List<KingdomCarrySource>();
		public List<KingdomLifecycleProjection> Outputs = new List<KingdomLifecycleProjection>();
		public int Mud;
		public int Brush;
		public int Timber;
		public int Stone;
		public int Marble;
		public int Scrap;
		public int EscrowMud;
		public int EscrowBrush;
		public int EscrowTimber;
		public int EscrowStone;
		public int EscrowMarble;
		public int EscrowScrap;
		public int DeliveredMud;
		public int DeliveredBrush;
		public int DeliveredTimber;
		public int DeliveredStone;
		public int DeliveredMarble;
		public int DeliveredScrap;
		public int LostMud;
		public int LostBrush;
		public int LostTimber;
		public int LostStone;
		public int LostMarble;
		public int LostScrap;
		public KingdomLifecycleOutbox Outbox;
		public string Fault;

		[NonSerialized]
		public object LiveAuthority;
	}

	[Serializable]
	public sealed class KingdomCarryBook
#if !TAF_TESTS
		: IComposite
#endif
	{
		public int FormatVersion = KingdomLifecycleRules.CurrentFormatVersion;
		public bool LegacyIdentity;
		public string LegacyMigrationKey;
		public bool Quarantined;
		public string Fault;
		public string RealmId;
		public List<string> SettlementIds = new List<string>();
		public bool IdentityBound;
		public string IdentityProof;
		public long NextSequence = 1L;
		public long RetiredThrough;
		public KingdomCarryOperation Open;
		public List<KingdomLifecycleResourceRevision> Resources =
			new List<KingdomLifecycleResourceRevision>();
		public List<KingdomLifecycleProof> RecentProofs = new List<KingdomLifecycleProof>();

		[NonSerialized]
		public bool WireRejected;

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			KingdomLifecycleWireCodec.WriteCarry(Writer, this);
		}

		public void Read(SerializationReader Reader)
		{
			try
			{
				KingdomLifecycleWireCodec.ReadCarry(Reader, this);
			}
			catch (Exception)
			{
				WireRejected = true;
				Quarantined = true;
				Open = null;
				SettlementIds = new List<string>();
				Resources = new List<KingdomLifecycleResourceRevision>();
				RecentProofs = new List<KingdomLifecycleProof>();
				throw;
			}
		}
#endif
	}

	/// <summary>
	/// Version-first, manually bounded codec. It validates list counts and UTF-8 byte lengths
	/// before allocating. Only the two owning books are engine composites; nested rows cannot be
	/// independently reflection-deserialized into oversized lists.
	/// </summary>
	public static class KingdomLifecycleWireCodec
	{
		public const int LifecycleMagic = 0x544C4332; // TLC2
		public const int CarryMagic = 0x54434332; // TCC2
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static void WriteLifecycle(BinaryWriter Writer, KingdomLifecycleBook Book)
		{
			if (Writer == null || Book == null || Book.WireRejected
				|| Book.FormatVersion != KingdomLifecycleRules.CurrentFormatVersion)
				throw new InvalidDataException("lifecycle authority is not writable");
			EnsureCount(Book.Resources, KingdomLifecycleRules.MaxResourceRows, "resource rows");
			EnsureCount(Book.RecentProofs, KingdomLifecycleRules.MaxRecentProofs, "proof rows");
			Writer.Write(LifecycleMagic);
			Writer.Write(KingdomLifecycleRules.CurrentFormatVersion);
			Writer.Write(Book.LegacyIdentity);
			WriteString(Writer, Book.LegacyMigrationKey, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.Quarantined);
			WriteString(Writer, Book.Fault, KingdomLifecycleRules.MaxTextBytes);
			WriteString(Writer, Book.SettlementId, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.IdentityBound);
			WriteString(Writer, Book.IdentityProof, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.PlainGuestNextSequence);
			Writer.Write(Book.PlainGuestRetiredThrough);
			Writer.Write(Book.NotableGuestNextSequence);
			Writer.Write(Book.NotableGuestRetiredThrough);
			Writer.Write(Book.RaidNextSequence);
			Writer.Write(Book.RaidRetiredThrough);
			Writer.Write(Book.PetitionNextSequence);
			Writer.Write(Book.PetitionRetiredThrough);
			WriteOption(Writer, Book.LocusOption, Book.LocusOptionTick);
			WriteOption(Writer, Book.NotableOption, Book.NotableOptionTick);
			WriteOption(Writer, Book.RaidOption, Book.RaidOptionTick);
			WriteOption(Writer, Book.PetitionOption, Book.PetitionOptionTick);
			WriteOperation(Writer, Book.PlainGuest);
			WriteOperation(Writer, Book.NotableGuest);
			WriteOperation(Writer, Book.Raid);
			WriteOperation(Writer, Book.Petition);
			Writer.Write(Book.Resources.Count);
			for (int i = 0; i < Book.Resources.Count; i++) WriteResource(Writer, Book.Resources[i]);
			Writer.Write(Book.RecentProofs.Count);
			for (int i = 0; i < Book.RecentProofs.Count; i++) WriteProof(Writer, Book.RecentProofs[i]);
		}

		public static void ReadLifecycle(BinaryReader Reader, KingdomLifecycleBook Target)
		{
			if (Reader == null || Target == null) throw new ArgumentNullException();
			try
			{
				if (Reader.ReadInt32() != LifecycleMagic) Reject(Target, "invalid lifecycle framing");
				int version = Reader.ReadInt32();
				Target.FormatVersion = version;
				if (version != KingdomLifecycleRules.CurrentFormatVersion)
					Reject(Target, "unsupported lifecycle version");
				KingdomLifecycleBook value = new KingdomLifecycleBook();
				value.FormatVersion = version;
				value.LegacyIdentity = ReadExactBoolean(Reader);
				value.LegacyMigrationKey = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				value.Quarantined = ReadExactBoolean(Reader);
				value.Fault = ReadString(Reader, KingdomLifecycleRules.MaxTextBytes);
				value.SettlementId = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				value.IdentityBound = ReadExactBoolean(Reader);
				value.IdentityProof = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				value.PlainGuestNextSequence = Reader.ReadInt64();
				value.PlainGuestRetiredThrough = Reader.ReadInt64();
				value.NotableGuestNextSequence = Reader.ReadInt64();
				value.NotableGuestRetiredThrough = Reader.ReadInt64();
				value.RaidNextSequence = Reader.ReadInt64();
				value.RaidRetiredThrough = Reader.ReadInt64();
				value.PetitionNextSequence = Reader.ReadInt64();
				value.PetitionRetiredThrough = Reader.ReadInt64();
				ReadOption(Reader, out value.LocusOption, out value.LocusOptionTick);
				ReadOption(Reader, out value.NotableOption, out value.NotableOptionTick);
				ReadOption(Reader, out value.RaidOption, out value.RaidOptionTick);
				ReadOption(Reader, out value.PetitionOption, out value.PetitionOptionTick);
				value.PlainGuest = ReadOperation(Reader);
				value.NotableGuest = ReadOperation(Reader);
				value.Raid = ReadOperation(Reader);
				value.Petition = ReadOperation(Reader);
				int resources = ReadCount(Reader, KingdomLifecycleRules.MaxResourceRows);
				value.Resources = new List<KingdomLifecycleResourceRevision>(resources);
				for (int i = 0; i < resources; i++) value.Resources.Add(ReadResource(Reader));
				int proofs = ReadCount(Reader, KingdomLifecycleRules.MaxRecentProofs);
				value.RecentProofs = new List<KingdomLifecycleProof>(proofs);
				for (int i = 0; i < proofs; i++) value.RecentProofs.Add(ReadProof(Reader));
				KingdomLifecycleRules.Normalize(value);
				Copy(value, Target);
			}
			catch (Exception)
			{
				Poison(Target, "malformed lifecycle wire was rejected");
				throw;
			}
		}

		public static void WriteCarry(BinaryWriter Writer, KingdomCarryBook Book)
		{
			if (Writer == null || Book == null || Book.WireRejected
				|| Book.FormatVersion != KingdomLifecycleRules.CurrentFormatVersion)
				throw new InvalidDataException("carry authority is not writable");
			EnsureCount(Book.SettlementIds, KingdomLifecycleRules.MaxSettlementIds,
				"settlement ids");
			EnsureCount(Book.Resources, KingdomLifecycleRules.MaxResourceRows, "resource rows");
			EnsureCount(Book.RecentProofs, KingdomLifecycleRules.MaxRecentProofs, "proof rows");
			Writer.Write(CarryMagic);
			Writer.Write(KingdomLifecycleRules.CurrentFormatVersion);
			Writer.Write(Book.LegacyIdentity);
			WriteString(Writer, Book.LegacyMigrationKey, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.Quarantined);
			WriteString(Writer, Book.Fault, KingdomLifecycleRules.MaxTextBytes);
			WriteString(Writer, Book.RealmId, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.SettlementIds.Count);
			for (int i = 0; i < Book.SettlementIds.Count; i++)
				WriteString(Writer, Book.SettlementIds[i], KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.IdentityBound);
			WriteString(Writer, Book.IdentityProof, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.NextSequence);
			Writer.Write(Book.RetiredThrough);
			WriteCarryOperation(Writer, Book.Open);
			Writer.Write(Book.Resources.Count);
			for (int i = 0; i < Book.Resources.Count; i++) WriteResource(Writer, Book.Resources[i]);
			Writer.Write(Book.RecentProofs.Count);
			for (int i = 0; i < Book.RecentProofs.Count; i++) WriteProof(Writer, Book.RecentProofs[i]);
		}

		public static void ReadCarry(BinaryReader Reader, KingdomCarryBook Target)
		{
			if (Reader == null || Target == null) throw new ArgumentNullException();
			try
			{
				if (Reader.ReadInt32() != CarryMagic) Reject(Target, "invalid carry framing");
				int version = Reader.ReadInt32();
				Target.FormatVersion = version;
				if (version != KingdomLifecycleRules.CurrentFormatVersion)
					Reject(Target, "unsupported carry version");
				KingdomCarryBook value = new KingdomCarryBook();
				value.FormatVersion = version;
				value.LegacyIdentity = ReadExactBoolean(Reader);
				value.LegacyMigrationKey = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				value.Quarantined = ReadExactBoolean(Reader);
				value.Fault = ReadString(Reader, KingdomLifecycleRules.MaxTextBytes);
				value.RealmId = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				int settlements = ReadCount(Reader, KingdomLifecycleRules.MaxSettlementIds);
				value.SettlementIds = new List<string>(settlements);
				for (int i = 0; i < settlements; i++)
					value.SettlementIds.Add(ReadString(Reader, KingdomLifecycleRules.MaxIdBytes));
				value.IdentityBound = ReadExactBoolean(Reader);
				value.IdentityProof = ReadString(Reader, KingdomLifecycleRules.MaxIdBytes);
				value.NextSequence = Reader.ReadInt64();
				value.RetiredThrough = Reader.ReadInt64();
				value.Open = ReadCarryOperation(Reader);
				int resources = ReadCount(Reader, KingdomLifecycleRules.MaxResourceRows);
				value.Resources = new List<KingdomLifecycleResourceRevision>(resources);
				for (int i = 0; i < resources; i++) value.Resources.Add(ReadResource(Reader));
				int proofs = ReadCount(Reader, KingdomLifecycleRules.MaxRecentProofs);
				value.RecentProofs = new List<KingdomLifecycleProof>(proofs);
				for (int i = 0; i < proofs; i++) value.RecentProofs.Add(ReadProof(Reader));
				KingdomLifecycleRules.Normalize(value);
				Copy(value, Target);
			}
			catch (Exception)
			{
				Poison(Target, "malformed carry wire was rejected");
				throw;
			}
		}

		public static int ReadCount(BinaryReader Reader, int Maximum)
		{
			int count = Reader.ReadInt32();
			if (count < 0 || count > Maximum)
				throw new InvalidDataException("bounded row count exceeded");
			return count;
		}

		private static bool ReadExactBoolean(BinaryReader Reader)
		{
			byte value = Reader.ReadByte();
			if (value > 1) throw new InvalidDataException("noncanonical boolean byte");
			return value == 1;
		}

		public static string ReadString(BinaryReader Reader, int MaximumBytes)
		{
			int length = Reader.ReadInt32();
			if (length == -1) return null;
			if (length < 0 || length > MaximumBytes)
				throw new InvalidDataException("bounded string length exceeded");
			byte[] bytes = Reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			return StrictUtf8.GetString(bytes);
		}

		public static void WriteString(BinaryWriter Writer, string Value, int MaximumBytes)
		{
			if (Value == null)
			{
				Writer.Write(-1);
				return;
			}
			int byteCount = StrictUtf8.GetByteCount(Value);
			if (byteCount > MaximumBytes)
				throw new InvalidDataException("bounded string length exceeded");
			byte[] bytes = StrictUtf8.GetBytes(Value);
			Writer.Write(byteCount);
			Writer.Write(bytes);
		}

		private static void WriteOperation(BinaryWriter w, KingdomLifecycleOperation o)
		{
			w.Write(o != null);
			if (o == null) return;
			EnsureCount(o.WaterLegs, KingdomLifecycleRules.MaxWaterLegs, "water legs");
			EnsureCount(o.Projections, KingdomLifecycleRules.MaxProjections, "projections");
			EnsureCount(o.ResourceLeases, KingdomLifecycleRules.MaxResourceLeases, "resource leases");
			w.Write(o.Sequence);
			S(w, o.Id, true); S(w, o.PlanHash, true);
			w.Write((byte)o.Lane); w.Write((byte)o.Action); w.Write((byte)o.Phase);
			w.Write(o.CreatedTick); w.Write(o.UpdatedTick);
			S(w, o.SettlementId, true); S(w, o.ZoneId, false); S(w, o.ObjectId, true);
			S(w, o.ObjectMarker, true); S(w, o.Blueprint, false);
			w.Write((byte)o.ObjectTopology); S(w, o.ObjectOwnerId, true);
			w.Write(o.ObjectX); w.Write(o.ObjectY); S(w, o.ObjectName, false);
			S(w, o.Origin, false); S(w, o.Faction, false); S(w, o.DisplayFaction, false);
			S(w, o.Detail, false, true); S(w, o.Creed, false);
			w.Write(o.Kind); w.Write(o.Target); w.Write(o.Count); w.Write(o.DepartedCount);
			w.Write(o.DueBefore); w.Write(o.DueAfter); w.Write(o.DepartTick);
			w.Write(o.WaterRequested); w.Write(o.WaterProved); w.Write(o.WaterOutstanding);
			w.Write(o.WaterLost); w.Write(o.WaterAmbiguous); w.Write((byte)o.WaterState);
			w.Write(o.WaterLegs.Count);
			for (int i = 0; i < o.WaterLegs.Count; i++) WriteWater(w, o.WaterLegs[i]);
			w.Write((byte)o.RemovalState);
			w.Write(o.Projections.Count);
			for (int i = 0; i < o.Projections.Count; i++) WriteProjection(w, o.Projections[i]);
			w.Write((byte)o.EffectState);
			w.Write(o.ResourceLeases.Count);
			for (int i = 0; i < o.ResourceLeases.Count; i++) WriteLease(w, o.ResourceLeases[i]);
			w.Write(o.Defence); w.Write(o.PartySize); w.Write(o.Spawned);
			w.Write(o.PlunderRequested); w.Write(o.PlunderProved);
			S(w, o.ArrivalText, false, true); WriteOutbox(w, o.Outbox); S(w, o.Fault, false, true);
		}

		private static KingdomLifecycleOperation ReadOperation(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			KingdomLifecycleOperation o = new KingdomLifecycleOperation();
			o.Sequence = r.ReadInt64();
			o.Id = S(r, true); o.PlanHash = S(r, true);
			o.Lane = (KingdomLifecycleLane)r.ReadByte();
			o.Action = (KingdomLifecycleAction)r.ReadByte();
			o.Phase = (KingdomLifecyclePhase)r.ReadByte();
			o.CreatedTick = r.ReadInt64(); o.UpdatedTick = r.ReadInt64();
			o.SettlementId = S(r, true); o.ZoneId = S(r, false); o.ObjectId = S(r, true);
			o.ObjectMarker = S(r, true); o.Blueprint = S(r, false);
			o.ObjectTopology = (KingdomLifecycleTopology)r.ReadByte();
			o.ObjectOwnerId = S(r, true); o.ObjectX = r.ReadInt32(); o.ObjectY = r.ReadInt32();
			o.ObjectName = S(r, false);
			o.Origin = S(r, false); o.Faction = S(r, false); o.DisplayFaction = S(r, false);
			o.Detail = S(r, false, true); o.Creed = S(r, false);
			o.Kind = r.ReadInt32(); o.Target = r.ReadInt32(); o.Count = r.ReadInt32();
			o.DepartedCount = r.ReadInt32(); o.DueBefore = r.ReadInt64();
			o.DueAfter = r.ReadInt64(); o.DepartTick = r.ReadInt64();
			o.WaterRequested = r.ReadInt32(); o.WaterProved = r.ReadInt32();
			o.WaterOutstanding = r.ReadInt32(); o.WaterLost = r.ReadInt32();
			o.WaterAmbiguous = r.ReadInt32(); o.WaterState = (KingdomLifecyclePhysicalState)r.ReadByte();
			int water = ReadCount(r, KingdomLifecycleRules.MaxWaterLegs);
			o.WaterLegs = new List<KingdomLifecycleWaterLeg>(water);
			for (int i = 0; i < water; i++) o.WaterLegs.Add(ReadWater(r));
			o.RemovalState = (KingdomLifecyclePhysicalState)r.ReadByte();
			int projections = ReadCount(r, KingdomLifecycleRules.MaxProjections);
			o.Projections = new List<KingdomLifecycleProjection>(projections);
			for (int i = 0; i < projections; i++) o.Projections.Add(ReadProjection(r));
			o.EffectState = (KingdomLifecyclePhysicalState)r.ReadByte();
			int leases = ReadCount(r, KingdomLifecycleRules.MaxResourceLeases);
			o.ResourceLeases = new List<KingdomLifecycleResourceLease>(leases);
			for (int i = 0; i < leases; i++) o.ResourceLeases.Add(ReadLease(r));
			o.Defence = r.ReadInt32(); o.PartySize = r.ReadInt32(); o.Spawned = r.ReadInt32();
			o.PlunderRequested = r.ReadInt32(); o.PlunderProved = r.ReadInt32();
			o.ArrivalText = S(r, false, true); o.Outbox = ReadOutbox(r); o.Fault = S(r, false, true);
			return o;
		}

		private static void WriteWater(BinaryWriter w, KingdomLifecycleWaterLeg x)
		{
			if (x == null) throw new InvalidDataException("null water leg");
			S(w, x.OperationId, true); S(w, x.LeaseKey, true); S(w, x.OwnerId, true);
			S(w, x.Blueprint, false); S(w, x.ZoneId, false); w.Write(x.Capacity);
			w.Write(x.Before); w.Write(x.Delta);
			w.Write(x.After); S(w, x.Composition, false, true); S(w, x.ReceiptId, false);
			w.Write(x.ReceiptBeforeMatches); w.Write(x.ReceiptAfterMatches);
			w.Write(x.ReceiptSameReference); S(w, x.ReceiptProofId, false);
			w.Write((byte)x.ReceiptState); w.Write((byte)x.State);
		}

		private static KingdomLifecycleWaterLeg ReadWater(BinaryReader r)
		{
			return new KingdomLifecycleWaterLeg
			{
				OperationId = S(r, true), LeaseKey = S(r, true), OwnerId = S(r, true),
				Blueprint = S(r, false), ZoneId = S(r, false), Capacity = r.ReadInt32(),
				Before = r.ReadInt32(),
				Delta = r.ReadInt32(), After = r.ReadInt32(), Composition = S(r, false, true),
				ReceiptId = S(r, false), ReceiptBeforeMatches = r.ReadInt32(),
				ReceiptAfterMatches = r.ReadInt32(), ReceiptSameReference = ReadExactBoolean(r),
				ReceiptProofId = S(r, false),
				ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte(),
				State = (KingdomLifecyclePhysicalState)r.ReadByte()
			};
		}

		private static void WriteProjection(BinaryWriter w, KingdomLifecycleProjection x)
		{
			if (x == null) throw new InvalidDataException("null projection");
			S(w, x.OperationId, true); S(w, x.EventId, true); S(w, x.ObjectId, true);
			S(w, x.Marker, true); S(w, x.Blueprint, false); S(w, x.ZoneId, false);
			w.Write((byte)x.Topology); S(w, x.OwnerId, true); w.Write(x.X); w.Write(x.Y);
			w.Write(x.Material); w.Write(x.Count); w.Write(x.NoStack); w.Write((byte)x.State);
			S(w, x.ReceiptId, false); S(w, x.ReceiptTopologyId, false);
			w.Write(x.ReceiptBeforeIdMatches); w.Write(x.ReceiptBeforeMarkerMatches);
			w.Write(x.ReceiptBeforeCount); w.Write(x.ReceiptAfterIdMatches);
			w.Write(x.ReceiptAfterMarkerMatches); w.Write(x.ReceiptAfterCount);
			w.Write(x.ReceiptSameReference); S(w, x.ReceiptProofId, false);
			w.Write((byte)x.ReceiptState);
		}

		private static KingdomLifecycleProjection ReadProjection(BinaryReader r)
		{
			return new KingdomLifecycleProjection
			{
				OperationId = S(r, true), EventId = S(r, true), ObjectId = S(r, true),
				Marker = S(r, true), Blueprint = S(r, false), ZoneId = S(r, false),
				Topology = (KingdomLifecycleTopology)r.ReadByte(), OwnerId = S(r, true),
				X = r.ReadInt32(), Y = r.ReadInt32(), Material = r.ReadInt32(),
					Count = r.ReadInt32(), NoStack = ReadExactBoolean(r),
					State = (KingdomLifecyclePhysicalState)r.ReadByte(),
					ReceiptId = S(r, false), ReceiptTopologyId = S(r, false),
					ReceiptBeforeIdMatches = r.ReadInt32(),
					ReceiptBeforeMarkerMatches = r.ReadInt32(),
					ReceiptBeforeCount = r.ReadInt32(),
					ReceiptAfterIdMatches = r.ReadInt32(),
					ReceiptAfterMarkerMatches = r.ReadInt32(),
					ReceiptAfterCount = r.ReadInt32(),
					ReceiptSameReference = ReadExactBoolean(r),
					ReceiptProofId = S(r, false),
					ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte()
			};
		}

		private static void WriteLease(BinaryWriter w, KingdomLifecycleResourceLease x)
		{
			if (x == null) throw new InvalidDataException("null lease");
			S(w, x.OperationId, true); w.Write((byte)x.Kind); S(w, x.ScopeId, true);
			S(w, x.SubjectId, true); S(w, x.Key, true); w.Write(x.Before); w.Write(x.Delta);
			w.Write(x.After); w.Write(x.BeforeRevision); w.Write(x.AfterRevision);
			w.Write((byte)x.State);
		}

		private static KingdomLifecycleResourceLease ReadLease(BinaryReader r)
		{
			return new KingdomLifecycleResourceLease
			{
				OperationId = S(r, true), Kind = (KingdomLifecycleResourceKind)r.ReadByte(),
				ScopeId = S(r, true), SubjectId = S(r, true), Key = S(r, true),
				Before = r.ReadInt64(), Delta = r.ReadInt64(), After = r.ReadInt64(),
				BeforeRevision = r.ReadInt64(), AfterRevision = r.ReadInt64(),
				State = (KingdomLifecycleLeaseState)r.ReadByte()
			};
		}

		private static void WriteResource(BinaryWriter w, KingdomLifecycleResourceRevision x)
		{
			if (x == null) throw new InvalidDataException("null resource row");
			w.Write((byte)x.Kind); S(w, x.ScopeId, true); S(w, x.SubjectId, true);
			S(w, x.Key, true); w.Write(x.Revision); S(w, x.ActiveOperationId, true);
			S(w, x.LastOperationId, true);
		}

		private static KingdomLifecycleResourceRevision ReadResource(BinaryReader r)
		{
			return new KingdomLifecycleResourceRevision
			{
				Kind = (KingdomLifecycleResourceKind)r.ReadByte(), ScopeId = S(r, true),
				SubjectId = S(r, true), Key = S(r, true), Revision = r.ReadInt64(),
				ActiveOperationId = S(r, true), LastOperationId = S(r, true)
			};
		}

		private static void WriteOutbox(BinaryWriter w, KingdomLifecycleOutbox x)
		{
			w.Write(x != null);
			if (x == null) return;
			S(w, x.OperationId, true); S(w, x.EventId, true); S(w, x.ChronicleReceiptId, true);
			S(w, x.Chronicle, false, true); w.Write(x.ChronicleAccomplishment);
			w.Write((byte)x.ChronicleDisposition); w.Write((byte)x.ChronicleState);
			S(w, x.Ledger, false, true); w.Write((byte)x.LedgerDisposition);
			w.Write((byte)x.LedgerState); S(w, x.Message, false, true);
			w.Write((byte)x.MessageDisposition); w.Write((byte)x.MessageState);
			S(w, x.Deed, false, true); w.Write((byte)x.DeedDisposition);
			w.Write((byte)x.DeedState); S(w, x.GuestbookLine, false, true);
			w.Write((byte)x.GuestbookDisposition); w.Write((byte)x.GuestbookState);
		}

		private static KingdomLifecycleOutbox ReadOutbox(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			return new KingdomLifecycleOutbox
			{
				OperationId = S(r, true), EventId = S(r, true), ChronicleReceiptId = S(r, true),
				Chronicle = S(r, false, true), ChronicleAccomplishment = ReadExactBoolean(r),
				ChronicleDisposition = (KingdomLifecycleSinkDisposition)r.ReadByte(),
				ChronicleState = (KingdomLifecycleSinkState)r.ReadByte(),
				Ledger = S(r, false, true),
				LedgerDisposition = (KingdomLifecycleSinkDisposition)r.ReadByte(),
				LedgerState = (KingdomLifecycleSinkState)r.ReadByte(), Message = S(r, false, true),
				MessageDisposition = (KingdomLifecycleSinkDisposition)r.ReadByte(),
				MessageState = (KingdomLifecycleSinkState)r.ReadByte(), Deed = S(r, false, true),
				DeedDisposition = (KingdomLifecycleSinkDisposition)r.ReadByte(),
				DeedState = (KingdomLifecycleSinkState)r.ReadByte(), GuestbookLine = S(r, false, true),
				GuestbookDisposition = (KingdomLifecycleSinkDisposition)r.ReadByte(),
				GuestbookState = (KingdomLifecycleSinkState)r.ReadByte()
			};
		}

		private static void WriteProof(BinaryWriter w, KingdomLifecycleProof x)
		{
			if (x == null) throw new InvalidDataException("null proof");
			w.Write(x.Sequence); S(w, x.Id, true); S(w, x.PlanHash, true);
			w.Write((byte)x.Lane); w.Write((byte)x.Action); w.Write(x.Tick);
		}

		private static KingdomLifecycleProof ReadProof(BinaryReader r)
		{
			return new KingdomLifecycleProof
			{
				Sequence = r.ReadInt64(), Id = S(r, true), PlanHash = S(r, true),
				Lane = (KingdomLifecycleLane)r.ReadByte(),
				Action = (KingdomLifecycleAction)r.ReadByte(), Tick = r.ReadInt64()
			};
		}

		private static void WriteCarryOperation(BinaryWriter w, KingdomCarryOperation o)
		{
			w.Write(o != null);
			if (o == null) return;
			EnsureCount(o.Sources, KingdomLifecycleRules.MaxCarrySources, "carry sources");
			EnsureCount(o.Outputs, KingdomLifecycleRules.MaxCarryOutputs, "carry outputs");
			EnsureCount(o.SettlementIds, KingdomLifecycleRules.MaxSettlementIds,
				"frozen carry settlement ids");
			w.Write(o.Sequence); S(w, o.Id, true); S(w, o.PlanHash, true);
			w.Write((byte)o.Phase); w.Write(o.CreatedTick); w.Write(o.UpdatedTick);
			w.Write(o.SettlementIds.Count);
			for (int i = 0; i < o.SettlementIds.Count; i++) S(w, o.SettlementIds[i], true);
			S(w, o.RealmTopologyHash, true);
			S(w, o.OriginSettlementId, true); S(w, o.OriginZoneId, false);
			w.Write(o.OriginX); w.Write(o.OriginY);
			S(w, o.DestinationSettlementId, true); S(w, o.DestinationSettlementName, false);
			S(w, o.DestinationZoneId, false); w.Write((byte)o.DestinationTopology);
			S(w, o.DestinationOwnerId, true); w.Write(o.DestinationX); w.Write(o.DestinationY);
			w.Write(o.DueTick); w.Write(o.RiskFrozen); w.Write(o.LostOnRoad);
			w.Write(o.SourceIndex); w.Write(o.OutputIndex);
			WriteLease(w, o.ScheduleLease);
			S(w, o.ScheduleReceiptId, false); S(w, o.ScheduleTopologyId, false);
			w.Write(o.ScheduleBeforeMatches); w.Write(o.ScheduleAfterMatches);
			w.Write(o.ScheduleSameReference); S(w, o.ScheduleProofId, false);
			w.Write((byte)o.ScheduleReceiptState);
			w.Write(o.Sources.Count);
			for (int i = 0; i < o.Sources.Count; i++) WriteCarrySource(w, o.Sources[i]);
			w.Write(o.Outputs.Count);
			for (int i = 0; i < o.Outputs.Count; i++) WriteProjection(w, o.Outputs[i]);
			WriteSix(w, o.Mud, o.Brush, o.Timber, o.Stone, o.Marble, o.Scrap);
			WriteSix(w, o.EscrowMud, o.EscrowBrush, o.EscrowTimber, o.EscrowStone,
				o.EscrowMarble, o.EscrowScrap);
			WriteSix(w, o.DeliveredMud, o.DeliveredBrush, o.DeliveredTimber, o.DeliveredStone,
				o.DeliveredMarble, o.DeliveredScrap);
			WriteSix(w, o.LostMud, o.LostBrush, o.LostTimber, o.LostStone, o.LostMarble, o.LostScrap);
			WriteOutbox(w, o.Outbox); S(w, o.Fault, false, true);
		}

		private static KingdomCarryOperation ReadCarryOperation(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			KingdomCarryOperation o = new KingdomCarryOperation();
			o.Sequence = r.ReadInt64(); o.Id = S(r, true); o.PlanHash = S(r, true);
			o.Phase = (KingdomLifecyclePhase)r.ReadByte(); o.CreatedTick = r.ReadInt64();
			o.UpdatedTick = r.ReadInt64();
			int settlements = ReadCount(r, KingdomLifecycleRules.MaxSettlementIds);
			o.SettlementIds = new List<string>(settlements);
			for (int i = 0; i < settlements; i++) o.SettlementIds.Add(S(r, true));
			o.RealmTopologyHash = S(r, true); o.OriginSettlementId = S(r, true);
			o.OriginZoneId = S(r, false);
			o.OriginX = r.ReadInt32(); o.OriginY = r.ReadInt32();
			o.DestinationSettlementId = S(r, true); o.DestinationSettlementName = S(r, false);
			o.DestinationZoneId = S(r, false);
			o.DestinationTopology = (KingdomLifecycleTopology)r.ReadByte();
			o.DestinationOwnerId = S(r, true);
			o.DestinationX = r.ReadInt32(); o.DestinationY = r.ReadInt32();
			o.DueTick = r.ReadInt64(); o.RiskFrozen = ReadExactBoolean(r);
			o.LostOnRoad = ReadExactBoolean(r);
			o.SourceIndex = r.ReadInt32(); o.OutputIndex = r.ReadInt32();
			o.ScheduleLease = ReadLease(r);
			o.ScheduleReceiptId = S(r, false); o.ScheduleTopologyId = S(r, false);
			o.ScheduleBeforeMatches = r.ReadInt32(); o.ScheduleAfterMatches = r.ReadInt32();
			o.ScheduleSameReference = ReadExactBoolean(r); o.ScheduleProofId = S(r, false);
			o.ScheduleReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte();
			int sources = ReadCount(r, KingdomLifecycleRules.MaxCarrySources);
			o.Sources = new List<KingdomCarrySource>(sources);
			for (int i = 0; i < sources; i++) o.Sources.Add(ReadCarrySource(r));
			int outputs = ReadCount(r, KingdomLifecycleRules.MaxCarryOutputs);
			o.Outputs = new List<KingdomLifecycleProjection>(outputs);
			for (int i = 0; i < outputs; i++) o.Outputs.Add(ReadProjection(r));
			ReadSix(r, out o.Mud, out o.Brush, out o.Timber, out o.Stone, out o.Marble, out o.Scrap);
			ReadSix(r, out o.EscrowMud, out o.EscrowBrush, out o.EscrowTimber, out o.EscrowStone,
				out o.EscrowMarble, out o.EscrowScrap);
			ReadSix(r, out o.DeliveredMud, out o.DeliveredBrush, out o.DeliveredTimber,
				out o.DeliveredStone, out o.DeliveredMarble, out o.DeliveredScrap);
			ReadSix(r, out o.LostMud, out o.LostBrush, out o.LostTimber, out o.LostStone,
				out o.LostMarble, out o.LostScrap);
			o.Outbox = ReadOutbox(r); o.Fault = S(r, false, true);
			return o;
		}

		private static void WriteCarrySource(BinaryWriter w, KingdomCarrySource x)
		{
			if (x == null) throw new InvalidDataException("null carry source");
			S(w, x.OperationId, true); S(w, x.SourceEventId, true); S(w, x.ObjectId, true);
			S(w, x.Blueprint, false); w.Write((byte)x.Topology); S(w, x.OwnerId, true);
			S(w, x.ZoneId, false); w.Write(x.X); w.Write(x.Y); w.Write(x.Material);
			w.Write(x.OriginalCount); w.Write(x.PlannedCount); w.Write(x.Removed); w.Write(x.UnitCursor);
			w.Write(x.UnitBefore); w.Write(x.UnitAfter); S(w, x.UnitEventId, true);
			w.Write((byte)x.UnitState); S(w, x.ReceiptId, false); S(w, x.ReceiptTopologyId, false);
			w.Write(x.ReceiptBeforeIdMatches); w.Write(x.ReceiptAfterIdMatches);
			w.Write(x.ReceiptBeforeCount); w.Write(x.ReceiptAfterCount);
			w.Write(x.ReceiptSameReference); S(w, x.ReceiptProofId, false);
			S(w, x.ReceiptChainId, false); w.Write(x.ReceiptChainCount);
			w.Write((byte)x.ReceiptState); w.Write((byte)x.State);
		}

		private static KingdomCarrySource ReadCarrySource(BinaryReader r)
		{
			return new KingdomCarrySource
			{
				OperationId = S(r, true), SourceEventId = S(r, true), ObjectId = S(r, true),
				Blueprint = S(r, false), Topology = (KingdomLifecycleTopology)r.ReadByte(),
				OwnerId = S(r, true), ZoneId = S(r, false), X = r.ReadInt32(), Y = r.ReadInt32(),
				Material = r.ReadInt32(), OriginalCount = r.ReadInt32(), PlannedCount = r.ReadInt32(),
				Removed = r.ReadInt32(),
				UnitCursor = r.ReadInt32(), UnitBefore = r.ReadInt32(), UnitAfter = r.ReadInt32(),
				UnitEventId = S(r, true), UnitState = (KingdomLifecyclePhysicalState)r.ReadByte(),
				ReceiptId = S(r, false), ReceiptTopologyId = S(r, false),
				ReceiptBeforeIdMatches = r.ReadInt32(), ReceiptAfterIdMatches = r.ReadInt32(),
				ReceiptBeforeCount = r.ReadInt32(), ReceiptAfterCount = r.ReadInt32(),
				ReceiptSameReference = ReadExactBoolean(r), ReceiptProofId = S(r, false),
				ReceiptChainId = S(r, false), ReceiptChainCount = r.ReadInt32(),
				ReceiptState = (KingdomLifecyclePhysicalState)r.ReadByte(),
				State = (KingdomLifecyclePhysicalState)r.ReadByte()
			};
		}

		private static void WriteOption(BinaryWriter w, KingdomLifecycleOptionState s, long tick)
		{
			w.Write((byte)s); w.Write(tick);
		}

		private static void ReadOption(BinaryReader r, out KingdomLifecycleOptionState s, out long tick)
		{
			s = (KingdomLifecycleOptionState)r.ReadByte(); tick = r.ReadInt64();
		}

		private static void WriteSix(BinaryWriter w, int a, int b, int c, int d, int e, int f)
		{
			w.Write(a); w.Write(b); w.Write(c); w.Write(d); w.Write(e); w.Write(f);
		}

		private static void ReadSix(BinaryReader r, out int a, out int b, out int c,
			out int d, out int e, out int f)
		{
			a = r.ReadInt32(); b = r.ReadInt32(); c = r.ReadInt32();
			d = r.ReadInt32(); e = r.ReadInt32(); f = r.ReadInt32();
		}

		private static string S(BinaryReader r, bool id, bool text = false)
		{
			return ReadString(r, text ? KingdomLifecycleRules.MaxTextBytes
				: id ? KingdomLifecycleRules.MaxIdBytes : KingdomLifecycleRules.MaxNameBytes);
		}

		private static void S(BinaryWriter w, string value, bool id, bool text = false)
		{
			WriteString(w, value, text ? KingdomLifecycleRules.MaxTextBytes
				: id ? KingdomLifecycleRules.MaxIdBytes : KingdomLifecycleRules.MaxNameBytes);
		}

		private static void EnsureCount<T>(List<T> rows, int maximum, string description)
		{
			if (rows == null || rows.Count > maximum)
				throw new InvalidDataException("invalid " + description);
		}

		private static void Reject(KingdomLifecycleBook target, string fault)
		{
			target.WireRejected = true; target.Quarantined = true; target.Fault = fault;
			throw new InvalidDataException(fault);
		}

		private static void Reject(KingdomCarryBook target, string fault)
		{
			target.WireRejected = true; target.Quarantined = true; target.Fault = fault;
			throw new InvalidDataException(fault);
		}

		private static void Poison(KingdomLifecycleBook target, string fault)
		{
			target.WireRejected = true;
			target.Quarantined = true;
			if (string.IsNullOrEmpty(target.Fault)) target.Fault = fault;
			target.PlainGuest = null;
			target.NotableGuest = null;
			target.Raid = null;
			target.Petition = null;
			target.Resources = new List<KingdomLifecycleResourceRevision>();
			target.RecentProofs = new List<KingdomLifecycleProof>();
		}

		private static void Poison(KingdomCarryBook target, string fault)
		{
			target.WireRejected = true;
			target.Quarantined = true;
			if (string.IsNullOrEmpty(target.Fault)) target.Fault = fault;
			target.Open = null;
			target.SettlementIds = new List<string>();
			target.Resources = new List<KingdomLifecycleResourceRevision>();
			target.RecentProofs = new List<KingdomLifecycleProof>();
		}

		private static void Copy(KingdomLifecycleBook a, KingdomLifecycleBook b)
		{
			b.FormatVersion = a.FormatVersion; b.LegacyIdentity = a.LegacyIdentity;
			b.LegacyMigrationKey = a.LegacyMigrationKey; b.Quarantined = a.Quarantined;
			b.Fault = a.Fault; b.SettlementId = a.SettlementId;
			b.IdentityBound = a.IdentityBound; b.IdentityProof = a.IdentityProof;
			b.PlainGuestNextSequence = a.PlainGuestNextSequence;
			b.PlainGuestRetiredThrough = a.PlainGuestRetiredThrough;
			b.NotableGuestNextSequence = a.NotableGuestNextSequence;
			b.NotableGuestRetiredThrough = a.NotableGuestRetiredThrough;
			b.RaidNextSequence = a.RaidNextSequence; b.RaidRetiredThrough = a.RaidRetiredThrough;
			b.PetitionNextSequence = a.PetitionNextSequence;
			b.PetitionRetiredThrough = a.PetitionRetiredThrough;
			b.LocusOption = a.LocusOption; b.LocusOptionTick = a.LocusOptionTick;
			b.NotableOption = a.NotableOption; b.NotableOptionTick = a.NotableOptionTick;
			b.RaidOption = a.RaidOption; b.RaidOptionTick = a.RaidOptionTick;
			b.PetitionOption = a.PetitionOption; b.PetitionOptionTick = a.PetitionOptionTick;
			b.PlainGuest = a.PlainGuest; b.NotableGuest = a.NotableGuest;
			b.Raid = a.Raid; b.Petition = a.Petition;
			b.Resources = a.Resources; b.RecentProofs = a.RecentProofs; b.WireRejected = false;
		}

		private static void Copy(KingdomCarryBook a, KingdomCarryBook b)
		{
			b.FormatVersion = a.FormatVersion; b.LegacyIdentity = a.LegacyIdentity;
			b.LegacyMigrationKey = a.LegacyMigrationKey; b.Quarantined = a.Quarantined;
			b.Fault = a.Fault; b.RealmId = a.RealmId; b.SettlementIds = a.SettlementIds;
			b.IdentityBound = a.IdentityBound; b.IdentityProof = a.IdentityProof;
			b.NextSequence = a.NextSequence;
			b.RetiredThrough = a.RetiredThrough; b.Open = a.Open;
			b.Resources = a.Resources; b.RecentProofs = a.RecentProofs; b.WireRejected = false;
		}
	}
}
