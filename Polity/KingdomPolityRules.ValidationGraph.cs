using System;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		private static bool ValidateGraph(KingdomPolityLedger L, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < L.Polities.Count; i++)
			{
				KingdomPolityRecord p = L.Polities[i];
				if (!string.IsNullOrEmpty(p.ProfileId) &&
					!HasProfile(L, p.ProfileId, p.ProfileRevision, p.PolityId))
					return Fail("polity points to a missing profile revision", out Failure);
			}
			for (int i = 0; i < L.Profiles.Count; i++)
				if (!HasPolity(L, L.Profiles[i].PolityId))
					return Fail("profile owner polity is missing", out Failure);
			if (!ValidateNamedFigureGraph(L, out Failure)) return false;
			for (int i = 0; i < L.Relations.Count; i++)
			{
				KingdomPolityRelation r = L.Relations[i];
				if (!HasPolity(L, r.FromPolityId) || !HasPolity(L, r.ToPolityId))
					return Fail("relation endpoint polity is missing", out Failure);
			}
			for (int i = 0; i < L.Grievances.Count; i++)
			{
				KingdomPolityGrievanceRecord g = L.Grievances[i];
				if (!HasPolity(L, g.IssuerPolityId) || !HasPolity(L, g.TargetPolityId))
					return Fail("grievance endpoint polity is missing", out Failure);
				if (!string.IsNullOrEmpty(g.ConsumedByIncidentId) &&
					!HasIncidentId(L, g.ConsumedByIncidentId))
					return Fail("grievance consuming incident is missing", out Failure);
				if (!string.IsNullOrEmpty(g.ConsumedByIncidentId) &&
					!IncidentContainsGrievance(L, g.ConsumedByIncidentId, g.GrievanceId))
					return Fail("grievance consuming incident lacks the grievance", out Failure);
			}
			if (!ValidateFrontGraph(L, out Failure) || !ValidateCohortGraph(L, out Failure) ||
				!ValidateIncidentGraph(L, out Failure) || !ValidateProjectionGraph(L, out Failure))
				return false;
			for (int i = 0; i < L.Compactions.Count; i++)
				for (int j = 0; j < L.Compactions[i].RemovedProfiles.Count; j++)
				{
					KingdomPolityProfileRef p = L.Compactions[i].RemovedProfiles[j];
					if (HasProfile(L, p.ProfileId, p.Revision, null))
						return Fail("compaction receipt names a retained profile", out Failure);
				}
			return true;
		}

		private static bool ValidateNamedFigureGraph(KingdomPolityLedger L, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < L.NamedFigures.Count; i++)
			{
				KingdomPolityNamedFigureRecord f = L.NamedFigures[i];
				KingdomPolityRecord polity = FindGraphPolity(L, f.PolityId);
				if (polity == null) return Fail("named figure polity is missing", out Failure);
				if (f.ResidentId == 0) continue;
				if (polity.Source != KingdomPolitySource.CurrentRealm ||
					f.Phase != KingdomPolityFigurePhase.Active)
					return Fail("resident bridge does not belong to an active current figure", out Failure);
				for (int j = 0; j < i; j++)
					if (L.NamedFigures[j].ResidentId == f.ResidentId &&
						string.Equals(L.NamedFigures[j].ResidentSettlementId,
							f.ResidentSettlementId, StringComparison.Ordinal))
						return Fail("resident bridge is claimed by more than one figure", out Failure);
			}
			return true;
		}

		private static bool ValidateFrontGraph(KingdomPolityLedger L, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < L.Fronts.Count; i++)
			{
				KingdomPolityFrontRecord f = L.Fronts[i];
				for (int j = 0; j < f.GrievanceRefs.Count; j++)
					if (!HasGrievance(L, f.GrievanceRefs[j]))
						return Fail("front grievance is missing", out Failure);
				if (f.TargetKind == KingdomPolityFrontTarget.Route)
				{
					KingdomPolityRouteRecord route = FindRoute(L, f.TargetRef);
					if (route == null || route.FrontId != f.FrontId)
						return Fail("route front binding is not bidirectional", out Failure);
				}
				if (f.TargetKind == KingdomPolityFrontTarget.Cohort && !HasCohort(L, f.TargetRef))
					return Fail("front target cohort is missing", out Failure);
			}
			for (int i = 0; i < L.Routes.Count; i++)
				if (!string.IsNullOrEmpty(L.Routes[i].FrontId) &&
					!HasFrontTargeting(L, L.Routes[i].FrontId, L.Routes[i].RouteId))
					return Fail("route points to a missing or foreign front", out Failure);
			return true;
		}

		private static bool ValidateCohortGraph(KingdomPolityLedger L, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < L.Cohorts.Count; i++)
			{
				KingdomPolityCohortPlan c = L.Cohorts[i];
				if (!HasPolity(L, c.PolityId) ||
					!HasProfile(L, c.ProfileId, c.ProfileRevision, c.PolityId))
					return Fail("cohort pins missing polity profile", out Failure);
				if (c.SourceRef.StartsWith("taf:route:", StringComparison.Ordinal) &&
					FindRoute(L, c.SourceRef) == null)
					return Fail("cohort source route is missing", out Failure);
				if (c.SourceRef.StartsWith("taf:front:", StringComparison.Ordinal) &&
					!HasFront(L, c.SourceRef))
					return Fail("cohort source front is missing", out Failure);
				if (!string.IsNullOrEmpty(c.ManifestationReceiptId) &&
					!HasProjection(L, c.ManifestationReceiptId,
						KingdomPolityProjectionKind.CohortManifestation, c.CohortId))
					return Fail("cohort manifestation receipt is missing", out Failure);
				if (c.Phase == KingdomPolityCohortPhase.Abandoned)
				{
					KingdomPolityProjectionReceipt abandoned = FindGraphProjection(L,
						c.ManifestationReceiptId);
					if (abandoned == null || (abandoned.Phase !=
						KingdomPolityProjectionPhase.Committed && abandoned.Phase !=
						KingdomPolityProjectionPhase.Cleaned))
						return Fail("abandoned cohort lacks committed physical projection proof",
							out Failure);
				}
			}
			return true;
		}

		private static bool ValidateIncidentGraph(KingdomPolityLedger L, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < L.Incidents.Count; i++)
			{
				KingdomPolityIncidentRecord p = L.Incidents[i];
				if (!KingdomPolityConflictRules.TryValidateGraph(L, p, out Failure)) return false;
				if (!KingdomPolityCorrespondenceRules.TryValidateGraph(L, p, out Failure)) return false;
				for (int j = 0; j < p.GrievanceRefs.Count; j++)
					if (!HasGrievance(L, p.GrievanceRefs[j]))
						return Fail("incident grievance is missing", out Failure);
				for (int j = 0; j < p.ParticipantCohortRefs.Count; j++)
					if (!HasCohort(L, p.ParticipantCohortRefs[j]))
						return Fail("incident participant cohort is missing", out Failure);
				if (p.Conclusion != null)
				{
					for (int j = 0; j < p.Conclusion.RelationDeltas.Count; j++)
						if (!HasRelation(L, p.Conclusion.RelationDeltas[j].RelationId))
							return Fail("conclusion relation delta target is missing", out Failure);
					for (int j = 0; j < i; j++)
						if (L.Incidents[j].Conclusion != null &&
							L.Incidents[j].Conclusion.ConclusionId == p.Conclusion.ConclusionId)
							return Fail("conclusion id is duplicated", out Failure);
				}
			}
			return true;
		}

		private static bool ValidateProjectionGraph(KingdomPolityLedger L, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < L.Projections.Count; i++)
			{
				KingdomPolityProjectionReceipt p = L.Projections[i]; bool found;
				switch (p.Kind)
				{
				case KingdomPolityProjectionKind.Faction:
				case KingdomPolityProjectionKind.FactionTombstone: found = HasPolity(L, p.SourceRef); break;
				case KingdomPolityProjectionKind.Relation: found = HasRelation(L, p.SourceRef); break;
				case KingdomPolityProjectionKind.CohortManifestation: found = HasCohort(L, p.SourceRef); break;
				case KingdomPolityProjectionKind.RoutePrompt: found = FindRoute(L, p.SourceRef) != null; break;
				case KingdomPolityProjectionKind.IncidentView: found = HasOpenPlan(L, p.SourceRef); break;
				case KingdomPolityProjectionKind.Aftermath: found = HasConclusion(L, p.SourceRef); break;
				case KingdomPolityProjectionKind.ConsentedEscrow:
					found = HasPlan(L, p.SourceRef); break;
				default: found = false; break;
				}
				if (!found) return Fail("projection source is missing or wrong kind", out Failure);
			}
			return true;
		}

		private static bool HasPolity(KingdomPolityLedger L, string Id)
		{
			return FindGraphPolity(L, Id) != null;
		}

		private static KingdomPolityRecord FindGraphPolity(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Polities.Count; i++) if (L.Polities[i].PolityId == Id)
				return L.Polities[i];
			return null;
		}

		private static bool HasProfile(KingdomPolityLedger L, string Id, int Revision, string Polity)
		{
			for (int i = 0; i < L.Profiles.Count; i++) if (L.Profiles[i].ProfileId == Id &&
				L.Profiles[i].Revision == Revision && (Polity == null || L.Profiles[i].PolityId == Polity))
				return true;
			return false;
		}

		private static bool HasRelation(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Relations.Count; i++) if (L.Relations[i].RelationId == Id) return true;
			return false;
		}

		private static bool HasGrievance(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Grievances.Count; i++) if (L.Grievances[i].GrievanceId == Id) return true;
			return false;
		}

		private static bool HasCohort(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Cohorts.Count; i++) if (L.Cohorts[i].CohortId == Id) return true;
			return false;
		}

		private static KingdomPolityRouteRecord FindRoute(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Routes.Count; i++) if (L.Routes[i].RouteId == Id) return L.Routes[i];
			return null;
		}

		private static bool HasFront(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Fronts.Count; i++) if (L.Fronts[i].FrontId == Id) return true;
			return false;
		}

		private static bool HasFrontTargeting(KingdomPolityLedger L, string Id, string Route)
		{
			for (int i = 0; i < L.Fronts.Count; i++) if (L.Fronts[i].FrontId == Id &&
				L.Fronts[i].TargetKind == KingdomPolityFrontTarget.Route &&
				L.Fronts[i].TargetRef == Route) return true;
			return false;
		}

		private static bool HasIncidentId(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Incidents.Count; i++) if (L.Incidents[i].IncidentId == Id) return true;
			return false;
		}

		private static bool IncidentContainsGrievance(KingdomPolityLedger L, string IncidentId,
			string GrievanceId)
		{
			for (int i = 0; i < L.Incidents.Count; i++)
				if (L.Incidents[i].IncidentId == IncidentId)
					for (int j = 0; j < L.Incidents[i].GrievanceRefs.Count; j++)
						if (L.Incidents[i].GrievanceRefs[j] == GrievanceId) return true;
			return false;
		}

		private static bool HasOpenPlan(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Incidents.Count; i++) if (L.Incidents[i].IncidentPlanId == Id)
				return L.Incidents[i].Conclusion == null;
			return false;
		}

		private static bool HasPlan(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Incidents.Count; i++)
				if (L.Incidents[i].IncidentPlanId == Id) return true;
			return false;
		}

		private static bool HasConclusion(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Incidents.Count; i++) if (L.Incidents[i].Conclusion != null &&
				L.Incidents[i].Conclusion.ConclusionId == Id) return true;
			return false;
		}

		private static bool HasProjection(KingdomPolityLedger L, string Id,
			KingdomPolityProjectionKind Kind, string Source)
		{
			for (int i = 0; i < L.Projections.Count; i++) if (L.Projections[i].ProjectionId == Id &&
				L.Projections[i].Kind == Kind && L.Projections[i].SourceRef == Source) return true;
			return false;
		}

		private static KingdomPolityProjectionReceipt FindGraphProjection(
			KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Projections.Count; i++)
				if (L.Projections[i].ProjectionId == Id) return L.Projections[i];
			return null;
		}
	}
}
