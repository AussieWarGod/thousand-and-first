using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		private static bool ValidateTrafficState(KingdomPolityLedger L, out string Failure)
		{
			if (!ValidateRoutes(L.Routes, out Failure)) return false;
			if (!ValidateGrievances(L.Grievances, out Failure)) return false;
			if (!ValidateFronts(L.Fronts, out Failure)) return false;
			return ValidateCohorts(L, out Failure);
		}

		private static bool ValidateRoutes(IList<KingdomPolityRouteRecord> Values,
			out string Failure)
		{
			Failure = null; string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolityRouteRecord r = Values[i];
				if (r == null || !TypedId(r.RouteId, "taf:route:") || !After(previous, r.RouteId) ||
					!TypedId(r.EventStreamId, "taf:stream:") || !SemanticId(r.OriginId) ||
					!SemanticId(r.DestinationId) || r.OriginId == r.DestinationId ||
					!ValidOrderedPath(r.OrderedPath, r.OriginId, r.DestinationId) ||
					!Defined((byte)r.Mode, 4) || r.Mode == KingdomPolityRouteMode.None ||
					!Defined((byte)r.Purpose, 5) || r.Purpose == KingdomPolityRoutePurpose.None ||
					!Defined((byte)r.Phase, 7) || r.DepartureTick < 0L || r.NextDueTick < 0L ||
					r.SegmentIndex < 0 || r.SegmentIndex >= r.OrderedPath.Count ||
					!OptionalId(r.ManifestOrErrandId) || !OptionalId(r.CounterpartyRef) ||
					!OptionalId(r.FrontId) || !OptionalId(r.DepartureReceiptId) ||
					!OptionalId(r.DeliveryReceiptId) || !OptionalId(r.ReturnReceiptId) ||
					!OptionalId(r.ActiveManifestationId))
					return Fail("route record is invalid or noncanonical", out Failure);
				previous = r.RouteId;
				if (r.Phase == KingdomPolityRoutePhase.Preparing &&
					(r.DepartureTick != 0L || r.SegmentIndex != 0 ||
					 !string.IsNullOrEmpty(r.DepartureReceiptId)))
					return Fail("preparing route carries departure evidence", out Failure);
				if (r.Phase != KingdomPolityRoutePhase.Preparing &&
					r.Phase != KingdomPolityRoutePhase.Cancelled &&
					string.IsNullOrEmpty(r.DepartureReceiptId))
					return Fail("departed route lacks receipt", out Failure);
				if ((r.Phase == KingdomPolityRoutePhase.Arrived ||
					r.Phase == KingdomPolityRoutePhase.Returned) &&
					string.IsNullOrEmpty(r.DeliveryReceiptId))
					return Fail("arrived route lacks delivery receipt", out Failure);
				if (r.Phase == KingdomPolityRoutePhase.Returned &&
					string.IsNullOrEmpty(r.ReturnReceiptId))
					return Fail("returned route lacks return receipt", out Failure);
			}
			return true;
		}

		private static bool ValidateGrievances(IList<KingdomPolityGrievanceRecord> Values,
			out string Failure)
		{
			Failure = null; string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolityGrievanceRecord g = Values[i];
				if (g == null || !TypedId(g.GrievanceId, "taf:grievance:") ||
					!After(previous, g.GrievanceId) || !SemanticId(g.IssuerPolityId) ||
					!SemanticId(g.TargetPolityId) || g.IssuerPolityId == g.TargetPolityId ||
					!Defined((byte)g.Cause, 8) || g.Cause == KingdomPolityGrievanceCause.None ||
					!SemanticId(g.SourceEventId) || g.Severity < 1 || g.Severity > 5 ||
					!SortedSemanticRefs(g.EvidenceRefs, MaxRefs, true) ||
					!Defined((byte)g.Phase, 3) || !OptionalId(g.ConsumedByIncidentId) ||
					!OptionalId(g.ResolutionRef))
					return Fail("grievance record is invalid or noncanonical", out Failure);
				previous = g.GrievanceId;
				for (int j = 0; j < i; j++) if (Values[j].SourceEventId == g.SourceEventId)
					return Fail("source event emitted more than one grievance", out Failure);
				if (g.Phase == KingdomPolityGrievancePhase.Open &&
					(!string.IsNullOrEmpty(g.ConsumedByIncidentId) ||
					 !string.IsNullOrEmpty(g.ResolutionRef)))
					return Fail("open grievance carries terminal evidence", out Failure);
				if (g.Phase == KingdomPolityGrievancePhase.Consumed &&
					string.IsNullOrEmpty(g.ConsumedByIncidentId))
					return Fail("consumed grievance lacks incident", out Failure);
				if ((g.Phase == KingdomPolityGrievancePhase.Resolved ||
					g.Phase == KingdomPolityGrievancePhase.Withdrawn) &&
					string.IsNullOrEmpty(g.ResolutionRef))
					return Fail("closed grievance lacks resolution", out Failure);
			}
			return true;
		}

		private static bool ValidateFronts(IList<KingdomPolityFrontRecord> Values,
			out string Failure)
		{
			Failure = null; string previous = null; int active = 0;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolityFrontRecord f = Values[i];
				if (f == null || !TypedId(f.FrontId, "taf:front:") ||
					!After(previous, f.FrontId) || !Defined((byte)f.TargetKind, 4) ||
					f.TargetKind == KingdomPolityFrontTarget.None || !SemanticId(f.TargetRef) ||
					f.PressureBand < 0 || f.PressureBand > 5 || f.NextDueEventTick < 0L ||
					!SortedSemanticRefs(f.GrievanceRefs, MaxRefs,
						f.Phase != KingdomPolityFrontPhase.Quiet) ||
					!Defined((byte)f.Phase, 5))
					return Fail("front record is invalid or noncanonical", out Failure);
				previous = f.FrontId;
				if (f.Phase == KingdomPolityFrontPhase.Friction ||
					f.Phase == KingdomPolityFrontPhase.Contested ||
					f.Phase == KingdomPolityFrontPhase.ConfrontationAvailable) active++;
			}
			if (active > MaxActiveFronts) return Fail("active front capacity is exceeded", out Failure);
			return true;
		}

		private static bool ValidateCohorts(KingdomPolityLedger Ledger, out string Failure)
		{
			IList<KingdomPolityCohortPlan> Values = Ledger.Cohorts;
			Failure = null; string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolityCohortPlan c = Values[i];
				if (c == null || !TypedId(c.CohortId, "taf:cohort:") ||
					!After(previous, c.CohortId) || !Defined((byte)c.Purpose, 7) ||
					c.Purpose == KingdomPolityCohortPurpose.None || !SemanticId(c.SourceRef) ||
					!SemanticId(c.PolityId) || !TypedId(c.ProfileId, "taf:polity-profile:") ||
					c.ProfileRevision < 1 || c.MinimumLevel < 1 ||
					c.MaximumLevel < c.MinimumLevel || c.MaximumLevel > MaxLevel ||
					!SemanticId(c.SurfaceRef) || c.ScaleBudget < 1 ||
					c.ScaleBudget > MaxCohortMembers || !SortedText(c.RoleSlots,
						MaxCohortMembers, true) || !ValidMembers(c.ResolvedMembers) ||
					c.NamedRepresentativeAllowance < 0 || c.NamedRepresentativeAllowance > 1 ||
					!TypedId(c.EventStreamId, "taf:stream:") || c.RulesVersion < 1 ||
					!ValidPresentationAuthority(c, Ledger.MigratedFromVersion > 0) ||
					!Defined((byte)c.Phase, 6) || !OptionalId(c.ManifestationReceiptId) ||
					!OptionalId(c.RewardEventId))
					return Fail("cohort plan is invalid or noncanonical", out Failure);
				previous = c.CohortId;
				if (c.ScaleBudget != c.ResolvedMembers.Count)
					return Fail("cohort scale does not match pinned members", out Failure);
				if (c.Phase != KingdomPolityCohortPhase.Planned &&
					c.Phase != KingdomPolityCohortPhase.Cancelled &&
					string.IsNullOrEmpty(c.ManifestationReceiptId))
					return Fail("manifested cohort lacks receipt", out Failure);
				if (c.Phase == KingdomPolityCohortPhase.Abandoned &&
					!string.IsNullOrEmpty(c.RewardEventId))
					return Fail("abandoned cohort claims a semantic reward or conclusion", out Failure);
			}
			return true;
		}

		private static bool ValidPresentationAuthority(KingdomPolityCohortPlan Cohort,
			bool AllowsLegacyAmbiguity)
		{
			if (Cohort.PresentationOptionKind == KingdomExperienceOptionKind.None)
				return AllowsLegacyAmbiguity && Cohort.PresentationEnableEpoch == 0L &&
					Cohort.PresentationReservedTick == 0L;
			KingdomExperienceOptionKind expected =
				Cohort.Purpose == KingdomPolityCohortPurpose.Envoy ||
				Cohort.Purpose == KingdomPolityCohortPurpose.Warband
					? KingdomExperienceOptionKind.CivicStory
					: KingdomExperienceOptionKind.AmbientUse;
			return Cohort.PresentationOptionKind == expected &&
				Cohort.PresentationEnableEpoch >= 1L && Cohort.PresentationReservedTick >= 0L;
		}

		private static bool ValidMembers(IList<KingdomPolityCohortMember> Values)
		{
			if (Values == null || Values.Count < 1 || Values.Count > MaxCohortMembers) return false;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolityCohortMember m = Values[i];
				if (m == null || m.Ordinal != i || !TypedId(m.MemberKey, "taf:cohort-member:") ||
					!Text(m.BlueprintKey, true) || !Text(m.LoadoutKey, true) ||
					!Text(m.SignatureKey, true)) return false;
			}
			return true;
		}

		private static bool ValidOrderedPath(IList<string> Path, string Origin, string Destination)
		{
			if (Path == null || Path.Count < 2 || Path.Count > MaxPath || Path[0] != Origin ||
				Path[Path.Count - 1] != Destination) return false;
			for (int i = 0; i < Path.Count; i++)
			{
				if (!SemanticId(Path[i])) return false;
				for (int j = 0; j < i; j++) if (Path[i] == Path[j]) return false;
			}
			return true;
		}

		internal static bool OptionalId(string Value)
		{
			return string.IsNullOrEmpty(Value) || SemanticId(Value);
		}
	}
}
