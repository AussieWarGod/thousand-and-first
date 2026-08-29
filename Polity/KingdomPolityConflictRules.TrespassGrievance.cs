namespace ThousandAndFirst
{
	public static partial class KingdomPolityConflictRules
	{
		/// <summary>Publishes exact loaded support and its witnessed trespass atomically.</summary>
		public static bool TryRecordWitnessedTrespass(KingdomPolityLedger Ledger,
			long ExpectedRevision, string IncidentPlanId, string SurfaceRef, string ZoneId,
			long Tick, string ObservedFactId,
			System.Collections.Generic.IList<string> ParticipantProjectionIds,
			string IssuerPolityId, string TargetPolityId, out string GrievanceId,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			GrievanceId = null; Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!TryCreateIntervention(IncidentPlanId,
					KingdomPolityInterventionChoice.SupportSettlement, SurfaceRef, ZoneId,
					Tick, ObservedFactId, ParticipantProjectionIds,
					out KingdomPolityInterventionRecord intervention, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityIncidentRecord plan = FindPlan(Ledger, IncidentPlanId);
			if (plan == null || !KingdomPolityAuthority.Contains(plan.InterventionOptionKeys,
				OptionKey(intervention.Choice)) ||
				!ExactLiveParticipants(Ledger, plan, intervention, out Failure))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "support is not witnessed in this loaded clash", out Failure);
			if (plan.Intervention != null &&
				plan.Intervention.ProofDigest != intervention.ProofDigest)
				return KingdomPolityAuthority.Refuse(Result,
					"clash already carries another intervention", out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			FindPlan(candidate, IncidentPlanId).Intervention = intervention;
			KingdomPolityGrievanceIngressRequest ingress =
				new KingdomPolityGrievanceIngressRequest
				{
					SourceKind = KingdomPolityGrievanceSourceKind.WitnessedTrespass,
					SourceRef = IncidentPlanId, SourceReceiptId = intervention.ReceiptId,
					IssuerPolityId = IssuerPolityId, TargetPolityId = TargetPolityId
				};
			if (!KingdomPolityDiplomacyRules.TryDeriveExactGrievance(candidate, ingress,
				out KingdomPolityGrievanceRecord grievance, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			GrievanceId = grievance.GrievanceId;
			KingdomPolityGrievanceRecord existing = FindTrespassGrievance(Ledger,
				GrievanceId);
			if (existing != null)
			{
				if (!KingdomPolityDiplomacyRules.ExactOpenGrievance(existing, grievance))
					return KingdomPolityAuthority.Refuse(Result,
						"trespass retry changed its exact source", out Failure);
				if (plan.Intervention == null)
					return KingdomPolityAuthority.Refuse(Result,
						"trespass predates its witnessed stance", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (HasTrespassSource(Ledger, grievance.SourceEventId))
				return KingdomPolityAuthority.Refuse(Result,
					"support source already emitted one grievance", out Failure);
			if (Ledger.Grievances.Count >= KingdomPolityRules.MaxGrievances)
				return KingdomPolityAuthority.Refuse(Result,
					"grievance capacity is exhausted", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityDiplomacyRules.InsertGrievance(candidate, grievance);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		private static KingdomPolityGrievanceRecord FindTrespassGrievance(
			KingdomPolityLedger L, string Id)
		{
			for (int i = 0; L != null && i < L.Grievances.Count; i++)
				if (L.Grievances[i].GrievanceId == Id) return L.Grievances[i];
			return null;
		}

		private static bool HasTrespassSource(KingdomPolityLedger L, string Source)
		{
			for (int i = 0; L != null && i < L.Grievances.Count; i++)
				if (L.Grievances[i].SourceEventId == Source) return true;
			return false;
		}
	}
}
