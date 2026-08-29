using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomPolityConsentedEscrowRuntime
	{
		internal static bool TryBegin(KingdomSystem System,
			KingdomPolityIncidentRecord Plan, GameObject Item, long Tick,
			string ConsentFactId, out string Failure)
		{
			Failure = null;
			if (!TryCaptureNew(System, Plan, Item,
				out KingdomPolityGroundEscrowSnapshot snapshot, out Failure)) return false;
			if (!KingdomPolityEndpointRuntime.TryPrepareCurrentEndpointEscrow(System,
				Plan.IncidentPlanId, snapshot.StakeRef, snapshot.ObjectId, snapshot.Digest,
				Tick, ConsentFactId, out string projectionId, out Failure)) return false;
			KingdomPolityProjectionReceipt projection = KingdomPolityAuthority.Projection(
				System.PolityLedger, projectionId);
			if (projection == null || projection.PriorDigest != snapshot.Digest ||
				!TryCaptureNew(System, FindPlan(System.PolityLedger, Plan.IncidentPlanId), Item,
					out KingdomPolityGroundEscrowSnapshot recheck, out Failure) ||
				recheck.Digest != snapshot.Digest)
			{
				string cause = Failure ?? "collateral changed before lease";
				if (!TryCancelUnleased(System, projectionId, Tick, out string rollback))
					cause += "; cancellation failed: " + rollback;
				Failure = cause; return false;
			}
			if (!TryAttachMarker(snapshot, projection, out Failure))
			{
				string cause = Failure;
				if (!TryCancelUnleased(System, projectionId, Tick, out string rollback))
					cause += "; cancellation failed: " + rollback;
				Failure = cause; return false;
			}
			return Continue(System, projectionId, Item, Tick, out Failure);
		}

		internal static bool TryRecover(KingdomSystem System, long Tick, out string Failure)
		{
			Failure = null;
			if (System?.PolityLedger == null || Tick < 0L || The.Player?.CurrentZone == null)
				return true;
			Zone zone = The.Player.CurrentZone;
			if (!System.ClaimedZones.Contains(zone.ZoneID)) return true;
			for (int i = 0; i < System.PolityLedger.Projections.Count; i++)
			{
				KingdomPolityProjectionReceipt p = System.PolityLedger.Projections[i];
				if (p.Kind != KingdomPolityProjectionKind.ConsentedEscrow ||
					p.ZoneId != zone.ZoneID || p.ObjectIds.Count != 1) continue;
				if (!TryFindExactRoot(zone, p.ObjectIds[0], out GameObject item))
				{
					if (p.Phase == KingdomPolityProjectionPhase.Cleaned ||
						p.Phase == KingdomPolityProjectionPhase.Cancelled) continue;
					Failure = "consented escrow object is absent from its exact loaded ground";
					return false;
				}
				r_KingdomPolityEscrow marker = item.GetPart<r_KingdomPolityEscrow>();
				if (p.Phase == KingdomPolityProjectionPhase.Cleaned ||
					p.Phase == KingdomPolityProjectionPhase.Cancelled)
				{
					if (marker != null && !TryRemoveMarker(System, p, item, out Failure))
						return false;
					continue;
				}
				if (marker == null)
				{
					if (p.Phase != KingdomPolityProjectionPhase.Prepared)
					{
						Failure = "committed escrow lost its exact visible lease"; return false;
					}
					if (!TryCancelUnleased(System, p.ProjectionId, Tick, out Failure)) return false;
					continue;
				}
				if (!Continue(System, p.ProjectionId, item, Tick, out Failure)) return false;
			}
			return true;
		}

		private static bool Continue(KingdomSystem System, string ProjectionId,
			GameObject Item, long Tick, out string Failure)
		{
			Failure = null;
			KingdomPolityProjectionReceipt projection = KingdomPolityAuthority.Projection(
				System.PolityLedger, ProjectionId);
			if (!TryReproveMarker(System, projection, Item,
				out KingdomPolityGroundEscrowSnapshot _, out Failure)) return false;
			if (projection.Phase == KingdomPolityProjectionPhase.Prepared)
			{
				if (!KingdomPolityConflictRules.TryCreateEscrowCustodyProof(
					System.PolityLedger, ProjectionId, Tick,
					out KingdomPolityEscrowCustodyProof proof, out Failure) ||
					!KingdomPolityConflictRules.TryCommitConsentedEscrowCustody(
						System.PolityLedger, System.PolityLedger.Revision, proof,
						out KingdomPolityPublicationResult _, out Failure)) return false;
			}
			projection = KingdomPolityAuthority.Projection(System.PolityLedger, ProjectionId);
			KingdomPolityIncidentRecord plan = FindPlan(System.PolityLedger,
				projection.SourceRef);
			if (plan?.Conclusion == null && !KingdomPolityConflictRules.TryConcludeConsentedEscrow(
				System.PolityLedger, System.PolityLedger.Revision, ProjectionId, Tick,
				out KingdomPolityPublicationResult _, out Failure)) return false;
			projection = KingdomPolityAuthority.Projection(System.PolityLedger, ProjectionId);
			if (!TryReproveMarker(System, projection, Item,
				out KingdomPolityGroundEscrowSnapshot _, out Failure) ||
				!KingdomPolityConflictRules.TryCreateEscrowRefundProof(System.PolityLedger,
					ProjectionId, Tick, out KingdomPolityEscrowRefundProof refund, out Failure) ||
				!KingdomPolityConflictRules.TryReleaseConsentedEscrow(System.PolityLedger,
					System.PolityLedger.Revision, refund,
					out KingdomPolityPublicationResult _, out Failure)) return false;
			projection = KingdomPolityAuthority.Projection(System.PolityLedger, ProjectionId);
			return TryRemoveMarker(System, projection, Item, out Failure);
		}

		private static bool TryCancelUnleased(KingdomSystem System, string ProjectionId,
			long Tick, out string Failure)
		{
			Failure = null;
			if (string.IsNullOrEmpty(ProjectionId))
			{
				Failure = "projection identity is absent"; return false;
			}
			if (!KingdomPolityConflictRules.TryCreateEscrowRefundProof(System.PolityLedger,
				ProjectionId, Tick, out KingdomPolityEscrowRefundProof refund, out Failure))
				return false;
			return KingdomPolityConflictRules.TryReleaseConsentedEscrow(System.PolityLedger,
				System.PolityLedger.Revision, refund, out KingdomPolityPublicationResult _,
				out Failure);
		}
	}
}
