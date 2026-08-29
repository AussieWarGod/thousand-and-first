using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	internal static partial class KingdomPolityClashRules
	{
		private static bool ExactLiveParticipants(KingdomPolityLedger L,
			KingdomPolityIncidentRecord Plan, KingdomPolityWitnessedClashProof Proof,
			out string Failure)
		{
			Failure = null;
			if (Plan.ParticipantCohortRefs.Count != Proof.ParticipantProjectionIds.Count) return false;
			for (int i = 0; i < Plan.ParticipantCohortRefs.Count; i++)
			{
				KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(L,
					Plan.ParticipantCohortRefs[i]);
				KingdomPolityProjectionReceipt receipt = cohort == null ? null :
					KingdomPolityAuthority.Projection(L, cohort.ManifestationReceiptId);
				if (cohort == null || cohort.SurfaceRef != Proof.SurfaceRef ||
					(cohort.Phase != KingdomPolityCohortPhase.Materialized &&
					 cohort.Phase != KingdomPolityCohortPhase.Concluded) || receipt == null ||
					receipt.Phase != KingdomPolityProjectionPhase.Committed ||
					receipt.ZoneId != Proof.ZoneId ||
					!KingdomPolityAuthority.Contains(Proof.ParticipantProjectionIds,
						receipt.ProjectionId))
				{
					Failure = "clash participant was not committed at this loaded surface"; return false;
				}
			}
			return true;
		}

		private static bool RelationBeforeMatches(KingdomPolityLedger L,
			IList<KingdomPolityRelationDelta> Values, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolityRelation relation = FindRelationById(L, Values[i].RelationId);
				if (relation == null || relation.Band != Values[i].Before)
				{
					Failure = "clash relation delta does not start from current authority"; return false;
				}
			}
			return true;
		}

		private static bool ValidRelationDeltas(IList<KingdomPolityRelationDelta> Values,
			IList<string> Receipts)
		{
			if (Values == null || Values.Count > KingdomPolityRules.MaxDeltas) return false;
			string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolityRelationDelta d = Values[i];
				string key = d == null ? null : d.RelationId + "\n" + d.ReceiptId;
				if (d == null || !KingdomPolityRules.TypedId(d.RelationId, "taf:relation:") ||
					(byte)d.Before > 6 || (byte)d.After > 6 || d.Before == d.After ||
					!KingdomPolityRules.SemanticId(d.ReceiptId) ||
					!KingdomPolityAuthority.Contains(Receipts, d.ReceiptId) ||
					(previous != null && string.CompareOrdinal(previous, key) >= 0)) return false;
				previous = key;
			}
			return true;
		}

		private static void ApplyRelationDeltas(KingdomPolityLedger L,
			KingdomPolityWitnessedClashProof Proof)
		{
			for (int i = 0; i < Proof.RelationDeltas.Count; i++)
			{
				KingdomPolityRelationDelta delta = Proof.RelationDeltas[i];
				KingdomPolityRelation relation = FindRelationById(L, delta.RelationId);
				relation.Band = delta.After; relation.ChangedTick = Proof.CommitTick;
				KingdomPolityAuthority.AddSortedUnique(relation.SourceRefs,
					Proof.ObservedFactIds[0]);
			}
		}

		internal static void ResolveOpenClashGrievances(KingdomPolityLedger L,
			KingdomPolityIncidentRecord Plan, string ConclusionId)
		{
			for (int i = 0; i < Plan.GrievanceRefs.Count; i++)
			{
				KingdomPolityGrievanceRecord grievance = null;
				for (int j = 0; j < L.Grievances.Count; j++)
					if (L.Grievances[j].GrievanceId == Plan.GrievanceRefs[i]) grievance = L.Grievances[j];
				if (grievance != null && grievance.Phase == KingdomPolityGrievancePhase.Open)
				{
					grievance.Phase = KingdomPolityGrievancePhase.Resolved;
					grievance.ResolutionRef = ConclusionId;
				}
			}
		}

		internal static void EndWitnessedFronts(KingdomPolityLedger L,
			KingdomPolityIncidentRecord Plan, string ConclusionId)
		{
			for (int i = 0; i < L.Fronts.Count; i++)
			{
				KingdomPolityFrontRecord front = L.Fronts[i]; bool relevant =
					KingdomPolityAuthority.Contains(Plan.DisclosedStakeRefs, front.TargetRef);
				for (int j = 0; !relevant && j < Plan.ParticipantCohortRefs.Count; j++)
					relevant = front.TargetRef == Plan.ParticipantCohortRefs[j];
				if (!relevant) continue;
				front.PressureBand = 0; front.Phase = KingdomPolityFrontPhase.Ended;
				for (int j = 0; j < front.GrievanceRefs.Count; j++)
				{
					KingdomPolityGrievanceRecord grievance = null;
					for (int k = 0; k < L.Grievances.Count; k++)
						if (L.Grievances[k].GrievanceId == front.GrievanceRefs[j])
							grievance = L.Grievances[k];
					if (grievance != null && grievance.Phase == KingdomPolityGrievancePhase.Open)
					{
						grievance.Phase = KingdomPolityGrievancePhase.Resolved;
						grievance.ResolutionRef = ConclusionId;
					}
				}
				if (front.TargetKind == KingdomPolityFrontTarget.Route)
				{
					KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(L, front.TargetRef);
					if (route != null && route.Phase == KingdomPolityRoutePhase.ConfrontationAvailable)
						route.Phase = KingdomPolityRoutePhase.AvailableToWitness;
				}
			}
		}

		private static KingdomPolityIncidentRecord FindClashPlan(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; L != null && i < L.Incidents.Count; i++)
				if (L.Incidents[i].IncidentPlanId == Id) return L.Incidents[i];
			return null;
		}

		private static KingdomPolityRelation FindRelationById(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Relations.Count; i++)
				if (L.Relations[i].RelationId == Id) return L.Relations[i];
			return null;
		}

		private static bool CanonicalSemantic(IList<string> Values, int Minimum, int Maximum)
		{
			if (Values == null || Values.Count < Minimum || Values.Count > Maximum) return false;
			string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				if (!KingdomPolityRules.SemanticId(Values[i]) ||
					(previous != null && string.CompareOrdinal(previous, Values[i]) >= 0)) return false;
				previous = Values[i];
			}
			return true;
		}

		private static string Digest(KingdomPolityWitnessedClashProof P)
		{
			List<string> values = new List<string> { P.ProofId ?? "", P.IncidentPlanId ?? "",
				P.SurfaceRef ?? "", P.ZoneId ?? "", P.CommitTick.ToString(CultureInfo.InvariantCulture) };
			values.AddRange(P.ObservedFactIds); values.AddRange(P.ParticipantProjectionIds);
			for (int i = 0; i < P.SystemicDeltas.Count; i++) values.Add(((byte)P.SystemicDeltas[i].Kind)
				.ToString(CultureInfo.InvariantCulture) + "|" + P.SystemicDeltas[i].TargetId + "|" +
				P.SystemicDeltas[i].Amount.ToString(CultureInfo.InvariantCulture) + "|" +
				P.SystemicDeltas[i].ReceiptId);
			for (int i = 0; i < P.RelationDeltas.Count; i++) values.Add(P.RelationDeltas[i].RelationId +
				"|" + ((byte)P.RelationDeltas[i].Before).ToString(CultureInfo.InvariantCulture) +
				"|" + ((byte)P.RelationDeltas[i].After).ToString(CultureInfo.InvariantCulture) + "|" +
				P.RelationDeltas[i].ReceiptId);
			values.AddRange(P.ReceiptRefs);
			return KingdomPolityRules.ActivationDigest("polity-witnessed-clash-proof-v1", values);
		}

		private static List<string> Copy(IList<string> Values)
		{
			return Values == null ? new List<string>() : new List<string>(Values);
		}

		private static List<KingdomPolitySystemicDelta> CopySystemic(
			IList<KingdomPolitySystemicDelta> Values)
		{
			List<KingdomPolitySystemicDelta> result = new List<KingdomPolitySystemicDelta>();
			for (int i = 0; Values != null && i < Values.Count; i++) result.Add(new KingdomPolitySystemicDelta
				{ Kind = Values[i].Kind, TargetId = Values[i].TargetId, Amount = Values[i].Amount,
					ReceiptId = Values[i].ReceiptId });
			return result;
		}

		private static List<KingdomPolityRelationDelta> CopyRelations(
			IList<KingdomPolityRelationDelta> Values)
		{
			List<KingdomPolityRelationDelta> result = new List<KingdomPolityRelationDelta>();
			for (int i = 0; Values != null && i < Values.Count; i++) result.Add(new KingdomPolityRelationDelta
				{ RelationId = Values[i].RelationId, Before = Values[i].Before,
					After = Values[i].After, ReceiptId = Values[i].ReceiptId });
			return result;
		}
	}
}
