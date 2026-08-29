using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityConflictRules
	{
		internal static bool TryConcludeConsentedEscrow(KingdomPolityLedger Ledger,
			long ExpectedRevision, string ProjectionId, long Tick,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || Tick < 0L)
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "escrow conclusion tick is invalid", out Failure);
			KingdomPolityProjectionReceipt projection = KingdomPolityAuthority.Projection(
				Ledger, ProjectionId);
			KingdomPolityIncidentRecord plan = projection == null ? null :
				FindPlan(Ledger, projection.SourceRef);
			KingdomPolityRouteRecord stake = ExactEscrowStake(Ledger, plan);
			string conclusionId = projection == null ? null : KingdomPolityRules.ActivationId(
				"taf:conclusion:consented-escrow:v1:", "polity-consented-escrow-result-v1",
				projection.ProjectionId, projection.AppliedDigest);
			if (plan?.Conclusion != null)
			{
				if (plan.Conclusion.ResolutionKind !=
					KingdomPolityResolutionKind.ConsentedEscrow ||
					plan.Conclusion.ConclusionId != conclusionId)
					return KingdomPolityAuthority.Refuse(Result,
						"clash already carries another conclusion", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (projection == null || projection.Kind !=
				KingdomPolityProjectionKind.ConsentedEscrow || projection.Phase !=
					KingdomPolityProjectionPhase.Committed || plan == null ||
				plan.Intervention?.Choice !=
					KingdomPolityInterventionChoice.ConsentAbstractResolution || stake == null ||
				(stake.Phase != KingdomPolityRoutePhase.AvailableToWitness &&
				 stake.Phase != KingdomPolityRoutePhase.ConfrontationAvailable) ||
				Tick < projection.CommittedTick)
				return KingdomPolityAuthority.Refuse(Result,
					"consented conclusion lacks exact committed custody or route stake",
					out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityIncidentRecord changed = FindPlan(candidate, plan.IncidentPlanId);
			KingdomPolityRouteRecord changedStake = KingdomPolityAuthority.Route(candidate,
				stake.RouteId);
			string escrowReceipt = KingdomPolityRules.ActivationId(
				"taf:receipt:consented-escrow:v1:", "polity-consented-escrow-receipt-v1",
				projection.ProjectionId, projection.AppliedDigest);
			string snapshotReceipt = KingdomPolityRules.ActivationId(
				"taf:receipt:consented-snapshot:v1:", "polity-consented-snapshot-v1",
				projection.ProjectionId, projection.PriorDigest, projection.ObjectIds[0]);
			string stakeReceipt = KingdomPolityRules.ActivationId(
				"taf:receipt:consented-stake:v1:", "polity-consented-route-stake-v1",
				projection.ProjectionId, stake.RouteId);
			changed.Conclusion = new KingdomPolityIncidentConclusion
			{
				ConclusionId = conclusionId,
				ResolutionKind = KingdomPolityResolutionKind.ConsentedEscrow,
				CommitTick = Tick,
				SystemicDeltas = new List<KingdomPolitySystemicDelta>
				{
					new KingdomPolitySystemicDelta
					{
						Kind = KingdomPolitySystemicDeltaKind.ReservedStake,
						TargetId = stake.RouteId, Amount = -1, ReceiptId = stakeReceipt
					}
				},
				ConsentReceiptId = changed.Intervention.ReceiptId,
				EscrowReceiptId = escrowReceipt, SnapshotReceiptId = snapshotReceipt
			};
			changed.Aftermath = ConsentedAftermath(changed, Tick);
			changed.Conclusion.ReceiptRefs = new List<string> { changed.Intervention.ReceiptId,
				escrowReceipt, snapshotReceipt, stakeReceipt, changed.Aftermath.ReceiptId };
			changed.Conclusion.ReceiptRefs.Sort(System.StringComparer.Ordinal);
			KingdomPolityClashRules.ResolveOpenClashGrievances(candidate, changed,
				conclusionId);
			KingdomPolityClashRules.EndWitnessedFronts(candidate, changed, conclusionId);
			changedStake.Phase = KingdomPolityRoutePhase.Blocked;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		private static KingdomPolityAftermathRecord ConsentedAftermath(
			KingdomPolityIncidentRecord Plan, long Tick)
		{
			string id = KingdomPolityRules.ActivationId("taf:aftermath:witnessed:v1:",
				"polity-consented-aftermath-id-v1", Plan.IncidentPlanId,
				Plan.Conclusion.ConclusionId);
			string receipt = KingdomPolityRules.ActivationId("taf:receipt:aftermath:v1:",
				"polity-consented-aftermath-receipt-v1", id,
				Plan.Intervention.ProofDigest);
			KingdomPolityAftermathRecord result = new KingdomPolityAftermathRecord
			{
				AftermathId = id, IncidentPlanId = Plan.IncidentPlanId,
				ConclusionId = Plan.Conclusion.ConclusionId,
				Kind = KingdomPolityAftermathKind.ConsentedResolution,
				SurfaceRef = Plan.Intervention.SurfaceRef, ZoneId = Plan.Intervention.ZoneId,
				CommitTick = Tick, ObservedFactId = Plan.Intervention.ObservedFactId,
				InterventionId = Plan.Intervention.InterventionId, ReceiptId = receipt
			};
			result.ProofDigest = AftermathDigest(result); return result;
		}

		internal static KingdomPolityRouteRecord ExactEscrowStake(KingdomPolityLedger L,
			KingdomPolityIncidentRecord Plan)
		{
			KingdomPolityRouteRecord result = null;
			for (int i = 0; Plan != null && i < Plan.DisclosedStakeRefs.Count; i++)
			{
				KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(L,
					Plan.DisclosedStakeRefs[i]);
				if (route == null) continue;
				if (result != null) return null; result = route;
			}
			return result;
		}
	}
}
