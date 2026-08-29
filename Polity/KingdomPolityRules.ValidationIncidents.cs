using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		private static bool ValidateIncidentState(KingdomPolityLedger L, out string Failure)
		{
			if (!ValidateNamedFigures(L.NamedFigures, out Failure)) return false;
			if (!ValidateIncidents(L.Incidents, out Failure)) return false;
			if (!ValidateProjections(L.Projections, out Failure)) return false;
			return ValidateCompactions(L, out Failure);
		}

		private static bool ValidateNamedFigures(IList<KingdomPolityNamedFigureRecord> Values,
			out string Failure)
		{
			Failure = null; string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolityNamedFigureRecord f = Values[i];
				if (f == null || !TypedId(f.FigureId, "taf:figure:") ||
					!After(previous, f.FigureId) || !SemanticId(f.PolityId) ||
					!Text(f.DisplayName, true) || !Text(f.RoleKey, true) ||
					!Defined((byte)f.Origin, 7) || f.Origin == KingdomPolityFigureOrigin.None ||
					!Defined((byte)f.Phase, 5) || !SemanticId(f.CauseRef) ||
					!OptionalId(f.ChronicleRef) || !OptionalId(f.ConclusionRef) ||
					!ValidResidentBridge(f))
					return Fail("named figure is invalid or noncanonical", out Failure);
				previous = f.FigureId;
				if ((f.Phase == KingdomPolityFigurePhase.Active) !=
					string.IsNullOrEmpty(f.ConclusionRef))
					return Fail("named figure conclusion is incoherent", out Failure);
			}
			return true;
		}

		private static bool ValidResidentBridge(KingdomPolityNamedFigureRecord Figure)
		{
			bool hasId = Figure.ResidentId != 0;
			bool hasSettlement = !string.IsNullOrEmpty(Figure.ResidentSettlementId);
			if (!hasId && !hasSettlement) return true;
			return Figure.ResidentId > 0 && hasSettlement &&
				TypedId(Figure.ResidentSettlementId, "taf:settlement:v1:");
		}

		private static bool ValidateIncidents(IList<KingdomPolityIncidentRecord> Values,
			out string Failure)
		{
			Failure = null; string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolityIncidentRecord p = Values[i];
				if (p == null || !TypedId(p.IncidentPlanId, "taf:incident-plan:") ||
					!After(previous, p.IncidentPlanId) || !TypedId(p.IncidentId, "taf:incident:") ||
					!SortedSemanticRefs(p.GrievanceRefs, MaxRefs, true) ||
					!SortedSemanticRefs(p.ParticipantCohortRefs, MaxRefs, true) ||
					!SortedSemanticRefs(p.DisclosedStakeRefs, MaxRefs, true) ||
					p.MaxSystemicWound < 0 || p.MaxSystemicWound > MaxValueBudget ||
					!Defined((byte)p.Purpose, 7) || p.Purpose == KingdomPolityCohortPurpose.None ||
					!TypedId(p.EventStreamId, "taf:stream:") || p.RulesVersion < 1 ||
					!SortedSemanticRefs(p.EligibleSurfaceRefs, MaxRefs, true) ||
					!SortedText(p.InterventionOptionKeys, MaxRefs, true) ||
					!ValidConclusion(p.Conclusion, p.MaxSystemicWound, p.DisclosedStakeRefs) ||
					!KingdomPolityHospitalityRules.TryValidateTransaction(p.Hospitality,
						p.IncidentPlanId, p.Conclusion, out _) && p.Hospitality != null ||
					!KingdomPolityConflictRules.TryValidateIncident(p, out _) ||
					!KingdomPolityCorrespondenceRules.TryValidateIncident(p, out _))
					return Fail("incident plan or conclusion is invalid", out Failure);
				previous = p.IncidentPlanId;
				if (p.Hospitality != null && (p.Purpose != KingdomPolityCohortPurpose.Envoy ||
					!Contains(p.EligibleSurfaceRefs, p.Hospitality.SurfaceRef)))
					return Fail("hospitality is not bound to an eligible envoy surface", out Failure);
				for (int j = 0; j < i; j++) if (Values[j].IncidentId == p.IncidentId)
					return Fail("incident has more than one plan", out Failure);
			}
			return true;
		}

		private static bool ValidConclusion(KingdomPolityIncidentConclusion C, int MaxWound,
			IList<string> DisclosedStakes)
		{
			if (C == null) return true;
			bool consented = C.ResolutionKind == KingdomPolityResolutionKind.ConsentedEscrow;
			if (!TypedId(C.ConclusionId, "taf:conclusion:") ||
				!Defined((byte)C.ResolutionKind, 2) ||
				C.ResolutionKind == KingdomPolityResolutionKind.None || C.CommitTick < 0L ||
				!SortedSemanticRefs(C.ObservedFactIds, MaxObservedFacts, !consented) ||
				!ValidSystemicDeltas(C.SystemicDeltas, C.ResolutionKind, MaxWound,
					DisclosedStakes) ||
				!ValidRelationDeltas(C.RelationDeltas) ||
				!SortedSemanticRefs(C.ReceiptRefs, MaxRefs, true)) return false;
			if (consented)
			{
				return C.ObservedFactIds.Count == 0 && SemanticId(C.ConsentReceiptId) &&
					SemanticId(C.EscrowReceiptId) && SemanticId(C.SnapshotReceiptId) &&
					C.RelationDeltas.Count == 0 && Contains(C.ReceiptRefs, C.ConsentReceiptId) &&
					Contains(C.ReceiptRefs, C.EscrowReceiptId) &&
					Contains(C.ReceiptRefs, C.SnapshotReceiptId);
			}
			return string.IsNullOrEmpty(C.ConsentReceiptId) &&
				string.IsNullOrEmpty(C.EscrowReceiptId) && string.IsNullOrEmpty(C.SnapshotReceiptId);
		}

		private static bool ValidSystemicDeltas(IList<KingdomPolitySystemicDelta> Values,
			KingdomPolityResolutionKind Resolution, int MaxWound, IList<string> DisclosedStakes)
		{
			if (!Count(Values, MaxDeltas)) return false;
			string previous = null; int previousKind = -1; int wounds = 0, stakes = 0;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolitySystemicDelta d = Values[i];
				if (d == null || !Defined((byte)d.Kind, 6) ||
					d.Kind == KingdomPolitySystemicDeltaKind.None || !SemanticId(d.TargetId) ||
					d.Amount == 0 || d.Amount < -MaxValueBudget || d.Amount > MaxValueBudget ||
					!SemanticId(d.ReceiptId) || !DeltaAfter(previousKind, previous, d)) return false;
				previousKind = (int)d.Kind; previous = d.TargetId + "\n" + d.ReceiptId;
				if (d.Kind == KingdomPolitySystemicDeltaKind.ReversibleWound)
				{
					wounds++;
					if (Math.Abs((long)d.Amount) > MaxWound) return false;
				}
				if (d.Kind == KingdomPolitySystemicDeltaKind.ReservedStake)
				{
					stakes++;
					if (!Contains(DisclosedStakes, d.TargetId)) return false;
				}
				if (Resolution == KingdomPolityResolutionKind.ConsentedEscrow &&
					d.Kind != KingdomPolitySystemicDeltaKind.ReservedStake &&
					d.Kind != KingdomPolitySystemicDeltaKind.ReversibleWound) return false;
			}
			return wounds <= 1 && stakes <= 1 &&
				(Resolution != KingdomPolityResolutionKind.ConsentedEscrow || Values.Count <= 2);
		}

		private static bool Contains(IList<string> Values, string Value)
		{
			if (Values == null || Value == null) return false;
			for (int i = 0; i < Values.Count; i++) if (Values[i] == Value) return true;
			return false;
		}

		private static bool DeltaAfter(int PreviousKind, string Previous,
			KingdomPolitySystemicDelta Current)
		{
			int kind = (int)Current.Kind;
			string key = Current.TargetId + "\n" + Current.ReceiptId;
			return kind > PreviousKind || (kind == PreviousKind && After(Previous, key));
		}

		private static bool ValidRelationDeltas(IList<KingdomPolityRelationDelta> Values)
		{
			if (!Count(Values, MaxDeltas)) return false;
			string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolityRelationDelta d = Values[i];
				string key = d == null ? null : d.RelationId + "\n" + d.ReceiptId;
				if (d == null || !TypedId(d.RelationId, "taf:relation:") ||
					!Defined((byte)d.Before, 6) || !Defined((byte)d.After, 6) ||
					d.Before == d.After || !SemanticId(d.ReceiptId) || !After(previous, key))
					return false;
				previous = key;
			}
			return true;
		}

		private static bool ValidateProjections(IList<KingdomPolityProjectionReceipt> Values,
			out string Failure)
		{
			Failure = null; string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolityProjectionReceipt p = Values[i];
				if (p == null || !TypedId(p.ProjectionId, "taf:projection:") ||
					!After(previous, p.ProjectionId) || !Defined((byte)p.Kind, 8) ||
					p.Kind == KingdomPolityProjectionKind.None || !SemanticId(p.SourceRef) ||
					!Defined((byte)p.Phase, 4) || !Text(p.ZoneId, false) ||
					!SortedText(p.ObjectIds, MaxRefs, false) || !Digest(p.PriorDigest) ||
					!Digest(p.AppliedDigest) || p.PreparedTick < 0L || p.CommittedTick < 0L)
					return Fail("projection receipt is invalid or noncanonical", out Failure);
				previous = p.ProjectionId;
				if (p.Phase == KingdomPolityProjectionPhase.Prepared && p.CommittedTick != 0L)
					return Fail("prepared projection carries commit tick", out Failure);
				if (p.Phase != KingdomPolityProjectionPhase.Prepared &&
					p.Phase != KingdomPolityProjectionPhase.Cancelled &&
					p.CommittedTick < p.PreparedTick)
					return Fail("committed projection has invalid tick", out Failure);
			}
			return true;
		}

		private static bool ValidateCompactions(KingdomPolityLedger L, out string Failure)
		{
			Failure = null; string previous = null;
			for (int i = 0; i < L.Compactions.Count; i++)
			{
				KingdomPolityCompactionReceipt c = L.Compactions[i];
				if (c == null || !TypedId(c.ReceiptId, "taf:compaction:") ||
					!After(previous, c.ReceiptId) || c.SourceRevision < 0L ||
					c.CommittedRevision != c.SourceRevision + 1L ||
					c.CommittedRevision > L.Revision || c.CommitTick < 0L ||
					!ValidProfileRefs(c.RemovedProfiles) || !Digest(c.RemovedDigest))
					return Fail("compaction receipt is invalid or noncanonical", out Failure);
				previous = c.ReceiptId;
				for (int j = 0; j < i; j++)
					if (L.Compactions[j].CommittedRevision == c.CommittedRevision)
						return Fail("compaction revision is duplicated", out Failure);
			}
			return true;
		}

		private static bool ValidProfileRefs(IList<KingdomPolityProfileRef> Values)
		{
			if (Values == null || Values.Count < 1 || Values.Count > MaxProfiles) return false;
			string previous = null; int revision = 0;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolityProfileRef p = Values[i];
				if (p == null || !TypedId(p.ProfileId, "taf:polity-profile:") || p.Revision < 1 ||
					!ProfileAfter(previous, revision, p.ProfileId, p.Revision)) return false;
				previous = p.ProfileId; revision = p.Revision;
			}
			return true;
		}
	}
}
