using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Plans current-window work and reifies only at the player's exact loaded endpoint.</summary>
	internal static partial class KingdomPolitySchedulerRuntime
	{
		internal static bool TryReconcile(KingdomSystem System, long Tick,
			bool EnabledForNewCauses, out string Failure)
		{
			Failure = null;
			if (!KingdomPolityEndpointFactRuntime.TryOffer(System, Tick,
				out KingdomPolityDispatchOffer offer, out Failure)) return false;
			KingdomPolityDispatchState state = System.PolityDispatch ??
				(System.PolityDispatch = new KingdomPolityDispatchState());
			if (!KingdomPolityDispatchRules.ValidState(state, out Failure))
			{
				string fault = Failure;
				if (!KingdomPolityDispatchRules.TryRecover(state, offer.RealmId,
					fault, out Failure)) return false;
			}
			ulong window = (ulong)(Tick / KingdomPolityDispatchRules.PeriodTicks);
			if (!RetireOld(System, Tick, window, out Failure)) return false;
			if (!KingdomPolityDispatchRules.TryOpen(state, state.Revision, offer,
				EnabledForNewCauses && KingdomPolityRules.CanEmitOptionalProjection(
					System.PolityLedger, Tick - Tick % KingdomPolityDispatchRules.PeriodTicks),
				out List<KingdomPolityDueWork> work, out Failure)) return false;
			if (!TryOrderFair(System, work, out Failure)) return false;
			for (int i = 0; i < work.Count; i++)
			{
				KingdomPolityDueWork row = work[i];
				if (!KingdomPolityRivalTrafficRules.TryAssign(System.PolityLedger, row,
					out KingdomPolityTrafficAssignment assignment, out Failure)) return false;
				// Assignment selects the semantic owner only. Dispatch owns the already-frozen
				// cohort/source identity used by W0, direct fallback, and crash adoption.
				KingdomPolityCohortPlanRequest request = new KingdomPolityCohortPlanRequest
				{
					CohortId = row.CohortId, Purpose = row.Purpose, SourceRef = row.SourceRef,
					PolityId = assignment.PolityId, SurfaceRef = row.SettlementId,
					MemberCount = row.MemberCount, EventStreamId = row.EventStreamId,
					RulesVersion = KingdomPolityNpcRules.RulesVersion,
					EventOrdinal = row.WindowOrdinal
				};
				KingdomPolityCohortPlan existing = KingdomPolityAuthority.Cohort(
					System.PolityLedger, row.CohortId);
				if (existing != null && (existing.Phase == KingdomPolityCohortPhase.Cancelled ||
					existing.Phase == KingdomPolityCohortPhase.Cleaned ||
					existing.Phase == KingdomPolityCohortPhase.Abandoned ||
					existing.Phase == KingdomPolityCohortPhase.Archived))
				{
					if (!TryCompleteTerminalDue(state, row, existing, out Failure)) return false;
					continue;
				}
				if (existing == null && !KingdomPolityAttentionRules.TryAdmitPlan(
					System.PolityLedger, row.MemberCount, out string _))
				{
					if (!KingdomPolityDispatchRules.TryRecordCapacityFallback(state,
						state.Revision, row,
						out KingdomPolityDirectRecord _, out Failure)) return false;
					continue;
				}
				if (!KingdomPolityExperienceRuntime.TryReserveAmbientPlan(System, row.CohortId,
					row.SettlementId, row.MemberCount, row.CauseTick, Tick,
					out KingdomPolityPresentationAuthorityProof authority, out bool _,
					out KingdomExperienceCapacityFault fault, out Failure))
				{
					if (KingdomPolityExperienceRuntime.ExpectedCapacityRefusal(fault))
					{
						Failure = null;
						if (KingdomPolityExperienceRuntime.CapacityRefusalNeedsDirectRecord(fault))
						{
							if (!KingdomPolityDispatchRules.TryRecordCapacityFallback(state,
								state.Revision, row,
								out KingdomPolityDirectRecord _, out Failure)) return false;
						}
						else if (!KingdomPolityDispatchRules.TryComplete(state, state.Revision,
							row.WindowOrdinal, row.EndpointOrdinal, out Failure)) return false;
						continue;
					}
					return false;
				}
				request.PresentationAuthority = authority;
				if (!KingdomPolityCohortRules.TryPlan(System.PolityLedger,
					System.PolityLedger.Revision, request,
					out KingdomPolityPublicationResult _, out Failure))
				{
					string planFailure = Failure;
					if (existing == null && !KingdomPolityExperienceRuntime.TryReleaseAmbient(System,
						row.CohortId, out string releaseFailure)) Failure = planFailure +
						"; presentation rollback failed: " + releaseFailure;
					else Failure = planFailure;
					return false;
				}
				if (!KingdomPolityDispatchRules.TryComplete(state, state.Revision,
					row.WindowOrdinal, row.EndpointOrdinal, out Failure)) return false;
			}
			return ReconcileLoadedEndpoint(System, Tick, EnabledForNewCauses, out Failure);
		}

		internal static void WitnessDeath(KingdomSystem System, string CohortId, long Tick)
		{
			if (!KingdomPolityEndpointRuntime.TryReplayDeathIntents(System, CohortId,
				out string failure)) KingdomLog.Log(
				"polity: scheduled death intent replay refused (" + failure + ")");
		}

		private static bool RetireOld(KingdomSystem System, long Tick, ulong Window,
			out string Failure)
		{
			KingdomPolityLedger L = System.PolityLedger;
			Failure = null; List<string> ids = new List<string>();
			List<string> departures = new List<string>();
			if (!KingdomPolityLoadedEndpointRuntime.TryObserve(System, out XRL.World.Zone _,
				out string loadedSettlementId, out bool endpointAvailable, out Failure)) return false;
			if (!endpointAvailable) loadedSettlementId = null;
			for (int i = 0; i < L.Cohorts.Count; i++)
				if (KingdomPolityDispatchRules.IsScheduled(L.Cohorts[i]) &&
					L.Cohorts[i].Phase == KingdomPolityCohortPhase.Planned &&
					string.IsNullOrEmpty(L.Cohorts[i].ManifestationReceiptId) &&
					KingdomPolityDispatchRules.Expired(L.Cohorts[i], Tick)) ids.Add(
						L.Cohorts[i].CohortId);
				else if (KingdomPolityDispatchRules.IsScheduled(L.Cohorts[i]) &&
					L.Cohorts[i].Phase == KingdomPolityCohortPhase.Materialized &&
					L.Cohorts[i].SurfaceRef == loadedSettlementId &&
					KingdomPolityDispatchRules.Expired(L.Cohorts[i], Tick)) departures.Add(
						L.Cohorts[i].CohortId);
			for (int i = 0; i < ids.Count; i++)
			{
				if (!KingdomPolityCohortRules.TryCancelExpiredScheduled(L, L.Revision,
					ids[i], Tick, out KingdomPolityPublicationResult _, out Failure) ||
					!KingdomPolityExperienceRuntime.TryReleaseAmbient(System, ids[i],
						out Failure)) return false;
			}
			for (int i = 0; i < departures.Count; i++)
			{
				KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(L, departures[i]);
				if (!KingdomPolityEndpointRuntime.TryReplayDeathIntents(System, cohort.CohortId,
					out Failure)) return false;
				cohort = KingdomPolityAuthority.Cohort(L, departures[i]);
				if (cohort.Phase == KingdomPolityCohortPhase.Abandoned ||
					cohort.Phase == KingdomPolityCohortPhase.Concluded) continue;
				if (!KingdomPolityEndpointRuntime.TryProveMaterializedLifecycleAfterDeathReplay(
					System, cohort.CohortId, out Failure)) return false;
				if (!KingdomPolityCohortRules.TryConcludeScheduledStay(L, L.Revision,
					cohort.CohortId, cohort.SurfaceRef, Tick,
					out KingdomPolityPublicationResult _, out Failure)) return false;
			}
			return KingdomPolityCohortRules.TryPruneScheduledTerminals(L, L.Revision,
				Window, out KingdomPolityPublicationResult _, out Failure);
		}

		private static bool ReconcileLoadedEndpoint(KingdomSystem S, long Tick,
			bool Enabled, out string Failure)
		{
			Failure = null;
			if (!KingdomPolityLoadedEndpointRuntime.TryObserve(S, out XRL.World.Zone _,
				out string loadedSettlementId, out bool available, out Failure)) return false;
			if (!available) return true;
			List<string> ids = new List<string>();
			for (int i = 0; i < S.PolityLedger.Cohorts.Count; i++)
				if (KingdomPolityDispatchRules.IsScheduled(S.PolityLedger.Cohorts[i]) &&
					S.PolityLedger.Cohorts[i].SurfaceRef == loadedSettlementId)
					ids.Add(S.PolityLedger.Cohorts[i].CohortId);
			for (int i = 0; i < ids.Count; i++)
			{
					KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(
						S.PolityLedger, ids[i]);
					if (!string.IsNullOrEmpty(cohort.ManifestationReceiptId) &&
						(cohort.Phase == KingdomPolityCohortPhase.Materialized ||
						 cohort.Phase == KingdomPolityCohortPhase.Concluded ||
						 cohort.Phase == KingdomPolityCohortPhase.Abandoned) &&
						!KingdomPolityEndpointRuntime.TryReplayDeathIntents(S,
							cohort.CohortId, out Failure)) return false;
					cohort = KingdomPolityAuthority.Cohort(S.PolityLedger, ids[i]);
					bool expired = KingdomPolityDispatchRules.Expired(cohort, Tick);
				bool firstProjection = string.IsNullOrEmpty(cohort.ManifestationReceiptId);
				if (cohort.Phase == KingdomPolityCohortPhase.Planned &&
					(!string.IsNullOrEmpty(cohort.ManifestationReceiptId) || (!expired && Enabled &&
					 KingdomPolityRules.CanEmitOptionalProjection(S.PolityLedger,
						WindowStart(cohort)))))
				{
					if (!KingdomPolityAttentionRules.TryAdmitManifestation(S.PolityLedger,
						cohort, out string _)) continue;
					if (!KingdomPolityEndpointRuntime.TryManifestCurrentEndpoint(S,
						cohort.CohortId, Tick, out Failure)) return false;
					cohort = KingdomPolityAuthority.Cohort(S.PolityLedger, ids[i]);
					if (firstProjection && cohort.Phase == KingdomPolityCohortPhase.Materialized)
						Present(S, cohort, loadedSettlementId);
				}
				if (cohort.Phase == KingdomPolityCohortPhase.Materialized && expired &&
					(!KingdomPolityEndpointRuntime.TryProveMaterializedLifecycleAfterDeathReplay(
						S, cohort.CohortId, out Failure) ||
					 !KingdomPolityCohortRules.TryConcludeScheduledStay(S.PolityLedger,
						S.PolityLedger.Revision, cohort.CohortId, cohort.SurfaceRef, Tick,
						out KingdomPolityPublicationResult _, out Failure))) return false;
				cohort = KingdomPolityAuthority.Cohort(S.PolityLedger, ids[i]);
					if ((cohort.Phase == KingdomPolityCohortPhase.Concluded ||
						cohort.Phase == KingdomPolityCohortPhase.Abandoned) &&
						!KingdomPolityEndpointRuntime.TryCleanupCurrentEndpoint(S,
						cohort.CohortId, out Failure)) return false;
			}
			return true;
		}

		/// <summary>Consumes due work only after durable terminal evidence proves no new plan exists.</summary>
		private static bool TryCompleteTerminalDue(KingdomPolityDispatchState State,
			KingdomPolityDueWork Work, KingdomPolityCohortPlan Cohort, out string Failure)
		{
			Failure = null;
			if (Work == null || Cohort == null || Cohort.CohortId != Work.CohortId ||
					(Cohort.Phase != KingdomPolityCohortPhase.Cancelled &&
					 Cohort.Phase != KingdomPolityCohortPhase.Cleaned &&
					 Cohort.Phase != KingdomPolityCohortPhase.Abandoned &&
					 Cohort.Phase != KingdomPolityCohortPhase.Archived))
			{
				Failure = "due polity work lacks exact terminal evidence"; return false;
			}
			return KingdomPolityDispatchRules.TryComplete(State, State.Revision,
				Work.WindowOrdinal, Work.EndpointOrdinal, out Failure);
		}

		private static void Present(KingdomSystem S, KingdomPolityCohortPlan Cohort,
			string LoadedSettlementId)
		{
			string verb = KingdomPolityDispatchRules.EndpointVerb(Cohort.Purpose);
			if (string.IsNullOrEmpty(verb)) return;
			KingdomPolityRecord polity = KingdomPolityAuthority.Polity(S.PolityLedger,
				Cohort.PolityId);
			bool external = polity != null && polity.Source != KingdomPolitySource.CurrentRealm;
			string company = external ? "a company of {{C|" +
				KingdomPresentation.Rich(polity.DisplayName) + "}} " : "the realm's company ";
			if (external && Cohort.Purpose == KingdomPolityCohortPurpose.Patrol)
				verb = "is sighted at the boundary";
			XRL.Messages.MessageQueue.AddPlayerMessage("{{C|" + KingdomPresentation.Rich(
				EndpointName(S, LoadedSettlementId)) + "}}: " + company + verb + ".");
		}

		private static string EndpointName(KingdomSystem S, string SettlementId)
		{
			if (S.TryFindSettlement(SettlementId, out bool seated,
				out KingdomSettlement settlement))
			{
				if (seated) return S.SeatName;
				if (!string.IsNullOrEmpty(settlement?.SettlementName))
					return settlement.SettlementName;
			}
			return S.KingdomDisplayName;
		}

		private static long WindowStart(KingdomPolityCohortPlan C)
		{
			return C.EventOrdinal > (ulong)(long.MaxValue / KingdomPolityDispatchRules.PeriodTicks)
				? long.MaxValue : (long)C.EventOrdinal * KingdomPolityDispatchRules.PeriodTicks;
		}

	}
}
