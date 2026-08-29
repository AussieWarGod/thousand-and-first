using System;

namespace ThousandAndFirst
{
	/// <summary>Deterministic first-contact ids derived only from fresh polity authority.</summary>
	internal sealed class KingdomPolityVisitPlan
	{
		internal KingdomPolityRecord Current;
		internal KingdomPolityRecord Visitor;
		internal KingdomPolityRelation Relation;
		internal KingdomPolityNamedFigureRecord Representative;
		internal KingdomPolityNamedFigureRecord Claimant;
		internal string SurfaceId;
		internal string ExternalEndpointId;
		internal string RouteId;
		internal string StreamId;
		internal string ErrandId;
		internal string ManifestProofId;
		internal string DepartureReceiptId;
		internal string DeliveryReceiptId;
		internal string ReturnReceiptId;
		internal string EnvoyCohortId;
		internal string ClaimEventId;
		internal string GrievanceId;
		internal string WarbandCohortId;
		internal string TermsPlanId;
		internal string TermsIncidentId;
		internal string ClashPlanId;
		internal string ClashIncidentId;
		internal long DepartureTick;
		internal long ArrivalDueTick;

		internal bool HostileContact => Relation.Band == KingdomPolityRelationBand.Rival ||
			Relation.Band == KingdomPolityRelationBand.Hostile;

		internal static bool TryCreate(KingdomPolityLedger Ledger, string SurfaceId, long CauseTick,
			out KingdomPolityVisitPlan Plan, out string Failure)
		{
			Plan = null; Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!KingdomPolityRules.TypedId(SurfaceId, "taf:settlement:v1:") || CauseTick < 0L)
				return false;
			KingdomPolityRecord current = null, visitor = null;
			for (int i = 0; i < Ledger.Polities.Count; i++)
			{
				KingdomPolityRecord polity = Ledger.Polities[i];
				if (polity.Source == KingdomPolitySource.CurrentRealm) current = polity;
				else if (polity.Source == KingdomPolitySource.ImportedLegacy) visitor = polity;
			}
			if (current == null || visitor == null ||
				visitor.Lifecycle != KingdomPolityLifecycle.Active)
			{
				Failure = "active imported polity is unavailable for first contact"; return false;
			}
			KingdomPolityRelation relation = null;
			for (int i = 0; i < Ledger.Relations.Count; i++)
				if (Ledger.Relations[i].FromPolityId == visitor.PolityId &&
					Ledger.Relations[i].ToPolityId == current.PolityId) relation = Ledger.Relations[i];
			if (relation == null || relation.Band == KingdomPolityRelationBand.Unspecified)
			{
				Failure = "imported polity has no directed first-contact relation"; return false;
			}
			KingdomPolityProfileRevision profile = KingdomPolityAuthority.Profile(Ledger,
				visitor.ProfileId, visitor.ProfileRevision);
			if (profile == null) { Failure = "visitor profile is missing"; return false; }
			KingdomPolityVisitPlan result = new KingdomPolityVisitPlan
			{
				Current = current, Visitor = visitor, Relation = relation,
				DepartureTick = Math.Max(profile.EffectiveTick, CauseTick)
			};
			result.ExternalEndpointId = Id("taf:site:polity-external:v1:", "endpoint", visitor.PolityId);
			result.RouteId = Id("taf:route:legacy-visit:v1:", "route", visitor.PolityId, current.PolityId);
			KingdomPolityRouteRecord frozenRoute = KingdomPolityAuthority.Route(Ledger, result.RouteId);
			if (frozenRoute != null)
			{
				if (!string.IsNullOrEmpty(frozenRoute.DepartureReceiptId))
					result.DepartureTick = frozenRoute.DepartureTick;
				else if (frozenRoute.NextDueTick < KingdomRules.TicksPerDay)
				{
					Failure = "prepared first-contact route lost its exact departure tick"; return false;
				}
				else result.DepartureTick = frozenRoute.NextDueTick - KingdomRules.TicksPerDay;
			}
			if (!TryAddDay(result.DepartureTick, out result.ArrivalDueTick))
			{
				Failure = "first-contact schedule exceeds the semantic clock"; return false;
			}
			result.SurfaceId = frozenRoute == null ? SurfaceId : frozenRoute.DestinationId;
			if (!KingdomPolityRules.TypedId(result.SurfaceId, "taf:settlement:v1:"))
			{
				Failure = "first-contact route has no exact settlement endpoint"; return false;
			}
			result.StreamId = Id("taf:stream:legacy-visit:v1:", "stream", result.RouteId);
			result.ErrandId = Id("taf:errand:polity-visit:v1:", "errand", result.RouteId);
			result.ManifestProofId = Id("taf:manifest-proof:polity-visit:v1:", "proof", result.RouteId);
			result.DepartureReceiptId = Id("taf:receipt:polity-departure:v1:", "depart", result.RouteId);
			result.DeliveryReceiptId = Id("taf:receipt:polity-delivery:v1:", "deliver", result.RouteId);
			result.ReturnReceiptId = Id("taf:receipt:polity-return:v1:", "return", result.RouteId);
			result.EnvoyCohortId = Id("taf:cohort:legacy-envoy:v1:", "envoy", result.RouteId);
			result.ClaimEventId = Id("taf:event:legacy-claim:v1:", "claim-event", result.RouteId);
			result.GrievanceId = Id("taf:grievance:legacy-claim:v1:", "grievance", result.RouteId);
			result.WarbandCohortId = Id("taf:cohort:legacy-warband:v1:", "warband", result.RouteId);
			result.TermsPlanId = Id("taf:incident-plan:legacy-terms:v1:", "terms-plan", result.RouteId);
			result.TermsIncidentId = Id("taf:incident:legacy-terms:v1:", "terms", result.RouteId);
			result.ClashPlanId = Id("taf:incident-plan:legacy-clash:v1:", "clash-plan", result.RouteId);
			result.ClashIncidentId = Id("taf:incident:legacy-clash:v1:", "clash", result.RouteId);
			SelectFigures(Ledger, result); Plan = result; return true;
		}

		private static void SelectFigures(KingdomPolityLedger L, KingdomPolityVisitPlan P)
		{
			for (int i = 0; i < L.NamedFigures.Count; i++)
			{
				KingdomPolityNamedFigureRecord figure = L.NamedFigures[i];
				if (figure.PolityId != P.Visitor.PolityId ||
					figure.Phase != KingdomPolityFigurePhase.Active || figure.ResidentId != 0) continue;
				if (figure.RoleKey == "claimant") P.Claimant = figure;
				else if (figure.RoleKey == "envoy" || figure.RoleKey == "namesake" ||
					figure.RoleKey == "successor") P.Representative = figure;
			}
		}

		private static string Id(string Prefix, string Kind, params string[] Values)
		{
			string[] input = new string[(Values == null ? 0 : Values.Length) + 1]; input[0] = Kind;
			for (int i = 0; Values != null && i < Values.Length; i++) input[i + 1] = Values[i];
			return KingdomPolityRules.ActivationId(Prefix, "polity-first-contact-v1", input);
		}

		private static bool TryAddDay(long Tick, out long Result)
		{
			Result = 0L;
			if (Tick < 0L || Tick > long.MaxValue - KingdomRules.TicksPerDay) return false;
			Result = Tick + KingdomRules.TicksPerDay; return true;
		}
	}
}
