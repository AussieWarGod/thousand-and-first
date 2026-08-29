using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private void MigrateDirectionalStandingStateAfterLoad()
		{
			if (LoadedSerializationVersion != 8) return;
			if (Founded && !TryMigrateCurrentDirectionalStanding(out string currentFailure))
				QuarantineIdentity("version-8 directional migration refused: " + currentFailure);
			KingdomRealmArchive archive = ExiledRealmArchive;
			if (archive != null && archive.RequiresDirectionalStandingMigration &&
				!TryMigrateArchivedDirectionalStanding(archive, out string archiveFailure))
				archive.Quarantine("version-8 directional migration refused: " + archiveFailure);
		}

		private bool TryMigrateCurrentDirectionalStanding(out string failure)
		{
			failure = null;
			Faction realm = Factions.GetIfExists(KingdomFactionName);
			KingdomPolityLedger sourceLedger = null;
			bool requireActiveFaction = true;
			if (DirectionalStandingSchemaVersion != 0 || RealmPolicyToward == null ||
				RealmPolicyToward.Count != 0 || RegardSpilloverRemainders == null ||
				RegardSpilloverRemainders.Count != 0 ||
				!KingdomStandingRules.CanonicalPairs(Standings,
					RegardSpilloverRemainders) ||
				RegardSpilloverObservedReputation == null ||
				RegardSpilloverObservedReputation.Count != 0 ||
				!CurrentRelationshipAuthorityHealthy() || realm == null ||
				!KingdomFoundingTransaction.FactionRegistryCoherent(KingdomFactionName, realm) ||
				!KingdomFounding.DirectionalAuthorityPublished(realm) ||
				!TrySelectCurrentMigrationSource(out sourceLedger,
					out requireActiveFaction, out failure) ||
				!MigrationFactionObserved(realm, requireActiveFaction,
					ExiledRealmArchive?.Phase ?? KingdomRealmArchivePhase.None, out failure) ||
				!TryReadLegacyOutboundPolicy(realm, Standings, sourceLedger,
					out Dictionary<string, int> policy, out failure))
			{
				failure = failure ?? "current realm provenance or empty migration roots differ";
				return false;
			}
			RealmPolicyToward = policy;
			DirectionalStandingSchemaVersion = 1;
			return true;
		}

		private bool TryMigrateArchivedDirectionalStanding(KingdomRealmArchive archive,
			out string failure)
		{
			failure = null;
			Faction realm = archive == null ? null : Factions.GetIfExists(archive.FactionName);
			KingdomPolityLedger sourceLedger = null;
			bool requireActiveFaction = true;
			if (archive == null || archive.Quarantined || archive.DirectionalStandingSchemaVersion != 0 ||
				archive.CallbackAuthoritySchemaVersion != 1 || archive.RealmPolicyToward == null ||
				archive.RealmPolicyToward.Count != 0 || archive.RegardSpilloverRemainders == null ||
				archive.RegardSpilloverRemainders.Count != 0 ||
				!KingdomStandingRules.CanonicalPairs(archive.Standings,
					archive.RegardSpilloverRemainders) ||
				archive.RegardSpilloverObservedReputation == null ||
				archive.RegardSpilloverObservedReputation.Count != 0 ||
				ExiledRealmPolicyToward == null || ExiledRealmPolicyToward.Count != 0 ||
				ExiledRegardSpilloverRemainders == null ||
				ExiledRegardSpilloverRemainders.Count != 0 ||
				ExiledRegardSpilloverObservedReputation == null ||
				ExiledRegardSpilloverObservedReputation.Count != 0 || realm == null ||
				!KingdomIdentityRules.ReproveRealm(archive.RealmId,
					archive.RealmIdentityVersion, archive.RealmIdentityOrigin,
					archive.RealmIdentityTransactionId, archive.RealmIdentityLegacyFaction,
					archive.RealmIdentityFoundedTick, archive.RealmIdentitySeedHigh,
				archive.RealmIdentitySeedLow, archive.RealmIdentityFirstClaimedZone,
					out KingdomIdentityFault _) ||
				!KingdomFoundingTransaction.FactionRegistryCoherent(archive.FactionName, realm) ||
				!KingdomFounding.DirectionalAuthorityPublished(realm) ||
				!TrySelectArchivedMigrationSource(archive, out sourceLedger,
					out requireActiveFaction, out failure) ||
				!MigrationFactionObserved(realm, requireActiveFaction,
					archive.Phase, out failure) ||
				!TryReadLegacyOutboundPolicy(realm, archive.Standings, sourceLedger,
					out Dictionary<string, int> policy, out failure))
			{
				failure = failure ?? "archived realm provenance or empty migration roots differ";
				return false;
			}
			if (!KingdomRealmArchive.TryDirectionalStandingDigest(archive.FactionName,
				policy, archive.RegardSpilloverRemainders,
				archive.RegardSpilloverObservedReputation, out string digest, out failure))
				return false;
			archive.RealmPolicyToward = policy;
			archive.DirectionalStandingSchemaVersion = 1;
			archive.DirectionalStandingDigest = digest;
			bool cleaning = archive.Phase == KingdomRealmArchivePhase.ReturnCleaning;
			if (!cleaning && !TryEnsureExileMirrors(archive,
				AllowCanonicalMissing: archive.Phase == KingdomRealmArchivePhase.TradeClosed,
				AllowDirectionalMissing: true, out failure)) return false;
			if (!archive.Validate(out failure) ||
				(cleaning && !archive.CurrentGraphMatches(this, out failure)) ||
				(!cleaning && !ExactExileMirrors(archive))) return false;
			archive.RequiresDirectionalStandingMigration = false;
			return true;
		}

		private bool TrySelectCurrentMigrationSource(out KingdomPolityLedger source,
			out bool requireActiveFaction, out string failure)
		{
			source = null; requireActiveFaction = true; failure = null;
			if (KingdomPolityRules.TryObserveCurrentFoundation(PolityLedger, RealmId,
				KingdomFactionName, out string _)) { source = PolityLedger; return true; }
			KingdomRealmArchive archive = ExiledRealmArchive;
			KingdomPolityRealmTransition transition = PolityTransition;
			KingdomPolityRealmTransitionPhase phase = transition == null
				? KingdomPolityRealmTransitionPhase.None : transition.Phase;
			if (archive == null ||
				archive.RealmId != RealmId || archive.FactionName != KingdomFactionName ||
				!ArchiveTransitionPairAdmitted(archive.Phase, phase) ||
				phase == KingdomPolityRealmTransitionPhase.None ||
				phase == KingdomPolityRealmTransitionPhase.Rebound ||
				!TransitionMatchesArchive(transition, archive) ||
				!KingdomPolityRules.TryTransitionLedger(transition, out source) ||
				ReferenceEquals(archive.Standings, Standings) ||
				!KingdomRealmArchive.ExactDictionary(archive.Standings, Standings))
				return RefuseMigration("current transition cut lacks exact source polity authority",
					out failure);
			requireActiveFaction = false;
			return true;
		}

		private bool TrySelectArchivedMigrationSource(KingdomRealmArchive archive,
			out KingdomPolityLedger source, out bool requireActiveFaction, out string failure)
		{
			source = null; requireActiveFaction = true; failure = null;
			KingdomPolityRealmTransition transition = PolityTransition;
			KingdomPolityRealmTransitionPhase phase = transition == null
				? KingdomPolityRealmTransitionPhase.None : transition.Phase;
			if (!ArchiveTransitionPairAdmitted(archive.Phase, phase))
				return RefuseMigration("archive and polity transition phases differ", out failure);
			if (phase == KingdomPolityRealmTransitionPhase.Rebound)
				return RefuseMigration("refounding destroyed the old source polity envelope",
					out failure);
			if (phase == KingdomPolityRealmTransitionPhase.None)
			{
				if (!KingdomPolityRules.TryObserveCurrentFoundation(PolityLedger,
					archive.RealmId, archive.FactionName, out failure)) return false;
				source = PolityLedger;
				return true;
			}
			if (!TransitionMatchesArchive(transition, archive) ||
				!KingdomPolityRules.TryTransitionLedger(transition, out source))
				return RefuseMigration("archive lacks its exact source polity envelope", out failure);
			requireActiveFaction = false;
			return true;
		}

		private static bool TransitionMatchesArchive(KingdomPolityRealmTransition transition,
			KingdomRealmArchive archive)
		{
			return transition != null && archive != null &&
				KingdomPolityRules.TryValidateRealmTransition(transition, out string _) &&
				transition.OldRealmId == archive.RealmId &&
				transition.OldCurrentFactionId == archive.FactionName &&
				transition.ClosedTick == archive.ClosedTick;
		}

		private static bool ArchiveTransitionPairAdmitted(KingdomRealmArchivePhase archive,
			KingdomPolityRealmTransitionPhase transition)
		{
			if (archive == KingdomRealmArchivePhase.TradeClosed ||
				archive == KingdomRealmArchivePhase.MirrorsPublished ||
				archive == KingdomRealmArchivePhase.ChronicleFrozen ||
				archive == KingdomRealmArchivePhase.ChronicleCleared)
				return transition == KingdomPolityRealmTransitionPhase.None;
			if (archive == KingdomRealmArchivePhase.Resetting)
				return transition == KingdomPolityRealmTransitionPhase.None ||
					transition == KingdomPolityRealmTransitionPhase.Prepared ||
					transition == KingdomPolityRealmTransitionPhase.Tombstoned ||
					transition == KingdomPolityRealmTransitionPhase.Detached;
			if (archive == KingdomRealmArchivePhase.Closed)
				return transition == KingdomPolityRealmTransitionPhase.Detached ||
					transition == KingdomPolityRealmTransitionPhase.Rebound;
			if (archive == KingdomRealmArchivePhase.Restoring)
				return transition == KingdomPolityRealmTransitionPhase.Detached ||
					transition == KingdomPolityRealmTransitionPhase.Restored;
			if (archive == KingdomRealmArchivePhase.Restored)
				return transition == KingdomPolityRealmTransitionPhase.Restored;
			return archive == KingdomRealmArchivePhase.ReturnCleaning &&
				(transition == KingdomPolityRealmTransitionPhase.Restored ||
				 transition == KingdomPolityRealmTransitionPhase.None);
		}

		private bool MigrationFactionObserved(Faction realm, bool requireActive,
			KingdomRealmArchivePhase archivePhase, out string failure)
		{
			failure = null;
			if (realm == null || realm.GetIntProperty("Village") != 1 ||
				realm.GetIntProperty("TAFFoundingPending") != 0)
				return RefuseMigration("realm faction lacks founding provenance", out failure);
			if (requireActive)
			{
				if (!realm.Visible || realm.GetIntProperty("PlayerKingdom") != 1 ||
					realm.WaterRitualLiquid != "water")
					return RefuseMigration("realm faction is not the active civic endpoint",
						out failure);
				KingdomPolityRealmTransition transition = PolityTransition;
				if (transition == null || transition.Phase == KingdomPolityRealmTransitionPhase.None ||
					transition.OldCurrentFactionId != realm.Name) return true;
				return KingdomPolityFactionRuntime.OldCurrentFactionObserved(transition, realm,
					archivePhase == KingdomRealmArchivePhase.Restoring, out failure);
			}
			return KingdomPolityFactionRuntime.OldCurrentFactionObserved(
				PolityTransition, realm,
				archivePhase == KingdomRealmArchivePhase.Restoring, out failure);
		}

		private static bool TryReadLegacyOutboundPolicy(Faction realm,
			Dictionary<string, int> regard, KingdomPolityLedger sourceLedger,
			out Dictionary<string, int> policy,
			out string failure)
		{
			policy = null;
			failure = null;
			if (realm?.FactionFeeling == null || regard == null ||
				regard.Count > KingdomStandingRules.MaxRelationships ||
				realm.FactionFeeling.Count > 4096 || !KingdomPolityRules.Usable(sourceLedger))
				return RefuseMigration("legacy faction or polity authority is unavailable", out failure);
			Dictionary<string, int> desired =
				new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (KeyValuePair<string, int> row in regard)
			{
				if (!KingdomStandingRules.EligibleForeignFaction(row.Key, realm.Name) ||
					Factions.GetIfExists(row.Key) == null ||
					IsOtherPolityEndpoint(row.Key, realm.Name, sourceLedger))
					return RefuseMigration("legacy regard names a reserved or absent endpoint", out failure);
				if (realm.FactionFeeling.TryGetValue(row.Key, out int feeling))
				{
					if (!KingdomStandingRules.TryLegacyFeelingPolicy(feeling, out int value))
						return RefuseMigration("legacy outbound feeling is noncanonical", out failure);
					desired.Add(row.Key, value);
				}
			}
			foreach (KeyValuePair<string, int> row in realm.FactionFeeling)
				if (row.Key != "Player" && !regard.ContainsKey(row.Key))
					return RefuseMigration("legacy realm carries an unowned outbound edge", out failure);
			policy = desired;
			return true;
		}

		private static bool IsOtherPolityEndpoint(string factionName, string realmFaction,
			KingdomPolityLedger sourceLedger)
		{
			for (int i = 0; i < sourceLedger.Polities.Count; i++)
			{
				KingdomPolityRecord row = sourceLedger.Polities[i];
				if (row != null && row.ProjectedFactionId == factionName &&
					row.ProjectedFactionId != realmFaction) return true;
			}
			return false;
		}

		private static bool RefuseMigration(string reason, out string failure)
		{
			failure = reason;
			return false;
		}
	}
}
