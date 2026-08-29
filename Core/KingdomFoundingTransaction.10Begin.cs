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
		private static KingdomFoundingResult Begin(r_FounderBasin Basin, GameObject Actor,
			Zone Site, KingdomFoundingKind Kind, string Name, string Vocation,
			string VillageFaction, string VillageDisplayName)
		{
			if ((Kind == KingdomFoundingKind.FirstCity ||
				 Kind == KingdomFoundingKind.SecondCity) &&
				!KingdomPresentationRules.TryNormalizeName(Name, out Name,
					out string presentationFailure))
			{
				return Result(KingdomFoundingOutcome.Refused,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None, presentationFailure);
			}
			if (Basin == null || Basin.ParentObject == null || Actor == null || Site == null ||
				Actor.CurrentZone != Site || Actor.CurrentCell == null ||
				Actor.CurrentCell.ParentZone != Site ||
				!KingdomFoundingTransactionRules.IsKnownKind(Kind) ||
				Kind == KingdomFoundingKind.None || string.IsNullOrEmpty(Name) ||
				Name.Length > 256 ||
				(Kind == KingdomFoundingKind.FirstCity &&
					(!string.IsNullOrEmpty(Vocation) || !string.IsNullOrEmpty(VillageFaction) ||
					 !string.IsNullOrEmpty(VillageDisplayName))) ||
				(Kind == KingdomFoundingKind.SecondCity &&
					(!KingdomSettlement.IsKnownVocation(Vocation) ||
					 !string.IsNullOrEmpty(VillageFaction) ||
					 !string.IsNullOrEmpty(VillageDisplayName))) ||
				(Kind == KingdomFoundingKind.VillageCharter &&
					(string.IsNullOrEmpty(VillageFaction) ||
					 VillageFaction.Length > 256 ||
					 string.IsNullOrEmpty(VillageDisplayName) ||
					 !string.IsNullOrEmpty(Vocation))))
			{
				return Result(KingdomFoundingOutcome.Refused,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"The founder, basin, site, and name must all still be present.");
			}
			KingdomFoundingReceiptNormalization normalization = NormalizeReceipt(Basin);
			if (normalization == KingdomFoundingReceiptNormalization.ClearStaged)
			{
				if (!TryClearStagedReceipt(Basin, Site))
				{
					return Result(KingdomFoundingOutcome.RecoverableFailure,
						KingdomFoundingWaterDisposition.RestorationFailed,
						KingdomFoundingProjection.None,
						"This basin carries an unpaid but malformed staged receipt; it was quarantined.");
				}
				normalization = NormalizeReceipt(Basin);
			}
			if (normalization == KingdomFoundingReceiptNormalization.Pending)
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.HeldForRecovery,
					KingdomFoundingProjection.None,
					"This basin already carries an interrupted founding receipt.");
			}
			if (normalization == KingdomFoundingReceiptNormalization.Quarantine)
			{
				return Result(KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.RestorationFailed,
					KingdomFoundingProjection.None,
					"This basin carries a malformed founding receipt; it was quarantined without another debit.");
			}

			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			string realmFaction = Kind == KingdomFoundingKind.FirstCity
				? null : system.KingdomFactionName;
			if (Kind == KingdomFoundingKind.FirstCity && system.Founded)
			{
				return Result(KingdomFoundingOutcome.Refused,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"A realm already stands; this pour cannot replace it.");
			}
			if (Kind == KingdomFoundingKind.SecondCity)
			{
				KingdomSettlement.SecondFoundingVerdict verdict =
					KingdomFounding.JudgeSite(system, Site);
				if (verdict != KingdomSettlement.SecondFoundingVerdict.Allowed ||
					!KingdomFoundingTransactionRules.SecondRecoveryCanProject(
						system.SettlementCount, KingdomSettlement.MaxSettlements,
						system.NonSeatSettlementCount < KingdomSettlementTopologyRules.MaxNonSeatSettlements,
						TargetIsExactSeat: false,
						AlreadyPublished: false))
				{
					return Result(KingdomFoundingOutcome.Refused,
						KingdomFoundingWaterDisposition.Untouched,
						KingdomFoundingProjection.None,
						"The realm no longer has one open, eligible second-city seat.");
				}
			}
			if (Kind != KingdomFoundingKind.FirstCity &&
				(string.IsNullOrEmpty(realmFaction) || !system.Founded))
			{
				return Result(KingdomFoundingOutcome.Refused,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"No coherent realm exists for this rite.");
			}
			if (Kind != KingdomFoundingKind.FirstCity)
			{
				Faction realm = Factions.GetIfExists(realmFaction);
				if (!FactionRegistryCoherent(realmFaction, realm) ||
					realm.GetIntProperty("PlayerKingdom") != 1 ||
					realm.GetIntProperty("Village") != 1)
				{
					return Result(KingdomFoundingOutcome.Refused,
						KingdomFoundingWaterDisposition.Untouched,
						KingdomFoundingProjection.None,
						"The realm faction table and list do not agree; nothing was poured.");
				}
			}
			if (Kind == KingdomFoundingKind.VillageCharter)
			{
				Faction village = Factions.GetIfExists(VillageFaction);
				if (!FactionRegistryCoherent(VillageFaction, village) ||
					village.GetIntProperty("Village") != 1 || village.DisplayName != VillageDisplayName ||
					Site.GetZoneProperty("faction", null) != VillageFaction)
				{
					return Result(KingdomFoundingOutcome.Refused,
						KingdomFoundingWaterDisposition.Untouched,
						KingdomFoundingProjection.None,
						"The village faction is no longer one coherent living village.");
				}
			}

			LiquidVolume vessel = Basin.ParentObject.GetPart<LiquidVolume>();
			int committedVolume;
			if (vessel == null || vessel.ParentObject != Basin.ParentObject ||
				vessel.MaxVolume < vessel.Volume || vessel.MaxVolume < 0 ||
				!KingdomLiquids.HasFreshWater(vessel) ||
				!KingdomFoundingTransactionRules.TryCommittedVolume(vessel.Volume,
					KingdomRules.FoundingCostDrams, out committedVolume))
			{
				return Result(KingdomFoundingOutcome.Refused,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"The basin does not hold the exact fresh-water cost.");
			}
			if (!TryChooseExternalBinding(Site, Kind, out string externalBinding,
				out string externalFailure))
			{
				return Result(KingdomFoundingOutcome.Refused,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None, externalFailure);
			}

			string ownerNonce = Basin.EnsureOwnerNonce();
			string transaction = Guid.NewGuid().ToString("N");
			if (Kind == KingdomFoundingKind.FirstCity &&
				(!KingdomIdentityRules.TryMintRealm(transaction, out realmFaction,
					out KingdomIdentityFault identityFault) ||
				 !FactionNameAvailable(realmFaction)))
			{
				return Result(KingdomFoundingOutcome.Refused,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"The founding transaction could not reserve one unique internal realm key (" +
						identityFault + ").");
			}
			Dictionary<string, int> originalComponents = Copy(vessel.ComponentLiquids);
			Dictionary<string, int> committedComponents = committedVolume == 0
				? new Dictionary<string, int>() : Copy(vessel.ComponentLiquids);
			string originalEncoding = EncodeComponents(originalComponents);
			string committedEncoding = EncodeComponents(committedComponents);
			string payloadDigest =
				KingdomFoundingTransactionRules.PayloadDigestWithExternalBinding(Kind, Name,
				Vocation, VillageFaction, VillageDisplayName, vessel.Volume, vessel.MaxVolume,
				committedVolume, vessel.MaxVolume, originalEncoding, committedEncoding,
				externalBinding);
			KingdomFoundingAuthority authority = NewAuthority(Kind,
				KingdomFoundingOwnerKind.Basin, transaction, ownerNonce, realmFaction,
				Site.ZoneID, Actor.CurrentCell.X, Actor.CurrentCell.Y, payloadDigest);
			string encodedAuthority = KingdomFoundingTransactionRules.FormatAuthority(authority);
			if (encodedAuthority == null)
			{
				return Result(KingdomFoundingOutcome.Refused,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"The founding authority could not be encoded exactly.");
			}
			if (Kind == KingdomFoundingKind.SecondCity)
			{
				if (!system.TryPrepareLaterSettlementIdentity(transaction, Site.ZoneID,
						out string preflightSettlementId, out string topologyFailure) ||
					!system.TryPrepareSecondCityTopology(preflightSettlementId,
						out KingdomSecondCityTopologyPlan ignoredPlan, out topologyFailure))
				{
					return Result(KingdomFoundingOutcome.Refused,
						KingdomFoundingWaterDisposition.Untouched,
						KingdomFoundingProjection.None,
						"Trade or Carry cannot admit another city before this pour: " +
							topologyFailure);
				}
			}

			if (!VillageCovenantPreflight(system, Kind, transaction, encodedAuthority,
				VillageFaction, VillageDisplayName, Site, out KingdomFoundingResult covenantBar))
				return covenantBar;
			if (!TryStageFoundingReceipt(Basin, Actor, Site, vessel, Kind, transaction,
				ownerNonce, payloadDigest, encodedAuthority, realmFaction, Name, Vocation,
				VillageFaction, VillageDisplayName, committedVolume, originalComponents,
				committedComponents, externalBinding, out string stagedFailure,
				out KingdomFoundingResult stagingResult))
			{
				return stagingResult;
			}
			if (!TryAcquireFoundingReservations(Basin, Site, vessel, Kind, encodedAuthority,
				realmFaction, Name, Vocation, VillageFaction, VillageDisplayName,
				externalBinding, stagedFailure,
				out KingdomFoundingResult reservationResult))
			{
				return reservationResult;
			}

			try
			{
				if (!TryPassExternalPourBarrier(Basin, Site,
					out KingdomFoundingResult externalBlocked)) return externalBlocked;
				// Durable forward-recovery intent precedes liquid removal. A save from inside
				// Drain can never look like an unpaid staged receipt.
				Basin.PendingPhase = KingdomFoundingPhase.WaterCommitted;
				int removed = KingdomLiquids.Drain(vessel, KingdomRules.FoundingCostDrams);
				if (removed != KingdomRules.FoundingCostDrams || vessel.Volume != committedVolume)
				{
					bool restored = RestorePrePublication(Basin, vessel);
					bool cleaned = false;
					if (restored)
					{
						Basin.PendingPhase = KingdomFoundingPhase.None;
						cleaned = ClearExactReservationSet(Site, encodedAuthority,
							realmFaction, Kind == KingdomFoundingKind.VillageCharter
								? VillageFaction : null, externalBinding) && SafeClearReceipt(Basin);
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
						KingdomFoundingProjection.Water,
						"The basin did not yield the measured amount exactly.");
				}
				if (!CommittedSnapshotStillExact(Basin, vessel))
				{
					throw new InvalidOperationException(
						"The basin's committed liquid snapshot did not match its staged algebra.");
				}
				return Result(KingdomFoundingOutcome.Committed,
					KingdomFoundingWaterDisposition.Spent,
					KingdomFoundingProjection.Water);
			}
			catch (Exception ex)
			{
				bool restored = RestorePrePublication(Basin, vessel);
				bool cleaned = false;
				if (restored)
				{
					Basin.PendingPhase = KingdomFoundingPhase.None;
					cleaned = ClearExactReservationSet(Site, encodedAuthority,
						realmFaction, Kind == KingdomFoundingKind.VillageCharter
							? VillageFaction : null, externalBinding) && SafeClearReceipt(Basin);
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
					KingdomFoundingProjection.Water, Describe(ex));
			}
		}

	}
}
