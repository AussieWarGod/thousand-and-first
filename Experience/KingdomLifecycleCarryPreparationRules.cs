using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
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

		/// <summary>Prepares the only carry authority new runtime work may publish. The caller
		/// freezes common route/destination fields, whole-stack sources and same-identity outputs,
		/// then calls <see cref="FreezeExactCarryManifest"/> before publication.</summary>
		public static KingdomCarryOperation PrepareExactCarry(KingdomCarryBook Book, long Tick)
		{
			KingdomCarryOperation operation = PrepareCarry(Book, Tick);
			if (operation == null) return null;
			operation.AuthorityKind = KingdomCarryAuthorityKind.ExactManifest;
			operation.ManifestVersion = CurrentCarryManifestVersion;
			operation.JobIds = new List<int>();
			operation.TripIds = new List<int>();
			return operation;
		}

		/// <summary>One exact manifest atom is one whole GameObject/stack. Partial-stack plans are
		/// refused because Qud can only split them by minting another object identity.</summary>
		public static KingdomCarrySource PrepareExactCarrySource(KingdomCarryOperation Operation,
			int SourceOrdinal, string ObjectId, string Blueprint,
			KingdomLifecycleTopology Topology, string OwnerId, string ZoneId,
			int X, int Y, int WholeCount)
		{
			if (Operation == null || Operation.AuthorityKind != KingdomCarryAuthorityKind.ExactManifest
				|| Operation.Phase != KingdomLifecyclePhase.Prepared
				|| !string.IsNullOrEmpty(Operation.ManifestDigest)
				|| Operation.Sources == null || SourceOrdinal != Operation.Sources.Count)
				return null;
			if (SourceOrdinal < 0 || SourceOrdinal >= MaxCarrySources
				|| !ValidRootId(ObjectId) || !ValidName(Blueprint)
				|| !TopologyValid(Topology, OwnerId, ZoneId, X, Y)
				|| WholeCount <= 0 || WholeCount > MaxPhysicalCount) return null;
			return new KingdomCarrySource
			{
				OperationId = Operation.Id,
				SourceEventId = ChildId(Operation.Id, "source", SourceOrdinal),
				ObjectId = ObjectId, Blueprint = Blueprint, Topology = Topology,
				OwnerId = OwnerId, ZoneId = ZoneId, X = X, Y = Y, Material = -1,
				OriginalCount = WholeCount, PlannedCount = WholeCount,
				UnitBefore = WholeCount, UnitAfter = WholeCount,
				UnitEventId = ChildId(Operation.Id,
					"source-unit-" + SourceOrdinal.ToString(CultureInfo.InvariantCulture), 0),
				UnitState = KingdomLifecyclePhysicalState.Prepared,
				ReceiptId = ChildId(Operation.Id, "source-receipt-" +
					SourceOrdinal.ToString(CultureInfo.InvariantCulture), 0),
				ReceiptTopologyId = TopologyId(Topology, OwnerId, ZoneId, X, Y),
				ReceiptState = KingdomLifecyclePhysicalState.Prepared,
				State = KingdomLifecyclePhysicalState.Prepared,
				CurrentTopology = Topology, CurrentOwnerId = OwnerId,
				CurrentZoneId = ZoneId, CurrentX = X, CurrentY = Y,
				PendingX = -1, PendingY = -1
			};
		}

		/// <summary>Freezes the destination intent for one exact source. Output identity and count
		/// deliberately equal the source; this is movement, never projection.</summary>
		public static KingdomLifecycleProjection PrepareExactCarryOutput(
			KingdomCarryOperation Operation, int OutputOrdinal, KingdomCarrySource Source,
			KingdomLifecycleTopology Topology, string OwnerId, string ZoneId, int X, int Y)
		{
			if (Operation == null || Operation.AuthorityKind != KingdomCarryAuthorityKind.ExactManifest
				|| Operation.Phase != KingdomLifecyclePhase.Prepared
				|| !string.IsNullOrEmpty(Operation.ManifestDigest)
				|| Operation.Outputs == null || OutputOrdinal != Operation.Outputs.Count
				|| Source == null || Source.OperationId != Operation.Id) return null;
			if (OutputOrdinal < 0 || OutputOrdinal >= MaxCarryOutputs
				|| !TopologyValid(Topology, OwnerId, ZoneId, X, Y)) return null;
			return new KingdomLifecycleProjection
			{
				OperationId = Operation.Id,
				EventId = ChildId(Operation.Id, "projection", OutputOrdinal),
				ObjectId = Source.ObjectId,
				Marker = ChildId(Operation.Id, "marker", OutputOrdinal),
				Blueprint = Source.Blueprint, Topology = Topology, OwnerId = OwnerId,
				ZoneId = ZoneId, X = X, Y = Y, Material = -1,
				Count = Source.PlannedCount, NoStack = true,
				State = KingdomLifecyclePhysicalState.Prepared,
				ReceiptId = ChildId(Operation.Id, "output-receipt", OutputOrdinal),
				ReceiptTopologyId = TopologyId(Topology, OwnerId, ZoneId, X, Y),
				ReceiptState = KingdomLifecyclePhysicalState.Prepared
			};
		}

		/// <summary>Freezes the exact consumed sign and the central job/trip keys before either
		/// authority is published. Collections are copied and must already be canonical ascending
		/// unique ids; routing order remains owned by the central rows.</summary>
		public static bool FreezeExactCarryManifest(KingdomCarryOperation Operation,
			string SignObjectId, string SignBlueprint, KingdomLifecycleTopology SignTopology,
			string SignOwnerId, string SignZoneId, int SignX, int SignY, int SignCount,
			ICollection<int> JobIds, ICollection<int> TripIds)
		{
			if (Operation == null || Operation.AuthorityKind != KingdomCarryAuthorityKind.ExactManifest
				|| Operation.Phase != KingdomLifecyclePhase.Prepared
				|| Operation.ManifestVersion != CurrentCarryManifestVersion
				|| Operation.ManifestRevision != 0L || !string.IsNullOrEmpty(Operation.ManifestDigest)
				|| !ValidRootId(SignObjectId) || !ValidName(SignBlueprint) || SignCount <= 0
				|| SignCount > MaxPhysicalCount
				|| !TopologyValid(SignTopology, SignOwnerId, SignZoneId, SignX, SignY)
				|| Operation.Sources == null || Operation.Outputs == null
				|| Operation.Sources.Count == 0
				|| Operation.Sources.Count != Operation.Outputs.Count) return false;
			List<int> jobs;
			List<int> trips;
			if (!TryFrozenPositiveIds(JobIds, MaxCarryJobIds, out jobs)
				|| !TryFrozenPositiveIds(TripIds, MaxCarryTripIds, out trips)
				|| jobs.Count == 0 || trips.Count == 0) return false;
			for (int i = 0; i < Operation.Sources.Count; i++)
			{
				KingdomCarrySource source = Operation.Sources[i];
				KingdomLifecycleProjection output = Operation.Outputs[i];
				if (!ExactManifestSourcePrepared(source, Operation, i)
					|| output == null || !string.Equals(output.ObjectId, source.ObjectId,
						StringComparison.Ordinal)
					|| !string.Equals(output.Blueprint, source.Blueprint, StringComparison.Ordinal)
					|| output.Material != source.Material || output.Count != source.PlannedCount)
					return false;
			}
			Operation.SignObjectId = SignObjectId;
			Operation.SignBlueprint = SignBlueprint;
			Operation.SignTopology = SignTopology;
			Operation.SignOwnerId = SignOwnerId;
			Operation.SignZoneId = SignZoneId;
			Operation.SignX = SignX;
			Operation.SignY = SignY;
			Operation.SignCount = SignCount;
			Operation.SignReceiptId = ChildId(Operation.Id, "sign-receipt", 0);
			Operation.SignReceiptState = KingdomLifecyclePhysicalState.Prepared;
			Operation.JobIds = jobs;
			Operation.TripIds = trips;
			string digest;
			if (!TryCarryManifestDigest(Operation, out digest)) return false;
			Operation.ManifestDigest = digest;
			return true;
		}

	}
}
