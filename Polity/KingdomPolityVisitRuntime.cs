using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>One bounded, playable first contact with the imported polity.</summary>
	public static partial class KingdomPolityVisitRuntime
	{
		public static bool TryReconcile(KingdomSystem System, long Tick, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded || System.City == null || Tick < 0L ||
				System.PolityLedger == null || !KingdomPolityRules.TryValidate(
					System.PolityLedger, out Failure)) return false;
			if (!HasImported(System.PolityLedger)) return true;
			if (!KingdomPolityPresentationRuntime.TryObserve(System, Tick,
				out bool enabledForNewCauses, out Failure)) return false;
			if (!KingdomPolityVisitPlan.TryCreate(System.PolityLedger,
				System.City.SettlementId, Tick, out KingdomPolityVisitPlan plan, out Failure)) return false;
			if (!KingdomPolityExperienceRuntime.TryPinnedDirectedCause(System, plan, Tick,
				out long pinnedCause, out Failure)) return false;
			if (pinnedCause != plan.DepartureTick && !KingdomPolityVisitPlan.TryCreate(
				System.PolityLedger, System.City.SettlementId, pinnedCause, out plan,
				out Failure)) return false;
			bool mayManifest = enabledForNewCauses &&
				KingdomPolityRules.CanEmitOptionalProjection(System.PolityLedger,
					plan.DepartureTick);
			if (!EnsureEnvoy(System, plan, Tick, out KingdomPolityManifestProof manifest,
				out bool ready, out Failure)) return false;
			if (!ready) return true;
			if (!EnsureDispute(System, plan, Tick, out Failure) ||
				!ReplayLoadedDeathIntents(System, plan, out Failure) ||
				!RecoverIncidentConclusions(System.PolityLedger, plan, out Failure) ||
				!ReconcileUnusedWarband(System, plan, out Failure) ||
				!ReconcileEnvoy(System, plan, manifest, Tick, mayManifest, out Failure) ||
				!KingdomPolityCorrespondenceRuntime.TryEnsureFirstContact(System, plan, out Failure) ||
				!KingdomPolityCorrespondenceRuntime.TryRecoverTradeReceipts(System, out Failure) ||
				!KingdomPolityCorrespondenceRuntime.TryRecoverEnvoyDeaths(System, out Failure) ||
				!ReconcileWarband(System, plan, Tick, mayManifest, out Failure)) return false;
			return true;
		}

		private static bool ReplayLoadedDeathIntents(KingdomSystem System,
			KingdomPolityVisitPlan Plan, out string Failure)
		{
			Failure = null;
			if (!KingdomPolityLoadedEndpointRuntime.TryObserve(System, out XRL.World.Zone _,
				out string settlementId, out bool available, out Failure)) return false;
			if (!available) return true;
			string[] ids = { Plan.EnvoyCohortId, Plan.WarbandCohortId };
			for (int i = 0; i < ids.Length; i++)
			{
				KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(
					System.PolityLedger, ids[i]);
				if (cohort == null || cohort.SurfaceRef != settlementId ||
					string.IsNullOrEmpty(cohort.ManifestationReceiptId) ||
					(cohort.Phase != KingdomPolityCohortPhase.Materialized &&
					 cohort.Phase != KingdomPolityCohortPhase.Concluded &&
					 cohort.Phase != KingdomPolityCohortPhase.Abandoned)) continue;
				if (!KingdomPolityEndpointRuntime.TryReplayDeathIntents(System,
					cohort.CohortId, out Failure)) return false;
			}
			return true;
		}

		private static bool EnsureRoute(KingdomPolityLedger L, KingdomPolityVisitPlan P,
			long Tick, out KingdomPolityManifestProof Manifest, out string Failure)
		{
			Manifest = null;
			KingdomPolityRoutePlanRequest request = new KingdomPolityRoutePlanRequest
			{
				RouteId = P.RouteId, EventStreamId = P.StreamId,
				OriginId = P.ExternalEndpointId, DestinationId = P.SurfaceId,
				OrderedPath = new List<string> { P.ExternalEndpointId, P.SurfaceId },
				Mode = KingdomPolityRouteMode.Foot, Purpose = KingdomPolityRoutePurpose.Delegation,
				DepartureOrdinal = 0UL, FirstDueTick = P.ArrivalDueTick,
				ManifestOrErrandId = P.ErrandId, CounterpartyRef = P.Current.PolityId
			};
			if (!KingdomPolityRouteRules.TryPlan(L, L.Revision, request,
				out KingdomPolityPublicationResult _, out Failure) ||
				!KingdomPolityManifestRules.TryCreateErrandProof(P.ManifestProofId,
					P.Visitor.PolityId, P.ErrandId, out Manifest, out Failure)) return false;
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(L, P.RouteId);
			if (route.Phase == KingdomPolityRoutePhase.Preparing &&
				!KingdomPolityRouteRules.TryDepart(L, L.Revision, P.RouteId, P.DepartureTick,
					P.DepartureReceiptId, Manifest, out KingdomPolityPublicationResult _,
					out Failure)) return false;
			route = KingdomPolityAuthority.Route(L, P.RouteId);
			if (route.Phase == KingdomPolityRoutePhase.Traveling && Tick >= route.NextDueTick &&
				!KingdomPolityRouteRules.TryAdvance(L, L.Revision, P.RouteId, route.SegmentIndex,
					Tick, Tick, out KingdomPolityPublicationResult _, out Failure)) return false;
			return true;
		}

		private static bool EnsureEnvoy(KingdomSystem System, KingdomPolityVisitPlan P,
			long Tick, out KingdomPolityManifestProof Manifest, out bool Ready,
			out string Failure)
		{
			KingdomPolityLedger L = System.PolityLedger; Manifest = null;
			Ready = false; Failure = null;
			KingdomPolityCohortPlanRequest request = new KingdomPolityCohortPlanRequest
			{
				CohortId = P.EnvoyCohortId, Purpose = KingdomPolityCohortPurpose.Envoy,
				SourceRef = P.RouteId, PolityId = P.Visitor.PolityId, SurfaceRef = P.SurfaceId,
				MemberCount = 2, NamedFigureId = P.Representative?.FigureId,
				EventStreamId = P.StreamId, RulesVersion = KingdomPolityNpcRules.RulesVersion,
				EventOrdinal = 0UL
			};
			KingdomPolityCohortPlan existing = KingdomPolityAuthority.Cohort(L, P.EnvoyCohortId);
			bool terminal = existing != null && (existing.Phase == KingdomPolityCohortPhase.Cleaned ||
				existing.Phase == KingdomPolityCohortPhase.Cancelled ||
				existing.Phase == KingdomPolityCohortPhase.Abandoned ||
				existing.Phase == KingdomPolityCohortPhase.Archived);
			if (!terminal && !KingdomPolityRules.CanEmitOptionalProjection(L, P.DepartureTick))
				return true;
			if (terminal)
			{
				if (!EnsureRoute(L, P, Tick, out Manifest, out Failure)) return false;
				Ready = true; return true;
			}
			if (existing == null && !KingdomPolityAttentionRules.TryAdmitPlan(L, 2,
				out string _)) return true;
			if (!KingdomPolityExperienceRuntime.TryReserveDirectedPlan(System,
				P.EnvoyCohortId, P.SurfaceId, 2, P.DepartureTick, Tick,
				out KingdomPolityPresentationAuthorityProof authority, out bool _,
				out KingdomExperienceCapacityFault fault,
				out Failure))
			{
				if (KingdomPolityExperienceRuntime.ExpectedCapacityRefusal(fault))
				{
					Failure = null; return true;
				}
				return false;
			}
			if (!EnsureRoute(L, P, Tick, out Manifest, out Failure))
			{
				string routeFailure = Failure;
				if (existing == null && !KingdomPolityExperienceRuntime.TryReleaseDirected(System,
					P.EnvoyCohortId, out string releaseFailure)) Failure = routeFailure
					+ "; presentation rollback failed: " + releaseFailure;
				else Failure = routeFailure;
				return false;
			}
			request.PresentationAuthority = authority;
			if (!KingdomPolityCohortRules.TryPlan(L, L.Revision, request,
				out KingdomPolityPublicationResult _, out Failure))
			{
				string planFailure = Failure;
				if (existing == null && !KingdomPolityExperienceRuntime.TryReleaseDirected(System,
					P.EnvoyCohortId, out string releaseFailure)) Failure = planFailure +
					"; presentation rollback failed: " + releaseFailure;
				else Failure = planFailure;
				return false;
			}
			Ready = true; return true;
		}

		private static bool HasImported(KingdomPolityLedger L)
		{
			for (int i = 0; i < L.Polities.Count; i++)
				if (L.Polities[i].Source == KingdomPolitySource.ImportedLegacy) return true;
			return false;
		}
	}
}
