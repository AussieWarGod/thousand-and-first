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
	}
}
