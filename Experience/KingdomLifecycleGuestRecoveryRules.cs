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
		internal static partial class GuestRuntimeAdapter
		{
			internal static bool RecoverProjectionIntent(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, KingdomLifecycleProjection projection,
				bool exactPresent, bool exactAbsent)
			{
				if (!ExactOperationAuthority(book, operation) || projection == null
					|| operation.Phase != KingdomLifecyclePhase.ProjectionIntent
					|| projection.State != KingdomLifecyclePhysicalState.Intent
					|| exactPresent == exactAbsent) return false;
				string topology = TopologyId(projection.Topology, projection.OwnerId,
					projection.ZoneId, projection.X, projection.Y);
				KingdomLifecycleResourceLease lease = FindLease(operation, ResourceKey(
					KingdomLifecycleResourceKind.Projection, topology, projection.ObjectId));
				KingdomLifecycleResourceRevision row = lease == null ? null : FindResource(book, lease.Key);
				if (lease == null || row == null || lease.State != KingdomLifecycleLeaseState.Intent)
					return false;
				if (exactAbsent)
				{
					if (row.Revision != lease.BeforeRevision
						|| string.Equals(row.LastOperationId, operation.Id, StringComparison.Ordinal))
						return false;
					lease.State = KingdomLifecycleLeaseState.Prepared;
					projection.State = KingdomLifecyclePhysicalState.Prepared;
					return ExactOperationAuthority(book, operation);
				}
				int spawned;
				if (!CheckedAdd(operation.Spawned, projection.Count, out spawned)
					|| !CommitLeaseWitnessCore(book, operation, lease, row, lease.After)) return false;
				projection.State = KingdomLifecyclePhysicalState.Proved;
				operation.Spawned = spawned;
				return ExactOperationAuthority(book, operation);
			}

			internal static bool ResetWaterIntent(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, KingdomLifecycleWaterLeg leg, long before)
			{
				KingdomLifecycleResourceLease lease = leg == null ? null : FindLease(operation, leg.LeaseKey);
				KingdomLifecycleResourceRevision row = lease == null ? null : FindResource(book, lease.Key);
				if (!ExactOperationAuthority(book, operation)
					|| operation.Phase != KingdomLifecyclePhase.WaterIntent || leg == null
					|| leg.State != KingdomLifecyclePhysicalState.Intent
					|| leg.ReceiptState != KingdomLifecyclePhysicalState.Intent || before != leg.Before
					|| lease == null || lease.State != KingdomLifecycleLeaseState.Intent || row == null
					|| row.Revision != lease.BeforeRevision
					|| string.Equals(row.LastOperationId, operation.Id, StringComparison.Ordinal)) return false;
				lease.State = KingdomLifecycleLeaseState.Prepared;
				leg.State = KingdomLifecyclePhysicalState.Prepared;
				leg.ReceiptState = KingdomLifecyclePhysicalState.Prepared;
				return ExactOperationAuthority(book, operation);
			}

			internal static bool RecoverWaterIntent(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, KingdomLifecycleResourceLease lease,
				KingdomLifecycleWaterLeg leg, long after)
			{
				if (!ExactOperationAuthority(book, operation)
					|| operation.Phase != KingdomLifecyclePhase.WaterIntent || lease == null
					|| leg == null || leg.State != KingdomLifecyclePhysicalState.Intent
					|| leg.ReceiptState != KingdomLifecyclePhysicalState.Intent
					|| after != leg.After || lease.State != KingdomLifecycleLeaseState.Intent
					|| !ConfirmWaterLeaseCore(book, lease, leg, after)) return false;
				leg.ReceiptAfterMatches = 1;
				leg.ReceiptSameReference = true;
				leg.ReceiptProofId = WaterReceiptProof(operation, lease, leg);
				leg.ReceiptState = KingdomLifecyclePhysicalState.Proved;
				return ExactWaterReceipt(operation, lease, leg);
			}
		}
	}
}
