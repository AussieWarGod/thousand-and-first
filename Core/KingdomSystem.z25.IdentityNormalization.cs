using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private void NormalizeIdentity(bool AllowLegacyMigration)
		{
			if (!Founded)
			{
				// A first-founding callback may save after exact ids were written but before the
				// faction/name publication. That complete transaction tuple is recoverable; every
				// other current-realm fragment is quarantined rather than guessed into authority.
				if (NewIdentityEvidenceEmpty() &&
					string.IsNullOrEmpty(PendingSettlementId) &&
					string.IsNullOrEmpty(PendingSettlementTransactionId) &&
					string.IsNullOrEmpty(PendingSettlementZoneId) &&
					string.IsNullOrEmpty(PendingSettlementAuthority)) return;
				if (RealmIdentityOrigin == KingdomIdentityOrigin.FoundingTransaction &&
					FirstIdentityMatches(RealmIdentityTransactionId,
						RealmIdentityFirstClaimedZone)) return;
				QuarantineIdentity("unfounded state carries partial current-realm identity");
				return;
			}

			if (NewIdentityEvidenceEmpty())
			{
				string migrationFailure = null;
				if (!AllowLegacyMigration || !TryMigrateLegacyIdentity(out migrationFailure))
				{
					QuarantineIdentity(AllowLegacyMigration
						? migrationFailure
						: "this named save has no immutable identity; pre-v8 authority is not readable");
					return;
				}
			}
			string lifecycleFailure;
			if (!TryBindDormantLifecycleIdentity(out lifecycleFailure))
			{
				QuarantineIdentity(lifecycleFailure);
				return;
			}
			if (Away != null)
			{
				if (Away.LifecycleBook == null) Away.LifecycleBook = new KingdomLifecycleBook();
				KingdomLifecycleRules.Normalize(Away.LifecycleBook);
				List<string> seatedLifecycleIds = LifecycleCollisionIds(
					IncludeSeat: true, IncludeAway: false);
				if (!KingdomLifecycleRules.BindSettlementIdentity(Away.LifecycleBook,
					Away.City?.SettlementId, LegacyMigration: false, MigrationKey: null,
					ExistingIds: seatedLifecycleIds))
				{
					Away.LifecycleBook.Quarantined = true;
					Away.LifecycleBook.Fault =
						"away lifecycle book does not match immutable city identity";
					QuarantineIdentity(Away.LifecycleBook.Fault);
					return;
				}
			}
			List<string> current;
			string failure;
			if (!TryExactSettlementIds(RequirePublishedClaims: true, out current,
				out failure))
			{
				// The exact first transaction is permitted to wait for its claim callback. It
				// grants no CurrentSettlementId until the claim exists.
				if (FirstIdentityMatches(RealmIdentityTransactionId,
					RealmIdentityFirstClaimedZone)) return;
				QuarantineIdentity(failure);
				return;
			}
			if (!PendingSettlementTupleValid(out string pendingFailure))
			{
				QuarantineIdentity(pendingFailure);
				return;
			}
			if (!string.IsNullOrEmpty(PendingSettlementId) &&
				current.Contains(PendingSettlementId))
			{
				// City publication won the save cut. Only explicit forward settlement may erase
				// the redo tuple; normalization never grows either authority book independently.
				if (!TrySettlePendingSettlementIdentity(PendingSettlementTransactionId,
					PendingSettlementZoneId, PendingSettlementAuthority, out failure))
				{
					QuarantineIdentity("published pending city could not settle exact topology: " +
						failure);
				}
			}
		}

		private bool TryMigrateLegacyIdentity(out string Failure)
		{
			Failure = null;
			string seatZone;
			string awayZone = null;
			if (!TryFirstClaimEvidence(ClaimedZones, out seatZone) ||
				(Away != null && !TryFirstClaimEvidence(Away.ClaimedZones, out awayZone)) ||
				string.IsNullOrEmpty(KingdomFactionName) || KingdomFactionName.Length > 512 ||
				(City != null && City.SettlementId != null && City.SettlementId.Length > 256) ||
				(Away?.City != null && Away.City.SettlementId != null &&
				 Away.City.SettlementId.Length > 256))
			{
				Failure = "legacy identity evidence is partial or outside hard bounds";
				return false;
			}
			KingdomIdentityFault fault;
			string realm;
			string seatId;
			string awayId = null;
			if (!KingdomIdentityRules.TryMigrateRealm(KingdomFactionName, FoundedTick,
					SimulationSeedHigh, SimulationSeedLow, seatZone, out realm, out fault) ||
				!KingdomIdentityRules.TryMigrateSettlement(realm, FoundedTick, seatZone,
					out seatId, out fault) ||
				(Away != null && !KingdomIdentityRules.TryMigrateSettlement(realm,
					Away.FoundedTick, awayZone, out awayId, out fault)))
			{
				Failure = "legacy identity evidence could not mint a complete set (" + fault + ").";
				return false;
			}
			List<string> ids = new List<string> { seatId };
			if (awayId != null) ids.Add(awayId);
			if (!KingdomIdentityRules.ValidateRealmTopology(realm, ids, out fault))
			{
				Failure = "legacy identity set is duplicate or malformed (" + fault + ").";
				return false;
			}
			string oldSeatId = City?.SettlementId;
			string oldAwayId = Away?.City?.SettlementId;
			RealmId = realm;
			RealmIdentityVersion = KingdomIdentityRules.RulesVersion;
			RealmIdentityOrigin = KingdomIdentityOrigin.LegacyMigration;
			RealmIdentityTransactionId = null;
			RealmIdentityLegacyFaction = KingdomFactionName;
			RealmIdentityFoundedTick = FoundedTick;
			RealmIdentitySeedHigh = SimulationSeedHigh;
			RealmIdentitySeedLow = SimulationSeedLow;
			RealmIdentityFirstClaimedZone = seatZone;
			if (City == null) City = new Simulation.City.KingdomCityBook();
			City.SettlementId = seatId;
			SettlementIdentityVersion = KingdomIdentityRules.RulesVersion;
			SettlementIdentityOrigin = KingdomIdentityOrigin.LegacyMigration;
			SettlementIdentityTransactionId = null;
			SettlementIdentityFoundedTick = FoundedTick;
			SettlementIdentityFirstClaimedZone = seatZone;
			SettlementIdentityLegacyId = oldSeatId;
			if (Away != null)
			{
				if (Away.City == null) Away.City = new Simulation.City.KingdomCityBook();
				Away.City.SettlementId = awayId;
				Away.SettlementIdentityVersion = KingdomIdentityRules.RulesVersion;
				Away.SettlementIdentityOrigin = KingdomIdentityOrigin.LegacyMigration;
				Away.SettlementIdentityTransactionId = null;
				Away.SettlementIdentityFoundedTick = Away.FoundedTick;
				Away.SettlementIdentityFirstClaimedZone = awayZone;
				Away.SettlementIdentityLegacyId = oldAwayId;
			}
			IdentityFault = null;
			return true;
		}

	}
}
