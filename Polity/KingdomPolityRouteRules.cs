using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Semantic route authority. No method creates an actor or loads a zone.</summary>
	public static partial class KingdomPolityRouteRules
	{
		public static bool TryPlan(KingdomPolityLedger Ledger, long ExpectedRevision,
			KingdomPolityRoutePlanRequest Request, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!ValidPlan(Request, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityRouteRecord expected = Record(Request);
			KingdomPolityRouteRecord existing = KingdomPolityAuthority.Route(Ledger, Request.RouteId);
			if (existing != null)
			{
				if (!ExactPlan(existing, expected)) return KingdomPolityAuthority.Refuse(Result,
					"route id already carries different authority", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (Ledger.Routes.Count >= KingdomPolityRules.MaxRoutes)
				return KingdomPolityAuthority.Refuse(Result, "route capacity is exhausted", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			candidate.Routes.Add(expected);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		public static bool TryDepart(KingdomPolityLedger Ledger, long ExpectedRevision,
			string RouteId, long DepartureTick, string DepartureReceiptId,
			KingdomPolityManifestProof Manifest, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || DepartureTick < 0L ||
				!KingdomPolityRules.SemanticId(DepartureReceiptId))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "route departure input is invalid", out Failure);
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(Ledger, RouteId);
			if (route == null || !KingdomPolityManifestRules.IsDepartable(Manifest,
				route.ManifestOrErrandId, out Failure))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "route or exact departure manifest is missing", out Failure);
			if (!string.IsNullOrEmpty(route.DepartureReceiptId))
			{
				if (route.DepartureReceiptId != DepartureReceiptId ||
					route.DepartureTick != DepartureTick)
					return KingdomPolityAuthority.Refuse(Result,
						"route already departed under different evidence", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (route.Phase != KingdomPolityRoutePhase.Preparing ||
				route.NextDueTick < DepartureTick)
				return KingdomPolityAuthority.Refuse(Result,
					"route cannot depart from this phase or schedule", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityRouteRecord changed = KingdomPolityAuthority.Route(candidate, RouteId);
			changed.Phase = KingdomPolityRoutePhase.Traveling;
			changed.DepartureTick = DepartureTick;
			changed.DepartureReceiptId = DepartureReceiptId;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		public static bool TryAdvance(KingdomPolityLedger Ledger, long ExpectedRevision,
			string RouteId, int FromSegmentIndex, long Tick, long NextDueTick,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || Tick < 0L ||
				NextDueTick < Tick || FromSegmentIndex < 0)
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "route step input is invalid", out Failure);
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(Ledger, RouteId);
			int desired = FromSegmentIndex + 1;
			if (route == null || desired >= route.OrderedPath.Count)
				return KingdomPolityAuthority.Refuse(Result, "route step is outside its path", out Failure);
			KingdomPolityRoutePhase desiredPhase = desired == route.OrderedPath.Count - 1
				? KingdomPolityRoutePhase.AvailableToWitness : KingdomPolityRoutePhase.Traveling;
			if (route.SegmentIndex == desired && route.Phase == desiredPhase &&
				route.NextDueTick == NextDueTick)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (route.Phase != KingdomPolityRoutePhase.Traveling ||
				route.SegmentIndex != FromSegmentIndex || Tick < route.NextDueTick)
				return KingdomPolityAuthority.Refuse(Result,
					"route did not reach this semantic segment", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityRouteRecord changed = KingdomPolityAuthority.Route(candidate, RouteId);
			changed.SegmentIndex = desired; changed.Phase = desiredPhase;
			changed.NextDueTick = NextDueTick;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		private static bool ValidPlan(KingdomPolityRoutePlanRequest R, out string Failure)
		{
			Failure = null;
			if (R == null || !KingdomPolityRules.TypedId(R.RouteId, "taf:route:") ||
				!KingdomPolityRules.TypedId(R.EventStreamId, "taf:stream:") ||
				!KingdomPolityRules.SemanticId(R.OriginId) ||
				!KingdomPolityRules.SemanticId(R.DestinationId) || R.OriginId == R.DestinationId ||
				R.Mode == KingdomPolityRouteMode.None || (byte)R.Mode > 4 ||
				R.Purpose == KingdomPolityRoutePurpose.None || (byte)R.Purpose > 5 ||
				R.FirstDueTick < 0L || !KingdomPolityRules.SemanticId(R.ManifestOrErrandId) ||
				!KingdomPolityRules.SemanticId(R.CounterpartyRef) || !ValidPath(R))
			{
				Failure = "semantic route plan is invalid or unbounded"; return false;
			}
			return true;
		}

		private static bool ValidPath(KingdomPolityRoutePlanRequest R)
		{
			if (R.OrderedPath == null || R.OrderedPath.Count < 2 ||
				R.OrderedPath.Count > KingdomPolityRules.MaxPath || R.OrderedPath[0] != R.OriginId ||
				R.OrderedPath[R.OrderedPath.Count - 1] != R.DestinationId) return false;
			for (int i = 0; i < R.OrderedPath.Count; i++)
			{
				if (!KingdomPolityRules.SemanticId(R.OrderedPath[i])) return false;
				for (int j = 0; j < i; j++) if (R.OrderedPath[j] == R.OrderedPath[i]) return false;
			}
			return true;
		}

		private static KingdomPolityRouteRecord Record(KingdomPolityRoutePlanRequest R)
		{
			return new KingdomPolityRouteRecord
			{
				RouteId = R.RouteId, EventStreamId = R.EventStreamId, OriginId = R.OriginId,
				DestinationId = R.DestinationId, OrderedPath = new List<string>(R.OrderedPath),
				Mode = R.Mode, Purpose = R.Purpose, Phase = KingdomPolityRoutePhase.Preparing,
				DepartureOrdinal = R.DepartureOrdinal, SegmentIndex = 0,
				NextDueTick = R.FirstDueTick, ManifestOrErrandId = R.ManifestOrErrandId,
				CounterpartyRef = R.CounterpartyRef
			};
		}

		private static bool ExactPlan(KingdomPolityRouteRecord A, KingdomPolityRouteRecord E)
		{
			if (A.EventStreamId != E.EventStreamId || A.OriginId != E.OriginId ||
				A.DestinationId != E.DestinationId || A.Mode != E.Mode || A.Purpose != E.Purpose ||
				A.DepartureOrdinal != E.DepartureOrdinal || A.ManifestOrErrandId != E.ManifestOrErrandId ||
				A.CounterpartyRef != E.CounterpartyRef || A.OrderedPath.Count != E.OrderedPath.Count)
				return false;
			for (int i = 0; i < A.OrderedPath.Count; i++)
				if (A.OrderedPath[i] != E.OrderedPath[i]) return false;
			return true;
		}
	}
}
