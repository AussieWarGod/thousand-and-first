namespace ThousandAndFirst
{
	public static partial class KingdomPolityRouteRules
	{
		public static bool TryReturn(KingdomPolityLedger Ledger, long ExpectedRevision,
			string RouteId, long Tick, string ReturnReceiptId,
			KingdomPolityManifestProof Manifest, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || Tick < 0L ||
				!KingdomPolityRules.SemanticId(ReturnReceiptId))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "route return input is invalid", out Failure);
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(Ledger, RouteId);
			if (route == null || !KingdomPolityManifestRules.IsLoadedDelivery(Manifest,
				route.ManifestOrErrandId, out Failure))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "route return lacks loaded delivery reconciliation", out Failure);
			if (!string.IsNullOrEmpty(route.ReturnReceiptId))
			{
				if (route.ReturnReceiptId != ReturnReceiptId)
					return KingdomPolityAuthority.Refuse(Result,
						"route already returned under other evidence", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (route.Phase != KingdomPolityRoutePhase.Arrived || Tick < route.NextDueTick ||
				!string.IsNullOrEmpty(route.ActiveManifestationId))
				return KingdomPolityAuthority.Refuse(Result,
					"route cannot return before its endpoint party is reconciled", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityRouteRecord changed = KingdomPolityAuthority.Route(candidate, RouteId);
			changed.Phase = KingdomPolityRoutePhase.Returned; changed.ReturnReceiptId = ReturnReceiptId;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		public static bool TryCancelPreparing(KingdomPolityLedger Ledger, long ExpectedRevision,
			string RouteId, out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(Ledger, RouteId);
			if (route == null) return KingdomPolityAuthority.Refuse(Result,
				"route to cancel is missing", out Failure);
			if (route.Phase == KingdomPolityRoutePhase.Cancelled)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (route.Phase != KingdomPolityRoutePhase.Preparing)
				return KingdomPolityAuthority.Refuse(Result,
					"a departed route cannot discard custody by cancellation", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityAuthority.Route(candidate, RouteId).Phase = KingdomPolityRoutePhase.Cancelled;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}
	}
}
