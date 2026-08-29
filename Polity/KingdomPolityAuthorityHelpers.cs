using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Shared CAS plumbing for bounded polity authority extensions.</summary>
	internal static class KingdomPolityAuthority
	{
		internal static KingdomPolityPublicationResult Begin(KingdomPolityLedger Ledger)
		{
			long revision = Ledger == null ? -1L : Ledger.Revision;
			return new KingdomPolityPublicationResult
			{
				Outcome = KingdomPolityCasOutcome.Refused,
				SourceRevision = revision,
				CommittedRevision = revision
			};
		}

		internal static bool Conflict(KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result.Outcome = KingdomPolityCasOutcome.Conflict;
			Failure = "polity compare-and-swap revision conflict"; return false;
		}

		internal static bool Refuse(KingdomPolityPublicationResult Result, string Reason,
			out string Failure)
		{
			Result.Outcome = KingdomPolityCasOutcome.Refused;
			Failure = string.IsNullOrEmpty(Reason) ? "polity authority change was refused" : Reason;
			return false;
		}

		internal static bool Commit(KingdomPolityLedger Target, KingdomPolityLedger Candidate,
			KingdomPolityPublicationResult Result, out string Failure)
		{
			Failure = null;
			if (Candidate.Revision == long.MaxValue)
				return Refuse(Result, "polity revision is exhausted", out Failure);
			Candidate.Revision++;
			Sort(Candidate);
			if (!KingdomPolityRules.TryValidate(Candidate, out Failure))
				return Refuse(Result, Failure, out Failure);
			Target.CopyFrom(Candidate); Result.Outcome = KingdomPolityCasOutcome.Applied;
			Result.CommittedRevision = Candidate.Revision; return true;
		}

		internal static void Sort(KingdomPolityLedger L)
		{
			L.Polities.Sort((a, b) => string.CompareOrdinal(a.PolityId, b.PolityId));
			L.Relations.Sort((a, b) => string.CompareOrdinal(a.RelationId, b.RelationId));
			L.Profiles.Sort(delegate(KingdomPolityProfileRevision a,
				KingdomPolityProfileRevision b)
			{
				int compared = string.CompareOrdinal(a.ProfileId, b.ProfileId);
				return compared == 0 ? a.Revision.CompareTo(b.Revision) : compared;
			});
			L.Routes.Sort((a, b) => string.CompareOrdinal(a.RouteId, b.RouteId));
			L.Grievances.Sort((a, b) => string.CompareOrdinal(a.GrievanceId, b.GrievanceId));
			L.Fronts.Sort((a, b) => string.CompareOrdinal(a.FrontId, b.FrontId));
			L.Cohorts.Sort((a, b) => string.CompareOrdinal(a.CohortId, b.CohortId));
			L.NamedFigures.Sort((a, b) => string.CompareOrdinal(a.FigureId, b.FigureId));
			L.Incidents.Sort((a, b) => string.CompareOrdinal(a.IncidentPlanId, b.IncidentPlanId));
			L.Projections.Sort((a, b) => string.CompareOrdinal(a.ProjectionId, b.ProjectionId));
		}

		internal static KingdomPolityRouteRecord Route(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; L != null && i < L.Routes.Count; i++)
				if (L.Routes[i].RouteId == Id) return L.Routes[i];
			return null;
		}

		internal static KingdomPolityCohortPlan Cohort(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; L != null && i < L.Cohorts.Count; i++)
				if (L.Cohorts[i].CohortId == Id) return L.Cohorts[i];
			return null;
		}

		internal static KingdomPolityProjectionReceipt Projection(KingdomPolityLedger L,
			string Id)
		{
			for (int i = 0; L != null && i < L.Projections.Count; i++)
				if (L.Projections[i].ProjectionId == Id) return L.Projections[i];
			return null;
		}

		internal static KingdomPolityRecord Polity(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; L != null && i < L.Polities.Count; i++)
				if (L.Polities[i].PolityId == Id) return L.Polities[i];
			return null;
		}

		internal static KingdomPolityProfileRevision Profile(KingdomPolityLedger L,
			string Id, int Revision)
		{
			for (int i = 0; L != null && i < L.Profiles.Count; i++)
				if (L.Profiles[i].ProfileId == Id && L.Profiles[i].Revision == Revision)
					return L.Profiles[i];
			return null;
		}

		internal static KingdomPolityNamedFigureRecord Figure(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; L != null && i < L.NamedFigures.Count; i++)
				if (L.NamedFigures[i].FigureId == Id) return L.NamedFigures[i];
			return null;
		}

		internal static bool Contains(IList<string> Values, string Value)
		{
			for (int i = 0; Values != null && i < Values.Count; i++)
				if (Values[i] == Value) return true;
			return false;
		}

		internal static void AddSortedUnique(List<string> Values, string Value)
		{
			if (string.IsNullOrEmpty(Value) || Contains(Values, Value)) return;
			Values.Add(Value); Values.Sort(StringComparer.Ordinal);
		}
	}
}
