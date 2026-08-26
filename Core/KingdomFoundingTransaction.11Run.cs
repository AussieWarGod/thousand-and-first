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
		private static KingdomFoundingResult Run(r_FounderBasin Basin,
			GameObject Actor, Zone Site)
		{
			KingdomFoundingReceiptNormalization normalization = NormalizeReceipt(Basin);
			if (normalization != KingdomFoundingReceiptNormalization.Pending)
			{
				return Result(normalization == KingdomFoundingReceiptNormalization.Quarantine
						? KingdomFoundingOutcome.RecoverableFailure
						: KingdomFoundingOutcome.Refused,
					normalization == KingdomFoundingReceiptNormalization.Quarantine
						? KingdomFoundingWaterDisposition.RestorationFailed
						: KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"The founding receipt header is not resumable.");
			}
			LiquidVolume vessel = Basin?.ParentObject?.GetPart<LiquidVolume>();
			if (!ValidateReceiptPayload(Basin, null, vessel, out var payloadFailure))
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.None,
					"The founding receipt payload is malformed and was quarantined before identity lookup: " +
					payloadFailure);
			}
			if (Basin.PendingOwnerNonce != Basin.OwnerNonce ||
				Basin.PendingOwnerKind != KingdomFoundingOwnerKind.Basin)
			{
				// Ordinary copies are stripped during FinalizeCopy. A surviving mismatch cannot be
				// proven to be a clone rather than corruption of the paid original, so quarantine
				// without projecting, restoring, clearing, or touching global authority.
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.None,
					"This basin does not own the founding receipt; its authority was quarantined.");
			}
			if (Basin.PendingPhase == KingdomFoundingPhase.WaterCommitted &&
				OriginalSnapshotStillExact(Basin, vessel) &&
				!TryFinishWaterCommit(Basin, vessel, out string waterFailure))
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					Basin.PendingPhase == KingdomFoundingPhase.RecoveryRequired
						? KingdomFoundingWaterDisposition.RestorationFailed
						: KingdomFoundingWaterDisposition.HeldForRecovery,
					KingdomFoundingProjection.Water, waterFailure);
			}
			if (Basin.PendingPhase == KingdomFoundingPhase.RecoveryRequired ||
				!CommittedSnapshotStillExact(Basin, vessel))
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.Water,
					"The receipt-bound basin is not the exact committed liquid snapshot; it is quarantined.");
			}
			if (Basin == null || Actor == null || Site == null || Actor.CurrentZone != Site ||
				Site.ZoneID != Basin.PendingZoneID)
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.HeldForRecovery,
					KingdomFoundingProjection.Water,
					"Return with this basin to the ground where the interrupted rite began.");
			}
			if (!ValidateReceiptPayload(Basin, Site, vessel, out payloadFailure))
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.Water,
					"The founding site no longer matches the bounded receipt: " + payloadFailure);
			}
			string authority = Basin.PendingAuthority;
			bool complete = Basin.PendingPhase == KingdomFoundingPhase.Complete;
			string reservationVillage = Basin.PendingKind ==
				KingdomFoundingKind.VillageCharter ? Basin.PendingVillageFaction : null;
			if (!complete && !GlobalReservationMatches(authority) &&
				ReservationAbsentOrExact(authority, Basin.PendingRealmFaction,
					reservationVillage) && SiteReservationMatches(Site, authority) &&
				ExistingReservationOwnersMatch(Basin))
			{
				AcquireGlobalReservation(authority, Basin.PendingRealmFaction,
					reservationVillage);
			}
			if ((!complete && (!GlobalReservationMatches(authority) ||
					!SiteReservationMatches(Site, authority))) ||
				(complete && (!ReservationAbsentOrExact(authority, Basin.PendingRealmFaction,
					reservationVillage) ||
					(HasSiteReservation(Site) &&
					 !CompletedSiteReservationSubsetMatches(Site, Basin)))))
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.HeldForRecovery,
					KingdomFoundingProjection.Water,
					"The exact global or site founding reservation is missing or belongs to another transaction.");
			}

			// Structural receipt, water algebra, site, digest and authority are proved before any
			// live realm/faction identity is consulted.
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!KingdomFoundingTransactionRules.ReceiptBindingMatches(
				Basin.PendingOwnerNonce, Basin.OwnerNonce, Basin.PendingOwnerKind,
				Basin.PendingTransactionID, Basin.PendingRealmFaction, Basin.PendingName,
				system.KingdomFactionName, Basin.PendingKind))
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.None,
					"The founding receipt no longer binds its exact transaction and realm; it is quarantined.");
			}
			if (!ReceiptFactionCoherent(Basin, system))
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.HeldForRecovery,
					KingdomFoundingProjection.Identity,
					"The exact faction registry publication is incomplete or was replaced; recovery retained it.");
			}
			if (complete)
			{
				if (!CompletionObserved(Basin, Actor, Site, system))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.HeldForRecovery,
						KingdomFoundingProjection.Seal,
						"A Complete receipt did not have every live projection and seal; it was quarantined.");
				}
				if (!FinishReceipt(Basin, Site))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.HeldForRecovery,
						KingdomFoundingProjection.Seal,
						"The completed rite could not clear its exact reservation yet.");
				}
				return Result(KingdomFoundingOutcome.Committed,
					KingdomFoundingWaterDisposition.Spent, KingdomFoundingProjection.Seal);
			}
			if (Basin.PendingKind == KingdomFoundingKind.SecondCity)
			{
				bool publishedSecond = SecondPublished(system, Basin.PendingName, Site.ZoneID,
					Basin.PendingTransactionID) &&
					PublishedSecondAuthorityMatches(Site, authority) &&
					SiteReservationMatches(Site, authority);
				bool exactSecondSeat = SecondIsExactSeat(system, Basin.PendingName,
					Site.ZoneID, Basin.PendingTransactionID);
				bool exactSecondAway = SecondIsExactAway(system, Basin.PendingName,
					Site.ZoneID, Basin.PendingTransactionID);
				if (!KingdomFoundingTransactionRules.SecondRecoveryCanProject(
					system.SettlementCount, KingdomSettlement.MaxSettlements,
					system.Away == null, exactSecondSeat, exactSecondAway, publishedSecond))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.HeldForRecovery,
						KingdomFoundingProjection.Water,
						"Another city now occupies this receipt's seat; the stale rite was quarantined without projection.");
				}
			}
			KingdomFoundingProjection projection = KingdomFoundingProjection.Water;
			try
			{
				switch (Basin.PendingKind)
				{
				case KingdomFoundingKind.FirstCity:
					PublishFirst(Basin, Actor, Site, ref projection);
					break;
				case KingdomFoundingKind.SecondCity:
					PublishSecond(Basin, Actor, Site, ref projection, Force: false);
					break;
				case KingdomFoundingKind.VillageCharter:
					PublishVillageCharter(Basin, Site, ref projection);
					break;
				default:
					throw new InvalidOperationException("The pending rite kind is not readable.");
				}
				Basin.PendingPhase = KingdomFoundingPhase.Complete;
				if (!CompletionObserved(Basin, Actor, Site, system) ||
					!FinishReceipt(Basin, Site))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.HeldForRecovery,
						KingdomFoundingProjection.Seal,
						"The rite is sealed, but its exact completion receipt remains for cleanup.");
				}
				return Result(KingdomFoundingOutcome.Committed,
					KingdomFoundingWaterDisposition.Spent, KingdomFoundingProjection.Seal);
			}
			catch (Exception ex)
			{
				bool published = DetectPublication(Basin, Site);
				if (published ||
					Basin.PendingPhase == KingdomFoundingPhase.PublicationCommitted)
				{
					Basin.PendingPhase = KingdomFoundingPhase.PublicationCommitted;
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.HeldForRecovery, projection, Describe(ex));
					}
					bool restored = RestoreOriginal(Basin, vessel, TrustCurrent: false);
					bool cleaned = false;
					if (restored)
					{
						Basin.PendingPhase = KingdomFoundingPhase.None;
						cleaned = ClearExactReservationSet(Site,
							Basin.PendingAuthority, Basin.PendingRealmFaction,
							Basin.PendingKind == KingdomFoundingKind.VillageCharter
								? Basin.PendingVillageFaction : null) &&
							SafeClearReceipt(Basin);
				}
				else
				{
					PoisonReceipt(Basin, vessel);
				}
					return Result(restored && cleaned
						? KingdomFoundingOutcome.CompensatedFailure
						: KingdomFoundingOutcome.RecoverableFailure,
					restored
						? KingdomFoundingWaterDisposition.RestoredExactly
						: KingdomFoundingWaterDisposition.RestorationFailed,
					projection, Describe(ex));
			}
		}

		/// <summary>Completes the one safe save cut between WaterCommitted intent and Drain.
		/// Only the exact original snapshot may retry removal; every third water graph poisons.</summary>
		private static bool TryFinishWaterCommit(r_FounderBasin Basin,
			LiquidVolume Vessel, out string Failure)
		{
			Failure = null;
			if (CommittedSnapshotStillExact(Basin, Vessel)) return true;
			if (Basin == null || Vessel == null ||
				Basin.PendingPhase != KingdomFoundingPhase.WaterCommitted ||
				!OriginalSnapshotStillExact(Basin, Vessel))
			{
				Failure = "The water-commit barrier does not retain its exact original snapshot.";
				return false;
			}
			try
			{
				int removed = KingdomLiquids.Drain(Vessel, KingdomRules.FoundingCostDrams);
				if (removed == KingdomRules.FoundingCostDrams &&
					CommittedSnapshotStillExact(Basin, Vessel)) return true;
			}
			catch (Exception ex)
			{
				if (CommittedSnapshotStillExact(Basin, Vessel)) return true;
				if (OriginalSnapshotStillExact(Basin, Vessel))
				{
					Failure = "The deferred water commit can retry: " + Describe(ex);
					return false;
				}
			}
			if (!OriginalSnapshotStillExact(Basin, Vessel)) PoisonReceipt(Basin, Vessel);
			Failure = "The deferred water commit did not yield the exact measured amount.";
			return false;
		}

	}
}
