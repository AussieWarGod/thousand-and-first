namespace ThousandAndFirst
{
	/// <summary>Deterministic one-route correspondence; it never invents a person or cargo row.</summary>
	public static partial class KingdomPolityCorrespondenceRules
	{
		public static bool TryCreateProof(string CorrespondenceId, string RouteId,
			string CounterpartyRef, string NeedRef, string NewsRef, string ManifestOrErrandId,
			string ReturnRef, out KingdomPolityCorrespondenceProof Proof, out string Failure)
		{
			Proof = new KingdomPolityCorrespondenceProof
			{
				CorrespondenceId = CorrespondenceId, RouteId = RouteId,
				CounterpartyRef = CounterpartyRef, NeedRef = NeedRef, NewsRef = NewsRef,
				ManifestOrErrandId = ManifestOrErrandId, ReturnRef = ReturnRef
			};
			Proof.ProofDigest = Digest(Proof);
			if (ValidProof(Proof, out Failure)) return true;
			Proof = null; return false;
		}

		public static bool TryDescribe(KingdomPolityLedger Ledger,
			KingdomPolityCorrespondenceProof Proof, out KingdomPolityCorrespondenceView View,
			out string Failure)
		{
			View = null; Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!ValidProof(Proof, out Failure)) return false;
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(Ledger, Proof.RouteId);
			if (route == null || route.CounterpartyRef != Proof.CounterpartyRef ||
				route.ManifestOrErrandId != Proof.ManifestOrErrandId)
			{
				Failure = "correspondence does not bind its exact route"; return false;
			}
			if ((route.Phase == KingdomPolityRoutePhase.Returned) !=
				!string.IsNullOrEmpty(Proof.ReturnRef))
			{
				Failure = "correspondence return evidence is noncanonical"; return false;
			}
			View = new KingdomPolityCorrespondenceView
			{
				CorrespondenceId = Proof.CorrespondenceId, RouteId = route.RouteId,
				CounterpartyRef = Proof.CounterpartyRef, NeedRef = Proof.NeedRef,
				NewsRef = Proof.NewsRef, ManifestOrErrandId = Proof.ManifestOrErrandId,
				ReturnRef = Proof.ReturnRef, PurposeVerb = Verb(route.Purpose),
				Phase = Phase(route.Phase), SegmentIndex = route.SegmentIndex,
				SegmentCount = route.OrderedPath.Count - 1, NextDueTick = route.NextDueTick
			};
			return true;
		}

		private static bool ValidProof(KingdomPolityCorrespondenceProof P, out string Failure)
		{
			Failure = null;
			if (P == null || !KingdomPolityRules.TypedId(P.CorrespondenceId,
				"taf:correspondence:") || !KingdomPolityRules.TypedId(P.RouteId, "taf:route:") ||
				!KingdomPolityRules.SemanticId(P.CounterpartyRef) ||
				!KingdomPolityRules.SemanticId(P.NeedRef) ||
				(!string.IsNullOrEmpty(P.NewsRef) && !KingdomPolityRules.SemanticId(P.NewsRef)) ||
				!KingdomPolityRules.SemanticId(P.ManifestOrErrandId) ||
				(!string.IsNullOrEmpty(P.ReturnRef) && !KingdomPolityRules.SemanticId(P.ReturnRef)) ||
				!KingdomPolityRules.Digest(P.ProofDigest) || P.ProofDigest != Digest(P))
			{
				Failure = "correspondence proof is invalid or changed"; return false;
			}
			return true;
		}

		private static string Digest(KingdomPolityCorrespondenceProof P)
		{
			return KingdomPolityRules.ActivationDigest("polity-correspondence-proof-v1",
				P.CorrespondenceId ?? "", P.RouteId ?? "", P.CounterpartyRef ?? "",
				P.NeedRef ?? "", P.NewsRef ?? "", P.ManifestOrErrandId ?? "",
				P.ReturnRef ?? "");
		}

		private static string Verb(KingdomPolityRoutePurpose Purpose)
		{
			switch (Purpose)
			{
			case KingdomPolityRoutePurpose.Trade: return "offer exact manifest";
			case KingdomPolityRoutePurpose.Delegation: return "present caused terms";
			case KingdomPolityRoutePurpose.Patrol: return "report caused route condition";
			case KingdomPolityRoutePurpose.Migration: return "request witnessed passage";
			case KingdomPolityRoutePurpose.Courier: return "deliver exact message";
			default: return "report exact errand";
			}
		}

		private static KingdomPolityCorrespondencePhase Phase(KingdomPolityRoutePhase Phase)
		{
			switch (Phase)
			{
			case KingdomPolityRoutePhase.Preparing: return KingdomPolityCorrespondencePhase.Prepared;
			case KingdomPolityRoutePhase.Traveling: return KingdomPolityCorrespondencePhase.Outbound;
			case KingdomPolityRoutePhase.AvailableToWitness:
				return KingdomPolityCorrespondencePhase.Available;
			case KingdomPolityRoutePhase.Blocked: return KingdomPolityCorrespondencePhase.Blocked;
			case KingdomPolityRoutePhase.ConfrontationAvailable:
				return KingdomPolityCorrespondencePhase.Confrontation;
			case KingdomPolityRoutePhase.Arrived:
				return KingdomPolityCorrespondencePhase.EntitlementRecorded;
			case KingdomPolityRoutePhase.Returned: return KingdomPolityCorrespondencePhase.Returned;
			default: return KingdomPolityCorrespondencePhase.Cancelled;
			}
		}
	}
}
