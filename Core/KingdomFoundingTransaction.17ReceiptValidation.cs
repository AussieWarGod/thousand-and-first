using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using ThousandAndFirst.Api;
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
		private static KingdomFoundingReceiptNormalization NormalizeReceipt(
			r_FounderBasin Basin)
		{
			if (Basin == null)
			{
				return KingdomFoundingReceiptNormalization.Clean;
			}
			Basin.TryReadRawHeader(out var rawKind, out var rawPhase,
				out var kindPresent, out var phasePresent);
			return KingdomFoundingTransactionRules.NormalizeRaw(kindPresent, rawKind,
				phasePresent, rawPhase, Basin.HasReceiptPayloadBeyondHeader);
		}

		private static bool ValidateReceiptPayload(r_FounderBasin Basin, Zone Site,
			LiquidVolume Vessel, out string Failure)
		{
			Failure = "";
			if (Basin == null || Basin.ParentObject == null ||
				!Basin.HasCompleteReceiptSchema)
			{
				Failure = "required receipt keys are missing";
				return false;
			}
			Basin.TryReadRawHeader(out var rawKind, out var rawPhase,
				out var kindPresent, out var phasePresent);
			bool chronicleRawPresent = Basin.TryReadRawChronicle(out var chronicleRaw);
			bool dispositionRawPresent = Basin.TryReadRawChronicleDisposition(
				out var dispositionRaw);
			if (!kindPresent || !phasePresent ||
				!KingdomFoundingTransactionRules.TryParseKind(rawKind, out var kind) ||
				!KingdomFoundingTransactionRules.TryParsePhase(rawPhase, out var phase) ||
				kind == KingdomFoundingKind.None ||
				!KingdomFoundingTransactionRules.IsValidPair(kind, phase) ||
				Basin.PendingKind != kind || Basin.PendingPhase != phase)
			{
				Failure = "kind/phase is unknown or contradictory";
				return false;
			}
			if (!KingdomFoundingTransactionRules.IsNonce(Basin.PendingTransactionID) ||
				!KingdomFoundingTransactionRules.IsNonce(Basin.PendingOwnerNonce) ||
				Basin.PendingOwnerKind != KingdomFoundingOwnerKind.Basin ||
				string.IsNullOrEmpty(Basin.PendingBasinID) ||
				Basin.PendingBasinID.Length > 256 ||
				Basin.PendingBasinID != Basin.ParentObject.IDIfAssigned ||
				string.IsNullOrEmpty(Basin.PendingRealmFaction) ||
				Basin.PendingRealmFaction.Length > 256 ||
				string.IsNullOrEmpty(Basin.PendingName) || Basin.PendingName.Length > 256 ||
				string.IsNullOrEmpty(Basin.PendingZoneID) || Basin.PendingZoneID.Length > 512 ||
				Basin.PendingRiteX < 0 || Basin.PendingRiteX > 255 ||
				Basin.PendingRiteY < 0 || Basin.PendingRiteY > 255 ||
				!KingdomFoundingTransactionRules.IsLowerHex(
					Basin.PendingPayloadDigest, 64) ||
				!chronicleRawPresent || (chronicleRaw != 0 && chronicleRaw != 1) ||
				Basin.PendingChronicleStage < 0 || Basin.PendingChronicleStage > 2 ||
				Basin.PendingChronicleRecorded !=
					(Basin.PendingChronicleStage == 2) ||
				((phase == KingdomFoundingPhase.None ||
				  phase == KingdomFoundingPhase.WaterCommitted) &&
				 Basin.PendingChronicleStage != 0) ||
				(phase == KingdomFoundingPhase.Complete &&
				 Basin.PendingChronicleStage != 2) ||
				Basin.PendingChronicleEventID != FoundingEventID(kind,
					Basin.PendingTransactionID, "chronicle"))
			{
				Failure = "identity, bounds, or outbox fields are malformed";
				return false;
			}
			int accomplishmentCount = CountAccomplishments(
				Basin.PendingChronicleEventID);
			if (!KingdomFoundingTransactionRules.TryMigrateChronicleDisposition(
				Basin.PendingChronicleStage, dispositionRawPresent, dispositionRaw,
				accomplishmentCount,
				Options.GetOption("r_TAF_OptionChronicle") == "No",
				out var disposition, out var writeDisposition))
			{
				Failure = "chronicle journal disposition is malformed or ambiguous";
				return false;
			}
			if (!writeDisposition && Basin.PendingChronicleDisposition != disposition)
			{
				Failure = "chronicle journal disposition readback differs";
				return false;
			}
			if ((kind == KingdomFoundingKind.FirstCity &&
					(Basin.HasVocationField || Basin.HasVillageFactionField ||
					 Basin.HasVillageDisplayField ||
					 !KingdomIdentityRules.FirstFactionKeyMatches(
						 Basin.PendingRealmFaction, Basin.PendingTransactionID,
						 Basin.PendingName, AllowLegacy: true))) ||
				(kind == KingdomFoundingKind.SecondCity &&
					(!Basin.HasVocationField ||
					 !KingdomSettlement.IsKnownVocation(Basin.PendingVocation) ||
					 Basin.HasVillageFactionField || Basin.HasVillageDisplayField)) ||
				(kind == KingdomFoundingKind.VillageCharter &&
					(Basin.HasVocationField || !Basin.HasVillageFactionField ||
					 !Basin.HasVillageDisplayField ||
					 string.IsNullOrEmpty(Basin.PendingVillageFaction) ||
					 Basin.PendingVillageFaction.Length > 256 ||
					 string.IsNullOrEmpty(Basin.PendingVillageDisplayName) ||
					 Basin.PendingVillageDisplayName.Length > 256)))
			{
				Failure = "kind-specific payload fields are malformed";
				return false;
			}
			if (!VillageStandingEffectReceiptValid(Basin, kind, phase, out Failure))
				return false;
			if (!Basin.TryGetOriginalComponents(out var original, out var originalEncoded) ||
				!Basin.TryGetCommittedComponents(out var committed, out var committedEncoded) ||
				EncodeComponents(original) != originalEncoded ||
				EncodeComponents(committed) != committedEncoded ||
				!KingdomFoundingTransactionRules.WaterAlgebraValid(
					Basin.PendingOriginalVolume, Basin.PendingOriginalMaxVolume,
					Basin.PendingCommittedVolume, Basin.PendingCommittedMaxVolume,
					KingdomRules.FoundingCostDrams,
					KingdomFoundingTransactionRules.ComponentsDescribePureWater(original,
						Basin.PendingOriginalVolume),
					KingdomFoundingTransactionRules.ComponentsDescribePureWater(committed,
						Basin.PendingCommittedVolume)))
			{
				Failure = "liquid components or volume algebra is malformed";
				return false;
			}
			string digest;
			if (Basin.HasExternalBindingField)
			{
				if (!KingdomExternalOwnershipRules.TryDecode(
					Basin.PendingExternalBinding, out var externalBinding))
				{
					Failure = "external-owner receipt is malformed";
					return false;
				}
				digest = KingdomFoundingTransactionRules.PayloadDigestWithExternalBinding(
					kind, Basin.PendingName, Basin.PendingVocation,
					Basin.PendingVillageFaction, Basin.PendingVillageDisplayName,
					Basin.PendingOriginalVolume, Basin.PendingOriginalMaxVolume,
					Basin.PendingCommittedVolume, Basin.PendingCommittedMaxVolume,
					originalEncoded, committedEncoded, Basin.PendingExternalBinding);
			}
			else
			{
				digest = KingdomFoundingTransactionRules.PayloadDigest(kind,
					Basin.PendingName, Basin.PendingVocation, Basin.PendingVillageFaction,
					Basin.PendingVillageDisplayName, Basin.PendingOriginalVolume,
					Basin.PendingOriginalMaxVolume, Basin.PendingCommittedVolume,
					Basin.PendingCommittedMaxVolume, originalEncoded, committedEncoded);
			}
			if (digest != Basin.PendingPayloadDigest ||
				!KingdomFoundingTransactionRules.TryParseAuthority(
					Basin.PendingAuthority, out var authority) ||
				authority.Kind != kind || authority.TransactionID != Basin.PendingTransactionID ||
				authority.OwnerKind != Basin.PendingOwnerKind ||
				authority.OwnerNonce != Basin.PendingOwnerNonce ||
				authority.RealmFaction != Basin.PendingRealmFaction ||
				authority.ZoneID != Basin.PendingZoneID ||
				authority.RiteX != Basin.PendingRiteX ||
				authority.RiteY != Basin.PendingRiteY ||
				authority.PayloadDigest != digest)
			{
				Failure = "payload digest or authority tuple does not match";
				return false;
			}
			if (Site != null)
			{
				if (Site.ZoneID != Basin.PendingZoneID ||
					Site.GetCell(Basin.PendingRiteX, Basin.PendingRiteY) == null)
				{
					Failure = "site, rite coordinates, or site payload differs";
					return false;
				}
				bool siteReservationPresent = HasSiteReservation(Site);
				if (phase == KingdomFoundingPhase.Complete && siteReservationPresent &&
					!CompletedSiteReservationSubsetMatches(Site, Basin))
				{
					Failure = "completed site cleanup contains a foreign marker";
					return false;
				}
				if (phase != KingdomFoundingPhase.Complete &&
					(!TryReadSiteReservation(Site, out var siteAuthority, out var siteName,
						out var siteVocation, out var siteVillage, out var siteDisplay,
						out var siteTick) ||
					 KingdomFoundingTransactionRules.FormatAuthority(siteAuthority) !=
						Basin.PendingAuthority || siteName != Basin.PendingName ||
					 siteVocation != Basin.PendingVocation ||
					 siteVillage != Basin.PendingVillageFaction ||
					 siteDisplay != Basin.PendingVillageDisplayName))
				{
					Failure = "site reservation payload differs";
					return false;
				}
				if (kind != KingdomFoundingKind.VillageCharter &&
					Basin.HasExternalBindingField && phase != KingdomFoundingPhase.Complete &&
					!KingdomExternalOwnershipBindingRuntime.StageMatches(Site,
						Basin.PendingAuthority, Basin.PendingExternalBinding))
				{
					Failure = "external-owner site reservation differs";
					return false;
				}
			}
			if (Vessel == null || Vessel.ParentObject != Basin.ParentObject)
			{
				Failure = "receipt vessel is missing or belongs to another object";
				return false;
			}
			// Legacy migration is itself a mutation. Defer it until every identity, water, site,
			// and vessel check has succeeded so a malformed receipt remains byte-for-byte intact.
			if (writeDisposition)
			{
				Basin.PendingChronicleDisposition = disposition;
				if (!Basin.TryReadRawChronicleDisposition(out var writtenDisposition) ||
					writtenDisposition != (int)disposition)
				{
					Failure = "chronicle journal disposition migration was not retained";
					return false;
				}
			}
			return true;
		}

		private static bool TryClearStagedReceipt(r_FounderBasin Basin, Zone Site)
		{
			LiquidVolume vessel = Basin?.ParentObject?.GetPart<LiquidVolume>();
			if (!ValidateReceiptPayload(Basin, null, vessel, out var failure) ||
				Basin.PendingPhase != KingdomFoundingPhase.None ||
				!OriginalSnapshotStillExact(Basin, vessel) ||
				Site == null || Site.ZoneID != Basin.PendingZoneID ||
				Site.GetCell(Basin.PendingRiteX, Basin.PendingRiteY) == null ||
				DetectPublication(Basin, Site) ||
				!ReservationAbsentOrExact(Basin.PendingAuthority,
					Basin.PendingRealmFaction,
					Basin.PendingKind == KingdomFoundingKind.VillageCharter
						? Basin.PendingVillageFaction : null) ||
				(HasSiteReservation(Site) &&
				 !SiteReservationMatches(Site, Basin.PendingAuthority)))
			{
				return false;
			}
			string authority = Basin.PendingAuthority;
			string village = Basin.PendingKind == KingdomFoundingKind.VillageCharter
				? Basin.PendingVillageFaction : null;
			if (!ClearExactReservationSet(Site, authority,
				Basin.PendingRealmFaction, village,
				Basin.HasExternalBindingField ? Basin.PendingExternalBinding : null))
			{
				return false;
			}
			return SafeClearReceipt(Basin);
		}

	}
}
