using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCorrespondenceRules
	{
		public const int FirstContactWaterDrams = 8;
		public const int ConsignmentStandingPerDram = 1;
		internal const string ConsignmentPlanPrefix = "taf:incident-plan:correspondence:v1:";

		public static bool TryPlanConsignment(KingdomPolityLedger Ledger,
			long ExpectedRevision, string TermsPlanId, string EnvoyCohortId,
			string SurfaceRef, out KingdomPolityConsignmentRequest Request,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Request = null; Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			string planId = Id(ConsignmentPlanPrefix, "plan", TermsPlanId, EnvoyCohortId);
			KingdomPolityIncidentRecord existing = FindPlan(Ledger, planId);
			if (existing != null)
			{
				if (!TryReadRequest(Ledger, existing, out Request, out Failure) ||
					Request.TermsPlanId != TermsPlanId || Request.RecipientCohortId !=
					EnvoyCohortId || Request.SurfaceRef != SurfaceRef)
					return KingdomPolityAuthority.Refuse(Result,
						Failure ?? "correspondence retry changed its frozen cause", out Failure);
				KingdomPolityIncidentRecord frozen = BuildPlan(Ledger, Request,
					existing.DisclosedStakeRefs.Count == 2);
				if (!ExactOpenPlan(existing, frozen)) return KingdomPolityAuthority.Refuse(Result,
					"correspondence id already carries another request", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (!TryBuildNewRequest(Ledger, TermsPlanId, EnvoyCohortId, SurfaceRef,
				out Request, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityIncidentRecord expected = BuildPlan(Ledger, Request);
			if (!EligibleRequestRelation(Ledger, Request))
				return KingdomPolityAuthority.Refuse(Result,
					"terms do not cause an eligible consignment request", out Failure);
			if (Ledger.Incidents.Count >= KingdomPolityRules.MaxIncidents)
				return KingdomPolityAuthority.Refuse(Result,
					"correspondence capacity is exhausted", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			InsertIncident(candidate, expected);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		public static bool TryDescribeConsignment(KingdomPolityLedger Ledger,
			string CorrespondencePlanId, out KingdomPolityConsignmentRequest Request,
			out KingdomPolityCorrespondenceReplyKind Reply, out string Failure)
		{
			Request = null; Reply = KingdomPolityCorrespondenceReplyKind.None;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure)) return false;
			KingdomPolityIncidentRecord plan = FindPlan(Ledger, CorrespondencePlanId);
			if (!TryReadRequest(Ledger, plan, out Request, out Failure)) return false;
			return TryReplyKind(Ledger, plan, Request, out Reply, out Failure);
		}

		public static bool TryConsumeTradeReceipt(KingdomPolityLedger Ledger,
			long ExpectedRevision, KingdomTradePolityConsignmentReceipt Receipt,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityIncidentRecord plan = FindPlan(Ledger, Receipt?.CorrespondencePlanId);
			if (!TryReadRequest(Ledger, plan, out KingdomPolityConsignmentRequest request,
				out Failure) || !TryValidateTradeReceipt(request, Receipt, out Failure) ||
				!TryValidateReceiptRecipient(Ledger, request, Receipt, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			if (plan.Conclusion != null)
			{
				if (!TryReplyKind(Ledger, plan, request,
					out KingdomPolityCorrespondenceReplyKind reply, out Failure) ||
					!ReceiptMatchesConclusion(plan, request, Receipt, reply))
					return KingdomPolityAuthority.Refuse(Result,
						"correspondence already carries another reply", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityRelation relation = Relation(Ledger, request.CounterpartyPolityId,
				request.CurrentPolityId);
			if (Receipt.Kind == KingdomTradePolityConsignmentReceiptKind.Landed && relation == null)
				return KingdomPolityAuthority.Refuse(Result,
					"landed consignment lost its directed relation", out Failure);
			KingdomPolityIncidentConclusion expected = Receipt.Kind ==
				KingdomTradePolityConsignmentReceiptKind.Landed ? FulfilledConclusion(request,
					Receipt, relation) : UnfulfilledConclusion(request, Receipt);
			if (Receipt.Kind == KingdomTradePolityConsignmentReceiptKind.Landed &&
				!CanApplyConsignmentRelationship(Ledger, request, expected, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			FindPlan(candidate, request.CorrespondencePlanId).Conclusion = expected;
			if (Receipt.Kind == KingdomTradePolityConsignmentReceiptKind.Landed)
				ApplyConsignmentRelationship(candidate, request, expected, Receipt.CommitTick);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		internal static bool TryDeclineConsignment(KingdomPolityLedger Ledger,
			long ExpectedRevision, string CorrespondencePlanId, string WitnessedFactId,
			long Tick, out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || Tick < 0L ||
				!KingdomPolityRules.TypedId(WitnessedFactId, "taf:fact:witnessed:"))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "correspondence decline evidence is invalid", out Failure);
			KingdomPolityIncidentRecord plan = FindPlan(Ledger, CorrespondencePlanId);
			if (!TryReadRequest(Ledger, plan, out KingdomPolityConsignmentRequest request,
				out Failure)) return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityIncidentConclusion expected = DeclinedConclusion(request,
				WitnessedFactId, Tick);
			if (plan.Conclusion != null)
			{
				if (!ExactConclusion(plan.Conclusion, expected))
					return KingdomPolityAuthority.Refuse(Result,
						"correspondence already carries another reply", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			FindPlan(candidate, CorrespondencePlanId).Conclusion = expected;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		private static KingdomPolityIncidentRecord BuildPlan(KingdomPolityLedger L,
			KingdomPolityConsignmentRequest R, bool LegacyStakeShape = false)
		{
			KingdomPolityIncidentRecord terms = FindPlan(L, R.TermsPlanId);
			return new KingdomPolityIncidentRecord
			{
				IncidentPlanId = R.CorrespondencePlanId, IncidentId = R.CorrespondenceId,
				GrievanceRefs = new List<string>(terms.GrievanceRefs),
				ParticipantCohortRefs = new List<string> { R.RecipientCohortId },
				DisclosedStakeRefs = LegacyStakeShape ? Sorted(R.NeedRef, R.TermsPlanId) :
					Sorted(R.NeedRef, R.TermsPlanId, R.CurrentPolityId,
						R.CounterpartyPolityId), MaxSystemicWound = 0,
				Purpose = KingdomPolityCohortPurpose.Courier, EventStreamId = terms.EventStreamId,
				RulesVersion = 1, EventOrdinal = terms.EventOrdinal + 1UL,
				EligibleSurfaceRefs = new List<string> { R.SurfaceRef },
				InterventionOptionKeys = new List<string>
					{ "decline-consignment", "deliver-water-8-drams" }
			};
		}

		private static List<string> Sorted(string A, string B)
		{
			return string.CompareOrdinal(A, B) < 0 ? new List<string> { A, B } :
				new List<string> { B, A };
		}

		private static List<string> Sorted(params string[] Values)
		{
			List<string> result = new List<string>(Values);
			result.Sort(StringComparer.Ordinal); return result;
		}

		private static void InsertIncident(KingdomPolityLedger L,
			KingdomPolityIncidentRecord Plan)
		{
			int at = 0;
			while (at < L.Incidents.Count && string.CompareOrdinal(
				L.Incidents[at].IncidentPlanId, Plan.IncidentPlanId) < 0) at++;
			L.Incidents.Insert(at, Plan);
		}
	}
}
