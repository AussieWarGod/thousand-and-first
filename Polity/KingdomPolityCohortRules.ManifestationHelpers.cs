using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCohortRules
	{
		private static bool EligibleEndpoint(KingdomPolityLedger L, KingdomPolityCohortPlan C,
			out string Failure)
		{
			Failure = null;
			if (C == null || C.Phase != KingdomPolityCohortPhase.Planned)
			{
				Failure = "cohort is not a planned finite endpoint party"; return false;
			}
			if (!C.SourceRef.StartsWith("taf:route:", StringComparison.Ordinal)) return true;
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(L, C.SourceRef);
			if (route == null || (route.Phase != KingdomPolityRoutePhase.AvailableToWitness &&
				route.Phase != KingdomPolityRoutePhase.ConfrontationAvailable) ||
				route.OrderedPath[route.SegmentIndex] != C.SurfaceRef ||
				!string.IsNullOrEmpty(route.ActiveManifestationId))
			{
				Failure = "route cohort is not at its exact semantic endpoint"; return false;
			}
			return true;
		}

		private static bool ExactPrepared(KingdomPolityProjectionReceipt A,
			KingdomPolityProjectionReceipt E)
		{
			return A.Kind == E.Kind && A.SourceRef == E.SourceRef && A.ZoneId == E.ZoneId &&
				A.PriorDigest == E.PriorDigest && A.AppliedDigest == E.AppliedDigest &&
				A.PreparedTick == E.PreparedTick && ExactObjectIds(A.ObjectIds, E.ObjectIds) &&
				(A.Phase == KingdomPolityProjectionPhase.Prepared ||
				 A.Phase == KingdomPolityProjectionPhase.Committed ||
				 A.Phase == KingdomPolityProjectionPhase.Cleaned);
		}

		internal static bool ExactEndpointReceipt(KingdomPolityCohortPlan Cohort,
			KingdomPolityProjectionReceipt Receipt, string ZoneId)
		{
			if (Cohort == null || Receipt == null || Receipt.ZoneId != ZoneId ||
				Cohort.ManifestationReceiptId != Receipt.ProjectionId) return false;
			return ExactPrepared(Receipt, PreparedReceipt(Cohort, ZoneId, Receipt.PreparedTick));
		}

		private static bool BoundReceipt(KingdomPolityCohortPlan C,
			KingdomPolityProjectionReceipt R)
		{
			return C != null && R != null && C.ManifestationReceiptId == R.ProjectionId &&
				R.Kind == KingdomPolityProjectionKind.CohortManifestation &&
				R.SourceRef == C.CohortId && R.ObjectIds.Count == C.ResolvedMembers.Count &&
				R.PriorDigest == EmptyDigest;
		}

		private static bool ExactObjectIds(IList<string> Expected, IList<string> Actual)
		{
			if (Expected == null || Actual == null || Expected.Count != Actual.Count) return false;
			string previous = null;
			for (int i = 0; i < Actual.Count; i++)
			{
				if (!KingdomPolityRules.Text(Actual[i], true) ||
					(previous != null && string.CompareOrdinal(previous, Actual[i]) >= 0) ||
					Expected[i] != Actual[i]) return false;
				previous = Actual[i];
			}
			return true;
		}

		private static void BindRouteManifestation(KingdomPolityLedger L,
			KingdomPolityCohortPlan C, string ProjectionId)
		{
			if (!C.SourceRef.StartsWith("taf:route:", StringComparison.Ordinal)) return;
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(L, C.SourceRef);
			if (route != null) route.ActiveManifestationId = ProjectionId;
		}

		private static void ClearRouteManifestation(KingdomPolityLedger L, string ProjectionId)
		{
			for (int i = 0; i < L.Routes.Count; i++)
				if (L.Routes[i].ActiveManifestationId == ProjectionId)
					L.Routes[i].ActiveManifestationId = null;
		}
	}
}
