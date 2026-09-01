using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomPolityV9MigrationRules
	{
		private static bool Untouched(KingdomPolityLedger L, KingdomPolityRecord Current,
			KingdomPolityRecord Imported, KingdomPolityRelation A, KingdomPolityRelation B,
			out KingdomPolityNamedFigureRecord Figure, out string OldCause)
		{
			Figure = null; OldCause = null;
			if (A.Band != KingdomPolityRelationBand.Pact &&
				A.Band != KingdomPolityRelationBand.Rival || A.SourceRefs == null ||
				B.SourceRefs == null || A.SourceRefs.Count != 1 || B.SourceRefs.Count != 1 ||
				A.SourceRefs[0] != B.SourceRefs[0]) return false;
			OldCause = KingdomPolityRules.ActivationId("taf:fact:legacy-relation:v1:",
				"legacy-relation-fact-v1", Current.PolityId, Imported.PolityId,
				A.Band.ToString());
			if (A.SourceRefs[0] != OldCause || !NoCausalConsumers(L) ||
				!string.IsNullOrEmpty(Current.ExternalCounterpartyKey) ||
				!string.IsNullOrEmpty(Imported.ExternalCounterpartyKey)) return false;
			int matches = 0;
			for (int i = 0; i < L.NamedFigures.Count; i++)
			{
				KingdomPolityNamedFigureRecord f = L.NamedFigures[i];
				if (f.PolityId != Imported.PolityId || f.CauseRef != OldCause) continue;
				if (f.Phase != KingdomPolityFigurePhase.Active || f.ResidentId != 0 ||
					!string.IsNullOrEmpty(f.ResidentSettlementId) ||
					!string.IsNullOrEmpty(f.ConclusionRef) ||
					(f.Origin != KingdomPolityFigureOrigin.Namesake &&
					 f.Origin != KingdomPolityFigureOrigin.Successor &&
					 f.Origin != KingdomPolityFigureOrigin.LegacyEnvoy &&
					 f.Origin != KingdomPolityFigureOrigin.Claimant)) return false;
				Figure = f; matches++;
			}
			return matches == 1;
		}

		private static bool NoCausalConsumers(KingdomPolityLedger L)
		{
			if (L.Routes.Count != 0 || L.Grievances.Count != 0 || L.Fronts.Count != 0 ||
				L.Cohorts.Count != 0 || L.Incidents.Count != 0 || L.Compactions.Count != 0 ||
				L.FoldedCompactionCount != 0L) return false;
			for (int i = 0; i < L.Projections.Count; i++)
				if (L.Projections[i].Kind == KingdomPolityProjectionKind.Relation) return false;
			return true;
		}

		private static bool RewriteUntouched(KingdomPolityLedger L,
			KingdomPolityRecord Current, KingdomPolityRecord Imported, KingdomPolityRelation A,
			KingdomPolityRelation B, KingdomPolityNamedFigureRecord Figure, string OldCause,
			out string Failure)
		{
			Failure = null;
			if (L.Revision == long.MaxValue) return Fail("v9 correction exhausted polity revision",
				out Failure);
			string contact = KingdomPolityRules.ActivationId("taf:fact:legacy-contact:v2:",
				"legacy-contact-fact-v2", Current.PolityId, Imported.PolityId);
			string correction = KingdomPolityRules.ActivationId(
				"taf:receipt:foundation-relation-correction:v1:",
				"foundation-relation-correction-v1", A.RelationId, B.RelationId, OldCause,
				A.Band.ToString(), A.ChangedTick.ToString(System.Globalization.CultureInfo.InvariantCulture));
			if (Figure.Origin == KingdomPolityFigureOrigin.Claimant &&
				L.NamedFigures.Count >= KingdomPolityRules.MaxNamedFigures)
			{
				MarkUnresolved(A); MarkUnresolved(B); return true;
			}
			RewriteRelation(A, contact, correction); RewriteRelation(B, contact, correction);
			if (Figure.Origin == KingdomPolityFigureOrigin.Claimant)
			{
				Figure.Phase = KingdomPolityFigurePhase.Transferred;
				Figure.ConclusionRef = correction;
				string name = "the Envoy of " + Imported.DisplayName;
				if (name.Length > 240) name = name.Substring(0, 240);
				L.NamedFigures.Add(new KingdomPolityNamedFigureRecord
				{
					FigureId = KingdomPolityRules.ActivationId("taf:figure:migration-envoy:v1:",
						"foundation-migration-envoy-v1", Imported.PolityId, correction),
					PolityId = Imported.PolityId, DisplayName = name, RoleKey = "envoy",
					Origin = KingdomPolityFigureOrigin.LegacyEnvoy,
					Phase = KingdomPolityFigurePhase.Active, CauseRef = correction
				});
			}
			else Figure.CauseRef = correction;
			L.NamedFigures.Sort((x, y) => string.CompareOrdinal(x.FigureId, y.FigureId));
			L.Revision++; return true;
		}

		private static void RewriteRelation(KingdomPolityRelation R, string Contact,
			string Correction)
		{
			KingdomPolityRelationBand original = R.Band; string old = R.SourceRefs[0];
			R.Band = KingdomPolityRelationBand.Contact;
			R.SourceRefs = new List<string> { Contact, Correction };
			R.SourceRefs.Sort(StringComparer.Ordinal);
			R.FoundationState = KingdomPolityFoundationRelationState.Causal;
			R.InitialBand = original; R.FoundationOriginalCauseRef = old;
			R.FoundationCorrectionReceiptId = Correction;
		}
	}
}
