namespace ThousandAndFirst
{
	public static partial class KingdomPolityCorrespondenceRules
	{
		/// <summary>Publishes one witnessed decline and its exact caused grievance atomically.</summary>
		public static bool TryDeclineConsignmentWithExactGrievance(
			KingdomPolityLedger Ledger, long ExpectedRevision, string CorrespondencePlanId,
			string WitnessedFactId, long Tick, out string GrievanceId,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			GrievanceId = null; Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || Tick < 0L ||
				!KingdomPolityRules.TypedId(WitnessedFactId, "taf:fact:witnessed:"))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "correspondence decline evidence is invalid", out Failure);
			KingdomPolityIncidentRecord plan = FindPlan(Ledger, CorrespondencePlanId);
			if (!TryReadRequest(Ledger, plan, out KingdomPolityConsignmentRequest request,
				out Failure)) return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityIncidentConclusion expected = DeclinedConclusion(request,
				WitnessedFactId, Tick);
			if (plan.Conclusion != null && !ExactConclusion(plan.Conclusion, expected))
				return KingdomPolityAuthority.Refuse(Result,
					"correspondence already carries another reply", out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			FindPlan(candidate, CorrespondencePlanId).Conclusion = expected;
			KingdomPolityGrievanceIngressRequest ingress =
				new KingdomPolityGrievanceIngressRequest
				{
					SourceKind = KingdomPolityGrievanceSourceKind.ResourceRefusal,
					SourceRef = CorrespondencePlanId,
					SourceReceiptId = expected.ReceiptRefs[0],
					IssuerPolityId = request.CounterpartyPolityId,
					TargetPolityId = request.CurrentPolityId
				};
			if (!KingdomPolityDiplomacyRules.TryDeriveExactGrievance(candidate, ingress,
				out KingdomPolityGrievanceRecord grievance, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			GrievanceId = grievance.GrievanceId;
			KingdomPolityGrievanceRecord existing = FindExactGrievance(Ledger, GrievanceId);
			if (existing != null)
			{
				if (!KingdomPolityDiplomacyRules.ExactOpenGrievance(existing, grievance))
					return KingdomPolityAuthority.Refuse(Result,
						"resource-refusal retry changed its exact source", out Failure);
				if (plan.Conclusion == null)
					return KingdomPolityAuthority.Refuse(Result,
						"resource grievance predates its exact decline", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (HasGrievanceSource(Ledger, grievance.SourceEventId))
				return KingdomPolityAuthority.Refuse(Result,
					"decline source already emitted one grievance", out Failure);
			if (Ledger.Grievances.Count >= KingdomPolityRules.MaxGrievances)
				return KingdomPolityAuthority.Refuse(Result,
					"grievance capacity is exhausted", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityDiplomacyRules.InsertGrievance(candidate, grievance);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		private static KingdomPolityGrievanceRecord FindExactGrievance(
			KingdomPolityLedger L, string Id)
		{
			for (int i = 0; L != null && i < L.Grievances.Count; i++)
				if (L.Grievances[i].GrievanceId == Id) return L.Grievances[i];
			return null;
		}

		private static bool HasGrievanceSource(KingdomPolityLedger L, string Source)
		{
			for (int i = 0; L != null && i < L.Grievances.Count; i++)
				if (L.Grievances[i].SourceEventId == Source) return true;
			return false;
		}
	}
}
