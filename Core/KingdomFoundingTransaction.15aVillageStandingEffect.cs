using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransaction
	{
#if TAF_TESTS
		internal static Action<string> VillageStandingEffectFaultInjection;
#endif

		private static void VillageStandingEffectCut(string cut)
		{
#if TAF_TESTS
			VillageStandingEffectFaultInjection?.Invoke(cut);
#endif
		}

		private static void EnsureVillageStandingEffectApplied(r_FounderBasin basin,
			Zone site, KingdomSystem system)
		{
			basin.ReadVillageEffect(out int state, out int before, out int beforeCarry,
				out int after, out int afterCarry, out string digest,
				out bool any, out bool complete);
			if (!any)
			{
				// Pending receipts written before effect-v1 may finish only from their exact archived
				// covenant. A naked standing threshold is never migrated into ownership.
				if (basin.PendingPhase == KingdomFoundingPhase.PublicationCommitted &&
					ExactArchivedVillageCovenant(basin, site, system)) return;
				if (basin.PendingPhase != KingdomFoundingPhase.WaterCommitted ||
					!system.TryGetRegardPair(basin.PendingVillageFaction,
						out before, out beforeCarry))
					throw new InvalidOperationException(
						"The covenant lacks a pristine standing-effect source.");
				after = KingdomRules.VillageCharterSealedStanding;
				afterCarry = 0;
				digest = KingdomFoundingTransactionRules.VillageStandingEffectDigest(
					basin.PendingTransactionID, basin.PendingAuthority,
					basin.PendingVillageFaction, basin.PendingVillageDisplayName,
					basin.PendingZoneID, before, beforeCarry, after, afterCarry);
				if (digest == null)
					throw new InvalidOperationException(
						"Preexisting village regard cannot be attributed to this covenant.");
				// Write-ahead facts first, phase last. A save after this point can distinguish exact
				// before, exact after, and every third value without guessing from a threshold.
				basin.PendingVillageEffectBefore = before;
				basin.PendingVillageEffectBeforeCarry = beforeCarry;
				basin.PendingVillageEffectAfter = after;
				basin.PendingVillageEffectAfterCarry = afterCarry;
				basin.PendingVillageEffectDigest = digest;
				basin.PendingVillageEffectState =
					KingdomFoundingTransactionRules.VillageStandingEffectPrepared;
				basin.ReadVillageEffect(out state, out before, out beforeCarry,
					out after, out afterCarry, out digest, out any, out complete);
				VillageStandingEffectCut("village-standing:prepared");
			}
			// An incomplete receipt short-circuits before the validator runs, so the refusal names
			// itself rather than reporting whatever the validator would have said.
			string failure = "the village-standing receipt is incomplete";
			if (!complete || !VillageStandingEffectReceiptValid(basin,
				KingdomFoundingKind.VillageCharter, basin.PendingPhase, out failure))
				throw new InvalidOperationException("The village-standing intent is malformed: " +
					failure);
			if (state == KingdomFoundingTransactionRules.VillageStandingEffectApplied) return;
			if (!system.TryGetRegardPair(basin.PendingVillageFaction,
				out int current, out int currentCarry))
				throw new InvalidOperationException("The village-standing source is unreadable.");
			if (current == before && currentCarry == beforeCarry)
			{
				if (!system.TrySetRegardForRealm(basin.PendingVillageFaction, after,
					mirror: true) || !system.TryGetRegardPair(basin.PendingVillageFaction,
						out current, out currentCarry) || current != after ||
					currentCarry != afterCarry)
					throw new InvalidOperationException(
						"The exact covenant standing projection was refused.");
			}
			else if (current != after || currentCarry != afterCarry)
			{
				throw new InvalidOperationException(
					"Village regard is neither the receipt's before nor its after pair.");
			}
			VillageStandingEffectCut("village-standing:standing");
			basin.PendingVillageEffectState =
				KingdomFoundingTransactionRules.VillageStandingEffectApplied;
			if (!VillageStandingEffectReceiptValid(basin,
				KingdomFoundingKind.VillageCharter, basin.PendingPhase, out failure) ||
				basin.PendingVillageEffectState !=
					KingdomFoundingTransactionRules.VillageStandingEffectApplied)
				throw new InvalidOperationException(
					"The applied village-standing receipt was not retained exactly.");
		}

		private static bool VillageStandingEffectProvesPublication(r_FounderBasin basin,
			Zone site, KingdomSystem system)
		{
			if (!ExactVillagePublicationIdentity(basin, site, system)) return false;
			if (ExactArchivedVillageCovenant(basin, site, system)) return true;
			basin.ReadVillageEffect(out int state, out int before, out int beforeCarry,
				out int after, out int afterCarry, out string _, out bool any,
				out bool complete);
			if (!any || !complete || !VillageStandingEffectReceiptValid(basin,
				KingdomFoundingKind.VillageCharter, basin.PendingPhase, out string _)) return false;
			if (state == KingdomFoundingTransactionRules.VillageStandingEffectApplied)
				return true;
			return state == KingdomFoundingTransactionRules.VillageStandingEffectPrepared &&
				system.TryGetRegardPair(basin.PendingVillageFaction,
					out int current, out int currentCarry) &&
				current == after && currentCarry == afterCarry &&
				(before != after || beforeCarry != afterCarry);
		}

		private static bool ExactVillagePublicationIdentity(r_FounderBasin basin,
			Zone site, KingdomSystem system)
		{
			Faction village = basin == null ? null :
				Factions.GetIfExists(basin.PendingVillageFaction);
			return basin != null && site != null && system != null && system.Founded &&
				site.ZoneID == basin.PendingZoneID &&
				!string.IsNullOrEmpty(basin.PendingVillageFaction) &&
				FactionRegistryCoherent(basin.PendingVillageFaction, village) &&
				village.GetIntProperty("Village") == 1 &&
				village.DisplayName == basin.PendingVillageDisplayName &&
				village.GetStringProperty(VillageReservationProperty, null) ==
					basin.PendingAuthority &&
				SiteReservationMatches(site, basin.PendingAuthority) &&
				site.GetZoneProperty("faction", null) == basin.PendingVillageFaction;
		}

		private static bool ExactArchivedVillageCovenant(r_FounderBasin basin,
			Zone site, KingdomSystem system)
		{
			return ExactVillagePublicationIdentity(basin, site, system) &&
				KingdomVillageCovenantRuntime.TryArchived(system,
					basin.PendingTransactionID, basin.PendingAuthority,
					basin.PendingVillageFaction, basin.PendingVillageDisplayName,
					site.ZoneID, basin.PendingChronicleEventID,
					out int sealedStanding, out long reservationTick) &&
				sealedStanding >= KingdomVillageCovenantRules.MinimumSealedStandingV1 &&
				ArchivedReservationTickStillMatches(site, reservationTick);
		}
	}
}
