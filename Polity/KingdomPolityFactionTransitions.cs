using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		public static bool TryPrepareLegacyFaction(KingdomPolityLedger Ledger,
			long ExpectedRevision, long Tick, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = BeginResult(Ledger); Failure = null;
			if (!TryValidate(Ledger, out Failure) || Tick < 0L)
				return Refuse(Result, Failure ?? "faction preparation tick is invalid", out Failure);
			KingdomPolityRecord polity = ImportedPolity(Ledger);
			KingdomPolityProfileRevision profile = polity == null ? null :
				FindProfile(Ledger, polity.ProfileId, polity.ProfileRevision);
			if (polity == null || profile == null ||
				!SemanticId(polity.ProjectedFactionId))
				return Refuse(Result, "one imported polity is required", out Failure);
			KingdomPolityProjectionReceipt expected = FoundationProjection(polity.PolityId,
				polity.ProjectedFactionId, ProfileExpressionDigest(profile), Tick, false);
			Result.ImportedPolityId = polity.PolityId; Result.ProjectionId = expected.ProjectionId;
			KingdomPolityProjectionReceipt existing = FindProjection(Ledger, expected.ProjectionId);
			if (existing != null)
			{
				if (!SameFactionReceipt(existing, expected))
					return Refuse(Result, "faction projection id carries foreign evidence", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (polity.Lifecycle != KingdomPolityLifecycle.Latent)
				return Refuse(Result, "new faction preparation requires a latent polity", out Failure);
			if (Ledger.Revision != ExpectedRevision) return Conflict(Result, out Failure);
			KingdomPolityLedger candidate = Clone(Ledger); candidate.Projections.Add(expected);
			CanonicalSort(candidate);
			if (!Increment(candidate, out Failure) || !TryValidate(candidate, out Failure))
				return Refuse(Result, Failure, out Failure);
			Commit(Ledger, candidate, Result); return true;
		}

		public static bool TryCommitLegacyFaction(KingdomPolityLedger Ledger,
			long ExpectedRevision, string ProjectionId, long Tick,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = BeginResult(Ledger); Failure = null; Result.ProjectionId = ProjectionId;
			if (!TryValidate(Ledger, out Failure) || !TypedId(ProjectionId, "taf:projection:faction:") ||
				Tick < 0L) return Refuse(Result, Failure ?? "faction commit input is invalid", out Failure);
			KingdomPolityProjectionReceipt receipt = FindProjection(Ledger, ProjectionId);
			KingdomPolityRecord polity = receipt == null ? null : FindPolity(Ledger, receipt.SourceRef);
			if (!ValidImportedFactionReceipt(Ledger, receipt, polity))
				return Refuse(Result, "prepared imported faction receipt is not exact", out Failure);
			Result.ImportedPolityId = polity.PolityId;
			if (receipt.Phase == KingdomPolityProjectionPhase.Committed &&
				polity.Lifecycle == KingdomPolityLifecycle.Active)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (receipt.Phase != KingdomPolityProjectionPhase.Prepared ||
				polity.Lifecycle != KingdomPolityLifecycle.Latent || Tick < receipt.PreparedTick)
				return Refuse(Result, "faction projection cannot commit from this phase", out Failure);
			if (Ledger.Revision != ExpectedRevision) return Conflict(Result, out Failure);
			KingdomPolityLedger candidate = Clone(Ledger);
			FindProjection(candidate, ProjectionId).Phase = KingdomPolityProjectionPhase.Committed;
			FindProjection(candidate, ProjectionId).CommittedTick = Tick;
			FindPolity(candidate, polity.PolityId).Lifecycle = KingdomPolityLifecycle.Active;
			if (!Increment(candidate, out Failure) || !TryValidate(candidate, out Failure))
				return Refuse(Result, Failure, out Failure);
			Commit(Ledger, candidate, Result); return true;
		}

		public static bool TryPrepareLegacyFactionTombstone(KingdomPolityLedger Ledger,
			long ExpectedRevision, long Tick, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = BeginResult(Ledger); Failure = null;
			if (!TryValidate(Ledger, out Failure) || Tick < 0L)
				return Refuse(Result, Failure ?? "tombstone tick is invalid", out Failure);
			KingdomPolityRecord polity = ImportedPolity(Ledger);
			KingdomPolityProjectionReceipt faction = polity == null ? null :
				CommittedFactionReceipt(Ledger, polity.PolityId);
			if (polity == null || faction == null)
				return Refuse(Result, "an owned faction is required for tombstone", out Failure);
			string id = ActivationId("taf:projection:faction-tombstone:v1:",
				"polity-faction-tombstone-v1", polity.PolityId, polity.ProjectedFactionId,
				faction.AppliedDigest);
			Result.ImportedPolityId = polity.PolityId; Result.ProjectionId = id;
			KingdomPolityProjectionReceipt existing = FindProjection(Ledger, id);
			if (existing != null)
			{
				if (!ValidTombstone(existing, polity, faction))
					return Refuse(Result, "tombstone id carries foreign evidence", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (polity.Lifecycle != KingdomPolityLifecycle.Active)
				return Refuse(Result, "new tombstone preparation requires an active polity", out Failure);
			if (Ledger.Revision != ExpectedRevision) return Conflict(Result, out Failure);
			KingdomPolityLedger candidate = Clone(Ledger);
			candidate.Projections.Add(new KingdomPolityProjectionReceipt
			{
				ProjectionId = id, Kind = KingdomPolityProjectionKind.FactionTombstone,
				SourceRef = polity.PolityId, Phase = KingdomPolityProjectionPhase.Prepared,
				ObjectIds = new List<string> { polity.ProjectedFactionId },
				PriorDigest = faction.AppliedDigest,
				AppliedDigest = ActivationDigest("polity-faction-hidden-v1", polity.PolityId,
					polity.ProjectedFactionId, faction.AppliedDigest), PreparedTick = Tick
			});
			CanonicalSort(candidate);
			if (!Increment(candidate, out Failure) || !TryValidate(candidate, out Failure))
				return Refuse(Result, Failure, out Failure);
			Commit(Ledger, candidate, Result); return true;
		}

		public static bool TryCommitLegacyFactionTombstone(KingdomPolityLedger Ledger,
			long ExpectedRevision, string ProjectionId, long Tick,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = BeginResult(Ledger); Failure = null; Result.ProjectionId = ProjectionId;
			if (!TryValidate(Ledger, out Failure) || Tick < 0L)
				return Refuse(Result, Failure ?? "tombstone commit input is invalid", out Failure);
			KingdomPolityProjectionReceipt receipt = FindProjection(Ledger, ProjectionId);
			KingdomPolityRecord polity = receipt == null ? null : FindPolity(Ledger, receipt.SourceRef);
			KingdomPolityProjectionReceipt faction = polity == null ? null :
				CommittedFactionReceipt(Ledger, polity.PolityId);
			if (!ValidTombstone(receipt, polity, faction))
				return Refuse(Result, "prepared tombstone receipt is not exact", out Failure);
			Result.ImportedPolityId = polity.PolityId;
			if (receipt.Phase == KingdomPolityProjectionPhase.Committed &&
				polity.Lifecycle == KingdomPolityLifecycle.Dormant)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (receipt.Phase != KingdomPolityProjectionPhase.Prepared ||
				polity.Lifecycle != KingdomPolityLifecycle.Active || Tick < receipt.PreparedTick)
				return Refuse(Result, "tombstone cannot commit from this phase", out Failure);
			if (Ledger.Revision != ExpectedRevision) return Conflict(Result, out Failure);
			KingdomPolityLedger candidate = Clone(Ledger);
			FindProjection(candidate, ProjectionId).Phase = KingdomPolityProjectionPhase.Committed;
			FindProjection(candidate, ProjectionId).CommittedTick = Tick;
			FindPolity(candidate, polity.PolityId).Lifecycle = KingdomPolityLifecycle.Dormant;
			if (!Increment(candidate, out Failure) || !TryValidate(candidate, out Failure))
				return Refuse(Result, Failure, out Failure);
			Commit(Ledger, candidate, Result); return true;
		}

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

		private static KingdomPolityLegacySnapshot CopyLegacy(KingdomPolityLegacySnapshot S)
		{
			return S?.Copy();
		}
	}
}
