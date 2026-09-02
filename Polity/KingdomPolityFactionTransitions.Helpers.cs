namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		public static bool TryGetImportedFactionProjection(KingdomPolityLedger Ledger,
			out KingdomPolityFactionProjectionView View, out string Failure)
		{
			View = null; Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			KingdomPolityRecord polity = ImportedPolity(Ledger);
			if (polity == null) { Failure = "no imported polity is published"; return false; }
			KingdomPolityProjectionReceipt receipt = FactionReceipt(Ledger, polity.PolityId);
			if (receipt == null || !ValidImportedFactionReceipt(Ledger, receipt, polity))
			{
				Failure = "imported polity has no exact faction projection"; return false;
			}
			View = new KingdomPolityFactionProjectionView
			{
				PolityId = polity.PolityId, FactionId = polity.ProjectedFactionId,
				ProjectionId = receipt.ProjectionId, AppliedDigest = receipt.AppliedDigest,
				Phase = receipt.Phase, Lifecycle = polity.Lifecycle
			};
			return true;
		}

		private static KingdomPolityRecord ImportedPolity(KingdomPolityLedger L)
		{
			KingdomPolityRecord result = null;
			for (int i = 0; i < L.Polities.Count; i++)
				if (L.Polities[i].Source == KingdomPolitySource.ImportedLegacy)
				{
					if (result != null) return null; result = L.Polities[i];
				}
			return result;
		}

		private static KingdomPolityRecord FindPolity(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Polities.Count; i++) if (L.Polities[i].PolityId == Id) return L.Polities[i];
			return null;
		}

		private static KingdomPolityProfileRevision FindProfile(KingdomPolityLedger L,
			string Id, int Revision)
		{
			for (int i = 0; i < L.Profiles.Count; i++)
				if (L.Profiles[i].ProfileId == Id && L.Profiles[i].Revision == Revision) return L.Profiles[i];
			return null;
		}

		private static KingdomPolityProjectionReceipt FindProjection(KingdomPolityLedger L,
			string Id)
		{
			for (int i = 0; i < L.Projections.Count; i++)
				if (L.Projections[i].ProjectionId == Id) return L.Projections[i];
			return null;
		}

		private static KingdomPolityProjectionReceipt FactionReceipt(KingdomPolityLedger L,
			string PolityId)
		{
			KingdomPolityProjectionReceipt result = null;
			for (int i = 0; i < L.Projections.Count; i++)
				if (L.Projections[i].Kind == KingdomPolityProjectionKind.Faction &&
					L.Projections[i].SourceRef == PolityId)
				{
					if (result != null) return null; result = L.Projections[i];
				}
			return result;
		}

		private static KingdomPolityProjectionReceipt CommittedFactionReceipt(
			KingdomPolityLedger L, string PolityId)
		{
			KingdomPolityProjectionReceipt result = FactionReceipt(L, PolityId);
			KingdomPolityRecord polity = FindPolity(L, PolityId);
			return result != null && result.Phase == KingdomPolityProjectionPhase.Committed &&
				ValidImportedFactionReceipt(L, result, polity) ? result : null;
		}

		private static bool ValidImportedFactionReceipt(KingdomPolityLedger L,
			KingdomPolityProjectionReceipt R, KingdomPolityRecord P)
		{
			KingdomPolityProfileRevision profile = P == null ? null :
				FindProfile(L, P.ProfileId, P.ProfileRevision);
			if (R == null || P == null || profile == null) return false;
			KingdomPolityProjectionReceipt expected = FoundationProjection(P.PolityId,
				P.ProjectedFactionId, ProfileExpressionDigest(profile), R.PreparedTick, false);
			return P.Source == KingdomPolitySource.ImportedLegacy &&
				R.ProjectionId == expected.ProjectionId &&
				R.Kind == KingdomPolityProjectionKind.Faction && R.SourceRef == P.PolityId &&
				R.ObjectIds.Count == 1 && R.ObjectIds[0] == P.ProjectedFactionId &&
				R.PriorDigest == expected.PriorDigest && R.AppliedDigest == expected.AppliedDigest &&
				string.IsNullOrEmpty(R.ZoneId) &&
				(R.Phase == KingdomPolityProjectionPhase.Prepared ||
				 R.Phase == KingdomPolityProjectionPhase.Committed);
		}

		private static bool SameFactionReceipt(KingdomPolityProjectionReceipt A,
			KingdomPolityProjectionReceipt E)
		{
			return A.Kind == E.Kind && A.SourceRef == E.SourceRef &&
				A.ObjectIds.Count == 1 && A.ObjectIds[0] == E.ObjectIds[0] &&
				A.PriorDigest == E.PriorDigest && A.AppliedDigest == E.AppliedDigest &&
				(A.Phase == KingdomPolityProjectionPhase.Prepared ||
				 A.Phase == KingdomPolityProjectionPhase.Committed);
		}

		private static bool ValidTombstone(KingdomPolityProjectionReceipt R,
			KingdomPolityRecord P, KingdomPolityProjectionReceipt Faction)
		{
			string id = P == null || Faction == null ? null : ActivationId(
				"taf:projection:faction-tombstone:v1:", "polity-faction-tombstone-v1",
				P.PolityId, P.ProjectedFactionId, Faction.AppliedDigest);
			return R != null && P != null && Faction != null &&
				R.ProjectionId == id &&
				R.Kind == KingdomPolityProjectionKind.FactionTombstone &&
				R.SourceRef == P.PolityId && R.ObjectIds.Count == 1 &&
				R.ObjectIds[0] == P.ProjectedFactionId && R.PriorDigest == Faction.AppliedDigest &&
				R.AppliedDigest == ActivationDigest("polity-faction-hidden-v1", P.PolityId,
					P.ProjectedFactionId, Faction.AppliedDigest) && string.IsNullOrEmpty(R.ZoneId) &&
				(R.Phase == KingdomPolityProjectionPhase.Prepared ||
				 R.Phase == KingdomPolityProjectionPhase.Committed);
		}
	}
}
