namespace ThousandAndFirst
{
	/// <summary>Exact observation of the immutable current-realm foundation authority.</summary>
	public static partial class KingdomPolityRules
	{
		public static bool TryObserveCurrentFoundation(KingdomPolityLedger Ledger,
			string RealmId, string FactionId, out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure) || Ledger.RealmId != RealmId ||
				RealmId != FactionId)
				return Fail("current foundation identity is not exact", out Failure);
			KingdomPolityRecord current = null;
			for (int i = 0; i < Ledger.Polities.Count; i++)
				if (Ledger.Polities[i].Source == KingdomPolitySource.CurrentRealm)
				{
					if (current != null)
						return Fail("current foundation polity is not unique", out Failure);
					current = Ledger.Polities[i];
				}
			if (current == null || current.PolityId != RealmId ||
				current.ProjectedFactionId != FactionId || current.NameRevision != 1 ||
				current.Lifecycle != KingdomPolityLifecycle.Active ||
				current.ProfileRevision < 1)
				return Fail("current foundation polity is absent or changed", out Failure);
			KingdomPolityProfileRevision profile = null;
			for (int i = 0; i < Ledger.Profiles.Count; i++)
				if (Ledger.Profiles[i].ProfileId == current.ProfileId &&
					Ledger.Profiles[i].Revision == current.ProfileRevision)
				{
					if (profile != null)
						return Fail("current foundation profile is not unique", out Failure);
					profile = Ledger.Profiles[i];
				}
			if (profile == null || profile.PolityId != RealmId || profile.RulesVersion !=
				KingdomPolityProfileRules.RulesVersion)
				return Fail("current foundation profile is absent or foreign", out Failure);
			KingdomPolityProjectionReceipt receipt = null;
			for (int i = 0; i < Ledger.Projections.Count; i++)
				if (Ledger.Projections[i].Kind == KingdomPolityProjectionKind.Faction &&
					Ledger.Projections[i].SourceRef == RealmId)
				{
					if (receipt != null)
						return Fail("current foundation receipt is not unique", out Failure);
					receipt = Ledger.Projections[i];
				}
			if (receipt == null || receipt.Phase != KingdomPolityProjectionPhase.Committed)
				return Fail("current foundation receipt is absent or uncommitted", out Failure);
			KingdomPolityProfileRevision foundation = null;
			for (int i = 0; i < Ledger.Profiles.Count; i++)
				if (Ledger.Profiles[i].ProfileId == current.ProfileId &&
					Ledger.Profiles[i].Revision == 1) foundation = Ledger.Profiles[i];
			if (foundation == null || foundation.PolityId != RealmId)
				return Fail("current foundation profile root is absent", out Failure);
			KingdomPolityProjectionReceipt expected = FoundationProjection(RealmId, FactionId,
				ProfileExpressionDigest(foundation), receipt.PreparedTick, true);
			return ExactFoundationReceipt(receipt, expected) ||
				Fail("current foundation receipt does not prove the profile", out Failure);
		}

		private static bool ExactFoundationReceipt(KingdomPolityProjectionReceipt Actual,
			KingdomPolityProjectionReceipt Expected)
		{
			return Actual.ProjectionId == Expected.ProjectionId &&
				Actual.Kind == Expected.Kind && Actual.SourceRef == Expected.SourceRef &&
				Actual.Phase == Expected.Phase && Actual.ObjectIds.Count == 1 &&
				Actual.ObjectIds[0] == Expected.ObjectIds[0] &&
				Actual.PriorDigest == Expected.PriorDigest &&
				Actual.AppliedDigest == Expected.AppliedDigest &&
				Actual.PreparedTick == Expected.PreparedTick &&
				Actual.CommittedTick == Expected.CommittedTick &&
				string.IsNullOrEmpty(Actual.ZoneId);
		}

		public static bool TryMarkRealmExileTombstoned(KingdomPolityLedger Ledger,
			long ExpectedLedgerRevision, KingdomPolityRealmTransition Transition,
			long ExpectedTransitionRevision, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = BeginResult(Ledger); Failure = null;
			if (!TryValidate(Ledger, out Failure) ||
				!TryValidateRealmTransition(Transition, out Failure))
				return Refuse(Result, Failure, out Failure);
			if (Transition.Phase == KingdomPolityRealmTransitionPhase.Tombstoned)
			{
				if (!RetiredLedgerExact(Ledger, Transition))
					return Refuse(Result, "tombstoned receipt differs from retired ledger", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (Transition.Phase != KingdomPolityRealmTransitionPhase.Prepared ||
				Ledger.Revision != ExpectedLedgerRevision ||
				Transition.Revision != ExpectedTransitionRevision)
				return Conflict(Result, out Failure);
			if (!RetiredLedgerExact(Ledger, Transition))
				return Refuse(Result, "retired ledger differs before tombstone commit", out Failure);
			KingdomPolityRealmTransition next = CloneTransition(Transition);
			next.Phase = KingdomPolityRealmTransitionPhase.Tombstoned; next.Revision = 2L;
			if (!TryValidateRealmTransition(next, out Failure))
				return Refuse(Result, Failure, out Failure);
			Transition.CopyFrom(next); Result.Outcome = KingdomPolityCasOutcome.Applied;
			Result.CommittedRevision = Ledger.Revision; return true;
		}

		public static bool TryDetachRealmExile(KingdomPolityLedger Ledger,
			long ExpectedLedgerRevision, KingdomPolityRealmTransition Transition,
			long ExpectedTransitionRevision, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = BeginResult(Ledger); Failure = null;
			if (!TryValidate(Ledger, out Failure) ||
				!TryValidateRealmTransition(Transition, out Failure))
				return Refuse(Result, Failure, out Failure);
			if (Transition.Phase == KingdomPolityRealmTransitionPhase.Detached &&
				DetachedLedgerExact(Ledger, Transition))
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (Transition.Phase != KingdomPolityRealmTransitionPhase.Tombstoned ||
				Ledger.Revision != ExpectedLedgerRevision ||
				Transition.Revision != ExpectedTransitionRevision)
				return Conflict(Result, out Failure);
			if (!RetiredLedgerExact(Ledger, Transition))
				return Refuse(Result, "retired ledger differs before detach", out Failure);
			KingdomPolityLedger candidate = new KingdomPolityLedger
			{
				Revision = Transition.RetiredRevision + 1L,
				Options = KingdomPolityCodec.DisabledDefaultOptions()
			};
			if (!TryValidate(candidate, out Failure)) return Refuse(Result, Failure, out Failure);
			KingdomPolityRealmTransition next = CloneTransition(Transition);
			next.Phase = KingdomPolityRealmTransitionPhase.Detached; next.Revision = 3L;
			next.DetachedRevision = candidate.Revision;
			if (!TryValidateRealmTransition(next, out Failure))
				return Refuse(Result, Failure, out Failure);
			Ledger.CopyFrom(candidate); Transition.CopyFrom(next);
			Result.Outcome = KingdomPolityCasOutcome.Applied;
			Result.CommittedRevision = candidate.Revision; return true;
		}

		public static bool TryRestoreRealmReturn(KingdomPolityLedger Ledger,
			long ExpectedLedgerRevision, KingdomPolityRealmTransition Transition,
			long ExpectedTransitionRevision, string RealmId,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = BeginResult(Ledger); Failure = null;
			if (!TryValidate(Ledger, out Failure) ||
				!TryValidateRealmTransition(Transition, out Failure))
				return Refuse(Result, Failure, out Failure);
			if (Transition.Phase == KingdomPolityRealmTransitionPhase.Restored)
			{
				if (RealmId != Transition.OldRealmId || !RestoredLedgerExact(Ledger, Transition))
					return Refuse(Result, "restored receipt differs from current ledger", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (Transition.Phase != KingdomPolityRealmTransitionPhase.Detached ||
				RealmId != Transition.OldRealmId || Ledger.Revision != ExpectedLedgerRevision ||
				Transition.Revision != ExpectedTransitionRevision ||
				!DetachedLedgerExact(Ledger, Transition)) return Conflict(Result, out Failure);
			if (!TryTransitionLedger(Transition, out KingdomPolityLedger restored))
				return Refuse(Result, "rollback polity authority cannot decode", out Failure);
			KingdomPolityRealmTransition next = CloneTransition(Transition);
			next.Phase = KingdomPolityRealmTransitionPhase.Restored; next.Revision = 4L;
			if (!TryValidateRealmTransition(next, out Failure))
				return Refuse(Result, Failure, out Failure);
			Ledger.CopyFrom(restored); Transition.CopyFrom(next);
			Result.Outcome = KingdomPolityCasOutcome.Applied;
			Result.CommittedRevision = restored.Revision; return true;
		}

		public static bool TryCommitRealmRefound(KingdomPolityLedger Ledger,
			KingdomPolityRealmTransition Transition, long ExpectedTransitionRevision,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = BeginResult(Ledger); Failure = null;
			if (!TryValidate(Ledger, out Failure) ||
				!TryValidateRealmTransition(Transition, out Failure))
				return Refuse(Result, Failure, out Failure);
			if (Transition.Phase == KingdomPolityRealmTransitionPhase.Rebound)
			{
				if (!RefoundLedgerExact(Ledger, Transition, true))
					return Refuse(Result, "refound receipt differs from current ledger", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CurrentPolityId = Transition.ReboundRealmId;
				Result.ImportedPolityId = Transition.ReboundPolityId;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			KingdomPolityRecord imported = ImportedPolity(Ledger);
			if (Transition.Phase != KingdomPolityRealmTransitionPhase.Detached ||
				Transition.Revision != ExpectedTransitionRevision ||
				!RefoundLedgerExact(Ledger, Transition, false))
				return Refuse(Result, "refounded polity lacks fresh committed authority", out Failure);
			KingdomPolityRealmTransition next = CloneTransition(Transition);
			next.Phase = KingdomPolityRealmTransitionPhase.Rebound; next.Revision = 4L;
			next.ReboundRealmId = Ledger.RealmId; next.ReboundPolityId = imported.PolityId;
			next.ReboundFactionId = imported.ProjectedFactionId;
			next.ReboundRevision = Ledger.Revision; next.ReturnLedgerEnvelope = null;
			if (!TryValidateRealmTransition(next, out Failure))
				return Refuse(Result, Failure, out Failure);
			Transition.CopyFrom(next); Result.Outcome = KingdomPolityCasOutcome.Applied;
			Result.CurrentPolityId = Ledger.RealmId; Result.ImportedPolityId = imported.PolityId;
			Result.CommittedRevision = Ledger.Revision; return true;
		}

		public static bool TryGetRealmTransitionLegacy(KingdomPolityRealmTransition Transition,
			out KingdomPolityLegacySnapshot Legacy, out string Failure)
		{
			Legacy = null;
			if (!TryValidateRealmTransition(Transition, out Failure) ||
				Transition.Phase != KingdomPolityRealmTransitionPhase.Detached)
				return Fail(Failure ?? "no detached realm legacy is available", out Failure);
			Legacy = CopyLegacy(Transition.Legacy); return true;
		}

		public static bool TryCompleteRealmReturn(KingdomPolityLedger Ledger,
			KingdomPolityRealmTransition Transition, long ExpectedTransitionRevision,
			string RealmId, out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = BeginResult(Ledger); Failure = null;
			if (!TryValidate(Ledger, out Failure) ||
				!TryValidateRealmTransition(Transition, out Failure))
				return Refuse(Result, Failure, out Failure);
			if (Transition.Phase == KingdomPolityRealmTransitionPhase.None &&
				TryObserveCurrentFoundation(Ledger, RealmId, RealmId, out Failure))
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (Transition.Phase != KingdomPolityRealmTransitionPhase.Restored ||
				Transition.Revision != ExpectedTransitionRevision || RealmId != Transition.OldRealmId ||
				!RestoredLedgerExact(Ledger, Transition) ||
				!TryObserveCurrentFoundation(Ledger, RealmId, RealmId, out Failure))
				return Refuse(Result, Failure ?? "returned polity is not exact", out Failure);
			Transition.CopyFrom(new KingdomPolityRealmTransition());
			Result.Outcome = KingdomPolityCasOutcome.Applied;
			Result.CommittedRevision = Ledger.Revision; return true;
		}

		public static void NormalizeRealmTransition(KingdomPolityRealmTransition Transition)
		{
			if (Transition == null) return;
			Transition.Normalize();
			if (Transition.Phase == KingdomPolityRealmTransitionPhase.None ||
				Transition.Phase == KingdomPolityRealmTransitionPhase.Quarantined) return;
			if (TryValidateRealmTransition(Transition, out string failure)) return;
			Transition.Phase = KingdomPolityRealmTransitionPhase.Quarantined;
			Transition.Fault = Text(failure, true) ? failure :
				"Realm polity transition authority is invalid.";
		}

		private static KingdomPolityRealmTransition CloneTransition(
			KingdomPolityRealmTransition Source)
		{
			KingdomPolityRealmTransition result = new KingdomPolityRealmTransition();
			result.CopyFrom(Source); return result;
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
