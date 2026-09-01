using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal sealed class KingdomPolityRetirementGroundLocator
	{
		internal readonly string ZoneId;
		internal readonly string SettlementId;

		internal KingdomPolityRetirementGroundLocator(string ZoneId, string SettlementId)
		{
			this.ZoneId = ZoneId;
			this.SettlementId = SettlementId;
		}
	}

	/// <summary>Read-only realm-removal fence for polity-owned durable work.</summary>
	public static partial class KingdomPolityRemovalRules
	{
		public static bool TryDescribeRealmRemovalBlocker(
			KingdomPolityDispatchState Dispatch, KingdomPolityRealmTransition Transition,
			out string Blocker, out string Failure)
		{
			Blocker = null;
			Failure = null;
			if (Dispatch == null || Transition == null)
				return Fail("polity removal authority is absent", out Failure);
			if (!KingdomPolityDispatchRules.ValidState(Dispatch, out string dispatchFailure))
			{
				Blocker = "Polity dispatch authority is malformed: " + dispatchFailure;
				return true;
			}
			if (!string.IsNullOrEmpty(Dispatch.Fault))
			{
				Blocker = "Polity dispatch authority is quarantined: " + Dispatch.Fault;
				return true;
			}
			if (Dispatch.HasWindow && Dispatch.CompletedMask != CompleteMask(Dispatch.EndpointCount))
			{
				Blocker = "A polity settlement-dispatch window still has uncommitted endpoint work.";
				return true;
			}
			if (!KingdomPolityRules.TryValidateRealmTransition(Transition,
				out string transitionFailure))
			{
				Blocker = "Polity realm-transition authority is malformed or quarantined: " +
					transitionFailure;
				return true;
			}
			if (Transition.Phase != KingdomPolityRealmTransitionPhase.None &&
				Transition.Phase != KingdomPolityRealmTransitionPhase.Rebound)
			{
				Blocker = "A polity exile/return transition still owns rollback or faction work.";
			}
			return true;
		}

		/// <summary>Classifies ledger-owned work for an already-consented attended retirement.
		/// Safe rows still require exact ground/final reconciliation; this method never mutates.</summary>
		internal static bool TryDescribeRealmRemovalBlocker(KingdomPolityLedger Ledger,
			KingdomPolityDispatchState Dispatch, KingdomPolityRealmTransition Transition,
			IList<KingdomPolityRetirementGroundLocator> Locators,
			out string Blocker, out string Failure)
		{
			Blocker = null; Failure = null;
			if (Ledger == null || !ValidLocators(Locators, out Failure) ||
				!KingdomPolityRules.TryValidate(Ledger, out Failure)) return false;
			if (!KingdomPolityDispatchRules.ValidState(Dispatch, out string dispatchFailure))
			{
				Blocker = "Polity dispatch authority is malformed: " + dispatchFailure; return true;
			}
			if (Dispatch.RealmId != null && Dispatch.RealmId != Ledger.RealmId ||
				!string.IsNullOrEmpty(Dispatch.Fault))
			{
				Blocker = "Polity dispatch authority is foreign or quarantined: " +
					(Dispatch.Fault ?? Dispatch.RealmId); return true;
			}
			if (!KingdomPolityRules.TryValidateRealmTransition(Transition,
				out string transitionFailure))
			{
				Blocker = "Polity realm-transition authority is malformed or quarantined: " +
					transitionFailure; return true;
			}
			if (Transition.Phase != KingdomPolityRealmTransitionPhase.None &&
				Transition.Phase != KingdomPolityRealmTransitionPhase.Rebound)
			{
				Blocker = "A polity exile/return transition still owns rollback or faction work.";
				return true;
			}
			for (int i = 0; i < Ledger.Routes.Count; i++)
			{
					KingdomPolityRouteRecord route = Ledger.Routes[i];
					if (route.Phase == KingdomPolityRoutePhase.Preparing ||
						route.Phase == KingdomPolityRoutePhase.Returned ||
						route.Phase == KingdomPolityRoutePhase.Cancelled ||
						route.Phase == KingdomPolityRoutePhase.AvailableToWitness &&
							AbandonedRoute(Ledger, route)) continue;
				Blocker = "Polity route " + route.RouteId + " retains " + route.Phase +
					" custody; resolve its named endpoint " + route.DestinationId + "."; return true;
			}
			for (int i = 0; i < Ledger.Cohorts.Count; i++)
				if (!SafeCohort(Ledger, Ledger.Cohorts[i], Locators, out Blocker)) return true;
			for (int i = 0; i < Ledger.Incidents.Count; i++)
				if (!SafeIncident(Ledger, Ledger.Incidents[i], out Blocker)) return true;
			for (int i = 0; i < Ledger.Projections.Count; i++)
				if (!SafeProjection(Ledger, Ledger.Projections[i], out Blocker)) return true;
			return true;
		}

		private static bool AbandonedRoute(KingdomPolityLedger Ledger,
			KingdomPolityRouteRecord Route)
		{
			KingdomPolityCohortPlan match = null;
			for (int i = 0; Ledger != null && Route != null && i < Ledger.Cohorts.Count; i++)
			{
				KingdomPolityCohortPlan cohort = Ledger.Cohorts[i];
				if (cohort.SourceRef != Route.RouteId || cohort.Purpose !=
					KingdomPolityCohortPurpose.Envoy) continue;
				if (match != null) return false;
				match = cohort;
			}
			KingdomPolityProjectionReceipt receipt = match == null ? null :
				KingdomPolityAuthority.Projection(Ledger, match.ManifestationReceiptId);
			return match?.Phase == KingdomPolityCohortPhase.Abandoned &&
				string.IsNullOrEmpty(match.RewardEventId) && receipt?.Phase ==
					KingdomPolityProjectionPhase.Cleaned;
		}

		private static bool SafeCohort(KingdomPolityLedger Ledger,
			KingdomPolityCohortPlan Cohort, IList<KingdomPolityRetirementGroundLocator> Locators,
			out string Blocker)
		{
			Blocker = null;
			if ((Cohort.Phase == KingdomPolityCohortPhase.Planned ||
				Cohort.Phase == KingdomPolityCohortPhase.Cancelled) &&
				string.IsNullOrEmpty(Cohort.ManifestationReceiptId)) return true;
			KingdomPolityProjectionReceipt receipt = KingdomPolityAuthority.Projection(Ledger,
				Cohort.ManifestationReceiptId);
			bool terminal = Cohort.Phase == KingdomPolityCohortPhase.Cleaned ||
				Cohort.Phase == KingdomPolityCohortPhase.Abandoned ||
				Cohort.Phase == KingdomPolityCohortPhase.Archived;
			if (terminal && receipt != null && (receipt.Phase ==
				KingdomPolityProjectionPhase.Cleaned || receipt.Phase ==
				KingdomPolityProjectionPhase.Archived)) return true;
			bool prepared = Cohort.Phase == KingdomPolityCohortPhase.Planned && receipt != null &&
				receipt.Phase == KingdomPolityProjectionPhase.Prepared;
			bool committed = (Cohort.Phase == KingdomPolityCohortPhase.Materialized ||
				Cohort.Phase == KingdomPolityCohortPhase.Concluded ||
				Cohort.Phase == KingdomPolityCohortPhase.Abandoned) && receipt != null &&
				receipt.Phase == KingdomPolityProjectionPhase.Committed;
			if ((prepared || committed) && ExactLocator(Locators, receipt.ZoneId,
				Cohort.SurfaceRef)) return true;
			Blocker = "Polity cohort " + Cohort.CohortId +
				" has unresolved or ambiguous endpoint custody at " +
				(receipt?.ZoneId ?? Cohort.SurfaceRef) + "."; return false;
		}

		private static bool SafeIncident(KingdomPolityLedger Ledger,
			KingdomPolityIncidentRecord Incident, out string Blocker)
		{
			Blocker = null;
			if (Incident.Conclusion != null) return true;
			string locator = Incident.EligibleSurfaceRefs.Count == 0 ? Incident.IncidentPlanId :
				Incident.EligibleSurfaceRefs[0];
			if (KingdomPolityCorrespondenceRules.IsConsignmentPlan(Incident) ||
				Incident.Hospitality != null || Incident.Intervention != null ||
				Incident.Aftermath != null || HasLiveEscrow(Ledger, Incident.IncidentPlanId))
			{
				Blocker = "Polity incident " + Incident.IncidentPlanId +
					" retains Trade, hospitality, intervention, or escrow custody at " + locator + ".";
				return false;
			}
			return true;
		}

		private static bool SafeProjection(KingdomPolityLedger Ledger,
			KingdomPolityProjectionReceipt Projection, out string Blocker)
		{
			Blocker = null;
			if (Projection.Kind == KingdomPolityProjectionKind.CohortManifestation) return true;
			if (Projection.Kind == KingdomPolityProjectionKind.ConsentedEscrow &&
				Projection.Phase != KingdomPolityProjectionPhase.Cleaned &&
				Projection.Phase != KingdomPolityProjectionPhase.Archived &&
				Projection.Phase != KingdomPolityProjectionPhase.Cancelled)
			{
				Blocker = "Polity consented escrow " + Projection.ProjectionId +
					" retains exact custody at " + Projection.ZoneId + "."; return false;
			}
			if ((Projection.Kind == KingdomPolityProjectionKind.RoutePrompt ||
				Projection.Kind == KingdomPolityProjectionKind.IncidentView) &&
				Projection.ObjectIds.Count != 0)
			{
				Blocker = "Polity projection " + Projection.ProjectionId +
					" retains unexpected physical custody at " + Projection.ZoneId + "."; return false;
			}
			return true;
		}

		private static bool HasLiveEscrow(KingdomPolityLedger Ledger, string IncidentPlanId)
		{
			for (int i = 0; i < Ledger.Projections.Count; i++)
				if (Ledger.Projections[i].Kind == KingdomPolityProjectionKind.ConsentedEscrow &&
					Ledger.Projections[i].SourceRef == IncidentPlanId &&
					Ledger.Projections[i].Phase != KingdomPolityProjectionPhase.Cleaned &&
					Ledger.Projections[i].Phase != KingdomPolityProjectionPhase.Archived &&
					Ledger.Projections[i].Phase != KingdomPolityProjectionPhase.Cancelled) return true;
			return false;
		}

		private static bool ExactLocator(IList<KingdomPolityRetirementGroundLocator> Values,
			string ZoneId, string SettlementId)
		{
			for (int i = 0; i < Values.Count; i++) if (Values[i].ZoneId == ZoneId &&
				Values[i].SettlementId == SettlementId) return true;
			return false;
		}

		private static bool ValidLocators(IList<KingdomPolityRetirementGroundLocator> Values,
			out string Failure)
		{
			Failure = null;
			if (Values == null) return Fail("polity retirement ground locators are absent", out Failure);
			string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolityRetirementGroundLocator row = Values[i];
				if (row == null || !KingdomPolityRules.Text(row.ZoneId, true) ||
					!KingdomPolityRules.TypedId(row.SettlementId, "taf:settlement:v1:") ||
					(previous != null && string.CompareOrdinal(previous, row.ZoneId) >= 0))
					return Fail("polity retirement ground locators are invalid or noncanonical",
						out Failure);
				previous = row.ZoneId;
			}
			return true;
		}

		private static int CompleteMask(int EndpointCount)
		{
			return EndpointCount <= 0 ? 0 : (1 << EndpointCount) - 1;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
