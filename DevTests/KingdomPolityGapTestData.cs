#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	internal static class KingdomPolityGapTestData
	{
		internal const string Remote =
			"taf:settlement:v1:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
		internal const string Route = "taf:route:semantic-delegation";
		internal const string Zone = "JoppaWorld.11.22.1.1.10";
		internal const string Envoy = "taf:cohort:terms-envoy";
		internal const string Warband = "taf:cohort:frozen-warband";
		internal const string TermsPlan = "taf:incident-plan:terms";
		internal const string ClashPlan = "taf:incident-plan:clash";
		internal const string RefusalFact = "taf:fact:witnessed:terms-refused";

		internal static KingdomPolityLedger TermsAwaitingAnswer(
			KingdomPolityRelationBand InitialBand)
		{
			KingdomPolityLedger ledger = Fresh();
			Relation(ledger).Band = InitialBand;
			PlanRoute(ledger);
			Assert.IsTrue(KingdomPolityManifestRules.TryCreateErrandProof(
				"taf:manifest-proof:terms", "taf:office:rival", "taf:errand:terms",
				out KingdomPolityManifestProof errand, out string failure), failure);
			Assert.IsTrue(KingdomPolityRouteRules.TryDepart(ledger, ledger.Revision, Route,
				90L, "taf:receipt:terms-depart", errand,
				out KingdomPolityPublicationResult _, out failure), failure);
			Assert.IsTrue(KingdomPolityRouteRules.TryAdvance(ledger, ledger.Revision, Route,
				0, 100L, 100L, out _, out failure), failure);
			KingdomPolityCohortPlanRequest envoy = CohortRequest(Envoy,
				KingdomPolityCohortPurpose.Envoy, Route, 2);
			envoy.NamedFigureId = "taf:figure:rival-envoy";
			Assert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, envoy,
				out _, out failure), failure);
			Assert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision,
				CohortRequest(Warband, KingdomPolityCohortPurpose.Warband,
					"taf:event:warband-mustered", 2), out _, out failure), failure);
			CommitManifestation(ledger, Envoy, 120L);
			CommitManifestation(ledger, Warband, 121L);
			Assert.IsTrue(KingdomPolityDiplomacyRules.TryOpenGrievance(ledger,
				ledger.Revision, OriginalGrievance(), out _, out failure), failure);
			KingdomPolityTermsPlanRequest terms = new KingdomPolityTermsPlanRequest
			{
				GrievanceId = "taf:grievance:caused-crossing", TermsPlanId = TermsPlan,
				TermsIncidentId = "taf:incident:terms", ClashPlanId = ClashPlan,
				ClashIncidentId = "taf:incident:clash", EnvoyCohortId = Envoy,
				ClashCohortRefs = new List<string> { Warband },
				DisclosedStakeRefs = new List<string> { Route },
				EligibleSurfaceRefs = new List<string> { KingdomPolityTestData.Settlement },
				TermKeys = new List<string> { "recognize-passage", "restore-access" },
				EventStreamId = "taf:stream:terms", RulesVersion = 1, MaxSystemicWound = 1
			};
			Assert.IsTrue(KingdomPolityDiplomacyRules.TryPlanTerms(ledger,
				ledger.Revision, terms, out _, out failure), failure);
			return ledger;
		}

		internal static KingdomPolityLedger OpenClash(KingdomPolityRelationBand InitialBand)
		{
			KingdomPolityLedger ledger = TermsAwaitingAnswer(InitialBand);
			Assert.IsTrue(KingdomPolityDiplomacyRules.TryAnswerTerms(ledger,
				ledger.Revision, TermsPlan, KingdomPolityTermsChoice.Refuse, RefusalFact,
				200L, null, out KingdomPolityPublicationResult _, out string failure), failure);
			return ledger;
		}

		internal static KingdomPolityConsentedEscrowRequest EscrowRequest(
			KingdomPolityLedger Ledger, string ObjectId, string Snapshot, long Tick)
		{
			KingdomPolityIncidentRecord plan = Incident(Ledger, ClashPlan);
			List<string> projections = new List<string>();
			for (int i = 0; i < plan.ParticipantCohortRefs.Count; i++)
				projections.Add(KingdomPolityAuthority.Cohort(Ledger,
					plan.ParticipantCohortRefs[i]).ManifestationReceiptId);
			projections.Sort(System.StringComparer.Ordinal);
			return new KingdomPolityConsentedEscrowRequest
			{
				IncidentPlanId = ClashPlan, SurfaceRef = KingdomPolityTestData.Settlement,
				ZoneId = Zone, ConsentTick = Tick,
				ConsentFactId = "taf:fact:witnessed:consented-escrow",
				ParticipantProjectionIds = projections, StakeRef = Route,
				CollateralObjectId = ObjectId, SnapshotDigest = Snapshot
			};
		}

		internal static KingdomPolityIncidentRecord Incident(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Incidents.Count; i++)
				if (L.Incidents[i].IncidentPlanId == Id) return L.Incidents[i];
			return null;
		}

		internal static KingdomPolityProjectionReceipt Projection(
			KingdomPolityLedger L, string Id)
		{
			return KingdomPolityAuthority.Projection(L, Id);
		}

		internal static KingdomPolityRouteRecord RouteRecord(KingdomPolityLedger L)
		{
			return KingdomPolityAuthority.Route(L, Route);
		}

		internal static KingdomPolityRelation Relation(KingdomPolityLedger L)
		{
			for (int i = 0; i < L.Relations.Count; i++)
				if (L.Relations[i].FromPolityId == KingdomPolityTestData.Rival &&
					L.Relations[i].ToPolityId == KingdomPolityTestData.Realm)
					return L.Relations[i];
			return null;
		}

		private static KingdomPolityLedger Fresh()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			ledger.Routes.Clear(); ledger.Grievances.Clear(); ledger.Fronts.Clear();
			ledger.Cohorts.Clear(); ledger.Incidents.Clear();
			for (int i = ledger.Projections.Count - 1; i >= 0; i--)
				if (ledger.Projections[i].Kind != KingdomPolityProjectionKind.Faction)
					ledger.Projections.RemoveAt(i);
			for (int i = 0; i < ledger.Profiles.Count; i++)
				if (ledger.Profiles[i].PolityId == KingdomPolityTestData.Rival)
					ledger.Profiles[i].RoleKeys = new List<string> { "claimant", "courier",
						"envoy", "guard", "migrant", "namesake", "patrol", "trader", "warband" };
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out string failure), failure);
			return ledger;
		}

		private static void PlanRoute(KingdomPolityLedger Ledger)
		{
			KingdomPolityRoutePlanRequest request = new KingdomPolityRoutePlanRequest
			{
				RouteId = Route, EventStreamId = "taf:stream:semantic-route", OriginId = Remote,
				DestinationId = KingdomPolityTestData.Settlement,
				OrderedPath = new List<string> { Remote, KingdomPolityTestData.Settlement },
				Mode = KingdomPolityRouteMode.Foot,
				Purpose = KingdomPolityRoutePurpose.Delegation, FirstDueTick = 100L,
				ManifestOrErrandId = "taf:errand:terms",
				CounterpartyRef = KingdomPolityTestData.Rival
			};
			Assert.IsTrue(KingdomPolityRouteRules.TryPlan(Ledger, Ledger.Revision, request,
				out KingdomPolityPublicationResult _, out string failure), failure);
		}

		private static KingdomPolityGrievanceRequest OriginalGrievance()
		{
			return new KingdomPolityGrievanceRequest
			{
				GrievanceId = "taf:grievance:caused-crossing",
				IssuerPolityId = KingdomPolityTestData.Rival,
				TargetPolityId = KingdomPolityTestData.Realm,
				Cause = KingdomPolityGrievanceCause.RouteObstruction,
				SourceEventId = "taf:event:crossing-blocked", Severity = 2,
				EvidenceRefs = new List<string> { "taf:evidence:crossing-marker" }
			};
		}

		private static KingdomPolityCohortPlanRequest CohortRequest(string Id,
			KingdomPolityCohortPurpose Purpose, string Source, int Count)
		{
			return new KingdomPolityCohortPlanRequest
			{
				CohortId = Id, Purpose = Purpose, SourceRef = Source,
				PolityId = KingdomPolityTestData.Rival,
				SurfaceRef = KingdomPolityTestData.Settlement, MemberCount = Count,
				EventStreamId = "taf:stream:" + Id.Substring("taf:cohort:".Length),
				RulesVersion = KingdomPolityNpcRules.RulesVersion,
				PresentationAuthority = new KingdomPolityPresentationAuthorityProof
				{
					OptionKind = KingdomExperienceOptionKind.CivicStory,
					EnableEpoch = 1L, ReservedTick = 100L
				}
			};
		}

		private static void CommitManifestation(KingdomPolityLedger Ledger,
			string CohortId, long Tick)
		{
			Assert.IsTrue(KingdomPolityCohortRules.TryPrepareEndpointManifestation(Ledger,
				Ledger.Revision, CohortId, Zone, Tick, out KingdomPolityPublicationResult result,
				out string failure), failure);
			KingdomPolityProjectionReceipt receipt = Projection(Ledger, result.ProjectionId);
			Assert.IsTrue(KingdomPolityCohortRules.TryCommitEndpointManifestation(Ledger,
				Ledger.Revision, CohortId, receipt.ProjectionId, receipt.ObjectIds, Tick,
				out KingdomPolityPublicationResult _, out failure), failure);
		}
	}
}
#endif
