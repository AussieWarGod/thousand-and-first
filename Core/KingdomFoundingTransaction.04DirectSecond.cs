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
		private static bool TryFoundSecondWithoutWaterCore(string Name, string Vocation,
			Zone Site, bool Force, FoundingLease Lease, out string Failure)
		{
			Failure = "";
			if (!KingdomPresentationRules.TryNormalizeName(Name, out Name,
					out string presentationFailure) || Site == null ||
				The.ZoneManager?.ActiveZone != Site)
			{
				Failure = Site == null ? "The second city needs a site."
					: The.ZoneManager?.ActiveZone != Site
						? "Direct founding can inspect only the exact active ground."
						: presentationFailure;
				return false;
			}
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Faction realm = Factions.GetIfExists(system.KingdomFactionName);
			if (!system.Founded || !FactionRegistryCoherent(system.KingdomFactionName, realm) ||
				realm.GetIntProperty("PlayerKingdom") != 1 ||
				realm.GetIntProperty("Village") != 1)
			{
				Failure = "The realm faction is not registered coherently.";
				return false;
			}
			string vocation = KingdomSettlement.IsKnownVocation(Vocation)
				? Vocation : KingdomSettlement.NeutralVocation;
			Cell rite = The.Player?.CurrentCell;
			bool riteIsHere = rite != null && rite.ParentZone == Site;
			int riteX = riteIsHere ? rite.X : (Site.Width / 2);
			int riteY = riteIsHere ? rite.Y : (Site.Height / 2);
			KingdomFoundingAuthority authority;
			string externalBinding = null;
			bool hasSite = HasSiteReservation(Site);
			string publishedAuthority = Site.GetZoneProperty(
				SecondPublicationAuthorityProperty, null);
			if (!string.IsNullOrEmpty(publishedAuthority) && !hasSite)
			{
				Failure = "This ground already carries a completed second-city publication.";
				return false;
			}
			if (hasSite)
			{
				if (!TryReadSiteReservation(Site, out authority, out var storedName,
					out var storedVocation, out var storedVillage, out var storedDisplay,
					out var storedTick) || authority.OwnerKind != KingdomFoundingOwnerKind.Direct ||
					authority.Kind != KingdomFoundingKind.SecondCity || storedName != Name ||
					storedVocation != vocation || !string.IsNullOrEmpty(storedVillage) ||
					!string.IsNullOrEmpty(storedDisplay) || authority.RealmFaction !=
						system.KingdomFactionName || authority.ZoneID != Site.ZoneID)
				{
					Failure = "This ground carries a quarantined or foreign founding reservation.";
					return false;
				}
				riteX = authority.RiteX;
				riteY = authority.RiteY;
				if (!TryResolveDirectExternalBinding(Site, authority, Name, vocation,
					out externalBinding, out Failure)) return false;
			}
			else
			{
				if (!TryChooseExternalBinding(Site, KingdomFoundingKind.SecondCity,
					out externalBinding, out Failure)) return false;
				authority = NewAuthority(KingdomFoundingKind.SecondCity,
					KingdomFoundingOwnerKind.Direct, Guid.NewGuid().ToString("N"),
					Guid.NewGuid().ToString("N"), system.KingdomFactionName, Site.ZoneID,
					riteX, riteY, DirectPayloadDigest(KingdomFoundingKind.SecondCity,
						Name, vocation, null, null, externalBinding));
			}
			string encodedAuthority = KingdomFoundingTransactionRules.FormatAuthority(authority);
			if (!string.IsNullOrEmpty(publishedAuthority) &&
				publishedAuthority != encodedAuthority)
			{
				Failure = "This ground carries another transaction's city publication.";
				return false;
			}
			if (encodedAuthority == null || !Lease.Bind(encodedAuthority, null))
			{
				Failure = "Another founding already holds this realm or site reservation.";
				return false;
			}
			// Site is durable recovery authority for direct founding. Publish it before broad
			// realm/global locks; an interrupted cleanup that already released those locks can
			// therefore reacquire the same exact authority instead of minting a replacement.
			if (!StageSiteReservation(Site, encodedAuthority, Name, vocation, null, null) ||
				(!string.IsNullOrEmpty(externalBinding) &&
				 !TryStageExternalBinding(Site, KingdomFoundingKind.SecondCity,
					encodedAuthority, externalBinding, out Failure)))
			{
				if (hasSite)
				{
					Failure = "The exact direct second-founding reservation is malformed and remains quarantined.";
					return false;
				}
				bool cleanedExternal = string.IsNullOrEmpty(externalBinding) ||
					RollbackExternalBinding(Site, encodedAuthority, externalBinding,
						PublicationObserved: false);
				bool cleanedSite = ClearStagedSiteSubset(Site, encodedAuthority, Name,
					vocation, null, null);
				Failure = cleanedExternal && cleanedSite
					? "Another founding already holds this realm or site reservation."
					: "The direct second-founding reservation remains staged for exact cleanup.";
				return false;
			}
			if (!AcquireGlobalReservation(encodedAuthority,
				system.KingdomFactionName, null))
			{
				// Existing exact site is retry receipt. New site also remains exact: clearing it
				// here would recreate site-last cleanup and lose authority across callback cuts.
				Failure = "Another founding holds the realm lock; this exact site receipt can retry.";
				return false;
			}
			bool published = SecondPublished(system, Name, Site.ZoneID,
				authority.TransactionID) &&
				PublishedSecondAuthorityMatches(Site, encodedAuthority) &&
				SiteReservationMatches(Site, encodedAuthority);
			bool targetIsExactSeat = SecondIsExactSeat(system, Name, Site.ZoneID,
				authority.TransactionID);
			bool targetIsExactNonSeat = SecondIsExactNonSeat(system, Name, Site.ZoneID,
				authority.TransactionID);
			if (!KingdomFoundingTransactionRules.SecondRecoveryCanProject(
				system.SettlementCount, KingdomSettlement.MaxSettlements,
				system.NonSeatSettlementCount < KingdomSettlementTopologyRules.MaxNonSeatSettlements,
				targetIsExactSeat, targetIsExactNonSeat, published))
			{
				bool forwardRedo = DirectSecondHasForwardRedo(system, Site, authority,
					encodedAuthority);
				if (!forwardRedo)
				{
					if (!ClearExactReservationSet(Site, encodedAuthority,
						system.KingdomFactionName, null, externalBinding))
					{
						Failure = "The invalid direct second-founding reservation remains staged for exact cleanup.";
						return false;
					}
				}
				Failure = forwardRedo
					? "The realm's city seats changed, but this exact forward-recovery receipt remains."
					: "The realm's city seats no longer match this stale transaction; its exact reservations were cleared.";
				return false;
			}
			bool partialClaim = Site.GetZoneProperty("faction", null) ==
				system.KingdomFactionName || realm.HolyPlaces.Contains(Site.ZoneID);
			r_FounderBasin carrier = new r_FounderBasin
			{
				PendingKind = KingdomFoundingKind.SecondCity,
				PendingOwnerKind = KingdomFoundingOwnerKind.Direct,
				PendingPhase = partialClaim || published
					? KingdomFoundingPhase.PublicationCommitted
					: KingdomFoundingPhase.WaterCommitted,
				PendingTransactionID = authority.TransactionID,
				PendingBasinID = "direct:" + authority.TransactionID,
				PendingOwnerNonce = authority.OwnerNonce,
				PendingPayloadDigest = authority.PayloadDigest,
				PendingAuthority = encodedAuthority,
				PendingRealmFaction = system.KingdomFactionName,
				PendingName = Name,
				PendingVocation = vocation,
				PendingZoneID = Site.ZoneID,
				PendingRiteX = riteX,
				PendingRiteY = riteY,
				PendingChronicleEventID = FoundingEventID(
					authority.Kind, authority.TransactionID, "chronicle"),
				PendingChronicleDisposition = KingdomChronicleDisposition.None
			};
			carrier.PendingExternalBinding = externalBinding;
			if (!Lease.Bind(encodedAuthority, carrier))
			{
				bool cleared = ClearExactReservationSet(Site, encodedAuthority,
					system.KingdomFactionName, null, externalBinding);
				Failure = "The exact direct founding authority left its synchronous guard." +
					(cleared ? "" : " Its exact reservation remains pending cleanup.");
				return false;
			}
			try
			{
				if (!RevalidateExternalBinding(carrier, Site, out Failure))
					throw new InvalidOperationException(Failure);
				KingdomFoundingProjection projection = KingdomFoundingProjection.Water;
				PublishSecond(carrier, The.Player, Site, ref projection, Force);
				carrier.PendingPhase = KingdomFoundingPhase.Complete;
				if (!FinishDirectReservations(Site, encodedAuthority,
					system.KingdomFactionName, externalBinding))
				{
					throw new InvalidOperationException(
						"The completed second founding could not clear its exact reservation.");
				}
				return true;
			}
			catch (Exception ex)
			{
				Failure = Describe(ex);
				if (!DetectPublication(carrier, Site) &&
					carrier.PendingPhase != KingdomFoundingPhase.PublicationCommitted)
				{
					if (!ClearExactReservationSet(Site, encodedAuthority,
						system.KingdomFactionName, null, externalBinding))
					{
						Failure += " Exact direct reservation cleanup remains pending.";
					}
				}
				KingdomLog.Log("second founding remains recoverable: " + Failure);
				return false;
			}
		}

		/// <summary>Debug/script first founding with same authority and verification as basin flow,
		/// but no liquid debit.</summary>
	}
}
