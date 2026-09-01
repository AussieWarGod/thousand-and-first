using System;

namespace ThousandAndFirst
{
	public static partial class KingdomFounderHistoryRules
	{
		/// <summary>Migrates one exact schema-1 owner into schema 2 without guessing its external
		/// state. Any phase that could have inserted a vanilla object retains all old ids and becomes
		/// a required-cleanup receipt. No global state is touched here.</summary>
		public static bool TryMigrateV1(KingdomFounderHistoryReceipt Receipt,
			out string Failure)
		{
			Failure = "";
			if (!ValidateV1(Receipt, out Failure)) return false;
			KingdomFounderHistoryPhase oldPhase = Receipt.Phase;
			string digest = Digest(Receipt.RealmId, Receipt.DeathToken);
			Receipt.Version = CurrentVersion;
			if (oldPhase == KingdomFounderHistoryPhase.None)
			{
				Receipt.Normalize();
				return true;
			}
			Receipt.ProjectionId = ProjectionPrefix + digest;
			Receipt.ProjectionProofId = ProjectionProofPrefix + digest;
			bool mayHaveEnteredVanilla = oldPhase == KingdomFounderHistoryPhase.EntityPublished
				|| oldPhase == KingdomFounderHistoryPhase.EventPublished
				|| oldPhase == KingdomFounderHistoryPhase.NotePublished
				|| oldPhase == KingdomFounderHistoryPhase.Committed
				|| oldPhase == KingdomFounderHistoryPhase.Quarantined;
			if (mayHaveEnteredVanilla)
			{
				Receipt.LegacyCleanupState = KingdomFounderHistoryLegacyCleanupState.Required;
				Receipt.LegacyPhase = oldPhase;
				Receipt.Phase = KingdomFounderHistoryPhase.Prepared;
				Receipt.PublicationEnabled = true;
				Receipt.CommittedTick = 0L;
				Receipt.Fault = "";
			}
			else
			{
				Receipt.LegacyCleanupState = KingdomFounderHistoryLegacyCleanupState.None;
				Receipt.LegacyPhase = KingdomFounderHistoryPhase.None;
				Receipt.EntityId = Receipt.NoteId = Receipt.ProofId = "";
				Receipt.EventId = 0L;
			}
			return Validate(Receipt, out Failure);
		}

		private static bool LegacyEvidenceValid(KingdomFounderHistoryReceipt Receipt,
			string DigestValue)
		{
			if (Receipt.LegacyCleanupState == KingdomFounderHistoryLegacyCleanupState.None)
				return Receipt.LegacyPhase == KingdomFounderHistoryPhase.None
					&& string.IsNullOrEmpty(Receipt.EntityId)
					&& string.IsNullOrEmpty(Receipt.NoteId)
					&& string.IsNullOrEmpty(Receipt.ProofId) && Receipt.EventId == 0L;
			if (Receipt.LegacyCleanupState != KingdomFounderHistoryLegacyCleanupState.Required
				&& Receipt.LegacyCleanupState != KingdomFounderHistoryLegacyCleanupState.Complete)
				return false;
			if (Receipt.LegacyPhase != KingdomFounderHistoryPhase.EntityPublished
				&& Receipt.LegacyPhase != KingdomFounderHistoryPhase.EventPublished
				&& Receipt.LegacyPhase != KingdomFounderHistoryPhase.NotePublished
				&& Receipt.LegacyPhase != KingdomFounderHistoryPhase.Committed
				&& Receipt.LegacyPhase != KingdomFounderHistoryPhase.Quarantined) return false;
			if (Receipt.EntityId != LegacyEntityPrefix + DigestValue
				|| Receipt.NoteId != LegacyNotePrefix + DigestValue
				|| Receipt.ProofId != LegacyProofPrefix + DigestValue) return false;
			if (Receipt.LegacyPhase == KingdomFounderHistoryPhase.EntityPublished)
				return Receipt.EventId == 0L;
			if (Receipt.LegacyPhase == KingdomFounderHistoryPhase.EventPublished
				|| Receipt.LegacyPhase == KingdomFounderHistoryPhase.NotePublished
				|| Receipt.LegacyPhase == KingdomFounderHistoryPhase.Committed)
				return Receipt.EventId > 0L;
			return Receipt.EventId >= 0L;
		}

		private static bool ValidateV1(KingdomFounderHistoryReceipt Receipt,
			out string Failure)
		{
			Failure = "";
			if (Receipt == null || Receipt.Version != 1
				|| !Enum.IsDefined(typeof(KingdomFounderHistoryPhase), Receipt.Phase))
				return Fail("unknown schema-1 founder-memory version or phase", out Failure);
			if (Receipt.Phase == KingdomFounderHistoryPhase.None)
				return EmptyV1(Receipt)
					|| Fail("idle schema-1 founder-memory receipt carries residue", out Failure);
			if (!Bounded(Receipt.RealmId, MaxIdentityChars)
				|| !Bounded(Receipt.DeathToken, MaxIdentityChars)
				|| !Bounded(Receipt.FounderName, MaxNameChars)
				|| !Bounded(Receipt.CityName, MaxNameChars)
				|| !Bounded(Receipt.RegionName, MaxNameChars)
				|| !Bounded(Receipt.Cause, MaxCauseChars)
				|| !Bounded(Receipt.Gospel, MaxGospelChars)
				|| !Bounded(Receipt.EntityId, MaxIdentityChars)
				|| !Bounded(Receipt.NoteId, MaxIdentityChars)
				|| !Bounded(Receipt.ProofId, MaxIdentityChars)
				|| Receipt.DeathTick < 0L || Receipt.PreparedTick < Receipt.DeathTick
				|| Receipt.HistoricYear == long.MinValue)
				return Fail("schema-1 founder-memory receipt has malformed evidence", out Failure);
			string digest = Digest(Receipt.RealmId, Receipt.DeathToken);
			if (string.IsNullOrEmpty(digest)
				|| Receipt.EntityId != LegacyEntityPrefix + digest
				|| Receipt.NoteId != LegacyNotePrefix + digest
				|| Receipt.ProofId != LegacyProofPrefix + digest
				|| Receipt.Gospel != Gospel(Receipt.FounderName, Receipt.CityName,
					Receipt.RegionName, Receipt.Cause))
				return Fail("schema-1 founder-memory identity or telling diverged", out Failure);
			if (Receipt.Phase == KingdomFounderHistoryPhase.Suppressed)
				return !Receipt.PublicationEnabled && Receipt.EventId == 0L
					&& Receipt.CommittedTick >= Receipt.PreparedTick
					&& string.IsNullOrEmpty(Receipt.Fault)
					|| Fail("suppressed schema-1 receipt carries publication residue", out Failure);
			if (Receipt.Phase == KingdomFounderHistoryPhase.Quarantined)
				return Receipt.PublicationEnabled && Receipt.CommittedTick == 0L
					&& Bounded(Receipt.Fault, MaxFaultChars)
					|| Fail("quarantined schema-1 receipt lacks a bounded fault", out Failure);
			if (!Receipt.PublicationEnabled || !string.IsNullOrEmpty(Receipt.Fault))
				return Fail("active schema-1 receipt has inconsistent state", out Failure);
			bool eventPhase = Receipt.Phase >= KingdomFounderHistoryPhase.EventPublished;
			if (eventPhase != (Receipt.EventId > 0L))
				return Fail("schema-1 event identity disagrees with its phase", out Failure);
			if (Receipt.Phase == KingdomFounderHistoryPhase.Committed)
				return Receipt.CommittedTick >= Receipt.PreparedTick
					|| Fail("committed schema-1 receipt lacks its tick", out Failure);
			return Receipt.CommittedTick == 0L
				|| Fail("open schema-1 receipt carries a terminal tick", out Failure);
		}

		private static bool CanonicalQuarantine(KingdomFounderHistoryReceipt Receipt)
		{
			return Receipt.PublicationEnabled && Receipt.CommittedTick == 0L
				&& Bounded(Receipt.Fault, MaxFaultChars)
				&& Receipt.LegacyCleanupState == KingdomFounderHistoryLegacyCleanupState.None
				&& Receipt.LegacyPhase == KingdomFounderHistoryPhase.None
				&& string.IsNullOrEmpty(Receipt.RealmId)
				&& string.IsNullOrEmpty(Receipt.DeathToken)
				&& string.IsNullOrEmpty(Receipt.FounderName)
				&& string.IsNullOrEmpty(Receipt.CityName)
				&& string.IsNullOrEmpty(Receipt.RegionName)
				&& string.IsNullOrEmpty(Receipt.Cause)
				&& string.IsNullOrEmpty(Receipt.Gospel)
				&& string.IsNullOrEmpty(Receipt.ProjectionId)
				&& string.IsNullOrEmpty(Receipt.ProjectionProofId)
				&& string.IsNullOrEmpty(Receipt.EntityId)
				&& string.IsNullOrEmpty(Receipt.NoteId)
				&& string.IsNullOrEmpty(Receipt.ProofId)
				&& Receipt.EventId == 0L;
		}

		private static bool OwnedQuarantine(KingdomFounderHistoryReceipt Receipt)
		{
			if (!Receipt.PublicationEnabled || Receipt.CommittedTick != 0L
				|| !Bounded(Receipt.Fault, MaxFaultChars)
				|| !Bounded(Receipt.RealmId, MaxIdentityChars)
				|| !Bounded(Receipt.DeathToken, MaxIdentityChars)
				|| !Bounded(Receipt.FounderName, MaxNameChars)
				|| !Bounded(Receipt.CityName, MaxNameChars)
				|| !Bounded(Receipt.RegionName, MaxNameChars)
				|| !Bounded(Receipt.Cause, MaxCauseChars)
				|| !Bounded(Receipt.Gospel, MaxGospelChars)
				|| !Bounded(Receipt.ProjectionId, MaxIdentityChars)
				|| !Bounded(Receipt.ProjectionProofId, MaxIdentityChars)
				|| Receipt.DeathTick < 0L || Receipt.PreparedTick < Receipt.DeathTick
				|| Receipt.HistoricYear == long.MinValue
				|| !Enum.IsDefined(typeof(KingdomFounderHistoryLegacyCleanupState),
					Receipt.LegacyCleanupState)
				|| !Enum.IsDefined(typeof(KingdomFounderHistoryPhase), Receipt.LegacyPhase))
				return false;
			string digest = Digest(Receipt.RealmId, Receipt.DeathToken);
			return !string.IsNullOrEmpty(digest)
				&& Receipt.ProjectionId == ProjectionPrefix + digest
				&& Receipt.ProjectionProofId == ProjectionProofPrefix + digest
				&& Receipt.Gospel == Gospel(Receipt.FounderName, Receipt.CityName,
					Receipt.RegionName, Receipt.Cause)
				&& LegacyEvidenceValid(Receipt, digest);
		}

		private static bool EmptyV1(KingdomFounderHistoryReceipt R)
		{
			return !R.PublicationEnabled && string.IsNullOrEmpty(R.RealmId)
				&& string.IsNullOrEmpty(R.DeathToken) && R.DeathTick == 0L
				&& R.PreparedTick == 0L && R.HistoricYear == long.MinValue
				&& R.CommittedTick == 0L && string.IsNullOrEmpty(R.FounderName)
				&& string.IsNullOrEmpty(R.CityName) && string.IsNullOrEmpty(R.RegionName)
				&& string.IsNullOrEmpty(R.Cause) && string.IsNullOrEmpty(R.Gospel)
				&& string.IsNullOrEmpty(R.ProjectionId)
				&& string.IsNullOrEmpty(R.ProjectionProofId)
				&& R.LegacyCleanupState == KingdomFounderHistoryLegacyCleanupState.None
				&& R.LegacyPhase == KingdomFounderHistoryPhase.None
				&& string.IsNullOrEmpty(R.EntityId) && string.IsNullOrEmpty(R.NoteId)
				&& string.IsNullOrEmpty(R.ProofId) && R.EventId == 0L
				&& string.IsNullOrEmpty(R.Fault);
		}
	}
}
