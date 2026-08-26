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

		/// <summary>
		/// Makes the debug reset safe to run. Paid basin authority is never discarded: reset
		/// refuses before mutation while one is in flight or retained anywhere it can own the
		/// current transaction. Stable realm-owned claim/direct markers on the current zone are
		/// removed exactly and verified so a later debug founding can use that ground again.
		/// </summary>
		internal static bool TryPrepareDebugReset(KingdomSystem System, GameObject Actor,
			Zone Site, out string Failure)
		{
			Failure = "";
			if (System == null || Actor == null)
			{
				Failure = "The founder or kingdom system is unavailable.";
				return false;
			}
			lock (InFlightSync)
			{
				if (InFlight != null)
				{
					Failure = "A founding callback is in flight.";
					return false;
				}
			}
			if (ObjectTreeHasPaidReceipt(Actor))
			{
				Failure = "A founder's basin retains a paid or staged founding receipt.";
				return false;
			}
			if (Site != null)
			{
				foreach (GameObject root in Site.GetObjects())
				{
					if (!ReferenceEquals(root, Actor) && ObjectTreeHasPaidReceipt(root))
					{
						Failure = "A basin on this ground retains a paid or staged founding receipt.";
						return false;
					}
				}
			}

			HashSet<string> realms = new HashSet<string>(StringComparer.Ordinal);
			if (!string.IsNullOrEmpty(System.KingdomFactionName))
			{
				realms.Add(System.KingdomFactionName);
			}
			if (!string.IsNullOrEmpty(System.ExiledFactionName))
			{
				realms.Add(System.ExiledFactionName);
			}
			bool realmListsSite = Site != null &&
				(System.ClaimedZones.Contains(Site.ZoneID) ||
				 (System.Away != null && System.Away.ClaimedZones.Contains(Site.ZoneID)) ||
				 (System.ExiledSeat != null &&
				  System.ExiledSeat.ClaimedZones.Contains(Site.ZoneID)) ||
				 (System.ExiledAway != null &&
				  System.ExiledAway.ClaimedZones.Contains(Site.ZoneID)));
			string zoneFaction = Site?.GetZoneProperty("faction", null);
			if (realmListsSite && !string.IsNullOrEmpty(zoneFaction) &&
				!realms.Contains(zoneFaction))
			{
				Failure = "The current zone carries a foreign faction claim.";
				return false;
			}
			bool realmOwnsSite = Site != null &&
				(realmListsSite || (!string.IsNullOrEmpty(zoneFaction) &&
				 realms.Contains(zoneFaction)));

			string liveAuthority = null;
			string global = The.Game?.GetStringGameState(GlobalReservationState, null);
			if (!string.IsNullOrEmpty(global) &&
				!AcceptDirectResetAuthority(global, realms, Site,
					ref liveAuthority, out Failure))
			{
				return false;
			}
			foreach (string realmName in realms)
			{
				Faction realm = Factions.GetIfExists(realmName);
				string bound = realm?.GetStringProperty(RealmReservationProperty, null);
				if (!string.IsNullOrEmpty(bound) &&
					!AcceptDirectResetAuthority(bound, realms, Site,
						ref liveAuthority, out Failure))
				{
					return false;
				}
				if (realm != null && realm.GetIntProperty(PendingFactionProperty) == 1)
				{
					string pending = realm.GetStringProperty(
						PendingFactionAuthorityProperty, null);
					if (!AcceptDirectResetAuthority(pending, realms, Site,
							ref liveAuthority, out Failure) ||
						realm.GetStringProperty(PendingFactionTransactionProperty, null) !=
							ParseTransaction(pending))
					{
						Failure = "The realm faction retains a paid or malformed pending founding.";
						return false;
					}
				}
			}

			bool clearLegacyDirect = false;
			if (Site != null && HasSiteReservation(Site))
			{
				string siteAuthority = Site.GetZoneProperty(SiteReservationProperty, null);
				if (!string.IsNullOrEmpty(siteAuthority))
				{
					if (!TryReadSiteReservation(Site, out var parsedSite,
						out var _, out var _, out var _, out var _, out var _) ||
						!AcceptDirectResetAuthority(siteAuthority, realms, Site,
							ref liveAuthority, out Failure))
					{
						Failure = "The current zone retains a paid, foreign, or malformed founding reservation.";
						return false;
					}
				}
				else if (!realmOwnsSite || !LegacyDirectResetMarkersAreExact(
					Site, realms))
				{
					Failure = "The current zone carries partial or foreign legacy founding markers.";
					return false;
				}
				else
				{
					clearLegacyDirect = true;
				}
			}

			string published = Site?.GetZoneProperty(
				SecondPublicationAuthorityProperty, null);
			if (!string.IsNullOrEmpty(published) &&
				(!realmOwnsSite ||
				 !KingdomFoundingTransactionRules.TryParseAuthority(published,
					out var publishedAuthority) ||
				 publishedAuthority.Kind != KingdomFoundingKind.SecondCity ||
				 publishedAuthority.ZoneID != Site.ZoneID ||
				 !realms.Contains(publishedAuthority.RealmFaction)))
			{
				Failure = "The current zone carries a foreign or malformed second-founding publication.";
				return false;
			}

			// Everything above is read-only. From here on no paid or foreign authority is touched.
			if (!string.IsNullOrEmpty(liveAuthority))
			{
				if (Site != null && SiteReservationMatches(Site, liveAuthority) &&
					!ReleaseSiteReservation(Site, liveAuthority))
				{
					Failure = "The exact direct site reservation could not be cleared.";
					return false;
				}
				if (!KingdomFoundingTransactionRules.TryParseAuthority(liveAuthority,
						out var parsedLive) ||
					!ReleaseGlobalReservation(liveAuthority,
						parsedLive.RealmFaction, null))
				{
					Failure = "The exact direct global/faction reservation could not be cleared.";
					return false;
				}
			}
			if (clearLegacyDirect)
			{
				ClearLegacyDirectRecovery(Site);
			}
			if (Site != null && HasSiteReservation(Site))
			{
				Failure = "A site founding marker remains after exact cleanup.";
				return false;
			}

			if (realmOwnsSite)
			{
				Site.RemoveZoneProperty("faction");
				Site.RemoveZoneProperty(ClaimChronicleEventProperty);
				Site.RemoveZoneProperty(ClaimChronicleStageProperty);
				Site.RemoveZoneProperty(ClaimChronicleDispositionProperty);
				Site.RemoveZoneProperty(ClaimFoundingProperty);
				Site.RemoveZoneProperty(SecondChronicleProperty);
				Site.RemoveZoneProperty(SecondChronicleStageProperty);
				Site.RemoveZoneProperty(SecondChronicleDispositionProperty);
				Site.RemoveZoneProperty(SecondRestoredProperty);
				Site.RemoveZoneProperty(SecondPublicationAuthorityProperty);
			}
			foreach (string realmName in realms)
			{
				Faction realm = Factions.GetIfExists(realmName);
				if (realm != null)
				{
					realm.HolyPlaces.Remove(Site?.ZoneID);
					if (!KingdomFounding.ClearDebugFoundingMarkers(realm))
					{
						Failure = "The exact realm founding markers could not be cleared.";
						return false;
					}
					if (!string.IsNullOrEmpty(realm.GetStringProperty(
						RealmReservationProperty, null)))
					{
						Failure = "A realm faction reservation remains after exact cleanup.";
						return false;
					}
				}
			}
			if (!string.IsNullOrEmpty(The.Game?.GetStringGameState(
					GlobalReservationState, null)) ||
				(Site != null && HasSiteReservation(Site)) ||
				(realmOwnsSite && (!string.IsNullOrEmpty(
					Site.GetZoneProperty("faction", null)) ||
					Site.HasZoneProperty(ClaimChronicleEventProperty) ||
					Site.HasZoneProperty(ClaimChronicleStageProperty) ||
					Site.HasZoneProperty(ClaimChronicleDispositionProperty) ||
					Site.HasZoneProperty(ClaimFoundingProperty) ||
					Site.HasZoneProperty(SecondPublicationAuthorityProperty))))
			{
				Failure = "Founding or claim cleanup did not retain an empty exact state.";
				return false;
			}
			return true;
		}

	}
}
