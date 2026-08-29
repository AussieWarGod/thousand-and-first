using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCohortRules
	{
		private const string EmptyDigest =
			"0000000000000000000000000000000000000000000000000000000000000000";

		public static bool TryPrepareEndpointManifestation(KingdomPolityLedger Ledger,
			long ExpectedRevision, string CohortId, string ZoneId, long Tick,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!KingdomPolityRules.Text(ZoneId, true) || Tick < 0L)
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "endpoint manifestation input is invalid", out Failure);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(Ledger, CohortId);
			if (cohort == null) return KingdomPolityAuthority.Refuse(Result,
				"cohort to manifest is missing", out Failure);
			KingdomPolityProjectionReceipt expected = PreparedReceipt(cohort, ZoneId, Tick);
			Result.ProjectionId = expected.ProjectionId;
			KingdomPolityProjectionReceipt existing = KingdomPolityAuthority.Projection(
				Ledger, expected.ProjectionId);
			if (existing != null)
			{
				if (!ExactPrepared(existing, expected) ||
					cohort.ManifestationReceiptId != expected.ProjectionId)
					return KingdomPolityAuthority.Refuse(Result,
						"manifestation projection carries foreign prepared evidence", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (!EligibleEndpoint(Ledger, cohort, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			if (!KingdomPolityAttentionRules.TryAdmitManifestation(Ledger, cohort, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			if (!string.IsNullOrEmpty(cohort.ManifestationReceiptId) ||
				Ledger.Projections.Count >= KingdomPolityRules.MaxProjections)
				return KingdomPolityAuthority.Refuse(Result,
					"cohort already has a manifestation or projection capacity is exhausted", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityCohortPlan changed = KingdomPolityAuthority.Cohort(candidate, CohortId);
			changed.ManifestationReceiptId = expected.ProjectionId;
			candidate.Projections.Add(expected); BindRouteManifestation(candidate, changed, expected.ProjectionId);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		public static bool TryCommitEndpointManifestation(KingdomPolityLedger Ledger,
			long ExpectedRevision, string CohortId, string ProjectionId,
			IList<string> ObservedObjectIds, long Tick,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Result.ProjectionId = ProjectionId;
			Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || Tick < 0L)
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(Ledger, CohortId);
			KingdomPolityProjectionReceipt receipt = KingdomPolityAuthority.Projection(Ledger,
				ProjectionId);
			if (!BoundReceipt(cohort, receipt) || !ExactObjectIds(receipt.ObjectIds,
				ObservedObjectIds)) return KingdomPolityAuthority.Refuse(Result,
					"commit does not observe every exact prepared cohort body", out Failure);
			if (receipt.Phase == KingdomPolityProjectionPhase.Committed &&
				cohort.Phase == KingdomPolityCohortPhase.Materialized)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (receipt.Phase != KingdomPolityProjectionPhase.Prepared ||
				cohort.Phase != KingdomPolityCohortPhase.Planned || Tick < receipt.PreparedTick)
				return KingdomPolityAuthority.Refuse(Result,
					"manifestation cannot commit from this phase", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityProjectionReceipt changed = KingdomPolityAuthority.Projection(candidate,
				ProjectionId);
			changed.Phase = KingdomPolityProjectionPhase.Committed; changed.CommittedTick = Tick;
			KingdomPolityAuthority.Cohort(candidate, CohortId).Phase =
				KingdomPolityCohortPhase.Materialized;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		public static bool TryConcludeEndpointCohort(KingdomPolityLedger Ledger,
			long ExpectedRevision, string CohortId, string WitnessedConclusionRef,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!KingdomPolityRules.SemanticId(WitnessedConclusionRef) ||
				WitnessedConclusionRef.StartsWith("taf:standing:", StringComparison.Ordinal))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "cohort conclusion is not witnessed", out Failure);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(Ledger, CohortId);
			if (cohort != null && cohort.Phase == KingdomPolityCohortPhase.Concluded &&
				cohort.RewardEventId == WitnessedConclusionRef)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (cohort == null || cohort.Phase != KingdomPolityCohortPhase.Materialized ||
				string.IsNullOrEmpty(cohort.ManifestationReceiptId))
				return KingdomPolityAuthority.Refuse(Result,
					"only a materialized finite cohort can conclude", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityCohortPlan changed = KingdomPolityAuthority.Cohort(candidate, CohortId);
			changed.Phase = KingdomPolityCohortPhase.Concluded;
			changed.RewardEventId = WitnessedConclusionRef;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		public static bool TryCommitEndpointCleanup(KingdomPolityLedger Ledger,
			long ExpectedRevision, string CohortId, string ProjectionId,
			IList<string> RemovedOrAbsentObjectIds, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Result.ProjectionId = ProjectionId;
			Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(Ledger, CohortId);
			KingdomPolityProjectionReceipt receipt = KingdomPolityAuthority.Projection(Ledger,
				ProjectionId);
			if (!BoundReceipt(cohort, receipt) || !ExactObjectIds(receipt.ObjectIds,
				RemovedOrAbsentObjectIds)) return KingdomPolityAuthority.Refuse(Result,
					"cleanup did not account for the exact manifestation", out Failure);
			if (receipt.Phase == KingdomPolityProjectionPhase.Cleaned &&
				(cohort.Phase == KingdomPolityCohortPhase.Cleaned ||
				 cohort.Phase == KingdomPolityCohortPhase.Abandoned))
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (receipt.Phase != KingdomPolityProjectionPhase.Committed ||
				(cohort.Phase != KingdomPolityCohortPhase.Concluded &&
				 cohort.Phase != KingdomPolityCohortPhase.Abandoned))
				return KingdomPolityAuthority.Refuse(Result,
					"only a concluded or abandoned committed manifestation can clean", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityAuthority.Projection(candidate, ProjectionId).Phase =
				KingdomPolityProjectionPhase.Cleaned;
			KingdomPolityCohortPlan cleaned = KingdomPolityAuthority.Cohort(candidate, CohortId);
			if (cleaned.Phase != KingdomPolityCohortPhase.Abandoned)
				cleaned.Phase = KingdomPolityCohortPhase.Cleaned;
			ClearRouteManifestation(candidate, ProjectionId);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		internal static string PreparedObjectId(KingdomPolityCohortPlan Cohort, int Ordinal)
		{
			KingdomPolityCohortMember member = Cohort.ResolvedMembers[Ordinal];
			return KingdomPolityRules.ActivationId("taf:object:polity-cohort:v1:",
				"polity-cohort-object-v1", Cohort.CohortId, member.MemberKey);
		}

		private static KingdomPolityProjectionReceipt PreparedReceipt(
			KingdomPolityCohortPlan Cohort, string ZoneId, long Tick)
		{
			string id = KingdomPolityRules.ActivationId("taf:projection:cohort:v1:",
				"polity-cohort-projection-v1", Cohort.CohortId, ZoneId,
				Cohort.ProfileId, Cohort.ProfileRevision.ToString(
					System.Globalization.CultureInfo.InvariantCulture));
			List<string> objects = new List<string>(); List<string> digest = new List<string>();
			for (int i = 0; i < Cohort.ResolvedMembers.Count; i++)
			{
				objects.Add(PreparedObjectId(Cohort, i));
				digest.Add(Cohort.ResolvedMembers[i].MemberKey);
				digest.Add(Cohort.ResolvedMembers[i].SignatureKey);
			}
			objects.Sort(StringComparer.Ordinal); digest.AddRange(objects);
			return new KingdomPolityProjectionReceipt
			{
				ProjectionId = id, Kind = KingdomPolityProjectionKind.CohortManifestation,
				SourceRef = Cohort.CohortId, Phase = KingdomPolityProjectionPhase.Prepared,
				ZoneId = ZoneId, ObjectIds = objects, PriorDigest = EmptyDigest,
				AppliedDigest = KingdomPolityRules.ActivationDigest(
					"polity-cohort-manifestation-v1", digest), PreparedTick = Tick
			};
		}
	}
}
