namespace ThousandAndFirst
{
	internal static partial class KingdomPolityExperienceRuntime
	{
		/// <summary>Authenticates or releases a W0 cut left before semantic cohort commit.</summary>
		private static bool TryReconcileOrphanSource(KingdomSystem System, string Source,
			long Tick, bool AllowNew, out string Failure)
		{
			Failure = null;
			if (KingdomPolityAuthority.Cohort(System.PolityLedger, Source) != null) return true;
			KingdomExperienceAudienceReceipt audience = FindAudience(System.Experience, Source);
			KingdomExperienceBodyReservation bodies = FindBodies(System.Experience, Source);
			if (bodies == null)
				return FailIntent("polity capacity source has no exact body half; lease retained",
					out Failure);
			if (!KingdomPolityDispatchRules.TryReadPresentationSource(System.PolityDispatch,
				Source, out bool active, out bool terminal, out string settlement,
				out int bodyCount, out long cause, out Failure)) return false;
			if (active || terminal)
			{
				if (audience == null || !Matches(audience, System.RealmId, settlement, Source,
					KingdomExperienceOptionKind.AmbientUse, cause, bodies.EnableEpoch, Tick)
					|| !Matches(bodies, System.RealmId, settlement, Source,
						KingdomExperienceOptionKind.AmbientUse, bodyCount, cause,
						bodies.EnableEpoch, Tick) || audience.ReservedTick != bodies.ReservedTick)
					return FailIntent("ambient polity orphan differs from its dispatch intent; lease retained",
						out Failure);
				return active && AllowNew || TryReleaseAmbient(System, Source, out Failure);
			}
			if (!TryDirectedPlanForLease(System, bodies, audience, Tick,
				out KingdomPolityVisitPlan plan, out Failure)) return false;
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(System.PolityLedger,
				plan.RouteId);
			if (route == null) return AllowNew || TryReleaseDirected(System, Source, out Failure);
			bool terminalRoute = route != null && (route.Phase == KingdomPolityRoutePhase.Returned
				|| route.Phase == KingdomPolityRoutePhase.Cancelled);
			return !terminalRoute || TryReleaseDirected(System, Source, out Failure);
		}

		internal static bool TryPinnedDirectedCause(KingdomSystem System,
			KingdomPolityVisitPlan Plan, long Tick, out long CauseTick, out string Failure)
		{
			CauseTick = Plan?.DepartureTick ?? -1L; Failure = null;
			if (System?.Experience == null || Plan == null) return true;
			KingdomExperienceBodyReservation envoy = FindBodies(System.Experience, Plan.EnvoyCohortId);
			KingdomExperienceBodyReservation warband = FindBodies(System.Experience,
				Plan.WarbandCohortId);
			if (envoy == null && warband == null) return true;
			KingdomExperienceBodyReservation held = envoy ?? warband;
			if (envoy != null && warband != null && envoy.CauseTick != warband.CauseTick)
				return FailIntent("directed polity intents disagree on their causal tick", out Failure);
			if (!TryDirectedPlanForLease(System, held, null, Tick,
				out KingdomPolityVisitPlan _, out Failure)) return false;
			CauseTick = held.CauseTick; return true;
		}

		private static bool TryDirectedPlanForLease(KingdomSystem System,
			KingdomExperienceBodyReservation BodiesLease,
			KingdomExperienceAudienceReceipt AudienceLease,
			long Tick,
			out KingdomPolityVisitPlan Plan, out string Failure)
		{
			Plan = null; Failure = null;
			if (BodiesLease == null || AudienceLease != null
				|| BodiesLease.RealmId != System.RealmId
				|| BodiesLease.Lane != KingdomExperienceLane.PolityCohort
				|| BodiesLease.OptionKind != KingdomExperienceOptionKind.CivicStory
				|| BodiesLease.ReservationId != BodyReservationId(BodiesLease.SourceId)
				|| BodiesLease.CauseTick < 0L || BodiesLease.ReservedTick < BodiesLease.CauseTick
				|| BodiesLease.ReservedTick > Tick || BodiesLease.EnableEpoch < 1L
				|| !KingdomPolityVisitPlan.TryCreate(System.PolityLedger,
					BodiesLease.SettlementId, BodiesLease.CauseTick, out Plan, out Failure))
				return FailIntent(Failure ?? "directed polity orphan is unauthenticated; lease retained",
					out Failure);
			bool envoy = BodiesLease.SourceId == Plan.EnvoyCohortId && BodiesLease.BodyCount == 2;
			bool warband = BodiesLease.SourceId == Plan.WarbandCohortId
				&& BodiesLease.BodyCount == 3 && Plan.HostileContact;
			return envoy || warband || FailIntent(
				"directed polity orphan differs from its deterministic intent; lease retained",
				out Failure);
		}

		private static bool FailIntent(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
