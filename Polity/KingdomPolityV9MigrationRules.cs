using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomPolityV9MigrationRules
	{
		internal static bool TryMigrate(KingdomPolityLedger L, int SourceFormat,
			out string Failure)
		{
			Failure = null;
			if (L == null || SourceFormat < KingdomPolityRules.LegacyFormatVersion ||
				SourceFormat > KingdomPolityRules.AdmissionPriorFormatVersion)
				return Fail("v9 polity migration source is invalid", out Failure);
			for (int i = 0; i < L.Relations.Count; i++) ClearProvenance(L.Relations[i]);
			KingdomPolityRecord current = null, imported = null;
			for (int i = 0; i < L.Polities.Count; i++)
			{
				KingdomPolityRecord p = L.Polities[i];
				if (p.Source == KingdomPolitySource.CurrentRealm)
				{
					if (current != null) return Fail("v9 migration found multiple current polities",
						out Failure); current = p;
				}
				else if (p.Source == KingdomPolitySource.ImportedLegacy)
				{
					if (imported != null) return Fail("v9 migration found multiple imported foundations",
						out Failure); imported = p;
				}
			}
			if (current == null || imported == null) return true;
			KingdomPolityRelation forward = Find(L, current.PolityId, imported.PolityId);
			KingdomPolityRelation reverse = Find(L, imported.PolityId, current.PolityId);
			// A pair that does not match the canonical foundation identity is not a
			// foundation pair; migration preserves it as ordinary provenance untouched.
			if (!ExactPairIdentity(forward, reverse, current.PolityId, imported.PolityId))
				return true;
			if (Untouched(L, current, imported, forward, reverse,
				out KingdomPolityNamedFigureRecord figure, out string oldCause))
				return RewriteUntouched(L, current, imported, forward, reverse, figure, oldCause,
					out Failure);
			MarkUnresolved(forward); MarkUnresolved(reverse); return true;
		}

		private static void ClearProvenance(KingdomPolityRelation R)
		{
			if (R == null) return;
			R.FoundationState = KingdomPolityFoundationRelationState.Ordinary;
			R.InitialBand = KingdomPolityRelationBand.Unspecified;
			R.FoundationOriginalCauseRef = R.FoundationCorrectionReceiptId = null;
		}

		private static KingdomPolityRelation Find(KingdomPolityLedger L, string From, string To)
		{
			KingdomPolityRelation found = null;
			for (int i = 0; i < L.Relations.Count; i++)
				if (L.Relations[i].FromPolityId == From && L.Relations[i].ToPolityId == To)
				{
					if (found != null) return null; found = L.Relations[i];
				}
			return found;
		}

		private static bool ExactPairIdentity(KingdomPolityRelation A,
			KingdomPolityRelation B, string Current, string Imported)
		{
			return A != null && B != null && A.RelationId == KingdomPolityRules.ActivationId(
				"taf:relation:v1:", "polity-relation-id-v1", Current, Imported) &&
				B.RelationId == KingdomPolityRules.ActivationId("taf:relation:v1:",
					"polity-relation-id-v1", Imported, Current) && A.Band == B.Band &&
				A.ChangedTick == B.ChangedTick;
		}

		private static void MarkUnresolved(KingdomPolityRelation R)
		{
			R.FoundationState = KingdomPolityFoundationRelationState.LegacyUnresolved;
			R.InitialBand = R.Band;
			R.FoundationOriginalCauseRef = R.SourceRefs != null && R.SourceRefs.Count == 1
				? R.SourceRefs[0] : null;
		}

		private static bool Fail(string Reason, out string Failure)
		{
			Failure = Reason; return false;
		}
	}
}
