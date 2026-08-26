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
		private static bool StageSiteReservation(Zone Site, string Authority, string Name,
			string Vocation, string VillageFaction, string VillageDisplay)
		{
			if (Site == null || string.IsNullOrEmpty(Name) ||
				!KingdomFoundingTransactionRules.TryParseAuthority(Authority, out var parsed) ||
				parsed.ZoneID != Site.ZoneID)
			{
				return false;
			}
			string published = Site.GetZoneProperty(
				SecondPublicationAuthorityProperty, null);
			if (parsed.Kind == KingdomFoundingKind.SecondCity &&
				!string.IsNullOrEmpty(published) && published != Authority)
			{
				return false;
			}
			string existing = Site.GetZoneProperty(SiteReservationProperty, null);
			bool hasExisting = HasSiteReservation(Site);
			if (hasExisting && existing != Authority)
			{
				return false;
			}
			if (hasExisting)
			{
				return TryReadSiteReservation(Site, out var existingAuthority,
					out var existingName, out var existingVocation,
					out var existingVillage, out var existingDisplay,
					out var existingTick) &&
					KingdomFoundingTransactionRules.FormatAuthority(existingAuthority) ==
						Authority && existingName == Name &&
					existingVocation == Vocation &&
					existingVillage == VillageFaction &&
					existingDisplay == VillageDisplay;
			}
			try
			{
				Site.SetZoneProperty(SiteReservationProperty, Authority);
				Site.SetZoneProperty(SiteReservationNameProperty, Name);
				SetOrRemoveZoneProperty(Site, SiteReservationVocationProperty, Vocation);
				SetOrRemoveZoneProperty(Site, SiteReservationVillageProperty, VillageFaction);
				SetOrRemoveZoneProperty(Site, SiteReservationDisplayProperty, VillageDisplay);
				Site.SetZoneProperty(SiteReservationTickProperty,
					The.Game.TimeTicks.ToString());
				return TryReadSiteReservation(Site, out var read, out var readName,
					out var readVocation, out var readVillage, out var readDisplay,
					out var readTick) && KingdomFoundingTransactionRules.FormatAuthority(read) ==
						Authority && readName == Name && readVocation == Vocation &&
						readVillage == VillageFaction && readDisplay == VillageDisplay;
			}
			catch
			{
				return false;
			}
		}

		private static void SetOrRemoveZoneProperty(Zone Site, string Property,
			string Value)
		{
			if (string.IsNullOrEmpty(Value))
			{
				Site.RemoveZoneProperty(Property);
			}
			else
			{
				Site.SetZoneProperty(Property, Value);
			}
		}

		private static bool TryReadSiteReservation(Zone Site,
			out KingdomFoundingAuthority Authority, out string Name, out string Vocation,
			out string VillageFaction, out string VillageDisplay, out long Tick)
		{
			Authority = default(KingdomFoundingAuthority);
			Name = Vocation = VillageFaction = VillageDisplay = null;
			Tick = -1L;
			if (Site == null || !KingdomFoundingTransactionRules.TryParseAuthority(
				Site.GetZoneProperty(SiteReservationProperty, null), out Authority) ||
				Authority.ZoneID != Site.ZoneID)
			{
				return false;
			}
			Name = Site.GetZoneProperty(SiteReservationNameProperty, null);
			Vocation = Site.GetZoneProperty(SiteReservationVocationProperty, null);
			VillageFaction = Site.GetZoneProperty(SiteReservationVillageProperty, null);
			VillageDisplay = Site.GetZoneProperty(SiteReservationDisplayProperty, null);
			return !string.IsNullOrEmpty(Name) && Name.Length <= 256 &&
				(Vocation == null || Vocation.Length <= 64) &&
				(VillageFaction == null || VillageFaction.Length <= 256) &&
				(VillageDisplay == null || VillageDisplay.Length <= 256) &&
				long.TryParse(Site.GetZoneProperty(SiteReservationTickProperty, null), out Tick) &&
				Tick >= 0L;
		}

		private static bool ReleaseSiteReservation(Zone Site, string Authority)
		{
			if (!SiteReservationMatches(Site, Authority))
			{
				return false;
			}
			if (!ClearFrozenSecondIdentity(Site, Authority)) return false;
			Site.RemoveZoneProperty(SiteReservationProperty);
			Site.RemoveZoneProperty(SiteReservationNameProperty);
			Site.RemoveZoneProperty(SiteReservationVocationProperty);
			Site.RemoveZoneProperty(SiteReservationVillageProperty);
			Site.RemoveZoneProperty(SiteReservationDisplayProperty);
			Site.RemoveZoneProperty(SiteReservationTickProperty);
			ClearLegacyDirectRecovery(Site);
			return !HasSiteReservation(Site);
		}

		private static bool ClearFrozenSecondIdentity(Zone Site, string Authority)
		{
			if (Site == null || !KingdomFoundingTransactionRules.TryParseAuthority(
				Authority, out var parsed) || parsed.ZoneID != Site.ZoneID)
				return false;
			KingdomSystem currentSystem = null;
			if (parsed.Kind == KingdomFoundingKind.SecondCity)
			{
				currentSystem = The.Game?.GetSystem<KingdomSystem>();
				if (currentSystem == null || !currentSystem.Founded ||
					currentSystem.KingdomFactionName != parsed.RealmFaction) return false;
			}
			bool any = Site.HasZoneProperty(SecondIdentityTransactionProperty) ||
				Site.HasZoneProperty(SecondIdentityRealmProperty) ||
				Site.HasZoneProperty(SecondIdentitySettlementProperty) ||
				Site.HasZoneProperty(SecondIdentityVersionProperty) ||
				Site.HasZoneProperty(SecondIdentityOriginProperty);
			if (!any) return true;
			string transaction = Site.GetZoneProperty(
				SecondIdentityTransactionProperty, null);
			string realm = Site.GetZoneProperty(SecondIdentityRealmProperty, null);
			string settlement = Site.GetZoneProperty(
				SecondIdentitySettlementProperty, null);
			if ((transaction != null && transaction != parsed.TransactionID) ||
				(realm != null && (currentSystem == null ||
				 realm != currentSystem.RealmId)) ||
				(Site.HasZoneProperty(SecondIdentityVersionProperty) &&
				 Site.GetZoneProperty(SecondIdentityVersionProperty, null) !=
					KingdomIdentityRules.RulesVersion.ToString()) ||
				(Site.HasZoneProperty(SecondIdentityOriginProperty) &&
				 Site.GetZoneProperty(SecondIdentityOriginProperty, null) !=
					((int)KingdomIdentityOrigin.FoundingTransaction).ToString()))
				return false;
			if (settlement != null)
			{
				string expected;
				KingdomIdentityFault fault;
				if (realm == null || !KingdomIdentityRules.TryMintSettlement(realm,
					parsed.TransactionID, out expected, out fault) || expected != settlement)
					return false;
			}
			Site.RemoveZoneProperty(SecondIdentityTransactionProperty);
			Site.RemoveZoneProperty(SecondIdentityRealmProperty);
			Site.RemoveZoneProperty(SecondIdentitySettlementProperty);
			Site.RemoveZoneProperty(SecondIdentityVersionProperty);
			Site.RemoveZoneProperty(SecondIdentityOriginProperty);
			return !Site.HasZoneProperty(SecondIdentityTransactionProperty) &&
				!Site.HasZoneProperty(SecondIdentityRealmProperty) &&
				!Site.HasZoneProperty(SecondIdentitySettlementProperty) &&
				!Site.HasZoneProperty(SecondIdentityVersionProperty) &&
				!Site.HasZoneProperty(SecondIdentityOriginProperty);
		}

		private static void ClearLegacyDirectRecovery(Zone Site)
		{
			if (Site == null)
			{
				return;
			}
			Site.RemoveZoneProperty(DirectRecoveryNameProperty);
			Site.RemoveZoneProperty(DirectRecoveryVocationProperty);
			Site.RemoveZoneProperty(DirectRecoveryRiteXProperty);
			Site.RemoveZoneProperty(DirectRecoveryRiteYProperty);
			Site.RemoveZoneProperty(DirectRecoveryTickProperty);
			Site.RemoveZoneProperty(DirectRecoveryRealmProperty);
			Site.RemoveZoneProperty(DirectRecoveryTransactionProperty);
		}

	}
}
