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

		public static bool TryValidateRealmTransition(KingdomPolityRealmTransition T,
			out string Failure)
		{
			Failure = null;
			if (T == null) return Fail("realm transition is null", out Failure);
			if (T.Phase == KingdomPolityRealmTransitionPhase.None)
				return (T.Version == KingdomPolityRealmTransition.CurrentVersion && T.Revision == 0L &&
					T.ReturnLedgerEnvelope == null && T.Legacy == null &&
					string.IsNullOrEmpty(T.TransitionId) && string.IsNullOrEmpty(T.Fault)) ||
					Fail("empty realm transition carries authority", out Failure);
			if (T.Phase == KingdomPolityRealmTransitionPhase.Quarantined)
				return Fail(T.Fault ?? "realm transition is quarantined", out Failure);
			if (T.Version != KingdomPolityRealmTransition.CurrentVersion || T.Revision < 1L ||
				T.Phase < KingdomPolityRealmTransitionPhase.Prepared ||
				T.Phase > KingdomPolityRealmTransitionPhase.Restored || !string.IsNullOrEmpty(T.Fault) ||
				!TypedId(T.OldRealmId, "taf:realm:") || T.OldCurrentPolityId != T.OldRealmId ||
				T.OldCurrentFactionId != T.OldRealmId || T.ClosedTick <= 0L ||
				T.SourceRevision < 1L || T.RetiredRevision != T.SourceRevision + 1L ||
				!TypedId(T.OldCurrentProjectionId, "taf:projection:faction:") ||
				!Digest(T.OldCurrentProjectionDigest) || !Digest(T.ReturnLedgerDigest) ||
				!Digest(T.RetiredLedgerDigest) ||
				!KingdomPolityProfileRules.ValidLegacy(T.Legacy, out Failure))
				return Fail(Failure ?? "realm transition core evidence is invalid", out Failure);
			string expectedId = ActivationId("taf:polity-transition:exile:v1:",
				"polity-realm-exile-transition-v1", T.OldRealmId,
				T.SourceRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
				T.ClosedTick.ToString(System.Globalization.CultureInfo.InvariantCulture),
				T.ReturnLedgerDigest);
			string expectedCause = ActivationId("taf:fact:polity-exile:v1:",
				"polity-realm-exile-cause-v1", expectedId, T.RetiredLedgerDigest);
			if (T.TransitionId != expectedId || T.CauseRef != expectedCause ||
				T.Legacy.LegacyToken == T.OldRealmId || T.Legacy.LineageToken == T.OldRealmId ||
				!ValidOldImportedFields(T))
				return Fail("realm transition identity proof is invalid", out Failure);
			bool rebound = T.Phase == KingdomPolityRealmTransitionPhase.Rebound;
			if ((rebound && T.ReturnLedgerEnvelope != null) ||
				(!rebound && (T.ReturnLedgerEnvelope == null ||
				 T.ReturnLedgerEnvelope.Length > KingdomPolityCodec.MaxEnvelopeBytes)))
				return Fail("realm transition rollback escrow is invalid", out Failure);
			if (!rebound && (!TryTransitionLedger(T, out KingdomPolityLedger source) ||
				RealmLedgerDigest(T.ReturnLedgerEnvelope) != T.ReturnLedgerDigest ||
				!SourceMatchesTransition(source, T)))
				return Fail("realm transition rollback authority differs", out Failure);
			if (T.Phase == KingdomPolityRealmTransitionPhase.Prepared &&
				(T.Revision != 1L || T.DetachedRevision != 0L || T.ReboundRevision != 0L) ||
				T.Phase == KingdomPolityRealmTransitionPhase.Tombstoned &&
				(T.Revision != 2L || T.DetachedRevision != 0L || T.ReboundRevision != 0L) ||
				(T.Phase == KingdomPolityRealmTransitionPhase.Detached ||
				 T.Phase == KingdomPolityRealmTransitionPhase.Restored) &&
				(T.Revision < 3L || T.DetachedRevision != T.RetiredRevision + 1L ||
				 T.ReboundRevision != 0L))
				return Fail("realm transition phase revision is incoherent", out Failure);
			if (rebound && (T.Revision != 4L || T.DetachedRevision != T.RetiredRevision + 1L ||
				T.ReboundRevision < 1L || !TypedId(T.ReboundRealmId, "taf:realm:") ||
				!SemanticId(T.ReboundPolityId) || !SemanticId(T.ReboundFactionId) ||
				T.ReboundRealmId == T.OldRealmId || T.ReboundPolityId == T.OldCurrentPolityId ||
				T.ReboundFactionId == T.OldCurrentFactionId ||
				T.ReboundPolityId == T.OldImportedPolityId ||
				T.ReboundFactionId == T.OldImportedFactionId))
				return Fail("refounded polity reused retired identity", out Failure);
			return true;
		}

		internal static string RealmLedgerDigest(byte[] Envelope)
		{
			return ActivationDigest("polity-realm-ledger-envelope-v1",
				Envelope == null ? "" : System.Convert.ToBase64String(Envelope));
		}

		internal static bool TryTransitionLedger(KingdomPolityRealmTransition T,
			out KingdomPolityLedger Ledger)
		{
			Ledger = null;
			try { Ledger = KingdomPolityCodec.DecodeEnvelope(T.ReturnLedgerEnvelope); }
			catch { return false; }
			return TryValidate(Ledger, out string _);
		}

		private static bool ValidOldImportedFields(KingdomPolityRealmTransition T)
		{
			bool none = string.IsNullOrEmpty(T.OldImportedPolityId);
			if (none) return string.IsNullOrEmpty(T.OldImportedFactionId) &&
				string.IsNullOrEmpty(T.OldImportedProjectionId) &&
				string.IsNullOrEmpty(T.OldImportedProjectionDigest) && !T.OldImportedWasVisible;
			return SemanticId(T.OldImportedPolityId) && SemanticId(T.OldImportedFactionId) &&
				((string.IsNullOrEmpty(T.OldImportedProjectionId) &&
				  string.IsNullOrEmpty(T.OldImportedProjectionDigest) && !T.OldImportedWasVisible) ||
				 (TypedId(T.OldImportedProjectionId, "taf:projection:faction:") &&
				  Digest(T.OldImportedProjectionDigest)));
		}

		private static bool SourceMatchesTransition(KingdomPolityLedger L,
			KingdomPolityRealmTransition T)
		{
			if (L.Revision != T.SourceRevision || L.RealmId != T.OldRealmId) return false;
			KingdomPolityRecord current = FindPolity(L, T.OldCurrentPolityId);
			KingdomPolityProjectionReceipt projection = FactionReceipt(L, T.OldCurrentPolityId);
			if (current == null || current.Source != KingdomPolitySource.CurrentRealm ||
				projection == null || projection.ProjectionId != T.OldCurrentProjectionId ||
				projection.AppliedDigest != T.OldCurrentProjectionDigest) return false;
			KingdomPolityRecord imported = ImportedPolity(L);
			if (imported == null) return string.IsNullOrEmpty(T.OldImportedPolityId);
			KingdomPolityProjectionReceipt importedProjection = FactionReceipt(L, imported.PolityId);
			return imported.PolityId == T.OldImportedPolityId &&
				imported.ProjectedFactionId == T.OldImportedFactionId &&
				(imported.Lifecycle == KingdomPolityLifecycle.Active) == T.OldImportedWasVisible &&
				((importedProjection == null && string.IsNullOrEmpty(T.OldImportedProjectionId)) ||
				 (importedProjection != null && importedProjection.ProjectionId ==
				  T.OldImportedProjectionId && importedProjection.AppliedDigest ==
				  T.OldImportedProjectionDigest));
		}
	}
}
