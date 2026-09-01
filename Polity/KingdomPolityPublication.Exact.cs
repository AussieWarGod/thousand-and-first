using System;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		private static bool ExactPolity(KingdomPolityLedger L, KingdomPolityRecord E)
		{
			for (int i = 0; i < L.Polities.Count; i++)
			{
				KingdomPolityRecord a = L.Polities[i];
				if (a.PolityId == E.PolityId) return a.DisplayName == E.DisplayName &&
					a.NameRevision == E.NameRevision && a.Source == E.Source &&
					a.Lifecycle == E.Lifecycle && a.ProfileId == E.ProfileId &&
					a.ProfileRevision == E.ProfileRevision &&
					a.ProjectedFactionId == E.ProjectedFactionId &&
					a.ExternalCounterpartyKey == E.ExternalCounterpartyKey && a.EndedTick == E.EndedTick;
			}
			return false;
		}

		private static bool ExactProfile(KingdomPolityLedger L, KingdomPolityProfileRevision E)
		{
			for (int i = 0; i < L.Profiles.Count; i++)
			{
				KingdomPolityProfileRevision a = L.Profiles[i];
				if (a.ProfileId == E.ProfileId && a.Revision == E.Revision)
					return a.PolityId == E.PolityId && a.FactsDigest == E.FactsDigest &&
						a.EffectiveTick == E.EffectiveTick && a.RulesVersion == E.RulesVersion &&
						a.TechnologyBand == E.TechnologyBand &&
						ExactProfileList(a.DerivedFromFactIds, E.DerivedFromFactIds) &&
						ExactProfileList(a.PracticeTags, E.PracticeTags) &&
						ExactProfileList(a.BodyKeys, E.BodyKeys) &&
						ExactProfileList(a.RoleKeys, E.RoleKeys) &&
						ExactProfileList(a.GearKeys, E.GearKeys) &&
						ExactExpressionCues(a.ExpressionCues, E.ExpressionCues) &&
						ExactLoadout(a.Loadout, E.Loadout);
			}
			return false;
		}

		private static bool ExactLegacyPolity(KingdomPolityLedger L, KingdomPolityRecord E)
		{
			for (int i = 0; i < L.Polities.Count; i++)
			{
				KingdomPolityRecord a = L.Polities[i];
				if (a.PolityId != E.PolityId) continue;
				return a.DisplayName == E.DisplayName && a.NameRevision == E.NameRevision &&
					a.Source == E.Source && a.Lifecycle != KingdomPolityLifecycle.Ended &&
					a.ProfileId == E.ProfileId && a.ProfileRevision == E.ProfileRevision &&
					a.ProjectedFactionId == E.ProjectedFactionId &&
					a.ExternalCounterpartyKey == E.ExternalCounterpartyKey && a.EndedTick == 0L;
			}
			return false;
		}

		private static bool ExactRelation(KingdomPolityLedger L, KingdomPolityRelation E)
		{
			for (int i = 0; i < L.Relations.Count; i++)
			{
				KingdomPolityRelation a = L.Relations[i];
				if (a.RelationId == E.RelationId) return a.FromPolityId == E.FromPolityId &&
					a.ToPolityId == E.ToPolityId && a.Band == E.Band &&
					a.ChangedTick == E.ChangedTick && a.SourceRefs.Count == 1 &&
					a.SourceRefs[0] == E.SourceRefs[0] &&
					a.FoundationState == E.FoundationState && a.InitialBand == E.InitialBand &&
					a.FoundationOriginalCauseRef == E.FoundationOriginalCauseRef &&
					a.FoundationCorrectionReceiptId == E.FoundationCorrectionReceiptId;
			}
			return false;
		}

		private static bool ExactFigure(KingdomPolityLedger L, KingdomPolityNamedFigureRecord E)
		{
			for (int i = 0; i < L.NamedFigures.Count; i++)
			{
				KingdomPolityNamedFigureRecord a = L.NamedFigures[i];
				if (a.FigureId == E.FigureId) return a.PolityId == E.PolityId &&
					a.DisplayName == E.DisplayName && a.RoleKey == E.RoleKey &&
					a.Origin == E.Origin && a.Phase == E.Phase && a.CauseRef == E.CauseRef &&
					a.ChronicleRef == E.ChronicleRef && a.ConclusionRef == E.ConclusionRef &&
					a.DeedSummary == E.DeedSummary &&
					a.ResidentId == E.ResidentId &&
					a.ResidentSettlementId == E.ResidentSettlementId;
			}
			return false;
		}

		private static bool ExactProjection(KingdomPolityLedger L,
			KingdomPolityProjectionReceipt E)
		{
			for (int i = 0; i < L.Projections.Count; i++)
			{
				KingdomPolityProjectionReceipt a = L.Projections[i];
				if (a.ProjectionId == E.ProjectionId) return a.Kind == E.Kind &&
					a.SourceRef == E.SourceRef && a.Phase == E.Phase && a.ZoneId == E.ZoneId &&
					a.ObjectIds.Count == 1 && a.ObjectIds[0] == E.ObjectIds[0] &&
					a.PriorDigest == E.PriorDigest && a.AppliedDigest == E.AppliedDigest &&
					a.PreparedTick == E.PreparedTick && a.CommittedTick == E.CommittedTick;
			}
			return false;
		}

		private static bool ExactLoadout(KingdomPolityLoadoutPolicy A,
			KingdomPolityLoadoutPolicy B)
		{
			return A != null && B != null && A.Kind == B.Kind &&
				A.ExpectedValueBudget == B.ExpectedValueBudget &&
				ExactProfileList(A.ExcludedKeys, B.ExcludedKeys) &&
				ExactProfileList(A.SelectedKeys, B.SelectedKeys);
		}

		private static bool ExactProfileList(System.Collections.Generic.IList<string> A,
			System.Collections.Generic.IList<string> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++) if (A[i] != B[i]) return false;
			return true;
		}

		private static bool ExactExpressionCues(
			System.Collections.Generic.IList<KingdomPolityExpressionCue> A,
			System.Collections.Generic.IList<KingdomPolityExpressionCue> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++)
				if (A[i].Kind != B[i].Kind || A[i].ExpressionKey != B[i].ExpressionKey ||
					A[i].Weight != B[i].Weight || A[i].SourceKind != B[i].SourceKind ||
					A[i].SourceValueKey != B[i].SourceValueKey || A[i].SourceRef != B[i].SourceRef ||
					A[i].ReasonFactId != B[i].ReasonFactId) return false;
			return true;
		}
	}
}
