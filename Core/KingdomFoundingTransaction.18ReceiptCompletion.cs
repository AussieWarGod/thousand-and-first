using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransaction
	{
		private static bool CompletionObserved(r_FounderBasin Basin, GameObject Actor,
			Zone Site, KingdomSystem System)
		{
			if (Basin == null || Site == null || System == null ||
				Basin.PendingPhase != KingdomFoundingPhase.Complete ||
				Site.ZoneID != Basin.PendingZoneID ||
				Basin.PendingChronicleStage != 2 ||
				Basin.PendingChronicleEventID != FoundingEventID(Basin.PendingKind,
					Basin.PendingTransactionID, "chronicle") ||
				!ChronicleAccomplishmentObserved(Basin.PendingChronicleEventID,
					Basin.PendingChronicleDisposition))
			{
				return false;
			}
			Faction realm = Factions.GetIfExists(Basin.PendingRealmFaction);
			if (!FactionRegistryCoherent(Basin.PendingRealmFaction, realm) ||
				realm.GetIntProperty("PlayerKingdom") != 1 ||
				realm.GetIntProperty("Village") != 1 || !System.Founded ||
				System.KingdomFactionName != Basin.PendingRealmFaction)
			{
				return false;
			}
			bool projected;
			switch (Basin.PendingKind)
			{
			case KingdomFoundingKind.FirstCity:
				projected = System.SettlementCount == 1 &&
					System.NonSeatSettlementCount == 0 &&
					KingdomPolityRuntime.FoundationObserved(System, realm) &&
					System.FirstIdentityMatches(Basin.PendingTransactionID, Site.ZoneID) &&
					System.SettlementName == Basin.PendingName &&
					System.ClaimedZones.Contains(Site.ZoneID) &&
					Site.GetZoneProperty("faction", null) == Basin.PendingRealmFaction &&
					realm.HolyPlaces.Contains(Site.ZoneID) &&
					realm.GetIntProperty(PendingFactionProperty) == 0 &&
					realm.GetStringProperty(PendingFactionTransactionProperty, null) ==
						Basin.PendingTransactionID &&
					realm.GetStringProperty(PendingFactionAuthorityProperty, null) ==
						Basin.PendingAuthority &&
					Site.GetZoneProperty(ClaimChronicleEventProperty, null) ==
						FoundingEventID(KingdomFoundingKind.FirstCity,
							Basin.PendingTransactionID, "claim") &&
					Site.GetZoneProperty(ClaimChronicleStageProperty, null) == "2" &&
					Site.GetZoneProperty(ClaimChronicleDispositionProperty, null) ==
						((int)KingdomChronicleDisposition.Skipped).ToString() &&
					Site.GetZoneProperty(ClaimFoundingProperty, null) == "1";
				break;
			case KingdomFoundingKind.SecondCity:
				projected = System.SettlementCount >= 2 &&
					System.SettlementCount <= KingdomSettlement.MaxSettlements &&
					System.TryProveSettledSecondCityTopology(out string _) &&
					PublishedSecondAuthorityMatches(Site, Basin.PendingAuthority) &&
					SecondIsExactSeat(System, Basin.PendingName, Site.ZoneID,
						Basin.PendingTransactionID) &&
					System.NonSeatSettlementCount >= 1 &&
					Site.GetZoneProperty(SecondChronicleDispositionProperty, null) ==
						((int)Basin.PendingChronicleDisposition).ToString() &&
					Site.GetZoneProperty("faction", null) == Basin.PendingRealmFaction &&
					realm.HolyPlaces.Contains(Site.ZoneID);
				break;
			case KingdomFoundingKind.VillageCharter:
				Faction village = Factions.GetIfExists(Basin.PendingVillageFaction);
				// The archived row is required, and the covenant's own frozen standing and
				// reservation tick are what prove it -- never today's ledger, not even by way of
				// a live standing read. Completion is what releases the reservation and clears the
				// receipt, so a covenant allowed to complete without its record would have that
				// cleanup erase the only evidence the rite happened; and a covenant proved by a
				// standing that has moved since would be history read off the weather.
				projected = FactionRegistryCoherent(Basin.PendingVillageFaction, village) &&
					village.GetIntProperty("Village") == 1 &&
					village.DisplayName == Basin.PendingVillageDisplayName &&
					Site.GetZoneProperty("faction", null) == Basin.PendingVillageFaction &&
					KingdomVillageCovenantRuntime.TryArchived(System,
						Basin.PendingTransactionID, Basin.PendingAuthority,
						Basin.PendingVillageFaction, Basin.PendingVillageDisplayName,
						Site.ZoneID, Basin.PendingChronicleEventID,
						out int sealedStanding, out long reservationTick) &&
					sealedStanding >= KingdomVillageCovenantRules.MinimumSealedStandingV1 &&
					ArchivedReservationTickStillMatches(Site, reservationTick);
				break;
			default:
				return false;
			}
			if (!projected || !ExternalBindingCompletionObserved(Basin, Site) ||
				!EnsureAbility(Actor) ||
				(Basin.PendingKind != KingdomFoundingKind.VillageCharter &&
				 !EnsurePlacement(System, Site, Basin.PendingRiteX, Basin.PendingRiteY)))
			{
				return false;
			}
			string failure;
			return Basin.PendingKind == KingdomFoundingKind.FirstCity
				? KingdomSeal.TryFoundingCompleted(out failure)
				: KingdomSeal.TryStageSemanticSnapshot(
					Basin.PendingKind == KingdomFoundingKind.SecondCity
						? "second founding completion observation"
						: "village charter completion observation", out failure);
		}

		private static bool FinishReceipt(r_FounderBasin Basin, Zone Site)
		{
			if (Basin == null || Site == null ||
				Basin.PendingPhase != KingdomFoundingPhase.Complete)
			{
				return false;
			}
			string authority = Basin.PendingAuthority;
			string village = Basin.PendingKind == KingdomFoundingKind.VillageCharter
				? Basin.PendingVillageFaction : null;
			if (!ReservationAbsentOrExact(authority, Basin.PendingRealmFaction, village) ||
				(HasSiteReservation(Site) &&
				 !CompletedSiteReservationSubsetMatches(Site, Basin)))
			{
				return false;
			}
			if (KingdomFoundingTransactionRules.TryParseAuthority(authority,
				out var parsed) && parsed.Kind == KingdomFoundingKind.SecondCity)
			{
				KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
				if (system == null || !system.Founded ||
					system.KingdomFactionName != parsed.RealmFaction ||
					!system.TryProveSettledSecondCityTopology(out string _)) return false;
			}
			if (!ReleaseGlobalReservation(authority, Basin.PendingRealmFaction, village))
			{
				return false;
			}
			if (!FinishExternalBinding(Basin, Site)) return false;
			if (HasSiteReservation(Site) &&
				!ClearCompletedSiteReservation(Site, Basin))
			{
				return false;
			}
			return !HasSiteReservation(Site) &&
				GlobalReservationMarkersAbsent(Basin.PendingRealmFaction, village) &&
				SafeClearReceipt(Basin);
		}

		private static bool SafeClearReceipt(r_FounderBasin Basin)
		{
			try
			{
				Basin?.ClearPendingRite();
				if (Basin == null)
				{
					return true;
				}
				Basin.TryReadRawHeader(out var rawKind, out var rawPhase,
					out var kindPresent, out var phasePresent);
				return !kindPresent && !phasePresent && !Basin.HasAnyReceiptState &&
					!Basin.HasReceiptPayloadBeyondHeader &&
					Basin.PendingKind == KingdomFoundingKind.None &&
					Basin.PendingPhase == KingdomFoundingPhase.None;
			}
			catch (Exception ex)
			{
				KingdomLog.Log("founding receipt cleanup failed: " + Describe(ex));
				return false;
			}
		}

		/// <summary>Both engine registries must name the same faction exactly once.</summary>
	}
}
