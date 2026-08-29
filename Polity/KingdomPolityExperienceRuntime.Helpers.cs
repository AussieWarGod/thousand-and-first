using System;

namespace ThousandAndFirst
{
	internal static partial class KingdomPolityExperienceRuntime
	{
		private static bool ExactAmbient(KingdomExperienceAudienceReceipt Audience,
			KingdomExperienceBodyReservation Bodies, string RealmId, string SettlementId,
			string CohortId, KingdomExperienceOptionKind Option, int BodyCount,
			long CauseTick, long Epoch, long Tick,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.None; Failure = null;
			if ((Audience != null && !Matches(Audience, RealmId, SettlementId, CohortId,
				Option, CauseTick, Epoch, Tick)) || (Bodies != null && !Matches(Bodies,
				RealmId, SettlementId, CohortId, Option, BodyCount, CauseTick, Epoch, Tick)) ||
				(Audience != null && Bodies != null && Audience.ReservedTick != Bodies.ReservedTick))
			{
				Fault = KingdomExperienceCapacityFault.DuplicateMismatch;
				Failure = "ambient polity presentation evidence is mismatched"; return false;
			}
			return true;
		}

		private static KingdomExperienceAudienceReceipt Audience(string RealmId,
			string SettlementId, string CohortId, KingdomExperienceOptionKind Option,
			long CauseTick, long ReservedTick, long Epoch)
		{
			return new KingdomExperienceAudienceReceipt
			{
				ReservationId = AudienceReservationId(CohortId), RealmId = RealmId,
				SettlementId = SettlementId, SourceId = CohortId,
				Lane = KingdomExperienceLane.PolityCohort, OptionKind = Option,
				CauseTick = CauseTick, ReservedTick = ReservedTick, EnableEpoch = Epoch
			};
		}

		private static KingdomExperienceBodyReservation Bodies(string RealmId,
			string SettlementId, string CohortId, KingdomExperienceOptionKind Option, int BodyCount,
			long CauseTick, long ReservedTick, long Epoch)
		{
			return new KingdomExperienceBodyReservation
			{
				ReservationId = BodyReservationId(CohortId), RealmId = RealmId,
				SettlementId = SettlementId, SourceId = CohortId,
				Lane = KingdomExperienceLane.PolityCohort, OptionKind = Option,
				CauseTick = CauseTick, ReservedTick = ReservedTick, EnableEpoch = Epoch,
				BodyCount = BodyCount
			};
		}

		private static KingdomExperienceAudienceReceipt FindAudience(
			KingdomExperienceLedger Ledger, string CohortId)
		{
			string id = AudienceReservationId(CohortId);
			for (int i = 0; Ledger != null && i < Ledger.Audiences.Count; i++)
				if (Ledger.Audiences[i].ReservationId == id) return Ledger.Audiences[i];
			return null;
		}

		private static KingdomExperienceBodyReservation FindBodies(
			KingdomExperienceLedger Ledger, string CohortId)
		{
			string id = BodyReservationId(CohortId);
			for (int i = 0; Ledger != null && i < Ledger.BodyReservations.Count; i++)
				if (Ledger.BodyReservations[i].ReservationId == id) return Ledger.BodyReservations[i];
			return null;
		}

		private static bool Matches(KingdomExperienceAudienceReceipt Lease, string RealmId,
			string SettlementId, string CohortId, KingdomExperienceOptionKind Option,
			long CauseTick, long Epoch, long Tick)
		{
			return Lease.ReservationId == AudienceReservationId(CohortId) &&
				Lease.RealmId == RealmId && Lease.SettlementId == SettlementId &&
				Lease.SourceId == CohortId && Lease.Lane == KingdomExperienceLane.PolityCohort &&
				Lease.OptionKind == Option && Lease.CauseTick == CauseTick &&
				Lease.EnableEpoch == Epoch && Lease.ReservedTick >= CauseTick &&
				Lease.ReservedTick <= Tick;
		}

		private static bool Matches(KingdomExperienceBodyReservation Lease, string RealmId,
			string SettlementId, string CohortId, KingdomExperienceOptionKind Option, int BodyCount,
			long CauseTick, long Epoch, long Tick)
		{
			return Lease.ReservationId == BodyReservationId(CohortId) &&
				Lease.RealmId == RealmId && Lease.SettlementId == SettlementId &&
				Lease.SourceId == CohortId && Lease.Lane == KingdomExperienceLane.PolityCohort &&
				Lease.OptionKind == Option && Lease.BodyCount == BodyCount &&
				Lease.CauseTick == CauseTick && Lease.EnableEpoch == Epoch &&
				Lease.ReservedTick >= CauseTick && Lease.ReservedTick <= Tick;
		}

		private static bool TryCause(KingdomPolityLedger Ledger, KingdomPolityCohortPlan Cohort,
			out long CauseTick, out string Failure)
		{
			CauseTick = -1L; Failure = null;
			if (Cohort == null) { Failure = "polity projection has no cohort"; return false; }
			bool scheduledCause = Cohort.PresentationOptionKind ==
				KingdomExperienceOptionKind.AmbientUse;
			// Wire-v3 cohorts can lack a mode only long enough to cancel unpresented work.
			if (!scheduledCause && Cohort.PresentationOptionKind ==
				KingdomExperienceOptionKind.None &&
				KingdomPolityDispatchRules.IsScheduled(Cohort)) scheduledCause = true;
			if (scheduledCause)
			{
				CauseTick = Cohort.EventOrdinal > (ulong)(long.MaxValue /
					KingdomPolityDispatchRules.PeriodTicks) ? long.MaxValue :
					(long)Cohort.EventOrdinal * KingdomPolityDispatchRules.PeriodTicks;
				return true;
			}
			KingdomPolityRouteRecord route = Cohort.SourceRef.StartsWith("taf:route:",
				StringComparison.Ordinal) ? KingdomPolityAuthority.Route(Ledger, Cohort.SourceRef) : null;
			for (int i = 0; route == null && Ledger != null && i < Ledger.Routes.Count; i++)
				if (Ledger.Routes[i].EventStreamId == Cohort.EventStreamId) route = Ledger.Routes[i];
			if (route == null || route.DepartureTick < 0L)
			{
				Failure = "directed polity cohort lost its exact causal route"; return false;
			}
			CauseTick = route.DepartureTick; return true;
		}
	}
}
