using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

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
		public const int CurrentFormatVersion = 5;
		public const int MaxRecentProofs = 64;
		public const int MaxWaterLegs = 24;
		public const int MaxProjections = 64;
		public const int MaxResourceLeases = 32;
		public const int MaxResourceRows = 128;
		public const int MaxCarrySources = 64;
		public const int MaxCarryOutputs = 64;
		public const int MaxSettlementIds = 64;
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
				&& Book.FormatVersion == CurrentFormatVersion && ValidRootId(Book.RealmId)
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
			if (!ExistingIdsExclude(ExistingIds, ExactId) || !PristineLifecycleBook(Book))
				return false;
			Book.SettlementId = ExactId;
			Book.LegacyIdentity = LegacyMigration;
			Book.LegacyMigrationKey = LegacyMigration ? MigrationKey : null;
			Book.IdentityBound = true;
			Book.IdentityProof = SettlementIdentityProof(Book.SettlementId,
				Book.LegacyIdentity, Book.LegacyMigrationKey);
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
				&& ActiveResourcesValid(book);
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
			if (Book.FormatVersion != CurrentFormatVersion)
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
				&& KnownResourceKind(lease.Kind) && ValidRootId(lease.ScopeId)
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
			return row != null && KnownResourceKind(row.Kind) && ValidRootId(row.ScopeId)
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
				return string.IsNullOrEmpty(OwnerId) && X >= 0 && X <= MaxCoordinate
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
				if (string.IsNullOrEmpty(row.ActiveOperationId)) continue;
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
				if (string.IsNullOrEmpty(row.ActiveOperationId)) continue;
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

		private static bool IsDomainResourceKind(KingdomLifecycleResourceKind Kind)
		{
			return KnownResourceKind(Kind)
				&& Kind != KingdomLifecycleResourceKind.Schedule
				&& Kind != KingdomLifecycleResourceKind.WaterVessel
				&& Kind != KingdomLifecycleResourceKind.Object
				&& Kind != KingdomLifecycleResourceKind.Projection;
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
				&& book.RecentProofs != null && book.RecentProofs.Count == 0;
		}

		private static bool PristineCarryBook(KingdomCarryBook book)
		{
			return book != null && book.FormatVersion == CurrentFormatVersion
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
				if (source.Count > MaxSettlementIds) return false;
				HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
				int count = 0;
				foreach (string id in source)
				{
					count++;
					if (count > MaxSettlementIds || !ValidRootId(id) || !seen.Add(id)
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
			return !string.IsNullOrEmpty(Value) && Value.Length <= MaxIdChars;
		}

		private static bool ValidName(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= MaxNameChars;
		}

		private static bool TooLong(string Value, int Limit)
		{
			return Value != null && Value.Length > Limit;
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
