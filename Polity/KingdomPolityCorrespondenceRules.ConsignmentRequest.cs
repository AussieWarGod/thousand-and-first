using System;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCorrespondenceRules
	{
		private static bool TryBuildNewRequest(KingdomPolityLedger L, string TermsPlanId,
			string EnvoyCohortId, string SurfaceRef,
			out KingdomPolityConsignmentRequest R, out string Failure)
		{
			R = null; Failure = null;
			KingdomPolityRecord current = Current(L);
			KingdomPolityCohortPlan envoy = KingdomPolityAuthority.Cohort(L, EnvoyCohortId);
			KingdomPolityRecord counterparty = envoy == null ? null :
				KingdomPolityAuthority.Polity(L, envoy.PolityId);
			KingdomPolityRelation relation = Relation(L, counterparty?.PolityId,
				current?.PolityId);
			if (current == null || counterparty == null || counterparty.Source ==
				KingdomPolitySource.CurrentRealm || counterparty.Lifecycle !=
				KingdomPolityLifecycle.Active || relation == null || !EligibleRelation(relation.Band))
				return Fail("terms do not cause an active eligible consignment request", out Failure);
			return TryBuildFrozenRequest(L, TermsPlanId, EnvoyCohortId, SurfaceRef,
				current.PolityId, counterparty.PolityId, out R, out Failure);
		}

		private static bool TryBuildFrozenRequest(KingdomPolityLedger L, string TermsPlanId,
			string EnvoyCohortId, string SurfaceRef, string CurrentPolityId,
			string CounterpartyPolityId, out KingdomPolityConsignmentRequest R,
			out string Failure)
		{
			R = null; Failure = null;
			KingdomPolityIncidentRecord terms = FindPlan(L, TermsPlanId);
			KingdomPolityCohortPlan envoy = KingdomPolityAuthority.Cohort(L, EnvoyCohortId);
			KingdomPolityRecord current = KingdomPolityAuthority.Polity(L, CurrentPolityId);
			KingdomPolityRecord counterparty = KingdomPolityAuthority.Polity(L,
				CounterpartyPolityId);
			if (terms == null || terms.Purpose != KingdomPolityCohortPurpose.Envoy ||
				envoy == null || envoy.Purpose != KingdomPolityCohortPurpose.Envoy ||
				!KingdomPolityAuthority.Contains(terms.ParticipantCohortRefs, EnvoyCohortId) ||
				envoy.PolityId != CounterpartyPolityId || envoy.SurfaceRef != SurfaceRef ||
				!KingdomPolityAuthority.Contains(terms.EligibleSurfaceRefs, SurfaceRef) ||
				current == null || current.Source != KingdomPolitySource.CurrentRealm ||
				counterparty == null || counterparty.Source == KingdomPolitySource.CurrentRealm ||
				terms.EventOrdinal == ulong.MaxValue)
				return Fail("frozen correspondence cause or original polity identity is invalid",
					out Failure);
			string plan = Id(ConsignmentPlanPrefix, "plan", terms.IncidentPlanId, EnvoyCohortId);
			string need = Id("taf:need:polity-water:v1:", "need", plan,
				CounterpartyPolityId, CurrentPolityId, SurfaceRef, N(FirstContactWaterDrams));
			R = new KingdomPolityConsignmentRequest
			{
				CorrespondencePlanId = plan,
				CorrespondenceId = Id("taf:incident:correspondence:v1:", "incident", plan),
				TermsPlanId = terms.IncidentPlanId, RecipientCohortId = envoy.CohortId,
				CounterpartyPolityId = CounterpartyPolityId,
				CurrentPolityId = CurrentPolityId, SurfaceRef = SurfaceRef, NeedRef = need,
				RequestedDrams = FirstContactWaterDrams
			};
			R.ConsignmentId = Id("taf:manifest:polity-consignment:v1:", "consignment",
				R.CorrespondencePlanId, R.NeedRef, N(R.RequestedDrams));
			R.RequestDigest = RequestDigest(R); return true;
		}

		private static bool TryReadRequest(KingdomPolityLedger L,
			KingdomPolityIncidentRecord Plan, out KingdomPolityConsignmentRequest R,
			out string Failure)
		{
			R = null; Failure = null;
			bool legacy = Plan?.DisclosedStakeRefs?.Count == 2;
			if (!IsConsignmentPlan(Plan) || Plan.ParticipantCohortRefs.Count != 1 ||
				Plan.EligibleSurfaceRefs.Count != 1 || (!legacy &&
				Plan.DisclosedStakeRefs.Count != 4))
				return Fail("correspondence request is missing or malformed", out Failure);
			string terms = null, need = null;
			for (int i = 0; i < Plan.DisclosedStakeRefs.Count; i++)
			{
				string row = Plan.DisclosedStakeRefs[i];
				if (row.StartsWith("taf:incident-plan:", StringComparison.Ordinal)) terms = row;
				if (row.StartsWith("taf:need:polity-water:v1:", StringComparison.Ordinal)) need = row;
			}
			KingdomPolityCohortPlan envoy = KingdomPolityAuthority.Cohort(L,
				Plan.ParticipantCohortRefs[0]);
			KingdomPolityRecord current = legacy ? CurrentAny(L) :
				FrozenCurrent(L, Plan, envoy?.PolityId);
			if (terms == null || need == null || envoy == null || current == null ||
				!TryBuildFrozenRequest(L, terms, envoy.CohortId, Plan.EligibleSurfaceRefs[0],
					current.PolityId, envoy.PolityId, out R, out Failure) ||
				R.CorrespondencePlanId != Plan.IncidentPlanId || R.NeedRef != need) return false;
			if (!legacy)
			{
				if (!KingdomPolityAuthority.Contains(Plan.DisclosedStakeRefs, current.PolityId) ||
					!KingdomPolityAuthority.Contains(Plan.DisclosedStakeRefs, envoy.PolityId))
					return Fail("correspondence lost its frozen original identities", out Failure);
			}
			return true;
		}

		private static KingdomPolityRecord CurrentAny(KingdomPolityLedger L)
		{
			KingdomPolityRecord result = null;
			for (int i = 0; L != null && i < L.Polities.Count; i++)
				if (L.Polities[i].Source == KingdomPolitySource.CurrentRealm)
				{
					if (result != null) return null; result = L.Polities[i];
				}
			return result;
		}

		private static KingdomPolityRecord FrozenCurrent(KingdomPolityLedger L,
			KingdomPolityIncidentRecord Plan, string CounterpartyPolityId)
		{
			KingdomPolityRecord result = null;
			for (int i = 0; Plan?.DisclosedStakeRefs != null &&
				i < Plan.DisclosedStakeRefs.Count; i++)
			{
				string id = Plan.DisclosedStakeRefs[i];
				if (id == CounterpartyPolityId) continue;
				KingdomPolityRecord row = KingdomPolityAuthority.Polity(L, id);
				if (row == null || row.Source != KingdomPolitySource.CurrentRealm) continue;
				if (result != null) return null;
				result = row;
			}
			return result;
		}
	}
}
