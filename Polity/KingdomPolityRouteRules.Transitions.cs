namespace ThousandAndFirst
{
	public static partial class KingdomPolityRouteRules
	{
		public static bool TryBlock(KingdomPolityLedger Ledger, long ExpectedRevision,
			string RouteId, string FrontId, long Tick, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || Tick < 0L)
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(Ledger, RouteId);
			KingdomPolityFrontRecord front = FindFront(Ledger, FrontId);
			if (!ExactFront(route, front)) return KingdomPolityAuthority.Refuse(Result,
				"route block lacks its exact caused front", out Failure);
			if (route.Phase == KingdomPolityRoutePhase.Blocked)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (route.Phase != KingdomPolityRoutePhase.Traveling &&
				route.Phase != KingdomPolityRoutePhase.AvailableToWitness)
				return KingdomPolityAuthority.Refuse(Result,
					"route cannot pause from this phase", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityAuthority.Route(candidate, RouteId).Phase = KingdomPolityRoutePhase.Blocked;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		public static bool TryResume(KingdomPolityLedger Ledger, long ExpectedRevision,
			string RouteId, long NextDueTick, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || NextDueTick < 0L)
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(Ledger, RouteId);
			KingdomPolityFrontRecord front = route == null ? null : FindFront(Ledger, route.FrontId);
			KingdomPolityRoutePhase desired = route != null &&
				route.SegmentIndex == route.OrderedPath.Count - 1
				? KingdomPolityRoutePhase.AvailableToWitness : KingdomPolityRoutePhase.Traveling;
			if (route != null && route.Phase == desired && route.NextDueTick == NextDueTick)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (route == null || route.Phase != KingdomPolityRoutePhase.Blocked || front == null ||
				(front.Phase != KingdomPolityFrontPhase.Quiet &&
				 front.Phase != KingdomPolityFrontPhase.Truce &&
				 front.Phase != KingdomPolityFrontPhase.Ended))
				return KingdomPolityAuthority.Refuse(Result,
					"route front still prevents semantic travel", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityRouteRecord changed = KingdomPolityAuthority.Route(candidate, RouteId);
			changed.Phase = desired; changed.NextDueTick = NextDueTick;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		public static bool TryOfferConfrontation(KingdomPolityLedger Ledger,
			long ExpectedRevision, string RouteId, string FrontId,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(Ledger, RouteId);
			KingdomPolityFrontRecord front = FindFront(Ledger, FrontId);
			if (!ExactFront(route, front) || front.Phase !=
				KingdomPolityFrontPhase.ConfrontationAvailable)
				return KingdomPolityAuthority.Refuse(Result,
					"route has no caused confrontation to offer", out Failure);
			if (route.Phase == KingdomPolityRoutePhase.ConfrontationAvailable)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (route.Phase != KingdomPolityRoutePhase.Blocked &&
				route.Phase != KingdomPolityRoutePhase.AvailableToWitness)
				return KingdomPolityAuthority.Refuse(Result,
					"route cannot offer confrontation from this phase", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityAuthority.Route(candidate, RouteId).Phase =
				KingdomPolityRoutePhase.ConfrontationAvailable;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		public static bool TryDeliverEntitlement(KingdomPolityLedger Ledger,
			long ExpectedRevision, string RouteId, long Tick, long ReturnDueTick,
			string EntitlementReceiptId, KingdomPolityManifestProof Manifest,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || Tick < 0L ||
				ReturnDueTick < Tick || !KingdomPolityRules.SemanticId(EntitlementReceiptId))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "semantic entitlement input is invalid", out Failure);
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(Ledger, RouteId);
			if (route == null || !KingdomPolityManifestRules.IsSemanticEntitlement(Manifest,
				route.ManifestOrErrandId, out Failure))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "route entitlement manifest is missing", out Failure);
			if (!string.IsNullOrEmpty(route.DeliveryReceiptId))
			{
				if (route.DeliveryReceiptId != EntitlementReceiptId)
					return KingdomPolityAuthority.Refuse(Result,
						"route already records another entitlement", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (route.Phase != KingdomPolityRoutePhase.AvailableToWitness ||
				route.SegmentIndex != route.OrderedPath.Count - 1 || Tick < route.NextDueTick)
				return KingdomPolityAuthority.Refuse(Result,
					"route has not reached its semantic endpoint", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityRouteRecord changed = KingdomPolityAuthority.Route(candidate, RouteId);
			changed.Phase = KingdomPolityRoutePhase.Arrived;
			changed.DeliveryReceiptId = EntitlementReceiptId;
			changed.NextDueTick = ReturnDueTick;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		public static bool TryValidateLoadedEndpointDelivery(KingdomPolityLedger Ledger,
			string RouteId, string EndpointId, KingdomPolityManifestProof Manifest,
			out string Failure)
		{
			Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure)) return false;
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(Ledger, RouteId);
			if (route == null || route.DestinationId != EndpointId ||
				(route.Phase != KingdomPolityRoutePhase.Arrived &&
				 route.Phase != KingdomPolityRoutePhase.Returned) ||
				string.IsNullOrEmpty(route.DeliveryReceiptId))
			{
				Failure = "loaded endpoint does not match an arrived route entitlement"; return false;
			}
			return KingdomPolityManifestRules.IsLoadedDelivery(Manifest,
				route.ManifestOrErrandId, out Failure);
		}

		private static KingdomPolityFrontRecord FindFront(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; L != null && i < L.Fronts.Count; i++)
				if (L.Fronts[i].FrontId == Id) return L.Fronts[i];
			return null;
		}

		private static bool ExactFront(KingdomPolityRouteRecord R, KingdomPolityFrontRecord F)
		{
			return R != null && F != null && R.FrontId == F.FrontId &&
				F.TargetKind == KingdomPolityFrontTarget.Route && F.TargetRef == R.RouteId;
		}
	}
}
