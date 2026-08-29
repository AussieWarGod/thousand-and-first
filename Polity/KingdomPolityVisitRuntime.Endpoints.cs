namespace ThousandAndFirst
{
	public static partial class KingdomPolityVisitRuntime
	{
		private static bool RecoverIncidentConclusions(KingdomPolityLedger L,
			KingdomPolityVisitPlan P, out string Failure)
		{
			Failure = null;
			if (!RecoverConclusion(L, P.TermsPlanId, P.EnvoyCohortId, out Failure) ||
				!RecoverConclusion(L, P.ClashPlanId, P.WarbandCohortId, out Failure)) return false;
			return true;
		}

		private static bool RecoverConclusion(KingdomPolityLedger L, string PlanId,
			string CohortId, out string Failure)
		{
			Failure = null; KingdomPolityIncidentRecord incident = Incident(L, PlanId);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(L, CohortId);
			if (incident == null || incident.Conclusion == null || cohort == null ||
				cohort.Phase != KingdomPolityCohortPhase.Materialized) return true;
			if (KingdomPolityDiplomacyRules.IsPendingEnvoyDeathClosure(L, PlanId, CohortId))
				return true;
			return KingdomPolityCohortRules.TryConcludeEndpointCohort(L, L.Revision,
				CohortId, incident.Conclusion.ConclusionId,
				out KingdomPolityPublicationResult _, out Failure);
		}

		private static bool ReconcileUnusedWarband(KingdomSystem System,
			KingdomPolityVisitPlan P, out string Failure)
		{
			Failure = null; KingdomPolityLedger ledger = System.PolityLedger;
			KingdomPolityIncidentRecord terms = Incident(ledger, P.TermsPlanId);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(ledger,
				P.WarbandCohortId);
			if (terms?.Conclusion == null || cohort == null || ConfrontationAvailable(ledger, P))
				return true;
			if (cohort.Phase == KingdomPolityCohortPhase.Cancelled)
				return KingdomPolityExperienceRuntime.TryReleaseDirected(System,
					cohort.CohortId, out Failure);
			if (cohort.Phase != KingdomPolityCohortPhase.Planned ||
				!string.IsNullOrEmpty(cohort.ManifestationReceiptId)) return true;
			if (!KingdomPolityCohortRules.TryCancelUnpresented(ledger, ledger.Revision,
				cohort.CohortId, terms.Conclusion.ConclusionId,
				out KingdomPolityPublicationResult _, out Failure)) return false;
			return KingdomPolityExperienceRuntime.TryReleaseDirected(System,
				cohort.CohortId, out Failure);
		}

		private static bool ReconcileEnvoy(KingdomSystem System, KingdomPolityVisitPlan P,
			KingdomPolityManifestProof Manifest, long Tick, bool MayManifest, out string Failure)
		{
			Failure = null; KingdomPolityLedger ledger = System.PolityLedger;
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(ledger, P.EnvoyCohortId);
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(ledger, P.RouteId);
			if (cohort == null || route == null)
			{
				Failure = "first-contact envoy or route disappeared"; return false;
			}
			if (cohort.Phase == KingdomPolityCohortPhase.Cancelled)
				return ReconcileReturn(ledger, P, Manifest, Tick, out Failure);
			bool atSurface = System.City?.SettlementId == P.SurfaceId;
			if (MayManifest && cohort.Phase == KingdomPolityCohortPhase.Planned &&
				route.Phase == KingdomPolityRoutePhase.AvailableToWitness && atSurface &&
				!KingdomPolityEndpointRuntime.TryManifestCurrentEndpoint(System,
					P.EnvoyCohortId, Tick, out Failure)) return false;
			cohort = KingdomPolityAuthority.Cohort(ledger, P.EnvoyCohortId);
			if ((cohort.Phase == KingdomPolityCohortPhase.Concluded ||
				cohort.Phase == KingdomPolityCohortPhase.Abandoned) && atSurface &&
				!KingdomPolityEndpointRuntime.TryCleanupCurrentEndpoint(System,
					P.EnvoyCohortId, out Failure)) return false;
			cohort = KingdomPolityAuthority.Cohort(ledger, P.EnvoyCohortId);
			if (cohort.Phase == KingdomPolityCohortPhase.Abandoned) return true;
			if (cohort.Phase != KingdomPolityCohortPhase.Cleaned) return true;
			return ReconcileReturn(ledger, P, Manifest, Tick, out Failure);
		}

		private static bool ReconcileReturn(KingdomPolityLedger L, KingdomPolityVisitPlan P,
			KingdomPolityManifestProof Manifest, long Tick, out string Failure)
		{
			Failure = null; KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(L, P.RouteId);
			if (route.Phase == KingdomPolityRoutePhase.AvailableToWitness)
			{
				if (!KingdomPolityRouteRules.TryDeliverEntitlement(L, L.Revision, P.RouteId,
					Tick, Tick, P.DeliveryReceiptId, Manifest,
					out KingdomPolityPublicationResult _, out Failure)) return false;
				route = KingdomPolityAuthority.Route(L, P.RouteId);
			}
			if (route.Phase == KingdomPolityRoutePhase.Arrived)
				return KingdomPolityRouteRules.TryReturn(L, L.Revision, P.RouteId, Tick,
					P.ReturnReceiptId, Manifest, out KingdomPolityPublicationResult _, out Failure);
			return route.Phase == KingdomPolityRoutePhase.Returned ||
				route.Phase == KingdomPolityRoutePhase.ConfrontationAvailable ||
				route.Phase == KingdomPolityRoutePhase.Blocked;
		}

		private static bool ReconcileWarband(KingdomSystem System, KingdomPolityVisitPlan P,
			long Tick, bool MayManifest, out string Failure)
		{
			Failure = null; KingdomPolityLedger ledger = System.PolityLedger;
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(ledger, P.WarbandCohortId);
			if (cohort == null) return true;
			bool atSurface = System.City?.SettlementId == P.SurfaceId;
			if (MayManifest && cohort.Phase == KingdomPolityCohortPhase.Planned && atSurface &&
				ConfrontationAvailable(ledger, P) &&
				!KingdomPolityEndpointRuntime.TryManifestCurrentEndpoint(System,
					P.WarbandCohortId, Tick, out Failure)) return false;
			cohort = KingdomPolityAuthority.Cohort(ledger, P.WarbandCohortId);
			if ((cohort.Phase == KingdomPolityCohortPhase.Concluded ||
				cohort.Phase == KingdomPolityCohortPhase.Abandoned) && atSurface)
				return KingdomPolityEndpointRuntime.TryCleanupCurrentEndpoint(System,
					P.WarbandCohortId, out Failure);
			return true;
		}

		private static bool ConfrontationAvailable(KingdomPolityLedger L,
			KingdomPolityVisitPlan P)
		{
			KingdomPolityIncidentRecord clash = Incident(L, P.ClashPlanId);
			if (clash == null || clash.Conclusion != null) return false;
			for (int i = 0; i < L.Fronts.Count; i++)
				if (L.Fronts[i].Phase == KingdomPolityFrontPhase.ConfrontationAvailable &&
					(L.Fronts[i].TargetRef == P.RouteId ||
					 L.Fronts[i].TargetRef == P.WarbandCohortId)) return true;
			return false;
		}
	}
}
