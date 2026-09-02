using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Realm-transition CAS ladder: prepare exile, tombstone, detach, then exact
	/// return or refound, completion, and quarantine normalization.</summary>
	public static partial class KingdomPolityRules
	{
		/// <summary>
		/// One CAS retires current polity and causally ends imported polity. Runtime factions are
		/// hidden only after this durable intent exists; rollback bytes are never import facts.
		/// </summary>
		public static bool TryPrepareRealmExile(KingdomPolityLedger Ledger,
			long ExpectedRevision, KingdomPolityRealmExileFacts Facts,
			out KingdomPolityRealmTransition Transition,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Transition = null; Result = BeginResult(Ledger); Failure = null;
			if (!TryValidate(Ledger, out Failure) || Facts == null || Facts.ClosedTick <= 0L ||
				Facts.RealmId != Ledger?.RealmId || Facts.FactionId != Facts.RealmId ||
				!TypedId(Facts.RealmId, "taf:realm:") ||
				!KingdomPolityProfileRules.ValidLegacy(Facts.Legacy, out Failure) ||
				Facts.Legacy.LegacyToken == Facts.RealmId || Facts.Legacy.LineageToken == Facts.RealmId)
				return Refuse(Result, Failure ?? "realm exile facts are invalid", out Failure);
			KingdomPolityRecord current = FindPolity(Ledger, Facts.RealmId);
			KingdomPolityProfileRevision currentProfile = current == null ? null :
				FindProfile(Ledger, current.ProfileId, current.ProfileRevision);
			KingdomPolityProjectionReceipt currentProjection = current == null ? null :
				FactionReceipt(Ledger, current.PolityId);
			if (current == null || currentProfile == null || currentProjection == null ||
				current.Source != KingdomPolitySource.CurrentRealm ||
				current.Lifecycle != KingdomPolityLifecycle.Active ||
				current.ProjectedFactionId != Facts.FactionId ||
				currentProjection.Phase != KingdomPolityProjectionPhase.Committed ||
				!ExactFoundationReceipt(currentProjection, FoundationProjection(current.PolityId,
					current.ProjectedFactionId, ProfileExpressionDigest(currentProfile),
					currentProjection.PreparedTick, true)))
				return Refuse(Result, "current polity has no exact active foundation", out Failure);
			if (Facts.Legacy.ProfileSchema !=
				KingdomPolityProfileRules.CurrentLegacyProfileSchema ||
				!KingdomPolityProfileRules.MatchesLegacyProfileSource(Facts.Legacy, currentProfile))
				return Refuse(Result, "realm exile seal lacks exact current profile provenance",
					out Failure);
			KingdomPolityRecord imported = ImportedPolity(Ledger);
			KingdomPolityProjectionReceipt importedProjection = imported == null ? null :
				FactionReceipt(Ledger, imported.PolityId);
			if (imported != null && importedProjection != null &&
				(importedProjection.Phase != KingdomPolityProjectionPhase.Committed ||
				 !ValidImportedFactionReceipt(Ledger, importedProjection, imported)) ||
				imported != null && importedProjection == null &&
				imported.Lifecycle != KingdomPolityLifecycle.Latent)
				return Refuse(Result, "imported polity projection is torn", out Failure);
			if (Ledger.Revision != ExpectedRevision) return Conflict(Result, out Failure);
			byte[] rollback;
			try { rollback = KingdomPolityCodec.EncodeEnvelope(Ledger); }
			catch (Exception ex) { return Refuse(Result, ex.Message, out Failure); }
			KingdomPolityLedger candidate = Clone(Ledger);
			KingdomPolityRecord retiredCurrent = FindPolity(candidate, current.PolityId);
			retiredCurrent.Lifecycle = KingdomPolityLifecycle.Ended;
			retiredCurrent.EndedTick = Facts.ClosedTick;
			if (imported != null && imported.Lifecycle != KingdomPolityLifecycle.Ended)
			{
				KingdomPolityRecord retiredImported = FindPolity(candidate, imported.PolityId);
				retiredImported.Lifecycle = KingdomPolityLifecycle.Ended;
				retiredImported.EndedTick = Facts.ClosedTick;
			}
			if (!Increment(candidate, out Failure) || !TryValidate(candidate, out Failure))
				return Refuse(Result, Failure, out Failure);
			string rollbackDigest = RealmLedgerDigest(rollback);
			string retiredDigest;
			try { retiredDigest = RealmLedgerDigest(KingdomPolityCodec.EncodeEnvelope(candidate)); }
			catch (Exception ex) { return Refuse(Result, ex.Message, out Failure); }
			string transitionId = ActivationId("taf:polity-transition:exile:v1:",
				"polity-realm-exile-transition-v1", Facts.RealmId,
				Ledger.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
				Facts.ClosedTick.ToString(System.Globalization.CultureInfo.InvariantCulture),
				rollbackDigest);
			Transition = new KingdomPolityRealmTransition
			{
				Phase = KingdomPolityRealmTransitionPhase.Prepared, Revision = 1L,
				TransitionId = transitionId, CauseRef = ActivationId(
					"taf:fact:polity-exile:v1:", "polity-realm-exile-cause-v1",
					transitionId, retiredDigest), OldRealmId = Facts.RealmId,
				OldCurrentPolityId = current.PolityId, OldCurrentFactionId = current.ProjectedFactionId,
				OldCurrentProjectionId = currentProjection.ProjectionId,
				OldCurrentProjectionDigest = currentProjection.AppliedDigest,
				OldImportedPolityId = imported?.PolityId,
				OldImportedFactionId = imported?.ProjectedFactionId,
				OldImportedProjectionId = importedProjection?.ProjectionId,
				OldImportedProjectionDigest = importedProjection?.AppliedDigest,
				OldImportedWasVisible = imported?.Lifecycle == KingdomPolityLifecycle.Active,
				ClosedTick = Facts.ClosedTick, SourceRevision = Ledger.Revision,
				RetiredRevision = candidate.Revision, ReturnLedgerDigest = rollbackDigest,
				RetiredLedgerDigest = retiredDigest, ReturnLedgerEnvelope = rollback,
				Legacy = CopyLegacy(Facts.Legacy)
			};
			if (!TryValidateRealmTransition(Transition, out Failure))
				return Refuse(Result, Failure, out Failure);
			Result.CurrentPolityId = current.PolityId;
			Result.ImportedPolityId = imported?.PolityId; Commit(Ledger, candidate, Result); return true;
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

		private static KingdomPolityLegacySnapshot CopyLegacy(KingdomPolityLegacySnapshot S)
		{
			return S?.Copy();
		}
	}
}
