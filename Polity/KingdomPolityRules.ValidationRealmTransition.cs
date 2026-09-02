namespace ThousandAndFirst
{
	/// <summary>Realm-transition evidence: validator, ledger digest, rollback decode, and the
	/// phase-exact ledger checks the CAS ladder consults.</summary>
	public static partial class KingdomPolityRules
	{
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
			KingdomPolityProfileRevision profile = current == null ? null :
				FindProfile(L, current.ProfileId, current.ProfileRevision);
			if (current == null || current.Source != KingdomPolitySource.CurrentRealm ||
				profile == null || !KingdomPolityProfileRules.MatchesLegacyProfileSource(
					T.Legacy, profile) || projection == null ||
				projection.ProjectionId != T.OldCurrentProjectionId ||
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

		private static bool RetiredLedgerExact(KingdomPolityLedger L,
			KingdomPolityRealmTransition T)
		{
			if (L.Revision != T.RetiredRevision || L.RealmId != T.OldRealmId) return false;
			try { return RealmLedgerDigest(KingdomPolityCodec.EncodeEnvelope(L)) ==
				T.RetiredLedgerDigest; } catch { return false; }
		}

		private static bool DetachedLedgerExact(KingdomPolityLedger L,
			KingdomPolityRealmTransition T)
		{
			return L.Revision == T.DetachedRevision && !L.IdentityBound &&
				string.IsNullOrEmpty(L.RealmId) && L.Polities.Count == 0 && L.Projections.Count == 0;
		}

		private static bool RestoredLedgerExact(KingdomPolityLedger L,
			KingdomPolityRealmTransition T)
		{
			try { return L.Revision == T.SourceRevision && L.RealmId == T.OldRealmId &&
				RealmLedgerDigest(KingdomPolityCodec.EncodeEnvelope(L)) == T.ReturnLedgerDigest; }
			catch { return false; }
		}

		private static bool RefoundLedgerExact(KingdomPolityLedger L,
			KingdomPolityRealmTransition T, bool ReceiptCommitted)
		{
			KingdomPolityRecord imported = ImportedPolity(L);
			if (!L.IdentityBound || L.RealmId == T.OldRealmId || imported == null ||
				CommittedFactionReceipt(L, imported.PolityId) == null ||
				imported.Lifecycle != KingdomPolityLifecycle.Active ||
				imported.PolityId == T.OldCurrentPolityId || imported.PolityId == T.OldImportedPolityId ||
				imported.ProjectedFactionId == T.OldCurrentFactionId ||
				imported.ProjectedFactionId == T.OldImportedFactionId ||
				!TryObserveCurrentFoundation(L, L.RealmId, L.RealmId, out string _)) return false;
			return !ReceiptCommitted || L.Revision == T.ReboundRevision &&
				L.RealmId == T.ReboundRealmId && imported.PolityId == T.ReboundPolityId &&
				imported.ProjectedFactionId == T.ReboundFactionId;
		}
	}
}
