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
			List<KingdomSettlement> nonSeat = NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++)
			{
				KingdomSettlement row = nonSeat[i];
				if (row.LifecycleBook == null) row.LifecycleBook = new KingdomLifecycleBook();
				KingdomLifecycleRules.Normalize(row.LifecycleBook);
				List<string> collisionIds = LifecycleCollisionIds(
					IncludeSeat: true, IncludeAway: true);
				collisionIds.Remove(row.City?.SettlementId);
				if (!KingdomLifecycleRules.BindSettlementIdentity(row.LifecycleBook,
					row.City?.SettlementId, LegacyMigration: false, MigrationKey: null,
					ExistingIds: collisionIds))
				{
					row.LifecycleBook.Quarantined = true;
					row.LifecycleBook.Fault =
						"non-seat lifecycle book does not match immutable city identity";
					QuarantineIdentity(row.LifecycleBook.Fault);
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
			List<KingdomSettlement> nonSeat = NonSeatSettlements();
			List<string> otherZones = new List<string>();
			if (!TryFirstClaimEvidence(ClaimedZones, out seatZone) ||
				string.IsNullOrEmpty(KingdomFactionName) || KingdomFactionName.Length > 512 ||
				(City != null && City.SettlementId != null && City.SettlementId.Length > 256))
			{
				Failure = "legacy identity evidence is partial or outside hard bounds";
				return false;
			}
			for (int i = 0; i < nonSeat.Count; i++)
			{
				if (!TryFirstClaimEvidence(nonSeat[i].ClaimedZones, out string zone) ||
					(nonSeat[i].City?.SettlementId != null &&
					 nonSeat[i].City.SettlementId.Length > 256))
				{
					Failure = "legacy non-seat identity evidence is partial or outside hard bounds";
					return false;
				}
				otherZones.Add(zone);
			}
			KingdomIdentityFault fault;
			string realm;
			string seatId;
			if (!KingdomIdentityRules.TryMigrateRealm(KingdomFactionName, FoundedTick,
					SimulationSeedHigh, SimulationSeedLow, seatZone, out realm, out fault) ||
				!KingdomIdentityRules.TryMigrateSettlement(realm, FoundedTick, seatZone,
					out seatId, out fault))
			{
				Failure = "legacy identity evidence could not mint a complete set (" + fault + ").";
				return false;
			}
			List<string> otherIds = new List<string>();
			for (int i = 0; i < nonSeat.Count; i++)
			{
				if (!KingdomIdentityRules.TryMigrateSettlement(realm, nonSeat[i].FoundedTick,
					otherZones[i], out string id, out fault))
				{
					Failure = "legacy identity evidence could not mint a complete set (" +
						fault + ").";
					return false;
				}
				otherIds.Add(id);
			}
			List<string> ids = new List<string> { seatId };
			ids.AddRange(otherIds);
			if (!KingdomIdentityRules.ValidateRealmTopology(realm, ids, out fault))
			{
				Failure = "legacy identity set is duplicate or malformed (" + fault + ").";
				return false;
			}
			string oldSeatId = City?.SettlementId;
			List<string> oldOtherIds = new List<string>();
			for (int i = 0; i < nonSeat.Count; i++)
				oldOtherIds.Add(nonSeat[i]?.City?.SettlementId);
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
			for (int i = 0; i < nonSeat.Count; i++)
			{
				KingdomSettlement row = nonSeat[i];
				if (row.City == null) row.City = new Simulation.City.KingdomCityBook();
				row.City.SettlementId = otherIds[i];
				row.SettlementIdentityVersion = KingdomIdentityRules.RulesVersion;
				row.SettlementIdentityOrigin = KingdomIdentityOrigin.LegacyMigration;
				row.SettlementIdentityTransactionId = null;
				row.SettlementIdentityFoundedTick = row.FoundedTick;
				row.SettlementIdentityFirstClaimedZone = otherZones[i];
				row.SettlementIdentityLegacyId = oldOtherIds[i];
			}
			IdentityFault = null;
			return true;
		}

	}
}
