using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomPolityExperienceRuntime
	{
		internal static bool TryRecover(KingdomSystem System, long Tick,
			bool PresentationEnabled, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded || Tick < 0L) return true;
			if (System.PolityLedger == null ||
				!KingdomPolityRules.TryValidate(System.PolityLedger, out Failure))
				return false;
			bool allowNew = KingdomMaster.NewWorkAllowed(System) && PresentationEnabled;
			if (allowNew && !KingdomExperienceRuntime.TryObserveConfiguredOptions(System, Tick,
				out Failure)) return false;
			if (System.Experience == null)
			{
				if (System.PolityLedger.Cohorts.Count == 0) return true;
				Failure = "polity presentation recovery has no durable authority"; return false;
			}
			if (!KingdomExperienceRules.TryValidate(System.Experience, out Failure)) return false;
			if (!KingdomPolityLoadedEndpointRuntime.TryObserve(System, out XRL.World.Zone _,
				out string loadedSettlementId, out bool endpointAvailable, out Failure)) return false;
			if (!endpointAvailable) loadedSettlementId = null;
			List<string> sources = PresentationSources(System.Experience, out Failure);
			if (sources == null || !ValidatePresentationSources(System, sources, Tick, allowNew,
				out Failure))
				return false;
			List<string> cohorts = new List<string>();
			for (int i = 0; i < System.PolityLedger.Cohorts.Count; i++)
				cohorts.Add(System.PolityLedger.Cohorts[i].CohortId);
			for (int i = 0; i < cohorts.Count; i++)
			{
				KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(
					System.PolityLedger, cohorts[i]);
				if (cohort == null) continue;
				bool deathReplay = cohort.SurfaceRef == loadedSettlementId &&
					!string.IsNullOrEmpty(cohort.ManifestationReceiptId) &&
					(cohort.Phase == KingdomPolityCohortPhase.Materialized ||
					 cohort.Phase == KingdomPolityCohortPhase.Concluded ||
					 cohort.Phase == KingdomPolityCohortPhase.Abandoned);
				if (deathReplay && !KingdomPolityEndpointRuntime.TryReplayDeathIntents(
					System, cohort.CohortId, out Failure)) return false;
				cohort = KingdomPolityAuthority.Cohort(System.PolityLedger, cohorts[i]);
				if (!TryCause(System.PolityLedger, cohort, out long cause, out Failure)) return false;
				bool exactMode = cohort.PresentationOptionKind ==
					KingdomExperienceOptionKind.AmbientUse || cohort.PresentationOptionKind ==
					KingdomExperienceOptionKind.CivicStory;
				KingdomExperienceLeaseState proofState = KingdomExperienceLeaseState.Missing;
				if (exactMode && !KingdomExperienceRules.TryClassifyLeaseProof(System.Experience,
					cohort.PresentationOptionKind, cause, cohort.PresentationReservedTick,
					cohort.PresentationEnableEpoch, out proofState, out Failure)) return false;
				bool current = exactMode && proofState == KingdomExperienceLeaseState.Active && allowNew &&
					KingdomPolityRules.CanEmitOptionalProjection(System.PolityLedger, cause) &&
					KingdomExperienceRules.CanEmit(System.Experience,
						cohort.PresentationOptionKind, cause);
				KingdomPolityLeaseRecoveryAction action =
					KingdomPolityExperienceRecoveryRules.Decide(cohort,
						loadedSettlementId, current);
				if (!TryApplyRecoveryAction(System, cohort, cause, Tick, action, allowNew,
					out Failure)) return false;
			}
			return true;
		}

		private static bool TryApplyRecoveryAction(KingdomSystem System,
			KingdomPolityCohortPlan Cohort, long CauseTick, long Tick,
			KingdomPolityLeaseRecoveryAction Action, bool AllowNew, out string Failure)
		{
			Failure = null;
			switch (Action)
			{
			case KingdomPolityLeaseRecoveryAction.ReleaseTerminal:
				if (HasExactAuthority(Cohort) && !TryReadCohortLeases(System, Cohort,
					CauseTick, Tick,
					out KingdomExperienceAudienceReceipt _,
					out KingdomExperienceBodyReservation _,
					out KingdomExperienceLeaseState _, out Failure)) return false;
				return TryReleaseForCohort(System, Cohort, out Failure);
			case KingdomPolityLeaseRecoveryAction.CancelUnpresented:
				if ((HasExactAuthority(Cohort) && !TryReadCohortLeases(System, Cohort,
					CauseTick, Tick,
					out KingdomExperienceAudienceReceipt _,
					out KingdomExperienceBodyReservation _,
					out KingdomExperienceLeaseState _, out Failure)) ||
					!TryCancelLapsedUnpresented(System, Cohort.CohortId, CauseTick,
						out Failure)) return false;
				Cohort = KingdomPolityAuthority.Cohort(System.PolityLedger, Cohort.CohortId);
				return TryReleaseForCohort(System, Cohort, out Failure);
			case KingdomPolityLeaseRecoveryAction.EnsureCurrentPlan:
				if (!AllowNew)
				{
					Failure = "polity recovery cannot admit a current plan while new work is off";
					return false;
				}
				return TryEnsureCurrentPlanLease(System, Cohort, CauseTick, Tick,
					out KingdomExperienceCapacityFault _, out Failure);
			case KingdomPolityLeaseRecoveryAction.EnsureProjected:
			case KingdomPolityLeaseRecoveryAction.EnsureThenRetainFrozen:
				return TryEnsureProjectedLease(System, Cohort, CauseTick, Tick,
					out KingdomExperienceCapacityFault _, out Failure);
			case KingdomPolityLeaseRecoveryAction.EnsureThenCleanupLoaded:
				if (!TryEnsureProjectedLease(System, Cohort, CauseTick, Tick,
					out KingdomExperienceCapacityFault _, out Failure)) return false;
				return KingdomPolityEndpointRuntime.TryCleanupCurrentEndpoint(System,
					Cohort.CohortId, out Failure);
			case KingdomPolityLeaseRecoveryAction.CleanupAbandonedLoaded:
				return KingdomPolityEndpointRuntime.TryCleanupCurrentEndpoint(System,
					Cohort.CohortId, out Failure);
			case KingdomPolityLeaseRecoveryAction.EnsureThenWithdrawLoaded:
				if (!TryEnsureProjectedLease(System, Cohort, CauseTick, Tick,
					out KingdomExperienceCapacityFault _, out Failure) ||
					!KingdomPolityEndpointRuntime.TryWithdrawCurrentEndpoint(System,
						Cohort.CohortId, Tick, out Failure) ||
					!TryCancelLapsedUnpresented(System, Cohort.CohortId, CauseTick,
						out Failure)) return false;
				Cohort = KingdomPolityAuthority.Cohort(System.PolityLedger, Cohort.CohortId);
				return TryReleaseForCohort(System, Cohort, out Failure);
			default:
				Failure = "polity lease recovery found an incoherent cohort phase"; return false;
			}
		}

		private static bool ValidatePresentationSources(KingdomSystem System,
			List<string> Sources, long Tick, bool AllowNew, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Sources.Count; i++)
			{
				string source = Sources[i];
				if (!TryReconcileOrphanSource(System, source, Tick, AllowNew, out Failure)) return false;
			}
			return true;
		}

		private static bool TryCancelLapsedUnpresented(KingdomSystem System, string CohortId,
			long CauseTick, out string Failure)
		{
			Failure = null; KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(
				System.PolityLedger, CohortId);
			if (cohort == null || cohort.Phase != KingdomPolityCohortPhase.Planned ||
				!string.IsNullOrEmpty(cohort.ManifestationReceiptId)) return true;
			string cancellation = KingdomPolityRules.ActivationId(
				"taf:event:polity-presentation-lapse:v1:", "polity-presentation-lapse-v1",
				cohort.CohortId, CauseTick.ToString(
					global::System.Globalization.CultureInfo.InvariantCulture),
				System.PolityLedger.Options.ObservedTick.ToString(
					global::System.Globalization.CultureInfo.InvariantCulture),
				System.PolityLedger.Options.EnableEpoch.ToString(
					global::System.Globalization.CultureInfo.InvariantCulture));
			return KingdomPolityCohortRules.TryCancelUnpresented(System.PolityLedger,
				System.PolityLedger.Revision, CohortId, cancellation,
				out KingdomPolityPublicationResult _, out Failure);
		}

		private static List<string> PresentationSources(KingdomExperienceLedger Ledger,
			out string Failure)
		{
			Failure = null; List<string> result = new List<string>();
			for (int i = 0; i < Ledger.Audiences.Count; i++)
			{
				KingdomExperienceAudienceReceipt row = Ledger.Audiences[i];
				if (row.Lane != KingdomExperienceLane.PolityCohort) continue;
				if (row.ReservationId != AudienceReservationId(row.SourceId) ||
					!AddSource(result, row.SourceId, out Failure)) return null;
			}
			for (int i = 0; i < Ledger.BodyReservations.Count; i++)
			{
				KingdomExperienceBodyReservation row = Ledger.BodyReservations[i];
				if (row.Lane != KingdomExperienceLane.PolityCohort) continue;
				if (row.ReservationId != BodyReservationId(row.SourceId) ||
					!AddSource(result, row.SourceId, out Failure)) return null;
			}
			result.Sort(global::System.StringComparer.Ordinal); return result;
		}

		private static bool AddSource(List<string> Values, string Source, out string Failure)
		{
			Failure = null;
			if (!KingdomPolityRules.TypedId(Source, "taf:cohort:"))
			{
				Failure = "polity presentation lease names an invalid cohort source"; return false;
			}
			if (!Values.Contains(Source)) Values.Add(Source); return true;
		}
	}
}
