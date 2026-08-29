using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Pure fold for one trusted loaded clash; no offscreen conclusion entrypoint.</summary>
	internal static partial class KingdomPolityClashRules
	{
		internal static bool TryCreateLiveProof(string ProofId, string IncidentPlanId,
			string SurfaceRef, string ZoneId, long CommitTick, IList<string> ObservedFactIds,
			IList<string> ParticipantProjectionIds, IList<KingdomPolitySystemicDelta> SystemicDeltas,
			IList<KingdomPolityRelationDelta> RelationDeltas, IList<string> ReceiptRefs,
			out KingdomPolityWitnessedClashProof Proof, out string Failure)
		{
			Proof = new KingdomPolityWitnessedClashProof
			{
				ProofId = ProofId, IncidentPlanId = IncidentPlanId, SurfaceRef = SurfaceRef,
				ZoneId = ZoneId, CommitTick = CommitTick,
				ObservedFactIds = Copy(ObservedFactIds),
				ParticipantProjectionIds = Copy(ParticipantProjectionIds),
				SystemicDeltas = CopySystemic(SystemicDeltas),
				RelationDeltas = CopyRelations(RelationDeltas), ReceiptRefs = Copy(ReceiptRefs)
			};
			Proof.ProofDigest = Digest(Proof);
			if (TryValidateProof(Proof, out Failure)) return true;
			Proof = null; return false;
		}

		internal static bool TryConcludeWitnessed(KingdomPolityLedger Ledger,
			long ExpectedRevision, KingdomPolityWitnessedClashProof Proof,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!TryValidateProof(Proof, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityIncidentRecord plan = FindClashPlan(Ledger, Proof.IncidentPlanId);
			string conclusionId = KingdomPolityRules.ActivationId(
				"taf:conclusion:clash:v1:", "polity-witnessed-clash-v1",
				Proof.IncidentPlanId, Proof.ProofDigest);
			if (plan != null && plan.Conclusion != null)
			{
				if (plan.Conclusion.ConclusionId != conclusionId)
					return KingdomPolityAuthority.Refuse(Result,
						"clash already carries another witnessed conclusion", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (plan == null || plan.Purpose != KingdomPolityCohortPurpose.Warband ||
				!KingdomPolityAuthority.Contains(plan.EligibleSurfaceRefs, Proof.SurfaceRef) ||
				!ExactLiveParticipants(Ledger, plan, Proof, out Failure) ||
				!RelationBeforeMatches(Ledger, Proof.RelationDeltas, out Failure))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "clash is not an exact committed live scene", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityIncidentRecord changed = FindClashPlan(candidate, plan.IncidentPlanId);
			List<string> facts = Copy(Proof.ObservedFactIds);
			List<string> receipts = Copy(Proof.ReceiptRefs);
			if (changed.Intervention != null)
			{
				KingdomPolityAuthority.AddSortedUnique(facts, changed.Intervention.ObservedFactId);
				KingdomPolityAuthority.AddSortedUnique(receipts, changed.Intervention.ReceiptId);
			}
			if (facts.Count > KingdomPolityRules.MaxObservedFacts ||
				receipts.Count > KingdomPolityRules.MaxRefs)
				return KingdomPolityAuthority.Refuse(Result,
					"witnessed clash evidence exceeds its bounded conclusion", out Failure);
			changed.Conclusion = new KingdomPolityIncidentConclusion
			{
				ConclusionId = conclusionId, ResolutionKind = KingdomPolityResolutionKind.LiveScene,
				CommitTick = Proof.CommitTick, ObservedFactIds = facts,
				SystemicDeltas = CopySystemic(Proof.SystemicDeltas),
				RelationDeltas = CopyRelations(Proof.RelationDeltas),
				ReceiptRefs = receipts
			};
			KingdomPolityAftermathKind aftermathKind = HasCeasefire(changed)
				? KingdomPolityAftermathKind.Ceasefire
				: KingdomPolityAftermathKind.WitnessedWithdrawal;
			if (!KingdomPolityConflictRules.TryCreateAftermath(changed, Proof, aftermathKind,
				out KingdomPolityAftermathRecord aftermath, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityAuthority.AddSortedUnique(changed.Conclusion.ReceiptRefs,
				aftermath.ReceiptId);
			if (changed.Conclusion.ReceiptRefs.Count > KingdomPolityRules.MaxRefs)
				return KingdomPolityAuthority.Refuse(Result,
					"witnessed aftermath exceeds its bounded receipts", out Failure);
			changed.Aftermath = aftermath;
			ApplyRelationDeltas(candidate, Proof);
			if (!KingdomPolityDiplomacyRules.TryInsertBrokenPactGrievances(candidate,
				changed, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			ResolveOpenClashGrievances(candidate, changed, conclusionId);
			EndWitnessedFronts(candidate, changed, conclusionId);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		private static bool HasCeasefire(KingdomPolityIncidentRecord Plan)
		{
			if (Plan.Intervention?.Choice ==
				KingdomPolityInterventionChoice.MediateCeasefire) return true;
			for (int i = 0; i < Plan.Conclusion.RelationDeltas.Count; i++)
				if (Plan.Conclusion.RelationDeltas[i].After == KingdomPolityRelationBand.Truce ||
					Plan.Conclusion.RelationDeltas[i].After == KingdomPolityRelationBand.Pact)
					return true;
			return false;
		}

		private static bool TryValidateProof(KingdomPolityWitnessedClashProof P,
			out string Failure)
		{
			Failure = null;
			if (P == null || !KingdomPolityRules.TypedId(P.ProofId, "taf:clash-proof:") ||
				!KingdomPolityRules.TypedId(P.IncidentPlanId, "taf:incident-plan:") ||
				!KingdomPolityRules.SemanticId(P.SurfaceRef) ||
				!KingdomPolityRules.Text(P.ZoneId, true) || P.CommitTick < 0L ||
				!CanonicalWitnesses(P.ObservedFactIds) ||
				!CanonicalSemantic(P.ParticipantProjectionIds, 1, KingdomPolityRules.MaxRefs) ||
				!CanonicalSemantic(P.ReceiptRefs, 1, KingdomPolityRules.MaxRefs) ||
				!ValidSystemic(P.SystemicDeltas, P.ReceiptRefs) ||
				!ValidRelationDeltas(P.RelationDeltas, P.ReceiptRefs) ||
				!KingdomPolityRules.Digest(P.ProofDigest) || P.ProofDigest != Digest(P))
			{
				Failure = "witnessed clash proof is invalid, unbounded, or noncanonical"; return false;
			}
			return true;
		}

		private static bool CanonicalWitnesses(IList<string> Values)
		{
			if (!CanonicalSemantic(Values, 1, KingdomPolityRules.MaxObservedFacts)) return false;
			for (int i = 0; i < Values.Count; i++)
				if (!Values[i].StartsWith("taf:fact:witnessed:", StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool ValidSystemic(IList<KingdomPolitySystemicDelta> Values,
			IList<string> Receipts)
		{
			if (Values == null || Values.Count > KingdomPolityRules.MaxDeltas) return false;
			int previousKind = -1; string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolitySystemicDelta d = Values[i];
				if (d == null || (d.Kind != KingdomPolitySystemicDeltaKind.RoutePosture &&
					d.Kind != KingdomPolitySystemicDeltaKind.Standing &&
					d.Kind != KingdomPolitySystemicDeltaKind.ReversibleWound) ||
					!KingdomPolityRules.SemanticId(d.TargetId) || d.Amount == 0 ||
					Math.Abs((long)d.Amount) > KingdomPolityRules.MaxValueBudget ||
					!KingdomPolityRules.SemanticId(d.ReceiptId) ||
					!KingdomPolityAuthority.Contains(Receipts, d.ReceiptId)) return false;
				string key = d.TargetId + "\n" + d.ReceiptId; int kind = (int)d.Kind;
				if (kind < previousKind || (kind == previousKind &&
					string.CompareOrdinal(previous, key) >= 0)) return false;
				previousKind = kind; previous = key;
			}
			return true;
		}
	}
}
