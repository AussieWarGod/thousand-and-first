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
		internal static bool FoundingAuthorityStillExact(string Authority, Zone Site)
		{
			r_FounderBasin basin;
			lock (InFlightSync)
			{
				if (InFlight == null || InFlight.Authority != Authority || InFlight.Basin == null)
				{
					return false;
				}
				basin = InFlight.Basin;
			}
			if (!KingdomFoundingTransactionRules.TryParseAuthority(Authority, out var parsed) ||
				Site == null || Site.ZoneID != parsed.ZoneID ||
				basin.PendingAuthority != Authority || basin.PendingKind != parsed.Kind ||
				basin.PendingTransactionID != parsed.TransactionID ||
				basin.PendingOwnerKind != parsed.OwnerKind ||
				basin.PendingOwnerNonce != parsed.OwnerNonce ||
				basin.PendingRealmFaction != parsed.RealmFaction ||
				basin.PendingZoneID != parsed.ZoneID || basin.PendingRiteX != parsed.RiteX ||
				basin.PendingRiteY != parsed.RiteY ||
				basin.PendingPayloadDigest != parsed.PayloadDigest ||
				!GlobalReservationMatches(Authority) || !SiteReservationMatches(Site, Authority))
			{
				return false;
			}
			if (!FoundingReceiptStillExact(basin, parsed))
			{
				return false;
			}
			Faction realm = Factions.GetIfExists(parsed.RealmFaction);
			if (realm == null || realm.GetStringProperty(RealmReservationProperty, null) != Authority)
			{
				return false;
			}
			if (parsed.Kind == KingdomFoundingKind.VillageCharter)
			{
				Faction village = Factions.GetIfExists(basin.PendingVillageFaction);
				return village != null && village.GetStringProperty(
					VillageReservationProperty, null) == Authority;
			}
			return true;
		}

		private static bool FoundingReceiptStillExact(r_FounderBasin Basin,
			KingdomFoundingAuthority Authority)
		{
			if (Basin == null ||
				(Basin.PendingPhase != KingdomFoundingPhase.WaterCommitted &&
				 Basin.PendingPhase != KingdomFoundingPhase.PublicationCommitted) ||
				Basin.PendingChronicleEventID != FoundingEventID(Authority.Kind,
					Authority.TransactionID, "chronicle"))
			{
				return false;
			}
			if (Authority.OwnerKind == KingdomFoundingOwnerKind.Direct)
			{
				return Basin.ParentObject == null && Authority.PayloadDigest ==
					DirectPayloadDigest(Authority.Kind, Basin.PendingName,
						Basin.PendingVocation, Basin.PendingVillageFaction,
						Basin.PendingVillageDisplayName);
			}
			if (Authority.OwnerKind != KingdomFoundingOwnerKind.Basin ||
				Basin.ParentObject == null || !Basin.HasCompleteReceiptSchema ||
				Basin.PendingBasinID != Basin.ParentObject.ID ||
				!Basin.TryGetOriginalComponents(out var original, out var originalEncoded) ||
				!Basin.TryGetCommittedComponents(out var committed, out var committedEncoded) ||
				EncodeComponents(original) != originalEncoded ||
				EncodeComponents(committed) != committedEncoded ||
				!KingdomFoundingTransactionRules.WaterAlgebraValid(
					Basin.PendingOriginalVolume, Basin.PendingOriginalMaxVolume,
					Basin.PendingCommittedVolume, Basin.PendingCommittedMaxVolume,
					KingdomRules.FoundingCostDrams,
					KingdomFoundingTransactionRules.ComponentsDescribePureWater(
						original, Basin.PendingOriginalVolume),
					KingdomFoundingTransactionRules.ComponentsDescribePureWater(
						committed, Basin.PendingCommittedVolume)) ||
				!CommittedSnapshotStillExact(Basin,
					Basin.ParentObject.GetPart<LiquidVolume>()))
			{
				return false;
			}
			return Authority.PayloadDigest == KingdomFoundingTransactionRules.PayloadDigest(
				Authority.Kind, Basin.PendingName, Basin.PendingVocation,
				Basin.PendingVillageFaction, Basin.PendingVillageDisplayName,
				Basin.PendingOriginalVolume, Basin.PendingOriginalMaxVolume,
				Basin.PendingCommittedVolume, Basin.PendingCommittedMaxVolume,
				originalEncoded, committedEncoded);
		}

		/// <summary>
		/// Debug/script route for a second city. It uses the same verified, retryable projection as
		/// the basin but has no water receipt to debit. A false result may be resumed by calling the
		/// same name/site again; it never permits Force to overwrite foreign ground.
		/// </summary>
		internal static bool TryFoundSecondWithoutWater(string Name, string Vocation, Zone Site,
			bool Force, out string Failure)
		{
			Failure = "";
			if (!TryEnterFounding(null, null, out var lease))
			{
				Failure = "Another founding callback is already in flight; this nested attempt changed nothing.";
				return false;
			}
			using (lease)
			{
				return TryFoundSecondWithoutWaterCore(Name, Vocation, Site, Force,
					lease, out Failure);
			}
		}

	}
}
