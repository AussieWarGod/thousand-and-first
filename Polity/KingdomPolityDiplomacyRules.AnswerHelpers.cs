namespace ThousandAndFirst
{
	public static partial class KingdomPolityDiplomacyRules
	{
		private static bool WitnessedCohort(KingdomPolityLedger L, KingdomPolityCohortPlan C)
		{
			if (C == null || (C.Phase != KingdomPolityCohortPhase.Materialized &&
				C.Phase != KingdomPolityCohortPhase.Concluded) ||
				string.IsNullOrEmpty(C.ManifestationReceiptId)) return false;
			KingdomPolityProjectionReceipt projection = KingdomPolityAuthority.Projection(L,
				C.ManifestationReceiptId);
			return projection != null && projection.Kind ==
				KingdomPolityProjectionKind.CohortManifestation &&
				projection.SourceRef == C.CohortId &&
				projection.Phase == KingdomPolityProjectionPhase.Committed;
		}

		private static KingdomPolityRelation FindRelation(KingdomPolityLedger L,
			string From, string To)
		{
			for (int i = 0; L != null && i < L.Relations.Count; i++)
				if (L.Relations[i].FromPolityId == From && L.Relations[i].ToPolityId == To)
					return L.Relations[i];
			return null;
		}

		private static KingdomPolityFrontRecord FindFront(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; L != null && i < L.Fronts.Count; i++)
				if (L.Fronts[i].FrontId == Id) return L.Fronts[i];
			return null;
		}

		private static KingdomPolityRelationBand BandFor(KingdomPolityTermsChoice Choice)
		{
			switch (Choice)
			{
			case KingdomPolityTermsChoice.Accept: return KingdomPolityRelationBand.Pact;
			case KingdomPolityTermsChoice.Counteroffer: return KingdomPolityRelationBand.Neutral;
			case KingdomPolityTermsChoice.Truce: return KingdomPolityRelationBand.Truce;
			default: return KingdomPolityRelationBand.Hostile;
			}
		}

		private static string TermsConclusionId(string TermsPlanId,
			KingdomPolityTermsChoice Choice, string WitnessedFactId)
		{
			return KingdomPolityRules.ActivationId("taf:conclusion:terms:v1:",
				"polity-terms-conclusion-v1", TermsPlanId ?? "", Choice.ToString(),
				WitnessedFactId ?? "");
		}

		private static void SettleFront(KingdomPolityLedger L, string GrievanceId, bool Truce)
		{
			for (int i = 0; i < L.Fronts.Count; i++)
			{
				KingdomPolityFrontRecord front = L.Fronts[i];
				if (!KingdomPolityAuthority.Contains(front.GrievanceRefs, GrievanceId)) continue;
				front.PressureBand = 0;
				front.Phase = Truce ? KingdomPolityFrontPhase.Truce :
					KingdomPolityFrontPhase.Ended;
				if (front.TargetKind != KingdomPolityFrontTarget.Route) continue;
				KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(L, front.TargetRef);
				if (route != null && (route.Phase == KingdomPolityRoutePhase.Blocked ||
					route.Phase == KingdomPolityRoutePhase.ConfrontationAvailable))
					route.Phase = KingdomPolityRoutePhase.AvailableToWitness;
			}
		}
	}
}
