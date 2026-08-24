using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public enum KingdomLifecycleOptionAction : byte
	{
		None = 0,
		StayDisabled = 1,
		Disable = 2,
		EnableAndRestamp = 3,
		Quarantine = 4
	}

	public sealed class KingdomLifecycleOptionDecision
	{
		public bool Valid;
		public KingdomLifecycleOptionAction Action;
		public KingdomLifecycleOptionState State;
		public long Tick;
		public bool AllowNewWork;
		public bool ReconcileOpenWork;
	}

	public sealed class KingdomGrowthAvailabilityDecision
	{
		public bool Valid;
		public string Failure;
		public bool AllowStarters;
		public bool ReconcileOpen;
		public KingdomLifecycleOptionState OptionState;
		public KingdomGrowthHealthState HealthState;
		public long ObservedTick;
		public bool WorkPaused;
		public long PauseStartedTick;
		public long PausedTicks;
		public long EffectiveWorkTick;
		public bool RestampClocks;
		public long NextArrivalTick;
		public long ArrivalIntervalTicks;
	}

	public enum KingdomLifecycleMutationAction : byte
	{
		Settled = 0,
		InvokeOnce = 1,
		ConfirmAfter = 2,
		Quarantine = 3
	}

	public enum KingdomLifecycleCasAction : byte
	{
		Apply = 1,
		Confirm = 2,
		Quarantine = 3
	}

	#if TAF_TESTS
	/// <summary>
	/// Runtime trust boundary. Implementations must expose opaque live engine references and derive
	/// every field from a bounded scan of the real Qud object graph. They must never wrap a
	/// caller-authored DTO. The dormant lane's engine adapter belongs at the shell, outside Rules.
	/// </summary>
	internal interface IKingdomLifecycleTrustedObservation
	{
		object Reference { get; }
		string ObjectId { get; }
		string Marker { get; }
		string Blueprint { get; }
		string SettlementId { get; }
		string OwnerId { get; }
		string ZoneId { get; }
		KingdomLifecycleTopology Topology { get; }
		int X { get; }
		int Y { get; }
		int Count { get; }
		int Capacity { get; }
		string Composition { get; }
		long Value { get; }
		long Revision { get; }
		string LastOperationId { get; }
	}

	/// <summary>
	/// Trusted shell owns both the global scan and callback. Rules callers cannot supply observation
	/// literals, success booleans, or a callback detached from that same live graph.
	/// </summary>
	internal interface IKingdomLifecycleTrustedWorld
	{
		int ObservationCount { get; }
		IKingdomLifecycleTrustedObservation Observe(int Index);
		object InvokeCarryOutput(KingdomLifecycleProjection Output);
		object InvokeWater(object VesselReference, int Amount);
		object InvokeSchedule(object ScheduleReference, long DueTick, string OperationId);
		object InvokeCarryRemoval(object SourceReference, int Count, string UnitEventId);
		object InvokeLifecycleProjection(KingdomLifecycleProjection Projection);
		object InvokeLifecycleRemoval(object ObjectReference, int Count, string OperationId);
	}
	#endif

	[Flags]
	public enum KingdomLifecycleSinkMask : byte
	{
		None = 0,
		Chronicle = 1,
		Ledger = 2,
		Message = 4,
		Deed = 8,
		Guestbook = 16
	}

	/// <summary>Engine-free authority, replay, FSM, and conservation laws.</summary>
	public static class KingdomLifecycleRules
	{
		public const int LegacyLifecycleFormatVersion = 5;
		public const int CurrentFormatVersion = 6;
		public const int CurrentCarryFormatVersion = 5;
		public const int LegacyGrowthFormatVersion = 1;
		public const int CurrentGrowthFormatVersion = 2;
		public const int MaxGrowthFields = 8;
		public const int MaxGrowthSources = 64;
		public const int MaxGrowthOutputs = 96;
		public const int MaxGrowthOutboxEvents = 12;
		public const int MaxGrowthObjectCallbacks = 4;
		public const int MaxGrowthCropRows = 96;
		public const int MaxGrowthSectionBytes = 512 * 1024;
		public const int MaxRecentProofs = 64;
		public const int MaxWaterLegs = 24;
		public const int MaxProjections = 64;
		public const int MaxResourceLeases = 32;
		public const int MaxResourceRows = 128;
		public const int MaxCarrySources = 64;
		public const int MaxCarryOutputs = 64;
		public const int MaxSettlementIds = 4;
		public const int MaxLifecycleCollisionIds = 64;
		public const int MaxCoordinate = 4095;
		public const int MaxIdChars = 256;
		public const int MaxNameChars = 512;
		public const int MaxTextChars = 4096;
		public const int MaxIdBytes = MaxIdChars * 4;
		public const int MaxNameBytes = MaxNameChars * 4;
		public const int MaxTextBytes = MaxTextChars * 4;
		public const int MaxPhysicalCount = 1000000;

		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static bool CanOwnAuthority(KingdomLifecycleBook Book)
		{
			return Book != null && !Book.WireRejected && !Book.Quarantined
				&& Book.FormatVersion == CurrentFormatVersion && ValidRootId(Book.SettlementId)
				&& Book.IdentityBound && ExactSettlementIdentityProof(Book)
				&& LifecycleBookShape(Book);
		}

		public static bool CanOwnAuthority(KingdomCarryBook Book)
		{
			return Book != null && !Book.WireRejected && !Book.Quarantined
				&& Book.FormatVersion == CurrentCarryFormatVersion && ValidRootId(Book.RealmId)
				&& Book.IdentityBound && ExactCarryIdentityProof(Book)
				&& CarryBookShape(Book);
		}

		/// <summary>Only for one explicit migration. New work accepts Core's exact City.SettlementId.</summary>
		public static string LegacySettlementId(string RealmFaction, long FoundedTick,
			string FirstClaimedZone)
		{
			return HashId("legacy-settlement", delegate(BinaryWriter w)
			{
				CanonicalString(w, RealmFaction);
				w.Write(FoundedTick);
				CanonicalString(w, FirstClaimedZone);
			});
		}

		public static bool BindSettlementIdentity(KingdomLifecycleBook Book, string ExactId,
			bool LegacyMigration, string MigrationKey, ICollection<string> ExistingIds)
		{
			if (Book == null || !ValidRootId(ExactId)) return false;
			if (Book.IdentityBound || !string.IsNullOrEmpty(Book.SettlementId)
				|| !string.IsNullOrEmpty(Book.IdentityProof))
				return ExistingIdsExclude(ExistingIds, ExactId) && CanOwnAuthority(Book)
					&& string.Equals(Book.SettlementId, ExactId, StringComparison.Ordinal)
					&& Book.LegacyIdentity == LegacyMigration
					&& string.Equals(Book.LegacyMigrationKey,
						LegacyMigration ? MigrationKey : null, StringComparison.Ordinal);
			if (LegacyMigration && !ValidRootId(MigrationKey)) return false;
			if (!LegacyMigration && !string.IsNullOrEmpty(MigrationKey)) return false;
			if (!ExistingIdsExclude(ExistingIds, ExactId) || !PristineLifecycleBook(Book) ||
				!PristineGrowthBook(Book.Growth))
				return false;
			KingdomGrowthBook growth = NewBoundGrowth(ExactId);
			if (growth == null) return false;
			Book.SettlementId = ExactId;
			Book.LegacyIdentity = LegacyMigration;
			Book.LegacyMigrationKey = LegacyMigration ? MigrationKey : null;
			Book.IdentityBound = true;
			Book.IdentityProof = SettlementIdentityProof(Book.SettlementId,
				Book.LegacyIdentity, Book.LegacyMigrationKey);
			Book.Growth = growth;
			return ExactSettlementIdentityProof(Book);
		}

		public static bool BindCarryIdentity(KingdomCarryBook Book, string RealmId,
			ICollection<string> SettlementIds, bool LegacyMigration, string MigrationKey)
		{
			if (Book == null || !ValidRootId(RealmId)) return false;
			List<string> frozen;
			if (!TryFrozenSettlementSet(SettlementIds, out frozen)) return false;
			if (Book.IdentityBound || !string.IsNullOrEmpty(Book.RealmId)
				|| !string.IsNullOrEmpty(Book.IdentityProof)
				|| (Book.SettlementIds != null && Book.SettlementIds.Count > 0))
				return CanOwnAuthority(Book)
					&& string.Equals(Book.RealmId, RealmId, StringComparison.Ordinal)
					&& Book.LegacyIdentity == LegacyMigration
					&& string.Equals(Book.LegacyMigrationKey,
						LegacyMigration ? MigrationKey : null, StringComparison.Ordinal)
					&& ExactStringList(Book.SettlementIds, frozen);
			if (!PristineCarryBook(Book)) return false;
			if (LegacyMigration ? !ValidRootId(MigrationKey) : !string.IsNullOrEmpty(MigrationKey))
				return false;
			Book.RealmId = RealmId;
			Book.SettlementIds = frozen;
			Book.LegacyIdentity = LegacyMigration;
			Book.LegacyMigrationKey = LegacyMigration ? MigrationKey : null;
			Book.IdentityBound = true;
			Book.IdentityProof = CarryIdentityProof(Book.RealmId, Book.SettlementIds,
				Book.LegacyIdentity, Book.LegacyMigrationKey);
			return ExactCarryIdentityProof(Book);
		}

		/// <summary>Builds the first city's two authority books off-graph. Dirty dormant
		/// books are evidence and are never overwritten during first publication.</summary>
		public static bool TryPrepareFirstIdentityBooks(KingdomLifecycleBook ExistingLifecycle,
			KingdomCarryBook ExistingCarry, string RealmId, string SettlementId,
			out KingdomLifecycleBook Lifecycle, out KingdomCarryBook Carry)
		{
			Lifecycle = null;
			Carry = null;
			KingdomLifecycleBook sourceLifecycle = ExistingLifecycle ??
				new KingdomLifecycleBook();
			KingdomCarryBook sourceCarry = ExistingCarry ?? new KingdomCarryBook();
			if (!PristineLifecycleBook(sourceLifecycle) ||
				!PristineCarryBook(sourceCarry)) return false;
			KingdomLifecycleBook lifecycle = new KingdomLifecycleBook();
			KingdomCarryBook carry = new KingdomCarryBook();
			if (!BindSettlementIdentity(lifecycle, SettlementId, LegacyMigration: false,
				MigrationKey: null, ExistingIds: new List<string>()) ||
				!BindCarryIdentity(carry, RealmId, new List<string> { SettlementId },
					LegacyMigration: false, MigrationKey: null)) return false;
			Lifecycle = lifecycle;
			Carry = carry;
			return true;
		}

		/// <summary>Preflights a monotone exact-city expansion without changing the book.</summary>
		public static bool CanExpandCarryIdentity(KingdomCarryBook Book, string RealmId,
			ICollection<string> SettlementIds, out string Failure)
		{
			Failure = null;
			if (!CanOwnAuthority(Book))
			{
				Failure = "Carry identity expansion requires bound exact authority.";
				return false;
			}
			List<string> frozen;
			if (!TryFrozenSettlementSet(SettlementIds, out frozen))
			{
				Failure = "Carry identity expansion candidate is malformed or exceeds cap.";
				return false;
			}
			if (!CanOwnAuthority(Book))
			{
				Failure = "Carry authority changed while its expansion candidate was frozen.";
				return false;
			}
			if (!string.Equals(Book.RealmId, RealmId, StringComparison.Ordinal))
			{
				Failure = "The immutable carry realm changed during identity expansion.";
				return false;
			}
			if (ExactStringList(Book.SettlementIds, frozen)) return true;
			for (int i = 0; i < Book.SettlementIds.Count; i++)
				if (!frozen.Contains(Book.SettlementIds[i]))
				{
					Failure = "An exact carry settlement identity was removed or replaced.";
					return false;
				}
			if (Book.Open != null)
			{
				Failure = "Carry identity expansion deferred while a haul receipt is open.";
				return false;
			}
			return true;
		}

		/// <summary>Publishes a preflighted monotone exact-city expansion and its new proof.</summary>
		public static bool ExpandCarryIdentity(KingdomCarryBook Book, string RealmId,
			ICollection<string> SettlementIds, out string Failure)
		{
			Failure = null;
			if (Book == null || !CanOwnAuthority(Book))
			{
				Failure = "Carry identity expansion requires bound exact authority.";
				return false;
			}
			List<string> frozen;
			if (!TryFrozenSettlementSet(SettlementIds, out frozen))
			{
				Failure = "Carry identity expansion candidate is malformed or exceeds cap.";
				return false;
			}
			if (!CanOwnAuthority(Book))
			{
				Deny(Book, "carry authority changed while its expansion candidate was frozen");
				Failure = Book.Fault;
				return false;
			}
			if (!string.Equals(Book.RealmId, RealmId, StringComparison.Ordinal))
			{
				Deny(Book, "immutable carry realm changed during identity expansion");
				Failure = Book.Fault;
				return false;
			}
			if (ExactStringList(Book.SettlementIds, frozen)) return true;
			for (int i = 0; i < Book.SettlementIds.Count; i++)
				if (!frozen.Contains(Book.SettlementIds[i]))
				{
					Deny(Book, "exact carry settlement identity was removed or replaced");
					Failure = Book.Fault;
					return false;
				}
			if (Book.Open != null)
			{
				Failure = "Carry identity expansion deferred while a haul receipt is open.";
				return false;
			}
			List<string> previous = Book.SettlementIds;
			string previousProof = Book.IdentityProof;
			Book.SettlementIds = frozen;
			Book.IdentityProof = CarryIdentityProof(Book.RealmId, Book.SettlementIds,
				Book.LegacyIdentity, Book.LegacyMigrationKey);
			if (CanOwnAuthority(Book)) return true;
			Book.SettlementIds = previous;
			Book.IdentityProof = previousProof;
			Deny(Book, "expanded carry identity did not retain exact authority");
			Failure = Book.Fault;
			return false;
		}

		#if TAF_TESTS
		/// <summary>
		/// Only the runtime shell may call this seam. Receipt constructors stay private and all
		/// observations are re-derived from the same opaque world before and after its callback.
		/// Public Rules APIs cannot mint a physical or schedule receipt from literals.
		/// </summary>
		internal static class TrustedAdapter
		{
			private const string ScheduleBlueprint = "Schedule";

			private sealed class Snapshot
			{
				internal readonly object Reference;
				internal readonly string ObjectId;
				internal readonly string Marker;
				internal readonly string Blueprint;
				internal readonly string SettlementId;
				internal readonly string OwnerId;
				internal readonly string ZoneId;
				internal readonly KingdomLifecycleTopology Topology;
				internal readonly int X;
				internal readonly int Y;
				internal readonly int Count;
				internal readonly int Capacity;
				internal readonly string Composition;
				internal readonly long Value;
				internal readonly long Revision;
				internal readonly string LastOperationId;

				private Snapshot(IKingdomLifecycleTrustedObservation source)
				{
					Reference = source.Reference;
					ObjectId = source.ObjectId;
					Marker = source.Marker;
					Blueprint = source.Blueprint;
					SettlementId = source.SettlementId;
					OwnerId = source.OwnerId;
					ZoneId = source.ZoneId;
					Topology = source.Topology;
					X = source.X;
					Y = source.Y;
					Count = source.Count;
					Capacity = source.Capacity;
					Composition = source.Composition;
					Value = source.Value;
					Revision = source.Revision;
					LastOperationId = source.LastOperationId;
				}

				internal static Snapshot Capture(IKingdomLifecycleTrustedObservation source)
				{
					return source == null ? null : new Snapshot(source);
				}
			}

			private sealed class CallbackReceipt
			{
				internal readonly Snapshot Before;
				internal readonly Snapshot After;
				internal readonly object Returned;

				private CallbackReceipt(Snapshot before, Snapshot after, object returned)
				{
					Before = before;
					After = after;
					Returned = returned;
				}

				internal static CallbackReceipt Create(Snapshot before,
					Snapshot after, object returned)
				{
					return new CallbackReceipt(before, after, returned);
				}
			}

			internal static KingdomLifecycleResourceLease PreparePhysicalLease(
				KingdomLifecycleBook book, KingdomLifecycleOperation operation,
				KingdomLifecycleResourceKind kind, string scopeId, string subjectId,
				long before, long delta)
			{
				return IsPhysicalResourceKind(kind)
					? PrepareLeaseCore(book, operation, kind, scopeId, subjectId, before, delta)
					: null;
			}

			internal static bool ProveCarrySource(KingdomCarryBook book,
				KingdomCarryOperation operation, KingdomCarrySource source,
				IKingdomLifecycleTrustedWorld world)
			{
				int sourceIndex = IndexOfSource(operation, source);
				int beforeMatches;
				Snapshot before = ExactObservation(world, delegate(Snapshot value)
				{
					return string.Equals(value.ObjectId, source == null ? null : source.ObjectId,
						StringComparison.Ordinal);
				}, out beforeMatches);
				if (sourceIndex < 0 || beforeMatches != 1 || before == null
					|| !ExactCarrySourceFields(before, source, source.UnitBefore)
					|| !BeginCarryUnitCore(book, operation, source)) return false;
				source.LiveAuthority = before.Reference;
				object returned;
				try { returned = world.InvokeCarryRemoval(before.Reference, 1, source.UnitEventId); }
				catch (Exception) { return false; }
				if (returned == null) return false;
				int afterMatches;
				Snapshot after = ExactObservation(world, delegate(Snapshot value)
				{
					return string.Equals(value.ObjectId, source.ObjectId, StringComparison.Ordinal);
				}, out afterMatches);
				CallbackReceipt receipt = CallbackReceipt.Create(before, after, returned);
				if (afterMatches != 1 || receipt.After == null
					|| !ExactCarrySourceFields(receipt.After, source, source.UnitAfter)
					|| !ReferenceEquals(receipt.Before.Reference, receipt.Returned)
					|| !ReferenceEquals(receipt.After.Reference, receipt.Returned)) return false;
				source.ReceiptAfterIdMatches = 1;
				source.ReceiptAfterCount = receipt.After.Count;
				source.ReceiptSameReference = true;
				source.ReceiptProofId = CarrySourceReceiptProof(operation, source, sourceIndex);
				source.ReceiptState = KingdomLifecyclePhysicalState.Proved;
				return ConfirmCarryUnitCore(book, operation, source);
			}

			internal static bool ProveLifecycleProjection(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, KingdomLifecycleProjection projection,
				IKingdomLifecycleTrustedWorld world)
			{
				if (!ExactOperationAuthority(book, operation) || projection == null
					|| operation.Phase != KingdomLifecyclePhase.ProjectionIntent
					|| projection.State != KingdomLifecyclePhysicalState.Prepared) return false;
				string topology = TopologyId(projection.Topology, projection.OwnerId,
					projection.ZoneId, projection.X, projection.Y);
				KingdomLifecycleResourceLease lease = FindLease(operation, ResourceKey(
					KingdomLifecycleResourceKind.Projection, topology, projection.ObjectId));
				if (lease == null || !ReferenceEquals(ProjectionForLease(operation, lease), projection))
					return false;
				int beforeIds;
				int beforeMarkers;
				ScanOutput(world, projection, out beforeIds, out beforeMarkers);
				if (beforeIds != 0 || beforeMarkers != 0
					|| !BeginLeaseCore(book, lease, lease.Before)) return false;
				projection.State = KingdomLifecyclePhysicalState.Intent;
				object returned;
				try { returned = world.InvokeLifecycleProjection(projection); }
				catch (Exception) { return false; }
				if (returned == null) return false;
				int afterIds;
				int afterMarkers;
				Snapshot after = ScanOutput(world, projection, out afterIds, out afterMarkers);
				if (afterIds != 1 || afterMarkers != 1 || after == null
					|| !ReferenceEquals(after.Reference, returned)
					|| !string.Equals(after.Marker, projection.Marker, StringComparison.Ordinal)
					|| !string.Equals(after.Blueprint, projection.Blueprint, StringComparison.Ordinal)
					|| after.Count != projection.Count || !ExactTopology(after,
						projection.Topology, projection.OwnerId, projection.ZoneId,
						projection.X, projection.Y)) return false;
				int spawned;
				if (!CheckedAdd(operation.Spawned, projection.Count, out spawned)
					|| !ValidCount(spawned)) return false;
				KingdomLifecycleResourceRevision row = FindResource(book, lease.Key);
				if (!CommitLeaseWitnessCore(book, operation, lease, row, lease.After)) return false;
				projection.State = KingdomLifecyclePhysicalState.Proved;
				projection.LiveAuthority = returned;
				operation.Spawned = spawned;
				return ExactOperationAuthority(book, operation);
			}

			internal static bool ProveLifecycleRemoval(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, IKingdomLifecycleTrustedWorld world)
			{
				if (!ExactOperationAuthority(book, operation)
					|| operation.Phase != KingdomLifecyclePhase.RemovalIntent
					|| operation.RemovalState != KingdomLifecyclePhysicalState.Prepared
					|| !ValidName(operation.Blueprint)) return false;
				string topology = TopologyId(operation.ObjectTopology, operation.ObjectOwnerId,
					operation.ZoneId, operation.ObjectX, operation.ObjectY);
				KingdomLifecycleResourceLease lease = FindLease(operation, ResourceKey(
					KingdomLifecycleResourceKind.Object, topology, operation.ObjectId));
				int beforeMatches;
				Snapshot before = ExactObservation(world, delegate(Snapshot value)
				{
					return string.Equals(value.ObjectId, operation.ObjectId, StringComparison.Ordinal);
				}, out beforeMatches);
				if (lease == null || beforeMatches != 1 || before == null
					|| !ExactLifecycleObjectFields(before, operation, operation.Count)
					|| !BeginLeaseCore(book, lease, lease.Before)) return false;
				operation.RemovalState = KingdomLifecyclePhysicalState.Intent;
				operation.LiveAuthority = before.Reference;
				object returned;
				try
				{
					returned = world.InvokeLifecycleRemoval(before.Reference,
						operation.Count, operation.Id);
				}
				catch (Exception) { return false; }
				if (returned == null) return false;
				int afterMatches;
				Snapshot after = ExactObservation(world, delegate(Snapshot value)
				{
					return string.Equals(value.ObjectId, operation.ObjectId, StringComparison.Ordinal);
				}, out afterMatches);
				if (afterMatches != 1 || after == null
					|| !ReferenceEquals(before.Reference, returned)
					|| !ReferenceEquals(after.Reference, returned)
					|| !ExactLifecycleObjectFields(after, operation, 0)) return false;
				KingdomLifecycleResourceRevision row = FindResource(book, lease.Key);
				if (!CommitLeaseWitnessCore(book, operation, lease, row, lease.After)) return false;
				operation.RemovalState = KingdomLifecyclePhysicalState.Proved;
				return ExactOperationAuthority(book, operation);
			}

			internal static bool ProveLifecycleSchedule(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, IKingdomLifecycleTrustedWorld world)
			{
				if (!ExactOperationAuthority(book, operation)
					|| operation.Phase != KingdomLifecyclePhase.ScheduleIntent) return false;
				string subject = ScheduleSubjectId(operation.SettlementId, operation.Lane);
				KingdomLifecycleResourceLease lease = FindLease(operation, ResourceKey(
					KingdomLifecycleResourceKind.Schedule, operation.SettlementId, subject));
				KingdomLifecycleResourceRevision row = lease == null ? null : FindResource(book, lease.Key);
				int beforeMatches;
				Snapshot before = ExactObservation(world, delegate(Snapshot value)
				{
					return string.Equals(value.ObjectId, lease == null ? null : lease.Key,
						StringComparison.Ordinal);
				}, out beforeMatches);
				if (lease == null || row == null || beforeMatches != 1 || before == null
					|| !ExactLifecycleScheduleFields(before, operation, lease.Before,
						lease.BeforeRevision, row.LastOperationId)
					|| !BeginLeaseCore(book, lease, before.Value)) return false;
				object returned;
				try { returned = world.InvokeSchedule(before.Reference, lease.After, operation.Id); }
				catch (Exception) { return false; }
				if (returned == null) return false;
				int afterMatches;
				Snapshot after = ExactObservation(world, delegate(Snapshot value)
				{
					return string.Equals(value.ObjectId, lease.Key, StringComparison.Ordinal);
				}, out afterMatches);
				if (afterMatches != 1 || after == null
					|| !ReferenceEquals(before.Reference, returned)
					|| !ReferenceEquals(after.Reference, returned)
					|| !ExactLifecycleScheduleFields(after, operation, lease.After,
						lease.AfterRevision, operation.Id)
					|| before.Topology != after.Topology || before.X != after.X || before.Y != after.Y
					|| !string.Equals(before.OwnerId, after.OwnerId, StringComparison.Ordinal)) return false;
				return CommitLeaseWitnessCore(book, operation, lease, row, after.Value);
			}

			internal static bool PrepareCarrySchedule(KingdomCarryBook book,
				KingdomCarryOperation operation, IKingdomLifecycleTrustedWorld world)
			{
				if (!CanOwnAuthority(book) || operation == null
					|| operation.Phase != KingdomLifecyclePhase.Prepared
					|| !ExactStringList(operation.SettlementIds, book.SettlementIds)
					|| !string.Equals(operation.RealmTopologyHash,
						RealmTopologyDigest(book.RealmId, book.SettlementIds), StringComparison.Ordinal)
					|| !SettlementMember(book, operation.DestinationSettlementId)) return false;
				string key = ResourceKey(KingdomLifecycleResourceKind.Schedule,
					book.RealmId, operation.DestinationSettlementId);
				int matches;
				Snapshot before = ExactObservation(world,
					delegate(Snapshot x)
					{
						return string.Equals(x.ObjectId, key, StringComparison.Ordinal);
					}, out matches);
				KingdomLifecycleResourceRevision row = FindResource(book, key);
				long revision = row == null ? 0L : row.Revision;
				if (matches != 1 || before == null || before.Reference == null
					|| !string.Equals(before.Blueprint, ScheduleBlueprint, StringComparison.Ordinal)
					|| !string.Equals(before.SettlementId, operation.DestinationSettlementId,
						StringComparison.Ordinal)
					|| before.Value < 0L || before.Revision != revision
					|| !string.Equals(before.LastOperationId,
						row == null ? null : row.LastOperationId, StringComparison.Ordinal)
					|| !TopologyValid(before.Topology, before.OwnerId, before.ZoneId,
						before.X, before.Y)) return false;
				KingdomLifecycleResourceLease lease = PrepareCarryScheduleLeaseCore(book,
					operation, before.Value);
				if (lease == null) return false;
				operation.DestinationZoneId = before.ZoneId;
				operation.DestinationTopology = before.Topology;
				operation.DestinationOwnerId = before.OwnerId;
				operation.DestinationX = before.X;
				operation.DestinationY = before.Y;
				operation.ScheduleLease = lease;
				operation.ScheduleReceiptId = ChildId(operation.Id, "schedule-receipt", 0);
				operation.ScheduleTopologyId = TopologyId(before.Topology, before.OwnerId,
					before.ZoneId, before.X, before.Y);
				operation.ScheduleReceiptState = KingdomLifecyclePhysicalState.Prepared;
				operation.LiveAuthority = before.Reference;
				return CarryScheduleReceiptShape(operation, true);
			}

			internal static bool ProveCarrySchedule(KingdomCarryBook book,
				KingdomCarryOperation operation, IKingdomLifecycleTrustedWorld world)
			{
				if (!ExactCarryAuthority(book, operation) || operation.ScheduleLease == null
					|| operation.Phase != KingdomLifecyclePhase.ScheduleIntent
					|| operation.ScheduleReceiptState != KingdomLifecyclePhysicalState.Prepared)
					return false;
				KingdomLifecycleResourceLease lease = operation.ScheduleLease;
				KingdomLifecycleResourceRevision resource = FindResource(book, lease.Key);
				int beforeMatches;
				Snapshot before = ExactScheduleObservation(world,
					operation, lease.Before, lease.BeforeRevision,
					resource == null ? null : resource.LastOperationId, out beforeMatches);
				if (beforeMatches != 1 || before == null || before.Reference == null
					|| !ExactScheduleFields(before, operation, lease.Before,
						lease.BeforeRevision, resource == null ? null : resource.LastOperationId)
					|| !BeginCarryScheduleCore(book, operation, lease, before.Value)) return false;
				operation.ScheduleBeforeMatches = 1;
				operation.ScheduleReceiptState = KingdomLifecyclePhysicalState.Intent;
				object returned;
				try { returned = world.InvokeSchedule(before.Reference, lease.After, operation.Id); }
				catch (Exception) { return false; }
				if (returned == null) return false;
				int afterMatches;
				Snapshot after = ExactScheduleObservation(world,
					operation, lease.After, lease.AfterRevision, operation.Id, out afterMatches);
				CallbackReceipt receipt = CallbackReceipt.Create(before, after, returned);
				if (afterMatches != 1 || receipt.After == null
					|| !ExactScheduleFields(receipt.After, operation, lease.After,
						lease.AfterRevision, operation.Id)
					|| !ReferenceEquals(receipt.Before.Reference, receipt.Returned)
					|| !ReferenceEquals(receipt.After.Reference, receipt.Returned)) return false;
				if (!CommitCarryScheduleCore(book, operation, lease, after.Value)) return false;
				operation.ScheduleAfterMatches = 1;
				operation.ScheduleSameReference = true;
				operation.ScheduleProofId = CarryScheduleReceiptProof(operation);
				operation.ScheduleReceiptState = KingdomLifecyclePhysicalState.Proved;
				return CarryScheduleReceiptShape(operation, false);
			}

			internal static bool ProveCarryOutput(KingdomCarryBook book,
				KingdomCarryOperation operation, KingdomLifecycleProjection output,
				IKingdomLifecycleTrustedWorld world)
			{
				int idBefore;
				int markerBefore;
				ScanOutput(world, output, out idBefore, out markerBefore);
				if (idBefore != 0 || markerBefore != 0
					|| !BeginCarryOutputCore(book, operation, output)) return false;
				object returned;
				try { returned = world.InvokeCarryOutput(output); }
				catch (Exception) { return false; }
				if (returned == null) return false;
				int idAfter;
				int markerAfter;
				Snapshot after = ScanOutput(world, output,
					out idAfter, out markerAfter);
				CallbackReceipt receipt = CallbackReceipt.Create(null, after, returned);
				if (idAfter != 1 || markerAfter != 1 || receipt.After == null
					|| !ReferenceEquals(receipt.After.Reference, receipt.Returned)
					|| !string.Equals(receipt.After.Marker, output.Marker, StringComparison.Ordinal)
					|| !string.Equals(receipt.After.Blueprint, output.Blueprint, StringComparison.Ordinal)
					|| receipt.After.Count != output.Count || !ExactTopology(receipt.After,
						output.Topology, output.OwnerId, output.ZoneId, output.X, output.Y)) return false;
				if (!ConfirmCarryOutputCore(book, operation, output)) return false;
				output.ReceiptAfterIdMatches = 1;
				output.ReceiptAfterMarkerMatches = 1;
				output.ReceiptAfterCount = receipt.After.Count;
				output.ReceiptSameReference = true;
				output.ReceiptProofId = CarryOutputReceiptProof(operation, output, false);
				output.ReceiptState = KingdomLifecyclePhysicalState.Proved;
				output.LiveAuthority = returned;
				return CarryOutputShape(output, operation.Id,
					IndexOfOutput(operation, output), false);
			}

			internal static bool ProveCarryRoadAbsence(KingdomCarryBook book,
				KingdomCarryOperation operation, KingdomLifecycleProjection output,
				IKingdomLifecycleTrustedWorld world)
			{
				int ids;
				int markers;
				ScanOutput(world, output, out ids, out markers);
				return ids == 0 && markers == 0
					&& SkipCarryOutputOnRoadCore(book, operation, output);
			}

			internal static bool ProveWater(KingdomLifecycleBook book,
				KingdomLifecycleResourceLease lease, KingdomLifecycleWaterLeg leg,
				IKingdomLifecycleTrustedWorld world)
			{
				int beforeMatches;
				Snapshot before = ExactWaterObservation(world, leg,
					leg.Before, out beforeMatches);
				if (beforeMatches != 1 || before == null || before.Reference == null
					|| !ExactWaterFields(before, leg, leg.Before)
					|| !BeginWaterLeaseCore(book, lease, leg, before.Value)) return false;
				leg.ReceiptBeforeMatches = 1;
				leg.LiveAuthority = before.Reference;
				object returned;
				try { returned = world.InvokeWater(before.Reference, leg.Delta); }
				catch (Exception) { return false; }
				if (returned == null) return false;
				int afterMatches;
				Snapshot after = ExactWaterObservation(world, leg,
					leg.After, out afterMatches);
				CallbackReceipt receipt = CallbackReceipt.Create(before, after, returned);
				if (afterMatches != 1 || receipt.After == null
					|| !ExactWaterFields(receipt.After, leg, leg.After)
					|| !ReferenceEquals(receipt.Before.Reference, receipt.Returned)
					|| !ReferenceEquals(receipt.After.Reference, receipt.Returned)) return false;
				KingdomLifecycleOperation operation = FindOpenOperation(book, lease.OperationId);
				if (!ConfirmWaterLeaseCore(book, lease, leg, after.Value)) return false;
				leg.ReceiptAfterMatches = 1;
				leg.ReceiptSameReference = true;
				leg.ReceiptProofId = WaterReceiptProof(operation, lease, leg);
				leg.ReceiptState = KingdomLifecyclePhysicalState.Proved;
				return ExactWaterReceipt(operation, lease, leg);
			}

			private static Snapshot ExactScheduleObservation(
				IKingdomLifecycleTrustedWorld world, KingdomCarryOperation operation,
				long value, long revision, string lastOperationId, out int matches)
			{
				return ExactObservation(world, delegate(Snapshot x)
				{
					return string.Equals(x.ObjectId, operation.ScheduleLease.Key, StringComparison.Ordinal);
				}, out matches);
			}

			private static Snapshot ExactWaterObservation(
				IKingdomLifecycleTrustedWorld world, KingdomLifecycleWaterLeg leg,
				long value, out int matches)
			{
				return ExactObservation(world, delegate(Snapshot x)
				{
					return string.Equals(x.ObjectId, leg.OwnerId, StringComparison.Ordinal);
				}, out matches);
			}

			private static Snapshot ScanOutput(
				IKingdomLifecycleTrustedWorld world, KingdomLifecycleProjection output,
				out int idMatches, out int markerMatches)
			{
				idMatches = 0;
				markerMatches = 0;
				Snapshot exact = null;
				List<Snapshot> observations;
				if (!TrySnapshots(world, out observations))
				{
					idMatches = -1; markerMatches = -1; return null;
				}
				for (int i = 0; i < observations.Count; i++)
				{
					Snapshot x = observations[i];
					if (string.Equals(x.ObjectId, output.ObjectId, StringComparison.Ordinal))
					{
						idMatches++;
						exact = x;
					}
					if (string.Equals(x.Marker, output.Marker, StringComparison.Ordinal)) markerMatches++;
				}
				return exact;
			}

			private static Snapshot ExactObservation(
				IKingdomLifecycleTrustedWorld world,
				Predicate<Snapshot> predicate, out int matches)
			{
				matches = 0;
				Snapshot exact = null;
				List<Snapshot> observations;
				if (predicate == null || !TrySnapshots(world, out observations)) return null;
				for (int i = 0; i < observations.Count; i++)
				{
					Snapshot x = observations[i];
					if (!predicate(x)) continue;
					matches++;
					exact = x;
				}
				return exact;
			}

			private static bool TrySnapshots(IKingdomLifecycleTrustedWorld world,
				out List<Snapshot> snapshots)
			{
				snapshots = null;
				if (world == null) return false;
				try
				{
					int count = world.ObservationCount;
					if (count < 0 || count > MaxPhysicalCount) return false;
					List<Snapshot> captured = new List<Snapshot>(count);
					for (int i = 0; i < count; i++)
					{
						Snapshot value = Snapshot.Capture(world.Observe(i));
						if (!ObservationShape(value)) return false;
						captured.Add(value);
					}
					snapshots = captured;
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}

			private static bool ObservationShape(Snapshot value)
			{
				return value != null && value.Reference != null
					&& !TooLong(value.ObjectId, MaxIdChars)
					&& !TooLong(value.Marker, MaxIdChars)
					&& !TooLong(value.Blueprint, MaxNameChars)
					&& !TooLong(value.SettlementId, MaxIdChars)
					&& !TooLong(value.OwnerId, MaxIdChars)
					&& !TooLong(value.ZoneId, MaxNameChars)
					&& !TooLong(value.Composition, MaxTextChars)
					&& Enum.IsDefined(typeof(KingdomLifecycleTopology), value.Topology)
					&& value.X >= -1 && value.X <= MaxCoordinate
					&& value.Y >= -1 && value.Y <= MaxCoordinate
					&& value.Count >= 0 && value.Count <= MaxPhysicalCount
					&& value.Capacity >= 0 && value.Capacity <= MaxPhysicalCount
					&& value.Revision >= 0L;
			}

			private static bool ExactTopology(Snapshot x,
				KingdomLifecycleTopology topology, string ownerId, string zoneId, int px, int py)
			{
				return x != null && x.Topology == topology
					&& string.Equals(x.OwnerId, ownerId, StringComparison.Ordinal)
					&& string.Equals(x.ZoneId, zoneId, StringComparison.Ordinal)
					&& x.X == px && x.Y == py;
			}

			private static bool ExactScheduleFields(Snapshot x,
				KingdomCarryOperation operation, long value, long revision, string lastOperationId)
			{
				return x != null && string.Equals(x.SettlementId,
					operation.DestinationSettlementId, StringComparison.Ordinal)
					&& string.Equals(x.Blueprint, ScheduleBlueprint, StringComparison.Ordinal)
					&& x.Value == value && x.Revision == revision
					&& string.Equals(x.LastOperationId, lastOperationId, StringComparison.Ordinal)
					&& ExactTopology(x, operation.DestinationTopology,
						operation.DestinationOwnerId, operation.DestinationZoneId,
						operation.DestinationX, operation.DestinationY);
			}

			private static bool ExactLifecycleScheduleFields(Snapshot x,
				KingdomLifecycleOperation operation, long value, long revision,
				string lastOperationId)
			{
				return x != null
					&& string.Equals(x.Blueprint, ScheduleBlueprint, StringComparison.Ordinal)
					&& string.Equals(x.SettlementId, operation.SettlementId,
						StringComparison.Ordinal)
					&& string.Equals(x.ZoneId, operation.ZoneId, StringComparison.Ordinal)
					&& x.Value == value && x.Revision == revision
					&& string.Equals(x.LastOperationId, lastOperationId, StringComparison.Ordinal)
					&& TopologyValid(x.Topology, x.OwnerId, x.ZoneId, x.X, x.Y);
			}

			private static bool ExactCarrySourceFields(Snapshot x,
				KingdomCarrySource source, int count)
			{
				return x != null && source != null
					&& string.Equals(x.ObjectId, source.ObjectId, StringComparison.Ordinal)
					&& string.Equals(x.Blueprint, source.Blueprint, StringComparison.Ordinal)
					&& x.Count == count && ExactTopology(x, source.Topology, source.OwnerId,
						source.ZoneId, source.X, source.Y);
			}

			private static bool ExactLifecycleObjectFields(Snapshot x,
				KingdomLifecycleOperation operation, int count)
			{
				return x != null && operation != null
					&& string.Equals(x.ObjectId, operation.ObjectId, StringComparison.Ordinal)
					&& string.Equals(x.Blueprint, operation.Blueprint, StringComparison.Ordinal)
					&& x.Count == count && ExactTopology(x, operation.ObjectTopology,
						operation.ObjectOwnerId, operation.ZoneId,
						operation.ObjectX, operation.ObjectY);
			}

			private static bool ExactWaterFields(Snapshot x,
				KingdomLifecycleWaterLeg leg, long value)
			{
				return x != null && string.Equals(x.ObjectId, leg.OwnerId, StringComparison.Ordinal)
					&& string.Equals(x.Blueprint, leg.Blueprint, StringComparison.Ordinal)
					&& string.Equals(x.ZoneId, leg.ZoneId, StringComparison.Ordinal)
					&& x.Capacity == leg.Capacity && x.Value == value
					&& string.Equals(x.Composition, leg.Composition, StringComparison.Ordinal);
			}
		}
		#endif

		public static string OperationId(string SettlementId, KingdomLifecycleLane Lane,
			long Sequence)
		{
			return HashId("operation", delegate(BinaryWriter w)
			{
				CanonicalString(w, SettlementId);
				w.Write((byte)Lane);
				w.Write(Sequence);
			});
		}

		public static string CarryId(string RealmId, long Sequence)
		{
			return HashId("carry", delegate(BinaryWriter w)
			{
				CanonicalString(w, RealmId);
				w.Write(Sequence);
			});
		}

		public static string ChildId(string Parent, string Kind, int Ordinal)
		{
			return HashId("child", delegate(BinaryWriter w)
			{
				CanonicalString(w, Parent);
				CanonicalString(w, Kind);
				w.Write(Ordinal);
			});
		}

		public static string ResourceKey(KingdomLifecycleResourceKind Kind,
			string ScopeId, string SubjectId)
		{
			if (!KnownResourceKind(Kind) || !ValidRootId(ScopeId) || !ValidRootId(SubjectId))
				return null;
			return HashId("resource", delegate(BinaryWriter w)
			{
				w.Write((byte)Kind);
				CanonicalString(w, ScopeId);
				CanonicalString(w, SubjectId);
			});
		}

		public static string TopologyId(KingdomLifecycleTopology Topology, string OwnerId,
			string ZoneId, int X, int Y)
		{
			if (!TopologyValid(Topology, OwnerId, ZoneId, X, Y)) return null;
			return HashId("topology", delegate(BinaryWriter w)
			{
				w.Write((byte)Topology);
				CanonicalString(w, OwnerId);
				CanonicalString(w, ZoneId);
				w.Write(X);
				w.Write(Y);
			});
		}

		public static string ScheduleSubjectId(string SettlementId, KingdomLifecycleLane Lane)
		{
			if (!ValidRootId(SettlementId) || Lane == KingdomLifecycleLane.None
				|| !Enum.IsDefined(typeof(KingdomLifecycleLane), Lane)) return null;
			return HashId("schedule-subject", delegate(BinaryWriter w)
			{
				CanonicalString(w, SettlementId);
				w.Write((byte)Lane);
			});
		}

		public static bool ActionAllowedInLane(KingdomLifecycleAction Action,
			KingdomLifecycleLane Lane)
		{
			switch (Action)
			{
			case KingdomLifecycleAction.Passages:
				return Lane == KingdomLifecycleLane.PlainGuest;
			case KingdomLifecycleAction.Spawn:
			case KingdomLifecycleAction.Depart:
			case KingdomLifecycleAction.OfferWater:
				return Lane == KingdomLifecycleLane.PlainGuest
					|| Lane == KingdomLifecycleLane.NotableGuest;
			case KingdomLifecycleAction.Lodge:
				return Lane == KingdomLifecycleLane.NotableGuest;
			case KingdomLifecycleAction.RaidWarning:
			case KingdomLifecycleAction.RaidRewarning:
			case KingdomLifecycleAction.RaidTribute:
			case KingdomLifecycleAction.RaidTalkDown:
			case KingdomLifecycleAction.RaidAttack:
			case KingdomLifecycleAction.RaidCancel:
				return Lane == KingdomLifecycleLane.Raid;
			case KingdomLifecycleAction.PetitionOffer:
			case KingdomLifecycleAction.PetitionAccept:
			case KingdomLifecycleAction.PetitionDecline:
			case KingdomLifecycleAction.PetitionResolve:
			case KingdomLifecycleAction.PetitionExpire:
				return Lane == KingdomLifecycleLane.Petition;
			default:
				return false;
			}
		}

		public static KingdomLifecycleOperation PrepareOperation(KingdomLifecycleBook Book,
			KingdomLifecycleLane Lane, KingdomLifecycleAction Action, long Tick)
		{
			if (!CanOwnAuthority(Book) || Tick < 0L || !ActionAllowedInLane(Action, Lane)
				|| GetSlot(Book, Lane) != null) return null;
			long next = GetNextSequence(Book, Lane);
			if (next <= GetRetiredThrough(Book, Lane) || next == long.MaxValue) return null;
			return new KingdomLifecycleOperation
			{
				Sequence = next,
				Id = OperationId(Book.SettlementId, Lane, next),
				Lane = Lane,
				Action = Action,
				Phase = KingdomLifecyclePhase.Prepared,
				CreatedTick = Tick,
				UpdatedTick = Tick,
				SettlementId = Book.SettlementId,
				WaterState = KingdomLifecyclePhysicalState.Skipped,
				RemovalState = KingdomLifecyclePhysicalState.Skipped,
				EffectState = KingdomLifecyclePhysicalState.Skipped
			};
		}

		public static KingdomLifecycleResourceLease PrepareLease(KingdomLifecycleBook Book,
			KingdomLifecycleOperation Operation, KingdomLifecycleResourceKind Kind,
			string ScopeId, string SubjectId, long Before, long Delta)
		{
			return IsDomainResourceKind(Kind)
				? PrepareLeaseCore(Book, Operation, Kind, ScopeId, SubjectId, Before, Delta)
				: null;
		}

		private static KingdomLifecycleResourceLease PrepareLeaseCore(KingdomLifecycleBook Book,
			KingdomLifecycleOperation Operation, KingdomLifecycleResourceKind Kind,
			string ScopeId, string SubjectId, long Before, long Delta)
		{
			if (!CanOwnAuthority(Book) || Operation == null || Delta == 0L) return null;
			long after;
			if (!CheckedAdd(Before, Delta, out after)) return null;
			string key = ResourceKey(Kind, ScopeId, SubjectId);
			if (key == null) return null;
			KingdomLifecycleResourceRevision row = FindResource(Book, key);
			if (row != null && (!string.IsNullOrEmpty(row.ActiveOperationId)
				|| row.Kind != Kind || row.ScopeId != ScopeId || row.SubjectId != SubjectId)) return null;
			long revision = row == null ? 0L : row.Revision;
			if (revision < 0L || revision == long.MaxValue) return null;
			return new KingdomLifecycleResourceLease
			{
				OperationId = Operation.Id,
				Kind = Kind,
				ScopeId = ScopeId,
				SubjectId = SubjectId,
				Key = key,
				Before = Before,
				Delta = Delta,
				After = after,
				BeforeRevision = revision,
				AfterRevision = revision + 1L,
				State = KingdomLifecycleLeaseState.Prepared
			};
		}

		public static KingdomLifecycleOutbox PrepareOutbox(KingdomLifecycleOperation Operation,
			string Chronicle, string Ledger, string Message, string Deed, string Guestbook)
		{
			if (Operation == null || !CanonicalOperationId(Operation)) return null;
			return new KingdomLifecycleOutbox
			{
				OperationId = Operation.Id,
				EventId = ChildId(Operation.Id, "outbox", 0),
				ChronicleReceiptId = ChildId(Operation.Id, "chronicle", 0),
				Chronicle = Chronicle,
				ChronicleDisposition = InitialDisposition(Chronicle),
				ChronicleState = InitialSink(Chronicle),
				Ledger = Ledger,
				LedgerDisposition = InitialDisposition(Ledger),
				LedgerState = InitialSink(Ledger),
				Message = Message,
				MessageDisposition = InitialDisposition(Message),
				MessageState = InitialSink(Message),
				Deed = Deed,
				DeedDisposition = InitialDisposition(Deed),
				DeedState = InitialSink(Deed),
				GuestbookLine = Guestbook,
				GuestbookDisposition = InitialDisposition(Guestbook),
				GuestbookState = InitialSink(Guestbook)
			};
		}

		public static bool TryPublish(KingdomLifecycleBook Book,
			KingdomLifecycleOperation Operation)
		{
			if (!CanOwnAuthority(Book) || Operation == null
				|| GetSlot(Book, Operation.Lane) != null
				|| !string.Equals(Operation.SettlementId, Book.SettlementId,
					StringComparison.Ordinal)
				|| Operation.Sequence != GetNextSequence(Book, Operation.Lane)
				|| !IsExactSuccessor(Operation.Sequence,
					GetRetiredThrough(Book, Operation.Lane))
				|| Operation.Sequence == long.MaxValue
				|| !CanonicalOperationId(Operation)
				|| Operation.Phase != KingdomLifecyclePhase.Prepared
				|| !PublicationPlanValid(Operation)) return false;

			string expectedHash;
			if (!TryPlanHash(Operation, out expectedHash)) return false;
			if (!string.IsNullOrEmpty(Operation.PlanHash)
				&& !string.Equals(Operation.PlanHash, expectedHash, StringComparison.Ordinal)) return false;

			List<KingdomLifecycleResourceRevision> rows = new List<KingdomLifecycleResourceRevision>();
			int newRows = 0;
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Operation.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = Operation.ResourceLeases[i];
				if (!LeaseShape(lease, Operation.Id, true) || !keys.Add(lease.Key)) return false;
				KingdomLifecycleResourceRevision row = FindResource(Book, lease.Key);
				if (row == null)
				{
					row = new KingdomLifecycleResourceRevision
					{
						Kind = lease.Kind, ScopeId = lease.ScopeId, SubjectId = lease.SubjectId,
						Key = lease.Key, Revision = 0L
					};
					newRows++;
				}
				if (!ResourceMatches(row, lease) || row.Revision != lease.BeforeRevision
					|| !string.IsNullOrEmpty(row.ActiveOperationId)
					|| string.Equals(row.LastOperationId, Operation.Id,
						StringComparison.Ordinal)) return false;
				rows.Add(row);
			}
			if (Book.Resources.Count + newRows > MaxResourceRows) return false;

			Operation.PlanHash = expectedHash;
			for (int i = 0; i < rows.Count; i++)
			{
				if (FindResource(Book, rows[i].Key) == null) Book.Resources.Add(rows[i]);
				rows[i].ActiveOperationId = Operation.Id;
			}
			SetNextSequence(Book, Operation.Lane, Operation.Sequence + 1L);
			SetSlot(Book, Operation.Lane, Operation);
			return true;
		}

		public static KingdomLifecycleCasAction LeaseAction(KingdomLifecycleBook Book,
			KingdomLifecycleResourceLease Lease, long CurrentValue)
		{
			return Lease != null && IsDomainResourceKind(Lease.Kind)
				? LeaseActionCore(Book, Lease, CurrentValue)
				: KingdomLifecycleCasAction.Quarantine;
		}

		private static KingdomLifecycleCasAction LeaseActionCore(KingdomLifecycleBook Book,
			KingdomLifecycleResourceLease Lease, long CurrentValue)
		{
			KingdomLifecycleOperation operation;
			KingdomLifecycleResourceRevision row;
			if (!ExactLeaseAuthority(Book, Lease, out operation, out row)
				|| !LeasePhaseAllows(operation, Lease)) return KingdomLifecycleCasAction.Quarantine;
			return LeaseSnapshotAction(CurrentValue, row.Revision, row.LastOperationId,
				row.ActiveOperationId, Lease);
		}

		private static KingdomLifecycleCasAction LeaseSnapshotAction(long CurrentValue,
			long CurrentRevision, string LastOperationId, string ActiveOperationId,
			KingdomLifecycleResourceLease Lease)
		{
			if (!LeaseShape(Lease, Lease == null ? null : Lease.OperationId, false)
				|| !string.Equals(ActiveOperationId, Lease.OperationId, StringComparison.Ordinal))
				return KingdomLifecycleCasAction.Quarantine;
			if (Lease.State == KingdomLifecycleLeaseState.Prepared)
			{
				return CurrentValue == Lease.Before && CurrentRevision == Lease.BeforeRevision
					&& !string.Equals(LastOperationId, Lease.OperationId, StringComparison.Ordinal)
					? KingdomLifecycleCasAction.Apply : KingdomLifecycleCasAction.Quarantine;
			}
			if (Lease.State == KingdomLifecycleLeaseState.Intent)
			{
				return CurrentValue == Lease.After && CurrentRevision == Lease.AfterRevision
					&& string.Equals(LastOperationId, Lease.OperationId, StringComparison.Ordinal)
					? KingdomLifecycleCasAction.Confirm : KingdomLifecycleCasAction.Quarantine;
			}
			if (Lease.State == KingdomLifecycleLeaseState.Proved)
			{
				return CurrentValue == Lease.After && CurrentRevision == Lease.AfterRevision
					&& string.Equals(LastOperationId, Lease.OperationId, StringComparison.Ordinal)
					? KingdomLifecycleCasAction.Confirm : KingdomLifecycleCasAction.Quarantine;
			}
			return KingdomLifecycleCasAction.Quarantine;
		}

		public static bool BeginLease(KingdomLifecycleBook Book,
			KingdomLifecycleResourceLease Lease, long CurrentValue)
		{
			return Lease != null && IsDomainResourceKind(Lease.Kind)
				&& BeginLeaseCore(Book, Lease, CurrentValue);
		}

		private static bool BeginLeaseCore(KingdomLifecycleBook Book,
			KingdomLifecycleResourceLease Lease, long CurrentValue)
		{
			KingdomLifecycleOperation operation;
			KingdomLifecycleResourceRevision row;
			if (Lease == null || !ExactLeaseAuthority(Book, Lease, out operation, out row)
				|| !LeasePhaseAllows(operation, Lease)
				|| LeaseActionCore(Book, Lease, CurrentValue)
					!= KingdomLifecycleCasAction.Apply) return false;
			Lease.State = KingdomLifecycleLeaseState.Intent;
			return true;
		}

		/// <summary>Called only in the same live stack after the scalar mutation returned.</summary>
		public static bool CommitLeaseWitness(KingdomLifecycleBook Book,
			KingdomLifecycleResourceLease Lease, long CurrentValue)
		{
			KingdomLifecycleOperation operation;
			KingdomLifecycleResourceRevision row;
			if (Lease == null || !IsDomainResourceKind(Lease.Kind)
				|| !ExactLeaseAuthority(Book, Lease, out operation, out row)) return false;
			return CommitLeaseWitnessCore(Book, operation, Lease, row, CurrentValue);
		}

		private static bool CommitLeaseWitnessCore(KingdomLifecycleBook Book,
			KingdomLifecycleOperation operation, KingdomLifecycleResourceLease Lease,
			KingdomLifecycleResourceRevision row, long CurrentValue)
		{
			if (!ExactOperationAuthority(Book, operation)
				|| !LeasePhaseAllows(operation, Lease)
				|| Lease.State != KingdomLifecycleLeaseState.Intent
				|| CurrentValue != Lease.After || row.Revision != Lease.BeforeRevision
				|| Lease.AfterRevision != Lease.BeforeRevision + 1L
				|| !string.Equals(row.ActiveOperationId, Lease.OperationId, StringComparison.Ordinal)
				|| string.Equals(row.LastOperationId, Lease.OperationId, StringComparison.Ordinal))
				return false;
			row.Revision = Lease.AfterRevision;
			row.LastOperationId = Lease.OperationId;
			Lease.State = KingdomLifecycleLeaseState.Proved;
			if (operation.Action == KingdomLifecycleAction.Depart
				&& IsRequiredDomainLease(operation, Lease)) operation.DepartedCount = operation.Count;
			return true;
		}

		private static bool BeginWaterLeaseCore(KingdomLifecycleBook Book,
			KingdomLifecycleResourceLease Lease, KingdomLifecycleWaterLeg Leg,
			long CurrentValue)
		{
			KingdomLifecycleOperation operation;
			KingdomLifecycleResourceRevision row;
			if (!ExactLeaseAuthority(Book, Lease, out operation, out row)
				|| Leg == null || Leg.State != KingdomLifecyclePhysicalState.Prepared
				|| Leg.ReceiptState != KingdomLifecyclePhysicalState.Prepared
				|| !ReferenceEquals(FindWaterLeg(operation, Lease.Key), Leg)
				|| LeaseActionCore(Book, Lease, CurrentValue) != KingdomLifecycleCasAction.Apply)
				return false;
			Lease.State = KingdomLifecycleLeaseState.Intent;
			Leg.State = KingdomLifecyclePhysicalState.Intent;
			Leg.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			return true;
		}

		private static bool ConfirmWaterLeaseCore(KingdomLifecycleBook Book,
			KingdomLifecycleResourceLease Lease, KingdomLifecycleWaterLeg Leg,
			long CurrentValue)
		{
			KingdomLifecycleOperation operation;
			KingdomLifecycleResourceRevision row;
			int proved;
			int outstanding;
			if (!ExactLeaseAuthority(Book, Lease, out operation, out row)
				|| Leg == null || Leg.State != KingdomLifecyclePhysicalState.Intent
				|| Leg.ReceiptState != KingdomLifecyclePhysicalState.Intent
				|| !ReferenceEquals(FindWaterLeg(operation, Lease.Key), Leg)
				|| !CheckedAdd(operation.WaterProved, Leg.Delta, out proved)
				|| !CheckedAdd(operation.WaterOutstanding, -Leg.Delta, out outstanding)
				|| !ValidCount(proved) || !ValidCount(outstanding)
				|| !CommitLeaseWitnessCore(Book, operation, Lease, row, CurrentValue)) return false;
			Leg.State = KingdomLifecyclePhysicalState.Proved;
			operation.WaterProved = proved;
			operation.WaterOutstanding = outstanding;
			operation.WaterState = AllWaterLegsProved(operation)
				? KingdomLifecyclePhysicalState.Proved : KingdomLifecyclePhysicalState.Intent;
			return true;
		}

		public static KingdomLifecycleMutationAction MutationAction(
			KingdomLifecyclePhysicalState State, bool ExactBefore, bool ExactAfter)
		{
			switch (State)
			{
			case KingdomLifecyclePhysicalState.Prepared:
				return ExactBefore && !ExactAfter
					? KingdomLifecycleMutationAction.InvokeOnce
					: KingdomLifecycleMutationAction.Quarantine;
			case KingdomLifecyclePhysicalState.Intent:
				return ExactAfter && !ExactBefore
					? KingdomLifecycleMutationAction.ConfirmAfter
					: KingdomLifecycleMutationAction.Quarantine;
			case KingdomLifecyclePhysicalState.Proved:
			case KingdomLifecyclePhysicalState.Skipped:
				return KingdomLifecycleMutationAction.Settled;
			default:
				return KingdomLifecycleMutationAction.Quarantine;
			}
		}

		public static bool CanTransition(KingdomLifecycleAction Action,
			KingdomLifecyclePhase From, KingdomLifecyclePhase To)
		{
			if (To == KingdomLifecyclePhase.Quarantined)
				return PhaseAllowed(Action, From) && From != KingdomLifecyclePhase.Terminal
					&& From != KingdomLifecyclePhase.Quarantined;
			KingdomLifecyclePhase next;
			return TryNextPhase(Action, From, out next) && next == To;
		}

		public static bool PhaseAllowed(KingdomLifecycleAction Action,
			KingdomLifecyclePhase Phase)
		{
			if (Phase == KingdomLifecyclePhase.Quarantined)
				return KnownAction(Action);
			KingdomLifecyclePhase current = KingdomLifecyclePhase.Prepared;
			if (!KnownAction(Action)) return false;
			for (int i = 0; i < 16; i++)
			{
				if (current == Phase) return true;
				KingdomLifecyclePhase next;
				if (!TryNextPhase(Action, current, out next)) return false;
				current = next;
			}
			return false;
		}

		public static bool AdvancePhase(KingdomLifecycleBook Book,
			KingdomLifecycleOperation Operation, KingdomLifecyclePhase To, long Tick)
		{
			if (!ExactOperationAuthority(Book, Operation) || Tick < Operation.UpdatedTick
				|| !CanTransition(Operation.Action, Operation.Phase, To)
				|| !TransitionReady(Book, Operation, To)) return false;
			if (To == KingdomLifecyclePhase.Terminal
				&& !TerminalComponentsSettled(Book, Operation)) return false;
			Operation.Phase = To;
			Operation.UpdatedTick = Tick;
			return true;
		}

		public static bool Quarantine(KingdomLifecycleOperation Operation, string Fault)
		{
			if (Operation == null || Operation.Phase == KingdomLifecyclePhase.Quarantined) return false;
			Operation.Phase = KingdomLifecyclePhase.Quarantined;
			Operation.Fault = SafeFault(Fault);
			return true;
		}

		public static bool Retire(KingdomLifecycleBook Book,
			KingdomLifecycleOperation Operation, long Tick)
		{
			if (!ExactOperationAuthority(Book, Operation) || Tick < Operation.UpdatedTick
				|| Operation.Phase != KingdomLifecyclePhase.Terminal
				|| !IsExactSuccessor(Operation.Sequence,
					GetRetiredThrough(Book, Operation.Lane))
				|| !TerminalComponentsSettled(Book, Operation)
				|| !ProofListValid(Book)) return false;
			for (int i = 0; i < Operation.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = Operation.ResourceLeases[i];
				KingdomLifecycleResourceRevision row = FindResource(Book, lease.Key);
				if (row == null || lease.State != KingdomLifecycleLeaseState.Proved
					|| row.Revision != lease.AfterRevision
					|| !string.Equals(row.LastOperationId, Operation.Id, StringComparison.Ordinal)
					|| !string.Equals(row.ActiveOperationId, Operation.Id, StringComparison.Ordinal))
					return false;
			}
			for (int i = 0; i < Operation.ResourceLeases.Count; i++)
				FindResource(Book, Operation.ResourceLeases[i].Key).ActiveOperationId = null;
			Operation.UpdatedTick = Tick;
			SetRetiredThrough(Book, Operation.Lane, Operation.Sequence);
			AppendProof(Book.RecentProofs, new KingdomLifecycleProof
			{
				Sequence = Operation.Sequence,
				Id = Operation.Id,
				PlanHash = Operation.PlanHash,
				Lane = Operation.Lane,
				Action = Operation.Action,
				Tick = Tick
			});
			SetSlot(Book, Operation.Lane, null);
			return true;
		}

		public static bool SinkSettled(KingdomLifecycleSinkState State)
		{
			return State == KingdomLifecycleSinkState.Delivered
				|| State == KingdomLifecycleSinkState.Skipped
				|| State == KingdomLifecycleSinkState.Lost;
		}

		public static KingdomLifecycleSinkState ResumeSink(
			KingdomLifecycleSinkState State, bool ChronicleRecordOnce)
		{
			if (State != KingdomLifecycleSinkState.Intent) return State;
			return ChronicleRecordOnce ? KingdomLifecycleSinkState.Pending
				: KingdomLifecycleSinkState.Lost;
		}

		public static bool RecoverOutbox(KingdomLifecycleBook Book,
			KingdomLifecycleOperation Operation)
		{
			if (!ExactOperationAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.Sinks
				|| Operation.Outbox == null) return false;
			KingdomLifecycleOutbox Outbox = Operation.Outbox;
			Outbox.ChronicleState = ResumeSink(Outbox.ChronicleState, true);
			Outbox.LedgerState = ResumeSink(Outbox.LedgerState, false);
			Outbox.MessageState = ResumeSink(Outbox.MessageState, false);
			Outbox.DeedState = ResumeSink(Outbox.DeedState, false);
			Outbox.GuestbookState = ResumeSink(Outbox.GuestbookState, false);
			return ExactOperationAuthority(Book, Operation);
		}

		public static bool RecoverCarryOutbox(KingdomCarryBook Book,
			KingdomCarryOperation Operation)
		{
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.Sinks
				|| Operation.Outbox == null) return false;
			KingdomLifecycleOutbox Outbox = Operation.Outbox;
			Outbox.ChronicleState = ResumeSink(Outbox.ChronicleState, true);
			Outbox.LedgerState = ResumeSink(Outbox.LedgerState, false);
			Outbox.MessageState = ResumeSink(Outbox.MessageState, false);
			Outbox.DeedState = ResumeSink(Outbox.DeedState, false);
			Outbox.GuestbookState = ResumeSink(Outbox.GuestbookState, false);
			return ExactCarryAuthority(Book, Operation);
		}

		public static KingdomLifecycleOptionDecision ObserveOption(
			KingdomLifecycleOptionState Prior, long PriorTick, bool Enabled,
			long Now, bool HasOpenOperation)
		{
			KingdomLifecycleOptionDecision result = new KingdomLifecycleOptionDecision
			{
				Valid = false,
				Action = KingdomLifecycleOptionAction.Quarantine,
				State = Prior,
				Tick = PriorTick,
				AllowNewWork = false,
				ReconcileOpenWork = HasOpenOperation
			};
			if (!KnownOption(Prior) || PriorTick < 0L || Now < PriorTick) return result;
			result.Valid = true;
			if (!Enabled)
			{
				result.Action = Prior == KingdomLifecycleOptionState.Disabled
					? KingdomLifecycleOptionAction.StayDisabled : KingdomLifecycleOptionAction.Disable;
				result.State = KingdomLifecycleOptionState.Disabled;
				result.Tick = Prior == KingdomLifecycleOptionState.Disabled ? PriorTick : Now;
				return result;
			}
			if (Prior != KingdomLifecycleOptionState.Enabled)
			{
				result.Action = KingdomLifecycleOptionAction.EnableAndRestamp;
				result.State = KingdomLifecycleOptionState.Enabled;
				result.Tick = Now;
				return result;
			}
			result.Action = KingdomLifecycleOptionAction.None;
			result.AllowNewWork = !HasOpenOperation;
			return result;
		}

		public static bool CanStartAfterOption(KingdomLifecycleOptionDecision Decision,
			long Now, long MinimumElapsed)
		{
			if (Decision == null || !Decision.Valid || !Decision.AllowNewWork
				|| Decision.State != KingdomLifecycleOptionState.Enabled
				|| MinimumElapsed < 0L || Now < Decision.Tick) return false;
			long due;
			return CheckedAdd(Decision.Tick, MinimumElapsed, out due) && Now >= due;
		}

		public static KingdomCarryOperation PrepareCarry(KingdomCarryBook Book, long Tick)
		{
			if (!CanOwnAuthority(Book) || Tick < 0L || Book.Open != null
				|| !FrozenSettlementSetValid(Book.SettlementIds)
				|| Book.NextSequence <= Book.RetiredThrough || Book.NextSequence == long.MaxValue)
				return null;
			return new KingdomCarryOperation
			{
				Sequence = Book.NextSequence,
				Id = CarryId(Book.RealmId, Book.NextSequence),
					Phase = KingdomLifecyclePhase.Prepared,
					CreatedTick = Tick,
					UpdatedTick = Tick,
					SettlementIds = new List<string>(Book.SettlementIds),
					RealmTopologyHash = RealmTopologyDigest(Book.RealmId, Book.SettlementIds),
					RiskFrozen = true
			};
		}

		private static KingdomLifecycleResourceLease PrepareCarryScheduleLeaseCore(
			KingdomCarryBook Book, KingdomCarryOperation Operation, long Before)
		{
			if (!CanOwnAuthority(Book) || Operation == null
				|| Operation.Phase != KingdomLifecyclePhase.Prepared
				|| !string.Equals(Operation.Id, CarryId(Book.RealmId, Operation.Sequence),
					StringComparison.Ordinal)
				|| !ExactStringList(Operation.SettlementIds, Book.SettlementIds)
				|| !string.Equals(Operation.RealmTopologyHash,
					RealmTopologyDigest(Book.RealmId, Book.SettlementIds), StringComparison.Ordinal)
				|| !SettlementMember(Book, Operation.OriginSettlementId)
				|| !SettlementMember(Book, Operation.DestinationSettlementId)
				|| Operation.DueTick < 0L || Before < 0L) return null;
			long delta;
			if (!CheckedAdd(Operation.DueTick, -Before, out delta) || delta == 0L) return null;
			string key = ResourceKey(KingdomLifecycleResourceKind.Schedule,
				Book.RealmId, Operation.DestinationSettlementId);
			KingdomLifecycleResourceRevision row = FindResource(Book, key);
			if (key == null || (row != null && !string.IsNullOrEmpty(row.ActiveOperationId))) return null;
			long revision = row == null ? 0L : row.Revision;
			if (revision < 0L || revision == long.MaxValue) return null;
			return new KingdomLifecycleResourceLease
			{
				OperationId = Operation.Id,
				Kind = KingdomLifecycleResourceKind.Schedule,
				ScopeId = Book.RealmId,
				SubjectId = Operation.DestinationSettlementId,
				Key = key,
				Before = Before,
				Delta = delta,
				After = Operation.DueTick,
				BeforeRevision = revision,
				AfterRevision = revision + 1L,
				State = KingdomLifecycleLeaseState.Prepared
			};
		}

		public static KingdomCarrySource PrepareCarrySource(KingdomCarryOperation Operation,
			int SourceOrdinal, string ObjectId, string Blueprint,
			KingdomLifecycleTopology Topology, string OwnerId, string ZoneId,
			int X, int Y, int Material, int OriginalCount, int PlannedCount)
		{
			if (Operation == null || SourceOrdinal < 0 || SourceOrdinal >= MaxCarrySources
				|| !ValidRootId(ObjectId) || !ValidName(Blueprint)
				|| !TopologyValid(Topology, OwnerId, ZoneId, X, Y)
				|| Material < 0 || Material >= 6 || OriginalCount <= 0
				|| OriginalCount > MaxPhysicalCount || PlannedCount <= 0
				|| PlannedCount > OriginalCount) return null;
			return new KingdomCarrySource
			{
				OperationId = Operation.Id,
				SourceEventId = ChildId(Operation.Id, "source", SourceOrdinal),
				ObjectId = ObjectId,
				Blueprint = Blueprint,
				Topology = Topology,
				OwnerId = OwnerId,
				ZoneId = ZoneId,
				X = X,
				Y = Y,
				Material = Material,
				OriginalCount = OriginalCount,
				PlannedCount = PlannedCount,
				Removed = 0,
				UnitCursor = 0,
				UnitBefore = OriginalCount,
					UnitAfter = OriginalCount - 1,
					UnitEventId = ChildId(Operation.Id, "source-unit-" + SourceOrdinal, 0),
					UnitState = KingdomLifecyclePhysicalState.Prepared,
					ReceiptId = ChildId(Operation.Id, "source-receipt-" + SourceOrdinal, 0),
					ReceiptTopologyId = TopologyId(Topology, OwnerId, ZoneId, X, Y),
					ReceiptState = KingdomLifecyclePhysicalState.Prepared,
					State = KingdomLifecyclePhysicalState.Prepared
			};
		}

		public static KingdomLifecycleProjection PrepareCarryOutput(
			KingdomCarryOperation Operation, int OutputOrdinal, string ObjectId,
			string Blueprint, KingdomLifecycleTopology Topology, string OwnerId,
			string ZoneId, int X, int Y, int Material, int Count)
		{
			if (Operation == null || OutputOrdinal < 0 || OutputOrdinal >= MaxCarryOutputs
				|| !ValidRootId(ObjectId) || !ValidName(Blueprint)
				|| !TopologyValid(Topology, OwnerId, ZoneId, X, Y)
				|| Material < 0 || Material >= 6 || Count <= 0 || Count > MaxPhysicalCount)
				return null;
			return new KingdomLifecycleProjection
			{
				OperationId = Operation.Id,
				EventId = ChildId(Operation.Id, "projection", OutputOrdinal),
				ObjectId = ObjectId,
				Marker = ChildId(Operation.Id, "marker", OutputOrdinal),
				Blueprint = Blueprint,
				Topology = Topology,
				OwnerId = OwnerId,
				ZoneId = ZoneId,
				X = X,
				Y = Y,
				Material = Material,
				Count = Count,
				NoStack = true,
				State = KingdomLifecyclePhysicalState.Prepared,
				ReceiptId = ChildId(Operation.Id, "output-receipt", OutputOrdinal),
				ReceiptTopologyId = TopologyId(Topology, OwnerId, ZoneId, X, Y),
				ReceiptState = KingdomLifecyclePhysicalState.Prepared
			};
		}

		public static bool TryPublishCarry(KingdomCarryBook Book,
			KingdomCarryOperation Operation)
		{
			if (!CanOwnAuthority(Book) || Operation == null || Book.Open != null
				|| Operation.Sequence != Book.NextSequence
				|| !IsExactSuccessor(Operation.Sequence, Book.RetiredThrough)
				|| Operation.Sequence == long.MaxValue
				|| !string.Equals(Operation.Id, CarryId(Book.RealmId, Operation.Sequence),
					StringComparison.Ordinal)
				|| !ExactStringList(Operation.SettlementIds, Book.SettlementIds)
				|| !string.Equals(Operation.RealmTopologyHash,
					RealmTopologyDigest(Book.RealmId, Book.SettlementIds), StringComparison.Ordinal)
				|| !SettlementMember(Book, Operation.OriginSettlementId)
				|| !SettlementMember(Book, Operation.DestinationSettlementId)
				|| Operation.Phase != KingdomLifecyclePhase.Prepared
				|| !CarryPublicationPlanValid(Operation)) return false;
			KingdomLifecycleResourceLease lease = Operation.ScheduleLease;
			if (!CarryScheduleLeaseShape(Book, Operation, lease, true)) return false;
			KingdomLifecycleResourceRevision row = FindResource(Book, lease.Key);
			bool addRow = row == null;
			if (addRow)
			{
				if (Book.Resources.Count >= MaxResourceRows) return false;
				row = new KingdomLifecycleResourceRevision
				{
					Kind = lease.Kind, ScopeId = lease.ScopeId, SubjectId = lease.SubjectId,
					Key = lease.Key, Revision = 0L
				};
			}
			if (!ResourceMatches(row, lease) || row.Revision != lease.BeforeRevision
				|| !string.IsNullOrEmpty(row.ActiveOperationId)
				|| string.Equals(row.LastOperationId, Operation.Id, StringComparison.Ordinal)) return false;
			string hash;
			if (!TryCarryPlanHash(Operation, out hash)) return false;
			if (!string.IsNullOrEmpty(Operation.PlanHash)
				&& !string.Equals(Operation.PlanHash, hash, StringComparison.Ordinal)) return false;
			Operation.PlanHash = hash;
			if (addRow) Book.Resources.Add(row);
			row.ActiveOperationId = Operation.Id;
			Book.NextSequence = Operation.Sequence + 1L;
			Book.Open = Operation;
			return true;
		}

		private static bool BeginCarryScheduleCore(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomLifecycleResourceLease Lease,
			long CurrentValue)
		{
			KingdomLifecycleResourceRevision row;
			if (!ExactCarryScheduleAuthority(Book, Operation, Lease, out row)
				|| Operation.Phase != KingdomLifecyclePhase.ScheduleIntent
				|| Lease.State != KingdomLifecycleLeaseState.Prepared
				|| CurrentValue != Lease.Before || row.Revision != Lease.BeforeRevision
				|| string.Equals(row.LastOperationId, Operation.Id, StringComparison.Ordinal)) return false;
			Lease.State = KingdomLifecycleLeaseState.Intent;
			return true;
		}

		private static bool CommitCarryScheduleCore(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomLifecycleResourceLease Lease,
			long CurrentValue)
		{
			KingdomLifecycleResourceRevision row;
			if (!ExactCarryScheduleAuthority(Book, Operation, Lease, out row)
				|| Operation.Phase != KingdomLifecyclePhase.ScheduleIntent
				|| Lease.State != KingdomLifecycleLeaseState.Intent
				|| CurrentValue != Lease.After || row.Revision != Lease.BeforeRevision
				|| Lease.AfterRevision != Lease.BeforeRevision + 1L
				|| string.Equals(row.LastOperationId, Operation.Id, StringComparison.Ordinal)) return false;
			row.Revision = Lease.AfterRevision;
			row.LastOperationId = Operation.Id;
			Lease.State = KingdomLifecycleLeaseState.Proved;
			return true;
		}

		private static KingdomLifecycleMutationAction CarryUnitAction(KingdomCarrySource Source,
			int ObservedCount, bool SameIdentity, bool SameTopology)
		{
			if (Source == null || !SameIdentity || !SameTopology) return KingdomLifecycleMutationAction.Quarantine;
			bool before = ObservedCount == Source.UnitBefore;
			bool after = ObservedCount == Source.UnitAfter;
			return MutationAction(Source.UnitState, before, after);
		}

		private static bool BeginCarryUnitCore(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomCarrySource Source)
		{
			int sourceIndex = IndexOfSource(Operation, Source);
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.RemovalIntent
				|| Source == null || Source.OperationId != Operation.Id
				|| sourceIndex < 0 || sourceIndex != Operation.SourceIndex
				|| Source.State != KingdomLifecyclePhysicalState.Prepared
				|| Source.UnitState != KingdomLifecyclePhysicalState.Prepared
				|| Source.ReceiptState != KingdomLifecyclePhysicalState.Prepared
				|| !CarrySourceReceiptPrepared(Source, Operation, sourceIndex)) return false;
			Source.UnitState = KingdomLifecyclePhysicalState.Intent;
			Source.ReceiptBeforeIdMatches = 1;
			Source.ReceiptBeforeCount = Source.UnitBefore;
			Source.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			return true;
		}

		private static bool ConfirmCarryUnitCore(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomCarrySource Source)
		{
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.RemovalIntent
				|| Source == null || Source.OperationId != Operation.Id
				|| Source.UnitState != KingdomLifecyclePhysicalState.Intent
				|| Source.ReceiptState != KingdomLifecyclePhysicalState.Proved
				|| !CarryConserved(Operation)) return false;
			int sourceIndex = IndexOfSource(Operation, Source);
			if (sourceIndex < 0 || sourceIndex != Operation.SourceIndex
				|| !ExactCarrySourceReceipt(Operation, Source, sourceIndex)) return false;
			int nextRemoved;
			int nextEscrow;
			if (!CheckedAdd(Source.Removed, 1, out nextRemoved)
				|| nextRemoved > Source.PlannedCount
				|| !CheckedAdd(MaterialValue(Operation, Source.Material, 1), 1, out nextEscrow)
				|| !ValidCount(nextEscrow)) return false;
			string nextEvent = nextRemoved == Source.PlannedCount ? Source.UnitEventId
				: ChildId(Operation.Id, "source-unit-" + sourceIndex.ToString(
					CultureInfo.InvariantCulture), nextRemoved);
			if (!ValidGeneratedId(nextEvent)) return false;
			string chain = CarrySourceReceiptChain(Source.ReceiptChainId,
				Source.ReceiptProofId, nextRemoved);
			if (!ValidHashNamespace(chain, "carry-source-chain")) return false;
			SetMaterial(Operation, Source.Material, 1, nextEscrow);
			Source.Removed = nextRemoved;
			Source.UnitCursor = nextRemoved;
			Source.ReceiptChainId = chain;
			Source.ReceiptChainCount = nextRemoved;
			Source.UnitState = KingdomLifecyclePhysicalState.Proved;
			if (nextRemoved == Source.PlannedCount)
			{
				Source.State = KingdomLifecyclePhysicalState.Proved;
			}
			else
			{
				Source.UnitBefore = Source.OriginalCount - nextRemoved;
				Source.UnitAfter = Source.UnitBefore - 1;
				Source.UnitEventId = nextEvent;
				Source.UnitState = KingdomLifecyclePhysicalState.Prepared;
				ResetCarrySourceReceipt(Operation, Source, sourceIndex, nextRemoved);
			}
			Operation.SourceIndex = FirstIncompleteSource(Operation);
			return CarryConserved(Operation);
		}

		private static bool BeginCarryOutputCore(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomLifecycleProjection Output)
		{
			int index = IndexOfOutput(Operation, Output);
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.ProjectionIntent
				|| index < 0 || index != Operation.OutputIndex || Operation.LostOnRoad
				|| !CarryOutputShape(Output, Operation.Id, index, false)
				|| Output.State != KingdomLifecyclePhysicalState.Prepared
				|| Output.ReceiptState != KingdomLifecyclePhysicalState.Prepared
				|| Output.ReceiptBeforeIdMatches != -1
				|| Output.ReceiptBeforeMarkerMatches != -1
				|| Output.ReceiptBeforeCount != -1) return false;
			Output.ReceiptBeforeIdMatches = 0;
			Output.ReceiptBeforeMarkerMatches = 0;
			Output.ReceiptBeforeCount = 0;
			Output.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			Output.State = KingdomLifecyclePhysicalState.Intent;
			return true;
		}

		private static bool ConfirmCarryOutputCore(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomLifecycleProjection Output)
		{
			int index = IndexOfOutput(Operation, Output);
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.ProjectionIntent
				|| index < 0 || index != Operation.OutputIndex || Operation.LostOnRoad
				|| Output.State != KingdomLifecyclePhysicalState.Intent
				|| Output.ReceiptState != KingdomLifecyclePhysicalState.Intent
				|| Output.ReceiptBeforeIdMatches != 0
				|| Output.ReceiptBeforeMarkerMatches != 0 || Output.ReceiptBeforeCount != 0
				|| Output.ReceiptAfterIdMatches != -1 || Output.ReceiptAfterMarkerMatches != -1
				|| Output.ReceiptAfterCount != -1 || Output.ReceiptSameReference
				|| !string.IsNullOrEmpty(Output.ReceiptProofId)) return false;
			Output.State = KingdomLifecyclePhysicalState.Proved;
			return true;
		}

		private static bool SkipCarryOutputOnRoadCore(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomLifecycleProjection Output)
		{
			int index = IndexOfOutput(Operation, Output);
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.ProjectionIntent
				|| index < 0 || index != Operation.OutputIndex || !Operation.LostOnRoad
				|| !CarryOutputShape(Output, Operation.Id, index, false)
				|| Output.State != KingdomLifecyclePhysicalState.Prepared
				|| Output.ReceiptState != KingdomLifecyclePhysicalState.Prepared
				|| Output.ReceiptBeforeIdMatches != -1
				|| Output.ReceiptBeforeMarkerMatches != -1
				|| Output.ReceiptBeforeCount != -1) return false;
			Output.ReceiptBeforeIdMatches = 0;
			Output.ReceiptBeforeMarkerMatches = 0;
			Output.ReceiptBeforeCount = 0;
			Output.ReceiptAfterIdMatches = 0;
			Output.ReceiptAfterMarkerMatches = 0;
			Output.ReceiptAfterCount = 0;
			Output.ReceiptProofId = CarryOutputReceiptProof(Operation, Output, true);
			Output.ReceiptState = KingdomLifecyclePhysicalState.Skipped;
			Output.State = KingdomLifecyclePhysicalState.Skipped;
			return CarryOutputShape(Output, Operation.Id, index, false);
		}

		public static bool MoveCarryEscrow(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomLifecycleProjection Output, bool Lost)
		{
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.ProjectionIntent
				|| Output == null || Operation.OutputIndex < 0
				|| Operation.OutputIndex >= Operation.Outputs.Count
				|| !ReferenceEquals(Operation.Outputs[Operation.OutputIndex], Output)
				|| Lost != Operation.LostOnRoad
				|| !CarryOutputShape(Output, Operation.Id, Operation.OutputIndex, false)
				|| Output.State != (Lost ? KingdomLifecyclePhysicalState.Skipped
					: KingdomLifecyclePhysicalState.Proved)
				|| Output.ReceiptState != Output.State
				|| !ExactCarryOutputReceipt(Operation, Output, Lost)
				|| !CarryConserved(Operation)) return false;
			int Material = Output.Material;
			int Count = Output.Count;
			int escrow = MaterialValue(Operation, Material, 1);
			if (escrow < Count) return false;
			int delivered = MaterialValue(Operation, Material, 2);
			int roadLost = MaterialValue(Operation, Material, 3);
			if (!AddMaterial(Operation, Material, -Count, Lost ? 0 : Count, Lost ? Count : 0))
				return false;
			Operation.OutputIndex++;
			if (CarryConserved(Operation)) return true;
			Operation.OutputIndex--;
			SetMaterial(Operation, Material, 1, escrow);
			SetMaterial(Operation, Material, 2, delivered);
			SetMaterial(Operation, Material, 3, roadLost);
			return false;
		}

		public static bool CanTransitionCarry(KingdomLifecyclePhase From,
			KingdomLifecyclePhase To)
		{
			if (To == KingdomLifecyclePhase.Quarantined)
				return CarryPhaseAllowed(From) && From != KingdomLifecyclePhase.Terminal
					&& From != KingdomLifecyclePhase.Quarantined;
			switch (From)
			{
			case KingdomLifecyclePhase.Prepared: return To == KingdomLifecyclePhase.RemovalIntent;
			case KingdomLifecyclePhase.RemovalIntent: return To == KingdomLifecyclePhase.Removed;
			case KingdomLifecyclePhase.Removed: return To == KingdomLifecyclePhase.ScheduleIntent;
			case KingdomLifecyclePhase.ScheduleIntent: return To == KingdomLifecyclePhase.ProjectionIntent;
			case KingdomLifecyclePhase.ProjectionIntent: return To == KingdomLifecyclePhase.Projected;
			case KingdomLifecyclePhase.Projected: return To == KingdomLifecyclePhase.Sinks;
			case KingdomLifecyclePhase.Sinks: return To == KingdomLifecyclePhase.Terminal;
			default: return false;
			}
		}

		public static bool AdvanceCarryPhase(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomLifecyclePhase To, long Tick)
		{
			if (!ExactCarryAuthority(Book, Operation) || Tick < Operation.UpdatedTick
				|| !CanTransitionCarry(Operation.Phase, To)) return false;
			if (To == KingdomLifecyclePhase.Removed && !AllSourcesProved(Operation)) return false;
			if (To == KingdomLifecyclePhase.ProjectionIntent
				&& !CarryScheduleProved(Book, Operation)) return false;
			if (To == KingdomLifecyclePhase.Projected
				&& (!OutputsSettledForRoad(Operation) || CarryEscrow(Operation) != 0
					|| !CarryConserved(Operation))) return false;
			if (To == KingdomLifecyclePhase.Terminal && !CarryTerminalComponentsSettled(Operation))
				return false;
			Operation.Phase = To;
			Operation.UpdatedTick = Tick;
			return true;
		}

		public static bool Quarantine(KingdomCarryOperation Operation, string Fault)
		{
			if (Operation == null || Operation.Phase == KingdomLifecyclePhase.Quarantined) return false;
			Operation.Phase = KingdomLifecyclePhase.Quarantined;
			Operation.Fault = SafeFault(Fault);
			return true;
		}

		public static bool RetireCarry(KingdomCarryBook Book,
			KingdomCarryOperation Operation, long Tick)
		{
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.Terminal
				|| !IsExactSuccessor(Operation.Sequence, Book.RetiredThrough)
				|| Tick < Operation.UpdatedTick
				|| !CarryTerminalComponentsSettled(Operation)
				|| !CarryScheduleProved(Book, Operation)
				|| !CarryProofListValid(Book)) return false;
			KingdomLifecycleResourceRevision schedule = FindResource(Book,
				Operation.ScheduleLease.Key);
			if (schedule == null || !string.Equals(schedule.ActiveOperationId,
				Operation.Id, StringComparison.Ordinal)) return false;
			schedule.ActiveOperationId = null;
			Operation.UpdatedTick = Tick;
			Book.RetiredThrough = Operation.Sequence;
			AppendProof(Book.RecentProofs, new KingdomLifecycleProof
			{
				Sequence = Operation.Sequence,
				Id = Operation.Id,
				PlanHash = Operation.PlanHash,
				Lane = KingdomLifecycleLane.None,
				Action = KingdomLifecycleAction.None,
				Tick = Tick
			});
			Book.Open = null;
			return true;
		}

		public static int CarryEscrow(KingdomCarryOperation Operation)
		{
			long value = Operation == null ? -1L : SumSix(Operation.EscrowMud,
				Operation.EscrowBrush, Operation.EscrowTimber, Operation.EscrowStone,
				Operation.EscrowMarble, Operation.EscrowScrap);
			return value < 0L || value > int.MaxValue ? -1 : (int)value;
		}

		public static bool CarryConserved(KingdomCarryOperation Operation)
		{
			if (Operation == null || !CarryCountsValid(Operation) || Operation.Sources == null) return false;
			long[] planned = new long[6];
			long[] removed = new long[6];
			for (int i = 0; i < Operation.Sources.Count; i++)
			{
				KingdomCarrySource source = Operation.Sources[i];
				if (!CarrySourceShape(source, Operation, i, false)) return false;
				if (!CheckedAccumulate(planned, source.Material, source.PlannedCount)
					|| !CheckedAccumulate(removed, source.Material, source.Removed)) return false;
				long physical = (long)source.OriginalCount - source.Removed;
				if (physical < 0L || (long)source.OriginalCount != physical + source.Removed) return false;
			}
			long[] provedOutput = new long[6];
			long[] skippedOutput = new long[6];
			if (Operation.Outputs == null || Operation.Outputs.Count > MaxCarryOutputs) return false;
			for (int i = 0; i < Operation.Outputs.Count; i++)
			{
				KingdomLifecycleProjection output = Operation.Outputs[i];
				if (!CarryOutputShape(output, Operation.Id, i, false)
					|| output.Material < 0 || output.Material >= 6) return false;
				if (output.State == KingdomLifecyclePhysicalState.Proved)
				{
					if (!CheckedAccumulate(provedOutput, output.Material, output.Count)) return false;
				}
				else if (output.State == KingdomLifecyclePhysicalState.Skipped)
				{
					if (!CheckedAccumulate(skippedOutput, output.Material, output.Count)) return false;
				}
			}
			for (int material = 0; material < 6; material++)
			{
				long frozen = MaterialValue(Operation, material, 0);
				long escrow = MaterialValue(Operation, material, 1);
				long delivered = MaterialValue(Operation, material, 2);
				long lost = MaterialValue(Operation, material, 3);
				if (planned[material] != frozen || removed[material] != escrow + delivered + lost
					|| delivered > provedOutput[material]
					|| (Operation.LostOnRoad ? lost > skippedOutput[material] : lost != 0L))
					return false;
			}
			return true;
		}

		public static bool WaterConserved(KingdomLifecycleOperation Operation, bool Terminal)
		{
			if (Operation == null || !ValidCount(Operation.WaterRequested)
				|| !ValidCount(Operation.WaterProved) || !ValidCount(Operation.WaterOutstanding)
				|| !ValidCount(Operation.WaterLost) || !ValidCount(Operation.WaterAmbiguous)
				|| Operation.WaterLegs == null || Operation.WaterLegs.Count > MaxWaterLegs) return false;
			long planned = 0L;
			long proved = 0L;
			for (int i = 0; i < Operation.WaterLegs.Count; i++)
			{
				KingdomLifecycleWaterLeg leg = Operation.WaterLegs[i];
				if (!WaterLegShape(leg, Operation, i, false)) return false;
				planned += leg.Delta;
				if (leg.State == KingdomLifecyclePhysicalState.Proved) proved += leg.Delta;
			}
				if (planned != Operation.WaterRequested || proved != Operation.WaterProved
					|| (long)Operation.WaterRequested != Operation.WaterProved
						+ Operation.WaterOutstanding + Operation.WaterLost
						+ Operation.WaterAmbiguous) return false;
			if (Terminal && (Operation.WaterOutstanding != 0 || Operation.WaterAmbiguous != 0
				|| Operation.WaterLost != 0 || Operation.WaterProved != Operation.WaterRequested))
				return false;
			return true;
		}

		private static bool LifecycleBookShape(KingdomLifecycleBook book)
		{
			return book != null
				&& (book.LegacyIdentity ? ValidRootId(book.LegacyMigrationKey)
					: string.IsNullOrEmpty(book.LegacyMigrationKey))
				&& !TooLong(book.Fault, MaxTextChars)
				&& KnownOption(book.LocusOption) && KnownOption(book.NotableOption)
				&& KnownOption(book.RaidOption) && KnownOption(book.PetitionOption)
				&& book.LocusOptionTick >= 0L && book.NotableOptionTick >= 0L
				&& book.RaidOptionTick >= 0L && book.PetitionOptionTick >= 0L
				&& ResourceRegistryValid(book) && ProofListValid(book)
				&& LaneAuthorityValid(book, KingdomLifecycleLane.PlainGuest, book.PlainGuest)
				&& LaneAuthorityValid(book, KingdomLifecycleLane.NotableGuest, book.NotableGuest)
				&& LaneAuthorityValid(book, KingdomLifecycleLane.Raid, book.Raid)
				&& LaneAuthorityValid(book, KingdomLifecycleLane.Petition, book.Petition)
				&& ActiveResourcesValid(book) && GrowthAttachmentValid(book);
		}

		private static bool CarryBookShape(KingdomCarryBook book)
		{
			if (book == null || (book.LegacyIdentity ? !ValidRootId(book.LegacyMigrationKey)
				: !string.IsNullOrEmpty(book.LegacyMigrationKey))
				|| TooLong(book.Fault, MaxTextChars) || !CarryProofListValid(book)
				|| !CarrySettlementSetShape(book) || !CarryResourceRegistryValid(book)
				|| !CarrySequenceValid(book)) return false;
			if (book.Open == null) return CarryActiveResourcesValid(book);
			KingdomCarryOperation op = book.Open;
			string hash;
			return string.Equals(op.Id, CarryId(book.RealmId, op.Sequence), StringComparison.Ordinal)
				&& ExactStringList(op.SettlementIds, book.SettlementIds)
				&& string.Equals(op.RealmTopologyHash,
					RealmTopologyDigest(book.RealmId, book.SettlementIds), StringComparison.Ordinal)
				&& CarryPhaseAllowed(op.Phase) && op.CreatedTick >= 0L
				&& op.UpdatedTick >= op.CreatedTick && !TooLong(op.Fault, MaxTextChars)
				&& CarryPlanShape(op, false) && TryCarryPlanHash(op, out hash)
				&& string.Equals(op.PlanHash, hash, StringComparison.Ordinal)
				&& SettlementMember(book, op.OriginSettlementId)
				&& SettlementMember(book, op.DestinationSettlementId)
				&& CarryConserved(op) && CarryPhaseProgressValid(op)
				&& CarryActiveResourcesValid(book);
		}

		private static bool LaneAuthorityValid(KingdomLifecycleBook book,
			KingdomLifecycleLane lane, KingdomLifecycleOperation op)
		{
			if (!LaneSequenceValid(book, lane, op)) return false;
			if (op == null) return true;
			string hash;
			return op.Lane == lane && ActionAllowedInLane(op.Action, lane)
				&& CanonicalOperationId(op)
				&& string.Equals(op.SettlementId, book.SettlementId, StringComparison.Ordinal)
				&& KnownPhase(op.Phase) && PhaseAllowed(op.Action, op.Phase)
				&& op.CreatedTick >= 0L && op.UpdatedTick >= op.CreatedTick
				&& !TooLong(op.Fault, MaxTextChars) && PlanShape(op, false)
				&& LifecyclePhaseProgressValid(op)
				&& TryPlanHash(op, out hash)
				&& string.Equals(op.PlanHash, hash, StringComparison.Ordinal);
		}

		private static bool LaneSequenceValid(KingdomLifecycleBook book,
			KingdomLifecycleLane lane, KingdomLifecycleOperation op)
		{
			if (book == null) return false;
			long next = GetNextSequence(book, lane);
			long retired = GetRetiredThrough(book, lane);
			if (!CounterShape(next, retired)) return false;
			if (op == null) return IsExactSuccessor(next, retired);
			long after;
			return IsExactSuccessor(op.Sequence, retired)
				&& CheckedAdd(op.Sequence, 1L, out after) && next == after;
		}

		private static bool CarrySequenceValid(KingdomCarryBook book)
		{
			if (book == null || !CounterShape(book.NextSequence, book.RetiredThrough)) return false;
			if (book.Open == null) return IsExactSuccessor(book.NextSequence, book.RetiredThrough);
			long after;
			return IsExactSuccessor(book.Open.Sequence, book.RetiredThrough)
				&& CheckedAdd(book.Open.Sequence, 1L, out after) && book.NextSequence == after;
		}

		private static bool CarryPhaseProgressValid(KingdomCarryOperation operation)
		{
			if (operation == null) return false;
			if (operation.Phase == KingdomLifecyclePhase.Quarantined) return true;
			bool sourcesDone = AllSourcesProved(operation);
			bool outputsDone = OutputsSettledForRoad(operation);
			bool outputsPrepared = operation.OutputIndex == 0;
			if (operation.Outputs == null) return false;
			for (int i = 0; i < operation.Outputs.Count; i++)
				if (operation.Outputs[i] == null
					|| operation.Outputs[i].State != KingdomLifecyclePhysicalState.Prepared)
					outputsPrepared = false;
			KingdomLifecycleLeaseState schedule = operation.ScheduleLease == null
				? KingdomLifecycleLeaseState.None : operation.ScheduleLease.State;
			switch (operation.Phase)
			{
			case KingdomLifecyclePhase.Prepared:
				return operation.SourceIndex == 0 && outputsPrepared
					&& schedule == KingdomLifecycleLeaseState.Prepared
					&& CarryEscrow(operation) == 0 && OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.RemovalIntent:
				return outputsPrepared && schedule == KingdomLifecycleLeaseState.Prepared
					&& MaterialDisposition(operation) == 0
					&& OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.Removed:
				return sourcesDone && outputsPrepared
					&& schedule == KingdomLifecycleLeaseState.Prepared
					&& MaterialDisposition(operation) == 0 && OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.ScheduleIntent:
				return sourcesDone && outputsPrepared
					&& (schedule == KingdomLifecycleLeaseState.Prepared
						|| schedule == KingdomLifecycleLeaseState.Intent
						|| schedule == KingdomLifecycleLeaseState.Proved)
					&& MaterialDisposition(operation) == 0 && OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.ProjectionIntent:
				return sourcesDone && schedule == KingdomLifecycleLeaseState.Proved
					&& OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.Projected:
				return sourcesDone && schedule == KingdomLifecycleLeaseState.Proved
					&& outputsDone && CarryEscrow(operation) == 0
					&& OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.Sinks:
				return sourcesDone && schedule == KingdomLifecycleLeaseState.Proved
					&& outputsDone && CarryEscrow(operation) == 0;
			case KingdomLifecyclePhase.Terminal:
				return sourcesDone && schedule == KingdomLifecycleLeaseState.Proved
					&& outputsDone && CarryEscrow(operation) == 0
					&& CarryOutboxTerminal(operation);
			default:
				return false;
			}
		}

		private static int MaterialDisposition(KingdomCarryOperation operation)
		{
			long value = operation == null ? -1L : SumSix(operation.DeliveredMud + operation.LostMud,
				operation.DeliveredBrush + operation.LostBrush,
				operation.DeliveredTimber + operation.LostTimber,
				operation.DeliveredStone + operation.LostStone,
				operation.DeliveredMarble + operation.LostMarble,
				operation.DeliveredScrap + operation.LostScrap);
			return value < 0L || value > int.MaxValue ? -1 : (int)value;
		}

		private static bool ExactOperationAuthority(KingdomLifecycleBook book,
			KingdomLifecycleOperation operation)
		{
			return operation != null && CanOwnAuthority(book)
				&& ReferenceEquals(GetSlot(book, operation.Lane), operation)
				&& string.Equals(operation.SettlementId, book.SettlementId,
					StringComparison.Ordinal);
		}

		private static bool ExactCarryAuthority(KingdomCarryBook book,
			KingdomCarryOperation operation)
		{
			return operation != null && CanOwnAuthority(book)
				&& ReferenceEquals(book.Open, operation)
				&& ExactStringList(operation.SettlementIds, book.SettlementIds)
				&& string.Equals(operation.RealmTopologyHash,
					RealmTopologyDigest(book.RealmId, book.SettlementIds), StringComparison.Ordinal);
		}

		private static bool ExactCarryScheduleAuthority(KingdomCarryBook book,
			KingdomCarryOperation operation, KingdomLifecycleResourceLease lease,
			out KingdomLifecycleResourceRevision row)
		{
			row = null;
			if (!ExactCarryAuthority(book, operation) || lease == null
				|| !ReferenceEquals(operation.ScheduleLease, lease)
				|| !CarryScheduleLeaseShape(book, operation, lease, false)) return false;
			row = FindResource(book, lease.Key);
			return ResourceMatches(row, lease)
				&& string.Equals(row.ActiveOperationId, operation.Id, StringComparison.Ordinal);
		}

		private static bool CarryScheduleLeaseShape(KingdomCarryBook book,
			KingdomCarryOperation operation, KingdomLifecycleResourceLease lease,
			bool Publication)
		{
			return book != null && operation != null
				&& LeaseShape(lease, operation.Id, Publication)
				&& lease.Kind == KingdomLifecycleResourceKind.Schedule
				&& string.Equals(lease.ScopeId, book.RealmId, StringComparison.Ordinal)
				&& string.Equals(lease.SubjectId, operation.DestinationSettlementId,
					StringComparison.Ordinal)
				&& lease.After == operation.DueTick;
		}

		private static bool CarryScheduleProved(KingdomCarryBook book,
			KingdomCarryOperation operation)
		{
			KingdomLifecycleResourceRevision row;
			return ExactCarryScheduleAuthority(book, operation, operation == null
				? null : operation.ScheduleLease, out row)
				&& operation.ScheduleLease.State == KingdomLifecycleLeaseState.Proved
				&& row.Revision == operation.ScheduleLease.AfterRevision
				&& string.Equals(row.LastOperationId, operation.Id, StringComparison.Ordinal)
				&& CarryScheduleReceiptShape(operation, false)
				&& operation.ScheduleReceiptState == KingdomLifecyclePhysicalState.Proved;
		}

		private static bool ExactLeaseAuthority(KingdomLifecycleBook book,
			KingdomLifecycleResourceLease lease, out KingdomLifecycleOperation operation,
			out KingdomLifecycleResourceRevision row)
		{
			operation = null;
			row = null;
			if (lease == null || !CanOwnAuthority(book)) return false;
			operation = FindOpenOperation(book, lease.OperationId);
			if (operation == null || !ReferenceEquals(GetSlot(book, operation.Lane), operation))
				return false;
			bool member = false;
			for (int i = 0; i < operation.ResourceLeases.Count; i++)
				if (ReferenceEquals(operation.ResourceLeases[i], lease)) { member = true; break; }
			if (!member) return false;
			row = FindResource(book, lease.Key);
			return row != null && ResourceMatches(row, lease)
				&& string.Equals(row.ActiveOperationId, operation.Id, StringComparison.Ordinal);
		}

		private static bool LeasePhaseAllows(KingdomLifecycleOperation operation,
			KingdomLifecycleResourceLease lease)
		{
			if (operation == null || lease == null) return false;
			switch (lease.Kind)
			{
			case KingdomLifecycleResourceKind.Schedule:
				return operation.Phase == KingdomLifecyclePhase.ScheduleIntent;
			case KingdomLifecycleResourceKind.WaterVessel:
				return operation.Phase == KingdomLifecyclePhase.WaterIntent;
			case KingdomLifecycleResourceKind.Projection:
				return operation.Phase == KingdomLifecyclePhase.ProjectionIntent;
			case KingdomLifecycleResourceKind.Object:
				return operation.Phase == KingdomLifecyclePhase.RemovalIntent;
			default:
				return operation.Phase == KingdomLifecyclePhase.DomainIntent;
			}
		}

		private static KingdomLifecyclePhase LeaseIntentPhase(
			KingdomLifecycleResourceLease lease)
		{
			if (lease == null) return KingdomLifecyclePhase.Invalid;
			switch (lease.Kind)
			{
			case KingdomLifecycleResourceKind.Schedule: return KingdomLifecyclePhase.ScheduleIntent;
			case KingdomLifecycleResourceKind.WaterVessel: return KingdomLifecyclePhase.WaterIntent;
			case KingdomLifecycleResourceKind.Projection: return KingdomLifecyclePhase.ProjectionIntent;
			case KingdomLifecycleResourceKind.Object: return KingdomLifecyclePhase.RemovalIntent;
			default: return KingdomLifecyclePhase.DomainIntent;
			}
		}

		private static int PhaseOrdinal(KingdomLifecycleAction action,
			KingdomLifecyclePhase phase)
		{
			if (phase == KingdomLifecyclePhase.Quarantined) return -2;
			KingdomLifecyclePhase current = KingdomLifecyclePhase.Prepared;
			for (int i = 0; i < 16; i++)
			{
				if (current == phase) return i;
				KingdomLifecyclePhase next;
				if (!TryNextPhase(action, current, out next)) break;
				current = next;
			}
			return -1;
		}

		private static bool LifecyclePhaseProgressValid(KingdomLifecycleOperation operation)
		{
			if (operation == null) return false;
			if (operation.Phase == KingdomLifecyclePhase.Quarantined) return true;
			int current = PhaseOrdinal(operation.Action, operation.Phase);
			int sinks = PhaseOrdinal(operation.Action, KingdomLifecyclePhase.Sinks);
			if (current < 0 || sinks < 0) return false;

			if (operation.Projections.Count > 0)
			{
				for (int i = 0; i < operation.Projections.Count; i++)
					if (!PhysicalProgressValid(operation, KingdomLifecyclePhase.ProjectionIntent,
						KingdomLifecyclePhase.Projected, operation.Projections[i].State, false)) return false;
				int projectionIntent = PhaseOrdinal(operation.Action,
					KingdomLifecyclePhase.ProjectionIntent);
				int projected = PhaseOrdinal(operation.Action, KingdomLifecyclePhase.Projected);
				if (current < projectionIntent && operation.Spawned != 0) return false;
				if (current >= projected && !ProjectionConserved(operation, true)) return false;
			}

			if (operation.WaterRequested > 0)
			{
				if (!PhysicalProgressValid(operation, KingdomLifecyclePhase.WaterIntent,
					KingdomLifecyclePhase.WaterSettled, operation.WaterState, false)) return false;
				for (int i = 0; i < operation.WaterLegs.Count; i++)
					if (!PhysicalProgressValid(operation, KingdomLifecyclePhase.WaterIntent,
						KingdomLifecyclePhase.WaterSettled, operation.WaterLegs[i].State, false))
						return false;
				int waterIntent = PhaseOrdinal(operation.Action, KingdomLifecyclePhase.WaterIntent);
				int waterSettled = PhaseOrdinal(operation.Action, KingdomLifecyclePhase.WaterSettled);
				if (current < waterIntent && (operation.WaterProved != 0
					|| operation.WaterOutstanding != operation.WaterRequested
					|| operation.WaterLost != 0 || operation.WaterAmbiguous != 0)) return false;
				if (current >= waterSettled && !WaterConserved(operation, true)) return false;
			}

			bool removes = operation.Action == KingdomLifecycleAction.Depart
				|| operation.Action == KingdomLifecycleAction.OfferWater;
			if (removes && !PhysicalProgressValid(operation, KingdomLifecyclePhase.RemovalIntent,
				KingdomLifecyclePhase.Removed, operation.RemovalState, false)) return false;

			if (operation.Action == KingdomLifecycleAction.RaidAttack)
			{
				int effectIntent = PhaseOrdinal(operation.Action, KingdomLifecyclePhase.EffectIntent);
				int effectsSettled = PhaseOrdinal(operation.Action, KingdomLifecyclePhase.EffectsSettled);
				if (current < effectIntent)
				{
					if (operation.EffectState != KingdomLifecyclePhysicalState.Prepared
						|| operation.PlunderProved != 0) return false;
				}
				else if (current == effectIntent)
				{
					if (operation.EffectState != KingdomLifecyclePhysicalState.Prepared
						&& operation.EffectState != KingdomLifecyclePhysicalState.Intent
						&& operation.EffectState != KingdomLifecyclePhysicalState.Proved
						&& operation.EffectState != KingdomLifecyclePhysicalState.Skipped) return false;
				}
				else if (current >= effectsSettled
					&& operation.EffectState != KingdomLifecyclePhysicalState.Proved
					&& operation.EffectState != KingdomLifecyclePhysicalState.Skipped) return false;
			}

			if (current < sinks) return OutboxInitial(operation.Outbox);
			if (current > sinks) return OutboxTerminal(operation);
			return true;
		}

		private static bool PhysicalProgressValid(KingdomLifecycleOperation operation,
			KingdomLifecyclePhase intentPhase, KingdomLifecyclePhase settledPhase,
			KingdomLifecyclePhysicalState state, bool allowSkipped)
		{
			int current = PhaseOrdinal(operation.Action, operation.Phase);
			int intent = PhaseOrdinal(operation.Action, intentPhase);
			int settled = PhaseOrdinal(operation.Action, settledPhase);
			if (current < 0 || intent < 0 || settled < 0) return false;
			if (current < intent) return state == KingdomLifecyclePhysicalState.Prepared;
			if (current < settled) return state == KingdomLifecyclePhysicalState.Prepared
				|| state == KingdomLifecyclePhysicalState.Intent
				|| state == KingdomLifecyclePhysicalState.Proved
				|| (allowSkipped && state == KingdomLifecyclePhysicalState.Skipped);
			return state == KingdomLifecyclePhysicalState.Proved
				|| (allowSkipped && state == KingdomLifecyclePhysicalState.Skipped);
		}

		private static bool OutboxInitial(KingdomLifecycleOutbox box)
		{
			return box != null
				&& InitialSinkState(box.ChronicleDisposition, box.ChronicleState)
				&& InitialSinkState(box.LedgerDisposition, box.LedgerState)
				&& InitialSinkState(box.MessageDisposition, box.MessageState)
				&& InitialSinkState(box.DeedDisposition, box.DeedState)
				&& InitialSinkState(box.GuestbookDisposition, box.GuestbookState);
		}

		private static bool InitialSinkState(KingdomLifecycleSinkDisposition disposition,
			KingdomLifecycleSinkState state)
		{
			return disposition == KingdomLifecycleSinkDisposition.Skip
				? state == KingdomLifecycleSinkState.Skipped
				: disposition == KingdomLifecycleSinkDisposition.Deliver
					&& state == KingdomLifecycleSinkState.Pending;
		}

		private static bool LeaseStateAllowedAtPhase(KingdomLifecycleOperation operation,
			KingdomLifecycleResourceLease lease)
		{
			if (operation == null || lease == null) return false;
			if (operation.Phase == KingdomLifecyclePhase.Quarantined)
				return lease.State == KingdomLifecycleLeaseState.Prepared
					|| lease.State == KingdomLifecycleLeaseState.Intent
					|| lease.State == KingdomLifecycleLeaseState.Proved;
			int current = PhaseOrdinal(operation.Action, operation.Phase);
			int intent = PhaseOrdinal(operation.Action, LeaseIntentPhase(lease));
			if (current < 0 || intent < 0) return false;
			if (current < intent) return lease.State == KingdomLifecycleLeaseState.Prepared;
			if (current == intent) return lease.State == KingdomLifecycleLeaseState.Prepared
				|| lease.State == KingdomLifecycleLeaseState.Intent
				|| lease.State == KingdomLifecycleLeaseState.Proved;
			return lease.State == KingdomLifecycleLeaseState.Proved;
		}

		private static KingdomLifecycleProjection ProjectionForLease(
			KingdomLifecycleOperation operation, KingdomLifecycleResourceLease lease)
		{
			if (operation == null || lease == null || operation.Projections == null) return null;
			KingdomLifecycleProjection found = null;
			for (int i = 0; i < operation.Projections.Count; i++)
			{
				KingdomLifecycleProjection projection = operation.Projections[i];
				if (projection == null) continue;
				string topology = TopologyId(projection.Topology, projection.OwnerId,
					projection.ZoneId, projection.X, projection.Y);
				string key = ResourceKey(KingdomLifecycleResourceKind.Projection,
					topology, projection.ObjectId);
				if (!string.Equals(key, lease.Key, StringComparison.Ordinal)) continue;
				if (found != null) return null;
				found = projection;
			}
			return found;
		}

		private static bool IsExactSuccessor(long value, long previous)
		{
			long expected;
			return previous >= 0L && previous < long.MaxValue
				&& CheckedAdd(previous, 1L, out expected) && value == expected;
		}

		public static void Normalize(KingdomLifecycleBook Book)
		{
			if (Book == null) return;
			if (PristineLifecycleBook(Book)) return;
			if (CanonicalLifecycleQuarantine(Book)) return;
			if (Book.FormatVersion != CurrentFormatVersion)
			{
				Book.WireRejected = true;
				Deny(Book, "unsupported lifecycle book version");
				return;
			}
			bool bad = Book.WireRejected || !ValidRootId(Book.SettlementId)
				|| !Book.IdentityBound || !ExactSettlementIdentityProof(Book)
				|| (Book.LegacyIdentity ? !ValidRootId(Book.LegacyMigrationKey)
					: !string.IsNullOrEmpty(Book.LegacyMigrationKey))
				|| !CounterShape(Book.PlainGuestNextSequence, Book.PlainGuestRetiredThrough)
				|| !CounterShape(Book.NotableGuestNextSequence, Book.NotableGuestRetiredThrough)
				|| !CounterShape(Book.RaidNextSequence, Book.RaidRetiredThrough)
				|| !CounterShape(Book.PetitionNextSequence, Book.PetitionRetiredThrough)
				|| !KnownOption(Book.LocusOption) || !KnownOption(Book.NotableOption)
				|| !KnownOption(Book.RaidOption) || !KnownOption(Book.PetitionOption)
				|| Book.LocusOptionTick < 0L || Book.NotableOptionTick < 0L
				|| Book.RaidOptionTick < 0L || Book.PetitionOptionTick < 0L
				|| TooLong(Book.Fault, MaxTextChars)
				|| Book.Resources == null || Book.Resources.Count > MaxResourceRows
				|| Book.RecentProofs == null || Book.RecentProofs.Count > MaxRecentProofs;

			HashSet<string> resourceKeys = new HashSet<string>(StringComparer.Ordinal);
			if (Book.Resources != null && Book.Resources.Count <= MaxResourceRows)
			{
				for (int i = 0; i < Book.Resources.Count; i++)
				{
					KingdomLifecycleResourceRevision row = Book.Resources[i];
					if (!ResourceShape(row) || !resourceKeys.Add(row.Key)) bad = true;
				}
			}

			if (!NormalizeOperation(Book, Book.PlainGuest, KingdomLifecycleLane.PlainGuest)) bad = true;
			if (!NormalizeOperation(Book, Book.NotableGuest, KingdomLifecycleLane.NotableGuest)) bad = true;
			if (!NormalizeOperation(Book, Book.Raid, KingdomLifecycleLane.Raid)) bad = true;
			if (!NormalizeOperation(Book, Book.Petition, KingdomLifecycleLane.Petition)) bad = true;
			if (!LaneSequenceValid(Book, KingdomLifecycleLane.PlainGuest, Book.PlainGuest)
				|| !LaneSequenceValid(Book, KingdomLifecycleLane.NotableGuest, Book.NotableGuest)
				|| !LaneSequenceValid(Book, KingdomLifecycleLane.Raid, Book.Raid)
				|| !LaneSequenceValid(Book, KingdomLifecycleLane.Petition, Book.Petition)) bad = true;
			if (!ProofListValid(Book)) bad = true;
			if (!ActiveResourcesValid(Book)) bad = true;
			if (bad) Deny(Book, "malformed lifecycle authority was quarantined without reinterpretation");
		}

		public static void Normalize(KingdomCarryBook Book)
		{
			if (Book == null) return;
			if (PristineCarryBook(Book)) return;
			if (Book.FormatVersion != CurrentCarryFormatVersion)
			{
				Book.WireRejected = true;
				Deny(Book, "unsupported carry book version");
				return;
			}
			bool bad = Book.WireRejected || !ValidRootId(Book.RealmId)
				|| !Book.IdentityBound || !ExactCarryIdentityProof(Book)
				|| (Book.LegacyIdentity ? !ValidRootId(Book.LegacyMigrationKey)
					: !string.IsNullOrEmpty(Book.LegacyMigrationKey))
				|| !CounterShape(Book.NextSequence, Book.RetiredThrough)
				|| TooLong(Book.Fault, MaxTextChars)
				|| !CarrySettlementSetShape(Book)
				|| !CarryResourceRegistryValid(Book)
				|| Book.RecentProofs == null || Book.RecentProofs.Count > MaxRecentProofs
				|| !CarryProofListValid(Book);
			if (Book.Open != null)
			{
				KingdomCarryOperation op = Book.Open;
				string hash;
					bool opBad = !CarrySequenceValid(Book)
						|| !string.Equals(op.Id, CarryId(Book.RealmId, op.Sequence), StringComparison.Ordinal)
						|| !ExactStringList(op.SettlementIds, Book.SettlementIds)
						|| !string.Equals(op.RealmTopologyHash,
							RealmTopologyDigest(Book.RealmId, Book.SettlementIds), StringComparison.Ordinal)
					|| !CarryPhaseAllowed(op.Phase) || op.CreatedTick < 0L
					|| op.UpdatedTick < op.CreatedTick || TooLong(op.Fault, MaxTextChars)
					|| !CarryPlanShape(op, false) || !TryCarryPlanHash(op, out hash)
					|| !string.Equals(op.PlanHash, hash, StringComparison.Ordinal)
					|| !SettlementMember(Book, op.OriginSettlementId)
					|| !SettlementMember(Book, op.DestinationSettlementId)
					|| !CarryConserved(op) || !CarryPhaseProgressValid(op);
				if (opBad)
				{
					if (KnownPhase(op.Phase)) Quarantine(op,
						"malformed carry operation was denied authority");
					bad = true;
				}
			}
			else if (!CarrySequenceValid(Book)) bad = true;
			if (!CarryActiveResourcesValid(Book)) bad = true;
			if (bad) Deny(Book, "malformed carry authority was quarantined without reinterpretation");
		}

		private static bool NormalizeOperation(KingdomLifecycleBook Book,
			KingdomLifecycleOperation Operation, KingdomLifecycleLane ExpectedLane)
		{
			if (Operation == null) return true;
			string hash;
			bool knownPhase = KnownPhase(Operation.Phase);
			bool good = Operation.Lane == ExpectedLane
					&& ActionAllowedInLane(Operation.Action, ExpectedLane)
					&& IsExactSuccessor(Operation.Sequence,
						GetRetiredThrough(Book, ExpectedLane))
					&& Operation.Sequence < GetNextSequence(Book, ExpectedLane)
				&& CanonicalOperationId(Operation)
				&& string.Equals(Operation.SettlementId, Book.SettlementId, StringComparison.Ordinal)
				&& knownPhase && PhaseAllowed(Operation.Action, Operation.Phase)
				&& Operation.CreatedTick >= 0L && Operation.UpdatedTick >= Operation.CreatedTick
					&& !TooLong(Operation.Fault, MaxTextChars)
					&& PlanShape(Operation, false)
					&& LifecyclePhaseProgressValid(Operation)
					&& TryPlanHash(Operation, out hash)
				&& string.Equals(Operation.PlanHash, hash, StringComparison.Ordinal);
			if (!good && knownPhase) Quarantine(Operation,
				"malformed lifecycle operation was denied authority");
			return good;
		}

		private static bool PublicationPlanValid(KingdomLifecycleOperation Operation)
		{
			return PlanShape(Operation, true)
				&& Operation.CreatedTick == Operation.UpdatedTick
				&& Operation.Phase == KingdomLifecyclePhase.Prepared;
		}

		private static bool PlanShape(KingdomLifecycleOperation op, bool Publication)
		{
			if (op == null || !ActionAllowedInLane(op.Action, op.Lane)
				|| !CanonicalOperationId(op) || !ValidRootId(op.SettlementId)
				|| op.CreatedTick < 0L || op.UpdatedTick < op.CreatedTick
				|| op.DueBefore < 0L || op.DueAfter < 0L || op.DepartTick < 0L
				|| TooLong(op.ZoneId, MaxNameChars) || TooLong(op.ObjectId, MaxIdChars)
				|| TooLong(op.ObjectMarker, MaxIdChars) || TooLong(op.Blueprint, MaxNameChars)
				|| TooLong(op.ObjectOwnerId, MaxIdChars)
				|| TooLong(op.ObjectName, MaxNameChars) || TooLong(op.Origin, MaxNameChars)
				|| TooLong(op.Faction, MaxNameChars) || TooLong(op.DisplayFaction, MaxNameChars)
				|| TooLong(op.Detail, MaxTextChars) || TooLong(op.Creed, MaxNameChars)
				|| TooLong(op.ArrivalText, MaxTextChars) || TooLong(op.Fault, MaxTextChars)
				|| !ValidCount(op.Count) || !ValidCount(op.DepartedCount)
				|| !ValidCount(op.Spawned) || !ValidCount(op.PlunderRequested)
				|| !ValidCount(op.PlunderProved)
				|| op.WaterLegs == null || op.WaterLegs.Count > MaxWaterLegs
				|| op.Projections == null || op.Projections.Count > MaxProjections
				|| op.ResourceLeases == null || op.ResourceLeases.Count > MaxResourceLeases
				|| !KnownPhysical(op.WaterState) || !KnownPhysical(op.RemovalState)
				|| !KnownPhysical(op.EffectState) || !OutboxShape(op, Publication)) return false;

			HashSet<string> waterOwners = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < op.WaterLegs.Count; i++)
					if (!WaterLegShape(op.WaterLegs[i], op, i, Publication)
					|| !waterOwners.Add(op.WaterLegs[i].OwnerId)) return false;
			HashSet<string> objects = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> events = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> markers = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < op.Projections.Count; i++)
			{
				KingdomLifecycleProjection p = op.Projections[i];
				if (!ProjectionShape(p, op.Id, i, Publication)
					|| !LifecycleProjectionReceiptPristine(p)
					|| !objects.Add(p.ObjectId) || !events.Add(p.EventId) || !markers.Add(p.Marker))
					return false;
				string topology = TopologyId(p.Topology, p.OwnerId, p.ZoneId, p.X, p.Y);
				KingdomLifecycleResourceLease projectionLease = FindLease(op,
					ResourceKey(KingdomLifecycleResourceKind.Projection, topology, p.ObjectId));
				if (projectionLease == null || projectionLease.Before != 0L
					|| projectionLease.Delta != p.Count || projectionLease.After != p.Count) return false;
			}
			HashSet<string> leases = new HashSet<string>(StringComparer.Ordinal);
			int scheduleLeases = 0;
			int waterLeases = 0;
			int projectionLeases = 0;
			int objectLeases = 0;
			int domainLeases = 0;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = op.ResourceLeases[i];
				if (!LeaseShape(lease, op.Id, Publication) || !leases.Add(lease.Key)) return false;
				switch (lease.Kind)
				{
				case KingdomLifecycleResourceKind.Schedule: scheduleLeases++; break;
				case KingdomLifecycleResourceKind.WaterVessel: waterLeases++; break;
				case KingdomLifecycleResourceKind.Projection: projectionLeases++; break;
				case KingdomLifecycleResourceKind.Object: objectLeases++; break;
				default: domainLeases++; break;
				}
			}
			string scheduleSubject = ScheduleSubjectId(op.SettlementId, op.Lane);
			KingdomLifecycleResourceLease schedule = FindLease(op,
				ResourceKey(KingdomLifecycleResourceKind.Schedule, op.SettlementId, scheduleSubject));
			if (scheduleLeases != 1 || schedule == null || schedule.Before != op.DueBefore
				|| schedule.After != op.DueAfter || schedule.Delta != op.DueAfter - op.DueBefore)
				return false;

			bool needsWater = op.Action == KingdomLifecycleAction.OfferWater
				|| op.Action == KingdomLifecycleAction.Lodge
				|| op.Action == KingdomLifecycleAction.RaidTribute;
			bool waterSkipped = op.WaterRequested == 0 && op.WaterProved == 0
				&& op.WaterOutstanding == 0 && op.WaterLost == 0 && op.WaterAmbiguous == 0
				&& op.WaterLegs.Count == 0 && op.WaterState == KingdomLifecyclePhysicalState.Skipped;
			if (needsWater)
			{
				if (op.WaterRequested <= 0 || !WaterConserved(op, false)) return false;
				if (Publication && (op.WaterProved != 0 || op.WaterOutstanding != op.WaterRequested
					|| op.WaterLost != 0 || op.WaterAmbiguous != 0
					|| op.WaterState != KingdomLifecyclePhysicalState.Prepared)) return false;
			}
			else if (!waterSkipped && op.Action != KingdomLifecycleAction.RaidAttack) return false;
			else if (op.Action == KingdomLifecycleAction.RaidAttack
				&& !waterSkipped && !WaterConserved(op, false)) return false;
			if (waterLeases != op.WaterLegs.Count) return false;

			bool needsProjection = op.Action == KingdomLifecycleAction.Spawn
				|| op.Action == KingdomLifecycleAction.RaidAttack;
			if (needsProjection && op.Projections.Count == 0) return false;
			if (!needsProjection && op.Projections.Count != 0) return false;
			if (projectionLeases != op.Projections.Count) return false;
			bool needsRemoval = op.Action == KingdomLifecycleAction.Depart
				|| op.Action == KingdomLifecycleAction.OfferWater;
			if (needsRemoval)
			{
				if (!ValidRootId(op.ObjectId) || !ValidName(op.Blueprint) || op.Count <= 0
					|| !TopologyValid(op.ObjectTopology, op.ObjectOwnerId, op.ZoneId,
						op.ObjectX, op.ObjectY)) return false;
				string topology = TopologyId(op.ObjectTopology, op.ObjectOwnerId, op.ZoneId,
					op.ObjectX, op.ObjectY);
				KingdomLifecycleResourceLease objectLease = FindLease(op,
					ResourceKey(KingdomLifecycleResourceKind.Object, topology, op.ObjectId));
				if (objectLease == null || objectLease.Before != op.Count
					|| objectLease.Delta != -op.Count || objectLease.After != 0L) return false;
				if (Publication && op.RemovalState != KingdomLifecyclePhysicalState.Prepared) return false;
			}
			else if (op.RemovalState != KingdomLifecyclePhysicalState.Skipped
				|| op.ObjectTopology != KingdomLifecycleTopology.None
				|| !string.IsNullOrEmpty(op.ObjectOwnerId) || op.ObjectX != -1 || op.ObjectY != -1)
				return false;
			if (objectLeases != (needsRemoval ? 1 : 0)) return false;
			bool needsEffect = op.Action == KingdomLifecycleAction.RaidAttack;
			if (Publication && needsEffect && op.EffectState != KingdomLifecyclePhysicalState.Prepared)
				return false;
			if (!needsEffect && op.EffectState != KingdomLifecyclePhysicalState.Skipped) return false;
			if (Publication)
			{
				if (op.Spawned != 0 || op.PlunderProved != 0 || op.DepartedCount != 0)
					return false;
				for (int i = 0; i < op.Projections.Count; i++)
					if (op.Projections[i].State != KingdomLifecyclePhysicalState.Prepared) return false;
			}
			KingdomLifecycleResourceKind requiredKind;
			long requiredDelta;
			bool requiresDomain = TryRequiredDomain(op, out requiredKind, out requiredDelta);
			if (requiresDomain)
			{
				KingdomLifecycleResourceLease domain = null;
				for (int i = 0; i < op.ResourceLeases.Count; i++)
					if (IsDomainLease(op.ResourceLeases[i])) domain = op.ResourceLeases[i];
				if (domainLeases != 1 || domain == null || domain.Kind != requiredKind
					|| !string.Equals(domain.ScopeId, op.SettlementId, StringComparison.Ordinal)
					|| !string.Equals(domain.SubjectId, op.SettlementId, StringComparison.Ordinal)
					|| domain.Delta != requiredDelta || domain.Before < 0L || domain.After < 0L)
					return false;
			}
			else if (domainLeases != 0) return false;
			if (op.Action == KingdomLifecycleAction.Depart)
			{
				KingdomLifecycleResourceLease domain = RequiredDomainLease(op);
				bool proved = domain != null && domain.State == KingdomLifecycleLeaseState.Proved;
				if (op.DepartedCount != (proved ? op.Count : 0)) return false;
			}
			else if (op.DepartedCount != 0) return false;
			if (Publication && op.DepartedCount != 0) return false;
			return ConservationEquations(op, false);
		}

		private static bool TerminalComponentsSettled(KingdomLifecycleBook Book,
			KingdomLifecycleOperation op)
		{
			if (Book == null || op == null || !PlanShape(op, false) || !OutboxTerminal(op)
				|| !ConservationEquations(op, true)) return false;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = op.ResourceLeases[i];
				KingdomLifecycleResourceRevision row = FindResource(Book, lease.Key);
				if (lease.State != KingdomLifecycleLeaseState.Proved || row == null
					|| row.Revision != lease.AfterRevision
					|| !string.Equals(row.ActiveOperationId, op.Id, StringComparison.Ordinal)
					|| !string.Equals(row.LastOperationId, op.Id, StringComparison.Ordinal)) return false;
			}
			for (int i = 0; i < op.Projections.Count; i++)
				if (op.Projections[i].State != KingdomLifecyclePhysicalState.Proved
					&& op.Projections[i].State != KingdomLifecyclePhysicalState.Skipped) return false;
			if (op.RemovalState != KingdomLifecyclePhysicalState.Proved
				&& op.RemovalState != KingdomLifecyclePhysicalState.Skipped) return false;
			if (op.EffectState != KingdomLifecyclePhysicalState.Proved
				&& op.EffectState != KingdomLifecyclePhysicalState.Skipped) return false;
			return true;
		}

		private static bool ConservationEquations(KingdomLifecycleOperation op, bool Terminal)
		{
			if (!WaterConserved(op, Terminal)) return false;
			if (op.PlunderProved > op.PlunderRequested || !ProjectionConserved(op, Terminal)) return false;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = op.ResourceLeases[i];
				long after;
				if (!CheckedAdd(lease.Before, lease.Delta, out after) || after != lease.After
					|| lease.BeforeRevision < 0L || lease.BeforeRevision == long.MaxValue
					|| lease.AfterRevision != lease.BeforeRevision + 1L) return false;
			}
			KingdomLifecycleResourceLease domain = RequiredDomainLease(op);
			if (RequiresDomainLease(op.Action) && domain == null) return false;
			if (op.Action == KingdomLifecycleAction.Depart)
			{
				bool proved = domain != null && domain.State == KingdomLifecycleLeaseState.Proved;
				if (op.DepartedCount != (proved ? op.Count : 0)
					|| (Terminal && op.DepartedCount != op.Count)) return false;
			}
			else if (op.DepartedCount != 0) return false;
			return true;
		}

		private static bool ProjectionConserved(KingdomLifecycleOperation op, bool terminal)
		{
			bool projects = op.Action == KingdomLifecycleAction.Spawn
				|| op.Action == KingdomLifecycleAction.RaidAttack;
			if (!projects) return op.Spawned == 0 && op.PartySize == 0;
			long planned = 0L;
			long proved = 0L;
			for (int i = 0; i < op.Projections.Count; i++)
			{
				planned += op.Projections[i].Count;
				if (op.Projections[i].State == KingdomLifecyclePhysicalState.Proved)
					proved += op.Projections[i].Count;
			}
			if (planned != op.PartySize || op.Spawned < 0 || op.Spawned > proved) return false;
			return !terminal || (proved == planned && op.Spawned == proved);
		}

		public static KingdomLifecycleSinkMask RequiredSinks(KingdomLifecycleAction Action,
			KingdomLifecycleLane Lane)
		{
			KingdomLifecycleSinkMask common = KingdomLifecycleSinkMask.Chronicle
				| KingdomLifecycleSinkMask.Ledger;
			switch (Action)
			{
			case KingdomLifecycleAction.Passages:
			case KingdomLifecycleAction.Depart:
				return common | (Lane == KingdomLifecycleLane.NotableGuest
					? KingdomLifecycleSinkMask.Guestbook : KingdomLifecycleSinkMask.None);
			case KingdomLifecycleAction.Lodge:
				return common | KingdomLifecycleSinkMask.Message
					| KingdomLifecycleSinkMask.Guestbook;
			case KingdomLifecycleAction.Spawn:
				return common | KingdomLifecycleSinkMask.Message
					| (Lane == KingdomLifecycleLane.NotableGuest
						? KingdomLifecycleSinkMask.Guestbook : KingdomLifecycleSinkMask.None);
			case KingdomLifecycleAction.OfferWater:
			case KingdomLifecycleAction.RaidWarning:
			case KingdomLifecycleAction.RaidRewarning:
			case KingdomLifecycleAction.RaidTribute:
			case KingdomLifecycleAction.RaidTalkDown:
			case KingdomLifecycleAction.RaidCancel:
			case KingdomLifecycleAction.PetitionOffer:
			case KingdomLifecycleAction.PetitionAccept:
			case KingdomLifecycleAction.PetitionDecline:
			case KingdomLifecycleAction.PetitionResolve:
			case KingdomLifecycleAction.PetitionExpire:
				return common | KingdomLifecycleSinkMask.Message;
			case KingdomLifecycleAction.RaidAttack:
				return common | KingdomLifecycleSinkMask.Message | KingdomLifecycleSinkMask.Deed;
			default:
				return KingdomLifecycleSinkMask.None;
			}
		}

		private static bool OutboxShape(KingdomLifecycleOperation op, bool Publication)
		{
			KingdomLifecycleOutbox box = op.Outbox;
			if (box == null || !string.Equals(box.OperationId, op.Id, StringComparison.Ordinal)
				|| !string.Equals(box.EventId, ChildId(op.Id, "outbox", 0), StringComparison.Ordinal)
				|| !string.Equals(box.ChronicleReceiptId, ChildId(op.Id, "chronicle", 0),
					StringComparison.Ordinal)
				|| TooLong(box.Chronicle, MaxTextChars) || TooLong(box.Ledger, MaxTextChars)
				|| TooLong(box.Message, MaxTextChars) || TooLong(box.Deed, MaxTextChars)
				|| TooLong(box.GuestbookLine, MaxTextChars)
				|| !SinkTextShape(box.Chronicle, box.ChronicleDisposition,
					box.ChronicleState, Publication)
				|| !SinkTextShape(box.Ledger, box.LedgerDisposition, box.LedgerState, Publication)
				|| !SinkTextShape(box.Message, box.MessageDisposition, box.MessageState, Publication)
				|| !SinkTextShape(box.Deed, box.DeedDisposition, box.DeedState, Publication)
				|| !SinkTextShape(box.GuestbookLine, box.GuestbookDisposition,
					box.GuestbookState, Publication)) return false;
			KingdomLifecycleSinkMask required = RequiredSinks(op.Action, op.Lane);
			return RequiredText(required, KingdomLifecycleSinkMask.Chronicle, box.Chronicle,
				box.ChronicleDisposition)
				&& RequiredText(required, KingdomLifecycleSinkMask.Ledger, box.Ledger,
					box.LedgerDisposition)
				&& RequiredText(required, KingdomLifecycleSinkMask.Message, box.Message,
					box.MessageDisposition)
				&& RequiredText(required, KingdomLifecycleSinkMask.Deed, box.Deed, box.DeedDisposition)
				&& RequiredText(required, KingdomLifecycleSinkMask.Guestbook, box.GuestbookLine,
					box.GuestbookDisposition);
		}

		private static bool OutboxTerminal(KingdomLifecycleOperation op)
		{
			if (!OutboxShape(op, false)) return false;
			KingdomLifecycleOutbox b = op.Outbox;
			if (!SinkSettled(b.ChronicleState) || !SinkSettled(b.LedgerState)
				|| !SinkSettled(b.MessageState) || !SinkSettled(b.DeedState)
				|| !SinkSettled(b.GuestbookState)) return false;
			// Chronicle.RecordOnce has exact receipt ownership and must be reconciled, never lost.
			return string.IsNullOrEmpty(b.Chronicle)
				? b.ChronicleState == KingdomLifecycleSinkState.Skipped
				: b.ChronicleState == KingdomLifecycleSinkState.Delivered;
		}

		private static bool SinkTextShape(string Text, KingdomLifecycleSinkDisposition Disposition,
			KingdomLifecycleSinkState State, bool Publication)
		{
			if (!KnownSink(State) || !KnownDisposition(Disposition)) return false;
			if (Disposition == KingdomLifecycleSinkDisposition.Skip)
				return State == KingdomLifecycleSinkState.Skipped;
			if (string.IsNullOrEmpty(Text)) return false;
			return Publication ? State == KingdomLifecycleSinkState.Pending
				: State == KingdomLifecycleSinkState.Pending || State == KingdomLifecycleSinkState.Intent
					|| State == KingdomLifecycleSinkState.Delivered
					|| State == KingdomLifecycleSinkState.Lost;
		}

		private static KingdomLifecycleSinkState InitialSink(string Text)
		{
			return string.IsNullOrEmpty(Text) ? KingdomLifecycleSinkState.Skipped
				: KingdomLifecycleSinkState.Pending;
		}

		private static KingdomLifecycleSinkDisposition InitialDisposition(string Text)
		{
			return string.IsNullOrEmpty(Text) ? KingdomLifecycleSinkDisposition.Skip
				: KingdomLifecycleSinkDisposition.Deliver;
		}

		private static bool RequiredText(KingdomLifecycleSinkMask Required,
			KingdomLifecycleSinkMask Bit, string Text, KingdomLifecycleSinkDisposition Disposition)
		{
			return (Required & Bit) == 0 || (!string.IsNullOrEmpty(Text)
				&& Disposition == KingdomLifecycleSinkDisposition.Deliver);
		}

		private static bool TryNextPhase(KingdomLifecycleAction Action,
			KingdomLifecyclePhase From, out KingdomLifecyclePhase To)
		{
			To = KingdomLifecyclePhase.Invalid;
			switch (From)
			{
			case KingdomLifecyclePhase.Prepared:
				if (Action == KingdomLifecycleAction.Passages) To = KingdomLifecyclePhase.Sinks;
				else if (Action == KingdomLifecycleAction.Spawn
					|| Action == KingdomLifecycleAction.RaidAttack) To = KingdomLifecyclePhase.ProjectionIntent;
				else if (Action == KingdomLifecycleAction.OfferWater
					|| Action == KingdomLifecycleAction.Lodge
					|| Action == KingdomLifecycleAction.RaidTribute) To = KingdomLifecyclePhase.WaterIntent;
				else if (Action == KingdomLifecycleAction.Depart) To = KingdomLifecyclePhase.RemovalIntent;
				else if (KnownAction(Action)) To = KingdomLifecyclePhase.DomainIntent;
				return To != KingdomLifecyclePhase.Invalid;
			case KingdomLifecyclePhase.ProjectionIntent:
				To = KingdomLifecyclePhase.Projected; return true;
			case KingdomLifecyclePhase.Projected:
				To = Action == KingdomLifecycleAction.RaidAttack
					? KingdomLifecyclePhase.WaterIntent : KingdomLifecyclePhase.DomainIntent;
				return true;
			case KingdomLifecyclePhase.WaterIntent:
				To = KingdomLifecyclePhase.WaterSettled; return true;
			case KingdomLifecyclePhase.WaterSettled:
				To = Action == KingdomLifecycleAction.OfferWater
					? KingdomLifecyclePhase.RemovalIntent : KingdomLifecyclePhase.DomainIntent;
				return true;
			case KingdomLifecyclePhase.RemovalIntent:
				To = KingdomLifecyclePhase.Removed; return true;
			case KingdomLifecyclePhase.Removed:
				To = KingdomLifecyclePhase.DomainIntent; return true;
			case KingdomLifecyclePhase.DomainIntent:
				To = KingdomLifecyclePhase.DomainSettled; return true;
			case KingdomLifecyclePhase.DomainSettled:
				To = Action == KingdomLifecycleAction.RaidAttack
					? KingdomLifecyclePhase.EffectIntent : KingdomLifecyclePhase.Sinks;
				return true;
			case KingdomLifecyclePhase.EffectIntent:
				To = KingdomLifecyclePhase.EffectsSettled; return true;
			case KingdomLifecyclePhase.EffectsSettled:
				To = KingdomLifecyclePhase.Sinks; return true;
			case KingdomLifecyclePhase.Sinks:
				To = KingdomLifecyclePhase.ScheduleIntent; return true;
			case KingdomLifecyclePhase.ScheduleIntent:
				To = KingdomLifecyclePhase.Terminal; return true;
			default:
				return false;
			}
		}

		private static bool TransitionReady(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, KingdomLifecyclePhase to)
		{
			if (to == KingdomLifecyclePhase.Quarantined) return true;
			if (to == KingdomLifecyclePhase.Projected)
			{
				for (int i = 0; i < op.Projections.Count; i++)
					if (op.Projections[i].State != KingdomLifecyclePhysicalState.Proved) return false;
				return ProjectionConserved(op, true)
					&& LeaseKindsProved(book, op, KingdomLifecycleResourceKind.Projection);
			}
			if (to == KingdomLifecyclePhase.WaterSettled)
			{
				if (op.WaterRequested == 0) return op.WaterState == KingdomLifecyclePhysicalState.Skipped;
				if (op.WaterState != KingdomLifecyclePhysicalState.Proved
					|| !WaterConserved(op, true)) return false;
				for (int i = 0; i < op.WaterLegs.Count; i++)
					if (op.WaterLegs[i].State != KingdomLifecyclePhysicalState.Proved) return false;
				return LeaseKindsProved(book, op, KingdomLifecycleResourceKind.WaterVessel);
			}
			if (to == KingdomLifecyclePhase.Removed)
				return op.RemovalState == KingdomLifecyclePhysicalState.Proved
					&& LeaseKindsProved(book, op, KingdomLifecycleResourceKind.Object);
			if (to == KingdomLifecyclePhase.DomainSettled)
			{
				for (int i = 0; i < op.ResourceLeases.Count; i++)
				{
					KingdomLifecycleResourceLease lease = op.ResourceLeases[i];
					if (lease.Kind != KingdomLifecycleResourceKind.Schedule
						&& lease.Kind != KingdomLifecycleResourceKind.WaterVessel
						&& lease.Kind != KingdomLifecycleResourceKind.Projection
						&& lease.Kind != KingdomLifecycleResourceKind.Object
						&& !LeaseProvedByRow(book, lease)) return false;
				}
				return true;
			}
			if (to == KingdomLifecyclePhase.EffectsSettled)
				return (op.EffectState == KingdomLifecyclePhysicalState.Proved
					|| op.EffectState == KingdomLifecyclePhysicalState.Skipped)
					&& op.PlunderProved <= op.PlunderRequested;
			if (to == KingdomLifecyclePhase.ScheduleIntent) return OutboxTerminal(op);
			if (to == KingdomLifecyclePhase.Terminal)
				return LeaseKindsProved(book, op, KingdomLifecycleResourceKind.Schedule)
					&& TerminalComponentsSettled(book, op);
			return true;
		}

		private static bool LeaseKindsProved(KingdomLifecycleBook book,
			KingdomLifecycleOperation op,
			KingdomLifecycleResourceKind kind)
		{
			bool found = false;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = op.ResourceLeases[i];
				if (lease.Kind != kind) continue;
				found = true;
				if (!LeaseProvedByRow(book, lease)) return false;
			}
			return found;
		}

		private static bool LeaseProvedByRow(KingdomLifecycleBook book,
			KingdomLifecycleResourceLease lease)
		{
			return lease != null && lease.State == KingdomLifecycleLeaseState.Proved
				&& ResourceWitnessMatches(FindResource(book, lease.Key), lease);
		}

		private static bool WaterLegShape(KingdomLifecycleWaterLeg leg,
			KingdomLifecycleOperation op, int ordinal, bool Publication)
		{
			if (leg == null || !string.Equals(leg.OperationId, op.Id, StringComparison.Ordinal)
				|| !ValidRootId(leg.OwnerId) || !ValidName(leg.Blueprint) || !ValidName(leg.ZoneId)
				|| leg.Capacity < 0 || leg.Before <= 0 || leg.Before > leg.Capacity
				|| leg.Delta <= 0 || leg.Delta > leg.Before || leg.After != leg.Before - leg.Delta
				|| string.IsNullOrEmpty(leg.Composition) || TooLong(leg.Composition, MaxTextChars)
				|| !KnownPhysical(leg.State) || !KnownPhysical(leg.ReceiptState)
				|| !string.Equals(leg.ReceiptId, ChildId(op.Id, "water-receipt", ordinal),
					StringComparison.Ordinal)) return false;
			string key = ResourceKey(KingdomLifecycleResourceKind.WaterVessel,
				leg.ZoneId, leg.OwnerId);
			if (!string.Equals(leg.LeaseKey, key, StringComparison.Ordinal)) return false;
			KingdomLifecycleResourceLease lease = FindLease(op, key);
			if (lease == null || lease.Before != leg.Before || lease.Delta != -leg.Delta
				|| lease.After != leg.After) return false;
			bool prepared = leg.State == KingdomLifecyclePhysicalState.Prepared
				&& leg.ReceiptState == KingdomLifecyclePhysicalState.Prepared
				&& leg.ReceiptBeforeMatches == -1 && leg.ReceiptAfterMatches == -1
				&& !leg.ReceiptSameReference && string.IsNullOrEmpty(leg.ReceiptProofId);
			if (Publication || prepared) return prepared;
			if (leg.State == KingdomLifecyclePhysicalState.Intent
				&& leg.ReceiptState == KingdomLifecyclePhysicalState.Intent)
				return leg.ReceiptBeforeMatches == 1 && leg.ReceiptAfterMatches == -1
					&& !leg.ReceiptSameReference && string.IsNullOrEmpty(leg.ReceiptProofId);
			return leg.State == KingdomLifecyclePhysicalState.Proved
				&& leg.ReceiptState == KingdomLifecyclePhysicalState.Proved
				&& leg.ReceiptBeforeMatches == 1 && leg.ReceiptAfterMatches == 1
				&& leg.ReceiptSameReference && ExactWaterReceipt(op, lease, leg);
		}

		private static bool ExactWaterReceipt(KingdomLifecycleOperation operation,
			KingdomLifecycleResourceLease lease, KingdomLifecycleWaterLeg leg)
		{
			if (operation == null || lease == null || leg == null
				|| lease.Kind != KingdomLifecycleResourceKind.WaterVessel
				|| !ReferenceEquals(FindWaterLeg(operation, lease.Key), leg)
				|| !string.Equals(lease.Key, leg.LeaseKey, StringComparison.Ordinal)
				|| leg.ReceiptBeforeMatches != 1 || leg.ReceiptAfterMatches != 1
				|| !leg.ReceiptSameReference) return false;
			return string.Equals(leg.ReceiptProofId,
				WaterReceiptProof(operation, lease, leg), StringComparison.Ordinal);
		}

		private static KingdomLifecycleWaterLeg FindWaterLeg(KingdomLifecycleOperation operation,
			string leaseKey)
		{
			if (operation == null || operation.WaterLegs == null || leaseKey == null) return null;
			KingdomLifecycleWaterLeg found = null;
			for (int i = 0; i < operation.WaterLegs.Count; i++)
			{
				KingdomLifecycleWaterLeg leg = operation.WaterLegs[i];
				if (leg == null || !string.Equals(leg.LeaseKey, leaseKey,
					StringComparison.Ordinal)) continue;
				if (found != null) return null;
				found = leg;
			}
			return found;
		}

		private static bool AllWaterLegsProved(KingdomLifecycleOperation operation)
		{
			if (operation == null || operation.WaterLegs == null
				|| operation.WaterLegs.Count == 0) return false;
			for (int i = 0; i < operation.WaterLegs.Count; i++)
				if (operation.WaterLegs[i] == null
					|| operation.WaterLegs[i].State != KingdomLifecyclePhysicalState.Proved)
					return false;
			return true;
		}

		private static bool ProjectionShape(KingdomLifecycleProjection p, string OperationId,
			int Ordinal, bool Publication)
		{
			if (p == null || !string.Equals(p.OperationId, OperationId, StringComparison.Ordinal)
				|| !string.Equals(p.EventId, ChildId(OperationId, "projection", Ordinal),
					StringComparison.Ordinal)
				|| !string.Equals(p.Marker, ChildId(OperationId, "marker", Ordinal),
					StringComparison.Ordinal)
				|| !ValidRootId(p.ObjectId) || !ValidName(p.Blueprint)
				|| !TopologyValid(p.Topology, p.OwnerId, p.ZoneId, p.X, p.Y)
				|| p.Material < -1 || p.Material >= 6 || p.Count <= 0
				|| p.Count > MaxPhysicalCount || !p.NoStack || !KnownPhysical(p.State)) return false;
			return !Publication || p.State == KingdomLifecyclePhysicalState.Prepared;
		}

		private static bool LifecycleProjectionReceiptPristine(KingdomLifecycleProjection p)
		{
			return p != null && string.IsNullOrEmpty(p.ReceiptId)
				&& string.IsNullOrEmpty(p.ReceiptTopologyId)
				&& p.ReceiptBeforeIdMatches == -1 && p.ReceiptBeforeMarkerMatches == -1
				&& p.ReceiptBeforeCount == -1 && p.ReceiptAfterIdMatches == -1
				&& p.ReceiptAfterMarkerMatches == -1 && p.ReceiptAfterCount == -1
				&& !p.ReceiptSameReference && string.IsNullOrEmpty(p.ReceiptProofId)
				&& p.ReceiptState == KingdomLifecyclePhysicalState.None;
		}

		private static bool CarrySourceReceiptPrepared(KingdomCarrySource source,
			KingdomCarryOperation operation, int ordinal)
		{
			return source != null && operation != null
				&& string.Equals(source.ReceiptId, ChildId(operation.Id,
					"source-receipt-" + ordinal.ToString(CultureInfo.InvariantCulture),
					source.Removed), StringComparison.Ordinal)
				&& string.Equals(source.ReceiptTopologyId, TopologyId(source.Topology,
					source.OwnerId, source.ZoneId, source.X, source.Y), StringComparison.Ordinal)
				&& source.ReceiptBeforeIdMatches == -1 && source.ReceiptAfterIdMatches == -1
				&& source.ReceiptBeforeCount == -1 && source.ReceiptAfterCount == -1
				&& !source.ReceiptSameReference && string.IsNullOrEmpty(source.ReceiptProofId)
				&& source.ReceiptState == KingdomLifecyclePhysicalState.Prepared
				&& ExactCarrySourceChain(source);
		}

		private static bool CarrySourceReceiptIntent(KingdomCarrySource source,
			KingdomCarryOperation operation, int ordinal)
		{
			return source != null && operation != null
				&& string.Equals(source.ReceiptId, ChildId(operation.Id,
					"source-receipt-" + ordinal.ToString(CultureInfo.InvariantCulture),
					source.Removed), StringComparison.Ordinal)
				&& string.Equals(source.ReceiptTopologyId, TopologyId(source.Topology,
					source.OwnerId, source.ZoneId, source.X, source.Y), StringComparison.Ordinal)
				&& source.ReceiptBeforeIdMatches == 1 && source.ReceiptAfterIdMatches == -1
				&& source.ReceiptBeforeCount == source.UnitBefore
				&& source.ReceiptAfterCount == -1 && !source.ReceiptSameReference
				&& string.IsNullOrEmpty(source.ReceiptProofId)
				&& source.ReceiptState == KingdomLifecyclePhysicalState.Intent
				&& ExactCarrySourceChain(source);
		}

		private static bool ExactCarrySourceReceipt(KingdomCarryOperation operation,
			KingdomCarrySource source, int ordinal)
		{
			int receiptOrdinal = source != null && source.Removed == source.PlannedCount
				? source.Removed - 1 : source == null ? -1 : source.Removed;
			return source != null && operation != null
				&& string.Equals(source.ReceiptId, ChildId(operation.Id,
					"source-receipt-" + ordinal.ToString(CultureInfo.InvariantCulture),
					receiptOrdinal), StringComparison.Ordinal)
				&& string.Equals(source.ReceiptTopologyId, TopologyId(source.Topology,
					source.OwnerId, source.ZoneId, source.X, source.Y), StringComparison.Ordinal)
				&& source.ReceiptBeforeIdMatches == 1 && source.ReceiptAfterIdMatches == 1
				&& source.ReceiptBeforeCount == source.UnitBefore
				&& source.ReceiptAfterCount == source.UnitAfter
				&& source.ReceiptSameReference
				&& source.ReceiptState == KingdomLifecyclePhysicalState.Proved
				&& string.Equals(source.ReceiptProofId,
					CarrySourceReceiptProof(operation, source, ordinal), StringComparison.Ordinal)
				&& ExactCarrySourceChain(source);
		}

		private static bool ExactCarrySourceChain(KingdomCarrySource source)
		{
			return source != null && source.ReceiptChainCount == source.Removed
				&& (source.Removed == 0 ? string.IsNullOrEmpty(source.ReceiptChainId)
					: ValidHashNamespace(source.ReceiptChainId, "carry-source-chain"));
		}

		private static string CarrySourceReceiptProof(KingdomCarryOperation operation,
			KingdomCarrySource source, int ordinal)
		{
			return HashId("carry-source-receipt", delegate(BinaryWriter w)
			{
				CanonicalString(w, operation == null ? null : operation.Id);
				w.Write(ordinal); CanonicalString(w, source == null ? null : source.UnitEventId);
				CanonicalString(w, source == null ? null : source.ReceiptId);
				CanonicalString(w, source == null ? null : source.ObjectId);
				CanonicalString(w, source == null ? null : source.Blueprint);
				CanonicalString(w, source == null ? null : source.ReceiptTopologyId);
				w.Write(source == null ? -1 : source.UnitBefore);
				w.Write(source == null ? -1 : source.UnitAfter);
				w.Write(source == null ? -1 : source.ReceiptBeforeIdMatches);
				w.Write(source == null ? -1 : source.ReceiptAfterIdMatches);
				w.Write(source == null ? -1 : source.ReceiptBeforeCount);
				w.Write(source == null ? -1 : source.ReceiptAfterCount);
				w.Write(source != null && source.ReceiptSameReference);
			});
		}

		private static string CarrySourceReceiptChain(string previous,
			string receiptProof, int count)
		{
			return HashId("carry-source-chain", delegate(BinaryWriter w)
			{
				CanonicalString(w, previous); CanonicalString(w, receiptProof); w.Write(count);
			});
		}

		private static void ResetCarrySourceReceipt(KingdomCarryOperation operation,
			KingdomCarrySource source, int ordinal, int removed)
		{
			source.ReceiptId = ChildId(operation.Id,
				"source-receipt-" + ordinal.ToString(CultureInfo.InvariantCulture), removed);
			source.ReceiptTopologyId = TopologyId(source.Topology, source.OwnerId,
				source.ZoneId, source.X, source.Y);
			source.ReceiptBeforeIdMatches = -1;
			source.ReceiptAfterIdMatches = -1;
			source.ReceiptBeforeCount = -1;
			source.ReceiptAfterCount = -1;
			source.ReceiptSameReference = false;
			source.ReceiptProofId = null;
			source.ReceiptState = KingdomLifecyclePhysicalState.Prepared;
			source.LiveAuthority = null;
		}

		private static bool CarryOutputShape(KingdomLifecycleProjection p, string OperationId,
			int Ordinal, bool Publication)
		{
			if (!ProjectionShape(p, OperationId, Ordinal, false)
				|| !string.Equals(p.ReceiptId, ChildId(OperationId, "output-receipt", Ordinal),
					StringComparison.Ordinal)
				|| !string.Equals(p.ReceiptTopologyId, TopologyId(p.Topology, p.OwnerId,
					p.ZoneId, p.X, p.Y), StringComparison.Ordinal)
				|| !KnownPhysical(p.ReceiptState)) return false;
			bool prepared = p.State == KingdomLifecyclePhysicalState.Prepared
				&& p.ReceiptState == KingdomLifecyclePhysicalState.Prepared
				&& p.ReceiptBeforeIdMatches == -1 && p.ReceiptBeforeMarkerMatches == -1
					&& p.ReceiptBeforeCount == -1 && p.ReceiptAfterIdMatches == -1
					&& p.ReceiptAfterMarkerMatches == -1 && p.ReceiptAfterCount == -1
					&& !p.ReceiptSameReference && string.IsNullOrEmpty(p.ReceiptProofId);
			if (Publication) return prepared;
			if (prepared) return true;
			if (p.State == KingdomLifecyclePhysicalState.Intent
				&& p.ReceiptState == KingdomLifecyclePhysicalState.Intent)
					return p.ReceiptBeforeIdMatches == 0 && p.ReceiptBeforeMarkerMatches == 0
						&& p.ReceiptBeforeCount == 0 && p.ReceiptAfterIdMatches == -1
						&& p.ReceiptAfterMarkerMatches == -1 && p.ReceiptAfterCount == -1
						&& !p.ReceiptSameReference && string.IsNullOrEmpty(p.ReceiptProofId);
			if (p.State == KingdomLifecyclePhysicalState.Proved
				&& p.ReceiptState == KingdomLifecyclePhysicalState.Proved)
					return p.ReceiptBeforeIdMatches == 0 && p.ReceiptBeforeMarkerMatches == 0
						&& p.ReceiptBeforeCount == 0 && p.ReceiptAfterIdMatches == 1
						&& p.ReceiptAfterMarkerMatches == 1 && p.ReceiptAfterCount == p.Count
						&& p.ReceiptSameReference && ExactCarryOutputReceiptForShape(p,
							OperationId, false);
			if (p.State == KingdomLifecyclePhysicalState.Skipped
				&& p.ReceiptState == KingdomLifecyclePhysicalState.Skipped)
					return p.ReceiptBeforeIdMatches == 0 && p.ReceiptBeforeMarkerMatches == 0
						&& p.ReceiptBeforeCount == 0 && p.ReceiptAfterIdMatches == 0
						&& p.ReceiptAfterMarkerMatches == 0 && p.ReceiptAfterCount == 0
						&& !p.ReceiptSameReference && ExactCarryOutputReceiptForShape(p,
							OperationId, true);
			return false;
		}

		private static bool ExactCarryOutputReceiptForShape(KingdomLifecycleProjection output,
			string operationId, bool lost)
		{
			return output != null && string.Equals(output.ReceiptProofId,
				CarryOutputReceiptProof(operationId, output, lost), StringComparison.Ordinal);
		}

		private static bool ExactCarryOutputReceipt(KingdomCarryOperation operation,
			KingdomLifecycleProjection output, bool lost)
		{
			return operation != null && output != null
				&& string.Equals(operation.Id, output.OperationId, StringComparison.Ordinal)
				&& ExactCarryOutputReceiptForShape(output, operation.Id, lost);
		}

		private static string CarryOutputReceiptProof(KingdomCarryOperation operation,
			KingdomLifecycleProjection output, bool lost)
		{
			return CarryOutputReceiptProof(operation == null ? null : operation.Id, output, lost);
		}

		private static string CarryOutputReceiptProof(string operationId,
			KingdomLifecycleProjection output, bool lost)
		{
			return HashId("carry-output-receipt", delegate(BinaryWriter w)
			{
				CanonicalString(w, operationId); CanonicalString(w, output.ReceiptId);
				CanonicalString(w, output.ObjectId); CanonicalString(w, output.Marker);
				CanonicalString(w, output.Blueprint);
				CanonicalString(w, output.ReceiptTopologyId); w.Write(output.Material);
				w.Write(output.Count); w.Write(output.ReceiptBeforeIdMatches);
				w.Write(output.ReceiptBeforeMarkerMatches); w.Write(output.ReceiptBeforeCount);
				w.Write(output.ReceiptAfterIdMatches); w.Write(output.ReceiptAfterMarkerMatches);
				w.Write(output.ReceiptAfterCount); w.Write(output.ReceiptSameReference);
				w.Write(lost);
			});
		}

		private static string WaterReceiptProof(KingdomLifecycleOperation operation,
			KingdomLifecycleResourceLease lease, KingdomLifecycleWaterLeg leg)
		{
			return HashId("water-receipt", delegate(BinaryWriter w)
			{
				CanonicalString(w, operation == null ? null : operation.Id);
				CanonicalString(w, operation == null ? null : operation.PlanHash);
				CanonicalString(w, leg.ReceiptId); WriteLeasePlan(w, lease);
				CanonicalString(w, leg.OwnerId); CanonicalString(w, leg.Blueprint);
				CanonicalString(w, leg.ZoneId);
				w.Write(leg.Capacity); w.Write(leg.Before); w.Write(leg.Delta); w.Write(leg.After);
				CanonicalString(w, leg.Composition); w.Write(leg.ReceiptBeforeMatches);
				w.Write(leg.ReceiptAfterMatches); w.Write(leg.ReceiptSameReference);
			});
		}

		private static bool CarryScheduleReceiptShape(KingdomCarryOperation operation,
			bool publication)
		{
			if (operation == null || operation.ScheduleLease == null
				|| !string.Equals(operation.ScheduleReceiptId,
					ChildId(operation.Id, "schedule-receipt", 0), StringComparison.Ordinal)
				|| !string.Equals(operation.ScheduleTopologyId, TopologyId(
					operation.DestinationTopology, operation.DestinationOwnerId,
					operation.DestinationZoneId, operation.DestinationX, operation.DestinationY),
					StringComparison.Ordinal) || !KnownPhysical(operation.ScheduleReceiptState))
				return false;
			bool prepared = operation.ScheduleReceiptState == KingdomLifecyclePhysicalState.Prepared
				&& operation.ScheduleBeforeMatches == -1 && operation.ScheduleAfterMatches == -1
				&& !operation.ScheduleSameReference && string.IsNullOrEmpty(operation.ScheduleProofId);
			if (publication || prepared) return prepared;
			if (operation.ScheduleReceiptState == KingdomLifecyclePhysicalState.Intent)
				return operation.ScheduleBeforeMatches == 1 && operation.ScheduleAfterMatches == -1
					&& !operation.ScheduleSameReference && string.IsNullOrEmpty(operation.ScheduleProofId);
			return operation.ScheduleReceiptState == KingdomLifecyclePhysicalState.Proved
				&& operation.ScheduleBeforeMatches == 1 && operation.ScheduleAfterMatches == 1
				&& operation.ScheduleSameReference
				&& string.Equals(operation.ScheduleProofId,
					CarryScheduleReceiptProof(operation), StringComparison.Ordinal);
		}

		private static string CarryScheduleReceiptProof(KingdomCarryOperation operation)
		{
			return HashId("carry-schedule-receipt", delegate(BinaryWriter w)
			{
				CanonicalString(w, operation.Id); CanonicalString(w, operation.PlanHash);
				CanonicalString(w, operation.RealmTopologyHash);
				CanonicalString(w, operation.DestinationSettlementId);
				CanonicalString(w, operation.ScheduleReceiptId);
				CanonicalString(w, operation.ScheduleTopologyId);
				CanonicalString(w, "Schedule");
				WriteLeasePlan(w, operation.ScheduleLease);
				w.Write(operation.ScheduleBeforeMatches); w.Write(operation.ScheduleAfterMatches);
				w.Write(operation.ScheduleSameReference);
			});
		}

		private static bool LeaseShape(KingdomLifecycleResourceLease lease,
			string OperationId, bool Publication)
		{
			long after;
			return lease != null && ValidRootId(OperationId)
				&& string.Equals(lease.OperationId, OperationId, StringComparison.Ordinal)
				&& KnownOuterResourceKind(lease.Kind) && ValidRootId(lease.ScopeId)
				&& ValidRootId(lease.SubjectId)
				&& string.Equals(lease.Key, ResourceKey(lease.Kind, lease.ScopeId, lease.SubjectId),
					StringComparison.Ordinal)
				&& lease.Delta != 0L && CheckedAdd(lease.Before, lease.Delta, out after)
				&& after == lease.After && lease.BeforeRevision >= 0L
				&& lease.BeforeRevision < long.MaxValue
				&& lease.AfterRevision == lease.BeforeRevision + 1L
				&& Enum.IsDefined(typeof(KingdomLifecycleLeaseState), lease.State)
				&& (!Publication || lease.State == KingdomLifecycleLeaseState.Prepared);
		}

		private static bool ResourceShape(KingdomLifecycleResourceRevision row)
		{
			return row != null && KnownOuterResourceKind(row.Kind) && ValidRootId(row.ScopeId)
				&& ValidRootId(row.SubjectId) && row.Revision >= 0L
				&& string.Equals(row.Key, ResourceKey(row.Kind, row.ScopeId, row.SubjectId),
					StringComparison.Ordinal)
				&& (string.IsNullOrEmpty(row.ActiveOperationId) || ValidGeneratedId(row.ActiveOperationId))
				&& (string.IsNullOrEmpty(row.LastOperationId) || ValidGeneratedId(row.LastOperationId));
		}

		private static bool ResourceMatches(KingdomLifecycleResourceRevision row,
			KingdomLifecycleResourceLease lease)
		{
			return row != null && lease != null && row.Kind == lease.Kind
				&& string.Equals(row.ScopeId, lease.ScopeId, StringComparison.Ordinal)
				&& string.Equals(row.SubjectId, lease.SubjectId, StringComparison.Ordinal)
				&& string.Equals(row.Key, lease.Key, StringComparison.Ordinal);
		}

		private static bool TopologyValid(KingdomLifecycleTopology topology,
			string OwnerId, string ZoneId, int X, int Y)
		{
			if (!ValidName(ZoneId)) return false;
			if (topology == KingdomLifecycleTopology.Cell)
				return OwnerId == null && X >= 0 && X <= MaxCoordinate
					&& Y >= 0 && Y <= MaxCoordinate;
			if (topology == KingdomLifecycleTopology.Inventory)
				return ValidRootId(OwnerId) && X == -1 && Y == -1;
			return false;
		}

		private static bool CarryPublicationPlanValid(KingdomCarryOperation op)
		{
			return CarryPlanShape(op, true) && op.CreatedTick == op.UpdatedTick
				&& op.Phase == KingdomLifecyclePhase.Prepared && CarryConserved(op);
		}

		private static bool CarryPlanShape(KingdomCarryOperation op, bool Publication)
		{
			if (op == null || op.Sequence <= 0L || !ValidGeneratedId(op.Id)
				|| op.CreatedTick < 0L || op.UpdatedTick < op.CreatedTick
				|| !FrozenSettlementSetValid(op.SettlementIds)
				|| !ValidHashNamespace(op.RealmTopologyHash, "carry-realm-topology")
				|| !ValidRootId(op.OriginSettlementId)
				|| op.SettlementIds.BinarySearch(op.OriginSettlementId, StringComparer.Ordinal) < 0
				|| !ValidName(op.OriginZoneId) || op.OriginX < 0 || op.OriginX > MaxCoordinate
				|| op.OriginY < 0 || op.OriginY > MaxCoordinate
				|| !ValidRootId(op.DestinationSettlementId)
				|| op.SettlementIds.BinarySearch(op.DestinationSettlementId, StringComparer.Ordinal) < 0
				|| !ValidName(op.DestinationSettlementName)
				|| !TopologyValid(op.DestinationTopology, op.DestinationOwnerId,
					op.DestinationZoneId, op.DestinationX, op.DestinationY)
				|| op.DueTick < 0L || !op.RiskFrozen
				|| op.SourceIndex < 0 || op.OutputIndex < 0
				|| op.Sources == null || op.Sources.Count == 0
				|| op.Sources.Count > MaxCarrySources
				|| op.Outputs == null || op.Outputs.Count == 0
				|| op.Outputs.Count > MaxCarryOutputs
				|| op.SourceIndex > op.Sources.Count || op.OutputIndex > op.Outputs.Count
				|| TooLong(op.Fault, MaxTextChars) || !CarryCountsValid(op)
				|| !LeaseShape(op.ScheduleLease, op.Id, Publication)
				|| op.ScheduleLease.Kind != KingdomLifecycleResourceKind.Schedule
				|| !string.Equals(op.ScheduleLease.SubjectId, op.DestinationSettlementId,
					StringComparison.Ordinal)
				|| op.ScheduleLease.After != op.DueTick
				|| !CarryScheduleReceiptShape(op, Publication)
				|| !CarryOutboxShape(op, Publication)) return false;

			HashSet<string> objects = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> events = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < op.Sources.Count; i++)
			{
				KingdomCarrySource source = op.Sources[i];
				if (!CarrySourceShape(source, op, i, Publication)
					|| !objects.Add(source.ObjectId) || !events.Add(source.SourceEventId)) return false;
			}
			if (op.SourceIndex != FirstIncompleteSource(op)) return false;
			HashSet<string> outputObjects = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> outputEvents = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> markers = new HashSet<string>(StringComparer.Ordinal);
			long[] output = new long[6];
			for (int i = 0; i < op.Outputs.Count; i++)
			{
				KingdomLifecycleProjection p = op.Outputs[i];
				KingdomLifecyclePhysicalState settled = op.LostOnRoad
					? KingdomLifecyclePhysicalState.Skipped : KingdomLifecyclePhysicalState.Proved;
				if (!CarryOutputShape(p, op.Id, i, Publication) || p.Material < 0
					|| !string.Equals(p.ZoneId, op.DestinationZoneId, StringComparison.Ordinal)
					|| objects.Contains(p.ObjectId)
					|| !outputObjects.Add(p.ObjectId) || !outputEvents.Add(p.EventId)
					|| !markers.Add(p.Marker) || !CheckedAccumulate(output, p.Material, p.Count))
					return false;
				if (!Publication && ((i < op.OutputIndex && p.State != settled)
					|| (i == op.OutputIndex && p.State != KingdomLifecyclePhysicalState.Prepared
						&& p.State != KingdomLifecyclePhysicalState.Intent && p.State != settled)
					|| (i > op.OutputIndex && p.State != KingdomLifecyclePhysicalState.Prepared)))
					return false;
			}
			for (int material = 0; material < 6; material++)
				if (output[material] != MaterialValue(op, material, 0)) return false;
			if (Publication)
			{
				if (op.SourceIndex != 0 || op.OutputIndex != 0) return false;
				for (int material = 0; material < 6; material++)
					if (MaterialValue(op, material, 1) != 0
						|| MaterialValue(op, material, 2) != 0
						|| MaterialValue(op, material, 3) != 0) return false;
			}
			return true;
		}

		private static bool CarrySourceShape(KingdomCarrySource source,
			KingdomCarryOperation op, int ordinal, bool Publication)
		{
			if (source == null || !string.Equals(source.OperationId, op.Id, StringComparison.Ordinal)
				|| !string.Equals(source.SourceEventId, ChildId(op.Id, "source", ordinal),
					StringComparison.Ordinal)
				|| !ValidRootId(source.ObjectId) || !ValidName(source.Blueprint)
				|| !TopologyValid(source.Topology, source.OwnerId, source.ZoneId, source.X, source.Y)
				|| source.Material < 0 || source.Material >= 6 || source.OriginalCount <= 0
				|| source.OriginalCount > MaxPhysicalCount || source.PlannedCount <= 0
				|| source.PlannedCount > source.OriginalCount || source.Removed < 0
				|| source.Removed > source.PlannedCount || source.UnitCursor != source.Removed
				|| !KnownPhysical(source.UnitState) || !KnownPhysical(source.State)) return false;
			int expectedOrdinal = source.Removed == source.PlannedCount
				? source.Removed - 1 : source.Removed;
			if (expectedOrdinal < 0) expectedOrdinal = 0;
			if (!string.Equals(source.UnitEventId, ChildId(op.Id,
				"source-unit-" + ordinal.ToString(CultureInfo.InvariantCulture), expectedOrdinal),
				StringComparison.Ordinal)) return false;
			if (source.Removed == source.PlannedCount)
			{
				if (source.State != KingdomLifecyclePhysicalState.Proved
					|| source.UnitState != KingdomLifecyclePhysicalState.Proved
					|| source.UnitBefore != source.OriginalCount - source.Removed + 1
					|| source.UnitAfter != source.OriginalCount - source.Removed
					|| !ExactCarrySourceReceipt(op, source, ordinal)) return false;
			}
			else
			{
				if (source.State != KingdomLifecyclePhysicalState.Prepared
					|| (source.UnitState != KingdomLifecyclePhysicalState.Prepared
						&& source.UnitState != KingdomLifecyclePhysicalState.Intent)
					|| source.UnitBefore != source.OriginalCount - source.Removed
					|| source.UnitAfter != source.UnitBefore - 1) return false;
				if (source.UnitState == KingdomLifecyclePhysicalState.Prepared)
				{
					if (!CarrySourceReceiptPrepared(source, op, ordinal)) return false;
				}
				else if (source.ReceiptState == KingdomLifecyclePhysicalState.Intent)
				{
					if (!CarrySourceReceiptIntent(source, op, ordinal)) return false;
				}
				else if (!ExactCarrySourceReceipt(op, source, ordinal)) return false;
			}
			return !Publication || (source.Removed == 0 && source.UnitCursor == 0
				&& source.UnitState == KingdomLifecyclePhysicalState.Prepared
				&& source.State == KingdomLifecyclePhysicalState.Prepared
				&& CarrySourceReceiptPrepared(source, op, ordinal));
		}

		private static bool CarryOutboxShape(KingdomCarryOperation op, bool Publication)
		{
			KingdomLifecycleOutbox b = op.Outbox;
			return b != null && b.OperationId == op.Id
				&& b.EventId == ChildId(op.Id, "outbox", 0)
				&& b.ChronicleReceiptId == ChildId(op.Id, "chronicle", 0)
				&& !string.IsNullOrEmpty(b.Chronicle) && !string.IsNullOrEmpty(b.Ledger)
				&& !string.IsNullOrEmpty(b.Message)
				&& b.ChronicleDisposition == KingdomLifecycleSinkDisposition.Deliver
				&& b.LedgerDisposition == KingdomLifecycleSinkDisposition.Deliver
				&& b.MessageDisposition == KingdomLifecycleSinkDisposition.Deliver
				&& SinkTextShape(b.Chronicle, b.ChronicleDisposition,
					b.ChronicleState, Publication)
				&& SinkTextShape(b.Ledger, b.LedgerDisposition, b.LedgerState, Publication)
				&& SinkTextShape(b.Message, b.MessageDisposition, b.MessageState, Publication)
				&& SinkTextShape(b.Deed, b.DeedDisposition, b.DeedState, Publication)
				&& SinkTextShape(b.GuestbookLine, b.GuestbookDisposition,
					b.GuestbookState, Publication);
		}

		private static bool CarryOutboxTerminal(KingdomCarryOperation op)
		{
			if (!CarryOutboxShape(op, false)) return false;
			KingdomLifecycleOutbox b = op.Outbox;
			return b.ChronicleState == KingdomLifecycleSinkState.Delivered
				&& b.LedgerState == KingdomLifecycleSinkState.Delivered
				&& b.MessageState == KingdomLifecycleSinkState.Delivered
				&& SinkSettled(b.DeedState) && SinkSettled(b.GuestbookState);
		}

		private static bool CarryTerminalComponentsSettled(KingdomCarryOperation op)
		{
			if (op == null || !CarryPlanShape(op, false) || !CarryConserved(op)
				|| !AllSourcesProved(op) || !OutputsSettledForRoad(op)
				|| CarryEscrow(op) != 0 || !CarryOutboxTerminal(op)) return false;
			for (int material = 0; material < 6; material++)
				if (MaterialValue(op, material, 0) != MaterialValue(op, material, 2)
					+ MaterialValue(op, material, 3)) return false;
			return true;
		}

		private static bool AllSourcesProved(KingdomCarryOperation op)
		{
			if (op == null || op.Sources == null || op.SourceIndex != op.Sources.Count) return false;
			for (int i = 0; i < op.Sources.Count; i++)
				if (op.Sources[i].State != KingdomLifecyclePhysicalState.Proved
					|| op.Sources[i].Removed != op.Sources[i].PlannedCount) return false;
			return true;
		}

		private static bool OutputsSettledForRoad(KingdomCarryOperation op)
		{
			if (op == null || op.Outputs == null || op.OutputIndex != op.Outputs.Count) return false;
			long[] proved = new long[6];
			for (int i = 0; i < op.Outputs.Count; i++)
			{
				KingdomLifecycleProjection p = op.Outputs[i];
				KingdomLifecyclePhysicalState expected = op.LostOnRoad
					? KingdomLifecyclePhysicalState.Skipped : KingdomLifecyclePhysicalState.Proved;
				if (p.State != expected || p.ReceiptState != expected
					|| !CarryOutputShape(p, op.Id, i, false)
					|| !CheckedAccumulate(proved, p.Material,
					op.LostOnRoad ? 0 : p.Count)) return false;
			}
			for (int material = 0; material < 6; material++)
				if (proved[material] != MaterialValue(op, material, 2)) return false;
			return true;
		}

		private static bool CarryPhaseAllowed(KingdomLifecyclePhase phase)
		{
			return phase == KingdomLifecyclePhase.Prepared
				|| phase == KingdomLifecyclePhase.RemovalIntent
				|| phase == KingdomLifecyclePhase.Removed
				|| phase == KingdomLifecyclePhase.ScheduleIntent
				|| phase == KingdomLifecyclePhase.ProjectionIntent
				|| phase == KingdomLifecyclePhase.Projected
				|| phase == KingdomLifecyclePhase.Sinks
				|| phase == KingdomLifecyclePhase.Terminal
				|| phase == KingdomLifecyclePhase.Quarantined;
		}

		private static bool CarryCountsValid(KingdomCarryOperation op)
		{
			for (int material = 0; material < 6; material++)
				for (int group = 0; group < 4; group++)
					if (!ValidCount(MaterialValue(op, material, group))) return false;
			return true;
		}

		private static bool AddMaterial(KingdomCarryOperation op, int material,
			int escrowDelta, int deliveredDelta, int lostDelta)
		{
			int escrow = MaterialValue(op, material, 1);
			int delivered = MaterialValue(op, material, 2);
			int lost = MaterialValue(op, material, 3);
			int e, d, l;
			if (!CheckedAdd(escrow, escrowDelta, out e) || !CheckedAdd(delivered, deliveredDelta, out d)
				|| !CheckedAdd(lost, lostDelta, out l) || !ValidCount(e) || !ValidCount(d)
				|| !ValidCount(l)) return false;
			SetMaterial(op, material, 1, e); SetMaterial(op, material, 2, d);
			SetMaterial(op, material, 3, l);
			return true;
		}

		private static int MaterialValue(KingdomCarryOperation op, int material, int group)
		{
			if (op == null || material < 0 || material >= 6 || group < 0 || group > 3) return -1;
			if (group == 0)
			{
				switch (material) { case 0: return op.Mud; case 1: return op.Brush;
				case 2: return op.Timber; case 3: return op.Stone; case 4: return op.Marble;
				default: return op.Scrap; }
			}
			if (group == 1)
			{
				switch (material) { case 0: return op.EscrowMud; case 1: return op.EscrowBrush;
				case 2: return op.EscrowTimber; case 3: return op.EscrowStone;
				case 4: return op.EscrowMarble; default: return op.EscrowScrap; }
			}
			if (group == 2)
			{
				switch (material) { case 0: return op.DeliveredMud; case 1: return op.DeliveredBrush;
				case 2: return op.DeliveredTimber; case 3: return op.DeliveredStone;
				case 4: return op.DeliveredMarble; default: return op.DeliveredScrap; }
			}
			switch (material) { case 0: return op.LostMud; case 1: return op.LostBrush;
			case 2: return op.LostTimber; case 3: return op.LostStone;
			case 4: return op.LostMarble; default: return op.LostScrap; }
		}

		private static void SetMaterial(KingdomCarryOperation op, int material, int group, int value)
		{
			if (group == 1)
			{
				switch (material) { case 0: op.EscrowMud = value; break; case 1: op.EscrowBrush = value; break;
				case 2: op.EscrowTimber = value; break; case 3: op.EscrowStone = value; break;
				case 4: op.EscrowMarble = value; break; default: op.EscrowScrap = value; break; }
			}
			else if (group == 2)
			{
				switch (material) { case 0: op.DeliveredMud = value; break; case 1: op.DeliveredBrush = value; break;
				case 2: op.DeliveredTimber = value; break; case 3: op.DeliveredStone = value; break;
				case 4: op.DeliveredMarble = value; break; default: op.DeliveredScrap = value; break; }
			}
			else if (group == 3)
			{
				switch (material) { case 0: op.LostMud = value; break; case 1: op.LostBrush = value; break;
				case 2: op.LostTimber = value; break; case 3: op.LostStone = value; break;
				case 4: op.LostMarble = value; break; default: op.LostScrap = value; break; }
			}
		}

		public static bool TryPlanHash(KingdomLifecycleOperation op, out string Hash)
		{
			Hash = null;
			if (op == null) return false;
			try
			{
				Hash = HashId("plan", delegate(BinaryWriter w)
				{
					w.Write(op.Sequence); CanonicalString(w, op.Id); w.Write((byte)op.Lane);
					w.Write((byte)op.Action); w.Write(op.CreatedTick);
					CanonicalString(w, op.SettlementId); CanonicalString(w, op.ZoneId);
					CanonicalString(w, op.ObjectId); CanonicalString(w, op.ObjectMarker);
					CanonicalString(w, op.Blueprint); w.Write((byte)op.ObjectTopology);
					CanonicalString(w, op.ObjectOwnerId); w.Write(op.ObjectX); w.Write(op.ObjectY);
					CanonicalString(w, op.ObjectName);
					CanonicalString(w, op.Origin); CanonicalString(w, op.Faction);
					CanonicalString(w, op.DisplayFaction); CanonicalString(w, op.Detail);
					CanonicalString(w, op.Creed); w.Write(op.Kind); w.Write(op.Target);
					w.Write(op.Count); w.Write(op.DueBefore);
					w.Write(op.DueAfter); w.Write(op.DepartTick); w.Write(op.WaterRequested);
					w.Write(op.Defence); w.Write(op.PartySize);
					w.Write(op.PlunderRequested); CanonicalString(w, op.ArrivalText);
					w.Write(op.WaterLegs == null ? -1 : op.WaterLegs.Count);
					if (op.WaterLegs != null) for (int i = 0; i < op.WaterLegs.Count; i++)
					{
						KingdomLifecycleWaterLeg x = op.WaterLegs[i];
						CanonicalString(w, x.OperationId); CanonicalString(w, x.LeaseKey);
						CanonicalString(w, x.OwnerId); CanonicalString(w, x.Blueprint);
						CanonicalString(w, x.ZoneId);
						w.Write(x.Capacity); w.Write(x.Before); w.Write(x.Delta); w.Write(x.After);
						CanonicalString(w, x.Composition); CanonicalString(w, x.ReceiptId);
					}
					w.Write(op.Projections == null ? -1 : op.Projections.Count);
					if (op.Projections != null) for (int i = 0; i < op.Projections.Count; i++)
						WriteProjectionPlan(w, op.Projections[i]);
					w.Write(op.ResourceLeases == null ? -1 : op.ResourceLeases.Count);
					if (op.ResourceLeases != null) for (int i = 0; i < op.ResourceLeases.Count; i++)
						WriteLeasePlan(w, op.ResourceLeases[i]);
					WriteOutboxPlan(w, op.Outbox);
				});
				return ValidHashNamespace(Hash, "plan");
			}
			catch (Exception)
			{
				Hash = null;
				return false;
			}
		}

		public static bool TryCarryPlanHash(KingdomCarryOperation op, out string Hash)
		{
			Hash = null;
			if (op == null) return false;
			try
			{
				Hash = HashId("carry-plan", delegate(BinaryWriter w)
				{
					w.Write(op.Sequence); CanonicalString(w, op.Id); w.Write(op.CreatedTick);
					w.Write(op.SettlementIds == null ? -1 : op.SettlementIds.Count);
					if (op.SettlementIds != null) for (int i = 0; i < op.SettlementIds.Count; i++)
						CanonicalString(w, op.SettlementIds[i]);
					CanonicalString(w, op.RealmTopologyHash);
					CanonicalString(w, op.OriginSettlementId);
					CanonicalString(w, op.OriginZoneId); w.Write(op.OriginX); w.Write(op.OriginY);
					CanonicalString(w, op.DestinationSettlementId);
					CanonicalString(w, op.DestinationSettlementName);
					CanonicalString(w, op.DestinationZoneId); w.Write((byte)op.DestinationTopology);
					CanonicalString(w, op.DestinationOwnerId); w.Write(op.DestinationX);
					w.Write(op.DestinationY); w.Write(op.DueTick);
					w.Write(op.RiskFrozen); w.Write(op.LostOnRoad);
					WriteLeasePlan(w, op.ScheduleLease);
					CanonicalString(w, op.ScheduleReceiptId);
					CanonicalString(w, op.ScheduleTopologyId);
					w.Write(op.Sources == null ? -1 : op.Sources.Count);
					if (op.Sources != null) for (int i = 0; i < op.Sources.Count; i++)
					{
						KingdomCarrySource x = op.Sources[i];
						CanonicalString(w, x.OperationId); CanonicalString(w, x.SourceEventId);
						CanonicalString(w, x.ObjectId); CanonicalString(w, x.Blueprint);
						w.Write((byte)x.Topology); CanonicalString(w, x.OwnerId);
						CanonicalString(w, x.ZoneId); w.Write(x.X); w.Write(x.Y);
						w.Write(x.Material); w.Write(x.OriginalCount); w.Write(x.PlannedCount);
					}
					w.Write(op.Outputs == null ? -1 : op.Outputs.Count);
					if (op.Outputs != null) for (int i = 0; i < op.Outputs.Count; i++)
						WriteProjectionPlan(w, op.Outputs[i]);
					for (int material = 0; material < 6; material++)
						w.Write(MaterialValue(op, material, 0));
					WriteOutboxPlan(w, op.Outbox);
				});
				return ValidHashNamespace(Hash, "carry-plan");
			}
			catch (Exception)
			{
				Hash = null;
				return false;
			}
		}

		private static void WriteProjectionPlan(BinaryWriter w, KingdomLifecycleProjection x)
		{
			CanonicalString(w, x.OperationId); CanonicalString(w, x.EventId);
			CanonicalString(w, x.ObjectId); CanonicalString(w, x.Marker);
			CanonicalString(w, x.Blueprint); CanonicalString(w, x.ZoneId);
			w.Write((byte)x.Topology); CanonicalString(w, x.OwnerId); w.Write(x.X); w.Write(x.Y);
			w.Write(x.Material); w.Write(x.Count); w.Write(x.NoStack);
			CanonicalString(w, x.ReceiptId); CanonicalString(w, x.ReceiptTopologyId);
		}

		private static void WriteLeasePlan(BinaryWriter w, KingdomLifecycleResourceLease x)
		{
			CanonicalString(w, x.OperationId); w.Write((byte)x.Kind);
			CanonicalString(w, x.ScopeId); CanonicalString(w, x.SubjectId);
			CanonicalString(w, x.Key); w.Write(x.Before); w.Write(x.Delta); w.Write(x.After);
			w.Write(x.BeforeRevision); w.Write(x.AfterRevision);
		}

		private static void WriteOutboxPlan(BinaryWriter w, KingdomLifecycleOutbox x)
		{
			if (x == null) { w.Write(false); return; }
			w.Write(true); CanonicalString(w, x.OperationId); CanonicalString(w, x.EventId);
			CanonicalString(w, x.ChronicleReceiptId); CanonicalString(w, x.Chronicle);
			w.Write(x.ChronicleAccomplishment); w.Write((byte)x.ChronicleDisposition);
			CanonicalString(w, x.Ledger); w.Write((byte)x.LedgerDisposition);
			CanonicalString(w, x.Message); w.Write((byte)x.MessageDisposition);
			CanonicalString(w, x.Deed); w.Write((byte)x.DeedDisposition);
			CanonicalString(w, x.GuestbookLine); w.Write((byte)x.GuestbookDisposition);
		}

		private static bool ProofListValid(KingdomLifecycleBook Book)
		{
			if (Book == null || Book.RecentProofs == null
				|| Book.RecentProofs.Count > MaxRecentProofs) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> coordinates = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Book.RecentProofs.Count; i++)
			{
				KingdomLifecycleProof p = Book.RecentProofs[i];
				if (p == null || p.Sequence <= 0L || !ActionAllowedInLane(p.Action, p.Lane)
					|| p.Sequence > GetRetiredThrough(Book, p.Lane) || p.Tick < 0L
					|| !string.Equals(p.Id, OperationId(Book.SettlementId, p.Lane, p.Sequence),
						StringComparison.Ordinal)
					|| !ValidHashNamespace(p.PlanHash, "plan") || !ids.Add(p.Id)
					|| !coordinates.Add(((byte)p.Lane).ToString(CultureInfo.InvariantCulture)
						+ ":" + p.Sequence.ToString(CultureInfo.InvariantCulture))) return false;
			}
			return true;
		}

		private static bool CarryProofListValid(KingdomCarryBook Book)
		{
			if (Book == null || Book.RecentProofs == null
				|| Book.RecentProofs.Count > MaxRecentProofs) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			HashSet<long> sequences = new HashSet<long>();
			for (int i = 0; i < Book.RecentProofs.Count; i++)
			{
				KingdomLifecycleProof p = Book.RecentProofs[i];
				if (p == null || p.Sequence <= 0L || p.Sequence > Book.RetiredThrough
					|| p.Lane != KingdomLifecycleLane.None || p.Action != KingdomLifecycleAction.None
					|| p.Tick < 0L || !string.Equals(p.Id, CarryId(Book.RealmId, p.Sequence),
						StringComparison.Ordinal) || !ValidHashNamespace(p.PlanHash, "carry-plan")
					|| !ids.Add(p.Id) || !sequences.Add(p.Sequence)) return false;
			}
			return true;
		}

		private static bool ResourceRegistryValid(KingdomLifecycleBook Book)
		{
			if (Book == null || Book.Resources == null || Book.Resources.Count > MaxResourceRows)
				return false;
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Book.Resources.Count; i++)
			{
				KingdomLifecycleResourceRevision row = Book.Resources[i];
				if (!ResourceShape(row) || !keys.Add(row.Key)) return false;
			}
			return true;
		}

		private static bool CarryResourceRegistryValid(KingdomCarryBook Book)
		{
			if (Book == null || Book.Resources == null || Book.Resources.Count > MaxResourceRows)
				return false;
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Book.Resources.Count; i++)
				if (!ResourceShape(Book.Resources[i]) || !keys.Add(Book.Resources[i].Key))
					return false;
			return true;
		}

		private static bool CarryActiveResourcesValid(KingdomCarryBook Book)
		{
			if (!CarryResourceRegistryValid(Book)) return false;
			for (int i = 0; i < Book.Resources.Count; i++)
			{
				KingdomLifecycleResourceRevision row = Book.Resources[i];
				if (row.ActiveOperationId == null) continue;
				if (Book.Open == null || !string.Equals(row.ActiveOperationId,
					Book.Open.Id, StringComparison.Ordinal)
					|| !string.Equals(row.Key, Book.Open.ScheduleLease == null
						? null : Book.Open.ScheduleLease.Key, StringComparison.Ordinal)
					|| !ResourceWitnessMatches(row, Book.Open.ScheduleLease)) return false;
			}
			if (Book.Open == null) return true;
			return Book.Open.ScheduleLease != null
				&& ResourceWitnessMatches(FindResource(Book, Book.Open.ScheduleLease.Key),
					Book.Open.ScheduleLease);
		}

		private static bool ActiveResourcesValid(KingdomLifecycleBook Book)
		{
			if (!ResourceRegistryValid(Book)) return false;
			for (int i = 0; i < Book.Resources.Count; i++)
			{
				KingdomLifecycleResourceRevision row = Book.Resources[i];
				if (row.ActiveOperationId == null) continue;
				KingdomLifecycleOperation op = FindOpenOperation(Book, row.ActiveOperationId);
				KingdomLifecycleResourceLease lease = op == null ? null : FindLease(op, row.Key);
				if (lease == null || !ResourceWitnessMatches(row, lease)) return false;
			}
			return OperationResourcesValid(Book, Book.PlainGuest)
				&& OperationResourcesValid(Book, Book.NotableGuest)
				&& OperationResourcesValid(Book, Book.Raid)
				&& OperationResourcesValid(Book, Book.Petition);
		}

		private static bool OperationResourcesValid(KingdomLifecycleBook book,
			KingdomLifecycleOperation operation)
		{
			if (operation == null) return true;
			if (operation.ResourceLeases == null) return false;
			for (int i = 0; i < operation.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = operation.ResourceLeases[i];
				KingdomLifecycleResourceRevision row = lease == null
					? null : FindResource(book, lease.Key);
				if (!LeaseStateAllowedAtPhase(operation, lease)
					|| !ResourceWitnessMatches(row, lease)) return false;
			}
			return true;
		}

		private static bool ResourceWitnessMatches(KingdomLifecycleResourceRevision row,
			KingdomLifecycleResourceLease lease)
		{
			if (!ResourceMatches(row, lease)
				|| !string.Equals(row.ActiveOperationId, lease.OperationId,
					StringComparison.Ordinal)) return false;
			if (lease.State == KingdomLifecycleLeaseState.Prepared
				|| lease.State == KingdomLifecycleLeaseState.Intent)
				return row.Revision == lease.BeforeRevision
					&& !string.Equals(row.LastOperationId, lease.OperationId,
						StringComparison.Ordinal);
			if (lease.State == KingdomLifecycleLeaseState.Proved)
				return row.Revision == lease.AfterRevision
					&& string.Equals(row.LastOperationId, lease.OperationId,
						StringComparison.Ordinal);
			return false;
		}

		private static KingdomLifecycleOperation FindOpenOperation(KingdomLifecycleBook Book,
			string Id)
		{
			if (Book == null || string.IsNullOrEmpty(Id)) return null;
			if (Book.PlainGuest != null && Book.PlainGuest.Id == Id) return Book.PlainGuest;
			if (Book.NotableGuest != null && Book.NotableGuest.Id == Id) return Book.NotableGuest;
			if (Book.Raid != null && Book.Raid.Id == Id) return Book.Raid;
			if (Book.Petition != null && Book.Petition.Id == Id) return Book.Petition;
			return null;
		}

		private static KingdomLifecycleResourceRevision FindResource(KingdomLifecycleBook Book,
			string Key)
		{
			if (Book == null || Book.Resources == null || Key == null) return null;
			for (int i = 0; i < Book.Resources.Count; i++)
				if (Book.Resources[i] != null && Book.Resources[i].Key == Key) return Book.Resources[i];
			return null;
		}

		private static KingdomLifecycleResourceRevision FindResource(KingdomCarryBook Book,
			string Key)
		{
			if (Book == null || Book.Resources == null || Key == null) return null;
			for (int i = 0; i < Book.Resources.Count; i++)
				if (Book.Resources[i] != null && string.Equals(Book.Resources[i].Key, Key,
					StringComparison.Ordinal)) return Book.Resources[i];
			return null;
		}

		private static KingdomLifecycleResourceLease FindLease(KingdomLifecycleOperation op,
			string Key)
		{
			if (op == null || op.ResourceLeases == null || Key == null) return null;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
				if (op.ResourceLeases[i] != null && op.ResourceLeases[i].Key == Key)
					return op.ResourceLeases[i];
			return null;
		}

		private static bool HasLease(KingdomLifecycleOperation op,
			KingdomLifecycleResourceKind Kind)
		{
			if (op == null || op.ResourceLeases == null) return false;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
				if (op.ResourceLeases[i] != null && op.ResourceLeases[i].Kind == Kind) return true;
			return false;
		}

		private static bool HasDomainLease(KingdomLifecycleOperation op)
		{
			if (op == null || op.ResourceLeases == null) return false;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
				if (op.ResourceLeases[i] != null
					&& op.ResourceLeases[i].Kind != KingdomLifecycleResourceKind.Schedule
					&& op.ResourceLeases[i].Kind != KingdomLifecycleResourceKind.WaterVessel
					&& op.ResourceLeases[i].Kind != KingdomLifecycleResourceKind.Projection
					&& op.ResourceLeases[i].Kind != KingdomLifecycleResourceKind.Object) return true;
			return false;
		}

		private static bool IsDomainLease(KingdomLifecycleResourceLease lease)
		{
			return lease != null
				&& lease.Kind != KingdomLifecycleResourceKind.Schedule
				&& lease.Kind != KingdomLifecycleResourceKind.WaterVessel
				&& lease.Kind != KingdomLifecycleResourceKind.Projection
				&& lease.Kind != KingdomLifecycleResourceKind.Object;
		}

		private static KingdomLifecycleResourceLease RequiredDomainLease(
			KingdomLifecycleOperation op)
		{
			if (op == null || op.ResourceLeases == null) return null;
			KingdomLifecycleResourceLease found = null;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
				if (IsDomainLease(op.ResourceLeases[i]))
				{
					if (found != null) return null;
					found = op.ResourceLeases[i];
				}
			return found;
		}

		private static bool IsRequiredDomainLease(KingdomLifecycleOperation op,
			KingdomLifecycleResourceLease lease)
		{
			KingdomLifecycleResourceKind kind;
			long delta;
			return lease != null && TryRequiredDomain(op, out kind, out delta)
				&& lease.Kind == kind && lease.Delta == delta
				&& string.Equals(lease.ScopeId, op.SettlementId, StringComparison.Ordinal)
				&& string.Equals(lease.SubjectId, op.SettlementId, StringComparison.Ordinal)
				&& ReferenceEquals(RequiredDomainLease(op), lease);
		}

		private static bool TryRequiredDomain(KingdomLifecycleOperation op,
			out KingdomLifecycleResourceKind kind, out long delta)
		{
			kind = KingdomLifecycleResourceKind.None;
			delta = 0L;
			if (op == null) return false;
			switch (op.Action)
			{
			case KingdomLifecycleAction.Passages:
				return false;
			case KingdomLifecycleAction.Spawn:
				kind = KingdomLifecycleResourceKind.Population; delta = op.PartySize; break;
			case KingdomLifecycleAction.Depart:
				kind = KingdomLifecycleResourceKind.Population; delta = -op.Count; break;
			case KingdomLifecycleAction.OfferWater:
				kind = KingdomLifecycleResourceKind.Standing; delta = op.WaterRequested; break;
			case KingdomLifecycleAction.Lodge:
				kind = KingdomLifecycleResourceKind.Roster; delta = 1L; break;
			case KingdomLifecycleAction.RaidWarning:
			case KingdomLifecycleAction.RaidRewarning:
			case KingdomLifecycleAction.RaidTribute:
			case KingdomLifecycleAction.RaidTalkDown:
			case KingdomLifecycleAction.RaidAttack:
			case KingdomLifecycleAction.RaidCancel:
				kind = KingdomLifecycleResourceKind.Raid; delta = 1L; break;
			case KingdomLifecycleAction.PetitionOffer:
			case KingdomLifecycleAction.PetitionAccept:
			case KingdomLifecycleAction.PetitionDecline:
			case KingdomLifecycleAction.PetitionResolve:
			case KingdomLifecycleAction.PetitionExpire:
				kind = KingdomLifecycleResourceKind.Petition; delta = 1L; break;
			default:
				return false;
			}
			return delta != 0L;
		}

		private static bool RequiresDomainLease(KingdomLifecycleAction action)
		{
			return action != KingdomLifecycleAction.Passages;
		}

		private static void AppendProof(List<KingdomLifecycleProof> Proofs,
			KingdomLifecycleProof Proof)
		{
			Proofs.Add(Proof);
			while (Proofs.Count > MaxRecentProofs) Proofs.RemoveAt(0);
		}

		private static int IndexOfSource(KingdomCarryOperation op, KingdomCarrySource source)
		{
			if (op == null || op.Sources == null) return -1;
			for (int i = 0; i < op.Sources.Count; i++) if (ReferenceEquals(op.Sources[i], source)) return i;
			return -1;
		}

		private static int IndexOfOutput(KingdomCarryOperation op,
			KingdomLifecycleProjection output)
		{
			if (op == null || op.Outputs == null) return -1;
			for (int i = 0; i < op.Outputs.Count; i++)
				if (ReferenceEquals(op.Outputs[i], output)) return i;
			return -1;
		}

		private static int FirstIncompleteSource(KingdomCarryOperation op)
		{
			if (op == null || op.Sources == null) return 0;
			for (int i = 0; i < op.Sources.Count; i++)
				if (op.Sources[i] == null
					|| op.Sources[i].State != KingdomLifecyclePhysicalState.Proved) return i;
			return op.Sources.Count;
		}

		private static KingdomLifecycleOperation GetSlot(KingdomLifecycleBook Book,
			KingdomLifecycleLane Lane)
		{
			if (Book == null) return null;
			switch (Lane)
			{
			case KingdomLifecycleLane.PlainGuest: return Book.PlainGuest;
			case KingdomLifecycleLane.NotableGuest: return Book.NotableGuest;
			case KingdomLifecycleLane.Raid: return Book.Raid;
			case KingdomLifecycleLane.Petition: return Book.Petition;
			default: return null;
			}
		}

		private static void SetSlot(KingdomLifecycleBook Book, KingdomLifecycleLane Lane,
			KingdomLifecycleOperation Operation)
		{
			switch (Lane)
			{
			case KingdomLifecycleLane.PlainGuest: Book.PlainGuest = Operation; break;
			case KingdomLifecycleLane.NotableGuest: Book.NotableGuest = Operation; break;
			case KingdomLifecycleLane.Raid: Book.Raid = Operation; break;
			case KingdomLifecycleLane.Petition: Book.Petition = Operation; break;
			}
		}

		private static long GetNextSequence(KingdomLifecycleBook Book,
			KingdomLifecycleLane Lane)
		{
			switch (Lane)
			{
			case KingdomLifecycleLane.PlainGuest: return Book.PlainGuestNextSequence;
			case KingdomLifecycleLane.NotableGuest: return Book.NotableGuestNextSequence;
			case KingdomLifecycleLane.Raid: return Book.RaidNextSequence;
			case KingdomLifecycleLane.Petition: return Book.PetitionNextSequence;
			default: return 0L;
			}
		}

		private static void SetNextSequence(KingdomLifecycleBook Book,
			KingdomLifecycleLane Lane, long Value)
		{
			switch (Lane)
			{
			case KingdomLifecycleLane.PlainGuest: Book.PlainGuestNextSequence = Value; break;
			case KingdomLifecycleLane.NotableGuest: Book.NotableGuestNextSequence = Value; break;
			case KingdomLifecycleLane.Raid: Book.RaidNextSequence = Value; break;
			case KingdomLifecycleLane.Petition: Book.PetitionNextSequence = Value; break;
			}
		}

		private static long GetRetiredThrough(KingdomLifecycleBook Book,
			KingdomLifecycleLane Lane)
		{
			switch (Lane)
			{
			case KingdomLifecycleLane.PlainGuest: return Book.PlainGuestRetiredThrough;
			case KingdomLifecycleLane.NotableGuest: return Book.NotableGuestRetiredThrough;
			case KingdomLifecycleLane.Raid: return Book.RaidRetiredThrough;
			case KingdomLifecycleLane.Petition: return Book.PetitionRetiredThrough;
			default: return long.MaxValue;
			}
		}

		private static void SetRetiredThrough(KingdomLifecycleBook Book,
			KingdomLifecycleLane Lane, long Value)
		{
			switch (Lane)
			{
			case KingdomLifecycleLane.PlainGuest: Book.PlainGuestRetiredThrough = Value; break;
			case KingdomLifecycleLane.NotableGuest: Book.NotableGuestRetiredThrough = Value; break;
			case KingdomLifecycleLane.Raid: Book.RaidRetiredThrough = Value; break;
			case KingdomLifecycleLane.Petition: Book.PetitionRetiredThrough = Value; break;
			}
		}

		private static bool CanonicalOperationId(KingdomLifecycleOperation Operation)
		{
			return Operation != null && Operation.Sequence > 0L
				&& ValidRootId(Operation.SettlementId)
				&& string.Equals(Operation.Id,
					OperationId(Operation.SettlementId, Operation.Lane, Operation.Sequence),
					StringComparison.Ordinal);
		}

		private static bool CounterShape(long Next, long Retired)
		{
			return Next > 0L && Retired >= 0L && Retired < long.MaxValue && Next > Retired;
		}

		private static bool KnownAction(KingdomLifecycleAction Action)
		{
			return Enum.IsDefined(typeof(KingdomLifecycleAction), Action)
				&& Action != KingdomLifecycleAction.None;
		}

		private static bool KnownPhase(KingdomLifecyclePhase Phase)
		{
			return Enum.IsDefined(typeof(KingdomLifecyclePhase), Phase)
				&& Phase != KingdomLifecyclePhase.Invalid;
		}

		private static bool KnownPhysical(KingdomLifecyclePhysicalState State)
		{
			return Enum.IsDefined(typeof(KingdomLifecyclePhysicalState), State);
		}

		private static bool KnownSink(KingdomLifecycleSinkState State)
		{
			return Enum.IsDefined(typeof(KingdomLifecycleSinkState), State);
		}

		private static bool KnownDisposition(KingdomLifecycleSinkDisposition Disposition)
		{
			return Disposition == KingdomLifecycleSinkDisposition.Deliver
				|| Disposition == KingdomLifecycleSinkDisposition.Skip;
		}

		private static bool KnownOption(KingdomLifecycleOptionState State)
		{
			return Enum.IsDefined(typeof(KingdomLifecycleOptionState), State);
		}

		private static bool KnownResourceKind(KingdomLifecycleResourceKind Kind)
		{
			return Enum.IsDefined(typeof(KingdomLifecycleResourceKind), Kind)
				&& Kind != KingdomLifecycleResourceKind.None;
		}

		private static bool KnownOuterResourceKind(KingdomLifecycleResourceKind Kind)
		{
			return KnownResourceKind(Kind) &&
				(byte)Kind <= (byte)KingdomLifecycleResourceKind.Raid;
		}

		private static bool IsDomainResourceKind(KingdomLifecycleResourceKind Kind)
		{
			return KnownOuterResourceKind(Kind)
				&& Kind != KingdomLifecycleResourceKind.Schedule
				&& Kind != KingdomLifecycleResourceKind.WaterVessel
				&& Kind != KingdomLifecycleResourceKind.Object
				&& Kind != KingdomLifecycleResourceKind.Projection;
		}

		public static bool CanOwnGrowthAuthority(KingdomLifecycleBook Parent)
		{
			return Parent != null && CanOwnAuthority(Parent) && Parent.Growth != null &&
				CanOwnGrowthAuthority(Parent.Growth, Parent.SettlementId);
		}

		public static bool CanOwnGrowthAuthority(KingdomGrowthBook Book, string SettlementId)
		{
			return Book != null && !Book.Quarantined && Book.OpaquePayload == null &&
				Book.FormatVersion == CurrentGrowthFormatVersion && !Book.MigrationPending &&
				ValidRootId(SettlementId) && Book.IdentityBound &&
				string.Equals(Book.SettlementId, SettlementId, StringComparison.Ordinal) &&
				string.Equals(Book.IdentityProof, GrowthIdentityProof(SettlementId),
					StringComparison.Ordinal) && GrowthRootShape(Book, ValidateOperations: true)
				&& KingdomLifecycleWireCodec.GrowthPayloadFitsAggregateCap(Book);
		}

		/// <summary>Structural writer gate. Opaque bytes are exact evidence; parsed current
		/// envelopes must be canonical authority, a staged v5 migration, or a canonical empty
		/// quarantine. Programmatic malformed state is refused rather than truncated.</summary>
		public static bool GrowthEnvelopeWritable(KingdomGrowthBook Book)
		{
			if (Book == null || Book.FormatVersion != CurrentGrowthFormatVersion ||
				TooLong(Book.Fault, MaxTextChars)) return false;
			if (Book.OpaquePayload != null)
				return KingdomLifecycleWireCodec.OpaqueGrowthEnvelopeWritable(Book);
			if (Book.OpaqueWireVersion != 0 || !GrowthCollectionsBounded(Book)) return false;
			bool shape = Book.MigrationPending ? StagedGrowthShape(Book)
				: Book.Quarantined ? CanonicalQuarantinedGrowth(Book)
					: GrowthRootShape(Book, ValidateOperations: true);
			return shape && KingdomLifecycleWireCodec.GrowthPayloadFitsAggregateCap(Book);
		}

		/// <summary>Validates the exact v5 outer graph before installing a dormant staged
		/// Growth book. No clock or pending-crop authority is fabricated here.</summary>
		public static bool TryStageGrowthMigrationFromV5(KingdomLifecycleBook Book,
			out KingdomGrowthBook Staged)
		{
			Staged = null;
			if (Book == null || Book.FormatVersion != LegacyLifecycleFormatVersion ||
				Book.WireRejected || !LegacyResourceKindsOnly(Book)) return false;
			int version = Book.FormatVersion;
			KingdomGrowthBook prior = Book.Growth;
			KingdomGrowthBook staged = NewStagedGrowth();
			Book.FormatVersion = CurrentFormatVersion;
			bool unbound = !Book.IdentityBound;
			Book.Growth = unbound ? new KingdomGrowthBook() : staged;
			bool valid = PristineLifecycleBook(Book) ||
				CanonicalLifecycleQuarantine(Book) ||
				(!Book.Quarantined && ValidRootId(Book.SettlementId) && Book.IdentityBound &&
				 ExactSettlementIdentityProof(Book) && LifecycleBookShape(Book));
			Book.FormatVersion = version;
			Book.Growth = prior;
			if (!valid) return false;
			Staged = unbound ? new KingdomGrowthBook() : staged;
			return true;
		}

		public static bool StageLegacyGrowthMigration(KingdomLifecycleBook Book)
		{
			if (!TryStageGrowthMigrationFromV5(Book, out KingdomGrowthBook staged)) return false;
			Book.FormatVersion = CurrentFormatVersion;
			Book.Growth = staged;
			return true;
		}

		public static KingdomGrowthMigrationResult ApplyGrowthMigration(
			KingdomLifecycleBook Parent, KingdomGrowthMigrationInput Input)
		{
			KingdomGrowthMigrationResult result = new KingdomGrowthMigrationResult
			{
				Failure = "growth migration input is invalid"
			};
			if (Parent == null || !CanOwnAuthority(Parent) ||
				!ValidRootId(Parent.SettlementId) || !Parent.IdentityBound ||
				!ExactSettlementIdentityProof(Parent) || Parent.Growth == null ||
				!StagedGrowthShape(Parent.Growth) || Input == null || !Input.HasNow ||
				Input.Now < 0L || Input.ArrivalIntervalTicks <= 0L ||
				!ValidCount(Input.PendingCrop) || TooLong(Input.PendingCropBlueprint, MaxNameChars) ||
				TooLong(Input.PendingCropZoneId, MaxNameChars)
				|| (Input.PendingCrop == 0 ? (!string.IsNullOrEmpty(Input.PendingCropBlueprint)
					|| !string.IsNullOrEmpty(Input.PendingCropZoneId))
					: (!ValidName(Input.PendingCropBlueprint) || !ValidName(Input.PendingCropZoneId))))
				return result;
			long arrival;
			if (!CheckedAdd(Input.Now, Input.ArrivalIntervalTicks, out arrival))
			{
				result.Failure = "growth arrival migration clock overflowed";
				return result;
			}
			KingdomGrowthBook growth = NewBoundGrowth(Parent.SettlementId);
			if (growth == null) return result;
			growth.MigratedFromLifecycleVersion = LegacyLifecycleFormatVersion;
			growth.MigrationTick = Input.Now;
			growth.OptionState = Input.OptionEnabled ? KingdomLifecycleOptionState.Enabled :
				KingdomLifecycleOptionState.Disabled;
			growth.OptionTick = Input.Now;
			growth.HealthState = Input.Healthy ? KingdomGrowthHealthState.Healthy :
				KingdomGrowthHealthState.Unhealthy;
			growth.HealthTick = Input.Now;
			growth.ScarcityOptionState = Input.ScarcityEnabled
				? KingdomLifecycleOptionState.Enabled : KingdomLifecycleOptionState.Disabled;
			growth.ScarcityOptionTick = Input.Now;
			growth.WorkPaused = !Input.OptionEnabled || !Input.Healthy;
			growth.WorkPauseStartedTick = growth.WorkPaused ? Input.Now : 0L;
			growth.WorkPausedTicks = 0L;
			growth.EffectiveWorkTick = Input.Now;
			growth.LastHeartbeatTick = Input.Now;
			growth.NextArrivalTick = growth.WorkPaused ? 0L : arrival;
			growth.ArrivalIntervalTicks = Input.ArrivalIntervalTicks;
			growth.LastFetchTick = Input.Now;
			growth.LastMillTick = Input.Now;
			growth.LastSubsidenceTick = Input.Now;
			growth.PendingCrop = Input.PendingCrop;
			growth.PendingCropBlueprint = Input.PendingCrop == 0 ? null : Input.PendingCropBlueprint;
			growth.PendingCropZoneId = Input.PendingCrop == 0 ? null : Input.PendingCropZoneId;
			if (!CanOwnGrowthAuthority(growth, Parent.SettlementId))
			{
				result.Failure = "detached growth migration result is malformed";
				return result;
			}
			result.Valid = true;
			result.Failure = null;
			result.Growth = growth;
			return result;
		}

		public static bool TryPublishGrowthMigration(KingdomLifecycleBook Parent,
			KingdomGrowthMigrationResult Result)
		{
			if (Parent == null || !CanOwnAuthority(Parent) || Result == null
				|| !Result.Valid || Result.Growth == null ||
				!StagedGrowthShape(Parent.Growth) || !ValidRootId(Parent.SettlementId) ||
				!ExactSettlementIdentityProof(Parent) ||
				!CanOwnGrowthAuthority(Result.Growth, Parent.SettlementId)) return false;
			KingdomGrowthBook detached;
			try
			{
				detached = KingdomLifecycleWireCodec.ReadGrowthPayload(
					KingdomLifecycleWireCodec.GrowthPayloadForWrite(Result.Growth));
			}
			catch (Exception) { return false; }
			if (!CanOwnGrowthAuthority(detached, Parent.SettlementId)) return false;
			Parent.Growth = detached;
			return true;
		}

		public static KingdomGrowthAvailabilityDecision ObserveGrowthAvailability(
			KingdomGrowthBook Book, bool OptionEnabled, bool Healthy, long Now,
			long CurrentArrivalIntervalTicks)
		{
			KingdomGrowthAvailabilityDecision result = new KingdomGrowthAvailabilityDecision
			{
				Failure = "growth availability observation is malformed",
				ReconcileOpen = HasOpenGrowthOperation(Book)
			};
			if (Book == null || Book.Quarantined || Book.MigrationPending || Now < 0L ||
				CurrentArrivalIntervalTicks <= 0L || Book.OptionTick < 0L || Book.HealthTick < 0L ||
				Book.EffectiveWorkTick < 0L || Book.WorkPausedTicks < 0L ||
				!KnownOption(Book.OptionState) || !KnownGrowthHealth(Book.HealthState)
				|| Now < Book.OptionTick || Now < Book.HealthTick || Now < Book.EffectiveWorkTick
				|| Now < Book.LastHeartbeatTick || Now < Book.LastFetchTick
				|| Now < Book.LastMillTick || Now < Book.LastSubsidenceTick
				|| Now < Book.MigrationTick) return result;
			bool active = OptionEnabled && Healthy;
			bool wasActive = Book.OptionState == KingdomLifecycleOptionState.Enabled &&
				Book.HealthState == KingdomGrowthHealthState.Healthy && !Book.WorkPaused;
			long paused = Book.WorkPausedTicks;
			if (Book.WorkPaused && Book.WorkPauseStartedTick > Now) return result;
			if (Book.WorkPaused && active &&
				!CheckedAdd(paused, Now - Book.WorkPauseStartedTick, out paused)) return result;
			long nextArrival = Book.NextArrivalTick;
			bool restamp = active != wasActive || Book.OptionState == KingdomLifecycleOptionState.Unknown ||
				Book.HealthState == KingdomGrowthHealthState.Unknown;
			bool openArrival = Book.ArrivalOp != null || Book.ArrivalCandidate != null;
			if (!active) nextArrival = openArrival ? Book.NextArrivalTick : 0L;
			else if (restamp && !openArrival
				&& !CheckedAdd(Now, CurrentArrivalIntervalTicks, out nextArrival))
				return result;
			long effectiveAnchor = active ? Now : (Book.WorkPaused ?
				Book.WorkPauseStartedTick : Now);
			if (effectiveAnchor < paused) return result;
			long effectiveNow = effectiveAnchor - paused;
			result.Valid = true; result.Failure = null; result.AllowStarters = active;
			result.OptionState = OptionEnabled ? KingdomLifecycleOptionState.Enabled :
				KingdomLifecycleOptionState.Disabled;
			result.HealthState = Healthy ? KingdomGrowthHealthState.Healthy :
				KingdomGrowthHealthState.Unhealthy;
			result.ObservedTick = Now; result.WorkPaused = !active;
			result.PauseStartedTick = active ? 0L : (Book.WorkPaused ?
				Book.WorkPauseStartedTick : Now);
			result.PausedTicks = paused; result.EffectiveWorkTick = restamp ? effectiveNow :
				Book.EffectiveWorkTick; result.RestampClocks = restamp;
			result.NextArrivalTick = nextArrival;
			result.ArrivalIntervalTicks = CurrentArrivalIntervalTicks;
			return result;
		}

		public static bool ApplyGrowthAvailability(KingdomGrowthBook Book,
			KingdomGrowthAvailabilityDecision Decision)
		{
			if (Book == null || Decision == null || !Decision.Valid ||
				!CanOwnGrowthAuthority(Book, Book.SettlementId)) return false;
			KingdomGrowthAvailabilityDecision expected = ObserveGrowthAvailability(Book,
				Decision.OptionState == KingdomLifecycleOptionState.Enabled,
				Decision.HealthState == KingdomGrowthHealthState.Healthy,
				Decision.ObservedTick, Decision.ArrivalIntervalTicks);
			if (!expected.Valid || expected.AllowStarters != Decision.AllowStarters
				|| expected.ReconcileOpen != Decision.ReconcileOpen
				|| expected.OptionState != Decision.OptionState
				|| expected.HealthState != Decision.HealthState
				|| expected.WorkPaused != Decision.WorkPaused
				|| expected.PauseStartedTick != Decision.PauseStartedTick
				|| expected.PausedTicks != Decision.PausedTicks
				|| expected.EffectiveWorkTick != Decision.EffectiveWorkTick
				|| expected.RestampClocks != Decision.RestampClocks
				|| expected.NextArrivalTick != Decision.NextArrivalTick) return false;
			KingdomLifecycleOptionState oldOption = Book.OptionState;
			long oldOptionTick = Book.OptionTick;
			KingdomGrowthHealthState oldHealth = Book.HealthState;
			long oldHealthTick = Book.HealthTick;
			bool oldPaused = Book.WorkPaused; long oldPauseStart = Book.WorkPauseStartedTick;
			long oldPausedTicks = Book.WorkPausedTicks; long oldEffective = Book.EffectiveWorkTick;
			long oldArrival = Book.NextArrivalTick; long oldInterval = Book.ArrivalIntervalTicks;
			Book.OptionState = Decision.OptionState; Book.OptionTick = Decision.ObservedTick;
			Book.HealthState = Decision.HealthState; Book.HealthTick = Decision.ObservedTick;
			Book.WorkPaused = Decision.WorkPaused;
			Book.WorkPauseStartedTick = Decision.PauseStartedTick;
			Book.WorkPausedTicks = Decision.PausedTicks;
			Book.EffectiveWorkTick = Decision.EffectiveWorkTick;
			Book.NextArrivalTick = Decision.NextArrivalTick;
			Book.ArrivalIntervalTicks = Decision.ArrivalIntervalTicks;
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			Book.OptionState = oldOption; Book.OptionTick = oldOptionTick;
			Book.HealthState = oldHealth; Book.HealthTick = oldHealthTick;
			Book.WorkPaused = oldPaused; Book.WorkPauseStartedTick = oldPauseStart;
			Book.WorkPausedTicks = oldPausedTicks; Book.EffectiveWorkTick = oldEffective;
			Book.NextArrivalTick = oldArrival; Book.ArrivalIntervalTicks = oldInterval;
			return false;
		}

		public static bool TryEffectiveWorkElapsed(KingdomGrowthBook Book, long Now,
			out long Elapsed)
		{
			Elapsed = 0L;
			if (Book == null || Book.WorkPaused ||
				Book.OptionState != KingdomLifecycleOptionState.Enabled ||
				Book.HealthState != KingdomGrowthHealthState.Healthy) return false;
			long effectiveNow;
			if (!TryGrowthEffectiveNow(Book, Now, out effectiveNow)
				|| effectiveNow < Book.EffectiveWorkTick) return false;
			Elapsed = effectiveNow - Book.EffectiveWorkTick;
			return true;
		}

		public static bool TryObserveGrowthScarcityOption(KingdomGrowthBook Book,
			bool Enabled, long Tick)
		{
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| Tick < Book.ScarcityOptionTick || Tick < Book.MigrationTick) return false;
			KingdomLifecycleOptionState beforeState = Book.ScarcityOptionState;
			long beforeTick = Book.ScarcityOptionTick;
			Book.ScarcityOptionState = Enabled ? KingdomLifecycleOptionState.Enabled
				: KingdomLifecycleOptionState.Disabled;
			Book.ScarcityOptionTick = Tick;
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			Book.ScarcityOptionState = beforeState;
			Book.ScarcityOptionTick = beforeTick;
			return false;
		}

		private static bool TryGrowthEffectiveNow(KingdomGrowthBook Book, long Now,
			out long EffectiveNow)
		{
			EffectiveNow = 0L;
			if (Book == null || Now < 0L || Book.WorkPausedTicks < 0L
				|| (Book.WorkPaused && (Book.WorkPauseStartedTick < 0L
					|| Now < Book.WorkPauseStartedTick))) return false;
			long anchor = Book.WorkPaused ? Book.WorkPauseStartedTick : Now;
			if (anchor < Book.WorkPausedTicks) return false;
			EffectiveNow = anchor - Book.WorkPausedTicks;
			return true;
		}

		private static bool GrowthEffectiveWorkBounded(KingdomGrowthBook book)
		{
			if (book == null || book.FieldOps == null) return false;
			long observationTick = Math.Max(book.OptionTick, book.HealthTick);
			long ceiling;
			if (!TryGrowthEffectiveNow(book, observationTick, out ceiling)) return false;
			for (int i = 0; i < book.FieldOps.Count; i++)
			{
				KingdomGrowthFieldSlot field = book.FieldOps[i];
				if (field == null || field.ClockTick < 0L) return false;
				if (field.ClockTick > ceiling) ceiling = field.ClockTick;
			}
			return book.EffectiveWorkTick <= ceiling;
		}

		public static string GrowthOperationId(string SettlementId, KingdomGrowthSlotKind Slot,
			string FieldId, long Sequence)
		{
			if (Slot != KingdomGrowthSlotKind.Field && FieldId != null && FieldId.Length == 0)
				FieldId = null;
			if (!ValidRootId(SettlementId) || !KnownGrowthSlot(Slot) || Sequence <= 0L
				|| (Slot == KingdomGrowthSlotKind.Field ? !ValidRootId(FieldId)
					: FieldId != null)) return null;
			return HashId("growth-operation", delegate(BinaryWriter w)
			{
				CanonicalString(w, SettlementId); w.Write((byte)Slot);
				CanonicalString(w, FieldId); w.Write(Sequence);
			});
		}

		public static string GrowthArrivalCandidateId(string SettlementId, long Sequence)
		{
			if (!ValidRootId(SettlementId) || Sequence <= 0L) return null;
			return HashId("growth-arrival-candidate", delegate(BinaryWriter w)
			{
				CanonicalString(w, SettlementId); w.Write(Sequence);
			});
		}

		public static bool TryGrowthArrivalCandidatePlanHash(
			KingdomGrowthArrivalCandidate Candidate, out string Hash)
		{
			string baseHash;
			if (!TryGrowthArrivalCandidateBasePlanHash(Candidate, out baseHash))
			{
				Hash = null;
				return false;
			}
			KingdomGrowthArrivalCandidatePhase phase = Candidate.Phase ==
				KingdomGrowthArrivalCandidatePhase.Quarantined
					? Candidate.EvidencePhase : Candidate.Phase;
			if (phase == KingdomGrowthArrivalCandidatePhase.Prepared
				|| phase == KingdomGrowthArrivalCandidatePhase.CreateIntent
				|| phase == KingdomGrowthArrivalCandidatePhase.Escrowed)
			{
				Hash = baseHash;
				return true;
			}
			if (phase == KingdomGrowthArrivalCandidatePhase.LodgingIntent)
				return TryGrowthArrivalLodgingIntentPlanHash(Candidate, baseHash, out Hash);
			string observedHash;
			if (!TryGrowthArrivalObservedPlanHash(Candidate, baseHash, out observedHash))
			{
				Hash = null;
				return false;
			}
			if (phase == KingdomGrowthArrivalCandidatePhase.Observed)
			{
				Hash = observedHash;
				return true;
			}
			return TryGrowthArrivalDispositionPlanHash(Candidate, observedHash, out Hash);
		}

		private static bool TryGrowthArrivalLodgingIntentPlanHash(
			KingdomGrowthArrivalCandidate Candidate, string BaseHash, out string Hash)
		{
			try
			{
				Hash = HashId("growth-arrival-candidate-plan", delegate(BinaryWriter w)
				{
					CanonicalString(w, BaseHash); CanonicalString(w, "lodging-intent");
					CanonicalString(w, Candidate.ObjectId);
					CanonicalString(w, Candidate.LodgingZoneId);
					w.Write(Candidate.LodgingX); w.Write(Candidate.LodgingY);
					CanonicalString(w, Candidate.LodgingBeforeGraphHash);
					CanonicalString(w, Candidate.LodgingReceiptId);
				});
				return ValidHashNamespace(Hash, "growth-arrival-candidate-plan");
			}
			catch (Exception) { Hash = null; return false; }
		}

		private static bool TryGrowthArrivalObservedPlanHash(
			KingdomGrowthArrivalCandidate Candidate, string BaseHash, out string Hash)
		{
			Hash = null;
			if (Candidate == null || Candidate.LodgingState !=
				KingdomLifecyclePhysicalState.Proved) return false;
			try
			{
				Hash = HashId("growth-arrival-candidate-plan", delegate(BinaryWriter w)
				{
					CanonicalString(w, BaseHash); CanonicalString(w, "observed");
					CanonicalString(w, Candidate.ObjectId);
					CanonicalString(w, Candidate.LodgingZoneId);
					w.Write(Candidate.LodgingX); w.Write(Candidate.LodgingY);
					w.Write((byte)Candidate.Disposition);
					w.Write((byte)Candidate.RefusalReason);
					CanonicalString(w, Candidate.LodgingBeforeGraphHash);
					CanonicalString(w, Candidate.LodgingDeclaredGraphHash);
					CanonicalString(w, Candidate.LodgingReceiptGraphHash);
					CanonicalString(w, Candidate.LodgingCallbackReferenceHash);
					w.Write(Candidate.LodgingSameReference);
					CanonicalString(w, Candidate.LodgingReceiptId);
				});
				return ValidHashNamespace(Hash, "growth-arrival-candidate-plan");
			}
			catch (Exception) { Hash = null; return false; }
		}

		private static bool TryGrowthArrivalDispositionPlanHash(
			KingdomGrowthArrivalCandidate Candidate, string ObservedHash, out string Hash)
		{
			Hash = null;
			KingdomGrowthObjectCallbackStep step = Candidate == null
				? null : Candidate.DispositionStep;
			if (step == null || string.IsNullOrEmpty(Candidate.ConsumingOperationId)
				|| Candidate.ConsumingOperationSequence <= 0L) return false;
			try
			{
				Hash = HashId("growth-arrival-candidate-plan", delegate(BinaryWriter w)
				{
					CanonicalString(w, ObservedHash); CanonicalString(w, "disposition-intent");
					CanonicalString(w, Candidate.ConsumingOperationId);
					w.Write(Candidate.ConsumingOperationSequence);
					WriteGrowthObjectCallbackPlan(w, step);
					CanonicalString(w, step.BeforeOwnerGraphHash);
					CanonicalString(w, step.AfterOwnerGraphHash);
					CanonicalString(w, step.BeforeObjectGraphHash);
					CanonicalString(w, step.AfterObjectGraphHash);
					CanonicalString(w, step.BeforeTopologyHash);
					CanonicalString(w, step.AfterTopologyHash);
				});
				return ValidHashNamespace(Hash, "growth-arrival-candidate-plan");
			}
			catch (Exception) { Hash = null; return false; }
		}

		private static bool TryGrowthArrivalCandidateBasePlanHash(
			KingdomGrowthArrivalCandidate Candidate, out string Hash)
		{
			return TryGrowthArrivalCandidateBasePlanHashCore(Candidate, true, out Hash);
		}

		private static bool TryLegacyGrowthArrivalCandidateBasePlanHash(
			KingdomGrowthArrivalCandidate Candidate, out string Hash)
		{
			return TryGrowthArrivalCandidateBasePlanHashCore(Candidate, false, out Hash);
		}

		private static bool TryGrowthArrivalCandidateBasePlanHashCore(
			KingdomGrowthArrivalCandidate Candidate, bool IncludeZone, out string Hash)
		{
			Hash = null;
			if (Candidate == null || Candidate.CreateStep == null) return false;
			try
			{
				Hash = HashId("growth-arrival-candidate-plan", delegate(BinaryWriter w)
				{
					w.Write(Candidate.Sequence); CanonicalString(w, Candidate.Id);
					CanonicalString(w, Candidate.SettlementId); w.Write(Candidate.CreatedTick);
					CanonicalString(w, Candidate.Marker); CanonicalString(w, Candidate.Blueprint);
					CanonicalString(w, Candidate.EscrowKey);
					if (IncludeZone) CanonicalString(w, Candidate.LodgingZoneId);
					WriteLeasePlan(w, Candidate.CandidateLease);
					WriteLeasePlan(w, Candidate.LodgingLease);
					WriteLeasePlan(w, Candidate.EscrowLease);
					KingdomGrowthObjectCallbackStep step = Candidate.CreateStep;
					CanonicalString(w, step.EventId); w.Write((byte)step.Kind);
					w.Write((byte)step.FromLocation); w.Write((byte)step.ToLocation);
					CanonicalString(w, step.EscrowKey); w.Write(step.BeforeCount);
					w.Write(step.AfterCount); w.Write(step.NoStack);
					CanonicalString(w, step.BeforeOwnerGraphHash);
					CanonicalString(w, step.BeforeObjectGraphHash);
					CanonicalString(w, step.BeforeTopologyHash);
					CanonicalString(w, step.ReceiptId);
				});
				return ValidHashNamespace(Hash, "growth-arrival-candidate-plan");
			}
			catch (Exception) { Hash = null; return false; }
		}

		private static string GrowthArrivalLodgingProof(
			KingdomGrowthArrivalCandidate Candidate)
		{
			string baseHash;
			if (!TryGrowthArrivalCandidateBasePlanHash(Candidate, out baseHash)) return null;
			string proof = HashId("growth-arrival-lodging-proof", delegate(BinaryWriter w)
			{
				CanonicalString(w, baseHash); CanonicalString(w, Candidate.ObjectId);
				CanonicalString(w, Candidate.LodgingZoneId);
				w.Write(Candidate.LodgingX); w.Write(Candidate.LodgingY);
				w.Write((byte)Candidate.Disposition); w.Write((byte)Candidate.RefusalReason);
				CanonicalString(w, Candidate.LodgingBeforeGraphHash);
				CanonicalString(w, Candidate.LodgingReceiptGraphHash);
				CanonicalString(w, Candidate.LodgingCallbackReferenceHash);
				w.Write(Candidate.LodgingSameReference);
				CanonicalString(w, Candidate.LodgingReceiptId);
			});
			return ValidHashNamespace(proof, "growth-arrival-lodging-proof")
				? proof.Substring(proof.Length - 64) : null;
		}

		internal static bool UpgradeLegacyGrowthArrivalCandidate(
			KingdomGrowthArrivalCandidate Candidate)
		{
			if (Candidate == null) return true;
			string legacyBaseHash;
			string baseHash;
			if (!TryLegacyGrowthArrivalCandidateBasePlanHash(Candidate, out legacyBaseHash)
				|| !string.Equals(Candidate.PlanHash, legacyBaseHash, StringComparison.Ordinal)
				) return false;
			KingdomGrowthObjectCallbackStep create = Candidate.CreateStep;
			if (create != null && create.State == KingdomLifecyclePhysicalState.Proved
				&& !string.Equals(create.ReceiptProofId,
					GrowthArrivalCandidateCallbackProof(Candidate, create, 0, true),
					StringComparison.Ordinal)) return false;
			KingdomGrowthArrivalCandidatePhase phase = Candidate.Phase ==
				KingdomGrowthArrivalCandidatePhase.Quarantined
					? Candidate.EvidencePhase : Candidate.Phase;
			if (Candidate.LegacyGrowthV1UnboundZone)
			{
				return Candidate.LodgingZoneId == null
					&& (phase == KingdomGrowthArrivalCandidatePhase.Prepared
						|| phase == KingdomGrowthArrivalCandidatePhase.CreateIntent
						|| phase == KingdomGrowthArrivalCandidatePhase.Escrowed)
					&& Candidate.LodgingState == KingdomLifecyclePhysicalState.None;
			}
			if (!ValidName(Candidate.LodgingZoneId)
				|| !TryGrowthArrivalCandidateBasePlanHash(Candidate, out baseHash)) return false;
			string currentHash;
			if (Candidate.LodgingState != KingdomLifecyclePhysicalState.Proved)
			{
				if (!TryGrowthArrivalCandidatePlanHash(Candidate, out currentHash)) return false;
				Candidate.PlanHash = currentHash;
				if (create != null && create.State == KingdomLifecyclePhysicalState.Proved)
					create.ReceiptProofId = GrowthArrivalCandidateCallbackProof(
						Candidate, create, 0);
				return true;
			}
			string proof = GrowthArrivalLodgingProof(Candidate);
			if (proof == null) return false;
			if (!string.Equals(Candidate.LodgingDeclaredGraphHash,
					Candidate.LodgingReceiptGraphHash, StringComparison.Ordinal)) return false;
			KingdomGrowthObjectCallbackStep disposition = Candidate.DispositionStep;
			if (disposition != null
				&& disposition.State == KingdomLifecyclePhysicalState.Proved
				&& !string.Equals(disposition.ReceiptProofId,
					GrowthArrivalCandidateCallbackProof(Candidate, disposition, 1, true),
					StringComparison.Ordinal)) return false;
			Candidate.LodgingDeclaredGraphHash = proof;
			if (!TryGrowthArrivalCandidatePlanHash(Candidate, out currentHash)) return false;
			Candidate.PlanHash = currentHash;
			if (create != null && create.State == KingdomLifecyclePhysicalState.Proved)
				create.ReceiptProofId = GrowthArrivalCandidateCallbackProof(Candidate, create, 0);
			if (disposition != null
				&& disposition.State == KingdomLifecyclePhysicalState.Proved)
				disposition.ReceiptProofId = GrowthArrivalCandidateCallbackProof(
					Candidate, disposition, 1);
			return true;
		}

		internal static bool BindLegacyGrowthArrivalCandidateZone(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, string ZoneId, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase == KingdomGrowthArrivalCandidatePhase.Quarantined
				|| !Candidate.LegacyGrowthV1UnboundZone || Candidate.LodgingZoneId != null
				|| !ValidName(ZoneId) || Tick < Candidate.UpdatedTick
				|| Tick < Book.OptionTick || Tick < Book.HealthTick) return false;
			KingdomGrowthArrivalCandidatePhase phase = Candidate.Phase ==
				KingdomGrowthArrivalCandidatePhase.Quarantined
					? Candidate.EvidencePhase : Candidate.Phase;
			if (phase != KingdomGrowthArrivalCandidatePhase.Prepared
				&& phase != KingdomGrowthArrivalCandidatePhase.CreateIntent
				&& phase != KingdomGrowthArrivalCandidatePhase.Escrowed) return false;
			string oldHash = Candidate.PlanHash;
			long oldTick = Candidate.UpdatedTick;
			string oldProof = Candidate.CreateStep == null
				? null : Candidate.CreateStep.ReceiptProofId;
			Candidate.LodgingZoneId = ZoneId;
			Candidate.LegacyGrowthV1UnboundZone = false;
			string hash;
			if (!TryGrowthArrivalCandidatePlanHash(Candidate, out hash))
			{
				Candidate.LodgingZoneId = null;
				Candidate.LegacyGrowthV1UnboundZone = true;
				return false;
			}
			Candidate.PlanHash = hash;
			if (Candidate.CreateStep != null
				&& Candidate.CreateStep.State == KingdomLifecyclePhysicalState.Proved)
				Candidate.CreateStep.ReceiptProofId = GrowthArrivalCandidateCallbackProof(
					Candidate, Candidate.CreateStep, 0);
			Candidate.UpdatedTick = Tick;
			if (ExactGrowthArrivalCandidateAuthority(Book, Candidate)) return true;
			Candidate.LodgingZoneId = null;
			Candidate.LegacyGrowthV1UnboundZone = true;
			Candidate.PlanHash = oldHash;
			Candidate.UpdatedTick = oldTick;
			if (Candidate.CreateStep != null)
				Candidate.CreateStep.ReceiptProofId = oldProof;
			return false;
		}

		internal static bool DowngradeGrowthArrivalCandidateForV1Fixture(
			KingdomGrowthArrivalCandidate Candidate)
		{
			if (Candidate == null) return true;
			string currentHash;
			string legacyBaseHash;
			if (!TryGrowthArrivalCandidatePlanHash(Candidate, out currentHash)
				|| !string.Equals(Candidate.PlanHash, currentHash, StringComparison.Ordinal)
				|| !TryLegacyGrowthArrivalCandidateBasePlanHash(Candidate,
					out legacyBaseHash)) return false;
			if (Candidate.LodgingState == KingdomLifecyclePhysicalState.Proved)
			{
				string proof = GrowthArrivalLodgingProof(Candidate);
				if (proof == null || !string.Equals(Candidate.LodgingDeclaredGraphHash, proof,
					StringComparison.Ordinal)) return false;
				Candidate.LodgingDeclaredGraphHash = Candidate.LodgingReceiptGraphHash;
			}
			KingdomGrowthArrivalCandidatePhase phase = Candidate.Phase ==
				KingdomGrowthArrivalCandidatePhase.Quarantined
					? Candidate.EvidencePhase : Candidate.Phase;
			if (phase == KingdomGrowthArrivalCandidatePhase.Prepared
				|| phase == KingdomGrowthArrivalCandidatePhase.CreateIntent
				|| phase == KingdomGrowthArrivalCandidatePhase.Escrowed)
			{
				Candidate.LodgingZoneId = null;
				Candidate.LegacyGrowthV1UnboundZone = true;
			}
			Candidate.PlanHash = legacyBaseHash;
			KingdomGrowthObjectCallbackStep create = Candidate.CreateStep;
			if (create != null && create.State == KingdomLifecyclePhysicalState.Proved)
				create.ReceiptProofId = GrowthArrivalCandidateCallbackProof(
					Candidate, create, 0, true);
			KingdomGrowthObjectCallbackStep disposition = Candidate.DispositionStep;
			if (disposition != null
				&& disposition.State == KingdomLifecyclePhysicalState.Proved)
				disposition.ReceiptProofId = GrowthArrivalCandidateCallbackProof(
					Candidate, disposition, 1, true);
			return true;
		}

		public static KingdomGrowthArrivalCandidate PrepareGrowthArrivalCandidate(
			KingdomGrowthBook Book, string Marker, string Blueprint, string EscrowKey, string ZoneId,
			long Tick, string BeforeOwnerGraphHash, string BeforeObjectGraphHash,
			string BeforeTopologyHash)
		{
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| Book.ArrivalCandidate != null || Book.ArrivalCandidateNextSequence == long.MaxValue
				|| Book.OptionState != KingdomLifecycleOptionState.Enabled
				|| Book.HealthState != KingdomGrowthHealthState.Healthy || Book.WorkPaused
				|| Tick < Book.OptionTick || Tick < Book.HealthTick || Tick < Book.ScarcityOptionTick
				|| !ValidRootId(Marker) || !ValidName(Blueprint) || !ValidRootId(EscrowKey)
				|| !ValidName(ZoneId)
				|| !GrowthWitnessHash(BeforeOwnerGraphHash)
				|| !GrowthWitnessHash(BeforeObjectGraphHash)
				|| !GrowthWitnessHash(BeforeTopologyHash)) return null;
			long sequence = Book.ArrivalCandidateNextSequence;
			if (!IsExactSuccessor(sequence, Book.ArrivalCandidateRetiredThrough)) return null;
			string id = GrowthArrivalCandidateId(Book.SettlementId, sequence);
			string candidateKey = ResourceKey(KingdomLifecycleResourceKind.GrowthArrivalCandidate,
				Book.SettlementId, id);
			string lodgingSubject = ChildId(id, "lodging-lease", 0);
			string lodgingKey = ResourceKey(KingdomLifecycleResourceKind.GrowthArrivalCandidate,
				Book.SettlementId, lodgingSubject);
			string escrowLeaseKey = ResourceKey(KingdomLifecycleResourceKind.GrowthEscrowRelease,
				Book.SettlementId, EscrowKey);
			KingdomLifecycleResourceRevision candidateRow = FindGrowthResource(Book, candidateKey);
			KingdomLifecycleResourceRevision lodgingRow = FindGrowthResource(Book, lodgingKey);
			KingdomLifecycleResourceRevision escrowRow = FindGrowthResource(Book, escrowLeaseKey);
			if (id == null || candidateKey == null || lodgingKey == null || escrowLeaseKey == null
				|| candidateRow != null && (!string.IsNullOrEmpty(candidateRow.ActiveOperationId)
					|| candidateRow.Revision == long.MaxValue)
				|| lodgingRow != null && (!string.IsNullOrEmpty(lodgingRow.ActiveOperationId)
					|| lodgingRow.Revision == long.MaxValue)
				|| escrowRow != null && (!string.IsNullOrEmpty(escrowRow.ActiveOperationId)
					|| escrowRow.Revision == long.MaxValue)) return null;
			KingdomGrowthArrivalCandidate candidate = new KingdomGrowthArrivalCandidate
			{
				Sequence = sequence, Id = id, SettlementId = Book.SettlementId,
				CreatedTick = Tick, UpdatedTick = Tick,
				Phase = KingdomGrowthArrivalCandidatePhase.Prepared,
				Marker = Marker, Blueprint = Blueprint, EscrowKey = EscrowKey,
				LodgingZoneId = ZoneId,
				CandidateLease = new KingdomLifecycleResourceLease
				{
					OperationId = id, Kind = KingdomLifecycleResourceKind.GrowthArrivalCandidate,
					ScopeId = Book.SettlementId, SubjectId = id, Key = candidateKey,
					Before = Book.ArrivalCandidateRetiredThrough, Delta = 1L, After = sequence,
					BeforeRevision = candidateRow == null ? 0L : candidateRow.Revision,
					AfterRevision = (candidateRow == null ? 0L : candidateRow.Revision) + 1L,
					State = KingdomLifecycleLeaseState.Prepared
				},
				LodgingLease = new KingdomLifecycleResourceLease
				{
					OperationId = id, Kind = KingdomLifecycleResourceKind.GrowthArrivalCandidate,
					ScopeId = Book.SettlementId, SubjectId = lodgingSubject, Key = lodgingKey,
					Before = 0L, Delta = 1L, After = 1L,
					BeforeRevision = lodgingRow == null ? 0L : lodgingRow.Revision,
					AfterRevision = (lodgingRow == null ? 0L : lodgingRow.Revision) + 1L,
					State = KingdomLifecycleLeaseState.Prepared
				},
				EscrowLease = new KingdomLifecycleResourceLease
				{
					OperationId = id, Kind = KingdomLifecycleResourceKind.GrowthEscrowRelease,
					ScopeId = Book.SettlementId, SubjectId = EscrowKey, Key = escrowLeaseKey,
					Before = 0L, Delta = 1L, After = 1L,
					BeforeRevision = escrowRow == null ? 0L : escrowRow.Revision,
					AfterRevision = (escrowRow == null ? 0L : escrowRow.Revision) + 1L,
					State = KingdomLifecycleLeaseState.Prepared
				},
				CreateStep = new KingdomGrowthObjectCallbackStep
				{
					EventId = ChildId(id, "object-callback", 0),
					Kind = KingdomGrowthObjectMutationKind.Create,
					FromLocation = KingdomGrowthLocationKind.Absent,
					ToLocation = KingdomGrowthLocationKind.Escrow, EscrowKey = EscrowKey,
					BeforeX = -1, BeforeY = -1, AfterX = -1, AfterY = -1,
					BeforeCount = 0, AfterCount = 1, NoStack = true,
					BeforeOwnerGraphHash = BeforeOwnerGraphHash,
					BeforeObjectGraphHash = BeforeObjectGraphHash,
					BeforeTopologyHash = BeforeTopologyHash,
					State = KingdomLifecyclePhysicalState.Prepared,
					ReceiptId = ChildId(id, "object-callback-receipt", 0),
					ReceiptState = KingdomLifecyclePhysicalState.Prepared
				}
			};
			return GrowthArrivalCandidateShape(Book, candidate, true) ? candidate : null;
		}

		public static bool TryPublishGrowthArrivalCandidate(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate)
		{
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| Candidate == null || Book.ArrivalCandidate != null
				|| Book.OptionState != KingdomLifecycleOptionState.Enabled
				|| Book.HealthState != KingdomGrowthHealthState.Healthy || Book.WorkPaused
				|| Candidate.CreatedTick < Book.OptionTick
				|| Candidate.CreatedTick < Book.HealthTick
				|| !GrowthArrivalCandidateShape(Book, Candidate, true)
				|| !ClaimGrowthArrivalCandidateAgainstBook(Book, Candidate)) return false;
			string hash;
			if (!TryGrowthArrivalCandidatePlanHash(Candidate, out hash)) return false;
			KingdomLifecycleResourceLease[] leases =
				{ Candidate.CandidateLease, Candidate.LodgingLease, Candidate.EscrowLease };
			KingdomLifecycleResourceRevision[] rows = new KingdomLifecycleResourceRevision[3];
			bool[] created = new bool[3];
			for (int i = 0; i < leases.Length; i++)
			{
				KingdomLifecycleResourceLease lease = leases[i];
				rows[i] = FindGrowthResource(Book, lease.Key);
				created[i] = rows[i] == null;
				if (rows[i] == null) rows[i] = new KingdomLifecycleResourceRevision
				{
					Kind = lease.Kind, ScopeId = lease.ScopeId, SubjectId = lease.SubjectId,
					Key = lease.Key, Revision = lease.BeforeRevision
				};
				if (!GrowthResourceMatches(rows[i], lease)
					|| rows[i].Revision != lease.BeforeRevision
					|| !string.IsNullOrEmpty(rows[i].ActiveOperationId)) return false;
			}
			int additions = (created[0] ? 1 : 0) + (created[1] ? 1 : 0)
				+ (created[2] ? 1 : 0);
			if (Book.Resources.Count + additions > MaxResourceRows) return false;
			string oldHash = Candidate.PlanHash;
			Candidate.PlanHash = hash;
			for (int i = 0; i < rows.Length; i++)
			{
				if (created[i]) Book.Resources.Add(rows[i]);
				rows[i].ActiveOperationId = Candidate.Id;
			}
			Book.ArrivalCandidate = Candidate;
			Book.ArrivalCandidateNextSequence = Candidate.Sequence + 1L;
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			Book.ArrivalCandidate = null; Book.ArrivalCandidateNextSequence = Candidate.Sequence;
			for (int i = rows.Length - 1; i >= 0; i--)
			{
				if (created[i]) Book.Resources.Remove(rows[i]);
				else rows[i].ActiveOperationId = null;
			}
			Candidate.PlanHash = oldHash;
			return false;
		}

		private static bool ClaimGrowthArrivalCandidateAgainstBook(KingdomGrowthBook book,
			KingdomGrowthArrivalCandidate candidate)
		{
			Dictionary<string, string> claims = new Dictionary<string, string>(StringComparer.Ordinal);
			if (!ClaimGrowthOperationIdentities(claims, book.HeartbeatOp)
				|| !ClaimGrowthOperationIdentities(claims, book.ArrivalOp)
				|| !ClaimGrowthOperationIdentities(claims, book.DepartureOp)
				|| !ClaimGrowthOperationIdentities(claims, book.DeliveryOp)
				|| !ClaimGrowthOperationIdentities(claims, book.FetchOp)
				|| !ClaimGrowthOperationIdentities(claims, book.MillOp)) return false;
			for (int i = 0; i < book.FieldOps.Count; i++)
				if (book.FieldOps[i] != null && book.FieldOps[i].Operation != null
					&& !ClaimGrowthOperationIdentities(claims, book.FieldOps[i].Operation)) return false;
			return ClaimGrowthArrivalCandidateIdentities(claims, candidate, null);
		}

		internal static bool BeginGrowthArrivalCandidateCreate(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.Prepared
				|| Tick < Candidate.UpdatedTick) return false;
			KingdomGrowthObjectCallbackStep step = Candidate.CreateStep;
			KingdomLifecyclePhysicalState oldStepState = step.State;
			KingdomLifecyclePhysicalState oldReceiptState = step.ReceiptState;
			int oldBeforeMatches = step.ReceiptBeforeMatches;
			int oldBeforeCount = step.ReceiptBeforeCount;
			string oldBeforeOwner = step.ReceiptBeforeOwnerGraphHash;
			string oldBeforeObject = step.ReceiptBeforeObjectGraphHash;
			string oldBeforeTopology = step.ReceiptBeforeTopologyHash;
			KingdomLifecycleLeaseState oldLeaseState = Candidate.CandidateLease.State;
			KingdomGrowthArrivalCandidatePhase oldPhase = Candidate.Phase;
			long oldTick = Candidate.UpdatedTick;
			step.State = KingdomLifecyclePhysicalState.Intent;
			step.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			step.ReceiptBeforeMatches = 0; step.ReceiptBeforeCount = 0;
			step.ReceiptBeforeOwnerGraphHash = step.BeforeOwnerGraphHash;
			step.ReceiptBeforeObjectGraphHash = step.BeforeObjectGraphHash;
			step.ReceiptBeforeTopologyHash = step.BeforeTopologyHash;
			Candidate.CandidateLease.State = KingdomLifecycleLeaseState.Intent;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.CreateIntent;
			Candidate.UpdatedTick = Tick;
			if (ExactGrowthArrivalCandidateAuthority(Book, Candidate)) return true;
			step.State = oldStepState; step.ReceiptState = oldReceiptState;
			step.ReceiptBeforeMatches = oldBeforeMatches;
			step.ReceiptBeforeCount = oldBeforeCount;
			step.ReceiptBeforeOwnerGraphHash = oldBeforeOwner;
			step.ReceiptBeforeObjectGraphHash = oldBeforeObject;
			step.ReceiptBeforeTopologyHash = oldBeforeTopology;
			Candidate.CandidateLease.State = oldLeaseState;
			Candidate.Phase = oldPhase; Candidate.UpdatedTick = oldTick;
			return false;
		}

		internal static bool CommitGrowthArrivalCandidateCreate(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, string ObjectId,
			string AfterOwnerGraphHash, string AfterObjectGraphHash, string AfterTopologyHash,
			string CallbackReferenceHash, bool SameReference, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.CreateIntent
				|| Tick < Candidate.UpdatedTick || !ValidRootId(ObjectId)
				|| !GrowthWitnessHash(AfterOwnerGraphHash)
				|| !GrowthWitnessHash(AfterObjectGraphHash)
				|| !GrowthWitnessHash(AfterTopologyHash)
				|| !GrowthWitnessHash(CallbackReferenceHash) || !SameReference) return false;
			KingdomLifecycleResourceRevision candidateRow = FindGrowthResource(Book,
				Candidate.CandidateLease.Key);
			if (!GrowthResourceMatches(candidateRow, Candidate.CandidateLease)
				|| candidateRow.Revision != Candidate.CandidateLease.BeforeRevision
				|| !string.Equals(candidateRow.ActiveOperationId, Candidate.Id,
					StringComparison.Ordinal)) return false;
			KingdomGrowthObjectCallbackStep step = Candidate.CreateStep;
			string oldObjectId = Candidate.ObjectId;
			string oldAfterOwner = step.AfterOwnerGraphHash;
			string oldAfterObject = step.AfterObjectGraphHash;
			string oldAfterTopology = step.AfterTopologyHash;
			KingdomLifecyclePhysicalState oldState = step.State;
			KingdomLifecyclePhysicalState oldReceiptState = step.ReceiptState;
			int oldAfterMatches = step.ReceiptAfterMatches;
			int oldAfterCount = step.ReceiptAfterCount;
			string oldCallbackId = step.ReceiptCallbackObjectId;
			string oldCallbackMarker = step.ReceiptCallbackMarker;
			string oldCallbackReference = step.ReceiptCallbackReferenceHash;
			bool oldSameReference = step.ReceiptSameReference;
			string oldReceiptAfterOwner = step.ReceiptAfterOwnerGraphHash;
			string oldReceiptAfterObject = step.ReceiptAfterObjectGraphHash;
			string oldReceiptAfterTopology = step.ReceiptAfterTopologyHash;
			string oldProof = step.ReceiptProofId;
			long oldRevision = candidateRow.Revision;
			string oldLastOperation = candidateRow.LastOperationId;
			KingdomLifecycleLeaseState oldLeaseState = Candidate.CandidateLease.State;
			KingdomGrowthArrivalCandidatePhase oldPhase = Candidate.Phase;
			long oldTick = Candidate.UpdatedTick;
			Candidate.ObjectId = ObjectId;
			step.AfterOwnerGraphHash = AfterOwnerGraphHash;
			step.AfterObjectGraphHash = AfterObjectGraphHash;
			step.AfterTopologyHash = AfterTopologyHash;
			step.State = KingdomLifecyclePhysicalState.Proved;
			step.ReceiptState = KingdomLifecyclePhysicalState.Proved;
			step.ReceiptAfterMatches = 1; step.ReceiptAfterCount = 1;
			step.ReceiptCallbackObjectId = ObjectId;
			step.ReceiptCallbackMarker = Candidate.Marker;
			step.ReceiptCallbackReferenceHash = CallbackReferenceHash;
			step.ReceiptSameReference = true;
			step.ReceiptAfterOwnerGraphHash = AfterOwnerGraphHash;
			step.ReceiptAfterObjectGraphHash = AfterObjectGraphHash;
			step.ReceiptAfterTopologyHash = AfterTopologyHash;
			step.ReceiptProofId = GrowthArrivalCandidateCallbackProof(Candidate, step, 0);
			candidateRow.Revision = Candidate.CandidateLease.AfterRevision;
			candidateRow.LastOperationId = Candidate.Id;
			Candidate.CandidateLease.State = KingdomLifecycleLeaseState.Proved;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.Escrowed;
			Candidate.UpdatedTick = Tick;
			if (ExactGrowthArrivalCandidateAuthority(Book, Candidate)) return true;
			Candidate.ObjectId = oldObjectId;
			step.AfterOwnerGraphHash = oldAfterOwner;
			step.AfterObjectGraphHash = oldAfterObject;
			step.AfterTopologyHash = oldAfterTopology;
			step.State = oldState; step.ReceiptState = oldReceiptState;
			step.ReceiptAfterMatches = oldAfterMatches;
			step.ReceiptAfterCount = oldAfterCount;
			step.ReceiptCallbackObjectId = oldCallbackId;
			step.ReceiptCallbackMarker = oldCallbackMarker;
			step.ReceiptCallbackReferenceHash = oldCallbackReference;
			step.ReceiptSameReference = oldSameReference;
			step.ReceiptAfterOwnerGraphHash = oldReceiptAfterOwner;
			step.ReceiptAfterObjectGraphHash = oldReceiptAfterObject;
			step.ReceiptAfterTopologyHash = oldReceiptAfterTopology;
			step.ReceiptProofId = oldProof;
			candidateRow.Revision = oldRevision;
			candidateRow.LastOperationId = oldLastOperation;
			Candidate.CandidateLease.State = oldLeaseState;
			Candidate.Phase = oldPhase; Candidate.UpdatedTick = oldTick;
			return false;
		}

		internal static bool BeginGrowthArrivalLodgingObservation(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, string ZoneId, int X, int Y,
			string BeforeGraphHash, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.Escrowed
				|| Tick < Candidate.UpdatedTick || !string.Equals(ZoneId,
					Candidate.LodgingZoneId, StringComparison.Ordinal)
				|| X < 0 || X > MaxCoordinate
				|| Y < 0 || Y > MaxCoordinate || !GrowthWitnessHash(BeforeGraphHash)) return false;
			string oldZone = Candidate.LodgingZoneId;
			int oldX = Candidate.LodgingX; int oldY = Candidate.LodgingY;
			string oldBefore = Candidate.LodgingBeforeGraphHash;
			string oldDeclared = Candidate.LodgingDeclaredGraphHash;
			string oldReceiptId = Candidate.LodgingReceiptId;
			KingdomLifecyclePhysicalState oldState = Candidate.LodgingState;
			KingdomLifecycleLeaseState oldLease = Candidate.LodgingLease.State;
			KingdomGrowthArrivalCandidatePhase oldPhase = Candidate.Phase;
			string oldPlanHash = Candidate.PlanHash;
			long oldTick = Candidate.UpdatedTick;
			Candidate.LodgingZoneId = ZoneId; Candidate.LodgingX = X; Candidate.LodgingY = Y;
			Candidate.LodgingBeforeGraphHash = BeforeGraphHash;
			Candidate.LodgingDeclaredGraphHash = null;
			Candidate.LodgingReceiptId = ChildId(Candidate.Id, "lodging-receipt", 0);
			Candidate.LodgingState = KingdomLifecyclePhysicalState.Intent;
			Candidate.LodgingLease.State = KingdomLifecycleLeaseState.Intent;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.LodgingIntent;
			Candidate.UpdatedTick = Tick;
			string intentPlanHash;
			if (TryGrowthArrivalCandidatePlanHash(Candidate, out intentPlanHash))
				Candidate.PlanHash = intentPlanHash;
			if (intentPlanHash != null && ExactGrowthArrivalCandidateAuthority(Book, Candidate))
				return true;
			Candidate.LodgingZoneId = oldZone; Candidate.LodgingX = oldX;
			Candidate.LodgingY = oldY; Candidate.LodgingBeforeGraphHash = oldBefore;
			Candidate.LodgingDeclaredGraphHash = oldDeclared;
			Candidate.LodgingReceiptId = oldReceiptId; Candidate.LodgingState = oldState;
			Candidate.LodgingLease.State = oldLease; Candidate.Phase = oldPhase;
			Candidate.PlanHash = oldPlanHash;
			Candidate.UpdatedTick = oldTick;
			return false;
		}

		internal static bool CommitGrowthArrivalLodgingObservation(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, KingdomGrowthArrivalDisposition Disposition,
			KingdomGrowthArrivalRefusalReason RefusalReason, string ReceiptGraphHash,
			string CallbackReferenceHash, bool SameReference, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.LodgingIntent
				|| Tick < Candidate.UpdatedTick
				|| (Disposition != KingdomGrowthArrivalDisposition.Joined
					&& Disposition != KingdomGrowthArrivalDisposition.NoAcceptableHome)
				|| !GrowthWitnessHash(ReceiptGraphHash)
				|| string.Equals(ReceiptGraphHash, Candidate.LodgingBeforeGraphHash,
					StringComparison.Ordinal)
				|| !Enum.IsDefined(typeof(KingdomGrowthArrivalRefusalReason), RefusalReason)
				|| (Disposition == KingdomGrowthArrivalDisposition.Joined
					? RefusalReason != KingdomGrowthArrivalRefusalReason.None
					: RefusalReason == KingdomGrowthArrivalRefusalReason.None)
				|| !GrowthWitnessHash(CallbackReferenceHash) || !SameReference) return false;
			KingdomLifecycleResourceRevision lodgingRow = FindGrowthResource(Book,
				Candidate.LodgingLease.Key);
			if (!GrowthResourceMatches(lodgingRow, Candidate.LodgingLease)
				|| lodgingRow.Revision != Candidate.LodgingLease.BeforeRevision
				|| !string.Equals(lodgingRow.ActiveOperationId, Candidate.Id,
					StringComparison.Ordinal)) return false;
			KingdomGrowthArrivalDisposition oldDisposition = Candidate.Disposition;
			KingdomGrowthArrivalRefusalReason oldReason = Candidate.RefusalReason;
			string oldDeclaredGraph = Candidate.LodgingDeclaredGraphHash;
			string oldReceiptGraph = Candidate.LodgingReceiptGraphHash;
			string oldCallbackReference = Candidate.LodgingCallbackReferenceHash;
			bool oldSameReference = Candidate.LodgingSameReference;
			string oldPlanHash = Candidate.PlanHash;
			KingdomLifecyclePhysicalState oldState = Candidate.LodgingState;
			long oldRevision = lodgingRow.Revision;
			string oldLastOperation = lodgingRow.LastOperationId;
			KingdomLifecycleLeaseState oldLease = Candidate.LodgingLease.State;
			KingdomGrowthArrivalCandidatePhase oldPhase = Candidate.Phase;
			long oldTick = Candidate.UpdatedTick;
			Candidate.Disposition = Disposition;
			Candidate.RefusalReason = RefusalReason;
			Candidate.LodgingReceiptGraphHash = ReceiptGraphHash;
			Candidate.LodgingCallbackReferenceHash = CallbackReferenceHash;
			Candidate.LodgingSameReference = true;
			Candidate.LodgingDeclaredGraphHash = GrowthArrivalLodgingProof(Candidate);
			Candidate.LodgingState = KingdomLifecyclePhysicalState.Proved;
			string observedPlanHash;
			string basePlanHash;
			if (Candidate.LodgingDeclaredGraphHash == null
				|| !TryGrowthArrivalCandidateBasePlanHash(Candidate, out basePlanHash)
				|| !TryGrowthArrivalObservedPlanHash(Candidate, basePlanHash,
					out observedPlanHash))
			{
				Candidate.Disposition = oldDisposition; Candidate.RefusalReason = oldReason;
				Candidate.LodgingDeclaredGraphHash = oldDeclaredGraph;
				Candidate.LodgingReceiptGraphHash = oldReceiptGraph;
				Candidate.LodgingCallbackReferenceHash = oldCallbackReference;
				Candidate.LodgingSameReference = oldSameReference;
				Candidate.LodgingState = oldState;
				return false;
			}
			Candidate.PlanHash = observedPlanHash;
			lodgingRow.Revision = Candidate.LodgingLease.AfterRevision;
			lodgingRow.LastOperationId = Candidate.Id;
			Candidate.LodgingLease.State = KingdomLifecycleLeaseState.Proved;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.Observed;
			Candidate.UpdatedTick = Tick;
			if (ExactGrowthArrivalCandidateAuthority(Book, Candidate)) return true;
			Candidate.Disposition = oldDisposition;
			Candidate.RefusalReason = oldReason;
			Candidate.LodgingDeclaredGraphHash = oldDeclaredGraph;
			Candidate.LodgingReceiptGraphHash = oldReceiptGraph;
			Candidate.LodgingCallbackReferenceHash = oldCallbackReference;
			Candidate.LodgingSameReference = oldSameReference;
			Candidate.PlanHash = oldPlanHash;
			Candidate.LodgingState = oldState;
			lodgingRow.Revision = oldRevision; lodgingRow.LastOperationId = oldLastOperation;
			Candidate.LodgingLease.State = oldLease; Candidate.Phase = oldPhase;
			Candidate.UpdatedTick = oldTick;
			return false;
		}

		internal static bool BeginGrowthArrivalCandidateDisposition(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, string ConsumingOperationId,
			KingdomGrowthObjectMutationKind Kind, KingdomGrowthLocationKind ToLocation,
			string OwnerId, string ZoneId, int X, int Y, string BeforeOwnerGraphHash,
			string AfterOwnerGraphHash, string BeforeObjectGraphHash,
			string AfterObjectGraphHash, string BeforeTopologyHash, string AfterTopologyHash,
			long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.Observed
				|| Tick < Candidate.UpdatedTick || !GrowthWitnessHash(BeforeOwnerGraphHash)
				|| !GrowthWitnessHash(AfterOwnerGraphHash)
				|| !GrowthWitnessHash(BeforeObjectGraphHash)
				|| !GrowthWitnessHash(AfterObjectGraphHash)
				|| !GrowthWitnessHash(BeforeTopologyHash)
				|| !GrowthWitnessHash(AfterTopologyHash)) return false;
			bool joined = Candidate.Disposition == KingdomGrowthArrivalDisposition.Joined;
			KingdomGrowthOperation operation = Book.ArrivalOp;
			if (operation == null || !ReferenceEquals(Book.ArrivalOp, operation)
				|| !string.Equals(operation.Id, ConsumingOperationId, StringComparison.Ordinal)
				|| !string.Equals(operation.ArrivalCandidateId, Candidate.Id,
					StringComparison.Ordinal)
				|| operation.ArrivalDisposition != Candidate.Disposition
				|| (operation.Phase != KingdomGrowthPhase.Prepared
					&& operation.Phase != KingdomGrowthPhase.WaterIntent
					&& operation.Phase != KingdomGrowthPhase.WaterSettled)
				|| !ExactGrowthOperationAuthority(Book, operation)) return false;
			if (joined ? !ValidGeneratedId(ConsumingOperationId)
				|| Kind != KingdomGrowthObjectMutationKind.CellAdd
				|| ToLocation != KingdomGrowthLocationKind.Cell || OwnerId != null
				|| !string.Equals(ZoneId, Candidate.LodgingZoneId,
					StringComparison.Ordinal) || X != Candidate.LodgingX || Y != Candidate.LodgingY
				|| !string.Equals(ZoneId, operation.ZoneId, StringComparison.Ordinal)
				|| X != operation.TargetX || Y != operation.TargetY
				|| !GrowthLocationShape(ToLocation, OwnerId, ZoneId, X, Y)
				: Candidate.Disposition != KingdomGrowthArrivalDisposition.NoAcceptableHome
					|| !ValidGeneratedId(ConsumingOperationId)
					|| Kind != KingdomGrowthObjectMutationKind.Obliterate
					|| ToLocation != KingdomGrowthLocationKind.Graveyard
					|| !GrowthLocationShape(ToLocation, OwnerId, ZoneId, X, Y)) return false;
			KingdomGrowthObjectCallbackStep step = new KingdomGrowthObjectCallbackStep
			{
				EventId = ChildId(Candidate.Id, "object-callback", 1), Kind = Kind,
				FromLocation = KingdomGrowthLocationKind.Escrow, ToLocation = ToLocation,
				EscrowKey = Candidate.EscrowKey, BeforeX = -1, BeforeY = -1,
				AfterOwnerId = OwnerId, AfterZoneId = ZoneId, AfterX = X, AfterY = Y,
				BeforeCount = 1, AfterCount = joined ? 1 : 0, NoStack = joined,
				BeforeOwnerGraphHash = BeforeOwnerGraphHash,
				AfterOwnerGraphHash = AfterOwnerGraphHash,
				BeforeObjectGraphHash = BeforeObjectGraphHash,
				AfterObjectGraphHash = AfterObjectGraphHash,
				BeforeTopologyHash = BeforeTopologyHash, AfterTopologyHash = AfterTopologyHash,
				State = KingdomLifecyclePhysicalState.Intent,
				ReceiptId = ChildId(Candidate.Id, "object-callback-receipt", 1),
				ReceiptBeforeMatches = 1, ReceiptBeforeCount = 1,
				ReceiptBeforeOwnerGraphHash = BeforeOwnerGraphHash,
				ReceiptBeforeObjectGraphHash = BeforeObjectGraphHash,
				ReceiptBeforeTopologyHash = BeforeTopologyHash,
				ReceiptState = KingdomLifecyclePhysicalState.Intent
			};
			KingdomGrowthObjectCallbackStep oldStep = Candidate.DispositionStep;
			string oldConsuming = Candidate.ConsumingOperationId;
			long oldConsumingSequence = Candidate.ConsumingOperationSequence;
			string oldPlanHash = Candidate.PlanHash;
			KingdomLifecycleLeaseState oldLease = Candidate.EscrowLease.State;
			KingdomGrowthArrivalCandidatePhase oldPhase = Candidate.Phase;
			long oldTick = Candidate.UpdatedTick;
			Candidate.DispositionStep = step;
			Candidate.ConsumingOperationId = ConsumingOperationId;
			Candidate.ConsumingOperationSequence = operation.Sequence;
			Candidate.EscrowLease.State = KingdomLifecycleLeaseState.Intent;
			Candidate.Phase = joined ? KingdomGrowthArrivalCandidatePhase.ConsumeIntent
				: KingdomGrowthArrivalCandidatePhase.RefusalIntent;
			Candidate.UpdatedTick = Tick;
			string dispositionPlanHash;
			if (TryGrowthArrivalCandidatePlanHash(Candidate, out dispositionPlanHash))
				Candidate.PlanHash = dispositionPlanHash;
			if (dispositionPlanHash != null && ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				&& ExactGrowthOperationAuthority(Book, operation)) return true;
			Candidate.DispositionStep = oldStep;
			Candidate.ConsumingOperationId = oldConsuming;
			Candidate.ConsumingOperationSequence = oldConsumingSequence;
			Candidate.PlanHash = oldPlanHash;
			Candidate.EscrowLease.State = oldLease;
			Candidate.Phase = oldPhase; Candidate.UpdatedTick = oldTick;
			return false;
		}

		internal static bool CommitGrowthArrivalCandidateDisposition(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, string CallbackReferenceHash,
			bool SameReference, long Tick)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| (Candidate.Phase != KingdomGrowthArrivalCandidatePhase.ConsumeIntent
					&& Candidate.Phase != KingdomGrowthArrivalCandidatePhase.RefusalIntent)
				|| Tick < Candidate.UpdatedTick || !GrowthWitnessHash(CallbackReferenceHash)
				|| SameReference != (Candidate.Disposition ==
					KingdomGrowthArrivalDisposition.Joined)) return false;
			KingdomGrowthObjectCallbackStep step = Candidate.DispositionStep;
			KingdomLifecycleResourceLease[] leases = { Candidate.EscrowLease };
			KingdomLifecycleResourceRevision[] rows =
				{ FindGrowthResource(Book, leases[0].Key) };
			for (int i = 0; i < rows.Length; i++)
				if (!GrowthResourceMatches(rows[i], leases[i])
					|| rows[i].Revision != leases[i].BeforeRevision
					|| !string.Equals(rows[i].ActiveOperationId, Candidate.Id,
						StringComparison.Ordinal)) return false;
			KingdomLifecyclePhysicalState oldState = step.State;
			KingdomLifecyclePhysicalState oldReceiptState = step.ReceiptState;
			int oldAfterMatches = step.ReceiptAfterMatches;
			int oldAfterCount = step.ReceiptAfterCount;
			string oldCallbackId = step.ReceiptCallbackObjectId;
			string oldCallbackMarker = step.ReceiptCallbackMarker;
			string oldCallbackReference = step.ReceiptCallbackReferenceHash;
			bool oldSameReference = step.ReceiptSameReference;
			string oldAfterOwner = step.ReceiptAfterOwnerGraphHash;
			string oldAfterObject = step.ReceiptAfterObjectGraphHash;
			string oldAfterTopology = step.ReceiptAfterTopologyHash;
			string oldProof = step.ReceiptProofId;
			long oldRevision = rows[0].Revision;
			string oldLastOperation = rows[0].LastOperationId;
			KingdomLifecycleLeaseState oldLeaseState = leases[0].State;
			KingdomGrowthArrivalCandidatePhase oldPhase = Candidate.Phase;
			long oldTick = Candidate.UpdatedTick;
			step.State = KingdomLifecyclePhysicalState.Proved;
			step.ReceiptState = KingdomLifecyclePhysicalState.Proved;
			step.ReceiptAfterMatches = step.AfterCount == 0 ? 0 : 1;
			step.ReceiptAfterCount = step.AfterCount;
			step.ReceiptCallbackObjectId = Candidate.ObjectId;
			step.ReceiptCallbackMarker = Candidate.Marker;
			step.ReceiptCallbackReferenceHash = CallbackReferenceHash;
			step.ReceiptSameReference = SameReference;
			step.ReceiptAfterOwnerGraphHash = step.AfterOwnerGraphHash;
			step.ReceiptAfterObjectGraphHash = step.AfterObjectGraphHash;
			step.ReceiptAfterTopologyHash = step.AfterTopologyHash;
			step.ReceiptProofId = GrowthArrivalCandidateCallbackProof(Candidate, step, 1);
			for (int i = 0; i < rows.Length; i++)
			{
				rows[i].Revision = leases[i].AfterRevision;
				rows[i].LastOperationId = Candidate.Id;
				leases[i].State = KingdomLifecycleLeaseState.Proved;
			}
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.Settled;
			Candidate.UpdatedTick = Tick;
			KingdomGrowthOperation operation = Book.ArrivalOp;
			if (ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				&& operation != null && string.Equals(operation.Id,
					Candidate.ConsumingOperationId, StringComparison.Ordinal)
				&& ExactGrowthOperationAuthority(Book, operation)) return true;
			step.State = oldState; step.ReceiptState = oldReceiptState;
			step.ReceiptAfterMatches = oldAfterMatches;
			step.ReceiptAfterCount = oldAfterCount;
			step.ReceiptCallbackObjectId = oldCallbackId;
			step.ReceiptCallbackMarker = oldCallbackMarker;
			step.ReceiptCallbackReferenceHash = oldCallbackReference;
			step.ReceiptSameReference = oldSameReference;
			step.ReceiptAfterOwnerGraphHash = oldAfterOwner;
			step.ReceiptAfterObjectGraphHash = oldAfterObject;
			step.ReceiptAfterTopologyHash = oldAfterTopology;
			step.ReceiptProofId = oldProof;
			rows[0].Revision = oldRevision; rows[0].LastOperationId = oldLastOperation;
			leases[0].State = oldLeaseState;
			Candidate.Phase = oldPhase; Candidate.UpdatedTick = oldTick;
			return false;
		}

		public static bool RetireGrowthArrivalCandidate(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase != KingdomGrowthArrivalCandidatePhase.Settled
				|| Book.ArrivalOp != null
				|| !GrowthRetiredArrivalBarrierExists(Book, Candidate)) return false;
			KingdomLifecycleResourceRevision candidateRow = FindGrowthResource(Book,
				Candidate.CandidateLease.Key);
			KingdomLifecycleResourceRevision escrowRow = FindGrowthResource(Book,
				Candidate.EscrowLease.Key);
			KingdomLifecycleResourceRevision lodgingRow = FindGrowthResource(Book,
				Candidate.LodgingLease.Key);
			if (!GrowthLeaseProvedByCandidateRow(candidateRow, Candidate.CandidateLease, Candidate.Id)
				|| !GrowthLeaseProvedByCandidateRow(lodgingRow, Candidate.LodgingLease, Candidate.Id)
				|| !GrowthLeaseProvedByCandidateRow(escrowRow, Candidate.EscrowLease,
					Candidate.Id)) return false;
			string candidateActive = candidateRow.ActiveOperationId;
			string lodgingActive = lodgingRow.ActiveOperationId;
			string escrowActive = escrowRow.ActiveOperationId;
			long retiredBefore = Book.ArrivalCandidateRetiredThrough;
			long arrivalBefore = Book.NextArrivalTick;
			candidateRow.ActiveOperationId = null; lodgingRow.ActiveOperationId = null;
			escrowRow.ActiveOperationId = null;
			Book.ArrivalCandidateRetiredThrough = Candidate.Sequence;
			Book.ArrivalCandidate = null;
			if (Book.WorkPaused) Book.NextArrivalTick = 0L;
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			Book.ArrivalCandidate = Candidate;
			Book.ArrivalCandidateRetiredThrough = retiredBefore;
			Book.NextArrivalTick = arrivalBefore;
			candidateRow.ActiveOperationId = candidateActive;
			lodgingRow.ActiveOperationId = lodgingActive;
			escrowRow.ActiveOperationId = escrowActive;
			return false;
		}

		private static bool GrowthRetiredArrivalBarrierExists(KingdomGrowthBook book,
			KingdomGrowthArrivalCandidate candidate)
		{
			if (book == null || candidate == null || candidate.ConsumingOperationSequence <= 0L
				|| book.ArrivalRetiredThrough < candidate.ConsumingOperationSequence
				|| !string.Equals(candidate.ConsumingOperationId,
					GrowthOperationId(book.SettlementId, KingdomGrowthSlotKind.Arrival, null,
						candidate.ConsumingOperationSequence), StringComparison.Ordinal)) return false;
			string subject = GrowthClockSubject(book.SettlementId,
				KingdomGrowthSlotKind.Arrival, null);
			KingdomLifecycleResourceRevision row = FindGrowthResource(book,
				ResourceKey(KingdomLifecycleResourceKind.GrowthClock, book.SettlementId, subject));
			return row != null && row.Kind == KingdomLifecycleResourceKind.GrowthClock
				&& row.ActiveOperationId == null
				&& string.Equals(row.LastOperationId, candidate.ConsumingOperationId,
					StringComparison.Ordinal);
		}

		public static bool QuarantineGrowthArrivalCandidate(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate, string Fault)
		{
			if (!ExactGrowthArrivalCandidateAuthority(Book, Candidate)
				|| Candidate.Phase == KingdomGrowthArrivalCandidatePhase.Quarantined
				|| string.IsNullOrEmpty(Fault) || TooLong(Fault, MaxTextChars)) return false;
			KingdomGrowthArrivalCandidatePhase before = Candidate.Phase;
			Candidate.EvidencePhase = before;
			Candidate.Phase = KingdomGrowthArrivalCandidatePhase.Quarantined;
			Candidate.Fault = SafeFault(Fault);
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			Candidate.Phase = before; Candidate.EvidencePhase = 0; Candidate.Fault = null;
			return false;
		}

		private static bool ExactGrowthArrivalCandidateAuthority(KingdomGrowthBook Book,
			KingdomGrowthArrivalCandidate Candidate)
		{
			return Candidate != null && ReferenceEquals(Book == null ? null : Book.ArrivalCandidate,
				Candidate) && CanOwnGrowthAuthority(Book, Book.SettlementId);
		}

		private static bool GrowthLeaseProvedByCandidateRow(
			KingdomLifecycleResourceRevision row, KingdomLifecycleResourceLease lease, string id)
		{
			return GrowthResourceMatches(row, lease) && lease.State == KingdomLifecycleLeaseState.Proved
				&& row.Revision == lease.AfterRevision
				&& string.Equals(row.ActiveOperationId, id, StringComparison.Ordinal)
				&& string.Equals(row.LastOperationId, id, StringComparison.Ordinal);
		}

		private static string GrowthArrivalCandidateCallbackProof(
			KingdomGrowthArrivalCandidate candidate, KingdomGrowthObjectCallbackStep step,
			int ordinal, bool LegacyV1 = false)
		{
			string binding = candidate == null ? null : candidate.PlanHash;
			if (ordinal == 0
				&& !(LegacyV1
					? TryLegacyGrowthArrivalCandidateBasePlanHash(candidate, out binding)
					: TryGrowthArrivalCandidateBasePlanHash(candidate, out binding))) return null;
			return HashId("growth-arrival-candidate-callback-proof", delegate(BinaryWriter w)
			{
				CanonicalString(w, candidate.Id); CanonicalString(w, binding);
				w.Write(ordinal); CanonicalString(w, candidate.ObjectId);
				CanonicalString(w, candidate.Marker); CanonicalString(w, step.EventId);
				CanonicalString(w, step.ReceiptCallbackReferenceHash);
				if (!LegacyV1) w.Write(step.ReceiptSameReference);
				CanonicalString(w, step.ReceiptAfterOwnerGraphHash);
				CanonicalString(w, step.ReceiptAfterObjectGraphHash);
				CanonicalString(w, step.ReceiptAfterTopologyHash);
			});
		}

		public static bool TryRegisterGrowthField(KingdomGrowthBook Book, string FieldId)
		{
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| !ValidRootId(FieldId)) return false;
			KingdomGrowthFieldSlot existing = FindGrowthField(Book, FieldId);
			if (existing != null) return !existing.Quarantined;
			if (Book.FieldOps.Count >= MaxGrowthFields) return false;
			KingdomGrowthFieldSlot added = new KingdomGrowthFieldSlot { FieldId = FieldId };
			Book.FieldOps.Add(added);
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			Book.FieldOps.RemoveAt(Book.FieldOps.Count - 1);
			return false;
		}

		internal static bool InstallGrowthFieldBootstrap(KingdomGrowthBook Book,
			KingdomGrowthFieldState State, List<KingdomGrowthCropRow> Rows)
		{
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| State == null || Rows == null || !GrowthFieldStateShape(State, State.FieldId)
				|| !GrowthCropRowsShape(Rows, State.FieldId, false, null)
				|| State.DeclaredRows != Rows.Count) return false;
			KingdomGrowthFieldSlot field = FindGrowthField(Book, State.FieldId);
			if (field == null || field.Quarantined || field.Operation != null
				|| !GrowthFieldMatchesState(field, new KingdomGrowthFieldState
				{
					FieldId = field.FieldId, X = -1, Y = -1
				})) return false;
			for (int i = 0; i < Rows.Count; i++)
				if (!string.Equals(Rows[i].FieldId, State.FieldId, StringComparison.Ordinal)) return false;
			KingdomGrowthFieldState before = GrowthFieldState(field);
			List<KingdomGrowthCropRow> rowsBefore = new List<KingdomGrowthCropRow>(Book.CropRows);
			ApplyGrowthFieldState(field, State);
			for (int i = 0; i < Rows.Count; i++) Book.CropRows.Add(CloneGrowthCropRow(Rows[i]));
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			ApplyGrowthFieldState(field, before); Book.CropRows.Clear();
			Book.CropRows.AddRange(rowsBefore); return false;
		}

		public static KingdomGrowthOperation PrepareGrowthOperation(KingdomGrowthBook Book,
			KingdomGrowthAction Action, string FieldId, long Tick)
		{
			bool productiveStarter = Action != KingdomGrowthAction.Withdraw;
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| Tick < 0L || !KnownGrowthAction(Action)
				|| (productiveStarter && (Book.OptionState != KingdomLifecycleOptionState.Enabled
					|| Book.HealthState != KingdomGrowthHealthState.Healthy || Book.WorkPaused))
				|| Tick < Book.OptionTick || Tick < Book.HealthTick
				|| Tick < Book.ScarcityOptionTick || Tick < Book.EffectiveWorkTick)
				return null;
			KingdomGrowthSlotKind slot = SlotForGrowthAction(Action);
			if (slot == KingdomGrowthSlotKind.None) return null;
			if (slot != KingdomGrowthSlotKind.Field && FieldId != null && FieldId.Length == 0)
				FieldId = null;
			KingdomGrowthFieldSlot field = slot == KingdomGrowthSlotKind.Field
				? FindGrowthField(Book, FieldId) : null;
			if (slot == KingdomGrowthSlotKind.Field && (field == null || field.Quarantined)) return null;
			if (slot != KingdomGrowthSlotKind.Field && FieldId != null) return null;
			if (GetGrowthOperation(Book, slot, FieldId) != null) return null;
			long next = GetGrowthNext(Book, slot, field);
			long retired = GetGrowthRetired(Book, slot, field);
			if (!IsExactSuccessor(next, retired) || next == long.MaxValue) return null;
			if (Action == KingdomGrowthAction.Arrival &&
				(Book.ArrivalIntervalTicks <= 0L || Book.NextArrivalTick <= 0L
					|| Tick < Book.NextArrivalTick)) return null;
			long actionClockBefore = GrowthClockValue(Book, Action, field);
			long clockBefore = slot == KingdomGrowthSlotKind.Field
				? field.CommitRevision : actionClockBefore;
			long clockAfter;
			long effectiveNow;
			if (!TryGrowthEffectiveNow(Book, Tick, out effectiveNow)) return null;
			long fieldClockAfter = slot == KingdomGrowthSlotKind.Field
				? Math.Max(field.ClockTick, effectiveNow) : 0L;
			if (slot == KingdomGrowthSlotKind.Field)
			{
				if (!CheckedAdd(clockBefore, 1L, out clockAfter)) return null;
			}
			else if (Action == KingdomGrowthAction.Arrival)
			{
				if (!CheckedAdd(Tick, Book.ArrivalIntervalTicks, out clockAfter)) return null;
			}
			else if (Action == KingdomGrowthAction.Heartbeat
				|| Action == KingdomGrowthAction.Fetch || Action == KingdomGrowthAction.Mill)
			{
				if (Tick <= clockBefore) return null;
				clockAfter = Tick;
			}
			else
			{
				if (!CheckedAdd(clockBefore, 1L, out clockAfter)) return null;
				if (Tick > clockAfter) clockAfter = Tick;
			}
			long delta;
			if (!CheckedAdd(clockAfter, -clockBefore, out delta) || delta == 0L) return null;
			string id = GrowthOperationId(Book.SettlementId, slot, FieldId, next);
			string subject = GrowthClockSubject(Book.SettlementId, slot, FieldId);
			string key = ResourceKey(KingdomLifecycleResourceKind.GrowthClock,
				Book.SettlementId, subject);
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, key);
			if (id == null || key == null || (row != null && (!string.IsNullOrEmpty(row.ActiveOperationId)
				|| row.Revision == long.MaxValue))) return null;
			long revision = row == null ? 0L : row.Revision;
			KingdomGrowthOperation operation = new KingdomGrowthOperation
			{
				Sequence = next, Id = id, Action = Action, Phase = KingdomGrowthPhase.Prepared,
				CreatedTick = Tick, UpdatedTick = Tick, SettlementId = Book.SettlementId,
				FieldId = FieldId, TargetX = -1, TargetY = -1,
				OptionState = Book.OptionState, OptionTick = Book.OptionTick,
				HealthState = Book.HealthState, HealthTick = Book.HealthTick,
					EffectiveWorkBefore = Book.EffectiveWorkTick,
				EffectiveWorkAfter = IsGrowthFieldAction(Action)
						? effectiveNow : Book.EffectiveWorkTick,
					FieldClockBefore = slot == KingdomGrowthSlotKind.Field ? field.ClockTick : 0L,
					FieldClockAfter = fieldClockAfter,
				HeartbeatBefore = Book.LastHeartbeatTick,
					HeartbeatAfter = Action == KingdomGrowthAction.Heartbeat
						? clockAfter : Book.LastHeartbeatTick,
				ArrivalBefore = Book.NextArrivalTick,
				ArrivalAfter = Action == KingdomGrowthAction.Arrival ? clockAfter : Book.NextArrivalTick,
					FetchBefore = Book.LastFetchTick,
					FetchAfter = Action == KingdomGrowthAction.Fetch ? clockAfter : Book.LastFetchTick,
					MillBefore = Book.LastMillTick,
					MillAfter = Action == KingdomGrowthAction.Mill ? clockAfter : Book.LastMillTick,
					SubsidenceBefore = Book.LastSubsidenceTick,
					SubsidenceAfter = Book.LastSubsidenceTick,
					DeliveryBefore = Book.LastDeliveryTick,
					DeliveryAfter = Action == KingdomGrowthAction.Delivery
						? clockAfter : Book.LastDeliveryTick,
					DepartureBefore = Book.LastDepartureTick,
					DepartureAfter = Action == KingdomGrowthAction.Departure
						? clockAfter : Book.LastDepartureTick,
					ScarcityOptionState = Book.ScarcityOptionState,
					ScarcityOptionTick = Book.ScarcityOptionTick,
				PendingCropBefore = Book.PendingCrop, PendingCropAfter = Book.PendingCrop,
				PendingCropBlueprintBefore = Book.PendingCropBlueprint,
				PendingCropZoneIdBefore = Book.PendingCropZoneId,
				PendingCropBlueprintAfter = Book.PendingCropBlueprint,
				PendingCropZoneIdAfter = Book.PendingCropZoneId,
				ClockState = KingdomLifecyclePhysicalState.Prepared,
				ClockLease = new KingdomLifecycleResourceLease
				{
					OperationId = id, Kind = KingdomLifecycleResourceKind.GrowthClock,
					ScopeId = Book.SettlementId, SubjectId = subject, Key = key,
					Before = clockBefore, Delta = delta, After = clockAfter,
					BeforeRevision = revision, AfterRevision = revision + 1L,
					State = KingdomLifecycleLeaseState.Prepared
				}
			};
			operation.OutboxEvents = new List<KingdomGrowthOutboxEvent>();
			return operation;
		}

		public static KingdomLifecycleOutbox PrepareGrowthOutbox(KingdomGrowthOperation Operation,
			string Chronicle, string Ledger, string Message, string Deed, string Guestbook)
		{
			if (Operation == null || !ValidGeneratedId(Operation.Id)) return null;
			if (Chronicle != null && Chronicle.Length == 0) Chronicle = null;
			if (Ledger != null && Ledger.Length == 0) Ledger = null;
			if (Message != null && Message.Length == 0) Message = null;
			if (Deed != null && Deed.Length == 0) Deed = null;
			if (Guestbook != null && Guestbook.Length == 0) Guestbook = null;
			return new KingdomLifecycleOutbox
			{
				OperationId = Operation.Id, EventId = ChildId(Operation.Id, "outbox", 0),
				ChronicleReceiptId = ChildId(Operation.Id, "chronicle", 0),
				Chronicle = Chronicle, ChronicleDisposition = InitialDisposition(Chronicle),
				ChronicleState = InitialSink(Chronicle), Ledger = Ledger,
				LedgerDisposition = InitialDisposition(Ledger), LedgerState = InitialSink(Ledger),
				Message = Message, MessageDisposition = InitialDisposition(Message),
				MessageState = InitialSink(Message), Deed = Deed,
				DeedDisposition = InitialDisposition(Deed), DeedState = InitialSink(Deed),
				GuestbookLine = Guestbook, GuestbookDisposition = InitialDisposition(Guestbook),
				GuestbookState = InitialSink(Guestbook)
			};
		}

		public static KingdomGrowthOutboxEvent PrepareGrowthOutboxEvent(
			KingdomGrowthOperation Operation, int Ordinal, string Kind, string Chronicle,
			string Ledger, string Message, string Deed, string Guestbook)
		{
			return PrepareGrowthOutboxEvent(Operation, Ordinal, Kind, Chronicle, Ledger,
				Message, Deed, Guestbook, 0, null, 0, null, 0, null, 0, null,
				0, null, 0, null);
		}

		internal static string GrowthChronicleOutboxReceiptId(
			KingdomGrowthOperation Operation, int Ordinal)
		{
			if (Operation == null || !ValidGeneratedId(Operation.Id) || Ordinal < 0
				|| Ordinal >= MaxGrowthOutboxEvents) return null;
			string eventId = ChildId(Operation.Id, "outbox-event", Ordinal);
			return ChildId(eventId, "chronicle", 0);
		}

		public static KingdomGrowthOutboxEvent PrepareGrowthOutboxEvent(
			KingdomGrowthOperation Operation, int Ordinal, string Kind, string Chronicle,
			string Ledger, string Message, string Deed, string Guestbook,
			int ChronicleBeforeCount, string ChronicleBeforeHash,
			int ChronicleDeclaredAfterCount, string ChronicleDeclaredAfterHash,
			int LedgerBeforeCount, string LedgerBeforeHash,
			int LedgerDeclaredAfterCount, string LedgerDeclaredAfterHash)
		{
			// The v1 signature remains callable for source compatibility, but v2 cannot mint a
			// one-register Chronicle promise. Null Chronicle declarations remain canonical.
			if (Chronicle != null) return null;
			return PrepareGrowthOutboxEvent(Operation, Ordinal, Kind, Chronicle, Ledger,
				Message, Deed, Guestbook, ChronicleBeforeCount, ChronicleBeforeHash,
				ChronicleDeclaredAfterCount, ChronicleDeclaredAfterHash,
				0, null, 0, null, LedgerBeforeCount, LedgerBeforeHash,
				LedgerDeclaredAfterCount, LedgerDeclaredAfterHash);
		}

		public static KingdomGrowthOutboxEvent PrepareGrowthOutboxEvent(
			KingdomGrowthOperation Operation, int Ordinal, string Kind, string Chronicle,
			string Ledger, string Message, string Deed, string Guestbook,
			int ChronicleBeforeCount, string ChronicleBeforeHash,
			int ChronicleDeclaredAfterCount, string ChronicleDeclaredAfterHash,
			int OutsiderBeforeCount, string OutsiderBeforeHash,
			int OutsiderDeclaredAfterCount, string OutsiderDeclaredAfterHash,
			int LedgerBeforeCount, string LedgerBeforeHash,
			int LedgerDeclaredAfterCount, string LedgerDeclaredAfterHash)
		{
			// A v2 Chronicle callback must carry exact rendered entries. Compatibility callers
			// without them may still prepare every non-Chronicle sink.
			if (Chronicle != null && Chronicle.Length > 0) return null;
			return PrepareDeclaredGrowthOutboxEvent(Operation, Ordinal, Kind, Chronicle,
				null, null, Ledger, Message, Deed, Guestbook, ChronicleBeforeCount,
				ChronicleBeforeHash, ChronicleDeclaredAfterCount, ChronicleDeclaredAfterHash,
				OutsiderBeforeCount, OutsiderBeforeHash, OutsiderDeclaredAfterCount,
				OutsiderDeclaredAfterHash, LedgerBeforeCount, LedgerBeforeHash,
				LedgerDeclaredAfterCount, LedgerDeclaredAfterHash);
		}

		internal static KingdomGrowthOutboxEvent PrepareDeclaredGrowthOutboxEvent(
			KingdomGrowthOperation Operation, int Ordinal, string Kind, string Chronicle,
			string ChronicleOfficial, string ChronicleOutsider, string Ledger, string Message,
			string Deed, string Guestbook,
			int ChronicleBeforeCount, string ChronicleBeforeHash,
			int ChronicleDeclaredAfterCount, string ChronicleDeclaredAfterHash,
			int OutsiderBeforeCount, string OutsiderBeforeHash,
			int OutsiderDeclaredAfterCount, string OutsiderDeclaredAfterHash,
			int LedgerBeforeCount, string LedgerBeforeHash,
			int LedgerDeclaredAfterCount, string LedgerDeclaredAfterHash)
		{
			if (Chronicle != null && Chronicle.Length == 0) Chronicle = null;
			if (Ledger != null && Ledger.Length == 0) Ledger = null;
			if (Message != null && Message.Length == 0) Message = null;
			if (Deed != null && Deed.Length == 0) Deed = null;
			if (Guestbook != null && Guestbook.Length == 0) Guestbook = null;
			if (Operation == null || Ordinal < 0 || Ordinal >= MaxGrowthOutboxEvents
				|| !ValidName(Kind)
				|| (Chronicle == null ? ChronicleOfficial != null || ChronicleOutsider != null
					: string.IsNullOrEmpty(ChronicleOfficial)
						|| ChronicleOfficial.Length > KingdomChronicleReceiptRules.MaxEntryChars
						|| string.IsNullOrEmpty(ChronicleOutsider)
						|| ChronicleOutsider.Length > KingdomChronicleReceiptRules.MaxEntryChars)
				|| !GrowthSinkDeclarationShape(Chronicle, ChronicleBeforeCount,
					ChronicleBeforeHash, ChronicleDeclaredAfterCount,
					ChronicleDeclaredAfterHash, KingdomChronicleReceiptRules.MaxEntries)
				|| !GrowthSinkDeclarationShape(Chronicle, OutsiderBeforeCount,
					OutsiderBeforeHash, OutsiderDeclaredAfterCount,
					OutsiderDeclaredAfterHash, KingdomChronicleReceiptRules.MaxEntries)
				|| !GrowthSinkDeclarationShape(Ledger, LedgerBeforeCount, LedgerBeforeHash,
					LedgerDeclaredAfterCount, LedgerDeclaredAfterHash)) return null;
			KingdomLifecycleOutbox box = PrepareGrowthOutbox(Operation, Chronicle, Ledger,
				Message, Deed, Guestbook);
			if (box == null) return null;
			string eventId = ChildId(Operation.Id, "outbox-event", Ordinal);
			box.EventId = eventId;
			box.ChronicleReceiptId = ChildId(eventId, "chronicle", 0);
			return new KingdomGrowthOutboxEvent
			{
				EventId = eventId, Kind = Kind, Outbox = box,
				ChronicleBeforeCount = ChronicleBeforeCount,
				ChronicleBeforeHash = ChronicleBeforeHash,
				ChronicleDeclaredAfterCount = ChronicleDeclaredAfterCount,
				ChronicleDeclaredAfterHash = ChronicleDeclaredAfterHash,
				ChronicleOfficial = ChronicleOfficial,
				ChronicleOutsider = ChronicleOutsider,
				OutsiderBeforeCount = OutsiderBeforeCount,
				OutsiderBeforeHash = OutsiderBeforeHash,
				OutsiderDeclaredAfterCount = OutsiderDeclaredAfterCount,
				OutsiderDeclaredAfterHash = OutsiderDeclaredAfterHash,
				LedgerBeforeCount = LedgerBeforeCount, LedgerBeforeHash = LedgerBeforeHash,
				LedgerDeclaredAfterCount = LedgerDeclaredAfterCount,
				LedgerDeclaredAfterHash = LedgerDeclaredAfterHash
			};
		}

		private static bool GrowthSinkDeclarationShape(string text, int beforeCount,
			string beforeHash, int afterCount, string afterHash, int boundedCount = -1)
		{
			if (text == null) return beforeCount == 0 && afterCount == 0
				&& beforeHash == null && afterHash == null;
			bool countShape = boundedCount < 0
				? beforeCount >= 0 && beforeCount < int.MaxValue
					&& afterCount == beforeCount + 1
				: beforeCount >= 0 && beforeCount <= boundedCount
					&& (beforeCount < boundedCount ? afterCount == beforeCount + 1
						: afterCount == beforeCount);
			return countShape && GrowthWitnessHash(beforeHash)
				&& GrowthWitnessHash(afterHash)
				&& !string.Equals(beforeHash, afterHash, StringComparison.Ordinal);
		}

		public static bool TryGrowthPlanHash(KingdomGrowthOperation Operation, out string Hash)
		{
			Hash = null;
			if (Operation == null) return false;
			try
			{
				Hash = HashId("growth-plan", delegate(BinaryWriter w)
				{
					w.Write(Operation.Sequence); CanonicalString(w, Operation.Id);
					w.Write((byte)Operation.Action); w.Write(Operation.CreatedTick);
					CanonicalString(w, Operation.SettlementId);
					CanonicalString(w, Operation.FieldId); CanonicalString(w, Operation.ZoneId);
					CanonicalString(w, Operation.TargetId); CanonicalString(w, Operation.TargetMarker);
					CanonicalString(w, Operation.Blueprint); w.Write((byte)Operation.TargetTopology);
					w.Write((byte)Operation.TargetLocation);
					CanonicalString(w, Operation.TargetOwnerId); w.Write(Operation.TargetX);
					w.Write(Operation.TargetY); w.Write((byte)Operation.OptionState);
					w.Write(Operation.OptionTick); w.Write((byte)Operation.HealthState);
					w.Write(Operation.HealthTick); w.Write(Operation.EffectiveWorkBefore);
					w.Write(Operation.EffectiveWorkAfter); w.Write(Operation.FieldClockBefore);
					w.Write(Operation.FieldClockAfter); w.Write(Operation.HeartbeatBefore);
					w.Write(Operation.HeartbeatAfter); w.Write(Operation.ArrivalBefore);
					w.Write(Operation.ArrivalAfter); w.Write(Operation.FetchBefore);
					w.Write(Operation.FetchAfter); w.Write(Operation.MillBefore);
					w.Write(Operation.MillAfter);
					CanonicalString(w, Operation.MillCropBlueprint);
					CanonicalString(w, Operation.MillStapleBlueprint);
					w.Write(Operation.SubsidenceBefore);
					w.Write(Operation.SubsidenceAfter); w.Write(Operation.DeliveryBefore);
					w.Write(Operation.DeliveryAfter); w.Write(Operation.DepartureBefore);
					w.Write(Operation.DepartureAfter); w.Write((byte)Operation.ArrivalDisposition);
					CanonicalString(w, Operation.ArrivalCandidateId);
					w.Write((byte)Operation.DeliveryMode);
					w.Write((byte)Operation.DepartureCauseKind);
					CanonicalString(w, Operation.DepartureCause);
					CanonicalString(w, Operation.DepartureNote);
					CanonicalString(w, Operation.DepartureName);
					CanonicalString(w, Operation.DepartureOrigin);
					w.Write(Operation.DepartureArrivedTick);
					CanonicalString(w, Operation.DepartureCreed);
					w.Write(Operation.DepartureChronicled);
					CanonicalString(w, Operation.TriggeredByOperationId);
					w.Write((byte)Operation.ScarcityOptionState);
					w.Write(Operation.ScarcityOptionTick); w.Write(Operation.PendingCropBefore);
					w.Write(Operation.PendingCropDelta); w.Write(Operation.PendingCropAfter);
					CanonicalString(w, Operation.PendingCropBlueprintBefore);
					CanonicalString(w, Operation.PendingCropZoneIdBefore);
					CanonicalString(w, Operation.PendingCropBlueprintAfter);
					CanonicalString(w, Operation.PendingCropZoneIdAfter);
					w.Write(Operation.PopulationBefore); w.Write(Operation.PopulationDelta);
					w.Write(Operation.PopulationAfter);
					w.Write(Operation.HarvestStandingRows);
					w.Write(Operation.HarvestRipeRows); w.Write(Operation.HarvestCycles);
					w.Write(Operation.HarvestCountsRipeLast);
					w.Write(Operation.HarvestEffectivenessPercent);
					w.Write(Operation.HarvestMethodPercent);
					w.Write(Operation.HarvestFirstOrdinal);
					CanonicalString(w, Operation.HarvestCropBlueprint);
					CanonicalString(w, Operation.HarvestSeedBlueprint);
					w.Write(Operation.WaterLegs == null ? -1 : Operation.WaterLegs.Count);
					if (Operation.WaterLegs != null) for (int i = 0;
						i < Operation.WaterLegs.Count; i++) WriteGrowthWaterPlan(w,
							Operation.WaterLegs[i]);
					w.Write(Operation.Sources == null ? -1 : Operation.Sources.Count);
					if (Operation.Sources != null) for (int i = 0; i < Operation.Sources.Count; i++)
						WriteGrowthObjectPlan(w, Operation.Sources[i]);
					w.Write(Operation.Outputs == null ? -1 : Operation.Outputs.Count);
					if (Operation.Outputs != null) for (int i = 0; i < Operation.Outputs.Count; i++)
						WriteGrowthObjectPlan(w, Operation.Outputs[i]);
					w.Write(Operation.DomainSteps == null ? -1 : Operation.DomainSteps.Count);
					if (Operation.DomainSteps != null) for (int i = 0;
						i < Operation.DomainSteps.Count; i++) WriteGrowthDomainPlan(w,
							Operation.DomainSteps[i]);
					WriteLeasePlan(w, Operation.ClockLease);
					w.Write(Operation.OutboxEvents == null ? -1 : Operation.OutboxEvents.Count);
					if (Operation.OutboxEvents != null) for (int i = 0;
						i < Operation.OutboxEvents.Count; i++) WriteGrowthOutboxEventPlan(w,
							Operation.OutboxEvents[i], Operation.LegacyGrowthV1Plan);
				});
				return ValidHashNamespace(Hash, "growth-plan");
			}
			catch (Exception)
			{
				Hash = null;
				return false;
			}
		}

		private static void WriteGrowthOutboxEventPlan(BinaryWriter w,
			KingdomGrowthOutboxEvent x, bool legacyV1 = false)
		{
			CanonicalString(w, x.EventId); CanonicalString(w, x.Kind);
			w.Write(x.ChronicleBeforeCount); w.Write(x.ChronicleDeclaredAfterCount);
			CanonicalString(w, x.ChronicleBeforeHash);
			CanonicalString(w, x.ChronicleDeclaredAfterHash);
			if (!legacyV1)
			{
				w.Write(x.LegacySingleRegisterChronicle);
				CanonicalString(w, x.ChronicleOfficial);
				CanonicalString(w, x.ChronicleOutsider);
				w.Write(x.OutsiderBeforeCount); w.Write(x.OutsiderDeclaredAfterCount);
				CanonicalString(w, x.OutsiderBeforeHash);
				CanonicalString(w, x.OutsiderDeclaredAfterHash);
			}
			w.Write(x.LedgerBeforeCount); w.Write(x.LedgerDeclaredAfterCount);
			CanonicalString(w, x.LedgerBeforeHash);
			CanonicalString(w, x.LedgerDeclaredAfterHash);
			WriteOutboxPlan(w, x.Outbox);
		}

		private static void WriteGrowthWaterPlan(BinaryWriter w, KingdomGrowthWaterLeg x)
		{
			CanonicalString(w, x.OperationId); CanonicalString(w, x.EventId);
			CanonicalString(w, x.LeaseKey); w.Write((byte)x.MutationKind);
			w.Write((byte)x.ContainerKind); CanonicalString(w, x.ContainerId);
			w.Write((byte)x.BeforeLocation); w.Write((byte)x.AfterLocation);
			CanonicalString(w, x.BeforeOwnerId); CanonicalString(w, x.AfterOwnerId);
			CanonicalString(w, x.BeforeZoneId); CanonicalString(w, x.AfterZoneId);
			w.Write(x.BeforeX); w.Write(x.BeforeY); w.Write(x.AfterX); w.Write(x.AfterY);
			w.Write(x.OwnerRemovedAfter);
			w.Write((byte)x.OwnerTopology); CanonicalString(w, x.OwnerId);
			CanonicalString(w, x.Blueprint); CanonicalString(w, x.ZoneId); w.Write(x.X);
			w.Write(x.Y); w.Write(x.Capacity); w.Write(x.Before); w.Write(x.Delta);
			w.Write(x.After); CanonicalString(w, x.BeforeComposition);
			CanonicalString(w, x.AfterComposition);
			CanonicalString(w, x.BeforeOwnerGraphHash); CanonicalString(w, x.AfterOwnerGraphHash);
			CanonicalString(w, x.BeforePartGraphHash); CanonicalString(w, x.AfterPartGraphHash);
			CanonicalString(w, x.BeforeTopologyHash); CanonicalString(w, x.AfterTopologyHash);
			CanonicalString(w, x.ReceiptId); WriteLeasePlan(w, x.Lease);
		}

		private static void WriteGrowthObjectPlan(BinaryWriter w, KingdomGrowthObjectLeg x)
		{
			CanonicalString(w, x.OperationId); CanonicalString(w, x.EventId);
			CanonicalString(w, x.MutationKind == KingdomGrowthObjectMutationKind.Create
				? null : x.ObjectId); CanonicalString(w, x.Marker);
			CanonicalString(w, x.Blueprint); CanonicalString(w, x.ZoneId);
			w.Write((byte)x.Topology); CanonicalString(w, x.OwnerId); w.Write(x.X); w.Write(x.Y);
			w.Write(x.BeforeCount); w.Write(x.Delta); w.Write(x.AfterCount); w.Write(x.NoStack);
			w.Write((byte)x.MutationKind); CanonicalString(w, x.BeforeOwnerGraphHash);
			CanonicalString(w, x.MutationKind == KingdomGrowthObjectMutationKind.Create
				? null : x.AfterOwnerGraphHash); CanonicalString(w, x.BeforeObjectGraphHash);
			CanonicalString(w, x.MutationKind == KingdomGrowthObjectMutationKind.Create
				? null : x.AfterObjectGraphHash); CanonicalString(w, x.BeforeTopologyHash);
			CanonicalString(w, x.MutationKind == KingdomGrowthObjectMutationKind.Create
				? null : x.AfterTopologyHash); CanonicalString(w, x.CreatedMarker);
			CanonicalString(w, x.DetachedMarker); CanonicalString(w, x.ReceiptId);
			CanonicalString(w, x.ReceiptTopologyId); w.Write((byte)x.BeforeLocation);
			w.Write((byte)x.AfterLocation); CanonicalString(w, x.EscrowKey);
			w.Write(x.Callbacks == null ? -1 : x.Callbacks.Count);
			if (x.Callbacks != null) for (int i = 0; i < x.Callbacks.Count; i++)
				WriteGrowthObjectCallbackPlan(w, x.Callbacks[i]);
			WriteLeasePlan(w, x.Lease);
		}

		private static void WriteGrowthObjectCallbackPlan(BinaryWriter w,
			KingdomGrowthObjectCallbackStep x)
		{
			CanonicalString(w, x.EventId); w.Write((byte)x.Kind); w.Write((byte)x.FromLocation);
			w.Write((byte)x.ToLocation); CanonicalString(w, x.EscrowKey);
			CanonicalString(w, x.BeforeOwnerId); CanonicalString(w, x.AfterOwnerId);
			CanonicalString(w, x.BeforeZoneId); CanonicalString(w, x.AfterZoneId);
			w.Write(x.BeforeX); w.Write(x.BeforeY); w.Write(x.AfterX); w.Write(x.AfterY);
			w.Write(x.BeforeCount); w.Write(x.AfterCount); w.Write(x.NoStack);
			w.Write(x.BeforeHasHarvestable); w.Write(x.AfterHasHarvestable);
			w.Write(x.BeforeRipe); w.Write(x.AfterRipe);
			w.Write(x.BeforeRegenTimer); w.Write(x.AfterRegenTimer);
			CanonicalString(w, x.BeforeRegenTime); CanonicalString(w, x.AfterRegenTime);
			w.Write(x.BeforeTileIndex); w.Write(x.AfterTileIndex);
			CanonicalString(w, x.BeforeRenderTile); CanonicalString(w, x.AfterRenderTile);
			CanonicalString(w, x.BeforeRenderColor); CanonicalString(w, x.AfterRenderColor);
			CanonicalString(w, x.BeforeRenderDetail); CanonicalString(w, x.AfterRenderDetail);
			CanonicalString(w, x.BeforeRenderString); CanonicalString(w, x.AfterRenderString);
			CanonicalString(w, x.BeforeTileColor); CanonicalString(w, x.AfterTileColor);
			// Callback graph witnesses are frozen immediately before each callback. They are
			// deliberately outside the operation plan because Create/replacement determines the
			// exact object graph and later placement witnesses only after the created ref exists.
			CanonicalString(w, x.ReceiptId);
		}

		private static void WriteGrowthDomainPlan(BinaryWriter w, KingdomGrowthDomainStep x)
		{
			w.Write((byte)x.Kind); w.Write((byte)x.CallbackKind);
			CanonicalString(w, x.CallbackBodyHash); CanonicalString(w, x.EventId);
			CanonicalString(w, x.ActorId);
			CanonicalString(w, x.SubjectId); w.Write(x.BeforeValue); w.Write(x.AfterValue);
			CanonicalString(w, x.BeforeGraphHash); CanonicalString(w, x.AfterGraphHash);
			CanonicalString(w, x.BeforeMapHash); CanonicalString(w, x.AfterMapHash);
			CanonicalString(w, x.ReceiptId); WriteLeasePlan(w, x.Lease);
			WriteGrowthScarcityPlan(w, x.ScarcityBefore);
			WriteGrowthScarcityPlan(w, x.ScarcityAfter);
			WriteGrowthAccountingPlan(w, x.AccountingBefore);
			WriteGrowthAccountingPlan(w, x.AccountingAfter);
			WriteGrowthFieldStatePlan(w, x.FieldBefore);
			WriteGrowthFieldStatePlan(w, x.FieldAfter);
			WriteGrowthCropRowsPlan(w, x.CropRowsBefore);
			WriteGrowthCropRowsPlan(w, x.CropRowsDeclaredAfter);
		}

		private static void WriteGrowthFieldStatePlan(BinaryWriter w,
			KingdomGrowthFieldState x)
		{
			w.Write(x != null); if (x == null) return;
			CanonicalString(w, x.FieldId); CanonicalString(w, x.WorkObjectId);
			CanonicalString(w, x.WorkPartId); CanonicalString(w, x.Marker);
			CanonicalString(w, x.Blueprint); CanonicalString(w, x.ZoneId);
			w.Write(x.X); w.Write(x.Y); CanonicalString(w, x.CropBlueprint);
			w.Write(x.Stage); w.Write(x.NextStageTick); w.Write(x.SownTick);
			w.Write(x.Cycles); w.Write(x.SaidWant); w.Write(x.DeclaredRows);
			w.Write(x.EffectivenessPercent); w.Write(x.MethodPercent);
			w.Write(x.NoLarderAnnounced); CanonicalString(w, x.SeedBlueprint);
			CanonicalString(w, x.PartGraphHash); CanonicalString(w, x.ObjectGraphHash);
			CanonicalString(w, x.TopologyHash);
		}

		private static void WriteGrowthCropRowsPlan(BinaryWriter w,
			List<KingdomGrowthCropRow> rows)
		{
			w.Write(rows != null); if (rows == null) return;
			w.Write(rows.Count);
			for (int i = 0; i < rows.Count; i++) WriteGrowthCropRowPlan(w, rows[i]);
		}

		private static void WriteGrowthCropRowPlan(BinaryWriter w, KingdomGrowthCropRow x)
		{
			w.Write(x != null); if (x == null) return;
			CanonicalString(w, x.FieldId); CanonicalString(w, x.RowId);
			CanonicalString(w, x.ObjectId); CanonicalString(w, x.Marker);
			CanonicalString(w, x.Blueprint); CanonicalString(w, x.ZoneId);
			CanonicalString(w, x.OwnerId); w.Write(x.X); w.Write(x.Y); w.Write(x.Count);
			w.Write(x.HasHarvestable); w.Write(x.Ripe); w.Write(x.RegenTimer);
			CanonicalString(w, x.RegenTime); w.Write(x.TileIndex);
			CanonicalString(w, x.RenderTile); CanonicalString(w, x.RenderColor);
			CanonicalString(w, x.RenderDetail); CanonicalString(w, x.RenderString);
			CanonicalString(w, x.TileColor); CanonicalString(w, x.PartGraphHash);
			CanonicalString(w, x.ObjectGraphHash); CanonicalString(w, x.TopologyHash);
			w.Write(x.Revision); CanonicalString(w, x.LastOperationId);
		}

		private static void WriteGrowthScarcityPlan(BinaryWriter w,
			KingdomGrowthScarcitySnapshot x)
		{
			w.Write(x != null); if (x == null) return;
			w.Write(x.DryStreak); w.Write(x.Withered); w.Write(x.HungerStreak);
			w.Write(x.Famished); w.Write((int)x.LastMeal); w.Write(x.MealShade);
			w.Write(x.ScrapsAnnounced); w.Write(x.ElapsedTicks); w.Write(x.Days);
			w.Write(x.Population); w.Write(x.Stage); w.Write(x.UpkeepRequested);
			w.Write(x.WaterAvailable);
			w.Write(x.RationsAvailable); w.Write(x.Foraged); w.Write(x.Eaten);
			w.Write(x.FromDish); w.Write(x.Kitchens); CanonicalString(w, x.DishName);
			CanonicalString(w, x.DishText); CanonicalString(w, x.DishStaple);
			CanonicalString(w, x.DishSource);
			w.Write((byte)x.ComposedBite); w.Write(x.RequestedWater);
			w.Write(x.ProvedWater); w.Write(x.RequestedRations); w.Write(x.ProvedRations);
			w.Write(x.StoresPolicy); w.Write(x.DistrictPercent); w.Write((byte)x.ThirstOutcome);
			w.Write((byte)x.HungerOutcome); w.Write(x.Thirsting); w.Write(x.Starving);
			w.Write(x.Withering); w.Write(x.Famishing); w.Write(x.Healthy);
		}

		private static void WriteGrowthAccountingPlan(BinaryWriter w,
			KingdomGrowthAccountingSnapshot x)
		{
			w.Write(x != null); if (x == null) return;
			w.Write(x.Fetched); w.Write(x.UpkeepDrawn); w.Write(x.ArrivalCost);
			w.Write(x.Delivered); w.Write(x.Harvested); w.Write(x.Foraged);
			w.Write(x.RationsDrawn); w.Write(x.Milled); w.Write(x.HarvestLost);
			w.Write(x.Plundered); w.Write(x.Arrivals); w.Write(x.Departures);
		}

		public static bool TryPublishGrowth(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation)
		{
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| Operation == null || Operation.LegacyGrowthV1Plan) return false;
			KingdomGrowthSlotKind slot = SlotForGrowthAction(Operation.Action);
			KingdomGrowthFieldSlot field = slot == KingdomGrowthSlotKind.Field
				? FindGrowthField(Book, Operation.FieldId) : null;
			if (slot == KingdomGrowthSlotKind.None || (slot == KingdomGrowthSlotKind.Field
				&& (field == null || field.Quarantined)) || GetGrowthOperation(Book, slot,
					Operation.FieldId) != null || Operation.Sequence != GetGrowthNext(Book, slot, field)
				|| !IsExactSuccessor(Operation.Sequence, GetGrowthRetired(Book, slot, field))
				|| Operation.Sequence == long.MaxValue
				|| !GrowthOperationShape(Book, Operation, slot, Operation.FieldId, true)
				|| !GrowthPublicationSnapshotsMatch(Book, Operation, field)
				|| !GrowthActiveIdentityClaimsValid(Book, Operation)) return false;
			string hash;
			if (!TryGrowthPlanHash(Operation, out hash)) return false;
			List<KingdomLifecycleResourceLease> leases = GrowthLeases(Operation);
			if (leases == null) return false;
			List<KingdomLifecycleResourceRevision> rows =
				new List<KingdomLifecycleResourceRevision>(leases.Count);
			List<bool> createdRows = new List<bool>(leases.Count);
			List<string> activeBefore = new List<string>(leases.Count);
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			int newRows = 0;
			for (int i = 0; i < leases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = leases[i];
				if (!GrowthLeaseShape(lease, Operation.Id, true) || !keys.Add(lease.Key)) return false;
				KingdomLifecycleResourceRevision row = FindGrowthResource(Book, lease.Key);
				bool created = row == null;
				if (row == null)
				{
					row = new KingdomLifecycleResourceRevision
					{
						Kind = lease.Kind, ScopeId = lease.ScopeId, SubjectId = lease.SubjectId,
						Key = lease.Key, Revision = 0L
					};
					newRows++;
				}
				if (!GrowthResourceMatches(row, lease) || row.Revision != lease.BeforeRevision
					|| !string.IsNullOrEmpty(row.ActiveOperationId)
					|| string.Equals(row.LastOperationId, Operation.Id, StringComparison.Ordinal))
					return false;
				rows.Add(row);
				createdRows.Add(created);
				activeBefore.Add(row.ActiveOperationId);
			}
			if (Book.Resources.Count + newRows > MaxResourceRows) return false;
			string planHashBefore = Operation.PlanHash;
			Operation.PlanHash = hash;
			for (int i = 0; i < rows.Count; i++)
			{
				if (FindGrowthResource(Book, rows[i].Key) == null) Book.Resources.Add(rows[i]);
				rows[i].ActiveOperationId = Operation.Id;
			}
			SetGrowthNext(Book, slot, field, Operation.Sequence + 1L);
			SetGrowthOperation(Book, slot, field, Operation);
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			SetGrowthOperation(Book, slot, field, null);
			SetGrowthNext(Book, slot, field, Operation.Sequence);
			for (int i = rows.Count - 1; i >= 0; i--)
			{
				if (createdRows[i]) Book.Resources.Remove(rows[i]);
				else rows[i].ActiveOperationId = activeBefore[i];
			}
			Operation.PlanHash = planHashBefore;
			return false;
		}

		public static bool CanTransitionGrowth(KingdomGrowthAction Action,
			KingdomGrowthPhase From, KingdomGrowthPhase To)
		{
			// Quarantine publication owns a bounded fault and retained evidence. The generic
			// phase mover cannot construct that receipt, so it must never enter this phase.
			if (To == KingdomGrowthPhase.Quarantined) return false;
			KingdomGrowthPhase next;
			return TryNextGrowthPhase(Action, From, out next) && next == To;
		}

		public static bool CanTransitionGrowth(KingdomGrowthOperation Operation,
			KingdomGrowthPhase From, KingdomGrowthPhase To)
		{
			if (Operation == null || To == KingdomGrowthPhase.Quarantined) return false;
			KingdomGrowthPhase next;
			return TryNextGrowthPhase(Operation, From, out next) && next == To;
		}

		public static bool AdvanceGrowthPhase(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, KingdomGrowthPhase To, long Tick)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation) || Tick < Operation.UpdatedTick
				|| !CanTransitionGrowth(Operation, Operation.Phase, To)
				|| !GrowthTransitionReady(Book, Operation, To)) return false;
			KingdomGrowthPhase phaseBefore = Operation.Phase;
			long tickBefore = Operation.UpdatedTick;
			Operation.Phase = To; Operation.UpdatedTick = Tick;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			Operation.Phase = phaseBefore; Operation.UpdatedTick = tickBefore;
			return false;
		}

		public static bool QuarantineGrowthOperation(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, string Fault)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation)
				|| Operation.Phase == KingdomGrowthPhase.Quarantined
				|| string.IsNullOrEmpty(Fault) || TooLong(Fault, MaxTextChars)) return false;
			KingdomGrowthPhase before = Operation.Phase;
			string beforeFault = Operation.Fault;
			Operation.Phase = KingdomGrowthPhase.Quarantined;
			Operation.Fault = SafeFault(Fault);
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			Operation.Phase = before; Operation.Fault = beforeFault;
			return false;
		}

		public static KingdomLifecycleCasAction GrowthClockAction(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, long CurrentValue)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation)
				|| Operation.Phase != KingdomGrowthPhase.ClockIntent) return KingdomLifecycleCasAction.Quarantine;
			KingdomLifecycleResourceLease lease = Operation.ClockLease;
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, lease.Key);
			if (!GrowthResourceMatches(row, lease)
				|| !string.Equals(row.ActiveOperationId, Operation.Id, StringComparison.Ordinal)
				|| CurrentValue < GrowthClockValue(Book, Operation.Action,
					SlotForGrowthAction(Operation.Action) == KingdomGrowthSlotKind.Field
						? FindGrowthField(Book, Operation.FieldId) : null))
				return KingdomLifecycleCasAction.Quarantine;
			if (Operation.ClockState == KingdomLifecyclePhysicalState.Prepared
				&& lease.State == KingdomLifecycleLeaseState.Prepared)
				return CurrentValue == lease.Before && row.Revision == lease.BeforeRevision
					&& !string.Equals(row.LastOperationId, Operation.Id, StringComparison.Ordinal)
					? KingdomLifecycleCasAction.Apply : KingdomLifecycleCasAction.Quarantine;
			if ((Operation.ClockState == KingdomLifecyclePhysicalState.Intent
				|| Operation.ClockState == KingdomLifecyclePhysicalState.Proved)
				&& (lease.State == KingdomLifecycleLeaseState.Intent
					|| lease.State == KingdomLifecycleLeaseState.Proved))
				return CurrentValue == lease.After && (row.Revision == lease.BeforeRevision
					|| row.Revision == lease.AfterRevision) ? KingdomLifecycleCasAction.Confirm
					: KingdomLifecycleCasAction.Quarantine;
			return KingdomLifecycleCasAction.Quarantine;
		}

		internal static bool BeginGrowthClock(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, long CurrentValue)
		{
			if (GrowthClockAction(Book, Operation, CurrentValue) != KingdomLifecycleCasAction.Apply)
				return false;
			Operation.ClockState = KingdomLifecyclePhysicalState.Intent;
			Operation.ClockLease.State = KingdomLifecycleLeaseState.Intent;
			return true;
		}

		internal static bool CommitGrowthClockWitness(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, long CurrentValue)
		{
			if (GrowthClockAction(Book, Operation, CurrentValue) != KingdomLifecycleCasAction.Confirm
				|| Operation.ClockState != KingdomLifecyclePhysicalState.Intent
				|| Operation.ClockLease.State != KingdomLifecycleLeaseState.Intent) return false;
			KingdomLifecycleResourceLease lease = Operation.ClockLease;
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, lease.Key);
			if (row.Revision != lease.BeforeRevision
				|| string.Equals(row.LastOperationId, Operation.Id, StringComparison.Ordinal)) return false;
			long rowRevisionBefore = row.Revision;
			string rowLastOperationBefore = row.LastOperationId;
			KingdomLifecycleLeaseState leaseStateBefore = lease.State;
			KingdomLifecyclePhysicalState clockStateBefore = Operation.ClockState;
			long heartbeatBefore = Book.LastHeartbeatTick;
			long arrivalBefore = Book.NextArrivalTick;
			long fetchBefore = Book.LastFetchTick;
			long millBefore = Book.LastMillTick;
			long subsidenceBefore = Book.LastSubsidenceTick;
			long deliveryBefore = Book.LastDeliveryTick;
			long departureBefore = Book.LastDepartureTick;
			long effectiveBefore = Book.EffectiveWorkTick;
			KingdomGrowthFieldSlot field = SlotForGrowthAction(Operation.Action) ==
				KingdomGrowthSlotKind.Field ? FindGrowthField(Book, Operation.FieldId) : null;
			long fieldClockBefore = field == null ? 0L : field.ClockTick;
			long fieldRevisionBefore = field == null ? 0L : field.CommitRevision;
			string fieldLastOperationBefore = field == null ? null : field.LastOperationId;
			row.Revision = lease.AfterRevision; row.LastOperationId = Operation.Id;
			lease.State = KingdomLifecycleLeaseState.Proved;
			Operation.ClockState = KingdomLifecyclePhysicalState.Proved;
			ApplyGrowthClockValue(Book, Operation);
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			row.Revision = rowRevisionBefore; row.LastOperationId = rowLastOperationBefore;
			lease.State = leaseStateBefore; Operation.ClockState = clockStateBefore;
			Book.LastHeartbeatTick = heartbeatBefore; Book.NextArrivalTick = arrivalBefore;
			Book.LastFetchTick = fetchBefore; Book.LastMillTick = millBefore;
			Book.LastSubsidenceTick = subsidenceBefore;
			Book.LastDeliveryTick = deliveryBefore; Book.LastDepartureTick = departureBefore;
			Book.EffectiveWorkTick = effectiveBefore;
			if (field != null)
			{
				field.ClockTick = fieldClockBefore;
				field.CommitRevision = fieldRevisionBefore;
				field.LastOperationId = fieldLastOperationBefore;
			}
			return false;
		}

		internal static KingdomLifecycleCasAction GrowthInspectableOutboxAction(
			KingdomGrowthBook Book, KingdomGrowthOperation Operation, int EventOrdinal,
			KingdomGrowthOutboxSinkKind Sink, int ObservedCount, string ObservedHash)
		{
			KingdomGrowthOutboxEvent e = GrowthOutboxEventAt(Book, Operation, EventOrdinal);
			if (e == null || (Sink != KingdomGrowthOutboxSinkKind.Chronicle
				&& Sink != KingdomGrowthOutboxSinkKind.Ledger))
				return KingdomLifecycleCasAction.Quarantine;
			if (Sink == KingdomGrowthOutboxSinkKind.Chronicle
				&& !e.LegacySingleRegisterChronicle)
				return KingdomLifecycleCasAction.Quarantine;
			KingdomLifecycleSinkState state = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.Outbox.ChronicleState : e.Outbox.LedgerState;
			string text = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.Outbox.Chronicle : e.Outbox.Ledger;
			if (text == null) return state == KingdomLifecycleSinkState.Skipped
				? KingdomLifecycleCasAction.Confirm : KingdomLifecycleCasAction.Quarantine;
			int beforeCount = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.ChronicleBeforeCount : e.LedgerBeforeCount;
			string beforeHash = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.ChronicleBeforeHash : e.LedgerBeforeHash;
			int afterCount = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.ChronicleDeclaredAfterCount : e.LedgerDeclaredAfterCount;
			string afterHash = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.ChronicleDeclaredAfterHash : e.LedgerDeclaredAfterHash;
			bool before = ObservedCount == beforeCount
				&& string.Equals(ObservedHash, beforeHash, StringComparison.Ordinal);
			bool after = ObservedCount == afterCount
				&& string.Equals(ObservedHash, afterHash, StringComparison.Ordinal);
			if (state == KingdomLifecycleSinkState.Pending
				|| state == KingdomLifecycleSinkState.Intent)
				return before ? KingdomLifecycleCasAction.Apply
					: state == KingdomLifecycleSinkState.Intent && after
						? KingdomLifecycleCasAction.Confirm
						: KingdomLifecycleCasAction.Quarantine;
			if (state == KingdomLifecycleSinkState.Delivered)
				return after ? KingdomLifecycleCasAction.Confirm
					: KingdomLifecycleCasAction.Quarantine;
			return KingdomLifecycleCasAction.Quarantine;
		}

		internal static KingdomLifecycleCasAction GrowthChronicleOutboxAction(
			KingdomGrowthBook Book, KingdomGrowthOperation Operation, int EventOrdinal,
			int ChronicleCount, string ChronicleHash, int OutsiderCount, string OutsiderHash)
		{
			KingdomGrowthOutboxEvent e = GrowthOutboxEventAt(Book, Operation, EventOrdinal);
			if (e == null || e.LegacySingleRegisterChronicle || e.Outbox.Chronicle == null)
				return KingdomLifecycleCasAction.Quarantine;
			KingdomLifecycleSinkState state = e.Outbox.ChronicleState;
			bool before = ChronicleCount == e.ChronicleBeforeCount
				&& string.Equals(ChronicleHash, e.ChronicleBeforeHash, StringComparison.Ordinal)
				&& OutsiderCount == e.OutsiderBeforeCount
				&& string.Equals(OutsiderHash, e.OutsiderBeforeHash, StringComparison.Ordinal);
			bool after = ChronicleCount == e.ChronicleDeclaredAfterCount
				&& string.Equals(ChronicleHash, e.ChronicleDeclaredAfterHash,
					StringComparison.Ordinal)
				&& OutsiderCount == e.OutsiderDeclaredAfterCount
				&& string.Equals(OutsiderHash, e.OutsiderDeclaredAfterHash,
					StringComparison.Ordinal);
			bool orderedCut = ChronicleCount == e.ChronicleDeclaredAfterCount
				&& string.Equals(ChronicleHash, e.ChronicleDeclaredAfterHash,
					StringComparison.Ordinal)
				&& OutsiderCount == e.OutsiderBeforeCount
				&& string.Equals(OutsiderHash, e.OutsiderBeforeHash,
					StringComparison.Ordinal);
			if (state == KingdomLifecycleSinkState.Pending
				|| state == KingdomLifecycleSinkState.Intent)
				return before ? KingdomLifecycleCasAction.Apply
					: state == KingdomLifecycleSinkState.Intent && orderedCut
						? KingdomLifecycleCasAction.Apply
					: state == KingdomLifecycleSinkState.Intent && after
						? KingdomLifecycleCasAction.Confirm
						: KingdomLifecycleCasAction.Quarantine;
			return state == KingdomLifecycleSinkState.Delivered && after
				? KingdomLifecycleCasAction.Confirm : KingdomLifecycleCasAction.Quarantine;
		}

		internal static bool BeginGrowthChronicleOutbox(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int EventOrdinal, int ChronicleBeforeCount,
			string ChronicleBeforeHash, int OutsiderBeforeCount, string OutsiderBeforeHash)
		{
			if (GrowthChronicleOutboxAction(Book, Operation, EventOrdinal,
				ChronicleBeforeCount, ChronicleBeforeHash, OutsiderBeforeCount,
				OutsiderBeforeHash) != KingdomLifecycleCasAction.Apply) return false;
			KingdomGrowthOutboxEvent e = Operation.OutboxEvents[EventOrdinal];
			KingdomLifecycleSinkState old = e.Outbox.ChronicleState;
			e.Outbox.ChronicleState = KingdomLifecycleSinkState.Intent;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			e.Outbox.ChronicleState = old; return false;
		}

		internal static bool CommitGrowthChronicleOutbox(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int EventOrdinal, int ChronicleObservedCount,
			string ChronicleObservedHash, int OutsiderObservedCount, string OutsiderObservedHash)
		{
			if (GrowthChronicleOutboxAction(Book, Operation, EventOrdinal,
				ChronicleObservedCount, ChronicleObservedHash, OutsiderObservedCount,
				OutsiderObservedHash) != KingdomLifecycleCasAction.Confirm) return false;
			KingdomGrowthOutboxEvent e = Operation.OutboxEvents[EventOrdinal];
			KingdomLifecycleSinkState oldState = e.Outbox.ChronicleState;
			int oldChronicleCount = e.ChronicleObservedCount;
			string oldChronicleHash = e.ChronicleObservedHash;
			int oldOutsiderCount = e.OutsiderObservedCount;
			string oldOutsiderHash = e.OutsiderObservedHash;
			e.Outbox.ChronicleState = KingdomLifecycleSinkState.Delivered;
			e.ChronicleObservedCount = ChronicleObservedCount;
			e.ChronicleObservedHash = ChronicleObservedHash;
			e.OutsiderObservedCount = OutsiderObservedCount;
			e.OutsiderObservedHash = OutsiderObservedHash;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			e.Outbox.ChronicleState = oldState;
			e.ChronicleObservedCount = oldChronicleCount;
			e.ChronicleObservedHash = oldChronicleHash;
			e.OutsiderObservedCount = oldOutsiderCount;
			e.OutsiderObservedHash = oldOutsiderHash;
			return false;
		}

		internal static bool BeginGrowthInspectableOutbox(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int EventOrdinal,
			KingdomGrowthOutboxSinkKind Sink, int BeforeCount, string BeforeHash)
		{
			if (GrowthInspectableOutboxAction(Book, Operation, EventOrdinal, Sink,
				BeforeCount, BeforeHash) != KingdomLifecycleCasAction.Apply) return false;
			KingdomGrowthOutboxEvent e = Operation.OutboxEvents[EventOrdinal];
			KingdomLifecycleSinkState old = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.Outbox.ChronicleState : e.Outbox.LedgerState;
			if (Sink == KingdomGrowthOutboxSinkKind.Chronicle)
				e.Outbox.ChronicleState = KingdomLifecycleSinkState.Intent;
			else e.Outbox.LedgerState = KingdomLifecycleSinkState.Intent;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			if (Sink == KingdomGrowthOutboxSinkKind.Chronicle) e.Outbox.ChronicleState = old;
			else e.Outbox.LedgerState = old;
			return false;
		}

		internal static bool CommitGrowthInspectableOutbox(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int EventOrdinal,
			KingdomGrowthOutboxSinkKind Sink, int ObservedCount, string ObservedHash)
		{
			if (GrowthInspectableOutboxAction(Book, Operation, EventOrdinal, Sink,
				ObservedCount, ObservedHash) != KingdomLifecycleCasAction.Confirm) return false;
			KingdomGrowthOutboxEvent e = Operation.OutboxEvents[EventOrdinal];
			KingdomLifecycleSinkState oldState = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.Outbox.ChronicleState : e.Outbox.LedgerState;
			int oldCount = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.ChronicleObservedCount : e.LedgerObservedCount;
			string oldHash = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.ChronicleObservedHash : e.LedgerObservedHash;
			if (Sink == KingdomGrowthOutboxSinkKind.Chronicle)
			{
				e.Outbox.ChronicleState = KingdomLifecycleSinkState.Delivered;
				e.ChronicleObservedCount = ObservedCount;
				e.ChronicleObservedHash = ObservedHash;
			}
			else
			{
				e.Outbox.LedgerState = KingdomLifecycleSinkState.Delivered;
				e.LedgerObservedCount = ObservedCount;
				e.LedgerObservedHash = ObservedHash;
			}
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			if (Sink == KingdomGrowthOutboxSinkKind.Chronicle)
			{
				e.Outbox.ChronicleState = oldState; e.ChronicleObservedCount = oldCount;
				e.ChronicleObservedHash = oldHash;
			}
			else
			{
				e.Outbox.LedgerState = oldState; e.LedgerObservedCount = oldCount;
				e.LedgerObservedHash = oldHash;
			}
			return false;
		}

		internal static bool BeginGrowthAtMostOnceOutbox(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int EventOrdinal,
			KingdomGrowthOutboxSinkKind Sink)
		{
			KingdomGrowthOutboxEvent e = GrowthOutboxEventAt(Book, Operation, EventOrdinal);
			if (e == null || (Sink != KingdomGrowthOutboxSinkKind.Message
				&& Sink != KingdomGrowthOutboxSinkKind.Deed
				&& Sink != KingdomGrowthOutboxSinkKind.Guestbook)) return false;
			KingdomLifecycleSinkState old = GrowthOutboxSinkState(e.Outbox, Sink);
			if (old != KingdomLifecycleSinkState.Pending) return false;
			SetGrowthOutboxSinkState(e.Outbox, Sink, KingdomLifecycleSinkState.Intent);
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			SetGrowthOutboxSinkState(e.Outbox, Sink, old); return false;
		}

		internal static bool CommitGrowthAtMostOnceOutbox(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int EventOrdinal,
			KingdomGrowthOutboxSinkKind Sink)
		{
			KingdomGrowthOutboxEvent e = GrowthOutboxEventAt(Book, Operation, EventOrdinal);
			if (e == null || (Sink != KingdomGrowthOutboxSinkKind.Message
				&& Sink != KingdomGrowthOutboxSinkKind.Deed
				&& Sink != KingdomGrowthOutboxSinkKind.Guestbook)
				|| GrowthOutboxSinkState(e.Outbox, Sink) != KingdomLifecycleSinkState.Intent)
				return false;
			SetGrowthOutboxSinkState(e.Outbox, Sink, KingdomLifecycleSinkState.Delivered);
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			SetGrowthOutboxSinkState(e.Outbox, Sink, KingdomLifecycleSinkState.Intent);
			return false;
		}

		internal static bool RecoverGrowthOutbox(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation)
				|| Operation.Phase != KingdomGrowthPhase.Sinks
				|| Operation.OutboxEvents == null) return false;
			List<KingdomLifecycleSinkState> old = new List<KingdomLifecycleSinkState>();
			for (int i = 0; i < Operation.OutboxEvents.Count; i++)
			{
				KingdomLifecycleOutbox box = Operation.OutboxEvents[i].Outbox;
				old.Add(box.MessageState); old.Add(box.DeedState); old.Add(box.GuestbookState);
				if (box.MessageState == KingdomLifecycleSinkState.Intent)
					box.MessageState = KingdomLifecycleSinkState.Lost;
				if (box.DeedState == KingdomLifecycleSinkState.Intent)
					box.DeedState = KingdomLifecycleSinkState.Lost;
				if (box.GuestbookState == KingdomLifecycleSinkState.Intent)
					box.GuestbookState = KingdomLifecycleSinkState.Lost;
			}
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			int p = 0;
			for (int i = 0; i < Operation.OutboxEvents.Count; i++)
			{
				KingdomLifecycleOutbox box = Operation.OutboxEvents[i].Outbox;
				box.MessageState = old[p++]; box.DeedState = old[p++];
				box.GuestbookState = old[p++];
			}
			return false;
		}

		private static KingdomGrowthOutboxEvent GrowthOutboxEventAt(KingdomGrowthBook book,
			KingdomGrowthOperation operation, int ordinal)
		{
			return ExactGrowthOperationAuthority(book, operation)
				&& operation.Phase == KingdomGrowthPhase.Sinks
				&& operation.OutboxEvents != null && ordinal >= 0
				&& ordinal < operation.OutboxEvents.Count ? operation.OutboxEvents[ordinal] : null;
		}

		private static KingdomLifecycleSinkState GrowthOutboxSinkState(
			KingdomLifecycleOutbox box, KingdomGrowthOutboxSinkKind sink)
		{
			switch (sink)
			{
			case KingdomGrowthOutboxSinkKind.Message: return box.MessageState;
			case KingdomGrowthOutboxSinkKind.Deed: return box.DeedState;
			case KingdomGrowthOutboxSinkKind.Guestbook: return box.GuestbookState;
			default: return KingdomLifecycleSinkState.None;
			}
		}

		private static void SetGrowthOutboxSinkState(KingdomLifecycleOutbox box,
			KingdomGrowthOutboxSinkKind sink, KingdomLifecycleSinkState state)
		{
			switch (sink)
			{
			case KingdomGrowthOutboxSinkKind.Message: box.MessageState = state; break;
			case KingdomGrowthOutboxSinkKind.Deed: box.DeedState = state; break;
			case KingdomGrowthOutboxSinkKind.Guestbook: box.GuestbookState = state; break;
			}
		}

		public static bool RetireGrowth(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, long Tick)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation) || Tick < Operation.UpdatedTick
				|| Operation.Phase != KingdomGrowthPhase.Terminal || !GrowthOutboxTerminal(Operation)
				|| !GrowthAllResourcesProved(Book, Operation)) return false;
			KingdomGrowthSlotKind slot = SlotForGrowthAction(Operation.Action);
			KingdomGrowthFieldSlot field = slot == KingdomGrowthSlotKind.Field
				? FindGrowthField(Book, Operation.FieldId) : null;
			if (!IsExactSuccessor(Operation.Sequence, GetGrowthRetired(Book, slot, field))) return false;
			List<KingdomLifecycleResourceLease> leases = GrowthLeases(Operation);
			KingdomGrowthProof proof = new KingdomGrowthProof
			{
				Slot = slot, FieldId = Operation.FieldId, Sequence = Operation.Sequence,
				Id = Operation.Id, PlanHash = Operation.PlanHash, Action = Operation.Action, Tick = Tick
			};
			if (leases == null || !GrowthProofAppendWouldBeValid(Book, proof, slot, field)) return false;
			long retiredBefore = GetGrowthRetired(Book, slot, field);
			long arrivalBefore = Book.NextArrivalTick;
			List<KingdomGrowthProof> proofsBefore =
				new List<KingdomGrowthProof>(Book.RecentProofs);
			for (int i = 0; i < leases.Count; i++)
				FindGrowthResource(Book, leases[i].Key).ActiveOperationId = null;
			SetGrowthRetired(Book, slot, field, Operation.Sequence);
			AppendGrowthProof(Book, proof);
			SetGrowthOperation(Book, slot, field, null);
			if (slot == KingdomGrowthSlotKind.Arrival && Book.WorkPaused
				&& Book.ArrivalCandidate == null)
				Book.NextArrivalTick = 0L;
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			for (int i = 0; i < leases.Count; i++)
				FindGrowthResource(Book, leases[i].Key).ActiveOperationId = Operation.Id;
			SetGrowthRetired(Book, slot, field, retiredBefore);
			Book.RecentProofs.Clear(); Book.RecentProofs.AddRange(proofsBefore);
			SetGrowthOperation(Book, slot, field, Operation);
			Book.NextArrivalTick = arrivalBefore;
			return false;
		}

		public static bool QuarantineGrowthField(KingdomGrowthBook Book, string FieldId,
			string Fault)
		{
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| !ValidRootId(FieldId) || string.IsNullOrEmpty(Fault)
				|| TooLong(Fault, MaxTextChars)) return false;
			KingdomGrowthFieldSlot field = FindGrowthField(Book, FieldId);
			if (field == null || field.Quarantined) return false;
			bool oldQuarantined = field.Quarantined;
			string oldFault = field.Fault;
			field.Quarantined = true; field.Fault = SafeFault(Fault);
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			field.Quarantined = oldQuarantined; field.Fault = oldFault;
			return false;
		}

		public static KingdomGrowthWaterLeg PrepareGrowthWaterLeg(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, KingdomGrowthWaterMutationKind MutationKind,
			string ContainerId, KingdomLifecycleTopology OwnerTopology, string OwnerId,
			string Blueprint, string ZoneId, int X, int Y, int Capacity, int Before, int Delta,
			string BeforeComposition, string AfterComposition, string BeforeOwnerGraphHash,
			string AfterOwnerGraphHash, string BeforePartGraphHash, string AfterPartGraphHash,
			string BeforeTopologyHash, string AfterTopologyHash,
			bool OwnerRemovedAfter = false)
		{
			if (OwnerTopology == KingdomLifecycleTopology.Cell && OwnerId != null
				&& OwnerId.Length == 0) OwnerId = null;
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| Operation == null || Operation.Phase != KingdomGrowthPhase.Prepared
				|| Operation.PlanHash != null || Operation.WaterLegs == null
				|| Operation.WaterLegs.Count >= MaxWaterLegs) return null;
			int ordinal = Operation.WaterLegs.Count;
			int after;
			if (Delta <= 0 || !CheckedAdd(Before,
				MutationKind == KingdomGrowthWaterMutationKind.Drain ? -Delta : Delta, out after))
				return null;
			string key = ResourceKey(KingdomLifecycleResourceKind.WaterVessel, ZoneId, ContainerId);
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, key);
			if (key == null || (row != null && (!string.IsNullOrEmpty(row.ActiveOperationId)
				|| row.Revision == long.MaxValue))) return null;
			long revision = row == null ? 0L : row.Revision;
			KingdomGrowthLocationKind location = GrowthLocationFromTopology(OwnerTopology);
			if (OwnerRemovedAfter && (MutationKind != KingdomGrowthWaterMutationKind.Drain
				|| after != 0)) return null;
			KingdomGrowthWaterLeg leg = new KingdomGrowthWaterLeg
			{
				OperationId = Operation.Id, EventId = ChildId(Operation.Id, "water", ordinal),
				LeaseKey = key, MutationKind = MutationKind,
					ContainerKind = KingdomGrowthWaterContainerKind.LiquidVolume,
					ContainerId = ContainerId, OwnerTopology = OwnerTopology, OwnerId = OwnerId,
					BeforeLocation = location,
					AfterLocation = OwnerRemovedAfter ? KingdomGrowthLocationKind.Graveyard : location,
					BeforeOwnerId = OwnerId, AfterOwnerId = OwnerId,
					BeforeZoneId = ZoneId, AfterZoneId = ZoneId,
					BeforeX = X, BeforeY = Y, AfterX = X, AfterY = Y,
					OwnerRemovedAfter = OwnerRemovedAfter,
				Blueprint = Blueprint, ZoneId = ZoneId, X = X, Y = Y, Capacity = Capacity,
				Before = Before, Delta = Delta, After = after,
				BeforeComposition = BeforeComposition, AfterComposition = AfterComposition,
				BeforeOwnerGraphHash = BeforeOwnerGraphHash,
				AfterOwnerGraphHash = AfterOwnerGraphHash,
				BeforePartGraphHash = BeforePartGraphHash,
				AfterPartGraphHash = AfterPartGraphHash,
				BeforeTopologyHash = BeforeTopologyHash, AfterTopologyHash = AfterTopologyHash,
					State = KingdomLifecyclePhysicalState.Prepared,
				ReceiptId = ChildId(Operation.Id, "water-receipt", ordinal),
				ReceiptState = KingdomLifecyclePhysicalState.Prepared,
				Lease = new KingdomLifecycleResourceLease
				{
					OperationId = Operation.Id, Kind = KingdomLifecycleResourceKind.WaterVessel,
					ScopeId = ZoneId, SubjectId = ContainerId, Key = key, Before = Before,
					Delta = MutationKind == KingdomGrowthWaterMutationKind.Drain ? -Delta : Delta,
					After = after, BeforeRevision = revision, AfterRevision = revision + 1L,
					State = KingdomLifecycleLeaseState.Prepared
				}
			};
			if (OwnerRemovedAfter)
			{
				leg.AfterOwnerId = null; leg.AfterZoneId = null;
				leg.AfterX = -1; leg.AfterY = -1;
			}
			return GrowthWaterShape(Operation, leg, ordinal, true) ? leg : null;
		}

		public static KingdomGrowthObjectLeg PrepareGrowthObjectLeg(
			KingdomGrowthBook Book, KingdomGrowthOperation Operation, bool Output,
			KingdomGrowthObjectMutationKind MutationKind, string ObjectId, string Marker,
			string Blueprint, KingdomLifecycleTopology Topology, string OwnerId, string ZoneId,
			int X, int Y, int BeforeCount, int Delta, bool NoStack,
			string BeforeOwnerGraphHash, string AfterOwnerGraphHash,
			string BeforeObjectGraphHash, string AfterObjectGraphHash,
			string BeforeTopologyHash, string AfterTopologyHash)
		{
			if (Topology == KingdomLifecycleTopology.Cell && OwnerId != null
				&& OwnerId.Length == 0) OwnerId = null;
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| Operation == null || Operation.Phase != KingdomGrowthPhase.Prepared
				|| !string.Equals(Operation.SettlementId, Book.SettlementId,
					StringComparison.Ordinal)
				|| Operation.PlanHash != null || Operation.Sources == null || Operation.Outputs == null)
				return null;
			if (MutationKind == KingdomGrowthObjectMutationKind.Create && ObjectId != null)
				return null;
			if (MutationKind == KingdomGrowthObjectMutationKind.CellAdd
				&& Topology != KingdomLifecycleTopology.Cell) return null;
			if ((MutationKind == KingdomGrowthObjectMutationKind.InventoryAdd
				|| MutationKind == KingdomGrowthObjectMutationKind.Receive)
				&& Topology != KingdomLifecycleTopology.Inventory) return null;
			List<KingdomGrowthObjectLeg> list = Output ? Operation.Outputs : Operation.Sources;
			if (list.Count >= (Output ? MaxGrowthOutputs : MaxGrowthSources)) return null;
			int after;
			if (!CheckedAdd(BeforeCount, Delta, out after)) return null;
			int ordinal = list.Count;
			KingdomGrowthLocationKind physical = GrowthLocationFromTopology(Topology);
			KingdomGrowthLocationKind beforeLocation = MutationKind ==
				KingdomGrowthObjectMutationKind.Create ? KingdomGrowthLocationKind.Absent
				: (MutationKind == KingdomGrowthObjectMutationKind.CellAdd
					|| MutationKind == KingdomGrowthObjectMutationKind.InventoryAdd
					|| MutationKind == KingdomGrowthObjectMutationKind.Receive)
					? KingdomGrowthLocationKind.Escrow : physical;
			KingdomGrowthLocationKind afterLocation = MutationKind ==
				KingdomGrowthObjectMutationKind.Create ? KingdomGrowthLocationKind.Escrow
				: (MutationKind == KingdomGrowthObjectMutationKind.DestroyOne
					|| MutationKind == KingdomGrowthObjectMutationKind.Obliterate) && after == 0
					? KingdomGrowthLocationKind.Graveyard : physical;
			string escrowKey = beforeLocation == KingdomGrowthLocationKind.Escrow
				|| afterLocation == KingdomGrowthLocationKind.Escrow
				? ChildId(Operation.Id, "object-escrow", Output ? ordinal : MaxGrowthOutputs + ordinal)
				: null;
			string leaseSubject = MutationKind == KingdomGrowthObjectMutationKind.Create
				? Marker : ObjectId;
			string leaseKey = ResourceKey(KingdomLifecycleResourceKind.Object,
				Operation.SettlementId, leaseSubject);
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, leaseKey);
			if (leaseKey == null || row != null && (!string.IsNullOrEmpty(row.ActiveOperationId)
				|| row.Revision == long.MaxValue)) return null;
			long revision = row == null ? 0L : row.Revision;
			KingdomGrowthObjectLeg leg = new KingdomGrowthObjectLeg
			{
				OperationId = Operation.Id, EventId = ChildId(Operation.Id,
					Output ? "output" : "source", ordinal), ObjectId = ObjectId, Marker = Marker,
				Blueprint = Blueprint, Topology = Topology, OwnerId = OwnerId, ZoneId = ZoneId,
				X = X, Y = Y, BeforeCount = BeforeCount, Delta = Delta, AfterCount = after,
				NoStack = NoStack, MutationKind = MutationKind,
				BeforeOwnerGraphHash = BeforeOwnerGraphHash,
				AfterOwnerGraphHash = MutationKind == KingdomGrowthObjectMutationKind.Create
					? null : AfterOwnerGraphHash,
				BeforeObjectGraphHash = BeforeObjectGraphHash,
				AfterObjectGraphHash = MutationKind == KingdomGrowthObjectMutationKind.Create
					? null : AfterObjectGraphHash,
				BeforeTopologyHash = BeforeTopologyHash,
				AfterTopologyHash = MutationKind == KingdomGrowthObjectMutationKind.Create
					? null : AfterTopologyHash,
				CreatedMarker = Output && MutationKind == KingdomGrowthObjectMutationKind.Create
					? Marker : null,
					DetachedMarker = !Output || MutationKind != KingdomGrowthObjectMutationKind.Create
						? Marker : null,
					BeforeLocation = beforeLocation, AfterLocation = afterLocation,
					EscrowKey = escrowKey,
					Lease = new KingdomLifecycleResourceLease
					{
						OperationId = Operation.Id, Kind = KingdomLifecycleResourceKind.Object,
						ScopeId = Operation.SettlementId, SubjectId = leaseSubject, Key = leaseKey,
						Before = revision, Delta = 1L, After = revision + 1L,
						BeforeRevision = revision, AfterRevision = revision + 1L,
						State = KingdomLifecycleLeaseState.Prepared
					},
				State = KingdomLifecyclePhysicalState.Prepared,
				ReceiptId = ChildId(Operation.Id, Output ? "output-receipt" : "source-receipt",
					ordinal), ReceiptTopologyId = TopologyId(Topology, OwnerId, ZoneId, X, Y),
					ReceiptState = KingdomLifecyclePhysicalState.Prepared
				};
			leg.Callbacks.Add(new KingdomGrowthObjectCallbackStep
			{
				EventId = ChildId(leg.EventId, "object-callback", 0), Kind = MutationKind,
				FromLocation = beforeLocation, ToLocation = afterLocation, EscrowKey = escrowKey,
				BeforeOwnerId = beforeLocation == physical ? OwnerId : null,
				AfterOwnerId = afterLocation == physical ? OwnerId : null,
				BeforeZoneId = beforeLocation == physical ? ZoneId : null,
				AfterZoneId = afterLocation == physical ? ZoneId : null,
				BeforeX = beforeLocation == physical ? X : -1,
				BeforeY = beforeLocation == physical ? Y : -1,
				AfterX = afterLocation == physical ? X : -1,
				AfterY = afterLocation == physical ? Y : -1,
				BeforeCount = BeforeCount, AfterCount = after, NoStack = NoStack,
				BeforeOwnerGraphHash = BeforeOwnerGraphHash,
				AfterOwnerGraphHash = MutationKind == KingdomGrowthObjectMutationKind.Create
					? null : AfterOwnerGraphHash,
				BeforeObjectGraphHash = BeforeObjectGraphHash,
				AfterObjectGraphHash = MutationKind == KingdomGrowthObjectMutationKind.Create
					? null : AfterObjectGraphHash,
				BeforeTopologyHash = BeforeTopologyHash,
				AfterTopologyHash = MutationKind == KingdomGrowthObjectMutationKind.Create
					? null : AfterTopologyHash,
				State = KingdomLifecyclePhysicalState.Prepared,
				ReceiptId = ChildId(leg.EventId, "object-callback-receipt", 0),
				ReceiptState = KingdomLifecyclePhysicalState.Prepared
			});
			return leg;
		}

		internal static bool BeginGrowthWaterCallback(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int Ordinal)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation)
				|| Operation.Phase != KingdomGrowthPhase.WaterIntent
				|| Ordinal != Operation.WaterCursor || Ordinal < 0
				|| Ordinal >= Operation.WaterLegs.Count) return false;
			KingdomGrowthWaterLeg leg = Operation.WaterLegs[Ordinal];
			if (leg.State != KingdomLifecyclePhysicalState.Prepared
				|| leg.ReceiptState != KingdomLifecyclePhysicalState.Prepared
				|| leg.Lease.State != KingdomLifecycleLeaseState.Prepared) return false;
			leg.State = KingdomLifecyclePhysicalState.Intent;
			leg.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			leg.Lease.State = KingdomLifecycleLeaseState.Intent;
			leg.ReceiptBeforeMatches = 1;
			leg.ReceiptBeforeOwnerGraphHash = leg.BeforeOwnerGraphHash;
			leg.ReceiptBeforePartGraphHash = leg.BeforePartGraphHash;
			leg.ReceiptBeforeTopologyHash = leg.BeforeTopologyHash;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			leg.State = KingdomLifecyclePhysicalState.Prepared;
			leg.ReceiptState = KingdomLifecyclePhysicalState.Prepared;
			leg.Lease.State = KingdomLifecycleLeaseState.Prepared;
			leg.ReceiptBeforeMatches = -1;
			leg.ReceiptBeforeOwnerGraphHash = null;
			leg.ReceiptBeforePartGraphHash = null;
			leg.ReceiptBeforeTopologyHash = null;
			return false;
		}

		internal static bool CommitGrowthWaterCallback(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int Ordinal, string CallbackContainerId,
			string CallbackReferenceHash, bool SameReference,
			string ObservedAfterOwnerGraphHash, string ObservedAfterPartGraphHash,
			string ObservedAfterTopologyHash)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation)
				|| Operation.Phase != KingdomGrowthPhase.WaterIntent
				|| Ordinal != Operation.WaterCursor || Ordinal < 0
				|| Ordinal >= Operation.WaterLegs.Count
				|| !GrowthWitnessHash(CallbackReferenceHash) || !SameReference)
				return false;
			KingdomGrowthWaterLeg leg = Operation.WaterLegs[Ordinal];
			if (leg.State != KingdomLifecyclePhysicalState.Intent
				|| leg.ReceiptState != KingdomLifecyclePhysicalState.Intent
				|| leg.Lease.State != KingdomLifecycleLeaseState.Intent
				|| !string.Equals(CallbackContainerId, leg.ContainerId, StringComparison.Ordinal)
				|| !string.Equals(ObservedAfterOwnerGraphHash, leg.AfterOwnerGraphHash,
					StringComparison.Ordinal)
				|| !string.Equals(ObservedAfterPartGraphHash, leg.AfterPartGraphHash,
					StringComparison.Ordinal)
				|| !string.Equals(ObservedAfterTopologyHash, leg.AfterTopologyHash,
					StringComparison.Ordinal)) return false;
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, leg.Lease.Key);
			if (!GrowthResourceMatches(row, leg.Lease)
				|| row.Revision != leg.Lease.BeforeRevision
				|| !string.Equals(row.ActiveOperationId, Operation.Id,
					StringComparison.Ordinal)) return false;
			long oldRevision = row.Revision; string oldLast = row.LastOperationId;
			leg.State = KingdomLifecyclePhysicalState.Proved;
			leg.ReceiptState = KingdomLifecyclePhysicalState.Proved;
			leg.Lease.State = KingdomLifecycleLeaseState.Proved;
			leg.ReceiptAfterMatches = 1;
			leg.ReceiptAfterOwnerGraphHash = leg.AfterOwnerGraphHash;
			leg.ReceiptAfterPartGraphHash = leg.AfterPartGraphHash;
			leg.ReceiptAfterTopologyHash = leg.AfterTopologyHash;
			leg.ReceiptCallbackContainerId = CallbackContainerId;
			leg.ReceiptCallbackReferenceHash = CallbackReferenceHash;
			leg.ReceiptSameReference = true;
			leg.ReceiptProofId = GrowthWaterReceiptProof(Operation, leg, Ordinal);
			row.Revision = leg.Lease.AfterRevision; row.LastOperationId = Operation.Id;
			Operation.WaterCursor++;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			Operation.WaterCursor--; row.Revision = oldRevision; row.LastOperationId = oldLast;
			leg.State = KingdomLifecyclePhysicalState.Intent;
			leg.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			leg.Lease.State = KingdomLifecycleLeaseState.Intent;
			leg.ReceiptAfterMatches = -1;
			leg.ReceiptAfterOwnerGraphHash = null;
			leg.ReceiptAfterPartGraphHash = null;
			leg.ReceiptAfterTopologyHash = null;
			leg.ReceiptCallbackContainerId = null;
			leg.ReceiptCallbackReferenceHash = null;
			leg.ReceiptSameReference = false; leg.ReceiptProofId = null;
			return false;
		}

		public static bool TryAppendGrowthObjectPlacement(KingdomGrowthOperation Operation,
			KingdomGrowthObjectLeg Leg,
			KingdomGrowthObjectMutationKind Kind, KingdomLifecycleTopology Topology, string OwnerId,
			string ZoneId, int X, int Y, string BeforeOwnerGraphHash,
			string AfterOwnerGraphHash, string BeforeObjectGraphHash, string AfterObjectGraphHash,
			string BeforeTopologyHash, string AfterTopologyHash)
		{
			if (Operation == null || Operation.Phase != KingdomGrowthPhase.Prepared
				|| Operation.PlanHash != null || Leg == null
				|| !string.Equals(Leg.OperationId, Operation.Id, StringComparison.Ordinal)
				|| Leg.State != KingdomLifecyclePhysicalState.Prepared
				|| Leg.Lease == null || Leg.Lease.State != KingdomLifecycleLeaseState.Prepared
				|| Leg.Callbacks == null || Leg.Callbacks.Count == 0
				|| Leg.Callbacks.Count >= MaxGrowthObjectCallbacks
				|| Leg.AfterLocation != KingdomGrowthLocationKind.Escrow
				|| (Kind != KingdomGrowthObjectMutationKind.CellAdd
					&& Kind != KingdomGrowthObjectMutationKind.InventoryAdd
					&& Kind != KingdomGrowthObjectMutationKind.Receive)) return false;
			KingdomGrowthLocationKind afterLocation = GrowthLocationFromTopology(Topology);
			int ordinal = Leg.Callbacks.Count;
			KingdomGrowthObjectCallbackStep step = new KingdomGrowthObjectCallbackStep
			{
				EventId = ChildId(Leg.EventId, "object-callback", ordinal), Kind = Kind,
				FromLocation = KingdomGrowthLocationKind.Escrow, ToLocation = afterLocation,
				EscrowKey = Leg.EscrowKey, AfterOwnerId = OwnerId, AfterZoneId = ZoneId,
				AfterX = X, AfterY = Y, BeforeCount = Leg.AfterCount,
				AfterCount = Leg.AfterCount, NoStack = Leg.NoStack,
				BeforeOwnerGraphHash = BeforeOwnerGraphHash,
				AfterOwnerGraphHash = AfterOwnerGraphHash,
				BeforeObjectGraphHash = BeforeObjectGraphHash,
				AfterObjectGraphHash = AfterObjectGraphHash,
				BeforeTopologyHash = BeforeTopologyHash, AfterTopologyHash = AfterTopologyHash,
				State = KingdomLifecyclePhysicalState.Prepared,
				ReceiptId = ChildId(Leg.EventId, "object-callback-receipt", ordinal),
				ReceiptState = KingdomLifecyclePhysicalState.Prepared
			};
			if (!GrowthObjectCallbackStepShape(step, Leg.EventId, Leg.ObjectId, Leg.Marker, ordinal))
				return false;
			Leg.Callbacks.Add(step); Leg.AfterLocation = afterLocation; Leg.Topology = Topology;
			Leg.OwnerId = OwnerId; Leg.ZoneId = ZoneId; Leg.X = X; Leg.Y = Y;
			Leg.AfterOwnerGraphHash = AfterOwnerGraphHash;
			Leg.AfterObjectGraphHash = AfterObjectGraphHash;
			Leg.AfterTopologyHash = AfterTopologyHash;
			Leg.ReceiptTopologyId = TopologyId(Topology, OwnerId, ZoneId, X, Y);
			return true;
		}

		public static KingdomGrowthObjectLeg PrepareGrowthHarvestableMutationLeg(
			KingdomGrowthBook Book, KingdomGrowthOperation Operation, string ObjectId,
			string Marker, string Blueprint,
			string ZoneId, int X, int Y, int Count, bool BeforeRipe, bool AfterRipe,
			int BeforeRegenTimer, int AfterRegenTimer, string BeforeRegenTime,
			string AfterRegenTime, int BeforeTileIndex, int AfterTileIndex,
			string BeforeRenderTile, string AfterRenderTile, string BeforeRenderColor,
			string AfterRenderColor, string BeforeRenderDetail, string AfterRenderDetail,
			string BeforeRenderString, string AfterRenderString, string BeforeTileColor,
			string AfterTileColor, string BeforeOwnerGraphHash, string AfterOwnerGraphHash,
			string BeforeObjectGraphHash, string AfterObjectGraphHash,
			string BeforeTopologyHash, string AfterTopologyHash)
		{
			KingdomGrowthObjectLeg leg = PrepareGrowthObjectLeg(Book, Operation, false,
				KingdomGrowthObjectMutationKind.HarvestableRipeSet, ObjectId, Marker, Blueprint,
				KingdomLifecycleTopology.Cell, null, ZoneId, X, Y, Count, 0, false,
				BeforeOwnerGraphHash, AfterOwnerGraphHash, BeforeObjectGraphHash,
				AfterObjectGraphHash, BeforeTopologyHash, AfterTopologyHash);
			if (leg == null || leg.Callbacks == null || leg.Callbacks.Count != 1) return null;
			leg.DetachedMarker = null;
			KingdomGrowthObjectCallbackStep step = leg.Callbacks[0];
			step.BeforeHasHarvestable = true; step.AfterHasHarvestable = true;
			step.BeforeRipe = BeforeRipe; step.AfterRipe = AfterRipe;
			step.BeforeRegenTimer = BeforeRegenTimer; step.AfterRegenTimer = AfterRegenTimer;
			step.BeforeRegenTime = BeforeRegenTime; step.AfterRegenTime = AfterRegenTime;
			step.BeforeTileIndex = BeforeTileIndex; step.AfterTileIndex = AfterTileIndex;
			step.BeforeRenderTile = BeforeRenderTile; step.AfterRenderTile = AfterRenderTile;
			step.BeforeRenderColor = BeforeRenderColor; step.AfterRenderColor = AfterRenderColor;
			step.BeforeRenderDetail = BeforeRenderDetail;
			step.AfterRenderDetail = AfterRenderDetail;
			step.BeforeRenderString = BeforeRenderString;
			step.AfterRenderString = AfterRenderString;
			step.BeforeTileColor = BeforeTileColor; step.AfterTileColor = AfterTileColor;
			return GrowthObjectCallbackStepShape(step, leg.EventId, leg.ObjectId, leg.Marker, 0)
				? leg : null;
		}

		internal static bool BeginGrowthObjectCallback(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, bool Output, int LegOrdinal,
			string BeforeOwnerGraphHash, string AfterOwnerGraphHash,
			string BeforeObjectGraphHash, string AfterObjectGraphHash,
			string BeforeTopologyHash, string AfterTopologyHash)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation)) return false;
			List<KingdomGrowthObjectLeg> list = Output ? Operation.Outputs : Operation.Sources;
			int cursor = Output ? Operation.OutputCursor : Operation.SourceCursor;
			KingdomGrowthPhase required = Output ? KingdomGrowthPhase.OutputIntent
				: KingdomGrowthPhase.SourceIntent;
			if (Operation.Phase != required || LegOrdinal != cursor || LegOrdinal < 0
				|| LegOrdinal >= list.Count) return false;
			KingdomGrowthObjectLeg leg = list[LegOrdinal];
			if (leg.State != KingdomLifecyclePhysicalState.Prepared
				&& leg.State != KingdomLifecyclePhysicalState.Intent
				|| leg.CallbackCursor < 0 || leg.CallbackCursor >= leg.Callbacks.Count) return false;
			KingdomGrowthObjectCallbackStep step = leg.Callbacks[leg.CallbackCursor];
			if (step.State != KingdomLifecyclePhysicalState.Prepared) return false;
			bool create = step.Kind == KingdomGrowthObjectMutationKind.Create;
			string oldStepBeforeOwner = step.BeforeOwnerGraphHash;
			string oldStepAfterOwner = step.AfterOwnerGraphHash;
			string oldStepBeforeObject = step.BeforeObjectGraphHash;
			string oldStepAfterObject = step.AfterObjectGraphHash;
			string oldStepBeforeTopology = step.BeforeTopologyHash;
			string oldStepAfterTopology = step.AfterTopologyHash;
			string oldLegAfterOwner = leg.AfterOwnerGraphHash;
			string oldLegAfterObject = leg.AfterObjectGraphHash;
			string oldLegAfterTopology = leg.AfterTopologyHash;
			KingdomLifecyclePhysicalState oldStepState = step.State;
			KingdomLifecyclePhysicalState oldStepReceiptState = step.ReceiptState;
			int oldStepBeforeMatches = step.ReceiptBeforeMatches;
			int oldStepBeforeCount = step.ReceiptBeforeCount;
			string oldStepReceiptBeforeOwner = step.ReceiptBeforeOwnerGraphHash;
			string oldStepReceiptBeforeObject = step.ReceiptBeforeObjectGraphHash;
			string oldStepReceiptBeforeTopology = step.ReceiptBeforeTopologyHash;
			KingdomLifecyclePhysicalState oldLegState = leg.State;
			KingdomLifecycleLeaseState oldLeaseState = leg.Lease.State;
			KingdomLifecyclePhysicalState oldLegReceiptState = leg.ReceiptState;
			int oldLegBeforeIdMatches = leg.ReceiptBeforeIdMatches;
			int oldLegBeforeMarkerMatches = leg.ReceiptBeforeMarkerMatches;
			int oldLegBeforeCount = leg.ReceiptBeforeCount;
			string oldLegReceiptBeforeOwner = leg.ReceiptBeforeOwnerGraphHash;
			string oldLegReceiptBeforeObject = leg.ReceiptBeforeObjectGraphHash;
			string oldLegReceiptBeforeTopology = leg.ReceiptBeforeTopologyHash;
			if (create)
			{
				if (BeforeOwnerGraphHash != null || AfterOwnerGraphHash != null
					|| BeforeObjectGraphHash != null || AfterObjectGraphHash != null
					|| BeforeTopologyHash != null || AfterTopologyHash != null) return false;
			}
			else
			{
				if (!GrowthWitnessHash(BeforeOwnerGraphHash)
					|| !GrowthWitnessHash(AfterOwnerGraphHash)
					|| !GrowthWitnessHash(BeforeObjectGraphHash)
					|| !GrowthWitnessHash(AfterObjectGraphHash)
					|| !GrowthWitnessHash(BeforeTopologyHash)
					|| !GrowthWitnessHash(AfterTopologyHash)) return false;
				if (step.BeforeOwnerGraphHash != null && (!string.Equals(
					step.BeforeOwnerGraphHash, BeforeOwnerGraphHash, StringComparison.Ordinal)
					|| !string.Equals(step.AfterOwnerGraphHash, AfterOwnerGraphHash,
						StringComparison.Ordinal)
					|| !string.Equals(step.BeforeObjectGraphHash, BeforeObjectGraphHash,
						StringComparison.Ordinal)
					|| !string.Equals(step.AfterObjectGraphHash, AfterObjectGraphHash,
						StringComparison.Ordinal)
					|| !string.Equals(step.BeforeTopologyHash, BeforeTopologyHash,
						StringComparison.Ordinal)
					|| !string.Equals(step.AfterTopologyHash, AfterTopologyHash,
						StringComparison.Ordinal))) return false;
				step.BeforeOwnerGraphHash = BeforeOwnerGraphHash;
				step.AfterOwnerGraphHash = AfterOwnerGraphHash;
				step.BeforeObjectGraphHash = BeforeObjectGraphHash;
				step.AfterObjectGraphHash = AfterObjectGraphHash;
				step.BeforeTopologyHash = BeforeTopologyHash;
				step.AfterTopologyHash = AfterTopologyHash;
				if (leg.CallbackCursor == leg.Callbacks.Count - 1)
				{
					leg.AfterOwnerGraphHash = AfterOwnerGraphHash;
					leg.AfterObjectGraphHash = AfterObjectGraphHash;
					leg.AfterTopologyHash = AfterTopologyHash;
				}
			}
			step.State = KingdomLifecyclePhysicalState.Intent;
			step.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			step.ReceiptBeforeMatches = step.BeforeCount == 0 ? 0 : 1;
			step.ReceiptBeforeCount = step.BeforeCount;
			step.ReceiptBeforeOwnerGraphHash = step.BeforeOwnerGraphHash;
			step.ReceiptBeforeObjectGraphHash = step.BeforeObjectGraphHash;
			step.ReceiptBeforeTopologyHash = step.BeforeTopologyHash;
			leg.State = KingdomLifecyclePhysicalState.Intent;
			leg.Lease.State = KingdomLifecycleLeaseState.Intent;
			leg.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			leg.ReceiptBeforeIdMatches = Output
				&& leg.MutationKind == KingdomGrowthObjectMutationKind.Create ? 0 : 1;
			leg.ReceiptBeforeMarkerMatches = leg.ReceiptBeforeIdMatches;
			leg.ReceiptBeforeCount = leg.BeforeCount;
			leg.ReceiptBeforeOwnerGraphHash = leg.BeforeOwnerGraphHash;
			leg.ReceiptBeforeObjectGraphHash = leg.BeforeObjectGraphHash;
			leg.ReceiptBeforeTopologyHash = leg.BeforeTopologyHash;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			step.BeforeOwnerGraphHash = oldStepBeforeOwner;
			step.AfterOwnerGraphHash = oldStepAfterOwner;
			step.BeforeObjectGraphHash = oldStepBeforeObject;
			step.AfterObjectGraphHash = oldStepAfterObject;
			step.BeforeTopologyHash = oldStepBeforeTopology;
			step.AfterTopologyHash = oldStepAfterTopology;
			leg.AfterOwnerGraphHash = oldLegAfterOwner;
			leg.AfterObjectGraphHash = oldLegAfterObject;
			leg.AfterTopologyHash = oldLegAfterTopology;
			step.State = oldStepState; step.ReceiptState = oldStepReceiptState;
			step.ReceiptBeforeMatches = oldStepBeforeMatches;
			step.ReceiptBeforeCount = oldStepBeforeCount;
			step.ReceiptBeforeOwnerGraphHash = oldStepReceiptBeforeOwner;
			step.ReceiptBeforeObjectGraphHash = oldStepReceiptBeforeObject;
			step.ReceiptBeforeTopologyHash = oldStepReceiptBeforeTopology;
			leg.State = oldLegState; leg.Lease.State = oldLeaseState;
			leg.ReceiptState = oldLegReceiptState;
			leg.ReceiptBeforeIdMatches = oldLegBeforeIdMatches;
			leg.ReceiptBeforeMarkerMatches = oldLegBeforeMarkerMatches;
			leg.ReceiptBeforeCount = oldLegBeforeCount;
			leg.ReceiptBeforeOwnerGraphHash = oldLegReceiptBeforeOwner;
			leg.ReceiptBeforeObjectGraphHash = oldLegReceiptBeforeObject;
			leg.ReceiptBeforeTopologyHash = oldLegReceiptBeforeTopology;
			return false;
		}

		internal static bool CommitGrowthObjectCallback(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, bool Output, int LegOrdinal,
			string CallbackObjectId, string CallbackMarker, string CallbackReferenceHash,
			bool SameReference, string ObservedAfterOwnerGraphHash,
			string ObservedAfterObjectGraphHash, string ObservedAfterTopologyHash)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation) || !ValidRootId(CallbackObjectId)
				|| !ValidRootId(CallbackMarker) || !GrowthWitnessHash(CallbackReferenceHash)
				|| !SameReference || !GrowthWitnessHash(ObservedAfterOwnerGraphHash)
				|| !GrowthWitnessHash(ObservedAfterObjectGraphHash)
				|| !GrowthWitnessHash(ObservedAfterTopologyHash)) return false;
			List<KingdomGrowthObjectLeg> list = Output ? Operation.Outputs : Operation.Sources;
			int cursor = Output ? Operation.OutputCursor : Operation.SourceCursor;
			if (LegOrdinal != cursor || LegOrdinal < 0 || LegOrdinal >= list.Count) return false;
			KingdomGrowthObjectLeg leg = list[LegOrdinal];
			if (leg.State != KingdomLifecyclePhysicalState.Intent || leg.CallbackCursor < 0
				|| leg.CallbackCursor >= leg.Callbacks.Count) return false;
			KingdomGrowthObjectCallbackStep step = leg.Callbacks[leg.CallbackCursor];
			bool create = step.Kind == KingdomGrowthObjectMutationKind.Create;
			if (step.State != KingdomLifecyclePhysicalState.Intent
				|| !string.Equals(CallbackMarker, leg.Marker, StringComparison.Ordinal)
				|| (!create && !string.Equals(CallbackObjectId, leg.ObjectId,
					StringComparison.Ordinal))
				|| (!create && (!string.Equals(ObservedAfterOwnerGraphHash,
					step.AfterOwnerGraphHash, StringComparison.Ordinal)
					|| !string.Equals(ObservedAfterObjectGraphHash, step.AfterObjectGraphHash,
						StringComparison.Ordinal)
					|| !string.Equals(ObservedAfterTopologyHash, step.AfterTopologyHash,
						StringComparison.Ordinal)))) return false;
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, leg.Lease.Key);
			if (!GrowthResourceMatches(row, leg.Lease)
				|| row.Revision != leg.Lease.BeforeRevision
				|| !string.Equals(row.ActiveOperationId, Operation.Id,
					StringComparison.Ordinal)) return false;
			string oldObjectId = leg.ObjectId;
			string oldStepAfterOwner = step.AfterOwnerGraphHash;
			string oldStepAfterObject = step.AfterObjectGraphHash;
			string oldStepAfterTopology = step.AfterTopologyHash;
			KingdomLifecyclePhysicalState oldStepState = step.State;
			KingdomLifecyclePhysicalState oldStepReceiptState = step.ReceiptState;
			int oldStepAfterMatches = step.ReceiptAfterMatches;
			int oldStepAfterCount = step.ReceiptAfterCount;
			string oldStepCallbackId = step.ReceiptCallbackObjectId;
			string oldStepCallbackMarker = step.ReceiptCallbackMarker;
			string oldStepCallbackReference = step.ReceiptCallbackReferenceHash;
			bool oldStepSameReference = step.ReceiptSameReference;
			string oldStepReceiptAfterOwner = step.ReceiptAfterOwnerGraphHash;
			string oldStepReceiptAfterObject = step.ReceiptAfterObjectGraphHash;
			string oldStepReceiptAfterTopology = step.ReceiptAfterTopologyHash;
			string oldStepProof = step.ReceiptProofId;
			int oldCallbackCursor = leg.CallbackCursor;
			KingdomGrowthObjectCallbackStep nextStep = oldCallbackCursor + 1 < leg.Callbacks.Count
				? leg.Callbacks[oldCallbackCursor + 1] : null;
			string oldNextBeforeOwner = nextStep == null ? null : nextStep.BeforeOwnerGraphHash;
			string oldNextBeforeObject = nextStep == null ? null : nextStep.BeforeObjectGraphHash;
			string oldNextBeforeTopology = nextStep == null ? null : nextStep.BeforeTopologyHash;
			KingdomLifecyclePhysicalState oldLegState = leg.State;
			KingdomLifecycleLeaseState oldLeaseState = leg.Lease.State;
			string oldLegAfterOwner = leg.AfterOwnerGraphHash;
			string oldLegAfterObject = leg.AfterObjectGraphHash;
			string oldLegAfterTopology = leg.AfterTopologyHash;
			KingdomLifecyclePhysicalState oldLegReceiptState = leg.ReceiptState;
			int oldLegAfterIdMatches = leg.ReceiptAfterIdMatches;
			int oldLegAfterMarkerMatches = leg.ReceiptAfterMarkerMatches;
			int oldLegAfterCount = leg.ReceiptAfterCount;
			string oldLegReceiptAfterOwner = leg.ReceiptAfterOwnerGraphHash;
			string oldLegReceiptAfterObject = leg.ReceiptAfterObjectGraphHash;
			string oldLegReceiptAfterTopology = leg.ReceiptAfterTopologyHash;
			string oldLegCallbackId = leg.ReceiptCallbackObjectId;
			string oldLegCallbackMarker = leg.ReceiptCallbackMarker;
			string oldLegCallbackReference = leg.ReceiptCallbackReferenceHash;
			bool oldLegSameReference = leg.ReceiptSameReference;
			string oldLegProof = leg.ReceiptProofId;
			long oldRowRevision = row.Revision;
			string oldRowLastOperation = row.LastOperationId;
			int oldOperationCursor = cursor;
			if (create)
			{
				leg.ObjectId = CallbackObjectId;
				step.AfterOwnerGraphHash = ObservedAfterOwnerGraphHash;
				step.AfterObjectGraphHash = ObservedAfterObjectGraphHash;
				step.AfterTopologyHash = ObservedAfterTopologyHash;
			}
			step.State = KingdomLifecyclePhysicalState.Proved;
			step.ReceiptState = KingdomLifecyclePhysicalState.Proved;
			step.ReceiptAfterMatches = step.AfterCount == 0 ? 0 : 1;
			step.ReceiptAfterCount = step.AfterCount;
			step.ReceiptCallbackObjectId = CallbackObjectId;
			step.ReceiptCallbackMarker = CallbackMarker;
			step.ReceiptCallbackReferenceHash = CallbackReferenceHash;
			step.ReceiptSameReference = true;
			step.ReceiptAfterOwnerGraphHash = ObservedAfterOwnerGraphHash;
			step.ReceiptAfterObjectGraphHash = ObservedAfterObjectGraphHash;
			step.ReceiptAfterTopologyHash = ObservedAfterTopologyHash;
			step.ReceiptProofId = GrowthObjectCallbackProof(Operation, leg,
				LegOrdinal, Output, leg.CallbackCursor);
			leg.CallbackCursor++;
			if (leg.CallbackCursor < leg.Callbacks.Count)
			{
				KingdomGrowthObjectCallbackStep next = leg.Callbacks[leg.CallbackCursor];
				if (next.BeforeOwnerGraphHash == null)
				{
					next.BeforeOwnerGraphHash = ObservedAfterOwnerGraphHash;
					next.BeforeObjectGraphHash = ObservedAfterObjectGraphHash;
					next.BeforeTopologyHash = ObservedAfterTopologyHash;
				}
			}
			else
			{
				leg.State = KingdomLifecyclePhysicalState.Proved;
				leg.Lease.State = KingdomLifecycleLeaseState.Proved;
				leg.AfterOwnerGraphHash = ObservedAfterOwnerGraphHash;
				leg.AfterObjectGraphHash = ObservedAfterObjectGraphHash;
				leg.AfterTopologyHash = ObservedAfterTopologyHash;
				leg.ReceiptState = KingdomLifecyclePhysicalState.Proved;
				leg.ReceiptAfterIdMatches = step.AfterCount == 0 ? 0 : 1;
				leg.ReceiptAfterMarkerMatches = leg.ReceiptAfterIdMatches;
				leg.ReceiptAfterCount = leg.AfterCount;
				leg.ReceiptAfterOwnerGraphHash = ObservedAfterOwnerGraphHash;
				leg.ReceiptAfterObjectGraphHash = ObservedAfterObjectGraphHash;
				leg.ReceiptAfterTopologyHash = ObservedAfterTopologyHash;
				leg.ReceiptCallbackObjectId = CallbackObjectId;
				leg.ReceiptCallbackMarker = CallbackMarker;
				leg.ReceiptCallbackReferenceHash = CallbackReferenceHash;
				leg.ReceiptSameReference = true;
				leg.ReceiptProofId = GrowthObjectReceiptProof(Operation, leg, LegOrdinal, Output);
				row.Revision = leg.Lease.AfterRevision;
				row.LastOperationId = Operation.Id;
				if (Output) Operation.OutputCursor++; else Operation.SourceCursor++;
			}
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			leg.ObjectId = oldObjectId;
			step.AfterOwnerGraphHash = oldStepAfterOwner;
			step.AfterObjectGraphHash = oldStepAfterObject;
			step.AfterTopologyHash = oldStepAfterTopology;
			step.State = oldStepState; step.ReceiptState = oldStepReceiptState;
			step.ReceiptAfterMatches = oldStepAfterMatches;
			step.ReceiptAfterCount = oldStepAfterCount;
			step.ReceiptCallbackObjectId = oldStepCallbackId;
			step.ReceiptCallbackMarker = oldStepCallbackMarker;
			step.ReceiptCallbackReferenceHash = oldStepCallbackReference;
			step.ReceiptSameReference = oldStepSameReference;
			step.ReceiptAfterOwnerGraphHash = oldStepReceiptAfterOwner;
			step.ReceiptAfterObjectGraphHash = oldStepReceiptAfterObject;
			step.ReceiptAfterTopologyHash = oldStepReceiptAfterTopology;
			step.ReceiptProofId = oldStepProof;
			leg.CallbackCursor = oldCallbackCursor;
			if (nextStep != null)
			{
				nextStep.BeforeOwnerGraphHash = oldNextBeforeOwner;
				nextStep.BeforeObjectGraphHash = oldNextBeforeObject;
				nextStep.BeforeTopologyHash = oldNextBeforeTopology;
			}
			leg.State = oldLegState; leg.Lease.State = oldLeaseState;
			leg.AfterOwnerGraphHash = oldLegAfterOwner;
			leg.AfterObjectGraphHash = oldLegAfterObject;
			leg.AfterTopologyHash = oldLegAfterTopology;
			leg.ReceiptState = oldLegReceiptState;
			leg.ReceiptAfterIdMatches = oldLegAfterIdMatches;
			leg.ReceiptAfterMarkerMatches = oldLegAfterMarkerMatches;
			leg.ReceiptAfterCount = oldLegAfterCount;
			leg.ReceiptAfterOwnerGraphHash = oldLegReceiptAfterOwner;
			leg.ReceiptAfterObjectGraphHash = oldLegReceiptAfterObject;
			leg.ReceiptAfterTopologyHash = oldLegReceiptAfterTopology;
			leg.ReceiptCallbackObjectId = oldLegCallbackId;
			leg.ReceiptCallbackMarker = oldLegCallbackMarker;
			leg.ReceiptCallbackReferenceHash = oldLegCallbackReference;
			leg.ReceiptSameReference = oldLegSameReference;
			leg.ReceiptProofId = oldLegProof;
			row.Revision = oldRowRevision; row.LastOperationId = oldRowLastOperation;
			if (Output) Operation.OutputCursor = oldOperationCursor;
			else Operation.SourceCursor = oldOperationCursor;
			return false;
		}

		private static string GrowthObjectCallbackProof(KingdomGrowthOperation operation,
			KingdomGrowthObjectLeg leg, int legOrdinal, bool output, int callbackOrdinal)
		{
			KingdomGrowthObjectCallbackStep step = leg.Callbacks[callbackOrdinal];
			return HashId("growth-object-callback-proof", delegate(BinaryWriter w)
			{
				CanonicalString(w, operation.Id); CanonicalString(w, operation.PlanHash);
				w.Write(output); w.Write(legOrdinal); w.Write(callbackOrdinal);
				CanonicalString(w, leg.ObjectId); CanonicalString(w, leg.Marker);
				CanonicalString(w, step.ReceiptCallbackReferenceHash);
				CanonicalString(w, step.ReceiptAfterOwnerGraphHash);
				CanonicalString(w, step.ReceiptAfterObjectGraphHash);
				CanonicalString(w, step.ReceiptAfterTopologyHash);
			});
		}

		public static KingdomGrowthDomainStep PrepareGrowthDomainStep(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, KingdomGrowthDomainStepKind Kind,
			KingdomGrowthDomainCallbackKind CallbackKind, string ActorId, string SubjectId,
			long Before, long After, string CallbackBodyHash, string BeforeGraphHash,
			string AfterGraphHash, string BeforeMapHash, string AfterMapHash)
		{
			return PrepareGrowthDomainStep(Book, Operation, Kind, CallbackKind, ActorId,
				SubjectId, Before, After, CallbackBodyHash, BeforeGraphHash, AfterGraphHash,
				BeforeMapHash, AfterMapHash, null, null, null, null);
		}

		public static KingdomGrowthDomainStep PrepareGrowthDomainStep(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, KingdomGrowthDomainStepKind Kind,
			KingdomGrowthDomainCallbackKind CallbackKind, string ActorId, string SubjectId,
			long Before, long After, string CallbackBodyHash, string BeforeGraphHash,
			string AfterGraphHash, string BeforeMapHash, string AfterMapHash,
			KingdomGrowthScarcitySnapshot ScarcityBefore,
			KingdomGrowthScarcitySnapshot ScarcityAfter,
			KingdomGrowthAccountingSnapshot AccountingBefore,
			KingdomGrowthAccountingSnapshot AccountingAfter,
			KingdomGrowthFieldState FieldBefore = null,
			KingdomGrowthFieldState FieldAfter = null,
			List<KingdomGrowthCropRow> CropRowsBefore = null,
			List<KingdomGrowthCropRow> CropRowsDeclaredAfter = null)
		{
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| Operation == null || Operation.Phase != KingdomGrowthPhase.Prepared
				|| Operation.PlanHash != null || Operation.DomainSteps == null
				|| Operation.DomainSteps.Count >= MaxResourceLeases) return null;
			KingdomLifecycleResourceKind resourceKind;
			if (!TryGrowthDomainKind(Kind, CallbackKind, out resourceKind)) return null;
			long delta;
			if (!CheckedAdd(After, -Before, out delta) || delta == 0L) return null;
			string key = ResourceKey(resourceKind, Operation.SettlementId, SubjectId);
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, key);
			if (key == null || (row != null && (!string.IsNullOrEmpty(row.ActiveOperationId)
				|| row.Revision == long.MaxValue))) return null;
			long revision = row == null ? 0L : row.Revision;
			int ordinal = Operation.DomainSteps.Count;
			KingdomGrowthDomainStep step = new KingdomGrowthDomainStep
			{
				Kind = Kind, CallbackKind = CallbackKind, CallbackBodyHash = CallbackBodyHash,
				EventId = ChildId(Operation.Id, "domain", ordinal), ActorId = ActorId,
				SubjectId = SubjectId, BeforeValue = Before, AfterValue = After,
				BeforeGraphHash = BeforeGraphHash, AfterGraphHash = AfterGraphHash,
				BeforeMapHash = BeforeMapHash, AfterMapHash = AfterMapHash,
				ScarcityBefore = CloneGrowthScarcity(ScarcityBefore),
				ScarcityAfter = CloneGrowthScarcity(ScarcityAfter),
				AccountingBefore = CloneGrowthAccounting(AccountingBefore),
				AccountingAfter = CloneGrowthAccounting(AccountingAfter),
				FieldBefore = CloneGrowthFieldState(FieldBefore),
				FieldAfter = CloneGrowthFieldState(FieldAfter),
				CropRowsBefore = CloneGrowthCropRows(CropRowsBefore),
				CropRowsDeclaredAfter = CloneGrowthCropRows(CropRowsDeclaredAfter),
				CropRowsAfter = null,
				State = KingdomLifecyclePhysicalState.Prepared,
				ReceiptId = ChildId(Operation.Id, "domain-receipt", ordinal),
				ReceiptState = KingdomLifecyclePhysicalState.Prepared,
				Lease = new KingdomLifecycleResourceLease
				{
					OperationId = Operation.Id, Kind = resourceKind, ScopeId = Operation.SettlementId,
					SubjectId = SubjectId, Key = key, Before = Before, Delta = delta, After = After,
					BeforeRevision = revision, AfterRevision = revision + 1L,
					State = KingdomLifecycleLeaseState.Prepared
				}
			};
			return GrowthDomainShape(Operation, step, ordinal, true) ? step : null;
		}

		internal static bool BeginGrowthDomainCallback(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int Ordinal)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation)
				|| Operation.Phase != KingdomGrowthPhase.DomainIntent
				|| Ordinal != Operation.DomainCursor || Ordinal < 0
				|| Ordinal >= Operation.DomainSteps.Count) return false;
			KingdomGrowthDomainStep step = Operation.DomainSteps[Ordinal];
			if (step.State != KingdomLifecyclePhysicalState.Prepared
				|| step.ReceiptState != KingdomLifecyclePhysicalState.Prepared
				|| step.Lease.State != KingdomLifecycleLeaseState.Prepared) return false;
			step.State = KingdomLifecyclePhysicalState.Intent;
			step.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			step.Lease.State = KingdomLifecycleLeaseState.Intent;
			step.ReceiptBeforeValue = step.BeforeValue;
			step.ReceiptBeforeGraphHash = step.BeforeGraphHash;
			step.ReceiptBeforeMapHash = step.BeforeMapHash;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			step.State = KingdomLifecyclePhysicalState.Prepared;
			step.ReceiptState = KingdomLifecyclePhysicalState.Prepared;
			step.Lease.State = KingdomLifecycleLeaseState.Prepared;
			step.ReceiptBeforeValue = 0L;
			step.ReceiptBeforeGraphHash = null; step.ReceiptBeforeMapHash = null;
			return false;
		}

		internal static bool CommitGrowthDomainCallback(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int Ordinal, long ObservedAfterValue,
			string ObservedAfterGraphHash, string ObservedAfterMapHash,
			KingdomGrowthFieldState ObservedFieldAfter = null,
			List<KingdomGrowthCropRow> ObservedCropRowsAfter = null)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation)
				|| Operation.Phase != KingdomGrowthPhase.DomainIntent
				|| Ordinal != Operation.DomainCursor || Ordinal < 0
				|| Ordinal >= Operation.DomainSteps.Count) return false;
			KingdomGrowthDomainStep step = Operation.DomainSteps[Ordinal];
			if (step.State != KingdomLifecyclePhysicalState.Intent
				|| step.ReceiptState != KingdomLifecyclePhysicalState.Intent
				|| step.Lease.State != KingdomLifecycleLeaseState.Intent
				|| ObservedAfterValue != step.AfterValue
				|| !string.Equals(ObservedAfterGraphHash, step.AfterGraphHash,
					StringComparison.Ordinal)
				|| !string.Equals(ObservedAfterMapHash, step.AfterMapHash,
					StringComparison.Ordinal)) return false;
			KingdomGrowthFieldSlot field = SlotForGrowthAction(Operation.Action)
				== KingdomGrowthSlotKind.Field ? FindGrowthField(Book, Operation.FieldId) : null;
			if (step.Kind == KingdomGrowthDomainStepKind.Field)
			{
				if (!GrowthFieldStateEquals(ObservedFieldAfter, step.FieldAfter)
					|| ObservedCropRowsAfter != null
					|| !GrowthFieldMatchesState(field, step.FieldBefore)) return false;
			}
			else if (step.Kind == KingdomGrowthDomainStepKind.CropRegistry)
			{
				if (ObservedFieldAfter != null
					|| !GrowthCropRowsEqual(Book.CropRows, step.CropRowsBefore)
					|| !GrowthCropDeclarationMatchesObserved(Operation,
						step.CropRowsDeclaredAfter, ObservedCropRowsAfter)) return false;
			}
			else if (ObservedFieldAfter != null || ObservedCropRowsAfter != null) return false;
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, step.Lease.Key);
			if (!GrowthResourceMatches(row, step.Lease)
				|| row.Revision != step.Lease.BeforeRevision
				|| !string.Equals(row.ActiveOperationId, Operation.Id,
					StringComparison.Ordinal)) return false;
			long oldRevision = row.Revision; string oldLast = row.LastOperationId;
			int oldPending = Book.PendingCrop;
			string oldPendingBlueprint = Book.PendingCropBlueprint;
			string oldPendingZone = Book.PendingCropZoneId;
			long oldSubsidence = Book.LastSubsidenceTick;
			KingdomGrowthFieldState oldField = field == null ? null : GrowthFieldState(field);
			List<KingdomGrowthCropRow> oldRows = new List<KingdomGrowthCropRow>(Book.CropRows);
			List<KingdomGrowthCropRow> oldStepRowsAfter = step.CropRowsAfter;
			if (step.Kind == KingdomGrowthDomainStepKind.Field)
				ApplyGrowthFieldState(field, ObservedFieldAfter);
			if (step.Kind == KingdomGrowthDomainStepKind.CropRegistry)
			{
				step.CropRowsAfter = CloneGrowthCropRows(ObservedCropRowsAfter);
				Book.CropRows.Clear();
				Book.CropRows.AddRange(CloneGrowthCropRows(ObservedCropRowsAfter));
			}
			step.State = KingdomLifecyclePhysicalState.Proved;
			step.ReceiptState = KingdomLifecyclePhysicalState.Proved;
			step.Lease.State = KingdomLifecycleLeaseState.Proved;
			step.ReceiptAfterValue = ObservedAfterValue;
			step.ReceiptAfterGraphHash = ObservedAfterGraphHash;
			step.ReceiptAfterMapHash = ObservedAfterMapHash;
			step.ReceiptProofId = GrowthDomainReceiptProof(Operation, step, Ordinal);
			row.Revision = step.Lease.AfterRevision; row.LastOperationId = Operation.Id;
			Operation.DomainCursor++;
			if (step.Kind == KingdomGrowthDomainStepKind.PendingCrop)
			{
				Book.PendingCrop = Operation.PendingCropAfter;
				Book.PendingCropBlueprint = Operation.PendingCropBlueprintAfter;
				Book.PendingCropZoneId = Operation.PendingCropZoneIdAfter;
			}
			if (step.Kind == KingdomGrowthDomainStepKind.SubsidenceSchedule)
				Book.LastSubsidenceTick = Operation.SubsidenceAfter;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			Operation.DomainCursor--; row.Revision = oldRevision; row.LastOperationId = oldLast;
			Book.PendingCrop = oldPending; Book.PendingCropBlueprint = oldPendingBlueprint;
			Book.PendingCropZoneId = oldPendingZone; Book.LastSubsidenceTick = oldSubsidence;
			if (field != null && oldField != null) ApplyGrowthFieldState(field, oldField);
			Book.CropRows.Clear(); Book.CropRows.AddRange(oldRows);
			step.CropRowsAfter = oldStepRowsAfter;
			step.State = KingdomLifecyclePhysicalState.Intent;
			step.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			step.Lease.State = KingdomLifecycleLeaseState.Intent;
			step.ReceiptAfterValue = 0L; step.ReceiptAfterGraphHash = null;
			step.ReceiptAfterMapHash = null; step.ReceiptProofId = null;
			return false;
		}

		private static KingdomGrowthScarcitySnapshot CloneGrowthScarcity(
			KingdomGrowthScarcitySnapshot x)
		{
			if (x == null) return null;
			return new KingdomGrowthScarcitySnapshot
			{
				DryStreak = x.DryStreak, Withered = x.Withered,
				HungerStreak = x.HungerStreak, Famished = x.Famished,
				LastMeal = x.LastMeal, MealShade = x.MealShade,
				ScrapsAnnounced = x.ScrapsAnnounced, ElapsedTicks = x.ElapsedTicks,
				Days = x.Days, Population = x.Population, Stage = x.Stage,
				UpkeepRequested = x.UpkeepRequested, WaterAvailable = x.WaterAvailable,
				RationsAvailable = x.RationsAvailable, Foraged = x.Foraged, Eaten = x.Eaten,
				FromDish = x.FromDish, Kitchens = x.Kitchens, DishName = x.DishName,
				DishText = x.DishText, DishStaple = x.DishStaple, DishSource = x.DishSource,
				ComposedBite = x.ComposedBite, RequestedWater = x.RequestedWater,
				ProvedWater = x.ProvedWater, RequestedRations = x.RequestedRations,
				ProvedRations = x.ProvedRations, StoresPolicy = x.StoresPolicy,
				DistrictPercent = x.DistrictPercent, ThirstOutcome = x.ThirstOutcome,
				HungerOutcome = x.HungerOutcome, Thirsting = x.Thirsting,
				Starving = x.Starving, Withering = x.Withering,
				Famishing = x.Famishing, Healthy = x.Healthy
			};
		}

		private static KingdomGrowthAccountingSnapshot CloneGrowthAccounting(
			KingdomGrowthAccountingSnapshot x)
		{
			if (x == null) return null;
			return new KingdomGrowthAccountingSnapshot
			{
				Fetched = x.Fetched, UpkeepDrawn = x.UpkeepDrawn, ArrivalCost = x.ArrivalCost,
				Delivered = x.Delivered, Harvested = x.Harvested, Foraged = x.Foraged,
				RationsDrawn = x.RationsDrawn, Milled = x.Milled,
				HarvestLost = x.HarvestLost, Plundered = x.Plundered,
				Arrivals = x.Arrivals, Departures = x.Departures
			};
		}

		private static KingdomGrowthFieldState CloneGrowthFieldState(KingdomGrowthFieldState x)
		{
			if (x == null) return null;
			return new KingdomGrowthFieldState
			{
				FieldId = x.FieldId, WorkObjectId = x.WorkObjectId,
				WorkPartId = x.WorkPartId, Marker = x.Marker, Blueprint = x.Blueprint,
				ZoneId = x.ZoneId, X = x.X, Y = x.Y, CropBlueprint = x.CropBlueprint,
				Stage = x.Stage, NextStageTick = x.NextStageTick, SownTick = x.SownTick,
				Cycles = x.Cycles, SaidWant = x.SaidWant, DeclaredRows = x.DeclaredRows,
				EffectivenessPercent = x.EffectivenessPercent,
				MethodPercent = x.MethodPercent,
				NoLarderAnnounced = x.NoLarderAnnounced, SeedBlueprint = x.SeedBlueprint,
				PartGraphHash = x.PartGraphHash, ObjectGraphHash = x.ObjectGraphHash,
				TopologyHash = x.TopologyHash
			};
		}

		private static KingdomGrowthCropRow CloneGrowthCropRow(KingdomGrowthCropRow x)
		{
			if (x == null) return null;
			return new KingdomGrowthCropRow
			{
				FieldId = x.FieldId, RowId = x.RowId, ObjectId = x.ObjectId,
				Marker = x.Marker, Blueprint = x.Blueprint, ZoneId = x.ZoneId,
				OwnerId = x.OwnerId, X = x.X, Y = x.Y, Count = x.Count,
				HasHarvestable = x.HasHarvestable, Ripe = x.Ripe,
				RegenTimer = x.RegenTimer, RegenTime = x.RegenTime,
				TileIndex = x.TileIndex, RenderTile = x.RenderTile,
				RenderColor = x.RenderColor, RenderDetail = x.RenderDetail,
				RenderString = x.RenderString, TileColor = x.TileColor,
				PartGraphHash = x.PartGraphHash, ObjectGraphHash = x.ObjectGraphHash,
				TopologyHash = x.TopologyHash, Revision = x.Revision,
				LastOperationId = x.LastOperationId
			};
		}

		private static List<KingdomGrowthCropRow> CloneGrowthCropRows(
			List<KingdomGrowthCropRow> rows)
		{
			if (rows == null) return null;
			List<KingdomGrowthCropRow> clone = new List<KingdomGrowthCropRow>(rows.Count);
			for (int i = 0; i < rows.Count; i++) clone.Add(CloneGrowthCropRow(rows[i]));
			return clone;
		}

		private static KingdomGrowthFieldState GrowthFieldState(KingdomGrowthFieldSlot field)
		{
			if (field == null) return null;
			return new KingdomGrowthFieldState
			{
				FieldId = field.FieldId, WorkObjectId = field.WorkObjectId,
				WorkPartId = field.WorkPartId, Marker = field.Marker,
				Blueprint = field.Blueprint, ZoneId = field.ZoneId, X = field.X, Y = field.Y,
				CropBlueprint = field.CropBlueprint, Stage = field.Stage,
				NextStageTick = field.NextStageTick, SownTick = field.SownTick,
				Cycles = field.Cycles, SaidWant = field.SaidWant,
				DeclaredRows = field.DeclaredRows,
				EffectivenessPercent = field.EffectivenessPercent,
				MethodPercent = field.MethodPercent,
				NoLarderAnnounced = field.NoLarderAnnounced,
				SeedBlueprint = field.SeedBlueprint, PartGraphHash = field.PartGraphHash,
				ObjectGraphHash = field.ObjectGraphHash, TopologyHash = field.TopologyHash
			};
		}

		private static bool GrowthFieldStateShape(KingdomGrowthFieldState state,
			string fieldId)
		{
			if (state == null || !string.Equals(state.FieldId, fieldId, StringComparison.Ordinal)
				|| state.NextStageTick < 0L || state.SownTick < 0L || state.Cycles < 0
				|| state.SaidWant < 0 || state.SaidWant > 4 || state.DeclaredRows < 0
				|| state.DeclaredRows > MaxGrowthCropRows) return false;
			bool dormant = state.WorkObjectId == null && state.WorkPartId == null
				&& state.Marker == null && state.Blueprint == null && state.ZoneId == null
				&& state.X == -1 && state.Y == -1 && state.CropBlueprint == null
				&& state.Stage == 0 && state.NextStageTick == 0L && state.SownTick == 0L
				&& state.Cycles == 0 && state.SaidWant == 0 && state.DeclaredRows == 0
				&& state.EffectivenessPercent == 0 && state.MethodPercent == 0
				&& !state.NoLarderAnnounced && state.SeedBlueprint == null
				&& state.PartGraphHash == null && state.ObjectGraphHash == null
				&& state.TopologyHash == null;
			if (dormant) return true;
			return ValidRootId(state.WorkObjectId) && ValidRootId(state.WorkPartId)
				&& ValidRootId(state.Marker) && ValidName(state.Blueprint)
				&& ValidName(state.ZoneId) && state.X >= 0 && state.X <= MaxCoordinate
				&& state.Y >= 0 && state.Y <= MaxCoordinate
				&& ValidName(state.CropBlueprint) && state.Stage >= 0 && state.Stage <= 255
				&& state.EffectivenessPercent > 0 && state.EffectivenessPercent <= 100
				&& state.MethodPercent >= 100
				&& state.MethodPercent <= KingdomResearchRules.MaxMethodPercent
				&& ValidName(state.SeedBlueprint) && GrowthWitnessHash(state.PartGraphHash)
				&& GrowthWitnessHash(state.ObjectGraphHash)
				&& GrowthWitnessHash(state.TopologyHash);
		}

		private static bool GrowthFieldStateEquals(KingdomGrowthFieldState a,
			KingdomGrowthFieldState b)
		{
			return a != null && b != null
				&& string.Equals(a.FieldId, b.FieldId, StringComparison.Ordinal)
				&& string.Equals(a.WorkObjectId, b.WorkObjectId, StringComparison.Ordinal)
				&& string.Equals(a.WorkPartId, b.WorkPartId, StringComparison.Ordinal)
				&& string.Equals(a.Marker, b.Marker, StringComparison.Ordinal)
				&& string.Equals(a.Blueprint, b.Blueprint, StringComparison.Ordinal)
				&& string.Equals(a.ZoneId, b.ZoneId, StringComparison.Ordinal)
				&& a.X == b.X && a.Y == b.Y
				&& string.Equals(a.CropBlueprint, b.CropBlueprint, StringComparison.Ordinal)
				&& a.Stage == b.Stage && a.NextStageTick == b.NextStageTick
				&& a.SownTick == b.SownTick && a.Cycles == b.Cycles
				&& a.SaidWant == b.SaidWant && a.DeclaredRows == b.DeclaredRows
				&& a.EffectivenessPercent == b.EffectivenessPercent
				&& a.MethodPercent == b.MethodPercent
				&& a.NoLarderAnnounced == b.NoLarderAnnounced
				&& string.Equals(a.SeedBlueprint, b.SeedBlueprint, StringComparison.Ordinal)
				&& string.Equals(a.PartGraphHash, b.PartGraphHash, StringComparison.Ordinal)
				&& string.Equals(a.ObjectGraphHash, b.ObjectGraphHash, StringComparison.Ordinal)
				&& string.Equals(a.TopologyHash, b.TopologyHash, StringComparison.Ordinal);
		}

		private static bool GrowthFieldMatchesState(KingdomGrowthFieldSlot field,
			KingdomGrowthFieldState state)
		{
			return field != null && GrowthFieldStateEquals(GrowthFieldState(field), state);
		}

		private static void ApplyGrowthFieldState(KingdomGrowthFieldSlot field,
			KingdomGrowthFieldState state)
		{
			field.WorkObjectId = state.WorkObjectId; field.WorkPartId = state.WorkPartId;
			field.Marker = state.Marker; field.Blueprint = state.Blueprint;
			field.ZoneId = state.ZoneId; field.X = state.X; field.Y = state.Y;
			field.CropBlueprint = state.CropBlueprint; field.Stage = state.Stage;
			field.NextStageTick = state.NextStageTick; field.SownTick = state.SownTick;
			field.Cycles = state.Cycles; field.SaidWant = state.SaidWant;
			field.DeclaredRows = state.DeclaredRows;
			field.EffectivenessPercent = state.EffectivenessPercent;
			field.MethodPercent = state.MethodPercent;
			field.NoLarderAnnounced = state.NoLarderAnnounced;
			field.SeedBlueprint = state.SeedBlueprint; field.PartGraphHash = state.PartGraphHash;
			field.ObjectGraphHash = state.ObjectGraphHash;
			field.TopologyHash = state.TopologyHash;
		}

		private static bool GrowthCropRowScalarShape(KingdomGrowthCropRow row,
			string fieldId, bool allowCreateDeclaration, KingdomGrowthOperation operation)
		{
			if (row == null || !string.Equals(row.FieldId, fieldId, StringComparison.Ordinal)
				|| !ValidRootId(row.RowId) || !ValidRootId(row.Marker)
				|| !ValidName(row.Blueprint) || !ValidName(row.ZoneId)
				|| !ValidRootId(row.OwnerId) || row.X < 0 || row.X > MaxCoordinate
				|| row.Y < 0 || row.Y > MaxCoordinate || row.Count <= 0
				|| row.Count > MaxPhysicalCount || !row.HasHarvestable || row.RegenTimer < 0
				|| !string.Equals(row.RegenTime, string.Empty, StringComparison.Ordinal)
				|| row.TileIndex < -1 || !GrowthBoundedPresentString(row.RenderTile)
				|| !GrowthBoundedPresentString(row.RenderColor)
				|| !GrowthBoundedPresentString(row.RenderDetail)
				|| !GrowthBoundedPresentString(row.RenderString)
				|| !GrowthBoundedPresentString(row.TileColor) || row.Revision < 0L
				|| (row.LastOperationId != null && !ValidGeneratedId(row.LastOperationId))) return false;
			if (row.ObjectId != null) return ValidRootId(row.ObjectId)
				&& GrowthWitnessHash(row.PartGraphHash) && GrowthWitnessHash(row.ObjectGraphHash)
				&& GrowthWitnessHash(row.TopologyHash);
			if (!allowCreateDeclaration || row.PartGraphHash != null
				|| row.ObjectGraphHash != null || row.TopologyHash != null
				|| operation == null || !string.Equals(row.LastOperationId, operation.Id,
					StringComparison.Ordinal)) return false;
			for (int i = 0; i < operation.Outputs.Count; i++)
			{
				KingdomGrowthObjectLeg output = operation.Outputs[i];
				if (output != null && output.MutationKind == KingdomGrowthObjectMutationKind.Create
					&& string.Equals(output.Marker, row.Marker, StringComparison.Ordinal)
					&& string.Equals(output.Blueprint, row.Blueprint, StringComparison.Ordinal)
					&& string.Equals(output.ZoneId, row.ZoneId, StringComparison.Ordinal)
					&& output.X == row.X && output.Y == row.Y && output.AfterCount == row.Count)
					return true;
			}
			return false;
		}

		private static bool GrowthCropRowsShape(List<KingdomGrowthCropRow> rows,
			string fieldId, bool allowCreateDeclaration, KingdomGrowthOperation operation)
		{
			if (rows == null || rows.Count > MaxGrowthCropRows) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> objects = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> markers = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomGrowthCropRow row = rows[i];
				if (!GrowthCropRowScalarShape(row, row == null ? null : row.FieldId,
					allowCreateDeclaration, operation) || !ids.Add(row.RowId)
					|| !markers.Add(row.Marker) || row.ObjectId != null && !objects.Add(row.ObjectId))
					return false;
			}
			return true;
		}

		private static bool GrowthCropRowEquals(KingdomGrowthCropRow a,
			KingdomGrowthCropRow b)
		{
			return a != null && b != null
				&& string.Equals(a.FieldId, b.FieldId, StringComparison.Ordinal)
				&& string.Equals(a.RowId, b.RowId, StringComparison.Ordinal)
				&& string.Equals(a.ObjectId, b.ObjectId, StringComparison.Ordinal)
				&& string.Equals(a.Marker, b.Marker, StringComparison.Ordinal)
				&& string.Equals(a.Blueprint, b.Blueprint, StringComparison.Ordinal)
				&& string.Equals(a.ZoneId, b.ZoneId, StringComparison.Ordinal)
				&& string.Equals(a.OwnerId, b.OwnerId, StringComparison.Ordinal)
				&& a.X == b.X && a.Y == b.Y && a.Count == b.Count
				&& a.HasHarvestable == b.HasHarvestable && a.Ripe == b.Ripe
				&& a.RegenTimer == b.RegenTimer
				&& string.Equals(a.RegenTime, b.RegenTime, StringComparison.Ordinal)
				&& a.TileIndex == b.TileIndex
				&& string.Equals(a.RenderTile, b.RenderTile, StringComparison.Ordinal)
				&& string.Equals(a.RenderColor, b.RenderColor, StringComparison.Ordinal)
				&& string.Equals(a.RenderDetail, b.RenderDetail, StringComparison.Ordinal)
				&& string.Equals(a.RenderString, b.RenderString, StringComparison.Ordinal)
				&& string.Equals(a.TileColor, b.TileColor, StringComparison.Ordinal)
				&& string.Equals(a.PartGraphHash, b.PartGraphHash, StringComparison.Ordinal)
				&& string.Equals(a.ObjectGraphHash, b.ObjectGraphHash, StringComparison.Ordinal)
				&& string.Equals(a.TopologyHash, b.TopologyHash, StringComparison.Ordinal)
				&& a.Revision == b.Revision
				&& string.Equals(a.LastOperationId, b.LastOperationId, StringComparison.Ordinal);
		}

		private static bool GrowthCropRowsEqual(List<KingdomGrowthCropRow> a,
			List<KingdomGrowthCropRow> b)
		{
			if (a == null || b == null || a.Count != b.Count) return false;
			for (int i = 0; i < a.Count; i++)
				if (!GrowthCropRowEquals(a[i], b[i])) return false;
			return true;
		}

		private static bool GrowthCropDeclarationMatchesObserved(
			KingdomGrowthOperation operation, List<KingdomGrowthCropRow> declared,
			List<KingdomGrowthCropRow> observed)
		{
			if (!GrowthCropRowsShape(observed, operation.FieldId, false, operation)
				|| declared == null || declared.Count != observed.Count) return false;
			for (int i = 0; i < declared.Count; i++)
			{
				KingdomGrowthCropRow plan = declared[i]; KingdomGrowthCropRow actual = observed[i];
				if (plan == null || actual == null) return false;
				if (plan.ObjectId != null)
				{
					if (!GrowthCropRowEquals(plan, actual)) return false;
					continue;
				}
				KingdomGrowthCropRow stable = CloneGrowthCropRow(actual);
				stable.ObjectId = null; stable.PartGraphHash = null;
				stable.ObjectGraphHash = null; stable.TopologyHash = null;
				if (!GrowthCropRowEquals(plan, stable)) return false;
				KingdomGrowthObjectLeg output = null;
				for (int j = 0; j < operation.Outputs.Count; j++)
					if (string.Equals(operation.Outputs[j].Marker, plan.Marker,
						StringComparison.Ordinal)) { output = operation.Outputs[j]; break; }
				if (output == null || output.State != KingdomLifecyclePhysicalState.Proved
					|| !string.Equals(output.ObjectId, actual.ObjectId, StringComparison.Ordinal)
					|| !string.Equals(output.ReceiptAfterObjectGraphHash,
						actual.ObjectGraphHash, StringComparison.Ordinal)
					|| !string.Equals(output.ReceiptAfterTopologyHash,
						actual.TopologyHash, StringComparison.Ordinal)) return false;
			}
			return true;
		}

		private static bool GrowthPublicationSnapshotsMatch(KingdomGrowthBook book,
			KingdomGrowthOperation operation, KingdomGrowthFieldSlot field)
		{
			if (operation.OptionState != book.OptionState || operation.OptionTick != book.OptionTick
				|| operation.HealthState != book.HealthState || operation.HealthTick != book.HealthTick
				|| operation.ScarcityOptionState != book.ScarcityOptionState
				|| operation.ScarcityOptionTick != book.ScarcityOptionTick
				|| operation.EffectiveWorkBefore != book.EffectiveWorkTick
				|| operation.HeartbeatBefore != book.LastHeartbeatTick
				|| operation.ArrivalBefore != book.NextArrivalTick
				|| operation.FetchBefore != book.LastFetchTick
				|| operation.MillBefore != book.LastMillTick
				|| operation.SubsidenceBefore != book.LastSubsidenceTick
				|| operation.DeliveryBefore != book.LastDeliveryTick
				|| operation.DepartureBefore != book.LastDepartureTick
				|| operation.PendingCropBefore != book.PendingCrop
				|| !string.Equals(operation.PendingCropBlueprintBefore, book.PendingCropBlueprint,
					StringComparison.Ordinal)
				|| !string.Equals(operation.PendingCropZoneIdBefore, book.PendingCropZoneId,
					StringComparison.Ordinal)) return false;
			if (operation.Action == KingdomGrowthAction.Arrival)
			{
				long after;
				if (book.ArrivalIntervalTicks <= 0L || operation.CreatedTick < book.NextArrivalTick
					|| !CheckedAdd(operation.CreatedTick, book.ArrivalIntervalTicks, out after)
					|| operation.ArrivalAfter != after) return false;
			}
			if (field != null && (operation.ClockLease.Before != field.CommitRevision
				|| operation.FieldClockBefore != field.ClockTick)) return false;
			for (int i = 0; i < operation.DomainSteps.Count; i++)
			{
				KingdomGrowthDomainStep step = operation.DomainSteps[i];
				if (step.Kind == KingdomGrowthDomainStepKind.Field
					&& !GrowthFieldMatchesState(field, step.FieldBefore)) return false;
				if (step.Kind == KingdomGrowthDomainStepKind.CropRegistry
					&& !GrowthCropRowsEqual(book.CropRows, step.CropRowsBefore)) return false;
			}
			return GrowthNonTargetScalarsFrozen(operation);
		}

		private static bool GrowthNonTargetScalarsFrozen(KingdomGrowthOperation operation)
		{
			if (operation.Action != KingdomGrowthAction.Heartbeat
				&& operation.HeartbeatAfter != operation.HeartbeatBefore) return false;
			if (operation.Action != KingdomGrowthAction.Arrival
				&& operation.ArrivalAfter != operation.ArrivalBefore) return false;
			if (operation.Action != KingdomGrowthAction.Fetch
				&& operation.FetchAfter != operation.FetchBefore) return false;
			if (operation.Action != KingdomGrowthAction.Mill
				&& operation.MillAfter != operation.MillBefore) return false;
			bool subsidence = operation.Action == KingdomGrowthAction.Departure
				&& operation.DepartureCauseKind == KingdomGrowthDepartureCauseKind.Subsidence;
			if (subsidence ? operation.SubsidenceAfter <= operation.SubsidenceBefore
				: operation.SubsidenceAfter != operation.SubsidenceBefore) return false;
			if (operation.Action != KingdomGrowthAction.Delivery
				&& operation.DeliveryAfter != operation.DeliveryBefore) return false;
			if (operation.Action != KingdomGrowthAction.Departure
				&& operation.DepartureAfter != operation.DepartureBefore) return false;
			if (!IsGrowthFieldAction(operation.Action)
				&& operation.EffectiveWorkAfter != operation.EffectiveWorkBefore) return false;
			if (operation.Action == KingdomGrowthAction.Heartbeat)
				return operation.PendingCropDelta == 0 && operation.PopulationDelta <= 0;
			if (operation.Action == KingdomGrowthAction.Delivery
				|| operation.Action == KingdomGrowthAction.Harvest)
				return operation.PopulationDelta == 0;
			if (operation.Action == KingdomGrowthAction.Arrival)
				return operation.PendingCropDelta == 0
					&& (operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.Joined
						? operation.PopulationDelta > 0 : operation.PopulationDelta == 0);
			if (operation.Action == KingdomGrowthAction.Departure)
				return operation.PendingCropDelta == 0 && operation.PopulationDelta < 0;
			return operation.PendingCropDelta == 0 && operation.PopulationDelta == 0;
		}

		private static bool IsGrowthFieldAction(KingdomGrowthAction action)
		{
			return action == KingdomGrowthAction.Sow || action == KingdomGrowthAction.Withdraw
				|| action == KingdomGrowthAction.Ripen || action == KingdomGrowthAction.Harvest
				|| action == KingdomGrowthAction.Irrigate;
		}

		private static List<KingdomLifecycleResourceLease> GrowthLeases(
			KingdomGrowthOperation operation)
		{
			if (operation == null || operation.ClockLease == null || operation.WaterLegs == null
				|| operation.Sources == null || operation.Outputs == null
				|| operation.DomainSteps == null) return null;
			List<KingdomLifecycleResourceLease> result =
				new List<KingdomLifecycleResourceLease>(1 + operation.WaterLegs.Count
					+ operation.Sources.Count + operation.Outputs.Count
					+ operation.DomainSteps.Count);
			for (int i = 0; i < operation.WaterLegs.Count; i++)
				if (operation.WaterLegs[i] == null || operation.WaterLegs[i].Lease == null) return null;
				else result.Add(operation.WaterLegs[i].Lease);
			for (int i = 0; i < operation.Sources.Count; i++)
				if (operation.Sources[i] == null || operation.Sources[i].Lease == null) return null;
				else result.Add(operation.Sources[i].Lease);
			for (int i = 0; i < operation.Outputs.Count; i++)
				if (operation.Outputs[i] == null || operation.Outputs[i].Lease == null) return null;
				else result.Add(operation.Outputs[i].Lease);
			for (int i = 0; i < operation.DomainSteps.Count; i++)
				if (operation.DomainSteps[i] == null || operation.DomainSteps[i].Lease == null) return null;
				else result.Add(operation.DomainSteps[i].Lease);
			result.Add(operation.ClockLease);
			return result;
		}

		private static bool GrowthResourceMatches(KingdomLifecycleResourceRevision row,
			KingdomLifecycleResourceLease lease)
		{
			return row != null && lease != null && row.Kind == lease.Kind
				&& string.Equals(row.ScopeId, lease.ScopeId, StringComparison.Ordinal)
				&& string.Equals(row.SubjectId, lease.SubjectId, StringComparison.Ordinal)
				&& string.Equals(row.Key, lease.Key, StringComparison.Ordinal);
		}

		private static bool TryNextGrowthPhase(KingdomGrowthAction action,
			KingdomGrowthPhase from, out KingdomGrowthPhase to)
		{
			to = KingdomGrowthPhase.Invalid;
			bool water = action == KingdomGrowthAction.Heartbeat
				|| action == KingdomGrowthAction.Fetch || action == KingdomGrowthAction.Arrival
				|| action == KingdomGrowthAction.Sow;
			bool source = action == KingdomGrowthAction.Heartbeat
				|| action == KingdomGrowthAction.Departure || action == KingdomGrowthAction.Mill
				|| action == KingdomGrowthAction.Sow || action == KingdomGrowthAction.Withdraw
				|| action == KingdomGrowthAction.Ripen || action == KingdomGrowthAction.Harvest;
			bool output = action == KingdomGrowthAction.Arrival
				|| action == KingdomGrowthAction.Delivery || action == KingdomGrowthAction.Mill
				|| action == KingdomGrowthAction.Sow || action == KingdomGrowthAction.Withdraw
				|| action == KingdomGrowthAction.Harvest;
			switch (from)
			{
			case KingdomGrowthPhase.Prepared:
				to = water ? KingdomGrowthPhase.WaterIntent : source
					? KingdomGrowthPhase.SourceIntent : output
						? KingdomGrowthPhase.OutputIntent : KingdomGrowthPhase.DomainIntent; return true;
			case KingdomGrowthPhase.WaterIntent: if (!water) return false;
				to = KingdomGrowthPhase.WaterSettled; return true;
			case KingdomGrowthPhase.WaterSettled: if (!water) return false;
				to = source ? KingdomGrowthPhase.SourceIntent : output
					? KingdomGrowthPhase.OutputIntent : KingdomGrowthPhase.DomainIntent; return true;
			case KingdomGrowthPhase.SourceIntent: if (!source) return false;
				to = KingdomGrowthPhase.SourcesSettled; return true;
			case KingdomGrowthPhase.SourcesSettled: if (!source) return false;
				to = output ? KingdomGrowthPhase.OutputIntent : KingdomGrowthPhase.DomainIntent; return true;
			case KingdomGrowthPhase.OutputIntent: if (!output) return false;
				to = KingdomGrowthPhase.OutputsSettled; return true;
			case KingdomGrowthPhase.OutputsSettled: if (!output) return false;
				to = KingdomGrowthPhase.DomainIntent; return true;
			case KingdomGrowthPhase.DomainIntent: to = KingdomGrowthPhase.DomainSettled; return true;
			case KingdomGrowthPhase.DomainSettled: to = KingdomGrowthPhase.ClockIntent; return true;
			case KingdomGrowthPhase.ClockIntent: to = KingdomGrowthPhase.Sinks; return true;
			case KingdomGrowthPhase.Sinks: to = KingdomGrowthPhase.Terminal; return true;
			default: return false;
			}
		}

		private static int GrowthPhaseIndex(KingdomGrowthAction action, KingdomGrowthPhase phase)
		{
			KingdomGrowthPhase current = KingdomGrowthPhase.Prepared;
			for (int i = 0; i < 16; i++)
			{
				if (current == phase) return i;
				if (!TryNextGrowthPhase(action, current, out current)) return -1;
			}
			return -1;
		}

		private static bool TryNextGrowthPhase(KingdomGrowthOperation operation,
			KingdomGrowthPhase from, out KingdomGrowthPhase to)
		{
			to = KingdomGrowthPhase.Invalid;
			if (operation == null || operation.WaterLegs == null || operation.Sources == null
				|| operation.Outputs == null || operation.DomainSteps == null) return false;
			bool water = operation.WaterLegs.Count > 0;
			bool source = operation.Sources.Count > 0;
			bool output = operation.Outputs.Count > 0;
			bool domain = operation.DomainSteps.Count > 0;
			switch (from)
			{
			case KingdomGrowthPhase.Prepared:
				to = water ? KingdomGrowthPhase.WaterIntent : source
					? KingdomGrowthPhase.SourceIntent : output ? KingdomGrowthPhase.OutputIntent
						: domain ? KingdomGrowthPhase.DomainIntent : KingdomGrowthPhase.ClockIntent;
				return true;
			case KingdomGrowthPhase.WaterIntent:
				if (!water) return false; to = KingdomGrowthPhase.WaterSettled; return true;
			case KingdomGrowthPhase.WaterSettled:
				if (!water) return false; to = source ? KingdomGrowthPhase.SourceIntent : output
					? KingdomGrowthPhase.OutputIntent : domain ? KingdomGrowthPhase.DomainIntent
						: KingdomGrowthPhase.ClockIntent; return true;
			case KingdomGrowthPhase.SourceIntent:
				if (!source) return false; to = KingdomGrowthPhase.SourcesSettled; return true;
			case KingdomGrowthPhase.SourcesSettled:
				if (!source) return false; to = output ? KingdomGrowthPhase.OutputIntent : domain
					? KingdomGrowthPhase.DomainIntent : KingdomGrowthPhase.ClockIntent; return true;
			case KingdomGrowthPhase.OutputIntent:
				if (!output) return false; to = KingdomGrowthPhase.OutputsSettled; return true;
			case KingdomGrowthPhase.OutputsSettled:
				if (!output) return false; to = domain ? KingdomGrowthPhase.DomainIntent
					: KingdomGrowthPhase.ClockIntent; return true;
			case KingdomGrowthPhase.DomainIntent:
				if (!domain) return false; to = KingdomGrowthPhase.DomainSettled; return true;
			case KingdomGrowthPhase.DomainSettled:
				if (!domain) return false; to = KingdomGrowthPhase.ClockIntent; return true;
			case KingdomGrowthPhase.ClockIntent: to = KingdomGrowthPhase.Sinks; return true;
			case KingdomGrowthPhase.Sinks: to = KingdomGrowthPhase.Terminal; return true;
			default: return false;
			}
		}

		private static int GrowthPhaseIndex(KingdomGrowthOperation operation,
			KingdomGrowthPhase phase)
		{
			KingdomGrowthPhase current = KingdomGrowthPhase.Prepared;
			for (int i = 0; i < 16; i++)
			{
				if (current == phase) return i;
				if (!TryNextGrowthPhase(operation, current, out current)) return -1;
			}
			return -1;
		}

		private static bool ExactGrowthOperationAuthority(KingdomGrowthBook book,
			KingdomGrowthOperation operation)
		{
			if (!CanOwnGrowthAuthority(book, book == null ? null : book.SettlementId)
				|| operation == null) return false;
			KingdomGrowthSlotKind slot = SlotForGrowthAction(operation.Action);
			KingdomGrowthFieldSlot field = slot == KingdomGrowthSlotKind.Field
				? FindGrowthField(book, operation.FieldId) : null;
			if (slot == KingdomGrowthSlotKind.Field
				&& (field == null || field.Quarantined)) return false;
			return slot != KingdomGrowthSlotKind.None && ReferenceEquals(
				GetGrowthOperation(book, slot, operation.FieldId), operation);
		}

		private static bool GrowthTransitionReady(KingdomGrowthBook book,
			KingdomGrowthOperation operation, KingdomGrowthPhase to)
		{
			if (operation.Action == KingdomGrowthAction.Arrival
				&& operation.ArrivalCandidateId != null
				&& (to == KingdomGrowthPhase.DomainIntent || to == KingdomGrowthPhase.ClockIntent
					|| to == KingdomGrowthPhase.Sinks || to == KingdomGrowthPhase.Terminal))
			{
				KingdomGrowthArrivalCandidate candidate = book.ArrivalCandidate;
				if (candidate == null
					|| candidate.Phase != KingdomGrowthArrivalCandidatePhase.Settled
					|| !string.Equals(candidate.Id, operation.ArrivalCandidateId,
						StringComparison.Ordinal)
					|| !string.Equals(candidate.ConsumingOperationId, operation.Id,
						StringComparison.Ordinal)) return false;
			}
			if (to == KingdomGrowthPhase.WaterSettled)
				return operation.WaterCursor == operation.WaterLegs.Count;
			if (to == KingdomGrowthPhase.SourcesSettled)
				return operation.SourceCursor == operation.Sources.Count;
			if (to == KingdomGrowthPhase.OutputsSettled)
				return operation.OutputCursor == operation.Outputs.Count;
			if (to == KingdomGrowthPhase.DomainSettled)
				return operation.DomainCursor == operation.DomainSteps.Count;
			if (to == KingdomGrowthPhase.Sinks)
				return GrowthAllPrefixesSettled(operation)
					&& operation.ClockState == KingdomLifecyclePhysicalState.Proved
					&& GrowthLeaseProvedByRow(book, operation.ClockLease);
			if (to == KingdomGrowthPhase.Terminal)
				return GrowthAllPrefixesSettled(operation) && GrowthAllResourcesProved(book, operation)
					&& GrowthOutboxTerminal(operation);
			return true;
		}

		private static bool GrowthAllPrefixesSettled(KingdomGrowthOperation operation)
		{
			return operation.WaterCursor == operation.WaterLegs.Count
				&& operation.SourceCursor == operation.Sources.Count
				&& operation.OutputCursor == operation.Outputs.Count
				&& operation.DomainCursor == operation.DomainSteps.Count;
		}

		private static bool GrowthLeaseProvedByRow(KingdomGrowthBook book,
			KingdomLifecycleResourceLease lease)
		{
			KingdomLifecycleResourceRevision row = FindGrowthResource(book,
				lease == null ? null : lease.Key);
			return lease != null && lease.State == KingdomLifecycleLeaseState.Proved
				&& GrowthResourceMatches(row, lease) && row.Revision == lease.AfterRevision
				&& string.Equals(row.LastOperationId, lease.OperationId, StringComparison.Ordinal)
				&& string.Equals(row.ActiveOperationId, lease.OperationId, StringComparison.Ordinal);
		}

		private static bool GrowthAllResourcesProved(KingdomGrowthBook book,
			KingdomGrowthOperation operation)
		{
			List<KingdomLifecycleResourceLease> leases = GrowthLeases(operation);
			if (leases == null) return false;
			for (int i = 0; i < leases.Count; i++)
				if (!GrowthLeaseProvedByRow(book, leases[i])) return false;
			return true;
		}

		private static void ApplyGrowthClockValue(KingdomGrowthBook book,
			KingdomGrowthOperation operation)
		{
			switch (operation.Action)
			{
			case KingdomGrowthAction.Heartbeat: book.LastHeartbeatTick = operation.ClockLease.After; break;
			case KingdomGrowthAction.Arrival: book.NextArrivalTick = operation.ClockLease.After; break;
			case KingdomGrowthAction.Departure: book.LastDepartureTick = operation.ClockLease.After; break;
			case KingdomGrowthAction.Delivery: book.LastDeliveryTick = operation.ClockLease.After; break;
			case KingdomGrowthAction.Fetch: book.LastFetchTick = operation.ClockLease.After; break;
			case KingdomGrowthAction.Mill: book.LastMillTick = operation.ClockLease.After; break;
			default:
				KingdomGrowthFieldSlot field = FindGrowthField(book, operation.FieldId);
				if (field != null)
				{
					field.CommitRevision = operation.ClockLease.After;
					field.ClockTick = operation.FieldClockAfter;
					field.LastOperationId = operation.Id;
				}
				if (operation.EffectiveWorkAfter > book.EffectiveWorkTick)
					book.EffectiveWorkTick = operation.EffectiveWorkAfter;
				break;
			}
		}

		private static void AppendGrowthProof(KingdomGrowthBook book, KingdomGrowthProof proof)
		{
			if (book.RecentProofs.Count == MaxRecentProofs) book.RecentProofs.RemoveAt(0);
			book.RecentProofs.Add(proof);
		}

		private static bool GrowthProofAppendWouldBeValid(KingdomGrowthBook book,
			KingdomGrowthProof candidate, KingdomGrowthSlotKind slot, KingdomGrowthFieldSlot field)
		{
			if (book == null || candidate == null || book.RecentProofs == null
				|| book.RecentProofs.Count > MaxRecentProofs
				|| !IsExactSuccessor(candidate.Sequence, GetGrowthRetired(book, slot, field))) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			long priorTick = -1L;
			int start = book.RecentProofs.Count == MaxRecentProofs ? 1 : 0;
			for (int i = start; i <= book.RecentProofs.Count; i++)
			{
				KingdomGrowthProof proof = i == book.RecentProofs.Count
					? candidate : book.RecentProofs[i];
				if (proof == null) return false;
				long retired = proof.Slot == slot
					&& (slot != KingdomGrowthSlotKind.Field || string.Equals(proof.FieldId,
						field == null ? null : field.FieldId, StringComparison.Ordinal))
					? candidate.Sequence : GrowthProofRetiredThrough(book, proof);
				if (!GrowthProofShape(book, proof, retired) || proof.Tick < priorTick
					|| !ids.Add(proof.Id)) return false;
				priorTick = proof.Tick;
			}
			return true;
		}

		private static bool GrowthProofShape(KingdomGrowthBook book, KingdomGrowthProof proof,
			long retiredThrough)
		{
			return proof != null && KnownGrowthSlot(proof.Slot) && proof.Sequence > 0L
				&& KnownGrowthAction(proof.Action) && SlotForGrowthAction(proof.Action) == proof.Slot
				&& (proof.Slot == KingdomGrowthSlotKind.Field ? ValidRootId(proof.FieldId)
					: proof.FieldId == null)
				&& string.Equals(proof.Id, GrowthOperationId(book.SettlementId, proof.Slot,
					proof.FieldId, proof.Sequence), StringComparison.Ordinal)
				&& ValidHashNamespace(proof.PlanHash, "growth-plan")
				&& proof.Tick >= 0L && proof.Sequence <= retiredThrough;
		}

		private static bool GrowthCropRowShape(KingdomGrowthBook book, KingdomGrowthCropRow row,
			bool requireLiveField)
		{
			KingdomGrowthFieldSlot field = FindGrowthField(book, row == null ? null : row.FieldId);
			return row != null && field != null && (!requireLiveField || !field.Quarantined)
				&& ValidRootId(row.RowId)
				&& ValidRootId(row.ObjectId) && ValidRootId(row.Marker) && ValidName(row.Blueprint)
				&& ValidName(row.ZoneId) && ValidRootId(row.OwnerId) && row.X >= 0
				&& row.X <= MaxCoordinate && row.Y >= 0 && row.Y <= MaxCoordinate
				&& row.Count > 0 && row.Count <= MaxPhysicalCount
				&& row.HasHarvestable && row.RegenTimer >= 0
				&& string.Equals(row.RegenTime, string.Empty, StringComparison.Ordinal)
				&& row.TileIndex >= -1 && GrowthBoundedPresentString(row.RenderTile)
				&& GrowthBoundedPresentString(row.RenderColor)
				&& GrowthBoundedPresentString(row.RenderDetail)
				&& GrowthBoundedPresentString(row.RenderString)
				&& GrowthBoundedPresentString(row.TileColor)
				&& GrowthWitnessHash(row.PartGraphHash) && GrowthWitnessHash(row.ObjectGraphHash)
				&& GrowthWitnessHash(row.TopologyHash) && row.Revision >= 0L
				&& (row.LastOperationId == null || ValidGeneratedId(row.LastOperationId));
		}

		public static void NormalizeGrowth(KingdomGrowthBook Book)
		{
			if (Book == null || GrowthEnvelopeWritable(Book)) return;
			Book.FormatVersion = CurrentGrowthFormatVersion;
			Book.Quarantined = true;
			Book.Fault = "malformed growth authority was quarantined";
			Book.OpaqueWireVersion = 0;
			Book.OpaquePayload = null;
			Book.SettlementId = null; Book.IdentityBound = false; Book.IdentityProof = null;
			Book.MigratedFromLifecycleVersion = 0; Book.MigrationPending = false;
			Book.MigrationTick = 0L; Book.OptionState = KingdomLifecycleOptionState.Unknown;
			Book.OptionTick = 0L; Book.HealthState = KingdomGrowthHealthState.Unknown;
			Book.HealthTick = 0L; Book.ScarcityOptionState = KingdomLifecycleOptionState.Unknown;
			Book.ScarcityOptionTick = 0L; Book.WorkPaused = false; Book.WorkPauseStartedTick = 0L;
			Book.WorkPausedTicks = 0L; Book.EffectiveWorkTick = 0L;
			Book.LastHeartbeatTick = 0L; Book.NextArrivalTick = 0L;
			Book.ArrivalIntervalTicks = 0L; Book.LastFetchTick = 0L;
			Book.LastMillTick = 0L; Book.LastSubsidenceTick = 0L;
			Book.LastDeliveryTick = 0L; Book.LastDepartureTick = 0L;
			Book.PendingCrop = 0; Book.PendingCropBlueprint = null; Book.PendingCropZoneId = null;
			Book.HeartbeatNextSequence = Book.ArrivalNextSequence =
				Book.DepartureNextSequence = Book.DeliveryNextSequence = 1L;
			Book.FetchNextSequence = Book.MillNextSequence = 1L;
			Book.ArrivalCandidateNextSequence = 1L;
			Book.HeartbeatRetiredThrough = Book.ArrivalRetiredThrough =
				Book.DepartureRetiredThrough = Book.DeliveryRetiredThrough = 0L;
			Book.FetchRetiredThrough = Book.MillRetiredThrough = 0L;
			Book.ArrivalCandidateRetiredThrough = 0L;
			Book.HeartbeatOp = Book.ArrivalOp = Book.DepartureOp = Book.DeliveryOp = null;
			Book.FetchOp = Book.MillOp = null; Book.ArrivalCandidate = null;
			Book.FieldOps = new List<KingdomGrowthFieldSlot>();
			Book.CropRows = new List<KingdomGrowthCropRow>();
			Book.Resources = new List<KingdomLifecycleResourceRevision>();
			Book.RecentProofs = new List<KingdomGrowthProof>();
		}

		private static KingdomGrowthBook NewStagedGrowth()
		{
			return new KingdomGrowthBook
			{
				FormatVersion = CurrentGrowthFormatVersion,
				MigratedFromLifecycleVersion = LegacyLifecycleFormatVersion,
				MigrationPending = true
			};
		}

		private static KingdomGrowthBook NewBoundGrowth(string settlementId)
		{
			if (!ValidRootId(settlementId)) return null;
			KingdomGrowthBook result = new KingdomGrowthBook
			{
				FormatVersion = CurrentGrowthFormatVersion,
				SettlementId = settlementId,
				IdentityBound = true,
				IdentityProof = GrowthIdentityProof(settlementId)
			};
			return result;
		}

		private static string GrowthIdentityProof(string settlementId)
		{
			return HashId("growth-binding", delegate(BinaryWriter w)
			{
				CanonicalString(w, settlementId);
			});
		}

		private static bool KnownGrowthHealth(KingdomGrowthHealthState state)
		{
			return Enum.IsDefined(typeof(KingdomGrowthHealthState), state);
		}

		private static bool KnownGrowthAction(KingdomGrowthAction action)
		{
			return Enum.IsDefined(typeof(KingdomGrowthAction), action) &&
				action != KingdomGrowthAction.None;
		}

		private static bool KnownGrowthPhase(KingdomGrowthPhase phase)
		{
			return Enum.IsDefined(typeof(KingdomGrowthPhase), phase) &&
				phase != KingdomGrowthPhase.Invalid;
		}

		private static bool KnownGrowthSlot(KingdomGrowthSlotKind slot)
		{
			return Enum.IsDefined(typeof(KingdomGrowthSlotKind), slot) &&
				slot != KingdomGrowthSlotKind.None;
		}

		private static bool GrowthCollectionsBounded(KingdomGrowthBook book)
		{
			return book != null && book.FieldOps != null && book.FieldOps.Count <= MaxGrowthFields
				&& book.CropRows != null && book.CropRows.Count <= MaxGrowthCropRows
				&& book.Resources != null && book.Resources.Count <= MaxResourceRows
				&& book.RecentProofs != null && book.RecentProofs.Count <= MaxRecentProofs;
		}

		private static bool PristineGrowthBook(KingdomGrowthBook book)
		{
			return book != null && book.FormatVersion == CurrentGrowthFormatVersion
				&& !book.Quarantined && book.Fault == null
				&& book.OpaqueWireVersion == 0 && book.OpaquePayload == null
				&& book.SettlementId == null && !book.IdentityBound
				&& book.IdentityProof == null
				&& book.MigratedFromLifecycleVersion == 0 && !book.MigrationPending
				&& book.MigrationTick == 0L && book.OptionState == KingdomLifecycleOptionState.Unknown
				&& book.OptionTick == 0L && book.HealthState == KingdomGrowthHealthState.Unknown
				&& book.HealthTick == 0L
				&& book.ScarcityOptionState == KingdomLifecycleOptionState.Unknown
				&& book.ScarcityOptionTick == 0L && !book.WorkPaused
				&& book.WorkPauseStartedTick == 0L
				&& book.WorkPausedTicks == 0L && book.EffectiveWorkTick == 0L
				&& book.LastHeartbeatTick == 0L && book.NextArrivalTick == 0L
				&& book.ArrivalIntervalTicks == 0L && book.LastFetchTick == 0L
				&& book.LastMillTick == 0L && book.LastSubsidenceTick == 0L
				&& book.LastDeliveryTick == 0L && book.LastDepartureTick == 0L
				&& book.PendingCrop == 0 && book.PendingCropBlueprint == null
				&& book.PendingCropZoneId == null
				&& book.HeartbeatNextSequence == 1L && book.HeartbeatRetiredThrough == 0L
				&& book.ArrivalNextSequence == 1L && book.ArrivalRetiredThrough == 0L
				&& book.DepartureNextSequence == 1L && book.DepartureRetiredThrough == 0L
				&& book.DeliveryNextSequence == 1L && book.DeliveryRetiredThrough == 0L
				&& book.FetchNextSequence == 1L && book.FetchRetiredThrough == 0L
				&& book.MillNextSequence == 1L && book.MillRetiredThrough == 0L
				&& book.ArrivalCandidateNextSequence == 1L
				&& book.ArrivalCandidateRetiredThrough == 0L
				&& book.HeartbeatOp == null && book.ArrivalOp == null
				&& book.DepartureOp == null && book.DeliveryOp == null
				&& book.FetchOp == null && book.MillOp == null && book.ArrivalCandidate == null
				&& GrowthCollectionsBounded(book) && book.FieldOps.Count == 0
				&& book.CropRows.Count == 0 && book.Resources.Count == 0
				&& book.RecentProofs.Count == 0;
		}

		internal static bool OpaqueGrowthParsedStateIsPristine(KingdomGrowthBook book)
		{
			if (book == null) return false;
			bool quarantined = book.Quarantined;
			string fault = book.Fault;
			int wireVersion = book.OpaqueWireVersion;
			byte[] payload = book.OpaquePayload;
			try
			{
				book.Quarantined = false;
				book.Fault = null;
				book.OpaqueWireVersion = 0;
				book.OpaquePayload = null;
				return PristineGrowthBook(book);
			}
			finally
			{
				book.Quarantined = quarantined;
				book.Fault = fault;
				book.OpaqueWireVersion = wireVersion;
				book.OpaquePayload = payload;
			}
		}

		private static bool StagedGrowthShape(KingdomGrowthBook book)
		{
			if (book == null || book.FormatVersion != CurrentGrowthFormatVersion || book.Quarantined
				|| book.Fault != null || book.OpaquePayload != null
				|| book.OpaqueWireVersion != 0 || !book.MigrationPending
				|| book.MigratedFromLifecycleVersion != LegacyLifecycleFormatVersion) return false;
			bool pending = book.MigrationPending;
			int migrated = book.MigratedFromLifecycleVersion;
			book.MigrationPending = false; book.MigratedFromLifecycleVersion = 0;
			bool result = PristineGrowthBook(book);
			book.MigrationPending = pending; book.MigratedFromLifecycleVersion = migrated;
			return result;
		}

		private static bool CanonicalQuarantinedGrowth(KingdomGrowthBook book)
		{
			if (book == null || !book.Quarantined || string.IsNullOrEmpty(book.Fault)
				|| TooLong(book.Fault, MaxTextChars) || book.OpaquePayload != null) return false;
			bool quarantined = book.Quarantined; string fault = book.Fault;
			book.Quarantined = false; book.Fault = null;
			bool result = PristineGrowthBook(book);
			book.Quarantined = quarantined; book.Fault = fault;
			return result;
		}

		private static bool GrowthAttachmentValid(KingdomLifecycleBook book)
		{
			if (book == null || book.Growth == null) return false;
			if (book.Growth.OpaquePayload != null || book.Growth.Quarantined)
				return GrowthEnvelopeWritable(book.Growth);
			if (book.Growth.MigrationPending) return StagedGrowthShape(book.Growth);
			if (!book.IdentityBound) return PristineGrowthBook(book.Growth);
			return CanOwnGrowthAuthority(book.Growth, book.SettlementId);
		}

		private static bool GrowthRootShape(KingdomGrowthBook book, bool ValidateOperations)
		{
			if (book == null || book.FormatVersion != CurrentGrowthFormatVersion || book.Quarantined
				|| book.OpaquePayload != null || book.OpaqueWireVersion != 0 || book.MigrationPending
				|| TooLong(book.Fault, MaxTextChars) || book.Fault != null
				|| !GrowthCollectionsBounded(book) || !KnownOption(book.OptionState)
				|| !KnownOption(book.ScarcityOptionState)
				|| !KnownGrowthHealth(book.HealthState) || book.OptionTick < 0L || book.HealthTick < 0L
				|| book.ScarcityOptionTick < 0L
				|| book.WorkPauseStartedTick < 0L || book.WorkPausedTicks < 0L
				|| book.EffectiveWorkTick < 0L || book.LastHeartbeatTick < 0L
				|| book.NextArrivalTick < 0L || book.ArrivalIntervalTicks < 0L
				|| book.LastFetchTick < 0L || book.LastMillTick < 0L || book.LastSubsidenceTick < 0L
				|| book.LastDeliveryTick < 0L || book.LastDepartureTick < 0L
				|| !ValidCount(book.PendingCrop)
				|| TooLong(book.PendingCropBlueprint, MaxNameChars)
				|| TooLong(book.PendingCropZoneId, MaxNameChars)
				|| !CounterShape(book.HeartbeatNextSequence, book.HeartbeatRetiredThrough)
				|| !CounterShape(book.ArrivalNextSequence, book.ArrivalRetiredThrough)
				|| !CounterShape(book.DepartureNextSequence, book.DepartureRetiredThrough)
				|| !CounterShape(book.DeliveryNextSequence, book.DeliveryRetiredThrough)
				|| !CounterShape(book.FetchNextSequence, book.FetchRetiredThrough)
				|| !CounterShape(book.MillNextSequence, book.MillRetiredThrough)
				|| !CounterShape(book.ArrivalCandidateNextSequence,
					book.ArrivalCandidateRetiredThrough)) return false;
			if (!book.IdentityBound || !ValidRootId(book.SettlementId)
				|| !string.Equals(book.IdentityProof, GrowthIdentityProof(book.SettlementId),
					StringComparison.Ordinal)) return false;
			if (book.WorkPaused)
			{
				if (book.WorkPauseStartedTick > Math.Max(book.OptionTick, book.HealthTick)
					|| !PausedArrivalClockAllowed(book)) return false;
			}
			else if ((book.OptionState == KingdomLifecycleOptionState.Disabled
				|| book.HealthState == KingdomGrowthHealthState.Unhealthy)) return false;
			if (!GrowthEffectiveWorkBounded(book)) return false;
			if (book.ArrivalIntervalTicks == 0L && book.NextArrivalTick != 0L) return false;
			if (book.PendingCrop == 0 ? (book.PendingCropBlueprint != null
				|| book.PendingCropZoneId != null)
				: (!ValidName(book.PendingCropBlueprint) || !ValidName(book.PendingCropZoneId)))
				return false;
			if (!GrowthFieldRowsValid(book) || !GrowthCropRowsValid(book)
				|| !GrowthResourceRowsValid(book) || !GrowthProofRowsValid(book)
				|| !GrowthArrivalCandidateShape(book, book.ArrivalCandidate, false)
				|| !GrowthActiveResourcesValid(book)
				|| !GrowthActiveIdentityClaimsValid(book, null)) return false;
			return !ValidateOperations || GrowthOperationsValid(book);
		}

		private static bool PausedArrivalClockAllowed(KingdomGrowthBook book)
		{
			if (book == null || !book.WorkPaused) return false;
			KingdomGrowthOperation operation = book.ArrivalOp;
			if (operation == null) return book.ArrivalCandidate == null
				? book.NextArrivalTick == 0L : book.NextArrivalTick > 0L;
			if (operation.Action != KingdomGrowthAction.Arrival || operation.ClockLease == null)
				return false;
			bool proved = operation.ClockState == KingdomLifecyclePhysicalState.Proved
				&& operation.ClockLease.State == KingdomLifecycleLeaseState.Proved;
			bool before = operation.ClockState == KingdomLifecyclePhysicalState.Prepared
				&& operation.ClockLease.State == KingdomLifecycleLeaseState.Prepared
				|| operation.ClockState == KingdomLifecyclePhysicalState.Intent
					&& operation.ClockLease.State == KingdomLifecycleLeaseState.Intent;
			return (proved && book.NextArrivalTick == operation.ClockLease.After)
				|| (before && book.NextArrivalTick == operation.ClockLease.Before);
		}

		private static bool GrowthArrivalCandidateShape(KingdomGrowthBook book,
			KingdomGrowthArrivalCandidate candidate, bool publication)
		{
			if (candidate == null) return IsExactSuccessor(book.ArrivalCandidateNextSequence,
				book.ArrivalCandidateRetiredThrough);
			if (!IsExactSuccessor(candidate.Sequence, book.ArrivalCandidateRetiredThrough)
				|| (publication ? book.ArrivalCandidateNextSequence != candidate.Sequence
					: !IsExactSuccessor(book.ArrivalCandidateNextSequence, candidate.Sequence))
				|| !string.Equals(candidate.Id, GrowthArrivalCandidateId(book.SettlementId,
					candidate.Sequence), StringComparison.Ordinal)
				|| !string.Equals(candidate.SettlementId, book.SettlementId,
					StringComparison.Ordinal) || candidate.CreatedTick < 0L
				|| candidate.UpdatedTick < candidate.CreatedTick
				|| !Enum.IsDefined(typeof(KingdomGrowthArrivalCandidatePhase), candidate.Phase)
				|| !Enum.IsDefined(typeof(KingdomGrowthArrivalDisposition), candidate.Disposition)
				|| !ValidRootId(candidate.Marker) || !ValidName(candidate.Blueprint)
				|| !ValidRootId(candidate.EscrowKey)
				|| candidate.CandidateLease == null || candidate.LodgingLease == null
				|| candidate.EscrowLease == null
				|| !GrowthLeaseShape(candidate.CandidateLease, candidate.Id, publication)
				|| !GrowthLeaseShape(candidate.LodgingLease, candidate.Id, publication)
				|| !GrowthLeaseShape(candidate.EscrowLease, candidate.Id, publication)
				|| candidate.CandidateLease.Kind != KingdomLifecycleResourceKind.GrowthArrivalCandidate
				|| candidate.LodgingLease.Kind != KingdomLifecycleResourceKind.GrowthArrivalCandidate
				|| candidate.EscrowLease.Kind != KingdomLifecycleResourceKind.GrowthEscrowRelease
				|| !string.Equals(candidate.CandidateLease.ScopeId, book.SettlementId,
					StringComparison.Ordinal)
				|| !string.Equals(candidate.CandidateLease.SubjectId, candidate.Id,
					StringComparison.Ordinal)
				|| !string.Equals(candidate.LodgingLease.ScopeId, book.SettlementId,
					StringComparison.Ordinal)
				|| !string.Equals(candidate.LodgingLease.SubjectId,
					ChildId(candidate.Id, "lodging-lease", 0), StringComparison.Ordinal)
				|| !string.Equals(candidate.EscrowLease.ScopeId, book.SettlementId,
					StringComparison.Ordinal)
				|| !string.Equals(candidate.EscrowLease.SubjectId, candidate.EscrowKey,
					StringComparison.Ordinal)
				|| TooLong(candidate.Fault, MaxTextChars)) return false;
			bool quarantined = candidate.Phase == KingdomGrowthArrivalCandidatePhase.Quarantined;
			if (quarantined ? string.IsNullOrEmpty(candidate.Fault)
				|| !Enum.IsDefined(typeof(KingdomGrowthArrivalCandidatePhase),
					candidate.EvidencePhase)
				|| candidate.EvidencePhase == KingdomGrowthArrivalCandidatePhase.Quarantined
				: candidate.Fault != null || (byte)candidate.EvidencePhase != 0) return false;
			KingdomGrowthArrivalCandidatePhase phase = quarantined
				? candidate.EvidencePhase : candidate.Phase;
			if (publication && phase != KingdomGrowthArrivalCandidatePhase.Prepared) return false;
			string hash;
			bool legacyUnbound = candidate.LegacyGrowthV1UnboundZone;
			if (legacyUnbound)
			{
				if (publication || candidate.LodgingZoneId != null
					|| phase != KingdomGrowthArrivalCandidatePhase.Prepared
						&& phase != KingdomGrowthArrivalCandidatePhase.CreateIntent
						&& phase != KingdomGrowthArrivalCandidatePhase.Escrowed
					|| !TryLegacyGrowthArrivalCandidateBasePlanHash(candidate, out hash)
					|| !string.Equals(candidate.PlanHash, hash, StringComparison.Ordinal)) return false;
			}
			else if (!TryGrowthArrivalCandidatePlanHash(candidate, out hash)
				|| (publication ? candidate.PlanHash != null
					: !string.Equals(candidate.PlanHash, hash, StringComparison.Ordinal))) return false;
			if (!GrowthArrivalCandidateLeaseStates(candidate, phase)
				|| !GrowthArrivalCreateStepShape(candidate, phase, legacyUnbound)) return false;
			if (phase == KingdomGrowthArrivalCandidatePhase.Prepared
				|| phase == KingdomGrowthArrivalCandidatePhase.CreateIntent)
				return candidate.ObjectId == null && candidate.DispositionStep == null
					&& GrowthArrivalLodgingEmpty(candidate, legacyUnbound)
					&& GrowthArrivalDispositionReasonShape(candidate)
					&& candidate.ConsumingOperationId == null
					&& candidate.ConsumingOperationSequence == 0L;
			if (!ValidRootId(candidate.ObjectId)) return false;
			if (phase == KingdomGrowthArrivalCandidatePhase.Escrowed)
				return candidate.DispositionStep == null
					&& GrowthArrivalLodgingEmpty(candidate, legacyUnbound)
					&& GrowthArrivalDispositionReasonShape(candidate)
					&& candidate.ConsumingOperationId == null
					&& candidate.ConsumingOperationSequence == 0L;
			if (phase == KingdomGrowthArrivalCandidatePhase.LodgingIntent)
				return candidate.DispositionStep == null
					&& GrowthArrivalLodgingIntentShape(candidate)
					&& GrowthArrivalDispositionReasonShape(candidate)
					&& candidate.ConsumingOperationId == null
					&& candidate.ConsumingOperationSequence == 0L;
			if (!GrowthArrivalLodgingObservedShape(candidate)
				|| !GrowthArrivalDispositionReasonShape(candidate)
				|| candidate.Disposition == KingdomGrowthArrivalDisposition.None) return false;
			if (phase == KingdomGrowthArrivalCandidatePhase.Observed)
				return candidate.DispositionStep == null && candidate.ConsumingOperationId == null
					&& candidate.ConsumingOperationSequence == 0L;
			bool joined = candidate.Disposition == KingdomGrowthArrivalDisposition.Joined;
			if (phase == KingdomGrowthArrivalCandidatePhase.ConsumeIntent
				|| phase == KingdomGrowthArrivalCandidatePhase.RefusalIntent)
				return (phase == KingdomGrowthArrivalCandidatePhase.ConsumeIntent) == joined
					&& candidate.ConsumingOperationSequence > 0L
					&& string.Equals(candidate.ConsumingOperationId,
						GrowthOperationId(candidate.SettlementId, KingdomGrowthSlotKind.Arrival,
							null, candidate.ConsumingOperationSequence), StringComparison.Ordinal)
					&& GrowthArrivalDispositionStepShape(candidate, false);
			return phase == KingdomGrowthArrivalCandidatePhase.Settled
				&& candidate.ConsumingOperationSequence > 0L
				&& string.Equals(candidate.ConsumingOperationId,
					GrowthOperationId(candidate.SettlementId, KingdomGrowthSlotKind.Arrival,
						null, candidate.ConsumingOperationSequence), StringComparison.Ordinal)
				&& GrowthArrivalDispositionStepShape(candidate, true);
		}

		private static bool GrowthArrivalCandidateLeaseStates(
			KingdomGrowthArrivalCandidate candidate, KingdomGrowthArrivalCandidatePhase phase)
		{
			KingdomLifecycleLeaseState create = phase == KingdomGrowthArrivalCandidatePhase.CreateIntent
				? KingdomLifecycleLeaseState.Intent
				: phase >= KingdomGrowthArrivalCandidatePhase.Escrowed
					? KingdomLifecycleLeaseState.Proved : KingdomLifecycleLeaseState.Prepared;
			KingdomLifecycleLeaseState lodging = phase == KingdomGrowthArrivalCandidatePhase.LodgingIntent
				? KingdomLifecycleLeaseState.Intent
				: phase >= KingdomGrowthArrivalCandidatePhase.Observed
					? KingdomLifecycleLeaseState.Proved : KingdomLifecycleLeaseState.Prepared;
			KingdomLifecycleLeaseState escrow = phase == KingdomGrowthArrivalCandidatePhase.ConsumeIntent
				|| phase == KingdomGrowthArrivalCandidatePhase.RefusalIntent
					? KingdomLifecycleLeaseState.Intent
					: phase == KingdomGrowthArrivalCandidatePhase.Settled
						? KingdomLifecycleLeaseState.Proved : KingdomLifecycleLeaseState.Prepared;
			return candidate.CandidateLease.State == create
				&& candidate.LodgingLease.State == lodging
				&& candidate.EscrowLease.State == escrow;
		}

		private static bool GrowthArrivalCreateStepShape(KingdomGrowthArrivalCandidate candidate,
			KingdomGrowthArrivalCandidatePhase phase, bool legacyV1 = false)
		{
			KingdomGrowthObjectCallbackStep step = candidate.CreateStep;
			if (step == null || step.Kind != KingdomGrowthObjectMutationKind.Create
				|| !string.Equals(step.EventId, ChildId(candidate.Id, "object-callback", 0),
					StringComparison.Ordinal)
				|| step.FromLocation != KingdomGrowthLocationKind.Absent
				|| step.ToLocation != KingdomGrowthLocationKind.Escrow
				|| !string.Equals(step.EscrowKey, candidate.EscrowKey, StringComparison.Ordinal)
				|| step.BeforeOwnerId != null || step.AfterOwnerId != null
				|| step.BeforeZoneId != null || step.AfterZoneId != null
				|| step.BeforeX != -1 || step.BeforeY != -1 || step.AfterX != -1 || step.AfterY != -1
				|| step.BeforeCount != 0 || step.AfterCount != 1 || !step.NoStack
				|| !GrowthWitnessHash(step.BeforeOwnerGraphHash)
				|| !GrowthWitnessHash(step.BeforeObjectGraphHash)
				|| !GrowthWitnessHash(step.BeforeTopologyHash)
				|| !string.Equals(step.ReceiptId,
					ChildId(candidate.Id, "object-callback-receipt", 0), StringComparison.Ordinal))
				return false;
			bool proved = phase >= KingdomGrowthArrivalCandidatePhase.Escrowed;
			if (proved) return GrowthObjectCallbackStepShape(step, candidate.Id,
				candidate.ObjectId, candidate.Marker, 0)
				&& step.State == KingdomLifecyclePhysicalState.Proved
				&& string.Equals(step.ReceiptProofId,
					GrowthArrivalCandidateCallbackProof(candidate, step, 0, legacyV1),
					StringComparison.Ordinal);
			if (step.AfterOwnerGraphHash != null || step.AfterObjectGraphHash != null
				|| step.AfterTopologyHash != null) return false;
			if (phase == KingdomGrowthArrivalCandidatePhase.Prepared)
				return step.State == KingdomLifecyclePhysicalState.Prepared
					&& step.ReceiptState == KingdomLifecyclePhysicalState.Prepared
					&& step.ReceiptBeforeMatches == -1 && step.ReceiptAfterMatches == -1
					&& step.ReceiptBeforeCount == -1 && step.ReceiptAfterCount == -1
					&& GrowthObjectCallbackReceiptEmpty(step);
			return step.State == KingdomLifecyclePhysicalState.Intent
				&& step.ReceiptState == KingdomLifecyclePhysicalState.Intent
				&& step.ReceiptBeforeMatches == 0 && step.ReceiptBeforeCount == 0
				&& step.ReceiptAfterMatches == -1 && step.ReceiptAfterCount == -1
				&& GrowthObjectCallbackReceiptBeforeExact(step)
				&& step.ReceiptAfterOwnerGraphHash == null
				&& step.ReceiptAfterObjectGraphHash == null
				&& step.ReceiptAfterTopologyHash == null
				&& step.ReceiptCallbackObjectId == null && step.ReceiptCallbackMarker == null
				&& step.ReceiptCallbackReferenceHash == null && !step.ReceiptSameReference
				&& step.ReceiptProofId == null;
		}

		private static bool GrowthArrivalLodgingEmpty(KingdomGrowthArrivalCandidate candidate,
			bool allowLegacyUnbound = false)
		{
			return (allowLegacyUnbound ? candidate.LodgingZoneId == null
				: ValidName(candidate.LodgingZoneId)) && candidate.LodgingX == -1
				&& candidate.LodgingY == -1 && candidate.LodgingBeforeGraphHash == null
				&& candidate.LodgingDeclaredGraphHash == null
				&& candidate.LodgingReceiptGraphHash == null
				&& candidate.LodgingCallbackReferenceHash == null
				&& !candidate.LodgingSameReference && candidate.LodgingReceiptId == null
				&& candidate.LodgingState == KingdomLifecyclePhysicalState.None;
		}

		private static bool GrowthArrivalLodgingIntentShape(
			KingdomGrowthArrivalCandidate candidate)
		{
			return ValidName(candidate.LodgingZoneId) && candidate.LodgingX >= 0
				&& candidate.LodgingX <= MaxCoordinate && candidate.LodgingY >= 0
				&& candidate.LodgingY <= MaxCoordinate
				&& GrowthWitnessHash(candidate.LodgingBeforeGraphHash)
				&& candidate.LodgingDeclaredGraphHash == null
				&& candidate.LodgingReceiptGraphHash == null
				&& candidate.LodgingCallbackReferenceHash == null
				&& !candidate.LodgingSameReference
				&& string.Equals(candidate.LodgingReceiptId,
					ChildId(candidate.Id, "lodging-receipt", 0), StringComparison.Ordinal)
				&& candidate.LodgingState == KingdomLifecyclePhysicalState.Intent;
		}

		private static bool GrowthArrivalLodgingObservedShape(
			KingdomGrowthArrivalCandidate candidate)
		{
			return ValidName(candidate.LodgingZoneId) && candidate.LodgingX >= 0
				&& candidate.LodgingX <= MaxCoordinate && candidate.LodgingY >= 0
				&& candidate.LodgingY <= MaxCoordinate
				&& GrowthWitnessHash(candidate.LodgingBeforeGraphHash)
				&& GrowthWitnessHash(candidate.LodgingDeclaredGraphHash)
				&& GrowthWitnessHash(candidate.LodgingReceiptGraphHash)
				&& GrowthWitnessHash(candidate.LodgingCallbackReferenceHash)
				&& candidate.LodgingSameReference
				&& string.Equals(candidate.LodgingReceiptId,
					ChildId(candidate.Id, "lodging-receipt", 0), StringComparison.Ordinal)
				&& string.Equals(candidate.LodgingDeclaredGraphHash,
					GrowthArrivalLodgingProof(candidate), StringComparison.Ordinal)
				&& candidate.LodgingState == KingdomLifecyclePhysicalState.Proved;
		}

		private static bool GrowthArrivalDispositionReasonShape(
			KingdomGrowthArrivalCandidate candidate)
		{
			if (!Enum.IsDefined(typeof(KingdomGrowthArrivalRefusalReason),
				candidate.RefusalReason)) return false;
			return candidate.Disposition == KingdomGrowthArrivalDisposition.NoAcceptableHome
				? candidate.RefusalReason != KingdomGrowthArrivalRefusalReason.None
				: candidate.RefusalReason == KingdomGrowthArrivalRefusalReason.None;
		}

		private static bool GrowthArrivalDispositionStepShape(
			KingdomGrowthArrivalCandidate candidate, bool proved)
		{
			KingdomGrowthObjectCallbackStep step = candidate.DispositionStep;
			if (step == null || !GrowthObjectCallbackStepShape(step, candidate.Id,
				candidate.ObjectId, candidate.Marker, 1,
				candidate.Disposition == KingdomGrowthArrivalDisposition.NoAcceptableHome)
				|| step.FromLocation != KingdomGrowthLocationKind.Escrow
				|| !string.Equals(step.EscrowKey, candidate.EscrowKey, StringComparison.Ordinal)
				|| step.BeforeOwnerId != null || step.BeforeZoneId != null
				|| step.BeforeX != -1 || step.BeforeY != -1 || step.BeforeCount != 1
				|| (candidate.Disposition == KingdomGrowthArrivalDisposition.Joined
					? step.Kind != KingdomGrowthObjectMutationKind.CellAdd
						|| step.ToLocation != KingdomGrowthLocationKind.Cell
						|| step.AfterOwnerId != null
						|| !string.Equals(step.AfterZoneId, candidate.LodgingZoneId,
							StringComparison.Ordinal)
						|| step.AfterX != candidate.LodgingX || step.AfterY != candidate.LodgingY
						|| step.AfterCount != 1 || !step.NoStack
					: step.Kind != KingdomGrowthObjectMutationKind.Obliterate
						|| step.ToLocation != KingdomGrowthLocationKind.Graveyard
						|| step.AfterOwnerId != null || step.AfterZoneId != null
						|| step.AfterX != -1 || step.AfterY != -1 || step.AfterCount != 0
						|| step.NoStack)) return false;
			return proved ? step.State == KingdomLifecyclePhysicalState.Proved
				&& step.ReceiptSameReference == (candidate.Disposition ==
					KingdomGrowthArrivalDisposition.Joined)
				&& string.Equals(step.ReceiptProofId,
					GrowthArrivalCandidateCallbackProof(candidate, step, 1),
					StringComparison.Ordinal)
				: step.State == KingdomLifecyclePhysicalState.Intent;
		}

		private static bool GrowthObjectCallbackSettledForCandidate(
			KingdomGrowthArrivalCandidate candidate)
		{
			return candidate.DispositionStep != null
				&& candidate.DispositionStep.State == KingdomLifecyclePhysicalState.Proved
				&& candidate.DispositionStep.ReceiptState == KingdomLifecyclePhysicalState.Proved;
		}

		private static bool GrowthFieldRowsValid(KingdomGrowthBook book)
		{
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < book.FieldOps.Count; i++)
			{
				KingdomGrowthFieldSlot field = book.FieldOps[i];
				if (field == null || !ValidRootId(field.FieldId) || !ids.Add(field.FieldId)
					|| !CounterShape(field.NextSequence, field.RetiredThrough) || field.ClockTick < 0L
					|| field.CommitRevision < 0L || field.NextStageTick < 0L || field.SownTick < 0L
					|| field.Cycles < 0 || TooLong(field.Fault, MaxTextChars)
					|| !GrowthFieldAuthorityShape(field)) return false;
				if (field.Quarantined)
				{
					if (string.IsNullOrEmpty(field.Fault)
						|| !GrowthOperationEvidenceBounded(field.Operation)) return false;
				}
				else if (field.Fault != null) return false;
			}
			return true;
		}

		private static bool GrowthFieldAuthorityShape(KingdomGrowthFieldSlot field)
		{
			bool dormant = field.WorkObjectId == null && field.WorkPartId == null
				&& field.Marker == null && field.Blueprint == null && field.ZoneId == null
				&& field.X == -1 && field.Y == -1 && field.CropBlueprint == null
				&& field.Stage == 0 && field.NextStageTick == 0L && field.SownTick == 0L
				&& field.Cycles == 0 && field.SaidWant == 0 && field.DeclaredRows == 0
				&& field.EffectivenessPercent == 0 && field.MethodPercent == 0
				&& !field.NoLarderAnnounced
				&& field.SeedBlueprint == null && field.PartGraphHash == null
				&& field.ObjectGraphHash == null && field.TopologyHash == null
				&& field.CommitRevision == 0L && field.LastOperationId == null;
			if (dormant) return true;
			return ValidRootId(field.WorkObjectId) && ValidRootId(field.WorkPartId)
				&& ValidRootId(field.Marker) && ValidName(field.Blueprint) && ValidName(field.ZoneId)
				&& field.X >= 0 && field.X <= MaxCoordinate && field.Y >= 0
				&& field.Y <= MaxCoordinate && ValidName(field.CropBlueprint)
				&& field.Stage >= 0 && field.Stage <= 255 && field.SaidWant >= 0
				&& field.SaidWant <= 4 && field.DeclaredRows >= 0
				&& field.DeclaredRows <= MaxGrowthCropRows
				&& field.EffectivenessPercent > 0 && field.EffectivenessPercent <= 100
				&& field.MethodPercent >= 100
				&& field.MethodPercent <= KingdomResearchRules.MaxMethodPercent
				&& ValidName(field.SeedBlueprint)
				&& GrowthWitnessHash(field.PartGraphHash)
				&& GrowthWitnessHash(field.ObjectGraphHash)
				&& GrowthWitnessHash(field.TopologyHash)
				&& (field.LastOperationId == null || ValidGeneratedId(field.LastOperationId));
		}

		private static bool GrowthCropRowsValid(KingdomGrowthBook book)
		{
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> objects = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> markers = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < book.CropRows.Count; i++)
			{
				KingdomGrowthCropRow row = book.CropRows[i];
				if (!GrowthCropRowShape(book, row, false) || !ids.Add(row.RowId)
					|| !objects.Add(row.ObjectId) || !markers.Add(row.Marker)) return false;
			}
			return true;
		}

		private static bool GrowthResourceRowsValid(KingdomGrowthBook book)
		{
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < book.Resources.Count; i++)
				if (!GrowthResourceShape(book.Resources[i]) || !keys.Add(book.Resources[i].Key))
					return false;
			return true;
		}

		private static bool GrowthActiveResourcesValid(KingdomGrowthBook book)
		{
			Dictionary<string, string> expected = new Dictionary<string, string>(StringComparer.Ordinal);
			List<KingdomGrowthOperation> operations = new List<KingdomGrowthOperation>();
			if (book.HeartbeatOp != null) operations.Add(book.HeartbeatOp);
			if (book.ArrivalOp != null) operations.Add(book.ArrivalOp);
			if (book.DepartureOp != null) operations.Add(book.DepartureOp);
			if (book.DeliveryOp != null) operations.Add(book.DeliveryOp);
			if (book.FetchOp != null) operations.Add(book.FetchOp);
			if (book.MillOp != null) operations.Add(book.MillOp);
			for (int i = 0; i < book.FieldOps.Count; i++)
				if (book.FieldOps[i].Operation != null && (!book.FieldOps[i].Quarantined
					|| ValidHashNamespace(book.FieldOps[i].Operation.PlanHash, "growth-plan")))
					operations.Add(book.FieldOps[i].Operation);
			for (int i = 0; i < operations.Count; i++)
			{
				KingdomGrowthOperation operation = operations[i];
				List<KingdomLifecycleResourceLease> leases = GrowthLeases(operation);
				if (leases == null) return false;
				for (int j = 0; j < leases.Count; j++)
				{
					KingdomLifecycleResourceLease lease = leases[j];
					KingdomLifecycleResourceRevision row = FindGrowthResource(book, lease.Key);
					if (!GrowthResourceMatches(row, lease) || expected.ContainsKey(lease.Key)
						|| !string.Equals(row.ActiveOperationId, operation.Id,
							StringComparison.Ordinal)) return false;
					if (lease.State == KingdomLifecycleLeaseState.Proved)
					{
						if (row.Revision != lease.AfterRevision || !string.Equals(row.LastOperationId,
							operation.Id, StringComparison.Ordinal)) return false;
					}
					else if (lease.State == KingdomLifecycleLeaseState.Prepared
						|| lease.State == KingdomLifecycleLeaseState.Intent)
					{
						if (row.Revision != lease.BeforeRevision || string.Equals(row.LastOperationId,
							operation.Id, StringComparison.Ordinal)) return false;
					}
					else return false;
					expected.Add(lease.Key, operation.Id);
				}
			}
			if (book.ArrivalCandidate != null)
			{
				KingdomLifecycleResourceLease[] candidateLeases =
				{
					book.ArrivalCandidate.CandidateLease,
					book.ArrivalCandidate.LodgingLease,
					book.ArrivalCandidate.EscrowLease
				};
				for (int i = 0; i < candidateLeases.Length; i++)
				{
					KingdomLifecycleResourceLease lease = candidateLeases[i];
					KingdomLifecycleResourceRevision row = FindGrowthResource(book,
						lease == null ? null : lease.Key);
					if (!GrowthResourceMatches(row, lease) || expected.ContainsKey(lease.Key)
						|| !string.Equals(row.ActiveOperationId, book.ArrivalCandidate.Id,
							StringComparison.Ordinal)) return false;
					if (lease.State == KingdomLifecycleLeaseState.Proved)
					{
						if (row.Revision != lease.AfterRevision || !string.Equals(
							row.LastOperationId, book.ArrivalCandidate.Id,
							StringComparison.Ordinal)) return false;
					}
					else if (lease.State == KingdomLifecycleLeaseState.Prepared
						|| lease.State == KingdomLifecycleLeaseState.Intent)
					{
						if (row.Revision != lease.BeforeRevision || string.Equals(
							row.LastOperationId, book.ArrivalCandidate.Id,
							StringComparison.Ordinal)) return false;
					}
					else return false;
					expected.Add(lease.Key, book.ArrivalCandidate.Id);
				}
			}
			for (int i = 0; i < book.Resources.Count; i++)
			{
				KingdomLifecycleResourceRevision row = book.Resources[i];
				if (string.IsNullOrEmpty(row.ActiveOperationId)) continue;
				if (!expected.TryGetValue(row.Key, out string operationId)
					|| !string.Equals(operationId, row.ActiveOperationId, StringComparison.Ordinal))
					return false;
			}
			return true;
		}

		private static bool GrowthActiveIdentityClaimsValid(KingdomGrowthBook book,
			KingdomGrowthOperation candidate)
		{
			if (book == null) return false;
			KingdomGrowthOperation arrivalOwner = book.ArrivalOp;
			if (arrivalOwner == null && candidate != null
				&& candidate.Action == KingdomGrowthAction.Arrival)
				arrivalOwner = candidate;
			Dictionary<string, string> claims =
				new Dictionary<string, string>(StringComparer.Ordinal);
			if (!ClaimGrowthOperationIdentities(claims, book.HeartbeatOp)
				|| !ClaimGrowthOperationIdentities(claims, book.ArrivalOp)
				|| !ClaimGrowthOperationIdentities(claims, book.DepartureOp)
				|| !ClaimGrowthOperationIdentities(claims, book.DeliveryOp)
				|| !ClaimGrowthOperationIdentities(claims, book.FetchOp)
				|| !ClaimGrowthOperationIdentities(claims, book.MillOp)
				|| !ClaimGrowthArrivalCandidateIdentities(claims,
					book.ArrivalCandidate, arrivalOwner)) return false;
			for (int i = 0; i < book.FieldOps.Count; i++)
			{
				KingdomGrowthFieldSlot field = book.FieldOps[i];
				if (field == null || field.Operation == null) continue;
				if (field.Quarantined
					&& !ValidHashNamespace(field.Operation.PlanHash, "growth-plan")) continue;
				if (!ClaimGrowthOperationIdentities(claims, field.Operation)) return false;
			}
			if (candidate != null && (!GrowthOperationAlreadyPresent(book, candidate)
				&& !ClaimGrowthOperationIdentities(claims, candidate))) return false;
			for (int i = 0; i < book.CropRows.Count; i++)
			{
				KingdomGrowthCropRow row = book.CropRows[i];
				string owner = "crop-row:" + (row == null ? "?" : row.RowId);
				KingdomGrowthOperation fieldOperation = row == null ? null
					: GetGrowthOperation(book, KingdomGrowthSlotKind.Field, row.FieldId);
				if (candidate != null && IsGrowthFieldAction(candidate.Action)
					&& string.Equals(candidate.FieldId, row == null ? null : row.FieldId,
						StringComparison.Ordinal)) fieldOperation = candidate;
				if (fieldOperation != null && GrowthOperationUsesCropRow(fieldOperation, row))
					owner = fieldOperation.Id;
				if (row == null || !ClaimGrowthIdentity(claims, "object", row.ObjectId, owner)
					|| !ClaimGrowthIdentity(claims, "marker", row.Marker, owner)) return false;
			}
			return true;
		}

		private static bool GrowthOperationUsesCropRow(KingdomGrowthOperation operation,
			KingdomGrowthCropRow row)
		{
			return operation != null && row != null
				&& (GrowthObjectLegsUseCropRow(operation.Sources, row)
					|| GrowthObjectLegsUseCropRow(operation.Outputs, row));
		}

		private static bool GrowthObjectLegsUseCropRow(List<KingdomGrowthObjectLeg> legs,
			KingdomGrowthCropRow row)
		{
			if (legs == null) return false;
			for (int i = 0; i < legs.Count; i++)
				if (string.Equals(legs[i].ObjectId, row.ObjectId, StringComparison.Ordinal)
					&& string.Equals(legs[i].Marker, row.Marker, StringComparison.Ordinal)) return true;
			return false;
		}

		private static bool ClaimGrowthArrivalCandidateIdentities(
			Dictionary<string, string> claims, KingdomGrowthArrivalCandidate candidate,
			KingdomGrowthOperation arrival)
		{
			if (candidate == null) return true;
			string physicalOwner = candidate.Id;
			if (arrival != null && arrival.Action == KingdomGrowthAction.Arrival
				&& arrival.ArrivalDisposition == KingdomGrowthArrivalDisposition.Joined
				&& string.Equals(arrival.ArrivalCandidateId, candidate.Id,
					StringComparison.Ordinal)
				&& string.Equals(arrival.TargetId, candidate.ObjectId, StringComparison.Ordinal)
				&& string.Equals(arrival.TargetMarker, candidate.Marker,
					StringComparison.Ordinal)) physicalOwner = arrival.Id;
			return ValidGeneratedId(candidate.Id)
				&& ClaimGrowthIdentity(claims, "marker", candidate.Marker, physicalOwner)
				&& ClaimGrowthIdentity(claims, "object", candidate.ObjectId, physicalOwner)
				&& ClaimGrowthIdentity(claims, "escrow", candidate.EscrowKey, candidate.Id);
		}

		private static bool GrowthOperationAlreadyPresent(KingdomGrowthBook book,
			KingdomGrowthOperation operation)
		{
			if (ReferenceEquals(book.HeartbeatOp, operation) || ReferenceEquals(book.ArrivalOp, operation)
				|| ReferenceEquals(book.DepartureOp, operation)
				|| ReferenceEquals(book.DeliveryOp, operation)
				|| ReferenceEquals(book.FetchOp, operation)
				|| ReferenceEquals(book.MillOp, operation)) return true;
			for (int i = 0; i < book.FieldOps.Count; i++)
				if (ReferenceEquals(book.FieldOps[i].Operation, operation)) return true;
			return false;
		}

		private static bool ClaimGrowthOperationIdentities(Dictionary<string, string> claims,
			KingdomGrowthOperation operation)
		{
			if (operation == null) return true;
			if (!ValidGeneratedId(operation.Id) || operation.WaterLegs == null
				|| operation.Sources == null || operation.Outputs == null) return false;
			string owner = operation.Id;
			if (!ClaimGrowthIdentity(claims, "field", operation.FieldId, owner)
				|| !ClaimGrowthIdentity(claims, "object", operation.TargetId, owner)
				|| !ClaimGrowthIdentity(claims, "marker", operation.TargetMarker, owner)) return false;
			if (operation.TargetId != null)
			{
				string topology = TopologyId(operation.TargetTopology, operation.TargetOwnerId,
					operation.ZoneId, operation.TargetX, operation.TargetY);
				if (topology == null || !ClaimGrowthIdentity(claims, "target-topology",
					operation.TargetId + "\n" + topology, owner)) return false;
			}
			for (int i = 0; i < operation.WaterLegs.Count; i++)
			{
				KingdomGrowthWaterLeg leg = operation.WaterLegs[i];
				if (leg == null || !ClaimGrowthIdentity(claims, "water-container",
					leg.ContainerId, owner)) return false;
				string topology = TopologyId(leg.OwnerTopology, leg.OwnerId, leg.ZoneId, leg.X, leg.Y);
				if (topology == null || !ClaimGrowthIdentity(claims, "water-topology",
					leg.ContainerId + "\n" + topology, owner)) return false;
			}
			if (!ClaimGrowthObjectIdentities(claims, operation.Sources, owner)
				|| !ClaimGrowthObjectIdentities(claims, operation.Outputs, owner)) return false;
			return true;
		}

		private static bool ClaimGrowthObjectIdentities(Dictionary<string, string> claims,
			List<KingdomGrowthObjectLeg> legs, string owner)
		{
			for (int i = 0; i < legs.Count; i++)
			{
				KingdomGrowthObjectLeg leg = legs[i];
				if (leg == null || !ClaimGrowthIdentity(claims, "object", leg.ObjectId, owner)
					|| !ClaimGrowthIdentity(claims, "marker", leg.Marker, owner)
					|| !ClaimGrowthIdentity(claims, "marker", leg.CreatedMarker, owner)
					|| !ClaimGrowthIdentity(claims, "marker", leg.DetachedMarker, owner)) return false;
			}
			return true;
		}

		private static bool ClaimGrowthIdentity(Dictionary<string, string> claims,
			string kind, string value, string owner)
		{
			if (value == null) return true;
			if (value.Length == 0) return false;
			string key = kind + "\n" + value;
			string prior;
			if (claims.TryGetValue(key, out prior))
				return string.Equals(prior, owner, StringComparison.Ordinal);
			claims.Add(key, owner);
			return true;
		}

		private static bool GrowthProofRowsValid(KingdomGrowthBook book)
		{
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			long priorTick = -1L;
			for (int i = 0; i < book.RecentProofs.Count; i++)
			{
				KingdomGrowthProof proof = book.RecentProofs[i];
				if (proof == null || !KnownGrowthSlot(proof.Slot) || proof.Sequence <= 0L
					|| !KnownGrowthAction(proof.Action) || proof.Tick < priorTick || !ids.Add(proof.Id)
					|| SlotForGrowthAction(proof.Action) != proof.Slot
					|| (proof.Slot == KingdomGrowthSlotKind.Field ? !ValidRootId(proof.FieldId)
						: proof.FieldId != null)
					|| !string.Equals(proof.Id, GrowthOperationId(book.SettlementId, proof.Slot,
						proof.FieldId, proof.Sequence), StringComparison.Ordinal)
					|| !ValidHashNamespace(proof.PlanHash, "growth-plan")
					|| proof.Sequence > GrowthProofRetiredThrough(book, proof)) return false;
				priorTick = proof.Tick;
			}
			return true;
		}

		private static long GrowthProofRetiredThrough(KingdomGrowthBook book,
			KingdomGrowthProof proof)
		{
			if (proof.Slot == KingdomGrowthSlotKind.Field)
			{
				KingdomGrowthFieldSlot field = FindGrowthField(book, proof.FieldId);
				return field == null ? -1L : field.RetiredThrough;
			}
			return GetGrowthRetired(book, proof.Slot, null);
		}

		private static bool GrowthOperationsValid(KingdomGrowthBook book)
		{
			if (!GrowthOperationSlotValid(book, KingdomGrowthSlotKind.Heartbeat, null,
				book.HeartbeatOp, book.HeartbeatNextSequence, book.HeartbeatRetiredThrough)
				|| !GrowthOperationSlotValid(book, KingdomGrowthSlotKind.Arrival, null,
					book.ArrivalOp, book.ArrivalNextSequence, book.ArrivalRetiredThrough)
				|| !GrowthOperationSlotValid(book, KingdomGrowthSlotKind.Departure, null,
					book.DepartureOp, book.DepartureNextSequence, book.DepartureRetiredThrough)
				|| !GrowthOperationSlotValid(book, KingdomGrowthSlotKind.Delivery, null,
					book.DeliveryOp, book.DeliveryNextSequence, book.DeliveryRetiredThrough)
				|| !GrowthOperationSlotValid(book, KingdomGrowthSlotKind.Fetch, null,
					book.FetchOp, book.FetchNextSequence, book.FetchRetiredThrough)
				|| !GrowthOperationSlotValid(book, KingdomGrowthSlotKind.Mill, null,
					book.MillOp, book.MillNextSequence, book.MillRetiredThrough)) return false;
			for (int i = 0; i < book.FieldOps.Count; i++)
			{
				KingdomGrowthFieldSlot field = book.FieldOps[i];
				if (field.Quarantined) continue;
				if (!GrowthOperationSlotValid(book, KingdomGrowthSlotKind.Field, field.FieldId,
					field.Operation, field.NextSequence, field.RetiredThrough)) return false;
			}
			return true;
		}

		private static bool GrowthOperationSlotValid(KingdomGrowthBook book,
			KingdomGrowthSlotKind slot, string fieldId, KingdomGrowthOperation operation,
			long next, long retired)
		{
			if (operation == null) return IsExactSuccessor(next, retired);
			KingdomGrowthFieldSlot field = slot == KingdomGrowthSlotKind.Field
				? FindGrowthField(book, fieldId) : null;
			return IsExactSuccessor(operation.Sequence, retired)
				&& IsExactSuccessor(next, operation.Sequence)
				&& GrowthOperationShape(book, operation, slot, fieldId, false)
				&& GrowthPersistedClockMatches(book, operation, field)
				&& GrowthPersistedDomainScalarsMatch(book, operation);
		}

		private static bool GrowthPersistedDomainScalarsMatch(KingdomGrowthBook book,
			KingdomGrowthOperation operation)
		{
			KingdomGrowthFieldSlot field = SlotForGrowthAction(operation.Action)
				== KingdomGrowthSlotKind.Field ? FindGrowthField(book, operation.FieldId) : null;
			for (int i = 0; i < operation.DomainSteps.Count; i++)
			{
				KingdomGrowthDomainStep step = operation.DomainSteps[i];
				bool proved = step.State == KingdomLifecyclePhysicalState.Proved;
				if (step.Kind == KingdomGrowthDomainStepKind.Field
					&& !GrowthFieldMatchesState(field, proved ? step.FieldAfter : step.FieldBefore))
					return false;
				if (step.Kind == KingdomGrowthDomainStepKind.CropRegistry
					&& !GrowthCropRowsEqual(book.CropRows,
						proved ? step.CropRowsAfter : step.CropRowsBefore)) return false;
			}
			KingdomGrowthDomainStep pending = FindGrowthDomain(operation,
				KingdomGrowthDomainStepKind.PendingCrop);
			int pendingValue = pending != null
				&& pending.State == KingdomLifecyclePhysicalState.Proved
					? operation.PendingCropAfter : operation.PendingCropBefore;
			bool pendingProved = pending != null
				&& pending.State == KingdomLifecyclePhysicalState.Proved;
			string pendingBlueprint = pendingProved ? operation.PendingCropBlueprintAfter
				: operation.PendingCropBlueprintBefore;
			string pendingZone = pendingProved ? operation.PendingCropZoneIdAfter
				: operation.PendingCropZoneIdBefore;
			if (book.PendingCrop != pendingValue
				|| !string.Equals(book.PendingCropBlueprint, pendingBlueprint,
					StringComparison.Ordinal)
				|| !string.Equals(book.PendingCropZoneId, pendingZone,
					StringComparison.Ordinal)) return false;
			KingdomGrowthDomainStep subsidence = FindGrowthDomain(operation,
				KingdomGrowthDomainStepKind.SubsidenceSchedule);
			long subsidenceValue = subsidence != null
				&& subsidence.State == KingdomLifecyclePhysicalState.Proved
					? operation.SubsidenceAfter : operation.SubsidenceBefore;
			return book.LastSubsidenceTick == subsidenceValue;
		}

		private static bool GrowthPersistedClockMatches(KingdomGrowthBook book,
			KingdomGrowthOperation operation, KingdomGrowthFieldSlot field)
		{
			if (book == null || operation == null || operation.ClockLease == null) return false;
			bool proved = operation.ClockState == KingdomLifecyclePhysicalState.Proved
				&& operation.ClockLease.State == KingdomLifecycleLeaseState.Proved;
			bool before = operation.ClockState == KingdomLifecyclePhysicalState.Prepared
				&& operation.ClockLease.State == KingdomLifecycleLeaseState.Prepared
				|| operation.ClockState == KingdomLifecyclePhysicalState.Intent
					&& operation.ClockLease.State == KingdomLifecycleLeaseState.Intent;
			if (!proved && !before) return false;
			long expected = proved ? operation.ClockLease.After : operation.ClockLease.Before;
			if (GrowthClockValue(book, operation.Action, field) != expected) return false;
			return field == null || field.ClockTick == (proved
				? operation.FieldClockAfter : operation.FieldClockBefore)
				&& (proved ? string.Equals(field.LastOperationId, operation.Id,
					StringComparison.Ordinal) : !string.Equals(field.LastOperationId,
					operation.Id, StringComparison.Ordinal));
		}

		private static bool GrowthOperationShape(KingdomGrowthBook book,
			KingdomGrowthOperation operation, KingdomGrowthSlotKind slot, string fieldId,
			bool publication)
		{
			if (book == null || operation == null || !KnownGrowthAction(operation.Action)
				|| !KnownGrowthPhase(operation.Phase) || operation.Sequence <= 0L
				|| operation.CreatedTick < 0L || operation.UpdatedTick < operation.CreatedTick
				|| !string.Equals(operation.SettlementId, book.SettlementId, StringComparison.Ordinal)
				|| !string.Equals(operation.FieldId, fieldId, StringComparison.Ordinal)
				|| (slot == KingdomGrowthSlotKind.Field ? !ValidRootId(operation.FieldId)
					: operation.FieldId != null)
				|| !string.Equals(operation.Id, GrowthOperationId(book.SettlementId, slot,
					fieldId, operation.Sequence), StringComparison.Ordinal)
				|| operation.Phase != KingdomGrowthPhase.Quarantined
					&& GrowthPhaseIndex(operation, operation.Phase) < 0
				|| operation.WaterLegs == null || operation.WaterLegs.Count > MaxWaterLegs
				|| operation.Sources == null || operation.Sources.Count > MaxGrowthSources
				|| operation.Outputs == null || operation.Outputs.Count > MaxGrowthOutputs
				|| operation.DomainSteps == null || operation.DomainSteps.Count > MaxResourceLeases
				|| operation.WaterCursor < 0 || operation.WaterCursor > operation.WaterLegs.Count
				|| operation.SourceCursor < 0 || operation.SourceCursor > operation.Sources.Count
				|| operation.OutputCursor < 0 || operation.OutputCursor > operation.Outputs.Count
				|| operation.DomainCursor < 0 || operation.DomainCursor > operation.DomainSteps.Count
				|| !KnownPhysical(operation.ClockState)
				|| operation.ClockLease == null || !GrowthLeaseShape(operation.ClockLease,
					operation.Id, publication) || operation.ClockLease.Kind !=
					KingdomLifecycleResourceKind.GrowthClock || TooLong(operation.Fault, MaxTextChars)
				|| !GrowthOperationScalarsValid(book, operation, slot, fieldId))
				return false;
			if (slot == KingdomGrowthSlotKind.Heartbeat && operation.Action != KingdomGrowthAction.Heartbeat
				|| slot == KingdomGrowthSlotKind.Arrival && operation.Action != KingdomGrowthAction.Arrival
				|| slot == KingdomGrowthSlotKind.Departure && operation.Action != KingdomGrowthAction.Departure
				|| slot == KingdomGrowthSlotKind.Delivery && operation.Action != KingdomGrowthAction.Delivery
				|| slot == KingdomGrowthSlotKind.Fetch && operation.Action != KingdomGrowthAction.Fetch
				|| slot == KingdomGrowthSlotKind.Mill && operation.Action != KingdomGrowthAction.Mill
				|| slot == KingdomGrowthSlotKind.Field && !IsGrowthFieldAction(operation.Action))
				return false;
			if (!GrowthTargetShape(operation, slot) || !GrowthPrefixShape(operation, publication)
				|| !GrowthOutboxShape(operation, publication)) return false;
			HashSet<string> events = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> leaseKeys = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> waterContainers = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> objectIds = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> markers = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < operation.WaterLegs.Count; i++)
				if (!GrowthWaterShape(operation, operation.WaterLegs[i], i, publication)
					|| !events.Add(operation.WaterLegs[i].EventId)
					|| !waterContainers.Add(operation.WaterLegs[i].ContainerId)
					|| !leaseKeys.Add(operation.WaterLegs[i].Lease.Key)) return false;
			for (int i = 0; i < operation.Sources.Count; i++)
				if (!GrowthObjectShape(operation, operation.Sources[i], i, false, publication)
					|| !events.Add(operation.Sources[i].EventId)
					|| !objectIds.Add(operation.Sources[i].ObjectId)
					|| !markers.Add(operation.Sources[i].Marker)
					|| !leaseKeys.Add(operation.Sources[i].Lease.Key)) return false;
			for (int i = 0; i < operation.Outputs.Count; i++)
				if (!GrowthObjectShape(operation, operation.Outputs[i], i, true, publication)
					|| !events.Add(operation.Outputs[i].EventId)
					|| (operation.Outputs[i].ObjectId != null
						&& !objectIds.Add(operation.Outputs[i].ObjectId))
					|| !markers.Add(operation.Outputs[i].Marker)
					|| !leaseKeys.Add(operation.Outputs[i].Lease.Key)) return false;
			for (int i = 0; i < operation.DomainSteps.Count; i++)
				if (!GrowthDomainShape(operation, operation.DomainSteps[i], i, publication)
					|| !events.Add(operation.DomainSteps[i].EventId)
					|| !leaseKeys.Add(operation.DomainSteps[i].Lease.Key)) return false;
			if (!leaseKeys.Add(operation.ClockLease.Key)
				|| !GrowthGroupsMatchAction(operation)
				|| !GrowthArrivalCandidateBindingShape(book, operation, publication)) return false;
			string hash;
			if (!TryGrowthPlanHash(operation, out hash)) return false;
			if (publication)
				return operation.Phase == KingdomGrowthPhase.Prepared
					&& operation.CreatedTick == operation.UpdatedTick
					&& operation.PlanHash == null
					&& operation.Fault == null
					&& operation.ClockState == KingdomLifecyclePhysicalState.Prepared;
			return string.Equals(operation.PlanHash, hash, StringComparison.Ordinal)
				&& (operation.Phase == KingdomGrowthPhase.Quarantined
					? !string.IsNullOrEmpty(operation.Fault) : operation.Fault == null);
		}

		public static bool GrowthPhaseAllowed(KingdomGrowthAction action, KingdomGrowthPhase phase)
		{
			if (!KnownGrowthAction(action) || !KnownGrowthPhase(phase)) return false;
			if (phase == KingdomGrowthPhase.Quarantined) return true;
			if (phase == KingdomGrowthPhase.Prepared || phase == KingdomGrowthPhase.DomainIntent
				|| phase == KingdomGrowthPhase.DomainSettled || phase == KingdomGrowthPhase.ClockIntent
				|| phase == KingdomGrowthPhase.Sinks || phase == KingdomGrowthPhase.Terminal) return true;
			if (phase == KingdomGrowthPhase.WaterIntent || phase == KingdomGrowthPhase.WaterSettled)
				return action == KingdomGrowthAction.Heartbeat || action == KingdomGrowthAction.Fetch
					|| action == KingdomGrowthAction.Arrival || action == KingdomGrowthAction.Sow;
			if (phase == KingdomGrowthPhase.SourceIntent || phase == KingdomGrowthPhase.SourcesSettled)
				return action == KingdomGrowthAction.Heartbeat || action == KingdomGrowthAction.Departure
					|| action == KingdomGrowthAction.Mill || action == KingdomGrowthAction.Sow
					|| action == KingdomGrowthAction.Withdraw || action == KingdomGrowthAction.Ripen
					|| action == KingdomGrowthAction.Harvest;
			if (phase == KingdomGrowthPhase.OutputIntent || phase == KingdomGrowthPhase.OutputsSettled)
				return action == KingdomGrowthAction.Arrival || action == KingdomGrowthAction.Delivery
					|| action == KingdomGrowthAction.Mill || action == KingdomGrowthAction.Sow
					|| action == KingdomGrowthAction.Withdraw || action == KingdomGrowthAction.Harvest;
			return false;
		}

		private static bool GrowthOperationScalarsValid(KingdomGrowthBook book,
			KingdomGrowthOperation operation, KingdomGrowthSlotKind slot, string fieldId)
		{
			int pending; int population;
			if (!KnownOption(operation.OptionState) || !KnownGrowthHealth(operation.HealthState)
				|| operation.OptionTick < 0L || operation.HealthTick < 0L
				|| operation.EffectiveWorkBefore < 0L || operation.EffectiveWorkAfter < 0L
				|| operation.HeartbeatBefore < 0L || operation.HeartbeatAfter < 0L
				|| operation.ArrivalBefore < 0L || operation.ArrivalAfter < 0L
				|| operation.FetchBefore < 0L || operation.FetchAfter < 0L
				|| operation.MillBefore < 0L || operation.MillAfter < 0L
				|| operation.SubsidenceBefore < 0L || operation.SubsidenceAfter < 0L
				|| operation.DeliveryBefore < 0L || operation.DeliveryAfter < 0L
				|| operation.DepartureBefore < 0L || operation.DepartureAfter < 0L
				|| operation.FieldClockBefore < 0L || operation.FieldClockAfter < 0L
				|| !ValidCount(operation.PendingCropBefore)
				|| !CheckedAdd(operation.PendingCropBefore, operation.PendingCropDelta, out pending)
				|| pending != operation.PendingCropAfter || !ValidCount(operation.PendingCropAfter)
				|| !CheckedAdd(operation.PopulationBefore, operation.PopulationDelta, out population)
				|| population != operation.PopulationAfter || !ValidCount(operation.PopulationBefore)
				|| !ValidCount(operation.PopulationAfter)
				|| TooLong(operation.PendingCropBlueprintBefore, MaxNameChars)
				|| TooLong(operation.PendingCropZoneIdBefore, MaxNameChars)
				|| TooLong(operation.PendingCropBlueprintAfter, MaxNameChars)
				|| TooLong(operation.PendingCropZoneIdAfter, MaxNameChars)
				|| (operation.PendingCropBefore == 0
					? operation.PendingCropBlueprintBefore != null
						|| operation.PendingCropZoneIdBefore != null
					: !ValidName(operation.PendingCropBlueprintBefore)
						|| !ValidName(operation.PendingCropZoneIdBefore))
				|| (operation.PendingCropAfter == 0
					? operation.PendingCropBlueprintAfter != null
						|| operation.PendingCropZoneIdAfter != null
					: !ValidName(operation.PendingCropBlueprintAfter)
						|| !ValidName(operation.PendingCropZoneIdAfter))
				|| !GrowthPendingTupleTransitionShape(operation)
				|| !GrowthHarvestOracleShape(operation)
				|| !GrowthHarvestAuthorityShape(book, operation)
				|| !GrowthFieldActionAuthorityShape(operation)
				|| !GrowthVariantScalarsValid(operation)) return false;
			long clockBefore = operation.ClockLease.Before;
			long clockAfter = operation.ClockLease.After;
			KingdomGrowthFieldSlot field = slot == KingdomGrowthSlotKind.Field
				? FindGrowthField(book, fieldId) : null;
			if (!string.Equals(operation.ClockLease.SubjectId,
				GrowthClockSubject(book.SettlementId, slot, fieldId), StringComparison.Ordinal)
				|| !string.Equals(operation.ClockLease.ScopeId, book.SettlementId,
					StringComparison.Ordinal)) return false;
			switch (operation.Action)
			{
			case KingdomGrowthAction.Heartbeat:
				return clockBefore == operation.HeartbeatBefore && clockAfter == operation.HeartbeatAfter
					&& operation.HeartbeatAfter > operation.HeartbeatBefore;
			case KingdomGrowthAction.Arrival:
				return clockBefore == operation.ArrivalBefore && clockAfter == operation.ArrivalAfter
					&& operation.ArrivalAfter > operation.CreatedTick;
			case KingdomGrowthAction.Departure:
				return clockBefore == operation.DepartureBefore
					&& clockAfter == operation.DepartureAfter
					&& operation.DepartureAfter > operation.DepartureBefore;
			case KingdomGrowthAction.Delivery:
				return clockBefore == operation.DeliveryBefore
					&& clockAfter == operation.DeliveryAfter
					&& operation.DeliveryAfter > operation.DeliveryBefore;
			case KingdomGrowthAction.Fetch:
				return clockBefore == operation.FetchBefore && clockAfter == operation.FetchAfter
					&& operation.FetchAfter > operation.FetchBefore;
			case KingdomGrowthAction.Mill:
				return clockBefore == operation.MillBefore && clockAfter == operation.MillAfter
					&& operation.MillAfter > operation.MillBefore;
			default:
				return IsGrowthFieldAction(operation.Action) && field != null
					&& clockBefore < long.MaxValue && clockBefore + 1L == clockAfter
					&& operation.FieldClockAfter >= operation.FieldClockBefore
					&& operation.EffectiveWorkAfter >= operation.EffectiveWorkBefore;
			}
		}

		private static bool GrowthPendingTupleTransitionShape(KingdomGrowthOperation operation)
		{
			if (operation.PendingCropDelta == 0)
				return string.Equals(operation.PendingCropBlueprintBefore,
					operation.PendingCropBlueprintAfter, StringComparison.Ordinal)
					&& string.Equals(operation.PendingCropZoneIdBefore,
						operation.PendingCropZoneIdAfter, StringComparison.Ordinal);
			if (operation.PendingCropBefore > 0 && operation.PendingCropAfter > 0)
				return string.Equals(operation.PendingCropBlueprintBefore,
					operation.PendingCropBlueprintAfter, StringComparison.Ordinal)
					&& string.Equals(operation.PendingCropZoneIdBefore,
						operation.PendingCropZoneIdAfter, StringComparison.Ordinal);
			return true;
		}

		private static bool GrowthHarvestOracleShape(KingdomGrowthOperation operation)
		{
			const int baselineMethodPercent = 100;
			if (operation.Action != KingdomGrowthAction.Harvest)
				return operation.HarvestStandingRows == 0 && operation.HarvestRipeRows == 0
					&& operation.HarvestCycles == 0 && !operation.HarvestCountsRipeLast
					&& operation.HarvestEffectivenessPercent == 0
					&& operation.HarvestMethodPercent == 0
					&& operation.HarvestFirstOrdinal == 0UL
					&& operation.HarvestCropBlueprint == null
					&& operation.HarvestSeedBlueprint == null;
			return operation.HarvestStandingRows > 0
				&& operation.HarvestRipeRows >= 0
				&& operation.HarvestRipeRows <= operation.HarvestStandingRows
				&& operation.HarvestCycles > 0
				&& operation.HarvestEffectivenessPercent > 0
				&& operation.HarvestEffectivenessPercent <= 100
				&& operation.HarvestMethodPercent >= baselineMethodPercent
				&& operation.HarvestMethodPercent <= KingdomResearchRules.MaxMethodPercent
				&& ValidName(operation.HarvestCropBlueprint)
				&& (operation.HarvestSeedBlueprint == null
					|| ValidName(operation.HarvestSeedBlueprint));
		}

		private static bool GrowthHarvestAuthorityShape(KingdomGrowthBook book,
			KingdomGrowthOperation operation)
		{
			if (operation.Action != KingdomGrowthAction.Harvest) return true;
			KingdomGrowthDomainStep registry = FindGrowthDomain(operation,
				KingdomGrowthDomainStepKind.CropRegistry);
			KingdomGrowthDomainStep fieldStep = FindGrowthDomain(operation,
				KingdomGrowthDomainStepKind.Field);
			if (registry == null || fieldStep == null || registry.CropRowsBefore == null
				|| registry.CropRowsDeclaredAfter == null || fieldStep.FieldBefore == null
				|| fieldStep.FieldAfter == null) return false;
			KingdomGrowthFieldState before = fieldStep.FieldBefore;
			KingdomGrowthFieldState after = fieldStep.FieldAfter;
			if (!string.Equals(operation.TargetId, before.WorkObjectId, StringComparison.Ordinal)
				|| !string.Equals(operation.TargetMarker, before.Marker, StringComparison.Ordinal)
				|| !string.Equals(operation.Blueprint, before.Blueprint, StringComparison.Ordinal)
				|| !string.Equals(operation.ZoneId, before.ZoneId, StringComparison.Ordinal)
				|| operation.TargetX != before.X || operation.TargetY != before.Y
				|| !string.Equals(operation.HarvestCropBlueprint, before.CropBlueprint,
					StringComparison.Ordinal)
				|| !string.Equals(operation.HarvestSeedBlueprint, before.SeedBlueprint,
					StringComparison.Ordinal)
				|| operation.HarvestEffectivenessPercent != before.EffectivenessPercent
				|| operation.HarvestMethodPercent != before.MethodPercent
				|| operation.HarvestFirstOrdinal != (ulong)(uint)before.Cycles
				|| after.Cycles - before.Cycles != operation.HarvestCycles) return false;
			int standing = 0; int ripe = 0;
			HashSet<string> mutated = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < registry.CropRowsBefore.Count; i++)
			{
				KingdomGrowthCropRow row = registry.CropRowsBefore[i];
				if (!string.Equals(row.FieldId, operation.FieldId, StringComparison.Ordinal)) continue;
				standing++; if (row.Ripe) ripe++;
				KingdomGrowthCropRow changed = FindGrowthCropRow(registry.CropRowsDeclaredAfter,
					row.RowId);
				KingdomGrowthObjectLeg leg = FindGrowthObjectLeg(operation.Sources, row.ObjectId,
					row.Marker);
				if (changed == null || leg == null || !mutated.Add(row.RowId)
					|| !GrowthHarvestableMutationMatches(row, changed, leg)) return false;
			}
			for (int i = 0; i < registry.CropRowsBefore.Count; i++)
			{
				KingdomGrowthCropRow row = registry.CropRowsBefore[i];
				if (string.Equals(row.FieldId, operation.FieldId, StringComparison.Ordinal)) continue;
				KingdomGrowthCropRow afterRow = FindGrowthCropRow(
					registry.CropRowsDeclaredAfter, row.RowId);
				if (!GrowthCropRowEquals(row, afterRow)) return false;
			}
			int expectedRipe = operation.HarvestCountsRipeLast ? ripe : standing;
			return standing > 0
				&& registry.CropRowsBefore.Count == registry.CropRowsDeclaredAfter.Count
				&& operation.Sources.Count == standing
				&& operation.HarvestStandingRows == standing
				&& operation.HarvestRipeRows == expectedRipe;
		}

		private static bool GrowthFieldActionAuthorityShape(KingdomGrowthOperation operation)
		{
			if (!IsGrowthFieldAction(operation.Action)) return true;
			KingdomGrowthDomainStep field = FindGrowthDomain(operation,
				KingdomGrowthDomainStepKind.Field);
			if (field == null || field.FieldBefore == null || field.FieldAfter == null) return false;
			if (operation.Action == KingdomGrowthAction.Irrigate)
				return GrowthFieldIdentityStable(field.FieldBefore, field.FieldAfter)
					&& GrowthOperationTargetsField(operation, field.FieldBefore);
			KingdomGrowthDomainStep registry = FindGrowthDomain(operation,
				KingdomGrowthDomainStepKind.CropRegistry);
			if (registry == null || registry.CropRowsBefore == null
				|| registry.CropRowsDeclaredAfter == null) return false;
			switch (operation.Action)
			{
			case KingdomGrowthAction.Sow:
				return GrowthSowAuthorityShape(operation, field, registry);
			case KingdomGrowthAction.Withdraw:
				return GrowthWithdrawAuthorityShape(operation, field, registry);
			case KingdomGrowthAction.Ripen:
				return GrowthRipenAuthorityShape(operation, field, registry);
			case KingdomGrowthAction.Harvest:
				return true; // The richer harvest oracle binds every row and callback above.
			default: return false;
			}
		}

		private static bool GrowthSowAuthorityShape(KingdomGrowthOperation operation,
			KingdomGrowthDomainStep field, KingdomGrowthDomainStep registry)
		{
			if (!GrowthFieldStateDormant(field.FieldBefore)
				|| !GrowthOperationTargetsField(operation, field.FieldAfter)
				|| operation.Sources.Count != 1 || operation.Sources[0].BeforeCount != 1
				|| operation.Sources[0].AfterCount != 0
				|| !string.Equals(field.FieldAfter.SeedBlueprint,
					operation.Sources[0].Blueprint, StringComparison.Ordinal)) return false;
			int beforeCount = GrowthRowsForField(registry.CropRowsBefore, operation.FieldId);
			int afterCount = GrowthRowsForField(registry.CropRowsDeclaredAfter, operation.FieldId);
			if (beforeCount != 0 || afterCount <= 0 || afterCount != operation.Outputs.Count
				|| field.FieldAfter.DeclaredRows != afterCount) return false;
			for (int i = 0; i < operation.Outputs.Count; i++)
			{
				KingdomGrowthObjectLeg output = operation.Outputs[i];
				KingdomGrowthCropRow row = FindGrowthCropRowByMarker(
					registry.CropRowsDeclaredAfter, output.Marker);
				if (output.BeforeCount != 0 || output.AfterCount != 1 || row == null
					|| !string.Equals(row.FieldId, operation.FieldId, StringComparison.Ordinal)
					|| row.ObjectId != null || row.Count != 1
					|| !string.Equals(row.Blueprint, output.Blueprint, StringComparison.Ordinal)
					|| !string.Equals(row.Blueprint, field.FieldAfter.CropBlueprint,
						StringComparison.Ordinal)) return false;
			}
			return GrowthNonTargetCropRowsStable(registry, operation.FieldId);
		}

		private static bool GrowthWithdrawAuthorityShape(KingdomGrowthOperation operation,
			KingdomGrowthDomainStep field, KingdomGrowthDomainStep registry)
		{
			if (!GrowthOperationTargetsField(operation, field.FieldBefore)
				|| !GrowthFieldStateDormant(field.FieldAfter)
				|| GrowthRowsForField(registry.CropRowsDeclaredAfter, operation.FieldId) != 0
				|| operation.Sources.Count != GrowthRowsForField(registry.CropRowsBefore,
					operation.FieldId) || operation.Outputs.Count > 1) return false;
			for (int i = 0; i < operation.Sources.Count; i++)
			{
				KingdomGrowthObjectLeg source = operation.Sources[i];
				KingdomGrowthCropRow row = FindGrowthCropRowByObject(registry.CropRowsBefore,
					source.ObjectId, source.Marker);
				if (row == null || source.MutationKind != KingdomGrowthObjectMutationKind.Obliterate
					|| source.BeforeCount != row.Count || source.AfterCount != 0) return false;
			}
			return (operation.Outputs.Count == 0 || operation.Outputs[0].BeforeCount == 0
				&& operation.Outputs[0].AfterCount == 1
				&& string.Equals(operation.Outputs[0].Blueprint, field.FieldBefore.SeedBlueprint,
					StringComparison.Ordinal))
				&& GrowthNonTargetCropRowsStable(registry, operation.FieldId);
		}

		private static bool GrowthRipenAuthorityShape(KingdomGrowthOperation operation,
			KingdomGrowthDomainStep field, KingdomGrowthDomainStep registry)
		{
			if (!GrowthOperationTargetsField(operation, field.FieldBefore)
				|| !GrowthFieldIdentityStable(field.FieldBefore, field.FieldAfter)
				|| operation.Sources.Count != GrowthRowsForField(registry.CropRowsBefore,
					operation.FieldId)) return false;
			for (int i = 0; i < registry.CropRowsBefore.Count; i++)
			{
				KingdomGrowthCropRow before = registry.CropRowsBefore[i];
				KingdomGrowthCropRow after = FindGrowthCropRow(registry.CropRowsDeclaredAfter,
					before.RowId);
				if (!string.Equals(before.FieldId, operation.FieldId, StringComparison.Ordinal))
				{
					if (!GrowthCropRowEquals(before, after)) return false;
					continue;
				}
				KingdomGrowthObjectLeg source = FindGrowthObjectLeg(operation.Sources,
					before.ObjectId, before.Marker);
				if (after == null || source == null || before.Ripe || !after.Ripe
					|| !GrowthHarvestableMutationMatches(before, after, source)) return false;
			}
			return registry.CropRowsBefore.Count == registry.CropRowsDeclaredAfter.Count;
		}

		private static bool GrowthOperationTargetsField(KingdomGrowthOperation operation,
			KingdomGrowthFieldState field)
		{
			return field != null
				&& string.Equals(operation.TargetId, field.WorkObjectId, StringComparison.Ordinal)
				&& string.Equals(operation.TargetMarker, field.Marker, StringComparison.Ordinal)
				&& string.Equals(operation.Blueprint, field.Blueprint, StringComparison.Ordinal)
				&& string.Equals(operation.ZoneId, field.ZoneId, StringComparison.Ordinal)
				&& operation.TargetX == field.X && operation.TargetY == field.Y;
		}

		private static bool GrowthFieldIdentityStable(KingdomGrowthFieldState before,
			KingdomGrowthFieldState after)
		{
			return before != null && after != null
				&& string.Equals(before.FieldId, after.FieldId, StringComparison.Ordinal)
				&& string.Equals(before.WorkObjectId, after.WorkObjectId, StringComparison.Ordinal)
				&& string.Equals(before.WorkPartId, after.WorkPartId, StringComparison.Ordinal)
				&& string.Equals(before.Marker, after.Marker, StringComparison.Ordinal)
				&& string.Equals(before.Blueprint, after.Blueprint, StringComparison.Ordinal)
				&& string.Equals(before.ZoneId, after.ZoneId, StringComparison.Ordinal)
				&& before.X == after.X && before.Y == after.Y;
		}

		private static bool GrowthFieldStateDormant(KingdomGrowthFieldState state)
		{
			return state != null && state.WorkObjectId == null && state.WorkPartId == null
				&& state.Marker == null && state.Blueprint == null && state.ZoneId == null
				&& state.X == -1 && state.Y == -1 && state.CropBlueprint == null
				&& state.Stage == 0 && state.NextStageTick == 0L && state.SownTick == 0L
				&& state.Cycles == 0 && state.SaidWant == 0 && state.DeclaredRows == 0
				&& state.EffectivenessPercent == 0 && state.MethodPercent == 0
				&& !state.NoLarderAnnounced && state.SeedBlueprint == null
				&& state.PartGraphHash == null && state.ObjectGraphHash == null
				&& state.TopologyHash == null;
		}

		private static int GrowthRowsForField(List<KingdomGrowthCropRow> rows, string fieldId)
		{
			int count = 0;
			for (int i = 0; i < rows.Count; i++)
				if (string.Equals(rows[i].FieldId, fieldId, StringComparison.Ordinal)) count++;
			return count;
		}

		private static KingdomGrowthCropRow FindGrowthCropRowByMarker(
			List<KingdomGrowthCropRow> rows, string marker)
		{
			KingdomGrowthCropRow found = null;
			for (int i = 0; i < rows.Count; i++)
				if (string.Equals(rows[i].Marker, marker, StringComparison.Ordinal))
				{
					if (found != null) return null; found = rows[i];
				}
			return found;
		}

		private static KingdomGrowthCropRow FindGrowthCropRowByObject(
			List<KingdomGrowthCropRow> rows, string objectId, string marker)
		{
			for (int i = 0; i < rows.Count; i++)
				if (string.Equals(rows[i].ObjectId, objectId, StringComparison.Ordinal)
					&& string.Equals(rows[i].Marker, marker, StringComparison.Ordinal)) return rows[i];
			return null;
		}

		private static bool GrowthNonTargetCropRowsStable(KingdomGrowthDomainStep registry,
			string fieldId)
		{
			int beforeTarget = GrowthRowsForField(registry.CropRowsBefore, fieldId);
			int afterTarget = GrowthRowsForField(registry.CropRowsDeclaredAfter, fieldId);
			if (registry.CropRowsDeclaredAfter.Count
				!= registry.CropRowsBefore.Count - beforeTarget + afterTarget) return false;
			for (int i = 0; i < registry.CropRowsBefore.Count; i++)
			{
				KingdomGrowthCropRow before = registry.CropRowsBefore[i];
				if (string.Equals(before.FieldId, fieldId, StringComparison.Ordinal)) continue;
				if (!GrowthCropRowEquals(before, FindGrowthCropRow(
					registry.CropRowsDeclaredAfter, before.RowId))) return false;
			}
			return true;
		}

		private static KingdomGrowthCropRow FindGrowthCropRow(
			List<KingdomGrowthCropRow> rows, string rowId)
		{
			KingdomGrowthCropRow found = null;
			if (rows == null) return null;
			for (int i = 0; i < rows.Count; i++)
				if (rows[i] != null && string.Equals(rows[i].RowId, rowId,
					StringComparison.Ordinal))
				{
					if (found != null) return null;
					found = rows[i];
				}
			return found;
		}

		private static KingdomGrowthObjectLeg FindGrowthObjectLeg(
			List<KingdomGrowthObjectLeg> legs, string objectId, string marker)
		{
			KingdomGrowthObjectLeg found = null;
			for (int i = 0; i < legs.Count; i++)
				if (legs[i] != null && string.Equals(legs[i].ObjectId, objectId,
					StringComparison.Ordinal) && string.Equals(legs[i].Marker, marker,
					StringComparison.Ordinal))
				{
					if (found != null) return null;
					found = legs[i];
				}
			return found;
		}

		private static bool GrowthHarvestableMutationMatches(KingdomGrowthCropRow before,
			KingdomGrowthCropRow after, KingdomGrowthObjectLeg leg)
		{
			if (leg.MutationKind != KingdomGrowthObjectMutationKind.HarvestableRipeSet
				|| leg.Callbacks == null || leg.Callbacks.Count != 1
				|| leg.BeforeCount != before.Count || leg.AfterCount != after.Count
				|| !string.Equals(leg.Blueprint, before.Blueprint, StringComparison.Ordinal)
				|| !string.Equals(leg.ZoneId, before.ZoneId, StringComparison.Ordinal)
				|| leg.X != before.X || leg.Y != before.Y) return false;
			KingdomGrowthObjectCallbackStep step = leg.Callbacks[0];
			return step.Kind == KingdomGrowthObjectMutationKind.HarvestableRipeSet
				&& step.BeforeHasHarvestable == before.HasHarvestable
				&& step.AfterHasHarvestable == after.HasHarvestable
				&& step.BeforeRipe == before.Ripe && step.AfterRipe == after.Ripe
				&& step.BeforeRegenTimer == before.RegenTimer
				&& step.AfterRegenTimer == after.RegenTimer
				&& string.Equals(step.BeforeRegenTime, before.RegenTime,
					StringComparison.Ordinal)
				&& string.Equals(step.AfterRegenTime, after.RegenTime,
					StringComparison.Ordinal)
				&& step.BeforeTileIndex == before.TileIndex && step.AfterTileIndex == after.TileIndex
				&& string.Equals(step.BeforeRenderTile, before.RenderTile, StringComparison.Ordinal)
				&& string.Equals(step.AfterRenderTile, after.RenderTile, StringComparison.Ordinal)
				&& string.Equals(step.BeforeRenderColor, before.RenderColor,
					StringComparison.Ordinal)
				&& string.Equals(step.AfterRenderColor, after.RenderColor,
					StringComparison.Ordinal)
				&& string.Equals(step.BeforeRenderDetail, before.RenderDetail,
					StringComparison.Ordinal)
				&& string.Equals(step.AfterRenderDetail, after.RenderDetail,
					StringComparison.Ordinal)
				&& string.Equals(step.BeforeRenderString, before.RenderString,
					StringComparison.Ordinal)
				&& string.Equals(step.AfterRenderString, after.RenderString,
					StringComparison.Ordinal)
				&& string.Equals(step.BeforeTileColor, before.TileColor,
					StringComparison.Ordinal)
				&& string.Equals(step.AfterTileColor, after.TileColor,
					StringComparison.Ordinal)
				&& string.Equals(step.BeforeObjectGraphHash, before.ObjectGraphHash,
					StringComparison.Ordinal)
				&& string.Equals(step.AfterObjectGraphHash, after.ObjectGraphHash,
					StringComparison.Ordinal)
				&& string.Equals(step.BeforeTopologyHash, before.TopologyHash,
					StringComparison.Ordinal)
				&& string.Equals(step.AfterTopologyHash, after.TopologyHash,
					StringComparison.Ordinal);
		}

		private static bool GrowthVariantScalarsValid(KingdomGrowthOperation operation)
		{
			if (!Enum.IsDefined(typeof(KingdomGrowthArrivalDisposition),
				operation.ArrivalDisposition)
				|| !Enum.IsDefined(typeof(KingdomGrowthDeliveryMode), operation.DeliveryMode)
				|| !Enum.IsDefined(typeof(KingdomGrowthDepartureCauseKind),
					operation.DepartureCauseKind)
				|| !KnownOption(operation.ScarcityOptionState)
				|| operation.ScarcityOptionTick < 0L
				|| TooLong(operation.DepartureCause, MaxNameChars)
				|| TooLong(operation.DepartureNote, MaxTextChars)
				|| TooLong(operation.DepartureName, MaxNameChars)
				|| TooLong(operation.DepartureOrigin, MaxNameChars)
				|| TooLong(operation.DepartureCreed, MaxNameChars)
				|| TooLong(operation.MillCropBlueprint, MaxNameChars)
				|| TooLong(operation.MillStapleBlueprint, MaxNameChars)
				|| (operation.TriggeredByOperationId != null
					&& !ValidGeneratedId(operation.TriggeredByOperationId))) return false;
			if (operation.Action == KingdomGrowthAction.Arrival)
			{
				if (operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.None) return false;
			}
			else if (operation.ArrivalDisposition != KingdomGrowthArrivalDisposition.None
				|| operation.ArrivalCandidateId != null) return false;
			if (operation.Action == KingdomGrowthAction.Delivery)
			{
				if (operation.DeliveryMode != KingdomGrowthDeliveryMode.PlainLarder) return false;
			}
			else if (operation.DeliveryMode != KingdomGrowthDeliveryMode.None) return false;
			if (operation.Action == KingdomGrowthAction.Mill)
			{
				if (!ValidName(operation.MillCropBlueprint)
					|| !ValidName(operation.MillStapleBlueprint)) return false;
			}
			else if (operation.MillCropBlueprint != null
				|| operation.MillStapleBlueprint != null) return false;
			bool departure = operation.Action == KingdomGrowthAction.Departure
				|| operation.Action == KingdomGrowthAction.Heartbeat
					&& operation.PopulationDelta < 0;
			if (departure)
			{
				if (operation.DepartureCauseKind == KingdomGrowthDepartureCauseKind.None
					|| !GrowthBoundedPresentString(operation.DepartureCause)
					|| !GrowthBoundedPresentString(operation.DepartureName)
					|| !GrowthBoundedPresentString(operation.DepartureOrigin)
					|| operation.DepartureArrivedTick < 0L
					|| !GrowthBoundedPresentString(operation.DepartureCreed)) return false;
			}
			else if (operation.DepartureCauseKind != KingdomGrowthDepartureCauseKind.None
				|| operation.DepartureCause != null || operation.DepartureNote != null
				|| operation.DepartureName != null || operation.DepartureOrigin != null
				|| operation.DepartureArrivedTick != 0L || operation.DepartureCreed != null
				|| operation.DepartureChronicled || operation.TriggeredByOperationId != null)
				return false;
			return true;
		}

		private static bool GrowthTargetShape(KingdomGrowthOperation operation,
			KingdomGrowthSlotKind slot)
		{
			bool empty = operation.TargetId == null
				&& operation.TargetMarker == null
				&& operation.Blueprint == null && operation.ZoneId == null
				&& operation.TargetTopology == KingdomLifecycleTopology.None
				&& operation.TargetLocation == KingdomGrowthLocationKind.None
				&& operation.TargetOwnerId == null
				&& operation.TargetX == -1 && operation.TargetY == -1;
			if (empty) return slot != KingdomGrowthSlotKind.Field
				&& operation.Action != KingdomGrowthAction.Departure
				&& !(operation.Action == KingdomGrowthAction.Heartbeat
					&& operation.PopulationDelta < 0);
			return ValidRootId(operation.TargetId) && ValidRootId(operation.TargetMarker)
				&& ValidName(operation.Blueprint) && GrowthTopologyValid(operation.TargetTopology,
					operation.TargetOwnerId, operation.ZoneId, operation.TargetX, operation.TargetY)
				&& operation.TargetLocation == GrowthLocationFromTopology(operation.TargetTopology);
		}

		private static bool GrowthGroupsMatchAction(KingdomGrowthOperation operation)
		{
			bool groups;
			switch (operation.Action)
			{
			case KingdomGrowthAction.Heartbeat:
				groups = operation.Outputs.Count == 0
					&& GrowthAllWaterKinds(operation, KingdomGrowthWaterMutationKind.Drain)
					&& GrowthHeartbeatSourcesShape(operation)
					&& (operation.ScarcityOptionState == KingdomLifecycleOptionState.Enabled
						|| operation.ScarcityOptionState == KingdomLifecycleOptionState.Disabled
							&& operation.WaterLegs.Count == 0 && operation.Sources.Count == 0
							&& operation.PopulationDelta == 0);
				break;
			case KingdomGrowthAction.Fetch:
				groups = operation.WaterLegs.Count >= 2 && operation.Sources.Count == 0
					&& operation.Outputs.Count == 0 && GrowthFetchWaterShape(operation);
				break;
			case KingdomGrowthAction.Mill:
				groups = operation.WaterLegs.Count == 0 && operation.Sources.Count > 0
					&& operation.Outputs.Count > 0
					&& GrowthAllObjectKinds(operation.Sources,
						KingdomGrowthObjectMutationKind.DestroyOne)
					&& GrowthAllObjectKinds(operation.Outputs,
						KingdomGrowthObjectMutationKind.Create)
					&& GrowthAllObjectBlueprints(operation.Sources,
						operation.MillCropBlueprint)
					&& GrowthAllObjectBlueprints(operation.Outputs,
						operation.MillStapleBlueprint);
				break;
			case KingdomGrowthAction.Arrival:
				if (operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.Joined)
					groups = operation.WaterLegs.Count > 0 && operation.Sources.Count == 0
						&& operation.Outputs.Count == 0
						&& ValidGeneratedId(operation.ArrivalCandidateId)
						&& GrowthAllWaterKinds(operation, KingdomGrowthWaterMutationKind.Drain);
				else groups = operation.ArrivalDisposition != KingdomGrowthArrivalDisposition.None
					&& operation.WaterLegs.Count == 0 && operation.Sources.Count == 0
					&& operation.Outputs.Count == 0
					&& (operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.NoGround
						|| operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.WaterUnavailable
						|| operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.PopulationCap
						|| operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.SupportCap
							? operation.ArrivalCandidateId == null
							: ValidGeneratedId(operation.ArrivalCandidateId));
				break;
			case KingdomGrowthAction.Departure:
				groups = operation.WaterLegs.Count == 0 && operation.Sources.Count == 1
					&& operation.Outputs.Count == 0
					&& operation.Sources[0].MutationKind ==
						KingdomGrowthObjectMutationKind.Obliterate
					&& operation.Sources[0].BeforeCount == 1
					&& operation.Sources[0].AfterCount == 0
					&& string.Equals(operation.Sources[0].ObjectId, operation.TargetId,
						StringComparison.Ordinal)
					&& string.Equals(operation.Sources[0].Marker, operation.TargetMarker,
						StringComparison.Ordinal)
					&& string.Equals(operation.Sources[0].Blueprint, operation.Blueprint,
						StringComparison.Ordinal)
					&& string.Equals(operation.Sources[0].ZoneId, operation.ZoneId,
						StringComparison.Ordinal)
					&& operation.Sources[0].X == operation.TargetX
					&& operation.Sources[0].Y == operation.TargetY;
				break;
			case KingdomGrowthAction.Delivery:
				groups = operation.WaterLegs.Count == 0 && operation.Sources.Count == 0
					&& operation.Outputs.Count > 0
					&& GrowthAllObjectKinds(operation.Outputs,
						KingdomGrowthObjectMutationKind.Create)
					&& GrowthDeliveryOutputsShape(operation)
					&& operation.DeliveryMode == KingdomGrowthDeliveryMode.PlainLarder;
				break;
			case KingdomGrowthAction.Sow:
				groups = operation.WaterLegs.Count > 0 && operation.Sources.Count > 0
					&& operation.Outputs.Count > 0
					&& GrowthAllWaterKinds(operation, KingdomGrowthWaterMutationKind.Drain)
					&& operation.Sources.Count == 1
					&& operation.Sources[0].MutationKind ==
						KingdomGrowthObjectMutationKind.DestroyOne
					&& GrowthAllObjectKinds(operation.Outputs,
						KingdomGrowthObjectMutationKind.Create);
				break;
			case KingdomGrowthAction.Withdraw:
				groups = operation.WaterLegs.Count == 0 && operation.Sources.Count > 0
					&& operation.Outputs.Count <= 1
					&& GrowthAllObjectKinds(operation.Sources,
						KingdomGrowthObjectMutationKind.Obliterate)
					&& (operation.Outputs.Count == 0 || operation.Outputs[0].MutationKind ==
						KingdomGrowthObjectMutationKind.Create);
				break;
			case KingdomGrowthAction.Ripen:
				groups = operation.WaterLegs.Count == 0 && operation.Sources.Count > 0
					&& operation.Outputs.Count == 0
					&& GrowthAllObjectKinds(operation.Sources,
						KingdomGrowthObjectMutationKind.HarvestableRipeSet);
				break;
			case KingdomGrowthAction.Harvest:
				groups = operation.WaterLegs.Count == 0 && operation.Sources.Count > 0
					&& GrowthAllObjectKinds(operation.Sources,
						KingdomGrowthObjectMutationKind.HarvestableRipeSet)
					&& GrowthAllObjectKinds(operation.Outputs,
						KingdomGrowthObjectMutationKind.Create);
				break;
			case KingdomGrowthAction.Irrigate:
				groups = operation.WaterLegs.Count == 0 && operation.Sources.Count == 0
					&& operation.Outputs.Count == 0;
				break;
			default: return false;
			}
			return groups && GrowthDomainSetMatchesAction(operation)
				&& GrowthActionConservationShape(operation);
		}

		private static bool GrowthArrivalCandidateBindingShape(KingdomGrowthBook book,
			KingdomGrowthOperation operation, bool publication)
		{
			if (operation.Action != KingdomGrowthAction.Arrival)
				return operation.ArrivalCandidateId == null;
			bool needsCandidate = operation.ArrivalDisposition ==
				KingdomGrowthArrivalDisposition.Joined
				|| operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.NoAcceptableHome;
			if (!needsCandidate)
				return operation.ArrivalCandidateId == null && book.ArrivalCandidate == null;
			KingdomGrowthArrivalCandidate candidate = book.ArrivalCandidate;
			if (candidate == null || !ReferenceEquals(book.ArrivalCandidate, candidate)
				|| !string.Equals(candidate.Id, operation.ArrivalCandidateId,
					StringComparison.Ordinal)
				|| candidate.Disposition != operation.ArrivalDisposition) return false;
			if (candidate.Disposition == KingdomGrowthArrivalDisposition.Joined)
			{
				if (!string.Equals(operation.TargetId, candidate.ObjectId, StringComparison.Ordinal)
					|| !string.Equals(operation.TargetMarker, candidate.Marker,
						StringComparison.Ordinal)
					|| !string.Equals(operation.Blueprint, candidate.Blueprint,
						StringComparison.Ordinal)
					|| operation.TargetTopology != KingdomLifecycleTopology.Cell
					|| operation.TargetLocation != KingdomGrowthLocationKind.Cell
					|| operation.TargetOwnerId != null
					|| !string.Equals(operation.ZoneId, candidate.LodgingZoneId,
						StringComparison.Ordinal)
					|| operation.TargetX != candidate.LodgingX
					|| operation.TargetY != candidate.LodgingY) return false;
			}
			if (publication)
				return candidate.Phase == KingdomGrowthArrivalCandidatePhase.Observed
					&& candidate.ConsumingOperationId == null
					&& candidate.ConsumingOperationSequence == 0L;
			if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.Quarantined
				&& operation.Phase != KingdomGrowthPhase.Quarantined) return false;
			KingdomGrowthArrivalCandidatePhase effectivePhase = candidate.Phase ==
				KingdomGrowthArrivalCandidatePhase.Quarantined
					? candidate.EvidencePhase : candidate.Phase;
			if (effectivePhase == KingdomGrowthArrivalCandidatePhase.Observed)
				return candidate.ConsumingOperationId == null
					&& candidate.ConsumingOperationSequence == 0L
					&& (operation.Phase == KingdomGrowthPhase.Prepared
						|| operation.Phase == KingdomGrowthPhase.WaterIntent
						|| operation.Phase == KingdomGrowthPhase.WaterSettled
						|| operation.Phase == KingdomGrowthPhase.Quarantined);
			bool rightIntent = candidate.Disposition == KingdomGrowthArrivalDisposition.Joined
				? effectivePhase == KingdomGrowthArrivalCandidatePhase.ConsumeIntent
				: effectivePhase == KingdomGrowthArrivalCandidatePhase.RefusalIntent;
			return (rightIntent || effectivePhase == KingdomGrowthArrivalCandidatePhase.Settled)
				&& string.Equals(candidate.ConsumingOperationId, operation.Id,
					StringComparison.Ordinal)
				&& candidate.ConsumingOperationSequence == operation.Sequence;
		}

		private static bool GrowthHeartbeatSourcesShape(KingdomGrowthOperation operation)
		{
			if (operation.PopulationDelta != 0 && operation.PopulationDelta != -1) return false;
			int leavers = 0;
			for (int i = 0; i < operation.Sources.Count; i++)
			{
				KingdomGrowthObjectLeg leg = operation.Sources[i];
				if (operation.PopulationDelta < 0
					&& string.Equals(leg.ObjectId, operation.TargetId, StringComparison.Ordinal))
				{
					if (leg.MutationKind != KingdomGrowthObjectMutationKind.Obliterate
						|| leg.BeforeCount != 1 || leg.AfterCount != 0
						|| !string.Equals(leg.Marker, operation.TargetMarker,
							StringComparison.Ordinal)
						|| !string.Equals(leg.Blueprint, operation.Blueprint,
							StringComparison.Ordinal)
						|| !string.Equals(leg.ZoneId, operation.ZoneId,
							StringComparison.Ordinal)
						|| leg.X != operation.TargetX || leg.Y != operation.TargetY
						|| ++leavers != 1) return false;
				}
				else if (leg.MutationKind != KingdomGrowthObjectMutationKind.DestroyOne) return false;
			}
			return leavers == (operation.PopulationDelta < 0 ? 1 : 0);
		}

		private static bool GrowthDeliveryOutputsShape(KingdomGrowthOperation operation)
		{
			if (operation.PendingCropBefore <= 0 || operation.PendingCropDelta >= 0
				|| !ValidName(operation.PendingCropBlueprintBefore)
				|| !ValidName(operation.PendingCropZoneIdBefore)) return false;
			for (int i = 0; i < operation.Outputs.Count; i++)
			{
				KingdomGrowthObjectLeg output = operation.Outputs[i];
				if (!string.Equals(output.Blueprint, operation.PendingCropBlueprintBefore,
					StringComparison.Ordinal)
					|| !string.Equals(output.ZoneId, operation.PendingCropZoneIdBefore,
						StringComparison.Ordinal)) return false;
			}
			return true;
		}

		private static bool GrowthActionConservationShape(KingdomGrowthOperation operation)
		{
			int water;
			int removed;
			int added;
			switch (operation.Action)
			{
			case KingdomGrowthAction.Heartbeat:
				KingdomGrowthDomainStep scarcity = FindGrowthDomain(operation,
					KingdomGrowthDomainStepKind.Scarcity);
				return scarcity != null && scarcity.ScarcityAfter != null
					&& GrowthWaterQuantity(operation, KingdomGrowthWaterMutationKind.Drain,
						out water) && water == scarcity.ScarcityAfter.ProvedWater
					&& GrowthRemovedObjectQuantity(operation, true, out removed)
					&& removed == scarcity.ScarcityAfter.Eaten;
			case KingdomGrowthAction.Fetch:
				return GrowthFetchWaterShape(operation);
			case KingdomGrowthAction.Mill:
				if (!GrowthRemovedObjectQuantity(operation, false, out removed)
					|| !GrowthAddedObjectQuantity(operation, null, out added)) return false;
				return removed > 0 && added > 0
					&& (long)removed * KingdomRules.PreserveMultiple >= added;
			case KingdomGrowthAction.Arrival:
				if (operation.ArrivalDisposition != KingdomGrowthArrivalDisposition.Joined)
					return operation.PopulationDelta == 0;
				return operation.PopulationDelta == 1
					&& GrowthWaterQuantity(operation, KingdomGrowthWaterMutationKind.Drain,
						out water) && water == KingdomRules.DramsPerArrival;
			case KingdomGrowthAction.Departure:
				return operation.PopulationDelta == -1;
			case KingdomGrowthAction.Delivery:
				return operation.PendingCropDelta < 0
					&& GrowthAddedObjectQuantity(operation, null, out added)
					&& added == -operation.PendingCropDelta;
			case KingdomGrowthAction.Sow:
				return GrowthWaterQuantity(operation, KingdomGrowthWaterMutationKind.Drain,
						out water) && water == KingdomCropRules.PlantWaterCostDrams
					&& GrowthRemovedObjectQuantity(operation, false, out removed) && removed == 1
					&& GrowthAddedObjectQuantity(operation, null, out added) && added > 0;
			case KingdomGrowthAction.Withdraw:
				return GrowthRemovedObjectQuantity(operation, false, out removed) && removed > 0
					&& GrowthAddedObjectQuantity(operation, null, out added) && added <= 1;
			case KingdomGrowthAction.Ripen:
				return operation.PendingCropDelta == 0;
			case KingdomGrowthAction.Harvest:
				return GrowthHarvestConservationShape(operation);
			case KingdomGrowthAction.Irrigate:
				return operation.PendingCropDelta == 0;
			default: return false;
			}
		}

		private static bool GrowthHarvestConservationShape(KingdomGrowthOperation operation)
		{
			int crop = 0;
			int seed = 0;
			if (operation.PendingCropDelta < 0) return false;
			for (int i = 0; i < operation.Outputs.Count; i++)
			{
				KingdomGrowthObjectLeg leg = operation.Outputs[i];
				int quantity = leg.AfterCount - leg.BeforeCount;
				if (quantity < 0) return false;
				if (string.Equals(leg.Blueprint, operation.HarvestCropBlueprint,
					StringComparison.Ordinal))
				{
					if (!CheckedAdd(crop, quantity, out crop)) return false;
				}
				else if (operation.HarvestSeedBlueprint != null
					&& string.Equals(leg.Blueprint, operation.HarvestSeedBlueprint,
						StringComparison.Ordinal))
				{
					if (!CheckedAdd(seed, quantity, out seed)) return false;
				}
				else return false;
			}
			int yield = GrowthHarvestExpectedYield(operation);
			if (yield <= 0 || crop > yield || operation.PendingCropDelta > yield - crop) return false;
			int expectedSeeds = operation.HarvestSeedBlueprint == null ? 0
				: KingdomCropRules.SeedReturned(operation.SettlementId, operation.TargetId,
					operation.HarvestFirstOrdinal, operation.HarvestCycles, yield);
			return seed == expectedSeeds;
		}

		private static int GrowthHarvestExpectedYield(KingdomGrowthOperation operation)
		{
			if (!GrowthHarvestOracleShape(operation)) return -1;
			return KingdomCropRules.GatheredYield(operation.HarvestStandingRows,
				operation.HarvestRipeRows, operation.HarvestCycles,
				operation.HarvestCountsRipeLast, operation.HarvestEffectivenessPercent,
				operation.HarvestMethodPercent);
		}

		private static KingdomGrowthDomainStep FindGrowthDomain(KingdomGrowthOperation operation,
			KingdomGrowthDomainStepKind kind)
		{
			KingdomGrowthDomainStep found = null;
			if (operation == null || operation.DomainSteps == null) return null;
			for (int i = 0; i < operation.DomainSteps.Count; i++)
				if (operation.DomainSteps[i] != null && operation.DomainSteps[i].Kind == kind)
				{
					if (found != null) return null;
					found = operation.DomainSteps[i];
				}
			return found;
		}

		private static bool GrowthWaterQuantity(KingdomGrowthOperation operation,
			KingdomGrowthWaterMutationKind kind, out int quantity)
		{
			long total = 0L;
			quantity = 0;
			if (operation == null || operation.WaterLegs == null) return false;
			for (int i = 0; i < operation.WaterLegs.Count; i++)
				if (operation.WaterLegs[i] != null && operation.WaterLegs[i].MutationKind == kind)
				{
					total += operation.WaterLegs[i].Delta;
					if (total > int.MaxValue) return false;
				}
			quantity = (int)total;
			return true;
		}

		private static bool GrowthRemovedObjectQuantity(KingdomGrowthOperation operation,
			bool excludeTarget, out int quantity)
		{
			long total = 0L;
			quantity = 0;
			if (operation == null || operation.Sources == null) return false;
			for (int i = 0; i < operation.Sources.Count; i++)
			{
				KingdomGrowthObjectLeg leg = operation.Sources[i];
				if (leg == null || excludeTarget && string.Equals(leg.ObjectId,
					operation.TargetId, StringComparison.Ordinal)) continue;
				int removed = leg.BeforeCount - leg.AfterCount;
				if (removed < 0) return false;
				total += removed;
				if (total > int.MaxValue) return false;
			}
			quantity = (int)total;
			return true;
		}

		private static bool GrowthAddedObjectQuantity(KingdomGrowthOperation operation,
			string blueprint, out int quantity)
		{
			long total = 0L;
			quantity = 0;
			if (operation == null || operation.Outputs == null) return false;
			for (int i = 0; i < operation.Outputs.Count; i++)
			{
				KingdomGrowthObjectLeg leg = operation.Outputs[i];
				if (leg == null || blueprint != null && !string.Equals(leg.Blueprint, blueprint,
					StringComparison.Ordinal)) continue;
				int added = leg.AfterCount - leg.BeforeCount;
				if (added < 0) return false;
				total += added;
				if (total > int.MaxValue) return false;
			}
			quantity = (int)total;
			return true;
		}

		private static bool GrowthFetchWaterShape(KingdomGrowthOperation operation)
		{
			long drained = 0L; long filled = 0L; bool fillSeen = false;
			for (int i = 0; i < operation.WaterLegs.Count; i++)
			{
				KingdomGrowthWaterLeg leg = operation.WaterLegs[i];
				if (leg == null || leg.Delta <= 0) return false;
				if (leg.MutationKind == KingdomGrowthWaterMutationKind.Drain)
				{
					if (fillSeen || !CheckedAdd(drained, leg.Delta, out drained)) return false;
				}
				else if (leg.MutationKind == KingdomGrowthWaterMutationKind.Fill)
				{
					fillSeen = true;
					if (!CheckedAdd(filled, leg.Delta, out filled)) return false;
				}
				else return false;
			}
			return fillSeen && drained > 0L && drained == filled;
		}

		private static bool GrowthAllWaterKinds(KingdomGrowthOperation operation,
			KingdomGrowthWaterMutationKind kind)
		{
			for (int i = 0; i < operation.WaterLegs.Count; i++)
				if (operation.WaterLegs[i] == null || operation.WaterLegs[i].MutationKind != kind)
					return false;
			return true;
		}

		private static bool GrowthAllObjectKinds(List<KingdomGrowthObjectLeg> legs,
			KingdomGrowthObjectMutationKind kind)
		{
			for (int i = 0; i < legs.Count; i++)
				if (legs[i] == null || legs[i].MutationKind != kind) return false;
			return true;
		}

		private static bool GrowthAllObjectBlueprints(List<KingdomGrowthObjectLeg> legs,
			string blueprint)
		{
			if (!ValidName(blueprint)) return false;
			for (int i = 0; i < legs.Count; i++)
				if (legs[i] == null || !string.Equals(legs[i].Blueprint, blueprint,
					StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool GrowthDomainSetMatchesAction(KingdomGrowthOperation operation)
		{
			KingdomGrowthDomainStepKind[] expected;
			switch (operation.Action)
			{
			case KingdomGrowthAction.Heartbeat:
				expected = operation.PopulationDelta < 0
					? new[] { KingdomGrowthDomainStepKind.Scarcity,
						KingdomGrowthDomainStepKind.Roster, KingdomGrowthDomainStepKind.Creed,
						KingdomGrowthDomainStepKind.Population,
						KingdomGrowthDomainStepKind.Accounting }
					: new[] { KingdomGrowthDomainStepKind.Scarcity,
						KingdomGrowthDomainStepKind.Accounting };
				break;
			case KingdomGrowthAction.Fetch:
			case KingdomGrowthAction.Mill:
				expected = new[] { KingdomGrowthDomainStepKind.Accounting }; break;
			case KingdomGrowthAction.Arrival:
				expected = operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.Joined
					? new[] { KingdomGrowthDomainStepKind.Enrollment,
						KingdomGrowthDomainStepKind.Roster, KingdomGrowthDomainStepKind.Creed,
						KingdomGrowthDomainStepKind.Population,
						KingdomGrowthDomainStepKind.Accounting }
					: new KingdomGrowthDomainStepKind[0];
				break;
			case KingdomGrowthAction.Departure:
				expected = operation.DepartureCauseKind == KingdomGrowthDepartureCauseKind.Subsidence
					? new[] { KingdomGrowthDomainStepKind.Roster, KingdomGrowthDomainStepKind.Creed,
						KingdomGrowthDomainStepKind.Population,
						KingdomGrowthDomainStepKind.SubsidenceSchedule,
						KingdomGrowthDomainStepKind.Accounting }
					: new[] { KingdomGrowthDomainStepKind.Roster,
						KingdomGrowthDomainStepKind.Creed, KingdomGrowthDomainStepKind.Population,
						KingdomGrowthDomainStepKind.Accounting };
				break;
			case KingdomGrowthAction.Delivery:
				expected = new[] { KingdomGrowthDomainStepKind.PendingCrop,
					KingdomGrowthDomainStepKind.Accounting };
				break;
			case KingdomGrowthAction.Sow:
			case KingdomGrowthAction.Withdraw:
			case KingdomGrowthAction.Ripen:
				expected = new[] { KingdomGrowthDomainStepKind.CropRegistry,
					KingdomGrowthDomainStepKind.Field }; break;
			case KingdomGrowthAction.Harvest:
				expected = operation.PendingCropDelta == 0
					? new[] { KingdomGrowthDomainStepKind.CropRegistry,
						KingdomGrowthDomainStepKind.Field,
						KingdomGrowthDomainStepKind.Accounting }
					: new[] { KingdomGrowthDomainStepKind.CropRegistry,
						KingdomGrowthDomainStepKind.Field,
						KingdomGrowthDomainStepKind.PendingCrop,
						KingdomGrowthDomainStepKind.Accounting };
				break;
			case KingdomGrowthAction.Irrigate:
				expected = new[] { KingdomGrowthDomainStepKind.Field }; break;
			default: return false;
			}
			if (operation.DomainSteps.Count != expected.Length) return false;
			for (int i = 0; i < expected.Length; i++)
			{
				KingdomGrowthDomainStep step = operation.DomainSteps[i];
				if (step == null || step.Kind != expected[i]
					|| !string.Equals(step.Lease.ScopeId, operation.SettlementId,
						StringComparison.Ordinal)
					|| !GrowthDomainScalarBinding(operation, step)) return false;
				if (step.Kind == KingdomGrowthDomainStepKind.Population
					|| step.Kind == KingdomGrowthDomainStepKind.PendingCrop
					|| step.Kind == KingdomGrowthDomainStepKind.Scarcity
					|| step.Kind == KingdomGrowthDomainStepKind.Accounting
					|| step.Kind == KingdomGrowthDomainStepKind.SubsidenceSchedule
					|| step.Kind == KingdomGrowthDomainStepKind.PorterJob)
				{
					if (!string.Equals(step.SubjectId, operation.SettlementId,
						StringComparison.Ordinal)) return false;
				}
				else if (step.Kind == KingdomGrowthDomainStepKind.Field
					|| step.Kind == KingdomGrowthDomainStepKind.CropRegistry)
				{
					if (!string.Equals(step.SubjectId, operation.FieldId,
						StringComparison.Ordinal) || !string.Equals(step.ActorId,
						operation.TargetId, StringComparison.Ordinal)) return false;
				}
				else
				{
					string actor = operation.TargetId;
					if (!string.Equals(step.ActorId, actor, StringComparison.Ordinal)
						|| !string.Equals(step.SubjectId, actor, StringComparison.Ordinal)) return false;
				}
			}
			return true;
		}

		private static bool GrowthDomainScalarBinding(KingdomGrowthOperation operation,
			KingdomGrowthDomainStep step)
		{
			switch (step.Kind)
			{
			case KingdomGrowthDomainStepKind.Population:
				return step.BeforeValue == operation.PopulationBefore
					&& step.AfterValue == operation.PopulationAfter;
			case KingdomGrowthDomainStepKind.PendingCrop:
				return step.BeforeValue == operation.PendingCropBefore
					&& step.AfterValue == operation.PendingCropAfter;
			case KingdomGrowthDomainStepKind.SubsidenceSchedule:
				return step.BeforeValue == operation.SubsidenceBefore
					&& step.AfterValue == operation.SubsidenceAfter;
			case KingdomGrowthDomainStepKind.Enrollment:
			case KingdomGrowthDomainStepKind.Roster:
			case KingdomGrowthDomainStepKind.Creed:
				if (operation.Action == KingdomGrowthAction.Arrival)
					return step.AfterValue == step.BeforeValue + 1L;
				return step.BeforeValue > 0L && step.AfterValue == step.BeforeValue - 1L;
			case KingdomGrowthDomainStepKind.Field:
			case KingdomGrowthDomainStepKind.CropRegistry:
			case KingdomGrowthDomainStepKind.Scarcity:
			case KingdomGrowthDomainStepKind.Accounting:
				return step.BeforeValue < long.MaxValue
					&& step.AfterValue == step.BeforeValue + 1L;
			default: return false;
			}
		}

		private static bool GrowthLeaseShape(KingdomLifecycleResourceLease lease,
			string operationId, bool publication)
		{
			long after;
			return lease != null && ValidGeneratedId(operationId)
				&& string.Equals(lease.OperationId, operationId, StringComparison.Ordinal)
				&& GrowthResourceKindAllowed(lease.Kind) && ValidRootId(lease.ScopeId)
				&& ValidRootId(lease.SubjectId)
				&& string.Equals(lease.Key, ResourceKey(lease.Kind, lease.ScopeId,
					lease.SubjectId), StringComparison.Ordinal)
				&& lease.Delta != 0L && CheckedAdd(lease.Before, lease.Delta, out after)
				&& after == lease.After && lease.BeforeRevision >= 0L
				&& lease.BeforeRevision < long.MaxValue
				&& lease.AfterRevision == lease.BeforeRevision + 1L
				&& Enum.IsDefined(typeof(KingdomLifecycleLeaseState), lease.State)
				&& (!publication || lease.State == KingdomLifecycleLeaseState.Prepared);
		}

		private static bool GrowthResourceKindAllowed(KingdomLifecycleResourceKind kind)
		{
			return kind == KingdomLifecycleResourceKind.Population
				|| kind == KingdomLifecycleResourceKind.Roster
				|| kind == KingdomLifecycleResourceKind.OriginRoster
				|| kind == KingdomLifecycleResourceKind.CreedRoster
				|| kind == KingdomLifecycleResourceKind.WaterVessel
				|| kind == KingdomLifecycleResourceKind.Object
				|| kind == KingdomLifecycleResourceKind.Projection
				|| kind == KingdomLifecycleResourceKind.GrowthClock
				|| kind == KingdomLifecycleResourceKind.GrowthPendingCrop
				|| kind == KingdomLifecycleResourceKind.GrowthField
				|| kind == KingdomLifecycleResourceKind.GrowthHealth
				|| kind == KingdomLifecycleResourceKind.GrowthScarcity
				|| kind == KingdomLifecycleResourceKind.GrowthAccounting
				|| kind == KingdomLifecycleResourceKind.GrowthCropRegistry
				|| kind == KingdomLifecycleResourceKind.GrowthSubsidenceSchedule
				|| kind == KingdomLifecycleResourceKind.GrowthPorterJob
				|| kind == KingdomLifecycleResourceKind.GrowthEscrowRelease
				|| kind == KingdomLifecycleResourceKind.GrowthArrivalCandidate;
		}

		private static bool GrowthResourceShape(KingdomLifecycleResourceRevision row)
		{
			return row != null && GrowthResourceKindAllowed(row.Kind) && ValidRootId(row.ScopeId)
				&& ValidRootId(row.SubjectId) && row.Revision >= 0L
				&& string.Equals(row.Key, ResourceKey(row.Kind, row.ScopeId, row.SubjectId),
					StringComparison.Ordinal)
				&& (row.ActiveOperationId == null
					|| ValidGeneratedId(row.ActiveOperationId))
				&& (row.LastOperationId == null
					|| ValidGeneratedId(row.LastOperationId));
		}

		private static bool GrowthPrefixShape(KingdomGrowthOperation operation, bool publication)
		{
			if (!publication && operation.Phase == KingdomGrowthPhase.Quarantined)
				return GrowthWaterPrefix(operation.WaterLegs, operation.WaterCursor, false)
					&& GrowthObjectPrefix(operation.Sources, operation.SourceCursor, false)
					&& GrowthObjectPrefix(operation.Outputs, operation.OutputCursor, false)
					&& GrowthDomainPrefix(operation.DomainSteps, operation.DomainCursor, false)
					&& (operation.ClockState == KingdomLifecyclePhysicalState.Prepared
						&& operation.ClockLease.State == KingdomLifecycleLeaseState.Prepared
						|| operation.ClockState == KingdomLifecyclePhysicalState.Intent
							&& operation.ClockLease.State == KingdomLifecycleLeaseState.Intent
						|| operation.ClockState == KingdomLifecyclePhysicalState.Proved
							&& operation.ClockLease.State == KingdomLifecycleLeaseState.Proved);
			int current = GrowthPhaseIndex(operation, operation.Phase);
			if (current < 0 || !GrowthWaterPhaseShape(operation, current, publication)
				|| !GrowthObjectPhaseShape(operation, operation.Sources, operation.SourceCursor,
					KingdomGrowthPhase.SourceIntent, KingdomGrowthPhase.SourcesSettled,
					current, publication)
				|| !GrowthObjectPhaseShape(operation, operation.Outputs, operation.OutputCursor,
					KingdomGrowthPhase.OutputIntent, KingdomGrowthPhase.OutputsSettled,
					current, publication)
				|| !GrowthDomainPhaseShape(operation, current, publication))
				return false;
			if (publication) return operation.ClockState == KingdomLifecyclePhysicalState.Prepared
				&& operation.ClockLease.State == KingdomLifecycleLeaseState.Prepared;
			if (operation.Phase == KingdomGrowthPhase.Sinks
				|| operation.Phase == KingdomGrowthPhase.Terminal)
				return operation.ClockState == KingdomLifecyclePhysicalState.Proved
					&& operation.ClockLease.State == KingdomLifecycleLeaseState.Proved
					&& (operation.Phase != KingdomGrowthPhase.Terminal
						|| GrowthOutboxTerminal(operation));
			if (operation.Phase == KingdomGrowthPhase.ClockIntent)
				return operation.ClockState == KingdomLifecyclePhysicalState.Prepared
					&& operation.ClockLease.State == KingdomLifecycleLeaseState.Prepared
					|| operation.ClockState == KingdomLifecyclePhysicalState.Intent
						&& operation.ClockLease.State == KingdomLifecycleLeaseState.Intent
					|| operation.ClockState == KingdomLifecyclePhysicalState.Proved
						&& operation.ClockLease.State == KingdomLifecycleLeaseState.Proved;
			return operation.ClockState == KingdomLifecyclePhysicalState.Prepared
				&& operation.ClockLease.State == KingdomLifecycleLeaseState.Prepared;
		}

		private static bool GrowthWaterPhaseShape(KingdomGrowthOperation operation,
			int current, bool publication)
		{
			int intent = GrowthPhaseIndex(operation, KingdomGrowthPhase.WaterIntent);
			int settled = GrowthPhaseIndex(operation, KingdomGrowthPhase.WaterSettled);
			if (intent < 0) return operation.WaterLegs.Count == 0 && operation.WaterCursor == 0;
			if (current < intent || publication) return operation.WaterCursor == 0
				&& GrowthWaterPrefix(operation.WaterLegs, 0, true);
			if (current == intent) return GrowthWaterPrefix(operation.WaterLegs,
				operation.WaterCursor, false);
			return current >= settled && operation.WaterCursor == operation.WaterLegs.Count
				&& GrowthWaterPrefix(operation.WaterLegs, operation.WaterCursor, false);
		}

		private static bool GrowthObjectPhaseShape(KingdomGrowthOperation operation,
			List<KingdomGrowthObjectLeg> rows, int cursor, KingdomGrowthPhase intentPhase,
			KingdomGrowthPhase settledPhase, int current, bool publication)
		{
			int intent = GrowthPhaseIndex(operation, intentPhase);
			int settled = GrowthPhaseIndex(operation, settledPhase);
			if (intent < 0) return rows.Count == 0 && cursor == 0;
			if (current < intent || publication) return cursor == 0
				&& GrowthObjectPrefix(rows, 0, true);
			if (current == intent) return GrowthObjectPrefix(rows, cursor, false);
			return current >= settled && cursor == rows.Count && GrowthObjectPrefix(rows, cursor, false);
		}

		private static bool GrowthDomainPhaseShape(KingdomGrowthOperation operation,
			int current, bool publication)
		{
			int intent = GrowthPhaseIndex(operation, KingdomGrowthPhase.DomainIntent);
			int settled = GrowthPhaseIndex(operation, KingdomGrowthPhase.DomainSettled);
			if (current < intent || publication) return operation.DomainCursor == 0
				&& GrowthDomainPrefix(operation.DomainSteps, 0, true);
			if (current == intent) return GrowthDomainPrefix(operation.DomainSteps,
				operation.DomainCursor, false);
			return current >= settled && operation.DomainCursor == operation.DomainSteps.Count
				&& GrowthDomainPrefix(operation.DomainSteps, operation.DomainCursor, false);
		}

		private static bool GrowthWaterPrefix(List<KingdomGrowthWaterLeg> rows, int cursor,
			bool publication)
		{
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomLifecyclePhysicalState expected = i < cursor
					? KingdomLifecyclePhysicalState.Proved : KingdomLifecyclePhysicalState.Prepared;
				if (i == cursor && !publication && rows[i].State == KingdomLifecyclePhysicalState.Intent)
					expected = KingdomLifecyclePhysicalState.Intent;
				if (rows[i].State != expected) return false;
			}
			return !publication || cursor == 0;
		}

		private static bool GrowthObjectPrefix(List<KingdomGrowthObjectLeg> rows, int cursor,
			bool publication)
		{
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomLifecyclePhysicalState expected = i < cursor
					? KingdomLifecyclePhysicalState.Proved : KingdomLifecyclePhysicalState.Prepared;
				if (i == cursor && !publication && rows[i].State == KingdomLifecyclePhysicalState.Intent)
					expected = KingdomLifecyclePhysicalState.Intent;
				if (rows[i].State != expected) return false;
			}
			return !publication || cursor == 0;
		}

		private static bool GrowthDomainPrefix(List<KingdomGrowthDomainStep> rows, int cursor,
			bool publication)
		{
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomLifecyclePhysicalState expected = i < cursor
					? KingdomLifecyclePhysicalState.Proved : KingdomLifecyclePhysicalState.Prepared;
				if (i == cursor && !publication && rows[i].State == KingdomLifecyclePhysicalState.Intent)
					expected = KingdomLifecyclePhysicalState.Intent;
				if (rows[i].State != expected) return false;
			}
			return !publication || cursor == 0;
		}

		private static bool GrowthWaterShape(KingdomGrowthOperation operation,
			KingdomGrowthWaterLeg leg, int ordinal, bool publication)
		{
			int after;
			if (leg == null || !string.Equals(leg.OperationId, operation.Id, StringComparison.Ordinal)
				|| !string.Equals(leg.EventId, ChildId(operation.Id, "water", ordinal),
					StringComparison.Ordinal)
				|| leg.ContainerKind != KingdomGrowthWaterContainerKind.LiquidVolume
				|| !ValidRootId(leg.ContainerId) || !ValidName(leg.Blueprint)
				|| !GrowthTopologyValid(leg.OwnerTopology, leg.OwnerId, leg.ZoneId, leg.X, leg.Y)
				|| !GrowthLocationShape(leg.BeforeLocation, leg.BeforeOwnerId, leg.BeforeZoneId,
					leg.BeforeX, leg.BeforeY)
				|| !GrowthLocationShape(leg.AfterLocation, leg.AfterOwnerId, leg.AfterZoneId,
					leg.AfterX, leg.AfterY)
				|| GrowthLocationFromTopology(leg.OwnerTopology) != leg.BeforeLocation
				|| !string.Equals(leg.OwnerId, leg.BeforeOwnerId, StringComparison.Ordinal)
				|| !string.Equals(leg.ZoneId, leg.BeforeZoneId, StringComparison.Ordinal)
				|| leg.X != leg.BeforeX || leg.Y != leg.BeforeY
				|| (leg.OwnerRemovedAfter ? (leg.MutationKind != KingdomGrowthWaterMutationKind.Drain
					|| leg.After != 0 || leg.AfterLocation != KingdomGrowthLocationKind.Graveyard)
					: (leg.AfterLocation != leg.BeforeLocation
						|| !string.Equals(leg.AfterOwnerId, leg.BeforeOwnerId, StringComparison.Ordinal)
						|| !string.Equals(leg.AfterZoneId, leg.BeforeZoneId, StringComparison.Ordinal)
						|| leg.AfterX != leg.BeforeX || leg.AfterY != leg.BeforeY))
				|| leg.Capacity <= 0 || leg.Before < 0 || leg.Before > leg.Capacity
				|| leg.Delta <= 0 || !CheckedAdd(leg.Before,
					leg.MutationKind == KingdomGrowthWaterMutationKind.Drain ? -leg.Delta : leg.Delta,
					out after) || after != leg.After || leg.After < 0 || leg.After > leg.Capacity
				|| (leg.MutationKind != KingdomGrowthWaterMutationKind.Drain
					&& leg.MutationKind != KingdomGrowthWaterMutationKind.Fill)
				|| string.IsNullOrEmpty(leg.BeforeComposition)
				|| string.IsNullOrEmpty(leg.AfterComposition)
				|| TooLong(leg.BeforeComposition, MaxTextChars)
				|| TooLong(leg.AfterComposition, MaxTextChars)
				|| !GrowthWitnessHash(leg.BeforeOwnerGraphHash)
				|| !GrowthWitnessHash(leg.AfterOwnerGraphHash)
				|| !GrowthWitnessHash(leg.BeforePartGraphHash)
				|| !GrowthWitnessHash(leg.AfterPartGraphHash)
				|| !GrowthWitnessHash(leg.BeforeTopologyHash)
				|| !GrowthWitnessHash(leg.AfterTopologyHash)
				|| string.Equals(leg.BeforePartGraphHash, leg.AfterPartGraphHash,
					StringComparison.Ordinal)
				|| !string.Equals(leg.ReceiptId, ChildId(operation.Id, "water-receipt", ordinal),
					StringComparison.Ordinal)
				|| !GrowthLeaseShape(leg.Lease, operation.Id, publication)
				|| leg.Lease.Kind != KingdomLifecycleResourceKind.WaterVessel
				|| !string.Equals(leg.Lease.ScopeId, leg.ZoneId, StringComparison.Ordinal)
				|| !string.Equals(leg.Lease.SubjectId, leg.ContainerId, StringComparison.Ordinal)
				|| !string.Equals(leg.LeaseKey, leg.Lease.Key, StringComparison.Ordinal)
				|| leg.Lease.Before != leg.Before || leg.Lease.After != leg.After
				|| !KnownPhysical(leg.State) || !KnownPhysical(leg.ReceiptState)) return false;
			return GrowthWaterReceiptShape(operation, leg, ordinal, publication);
		}

		private static bool GrowthWaterReceiptShape(KingdomGrowthOperation operation,
			KingdomGrowthWaterLeg leg, int ordinal, bool publication)
		{
			if (publication || leg.State == KingdomLifecyclePhysicalState.Prepared)
				return leg.State == KingdomLifecyclePhysicalState.Prepared
					&& leg.ReceiptState == KingdomLifecyclePhysicalState.Prepared
					&& leg.Lease.State == KingdomLifecycleLeaseState.Prepared
					&& leg.ReceiptBeforeMatches == -1 && leg.ReceiptAfterMatches == -1
					&& GrowthWaterReceiptHashesEmpty(leg) && leg.ReceiptProofId == null;
			if (leg.State == KingdomLifecyclePhysicalState.Intent)
				return leg.ReceiptState == KingdomLifecyclePhysicalState.Intent
					&& leg.Lease.State == KingdomLifecycleLeaseState.Intent
					&& leg.ReceiptBeforeMatches == 1 && leg.ReceiptAfterMatches == -1
					&& GrowthWaterReceiptBeforeExact(leg) && GrowthWaterReceiptAfterEmpty(leg)
					&& leg.ReceiptProofId == null;
			return leg.State == KingdomLifecyclePhysicalState.Proved
				&& leg.ReceiptState == KingdomLifecyclePhysicalState.Proved
				&& leg.Lease.State == KingdomLifecycleLeaseState.Proved
				&& leg.ReceiptBeforeMatches == 1 && leg.ReceiptAfterMatches == 1
				&& GrowthWaterReceiptBeforeExact(leg) && GrowthWaterReceiptAfterExact(leg)
				&& string.Equals(leg.ReceiptCallbackContainerId, leg.ContainerId,
					StringComparison.Ordinal)
				&& GrowthWitnessHash(leg.ReceiptCallbackReferenceHash)
				&& leg.ReceiptSameReference
				&& string.Equals(leg.ReceiptProofId,
					GrowthWaterReceiptProof(operation, leg, ordinal), StringComparison.Ordinal);
		}

		private static bool GrowthWaterReceiptHashesEmpty(KingdomGrowthWaterLeg leg)
		{
			return leg.ReceiptBeforeOwnerGraphHash == null
				&& leg.ReceiptAfterOwnerGraphHash == null
				&& leg.ReceiptBeforePartGraphHash == null
				&& leg.ReceiptAfterPartGraphHash == null
				&& leg.ReceiptBeforeTopologyHash == null
				&& leg.ReceiptAfterTopologyHash == null
				&& leg.ReceiptCallbackContainerId == null
				&& leg.ReceiptCallbackReferenceHash == null
				&& !leg.ReceiptSameReference;
		}

		private static bool GrowthWaterReceiptBeforeExact(KingdomGrowthWaterLeg leg)
		{
			return string.Equals(leg.ReceiptBeforeOwnerGraphHash, leg.BeforeOwnerGraphHash,
				StringComparison.Ordinal) && string.Equals(leg.ReceiptBeforePartGraphHash,
				leg.BeforePartGraphHash, StringComparison.Ordinal)
				&& string.Equals(leg.ReceiptBeforeTopologyHash, leg.BeforeTopologyHash,
					StringComparison.Ordinal);
		}

		private static bool GrowthWaterReceiptAfterEmpty(KingdomGrowthWaterLeg leg)
		{
			return leg.ReceiptAfterOwnerGraphHash == null
				&& leg.ReceiptAfterPartGraphHash == null
				&& leg.ReceiptAfterTopologyHash == null
				&& leg.ReceiptCallbackContainerId == null
				&& leg.ReceiptCallbackReferenceHash == null
				&& !leg.ReceiptSameReference;
		}

		private static bool GrowthWaterReceiptAfterExact(KingdomGrowthWaterLeg leg)
		{
			return string.Equals(leg.ReceiptAfterOwnerGraphHash, leg.AfterOwnerGraphHash,
				StringComparison.Ordinal) && string.Equals(leg.ReceiptAfterPartGraphHash,
				leg.AfterPartGraphHash, StringComparison.Ordinal)
				&& string.Equals(leg.ReceiptAfterTopologyHash, leg.AfterTopologyHash,
					StringComparison.Ordinal);
		}

		private static bool GrowthObjectShape(KingdomGrowthOperation operation,
			KingdomGrowthObjectLeg leg, int ordinal, bool output, bool publication)
		{
			int after;
			bool create = leg != null
				&& leg.MutationKind == KingdomGrowthObjectMutationKind.Create;
			bool createObserved = create && leg.Callbacks != null && leg.Callbacks.Count > 0
				&& leg.Callbacks[0] != null
				&& leg.Callbacks[0].State == KingdomLifecyclePhysicalState.Proved;
			bool createSettled = create && leg.State == KingdomLifecyclePhysicalState.Proved;
			if (leg == null || !string.Equals(leg.OperationId, operation.Id, StringComparison.Ordinal)
				|| !string.Equals(leg.EventId, ChildId(operation.Id,
					output ? "output" : "source", ordinal), StringComparison.Ordinal)
				|| (createObserved ? !ValidRootId(leg.ObjectId)
					: create ? leg.ObjectId != null : !ValidRootId(leg.ObjectId))
				|| !ValidRootId(leg.Marker)
				|| !ValidName(leg.Blueprint) || !GrowthTopologyValid(leg.Topology, leg.OwnerId,
					leg.ZoneId, leg.X, leg.Y) || leg.BeforeCount < 0
				|| !CheckedAdd(leg.BeforeCount, leg.Delta, out after) || after != leg.AfterCount
				|| !ValidCount(leg.BeforeCount) || !ValidCount(leg.AfterCount)
				|| !GrowthWitnessHash(leg.BeforeOwnerGraphHash)
				|| (create && !createSettled ? !GrowthOptionalWitnessSet(
					leg.AfterOwnerGraphHash, leg.AfterObjectGraphHash, leg.AfterTopologyHash)
					: !GrowthWitnessHash(leg.AfterOwnerGraphHash))
				|| !GrowthWitnessHash(leg.BeforeObjectGraphHash)
				|| (!create || createSettled) && !GrowthWitnessHash(leg.AfterObjectGraphHash)
				|| !GrowthWitnessHash(leg.BeforeTopologyHash)
				|| (!create || createSettled) && !GrowthWitnessHash(leg.AfterTopologyHash)
				|| (leg.AfterOwnerGraphHash != null && string.Equals(leg.BeforeOwnerGraphHash,
					leg.AfterOwnerGraphHash, StringComparison.Ordinal))
				|| !string.Equals(leg.ReceiptId, ChildId(operation.Id,
					output ? "output-receipt" : "source-receipt", ordinal),
					StringComparison.Ordinal)
				|| !string.Equals(leg.ReceiptTopologyId, TopologyId(leg.Topology, leg.OwnerId,
					leg.ZoneId, leg.X, leg.Y), StringComparison.Ordinal)
				|| !GrowthLeaseShape(leg.Lease, operation.Id, publication)
				|| leg.Lease.Kind != KingdomLifecycleResourceKind.Object
				|| !string.Equals(leg.Lease.ScopeId, operation.SettlementId,
					StringComparison.Ordinal)
				|| !string.Equals(leg.Lease.SubjectId, create ? leg.Marker : leg.ObjectId,
					StringComparison.Ordinal)
				|| !KnownPhysical(leg.State) || !KnownPhysical(leg.ReceiptState)
				|| !GrowthObjectPipelineShape(leg, publication)) return false;
			KingdomLifecycleLeaseState expectedLease = leg.State == KingdomLifecyclePhysicalState.Proved
				? KingdomLifecycleLeaseState.Proved : leg.State == KingdomLifecyclePhysicalState.Intent
					? KingdomLifecycleLeaseState.Intent : KingdomLifecycleLeaseState.Prepared;
			if (leg.Lease.State != expectedLease) return false;
			if (output)
			{
				if (leg.Delta <= 0 || !leg.NoStack
					|| (leg.MutationKind != KingdomGrowthObjectMutationKind.Create
						&& leg.MutationKind != KingdomGrowthObjectMutationKind.CellAdd
						&& leg.MutationKind != KingdomGrowthObjectMutationKind.InventoryAdd
						&& leg.MutationKind != KingdomGrowthObjectMutationKind.Receive)) return false;
				if (leg.MutationKind == KingdomGrowthObjectMutationKind.Create
					? (!string.Equals(leg.CreatedMarker, leg.Marker, StringComparison.Ordinal)
						|| leg.DetachedMarker != null || leg.BeforeCount != 0
						|| leg.Callbacks.Count < 2
						|| leg.AfterLocation != GrowthLocationFromTopology(leg.Topology))
					: (leg.CreatedMarker != null || !string.Equals(
						leg.DetachedMarker, leg.Marker, StringComparison.Ordinal))) return false;
				if (leg.MutationKind == KingdomGrowthObjectMutationKind.CellAdd
					&& leg.Topology != KingdomLifecycleTopology.Cell) return false;
				if ((leg.MutationKind == KingdomGrowthObjectMutationKind.InventoryAdd
					|| leg.MutationKind == KingdomGrowthObjectMutationKind.Receive)
					&& leg.Topology != KingdomLifecycleTopology.Inventory) return false;
			}
			else
			{
				if (leg.CreatedMarker != null) return false;
				if (leg.MutationKind == KingdomGrowthObjectMutationKind.HarvestableRipeSet)
				{
					if (leg.Delta != 0 || leg.BeforeCount != leg.AfterCount
						|| leg.DetachedMarker != null) return false;
				}
				else
				{
					if (leg.Delta >= 0
						|| !string.Equals(leg.DetachedMarker, leg.Marker, StringComparison.Ordinal)
						|| (leg.MutationKind != KingdomGrowthObjectMutationKind.DestroyOne
							&& leg.MutationKind != KingdomGrowthObjectMutationKind.Obliterate)) return false;
					if (leg.MutationKind == KingdomGrowthObjectMutationKind.DestroyOne
						&& leg.Delta != -1) return false;
					if (leg.MutationKind == KingdomGrowthObjectMutationKind.Obliterate
						&& leg.AfterCount != 0) return false;
				}
			}
			return GrowthObjectReceiptShape(operation, leg, ordinal, output, publication);
		}

		private static bool GrowthOptionalWitnessSet(string one, string two, string three)
		{
			return one == null && two == null && three == null
				|| GrowthWitnessHash(one) && GrowthWitnessHash(two) && GrowthWitnessHash(three);
		}

		private static bool GrowthObjectPipelineShape(KingdomGrowthObjectLeg leg, bool publication)
		{
			if (leg.Callbacks == null || leg.Callbacks.Count == 0
				|| leg.Callbacks.Count > MaxGrowthObjectCallbacks || leg.CallbackCursor < 0
				|| leg.CallbackCursor > leg.Callbacks.Count
				|| leg.BeforeLocation == KingdomGrowthLocationKind.None
				|| leg.AfterLocation == KingdomGrowthLocationKind.None) return false;
			KingdomGrowthObjectCallbackStep first = leg.Callbacks[0];
			KingdomGrowthObjectCallbackStep last = leg.Callbacks[leg.Callbacks.Count - 1];
			if (first == null || last == null || first.Kind != leg.MutationKind
				|| first.FromLocation != leg.BeforeLocation || last.ToLocation != leg.AfterLocation
				|| first.BeforeCount != leg.BeforeCount || last.AfterCount != leg.AfterCount
				|| first.NoStack != leg.NoStack || last.NoStack != leg.NoStack
				|| !string.Equals(first.BeforeOwnerGraphHash, leg.BeforeOwnerGraphHash,
					StringComparison.Ordinal)
				|| !string.Equals(last.AfterOwnerGraphHash, leg.AfterOwnerGraphHash,
					StringComparison.Ordinal)
				|| !string.Equals(first.BeforeObjectGraphHash, leg.BeforeObjectGraphHash,
					StringComparison.Ordinal)
				|| !string.Equals(last.AfterObjectGraphHash, leg.AfterObjectGraphHash,
					StringComparison.Ordinal)
				|| !string.Equals(first.BeforeTopologyHash, leg.BeforeTopologyHash,
					StringComparison.Ordinal)
				|| !string.Equals(last.AfterTopologyHash, leg.AfterTopologyHash,
					StringComparison.Ordinal)) return false;
			for (int i = 0; i < leg.Callbacks.Count; i++)
			{
				KingdomGrowthObjectCallbackStep step = leg.Callbacks[i];
				if (!GrowthObjectCallbackStepShape(step, leg.EventId, leg.ObjectId, leg.Marker, i)
					|| (i > 0 && (leg.Callbacks[i - 1].ToLocation != step.FromLocation
						|| leg.Callbacks[i - 1].AfterCount != step.BeforeCount
						|| !GrowthOptionalWitnessChain(leg.Callbacks[i - 1].AfterOwnerGraphHash,
							step.BeforeOwnerGraphHash)
						|| !GrowthOptionalWitnessChain(leg.Callbacks[i - 1].AfterObjectGraphHash,
							step.BeforeObjectGraphHash)
						|| !GrowthOptionalWitnessChain(leg.Callbacks[i - 1].AfterTopologyHash,
							step.BeforeTopologyHash)))) return false;
				KingdomLifecyclePhysicalState expected = i < leg.CallbackCursor
					? KingdomLifecyclePhysicalState.Proved : KingdomLifecyclePhysicalState.Prepared;
				if (i == leg.CallbackCursor && !publication
					&& step.State == KingdomLifecyclePhysicalState.Intent)
					expected = KingdomLifecyclePhysicalState.Intent;
				if (step.State != expected) return false;
			}
			if (publication) return leg.CallbackCursor == 0
				&& leg.State == KingdomLifecyclePhysicalState.Prepared;
			if (leg.State == KingdomLifecyclePhysicalState.Prepared)
				return leg.CallbackCursor == 0;
			if (leg.State == KingdomLifecyclePhysicalState.Intent)
				return leg.CallbackCursor < leg.Callbacks.Count;
			return leg.State == KingdomLifecyclePhysicalState.Proved
				&& leg.CallbackCursor == leg.Callbacks.Count;
		}

		private static bool GrowthOptionalWitnessChain(string left, string right)
		{
			return left == null && right == null
				|| left != null && string.Equals(left, right, StringComparison.Ordinal);
		}

		private static bool GrowthObjectCallbackStepShape(KingdomGrowthObjectCallbackStep step,
			string parentId, string objectId, string marker, int ordinal,
			bool allowAbsentReference = false)
		{
			if (step == null || !string.Equals(step.EventId,
				ChildId(parentId, "object-callback", ordinal), StringComparison.Ordinal)
				|| !Enum.IsDefined(typeof(KingdomGrowthObjectMutationKind), step.Kind)
				|| step.Kind == KingdomGrowthObjectMutationKind.None
				|| !GrowthLocationShape(step.FromLocation, step.BeforeOwnerId, step.BeforeZoneId,
					step.BeforeX, step.BeforeY)
				|| !GrowthLocationShape(step.ToLocation, step.AfterOwnerId, step.AfterZoneId,
					step.AfterX, step.AfterY)
				|| ((step.FromLocation == KingdomGrowthLocationKind.Escrow
					|| step.ToLocation == KingdomGrowthLocationKind.Escrow)
					? !ValidRootId(step.EscrowKey) : step.EscrowKey != null)
				|| !ValidCount(step.BeforeCount) || !ValidCount(step.AfterCount)
				|| !KnownPhysical(step.State) || !KnownPhysical(step.ReceiptState)
				|| !string.Equals(step.ReceiptId,
					ChildId(parentId, "object-callback-receipt", ordinal), StringComparison.Ordinal))
				return false;
			bool createPending = step.Kind == KingdomGrowthObjectMutationKind.Create
				&& step.State != KingdomLifecyclePhysicalState.Proved;
			bool deferredPrepared = step.State == KingdomLifecyclePhysicalState.Prepared
				&& step.Kind != KingdomGrowthObjectMutationKind.Create
				&& step.BeforeOwnerGraphHash == null
				&& step.BeforeObjectGraphHash == null && step.BeforeTopologyHash == null
				&& GrowthOptionalWitnessSet(step.AfterOwnerGraphHash,
					step.AfterObjectGraphHash, step.AfterTopologyHash);
			if (createPending)
			{
				if (!GrowthWitnessHash(step.BeforeOwnerGraphHash)
					|| !GrowthWitnessHash(step.BeforeObjectGraphHash)
					|| !GrowthWitnessHash(step.BeforeTopologyHash)
					|| step.AfterOwnerGraphHash != null || step.AfterObjectGraphHash != null
					|| step.AfterTopologyHash != null || objectId != null) return false;
			}
			else if (!deferredPrepared && (!GrowthWitnessHash(step.BeforeOwnerGraphHash)
				|| !GrowthWitnessHash(step.AfterOwnerGraphHash)
				|| !GrowthWitnessHash(step.BeforeObjectGraphHash)
				|| !GrowthWitnessHash(step.AfterObjectGraphHash)
				|| !GrowthWitnessHash(step.BeforeTopologyHash)
				|| !GrowthWitnessHash(step.AfterTopologyHash))) return false;
			bool cropMutation = step.Kind == KingdomGrowthObjectMutationKind.HarvestableRipeSet;
			if (cropMutation)
			{
				if (step.FromLocation != step.ToLocation || step.FromLocation != KingdomGrowthLocationKind.Cell
					|| step.BeforeCount != step.AfterCount || step.BeforeCount <= 0
					|| !step.BeforeHasHarvestable || !step.AfterHasHarvestable
					|| step.BeforeRipe == step.AfterRipe
					|| step.BeforeRegenTimer < 0 || step.AfterRegenTimer < 0
					|| !string.Equals(step.BeforeRegenTime, string.Empty, StringComparison.Ordinal)
					|| !string.Equals(step.AfterRegenTime, string.Empty, StringComparison.Ordinal)
					|| step.BeforeTileIndex < -1 || step.AfterTileIndex < -1
					|| !GrowthBoundedPresentString(step.BeforeRenderTile)
					|| !GrowthBoundedPresentString(step.AfterRenderTile)
					|| !GrowthBoundedPresentString(step.BeforeRenderColor)
					|| !GrowthBoundedPresentString(step.AfterRenderColor)
					|| !GrowthBoundedPresentString(step.BeforeRenderDetail)
					|| !GrowthBoundedPresentString(step.AfterRenderDetail)
					|| !GrowthBoundedPresentString(step.BeforeRenderString)
					|| !GrowthBoundedPresentString(step.AfterRenderString)
					|| !GrowthBoundedPresentString(step.BeforeTileColor)
					|| !GrowthBoundedPresentString(step.AfterTileColor)) return false;
			}
			else if (step.BeforeHasHarvestable || step.AfterHasHarvestable
				|| step.BeforeRipe || step.AfterRipe
				|| step.BeforeRegenTimer != 0 || step.AfterRegenTimer != 0
				|| step.BeforeRegenTime != null || step.AfterRegenTime != null
				|| step.BeforeTileIndex != 0 || step.AfterTileIndex != 0
				|| step.BeforeRenderTile != null || step.AfterRenderTile != null
				|| step.BeforeRenderColor != null || step.AfterRenderColor != null
				|| step.BeforeRenderDetail != null || step.AfterRenderDetail != null
				|| step.BeforeRenderString != null || step.AfterRenderString != null
				|| step.BeforeTileColor != null || step.AfterTileColor != null) return false;
			if (!GrowthObjectCallbackTransition(step)) return false;
			if (step.State == KingdomLifecyclePhysicalState.Prepared)
				return step.ReceiptState == KingdomLifecyclePhysicalState.Prepared
					&& step.ReceiptBeforeMatches == -1 && step.ReceiptAfterMatches == -1
					&& step.ReceiptBeforeCount == -1 && step.ReceiptAfterCount == -1
					&& GrowthObjectCallbackReceiptEmpty(step);
			if (step.State == KingdomLifecyclePhysicalState.Intent)
				return step.ReceiptState == KingdomLifecyclePhysicalState.Intent
					&& step.ReceiptBeforeMatches == (step.BeforeCount == 0 ? 0 : 1)
					&& step.ReceiptBeforeCount == step.BeforeCount
					&& step.ReceiptAfterMatches == -1 && step.ReceiptAfterCount == -1
					&& GrowthObjectCallbackReceiptBeforeExact(step)
					&& step.ReceiptAfterOwnerGraphHash == null
					&& step.ReceiptAfterObjectGraphHash == null
					&& step.ReceiptAfterTopologyHash == null
					&& step.ReceiptCallbackObjectId == null
					&& step.ReceiptCallbackMarker == null
					&& step.ReceiptCallbackReferenceHash == null && !step.ReceiptSameReference
					&& step.ReceiptProofId == null;
			return step.State == KingdomLifecyclePhysicalState.Proved
				&& step.ReceiptState == KingdomLifecyclePhysicalState.Proved
				&& step.ReceiptBeforeMatches == (step.BeforeCount == 0 ? 0 : 1)
				&& step.ReceiptAfterMatches == (step.AfterCount == 0 ? 0 : 1)
				&& step.ReceiptBeforeCount == step.BeforeCount
				&& step.ReceiptAfterCount == step.AfterCount
				&& string.Equals(step.ReceiptCallbackObjectId, objectId, StringComparison.Ordinal)
				&& string.Equals(step.ReceiptCallbackMarker, marker, StringComparison.Ordinal)
				&& GrowthWitnessHash(step.ReceiptCallbackReferenceHash)
				&& (step.ReceiptSameReference || allowAbsentReference
					&& step.Kind == KingdomGrowthObjectMutationKind.Obliterate
					&& step.AfterCount == 0)
				&& GrowthObjectCallbackReceiptBeforeExact(step)
				&& string.Equals(step.ReceiptAfterOwnerGraphHash, step.AfterOwnerGraphHash,
					StringComparison.Ordinal)
				&& string.Equals(step.ReceiptAfterObjectGraphHash, step.AfterObjectGraphHash,
					StringComparison.Ordinal)
				&& string.Equals(step.ReceiptAfterTopologyHash, step.AfterTopologyHash,
					StringComparison.Ordinal)
				&& ValidGeneratedId(step.ReceiptProofId);
		}

		private static bool GrowthBoundedPresentString(string value)
		{
			return value != null && !TooLong(value, MaxNameChars);
		}

		private static bool GrowthObjectCallbackTransition(KingdomGrowthObjectCallbackStep step)
		{
			switch (step.Kind)
			{
			case KingdomGrowthObjectMutationKind.Create:
				return step.FromLocation == KingdomGrowthLocationKind.Absent
					&& step.ToLocation == KingdomGrowthLocationKind.Escrow
					&& step.BeforeCount == 0 && step.AfterCount > 0 && step.NoStack;
			case KingdomGrowthObjectMutationKind.CellAdd:
				return step.FromLocation == KingdomGrowthLocationKind.Escrow
					&& step.ToLocation == KingdomGrowthLocationKind.Cell
					&& step.BeforeCount == step.AfterCount && step.BeforeCount > 0 && step.NoStack;
			case KingdomGrowthObjectMutationKind.InventoryAdd:
			case KingdomGrowthObjectMutationKind.Receive:
				return step.FromLocation == KingdomGrowthLocationKind.Escrow
					&& step.ToLocation == KingdomGrowthLocationKind.Inventory
					&& step.BeforeCount == step.AfterCount && step.BeforeCount > 0 && step.NoStack;
			case KingdomGrowthObjectMutationKind.DestroyOne:
				return step.BeforeCount > 0 && step.AfterCount == step.BeforeCount - 1
					&& (step.AfterCount == 0 ? step.ToLocation == KingdomGrowthLocationKind.Graveyard
						: step.ToLocation == step.FromLocation);
			case KingdomGrowthObjectMutationKind.Obliterate:
				return step.BeforeCount > 0 && step.AfterCount == 0
					&& step.ToLocation == KingdomGrowthLocationKind.Graveyard;
			case KingdomGrowthObjectMutationKind.HarvestableRipeSet:
				return true;
			default: return false;
			}
		}

		private static bool GrowthObjectCallbackReceiptEmpty(KingdomGrowthObjectCallbackStep step)
		{
			return step.ReceiptCallbackObjectId == null && step.ReceiptCallbackMarker == null
				&& step.ReceiptCallbackReferenceHash == null && !step.ReceiptSameReference
				&& step.ReceiptBeforeOwnerGraphHash == null && step.ReceiptAfterOwnerGraphHash == null
				&& step.ReceiptBeforeObjectGraphHash == null && step.ReceiptAfterObjectGraphHash == null
				&& step.ReceiptBeforeTopologyHash == null && step.ReceiptAfterTopologyHash == null
				&& step.ReceiptProofId == null;
		}

		private static bool GrowthObjectCallbackReceiptBeforeExact(
			KingdomGrowthObjectCallbackStep step)
		{
			return string.Equals(step.ReceiptBeforeOwnerGraphHash, step.BeforeOwnerGraphHash,
				StringComparison.Ordinal)
				&& string.Equals(step.ReceiptBeforeObjectGraphHash, step.BeforeObjectGraphHash,
					StringComparison.Ordinal)
				&& string.Equals(step.ReceiptBeforeTopologyHash, step.BeforeTopologyHash,
					StringComparison.Ordinal);
		}

		private static bool GrowthLocationShape(KingdomGrowthLocationKind location,
			string ownerId, string zoneId, int x, int y)
		{
			if (!Enum.IsDefined(typeof(KingdomGrowthLocationKind), location)
				|| location == KingdomGrowthLocationKind.None) return false;
			if (location == KingdomGrowthLocationKind.Cell)
				return ownerId == null && ValidName(zoneId) && x >= 0 && x <= MaxCoordinate
					&& y >= 0 && y <= MaxCoordinate;
			if (location == KingdomGrowthLocationKind.Inventory)
				return ValidRootId(ownerId) && ValidName(zoneId) && x == -1 && y == -1;
			return ownerId == null && zoneId == null && x == -1 && y == -1;
		}

		private static KingdomGrowthLocationKind GrowthLocationFromTopology(
			KingdomLifecycleTopology topology)
		{
			if (topology == KingdomLifecycleTopology.Cell) return KingdomGrowthLocationKind.Cell;
			if (topology == KingdomLifecycleTopology.Inventory)
				return KingdomGrowthLocationKind.Inventory;
			return KingdomGrowthLocationKind.None;
		}

		private static bool GrowthObjectReceiptShape(KingdomGrowthOperation operation,
			KingdomGrowthObjectLeg leg, int ordinal, bool output, bool publication)
		{
			if (publication || leg.State == KingdomLifecyclePhysicalState.Prepared)
				return leg.State == KingdomLifecyclePhysicalState.Prepared
					&& leg.ReceiptState == KingdomLifecyclePhysicalState.Prepared
					&& leg.ReceiptBeforeIdMatches == -1 && leg.ReceiptBeforeMarkerMatches == -1
					&& leg.ReceiptBeforeCount == -1 && leg.ReceiptAfterIdMatches == -1
					&& leg.ReceiptAfterMarkerMatches == -1 && leg.ReceiptAfterCount == -1
					&& GrowthObjectReceiptHashesEmpty(leg) && leg.ReceiptProofId == null;
			int beforeMatches = output && leg.MutationKind == KingdomGrowthObjectMutationKind.Create
				? 0 : 1;
			if (leg.State == KingdomLifecyclePhysicalState.Intent)
				return leg.ReceiptState == KingdomLifecyclePhysicalState.Intent
					&& leg.ReceiptBeforeIdMatches == beforeMatches
					&& leg.ReceiptBeforeMarkerMatches == beforeMatches
					&& leg.ReceiptBeforeCount == leg.BeforeCount
					&& leg.ReceiptAfterIdMatches == -1 && leg.ReceiptAfterMarkerMatches == -1
					&& leg.ReceiptAfterCount == -1 && GrowthObjectReceiptBeforeExact(leg)
					&& GrowthObjectReceiptAfterEmpty(leg) && leg.ReceiptProofId == null;
			int afterMatches = leg.AfterCount == 0 ? 0 : 1;
			return leg.State == KingdomLifecyclePhysicalState.Proved
				&& leg.ReceiptState == KingdomLifecyclePhysicalState.Proved
				&& leg.ReceiptBeforeIdMatches == beforeMatches
				&& leg.ReceiptBeforeMarkerMatches == beforeMatches
				&& leg.ReceiptBeforeCount == leg.BeforeCount
				&& leg.ReceiptAfterIdMatches == afterMatches
				&& leg.ReceiptAfterMarkerMatches == afterMatches
				&& leg.ReceiptAfterCount == leg.AfterCount
				&& GrowthObjectReceiptBeforeExact(leg) && GrowthObjectReceiptAfterExact(leg)
				&& string.Equals(leg.ReceiptCallbackObjectId, leg.ObjectId, StringComparison.Ordinal)
				&& string.Equals(leg.ReceiptCallbackMarker, leg.Marker, StringComparison.Ordinal)
				&& GrowthWitnessHash(leg.ReceiptCallbackReferenceHash)
				&& leg.ReceiptSameReference
				&& string.Equals(leg.ReceiptProofId,
					GrowthObjectReceiptProof(operation, leg, ordinal, output), StringComparison.Ordinal);
		}

		private static bool GrowthObjectReceiptHashesEmpty(KingdomGrowthObjectLeg leg)
		{
			return leg.ReceiptBeforeOwnerGraphHash == null
				&& leg.ReceiptAfterOwnerGraphHash == null
				&& leg.ReceiptBeforeObjectGraphHash == null
				&& leg.ReceiptAfterObjectGraphHash == null
				&& leg.ReceiptBeforeTopologyHash == null
				&& leg.ReceiptAfterTopologyHash == null
				&& leg.ReceiptCallbackObjectId == null
				&& leg.ReceiptCallbackMarker == null
				&& leg.ReceiptCallbackReferenceHash == null
				&& !leg.ReceiptSameReference;
		}

		private static bool GrowthObjectReceiptBeforeExact(KingdomGrowthObjectLeg leg)
		{
			return string.Equals(leg.ReceiptBeforeOwnerGraphHash, leg.BeforeOwnerGraphHash,
				StringComparison.Ordinal) && string.Equals(leg.ReceiptBeforeObjectGraphHash,
				leg.BeforeObjectGraphHash, StringComparison.Ordinal)
				&& string.Equals(leg.ReceiptBeforeTopologyHash, leg.BeforeTopologyHash,
					StringComparison.Ordinal);
		}

		private static bool GrowthObjectReceiptAfterEmpty(KingdomGrowthObjectLeg leg)
		{
			return leg.ReceiptAfterOwnerGraphHash == null
				&& leg.ReceiptAfterObjectGraphHash == null
				&& leg.ReceiptAfterTopologyHash == null
				&& leg.ReceiptCallbackObjectId == null
				&& leg.ReceiptCallbackMarker == null
				&& leg.ReceiptCallbackReferenceHash == null
				&& !leg.ReceiptSameReference;
		}

		private static bool GrowthObjectReceiptAfterExact(KingdomGrowthObjectLeg leg)
		{
			return string.Equals(leg.ReceiptAfterOwnerGraphHash, leg.AfterOwnerGraphHash,
				StringComparison.Ordinal) && string.Equals(leg.ReceiptAfterObjectGraphHash,
				leg.AfterObjectGraphHash, StringComparison.Ordinal)
				&& string.Equals(leg.ReceiptAfterTopologyHash, leg.AfterTopologyHash,
					StringComparison.Ordinal);
		}

		private static bool GrowthDomainShape(KingdomGrowthOperation operation,
			KingdomGrowthDomainStep step, int ordinal, bool publication)
		{
			KingdomLifecycleResourceKind kind;
			if (step == null || !TryGrowthDomainKind(step.Kind, step.CallbackKind, out kind)
				|| !GrowthWitnessHash(step.CallbackBodyHash)
				|| !string.Equals(step.EventId, ChildId(operation.Id, "domain", ordinal),
					StringComparison.Ordinal)
				|| !ValidRootId(step.ActorId) || !ValidRootId(step.SubjectId)
				|| !GrowthWitnessHash(step.BeforeGraphHash) || !GrowthWitnessHash(step.AfterGraphHash)
				|| !GrowthWitnessHash(step.BeforeMapHash) || !GrowthWitnessHash(step.AfterMapHash)
				|| string.Equals(step.BeforeMapHash, step.AfterMapHash, StringComparison.Ordinal)
				|| !string.Equals(step.ReceiptId, ChildId(operation.Id, "domain-receipt", ordinal),
					StringComparison.Ordinal)
				|| !GrowthLeaseShape(step.Lease, operation.Id, publication)
				|| step.Lease.Kind != kind || !string.Equals(step.Lease.SubjectId, step.SubjectId,
					StringComparison.Ordinal) || step.Lease.Before != step.BeforeValue
				|| step.Lease.After != step.AfterValue || !KnownPhysical(step.State)
				|| !KnownPhysical(step.ReceiptState)
				|| !GrowthDomainSnapshotsShape(operation, step)) return false;
			if (publication || step.State == KingdomLifecyclePhysicalState.Prepared)
				return step.State == KingdomLifecyclePhysicalState.Prepared
					&& step.ReceiptState == KingdomLifecyclePhysicalState.Prepared
					&& step.Lease.State == KingdomLifecycleLeaseState.Prepared
					&& step.ReceiptBeforeValue == 0L && step.ReceiptAfterValue == 0L
					&& GrowthDomainReceiptHashesEmpty(step)
					&& step.ReceiptProofId == null;
			if (step.State == KingdomLifecyclePhysicalState.Intent)
				return step.ReceiptState == KingdomLifecyclePhysicalState.Intent
					&& step.Lease.State == KingdomLifecycleLeaseState.Intent
					&& step.ReceiptBeforeValue == step.BeforeValue
					&& step.ReceiptAfterValue == 0L && GrowthDomainReceiptBeforeExact(step)
					&& GrowthDomainReceiptAfterEmpty(step) && step.ReceiptProofId == null;
			return step.State == KingdomLifecyclePhysicalState.Proved
				&& step.ReceiptState == KingdomLifecyclePhysicalState.Proved
				&& step.Lease.State == KingdomLifecycleLeaseState.Proved
				&& step.ReceiptBeforeValue == step.BeforeValue
				&& step.ReceiptAfterValue == step.AfterValue
				&& GrowthDomainReceiptBeforeExact(step) && GrowthDomainReceiptAfterExact(step)
				&& string.Equals(step.ReceiptProofId,
					GrowthDomainReceiptProof(operation, step, ordinal), StringComparison.Ordinal);
		}

		private static bool GrowthDomainSnapshotsShape(KingdomGrowthOperation operation,
			KingdomGrowthDomainStep step)
		{
			if (step.Kind == KingdomGrowthDomainStepKind.Scarcity)
				return GrowthScarcitySnapshotShape(step.ScarcityBefore)
					&& GrowthScarcitySnapshotShape(step.ScarcityAfter)
					&& GrowthScarcityTransitionShape(operation, step.ScarcityBefore,
						step.ScarcityAfter)
					&& step.AccountingBefore == null && step.AccountingAfter == null
					&& GrowthTypedDomainSnapshotsNull(step);
			if (step.Kind == KingdomGrowthDomainStepKind.Accounting)
				return GrowthAccountingSnapshotShape(step.AccountingBefore)
					&& GrowthAccountingSnapshotShape(step.AccountingAfter)
					&& GrowthAccountingTransitionShape(operation, step.AccountingBefore,
						step.AccountingAfter)
					&& step.ScarcityBefore == null && step.ScarcityAfter == null
					&& GrowthTypedDomainSnapshotsNull(step);
			if (step.Kind == KingdomGrowthDomainStepKind.Field)
				return step.ScarcityBefore == null && step.ScarcityAfter == null
					&& step.AccountingBefore == null && step.AccountingAfter == null
					&& GrowthFieldStateShape(step.FieldBefore, operation.FieldId)
					&& GrowthFieldStateShape(step.FieldAfter, operation.FieldId)
					&& step.CropRowsBefore == null && step.CropRowsDeclaredAfter == null
					&& step.CropRowsAfter == null;
			if (step.Kind == KingdomGrowthDomainStepKind.CropRegistry)
			{
				bool proved = step.State == KingdomLifecyclePhysicalState.Proved;
				return step.ScarcityBefore == null && step.ScarcityAfter == null
					&& step.AccountingBefore == null && step.AccountingAfter == null
					&& step.FieldBefore == null && step.FieldAfter == null
					&& GrowthCropRowsShape(step.CropRowsBefore, operation.FieldId, false,
						operation)
					&& GrowthCropRowsShape(step.CropRowsDeclaredAfter, operation.FieldId, true,
						operation)
					&& (proved ? GrowthCropDeclarationMatchesObserved(operation,
						step.CropRowsDeclaredAfter, step.CropRowsAfter)
						: step.CropRowsAfter == null);
			}
			return step.ScarcityBefore == null && step.ScarcityAfter == null
				&& step.AccountingBefore == null && step.AccountingAfter == null
				&& GrowthTypedDomainSnapshotsNull(step);
		}

		private static bool GrowthTypedDomainSnapshotsNull(KingdomGrowthDomainStep step)
		{
			return step.FieldBefore == null && step.FieldAfter == null
				&& step.CropRowsBefore == null && step.CropRowsDeclaredAfter == null
				&& step.CropRowsAfter == null;
		}

		private static bool GrowthScarcitySnapshotShape(KingdomGrowthScarcitySnapshot x)
		{
			int provedRations;
			if (x == null || x.DryStreak < 0 || x.HungerStreak < 0
				|| !Enum.IsDefined(typeof(KingdomRules.MealVerdict), x.LastMeal)
				|| x.MealShade < 0
				|| x.ElapsedTicks < 0L || x.Days < 0 || x.Population < 0
				|| !Enum.IsDefined(typeof(GrowthStage), x.Stage)
				|| x.UpkeepRequested < 0 || x.WaterAvailable < 0 || x.RationsAvailable < 0
				|| x.Foraged < 0 || x.Eaten < 0 || x.FromDish < 0 || x.FromDish > x.Eaten
				|| x.Kitchens < 0 || TooLong(x.DishName, MaxTextChars)
				|| TooLong(x.DishText, MaxTextChars) || TooLong(x.DishStaple, MaxTextChars)
				|| TooLong(x.DishSource, MaxTextChars)
				|| x.RequestedWater < 0 || x.ProvedWater < 0
				|| x.RequestedRations < 0 || x.ProvedRations < 0
				|| x.ProvedWater > x.RequestedWater || x.ProvedRations > x.RequestedRations
				|| x.Foraged > x.RequestedRations
				|| !CheckedAdd(x.Foraged, x.Eaten, out provedRations)
				|| x.ProvedRations != Math.Min(x.RequestedRations, provedRations)
				|| !Enum.IsDefined(typeof(KingdomRules.StoresPolicy), x.StoresPolicy)
				|| x.DistrictPercent < 0 || x.DistrictPercent > 100
				|| !Enum.IsDefined(typeof(KingdomGrowthComposedBite), x.ComposedBite)
				|| !Enum.IsDefined(typeof(KingdomGrowthThirstOutcome), x.ThirstOutcome)
				|| !Enum.IsDefined(typeof(KingdomGrowthHungerOutcome), x.HungerOutcome))
				return false;
			bool thirsting = x.ThirstOutcome != KingdomGrowthThirstOutcome.Sustained;
			bool starving = x.HungerOutcome != KingdomGrowthHungerOutcome.Fed;
			bool withering = x.ThirstOutcome == KingdomGrowthThirstOutcome.Withering;
			bool famishing = x.HungerOutcome == KingdomGrowthHungerOutcome.Famine;
			KingdomGrowthComposedBite bite = (KingdomGrowthComposedBite)Math.Max(
				GrowthThirstBite(x.ThirstOutcome), GrowthHungerBite(x.HungerOutcome));
			bool healthy = !thirsting && !starving;
			return x.Thirsting == thirsting && x.Starving == starving
				&& x.Withering == withering && x.Famishing == famishing
				&& x.Healthy == healthy && x.ComposedBite == bite;
		}

		private static bool GrowthScarcityTransitionShape(KingdomGrowthOperation operation,
			KingdomGrowthScarcitySnapshot before, KingdomGrowthScarcitySnapshot after)
		{
			if (operation == null || operation.Action != KingdomGrowthAction.Heartbeat
				|| before == null || after == null || !GrowthScarcityInputsEqual(before, after)
				|| before.Population != operation.PopulationBefore
				|| after.Population != operation.PopulationBefore) return false;
			int water;
			int food;
			if (!GrowthWaterQuantity(operation, KingdomGrowthWaterMutationKind.Drain, out water)
				|| !GrowthRemovedObjectQuantity(operation, true, out food)
				|| after.ProvedWater != water || after.Eaten != food) return false;
			bool enabled = operation.ScarcityOptionState == KingdomLifecycleOptionState.Enabled;
			if (!enabled)
			{
				if (operation.ScarcityOptionState != KingdomLifecycleOptionState.Disabled
					|| after.RequestedWater != 0 || after.ProvedWater != 0
					|| after.RequestedRations != 0 || after.ProvedRations != 0
					|| after.Foraged != 0 || after.Eaten != 0 || after.FromDish != 0
					|| after.ThirstOutcome != KingdomGrowthThirstOutcome.Sustained
					|| after.HungerOutcome != KingdomGrowthHungerOutcome.Fed) return false;
			}
			else
			{
				GrowthStage stage = (GrowthStage)after.Stage;
				int upkeep = KingdomRules.PolicyUpkeepForElapsed(after.Population,
					after.ElapsedTicks, (KingdomRules.StoresPolicy)after.StoresPolicy, stage);
				long districtUpkeep = (long)upkeep * after.DistrictPercent / 100L;
				int rations = KingdomRules.RationsForElapsed(after.Population, after.ElapsedTicks);
				if (districtUpkeep < 0L || districtUpkeep > int.MaxValue
					|| after.UpkeepRequested != (int)districtUpkeep
					|| after.RequestedWater != after.UpkeepRequested
					|| after.RequestedRations != rations
					|| after.ProvedWater > after.WaterAvailable
					|| after.Eaten > after.RationsAvailable
					|| after.Eaten > after.RequestedRations - after.Foraged) return false;
			}
			if (after.Days != KingdomRules.ElapsedDays(after.ElapsedTicks)) return false;
			bool waterPaid = after.ProvedWater == after.RequestedWater;
			bool foodPaid = after.ProvedRations == after.RequestedRations;
			int dryAfter = waterPaid ? 0 : before.DryStreak + 1;
			int hungerAfter = foodPaid ? 0 : before.HungerStreak + 1;
			if (dryAfter < 0 || hungerAfter < 0) return false;
			KingdomGrowthThirstOutcome thirst = waterPaid
				? KingdomGrowthThirstOutcome.Sustained
				: (KingdomGrowthThirstOutcome)KingdomRules.ResolveThirst(dryAfter,
					(GrowthStage)after.Stage, after.Population);
			KingdomGrowthHungerOutcome hunger = foodPaid
				? KingdomGrowthHungerOutcome.Fed
				: (KingdomGrowthHungerOutcome)KingdomRules.ResolveHunger(hungerAfter,
					(GrowthStage)after.Stage, after.Population);
			KingdomRules.MealVerdict meal = KingdomRules.JudgeMeal(after.RequestedRations,
				after.FromDish, after.Eaten, after.Kitchens > 0, (GrowthStage)after.Stage);
			return after.DryStreak == dryAfter && after.HungerStreak == hungerAfter
				&& after.Withered == (!waterPaid && (before.Withered
					|| thirst == KingdomGrowthThirstOutcome.Withering))
				&& after.Famished == (!foodPaid && (before.Famished
					|| hunger == KingdomGrowthHungerOutcome.Famine))
				&& after.ThirstOutcome == thirst && after.HungerOutcome == hunger
				&& after.LastMeal == meal && after.MealShade == KingdomRules.MealShadeFor(meal)
				&& after.ScrapsAnnounced == (meal == KingdomRules.MealVerdict.Scraps);
		}

		private static bool GrowthScarcityInputsEqual(KingdomGrowthScarcitySnapshot a,
			KingdomGrowthScarcitySnapshot b)
		{
			return a.ElapsedTicks == b.ElapsedTicks && a.Days == b.Days
				&& a.Population == b.Population && a.Stage == b.Stage
				&& a.UpkeepRequested == b.UpkeepRequested
				&& a.WaterAvailable == b.WaterAvailable
				&& a.RationsAvailable == b.RationsAvailable && a.Foraged == b.Foraged
				&& a.Eaten == b.Eaten && a.FromDish == b.FromDish && a.Kitchens == b.Kitchens
				&& string.Equals(a.DishName, b.DishName, StringComparison.Ordinal)
				&& string.Equals(a.DishText, b.DishText, StringComparison.Ordinal)
				&& string.Equals(a.DishStaple, b.DishStaple, StringComparison.Ordinal)
				&& string.Equals(a.DishSource, b.DishSource, StringComparison.Ordinal)
				&& a.RequestedWater == b.RequestedWater && a.ProvedWater == b.ProvedWater
				&& a.RequestedRations == b.RequestedRations
				&& a.ProvedRations == b.ProvedRations && a.StoresPolicy == b.StoresPolicy
				&& a.DistrictPercent == b.DistrictPercent;
		}

		private static int GrowthThirstBite(KingdomGrowthThirstOutcome value)
		{
			return value == KingdomGrowthThirstOutcome.Withering ? 3
				: value == KingdomGrowthThirstOutcome.Emigration ? 2
					: value == KingdomGrowthThirstOutcome.Warned ? 1 : 0;
		}

		private static int GrowthHungerBite(KingdomGrowthHungerOutcome value)
		{
			return value == KingdomGrowthHungerOutcome.Famine ? 3
				: value == KingdomGrowthHungerOutcome.Emigration ? 2
					: value == KingdomGrowthHungerOutcome.Warned ? 1 : 0;
		}

		private static bool GrowthAccountingSnapshotShape(KingdomGrowthAccountingSnapshot x)
		{
			return x != null && x.Fetched >= 0L && x.UpkeepDrawn >= 0L
				&& x.ArrivalCost >= 0L && x.Delivered >= 0L && x.Harvested >= 0L
				&& x.Foraged >= 0L && x.RationsDrawn >= 0L && x.Milled >= 0L
				&& x.HarvestLost >= 0L && x.Plundered >= 0L && x.Arrivals >= 0L
				&& x.Departures >= 0L;
		}

		private static bool GrowthAccountingTransitionShape(KingdomGrowthOperation operation,
			KingdomGrowthAccountingSnapshot before, KingdomGrowthAccountingSnapshot after)
		{
			if (operation == null || before == null || after == null) return false;
			int fetched = 0, upkeep = 0, arrivalCost = 0, delivered = 0, harvested = 0;
			int foraged = 0, rations = 0, milled = 0, harvestLost = 0;
			int plundered = 0, arrivals = 0, departures = 0;
			int quantity;
			switch (operation.Action)
			{
			case KingdomGrowthAction.Heartbeat:
				KingdomGrowthDomainStep scarcity = FindGrowthDomain(operation,
					KingdomGrowthDomainStepKind.Scarcity);
				if (scarcity == null || scarcity.ScarcityAfter == null) return false;
				upkeep = scarcity.ScarcityAfter.ProvedWater;
				foraged = scarcity.ScarcityAfter.Foraged;
				rations = scarcity.ScarcityAfter.Eaten;
				departures = operation.PopulationDelta < 0 ? -operation.PopulationDelta : 0;
				break;
			case KingdomGrowthAction.Fetch:
				if (!GrowthWaterQuantity(operation, KingdomGrowthWaterMutationKind.Fill,
					out fetched)) return false;
				break;
			case KingdomGrowthAction.Mill:
				int ground;
				int stored;
				if (!GrowthRemovedObjectQuantity(operation, false, out ground)
					|| !GrowthAddedObjectQuantity(operation, null, out stored)) return false;
				long made = (long)ground * KingdomRules.PreserveMultiple;
				if (made > int.MaxValue || stored > made) return false;
				milled = Math.Max(0, stored - ground);
				harvestLost = (int)made - stored;
				break;
			case KingdomGrowthAction.Arrival:
				if (operation.ArrivalDisposition != KingdomGrowthArrivalDisposition.Joined)
					return false;
				if (!GrowthWaterQuantity(operation, KingdomGrowthWaterMutationKind.Drain,
					out arrivalCost)) return false;
				arrivals = operation.PopulationDelta;
				break;
			case KingdomGrowthAction.Departure:
				departures = -operation.PopulationDelta;
				break;
			case KingdomGrowthAction.Delivery:
				if (!GrowthAddedObjectQuantity(operation, null, out delivered)) return false;
				break;
			case KingdomGrowthAction.Harvest:
				if (!GrowthAddedObjectQuantity(operation, operation.HarvestCropBlueprint,
					out quantity) || operation.PendingCropDelta < 0
					|| !CheckedAdd(quantity, operation.PendingCropDelta, out harvested)) return false;
				int yield = GrowthHarvestExpectedYield(operation);
				if (yield < harvested) return false;
				harvestLost = yield - harvested;
				break;
			default: return false;
			}
			return GrowthAccountingDelta(before.Fetched, after.Fetched, fetched)
				&& GrowthAccountingDelta(before.UpkeepDrawn, after.UpkeepDrawn, upkeep)
				&& GrowthAccountingDelta(before.ArrivalCost, after.ArrivalCost, arrivalCost)
				&& GrowthAccountingDelta(before.Delivered, after.Delivered, delivered)
				&& GrowthAccountingDelta(before.Harvested, after.Harvested, harvested)
				&& GrowthAccountingDelta(before.Foraged, after.Foraged, foraged)
				&& GrowthAccountingDelta(before.RationsDrawn, after.RationsDrawn, rations)
				&& GrowthAccountingDelta(before.Milled, after.Milled, milled)
				&& GrowthAccountingDelta(before.HarvestLost, after.HarvestLost, harvestLost)
				&& GrowthAccountingDelta(before.Plundered, after.Plundered, plundered)
				&& GrowthAccountingDelta(before.Arrivals, after.Arrivals, arrivals)
				&& GrowthAccountingDelta(before.Departures, after.Departures, departures);
		}

		private static bool GrowthAccountingDelta(int before, int after, int delta)
		{
			int expected;
			return delta >= 0 && CheckedAdd(before, delta, out expected) && after == expected;
		}

		private static bool TryGrowthDomainKind(KingdomGrowthDomainStepKind stepKind,
			KingdomGrowthDomainCallbackKind callbackKind,
			out KingdomLifecycleResourceKind resourceKind)
		{
			resourceKind = KingdomLifecycleResourceKind.None;
			switch (stepKind)
			{
			case KingdomGrowthDomainStepKind.Enrollment:
				if (callbackKind != KingdomGrowthDomainCallbackKind.Enroll) return false;
				resourceKind = KingdomLifecycleResourceKind.OriginRoster; return true;
			case KingdomGrowthDomainStepKind.Roster:
				if (callbackKind != KingdomGrowthDomainCallbackKind.RosterAdd
					&& callbackKind != KingdomGrowthDomainCallbackKind.RosterRemove) return false;
				resourceKind = KingdomLifecycleResourceKind.Roster; return true;
			case KingdomGrowthDomainStepKind.Creed:
				if (callbackKind != KingdomGrowthDomainCallbackKind.CreedSet) return false;
				resourceKind = KingdomLifecycleResourceKind.CreedRoster; return true;
			case KingdomGrowthDomainStepKind.Population:
				if (callbackKind != KingdomGrowthDomainCallbackKind.PopulationAdjust) return false;
				resourceKind = KingdomLifecycleResourceKind.Population; return true;
			case KingdomGrowthDomainStepKind.PendingCrop:
				if (callbackKind != KingdomGrowthDomainCallbackKind.PendingCropSet) return false;
				resourceKind = KingdomLifecycleResourceKind.GrowthPendingCrop; return true;
			case KingdomGrowthDomainStepKind.Field:
				if (callbackKind != KingdomGrowthDomainCallbackKind.FieldSet) return false;
				resourceKind = KingdomLifecycleResourceKind.GrowthField; return true;
			case KingdomGrowthDomainStepKind.Scarcity:
				if (callbackKind != KingdomGrowthDomainCallbackKind.ScarcitySet) return false;
				resourceKind = KingdomLifecycleResourceKind.GrowthScarcity; return true;
			case KingdomGrowthDomainStepKind.Accounting:
				if (callbackKind != KingdomGrowthDomainCallbackKind.AccountingSet) return false;
				resourceKind = KingdomLifecycleResourceKind.GrowthAccounting; return true;
			case KingdomGrowthDomainStepKind.CropRegistry:
				if (callbackKind != KingdomGrowthDomainCallbackKind.CropRegistrySet) return false;
				resourceKind = KingdomLifecycleResourceKind.GrowthCropRegistry; return true;
			case KingdomGrowthDomainStepKind.SubsidenceSchedule:
				if (callbackKind != KingdomGrowthDomainCallbackKind.SubsidenceScheduleSet) return false;
				resourceKind = KingdomLifecycleResourceKind.GrowthSubsidenceSchedule; return true;
			case KingdomGrowthDomainStepKind.PorterJob:
				if (callbackKind != KingdomGrowthDomainCallbackKind.PorterJobSet) return false;
				resourceKind = KingdomLifecycleResourceKind.GrowthPorterJob; return true;
			case KingdomGrowthDomainStepKind.EscrowRelease:
				if (callbackKind != KingdomGrowthDomainCallbackKind.EscrowRelease) return false;
				resourceKind = KingdomLifecycleResourceKind.GrowthEscrowRelease; return true;
			default: return false;
			}
		}

		private static bool GrowthDomainReceiptHashesEmpty(KingdomGrowthDomainStep step)
		{
			return step.ReceiptBeforeGraphHash == null
				&& step.ReceiptAfterGraphHash == null
				&& step.ReceiptBeforeMapHash == null
				&& step.ReceiptAfterMapHash == null;
		}

		private static bool GrowthDomainReceiptBeforeExact(KingdomGrowthDomainStep step)
		{
			return string.Equals(step.ReceiptBeforeGraphHash, step.BeforeGraphHash,
				StringComparison.Ordinal) && string.Equals(step.ReceiptBeforeMapHash,
				step.BeforeMapHash, StringComparison.Ordinal);
		}

		private static bool GrowthDomainReceiptAfterEmpty(KingdomGrowthDomainStep step)
		{
			return step.ReceiptAfterGraphHash == null
				&& step.ReceiptAfterMapHash == null;
		}

		private static bool GrowthDomainReceiptAfterExact(KingdomGrowthDomainStep step)
		{
			return string.Equals(step.ReceiptAfterGraphHash, step.AfterGraphHash,
				StringComparison.Ordinal) && string.Equals(step.ReceiptAfterMapHash,
				step.AfterMapHash, StringComparison.Ordinal);
		}

		private static bool GrowthOutboxShape(KingdomGrowthOperation operation, bool publication)
		{
			if (operation.OutboxEvents == null
				|| operation.OutboxEvents.Count > MaxGrowthOutboxEvents) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			bool haveChronicle = false; int chronicleCount = 0; string chronicleHash = null;
			bool haveOutsider = false; int outsiderCount = 0; string outsiderHash = null;
			bool haveLedger = false; int ledgerCount = 0; string ledgerHash = null;
			for (int i = 0; i < operation.OutboxEvents.Count; i++)
			{
				KingdomGrowthOutboxEvent e = operation.OutboxEvents[i];
				KingdomLifecycleOutbox box = e == null ? null : e.Outbox;
				bool legacyChronicle = e != null && e.LegacySingleRegisterChronicle;
				if (box == null || !ValidName(e.Kind)
					|| !string.Equals(e.EventId, ChildId(operation.Id, "outbox-event", i),
						StringComparison.Ordinal) || !ids.Add(e.EventId)
					|| !string.Equals(box.OperationId, operation.Id, StringComparison.Ordinal)
					|| !string.Equals(box.EventId, e.EventId, StringComparison.Ordinal)
					|| !string.Equals(box.ChronicleReceiptId,
						ChildId(e.EventId, "chronicle", 0), StringComparison.Ordinal)
					|| TooLong(box.Chronicle, MaxTextChars)
					|| TooLong(box.Ledger, MaxTextChars) || TooLong(box.Message, MaxTextChars)
					|| TooLong(box.Deed, MaxTextChars) || TooLong(box.GuestbookLine, MaxTextChars)
					|| !GrowthSinkTextShape(box.Chronicle, box.ChronicleDisposition,
						box.ChronicleState, publication)
					|| !GrowthSinkTextShape(box.Ledger, box.LedgerDisposition,
						box.LedgerState, publication)
					|| !GrowthSinkTextShape(box.Message, box.MessageDisposition,
						box.MessageState, publication)
					|| !GrowthSinkTextShape(box.Deed, box.DeedDisposition,
						box.DeedState, publication)
					|| !GrowthSinkTextShape(box.GuestbookLine, box.GuestbookDisposition,
						box.GuestbookState, publication)
					|| (legacyChronicle && (!operation.LegacyGrowthV1Plan
						|| box.Chronicle == null))
					|| (operation.LegacyGrowthV1Plan && box.Chronicle != null
						&& !legacyChronicle)
					|| (box.Chronicle == null
						? e.ChronicleOfficial != null || e.ChronicleOutsider != null
						: legacyChronicle
							? e.ChronicleOfficial != null || e.ChronicleOutsider != null
							: string.IsNullOrEmpty(e.ChronicleOfficial)
								|| e.ChronicleOfficial.Length >
									KingdomChronicleReceiptRules.MaxEntryChars
								|| string.IsNullOrEmpty(e.ChronicleOutsider)
								|| e.ChronicleOutsider.Length >
									KingdomChronicleReceiptRules.MaxEntryChars)
					|| !GrowthInspectableSinkShape(box.Chronicle, box.ChronicleState,
						e.ChronicleBeforeCount, e.ChronicleBeforeHash,
						e.ChronicleDeclaredAfterCount, e.ChronicleDeclaredAfterHash,
						e.ChronicleObservedCount, e.ChronicleObservedHash, publication,
						legacyChronicle ? -1 : KingdomChronicleReceiptRules.MaxEntries)
					|| (legacyChronicle
						? !GrowthOutsiderReceiptEmpty(e)
						: !GrowthInspectableSinkShape(box.Chronicle, box.ChronicleState,
							e.OutsiderBeforeCount, e.OutsiderBeforeHash,
							e.OutsiderDeclaredAfterCount, e.OutsiderDeclaredAfterHash,
							e.OutsiderObservedCount, e.OutsiderObservedHash, publication,
							KingdomChronicleReceiptRules.MaxEntries))
					|| !GrowthInspectableSinkShape(box.Ledger, box.LedgerState,
						e.LedgerBeforeCount, e.LedgerBeforeHash,
						e.LedgerDeclaredAfterCount, e.LedgerDeclaredAfterHash,
						e.LedgerObservedCount, e.LedgerObservedHash, publication)) return false;
				if (box.Chronicle != null)
				{
					if (haveChronicle && (e.ChronicleBeforeCount != chronicleCount
						|| !string.Equals(e.ChronicleBeforeHash, chronicleHash,
							StringComparison.Ordinal))) return false;
					haveChronicle = true; chronicleCount = e.ChronicleDeclaredAfterCount;
					chronicleHash = e.ChronicleDeclaredAfterHash;
					if (!legacyChronicle)
					{
						if (haveOutsider && (e.OutsiderBeforeCount != outsiderCount
							|| !string.Equals(e.OutsiderBeforeHash, outsiderHash,
								StringComparison.Ordinal))) return false;
						haveOutsider = true;
						outsiderCount = e.OutsiderDeclaredAfterCount;
						outsiderHash = e.OutsiderDeclaredAfterHash;
					}
				}
				if (box.Ledger != null)
				{
					if (haveLedger && (e.LedgerBeforeCount != ledgerCount
						|| !string.Equals(e.LedgerBeforeHash, ledgerHash,
							StringComparison.Ordinal))) return false;
					haveLedger = true; ledgerCount = e.LedgerDeclaredAfterCount;
					ledgerHash = e.LedgerDeclaredAfterHash;
				}
			}
			return true;
		}

		private static bool GrowthInspectableSinkShape(string text,
			KingdomLifecycleSinkState state, int beforeCount, string beforeHash,
			int declaredAfterCount, string declaredAfterHash, int observedCount,
			string observedHash, bool publication, int boundedCount = -1)
		{
			if (!GrowthSinkDeclarationShape(text, beforeCount, beforeHash,
				declaredAfterCount, declaredAfterHash, boundedCount)) return false;
			if (text == null)
				return state == KingdomLifecycleSinkState.Skipped
					&& observedCount == -1 && observedHash == null;
			if (publication || state == KingdomLifecycleSinkState.Pending
				|| state == KingdomLifecycleSinkState.Intent)
				return observedCount == -1 && observedHash == null;
			return state == KingdomLifecycleSinkState.Delivered
				&& observedCount == declaredAfterCount
				&& string.Equals(observedHash, declaredAfterHash, StringComparison.Ordinal);
		}

		private static bool GrowthOutsiderReceiptEmpty(KingdomGrowthOutboxEvent e)
		{
			return e.OutsiderBeforeCount == 0 && e.OutsiderDeclaredAfterCount == 0
				&& e.OutsiderObservedCount == -1 && e.OutsiderBeforeHash == null
				&& e.OutsiderDeclaredAfterHash == null && e.OutsiderObservedHash == null;
		}

		private static bool GrowthSinkTextShape(string text,
			KingdomLifecycleSinkDisposition disposition, KingdomLifecycleSinkState state,
			bool publication)
		{
			return (disposition == KingdomLifecycleSinkDisposition.Skip ? text == null
				: text != null && text.Length > 0)
				&& SinkTextShape(text, disposition, state, publication);
		}

		private static bool GrowthOutboxTerminal(KingdomGrowthOperation operation)
		{
			if (!GrowthOutboxShape(operation, false)) return false;
			for (int i = 0; i < operation.OutboxEvents.Count; i++)
			{
				KingdomLifecycleOutbox box = operation.OutboxEvents[i].Outbox;
				if (!SinkSettled(box.ChronicleState) || !SinkSettled(box.LedgerState)
					|| !SinkSettled(box.MessageState) || !SinkSettled(box.DeedState)
					|| !SinkSettled(box.GuestbookState)
					|| (box.Chronicle == null
						? box.ChronicleState != KingdomLifecycleSinkState.Skipped
						: box.ChronicleState != KingdomLifecycleSinkState.Delivered)
					|| (box.Ledger == null
						? box.LedgerState != KingdomLifecycleSinkState.Skipped
						: box.LedgerState != KingdomLifecycleSinkState.Delivered)) return false;
			}
			return true;
		}

		private static bool GrowthWitnessHash(string value)
		{
			if (value == null || value.Length != 64) return false;
			for (int i = 0; i < value.Length; i++)
				if (!((value[i] >= '0' && value[i] <= '9')
					|| (value[i] >= 'a' && value[i] <= 'f'))) return false;
			return true;
		}

		private static bool GrowthTopologyValid(KingdomLifecycleTopology topology,
			string ownerId, string zoneId, int x, int y)
		{
			return TopologyValid(topology, ownerId, zoneId, x, y)
				&& (topology != KingdomLifecycleTopology.Cell || ownerId == null);
		}

		private static string GrowthWaterReceiptProof(KingdomGrowthOperation operation,
			KingdomGrowthWaterLeg leg, int ordinal)
		{
			return HashId("growth-water-receipt", delegate(BinaryWriter w)
			{
				CanonicalString(w, operation.SettlementId); CanonicalString(w, operation.Id);
				CanonicalString(w, operation.PlanHash); w.Write(ordinal);
				WriteGrowthWaterPlan(w, leg); CanonicalString(w, leg.ReceiptBeforeOwnerGraphHash);
				CanonicalString(w, leg.ReceiptAfterOwnerGraphHash);
				CanonicalString(w, leg.ReceiptBeforePartGraphHash);
				CanonicalString(w, leg.ReceiptAfterPartGraphHash);
					CanonicalString(w, leg.ReceiptBeforeTopologyHash);
					CanonicalString(w, leg.ReceiptAfterTopologyHash);
					CanonicalString(w, leg.ReceiptCallbackContainerId);
					CanonicalString(w, leg.ReceiptCallbackReferenceHash);
					w.Write(leg.ReceiptSameReference);
				});
		}

		private static string GrowthObjectReceiptProof(KingdomGrowthOperation operation,
			KingdomGrowthObjectLeg leg, int ordinal, bool output)
		{
			return HashId("growth-object-receipt", delegate(BinaryWriter w)
			{
				CanonicalString(w, operation.SettlementId); CanonicalString(w, operation.Id);
				CanonicalString(w, operation.PlanHash); w.Write(output); w.Write(ordinal);
				WriteGrowthObjectPlan(w, leg); w.Write(leg.ReceiptBeforeIdMatches);
				w.Write(leg.ReceiptBeforeMarkerMatches); w.Write(leg.ReceiptBeforeCount);
				w.Write(leg.ReceiptAfterIdMatches); w.Write(leg.ReceiptAfterMarkerMatches);
				w.Write(leg.ReceiptAfterCount); CanonicalString(w, leg.ReceiptBeforeOwnerGraphHash);
				CanonicalString(w, leg.ReceiptAfterOwnerGraphHash);
				CanonicalString(w, leg.ReceiptBeforeObjectGraphHash);
				CanonicalString(w, leg.ReceiptAfterObjectGraphHash);
				CanonicalString(w, leg.ReceiptBeforeTopologyHash);
				CanonicalString(w, leg.ReceiptAfterTopologyHash);
					CanonicalString(w, leg.ReceiptCallbackObjectId);
					CanonicalString(w, leg.ReceiptCallbackMarker);
					CanonicalString(w, leg.ReceiptCallbackReferenceHash);
					w.Write(leg.ReceiptSameReference);
				});
		}

		private static string GrowthDomainReceiptProof(KingdomGrowthOperation operation,
			KingdomGrowthDomainStep step, int ordinal)
		{
			return HashId("growth-domain-receipt", delegate(BinaryWriter w)
			{
				CanonicalString(w, operation.SettlementId); CanonicalString(w, operation.Id);
				CanonicalString(w, operation.PlanHash); w.Write(ordinal);
				WriteGrowthDomainPlan(w, step); w.Write(step.ReceiptBeforeValue);
				w.Write(step.ReceiptAfterValue); CanonicalString(w, step.ReceiptBeforeGraphHash);
				CanonicalString(w, step.ReceiptAfterGraphHash);
				CanonicalString(w, step.ReceiptBeforeMapHash);
				CanonicalString(w, step.ReceiptAfterMapHash);
				WriteGrowthCropRowsPlan(w, step.CropRowsAfter);
			});
		}

		private static bool GrowthOperationEvidenceBounded(KingdomGrowthOperation operation)
		{
			if (operation == null) return true;
			if (operation.WaterLegs == null || operation.WaterLegs.Count > MaxWaterLegs
				|| operation.Sources == null || operation.Sources.Count > MaxGrowthSources
				|| operation.Outputs == null || operation.Outputs.Count > MaxGrowthOutputs
				|| operation.DomainSteps == null || operation.DomainSteps.Count > MaxResourceLeases
				|| operation.ClockLease == null || operation.OutboxEvents == null
				|| operation.OutboxEvents.Count > MaxGrowthOutboxEvents
				|| TooLong(operation.Fault, MaxTextChars)
				|| (operation.Phase == KingdomGrowthPhase.Quarantined
					? string.IsNullOrEmpty(operation.Fault) : operation.Fault != null)) return false;
			for (int i = 0; i < operation.WaterLegs.Count; i++) if (operation.WaterLegs[i] == null
				|| operation.WaterLegs[i].Lease == null) return false;
			for (int i = 0; i < operation.Sources.Count; i++) if (operation.Sources[i] == null) return false;
			for (int i = 0; i < operation.Outputs.Count; i++) if (operation.Outputs[i] == null) return false;
			for (int i = 0; i < operation.DomainSteps.Count; i++) if (operation.DomainSteps[i] == null
				|| operation.DomainSteps[i].Lease == null) return false;
			return true;
		}

		private static bool LegacyResourceKindsOnly(KingdomLifecycleBook book)
		{
			if (book == null || book.Resources == null) return false;
			for (int i = 0; i < book.Resources.Count; i++)
				if (book.Resources[i] == null || (byte)book.Resources[i].Kind > 11) return false;
			KingdomLifecycleOperation[] operations = { book.PlainGuest, book.NotableGuest,
				book.Raid, book.Petition };
			for (int i = 0; i < operations.Length; i++)
			{
				KingdomLifecycleOperation operation = operations[i];
				if (operation == null || operation.ResourceLeases == null) continue;
				for (int j = 0; j < operation.ResourceLeases.Count; j++)
					if (operation.ResourceLeases[j] == null ||
						(byte)operation.ResourceLeases[j].Kind > 11) return false;
			}
			return true;
		}

		private static bool HasOpenGrowthOperation(KingdomGrowthBook book)
		{
			if (book == null) return false;
			if (book.HeartbeatOp != null || book.ArrivalOp != null || book.DepartureOp != null
				|| book.DeliveryOp != null || book.FetchOp != null || book.MillOp != null
				|| book.ArrivalCandidate != null) return true;
			if (book.FieldOps != null) for (int i = 0; i < book.FieldOps.Count; i++)
				if (book.FieldOps[i] != null && book.FieldOps[i].Operation != null) return true;
			return false;
		}

		private static KingdomGrowthSlotKind SlotForGrowthAction(KingdomGrowthAction action)
		{
			switch (action)
			{
			case KingdomGrowthAction.Heartbeat: return KingdomGrowthSlotKind.Heartbeat;
			case KingdomGrowthAction.Arrival: return KingdomGrowthSlotKind.Arrival;
			case KingdomGrowthAction.Departure: return KingdomGrowthSlotKind.Departure;
			case KingdomGrowthAction.Delivery: return KingdomGrowthSlotKind.Delivery;
			case KingdomGrowthAction.Fetch: return KingdomGrowthSlotKind.Fetch;
			case KingdomGrowthAction.Mill: return KingdomGrowthSlotKind.Mill;
			case KingdomGrowthAction.Sow:
			case KingdomGrowthAction.Withdraw:
			case KingdomGrowthAction.Ripen:
			case KingdomGrowthAction.Harvest:
			case KingdomGrowthAction.Irrigate: return KingdomGrowthSlotKind.Field;
			default: return KingdomGrowthSlotKind.None;
			}
		}

		private static KingdomGrowthFieldSlot FindGrowthField(KingdomGrowthBook book,
			string fieldId)
		{
			if (book == null || book.FieldOps == null || fieldId == null) return null;
			KingdomGrowthFieldSlot found = null;
			for (int i = 0; i < book.FieldOps.Count; i++)
			{
				KingdomGrowthFieldSlot field = book.FieldOps[i];
				if (field == null || !string.Equals(field.FieldId, fieldId,
					StringComparison.Ordinal)) continue;
				if (found != null) return null;
				found = field;
			}
			return found;
		}

		private static KingdomGrowthOperation GetGrowthOperation(KingdomGrowthBook book,
			KingdomGrowthSlotKind slot, string fieldId)
		{
			if (book == null) return null;
			switch (slot)
			{
			case KingdomGrowthSlotKind.Heartbeat: return book.HeartbeatOp;
			case KingdomGrowthSlotKind.Arrival: return book.ArrivalOp;
			case KingdomGrowthSlotKind.Departure: return book.DepartureOp;
			case KingdomGrowthSlotKind.Delivery: return book.DeliveryOp;
			case KingdomGrowthSlotKind.Fetch: return book.FetchOp;
			case KingdomGrowthSlotKind.Mill: return book.MillOp;
			case KingdomGrowthSlotKind.Field:
				KingdomGrowthFieldSlot field = FindGrowthField(book, fieldId);
				return field == null ? null : field.Operation;
			default: return null;
			}
		}

		private static void SetGrowthOperation(KingdomGrowthBook book,
			KingdomGrowthSlotKind slot, KingdomGrowthFieldSlot field,
			KingdomGrowthOperation operation)
		{
			switch (slot)
			{
			case KingdomGrowthSlotKind.Heartbeat: book.HeartbeatOp = operation; break;
			case KingdomGrowthSlotKind.Arrival: book.ArrivalOp = operation; break;
			case KingdomGrowthSlotKind.Departure: book.DepartureOp = operation; break;
			case KingdomGrowthSlotKind.Delivery: book.DeliveryOp = operation; break;
			case KingdomGrowthSlotKind.Fetch: book.FetchOp = operation; break;
			case KingdomGrowthSlotKind.Mill: book.MillOp = operation; break;
			case KingdomGrowthSlotKind.Field: field.Operation = operation; break;
			}
		}

		private static long GetGrowthNext(KingdomGrowthBook book, KingdomGrowthSlotKind slot,
			KingdomGrowthFieldSlot field)
		{
			switch (slot)
			{
			case KingdomGrowthSlotKind.Heartbeat: return book.HeartbeatNextSequence;
			case KingdomGrowthSlotKind.Arrival: return book.ArrivalNextSequence;
			case KingdomGrowthSlotKind.Departure: return book.DepartureNextSequence;
			case KingdomGrowthSlotKind.Delivery: return book.DeliveryNextSequence;
			case KingdomGrowthSlotKind.Fetch: return book.FetchNextSequence;
			case KingdomGrowthSlotKind.Mill: return book.MillNextSequence;
			case KingdomGrowthSlotKind.Field: return field == null ? long.MaxValue : field.NextSequence;
			default: return long.MaxValue;
			}
		}

		private static void SetGrowthNext(KingdomGrowthBook book, KingdomGrowthSlotKind slot,
			KingdomGrowthFieldSlot field, long value)
		{
			switch (slot)
			{
			case KingdomGrowthSlotKind.Heartbeat: book.HeartbeatNextSequence = value; break;
			case KingdomGrowthSlotKind.Arrival: book.ArrivalNextSequence = value; break;
			case KingdomGrowthSlotKind.Departure: book.DepartureNextSequence = value; break;
			case KingdomGrowthSlotKind.Delivery: book.DeliveryNextSequence = value; break;
			case KingdomGrowthSlotKind.Fetch: book.FetchNextSequence = value; break;
			case KingdomGrowthSlotKind.Mill: book.MillNextSequence = value; break;
			case KingdomGrowthSlotKind.Field: field.NextSequence = value; break;
			}
		}

		private static long GetGrowthRetired(KingdomGrowthBook book, KingdomGrowthSlotKind slot,
			KingdomGrowthFieldSlot field)
		{
			switch (slot)
			{
			case KingdomGrowthSlotKind.Heartbeat: return book.HeartbeatRetiredThrough;
			case KingdomGrowthSlotKind.Arrival: return book.ArrivalRetiredThrough;
			case KingdomGrowthSlotKind.Departure: return book.DepartureRetiredThrough;
			case KingdomGrowthSlotKind.Delivery: return book.DeliveryRetiredThrough;
			case KingdomGrowthSlotKind.Fetch: return book.FetchRetiredThrough;
			case KingdomGrowthSlotKind.Mill: return book.MillRetiredThrough;
			case KingdomGrowthSlotKind.Field: return field == null ? long.MaxValue : field.RetiredThrough;
			default: return long.MaxValue;
			}
		}

		private static void SetGrowthRetired(KingdomGrowthBook book, KingdomGrowthSlotKind slot,
			KingdomGrowthFieldSlot field, long value)
		{
			switch (slot)
			{
			case KingdomGrowthSlotKind.Heartbeat: book.HeartbeatRetiredThrough = value; break;
			case KingdomGrowthSlotKind.Arrival: book.ArrivalRetiredThrough = value; break;
			case KingdomGrowthSlotKind.Departure: book.DepartureRetiredThrough = value; break;
			case KingdomGrowthSlotKind.Delivery: book.DeliveryRetiredThrough = value; break;
			case KingdomGrowthSlotKind.Fetch: book.FetchRetiredThrough = value; break;
			case KingdomGrowthSlotKind.Mill: book.MillRetiredThrough = value; break;
			case KingdomGrowthSlotKind.Field: field.RetiredThrough = value; break;
			}
		}

		private static long GrowthClockValue(KingdomGrowthBook book,
			KingdomGrowthAction action, KingdomGrowthFieldSlot field)
		{
			switch (action)
			{
			case KingdomGrowthAction.Heartbeat: return book.LastHeartbeatTick;
			case KingdomGrowthAction.Arrival: return book.NextArrivalTick;
			case KingdomGrowthAction.Departure: return book.LastDepartureTick;
			case KingdomGrowthAction.Delivery: return book.LastDeliveryTick;
			case KingdomGrowthAction.Fetch: return book.LastFetchTick;
			case KingdomGrowthAction.Mill: return book.LastMillTick;
			case KingdomGrowthAction.Sow:
			case KingdomGrowthAction.Withdraw:
			case KingdomGrowthAction.Ripen:
			case KingdomGrowthAction.Harvest:
			case KingdomGrowthAction.Irrigate: return field == null ? -1L : field.CommitRevision;
			default: return -1L;
			}
		}

		private static string GrowthClockSubject(string settlementId,
			KingdomGrowthSlotKind slot, string fieldId)
		{
			return HashId("growth-clock-subject", delegate(BinaryWriter w)
			{
				CanonicalString(w, settlementId); w.Write((byte)slot); CanonicalString(w, fieldId);
			});
		}

		private static KingdomLifecycleResourceRevision FindGrowthResource(KingdomGrowthBook book,
			string key)
		{
			if (book == null || book.Resources == null || key == null) return null;
			KingdomLifecycleResourceRevision found = null;
			for (int i = 0; i < book.Resources.Count; i++)
			{
				KingdomLifecycleResourceRevision row = book.Resources[i];
				if (row == null || !string.Equals(row.Key, key, StringComparison.Ordinal)) continue;
				if (found != null) return null;
				found = row;
			}
			return found;
		}

		private static bool IsPhysicalResourceKind(KingdomLifecycleResourceKind Kind)
		{
			return Kind == KingdomLifecycleResourceKind.Schedule
				|| Kind == KingdomLifecycleResourceKind.WaterVessel
				|| Kind == KingdomLifecycleResourceKind.Object
				|| Kind == KingdomLifecycleResourceKind.Projection;
		}

		private static bool ValidCount(int Value)
		{
			return Value >= 0 && Value <= MaxPhysicalCount;
		}

		private static bool PristineLifecycleBook(KingdomLifecycleBook book)
		{
			return book != null && book.FormatVersion == CurrentFormatVersion
				&& !book.WireRejected && !book.Quarantined && string.IsNullOrEmpty(book.Fault)
				&& string.IsNullOrEmpty(book.SettlementId) && !book.IdentityBound
				&& string.IsNullOrEmpty(book.IdentityProof) && !book.LegacyIdentity
				&& string.IsNullOrEmpty(book.LegacyMigrationKey)
				&& book.PlainGuestNextSequence == 1L && book.PlainGuestRetiredThrough == 0L
				&& book.NotableGuestNextSequence == 1L && book.NotableGuestRetiredThrough == 0L
				&& book.RaidNextSequence == 1L && book.RaidRetiredThrough == 0L
				&& book.PetitionNextSequence == 1L && book.PetitionRetiredThrough == 0L
				&& book.LocusOption == KingdomLifecycleOptionState.Unknown
				&& book.NotableOption == KingdomLifecycleOptionState.Unknown
				&& book.RaidOption == KingdomLifecycleOptionState.Unknown
				&& book.PetitionOption == KingdomLifecycleOptionState.Unknown
				&& book.LocusOptionTick == 0L && book.NotableOptionTick == 0L
				&& book.RaidOptionTick == 0L && book.PetitionOptionTick == 0L
				&& book.PlainGuest == null && book.NotableGuest == null
				&& book.Raid == null && book.Petition == null
				&& book.Resources != null && book.Resources.Count == 0
				&& book.RecentProofs != null && book.RecentProofs.Count == 0
				&& PristineGrowthBook(book.Growth);
		}

		private static bool CanonicalLifecycleQuarantine(KingdomLifecycleBook book)
		{
			if (book == null || book.FormatVersion != CurrentFormatVersion || book.WireRejected
				|| !book.Quarantined || string.IsNullOrEmpty(book.Fault)
				|| TooLong(book.Fault, MaxTextChars)) return false;
			bool identity = book.IdentityBound
				? ValidRootId(book.SettlementId) && ExactSettlementIdentityProof(book)
				: string.IsNullOrEmpty(book.SettlementId)
					&& string.IsNullOrEmpty(book.IdentityProof) && !book.LegacyIdentity
					&& string.IsNullOrEmpty(book.LegacyMigrationKey);
			return identity && LifecycleBookShape(book);
		}

		private static bool PristineCarryBook(KingdomCarryBook book)
		{
			return book != null && book.FormatVersion == CurrentCarryFormatVersion
				&& !book.WireRejected && !book.Quarantined && string.IsNullOrEmpty(book.Fault)
				&& string.IsNullOrEmpty(book.RealmId) && !book.IdentityBound
				&& string.IsNullOrEmpty(book.IdentityProof) && !book.LegacyIdentity
				&& string.IsNullOrEmpty(book.LegacyMigrationKey)
				&& book.SettlementIds != null && book.SettlementIds.Count == 0
				&& book.NextSequence == 1L && book.RetiredThrough == 0L && book.Open == null
				&& book.Resources != null && book.Resources.Count == 0
				&& book.RecentProofs != null && book.RecentProofs.Count == 0;
		}

		private static string SettlementIdentityProof(string settlementId, bool legacy,
			string migrationKey)
		{
			return HashId("lifecycle-binding", delegate(BinaryWriter w)
			{
				CanonicalString(w, settlementId);
				w.Write(legacy);
				CanonicalString(w, legacy ? migrationKey : null);
			});
		}

		private static bool ExactSettlementIdentityProof(KingdomLifecycleBook book)
		{
			return book != null && book.IdentityBound && ValidRootId(book.SettlementId)
				&& string.Equals(book.IdentityProof, SettlementIdentityProof(book.SettlementId,
					book.LegacyIdentity, book.LegacyMigrationKey), StringComparison.Ordinal);
		}

		private static string CarryIdentityProof(string realmId, List<string> settlementIds,
			bool legacy, string migrationKey)
		{
			return HashId("carry-binding", delegate(BinaryWriter w)
			{
				CanonicalString(w, realmId);
				w.Write(settlementIds == null ? -1 : settlementIds.Count);
				if (settlementIds != null) for (int i = 0; i < settlementIds.Count; i++)
					CanonicalString(w, settlementIds[i]);
				w.Write(legacy);
				CanonicalString(w, legacy ? migrationKey : null);
			});
		}

		private static string RealmTopologyDigest(string realmId, List<string> settlementIds)
		{
			return HashId("carry-realm-topology", delegate(BinaryWriter w)
			{
				CanonicalString(w, realmId);
				w.Write(settlementIds == null ? -1 : settlementIds.Count);
				if (settlementIds != null) for (int i = 0; i < settlementIds.Count; i++)
					CanonicalString(w, settlementIds[i]);
			});
		}

		private static bool ExactCarryIdentityProof(KingdomCarryBook book)
		{
			return book != null && book.IdentityBound && ValidRootId(book.RealmId)
				&& FrozenSettlementSetValid(book.SettlementIds)
				&& string.Equals(book.IdentityProof, CarryIdentityProof(book.RealmId,
					book.SettlementIds, book.LegacyIdentity, book.LegacyMigrationKey),
					StringComparison.Ordinal);
		}

		private static bool TryFrozenSettlementSet(ICollection<string> source,
			out List<string> frozen)
		{
			frozen = null;
			try
			{
				if (source == null || source.Count <= 0 || source.Count > MaxSettlementIds)
					return false;
				List<string> value = new List<string>(source.Count);
				HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
				foreach (string id in source)
					if (!ValidRootId(id) || !seen.Add(id)) return false; else value.Add(id);
				if (value.Count != source.Count) return false;
				value.Sort(StringComparer.Ordinal);
				frozen = value;
				return true;
			}
			catch (Exception)
			{
				frozen = null;
				return false;
			}
		}

		private static bool ExistingIdsExclude(ICollection<string> source, string exactId)
		{
			if (source == null) return false;
			try
			{
				if (source.Count > MaxLifecycleCollisionIds) return false;
				HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
				int count = 0;
				foreach (string id in source)
				{
					count++;
					if (count > MaxLifecycleCollisionIds || !ValidRootId(id) || !seen.Add(id)
						|| string.Equals(id, exactId, StringComparison.Ordinal)) return false;
				}
				return count == source.Count;
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static bool FrozenSettlementSetValid(List<string> ids)
		{
			if (ids == null || ids.Count <= 0 || ids.Count > MaxSettlementIds) return false;
			for (int i = 0; i < ids.Count; i++)
				if (!ValidRootId(ids[i]) || (i > 0 && string.CompareOrdinal(ids[i - 1], ids[i]) >= 0))
					return false;
			return true;
		}

		private static bool CarrySettlementSetShape(KingdomCarryBook book)
		{
			return book != null && FrozenSettlementSetValid(book.SettlementIds);
		}

		private static bool SettlementMember(KingdomCarryBook book, string id)
		{
			if (book == null || !ValidRootId(id) || book.SettlementIds == null) return false;
			for (int i = 0; i < book.SettlementIds.Count; i++)
				if (string.Equals(book.SettlementIds[i], id, StringComparison.Ordinal)) return true;
			return false;
		}

		private static bool ExactStringList(List<string> a, List<string> b)
		{
			if (a == null || b == null || a.Count != b.Count) return false;
			for (int i = 0; i < a.Count; i++)
				if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool ValidRootId(string Value)
		{
			return !string.IsNullOrEmpty(Value) && !TooLong(Value, MaxIdChars);
		}

		private static bool ValidName(string Value)
		{
			return !string.IsNullOrEmpty(Value) && !TooLong(Value, MaxNameChars);
		}

		private static bool TooLong(string Value, int Limit)
		{
			if (Value == null) return false;
			if (Limit < 0 || Value.Length > Limit) return true;
			try
			{
				return StrictUtf8.GetByteCount(Value) > (long)Limit * 4L;
			}
			catch (EncoderFallbackException) { return true; }
		}

		private static string SafeFault(string Value)
		{
			return Value != null && Value.Length <= MaxTextChars ? Value
				: "lifecycle authority quarantined";
		}

		private static void Deny(KingdomLifecycleBook Book, string Fault)
		{
			Book.Quarantined = true;
			Book.Fault = Fault;
		}

		private static void Deny(KingdomCarryBook Book, string Fault)
		{
			Book.Quarantined = true;
			Book.Fault = Fault;
		}

		public static bool CheckedAdd(long A, long B, out long Result)
		{
			if ((B > 0L && A > long.MaxValue - B) || (B < 0L && A < long.MinValue - B))
			{
				Result = A;
				return false;
			}
			Result = A + B;
			return true;
		}

		public static bool CheckedAdd(int A, int B, out int Result)
		{
			long value = (long)A + B;
			if (value < int.MinValue || value > int.MaxValue)
			{
				Result = A;
				return false;
			}
			Result = (int)value;
			return true;
		}

		public static bool ExactCountTransition(int Before, int After, int Removed,
			bool SameObject, bool SameContext)
		{
			return Before > 0 && Removed > 0 && Removed <= Before
				&& After == Before - Removed && SameObject && SameContext;
		}

		private static bool CheckedAccumulate(long[] Values, int Index, long Delta)
		{
			if (Values == null || Index < 0 || Index >= Values.Length || Delta < 0L) return false;
			long value;
			if (!CheckedAdd(Values[Index], Delta, out value)) return false;
			Values[Index] = value;
			return true;
		}

		private static long SumSix(int a, int b, int c, int d, int e, int f)
		{
			if (a < 0 || b < 0 || c < 0 || d < 0 || e < 0 || f < 0) return -1L;
			return (long)a + b + c + d + e + f;
		}

		private static string HashId(string Namespace, Action<BinaryWriter> WritePayload)
		{
			if (string.IsNullOrEmpty(Namespace) || WritePayload == null) return null;
			try
			{
				byte[] bytes;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					CanonicalString(writer, "taf:kingdom-lifecycle:v3");
					CanonicalString(writer, Namespace);
					WritePayload(writer);
					writer.Flush();
					bytes = stream.ToArray();
				}
				byte[] digest;
				using (SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(bytes);
				StringBuilder hex = new StringBuilder(64);
				for (int i = 0; i < digest.Length; i++)
					hex.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
				return "taf:" + Namespace + ":" + hex;
			}
			catch (Exception)
			{
				return null;
			}
		}

		private static void CanonicalString(BinaryWriter Writer, string Value)
		{
			if (Value == null)
			{
				Writer.Write(-1);
				return;
			}
			int byteCount = StrictUtf8.GetByteCount(Value);
			if (byteCount > MaxTextBytes)
				throw new InvalidDataException("bounded canonical string exceeded");
			byte[] bytes = StrictUtf8.GetBytes(Value);
			Writer.Write(byteCount);
			Writer.Write(bytes);
		}

		private static bool ValidGeneratedId(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > MaxIdChars || !Value.StartsWith("taf:",
				StringComparison.Ordinal)) return false;
			int colon = Value.LastIndexOf(':');
			if (colon <= 4 || Value.Length - colon - 1 != 64) return false;
			for (int i = colon + 1; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9') || (Value[i] >= 'a' && Value[i] <= 'f')))
					return false;
			return true;
		}

		private static bool ValidHashNamespace(string Value, string Namespace)
		{
			return ValidGeneratedId(Value) && Value.StartsWith("taf:" + Namespace + ":",
				StringComparison.Ordinal);
		}
	}
}
