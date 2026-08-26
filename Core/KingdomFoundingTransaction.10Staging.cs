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
		private static bool TryStageFoundingReceipt(r_FounderBasin Basin, GameObject Actor,
			Zone Site, LiquidVolume vessel, KingdomFoundingKind Kind, string transaction,
			string ownerNonce, string payloadDigest, string encodedAuthority,
			string realmFaction, string Name, string Vocation, string VillageFaction,
			string VillageDisplayName, int committedVolume,
			Dictionary<string, int> originalComponents,
			Dictionary<string, int> committedComponents,
			out string StagedFailure,
			out KingdomFoundingResult Failure)
		{
			StagedFailure = null;
			Failure = default(KingdomFoundingResult);
			try
			{
				Basin.PendingKind = Kind;
				Basin.PendingPhase = KingdomFoundingPhase.None;
				Basin.PendingOwnerKind = KingdomFoundingOwnerKind.Basin;
				Basin.PendingTransactionID = transaction;
				Basin.PendingBasinID = Basin.ParentObject.ID;
				Basin.PendingOwnerNonce = ownerNonce;
				Basin.PendingPayloadDigest = payloadDigest;
				Basin.PendingAuthority = encodedAuthority;
				Basin.PendingRealmFaction = realmFaction;
				Basin.PendingName = Name;
				Basin.PendingVocation = Vocation;
				Basin.PendingVillageFaction = VillageFaction;
				Basin.PendingVillageDisplayName = VillageDisplayName;
				Basin.PendingZoneID = Site.ZoneID;
				Basin.PendingRiteX = Actor.CurrentCell.X;
				Basin.PendingRiteY = Actor.CurrentCell.Y;
				Basin.PendingOriginalVolume = vessel.Volume;
				Basin.PendingOriginalMaxVolume = vessel.MaxVolume;
				Basin.PendingOriginalComponents = originalComponents;
				Basin.PendingCommittedVolume = committedVolume;
				Basin.PendingCommittedMaxVolume = vessel.MaxVolume;
				Basin.PendingCommittedComponents = committedComponents;
				Basin.PendingChronicleRecorded = false;
				Basin.PendingChronicleStage = 0;
				Basin.PendingChronicleDisposition = KingdomChronicleDisposition.None;
				Basin.PendingChronicleEventID = FoundingEventID(Kind,
					transaction, "chronicle");
			}
			catch (Exception ex)
			{
				bool cleared = ExactAuthorityMarkersAbsent(encodedAuthority,
					realmFaction, VillageFaction, Site) && SafeClearReceipt(Basin);
				Failure = Result(cleared ? KingdomFoundingOutcome.Refused :
						KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"The founding receipt could not be staged before the pour: " + Describe(ex));
				return false;
			}
			if (!ValidateReceiptPayload(Basin, null, vessel, out StagedFailure) ||
				!OriginalSnapshotStillExact(Basin, vessel))
			{
				bool cleared = ExactAuthorityMarkersAbsent(encodedAuthority,
					realmFaction, VillageFaction, Site) && SafeClearReceipt(Basin);
				Failure = Result(cleared ? KingdomFoundingOutcome.Refused :
						KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"The staged founding receipt failed its own strict readback: " +
					StagedFailure);
				return false;
			}
			return true;
		}

		private static bool TryAcquireFoundingReservations(r_FounderBasin Basin, Zone Site,
			LiquidVolume vessel, KingdomFoundingKind Kind, string encodedAuthority,
			string realmFaction, string Name, string Vocation, string VillageFaction,
			string VillageDisplayName, string stagedFailure,
			out KingdomFoundingResult Failure)
		{
			Failure = default(KingdomFoundingResult);
			bool siteWasReserved = HasSiteReservation(Site);
			if (!StageSiteReservation(Site, encodedAuthority, Name, Vocation,
					VillageFaction, VillageDisplayName) ||
				!ValidateReceiptPayload(Basin, Site, vessel, out stagedFailure) ||
				!OriginalSnapshotStillExact(Basin, vessel))
			{
				if (!siteWasReserved)
				{
					ClearStagedSiteSubset(Site, encodedAuthority, Name, Vocation,
						VillageFaction, VillageDisplayName);
				}
				bool cleared = ExactAuthorityMarkersAbsent(encodedAuthority,
					realmFaction, VillageFaction, Site) &&
					(siteWasReserved || !HasSiteReservation(Site)) &&
					SafeClearReceipt(Basin);
				Failure = Result(cleared ? KingdomFoundingOutcome.Refused :
						KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"The exact site reservation could not be staged and read back before the pour: " +
						stagedFailure);
				return false;
			}
			string reservationVillage = Kind == KingdomFoundingKind.VillageCharter
				? VillageFaction : null;
			if (!AcquireGlobalReservation(encodedAuthority, realmFaction,
				reservationVillage))
			{
				if (!siteWasReserved)
					ClearStagedSiteSubset(Site, encodedAuthority, Name, Vocation,
						VillageFaction, VillageDisplayName);
				bool cleared = ExactAuthorityMarkersAbsent(encodedAuthority,
					realmFaction, reservationVillage, Site) &&
					(siteWasReserved || !HasSiteReservation(Site)) && SafeClearReceipt(Basin);
				Failure = Result(cleared ? KingdomFoundingOutcome.Refused :
						KingdomFoundingOutcome.RecoverableFailure,
					KingdomFoundingWaterDisposition.Untouched,
					KingdomFoundingProjection.None,
					"Another founding already reserves this realm or ground.");
				return false;
			}
			return true;
		}
	}
}
