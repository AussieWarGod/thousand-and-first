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
		private static bool ClearExactReservationSet(Zone Site, string Authority,
			string Realm, string VillageFaction, string ExternalBinding = null)
		{
			if (Site == null || string.IsNullOrEmpty(Authority))
			{
				return false;
			}
			if (KingdomFoundingTransactionRules.TryParseAuthority(Authority,
				out var parsedAuthority) && parsedAuthority.Kind == KingdomFoundingKind.SecondCity)
			{
				KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
				if (system == null || !system.Founded ||
					system.KingdomFactionName != parsedAuthority.RealmFaction ||
					!system.TryAbortPendingSettlementIdentity(
						parsedAuthority.TransactionID, parsedAuthority.ZoneID, Authority,
						out string pendingFailure))
					return false;
			}
			// Clear broad reservation before site evidence. A save cut therefore leaves the
			// exact site marker available to reacquire authority; never an ownerless global lock.
			if (!ReleaseGlobalReservation(Authority, Realm, VillageFaction))
			{
				return false;
			}
			if (KingdomExternalOwnershipBindingRuntime.HasStage(Site) &&
				!RollbackExternalBinding(Site, Authority, ExternalBinding,
					PublicationObserved: false))
			{
				return false;
			}
			if (HasSiteReservation(Site) &&
				(!SiteReservationMatches(Site, Authority) ||
				 !ReleaseSiteReservation(Site, Authority)))
			{
				return false;
			}
			return !HasSiteReservation(Site) &&
				GlobalReservationMarkersAbsent(Realm, VillageFaction);
		}

		internal static bool HasSiteReservation(Zone Site)
		{
			return Site != null && (Site.HasZoneProperty(SiteReservationProperty) ||
				Site.HasZoneProperty(SiteReservationNameProperty) ||
				Site.HasZoneProperty(SiteReservationVocationProperty) ||
				Site.HasZoneProperty(SiteReservationVillageProperty) ||
				Site.HasZoneProperty(SiteReservationDisplayProperty) ||
				Site.HasZoneProperty(SiteReservationTickProperty) ||
				Site.HasZoneProperty(DirectRecoveryNameProperty) ||
				Site.HasZoneProperty(DirectRecoveryVocationProperty) ||
				Site.HasZoneProperty(DirectRecoveryRiteXProperty) ||
				Site.HasZoneProperty(DirectRecoveryRiteYProperty) ||
				Site.HasZoneProperty(DirectRecoveryTickProperty) ||
				Site.HasZoneProperty(DirectRecoveryRealmProperty) ||
				Site.HasZoneProperty(DirectRecoveryTransactionProperty));
		}

		private static bool CompletedSiteReservationSubsetMatches(Zone Site,
			r_FounderBasin Basin)
		{
			if (Site == null || Basin == null || Site.ZoneID != Basin.PendingZoneID ||
				Site.HasZoneProperty(DirectRecoveryNameProperty) ||
				Site.HasZoneProperty(DirectRecoveryVocationProperty) ||
				Site.HasZoneProperty(DirectRecoveryRiteXProperty) ||
				Site.HasZoneProperty(DirectRecoveryRiteYProperty) ||
				Site.HasZoneProperty(DirectRecoveryTickProperty) ||
				Site.HasZoneProperty(DirectRecoveryRealmProperty) ||
				Site.HasZoneProperty(DirectRecoveryTransactionProperty))
			{
				return false;
			}
			return (!Site.HasZoneProperty(SiteReservationProperty) ||
					Site.GetZoneProperty(SiteReservationProperty, null) == Basin.PendingAuthority) &&
				(!Site.HasZoneProperty(SiteReservationNameProperty) ||
					Site.GetZoneProperty(SiteReservationNameProperty, null) == Basin.PendingName) &&
				(!Site.HasZoneProperty(SiteReservationVocationProperty) ||
					Site.GetZoneProperty(SiteReservationVocationProperty, null) ==
						Basin.PendingVocation) &&
				(!Site.HasZoneProperty(SiteReservationVillageProperty) ||
					Site.GetZoneProperty(SiteReservationVillageProperty, null) ==
						Basin.PendingVillageFaction) &&
				(!Site.HasZoneProperty(SiteReservationDisplayProperty) ||
					Site.GetZoneProperty(SiteReservationDisplayProperty, null) ==
						Basin.PendingVillageDisplayName) &&
				(!Site.HasZoneProperty(SiteReservationTickProperty) ||
					(long.TryParse(Site.GetZoneProperty(SiteReservationTickProperty, null),
						out var tick) && tick >= 0L));
		}

		private static bool ClearCompletedSiteReservation(Zone Site, r_FounderBasin Basin)
		{
			if (!CompletedSiteReservationSubsetMatches(Site, Basin))
			{
				return false;
			}
			if (!ClearFrozenSecondIdentity(Site, Basin.PendingAuthority)) return false;
			Site.RemoveZoneProperty(SiteReservationNameProperty);
			Site.RemoveZoneProperty(SiteReservationVocationProperty);
			Site.RemoveZoneProperty(SiteReservationVillageProperty);
			Site.RemoveZoneProperty(SiteReservationDisplayProperty);
			Site.RemoveZoneProperty(SiteReservationTickProperty);
			Site.RemoveZoneProperty(SiteReservationProperty);
			return !HasSiteReservation(Site);
		}

		private static bool ClearStagedSiteSubset(Zone Site, string Authority, string Name,
			string Vocation, string VillageFaction, string VillageDisplay)
		{
			if (Site == null || string.IsNullOrEmpty(Authority) ||
				Site.HasZoneProperty(DirectRecoveryNameProperty) ||
				Site.HasZoneProperty(DirectRecoveryVocationProperty) ||
				Site.HasZoneProperty(DirectRecoveryRiteXProperty) ||
				Site.HasZoneProperty(DirectRecoveryRiteYProperty) ||
				Site.HasZoneProperty(DirectRecoveryTickProperty) ||
				Site.HasZoneProperty(DirectRecoveryRealmProperty) ||
				Site.HasZoneProperty(DirectRecoveryTransactionProperty) ||
				(Site.HasZoneProperty(SiteReservationProperty) &&
				 Site.GetZoneProperty(SiteReservationProperty, null) != Authority) ||
				(Site.HasZoneProperty(SiteReservationNameProperty) &&
				 Site.GetZoneProperty(SiteReservationNameProperty, null) != Name) ||
				(Site.HasZoneProperty(SiteReservationVocationProperty) &&
				 Site.GetZoneProperty(SiteReservationVocationProperty, null) != Vocation) ||
				(Site.HasZoneProperty(SiteReservationVillageProperty) &&
				 Site.GetZoneProperty(SiteReservationVillageProperty, null) != VillageFaction) ||
				(Site.HasZoneProperty(SiteReservationDisplayProperty) &&
				 Site.GetZoneProperty(SiteReservationDisplayProperty, null) != VillageDisplay) ||
				(Site.HasZoneProperty(SiteReservationTickProperty) &&
				 (!long.TryParse(Site.GetZoneProperty(SiteReservationTickProperty, null),
					out var tick) || tick < 0L)))
			{
				return false;
			}
			if (!ClearFrozenSecondIdentity(Site, Authority)) return false;
			Site.RemoveZoneProperty(SiteReservationNameProperty);
			Site.RemoveZoneProperty(SiteReservationVocationProperty);
			Site.RemoveZoneProperty(SiteReservationVillageProperty);
			Site.RemoveZoneProperty(SiteReservationDisplayProperty);
			Site.RemoveZoneProperty(SiteReservationTickProperty);
			Site.RemoveZoneProperty(SiteReservationProperty);
			return !HasSiteReservation(Site);
		}

		internal static bool SiteReservationMatches(Zone Site, string Authority)
		{
			return Site != null && !string.IsNullOrEmpty(Authority) &&
				Site.GetZoneProperty(SiteReservationProperty, null) == Authority &&
				KingdomFoundingTransactionRules.TryParseAuthority(Authority, out var parsed) &&
				parsed.ZoneID == Site.ZoneID;
		}

	}
}
