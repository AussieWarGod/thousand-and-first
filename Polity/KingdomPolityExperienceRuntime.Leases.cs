namespace ThousandAndFirst
{
	/// <summary>Exact active/retirement lease inspection and reconstruction for polity cohorts.</summary>
	internal static partial class KingdomPolityExperienceRuntime
	{
		private static bool TryAssertActiveProjectionLease(KingdomSystem System,
			KingdomPolityCohortPlan Cohort, long CauseTick, long Tick,
			KingdomExperienceOptionKind Expected, out KingdomExperienceCapacityFault Fault,
			out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.InvalidRequest; Failure = null;
			if (!HasExactAuthority(Cohort) || Cohort.PresentationOptionKind != Expected)
			{
				Failure = "polity projection mode does not match its pinned authority"; return false;
			}
			if (!TryReadCohortLeases(System, Cohort, CauseTick, Tick,
				out KingdomExperienceAudienceReceipt audience,
				out KingdomExperienceBodyReservation bodies,
				out KingdomExperienceLeaseState state, out Failure)) return false;
			bool ambient = Expected == KingdomExperienceOptionKind.AmbientUse;
			if (state != KingdomExperienceLeaseState.Active || bodies == null ||
				(ambient && audience == null))
			{
				Failure = "polity projection lacks exact active shared-capacity authority"; return false;
			}
			Fault = KingdomExperienceCapacityFault.None; return true;
		}

		private static bool TryEnsureCurrentPlanLease(KingdomSystem System,
			KingdomPolityCohortPlan Cohort, long CauseTick, long Tick,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.InvalidRequest; Failure = null;
			if (!HasExactAuthority(Cohort))
			{
				Failure = "unpresented legacy cohort has no exact capacity proof"; return false;
			}
			if (!TryReadCohortLeases(System, Cohort, CauseTick, Tick,
				out KingdomExperienceAudienceReceipt audience,
				out KingdomExperienceBodyReservation bodies,
				out KingdomExperienceLeaseState state, out Failure)) return false;
			bool ambient = Cohort.PresentationOptionKind == KingdomExperienceOptionKind.AmbientUse;
			if (bodies != null && (!ambient || audience != null))
			{
				if (state != KingdomExperienceLeaseState.Active)
				{
					Failure = "unpresented polity plan owns only retirement authority"; return false;
				}
				Fault = KingdomExperienceCapacityFault.None; return true;
			}
			BuildRequests(System, Cohort, CauseTick, audience, bodies,
				out KingdomExperienceAudienceReceipt requestedAudience,
				out KingdomExperienceBodyReservation requestedBodies);
			return ambient ? KingdomExperienceRuntime.TryReservePresentation(System,
				requestedAudience, requestedBodies, out Fault, out Failure) :
				KingdomExperienceRuntime.TryReserveBodies(System, requestedBodies,
					out Fault, out Failure);
		}

		private static bool TryEnsureProjectedLease(KingdomSystem System,
			KingdomPolityCohortPlan Cohort, long CauseTick, long Tick,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.InvalidRequest; Failure = null;
			KingdomPolityProjectionReceipt projection = KingdomPolityAuthority.Projection(
				System.PolityLedger, Cohort.ManifestationReceiptId);
			if (projection == null || !KingdomPolityCohortRules.ExactEndpointReceipt(
				Cohort, projection, projection.ZoneId))
			{
				Failure = "projected polity cohort lacks exact endpoint proof"; return false;
			}
			if (!HasExactAuthority(Cohort))
			{
				Failure = "legacy projected cohort lacks exact capacity metadata"; return false;
			}
			if (!TryReadCohortLeases(System, Cohort, CauseTick, Tick,
				out KingdomExperienceAudienceReceipt audience,
				out KingdomExperienceBodyReservation bodies,
				out KingdomExperienceLeaseState _, out Failure)) return false;
			bool ambient = Cohort.PresentationOptionKind == KingdomExperienceOptionKind.AmbientUse;
			if (bodies != null && (!ambient || audience != null))
			{
				Fault = KingdomExperienceCapacityFault.None; return true;
			}
			BuildRequests(System, Cohort, CauseTick, audience, bodies,
				out KingdomExperienceAudienceReceipt requestedAudience,
				out KingdomExperienceBodyReservation requestedBodies);
			return ambient ? KingdomExperienceRuntime.TryRecoverDurablePresentation(System,
				requestedAudience, requestedBodies, Tick, out Fault, out Failure) :
				KingdomExperienceRuntime.TryRecoverDurableBodies(System, requestedBodies,
					Tick, out Fault, out Failure);
		}

		private static void BuildRequests(KingdomSystem System, KingdomPolityCohortPlan Cohort,
			long CauseTick, KingdomExperienceAudienceReceipt AudienceLease,
			KingdomExperienceBodyReservation BodyLease,
			out KingdomExperienceAudienceReceipt RequestedAudience,
			out KingdomExperienceBodyReservation RequestedBodies)
		{
			bool ambient = Cohort.PresentationOptionKind == KingdomExperienceOptionKind.AmbientUse;
			RequestedAudience = ambient ? AudienceLease ?? Audience(System.RealmId,
				Cohort.SurfaceRef, Cohort.CohortId, Cohort.PresentationOptionKind, CauseTick,
				Cohort.PresentationReservedTick, Cohort.PresentationEnableEpoch) : null;
			RequestedBodies = BodyLease ?? Bodies(System.RealmId, Cohort.SurfaceRef,
				Cohort.CohortId, Cohort.PresentationOptionKind, Cohort.ScaleBudget, CauseTick,
				Cohort.PresentationReservedTick, Cohort.PresentationEnableEpoch);
		}

		private static bool TryReadCohortLeases(KingdomSystem System,
			KingdomPolityCohortPlan Cohort, long CauseTick, long Tick,
			out KingdomExperienceAudienceReceipt AudienceLease,
			out KingdomExperienceBodyReservation BodyLease,
			out KingdomExperienceLeaseState State, out string Failure)
		{
			AudienceLease = null; BodyLease = null; State = KingdomExperienceLeaseState.Missing;
			Failure = null;
			if (System?.Experience == null || Cohort == null || CauseTick < 0L || CauseTick > Tick)
			{
				Failure = "polity lease inspection context is invalid"; return false;
			}
			if (!KingdomExperienceRules.TryReadAudienceLease(System.Experience,
				AudienceReservationId(Cohort.CohortId), out AudienceLease,
				out KingdomExperienceLeaseState audienceState, out Failure) ||
				!KingdomExperienceRules.TryReadBodyLease(System.Experience,
					BodyReservationId(Cohort.CohortId), out BodyLease,
					out KingdomExperienceLeaseState bodyState, out Failure)) return false;
			if ((AudienceLease != null && !LeaseShape(AudienceLease, System.RealmId,
				Cohort, CauseTick, Tick)) || (BodyLease != null && !LeaseShape(BodyLease,
					System.RealmId, Cohort, CauseTick, Tick)))
			{
				Failure = "polity cohort lease evidence is mismatched"; return false;
			}
			bool ambient = Cohort.PresentationOptionKind == KingdomExperienceOptionKind.AmbientUse;
			if (!ambient && AudienceLease != null)
			{
				Failure = "directed polity recovery found a forbidden audience lease"; return false;
			}
			if (AudienceLease != null && BodyLease != null && audienceState != bodyState)
			{
				Failure = "ambient polity lease pair has split authority"; return false;
			}
			State = BodyLease != null ? bodyState : audienceState; return true;
		}

		private static bool LeaseShape(KingdomExperienceAudienceReceipt Lease, string RealmId,
			KingdomPolityCohortPlan Cohort, long CauseTick, long Tick)
		{
			return Lease.ReservationId == AudienceReservationId(Cohort.CohortId) &&
				Lease.RealmId == RealmId && Lease.SettlementId == Cohort.SurfaceRef &&
				Lease.SourceId == Cohort.CohortId && Lease.Lane == KingdomExperienceLane.PolityCohort &&
				Lease.OptionKind == Cohort.PresentationOptionKind && Lease.CauseTick == CauseTick &&
				Lease.ReservedTick == Cohort.PresentationReservedTick &&
				Lease.EnableEpoch == Cohort.PresentationEnableEpoch && Lease.ReservedTick <= Tick;
		}

		private static bool LeaseShape(KingdomExperienceBodyReservation Lease, string RealmId,
			KingdomPolityCohortPlan Cohort, long CauseTick, long Tick)
		{
			return Lease.ReservationId == BodyReservationId(Cohort.CohortId) &&
				Lease.RealmId == RealmId && Lease.SettlementId == Cohort.SurfaceRef &&
				Lease.SourceId == Cohort.CohortId && Lease.Lane == KingdomExperienceLane.PolityCohort &&
				Lease.OptionKind == Cohort.PresentationOptionKind &&
				Lease.BodyCount == Cohort.ScaleBudget && Lease.CauseTick == CauseTick &&
				Lease.ReservedTick == Cohort.PresentationReservedTick &&
				Lease.EnableEpoch == Cohort.PresentationEnableEpoch && Lease.ReservedTick <= Tick;
		}

		private static bool HasExactAuthority(KingdomPolityCohortPlan Cohort)
		{
			return Cohort != null && (Cohort.PresentationOptionKind ==
				KingdomExperienceOptionKind.AmbientUse || Cohort.PresentationOptionKind ==
				KingdomExperienceOptionKind.CivicStory) && Cohort.PresentationEnableEpoch >= 1L &&
				Cohort.PresentationReservedTick >= 0L;
		}
	}
}
